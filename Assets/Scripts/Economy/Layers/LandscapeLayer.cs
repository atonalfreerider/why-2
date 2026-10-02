using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using Why.Economy.Data;
using Why.Economy.Land;
using Why.Economy.Model;
using Debug = UnityEngine.Debug;
using Tier = Why.Economy.Land.Tier;

namespace Why.Economy.Layers
{
    /// <summary>
    /// The land itself (SPEC 2.1-2.8): the stepped bowl's five terraces (treads, risers, edges, the outer wall, the
    /// rim), every industry's sector split into wages, upkeep and the owners' gold, the pools of what households pay
    /// each industry directly, the roots beneath the terraces (purchases between industries rising into each buyer from
    /// below, the mesh roots of the smaller suppliers, the root balls of own-industry purchases), the corporations'
    /// towers with their plinths and foot rings, the crown ring over the bowl's center, and TECH's two overlays (the AI
    /// scaffold, the ads halo); their labels, anchors (land:tier:, land:sector:, land:pool:, land:root:, land:tower:,
    /// land:crown, land:ai, land:ads) and highlight ids (<see cref="EconomyIds"/> land ranges).
    ///
    /// Everything is built land-local from the snapshot on screen (<see cref="LandService"/>) and drawn with raw
    /// materials on objects placed in the land's frame (<see cref="EconomyStage.Land"/>), one renderer per emphasis
    /// group and shader (Terraces, Sectors, Pools, Roots, Towers, Crown, Overlays), whose alpha follows
    /// <see cref="LandView.Alpha"/>: no layer but the section layer morphs, so the land fades in with the reveal. A new
    /// year (8.3) rebuilds the builders on a worker (a preset's blocking snapshot too, waited for in the next Tick), uploads
    /// them in Tick, cross-fades from the old meshes over <see cref="LandStyle.CrossFadeSeconds"/> and reports the swap
    /// (<see cref="LandService.ReportReady"/>); a new snapshot of the same land (a new season) only reports. A build runs
    /// in four independent parts (each into its own builders and item lists, joined in a fixed order). The five tier
    /// labels stand on the outer wall (moved along it when their place is out of frame, spread where their bands project
    /// closer than a label), and the land places its labels itself where the label system will keep them: each sector's
    /// label aligned away from the bowl's center and moved or shortened until it is clear (<see cref="PlaceLabels"/>).
    /// Order 50: tier 5, after the land's model (45).
    /// </summary>
    [GraphScenes(GraphScene.Economy)]
    public sealed class LandscapeLayer : GraphLayer
    {
        public override int Order => 50;

        /// <summary>The name this layer reports its swaps under.</summary>
        const string ReadyName = "LandscapeLayer";

        // ------------------------------------------------------------------ groups and looks

        /// <summary>The emphasis groups this layer draws (one fill and one line renderer each, when not empty).</summary>
        static readonly LandGroup[] Groups =
            { LandGroup.Terraces, LandGroup.Sectors, LandGroup.Pools, LandGroup.Roots, LandGroup.Towers, LandGroup.Crown, LandGroup.Overlays };

        const int GTerraces = 0, GSectors = 1, GPools = 2, GRoots = 3, GTowers = 4, GCrown = 5, GOverlays = 6;

        /// <summary>Render queue of each group: the roots under the bowl under the pools, then towers, the crown and overlays.</summary>
        static readonly int[] Queues =
        {
            LandStyle.QueueTerraces, LandStyle.QueueSectors, LandStyle.QueuePools, LandStyle.QueueRoots, LandStyle.QueueTowers,
            LandStyle.QueueTowers + 1, LandStyle.QueueTowers + 1
        };

        /// <summary>HDR intensity of each tier's tread, riser and owners' strip: tech, the highest terrace, brightest.</summary>
        static readonly float[] TierIntensity = { 1f, 1f, 1f, 1.1f, 1.35f };

        /// <summary>Angular step of the sectors' strips, edges and pools (degrees; the treads use 96 segments a turn).</summary>
        const float SectorStep = 2.5f;

        /// <summary>Lift of the strips above their tread (they lie on it), of a strip edge, of the tower foot rings.</summary>
        const float StripLift = 0.003f, EdgeLift = 0.005f, FootLift = 0.004f;

        /// <summary>A sector's outline and its two strip boundaries (alpha), every tread's center line under the strips.</summary>
        const float SectorEdgeAlpha = 0.55f, StripEdgeAlpha = 0.3f;

        /// <summary>The rim's edge lines (people blue).</summary>
        const float RimEdgeAlpha = 0.35f;

        /// <summary>Mesh roots (the smaller suppliers): a dim steel; root balls: their industry's color.</summary>
        const float MeshRootAlpha = 0.15f;

        /// <summary>
        /// Root balls (own-industry purchases) at this share of the roots' alpha (WP6): drawn 4x as wide as a river, the
        /// largest ($1.6T, manufacturing) made a bright white band under the bowl that buried the roots between industries
        /// and the six root labels; the loops stay, quieter.
        /// </summary>
        const float RootBallShare = 0.35f;

        /// <summary>An overflowing pool's side wall, a pool's shoreline alpha.</summary>
        const float PoolWallAlpha = 0.10f, ShoreAlpha = 0.9f;

        /// <summary>Towers: edge and top-square alpha, the foot ring's alpha and segments, the private outline's alpha.</summary>
        const float TowerEdgeAlpha = 0.75f, TowerTopAlpha = 0.35f, FootAlpha = 0.5f, PrivateAlpha = 0.6f;

        const int FootSegments = 48;

        /// <summary>The crown ring's alpha.</summary>
        const float CrownAlpha = 0.9f;

        /// <summary>The AI scaffold: lattice spacing, dash on / off (world), its alpha, the plan's alpha, the feed arcs' lift and alpha.</summary>
        const float AiLattice = 0.2f, DashOn = 0.05f, DashOff = 0.035f, AiAlpha = 0.6f, AiPlanAlpha = 0.3f, AiFeedLift = 0.6f,
            AiFeedAlpha = 0.5f;

        /// <summary>The ads halo: the whole arc's alpha, the social arc's intensity.</summary>
        const float AdsAlpha = 0.45f, AdsSocialIntensity = 2.2f;

        static readonly Color Steel = EconomyStyle.Government, ScaffoldWhite = new Color(0.86f, 0.92f, 1f);

        /// <summary>
        /// The roots blend over each other (alpha) rather than adding light (WP6 review): dozens of wide roots overlap under
        /// the services ring, and added up they made a white band that buried the root labels; blended, the band keeps its
        /// tier's hue.
        /// </summary>
        const bool RootsAdditive = false;

        /// <summary>Flow pulses along the roots, toward the buyer (as the money threads pulsed).</summary>
        const float RootFlowFreq = 4f, RootFlowSpeed = 0.8f;

        // ------------------------------------------------------------------ labels

        /// <summary>Label sizes (px at 1080p): tiers, sectors, the rest.</summary>
        const float TierLabelPx = 13, SectorLabelPx = 11, LabelPx = 11;

        /// <summary>Label priorities (7.3).</summary>
        const float TierPriority = 40, PoolPriority = 14, CrownPriority = 30, RootPriority = 24, OverlayPriority = 20;

        /// <summary>
        /// Least screen gap between the tier labels' rects (px): each label stands at its band's middle on the wall, and
        /// where the bands project closer than a label's height (a far view) the stack is spread just enough, least squares
        /// around their own places (<see cref="SpreadTierLabels"/>).
        /// </summary>
        const float TierLabelGapPx = 1;

        /// <summary>
        /// Where on the outer wall the tier labels' stack may stand (degrees), in order of preference: the near wall at
        /// <see cref="LandStyle.TierLabelThetaDeg"/> (7.3) and beside it, then the inside of the far wall on the same side,
        /// where the close, raised views (capture) still see the wall's five bands. The first place whose whole stack is in
        /// frame and clear of the crown's label is used.
        /// </summary>
        static readonly float[] TierLabelThetas = { LandStyle.TierLabelThetaDeg, 270f, 230f, 120f, 130f, 140f, 150f, 110f };

        /// <summary>
        /// The overview's places for the tier stack (WP5): it sees the bowl from the road's side, where the near wall at
        /// 250° stands beside the cut and the road's own labels (the wall's tiers, "Now") would win its rects; the stack
        /// stands on the side of the bowl turned away from the road instead (20°-60°, the right of the overview's frame).
        /// </summary>
        static readonly float[] OverviewTierLabelThetas = { 40f, 30f, 50f, 60f, 20f, 320f, LandStyle.TierLabelThetaDeg };

        /// <summary>
        /// The label system's measure of a plain text, mirrored for the land's own placement (LabelSystem.EstimateWidth: em
        /// per space, capital, digit and other character, plus a margin) and its line height (em).
        /// </summary>
        const float EmSpace = 0.28f, EmUpper = 0.64f, EmDigit = 0.55f, EmOther = 0.5f, EmMargin = 0.2f, LineEm = 1.15f;

        /// <summary>Labels the land places itself keep this far inside the frame (px).</summary>
        const float FrameMarginPx = 6f;

        /// <summary>
        /// The places a sector's label may take, tried in order (7.3, 9 WP1): in units of the label's padded height, along
        /// the sector's screen radial (outward positive, from the sector's outer edge at its center angle) and up or down.
        /// The label is aligned away from the bowl's center (left of a sector on the left, right on the right, centered at
        /// the near and far side), so neighbours across the ring never overlap and a crowded ring's labels step outward.
        /// </summary>
        static Vector2[] sectorCandidates;

        /// <summary>
        /// The candidates (made on first use, on the main thread): outward steps of half a label height up to
        /// <see cref="CandidateOut"/>, one half step inward (onto the sector), and up to <see cref="CandidateRows"/> rows up
        /// or down, nearest first (a row and an inward step weigh more than an outward step; equal costs keep their order).
        /// </summary>
        static Vector2[] SectorCandidates => sectorCandidates ??= Candidates(CandidateOut, CandidateRows);

        /// <summary>
        /// The second, wider candidate set (WP6): outward up to <see cref="WideOut"/> half heights and up to
        /// <see cref="WideRows"/> rows, tried with a sector's shortest text when no place of the first set is clear (a
        /// phone's narrow frame left one sector of 25 unlabeled in the roots, capture and landscape views).
        /// </summary>
        static Vector2[] WideCandidates => wideCandidates ??= Candidates(WideOut, WideRows);

        /// <summary>The wide candidate set once made (see <see cref="WideCandidates"/>).</summary>
        static Vector2[] wideCandidates;

        /// <summary>How far the wide set steps outward (label heights) and how many rows up or down.</summary>
        const int WideOut = 9, WideRows = 7;

        /// <summary>A candidate set, nearest first (see <see cref="SectorCandidates"/>).</summary>
        static Vector2[] Candidates(int outward, int rows)
        {
            int n = (2 * outward + 2) * (2 * rows + 1), k = 0;
            Vector2[] at = new Vector2[n];
            float[] cost = new float[n];
            for (int a = -1; a <= 2 * outward; a++)
            {
                for (int b = -rows; b <= rows; b++, k++)
                {
                    // insertion sort, stable: a later candidate goes after every earlier one of equal cost
                    float c = (a < 0 ? 1.5f : 0.5f) * Math.Abs(a) + 1.2f * Math.Abs(b);
                    int i = k;
                    for (; i > 0 && cost[i - 1] > c; i--)
                    {
                        cost[i] = cost[i - 1];
                        at[i] = at[i - 1];
                    }

                    cost[i] = c;
                    at[i] = new Vector2(0.5f * a, b);
                }
            }

            return at;
        }

        /// <summary>How far a placed label may step outward (label heights) and how many rows up or down.</summary>
        const int CandidateOut = 4, CandidateRows = 4;

        /// <summary>
        /// The label system's width estimate runs short of the drawn text (by about a tenth); the land's own placement
        /// allows this much more on the side the text grows to, in its frame checks and between the labels it places.
        /// </summary>
        const float FrameWidthSlack = 0.15f;

        /// <summary>
        /// A root's label is placed like a sector's along the screen direction from this far above its lowest point down to
        /// it (land units): centered under the root's low point, moved down or up where the six would overlap.
        /// </summary>
        const float RootLabelUp = 0.3f;

        /// <summary>
        /// A root's label hangs this far below the root's lowest point (land units; WP6 review): at the point itself it stood
        /// on the band the overlapping roots under the services ring make, and three of the six did not read.
        /// </summary>
        const float RootLabelDrop = 0.4f;

        /// <summary>A sector's screen radial nearer to horizontal than this (|x| of the unit vector) aligns its label sideways.</summary>
        const float SideAlign = 0.45f;

        /// <summary>The tier labels' current place on the wall (degrees).</summary>
        float tierTheta = LandStyle.TierLabelThetaDeg;

        /// <summary>Tower labels stand this far above the top; the crown's above the ring.</summary>
        const float TowerLabelLift = 0.08f, CrownLabelLift = 0.12f;

        /// <summary>Overlay ids (<see cref="EconomyIds.LandOverlay"/>).</summary>
        const int OverlayAi = 0, OverlayAds = 1;

        /// <summary>The industries' short names in labels (the data's names are long); others use the data's name.</summary>
        static readonly Dictionary<string, string> ShortNames = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "federal", "Federal" }, { "state_local", "State & local" }, { "agriculture", "Agriculture" },
            { "oil_gas", "Oil & gas" }, { "mining_metals", "Mining & metals" }, { "utilities", "Utilities" },
            { "construction", "Construction" }, { "manufacturing", "Manufacturing" }, { "transport", "Transport" },
            { "trade", "Trade" }, { "banking", "Banking" }, { "finance", "Finance" }, { "insurance", "Insurance" },
            { "real_estate", "Real estate" }, { "health", "Health" }, { "legal", "Legal" },
            { "professional", "Professional services" }, { "education", "Education" }, { "entertainment", "Entertainment" },
            { "hospitality", "Restaurants & hotels" }, { "other_services", "Other services" }, { "hardware", "Computers & chips" },
            { "software", "Software" }, { "internet", "Internet & cloud" }, { "media_telecom", "Media & telecom" }
        };

        // ------------------------------------------------------------------ state

        EconomyModel model;
        LabelSystem labels;
        double nowYear;

        /// <summary>Built in Prepare from the first snapshot, uploaded in Upload.</summary>
        Content prepared;

        /// <summary>The renderers on screen, and the ones fading out after a year change.</summary>
        Shown current, old;

        /// <summary>The cross-fade's progress (0..1) while <see cref="old"/> fades out.</summary>
        float cross = 1;

        /// <summary>A published snapshot waiting to be built, and the build running on a worker.</summary>
        LandSnapshot wanted;

        Task<Content> building;

        /// <summary>The worker build of a blocking snapshot, started when it was published (waited for in the next Tick).</summary>
        Task<Content> blockingBuild;

        LandSnapshot blockingFor;

        /// <summary>The snapshot version on screen (a worker build of an older one is discarded) and the snapshot it was built from.</summary>
        int shownVersion = -1;

        LandSnapshot shownSnapshot;

        /// <summary>Label slots by anchor key (created once; their text and place follow the year), and the anchors.</summary>
        readonly Dictionary<string, Slot> slots = new Dictionary<string, Slot>(StringComparer.Ordinal);

        readonly List<Slot> slotList = new List<Slot>();
        readonly Dictionary<string, Anchor> anchors = new Dictionary<string, Anchor>(StringComparer.Ordinal);
        int seenLabels = -1, seenTower = -2;

        /// <summary>
        /// The land's labels in the label system's order (priority, then rank; <see cref="PlaceLabels"/> replays its greedy),
        /// re-sorted when a label is added; the rects kept so far in a replay; the tier slots by ring.
        /// </summary>
        readonly List<Slot> placeOrder = new List<Slot>();

        readonly List<Rect> placedRects = new List<Rect>();

        /// <summary>The same labels' rects as drawn (widened by <see cref="FrameWidthSlack"/>): a placed label keeps clear of these.</summary>
        readonly List<Rect> drawnRects = new List<Rect>();
        readonly Slot[] tierSlots = new Slot[LandStyle.TerraceY.Length];
        bool placeOrderDirty, placeDirty = true;

        /// <summary>The camera version and screen size the labels were last placed for.</summary>
        int placedCam = -1, placedW, placedH;

        /// <summary>The tier stack's spread (per ring: the shift up and sideways in px and the rect), its scratch (by shown label) and the crown's rect.</summary>
        readonly float[] tierShift = new float[LandStyle.TerraceY.Length], tierShiftX = new float[LandStyle.TerraceY.Length], spreadX = new float[LandStyle.TerraceY.Length],
            spreadY = new float[LandStyle.TerraceY.Length], blockValue = new float[LandStyle.TerraceY.Length];

        readonly int[] spreadIndex = new int[LandStyle.TerraceY.Length], blockCount = new int[LandStyle.TerraceY.Length];
        readonly Rect[] tierRects = new Rect[LandStyle.TerraceY.Length];
        Rect tierCrown;

        /// <summary>One labeled or anchored object of a build: its anchor and, when labeled, its label's text and place.</summary>
        sealed class Item
        {
            public string Key, Name, Blurb, Text;
            public LandGroup Group;
            public bool Present = true;
            public Vector3 Label, Anchor;            // land-local
            public Vector3 Inner;                    // a placed label's direction starts here (sectors: the inner edge at the center angle)
            public string[] Variants;                // sectors, roots: the label's texts, longest first (placed by the layer)
            public Vector2 Offset;                   // the label's screen offset (px)
            public int TierStack = -1;               // tiers: the ring whose label this is (its place follows the stack's)
            public IdRange Ids = IdRange.Empty;
            public float Priority, Rank, SizePx = LabelPx;   // Rank breaks equal priorities (higher first: the larger object)
            public Color Color = GraphStyle.Text;
            public int Tower = -1, AnchorTier = 2;
        }

        /// <summary>A label of this layer: its spec, its group, whether the year shows its object, its tower (selection).</summary>
        sealed class Slot
        {
            public LabelSpec Spec;
            public LandGroup Group;
            public bool Present;
            public int Tower = -1;

            /// <summary>A tier label's ring (its place on the wall follows the stack's, <see cref="TierLabelAt"/>); -1 otherwise.</summary>
            public int TierStack = -1;

            /// <summary>The label's own screen offset (px; the placement adds its shift to it).</summary>
            public Vector2 Offset;

            /// <summary>
            /// A label the layer places (<see cref="PlaceSector"/>: sectors and roots): its texts (longest first), the one
            /// shown, and the world point its direction starts at (a sector's inner edge; above a root's lowest point).
            /// </summary>
            public string[] Variants;

            public int Variant;
            public Vector3 Inner;

            /// <summary>The slot's place in <see cref="slotList"/> (the placement order's last tie-break).</summary>
            public int Index;
        }

        /// <summary>One snapshot's geometry and texts (built on any thread).</summary>
        sealed class Content
        {
            public LandSnapshot Snapshot;
            public readonly SurfaceMeshBuilder[] Fills = new SurfaceMeshBuilder[Groups.Length];
            public readonly LineMeshBuilder[] Lines = new LineMeshBuilder[Groups.Length];
            public RootGeom[] Roots;
            public readonly List<Item> Items = new List<Item>();
            public double Ms;
            public string Summary;

            public int Vertices
            {
                get
                {
                    int n = 0;
                    for (int g = 0; g < Groups.Length; g++) n += Fills[g].VertexCount + Lines[g].VertexCount;
                    return n;
                }
            }
        }

        /// <summary>The renderers of one build.</summary>
        sealed class Shown
        {
            public readonly MeshRenderer[] Fill = new MeshRenderer[Groups.Length], Line = new MeshRenderer[Groups.Length];
        }

        // ------------------------------------------------------------------ lifecycle

        public override void Prepare(GraphContext ctx)
        {
            Stopwatch sw = Stopwatch.StartNew();
            labels = ctx.Labels;
            nowYear = ctx.NowYear;
            model = ctx.Shared<EconomyModel>(EconomyModel.SharedKey);
            LandSnapshot first = ctx.Shared<LandSnapshot>(LandService.SharedKey);
            if (model == null || first?.Land == null)
            {
                Debug.LogWarning("[Why] LandscapeLayer: no land snapshot, the landscape is skipped");
                return;
            }

            prepared = Build(model.Data, first);
            foreach (Item it in prepared.Items)
            {
                Anchors.Register(AnchorOf(it, first.Year, out _));
                if (it.Text != null) AddSlot(it);
            }

            Debug.Log("[Why] LandscapeLayer.Prepare " + sw.Elapsed.TotalMilliseconds.ToString("0", LandFacts.Ci) + " ms: " +
                      prepared.Vertices.ToString(LandFacts.Ci) + " vertices (build " + prepared.Ms.ToString("0.0", LandFacts.Ci) +
                      " ms); " + prepared.Summary);
        }

        public override void Upload(GraphContext ctx)
        {
            if (prepared == null) return;
            Show(prepared, false);
            prepared = null;
            LandService.Changed += OnChanged;
        }

        void OnDestroy()
        {
            LandService.Changed -= OnChanged;
            Release(current);
            Release(old);
            current = old = null;
        }

        /// <summary>
        /// A new snapshot is on screen (main thread): its build starts on a worker now; a blocking one (a preset's year) is
        /// waited for and uploaded in the next Tick, an async one is uploaded in the first Tick after it finishes (8.3).
        /// </summary>
        void OnChanged(LandSnapshot s)
        {
            if (s?.Land == null) return;
            wanted = s;
            if (!s.Blocking || model == null || SameLand(s, shownSnapshot)) return;
            EconomyData data = model.Data;
            blockingBuild = Task.Run(() => Build(data, s));
            blockingFor = s;
        }

        /// <summary>Two snapshots of the same land and money (a new season of the same year): nothing this layer draws differs.</summary>
        static bool SameLand(LandSnapshot a, LandSnapshot b) =>
            a != null && b != null && ReferenceEquals(a.Land, b.Land) && ReferenceEquals(a.Money, b.Money);

        public override void Tick(GraphContext ctx, CameraRig rig)
        {
            if (model == null) return;
            Rebuild();
            if (old != null)
            {
                cross = Mathf.Min(1f, cross + Mathf.Min(0.1f, Time.unscaledDeltaTime) / LandStyle.CrossFadeSeconds);
                if (cross >= 1f)
                {
                    Release(old);
                    old = null;
                }
            }

            ApplyAlpha(current, old != null ? cross : 1f);
            ApplyAlpha(old, 1f - cross);
            UpdateLabels(false);
            PlaceLabels(rig);
        }

        /// <summary>A tier label's world place: on the outer wall at an angle, at the middle of its ring's band.</summary>
        static Vector3 TierLabelAt(int tier, float thetaDeg) =>
            EconomyStage.Land().World(LandFrame.Polar(LandStyle.RimR, thetaDeg, LandStyle.TerraceY[tier] + 0.5f * LandStyle.TerraceStep));

        /// <summary>
        /// Places the land's labels where the label system will keep them, whenever the camera, the screen or a label
        /// changed: the tier stack first (<see cref="PlaceTierLabels"/>), then a replay of the label system's greedy
        /// (priority, then rank) over the land's shown labels, in which each sector's label takes the first of its
        /// <see cref="SectorCandidates"/>, longest text first, that is in frame and clear of every label kept before it
        /// (<see cref="PlaceSector"/>), and every other label is kept in frame (shifted sideways by what it overflows).
        /// Other layers' labels still compete in the label system itself.
        /// </summary>
        void PlaceLabels(CameraRig rig)
        {
            Camera cam = rig != null ? rig.Cam : null;
            if (cam == null || labels == null) return;
            if (!placeDirty && rig.Version == placedCam && Screen.width == placedW && Screen.height == placedH) return;
            placeDirty = false;
            placedCam = rig.Version;
            placedW = Screen.width;
            placedH = Screen.height;
            if (placeOrderDirty)
            {
                placeOrderDirty = false;
                placeOrder.Clear();
                placeOrder.AddRange(slotList);
                placeOrder.Sort(ComparePlaceOrder);
                Array.Clear(tierSlots, 0, tierSlots.Length);
                foreach (Slot s in slotList)
                {
                    if (s.TierStack >= 0 && s.TierStack < tierSlots.Length) tierSlots[s.TierStack] = s;
                }
            }

            bool changed = PlaceTierLabels(cam);
            placedRects.Clear();
            drawnRects.Clear();
            foreach (Slot s in placeOrder)
            {
                if (s.Spec.Hidden) continue;
                if (s.Variants != null) changed |= PlaceSector(cam, s);   // sectors and roots
                else if (s.TierStack >= 0) Keep(LabelRect(cam, s.Spec, s.Spec.Text, s.Spec.PixelOffset, s.Spec.Align, out Rect r), r, s.Spec.Align);
                else changed |= KeepInFrame(cam, s);
            }

            if (changed) labels.MarkDirty();
        }

        /// <summary>The label system's placement order (higher priority, then higher rank, then the text), with an index tie-break.</summary>
        int ComparePlaceOrder(Slot a, Slot b)
        {
            int c = b.Spec.Priority.CompareTo(a.Spec.Priority);
            if (c == 0) c = b.Spec.Rank.CompareTo(a.Spec.Rank);
            if (c == 0) c = string.CompareOrdinal(a.Spec.Text, b.Spec.Text);
            return c != 0 ? c : a.Index.CompareTo(b.Index);
        }

        /// <summary>Keeps a rect the label system would keep (in front of the camera, not overlapping one kept before).</summary>
        void Keep(bool inFront, Rect r, TextAlignmentOptions align)
        {
            if (!inFront || Overlaps(placedRects, r)) return;
            placedRects.Add(r);
            drawnRects.Add(Drawn(r, align));
        }

        static bool Overlaps(List<Rect> rects, Rect r)
        {
            for (int i = 0; i < rects.Count; i++)
            {
                if (rects[i].Overlaps(r)) return true;
            }

            return false;
        }

        /// <summary>A label's rect widened by <see cref="FrameWidthSlack"/> on the side(s) its text grows to (the drawn text's extent).</summary>
        static Rect Drawn(Rect r, TextAlignmentOptions align)
        {
            float slack = FrameWidthSlack * r.width;
            float left = align == TextAlignmentOptions.Left ? 0 : align == TextAlignmentOptions.Center ? 0.5f * slack : slack;
            return new Rect(r.x - left, r.y, r.width + slack, r.height);
        }

        /// <summary>A label's rect is inside the frame, with <see cref="FrameWidthSlack"/> on the side(s) its text grows to.</summary>
        static bool InFrame(Rect r, TextAlignmentOptions align)
        {
            float slack = FrameWidthSlack * r.width;
            float left = align == TextAlignmentOptions.Left ? 0 : align == TextAlignmentOptions.Center ? 0.5f * slack : slack;
            float right = slack - left;
            return r.xMin - left >= FrameMarginPx && r.yMin >= FrameMarginPx && r.xMax + right <= Screen.width - FrameMarginPx &&
                   r.yMax <= Screen.height - FrameMarginPx;
        }

        /// <summary>
        /// A sector's label (and a root's, whose direction points down): aligned away from the bowl's center along the sector's screen radial (its inner to its outer
        /// edge at the center angle), at the first candidate place in frame and clear of the labels kept before it, with
        /// the longest text that has one (the full line, then name and value added, then the name). When none fits, the
        /// name stands at the edge, moved into the frame (the label system drops it).
        /// </summary>
        bool PlaceSector(Camera cam, Slot s)
        {
            Vector3 po = cam.WorldToScreenPoint(s.Spec.Data), pi = cam.WorldToScreenPoint(s.Inner);
            if (po.z <= cam.nearClipPlane) return false;
            Vector2 d = new Vector2(po.x - pi.x, po.y - pi.y);
            d = d.sqrMagnitude < 1f ? Vector2.up : d.normalized;
            TextAlignmentOptions align = d.x > SideAlign ? TextAlignmentOptions.Left
                : d.x < -SideAlign ? TextAlignmentOptions.Right : TextAlignmentOptions.Center;
            float scale = LabelSystem.UiScale, h = s.Spec.SizePx * scale * LineEm + 2 * labels.Padding;
            Vector2 at = new Vector2(po.x, po.y);
            for (int v = 0; v < s.Variants.Length; v++)
            {
                string text = s.Variants[v];
                foreach (Vector2 c in SectorCandidates)
                {
                    Vector2 shift = d * (c.x * h) + new Vector2(0, c.y * h);
                    Rect r = LabelRect(at + shift, text, s.Spec.SizePx, align);
                    Rect drawn = Drawn(r, align);
                    if (!InFrame(r, align) || Overlaps(drawnRects, drawn)) continue;
                    placedRects.Add(r);
                    drawnRects.Add(drawn);
                    return SetPlace(s, v, shift / scale, align);
                }
            }

            // the shortest text at the wider set of places (WP6)
            int last = s.Variants.Length - 1;
            foreach (Vector2 c in WideCandidates)
            {
                if (c.x <= CandidateOut && Mathf.Abs(c.y) <= CandidateRows) continue;   // tried above
                Vector2 shift = d * (c.x * h) + new Vector2(0, c.y * h);
                Rect r = LabelRect(at + shift, s.Variants[last], s.Spec.SizePx, align);
                Rect drawn = Drawn(r, align);
                if (!InFrame(r, align) || Overlaps(drawnRects, drawn)) continue;
                placedRects.Add(r);
                drawnRects.Add(drawn);
                return SetPlace(s, last, shift / scale, align);
            }

            // nothing fits: the shortest text at the edge, inside the frame (the label system drops it unless another
            // layer's label it would have met is gone)
            Rect at0 = LabelRect(at, s.Variants[last], s.Spec.SizePx, align);
            float dx = at0.xMin < FrameMarginPx ? FrameMarginPx - at0.xMin
                : at0.xMax > Screen.width - FrameMarginPx ? Screen.width - FrameMarginPx - at0.xMax : 0;
            return SetPlace(s, last, new Vector2(dx / scale, 0), align);
        }

        /// <summary>Sets a sector label's text, offset and alignment; true when any changed.</summary>
        bool SetPlace(Slot s, int variant, Vector2 offset, TextAlignmentOptions align)
        {
            offset += s.Offset;
            bool changed = false;
            if (s.Variant != variant || s.Spec.Text != s.Variants[variant])
            {
                s.Variant = variant;
                labels.SetText(s.Spec, s.Variants[variant]);
                changed = true;
            }

            if ((s.Spec.PixelOffset - offset).sqrMagnitude > 0.01f || s.Spec.Align != align)
            {
                s.Spec.PixelOffset = offset;
                s.Spec.Align = align;
                changed = true;
            }

            return changed;
        }

        /// <summary>
        /// Any other land label (a tower, the crown, a root, an overlay, a pool): shifted sideways just enough to stand
        /// wholly in frame when its place is near an edge (as wide as the frame at most), then kept as the label system
        /// would keep it.
        /// </summary>
        bool KeepInFrame(Camera cam, Slot s)
        {
            if (!LabelRect(cam, s.Spec, s.Spec.Text, s.Offset, s.Spec.Align, out Rect r)) return false;
            float dx = 0, lo = FrameMarginPx, hi = Screen.width - FrameMarginPx;
            bool visible = r.xMax > 0 && r.xMin < Screen.width && r.yMax > 0 && r.yMin < Screen.height;
            if (visible && r.width <= hi - lo)
            {
                if (r.xMin < lo) dx = lo - r.xMin;
                else if (r.xMax > hi) dx = hi - r.xMax;
            }

            r.x += dx;
            Keep(visible, r, s.Spec.Align);
            Vector2 offset = s.Offset + new Vector2(dx / LabelSystem.UiScale, 0);
            if ((s.Spec.PixelOffset - offset).sqrMagnitude <= 0.01f) return false;
            s.Spec.PixelOffset = offset;
            return true;
        }

        /// <summary>
        /// Keeps the five tier labels in frame and apart: the stack stands at the first of <see cref="TierLabelThetas"/>
        /// where every label's rect (spread by <see cref="SpreadTierLabels"/>) is inside the screen and clear of the crown's
        /// label (a higher-priority stack would hide it); when none is, at the place needing the least sideways shift into
        /// the frame, shifted; true when a label moved.
        /// </summary>
        bool PlaceTierLabels(Camera cam)
        {
            bool any = false;
            foreach (Slot s in tierSlots) any |= s != null && !s.Spec.Hidden;
            if (!any) return false;
            bool hasCrown = slots.TryGetValue("land:crown", out Slot cs) && !cs.Spec.Hidden &&
                            LabelRect(cam, cs.Spec, cs.Spec.Text, cs.Spec.PixelOffset, cs.Spec.Align, out tierCrown);

            // the first place whose stack is in frame as it stands; else the one needing the least sideways shift into it
            float[] thetas = LandView.PresetId == "overview" ? OverviewTierLabelThetas : TierLabelThetas;
            float chosen = thetas[0], best = float.MaxValue, shift = 0;
            foreach (float theta in thetas)
            {
                if (!SpreadTierLabels(cam, theta)) continue;
                float dx = TierStackShift(), cost = Mathf.Abs(dx);
                if (float.IsNaN(dx) || hasCrown && TierStackMeets(tierCrown, dx) || cost >= best) continue;
                chosen = theta;
                best = cost;
                shift = dx;
                if (cost == 0) break;
            }

            SpreadTierLabels(cam, chosen);
            for (int t = 0; t < tierShift.Length; t++) tierShiftX[t] = shift;
            tierTheta = chosen;
            bool changed = false;
            for (int t = 0; t < tierSlots.Length; t++)
            {
                Slot s = tierSlots[t];
                if (s == null) continue;
                Vector3 data = TierLabelAt(t, chosen);
                Vector2 offset = s.Offset + new Vector2(tierShiftX[t], tierShift[t]) / LabelSystem.UiScale;
                if ((s.Spec.Data - data).sqrMagnitude < 1e-8f && (s.Spec.PixelOffset - offset).sqrMagnitude <= 0.01f) continue;
                s.Spec.Data = data;
                s.Spec.PixelOffset = offset;
                changed = true;
            }

            return changed;
        }

        /// <summary>
        /// The sideways shift (px) that brings the spread tier stack wholly into the frame (0 when it is in frame; NaN when
        /// it does not fit in height or width).
        /// </summary>
        float TierStackShift()
        {
            float lo = float.MinValue, hi = float.MaxValue;
            for (int t = 0; t < tierSlots.Length; t++)
            {
                if (tierSlots[t] == null || tierSlots[t].Spec.Hidden) continue;
                Rect r = tierRects[t];
                float slack = FrameWidthSlack * r.width;
                if (r.yMin < FrameMarginPx || r.yMax > Screen.height - FrameMarginPx) return float.NaN;
                lo = Mathf.Max(lo, FrameMarginPx - (r.xMin - 0.5f * slack));
                hi = Mathf.Min(hi, Screen.width - FrameMarginPx - (r.xMax + 0.5f * slack));
            }

            return lo > hi ? float.NaN : lo > 0 ? lo : hi < 0 ? hi : 0;
        }

        /// <summary>Whether the spread tier stack, shifted sideways, meets a rect (the crown's label).</summary>
        bool TierStackMeets(Rect other, float dx)
        {
            for (int t = 0; t < tierSlots.Length; t++)
            {
                if (tierSlots[t] == null || tierSlots[t].Spec.Hidden) continue;
                Rect r = tierRects[t];
                r.x += dx;
                if (r.Overlaps(other)) return true;
            }

            return false;
        }

        /// <summary>
        /// The tier labels' screen places with the stack at a wall angle: each at its band's middle, spread apart where the
        /// bands project closer than a label's height plus <see cref="TierLabelGapPx"/> by the least-squares fit that keeps
        /// their order (pool-adjacent violators on y − i × gap); fills <see cref="tierShift"/> (px, up) and
        /// <see cref="tierRects"/>. False when a label is behind the camera.
        /// </summary>
        bool SpreadTierLabels(Camera cam, float theta)
        {
            int n = 0;
            float gap = 0;
            for (int t = 0; t < tierSlots.Length; t++)
            {
                tierShift[t] = 0;
                Slot s = tierSlots[t];
                if (s == null || s.Spec.Hidden) continue;
                Vector3 p = cam.WorldToScreenPoint(TierLabelAt(t, theta));
                if (p.z <= cam.nearClipPlane) return false;
                Vector2 o = s.Offset * LabelSystem.UiScale;
                spreadIndex[n] = t;
                spreadX[n] = p.x + o.x;
                spreadY[n] = p.y + o.y;
                gap = Mathf.Max(gap, s.Spec.SizePx * LabelSystem.UiScale * LineEm + 2 * labels.Padding + TierLabelGapPx);
                n++;
            }

            if (n == 0) return true;
            float sign = spreadY[n - 1] >= spreadY[0] ? 1f : -1f;
            int k = 0;
            for (int i = 0; i < n; i++)
            {
                blockValue[k] = sign * spreadY[i] - i * gap;
                blockCount[k] = 1;
                k++;
                while (k > 1 && blockValue[k - 2] > blockValue[k - 1])
                {
                    int w = blockCount[k - 2] + blockCount[k - 1];
                    blockValue[k - 2] = (blockValue[k - 2] * blockCount[k - 2] + blockValue[k - 1] * blockCount[k - 1]) / w;
                    blockCount[k - 2] = w;
                    k--;
                }
            }

            for (int b = 0, i = 0; b < k; b++)
            {
                for (int j = 0; j < blockCount[b]; j++, i++)
                {
                    int t = spreadIndex[i];
                    float y = sign * (blockValue[b] + i * gap);
                    tierShift[t] = y - spreadY[i];
                    tierRects[t] = LabelRect(new Vector2(spreadX[i], y), tierSlots[t].Spec.Text, tierSlots[t].Spec.SizePx,
                        tierSlots[t].Spec.Align);
                }
            }

            return true;
        }

        /// <summary>A label's screen rect as the label system measures it, at its world place plus a pixel offset; false behind the camera.</summary>
        bool LabelRect(Camera cam, LabelSpec spec, string text, Vector2 offset, TextAlignmentOptions align, out Rect rect)
        {
            rect = Rect.zero;
            Vector3 p = cam.WorldToScreenPoint(spec.Data);
            if (p.z <= cam.nearClipPlane) return false;
            rect = LabelRect(new Vector2(p.x, p.y) + offset * LabelSystem.UiScale, text, spec.SizePx, align);
            return true;
        }

        /// <summary>The rect the label system tests for a text at a screen point: its estimated width, line height, alignment and padding.</summary>
        Rect LabelRect(Vector2 p, string text, float sizePx, TextAlignmentOptions align)
        {
            float scale = LabelSystem.UiScale, pad = labels.Padding, w = TextEm(text) * sizePx * scale, h = sizePx * scale * LineEm;
            float x0 = align == TextAlignmentOptions.Right ? p.x - w : align == TextAlignmentOptions.Center ? p.x - 0.5f * w : p.x;
            return new Rect(x0 - pad, p.y - 0.5f * h - pad, w + 2 * pad, h + 2 * pad);
        }

        /// <summary>A plain text's width in em as the label system estimates it (LabelSystem.EstimateWidth without tags).</summary>
        static float TextEm(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            float w = 0;
            foreach (char c in text) w += c == ' ' ? EmSpace : char.IsUpper(c) ? EmUpper : char.IsDigit(c) ? EmDigit : EmOther;
            return w + EmMargin;
        }

        /// <summary>
        /// Builds the latest published snapshot: a blocking one (a preset's year) is the worker build started when it was
        /// published, waited for and shown at once (built here only when none was started); an async one is built on a
        /// worker, shown when it finishes unless a newer snapshot arrived meanwhile (rapid year changes coalesce to the
        /// latest; a stale build is discarded). At most one worker build runs; a newer async snapshot waits for it.
        /// </summary>
        void Rebuild()
        {
            if (building != null && building.IsCompleted)
            {
                Task<Content> t = building;
                building = null;
                if (t.IsFaulted) Debug.LogError("[Why] LandscapeLayer: a rebuild failed: " + t.Exception?.GetBaseException());
                else if (wanted == null && t.Result.Snapshot.Version > shownVersion) Show(t.Result, true);
            }

            if (wanted == null || building != null && !wanted.Blocking) return;
            LandSnapshot s = wanted;
            wanted = null;
            Task<Content> prebuilt = ReferenceEquals(blockingFor, s) ? blockingBuild : null;
            blockingBuild = null;
            blockingFor = null;
            if (SameLand(s, shownSnapshot))
            {
                // the same land and money (a new season of the year on screen): nothing of this layer changes (a worker
                // build of an older year still running is now stale and will be discarded)
                shownVersion = s.Version;
                LandService.ReportReady(ReadyName, s.Version);
                return;
            }

            EconomyData data = model.Data;
            if (s.Blocking) Show(prebuilt != null ? prebuilt.GetAwaiter().GetResult() : Build(data, s), true);
            else building = Task.Run(() => Build(data, s));
        }

        /// <summary>Uploads a build, starts the cross-fade from the meshes on screen, updates labels and anchors, reports the swap.</summary>
        void Show(Content c, bool log)
        {
            Stopwatch sw = Stopwatch.StartNew();
            Shown next = Upload(c);
            if (current != null)
            {
                Release(old);
                old = current;
                cross = 0;
            }

            current = next;
            shownVersion = c.Snapshot.Version;
            shownSnapshot = c.Snapshot;
            ApplyItems(c);
            ApplyAlpha(current, old != null ? 0f : 1f);
            ApplyAlpha(old, 1f);
            LandService.ReportReady(ReadyName, c.Snapshot.Version);
            if (log)
            {
                Debug.Log("[Why] LandscapeLayer rebuild " + c.Snapshot.Year.ToString(LandFacts.Ci) + ": build " +
                          c.Ms.ToString("0.0", LandFacts.Ci) + " ms, upload " + sw.Elapsed.TotalMilliseconds.ToString("0.0", LandFacts.Ci) +
                          " ms, " + c.Vertices.ToString(LandFacts.Ci) + " vertices; " + c.Summary);
            }
        }

        Shown Upload(Content c)
        {
            Shown s = new Shown();
            LandFrame frame = EconomyStage.Land();
            string year = c.Snapshot.Year.ToString(LandFacts.Ci);
            for (int g = 0; g < Groups.Length; g++)
            {
                string name = "Land" + Groups[g] + " " + year;
                if (c.Fills[g].VertexCount > 0)
                {
                    Material m = GraphMaterials.Raw(GraphMaterials.Surface(Color.white, 1f, Queues[g]));
                    m.SetFloat("_EdgeSoft", 0f);
                    s.Fill[g] = AddMesh(name, c.Fills[g].ToMesh(name), m);
                    frame.Place(s.Fill[g].transform);
                }

                if (c.Lines[g].VertexCount > 0)
                {
                    bool roots = g == GRoots;
                    Material m = GraphMaterials.Raw(GraphMaterials.Line(Color.white, 1f, Queues[g] + 1, !roots || RootsAdditive, 0, roots ? 1f : 0f));
                    if (roots)
                    {
                        m.SetFloat("_FlowFreq", RootFlowFreq);
                        m.SetFloat("_FlowSpeed", RootFlowSpeed);
                    }

                    s.Line[g] = AddMesh(name + " lines", c.Lines[g].ToMesh(name + " lines"), m);
                    frame.Place(s.Line[g].transform);
                }
            }

            return s;
        }

        void Release(Shown s)
        {
            if (s == null) return;
            foreach (MeshRenderer[] set in new[] { s.Fill, s.Line })
            {
                for (int g = 0; g < set.Length; g++)
                {
                    MeshRenderer r = set[g];
                    if (r == null) continue;
                    MeshFilter f = r.GetComponent<MeshFilter>();
                    if (f != null) Destroy(f.sharedMesh);
                    Destroy(r.sharedMaterial);
                    Destroy(r.gameObject);
                    set[g] = null;
                }
            }
        }

        /// <summary>Each group's alpha: its emphasis (× the reveal) times the cross-fade weight; hidden below 0.003.</summary>
        static void ApplyAlpha(Shown s, float weight)
        {
            if (s == null) return;
            for (int g = 0; g < Groups.Length; g++)
            {
                float a = LandView.Alpha(Groups[g]) * weight;
                SetAlpha(s.Fill[g], a);
                SetAlpha(s.Line[g], a);
            }
        }

        static void SetAlpha(MeshRenderer r, float a)
        {
            if (r == null) return;
            r.enabled = a > 0.003f;
            GraphMaterials.SetAlpha(r.sharedMaterial, a);
        }

        // ------------------------------------------------------------------ labels and anchors (main thread after Prepare)

        /// <summary>
        /// Adds an item's label (hidden until <see cref="UpdateLabels"/> shows it). Its rank breaks equal priorities in the
        /// label system's order, which is fixed when the label is added (the roots' rank, their table dollars, does not
        /// change with the year; the pools' is their first year's inflow).
        /// </summary>
        void AddSlot(Item it)
        {
            LandFrame frame = EconomyStage.Land();
            Slot slot = new Slot
            {
                Group = it.Group, Present = it.Present, Tower = it.Tower, TierStack = it.TierStack, Offset = it.Offset, Index = slotList.Count,
                Variants = it.Variants, Inner = frame.World(it.Inner),
                Spec = new LabelSpec
                {
                    Text = it.Text, Data = it.TierStack >= 0 ? TierLabelAt(it.TierStack, tierTheta) : frame.World(it.Label),
                    Fixed = true, FixedRange = 0, PixelOffset = it.Offset, Priority = it.Priority, Rank = it.Rank,
                    SizePx = it.SizePx, Color = it.Color, Align = TextAlignmentOptions.Center, AnchorKey = it.Key, Ids = it.Ids,
                    Hidden = true
                }
            };
            slots[it.Key] = slot;
            slotList.Add(slot);
            placeOrderDirty = true;
            labels?.Add(slot.Spec);
        }

        /// <summary>
        /// The anchor of an item, created the first time (the caller registers it), then updated in place: name, blurb,
        /// ids, its world position (fixed: the land stands beside the road) and the year it is about.
        /// </summary>
        Anchor AnchorOf(Item it, int year, out bool created)
        {
            created = !anchors.TryGetValue(it.Key, out Anchor a);
            if (created)
            {
                a = new Anchor { Key = it.Key, Level = GraphLevel.Humans };
                anchors[it.Key] = a;
            }

            a.Label = it.Name;
            a.Blurb = it.Blurb;
            a.Ids = it.Ids;
            a.Tier = it.AnchorTier;
            a.Fixed = EconomyStage.Land().World(it.Anchor);
            a.YearsAgo = a.EndYearsAgo = Math.Max(0, nowYear - (year + 0.5));
            return a;
        }

        /// <summary>A build's texts, places and anchors go on screen (main thread).</summary>
        void ApplyItems(Content c)
        {
            foreach (Item it in c.Items)
            {
                Anchor a = AnchorOf(it, c.Snapshot.Year, out bool created);
                if (created) Anchors.Register(a);
                if (it.Text == null) continue;
                if (!slots.TryGetValue(it.Key, out Slot slot))
                {
                    AddSlot(it);
                    continue;
                }

                slot.Variants = it.Variants;
                slot.Inner = EconomyStage.Land().World(it.Inner);
                string text = it.Variants != null ? it.Variants[Math.Min(slot.Variant, it.Variants.Length - 1)] : it.Text;
                if (labels != null) labels.SetText(slot.Spec, text);
                else slot.Spec.Text = text;
                slot.Spec.Data = slot.TierStack >= 0 ? TierLabelAt(slot.TierStack, tierTheta) : EconomyStage.Land().World(it.Label);
                slot.Present = it.Present;
            }

            // labels of objects the build does not have (none today: every build lists every slot) are hidden
            HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (Item it in c.Items) keys.Add(it.Key);
            foreach (KeyValuePair<string, Slot> kv in slots)
            {
                if (!keys.Contains(kv.Key)) kv.Value.Present = false;
            }

            UpdateLabels(true);
        }

        /// <summary>
        /// A label shows when the year has its object and the preset lists its group with an alpha of at least 0.3 (7.2);
        /// the selected tower's label shows whenever the towers' alpha is at least 0.3, listed or not.
        /// </summary>
        void UpdateLabels(bool force)
        {
            int tower = EconomyState.SelectedTower;
            if (!force && seenLabels == LandView.LabelsVersion && seenTower == tower) return;
            seenLabels = LandView.LabelsVersion;
            seenTower = tower;
            bool changed = false;
            foreach (Slot s in slotList)
            {
                bool selected = s.Tower >= 0 && s.Tower == tower && LandView.Alpha(s.Group) >= 0.3f;
                bool hidden = !s.Present || !(LandView.LabelsShown(s.Group) || selected);
                if (hidden == s.Spec.Hidden) continue;
                s.Spec.Hidden = hidden;
                changed = true;
            }

            if (!changed && !force) return;
            placeDirty = true;
            labels?.MarkDirty();
        }

        // ------------------------------------------------------------------ the build (pure: any thread)

        /// <summary>Everything this layer draws and writes for one snapshot.</summary>
        static Content Build(EconomyData data, LandSnapshot s)
        {
            Stopwatch sw = Stopwatch.StartNew();
            Content c = new Content { Snapshot = s };
            for (int g = 0; g < Groups.Length; g++)
            {
                c.Fills[g] = new SurfaceMeshBuilder();
                c.Lines[g] = new LineMeshBuilder(1024);
            }

            LandGeometry land = s.Land;

            // four independent parts, each into its own groups' builders and item lists (joined in a fixed order after),
            // so the meshes and labels are the same whatever the threads' timing
            List<Item> rootItems = new List<Item>(), overlayItems = new List<Item>();
            Parallel.Invoke(
                () =>
                {
                    Terraces(c, land);
                    Sectors(c, data, land);
                    Pools(c, land, s.Money);
                },
                () =>
                {
                    c.Roots = RootsModel.Build(data, land, land.Year);
                    Roots(c, data, land);
                    RootItems(c, data, land, rootItems);
                },
                () =>
                {
                    Towers(c, land);
                    Crown(c);
                    Overlays(c, data, land);
                    OverlayItems(data, land, overlayItems);
                },
                () => Items(c, data, s));
            c.Items.AddRange(rootItems);
            c.Items.AddRange(overlayItems);
            c.Ms = sw.Elapsed.TotalMilliseconds;
            c.Summary = Summary(c, data, land);
            return c;
        }

        /// <summary>A tier's own color (WP6: low saturation, steel for government; gold is the owners' strips' alone).</summary>
        static Color TierColor(int t) => LandStyle.TierTint(t);

        static Color32 Tint(Color c, float alpha) => LandMath.Tint(c, alpha);

        /// <summary>A band of a ring between two radii over an angle range (degrees), flat at a height.</summary>
        static void Band(SurfaceMeshBuilder b, float r0, float r1, float th0, float th1, float y, Color32 color, float id,
            float intensity, float step = SectorStep)
        {
            List<Vector3> inner = new List<Vector3>(), outer = new List<Vector3>();
            LandFrame.SampleBand(inner, outer, r0, r1, th0, th1, y, step);
            b.AddBand(inner, outer, new[] { color }, id, intensity);
        }

        /// <summary>An arc line at a radius and height.</summary>
        static void Arc(LineMeshBuilder b, float r, float th0, float th1, float y, Color32 color, float px, float id,
            float intensity = 1, float step = SectorStep, float world = 0)
        {
            List<Vector3> pts = new List<Vector3>();
            LandFrame.SampleArc(pts, r, th0, th1, y, step);
            b.AddPolyline(pts, color, px, world, id, intensity);
        }

        // ---- 2.1 the strata

        /// <summary>Treads, risers (shaded by angle), tread edges, the outer wall's five bands, the rim and its edges.</summary>
        static void Terraces(Content c, LandGeometry land)
        {
            SurfaceMeshBuilder fill = c.Fills[GTerraces];
            LineMeshBuilder lines = c.Lines[GTerraces];
            float[] a = LandStyle.RingA, b = LandStyle.RingB, y = LandStyle.TerraceY;
            const float Turn = 360f, Step = Turn / LandStyle.TreadSegments;
            List<Vector3> inner = new List<Vector3>(), outer = new List<Vector3>();
            List<Color32> shade = new List<Color32>();
            for (int t = 0; t < 5; t++)
            {
                Color hue = TierColor(t);
                int id = EconomyIds.LandTier(t);
                Band(fill, a[t], b[t], 0, Turn, y[t], Tint(hue, LandStyle.TreadAlpha), id, TierIntensity[t], Step);
                if (t > 0) Arc(lines, a[t], 0, Turn, y[t], Tint(hue, LandStyle.TreadEdgeAlpha), LandStyle.TreadEdgePx, id, 1, Step);
                Arc(lines, b[t], 0, Turn, y[t], Tint(hue, LandStyle.TreadEdgeAlpha), LandStyle.TreadEdgePx, id, 1, Step);

                // the riser up to the next tread (the last one climbs to the lip), the upper ring's color, the far wall lighter
                float r1 = t < 4 ? a[t + 1] : LandStyle.LipR, y1 = t < 4 ? y[t + 1] : LandStyle.RimY;
                Color up = TierColor(Math.Min(4, t + 1));
                inner.Clear();
                outer.Clear();
                shade.Clear();
                for (int k = 0; k <= LandStyle.TreadSegments; k++)
                {
                    float th = k * Step;
                    inner.Add(LandFrame.Polar(b[t], th, y[t]));
                    outer.Add(LandFrame.Polar(r1, th, y1));
                    float f = LandStyle.RiserShadeBase + LandStyle.RiserShadeCos * Mathf.Cos((th - 90f) * Mathf.Deg2Rad);
                    shade.Add(Tint(up * f, LandStyle.RiserAlpha));
                }

                fill.AddBand(inner, outer, shade, EconomyIds.LandTier(Math.Min(4, t + 1)), TierIntensity[Math.Min(4, t + 1)]);

                // the outer wall: the tiers' stack, gov at the bottom
                inner.Clear();
                outer.Clear();
                for (int k = 0; k <= LandStyle.TreadSegments; k++)
                {
                    inner.Add(LandFrame.Polar(LandStyle.RimR, k * Step, t * LandStyle.TerraceStep));
                    outer.Add(LandFrame.Polar(LandStyle.RimR, k * Step, (t + 1) * LandStyle.TerraceStep));
                }

                fill.AddBand(inner, outer, new[] { Tint(hue, LandStyle.OuterWallAlpha) }, id, TierIntensity[t]);
            }

            // the rim: where the people stand
            Band(fill, LandStyle.LipR, LandStyle.RimR, 0, Turn, LandStyle.RimY, Tint(EconomyStyle.People, LandStyle.RimAlpha),
                GraphIds.None, 1f, Step);
            Arc(lines, LandStyle.LipR, 0, Turn, LandStyle.RimY, Tint(EconomyStyle.People, RimEdgeAlpha), LandStyle.RimEdgePx, GraphIds.None,
                1, Step);
            Arc(lines, LandStyle.RimR, 0, Turn, LandStyle.RimY, Tint(EconomyStyle.People, RimEdgeAlpha), LandStyle.RimEdgePx, GraphIds.None,
                1, Step);
        }

        // ---- 2.2 sectors

        /// <summary>Every sector's three strips (wages inner, upkeep, owners outer) at area-true radii, their boundaries, its outline.</summary>
        static void Sectors(Content c, EconomyData data, LandGeometry land)
        {
            SurfaceMeshBuilder fill = c.Fills[GSectors];
            LineMeshBuilder lines = c.Lines[GSectors];
            List<Vector3> pts = new List<Vector3>();
            foreach (SectorGeom s in land.Sectors)
            {
                if (s.Theta1 - s.Theta0 <= 1e-4f) continue;
                int i = s.Industry, t = (int)s.Tier;
                Color hue = EconomyStyle.Level(data.Industries[i].Level);
                float y = s.Y + StripLift;
                Band(fill, s.R0, s.RWages, s.Theta0, s.Theta1, y, Tint(LandStyle.Wages, LandStyle.WagesAlpha),
                    EconomyIds.LandSector(i, EconomyIds.SectorWages), 1f);
                Band(fill, s.RWages, s.RUpkeep, s.Theta0, s.Theta1, y, Tint(Steel, LandStyle.UpkeepAlpha),
                    EconomyIds.LandSector(i, EconomyIds.SectorUpkeep), 1f);
                Band(fill, s.RUpkeep, s.R1, s.Theta0, s.Theta1, y, Tint(hue, LandStyle.OwnersAlpha),
                    EconomyIds.LandSector(i, EconomyIds.SectorOwners), LandStyle.OwnersIntensity * TierIntensity[t] / TierIntensity[2]);

                float edge = s.Y + EdgeLift;
                int edgeId = EconomyIds.LandSector(i, EconomyIds.SectorEdge);
                // the strip boundaries and the outline in the tier's own tint: only the owners' strip itself is gold
                Color tint = TierColor(t);
                Arc(lines, s.RWages, s.Theta0, s.Theta1, edge, Tint(tint, StripEdgeAlpha), LandStyle.StripEdgePx, edgeId);
                Arc(lines, s.RUpkeep, s.Theta0, s.Theta1, edge, Tint(tint, StripEdgeAlpha), LandStyle.StripEdgePx, edgeId);
                pts.Clear();
                LandFrame.SectorOutline(pts, s, EdgeLift, SectorStep);
                lines.AddPolyline(pts, Tint(tint, SectorEdgeAlpha), LandStyle.StripEdgePx, 0, edgeId);
            }
        }

        // ---- 2.5 pools

        /// <summary>
        /// The share of a sector's area a pool covers (inflow / value added, at most 1), the radius its shore reaches
        /// (filling from the outer edge inward) and its rise when households pay more than the value added.
        /// </summary>
        static void PoolShape(SectorGeom s, double inflow, out float fill, out float shore, out float rise)
        {
            double ratio = s.ValueAdded > 0 ? inflow / s.ValueAdded : 0;
            fill = (float)Math.Min(1, ratio);
            shore = Mathf.Sqrt(Mathf.Max(0, s.R1 * s.R1 - fill * (s.R1 * s.R1 - s.R0 * s.R0)));
            rise = ratio > 1 ? LandStyle.OverflowRise * (float)Math.Min(LandStyle.OverflowMax, ratio - 1) : 0;
        }

        /// <summary>
        /// Each pool: a veil from the sector's outer edge inward to its shore, raised with a side wall when it overflows,
        /// and its two-tone shoreline (rose over the desire share from the inlet end, the end nearer the falls, then ice).
        /// </summary>
        static void Pools(Content c, LandGeometry land, MoneyFlows money)
        {
            if (money == null) return;
            SurfaceMeshBuilder fill = c.Fills[GPools];
            LineMeshBuilder lines = c.Lines[GPools];
            Color32 veil = Tint(LandStyle.PoolVeil, LandStyle.PoolAlpha), wall = Tint(LandStyle.PoolVeil, PoolWallAlpha);
            List<Vector3> lo = new List<Vector3>(), hi = new List<Vector3>();
            foreach (SectorGeom s in land.Sectors)
            {
                int i = s.Industry;
                double inflow = money.PoolInflow[i];
                if (inflow <= 0 || s.Theta1 - s.Theta0 <= 1e-4f) continue;
                PoolShape(s, inflow, out float f, out float shore, out float rise);
                int id = EconomyIds.LandSector(i, EconomyIds.SectorPool);
                float y0 = s.Y + LandStyle.PoolLift, y1 = y0 + rise;
                Band(fill, shore, s.R1, s.Theta0, s.Theta1, y1, veil, id, 1f);
                if (rise > 0)
                {
                    // the side wall: outer arc, inner arc (rings), both radial ends
                    foreach (float r in s.R0 > 1e-4f ? new[] { s.R1, s.R0 } : new[] { s.R1 })
                    {
                        lo.Clear();
                        hi.Clear();
                        LandFrame.SampleArc(lo, r, s.Theta0, s.Theta1, y0, SectorStep);
                        LandFrame.SampleArc(hi, r, s.Theta0, s.Theta1, y1, SectorStep);
                        fill.AddBand(lo, hi, new[] { wall }, id);
                    }

                    foreach (float th in new[] { s.Theta0, s.Theta1 })
                    {
                        lo.Clear();
                        hi.Clear();
                        lo.Add(LandFrame.Polar(s.R0, th, y0));
                        lo.Add(LandFrame.Polar(s.R1, th, y0));
                        hi.Add(LandFrame.Polar(s.R0, th, y1));
                        hi.Add(LandFrame.Polar(s.R1, th, y1));
                        fill.AddBand(lo, hi, new[] { wall }, id);
                    }
                }

                // the shoreline: a full pool of a wedge has no inner arc, its shore is the outer one
                float line = shore > 1e-3f ? shore : s.R1;
                double fear = Math.Max(0, Math.Min(1, money.PoolFear[i] / inflow));
                bool inletHigh = InletAtEnd(s, money);
                float span = s.Theta1 - s.Theta0, desire = span * (float)(1 - fear);
                float a0 = inletHigh ? s.Theta1 : s.Theta0, dir = inletHigh ? -1 : 1, a1 = a0 + dir * desire;
                if (desire > 1e-3f)
                {
                    Arc(lines, line, a0, a1, y1 + 0.002f, Tint(EconomyStyle.Desire, ShoreAlpha), LandStyle.ShorePx, id,
                        LandStyle.ShoreIntensity, 1.5f);
                }

                if (span - desire > 1e-3f)
                {
                    Arc(lines, line, a1, a0 + dir * span, y1 + 0.002f, Tint(EconomyStyle.Fear, ShoreAlpha), LandStyle.ShorePx, id,
                        LandStyle.ShoreIntensity, 1.5f);
                }
            }
        }

        /// <summary>Whether a sector's inlet (where the rivers arrive) is its high-angle end: the end nearer a category's fall.</summary>
        static bool InletAtEnd(SectorGeom s, MoneyFlows money)
        {
            float best0 = float.MaxValue, best1 = float.MaxValue;
            for (int l = 0; l < LandStyle.LaneTaxes; l++)
            {
                best0 = Mathf.Min(best0, Mathf.Abs(LandMath.DeltaDeg(money.Fall[l], s.Theta0)));
                best1 = Mathf.Min(best1, Mathf.Abs(LandMath.DeltaDeg(money.Fall[l], s.Theta1)));
            }

            return best1 < best0;
        }

        // ---- 2.4 roots

        /// <summary>Roots (supplier's level color, pulses toward the buyer), mesh roots (dim steel), root balls (closed loops).</summary>
        static void Roots(Content c, EconomyData data, LandGeometry land)
        {
            LineMeshBuilder lines = c.Lines[GRoots];
            List<LinePoint> pts = new List<LinePoint>(LandStyle.RootPoints + 1);
            foreach (RootGeom r in c.Roots)
            {
                if (r.Points == null || r.Dollars <= 0) continue;
                Color hue;
                float alpha, id;
                switch (r.Kind)
                {
                    case RootKind.Root:
                        hue = RootColor(data, r.From);
                        alpha = LandStyle.RootAlpha;
                        id = EconomyIds.LandRoot(r.Index);
                        break;
                    case RootKind.Mesh:
                        hue = Steel;
                        alpha = MeshRootAlpha;
                        id = EconomyIds.LandSector(r.To, EconomyIds.SectorRootsIn);
                        break;
                    default:
                        hue = RootColor(data, r.To);
                        alpha = LandStyle.RootAlpha * RootBallShare;
                        id = EconomyIds.LandSector(r.To, EconomyIds.SectorRootsIn);
                        break;
                }

                Color32 col = Tint(hue, alpha * LandStyle.DollarFade(r.Dollars));
                pts.Clear();
                foreach (Vector3 p in r.Points) pts.Add(new LinePoint(p, col, LandStyle.LineFloorPx, r.Width, LandStyle.RootIntensity));
                if (r.Kind == RootKind.Ball) lines.AddPolyline(pts, id);
                else lines.AddFlowPath(pts, id, LandStyle.RootPulsePerUnit, LandMath.Hash01(r.From + 1, r.To + 1));
            }
        }

        /// <summary>
        /// A root's color by its supplier (WP6): matter red or life green for raw suppliers, else the supplier's tier tint
        /// (<see cref="LandStyle.RootTint"/>): purchases between industries are not capital, so never gold.
        /// </summary>
        static Color RootColor(EconomyData data, int industry)
        {
            Data.Industry ind = data.Industries[industry];
            if (ind.Level == "matter" || ind.Level == "life") return EconomyStyle.Level(ind.Level);
            return LandStyle.RootTint(LandLayout.TierIndex(ind));
        }

        // ---- 2.6 corporations

        /// <summary>
        /// Towers (walls fading up, four edges, the top square as bright as the margin, the foot ring), the private
        /// company's outline, and the plinths: gold veils continuing a sector's owners' strip where its towers need more arc.
        /// </summary>
        static void Towers(Content c, LandGeometry land)
        {
            SurfaceMeshBuilder fill = c.Fills[GTowers];
            LineMeshBuilder lines = c.Lines[GTowers];
            Color gold = EconomyStyle.Capital;
            foreach (SectorGeom s in land.Sectors)
            {
                if (float.IsNaN(s.Plinth0)) continue;
                int id = EconomyIds.LandSector(s.Industry, EconomyIds.SectorOwners);
                foreach ((float a, float b) in new[] { (s.Plinth0, s.Theta0), (s.Theta1, s.Plinth1) })
                {
                    if (b - a <= 1e-3f) continue;
                    Band(fill, s.RUpkeep, s.R1, a, b, s.Y + StripLift, Tint(gold, LandStyle.PlinthAlpha), id, 1f, 1f);
                    Arc(lines, s.R1, a, b, s.Y + EdgeLift, Tint(gold, StripEdgeAlpha), LandStyle.StripEdgePx, id, 1f, 1f);
                    Arc(lines, s.RUpkeep, a, b, s.Y + EdgeLift, Tint(gold, StripEdgeAlpha), LandStyle.StripEdgePx, id, 1f, 1f);
                }
            }

            Vector3[] baseSq = new Vector3[5], topSq = new Vector3[5];
            List<Vector3> ring = new List<Vector3>(FootSegments + 1);
            foreach (TowerGeom t in land.Towers)
            {
                int id = EconomyIds.LandTower(t.Company);
                Vector3 c0 = LandFrame.Polar(t.R, t.Theta, t.BaseY);
                Vector3 u = LandFrame.Radial(t.Theta), v = LandFrame.Tangent(t.Theta), up = new Vector3(0, t.Height, 0);
                float h = 0.5f * t.Side;
                Vector3[] corner = { c0 + h * (-u - v), c0 + h * (u - v), c0 + h * (u + v), c0 + h * (-u + v) };
                for (int k = 0; k < 4; k++)
                {
                    baseSq[k] = corner[k];
                    topSq[k] = corner[k] + up;
                }

                baseSq[4] = baseSq[0];
                topSq[4] = topSq[0];

                if (t.Private)
                {
                    // no accounts to size it: an outline at its valuation's height
                    Color32 dim = Tint(gold, PrivateAlpha);
                    for (int k = 0; k < 4; k++) lines.AddSegment(baseSq[k], topSq[k], dim, LandStyle.TowerEdgePx, 0, id);
                    lines.AddPolyline(baseSq, dim, LandStyle.TowerEdgePx, 0, id);
                    lines.AddPolyline(topSq, dim, LandStyle.TowerEdgePx, 0, id);
                    continue;
                }

                Color32 low = Tint(gold, LandStyle.TowerAlphaBase), high = Tint(gold, LandStyle.TowerAlphaTop);
                for (int k = 0; k < 4; k++)
                {
                    fill.AddBand(new[] { baseSq[k], baseSq[k + 1] }, new[] { topSq[k], topSq[k + 1] }, new[] { low }, new[] { high }, id);
                }

                float bright = 1f + LandStyle.TowerMarginIntensity * Mathf.Max(0, t.Margin);
                Color32 top = Tint(gold, TowerTopAlpha);
                fill.AddTriangle(topSq[0], topSq[1], topSq[2], top, id, bright);
                fill.AddTriangle(topSq[0], topSq[2], topSq[3], top, id, bright);

                Color32 edge = Tint(gold, TowerEdgeAlpha);
                for (int k = 0; k < 4; k++) lines.AddSegment(baseSq[k], topSq[k], edge, LandStyle.TowerEdgePx, 0, id);
                lines.AddPolyline(topSq, edge, LandStyle.TowerEdgePx, 0, id, bright);

                // the foot ring: revenue at the area scale
                ring.Clear();
                for (int k = 0; k <= FootSegments; k++)
                {
                    float a = 2 * Mathf.PI * k / FootSegments;
                    ring.Add(c0 + new Vector3(0, FootLift, 0) + t.FootR * (Mathf.Cos(a) * u + Mathf.Sin(a) * v));
                }

                lines.AddPolyline(ring, Tint(gold, FootAlpha), 1f, 0, id);
            }
        }

        // ---- 2.7 the crown

        /// <summary>The gold ring over the bowl's center where capital pools (its arcs in and out are the flows layer's).</summary>
        static void Crown(Content c)
        {
            List<Vector3> pts = new List<Vector3>(LandStyle.CrownSegments + 1);
            LandFrame.SampleArc(pts, LandStyle.CrownR, 0, 360, LandStyle.CrownY, 360f / LandStyle.CrownSegments);
            c.Lines[GCrown].AddPolyline(pts, Tint(EconomyStyle.Capital, CrownAlpha), LandStyle.CrownPx, 0, EconomyIds.LandCrown,
                LandStyle.CrownIntensity);
        }

        // ---- 2.8 overlays

        /// <summary>Whether a year shows TECH's overlays (their figures are 2025's).</summary>
        static bool ShowsOverlays(int year) => year >= LandStyle.AiFromYear;

        static Overlay OverlayById(EconomyData data, string id)
        {
            if (data.IndustriesFile?.Overlays == null) return null;
            foreach (Overlay o in data.IndustriesFile.Overlays)
            {
                if (o?.Id == id) return o;
            }

            return null;
        }

        /// <summary>
        /// The AI scaffold's foot (land polar) over the software-internet boundary of the tech ring: the middle of the gap
        /// between the two sectors (false when either is missing).
        /// </summary>
        static bool AiFoot(EconomyData data, LandGeometry land, out float theta, out float r, out float y)
        {
            theta = r = y = 0;
            Data.Industry sw = data.IndustryById("software"), net = data.IndustryById("internet");
            if (sw == null || net == null) return false;
            SectorGeom a = land.Sectors[net.Index], b = land.Sectors[sw.Index];
            // the boundary between them: the gap after the lower one
            bool netFirst = LandMath.DeltaDeg(a.Mid, b.Mid) > 0;
            SectorGeom first = netFirst ? a : b, second = netFirst ? b : a;
            theta = first.Theta1 + 0.5f * LandMath.DeltaDeg(first.Theta1, second.Theta0);
            r = LandStyle.RingMid((int)Tier.Tech);
            y = LandStyle.TerraceY[(int)Tier.Tech];
            return true;
        }

        /// <summary>
        /// The AI build-out (a dashed lattice prism as tall as the year's capex, a dashed outline at the next year's plan,
        /// feed arcs at the river width to hardware, construction and utilities) and the ads halo around the internet
        /// sector (US digital ads, with a brighter inner arc for social ads). From <see cref="LandStyle.AiFromYear"/>.
        /// </summary>
        static void Overlays(Content c, EconomyData data, LandGeometry land)
        {
            if (!ShowsOverlays(land.Year)) return;
            LineMeshBuilder lines = c.Lines[GOverlays];
            Overlay ai = OverlayById(data, "ai");
            if (ai != null && AiFoot(data, land, out float theta, out float r, out float y))
            {
                int id = EconomyIds.LandOverlay(OverlayAi);
                float height = (float)ai.Figure("capex2025") * LandStyle.AiHeightPerB;
                float plan = (float)ai.Figure("capex2026") * LandStyle.AiHeightPerB;
                Vector3 c0 = LandFrame.Polar(r, theta, y), u = LandFrame.Radial(theta), v = LandFrame.Tangent(theta);
                float h = 0.5f * LandStyle.AiWidth;
                Vector3[] corner = { c0 + h * (-u - v), c0 + h * (u - v), c0 + h * (u + v), c0 + h * (-u + v) };
                Color32 col = Tint(ScaffoldWhite, AiAlpha), dim = Tint(ScaffoldWhite, AiPlanAlpha);
                int levels = Mathf.Max(1, Mathf.RoundToInt(height / AiLattice));
                for (int k = 0; k < 4; k++)
                {
                    Vector3 a = corner[k], b = corner[(k + 1) % 4], mid = 0.5f * (a + b);
                    Dashed(lines, a, a + new Vector3(0, height, 0), col, id);
                    Dashed(lines, mid, mid + new Vector3(0, height, 0), col, id);
                    for (int l = 0; l <= levels; l++)
                    {
                        Vector3 lift = new Vector3(0, height * l / levels, 0);
                        Dashed(lines, a + lift, b + lift, col, id);
                    }

                    if (plan > height)
                    {
                        Dashed(lines, a + new Vector3(0, height, 0), a + new Vector3(0, plan, 0), dim, id);
                        Dashed(lines, a + new Vector3(0, plan, 0), b + new Vector3(0, plan, 0), dim, id);
                    }
                }

                // where the money goes: chips, buildings, power
                double capex = ai.Figure("capex2025"), chips = ai.Figure("nvidiaDataCenter"), build = ai.Figure("usDataCenterConstruction2025");
                Feed(lines, data, land, "hardware", chips, c0 + new Vector3(0, 0.5f * height, 0), id);
                Feed(lines, data, land, "construction", build, c0 + new Vector3(0, 0.5f * height, 0), id);
                Feed(lines, data, land, "utilities", Math.Max(0, capex - chips - build), c0 + new Vector3(0, 0.5f * height, 0), id);
            }

            Overlay ads = OverlayById(data, "social");
            Data.Industry internet = data.IndustryById("internet");
            if (ads != null && internet != null)
            {
                SectorGeom s = land.Sectors[internet.Index];
                int id = EconomyIds.LandOverlay(OverlayAds);
                float whole = (float)(ads.Figure("usDigitalAds") * land.WidthPerB);
                float social = (float)(ads.Figure("usSocialAds") * land.WidthPerB);
                float rr = s.R1 + LandStyle.AdsHaloGap, yy = s.Y + EdgeLift;
                Arc(lines, rr, s.Theta0, s.Theta1, yy, Tint(LandStyle.Fantasy, AdsAlpha), LandStyle.LineFloorPx, id, 1f, 1f, whole);
                Arc(lines, rr - 0.5f * (whole - social), s.Theta0, s.Theta1, yy + 0.001f, Tint(LandStyle.Fantasy, AdsAlpha),
                    LandStyle.LineFloorPx, id, AdsSocialIntensity, 1f, social);
            }
        }

        /// <summary>A dashed straight line (world dash lengths).</summary>
        static void Dashed(LineMeshBuilder lines, Vector3 a, Vector3 b, Color32 color, float id)
        {
            float len = Vector3.Distance(a, b);
            if (len < 1e-5f) return;
            Vector3 d = (b - a) / len;
            for (float s = 0; s < len; s += DashOn + DashOff)
            {
                lines.AddSegment(a + d * s, a + d * Mathf.Min(len, s + DashOn), color, LandStyle.LineFloorPx, 0, id);
            }
        }

        /// <summary>An arc from the scaffold to an industry's sector at the river width scale.</summary>
        static void Feed(LineMeshBuilder lines, EconomyData data, LandGeometry land, string industry, double dollars, Vector3 from, float id)
        {
            Data.Industry ind = data.IndustryById(industry);
            if (ind == null || dollars <= 0) return;
            SectorGeom s = land.Sectors[ind.Index];
            Vector3 to = LandFrame.Polar(0.5f * (s.RUpkeep + s.R1), s.Mid, s.Y + StripLift);
            Vector3 control = 0.5f * (from + to) + new Vector3(0, AiFeedLift, 0);
            Color32 col = Tint(ScaffoldWhite, AiFeedAlpha * LandStyle.DollarFade(dollars));
            List<LinePoint> pts = new List<LinePoint>(LandStyle.ArcPoints);
            for (int k = 0; k < LandStyle.ArcPoints; k++)
            {
                float t = k / (float)(LandStyle.ArcPoints - 1);
                pts.Add(new LinePoint(LandMath.Bezier(from, control, to, t), col, LandStyle.LineFloorPx, (float)(dollars * land.WidthPerB)));
            }

            lines.AddFlowPath(pts, id);
        }

        // ---- 7.3 labels, anchors, blurbs

        static string ShortName(Data.Industry ind) => ShortNames.TryGetValue(ind.Id, out string n) ? n : ind.Name;

        static string Lower(string s) => string.IsNullOrEmpty(s) ? s : char.ToLowerInvariant(s[0]) + s.Substring(1);

        /// <summary>The items of a build's tiers, sectors, pools, towers and the crown (labels and anchors); the roots' and overlays' follow.</summary>
        static void Items(Content c, EconomyData data, LandSnapshot snap)
        {
            LandGeometry land = snap.Land;
            MoneyFlows money = snap.Money;
            string est = data.IsEstimate(land.Year) ? " (estimate)" : "";
            string year = land.Year.ToString(LandFacts.Ci);
            double gdp = Math.Max(land.Gdp, 1e-9);

            // tiers: the outer wall's stack at 250 degrees, each at its band's middle
            foreach (Data.Tier tier in data.Tiers)
            {
                Data.Industry firstOfTier = FirstOf(data, tier);
                if (firstOfTier == null) continue;
                int t = LandLayout.TierIndex(firstOfTier);
                double va = 0;
                int first = -1, last = -1;
                foreach (SectorGeom s in land.Sectors)
                {
                    if ((int)s.Tier != t) continue;
                    va += s.ValueAdded;
                    first = first < 0 ? s.Industry : Math.Min(first, s.Industry);
                    last = Math.Max(last, s.Industry);
                }

                if (first < 0) continue;
                string name = (tier.Name ?? tier.Id).ToUpperInvariant();
                Vector3 at = LandFrame.Polar(LandStyle.RimR, LandStyle.TierLabelThetaDeg, LandStyle.TerraceY[t] + 0.5f * LandStyle.TerraceStep);
                c.Items.Add(new Item
                {
                    Key = "land:tier:" + tier.Id, Name = tier.Name, Group = LandGroup.Terraces,
                    Text = name + " · " + LandFacts.Money(va, 1) + " · " + LandFacts.Percent(va / gdp) + " of GDP" + est,
                    Blurb = (tier.Blurb ?? "") + " " + year + ": " + LandFacts.Money(va) + " of value added, " +
                            LandFacts.Percent(va / gdp, 1) + " of GDP; its sectors fill " + LandFacts.Percent(land.RingFill[t]) +
                            " of the ring" + est + ".",
                    Label = at, TierStack = t,
                    Anchor = LandFrame.Polar(LandStyle.RingMid(t), LandStyle.TierLabelThetaDeg, LandStyle.TerraceY[t]),
                    Ids = EconomyIds.LandSectors(first, last), Priority = TierPriority, SizePx = TierLabelPx,
                    Color = Color.Lerp(TierColor(t), Color.white, 0.35f), AnchorTier = 1
                });
            }

            // sectors, and the named companies' profit against the sector's owners
            foreach (SectorGeom s in land.Sectors)
            {
                Data.Industry ind = data.Industries[s.Industry];
                double share = s.ValueAdded / gdp, ownersShare = s.ValueAdded > 0 ? s.Owners / s.ValueAdded : 0;
                int named = 0;
                double profit = 0;
                foreach (TowerGeom t in land.Towers)
                {
                    if (t.Industry != s.Industry) continue;
                    named++;
                    profit += Math.Max(0, t.NetIncome);
                }

                StringBuilder blurb = new StringBuilder();
                blurb.Append(ShortName(ind)).Append(": creates ").Append(LandFacts.Money(s.ValueAdded)).Append(" in the US in ").Append(year)
                    .Append(est).Append(" · wages ").Append(LandFacts.Money(s.Wages)).Append(" · taxes and depreciation ")
                    .Append(LandFacts.Money(s.Upkeep)).Append(" · owners keep ").Append(LandFacts.Money(s.Owners)).Append(" (")
                    .Append(LandFacts.Percent(ownersShare)).Append(')');
                if (named > 0 && s.Owners > 0)
                {
                    blurb.Append(" · the ").Append(named).Append(named == 1 ? " named company earned " : " named companies earned ")
                        .Append(LandFacts.Money(profit)).Append(" worldwide, ").Append(LandFacts.Times(profit / s.Owners)).Append(" that");
                }

                blurb.Append(". Its area is the value it adds; the gold strip is what owners keep.");
                // the label's texts, longest first: the placement shortens a crowded sector's label to name and value added,
                // then to its name (the rest is in the blurb and the hover card)
                string full = ShortName(ind) + " · " + LandFacts.Money(s.ValueAdded) + " · owners keep " + LandFacts.Percent(ownersShare) + est;
                c.Items.Add(new Item
                {
                    Key = "land:sector:" + ind.Id, Name = ind.Name, Group = LandGroup.Sectors, Text = full,
                    Variants = new[] { full, ShortName(ind) + " · " + LandFacts.Money(s.ValueAdded) + est, ShortName(ind) },
                    Blurb = blurb.ToString(), Label = LandFrame.Polar(s.R1, s.Mid, s.Y), Inner = LandFrame.Polar(s.R0, s.Mid, s.Y),
                    Anchor = LandFrame.Polar(0.5f * (s.R0 + s.R1), s.Mid, s.Y), Ids = EconomyIds.LandSectors(s.Industry, s.Industry),
                    Priority = 10 + 60 * (float)share, Rank = (float)s.ValueAdded, SizePx = SectorLabelPx,
                    Present = s.Theta1 - s.Theta0 > 1e-4f, AnchorTier = share > 0.04 ? 1 : 2
                });
            }

            // pools: what households pay each industry directly
            foreach (SectorGeom s in land.Sectors)
            {
                Data.Industry ind = data.Industries[s.Industry];
                double inflow = money?.PoolInflow[s.Industry] ?? 0;
                double ratio = s.ValueAdded > 0 ? inflow / s.ValueAdded : 0, fear = inflow > 0 ? money.PoolFear[s.Industry] / inflow : 0;
                PoolShape(s, inflow, out _, out float shore, out float rise);
                string text = ShortName(ind) + " · creates " + LandFacts.Money(s.ValueAdded) + " · households pay it " + LandFacts.Money(inflow) +
                              " directly (" + LandFacts.Percent(ratio) + ") · " + LandFacts.Percent(fear) + " fear" + est;
                string blurb = text + (ratio > 1
                                  ? ". Households pay it more than the value it adds: the rest flows on down its roots to its suppliers."
                                  : inflow > 0
                                      ? ". The pool is the household spending this industry is paid first; the roots carry it on to suppliers."
                                      : ". Households pay it nothing directly: it sells to other industries, to investment and to the state.");
                Vector3 at = LandFrame.Polar(shore > 1e-3f ? shore : s.R1, s.Mid, s.Y + LandStyle.PoolLift + rise);
                c.Items.Add(new Item
                {
                    Key = "land:pool:" + ind.Id, Name = ShortName(ind) + " pool", Group = LandGroup.Pools, Text = text, Blurb = blurb,
                    Label = at, Anchor = at, Ids = IdRange.Single(EconomyIds.LandSector(s.Industry, EconomyIds.SectorPool)),
                    Priority = PoolPriority, Rank = (float)inflow, Present = inflow >= LandStyle.PoolLabelMinB
                });
            }

            // towers: every company of the capture list (shown from 2024)
            List<Company> cap = data.Circuit?.Capture ?? new List<Company>();
            TowerGeom[] byCompany = new TowerGeom[cap.Count];
            foreach (TowerGeom t in land.Towers)
            {
                if (t.Company >= 0 && t.Company < cap.Count) byCompany[t.Company] = t;
            }

            for (int k = 0; k < cap.Count; k++)
            {
                Company co = cap[k];
                if (co == null) continue;
                TowerGeom t = byCompany[k];
                Data.Industry ind = data.IndustryById(co.Industry);
                bool priv = co.Revenue <= 0 && co.NetIncome <= 0;
                string text = priv
                    ? co.Name + " · private · valuation " + LandFacts.Money(co.MarketCap)
                    : co.Name + " · " + LandFacts.Money(co.MarketCap) + " · profit " + LandFacts.Money(co.NetIncome) + " on " +
                      LandFacts.Money(co.Revenue) + " (" + LandFacts.Percent(co.Revenue > 0 ? co.NetIncome / co.Revenue : 0) + ")";
                StringBuilder blurb = new StringBuilder(text);
                blurb.Append(". Profits earned worldwide; the sector is US value added.");
                if (t != null && ind != null && money != null)
                {
                    SectorGeom s = land.Sectors[ind.Index];
                    double inflow = money.PoolInflow[ind.Index];
                    if (s.ValueAdded > 0 && inflow / s.ValueAdded < 0.4)
                    {
                        blurb.Append(' ').Append(co.Name).Append(' ').Append(LandFacts.Money(co.MarketCap)).Append(" · households pay ")
                            .Append(Lower(ShortName(ind))).Append(' ').Append(LandFacts.Money(inflow))
                            .Append(" directly: it is paid by advertisers and other industries, along the roots.");
                    }
                }

                string key = "land:tower:" + (string.IsNullOrEmpty(co.Ticker) ? co.Name : co.Ticker);
                Vector3 foot = t != null ? LandFrame.Polar(t.R, t.Theta, t.BaseY) : Vector3.zero;
                c.Items.Add(new Item
                {
                    Key = key, Name = co.Name, Group = LandGroup.Towers, Text = text, Blurb = blurb.ToString(), Tower = k,
                    Label = t != null ? foot + new Vector3(0, t.Height + TowerLabelLift, 0) : Vector3.zero,
                    Anchor = t != null ? foot + new Vector3(0, 0.5f * t.Height, 0) : Vector3.zero,
                    Ids = IdRange.Single(EconomyIds.LandTower(k)), Priority = 15 + 3 * (float)(co.MarketCap / 1000), Present = t != null,
                    AnchorTier = 1
                });
            }

            // the crown
            double home = 0, abroad = money?.PayoutAbroad ?? 0;
            if (money != null)
            {
                foreach (double v in money.PayoutBySector) home += v;
            }

            Vector3 crown = new Vector3(0, LandStyle.CrownY + CrownLabelLift, 0);
            c.Items.Add(new Item
            {
                Key = "land:crown", Name = "The crown", Group = LandGroup.Crown,
                Text = "Capital · " + LandFacts.Money(home) + " a year to owners at home, " + LandFacts.Money(abroad) + " abroad" + est,
                Blurb = "Every private sector's owners pay out into the crown: dividends by owners' share, rent from real estate, " +
                        "interest from banking, " + LandFacts.Money(home) + " a year in " + year + ", and " + LandFacts.Money(abroad) +
                        " to owners abroad. Capital income falls from it onto the players who own.",
                Label = crown, Anchor = new Vector3(0, LandStyle.CrownY, 0), Ids = IdRange.Single(EconomyIds.LandCrown),
                Priority = CrownPriority, AnchorTier = 1
            });
        }

        /// <summary>Every root's anchor (land:root:from>to); the six largest are labeled under their lowest point.</summary>
        static void RootItems(Content c, EconomyData data, LandGeometry land, List<Item> into)
        {
            string est = data.IsEstimate(land.Year) ? " (estimate)" : "";
            string year = land.Year.ToString(LandFacts.Ci);
            int tableYear = data.Circuit?.Io != null && data.Circuit.Io.Year > 0 ? data.Circuit.Io.Year : LandStyle.LayoutYear;
            foreach (RootGeom r in c.Roots)
            {
                if (r.Kind != RootKind.Root) continue;
                Data.Industry a = data.Industries[r.From], b = data.Industries[r.To];
                string text = ShortName(a) + " → " + Lower(ShortName(b)) + " " + LandFacts.Money(r.Dollars) + est;
                string blurb = ShortName(a) + " sells " + LandFacts.Money(r.Table) + " a year to " + Lower(ShortName(b)) + " (BEA " +
                               tableYear.ToString(LandFacts.Ci) + " Use table" + (land.Year != tableYear
                                   ? "; " + LandFacts.Money(r.Dollars) + " at " + year + " value added"
                                   : "") + "). " + (r.Direction > 0
                                   ? "The supplier stands on a lower terrace: the root feeds upward."
                                   : r.Direction < 0
                                       ? "The supplier stands on a higher terrace: the root still rises into its buyer from below."
                                       : "Both stand on the same terrace.");
                into.Add(new Item
                {
                    Key = "land:root:" + a.Id + ">" + b.Id, Name = ShortName(a) + " → " + Lower(ShortName(b)), Group = LandGroup.Roots,
                    Text = r.Largest >= 0 ? text : null, Variants = r.Largest >= 0 ? new[] { text } : null, Blurb = blurb,
                    Label = r.Lowest - Vector3.up * RootLabelDrop, Inner = r.Lowest + Vector3.up * (RootLabelUp - RootLabelDrop), Anchor = r.Lowest,
                    Ids = IdRange.Single(EconomyIds.LandRoot(r.Index)), Priority = RootPriority, Rank = (float)r.Table,
                    Present = r.Largest >= 0
                });
            }

        }

        /// <summary>The AI build-out's and the ads halo's anchors and labels (shown from <see cref="LandStyle.AiFromYear"/>).</summary>
        static void OverlayItems(EconomyData data, LandGeometry land, List<Item> into)
        {
            Overlay ai = OverlayById(data, "ai");
            bool shows = ShowsOverlays(land.Year);
            if (ai != null)
            {
                bool foot = AiFoot(data, land, out float theta, out float r, out float y);
                float top = (float)Math.Max(ai.Figure("capex2025"), ai.Figure("capex2026")) * LandStyle.AiHeightPerB;
                Vector3 at = foot ? LandFrame.Polar(r, theta, y) : Vector3.zero;
                string text = "AI build-out: " + LandFacts.Money(ai.Figure("capex2025")) + " of capex in " + ai.Year.ToString(LandFacts.Ci) +
                              ", " + LandFacts.Money(ai.Figure("capex2026")) + " planned for " + (ai.Year + 1).ToString(LandFacts.Ci) +
                              " (investment, not value added)";
                into.Add(new Item
                {
                    Key = "land:ai", Name = ai.Name, Group = LandGroup.Overlays, Text = text, Blurb = (ai.Blurb ?? "") + " " + text + ".",
                    Label = at + new Vector3(0, top + TowerLabelLift, 0), Anchor = at + new Vector3(0, 0.5f * top, 0),
                    Ids = IdRange.Single(EconomyIds.LandOverlay(OverlayAi)), Priority = OverlayPriority, Present = shows && foot
                });
            }

            Overlay ads = OverlayById(data, "social");
            Data.Industry net = data.IndustryById("internet");
            if (ads != null && net != null)
            {
                SectorGeom s = land.Sectors[net.Index];
                string text = "Attention sold: " + LandFacts.Money(ads.Figure("usDigitalAds")) + " of US digital ads; social " +
                              LandFacts.Money(ads.Figure("usSocialAds")) + " (+" + ads.Figure("usSocialAdsGrowthPct").ToString("0", LandFacts.Ci) + "%)";
                Vector3 at = LandFrame.Polar(s.R1 + LandStyle.AdsHaloGap, s.Mid, s.Y);
                into.Add(new Item
                {
                    Key = "land:ads", Name = ads.Name, Group = LandGroup.Overlays, Text = text, Blurb = (ads.Blurb ?? "") + " " + text + ".",
                    Label = at, Anchor = at, Ids = IdRange.Single(EconomyIds.LandOverlay(OverlayAds)), Priority = OverlayPriority,
                    Present = shows
                });
            }
        }

        /// <summary>The first industry of a data tier (its ring), or null.</summary>
        static Data.Industry FirstOf(EconomyData data, Data.Tier tier)
        {
            foreach (Data.Industry ind in data.Industries)
            {
                if (ind.TierId == tier.Id) return ind;
            }

            return null;
        }

        /// <summary>
        /// The build's log summary: vertices by group, the roots (count and the year's dollars between industries, which
        /// the Roots line prints), the root balls, the plinths and the smallest gap between neighbouring towers of a ring.
        /// </summary>
        static string Summary(Content c, EconomyData data, LandGeometry land)
        {
            StringBuilder s = new StringBuilder();
            for (int g = 0; g < Groups.Length; g++)
            {
                s.Append(g == 0 ? "" : ", ").Append(Groups[g].ToString().ToLowerInvariant()).Append(' ')
                    .Append((c.Fills[g].VertexCount + c.Lines[g].VertexCount).ToString(LandFacts.Ci));
            }

            int roots = 0, mesh = 0, balls = 0;
            double rootB = 0, meshB = 0, ballB = 0;
            foreach (RootGeom r in c.Roots)
            {
                switch (r.Kind)
                {
                    case RootKind.Root:
                        roots++;
                        rootB += r.Dollars;
                        break;
                    case RootKind.Mesh:
                        mesh++;
                        meshB += r.Dollars;
                        break;
                    default:
                        balls++;
                        ballB += r.Dollars;
                        break;
                }
            }

            s.Append("; ").Append(roots).Append(" roots ").Append(LandFacts.Money(rootB)).Append(" + ").Append(mesh).Append(" mesh roots ")
                .Append(LandFacts.Money(meshB)).Append(" = ").Append(LandFacts.Money(rootB + meshB)).Append(" between industries, ")
                .Append(balls).Append(" root balls ").Append(LandFacts.Money(ballB)).Append(" own (").Append(land.Year.ToString(LandFacts.Ci))
                .Append(" value added); ").Append(land.Towers.Length).Append(" towers");
            if (land.Towers.Length > 0)
            {
                float gap = MinTowerGap(land, out int a, out int b);
                s.Append(", smallest gap between towers ").Append(LandFacts.Num(gap, 3)).Append(" u");
                if (a >= 0) s.Append(" (").Append(land.Towers[a].Name).Append(" | ").Append(land.Towers[b].Name).Append(')');
            }

            return s.ToString();
        }

        /// <summary>
        /// The smallest arc gap (world, at the smaller radius) between the edges of two neighbouring towers on one ring, and
        /// the pair (tower indices).
        /// </summary>
        static float MinTowerGap(LandGeometry land, out int pa, out int pb)
        {
            pa = pb = -1;
            float best = float.MaxValue;
            for (int i = 0; i < land.Towers.Length; i++)
            {
                for (int j = i + 1; j < land.Towers.Length; j++)
                {
                    TowerGeom a = land.Towers[i], b = land.Towers[j];
                    if (land.Sectors[a.Industry].Tier != land.Sectors[b.Industry].Tier) continue;
                    float r = Mathf.Min(a.R, b.R);
                    float gap = Mathf.Abs(LandMath.DeltaDeg(a.Theta, b.Theta)) * Mathf.Deg2Rad * r - 0.5f * (a.Side + b.Side);
                    if (gap >= best) continue;
                    best = gap;
                    pa = i;
                    pb = j;
                }
            }

            return best;
        }
    }
}
