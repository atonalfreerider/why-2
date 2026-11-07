using System;
using System.Collections.Generic;
using System.Diagnostics;
using Newtonsoft.Json;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Why.Matter
{
    /// <summary>
    /// Bodies that struck the Earth (impactors.json): Theia, whose collision made the Moon, the Late Heavy Bombardment's
    /// rocks, and the asteroid that ended the dinosaurs at Chicxulub. The red layer's radius is the real distance from our
    /// lineage (<see cref="MatterScale"/>), so each body's path is drawn where it was: it starts when and where the body
    /// formed, out in the young solar system, keeps its distance from Earth while it waits (in the asteroid belt for
    /// billions of years), and plunges back into the inner track on the day it struck. Everything is a red line, as the rest
    /// of the matter layer: the body itself is a bloomed red streak (the line shader's path pulse) running along its path
    /// from where it formed to where it struck, one pass every <see cref="TravelSeconds"/>. An impact that changed life rises in one line from the
    /// impact to the life event it caused (the end-Cretaceous extinction). Each path's key points say which distances are
    /// measured and which are schematic (impactors.json and Docs/research/impacts.md).
    /// </summary>
    public sealed class ImpactorLayer : GraphLayer
    {
        public const string DataPath = "Data/impactors";

        public override int Order => 7;
        public override IEnumerable<string> RequiredTexts => new[] { MatterLayer.DataPath, DataPath };

        /// <summary>Samples per segment between two key points of a path.</summary>
        const int SegmentSamples = 40;

        /// <summary>Seconds the body's streak takes along its whole path (the paths' phase runs 0..1 from formation to
        /// impact, so every body, long path or short, takes the same time).</summary>
        const float TravelSeconds = 9f;

        /// <summary>Path opacity at zero matter emphasis, the path's glow where it plunges, and the streak's strength (the
        /// shader brightens the pulse's head by up to 1 + 3 × this).</summary>
        const float AlphaFloor = 0.3f, PlungeIntensity = 2.2f, StreakFlow = 2.5f;

        /// <summary>The path's hue: the matter layer's own red (the bloom whitens its brightest parts, as on the bands).</summary>
        static readonly Color PathColor = GraphStyle.Matter;

        sealed class KeyPoint
        {
            [JsonProperty("ya")] public double Ya;
            [JsonProperty("log10DistanceM")] public double Log10DistanceM;
            [JsonProperty("kind")] public string Kind;
            [JsonProperty("note")] public string Note;
        }

        sealed class Impactor
        {
            [JsonProperty("id")] public string Id;
            [JsonProperty("name")] public string Name;
            [JsonProperty("formedLabel")] public string FormedLabel;
            [JsonProperty("epoch")] public string Epoch;
            [JsonProperty("lifeEvent")] public string LifeEvent;
            [JsonProperty("lifeLabel")] public string LifeLabel;
            [JsonProperty("blurb")] public string Blurb;
            [JsonProperty("source")] public string Source;
            [JsonProperty("formedYa")] public double FormedYa;
            [JsonProperty("impactYa")] public double ImpactYa;
            [JsonProperty("path")] public List<KeyPoint> Path;

            /// <summary>A swarm (the bombardment): this many rocks, their impacts spread over <see cref="ImpactSpreadYa"/>
            /// before <see cref="ImpactYa"/>, their distances varied by up to <see cref="SpreadDecades"/>.</summary>
            [JsonProperty("count")] public int Count = 1;
            [JsonProperty("impactSpreadYa")] public double ImpactSpreadYa;
            [JsonProperty("spreadDecades")] public double SpreadDecades;
        }

        sealed class ImpactorFile
        {
            [JsonProperty("impactors")] public List<Impactor> Impactors;
        }

        /// <summary>One rock's path in data space (u, y, rho), from where it formed to the inner track.</summary>
        sealed class Track
        {
            public Vector3[] Data;
            public IdRange Ids;
        }

        sealed class Rise
        {
            public string LifeEvent, Label;
            public Vector3 From;
            public IdRange Ids;
        }

        readonly List<Track> tracks = new List<Track>();
        readonly List<Rise> rises = new List<Rise>();
        LineMeshBuilder lines;
        Material lineMat;
        IdRange allIds = IdRange.Empty;
        float emphasis = -1;

        public override void Prepare(GraphContext ctx)
        {
            Stopwatch sw = Stopwatch.StartNew();
            MatterFile matter = MatterFile.Parse(ctx.Text(MatterLayer.DataPath), out string error);
            if (matter == null)
            {
                Debug.LogWarning("[Why] ImpactorLayer: " + error);
                return;
            }

            ImpactorFile file;
            try
            {
                file = JsonConvert.DeserializeObject<ImpactorFile>(ctx.Text(DataPath) ?? "");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Why] ImpactorLayer: could not parse impactors.json: " + e.Message);
                return;
            }

            if (file?.Impactors == null || file.Impactors.Count == 0) return;
            MatterLayout layout = MatterLayout.Build(matter);
            if (layout.IsEmpty) return;
            MatterScale scale = new MatterScale(layout);

            lines = new LineMeshBuilder(8192);
            int id = GraphIds.ImpactBase, rocks = 0;
            List<LinePoint> pts = new List<LinePoint>(256);
            foreach (Impactor imp in file.Impactors)
            {
                if (imp?.Path == null || imp.Path.Count < 2 || string.IsNullOrEmpty(imp.Id)) continue;
                imp.Path.Sort((a, b) => b.Ya.CompareTo(a.Ya));
                int first = id, n = Mathf.Max(1, imp.Count);
                Vector3 formedAt = Vector3.zero;
                for (int k = 0; k < n; k++)
                {
                    // a swarm's rocks strike one after another across the span, from a spread of distances
                    double shiftYa = n > 1 ? imp.ImpactSpreadYa * k / (n - 1) : 0;
                    double shiftDecades = n > 1 ? imp.SpreadDecades * (((k * 7) % n) / (double)(n - 1) - 0.5) * 2 : 0;
                    Vector3[] data = Sample(imp, scale, shiftYa, shiftDecades);
                    if (data.Length < 2) continue;
                    if (k == 0) formedAt = data[0];

                    pts.Clear();
                    float length = 0;
                    for (int i = 1; i < data.Length; i++) length += Vector3.Distance(data[i - 1], data[i]);
                    for (int i = 0; i < data.Length; i++)
                    {
                        // faint while the body waits, brightening as it closes in: the plunge is the point
                        float t = i / (float)(data.Length - 1);
                        float close = Mathf.Pow(t, 6f);
                        byte alpha = (byte)(255 * Mathf.Lerp(n > 1 ? 0.3f : 0.45f, 1f, close));
                        pts.Add(new LinePoint(data[i], new Color32(255, 255, 255, alpha), n > 1 ? 1.1f : 1.6f, 0,
                            Mathf.Lerp(1f, PlungeIntensity, close)));
                    }

                    // the body: one streak per pass, its phase normalised to the path (0 formed .. 1 struck); a swarm's
                    // rocks start their passes apart
                    lines.AddFlowPath(pts, id, length > 0 ? 1f / length : 1f, (k * 0.37f) % 1f);
                    tracks.Add(new Track { Data = data, Ids = IdRange.Single(id) });
                    id++;
                    rocks++;
                }

                IdRange ids = new IdRange(first, id - 1);
                allIds = allIds.IsEmpty ? ids : new IdRange(allIds.Min, ids.Max);
                for (int t = tracks.Count - n; t < tracks.Count; t++)
                {
                    if (t >= 0) tracks[t].Ids = ids;
                }

                Anchor anchor = new Anchor
                {
                    Key = "impactor:" + imp.Id, Label = imp.Name, Blurb = imp.Blurb, Level = GraphLevel.Matter,
                    YearsAgo = imp.FormedYa, EndYearsAgo = imp.ImpactYa, Y = GraphStyle.MatterY, Rho = formedAt.z,
                    Ids = ids, Tier = 2
                };
                Anchors.Register(anchor);
                if (!string.IsNullOrEmpty(imp.FormedLabel))
                {
                    ctx.Labels.Add(new LabelSpec
                    {
                        Text = imp.FormedLabel, Data = formedAt, Priority = 9, SizePx = 11, Color = GraphStyle.TextDim,
                        PixelOffset = new Vector2(6, 6), AnchorKey = anchor.Key, Ids = ids, HandoffFade = true
                    });
                }

                if (!string.IsNullOrEmpty(imp.LifeEvent))
                {
                    rises.Add(new Rise
                    {
                        LifeEvent = imp.LifeEvent, Label = imp.LifeLabel, Ids = ids,
                        From = new Vector3(DeepTime.Arc(imp.ImpactYa), GraphStyle.MatterY, 0)
                    });
                }
            }

            Debug.Log($"[Why] ImpactorLayer.Prepare {sw.ElapsedMilliseconds} ms: {file.Impactors.Count} impactors, {rocks} rocks, " +
                      $"{lines.VertexCount} vertices");
        }

        /// <summary>
        /// A path in data space: between key points the distance from Earth (log10 metres) runs linearly in clock arc,
        /// placed on the red layer's scale; the last point is the inner track, where the body struck.
        /// </summary>
        static Vector3[] Sample(Impactor imp, MatterScale scale, double shiftYa, double shiftDecades)
        {
            List<Vector3> data = new List<Vector3>(imp.Path.Count * SegmentSamples + 2);
            for (int s = 0; s + 1 < imp.Path.Count; s++)
            {
                KeyPoint a = imp.Path[s], b = imp.Path[s + 1];
                // the swarm's shift moves the arrival, not the start; the last key point lands at the impact
                bool last = s + 2 == imp.Path.Count;
                double yaA = s == 0 ? a.Ya : a.Ya - shiftYa, yaB = b.Ya - shiftYa;
                float uA = DeepTime.Arc(yaA), uB = DeepTime.Arc(yaB);
                double dA = a.Log10DistanceM + (s == 0 ? shiftDecades : shiftDecades * 0.5),
                    dB = last ? b.Log10DistanceM : b.Log10DistanceM + shiftDecades * 0.5;
                for (int i = 0; i <= SegmentSamples; i++)
                {
                    if (s > 0 && i == 0) continue;
                    float t = i / (float)SegmentSamples;
                    float u = Mathf.Lerp(uA, uB, t);
                    double log10 = dA + (dB - dA) * t;
                    float rho = scale.Rho(u, log10);
                    if (rho < 0) rho = scale.EnvelopeAt(u);
                    data.Add(new Vector3(u, GraphStyle.MatterY, rho));
                }
            }

            if (data.Count > 0)
            {
                Vector3 end = data[data.Count - 1];
                data.Add(new Vector3(end.x, end.y, 0));
            }

            return data.ToArray();
        }

        public override void Upload(GraphContext ctx)
        {
            if (lines == null) return;
            // the impact that changed life: one line from the impact up to the life event it caused
            foreach (Rise rise in rises)
            {
                if (!Anchors.TryGet("lifeevent:" + rise.LifeEvent, out Anchor life))
                {
                    Debug.LogWarning("[Why] ImpactorLayer: no life event " + rise.LifeEvent);
                    continue;
                }

                Vector3 to = life.Data;
                List<LinePoint> pts = new List<LinePoint>(25);
                for (int i = 0; i <= 24; i++)
                {
                    float t = i / 24f;
                    Vector3 p = Vector3.Lerp(rise.From, to, t);
                    p.y = Mathf.Lerp(rise.From.y, to.y, Mathf.Sin(t * Mathf.PI * 0.5f));
                    pts.Add(new LinePoint(p, new Color32(255, 255, 255, (byte)(255 * Mathf.Lerp(1f, 0.55f, t))), 1.8f, 0,
                        Mathf.Lerp(PlungeIntensity, 1f, t)));
                }

                // the consequence rises from the impact into life, its streak in step with the body's (one pass per path)
                float rl = 0;
                for (int i = 1; i < pts.Count; i++) rl += Vector3.Distance(pts[i - 1].Data, pts[i].Data);
                lines.AddFlowPath(pts, rise.Ids.Max, rl > 0 ? 1f / rl : 1f, 0.5f);
                if (!string.IsNullOrEmpty(rise.Label))
                {
                    ctx.Labels.Add(new LabelSpec
                    {
                        Text = rise.Label, Data = Vector3.Lerp(rise.From, to, 0.5f) + Vector3.up * 0.02f, Priority = 10,
                        SizePx = 11, Color = GraphStyle.TextDim, PixelOffset = new Vector2(8, 0), Ids = rise.Ids,
                        HandoffFade = true
                    });
                }
            }

            lineMat = GraphMaterials.Line(PathColor, 1.4f, GraphMaterials.QueueMatter + 2, true, 0f, StreakFlow);
            lineMat.SetFloat("_FlowFreq", 1f);
            lineMat.SetFloat("_FlowSpeed", 1f / TravelSeconds);
            AddMesh("ImpactorPaths", lines.ToMesh("ImpactorPaths"), lineMat);
            lines = null;
        }

        /// <summary>The paths dim with the red layer unless the director points at them; the streaks move in the shader.</summary>
        public override void Tick(GraphContext ctx, CameraRig rig)
        {
            if (lineMat == null) return;
            float target = 1f;
            GraphRoot root = GraphRoot.Instance;
            if (root != null && root.CurrentPreset != null) target = root.CurrentPreset.MatterEmphasis;
            if (!allIds.IsEmpty && Highlighter.IsHighlighted(allIds)) target = 1f;
            emphasis = emphasis < 0 ? target : Mathf.MoveTowards(emphasis, target, GraphTime.Delta * 1.6f);
            GraphMaterials.SetAlpha(lineMat, Mathf.Lerp(AlphaFloor, 1f, emphasis));
        }
    }
}
