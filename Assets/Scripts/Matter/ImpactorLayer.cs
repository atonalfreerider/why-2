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
    /// billions of years), and plunges back into the inner track on the day it struck. A rock travels each path, slow out
    /// in space and fast at the end, and flashes where it hits. An impact that changed life rises in one line from the
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

        /// <summary>Seconds a rock takes along its whole path, then a pause at the impact.</summary>
        const float TravelSeconds = 9f, HoldSeconds = 1.6f;

        /// <summary>A rock's size on screen (px at 1080p), and its flash at the impact.</summary>
        const float RockPx = 13f, FlashScale = 2.6f;

        /// <summary>Path opacity at zero matter emphasis, and the path's glow where it plunges.</summary>
        const float AlphaFloor = 0.3f, PlungeIntensity = 2.2f;

        /// <summary>The path's hue: the matter red, lightened so the paths read over the red bands.</summary>
        static readonly Color PathColor = Color.Lerp(GraphStyle.Matter, Color.white, 0.35f);

        static readonly Color RockColor = new Color(0.62f, 0.57f, 0.52f), FlashColor = new Color(1f, 0.82f, 0.55f);

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
            public float Phase;
            public Transform Rock;
            public Material RockMat;
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
        Mesh rockMesh;
        IdRange allIds = IdRange.Empty;
        float emphasis = -1, clock;

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
                    for (int i = 0; i < data.Length; i++)
                    {
                        // faint while the body waits, brightening as it closes in: the plunge is the point
                        float t = i / (float)(data.Length - 1);
                        float close = Mathf.Pow(t, 6f);
                        byte alpha = (byte)(255 * Mathf.Lerp(n > 1 ? 0.3f : 0.45f, 1f, close));
                        pts.Add(new LinePoint(data[i], new Color32(255, 255, 255, alpha), n > 1 ? 1.1f : 1.6f, 0,
                            Mathf.Lerp(1f, PlungeIntensity, close)));
                    }

                    lines.AddPolyline(pts, id, 1f);
                    tracks.Add(new Track { Data = data, Phase = (k * 0.37f) % 1f, Ids = IdRange.Single(id) });
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

                lines.AddPolyline(pts, rise.Ids.Max, 1f);
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

            lineMat = GraphMaterials.Line(PathColor, 1f, GraphMaterials.QueueMatter + 2, true, 0f, 1f);
            AddMesh("ImpactorPaths", lines.ToMesh("ImpactorPaths"), lineMat);
            lines = null;

            rockMesh = RockMesh();
            Shader shader = Shader.Find("Sprites/Default");
            for (int i = 0; i < tracks.Count; i++)
            {
                GameObject go = new GameObject("Impactor rock " + i);
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = rockMesh;
                Material m = new Material(shader) { color = RockColor, renderQueue = GraphMaterials.QueueMatter + 3 };
                go.AddComponent<MeshRenderer>().sharedMaterial = m;
                tracks[i].Rock = go.transform;
                tracks[i].RockMat = m;
            }
        }

        /// <summary>The rocks travel their paths; the paths dim with the red layer unless the director points at them.</summary>
        public override void Tick(GraphContext ctx, CameraRig rig)
        {
            if (lineMat == null || rig == null) return;
            float target = 1f;
            GraphRoot root = GraphRoot.Instance;
            if (root != null && root.CurrentPreset != null) target = root.CurrentPreset.MatterEmphasis;
            bool lit = !allIds.IsEmpty && Highlighter.IsHighlighted(allIds);
            if (lit) target = 1f;
            emphasis = emphasis < 0 ? target : Mathf.MoveTowards(emphasis, target, GraphTime.Delta * 1.6f);
            GraphMaterials.SetAlpha(lineMat, Mathf.Lerp(AlphaFloor, 1f, emphasis));

            clock += GraphTime.Delta;
            Camera cam = rig.Cam;
            float pxToWorld = cam != null ? 2f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1, Screen.height) * (Screen.height / 1080f) : 0;
            bool show = emphasis > 0.15f;
            foreach (Track track in tracks)
            {
                if (track.Rock == null) continue;
                track.Rock.gameObject.SetActive(show);
                if (!show) continue;
                float period = TravelSeconds + HoldSeconds;
                float time = (clock / period + track.Phase) % 1f * period;
                float s = Mathf.Clamp01(time / TravelSeconds);
                float f = s * (track.Data.Length - 1);
                int i = Mathf.Min((int)f, track.Data.Length - 2);
                Vector3 world = GraphWarp.ToWorld(Vector3.Lerp(track.Data[i], track.Data[i + 1], f - i));
                float flash = time > TravelSeconds ? 1f - (time - TravelSeconds) / HoldSeconds : 0f;
                float size = cam != null ? Vector3.Distance(cam.transform.position, world) * pxToWorld * RockPx : 0.05f;
                track.Rock.position = world;
                // faces the camera, so its baked light stays put
                if (cam != null) track.Rock.rotation = cam.transform.rotation;
                track.Rock.localScale = Vector3.one * size * (1f + (FlashScale - 1f) * flash);
                Color c = Color.Lerp(RockColor, FlashColor, flash);
                c.a = Mathf.Clamp01(emphasis) * (time > TravelSeconds ? flash : 1f);
                track.RockMat.color = c;
            }
        }

        /// <summary>A lumpy unit rock with its light baked into the vertex colors (the sprite shader is unlit).</summary>
        static Mesh RockMesh()
        {
            const int rings = 10, segments = 14;
            List<Vector3> v = new List<Vector3>();
            List<Color32> c = new List<Color32>();
            List<int> tri = new List<int>();
            Vector3 light = new Vector3(0.5f, 0.7f, -0.5f).normalized;
            for (int r = 0; r <= rings; r++)
            {
                float lat = Mathf.PI * r / rings;
                for (int s = 0; s <= segments; s++)
                {
                    float lon = 2 * Mathf.PI * s / segments;
                    Vector3 n = new Vector3(Mathf.Sin(lat) * Mathf.Cos(lon), Mathf.Cos(lat), Mathf.Sin(lat) * Mathf.Sin(lon));
                    // a fixed lumpiness (no randomness: every build draws the same rock)
                    float bump = 1f + 0.16f * Mathf.Sin(3 * lat + 1.3f) * Mathf.Cos(2 * lon + 0.4f) + 0.08f * Mathf.Sin(5 * lon + 2 * lat);
                    v.Add(n * 0.5f * bump);
                    float shade = 0.35f + 0.65f * Mathf.Clamp01(Vector3.Dot(n, light));
                    byte g = (byte)(255 * shade);
                    c.Add(new Color32(g, g, g, 255));
                }
            }

            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < segments; s++)
                {
                    int a = r * (segments + 1) + s, b = a + segments + 1;
                    tri.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                }
            }

            Mesh m = new Mesh { name = "Impactor rock" };
            m.SetVertices(v);
            m.SetColors(c);
            m.SetTriangles(tri, 0);
            m.RecalculateBounds();
            return m;
        }
    }
}
