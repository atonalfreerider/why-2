using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;

namespace Why.Humans.Bands
{
    /// <summary>
    /// Anchors and labels of the civilization streams (worker thread): "civ:&lt;id&gt;" for every stream,
    /// "civ:humanity" for prehistoric Homo sapiens, "civ:_lineage" for our path along the inner edge, and
    /// "war:&lt;id&gt;" for every war. Label priorities are the level of detail: the most powerful streams
    /// are named first, small streams and wars appear as the view makes room.
    /// <para>The views of the streams' widths (<see cref="CivBandsLayer.WidthViews"/>) also name the axis across them and
    /// the states alive now, at the present edge on dark plates, and mark where the Histomap's widths hand over to shares
    /// of world GDP: seen at an angle, the layer's west-to-east width otherwise reads as a rise in rank. A mark at the
    /// United States' end leads on to its economy in every view.</para>
    /// </summary>
    public static class BandAnnotations
    {
        // the present edge's plates: the axis first, then the states (and the seam) as the view makes room
        const float AxisPriority = 92f;
        const float StatePriority = 72f;
        const float SeamPriority = 64f;
        const float EconomyPriority = 58f;
        static readonly Color PlateColor = new Color(0.02f, 0.027f, 0.047f, 0.75f);

        /// <summary>Last year the Histomap (Sparks 1931) gives widths; the extension's shares of world GDP start in 1949.</summary>
        const double HistomapLastYear = 1924, GdpFirstYear = 1949;

        /// <summary>A stream's names through time (civilizations.json "names"), for the people of a year and the present edge.</summary>
        static Dictionary<string, (double from, string name)[]> periods = new Dictionary<string, (double, string)[]>();

        sealed class NamesFile
        {
            public List<NamesDto> civs = new List<NamesDto>();
        }

        sealed class NamesDto
        {
            public string id;
            public List<PeriodDto> names;
        }

        sealed class PeriodDto
        {
            public double from;
            public string name;
        }

        /// <summary>
        /// What a stream was called in a year: its own name until the first of its later names begins. One stream runs
        /// from the Holy Roman Empire to Germany, another through the Soviet Union, and a person of 1914 belongs to the
        /// state of 1914.
        /// </summary>
        public static string NameAt(Civ c, double year)
        {
            if (c == null) return null;
            string name = c.Name;
            if (c.Id != null && periods.TryGetValue(c.Id, out (double from, string name)[] list))
            {
                foreach ((double from, string n) in list)
                {
                    if (year >= from) name = n;
                }
            }

            return name;
        }

        /// <summary>Reads the streams' later names from civilizations.json (worker thread; unknown or missing names keep the stream's own).</summary>
        public static void ReadNames(string civilizationsJson)
        {
            Dictionary<string, (double, string)[]> map = new Dictionary<string, (double, string)[]>();
            try
            {
                NamesFile file = JsonConvert.DeserializeObject<NamesFile>(civilizationsJson ?? "{}");
                foreach (NamesDto c in file?.civs ?? new List<NamesDto>())
                {
                    if (c?.id == null || c.names == null || c.names.Count == 0) continue;
                    List<(double, string)> list = new List<(double, string)>();
                    foreach (PeriodDto p in c.names)
                    {
                        if (p != null && !string.IsNullOrWhiteSpace(p.name)) list.Add((p.from, p.name));
                    }

                    list.Sort((a, b) => a.Item1.CompareTo(b.Item1));
                    map[c.id] = list.ToArray();
                }
            }
            catch (JsonException e)
            {
                Debug.LogWarning("[Why] BandAnnotations: the streams' later names were not read (" + e.Message + ")");
            }

            periods = map;
        }
        // streams: priority = base + scale * largest power share; text size grows with importance
        const float StreamPriorityBase = 8f;
        const float StreamPriorityScale = 60f;
        const float HumanityPriority = 45f;
        const float StreamSizeMin = 12f;
        const float StreamSizeRange = 4f;
        const double Tier1Share = 0.12;

        // wars: priority around 6, a little higher for deadlier wars
        const float WarPriorityBase = 4f;
        const float WarPriorityPerDecade = 0.4f;
        const float WarLabelSize = 11f;

        /// <summary>Registers every anchor and label; returns the labels only the width views show (hidden until then).</summary>
        public static List<LabelSpec> Register(GraphContext ctx, HumanWorld world, BandGeometry geometry)
        {
            foreach (StreamInfo s in geometry.Streams) RegisterStream(ctx, world, s);
            RegisterLineage();
            foreach (WarInfo w in geometry.Wars) RegisterWar(ctx, world, w);
            List<LabelSpec> widthLabels = new List<LabelSpec>();
            RegisterPresentEdge(ctx, world, widthLabels);
            return widthLabels;
        }

        /// <summary>
        /// The present edge of the layer: WEST at its inner side and EAST at its outer one, each state alive now at its
        /// band (by today's name), the seam where the Histomap hands over to shares of world GDP, and the way on from the
        /// United States to its economy (Tab, as on the keyboard everywhere).
        /// </summary>
        static void RegisterPresentEdge(GraphContext ctx, HumanWorld world, List<LabelSpec> widthLabels)
        {
            double year = world.NowYear - 0.5;
            float u = DeepTime.Arc(world.NowYear - year), outer = HumanWorld.HumanRho0 + world.LayerWidth(year);
            void Add(LabelSpec spec, bool widthViewsOnly)
            {
                spec.Hidden = widthViewsOnly;
                ctx.Labels.Add(spec);
                if (widthViewsOnly) widthLabels.Add(spec);
            }

            Add(Edge("WEST", new Vector3(u, GraphStyle.HumansY, HumanWorld.HumanRho0), new Vector2(0, -16)), true);
            Add(Edge("EAST", new Vector3(u, GraphStyle.HumansY, outer), new Vector2(0, 16)), true);
            foreach (Civ c in world.Civs)
            {
                if (!c.Extant || c == world.Humanity || !world.TryBand(c, year, out float lo, out float hi)) continue;
                Add(new LabelSpec
                {
                    Text = NameAt(c, year),
                    Plate = PlateColor,
                    Data = new Vector3(u, GraphStyle.HumansY, 0.5f * (lo + hi)),
                    Priority = StatePriority + 10f * Mathf.Clamp01((hi - lo) / Mathf.Max(outer, 1e-3f)),
                    SizePx = 12.5f,
                    Color = GraphStyle.Text,
                    Align = TextAlignmentOptions.Left,
                    PixelOffset = new Vector2(10, 0),
                    AnchorKey = "civ:" + c.Id
                }, true);
                if (c.Id == "united_states")
                {
                    Add(new LabelSpec
                    {
                        Text = "Economy (Tab) \u2192",
                        Plate = PlateColor,
                        Data = new Vector3(u, GraphStyle.HumansY, 0.5f * (lo + hi)),
                        Priority = EconomyPriority,
                        SizePx = 11.5f,
                        Color = Color.Lerp(GraphStyle.Text, GraphStyle.Humans, 0.35f),
                        Align = TextAlignmentOptions.Left,
                        PixelOffset = new Vector2(10, -16),
                        AnchorKey = "civ:" + c.Id
                    }, false);
                }
            }

            float seamU = DeepTime.Arc(world.NowYear - HistomapLastYear);
            Add(new LabelSpec
            {
                Text = "widths: the Histomap to " + HistomapLastYear.ToString("0") + " \u00B7 shares of world GDP from " +
                       GdpFirstYear.ToString("0"),
                Plate = PlateColor,
                Data = new Vector3(seamU, GraphStyle.HumansY, HumanWorld.HumanRho0 + world.LayerWidth(HistomapLastYear)),
                Priority = SeamPriority,
                SizePx = 11f,
                Color = GraphStyle.TextDim,
                Align = TextAlignmentOptions.Center,
                PixelOffset = new Vector2(0, 14)
            }, true);
        }

        static LabelSpec Edge(string text, Vector3 data, Vector2 offset) => new LabelSpec
        {
            Text = text,
            Plate = PlateColor,
            Data = data,
            Priority = AxisPriority,
            SizePx = 13f,
            Color = GraphStyle.Text,
            Align = TextAlignmentOptions.Center,
            PixelOffset = offset
        };

        /// <summary>
        /// The anchor sits on the band where the stream's data begins (its first Histomap slice), so the
        /// director's arrow lands on the stream; it spans the stream's own data years (emergence and
        /// dissolution are drawing, not history).
        /// </summary>
        static void RegisterStream(GraphContext ctx, HumanWorld world, StreamInfo s)
        {
            Civ c = s.Civ;
            bool humanity = c == world.Humanity;
            string blurb = c.Blurb ?? c.Region;
            if (!string.IsNullOrEmpty(c.Source)) blurb = string.IsNullOrEmpty(blurb) ? c.Source : blurb + " (" + c.Source + ")";

            Anchor anchor = new Anchor
            {
                Key = "civ:" + c.Id,
                Label = c.Name,
                Blurb = blurb,
                Level = GraphLevel.Humans,
                YearsAgo = world.NowYear - c.StartYear,
                EndYearsAgo = c.Extant ? 0 : Math.Max(0, world.NowYear - c.EndYear),
                Y = GraphStyle.HumansY,
                Rho = s.AnchorRho,
                Ids = new IdRange(GraphIds.Civ(c.Index), GraphIds.CivEnd(c.Index)),
                Tier = humanity || s.MaxShare > Tier1Share ? 1 : 2
            };
            Anchors.Register(anchor);
            if (!s.HasLabel) return;

            float importance = Mathf.Clamp01((float)s.MaxShare / 0.5f);
            ctx.Labels.Add(new LabelSpec
            {
                Text = c.Name,
                Data = new Vector3(s.LabelU, GraphStyle.HumansY, s.LabelRho),
                Priority = humanity ? HumanityPriority : StreamPriorityBase + StreamPriorityScale * (float)s.MaxShare,
                SizePx = humanity ? StreamSizeMin + StreamSizeRange : StreamSizeMin + StreamSizeRange * importance,
                Color = GraphStyle.Text,
                Align = TextAlignmentOptions.Center,
                AnchorKey = anchor.Key,
                Ids = anchor.Ids
            });
        }

        static void RegisterLineage()
        {
            Anchors.Register(new Anchor
            {
                Key = CivBandsLayer.LineageAnchor,
                Label = "Our path",
                Blurb = "The inner edge of the human layer: Homo sapiens rising out of the tree of life, then the " +
                        "westernmost stream of each age in the Histomap, down to the present. Inside is most " +
                        "relevant to us; everything further out split off along the way.",
                Level = GraphLevel.Humans,
                YearsAgo = HumanWorld.SapiensYearsAgo,
                EndYearsAgo = 0,
                Y = GraphStyle.HumansY,
                Rho = 0,
                Ids = IdRange.Single(CivBandsLayer.LineageId),
                Tier = 1
            });
        }

        static void RegisterWar(GraphContext ctx, HumanWorld world, WarInfo w)
        {
            War war = w.War;
            Anchor anchor = new Anchor
            {
                Key = "war:" + war.id,
                Label = war.name ?? war.id,
                Blurb = war.blurb,
                Level = GraphLevel.Humans,
                YearsAgo = world.NowYear - war.startYear,
                EndYearsAgo = Math.Max(0, world.NowYear - Math.Max(war.endYear, war.startYear)),
                Y = GraphStyle.HumansY + BandGeometry.WarY,
                Rho = w.Rho,
                Ids = IdRange.Single(w.Id),
                Tier = 2
            };
            Anchors.Register(anchor);

            float decades = (float)Math.Log10(Math.Max(war.deaths, 1e4));
            ctx.Labels.Add(new LabelSpec
            {
                Text = anchor.Label,
                Data = anchor.Data,
                Priority = WarPriorityBase + WarPriorityPerDecade * decades,
                SizePx = WarLabelSize,
                Color = GraphStyle.TextDim,
                Align = TextAlignmentOptions.Center,
                PixelOffset = new Vector2(0, 10),
                AnchorKey = anchor.Key,
                Ids = anchor.Ids
            });
        }
    }
}
