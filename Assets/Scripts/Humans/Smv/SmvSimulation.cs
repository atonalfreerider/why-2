using System;
using System.Collections.Generic;

namespace Why.Humans.Smv
{
    /// <summary>One simulated lifeline, standing for <see cref="SmvSimulation.PeoplePerLine"/> people.</summary>
    public sealed class SmvPerson
    {
        /// <summary>Birth order across the whole population (0 = earliest): the lifeline id offset.</summary>
        public int Index;

        /// <summary>Birth order within the person's sex (used to thin the population into coarser tiers).</summary>
        public int SexIndex;

        public bool Male;

        /// <summary>Arrived after birth (the line starts at arrival, not at birth).</summary>
        public bool Immigrant;

        /// <summary>Fractional calendar years.</summary>
        public double Birth, Enter, Death = double.PositiveInfinity;

        public float Base;              // bell-curve base value (1..10)
        public float Wealth;            // men's asset potential (0..1)
        public float PartnerQuantile;   // position in the partner-count distribution (0..1)
        public int Partners;            // lifetime sexual partners so far
        public int Spouse = -1;         // index of the current spouse
        public double MarriedAt;
        public int Children;
        public double LastBirth = double.NegativeInfinity;
        public int Mother = -1, Father = -1;

        /// <summary>Step at which a newborn was placed with its parents (-1 when it had none).</summary>
        public int BirthStep = -1;

        /// <summary>Social market value at the latest simulated step.</summary>
        public float Value;

        /// <summary>First and last simulation step at which the person is alive, and their sample offset.</summary>
        public int FirstStep, LastStep = -1, SampleOffset;

        /// <summary>Data-space height and radius of the line's first point (at <see cref="Enter"/>).</summary>
        public float StartY, StartRho;

        internal float OffsetSmooth;    // signed offset from the band center (fraction of the envelope)
        internal float HeightSmooth;
        internal float RankOffset;      // target offset from the rank (before smoothing)
        internal int ValueBin;          // value histogram bin (ranking)
        internal bool Placed;           // smoothing state initialized

        // value evaluation (see SmvSimulation.Rank): last exact value, its step, and the slope per step
        internal float ExactValue;
        internal float ValueSlope;
        internal int ExactStep = -1;
        internal bool StateChanged;     // children or partners changed since the last exact value

        public int SampleCount => LastStep >= FirstStep ? LastStep - FirstStep + 1 : 0;
    }

    /// <summary>
    /// Band of the United States stream at a calendar year: center and half width in data-space rho.
    /// </summary>
    public delegate void BandAt(double year, out float center, out float halfWidth);

    /// <summary>
    /// The United States population simulation ported from the smv project, re-engineered to run in a
    /// fraction of a second on a worker thread (no GameObjects, no LINQ over the population per step).
    ///
    /// 1. Demography: everyone alive at mid-1950 comes from the UN age pyramid; every following year adds
    ///    the newborn cohort and reconciles every birth cohort with the pyramid - random deaths (the "grim
    ///    reaper") when the simulated cohort is too large, immigrants when it is too small.
    /// 2. Life course, in quarter-year steps from the earliest birth (people alive in 1950 are backfilled
    ///    from birth) to now: partner counts, marriages at the US marriage rate by assortative matching,
    ///    divorces at the divorce rate, births assigned to mothers (and fathers), and social market value
    ///    from <see cref="SmvModel"/>.
    /// 3. Placement inside the stream: women on the inner side, men on the outer side, adults spread by
    ///    their value rank, married couples drawn together and mirrored, children near the center. Every
    ///    step writes each living person's (height, rho) sample.
    ///
    /// Deterministic for a seed (System.Random).
    /// </summary>
    public sealed class SmvSimulation
    {
        /// <summary>Simulation step in years (a quarter).</summary>
        public const double Step = 0.25;

        /// <summary>Smoothing time constants (years) for the radial offset and the height of a line.</summary>
        const double OffsetTau = 0.8;
        const double HeightTau = 0.4;

        /// <summary>Children stay in a narrow core around the band center (fraction of the envelope).</summary>
        const float ChildSpread = 0.05f;

        const float MotherMinAge = 18f;
        const float MotherMaxAge = 42f;
        const double BirthSpacing = 1.25;
        const float FatherMaxAge = 55f;

        /// <summary>Random draws tried before a weighted pick scans its whole candidate list.</summary>
        const int RejectionTries = 40;

        /// <summary>Value histogram resolution for ranking (0..10 in steps of about 0.01).</summary>
        const int ValueBins = 1024;

        /// <summary>Resolution of the tabulated <see cref="SmvModel.RankOffset"/> (single, married).</summary>
        const int OffsetTableSize = 2048;

        static readonly float[][] OffsetTable = BuildOffsetTable();

        public readonly List<SmvPerson> People = new List<SmvPerson>();

        /// <summary>Real people represented by one lifeline.</summary>
        public readonly double PeoplePerLine;

        public readonly double NowYear;

        /// <summary>Calendar year of step 0 and the number of steps (the last one is at or before now).</summary>
        public double StartTime { get; private set; }

        public int StepCount { get; private set; }

        /// <summary>Per-sample height and rho, indexed by <see cref="SmvPerson.SampleOffset"/> + step - FirstStep.</summary>
        public float[] SampleY { get; private set; } = Array.Empty<float>();

        public float[] SampleRho { get; private set; } = Array.Empty<float>();

        /// <summary>Per step: band center and population envelope (half width available to one sex).</summary>
        public float[] Center { get; private set; } = Array.Empty<float>();

        public float[] Envelope { get; private set; } = Array.Empty<float>();

        // statistics for the log and the anchors
        public int Marriages { get; private set; }
        public int Divorces { get; private set; }
        public int BirthsWithMother { get; private set; }
        public int Immigrants { get; private set; }

        readonly SmvData data;
        readonly BandAt band;
        readonly Random rng;

        List<SmvPerson>[,] cohorts;
        int minCohort;
        float[] partnerCurve = Array.Empty<float>(); // [person * Bands + band]
        double[] weights = new double[1024];

        // per-step working sets (arrays: the hot loops run over them hundreds of times)
        SmvPerson[] byIndex = Array.Empty<SmvPerson>();
        SmvPerson[] alive = new SmvPerson[4096];
        int aliveCount;
        readonly List<SmvPerson> newborns = new List<SmvPerson>(64);
        readonly int[][] binRank = { new int[ValueBins], new int[ValueBins] };
        readonly int[] adults = new int[2];
        readonly List<SmvPerson> singleWomen = new List<SmvPerson>(4096);
        readonly List<SmvPerson> singleMen = new List<SmvPerson>(4096);
        readonly List<SmvPerson> wives = new List<SmvPerson>(4096);
        readonly List<SmvPerson> marriedMothers = new List<SmvPerson>(2048);
        readonly List<SmvPerson> singleMothers = new List<SmvPerson>(2048);

        public SmvSimulation(SmvData data, BandAt band, double peoplePerLine, double nowYear, int seed)
        {
            this.data = data;
            this.band = band;
            PeoplePerLine = peoplePerLine;
            NowYear = nowYear;
            rng = new Random(seed);
        }

        /// <summary>Runs demography and the life course. Call once.</summary>
        public void Run()
        {
            if (!data.IsValid) return;
            Populate();
            AssignOrder();
            Live();
        }

        /// <summary>Index of the first person born at or after a calendar year (people are in birth order).</summary>
        public int FirstBornAtOrAfter(double year)
        {
            int lo = 0, hi = People.Count;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (People[mid].Birth < year) lo = mid + 1;
                else hi = mid;
            }

            return lo;
        }

        /// <summary>Step nearest to a calendar year (clamped to the simulated range).</summary>
        public int StepAt(double year)
        {
            int k = (int)Math.Round((year - StartTime) / Step);
            return Math.Max(0, Math.Min(StepCount - 1, k));
        }

        public double TimeOf(int step) => StartTime + step * Step;

        // ------------------------------------------------------------------ 1. demography

        void Populate()
        {
            int first = data.FirstYear;
            int lastMid = Math.Min(data.LastYear, (int)Math.Floor(NowYear - 0.5));
            minCohort = first - SmvData.MaxAge;
            int cohortCount = lastMid + 1 - minCohort + 1;
            cohorts = new List<SmvPerson>[2, cohortCount];
            for (int s = 0; s < 2; s++)
            {
                for (int c = 0; c < cohortCount; c++) cohorts[s, c] = new List<SmvPerson>();
            }

            // everyone alive at mid-year of the first year; the pyramid of year Y is taken at Y + 0.5 and
            // age a means born in [Y - a - 0.5, Y - a + 0.5)
            for (int age = 0; age <= SmvData.MaxAge; age++)
            {
                for (int s = 0; s < 2; s++) Born(s == 1, first - age, StochasticRound(Lines(first, s == 1, age)), 1.0);
            }

            for (int year = first + 1; year <= lastMid; year++)
            {
                for (int s = 0; s < 2; s++)
                {
                    bool male = s == 1;
                    Born(male, year, StochasticRound(Lines(year, male, 0)), 1.0);
                    for (int age = 1; age <= SmvData.MaxAge; age++) Reconcile(year, male, age);

                    // the open 100+ group: whoever is older leaves the simulation this year
                    int gone = year - SmvData.MaxAge - 1 - minCohort;
                    if (gone >= 0)
                    {
                        foreach (SmvPerson p in cohorts[s, gone]) p.Death = DeathTime(p, year);
                        cohorts[s, gone].Clear();
                    }
                }
            }

            // births from the last mid-year to now, at the last year's pace
            double tail = NowYear - (lastMid + 0.5);
            if (tail > 0)
            {
                for (int s = 0; s < 2; s++)
                {
                    int n = StochasticRound(Lines(lastMid, s == 1, 0) * tail);
                    Born(s == 1, lastMid + 1, n, tail);
                }
            }
        }

        /// <summary>Lines (fractional) for a pyramid cell.</summary>
        double Lines(int year, bool male, int age) => data.Pyramid(year, male, age) * 1000.0 / PeoplePerLine;

        /// <summary>
        /// Creates n people of a birth cohort, spread over the first <paramref name="span"/> years of the
        /// cohort's birth window [cohort - 0.5, cohort + 0.5).
        /// </summary>
        void Born(bool male, int cohort, int n, double span)
        {
            for (int j = 0; j < n; j++)
            {
                double birth = cohort - 0.5 + span * (j + 0.1 + 0.8 * rng.NextDouble()) / n;
                if (birth >= NowYear) continue;
                Add(NewPerson(male, birth, birth, false), cohort);
            }
        }

        /// <summary>Matches the simulated survivors of a birth cohort to the pyramid (deaths or immigrants).</summary>
        void Reconcile(int year, bool male, int age)
        {
            int cohort = year - age;
            List<SmvPerson> list = cohorts[male ? 1 : 0, cohort - minCohort];
            double target = Lines(year, male, age);
            int have = list.Count;
            int want = (int)Math.Round(target, MidpointRounding.AwayFromZero);

            if (have - target > 0.5)
            {
                // the grim reaper: random members die during the past year
                for (int k = have - want; k > 0 && list.Count > 0; k--)
                {
                    int j = rng.Next(list.Count);
                    SmvPerson p = list[j];
                    list[j] = list[list.Count - 1];
                    list.RemoveAt(list.Count - 1);
                    p.Death = DeathTime(p, year);
                }
            }
            else if (target - have > 0.5)
            {
                // immigrants of this birth cohort arrive during the past year
                for (int k = want - have; k > 0; k--)
                {
                    double birth = cohort - 0.5 + rng.NextDouble();
                    double enter = Math.Max(year - 0.5 + rng.NextDouble(), birth + 0.05);
                    Add(NewPerson(male, birth, enter, true), cohort);
                    Immigrants++;
                }
            }
        }

        /// <summary>A death time during the year before mid-year <paramref name="year"/>, after arrival.</summary>
        double DeathTime(SmvPerson p, int year)
        {
            double t = year - 0.5 + rng.NextDouble();
            double earliest = p.Enter + 0.02;
            if (t < earliest) t = earliest + (year + 0.5 - earliest) * rng.NextDouble();
            return t;
        }

        SmvPerson NewPerson(bool male, double birth, double enter, bool immigrant)
        {
            return new SmvPerson
            {
                Male = male,
                Birth = birth,
                Enter = enter,
                Immigrant = immigrant,
                Base = SmvModel.SampleBase(rng),
                // assets are skewed: most men accumulate little, a few accumulate a lot
                Wealth = male ? (float)Math.Pow(rng.NextDouble(), 1.6) : 0f,
                PartnerQuantile = (float)rng.NextDouble()
            };
        }

        void Add(SmvPerson p, int cohort)
        {
            People.Add(p);
            cohorts[p.Male ? 1 : 0, cohort - minCohort].Add(p);
        }

        int StochasticRound(double x) => x <= 0 ? 0 : (int)Math.Floor(x + rng.NextDouble());

        // ------------------------------------------------------------------ 2. order and sample layout

        void AssignOrder()
        {
            People.Sort((a, b) =>
            {
                int c = a.Birth.CompareTo(b.Birth);
                return c != 0 ? c : a.Male.CompareTo(b.Male);
            });

            int[] perSex = new int[2];
            partnerCurve = new float[People.Count * PartnerTable.Bands];
            double minEnter = double.MaxValue;
            for (int i = 0; i < People.Count; i++)
            {
                SmvPerson p = People[i];
                p.Index = i;
                p.SexIndex = perSex[p.Male ? 1 : 0]++;
                data.Partners.Sample(p.Male, p.PartnerQuantile, partnerCurve, i * PartnerTable.Bands);
                minEnter = Math.Min(minEnter, p.Enter);
            }

            if (People.Count == 0) return;
            StartTime = Math.Floor(minEnter / Step) * Step;
            StepCount = (int)Math.Floor((NowYear - StartTime) / Step) + 1;

            int total = 0;
            foreach (SmvPerson p in People)
            {
                p.FirstStep = (int)Math.Ceiling((p.Enter - StartTime) / Step - 1e-9);
                double end = Math.Min(p.Death, NowYear);
                int last = (int)Math.Floor((end - StartTime) / Step);
                if (TimeOf(last) >= p.Death) last--; // alive strictly before death
                p.LastStep = Math.Min(last, StepCount - 1);
                p.SampleOffset = total;
                total += p.SampleCount;
            }

            SampleY = new float[total];
            SampleRho = new float[total];
            Center = new float[StepCount];
            Envelope = new float[StepCount];
            byIndex = People.ToArray();
        }

        // ------------------------------------------------------------------ 3. life course

        void Live()
        {
            if (People.Count == 0) return;
            SmvPerson[] byEntry = People.ToArray();
            Array.Sort(byEntry, (a, b) =>
            {
                int c = a.FirstStep.CompareTo(b.FirstStep);
                return c != 0 ? c : a.Index.CompareTo(b.Index);
            });

            float aOff = (float)(1 - Math.Exp(-Step / OffsetTau));
            float aY = (float)(1 - Math.Exp(-Step / HeightTau));
            double popMax = data.MaxPopulation;
            double marriageDue = 0, divorceDue = 0;
            int next = 0;

            for (int k = 0; k < StepCount; k++)
            {
                double t = TimeOf(k);
                band(t, out float center, out float half);
                double popShare = Math.Max(0.25, Math.Min(1.0, data.Population(t) / popMax));
                float envelope = (float)(half * 0.95 * popShare);
                Center[k] = center;
                Envelope[k] = envelope;

                // leave: the dead
                int w = 0;
                for (int i = 0; i < aliveCount; i++)
                {
                    if (alive[i].LastStep >= k) alive[w++] = alive[i];
                }

                Array.Clear(alive, w, aliveCount - w);
                aliveCount = w;

                // enter: newborns and immigrants
                newborns.Clear();
                while (next < byEntry.Length && byEntry[next].FirstStep <= k)
                {
                    SmvPerson p = byEntry[next++];
                    if (p.SampleCount == 0) continue;
                    if (aliveCount == alive.Length) Array.Resize(ref alive, alive.Length * 2);
                    alive[aliveCount++] = p;
                    if (p.Immigrant) p.Partners = (int)Math.Round(PartnerCurve(p, (float)(t - p.Birth)));
                    else newborns.Add(p);
                }

                UpdateStates(t, k);
                foreach (SmvPerson c in newborns) PlaceNewborn(c, t, k, center, envelope);

                marriageDue += data.Marriage.At(t) / 1000.0 * aliveCount * Step;
                for (; marriageDue >= 1; marriageDue -= 1) Marry(t);
                divorceDue += data.Divorce.At(t) / 1000.0 * aliveCount * Step;
                for (; divorceDue >= 1; divorceDue -= 1) Divorce(t);

                Rank(t, k);
                Place(t, k, center, envelope, aOff, aY);
            }
        }

        /// <summary>
        /// Widowhood, partner counts (single adults catch up with their partner curve, one partner per step
        /// at most; children drop out of sex encounters) and this step's candidate lists for births,
        /// marriages and divorces - one pass over the living.
        /// </summary>
        void UpdateStates(double t, int k)
        {
            singleWomen.Clear();
            singleMen.Clear();
            wives.Clear();
            marriedMothers.Clear();
            singleMothers.Clear();
            for (int i = 0; i < aliveCount; i++)
            {
                SmvPerson p = alive[i];
                if (p.Spouse >= 0 && byIndex[p.Spouse].LastStep < k) p.Spouse = -1;
                float age = (float)(t - p.Birth);
                if (age < SmvModel.AdultAge) continue;
                if (p.Spouse < 0 && p.Partners + 0.5f < PartnerCurve(p, age))
                {
                    p.Partners++;
                    p.StateChanged = true;
                }

                if (p.Male)
                {
                    if (p.Spouse < 0) singleMen.Add(p);
                    continue;
                }

                if (p.Spouse < 0) singleWomen.Add(p);
                else wives.Add(p);
                if (age >= MotherMinAge && age <= MotherMaxAge) (p.Spouse >= 0 ? marriedMothers : singleMothers).Add(p);
            }
        }

        /// <summary>Lifetime partners expected at an age: the person's quantile of each age band, interpolated.</summary>
        float PartnerCurve(SmvPerson p, float age)
        {
            float[] mid = PartnerTable.BandMidAge;
            int o = p.Index * PartnerTable.Bands;
            if (age <= 15) return 0;
            if (age <= mid[0]) return partnerCurve[o] * (age - 15) / (mid[0] - 15);
            for (int b = 1; b < PartnerTable.Bands; b++)
            {
                if (age <= mid[b])
                {
                    float f = (age - mid[b - 1]) / (mid[b] - mid[b - 1]);
                    return partnerCurve[o + b - 1] + (partnerCurve[o + b] - partnerCurve[o + b - 1]) * f;
                }
            }

            return partnerCurve[o + PartnerTable.Bands - 1];
        }

        /// <summary>
        /// Gives a newborn its parents: a mother aged 18-42 (married unless the single-parent share says
        /// otherwise), and her husband or, for a single mother, a man whose value is at least hers (the
        /// storyline's rule for sex). The child's line starts on the mother's line.
        /// </summary>
        void PlaceNewborn(SmvPerson c, double t, int k, float center, float envelope)
        {
            bool single = rng.NextDouble() < data.SingleParent.At(c.Birth) / 100.0;
            double peak = Math.Max(24, Math.Min(31, SmvData.MedianFirstMarriageAge(false, c.Birth) + 4));
            Func<SmvPerson, double> fertility = m =>
            {
                if (t - m.LastBirth < BirthSpacing) return 0;
                double d = (t - m.Birth - peak) / 5.5;
                return Math.Exp(-0.5 * d * d) / (1 + 0.5 * m.Children);
            };

            SmvPerson mother = Pick(single ? singleMothers : marriedMothers, fertility) ??
                               Pick(single ? marriedMothers : singleMothers, fertility);
            c.BirthStep = k;
            if (mother == null) return;

            c.Mother = mother.Index;
            mother.Children++;
            mother.StateChanged = true;
            mother.LastBirth = t;
            BirthsWithMother++;

            SmvPerson father = null;
            if (mother.Spouse >= 0)
            {
                father = byIndex[mother.Spouse];
            }
            else
            {
                float vw = mother.Value;
                father = Pick(singleMen, m =>
                {
                    double age = t - m.Birth;
                    return age <= FatherMaxAge && SmvModel.CanPair(vw, m.Value) ? SmvModel.Attraction(m.Value) : 0;
                });
            }

            if (father != null)
            {
                c.Father = father.Index;
                father.Children++;
                father.StateChanged = true;
            }

            // born on the mother's line; smoothing then carries it into the children's core
            if (mother.Placed)
            {
                c.OffsetSmooth = mother.OffsetSmooth;
                c.HeightSmooth = mother.HeightSmooth;
                c.StartRho = center + envelope * c.OffsetSmooth;
                c.StartY = c.HeightSmooth;
                c.Placed = true;
            }
        }

        /// <summary>
        /// One marriage: a single woman chosen by age (around the era's median first-marriage age) and
        /// partner history, then a single man she can pair with (his value at least hers), weighted by her
        /// attraction to his value, similar value (assortative) and a typical age gap.
        /// </summary>
        void Marry(double t)
        {
            double medianW = SmvData.MedianFirstMarriageAge(false, t);
            double gap = SmvData.MedianFirstMarriageAge(true, t) - medianW;
            SmvPerson woman = Pick(singleWomen, p => p.Spouse >= 0
                ? 0
                : MarriageAgeWeight(t - p.Birth, medianW) * SmvModel.MarriageLikelihood(p.Partners));
            if (woman == null) return;

            float vw = woman.Value;
            double idealAge = t - woman.Birth + gap;
            SmvPerson man = Pick(singleMen, p =>
            {
                if (p.Spouse >= 0 || !SmvModel.CanPair(vw, p.Value)) return 0;
                double da = (t - p.Birth - idealAge) / 4.0;
                if (da * da > 16) return 0; // beyond four standard deviations of the age gap
                double dv = (p.Value - vw) / 1.5;
                return SmvModel.Attraction(p.Value) * Math.Exp(-0.5 * (dv * dv + da * da));
            });
            if (man == null) return;

            woman.Spouse = man.Index;
            man.Spouse = woman.Index;
            woman.MarriedAt = man.MarriedAt = t;
            Marriages++;
        }

        /// <summary>One divorce, most likely in the early years of a marriage.</summary>
        void Divorce(double t)
        {
            SmvPerson wife = Pick(wives, p =>
            {
                if (p.Spouse < 0) return 0;
                double years = t - p.MarriedAt;
                return (years < 1 ? 0.3 : 1.0) / (1 + years / 6);
            });
            if (wife == null) return;
            byIndex[wife.Spouse].Spouse = -1;
            wife.Spouse = -1;
            Divorces++;
        }

        static double MarriageAgeWeight(double age, double median)
        {
            if (age > 75) return 0;
            if (age <= median)
            {
                double d = (age - median) / 3.5;
                return Math.Exp(-0.5 * d * d);
            }

            // later first marriages and remarriages
            return Math.Max(0.03, Math.Exp(-(age - median) / 10));
        }

        /// <summary>
        /// Values and value ranks for this step. Ranks come from a counting sort over value bins (linear per
        /// step; ties within a bin keep the population order): every adult's rank among the adults of their
        /// sex becomes their offset from the band center, and married couples share one mirrored offset,
        /// drawn closer the longer they have been married.
        /// </summary>
        void Rank(double t, int k)
        {
            for (int s = 0; s < 2; s++)
            {
                Array.Clear(binRank[s], 0, ValueBins);
                adults[s] = 0;
            }

            for (int i = 0; i < aliveCount; i++)
            {
                SmvPerson p = alive[i];
                float age = (float)(t - p.Birth);
                if (age < SmvModel.AdultAge)
                {
                    p.Value = 0;
                    continue;
                }

                p.Value = ValueAt(p, age, k);
                int s = p.Male ? 1 : 0;
                p.ValueBin = Math.Min(ValueBins - 1, Math.Max(0, (int)(p.Value * (ValueBins / 10f))));
                binRank[s][p.ValueBin]++;
                adults[s]++;
            }

            // highest values first: turn bin counts into the rank of each bin's first member
            for (int s = 0; s < 2; s++)
            {
                int[] bins = binRank[s];
                int acc = 0;
                for (int b = ValueBins - 1; b >= 0; b--)
                {
                    int count = bins[b];
                    bins[b] = acc;
                    acc += count;
                }
            }

            for (int i = 0; i < aliveCount; i++)
            {
                SmvPerson p = alive[i];
                if (t - p.Birth < SmvModel.AdultAge) continue;
                int s = p.Male ? 1 : 0;
                p.RankOffset = Offset(binRank[s][p.ValueBin]++, adults[s], p.Spouse >= 0);
            }

            for (int i = 0; i < aliveCount; i++)
            {
                SmvPerson wife = alive[i];
                if (wife.Male || wife.Spouse < 0) continue;
                SmvPerson husband = byIndex[wife.Spouse];
                float years = (float)(t - wife.MarriedAt);
                float closeness = 1f - 0.4f * Math.Min(1f, years / 25f);
                float shared = 0.5f * (wife.RankOffset + husband.RankOffset) * closeness;
                wife.RankOffset = shared;
                husband.RankOffset = shared;
            }
        }

        /// <summary>
        /// An adult's social market value at step k. <see cref="SmvModel.Value"/> is evaluated exactly
        /// every other step (people alternate, so the work is spread evenly) and right after anything
        /// that changes it discontinuously (a child, a partner, adulthood); in between, the value moves
        /// on along the slope of its last two exact evaluations. Value is a smooth function of age between
        /// such events, so this halves the cost of the model's evaluation without changing the picture.
        /// </summary>
        float ValueAt(SmvPerson p, float age, int k)
        {
            bool fresh = p.ExactStep < 0 || p.StateChanged || k - p.ExactStep > 2;
            if (fresh || ((k + p.Index) & 1) == 0)
            {
                float v = SmvModel.Value(p.Male, p.Base, age, p.Children > 0, p.Partners, p.Wealth);
                p.ValueSlope = fresh ? 0f : (v - p.ExactValue) / (k - p.ExactStep);
                p.ExactValue = v;
                p.ExactStep = k;
                p.StateChanged = false;
                return v;
            }

            float guess = p.ExactValue + p.ValueSlope * (k - p.ExactStep);
            return guess < 0f ? 0f : guess > 10f ? 10f : guess;
        }

        /// <summary><see cref="SmvModel.RankOffset"/> through a lookup table (rank k of n).</summary>
        static float Offset(int k, int n, bool married)
        {
            if (n <= 1) return SmvModel.RankOffset(k, n, married);
            int i = (int)((k + 0.5f) / n * OffsetTableSize);
            return OffsetTable[married ? 1 : 0][Math.Min(OffsetTableSize - 1, Math.Max(0, i))];
        }

        static float[][] BuildOffsetTable()
        {
            float[][] table = { new float[OffsetTableSize], new float[OffsetTableSize] };
            for (int i = 0; i < OffsetTableSize; i++)
            {
                table[0][i] = SmvModel.RankOffset(i, OffsetTableSize, false);
                table[1][i] = SmvModel.RankOffset(i, OffsetTableSize, true);
            }

            return table;
        }

        /// <summary>Writes every living person's sample: smoothed toward their target place in the stream.</summary>
        void Place(double t, int k, float center, float envelope, float aOff, float aY)
        {
            float[] ys = SampleY, rhos = SampleRho;
            for (int j = 0; j < aliveCount; j++)
            {
                SmvPerson p = alive[j];
                float age = (float)(t - p.Birth);
                float off, y;
                if (age < SmvModel.AdultAge)
                {
                    off = ChildSpread * age / SmvModel.AdultAge;
                    y = SmvModel.Height(0f, age);
                }
                else
                {
                    off = p.RankOffset;
                    y = SmvModel.Height(p.Value, age);
                }

                if (!p.Male) off = -off;
                if (!p.Placed)
                {
                    p.OffsetSmooth = off;
                    p.HeightSmooth = y;
                    p.StartRho = center + envelope * off;
                    p.StartY = y;
                    p.Placed = true;
                }
                else
                {
                    p.OffsetSmooth += (off - p.OffsetSmooth) * aOff;
                    p.HeightSmooth += (y - p.HeightSmooth) * aY;
                }

                int i = p.SampleOffset + k - p.FirstStep;
                ys[i] = p.HeightSmooth;
                rhos[i] = center + envelope * p.OffsetSmooth;
            }
        }

        /// <summary>
        /// Weighted random choice (null when every weight is zero). Every weight function used here is
        /// bounded by 1 (products of SmvModel curves and Gaussians), so rejection sampling finds a choice in
        /// a few tries without scanning the list; a full scan is the fallback (e.g. when few candidates
        /// qualify). Both draw exactly from the weighted distribution.
        /// </summary>
        SmvPerson Pick(List<SmvPerson> list, Func<SmvPerson, double> weight)
        {
            int n = list.Count;
            if (n == 0) return null;
            for (int attempt = 0; attempt < RejectionTries; attempt++)
            {
                SmvPerson p = list[rng.Next(n)];
                if (rng.NextDouble() < weight(p)) return p;
            }

            if (weights.Length < n) weights = new double[n * 2];
            double total = 0;
            for (int i = 0; i < n; i++)
            {
                double w = weight(list[i]);
                weights[i] = w;
                total += w;
            }

            if (total <= 0) return null;
            double r = rng.NextDouble() * total;
            int lastPositive = -1;
            for (int i = 0; i < n; i++)
            {
                if (weights[i] <= 0) continue;
                lastPositive = i;
                r -= weights[i];
                if (r <= 0) return list[i];
            }

            return lastPositive >= 0 ? list[lastPositive] : null;
        }
    }
}
