using System;
using System.Collections.Generic;
using UnityEngine;
using Why.Economy.Data;
using Why.Economy.Model;

namespace Why.Economy.Land
{
    /// <summary>
    /// Where the sweep through time stands its bodies in each year (Docs/hills-design/SWEEP.md section 2; drawn by
    /// Layers/HillSweepLayer): the economy stratified top to bottom by how much of each stratum's sales households buy
    /// (measured from the 2024 Use table at load: services, manufacturing and infrastructure, tech, raw materials, the
    /// government at the base), each industry a body as large as its share of the year's output with its companies packed
    /// around it, rows flush to the screen's right, the owners' circles and the households in a column beside them. Every
    /// size is a share of that year's GDP drawn at 2025's scale (<see cref="Scaled"/>), so 1950 and 2025 stand at one
    /// scale of shares and need no deflator.
    /// <para>Two steps. <see cref="SolveAll"/> (a worker, at load) solves every year from <see cref="FirstYear"/> to
    /// <see cref="LastYear"/> in the landscape and the portrait arrangement: the bodies' sizes, each industry's cluster
    /// (its companies' places relative to it) and which industries share a row. <see cref="Stack"/> (the main thread, cheap)
    /// then stands one year's rows for a <see cref="Room"/>: the room its labels take, in units, which the layer solves with
    /// the camera's fit (the pixels a label takes are fixed, the units they take depend on the fitted scale). The frame is the
    /// layer's own: x to the screen's right, y up, z into the screen at the view's pose.</para>
    /// </summary>
    public sealed class SweepLayout
    {
        // ------------------------------------------------------------------ the scale

        /// <summary>An industry's radius per cube root of its value added (2025-equivalent $bn): ContestNetwork.Radius's.</summary>
        public const float KR = 2.4f;

        /// <summary>A company's radius per cube root of its year-end market value (2025-equivalent $bn): a quarter of an
        /// industry's volume per dollar (1.512 = 2.4 × ∛¼), because market value is a stock and value added a year's flow.</summary>
        public const float KC = 1.512f;

        /// <summary>The years laid out: industries.json's value added runs 1947-2026 (2026 an estimate).</summary>
        public const int FirstYear = 1947, LastYear = 2026;

        /// <summary>The year whose GDP sets the scale: a share of a year's GDP is drawn as that share of this year's.</summary>
        public const int ScaleYear = 2025;

        // ------------------------------------------------------------------ the bodies' levels

        /// <summary>The largest companies of a year drawn as full bodies (with each industry's largest and the selection).</summary>
        public const int FullBodies = 60;

        /// <summary>A company smaller than this (radius, units: $7.8bn at 2025's scale) is a dot, not a body.</summary>
        public const float SmallMinR = 3;

        /// <summary>A dot's least radius.</summary>
        public const float DotMinR = 1.6f;

        // ------------------------------------------------------------------ the packing

        /// <summary>Units kept clear between two companies, plus this share of their radii (the lumpy bodies' noise reaches
        /// about 5% past their volume radius).</summary>
        public const float Clear = 1.5f, ClearPerRadius = .06f;

        /// <summary>A company's spots around its industry: every <see cref="StepDegrees"/> up to <see cref="Steps"/> steps either
        /// side of its flank (±84°), on <see cref="Rings"/> rings <see cref="RingStep"/> of its radius apart.</summary>
        public const float StepDegrees = 12;

        public const int Steps = 7, Rings = 20;

        public const float RingStep = .8f;

        /// <summary>The first ring overlaps the industry's rim by this share of the smaller radius: the cluster is grouped by
        /// occlusion (the companies stand nearer the camera), not by a line.</summary>
        public const float RimOverlap = .25f;

        /// <summary>
        /// The free spot a company takes is the nearest by an ellipse this many times as tall as wide: a cluster grows sideways
        /// before it grows up or down, so six rows of 2025's 250 companies fit the screen's height (packed ring by ring, the
        /// tallest clusters stood 190 units and the frame fitted at 0.9 pixels a unit, not the 1.15 it reaches now). A portrait
        /// screen's rows are one a stratum, so its width binds instead: its clusters flatten less (<see cref="PortraitFlatten"/>;
        /// 2025's services row 1,075 units wide at 2.5, 956 at 1.8, fitting at 0.65 and 0.73 pixels a unit).
        /// </summary>
        public const float Flatten = 2.5f, PortraitFlatten = 1.8f;

        /// <summary>Companies stand this far nearer the camera than their industry, so their bodies hide its rim.</summary>
        public const float CompanyZ = -6;

        /// <summary>A dot stands this many units outside its cluster's bodies, plus up to <see cref="LooseSpan"/> more.</summary>
        public const float LooseMin = 2, LooseSpan = 4;

        // ------------------------------------------------------------------ the rows

        /// <summary>Units between neighbouring clusters (a cluster's slot may be wider: its name's, <see cref="Room.Widths"/>).</summary>
        public const float ClusterGap = 10;

        /// <summary>Units between two rows of a stratum, and between two strata (portrait: the heading parts them), beyond the
        /// labels' room (<see cref="Room"/>): the industries' names under the upper row, the named companies' over the lower,
        /// and between strata always a named company's room, so a gap between strata is at least 22 px and 8 units wider
        /// than any gap within one.</summary>
        public const float RowGap = 4, StratumGap = 12, PortraitStratumGap = 2;

        /// <summary>A landscape row holds at most this many industries (services' eleven stand in two rows); a portrait row
        /// this many: one row a stratum. A portrait screen's free rect (1,048 x 726 px under the HUD's blocks at 1080 x 1920)
        /// is wider than tall and its labels are 1.5 times larger, so its height binds: wrapped at 450 units (nine rows),
        /// 2025 needed 0.32 pixels a unit and the fit ran to its distance limit; one row a stratum fits at 0.73.</summary>
        public const int RowMax = 6, PortraitRowMax = 11;

        /// <summary>The column right of the stack (x +40 to +110): the owners' four circles stacked from the top row's top
        /// (each 22 √(its share of wealth) across) and the households' node at the first manufacturing row's height.</summary>
        public const float ColumnX = 75, OwnerK = 22, OwnerGap = 4, HouseholdsR = 16;

        /// <summary>Every stratum's stream departs this far left of the stack's widest row (one left edge for the five), its
        /// rings as wide as this share of an industry body of the stratum's whole value added.</summary>
        public const float StreamGap = 8, StreamK = .3f;

        /// <summary>The wealth groups the owners' circles draw, top to bottom (EconomyData.GroupIds' order is bottom up).</summary>
        public static readonly string[] OwnerGroups = { "top1", "next9", "next40", "bottom50" };

        public enum Level : byte { Full, Small, Dot }

        /// <summary>
        /// The room the labels take, in units at the fitted scale (the layer converts their pixels with the camera's fit and
        /// stacks again until the two agree): <see cref="Stack"/> stands the rows so that no label hangs into another row.
        /// </summary>
        public sealed class Room
        {
            /// <summary>Under a row whose industries are named (all of them on a landscape screen): their names.</summary>
            public float Below;

            /// <summary>Over a named company's body: its name; a second tier over the first where two names would meet, and how
            /// near two names may stand up or down before they meet.</summary>
            public float Above, Tier, Meet;

            /// <summary>Between two strata, the named companies' room kept whether or not the lower row names one (0: none).</summary>
            public float Reserve;

            /// <summary>A stratum's name: landscape, its two lines beside its stream; portrait, its heading over its first row.</summary>
            public float Name;

            /// <summary>The title's height, and the clearance between a name or the title and what stands under it.</summary>
            public float Title, Gap;

            /// <summary>The largest companies named (their names' widths in <see cref="CompanyWidths"/>).</summary>
            public int Named;

            /// <summary>Portrait: the strata's names stand over their first rows, right-aligned at the stack's right edge.</summary>
            public bool Headings;

            /// <summary>Per industry, its name's width (0: none): its cluster's slot in its row widens to it where the row has
            /// room, up to <see cref="MaxRow"/>, the largest industries first.</summary>
            public float[] Widths;

            public float MaxRow = float.PositiveInfinity;

            /// <summary>Per company (the roster's index), its name's width; the named ones only.</summary>
            public readonly Dictionary<int, float> CompanyWidths = new Dictionary<int, float>();

            /// <summary>Per industry, whether its name stands under its cluster (null: every one): a row naming none keeps no room
            /// under it.</summary>
            public bool[] Labelled;
        }

        /// <summary>A stratum: its tier id, the households' share of its sales (2024 Use table) and its industries, most
        /// household-facing first (standing at the screen's right).</summary>
        public sealed class Stratum
        {
            public string Tier;
            public double HouseholdShare;
            public int[] Industries;
        }

        /// <summary>A company drawn in a year.</summary>
        public struct Placed
        {
            public int Company;
            public Level Level;
            public Companies.Kind Kind;

            /// <summary>Drawn as measured (a measured entry, or held from one).</summary>
            public bool Solid;

            /// <summary>Its place relative to its industry's (solved once), and in the stack (each <see cref="Stack"/>).</summary>
            public Vector3 Rel, At;

            public float R;

            /// <summary>Its value as a share of the year's GDP at 2025's scale ($bn), its nominal year-end value ($bn) and the
            /// year that value stands for.</summary>
            public double X, Nominal;

            public int ValueYear;

            /// <summary>A named company's tier (1 over its body, 2 over the first tier), 0 when it is not named or has neither.</summary>
            public byte Tier;
        }

        /// <summary>A row of the stack: its stratum, its industries (right to left), how far its clusters reach above and below
        /// their base line, the room over it and under it, and its box.</summary>
        public sealed class Row
        {
            public int Stratum;
            public int[] Industries;
            public float Up, Down, Above, Below;
            public float Top, Base, Bottom, Left, Right;
        }

        /// <summary>One year laid out.</summary>
        public sealed class Frame
        {
            public int Year;
            public bool Portrait;
            public double Gdp;

            /// <summary>Per industry (industries.json's order): its value added (nominal $bn), its share of GDP, its value at
            /// 2025's scale, its radius (0: none yet), its body's place, its cluster's reach from that place and the x its name
            /// stands at (its cluster's middle).</summary>
            public double[] Nominal, Share, X;

            public float[] R;
            public Vector3[] At;
            public float[] LeftOf, RightOf, UpOf, DownOf, NameX;

            /// <summary>The companies drawn, the largest first, and each company's index in that list (-1: not drawn).</summary>
            public readonly List<Placed> Companies = new List<Placed>();

            public int[] PlacedOf;

            /// <summary>Companies that exist this year with no value: not drawn, counted in their industry's name and the title.</summary>
            public readonly List<int> NotMeasured = new List<int>();

            public int[] NotMeasuredOf;

            public Row[] Rows;

            /// <summary>Per industry, its row's index.</summary>
            public int[] RowOf;

            /// <summary>Per stratum: its stream's departure (left of the stack's widest row) and its rings' radius this year; its
            /// value added (nominal) and share of GDP; where its name stands (landscape: the bottom right of its two lines over
            /// its stream; portrait: the bottom right of its heading over its first row).</summary>
            public Vector3[] Anchor, NameAt;

            public float[] StreamR;
            public double[] StratumNominal, StratumShare;

            /// <summary>The owners' circles (top 1% down to the bottom 50%): centres, radii, shares of wealth.</summary>
            public Vector3[] Owners;

            public float[] OwnerR;
            public double[] OwnerShare;

            public Vector3 Households;

            /// <summary>The title's middle, and the top and bottom of everything with the labels' room.</summary>
            public Vector3 TitleAt;

            public float LabelTop, LabelBottom;

            /// <summary>The stack's box (every row's bodies; a short first or last stratum's padding included).</summary>
            public float Top, Bottom, Left, Right;

            /// <summary>The room it was stacked for last.</summary>
            public Room Stacked;

            /// <summary>Companies whose spots ran out (placed overlapping): none expected.</summary>
            public int Forced;
        }

        public readonly EconomyData Data;
        public readonly Companies Model;
        public readonly Stratum[] Strata;

        /// <summary>Per industry: its stratum's index and its tier id.</summary>
        public readonly int[] StratumOf;

        /// <summary>Per company: its industry's index, and its rank in the industry's packing order (by its largest share of
        /// GDP in any year, largest first): the order it is packed in, the same every year, and its flank (even left, odd
        /// right).</summary>
        public readonly int[] IndustryOf, Rank;

        /// <summary>Per company: its largest value at 2025's scale over the years ($bn).</summary>
        public readonly double[] Peak;

        /// <summary>The scale year's GDP ($bn).</summary>
        public readonly double ScaleGdp;

        readonly Frame[] landscape = new Frame[LastYear - FirstYear + 1], portrait = new Frame[LastYear - FirstYear + 1];
        readonly int[][] packingOrder;

        /// <summary>The room a frame is stacked for when it is solved, until the layer stacks it for its fit (per orientation).</summary>
        readonly Room landscapeRoom, portraitRoom;

        /// <summary>Sets the strata and the companies' packing order (any thread). Solve the years with <see cref="SolveAll"/>;
        /// each frame is stacked first for the given rooms (the layer stacks it again for its fit).</summary>
        public SweepLayout(EconomyData data, Companies model, Room landscapeRoom, Room portraitRoom)
        {
            Data = data;
            Model = model;
            this.landscapeRoom = landscapeRoom ?? new Room();
            this.portraitRoom = portraitRoom ?? new Room { Headings = true };
            ScaleGdp = data.Gdp.At(ScaleYear);
            Strata = MeasureStrata(data);
            StratumOf = new int[data.Industries.Count];
            for (int s = 0; s < Strata.Length; s++)
                foreach (int i in Strata[s].Industries)
                    StratumOf[i] = s;
            int n = model.All.Count;
            IndustryOf = new int[n];
            Rank = new int[n];
            Peak = new double[n];
            var perIndustry = new List<int>[data.Industries.Count];
            for (int i = 0; i < perIndustry.Length; i++) perIndustry[i] = new List<int>();
            for (int c = 0; c < n; c++)
            {
                var co = model.All[c];
                IndustryOf[c] = data.IndustryById(co.Industry)?.Index ?? -1;
                double peak = 0;
                for (int y = FirstYear; y <= LastYear; y++)
                    if (model.ValueAt(co, y, out double v, out _) != Companies.Kind.None)
                        peak = Math.Max(peak, Scaled(v, y));
                Peak[c] = peak;
                if (IndustryOf[c] >= 0) perIndustry[IndustryOf[c]].Add(c);
            }

            packingOrder = new int[perIndustry.Length][];
            for (int i = 0; i < perIndustry.Length; i++)
            {
                var list = perIndustry[i];
                list.Sort((a, b) =>
                {
                    int byPeak = Peak[b].CompareTo(Peak[a]);
                    return byPeak != 0 ? byPeak : string.CompareOrdinal(model.All[a].Id, model.All[b].Id);
                });
                for (int k = 0; k < list.Count; k++) Rank[list[k]] = k;
                packingOrder[i] = list.ToArray();
            }
        }

        /// <summary>A value of a year (nominal $bn) as the same share of 2025's GDP ($bn): the one scale of every size.</summary>
        public double Scaled(double nominal, int year)
        {
            double gdp = Data.Gdp.At(year);
            return gdp > 0 ? nominal / gdp * ScaleGdp : 0;
        }

        /// <summary>An industry body's radius for a value at 2025's scale (0 for none).</summary>
        public static float IndustryRadius(double x) => x > 0 ? KR * (float)Math.Pow(x, 1.0 / 3) : 0;

        /// <summary>A company body's radius for a value at 2025's scale.</summary>
        public static float CompanyRadius(double x) => x > 0 ? KC * (float)Math.Pow(x, 1.0 / 3) : 0;

        /// <summary>Solves every year in both arrangements (a worker: a few hundred milliseconds at most).</summary>
        public void SolveAll()
        {
            for (int y = FirstYear; y <= LastYear; y++)
            {
                landscape[y - FirstYear] = Solve(y, false);
                portrait[y - FirstYear] = Solve(y, true);
            }
        }

        /// <summary>A year's frame (clamped to the years laid out), stacked for the room it was stacked for last.</summary>
        public Frame Get(int year, bool portraitScreen)
        {
            int k = Mathf.Clamp(year, FirstYear, LastYear) - FirstYear;
            var frames = portraitScreen ? portrait : landscape;
            return frames[k] ?? (frames[k] = Solve(k + FirstYear, portraitScreen));
        }

        // ------------------------------------------------------------------ the strata

        /// <summary>
        /// The strata top to bottom: each market tier by the households' share of its sales (personal consumption over
        /// intermediate sales to the 25 industries plus every final use, circuit.json's 2024 Use table), the government last
        /// as the base; each stratum's industries most household-facing first. Measured, never written in: in 2024 services
        /// .502, manufacturing and infrastructure .331, tech .223, raw materials .218, government .027.
        /// </summary>
        static Stratum[] MeasureStrata(EconomyData data)
        {
            var inter = new Dictionary<string, double>(StringComparer.Ordinal);
            if (data.Circuit?.Io != null)
                foreach (var (from, _, value) in data.Circuit.Io.Flows())
                    inter[from] = (inter.TryGetValue(from, out double s) ? s : 0) + value;
            var share = new double[data.Industries.Count];
            var tierPce = new Dictionary<string, double>(StringComparer.Ordinal);
            var tierAll = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var ind in data.Industries)
            {
                double pce = 0, all = inter.TryGetValue(ind.Id, out double i) ? i : 0;
                if (data.Circuit?.FinalDemand != null && data.Circuit.FinalDemand.TryGetValue(ind.Id, out var fd) && fd != null)
                {
                    pce = Get(fd, "pce");
                    all += pce + Get(fd, "investment") + Get(fd, "government") + Get(fd, "exports");
                }

                share[ind.Index] = all > 0 ? pce / all : 0;
                tierPce[ind.TierId] = (tierPce.TryGetValue(ind.TierId, out double p) ? p : 0) + pce;
                tierAll[ind.TierId] = (tierAll.TryGetValue(ind.TierId, out double a) ? a : 0) + all;
            }

            var tiers = new List<string>();
            foreach (var t in data.Tiers)
                if (t.Id != "gov")
                    tiers.Add(t.Id);
            double TierShare(string t) => tierAll.TryGetValue(t, out double a) && a > 0 ? tierPce[t] / a : 0;
            tiers.Sort((a, b) => TierShare(b).CompareTo(TierShare(a)));
            tiers.Add("gov");
            var strata = new Stratum[tiers.Count];
            for (int s = 0; s < tiers.Count; s++)
            {
                var members = new List<int>();
                foreach (var ind in data.Industries)
                    if (ind.TierId == tiers[s])
                        members.Add(ind.Index);
                members.Sort((a, b) =>
                {
                    int byShare = share[b].CompareTo(share[a]);
                    return byShare != 0 ? byShare : a.CompareTo(b);
                });
                strata[s] = new Stratum { Tier = tiers[s], HouseholdShare = TierShare(tiers[s]), Industries = members.ToArray() };
            }

            return strata;
        }

        static double Get(Dictionary<string, double> d, string key) => d.TryGetValue(key, out double v) ? v : 0;

        // ------------------------------------------------------------------ one year

        /// <summary>Solves one year: the bodies' levels, each industry's cluster, the rows' arrangement and the strata's
        /// values; then stacks it for the orientation's first room.</summary>
        Frame Solve(int year, bool portraitScreen)
        {
            int ni = Data.Industries.Count, nc = Model.All.Count;
            var f = new Frame
            {
                Year = year, Portrait = portraitScreen, Gdp = Data.Gdp.At(year), Nominal = new double[ni], Share = new double[ni], X = new double[ni],
                R = new float[ni], At = new Vector3[ni], LeftOf = new float[ni], RightOf = new float[ni], UpOf = new float[ni], DownOf = new float[ni],
                NameX = new float[ni], PlacedOf = new int[nc], NotMeasuredOf = new int[ni], RowOf = new int[ni],
            };
            for (int i = 0; i < ni; i++)
            {
                double va = Data.Industries[i].ValueAdded.At(year);
                f.Nominal[i] = va;
                f.Share[i] = f.Gdp > 0 ? va / f.Gdp : 0;
                f.X[i] = Scaled(va, year);
                f.R[i] = IndustryRadius(f.X[i]);
            }

            // the companies drawn this year, the largest first, and those that exist without a value (counted, not drawn)
            for (int c = 0; c < nc; c++)
            {
                f.PlacedOf[c] = -1;
                if (IndustryOf[c] < 0) continue;
                var co = Model.All[c];
                var kind = Model.ValueAt(co, year, out double nominal, out int valueYear);
                if (kind == Companies.Kind.None)
                {
                    if (Model.Exists(co, year) && (co.Ended == null || year < co.Ended.Value))
                    {
                        f.NotMeasured.Add(c);
                        f.NotMeasuredOf[IndustryOf[c]]++;
                    }

                    continue;
                }

                double x = Scaled(nominal, year);
                f.Companies.Add(new Placed
                {
                    Company = c, Kind = kind, Solid = Companies.Solid(co, kind, valueYear), X = x, R = CompanyRadius(x), Nominal = nominal, ValueYear = valueYear,
                });
            }

            f.Companies.Sort((a, b) =>
            {
                int byX = b.X.CompareTo(a.X);
                return byX != 0 ? byX : a.Company.CompareTo(b.Company);
            });
            var largest = new bool[ni];
            for (int k = 0; k < f.Companies.Count; k++)
            {
                var p = f.Companies[k];
                int ind = IndustryOf[p.Company];
                p.Level = k < FullBodies || !largest[ind] ? Level.Full : p.R >= SmallMinR ? Level.Small : Level.Dot;
                largest[ind] = true;
                f.Companies[k] = p;
                f.PlacedOf[p.Company] = k;
            }

            for (int i = 0; i < ni; i++) Cluster(f, i, portraitScreen ? PortraitFlatten : Flatten);
            Arrange(f, portraitScreen);
            StrataValues(f);
            Stack(f, portraitScreen ? portraitRoom : landscapeRoom);
            return f;
        }

        // the candidate spots of one company, reused (Solve runs on one worker at a time per layout)
        readonly List<(float cost, int order, float x, float y)> spots = new List<(float, int, float, float)>(Rings * (2 * Steps + 1));
        readonly List<(float x, float y, float r)> placed = new List<(float, float, float)>(64);

        /// <summary>
        /// One industry's cluster in its own frame (the body at the origin): its full and small company bodies packed in the
        /// industry's fixed order, each on its flank (even ranks left, odd right) at the free spot nearest by the flattened
        /// ellipse among <see cref="Rings"/> rings of ±84°, the first overlapping the industry's rim by
        /// <see cref="RimOverlap"/> of the smaller radius, <see cref="Clear"/> units kept between companies; then its dots
        /// scattered just outside the bodies' box at angles hashed from their ids. The places stay relative to the industry
        /// (<see cref="Placed.Rel"/>); <see cref="Stack"/> moves them into the stack.
        /// </summary>
        void Cluster(Frame f, int industry, float flatten)
        {
            float big = f.R[industry];
            placed.Clear();
            float minX = -big, maxX = big, minY = -big, maxY = big;
            foreach (int c in packingOrder[industry])
            {
                int k = f.PlacedOf[c];
                if (k < 0) continue;
                var p = f.Companies[k];
                if (p.Level == Level.Dot) continue;
                float r = p.R, pref = Rank[c] % 2 == 0 ? 180 : 0, d0 = big + r - RimOverlap * Mathf.Min(big, r);
                spots.Clear();
                for (int j = 0; j < Rings; j++)
                {
                    float d = d0 + j * RingStep * r;
                    for (int s = 0; s <= 2 * Steps; s++)
                    {
                        int step = (s + 1) / 2 * (s % 2 == 1 ? 1 : -1);
                        float a = (pref + step * StepDegrees) * Mathf.Deg2Rad, x = d * Mathf.Cos(a), y = d * Mathf.Sin(a);
                        spots.Add((Mathf.Sqrt(x * x + flatten * flatten * y * y) + .001f * Mathf.Abs(step), spots.Count, x, y));
                    }
                }

                // nearest first; of two as near (a spot and its mirror across the flank), the one generated first: the sort
                // is not stable, and the layout must be the same in every run
                spots.Sort((a, b) =>
                {
                    int byCost = a.cost.CompareTo(b.cost);
                    return byCost != 0 ? byCost : a.order.CompareTo(b.order);
                });
                bool found = false;
                float px = 0, py = 0;
                foreach (var (_, _, x, y) in spots)
                {
                    if (!Free(x, y, r)) continue;
                    px = x;
                    py = y;
                    found = true;
                    break;
                }

                // spots ran out on the flank: further rings, all round
                for (int j = Rings; !found && j < Rings + 40; j++)
                {
                    float d = d0 + j * RingStep * r;
                    for (int s = 0; s < 30 && !found; s++)
                    {
                        int step = (s + 1) / 2 * (s % 2 == 1 ? 1 : -1);
                        float a = (pref + step * StepDegrees) * Mathf.Deg2Rad;
                        px = d * Mathf.Cos(a);
                        py = d * Mathf.Sin(a);
                        found = Free(px, py, r);
                    }
                }

                if (!found) f.Forced++;
                placed.Add((px, py, r));
                p.Rel = new Vector3(px, py, CompanyZ);
                f.Companies[k] = p;
                minX = Mathf.Min(minX, px - r);
                maxX = Mathf.Max(maxX, px + r);
                minY = Mathf.Min(minY, py - r);
                maxY = Mathf.Max(maxY, py + r);
            }

            // dots just outside the bodies' box, each at the box's edge in its hashed direction
            float cx = (minX + maxX) * .5f, cy = (minY + maxY) * .5f, hw = (maxX - minX) * .5f, hh = (maxY - minY) * .5f;
            float l = minX, rt = maxX, dn = minY, up = maxY;
            for (int k = 0; k < f.Companies.Count; k++)
            {
                var p = f.Companies[k];
                if (p.Level != Level.Dot || IndustryOf[p.Company] != industry) continue;
                float rr = Mathf.Max(p.R, DotMinR);
                p.Rel = Loose(Model.All[p.Company].Id, cx, cy, hw, hh, rr);
                f.Companies[k] = p;
                l = Mathf.Min(l, p.Rel.x - rr);
                rt = Mathf.Max(rt, p.Rel.x + rr);
                dn = Mathf.Min(dn, p.Rel.y - rr);
                up = Mathf.Max(up, p.Rel.y + rr);
            }

            f.LeftOf[industry] = -l;
            f.RightOf[industry] = rt;
            f.DownOf[industry] = -dn;
            f.UpOf[industry] = up;
        }

        /// <summary>Whether a company of radius r at (x, y) keeps clear of every company placed in the cluster.</summary>
        bool Free(float x, float y, float r)
        {
            foreach (var (px, py, pr) in placed)
            {
                float need = r + pr + Clear + ClearPerRadius * (r + pr), dx = x - px, dy = y - py;
                if (dx * dx + dy * dy < need * need) return false;
            }

            return true;
        }

        /// <summary>A dot's place: on the bodies' box's edge in a direction hashed from its id, then a hashed
        /// <see cref="LooseMin"/> to <see cref="LooseMin"/> + <see cref="LooseSpan"/> units and its own radius further out.</summary>
        static Vector3 Loose(string id, float cx, float cy, float hw, float hh, float r)
        {
            float a = Organism.Hash01(id) * Mathf.PI * 2, cs = Mathf.Cos(a), sn = Mathf.Sin(a);
            float t = Mathf.Min(Mathf.Abs(cs) > 1e-4f ? hw / Mathf.Abs(cs) : 1e6f, Mathf.Abs(sn) > 1e-4f ? hh / Mathf.Abs(sn) : 1e6f);
            float off = LooseMin + LooseSpan * Organism.Hash01(id + "#loose") + r;
            return new Vector3(cx + cs * (t + off), cy + sn * (t + off), CompanyZ);
        }

        /// <summary>Which industries share a row: each stratum's in rows of at most <see cref="RowMax"/> (portrait
        /// <see cref="PortraitRowMax"/>: one row a stratum), right to left, each row as tall as its tallest cluster.</summary>
        void Arrange(Frame f, bool portraitScreen)
        {
            var rows = new List<Row>();
            int cap = portraitScreen ? PortraitRowMax : RowMax;
            var row = new List<int>();
            for (int s = 0; s < Strata.Length; s++)
            {
                row.Clear();
                foreach (int i in Strata[s].Industries)
                {
                    if (row.Count >= cap)
                    {
                        rows.Add(new Row { Stratum = s, Industries = row.ToArray() });
                        row.Clear();
                    }

                    row.Add(i);
                }

                if (row.Count > 0) rows.Add(new Row { Stratum = s, Industries = row.ToArray() });
            }

            for (int k = 0; k < rows.Count; k++)
                foreach (int i in rows[k].Industries)
                {
                    rows[k].Up = Mathf.Max(rows[k].Up, f.UpOf[i]);
                    rows[k].Down = Mathf.Max(rows[k].Down, f.DownOf[i]);
                    f.RowOf[i] = k;
                }

            f.Rows = rows.ToArray();
        }

        /// <summary>Each stratum's value added (nominal), its share of GDP and its stream's ring radius this year:
        /// <see cref="StreamK"/> of a body of the stratum's value added.</summary>
        void StrataValues(Frame f)
        {
            int n = Strata.Length;
            f.StreamR = new float[n];
            f.StratumNominal = new double[n];
            f.StratumShare = new double[n];
            for (int s = 0; s < n; s++)
            {
                double va = 0;
                foreach (int i in Strata[s].Industries) va += f.Nominal[i];
                f.StratumNominal[s] = va;
                f.StratumShare[s] = f.Gdp > 0 ? va / f.Gdp : 0;
                f.StreamR[s] = StreamK * IndustryRadius(Scaled(va, f.Year));
            }
        }

        // ------------------------------------------------------------------ the stack (main thread, each fit)

        /// <summary>
        /// Stands a year's rows for a room (units at the fitted scale), cheap enough to run several times a rebuild:
        /// <list type="number">
        /// <item>across, each cluster in its slot: its box, widened on both sides to its name's width where the row has room
        /// (<see cref="Room.MaxRow"/>), the largest industries first (their names place first; a name not given its width is
        /// the label system's to cull), flush to x = 0 and running left, <see cref="ClusterGap"/> apart;</item>
        /// <item>the named companies' names (the largest <see cref="Room.Named"/>) a tier over their bodies, the second tier
        /// where two names of a row would meet, and each row's room over it for them;</item>
        /// <item>down, the rows top to bottom: under a row its industries' names (<see cref="Room.Below"/>, where it names
        /// any), then <see cref="RowGap"/> within a stratum or <see cref="StratumGap"/> and the named companies' room (at
        /// least <see cref="Room.Reserve"/>) between strata, and on a portrait screen the next stratum's heading; on a
        /// landscape screen a stratum too short for its name over its stream is padded above and below;</item>
        /// <item>the members to their places, the column, the streams' departures, the strata's names and the title.</item>
        /// </list>
        /// </summary>
        public void Stack(Frame f, Room room)
        {
            f.Stacked = room;
            var rows = f.Rows;
            var grant = new float[f.R.Length];
            for (int k = 0; k < rows.Length; k++)
            {
                var row = rows[k];
                float w0 = ClusterGap * (row.Industries.Length - 1);
                foreach (int i in row.Industries) w0 += f.LeftOf[i] + f.RightOf[i];
                float budget = room.MaxRow - w0;
                // the largest industries first: a slot is widened whole or not at all
                var byShare = (int[])row.Industries.Clone();
                Array.Sort(byShare, (a, b) =>
                {
                    int c = f.Share[b].CompareTo(f.Share[a]);
                    return c != 0 ? c : a.CompareTo(b);
                });
                foreach (int i in byShare)
                {
                    float need = room.Widths != null && i < room.Widths.Length ? Mathf.Max(0, room.Widths[i] - f.LeftOf[i] - f.RightOf[i]) : 0;
                    grant[i] = need <= budget ? need : 0;
                    budget -= grant[i];
                }

                float x = 0;
                foreach (int i in row.Industries)
                {
                    float half = (f.LeftOf[i] + f.RightOf[i] + grant[i]) * .5f, centre = x - half;
                    f.At[i] = new Vector3(centre - (f.RightOf[i] - f.LeftOf[i]) * .5f, 0, 0);
                    f.NameX[i] = centre;
                    x = centre - half - ClusterGap;
                }

                row.Left = x + ClusterGap;
                row.Right = 0;
                row.Above = 0;
                bool names = room.Labelled == null;
                foreach (int i in row.Industries) names |= room.Labelled != null && i < room.Labelled.Length && room.Labelled[i];
                row.Below = names ? room.Below : 0;
            }

            Name(f, room);
            int ns = Strata.Length;
            // each stratum's band (its rows and the gaps within) and, beside a short one on a landscape screen, the padding its
            // name and stream need (the pair centred on the band)
            var band = new float[ns];
            var pad = new float[ns];
            for (int k = 0; k < rows.Length; k++)
            {
                band[rows[k].Stratum] += rows[k].Up + rows[k].Down;
                if (k > 0 && rows[k - 1].Stratum == rows[k].Stratum) band[rows[k].Stratum] += rows[k - 1].Below + RowGap + rows[k].Above;
            }

            if (!room.Headings)
                for (int s = 0; s < ns; s++)
                    pad[s] = Mathf.Max(0, room.Name + 1 + 2 * f.StreamR[s] - band[s]) * .5f;
            float y = 0;
            for (int k = 0; k < rows.Length; k++)
            {
                var row = rows[k];
                if (k == 0) y -= pad[row.Stratum];
                else
                {
                    var prev = rows[k - 1];
                    if (prev.Stratum == row.Stratum) y -= prev.Below + RowGap + row.Above;
                    else
                    {
                        y -= prev.Below + pad[prev.Stratum] + (room.Headings ? PortraitStratumGap : StratumGap) + Mathf.Max(row.Above, room.Reserve) + pad[row.Stratum];
                        if (room.Headings) y -= room.Name + room.Gap;
                    }
                }

                row.Top = y;
                row.Base = y - row.Up;
                row.Bottom = row.Base - row.Down;
                foreach (int i in row.Industries) f.At[i].y = row.Base;
                y = row.Bottom;
            }

            f.Top = rows.Length > 0 ? rows[0].Top + pad[rows[0].Stratum] : 0;
            f.Bottom = rows.Length > 0 ? rows[rows.Length - 1].Bottom - pad[rows[rows.Length - 1].Stratum] : 0;
            f.Right = 0;
            f.Left = 0;
            foreach (var r in rows) f.Left = Mathf.Min(f.Left, r.Left);
            for (int k = 0; k < f.Companies.Count; k++)
            {
                var p = f.Companies[k];
                p.At = p.Rel + f.At[IndustryOf[p.Company]];
                f.Companies[k] = p;
            }

            Column(f);
            Streams(f, room, pad);
            // the band over the top row: its named companies' names (and a portrait screen's first heading); the title over it
            float over = rows.Length > 0 ? rows[0].Above : 0;
            if (room.Headings) over += room.Gap + room.Name;
            f.TitleAt = new Vector3((f.Left + f.Right) * .5f, f.Top + over + room.Gap + room.Title * .5f, 0);
            f.LabelTop = f.TitleAt.y + room.Title * .5f;
            f.LabelBottom = f.Bottom - (rows.Length > 0 ? rows[rows.Length - 1].Below : 0);
        }

        /// <summary>
        /// The named companies' names (the largest <see cref="Room.Named"/> drawn as bodies, in the order the label system
        /// places them): each a tier over its body unless that tier meets a name already given one in its row (across by their
        /// widths, up or down nearer than <see cref="Room.Meet"/>), then the tier over it, else none (the label system then
        /// stands it on its body's top or culls it); each row's room over it reaches its names' highest tier.
        /// </summary>
        void Name(Frame f, Room room)
        {
            var given = new List<(int row, float x, float y, float w)>(room.Named * 2);
            int named = 0;
            for (int k = 0; k < f.Companies.Count; k++)
            {
                var p = f.Companies[k];
                p.Tier = 0;
                if (named < room.Named && p.Level != Level.Dot)
                {
                    named++;
                    int i = IndustryOf[p.Company], r = f.RowOf[i];
                    float x = f.At[i].x + p.Rel.x, top = p.Rel.y + p.R, w = room.CompanyWidths.TryGetValue(p.Company, out float cw) ? cw : 0;
                    for (int t = 1; t <= 2 && p.Tier == 0; t++)
                    {
                        if (t == 2 && room.Tier <= 0) break;
                        float y = top + (t == 2 ? room.Tier : 0);
                        bool meets = false;
                        foreach (var (gr, gx, gy, gw) in given)
                            if (gr == r && Mathf.Abs(x - gx) < (w + gw) * .5f && Mathf.Abs(y - gy) < room.Meet)
                            {
                                meets = true;
                                break;
                            }

                        if (meets) continue;
                        p.Tier = (byte)t;
                        given.Add((r, x, y, w));
                    }

                    var row = f.Rows[r];
                    row.Above = Mathf.Max(row.Above, top + room.Above + (p.Tier == 2 ? room.Tier : 0) - row.Up);
                }

                f.Companies[k] = p;
            }
        }

        /// <summary>The column right of the stack: the owners' circles from the top row's top down, as wide as 22 √(each group's
        /// share of household wealth) (circuit.json groupHistory: the Fed's DFA from 1989, recalled Saez-Zucman shares 1950-1988,
        /// 1950's held before), and the households at the first manufacturing row's height.</summary>
        void Column(Frame f)
        {
            int n = OwnerGroups.Length;
            f.Owners ??= new Vector3[n];
            f.OwnerR ??= new float[n];
            f.OwnerShare ??= new double[n];
            float y = f.Top;
            for (int g = 0; g < n; g++)
            {
                int k = Array.IndexOf(EconomyData.GroupIds, OwnerGroups[g]);
                double share = k >= 0 && k < Data.GroupWealthHistory.Count ? Data.GroupWealthHistory[k].At(f.Year) : double.NaN;
                f.OwnerShare[g] = share;
                float r = double.IsNaN(share) || share <= 0 ? 1 : OwnerK * (float)Math.Sqrt(share);
                f.OwnerR[g] = r;
                f.Owners[g] = new Vector3(ColumnX, y - r, 0);
                y -= 2 * r + OwnerGap;
            }

            int make = -1;
            for (int s = 0; s < Strata.Length && make < 0; s++)
                if (Strata[s].Tier == "make")
                    make = s;
            float height = f.Rows.Length > 0 ? (f.Top + f.Bottom) * .5f : 0;
            foreach (var row in f.Rows)
            {
                if (row.Stratum != make) continue;
                height = row.Base;
                break;
            }

            f.Households = new Vector3(ColumnX, height, 0);
        }

        /// <summary>
        /// Each stratum's stream departure, <see cref="StreamGap"/> left of the stack's widest row (one left edge for the five:
        /// departing from its own widest row instead, a narrow stratum's stream set out from the middle of the screen and ran
        /// back under the wider rows above it), and where its name stands. Landscape: the name's two lines right-aligned at the
        /// departure, over the stream, the pair centred on the stratum's (padded) band. Portrait: the stream at the band's
        /// middle, the heading right-aligned at the stack's right edge over the stratum's first row and its named companies.
        /// </summary>
        void Streams(Frame f, Room room, float[] pad)
        {
            int n = Strata.Length;
            f.Anchor ??= new Vector3[n];
            f.NameAt ??= new Vector3[n];
            for (int s = 0; s < n; s++)
            {
                Row first = null, last = null;
                foreach (var row in f.Rows)
                {
                    if (row.Stratum != s) continue;
                    first ??= row;
                    last = row;
                }

                if (first == null) continue;
                float mid = (first.Top + pad[s] + last.Bottom - pad[s]) * .5f, x = f.Left - StreamGap;
                if (room.Headings)
                {
                    f.Anchor[s] = new Vector3(x, mid, 0);
                    f.NameAt[s] = new Vector3(0, first.Top + first.Above + room.Gap, 0);
                }
                else
                {
                    float y = mid - (room.Name + 1) * .5f;
                    f.Anchor[s] = new Vector3(x, y, 0);
                    f.NameAt[s] = new Vector3(x, y + f.StreamR[s] + 1, 0);
                }
            }
        }

        // ------------------------------------------------------------------ checks

        /// <summary>The worst overlap of two company bodies in a frame (units: the sum of their radii less the distance
        /// between them, plus nothing for the clearance) and the pair; dots are not bodies.</summary>
        public float WorstOverlap(Frame f, out string pair)
        {
            float worst = float.NegativeInfinity;
            pair = "";
            var list = f.Companies;
            for (int a = 0; a < list.Count; a++)
            {
                if (list[a].Level == Level.Dot) continue;
                for (int b = a + 1; b < list.Count; b++)
                {
                    if (list[b].Level == Level.Dot) continue;
                    float reach = list[a].R + list[b].R;
                    Vector2 d = (Vector2)list[a].At - (Vector2)list[b].At;
                    if (Mathf.Abs(d.x) > reach || Mathf.Abs(d.y) > reach) continue;
                    float over = reach - d.magnitude;
                    if (over <= worst) continue;
                    worst = over;
                    pair = Model.All[list[a].Company].Id + "/" + Model.All[list[b].Company].Id;
                }
            }

            return float.IsNegativeInfinity(worst) ? 0 : worst;
        }
    }
}
