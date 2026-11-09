using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using Why.Economy.Emphasis;
using Why.Economy.Land;
using Why.Economy.Model;
using Why.Economy.UI;
using Why.UI;
using Debug = UnityEngine.Debug;
using Roles = Why.Economy.EconomyStyle.Roles;

namespace Why.Economy.Layers
{
    /// <summary>
    /// The economy's sweep through time, redrawn in the point-cloud language (the user's eighth direction, 8 Nov 2026, verbatim
    /// in Docs/research/advantage/brief-v6.md: "redesign the sweep using this design aesthetic", "add every major player in
    /// every industry"; design Docs/hills-design/SWEEP.md). It is the Wave view (preset "overview", key 1) unless the app
    /// runs with <c>-classicwave</c>, which brings back the ridges, ribs and gold lines for comparison (<see cref="On"/>).
    /// <list type="bullet">
    /// <item><b>The cross-section at the year shown</b>, square to the camera: every industry a point-cloud body as large as its
    /// share of that year's GDP (<see cref="SweepLayout"/>, drawn at 2025's scale), its major companies gold bodies packed
    /// around it by year-end market value (a quarter of an industry's volume per dollar: a stock against a year's flow), the
    /// smallest as dots; those that existed without a value are not drawn but counted ("+24 not measured") in their
    /// industry's name and the title. The strata stand top to bottom by how much of their sales households buy (the 2024 Use
    /// table): services, manufacturing and infrastructure, tech, raw materials, and the government at the base. Every row is
    /// stood with the room its labels take at the camera's fitted scale (<see cref="FitStack"/>), so no name hangs into
    /// another row.</item>
    /// <item><b>Outlines and occlusion</b>, as the history, politics and advantage views draw them: each body's silhouette
    /// baked into its mesh from the view's direction (solid where its value is measured, dotted where approximate or
    /// estimated), and a depth-only solid inside it that hides what stands behind (the companies stand nearer than their
    /// industry, so a cluster is grouped by occlusion, not by a line).</item>
    /// <item><b>Time as a slant</b>, as politics draws it: one stream of dot rings per stratum receding to the screen's left
    /// and deeper (pitch lifts the deep past), fading, as wide as the stratum's share of each year's GDP; the industries'
    /// former peaks as faint dotted rings; a hovered or selected body's own rings back through its years.</item>
    /// <item><b>Motion at a frozen year</b>: the measured national accounts of that year (InstantModel) as eight packet lanes
    /// between the stack, the households and the owners' column and the government: spending, pay, owners' income, the
    /// state's taxes, purchases, benefits and pay. Each lane is as wide as its dollars.</item>
    /// </list>
    /// Nothing unmeasured is drawn as measured: an approximate (recalled) value draws faint, greyed and dotted with "≈"; an
    /// interpolated one likewise; a company with no value is counted, not drawn; an estimated industry split is dotted and "≈";
    /// Jev's judgments (which industry, who counts as major) show only on the hover card, in italics with their confidence.
    /// The view follows the year chip and the replay (the land's year), or a pinned state:
    /// "sweep:&lt;year&gt;[:in=&lt;stratum&gt;][:sel=&lt;id&gt;]".
    /// </summary>
    [GraphScenes(EconomyLayouts.HillsScene)]
    public sealed class HillSweepLayer : GraphLayer
    {
        public const string View = "overview", Sequence = "sweep";

        /// <summary>The redesign is the Wave view; <c>-classicwave</c> shows the old wave instead (A/B frames before the user's
        /// approval). Every hook that hides the old wave in the Wave view reads this.</summary>
        public static readonly bool On = !Environment.GetCommandLineArgs().Contains("-classicwave");

        /// <summary>After politics (67) and the moments' fences (68): OnFocus runs in this order, and history's (60) clears the
        /// portrait cards this view's hover card uses.</summary>
        public override int Order => 69;

        public override IEnumerable<string> RequiredTexts => On ? new[] { Companies.Path, Companies.LivesPath } : Array.Empty<string>();

        /// <summary>The view's title and subtitle in the catalog (HillPresets).</summary>
        public const string Title = "The US economy through time";

        public const string Subtitle = "Every industry a body as large as its share of the year's output, its major companies in gold beside it by market " +
                                       "value; the layers households buy from most on top, the government at the base; the past streams back to the left.";

        /// <summary>The view's key for the HUD's legend (its swatches are UI colours: the roles sRGB-encoded).</summary>
        public static readonly HillPresets.ViewKey Key = new HillPresets.ViewKey(new (string, Color)[]
            {
                ("Industries, by stratum", Roles.Encoded(Color.Lerp(Roles.TierRamp[2], Roles.InkText, .35f))),
                ("Companies (gold)", Roles.Encoded(Roles.Owners)), ("Approximate: dotted, ≈", Roles.Encoded(Color.Lerp(Roles.Owners, Roles.Missing, .5f))),
                ("Spending", Roles.Encoded(Roles.Commerce)),
                ("Pay", Roles.Encoded(Roles.People)), ("Owners' income", Roles.Encoded(Roles.Owners)),
                ("The state's flows", Roles.Encoded(Color.Lerp(Roles.State, Roles.InkText, .35f))),
            },
            "Sizes: shares of each year's GDP at 2025's scale; gold at a quarter of stone's volume per dollar (market value is a stock, value " +
            "added a year's flow). Strata by the share of their sales households buy. Market values year-end, measured from 1996 (dotted, ≈: " +
            "recalled). Major players and their industries: Jev's (TypeSafe) judgments. Moving: that year's national accounts.");

        // ------------------------------------------------------------------ the frame

        /// <summary>The content's place in the land's frame: in front of the latest crest, turned to face the view's camera, so
        /// its x runs to the screen's right, y up and z into the screen.</summary>
        const float RootX = 130, RootAhead = 30;

        /// <summary>The pose: straight from the future (yaw 180), a little from above; steeper on a portrait screen, where the
        /// rows wrap into a taller stack.</summary>
        const float Yaw = 180, Pitch = 8, PortraitPitch = 18;

        // ------------------------------------------------------------------ the bodies

        /// <summary>The radius an industry's or the households' body is built at; a transform scales it to its year's.</summary>
        const float IndustryRef = 30, HouseholdsRef = SweepLayout.HouseholdsR;

        /// <summary>The companies' shared shapes: (reference radius, detail, skin points under each surface point) for the
        /// full bodies of 10 units or more, the smaller full bodies, and the small bodies (no outline).</summary>
        static readonly (float r, int detail, int skin)[] Buckets = { (24, 2, 2), (10, 2, 1), (4, 1, 0) };

        const int BigBucket = 0, MidBucket = 1, SmallBucket = 2, Variants = 12;

        /// <summary>A full body this large or larger takes the big bucket's shape.</summary>
        const float BigR = 10;

        /// <summary>An approximate company draws at this share of its alpha, its points this far toward the no-data grey.</summary>
        const float ApproxAlpha = .55f, ApproxGrey = .3f;

        /// <summary>The selection dims everything else to this; a stratum's close-up dims the other strata to this.</summary>
        const float DimSelection = .35f, DimCloseUp = .15f;

        /// <summary>Seconds a body takes to its new place and size; a bankrupt company's fall and its fall's depth; a merged or
        /// acquired company's slide into its successor; the view's opening and the scale its bodies open from.</summary>
        const float TweenSeconds = .35f, FallSeconds = .9f, FallUnits = 30, MergeSeconds = .6f, EnterSeconds = .8f, EnterScale = .3f, BirthScale = .06f;

        /// <summary>A company's ghost (its last place, a grey hairline) stands this many years after the year its life ended;
        /// the largest this many are named (landscape).</summary>
        const int GhostYears = 2, NamedFates = 2;

        // ------------------------------------------------------------------ time

        /// <summary>The slant: a year Δ back stands L = 18 min(Δ, 25) + 8 max(0, Δ - 25) units to the screen's left,
        /// <see cref="SlantRise"/> L higher and 4 Δ deeper. The streams fill the screen to the left of the cross-section, which
        /// the fit leaves to them: only their last <see cref="StreamFitYears"/> years are among the essentials (the first frames
        /// fitted them back to 1947, which pushed the cross-section into the right half and left the screen's left third
        /// empty). 14 and 6 a year at the first frames' 1.14 pixels a unit; the rows' labels' room brings 2025 to 0.79, so 18 and
        /// 8 keep the streams as long on screen. The rise is the same for every stream and every year, a way into the depth,
        /// not a value: it carries the far years up into the screen's empty top left and the government's clear of the
        /// legend (a ring that would still stand under one of the HUD's blocks is not drawn, <see cref="UnderHud"/>).</summary>
        const float SlantNear = 18, SlantFar = 8, SlantDeep = 4, SlantRise = .28f;

        /// <summary>The years of each stream nearest the year shown that the camera's fit keeps on screen (fewer on a portrait
        /// screen, whose width the services row takes); the rest run on to the left, fading, where the fit has room or off the
        /// screen's edge.</summary>
        const int StreamFitYears = 10, PortraitStreamFitYears = 6;

        const int SlantYears = 25;

        /// <summary>The past fades: to <see cref="PastAlpha"/> over <see cref="FadeYears"/>, then to 0.6 of that over 50 more.</summary>
        const float FadeYears = 25, PastAlpha = .25f, DeepFadeYears = 50, DeepFade = .6f;

        /// <summary>A stream ring's dots: one every this many units around, 8 to 40 of them.</summary>
        const float RingDotStep = 1.8f;

        const int RingDotsMin = 8, RingDotsMax = 40;

        /// <summary>An industry's former peak share draws a faint dotted ring when it is at least this many times its share now;
        /// at most this many rings and this many named.</summary>
        const float PeakRatio = 1.4f;

        const int MaxPeaks = 6, NamedPeaks = 1;

        /// <summary>The years named along the top stream (a quarter-century apart: three names for 78 years), none within this
        /// many years of the year shown.</summary>
        static readonly int[] Decades = { 1950, 1975, 2000 };

        const int DecadeClear = 8;

        // ------------------------------------------------------------------ the circuit

        /// <summary>A lane bows this far out to the screen's right and toward the camera, so no body hides it; the second lane
        /// of a pair bows further.</summary>
        const float BowOut = 25, BowNear = 20, BowStep = 14;

        /// <summary>A lane's width per cube root of its dollars at 2025's scale ($bn a year).</summary>
        const float LaneK = .22f;

        /// <summary>Path units a second every packet travels.</summary>
        const float PacketSpeed = 12;

        /// <summary>The 8 lanes: the flows' ids (spending is its two halves' total: the split is judged and not drawn).</summary>
        enum Lane { Spending, Pay, Owners, TaxesIndustries, StateBuys, Benefits, TaxesHouseholds, PublicPay, Count }

        // ------------------------------------------------------------------ labels and the fit

        /// <summary>The companies named: the largest this many (landscape, portrait), and the selected one.</summary>
        const int Named = 8, PortraitNamed = 4;

        /// <summary>A stratum's close-up names every company of at least this value at 2025's scale ($bn), at most this many.</summary>
        const double CloseUpNamedX = 10;

        const int CloseUpNamed = 60;

        /// <summary>The labels a screen takes at most (the first frames showed about 70): landscape, and a portrait screen or a
        /// headset; past it the least important go first (the peak, the fates, the years, the column's names, then the smallest
        /// companies and industries). A portrait screen names its <see cref="PortraitIndustries"/> largest industries.</summary>
        const int WideLabels = 46, NarrowLabels = 30, PortraitIndustries = 10;

        /// <summary>The fit (as politics'): the screen's margin and the gap from the HUD's blocks (px at 1080p), the band of the
        /// screen's height whose blocks bound the whole width, the least side of a free rect, the passes and the distances.</summary>
        const float ScreenMargin = 16, BlockGap = 12, EdgeBand = .08f, MinFree = 120, MinDistance = 110, MaxDistance = 4000, Tie = 1e-3f;

        const int FitPasses = 6, HudSettleFrames = 8;

        /// <summary>The stack and the fit agree (<see cref="FitStack"/>): it stacks for a scale, fits, and stacks again for the
        /// fitted scale until the two are within this share, at most this many times; the first guesses (px a unit), and the
        /// share of its distance limit a fit may reach before the smoke run warns.</summary>
        const float FitTolerance = .015f, GuessScale = .85f, PortraitGuessScale = .75f, FitDistanceWarn = .9f;

        const int FitIterations = 10;

        /// <summary>A year's change refits the camera only past these: the distance by 8% or the essentials' box by 40 pixels.</summary>
        const float RefitShare = .08f, RefitPixels = 40, RefitSeconds = .8f;

        /// <summary>
        /// The labels' room in the stack (px at 1080p, times the label scale; SweepLayout.Room in units at the fitted scale):
        /// under a row its industries' names (two lines, 36 px with the plate, and 4 clear; portrait one line), over a named
        /// company its name (18 px and 4 clear; a second tier 28 px higher where two names would meet, nearer than 26 px up or
        /// down; none on a portrait screen, whose height binds), a stratum's name (two lines beside its stream; portrait one
        /// line over its first row), the title (one line, two with the coverage note; portrait two), the clearance under a name
        /// or the title, between two industries' names in a row and two companies' in a tier, and the width a row leaves spare.
        /// </summary>
        const float IndustryNamePx = 40, PortraitIndustryNamePx = 24, CompanyNamePx = 22, CompanyTierPx = 28, CompanyMeetPx = 26, StratumNamePx = 36,
            HeadingPx = 24, TitlePx = 26, TitleNotePx = 40, PortraitTitlePx = 42, LabelGapPx = 6, IndustryClearPx = 10, CompanyClearPx = 8, SlotSparePx = 24;

        /// <summary>Where the labels stand from their points (px at 1080p): an industry's name hangs this far under its cluster
        /// (half its height and 1), a company's this far over its body (its second tier <see cref="CompanyTierPx"/> higher),
        /// a stratum's name this far over its stream (its bottom there), the column's names this far right of their bodies.
        /// Portrait: the name hangs at half its one line's height less 1 and the heading stands at half its height less 1 over
        /// its point: the label system pads every label 4 px, and at 12 and 12 a name under the right of a row met the next
        /// stratum's heading by 1 to 5 px and was moved up onto its own cluster (the port: 2 to 5 a frame).</summary>
        const float IndustryDropPx = 19, PortraitIndustryDropPx = 10, CompanyLiftPx = 9, StratumLiftPx = 18, HeadingLiftPx = 10, ColumnNamePx = 6;

        /// <summary>The label system's padding round each label (px): two names closer than twice it meet.</summary>
        const float LabelPaddingPx = 4;

        /// <summary>The outlines are baked again when the camera settles this far from where they were baked (in units of the
        /// unit sphere's direction: about a degree), this many meshes a frame.</summary>
        const float RebakeAngle = 1f;

        const int RebakePerFrame = 10;

        /// <summary>A rebuild over this many milliseconds warns; the smoke run fails over <see cref="FailMs"/>.</summary>
        const double WarnMs = 20, FailMs = 50;

        static readonly bool SmokeRun = Environment.GetCommandLineArgs().Contains("-smoketest");
        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        /// <summary>The states the smoke test captures (ViewVariants) and <c>-smokeextra sweep</c> checks.</summary>
        static readonly string[] SmokeStates =
            { "sweep:1950", "sweep:1985", "sweep:1995", "sweep:1996", "sweep:2008", "sweep:2025", "sweep:2025:sel=nvidia", "sweep:2025:in=services" };

        /// <summary>The industries whose value added is an estimated split over some years (industries.json's notes): drawn with a
        /// dotted outline and "≈ split estimated". Every industry's 2026 is an estimate too (its valueAddedEstimatedFrom).</summary>
        static readonly (string[] ids, int from, int to)[] EstimatedSplits =
        {
            // BEA's 1947-1962 tables give only total government: state & local is its 1963 ratio to its spending, federal the rest
            (new[] { "federal", "state_local" }, 1947, 1962),
            // the sector totals split by their 1963 shares (detail starts in 1963)
            (new[] { "banking", "finance", "insurance", "other_services", "internet", "media_telecom" }, 1947, 1962),
            // legal services keep their 1963 share of professional, scientific and technical services
            (new[] { "legal", "professional" }, 1947, 1962),
            // software publishers stand inside "publishing incl. software" before 2017: their share is interpolated
            (new[] { "software", "media_telecom" }, 1947, 2016),
            // and 2025 keeps the 2024 share (85.9%): BEA publishes no quarterly detail of the publishing line
            (new[] { "software", "media_telecom" }, 2025, 2025),
        };

        enum Part { Industries, Companies, Streams, Circuit, Owners, Households, Ghosts, Marks }

        static readonly string[] PartNames = { "industries", "companies", "streams", "circuit", "owners", "households", "ghosts", "marks" };

        static HillSweepLayer current;

        /// <summary>The year a state pinned (NaN: the view follows the year chip and the replay).</summary>
        public static float Pinned { get; private set; } = float.NaN;

        /// <summary>The stratum a close-up shows (its tier id: "services", "make", ...), or null.</summary>
        public static string CloseUp { get; private set; }

        /// <summary>The body selected (a company's id, an industry's id or "households"), or null.</summary>
        public static string Selected { get; private set; }

        /// <summary>The body under the pointer, or null.</summary>
        public static string Hovered { get; private set; }

        // ------------------------------------------------------------------ prepared on the worker

        int[] keys;
        EconomyModel economy;
        InstantModel instant;
        Companies model;
        SweepLayout layout;
        double prepareMs, layoutMs;

        /// <summary>A shared shape: its organism (kept for its silhouettes) and its cloud's points, built on the worker.</summary>
        sealed class Shape
        {
            public Organism Body;
            public LineMeshBuilder Cloud;
            public Mesh CloudMesh, Occluder, Outline, Dotted;
            public string Name;
            public bool Outlined;
        }

        Shape[] industryShapes;
        Shape householdsShape;

        /// <summary>The companies' shapes by bucket, variant and whether approximate (greyed).</summary>
        Shape[,,] library;

        // ------------------------------------------------------------------ drawn on the main thread

        GameObject content;
        Material occluderMaterial;
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();

        /// <summary>A body on screen: its renderers (the cloud, its outline and its occluder) and its tween.</summary>
        sealed class Slot
        {
            public string Id;
            public int Company = -1, Industry = -1;
            public GameObject Go;
            public MeshRenderer Cloud, Outline, Occluder;
            public MeshFilter CloudFilter, OutlineFilter, OccluderFilter;
            public Material CloudMaterial, OutlineMaterial;
            public int Bucket = -1, Variant;
            public bool Approx, Dotted;
            public float RefR = 1;
            public Vector3 From, To, Now;
            public float FromR, ToR, NowR, FromA, ToA, NowA, Start, Seconds = TweenSeconds;
            public bool Shown, Moving;
        }

        Slot[] industrySlots = Array.Empty<Slot>();
        Slot householdsSlot;
        readonly Dictionary<int, Slot> companySlots = new Dictionary<int, Slot>();
        readonly List<Slot> moving = new List<Slot>();

        /// <summary>The meshes rebuilt each year: dots and ghosts; the streams; the peaks and the owners' circles; the
        /// hovered or selected body's tail; the circuit's bands; its packets.</summary>
        MeshRenderer looseRenderer, streamsRenderer, marksRenderer, tailRenderer, bandsRenderer, packetsRenderer;

        Material looseMaterial, streamsMaterial, marksMaterial, tailMaterial, bandsMaterial, packetsMaterial;

        /// <summary>Their builders, kept between years (cleared, their capacity kept: a year's streams are some 60,000 vertices).</summary>
        readonly LineMeshBuilder looseBuilder = new LineMeshBuilder(4096), streamsBuilder = new LineMeshBuilder(32768), marksBuilder = new LineMeshBuilder(2048),
            tailBuilder = new LineMeshBuilder(4096), bandsBuilder = new LineMeshBuilder(512);

        readonly PacketMeshBuilder packetsBuilder = new PacketMeshBuilder(512);

        SweepLayout.Frame frame;
        int builtYear = int.MinValue, builtCircuit = int.MinValue;
        bool builtPortrait, dirty = true, labelsShown, cardShown;
        string builtSelected, builtCloseUp, tailFor = "\0", cardFor;
        float clock;
        int pinnedChipYear = int.MinValue;
        double lastRebuildMs, maxRebuildMs;
        int rebuilds;
        string lastRebuild = "";

        readonly List<HillLabel> labels = new List<HillLabel>();

        // the flows of the national accounts, built on a worker ahead of the year shown
        sealed class Flows
        {
            public int Year, FlowYear;
            public double Gdp;
            public readonly double[] Amount = new double[(int)Lane.Count];
            public readonly bool[] Measured = new bool[(int)Lane.Count];
        }

        readonly Dictionary<int, Flows> flows = new Dictionary<int, Flows>();
        readonly HashSet<int> flowsQueued = new HashSet<int>();
        readonly Queue<int> flowsWanted = new Queue<int>();
        readonly object flowsGate = new object();

        /// <summary>Whether the worker is draining the queue (set and cleared under the gate, so a year queued as it finishes is
        /// never left waiting).</summary>
        bool flowsWorking;

        // the circuit's lanes as drawn (for their labels): its path's middle, its flow, its year
        readonly Vector3[] laneMiddle = new Vector3[(int)Lane.Count];
        Flows drawnFlows;

        // the fit (HillPoliticsLayer's): the essentials in the content's frame and the room their names take (px at 1080p)
        readonly List<Vector3> fitAt = new List<Vector3>(512);
        readonly List<Vector4> fitRoom = new List<Vector4>(512);
        readonly List<Rect> hudRects = new List<Rect>(32), obstacles = new List<Rect>(32);
        readonly List<float> lefts = new List<float>(34), rights = new List<float>(34), lows = new List<float>(34), highs = new List<float>(34);
        Vector3 camRight, camUp, camFwd;
        float tanV, tanH, screenW, screenH, poseDistance = 1150;
        int fittedHud, settlingHud, settling;
        Rect fittedBox;
        CameraPose flown;
        Vector3 flownTarget;
        bool flownValid;

        // the stack's fit (FitStack): the scale each orientation was last stacked for (px a unit; 0: none yet), the passes it
        // took, the camera's solved target and distance for the view (a close-up's own), and whether they stand
        readonly float[] stackScale = new float[2];
        int fitPasses;
        Vector3 solvedTarget;
        float solvedDistance = 1150;
        bool solvedValid;

        /// <summary>The labels' widths the stack and the fit keep room for (px at 1080p, before the label scale): each
        /// industry's name, the named companies' (by roster index), the strata's names, the title, the column's names.</summary>
        float[] industryNamePx = Array.Empty<float>(), strataNamePx = Array.Empty<float>();

        readonly Dictionary<int, float> companyNamePx = new Dictionary<int, float>();
        float titlePx, ownersNamePx, householdsNamePx;

        // the outlines' bake: the direction they face, and the screen's shape they were baked for
        Vector3 bakeDirection;
        bool bakedPortrait;
        readonly Queue<Action> rebakes = new Queue<Action>();
        Vector3 lastEye;
        int stillFrames;

        // hover and click
        Vector2 pressed, lastPointer;
        int pickedFor = -1;

        // ------------------------------------------------------------------ life cycle

        public override void Prepare(GraphContext ctx)
        {
            if (!On) return;
            var sw = Stopwatch.StartNew();
            keys = EconomyEmphasis.Keys("sweep", PartNames);
            // drawn alone in the Wave view: every land, wave, hill, activity, social and capture part is absent there
            EconomyEmphasis.RegisterView(View, new[] { "sweep.industries", "sweep.companies" }, new[] { "sweep.streams", "sweep.circuit" },
                new[] { "sweep.owners", "sweep.households", "sweep.ghosts", "sweep.marks" }, synthesis: true);
            economy = ctx.Shared<EconomyModel>(EconomyModel.SharedKey);
            instant = ctx.Shared<InstantModel>(InstantModel.SharedKey);
            if (economy?.Data == null || economy.Data.Industries.Count == 0) return;
            Companies.Provide(economy.Data, ctx.Text(Companies.Path), ctx.Text(Companies.LivesPath));
            model = Companies.Get();
            var lw = Stopwatch.StartNew();
            // each frame stacked first for the labels' room at a guessed scale; FitStack stacks it for its fit before it is drawn
            layout = new SweepLayout(economy.Data, model, RoomFor(GuessScale, false, false, false), RoomFor(PortraitGuessScale, true, false, false));
            layout.SolveAll();
            layoutMs = lw.Elapsed.TotalMilliseconds;
            Shapes();
            // the opening year's accounts now, so the first frame moves
            int open = EconomyState.DefaultYear(EconomyState.MaxYear);
            foreach (int y in new[] { open, Mathf.Min(open + 1, SweepLayout.LastYear) }) BuildFlows(y);
            prepareMs = sw.Elapsed.TotalMilliseconds;
        }

        public override void Upload(GraphContext ctx)
        {
            if (!On) return;
            current = this;
            EconomyStage.Land().Place(transform);
            if (layout == null)
            {
                Debug.LogWarning("[Why] sweep: no economy model; the Wave view draws nothing");
                return;
            }

            foreach (string p in model.Problems) Debug.LogWarning("[Why] sweep: " + p);
            var (major, curatedOnly, measured) = model.Counts();
            Debug.Log($"[Why] sweep: {model.All.Count} companies ({(model.Fallback ? "fallback: company-lives.json, every value approximate" : $"{major} major, {curatedOnly} curated only, {measured} with measured values; as of {model.AsOf}")}), " +
                      $"parsed in {model.ParseMs:0.0} ms; strata top to bottom {string.Join(", ", layout.Strata.Select(s => s.Tier + " " + s.HouseholdShare.ToString("0.000", Ci)))}; " +
                      $"layouts 1947-2026 in {layoutMs:0} ms, prepared in {prepareMs:0} ms");
            if (model.ParseMs > 20) Debug.LogWarning($"[Why] sweep: the roster took {model.ParseMs:0.0} ms to parse (20 ms expected)");
            EnsureContent();
            industrySlots = new Slot[economy.Data.Industries.Count];
            occluderMaterial = PointBodies.OccluderMaterial();
            owned.Add(occluderMaterial);
            bakedPortrait = ScreenLayout.IsPortrait;
            bakeDirection = BakeDirection(bakedPortrait);
            UploadShapes();
            MakeRenderers();
            // every body's renderers now, behind the loading screen: a year's change then only moves, scales and re-dresses them
            for (int i = 0; i < industrySlots.Length; i++) industrySlots[i] = MakeSlot(economy.Data.Industries[i].Id);
            householdsSlot = MakeSlot("households");
            for (int c = 0; c < model.All.Count; c++)
                if (layout.IndustryOf[c] >= 0)
                    companySlots[c] = MakeSlot(model.All[c].Id);
            var states = StateNames();
            ViewVariants.Register(View, () => states.Count, k => ShowState(k >= 0 && k < states.Count ? states[k] : null),
                k => k >= 0 && k < states.Count ? states[k] : k.ToString(Ci));
            if (SmokeRun) SmokeChecks.RegisterSequence(Sequence, Smoke());
        }

        /// <summary>The smoke states and every "show" the walkthroughs' cues give the view, so the director can show them by
        /// name (it matches a cue's string exactly).</summary>
        static List<string> StateNames()
        {
            var names = new List<string>(SmokeStates);
            foreach (string path in new[] { "Data/Walkthrough/economy_tour", "Data/Walkthrough/story_ai_tour", "Data/Walkthrough/story_revolutions_tour" })
            {
                var asset = Resources.Load<TextAsset>(path);
                if (asset == null) continue;
                foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(asset.text, "\"show\"\\s*:\\s*\"(sweep:[^\"]+)\""))
                    if (!names.Contains(m.Groups[1].Value))
                        names.Add(m.Groups[1].Value);
                Resources.UnloadAsset(asset);
            }

            return names;
        }

        void EnsureContent()
        {
            if (content != null) return;
            content = new GameObject("Sweep");
            content.transform.SetParent(transform, false);
            content.transform.localPosition = RootLocal;
            content.transform.localRotation = Quaternion.Euler(0, 180, 0);
            content.SetActive(false);
        }

        /// <summary>The content's origin in the land's frame.</summary>
        static Vector3 RootLocal => new Vector3(RootX, 0, HillLandscape.Z(EconomyState.MaxYear) + RootAhead);

        /// <summary>A point of the content's frame in the land's (labels and the camera's target take the land's).</summary>
        static Vector3 ToLand(Vector3 local)
        {
            Vector3 root = RootLocal;
            return new Vector3(root.x - local.x, local.y, root.z - local.z);
        }

        /// <summary>The catalog's pose for the view before the layer fits it: about where 2025's fit stands at 1920 x 1080 (the
        /// stack's middle, its labels' room in; the fit stands about 1,650 away).</summary>
        public static Vector3 DefaultTarget => ToLand(new Vector3(-760, -380, 0));

        void OnDestroy()
        {
            foreach (var o in owned)
                if (o)
                    Destroy(o);
            owned.Clear();
            if (labelsShown) HillLabels.Clear(this);
            labelsShown = false;
            if (cardShown) HistoryPopups.Clear();
            if (current == this) current = null;
        }

        public override void OnFocus(ViewPreset preset)
        {
            if (!On || layout == null) return;
            if (preset == null || preset.Id != View)
            {
                // history's focus (Order 60) has cleared the portrait cards for every other view, and the view focused may show
                // its own: this view's card is no longer on screen, and it must not clear theirs
                cardShown = false;
                cardFor = null;
                Hovered = null;
                return;
            }

            dirty = true;
            // the bodies open from a third of their size (at once in the smoke test, whose frames must not catch them growing)
            if (!SmokeRun) enterFrom = true;
            FlyToState(SmokeRun ? 0 : 1.4f);
        }

        bool enterFrom;

        public override void Tick(GraphContext ctx, CameraRig rig)
        {
            if (!On || layout == null) return;
            float dt = GraphTime.Delta;
            clock += dt;
            if (LandView.PresetId != View)
            {
                if (content && content.activeSelf) content.SetActive(false);
                ShowLabels(false);
                Hovered = null;
                return;
            }

            if (!content.activeSelf) content.SetActive(true);
            // the road's names step aside here (the view's spec says so on a focus; the scene's first view may open without one)
            var labelSystem = GraphRoot.Instance != null ? GraphRoot.Instance.Labels : null;
            if (labelSystem != null && !labelSystem.DataLabelsHidden) labelSystem.DataLabelsHidden = true;
            CheckPin();
            int year = Year;
            if (Stale(year))
            {
                // following the chip, the land drove the change: one hills layer rebuilds a frame
                bool chipMoved = !dirty && float.IsNaN(Pinned) && year != builtYear;
                if (!chipMoved || HillRebuilds.Take())
                {
                    bool yearChanged = year != builtYear;
                    Rebuild(year);
                    // a new year moves the camera only past the hysteresis; a selection keeps it still
                    if (yearChanged) Refit(chipMoved);
                }
            }
            else if (builtCircuit != CircuitYear(year) && FlowsOf(year) != null)
            {
                Circuit();
                MakeLabels();
                if (labelsShown) HillLabels.Set(this, labels);
            }

            Ease();
            HudRefit();
            Rebake(rig);
            if (rig != null && rig.Cam != null) Pick(rig.Cam, rig);
            Keys();
            ShowLabels(true);
        }

        /// <summary>The year the view shows: the pinned one, else the land's on screen (the chip's while it catches up).</summary>
        public static int Year
        {
            get
            {
                int year = !float.IsNaN(Pinned) ? Mathf.RoundToInt(Pinned) : LandService.ShownYear > 0 ? LandService.ShownYear : EconomyState.Year;
                return Mathf.Clamp(year, SweepLayout.FirstYear, SweepLayout.LastYear);
            }
        }

        /// <summary>Whether the drawing is not of this year, screen, selection and close-up.</summary>
        bool Stale(int year) => dirty || year != builtYear || ScreenLayout.IsPortrait != builtPortrait ||
                                !string.Equals(Selected, builtSelected, StringComparison.Ordinal) || !string.Equals(CloseUp, builtCloseUp, StringComparison.Ordinal);

        /// <summary>A pin gives way to the viewer: once the chip's year moves from where it stood when the state pinned it, the
        /// view follows the chip again.</summary>
        void CheckPin()
        {
            if (float.IsNaN(Pinned) || pinnedChipYear == int.MinValue || EconomyState.Year == pinnedChipYear) return;
            Pinned = float.NaN;
            CloseUp = null;
            pinnedChipYear = int.MinValue;
            dirty = true;
        }

        // ------------------------------------------------------------------ states

        /// <summary>Shows a named state, "sweep:&lt;year&gt;[:in=&lt;stratum&gt;][:sel=&lt;id&gt;]" (the year pinned); null returns
        /// to the year chip with nothing selected or opened.</summary>
        public static void ShowState(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                Selected = null;
                CloseUp = null;
                Pinned = float.NaN;
                if (current != null)
                {
                    current.pinnedChipYear = int.MinValue;
                    current.dirty = true;
                    current.FlyToState(SmokeRun ? 0 : 1.2f);
                }

                return;
            }

            var parts = name.Split(':');
            if (parts.Length < 2 || parts[0] != Sequence || !int.TryParse(parts[1], NumberStyles.Integer, Ci, out int year)) return;
            Pinned = Mathf.Clamp(year, SweepLayout.FirstYear, SweepLayout.LastYear);
            CloseUp = parts.FirstOrDefault(p => p.StartsWith("in=", StringComparison.Ordinal))?.Substring(3);
            Selected = parts.FirstOrDefault(p => p.StartsWith("sel=", StringComparison.Ordinal))?.Substring(4);
            if (current == null) return;
            // a close-up names a stratum by its tier ("services", "make", "tech", "raw", "gov"); any other opens none
            if (CloseUp != null && current.layout != null && current.layout.Strata.All(st => st.Tier != CloseUp)) CloseUp = null;
            current.pinnedChipYear = EconomyState.Year;
            current.dirty = true;
            current.FlyToState(SmokeRun ? 0 : 1.2f);
        }

        /// <summary>Opens a stratum's close-up at the year shown (its tier id), or closes it (null).</summary>
        static void Open(string stratum)
        {
            CloseUp = stratum;
            if (current == null) return;
            current.dirty = true;
            current.FlyToState(SmokeRun ? 0 : 1f);
        }

        /// <summary>Selects a body (its id) or clears the selection (null).</summary>
        static void Select(string id)
        {
            id = string.IsNullOrEmpty(id) ? null : id;
            // a click on empty ground with nothing selected changes nothing: no rebuild
            if (string.Equals(id, Selected, StringComparison.Ordinal)) return;
            Selected = id;
            if (current == null) return;
            current.dirty = true;
            current.ShowCard(Hovered ?? Selected);
        }

        void Keys()
        {
            var kb = Keyboard.current;
            if (kb == null || !kb.escapeKey.wasPressedThisFrame || HudKit.TypingInField()) return;
            if (CloseUp != null) Open(null);
            else if (Selected != null) Select(null);
        }

        // ------------------------------------------------------------------ shapes (the worker) and their meshes (main)

        static double Vol(float r) => Math.Pow(r / ContestNetwork.KR, 3);

        /// <summary>A single-lobe body of one colour at a reference radius (its volume radius exactly that: trueVolume).</summary>
        static Organism Body(string id, float r, Color tint, int detail)
        {
            var parts = new List<ContestNetwork.Part> { new ContestNetwork.Part(ContestNetwork.PartKind.Core, Vector3.zero, r, Vol(r), tint, .7f) };
            var body = new ContestNetwork.Body { Id = id, Name = id, Kind = ContestNetwork.Kind.Company, CustomParts = parts, Alive = true, Value = double.NaN };
            return new Organism(body, null, detail, trueVolume: true);
        }

        static int Hash(string s)
        {
            unchecked
            {
                int h = 17;
                foreach (char c in s ?? "") h = h * 31 + c;
                return h & 0x7fffffff;
            }
        }

        /// <summary>A tier's colour for its bodies and stream: its stone lightened toward the ink; the government's slate.</summary>
        static Color TierColor(string tier) => tier == "gov"
            ? Color.Lerp(Roles.State, Roles.InkText, .35f)
            : Color.Lerp(Roles.Tier(tier == "raw" ? Why.Economy.Land.Tier.Raw : tier == "make" ? Why.Economy.Land.Tier.Make : tier == "services" ? Why.Economy.Land.Tier.Services : Why.Economy.Land.Tier.Tech), Roles.InkText, .35f);

        /// <summary>The 25 industries' bodies, the households' node and the companies' shared shapes, with their clouds'
        /// points (any thread; the meshes, outlines and occluders are made on the main thread).</summary>
        void Shapes()
        {
            var data = economy.Data;
            industryShapes = new Shape[data.Industries.Count];
            for (int i = 0; i < data.Industries.Count; i++)
            {
                var ind = data.Industries[i];
                industryShapes[i] = MakeShape("sweep." + ind.Id, IndustryRef, TierColor(ind.TierId), 3, 3, Part.Industries);
            }

            householdsShape = MakeShape("sweep.households", HouseholdsRef, Roles.People, 3, 2, Part.Households);
            library = new Shape[Buckets.Length, Variants, 2];
            Color grey = Color.Lerp(Roles.Owners, Roles.Missing, ApproxGrey);
            for (int b = 0; b < Buckets.Length; b++)
                for (int v = 0; v < Variants; v++)
                    for (int a = 0; a < 2; a++)
                    {
                        var (r, detail, skin) = Buckets[b];
                        // the same id: the approximate shape is the measured one's, greyed
                        library[b, v, a] = MakeShape("sweep.company." + b + "." + v, r, a == 0 ? Roles.Owners : grey, detail, skin, Part.Companies);
                        library[b, v, a].Outlined = b != SmallBucket;
                    }
        }

        Shape MakeShape(string id, float r, Color tint, int detail, int skin, Part part)
        {
            var body = Body(id, r, tint, detail);
            var cloud = new LineMeshBuilder(body.Points.Length * (1 + skin) + 8);
            PointBodies.Cloud(cloud, body, Id(part), skin, new System.Random(Hash(id)), Roles.InkText);
            return new Shape { Body = body, Cloud = cloud, Name = id, Outlined = true };
        }

        float Id(Part part) => keys != null ? EconomyEmphasis.Id(keys[(int)part]) : GraphIds.None;

        /// <summary>The shapes' meshes (main thread, at load): clouds, occluders and outlines baked from the view's direction.</summary>
        void UploadShapes()
        {
            foreach (var s in industryShapes) UploadShape(s, 2);
            UploadShape(householdsShape, 2);
            for (int b = 0; b < Buckets.Length; b++)
                for (int v = 0; v < Variants; v++)
                    for (int a = 0; a < 2; a++)
                        // the greyed variant shares the measured one's occluder (one shape)
                        UploadShape(library[b, v, a], Mathf.Min(2, Buckets[b].detail), a == 0 ? null : library[b, v, 0]);
        }

        void UploadShape(Shape s, int occluderDetail, Shape occluderOf = null)
        {
            s.CloudMesh = s.Cloud.ToMesh("Sweep cloud " + s.Name);
            owned.Add(s.CloudMesh);
            s.Cloud = null;
            if (occluderOf != null) s.Occluder = occluderOf.Occluder;
            else
            {
                s.Occluder = PointBodies.Occluder(s.Body, occluderDetail, "Sweep occluder " + s.Name);
                owned.Add(s.Occluder);
            }

            if (s.Outlined) BakeOutline(s);
        }

        /// <summary>A shape's two silhouettes (solid and dotted) from the bake direction, in the shape's own frame.</summary>
        void BakeOutline(Shape s)
        {
            var oldSolid = s.Outline;
            var oldDotted = s.Dotted;
            Vector3 eye = bakeDirection * 1e4f;
            float id = Id(s == householdsShape ? Part.Households : Array.IndexOf(industryShapes, s) >= 0 ? Part.Industries : Part.Companies);
            var b = new LineMeshBuilder(256);
            PointBodies.Silhouette(b, s.Body, Vector3.zero, eye, id, 1, Roles.InkText, 1.4f);
            s.Outline = b.ToMesh("Sweep outline " + s.Name);
            b.Clear();
            PointBodies.Silhouette(b, s.Body, Vector3.zero, eye, id, 1, Roles.InkText, 1.4f, 2);
            s.Dotted = b.ToMesh("Sweep dotted outline " + s.Name);
            owned.Add(s.Outline);
            owned.Add(s.Dotted);
            foreach (var slot in AllSlots())
                if (slot.OutlineFilter != null && (slot.OutlineFilter.sharedMesh == oldSolid || slot.OutlineFilter.sharedMesh == oldDotted) && oldSolid != null)
                    slot.OutlineFilter.sharedMesh = slot.Dotted ? s.Dotted : s.Outline;
            if (oldSolid) Forget(oldSolid);
            if (oldDotted) Forget(oldDotted);
        }

        void Forget(UnityEngine.Object o)
        {
            owned.Remove(o);
            Destroy(o);
        }

        IEnumerable<Slot> AllSlots()
        {
            foreach (var s in industrySlots)
                if (s != null)
                    yield return s;
            if (householdsSlot != null) yield return householdsSlot;
            foreach (var s in companySlots.Values) yield return s;
        }

        /// <summary>The direction the outlines face (from a body to the camera, in the content's frame) at the view's pose.</summary>
        static Vector3 BakeDirection(bool portrait)
        {
            float p = (portrait ? PortraitPitch : Pitch) * Mathf.Deg2Rad;
            return -new Vector3(0, -Mathf.Sin(p), Mathf.Cos(p));
        }

        /// <summary>The year's dynamic meshes' renderers and materials (made once; their meshes replaced each year).</summary>
        void MakeRenderers()
        {
            looseRenderer = Renderer("Sweep dots and ghosts", 3192, out looseMaterial);
            streamsRenderer = Renderer("Sweep streams", 3191, out streamsMaterial);
            marksRenderer = Renderer("Sweep peaks and owners", 3189, out marksMaterial);
            tailRenderer = Renderer("Sweep tail", 3191, out tailMaterial);
            bandsRenderer = Renderer("Sweep circuit", 3190, out bandsMaterial);
            packetsMaterial = GraphMaterials.Raw(GraphMaterials.Packets(Color.white, 1, 3196));
            owned.Add(packetsMaterial);
            packetsRenderer = AddMesh("Sweep packets", null, packetsMaterial);
            packetsRenderer.transform.SetParent(content.transform, false);
        }

        MeshRenderer Renderer(string name, int queue, out Material m)
        {
            m = GraphMaterials.Raw(GraphMaterials.Line(Color.white, 1, queue, false));
            owned.Add(m);
            var r = AddMesh(name, null, m);
            r.transform.SetParent(content.transform, false);
            return r;
        }

        /// <summary>Replaces a renderer's mesh with a builder's (destroying the old one); an empty builder leaves none.</summary>
        void SetMesh(MeshRenderer r, LineMeshBuilder b, string name)
        {
            var f = r.GetComponent<MeshFilter>();
            if (f.sharedMesh) Forget(f.sharedMesh);
            f.sharedMesh = null;
            if (b == null || b.VertexCount == 0)
            {
                r.enabled = false;
                return;
            }

            var mesh = b.ToMesh(name);
            owned.Add(mesh);
            f.sharedMesh = mesh;
            r.enabled = true;
        }

        /// <summary>A body's renderers: its cloud with its own material (its alpha), its outline (its own material), its occluder.</summary>
        Slot MakeSlot(string id)
        {
            var go = new GameObject("Sweep body " + id);
            go.transform.SetParent(content.transform, false);
            var slot = new Slot { Id = id, Go = go };
            slot.CloudMaterial = GraphMaterials.Raw(GraphMaterials.Line(Color.white, 1, 3193, false));
            slot.OutlineMaterial = GraphMaterials.Raw(GraphMaterials.Line(Color.white, 1, 3194, false));
            owned.Add(slot.CloudMaterial);
            owned.Add(slot.OutlineMaterial);
            slot.Cloud = Child(go, "cloud", slot.CloudMaterial, out slot.CloudFilter);
            slot.Outline = Child(go, "outline", slot.OutlineMaterial, out slot.OutlineFilter);
            slot.Occluder = Child(go, "occluder", occluderMaterial, out slot.OccluderFilter);
            owned.Add(go);
            go.SetActive(false);
            return slot;
        }

        MeshRenderer Child(GameObject parent, string name, Material m, out MeshFilter filter)
        {
            var r = AddMesh(name, null, m);
            r.transform.SetParent(parent.transform, false);
            filter = r.GetComponent<MeshFilter>();
            return r;
        }

        /// <summary>Gives a slot a shape (by reference: no mesh is built) and whether its outline is dotted.</summary>
        static void Dress(Slot slot, Shape shape, float refR, bool dotted)
        {
            slot.CloudFilter.sharedMesh = shape.CloudMesh;
            slot.OccluderFilter.sharedMesh = shape.Occluder;
            slot.OutlineFilter.sharedMesh = shape.Outlined ? dotted ? shape.Dotted : shape.Outline : null;
            slot.Outline.enabled = shape.Outlined;
            slot.Dotted = dotted;
            slot.RefR = refR;
        }

        // ------------------------------------------------------------------ the tween

        /// <summary>Sends a slot to a place, a radius and an alpha over some seconds from where it stands now (a body that
        /// is not shown starts there, a birth: <see cref="BirthScale"/> of its size and transparent).</summary>
        void Send(Slot s, Vector3 to, float r, float alpha, float seconds, bool birth)
        {
            if (birth || !s.Go.activeSelf)
            {
                s.Now = to;
                s.NowR = r * BirthScale;
                s.NowA = 0;
                s.Go.SetActive(true);
            }

            s.From = s.Now;
            s.FromR = s.NowR;
            s.FromA = s.NowA;
            s.To = to;
            s.ToR = r;
            s.ToA = alpha;
            s.Start = clock;
            s.Seconds = Mathf.Max(seconds, 1e-3f);
            if (!s.Moving)
            {
                s.Moving = true;
                moving.Add(s);
            }

            if (seconds <= 0) Step(s, 1);
        }

        /// <summary>Moves every slot in flight along its tween (one SmoothStep each); a slot faded out is switched off.</summary>
        void Ease()
        {
            for (int k = moving.Count - 1; k >= 0; k--)
            {
                var s = moving[k];
                float t = Mathf.Clamp01((clock - s.Start) / s.Seconds);
                Step(s, t);
                if (t < 1) continue;
                s.Moving = false;
                moving.RemoveAt(k);
                if (!s.Shown && s.NowA <= .001f) s.Go.SetActive(false);
            }
        }

        static void Step(Slot s, float t)
        {
            float e = Mathf.SmoothStep(0, 1, t);
            s.Now = Vector3.Lerp(s.From, s.To, e);
            s.NowR = Mathf.Lerp(s.FromR, s.ToR, e);
            s.NowA = Mathf.Lerp(s.FromA, s.ToA, e);
            var tr = s.Go.transform;
            tr.localPosition = s.Now;
            tr.localScale = Vector3.one * Mathf.Max(1e-4f, s.NowR / s.RefR);
            GraphMaterials.SetAlpha(s.CloudMaterial, s.NowA);
            GraphMaterials.SetAlpha(s.OutlineMaterial, s.NowA);
            s.Cloud.enabled = s.NowA > .002f;
            // a fading body stops hiding what stands behind it once it is mostly gone
            s.Occluder.enabled = s.Shown && s.NowA >= .5f * s.ToA;
        }

        // ------------------------------------------------------------------ the rebuild

        /// <summary>How lit a body of a stratum is: dimmed by the selection (unless it is the one selected) and by a close-up of
        /// another stratum.</summary>
        float Lit(string id, int stratum)
        {
            float a = Selected == null || Selected == id ? 1 : DimSelection;
            if (CloseUp != null && (stratum < 0 || layout.Strata[stratum].Tier != CloseUp)) a *= DimCloseUp;
            return a;
        }

        /// <summary>Whether an industry's value added is an estimated split in a year (or the estimated 2026).</summary>
        bool Estimated(int industry, int year)
        {
            var ind = economy.Data.Industries[industry];
            if (ind.ValueAddedEstimatedFrom > 0 && year >= ind.ValueAddedEstimatedFrom) return true;
            foreach (var (ids, from, to) in EstimatedSplits)
                if (year >= from && year <= to && Array.IndexOf(ids, ind.Id) >= 0)
                    return true;
            return false;
        }

        void Rebuild(int year)
        {
            var sw = Stopwatch.StartNew();
            using (LayerClock.Measure("sweep: year"))
            {
                bool portrait = ScreenLayout.IsPortrait;
                int previous = builtYear;
                bool reopened = dirty && enterFrom;
                frame = layout.Get(year, portrait);
                dirty = false;
                builtYear = year;
                builtPortrait = portrait;
                builtSelected = Selected;
                builtCloseUp = CloseUp;
                if (portrait != bakedPortrait)
                {
                    // the screen turned: the pose's pitch changes with it
                    bakedPortrait = portrait;
                    bakeDirection = BakeDirection(portrait);
                    QueueRebake();
                }

                WantFlowsAround(year);
                // the rows stood with their labels' room at the camera's fitted scale, before any body is sent to its place
                FitStack();
                Industries(year, reopened);
                CompanyBodies(year, previous, reopened);
                enterFrom = false;
                Loose(year);
                Streams(year);
                Marks(year);
                Tail();
                Circuit();
                MakeLabels();
                if (labelsShown) HillLabels.Set(this, labels);
                // the card (the hovered body's, else the selected one's) reads the new year
                cardFor = "\0";
                ShowCard(Hovered ?? Selected);
            }

            lastRebuildMs = sw.Elapsed.TotalMilliseconds;
            maxRebuildMs = Math.Max(maxRebuildMs, lastRebuildMs);
            rebuilds++;
            lastRebuild = $"{year}{(Selected != null ? " sel=" + Selected : "")}{(CloseUp != null ? " in=" + CloseUp : "")}";
            if (SmokeRun) Debug.Log($"[Why] sweep rebuild {lastRebuild}: {lastRebuildMs:0.0} ms");
            if (lastRebuildMs > WarnMs) Debug.LogWarning($"[Why] sweep: rebuilding {lastRebuild} took {lastRebuildMs:0.0} ms (aim: about 10)");
        }

        /// <summary>The 25 industries' bodies at the year: at their places, as large as their share of GDP (none where the
        /// value added is 0), dotted where the year's split is estimated; and the households' node.</summary>
        void Industries(int year, bool reopened)
        {
            var data = economy.Data;
            for (int i = 0; i < data.Industries.Count; i++)
            {
                var slot = industrySlots[i] ??= MakeSlot(data.Industries[i].Id);
                slot.Industry = i;
                bool dotted = Estimated(i, year);
                if (slot.Bucket < 0 || slot.Dotted != dotted)
                {
                    Dress(slot, industryShapes[i], IndustryRef, dotted);
                    slot.Bucket = 0;
                }

                float r = frame.R[i];
                bool shown = r > 0;
                if (!shown && !slot.Go.activeSelf)
                {
                    slot.Shown = false;
                    continue;
                }

                bool birth = shown && !slot.Shown;
                slot.Shown = shown;
                float alpha = shown ? Lit(data.Industries[i].Id, layout.StratumOf[i]) : 0;
                Vector3 to = frame.At[i];
                if (reopened && shown) StartFrom(slot, to, r * EnterScale, alpha);
                Send(slot, to, shown ? r : slot.NowR, alpha, reopened ? EnterSeconds : TweenSeconds, birth && !reopened);
            }

            householdsSlot ??= MakeSlot("households");
            if (householdsSlot.Bucket < 0)
            {
                Dress(householdsSlot, householdsShape, HouseholdsRef, false);
                householdsSlot.Bucket = 0;
            }

            bool hb = !householdsSlot.Shown;
            householdsSlot.Shown = true;
            float ha = Lit("households", -1);
            if (reopened) StartFrom(householdsSlot, frame.Households, SweepLayout.HouseholdsR * EnterScale, ha);
            Send(householdsSlot, frame.Households, SweepLayout.HouseholdsR, ha, reopened ? EnterSeconds : TweenSeconds, hb && !reopened);
        }

        /// <summary>The view's opening: a body starts at its place a third of its size.</summary>
        static void StartFrom(Slot s, Vector3 at, float r, float alpha)
        {
            s.Go.SetActive(true);
            s.Now = at;
            s.NowR = r;
            s.NowA = alpha;
        }

        /// <summary>
        /// The companies' bodies at the year: each drawn company in its shape (by its level, a hashed variant, measured or
        /// approximate), sent from where it stands; one newly drawn grows from almost nothing; one no longer drawn leaves by
        /// its fate when its life ended in between (a bankrupt one falls, an acquired or merged one slides into its successor,
        /// a broken-up one fades in place), else fades.
        /// </summary>
        void CompanyBodies(int year, int previous, bool reopened)
        {
            var seen = new HashSet<int>();
            foreach (var p in frame.Companies)
            {
                var co = model.All[p.Company];
                var level = p.Level;
                if (Selected == co.Id && level != SweepLayout.Level.Full) level = SweepLayout.Level.Full;
                if (level == SweepLayout.Level.Dot) continue;
                seen.Add(p.Company);
                if (!companySlots.TryGetValue(p.Company, out var slot)) companySlots[p.Company] = slot = MakeSlot(co.Id);
                slot.Company = p.Company;
                int bucket = level == SweepLayout.Level.Small ? SmallBucket : p.R >= BigR ? BigBucket : MidBucket;
                int variant = Hash(co.Id) % Variants;
                bool approx = !p.Solid;
                if (slot.Bucket != bucket || slot.Variant != variant || slot.Approx != approx)
                {
                    float oldRef = slot.RefR;
                    Dress(slot, library[bucket, variant, approx ? 1 : 0], Buckets[bucket].r, approx);
                    slot.Bucket = bucket;
                    slot.Variant = variant;
                    slot.Approx = approx;
                    if (slot.Go.activeSelf && Math.Abs(oldRef - slot.RefR) > 1e-3f) slot.Go.transform.localScale = Vector3.one * Mathf.Max(1e-4f, slot.NowR / slot.RefR);
                }

                bool birth = !slot.Shown;
                slot.Shown = true;
                float alpha = (approx ? ApproxAlpha : 1) * Lit(co.Id, layout.StratumOf[layout.IndustryOf[p.Company]]);
                if (reopened) StartFrom(slot, p.At, p.R * EnterScale, alpha);
                Send(slot, p.At, p.R, alpha, reopened ? EnterSeconds : TweenSeconds, birth && !reopened);
            }

            foreach (var kv in companySlots)
            {
                var slot = kv.Value;
                if (seen.Contains(kv.Key) || !slot.Shown) continue;
                slot.Shown = false;
                var co = model.All[kv.Key];
                bool ended = co.Ended != null && previous != int.MinValue && previous <= co.Ended.Value && year > co.Ended.Value;
                if (ended && co.Fate == Model.Companies.Bankrupt) Send(slot, slot.Now + Vector3.down * FallUnits, slot.NowR, 0, FallSeconds, false);
                else if (ended && (co.Fate == Model.Companies.Acquired || co.Fate == Model.Companies.Merged))
                {
                    var successor = model.ById(co.Successor);
                    int k = successor != null ? frame.PlacedOf[successor.Index] : -1;
                    Vector3 into = k >= 0 ? frame.Companies[k].At : frame.At[layout.IndustryOf[kv.Key]];
                    Send(slot, into, slot.NowR * BirthScale, 0, MergeSeconds, false);
                }
                else if (ended && co.Fate == Model.Companies.Breakup) Send(slot, slot.Now, slot.NowR, 0, MergeSeconds, false);
                else Send(slot, slot.Now, slot.NowR * (year < previous ? BirthScale : 1), 0, TweenSeconds, false);
            }
        }

        /// <summary>Where a company stood in its cluster the last time it was drawn at or before a year (relative to its
        /// industry), and its radius; false when never.</summary>
        bool LastPlace(int company, int year, out Vector3 rel, out float r)
        {
            for (int y = year; y >= SweepLayout.FirstYear; y--)
            {
                var f = layout.Get(y, builtPortrait);
                int k = f.PlacedOf[company];
                if (k < 0) continue;
                rel = f.Companies[k].Rel;
                r = Mathf.Max(f.Companies[k].R, SweepLayout.DotMinR);
                return true;
            }

            rel = Vector3.zero;
            r = 0;
            return false;
        }

        /// <summary>The companies whose life ended within <see cref="GhostYears"/> before the year and are not drawn now:
        /// their last place in their cluster (beside their industry where it stands this year) and radius, the largest first.</summary>
        List<(int company, Vector3 at, float r)> Ghosts(int year)
        {
            var list = new List<(int, Vector3, float)>();
            foreach (var co in model.All)
            {
                if (co.Ended == null || year < co.Ended.Value || year > co.Ended.Value + GhostYears || frame.PlacedOf[co.Index] >= 0) continue;
                int industry = layout.IndustryOf[co.Index];
                if (industry < 0 || !LastPlace(co.Index, co.Ended.Value, out var rel, out float r)) continue;
                list.Add((co.Index, rel + frame.At[industry], r));
            }

            list.Sort((a, b) => b.Item3.CompareTo(a.Item3));
            return list;
        }

        /// <summary>The year's dots (the companies too small for a body: twelve points each) and its ghosts (grey hairlines
        /// where companies whose lives just ended last stood). A company that existed with no value draws nothing: its
        /// industry's name and the title count it ("+24 not measured"). The first frames drew it a grey dotted ring, about 100
        /// of them in 1950 and 130 in 1985 beside 3 and 26 bodies: their places and sizes said nothing (no value, a hashed
        /// place), so they were noise that read as objects; the count says exactly what is known.</summary>
        void Loose(int year)
        {
            var b = looseBuilder;
            b.Clear();
            var dirs = Organism.Geodesic(0).dirs;
            float idDots = Id(Part.Companies), idGhosts = Id(Part.Ghosts);
            Color grey = Color.Lerp(Roles.Owners, Roles.Missing, ApproxGrey);
            foreach (var p in frame.Companies)
            {
                var co = model.All[p.Company];
                if (p.Level != SweepLayout.Level.Dot || Selected == co.Id) continue;
                float r = Mathf.Max(p.R, SweepLayout.DotMinR), lit = Lit(co.Id, layout.StratumOf[layout.IndustryOf[p.Company]]);
                Color c = WithAlpha(p.Solid ? Roles.Owners : grey, .9f * (p.Solid ? 1 : ApproxAlpha) * lit);
                foreach (var d in dirs)
                {
                    Vector3 q = p.At + d * r;
                    b.AddSegment(q, q + Vector3.up * PointBodies.DotLength, c, 2.6f, 0, idDots, .93f);
                }
            }

            foreach (var (company, at, r) in Ghosts(year))
            {
                int n = Mathf.Clamp(Mathf.RoundToInt(r * 2.4f), 24, 96);
                Color c = WithAlpha(Roles.InkDim, .55f * Lit(model.All[company].Id, layout.StratumOf[layout.IndustryOf[company]]));
                for (int k = 0; k < n; k++)
                {
                    float a0 = k * 2 * Mathf.PI / n, a1 = (k + 1) * 2 * Mathf.PI / n;
                    b.AddSegment(at + new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0) * r, at + new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0) * r, c, 1, 0, idGhosts, .95f);
                }
            }

            SetMesh(looseRenderer, b, "Sweep dots and ghosts");
        }

        // ------------------------------------------------------------------ time: the slant, the streams, the peaks, the tail

        /// <summary>How far a year Δ back stands: to the screen's left (18 a year for 25 years, then 8), 0.28 of that higher,
        /// and 4 a year deeper.</summary>
        static Vector3 Slant(int delta)
        {
            float left = SlantNear * Mathf.Min(delta, SlantYears) + SlantFar * Mathf.Max(0, delta - SlantYears);
            return new Vector3(-left, SlantRise * left, SlantDeep * delta);
        }

        /// <summary>How much of its alpha a year Δ back keeps: whole at the year shown, a quarter after 25 years, 0.6 of that
        /// after 50 more.</summary>
        static float Age(int delta) =>
            Mathf.Lerp(1, PastAlpha, Mathf.SmoothStep(0, 1, delta / FadeYears)) * Mathf.Lerp(1, DeepFade, Mathf.Clamp01((delta - FadeYears) / DeepFadeYears));

        /// <summary>One ring of dots in the plane facing the camera (half its dots where estimated).</summary>
        static void Ring(LineMeshBuilder b, Vector3 centre, float r, Color c, float id, float phase, bool every)
        {
            int n = Mathf.Clamp(Mathf.RoundToInt(2 * Mathf.PI * r / RingDotStep), RingDotsMin, RingDotsMax);
            for (int i = 0; i < n; i++)
            {
                if (!every && i % 2 == 1) continue;
                float a = (i + phase) * 2 * Mathf.PI / n;
                Vector3 q = centre + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * r;
                b.AddSegment(q, q + Vector3.up * PointBodies.DotLength, c, 2.5f, .3f, id, .93f);
            }
        }

        /// <summary>
        /// Each stratum's stream: a ring a year from 1947 to the year before the one shown, on the slant from the stratum's
        /// departure at the year shown (left of the stack's widest row, at the stratum's height), as wide as its share of that
        /// year's GDP, fading into the past; an estimated year's ring faint and half-dotted. The rings stand on one straight
        /// slant rather than at each year's own departure: a year's rows narrow where its companies' values are missing (before
        /// 1996 most are), and the streams would kink at that seam, a layout's artefact drawn as if it were the economy's.
        /// </summary>
        void Streams(int year)
        {
            int n = layout.Strata.Length;
            var b = streamsBuilder;
            b.Clear();
            float id = Id(Part.Streams);
            for (int s = 0; s < n; s++)
            {
                Color c = TierColor(layout.Strata[s].Tier);
                float lit = CloseUp != null && layout.Strata[s].Tier != CloseUp ? DimCloseUp : Selected != null ? DimSelection : 1;
                for (int t = SweepLayout.FirstYear; t < year; t++)
                {
                    var past = layout.Get(t, builtPortrait);
                    float r = past.StreamR[s];
                    if (r <= 0) continue;
                    bool estimated = economy.Data.IsEstimate(t);
                    float alpha = .8f * Age(year - t) * lit * (estimated ? ApproxAlpha : 1);
                    Vector3 at = frame.Anchor[s] + Slant(year - t);
                    if (UnderHud(at, r)) continue;
                    Ring(b, at, r, WithAlpha(c, alpha), id, t * 7.3f % 1f, !estimated);
                }
            }

            SetMesh(streamsRenderer, b, "Sweep streams");
        }

        /// <summary>Whether a ring (its centre in the content's frame, its radius) would stand under one of the HUD's blocks
        /// at the camera's solved pose: such a ring is not drawn (the streams' far years and a tail run on to the screen's left,
        /// and nothing runs on under a block). False before a fit.</summary>
        bool UnderHud(Vector3 local, float r)
        {
            if (!solvedValid || hudRects.Count == 0) return false;
            Vector3 v = local - (solvedTarget - camFwd * solvedDistance);
            float depth = Mathf.Max(Vector3.Dot(v, camFwd), 1);
            float x = screenW * .5f * (1 + Vector3.Dot(v, camRight) / (depth * tanH)), y = screenH * .5f * (1 + Vector3.Dot(v, camUp) / (depth * tanV));
            float rp = r * screenH * .5f / (depth * tanV);
            foreach (Rect h in hudRects)
                if (x + rp > h.xMin && x - rp < h.xMax && y + rp > h.yMin && y - rp < h.yMax)
                    return true;
            return false;
        }

        /// <summary>An industry's largest measured share of GDP from 1947 to a year, and the year of it: a year whose split is
        /// estimated (the government's before 1963, software's before 2017, 2026) is never a peak drawn as if measured.</summary>
        (double share, int year) PeakShare(int industry, int upTo)
        {
            double best = 0;
            int at = upTo;
            for (int y = SweepLayout.FirstYear; y <= upTo; y++)
            {
                if (Estimated(industry, y)) continue;
                double s = layout.Get(y, builtPortrait).Share[industry];
                if (s <= best) continue;
                best = s;
                at = y;
            }

            return (best, at);
        }

        /// <summary>The industries whose largest share since 1947 is at least <see cref="PeakRatio"/> times their share now,
        /// the largest ratios first, at most <see cref="MaxPeaks"/>: (industry, peak share, its year, the peak's radius).</summary>
        List<(int industry, double share, int year, float r)> Peaks(int year)
        {
            var list = new List<(int, double, int, float, double)>();
            for (int i = 0; i < frame.R.Length; i++)
            {
                double now = frame.Share[i];
                if (now <= 0 || frame.R[i] <= 0) continue;
                var (peak, at) = PeakShare(i, year);
                if (peak < PeakRatio * now) continue;
                list.Add((i, peak, at, SweepLayout.IndustryRadius(peak * layout.ScaleGdp), peak / now));
            }

            list.Sort((a, b) => b.Item5.CompareTo(a.Item5));
            return list.Take(MaxPeaks).Select(x => (x.Item1, x.Item2, x.Item3, x.Item4)).ToList();
        }

        /// <summary>The faint marks: each peak's dotted ring around its industry, and the owners' four dotted circles (faint
        /// and "≈" where the shares are recalled, before the Fed's distributional accounts begin in 1989).</summary>
        void Marks(int year)
        {
            var b = marksBuilder;
            b.Clear();
            Vector3 eye = bakeDirection * 1e4f;
            float id = Id(Part.Marks);
            foreach (var (i, _, _, r) in Peaks(year))
                PointBodies.DottedCircle(b, frame.At[i], r, frame.At[i] + eye, WithAlpha(Roles.InkDim, .35f * Lit(economy.Data.Industries[i].Id, layout.StratumOf[i])), 1.2f, id, 1);
            bool recalled = year < DfaFirstYear;
            float owners = Id(Part.Owners), lit = Lit("owners", -1);
            for (int g = 0; g < frame.Owners.Length; g++)
                PointBodies.DottedCircle(b, frame.Owners[g], frame.OwnerR[g], frame.Owners[g] + eye, WithAlpha(Roles.Owners, (recalled ? .55f : .9f) * lit), recalled ? 1.4f : 1.8f, owners, 1);
            SetMesh(marksRenderer, b, "Sweep peaks and owners");
        }

        /// <summary>The first year the owners' shares come from the Fed's distributional accounts (before: recalled
        /// Saez-Zucman shares, circuit.json groupHistoryNote).</summary>
        const int DfaFirstYear = 1989;

        /// <summary>The body whose tail shows: the hovered one, else the selected one.</summary>
        string TailId => Hovered ?? Selected;

        /// <summary>
        /// The hovered or selected body's own rings back through its years, on the same slant from where it stands now: a
        /// ring a year at 0.3 of that year's radius, dotted and faint where the year's value is approximate, nothing where it
        /// has none (one tail at a time).
        /// </summary>
        void Tail()
        {
            string id = TailId;
            tailFor = id;
            if (id == null || frame == null)
            {
                SetMesh(tailRenderer, null, "Sweep tail");
                return;
            }

            var co = model.ById(id);
            int industry = co == null ? IndexOfIndustry(id) : -1;
            int placed = co != null ? frame.PlacedOf[co.Index] : -1;
            if (co != null ? placed < 0 : industry < 0 || frame.R[industry] <= 0)
            {
                // not drawn this year (or the households, which have no past of their own here)
                SetMesh(tailRenderer, null, "Sweep tail");
                return;
            }

            var b = tailBuilder;
            b.Clear();
            float part = Id(Part.Streams);
            int year = builtYear;
            Vector3 from = co != null ? frame.Companies[placed].At : frame.At[industry];

            Color colour = co != null ? Roles.Owners : TierColor(economy.Data.Industries[industry].TierId);
            for (int t = SweepLayout.FirstYear; t < year; t++)
            {
                var past = layout.Get(t, builtPortrait);
                float r;
                bool solid = true;
                if (co != null)
                {
                    int k = past.PlacedOf[co.Index];
                    if (k < 0) continue;
                    r = past.Companies[k].R;
                    solid = past.Companies[k].Solid;
                }
                else
                {
                    r = past.R[industry];
                    if (r <= 0) continue;
                    solid = !Estimated(industry, t);
                }

                Vector3 at = from + Slant(year - t);
                if (UnderHud(at, .3f * r)) continue;
                Ring(b, at, .3f * r, WithAlpha(colour, .85f * Age(year - t) * (solid ? 1 : ApproxAlpha)), part, t * 7.3f % 1f, solid);
            }

            SetMesh(tailRenderer, b, "Sweep tail");
        }

        int IndexOfIndustry(string id) => economy.Data.IndustryById(id)?.Index ?? -1;

        // ------------------------------------------------------------------ the circuit: the year's national accounts moving

        /// <summary>The year whose accounts the circuit draws for a year shown.</summary>
        static int CircuitYear(int year) => Mathf.Clamp(year, InstantModel.FirstYear, InstantModel.LastYear);

        /// <summary>Asks the worker for the year's accounts and the next three's (the replay's).</summary>
        void WantFlowsAround(int year)
        {
            for (int k = 0; k <= 3; k++) WantFlows(CircuitYear(year + k));
        }

        void WantFlows(int year)
        {
            if (instant == null) return;
            lock (flowsGate)
            {
                if (flows.ContainsKey(year) || !flowsQueued.Add(year)) return;
                flowsWanted.Enqueue(year);
                if (flowsWorking) return;
                flowsWorking = true;
            }

            Task.Run(FlowsWorker);
        }

        void FlowsWorker()
        {
            while (true)
            {
                int year;
                lock (flowsGate)
                {
                    if (flowsWanted.Count == 0)
                    {
                        flowsWorking = false;
                        return;
                    }

                    year = flowsWanted.Dequeue();
                }

                try
                {
                    BuildFlows(year);
                }
                catch (Exception e)
                {
                    lock (flowsGate) flows[year] = null;
                    Debug.LogWarning($"[Why] sweep: the accounts of {year} could not be built ({e.GetBaseException().Message})");
                }
            }
        }

        /// <summary>The 8 lanes' dollars of a year from the instant model (any thread; the model locks its own builds).</summary>
        void BuildFlows(int year)
        {
            if (instant == null) return;
            year = CircuitYear(year);
            var y = instant.Year(year);
            var f = new Flows { Year = year, FlowYear = y.FlowYear, Gdp = economy.Data.Gdp.At(y.FlowYear) };
            var want = y.Flow("spending-want");
            var fear = y.Flow("spending-fear");
            // the total is measured (personal consumption); its split by motive is judged and is not drawn here
            f.Amount[(int)Lane.Spending] = (want?.Value ?? double.NaN) + (fear?.Value ?? double.NaN);
            f.Measured[(int)Lane.Spending] = want != null && fear != null;
            Take(f, y, Lane.Pay, "pay");
            Take(f, y, Lane.Owners, "owners");
            Take(f, y, Lane.TaxesIndustries, "taxes-industries");
            Take(f, y, Lane.StateBuys, "state-buys");
            Take(f, y, Lane.Benefits, "benefits");
            Take(f, y, Lane.TaxesHouseholds, "taxes-households");
            Take(f, y, Lane.PublicPay, "public-pay");
            lock (flowsGate) flows[year] = f;
        }

        static void Take(Flows f, InstantYear y, Lane lane, string id)
        {
            var flow = y.Flow(id);
            f.Amount[(int)lane] = flow?.Value ?? double.NaN;
            // a total the accounts measure (its split across income groups, not drawn here, is modelled)
            f.Measured[(int)lane] = flow?.Basis != null && flow.Basis.StartsWith("measured", StringComparison.Ordinal);
        }

        Flows FlowsOf(int year)
        {
            lock (flowsGate) return flows.TryGetValue(CircuitYear(year), out var f) ? f : null;
        }

        /// <summary>A lane's points: a quadratic arc from a to b bowed out to the screen's right and toward the camera.</summary>
        static Vector3[] Arc(Vector3 a, Vector3 b, float bow)
        {
            Vector3 c = (a + b) * .5f + new Vector3(bow, 0, -BowNear * bow / BowOut);
            var pts = new Vector3[20];
            for (int k = 0; k < pts.Length; k++)
            {
                float t = k / (pts.Length - 1f), u = 1 - t;
                pts[k] = u * u * a + 2 * u * t * c + t * t * b;
            }

            return pts;
        }

        /// <summary>A lane's path in the frame: from where it leaves to where it arrives, bowed out to the screen's right (the
        /// second and third lanes of a pair further), the same whether or not the year's accounts are built.</summary>
        Vector3[] LanePath(Lane lane)
        {
            float StratumY(string tier)
            {
                for (int s = 0; s < layout.Strata.Length; s++)
                    if (layout.Strata[s].Tier == tier)
                        return frame.Anchor[s].y;
                return frame.Households.y;
            }

            Vector3 households = frame.Households - new Vector3(SweepLayout.HouseholdsR, 0, 0);
            Vector3 services = new Vector3(0, StratumY("services"), 0), make = new Vector3(0, frame.Households.y, 0), tech = new Vector3(0, StratumY("tech"), 0);
            Vector3 gov = new Vector3(0, StratumY("gov"), 0), top = new Vector3(0, frame.Rows.Length > 0 ? frame.Rows[0].Base : 0, 0);
            float ownersR = 0;
            Vector3 ownersMid = Vector3.zero;
            for (int g = 0; g < frame.Owners.Length; g++)
            {
                ownersMid += frame.Owners[g];
                ownersR = Mathf.Max(ownersR, frame.OwnerR[g]);
            }

            ownersMid = ownersMid / Mathf.Max(1, frame.Owners.Length) - new Vector3(ownersR, 0, 0);
            return lane switch
            {
                Lane.Spending => Arc(households, services, BowOut),
                Lane.Pay => Arc(make, households, BowOut),
                Lane.Owners => Arc(top, ownersMid, BowOut),
                Lane.TaxesIndustries => Arc(tech, gov, BowOut),
                Lane.StateBuys => Arc(gov, tech, BowOut + BowStep),
                Lane.Benefits => Arc(gov, households, BowOut),
                Lane.TaxesHouseholds => Arc(households, gov, BowOut + BowStep),
                _ => Arc(gov, households, BowOut + 2 * BowStep),
            };
        }

        /// <summary>Every lane's middle in the frame (the fit keeps them on screen, the labels stand at some).</summary>
        void LaneMiddles()
        {
            for (int l = 0; l < (int)Lane.Count; l++)
            {
                var path = LanePath((Lane)l);
                laneMiddle[l] = path[path.Length / 2];
            }
        }

        /// <summary>
        /// The year's money moving (InstantModel: the national accounts of that year, the last year's for 2026, drawn fainter
        /// and labelled with its year): spending from the households into the stack, pay back to them, owners' income up to the
        /// owners' column, the state's taxes from industries and households, its purchases, benefits and pay. Each a band as wide
        /// as 0.22 ∛ of its dollars at 2025's scale, under packets at most 2.5 a second; dotted and "≈" where not measured.
        /// Nothing moves between companies or industries: no such flow is measured by year.
        /// </summary>
        void Circuit()
        {
            int year = builtYear;
            var fl = FlowsOf(year) ?? drawnFlows;
            builtCircuit = fl != null && fl.Year == CircuitYear(year) ? fl.Year : int.MinValue;
            drawnFlows = fl;
            if (fl == null)
            {
                SetMesh(bandsRenderer, null, "Sweep circuit");
                packetsRenderer.enabled = false;
                return;
            }

            var bands = bandsBuilder;
            var packets = packetsBuilder;
            bands.Clear();
            packets.Clear();
            float id = Id(Part.Circuit);
            float faded = fl.FlowYear != fl.Year || fl.Year != CircuitYear(year) ? .6f : 1;
            Color state = Color.Lerp(Roles.State, Roles.InkText, .35f);
            Lay(Lane.Spending, PacketKind.Money, Roles.Commerce, .17f);
            Lay(Lane.Pay, PacketKind.Money, Roles.People, .53f);
            Lay(Lane.Owners, PacketKind.Capital, Roles.Owners, .71f);
            Lay(Lane.TaxesIndustries, PacketKind.Money, state, .29f);
            Lay(Lane.StateBuys, PacketKind.Money, state, .83f);
            Lay(Lane.Benefits, PacketKind.Money, state, .41f);
            Lay(Lane.TaxesHouseholds, PacketKind.Money, state, .07f);
            Lay(Lane.PublicPay, PacketKind.Money, Roles.People, .61f);
            SetMesh(bandsRenderer, bands, "Sweep circuit");
            var mf = packetsRenderer.GetComponent<MeshFilter>();
            if (mf.sharedMesh) Forget(mf.sharedMesh);
            mf.sharedMesh = null;
            packetsRenderer.enabled = packets.VertexCount > 0;
            if (packets.VertexCount > 0)
            {
                var mesh = packets.ToMesh("Sweep packets");
                owned.Add(mesh);
                mf.sharedMesh = mesh;
            }

            void Lay(Lane lane, PacketKind kind, Color colour, float seed)
            {
                double amount = fl.Amount[(int)lane];
                var path = LanePath(lane);
                laneMiddle[(int)lane] = path[path.Length / 2];
                if (double.IsNaN(amount) || amount <= 0 || fl.Gdp <= 0) return;
                double scaled = amount / fl.Gdp * layout.ScaleGdp;
                float w = LaneK * (float)Math.Pow(scaled, 1.0 / 3), lit = (Selected != null || CloseUp != null ? DimSelection : 1) * faded;
                bool measured = fl.Measured[(int)lane];
                // the band: whole where measured, every other piece where not
                for (int k = 0; k < path.Length - 1; k++)
                    if (measured || k % 2 == 0)
                        bands.AddSegment(path[k], path[k + 1], WithAlpha(colour, .22f * lit), 0, w, id, 1);
                float rate = Mathf.Clamp(.8f + .35f * Mathf.Log10((float)Math.Max(scaled, 1) / 100), .5f, 2.5f);
                packets.AddLane(path, kind, WithAlpha(colour, .9f * lit), rate, PacketSpeed, Mathf.Clamp(2.5f + .4f * w, 3, 5), id, 1, 3, seed);
            }
        }

        // ------------------------------------------------------------------ labels

        static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }

        /// <summary>Dollars as labels give them (nominal, $bn in): "$30.8T", "$4.24T", "$95bn", "$2.4bn", "$450M".</summary>
        static string Money(double bn)
        {
            double a = Math.Abs(bn);
            if (a >= 10000) return "$" + (a / 1000).ToString("0.0", Ci) + "T";
            if (a >= 1000) return "$" + (a / 1000).ToString("0.00", Ci) + "T";
            if (a >= 10) return "$" + a.ToString("0", Ci) + "bn";
            if (a >= 1) return "$" + a.ToString("0.0", Ci) + "bn";
            return "$" + (a * 1000).ToString("0", Ci) + "M";
        }

        /// <summary>A share of GDP to one decimal: "13.8%".</summary>
        static string Pct(double share) => (100 * share).ToString("0.0", Ci) + "%";

        static string StratumName(string tier) => tier switch
        {
            "services" => "SERVICES", "make" => "MANUFACTURING & INFRASTRUCTURE", "tech" => "TECH", "raw" => "RAW MATERIALS", "gov" => "GOVERNMENT", _ => tier.ToUpperInvariant(),
        };

        static string FateWords(Companies.Company co, Companies m)
        {
            string who = m.ById(co.Successor)?.Name;
            return co.Fate switch
            {
                Model.Companies.Bankrupt => "bankrupt", Model.Companies.Acquired => who != null ? "acquired by " + who : "acquired",
                Model.Companies.Merged => who != null ? "merged into " + who : "merged", Model.Companies.Breakup => "broken up", _ => "ended",
            };
        }

        /// <summary>A company's value as its label gives it: "$5.49T", "≈ $95bn", "$1.2T (end of 2025)", "≈ $400bn · private valuation".</summary>
        string ValueText(SweepLayout.Placed p)
        {
            var co = model.All[p.Company];
            string v = (p.Solid ? "" : "≈ ") + Money(p.Nominal);
            if (p.Kind == Model.Companies.Kind.Held) v += " (end of " + p.ValueYear.ToString(Ci) + ")";
            if (co.Private) v += " · private valuation";
            return v;
        }

        static Vector2[] Steps(int direction, float from) => Enumerable.Range(1, 4).Select(k => new Vector2(0, direction * (from + 16 * k))).ToArray();

        /// <summary>The shorter names the labels give the industries (the hover card gives industries.json's own).</summary>
        static readonly Dictionary<string, string> ShortNames = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["state_local"] = "State & local gov't", ["transport"] = "Transport", ["trade"] = "Wholesale & retail", ["real_estate"] = "Real estate",
            ["health"] = "Health care", ["professional"] = "Professional services", ["education"] = "Private education", ["entertainment"] = "Arts & entertainment",
            ["hospitality"] = "Restaurants & hotels", ["software"] = "Software & IT", ["internet"] = "Internet & cloud",
        };

        static string ShortName(Why.Economy.Data.Industry ind) => ShortNames.TryGetValue(ind.Id, out string s) ? s : ind.Name;

        /// <summary>The first year a market value is measured (1996: CompaniesMarketCap's year-end history), and how many
        /// companies have a recalled value (both read once from the roster).</summary>
        int firstMeasured = -1, recalledCompanies;

        void Coverage()
        {
            if (firstMeasured >= 0) return;
            firstMeasured = int.MaxValue;
            foreach (var co in model.All)
            {
                bool any = false;
                foreach (var v in co.Values)
                {
                    if (v.Measured) firstMeasured = Math.Min(firstMeasured, v.Year);
                    else any = true;
                }

                if (any) recalledCompanies++;
            }
        }

        /// <summary>What the values are, under the title: measured from the first measured year, recalled before it (and on the
        /// fallback, always); null once they are measured.</summary>
        string CoverageNote(int year)
        {
            Coverage();
            if (model.Fallback) return "Every market value here is recalled (≈ ±30-50%): the measured roster is not built yet";
            return year < firstMeasured ? "Market values measured from " + firstMeasured.ToString(Ci) + "; earlier, " + recalledCompanies.ToString(Ci) + " companies' values are recalled (≈)" : null;
        }

        /// <summary>
        /// The title, with the counts that say what is tracked: "The US economy in 2025 · GDP $30.8T · 25 industries · 162
        /// companies (155 major) · +1 not measured" (every company drawn a major player: "N major companies"; the fallback: "N
        /// companies, approximate"), the coverage note under it in the dim ink while the values are recalled. A portrait screen
        /// gives two lines: the year and GDP, then the counts (or, while the values are recalled, that they are).
        /// </summary>
        string TitleText(bool portrait)
        {
            int year = frame.Year, industries = 0, drawn = frame.Companies.Count, major = 0, unmeasured = frame.NotMeasured.Count;
            foreach (float r in frame.R)
                if (r > 0)
                    industries++;
            foreach (var p in frame.Companies)
                if (model.All[p.Company].Major)
                    major++;
            string who = model.Fallback ? drawn.ToString(Ci) + " companies, approximate"
                : major == drawn ? major.ToString(Ci) + " major companies" : drawn.ToString(Ci) + " companies (" + major.ToString(Ci) + " major)";
            string missing = unmeasured > 0 ? " · +" + unmeasured.ToString(Ci) + " not measured" : "";
            string head = "The US economy in " + year.ToString(Ci) + " · GDP " + Money(frame.Gdp);
            string coverage = CoverageNote(year);
            if (portrait)
            {
                string line = coverage != null && !model.Fallback ? drawn.ToString(Ci) + " companies, values recalled (≈)" + missing
                    : industries.ToString(Ci) + " industries · " + who + missing;
                return "<b>" + head + "</b>\n<size=72%>" + line + "</size>";
            }

            string title = "<b>" + head + " · " + industries.ToString(Ci) + " industries · " + who + missing + "</b>";
            return coverage == null ? title : title + "\n<size=67%><color=#" + ColorUtility.ToHtmlStringRGB(Roles.Encoded(Roles.InkDim)) + ">" + coverage + "</color></size>";
        }

        /// <summary>A stratum's name: landscape two lines ("SERVICES" / "48.3% of GDP · households buy 50% of its sales (2024)";
        /// the government's "the base" and that year's benefits), portrait one ("SERVICES · 48.3% of GDP").</summary>
        string StratumText(int s, bool portrait, bool measuring = false)
        {
            string tier = layout.Strata[s].Tier;
            if (portrait) return "<b>" + StratumName(tier) + "</b> · " + Pct(frame.StratumShare[s]) + " of GDP" + (tier == "gov" ? " · the base" : "");
            string line = tier == "gov" ? "the base" + BenefitsText(measuring) : "households buy " + (100 * layout.Strata[s].HouseholdShare).ToString("0", Ci) + "% of its sales (2024)";
            return "<b>" + StratumName(tier) + "</b>\n<size=80%>" + Pct(frame.StratumShare[s]) + " of GDP · " + line + "</size>";
        }

        /// <summary>" · benefits $4.85T" from the year's accounts (a placeholder of the widest while measuring before they are
        /// built; "≈" where not measured).</summary>
        string BenefitsText(bool measuring)
        {
            var fl = drawnFlows;
            if (fl == null || double.IsNaN(fl.Amount[(int)Lane.Benefits]) || fl.Amount[(int)Lane.Benefits] <= 0) return measuring ? " · benefits $0.00T" : "";
            return " · benefits " + (fl.Measured[(int)Lane.Benefits] ? "" : "≈ ") + Money(fl.Amount[(int)Lane.Benefits]) + FlowYearNote(fl);
        }

        /// <summary>
        /// An industry's name: landscape two short lines, its name and its value added and share of GDP ("Real estate" / "$4.24T
        /// · 13.8%"; "≈" where the split is estimated; "none yet" without value added), and the companies of the roster that
        /// existed that year with no market value ("· +24 not measured"); portrait one line, its name and share. Its company
        /// count is the title's and the hover card's.
        /// </summary>
        string IndustryText(int i, bool portrait)
        {
            var ind = economy.Data.Industries[i];
            bool estimated = Estimated(i, frame.Year);
            if (portrait) return "<b>" + ShortName(ind) + "</b> · " + (frame.R[i] <= 0 ? "none yet" : (estimated ? "≈ " : "") + Pct(frame.Share[i]));
            string value = frame.R[i] <= 0 ? "none yet" : (estimated ? "≈ " : "") + Money(frame.Nominal[i]) + " · " + Pct(frame.Share[i]);
            if (frame.NotMeasuredOf[i] > 0) value += " · +" + frame.NotMeasuredOf[i].ToString(Ci) + " not measured";
            return "<b>" + ShortName(ind) + "</b>\n<size=80%>" + value + "</size>";
        }

        string CompanyText(SweepLayout.Placed p) => "<b>" + model.All[p.Company].Name + "</b> " + ValueText(p);

        /// <summary>The owners' name, right of their circles: their income that year, the top 1%'s share of wealth (≈ where
        /// recalled, before the Fed's distributional accounts in 1989; 1950's held before 1950) and what the circles are.</summary>
        string OwnersText(bool portrait, bool measuring = false)
        {
            var fl = drawnFlows;
            double amount = fl != null ? fl.Amount[(int)Lane.Owners] : double.NaN;
            string income = !double.IsNaN(amount) && amount > 0 ? (fl.Measured[(int)Lane.Owners] ? "" : "≈ ") + Money(amount) + FlowYearNote(fl) : measuring ? "$0.00T" : "";
            int year = frame.Year;
            double share = frame.OwnerShare.Length > 0 ? frame.OwnerShare[0] : double.NaN;
            string top1 = double.IsNaN(share) ? "" : "\ntop 1% hold " + (year < DfaFirstYear ? "≈ " : "") + (100 * share).ToString("0", Ci) + "%" + (year < 1950 ? " (1950)" : "");
            if (portrait) return "<b>Owners' income</b>" + (income.Length > 0 ? "\n" + income : "") + top1;
            return "<b>Owners' income</b>" + (income.Length > 0 ? " " + income : "") + (top1.Length > 0 ? top1 + " of wealth" : "") + "\ncircles: top 1%, next 9%, 40%, 50%";
        }

        /// <summary>The households' name, right of their node: their spending that year and their pay (portrait: spending).</summary>
        string HouseholdsText(bool portrait, bool measuring = false)
        {
            var fl = drawnFlows;
            string Amount(Lane lane)
            {
                double a = fl != null ? fl.Amount[(int)lane] : double.NaN;
                return !double.IsNaN(a) && a > 0 ? (fl.Measured[(int)lane] ? "" : "≈ ") + Money(a) + FlowYearNote(fl) : measuring ? "$0.00T" : null;
            }

            string spending = Amount(Lane.Spending), pay = Amount(Lane.Pay);
            if (portrait) return "<b>Households</b>" + (spending != null ? "\nspend " + spending : "");
            return "<b>Households</b>" + (spending != null ? " · spending " + spending : "") + (pay != null ? "\npay " + pay : "");
        }

        /// <summary>Whether an industry's name stands under its cluster: every one on a landscape screen; on a portrait one the
        /// <see cref="PortraitIndustries"/> largest by their share of the year's GDP.</summary>
        readonly bool[] labelled = new bool[64];

        void ChooseLabelled(bool portrait)
        {
            int n = frame.R.Length;
            for (int i = 0; i < n; i++) labelled[i] = !portrait;
            if (!portrait) return;
            var order = Enumerable.Range(0, n).Where(i => frame.R[i] > 0).OrderByDescending(i => frame.Share[i]).ThenBy(i => i).Take(PortraitIndustries);
            foreach (int i in order) labelled[i] = true;
        }

        /// <summary>
        /// Whether an industry's name stands clear of its row's larger industries' names at its own place (each row's names
        /// taken largest first, by their estimated widths at the camera's solved scale and the label system's padding). One that
        /// meets a larger one's is not drawn: the label system would move it 1.2 em aside or up, and a name moved up stands on
        /// its own cluster (a portrait row, whose slots seldom widen to the names, had one to three a frame).
        /// </summary>
        readonly bool[] unmet = new bool[64];

        void Unmet(bool portrait)
        {
            int n = frame.R.Length;
            for (int i = 0; i < n; i++) unmet[i] = true;
            float perUnit = solvedValid ? Scale(solvedDistance) : stackScale[portrait ? 1 : 0];
            if (perUnit <= 0 || industryNamePx.Length < n) return;
            float scale = ScreenLayout.LabelScale;
            var kept = new List<(float x, float half)>(8);
            foreach (var row in frame.Rows)
            {
                kept.Clear();
                var bySize = (int[])row.Industries.Clone();
                Array.Sort(bySize, (a, b) =>
                {
                    int c = frame.Share[b].CompareTo(frame.Share[a]);
                    return c != 0 ? c : a.CompareTo(b);
                });
                foreach (int i in bySize)
                {
                    if (!labelled[i]) continue;
                    float x = frame.NameX[i], half = (industryNamePx[i] * scale + 2 * LabelPaddingPx) * .5f / perUnit;
                    foreach (var (kx, kh) in kept)
                        if (Mathf.Abs(x - kx) < half + kh)
                        {
                            unmet[i] = false;
                            break;
                        }

                    if (unmet[i]) kept.Add((x, half));
                }
            }
        }

        /// <summary>The companies named: the largest few drawn as bodies, and the selected one; in a close-up, every company of
        /// the stratum from $10bn at 2025's scale (at most 60).</summary>
        List<SweepLayout.Placed> NamedCompanies(bool portrait)
        {
            var chosen = new List<SweepLayout.Placed>();
            int named = 0, most = portrait ? PortraitNamed : Named;
            foreach (var p in frame.Companies)
            {
                if (CloseUp != null)
                {
                    int s = layout.StratumOf[layout.IndustryOf[p.Company]];
                    if (layout.Strata[s].Tier == CloseUp && p.X >= CloseUpNamedX && chosen.Count < CloseUpNamed) chosen.Add(p);
                    continue;
                }

                if (named >= most) break;
                if (p.Level == SweepLayout.Level.Dot) continue;
                named++;
                chosen.Add(p);
            }

            var selected = model.ById(Selected);
            if (selected != null && frame.PlacedOf[selected.Index] >= 0 && chosen.All(p => p.Company != selected.Index)) chosen.Add(frame.Companies[frame.PlacedOf[selected.Index]]);
            return chosen;
        }

        /// <summary>
        /// The view's labels, at most <see cref="WideLabels"/> (portrait or a headset <see cref="NarrowLabels"/>), each at its
        /// thing or culled, never sent into another row: the title (with the counts and the coverage note), the strata's names,
        /// the industries' (under their clusters, in the room each row keeps), the largest companies' (over their bodies, a
        /// second tier where two meet), the fates of the last two years, the owners' and the households' (right of them), the
        /// peak that fell furthest and the years along the top stream. Ranked for placement by role and rank (title 500,
        /// strata 450, industries 400 + share, companies 300 + radius, fates 250, owners and households 220, years 190, peak
        /// 180); the budget drops the least important first (the peak, the fates, the years, the column's names, then the
        /// smallest companies and industries).
        /// </summary>
        void MakeLabels()
        {
            labels.Clear();
            if (frame == null) return;
            int year = builtYear;
            var data = economy.Data;
            bool portrait = frame.Portrait;
            Color plate = Roles.Encoded(Roles.InkBacking);
            Color dim = Roles.Encoded(Roles.InkDim);
            var ranked = new List<(HillLabel label, float keep)>(64);

            ranked.Add((new HillLabel
            {
                Text = TitleText(portrait), At = ToLand(frame.TitleAt), Role = LabelRole.Title, Part = "sweep.industries", Rank = 500, Plate = plate, OnTop = true,
                Fallbacks = Steps(1, 0), OnClick = () => { Open(null); Select(null); },
            }, 1000));

            // the strata's names: landscape beside their streams, portrait over their first rows; no fallbacks (a name stays by
            // its rows)
            for (int s = 0; s < layout.Strata.Length; s++)
            {
                string tier = layout.Strata[s].Tier;
                ranked.Add((new HillLabel
                {
                    Text = StratumText(s, portrait), At = ToLand(frame.NameAt[s]), Role = LabelRole.Name, Part = "sweep.industries", Rank = 450, Plate = plate,
                    Color = Roles.Encoded(Color.Lerp(TierColor(tier), Roles.InkText, .4f)), OnTop = true, Align = TMPro.TextAlignmentOptions.Right,
                    PixelOffset = new Vector2(0, portrait ? HeadingLiftPx : StratumLiftPx), OnClick = () => Open(CloseUp == tier ? null : tier),
                }, 900));
            }

            // the industries' names, under their clusters' middles in the room their rows keep; no fallbacks, and none that
            // meets a larger industry's name in its row (Unmet)
            ChooseLabelled(portrait);
            Unmet(portrait);
            for (int i = 0; i < data.Industries.Count; i++)
            {
                if (!labelled[i] || !unmet[i]) continue;
                var ind = data.Industries[i];
                ranked.Add((new HillLabel
                {
                    Text = IndustryText(i, portrait), At = ToLand(new Vector3(frame.NameX[i], frame.At[i].y - frame.DownOf[i], 0)), Role = LabelRole.Name,
                    Part = "sweep.industries", Rank = 400 + 100 * (float)frame.Share[i], Plate = plate, Color = Roles.Encoded(Color.Lerp(TierColor(ind.TierId), Roles.InkText, .5f)),
                    OnTop = true, PixelOffset = new Vector2(0, -(portrait ? PortraitIndustryDropPx : IndustryDropPx)),
                }, 800 + 100 * (float)frame.Share[i]));
            }

            // the companies' names over their bodies (the second tier where the layout found two meet); no farther
            foreach (var p in NamedCompanies(portrait))
            {
                var co = model.All[p.Company];
                float r = Mathf.Max(p.R, SweepLayout.DotMinR);
                bool selected = co.Id == Selected;
                ranked.Add((new HillLabel
                {
                    Text = CompanyText(p), At = ToLand(p.At + Vector3.up * r), Role = LabelRole.Value, Part = "sweep.companies", Rank = 300 + r + (selected ? 400 : 0),
                    Plate = plate, Color = Roles.Encoded(p.Solid ? Roles.Owners : Color.Lerp(Roles.Owners, Roles.InkDim, .5f)), OnTop = true,
                    PixelOffset = new Vector2(0, CompanyLiftPx), Fallbacks = portrait ? null : new[] { new Vector2(0, CompanyLiftPx + CompanyTierPx) },
                }, selected ? 2000 : 600 + r));
            }

            // the fates of the last two years, under their ghosts (not on a portrait screen: there they would stand over the
            // rows' bodies, and its budget is short)
            int fates = portrait ? NamedFates : 0;
            foreach (var (company, at, r) in Ghosts(year))
            {
                if (fates++ >= NamedFates) break;
                var co = model.All[company];
                ranked.Add((new HillLabel
                {
                    Text = co.Name + " · " + FateWords(co, model) + " " + co.Ended.Value.ToString(Ci), At = ToLand(at + Vector3.down * r), Role = LabelRole.Note,
                    Part = "sweep.ghosts", Rank = 250, Plate = plate, Color = dim, OnTop = true, PixelOffset = new Vector2(0, -10),
                }, 300));
            }

            // the owners and the households, right of their bodies
            float ownersR = 0;
            foreach (float r in frame.OwnerR) ownersR = Mathf.Max(ownersR, r);
            ranked.Add((new HillLabel
            {
                Text = OwnersText(portrait), At = ToLand(new Vector3(SweepLayout.ColumnX + ownersR, OwnersMiddle(), 0)), Role = LabelRole.Note, Part = "sweep.owners",
                Rank = 220, Plate = plate, Color = Roles.Encoded(Roles.Owners), OnTop = true, Align = TMPro.TextAlignmentOptions.Left, PixelOffset = new Vector2(ColumnNamePx, 0),
            }, 500));
            ranked.Add((new HillLabel
            {
                Text = HouseholdsText(portrait), At = ToLand(frame.Households + Vector3.right * SweepLayout.HouseholdsR), Role = LabelRole.Note, Part = "sweep.households",
                Rank = 220, Plate = plate, Color = Roles.Encoded(Color.Lerp(Roles.People, Roles.InkText, .35f)), OnTop = true, Align = TMPro.TextAlignmentOptions.Left,
                PixelOffset = new Vector2(ColumnNamePx, 0),
            }, 500));

            // the peak that fell furthest in points of GDP (2025: manufacturing's 23.4% in 1953; not on a portrait screen)
            foreach (var (i, share, at, r) in Peaks(year).OrderByDescending(x => x.share - frame.Share[x.industry]).Take(portrait ? 0 : NamedPeaks))
                ranked.Add((new HillLabel
                {
                    Text = ShortName(data.Industries[i]) + ": " + Pct(share) + " of GDP in " + at.ToString(Ci), At = ToLand(frame.At[i] + Vector3.up * r), Role = LabelRole.Note,
                    Part = "sweep.marks", Rank = 180, Plate = plate, Color = dim, OnTop = true, PixelOffset = new Vector2(0, 8),
                }, 200));

            // the years along the top stream
            foreach (int decade in Decades)
            {
                if (decade >= year - DecadeClear || decade < SweepLayout.FirstYear) continue;
                float r = layout.Get(decade, builtPortrait).StreamR[0];
                ranked.Add((new HillLabel
                {
                    Text = decade.ToString(Ci), At = ToLand(frame.Anchor[0] + Slant(year - decade) + Vector3.up * r), Role = LabelRole.Note, Part = "sweep.streams", Rank = 190,
                    Plate = plate, Color = dim, OnTop = true, PixelOffset = new Vector2(0, 10), Fallbacks = Steps(1, 10),
                }, 400));
            }

            // the budget: the least important go first
            int budget = portrait || Why.Xr.XrSession.Active ? NarrowLabels : WideLabels;
            if (ranked.Count > budget) ranked = ranked.OrderByDescending(x => x.keep).Take(budget).ToList();
            foreach (var (label, _) in ranked) labels.Add(label);
        }

        /// <summary>The height the owners' name stands at, right of their circles: landscape the middle of the four, top to
        /// bottom; portrait the top circle's top (its three lines at the middle came down beside the top row's last name, and
        /// the port culled it in 16 to 19 of 80 years; at the top, in none).</summary>
        float OwnersMiddle()
        {
            if (frame.Owners.Length == 0) return frame.Top;
            float top = frame.Owners[0].y + frame.OwnerR[0];
            return frame.Portrait ? top : (top + frame.Owners[frame.Owners.Length - 1].y - frame.OwnerR[frame.Owners.Length - 1]) * .5f;
        }

        static string FlowYearNote(Flows fl) => fl.FlowYear != HillSweepLayer.Year ? " (" + fl.FlowYear.ToString(Ci) + ")" : "";

        void ShowLabels(bool show)
        {
            if (show == labelsShown) return;
            labelsShown = show;
            if (show) HillLabels.Set(this, labels);
            else HillLabels.Clear(this);
        }

        // ------------------------------------------------------------------ hover, click and the card

        /// <summary>The body under the pointer (companies before their industries, then the households); a click selects it,
        /// or clears the selection on empty ground.</summary>
        void Pick(Camera cam, CameraRig rig)
        {
            var root = GraphRoot.Instance;
            bool free = PointerInput.Available && !HudKit.PointerOverUi() && (root == null || root.Labels == null || root.Labels.Hovered == null) &&
                        (root == null || !root.TourActive);
            Vector2 pointer = free ? PointerInput.Position : new Vector2(-1, -1);
            int signature = rig.Version * 31 + rebuilds;
            if (!free) Hover(null);
            else if ((pointer - lastPointer).sqrMagnitude > .25f || signature != pickedFor)
            {
                lastPointer = pointer;
                pickedFor = signature;
                Hover(Under(cam, pointer));
            }

            if (!free) return;
            if (PointerInput.PressedThisFrame) pressed = pointer;
            if (PointerInput.ReleasedThisFrame && Vector2.Distance(pressed, pointer) < 5) Select(Hovered == Selected ? null : Hovered);
        }

        string Under(Camera cam, Vector2 pointer)
        {
            if (frame == null || pointer.x < 0) return null;
            float min = 8 * cam.pixelHeight / 1080f, best = float.MaxValue;
            string found = null;
            var tr = content.transform;
            void Try(string id, Vector3 local, float r)
            {
                Vector3 c = cam.WorldToScreenPoint(tr.TransformPoint(local));
                if (c.z <= 0) return;
                float reach = Mathf.Max(min, Vector2.Distance(cam.WorldToScreenPoint(tr.TransformPoint(local + Vector3.right * r)), c));
                float d = Vector2.Distance(pointer, c);
                if (d < reach && d < best)
                {
                    best = d;
                    found = id;
                }
            }

            foreach (var p in frame.Companies) Try(model.All[p.Company].Id, p.At, Mathf.Max(p.R, SweepLayout.DotMinR));
            if (found != null) return found;
            for (int i = 0; i < frame.R.Length; i++)
                if (frame.R[i] > 0)
                    Try(economy.Data.Industries[i].Id, frame.At[i], frame.R[i]);
            if (found != null) return found;
            Try("households", frame.Households, SweepLayout.HouseholdsR);
            return found;
        }

        void Hover(string id)
        {
            if (id == Hovered) return;
            Hovered = id;
            if (TailId != tailFor) Tail();
            ShowCard(Hovered ?? Selected);
        }

        /// <summary>The card of a body (HistoryPopups: its logo from the actors' atlas, else a monogram), or none.</summary>
        void ShowCard(string id)
        {
            if (id == cardFor) return;
            cardFor = id;
            if (id == null || frame == null)
            {
                if (cardShown) HistoryPopups.Clear();
                cardShown = false;
                return;
            }

            var card = Card(id);
            if (card.name == null)
            {
                if (cardShown) HistoryPopups.Clear();
                cardShown = false;
                return;
            }

            HistoryPopups.Show(new List<(string, string, string)> { card });
            cardShown = true;
        }

        /// <summary>A card's image id, name and line: what the body is, its value with its kind and year and share of GDP, its
        /// life, and Jev's judgments in italics with their confidence (never measurements).</summary>
        (string id, string name, string line) Card(string id)
        {
            int year = builtYear;
            var co = model.ById(id);
            if (co != null)
            {
                // three short lines (the card's text runs about 35 characters a line): the value, the life, Jev's judgments
                int k = frame.PlacedOf[co.Index];
                var ind = economy.Data.IndustryById(co.Industry);
                string value;
                if (k >= 0)
                {
                    var p = frame.Companies[k];
                    string kind = p.Kind switch
                    {
                        Model.Companies.Kind.Measured => "",
                        Model.Companies.Kind.Approx => ", recalled ±30-50%",
                        Model.Companies.Kind.Interpolated => ", interpolated",
                        _ => p.Solid ? ", held" : ", recalled, held",
                    };
                    value = (p.Solid ? "" : "≈ ") + Money(p.Nominal) + " (end " + p.ValueYear.ToString(Ci) + kind + ") · " + Pct(p.Nominal / Math.Max(1, frame.Gdp)) + " of GDP" +
                            (co.Private ? " · private valuation" : "");
                }
                else value = "no market value for " + year.ToString(Ci);

                string span = co.Founded != null ? co.Founded.Value.ToString(Ci) + "–" + (co.Ended != null ? co.Ended.Value.ToString(Ci) : "") : "";
                string fate = co.Ended != null ? FateWords(co, model) : "";
                string sells = string.IsNullOrEmpty(co.Sells) ? "" : co.Sells.Length > 44 ? co.Sells.Substring(0, 43).TrimEnd() + "…" : co.Sells;
                string life = string.Join(" · ", new[] { span, fate, sells }.Where(x => x.Length > 0));
                string jev = "";
                if (co.Judged != null)
                {
                    // Jev's (TypeSafe) judgments, never measurements: in italics with their confidence, said to be unsure under 0.5
                    var j = co.Judged;
                    string choice = economy.Data.IndustryById(j.Industry)?.Name ?? j.Industry;
                    bool sure = !float.IsNaN(j.Confidence) && j.Confidence >= .5f;
                    string confidence = float.IsNaN(j.Confidence) ? "" : " J " + j.Confidence.ToString("0.00", Ci);
                    jev = sure ? "\n<i>Jev: " + choice + "</i>" + confidence : "\n<i>Jev was unsure of its industry</i>" + confidence;
                    if (sure && j.Industry != co.Industry) jev += " (drawn in " + (ind?.Name ?? co.Industry) + ")";
                    if (!float.IsNaN(j.Major))
                        jev += " · <i>major " + j.Major.ToString("0.0", Ci) + "/4</i>" +
                               (float.IsNaN(j.MajorConfidence) ? "" : " J " + j.MajorConfidence.ToString("0.00", Ci) + (j.MajorConfidence < .5f ? " (unsure)" : ""));
                }

                return (co.Id, co.Name, "<size=92%><b>" + (ind?.Name ?? co.Industry) + "</b> · " + value + "\n" + life + jev + "</size>");
            }

            int i = IndexOfIndustry(id);
            if (i >= 0)
            {
                var ind = economy.Data.Industries[i];
                int count = frame.Companies.Count(p => layout.IndustryOf[p.Company] == i);
                string value = frame.R[i] <= 0 ? "no value added yet" : (Estimated(i, year) ? "≈ " : "") + Money(frame.Nominal[i]) + " of value added in " + year.ToString(Ci) + " · " +
                    Pct(frame.Share[i]) + " of GDP" + (Estimated(i, year) ? " (split estimated)" : "");
                string unmeasured = frame.NotMeasuredOf[i] > 0 ? " (+" + frame.NotMeasuredOf[i].ToString(Ci) + " that existed, not measured)" : "";
                return ("industry-" + ind.Id, ind.Name, value + "\n" + count.ToString(Ci) + (count == 1 ? " company" : " companies") + " drawn" + unmeasured + " · " +
                                                         StratumName(layout.Strata[layout.StratumOf[i]].Tier).ToLowerInvariant());
            }

            if (id == "households")
            {
                var fl = drawnFlows;
                string spending = fl != null && !double.IsNaN(fl.Amount[(int)Lane.Spending]) ? "spending " + Money(fl.Amount[(int)Lane.Spending]) + " in " + fl.FlowYear.ToString(Ci) : "spending not built";
                return ("households", "Households", spending + " (BEA personal consumption)\na node, not a measure: its size is fixed");
            }

            return (null, null, null);
        }

        // ------------------------------------------------------------------ the stack's fit: the rows stood for their labels' room

        /// <summary>
        /// Stands the year's rows with the room their labels take at the camera's fitted scale, and solves the camera for them
        /// (main thread, in every rebuild, before any body is sent to its place). A label's pixels are fixed and the units they
        /// take depend on the fitted scale, which depends on the stack: it stacks for a scale (the last fit's, or a guess),
        /// fits the essentials (<see cref="Essentials"/>, <see cref="SolveFit"/>) and stacks again for the fitted scale until
        /// the two agree within <see cref="FitTolerance"/>. Then each row's clusters are widened to their names' widths as far as
        /// the free rect gives at that scale (rows only grow across, so the height and the scale stay), and fitted once more,
        /// again if that fit came out smaller. A close-up keeps the view's stack and solves its own camera. Without a camera the
        /// stack stands for the last scale and no fit is kept.
        /// </summary>
        void FitStack()
        {
            bool portrait = builtPortrait;
            int o = portrait ? 1 : 0;
            bool note = !portrait && CoverageNote(builtYear) != null;
            ChooseLabelled(portrait);
            MeasureNames(portrait);
            float k = stackScale[o] > 0 ? stackScale[o] : portrait ? PortraitGuessScale : GuessScale;
            var root = GraphRoot.Instance;
            var cam = root != null && root.Rig != null ? root.Rig.Cam : null;
            if (cam == null)
            {
                layout.Stack(frame, RoomFor(k, portrait, note, true));
                LaneMiddles();
                Essentials();
                solvedValid = false;
                return;
            }

            fittedHud = MeasureHud();
            Axes(cam);
            obstacles.Clear();
            obstacles.AddRange(hudRects);
            Vector3 target = default;
            float d = solvedValid ? solvedDistance : poseDistance, stacked = k;
            Rect free = default;
            bool slots = false;
            fitPasses = 0;
            for (int pass = 0; pass < FitIterations; pass++)
            {
                // the names' slots once the scale has settled (and on the last pass whatever it has done): each row's clusters
                // widened as far as the free rect gives at this scale
                slots |= pass == FitIterations - 1;
                var room = RoomFor(k, portrait, note, slots);
                if (slots) room.MaxRow = MaxRow(free, k, portrait);
                layout.Stack(frame, room);
                stacked = k;
                LaneMiddles();
                Essentials();
                // the free rect found on the first pass serves the passes after it until the slots change the stack's shape
                if (pass == 0 || slots) SolveFit(ref target, ref d, out free);
                else fittedBox = Solve(free, ref target, ref d);
                fitPasses++;
                float fitted = Scale(d);
                bool agree = Mathf.Abs(fitted / k - 1) < FitTolerance;
                k = fitted;
                if (!agree) continue;
                if (slots) break;
                slots = true;
            }

            stackScale[o] = stacked;
            if (CloseUp != null)
            {
                // a close-up fits its stratum's rows at the view's stack; the essentials stay the view's
                var saveAt = new List<Vector3>(fitAt);
                var saveRoom = new List<Vector4>(fitRoom);
                CloseUpEssentials();
                if (fitAt.Count > 0)
                {
                    target = new Vector3((frame.Left + frame.Right) * .5f, (frame.Top + frame.Bottom) * .5f, 0);
                    SolveFit(ref target, ref d, out _);
                }

                fitAt.Clear();
                fitAt.AddRange(saveAt);
                fitRoom.Clear();
                fitRoom.AddRange(saveRoom);
            }

            solvedTarget = target;
            solvedDistance = d;
            solvedValid = true;
        }

        /// <summary>Pixels a unit at the stack's plane for a camera distance (the fit's target stands on it).</summary>
        float Scale(float distance) => screenH * .5f / (Mathf.Max(distance, 1) * tanV);

        /// <summary>The labels' room in units at a scale (px a unit): their pixels at 1080p times the label scale over the
        /// scale (SweepLayout.Room); with the industries' names' slots where asked.</summary>
        SweepLayout.Room RoomFor(float k, bool portrait, bool note, bool slots)
        {
            float u = (layout == null ? portrait ? ScreenLayout.PortraitLabelZoom : 1 : ScreenLayout.LabelScale) / Mathf.Max(k, .05f);
            var room = new SweepLayout.Room
            {
                Below = (portrait ? PortraitIndustryNamePx : IndustryNamePx) * u, Above = portrait ? 0 : CompanyNamePx * u, Tier = portrait ? 0 : CompanyTierPx * u,
                Meet = CompanyMeetPx * u, Reserve = portrait ? 0 : CompanyNamePx * u, Name = (portrait ? HeadingPx : StratumNamePx) * u,
                Title = (portrait ? PortraitTitlePx : note ? TitleNotePx : TitlePx) * u, Gap = LabelGapPx * u, Named = portrait ? PortraitNamed : Named, Headings = portrait,
            };
            if (layout == null || frame == null) return room;
            if (portrait)
            {
                room.Labelled = new bool[frame.R.Length];
                Array.Copy(labelled, room.Labelled, room.Labelled.Length);
            }

            if (slots)
            {
                room.Widths = new float[frame.R.Length];
                for (int i = 0; i < room.Widths.Length && i < industryNamePx.Length; i++)
                    room.Widths[i] = labelled[i] ? (industryNamePx[i] + IndustryClearPx) * u : 0;
            }

            foreach (var kv in companyNamePx) room.CompanyWidths[kv.Key] = (kv.Value + CompanyClearPx) * u;
            return room;
        }

        /// <summary>The widest a row may grow for its names at a scale: the free rect's width less the strata's names (or the
        /// streams' fitted years, whichever reach further left), the column's names and a spare, in units, less the streams'
        /// gap and the column.</summary>
        float MaxRow(Rect free, float k, bool portrait)
        {
            float scale = ScreenLayout.LabelScale, streamR = 0;
            foreach (float r in frame.StreamR) streamR = Mathf.Max(streamR, r);
            float names = 0;
            if (!portrait)
                foreach (float w in strataNamePx)
                    names = Mathf.Max(names, w);
            float left = Mathf.Max(names * scale, (SlantNear * (portrait ? PortraitStreamFitYears : StreamFitYears) + streamR) * k);
            float right = (Mathf.Max(ownersNamePx, householdsNamePx) + ColumnNamePx) * scale;
            return Mathf.Max(0, (free.width - left - right - SlotSparePx * ScreenLayout.UiScale) / Mathf.Max(k, .05f) - SweepLayout.StreamGap -
                                (SweepLayout.ColumnX + SweepLayout.HouseholdsR));
        }

        /// <summary>The labels' widths the stack and the fit keep room for (px at 1080p, estimated from their texts: the label
        /// system measures them only when it places them): each industry's name, the named companies', the strata's names,
        /// the title, the owners' and the households'. A name whose amounts are not built yet takes its widest placeholder.</summary>
        void MeasureNames(bool portrait)
        {
            int ni = frame.R.Length, ns = layout.Strata.Length;
            if (industryNamePx.Length != ni) industryNamePx = new float[ni];
            if (strataNamePx.Length != ns) strataNamePx = new float[ns];
            for (int i = 0; i < ni; i++) industryNamePx[i] = labelled[i] ? TextPx(IndustryText(i, portrait), LabelSystem.RolePx(LabelRole.Name)) : 0;
            for (int s = 0; s < ns; s++) strataNamePx[s] = TextPx(StratumText(s, portrait, true), LabelSystem.RolePx(LabelRole.Name));
            titlePx = TextPx(TitleText(portrait), LabelSystem.RolePx(LabelRole.Title));
            ownersNamePx = TextPx(OwnersText(portrait, true), LabelSystem.RolePx(LabelRole.Note));
            householdsNamePx = TextPx(HouseholdsText(portrait, true), LabelSystem.RolePx(LabelRole.Note));
            companyNamePx.Clear();
            int named = 0, most = portrait ? PortraitNamed : Named;
            foreach (var p in frame.Companies)
            {
                if (named >= most) break;
                if (p.Level == SweepLayout.Level.Dot) continue;
                named++;
                companyNamePx[p.Company] = TextPx(CompanyText(p), LabelSystem.RolePx(LabelRole.Value));
            }
        }

        static readonly System.Text.RegularExpressions.Regex Tags = new System.Text.RegularExpressions.Regex("<[^>]+>");
        static readonly System.Text.RegularExpressions.Regex LineSize = new System.Text.RegularExpressions.Regex(@"^<size=(\d+)%>");

        /// <summary>A label's width estimated from its rich text (px at 1080p, its plate's margins in): the widest line, each
        /// line at its leading size (a line opening with &lt;size=N%&gt;), bold where it has &lt;b&gt;, at about TextMeshPro's
        /// widths for the project's sans (half an em a letter; capitals, digits and narrow marks their own).</summary>
        static float TextPx(string rich, float sizePx)
        {
            float widest = 0;
            foreach (string line in rich.Split('\n'))
            {
                var m = LineSize.Match(line);
                float size = m.Success ? sizePx * int.Parse(m.Groups[1].Value, Ci) / 100f : sizePx, em = 0;
                bool bold = line.IndexOf("<b>", StringComparison.Ordinal) >= 0;
                foreach (char c in Tags.Replace(line, "")) em += CharEm(c);
                widest = Mathf.Max(widest, em * size * (bold ? 1.06f : 1));
            }

            return widest + 2 * .35f * sizePx;
        }

        static float CharEm(char c)
        {
            if (char.IsUpper(c)) return .67f;
            if (char.IsDigit(c)) return .556f;
            if (c == ' ') return .278f;
            if (".,:;·'|!()[]il".IndexOf(c) >= 0) return .3f;
            if ("mwMW".IndexOf(c) >= 0) return .83f;
            if ("$%&≈".IndexOf(c) >= 0) return .62f;
            return .5f;
        }

        /// <summary>
        /// The essentials the camera keeps in the free screen: every row's box with its labels' room (its named companies' names
        /// over it, its industries' names under it), each stratum's name with its width to the left (landscape beside its stream,
        /// portrait over its first row), each stream's last <see cref="StreamFitYears"/> years, the owners' circles and the
        /// households with their names to their right, the circuit's arcs and the title with its width.
        /// </summary>
        void Essentials()
        {
            fitAt.Clear();
            fitRoom.Clear();
            var f = frame;
            var room = f.Stacked;
            foreach (var row in f.Rows)
            {
                float top = row.Top + row.Above, bottom = row.Bottom - row.Below;
                Add(new Vector3(row.Left, top, 0), Vector4.zero);
                Add(new Vector3(row.Right, top, 0), Vector4.zero);
                Add(new Vector3(row.Left, bottom, SweepLayout.CompanyZ), Vector4.zero);
                Add(new Vector3(row.Right, bottom, SweepLayout.CompanyZ), Vector4.zero);
            }

            int fitYears = f.Portrait ? PortraitStreamFitYears : StreamFitYears;
            for (int s = 0; s < layout.Strata.Length; s++)
            {
                Add(f.NameAt[s] + Vector3.up * room.Name, new Vector4(s < strataNamePx.Length ? strataNamePx[s] : 0, 0, 0, 0));
                // the stream's last years (the rest run on to the left)
                for (int t = Mathf.Max(SweepLayout.FirstYear, f.Year - fitYears); t < f.Year; t += 2)
                {
                    float r = layout.Get(t, builtPortrait).StreamR[s];
                    Vector3 c = f.Anchor[s] + Slant(f.Year - t);
                    Add(c + new Vector3(-r, r, 0), Vector4.zero);
                    Add(c + new Vector3(r, -r, 0), Vector4.zero);
                }
            }

            float ownersR = 0;
            for (int g = 0; g < f.Owners.Length; g++)
            {
                Add(f.Owners[g] + new Vector3(f.OwnerR[g], f.OwnerR[g], 0), Vector4.zero);
                Add(f.Owners[g] - new Vector3(f.OwnerR[g], f.OwnerR[g], 0), Vector4.zero);
                ownersR = Mathf.Max(ownersR, f.OwnerR[g]);
            }

            Add(new Vector3(SweepLayout.ColumnX + ownersR, OwnersMiddle(), 0), new Vector4(0, ownersNamePx + ColumnNamePx, 0, 0));
            Add(f.Households + Vector3.right * SweepLayout.HouseholdsR, new Vector4(0, householdsNamePx + ColumnNamePx, 0, 0));
            Add(f.Households + Vector3.down * SweepLayout.HouseholdsR, Vector4.zero);
            Add(f.Households + Vector3.up * SweepLayout.HouseholdsR, Vector4.zero);
            foreach (var m in laneMiddle)
                if (m != Vector3.zero)
                    Add(m, Vector4.zero);
            // the title: its width each side of its middle; its top with the stack's
            Add(f.TitleAt, new Vector4(titlePx * .5f, titlePx * .5f, 0, 0));
            Add(new Vector3(f.TitleAt.x, f.LabelTop, 0), Vector4.zero);

            void Add(Vector3 at, Vector4 r)
            {
                fitAt.Add(at);
                fitRoom.Add(r);
            }
        }

        /// <summary>
        /// Flies to the state shown: stacked and fitted again first (the HUD, the screen or the state may have changed), its
        /// essentials as large as they go in the free screen the HUD's blocks leave (<see cref="LargestFree"/>); a stratum's
        /// close-up fits that stratum's rows.
        /// </summary>
        void FlyToState(float seconds)
        {
            var root = GraphRoot.Instance;
            if (root == null || root.Rig == null || root.Rig.Cam == null || layout == null || LandView.PresetId != View) return;
            EnsureContent();
            dirty = true;
            Rebuild(Year);
            if (solvedValid) Fly(solvedTarget, solvedDistance, seconds);
        }

        /// <summary>Fits the essentials as they stand (fitAt, fitRoom) from a target and a distance (none yet: the stack's
        /// middle and the last distance): first in the whole screen, then in the largest rect the HUD's blocks leave.</summary>
        void SolveFit(ref Vector3 target, ref float d, out Rect free)
        {
            if (target == Vector3.zero) target = new Vector3((frame.Left + frame.Right) * .5f, (frame.Top + frame.Bottom) * .5f, 0);
            float m = ScreenMargin * ScreenLayout.UiScale;
            Solve(Rect.MinMaxRect(m, m, screenW - m, screenH - m), ref target, ref d);
            Measure(target, d, out _, out Vector4 at, out Vector4 room);
            free = LargestFree(Mathf.Max(at.y - at.x, 1), room.x + room.y, Mathf.Max(at.w - at.z, 1), room.z + room.w);
            fittedBox = Solve(free, ref target, ref d);
        }

        /// <summary>A close-up's essentials: the stratum's rows with their labels' room, and its name.</summary>
        void CloseUpEssentials()
        {
            fitAt.Clear();
            fitRoom.Clear();
            for (int s = 0; s < layout.Strata.Length; s++)
            {
                if (layout.Strata[s].Tier != CloseUp) continue;
                foreach (var row in frame.Rows)
                {
                    if (row.Stratum != s) continue;
                    fitAt.Add(new Vector3(row.Left, row.Top + row.Above, 0));
                    fitRoom.Add(Vector4.zero);
                    fitAt.Add(new Vector3(row.Right, row.Bottom - row.Below, 0));
                    fitRoom.Add(Vector4.zero);
                }

                fitAt.Add(frame.NameAt[s] + Vector3.up * frame.Stacked.Name);
                fitRoom.Add(new Vector4(s < strataNamePx.Length ? strataNamePx[s] : 0, 0, 0, 0));
            }
        }

        void Fly(Vector3 target, float d, float seconds)
        {
            var root = GraphRoot.Instance;
            poseDistance = d;
            var p = HillPresets.SweepPose(ToLand(target), Yaw, ScreenLayout.IsPortrait ? PortraitPitch : Pitch, d);
            // the fitted distance stands on a portrait screen too: the preset's portrait width would step it back
            p.PortraitWidth = 0;
            flown = p.Pose();
            flownTarget = target;
            flownValid = true;
            root.Rig.FlyTo(flown, seconds);
        }

        /// <summary>After a year's change: the camera follows only past the hysteresis (the distance 8% off, or the essentials'
        /// box 40 pixels off the free screen's middle at the current pose), so a replay does not sway the frame year by year.</summary>
        void Refit(bool chipMoved)
        {
            var root = GraphRoot.Instance;
            if (root == null || root.Rig == null || root.Rig.Cam == null || !solvedValid) return;
            Vector3 target = solvedTarget;
            float d = solvedDistance;
            if (!flownValid || !chipMoved)
            {
                Fly(target, d, SmokeRun ? 0 : RefitSeconds);
                return;
            }

            float perUnit = screenH * .5f / (Mathf.Max(d, 1) * tanV);
            float moved = (target - flownTarget).magnitude * perUnit;
            if (Mathf.Abs(d / Mathf.Max(poseDistance, 1) - 1) > RefitShare || moved > RefitPixels) Fly(target, d, SmokeRun ? 0 : RefitSeconds);
        }

        /// <summary>The HUD's blocks moved (a panel, the screen turned): once they have held still, the camera fits again.</summary>
        void HudRefit()
        {
            int hud = MeasureHud();
            if (hud == fittedHud) settling = 0;
            else if (hud != settlingHud)
            {
                settlingHud = hud;
                settling = 0;
            }
            else if (++settling >= HudSettleFrames)
            {
                settling = 0;
                FlyToState(SmokeRun ? 0 : .6f);
            }
        }

        void Axes(Camera cam)
        {
            float p = (ScreenLayout.IsPortrait ? PortraitPitch : Pitch) * Mathf.Deg2Rad;
            // the content's frame: x to the screen's right, y up, z into the screen at yaw 180
            camRight = Vector3.right;
            camUp = new Vector3(0, Mathf.Cos(p), Mathf.Sin(p));
            camFwd = new Vector3(0, -Mathf.Sin(p), Mathf.Cos(p));
            screenW = cam.pixelWidth;
            screenH = cam.pixelHeight;
            tanV = Mathf.Tan(cam.fieldOfView * .5f * Mathf.Deg2Rad);
            tanH = tanV * screenW / Mathf.Max(screenH, 1);
        }

        Rect Solve(Rect free, ref Vector3 target, ref float distance)
        {
            for (int pass = 0; pass < FitPasses; pass++)
            {
                Measure(target, distance, out _, out Vector4 at, out Vector4 room);
                float across = (free.width - room.x - room.y) / Mathf.Max(at.y - at.x, 1), up = (free.height - room.z - room.w) / Mathf.Max(at.w - at.z, 1);
                distance = Mathf.Clamp(distance / Mathf.Max(Mathf.Min(across, up), .05f), MinDistance, MaxDistance);
                Measure(target, distance, out Vector4 edge, out _, out _);
                float perUnit = screenH * .5f / (distance * tanV);
                target += camRight * (((edge.x + edge.y) * .5f - free.center.x) / perUnit) + camUp * (((edge.z + edge.w) * .5f - free.center.y) / perUnit);
            }

            Measure(target, distance, out Vector4 box, out _, out _);
            return Rect.MinMaxRect(box.x, box.z, box.y, box.w);
        }

        void Measure(Vector3 target, float distance, out Vector4 edge, out Vector4 at, out Vector4 room)
        {
            Vector3 eye = target - camFwd * distance;
            float scale = ScreenLayout.LabelScale;
            edge = new Vector4(float.MaxValue, float.MinValue, float.MaxValue, float.MinValue);
            at = room = Vector4.zero;
            for (int i = 0; i < fitAt.Count; i++)
            {
                Vector3 v = fitAt[i] - eye;
                float depth = Mathf.Max(Vector3.Dot(v, camFwd), 1);
                float x = screenW * .5f * (1 + Vector3.Dot(v, camRight) / (depth * tanH)), y = screenH * .5f * (1 + Vector3.Dot(v, camUp) / (depth * tanV));
                Vector4 r = fitRoom[i] * scale;
                if (x - r.x < edge.x)
                {
                    edge.x = x - r.x;
                    at.x = x;
                    room.x = r.x;
                }

                if (x + r.y > edge.y)
                {
                    edge.y = x + r.y;
                    at.y = x;
                    room.y = r.y;
                }

                if (y - r.z < edge.z)
                {
                    edge.z = y - r.z;
                    at.z = y;
                    room.z = r.z;
                }

                if (y + r.w > edge.w)
                {
                    edge.w = y + r.w;
                    at.w = y;
                    room.w = r.w;
                }
            }
        }

        Rect LargestFree(float spanX, float roomX, float spanY, float roomY)
        {
            float m = ScreenMargin * ScreenLayout.UiScale, gap = BlockGap * ScreenLayout.UiScale, band = EdgeBand * screenH;
            float bottom = m, top = screenH - m;
            foreach (Rect r in obstacles)
            {
                if (r.yMax > screenH - band) top = Mathf.Min(top, r.yMin - gap);
                if (r.yMin < band) bottom = Mathf.Max(bottom, r.yMax + gap);
            }

            lefts.Clear();
            rights.Clear();
            lows.Clear();
            highs.Clear();
            lefts.Add(m);
            rights.Add(screenW - m);
            lows.Add(bottom);
            highs.Add(top);
            foreach (Rect r in obstacles)
            {
                lefts.Add(r.xMax + gap);
                rights.Add(r.xMin - gap);
                if (r.yMax + gap > bottom) lows.Add(r.yMax + gap);
                if (r.yMin - gap < top) highs.Add(r.yMin - gap);
            }

            Rect best = Rect.MinMaxRect(m, bottom, screenW - m, Mathf.Max(top, bottom + MinFree));
            float bestScale = float.NegativeInfinity, bestArea = 0;
            foreach (float x0 in lefts)
            {
                foreach (float x1 in rights)
                {
                    float w = x1 - x0, acrossScale = (w - roomX) / spanX;
                    if (x0 < m || x1 > screenW - m || w < MinFree || acrossScale < bestScale - Tie) continue;
                    foreach (float y0 in lows)
                    {
                        foreach (float y1 in highs)
                        {
                            float h = y1 - y0, scale = Mathf.Min(acrossScale, (h - roomY) / spanY), area = w * h;
                            if (h < MinFree || scale < bestScale - Tie || (scale < bestScale + Tie && area <= bestArea) || Blocked(x0, y0, x1, y1)) continue;
                            best = Rect.MinMaxRect(x0, y0, x1, y1);
                            bestScale = scale;
                            bestArea = area;
                        }
                    }
                }
            }

            return best;
        }

        bool Blocked(float x0, float y0, float x1, float y1)
        {
            foreach (Rect r in obstacles)
                if (r.xMin < x1 && r.xMax > x0 && r.yMin < y1 && r.yMax > y0)
                    return true;
            return false;
        }

        int MeasureHud()
        {
            hudRects.Clear();
            var root = GraphRoot.Instance;
            if (root != null && root.Labels != null) root.Labels.HudRects?.Invoke(hudRects);
            int signature = ScreenLayout.Version;
            unchecked
            {
                foreach (Rect r in hudRects)
                    signature = (((signature * 31 + Mathf.RoundToInt(r.xMin / 8)) * 31 + Mathf.RoundToInt(r.yMin / 8)) * 31 + Mathf.RoundToInt(r.xMax / 8)) * 31 +
                                Mathf.RoundToInt(r.yMax / 8);
            }

            return signature;
        }

        // ------------------------------------------------------------------ the outlines' bake

        /// <summary>Once the camera has settled more than a degree from where the outlines were baked (an orbit, a portrait
        /// screen), bakes them again from there, <see cref="RebakePerFrame"/> meshes a frame (on this thread: the silhouette
        /// keeps static buffers).</summary>
        void Rebake(CameraRig rig)
        {
            for (int k = 0; k < RebakePerFrame && rebakes.Count > 0; k++) rebakes.Dequeue()();
            if (rig == null || rig.Cam == null || frame == null || rebakes.Count > 0) return;
            Vector3 eye = content.transform.InverseTransformPoint(rig.Cam.transform.position);
            stillFrames = (eye - lastEye).sqrMagnitude < .01f && !rig.Flying ? stillFrames + 1 : 0;
            lastEye = eye;
            if (stillFrames != 3) return;
            Vector3 centre = new Vector3((frame.Left + frame.Right) * .5f, (frame.Top + frame.Bottom) * .5f, 0);
            Vector3 dir = (eye - centre).normalized;
            if (Vector3.Angle(dir, bakeDirection) < RebakeAngle) return;
            bakeDirection = dir;
            QueueRebake();
        }

        void QueueRebake()
        {
            rebakes.Clear();
            if (industryShapes == null) return;
            foreach (var s in industryShapes) rebakes.Enqueue(() => BakeOutline(s));
            rebakes.Enqueue(() => BakeOutline(householdsShape));
            for (int b = 0; b < Buckets.Length; b++)
                for (int v = 0; v < Variants; v++)
                    for (int a = 0; a < 2; a++)
                    {
                        var s = library[b, v, a];
                        if (s.Outlined) rebakes.Enqueue(() => BakeOutline(s));
                    }
        }

        // ------------------------------------------------------------------ smoke

        /// <summary>
        /// <c>-smokeextra sweep</c>: the smoke states each captured with their counts, rebuild time and fit logged, failing a
        /// state that draws no industry or no company body, an industry with value added and no body, two bodies overlapping by
        /// more than half a unit, essentials off the screen, a body drawn as measured without a measured value that year, or a
        /// rebuild over 50 ms; the never-still check at 2025; then a replay pass 1995-2005 at 0.35 s a year, each rebuild timed.
        /// </summary>
        IEnumerator Smoke()
        {
            var root = GraphRoot.Instance;
            if (root == null) yield break;
            root.Focus(View, 0f);
            yield return new WaitForSecondsRealtime(1f);
            if (layout == null)
            {
                Debug.LogError("[Why] sweep: no layout (no economy model)");
                yield break;
            }

            maxRebuildMs = 0;
            foreach (string state in SmokeStates)
            {
                ShowState(state);
                yield return new WaitForSecondsRealtime(1.6f);
                Check(state);
                yield return SmokeChecks.Capture("economy-" + state.Replace(':', '-').Replace('=', '-'), root.CurrentPreset);
                if (state == "sweep:2025") yield return SmokeChecks.NeverStill("economy-sweep-2025");
            }

            for (int y = 1995; y <= 2005; y++)
            {
                ShowState("sweep:" + y.ToString(Ci));
                yield return new WaitForSecondsRealtime(.35f);
                Debug.Log($"[Why] sweep replay {y}: rebuilt in {lastRebuildMs:0.0} ms");
                if (lastRebuildMs > FailMs) Debug.LogError($"[Why] sweep: rebuilding {y} took {lastRebuildMs:0.0} ms (over {FailMs:0} ms)");
            }

            Debug.Log($"[Why] sweep: the slowest rebuild of the sequence took {maxRebuildMs:0.0} ms");
            ShowState(null);
        }

        /// <summary>One state's counts and checks.</summary>
        void Check(string state)
        {
            int industries = 0, missing = 0, companies = 0, dots = 0;
            for (int i = 0; i < frame.R.Length; i++)
            {
                if (frame.R[i] <= 0) continue;
                var slot = industrySlots[i];
                if (slot != null && slot.Shown && slot.Go.activeSelf) industries++;
                else missing++;
            }

            foreach (var s in companySlots.Values)
                if (s.Shown && s.Go.activeSelf)
                    companies++;
            foreach (var p in frame.Companies)
                if (p.Level == SweepLayout.Level.Dot)
                    dots++;
            float overlap = layout.WorstOverlap(frame, out string pair);
            int dishonest = 0;
            string example = "";
            foreach (var p in frame.Companies)
            {
                if (p.Kind != Model.Companies.Kind.Measured || Model.Companies.MeasuredIn(model.All[p.Company], frame.Year)) continue;
                dishonest++;
                example = model.All[p.Company].Id;
            }

            // the stack against the camera: the rows' room was stood for the scale the fit solved; the camera flies to it (or,
            // within the refit's hysteresis, stands near it)
            float flownScale = Scale(poseDistance), stacked = stackScale[builtPortrait ? 1 : 0], largest = 0;
            for (int i = 0; i < frame.R.Length; i++) largest = Mathf.Max(largest, 2 * frame.R[i] * flownScale);
            var gaps = new List<string>();
            for (int k = 1; k < frame.Rows.Length; k++)
            {
                var a = frame.Rows[k - 1];
                var b = frame.Rows[k];
                gaps.Add((a.Stratum == b.Stratum ? "w " : "b ") + ((a.Bottom - b.Top) * flownScale).ToString("0", Ci));
            }

            int budget = builtPortrait || Why.Xr.XrSession.Active ? NarrowLabels : WideLabels;
            Debug.Log($"[Why] sweep {state}: {industries} industry bodies, {companies} company bodies, {dots} dots, {frame.NotMeasured.Count} not measured " +
                      $"(counted, not drawn), {labels.Count} labels (at most {budget}); rebuilt in {lastRebuildMs:0.0} ms; worst overlap {overlap:0.00} ({pair}), " +
                      $"{frame.Forced} forced; fit {fittedBox.xMin:0}-{fittedBox.xMax:0} x {fittedBox.yMin:0}-{fittedBox.yMax:0} px of {screenW:0}x{screenH:0}, " +
                      $"distance {poseDistance:0} of at most {MaxDistance:0} ({100 * poseDistance / MaxDistance:0}%), {flownScale:0.000} px a unit, stacked for " +
                      $"{stacked:0.000} in {fitPasses} passes; {frame.Rows.Length} rows, gaps (px; w within a stratum, b between) {string.Join(" ", gaps)}; " +
                      $"largest industry {largest:0} px across");
            if (industries == 0) Debug.LogError($"[Why] sweep: {state} draws no industry bodies");
            if (companies == 0) Debug.LogError($"[Why] sweep: {state} draws no company bodies");
            if (missing > 0) Debug.LogError($"[Why] sweep: {state}: {missing} industries with value added have no body");
            if (overlap > .5f) Debug.LogError($"[Why] sweep: {state}: bodies overlap by {overlap:0.00} units ({pair})");
            if (fittedBox.xMin < -1 || fittedBox.yMin < -1 || fittedBox.xMax > screenW + 1 || fittedBox.yMax > screenH + 1)
                Debug.LogError($"[Why] sweep: {state}'s essentials run off the screen ({fittedBox})");
            if (dishonest > 0) Debug.LogError($"[Why] sweep: {state}: {dishonest} bodies drawn as measured without a measured value that year (e.g. {example})");
            if (poseDistance >= MaxDistance - 1) Debug.LogError($"[Why] sweep: {state}'s fit ran to its distance limit ({MaxDistance:0}): the essentials do not fit the screen");
            else if (poseDistance > FitDistanceWarn * MaxDistance) Debug.LogWarning($"[Why] sweep: {state}'s fit stands at {poseDistance:0}, near its limit of {MaxDistance:0}");
            // (a close-up's camera stands nearer than the view's stack was fitted for: its labels have more room than they need)
            if (CloseUp == null && stacked > 0 && Mathf.Abs(flownScale / stacked - 1) > RefitShare + FitTolerance)
                Debug.LogError($"[Why] sweep: {state}: the rows' labels' room was stood for {stacked:0.000} px a unit and the camera stands at {flownScale:0.000}");
            if (labels.Count > budget) Debug.LogError($"[Why] sweep: {state}: {labels.Count} labels, over the budget of {budget}");
            if (builtPortrait && largest < 25) Debug.LogError($"[Why] sweep: {state}: the largest industry body is {largest:0} px across on a portrait screen (25 expected)");
            if (lastRebuildMs > FailMs) Debug.LogError($"[Why] sweep: {state} took {lastRebuildMs:0.0} ms to rebuild (over {FailMs:0} ms)");
        }
    }
}
