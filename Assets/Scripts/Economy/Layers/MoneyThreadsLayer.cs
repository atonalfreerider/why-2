using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using UnityEngine;
using Why.Economy.Data;
using Why.Economy.Model;
using Why.Humans.Smv;
using Debug = UnityEngine.Debug;

namespace Why.Economy.Layers
{
    /// <summary>
    /// The money between the industry wall and the people above it: threads of light at sampled years, one adult line in
    /// six every four years from 1950. Income rises from the wall to each sampled person's lifeline, one thread from the
    /// person's largest source: wages (light blue) from the wages part of the band of the industry the person works in,
    /// business income (gold) from the owners' part of their own industry, capital income (gold) from the owners' part
    /// of the industries that pay it (dividends from every private industry's profits, rent from real estate, interest
    /// from banking, mixed by the person's wealth group as the Fed's distributional accounts hold them), or government
    /// benefits (steel) from the federal band. Spending falls from the person back into the wall: up to three threads,
    /// for the person's largest spending categories, each to the industry that receives most of that category's money
    /// (its items' industry weights), tinted by the category's motive (<see cref="EconomyStyle.Motive"/>: rose desire,
    /// ice fear; money with mixed motives, necessities above all, is drawn fainter).
    ///
    /// Every thread is a smooth arc in the plane of constant time through the person's point: income peels off the
    /// front of the wall (toward the road's inside) and spending returns to its back. Width and brightness follow the
    /// dollars on a log scale relative to the year's median spending; pulses run along each thread, up for income and
    /// down for spending, out of step. A year's money is drawn as a sheaf two years wide so the threads can be told
    /// apart: income on its past half and spending on its present half (earned, then spent), and in each half the
    /// people in order of household income, so the gold of capital gathers at the present edge of each income half.
    ///
    /// Everything is data space (warped with the road and the wall); the threads land exactly on the wall's bands
    /// (<see cref="WallGeometry"/>, shared by <see cref="IndustryWallLayer"/>, which prepares in the tier before this
    /// one). They fade in with the population (once 1950 - now spans a few hundred pixels) and out at the stations, and
    /// thin out (alpha) the more threads share a pixel. The inspected person (<see cref="EconomyState.Person"/>) gets a
    /// separate bright mesh with every thread (every source of income) of every adult year, rebuilt when the choice
    /// changes, while the sample dims.
    /// </summary>
    [GraphScenes("economy-retired")]   // retired by the landscape (WP0); deleted by WP5
    public sealed class MoneyThreadsLayer : GraphLayer
    {
        // ------------------------------------------------------------------ sampling

        /// <summary>
        /// Sheaves every <see cref="SheafStep"/> years from <see cref="FirstSheafYear"/> (plus the last full year).
        /// </summary>
        const int FirstSheafYear = 1950, SheafStep = 4;

        /// <summary>
        /// One adult line in this many is sampled (deterministic by the person's index, the same people every sheaf).
        /// </summary>
        const int SampleStride = 6;

        /// <summary>
        /// A sheaf is two halves, each this many years wide, either side of the year's middle: income rises on the past side
        /// (earned, then spent) and spending falls on the present side, with this gap between them. In each half the people
        /// stand in order of household income, the poorest first.
        /// </summary>
        const float SheafHalfYears = 0.95f, SheafGapYears = 0.1f;

        /// <summary>
        /// The inspected person's income and spending of a year sit this far either side of the year's middle.
        /// </summary>
        const float PersonOffsetYears = 0.22f;

        /// <summary>
        /// A little extra spread (years) so a couple, who share one household rank, do not draw one thread twice.
        /// </summary>
        const float SheafJitterYears = 0.08f;

        /// <summary>
        /// Largest categories drawn per person, and the smallest share of their spending a drawn category has.
        /// </summary>
        const int MaxSpendingThreads = 3;

        const float MinCategoryShare = 0.1f;

        /// <summary>
        /// Money below this share of the year's median spending draws no thread (a dust of invisible lines).
        /// </summary>
        const float MinOfMedian = 0.05f;

        /// <summary>
        /// The year the findings are told for (the in-control share is calibrated to 2025 evidence; the data's last year,
        /// 2026, is a partial-year estimate).
        /// </summary>
        const int FindingsYear = 2025;

        /// <summary>
        /// The findings' labels stand above the lifelines this many years before the findings' year, inside the people view
        /// (which ends at the present) and apart from each other: the people in control's (right-aligned, so it ends near
        /// the year it tells of, clear of the Generation Alpha marker's name around 2010), then the fantasy's.
        /// </summary>
        const int OwnersLabelYears = 3, FantasyLabelYears = 18;

        /// <summary>
        /// The threads' own labels stand at the sheaves nearest these years, where the gap between the wall's top and the
        /// people is wide (the wall is still low) and the views that show the whole road have room.
        /// </summary>
        const int IncomeLabelYear = 1974, SpendingLabelYear = 1998;

        /// <summary>Two shares closer than this are told as the same (the findings' blurbs).</summary>
        const float SameShare = 0.02f;

        // ------------------------------------------------------------------ shape

        /// <summary>Points per thread (a cubic arc; seen mostly edge-on, so few points are smooth enough).</summary>
        const int ThreadPoints = 10;

        /// <summary>How far (data rho) a thread bows out of the wall's plane as it leaves or returns to it.</summary>
        const float LeaveRho = 0.09f;

        /// <summary>
        /// Shape of the arc as shares of its rise: it leaves the wall along its plane's normal for this share of the rise,
        /// then turns up and arrives at the person from below over the last share.
        /// </summary>
        const float LeaveShare = 0.3f, ArriveShare = 0.4f;

        /// <summary>Threads attach inside their part of a band, this share of its height away from its edges.</summary>
        const float AttachInset = 0.15f;

        /// <summary>
        /// A thread is dimmer where it enters the wall (this much of its alpha) and brightens over this share of its
        /// path.
        /// </summary>
        const float WallEndAlpha = 0.35f, WallEndLength = 0.3f;

        // ------------------------------------------------------------------ dollars to width and alpha

        /// <summary>
        /// Log2 range of the dollar scale: a thread of the year's median spending is in the middle, one at
        /// 2^(-LogSpan/2) of it is the thinnest and faintest, one at 2^(LogSpan/2) times it the widest and brightest.
        /// </summary>
        const float LogSpan = 7f;

        const float MinWidthPx = 0.7f, MaxWidthPx = 2.6f, MinAlpha = 0.1f, MaxAlpha = 1f;

        /// <summary>
        /// The sampled threads: alpha (their material's, times each thread's own by its dollars, before the density
        /// factor) and HDR intensity. Each is faint: they are seen together, as the sheaf's colors, with the largest sums
        /// of money standing out.
        /// </summary>
        const float SampleAlpha = 0.045f, SampleIntensity = 1f;

        /// <summary>
        /// The inspected person's threads: alpha, width factor and intensity (they glow); the sample dims meanwhile.
        /// </summary>
        const float PersonAlpha = 0.95f, PersonWidth = 1.5f, PersonIntensity = 1.3f, InspectDim = 0.3f;

        // ------------------------------------------------------------------ pulses

        /// <summary>
        /// Pulses per data unit of path (a thread is 0.3 - 0.9 long, so two to four pulses), their speed (phase units per
        /// second: a pulse crosses a thread in about four seconds) and the path phase per data unit.
        /// </summary>
        const float FlowFreq = 4f, FlowSpeed = 0.8f, PhasePerUnit = 1f;

        // ------------------------------------------------------------------ level of detail

        /// <summary>The threads appear as 1950 - now grows from this many pixels long on screen to the next.</summary>
        const float EraFromPx = 250, EraFullPx = 600;

        /// <summary>
        /// A sheaf holds about a thousand threads, and its width on screen is the road's pixels per year: alpha follows
        /// (pixels per year / reference)^exponent, between the minimum and 1, so a far view (many threads per pixel) does not
        /// burn into white, and closer up single threads gain alpha while the sheaf as a whole grows fainter per pixel (the
        /// people, not the threads, are the subject of the close views).
        /// </summary>
        const float CrowdReferencePxPerYear = 650f, CrowdExponent = 0.35f, CrowdMin = 0.2f;

        const float FadeSpeed = 1.8f;

        /// <summary>
        /// While the camera's target is less than this far before the first station along the road, the camera looks at
        /// the stations: the threads hide (as the circuit's diagram hides the timeline's labels).
        /// </summary>
        const float StationsAhead = EconomyStyle.FirstStationGap * 0.5f;

        /// <summary>Era probes: points along the road from 1950 to now, projected every frame.</summary>
        const int ProbeCount = 12;

        // ------------------------------------------------------------------ kinds and colors

        const int KindWages = 0, KindCapital = 1, KindTransfers = 2, KindSpending = 3;

        /// <summary>Wages: the people's blue, lightened so a thread reads against the bundle of lifelines.</summary>
        static readonly Color WagesColor = new Color(0.4f, 0.62f, 1f);

        /// <summary>
        /// Spending with a mixed motive (necessities: as much fear as desire) is drawn at this share of the alpha of a clear
        /// one, rising with the motive's strength |2 fearShare - 1|^<see cref="MotiveExponent"/>: intensity is attention,
        /// and the motive is what the spending threads are about.
        /// </summary>
        const float MixedMotiveAlpha = 0.45f, MotiveExponent = 0.5f;

        /// <summary>
        /// Thread colors are this much more saturated than the palette's: a thread is faint (many add up in a sheaf), and a
        /// faint pastel reads as grey.
        /// </summary>
        const float ThreadSaturation = 1.5f;

        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        public override int Order => 50;

        // inputs (Prepare; read by the inspected person's rebuilds on the main thread)
        EconomicLives lives;
        EconomyData data;
        SmvPopulation pop;
        WallGeometry wall;
        int[] categoryIndustry = Array.Empty<int>();
        float[] categoryFear = Array.Empty<float>(), categoryEmphasis = Array.Empty<float>();
        int federal = -1, firstYear;
        float[] medianSpending = Array.Empty<float>();
        float[][] capitalCdf = Array.Empty<float[]>();

        // geometry
        LineMeshBuilder pendingSample;
        Material sampleMat, personMat;
        MeshRenderer sampleRenderer, personRenderer;
        MeshFilter sampleFilter, personFilter;
        readonly List<LinePoint> scratch = new List<LinePoint>(ThreadPoints);
        int shownPerson = -1, seenVersion = -1, sampledThreads;
        bool uploaded;

        // level of detail
        Vector3[] probeMid = Array.Empty<Vector3>();
        float[] probeYears = Array.Empty<float>();
        Station circuitStation;
        float visible, crowd = 1f, inspect = 1f, sampleAlpha = -1f, personAlpha = -1f;
        readonly List<LabelSpec> labels = new List<LabelSpec>();
        bool labelsShown = true;

        /// <summary>
        /// How a set of threads is drawn: alpha and width factors, intensity, and whether ids are the person's line.
        /// </summary>
        readonly struct Look
        {
            public readonly float Alpha, Width, Intensity;

            /// <summary>
            /// Highlight id for every thread (the inspected person's lifeline), or -1 for the threads' own kind ids.
            /// </summary>
            public readonly int Id;

            /// <summary>
            /// Every source of income gets a thread (the inspected person), not only the largest (the sample).
            /// </summary>
            public readonly bool AllIncome;

            public Look(float alpha, float width, float intensity, int id, bool allIncome)
            {
                Alpha = alpha;
                Width = width;
                Intensity = intensity;
                Id = id;
                AllIncome = allIncome;
            }
        }

        // ================================================================ prepare (worker thread)

        public override void Prepare(GraphContext ctx)
        {
            Stopwatch sw = Stopwatch.StartNew();
            EconomyModel model = ctx.Shared<EconomyModel>(EconomyModel.SharedKey);
            pop = ctx.Shared<SmvPopulation>(SmvPopulation.SharedKey);
            wall = ctx.Shared<WallGeometry>(WallGeometry.SharedKey);
            if (model?.Data == null || model.Lives?.Ready != true || pop?.Sim == null || wall == null || !wall.IsValid)
            {
                Debug.LogWarning("[Why] MoneyThreadsLayer: no lives, population or wall; threads skipped");
                return;
            }

            data = model.Data;
            lives = model.Lives;
            firstYear = lives.FirstYear;
            PrepareInputs();

            List<int> sheaves = SheafYears(ctx.NowYear);
            int sampled = 0;
            for (int i = 0; i < pop.Sim.People.Count; i++)
            {
                if (Sampled(i)) sampled++;
            }

            pendingSample = new LineMeshBuilder(sampled * sheaves.Count / 3 * (MaxSpendingThreads + 2) * ThreadPoints);

            // the sample's faintness is the material's (SampleAlpha): the vertices keep the dollars' alpha at full 8-bit
            // precision (at ~0.03 per vertex most threads truncated to 0-6 of 255, the smallest to nothing)
            Look look = new Look(1f, 1f, SampleIntensity, -1, false);
            List<SmvPerson> people = pop.Sim.People;
            foreach (int year in sheaves)
            {
                for (int i = 0; i < people.Count; i++)
                {
                    if (!Sampled(i) || !lives.TryGet(i, year, out PersonYear r) || !r.Adult) continue;

                    // within its half (the jitter must not carry income across the gap into the spending half)
                    double across = Math.Max(0, Math.Min(SheafHalfYears,
                        SheafHalfYears * r.IncomeRank + SheafJitterYears * (2 * Hash01(i, 11) - 1)));
                    double middle = year + 0.5, half = 0.5 * SheafGapYears;
                    AddPerson(pendingSample, people[i], year, middle - half - SheafHalfYears + across, middle + half + across, r,
                        look);
                }
            }

            sampledThreads = pendingSample.PolylineCount;
            BuildProbes();
            Findings f = Register(ctx, sheaves);
            Debug.Log($"[Why] MoneyThreadsLayer.Prepare {sw.ElapsedMilliseconds} ms: {sampledThreads} threads of " +
                      $"{sampled} sampled lines in {sheaves.Count} sheaves ({pendingSample.VertexCount} vertices); " +
                      $"{f.Year}: in control {Percent(f.Share)} at y {f.OwnersPoint.y.ToString("0.000", Ci)} rho " +
                      $"{f.OwnersPoint.z.ToString("0.000", Ci)}, fantasy by fifth {Percent(f.FantasyByQuintile[0])} .. " +
                      $"{Percent(f.FantasyByQuintile[4])}");
        }

        /// <summary>
        /// Calendar years of the sheaves: every few years from 1950, and the last full year if it is far enough on.
        /// </summary>
        List<int> SheafYears(double now)
        {
            List<int> years = new List<int>();
            int last = Math.Min(lives.LastYear, (int)Math.Floor(now - 1.5));
            int y = FirstSheafYear;
            for (; y <= last; y += SheafStep) years.Add(y);
            if (years.Count > 0 && last - years[years.Count - 1] >= SheafStep / 2) years.Add(last);
            return years;
        }

        /// <summary>
        /// One adult line in <see cref="SampleStride"/>, picked by a hash of the line (spread over every cohort).
        /// </summary>
        static bool Sampled(int person) => Hash(person, 0x5A17) % SampleStride == 0;

        /// <summary>
        /// What every year's threads need: each spending category's receiving industry and motive, the federal band (benefits),
        /// the median spending of adults per year (the dollar scale), and where each wealth group's capital income comes from.
        /// </summary>
        void PrepareInputs()
        {
            IReadOnlyList<Industry> inds = data.Industries;
            int n = inds.Count;

            // the industry that receives most of each spending category (saving is not spending: no thread)
            int spendCategories = Math.Min(6, data.Categories.Count);
            categoryIndustry = new int[spendCategories];
            categoryFear = new float[spendCategories];
            categoryEmphasis = new float[spendCategories];
            for (int c = 0; c < spendCategories; c++)
            {
                Category cat = data.Categories[c];
                int best = -1;
                double bestShare = 0;
                for (int i = 0; i < n; i++)
                {
                    double s = cat.IndustryShare(inds[i].Id);
                    if (s <= bestShare) continue;
                    bestShare = s;
                    best = i;
                }

                categoryIndustry[c] = best;
                categoryFear[c] = (float)cat.FearShare;
                float strength = Mathf.Pow(Mathf.Abs(2 * categoryFear[c] - 1), MotiveExponent);
                categoryEmphasis[c] = Mathf.Lerp(MixedMotiveAlpha, 1f, strength);
            }

            federal = data.IndustryById("federal")?.Index ?? -1;

            // the median adult's spending, every year (the scale threads are measured against)
            int years = lives.LastYear - firstYear + 1;
            medianSpending = new float[years];
            List<float> values = new List<float>(pop.Sim.People.Count);
            for (int k = 0; k < years; k++)
            {
                values.Clear();
                for (int i = 0; i < pop.Sim.People.Count; i++)
                {
                    if (lives.TryGet(i, firstYear + k, out PersonYear r) && r.Adult && r.Spending > 0) values.Add(r.Spending);
                }

                values.Sort();
                medianSpending[k] = values.Count > 0 ? values[values.Count / 2] : 1f;
            }

            capitalCdf = new float[years][];
            for (int k = 0; k < years; k++) capitalCdf[k] = CapitalSources(firstYear + k);
        }

        /// <summary>
        /// Where capital income comes from, per wealth group (bottom 50%, 50-90%, 90-99%, top 1%), as cumulative weights over
        /// the industries (group-major): dividends from every private industry by what its owners keep, rent from real
        /// estate, interest from banking; each group's mix is its share of the nation's dividends, rent and interest
        /// (circuit.json groups, the Fed's distributional accounts) times their sizes in the national accounts.
        /// </summary>
        float[] CapitalSources(int year)
        {
            IReadOnlyList<Industry> inds = data.Industries;
            int n = inds.Count;
            int realEstate = data.IndustryById("real_estate")?.Index ?? -1;
            int banking = data.IndustryById("banking")?.Index ?? -1;
            double[] owners = new double[n];
            double equityTotal = 0;
            for (int i = 0; i < n; i++)
            {
                if (inds[i].TierId == "gov" || i == realEstate) continue;
                owners[i] = Math.Max(0, inds[i].ValueAdded.GrowthAt(year)) * inds[i].OwnersShare;
                equityTotal += owners[i];
            }

            double dividends = data.Circuit.IncomeOf("dividends"), rent = data.Circuit.IncomeOf("rental");
            double interest = data.Circuit.IncomeOf("netInterest");
            float[] cdf = new float[4 * n];
            for (int g = 0; g < 4; g++)
            {
                Group group = g < data.Groups.Count ? data.Groups[g] : null;
                double eq = (group?.EquityShare ?? 1) * dividends;
                double re = realEstate >= 0 ? (group?.RealEstateShare ?? 0) * rent : 0;
                double it = banking >= 0 ? (group?.InterestShare ?? 0) * interest : 0;
                double sum = eq + re + it;
                if (sum <= 0 || equityTotal <= 0)
                {
                    eq = 1;
                    re = it = 0;
                    sum = 1;
                }

                double acc = 0;
                for (int i = 0; i < n; i++)
                {
                    double w = equityTotal > 0 ? eq / sum * owners[i] / equityTotal : 0;
                    if (i == realEstate) w += re / sum;
                    if (i == banking) w += it / sum;
                    acc += w;
                    cdf[g * n + i] = (float)acc;
                }
            }

            return cdf;
        }

        /// <summary>
        /// The industry a person's capital income comes from in a year (a deterministic draw by the person and year).
        /// </summary>
        int CapitalSource(int person, int year, int group)
        {
            int k = Mathf.Clamp(year - firstYear, 0, capitalCdf.Length - 1);
            float[] cdf = capitalCdf[k];
            int n = data.Industries.Count;
            int o = Mathf.Clamp(group, 0, 3) * n;
            float total = cdf[o + n - 1];
            if (total <= 0) return -1;

            // strictly above: an industry with no weight (a step of 0 in the cumulative weights) is never drawn
            float x = Hash01(person, year * 31 + 7) * total;
            for (int i = 0; i < n; i++)
            {
                if (cdf[o + i] > x) return i;
            }

            return n - 1;
        }

        float Median(int year) => medianSpending.Length == 0
            ? 1f
            : Mathf.Max(1f, medianSpending[Mathf.Clamp(year - firstYear, 0, medianSpending.Length - 1)]);

        // ================================================================ threads

        /// <summary>
        /// One person's threads of one year (their state is the year's): earned income, capital income and benefits rising to
        /// their line at <paramref name="tIncome"/>, the largest spending categories falling from it at
        /// <paramref name="tSpending"/>. A time when the person is not alive (or off the wall) falls back to the year's
        /// middle.
        /// </summary>
        void AddPerson(LineMeshBuilder lines, SmvPerson person, int year, double tIncome, double tSpending, in PersonYear r,
            Look look)
        {
            int i = person.Index;
            float median = Median(year), least = MinOfMedian * median;
            if (OnLine(person, year, ref tIncome, out Vector3 at)) AddIncome(lines, i, year, tIncome, at, r, look, median, least);
            if (OnLine(person, year, ref tSpending, out at)) AddSpending(lines, i, tSpending, at, r, look, median, least);
        }

        /// <summary>
        /// The person's point at a time on the wall's stretch, else at the year's middle; false when neither exists.
        /// </summary>
        bool OnLine(SmvPerson person, int year, ref double t, out Vector3 at)
        {
            if (t >= wall.FirstYear && t <= wall.LastYear && PointAt(person, t, out at)) return true;
            t = year + 0.5;
            at = default;
            return t >= wall.FirstYear && t <= wall.LastYear && PointAt(person, t, out at);
        }

        /// <summary>
        /// The person's income, by source: earned (wages from the wages part of their own industry, a business's income from
        /// its owners' part), capital income (from what owners keep, where the person's wealth group holds its capital) and
        /// benefits (Social Security, Medicare and support, from the federal government). The sample draws only the largest
        /// source, the inspected person every one.
        /// </summary>
        void AddIncome(LineMeshBuilder lines, int i, int year, double t, Vector3 at, in PersonYear r, Look look, float median,
            float least)
        {
            bool earns = r.Employed && r.Industry >= 0 && r.Industry < wall.IndustryCount;
            float earned = earns ? r.Earned : 0, capital = r.CapitalIncome, benefits = federal >= 0 ? r.Transfers : 0;
            float largest = Mathf.Max(earned, Mathf.Max(capital, benefits));
            if (earned >= least && (look.AllIncome || earned >= largest))
            {
                WallBand b = wall.BandAt(r.Industry, t);
                if (r.SelfEmployed)
                {
                    Thread(lines, at, Attach(b.OwnersLo, b.Hi, i, 1), b.Rho, true, EconomyStyle.Capital, earned, median, look,
                        KindCapital, i, 1);
                }
                else
                {
                    Thread(lines, at, Attach(b.WagesLo, b.UpkeepLo, i, 1), b.Rho, true, WagesColor, earned, median, look,
                        KindWages, i, 1);
                }

                largest = float.MaxValue; // drawn: no other source is "the largest" any more
            }

            int source = capital >= least ? CapitalSource(i, year, r.WealthGroup) : -1;
            if (source >= 0 && (look.AllIncome || capital >= largest))
            {
                WallBand b = wall.BandAt(source, t);
                Thread(lines, at, Attach(b.OwnersLo, b.Hi, i, 2), b.Rho, true, EconomyStyle.Capital, capital, median, look,
                    KindCapital, i, 2);
                largest = float.MaxValue;
            }

            if (federal >= 0 && benefits >= least && (look.AllIncome || benefits >= largest))
            {
                WallBand b = wall.BandAt(federal, t);
                Thread(lines, at, Attach(b.WagesLo, b.Hi, i, 3), b.Rho, true, EconomyStyle.Government, benefits, median, look,
                    KindTransfers, i, 3);
            }
        }

        /// <summary>
        /// The person's largest spending categories, each falling to the industry that receives most of it.
        /// </summary>
        void AddSpending(LineMeshBuilder lines, int i, double t, Vector3 at, in PersonYear r, Look look, float median,
            float least)
        {
            if (r.Spending < least) return;
            int drawn = 0, taken = 0;
            while (drawn < MaxSpendingThreads)
            {
                int best = -1;
                float bestShare = MinCategoryShare;
                for (int c = 0; c < categoryIndustry.Length; c++)
                {
                    if ((taken & (1 << c)) != 0) continue;
                    float s = r.Category(c);
                    if (s < bestShare) continue;
                    bestShare = s;
                    best = c;
                }

                if (best < 0) break;
                taken |= 1 << best;
                int target = categoryIndustry[best];
                float amount = r.Spending * bestShare;
                if (target < 0 || target >= wall.IndustryCount || amount < least) continue;
                WallBand b = wall.BandAt(target, t);
                Thread(lines, at, Attach(b.WagesLo, b.Hi, i, 4 + best), b.Rho, false, EconomyStyle.Motive(categoryFear[best]),
                    amount, median, look, KindSpending + best, i, 4 + best, categoryEmphasis[best]);
                drawn++;
            }
        }

        /// <summary>
        /// A height inside a part of a band (between lo and hi, inset from its edges), spread by person and thread.
        /// </summary>
        static float Attach(float lo, float hi, int person, int salt)
        {
            if (hi - lo < 1e-4f) return 0.5f * (lo + hi);
            float f = AttachInset + (1 - 2 * AttachInset) * Hash01(person, 101 + salt);
            return lo + (hi - lo) * f;
        }

        /// <summary>
        /// One thread between a wall point (height <paramref name="wallY"/> on the wall's plane <paramref name="wallRho"/>) and
        /// a person's point, in the plane of the person's time (u): rising (income: the path starts at the wall, peeling off
        /// its front) or falling (spending: starts at the person, returning to the wall's back). Width and alpha follow the
        /// dollars against the year's median.
        /// </summary>
        void Thread(LineMeshBuilder lines, Vector3 person, float wallY, float wallRho, bool rising, Color color, float amount,
            float median, Look look, int kind, int salt, int threadSalt, float emphasis = 1f)
        {
            float weight = Mathf.Clamp01(0.5f + Mathf.Log(Mathf.Max(amount, 1e-3f) / median, 2f) / LogSpan);
            float width = Mathf.Lerp(MinWidthPx, MaxWidthPx, weight) * look.Width;
            float alpha = Mathf.Lerp(MinAlpha, MaxAlpha, weight) * look.Alpha * emphasis;
            color = Saturate(color, ThreadSaturation);
            float rise = Mathf.Max(0.05f, person.y - wallY);
            float side = rising ? -LeaveRho : LeaveRho;
            Vector2 p0 = new Vector2(wallY, wallRho);
            Vector2 p1 = new Vector2(wallY + LeaveShare * rise, wallRho + side);
            Vector2 p2 = new Vector2(person.y - ArriveShare * rise, person.z);
            Vector2 p3 = new Vector2(person.y, person.z);

            scratch.Clear();
            for (int k = 0; k < ThreadPoints; k++)
            {
                float s = k / (float)(ThreadPoints - 1);
                float w = rising ? s : 1 - s; // 0 at the wall, 1 at the person
                Vector2 q = Bezier(p0, p1, p2, p3, w);
                float a = alpha * Mathf.Lerp(WallEndAlpha, 1f, Mathf.SmoothStep(0f, 1f, w / WallEndLength));
                scratch.Add(new LinePoint(new Vector3(person.x, q.x, q.y), Tint(color, a), width, 0, look.Intensity));
            }

            float id = look.Id >= 0 ? look.Id : EconomyIds.Thread + kind;
            float phase = Hash01(salt, 997 + threadSalt) / FlowFreq;
            lines.AddFlowPath(scratch, id, PhasePerUnit, phase);
        }

        static Vector2 Bezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float u = 1 - t;
            return u * u * u * p0 + 3 * u * u * t * p1 + 3 * u * t * t * p2 + t * t * t * p3;
        }

        /// <summary>
        /// A person's lifeline point at any time (data space), interpolated between the simulation's steps; false when the
        /// person is not alive then.
        /// </summary>
        bool PointAt(SmvPerson p, double t, out Vector3 point)
        {
            SmvSimulation sim = pop.Sim;
            double x = (t - sim.StartTime) / SmvSimulation.Step;
            int k = (int)Math.Floor(x);
            bool a = pop.PointAt(p, k, out Vector3 pa);
            bool b = pop.PointAt(p, k + 1, out Vector3 pb);
            point = a && b ? Vector3.LerpUnclamped(pa, pb, (float)(x - k)) : a ? pa : pb;
            return a || b;
        }

        static Color32 Tint(Color c, float alpha) => EconomyGeometry.Tint(c, alpha);

        /// <summary>A color pushed away from its grey (luma) by a factor, clamped to 0..1.</summary>
        static Color Saturate(Color c, float factor)
        {
            float l = 0.3f * c.r + 0.59f * c.g + 0.11f * c.b;
            return new Color(Mathf.Clamp01(l + (c.r - l) * factor), Mathf.Clamp01(l + (c.g - l) * factor),
                Mathf.Clamp01(l + (c.b - l) * factor));
        }

        /// <summary>A well-mixed hash of two integers (deterministic sampling and placement).</summary>
        static uint Hash(int a, int b)
        {
            uint h = (uint)a * 0x9E3779B1u ^ (uint)b * 0x85EBCA77u;
            h ^= h >> 15;
            h *= 0x2C1B3C6Du;
            h ^= h >> 12;
            h *= 0x297A2D39u;
            h ^= h >> 15;
            return h;
        }

        static float Hash01(int a, int b) => (Hash(a, b) & 0xFFFFFF) / 16777216f;

        // ================================================================ anchors and labels

        /// <summary>
        /// What the model finds about the people in control and about fantasy, in <see cref="FindingsYear"/>.
        /// </summary>
        sealed class Findings
        {
            public int Year, InControl, Rest;
            public float Share;

            /// <summary>Means of the people in control [0] and of every other adult [1].</summary>
            public readonly float[] Fantasy = new float[2], Fear = new float[2], Reason = new float[2], Age = new float[2],
                Inherited = new float[2], SelfEmployed = new float[2];

            /// <summary>Fantasy share of spending by household income quintile (spending-weighted).</summary>
            public readonly float[] FantasyByQuintile = new float[5];

            /// <summary>
            /// Where the gold lines run in the year (the mean point of the people in control, data space).
            /// </summary>
            public Vector3 OwnersPoint;
        }

        Findings Find(int year)
        {
            Findings f = new Findings { Year = year };
            List<SmvPerson> people = pop.Sim.People;
            double[] fantasyQ = new double[5], spendQ = new double[5];
            Vector3 sum = Vector3.zero;
            int points = 0;
            for (int i = 0; i < people.Count; i++)
            {
                if (!lives.TryGet(i, year, out PersonYear r) || !r.Adult) continue;
                int g = r.InControl ? 0 : 1;
                if (g == 0) f.InControl++;
                else f.Rest++;
                f.Fantasy[g] += r.Fantasy;
                f.Fear[g] += r.FearShare;
                f.Reason[g] += r.Reason;
                f.Age[g] += r.Age;
                f.Inherited[g] += lives.InheritedFromParentsBy(i, year) ? 1 : 0;
                f.SelfEmployed[g] += r.SelfEmployed ? 1 : 0;
                int q = Math.Min(4, (int)(r.IncomeRank * 5));
                fantasyQ[q] += r.Spending * r.Fantasy;
                spendQ[q] += r.Spending;
                if (r.InControl && PointAt(people[i], year + 0.5, out Vector3 p))
                {
                    sum += p;
                    points++;
                }
            }

            for (int g = 0; g < 2; g++)
            {
                float n = Math.Max(1, g == 0 ? f.InControl : f.Rest);
                f.Fantasy[g] /= n;
                f.Fear[g] /= n;
                f.Reason[g] /= n;
                f.Age[g] /= n;
                f.Inherited[g] /= n;
                f.SelfEmployed[g] /= n;
            }

            for (int q = 0; q < 5; q++) f.FantasyByQuintile[q] = spendQ[q] > 0 ? (float)(fantasyQ[q] / spendQ[q]) : 0;
            PopulationYear a = lives.Aggregate(year);
            f.Share = a?.InControlShare ?? (f.InControl + f.Rest > 0 ? f.InControl / (float)(f.InControl + f.Rest) : 0);
            int k = pop.Sim.StepAt(year + 0.5);
            f.OwnersPoint = points > 0
                ? sum / points
                : new Vector3(pop.U(year + 0.5), GraphStyle.HumansY + 0.5f * GraphStyle.SmvHeight, pop.Sim.Center[k]);
            return f;
        }

        /// <summary>The anchors and labels of the threads and of the findings (returned for the log).</summary>
        Findings Register(GraphContext ctx, List<int> sheaves)
        {
            int year = Math.Min(FindingsYear, lives.LastYear);
            Findings f = Find(year);
            PopulationYear agg = lives.Aggregate(year);

            // the threads: one income anchor and one spending anchor, between the wall's top and the people
            int incomeYear = SheafNear(sheaves, IncomeLabelYear), spendingYear = SheafNear(sheaves, SpendingLabelYear);
            int lastKind = EconomyIds.Thread + KindSpending + Math.Max(0, categoryIndustry.Length - 1);
            RegisterThreads(ctx, "threads:income", "Income", IncomeBlurb(agg, year), incomeYear,
                new IdRange(EconomyIds.Thread + KindWages, EconomyIds.Thread + KindTransfers),
                "income rises from the industries");
            RegisterThreads(ctx, "threads:spending", "Spending", SpendingBlurb(agg, year), spendingYear,
                new IdRange(EconomyIds.Thread + KindSpending, lastKind), "spending falls back into them");

            // the people in control: among the gold lines. Focusing it (a click on its label, the tour's end card) lights
            // the lifelines, among which the gold ones stand out; the capital threads' id alone would dim the gold lines
            // with everything else (the people in control are no contiguous id range)
            Vector3 o = f.OwnersPoint;
            IdRange lifelines = pop.LineIds;
            Anchors.Register(new Anchor
            {
                Key = "people:owners",
                Label = "In control of their path",
                Blurb = OwnersBlurb(f),
                Level = GraphLevel.Humans,
                YearsAgo = ctx.NowYear - (year + 0.5),
                EndYearsAgo = ctx.NowYear - (year + 0.5),
                Y = o.y,
                Rho = o.z,
                Ids = lifelines,
                Tier = 1
            });
            AddLabel(ctx, new LabelSpec
            {
                Text = "IN CONTROL  " + Percent(f.Share) + " <color=#" + Hex(GraphStyle.TextDim) + ">of adults, " +
                       year.ToString(Ci) + "</color>",
                Data = new Vector3(pop.U(year + 0.5 - OwnersLabelYears), GraphStyle.HumansY + GraphStyle.SmvHeight, o.z),
                Priority = 30,
                SizePx = 13,
                Color = EconomyStyle.Capital,
                PixelOffset = new Vector2(0, 14),
                Align = TMPro.TextAlignmentOptions.Right,
                AnchorKey = "people:owners",
                Ids = lifelines
            });

            // fantasy: the share of spending that buys a fantasy, by income
            int fantasyYear = year - FantasyLabelYears;
            int fk = pop.Sim.StepAt(fantasyYear + 0.5);
            float fantasyRho = pop.Sim.Center[fk];
            IdRange spending = new IdRange(EconomyIds.Thread + KindSpending, lastKind);
            Anchors.Register(new Anchor
            {
                Key = "people:fantasy",
                Label = "Buying fantasy",
                Blurb = FantasyBlurb(f, agg),
                Level = GraphLevel.Humans,
                YearsAgo = ctx.NowYear - (fantasyYear + 0.5),
                EndYearsAgo = ctx.NowYear - (fantasyYear + 0.5),
                Y = GraphStyle.HumansY + GraphStyle.SmvHeight,
                Rho = fantasyRho,
                Ids = spending,
                Tier = 2
            });
            AddLabel(ctx, new LabelSpec
            {
                Text = "fantasy: " + Percent(f.FantasyByQuintile[0]) + " of spending, poorest fifth; " +
                       Percent(f.FantasyByQuintile[4]) + ", richest",
                Data = new Vector3(pop.U(fantasyYear + 0.5), GraphStyle.HumansY + GraphStyle.SmvHeight, fantasyRho),
                Priority = 16,
                SizePx = 12,
                Color = GraphStyle.TextDim,
                PixelOffset = new Vector2(0, 14),
                Align = TMPro.TextAlignmentOptions.Center,
                AnchorKey = "people:fantasy",
                Ids = spending
            });
            return f;
        }

        static int SheafNear(List<int> sheaves, int year)
        {
            int best = sheaves.Count > 0 ? sheaves[0] : year;
            foreach (int s in sheaves)
            {
                if (Math.Abs(s - year) < Math.Abs(best - year)) best = s;
            }

            return best;
        }

        /// <summary>An anchor and a small label halfway between the wall's top and the people, at a sheaf.</summary>
        void RegisterThreads(GraphContext ctx, string key, string label, string blurb, int year, IdRange ids, string text)
        {
            double t = year + 0.5;
            float y = 0.5f * (wall.TopAt(t) + GraphStyle.HumansY);
            float rho = wall.RhoAt(t);
            Anchors.Register(new Anchor
            {
                Key = key,
                Label = label,
                Blurb = blurb,
                Level = GraphLevel.Humans,
                YearsAgo = ctx.NowYear - t,
                EndYearsAgo = ctx.NowYear - t,
                Y = y,
                Rho = rho,
                Ids = ids,
                Tier = 2
            });
            AddLabel(ctx, new LabelSpec
            {
                Text = text,
                Data = new Vector3(pop.U(t), y, rho),
                Priority = 14,
                SizePx = 12,
                Color = GraphStyle.TextDim,
                PixelOffset = new Vector2(10, 0),
                AnchorKey = key,
                Ids = ids
            });
        }

        string IncomeBlurb(PopulationYear a, int year)
        {
            string shares = "";
            if (a != null)
            {
                double capital = a.CapitalIncome + a.Business, total = a.Wages + capital + a.Transfers;
                if (total > 0)
                {
                    shares = $" In {year.ToString(Ci)} wages were {Percent(a.Wages / total)} of the people's income, " +
                             $"capital and business income {Percent(capital / total)}, benefits {Percent(a.Transfers / total)}.";
                }
            }

            return "Money rising from the industries to the people, drawn for one adult line in " + Words(SampleStride) +
                   " every " + Words(SheafStep) + " years, from " +
                   "each person's largest source of income: wages (blue) from the industry they work in, business and capital " +
                   "income (gold) from what owners keep (their own business; dividends from every industry's profits, rent " +
                   "from real estate, interest from banks, as their wealth group holds them), benefits (steel) from the " +
                   "federal government. Width and brightness follow the dollars. Each year's income rises on the past side " +
                   "of its sheaf and its spending falls on the present side, people in order of income, the richest last." +
                   shares;
        }

        string SpendingBlurb(PopulationYear a, int year)
        {
            System.Text.StringBuilder s = new System.Text.StringBuilder();
            s.Append("Money falling from each person back into the industries: a thread for each of their largest spending " +
                     "categories (up to three), to the industry that receives most of that category's money (");
            bool first = true;
            for (int c = 0; c < categoryIndustry.Length; c++)
            {
                if (categoryIndustry[c] < 0) continue;
                if (!first) s.Append(", ");
                first = false;
                s.Append((data.Categories[c].Name ?? data.Categories[c].Id).ToLowerInvariant()).Append(" to ")
                    .Append(data.Industries[categoryIndustry[c]].Name);
            }

            s.Append("). Its color is the motive: rose for desire, ice for fear, white for both.");
            if (a != null)
            {
                s.Append(" In ").Append(year.ToString(Ci)).Append(" fear moves ").Append(Percent(a.MeanFear))
                    .Append(" of household spending, desire the rest.");
            }

            return s.ToString();
        }

        static string OwnersBlurb(Findings f)
        {
            bool sameFantasy = Mathf.Abs(f.Fantasy[0] - f.Fantasy[1]) < SameShare;
            bool sameFear = Mathf.Abs(f.Fear[0] - f.Fear[1]) < SameShare;
            string buys = sameFantasy && sameFear
                ? "What sets them apart is not what they buy: "
                : "They buy differently: ";
            return $"{Percent(f.Share)} of adults in {f.Year.ToString(Ci)} steer their own path in this model (the share " +
                   "calibrated to the evidence): they own a business or hold years of spending in savings, save, carry little " +
                   "debt and decide by reason. Their lines are gold. " + buys +
                   $"fantasy takes {Percent(f.Fantasy[0])} of their spending ({Percent(f.Fantasy[1])} of everyone else's) " +
                   $"and fear moves {Percent(f.Fear[0])} of it ({Percent(f.Fear[1])}). " +
                   (sameFantasy && sameFear ? "What does" : "What else sets them apart") +
                   $": reason ({F2(f.Reason[0])} against " +
                   $"{F2(f.Reason[1])}), age ({f.Age[0].ToString("0", Ci)} against {f.Age[1].ToString("0", Ci)}), " +
                   $"an inheritance ({Percent(f.Inherited[0])} received one, against {Percent(f.Inherited[1])}) and a " +
                   $"business of their own ({Percent(f.SelfEmployed[0])} self-employed, against {Percent(f.SelfEmployed[1])}).";
        }

        static string FantasyBlurb(Findings f, PopulationYear a)
        {
            float rise = f.FantasyByQuintile[4] - f.FantasyByQuintile[0];
            string trend = rise >= SameShare ? "rises with income"
                : rise <= -SameShare ? "falls with income"
                : "barely moves with income";
            System.Text.StringBuilder s = new System.Text.StringBuilder();
            s.Append("The share of spending that buys a fantasy (an escape, a status signal, a lottery ticket: each category's " +
                     "fantasy content) ").Append(trend).Append(": ").Append(Percent(f.FantasyByQuintile[0]))
                .Append(" for the poorest fifth of households, ").Append(Percent(f.FantasyByQuintile[4]))
                .Append(" for the richest (by fifth:");
            for (int q = 0; q < 5; q++) s.Append(' ').Append(Percent(f.FantasyByQuintile[q]));
            s.Append(").");
            if (a != null)
            {
                s.Append(" All households: ").Append(Percent(a.MeanFantasy)).Append(" in ").Append(f.Year.ToString(Ci))
                    .Append('.');
            }

            bool same = Mathf.Abs(f.Fantasy[0] - f.Fantasy[1]) < SameShare;
            s.Append(same ? " The people in control buy as much of it as anyone (" : " The people in control spend ")
                .Append(Percent(f.Fantasy[0])).Append(same ? " against " : " of their money on it, everyone else ")
                .Append(Percent(f.Fantasy[1])).Append(same ? "): fantasy is bought at every level." : ".");
            return s.ToString();
        }

        static readonly string[] NumberWords =
            { "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten" };

        /// <summary>A small count in words, as the blurbs' prose has it (digits from eleven on).</summary>
        static string Words(int n) => n >= 0 && n < NumberWords.Length ? NumberWords[n] : n.ToString(Ci);

        static string Percent(double share) => (100 * share).ToString("0", Ci) + "%";

        static string F2(float v) => v.ToString("0.00", Ci);

        static string Hex(Color c) =>
            ((int)(Mathf.Clamp01(c.r) * 255)).ToString("X2", Ci) + ((int)(Mathf.Clamp01(c.g) * 255)).ToString("X2", Ci) +
            ((int)(Mathf.Clamp01(c.b) * 255)).ToString("X2", Ci);

        void AddLabel(GraphContext ctx, LabelSpec spec)
        {
            labels.Add(spec);
            ctx.Labels.Add(spec);
        }

        /// <summary>Points along the road between the wall and the people, 1950 to now, for the level of detail.</summary>
        void BuildProbes()
        {
            double from = FirstSheafYear, to = pop.Sim.NowYear - 1;
            probeMid = new Vector3[ProbeCount];
            probeYears = new float[ProbeCount];
            float mid = 0.5f * (EconomyStyle.GroundY + GraphStyle.HumansY + GraphStyle.SmvHeight);
            for (int i = 0; i < ProbeCount; i++)
            {
                double year = from + (to - from) * i / (ProbeCount - 1);
                probeYears[i] = (float)year;
                probeMid[i] = new Vector3(pop.U(year), mid, wall.RhoAt(year));
            }
        }

        // ================================================================ upload and live (main thread)

        public override void Upload(GraphContext ctx)
        {
            if (pendingSample == null) return;
            circuitStation = EconomyStage.Get(EconomyStage.Circuit);
            sampleMat = ThreadMaterial(EconomyStyle.QueueThreads, SampleAlpha, true);
            personMat = ThreadMaterial(EconomyStyle.QueueThreads + 1, 1f, false);
            sampleRenderer = AddMesh("MoneyThreads", pendingSample.ToMesh("MoneyThreads"), sampleMat);
            sampleFilter = sampleRenderer.GetComponent<MeshFilter>();
            pendingSample = null;
            Apply(sampleMat, sampleRenderer, 0);
            ShowLabels(ctx, false);

            // EconomyLoaderLayer reset the state after Prepare: catch up with the person it holds now
            seenVersion = EconomyState.Version;
            uploaded = true;
            RebuildPerson(EconomyState.Person);
        }

        /// <summary>
        /// A line material with flow pulses; <paramref name="alpha"/> scales every thread drawn with it. The sample adds up
        /// (a sheaf's colors are the sum of its money); the inspected person's few threads are blended over what lies
        /// behind them instead, so they keep their own colors over the gold wall (added to it, they all paled toward
        /// white where they cross it).
        /// </summary>
        static Material ThreadMaterial(int queue, float alpha, bool additive)
        {
            Material m = GraphMaterials.Line(new Color(1f, 1f, 1f, alpha), 1f, queue, additive, 0, 1f);
            m.SetFloat("_FlowFreq", FlowFreq);
            m.SetFloat("_FlowSpeed", FlowSpeed);
            return m;
        }

        /// <summary>
        /// The inspected person's threads, every adult year they lived in the record, in a mesh of their own (the previous
        /// one destroyed); none without a person.
        /// </summary>
        void RebuildPerson(int person)
        {
            shownPerson = person;
            LineMeshBuilder lines = null;
            if (person >= 0 && lives != null && person < pop.Sim.People.Count)
            {
                SmvPerson p = pop.Sim.People[person];
                Look look = new Look(PersonAlpha, PersonWidth, PersonIntensity, pop.Id(p), true);
                lines = new LineMeshBuilder(128 * ThreadPoints);
                for (int year = lives.FirstYear; year <= lives.LastYear; year++)
                {
                    if (!lives.TryGet(person, year, out PersonYear r) || !r.Adult) continue;
                    AddPerson(lines, p, year, year + 0.5 - PersonOffsetYears, year + 0.5 + PersonOffsetYears, r, look);
                }

                if (lines.PolylineCount == 0) lines = null;
            }

            if (lines == null)
            {
                if (personFilter != null && personFilter.sharedMesh != null)
                {
                    Destroy(personFilter.sharedMesh);
                    personFilter.sharedMesh = null;
                }

                if (personRenderer != null) personRenderer.enabled = false;
                personAlpha = -1;
                return;
            }

            Mesh mesh = lines.ToMesh("MoneyThreadsPerson");
            if (personFilter == null)
            {
                personRenderer = AddMesh("MoneyThreadsPerson", mesh, personMat);
                personFilter = personRenderer.GetComponent<MeshFilter>();
            }
            else
            {
                Mesh old = personFilter.sharedMesh;
                personFilter.sharedMesh = mesh;
                if (old != null) Destroy(old);
            }

            personAlpha = -1;
        }

        void OnDestroy()
        {
            if (sampleFilter != null && sampleFilter.sharedMesh != null) Destroy(sampleFilter.sharedMesh);
            if (personFilter != null && personFilter.sharedMesh != null) Destroy(personFilter.sharedMesh);
            if (sampleMat != null) Destroy(sampleMat);
            if (personMat != null) Destroy(personMat);
        }

        /// <summary>
        /// Level of detail: the threads show while 1950 - now spans enough of the screen and the camera is not at the
        /// stations; their alpha follows how large the picture is (density); the sample dims while a person is inspected.
        /// </summary>
        public override void Tick(GraphContext ctx, CameraRig rig)
        {
            if (!uploaded) return;
            if (EconomyState.Version != seenVersion)
            {
                seenVersion = EconomyState.Version;
                if (EconomyState.Person != shownPerson) RebuildPerson(EconomyState.Person);
            }

            Measure(rig, GraphWarp.Current, out float eraPx, out float pxPerYear);
            bool stations = rig != null &&
                            Vector3.Dot(rig.Pose.Target - circuitStation.Origin, circuitStation.Forward) > -StationsAhead;
            float target = stations ? 0f : SmoothStep(EraFromPx, EraFullPx, eraPx);
            float dt = Time.unscaledDeltaTime;
            visible = Mathf.MoveTowards(visible, target, dt * FadeSpeed);
            float crowdTarget = pxPerYear > 0
                ? Mathf.Clamp(Mathf.Pow(pxPerYear / CrowdReferencePxPerYear, CrowdExponent), CrowdMin, 1f)
                : 1f;
            crowd = Mathf.Lerp(crowd, crowdTarget, 1f - Mathf.Exp(-6f * dt));
            bool inspecting = personRenderer != null && personFilter.sharedMesh != null;
            inspect = Mathf.MoveTowards(inspect, inspecting ? InspectDim : 1f, dt * FadeSpeed);

            float a = visible * crowd * inspect;
            if (Mathf.Abs(a - sampleAlpha) > 1e-3f)
            {
                sampleAlpha = a;
                Apply(sampleMat, sampleRenderer, a);
            }

            float pa = inspecting ? visible : 0f;
            if (Mathf.Abs(pa - personAlpha) > 1e-3f)
            {
                personAlpha = pa;
                Apply(personMat, personRenderer, pa);
            }

            if (visible > 0.3f != labelsShown) ShowLabels(ctx, visible > 0.3f);
        }

        /// <summary>
        /// On-screen size of the era: <paramref name="eraPx"/> the length of 1950 - now along the road (the parts in front of
        /// the camera that overlap the screen), <paramref name="pxPerYear"/> the road's mean pixels per year over those parts.
        /// Both 0 when the era is outside the lens window.
        /// </summary>
        void Measure(CameraRig rig, WarpState warp, out float eraPx, out float pxPerYear)
        {
            eraPx = pxPerYear = 0;
            if (probeMid.Length < 2 || rig == null || rig.Cam == null) return;
            if (GraphWarp.FocusFade(probeMid[probeMid.Length / 2].x, warp) < 0.3f) return;

            Camera cam = rig.Cam;
            Rect screen = new Rect(0, 0, Screen.width, Screen.height);
            Vector3 prev = Vector3.zero;
            float years = 0;
            for (int i = 0; i < probeMid.Length; i++)
            {
                Vector3 s = cam.WorldToScreenPoint(GraphWarp.ToWorld(probeMid[i]));
                if (i > 0 && prev.z > 0 && s.z > 0)
                {
                    Rect box = Rect.MinMaxRect(Mathf.Min(prev.x, s.x), Mathf.Min(prev.y, s.y), Mathf.Max(prev.x, s.x),
                        Mathf.Max(prev.y, s.y));
                    if (box.Overlaps(screen))
                    {
                        eraPx += Vector2.Distance(prev, s);
                        years += probeYears[i] - probeYears[i - 1];
                    }
                }

                prev = s;
            }

            pxPerYear = years > 0 ? eraPx / years : 0;
        }

        void ShowLabels(GraphContext ctx, bool show)
        {
            labelsShown = show;
            foreach (LabelSpec s in labels) s.Hidden = !show;
            if (ctx.Labels != null) ctx.Labels.MarkDirty();
        }

        static void Apply(Material m, MeshRenderer r, float alpha)
        {
            if (m == null || r == null) return;
            bool show = alpha > 0.003f;
            if (r.enabled != show) r.enabled = show;
            if (show) GraphMaterials.SetAlpha(m, alpha);
        }

        static float SmoothStep(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3 - 2 * t);
        }
    }
}
