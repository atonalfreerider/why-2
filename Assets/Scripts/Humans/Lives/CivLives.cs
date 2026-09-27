using System;
using UnityEngine;

namespace Why.Humans.Lives
{
    /// <summary>One simulated lifeline: a statistical person standing for many real ones.</summary>
    public struct Person
    {
        /// <summary>Calendar year of birth.</summary>
        public double Birth;

        /// <summary>Calendar year the line ends (death, the end of the stream, or where the drawing stops).</summary>
        public double End;

        public bool Male;

        /// <summary>Killed in a war: the line ends inside it.</summary>
        public bool DiedInWar;

        /// <summary>False for lives another layer draws (US lives that reach 1950); they still count in ranks.</summary>
        public bool Drawn;

        /// <summary>Social market value parameters (see <see cref="SmvModel"/>).</summary>
        public float Base, Wealth, ChildAge, Partners;

        /// <summary>Stable tie-breaker in [0, 1) so people of equal value keep their order between moments.</summary>
        public float Tie;

        /// <summary>First grid cell strictly after birth.</summary>
        public int FirstCell;

        /// <summary>Grid cells strictly inside the life.</summary>
        public int Interior;

        /// <summary>
        /// Index of the birth point in the point arrays. Slots are birth, one per interior cell, end; the drawn
        /// points (childhood thinned out) are packed from here, <see cref="PointCount"/> of them.
        /// </summary>
        public int FirstPoint;

        /// <summary>Drawn points, packed from <see cref="FirstPoint"/>.</summary>
        public int PointCount;

        /// <summary>Point slots reserved for this life.</summary>
        public int Slots => Interior + 2;
    }

    /// <summary>
    /// The statistical population of one stream, laid out as lifelines in data space (worker thread):
    /// <list type="number">
    /// <item>births per year B(t) = population(t) x crude birth rate(t); each line stands for N people, N a
    /// round number chosen so the coarse tier has at most <see cref="MaxCoarseLines"/> lines. The fine tier
    /// has <see cref="FineFactor"/> x the lines (N / <see cref="FineFactor"/> people each) and contains the
    /// coarse lines (every <see cref="FineFactor"/>-th birth), so zooming in only adds lines;</item>
    /// <item>sex from the sex ratio at birth; lifespan by inverse sampling of the era's life table, scaled by
    /// the female advantage; men of military age die in the stream's wars with the war's male mortality;</item>
    /// <item>social market value along the life (<see cref="SmvModel"/>): height above the layer;</item>
    /// <item>placement per grid cell: adults of each sex ranked by value, high value near the band center,
    /// low value at the envelope edge (women inside, men outside), children close to the center.</item>
    /// </list>
    /// Few people are alive at once in a sparse stream, so each moment's ranking also counts
    /// <see cref="PhantomRanks"/> phantom people spread over the stream's whole value distribution:
    /// with many alive this is the rank among the living, with few it is the rank in the stream's history.
    /// </summary>
    public sealed class CivLives
    {
        /// <summary>Children stay close to the band center: their offset is this share of the envelope at 18.</summary>
        public const float ChildSpread = 0.05f;

        /// <summary>Lines of the fine tier per line of the coarse tier.</summary>
        public const int FineFactor = 4;

        /// <summary>Upper bound of the coarse tier's lines per stream.</summary>
        public const double MaxCoarseLines = 400;

        /// <summary>Smallest number of people a coarse line stands for.</summary>
        public const double MinPeoplePerLine = 1e4;

        /// <summary>US lives reaching this year are simulated in detail by the United States (smv) layer.</summary>
        public const double UsCutoffYear = 1950;

        public const string UsId = "united_states";

        /// <summary>Lifeline numbers of the US start here (the smv layer numbers its own lines from 0).</summary>
        public const int UsIdOffset = 90_000;

        // births are sampled from a piecewise-linear CDF (population and birth rates change slowly)
        const double CdfCellYears = 4;
        const int MaxCdfCells = 1000;

        // how the prehistoric hand-over and the United States hand-over fade out (years)
        const double HandOverFadeBefore = 2 * HumanWorld.EmergeYears;
        const double UsHandOverYears = 8;

        // social market value parameters (the smv storyline: most women have a first child young, some men)
        const float WealthSkew = 1.6f;
        const float MotherShare = 0.85f;
        const float FatherShare = 0.6f;
        const float MotherAgeMin = 18f, MotherAgeRange = 8f;
        const float FatherAgeMin = 20f, FatherAgeRange = 12f;
        const float PartnerStartAge = 16f, PartnerRampYears = 24f;
        const float PreModernPartners = 1.3f, ModernPartners = 4.5f, MalePartnerRatio = 1.5f;

        // placement
        const float PhantomRanks = 8f;
        const int RankResolution = 4096;
        const float TieEpsilon = 1e-4f;
        const int ValueBins = 1000;
        const int InsertionSortMax = 24;

        /// <summary>Childhood is a smooth drift from the center: one drawn point every this many years is enough.</summary>
        const double ChildPointYears = 6;

        static readonly double[] RoundSteps = { 1, 2, 5 };

        /// <summary><see cref="SmvModel.RankOffset"/> tabulated over fractional ranks.</summary>
        static readonly float[] RankOffsets = BuildRankOffsets();

        public readonly Civ Civ;
        public readonly CivWars Wars;

        /// <summary>True for prehistoric humanity (300,000 years).</summary>
        public readonly bool Prehistoric;

        /// <summary>People a coarse line stands for (a fine line stands for this / <see cref="FineFactor"/>).</summary>
        public double PeoplePerLine { get; private set; }

        /// <summary>Largest population of the stream (the envelope's reference).</summary>
        public double PeakPopulation { get; private set; }

        /// <summary>Births simulated over the stream's span (people).</summary>
        public double TotalBirths { get; private set; }

        /// <summary>Added to the birth order to form lifeline numbers (see <see cref="UsIdOffset"/>).</summary>
        public int IdOffset { get; private set; }

        /// <summary>Calendar year where this layer's lines of the stream end (the stream's end, the US cutoff, now).</summary>
        public double LineEnd { get; private set; }

        public CivEnvelope Grid { get; private set; }

        /// <summary>People in birth order (the fine tier; index % FineFactor == 0 is also the coarse tier).</summary>
        public Person[] People { get; private set; } = Array.Empty<Person>();

        /// <summary>Drawn points (see <see cref="Person.FirstPoint"/>): data position, alpha multiplier, adult.</summary>
        public Vector3[] PtData;

        public float[] PtFade;
        public bool[] PtAdult;

        readonly HumanWorld world;
        readonly bool isUs;
        double spanStart, birthEnd, fadeFrom, fadeTo;
        double[] cdf;
        double cdfStep;

        // per slot: value and rank at interior cells
        float[] slotValue, slotRank;

        // share of each sex's adult values above a value bin (the phantoms)
        float[] aboveWomen, aboveMen;

        CivLives(HumanWorld world, Civ civ, CivWars wars)
        {
            this.world = world;
            Civ = civ;
            Wars = wars;
            Prehistoric = civ == world.Humanity;
            isUs = civ.Id == UsId;
        }

        /// <summary>Simulates and lays out one stream. Always returns the stream (with no lines if it has no births).</summary>
        public static CivLives Build(HumanWorld world, Civ civ, CivWars wars, int seed)
        {
            CivLives lives = new CivLives(world, civ, wars);
            if (!lives.Setup()) return lives;
            lives.Populate(new System.Random(seed));
            lives.ComputeValues();
            lives.ComputeRanks();
            lives.ComputePositions();
            lives.slotValue = lives.slotRank = lives.aboveWomen = lives.aboveMen = null;
            return lives;
        }

        /// <summary>Envelope of this stream at any year it exists (figures use it outside the grid).</summary>
        public bool Sample(double year, out float center, out float envWomen, out float envMen) =>
            CivEnvelope.Sample(world, Civ, Wars, PeakPopulation, year, out center, out envWomen, out envMen);

        /// <summary>
        /// Alpha multiplier where the stream's lines end: its dissolution, or prehistory's hand-over to the first
        /// civilizations. The lines of the living run into the present at full strength.
        /// </summary>
        public float EndFade(double year) => FadeOut(year, fadeFrom, fadeTo);

        /// <summary>Coarse lifelines alive at a calendar year, in expectation (population / people per line).</summary>
        public double LinesAlive(double year) =>
            PeoplePerLine > 0 ? world.Population(Civ, year) / PeoplePerLine : 0;

        /// <summary>
        /// Alpha multiplier of the population curve: like <see cref="EndFade"/>, and for the United States also
        /// handing over to the smv layer's detailed population at <see cref="UsCutoffYear"/>.
        /// </summary>
        public float CurveFade(double year) =>
            EndFade(year) * (isUs ? FadeOut(year, UsCutoffYear - UsHandOverYears, UsCutoffYear) : 1f);

        static float FadeOut(double year, double from, double to)
        {
            if (year <= from) return 1f;
            float t = Mathf.Clamp01((float)((year - from) / Math.Max(to - from, 1e-6)));
            return 1f - t * t * (3f - 2f * t);
        }

        // ------------------------------------------------------------------ births

        bool Setup()
        {
            double now = world.NowYear;
            (double start, double end) = world.Span(Civ);
            spanStart = start;
            if (Prehistoric)
            {
                // prehistoric humanity hands over to the first civilizations (its band dissolves after that)
                birthEnd = world.HistomapStartYear;
                LineEnd = world.HistomapStartYear + HumanWorld.EmergeYears;
                fadeFrom = world.HistomapStartYear - HandOverFadeBefore;
                fadeTo = LineEnd;
            }
            else if (Civ.Extant)
            {
                // every line of the living arrives at the present moment
                birthEnd = LineEnd = now;
                fadeFrom = fadeTo = double.PositiveInfinity;
            }
            else
            {
                // the band dissolves over its last EmergeYears; the lines fade with it
                birthEnd = LineEnd = end;
                fadeFrom = end - HumanWorld.EmergeYears;
                fadeTo = end;
            }

            if (isUs)
            {
                birthEnd = Math.Min(birthEnd, UsCutoffYear);
                LineEnd = UsCutoffYear;
                IdOffset = UsIdOffset;
            }

            if (birthEnd <= spanStart + 1) return false;

            int cells = Mathf.Clamp((int)Math.Ceiling((birthEnd - spanStart) / CdfCellYears), 1, MaxCdfCells);
            cdfStep = (birthEnd - spanStart) / cells;
            cdf = new double[cells + 1];
            double peak = 0;
            for (int i = 0; i < cells; i++)
            {
                double year = spanStart + (i + 0.5) * cdfStep;
                double population = world.Population(Civ, year);
                peak = Math.Max(peak, population);
                cdf[i + 1] = cdf[i] + population * world.BirthRatePer1000(year) / 1000.0 * cdfStep;
            }

            // the envelope's reference also covers what is drawn after the last birth (US figures after 1950)
            for (double year = birthEnd; year < end; year += CdfCellYears)
            {
                peak = Math.Max(peak, world.Population(Civ, year));
            }

            PeakPopulation = Math.Max(peak, 1);
            TotalBirths = cdf[cells];
            if (TotalBirths <= 0) return false;

            PeoplePerLine = RoundPeoplePerLine(TotalBirths / MaxCoarseLines);
            int coarse = Math.Max(1, (int)Math.Round(TotalBirths / PeoplePerLine));
            People = new Person[coarse * FineFactor];
            Grid = new CivEnvelope(world, Civ, Wars, PeakPopulation, spanStart, LineEnd);
            return true;
        }

        /// <summary>The smallest of 1, 2, 5 x 10^k (at least <see cref="MinPeoplePerLine"/>) not below the target.</summary>
        static double RoundPeoplePerLine(double target)
        {
            for (double decade = MinPeoplePerLine; decade < 1e13; decade *= 10)
            {
                foreach (double m in RoundSteps)
                {
                    if (m * decade >= target) return m * decade;
                }
            }

            return 1e13;
        }

        // ------------------------------------------------------------------ people

        void Populate(System.Random rng)
        {
            int n = People.Length;
            double total = TotalBirths;
            double maleShare = world.SexRatioAtBirth / (1 + world.SexRatioAtBirth);
            int cell = 0;
            int slots = 0;
            for (int i = 0; i < n; i++)
            {
                // stratified: the i-th birth falls in the i-th n-quantile of all births
                double x = (i + rng.NextDouble()) / n * total;
                while (cell < cdf.Length - 2 && cdf[cell + 1] < x) cell++;
                double span = cdf[cell + 1] - cdf[cell];
                double f = span > 0 ? Math.Min(1, Math.Max(0, (x - cdf[cell]) / span)) : 0.5;
                double birth = spanStart + (cell + f) * cdfStep;

                ref Person p = ref People[i];
                p.Birth = birth;
                p.Male = rng.NextDouble() < maleShare;
                double natural = birth + DeathAge(world.LifeTableFor(birth), p.Male, 1.0 - rng.NextDouble());
                double end = natural;
                if (p.Male && Wars.DrawWarDeath(birth, natural, rng, out double warDeath))
                {
                    end = warDeath;
                    p.DiedInWar = true;
                }

                p.Drawn = !isUs || end < UsCutoffYear;
                if (end > LineEnd)
                {
                    end = LineEnd;
                    p.DiedInWar = false;
                }

                p.End = Math.Max(end, birth);
                p.Base = SmvModel.SampleBase(rng);
                p.Wealth = p.Male ? Mathf.Pow((float)rng.NextDouble(), WealthSkew) : 0f;
                bool parent = rng.NextDouble() < (p.Male ? FatherShare : MotherShare);
                float parentAge = p.Male
                    ? FatherAgeMin + FatherAgeRange * (float)rng.NextDouble()
                    : MotherAgeMin + MotherAgeRange * (float)rng.NextDouble();
                p.ChildAge = parent ? parentAge : float.PositiveInfinity;
                p.Partners = 1f + (LifetimePartners(birth, p.Male) - 1f) * (float)-Math.Log(1.0 - rng.NextDouble());
                p.Tie = (float)rng.NextDouble();

                // grid cells strictly inside (birth, end)
                p.FirstCell = Grid.FirstAfter(birth);
                p.Interior = Math.Max(0, Grid.LastBefore(p.End) - p.FirstCell + 1);
                p.FirstPoint = slots;
                slots += p.Slots;
            }

            PtData = new Vector3[slots];
            PtFade = new float[slots];
            PtAdult = new bool[slots];
            slotValue = new float[slots];
            slotRank = new float[slots];
            cdf = null;
        }

        /// <summary>
        /// Age at death by inverse sampling of the life table's survival curve (linear between ages). The
        /// female advantage in life expectancy is applied as a scale (women +adv/2, men -adv/2 in e0), which
        /// keeps infant and child mortality where the table puts it.
        /// </summary>
        static double DeathAge(LifeTable table, bool male, double q)
        {
            double[] ages = table?.ages, survival = table?.survival;
            int n = ages != null && survival != null ? Math.Min(ages.Length, survival.Length) : 0;
            if (n < 2)
            {
                // no table: a pre-modern exponential with a mean of 30 years
                return -30.0 * Math.Log(Math.Max(q, 1e-9));
            }

            double age = ages[n - 1] + 1; // survived the whole table
            for (int i = 1; i < n; i++)
            {
                if (survival[i] > q) continue;
                double drop = survival[i - 1] - survival[i];
                double f = drop > 1e-12 ? (survival[i - 1] - q) / drop : 1;
                age = ages[i - 1] + Math.Min(1, Math.Max(0, f)) * (ages[i] - ages[i - 1]);
                break;
            }

            double e0 = Math.Max(table.e0, 1);
            double shift = 0.5 * table.femaleE0Advantage / e0;
            return Math.Max(0, age * (male ? 1 - shift : 1 + shift));
        }

        /// <summary>Mean lifetime partners by birth year: modest before 1900, rising to modern levels by 1960.</summary>
        static float LifetimePartners(double birthYear, bool male)
        {
            float modern = Mathf.Clamp01((float)((birthYear - 1900) / 60));
            float women = Mathf.Lerp(PreModernPartners, ModernPartners, modern);
            return male ? women * MalePartnerRatio : women;
        }

        static float Value(in Person p, double age)
        {
            if (age < SmvModel.AdultAge) return 0f;
            float a = (float)age;
            int partners = (int)(p.Partners * Mathf.Clamp01((a - PartnerStartAge) / PartnerRampYears));
            return SmvModel.Value(p.Male, p.Base, a, a >= p.ChildAge, partners, p.Wealth);
        }

        // ------------------------------------------------------------------ layout

        /// <summary>Pass 1: value at every interior cell, and each sex's distribution of adult values (the phantoms).</summary>
        void ComputeValues()
        {
            int[] histWomen = new int[ValueBins + 1], histMen = new int[ValueBins + 1];
            for (int i = 0; i < People.Length; i++)
            {
                ref Person p = ref People[i];
                for (int j = 0; j < p.Interior; j++)
                {
                    double age = Grid.Time(p.FirstCell + j) - p.Birth;
                    float v = Value(p, age);
                    slotValue[p.FirstPoint + 1 + j] = v;
                    if (age >= SmvModel.AdultAge) (p.Male ? histMen : histWomen)[Bin(v)]++;
                }
            }

            aboveWomen = SharesAbove(histWomen);
            aboveMen = SharesAbove(histMen);
        }

        static int Bin(float v) => Mathf.Clamp((int)(v * (ValueBins / 10f)), 0, ValueBins);

        /// <summary>For each value bin, the share of the distribution in higher bins.</summary>
        static float[] SharesAbove(int[] hist)
        {
            float[] above = new float[hist.Length];
            long total = 0;
            foreach (int c in hist) total += c;
            if (total == 0)
            {
                for (int b = 0; b < above.Length; b++) above[b] = 0.5f;
                return above;
            }

            long higher = 0;
            for (int b = hist.Length - 1; b >= 0; b--)
            {
                above[b] = higher / (float)total;
                higher += hist[b];
            }

            return above;
        }

        /// <summary>Pass 2: at every grid cell, rank the living adults of each sex by value.</summary>
        void ComputeRanks()
        {
            int[] active = new int[64];
            int activeCount = 0;
            double[] keysW = new double[64], keysM = new double[64];
            int[] slotsW = new int[64], slotsM = new int[64];
            int next = 0;
            int n = People.Length;
            for (int k = 0; k < Grid.Count; k++)
            {
                while (next < n && People[next].FirstCell <= k)
                {
                    if (People[next].Interior > 0)
                    {
                        if (activeCount == active.Length) Array.Resize(ref active, active.Length * 2);
                        active[activeCount++] = next;
                    }

                    next++;
                }

                int alive = 0;
                for (int a = 0; a < activeCount; a++)
                {
                    int i = active[a];
                    if (People[i].FirstCell + People[i].Interior - 1 >= k) active[alive++] = i;
                }

                activeCount = alive;
                if (activeCount == 0)
                {
                    if (next >= n) break;
                    k = Math.Max(k, People[next].FirstCell - 1); // skip empty stretches of time
                    continue;
                }

                if (activeCount > keysW.Length)
                {
                    int size = activeCount * 2;
                    Array.Resize(ref keysW, size);
                    Array.Resize(ref keysM, size);
                    Array.Resize(ref slotsW, size);
                    Array.Resize(ref slotsM, size);
                }

                double t = Grid.Time(k);
                int nw = 0, nm = 0;
                for (int a = 0; a < activeCount; a++)
                {
                    ref Person p = ref People[active[a]];
                    if (t - p.Birth < SmvModel.AdultAge) continue;
                    int slot = p.FirstPoint + 1 + (k - p.FirstCell);
                    double key = -(slotValue[slot] + TieEpsilon * p.Tie); // descending value
                    if (p.Male)
                    {
                        keysM[nm] = key;
                        slotsM[nm++] = slot;
                    }
                    else
                    {
                        keysW[nw] = key;
                        slotsW[nw++] = slot;
                    }
                }

                RankGroup(keysW, slotsW, nw, aboveWomen);
                RankGroup(keysM, slotsM, nm, aboveMen);
            }
        }

        void RankGroup(double[] keys, int[] slots, int count, float[] above)
        {
            if (count == 0) return;
            if (count <= InsertionSortMax) InsertionSort(keys, slots, count);
            else Array.Sort(keys, slots, 0, count);
            for (int r = 0; r < count; r++)
            {
                int slot = slots[r];
                slotRank[slot] = (r + 0.5f + PhantomRanks * above[Bin(slotValue[slot])]) / (count + PhantomRanks);
            }
        }

        static void InsertionSort(double[] keys, int[] items, int count)
        {
            for (int i = 1; i < count; i++)
            {
                double key = keys[i];
                int item = items[i];
                int j = i - 1;
                while (j >= 0 && keys[j] > key)
                {
                    keys[j + 1] = keys[j];
                    items[j + 1] = items[j];
                    j--;
                }

                keys[j + 1] = key;
                items[j + 1] = item;
            }
        }

        /// <summary>Offset from the band center (share of the envelope) for a fractional rank (0 = highest value).</summary>
        static float RankOffset(float rank) => RankOffsets[Mathf.Clamp((int)(rank * RankResolution), 0, RankResolution - 1)];

        static float[] BuildRankOffsets()
        {
            float[] table = new float[RankResolution];
            for (int k = 0; k < RankResolution; k++) table[k] = SmvModel.RankOffset(k, RankResolution, false);
            return table;
        }

        /// <summary>
        /// Pass 3: data-space position of every drawn point. Adults get a point at every cell (ranks change);
        /// childhood, a slow drift away from the center, is thinned to one point per
        /// <see cref="ChildPointYears"/>.
        /// </summary>
        void ComputePositions()
        {
            for (int i = 0; i < People.Length; i++)
            {
                ref Person p = ref People[i];
                if (!p.Drawn) continue;
                float side = p.Male ? 1f : -1f;
                int at = p.FirstPoint;

                // birth: the center of the band, on the floor of the layer
                Grid.Between(p.FirstCell - 1, p.Birth, out float u, out float center, out float _, out float _);
                Put(ref at, new Vector3(u, SmvModel.Height(0f, 0f), center), EndFade(p.Birth), false);

                float lastRank = float.NaN;
                double lastTime = p.Birth;
                double adultFrom = p.Birth + SmvModel.AdultAge;
                for (int j = 0; j < p.Interior; j++)
                {
                    int k = p.FirstCell + j;
                    int slot = p.FirstPoint + 1 + j;
                    double t = Grid.Time(k);
                    bool adult = t >= adultFrom;
                    if (!adult && t - lastTime < ChildPointYears && j < p.Interior - 1 && Grid.Time(k + 1) < adultFrom)
                    {
                        continue; // thinned childhood
                    }

                    float age = (float)(t - p.Birth);
                    float env = Grid.Envelope(k, p.Male);
                    float offset;
                    if (adult)
                    {
                        lastRank = slotRank[slot];
                        offset = RankOffset(lastRank) * env;
                    }
                    else
                    {
                        offset = ChildSpread * age / SmvModel.AdultAge * env;
                    }

                    lastTime = t;
                    Put(ref at, new Vector3(Grid.U(k), SmvModel.Height(slotValue[slot], age), Grid.Center(k) + side * offset),
                        EndFade(t), adult);
                }

                // end: keeps the last rank (or the rank in the stream's history if never ranked)
                float endAge = (float)(p.End - p.Birth);
                float endValue = Value(p, endAge);
                Grid.Between(p.FirstCell + p.Interior - 1, p.End, out u, out center, out float envW, out float envM);
                float envEnd = p.Male ? envM : envW;
                bool adultEnd = endAge >= SmvModel.AdultAge;
                float endOffset;
                if (adultEnd)
                {
                    float[] above = p.Male ? aboveMen : aboveWomen;
                    float rank = float.IsNaN(lastRank)
                        ? (0.5f + PhantomRanks * above[Bin(endValue)]) / (1 + PhantomRanks)
                        : lastRank;
                    endOffset = RankOffset(rank) * envEnd;
                }
                else
                {
                    endOffset = ChildSpread * endAge / SmvModel.AdultAge * envEnd;
                }

                Put(ref at, new Vector3(u, SmvModel.Height(endValue, endAge), center + side * endOffset), EndFade(p.End),
                    adultEnd);
                p.PointCount = at - p.FirstPoint;
            }
        }

        void Put(ref int at, Vector3 data, float fade, bool adult)
        {
            PtData[at] = data;
            PtFade[at] = fade;
            PtAdult[at] = adult;
            at++;
        }
    }
}
