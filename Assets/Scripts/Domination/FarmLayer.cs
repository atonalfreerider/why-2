using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Why.Humans;
using Debug = UnityEngine.Debug;

namespace Why.Domination
{
    /// <summary>
    /// Livestock and farm biomass - life under human control - drawn directly beneath the civilization layer
    /// (green: it is still life). Livestock is stacked from the inner track on the same mass scale as the
    /// human layer (humans ~60 MtC = the human layer's width today), so its greater weight is visible;
    /// cropland lies outside it and fades to black. Each domestication is anchored (with a small label) where
    /// the species enters the farm layer, for the tour and tooltips. Below the farm, on the life plane, the wild
    /// mammals shrink on the same carbon scale while the livestock grows: the present mass extinction as biomass.
    /// </summary>
    public sealed class FarmLayer : GraphLayer
    {
        public const string DataPath = "Data/domestication";

        /// <summary>Human biomass today (MtC) - the reference that maps mass to width.</summary>
        const double HumanMtCNow = 60;

        /// <summary>Cropland today (Mha) and the width it maps to.</summary>
        const double CroplandMhaNow = 1600;
        const float CroplandWidthNow = 2.2f;

        /// <summary>
        /// Highlight id of the wild mammals stream: outside farm:all (FarmBase .. FarmBase+99), so picking the livestock and
        /// crops never lights the wild mammals or the reverse (a tour stop names farm:wild_mammals to light them), but inside
        /// the farm's <see cref="StrataEmphasis"/> range (FarmBase .. ExtractionBase - 1), so highlighting it brings the faint
        /// farm stratum, the stream included, back to full strength. Livestock takes FarmBase+0..4, cropland FarmBase+50 and
        /// the domestications FarmBase+100 on (+100 .. +134 for the 35 of domestication.json).
        /// </summary>
        const int WildMammalsId = GraphIds.FarmBase + 9_000;

        /// <summary>
        /// Years before its first value over which the wild mammals stream fades in, at that first value, so it does
        /// not start as a wall.
        /// </summary>
        const double WildFadeYears = 2000;

        /// <summary>
        /// Calendar year of the wild mammals stream's label: inside the footprint view, before the collapse, where the
        /// tour's farm stops (looking from about 10,500 years ago to the 1950s) all keep it in frame.
        /// </summary>
        const double WildLabelYear = 500;

        static readonly (string key, string name)[] Livestock =
        {
            ("cattle", "Cattle"), ("sheep_goats", "Sheep & goats"), ("pigs", "Pigs"), ("poultry", "Poultry"),
            ("horses_other", "Horses, camels & other")
        };

        public override int Order => 40;
        public override IEnumerable<string> RequiredTexts => new[] { DataPath };

        const float FillAlpha = 0.3f;

        /// <summary>The wild mammals' fill: a faint wash, its outer edge line carrying the shape, so a band several units wide on
        /// the life plane does not read as a green slab over the human branch in the overview and the footprint view.</summary>
        const float WildFillAlpha = 0.035f;

        SurfaceMeshBuilder fills, wildFills;
        LineMeshBuilder lines, wildLines;
        Material fillMat, lineMat, wildFillMat, wildLineMat;
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

            RegisterDomestications(ctx, domestications, animalStack, cropStack, animals, now);
            bool wild = BuildWildMammals(ctx, series?["wildMammalMtC"], livestockMass, now, fade);
            Debug.Log($"[Why] FarmLayer.Prepare {sw.ElapsedMilliseconds} ms ({animals.Count} livestock streams, " +
                      $"{domestications.Count} domestications{(wild ? ", wild mammals" : "")})");
        }

        /// <summary>
        /// The present mass extinction as biomass: wild land and marine mammals (series.wildMammalMtC) as one stream
        /// on the life plane, where the tree of life ran before it dissolved, on the livestock's carbon scale and width
        /// rule, so it is seen to shrink while the livestock above it grows past it. The series opens at full strength
        /// and the stream fades in at that first value: a width rising from nothing would draw a growth the data does
        /// not have (its notes have the American extinctions still under way at the first year). The stream keeps a
        /// line along its outer edge, as the livestock has the cropland's, so the two widths read alike. Returns false,
        /// after an error, when the series is missing or malformed.
        /// </summary>
        bool BuildWildMammals(GraphContext ctx, JToken token, Func<double, double> livestockMass, double now, float fade)
        {
            List<double[]> data = ReadWildMammals(token);
            if (data == null) return false;

            double first = data[0][0];
            double fadeStart = first - WildFadeYears;
            List<double[]> drawn = new List<double[]>(data.Count + 1) { new[] { fadeStart, data[0][1] } };
            drawn.AddRange(data);
            Stream wild = new Stream { Id = "wild_mammals", Name = "Wild mammals", Values = new Series(drawn) };

            wildFills = new SurfaceMeshBuilder();
            wildLines = new LineMeshBuilder(1024);
            StackedStreams.Built built = StackedStreams.Build(new List<Stream> { wild },
                year => StackedStreams.Width(wild.Values.At(year), HumanMtCNow, HumanWorld.WidthNow), fadeStart, now,
                GraphStyle.LifeY, WildMammalsId, fade, wildFills, wildLines, fadeOuter: false, outerEdge: true,
                fadeIn: year => year >= first ? 1f : Mathf.SmoothStep(0f, 1f, (float)((year - fadeStart) / WildFadeYears)));

            // the anchor stands where the series begins (the tooltip spans it to now), the label where the tour looks
            IdRange ids = IdRange.Single(WildMammalsId);
            (float lo0, float hi0) = built.BandAt(0, first);
            Anchor anchor = new Anchor
            {
                Key = "farm:wild_mammals", Label = wild.Name, Level = GraphLevel.Life,
                Blurb = WildMammalsBlurb(data, livestockMass),
                YearsAgo = now - first, EndYearsAgo = 0, Y = GraphStyle.LifeY, Rho = 0.5f * (lo0 + hi0), Ids = ids, Tier = 2
            };
            Anchors.Register(anchor);

            double labelYear = Math.Min(Math.Max(WildLabelYear, first), now);
            (float lo, float hi) = built.BandAt(0, labelYear);
            AddLabel(ctx, new LabelSpec
            {
                Text = wild.Name, Data = new Vector3(DeepTime.Arc(Math.Max(now - labelYear, 1e-6)), GraphStyle.LifeY,
                    0.5f * (lo + hi)),
                Priority = 16, SizePx = 12.5f, Color = GraphStyle.Text, AnchorKey = anchor.Key, Ids = ids
            });
            return true;
        }

        /// <summary>
        /// The wild mammals' tooltip: the series' own first and last values, the livestock in the last year from the
        /// same file, and the sources and caveat domestication.json gives for the series.
        /// </summary>
        static string WildMammalsBlurb(List<double[]> data, Func<double, double> livestockMass)
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            double[] a = data[0], b = data[data.Count - 1];
            double livestock = livestockMass(b[0]);
            return $"Wild land and marine mammals: {a[1].ToString("0.##", c)} megatonnes of carbon in {Year(a[0])}, " +
                   $"{b[1].ToString("0.##", c)} in {Year(b[0])}" +
                   (livestock > 0 ? $", against {livestock.ToString("0", c)} for livestock. " : ". ") +
                   "After Bar-On, Phillips and Milo 2018, Greenspoon et al. 2023 and Barnosky 2008 (PNAS); before " +
                   "about 1900 a rough reconstruction.";
        }

        /// <summary>A calendar year of the data as text: "10,000 BCE" (negative years are BCE) or "2023".</summary>
        static string Year(double year)
        {
            double y = Math.Round(year);
            return y < 0 ? (-y).ToString("#,0", CultureInfo.InvariantCulture) + " BCE" : y.ToString("0", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// series.wildMammalMtC as [calendar year, MtC] rows, read strictly because the stream and its tooltip quote
        /// them: at least two rows, each two finite numbers, the years rising and no mass below zero. Null, after an
        /// error, when the series is missing or malformed.
        /// </summary>
        static List<double[]> ReadWildMammals(JToken token)
        {
            string problem = null;
            List<double[]> rows = new List<double[]>();
            JArray array = token as JArray;
            if (token == null) problem = "is missing";
            else if (array == null || array.Count < 2) problem = "is not a list of at least two [year, MtC] rows";
            else
            {
                for (int i = 0; i < array.Count && problem == null; i++)
                {
                    JArray row = array[i] as JArray;
                    if (row == null || row.Count != 2 || !IsNumber(row[0]) || !IsNumber(row[1]))
                    {
                        problem = $"has a row {i} that is not [year, MtC]";
                        continue;
                    }

                    double year = (double)row[0], mass = (double)row[1];
                    if (double.IsNaN(year) || double.IsInfinity(year) || double.IsNaN(mass) || double.IsInfinity(mass) ||
                        mass < 0)
                        problem = $"has a row {i} whose year or mass is not a finite number (masses at least zero)";
                    else if (rows.Count > 0 && year <= rows[rows.Count - 1][0])
                        problem = $"has a row {i} whose year does not follow the row before it";
                    else
                        rows.Add(new[] { year, mass });
                }
            }

            if (problem == null) return rows;
            Debug.LogError($"[Why] FarmLayer: series.wildMammalMtC in domestication.json {problem}; " +
                           "the wild mammals stream is not drawn");
            return null;
        }

        static bool IsNumber(JToken t) => t.Type == JTokenType.Integer || t.Type == JTokenType.Float;

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

        /// <summary>
        /// Anchors (for the tour and tooltips) and small labels where each domestication lands in the farm
        /// layer. No threads are drawn from the tree of life: seen side-on in the unrolled human-era views they
        /// read as tall green spikes over the early civilizations.
        /// </summary>
        void RegisterDomestications(GraphContext ctx, List<Domestication> list, StackedStreams.Built animals,
            StackedStreams.Built crops, List<Stream> animalStreams, double now)
        {
            foreach (Domestication d in list)
            {
                if (d == null || string.IsNullOrEmpty(d.id) || d.yearsAgo <= 0) continue;
                double year = now - d.yearsAgo;

                // where it lands: its livestock stream, or the cropland
                int streamIndex = animalStreams.FindIndex(s => s.Id == d.group);
                double landYear = Math.Min(now, year + Math.Max(300, d.yearsAgo * 0.08));
                float rhoFarm;
                IdRange ids = IdRange.Single(GraphIds.FarmBase + 100 + list.IndexOf(d));
                if (streamIndex >= 0)
                {
                    (float lo, float hi) = animals.BandAt(streamIndex, landYear);
                    rhoFarm = 0.5f * (lo + hi);
                    int built = animals.Streams.FindIndex(s => s.stream.Id == d.group);
                    if (built >= 0) ids = animals.Streams[built].ids;
                }
                else if (crops != null && d.kind == "plant")
                {
                    (float lo, float hi) = crops.BandAt(0, landYear);
                    rhoFarm = 0.5f * (lo + hi);
                    if (crops.Streams.Count > 0) ids = crops.Streams[0].ids;
                }
                else
                {
                    rhoFarm = 0;
                }

                float u1 = DeepTime.Arc(Math.Max(now - landYear, 1e-6));
                Anchor anchor = new Anchor
                {
                    Key = "domestication:" + d.id, Label = d.name + " domesticated", Level = GraphLevel.Life,
                    Blurb = d.blurb + (string.IsNullOrEmpty(d.region) ? "" : " (" + d.region + ")"),
                    YearsAgo = d.yearsAgo, EndYearsAgo = d.yearsAgo, Y = GraphStyle.FarmY, Rho = rhoFarm,
                    Ids = ids, Tier = 2
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
            if (wildFills != null)
            {
                // the wild mammals' plane lies between the extraction stratum and the farm, and is drawn between them
                wildFillMat = GraphMaterials.Surface(GraphStyle.Life, 1f, GraphMaterials.QueueHumans - 30, false, 0f, 4f);
                wildFillMat.SetFloat("_EdgeSoft", 0.25f);
                wildFillMat.SetFloat("_Alpha", WildFillAlpha);
                AddMesh("WildMammals", wildFills.ToMesh("WildMammals"), wildFillMat);
                wildLineMat = GraphMaterials.Line(GraphStyle.Life, 1f, GraphMaterials.QueueHumans - 29, true, 0f, 1f);
                AddMesh("WildMammalsLines", wildLines.ToMesh("WildMammalsLines"), wildLineMat);
                wildFills = null;
                wildLines = null;

                emphasis.Add(wildFillMat, WildFillAlpha);
                emphasis.Add(wildLineMat, 0.7f);
            }

            lock (labels) labels.ForEach(emphasis.Add);
        }

        /// <summary>Faint in human-focused views unless the director points at the farm layer.</summary>
        public override void Tick(GraphContext ctx, CameraRig rig) => emphasis.Tick(GraphTime.Delta);
    }
}
