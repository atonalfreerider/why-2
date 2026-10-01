using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Why.Humans.Smv;

namespace Why.Economy.Model
{
    /// <summary>
    /// Pass A of the economic lives: what each simulated person is like, drawn once at birth in birth order (so a
    /// parent's draws exist before their children's) from the person's own random stream.
    /// <list type="bullet">
    /// <item>Nine correlated traits (Big Five, internal locus, social comparison, loss aversion, present bias): standard
    /// normals mixed by the Cholesky factor of psyche.json's correlations, each mapped through its declared distribution
    /// (normal z; lognormal loss aversion; present bias as the research mixture, comonotone with its normal); women
    /// shifted by half the sex difference up, men half down. Age slopes are applied when a trait is used.</item>
    /// <item>Earnings rank: a Gaussian copula of the parent's rank (rank-rank slope 0.34, Chetty et al. 2014),
    /// conscientiousness and openness.</item>
    /// <item>A left-right lean, inherited from a parent with games.json partyInheritance, that the era's party shares
    /// turn into a tribe (so every year's adult shares match the data, see <see cref="TribeAt"/>).</item>
    /// <item>A strategy for repeated games: a softmax of the traits whose offsets are calibrated so the adults of the
    /// target year play the games.json mix.</item>
    /// <item>The propensity to decide by reason, the higher OS (calibrated so the adults' mean is os.higher.share),
    /// enterprise (self-employment), the Social Security claim age, and the drives' individual weights.</item>
    /// </list>
    /// </summary>
    internal sealed class TraitSampler
    {
        /// <summary>
        /// Latent correlation of a child's and a parent's earnings: a Gaussian copula needs 2 sin(pi 0.34 / 6) = 0.354
        /// for a rank-rank (Spearman) slope of 0.34 (Chetty, Hendren, Kline and Saez 2014).
        /// </summary>
        const double RankInheritance = 0.354;

        /// <summary>Earnings rank loadings on conscientiousness and openness (DESIGN: self-control and curiosity pay).</summary>
        const double RankC = 0.25, RankO = 0.12;

        /// <summary>Reason (higher OS) propensity: logit loadings on C, O, N and internal locus (DESIGN).</summary>
        const double ReasonC = 0.5, ReasonO = 0.3, ReasonN = -0.45, ReasonLocus = 0.2;

        /// <summary>
        /// Logit loadings of the strategies on the traits (A, N, C, O per sd), in PdStrategy order: forgiving rules
        /// rise with agreeableness and conscientiousness and fall with neuroticism, grim rises with neuroticism and
        /// falls with agreeableness, always defect falls with both A and C, always cooperate rises with A and falls
        /// with C, random play falls with C (DESIGN; judgments of direction, the offsets set the levels).
        /// </summary>
        static readonly double[][] StrategyLoadings =
        {
            new[] { 0.2, -0.1, 0.4, 0.0 },   // TitForTat: clear, reciprocal
            new[] { 0.8, -0.5, 0.5, 0.1 },   // GenerousTitForTat: forgiving
            new[] { 0.1, -0.2, 0.4, 0.4 },   // WinStayLoseShift: adaptive
            new[] { 0.9, 0.0, -0.5, 0.0 },   // AlwaysCooperate: kind, unguarded
            new[] { -0.8, 0.2, -0.6, 0.0 },  // AlwaysDefect
            new[] { -0.6, 0.8, 0.0, 0.0 },   // Grim: revenge without end
            new[] { 0.0, 0.2, -0.6, 0.2 }    // Random: inconsistent
        };

        /// <summary>Individual spread of the drives' weights (lognormal sigma, judgment).</summary>
        const double DriveSpread = 0.3;

        /// <summary>
        /// Social Security claim ages and their shares (SSA claiming data, recalled: most claims at 62, at the full
        /// retirement age 65-67, a few at 70).
        /// </summary>
        static readonly double[] ClaimAges = { 62, 63, 64, 65, 66, 67, 70 };

        static readonly double[] ClaimShares = { 0.27, 0.07, 0.06, 0.17, 0.25, 0.10, 0.08 };

        readonly LivesInputs inputs;
        readonly int seed;

        /// <summary>Per person: uniform of the strategy draw, logit of the reason propensity (before the offset).</summary>
        public float[] StrategyDraw, ReasonLogit, CoverageDraw;

        /// <summary>Calibrated offsets: of the strategies' logits and of the reason logit.</summary>
        public readonly double[] StrategyOffsets = new double[7];

        public double ReasonOffset, ReasonSd = 0.2;

        public TraitSampler(LivesInputs inputs, int seed)
        {
            this.inputs = inputs;
            this.seed = seed;
        }

        /// <summary>Draws every person's traits (birth order) and calibrates strategies and reason to the target year.</summary>
        [MethodImpl(LivesMath.Hot)]
        public PersonTraits[] Sample(SmvSimulation sim, int calibrationYear)
        {
            List<SmvPerson> people = sim.People;
            int n = people.Count;
            PersonTraits[] traits = new PersonTraits[n];
            StrategyDraw = new float[n];
            ReasonLogit = new float[n];
            CoverageDraw = new float[n];
            double[] z = new double[9], x = new double[9];
            double[,] l = inputs.TraitCholesky;
            double inheritRest = Math.Sqrt(Math.Max(0.05, 1 - RankInheritance * RankInheritance - RankC * RankC -
                                                          RankO * RankO - 2 * RankC * RankO * inputs.TraitCorrelationCO));

            for (int i = 0; i < n; i++)
            {
                SmvPerson p = people[i];
                Random rng = new Random(LivesMath.SeedOf(seed, i, 0));
                for (int k = 0; k < 9; k++) z[k] = LivesMath.Normal(rng);
                for (int r = 0; r < 9; r++)
                {
                    double s = 0;
                    for (int k = 0; k <= r; k++) s += l[r, k] * z[k];
                    x[r] = s;
                }

                PersonTraits t = new PersonTraits();
                double sex = p.Male ? -0.5 : 0.5;
                t.C = (float)Normal(LivesInputs.TC, x[LivesInputs.TC], sex);
                t.N = (float)Normal(LivesInputs.TN, x[LivesInputs.TN], sex);
                t.A = (float)Normal(LivesInputs.TA, x[LivesInputs.TA], sex);
                t.O = (float)Normal(LivesInputs.TO, x[LivesInputs.TO], sex);
                t.E = (float)Normal(LivesInputs.TE, x[LivesInputs.TE], sex);
                t.Locus = (float)Normal(LivesInputs.TLocus, x[LivesInputs.TLocus], sex);
                t.Comparison = (float)Normal(LivesInputs.TComparison, x[LivesInputs.TComparison], sex);
                t.Lambda = (float)LossAversion(x[LivesInputs.TLambda], sex);
                t.Beta = (float)PresentBias(x[LivesInputs.TBeta], sex);

                // earnings rank: partly the parent's (Gaussian copula), partly character, mostly luck
                int parent = p.Mother >= 0 && p.Mother < i ? p.Mother : p.Father >= 0 && p.Father < i ? p.Father : -1;
                double parentRank = parent >= 0 ? traits[parent].Rank : rng.NextDouble();
                double latent = RankInheritance * LivesMath.InvPhi(parentRank) + RankC * x[LivesInputs.TC] +
                                RankO * x[LivesInputs.TO] + inheritRest * LivesMath.Normal(rng);
                t.ParentRank = (float)parentRank;
                t.Rank = (float)LivesMath.Clamp(LivesMath.Phi(latent), 1e-4, 1 - 1e-4);

                // lean: the parent's with games.json partyInheritance, else a fresh draw
                double inherit = rng.NextDouble(), fresh = rng.NextDouble();
                t.Lean = parent >= 0 && inherit < inputs.PartyInheritance ? traits[parent].Lean : (float)fresh;

                // enterprise: openness, extraversion, tolerance of risk (low loss aversion), believing outcomes are one's own
                double lambdaZ = LambdaZ(t.Lambda);
                t.Enterprise = (float)(0.35 * x[LivesInputs.TO] + 0.25 * x[LivesInputs.TE] - 0.3 * lambdaZ + 0.2 * x[LivesInputs.TLocus]);

                // the higher OS: the logit before the population offset (calibrated below)
                ReasonLogit[i] = (float)(ReasonC * x[LivesInputs.TC] + ReasonO * x[LivesInputs.TO] + ReasonN * x[LivesInputs.TN] +
                                         ReasonLocus * x[LivesInputs.TLocus]);

                // drives: the population's weights, varied per person and leaning with the traits
                for (int d = 0; d < 5; d++)
                {
                    double link = DesireLink(d, x);
                    t.Desires[d] = (float)(inputs.DesireWeight[d] *
                                           Math.Exp(DriveSpread * LivesMath.Normal(rng) - 0.5 * DriveSpread * DriveSpread + link));
                }

                for (int f = 0; f < 5; f++)
                {
                    double link = FearLink(f, x, lambdaZ);
                    t.Fears[f] = (float)(inputs.FearWeight[f] *
                                         Math.Exp(DriveSpread * LivesMath.Normal(rng) - 0.5 * DriveSpread * DriveSpread + link));
                }

                // Social Security: claim age, and a persistent draw against the era's coverage
                double uc = rng.NextDouble(), acc = 0;
                t.ClaimAge = (float)ClaimAges[ClaimAges.Length - 1];
                for (int k = 0; k < ClaimAges.Length; k++)
                {
                    acc += ClaimShares[k];
                    if (uc < acc)
                    {
                        t.ClaimAge = (float)ClaimAges[k];
                        break;
                    }
                }

                CoverageDraw[i] = (float)rng.NextDouble();
                StrategyDraw[i] = (float)rng.NextDouble();
                traits[i] = t;
            }

            Calibrate(sim, traits, calibrationYear);
            return traits;
        }

        /// <summary>A normal trait (z-score or the trait's units) with half its sex difference.</summary>
        double Normal(int trait, double x, double sex)
        {
            Why.Economy.Data.TraitDistribution d = inputs.TraitDist[trait];
            double sd = d.Sd > 0 ? d.Sd : 1;
            double v = d.Mean + sd * x + sex * d.SexDiff;
            return d.Clamp != null && d.Clamp.Length >= 2 ? LivesMath.Clamp(v, d.Clamp[0], d.Clamp[1]) : v;
        }

        /// <summary>Loss aversion: lognormal exp(logMean + logSd x) (psyche.json lossAversion), half the sex difference.</summary>
        double LossAversion(double x, double sex)
        {
            Why.Economy.Data.TraitDistribution d = inputs.TraitDist[LivesInputs.TLambda];
            double logMean = d.LogMean != 0 ? d.LogMean : Math.Log(d.Median > 0 ? d.Median : 1.8);
            double logSd = d.LogSd > 0 ? d.LogSd : 0.45;
            double v = Math.Exp(logMean + logSd * x) + sex * d.SexDiff;
            double lo = d.Clamp != null && d.Clamp.Length >= 2 ? d.Clamp[0] : 0.3;
            double hi = d.Clamp != null && d.Clamp.Length >= 2 ? d.Clamp[1] : 8;
            return LivesMath.Clamp(v, lo, hi);
        }

        /// <summary>Loss aversion in sd units of its log (for tilts and enterprise).</summary>
        public double LambdaZ(double lambda)
        {
            Why.Economy.Data.TraitDistribution d = inputs.TraitDist[LivesInputs.TLambda];
            double logMean = d.LogMean != 0 ? d.LogMean : Math.Log(d.Median > 0 ? d.Median : 1.8);
            double logSd = d.LogSd > 0 ? d.LogSd : 0.45;
            return (Math.Log(Math.Max(0.05, lambda)) - logMean) / logSd;
        }

        /// <summary>
        /// Present bias beta from the research mixture (psyche.json presentBiasBeta.mixture: 35% present-biased around
        /// 0.75, 55% time-consistent at 1, 10% future-biased at 1.05), comonotone with the correlated normal so low x is
        /// more present-biased; a plain normal when no mixture is given; clamped.
        /// </summary>
        double PresentBias(double x, double sex)
        {
            Why.Economy.Data.TraitDistribution d = inputs.TraitDist[LivesInputs.TBeta];
            double v;
            if (d.Distribution == "mixture" && d.Mixture != null)
            {
                double wPresent = d.MixtureOf("presentBiased", 0.35), wFuture = d.MixtureOf("futureBiased", 0.1);
                double u = LivesMath.Phi(x);
                if (u < wPresent)
                {
                    double within = LivesMath.Clamp(u / Math.Max(wPresent, 1e-6), 1e-4, 1 - 1e-4);
                    v = d.MixtureOf("presentBiasedMean", 0.75) + d.MixtureOf("presentBiasedSd", 0.1) * LivesMath.InvPhi(within);
                }
                else if (u > 1 - wFuture)
                {
                    v = d.MixtureOf("futureBiasedMean", 1.05);
                }
                else
                {
                    v = 1.0;
                }
            }
            else
            {
                // no distribution in the data: the research mixture's moments (mean 0.92, sd 0.14)
                bool given = d.Mean > 0;
                v = (given ? d.Mean : 0.92) + (given && d.Sd > 0 ? d.Sd : 0.14) * x;
            }

            v += sex * d.SexDiff;
            double lo = d.Clamp != null && d.Clamp.Length >= 2 ? d.Clamp[0] : 0.5;
            double hi = d.Clamp != null && d.Clamp.Length >= 2 ? d.Clamp[1] : 1.05;
            return LivesMath.Clamp(v, lo, hi);
        }

        /// <summary>Trait links of the desires (log weight per sd; judgments): belonging with A and E, order with C, sex with E.</summary>
        static double DesireLink(int d, double[] x)
        {
            switch (d)
            {
                case 1: return 0.15 * x[LivesInputs.TA] + 0.15 * x[LivesInputs.TE];
                case 2: return 0.15 * x[LivesInputs.TC] + 0.1 * x[LivesInputs.TA];
                case 3: return 0.1 * x[LivesInputs.TC];
                case 4: return 0.2 * x[LivesInputs.TE] - 0.05 * x[LivesInputs.TA];
                default: return 0;
            }
        }

        /// <summary>Trait links of the fears (log weight per sd; judgments): neuroticism raises every fear, loss aversion ruin and exposure.</summary>
        static double FearLink(int f, double[] x, double lambdaZ)
        {
            double n = x[LivesInputs.TN];
            switch (f)
            {
                case 0: return 0.2 * n + 0.1 * lambdaZ;
                case 1: return 0.2 * n - 0.15 * x[LivesInputs.TE];
                case 2: return 0.2 * n;
                case 3: return 0.15 * n + 0.1 * lambdaZ;
                default: return 0.05 * x[LivesInputs.TA];
            }
        }

        /// <summary>The model's maturity term of reason (logit units): rises from 18 to about 50, eases after 75.</summary>
        public static double Maturity(double age)
        {
            double rise = 0.6 * LivesMath.Clamp01((age - 18) / 32);
            double fall = 0.4 * LivesMath.Clamp01((age - 75) / 20);
            return rise - fall - 0.3;
        }

        /// <summary>
        /// Calibrates the strategy offsets (softmax shares of the target year's adults = games.json mix) and the reason
        /// offset (the adults' mean propensity = os.higher.share), then draws every strategy.
        /// </summary>
        [MethodImpl(LivesMath.Hot)]
        void Calibrate(SmvSimulation sim, PersonTraits[] traits, int year)
        {
            List<SmvPerson> people = sim.People;
            double t = Math.Min(year + 0.5, sim.NowYear - 1e-3);
            List<int> adults = new List<int>();
            for (int i = 0; i < people.Count; i++)
            {
                SmvPerson p = people[i];
                if (p.Enter <= t && t < p.Death && t - p.Birth >= 18) adults.Add(i);
            }

            if (adults.Count == 0)
            {
                for (int i = 0; i < people.Count; i++) adults.Add(i);
            }

            // strategies: fixed-point iteration on the offsets (each step multiplies a share by target / current)
            double[] target = inputs.StrategyTargets;
            double[] logits = new double[7], probs = new double[7], mean = new double[7];
            for (int s = 0; s < 7; s++) StrategyOffsets[s] = Math.Log(Math.Max(target[s], 1e-6));
            for (int iter = 0; iter < 30; iter++)
            {
                Array.Clear(mean, 0, 7);
                foreach (int i in adults)
                {
                    Softmax(traits[i], logits, probs);
                    for (int s = 0; s < 7; s++) mean[s] += probs[s];
                }

                double err = 0;
                for (int s = 0; s < 7; s++)
                {
                    mean[s] /= adults.Count;
                    if (target[s] <= 0) continue;
                    StrategyOffsets[s] += Math.Log(target[s] / Math.Max(mean[s], 1e-9));
                    err = Math.Max(err, Math.Abs(mean[s] - target[s]));
                }

                if (err < 2e-4) break;
            }

            for (int i = 0; i < people.Count; i++)
            {
                Softmax(traits[i], logits, probs);
                double u = StrategyDraw[i], acc = 0;
                int pick = 6;
                for (int s = 0; s < 7; s++)
                {
                    acc += probs[s];
                    if (u < acc)
                    {
                        pick = s;
                        break;
                    }
                }

                traits[i].Strategy = (PdStrategy)pick;
            }

            // reason: bisection on the offset so the adults' mean propensity (with maturity at their age) is the target
            double lo = -6, hi = 6;
            for (int iter = 0; iter < 50; iter++)
            {
                double mid = 0.5 * (lo + hi), sum = 0;
                foreach (int i in adults) sum += LivesMath.Sigmoid(ReasonLogit[i] + mid + Maturity(t - people[i].Birth));
                if (sum / adults.Count < inputs.HigherOsShare) lo = mid;
                else hi = mid;
            }

            ReasonOffset = 0.5 * (lo + hi);
            double s1 = 0, s2 = 0;
            foreach (int i in adults)
            {
                double r = LivesMath.Sigmoid(ReasonLogit[i] + ReasonOffset + Maturity(t - people[i].Birth));
                s1 += r;
                s2 += r * r;
            }

            double m = s1 / adults.Count;
            ReasonSd = Math.Max(0.02, Math.Sqrt(Math.Max(0, s2 / adults.Count - m * m)));
            for (int i = 0; i < people.Count; i++) traits[i].Reason0 = (float)LivesMath.Sigmoid(ReasonLogit[i] + ReasonOffset);
        }

        [MethodImpl(LivesMath.Hot)]
        void Softmax(PersonTraits t, double[] logits, double[] probs)
        {
            double max = double.MinValue;
            for (int s = 0; s < 7; s++)
            {
                double[] w = StrategyLoadings[s];
                logits[s] = StrategyOffsets[s] + w[0] * t.A + w[1] * t.N + w[2] * t.C + w[3] * t.O;
                max = Math.Max(max, logits[s]);
            }

            double sum = 0;
            for (int s = 0; s < 7; s++)
            {
                probs[s] = inputs.StrategyTargets[s] > 0 ? Math.Exp(logits[s] - max) : 0;
                sum += probs[s];
            }

            for (int s = 0; s < 7; s++) probs[s] = sum > 0 ? probs[s] / sum : 1.0 / 7;
        }

        /// <summary>
        /// The tribe a lean falls in at a year (0 = Democrat, 1 = Republican, 2 = independent): the left end of the lean
        /// up to the year's Democratic share, the right end down to the Republican share, independents between, so the
        /// adult shares of every year match games.json partyId while each person keeps a stable place (Democrats who
        /// drift become independents first).
        /// </summary>
        public static byte TribeAt(LivesInputs inputs, float lean, double year)
        {
            double dem = inputs.Dem.At(year), rep = inputs.Rep.At(year);
            if (lean < dem) return 0;
            if (lean >= 1 - rep) return 1;
            return 2;
        }
    }
}
