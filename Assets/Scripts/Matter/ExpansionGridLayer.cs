using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Why.Matter
{
    /// <summary>
    /// The scale of space in the red layer, drawn as rails that inflate as the clock turns: the one continuous field of
    /// <see cref="MatterScale"/>, with no knots, no cells and no step changes.
    ///
    /// Every rail is a comoving distance from our lineage - a power of ten metres today, and the finer 2..9, tenth and
    /// hundredth steps of a log ruler - followed back through time. The field holds the distances between the inner
    /// track, whose scale is our lineage's home at that moment (the universe at first, then the galaxy, the solar
    /// nebula, the Sun, the Earth by the time life begins), and the horizon, the observable universe carried by the
    /// cosmic scale factor. Early on the two coincide and every rail lies crushed on the inner track. As the universe
    /// inflates through its twenty-odd orders of magnitude around the first half of the clock, and the home scale
    /// descends, the rails peel off the inner track one after another - the outermost first - and are pushed outward,
    /// hyperbolically against the clock, toward the horizon, where they dissolve into black. Only along the inner track
    /// does the fine scale remain, descending to the local level.
    ///
    /// The rails branch like a tree: a finer rail is born on its parent (their cell is narrower than a pixel) and
    /// diverges as the cell inflates, so as the camera closes in the next, finer rails keep peeling open inside the
    /// last. Thin, additive, far below the bloom threshold; tiers baked for finer crowding thresholds are crossfaded by
    /// pixel density. The HUD's probe (G) reads the same field. Builds its own layout because it prepares concurrently
    /// with <see cref="MatterLayer"/>.
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
        /// <summary>Decades of distance drawn, 10^MinDecade (a kilometre, the finest) .. 10^MaxDecade metres (the observable universe is 4.4e26 m).</summary>
        const int MinDecade = 3, MaxDecade = 26;

        /// <summary>The hundredth steps are drawn up to this decade: the local scales, where the closest tier peels them open.</summary>
        const int HundredthsUntilDecade = 9;

        /// <summary>Relative weight of the four classes of rail: decades, the 2..9 steps, the tenths, the hundredths.</summary>
        static readonly float[] ClassWeight = { 1f, 0.6f, 0.3f, 0.16f };

        /// <summary>The coarsest tier a class is baked into: the finer steps are details of the closer tiers.</summary>
        static readonly int[] ClassFirstTier = { 0, 0, 1, 3 };

        /// <summary>A decade rail keeps this much of its opacity after the finer rails have taken over around it.</summary>
        const float DecadeFloor = 0.35f;

        // --- tiers: the same rails baked for a crowding threshold Zoom times finer per tier, crossfaded by pixel density ---
        public const int Tiers = 4;
        const float Zoom = 4f;

        /// <summary>World-unit gap below which a rail is still part of its parent at the overview (~3 px at distance 22).</summary>
        const float CrowdGap = 0.05f;

        /// <summary>Pixel density (px per world unit) at which tier 0 is fully in; tier t at Zoom^t times more.</summary>
        const float OverviewPixelsPerUnit = 60f;

        /// <summary>A rail is fully there once its gap is this many times the crowding threshold.</summary>
        const float EmergeRatio = 2.5f;

        /// <summary>Fainter as the gap opens beyond that: a rail is clearest just as it branches off.</summary>
        const float Fainter = 1.0f;

        /// <summary>World width of a rail as a fraction of its gap, capped; plus one pixel.</summary>
        const float WidthPerGap = 0.03f, MaxWidthWorld = 0.03f, WidthPx = 1f;

        /// <summary>A rail fades in over this many decades as it leaves the inner track.</summary>
        const double InnerDecades = 0.4;

        /// <summary>The rails dissolve over the outer part of the fan: from this fraction of the way to the horizon, out.</summary>
        const float HorizonFadeFrom = 0.62f;

        // --- look ---
        const float GridAlpha = 0.26f;
        const float GridIntensity = 0.75f;
        const float GridRhoFade = 0f;
        const float HandoffExponent = 2f;
        const float CullAlpha = 0.006f;

        // --- sampling along the clock (arc): geometric out of the Big Bang, then uniform ---
        const float FirstTau = 1e-5f, TauGrowth = 1.06f, DenseUntilTau = 0.02f, ArcStep = 0.0015f;

        // --- labels ---
        static readonly int[] LabelDecades = { 6, 9, 12, 15, 18, 21, 24, 26 };
        const float LabelPriority = 2.5f, LabelSize = 11f;

        const string Blurb =
            "The scale of space, drawn as rails that inflate with the universe. Each rail follows the matter that lies a " +
            "round distance from our own lineage today - a kilometre, ten, a hundred, and so on, to the edge of the " +
            "observable universe - back through time. The fan holds the distances between the inner track, whose scale " +
            "is our home at that moment (the universe itself at first, then the galaxy, the Sun, the Earth), and the " +
            "horizon, the observable universe. Early on the two coincide and every rail lies on the inner track; as " +
            "space inflates through more than twenty orders of magnitude the rails peel away one after another, outer " +
            "first, and are pushed toward the horizon, where they dissolve. Each finer rail branches off its parent as " +
            "their cell inflates, so the next scale keeps opening as you close in. Press G and point at the red layer " +
            "to read the scale anywhere: metres from us, and how many metres one unit of the graph stands for.";

        /// <summary>A sample of the clock: the field's two ends, its width, and the fade before the human branch.</summary>
        sealed class Sample
        {
            public float U, Envelope, UnitsPerDecade;
            public double Log10Scale, HomeLog10, OuterLog10, Handoff;
        }

        public override int Order => 6;
        public override IEnumerable<string> RequiredTexts => new[] { MatterLayer.DataPath };

        LineMeshBuilder[] tiers;
        Material[] materials;
        readonly float[] lod = new float[Tiers];
        MatterScale scale;
        MatterLayout layout;

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
            List<Sample> samples = SampleClock();

            LineMeshBuilder[] builders = new LineMeshBuilder[Tiers];
            for (int t = 0; t < Tiers; t++) builders[t] = new LineMeshBuilder(32768);
            int rails = 0;
            Thinner thin = new Thinner();
            Trace trace = new Trace(samples.Count);
            Trace[] labelled = new Trace[MaxDecade + 1];
            foreach ((double log10, int cls, double step) in Values())
            {
                if (!BuildRail(builders, thin, trace, samples, log10, cls, step)) continue;
                rails++;
                if (cls == 0) labelled[(int)Math.Round(log10)] = trace.Copy();
            }

            Register(ctx, samples, labelled, layout.BigBangYa);
            ctx.Share(ScaleKey, scale);
            tiers = builders;

            string perTier = "";
            for (int t = 0; t < Tiers; t++) perTier += (t > 0 ? ", " : "") + "tier " + t + " " + builders[t].VertexCount;
            Debug.Log($"[Why] ExpansionGridLayer.Prepare {sw.ElapsedMilliseconds} ms: {samples.Count} samples, {rails} rails; vertices {perTier}; " +
                      $"inner track 10^{scale.HomeLog10(DeepTime.Arc(4e9)):0.0} m when life begins, 10^{scale.HomeLog10(MatterLayout.EndArc):0.0} m at the end, " +
                      $"{scale.UnitsPerDecade(0):0.00} units per decade");
        }

        /// <summary>
        /// All values drawn (log10 metres today) with their class and their spacing to the nearest rail of their own
        /// class or coarser (decades): the log ruler, one class per level - 10^n, 2..9 x 10^n, the tenths, the
        /// hundredths. Ascending.
        /// </summary>
        static IEnumerable<(double log10, int cls, double step)> Values()
        {
            for (int n = MinDecade; n <= MaxDecade; n++)
            {
                const int steps = 1000, unit = 100;
                int finest = n <= HundredthsUntilDecade ? 3 : 2;
                for (int m = unit; m < steps; m++)
                {
                    int cls = m == unit ? 0 : m % unit == 0 ? 1 : m % 10 == 0 ? 2 : 3;
                    if (cls > finest) continue;
                    int coarse = cls == 1 ? unit : cls == 2 ? 10 : cls == 3 ? 1 : steps;
                    double step = cls == 0 ? 1 : Math.Log10((m + coarse) / (double)m);
                    yield return (n + Math.Log10(m / (double)unit), cls, step);
                }
            }
        }

        List<Sample> SampleClock()
        {
            List<Sample> samples = new List<Sample>(1200);
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
            return new Sample
            {
                U = u,
                Envelope = scale.EnvelopeAt(u),
                UnitsPerDecade = (float)scale.UnitsPerDecade(u),
                Log10Scale = Math.Log10(Mathf.Max(layout.ScaleFactor(u), 1e-30f)),
                HomeLog10 = scale.HomeLog10(u),
                OuterLog10 = scale.OuterLog10(u),
                Handoff = Mathf.Pow(GraphStyle.HandoffFade(u), HandoffExponent)
            };
        }

        /// <summary>
        /// One rail at every tier: walks the clock, placing the matter that is 10^log10 metres from us today in the
        /// field at every sample, and appends the stretches where it has branched off its parent, thinned to the points
        /// that shape them. Returns whether anything was drawn.
        /// </summary>
        static bool BuildRail(LineMeshBuilder[] builders, Thinner thin, Trace trace, List<Sample> samples, double log10, int cls, double step)
        {
            float weight = ClassWeight[cls];

            // the rail's place in the field and everything about its opacity that does not depend on the tier
            for (int j = 0; j < samples.Count; j++)
            {
                Sample s = samples[j];
                trace.Gap[j] = 0;
                trace.Base[j] = 0;
                double pos = log10 + s.Log10Scale;               // comoving: its distance now
                double above = pos - s.HomeLog10;                // decades above the inner track
                if (above <= 0 || s.UnitsPerDecade <= 1e-9f) continue;
                float rho = (float)(above * s.UnitsPerDecade);
                if (rho > s.Envelope) continue;
                float gap = (float)step * s.UnitsPerDecade;
                // born on the inner track; dissolving toward the horizon, where space has stretched most
                float horizon = 1f - Smooth((rho / Mathf.Max(s.Envelope, 1e-6f) - HorizonFadeFrom) / (1f - HorizonFadeFrom));
                float alpha = weight * (float)s.Handoff * Smooth((float)(above / InnerDecades)) * horizon;
                trace.Rho[j] = rho;
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
                        if (gap > 0) thin.Add(samples[j].U, trace.Rho[j], 0, trace.Width[j]);
                        drawnHere |= thin.Flush(builders[t]);
                    }
                }

                drawnHere |= thin.Flush(builders[t]);
                drawn |= drawnHere;
            }

            return drawn;
        }

        /// <summary>One rail's place and tier-independent opacity at every sample (scratch, reused per rail).</summary>
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

            public Trace Copy()
            {
                Trace c = new Trace(Rho.Length);
                Array.Copy(Rho, c.Rho, Rho.Length);
                Array.Copy(Gap, c.Gap, Gap.Length);
                Array.Copy(Base, c.Base, Base.Length);
                Array.Copy(Width, c.Width, Width.Length);
                return c;
            }
        }

        /// <summary>
        /// Keeps only the samples that shape a stretch: a point is dropped while the stretch since the last kept
        /// point stays within a small deviation (in world-ish units: clock arc length and rho) and its opacity
        /// within a small step; the ends are always kept. Smooth rails shed most of their samples.
        /// </summary>
        sealed class Thinner
        {
            const float Deviation = 0.004f, AlphaStep = 0.03f, MaxSpan = 0.3f;

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

                if (keepPrevious) Keep(pending[pending.Count - 1]);
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

        void Register(GraphContext ctx, List<Sample> samples, Trace[] labelled, double bigBangYa)
        {
            Anchors.Register(new Anchor
            {
                Key = AnchorKey, Label = "Scale of space", Blurb = Blurb, Level = GraphLevel.Matter,
                YearsAgo = bigBangYa, EndYearsAgo = 0, Y = GraphStyle.MatterY, Rho = 0,
                Ids = IdRange.Single(GridId), Tier = 2
            });

            // decade rails are labeled near the present, where they have settled at their distance today
            foreach (int n in LabelDecades)
            {
                if (n > MaxDecade || labelled[n] == null) continue;
                Trace rail = labelled[n];
                for (int j = samples.Count - 1; j >= 0; j--)
                {
                    if (rail.Base[j] < 0.3f) continue;
                    AddLabel(ctx, "10<sup>" + n + "</sup> m", new Vector3(samples[j].U, GraphStyle.MatterY, rail.Rho[j]));
                    break;
                }
            }
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
                // above the band fills (which would veil the fine rails up close), additive with the band edges
                Material m = GraphMaterials.Line(color, GridIntensity, GraphMaterials.QueueMatter + 1, true, GridRhoFade, 0f);
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
            float pixelsPerUnit = rig.Cam.pixelHeight / (2f * Mathf.Max(rig.Pose.Distance, 1e-3f) * Mathf.Tan(rig.Cam.fieldOfView * 0.5f * Mathf.Deg2Rad));
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

        /// <summary>Opacity of a rail whose gap to its parent is <paramref name="gap"/>: 0 while within it, 1 once EmergeRatio times the threshold.</summary>
        static float Emerge(float gap, float crowd) => Smooth(Mathf.Log(Mathf.Max(gap, 1e-9f) / crowd) / Mathf.Log(EmergeRatio));

        /// <summary>Fainter as the gap opens beyond the threshold: a rail is clearest just as it branches off, and the finer rails carry the scale after.</summary>
        static float Faint(float gap, float crowd, int cls)
        {
            // the finest rails have nothing finer to hand over to: they stay
            if (cls == ClassWeight.Length - 1) return 1f;
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
