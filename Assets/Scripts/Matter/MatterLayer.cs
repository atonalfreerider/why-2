using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using TMPro;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Why.Matter
{
    /// <summary>
    /// RED layer: all the matter in the universe, from the Big Bang (6 o'clock) clockwise around the clock
    /// to 3 o'clock, where the straight human branch leaves the circle. The inner track is our current home
    /// (universe, Milky Way, solar nebula, Sun, Earth); other matter splits off outward along the way, and
    /// the rest of the universe fills an envelope that expands exponentially and dissipates underneath the
    /// life and human layers. Ahead of the handoff the bulk dissolves first and Earth last, so matter is
    /// gone by the time the human civilizations begin.
    ///
    /// Built on a worker thread (<see cref="MatterLayout"/>) into three meshes: band fills, the expanding
    /// envelope and the lines (band edges, our lineage with the causal flow pulse, the Big Bang, epochs).
    /// </summary>
    public sealed class MatterLayer : GraphLayer
    {
        /// <summary>Resources path of matter.json (the bands of matter and the epochs).</summary>
        public const string DataPath = "Data/matter";

        // lineage (our path along the inner edge)
        const float LineageWidthPx = 2.2f;
        const float LineageWidthWorld = 0.003f;
        const float LineageIntensity = 2.2f;

        // Big Bang burst
        const int BurstRays = 36;
        const float BurstIntensity = 4.5f;

        /// <summary>A ray reaches full brightness 1 / BurstIgnite of the way out.</summary>
        const float BurstIgnite = 4f;

        // epochs: ticks across the inner edge, labels inside the ring
        const float EpochRho = -0.08f;
        const float TickInner = -0.045f;
        const float TickOuter = 0.02f;

        /// <summary>Epochs closer than this arc share one spot on the clock; their labels are stacked.</summary>
        const float EpochClusterArc = 0.0015f;

        /// <summary>Vertical spacing (px at 1080p) of stacked epoch labels.</summary>
        const float EpochLabelSpacing = 32f;

        /// <summary>First highlight id index of the epoch ticks (after the bands).</summary>
        const int EpochIdBase = 100;

        /// <summary>Last matter id index: matter owns the ids 1..999 (<see cref="GraphIds.Matter"/>).</summary>
        const int MaxMatterIndex = 998;

        // item labels: the universe and the cosmic web are labeled out in the envelope, this far along
        const float UniverseLabelArc = 0.22f;
        const float EnvelopeItemLabelArc = 0.04f;

        /// <summary>A band is labeled at least this far (arc) after it starts to form.</summary>
        const float BandLabelArc = 0.0012f;

        // envelope rendering and level of detail
        const float EnvelopeRhoFade = 6f;
        const float EnvelopeNoiseScale = 1.1f;
        const float EnvelopeIntensity = 1.1f;
        const float EnvelopeAlphaClose = 0.4f;
        const float EnvelopeLodNear = 1.5f;
        const float EnvelopeLodFar = 9f;

        /// <summary>Persistent glow of our lineage.</summary>
        const float LineageGlow = 1.4f;

        public override int Order => 5;
        public override IEnumerable<string> RequiredTexts => new[] { DataPath };

        /// <summary>The layout (read-only once prepared); null if matter.json is missing.</summary>
        public MatterLayout Layout { get; private set; }

        SurfaceMeshBuilder fills, envelope;
        LineMeshBuilder lines;
        Material fillMat, envelopeMat, lineMat;
        float envelopeLod = -1;

        /// <summary>Layout evaluated at every sample: per band inner / outer edge, and the envelope.</summary>
        sealed class Sampled
        {
            public List<float> U;
            public float[][] Inner, Outer;
            public float[] StackOuter, Envelope;
        }

        public override void Prepare(GraphContext ctx)
        {
            Stopwatch sw = Stopwatch.StartNew();
            MatterFile file = MatterFile.Parse(ctx.Text(DataPath), out string error);
            if (file == null)
            {
                Debug.LogWarning("[Why] MatterLayer: " + error);
                return;
            }

            MatterLayout layout = MatterLayout.Build(file);
            if (layout.IsEmpty)
            {
                Debug.LogWarning("[Why] MatterLayer: matter.json has no items");
                return;
            }

            Sampled s = Sample(layout);
            fills = new SurfaceMeshBuilder();
            envelope = new SurfaceMeshBuilder();
            lines = new LineMeshBuilder(6_000);

            BuildFills(layout, s);
            BuildEnvelope(layout, s);
            BuildEdges(layout, s);
            BuildLineage(layout, s);
            BuildBurst(layout);
            List<EpochMark> marks = PlaceEpochs(layout, file);
            BuildEpochTicks(marks);

            RegisterItems(ctx, layout);
            RegisterEpochs(ctx, marks);

            Layout = layout;
            ctx.Share("matter.layer", this);
            Debug.Log($"[Why] MatterLayer.Prepare {sw.ElapsedMilliseconds} ms: {layout.Bands.Count} bands, " +
                      $"{s.U.Count} samples, {fills.VertexCount + envelope.VertexCount} fill + " +
                      $"{lines.VertexCount} line vertices");
        }

        static Sampled Sample(MatterLayout layout)
        {
            List<float> us = layout.Samples();
            int n = layout.Bands.Count, m = us.Count;
            Sampled s = new Sampled
            {
                U = us,
                Inner = new float[n][],
                Outer = new float[n][],
                StackOuter = new float[m],
                Envelope = new float[m]
            };
            for (int b = 0; b < n; b++)
            {
                s.Inner[b] = new float[m];
                s.Outer[b] = new float[m];
            }

            float[] inner = new float[n], outer = new float[n];
            for (int j = 0; j < m; j++)
            {
                layout.Evaluate(us[j], inner, outer, out s.StackOuter[j], out s.Envelope[j]);
                for (int b = 0; b < n; b++)
                {
                    s.Inner[b][j] = inner[b];
                    s.Outer[b][j] = outer[b];
                }
            }

            return s;
        }

        /// <summary>One filled band per body of matter over its lifetime.</summary>
        void BuildFills(MatterLayout layout, Sampled s)
        {
            float y = GraphStyle.MatterY;
            List<Vector3> inner = new List<Vector3>(s.U.Count), outer = new List<Vector3>(s.U.Count);
            List<Color32> cols = new List<Color32>(s.U.Count);
            foreach (MatterBand band in layout.Bands)
            {
                inner.Clear();
                outer.Clear();
                cols.Clear();
                for (int j = 0; j < s.U.Count; j++)
                {
                    float u = s.U[j];
                    if (!band.AliveAt(u)) continue;
                    inner.Add(new Vector3(u, y, s.Inner[band.Index][j]));
                    outer.Add(new Vector3(u, y, s.Outer[band.Index][j]));
                    cols.Add(Tint(band.FillAlpha * MatterLayout.HandoffBias(u, band.HandoffExponent)));
                }

                fills.AddBand(inner, outer, cols, band.Id, band.FillIntensity, band.Noise);
            }
        }

        /// <summary>
        /// The rest of the universe beyond the stack, as radial strips that thin out and break up into
        /// wisps outward. It belongs to the universe until the intergalactic gas (the cosmic web) forms.
        /// </summary>
        void BuildEnvelope(MatterLayout layout, Sampled s)
        {
            int m = s.U.Count;
            int split = m;
            if (layout.EnvelopeItem != null)
            {
                split = 0;
                while (split < m && s.U[split] > layout.EnvelopeItemArc) split++;
            }

            for (int k = 0; k < MatterLayout.EnvelopeStrips; k++)
            {
                // before the split sample the envelope is the universe itself, from it on the envelope item
                AddEnvelopeStrip(s, k, 0, Mathf.Min(split, m - 1), layout.UniverseEnvelopeId);
                if (split < m) AddEnvelopeStrip(s, k, split, m - 1, layout.EnvelopeItemId);
            }
        }

        /// <summary>Envelope strip k over the samples [from, to].</summary>
        void AddEnvelopeStrip(Sampled s, int k, int from, int to, int id)
        {
            float f0 = MatterLayout.StripEdge(k), f1 = MatterLayout.StripEdge(k + 1);
            // each strip blends into the next, and the outermost reaches exactly zero: the universe fans out
            // to transparent black with no border
            float alphaIn = MatterLayout.StripEdgeAlpha(k), alphaOut = MatterLayout.StripEdgeAlpha(k + 1);
            int count = to - from + 1;
            List<Vector3> inner = new List<Vector3>(count), outer = new List<Vector3>(count);
            List<Color32> colsIn = new List<Color32>(count), colsOut = new List<Color32>(count);
            for (int j = from; j <= to; j++)
            {
                float so = s.StackOuter[j], width = s.Envelope[j] - so;
                inner.Add(new Vector3(s.U[j], GraphStyle.MatterY, so + width * f0));
                outer.Add(new Vector3(s.U[j], GraphStyle.MatterY, so + width * f1));
                float a = MatterLayout.EnvelopeAlpha(s.U[j], so, s.Envelope[j]);
                colsIn.Add(Tint(a * alphaIn));
                colsOut.Add(Tint(a * alphaOut));
            }

            envelope.AddBand(inner, outer, colsIn, colsOut, id, EnvelopeIntensity, MatterLayout.StripNoise(k));
        }

        /// <summary>Thin red edge lines between the bands (brighter for our lineage).</summary>
        void BuildEdges(MatterLayout layout, Sampled s)
        {
            float y = GraphStyle.MatterY;
            List<LinePoint> pts = new List<LinePoint>(s.U.Count);
            foreach (MatterBand band in layout.Bands)
            {
                pts.Clear();
                for (int j = 0; j < s.U.Count; j++)
                {
                    float u = s.U[j];
                    if (!band.AliveAt(u)) continue;
                    float inner = s.Inner[band.Index][j], outer = s.Outer[band.Index][j];
                    // a band squeezed to nothing (forming, ending, or covered by its children) loses its edge
                    float full = 0.3f * band.Width * layout.StackScale(u);
                    float presence = full > 1e-6f ? Mathf.Clamp01((outer - inner) / full) : 0;
                    float alpha = band.LineAlpha * presence * MatterLayout.HandoffBias(u, band.HandoffExponent);
                    pts.Add(new LinePoint(new Vector3(u, y, outer), Tint(alpha), band.LineWidthPx, 0,
                        band.LineIntensity));
                }

                lines.AddPolyline(pts, band.Id);
            }
        }

        /// <summary>
        /// Our lineage along the inner edge of the clock, from the Big Bang to the handoff: one segment per
        /// home (universe, galaxy, solar nebula, Sun, Earth), bright and carrying the causal flow pulse.
        /// </summary>
        void BuildLineage(MatterLayout layout, Sampled s)
        {
            float y = GraphStyle.MatterY;
            float end = MatterLayout.EndArc;
            List<LinePoint> pts = new List<LinePoint>(s.U.Count);
            for (int p = 0; p < layout.Path.Count; p++)
            {
                float from = p == 0 ? layout.BigBangArc : DeepTime.Arc(layout.Path[p].StartYa);
                float to = p + 1 < layout.Path.Count ? DeepTime.Arc(layout.Path[p + 1].StartYa) : end;
                from = Mathf.Max(from, end);
                to = Mathf.Max(to, end);
                if (from <= to) continue;
                pts.Clear();
                pts.Add(LineagePoint(from, y));
                foreach (float u in s.U)
                {
                    if (u < from && u > to) pts.Add(LineagePoint(u, y));
                }

                pts.Add(LineagePoint(to, y));
                lines.AddPolyline(pts, layout.BandOf(layout.Path[p]).Id, 1f);
            }
        }

        static LinePoint LineagePoint(float u, float y) =>
            new LinePoint(new Vector3(u, y, 0), Tint(1), LineageWidthPx, LineageWidthWorld, LineageIntensity);

        /// <summary>
        /// A radiant burst at 6 o'clock where the clock and every band begin. Rays are laid out in world
        /// units around the Big Bang (converted to arc with the circle's own scale, so they stay round).
        /// </summary>
        void BuildBurst(MatterLayout layout)
        {
            System.Random rng = new System.Random(13787);
            float u0 = layout.BigBangArc;
            float arcPerUnit = 1f / GraphWarp.BasePath.SigmaPerArc;
            List<LinePoint> pts = new List<LinePoint>(8);
            for (int k = 0; k < BurstRays; k++)
            {
                float theta = 2f * Mathf.PI * (k + 0.6f * (float)rng.NextDouble()) / BurstRays;
                float length = 0.05f + 0.35f * Mathf.Pow((float)rng.NextDouble(), 2f);
                pts.Clear();
                const int steps = 6;
                for (int i = 0; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    float r = length * (0.04f + 0.96f * t);
                    Vector3 p = new Vector3(u0 + r * Mathf.Cos(theta) * arcPerUnit, GraphStyle.MatterY,
                        r * Mathf.Sin(theta));
                    float fade = 1 - t;
                    // rays ignite just off the center: dozens of additive rays piled up at one point would
                    // saturate every channel and bloom into a white blob instead of a red burst
                    float ignite = Mathf.Clamp01(t * BurstIgnite);
                    pts.Add(new LinePoint(p, Tint(ignite * fade * fade), 0.5f + 1.7f * fade, 0, BurstIntensity));
                }

                lines.AddPolyline(pts, layout.BurstId);
            }
        }

        /// <summary>Where an epoch sits on the clock, its highlight ids and its label offset.</summary>
        sealed class EpochMark
        {
            public MatterEpoch Epoch;
            public float U;
            public IdRange Ids;
            public bool AtBigBang;
            public float LabelOffsetY;
        }

        /// <summary>
        /// Epochs the clock cannot separate (the first moments after the Big Bang, the birth of the solar
        /// system) share one spot: their labels are stacked in time order instead of hiding each other.
        /// </summary>
        static List<EpochMark> PlaceEpochs(MatterLayout layout, MatterFile file)
        {
            List<EpochMark> marks = new List<EpochMark>(file.Epochs.Count);
            // epoch ids follow the band ids (never overlapping them) and stay inside the matter range
            int idBase = Math.Max(EpochIdBase, layout.AllIds.Max - GraphIds.MatterBase + 1);
            for (int e = 0; e < file.Epochs.Count; e++)
            {
                MatterEpoch epoch = file.Epochs[e];
                float u = DeepTime.Arc(epoch.Ya);
                bool atBigBang = layout.BigBangArc - u < EpochClusterArc;
                int id = idBase + e <= MaxMatterIndex ? GraphIds.Matter(idBase + e) : GraphIds.None;
                marks.Add(new EpochMark
                {
                    Epoch = epoch,
                    U = u,
                    AtBigBang = atBigBang,
                    Ids = IdRange.Single(atBigBang ? layout.BurstId : id)
                });
            }

            List<EpochMark> sorted = new List<EpochMark>(marks);
            sorted.Sort((a, b) =>
            {
                int c = b.U.CompareTo(a.U);
                return c != 0 ? c : (a.Epoch.AfterBigBangYears ?? 0).CompareTo(b.Epoch.AfterBigBangYears ?? 0);
            });

            for (int start = 0; start < sorted.Count;)
            {
                int end = start + 1;
                while (end < sorted.Count && sorted[end - 1].U - sorted[end].U < EpochClusterArc) end++;
                int count = end - start;
                for (int k = 0; k < count; k++) sorted[start + k].LabelOffsetY = EpochLabelSpacing * (count - 1 - k);
                start = end;
            }

            return marks;
        }

        void BuildEpochTicks(List<EpochMark> marks)
        {
            foreach (EpochMark mark in marks)
            {
                if (mark.AtBigBang) continue; // the burst marks these
                int tier = Mathf.Clamp(mark.Epoch.Tier, 1, 3);
                float alpha = tier == 1 ? 0.7f : tier == 2 ? 0.5f : 0.35f;
                lines.AddSegment(new Vector3(mark.U, GraphStyle.MatterY, TickInner),
                    new Vector3(mark.U, GraphStyle.MatterY, TickOuter), Tint(alpha), 1.2f, 0, mark.Ids.Min, 1.3f);
            }
        }

        static void RegisterItems(GraphContext ctx, MatterLayout layout)
        {
            float[] inner = new float[layout.Bands.Count], outer = new float[layout.Bands.Count];
            float y = GraphStyle.MatterY;

            // the universe: anchored at the Big Bang, labeled out in its expanding envelope
            MatterItem root = layout.Root;
            Anchor universe = new Anchor
            {
                Key = "matter:" + root.Id, Label = root.DisplayName, Blurb = root.Blurb, Level = GraphLevel.Matter,
                YearsAgo = root.StartYa, EndYearsAgo = Math.Max(0, root.EndYa), Y = y, Rho = 0,
                Ids = layout.AllIds, Tier = 1
            };
            Anchors.Register(universe);
            float uUniverse = layout.BigBangArc - UniverseLabelArc;
            layout.Evaluate(uUniverse, inner, outer, out float so, out float env);
            AddItemLabel(ctx, universe, new Vector3(uUniverse, y, so + 0.4f * (env - so)),
                new IdRange(layout.BurstId, layout.RootHome.Id), 36, 14, GraphStyle.Text);

            // the envelope item (intergalactic gas): the envelope once it forms. Anchored where it takes over
            // the envelope, labeled a little later where the envelope has widened
            MatterItem gas = layout.EnvelopeItem;
            if (gas != null)
            {
                layout.Evaluate(layout.EnvelopeItemArc, inner, outer, out so, out env);
                Anchor a = new Anchor
                {
                    Key = "matter:" + gas.Id, Label = gas.DisplayName, Blurb = gas.Blurb, Level = GraphLevel.Matter,
                    YearsAgo = gas.StartYa, EndYearsAgo = 0, Y = y, Rho = so + 0.3f * (env - so),
                    Ids = layout.IdsOf(gas), Tier = 2
                };
                Anchors.Register(a);
                float uGas = layout.EnvelopeItemArc - EnvelopeItemLabelArc;
                layout.Evaluate(uGas, inner, outer, out so, out env);
                AddItemLabel(ctx, a, new Vector3(uGas, y, so + 0.3f * (env - so)), a.Ids, 16, 12, GraphStyle.TextDim);
            }

            // stacked bands: labeled where they have formed, anchored at their start
            foreach (MatterBand band in layout.Bands)
            {
                if (band.IsRootHome) continue;
                MatterItem item = band.Item;
                float u = Mathf.Min(band.UFull, band.UStart - BandLabelArc);
                if (!band.Extant) u = Mathf.Max(u, 0.5f * (band.UFull + band.UDecay));
                layout.Evaluate(u, inner, outer, out _, out _);
                float rho = 0.5f * (inner[band.Index] + outer[band.Index]);
                Anchor a = new Anchor
                {
                    Key = "matter:" + item.Id, Label = item.DisplayName, Blurb = item.Blurb, Level = GraphLevel.Matter,
                    YearsAgo = item.StartYa, EndYearsAgo = Math.Max(0, item.EndYa), Y = y, Rho = rho,
                    Ids = band.Ids, Tier = band.InPath ? 1 : 2
                };
                Anchors.Register(a);
                float priority = band.IsHomeNow ? 46 : band.InPath ? 36 : 16;
                AddItemLabel(ctx, a, new Vector3(u, y, rho), IdRange.Single(band.Id), priority, band.InPath ? 14 : 12,
                    band.InPath ? GraphStyle.Text : GraphStyle.TextDim);
            }

            // the whole lineage, for the director
            List<string> names = new List<string>(layout.Path.Count);
            foreach (MatterItem item in layout.Path) names.Add(item.DisplayName);
            Anchors.Register(new Anchor
            {
                Key = "matter:_lineage", Label = "Our cosmic lineage",
                Blurb = "The matter that became us: " + string.Join(", then ", names) + ".",
                Level = GraphLevel.Matter, YearsAgo = root.StartYa, EndYearsAgo = 0, Y = y, Rho = 0,
                Ids = layout.PathIds, Tier = 1
            });
        }

        /// <summary>Item label; it brightens only when the item's own geometry is highlighted.</summary>
        static void AddItemLabel(GraphContext ctx, Anchor a, Vector3 data, IdRange ownIds, float priority, float size,
            Color color)
        {
            ctx.Labels.Add(new LabelSpec
            {
                Text = a.Label,
                Data = data,
                Priority = priority,
                SizePx = size,
                Color = color,
                PixelOffset = new Vector2(6, 8),
                AnchorKey = a.Key,
                HandoffFade = true,
                Ids = ownIds
            });
        }

        static void RegisterEpochs(GraphContext ctx, List<EpochMark> marks)
        {
            foreach (EpochMark mark in marks)
            {
                MatterEpoch e = mark.Epoch;
                int tier = Mathf.Clamp(e.Tier, 1, 3);
                Anchor a = new Anchor
                {
                    Key = "epoch:" + e.Id, Label = e.DisplayName, Blurb = e.Blurb, Level = GraphLevel.Matter,
                    YearsAgo = e.Ya, EndYearsAgo = e.Ya, Y = GraphStyle.MatterY, Rho = EpochRho, Ids = mark.Ids,
                    Tier = tier
                };
                Anchors.Register(a);

                // moments the clock cannot resolve say how long after the Big Bang they happened
                double after = e.AfterBigBangYears ?? 0;
                string text = mark.AtBigBang && after > 0 ? a.Label + ", " + SinceBigBang(after) : a.Label;
                ctx.Labels.Add(new LabelSpec
                {
                    Text = text,
                    Data = new Vector3(mark.U, GraphStyle.MatterY, EpochRho),
                    Priority = tier == 1 ? 28 : tier == 2 ? 12 : 5,
                    SizePx = tier == 1 ? 13 : tier == 2 ? 12 : 11,
                    Color = GraphStyle.TextDim,
                    Align = TextAlignmentOptions.Right,
                    PixelOffset = new Vector2(-6, mark.LabelOffsetY),
                    AnchorKey = a.Key,
                    Ids = a.Ids
                });
            }
        }

        /// <summary>Time after the Big Bang for a label, e.g. "10^-36 s", "3 min", "380,000 years".</summary>
        static string SinceBigBang(double years)
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            double seconds = years * 3.15576e7;
            if (seconds < 0.1) return "10<sup>" + Math.Round(Math.Log10(seconds)).ToString("0", c) + "</sup> s";
            if (seconds < 120) return seconds.ToString(seconds < 10 ? "0.#" : "0", c) + " s";
            if (seconds < 7200) return (seconds / 60).ToString("0", c) + " min";
            if (seconds < 172800) return (seconds / 3600).ToString("0", c) + " hours";
            if (years < 1) return (seconds / 86400).ToString("0", c) + " days";
            if (years < 1e6) return years.ToString("#,0", c) + " years";
            return (years / 1e6).ToString("0.#", c) + " million years";
        }

        public override void Upload(GraphContext ctx)
        {
            if (lines == null) return;

            envelopeMat = GraphMaterials.Surface(GraphStyle.Matter, 1f, GraphMaterials.QueueMatter, false,
                EnvelopeRhoFade, EnvelopeNoiseScale);
            envelopeMat.SetFloat("_EdgeSoft", 0f);
            envelopeMat.SetFloat("_NoiseContrast", 2f);
            // the clock ends at 3 o'clock: matter dissolves before the straight human branch
            GraphMaterials.FadeBeforeHumanBranch(envelopeMat);
            AddMesh("MatterEnvelope", envelope.ToMesh("MatterEnvelope"), envelopeMat);

            fillMat = GraphMaterials.Surface(GraphStyle.Matter, 1f, GraphMaterials.QueueMatter, false, 0f, 3f);
            fillMat.SetFloat("_EdgeSoft", 0.22f);
            GraphMaterials.FadeBeforeHumanBranch(fillMat);
            AddMesh("MatterBands", fills.ToMesh("MatterBands"), fillMat);

            lineMat = GraphMaterials.Line(GraphStyle.Matter, 1f, GraphMaterials.QueueMatter + 1, true, 0f, 1f);
            GraphMaterials.FadeBeforeHumanBranch(lineMat);
            AddMesh("MatterLines", lines.ToMesh("MatterLines"), lineMat);

            Highlighter.SetPersistent("matter", new[] { (Layout.PathIds, LineageGlow) });

            fills = null;
            envelope = null;
            lines = null;
        }

        /// <summary>Level of detail: the wide envelope recedes when the camera comes close.</summary>
        public override void Tick(GraphContext ctx, CameraRig rig)
        {
            if (envelopeMat == null || rig == null) return;
            float t = Mathf.InverseLerp(EnvelopeLodNear, EnvelopeLodFar, rig.Pose.Distance);
            float lod = Mathf.Lerp(EnvelopeAlphaClose, 1f, t * t * (3 - 2 * t));
            if (Mathf.Abs(lod - envelopeLod) < 0.01f) return;
            envelopeLod = lod;
            GraphMaterials.SetAlpha(envelopeMat, lod);
        }

        static Color32 Tint(float alpha) => new Color32(255, 255, 255, (byte)Mathf.Clamp(alpha * 255f, 0, 255));
    }
}
