using System;
using System.Collections.Generic;
using System.Diagnostics;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Why.Humans;
using Why.Life;
using Debug = UnityEngine.Debug;

namespace Why.Domination
{
    /// <summary>
    /// Livestock and farm biomass - life under human control - drawn directly beneath the civilization layer
    /// (green: it is still life). Livestock is stacked from the inner track on the same mass scale as the
    /// human layer (humans ~60 MtC = the human layer's width today), so its greater weight is visible;
    /// cropland lies outside it and fades to black. Domesticated species rise out of their wild families in
    /// the tree of life at the moment of domestication: cause and effect across levels.
    /// </summary>
    public sealed class FarmLayer : GraphLayer
    {
        public const string DataPath = "Data/domestication";

        /// <summary>Human biomass today (MtC) - the reference that maps mass to width.</summary>
        const double HumanMtCNow = 60;

        /// <summary>Cropland today (Mha) and the width it maps to.</summary>
        const double CroplandMhaNow = 1600;
        const float CroplandWidthNow = 2.2f;

        static readonly (string key, string name)[] Livestock =
        {
            ("cattle", "Cattle"), ("sheep_goats", "Sheep & goats"), ("pigs", "Pigs"), ("poultry", "Poultry"),
            ("horses_other", "Horses, camels & other")
        };

        public override int Order => 40;
        public override IEnumerable<string> RequiredTexts => new[] { DataPath };

        const float FillAlpha = 0.3f;

        SurfaceMeshBuilder fills;
        LineMeshBuilder lines;
        Material fillMat, lineMat;
        readonly List<LabelSpec> labels = new List<LabelSpec>();
        readonly StrataEmphasis emphasis = new StrataEmphasis(new IdRange(GraphIds.FarmBase, GraphIds.ExtractionBase - 1));

        void AddLabel(GraphContext ctx, LabelSpec spec)
        {
            ctx.Labels.Add(spec);
            lock (labels) labels.Add(spec);
        }

        sealed class Domestication
        {
            public string id, name, kind, region, wildFamily, group, blurb;
            public double yearsAgo;
        }

        public override void Prepare(GraphContext ctx)
        {
            Stopwatch sw = Stopwatch.StartNew();
            string json = ctx.Text(DataPath);
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogWarning("[Why] FarmLayer: Data/domestication.json missing");
                return;
            }

            JObject root;
            try
            {
                root = JObject.Parse(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Why] FarmLayer: could not parse domestication.json: {e.Message}");
                return;
            }

            JObject series = root["series"] as JObject;
            JObject livestock = series?["livestockMtC"] as JObject;
            List<Stream> animals = new List<Stream>();
            foreach ((string key, string name) in Livestock)
            {
                Series s = ReadSeries(livestock?[key]);
                if (!s.Empty) animals.Add(new Stream { Id = key, Name = name, Values = s });
            }

            Series cropland = ReadSeries(series?["croplandMha"]);
            List<Domestication> domestications =
                root["domestications"]?.ToObject<List<Domestication>>() ?? new List<Domestication>();

            double now = ctx.NowYear;
            double start = -10500;
            fills = new SurfaceMeshBuilder();
            lines = new LineMeshBuilder(8192);
            float y = GraphStyle.FarmY;
            float fade = HumanWorld.WidthNow * 1.1f;

            Func<double, double> livestockMass = year =>
            {
                double m = 0;
                foreach (Stream a in animals) m += a.Values.At(year);
                return m;
            };
            Func<double, float> livestockWidth = year =>
                StackedStreams.Width(livestockMass(year), HumanMtCNow, HumanWorld.WidthNow);

            StackedStreams.Built animalStack = StackedStreams.Build(animals, livestockWidth, start, now, y,
                GraphIds.FarmBase, fade, fills, lines, null, cropland.Empty);

            StackedStreams.Built cropStack = null;
            if (!cropland.Empty)
            {
                List<Stream> crops = new List<Stream> { new Stream { Id = "cropland", Name = "Cropland", Values = cropland } };
                cropStack = StackedStreams.Build(crops,
                    year => StackedStreams.Width(cropland.At(year), CroplandMhaNow, CroplandWidthNow), start, now, y,
                    GraphIds.FarmBase + 50, fade, fills, lines, year => animalStack.OuterAt(year));
            }

            RegisterStreams(ctx, animalStack, "livestock");
            if (cropStack != null) RegisterStreams(ctx, cropStack, "crops");

            Anchors.Register(new Anchor
            {
                Key = "farm:all", Label = "Livestock & crops", Level = GraphLevel.Life,
                Blurb = "Life under human control. Livestock now outweighs all wild mammals many times over, and " +
                        "cropland covers about an eighth of the land.",
                YearsAgo = now - 1800, EndYearsAgo = 0, Y = y, Rho = animalStack.OuterAt(1800) * 0.5f,
                Ids = new IdRange(GraphIds.FarmBase, GraphIds.FarmBase + 99), Tier = 1
            });
            AddLabel(ctx, new LabelSpec
            {
                Text = "LIVESTOCK & CROPS", Data = new Vector3(DeepTime.Arc(now - 1700), y, animalStack.OuterAt(1700) + 0.05f),
                Priority = 32, SizePx = 12, Color = GraphStyle.TextDim, AnchorKey = "farm:all",
                Ids = new IdRange(GraphIds.FarmBase, GraphIds.FarmBase + 99)
            });

            DrawDomestications(ctx, domestications, animalStack, cropStack, animals, now);
            Debug.Log($"[Why] FarmLayer.Prepare {sw.ElapsedMilliseconds} ms ({animals.Count} livestock streams, " +
                      $"{domestications.Count} domestications)");
        }

        void RegisterStreams(GraphContext ctx, StackedStreams.Built built, string kind)
        {
            foreach ((Stream stream, Vector3 label, IdRange ids, double year) in built.Streams)
            {
                Anchor a = new Anchor
                {
                    Key = "farm:" + stream.Id, Label = stream.Name, Level = GraphLevel.Life,
                    Blurb = kind == "crops" ? "Cropland area (HYDE)." : stream.Name + " biomass (carbon), on the same scale as humans.",
                    YearsAgo = DeepTime.YearsAgo(label.x), EndYearsAgo = 0, Y = label.y, Rho = label.z, Ids = ids, Tier = 2
                };
                Anchors.Register(a);
                AddLabel(ctx, new LabelSpec
                {
                    Text = stream.Name, Data = label, Priority = 16, SizePx = 12.5f, Color = GraphStyle.Text,
                    AnchorKey = a.Key, Ids = ids
                });
            }
        }

        /// <summary>A glowing thread rises from each wild family in the tree of life into the farm layer.</summary>
        void DrawDomestications(GraphContext ctx, List<Domestication> list, StackedStreams.Built animals,
            StackedStreams.Built crops, List<Stream> animalStreams, double now)
        {
            LifeLayer life = ctx.Shared<LifeLayer>("life.layer");
            List<LinePoint> pts = new List<LinePoint>(32);
            foreach (Domestication d in list)
            {
                if (d == null || string.IsNullOrEmpty(d.id) || d.yearsAgo <= 0) continue;
                double year = now - d.yearsAgo;
                float u0 = DeepTime.Arc(d.yearsAgo);
                float rhoLife = 0;
                if (life?.Tree != null && !string.IsNullOrEmpty(d.wildFamily))
                {
                    int leaf = life.Tree.FindLeaf(d.wildFamily);
                    if (leaf >= 0) rhoLife = life.NodeRho(leaf, u0);
                }

                // where it lands: its livestock stream, or the cropland
                int streamIndex = animalStreams.FindIndex(s => s.Id == d.group);
                double landYear = Math.Min(now, year + Math.Max(300, d.yearsAgo * 0.08));
                float rhoFarm;
                if (streamIndex >= 0)
                {
                    (float lo, float hi) = animals.BandAt(streamIndex, landYear);
                    rhoFarm = 0.5f * (lo + hi);
                }
                else if (crops != null && d.kind == "plant")
                {
                    (float lo, float hi) = crops.BandAt(0, landYear);
                    rhoFarm = 0.5f * (lo + hi);
                }
                else
                {
                    rhoFarm = 0;
                }

                float u1 = DeepTime.Arc(Math.Max(now - landYear, 1e-6));
                pts.Clear();
                const int steps = 24;
                for (int k = 0; k <= steps; k++)
                {
                    float t = k / (float)steps;
                    float e = t * t * (3 - 2 * t);
                    float u = Mathf.Lerp(u0, u1, e);
                    float yy = Mathf.Lerp(GraphStyle.LifeY, GraphStyle.FarmY, Mathf.Sin(t * Mathf.PI * 0.5f));
                    float rho = Mathf.Lerp(rhoLife, rhoFarm, e);
                    byte a = (byte)(255 * (0.25f + 0.65f * t));
                    pts.Add(new LinePoint(new Vector3(u, yy, rho), new Color32(255, 255, 255, a), 1.4f, 0, 2.2f));
                }

                int id = GraphIds.FarmBase + 100 + list.IndexOf(d);
                lines.AddPolyline(pts, id, 1f);

                Anchor anchor = new Anchor
                {
                    Key = "domestication:" + d.id, Label = d.name + " domesticated", Level = GraphLevel.Life,
                    Blurb = d.blurb + (string.IsNullOrEmpty(d.region) ? "" : " (" + d.region + ")"),
                    YearsAgo = d.yearsAgo, EndYearsAgo = d.yearsAgo, Y = GraphStyle.FarmY, Rho = rhoFarm,
                    Ids = IdRange.Single(id), Tier = 2
                };
                Anchors.Register(anchor);
                AddLabel(ctx, new LabelSpec
                {
                    Text = d.name, Data = new Vector3(u1, GraphStyle.FarmY, rhoFarm), Priority = 7, SizePx = 11,
                    Color = GraphStyle.TextDim, PixelOffset = new Vector2(4, -8), AnchorKey = anchor.Key, Ids = anchor.Ids
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
            fillMat = GraphMaterials.Surface(GraphStyle.Life, 1f, GraphMaterials.QueueHumans - 20, false, 0f, 4f);
            fillMat.SetFloat("_EdgeSoft", 0.25f);
            fillMat.SetFloat("_Alpha", FillAlpha);
            AddMesh("FarmStreams", fills.ToMesh("FarmStreams"), fillMat);
            lineMat = GraphMaterials.Line(GraphStyle.Life, 1f, GraphMaterials.QueueHumans - 19, true, 0f, 1f);
            AddMesh("FarmLines", lines.ToMesh("FarmLines"), lineMat);
            fills = null;
            lines = null;

            emphasis.Add(fillMat, FillAlpha);
            emphasis.Add(lineMat, 1f);
            lock (labels) labels.ForEach(emphasis.Add);
        }

        /// <summary>Faint in human-focused views unless the director points at the farm layer.</summary>
        public override void Tick(GraphContext ctx, CameraRig rig) => emphasis.Tick(Time.unscaledDeltaTime);
    }
}
