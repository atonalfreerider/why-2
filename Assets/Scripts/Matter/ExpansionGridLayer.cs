using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace Why.Matter
{
    /// <summary>
    /// The scale of space in the red layer, drawn as waves: space ten times inside ten times inside ten times, on the one
    /// continuous field of <see cref="MatterLayout"/>'s comb, with no knots, no cells and no step changes.
    ///
    /// Wave k is the shell of matter 10^k .. 10^(k+1) metres from our lineage today, followed back through time; its crest is
    /// the matter exactly 10^k m away, placed by <see cref="Sample.Place"/> (comoving, and carried in by the bound bodies of our
    /// lineage as they collapse), so the bands' edges, the impactors and the probe lie on the same waves. Every wave is born at
    /// the Big Bang, where the fan has no width, and rides outward as the comb opens - the outer waves fastest, like a Hubble
    /// flow - until the decades lie side by side from the inner track (a comoving kilometre) to the horizon (the observable
    /// universe), where they dissolve into black. Inside each wave the lines of the next level cut it into ten (2..9 x 10^k),
    /// each of those cells into ten again (tenths), and so on to thousandths, finer and finer toward each wave's outer crest,
    /// as a log map draws equal steps.
    ///
    /// Which levels show is a matter of pixels, so the waves on screen always match the ratio of metres to pixels the HUD reads
    /// out (<see cref="SpaceLod"/>, which mirrors the rule): a line emerges as its gap on screen grows from 3 to 7.4 px, glows
    /// gold where it opens (a node, above the bloom threshold) and fades to its level's floor as the next level opens in its
    /// cells, so about three levels read at once and every tenfold zoom opens one more. A gentle swell runs outward through
    /// the decades, timed by <see cref="GraphTime"/> so a capture repeats. All of it is one procedural fan (shader
    /// Why/ScaleWaves, one draw call): its vertices carry only the wave coordinate and the fades, and the spacing on screen
    /// comes from the screen-space derivatives of the wave coordinate, exact under the lens and the perspective.
    ///
    /// From Tick the layer publishes the scale of space at the centre of the screen (<see cref="SpaceLod"/>), shown above the
    /// HUD's legend and, at the cursor, by the probe (G). Builds its own layout because it prepares concurrently with
    /// <see cref="MatterLayer"/>.
    /// </summary>
    public sealed class ExpansionGridLayer : GraphLayer
    {
        /// <summary>Anchor of the grid: tooltips of its labels and the director.</summary>
        public const string AnchorKey = "matter:_expansion";

        /// <summary>Shared key of the <see cref="MatterScale"/> the grid was built from (the HUD's probe reads it).</summary>
        public const string ScaleKey = "matter.scale";

        /// <summary>Highlight id of the whole grid (the last matter id, clear of the bands and epochs).</summary>
        public static readonly int GridId = GraphIds.Matter(998);

        /// <summary>The smoke check that the scale of space is read out where a view frames the fan.</summary>
        public const string SmokeCheck = "scale-waves";

        /// <summary>Views whose framing puts the centre of the screen on the red fan: the readout must be published there.</summary>
        static readonly string[] CheckedViews = { "cosmos", "stars", "earth" };

        /// <summary>The grid of the scene on screen (null before it is uploaded and once it is gone): the probe reads through it.</summary>
        public static ExpansionGridLayer Current { get; private set; }

        const string ShaderName = "Why/ScaleWaves";

        // --- what is drawn ---
        /// <summary>
        /// Crests drawn, 10^MinDecade .. 10^MaxDecade m today; the outermost wave runs on to the horizon (the observable universe
        /// is 4.4e26 m). Once our lineage's bodies have formed the trunk is a comoving kilometre and every wave lies outside it;
        /// before, the matter that collapsed into the Earth, the Sun and the solar nebula still lay out on the fan, down to
        /// 10^-0.9 m today at the trunk, and its waves fill the inner track's side of the fan until they slide into the trunk
        /// as those bodies form.
        /// </summary>
        public const int MinDecade = -1, MaxDecade = 26;

        /// <summary>log10 of the horizon's distance today (metres): the last wave ends there.</summary>
        static readonly double HorizonLog10 = Math.Log10(MatterLayout.ObservableRadiusM);

        // --- look ---
        /// <summary>The lines: a neutral warm grey-white, faint and additive, so the scale reads as space rather than more matter.</summary>
        static readonly Color GridColor = new Color(0.80f, 0.74f, 0.66f);

        const float GridAlpha = 0.3f, GridIntensity = 0.75f;

        /// <summary>The crests, 10^k m today: an amber of the matter family, stronger than the finer lines, so each tenfold wave
        /// leads the ten inside it (the concept art's bright arcs over finer cells); below the bloom threshold except in the swell.</summary>
        static readonly Color CrestColor = new Color(1f, 0.62f, 0.32f);

        const float CrestOpacity = 0.55f;

        /// <summary>
        /// The nodes, where a line opens (its gap outward passes the open spacing as the comb widens): the gold-orange of the
        /// concept art, a warm member of the matter family. A node is <see cref="NodeGlow"/> times this colour at a crest (less
        /// on the finer levels), above the bloom threshold of 0.9.
        /// </summary>
        static readonly Color NodeColor = new Color(1f, 0.42f, 0.10f);

        const float NodeGlow = 3f;

        /// <summary>
        /// A node is a glint about this many pixels either side of the point where its line opens, however slowly the spacing
        /// changes along the line there, and never longer than the stretch where the line's gap is within this natural log of
        /// the open spacing (where the spacing changes fast, as near the Big Bang).
        /// </summary>
        const float NodeLengthPx = 5f, NodeWidth = 0.25f;

        /// <summary>
        /// A line is one pixel wide, plus this fraction of its gap on screen, at most this many world units or this many pixels
        /// in all (a crest zoomed far in stays a line, not a band).
        /// </summary>
        const float WidthPx = 1f, WidthPerGap = 0.03f, MaxWidthWorld = 0.03f, MaxWidthPx = 3f;

        /// <summary>
        /// The swell that runs outward through the decades: how much brighter its crest makes the waves (0: none), how fast it
        /// runs (decades a second), its half width (decades), how far below the trunk it starts, and the pause before the next.
        /// </summary>
        public const float PulseStrength = 0.4f;

        const float PulseSpeed = 2f, PulseWidth = 0.8f, PulseLead = 1.5f, PulsePause = 4f;

        /// <summary>Seconds from one swell to the next: below the trunk, past the horizon, then the pause.</summary>
        static float PulseCycle => (float)((HorizonLog10 - MinDecade + 2 * PulseLead) / PulseSpeed) + PulsePause;

        // --- fades ---
        /// <summary>A line fades in over this many decades as it leaves the inner track.</summary>
        const float InnerDecades = 0.4f;

        /// <summary>The waves dissolve over the outer part of the fan: from this fraction of the way to the horizon, out.</summary>
        const float HorizonFadeFrom = 0.62f;

        /// <summary>A line on matter that has just left a body's edge (where the matter inside fell in) fades in over this many decades.</summary>
        const double BoundDecades = 0.3;

        /// <summary>The waves dissolve ahead of the human branch as this power of <see cref="GraphStyle.HandoffFade"/>, on top of the material's own fade.</summary>
        const float HandoffExponent = 2f;

        /// <summary>How fast the grid follows a view's matter emphasis (per second), as the rest of the red layer does.</summary>
        const float EmphasisSpeed = 1.6f;

        /// <summary>Decades free of an edge stored for matter no body encloses (the fade has long saturated).</summary>
        const double FreeCap = 10;

        // --- the readout ---
        /// <summary>Below this share of their full strength the waves at a point (the centre of the screen, or the probe's
        /// cursor) are too faint to read out.</summary>
        const float ReadoutVisibility = 0.1f;

        /// <summary>A point lies on the fan when the warp's inverse comes back to it within this share of the camera's distance.</summary>
        const float OnFanTolerance = 1e-3f;

        // --- sampling along the clock (arc): geometric out of the Big Bang, then uniform, and dense through each formation ---
        const float FirstTau = 1e-5f, TauGrowth = 1.06f, DenseUntilTau = 0.02f, ArcStep = 0.0015f;
        const int FormationSamples = 16;

        // --- labels ---
        static readonly int[] LabelDecades = { 6, 9, 12, 15, 18, 21, 24, 26 };
        const float LabelPriority = 2.5f, LabelSize = 11f;

        const string Blurb =
            "The scale of space, drawn as waves. Each wave is a shell of matter between two round distances from our own " +
            "lineage today - ten metres and a hundred, a hundred and a thousand, and so on to the edge of the observable " +
            "universe - followed back through time. Every wave is born at the Big Bang, where space has no width, and rides " +
            "outward as the universe expands, the outer ones fastest; out at the horizon they dissolve, and the innermost " +
            "slide into the inner track as the Sun and the Earth collapse out of their cloud. Inside each wave finer waves " +
            "cut it into ten, and those into ten again: as you close in, the next level opens wherever its waves are a few " +
            "pixels apart, glowing gold where it opens, so the waves always match the scale on screen. The readout above " +
            "the key tells how much space one pixel covers at the centre of the screen; press G and point at the red layer " +
            "to read it anywhere.";

        /// <summary>A sample of the clock: the field's two ends, its width, and the fade before the human branch.</summary>
        sealed class Sample
        {
            public float U, Envelope, UnitsPerDecade;
            public double Log10Scale, HomeLog10, OuterLog10, Handoff;

            /// <summary>The bound bodies of our lineage, formed or not: log10 radius today (ascending), how far formed, and the edge's log10 radius now.</summary>
            public double[] BoundLog, BoundEdge;
            public float[] BoundProgress;

            /// <summary>
            /// Where the matter that is 10^<paramref name="log10Today"/> metres from us today lies now (log10 metres), and
            /// how far it is from being pinned to a bound body's edge (decades; large when free). Inside the innermost
            /// bound body that encloses it, it collapsed with the body: comoving before the body formed, fixed after;
            /// outside, comoving - but never inside the edge of a body it does not belong to (matter that fell in).
            /// </summary>
            public double Place(double log10Today, out double freeDecades)
            {
                // comoving, plus the collapse still to come of every bound body that encloses it
                double pos = log10Today + Log10Scale;
                for (int i = 0; i < BoundLog.Length; i++)
                {
                    if (BoundLog[i] < log10Today) continue;
                    pos += (1 - BoundProgress[i]) * Math.Log10(MatterLayout.CollapseFactor);
                }

                double pin = double.NegativeInfinity;
                for (int i = 0; i < BoundLog.Length && BoundLog[i] < log10Today; i++) pin = Math.Max(pin, BoundEdge[i]);
                freeDecades = double.IsNegativeInfinity(pin) ? 1e9 : pos - pin;
                return Math.Max(pos, pin);
            }

            /// <summary>
            /// The inverse of <see cref="Place"/>: how far from us today (log10 metres) the matter lies that is
            /// 10^<paramref name="log10Now"/> metres away now, and how far that is outside the edge of the bound body within it
            /// (decades; large when none). On a body's edge, where every shell that fell into the body is pinned, it is the first
            /// shell outside them (the edge's free side). Between two edges the matter is comoving, offset by the collapse still to
            /// come of the bodies around it. The edges lie in order outward: our lineage's bodies differ in radius by more than the
            /// collapse factor.
            /// </summary>
            public double Comoving(double log10Now, out double freeDecades)
            {
                int inside = -1;
                for (int i = 0; i < BoundEdge.Length; i++)
                {
                    if (BoundEdge[i] <= log10Now) inside = i;
                }

                double collapse = 0;
                for (int i = inside + 1; i < BoundLog.Length; i++) collapse += (1 - BoundProgress[i]) * Math.Log10(MatterLayout.CollapseFactor);
                freeDecades = inside < 0 ? 1e9 : log10Now - BoundEdge[inside];
                return log10Now - Log10Scale - collapse;
            }

            /// <summary>log10 of the distance from us now (metres) at rho: the comb is linear in log10 across the whole fan.</summary>
            public double Log10At(float rho) => HomeLog10 + rho / Math.Max(UnitsPerDecade, 1e-12);

            /// <summary>rho of a distance from us now (log10 metres), on the fan (0 .. its width).</summary>
            public float RhoOf(double log10Now) => (float)Math.Max(0, Math.Min(Envelope, (log10Now - HomeLog10) * UnitsPerDecade));
        }

        /// <summary>
        /// A row of the fan, the same at every sample of the clock so that consecutive samples join into strips: the inner track,
        /// a crest, a body's edge (its inner side, then its free side: the shells that fell in between have no width), or the
        /// horizon.
        /// </summary>
        enum RowKind
        {
            Trunk,
            Crest,
            EdgeIn,
            EdgeOut,
            Horizon
        }

        readonly struct Row
        {
            public readonly RowKind Kind;

            /// <summary>The crest's decade, or the body's index among the bound bodies.</summary>
            public readonly int Index;

            /// <summary>
            /// Base decade of the strip above this row: the wave coordinate is stored above it, so that a thousandth of a decade
            /// stays far above float precision on the outermost waves.
            /// </summary>
            public readonly int Cell;

            public Row(RowKind kind, int index, int cell)
            {
                Kind = kind;
                Index = index;
                Cell = cell;
            }
        }

        public override int Order => 6;
        public override IEnumerable<string> RequiredTexts => new[] { MatterLayer.DataPath };

        FanBuilder fan;
        Material material;
        MeshRenderer fanRenderer;
        MatterScale scale;
        MatterLayout layout;
        List<MatterBand> bound;
        int waveTimeId = -1;
        float emphasis = -1, shownAlpha = -1, pulseClock;

        // the readout of the centre of the screen, measured again only when the camera, the warp or the screen changes
        readonly Sample readAt = new Sample(), readSide = new Sample();
        int readCamera = -1, readWarp = -1, readWidth, readHeight;
        bool centreOnFan;
        SpaceLod.Reading centre;

        public override void Prepare(GraphContext ctx)
        {
            Stopwatch sw = Stopwatch.StartNew();
            MatterFile file = MatterFile.Parse(ctx.Text(MatterLayer.DataPath), out string error);
            if (file == null)
            {
                Debug.LogWarning("[Why] ExpansionGridLayer: " + error);
                return;
            }

            layout = MatterLayout.Build(file);
            if (layout.IsEmpty) return;
            scale = new MatterScale(layout);
            bound = BoundBodies(layout);
            List<Sample> samples = SampleClock();
            List<Row> rows = Rows(bound);
            fan = BuildFan(samples, rows, out int strips);

            Register(ctx, samples, layout.BigBangYa);
            ctx.Share(ScaleKey, scale);

            Debug.Log($"[Why] ExpansionGridLayer.Prepare {sw.ElapsedMilliseconds} ms: {samples.Count} samples, {rows.Count} rows, " +
                      $"{strips} strips, {fan.VertexCount} vertices; " +
                      $"inner track 10^{scale.HomeLog10(DeepTime.Arc(4e9)):0.0} m when life begins, 10^{scale.HomeLog10(MatterLayout.EndArc):0.0} m at the end, " +
                      $"{scale.UnitsPerDecade(MatterLayout.EndArc):0.00} units per decade");
        }

        /// <summary>The bound bodies of our lineage, every one that ever forms, by radius today: their collapse shapes the field before they form too.</summary>
        static List<MatterBand> BoundBodies(MatterLayout layout)
        {
            List<MatterBand> bodies = new List<MatterBand>(8);
            foreach (MatterBand band in layout.Bands)
            {
                if (band.IsRootHome || !band.InPath || !band.Item.Bound || band.Item.SizeM <= 0) continue;
                bodies.Add(band);
            }

            bodies.Sort((x, y) => RadiusLog10(x).CompareTo(RadiusLog10(y)));
            return bodies;
        }

        /// <summary>log10 of a body's radius today (metres).</summary>
        static double RadiusLog10(MatterBand band) => Math.Log10(0.5 * band.Item.SizeM);

        /// <summary>
        /// The clock's samples, from the Big Bang down: geometric out of it, then uniform, and dense through every bound body's
        /// formation, where the waves inside the body slide inward with its collapse (a few million years for the solar
        /// nebula: on the uniform step alone a lens drew it as a staircase of corners).
        /// </summary>
        List<Sample> SampleClock()
        {
            List<float> arcs = new List<float>(1200);
            float top = layout.BigBangArc, bottom = MatterLayout.EndArc;
            float tau = FirstTau;
            while (true)
            {
                float u = top - tau;
                if (u <= bottom) break;
                arcs.Add(u);
                tau = tau < DenseUntilTau ? tau * TauGrowth : tau + ArcStep;
            }

            foreach (MatterBand band in bound)
            {
                for (int k = 0; k <= FormationSamples; k++)
                {
                    float u = Mathf.Lerp(band.UStart, band.UFull, k / (float)FormationSamples);
                    if (u < top - FirstTau && u > bottom) arcs.Add(u);
                }
            }

            arcs.Sort((a, b) => b.CompareTo(a));
            List<Sample> samples = new List<Sample>(arcs.Count + 1);
            foreach (float u in arcs)
            {
                if (samples.Count > 0 && samples[samples.Count - 1].U - u < 2e-7f) continue;
                samples.Add(Take(layout, bound, u, null));
            }

            samples.Add(Take(layout, bound, bottom, null));
            return samples;
        }

        /// <summary>The field at arc u, into <paramref name="into"/> when given (the readout's scratch samples), else a new sample.</summary>
        static Sample Take(MatterLayout layout, List<MatterBand> bound, float u, Sample into)
        {
            Sample s = into ?? new Sample();
            int n = bound.Count;
            if (s.BoundLog == null || s.BoundLog.Length != n)
            {
                s.BoundLog = new double[n];
                s.BoundProgress = new float[n];
                s.BoundEdge = new double[n];
            }

            s.U = u;
            s.Envelope = layout.Envelope(u);
            s.UnitsPerDecade = (float)layout.UnitsPerDecade(u);
            s.Log10Scale = Math.Log10(Mathf.Max(layout.ScaleFactor(u), 1e-30f));
            s.HomeLog10 = layout.InnerLog10(u);
            s.OuterLog10 = layout.OuterLog10(u);
            s.Handoff = Mathf.Pow(GraphStyle.HandoffFade(u), HandoffExponent);
            for (int i = 0; i < n; i++)
            {
                MatterBand band = bound[i];
                s.BoundLog[i] = RadiusLog10(band);
                s.BoundProgress[i] = MatterLayout.Formation(band, u);
                s.BoundEdge[i] = layout.EdgeLog10(band, u);
            }

            return s;
        }

        // ------------------------------------------------------------------ the fan

        /// <summary>
        /// The fan's rows outward: the inner track, then the crests from the trunk to 10^MaxDecade m with every bound body's edge
        /// where its radius today falls among them, then the horizon. In this order they never cross at any moment (they may
        /// meet: the crests that fell into a body lie on its edge).
        /// </summary>
        static List<Row> Rows(List<MatterBand> bound)
        {
            List<Row> rows = new List<Row>(MaxDecade - MinDecade + 3 + 2 * bound.Count) { new Row(RowKind.Trunk, 0, MinDecade - 1) };
            int b = 0;
            for (int k = MinDecade; k <= MaxDecade + 1; k++)
            {
                // the bodies whose radius today lies below this crest come first, inner side then free side
                for (; b < bound.Count && (k > MaxDecade || RadiusLog10(bound[b]) < k); b++)
                {
                    int cell = (int)Math.Floor(RadiusLog10(bound[b]));
                    rows.Add(new Row(RowKind.EdgeIn, b, cell));
                    rows.Add(new Row(RowKind.EdgeOut, b, cell));
                }

                if (k <= MaxDecade) rows.Add(new Row(RowKind.Crest, k, k));
            }

            rows.Add(new Row(RowKind.Horizon, 0, MaxDecade));
            return rows;
        }

        /// <summary>
        /// A row at a sample: its rho, its wave coordinate (log10 metres today), how far it lies above the trunk (decades) and
        /// outside the edge of the body within it (decades, capped). Along a strip between two rows all three are linear in rho,
        /// which is what the shader's interpolation needs.
        /// </summary>
        static void RowAt(Sample s, Row row, out float rho, out double wave, out float above, out double free)
        {
            double pos;
            switch (row.Kind)
            {
                case RowKind.Trunk:
                    pos = s.HomeLog10;
                    wave = s.Comoving(pos, out free);
                    break;
                case RowKind.Horizon:
                    pos = s.OuterLog10;
                    wave = s.Comoving(pos, out free);
                    break;
                case RowKind.EdgeIn:
                    // the matter just inside the body's edge: its radius today
                    wave = s.BoundLog[row.Index];
                    pos = s.Place(wave, out free);
                    break;
                case RowKind.EdgeOut:
                    // the first shell outside those that fell in: the strip above starts on the edge's free side
                    pos = s.BoundEdge[row.Index];
                    wave = s.Comoving(pos, out free);
                    break;
                default:
                    wave = row.Index;
                    pos = s.Place(wave, out free);
                    // a crest that fell into a body lies on its edge: the strip above starts on the edge's free side
                    if (free < 0) wave = s.Comoving(pos, out free);
                    break;
            }

            // a row inside the trunk or beyond the horizon lies on it, with its wave: the strips there have no width
            if (pos < s.HomeLog10 || pos > s.OuterLog10)
            {
                pos = Math.Max(s.HomeLog10, Math.Min(s.OuterLog10, pos));
                wave = s.Comoving(pos, out free);
            }

            rho = s.RhoOf(pos);
            above = s.UnitsPerDecade > 1e-12f ? rho / s.UnitsPerDecade : 0;
            free = Math.Min(free, FreeCap);
        }

        FanBuilder BuildFan(List<Sample> samples, List<Row> rows, out int strips)
        {
            int n = samples.Count, r = rows.Count;
            float[] rho = new float[n * r], above = new float[n * r];
            double[] wave = new double[n * r], free = new double[n * r];
            int folded = 0, unordered = 0;
            for (int j = 0; j < n; j++)
            {
                Sample s = samples[j];
                for (int i = 1; i < s.BoundEdge.Length; i++)
                {
                    if (s.BoundEdge[i] <= s.BoundEdge[i - 1]) unordered++;
                }

                for (int i = 0; i < r; i++)
                {
                    int at = j * r + i;
                    RowAt(s, rows[i], out rho[at], out wave[at], out above[at], out free[at]);
                    if (i == 0 || rho[at] >= rho[at - 1]) continue;
                    // the rows never fold back over each other (they only meet)
                    if (rho[at - 1] - rho[at] > 1e-4f * Mathf.Max(1f, s.Envelope)) folded++;
                    rho[at] = rho[at - 1];
                }
            }

            if (unordered > 0) Debug.LogWarning($"[Why] ExpansionGridLayer: bound bodies' edges out of order at {unordered} samples; the waves inside them are misplaced");
            if (folded > 0) Debug.LogWarning($"[Why] ExpansionGridLayer: {folded} rows folded back over the row inside them and were held on it");

            FanBuilder builder = new FanBuilder(n * 2 * r);
            strips = 0;
            for (int i = 0; i + 1 < r; i++)
            {
                // between a body's inner side and its free side lie the shells that fell in: no width
                if (rows[i].Kind == RowKind.EdgeIn) continue;
                builder.AddStrip(samples, rows[i].Cell, i, i + 1, r, rho, above, wave, free);
                strips++;
            }

            return builder;
        }

        /// <summary>The fan's vertices and triangles (filled on the worker thread; <see cref="ToMesh"/> on the main thread).</summary>
        sealed class FanBuilder
        {
            [StructLayout(LayoutKind.Sequential)]
            struct Vertex
            {
                public Vector3 Pos;   // u, y, rho
                public Color32 Color; // a: the dissolve ahead of the human branch
                public Vector4 Wave;  // decades today above the strip's base, decades above the trunk, rho over the fan's width, decades free of a body's edge
                public Vector2 Strip; // the strip's base decade, world units per decade at the sample (the nodes' spacing outward)
            }

            static readonly VertexAttributeDescriptor[] Layout =
            {
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 4),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 2),
            };

            readonly List<Vertex> vertices;
            readonly List<int> indices;

            public int VertexCount => vertices.Count;

            public FanBuilder(int capacity)
            {
                vertices = new List<Vertex>(capacity);
                indices = new List<int>(capacity * 3);
            }

            /// <summary>The strip between two rows along the whole clock: a vertex of each at every sample, two triangles between samples.</summary>
            public void AddStrip(List<Sample> samples, int cell, int lower, int upper, int rows, float[] rho, float[] above,
                double[] wave, double[] free)
            {
                int first = vertices.Count;
                for (int j = 0; j < samples.Count; j++)
                {
                    Sample s = samples[j];
                    Color32 c = new Color32(255, 255, 255, (byte)Mathf.Clamp(Mathf.RoundToInt((float)s.Handoff * 255f), 0, 255));
                    float width = Mathf.Max(s.Envelope, 1e-6f);
                    for (int side = 0; side < 2; side++)
                    {
                        int at = j * rows + (side == 0 ? lower : upper);
                        vertices.Add(new Vertex
                        {
                            Pos = new Vector3(s.U, GraphStyle.MatterY, rho[at]),
                            Color = c,
                            Wave = new Vector4((float)(wave[at] - cell), above[at], rho[at] / width, (float)free[at]),
                            Strip = new Vector2(cell, s.UnitsPerDecade)
                        });
                    }
                }

                for (int j = 0; j + 1 < samples.Count; j++)
                {
                    int a = first + 2 * j;
                    indices.Add(a);
                    indices.Add(a + 2);
                    indices.Add(a + 1);
                    indices.Add(a + 1);
                    indices.Add(a + 2);
                    indices.Add(a + 3);
                }
            }

            public Mesh ToMesh(string name)
            {
                Mesh mesh = new Mesh { name = name };
                MeshUpdateFlags flags = MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices |
                                        MeshUpdateFlags.DontNotifyMeshUsers;
                mesh.SetVertexBufferParams(vertices.Count, Layout);
                mesh.SetVertexBufferData(vertices, 0, 0, vertices.Count, 0, flags);
                mesh.SetIndexBufferParams(indices.Count, IndexFormat.UInt32);
                mesh.SetIndexBufferData(indices, 0, 0, indices.Count, flags);
                mesh.subMeshCount = 1;
                mesh.SetSubMesh(0, new SubMeshDescriptor(0, indices.Count), flags);
                // data space, not world space: the shader warps every vertex, so the bounds must never cull
                mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1e6f);
                mesh.UploadMeshData(true);
                return mesh;
            }
        }

        // ------------------------------------------------------------------ labels

        void Register(GraphContext ctx, List<Sample> samples, double bigBangYa)
        {
            Anchors.Register(new Anchor
            {
                Key = AnchorKey, Label = "Scale of space", Blurb = Blurb, Level = GraphLevel.Matter,
                YearsAgo = bigBangYa, EndYearsAgo = 0, Y = GraphStyle.MatterY, Rho = 0,
                Ids = IdRange.Single(GridId), Tier = 2
            });

            // crests are labeled near the present, where they have settled at their distance today
            foreach (int n in LabelDecades)
            {
                if (n > MaxDecade) continue;
                for (int j = samples.Count - 1; j >= 0; j--)
                {
                    if (CrestAlpha(samples[j], n, out float rho) < 0.3f) continue;
                    AddLabel(ctx, "10<sup>" + n + "</sup> m", new Vector3(samples[j].U, GraphStyle.MatterY, rho));
                    break;
                }
            }
        }

        /// <summary>
        /// A crest's opacity at a sample apart from its spacing on screen - what the shader's fades give it there: born on the
        /// inner track, dissolving toward the horizon, gone where its matter fell into a body - and where it lies.
        /// </summary>
        static float CrestAlpha(Sample s, double log10Today, out float rho)
        {
            double pos = s.Place(log10Today, out double free);
            double above = pos - s.HomeLog10;
            rho = (float)(above * s.UnitsPerDecade);
            if (above <= 0 || s.UnitsPerDecade <= 1e-9f || rho > s.Envelope) return 0;
            return (float)s.Handoff * WaveFade(s, rho, free);
        }

        /// <summary>
        /// The grid's own fades at rho of a sample, with the matter there <paramref name="free"/> decades outside the edge of
        /// the body within it - what the shader multiplies per point from its vertices' wave coordinates (decades above the
        /// trunk, rho over the fan's width, decades free of an edge): born on the inner track over <see cref="InnerDecades"/>,
        /// dissolving toward the horizon from <see cref="HorizonFadeFrom"/> of the fan's width, and gone where the matter fell
        /// into a body, over <see cref="BoundDecades"/> outside its edge.
        /// </summary>
        static float WaveFade(Sample s, float rho, double free)
        {
            float above = s.UnitsPerDecade > 1e-12f ? rho / s.UnitsPerDecade : 0;
            float horizon = 1f - Smooth((rho / Mathf.Max(s.Envelope, 1e-6f) - HorizonFadeFrom) / (1f - HorizonFadeFrom));
            return Smooth(above / InnerDecades) * horizon * Smooth((float)(Math.Min(free, FreeCap) / BoundDecades));
        }

        static void AddLabel(GraphContext ctx, string text, Vector3 data)
        {
            ctx.Labels.Add(new LabelSpec
            {
                Text = text,
                Data = data,
                Priority = LabelPriority,
                SizePx = LabelSize,
                Color = GraphStyle.TextDim,
                PixelOffset = new Vector2(5, 6),
                AnchorKey = AnchorKey,
                HandoffFade = true,
                Ids = IdRange.Single(GridId)
            });
        }

        // ------------------------------------------------------------------ upload and per frame

        public override void Upload(GraphContext ctx)
        {
            if (fan == null) return;
            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError("[Why] ExpansionGridLayer: shader " + ShaderName + " is missing; the scale of space is not drawn");
                fan = null;
                return;
            }

            // above the band fills (which would veil the fine waves up close), additive with the band edges
            Material m = new Material(shader) { renderQueue = GraphMaterials.QueueMatter + 1 };
            Color line = GridColor * GridIntensity;
            line.a = GridAlpha;
            m.SetColor("_Color", line);
            m.SetColor("_NodeColor", NodeColor);
            Color crest = CrestColor;
            crest.a = CrestOpacity;
            m.SetColor("_CrestColor", crest);
            m.SetFloat("_Id", GridId);
            // the rule SpaceLod reads the waves by: the readout names exactly what is drawn
            float[] w = SpaceLod.Weight, f = SpaceLod.Floor;
            m.SetVector("_Open", new Vector4(SpaceLod.EmergePx, SpaceLod.OpenPx, SpaceLod.Levels, 0));
            m.SetVector("_Weights", new Vector4(w[0], w[1], w[2], w[3]));
            m.SetVector("_Floors", new Vector4(f[0], f[1], f[2], f[3]));
            m.SetVector("_Fine", new Vector4(w[4], 0, 0, 0));
            m.SetVector("_Range", new Vector4(MinDecade, (float)HorizonLog10, 0, 0));
            m.SetVector("_Fades", new Vector4(InnerDecades, HorizonFadeFrom, (float)BoundDecades, 0));
            m.SetVector("_Width", new Vector4(WidthPx, WidthPerGap, MaxWidthWorld, MaxWidthPx));
            m.SetVector("_Node", new Vector4(NodeGlow, NodeWidth, NodeLengthPx, 0));
            m.SetVector("_Pulse", new Vector4(PulseStrength, PulseSpeed, PulseWidth, PulseLead));
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.One);
            GraphMaterials.FadeBeforeHumanBranch(m);
            material = m;
            waveTimeId = Shader.PropertyToID("_WaveTime");
            fanRenderer = AddMesh("MatterScaleWaves", fan.ToMesh("MatterScaleWaves"), m);
            fan = null;

            Current = this;
            SmokeChecks.Register(SmokeCheck, Check);
        }

        /// <summary>
        /// The matter emphasis of the view (as <see cref="MatterLayer"/>: life views dim the red layer unless the director points
        /// at matter), the swell's clock, and the readout of the centre of the screen.
        /// </summary>
        public override void Tick(GraphContext ctx, CameraRig rig)
        {
            if (material == null) return;

            float target = 1f;
            GraphRoot root = GraphRoot.Instance;
            if (root != null && root.CurrentPreset != null) target = root.CurrentPreset.MatterEmphasis;
            if (Highlighter.IsHighlighted(IdRange.Single(GridId)) || (layout != null && Highlighter.IsHighlighted(layout.AllIds))) target = 1f;
            emphasis = emphasis < 0 ? target : Mathf.MoveTowards(emphasis, target, GraphTime.Delta * EmphasisSpeed);
            if (Mathf.Abs(emphasis - shownAlpha) > 1e-3f)
            {
                shownAlpha = emphasis;
                GraphMaterials.SetAlpha(material, emphasis);
            }

            bool drawn = shownAlpha > 0f;
            if (fanRenderer != null && fanRenderer.enabled != drawn) fanRenderer.enabled = drawn;

            // the graph's clock, which a capture steps frame by frame, so the swell repeats in every recording
            pulseClock = Mathf.Repeat(pulseClock + GraphTime.Delta, PulseCycle);
            material.SetFloat(waveTimeId, pulseClock);

            Readout(rig);
        }

        /// <summary>
        /// Publishes the scale of space where the centre of the screen meets the fan, or clears it: in a headset (no pixel to
        /// speak of), while the graph grows from the Big Bang, off the fan, and where the waves there have faded below a
        /// tenth (<see cref="Shows"/>).
        /// </summary>
        void Readout(CameraRig rig)
        {
            Camera cam = rig != null ? rig.Cam : null;
            if (cam == null || !Readable)
            {
                SpaceLod.Clear();
                return;
            }

            // the point under the centre of the screen moves only with the camera, the warp and the screen's size
            if (rig.Version != readCamera || GraphWarp.Version != readWarp || cam.pixelWidth != readWidth || cam.pixelHeight != readHeight)
            {
                readCamera = rig.Version;
                readWarp = GraphWarp.Version;
                readWidth = cam.pixelWidth;
                readHeight = cam.pixelHeight;
                centreOnFan = Read(cam, cam.pixelRect.center, out centre);
            }

            if (centreOnFan && Shows(centre)) SpaceLod.Publish(centre);
            else SpaceLod.Clear();
        }

        /// <summary>
        /// True when the grid may be read out at all, at the centre of the screen or by the probe (G): it is drawn (the view
        /// has not dimmed it away), outside a headset (no pixel to speak of) and not while the graph grows from the Big Bang
        /// (the waves are not drawn yet).
        /// </summary>
        public bool Readable => material != null && shownAlpha > 0f && !Xr.XrSession.Active && !GraphReveal.On;

        /// <summary>
        /// True when the waves at a reading's point (<see cref="Read"/>) show strongly enough to read out, by the same rule for
        /// the readout of the centre of the screen and for the probe (G): the grid may be read out, and the view's emphasis,
        /// the dissolve ahead of the human branch, the lens window's fade and the grid's own fades there
        /// (<see cref="SpaceLod.Reading.Fade"/>) leave at least <see cref="ReadoutVisibility"/> of its full strength.
        /// </summary>
        public bool Shows(SpaceLod.Reading reading) =>
            Readable && shownAlpha * Visibility(reading.U) * reading.Fade >= ReadoutVisibility;

        /// <summary>How much of the grid shows at arc u apart from its emphasis and its own fades: the dissolve ahead of the
        /// human branch (the vertices' and the material's) and the lens window's fade.</summary>
        static float Visibility(float u) =>
            Mathf.Pow(GraphStyle.HandoffFade(u), HandoffExponent + 1f) * GraphWarp.FocusFade(u, GraphWarp.Current);

        /// <summary>
        /// The scale of space at a screen point (pixels, as <see cref="Camera.ScreenPointToRay(Vector3)"/> takes them) as the
        /// grid draws it there: where the ray meets the matter plane, the metres one pixel spans outward (two points a little
        /// apart along the clock's normal, projected), the finest waves fully open by the shader's rule, and how much of the
        /// waves the grid's own fades leave there. False off the fan: off its ends, inside the inner track, beyond the horizon,
        /// before the Big Bang or after the matter era. A reading is shown only where <see cref="Shows"/> says the waves show.
        /// </summary>
        public bool Read(Camera cam, Vector2 screen, out SpaceLod.Reading reading)
        {
            reading = default;
            if (cam == null || layout == null || bound == null) return false;

            // the matter plane under the point, then back through the warp to the clock
            WarpState warp = GraphWarp.Current;
            Ray ray = cam.ScreenPointToRay(screen);
            if (Mathf.Abs(ray.direction.y) < 1e-5f) return false;
            float along = (GraphStyle.MatterY * warp.YScale - ray.origin.y) / ray.direction.y;
            if (along <= 0) return false;
            Vector3 hit = ray.GetPoint(along);
            GraphWarp.Inverse(hit, out float u, out float rho);
            if (u < MatterLayout.EndArc || u > layout.BigBangArc) return false;
            Sample s = Take(layout, bound, u, readAt);
            if (rho < 0 || rho > s.Envelope) return false;
            // off the ends of the fan the nearest point of the clock is not where the ray met the plane
            Vector3 p = GraphWarp.ToWorld(u, GraphStyle.MatterY, rho);
            if ((p - hit).magnitude > OnFanTolerance * along) return false;

            // metres per pixel outward: two points a little apart along the normal, projected
            Vector3 normal = GraphWarp.NormalAt(u, warp);
            float step = 1e-3f * along;
            Vector3 a = cam.WorldToScreenPoint(p), b = cam.WorldToScreenPoint(p + normal * step);
            if (a.z <= 0 || b.z <= 0) return false;
            double pxPerWorld = new Vector2(b.x - a.x, b.y - a.y).magnitude / step;
            if (pxPerWorld <= 1e-9) return false;
            double log10MetresPerPixel = scale.Log10MetresPerUnit(u, rho) - Math.Log10(Math.Max(warp.RhoScale, 1e-9f)) - Math.Log10(pxPerWorld);

            double pos = s.Log10At(rho);
            double wave = s.Comoving(pos, out double free);
            double g = DecadesPerPixel(cam, s, u, rho, wave, a);
            // below the least wave drawn none is open (the fan holds none there: a guard against a field that changes)
            double spacingToday = double.NaN;
            int level = wave >= MinDecade - 1e-6 ? SpaceLod.FinestOpenLevel(wave, g, out spacingToday) : -1;
            int crest = Mathf.Clamp((int)Math.Round(wave), MinDecade, MaxDecade);

            reading = new SpaceLod.Reading
            {
                U = u,
                Rho = rho,
                YearsAgo = DeepTime.YearsAgo(u),
                Log10MetresPerPixel = log10MetresPerPixel,
                ComovingLog10 = wave,
                Log10Then = pos - wave,
                DecadesPerPixel = g,
                FinestLevel = level,
                Log10WaveSpacing = spacingToday + (pos - wave),
                CrestDecade = crest,
                CrestRho = s.RhoOf(s.Place(crest, out _)),
                Fade = WaveFade(s, rho, free)
            };
            return true;
        }

        /// <summary>
        /// Decades of the wave coordinate per screen pixel at (u, rho), the length of its screen-space gradient, as the shader
        /// takes it from ddx and ddy: along a crest the coordinate does not change, outward it rises 1 / UnitsPerDecade a unit of
        /// rho, so the gradient is the outward step's rise over its pixels across the crest's direction on screen.
        /// </summary>
        double DecadesPerPixel(Camera cam, Sample s, float u, float rho, double wave, Vector3 at)
        {
            // the crest through the point a little earlier and later
            const float du = 2e-4f;
            float u0 = Mathf.Min(u + du, layout.BigBangArc), u1 = Mathf.Max(u - du, MatterLayout.EndArc);
            Sample side = Take(layout, bound, u0, readSide);
            Vector3 c0 = cam.WorldToScreenPoint(GraphWarp.ToWorld(u0, GraphStyle.MatterY, side.RhoOf(side.Place(wave, out _))));
            side = Take(layout, bound, u1, readSide);
            Vector3 c1 = cam.WorldToScreenPoint(GraphWarp.ToWorld(u1, GraphStyle.MatterY, side.RhoOf(side.Place(wave, out _))));
            // and a little outward
            float dr = Mathf.Max(1e-3f * s.Envelope, 1e-5f);
            Vector3 o = cam.WorldToScreenPoint(GraphWarp.ToWorld(u, GraphStyle.MatterY, rho + dr));

            Vector2 crestDirection = new Vector2(c1.x - c0.x, c1.y - c0.y);
            Vector2 outward = new Vector2(o.x - at.x, o.y - at.y) / dr;
            double rise = 1.0 / Math.Max(s.UnitsPerDecade, 1e-12);
            double across = crestDirection.sqrMagnitude > 1e-12f
                ? Math.Abs(Vector2.Dot(new Vector2(-crestDirection.y, crestDirection.x).normalized, outward))
                : outward.magnitude;
            return rise / Math.Max(across, 1e-9);
        }

        /// <summary>
        /// Smoke check: where a view frames the fan (outside the tour, which aims every stop at its own anchor, and outside a
        /// headset, which has no pixel to read) the scale of space is published, finite, with a wave fully open, and the grid
        /// is one renderer. Logs the reading, so a run's numbers can be compared with its frames.
        /// </summary>
        static IEnumerable<string> Check(ViewPreset view)
        {
            if (view == null || Array.IndexOf(CheckedViews, view.Id) < 0) yield break;
            GraphRoot root = GraphRoot.Instance;
            if ((root != null && root.TourActive) || Xr.XrSession.Active) yield break;
            if (Current != null && Current.GetComponentsInChildren<MeshRenderer>(true).Length != 1)
            {
                yield return $"the grid has {Current.GetComponentsInChildren<MeshRenderer>(true).Length} renderers, not one";
            }

            if (!SpaceLod.Published)
            {
                yield return $"no scale of space is read out in the {view.Id} view: the centre of the screen should lie on the red fan";
                yield break;
            }

            SpaceLod.Reading r = SpaceLod.Current;
            Debug.Log($"[Why] {SmokeCheck} {view.Id}: {SpaceLod.Describe(r, false)} at {DeepTime.FormatYearsAgo(r.YearsAgo, DeepTime.NowYear)}, " +
                      $"rho {r.Rho:0.00}, wave 10^{r.ComovingLog10:0.00} m today, level {r.FinestLevel}, {1 / Math.Max(r.DecadesPerPixel, 1e-12):0.0} px a decade");
            if (!Finite(r.Log10MetresPerPixel)) yield return $"metres per pixel is not finite in the {view.Id} view ({r.Log10MetresPerPixel})";
            if (r.FinestLevel < 0 || !Finite(r.Log10WaveSpacing))
            {
                yield return $"no wave is fully open at the centre of the {view.Id} view " +
                             $"({1 / Math.Max(r.DecadesPerPixel, 1e-12):0.0} px a decade at wave 10^{r.ComovingLog10:0.00} m)";
            }
        }

        static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);

        void OnDestroy()
        {
            if (Current == this) Current = null;
            SpaceLod.Clear();
        }

        // ------------------------------------------------------------------ for the tests

        /// <summary>rho of the crest of the matter 10^<paramref name="log10Today"/> m from us today at arc u, as the grid draws it.</summary>
        public static float CrestRho(MatterLayout layout, float u, double log10Today)
        {
            Sample s = Take(layout, BoundBodies(layout), u, null);
            return s.RhoOf(s.Place(log10Today, out _));
        }

        /// <summary>The wave at (u, rho), as the grid draws it: log10 of the metres from us today of the matter there.</summary>
        public static double WaveAt(MatterLayout layout, float u, float rho)
        {
            Sample s = Take(layout, BoundBodies(layout), u, null);
            return s.Comoving(s.Log10At(rho), out _);
        }

        static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3 - 2 * t);
        }
    }
}
