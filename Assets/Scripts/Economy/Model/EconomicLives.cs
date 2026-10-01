using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
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

        /// <summary>
        /// Tribe: 0 = Democrat, 1 = Republican, 2 = independent, in the last year the person lived in the record (the
        /// tribe of each year is <see cref="PersonYear.Tribe"/>).
        /// </summary>
        public byte Tribe = 2;

        /// <summary>Weights of the desires (food, collective, law, shelter, sex) and fears (starvation, isolation, murder, exposure, childless).</summary>
        public float[] Desires = new float[5], Fears = new float[5];

        /// <summary>Place on a left (0) to right (1) axis, partly inherited; the era's party shares turn it into a tribe.</summary>
        public float Lean = 0.5f;

        /// <summary>Propensity to work for oneself (z-like score from openness, extraversion, low loss aversion, locus).</summary>
        public float Enterprise;

        /// <summary>Age at which the person claims Social Security.</summary>
        public float ClaimAge = 65f;
    }

    /// <summary>One person in one calendar year (dollars are nominal, per person; a couple's household is split evenly).</summary>
    public struct PersonYear
    {
        public float Age;
        public bool Adult, Married, Employed, SelfEmployed, Homeowner, InControl;

        /// <summary>Index of the industry the person works in (EconomyData.Industries), or -1.</summary>
        public short Industry;

        /// <summary>Income by source and what is left after taxes ($ per person-year). Wages are employee compensation
        /// (wages and salaries with employers' contributions, as BEA counts personal income); taxes are income and
        /// payroll taxes; transfers include Social Security, Medicare and means-tested support.</summary>
        public float Wages, CapitalIncome, Transfers, Taxes, Disposable;

        /// <summary>Income from the household's own business (proprietors' income; the household's, split evenly).</summary>
        public float Business;

        /// <summary>What the person earned themselves this year (wages + business income, before pooling).</summary>
        public float Earned;

        /// <summary>The person's own Social Security benefit this year ($).</summary>
        public float SocialSecurity;

        /// <summary>Spending, saving (negative = borrowing), net worth and debt ($).</summary>
        public float Spending, Saving, Wealth, Debt;

        /// <summary>Consumer debt (cards, auto, student: everything but the mortgage), $.</summary>
        public float ConsumerDebt;

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

        /// <summary>Material autonomy (agency's first component): 1 for a business owner or 3+ years of spending in
        /// financial assets, else the share of that runway.</summary>
        public float Autonomy;

        /// <summary>Debt payments (mortgage and consumer debt) over disposable income.</summary>
        public float DebtService;

        /// <summary>Share of the person's moves that cooperate with the people they deal with this year (0..1).</summary>
        public float Cooperation;

        /// <summary>Wealth group by net worth this year (0 bottom 50% .. 3 top 1%).</summary>
        public byte WealthGroup;

        /// <summary>Tribe this year: 0 = Democrat, 1 = Republican, 2 = independent.</summary>
        public byte Tribe;

        /// <summary>Children under 18 in the household.</summary>
        public byte Kids;

        /// <summary>The household's income rank this year (0..1).</summary>
        public float IncomeRank;

        /// <summary>Received this year from a spouse's or a parent's estate ($).</summary>
        public float Inherited;

        /// <summary>A large uninsured loss this year (jeopardy realized: illness, lawsuit, fraud; the household's, split
        /// evenly), $; and whether unpayable consumer debt was discharged (bankruptcy, charge-off).</summary>
        public float Loss;

        public bool Discharged;

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

        /// <summary>Lines alive (children included) and households (couples and single adults).</summary>
        public int People, Households;

        public double Wages, CapitalIncome, Transfers, Taxes, Disposable, Spending, Saving, Wealth, Debt;

        /// <summary>Proprietors' income and consumer debt ($B).</summary>
        public double Business, ConsumerDebt;

        /// <summary>Spending by category ($B), in the order of EconomyData.CategoryIds (saving last).</summary>
        public readonly double[] Categories = new double[7];

        /// <summary>Share of adults in control of their path (the cut calibrated in one year, applied to every year).</summary>
        public float InControlShare;

        /// <summary>Fantasy and fear: spending-weighted means over adults; reason, future and cooperation: means over adults.</summary>
        public float MeanFantasy, MeanFear, MeanReason, MeanFuture, MeanCooperation;

        /// <summary>Share of adults playing each strategy (PdStrategy order).</summary>
        public readonly float[] Strategies = new float[7];

        /// <summary>Share of adults in each tribe (Democrat, Republican, independent).</summary>
        public readonly float[] Parties = new float[3];

        /// <summary>Shares of net worth by wealth group (bottom 50%, 50-90%, 90-99%, top 1% of adults; couples split evenly).</summary>
        public readonly float[] WealthShares = new float[4];

        /// <summary>Owner-occupied share of households of their own; employment rate of adults; self-employed share of the
        /// employed; adults with material autonomy; adults saving 10%+ of disposable income.</summary>
        public float Homeownership, EmploymentRate, SelfEmployedShare, AutonomyShare, SaverShare;

        /// <summary>Households hit by a large uninsured loss this year, and households whose consumer debt was discharged
        /// (shares of households); the losses ($B).</summary>
        public float LossShare, DischargeShare;

        public double Losses;
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
    /// <remarks>
    /// The work is split into <see cref="TraitSampler"/> (pass A, traits), <see cref="LivesSimulation"/> (pass B, the
    /// years), <see cref="LivesInputs"/> (the data and its fallbacks) and <see cref="LivesReport"/> (the log, the
    /// notable people). Every query is read-only after <see cref="Ready"/> and safe from any thread.
    /// </remarks>
    public sealed class EconomicLives : ISmvLineStyle
    {
        /// <summary>
        /// The tint toward capital gold: people in control are clearly gold (at least this much of the way), others only
        /// tinge toward it as their agency approaches the cut (at most <see cref="NearGold"/>), so few lines are gold.
        /// </summary>
        const float ControlGold = 0.9f, NearGold = 0.25f;

        /// <summary>Agency below which nobody is tinted at all (DESIGN: agency 0.3 = barely).</summary>
        const float GoldFrom = 0.3f;

        /// <summary>Brightness rises with agency (DESIGN: x (1 + 0.6 agency)), in proportion to the gold blend.</summary>
        const float AgencyGlow = 0.6f;

        /// <summary>Heavy fantasy spending dims a line: from this share, by up to <see cref="FantasyDim"/> at
        /// <see cref="FantasyFull"/> (DESIGN).</summary>
        const float FantasyFrom = 0.35f, FantasyFull = 0.6f, FantasyDim = 0.3f;

        readonly EconomyData data;
        readonly int seed;
        LivesInputs inputs;
        PersonTraits[] traits = Array.Empty<PersonTraits>();
        PersonYear[] store = Array.Empty<PersonYear>();
        int[] offset = Array.Empty<int>(), firstYear = Array.Empty<int>(), yearCount = Array.Empty<int>();
        float[] inheritedReal = Array.Empty<float>();
        PopulationYear[] years = Array.Empty<PopulationYear>();
        Color32 gold;
        volatile bool ready;

        public EconomicLives(EconomyData data, int seed)
        {
            this.data = data;
            this.seed = seed;
        }

        public bool Ready => ready;
        public SmvSimulation Sim { get; private set; }
        public int FirstYear { get; private set; }
        public int LastYear { get; private set; }
        public string Log { get; private set; } = "";

        /// <summary>The agency at or above which an adult with material autonomy is in control (calibrated once).</summary>
        public float ControlThreshold { get; private set; } = 1f;

        public IReadOnlyList<Notable> Notables => notables;

        readonly List<Notable> notables = new List<Notable>();

        /// <summary>Runs every life on the finished population (worker thread). Never throws: a failure is logged and
        /// the lines keep their colors.</summary>
        public void Prepare(SmvSimulation sim)
        {
            Stopwatch sw = Stopwatch.StartNew();
            Sim = sim;
            FirstYear = (int)Math.Floor(EconomyStyle.FirstYear);
            LastYear = Math.Max(FirstYear, Math.Min(data?.LastYear ?? 2025, (int)Math.Floor(sim?.NowYear ?? 2025)));
            ready = false;
            if (sim == null || sim.People.Count == 0 || data == null)
            {
                Log = "no population or no data: nothing simulated";
                return;
            }

            try
            {
                inputs = new LivesInputs(data);
                int calibrationYear = Math.Min(inputs.InControlYear, LastYear);
                LivesInputs inp = inputs;
                Task<float[,]> cooperation = Task.Run(() => LivesSimulation.TabulateCooperation(inp, seed));
                TraitSampler sampler = new TraitSampler(inputs, seed);
                traits = sampler.Sample(sim, calibrationYear);
                long traitsMs = sw.ElapsedMilliseconds;

                LivesSimulation run = new LivesSimulation(inputs, sim, traits, sampler, cooperation.Result, seed, FirstYear, LastYear);
                long setupMs = sw.ElapsedMilliseconds - traitsMs;
                run.Run();
                store = run.Store;
                offset = run.Offset;
                firstYear = run.FirstYearOf;
                yearCount = run.YearCount;
                inheritedReal = run.InheritedReal;
                years = run.Years;
                long yearsMs = sw.ElapsedMilliseconds - traitsMs;

                // each person's tribe of the last year they lived in the record
                for (int i = 0; i < traits.Length; i++)
                {
                    traits[i].Tribe = yearCount[i] > 0
                        ? store[offset[i] + yearCount[i] - 1].Tribe
                        : TraitSampler.TribeAt(inputs, traits[i].Lean, Math.Min(LastYear, (int)Math.Floor(sim.People[i].Death)));
                }

                ControlThreshold = LivesReport.CalibrateControl(inputs, store, offset, firstYear, yearCount, years, FirstYear,
                    calibrationYear, out _, out string controlNote);
                Color c = EconomyStyle.Capital;
                gold = new Color32((byte)(255 * Mathf.Clamp01(c.r)), (byte)(255 * Mathf.Clamp01(c.g)), (byte)(255 * Mathf.Clamp01(c.b)), 255);

                // readable from here on (nothing else reads the lives until Prepare returns)
                ready = true;
                notables.Clear();
                notables.AddRange(LivesReport.PickNotables(this, inputs, sim, traits, inheritedReal, LastYear));
                Log = LivesReport.Build(this, inputs, sim, traits, run, sampler, controlNote, traitsMs, setupMs, yearsMs,
                    sw.ElapsedMilliseconds);
            }
            catch (Exception e)
            {
                ready = false;
                Log = "economic lives failed: " + e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace;
            }
        }

        /// <summary>A person's state in a calendar year; false when they are not alive then or nothing was simulated.</summary>
        [MethodImpl(LivesMath.Hot)]
        public bool TryGet(int person, int year, out PersonYear state)
        {
            if (ready && person >= 0 && person < yearCount.Length)
            {
                int k = year - firstYear[person];
                if (k >= 0 && k < yearCount[person])
                {
                    state = store[offset[person] + k];
                    return true;
                }
            }

            state = default;
            return false;
        }

        /// <summary>A person's traits (null when unknown).</summary>
        public PersonTraits Traits(int person) => ready && person >= 0 && person < traits.Length ? traits[person] : null;

        /// <summary>Bequests a person received from parents, in 2025 dollars (0 when unknown).</summary>
        public float InheritedFromParents(int person) =>
            ready && person >= 0 && person < inheritedReal.Length ? inheritedReal[person] : 0;

        /// <summary>Population totals of a year (null when nothing was simulated).</summary>
        public PopulationYear Aggregate(int year)
        {
            int k = year - FirstYear;
            return ready && k >= 0 && k < years.Length ? years[k] : null;
        }

        /// <summary>
        /// A person's drives in a year, each pole normalized to 1 (the individual weights x the drives' life-stage curves
        /// and the year's state: married, a parent, a thin buffer); false when the person was not simulated that year.
        /// </summary>
        public bool DriveWeights(int person, int year, float[] desires, float[] fears)
        {
            if (desires == null || fears == null || desires.Length < 5 || fears.Length < 5) return false;
            if (!TryGet(person, year, out PersonYear y)) return false;
            SmvPerson p = Sim.People[person];
            bool parent = false;
            foreach (SmvPerson c in ChildrenOf(person))
            {
                if (c.Birth <= year + 0.5) parent = true;
            }

            double[] d = new double[5], f = new double[5];
            double buffer = y.Spending > 0 ? Mathf.Clamp01(1 - 4 * Math.Max(0, y.Wealth) / Math.Max(1, y.Spending)) : 0;
            LivesSimulation.Drives(inputs, traits[person], p.Male, y.Age, y.Married, parent, buffer, d, f);
            for (int k = 0; k < 5; k++)
            {
                desires[k] = (float)d[k];
                fears[k] = (float)f[k];
            }

            return true;
        }

        IEnumerable<SmvPerson> ChildrenOf(int person)
        {
            List<SmvPerson> people = Sim.People;
            for (int i = person + 1; i < people.Count; i++)
            {
                if (people[i].Mother == person || people[i].Father == person) yield return people[i];
            }
        }

        /// <summary>A notable person's role, or null.</summary>
        public string RoleOf(int person)
        {
            foreach (Notable n in notables)
            {
                if (n.Person == person) return n.Role;
            }

            return null;
        }

        /// <summary>
        /// Adults' lines lean toward capital gold by their agency: clearly gold in control, a faint tinge as agency
        /// nears the cut, none below 0.3; brighter with agency, dimmer with heavy fantasy spending. Children and the
        /// years before the record keep the population's blue. Interpolated between mid-years.
        /// </summary>
        [MethodImpl(LivesMath.Hot)]
        public void Restyle(SmvPerson person, double time, bool child, ref Color32 tint, ref float intensity)
        {
            if (!ready || child || person == null) return;
            int i = person.Index;
            if (i < 0 || i >= yearCount.Length || yearCount[i] == 0) return;
            double ty = time - 0.5 - firstYear[i];
            if (ty < -1) return;
            int k0 = (int)Math.Floor(ty), last = yearCount[i] - 1;
            float f = (float)(ty - k0);
            if (k0 < 0)
            {
                k0 = 0;
                f = 0;
            }

            if (k0 >= last)
            {
                k0 = last;
                f = 0;
            }

            ref PersonYear a = ref store[offset[i] + k0];
            if (!a.Adult) return;
            ref PersonYear b = ref store[offset[i] + Math.Min(last, k0 + 1)];
            float g = Gold(ref a) + (Gold(ref b) - Gold(ref a)) * f;
            float agency = a.Agency + (b.Agency - a.Agency) * f;
            float fantasy = a.Fantasy + (b.Fantasy - a.Fantasy) * f;
            if (g > 0)
            {
                tint = new Color32(Mix(tint.r, gold.r, g), Mix(tint.g, gold.g, g), Mix(tint.b, gold.b, g), tint.a);
            }

            // the glow of agency goes with the gold, so the blue of everyone else keeps its brightness
            intensity *= 1f + AgencyGlow * Mathf.Clamp01(agency) * g;
            if (fantasy > FantasyFrom) intensity *= 1f - FantasyDim * Mathf.Clamp01((fantasy - FantasyFrom) / (FantasyFull - FantasyFrom));
        }

        [MethodImpl(LivesMath.Hot)]
        float Gold(ref PersonYear r)
        {
            if (!r.Adult) return 0;
            float cut = ControlThreshold;
            if (r.InControl) return ControlGold + (1 - ControlGold) * Mathf.SmoothStep(0, 1, (r.Agency - cut) / 0.2f);
            float near = Mathf.Clamp01((r.Agency - GoldFrom) / Math.Max(0.05f, cut - GoldFrom));
            return NearGold * near * near * (3 - 2 * near);
        }

        static byte Mix(byte a, byte b, float t) => (byte)Mathf.Clamp(a + (b - a) * t + 0.5f, 0, 255);
    }
}
