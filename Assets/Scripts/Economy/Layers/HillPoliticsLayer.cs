using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Why.Economy.Draw;
using Why.Economy.Emphasis;
using Why.Economy.Land;
using Why.Economy.Model;
using Why.Economy.UI;
using Roles = Why.Economy.EconomyStyle.Roles;

namespace Why.Economy.Layers
{
    /// <summary>
    /// Who holds power (Docs/hills-design/POLITICS.md; the user's seventh direction, 7 Nov 2026, verbatim in
    /// Docs/research/advantage/brief-v5.md): the government of the United States since 1945 in the history view's point-cloud
    /// language, the shown year a cross-section and the past receding behind it. The camera looks straight from the future and a
    /// little from above, so the cross-section faces it square and both parties draw at one scale; the past is set back along
    /// a slant (<see cref="Shear"/>), so the decades run back to the screen's left and up, fading, and the parties' rise and
    /// fall reads along them. The camera is fitted to the shell, the parties and the trade partners in the screen the HUD's
    /// blocks, the panel and the portrait cards leave free.
    /// <list type="bullet">
    /// <item><b>The parties</b> are streams of dot rings through time, the Democrats on the screen's left (the political left
    /// takes +x, which a camera from the future sees on its left) and the Republicans on its right, each as high as its power
    /// by the stated rule (<see cref="Politics.PowerRule"/>: the presidency and each chamber's share of seats) and as wide as
    /// the votes it won in the last national election, faint and thin before the first one measured. A change of power eases
    /// over a tenth of a year, so it reads as a step rather than a cliff.</item>
    /// <item><b>The presidency</b> is a thin track through time between them at the executive's height, term by term in the
    /// president's party colour. <b>Key moments</b> (laws, regulations, trade actions, monetary turns, treaties and
    /// conflicts) are sparks on it at their dates, the recent ones with a strand straight down to the floor of no power: the
    /// topology the economy adapts to, the government's doing rather than a party's. The latest few are named beside the
    /// president.</item>
    /// <item><b>The cross-section</b> at the shown year: the president at the top centre and the offices cascading below by
    /// rank (the vice president; the Speaker and the Senate's leader; State, the Treasury and Defense; the Chairman of the
    /// Joint Chiefs standing on the army's shell), each a body in its holder's party colour (an appointee whose party the
    /// research takes from the appointing president in the administration's colour, and named as serving in it, never as a
    /// member; the military in the state's slate), and the parties' heads at their streams' ends outside the cascade, sized
    /// by their votes, with lobes for their House and Senate seats.</item>
    /// <item><b>The standing army</b> rings the country as a shell of dots as thick as its share of the world's military
    /// spending, NATO's share a fainter layer outside it, walled by Jev's judgments of its sources of advantage (command of
    /// the sea, nuclear weapons, alliances, spending); the rival powers stand beyond it on the right as shells of their
    /// own.</item>
    /// <item><b>Trade</b> runs from the country's edge through the shell under the sea-control wall to the partners on the
    /// lower left: goods out and goods in as two slim lanes of packets either side of the path, as wide as their dollars, the
    /// money paid for the deficit as a thin lane between them, and the deficit itself the hatched gap where the lanes cross
    /// the shell. Before partners are measured, one lane runs to the world.</item>
    /// </list>
    /// What moves at a frozen year (the goods and money of trade, the people along the streams, the chain of command) is a
    /// packet lane on the flow clock (<see cref="PacketMeshBuilder"/>, <see cref="FlowClock"/>), built once per rebuild, each
    /// lane at most four packets a second. Every number on screen comes from <see cref="Politics"/>, with the year it was
    /// measured; a series past its gaps draws faint and is labelled "not measured". The view follows the year chip (the
    /// middle of its year, where the data's annual measures stand) unless a tour's cue or the panel pins a year:
    /// "politics:us:&lt;year&gt;[:sel=&lt;id&gt;]".
    /// </summary>
    [GraphScenes(EconomyLayouts.HillsScene)]
    public sealed class HillPoliticsLayer : GraphLayer
    {
        public const string View = "politics", Sequence = "politics";

        /// <summary>The country the states name ("politics:us:1965"): the data holds the United States alone.</summary>
        const string Country = "us";

        /// <summary>Above history's 60: OnFocus runs in this order, and history's clears the portrait cards in every view but
        /// its own, so this view shows its cards after.</summary>
        public override int Order => 67;

        public override IEnumerable<string> RequiredTexts => new[] { Politics.Path };

        enum Part { Streams, Marks, Bodies, Army, Walls, Lanes, Outlines }

        static readonly string[] PartNames = { "streams", "marks", "bodies", "army", "walls", "lanes", "outlines" };
        static readonly int[] Keys = EconomyEmphasis.Keys(View, PartNames);

        static float Id(Part part) => EconomyEmphasis.Id(Keys[(int)part]);

        // ------------------------------------------------------------------ the frame (land-local, at the shown year's z)

        /// <summary>The cross-section's middle across the land (history's lanes stand over it too) and the floor of no power,
        /// which the moments' strands reach down to.</summary>
        const float X0 = 130, Floor = 60;

        /// <summary>The power axis: a party holding all the power the rule counts stands this high above the floor.</summary>
        const float PowerHeight = 60;

        /// <summary>How far each party's stream stands from the middle: outside the cascade's widest line (State, the Treasury
        /// and Defense 24 units either side, their names below them) and the Speaker's and the Senate leader's names, so the
        /// heads and their names, which stand outside them, keep clear of the officers' names.</summary>
        const float PartyX = 50;

        /// <summary>Votes per unit of a stream's radius: 80 million votes draw a ring 14 units across.</summary>
        const float VotesPerUnit = 80e6f / 7;

        /// <summary>A stream's radius before an election is measured: a thread, with no width implied.</summary>
        const float ThreadR = .45f;

        /// <summary>A change of power or votes eases over this many years (a step, not a cliff); the value just before a change
        /// is read this long before it.</summary>
        const float Ease = .1f, Before = .001f;

        /// <summary>
        /// The view's time axis: a year back from the shown year stands this many times as deep as on the land
        /// (<see cref="HillLandscape.Z"/>, about two units a year), the shown year's cross-section staying at its own plane.
        /// At the three-quarter pose the land's own depth set 2025's 80 years in about 200 pixels, a four-year term in 9 or
        /// 10, the decade marks atop one another and the past inside the shell; half as deep again, a term spans 13 to 22
        /// pixels over the years shown and the oldest decades stand clear of the shell, while the fitted cross-section keeps
        /// 97% of its size or more (twice as deep cost 2025 15% of it).
        /// </summary>
        const float TimeScale = 1.5f;

        /// <summary>A stream's ring every this many units of the view's time axis (every two thirds of a year, three pixels
        /// apart across the screen at the cross-section and two in the distance), dense enough that the rings read as a
        /// tube.</summary>
        const float RingEvery = 2;

        /// <summary>The army's shell: its centre's height, its inner radius (outside the parties' heads with their seat lobes
        /// at the most power a party has held, 84%), its thickness for the whole of the world's military spending (a share of
        /// 37% draws 9.6 units), and the thickness of a share not measured (two faint circles).</summary>
        const float ShellY = Floor + 24, ShellR = 74, ShareDepth = 26, Unmeasured = 1.2f;

        /// <summary>The shell's circles: one every this many units out, a dot every this many units around.</summary>
        const float RingStep = 1.2f, DotStep = 1.6f;

        /// <summary>The president's height (above the most power a party can hold, under the shell's top) and the size of the
        /// president's body and of each office's.</summary>
        const float ExecY = Floor + PowerHeight + 18, ExecR = 4.6f, OfficeR = 2.3f;

        /// <summary>The cascade's lines below the president, by rank: the vice president; the Speaker and the Senate's leader;
        /// State, the Treasury and Defense (War and the Navy before 1947); the Chairman of the Joint Chiefs on the shell.</summary>
        static readonly int[][] CascadeLines = { new[] { 1 }, new[] { 2, 3 }, new[] { 4, 5, 6 }, new[] { 7 } };

        /// <summary>How far apart the offices of a line stand (their names alternate above and below), and four on a line.</summary>
        const float OfficeGap = 24, CrowdedGap = 20;

        /// <summary>A seat lobe's radius for a whole chamber: a share draws the cube root of it times this, so the lobes'
        /// volumes are the shares.</summary>
        const float SeatLobe = 5.2f;

        /// <summary>The rivals' shells beyond the US's, on the screen's right: their screen angles (degrees counter-clockwise
        /// from the screen's right), how far out from the middle (beyond the Republicans' name, which stands outside their head
        /// between the two), and the empty core each stands around.</summary>
        const float RivalUpper = 24, RivalLower = -24, RivalD = ShellR + 58, RivalCore = 5;

        /// <summary>Partner nodes: how far out from the middle (beyond the tallest walls and the walls' names, which stand
        /// between them), their size, and how far inside the shell's inner edge their lanes start: at the country's edge, so
        /// the lanes cross the shell without crossing the cross-section's middle.</summary>
        const float NodeR = ShellR + 48, NodeSize = 2.4f, LaneInside = 8;

        /// <summary>The partners in the research (Politics.Partners' keys), the names labels give them, and their screen angles:
        /// on the lower left, away from the rivals, below the Democrats' name and the past receding above it, and no lower
        /// than the shell's reach so they stay in the screen the camera fits it to; in a fixed order so a node stays put across
        /// years.</summary>
        static readonly (string id, string name, float angle)[] PartnerSeats =
        {
            ("canada", "Canada", 192), ("eu", "European Union", 200), ("germany", "Germany", 208), ("japan", "Japan", 216),
            ("mexico", "Mexico", 224), ("china", "China", 232),
        };

        /// <summary>The screen angle of the lane to the world, drawn before partners are measured: the partners' middle.</summary>
        const float WorldAngle = 212;

        /// <summary>Where the shell's labels stand (screen angles): the forces' low on the right, clear of the cascade's names
        /// and the trade lanes; NATO's high on the right, clear of the president's name and the rivals'.</summary>
        const float ShellLabelAngle = 315, NatoLabelAngle = 40;

        /// <summary>Path units a second every packet of the view travels.</summary>
        const float PacketSpeed = 9;

        /// <summary>Packets a second, each lane at most four (faster lanes strobe at 30 frames a second, each packet moving rate /
        /// fps of a gap a frame): a flow's size is its lane's width, never its rate.</summary>
        const float GoodsRate = 1.5f, MoneyRate = .8f, PeopleRate = 1.2f, PromiseRate = 1.6f;

        /// <summary>Trade lanes: world units of width per million dollars a year (0.3 units for $100bn, so the data's widest
        /// flow, the European Union's $633bn of goods in 2024, draws 1.9 units: slim lanes, not slabs, in proportion to the
        /// last dollar), and the gap between the two directions, where the money paid for a deficit runs.</summary>
        const float WidthPerMillion = 3e-6f, LaneGap = 1.2f;

        /// <summary>Moments this many years before the shown year spark brighter and are named beside the president.</summary>
        const float NamedYears = 3;

        /// <summary>The most moments named at once, the newest first: a longer list would run down past the vice president.</summary>
        const int NamedMoments = 6;

        /// <summary>A partner is drawn when one of its benchmarks stands this many years from the shown year (the nearest is).</summary>
        const float PartnerYears = 3;

        /// <summary>
        /// The pose: straight from the future, 15 degrees from above. The cross-section faces the camera square, so the two
        /// parties stand at one depth and draw at one scale: a camera turned 30 degrees toward the Democrats' side drew their
        /// nearer head a tenth to a sixth larger, enough to reverse 2024's close popular vote on screen. The past is set back
        /// along a slant instead (<see cref="Shear"/>).
        /// </summary>
        const float Yaw = 180, Pitch = 15;

        /// <summary>
        /// The time axis's slant: a time standing a unit back from the shown year's plane stands this far toward +x too, the
        /// screen's left seen from the future, so the past recedes to the screen's left and up (with <see cref="Pitch"/>)
        /// behind the cross-section, the present on the right as a chart reads, while the cross-section stays square to the
        /// camera. 0.6 opens the decades as far as the 30-degree turn did.
        /// </summary>
        const float Shear = .6f;

        /// <summary>
        /// The past fades: a stream's rings, the track and the sparks a time stands this many years before the shown year draw
        /// at <see cref="PastAlpha"/> of their own alpha (eased from the shown year's whole; the track and the sparks keep at
        /// least half), so the present reads first and the decades the slant sets behind the cascade stay faint. A moment more
        /// than <see cref="StrandYears"/> before the shown year keeps its spark but not its strand to the floor: 56 strands by
        /// 2025 hung a curtain over the streams.
        /// </summary>
        const float FadeYears = 20, PastAlpha = .25f, StrandYears = 8;

        /// <summary>The decade marks along the track: every decade over a span this long or shorter, every second one over a
        /// longer span, where a decade's mark would stand on its neighbour's in the distance.</summary>
        const float EveryDecadeSpan = 40;

        /// <summary>
        /// The portrait cards' column as <see cref="HistoryPopups"/> lays it out (canvas units: its left, its top, a card's
        /// width and height, the gap between cards), which the camera's fit keeps clear of; and how many cards stand in it:
        /// the president and the three most senior holders on a landscape screen, at most the president's on a portrait one,
        /// where four tall cards would cover the view.
        /// </summary>
        const float CardLeft = 24, CardTop = 176, CardWidth = 330, CardHeight = 96, CardGap = 10;

        const int LandscapeCards = 4;

        /// <summary>
        /// The camera's fit (<see cref="FlyToState"/>): the screen's margin and the gap kept from each of the HUD's blocks
        /// (pixels at 1080p), the share of the screen's height within which a block standing against its top or bottom edge
        /// bounds the view's whole width (the title, the year chip, the preset bar: the view stands between them, never
        /// beside the title), and the least side a free rect may have.
        /// </summary>
        const float ScreenMargin = 16, BlockGap = 12, EdgeBand = .08f, MinFree = 120;

        /// <summary>The fit's passes (each scales the distance to the free rect, then centres the essentials in it; six come
        /// within a pixel, perspective and all), the distances it may choose between, and the points the shell's reach is
        /// sampled at.</summary>
        const int FitPasses = 6, ReachPoints = 24;

        const float MinDistance = 110, MaxDistance = 3000;

        /// <summary>Free rects whose essentials' scales differ by less than this are equals, and the larger is taken.</summary>
        const float Tie = 1e-3f;

        /// <summary>
        /// The room names take beyond their anchors (pixels at 1080p): a party's, the longest reading ("Republicans 76% of
        /// power", "votes not measured before 1948") at the name role's 15 pixels, and half its two lines' height; half the
        /// title's line at the title role's 18; and a decade mark's height above the presidency's track.
        /// </summary>
        const float PartyNamePx = 210, NameHalfPx = 19, TitleHalfPx = 14, DecadePx = 16;

        /// <summary>
        /// The room a trade node's name takes toward the screen's left of its anchor (pixels at 1080p, at the note role's 12):
        /// a partner's ("European Union", "US deficit $220bn (2025)", 115 pixels in v1's frames) and the world's, broken as a
        /// partner's is ("The world", "US surplus $4.7bn (1965)", "by partner from 1985": on one line it took 205 pixels and
        /// set the whole view's width on a portrait screen), and half their lines' height. The camera fits them, as v1's did:
        /// the nodes stand at the essentials' lower left, so a name left out ran onto the legend and was dropped.
        /// </summary>
        const float PartnerNamePx = 125, WorldNamePx = 125, NodeHalfPx = 16, WorldHalfPx = 24;

        /// <summary>The frames the HUD's blocks must hold still before the camera fits them again: a sheet opening or a panel
        /// sliding in is fitted once it stands, not chased.</summary>
        const int HudSettleFrames = 8;

        /// <summary>The decade marks along the presidency's track stop this many years before the shown year, where the
        /// president and the latest moments' names stand.</summary>
        const float DecadeClear = 4;

        /// <summary>The research's words for a party it infers rather than states: a cabinet secretary's from the appointing
        /// president, a chamber leader's from the chamber's majority (Tools/politics/build.py).</summary>
        const string AppointingBasis = "appointing president", MajorityBasis = "majority of the chamber";

        static readonly bool SmokeRun = Environment.GetCommandLineArgs().Contains("-smoketest");
        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        /// <summary>The states the smoke sequence captures (<c>-smokeextra politics</c>).</summary>
        static readonly string[] SmokeStates = { "politics:us:1950", "politics:us:1965", "politics:us:1985", "politics:us:2001", "politics:us:2025" };

        static HillPoliticsLayer current;

        /// <summary>The year a tour's cue or the panel pinned (clamped to the data's span); NaN while the view follows the year chip.</summary>
        public static float Pinned { get; private set; } = float.NaN;

        /// <summary>The thing highlighted: a party id, an office id ("president", "vice-president", "speaker", "senate-leader",
        /// "state", "treasury", "defense", "navy", "joint-chiefs"), "army", a rival ("ussr", "russia", "china"), a trade partner
        /// ("china-trade", "mexico", "canada", "japan", "germany", "eu", or "world" before partners are measured); null for none.</summary>
        public static string Selected { get; private set; }

        /// <summary>Raised when the year shown, the pin, the selection or the drawing changes (the panel follows).</summary>
        public static event Action Changed;

        /// <summary>The decimal year the view shows: the pinned year, else the middle of the year chip's year (the land on
        /// screen's, or the chip's while the land catches up), within the data's span.</summary>
        public static float Year
        {
            get
            {
                float year = !float.IsNaN(Pinned) ? Pinned : (LandService.ShownYear > 0 ? LandService.ShownYear : EconomyState.Year) + .5f;
                var m = Politics.Get();
                return m != null ? Mathf.Clamp(year, m.From, m.To) : year;
            }
        }

        /// <summary>A party's colour: its conventional one in the palette, the dim ink for any other.</summary>
        public static Color PartyColor(string party) =>
            party == Politics.Democrats ? Roles.Democratic : party == Politics.Republicans ? Roles.Republican : Roles.InkDim;

        /// <summary>
        /// A moment's colour by its kind, from the palette's meanings: a conflict is war (Conflict); a trade action is trade
        /// (Commerce); a monetary turn moves the price of money, which owners receive (Owners); a law and a regulation are the
        /// state's (its slate, a law lighter); a treaty settles or binds a conflict (Conflict, lightened).
        /// </summary>
        public static Color KindColor(string kind) => kind switch
        {
            "conflict" => Roles.Conflict, "trade" => Roles.Commerce, "monetary" => Roles.Owners,
            "law" => Color.Lerp(Roles.State, Roles.InkText, .5f), "regulation" => Color.Lerp(Roles.State, Roles.InkText, .25f),
            "treaty" => Color.Lerp(Roles.Conflict, Roles.InkText, .5f), _ => Roles.InkDim,
        };

        /// <summary>The view's key for the HUD's legend (its swatches are UI colours: the roles sRGB-encoded).</summary>
        public static readonly HillPresets.ViewKey Key = new HillPresets.ViewKey(new (string, Color)[]
            {
                ("Democratic Party", Roles.Encoded(Roles.Democratic)), ("Republican Party", Roles.Encoded(Roles.Republican)),
                ("The state: forces, laws, rules", Roles.Encoded(Roles.State)), ("Trade", Roles.Encoded(Roles.Commerce)),
                ("Money: deficits, monetary turns", Roles.Encoded(Roles.Owners)), ("War and treaties", Roles.Encoded(Roles.Conflict)),
            },
            "Left to right, the parties; up, their power by the stated rule (the presidency and each chamber's share of seats); ring width, the votes they won (faint before the first election measured); back and to the left, time, fading, by decade. The middle track: the presidency, term by term in the president's party colour; its sparks are the key moments (laws, regulations, trade actions, monetary turns, treaties, wars), the last eight years' with a strand to the floor. The dotted shell: the armed forces, as thick as their share of world military spending (NATO's fainter outside), walled by Jev's judgments, the sea wall over the trade lanes (freedom of navigation). Lanes: trade with each partner from the country's edge, goods out and in either side, the deficit hatched.");

        Politics model, builtModel, changesFor;
        GameObject content;
        Material occluderMaterial, outlineMaterial;
        Mesh outlineMesh;
        MeshFilter outlineFilter;
        readonly LineMeshBuilder outlineBuilder = new LineMeshBuilder(4096);
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        readonly List<HillLabel> labels = new List<HillLabel>();
        readonly List<Shown> bodies = new List<Shown>();
        readonly Dictionary<string, List<Vector3>> streamPaths = new Dictionary<string, List<Vector3>>();
        float[] changes = Array.Empty<float>();
        bool dirty = true, labelsShown, executiveDrawn;
        float builtYear = float.NaN, announcedYear = float.NaN, poseDistance = 330;
        int streamRings, trackTerms;
        string builtSelected;
        Vector3 outlineEye, lastEye;
        List<string> states = new List<string>();
        Scene scene;

        /// <summary>
        /// What the camera fits (<see cref="FlyToState"/>): the essentials, each a point in the content's frame and the room
        /// its name takes around it in pixels at 1080p (left, right, down, up). Built with the labels, once per rebuild.
        /// </summary>
        readonly List<Vector3> fitAt = new List<Vector3>(ReachPoints + 16);

        readonly List<Vector4> fitRoom = new List<Vector4>(ReachPoints + 16);

        /// <summary>The HUD's blocks as the labels keep out of them (screen pixels, origin bottom-left), the cards' column with
        /// them, and the free rect's candidate edges: filled again at each fit, never allocated after the first.</summary>
        readonly List<Rect> hudRects = new List<Rect>(32), obstacles = new List<Rect>(33);

        readonly List<float> lefts = new List<float>(34), rights = new List<float>(34), lows = new List<float>(34), highs = new List<float>(34);

        /// <summary>The camera's axes at the view's pose (in the content's frame) and its half-angle tangents and pixels,
        /// read at each fit.</summary>
        Vector3 camRight, camUp, camFwd;

        float tanV, tanH, screenW, screenH;

        /// <summary>The portrait cards shown (decided at each fit), the essentials' box on screen at the last fit, the HUD's
        /// blocks as that fit measured them, and a change of them waiting to hold still (<see cref="HudSettleFrames"/>).</summary>
        int shownCards, fittedHud, settlingHud, settling;

        Rect fittedBox;

        /// <summary>The pose the camera last flew to for this view (world), which a refit for the HUD compares with.</summary>
        CameraPose flown;

        /// <summary>The ids the parties' bodies are built under (see <see cref="FacingId"/>).</summary>
        static readonly Dictionary<string, string> facingIds = new Dictionary<string, string>();

        /// <summary>A body drawn this build: its shape where it stands and its alpha, for its outline.</summary>
        sealed class Shown
        {
            public Organism Shape;
            public Vector3 At;
            public float Alpha;
        }

        /// <summary>An office's holder in the cascade: where it stands, its colour's party (the administration's for an
        /// appointee the research gives no party of its own), and whether its name stands below it.</summary>
        sealed class Seat
        {
            public Politics.Office Office;
            public Vector3 At;
            public string Party;
            public bool Below;
            public int Line;
        }

        /// <summary>A party's head at the shown year: where its stream ends, its power by the rule, the votes it represents and
        /// the election they were cast in, and its seat shares.</summary>
        sealed class Head
        {
            public Politics.Party Party;
            public Vector3 At;
            public float Power, House, Senate;
            public long Votes;
            public Politics.Election Election;
            public Organism Shape;
        }

        /// <summary>A rival power beyond the shell: its share of world military spending (NaN past the series' gaps) and its
        /// pact's, with the points that measured them.</summary>
        sealed class Rival
        {
            public string Id, Name, PactName;
            public Vector3 At;
            public double Share, Pact = double.NaN;
            public Politics.Point Point, PactPoint;
            public bool PactStands;
            public float Thick, PactThick;
        }

        /// <summary>A trade partner measured near the shown year: its node and its nearest benchmark.</summary>
        sealed class Partner
        {
            public string Id, Select, Name;
            public Vector3 Node;
            public Politics.PartnerYear Year;
        }

        /// <summary>What a rebuild lays out at the shown year, shared by the drawing and the labels.</summary>
        sealed class Scene
        {
            public float Year, Z;
            public Politics.Term President;
            public Vector3 Centre, Exec;
            public readonly List<Seat> Offices = new List<Seat>();
            public readonly List<Head> Heads = new List<Head>();
            public double UsShare = double.NaN, NatoShare = double.NaN;
            public Politics.Point UsPoint, NatoPoint;
            public int NatoMembers;

            /// <summary>The shell's layers and walls by their radii; Reach is the walls' outer edge all round (the essentials'
            /// circle, with the title above it).</summary>
            public float UsThick, NatoThick, OuterR, WallR, Reach;

            public readonly List<Rival> Rivals = new List<Rival>();
            public readonly List<Politics.Wall> Walls = new List<Politics.Wall>();
            public readonly List<Partner> Partners = new List<Partner>();
            public Politics.TradeYear World;
            public Vector3 WorldNode;
            public readonly List<(Politics.Moment moment, Vector3 at)> Moments = new List<(Politics.Moment, Vector3)>();
        }

        // ------------------------------------------------------------------ life cycle

        public override void Prepare(GraphContext ctx) => Politics.Provide(ctx.Text(Politics.Path));

        public override void Upload(GraphContext ctx)
        {
            current = this;
            EconomyStage.Land().Place(transform);
            // drawn alone: the wave and the hill's marks behind the cross-section would only be noise
            EconomyEmphasis.RegisterView(View, PartNames.Select(p => View + "." + p), Array.Empty<string>(), Array.Empty<string>(), synthesis: true);
            states = StateNames();
            ViewVariants.Register(View, () => states.Count, k => ShowState(k >= 0 && k < states.Count ? states[k] : null),
                k => k >= 0 && k < states.Count ? states[k] : k.ToString(Ci));
            if (SmokeRun) SmokeChecks.RegisterSequence(Sequence, Smoke());
        }

        /// <summary>Two states of the view's own, and every "show" the walkthroughs' cues give it, so the director can show
        /// them by name (it matches a cue's string exactly).</summary>
        static List<string> StateNames()
        {
            var names = new List<string> { "politics:us:1965", "politics:us:2025" };
            foreach (string path in new[] { "Data/Walkthrough/economy_tour", "Data/Walkthrough/story_ai_tour", "Data/Walkthrough/story_revolutions_tour" })
            {
                var asset = Resources.Load<TextAsset>(path);
                if (asset == null) continue;
                foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(asset.text, "\"show\"\\s*:\\s*\"(politics:[^\"]+)\""))
                    if (!names.Contains(m.Groups[1].Value)) names.Add(m.Groups[1].Value);
                Resources.UnloadAsset(asset);
            }

            return names;
        }

        /// <summary>Shows a named state, "politics:us:&lt;year&gt;[:sel=&lt;id&gt;]", the year pinned; null restores the default:
        /// the year chip's year and nothing selected. A whole year ("1965") pins its middle, as the chip and the panel's steps
        /// do, where the data's annual measures stand and the year's own officeholders and Congress hold; a decimal year
        /// ("2025.06") pins that date.</summary>
        public static void ShowState(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                Selected = null;
                Pin(float.NaN);
                return;
            }

            var parts = name.Split(':');
            if (parts.Length < 3 || parts[0] != View || parts[1] != Country) return;
            if (!float.TryParse(parts[2], NumberStyles.Float, Ci, out float year)) return;
            if (parts[2].IndexOf('.') < 0) year += .5f;
            Selected = parts.FirstOrDefault(p => p.StartsWith("sel=", StringComparison.Ordinal))?.Substring(4);
            Pin(year);
        }

        /// <summary>Shows a year (clamped to the data's span), the camera flying to it; NaN follows the year chip again.</summary>
        public static void Pin(float year)
        {
            var m = Politics.Get();
            Pinned = float.IsNaN(year) ? float.NaN : m != null ? Mathf.Clamp(year, m.From, m.To) : year;
            if (current != null)
            {
                current.dirty = true;
                current.FlyToState(SmokeRun ? 0 : 1.6f);
            }

            Changed?.Invoke();
        }

        /// <summary>Highlights one thing (an id as <see cref="Selected"/> lists them); null clears the highlight.</summary>
        public static void Select(string id)
        {
            Selected = string.IsNullOrEmpty(id) ? null : id;
            if (current != null) current.dirty = true;
            Changed?.Invoke();
        }

        /// <summary>Flies to the year shown, which also shows the portrait cards the fit leaves room for.</summary>
        public override void OnFocus(ViewPreset preset)
        {
            if (preset == null || preset.Id != View) return;
            dirty = true;
            FlyToState(SmokeRun ? 0 : 1.4f);
        }

        void OnDestroy()
        {
            ClearBuilt();
            if (outlineMesh) Destroy(outlineMesh);
            if (outlineMaterial) Destroy(outlineMaterial);
            if (occluderMaterial) Destroy(occluderMaterial);
            if (labelsShown) HillLabels.Clear(this);
            labelsShown = false;
            if (current == this) current = null;
        }

        public override void Tick(GraphContext ctx, CameraRig rig)
        {
            float year = Year;
            if (year != announcedYear)
            {
                announcedYear = year;
                Changed?.Invoke();
            }

            model = Politics.Get();
            if (LandView.PresetId != View || model == null)
            {
                if (content) content.SetActive(false);
                ShowLabels(false);
                return;
            }

            EnsureContent();
            content.SetActive(true);
            if (Stale(year))
            {
                // following the chip, the land drove the change: one hills layer rebuilds a frame (HillRebuilds)
                bool chipMoved = !dirty && float.IsNaN(Pinned) && model == builtModel && year != builtYear, loaded = model != builtModel;
                if (!chipMoved || HillRebuilds.Take())
                {
                    Rebuild(year);
                    // the cross-section moved along the time axis (about two units a year), or the data arrived after the
                    // view opened: the camera fits it
                    if (chipMoved || loaded) FlyToState(SmokeRun ? 0 : .8f);
                }
            }

            // the HUD's blocks moved (the panel shows a frame after the view, a portrait sheet opens, a tour's card comes, the
            // screen turns): once they have held still a few frames, the camera fits the screen they leave. A drawing still
            // waiting for its turn to rebuild (HillRebuilds) waits: that rebuild fits the camera itself, and a refit now would
            // rebuild out of turn
            int hud = MeasureHud();
            if (hud == fittedHud) settling = 0;
            else if (hud != settlingHud)
            {
                settlingHud = hud;
                settling = 0;
            }
            else if (++settling >= HudSettleFrames && !Stale(year))
            {
                settling = 0;
                FlyToState(SmokeRun ? 0 : .6f, refit: true);
            }

            // the outlines face the camera: rebuilt once it has settled somewhere new, never through a flight
            Vector3 eye = Eye();
            if ((eye - lastEye).sqrMagnitude < .0025f && (eye - outlineEye).sqrMagnitude > 4) Outlines(eye);
            lastEye = eye;
            ShowLabels(true);
        }

        /// <summary>Whether the drawing is not of this year, selection and data (or was marked for a rebuild).</summary>
        bool Stale(float year) => dirty || model != builtModel || year != builtYear || !string.Equals(Selected, builtSelected, StringComparison.Ordinal);

        void EnsureContent()
        {
            if (content != null) return;
            content = new GameObject("Politics");
            content.transform.SetParent(transform, false);
        }

        /// <summary>The camera's place in the content's frame (the origin without a camera).</summary>
        Vector3 Eye()
        {
            var root = GraphRoot.Instance;
            var cam = root != null && root.Rig != null ? root.Rig.Cam : null;
            return cam != null && content != null ? content.transform.InverseTransformPoint(cam.transform.position) : Vector3.zero;
        }

        // ------------------------------------------------------------------ the build

        /// <summary>Destroys what the last build made (its renderers, meshes and materials).</summary>
        void ClearBuilt()
        {
            foreach (var o in owned)
                if (o)
                    Destroy(o);
            owned.Clear();
            bodies.Clear();
            streamPaths.Clear();
        }

        void Rebuild(float year)
        {
            dirty = false;
            builtYear = year;
            builtSelected = Selected;
            builtModel = model;
            ClearBuilt();
            if (occluderMaterial == null) occluderMaterial = PointBodies.OccluderMaterial();
            Changes();
            scene = Lay(year);
            Streams();
            Track();
            Marks();
            Bodies();
            Army();
            Walls();
            Lanes();
            Outlines(Eye());
            MakeLabels();
            Essentials();
            if (labelsShown) HillLabels.Set(this, labels);
            if (LandView.PresetId == View) ShowPopups();
            Changed?.Invoke();
        }

        /// <summary>Where a time stands on the view's time axis: the shown year's plane, and the years before it
        /// <see cref="TimeScale"/> times as deep as on the land.</summary>
        static float Back(float shownZ, float t) => shownZ + TimeScale * (HillLandscape.Z(t) - shownZ);

        /// <summary>A point of the past: at (x, y) in its time's plane on the view's time axis (<see cref="Back"/>), slanted
        /// toward +x as far as it stands back (<see cref="Shear"/>), so the past recedes to the screen's left.</summary>
        static Vector3 Past(float x, float y, float shownZ, float t)
        {
            float back = Back(shownZ, t);
            return new Vector3(x + Shear * (shownZ - back), y, back);
        }

        /// <summary>How much of its own alpha a time draws with: whole at the shown year, <see cref="PastAlpha"/> from
        /// <see cref="FadeYears"/> before it.</summary>
        float Age(float t) => Mathf.Lerp(1, PastAlpha, Mathf.SmoothStep(0, 1, (scene.Year - t) / FadeYears));

        /// <summary>A mesh of lines under the content, alpha-blended and in object space; its renderer, mesh and material are
        /// the build's.</summary>
        void Lines(LineMeshBuilder b, string name, int queue)
        {
            if (b.VertexCount == 0) return;
            var mesh = b.ToMesh("Politics " + name);
            Material m = GraphMaterials.Raw(GraphMaterials.Line(Color.white, 1, queue, false));
            Own(AddMesh("Politics " + name, mesh, m), mesh, m);
        }

        /// <summary>A renderer the build made: under the content, destroyed with its mesh and material on the next build.</summary>
        MeshRenderer Own(MeshRenderer r, Mesh mesh, Material material)
        {
            r.transform.SetParent(content.transform, false);
            owned.Add(r.gameObject);
            owned.Add(mesh);
            owned.Add(material);
            return r;
        }

        /// <summary>The times a change of power or votes can happen (a president's start, a chamber's row, an election), in
        /// order, once per model.</summary>
        void Changes()
        {
            if (changesFor == model) return;
            changesFor = model;
            changes = model.Presidents.Select(p => p.From).Concat(model.House.Select(r => r.From)).Concat(model.Senate.Select(r => r.From))
                .Concat(model.Elections.Select(e => e.Date)).Where(t => !float.IsNaN(t)).Distinct().OrderBy(t => t).ToArray();
        }

        /// <summary>The last change at or before t (-1 before the first).</summary>
        int LastChange(float t)
        {
            int i = Array.BinarySearch(changes, t);
            return i >= 0 ? i : ~i - 1;
        }

        /// <summary>A party's power at t by the rule, eased over <see cref="Ease"/> after each change.</summary>
        float EasedPower(string party, float t)
        {
            int i = LastChange(t);
            if (i < 0 || t - changes[i] >= Ease) return model.Power(party, t);
            return Mathf.Lerp(model.Power(party, changes[i] - Before), model.Power(party, changes[i]), Mathf.SmoothStep(0, 1, (t - changes[i]) / Ease));
        }

        /// <summary>The votes a party represents at t, eased over <see cref="Ease"/> after each election (0 before the first).</summary>
        float EasedVotes(string party, float t)
        {
            float now = model.Represented(party, t);
            int i = LastChange(t);
            if (i < 0 || t - changes[i] >= Ease) return now;
            float before = model.Represented(party, changes[i] - Before);
            return before > 0 ? Mathf.Lerp(before, now, Mathf.SmoothStep(0, 1, (t - changes[i]) / Ease)) : now;
        }

        /// <summary>A direction in the cross-section's plane from a screen angle (degrees counter-clockwise from the screen's
        /// right): seen from the future, the screen's right is -x.</summary>
        static Vector3 ScreenDir(float degrees)
        {
            float a = degrees * Mathf.Deg2Rad;
            return new Vector3(-Mathf.Cos(a), Mathf.Sin(a), 0);
        }

        /// <summary>A share of the world's military spending (%) at t, NaN past the series' gaps, with the point nearest t.</summary>
        double Share(string spender, float t, out Politics.Point nearest)
        {
            nearest = default;
            return model.MilitaryShare.TryGetValue(spender, out var series) ? Politics.At(series, t, out nearest) : double.NaN;
        }

        /// <summary>A shell's thickness for a share (%), the faint thickness when it is not measured; a share is held to 0-100,
        /// so the shell's circles stay bounded whatever a row holds.</summary>
        static float Thickness(double share) => double.IsNaN(share) ? Unmeasured : ShareDepth * Mathf.Clamp((float)share, 0, 100) / 100;

        /// <summary>The Soviet Union stands until its dissolution (the research's moment), else until Russia's series begins. The
        /// panel names the rival by the same rule, so its row and its highlight id ("ussr" or "russia") are the drawn rival's
        /// even when a moment's row pins the dissolution's own date.</summary>
        public static float SovietEnd(Politics model)
        {
            var end = model.Moments.FirstOrDefault(m => m.Id == "pol-1991-ussr-dissolved");
            if (end != null) return end.Date;
            return model.MilitaryShare.TryGetValue("russia", out var russia) && russia.Count > 0 ? Mathf.Floor(russia[0].T) : model.To;
        }

        /// <summary>Lays out the cross-section at a year: the president and the cascade, the parties' heads, the army and its
        /// rivals and walls, the partners measured near the year (or the world), and the moments up to it on the presidency's
        /// track.</summary>
        Scene Lay(float year)
        {
            float z = HillLandscape.Z(year);
            var s = new Scene { Year = year, Z = z, President = model.PresidentAt(year) };
            s.Centre = new Vector3(X0, ShellY, z);
            s.Exec = new Vector3(X0, ExecY, z);

            // the cascade: each rank's line a step down from the president to the Joint Chiefs' chairman on the shell
            var held = model.OfficesAt(year).ToList();
            float jc = ShellY - ShellR + OfficeR + .6f, step = (ExecY - jc) / CascadeLines.Length;
            for (int k = 0; k < CascadeLines.Length; k++)
            {
                var line = held.Where(o => Array.IndexOf(CascadeLines[k], o.Rank) >= 0).ToList();
                int n = line.Count;
                float y = ExecY - (k + 1) * step, gap = n > 3 ? CrowdedGap : OfficeGap;
                for (int i = 0; i < n; i++)
                {
                    var o = line[i];
                    // the higher rank on the screen's left (+x); neighbours' names alternate above and below, and on a line of
                    // three the outer names stand below, clear of a party low on power
                    bool below = n == 1 || (n == 3 ? i != 1 : i % 2 == (n == 2 ? 1 : 0));
                    // an appointee the research gives no party of its own serves the administration in office at the year (a
                    // holdover too), which its colour and its words name
                    string party = o.Military ? null : o.PartyBasis == AppointingBasis ? s.President?.Party ?? o.Party : o.Party;
                    s.Offices.Add(new Seat { Office = o, At = new Vector3(X0 + gap * ((n - 1) * .5f - i), y, z), Party = party, Below = below, Line = k });
                }
            }

            foreach (var p in model.Parties)
            {
                s.Heads.Add(new Head
                {
                    Party = p, At = new Vector3(X0 - p.Side * PartyX, Floor + PowerHeight * EasedPower(p.Id, year), z), Power = model.Power(p.Id, year),
                    Votes = model.Represented(p.Id, year), Election = model.ElectionAt(year), House = model.HouseAt(year)?.Share(p.Id) ?? 0,
                    Senate = model.SenateAt(year)?.Share(p.Id) ?? 0,
                });
            }

            // the army: the US's share as the shell, NATO's (which holds it) as the layer beyond
            s.UsShare = Share("usa", year, out s.UsPoint);
            s.NatoMembers = model.NatoMembersAt(year);
            if (s.NatoMembers > 0) s.NatoShare = Share("nato", year, out s.NatoPoint);
            s.UsThick = Thickness(s.UsShare);
            s.NatoThick = s.NatoMembers == 0 ? 0 : double.IsNaN(s.NatoShare) || double.IsNaN(s.UsShare) ? Unmeasured : Mathf.Max(0, Thickness(s.NatoShare) - s.UsThick);
            s.OuterR = ShellR + s.UsThick + s.NatoThick;
            s.WallR = s.OuterR + 2.5f;
            s.Walls.AddRange(model.WallsAt(year));
            s.Reach = s.WallR + s.Walls.Select(WallThickness).DefaultIfEmpty(0).Max();

            bool soviet = year < SovietEnd(model);
            AddRival(s, soviet ? "ussr" : "russia", soviet ? "Soviet Union" : "Russia", RivalUpper, soviet && model.WarsawPactAt(year));
            AddRival(s, "china", "China", RivalLower, false);

            // trade: each partner measured within three years (its nearest benchmark), or, before any is, the world
            foreach (var (id, name, angle) in PartnerSeats)
            {
                if (!model.Partners.TryGetValue(id, out var rows)) continue;
                var near = rows.Where(r => Mathf.Abs(r.T - year) <= PartnerYears).OrderBy(r => Mathf.Abs(r.T - year)).FirstOrDefault();
                if (near == null) continue;
                s.Partners.Add(new Partner { Id = id, Select = id == "china" ? "china-trade" : id, Name = name, Node = s.Centre + ScreenDir(angle) * NodeR, Year = near });
            }

            s.WorldNode = s.Centre + ScreenDir(WorldAngle) * NodeR;
            if (s.Partners.Count == 0) s.World = model.TradeAt(year);

            // every moment on the presidency's track, whichever party held it: the government did them, not a party
            foreach (var m in model.MomentsBetween(model.From, year)) s.Moments.Add((m, Past(X0, ExecY, z, m.Date)));
            return s;
        }

        /// <summary>A rival power at its screen angle: its share and, while its pact stands, the pact's (which holds it) as the
        /// layer beyond.</summary>
        void AddRival(Scene s, string id, string name, float angle, bool pact)
        {
            var r = new Rival { Id = id, Name = name, At = s.Centre + ScreenDir(angle) * RivalD, PactStands = pact, PactName = pact ? "Warsaw Pact" : null };
            r.Share = Share(id, s.Year, out r.Point);
            r.Thick = Thickness(r.Share);
            if (pact)
            {
                r.Pact = Share("wto", s.Year, out r.PactPoint);
                r.PactThick = double.IsNaN(r.Pact) || double.IsNaN(r.Share) ? Unmeasured : Mathf.Max(0, Thickness(r.Pact) - r.Thick);
            }

            s.Rivals.Add(r);
        }

        /// <summary>How lit a thing is under the selection: whole when nothing or it is selected, dimmed otherwise.</summary>
        static float Lit(string id) => Selected == null || Selected == id ? 1 : .35f;

        /// <summary>
        /// Each party's stream from the span's start to the shown year: a ring of dots at every sample, centred at the party's
        /// place and power, as wide as the votes it represents (a faint thread before the first election measured); and the
        /// stream's centre line, along which its people drift (<see cref="Lanes"/>).
        /// </summary>
        void Streams()
        {
            float perYear = TimeScale * Mathf.Max(.5f, Mathf.Abs(HillLandscape.Z(model.From + 1) - HillLandscape.Z(model.From)));
            var times = SampleTimes(scene.Year, RingEvery / perYear);
            // sized to its content: at most 16 dots a ring, each a two-point dash
            var b = new LineMeshBuilder(times.Count * model.Parties.Count * 16 * 2);
            float id = Id(Part.Streams);
            float firstVote = model.Elections.Count > 0 ? model.Elections[0].Date : float.PositiveInfinity;
            streamRings = 0;
            foreach (var party in model.Parties)
            {
                Color c = PartyColor(party.Id);
                float lit = Lit(party.Id), x = X0 - party.Side * PartyX;
                var path = new List<Vector3>(times.Count);
                foreach (float t in times)
                {
                    float votes = EasedVotes(party.Id, t);
                    bool measured = t >= firstVote && votes > 0;
                    float r = measured ? Mathf.Max(ThreadR, votes / VotesPerUnit) : ThreadR;
                    var centre = Past(x, Floor + PowerHeight * EasedPower(party.Id, t), scene.Z, t);
                    int n = Mathf.Clamp(Mathf.RoundToInt(r * 3.6f), 5, 16);
                    float alpha = (measured ? .82f : .26f) * lit * Age(t), phase = t * 7.3f % 1f;
                    for (int i = 0; i < n; i++)
                    {
                        float ang = (i + phase) * 2 * Mathf.PI / n;
                        Vector3 p = centre + new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0) * r;
                        b.AddSegment(p, p + Vector3.up * PointBodies.DotLength, WithAlpha(c, alpha), 2.1f, 0, id, .93f);
                    }

                    streamRings++;
                    if (measured) path.Add(centre);
                }

                streamPaths[party.Id] = path;
            }

            Lines(b, "streams", 3191);
        }

        /// <summary>
        /// The times the streams are drawn at: every <see cref="RingEvery"/> units of the view's time axis from the span's
        /// start to the year, and through each change before it, densely enough that its eased step reads as one (a ring every
        /// 1.5 units of the jump).
        /// </summary>
        List<float> SampleTimes(float year, float step)
        {
            var times = new List<float>(256);
            for (float t = model.From; t < year; t += step) times.Add(t);
            times.Add(year);
            foreach (float c in changes)
            {
                if (c <= model.From || c >= year) continue;
                float jump = 0;
                foreach (var p in model.Parties)
                {
                    jump = Mathf.Max(jump, PowerHeight * Mathf.Abs(model.Power(p.Id, c) - model.Power(p.Id, c - Before)));
                    jump = Mathf.Max(jump, Math.Abs(model.Represented(p.Id, c) - model.Represented(p.Id, c - Before)) / VotesPerUnit);
                }

                int k = Mathf.Clamp(Mathf.CeilToInt(jump / 1.5f), 1, 16);
                for (int j = 0; j <= k; j++)
                {
                    float t = c + Ease * j / k;
                    if (t < year) times.Add(t);
                }
            }

            times.Sort();
            int kept = 0;
            for (int i = 0; i < times.Count; i++)
                if (kept == 0 || times[i] - times[kept - 1] > 1e-3f)
                    times[kept++] = times[i];
            times.RemoveRange(kept, times.Count - kept);
            return times;
        }

        /// <summary>
        /// The presidency through time: a thin track at the executive's height down the middle from the span's start to the
        /// year, each president's term in the party's colour (Truman's blue, Eisenhower's red), with a short tick where each
        /// term begins, so a successor of the same party (Johnson after Kennedy) reads as a new term. The key moments spark on
        /// it (<see cref="Marks"/>): the parties' streams carry only the parties.
        /// </summary>
        void Track()
        {
            // a piece every two years at most, each as faded as its middle (the track keeps half its alpha however old), and
            // the term's tick: six segments a term at most, each two points
            var b = new LineMeshBuilder(model.Presidents.Count * 12);
            float id = Id(Part.Streams), lit = Lit("president");
            trackTerms = 0;
            foreach (var term in model.Presidents)
            {
                float from = Mathf.Max(term.From, model.From), to = float.IsNaN(term.To) ? scene.Year : Mathf.Min(term.To, scene.Year);
                if (to <= from) continue;
                Color c = PartyColor(term.Party);
                int pieces = Mathf.Max(1, Mathf.CeilToInt((to - from) / 2));
                for (int k = 0; k < pieces; k++)
                {
                    float a = Mathf.Lerp(from, to, (float)k / pieces), e = Mathf.Lerp(from, to, (float)(k + 1) / pieces);
                    b.AddSegment(Past(X0, ExecY, scene.Z, a), Past(X0, ExecY, scene.Z, e), WithAlpha(c, .9f * lit * Mathf.Max(Age((a + e) * .5f), .5f)),
                        2.6f, 0, id, 1);
                }

                Vector3 start = Past(X0, ExecY, scene.Z, from);
                if (term.From > model.From)
                    b.AddSegment(start + Vector3.down * 1.6f, start + Vector3.up * 1.6f, WithAlpha(c, .9f * lit * Mathf.Max(Age(from), .5f)), 2f, 0, id, 1);
                trackTerms++;
            }

            Lines(b, "presidency", 3191);
        }

        /// <summary>The key moments up to the year: a spark on the presidency's track at each one's date, coloured by its kind
        /// (the named ones larger and brighter, the old ones faded as the track is), and for those of the last
        /// <see cref="StrandYears"/> a faint strand straight down to the floor with a foot where it lands.</summary>
        void Marks()
        {
            var b = new LineMeshBuilder(4096);
            float id = Id(Part.Marks);
            foreach (var (m, at) in scene.Moments)
            {
                Color c = KindColor(m.Kind);
                bool named = scene.Year - m.Date <= NamedYears;
                float size = named ? 1.4f : 1, glow = named ? 1.4f : 1, fade = Mathf.Max(Age(m.Date), .5f);
                for (int k = 0; k < 8; k++)
                {
                    float ang = k * Mathf.PI / 4;
                    Vector3 ray = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), Mathf.Cos(ang) * .35f) * (k % 2 == 0 ? 3.2f : 1.9f) * size;
                    b.AddSegment(at, at + ray, WithAlpha(c, .92f * fade), 2.2f, 0, id, glow);
                }

                if (scene.Year - m.Date > StrandYears) continue;
                var foot = new Vector3(at.x, Floor, at.z);
                b.AddSegment(at + Vector3.down * 1.4f, foot, WithAlpha(c, named ? .5f : .28f), 1, 0, id, .95f);
                b.AddSegment(foot + Vector3.right * 1.6f, foot - Vector3.right * 1.6f, WithAlpha(c, .65f), 1.8f, 0, id, .95f);
            }

            Lines(b, "marks", 3192);
        }

        // ------------------------------------------------------------------ the cross-section's bodies

        static double Vol(float r) => Math.Pow(r / ContestNetwork.KR, 3);

        /// <summary>A body's core: the whole of a body that has no lobes.</summary>
        static ContestNetwork.Part Core(float r, Color tint, float solid) =>
            new ContestNetwork.Part(ContestNetwork.PartKind.Core, Vector3.zero, r, Vol(r), tint, solid);

        /// <summary>
        /// The president, the cascade's holders (a vacancy is an empty dotted outline, drawn with the outlines), the parties'
        /// heads and the trade partners' nodes, each an occluding point-cloud body.
        /// </summary>
        void Bodies()
        {
            executiveDrawn = false;
            if (scene.President != null)
            {
                Color c = PartyColor(scene.President.Party);
                AddBody("president", scene.Exec, new List<ContestNetwork.Part> { Core(ExecR, c, .8f) }, 3, Lit("president"));
                executiveDrawn = true;
            }

            foreach (var seat in scene.Offices)
            {
                if (seat.Office.Vacant) continue;
                Color c = seat.Office.Military ? Color.Lerp(Roles.State, Roles.InkText, .35f) : PartyColor(seat.Party);
                AddBody(seat.Office.Id, seat.At, new List<ContestNetwork.Part> { Core(OfficeR, c, .7f) }, 2, Lit(seat.Office.Id));
            }

            foreach (var h in scene.Heads)
            {
                Color c = PartyColor(h.Party.Id), lobe = Color.Lerp(c, Roles.InkText, .3f);
                bool measured = h.Votes > 0;
                // the core as wide as the stream's last ring; unmeasured, a small core of the party's colour greyed
                float core = measured ? Mathf.Max(1.6f, h.Votes / VotesPerUnit) : 2.2f;
                var parts = new List<ContestNetwork.Part>(3) { Core(core, measured ? c : Color.Lerp(c, Roles.Missing, .5f), .7f) };
                foreach (float share in new[] { h.House, h.Senate })
                {
                    float r = SeatLobe * Mathf.Pow(Mathf.Max(share, .01f), 1f / 3);
                    parts.Add(new ContestNetwork.Part(ContestNetwork.PartKind.Built, Vector3.zero, r, Vol(r), lobe, .8f));
                }

                h.Shape = AddBody(FacingId("politics." + h.Party.Id), h.At, parts, 3, Lit(h.Party.Id) * (measured ? 1 : .55f));
            }

            foreach (var p in scene.Partners)
                AddBody("partner." + p.Id, p.Node, new List<ContestNetwork.Part> { Core(NodeSize, Roles.Commerce, .6f) }, 2, Lit(p.Select));
            if (scene.Partners.Count == 0)
                AddBody("world", scene.WorldNode, new List<ContestNetwork.Part> { Core(NodeSize, Roles.Commerce, .6f) }, 2, Lit("world") * (scene.World != null ? 1 : .45f));
            Cascade();
        }

        /// <summary>The cascade's thread: a thin line from each holder (or vacancy) up to the nearest one on the line above it,
        /// the vice president's to the president.</summary>
        void Cascade()
        {
            var b = new LineMeshBuilder(64);
            float id = Id(Part.Bodies);
            foreach (var seat in scene.Offices)
            {
                Vector3 above = scene.Exec;
                float reach = ExecR, best = float.PositiveInfinity;
                // the nearest line above that holds anyone (the president's when none does)
                for (int k = seat.Line - 1; k >= 0 && float.IsPositiveInfinity(best); k--)
                    foreach (var up in scene.Offices)
                    {
                        float dx = Mathf.Abs(up.At.x - seat.At.x);
                        if (up.Line != k || dx >= best) continue;
                        best = dx;
                        above = up.At;
                        reach = OfficeR;
                    }

                b.AddSegment(above + Vector3.down * (reach + .6f), seat.At + Vector3.up * (OfficeR + .6f), WithAlpha(Roles.InkDim, .5f * Lit(seat.Office.Id)), 1.2f, 0, id, .95f);
            }

            Lines(b, "cascade", 3192);
        }

        /// <summary>
        /// The id a party's body is built under: the party's own, or the first of its numbered variants whose seeded twist
        /// turns its two seat lobes to the camera's left and right. Organism spreads the lobes above a core around an angle
        /// seeded by the id (the first lobe toward +x at a twist of 0, turning toward +z), and a lobe turned toward or away from
        /// the camera hides behind the core; the camera's left at the view's pose lies <see cref="Yaw"/> - 180 degrees from +x
        /// toward -z, so here the House lobe stands on the screen's left and the Senate's on its right for both parties.
        /// </summary>
        static string FacingId(string id)
        {
            if (facingIds.TryGetValue(id, out string found)) return found;
            string best = id;
            float bestCos = -2, facing = (180 - Yaw) * Mathf.Deg2Rad;
            for (int k = 0; k < 256 && bestCos < .97f; k++)
            {
                string candidate = k == 0 ? id : id + "#" + k.ToString(Ci);
                float c = Mathf.Cos(Organism.Hash01(candidate) * Mathf.PI * 2 - facing);
                if (c > bestCos)
                {
                    bestCos = c;
                    best = candidate;
                }
            }

            return facingIds[id] = best;
        }

        /// <summary>One body: its cloud and its occluder (which hides what stands behind it) at its place in the cross-section.</summary>
        Organism AddBody(string id, Vector3 at, List<ContestNetwork.Part> parts, int detail, float alpha)
        {
            var body = new ContestNetwork.Body { Id = id, Name = id, Kind = ContestNetwork.Kind.Company, CustomParts = parts, Alive = true, Value = double.NaN };
            var shape = new Organism(body, null, detail, trueVolume: true);
            var lb = new LineMeshBuilder(detail >= 3 ? 8192 : 2048);
            PointBodies.Cloud(lb, shape, Id(Part.Bodies), 3, new System.Random(Hash(id)), Roles.InkText, alpha);
            var mesh = lb.ToMesh("Politics body " + id);
            Material m = GraphMaterials.Raw(GraphMaterials.Line(Color.white, 1, 3193, false));
            var mr = Own(AddMesh("Politics body " + id, mesh, m), mesh, m);
            mr.transform.localPosition = at;
            var occ = PointBodies.Occluder(shape, 2, "Politics occluder " + id);
            owned.Add(occ);
            AddMesh("Politics occluder " + id, occ, occluderMaterial).transform.SetParent(mr.transform, false);
            bodies.Add(new Shown { Shape = shape, At = at, Alpha = alpha });
            return shape;
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

        /// <summary>Each body's silhouette from where the camera stands, and each vacancy's empty dotted outline; the mesh is
        /// replaced, its builder and material kept.</summary>
        void Outlines(Vector3 eye)
        {
            outlineEye = eye;
            if (content == null || scene == null) return;
            outlineBuilder.Clear();
            float id = Id(Part.Outlines);
            // in each body's own colours (no body here has a frontier for the silhouette's frontier colour to light)
            foreach (var v in bodies) PointBodies.Silhouette(outlineBuilder, v.Shape, v.At, eye, id, v.Alpha * .7f, Roles.InkText, 1.4f);
            foreach (var seat in scene.Offices)
                if (seat.Office.Vacant)
                    PointBodies.DottedCircle(outlineBuilder, seat.At, OfficeR, eye, WithAlpha(Roles.InkDim, .85f * Lit(seat.Office.Id)), 1.6f, id, 1);
            if (outlineMesh) Destroy(outlineMesh);
            outlineMesh = outlineBuilder.ToMesh("Politics outlines");
            if (outlineFilter == null)
            {
                outlineMaterial = GraphMaterials.Raw(GraphMaterials.Line(Color.white, 1, 3195, false));
                var r = AddMesh("Politics outlines", outlineMesh, outlineMaterial);
                r.transform.SetParent(content.transform, false);
                outlineFilter = r.GetComponent<MeshFilter>();
            }

            outlineFilter.sharedMesh = outlineMesh;
        }

        // ------------------------------------------------------------------ the army, its rivals and its walls

        /// <summary>
        /// The standing army: the US's shell around the cross-section as thick as its share of the world's military spending,
        /// NATO's share a fainter layer beyond it while NATO stands; the rivals' shells beyond, the Warsaw Pact's share a
        /// fainter layer on the Soviet Union's while the pact stands. A share not measured draws two faint circles.
        /// </summary>
        void Army()
        {
            var b = new LineMeshBuilder(16384);
            float id = Id(Part.Army), lit = Lit("army");
            Color slate = Color.Lerp(Roles.State, Roles.InkText, .3f);
            Shell(b, scene.Centre, ShellR, scene.UsThick, !double.IsNaN(scene.UsShare), slate, .8f * lit, id);
            if (scene.NatoMembers > 0) Shell(b, scene.Centre, ShellR + scene.UsThick + RingStep, scene.NatoThick - RingStep, !double.IsNaN(scene.NatoShare), slate, .3f * lit, id);
            foreach (var r in scene.Rivals)
            {
                float rl = Lit(r.Id);
                Shell(b, r.At, RivalCore, r.Thick, !double.IsNaN(r.Share), slate, .8f * rl, id);
                if (r.PactStands) Shell(b, r.At, RivalCore + r.Thick + RingStep, r.PactThick - RingStep, !double.IsNaN(r.Pact), slate, .3f * rl, id);
            }

            Lines(b, "army", 3190);
        }

        /// <summary>A shell of dots in the cross-section's plane: circles every <see cref="RingStep"/> from r0 through its
        /// thickness (one at least), a dot every <see cref="DotStep"/> around each, each circle's dots offset from the last so
        /// no seams line up; a share not measured draws faint.</summary>
        static void Shell(LineMeshBuilder b, Vector3 centre, float r0, float thick, bool measured, Color colour, float alpha, float id)
        {
            Color c = WithAlpha(colour, measured ? alpha : alpha * .35f);
            int rings = Mathf.Max(1, Mathf.FloorToInt(Mathf.Max(0, thick) / RingStep) + 1);
            for (int k = 0; k < rings; k++)
            {
                float r = r0 + k * RingStep, phase = k * .618f % 1f;
                int n = Mathf.Clamp(Mathf.RoundToInt(2 * Mathf.PI * r / DotStep), 12, 720);
                for (int i = 0; i < n; i++)
                {
                    float a = (i + phase) * 2 * Mathf.PI / n;
                    Vector3 p = centre + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * r;
                    b.AddSegment(p, p + Vector3.up * PointBodies.DotLength, c, 2f, 0, id, .93f);
                }
            }
        }

        /// <summary>Where each source's wall stands around the shell (screen angles, degrees): nuclear weapons facing the
        /// rivals, spending and alliances above, and command of the sea over the trade lanes, where it keeps them open.</summary>
        static (float from, float to) WallArc(string source) => source switch
        {
            "nuclear" => (-60, 40), "spending" => (46, 86), "alliances" => (94, 130), _ => (136, 262),
        };

        /// <summary>A wall Jev was unsure of: its confidence under 0.15 (its answer spread over the levels), as history's.</summary>
        static bool Uncertain(Politics.Wall w) => !float.IsNaN(w.Confidence) && w.Confidence < .15f;

        /// <summary>A wall's thickness: 1.2 units, and 2.4 more for each level Jev judged it (thin where it was not judged), the
        /// level held to Jev's 0-4 so the strands' loop stays bounded whatever a row holds.</summary>
        static float WallThickness(Politics.Wall w) => float.IsNaN(w.Height) ? 1.2f : 1.2f + 2.4f * Mathf.Clamp(w.Height, 0, 4);

        /// <summary>The army's walls outside the shell: an arc of strands for each source judged near the year, as thick as
        /// Jev judged it keeps the strongest rival from matching the United States (0-4), dashed where Jev was unsure; none
        /// when no judgment stands near the year.</summary>
        void Walls()
        {
            var b = new LineMeshBuilder(8192);
            float id = Id(Part.Walls), lit = Lit("army");
            foreach (var w in scene.Walls)
            {
                var (from, to) = WallArc(w.Source);
                float a0 = from * Mathf.Deg2Rad, a1 = to * Mathf.Deg2Rad, thick = WallThickness(w);
                bool judged = !float.IsNaN(w.Height) && !Uncertain(w);
                Color col = HillAdvantageLayer.SourceColor(w.Source);
                for (float d = 0; d <= thick + .01f; d += 1.1f)
                {
                    float rr = scene.WallR + d;
                    int segs = Mathf.Max(4, Mathf.RoundToInt((a1 - a0) * rr / (judged ? 1.2f : 2.4f)));
                    for (int i = 0; i < segs; i++)
                    {
                        float aa = Mathf.Lerp(a0, a1, i / (float)segs), ab = Mathf.Lerp(a0, a1, (i + (judged ? 1f : .5f)) / segs);
                        b.AddSegment(scene.Centre + ScreenDir(aa * Mathf.Rad2Deg) * rr, scene.Centre + ScreenDir(ab * Mathf.Rad2Deg) * rr,
                            WithAlpha(col, (d == 0 ? .95f : .6f) * lit), d == 0 ? 2.4f : 1.6f, 0, id, 1f);
                    }
                }
            }

            Lines(b, "walls", 3194);
        }

        // ------------------------------------------------------------------ what moves: trade, people, command

        /// <summary>
        /// The packets and the trade lanes: for each partner (or the world) goods out and goods in as two slim bands either side
        /// of a path from the country's edge through the shell to its node, each as wide as its dollars, their packets as wide
        /// as the band; the dollars paid for a deficit (or received for a surplus) as a thin lane between them; the deficit as
        /// the hatched gap beside the exports where the lanes cross the shell, as wide as the imports beyond the exports. The
        /// people of each party drift along its stream toward the present, and the chain of command marches from the president
        /// down to the forces.
        /// </summary>
        void Lanes()
        {
            // sized to their content: two bands a lane; three two-point lanes a partner, the people's paths and the command
            var bands = new LineMeshBuilder(scene.Partners.Count * 4 + 4);
            var marks = new MarkMeshes(1303.7f / poseDistance);
            var packets = new PacketMeshBuilder(streamPaths.Values.Sum(path => path.Count) + scene.Partners.Count * 6 + 8);
            float id = Id(Part.Lanes);
            for (int i = 0; i < scene.Partners.Count; i++)
            {
                var p = scene.Partners[i];
                Trade(bands, marks, packets, p.Node, p.Year.Exports, p.Year.Imports, Lit(p.Select), id, (i * .37f) % 1f);
            }

            if (scene.World != null) Trade(bands, marks, packets, scene.WorldNode, scene.World.Exports, scene.World.Imports, Lit("world"), id, 0);
            else if (scene.Partners.Count == 0)
                // trade not measured yet: a faint thread to the world, no flow implied
                bands.AddSegment(scene.Centre + ScreenDir(WorldAngle) * (ShellR - LaneInside), scene.WorldNode - ScreenDir(WorldAngle) * (NodeSize + 1.2f),
                    WithAlpha(Roles.Commerce, .25f), 1.2f, 0, id, 1);

            float streams = Id(Part.Streams);
            foreach (var party in model.Parties)
                if (streamPaths.TryGetValue(party.Id, out var path))
                    packets.AddLane(path, PacketKind.People, WithAlpha(Roles.People, .75f * Lit(party.Id)), PeopleRate, PacketSpeed, 4, streams,
                        seed: Organism.Hash01(party.Id), sizeWorld: .8f);

            if (scene.President != null)
            {
                // command of the forces: from the president down beside the cascade's spine to the shell where the chairman stands
                const float Aside = 6;
                var command = new[] { new Vector3(X0 - Aside, ExecY - ExecR, scene.Z), new Vector3(X0 - Aside, ShellY - Mathf.Sqrt(ShellR * ShellR - Aside * Aside), scene.Z) };
                float lit = Selected == null || Selected == "president" || Selected == "army" || Selected == "defense" || Selected == "joint-chiefs" ? 1 : .35f;
                packets.AddLane(command, PacketKind.Promise, WithAlpha(Color.Lerp(Roles.State, Roles.InkText, .45f), .9f * lit), PromiseRate, PacketSpeed, 6,
                    Id(Part.Army), seed: .37f);
            }

            Lines(bands, "lanes", 3189);
            foreach (var r in marks.Upload(content.transform, "Politics deficit", 3187, owned)) owned.Add(r.gameObject);
            if (packets.VertexCount == 0) return;
            var mesh = packets.ToMesh("Politics packets");
            Material m = GraphMaterials.Raw(GraphMaterials.Packets(Color.white, 1, 3196));
            Own(AddMesh("Politics packets", mesh, m), mesh, m);
        }

        /// <summary>One trade lane (amounts in millions of dollars a year): its two bands and their packets, the money between
        /// them, and the deficit's hatched gap where the lane crosses the shell. Each band keeps a hairline under its width, so
        /// a small flow still shows its lane, and its packets a few pixels, so each direction's goods are seen moving.</summary>
        void Trade(LineMeshBuilder bands, MarkMeshes marks, PacketMeshBuilder packets, Vector3 node, double exports, double imports, float lit, float id, float seed)
        {
            if (double.IsNaN(exports) || double.IsNaN(imports)) return;
            Vector3 c = scene.Centre, dir = (node - c).normalized, across = new Vector3(-dir.y, dir.x, 0);
            Vector3 from = c + dir * (ShellR - LaneInside), to = node - dir * (NodeSize + 1.2f);
            float we = (float)exports * WidthPerMillion, wi = (float)imports * WidthPerMillion;
            Vector3 oe = across * (LaneGap * .5f + we * .5f), oi = -across * (LaneGap * .5f + wi * .5f);
            Color goods = WithAlpha(Roles.Commerce, .9f * lit);
            bands.AddSegment(from + oe, to + oe, WithAlpha(Roles.Commerce, .22f * lit), 1, we, id, 1);
            bands.AddSegment(from + oi, to + oi, WithAlpha(Roles.Commerce, .22f * lit), 1, wi, id, 1);
            packets.AddLane(new[] { from + oe, to + oe }, PacketKind.Goods, goods, GoodsRate, PacketSpeed, 2.5f, id, seed: seed, sizeWorld: we * .85f);
            packets.AddLane(new[] { from + oi, to + oi }, PacketKind.Goods, goods, GoodsRate, PacketSpeed, 2.5f, id, seed: (seed + .5f) % 1f, backward: true, sizeWorld: wi * .85f);
            // the dollars go out for a deficit and come in for a surplus (the money's colour: they return as the partner's claims)
            if (exports != imports)
                packets.AddLane(new[] { from, to }, PacketKind.Money, WithAlpha(Roles.Owners, .85f * lit), MoneyRate, PacketSpeed, 3, id, seed: (seed + .25f) % 1f, backward: exports > imports);
            if (wi <= we) return;
            Vector3 gap = c + dir * (ShellR + scene.UsThick * .5f) + across * (LaneGap * .5f + we + (wi - we) * .5f);
            MarkCode.Add(marks, Mark.Owed, MarkQuad.Rect(gap, dir, across, scene.UsThick + 2, wi - we), WithAlpha(Roles.Commerce, .85f * lit), id);
        }

        // ------------------------------------------------------------------ labels

        /// <summary>A share as the research gives it: "37%", "46.2%".</summary>
        static string Pct(double v) => v.ToString(Math.Abs(v - Math.Round(v)) < .05 ? "0" : "0.0", Ci) + "%";

        /// <summary>Dollars from millions: "$297bn", "$6.0bn", "$6M".</summary>
        static string Money(double millions)
        {
            double a = Math.Abs(millions);
            if (a < 1000) return "$" + a.ToString("0", Ci) + "M";
            double bn = a / 1000;
            return "$" + bn.ToString(bn < 10 ? "0.0" : "0", Ci) + "bn";
        }

        /// <summary>The year a value was measured, as labels give it: " (2024)".</summary>
        static string Measured(float t) => " (" + Mathf.FloorToInt(t).ToString(Ci) + ")";

        /// <summary>A role as a label's text takes it: lightened toward the ink's text, so a dark role (the state's slate) reads
        /// on its plate, and the two parties' names, lightened alike from colours of one lightness, read alike; sRGB-encoded.</summary>
        static Color Ink(Color role) => Roles.Encoded(Color.Lerp(role, Roles.InkText, .45f));

        /// <summary>The selected thing's labels are placed first.</summary>
        static float Sel(string id) => Selected != null && Selected == id ? 400 : 0;

        /// <summary>Places further out, 18 pixels at a time and four at most, up (+1) or down (-1) from a first offset.</summary>
        static Vector2[] Steps(int direction, float from) => Enumerable.Range(1, 4).Select(k => new Vector2(0, direction * (from + 18 * k))).ToArray();

        /// <summary>An office's words: short titles, so a name and its office stay within twelve words.</summary>
        static string OfficeWords(Politics.Office o) => o.Id switch
        {
            "vice-president" => "Vice President", "speaker" => "Speaker of the House", "senate-leader" => "Senate Majority Leader",
            "state" => "Secretary of State", "treasury" => "Treasury Secretary",
            "joint-chiefs" => o.Title != null && o.Title.StartsWith("Chairman", StringComparison.Ordinal) ? "Joint Chiefs chairman" : o.Title,
            _ => o.Title,
        };

        /// <summary>An office's line: its words and its party as the research gives it, an appointee's as the administration it
        /// serves in and a chamber leader's inferred one as the chamber's majority (never naming the person a member of a party
        /// the research only inferred), as the panel words them.</summary>
        string OfficeLine(Seat seat)
        {
            var o = seat.Office;
            string words = OfficeWords(o) + (o.Acting ? ", acting" : "");
            var party = model.PartyById(seat.Party);
            if (o.Military || party == null) return words;
            string name = party.Name ?? party.Short ?? party.Id;
            string adjective = name.EndsWith(" Party", StringComparison.Ordinal) ? name.Substring(0, name.Length - 6) : name;
            if (o.PartyBasis == AppointingBasis) return words + ", in the " + adjective + " administration";
            if (o.PartyBasis == MajorityBasis) return words + ", of the " + adjective + " majority";
            string member = party.Short != null && party.Short.EndsWith("s", StringComparison.Ordinal) ? party.Short.Substring(0, party.Short.Length - 1) : adjective;
            return words + " · " + member;
        }

        /// <summary>The title's year: the bare year when the view follows the chip or a year is pinned at its middle (a cue's
        /// whole year, the panel's steps), as the panel writes it; else the pinned date's month (a key moment's).</summary>
        static string TitleYear()
        {
            float year = Year;
            return float.IsNaN(Pinned) || Mathf.Abs(year - Mathf.Floor(year) - .5f) < 1e-3f ? Mathf.FloorToInt(year).ToString(Ci) : HillHistoryLayer.YearText(year);
        }

        /// <summary>The view's title: the country and the year shown.</summary>
        string TitleText() => model.Name + ", " + TitleYear() + ": who holds power";

        /// <summary>Where the title stands: over the middle, above the walls' reach.</summary>
        static Vector3 TitleAt(Scene s) => new Vector3(s.Centre.x, s.Centre.y + s.Reach + 8, s.Z);

        /// <summary>The view's names: the title, the president with the latest moments beside, the cascade, the parties, the
        /// decades along the presidency's track, then the army's and the trade's.</summary>
        void MakeLabels()
        {
            labels.Clear();
            var s = scene;
            Color plate = Roles.Encoded(Roles.InkBacking);
            labels.Add(new HillLabel
            {
                Text = "<b>" + TitleText() + "</b>", At = TitleAt(s), Role = LabelRole.Title, Part = View + ".bodies", Rank = 500, Plate = plate,
                OnTop = true, Fallbacks = Steps(1, 0),
            });

            if (s.President != null)
            {
                labels.Add(new HillLabel
                {
                    Text = "<b>" + s.President.Name + "</b> <size=80%>President</size>\n<size=86%>" + (model.PartyById(s.President.Party)?.Name ?? "") + "</size>",
                    At = s.Exec + Vector3.up * (ExecR + 3.2f), Role = LabelRole.Name, Part = View + ".bodies", Rank = 480 + Sel("president"),
                    Color = Ink(PartyColor(s.President.Party)), Plate = plate, OnTop = true, Fallbacks = Steps(1, 0),
                });

                // the latest moments, newest first, listed beside the president on the screen's right (-x seen from the future),
                // clear of their sparks just behind it on the track, which recedes to the left
                int k = 0;
                foreach (var (m, _) in s.Moments.Where(x => s.Year - x.moment.Date <= NamedYears).OrderByDescending(x => x.moment.Date).Take(NamedMoments))
                {
                    labels.Add(new HillLabel
                    {
                        Text = "<b>" + Mathf.FloorToInt(m.Date).ToString(Ci) + "</b> · " + m.Title, At = s.Exec + Vector3.left * (ExecR + 4),
                        Role = LabelRole.Note, Part = View + ".marks", Rank = 300 - k, Color = Ink(KindColor(m.Kind)), Plate = plate, OnTop = true,
                        Align = TMPro.TextAlignmentOptions.Left, PixelOffset = new Vector2(0, -17 * k), Fallbacks = Steps(-1, 17 * k),
                    });
                    k++;
                }
            }

            foreach (var seat in s.Offices)
            {
                var o = seat.Office;
                Color colour = o.Vacant ? Roles.InkDim : o.Military ? Color.Lerp(Roles.State, Roles.InkText, .35f) : PartyColor(seat.Party);
                labels.Add(new HillLabel
                {
                    Text = o.Vacant ? "<b>" + OfficeWords(o) + "</b>\n<size=80%>vacant</size>" : "<b>" + o.Name + "</b>\n<size=80%>" + OfficeLine(seat) + "</size>",
                    At = seat.At + Vector3.up * (seat.Below ? -(OfficeR + 3.2f) : OfficeR + 3.2f), Role = LabelRole.Name, Part = View + ".bodies",
                    Rank = 440 - o.Rank + Sel(o.Id), Color = Ink(colour), Plate = plate, OnTop = true, Fallbacks = Steps(seat.Below ? -1 : 1, 0),
                });
            }

            foreach (var h in s.Heads)
            {
                // outside the body, away from the middle: the Democrats' to the screen's left, the Republicans' to its right
                labels.Add(new HillLabel
                {
                    Text = "<b>" + (h.Party.Short ?? h.Party.Name) + "</b> " + Mathf.RoundToInt(h.Power * 100).ToString(Ci) + "% of power\n<size=86%>" + VotesText(h) + "</size>",
                    At = HeadNameAt(h), Role = LabelRole.Name, Part = View + ".bodies", Rank = 460 + Sel(h.Party.Id), Color = Ink(PartyColor(h.Party.Id)),
                    Plate = plate, OnTop = true, Align = h.Party.Side < 0 ? TMPro.TextAlignmentOptions.Right : TMPro.TextAlignmentOptions.Left,
                    Fallbacks = Steps(1, 0),
                });
            }

            // the decades along the presidency's track, so the time axis reads back to the first (every second one over a long
            // span); none near the present, where the president and the latest moments' names stand
            int every = s.Year - model.From > EveryDecadeSpan ? 20 : 10;
            for (int decade = Mathf.CeilToInt(model.From / 10) * 10; decade <= s.Year - DecadeClear; decade += every)
            {
                labels.Add(new HillLabel
                {
                    Text = decade.ToString(Ci), At = Past(X0, ExecY, s.Z, decade), Role = LabelRole.Note, Part = View + ".streams", Rank = 120,
                    Color = Roles.Encoded(Roles.InkDim), Plate = plate, OnTop = true, PixelOffset = new Vector2(0, DecadePx * .7f), Fallbacks = Steps(1, DecadePx * .7f),
                });
            }

            ArmyLabels(s, plate);
            TradeLabels(s, plate);
        }

        /// <summary>A party's votes as its name gives them, with the election they were cast in, or that they were not measured.</summary>
        string VotesText(Head h) => h.Votes > 0 && h.Election != null
            ? (h.Votes / 1e6).ToString("0.0", Ci) + "M " + (h.Election.Presidential ? "" : "House ") + "votes" + Measured(h.Election.Date)
            : "votes not measured" + (model.Elections.Count > 0 ? " before " + Mathf.FloorToInt(model.Elections[0].Date).ToString(Ci) : "");

        /// <summary>Where a party's name stands: beside its head on the side away from the middle (+x is the screen's left seen
        /// from the future), its text running outward from there.</summary>
        static Vector3 HeadNameAt(Head h) => h.At + Vector3.right * (-h.Party.Side * ((h.Shape != null ? h.Shape.MaxRadius : 2.2f) + 4));

        /// <summary>
        /// The essentials the camera fits (<see cref="FlyToState"/>): the walls' reach all round the shell, each party's name
        /// with the room it takes outward, the title above, and the far end of the past (each stream's first ring and the
        /// presidency's, with its decade's mark above). The partners' nodes with their names (or the world's) are held inside
        /// too: on the lower left, nearer the camera than the shell, and named toward the screen's left, they would otherwise
        /// stand on the legend or the preset bar, where the labels drop a name and its balance. The rivals (always within the
        /// Republicans' name's reach) and the walls' names may stand near the free screen's edges.
        /// </summary>
        void Essentials()
        {
            fitAt.Clear();
            fitRoom.Clear();
            for (int i = 0; i < ReachPoints; i++)
            {
                fitAt.Add(scene.Centre + ScreenDir(i * 360f / ReachPoints) * scene.Reach);
                fitRoom.Add(Vector4.zero);
            }

            // a node's name runs to the screen's left of its anchor, which stands beyond the node itself; every partner's seat
            // and the world's are held whichever stand at the year, so the frame keeps still from year to year (the European Union,
            // measured from 1997, swung it 150 pixels)
            foreach (var (_, _, angle) in PartnerSeats)
            {
                fitAt.Add(NodeNameAt(scene.Centre + ScreenDir(angle) * NodeR));
                fitRoom.Add(new Vector4(PartnerNamePx, 0, NodeHalfPx, NodeHalfPx));
            }

            fitAt.Add(NodeNameAt(scene.WorldNode));
            fitRoom.Add(new Vector4(WorldNamePx, 0, WorldHalfPx, WorldHalfPx));

            foreach (var h in scene.Heads)
            {
                fitAt.Add(HeadNameAt(h));
                fitRoom.Add(h.Party.Side < 0 ? new Vector4(PartyNamePx, 0, NameHalfPx, NameHalfPx) : new Vector4(0, PartyNamePx, NameHalfPx, NameHalfPx));
                fitAt.Add(Past(X0 - h.Party.Side * PartyX, Floor + PowerHeight * EasedPower(h.Party.Id, model.From), scene.Z, model.From));
                fitRoom.Add(Vector4.zero);
            }

            // the title: its characters at about half an em each at the title role's 18 pixels, half each side
            float half = TitleText().Length * .55f * 18 * .5f;
            fitAt.Add(TitleAt(scene));
            fitRoom.Add(new Vector4(half, half, TitleHalfPx, TitleHalfPx));
            fitAt.Add(Past(X0, ExecY, scene.Z, model.From));
            fitRoom.Add(new Vector4(DecadePx, 0, 0, DecadePx * 1.4f));
        }

        /// <summary>The forces' and NATO's shares on the shell, the rivals' outside theirs, and each wall's name and reading.</summary>
        void ArmyLabels(Scene s, Color plate)
        {
            Color slate = Ink(Roles.State);
            labels.Add(new HillLabel
            {
                Text = "<b>" + model.Name + " forces</b>\n<size=86%>" + (double.IsNaN(s.UsShare) ? "share of world military spending not measured"
                    : Pct(s.UsPoint.V) + " of world military spending" + Measured(s.UsPoint.T)) + "</size>",
                At = s.Centre + ScreenDir(ShellLabelAngle) * (ShellR + s.UsThick * .5f), Role = LabelRole.Name, Part = View + ".army", Rank = 470 + Sel("army"),
                Color = slate, Plate = plate, OnTop = true, Fallbacks = Steps(-1, 0),
            });
            if (s.NatoMembers > 0)
                labels.Add(new HillLabel
                {
                    Text = "<b>NATO</b>, " + s.NatoMembers.ToString(Ci) + " members\n<size=86%>" + (double.IsNaN(s.NatoShare) ? "share not measured"
                        : Pct(s.NatoPoint.V) + " of world military spending" + Measured(s.NatoPoint.T)) + "</size>",
                    At = s.Centre + ScreenDir(NatoLabelAngle) * (ShellR + s.UsThick + s.NatoThick * .5f), Role = LabelRole.Note, Part = View + ".army",
                    Rank = 330 + Sel("army"), Color = slate, Plate = plate, OnTop = true, Fallbacks = Steps(1, 0),
                });

            foreach (var r in s.Rivals)
            {
                string share = double.IsNaN(r.Share) ? "share not measured" : Pct(r.Point.V) + Measured(r.Point.T);
                string pact = !r.PactStands ? "" : "\n<size=80%>" + r.PactName + " " + (double.IsNaN(r.Pact) ? "not measured" : Pct(r.PactPoint.V) + Measured(r.PactPoint.T)) + "</size>";
                // outside the rival, above the upper one and under the lower, clear of the parties' names between them
                int side = r.At.y > s.Centre.y ? 1 : -1;
                labels.Add(new HillLabel
                {
                    Text = "<b>" + r.Name + "</b> " + share + pact, At = r.At + Vector3.up * (side * (RivalCore + r.Thick + r.PactThick + 3.5f)),
                    Role = LabelRole.Note, Part = View + ".army", Rank = 320 + Sel(r.Id), Color = slate, Plate = plate, OnTop = true, Fallbacks = Steps(side, 0),
                });
            }

            foreach (var w in s.Walls)
            {
                var (from, to) = WallArc(w.Source);
                float mid = (from + to) * .5f;
                Vector3 outward = ScreenDir(mid);
                // Jev's reading names the source and gives its wall ("Command of the sea: wall 3.2 of 4, uncertain"), and the
                // benchmark it was judged at stands after it, as every other number's year does: the nearest benchmark can be
                // two and a half years from the year shown
                int colon = w.Reading != null ? w.Reading.IndexOf(": ", StringComparison.Ordinal) : -1;
                string reading = (colon >= 0 ? w.Reading.Substring(colon + 2)
                    : float.IsNaN(w.Height) ? "not judged" : "wall " + w.Height.ToString("0.0", Ci) + " of 4" + (Uncertain(w) ? ", uncertain" : "")) +
                    (float.IsNaN(w.T) ? "" : Measured(w.T));
                var step = new Vector2(-outward.x, outward.y);
                labels.Add(new HillLabel
                {
                    Text = "<b>" + HillHistoryLayer.SourceWord(w.Source) + "</b>\n<size=80%>" + reading + "</size>",
                    At = s.Centre + outward * (s.WallR + WallThickness(w) + 4), Role = LabelRole.Note, Part = View + ".walls",
                    Rank = 250 + (float.IsNaN(w.Height) ? 0 : w.Height) + Sel("army"), Color = Roles.Encoded(HillAdvantageLayer.SourceColor(w.Source)), Plate = plate,
                    OnTop = true, Fallbacks = new[] { step * 16, step * 32, step * 48 },
                });
            }
        }

        /// <summary>Each partner's balance with its year, or the world's before partners are measured, or that trade is not.</summary>
        void TradeLabels(Scene s, Color plate)
        {
            Color ink = Ink(Roles.Commerce);
            foreach (var p in s.Partners)
                labels.Add(NodeLabel(p.Node, "<b>" + p.Name + "</b>\n<size=86%>US " + Balance(p.Year.Balance) + Measured(p.Year.T) + "</size>", ink, plate,
                    300 + Sel(p.Select)));

            if (s.Partners.Count > 0) return;
            float byPartner = model.Partners.Values.Where(rows => rows.Count > 0).Select(rows => rows[0].T).DefaultIfEmpty(float.NaN).Min();
            // the lane is every partner together, and says from when the partners are measured one by one
            string later = float.IsNaN(byPartner) ? "" : "\nby partner from " + Mathf.FloorToInt(byPartner).ToString(Ci);
            string text = s.World != null
                ? "<b>The world</b>\n<size=86%>US " + Balance(s.World.Balance) + Measured(s.World.T) + later + "</size>"
                : "<b>Trade</b>\n<size=86%>not measured" + (model.Trade.Count > 0 && s.Year < model.Trade[0].T ? " before " + Mathf.FloorToInt(model.Trade[0].T).ToString(Ci) : "") +
                  "</size>";
            labels.Add(NodeLabel(s.WorldNode, text, ink, plate, 300 + Sel("world")));
        }

        /// <summary>A balance in millions of dollars as the labels give it: "deficit $297bn", "surplus $3.5bn", "balanced", or
        /// "balance not measured" where the research holds none.</summary>
        static string Balance(double millions) =>
            double.IsNaN(millions) ? "balance not measured" : millions < 0 ? "deficit " + Money(millions) : millions > 0 ? "surplus " + Money(millions) : "balanced";

        /// <summary>Where a trade node's name stands: beside it toward the screen's left (+x seen from the future), its text
        /// running outward from there; every node stands on the lower left (<see cref="PartnerSeats"/>, <see cref="WorldAngle"/>).</summary>
        static Vector3 NodeNameAt(Vector3 node) => node + Vector3.right * (NodeSize + 3);

        /// <summary>A trade node's name, beside it toward the screen's left (<see cref="NodeNameAt"/>).</summary>
        static HillLabel NodeLabel(Vector3 node, string text, Color colour, Color plate, float rank) => new HillLabel
        {
            Text = text, At = NodeNameAt(node), Role = LabelRole.Note, Part = View + ".lanes", Rank = rank, Color = colour, Plate = plate, OnTop = true,
            Align = TMPro.TextAlignmentOptions.Right, Fallbacks = Steps(-1, 0),
        };

        /// <summary>Puts the view's labels on screen or takes them off, once per change.</summary>
        void ShowLabels(bool show)
        {
            if (show == labelsShown) return;
            labelsShown = show;
            if (show) HillLabels.Set(this, labels);
            else HillLabels.Clear(this);
        }

        static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }

        // ------------------------------------------------------------------ the camera and the portraits

        /// <summary>
        /// Flies to the cross-section at the year shown, built first if it changed, so the pose fits it: from the future in the
        /// three-quarter view, the essentials (<see cref="Essentials"/>: the shell with its walls, the parties and the trade
        /// partners with their names, the title and the far end of the past) as large as they go in the free screen, the rivals
        /// near its edges. The free screen is the largest rect the HUD's blocks leave (the labels' keep-outs, the panel among them, so a
        /// portrait sheet's height counts) and, on a landscape screen, the portrait cards' column, below the blocks standing
        /// against the screen's top and above those against its bottom (<see cref="LargestFree"/>). On a portrait screen the
        /// view takes the screen first, and the president's card shows only where it then covers none of the essentials. A
        /// refit for the HUD's blocks flies only when the pose it finds moved: a panel that grew without narrowing the free
        /// screen neither restarts a flight nor takes back a camera the viewer turned.
        /// </summary>
        void FlyToState(float seconds, bool refit = false)
        {
            GraphRoot root = GraphRoot.Instance;
            model = Politics.Get();
            if (root == null || root.Rig == null || root.Rig.Cam == null || model == null || LandView.PresetId != View) return;
            EnsureContent();
            float year = Year;
            if (Stale(year)) Rebuild(year);
            fittedHud = MeasureHud();
            Axes(root.Rig.Cam);
            bool portrait = ScreenLayout.IsPortrait;
            int wanted = scene.President == null ? 0 : portrait ? 1 : Mathf.Min(LandscapeCards, 1 + scene.Offices.Count(x => !x.Office.Vacant));
            shownCards = portrait ? 0 : wanted;
            obstacles.Clear();
            obstacles.AddRange(hudRects);
            if (shownCards > 0) obstacles.Add(CardsRect(shownCards));
            // the essentials' shape from a first fit to the whole screen (the world's part of their box and their names'
            // pixels) chooses the free rect they stand largest in; the second fit puts them there
            Vector3 target = scene.Centre;
            float d = poseDistance, m = ScreenMargin * ScreenLayout.UiScale;
            Solve(Rect.MinMaxRect(m, m, screenW - m, screenH - m), ref target, ref d);
            Measure(target, d, out _, out Vector4 at, out Vector4 room);
            Rect free = LargestFree(Mathf.Max(at.y - at.x, 1), room.x + room.y, Mathf.Max(at.w - at.z, 1), room.z + room.w);
            fittedBox = Solve(free, ref target, ref d);
            if (portrait && wanted > 0 && !CardsRect(1).Overlaps(fittedBox)) shownCards = 1;
            poseDistance = d;
            var p = HillPresets.PoliticsPose(target, Yaw, Pitch, d);
            // the fitted distance stands on a portrait screen too: the preset's portrait width would step it back
            p.PortraitWidth = 0;
            CameraPose pose = p.Pose();
            if (!refit || (pose.Target - flown.Target).sqrMagnitude > .25f || Mathf.Abs(pose.Distance - flown.Distance) > .5f)
            {
                flown = pose;
                root.Rig.FlyTo(pose, seconds);
            }

            ShowPopups();
        }

        /// <summary>The camera's axes at the view's pose in the content's frame (the land's, which a pose's yaw turns from), its
        /// half-angle tangents and its pixels.</summary>
        void Axes(Camera cam)
        {
            Quaternion q = Quaternion.Euler(Pitch, Yaw, 0);
            camRight = q * Vector3.right;
            camUp = q * Vector3.up;
            camFwd = q * Vector3.forward;
            screenW = cam.pixelWidth;
            screenH = cam.pixelHeight;
            tanV = Mathf.Tan(cam.fieldOfView * .5f * Mathf.Deg2Rad);
            tanH = tanV * screenW / Mathf.Max(screenH, 1);
        }

        /// <summary>
        /// Moves the target and the distance until the essentials' box fills a screen rect on its tighter side and stands at its
        /// middle: each pass scales the distance by how far the world's part of the box (its names' pixels kept as they are)
        /// misses the rect, then centres the box in it. Returns the box on screen (pixels, origin bottom-left).
        /// </summary>
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

        /// <summary>The essentials' box on screen (pixels, origin bottom-left) with the camera at a target and a distance: its
        /// edges (left, right, bottom, top), the screen place of the essential at each edge, and the room its name takes
        /// beyond it there at the screen's label scale.</summary>
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

        /// <summary>
        /// The free screen: of the rects whose edges are the screen's margins or the obstacles' edges (a gap away), between the
        /// blocks standing against the screen's top and its bottom (<see cref="EdgeBand"/>: the view never climbs beside the
        /// title) and clear of every obstacle, the one in which the essentials stand largest (their names' pixels taken off its
        /// width and height before the world's part of their box is set against them), and of equals the largest; the whole
        /// screen between the margins and those bands when none is clear. The candidates are a few dozen edges each way.
        /// </summary>
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
                    // a rect this narrow cannot beat the best, however tall
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

        /// <summary>Whether a rect overlaps an obstacle.</summary>
        bool Blocked(float x0, float y0, float x1, float y1)
        {
            foreach (Rect r in obstacles)
                if (r.xMin < x1 && r.xMax > x0 && r.yMin < y1 && r.yMax > y0)
                    return true;
            return false;
        }

        /// <summary>The portrait cards' column on screen (pixels, origin bottom-left) for a number of cards, as
        /// <see cref="HistoryPopups"/> stands them on a canvas scaled as every HUD canvas is.</summary>
        Rect CardsRect(int cards)
        {
            float s = UiFactory.CanvasScale, top = screenH - CardTop * s;
            return Rect.MinMaxRect(CardLeft * s, top - (cards * (CardHeight + CardGap) - CardGap) * s, (CardLeft + CardWidth) * s, top);
        }

        /// <summary>Measures the HUD's blocks shown now as the labels keep out of them (<see cref="LabelSystem.HudRects"/>: the
        /// title, the year chip, the legend, the preset bar, the panels and the tour's card, in screen pixels) and returns a
        /// signature that changes when one moves by a few pixels, comes or goes, or the screen changes. Allocates nothing.</summary>
        int MeasureHud()
        {
            hudRects.Clear();
            GraphRoot root = GraphRoot.Instance;
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

        /// <summary>The portrait cards the fit left room for (<see cref="shownCards"/>): the president, then the most senior
        /// holders below (the selected office first), from the portraits' atlas by <see cref="Politics.ImageId"/> (a monogram
        /// where none is packed).</summary>
        void ShowPopups()
        {
            if (scene == null || model == null || shownCards == 0)
            {
                HistoryPopups.Clear();
                return;
            }

            var items = new List<(string, string, string)>(shownCards);
            if (scene.President != null)
                items.Add((Politics.ImageId(scene.President.Name), scene.President.Name, "President · " + (model.PartyById(scene.President.Party)?.Name ?? "")));
            foreach (var seat in scene.Offices.Where(x => !x.Office.Vacant).OrderBy(x => x.Office.Id == Selected ? 0 : 1).ThenBy(x => x.Office.Rank).Take(shownCards - items.Count))
                items.Add((Politics.ImageId(seat.Office.Name), seat.Office.Name, OfficeLine(seat)));
            HistoryPopups.Show(items);
        }

        // ------------------------------------------------------------------ smoke

        /// <summary><c>-smokeextra politics</c>: the view at five years from Truman's second term to now, each captured, with an
        /// error for a year that draws no executive, no streams or no presidency's track, or whose fitted essentials run off the
        /// screen; each logs the camera's fit (the free screen, the distance and the portrait cards beside it).</summary>
        IEnumerator Smoke()
        {
            GraphRoot root = GraphRoot.Instance;
            if (root == null) yield break;
            root.Focus(View, 0f);
            for (float w = 0; w < 10 && Politics.Get() == null; w += Time.unscaledDeltaTime) yield return null;
            var m = Politics.Get();
            if (m == null)
            {
                Debug.LogError("[Why] politics: no politics.json");
                yield break;
            }

            Debug.Log($"[Why] politics: {m.Presidents.Count} presidents, {m.House.Count} House and {m.Senate.Count} Senate rows, {m.Elections.Count} elections, " +
                      $"{m.Offices.Count} offices, {m.Moments.Count} moments, {m.Walls.Count} walls, {m.Partners.Count} partners, {m.Trade.Count} trade years");
            foreach (string state in SmokeStates)
            {
                ShowState(state);
                yield return new WaitForSecondsRealtime(3.2f);
                Debug.Log($"[Why] politics {state}: {bodies.Count} bodies, {streamRings} stream rings, {trackTerms} presidential terms, " +
                          $"{scene?.Moments.Count ?? 0} moments, {scene?.Walls.Count ?? 0} walls, {scene?.Partners.Count ?? 0} partners, {labels.Count} labels; " +
                          $"fit {fittedBox.xMin:0}-{fittedBox.xMax:0} x {fittedBox.yMin:0}-{fittedBox.yMax:0} px of {screenW:0}x{screenH:0}, distance {poseDistance:0}, {shownCards} cards");
                if (!executiveDrawn) Debug.LogError($"[Why] politics: {state} draws no executive");
                if (streamRings == 0) Debug.LogError($"[Why] politics: {state} draws no streams");
                if (trackTerms == 0) Debug.LogError($"[Why] politics: {state} draws no presidency's track");
                if (fittedBox.xMin < 0 || fittedBox.yMin < 0 || fittedBox.xMax > screenW || fittedBox.yMax > screenH)
                    Debug.LogError($"[Why] politics: {state}'s essentials run off the screen ({fittedBox})");
                yield return SmokeChecks.Capture("economy-" + state.Replace(':', '-').Replace('.', '_'), root.CurrentPreset);
            }

            ShowState(null);
        }
    }
}
