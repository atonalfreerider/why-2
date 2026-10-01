using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using Why.Economy.Data;
using Why.Economy.Model;
using Why.Humans.Smv;
using Debug = UnityEngine.Debug;
using static Why.Economy.EconomyGeometry;

namespace Why.Economy.Layers
{
    /// <summary>
    /// The mind station: a 3D space of motives, as pages 2 and 3 of the user's notebook draw them. Across, desire
    /// (left: food, collective, law, shelter, sex) against fear (right: starvation, isolation, murder, exposure,
    /// childless), each pole a big arrow pushing inward with its five drives; up, the default OS (emotion: revenge,
    /// violence, emotional punishment) below the higher OS (reason: long-term strategy, networks, forgiveness); deep,
    /// the present (near) against strategy for the future (far). A faint two-lobe brain stands on the back wall, reason
    /// above emotion.
    ///
    /// Inside stand the people of <see cref="EconomyState.Year"/>: every living adult of the modeled population as a
    /// small cross at the share of their spending moved by fear, the share of their decisions made by reason and their
    /// orientation toward the future (one cross per lifeline, 100,000 people; blue, gold for the people in control of
    /// their path; each cross carries its lifeline's highlight id, so highlighting a person lights both). Among them
    /// the seven spending categories sit at their places in the mind (spending.json "mind") as circles whose area is
    /// the year's spending (in 2025 dollars, so years compare), tinted by their motive (rose desire .. ice fear); the
    /// neurochemicals mark the poles they serve (psyche.json, with their caveats in the tooltips). Two regions are
    /// marked where their people actually are, with their share of adults: buying fantasy and living in the now, and
    /// owning the path. A caption states what the model finds for the year: how few are in control, that they buy as
    /// much fantasy and as much out of fear as everyone else, and what sets them apart (reason, age, inheritance,
    /// their own business).
    ///
    /// Everything stands in the station's local frame (x across, y up, z away from the viewer) and is drawn with raw
    /// materials. When the year changes, the people, the categories, the regions and the texts are rebuilt (the old
    /// meshes destroyed). The station's meshes fade out while the camera looks elsewhere, and a dark screen behind it
    /// hides the games station that stands farther down the road while the camera looks at this one.
    /// </summary>
    [GraphScenes(GraphScene.Economy)]
    public sealed class MindLayer : GraphLayer
    {
        // ------------------------------------------------------------------ the motivation space (station-local)

        /// <summary>
        /// The space's walls: x from desire (left) to fear (right), y from emotion, the default OS (bottom), to reason,
        /// the higher OS (top; the notebook draws REASON above EMOTION), z from now (near the viewer) to strategy for
        /// the future (far).
        /// </summary>
        const float DesireX = -4f, FearX = 4f, EmotionY = 0.4f, ReasonY = 4.6f, NowZ = -1.5f, FutureZ = 3.5f;

        /// <summary>The middle of the space: fading and the "is the camera here" test measure from it.</summary>
        static readonly Vector3 CenterLocal = new Vector3(0.5f * (DesireX + FearX), 0.5f * (EmotionY + ReasonY),
            0.5f * (NowZ + FutureZ));

        /// <summary>
        /// The poles' arrows stand in the near plane outside the side walls, pointing in: shaft from
        /// <see cref="PoleTail"/> to <see cref="PoleNeck"/> (distance from the middle), head to <see cref="PoleTip"/>;
        /// half-heights of the shaft and the head around <see cref="PoleY"/>.
        /// </summary>
        const float PoleTail = 5.45f, PoleNeck = 4.55f, PoleTip = 4.08f, PoleY = 2.6f, PoleShaft = 1.05f, PoleHead = 1.42f;

        /// <summary>The drives' names inside a pole's arrow: the spacing between rows (local units).</summary>
        const float DriveRowStep = 0.42f;

        /// <summary>
        /// The time axis is the floor's left edge, from now to strategy, with a head at the far end of this length.
        /// </summary>
        const float TimeAxisX = DesireX, TimeAxisHead = 0.3f;

        /// <summary>
        /// The brain on the back wall: the reason lobe above, the emotion lobe below (centers, half-widths,
        /// half-heights, bulges and their depth).
        /// </summary>
        static readonly Vector2 ReasonLobe = new Vector2(0f, 3.35f), EmotionLobe = new Vector2(0f, 1.38f);

        const float ReasonLobeRx = 2.5f, ReasonLobeRy = 1.12f, EmotionLobeRx = 1.75f, EmotionLobeRy = 0.78f;
        const int ReasonLobeBulges = 11, EmotionLobeBulges = 8;
        const float LobeDepth = 0.07f;

        // ------------------------------------------------------------------ people

        /// <summary>Half the arm of a person's cross and how far people are jittered apart (local units).</summary>
        const float CrossArm = 0.036f, Jitter = 0.045f;

        const float CrossWidth = 1.1f;

        /// <summary>
        /// The crosses add up where they overlap (additive lines): the blue majority faint, the gold minority bright
        /// enough to read inside the blue cloud.
        /// </summary>
        const float PersonAlpha = 0.36f, PersonIntensity = 0.7f, ControlAlpha = 0.9f, ControlIntensity = 1.6f;

        /// <summary>Seeds of the jitter of each axis.</summary>
        const int JitterX = 101, JitterY = 211, JitterZ = 307;

        // ------------------------------------------------------------------ categories

        /// <summary>
        /// Circle radius of a category: <see cref="NodeRadius"/> for the largest category of the last data year (in
        /// 2025 dollars), by the square root of spending (area = money), never below <see cref="NodeMinRadius"/>.
        /// </summary>
        const float NodeRadius = 0.55f, NodeMinRadius = 0.06f;

        const int NodeSegments = 56;
        const float NodeFillAlpha = 0.16f, NodeLineAlpha = 0.9f, NodeIntensity = 1.5f;

        /// <summary>Where a category's label stands beside its circle.</summary>
        enum Side
        {
            Left,
            Right,
            Below,
            Above
        }

        /// <summary>
        /// Label sides of the categories, chosen so the labels stay off the people's cloud (fear shares 0.48 - 0.63: just
        /// right of the middle) and off each other; a category not listed is labeled on the side away from the middle.
        /// </summary>
        static readonly Dictionary<string, Side> CategorySides = new Dictionary<string, Side>(StringComparer.Ordinal)
        {
            { "necessities", Side.Left }, { "escapism", Side.Right }, { "jeopardy", Side.Below }, { "status", Side.Below },
            { "growth", Side.Left }, { "collective", Side.Left }, { "saving", Side.Right }
        };

        /// <summary>
        /// Label sides that differ in a portrait frame, whose labels are drawn half again as large over a space seen from
        /// farther away: social status's label stands right of its circle (below it, it would meet escapism's label or
        /// stand beside escapism's circle; on the left, the far end of the time axis and the desire arrow).
        /// </summary>
        static readonly Dictionary<string, Side> PortraitCategorySides = new Dictionary<string, Side>(StringComparer.Ordinal)
        {
            { "status", Side.Right }
        };

        /// <summary>Gap between a circle and its label (local units).</summary>
        const float LabelGap = 0.07f;

        /// <summary>
        /// A category whose circle stands in the people's cloud gets its label beside the cloud instead, this far outside
        /// the band where most people stand (<see cref="CloudQuantile"/> to 1 - it of their places across), with a thin
        /// leader from its circle.
        /// </summary>
        const float ColumnGap = 0.2f, CloudQuantile = 0.04f, LeaderAlpha = 0.5f;

        /// <summary>A thin stem from each category down to the floor, so its depth reads.</summary>
        const float StemAlpha = 0.28f;

        // ------------------------------------------------------------------ regions

        /// <summary>
        /// A region's ellipse spans this many standard deviations of its people's places (across and up, in the plane of
        /// their mean depth), never less than <see cref="RegionMinRadius"/>; its footprint on the floor spans the same
        /// across and in depth.
        /// </summary>
        const float RegionSpread = 1.0f, RegionMinRadius = 0.25f;

        const int RegionSegments = 72;
        const float RegionLineAlpha = 0.75f, RegionFillAlpha = 0.07f, RegionFloorAlpha = 0.35f;

        /// <summary>
        /// "Buying fantasy, living in the now": adults who spend more of their money on fantasy (escapes, status signals,
        /// lottery tickets) than the agency rule allows before it costs agency (psyche.json agency "fantasy", this when
        /// missing) and whose orientation is nearer the present than the future (below <see cref="NowFuture"/>). In 2025
        /// about two thirds of adults pass the first test and a fifth both (a cut at the agency's zero point, 0.3, marks
        /// nobody: the most fantasy-laden 1% of adults spend ~0.25 on it).
        /// </summary>
        const float DefaultFantasyCut = 0.15f, NowFuture = 0.5f;

        // ------------------------------------------------------------------ visibility

        /// <summary>
        /// Fixed labels hide while the camera's target is farther than this from them (world units): every label of
        /// the station is within ~6 of the mind view's target, while the games' views (one station farther, their
        /// cameras standing inside this space) keep more than 8 away.
        /// </summary>
        const float LabelRange = 7.5f;

        /// <summary>
        /// The station's meshes fade out while the camera's target is farther than <see cref="FadeNear"/> from the
        /// space's middle and are gone beyond <see cref="FadeFar"/>: the games' views look from inside this space (their
        /// targets ~11 away), the circuit's from before it (~13 away).
        /// </summary>
        const float FadeNear = 7f, FadeFar = 10f;

        /// <summary>
        /// A dark screen behind the space (in the background color) hides the games station farther down the road; it
        /// stands at <see cref="BackdropZ"/> and is there only while the camera looks at this station.
        /// </summary>
        const float BackdropZ = 6f, BackdropHalfWidth = 26f, BackdropBottom = -14f, BackdropTop = 22f;

        const float BackdropNear = 6f, BackdropFar = 9f;

        /// <summary>
        /// Render queues from QueueStations + this: after the games station (QueueStations .. + 5), so the backdrop
        /// screens it, and before the circuit (+ 10 ..), whose own backdrop screens this station from the circuit's views.
        /// </summary>
        const int QueueOffset = 6;

        /// <summary>A rebuild for another year should take less than this (milliseconds).</summary>
        const int RebuildBudgetMs = 40;

        // ------------------------------------------------------------------ labels

        /// <summary>Caption lines: at most seven (a portrait frame's short ones; a landscape frame uses four).</summary>
        const int CaptionLines = 7;

        /// <summary>
        /// Pixel offsets (1080p) above the back wall's top edge: the higher OS label, the caption's lines, the title.
        /// </summary>
        const float OsOffset = 15f, CaptionOffset = 40f, CaptionStep = 23f, TitleGap = 30f;

        const float TitlePriority = 50, CaptionPriority = 46, HeadingPriority = 44, OsPriority = 42, AxisPriority = 41;
        const float RegionPriority = 40, CategoryPriority = 30, DrivePriority = 28, ChemicalPriority = 20;

        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        // ------------------------------------------------------------------ the notebook's words (data fills in)

        static readonly string[] DefaultDesires = { "food", "collective", "law", "shelter", "sex" };
        static readonly string[] DefaultFears = { "starvation", "isolation", "murder", "exposure", "childless" };

        const string HigherText = "HIGHER OS: reason", HigherDecisions = "long-term strategy, networks, forgiveness";
        const string DefaultText = "DEFAULT OS: emotion", DefaultDecisions = "revenge, violence, emotional punishment";

        /// <summary>
        /// Where each neurochemical stands (station-local), near the pole and the spending it serves: dopamine (wanting)
        /// on the desire side near the present, cortisol and adrenaline on the fear side near the present, oxytocin by
        /// the collective, serotonin and testosterone by social status, endorphins by escapism. Unknown chemicals stand
        /// by their pole.
        /// </summary>
        static readonly Dictionary<string, Vector3> ChemicalPlaces = new Dictionary<string, Vector3>(StringComparer.Ordinal)
        {
            { "dopamine", new Vector3(-3.42f, 3.55f, -1.1f) },
            { "endorphins", new Vector3(-3.55f, 0.5f, -1.1f) },
            { "serotonin", new Vector3(-2.85f, 2.75f, 0.25f) },
            { "testosterone", new Vector3(-2.9f, 1.9f, 0.25f) },
            { "oxytocin", new Vector3(-0.85f, 3.3f, 1.5f) },
            { "cortisol", new Vector3(3.35f, 3.3f, -1.1f) },
            { "adrenaline", new Vector3(3.5f, 1.15f, -1.1f) },
        };

        /// <summary>
        /// A chemical's name stands on the inward side of its mark (right of the marks on the desire side, left on the
        /// fear side, clear of the poles' arrows), except these, whose name stands left of the mark: beside the people's
        /// cloud, which it would cover.
        /// </summary>
        static readonly HashSet<string> ChemicalLabelsLeft = new HashSet<string>(StringComparer.Ordinal) { "oxytocin" };

        const float ChemicalRadius = 0.075f;

        /// <summary>
        /// Gap between a chemical's mark and its name (pixels at 1080p, before the label zoom): clear of the mark's rays;
        /// in a portrait frame the mark is smaller and the gap is zoomed, so less does.
        /// </summary>
        const float ChemicalLabelGap = 13f, PortraitChemicalLabelGap = 9f;

        // ------------------------------------------------------------------ highlight ids

        /// <summary>Drives: the desire pole's arrow, its five drives, the fear pole's arrow, its five drives.</summary>
        const int IdDesirePole = EconomyIds.MindDrive, IdFearPole = EconomyIds.MindDrive + 6;

        const int IdBrain = EconomyIds.MindAxis + 1, IdControl = EconomyIds.MindAxis + 2, IdFantasy = EconomyIds.MindAxis + 3;
        const int IdTimeAxis = EconomyIds.MindAxis + 4;

        // ------------------------------------------------------------------ colors

        static readonly Color Gold = EconomyStyle.Capital;
        static readonly Color Blue = EconomyStyle.People;
        static readonly Color Rose = EconomyStyle.Desire;
        static readonly Color Ice = EconomyStyle.Fear;
        static readonly Color FrameColor = new Color(0.55f, 0.57f, 0.62f);
        static readonly Color BrainColor = new Color(0.62f, 0.66f, 0.78f);

        /// <summary>The numbers in labels: dimmer than the names.</summary>
        static readonly Color ValueText = new Color(0.74f, 0.77f, 0.82f);

        public override int Order => 43;

        /// <summary>What the space shows for one year: the categories' money and the people's numbers.</summary>
        sealed class MindYear
        {
            public int Year;

            /// <summary>
            /// Adults drawn; of them in control, in the fantasy region, and buying fantasy at all (any orientation).
            /// </summary>
            public int Adults, Control, Fantasy, FantasyBuyers;

            /// <summary>
            /// Means over the people in control [1] and the rest [0]: fantasy and fear shares of spending, reason, age,
            /// share who inherited from a parent by then, share self-employed.
            /// </summary>
            public readonly double[] FantasyMean = new double[2], FearMean = new double[2], ReasonMean = new double[2],
                AgeMean = new double[2], Inherited = new double[2], SelfEmployed = new double[2];

            /// <summary>Share of household money moved by fear (spending-weighted).</summary>
            public double MoneyFear;

            /// <summary>Means over every adult: fear share of spending, reason.</summary>
            public double FearAll, ReasonAll;

            /// <summary>Fantasy share of spending by household income quintile (spending-weighted).</summary>
            public readonly double[] FantasyByQuintile = new double[5];

            /// <summary>Spending per category this year ($B nominal) and in 2025 dollars.</summary>
            public readonly double[] Spent = new double[7], SpentReal = new double[7];

            /// <summary>The regions: their people's mean place and spread (station-local).</summary>
            public Vector3 ControlCenter, ControlSpread, FantasyCenter, FantasySpread;

            /// <summary>The band across where most people stand (station-local x; see <see cref="CloudQuantile"/>).</summary>
            public float CloudLeft = 0.5f * (DesireX + FearX), CloudRight = 0.5f * (DesireX + FearX);
        }

        Station station;
        LabelSystem labelSystem;
        EconomyData data;
        MoneyCircuit circuit;
        EconomicLives lives;
        SmvPopulation population;
        MindYear shown;
        int seenVersion = -1;
        bool uploaded, portrait;

        /// <summary>Radius per square root of a category's spending (2025 dollars, $B).</summary>
        float radiusPerRoot;

        /// <summary>Fantasy share of spending above which a person buys fantasy (see <see cref="DefaultFantasyCut"/>).</summary>
        float fantasyCut = DefaultFantasyCut;

        /// <summary>People one cross stands for (the population's people per lifeline).</summary>
        double peoplePerLine;

        /// <summary>
        /// The year each person first received an estate from a parent (<see cref="NoYear"/>: never in the record), so a
        /// year counts only the bequests made by then. The first parent to die usually leaves everything to the
        /// surviving spouse, so a parent's death alone does not make an heir.
        /// </summary>
        int[] inheritYear = Array.Empty<int>();

        const int NoYear = int.MaxValue;

        /// <summary>Category places in the space (station-local), in EconomyData.CategoryIds order.</summary>
        readonly Vector3[] categoryPlace = new Vector3[7];

        readonly Category[] categories = new Category[7];

        /// <summary>Label sides of the categories in a landscape and a portrait frame (see <see cref="SideOf"/>).</summary>
        readonly Side[] categorySide = new Side[7], portraitSide = new Side[7];

        /// <summary>The people of the year being built: place, highlight id, in control (reused between years).</summary>
        readonly List<Vector3> peoplePlace = new List<Vector3>(4096);
        readonly List<int> peopleId = new List<int>(4096);
        readonly List<bool> peopleGold = new List<bool>(4096);
        readonly List<float> peopleAcross = new List<float>(4096);

        // meshes: static frame, the year's people and nodes, the backdrop
        SurfaceMeshBuilder frameFills, backdropFills, pendingFills;
        LineMeshBuilder frameLines, pendingLines;
        MeshFilter backdropFilter, frameFillFilter, frameLineFilter, yearFillFilter, yearLineFilter;
        Material backdropMat, frameFillMat, frameLineMat, yearFillMat, yearLineMat;
        float stationAlpha = -1, backdropAlpha = -1;

        // labels and anchors
        readonly List<LabelSpec> labels = new List<LabelSpec>(48);
        readonly LabelSpec[] categoryLabels = new LabelSpec[7];
        readonly Anchor[] categoryAnchors = new Anchor[7];
        readonly LabelSpec[] captionLabels = new LabelSpec[CaptionLines];
        LabelSpec titleLabel, controlLabel, fantasyLabel, strategyLabel;
        readonly List<LabelSpec> driveLabels = new List<LabelSpec>(10);

        /// <summary>The chemicals' names and the side of their marks they stand on (-1 left, +1 right).</summary>
        readonly List<(LabelSpec label, int side)> chemicalLabels = new List<(LabelSpec, int)>(8);
        Anchor controlAnchor, fantasyAnchor, peopleAnchor, spaceAnchor;

        // ------------------------------------------------------------------ prepare (worker thread)

        public override void Prepare(GraphContext ctx)
        {
            Stopwatch sw = Stopwatch.StartNew();
            station = EconomyStage.Get(EconomyStage.Mind);
            labelSystem = ctx.Labels;
            EconomyModel model = ctx.Shared<EconomyModel>(EconomyModel.SharedKey);
            if (model?.Data == null || model.Data.Categories.Count < 7)
            {
                Debug.LogWarning("[Why] MindLayer: no economy model, mind station skipped");
                return;
            }

            data = model.Data;
            circuit = model.Circuit;
            lives = model.Lives;
            population = ctx.Shared<SmvPopulation>(SmvPopulation.SharedKey);
            portrait = ScreenLayout.IsPortrait;
            FindInheritances();
            for (int c = 0; c < 7; c++)
            {
                categories[c] = data.CategoryById(EconomyData.CategoryIds[c]) ?? data.Categories[c];
                categoryPlace[c] = MindPlace(categories[c].Mind);
                categorySide[c] = categories[c].Id != null && CategorySides.TryGetValue(categories[c].Id, out Side side) ? side
                    : categoryPlace[c].x < 0 ? Side.Left : Side.Right;
                portraitSide[c] = categories[c].Id != null && PortraitCategorySides.TryGetValue(categories[c].Id, out side)
                    ? side
                    : categorySide[c];
            }

            radiusPerRoot = RadiusScale();
            double cut = data.Psyche?.Agency?.Threshold("fantasy", DefaultFantasyCut) ?? DefaultFantasyCut;
            fantasyCut = Mathf.Clamp01((float)(double.IsNaN(cut) ? DefaultFantasyCut : cut));

            frameFills = new SurfaceMeshBuilder();
            frameLines = new LineMeshBuilder(2048);
            DrawBox(frameLines);
            DrawTimeAxis(frameLines);
            DrawPole(frameFills, frameLines, -1, Rose, IdDesirePole);
            DrawPole(frameFills, frameLines, 1, Ice, IdFearPole);
            DrawBrain(frameLines);
            DrawChemicals(frameLines);
            backdropFills = new SurfaceMeshBuilder();
            DrawBackdrop(backdropFills);

            MindYear year = Measure(EconomyState.Year);
            BuildYear(year, out pendingFills, out pendingLines);

            AddStaticLabels();
            AddYearLabels();
            ApplyFrame(false);
            ApplyYear(year, false);
            // handed over placed and ranked: the label system sorts labels by priority when it takes them
            foreach (LabelSpec spec in labels) ctx.Labels.Add(spec);
            shown = year;
            Debug.Log($"[Why] MindLayer.Prepare {sw.ElapsedMilliseconds} ms: {year.Year}, {year.Adults} adults, " +
                      $"{year.Control} in control, {year.Fantasy} buying fantasy in the now; {Distribution(year.Year)}");
        }

        /// <summary>
        /// A place in the mind (spending.json: x -1 desire .. +1 fear, y 0 emotion .. 1 reason, z 0 now .. 1 future).
        /// </summary>
        static Vector3 MindPlace(MindPoint m) =>
            m == null ? CenterLocal : new Vector3(X(0.5f * (m.X + 1)), Y(m.Y), Z(m.Z));

        /// <summary>Across: a share of fear (0 = all desire, at the left wall; 1 = all fear, at the right).</summary>
        static float X(float fearShare) => Mathf.Lerp(DesireX, FearX, fearShare);

        /// <summary>Up: a share of decisions made by reason (0 = all emotion, the floor; 1 = all reason, the ceiling).</summary>
        static float Y(float reason) => Mathf.Lerp(EmotionY, ReasonY, reason);

        /// <summary>Deep: orientation toward the future (0 = the present only, near; 1 = strategy, far).</summary>
        static float Z(float future) => Mathf.Lerp(NowZ, FutureZ, future);

        /// <summary>
        /// Radius per square root of spending: the largest category of the last data year (2025 dollars) gets
        /// <see cref="NodeRadius"/>, so a circle's area is its money and years compare.
        /// </summary>
        float RadiusScale()
        {
            int year = data.LastYear;
            double largest = 0;
            CircuitYear c = circuit?.Build(year);
            for (int k = 0; k < 7; k++) largest = Math.Max(largest, data.Real(Spending(c, k, year), year));
            return largest > 0 ? NodeRadius / Mathf.Sqrt((float)largest) : 0;
        }

        /// <summary>
        /// A category's spending in a year ($B nominal): the money circuit's (so both stations show the same money), else
        /// the simulated population's.
        /// </summary>
        double Spending(CircuitYear c, int category, int year)
        {
            CircuitNode node = c?.Node(EconomyData.CategoryIds[category]);
            if (node != null) return node.Value;
            PopulationYear a = lives != null && lives.Ready ? lives.Aggregate(year) : null;
            return a != null ? a.Categories[category] : 0;
        }

        // ------------------------------------------------------------------ the static frame

        /// <summary>
        /// The space's edges, faint, the floor's a little brighter; the floor's left edge is the time axis
        /// (<see cref="DrawTimeAxis"/>).
        /// </summary>
        static void DrawBox(LineMeshBuilder lines)
        {
            Color32 edge = Tint(FrameColor, 0.32f), floor = Tint(FrameColor, 0.5f);
            Vector3 a = new Vector3(DesireX, EmotionY, NowZ), b = new Vector3(FearX, EmotionY, NowZ);
            Vector3 c = new Vector3(FearX, EmotionY, FutureZ), d = new Vector3(DesireX, EmotionY, FutureZ);
            Vector3 up = new Vector3(0, ReasonY - EmotionY, 0);
            lines.AddSegment(a, b, floor, 1.1f, 0, GraphIds.None);
            lines.AddSegment(b, c, floor, 1.1f, 0, GraphIds.None);
            lines.AddSegment(c, d, floor, 1.1f, 0, GraphIds.None);
            lines.AddSegment(a + up, b + up, edge, 1f, 0, GraphIds.None);
            lines.AddSegment(b + up, c + up, edge, 1f, 0, GraphIds.None);
            lines.AddSegment(c + up, d + up, edge, 1f, 0, GraphIds.None);
            lines.AddSegment(d + up, a + up, edge, 1f, 0, GraphIds.None);
            lines.AddSegment(a, a + up, edge, 1f, 0, GraphIds.None);
            lines.AddSegment(b, b + up, edge, 1f, 0, GraphIds.None);
            lines.AddSegment(c, c + up, edge, 1f, 0, GraphIds.None);
            lines.AddSegment(d, d + up, edge, 1f, 0, GraphIds.None);

            // the middle of the floor across and in depth: half desire half fear, halfway to the future
            Color32 faint = Tint(FrameColor, 0.16f);
            float midX = 0.5f * (DesireX + FearX), midZ = 0.5f * (NowZ + FutureZ);
            lines.AddSegment(new Vector3(midX, EmotionY, NowZ), new Vector3(midX, EmotionY, FutureZ), faint, 0.8f, 0,
                GraphIds.None);
            lines.AddSegment(new Vector3(DesireX, EmotionY, midZ), new Vector3(FearX, EmotionY, midZ), faint, 0.8f, 0,
                GraphIds.None);
        }

        /// <summary>
        /// The time axis along the floor's left edge, from now (near) to strategy (far), its head in the floor.
        /// </summary>
        static void DrawTimeAxis(LineMeshBuilder lines)
        {
            Color32 color = Tint(GraphStyle.Text, 0.75f);
            Vector3 from = new Vector3(TimeAxisX, EmotionY, NowZ), to = new Vector3(TimeAxisX, EmotionY, FutureZ);
            lines.AddSegment(from, to, color, 1.6f, 0, IdTimeAxis, 1.2f);
            float side = TimeAxisHead * Mathf.Tan(28f * Mathf.Deg2Rad);
            lines.AddSegment(to, to + new Vector3(-side, 0, -TimeAxisHead), color, 1.6f, 0, IdTimeAxis, 1.2f);
            lines.AddSegment(to, to + new Vector3(side, 0, -TimeAxisHead), color, 1.6f, 0, IdTimeAxis, 1.2f);
        }

        /// <summary>
        /// A pole's big arrow in the near plane, outside its wall and pointing in (<paramref name="side"/> -1 = desire on
        /// the left, +1 = fear on the right): a faint fill and a bright outline.
        /// </summary>
        static void DrawPole(SurfaceMeshBuilder fills, LineMeshBuilder lines, int side, Color hue, int id)
        {
            List<Vector3> pts = PoleOutline(side);
            Color32 fill = Tint(hue, 0.1f);
            // the shaft (a quad) and the head (a triangle)
            fills.AddTriangle(pts[0], pts[1], pts[5], fill, id);
            fills.AddTriangle(pts[0], pts[5], pts[6], fill, id);
            fills.AddTriangle(pts[2], pts[3], pts[4], fill, id);
            Polygon(lines, pts, true, Tint(hue, 0.85f), 2.2f, id, 1.4f);
        }

        /// <summary>
        /// Corners of a pole's arrow: tail bottom, neck bottom, head bottom, tip, head top, neck top, tail top.
        /// </summary>
        static List<Vector3> PoleOutline(int side)
        {
            float s = side;
            return new List<Vector3>
            {
                new Vector3(s * PoleTail, PoleY - PoleShaft, NowZ),
                new Vector3(s * PoleNeck, PoleY - PoleShaft, NowZ),
                new Vector3(s * PoleNeck, PoleY - PoleHead, NowZ),
                new Vector3(s * PoleTip, PoleY, NowZ),
                new Vector3(s * PoleNeck, PoleY + PoleHead, NowZ),
                new Vector3(s * PoleNeck, PoleY + PoleShaft, NowZ),
                new Vector3(s * PoleTail, PoleY + PoleShaft, NowZ),
            };
        }

        /// <summary>The faint brain on the back wall: the reason lobe above the emotion lobe.</summary>
        static void DrawBrain(LineMeshBuilder lines)
        {
            Color32 color = Tint(BrainColor, 0.3f);
            Lobe(lines, new Vector3(ReasonLobe.x, ReasonLobe.y, FutureZ), ReasonLobeRx, ReasonLobeRy, ReasonLobeBulges, color);
            Lobe(lines, new Vector3(EmotionLobe.x, EmotionLobe.y, FutureZ), EmotionLobeRx, EmotionLobeRy, EmotionLobeBulges,
                color);
            // the stem between them
            float neck = ReasonLobe.y - ReasonLobeRy + 0.04f, crown = EmotionLobe.y + EmotionLobeRy;
            lines.AddSegment(new Vector3(-0.18f, neck, FutureZ), new Vector3(-0.12f, crown, FutureZ), color, 1f, 0, IdBrain);
            lines.AddSegment(new Vector3(0.18f, neck, FutureZ), new Vector3(0.12f, crown, FutureZ), color, 1f, 0, IdBrain);
        }

        /// <summary>An elliptical cloud: rounded bulges around an ellipse (the notebook's brains, stretched).</summary>
        static void Lobe(LineMeshBuilder lines, Vector3 c, float rx, float ry, int bulges, Color32 color)
        {
            const int perBulge = 8;
            int n = bulges * perBulge;
            List<Vector3> pts = new List<Vector3>(n + 1);
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n;
                float a = t * 2f * Mathf.PI + Mathf.PI * 0.5f;
                float r = 1 - LobeDepth + LobeDepth * Mathf.Abs(Mathf.Sin((t * bulges + 0.5f) * Mathf.PI));
                pts.Add(new Vector3(c.x + rx * r * Mathf.Cos(a), c.y + ry * r * Mathf.Sin(a), c.z));
            }

            lines.AddPolyline(pts, color, 1.1f, 0, IdBrain);
        }

        /// <summary>Each neurochemical as a small spark at its place, in the hue of the pole it serves.</summary>
        void DrawChemicals(LineMeshBuilder lines)
        {
            List<Chemical> list = data.Psyche?.Chemicals;
            if (list == null) return;
            for (int k = 0; k < list.Count; k++)
            {
                Chemical ch = list[k];
                if (ch == null || string.IsNullOrEmpty(ch.Id)) continue; // unlabeled, as in AddStaticLabels
                Spark(lines, ChemicalPlace(ch, k), ChemicalRadius, Tint(PoleColor(ch.Pole), 0.9f), 1.3f,
                    EconomyIds.MindChemical + k, 1.6f);
            }
        }

        static Vector3 ChemicalPlace(Chemical ch, int index)
        {
            if (ch?.Id != null && ChemicalPlaces.TryGetValue(ch.Id, out Vector3 p)) return p;
            float side = ch?.Pole == "fear" ? 1 : -1;
            return new Vector3(side * 3.3f, 1.2f + 0.45f * (index % 6), NowZ + 0.4f);
        }

        static Color PoleColor(string pole) => pole == "fear" ? Ice : Rose;

        /// <summary>The dark screen behind the space (see <see cref="BackdropZ"/>).</summary>
        static void DrawBackdrop(SurfaceMeshBuilder fills)
        {
            Color32 dark = Tint(GraphStyle.Background, 1f);
            const float w = BackdropHalfWidth;
            Vector3 a = new Vector3(-w, BackdropBottom, BackdropZ), b = new Vector3(w, BackdropBottom, BackdropZ);
            Vector3 d = new Vector3(-w, BackdropTop, BackdropZ), e = new Vector3(w, BackdropTop, BackdropZ);
            fills.AddTriangle(a, b, e, dark, GraphIds.None);
            fills.AddTriangle(a, e, d, dark, GraphIds.None);
        }

        // ------------------------------------------------------------------ the year

        /// <summary>
        /// The year's numbers: the categories' spending, and one pass over the population that places every living adult
        /// (into <see cref="peoplePlace"/>), compares the people in control with the rest and finds the regions.
        /// </summary>
        MindYear Measure(int year)
        {
            MindYear m = new MindYear { Year = year };
            CircuitYear c = circuit?.Build(year);
            for (int k = 0; k < 7; k++)
            {
                m.Spent[k] = Spending(c, k, year);
                m.SpentReal[k] = data.Real(m.Spent[k], year);
            }

            peoplePlace.Clear();
            peopleId.Clear();
            peopleGold.Clear();
            SmvSimulation sim = lives != null && lives.Ready ? lives.Sim : null;
            if (sim == null) return m;

            List<SmvPerson> people = sim.People;
            int[] count = new int[2];
            double[] spendQ = new double[5], fantasyQ = new double[5];
            double money = 0, fearMoney = 0;
            Vector3 controlSum = Vector3.zero, controlSq = Vector3.zero, fantasySum = Vector3.zero, fantasySq = Vector3.zero;
            for (int i = 0; i < people.Count; i++)
            {
                if (!lives.TryGet(i, year, out PersonYear r) || !r.Adult) continue;
                SmvPerson p = people[i];
                int g = r.InControl ? 1 : 0;
                count[g]++;
                m.FantasyMean[g] += r.Fantasy;
                m.FearMean[g] += r.FearShare;
                m.ReasonMean[g] += r.Reason;
                m.AgeMean[g] += r.Age;
                if (i < inheritYear.Length && inheritYear[i] <= year) m.Inherited[g]++;
                if (r.SelfEmployed) m.SelfEmployed[g]++;

                float spend = Mathf.Max(0, r.Spending);
                money += spend;
                fearMoney += spend * r.FearShare;
                int q = Mathf.Clamp((int)(r.IncomeRank * 5), 0, 4);
                spendQ[q] += spend;
                fantasyQ[q] += spend * r.Fantasy;

                Vector3 place = new Vector3(X(r.FearShare), Y(r.Reason), Z(r.Future));
                if (r.InControl)
                {
                    m.Control++;
                    controlSum += place;
                    controlSq += Vector3.Scale(place, place);
                }

                if (r.Fantasy > fantasyCut) m.FantasyBuyers++;
                if (r.Fantasy > fantasyCut && r.Future < NowFuture)
                {
                    m.Fantasy++;
                    fantasySum += place;
                    fantasySq += Vector3.Scale(place, place);
                }

                Vector3 jitter = new Vector3(Hash(i, JitterX), Hash(i, JitterY), Hash(i, JitterZ)) * Jitter;
                peoplePlace.Add(Inside(place + jitter));
                peopleId.Add(population != null ? population.Id(p) : GraphIds.None);
                peopleGold.Add(r.InControl);
            }

            m.Adults = count[0] + count[1];
            if (m.Adults > 0)
            {
                m.FearAll = (m.FearMean[0] + m.FearMean[1]) / m.Adults;
                m.ReasonAll = (m.ReasonMean[0] + m.ReasonMean[1]) / m.Adults;
            }

            for (int g = 0; g < 2; g++)
            {
                double n = Math.Max(1, count[g]);
                m.FantasyMean[g] /= n;
                m.FearMean[g] /= n;
                m.ReasonMean[g] /= n;
                m.AgeMean[g] /= n;
                m.Inherited[g] /= n;
                m.SelfEmployed[g] /= n;
            }

            peopleAcross.Clear();
            foreach (Vector3 place in peoplePlace) peopleAcross.Add(place.x);
            peopleAcross.Sort();
            if (peopleAcross.Count > 0)
            {
                int last = peopleAcross.Count - 1;
                m.CloudLeft = peopleAcross[Mathf.Clamp((int)(CloudQuantile * last), 0, last)];
                m.CloudRight = peopleAcross[Mathf.Clamp((int)((1 - CloudQuantile) * last), 0, last)];
            }

            m.MoneyFear = money > 0 ? fearMoney / money : 0;
            for (int q = 0; q < 5; q++) m.FantasyByQuintile[q] = spendQ[q] > 0 ? fantasyQ[q] / spendQ[q] : 0;
            Spread(controlSum, controlSq, m.Control, out m.ControlCenter, out m.ControlSpread);
            Spread(fantasySum, fantasySq, m.Fantasy, out m.FantasyCenter, out m.FantasySpread);
            return m;
        }

        /// <summary>
        /// Fills <see cref="inheritYear"/> (and <see cref="peoplePerLine"/>) once: for everyone who received an estate
        /// from a parent, the first year one did.
        /// </summary>
        void FindInheritances()
        {
            if (lives == null || !lives.Ready || lives.Sim == null) return;
            peoplePerLine = lives.Sim.PeoplePerLine;
            List<SmvPerson> people = lives.Sim.People;
            inheritYear = new int[people.Count];
            for (int i = 0; i < people.Count; i++)
            {
                inheritYear[i] = NoYear;
                if (lives.InheritedFromParents(i) <= 0) continue;
                SmvPerson p = people[i];
                inheritYear[i] = Math.Min(BequestYear(people, i, p.Mother), BequestYear(people, i, p.Father));
            }
        }

        /// <summary>
        /// The year a parent's death left the person an estate, or <see cref="NoYear"/>: the model passes an estate on
        /// in the first year the parent does not live to see the middle of, and the heir's record of that year shows
        /// what it received (nothing when a surviving spouse took it all).
        /// </summary>
        int BequestYear(List<SmvPerson> people, int heir, int parent)
        {
            if (parent < 0 || parent >= people.Count) return NoYear;
            double death = people[parent].Death;
            if (double.IsNaN(death) || double.IsInfinity(death)) return NoYear; // still alive
            int guess = (int)Math.Ceiling(death - 0.5);
            for (int y = guess - 1; y <= guess + 1; y++)
            {
                if (lives.TryGet(parent, y, out _) || !lives.TryGet(parent, y - 1, out _)) continue;
                return lives.TryGet(heir, y, out PersonYear r) && r.Inherited > 0 ? y : NoYear;
            }

            return NoYear;
        }

        /// <summary>Mean and standard deviation of places from their sum and sum of squares.</summary>
        static void Spread(Vector3 sum, Vector3 squares, int n, out Vector3 mean, out Vector3 sd)
        {
            if (n <= 0)
            {
                mean = CenterLocal;
                sd = Vector3.zero;
                return;
            }

            mean = sum / n;
            Vector3 v = squares / n - Vector3.Scale(mean, mean);
            sd = new Vector3(Mathf.Sqrt(Mathf.Max(0, v.x)), Mathf.Sqrt(Mathf.Max(0, v.y)), Mathf.Sqrt(Mathf.Max(0, v.z)));
        }

        /// <summary>A point kept inside the space's walls.</summary>
        static Vector3 Inside(Vector3 p) => new Vector3(Mathf.Clamp(p.x, DesireX, FearX), Mathf.Clamp(p.y, EmotionY, ReasonY),
            Mathf.Clamp(p.z, NowZ, FutureZ));

        /// <summary>
        /// A deterministic value in [-1, 1) for a person and an axis (so people keep their jitter across years).
        /// </summary>
        static float Hash(int i, int salt)
        {
            unchecked
            {
                uint h = (uint)i * 0x9E3779B1u ^ (uint)salt * 0x85EBCA77u;
                h ^= h >> 15;
                h *= 0x2C1B3C6Du;
                h ^= h >> 12;
                h *= 0x297A2D39u;
                h ^= h >> 15;
                return (h & 0xFFFFFF) / (float)0x1000000 * 2f - 1f;
            }
        }

        /// <summary>The year's geometry: the regions, the categories' circles and stems, and the people's crosses.</summary>
        void BuildYear(MindYear m, out SurfaceMeshBuilder fills, out LineMeshBuilder lines)
        {
            fills = new SurfaceMeshBuilder();
            lines = new LineMeshBuilder(peoplePlace.Count * 4 + 7 * (NodeSegments + 4) + 4 * (RegionSegments + 1));

            if (m.Fantasy > 0) DrawRegion(fills, lines, m.FantasyCenter, m.FantasySpread, Rose, IdFantasy);
            if (m.Control > 0) DrawRegion(fills, lines, m.ControlCenter, m.ControlSpread, Gold, IdControl);

            for (int k = 0; k < 7; k++)
            {
                Vector3 c = categoryPlace[k];
                float r = NodeRadiusOf(m, k);
                Color hue = EconomyStyle.Motive((float)categories[k].FearShare);
                int id = EconomyIds.MindCategory + k;
                Disc(fills, c, r, NodeSegments, Tint(hue, NodeFillAlpha), id, 1.2f);
                Circle(lines, c, r, NodeSegments, Tint(hue, NodeLineAlpha), 1.6f, id, NodeIntensity);
                lines.AddSegment(new Vector3(c.x, c.y - r, c.z), new Vector3(c.x, EmotionY, c.z), Tint(hue, StemAlpha), 0.8f, 0,
                    id);
                lines.AddSegment(new Vector3(c.x - 0.08f, EmotionY, c.z), new Vector3(c.x + 0.08f, EmotionY, c.z),
                    Tint(hue, StemAlpha * 1.5f), 1f, 0, id);
                Vector3 label = CategoryLabelPlace(m, k, out Vector3 from);
                if (from != label)
                {
                    // stop short of the text
                    Vector3 to = Vector3.MoveTowards(label, from, LabelGap);
                    lines.AddSegment(from, to, Tint(hue, LeaderAlpha), 0.9f, 0, id);
                }
            }

            // the blue majority first, then the gold few on top (additive, but the order keeps the mesh readable)
            Color32 blue = Tint(Blue, PersonAlpha), gold = Tint(Gold, ControlAlpha);
            for (int pass = 0; pass < 2; pass++)
            {
                bool wantGold = pass == 1;
                for (int i = 0; i < peoplePlace.Count; i++)
                {
                    if (peopleGold[i] != wantGold) continue;
                    Vector3 p = peoplePlace[i];
                    Color32 color = wantGold ? gold : blue;
                    float intensity = wantGold ? ControlIntensity : PersonIntensity;
                    int id = peopleId[i];
                    Vector3 across = new Vector3(CrossArm, 0, 0), up = new Vector3(0, CrossArm, 0);
                    lines.AddSegment(p - across, p + across, color, CrossWidth, 0, id, intensity);
                    lines.AddSegment(p - up, p + up, color, CrossWidth, 0, id, intensity);
                }
            }
        }

        /// <summary>A category's circle radius in a year (area = spending in 2025 dollars).</summary>
        float NodeRadiusOf(MindYear m, int k) =>
            Mathf.Max(NodeMinRadius, radiusPerRoot * Mathf.Sqrt((float)Math.Max(0, m.SpentReal[k])));

        /// <summary>
        /// A region where its people are: an ellipse in the plane of their mean depth spanning their spread across and up,
        /// faintly filled, and its footprint on the floor spanning their spread across and in depth.
        /// </summary>
        static void DrawRegion(SurfaceMeshBuilder fills, LineMeshBuilder lines, Vector3 center, Vector3 spread, Color hue, int id)
        {
            RegionRadii(center, spread, out float rx, out float ry, out float rz);
            List<Vector3> ring = new List<Vector3>(RegionSegments + 1), floor = new List<Vector3>(RegionSegments + 1);
            for (int i = 0; i <= RegionSegments; i++)
            {
                float a = i * 2f * Mathf.PI / RegionSegments;
                float cos = Mathf.Cos(a), sin = Mathf.Sin(a);
                ring.Add(new Vector3(center.x + rx * cos, center.y + ry * sin, center.z));
                floor.Add(new Vector3(center.x + rx * cos, EmotionY, center.z + rz * sin));
            }

            Color32 fill = Tint(hue, RegionFillAlpha);
            for (int i = 0; i < RegionSegments; i++) fills.AddTriangle(center, ring[i], ring[i + 1], fill, id);
            lines.AddPolyline(ring, Tint(hue, RegionLineAlpha), 1.5f, 0, id, 1.2f);
            lines.AddPolyline(floor, Tint(hue, RegionFloorAlpha), 1.1f, 0, id);
        }

        /// <summary>A region's half-extents across, up and in depth, kept inside the space.</summary>
        static void RegionRadii(Vector3 center, Vector3 spread, out float rx, out float ry, out float rz)
        {
            rx = Mathf.Max(RegionMinRadius, RegionSpread * spread.x);
            ry = Mathf.Max(RegionMinRadius, RegionSpread * spread.y);
            rz = Mathf.Max(RegionMinRadius, RegionSpread * spread.z);
            rx = Mathf.Min(rx, Mathf.Min(center.x - DesireX, FearX - center.x) + RegionMinRadius);
            ry = Mathf.Min(ry, Mathf.Min(center.y - EmotionY, ReasonY - center.y) + RegionMinRadius);
            rz = Mathf.Min(rz, Mathf.Min(center.z - NowZ, FutureZ - center.z) + RegionMinRadius);
        }

        // ------------------------------------------------------------------ labels and anchors

        /// <summary>The labels and anchors that do not change with the year: the walls, the drives, the chemicals.</summary>
        void AddStaticLabels()
        {
            TextAlignmentOptions center = TextAlignmentOptions.Center, left = TextAlignmentOptions.Left,
                right = TextAlignmentOptions.Right;
            PsycheFile psyche = data.Psyche ?? new PsycheFile();

            // the poles: heading above each arrow, the five drives inside it
            AddPole(-1, "DESIRE", Rose, psyche.Desires, DefaultDesires, IdDesirePole);
            AddPole(1, "FEAR", Ice, psyche.Fears, DefaultFears, IdFearPole);

            // the operating systems: above the back wall's top edge, below the near floor edge
            string dim = "<color=#" + Hex(GraphStyle.TextDim) + ">";
            Vector3 top = new Vector3(0, ReasonY, FutureZ), bottom = new Vector3(0, EmotionY, NowZ);
            Add(HigherText + "  " + dim + "— " + HigherDecisions + "</color>", top, 14, GraphStyle.Text, center, OsPriority,
                "mind:higher", IdRange.Single(IdBrain), new Vector2(0, OsOffset));
            Add(DefaultText + "  " + dim + "— " + DefaultDecisions + "</color>", bottom, 14, GraphStyle.Text, center,
                OsPriority, "mind:default", IdRange.Single(IdBrain), new Vector2(0, -OsOffset - 2));
            RegisterOs(psyche, "higher", "mind:higher", "Higher OS: reason", top);
            RegisterOs(psyche, "default", "mind:default", "Default OS: emotion", bottom);

            // time: now at the near end of the floor's axis, strategy at the far end
            Vector3 now = new Vector3(TimeAxisX, EmotionY, NowZ), future = new Vector3(TimeAxisX, EmotionY, FutureZ);
            IdRange time = IdRange.Single(IdTimeAxis);
            Add("NOW", now, 13, GraphStyle.Text, right, AxisPriority, "mind:now", time, new Vector2(-8, -10));
            strategyLabel = Add("STRATEGY: the future", future, 13, GraphStyle.Text, right, AxisPriority, "mind:strategy", time,
                new Vector2(-12, 2));
            Register("mind:now", "Now", MindsEye(psyche, "world_state",
                    "The present: a running model of the world that predicts the senses and flags what matters now.") +
                " Near the viewer: spending and choices for today.", now, time);
            Register("mind:strategy", "Strategy: the future", MindsEye(psyche, "strategy",
                    "Prospection toward a goal: simulating futures and choosing a path.") +
                " Far from the viewer: people whose present bias is low and who save for years ahead. Memory, the past, " +
                "feeds both.", future, time);

            // the neurochemicals
            List<Chemical> chemicals = psyche.Chemicals ?? new List<Chemical>();
            for (int k = 0; k < chemicals.Count; k++)
            {
                Chemical ch = chemicals[k];
                if (ch == null || string.IsNullOrEmpty(ch.Id)) continue;
                Vector3 place = ChemicalPlace(ch, k);
                IdRange ids = IdRange.Single(EconomyIds.MindChemical + k);
                Color hue = Color.Lerp(PoleColor(ch.Pole), GraphStyle.Text, 0.35f);
                bool before = place.x > 0 || ChemicalLabelsLeft.Contains(ch.Id);
                LabelSpec spec = Add(ch.Name ?? ch.Id, place, 11, hue, before ? right : left, ChemicalPriority,
                    "chem:" + ch.Id, ids);
                chemicalLabels.Add((spec, before ? -1 : 1));
                Register("chem:" + ch.Id, ch.Name ?? ch.Id, ChemicalBlurb(ch), place, ids);
            }
        }

        /// <summary>A pole's heading and its drives' names, with an anchor per drive and one for the pole.</summary>
        void AddPole(int side, string heading, Color hue, List<Drive> drives, string[] fallback, int poleId)
        {
            float x = side * 0.5f * (PoleTail + PoleNeck);
            IdRange pole = new IdRange(poleId, poleId + 5);
            Vector3 head = new Vector3(side * 0.5f * (PoleTail + PoleTip), PoleY + PoleHead, NowZ);
            string poleKey = side < 0 ? "drive:desire" : "drive:fear";
            Add(heading, head, 17, hue, TextAlignmentOptions.Center, HeadingPriority, poleKey, pole, new Vector2(0, 16));

            int n = drives != null && drives.Count > 0 ? Math.Min(5, drives.Count) : fallback.Length;
            StringBuilder names = new StringBuilder();
            for (int k = 0; k < n; k++)
            {
                Drive d = drives != null && k < drives.Count ? drives[k] : null;
                string id = d?.Id ?? fallback[k];
                string name = d?.Name ?? Capitalize(id);
                if (k > 0) names.Append(", ");
                names.Append(name.ToLowerInvariant());
                Vector3 place = new Vector3(x, PoleY + (0.5f * (n - 1) - k) * DriveRowStep, NowZ);
                IdRange ids = IdRange.Single(poleId + 1 + k);
                driveLabels.Add(Add(name, place, 13, GraphStyle.Text, TextAlignmentOptions.Center, DrivePriority, "drive:" + id,
                    ids));
                Register("drive:" + id, name, DriveBlurb(d, side), place, ids);
            }

            string blurb = side < 0
                ? "DESIRE pushes from the left: " + names + ". Money spent moving toward what people want: food and " +
                  "drink, belonging, order, a home, a mate. Across the space, a person stands as far left as their " +
                  "spending is moved by desire."
                : "FEAR pushes from the right: " + names + ". Money spent moving away from what people fear: going " +
                  "without, being alone, violence, being unprotected, ending without a family. Across the space, a " +
                  "person stands as far right as their spending is moved by fear.";
            Register(poleKey, heading == "DESIRE" ? "Desire" : "Fear", blurb, head, pole);
        }

        /// <summary>
        /// The labels and anchors that follow the year: categories, regions, the title, the caption, the people.
        /// </summary>
        void AddYearLabels()
        {
            TextAlignmentOptions center = TextAlignmentOptions.Center;
            for (int k = 0; k < 7; k++)
            {
                Category cat = categories[k];
                IdRange ids = IdRange.Single(EconomyIds.MindCategory + k);
                // aligned and offset beside its circle in ApplyYear (the side depends on the frame)
                categoryLabels[k] = Add(cat.Name ?? cat.Id, categoryPlace[k], 12, GraphStyle.Text, TextAlignmentOptions.Left,
                    CategoryPriority + 0.1f * (7 - k), "mind:" + cat.Id, ids);
                categoryAnchors[k] = Register("mind:" + cat.Id, cat.Name ?? cat.Id, cat.Blurb, categoryPlace[k], ids);
            }

            controlLabel = Add("owning the path", CenterLocal, 13, Gold, TextAlignmentOptions.Left, RegionPriority,
                "mind:control", IdRange.Single(IdControl), new Vector2(6, 0));
            fantasyLabel = Add("buying fantasy, living in the now", CenterLocal, 13, Color.Lerp(Rose, GraphStyle.Text, 0.25f),
                TextAlignmentOptions.Center, RegionPriority - 1, "mind:fantasy", IdRange.Single(IdFantasy), new Vector2(0, -12));
            controlAnchor = Register("mind:control", "Owning the path", "", CenterLocal, IdRange.Single(IdControl));
            fantasyAnchor = Register("mind:fantasy", "Buying fantasy, living in the now", "", CenterLocal,
                IdRange.Single(IdFantasy));
            IdRange everyone = population != null ? population.LineIds : IdRange.Empty;
            peopleAnchor = Register("mind:people", "The people", "", CenterLocal, everyone);

            Vector3 top = new Vector3(0, ReasonY, FutureZ);
            IdRange all = new IdRange(EconomyIds.MindCategory, EconomyIds.MindAxis + 99);
            for (int i = 0; i < CaptionLines; i++)
            {
                captionLabels[i] = Add("·", top, 12, i == 0 ? GraphStyle.Text : ValueText, center,
                    CaptionPriority - 0.1f * i, "mind:people", everyone, Vector2.zero);
            }

            titleLabel = Add("Desire, fear and reason", top, 16, GraphStyle.Text, center, TitlePriority, "mind:space", all);
            spaceAnchor = Register("mind:space", "The mind", "", CenterLocal, all);
        }

        /// <summary>
        /// What the screen's shape changes (<see cref="portrait"/>): a portrait frame draws labels half again as large
        /// (<see cref="ScreenLayout.PortraitLabelZoom"/>) over a station seen from farther away, so the drives' names
        /// (which would spill out of their arrows; the headings' tooltips keep them) hide, and the far end of the time
        /// axis is labeled with one word just above it, clear of the desire arrow and of social status's circle. The
        /// caption, the categories' and the regions' labels follow in <see cref="ApplyYear"/> (shorter texts; a new
        /// alignment shows when the text changes, which it does with the frame).
        /// </summary>
        void ApplyFrame(bool live)
        {
            foreach (LabelSpec d in driveLabels) d.Hidden = portrait;
            float gap = portrait ? PortraitChemicalLabelGap : ChemicalLabelGap;
            foreach ((LabelSpec label, int side) in chemicalLabels) label.PixelOffset = new Vector2(side * gap, 0);
            strategyLabel.Align = portrait ? TextAlignmentOptions.Center : TextAlignmentOptions.Right;
            strategyLabel.PixelOffset = portrait ? new Vector2(0, 16) : new Vector2(-12, 2);
            SetText(strategyLabel, portrait ? "STRATEGY" : "STRATEGY: the future", live);
        }

        /// <summary>
        /// Points the year's labels and anchors at the year's places and gives them the year's texts (through the label
        /// system on the main thread, so shown labels re-measure; directly in Prepare).
        /// </summary>
        void ApplyYear(MindYear m, bool live)
        {
            double years = Math.Max(0.5, DeepTime.NowYear - m.Year);
            double spent = 0;
            for (int k = 0; k < 6; k++) spent += Math.Max(0, m.Spent[k]);
            for (int k = 0; k < 7; k++)
            {
                Category cat = categories[k];
                Vector3 c = categoryPlace[k];
                LabelSpec spec = categoryLabels[k];
                Side side = SideOf(k);
                // a new alignment shows when the text changes, which it does with the frame (see CategoryText)
                spec.Align = side == Side.Left ? TextAlignmentOptions.Right
                    : side == Side.Right ? TextAlignmentOptions.Left : TextAlignmentOptions.Center;
                spec.PixelOffset = side == Side.Below ? new Vector2(0, -9) : side == Side.Above ? new Vector2(0, 9) : Vector2.zero;
                spec.Data = station.World(CategoryLabelPlace(m, k, out _));
                SetText(spec, CategoryText(cat, m.Spent[k]), live);

                Anchor a = categoryAnchors[k];
                a.Blurb = CategoryBlurb(cat, m, k, spent);
                a.Y = c.y;
                a.YearsAgo = a.EndYearsAgo = years;
                a.Fixed = station.World(c);
            }

            PlaceRegion(m, controlLabel, controlAnchor, m.ControlCenter, m.ControlSpread, m.Control > 0, false, years);
            PlaceRegion(m, fantasyLabel, fantasyAnchor, m.FantasyCenter, m.FantasySpread, m.Fantasy > 0, true, years);
            SetText(controlLabel, "owning the path  " + Dim() + Percent(Share(m.Control, m.Adults)) +
                                  (portrait ? "" : " of adults") + "</color>", live);
            SetText(fantasyLabel, "buying fantasy, living in the now  " + Dim() + Percent(Share(m.Fantasy, m.Adults)) +
                                  (portrait ? "" : " of adults") + "</color>", live);
            controlAnchor.Blurb = ControlBlurb(m);
            fantasyAnchor.Blurb = FantasyBlurb(m);
            peopleAnchor.Blurb = PeopleBlurb(m);
            peopleAnchor.Y = CenterLocal.y;
            peopleAnchor.YearsAgo = peopleAnchor.EndYearsAgo = years;
            peopleAnchor.Fixed = station.World(CenterLocal);

            SetText(titleLabel, "Desire, fear and reason, " + m.Year.ToString(Ci), live);
            spaceAnchor.Blurb = SpaceBlurb(m);
            spaceAnchor.Y = CenterLocal.y;
            spaceAnchor.YearsAgo = spaceAnchor.EndYearsAgo = years;
            spaceAnchor.Fixed = station.World(CenterLocal);
            ApplyCaption(m, live);
            if (live) labelSystem.MarkDirty();
        }

        /// <summary>
        /// Where a category's label stands: beside its circle on its side, or, when the circle stands in the people's
        /// cloud and its label goes left or right, in a column just outside the cloud (<paramref name="leaderFrom"/> is
        /// then the circle's edge the leader starts from; otherwise the label's own place).
        /// </summary>
        Vector3 CategoryLabelPlace(MindYear m, int k, out Vector3 leaderFrom)
        {
            Vector3 c = categoryPlace[k];
            float r = NodeRadiusOf(m, k);
            Side side = SideOf(k);
            Vector3 place = LabelPlace(c, r + LabelGap, side);
            leaderFrom = place;
            bool inCloud = c.x + r > m.CloudLeft && c.x - r < m.CloudRight;
            if (!inCloud) return place;
            if (side == Side.Left && place.x > m.CloudLeft - ColumnGap)
            {
                leaderFrom = new Vector3(c.x - r, c.y, c.z);
                place.x = Mathf.Max(DesireX, m.CloudLeft - ColumnGap);
            }
            else if (side == Side.Right && place.x < m.CloudRight + ColumnGap)
            {
                leaderFrom = new Vector3(c.x + r, c.y, c.z);
                place.x = Mathf.Min(FearX, m.CloudRight + ColumnGap);
            }

            return place;
        }

        /// <summary>The side of its circle a category's label stands on in the current frame.</summary>
        Side SideOf(int k) => portrait ? portraitSide[k] : categorySide[k];

        /// <summary>A point beside a circle (or an ellipse's half-width) on one side, in the circle's plane.</summary>
        static Vector3 LabelPlace(Vector3 c, float reach, Side side)
        {
            switch (side)
            {
                case Side.Left: return new Vector3(c.x - reach, c.y, c.z);
                case Side.Right: return new Vector3(c.x + reach, c.y, c.z);
                case Side.Below: return new Vector3(c.x, c.y - reach, c.z);
                default: return new Vector3(c.x, c.y + reach, c.z);
            }
        }

        /// <summary>
        /// A region's label: right of its ring, outside the cloud (owning the path: the gold crowd would cover it), or
        /// <paramref name="onFloor"/> under the near edge of its footprint, in front of the cloud (fantasy). Its anchor is
        /// at its middle; the label hides when nobody is in the region.
        /// </summary>
        void PlaceRegion(MindYear m, LabelSpec label, Anchor anchor, Vector3 center, Vector3 spread, bool any, bool onFloor,
            double years)
        {
            RegionRadii(center, spread, out float rx, out _, out float rz);
            float right = Mathf.Min(FearX, Mathf.Max(center.x + rx + LabelGap, m.CloudRight + ColumnGap));
            label.Data = station.World(onFloor
                ? new Vector3(center.x, EmotionY, center.z - rz)
                : new Vector3(right, center.y, center.z));
            label.Hidden = !any;
            anchor.Y = center.y;
            anchor.YearsAgo = anchor.EndYearsAgo = years;
            anchor.Fixed = station.World(center);
        }

        /// <summary>
        /// The caption's lines above the title edge for the frame: four long lines in landscape, seven short ones in
        /// portrait (the rest hidden, keeping a text so the label system keeps them).
        /// </summary>
        void ApplyCaption(MindYear m, bool live)
        {
            List<string> lines = CaptionText(m, portrait);
            int used = Math.Min(CaptionLines, lines.Count);
            for (int i = 0; i < CaptionLines; i++)
            {
                LabelSpec spec = captionLabels[i];
                bool shown = i < used;
                spec.Hidden = !shown;
                if (!shown) continue;
                SetText(spec, lines[i], live);
                spec.PixelOffset = new Vector2(0, OsOffset + CaptionOffset + CaptionStep * (used - 1 - i));
            }

            titleLabel.PixelOffset = new Vector2(0, OsOffset + CaptionOffset + CaptionStep * (used - 1) + TitleGap);
            if (live) labelSystem.MarkDirty();
        }

        void SetText(LabelSpec spec, string text, bool live)
        {
            if (live) labelSystem.SetText(spec, text);
            else spec.Text = text;
        }

        /// <summary>A label at a local point of the station (a world position, shown only near the station).</summary>
        LabelSpec Add(string text, Vector3 local, float size, Color color, TextAlignmentOptions align, float priority,
            string anchor, IdRange ids, Vector2 offset = default)
        {
            LabelSpec spec = new LabelSpec
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
            labels.Add(spec);
            return spec;
        }

        Anchor Register(string key, string label, string blurb, Vector3 local, IdRange ids)
        {
            Anchor a = new Anchor
            {
                Key = key,
                Label = label,
                Blurb = blurb,
                Level = GraphLevel.Humans,
                YearsAgo = 0.5,
                EndYearsAgo = 0,
                Y = local.y,
                Rho = EconomyStyle.FramingRho,
                Ids = ids,
                Tier = 1,
                Fixed = station.World(local)
            };
            Anchors.Register(a);
            return a;
        }

        /// <summary>An operating system's anchor (psyche.json os.higher / os.default: name, decisions, share).</summary>
        void RegisterOs(PsycheFile psyche, string which, string key, string name, Vector3 local)
        {
            JObject os = psyche.Os?[which] as JObject;
            StringBuilder s = new StringBuilder();
            string title = Text(os?["name"]) ?? name;
            s.Append("Decides by ").Append(which == "higher" ? "optimization and reflection" : "feeling");
            List<string> decisions = new List<string>();
            if (os?["decisions"] is JArray list)
            {
                foreach (JToken d in list)
                {
                    if (Text(d) is string text) decisions.Add(text);
                }
            }

            for (int i = 0; i < decisions.Count; i++)
            {
                bool last = i == decisions.Count - 1;
                s.Append(i == 0 ? " and leads to " : last && decisions.Count > 2 ? ", and " : last ? " and " : ", ")
                    .Append(decisions[i]);
            }

            s.Append('.');
            JToken share = os?["share"];
            if (share != null && (share.Type == JTokenType.Float || share.Type == JTokenType.Integer))
            {
                s.Append(" About ").Append(Percent(share.Value<double>())).Append(" of everyday economic decisions ")
                    .Append("(a modeling parameter from habit, attention and reflection studies).");
            }

            s.Append(which == "higher"
                ? " Up the space, a person stands as high as the share of their decisions made by reason: conscientiousness, " +
                  "openness and a sense of control raise it, age matures it, debt, a lost job and a thin buffer lower it."
                : " Down the space: decisions by feeling. Fast and usually right in familiar settings, costly under threat, " +
                  "lack of sleep, hunger and stress.");
            Register(key, title, s.ToString(), local, IdRange.Single(IdBrain));
        }

        /// <summary>The blurb of an element of the mind's eye (psyche.json mindsEye: memory, world state, strategy).</summary>
        static string MindsEye(PsycheFile psyche, string id, string fallback)
        {
            if (psyche.MindsEye is JArray list)
            {
                foreach (JToken t in list)
                {
                    if (t is JObject o && Text(o["id"]) == id) return Text(o["blurb"]) ?? fallback;
                }
            }

            return fallback;
        }

        /// <summary>A JSON string's text, or null for anything else (free-form data never throws here).</summary>
        static string Text(JToken t) => t != null && t.Type == JTokenType.String ? (string)t : null;

        // ------------------------------------------------------------------ texts

        /// <summary>"Escapism  $1.79T · fantasy 62%" (in a portrait frame without the fantasy share).</summary>
        string CategoryText(Category cat, double spent) =>
            (cat.Name ?? cat.Id) + "  " + Dim() + MoneyCircuit.Money(spent) +
            (portrait ? "" : " · fantasy " + Percent(cat.Fantasy)) + "</color>";

        string CategoryBlurb(Category cat, MindYear m, int k, double spent)
        {
            StringBuilder s = new StringBuilder();
            if (!string.IsNullOrEmpty(cat.Blurb)) s.Append(cat.Blurb).Append(' ');
            s.Append(m.Year.ToString(Ci)).Append(": ").Append(MoneyCircuit.Money(m.Spent[k]));
            if (k < 6 && spent > 0) s.Append(", ").Append(Percent(m.Spent[k] / spent)).Append(" of household spending");
            s.Append(". Moved ").Append(Percent(cat.FearShare)).Append(" by fear and ").Append(Percent(1 - cat.FearShare))
                .Append(" by desire; ").Append(Percent(cat.Fantasy))
                .Append(" of it buys a fantasy (an escape, a status signal, a lottery ticket). The circle's area is the money ")
                .Append("in 2025 dollars.");
            return s.ToString();
        }

        static string DriveBlurb(Drive d, int side)
        {
            if (d == null) return side < 0 ? "A desire the money moves toward." : "A fear the money moves away from.";
            StringBuilder s = new StringBuilder();
            if (!string.IsNullOrEmpty(d.Blurb)) s.Append(d.Blurb).Append(' ');
            if (d.Weight > 0)
            {
                s.Append(Percent(d.Weight)).Append(side < 0 ? " of the weight of the desires" : " of the weight of the fears")
                    .Append(" in the population, before life stage and circumstance.");
            }

            if (d.Chemicals != null && d.Chemicals.Count > 0)
            {
                s.Append(" Chemistry: ").Append(string.Join(", ", d.Chemicals)).Append('.');
            }
            return s.ToString();
        }

        static string ChemicalBlurb(Chemical ch)
        {
            StringBuilder s = new StringBuilder();
            if (!string.IsNullOrEmpty(ch.Function)) s.Append(ch.Function).Append(' ');
            if (ch.Markets != null && ch.Markets.Count > 0)
            {
                s.Append("Markets built on it: ");
                for (int i = 0; i < ch.Markets.Count && i < 4; i++)
                {
                    if (i > 0) s.Append(", ");
                    s.Append(ch.Markets[i]);
                }

                s.Append(". ");
            }

            if (!string.IsNullOrEmpty(ch.Caveat)) s.Append("Caveat: ").Append(ch.Caveat);
            if (!string.IsNullOrEmpty(ch.Source)) s.Append(" (").Append(ch.Source).Append(')');
            return s.ToString();
        }

        string ControlBlurb(MindYear m)
        {
            if (m.Control == 0)
            {
                return "Nobody in the modeled population is in control of their path in " + m.Year.ToString(Ci) + ".";
            }

            return $"In {m.Year.ToString(Ci)}, {Percent(Share(m.Control, m.Adults))} of adults are in control of their path: " +
                   "they own a business or hold years of spending in savings, save steadily, carry little debt, decide by " +
                   "reason and spend little on fantasy (the model's agency rule, its cut set once so that 2025 matches " +
                   "the evidence and then applied to every year). The gold ring is where they are, reason " +
                   $"{F2(m.ReasonMean[1])} against {F2(m.ReasonMean[0])} for the rest. Fear moves " +
                   Compare(m.FearMean[1], m.FearMean[0], "as much of", "less of", "more of") + " their spending as the " +
                   $"rest's ({Percent(m.FearMean[1])} against {Percent(m.FearMean[0])}), and they buy " +
                   Compare(m.FantasyMean[1], m.FantasyMean[0], "as much", "less", "more") +
                   $" fantasy ({Percent(m.FantasyMean[1])} against {Percent(m.FantasyMean[0])}).";
        }

        string FantasyBlurb(MindYear m)
        {
            float span = FearX - DesireX, height = ReasonY - EmotionY;
            string where = m.Fantasy == 0
                ? ""
                : $" The rose ring is where they are: fear moves {Percent((m.FantasyCenter.x - DesireX) / span)} of their " +
                  $"spending (every adult: {Percent(m.FearAll)}), reason {F2((m.FantasyCenter.y - EmotionY) / height)} " +
                  $"(every adult: {F2(m.ReasonAll)}).";
            return $"In {m.Year.ToString(Ci)}, {Percent(Share(m.FantasyBuyers, m.Adults))} of adults spend more than " +
                   $"{Percent(fantasyCut)} of their money on fantasy (escapes, status signals, lottery tickets), the most the " +
                   $"agency rule allows before it costs control; {Percent(Share(m.Fantasy, m.Adults))} of adults also lean " +
                   "toward the present rather than the future." + where +
                   (m.FantasyByQuintile[4] > m.FantasyByQuintile[0] + 0.01 ? " Fantasy is not the vice of the poor:" : "") +
                   $" the fantasy share of spending {FantasyTrend(m)}, {Percent(m.FantasyByQuintile[0])} in the poorest fifth " +
                   $"of households and {Percent(m.FantasyByQuintile[4])} in the richest.";
        }

        /// <summary>How the fantasy share of spending moves from the poorest fifth to the richest (one point counts).</summary>
        static string FantasyTrend(MindYear m)
        {
            double poorest = m.FantasyByQuintile[0], richest = m.FantasyByQuintile[4];
            return richest > poorest + 0.01 ? "rises with income" : richest < poorest - 0.01 ? "falls with income"
                : "barely moves with income";
        }

        string PeopleBlurb(MindYear m) =>
            $"Every adult alive in {m.Year.ToString(Ci)}: {m.Adults.ToString("N0", Ci)} crosses, each for " +
            $"{peoplePerLine.ToString("N0", Ci)} people. " +
            "Across: the share of their spending moved by fear rather than desire. Up: the share of their decisions made " +
            "by reason. Deep: their orientation toward the future (patience and saving). Gold: in control of their path. " +
            $"Fear moves {Percent(m.MoneyFear)} of household money.";

        string SpaceBlurb(MindYear m) =>
            "The notebook's mind as a space: desire pushes from the left, fear from the right; reason, the higher OS, " +
            "above emotion, the default OS; the present near, strategy for the future far. The circles are where each " +
            $"kind of spending sits, sized by its money in {m.Year.ToString(Ci)}; the crosses are the people.";

        /// <summary>The caption: the model's findings for the year (landscape: four lines; portrait: seven short ones).</summary>
        List<string> CaptionText(MindYear m, bool tall)
        {
            List<string> lines = new List<string>(CaptionLines);
            string year = m.Year.ToString(Ci);
            if (m.Adults == 0)
            {
                lines.Add(year + ": the population's economic lives were not simulated");
                return lines;
            }

            string control = Percent(Share(m.Control, m.Adults));
            string fantasy = Compare(m.FantasyMean[1], m.FantasyMean[0], "as much fantasy as", "less fantasy than",
                "more fantasy than");
            string fear = Compare(m.FearMean[1], m.FearMean[0], "as much", "less", "more");
            string fantasyNumbers = Percent(m.FantasyMean[1]) + " of spending vs " + Percent(m.FantasyMean[0]);
            string fearNumbers = Percent(m.FearMean[1]) + " vs " + Percent(m.FearMean[0]);
            string reason = "reason " + F2(m.ReasonMean[1]) + " vs " + F2(m.ReasonMean[0]);
            string age = "age " + m.AgeMean[1].ToString("0", Ci) + " vs " + m.AgeMean[0].ToString("0", Ci);
            string inherited = "inherited from a parent " + Percent(m.Inherited[1]) + " vs " + Percent(m.Inherited[0]);
            string business = "self-employed " + Percent(m.SelfEmployed[1]) + " vs " + Percent(m.SelfEmployed[0]);
            string poorest = Percent(m.FantasyByQuintile[0]), richest = Percent(m.FantasyByQuintile[4]);
            string sep = "  ·  ";
            if (!tall)
            {
                lines.Add(year + ": " + control + " of adults are in control of their path (gold)");
                lines.Add("They buy " + fantasy + " everyone else (" + fantasyNumbers + ") and spend " + fear +
                          " out of fear (" + fearNumbers + ")");
                lines.Add("What sets them apart: " + reason + sep + age + sep + inherited + sep + business);
                lines.Add("Fear moves " + Percent(m.MoneyFear) + " of household money" + sep + "fantasy " + FantasyTrend(m) +
                          ": " + poorest + " of spending in the poorest fifth, " + richest + " in the richest");
            }
            else
            {
                lines.Add(year + ": " + control + " of adults are in control (gold)");
                lines.Add("They buy " + fantasy + " the rest (" + Percent(m.FantasyMean[1]) + " vs " +
                          Percent(m.FantasyMean[0]) + ")");
                lines.Add("and spend " + fear + " out of fear (" + fearNumbers + ")");
                lines.Add("Apart: " + reason + ", " + age + ",");
                lines.Add(inherited + ", " + business);
                lines.Add("Fear moves " + Percent(m.MoneyFear) + " of household money; fantasy");
                lines.Add(FantasyTrend(m) + ": " + poorest + " poorest fifth, " + richest + " richest");
            }

            return lines;
        }

        /// <summary>A comparison in words: the same (within two points), less or more.</summary>
        static string Compare(double mine, double others, string same, string less, string more) =>
            Math.Abs(mine - others) < 0.02 ? same : mine < others ? less : more;

        /// <summary>The year's distributions for the log: deciles of the people's places and fantasy.</summary>
        string Distribution(int year)
        {
            if (lives == null || !lives.Ready) return "no economic lives";
            List<float> fear = new List<float>(), reason = new List<float>(), future = new List<float>();
            List<float> fantasy = new List<float>();
            for (int i = 0; i < lives.Sim.People.Count; i++)
            {
                if (!lives.TryGet(i, year, out PersonYear r) || !r.Adult) continue;
                fear.Add(r.FearShare);
                reason.Add(r.Reason);
                future.Add(r.Future);
                fantasy.Add(r.Fantasy);
            }

            return "p10/p50/p90 fear " + Deciles(fear) + ", reason " + Deciles(reason) + ", future " + Deciles(future) +
                   ", fantasy " + Deciles(fantasy);
        }

        static string Deciles(List<float> v)
        {
            if (v.Count == 0) return "-";
            v.Sort();
            float At(float q) => v[Mathf.Clamp((int)(q * (v.Count - 1)), 0, v.Count - 1)];
            return At(0.1f).ToString("0.00", Ci) + "/" + At(0.5f).ToString("0.00", Ci) + "/" + At(0.9f).ToString("0.00", Ci);
        }

        static double Share(int part, int whole) => whole > 0 ? part / (double)whole : 0;

        static string Percent(double share) => (100 * share).ToString("0", Ci) + "%";

        static string F2(double v) => v.ToString("0.00", Ci);

        static string Capitalize(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        static string Dim() => "<color=#" + Hex(ValueText) + ">";

        static string Hex(Color c) =>
            ((int)(Mathf.Clamp01(c.r) * 255)).ToString("X2") + ((int)(Mathf.Clamp01(c.g) * 255)).ToString("X2") +
            ((int)(Mathf.Clamp01(c.b) * 255)).ToString("X2");

        // ------------------------------------------------------------------ upload (main thread)

        public override void Upload(GraphContext ctx)
        {
            if (pendingFills == null) return;
            int q = EconomyStyle.QueueStations + QueueOffset;
            backdropMat = Fills(q);
            frameFillMat = Fills(q + 1);
            yearFillMat = Fills(q + 1);
            frameLineMat = GraphMaterials.Raw(GraphMaterials.Line(Color.white, 1f, q + 2));
            yearLineMat = GraphMaterials.Raw(GraphMaterials.Line(Color.white, 1f, q + 3));

            backdropFilter = AddStationMesh("MindBackdrop", backdropFills.ToMesh("MindBackdrop"), backdropMat);
            frameFillFilter = AddStationMesh("MindFrame", frameFills.ToMesh("MindFrame"), frameFillMat);
            frameLineFilter = AddStationMesh("MindFrameLines", frameLines.ToMesh("MindFrameLines"), frameLineMat);
            yearFillFilter = AddStationMesh("MindYear", pendingFills.ToMesh("MindYear"), yearFillMat);
            yearLineFilter = AddStationMesh("MindPeople", pendingLines.ToMesh("MindPeople"), yearLineMat);
            backdropFills = frameFills = pendingFills = null;
            frameLines = pendingLines = null;

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
            MeshFilter[] filters = { backdropFilter, frameFillFilter, frameLineFilter, yearFillFilter, yearLineFilter };
            foreach (MeshFilter f in filters)
            {
                if (f != null && f.sharedMesh != null) Destroy(f.sharedMesh);
            }
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

            if (ScreenLayout.IsPortrait != portrait && shown != null)
            {
                // the frame moves some labels to other sides of their circles, and their leaders with them
                portrait = ScreenLayout.IsPortrait;
                ApplyFrame(true);
                Rebuild(shown.Year);
            }

            if (rig == null) return;
            float d = Vector3.Distance(rig.Pose.Target, station.World(CenterLocal));
            float alpha = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(FadeNear, FadeFar, d));
            float screen = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(BackdropNear, BackdropFar, d));
            if (Mathf.Abs(alpha - stationAlpha) > 1e-3f)
            {
                stationAlpha = alpha;
                GraphMaterials.SetAlpha(frameFillMat, alpha);
                GraphMaterials.SetAlpha(frameLineMat, alpha);
                GraphMaterials.SetAlpha(yearFillMat, alpha);
                GraphMaterials.SetAlpha(yearLineMat, alpha);
                bool visible = alpha > 0.002f;
                SetVisible(frameFillFilter, visible);
                SetVisible(frameLineFilter, visible);
                SetVisible(yearFillFilter, visible);
                SetVisible(yearLineFilter, visible);
            }

            if (Mathf.Abs(screen - backdropAlpha) > 1e-3f)
            {
                backdropAlpha = screen;
                GraphMaterials.SetAlpha(backdropMat, screen);
                SetVisible(backdropFilter, screen > 0.002f);
            }
        }

        static void SetVisible(MeshFilter filter, bool visible)
        {
            if (filter == null) return;
            MeshRenderer r = filter.GetComponent<MeshRenderer>();
            if (r != null && r.enabled != visible) r.enabled = visible;
        }

        /// <summary>Builds another year: new meshes (the old ones destroyed), label texts and places, anchors.</summary>
        void Rebuild(int year)
        {
            Stopwatch sw = Stopwatch.StartNew();
            MindYear m = Measure(year);
            BuildYear(m, out SurfaceMeshBuilder fills, out LineMeshBuilder lines);
            Replace(yearFillFilter, fills.ToMesh("MindYear"));
            Replace(yearLineFilter, lines.ToMesh("MindPeople"));
            ApplyYear(m, true);
            shown = m;
            if (sw.ElapsedMilliseconds > RebuildBudgetMs)
            {
                Debug.LogWarning($"[Why] MindLayer: rebuilding {year} took {sw.ElapsedMilliseconds} ms");
            }
        }
    }
}
