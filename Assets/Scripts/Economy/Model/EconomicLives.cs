using System;
using System.Collections.Generic;
using UnityEngine;
using Why.Economy.Data;
using Why.Humans.Smv;

namespace Why.Economy.Model
{
    /// <summary>What one simulated person (one lifeline) is like: drawn once at birth, deterministic per person.</summary>
    public sealed class PersonTraits
    {
        /// <summary>Big Five personality z-scores: conscientiousness, neuroticism, agreeableness, openness, extraversion.</summary>
        public float C, N, A, O, E;

        /// <summary>Present bias (beta-delta model; 1 = none, lower = the present weighs more).</summary>
        public float Beta = 0.8f;

        /// <summary>Loss aversion (losses weigh this many times as much as gains).</summary>
        public float Lambda = 2f;

        /// <summary>Sensitivity to how others live (z-score).</summary>
        public float Comparison;

        /// <summary>Belief that outcomes depend on oneself (z-score).</summary>
        public float Locus;

        /// <summary>Earnings rank within the cohort (0..1) and the parent's rank it partly inherits.</summary>
        public float Rank, ParentRank;

        /// <summary>Propensity to decide by reason (the higher OS) rather than by feeling (the default OS), 0..1.</summary>
        public float Reason0 = 0.5f;

        /// <summary>Strategy in repeated games with others.</summary>
        public PdStrategy Strategy = PdStrategy.TitForTat;

        /// <summary>Tribe: 0 = Democrat, 1 = Republican, 2 = independent.</summary>
        public byte Tribe = 2;

        /// <summary>Weights of the desires (food, collective, law, shelter, sex) and fears (starvation, isolation, murder, exposure, childless).</summary>
        public float[] Desires = new float[5], Fears = new float[5];
    }

    /// <summary>One person in one calendar year (dollars are nominal, per person; a couple's household is split evenly).</summary>
    public struct PersonYear
    {
        public float Age;
        public bool Adult, Married, Employed, SelfEmployed, Homeowner, InControl;

        /// <summary>Index of the industry the person works in (EconomyData.Industries), or -1.</summary>
        public short Industry;

        /// <summary>Income by source and what is left after taxes ($ per person-year).</summary>
        public float Wages, CapitalIncome, Transfers, Taxes, Disposable;

        /// <summary>Spending, saving (negative = borrowing), net worth and debt ($).</summary>
        public float Spending, Saving, Wealth, Debt;

        /// <summary>Spending share of each category, in the order of EconomyData.CategoryIds (saving excluded; sums to 1).</summary>
        public float Necessities, Escapism, Jeopardy, Status, Growth, Collective;

        /// <summary>Share of the spending's motive that is fear (0..1).</summary>
        public float FearShare;

        /// <summary>Share of decisions made by reason, the higher OS (0..1).</summary>
        public float Reason;

        /// <summary>Orientation toward the future (0 = the present only, 1 = strategy for the future).</summary>
        public float Future;

        /// <summary>Share of spending that buys a fantasy (0..1).</summary>
        public float Fantasy;

        /// <summary>How much the person steers their own life path (0..1); InControl above the calibrated threshold.</summary>
        public float Agency;

        /// <summary>Share of the person's moves that cooperate with the people they deal with this year (0..1).</summary>
        public float Cooperation;

        /// <summary>Wealth group by net worth this year (0 bottom 50% .. 3 top 1%).</summary>
        public byte WealthGroup;

        /// <summary>Category share by index of EconomyData.CategoryIds (6 = saving rate of disposable income).</summary>
        public float Category(int c)
        {
            switch (c)
            {
                case 0: return Necessities;
                case 1: return Escapism;
                case 2: return Jeopardy;
                case 3: return Status;
                case 4: return Growth;
                case 5: return Collective;
                default: return Disposable > 0 ? Saving / Disposable : 0;
            }
        }
    }

    /// <summary>A person worth meeting (the inspector's N key and the tour): who they are in the model and why.</summary>
    public struct Notable
    {
        public int Person;
        public string Role;
        public string Why;
    }

    /// <summary>Population totals of one year (dollars scaled up by the people each line stands for; $B nominal).</summary>
    public sealed class PopulationYear
    {
        public int Year;
        public int AdultLines;
        public double Wages, CapitalIncome, Transfers, Taxes, Disposable, Spending, Saving, Wealth, Debt;

        /// <summary>Spending by category ($B), in the order of EconomyData.CategoryIds (saving last).</summary>
        public readonly double[] Categories = new double[7];

        /// <summary>Shares of adults: in control of their path, and by wealth group.</summary>
        public float InControlShare;

        /// <summary>Spending-weighted means over adults.</summary>
        public float MeanFantasy, MeanFear, MeanReason, MeanFuture, MeanCooperation;

        /// <summary>Share of adults playing each strategy (PdStrategy order).</summary>
        public readonly float[] Strategies = new float[7];
    }

    /// <summary>
    /// The economic life of every simulated person of the United States population (one lifeline = 100,000 people):
    /// traits drawn at birth (personality, present bias, loss aversion, earnings rank partly inherited from a parent,
    /// tribe, a strategy for repeated games), then year by year income (wages by sex, age and rank; business and capital
    /// income; Social Security), households (married couples pool their money, children add needs), taxes, spending on
    /// the notebook's categories tilted by personality and calibrated to the data every year, saving and debt, homes,
    /// inheritance, and the state of mind behind the spending: desire or fear, reason or feeling, the present or the
    /// future, fantasy or control. Runs once on the finished population (<see cref="Prepare"/>), then restyles the
    /// lifelines: the people who own their life path turn gold.
    /// </summary>
    public sealed class EconomicLives : ISmvLineStyle
    {
        readonly EconomyData data;
        readonly int seed;

        public EconomicLives(EconomyData data, int seed)
        {
            this.data = data;
            this.seed = seed;
        }

        public bool Ready { get; private set; }
        public SmvSimulation Sim { get; private set; }
        public int FirstYear { get; private set; }
        public int LastYear { get; private set; }
        public string Log { get; private set; } = "";

        public IReadOnlyList<Notable> Notables => notables;

        readonly List<Notable> notables = new List<Notable>();

        /// <summary>Runs every life on the finished population (worker thread).</summary>
        public void Prepare(SmvSimulation sim)
        {
            // TODO(model): the year-major simulation (see the design notes)
            Sim = sim;
            FirstYear = (int)Math.Floor(EconomyStyle.FirstYear);
            LastYear = data.LastYear;
            Ready = false;
        }

        /// <summary>A person's state in a calendar year; false when they are not alive then or nothing was simulated.</summary>
        public bool TryGet(int person, int year, out PersonYear state)
        {
            state = default;
            return false;
        }

        /// <summary>A person's traits (null when unknown).</summary>
        public PersonTraits Traits(int person) => null;

        /// <summary>Population totals of a year (null when nothing was simulated).</summary>
        public PopulationYear Aggregate(int year) => null;

        /// <summary>A notable person's role, or null.</summary>
        public string RoleOf(int person)
        {
            foreach (Notable n in notables)
            {
                if (n.Person == person) return n.Role;
            }

            return null;
        }

        public void Restyle(SmvPerson person, double time, bool child, ref Color32 tint, ref float intensity)
        {
            // TODO(model): gold for the people in control, dimmer for heavy fantasy
        }
    }
}
