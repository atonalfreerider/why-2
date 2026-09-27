using System;
using System.Collections.Generic;
using System.Diagnostics;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Why.Humans;
using Debug = UnityEngine.Debug;

namespace Why.Domination
{
    /// <summary>
    /// Mineral resource extraction - matter under human control - drawn beneath the livestock and farm layer
    /// (red: it is still matter), completing the hierarchy of human domination: humans, then the life they
    /// farm, then the matter they dig up. Fossil fuels, metal ores and non-metallic minerals are stacked from
    /// the inner track; the stack's width follows annual extraction on the same power law as the human layer,
    /// exploding after 1800, and fades to black at the outside. Each material rises out of Earth's matter
    /// band when people first extracted it.
    /// </summary>
    public sealed class ExtractionLayer : GraphLayer
    {
        public const string DataPath = "Data/extraction";

        /// <summary>Width of the stack today, relative to the human layer's.</summary>
        const float WidthNow = HumanWorld.WidthNow * 0.9f;

        static readonly (string key, string name)[] Categories =
        {
            ("fossil_fuels", "Fossil fuels"), ("metal_ores", "Metal ores"), ("non_metallic_minerals", "Sand, stone & other minerals")
        };

        public override int Order => 41;
        public override IEnumerable<string> RequiredTexts => new[] { DataPath };

        const float FillAlpha = 0.4f;

        SurfaceMeshBuilder fills;
        LineMeshBuilder lines;
        Material fillMat, lineMat;
        readonly List<LabelSpec> labels = new List<LabelSpec>();
        readonly StrataEmphasis emphasis = new StrataEmphasis(new IdRange(GraphIds.ExtractionBase, GraphIds.ExtractionBase + 9999));

        void AddLabel(GraphContext ctx, LabelSpec spec)
        {
            ctx.Labels.Add(spec);
            lock (labels) labels.Add(spec);
        }

        sealed class Material_
        {
            public string id, name, category, blurb;
            public double firstYearsAgo;
        }

        sealed class Event_
        {
            public string id, name, category, blurb;
            public double yearsAgo;
            public int tier = 2;
        }

        public override void Prepare(GraphContext ctx)
        {
            Stopwatch sw = Stopwatch.StartNew();
            string json = ctx.Text(DataPath);
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogWarning("[Why] ExtractionLayer: Data/extraction.json missing");
                return;
            }

            JObject root;
            try
            {
                root = JObject.Parse(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Why] ExtractionLayer: could not parse extraction.json: {e.Message}");
                return;
            }

            JObject series = root["series"] as JObject;
            List<Stream> streams = new List<Stream>();
            foreach ((string key, string name) in Categories)
            {
                Series s = ReadSeries(series?[key]);
                if (!s.Empty) streams.Add(new Stream { Id = key, Name = name, Values = s });
            }

            if (streams.Count == 0) return;
            List<Material_> materials = root["materials"]?.ToObject<List<Material_>>() ?? new List<Material_>();
            List<Event_> events = root["events"]?.ToObject<List<Event_>>() ?? new List<Event_>();

            double now = ctx.NowYear;
            double totalNow = 0;
            foreach (Stream s in streams) totalNow += s.Values.At(now);
            Func<double, float> width = year =>
            {
                double t = 0;
                foreach (Stream s in streams) t += s.Values.At(year);
                return StackedStreams.Width(t, Math.Max(totalNow, 1), WidthNow);
            };

            fills = new SurfaceMeshBuilder();
            lines = new LineMeshBuilder(8192);
            float y = GraphStyle.ExtractionY;
            StackedStreams.Built stack = StackedStreams.Build(streams, width, -9000, now, y, GraphIds.ExtractionBase,
                HumanWorld.WidthNow * 1.1f, fills, lines);

            foreach ((Stream stream, Vector3 label, IdRange ids, double year) in stack.Streams)
            {
                Anchor a = new Anchor
                {
                    Key = "extraction:" + stream.Id, Label = stream.Name, Level = GraphLevel.Matter,
                    Blurb = stream.Name + ": megatonnes extracted per year (UN IRP / Krausmann et al.; rough before 1900).",
                    YearsAgo = DeepTime.YearsAgo(label.x), EndYearsAgo = 0, Y = y, Rho = label.z, Ids = ids, Tier = 2
                };
                Anchors.Register(a);
                AddLabel(ctx, new LabelSpec
                {
                    Text = stream.Name, Data = label, Priority = 16, SizePx = 12.5f, Color = GraphStyle.Text,
                    AnchorKey = a.Key, Ids = ids
                });
            }

            Anchors.Register(new Anchor
            {
                Key = "extraction:all", Label = "Mineral extraction", Level = GraphLevel.Matter,
                Blurb = "Matter under human control: about 100 billion tonnes of fuels, ores and minerals are dug up " +
                        "every year, most of it since 1950.",
                YearsAgo = now - 1850, EndYearsAgo = 0, Y = y, Rho = stack.OuterAt(1900) * 0.5f,
                Ids = new IdRange(GraphIds.ExtractionBase, GraphIds.ExtractionBase + 99), Tier = 1
            });
            AddLabel(ctx, new LabelSpec
            {
                Text = "MINERAL EXTRACTION", Data = new Vector3(DeepTime.Arc(now - 1750), y, stack.OuterAt(1750) + 0.05f),
                Priority = 32, SizePx = 12, Color = GraphStyle.TextDim, AnchorKey = "extraction:all",
                Ids = new IdRange(GraphIds.ExtractionBase, GraphIds.ExtractionBase + 99)
            });

            DrawMaterials(ctx, materials, stack, streams, now);
            RegisterEvents(ctx, events);
            Debug.Log($"[Why] ExtractionLayer.Prepare {sw.ElapsedMilliseconds} ms ({streams.Count} categories, " +
                      $"{materials.Count} materials, {events.Count} events)");
        }

        /// <summary>A glowing thread rises from Earth's matter into the extraction layer at first use.</summary>
        void DrawMaterials(GraphContext ctx, List<Material_> materials, StackedStreams.Built stack, List<Stream> streams,
            double now)
        {
            List<LinePoint> pts = new List<LinePoint>(32);
            for (int m = 0; m < materials.Count; m++)
            {
                Material_ mat = materials[m];
                if (mat == null || string.IsNullOrEmpty(mat.id) || mat.firstYearsAgo <= 0) continue;
                double year = now - mat.firstYearsAgo;
                float u0 = DeepTime.Arc(mat.firstYearsAgo);
                int si = streams.FindIndex(s => s.Id == mat.category);
                double landYear = Math.Min(now, year + Math.Max(150, mat.firstYearsAgo * 0.06));
                float rhoTo = 0;
                if (si >= 0)
                {
                    (float lo, float hi) = stack.BandAt(si, landYear);
                    rhoTo = 0.5f * (lo + hi);
                }

                float u1 = DeepTime.Arc(Math.Max(now - landYear, 1e-6));
                pts.Clear();
                const int steps = 24;
                for (int k = 0; k <= steps; k++)
                {
                    float t = k / (float)steps;
                    float e = t * t * (3 - 2 * t);
                    float yy = Mathf.Lerp(GraphStyle.MatterY, GraphStyle.ExtractionY, Mathf.Sin(t * Mathf.PI * 0.5f));
                    byte a = (byte)(255 * (0.25f + 0.65f * t));
                    pts.Add(new LinePoint(new Vector3(Mathf.Lerp(u0, u1, e), yy, Mathf.Lerp(0.01f, rhoTo, e)),
                        new Color32(255, 255, 255, a), 1.4f, 0, 2.2f));
                }

                int id = GraphIds.ExtractionBase + 100 + m;
                lines.AddPolyline(pts, id, 1f);
                Anchor anchor = new Anchor
                {
                    Key = "resource:" + mat.id, Label = mat.name, Level = GraphLevel.Matter, Blurb = mat.blurb,
                    YearsAgo = mat.firstYearsAgo, EndYearsAgo = 0, Y = GraphStyle.ExtractionY, Rho = rhoTo,
                    Ids = IdRange.Single(id), Tier = 2
                };
                Anchors.Register(anchor);
                AddLabel(ctx, new LabelSpec
                {
                    Text = mat.name, Data = new Vector3(u1, GraphStyle.ExtractionY, rhoTo), Priority = 7, SizePx = 11,
                    Color = GraphStyle.TextDim, PixelOffset = new Vector2(4, -8), AnchorKey = anchor.Key, Ids = anchor.Ids
                });
            }
        }

        void RegisterEvents(GraphContext ctx, List<Event_> events)
        {
            foreach (Event_ e in events)
            {
                if (e == null || string.IsNullOrEmpty(e.id) || e.yearsAgo <= 0) continue;
                Anchor a = new Anchor
                {
                    Key = "extractionevent:" + e.id, Label = e.name, Level = GraphLevel.Matter, Blurb = e.blurb,
                    YearsAgo = e.yearsAgo, EndYearsAgo = e.yearsAgo, Y = GraphStyle.ExtractionY, Rho = -0.06f,
                    Tier = e.tier
                };
                Anchors.Register(a);
                AddLabel(ctx, new LabelSpec
                {
                    Text = e.name, Data = a.Data, Priority = e.tier == 1 ? 14 : e.tier == 2 ? 6 : 2, SizePx = 11,
                    Color = GraphStyle.TextDim, Align = TMPro.TextAlignmentOptions.Right, PixelOffset = new Vector2(-6, 0),
                    AnchorKey = a.Key
                });
            }
        }

        static Series ReadSeries(JToken token)
        {
            if (token == null) return new Series(null);
            try
            {
                return new Series(token.ToObject<List<double[]>>());
            }
            catch (Exception)
            {
                return new Series(null);
            }
        }

        public override void Upload(GraphContext ctx)
        {
            if (fills == null) return;
            fillMat = GraphMaterials.Surface(GraphStyle.Matter, 1f, GraphMaterials.QueueHumans - 40, false, 0f, 4f);
            fillMat.SetFloat("_EdgeSoft", 0.25f);
            fillMat.SetFloat("_Alpha", FillAlpha);
            AddMesh("ExtractionStreams", fills.ToMesh("ExtractionStreams"), fillMat);
            lineMat = GraphMaterials.Line(GraphStyle.Matter, 1f, GraphMaterials.QueueHumans - 39, true, 0f, 1f);
            AddMesh("ExtractionLines", lines.ToMesh("ExtractionLines"), lineMat);
            fills = null;
            lines = null;

            emphasis.Add(fillMat, FillAlpha);
            emphasis.Add(lineMat, 1f);
            lock (labels) labels.ForEach(emphasis.Add);
        }

        /// <summary>Faint in human-focused views unless the director points at the extraction layer.</summary>
        public override void Tick(GraphContext ctx, CameraRig rig) => emphasis.Tick(Time.unscaledDeltaTime);
    }
}
