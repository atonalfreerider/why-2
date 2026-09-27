using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Why.Matter
{
    /// <summary>
    /// A faint scale grid drawn on space itself beneath the RED layer: a log-radial fan of rays that all burst
    /// out of the Big Bang. Ray j (0 .. <see cref="Orders"/>) runs at rho_j = S(u) * r^(j - Orders): ray 0, just
    /// outside our lineage, is our world's scale, and every ray outward stands for a scale ten times larger, up
    /// to 10^38. The gap between neighbouring rays therefore grows by the constant factor r outward: exponentially
    /// growing grid space, with the inside curve (rho = 0) where we live.
    ///
    /// The opening S(u) (<see cref="Fan.Opening"/>) is 0 at the Big Bang and bursts out of it (the outermost rays
    /// leave the cusp almost perpendicular to the clock, the inner ones hug our track: the fan opens like a horn),
    /// then keeps opening, ever more gently, about one and a half times as wide as the red envelope, until it holds
    /// still at the start of the biosphere; from there every ray runs parallel to the clock, receding, until the
    /// grid dissolves ahead of the human branch. A ray shows only once the gap to its inner neighbour has opened
    /// beyond a few pixels (<see cref="CrowdGap"/>), so finer rays keep emerging from the expanding space along our
    /// track, and as its gap widens a ray grows thicker and fainter: the outer rays are broad and faint, the inner
    /// ones thin and crisp.
    ///
    /// Rungs (the circumferential lines) are the orthogonal trajectories of the rays: close to the Big Bang they
    /// are circles around the cusp, from the biosphere on straight lines across the parallel rays. They leave our
    /// track at equal steps of ln(arc since the Big Bang), so they grow larger and farther apart with the distance
    /// from the cusp (a steady beat once the fan has settled). Each is a chain of short, fine dashes, one per cell,
    /// open around the rays it crosses (the sketch's cross-strokes, not a lattice); they too show only where the
    /// cells are resolvable, fading toward the fan's outer edge.
    ///
    /// Scaffolding, not content: neutral grey (hue belongs to the levels, and a red lattice would read as more
    /// matter contours), low alpha, far below the bloom threshold, drawn beneath the matter surfaces so the red
    /// veils it, and dimmed where the fan reaches beyond the red envelope into open space, where nothing veils it.
    /// Every position and opacity is a smooth function of the clock, so nothing pops. The grid is schematic (the
    /// clock is not physical time, and space never stopped expanding); the anchor's blurb tells the real
    /// expansion. It builds its own <see cref="MatterLayout"/> (the Big Bang's arc and the envelope) because it
    /// prepares concurrently with <see cref="MatterLayer"/>.
    /// </summary>
    public sealed class ExpansionGridLayer : GraphLayer
    {
        /// <summary>Anchor of the grid: tooltips of its labels and the director.</summary>
        public const string AnchorKey = "matter:_expansion";

        /// <summary>
        /// Highlight id of the grid: the last matter id (<see cref="GraphIds.Matter"/> 998), above the bands and
        /// the epochs, which <see cref="MatterLayer"/> numbers upward from index 100 (a few dozen of them).
        /// </summary>
        public static readonly int GridId = GraphIds.Matter(998);

        // --- the fan: S(s) = Scale * F(s / sb), s = arc length along the clock from the Big Bang and sb its value at the
        // biosphere, F(x) = Burst * (1 - (1 - x)^3) + (1 - Burst) * (6x^5 - 15x^4 + 10x^3) ---
        /// <summary>Years ago at which the fan has opened fully and its rays run parallel: the start of the biosphere.</summary>
        public const double BiosphereYearsAgo = 4.0e9;

        /// <summary>Orders of magnitude from our world's scale (ray 0) to the outermost ray.</summary>
        public const int Orders = 38;

        /// <summary>The fan's opening from the biosphere on, relative to the red envelope there (~20 data units, so ~26).</summary>
        const float EnvelopeReach = 1.3f;

        /// <summary>
        /// Radius of ray 0, our world's scale, once the fan has opened (data units): just outside our lineage. With
        /// the opening it sets the ratio r between neighbouring rays (~1.17), and so which rays open wide enough to
        /// show: from ray 7 or so (~0.2 units out) on, so the labelled ray 10 shows too.
        /// </summary>
        public const float InnerScale = 0.07f;

        /// <summary>
        /// Share of the burst in the opening. The burst, an ease-out, gives the fan its slope at the Big Bang,
        /// 3 * Burst * Scale / sb (~9: the outermost ray leaves the cusp ~84 degrees from the clock); the rest, a
        /// smootherstep, keeps the fan opening visibly all the way to the biosphere (a pure ease-out has all but
        /// settled two hours of the clock earlier, after reaching far out into the dark around 7 o'clock) and 1.3 to
        /// 1.8 times as wide as the red envelope on the way. Both parts' slope and curvature ease to 0 at the
        /// biosphere, so the rays bend smoothly into parallel.
        /// </summary>
        const double Burst = 0.5;

        // --- crowding: a line shows where the gap to its neighbour (world units, perpendicular to it) has opened beyond
        // CrowdGap, fully from EmergeRange times that; beyond it, it widens and fades with the gap ---
        /// <summary>
        /// Gap below which a line is hidden (world units): ~2 px in the overview, so sub-pixel lines never pile up
        /// into a solid block at the cusp or along our track.
        /// </summary>
        const float CrowdGap = 0.03f;

        /// <summary>A line fades in while its gap grows from <see cref="CrowdGap"/> to this many times that.</summary>
        const float EmergeRange = 2.5f;

        /// <summary>Opacity falls as (gap / CrowdGap)^-Fainter: a ray 100 times wider apart than the finest is ~5 times fainter.</summary>
        const float Fainter = 0.35f;

        /// <summary>World width of a ray per unit of its gap: the outermost rays are ~0.1 units (a few pixels) wide.</summary>
        const float RayWidthPerGap = 0.025f;

        /// <summary>
        /// World width of a rung per unit of the smaller side of its cells (the gap to the neighbouring rung or between
        /// the rays it crosses): the strokes stay fine along our track, where the rays crowd, and broaden far out.
        /// </summary>
        const float RungWidthPerGap = 0.015f;

        /// <summary>World widths are capped here (world units), so the far rungs stay lines, not bars.</summary>
        const float MaxWidthWorld = 0.12f;

        const float WidthPx = 1f;

        /// <summary>The outermost rays fade out over this many orders (the fan has no hard outer border).</summary>
        const float OuterOrders = 4f;

        // --- rungs: coordinate w = ln(s / sb) up to the biosphere, continued linearly (s - sb) / sb after it ---
        /// <summary>
        /// Rung step in w: each rung leaves our track 1.2 times farther from the Big Bang than the one before, and
        /// once the fan has settled they follow at a steady 0.18 sb (~0.8 world units along the track).
        /// </summary>
        const double RungStep = 0.18232155679395462; // ln 1.2

        /// <summary>Rungs leaving our track closer than this to the Big Bang (world units) are too crowded to be seen anywhere.</summary>
        const float FirstRungSigma = 0.05f;

        /// <summary>Rungs start this far (in ln rho) inside ray 0, where every ray is still crowded ...</summary>
        const float RungInnerMargin = 1f;

        /// <summary>... with this many samples up to ray 0 (then <see cref="CellSamples"/> per cell between neighbouring rays).</summary>
        const int RungInnerSamples = 4;

        /// <summary>
        /// Fraction of a cell left open at either end of a rung's dash, where it crosses a ray: the rungs read as the
        /// sketch's short cross-strokes between neighbouring rays rather than as a lattice of long spokes and rings.
        /// </summary>
        const float DashGap = 0.17f;

        /// <summary>Fraction of a cell over which a dash fades in (and out) at its ends: short, so the strokes read as separate.</summary>
        const float DashRamp = 0.07f;

        /// <summary>Samples of a rung per cell: the ray, the dash's start, its full-strength start, middle and end, and its end.</summary>
        const int CellSamples = 6;

        // --- opacity: the material carries the grid's opacity, vertices a 0..1 profile (full byte precision) ---
        /// <summary>Opacity of the grid at full weight (before the level of detail and the fades).</summary>
        const float GridAlpha = 0.3f;

        /// <summary>HDR multiplier of the neutral tone: ~0.48 at most, far below the bloom threshold.</summary>
        const float GridIntensity = 0.7f;

        const float RayWeight = 1f;
        const float RungWeight = 1f;

        /// <summary>Radial e-folding of the material's fade (data units of rho): the far fan dissolves into the dark.</summary>
        const float GridRhoFade = 20f;

        /// <summary>
        /// Opacity of the grid out in open space, beyond the red envelope, relative to inside it: there no red veils
        /// it (the envelope's fill lies over the grid), so it dims to keep the same weight on the dark and does not
        /// outshine the matter it frames.
        /// </summary>
        const float OpenSpaceAlpha = 0.45f;

        /// <summary>... reached this far beyond the envelope's edge (fraction of the envelope's width), where its fill has faded out.</summary>
        const float OpenSpaceSpan = 0.6f;

        /// <summary>
        /// Weight of the settled grid, relative to the fan: from the biosphere on the story's scale is earthly, so the
        /// parallel rays recede (reached over <see cref="SettleSpan"/>), leaving the late red layer and the life layer
        /// above it unstriped.
        /// </summary>
        const float SettledWeight = 0.5f;

        /// <summary>World units along the clock after the biosphere over which the grid recedes to <see cref="SettledWeight"/>.</summary>
        const float SettleSpan = 1.5f;

        /// <summary>Geometry fainter than this (vertex alpha after the radial fade) is left out.</summary>
        const float CullAlpha = 0.01f;

        /// <summary>
        /// The grid dissolves ahead of the human branch as HandoffFade^this on top of the material's own handoff
        /// fade: the vast far fan is gone well before the branch.
        /// </summary>
        const float HandoffExponent = 2f;

        // --- sampling along the clock: geometric steps out of the cusp, at most MaxStep of arc length, and at most
        // LensLnStep of lens time ---
        /// <summary>First sample after the cusp (world units along the clock); the rays fan out of it as straight lines.</summary>
        const float CuspSigma = 1e-3f;

        /// <summary>Largest step relative to the arc length since the Big Bang: the fan opens self-similarly out of the cusp.</summary>
        const float RelativeStep = 0.05f;

        /// <summary>Largest step (world units along the clock): the far rays sweep wide arcs.</summary>
        const float MaxStep = 0.03f;

        /// <summary>
        /// Largest step in ln(yearsAgo + <see cref="LensLogOffset"/>): lens time of the presets that still show
        /// matter (the smallest of their log offsets), so unrolled windows stay smooth too.
        /// </summary>
        const double LensLnStep = 0.04;

        const double LensLogOffset = 1e6;

        // --- labels ---
        /// <summary>Rays that carry an order-of-magnitude label, and how far through the fan's opening (0 .. 1) each sits.</summary>
        static readonly int[] LabelOrders = { 38, 30, 20, 10 };

        static readonly float[] LabelAlong = { 0.08f, 0.25f, 0.5f, 0.75f };

        /// <summary>The inflation label sits this far out of the cusp (world units along the clock), clear of the Big Bang's glow.</summary>
        const float InflationLabelSigma = 0.25f;

        const float OrderPriority = 2.5f;
        const float InflationPriority = 3f;
        const float WorldPriority = 2f;
        const float LabelSize = 11f;

        // --- level of detail: the grid is the overview's story (distance 22); it recedes as the camera comes close, to
        // about half in the lens presets (distances 7 - 10), where the settled rays would otherwise stripe the matter ---
        const float LodNear = 5f;
        const float LodFar = 18f;
        const float AlphaClose = 0.4f;

        const string Blurb =
            "A schematic scale grid on space itself. Our track is our world's scale, and each ray outward stands for " +
            "a scale ten times larger than the one inside it: 38 orders of magnitude in all, about as many as " +
            "separate the Planck length, the smallest scale current physics can describe, from our world's. All of " +
            "it started together at the Big Bang and fanned out as space expanded: inflation stretched space about " +
            "10^26-fold in roughly 10^-32 seconds, space has grown about 1,100-fold since the cosmic microwave " +
            "background was released 380,000 years after the Big Bang, and today the expansion is accelerating. The " +
            "grid steadies at the start of the biosphere only as a visual cue that the story's scale shifts from the " +
            "cosmic to the earthly: space itself has never stopped expanding.";

        public override int Order => 6;
        public override IEnumerable<string> RequiredTexts => new[] { MatterLayer.DataPath };

        /// <summary>The fan along the clock: its opening, growth and rays (plain math, safe on any thread).</summary>
        public readonly struct Fan
        {
            /// <summary>Clock arc of the Big Bang, the cusp every ray starts from.</summary>
            public readonly float BigBangArc;

            /// <summary>Clock arc of the start of the biosphere, where the fan holds still.</summary>
            public readonly float BiosphereArc;

            /// <summary>Arc length along the clock from the Big Bang to the biosphere (world units).</summary>
            public readonly float BiosphereSigma;

            /// <summary>Opening of the fan from the biosphere on: the radius of the outermost ray (data units).</summary>
            public readonly float Scale;

            /// <summary>ln of the ratio r between the radii of neighbouring rays.</summary>
            public readonly float LnRatio;

            /// <summary>The fan out of the Big Bang at <paramref name="bigBangArc"/>, opening to <see cref="EnvelopeReach"/> times the red envelope at the biosphere.</summary>
            public Fan(float bigBangArc, float envelopeAtBiosphere)
            {
                BigBangArc = bigBangArc;
                BiosphereArc = Mathf.Min(DeepTime.Arc(BiosphereYearsAgo), bigBangArc - 1e-3f);
                BiosphereSigma = (bigBangArc - BiosphereArc) * GraphWarp.BasePath.SigmaPerArc;
                Scale = Mathf.Max(EnvelopeReach * envelopeAtBiosphere, 4 * InnerScale);
                LnRatio = Mathf.Log(Scale / InnerScale) / Orders;
            }

            /// <summary>Ratio between the radii (and the gaps) of neighbouring rays.</summary>
            public float Ratio => Mathf.Exp(LnRatio);

            /// <summary>Arc length along the clock from the Big Bang at arc u (world units; the circle part of the base path).</summary>
            public float Sigma(float u) => (BigBangArc - u) * GraphWarp.BasePath.SigmaPerArc;

            /// <summary>Arc at arc length s from the Big Bang (inverse of <see cref="Sigma"/>).</summary>
            public float ArcOf(float s) => BigBangArc - s / GraphWarp.BasePath.SigmaPerArc;

            /// <summary>Opening S at arc length s: 0 at the Big Bang, <see cref="Scale"/> from the biosphere on.</summary>
            public float Opening(float s) => (float)(Scale * Shape(Math.Min(Math.Max(s / (double)BiosphereSigma, 0), 1)));

            /// <summary>Growth rate of the opening, d ln S / ds per world unit along the clock (~1 / s near the cusp, 0 from the biosphere on).</summary>
            public float Growth(float s)
            {
                if (s >= BiosphereSigma) return 0;
                double x = Math.Max(s / (double)BiosphereSigma, 1e-12);
                return (float)(ShapeSlope(x) / (Shape(x) * BiosphereSigma));
            }

            /// <summary>The opening's profile F(x), x = s / sb in 0 .. 1: 0 at the Big Bang, 1 with zero slope and curvature at the biosphere.</summary>
            static double Shape(double x)
            {
                double rest = 1 - x;
                return Burst * (1 - rest * rest * rest) + (1 - Burst) * x * x * x * (10 + x * (6 * x - 15));
            }

            /// <summary>dF / dx = (1 - x)^2 (3 Burst + 30 (1 - Burst) x^2).</summary>
            static double ShapeSlope(double x)
            {
                double rest = 1 - x;
                return rest * rest * (3 * Burst + 30 * (1 - Burst) * x * x);
            }

            /// <summary>Radius of a ray relative to the opening: r^(order - Orders), 1 for the outermost ray.</summary>
            public float Reach(float order) => Mathf.Exp((order - Orders) * LnRatio);

            /// <summary>Radius of ray <paramref name="order"/> at arc length s (data units of rho).</summary>
            public float Rho(float order, float s) => Opening(s) * Reach(order);

            /// <summary>
            /// Perpendicular world distance from a ray at radius rho (arc length s) to its inner neighbour, rho / r:
            /// the radial gap, foreshortened where the rays leave the clock steeply (near the cusp).
            /// </summary>
            public float Gap(float rho, float s)
            {
                float h = 1 + rho / GraphStyle.R0;
                float climb = rho * Growth(s);
                return rho * (1 - Mathf.Exp(-LnRatio)) * h / Mathf.Sqrt(h * h + climb * climb);
            }

            /// <summary>Rung coordinate at arc length s: ln(s / sb) up to the biosphere, continued linearly (C1) after it.</summary>
            public double Rung(float s) =>
                s <= BiosphereSigma ? Math.Log(Math.Max(s, 1e-9f) / (double)BiosphereSigma) : (s - BiosphereSigma) / (double)BiosphereSigma;

            /// <summary>Arc length at which the rung with coordinate w leaves our track (inverse of <see cref="Rung"/>).</summary>
            public float SigmaOfRung(double w) => (float)(w <= 0 ? BiosphereSigma * Math.Exp(w) : BiosphereSigma * (1 + w));

            /// <summary>
            /// Slope of a rung, d s / d ln(rho / S): the orthogonal trajectory of the rays through (s, rho) in the
            /// world metric of the clock (tangential lengths grow as (R0 + rho) / R0). Near the cusp this traces a
            /// circle around it; from the biosphere on it is 0 (straight across the parallel rays).
            /// </summary>
            public float RungSlope(float s, float rho)
            {
                float g = Growth(s);
                if (g <= 0) return 0;
                float h = 1 + rho / GraphStyle.R0;
                float climb = rho * g;
                return -rho * climb / (h * h + climb * climb);
            }
        }

        LineMeshBuilder lines;
        Material material;
        float lod = -1;

        public override void Prepare(GraphContext ctx)
        {
            Stopwatch sw = Stopwatch.StartNew();
            MatterFile file = MatterFile.Parse(ctx.Text(MatterLayer.DataPath), out string error);
            if (file == null)
            {
                Debug.LogWarning("[Why] ExpansionGridLayer: " + error);
                return;
            }

            MatterLayout layout = MatterLayout.Build(file);
            if (layout.IsEmpty) return;

            float uBio = Mathf.Min(DeepTime.Arc(BiosphereYearsAgo), layout.BigBangArc - 1e-3f);
            Fan fan = new Fan(layout.BigBangArc, layout.Envelope(uBio));
            List<float> ss = Samples(fan);
            LineMeshBuilder builder = new LineMeshBuilder((Orders + 1) * ss.Count / 2 + 32 * CellSamples * Orders);
            int rayCount = BuildRays(builder, fan, layout, ss);
            int rungCount = BuildRungs(builder, fan, layout);

            Register(ctx, fan, layout.BigBangYa);
            lines = builder;

            Debug.Log($"[Why] ExpansionGridLayer.Prepare {sw.Elapsed.TotalMilliseconds:0.0} ms: {rayCount} rays over " +
                      $"{ss.Count} samples (ratio {fan.Ratio:0.000}, opening {fan.Scale:0.0}), {rungCount} rungs, " +
                      $"{builder.VertexCount} vertices");
        }

        /// <summary>
        /// Arc lengths from the Big Bang (0, the cusp) to the end of the red layer, ascending: geometric steps out of
        /// the cusp, at most <see cref="MaxStep"/>, tighter where the clock compresses time (lens windows unroll the
        /// late clock), with the biosphere exactly.
        /// </summary>
        static List<float> Samples(Fan fan)
        {
            float end = fan.Sigma(MatterLayout.EndArc), bio = fan.BiosphereSigma;
            List<float> s = new List<float>(Mathf.CeilToInt(end / MaxStep) + 256) { 0 };
            for (float x = CuspSigma; x < end;)
            {
                s.Add(x);
                float step = Mathf.Min(MaxStep, RelativeStep * x);
                double d = Math.Abs(LensLn(fan.ArcOf(x)) - LensLn(fan.ArcOf(x + step)));
                if (d > LensLnStep) step *= (float)(LensLnStep / d);
                // land on the biosphere exactly (where the rays turn parallel) in one or two steps
                float toBio = bio - x;
                if (toBio > 1e-5f && toBio < 1.5f * step) step = toBio > step ? 0.5f * toBio : toBio;
                // the last step ends exactly at the end and is between half and one and a half steps long
                if (x + 1.5f * step >= end) break;
                x += step;
            }

            s.Add(end);
            return s;
        }

        static double LensLn(float u) => Math.Log(DeepTime.YearsAgo(Math.Max(u, 0)) + LensLogOffset);

        /// <summary>
        /// The rays, outermost first. Each starts where it first shows (one sample before) and ends where it has
        /// faded out (far out, or at the handoff); rays too crowded to show anywhere are left out.
        /// </summary>
        static int BuildRays(LineMeshBuilder builder, Fan fan, MatterLayout layout, List<float> ss)
        {
            int m = ss.Count;
            float[] us = new float[m], opening = new float[m], fade = new float[m], envelope = new float[m];
            for (int i = 0; i < m; i++)
            {
                us[i] = fan.ArcOf(ss[i]);
                opening[i] = fan.Opening(ss[i]);
                fade[i] = Handoff(us[i]) * Settle(fan, ss[i]);
                envelope[i] = layout.Envelope(us[i]);
            }

            List<LinePoint> pts = new List<LinePoint>(m);
            int count = 0;
            for (int j = Orders; j >= 0; j--)
            {
                float reach = fan.Reach(j), edge = Edge(j - Orders);
                pts.Clear();
                int first = -1, last = -1;
                for (int i = 1; i < m; i++)
                {
                    float rho = opening[i] * reach;
                    float gap = fan.Gap(rho, ss[i]);
                    float alpha = RayWeight * edge * Emerge(gap) * Faint(gap) * Unveiled(rho, envelope[i]) * fade[i];
                    float width = Mathf.Min(RayWidthPerGap * gap, MaxWidthWorld);
                    pts.Add(new LinePoint(new Vector3(us[i], GraphStyle.MatterY, rho), Tint(alpha), WidthPx, width));
                    if (alpha * Mathf.Exp(-rho / GridRhoFade) < CullAlpha) continue;
                    if (first < 0) first = i - 1;
                    last = i - 1;
                }

                if (first < 0) continue;
                // keep one invisible point on either side, so the ray fades in and out along a whole segment
                int from = Mathf.Max(first - 1, 0), to = Mathf.Min(last + 1, pts.Count - 1);
                if (to - from < 1) continue;
                if (to < pts.Count - 1) pts.RemoveRange(to + 1, pts.Count - 1 - to);
                if (from > 0) pts.RemoveRange(0, from);
                builder.AddPolyline(pts, GridId);
                count++;
            }

            return count;
        }

        /// <summary>
        /// The rungs: orthogonal trajectories of the rays, integrated outward (RK4 in t = ln(rho / S)) from where they
        /// leave our track, at equal steps of the rung coordinate. A rung shows where the gap to the rung inside it
        /// (toward the Big Bang) and the gap between the rays it crosses have both opened beyond the crowding gap, and
        /// it is drawn as a chain of dashes, one per cell between neighbouring rays, open where it crosses a ray that
        /// shows.
        /// </summary>
        static int BuildRungs(LineMeshBuilder builder, Fan fan, MatterLayout layout)
        {
            float end = fan.Sigma(MatterLayout.EndArc);
            int kFirst = (int)Math.Floor(fan.Rung(FirstRungSigma) / RungStep);
            int kLast = (int)Math.Floor(fan.Rung(end) / RungStep);
            if (fan.SigmaOfRung(kLast * RungStep) >= end) kLast--;
            int rungs = kLast - kFirst + 1;
            if (rungs < 2) return 0;

            // the grid of t shared by every rung, so neighbours meet each ray at the same index: a short run inside
            // ray 0, then per cell its inner ray, the dash (start, full strength, middle, full strength, end), and the
            // outermost ray last; open[i] is the ray whose crossing leaves sample i open (-1: none)
            int n = RungInnerSamples + CellSamples * Orders + 1;
            float[] ts = new float[n];
            int[] open = new int[n];
            float t0 = -Orders * fan.LnRatio;
            for (int i = 0; i < RungInnerSamples; i++)
            {
                ts[i] = t0 - RungInnerMargin * (1 - i / (float)RungInnerSamples);
                open[i] = -1;
            }

            for (int j = 0; j < Orders; j++)
            {
                int i = RungInnerSamples + CellSamples * j;
                float tj = t0 + j * fan.LnRatio;
                ts[i] = tj;
                ts[i + 1] = tj + DashGap * fan.LnRatio;
                ts[i + 2] = tj + (DashGap + DashRamp) * fan.LnRatio;
                ts[i + 3] = tj + 0.5f * fan.LnRatio;
                ts[i + 4] = tj + (1 - DashGap - DashRamp) * fan.LnRatio;
                ts[i + 5] = tj + (1 - DashGap) * fan.LnRatio;
                open[i] = open[i + 1] = j;
                open[i + 2] = open[i + 3] = open[i + 4] = -1;
                open[i + 5] = j + 1;
            }

            ts[n - 1] = 0;
            open[n - 1] = Orders;

            float[] sig = new float[rungs * n];
            for (int k = 0; k < rungs; k++)
            {
                float s = fan.SigmaOfRung((kFirst + k) * RungStep);
                sig[k * n] = s;
                for (int i = 1; i < n; i++) sig[k * n + i] = s = Step(fan, ts[i - 1], s, ts[i] - ts[i - 1]);
            }

            List<LinePoint> pts = new List<LinePoint>(n);
            int count = 0;
            // the innermost rung only serves as the neighbour of the next one
            for (int k = 1; k < rungs; k++)
            {
                pts.Clear();
                int first = -1, last = -1;
                for (int i = 0; i < n; i++)
                {
                    float scale = Mathf.Exp(ts[i]), s = sig[k * n + i], inner = sig[(k - 1) * n + i];
                    float rho = scale * fan.Opening(s), u = fan.ArcOf(s);
                    float gap = Distance(s, rho, inner, scale * fan.Opening(inner));
                    float cellGap = fan.Gap(rho, s), cells = Emerge(cellGap);
                    float alpha = RungWeight * Edge(ts[i] / fan.LnRatio) * Emerge(gap) * Faint(gap) * cells *
                                  Unveiled(rho, layout.Envelope(u)) * Handoff(u) * Settle(fan, s);
                    // open around a ray it crosses, as deep as that ray shows
                    if (open[i] >= 0) alpha *= 1 - cells * Edge(open[i] - Orders);
                    float width = Mathf.Min(RungWidthPerGap * Mathf.Min(gap, cellGap), MaxWidthWorld);
                    pts.Add(new LinePoint(new Vector3(u, GraphStyle.MatterY, rho), Tint(alpha), WidthPx, width));
                    if (alpha * Mathf.Exp(-rho / GridRhoFade) < CullAlpha) continue;
                    if (first < 0) first = i;
                    last = i;
                }

                if (first < 0) continue;
                int from = Mathf.Max(first - 1, 0), to = Mathf.Min(last + 1, pts.Count - 1);
                if (to < pts.Count - 1) pts.RemoveRange(to + 1, pts.Count - 1 - to);
                if (from > 0) pts.RemoveRange(0, from);
                builder.AddPolyline(pts, GridId);
                count++;
            }

            return count;
        }

        /// <summary>One RK4 step of a rung's arc length s over dt of ln(rho / S), starting at t.</summary>
        static float Step(Fan fan, float t, float s, float dt)
        {
            float k1 = Slope(fan, t, s);
            float k2 = Slope(fan, t + 0.5f * dt, s + 0.5f * dt * k1);
            float k3 = Slope(fan, t + 0.5f * dt, s + 0.5f * dt * k2);
            float k4 = Slope(fan, t + dt, s + dt * k3);
            return Mathf.Max(s + dt / 6 * (k1 + 2 * k2 + 2 * k3 + k4), 1e-7f);
        }

        static float Slope(Fan fan, float t, float s)
        {
            s = Mathf.Max(s, 1e-7f);
            return fan.RungSlope(s, Mathf.Exp(t) * fan.Opening(s));
        }

        /// <summary>World distance between two points of the clock's circle part, (arc length, rho) each.</summary>
        static float Distance(float s0, float rho0, float s1, float rho1)
        {
            float r0 = GraphStyle.R0 + rho0, r1 = GraphStyle.R0 + rho1;
            float dphi = (s1 - s0) / GraphStyle.R0;
            return Mathf.Sqrt(Mathf.Max(r0 * r0 + r1 * r1 - 2 * r0 * r1 * Mathf.Cos(dphi), 0));
        }

        /// <summary>The grid's anchor (the Big Bang, where space starts expanding), orders of magnitude on a few rays, a note on inflation and one on our world's scale.</summary>
        static void Register(GraphContext ctx, Fan fan, double bigBangYa)
        {
            // the tooltip's time span reads "13.8 billion years ago - now": space has expanded ever since
            Anchors.Register(new Anchor
            {
                Key = AnchorKey, Label = "Expanding space", Blurb = Blurb, Level = GraphLevel.Matter,
                YearsAgo = bigBangYa, EndYearsAgo = 0, Y = GraphStyle.MatterY, Rho = 0,
                Ids = IdRange.Single(GridId), Tier = 2
            });

            for (int k = 0; k < LabelOrders.Length; k++)
            {
                float s = LabelAlong[k] * fan.BiosphereSigma;
                AddLabel(ctx, "×10<sup>" + LabelOrders[k] + "</sup>",
                    new Vector3(fan.ArcOf(s), GraphStyle.MatterY, fan.Rho(LabelOrders[k], s)), OrderPriority);
            }

            AddLabel(ctx, "our world's scale",
                new Vector3(fan.BiosphereArc, GraphStyle.MatterY, fan.Rho(0, fan.BiosphereSigma)), WorldPriority);

            // among the rays bursting out of the cusp, just clear of the Big Bang's glow
            AddLabel(ctx, "inflation: ×10<sup>26</sup> in 10<sup>-32</sup> s",
                new Vector3(fan.ArcOf(InflationLabelSigma), GraphStyle.MatterY, fan.Rho(Orders - 4, InflationLabelSigma)),
                InflationPriority);
        }

        static void AddLabel(GraphContext ctx, string text, Vector3 data, float priority)
        {
            ctx.Labels.Add(new LabelSpec
            {
                Text = text,
                Data = data,
                Priority = priority,
                SizePx = LabelSize,
                Color = GraphStyle.TextDim,
                PixelOffset = new Vector2(5, 6),
                AnchorKey = AnchorKey,
                HandoffFade = true,
                Ids = IdRange.Single(GridId)
            });
        }

        public override void Upload(GraphContext ctx)
        {
            if (lines == null) return;
            // neutral like the axes: hue is reserved for the levels, and grey reads as space, not as matter
            Color color = GraphStyle.Axis;
            color.a = GridAlpha;
            // beneath the envelope and the band fills (QueueMatter, alpha blended: they veil it) and the matter
            // lines (QueueMatter + 1); additive like the other lines
            material = GraphMaterials.Line(color, GridIntensity, GraphMaterials.QueueMatter - 1, true, GridRhoFade, 0f);
            GraphMaterials.FadeBeforeHumanBranch(material);
            AddMesh("MatterExpansionGrid", lines.ToMesh("MatterExpansionGrid"), material);
            lines = null;
        }

        /// <summary>Level of detail: the grid recedes when the camera comes close.</summary>
        public override void Tick(GraphContext ctx, CameraRig rig)
        {
            if (material == null || rig == null) return;
            float t = Mathf.InverseLerp(LodNear, LodFar, rig.Pose.Distance);
            float target = Mathf.Lerp(AlphaClose, 1f, t * t * (3 - 2 * t));
            if (Mathf.Abs(target - lod) < 0.01f) return;
            lod = target;
            GraphMaterials.SetAlpha(material, lod);
        }

        /// <summary>Opacity of a line whose gap to its neighbour is <paramref name="gap"/> world units: 0 while crowded, 1 once resolvable.</summary>
        public static float Emerge(float gap) => Smooth(Mathf.Log(Mathf.Max(gap, 1e-9f) / CrowdGap) / Mathf.Log(EmergeRange));

        /// <summary>Relative opacity of a resolvable line: fainter the wider apart it is from its neighbour.</summary>
        public static float Faint(float gap) => Mathf.Pow(Mathf.Max(gap / CrowdGap, 1f), -Fainter);

        /// <summary>
        /// Relative opacity at <paramref name="orders"/> (0 at the outermost ray, negative inward): the last
        /// <see cref="OuterOrders"/> fade out toward the fan's edge, so it has no hard border.
        /// </summary>
        public static float Edge(float orders) => Smooth((1 - orders) / OuterOrders);

        /// <summary>Relative opacity at radius rho where the red envelope is <paramref name="envelope"/> wide: 1 inside it, <see cref="OpenSpaceAlpha"/> well beyond it.</summary>
        public static float Unveiled(float rho, float envelope) =>
            1 - (1 - OpenSpaceAlpha) * Smooth((rho - envelope) / Mathf.Max(OpenSpaceSpan * envelope, 1e-3f));

        static float Handoff(float u) => Mathf.Pow(GraphStyle.HandoffFade(u), HandoffExponent);

        /// <summary>Relative weight at arc length s: 1 while the fan opens, receding to <see cref="SettledWeight"/> once it has settled.</summary>
        static float Settle(Fan fan, float s) => 1 - (1 - SettledWeight) * Smooth((s - fan.BiosphereSigma) / SettleSpan);

        static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3 - 2 * t);
        }

        static Color32 Tint(float alpha) => new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha) * 255f + 0.5f));
    }
}
