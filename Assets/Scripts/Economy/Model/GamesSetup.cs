using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Why.Economy.Data;

namespace Why.Economy.Model
{
    /// <summary>
    /// What the games station plays with, taken from the scene's model: the payoffs, the mix of strategies people use,
    /// the members of two tribes, and how the mix evolves when strategies spread by their payoff. The mix comes from the
    /// simulated population when its economic lives have run (adults of 2025), else from games.json
    /// (strategies[].populationShare), else from <see cref="DefaultMix"/>. Pure and deterministic (any thread).
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

        /// <summary>Year whose adults stand for "people now" in the tribes and the evolution's starting mix.</summary>
        public const int PopulationYear = 2025;

        /// <summary>Strategies with less than this share of the population are left out of a tribe's description.</summary>
        const float MinDescribedShare = 0.04f;

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

        /// <summary>
        /// The members of a tribe (member 0 is its alpha). From the simulated population when it is ready: adults of
        /// <see cref="PopulationYear"/> in that tribe (0 = Democrats, 1 = Republicans) picked evenly through birth order,
        /// the highest earner among them leading. Otherwise the mix apportioned to the members (largest remainders) and
        /// shuffled with the seed.
        /// </summary>
        public static PdStrategy[] Tribe(EconomyModel model, int tribe, int size, float[] mix, int seed, out Source source)
        {
            size = Math.Max(2, size);
            List<int> members = PopulationMembers(model, tribe);
            if (members.Count >= size)
            {
                EconomicLives lives = model.Lives;
                PdStrategy[] picked = new PdStrategy[size];
                int alpha = 0;
                float bestRank = float.MinValue;
                for (int k = 0; k < size; k++)
                {
                    // evenly through birth order, so every generation of adults is represented
                    int person = members[(int)((k + 0.5) * members.Count / size)];
                    PersonTraits t = lives.Traits(person);
                    picked[k] = t.Strategy;
                    if (t.Rank > bestRank)
                    {
                        bestRank = t.Rank;
                        alpha = k;
                    }
                }

                (picked[0], picked[alpha]) = (picked[alpha], picked[0]);
                source = Source.Population;
                return picked;
            }

            source = Source.Default;
            return Apportion(mix, size, seed);
        }

        /// <summary>Strategy counts of a tribe in words, largest first ("tit for tat 7, grim 4, generous 3").</summary>
        public static string Describe(PdStrategy[] members, int maxItems)
        {
            int[] counts = new int[PrisonersDilemma.All.Length];
            foreach (PdStrategy s in members) counts[(int)s]++;
            StringBuilder sb = new StringBuilder();
            for (int item = 0; item < maxItems; item++)
            {
                int best = -1;
                for (int i = 0; i < counts.Length; i++)
                {
                    if (counts[i] > 0 && (best < 0 || counts[i] > counts[best])) best = i;
                }

                if (best < 0 || counts[best] < MinDescribedShare * members.Length) break;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(ShortName((PdStrategy)best)).Append(' ')
                    .Append(counts[best].ToString(CultureInfo.InvariantCulture));
                counts[best] = 0;
            }

            return sb.ToString();
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

        /// <summary>
        /// Strategy shares over generations of the replicator dynamics (<see cref="Replicator"/>) among
        /// <paramref name="strategies"/>: payoffs estimated over <paramref name="gamesPerPair"/> games per pair of
        /// strategies with the given mistakes and chance of meeting again (fixed seed), starting from the mix restricted
        /// to those strategies. Returns generations + 1 rows (the start included), each summing to 1.
        /// </summary>
        public static List<float[]> Evolve(PdStrategy[] strategies, PdPayoff pay, float[] mix, float noise, float continuation,
            int generations, int gamesPerPair, float mutation, int seed)
        {
            float[,] payoffs = PrisonersDilemma.PayoffMatrix(strategies, pay, continuation, noise, gamesPerPair, seed);
            float[] start = new float[strategies.Length];
            for (int i = 0; i < strategies.Length; i++)
            {
                int s = (int)strategies[i];
                start[i] = mix != null && s < mix.Length ? mix[s] : 1;
            }

            Replicator r = new Replicator(strategies, payoffs, start, mutation);
            r.Run(generations);
            return r.Generations;
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
                PersonTraits t = lives.Traits(i);
                if (t == null || (tribe >= 0 && t.Tribe != tribe)) continue;
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

        /// <summary>Members for a mix: counts by largest remainders, then a seeded shuffle.</summary>
        static PdStrategy[] Apportion(float[] mix, int size, int seed)
        {
            int n = PrisonersDilemma.All.Length;
            int[] counts = new int[n];
            float[] remainder = new float[n];
            int given = 0;
            for (int i = 0; i < n; i++)
            {
                float exact = (i < mix.Length ? mix[i] : 0) * size;
                counts[i] = (int)Math.Floor(exact);
                remainder[i] = exact - counts[i];
                given += counts[i];
            }

            for (; given < size; given++)
            {
                int best = 0;
                for (int i = 1; i < n; i++)
                {
                    if (remainder[i] > remainder[best]) best = i;
                }

                counts[best]++;
                remainder[best] = -1;
            }

            PdStrategy[] members = new PdStrategy[size];
            int k = 0;
            for (int i = 0; i < n; i++)
            {
                for (int c = 0; c < counts[i] && k < size; c++) members[k++] = (PdStrategy)i;
            }

            Random rng = new Random(seed);
            for (int i = size - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (members[i], members[j]) = (members[j], members[i]);
            }

            return members;
        }
    }
}
