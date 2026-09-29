using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Why.Matter
{
    /// <summary>
    /// The scale grid of the red layer: the physical map of <see cref="MatterScale"/> drawn as two families of
    /// lines, every one a real measurement.
    ///
    /// Shells: the loci where the distance from our lineage is a round number of metres (10^N, and the finer
    /// 2..9 x 10^N and 0.1 steps between them, as on a log ruler). A shell exists once the observable universe has
    /// grown past it, so it enters at the envelope's edge and settles inward as the fan keeps expanding around it;
    /// the ones inside bound bodies (Earth, the Sun, the Milky Way) hold still with them.
    ///
    /// Rays: comoving shells of matter, the matter that is 10^K metres from us today (and the finer steps), which
    /// was 10^K x a(t) metres away at every earlier time. They radiate from the Big Bang along the clock and fan
    /// out with the expansion of space, and they end where that matter fell into a gravitationally bound body
    /// (the Local Group and inward), so they survive only in the outer fan. Today the two families coincide.
    ///
    /// The exponential scale is what the map already had: between the knots (each lineage body's outer edge at
    /// its real radius; the envelope at the observable universe's radius, scaled back by the cosmic scale factor)
    /// the distance grows exponentially with rho. So the lines are spaced by orders of magnitude, crowd into the
    /// cusp at the Big Bang, and spread apart, thicker and fainter, as space opens; finer lines emerge as the
    /// camera closes in (tiers baked for coarser crowding thresholds, crossfaded by pixel density in Tick). The
    /// HUD's probe (G) reads the same field: metres from our lineage, metres per world unit, years per world unit.
    /// Neutral grey, far below the bloom threshold, beneath the matter surfaces. Builds its own layout because
    /// it prepares concurrently with <see cref="MatterLayer"/>.
    /// </summary>
    public sealed class ExpansionGridLayer : GraphLayer
    {
        /// <summary>Anchor of the grid: tooltips of its labels and the director.</summary>
        public const string AnchorKey = "matter:_expansion";

        /// <summary>Shared key of the <see cref="MatterScale"/> the grid was built from (the HUD's probe reads it).</summary>
        public const string ScaleKey = "matter.scale";

        /// <summary>Highlight id of the whole grid (the last matter id, clear of the bands and epochs).</summary>
        public static readonly int GridId = GraphIds.Matter(998);

        // --- what is drawn ---
        /// <summary>Decades of distance drawn, 10^0 .. 10^MaxDecade metres (the observable universe is 4.4e26 m).</summary>
        const int MaxDecade = 26;

        /// <summary>Relative weight of the three classes of line: decades, the 2..9 steps, the 0.1 steps.</summary>
        static readonly float[] ClassWeight = { 1f, 0.55f, 0.2f, 0.12f };

        /// <summary>The finest class (0.01 steps) is only generated from this decade outward, where the map can resolve it.</summary>
        const int HundredthsFromDecade = 21;

        /// <summary>The coarsest tier a class is baked into: the finer steps are details of the closer tiers.</summary>
        static readonly int[] ClassFirstTier = { 0, 0, 1, 2 };

        /// <summary>
        /// A decade line keeps this much of its opacity after the finer lines have taken over between it and
        /// the next decade, so the orders of magnitude stay readable; every other class fades out.
        /// </summary>
        const float DecadeFloor = 0.3f;

        /// <summary>Rays are a little stronger than shells: they are the matter, the shells the ruler.</summary>
        const float RayWeight = 1f, ShellWeight = 0.7f;

        // --- tiers: the same lines baked for a crowding threshold Zoom times finer per tier, crossfaded by pixel density ---
        public const int Tiers = 3;
        const float Zoom = 3f;

        /// <summary>World-unit gap below which a line is crowded out at the overview (~3 px at distance 22).</summary>
        const float CrowdGap = 0.05f;

        /// <summary>Pixel density (px per world unit) at which tier 0 is fully in; tier t at Zoom^t times more.</summary>
        const float OverviewPixelsPerUnit = 60f;

        /// <summary>A line is fully opaque once its gap is this many times the crowding threshold.</summary>
        const float EmergeRatio = 2.5f;

        /// <summary>Fainter as the gap opens beyond the threshold: opacity ~ (gap / crowd)^-Fainter.</summary>
        const float Fainter = 1.2f;

        /// <summary>World width of a line as a fraction of its gap, capped: wider as space opens.</summary>
        const float WidthPerGap = 0.05f, MaxWidthWorld = 0.08f, WidthPx = 1f;

        /// <summary>A shell fades in over this many decades after entering at the envelope; a ray fades out over this many decades before the bound edge.</summary>
        const double EnterDecades = 0.35, BoundDecades = 0.3;

        // --- look ---
        const float GridAlpha = 0.24f;
        const float GridIntensity = 0.7f;
        const float GridRhoFade = 30f;
        const float HandoffExponent = 2f;
        const float CullAlpha = 0.008f;

        // --- sampling along the clock (arc): geometric out of the Big Bang, then uniform ---
        const float FirstTau = 1e-5f, TauGrowth = 1.08f, DenseUntilTau = 0.02f, ArcStep = 0.002f;

        // --- labels ---
        static readonly int[] ShellLabelDecades = { 6, 9, 12, 15, 18, 21, 24, 26 };
        static readonly int[] RayLabelDecades = { 24, 26 };
        const float LabelPriority = 2.5f, LabelSize = 11f;

        const string Blurb =
            "A grid of real measurements. Each shell marks a round distance from our own lineage: ten metres, a " +
            "hundred, and so on, to the edge of the observable universe; each ray follows the matter that lies a " +
            "round distance from us today back in time, closer and closer to the Big Bang as space was smaller. " +
            "Between any two lines the distance grows tenfold, so the grid shows the exponential scale of the map: " +
            "space has stretched about 1,100-fold since the cosmic microwave background was released, 380,000 " +
            "years after the Big Bang, and it is still stretching faster today. Press G and point at the red layer " +
            "to read the scale anywhere: metres from us, and how many metres one unit of the graph stands for.";

        /// <summary>A sample of the clock with the map's knots at that moment.</summary>
        sealed class Sample
        {
            public float U, Envelope, ScaleFactor;
            public double Log10Universe, Log10Bound, Handoff;
            public MatterScale.Knot[] Knots;

            public double Log10Radius(float rho)
            {
                int i = Segment(rho);
                MatterScale.Knot a = Knots[i], b = Knots[i + 1];
                return a.Log10Radius + (rho - a.Rho) / (b.Rho - a.Rho) * (b.Log10Radius - a.Log10Radius);
            }

            /// <summary>rho of a distance, and the map's local decades per world unit there; false beyond the envelope.</summary>
            public bool Rho(double log10Radius, out float rho, out float decadesPerUnit)
            {
                rho = 0;
                decadesPerUnit = 0;
                if (log10Radius > Knots[Knots.Length - 1].Log10Radius) return false;
                for (int i = 0; i < Knots.Length - 1; i++)
                {
                    MatterScale.Knot a = Knots[i], b = Knots[i + 1];
                    if (log10Radius > b.Log10Radius) continue;
                    double span = b.Log10Radius - a.Log10Radius;
                    rho = a.Rho + (float)((log10Radius - a.Log10Radius) / span) * (b.Rho - a.Rho);
                    decadesPerUnit = (float)(span / (b.Rho - a.Rho));
                    return true;
                }

                return false;
            }

            int Segment(float rho)
            {
                int i = 0;
                while (i < Knots.Length - 2 && rho > Knots[i + 1].Rho) i++;
                return i;
            }
        }

        public override int Order => 6;
        public override IEnumerable<string> RequiredTexts => new[] { MatterLayer.DataPath };

        LineMeshBuilder[] tiers;
        Material[] materials;
        readonly float[] lod = new float[Tiers];
        MatterScale scale;

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
            scale = new MatterScale(layout);
            List<Sample> samples = SampleClock(layout);

            LineMeshBuilder[] builders = new LineMeshBuilder[Tiers];
            for (int t = 0; t < Tiers; t++) builders[t] = new LineMeshBuilder(32768);
            int shells = 0, rays = 0;
            Thinner thin = new Thinner();
            Trace trace = new Trace(samples.Count);
            foreach ((double log10, int cls, double step) in Values())
            {
                if (BuildLine(builders, thin, trace, samples, log10, cls, step, false)) shells++;
                // the rays mark matter, coarsely: decades and the 2..9 steps only
                if (cls <= 1 && BuildLine(builders, thin, trace, samples, log10, cls, step, true)) rays++;
            }

            Register(ctx, samples, layout.BigBangYa);
            ctx.Share(ScaleKey, scale);
            tiers = builders;

            string perTier = "";
            for (int t = 0; t < Tiers; t++) perTier += (t > 0 ? ", " : "") + "tier " + t + " " + builders[t].VertexCount;
            Debug.Log($"[Why] ExpansionGridLayer.Prepare {sw.ElapsedMilliseconds} ms: {samples.Count} samples, " +
                      $"{shells} shells and {rays} rays at the overview; vertices {perTier}");
        }

        /// <summary>All values drawn (log10 metres) with their class: decades, 2..9 steps, 0.1 steps.</summary>
        /// <summary>
        /// All values drawn (log10 metres) with their class and their spacing to the next line of the ruler
        /// (decades): the log ruler, one class per level: 10^n, 2..9 x 10^n, the 0.1 steps, and (out in the
        /// fan, where the map is wide enough for them to ever resolve) the 0.01 steps.
        /// </summary>
        static IEnumerable<(double log10, int cls, double step)> Values()
        {
            for (int n = 0; n <= MaxDecade; n++)
            {
                int finest = n >= HundredthsFromDecade ? 3 : 2;
                int steps = finest == 3 ? 1000 : 100;
                int unit = steps / 10;
                for (int m = unit; m < steps; m++)
                {
                    int cls = m == unit ? 0 : m % unit == 0 ? 1 : m % (unit / 10) == 0 ? 2 : 3;
                    // a decade is judged against the next decade; a finer line against its nearest neighbour
                    // of its own class or coarser
                    int coarse = cls == 1 ? unit : cls == 2 ? unit / 10 : cls == 3 ? 1 : steps;
                    double step = cls == 0 ? 1 : Math.Log10((m + coarse) / (double)m);
                    yield return (n + Math.Log10(m / (double)unit), cls, step);
                }
            }
        }

        List<Sample> SampleClock(MatterLayout layout)
        {
            List<Sample> samples = new List<Sample>(800);
            float top = layout.BigBangArc, bottom = MatterLayout.EndArc;
            float tau = FirstTau;
            while (true)
            {
                float u = top - tau;
                if (u <= bottom) break;
                samples.Add(Take(u));
                tau = tau < DenseUntilTau ? tau * TauGrowth : tau + ArcStep;
            }

            samples.Add(Take(bottom));
            return samples;
        }

        Sample Take(float u)
        {
            IReadOnlyList<MatterScale.Knot> knots = scale.Knots(u);
            MatterScale.Knot[] copy = new MatterScale.Knot[knots.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = knots[i];
            return new Sample
            {
                U = u,
                Envelope = scale.EnvelopeAt(u),
                ScaleFactor = Mathf.Max(scale is null ? 1f : LayoutScale(u), 1e-12f),
                Log10Universe = scale.Log10UniverseRadius(u),
                Log10Bound = scale.Log10BoundRadius(u),
                Handoff = Mathf.Pow(GraphStyle.HandoffFade(u), HandoffExponent),
                Knots = copy
            };
        }

        float LayoutScale(float u) => Mathf.Pow(10f, (float)(scale.Log10UniverseRadius(u) - Math.Log10(MatterScale.ObservableRadiusM)));

        /// <summary>
        /// One line of a family at one tier: walks the clock, placing the value on the map at every sample, and
        /// appends the stretches where it is resolvable, thinned to the points that shape them. Returns whether
        /// anything was drawn.
        /// </summary>
        static bool BuildLine(LineMeshBuilder[] builders, Thinner thin, Trace trace, List<Sample> samples, double log10, int cls,
            double step, bool ray)
        {
            float weight = ClassWeight[cls] * (ray ? RayWeight : ShellWeight);

            // the line's place on the map and everything about its opacity that does not depend on the tier
            for (int j = 0; j < samples.Count; j++)
            {
                Sample s = samples[j];
                double value = ray ? log10 + Math.Log10(s.ScaleFactor) : log10;
                trace.Gap[j] = 0;
                trace.Base[j] = 0;
                if (!s.Rho(value, out trace.Rho[j], out float decadesPerUnit) || decadesPerUnit <= 1e-9f) continue;
                float gap = (float)step / decadesPerUnit;
                float alpha = weight * (float)s.Handoff;
                if (ray)
                {
                    // the matter of a ray has fallen into a bound body once it lies inside the outermost one
                    if (s.Log10Bound >= 0) alpha *= Smooth((float)((value - s.Log10Bound) / BoundDecades));
                }
                else
                {
                    // a shell enters at the envelope as the universe grows past it
                    alpha *= Smooth((float)((s.Log10Universe - value) / EnterDecades));
                }

                trace.Gap[j] = gap;
                trace.Base[j] = alpha;
                trace.Width[j] = Mathf.Min(WidthPerGap * gap, MaxWidthWorld);
            }

            bool drawn = false;
            for (int t = 0; t < Tiers; t++)
            {
                if (t < ClassFirstTier[cls]) continue;
                float crowd = CrowdGap / Mathf.Pow(Zoom, t);
                thin.Reset();
                bool drawnHere = false;
                for (int j = 0; j < samples.Count; j++)
                {
                    float gap = trace.Gap[j];
                    float alpha = gap > 0 ? trace.Base[j] * Emerge(gap, crowd) * Faint(gap, crowd, cls) : 0;
                    if (alpha > CullAlpha)
                    {
                        thin.Add(samples[j].U, trace.Rho[j], alpha, trace.Width[j]);
                    }
                    else if (thin.Open)
                    {
                        // end the stretch softly on the first invisible sample
                        if (trace.Rho[j] > 0) thin.Add(samples[j].U, trace.Rho[j], 0, trace.Width[j]);
                        drawnHere |= thin.Flush(builders[t]);
                    }
                }

                drawnHere |= thin.Flush(builders[t]);
                if (t == 0) drawn = drawnHere;
            }

            return drawn;
        }

        /// <summary>One line's place and tier-independent opacity at every sample (scratch, reused per line).</summary>
        sealed class Trace
        {
            public readonly float[] Rho, Gap, Base, Width;

            public Trace(int samples)
            {
                Rho = new float[samples];
                Gap = new float[samples];
                Base = new float[samples];
                Width = new float[samples];
            }
        }

        /// <summary>
        /// Keeps only the samples that shape a stretch: a point is dropped while the stretch since the last kept
        /// point stays within a small deviation (in world-ish units: clock arc length and rho) and its opacity
        /// within a small step; the ends are always kept. Smooth lines shed most of their samples.
        /// </summary>
        sealed class Thinner
        {
            const float Deviation = 0.006f, AlphaStep = 0.035f, MaxSpan = 0.35f;

            struct Candidate
            {
                public float X, Rho, Alpha, Width, U;
            }

            readonly List<LinePoint> kept = new List<LinePoint>(256);
            readonly List<Candidate> pending = new List<Candidate>(64);
            Candidate last;

            public bool Open => kept.Count > 0;

            public void Reset()
            {
                kept.Clear();
                pending.Clear();
            }

            public void Add(float u, float rho, float alpha, float width)
            {
                Candidate c = new Candidate { X = (1f - u) * GraphWarp.BasePath.SigmaPerArc, Rho = rho, Alpha = alpha, Width = width, U = u };
                if (kept.Count == 0)
                {
                    Keep(c);
                    return;
                }

                // would the pending points still lie on the chord from the last kept point to this one?
                bool keepPrevious = false;
                if (pending.Count > 0)
                {
                    float dx = c.X - last.X, dr = c.Rho - last.Rho;
                    float len = Mathf.Sqrt(dx * dx + dr * dr);
                    if (len > MaxSpan) keepPrevious = true;
                    else
                    {
                        // long pending runs are straight by construction: checking every few points is enough
                        int stride = Mathf.Max(1, pending.Count / 12);
                        for (int i = pending.Count - 1; i >= 0; i -= stride)
                        {
                            Candidate p = pending[i];
                            float t = len > 1e-9f ? ((p.X - last.X) * dx + (p.Rho - last.Rho) * dr) / (len * len) : 0;
                            float ax = last.X + t * dx - p.X, ar = last.Rho + t * dr - p.Rho;
                            float chordAlpha = last.Alpha + t * (c.Alpha - last.Alpha);
                            if (ax * ax + ar * ar > Deviation * Deviation || Mathf.Abs(chordAlpha - p.Alpha) > AlphaStep)
                            {
                                keepPrevious = true;
                                break;
                            }
                        }
                    }
                }

                if (keepPrevious)
                {
                    Keep(pending[pending.Count - 1]);
                }

                pending.Add(c);
            }

            void Keep(Candidate c)
            {
                kept.Add(new LinePoint(new Vector3(c.U, GraphStyle.MatterY, c.Rho), Tint(c.Alpha), WidthPx, c.Width, 1f));
                last = c;
                pending.Clear();
            }

            public bool Flush(LineMeshBuilder builder)
            {
                if (pending.Count > 0) Keep(pending[pending.Count - 1]);
                bool ok = kept.Count >= 2;
                if (ok) builder.AddPolyline(kept, GridId);
                Reset();
                return ok;
            }
        }

        void Register(GraphContext ctx, List<Sample> samples, double bigBangYa)
        {
            Anchors.Register(new Anchor
            {
                Key = AnchorKey, Label = "Scale of space", Blurb = Blurb, Level = GraphLevel.Matter,
                YearsAgo = bigBangYa, EndYearsAgo = 0, Y = GraphStyle.MatterY, Rho = 0,
                Ids = IdRange.Single(GridId), Tier = 2
            });

            // shells are labeled where they have settled well inside the envelope; rays a little after the Big Bang
            foreach (int n in ShellLabelDecades)
            {
                Sample s = FindSample(samples, n, false);
                if (s != null && s.Rho(n, out float rho, out _)) AddLabel(ctx, "10<sup>" + n + "</sup> m", new Vector3(s.U, GraphStyle.MatterY, rho));
            }

            foreach (int k in RayLabelDecades)
            {
                Sample s = FindSample(samples, k, true);
                if (s != null && s.Rho(k + Math.Log10(s.ScaleFactor), out float rho, out _))
                {
                    AddLabel(ctx, "10<sup>" + k + "</sup> m today", new Vector3(s.U, GraphStyle.MatterY, rho));
                }
            }
        }

        /// <summary>The sample where a decade line is best labeled: a shell 1.5 decades inside the envelope, a ray at mid clock.</summary>
        static Sample FindSample(List<Sample> samples, int decade, bool ray)
        {
            if (ray)
            {
                Sample mid = samples[samples.Count / 2];
                return decade + Math.Log10(mid.ScaleFactor) > mid.Log10Bound ? mid : null;
            }

            foreach (Sample s in samples)
            {
                if (s.Log10Universe - decade >= 1.5) return s;
            }

            return null;
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

        public override void Upload(GraphContext ctx)
        {
            if (tiers == null) return;
            // neutral like the axes: hue is reserved for the levels, and grey reads as space, not as matter
            Color color = GraphStyle.Axis;
            color.a = GridAlpha;
            materials = new Material[Tiers];
            for (int t = 0; t < Tiers; t++)
            {
                // beneath the envelope and the band fills (QueueMatter: they veil it) and the matter lines
                Material m = GraphMaterials.Line(color, GridIntensity, GraphMaterials.QueueMatter - 1, true, GridRhoFade, 0f);
                GraphMaterials.FadeBeforeHumanBranch(m);
                lod[t] = t == 0 ? 1f : 0f;
                GraphMaterials.SetAlpha(m, lod[t]);
                materials[t] = m;
                AddMesh("MatterScaleGrid" + t, tiers[t].ToMesh("MatterScaleGrid" + t), m);
            }

            tiers = null;
        }

        /// <summary>Crossfades the tiers by pixel density: the tier baked for the current zoom carries the grid.</summary>
        public override void Tick(GraphContext ctx, CameraRig rig)
        {
            if (materials == null || rig == null || rig.Cam == null) return;
            float pixelsPerUnit = Screen.height / (2f * Mathf.Max(rig.Pose.Distance, 1e-3f) * Mathf.Tan(rig.Cam.fieldOfView * 0.5f * Mathf.Deg2Rad));
            float z = Mathf.Log(Mathf.Max(pixelsPerUnit / OverviewPixelsPerUnit, 1e-3f)) / Mathf.Log(Zoom);
            for (int t = 0; t < Tiers; t++)
            {
                float target = TierWeight(t, z);
                if (Mathf.Abs(target - lod[t]) < 0.01f) continue;
                lod[t] = target;
                GraphMaterials.SetAlpha(materials[t], target);
            }
        }

        /// <summary>Weight of tier t at zoom level z (log base Zoom of the pixel density over the overview's): hats that sum to 1.</summary>
        public static float TierWeight(int t, float z)
        {
            if (t == 0 && z <= 0) return 1f;
            if (t == Tiers - 1 && z >= t) return 1f;
            return Mathf.Clamp01(1f - Mathf.Abs(z - t));
        }

        /// <summary>Opacity of a line whose gap to its neighbours is <paramref name="gap"/>: 0 while crowded, 1 once EmergeRatio times the threshold.</summary>
        static float Emerge(float gap, float crowd) => Smooth(Mathf.Log(Mathf.Max(gap, 1e-9f) / crowd) / Mathf.Log(EmergeRatio));

        /// <summary>Fainter as the gap opens beyond the threshold: the widest-spaced lines are the softest.</summary>
        static float Faint(float gap, float crowd, int cls)
        {
            // a line is at its strongest just as it resolves; once its gap has opened far beyond that the finer
            // lines between it and its neighbours carry the grid, and it is redundant: it fades out fast
            // (a bell in log gap: a line lives in a band of about one zoom step around the resolution limit)
            float excess = Mathf.Log(Mathf.Max(gap / (crowd * EmergeRatio), 1f));
            float faint = Mathf.Exp(-Fainter * excess * excess);
            return cls == 0 ? Mathf.Max(faint, DecadeFloor) : faint;
        }

        static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3 - 2 * t);
        }

        static Color32 Tint(float alpha) => new Color32(255, 255, 255, (byte)Mathf.Clamp(alpha * 255f, 0, 255));
    }
}
