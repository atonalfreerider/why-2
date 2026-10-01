using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using Why.Economy.Data;
using Why.Economy.Model;
using Debug = UnityEngine.Debug;
using static Why.Economy.EconomyGeometry;

namespace Why.Economy.Layers
{
    /// <summary>
    /// The money circuit station: a Sankey diagram of one year's money (<see cref="MoneyCircuit"/>) standing in the
    /// station's plane z = 0, read left to right. Industries add value (the tiers of the wall, the whole column is the
    /// year's GDP); the value splits into wages, owners' share, taxes on production and depreciation; the wealth groups,
    /// the state, holders abroad and firms receive it (income and payroll taxes are withheld on the way to the state);
    /// households have it after social benefits and interest, the state's purse what is left of its taxes, and borrowing
    /// what savers lend; and the money is spent on the notebook's categories, public goods, investment and exports.
    ///
    /// The loop closes underground: from every use a glowing line dips under the floor and runs back to the industries
    /// that made what it bought (imports to the world, savings to the lenders), pulses travelling right to left, while
    /// the ribbons above pulse left to right. Behind the first columns stands the capture skyline: the most valuable US
    /// companies as gold towers, height by market value, brightness by net margin.
    ///
    /// The diagram follows <see cref="EconomyState.Year"/>: when it changes the year is rebuilt (meshes, label texts and
    /// positions, anchors). Labels only show while the camera looks at this station (<see cref="LabelRange"/>), and the
    /// station's meshes fade out while the camera is elsewhere, so nothing of it shows in the timeline's views; while
    /// the camera looks at the stations, the timeline's own labels (its end stands in front of this one) step aside.
    /// </summary>
    [GraphScenes(GraphScene.Economy)]
    public sealed class CircuitLayer : GraphLayer
    {
        // ------------------------------------------------------------------ layout (station-local: x -5..5 across, y up)

        /// <summary>
        /// The diagram's proportions for a screen shape: the five columns' x (industries, split, recipients, households,
        /// uses), the height of the industries column (the year's GDP; a year whose tallest column would be higher than
        /// <see cref="MaxColumnHeight"/> is drawn at a smaller scale) and the bottom of the return arcs. A portrait screen
        /// sees about 10.5 units across at the circuit view, so its columns stand closer and taller, a little left of
        /// the middle (the uses' labels on the right are the longest), and its labels are shorter.
        /// </summary>
        sealed class Frame
        {
            public float[] ColumnX;
            public float IndustriesHeight, MaxColumnHeight, ReturnBottom;

            /// <summary>The skyline behind the first columns: left edge, spacing and width of the towers, the tallest.</summary>
            public float SkylineLeft, SkylineStep, TowerWidth, TallestTower;

            /// <summary>
            /// Least height between two node labels of a column (local units: about one label line at the circuit view's
            /// distance, larger in portrait where labels are drawn larger), so a thin node's label is not dropped.
            /// </summary>
            public float LabelSpacing;

            /// <summary>The same for the labels of the skyline's towers (seen closer, in the capture view).</summary>
            public float TowerLabelSpacing;

            /// <summary>
            /// The columns stand closer than a node label is wide: the labels of the middle columns (to the right of
            /// their bars) also keep <see cref="LabelSpacing"/> from those of the column before, which reach over.
            /// </summary>
            public bool LabelsReachNextColumn;

            /// <summary>Labels of the end columns stand this far from their bars, clear of the arcs beside them.</summary>
            public float EndLabelGap;

            public bool Portrait;
        }

        static readonly Frame Landscape = new Frame
        {
            ColumnX = new[] { -4.4f, -2.2f, 0f, 2.2f, 4.4f }, IndustriesHeight = 4.2f, MaxColumnHeight = 5.3f,
            ReturnBottom = -1.2f, SkylineLeft = -5.25f, SkylineStep = 0.19f, TowerWidth = 0.15f, TallestTower = 3f,
            LabelSpacing = 0.22f, TowerLabelSpacing = 0.17f, EndLabelGap = 0.4f
        };

        static readonly Frame Tall = new Frame
        {
            ColumnX = new[] { -2.85f, -1.54f, -0.23f, 1.09f, 2.4f }, IndustriesHeight = 5.6f, MaxColumnHeight = 7f,
            ReturnBottom = -1.6f, SkylineLeft = -3.3f, SkylineStep = 0.11f, TowerWidth = 0.085f, TallestTower = 3.6f,
            LabelSpacing = 0.3f, TowerLabelSpacing = 0.2f, LabelsReachNextColumn = true, EndLabelGap = 0.36f,
            Portrait = true
        };

        /// <summary>In a portrait frame every other column heading stands this much higher (they would touch).</summary>
        const float HeadingStagger = 0.42f;

        /// <summary>Names longer than this are shortened in a portrait frame.</summary>
        const int PortraitNameLength = 16;

        /// <summary>Node bars: width, the gap between two bars of a column, the floor of the tallest column.</summary>
        const float BarWidth = 0.12f, NodeGap = 0.075f, BaseY = 0.3f;

        /// <summary>Shortest bar drawn for a node with money (so a small flow stays visible).</summary>
        const float MinBar = 0.01f;

        /// <summary>Samples along a ribbon.</summary>
        const int RibbonSamples = 24;

        const float RibbonAlpha = 0.3f, RibbonIntensity = 1f, BarAlpha = 0.92f, BarIntensity = 1.5f;

        /// <summary>The ribbons' center lines: pulses per world unit of path and their speed (pulses travel left to right).</summary>
        const float FlowFreq = 1.3f, FlowSpeed = 1.1f;

        /// <summary>
        /// The return arcs: under the floor between <see cref="ReturnTop"/> and the frame's bottom, slightly behind the
        /// diagram (<see cref="ReturnZ"/>); each arc a lane whose depth and turns grow with its order, its width a share
        /// of the ribbon it would be (<see cref="ReturnWidthMax"/>, less when the lanes need it).
        /// </summary>
        const float ReturnZ = 0.6f, ReturnTop = -0.12f, ReturnLaneGap = 0.01f;

        const float ReturnWidthMax = 0.32f;

        /// <summary>
        /// An arc leaves its use to the right by a hook (wider for deeper lanes, so the arcs nest), drops beside the
        /// column, turns into its lane with a corner of radius <see cref="CornerRadius"/> (+ per unit of depth) and rises
        /// beside the column it returns to.
        /// </summary>
        const float HookMin = 0.04f, HookSpread = 0.26f, CornerRadius = 0.12f, CornerPerDepth = 0.2f;

        const int HookSamples = 6, DropSamples = 10, CornerSamples = 8, RunSamples = 10;

        /// <summary>Points of one return arc.</summary>
        const int ArcPoints = 2 * (HookSamples + DropSamples + CornerSamples) + RunSamples + 2;
        const float ReturnAlpha = 0.32f, ReturnIntensity = 0.75f;

        /// <summary>Passes that push a column's crowded node labels apart (see <see cref="Frame.LabelSpacing"/>).</summary>
        const int LabelSpreadPasses = 24;

        /// <summary>The ground line the diagram stands on, across the station.</summary>
        const float GroundHalfWidth = 5.6f;

        /// <summary>
        /// A dark screen behind the station (in the background color) so the stations farther down the road do not show
        /// through the diagram: its depth, half width, bottom and top, and its opacity.
        /// </summary>
        const float BackdropZ = 3.2f, BackdropHalfWidth = 16f, BackdropBottom = -7f, BackdropTop = 13f, BackdropAlpha = 1f;

        /// <summary>
        /// The backdrop is there only while the camera looks at this station (it would stand in the next one's view).
        /// </summary>
        const float BackdropNear = 3.5f, BackdropFar = 6.5f;

        /// <summary>
        /// How much the ribbons and the flow lines recede while the skyline is the subject (the capture view looks at the
        /// towers through the first columns of the diagram).
        /// </summary>
        const float CaptureRibbonAlpha = 0.25f, CaptureLineAlpha = 0.3f;

        /// <summary>Render queues from QueueStations + this: after the other stations' diagrams (see Upload).</summary>
        const int BackdropQueueOffset = 10;

        // ------------------------------------------------------------------ the capture skyline

        /// <summary>Depth (z) of the towers behind the first columns (their spacing and size are the frame's).</summary>
        const float SkylineZ = 2.2f;

        const int MaxTowers = 25, LabeledTowers = 10;

        /// <summary>Tower brightness: HDR intensity at a 0% and a 50% net margin (unknown margins draw dim).</summary>
        const float TowerDim = 0.55f, TowerBright = 2.2f;

        const float TowerAlpha = 0.22f, TowerEdgeAlpha = 0.75f;

        /// <summary>
        /// The skyline's opacity while the Sankey is the subject (it stands behind it, readable through it); full while
        /// the skyline is (see <see cref="CaptureLocal"/>).
        /// </summary>
        const float SkylineDimAlpha = 0.2f;

        /// <summary>
        /// The capture view looks at this local point; the skyline is the subject while the camera's target is near it
        /// (between <see cref="CaptureNear"/> and <see cref="CaptureFar"/>) or behind the diagram, among the towers.
        /// </summary>
        static readonly Vector3 CaptureLocal = new Vector3(-0.22f * EconomyStyle.StationWidth, EconomyStyle.StationHeight * 0.5f, 0);

        const float CaptureNear = 0.6f, CaptureFar = 1.6f, TowersNear = 1f, TowersFar = 1.9f;

        // ------------------------------------------------------------------ visibility

        /// <summary>Fixed labels hide while the camera's target is farther than this from them (world units).</summary>
        const float LabelRange = 14f;

        /// <summary>
        /// The station's meshes fade out while the camera's target is farther than <see cref="FadeNear"/> from the
        /// station's center and are gone beyond <see cref="FadeFar"/> (the next station's views stand 12 away).
        /// </summary>
        const float FadeNear = 8f, FadeFar = 11f;

        static readonly Vector3 CenterLocal = new Vector3(0, EconomyStyle.StationHeight * 0.5f, 0);

        /// <summary>
        /// While the camera's target is less than this far before the station along the road (local z), the camera looks
        /// at the stations, not the road: the timeline's labels, whose end stands between the camera and this diagram,
        /// are hidden (<see cref="LabelSystem.DataLabelsHidden"/>). Half the gap between the road's end and the station.
        /// </summary>
        const float StationsAhead = EconomyStyle.FirstStationGap * 0.5f;

        /// <summary>
        /// Priorities of the skyline's labels: above every node label (they are only shown while the skyline is the
        /// subject, where the towers stand among the first columns' labels), below the diagram's title.
        /// </summary>
        const float CaptionPriority = 48, TowerPriority = 47;

        // ------------------------------------------------------------------ texts

        static readonly string[] Headings =
            { "Industries add value", "Split", "Who receives it", "After taxes and transfers", "What it is spent on" };

        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        // ------------------------------------------------------------------ colors

        static readonly Color Gold = EconomyStyle.Capital;
        static readonly Color GoldDim = new Color(0.85f, 0.62f, 0.25f);
        static readonly Color Blue = EconomyStyle.People;
        static readonly Color Steel = EconomyStyle.Government;
        static readonly Color Neutral = new Color(0.78f, 0.78f, 0.82f);
        static readonly Color World = new Color(0.62f, 0.7f, 0.78f);
        static readonly Color Lending = new Color(0.7f, 0.78f, 0.95f);

        /// <summary>The numbers in node labels: dimmer than the names, bright enough over the ribbons.</summary>
        static readonly Color ValueText = new Color(0.74f, 0.77f, 0.82f);

        public override int Order => 42;

        Station station;
        LabelSystem labelSystem;
        MoneyCircuit circuit;
        CircuitYear shown;
        int seenVersion = -1;
        bool uploaded;

        // the year's geometry (rebuilt), the static frame and skyline (built once)
        SurfaceMeshBuilder pendingFills, skylineFills, backdropFills;
        LineMeshBuilder pendingLines, frameLines, skylineLines;
        MeshFilter fillFilter, lineFilter, backdropFilter, frameFilter, skylineFillFilter, skylineLineFilter;
        List<Company> companies = new List<Company>();
        Frame skylineFrame;
        Material fillMat, lineMat, frameMat, skylineFillMat, skylineLineMat, backdropMat;
        float stationAlpha = -1, skylineAlpha = -1, ribbonAlpha = -1, backdropAlpha = -1;

        // labels and anchors (one per node, kept across years)
        LabelSpec[] nodeLabels;
        Anchor[] nodeAnchors;
        readonly LabelSpec[] headingLabels = new LabelSpec[5];
        LabelSpec titleLabel, subtitleLabel, returnsLabel, captionLabel;
        readonly List<LabelSpec> towerLabels = new List<LabelSpec>(LabeledTowers + 1);
        readonly List<Anchor> towerAnchors = new List<Anchor>(MaxTowers);
        Anchor skylineAnchor;
        readonly List<LabelSpec> labels = new List<LabelSpec>(64);
        Anchor loopAnchor, returnsAnchor;
        bool towersShown, atStations;

        /// <summary>Where the year's nodes and links ended up (station-local), for labels and anchors.</summary>
        sealed class Layout
        {
            /// <summary>World units per $B, and the top of the tallest column.</summary>
            public float Scale, Top;
            public float[] NodeTop, NodeBottom;
            public bool[] Visible;
        }

        Layout layout;
        Frame frame = Landscape;

        // ------------------------------------------------------------------ prepare (worker thread)

        public override void Prepare(GraphContext ctx)
        {
            Stopwatch sw = Stopwatch.StartNew();
            station = EconomyStage.Get(EconomyStage.Circuit);
            labelSystem = ctx.Labels;
            EconomyModel model = ctx.Shared<EconomyModel>(EconomyModel.SharedKey);
            if (model?.Circuit == null || model.Data.Industries.Count == 0)
            {
                Debug.LogWarning("[Why] CircuitLayer: no economy model, circuit skipped");
                return;
            }

            circuit = model.Circuit;
            frame = ScreenLayout.IsPortrait ? Tall : Landscape;
            CircuitYear year = circuit.Build(EconomyState.Year);
            BuildYear(year, out pendingFills, out pendingLines);
            frameLines = new LineMeshBuilder(64);
            DrawFrame(frameLines);
            backdropFills = new SurfaceMeshBuilder();
            DrawBackdrop(backdropFills);
            companies = Companies(model.Data);
            skylineFrame = frame;
            DrawSkyline(frame, companies, out skylineFills, out skylineLines);

            AddLabels(year);
            AddSkylineLabels(model.Data);
            RegisterAnchors(year);
            ApplyYear(year, false);
            PlaceSkyline();
            // handed over placed and ranked: the label system sorts labels by priority when it takes them
            foreach (LabelSpec spec in labels) ctx.Labels.Add(spec);
            foreach (string w in circuit.Warnings) Debug.LogWarning("[Why] money circuit: " + w);
            shown = year;
            Debug.Log($"[Why] CircuitLayer.Prepare {sw.ElapsedMilliseconds} ms: {year.Year}, {year.Nodes.Count} nodes, " +
                      $"{year.Links.Count} links, imbalance {(100 * year.Imbalance).ToString("0.0", Ci)}% at {year.ImbalanceNode}; " +
                      $"{companies.Count} towers");
        }

        /// <summary>The year's Sankey: ribbons, bars and center lines, and the return arcs under the floor.</summary>
        void BuildYear(CircuitYear c, out SurfaceMeshBuilder fills, out LineMeshBuilder lines)
        {
            layout = Place(c, out List<Band> bands, out List<Arc> arcs);
            fills = new SurfaceMeshBuilder();
            lines = new LineMeshBuilder(bands.Count * (RibbonSamples + 2) + arcs.Count * ArcPoints);
            List<Vector3> top = new List<Vector3>(RibbonSamples + 1), bottom = new List<Vector3>(RibbonSamples + 1);
            List<Color32> colors = new List<Color32>(RibbonSamples + 1);
            List<LinePoint> path = new List<LinePoint>(ArcPoints);
            foreach (Band b in bands) DrawBand(c, b, fills, lines, top, bottom, colors, path);
            foreach (Arc a in arcs) DrawArc(c, a, lines, path);
            DrawBars(c, fills);
        }

        // ------------------------------------------------------------------ layout

        /// <summary>A forward link drawn as a ribbon (two pieces when it passes a column: through its slot there).</summary>
        struct Band
        {
            public int Link;
            public float X0, Y0, X1, Y1, Thickness;
            public bool Skips;
            public float SlotX, SlotY;
        }

        /// <summary>A return link drawn as an arc under the floor.</summary>
        struct Arc
        {
            public int Link;
            public float StartX, StartY, EndX, EndY, StartWidth, EndWidth, Width, LaneY, Hook;
        }

        Layout Place(CircuitYear c, out List<Band> bands, out List<Arc> arcs)
        {
            float[] columnX = frame.ColumnX;
            int nodes = c.Nodes.Count, columns = columnX.Length;
            Layout l = new Layout
            {
                NodeTop = new float[nodes], NodeBottom = new float[nodes], Visible = new bool[nodes]
            };

            // money per column, including links that pass through it (they get a slot at its bottom)
            double[] money = new double[columns];
            int[] count = new int[columns];
            List<int>[] slots = new List<int>[columns];
            for (int k = 0; k < columns; k++) slots[k] = new List<int>();
            foreach (CircuitNode n in c.Nodes)
            {
                l.Visible[n.Index] = n.Value > c.Gdp * 1e-5;
                if (!l.Visible[n.Index]) continue;
                money[(int)n.Column] += n.Value;
                count[(int)n.Column]++;
            }

            for (int i = 0; i < c.Links.Count; i++)
            {
                CircuitLink link = c.Links[i];
                if (link.Return) continue;
                int a = (int)c.Nodes[link.From].Column, b = (int)c.Nodes[link.To].Column;
                for (int k = a + 1; k < b; k++)
                {
                    money[k] += link.Value;
                    count[k]++;
                    slots[k].Add(i);
                }
            }

            float scale = c.Gdp > 0 ? frame.IndustriesHeight / (float)c.Gdp : 0;
            for (int k = 0; k < columns; k++)
            {
                float gaps = NodeGap * Math.Max(0, count[k] - 1);
                if (money[k] > 0) scale = Mathf.Min(scale, (frame.MaxColumnHeight - gaps) / (float)money[k]);
            }

            l.Scale = scale;
            float[] height = new float[columns];
            for (int k = 0; k < columns; k++)
            {
                height[k] = (float)money[k] * scale + NodeGap * Math.Max(0, count[k] - 1);
            }

            float tallest = 0;
            foreach (float h in height) tallest = Mathf.Max(tallest, h);
            float mid = BaseY + tallest * 0.5f;
            l.Top = BaseY + tallest;

            // stack each column from its top (nodes in list order, then the slots of passing links)
            float[] slotTop = new float[c.Links.Count];
            for (int k = 0; k < columns; k++)
            {
                float y = mid + height[k] * 0.5f;
                foreach (CircuitNode n in c.Nodes)
                {
                    if ((int)n.Column != k || !l.Visible[n.Index]) continue;
                    float h = Mathf.Max(MinBar, (float)n.Value * scale);
                    l.NodeTop[n.Index] = y;
                    l.NodeBottom[n.Index] = y - h;
                    y -= h + NodeGap;
                }

                slots[k].Sort((p, q) => SourceY(c, l, p).CompareTo(SourceY(c, l, q)) * -1);
                foreach (int i in slots[k])
                {
                    slotTop[i] = y;
                    y -= (float)c.Links[i].Value * scale + NodeGap;
                }
            }

            // attach the links along their nodes: outgoing on the right, incoming on the left, each sorted by the other
            // end's height so ribbons cross as little as they can; returns below the forward links
            float[] outCursor = new float[nodes], inCursor = new float[nodes];
            for (int i = 0; i < nodes; i++) outCursor[i] = inCursor[i] = l.NodeTop[i];
            List<int> order = new List<int>(c.Links.Count);
            for (int i = 0; i < c.Links.Count; i++) order.Add(i);

            bands = new List<Band>(c.Links.Count);
            arcs = new List<Arc>(c.Links.Count / 2);
            float[] startY = new float[c.Links.Count], endY = new float[c.Links.Count];

            // outgoing: forward links by their target's height, then returns by their target's height
            order.Sort((p, q) => OutKey(c, l, slotTop, p).CompareTo(OutKey(c, l, slotTop, q)));
            foreach (int i in order)
            {
                CircuitLink link = c.Links[i];
                if (!l.Visible[link.From]) continue;
                float t = (float)link.Value * scale;
                startY[i] = outCursor[link.From];
                outCursor[link.From] -= t;
            }

            order.Sort((p, q) => InKey(c, l, p).CompareTo(InKey(c, l, q)));
            foreach (int i in order)
            {
                CircuitLink link = c.Links[i];
                if (!l.Visible[link.To]) continue;
                float t = (float)link.Value * scale;
                endY[i] = inCursor[link.To];
                inCursor[link.To] -= t;
            }

            float half = BarWidth * 0.5f;
            for (int i = 0; i < c.Links.Count; i++)
            {
                CircuitLink link = c.Links[i];
                if (!l.Visible[link.From] || !l.Visible[link.To]) continue;
                CircuitNode from = c.Nodes[link.From], to = c.Nodes[link.To];
                float t = (float)link.Value * scale;
                if (link.Return)
                {
                    arcs.Add(new Arc
                    {
                        Link = i,
                        StartX = columnX[(int)from.Column] + half, StartY = startY[i] - t * 0.5f,
                        EndX = columnX[(int)to.Column] - half, EndY = endY[i] - t * 0.5f,
                        StartWidth = t, EndWidth = t
                    });
                    continue;
                }

                int a = (int)from.Column, b = (int)to.Column;
                bands.Add(new Band
                {
                    Link = i, X0 = columnX[a] + half, Y0 = startY[i], X1 = columnX[b] - half, Y1 = endY[i], Thickness = t,
                    Skips = b > a + 1, SlotX = b > a + 1 ? columnX[a + 1] : 0, SlotY = b > a + 1 ? slotTop[i] : 0
                });
            }

            LayLanes(arcs, frame.ReturnBottom);
            return l;
        }

        static float SourceY(CircuitYear c, Layout l, int link) => l.NodeTop[c.Links[link].From];

        /// <summary>Sort key of a link at its source: forward links first by the target's (or slot's) height, then returns.</summary>
        static float OutKey(CircuitYear c, Layout l, float[] slotTop, int i)
        {
            CircuitLink link = c.Links[i];
            int from = (int)c.Nodes[link.From].Column, to = (int)c.Nodes[link.To].Column;
            float y = to > from + 1 ? slotTop[i] : 0.5f * (l.NodeTop[link.To] + l.NodeBottom[link.To]);
            return (link.Return ? 1000f : 0f) - y;
        }

        /// <summary>Sort key of a link at its target: forward links by the source's height, then returns.</summary>
        static float InKey(CircuitYear c, Layout l, int i)
        {
            CircuitLink link = c.Links[i];
            float y = 0.5f * (l.NodeTop[link.From] + l.NodeBottom[link.From]);
            return (link.Return ? 1000f : 0f) - y;
        }

        /// <summary>
        /// Gives every return arc its lane under the floor: the arc that leaves highest takes the deepest lane and the
        /// widest turns, so the arcs nest where they leave; widths shrink together if the lanes need more depth.
        /// </summary>
        static void LayLanes(List<Arc> arcs, float bottom)
        {
            arcs.Sort((p, q) => q.StartY.CompareTo(p.StartY));
            float total = 0;
            foreach (Arc a in arcs) total += a.StartWidth * ReturnWidthMax;
            float room = ReturnTop - bottom - ReturnLaneGap * Math.Max(0, arcs.Count - 1);
            float squeeze = total > room && total > 0 ? room / total : 1;
            float y = ReturnTop;
            for (int i = arcs.Count - 1; i >= 0; i--)
            {
                Arc a = arcs[i];
                a.Width = Mathf.Max(0.004f, a.StartWidth * ReturnWidthMax * squeeze);
                a.LaneY = y - a.Width * 0.5f;
                y -= a.Width + ReturnLaneGap;
                arcs[i] = a;
            }

            float deepest = Mathf.Max(ReturnTop - y, 1e-3f);
            for (int i = 0; i < arcs.Count; i++)
            {
                Arc a = arcs[i];
                a.Hook = HookMin + HookSpread * (ReturnTop - a.LaneY) / deepest;
                arcs[i] = a;
            }
        }

        // ------------------------------------------------------------------ drawing

        /// <summary>
        /// A forward link: its ribbon (through the slot of a column it passes) and the center line its pulses run on.
        /// </summary>
        void DrawBand(CircuitYear c, Band b, SurfaceMeshBuilder fills, LineMeshBuilder lines, List<Vector3> top,
            List<Vector3> bottom, List<Color32> colors, List<LinePoint> path)
        {
            CircuitLink link = c.Links[b.Link];
            Color hue = LinkColor(c, link);
            int id = EconomyIds.CircuitLink + b.Link;
            Color32 fill = Tint(hue, RibbonAlpha);
            path.Clear();
            if (!b.Skips)
            {
                Ribbon(b.X0, b.Y0, b.X1, b.Y1, b.Thickness, fill, id, fills, top, bottom, colors, path, hue);
            }
            else
            {
                // through the slot of the column it passes: two ribbons meeting flat at the slot
                Ribbon(b.X0, b.Y0, b.SlotX, b.SlotY, b.Thickness, fill, id, fills, top, bottom, colors, path, hue);
                path.RemoveAt(path.Count - 1);
                Ribbon(b.SlotX, b.SlotY, b.X1, b.Y1, b.Thickness, fill, id, fills, top, bottom, colors, path, hue);
            }

            lines.AddFlowPath(path, id);
        }

        /// <summary>
        /// A ribbon of constant thickness from (x0, y0 top) to (x1, y1 top), its height eased with a smoothstep; appends
        /// its center line to <paramref name="path"/>.
        /// </summary>
        static void Ribbon(float x0, float y0, float x1, float y1, float thickness, Color32 fill, int id,
            SurfaceMeshBuilder fills, List<Vector3> top, List<Vector3> bottom, List<Color32> colors, List<LinePoint> path,
            Color hue)
        {
            top.Clear();
            bottom.Clear();
            colors.Clear();
            Color32 line = Tint(Color.Lerp(hue, Color.white, 0.2f), 0.32f);
            float width = Mathf.Clamp(thickness * 0.08f, 0.003f, 0.02f);
            for (int s = 0; s <= RibbonSamples; s++)
            {
                float u = s / (float)RibbonSamples;
                float e = u * u * (3 - 2 * u);
                float x = Mathf.Lerp(x0, x1, u), y = Mathf.Lerp(y0, y1, e);
                top.Add(new Vector3(x, y, 0));
                bottom.Add(new Vector3(x, y - thickness, 0));
                colors.Add(fill);
                path.Add(new LinePoint(new Vector3(x, y - thickness * 0.5f, -0.002f), line, 0.6f, width, 1f));
            }

            fills.AddBand(top, bottom, colors, id, RibbonIntensity);
        }

        /// <summary>
        /// A return arc: out of the use to the right (its hook), down beside the column while it moves back under the
        /// floor (z), round a corner into its lane, along the lane to the left, round a corner up beside the column it
        /// returns to and in from the left. Its width eases from the ribbon it would be to its lane's width.
        /// </summary>
        void DrawArc(CircuitYear c, Arc a, LineMeshBuilder lines, List<LinePoint> path)
        {
            CircuitLink link = c.Links[a.Link];
            Color32 color = Tint(LinkColor(c, link), ReturnAlpha);
            float depth = ReturnTop - a.LaneY;
            float corner = CornerRadius + depth * CornerPerDepth;
            float xr = a.StartX + a.Hook, xl = a.EndX - a.Hook;
            float startWidth = Mathf.Min(a.StartWidth * ReturnWidthMax, a.Width * 2.5f);
            float endWidth = Mathf.Min(a.EndWidth * ReturnWidthMax, a.Width * 2.5f);
            float hookR = Mathf.Min(a.Hook, Mathf.Max(0.01f, (a.StartY - a.LaneY - corner) * 0.5f));
            float hookL = Mathf.Min(a.Hook, Mathf.Max(0.01f, (a.EndY - a.LaneY - corner) * 0.5f));
            path.Clear();

            // the hook out of the use: from heading right to heading down
            for (int s = 0; s <= HookSamples; s++)
            {
                float q = s / (float)HookSamples * Mathf.PI * 0.5f;
                float x = a.StartX + (a.Hook - hookR) + hookR * Mathf.Sin(q);
                float y = a.StartY - hookR * (1 - Mathf.Cos(q));
                Add(path, new Vector3(s == 0 ? a.StartX : x, y, 0), color, startWidth);
            }

            // down beside the column, moving back under the floor
            float dropTop = a.StartY - hookR, dropBottom = a.LaneY + corner;
            for (int s = 1; s <= DropSamples; s++)
            {
                float u = s / (float)DropSamples;
                float y = Mathf.Lerp(dropTop, dropBottom, u);
                Add(path, new Vector3(xr, y, ReturnZ * Smooth(u)), color, Mathf.Lerp(startWidth, a.Width, u));
            }

            // the corner into the lane, the lane, the corner up
            for (int s = 1; s <= CornerSamples; s++)
            {
                float q = s / (float)CornerSamples * Mathf.PI * 0.5f;
                Vector3 p = new Vector3(xr - corner * (1 - Mathf.Cos(q)), a.LaneY + corner * (1 - Mathf.Sin(q)), ReturnZ);
                Add(path, p, color, a.Width);
            }

            for (int s = 1; s < RunSamples; s++)
            {
                float x = Mathf.Lerp(xr - corner, xl + corner, s / (float)RunSamples);
                Add(path, new Vector3(x, a.LaneY, ReturnZ), color, a.Width);
            }

            for (int s = 0; s < CornerSamples; s++)
            {
                float q = s / (float)CornerSamples * Mathf.PI * 0.5f;
                Vector3 p = new Vector3(xl + corner * (1 - Mathf.Sin(q)), a.LaneY + corner * (1 - Mathf.Cos(q)), ReturnZ);
                Add(path, p, color, a.Width);
            }

            // up beside the column it returns to, coming forward out of the floor, and the hook into it
            float riseBottom = a.LaneY + corner, riseTop = a.EndY - hookL;
            for (int s = 0; s < DropSamples; s++)
            {
                float u = s / (float)DropSamples;
                float y = Mathf.Lerp(riseBottom, riseTop, u);
                Add(path, new Vector3(xl, y, ReturnZ * (1 - Smooth(u))), color, Mathf.Lerp(a.Width, endWidth, u));
            }

            for (int s = 0; s <= HookSamples; s++)
            {
                float q = s / (float)HookSamples * Mathf.PI * 0.5f;
                float x = xl + hookL * (1 - Mathf.Cos(q));
                float y = riseTop + hookL * Mathf.Sin(q);
                Add(path, new Vector3(s == HookSamples ? a.EndX : x, y, 0), color, endWidth);
            }

            lines.AddFlowPath(path, EconomyIds.CircuitLink + a.Link);
        }

        static void Add(List<LinePoint> path, Vector3 p, Color32 color, float width) =>
            path.Add(new LinePoint(p, color, 0.7f, width, ReturnIntensity));

        static float Smooth(float u) => u * u * (3 - 2 * u);

        /// <summary>A thin bar per node with money, in the node's hue, drawn over the ribbons' ends.</summary>
        void DrawBars(CircuitYear c, SurfaceMeshBuilder fills)
        {
            float half = BarWidth * 0.5f;
            foreach (CircuitNode n in c.Nodes)
            {
                if (!layout.Visible[n.Index]) continue;
                float x = frame.ColumnX[(int)n.Column], top = layout.NodeTop[n.Index], bottom = layout.NodeBottom[n.Index];
                Color32 color = Tint(NodeColor(n), BarAlpha);
                int id = EconomyIds.CircuitNode + n.Index;
                Vector3 a = new Vector3(x - half, bottom, -0.004f), b = new Vector3(x + half, bottom, -0.004f);
                Vector3 d = new Vector3(x - half, top, -0.004f), e = new Vector3(x + half, top, -0.004f);
                fills.AddTriangle(a, b, e, color, id, BarIntensity);
                fills.AddTriangle(a, e, d, color, id, BarIntensity);
            }
        }

        /// <summary>The ground the diagram stands on and its faint shadow line under which the money returns.</summary>
        static void DrawFrame(LineMeshBuilder lines)
        {
            lines.AddSegment(new Vector3(-GroundHalfWidth, 0, 0), new Vector3(GroundHalfWidth, 0, 0),
                Tint(GraphStyle.AxisDim, 0.55f), 1f, 0, GraphIds.None);
            lines.AddSegment(new Vector3(-GroundHalfWidth, 0, ReturnZ), new Vector3(GroundHalfWidth, 0, ReturnZ),
                Tint(GraphStyle.AxisDim, 0.25f), 0.8f, 0, GraphIds.None);
        }

        /// <summary>The dark screen behind the station (see <see cref="BackdropZ"/>).</summary>
        static void DrawBackdrop(SurfaceMeshBuilder fills)
        {
            Color32 dark = Tint(GraphStyle.Background, BackdropAlpha);
            const float w = BackdropHalfWidth;
            Vector3 a = new Vector3(-w, BackdropBottom, BackdropZ), b = new Vector3(w, BackdropBottom, BackdropZ);
            Vector3 d = new Vector3(-w, BackdropTop, BackdropZ), e = new Vector3(w, BackdropTop, BackdropZ);
            fills.AddTriangle(a, b, e, dark, GraphIds.None);
            fills.AddTriangle(a, e, d, dark, GraphIds.None);
        }

        /// <summary>
        /// A node's hue: capital gold (depreciation and reinvestment a darker gold), people blue, government steel, raw
        /// matter red, spending by its motive (desire rose .. fear ice), the world and lending pale greys.
        /// </summary>
        Color NodeColor(CircuitNode n)
        {
            switch (n.Level)
            {
                case "people": return Blue;
                case "gov": return Steel;
                case "matter": return EconomyStyle.Matter;
                case "life": return EconomyStyle.Life;
                case "capital": return n.Kind == "depreciation" || n.Kind == "reinvest" ? GoldDim : Gold;
                case "desire":
                case "fear":
                    return EconomyStyle.Motive(n.FearShare);
                default:
                    return n.Kind == "credit" || n.Kind == "borrowing" ? Lending : World;
            }
        }

        /// <summary>The hue of a link: the kind of money it carries (the split by what it becomes).</summary>
        Color LinkColor(CircuitYear c, CircuitLink link)
        {
            switch (link.Kind)
            {
                case "value": return NodeColor(c.Nodes[link.To]);
                case "wages":
                case "keep" when c.Nodes[link.From].Kind == "group":
                    return Blue;
                case "owners":
                case "payout":
                case "invest":
                    return Gold;
                case "retained": return GoldDim;
                case "tax":
                case "transfer":
                case "interest":
                case "public":
                case "repay":
                case "keep":
                    return Steel;
                case "spend":
                case "save":
                    return EconomyStyle.Motive(link.FearShare);
                case "credit":
                case "lend":
                    return c.Nodes[link.From].Kind == "saving" ? EconomyStyle.Motive(link.FearShare) : Lending;
                case "export":
                case "import":
                    return World;
                case "return":
                    return ReturnColor(c.Nodes[link.From], link);
                default: return Neutral;
            }
        }

        /// <summary>Returns: spending by its motive, public goods steel, investment gold, exports the world's grey.</summary>
        static Color ReturnColor(CircuitNode from, CircuitLink link)
        {
            switch (from.Kind)
            {
                case "public": return Steel;
                case "investment": return Gold;
                case "exports": return World;
                default: return EconomyStyle.Motive(link.FearShare >= 0 ? link.FearShare : from.FearShare);
            }
        }

        // ------------------------------------------------------------------ the capture skyline

        static List<Company> Companies(EconomyData data)
        {
            List<Company> list = new List<Company>();
            if (data.Circuit?.Capture != null)
            {
                foreach (Company co in data.Circuit.Capture)
                {
                    if (co != null && co.MarketCap > 0 && !string.IsNullOrEmpty(co.Name)) list.Add(co);
                }
            }

            list.Sort((a, b) => b.MarketCap.CompareTo(a.MarketCap));
            if (list.Count > MaxTowers) list.RemoveRange(MaxTowers, list.Count - MaxTowers);
            return list;
        }

        static float TowerX(Frame f, int i) => f.SkylineLeft + f.TowerWidth * 0.5f + i * f.SkylineStep;

        float TowerHeight(Frame f, int i) =>
            companies.Count > 0 && companies[0].MarketCap > 0
                ? (float)(companies[i].MarketCap / companies[0].MarketCap) * f.TallestTower
                : 0;

        /// <summary>Net income / revenue, or -1 when unknown.</summary>
        static float Margin(Company co) => co.Revenue > 0 && co.NetIncome != 0 ? (float)(co.NetIncome / co.Revenue) : -1;

        /// <summary>
        /// One tower per company, standing on the floor behind the first columns: a faint gold face, its outline, and a
        /// top whose glow is the company's net margin (what it keeps of each dollar it sells).
        /// </summary>
        void DrawSkyline(Frame f, List<Company> list, out SurfaceMeshBuilder fills, out LineMeshBuilder lines)
        {
            fills = new SurfaceMeshBuilder();
            lines = new LineMeshBuilder(MaxTowers * 12);
            List<Vector3> pts = new List<Vector3>(5);
            for (int i = 0; i < list.Count; i++)
            {
                float x = TowerX(f, i), h = TowerHeight(f, i), w = f.TowerWidth * 0.5f;
                float margin = Margin(list[i]);
                float glow = margin < 0 ? TowerDim : Mathf.Lerp(TowerDim, TowerBright, Mathf.Clamp01(margin / 0.5f));
                int id = EconomyIds.Capture + i;
                Vector3 a = new Vector3(x - w, 0, SkylineZ), b = new Vector3(x + w, 0, SkylineZ);
                Vector3 d = new Vector3(x - w, h, SkylineZ), e = new Vector3(x + w, h, SkylineZ);
                Color32 face = Tint(Gold, TowerAlpha * (margin < 0 ? 0.6f : 1f));
                fills.AddTriangle(a, b, e, face, id, glow);
                fills.AddTriangle(a, e, d, face, id, glow);
                pts.Clear();
                pts.Add(a);
                pts.Add(d);
                pts.Add(e);
                pts.Add(b);
                lines.AddPolyline(pts, Tint(GoldDim, TowerEdgeAlpha * 0.6f), 0.9f, 0, id, 0.9f);
                lines.AddSegment(d, e, Tint(Gold, TowerEdgeAlpha), 1.6f, 0.012f, id, glow * 1.3f);
            }
        }

        /// <summary>Labels of the most valuable companies, the skyline's caption and an anchor per tower (placed later).</summary>
        void AddSkylineLabels(EconomyData data)
        {
            if (companies.Count == 0) return;
            double total = 0;
            foreach (Company co in companies) total += co.MarketCap;
            IdRange all = new IdRange(EconomyIds.Capture, EconomyIds.Capture + companies.Count - 1);
            string dim = "<color=#" + Hex(ValueText) + ">";
            for (int i = 0; i < companies.Count && i < LabeledTowers; i++)
            {
                Company co = companies[i];
                float margin = Margin(co);
                string text = co.Name + "  " + dim + MoneyCircuit.Money(co.MarketCap) +
                              (margin >= 0 ? " · " + Percent(margin) + " margin" : "") + "</color>";
                LabelSpec spec = Label(text, Vector3.zero, 11, GraphStyle.Text, TextAlignmentOptions.Left, TowerPriority - i,
                    "capture:" + Key(co.Name), IdRange.Single(EconomyIds.Capture + i), new Vector2(4, 6));
                spec.Hidden = true; // shown while the skyline is the subject (Tick)
                labels.Add(spec);
                towerLabels.Add(spec);
            }

            double market = data.Circuit.MarketCapTotal, share = data.Circuit.Top10MarketCapShare;
            string caption = $"The {companies.Count.ToString(Ci)} most valuable US companies: {MoneyCircuit.Money(total)}" +
                             (market > 0 ? $" of the {MoneyCircuit.Money(market)} stock market" : "") +
                             (share > 0 ? $"; the top 10 hold {Percent((float)share)}" : "");
            captionLabel = Label(caption, Vector3.zero, 12, Gold, TextAlignmentOptions.Center, CaptionPriority, "capture:skyline",
                all);
            captionLabel.Hidden = true;
            labels.Add(captionLabel);
            towerLabels.Add(captionLabel);

            for (int i = 0; i < companies.Count; i++)
            {
                Company co = companies[i];
                float margin = Margin(co);
                Industry ind = data.IndustryById(co.Industry);
                Anchor a = new Anchor
                {
                    Key = "capture:" + Key(co.Name),
                    Label = co.Name,
                    Blurb = $"{co.Name}: worth {MoneyCircuit.Money(co.MarketCap)} on the stock market" +
                            (co.Revenue > 0
                                ? $"; sales {MoneyCircuit.Money(co.Revenue)}, net income {MoneyCircuit.Money(co.NetIncome)} " +
                                  $"({Percent(margin)} of each dollar it sells)"
                                : "") +
                            (ind != null ? $". Industry: {ind.Name}." : "."),
                    Level = GraphLevel.Humans,
                    YearsAgo = 0.5,
                    EndYearsAgo = 0,
                    Rho = EconomyStyle.FramingRho,
                    Ids = IdRange.Single(EconomyIds.Capture + i),
                    Tier = i < LabeledTowers ? 1 : 2
                };
                towerAnchors.Add(a);
                Anchors.Register(a);
            }

            skylineAnchor = new Anchor
            {
                Key = "capture:skyline",
                Label = "Where value is captured",
                Blurb = caption + ". Height is market value, the glow at the top the net margin: what the company keeps of " +
                        "each dollar it sells. A few companies are valued at more than whole industries add in a year.",
                Level = GraphLevel.Humans,
                YearsAgo = 0.5,
                EndYearsAgo = 0,
                Rho = EconomyStyle.FramingRho,
                Ids = all,
                Tier = 1
            };
            Anchors.Register(skylineAnchor);
        }

        /// <summary>Puts the skyline's labels and anchors on the towers of the current frame.</summary>
        void PlaceSkyline()
        {
            if (companies.Count == 0) return;
            Frame f = skylineFrame ?? frame;

            // each label at its tower's top, pushed apart where two towers are nearly as tall (their labels would collide)
            int labeled = towerLabels.Count - 1;
            float[] labelY = new float[labeled];
            for (int i = 0; i < labeled; i++) labelY[i] = TowerHeight(f, i);
            for (int pass = 0; pass < LabelSpreadPasses; pass++)
            {
                bool moved = false;
                for (int i = 1; i < labeled; i++)
                {
                    float overlap = f.TowerLabelSpacing - (labelY[i - 1] - labelY[i]);
                    if (overlap <= 1e-4f) continue;
                    labelY[i - 1] += overlap * 0.5f;
                    labelY[i] -= overlap * 0.5f;
                    moved = true;
                }

                if (!moved) break;
            }

            for (int i = 0; i < towerAnchors.Count; i++)
            {
                float h = TowerHeight(f, i);
                if (i < labeled) towerLabels[i].Data = station.World(TowerX(f, i) + f.TowerWidth * 0.5f, labelY[i], SkylineZ);
                towerAnchors[i].Y = h * 0.5f;
                towerAnchors[i].Fixed = station.World(TowerX(f, i), h * 0.5f, SkylineZ);
            }

            float mid = 0.5f * (TowerX(f, 0) + TowerX(f, companies.Count - 1));
            captionLabel.Data = station.World(mid, f.TallestTower + 0.5f, SkylineZ);
            skylineAnchor.Y = f.TallestTower * 0.5f;
            skylineAnchor.Fixed = station.World(mid, f.TallestTower * 0.5f, SkylineZ);
        }

        /// <summary>An anchor key part from a name: lower case, letters and digits, words joined by '_'.</summary>
        static string Key(string name)
        {
            StringBuilder s = new StringBuilder(name.Length);
            bool gap = false;
            foreach (char ch in name)
            {
                if (char.IsLetterOrDigit(ch))
                {
                    if (gap && s.Length > 0) s.Append('_');
                    s.Append(char.ToLowerInvariant(ch));
                    gap = false;
                }
                else gap = true;
            }

            return s.ToString();
        }

        // ------------------------------------------------------------------ labels and anchors

        void AddLabels(CircuitYear c)
        {
            nodeLabels = new LabelSpec[c.Nodes.Count];
            foreach (CircuitNode n in c.Nodes)
            {
                bool left = n.Column == CircuitColumn.Industries;
                bool end = n.Column == CircuitColumn.Industries || n.Column == CircuitColumn.Uses;
                TextAlignmentOptions align = left ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
                LabelSpec spec = Label("", Vector3.zero, end ? 12 : 11, GraphStyle.Text, align, 10, "circuit:" + n.Id,
                    IdRange.Single(EconomyIds.CircuitNode + n.Index));
                spec.Text = NodeText(c, n);
                nodeLabels[n.Index] = spec;
                labels.Add(spec);
            }

            IdRange all = new IdRange(EconomyIds.CircuitNode, EconomyIds.CircuitLink + 999);
            for (int k = 0; k < Headings.Length; k++)
            {
                headingLabels[k] = Label(Headings[k], Vector3.zero, 12, GraphStyle.TextDim, TextAlignmentOptions.Center, 40,
                    "circuit:loop", all);
                labels.Add(headingLabels[k]);
            }

            titleLabel = Label(TitleText(c), Vector3.zero, 16, GraphStyle.Text, TextAlignmentOptions.Center, 50, "circuit:loop", all);
            subtitleLabel = Label(SubtitleText(c), Vector3.zero, 12, GraphStyle.TextDim, TextAlignmentOptions.Center, 45,
                "circuit:loop", all);
            returnsLabel = Label("The money returns to the industries that made what it bought", Vector3.zero, 12,
                GraphStyle.TextDim, TextAlignmentOptions.Center, 35, "circuit:returns",
                new IdRange(EconomyIds.CircuitLink, EconomyIds.CircuitLink + 999));
            labels.Add(titleLabel);
            labels.Add(subtitleLabel);
            labels.Add(returnsLabel);
        }

        /// <summary>A label at a local point of the station (a world position, shown only near the station).</summary>
        LabelSpec Label(string text, Vector3 local, float size, Color color, TextAlignmentOptions align, float priority,
            string anchor, IdRange ids, Vector2 offset = default) =>
            new LabelSpec
            {
                Text = text,
                Data = station.World(local),
                Fixed = true,
                FixedRange = LabelRange,
                SizePx = size,
                Color = color,
                Align = align,
                Priority = priority,
                AnchorKey = anchor,
                Ids = ids,
                PixelOffset = offset
            };

        void RegisterAnchors(CircuitYear c)
        {
            nodeAnchors = new Anchor[c.Nodes.Count];
            foreach (CircuitNode n in c.Nodes)
            {
                Anchor a = new Anchor
                {
                    Key = "circuit:" + n.Id,
                    Label = n.Name,
                    Level = GraphLevel.Humans,
                    YearsAgo = 0.5,
                    EndYearsAgo = 0,
                    Rho = EconomyStyle.FramingRho,
                    Ids = IdRange.Single(EconomyIds.CircuitNode + n.Index),
                    Tier = n.Column == CircuitColumn.Industries || n.Column == CircuitColumn.Uses ? 1 : 2
                };
                nodeAnchors[n.Index] = a;
                Anchors.Register(a);
            }

            loopAnchor = new Anchor
            {
                Key = "circuit:loop", Label = "The money circuit", Level = GraphLevel.Humans, YearsAgo = 0.5, EndYearsAgo = 0,
                Rho = EconomyStyle.FramingRho, Ids = new IdRange(EconomyIds.CircuitNode, EconomyIds.CircuitLink + 999), Tier = 1
            };
            returnsAnchor = new Anchor
            {
                Key = "circuit:returns", Label = "The loop closes", Level = GraphLevel.Humans, YearsAgo = 0.5, EndYearsAgo = 0,
                Rho = EconomyStyle.FramingRho, Ids = new IdRange(EconomyIds.CircuitLink, EconomyIds.CircuitLink + 999), Tier = 2
            };
            Anchors.Register(loopAnchor);
            Anchors.Register(returnsAnchor);
        }

        /// <summary>
        /// Points every label and anchor at the year's layout and gives them the year's texts (on the main thread through
        /// the label system, so shown labels re-measure; in Prepare directly).
        /// </summary>
        void ApplyYear(CircuitYear c, bool live)
        {
            float half = BarWidth * 0.5f;
            float[] labelY = SpreadLabels(c);
            foreach (CircuitNode n in c.Nodes)
            {
                LabelSpec spec = nodeLabels[n.Index];
                bool visible = layout.Visible[n.Index];
                float x = frame.ColumnX[(int)n.Column];
                float y = visible ? 0.5f * (layout.NodeTop[n.Index] + layout.NodeBottom[n.Index]) : 0;
                bool left = n.Column == CircuitColumn.Industries;
                bool end = left || n.Column == CircuitColumn.Uses;
                float gap = end ? frame.EndLabelGap : half + 0.05f;
                spec.Data = station.World(left ? x - gap : x + gap, labelY[n.Index], -0.01f);
                if (!live) spec.Priority = Priority(c, n); // the label system ranks labels once, when it takes them
                spec.Hidden = !visible;
                SetText(spec, NodeText(c, n), live);

                Anchor a = nodeAnchors[n.Index];
                a.Blurb = n.Blurb;
                a.Y = y;
                a.YearsAgo = a.EndYearsAgo = Math.Max(0.5, DeepTime.NowYear - c.Year);
                a.Fixed = station.World(x, y, 0);
            }

            float headingY = layout.Top + 0.3f;
            for (int k = 0; k < headingLabels.Length; k++)
            {
                float stagger = frame.Portrait && k % 2 == 1 ? HeadingStagger : 0;
                headingLabels[k].Data = station.World(frame.ColumnX[k], headingY + stagger, 0);
            }

            float above = headingY + (frame.Portrait ? HeadingStagger : 0);
            titleLabel.Data = station.World(0, above + 0.75f, 0);
            subtitleLabel.Data = station.World(0, above + 0.42f, 0);
            SetText(titleLabel, TitleText(c), live);
            SetText(subtitleLabel, SubtitleText(c), live);
            returnsLabel.Data = station.World(0, frame.ReturnBottom - 0.22f, ReturnZ);

            loopAnchor.Blurb = $"The money circuit of {c.Year.ToString(Ci)}: {MoneyCircuit.Money(c.Gdp)} of value added flows " +
                               "to workers, owners, the state and the world, then to households and back to the industries " +
                               "through what people buy. " + FirstLine(c.Notes);
            loopAnchor.Y = CenterLocal.y;
            loopAnchor.Fixed = station.World(CenterLocal);
            returnsAnchor.Blurb = "Every dollar spent returns to the industries that made what it bought, along their supply " +
                                  "chains: necessities and health care mostly to services, investment to construction, " +
                                  "manufacturing and software, imports to the rest of the world, savings to the borrowers. " +
                                  $"Largest gap in {c.Year.ToString(Ci)}: {(100 * c.Imbalance).ToString("0.0", Ci)}% of GDP.";
            returnsAnchor.Y = 0.5f * (ReturnTop + frame.ReturnBottom);
            returnsAnchor.Fixed = station.World(0, returnsAnchor.Y, ReturnZ);
            if (live) labelSystem.MarkDirty();
        }

        /// <summary>
        /// Heights of the node labels: each at its bar's middle, pushed apart within its column (equally up and down)
        /// until neighbours are at least <see cref="Frame.LabelSpacing"/> apart, so a thin node keeps its label; where
        /// labels reach over the next column (<see cref="Frame.LabelsReachNextColumn"/>), a middle column's labels also
        /// step out of the way of the previous column's.
        /// </summary>
        float[] SpreadLabels(CircuitYear c)
        {
            float[] y = new float[c.Nodes.Count];
            float spacing = frame.LabelSpacing;
            List<int> column = new List<int>(c.Nodes.Count), before = new List<int>(c.Nodes.Count);
            int last = frame.ColumnX.Length - 1;
            for (int k = 0; k <= last; k++)
            {
                before.Clear();
                if (frame.LabelsReachNextColumn && k > (int)CircuitColumn.Income && k < last) before.AddRange(column);
                column.Clear();
                foreach (CircuitNode n in c.Nodes)
                {
                    if ((int)n.Column != k || !layout.Visible[n.Index]) continue;
                    y[n.Index] = 0.5f * (layout.NodeTop[n.Index] + layout.NodeBottom[n.Index]);
                    column.Add(n.Index); // stacked in list order: top to bottom
                }

                for (int pass = 0; pass < LabelSpreadPasses; pass++)
                {
                    bool moved = false;
                    foreach (int i in column)
                    {
                        foreach (int j in before)
                        {
                            float d = y[i] - y[j];
                            if (Mathf.Abs(d) >= spacing - 1e-4f) continue;
                            y[i] += (d >= 0 ? 1 : -1) * (spacing - Mathf.Abs(d));
                            moved = true;
                        }
                    }

                    for (int i = 1; i < column.Count; i++)
                    {
                        int above = column[i - 1], below = column[i];
                        float overlap = spacing - (y[above] - y[below]);
                        if (overlap <= 1e-4f) continue;
                        y[above] += overlap * 0.5f;
                        y[below] -= overlap * 0.5f;
                        moved = true;
                    }

                    if (!moved) break;
                }
            }

            return y;
        }

        void SetText(LabelSpec spec, string text, bool live)
        {
            if (live) labelSystem.SetText(spec, text);
            else spec.Text = text;
        }

        static string FirstLine(string notes)
        {
            if (string.IsNullOrEmpty(notes)) return "";
            int end = notes.IndexOf('\n');
            return end > 0 ? notes.Substring(0, end) : notes;
        }

        /// <summary>Bigger flows win label collisions; the industries and the uses (the loop's ends) come first.</summary>
        static float Priority(CircuitYear c, CircuitNode n)
        {
            float share = c.Gdp > 0 ? (float)(n.Value / c.Gdp) : 0;
            bool end = n.Column == CircuitColumn.Industries || n.Column == CircuitColumn.Uses;
            return (end ? 14 : 8) + 40 * share;
        }

        string NodeText(CircuitYear c, CircuitNode n)
        {
            string dim = "<color=#" + Hex(ValueText) + ">";
            string name = frame.Portrait ? ShortName(n) : n.Name;
            string value = MoneyCircuit.Money(n.Value);
            if (frame.Portrait) return name + "  " + dim + value + "</color>";
            switch (n.Kind)
            {
                case "tier":
                    return name + "  " + dim + value + "</color>";
                case "wages":
                case "owners":
                case "taxes":
                case "depreciation":
                case "government":
                    return name + "  " + dim + value + " · " + Percent(c.Gdp > 0 ? (float)(n.Value / c.Gdp) : 0) + " of GDP</color>";
                case "category":
                case "saving":
                    double spent = 0;
                    foreach (CircuitNode m in c.Nodes)
                    {
                        if (m.Kind == "category") spent += m.Value;
                    }

                    return n.Kind == "category"
                        ? name + "  " + dim + value + " · " + Percent(spent > 0 ? (float)(n.Value / spent) : 0) + "</color>"
                        : name + "  " + dim + value + "</color>";
                default:
                    return name + "  " + dim + value + "</color>";
            }
        }

        /// <summary>A node's name for a portrait frame, where columns stand closer: long names lose their second half.</summary>
        static string ShortName(CircuitNode n)
        {
            switch (n.Kind)
            {
                case "owners": return "Owners";
                case "taxes": return "Taxes";
                case "reinvest": return "Reinvested";
            }

            int amp = n.Name.IndexOf(" & ", StringComparison.Ordinal);
            return n.Name.Length > PortraitNameLength && amp > 0 ? n.Name.Substring(0, amp) : n.Name;
        }

        static string TitleText(CircuitYear c) => "The money circuit, " + c.Year.ToString(Ci);

        static string SubtitleText(CircuitYear c)
        {
            CircuitNode worst = c.Node(c.ImbalanceNode);
            string where = worst == null ? "" : worst.Kind == "tier" ? worst.Name + " industries" : worst.Name;
            return $"GDP {MoneyCircuit.Money(c.Gdp)} · every node balances within {(100 * c.Imbalance).ToString("0.0", Ci)}% of GDP" +
                   (worst != null ? $" (largest gap: {where})" : "");
        }

        static string Percent(float share) => (100 * share).ToString(Mathf.Abs(share) >= 0.095f ? "0" : "0.#", Ci) + "%";

        static string Hex(Color c) =>
            ((int)(Mathf.Clamp01(c.r) * 255)).ToString("X2") + ((int)(Mathf.Clamp01(c.g) * 255)).ToString("X2") +
            ((int)(Mathf.Clamp01(c.b) * 255)).ToString("X2");

        // ------------------------------------------------------------------ upload (main thread)

        public override void Upload(GraphContext ctx)
        {
            if (pendingFills == null) return;
            // the backdrop draws after every other station (whose queues run to QueueStations + 9) and before this
            // diagram, so what stands behind it is screened off
            int q = EconomyStyle.QueueStations + BackdropQueueOffset;
            backdropMat = Fills(q);
            skylineFillMat = Fills(q + 1);
            fillMat = Fills(q + 2);
            frameMat = GraphMaterials.Raw(GraphMaterials.Line(Color.white, 1f, q + 3));
            skylineLineMat = GraphMaterials.Raw(GraphMaterials.Line(Color.white, 1f, q + 4));
            lineMat = GraphMaterials.Raw(GraphMaterials.Line(Color.white, 1f, q + 5, true, 0, 1f));
            lineMat.SetFloat("_FlowFreq", FlowFreq);
            lineMat.SetFloat("_FlowSpeed", FlowSpeed);

            backdropFilter = AddStationMesh("CircuitBackdrop", backdropFills.ToMesh("CircuitBackdrop"), backdropMat);
            skylineFillFilter = AddStationMesh("CaptureSkyline", skylineFills.ToMesh("CaptureSkyline"), skylineFillMat);
            skylineLineFilter = AddStationMesh("CaptureSkylineLines", skylineLines.ToMesh("CaptureSkylineLines"), skylineLineMat);
            frameFilter = AddStationMesh("CircuitFrame", frameLines.ToMesh("CircuitFrame"), frameMat);
            fillFilter = AddStationMesh("Circuit", pendingFills.ToMesh("Circuit"), fillMat);
            lineFilter = AddStationMesh("CircuitFlows", pendingLines.ToMesh("CircuitFlows"), lineMat);
            pendingFills = skylineFills = backdropFills = null;
            pendingLines = frameLines = skylineLines = null;

            // EconomyLoaderLayer reset the state after Prepare read it: catch up with the year it holds now
            seenVersion = EconomyState.Version;
            uploaded = true;
            if (shown == null || EconomyState.Year != shown.Year) Rebuild(EconomyState.Year);
        }

        static Material Fills(int queue)
        {
            Material m = GraphMaterials.Raw(GraphMaterials.Surface(Color.white, 1f, queue));
            m.SetFloat("_EdgeSoft", 0f);
            return m;
        }

        MeshFilter AddStationMesh(string meshName, Mesh mesh, Material material)
        {
            MeshRenderer r = AddMesh(meshName, mesh, material);
            station.Place(r.transform);
            return r.GetComponent<MeshFilter>();
        }

        /// <summary>Shows a rebuilt mesh and destroys the one it replaces.</summary>
        static void Replace(MeshFilter filter, Mesh mesh)
        {
            if (filter == null)
            {
                Destroy(mesh);
                return;
            }

            Mesh old = filter.sharedMesh;
            filter.sharedMesh = mesh;
            if (old != null) Destroy(old);
        }

        void OnDestroy()
        {
            MeshFilter[] filters =
                { fillFilter, lineFilter, backdropFilter, frameFilter, skylineFillFilter, skylineLineFilter };
            foreach (MeshFilter f in filters)
            {
                if (f != null && f.sharedMesh != null) Destroy(f.sharedMesh);
            }

            // the label system may outlive this scene: give the timeline its labels back
            if (atStations && labelSystem != null) labelSystem.DataLabelsHidden = false;
        }

        // ------------------------------------------------------------------ live (main thread)

        public override void Tick(GraphContext ctx, CameraRig rig)
        {
            if (!uploaded) return;
            if (EconomyState.Version != seenVersion)
            {
                seenVersion = EconomyState.Version;
                if (shown == null || EconomyState.Year != shown.Year) Rebuild(EconomyState.Year);
            }

            Frame wanted = ScreenLayout.IsPortrait ? Tall : Landscape;
            if (wanted != frame)
            {
                frame = wanted;
                Rebuild(shown?.Year ?? EconomyState.Year);
            }

            if (rig == null) return;
            CameraPose pose = rig.Pose;

            // looking at the stations: the road's labels (its end stands in front of this diagram) step aside
            bool stations = Vector3.Dot(pose.Target - station.Origin, station.Forward) > -StationsAhead;
            if (stations != atStations)
            {
                atStations = stations;
                labelSystem.DataLabelsHidden = stations;
            }

            float d = Vector3.Distance(pose.Target, station.World(CenterLocal));
            float alpha = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(FadeNear, FadeFar, d));
            Vector3 local = Quaternion.Inverse(station.Rotation) * (pose.Target - station.Origin);
            float fromCapture = Vector3.Distance(local, CaptureLocal);
            float nearCapture = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(CaptureNear, CaptureFar, fromCapture));
            float amongTowers = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(TowersNear, TowersFar, local.z));
            float capture = Mathf.Max(nearCapture, amongTowers) * alpha;
            float sky = alpha * Mathf.Lerp(SkylineDimAlpha, 1f, capture);
            float ribbons = alpha * Mathf.Lerp(1f, CaptureRibbonAlpha, capture);
            float screen = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(BackdropNear, BackdropFar, d));
            float flows = alpha * Mathf.Lerp(1f, CaptureLineAlpha, capture);
            if (Mathf.Abs(flows - stationAlpha) > 1e-3f)
            {
                stationAlpha = flows;
                GraphMaterials.SetAlpha(lineMat, flows);
                GraphMaterials.SetAlpha(frameMat, alpha);
                SetVisible(fillFilter, alpha > 0.002f);
                SetVisible(lineFilter, alpha > 0.002f);
            }

            if (Mathf.Abs(ribbons - ribbonAlpha) > 1e-3f)
            {
                ribbonAlpha = ribbons;
                GraphMaterials.SetAlpha(fillMat, ribbons);
            }

            if (Mathf.Abs(screen - backdropAlpha) > 1e-3f)
            {
                backdropAlpha = screen;
                GraphMaterials.SetAlpha(backdropMat, screen);
                SetVisible(backdropFilter, screen > 0.002f);
            }

            if (Mathf.Abs(sky - skylineAlpha) > 1e-3f)
            {
                skylineAlpha = sky;
                GraphMaterials.SetAlpha(skylineFillMat, sky);
                GraphMaterials.SetAlpha(skylineLineMat, sky);
            }

            // the companies' names only where the skyline is the subject (the capture view and closer)
            bool towers = capture > 0.5f;
            if (towers != towersShown)
            {
                towersShown = towers;
                foreach (LabelSpec spec in towerLabels) spec.Hidden = !towers;
                labelSystem.MarkDirty();
            }
        }

        static void SetVisible(MeshFilter filter, bool visible)
        {
            if (filter == null) return;
            MeshRenderer r = filter.GetComponent<MeshRenderer>();
            if (r != null && r.enabled != visible) r.enabled = visible;
        }

        /// <summary>Builds another year: new meshes (the old ones destroyed), label texts and positions, anchors.</summary>
        void Rebuild(int year)
        {
            Stopwatch sw = Stopwatch.StartNew();
            CircuitYear c = circuit.Build(year);
            BuildYear(c, out SurfaceMeshBuilder fills, out LineMeshBuilder lines);
            Replace(fillFilter, fills.ToMesh("Circuit"));
            Replace(lineFilter, lines.ToMesh("CircuitFlows"));
            ApplyYear(c, true);
            if (skylineFrame != frame)
            {
                skylineFrame = frame;
                DrawSkyline(frame, companies, out SurfaceMeshBuilder towers, out LineMeshBuilder edges);
                Replace(skylineFillFilter, towers.ToMesh("CaptureSkyline"));
                Replace(skylineLineFilter, edges.ToMesh("CaptureSkylineLines"));
                PlaceSkyline();
            }

            shown = c;
            if (sw.ElapsedMilliseconds > 30)
            {
                Debug.LogWarning($"[Why] CircuitLayer: rebuilding {year} took {sw.ElapsedMilliseconds} ms");
            }
        }
    }
}
