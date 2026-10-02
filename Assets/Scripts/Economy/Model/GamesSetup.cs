using System;
using System.Collections.Generic;
using Why.Economy.Data;

namespace Why.Economy.Model
{
    /// <summary>
    /// The prisoner's dilemma settings the economic lives play with, taken from games.json: the payoffs, the "spec"
    /// numbers (noise, chance of meeting again, distrust, party inheritance) and the mix of strategies people use. The mix
    /// comes from the simulated population when its economic lives have run (adults of 2025), else from games.json
    /// (strategies[].populationShare), else from <see cref="DefaultMix"/>. The land's season (<c>Land/SocialSeason.cs</c>)
    /// reads games.json's "social" block instead. Pure and deterministic (any thread).
    /// </summary>
    public static class GamesSetup
    {
        /// <summary>Where a strategy mix came from.</summary>
        public enum Source
        {
            Default = 0,
            Data = 1,
            Population = 2
        }

        /// <summary>
        /// The mix used when neither the population nor the data give one (shares by <see cref="PdStrategy"/>): tit for
        /// tat 30%, generous tit for tat 20%, win-stay lose-shift 15%, grim 15%, always defect 10%, always cooperate 10%.
        /// An illustrative mix, not a measurement: experiments on repeated games find mostly reciprocal strategies (tit
        /// for tat, grim) besides unconditional defection, and some leniency (Dal Bo and Frechette 2018).
        /// </summary>
        public static readonly float[] DefaultMix = { 0.30f, 0.20f, 0.15f, 0.10f, 0.10f, 0.15f, 0f };

        /// <summary>Year whose adults stand for "people now" in <see cref="Mix"/>.</summary>
        public const int PopulationYear = 2025;

        /// <summary>
        /// The payoffs of games.json when they make a dilemma (T &gt; R &gt; P &gt; S, 2R &gt; T + S), else Axelrod's.
        /// </summary>
        public static PdPayoff Payoff(EconomyData data)
        {
            PayoffData p = data?.Games?.Payoff;
            if (p == null) return PdPayoff.Axelrod;
            PdPayoff pay = new PdPayoff(p.T, p.R, p.P, p.S);
            return pay.IsDilemma ? pay : PdPayoff.Axelrod;
        }

        /// <summary>A number of games.json's "spec" block, or the fallback.</summary>
        public static float Spec(EconomyData data, string key, float fallback) =>
            data?.Games != null ? (float)data.Games.SpecOf(key, fallback) : fallback;

        /// <summary>True when games.json's "spec" block has the key.</summary>
        public static bool HasSpec(EconomyData data, string key) =>
            data?.Games?.Spec != null && data.Games.Spec.ContainsKey(key);

        /// <summary>Shares of the strategies people play (indexed by <see cref="PdStrategy"/>, summing to 1).</summary>
        public static float[] Mix(EconomyModel model, out Source source)
        {
            float[] mix = new float[PrisonersDilemma.All.Length];
            if (CountPopulation(model, -1, mix) > 0 && Normalize(mix))
            {
                source = Source.Population;
                return mix;
            }

            Array.Clear(mix, 0, mix.Length);
            List<StrategyInfo> listed = model?.Data?.Games?.Strategies;
            if (listed != null)
            {
                foreach (StrategyInfo s in listed)
                {
                    if (s?.Id != null && Enum.TryParse(s.Id, false, out PdStrategy id) && s.PopulationShare > 0)
                    {
                        mix[(int)id] += (float)s.PopulationShare;
                    }
                }

                if (Normalize(mix))
                {
                    source = Source.Data;
                    return mix;
                }
            }

            source = Source.Default;
            return (float[])DefaultMix.Clone();
        }

        /// <summary>A strategy's name in two words or fewer, lower case, for running text and small labels.</summary>
        public static string ShortName(PdStrategy s)
        {
            switch (s)
            {
                case PdStrategy.TitForTat: return "tit for tat";
                case PdStrategy.GenerousTitForTat: return "generous";
                case PdStrategy.WinStayLoseShift: return "win-stay";
                case PdStrategy.AlwaysCooperate: return "always cooperate";
                case PdStrategy.AlwaysDefect: return "always defect";
                case PdStrategy.Grim: return "grim";
                default: return "random";
            }
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>Adults of the population year in a tribe (-1 = every tribe), in birth order.</summary>
        static List<int> PopulationMembers(EconomyModel model, int tribe)
        {
            List<int> list = new List<int>();
            EconomicLives lives = model?.Lives;
            if (lives == null || !lives.Ready || lives.Sim == null) return list;
            int year = Math.Min(PopulationYear, lives.LastYear);
            int n = lives.Sim.People.Count;
            for (int i = 0; i < n; i++)
            {
                if (!lives.TryGet(i, year, out PersonYear state) || !state.Adult) continue;
                // the tribe of that year (PersonTraits.Tribe is the person's last year's)
                if (lives.Traits(i) == null || (tribe >= 0 && state.Tribe != tribe)) continue;
                list.Add(i);
            }

            return list;
        }

        /// <summary>Adds the strategies of the population's adults (of one tribe, or all) into counts; returns how many.</summary>
        static int CountPopulation(EconomyModel model, int tribe, float[] counts)
        {
            List<int> members = PopulationMembers(model, tribe);
            foreach (int person in members) counts[(int)model.Lives.Traits(person).Strategy]++;
            return members.Count;
        }

        static bool Normalize(float[] shares)
        {
            float sum = 0;
            foreach (float s in shares) sum += Math.Max(0, s);
            if (sum <= 0) return false;
            for (int i = 0; i < shares.Length; i++) shares[i] = Math.Max(0, shares[i]) / sum;
            return true;
        }
    }
}
