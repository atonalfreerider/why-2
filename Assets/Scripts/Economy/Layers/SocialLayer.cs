using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using Why.Economy.Data;
using Why.Economy.Land;
using Why.Economy.Model;
using Debug = UnityEngine.Debug;

namespace Why.Economy.Layers
{
    /// <summary>
    /// The society (SPEC 5.5): the season of tit for tat drawn over the players, who never move. Every pair is a tie
    /// arcing between the two players' heads (low along the rim between neighbors, high across the bowl), glowing
    /// (<see cref="EconomyStyle.Cooperate"/>) as the pair cooperates and going out, grey and broken in the middle, where
    /// they feud; defection is the light going out, never red. The coalitions of the round's detection are bands on the
    /// rim's outer margin over each angular run of members, with the members' rivulet-side disc rings brightened and a
    /// generated label per coalition ("Coalition 3 · 28 players · ..."; other runs carry the short name). Signals: pain
    /// flashes a tie and sends a ring from the victim's head, relief runs a white pulse along the tie, satisfaction holds
    /// a tie's glow; a paused round shows the pain of its last three rounds as standing rings. Hovering or selecting a
    /// player recolors its ties by their openings (in-group bright, out-group dim and broken) and dims the rest.
    /// <para>The season shown is the snapshot's (<see cref="LandSnapshot.Society"/>), the betrayal view's
    /// (<see cref="LandSnapshot.Betrayal"/>, computed lazily) or one with a betrayal the viewer queued on a tie
    /// (<see cref="EconomyState.QueueIncident"/>; computed on a worker), at the round <see cref="LandView.Round"/> plays
    /// (4 rounds a second). The tie mesh is rebuilt on the main thread at the round tick (and on a hover, a selection, a
    /// new season); the signals are a small per-frame mesh. A new snapshot (a year, the season's settings) rebuilds the
    /// tie ends and the coalitions on a worker (a preset's blocking snapshot in the next Tick), swaps them in Tick and
    /// cross-fades from the old ones (8.3). Anchors <c>land:coalition:&lt;k&gt;</c>; emphasis groups Ties and Coalitions.
    /// Order 54.</para>
    /// </summary>
    [GraphScenes(GraphScene.Economy)]
    public sealed class SocialLayer : GraphLayer
    {
        public override int Order => 54;

        /// <summary>The name this layer reports its swaps under (<see cref="LandService.ReportReady"/>).</summary>
        const string ReadyName = "SocialLayer";

        // ------------------------------------------------------------------ looks (5.5 in pixels and units)

        /// <summary>
        /// Coalition labels: size (px), priority (7.3), the other runs' short labels, the lift above the band. One line per
        /// coalition (WP6: two labels 27 px apart lost their second line to the label system); the groups and the adults are
        /// in the hover text.
        /// </summary>
        const float LabelPx = 12f, LabelPriority = 22f, RunLabelPx = 11f, RunLabelPriority = 16f, LabelLift = 0.06f;

        /// <summary>
        /// The coalition bands (WP6): a colored arc just outside the rim's edge (radii), its veil and edges (alpha, px,
        /// intensity), the padding of a run beyond its outer members (degrees), the step. The spec's people-blue veil at
        /// alpha 0.10 on the rim's margin did not read; each coalition now has its own color (<see cref="CoalitionColors"/>).
        /// </summary>
        const float BandR0 = LandStyle.RimR + 0.05f, BandR1 = LandStyle.RimR + 0.19f, BandAlpha = 0.5f, BandEdgePx = 1.5f,
            BandEdgeAlpha = 0.95f, BandEdgeIntensity = 1.5f, BandPadDeg = 1.5f, BandStepDeg = 2f;

        /// <summary>
        /// Each coalition's color, by its number (matched across detections, so a coalition keeps its color): teal, magenta,
        /// lime, orange, coral, white. None is the ties' blue or violet, the people's blue, ice, rose or gold.
        /// </summary>
        static readonly Color[] CoalitionColors =
        {
            new Color(0.25f, 0.92f, 0.66f), new Color(0.98f, 0.40f, 0.86f), new Color(0.78f, 0.95f, 0.28f),
            new Color(1.00f, 0.60f, 0.22f), new Color(1.00f, 0.50f, 0.45f), new Color(0.92f, 0.92f, 0.92f)
        };

        /// <summary>A coalition's color (its number, from 0).</summary>
        static Color CoalitionColor(int id) => CoalitionColors[((id % CoalitionColors.Length) + CoalitionColors.Length) % CoalitionColors.Length];

        /// <summary>
        /// The ties drawn (WP6): the strong dealings at the round (Ec(p,q) + Ec(q,p)): a pair among the
        /// <see cref="StrongTies"/> strongest of both its players, or the strongest of either; a focus player's ties, a
        /// betrayal's echo and a selected tie always. The other pairs still play: they are only not drawn (900 arcs over the
        /// rim were one white web, and "the strongest of each" alone tied everyone to the largest players across the bowl).
        /// </summary>
        const int StrongTies = 3;

        /// <summary>
        /// In-group and out-group (WP6): a tie inside a coalition at the round's detection is drawn in a saturated version of
        /// the cooperate blue (hue 214°), a tie across two coalitions (or to a player outside every coalition) in violet,
        /// narrower and dimmer, so the groups the season organizes read as bright blue webs and their dealings across as
        /// thin violet bridges. Feuds stay grey and broken in either.
        /// </summary>
        static readonly Color InGroup = new Color(0.22f, 0.52f, 1.0f), OutGroup = new Color(0.78f, 0.36f, 1.0f);

        /// <summary>An out-group tie's share of the alpha and the width (px) of an in-group tie of the same cooperation.</summary>
        const float OutGroupAlpha = 0.75f, OutGroupPx = 0.7f;

        /// <summary>The ties' legend under the bowl's far rim (society views): size, priority, place (land-local).</summary>
        const float LegendPx = 11f, LegendPriority = 25f;

        static readonly Vector3 LegendLocal = new Vector3(0, LandStyle.RimY + 0.25f, LandStyle.RimR + 0.55f);

        const string LegendText = "Ties (the strongest dealings): blue within a coalition · violet across · grey, broken: feud";

        /// <summary>The legend of a season with a betrayal: the echo's ties are white (<see cref="EchoColor"/>).</summary>
        const string EchoLegendText = LegendText + " · white: the ties the betrayal moved";

        /// <summary>
        /// An in-group tie's intensity stays at or below this (WP6 review): the saturated blue at the satisfied glow's 1.6
        /// bloomed to near-white, and in-group against out-group read as white against violet.
        /// </summary>
        const float InGroupMaxIntensity = 1.2f;

        /// <summary>A betrayal's echo ties: the pain's white, so the echo reads by hue as well as by width and brightness.</summary>
        static readonly Color EchoColor = new Color(1f, 1f, 1f);

        /// <summary>A member's brightened disc ring: the half facing the bowl (its rivulet side).</summary>
        const float BrightPx = 1.2f;

        const int BrightSegments = 10;

        /// <summary>A selected tie: extra width (px), intensity.</summary>
        const float SelectedTiePx = 1.2f, SelectedTieIntensity = 2.2f;

        /// <summary>A hovered or selected player's ties by their opening: bright this far above the stranger level, broken this far below.</summary>
        const float OpeningSpread = 0.08f;

        /// <summary>The relief pulse: its length along the tie (share of the arc) and intensity.</summary>
        const float PulseLength = 0.12f, PulseIntensity = 2.2f;

        /// <summary>A pain ring's width (px).</summary>
        const float RingPx = 2f;

        /// <summary>Segments of a pain ring; rounds a paused season keeps showing the pain of.</summary>
        const int RingSegments = 20, HeldRounds = 3;

        /// <summary>The signals' mesh stays under this many vertices (8.6: ≤ 2K; and its buffers off the large-object heap): pain first, then relief pulses, by tie order.</summary>
        const int MaxEventVertices = 1200;

        /// <summary>A neutral grey for feuds (5.5: never red).</summary>
        static readonly Color FeudColor = new Color(LandStyle.FeudGrey, LandStyle.FeudGrey, LandStyle.FeudGrey);

        static readonly Color SignalWhite = new Color(1f, 1f, 1f);

        /// <summary>Ties blend over each other (alpha) rather than adding light: 900 arcs adding up would wash the bowl white.</summary>
        const bool TiesAdditive = false;

        /// <summary>
        /// A drawn tie's alpha also follows the dealings it carries, Ec(p,q) + Ec(q,p) (2025: median 0.11, 95th percentile
        /// 0.26): from <see cref="DealingsFloor"/> at none to 1 from <see cref="DealingsFull"/>.
        /// </summary>
        const float DealingsFull = 0.25f, DealingsFloor = 0.45f;

        /// <summary>
        /// A betrayal's echo (from its round on): the ties whose cooperation differs from the season without it by at least
        /// <see cref="LandStyle.CalmDelta"/> draw at full alpha and this intensity and extra width; the others at
        /// <see cref="EchoDim"/> of their alpha, so the echo reads against the calm.
        /// </summary>
        const float EchoIntensity = 2f, EchoPx = 0.8f, EchoDim = 0.3f;

        // ------------------------------------------------------------------ what the panel reads

        /// <summary>The season on screen (the default, the betrayal or the viewer's incident season); null before the land.</summary>
        public static SocialSeasonResult Shown { get; private set; }

        /// <summary>The number of coalitions of the shown season at each detection round (the panel's readout).</summary>
        public static int CoalitionsAt(SocialSeasonResult s, int round)
        {
            if (s?.CoalitionAt == null || s.CoalitionAt.Length == 0) return 0;
            int[] labels = s.CoalitionAt[Detection(round, s.CoalitionAt.Length)];
            HashSet<int> ids = new HashSet<int>();
            foreach (int c in labels)
            {
                if (c >= 0) ids.Add(c);
            }

            return ids.Count;
        }

        /// <summary>The detection a round shows: the last one at or before it (rounds 0, 24, 48, 72, 96).</summary>
        static int Detection(int round, int count)
        {
            int[] at = LandStyle.Detections;
            int d = 0;
            for (int i = 0; i < Math.Min(count, at.Length); i++)
            {
                if (round >= at[i]) d = i;
            }

            return d;
        }

        // ------------------------------------------------------------------ built per snapshot and season (pure)

        /// <summary>A coalition's label at one detection: text, land-local place, the main run's or another run's.</summary>
        struct CoalitionLabel
        {
            public int Id;
            public string Text;
            public Vector3 Local;
            public bool Main;
        }

        /// <summary>Everything this layer draws of one snapshot and season, apart from the per-round ties.</summary>
        sealed class Content
        {
            public LandSnapshot Snapshot;
            public SocialSeasonResult Season;
            public int Year;

            /// <summary>Per pair: the two heads (land-local) and the arc's apex height; the stranger level of the openings.</summary>
            public Vector3[] HeadA, HeadB;

            public float[] Apex;
            public float OpeningMean;

            /// <summary>Per round and pair: the tie has cooperated (m ≥ 0.36) for the last 12 rounds (satisfaction's held glow).</summary>
            public bool[][] Satisfied;

            /// <summary>For a season with an incident: the same season without it (the snapshot's), whose ties the echo is measured against.</summary>
            public SocialSeasonResult Baseline;

            /// <summary>Per detection: the bands and the brightened rings (fills, lines) and the labels.</summary>
            public SurfaceMeshBuilder[] Fills;

            public LineMeshBuilder[] Lines;
            public List<CoalitionLabel>[] Labels;

            /// <summary>Per coalition number: the anchor's name, blurb and land-local place (its last detection's main label).</summary>
            public readonly List<(int id, string blurb, Vector3 local)> Anchors = new List<(int, string, Vector3)>();

            public double Ms;

            public int Vertices
            {
                get
                {
                    int v = 0;
                    for (int d = 0; d < Fills.Length; d++) v += Fills[d].VertexCount + Lines[d].VertexCount;
                    return v;
                }
            }
        }

        /// <summary>
        /// The renderers of one content: the ties (rewritten each round), the signals (rewritten per frame while they run,
        /// in a fixed buffer), the bands of the detection shown (uploaded when it is first shown, released when another is:
        /// WP6, the five detections were uploaded at once).
        /// </summary>
        sealed class Generation
        {
            public Content Content;
            public TieBuffer Buffer;
            public SignalBuffer SignalBuffer;
            public MeshRenderer Ties, Signals;
            public MeshRenderer[] Fills, Lines;
            public int Detection = -1;
            public float Fade = 1;
            public bool Out;
        }

        /// <summary>One running signal (playback): pain (tie flash and victim ring) or relief (a pulse), from a start time.</summary>
        struct Effect
        {
            public byte Signal;
            public int Pair;
            public float Start;
        }

        EconomyModel model;
        LandFrame frame;
        LabelSystem labelSystem;
        double nowYear;
        Content prepared;
        readonly List<Generation> generations = new List<Generation>(2);
        Generation current;
        readonly List<LabelSpec> labels = new List<LabelSpec>(16);

        /// <summary>The ties' legend (society views): what the colors and the selection of ties mean.</summary>
        LabelSpec legend;
        readonly Dictionary<int, Anchor> anchors = new Dictionary<int, Anchor>();
        int labelsShownVersion = -1, labelDetection = -1, alignedCam = -1;
        bool labelsDirty = true, alignDirty = true;

        // rebuilds (8.3): the latest snapshot wanted, a worker build, a blocking build for the next Tick
        LandSnapshot wanted;
        Task<Content> building;
        int buildingVersion = -1;

        /// <summary>The worker build of a blocking snapshot, started when it was published (waited for in the next Tick).</summary>
        Task<Content> blockingBuild;

        LandSnapshot blockingFor;

        /// <summary>
        /// A season's content built on a worker (the other season of the view toggle, built ahead; or the viewer's own
        /// betrayal), with the season and snapshot it is for.
        /// </summary>
        Task<Content> seasonBuild;

        SocialSeasonResult seasonFor;
        LandSnapshot seasonSnap;

        /// <summary>
        /// The current snapshot's content of the season not on screen (the betrayal's while the society shows, the society's
        /// while the betrayal does), built ahead on a worker, so a view's change of season swaps it in at once.
        /// </summary>
        Content spare;

        // the viewer's own betrayal (a season with an incident on a tie they chose)
        Task<SocialSeasonResult> incidentTask;
        SocialSeasonResult userSeason;
        string userPreset;
        int betrayalAsked = -1;

        // what the tie mesh was built for
        int tiesRound = -1, tiesFocus = -2, tiesTie = -2;
        SocialSeasonResult tiesSeason;
        Content tiesContent;
        float tieMs, tieMax;
        int tieVertices, tieBuilds;

        /// <summary>The tie rebuilds after the first that are timed and logged once.</summary>
        const int StatBuilds = 10;

        // signals
        readonly List<Effect> effects = new List<Effect>(64);
        float clock;
        int signalsRound = -1;
        bool signalsHeld, signalsDirty = true;
        SocialSeasonResult signalsSeason;

        // reused buffers (main thread)
        readonly List<Vector3> stretch = new List<Vector3>(LandStyle.TiePoints + 1);
        readonly List<Vector3> ring = new List<Vector3>(RingSegments + 1);

        public override void Prepare(GraphContext ctx)
        {
            Stopwatch sw = Stopwatch.StartNew();
            model = ctx.Shared<EconomyModel>(EconomyModel.SharedKey);
            LandSnapshot s = ctx.Shared<LandSnapshot>(LandService.SharedKey);
            nowYear = ctx.NowYear;
            if (model == null || s?.Society?.PairA == null || s.Players?.Players == null)
            {
                Debug.LogWarning("[Why] SocialLayer: no season in the land snapshot, the society is not drawn");
                return;
            }

            frame = EconomyStage.Land();
            labelSystem = ctx.Labels;
            prepared = Build(model.Data, s, s.Society);
            Debug.Log("[Why] SocialLayer.Prepare " + sw.Elapsed.TotalMilliseconds.ToString("0", LandFacts.Ci) + " ms: " +
                      prepared.Vertices.ToString(LandFacts.Ci) + " vertices of coalitions over " + prepared.Fills.Length.ToString(LandFacts.Ci) +
                      " detections (only the shown one uploaded, when shown: " + DetectionRange(prepared) + " vertices), ties up to " +
                      (s.Society.PairA.Length * (LandStyle.TiePoints + 2) * 2).ToString(LandFacts.Ci) + " vertices a round; " +
                      Summary(prepared));
        }

        /// <summary>The fewest and most vertices of one detection's bands and rings ("3612-3790"): what is uploaded at a time.</summary>
        static string DetectionRange(Content c)
        {
            int lo = int.MaxValue, hi = 0;
            for (int d = 0; d < c.Fills.Length; d++)
            {
                int v = c.Fills[d].VertexCount + c.Lines[d].VertexCount;
                lo = Math.Min(lo, v);
                hi = Math.Max(hi, v);
            }

            return c.Fills.Length == 0 ? "0" : lo.ToString(LandFacts.Ci) + "-" + hi.ToString(LandFacts.Ci);
        }

        public override void Upload(GraphContext ctx)
        {
            if (prepared == null) return;
            labelSystem = ctx.Labels;
            legend = new LabelSpec
            {
                Text = LegendText, Fixed = true, FixedRange = 0, SizePx = LegendPx, Color = GraphStyle.TextDim,
                Align = TMPro.TextAlignmentOptions.Center, Priority = LegendPriority, Rank = LegendPriority, Hidden = true,
                Data = frame.World(LegendLocal)
            };
            labelSystem.Add(legend);
            Swap(prepared, 1f);
            prepared = null;
            LandService.Changed += OnChanged;
        }

        void OnDestroy()
        {
            LandService.Changed -= OnChanged;
            foreach (Generation g in generations) Release(g);
            generations.Clear();
            current = null;
            Shown = null;
        }

        /// <summary>A new snapshot is on screen: rebuild for it (blocking ones in the next Tick, others on a worker).</summary>
        void OnChanged(LandSnapshot s)
        {
            if (s?.Society?.PairA == null || model == null) return;
            wanted = s;
            if (!s.Blocking) return;

            // a preset's year: built on a worker from now, waited for in the next Tick (as the land's other layers do)
            EconomyData data = model.Data;
            SocialSeasonResult season = SeasonOf(s);
            blockingFor = s;
            blockingBuild = Task.Run(() => Build(data, s, season));
        }

        // ================================================================== per frame

        public override void Tick(GraphContext ctx, CameraRig rig)
        {
            if (current == null) return;
            float dt = Mathf.Min(0.1f, Time.unscaledDeltaTime);
            clock += dt;
            Rebuild();
            FollowSeason();

            // the cross-fade: the newest generation fades in, older ones out, then go
            for (int k = generations.Count - 1; k >= 0; k--)
            {
                Generation g = generations[k];
                float step = dt / LandStyle.CrossFadeSeconds;
                g.Fade = g.Out ? Mathf.Max(0, g.Fade - step) : Mathf.Min(1, g.Fade + step);
                if (g.Out && g.Fade <= 0)
                {
                    Release(g);
                    generations.RemoveAt(k);
                }
            }

            int round = Mathf.Clamp(LandView.Round, 0, current.Content.Season.Rounds);
            int focus = FocusPlayer();
            int tie = EconomyState.SelectedTie;
            if (round != tiesRound || focus != tiesFocus || tie != tiesTie || !ReferenceEquals(tiesSeason, current.Content.Season) ||
                !ReferenceEquals(tiesContent, current.Content))
            {
                RebuildTies(current, round, focus, tie);
            }

            Signals(round, dt);
            float ties = LandView.Alpha(LandGroup.Ties), coalitions = LandView.Alpha(LandGroup.Coalitions);
            int detection = Detection(round, current.Content.Fills.Length);
            foreach (Generation g in generations)
            {
                Show(g.Ties, ties * g.Fade);
                Show(g.Signals, ties * g.Fade);
                if (g.Fills.Length == 0) continue;
                int shown = Mathf.Min(detection, g.Fills.Length - 1);
                float a = coalitions * g.Fade;
                if (a > 0.003f) ShowDetection(g, shown);
                Show(g.Fills[shown], a);
                Show(g.Lines[shown], a);
            }

            UpdateLabels(detection);
            AlignLabels(rig);
        }

        /// <summary>
        /// Uploads a detection's bands and rings when it is first shown and releases the one shown before (only one detection's
        /// meshes exist at a time: a few thousand vertices instead of all five).
        /// </summary>
        void ShowDetection(Generation g, int d)
        {
            if (g.Detection == d) return;
            if (g.Detection >= 0)
            {
                ReleaseOne(ref g.Fills[g.Detection]);
                ReleaseOne(ref g.Lines[g.Detection]);
            }

            g.Detection = d;
            Content c = g.Content;
            string name = "SocialCoalitions " + c.Year.ToString(LandFacts.Ci) + " d" + d.ToString(LandFacts.Ci);
            if (c.Fills[d].VertexCount > 0) g.Fills[d] = Add(name, c.Fills[d].ToMesh(name), true, LandStyle.QueueGlyphs - 1, false);
            if (c.Lines[d].VertexCount > 0) g.Lines[d] = Add(name + " lines", c.Lines[d].ToMesh(name + " lines"), false, LandStyle.QueueGlyphs + 2, true);
        }

        /// <summary>
        /// Turns each coalition's main label toward the bowl: a label right of the bowl's center (on screen) ends at its
        /// band and runs inward, one left of it starts there, so the long labels stay on screen at both aspects.
        /// </summary>
        void AlignLabels(CameraRig rig)
        {
            Camera cam = rig != null ? rig.Cam : null;
            if (cam == null || rig.Version == alignedCam && !alignDirty) return;
            alignedCam = rig.Version;
            alignDirty = false;
            float center = cam.WorldToScreenPoint(frame.World(new Vector3(0, LandStyle.RimY, 0))).x;
            bool changed = false;
            foreach (LabelSpec spec in labels)
            {
                if (spec.SizePx < LabelPx) continue;   // the runs' short labels stay centered
                TMPro.TextAlignmentOptions align = cam.WorldToScreenPoint(spec.Data).x > center
                    ? TMPro.TextAlignmentOptions.Right
                    : TMPro.TextAlignmentOptions.Left;
                if (spec.Align != align)
                {
                    spec.Align = align;
                    changed = true;
                }

                changed |= LandViewLayer.KeepInFrame(cam, spec, 0, labelSystem != null ? labelSystem.Padding : 4f);
            }

            changed |= LandViewLayer.KeepInFrame(cam, legend, 0, labelSystem != null ? labelSystem.Padding : 4f);
            if (changed) labelSystem?.MarkDirty();
        }

        static void Show(MeshRenderer r, float alpha)
        {
            if (r == null) return;
            bool on = alpha > 0.003f;
            if (r.enabled != on) r.enabled = on;
            if (on) GraphMaterials.SetAlpha(r.sharedMaterial, alpha);
        }

        /// <summary>The player whose ties show their openings: the selected one, else a hovered one (its glyph lit), else -1.</summary>
        int FocusPlayer()
        {
            int n = current.Content.Snapshot.Players.Players.Length;
            int selected = EconomyState.SelectedPlayer;
            if (selected >= 0 && selected < n) return selected;
            if (!Highlighter.HasHighlight) return -1;
            for (int p = 0; p < n; p++)
            {
                if (Highlighter.IsHighlighted(EconomyIds.LandPlayers(p, p))) return p;
            }

            return -1;
        }

        /// <summary>
        /// Follows the snapshot wanted: a blocking one builds here (the Tick after it was published), an asynchronous one
        /// on a worker; a finished build is swapped in only if it is still the latest (stale builds are discarded).
        /// </summary>
        void Rebuild()
        {
            if (building != null && building.IsCompleted)
            {
                Task<Content> t = building;
                building = null;
                if (t.IsFaulted) Debug.LogError("[Why] SocialLayer: a rebuild failed: " + t.Exception?.GetBaseException());
                else if (wanted != null && t.Result.Snapshot.Version == wanted.Version)
                {
                    wanted = null;
                    userSeason = null;
                    Swap(t.Result, 0f);
                    LogRebuild(t.Result);
                }
            }

            if (wanted == null || building != null && buildingVersion == wanted.Version) return;
            if (wanted.Blocking)
            {
                LandSnapshot s = wanted;
                wanted = null;
                userSeason = null;
                Task<Content> prebuilt = ReferenceEquals(blockingFor, s) ? blockingBuild : null;
                blockingBuild = null;
                blockingFor = null;
                Content c = prebuilt != null ? prebuilt.GetAwaiter().GetResult() : Build(model.Data, s, SeasonOf(s));
                Swap(c, 0f);
                LogRebuild(c);
                return;
            }

            if (building != null) return;   // a stale build is still running: the wanted one starts when it ends
            LandSnapshot w = wanted;
            EconomyData data = model.Data;
            SocialSeasonResult season = SeasonOf(w);
            buildingVersion = w.Version;
            building = Task.Run(() => Build(data, w, season));
        }

        static void LogRebuild(Content c) =>
            Debug.Log("[Why] SocialLayer rebuild " + c.Year.ToString(LandFacts.Ci) + ": build " + c.Ms.ToString("0.0", LandFacts.Ci) +
                      " ms, " + c.Vertices.ToString(LandFacts.Ci) + " vertices; " + Summary(c));

        /// <summary>The season a snapshot shows in the current view: the betrayal's in the betrayal view (once computed).</summary>
        static SocialSeasonResult SeasonOf(LandSnapshot s) => LandView.ShowBetrayal && s.Betrayal != null ? s.Betrayal : s.Society;

        /// <summary>
        /// The season on screen follows the view: the betrayal view's (when its lazy season arrives), the default's, or the
        /// viewer's own betrayal (queued on a tie through <see cref="EconomyState.QueueIncident"/>, run on a worker, then
        /// played from the round before it). A season's content is built on a worker: the view's two seasons ahead of need
        /// (<see cref="BuildAhead"/>), the viewer's own when it is asked for.
        /// </summary>
        void FollowSeason()
        {
            Content c = current.Content;
            if (userSeason != null && userPreset != LandView.PresetId) userSeason = null;

            int pair = EconomyState.PendingIncident;
            if (pair >= 0 && incidentTask == null)
            {
                EconomyState.TakeIncident();
                if (pair < c.Season.PairA.Length) StartIncident(c, pair);
            }

            if (incidentTask != null && incidentTask.IsCompleted)
            {
                Task<SocialSeasonResult> t = incidentTask;
                incidentTask = null;
                if (t.IsFaulted) Debug.LogError("[Why] SocialLayer: the betrayal season failed: " + t.Exception?.GetBaseException());
                else if (t.Result != null && t.Result.Year == c.Year && c.Snapshot.Society != null &&
                         LandService.Same(t.Result.Settings, c.Snapshot.Society.Settings))   // a settings change since: dropped
                {
                    userSeason = t.Result;
                    userPreset = LandView.PresetId;

                    // play into the betrayal: on from here when it comes next, from the start when it lies behind
                    if (t.Result.Incident.HasValue && t.Result.Incident.Value.Round <= LandView.Round) LandView.Replay();
                    else LandView.Play(true);
                }
            }

            // the betrayal view's season of a snapshot published while it shows (a settings change): computed on a worker
            if (LandView.ShowBetrayal && c.Snapshot.Betrayal == null && ReferenceEquals(c.Snapshot, LandService.Current) &&
                betrayalAsked != c.Snapshot.Version)
            {
                betrayalAsked = c.Snapshot.Version;   // once per snapshot (a season without a cross-party tie has no betrayal)
                LandService.Betrayal(false);
            }

            // a change of season: the view's seasons (the society's, the betrayal's) are built ahead on a worker and swapped in
            // at once (a preset shows its season from its first frame; waited for if the build is not done yet), the viewer's
            // own betrayal is swapped in when its worker build is done (WP6: these were built here, on the main thread)
            SocialSeasonResult want = userSeason ?? SeasonOf(c.Snapshot);
            if (ReferenceEquals(want, c.Season))
            {
                BuildAhead(c);
                return;
            }

            if (spare != null && ReferenceEquals(spare.Season, want) && ReferenceEquals(spare.Snapshot, c.Snapshot))
            {
                Content s = spare;
                spare = c;
                Swap(s, 0f);
                return;
            }

            if (!ReferenceEquals(seasonFor, want) || !ReferenceEquals(seasonSnap, c.Snapshot)) StartSeason(c.Snapshot, want);
            if (!seasonBuild.IsCompleted && ReferenceEquals(want, userSeason)) return;
            Task<Content> task = seasonBuild;
            seasonBuild = null;
            seasonFor = null;
            seasonSnap = null;
            Content built;
            try
            {
                built = task.GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                Debug.LogError("[Why] SocialLayer: a season's build failed: " + e.GetBaseException());
                return;
            }

            spare = c;
            Swap(built, 0f);
        }

        /// <summary>
        /// Builds the content of the view toggle's other season on a worker (the betrayal's while the society shows, and back)
        /// and keeps it as <see cref="spare"/> when done; nothing while the viewer's own betrayal is on screen.
        /// </summary>
        void BuildAhead(Content c)
        {
            LandSnapshot snap = c.Snapshot;
            if (seasonBuild != null && seasonBuild.IsCompleted && ReferenceEquals(seasonSnap, snap))
            {
                Task<Content> t = seasonBuild;
                seasonBuild = null;
                seasonFor = null;
                seasonSnap = null;
                if (t.IsFaulted) Debug.LogError("[Why] SocialLayer: a season's build failed: " + t.Exception?.GetBaseException());
                else spare = t.Result;
            }

            if (userSeason != null || snap.Betrayal == null) return;
            SocialSeasonResult other = ReferenceEquals(c.Season, snap.Society) ? snap.Betrayal : snap.Society;
            if (!ReferenceEquals(c.Season, snap.Society) && !ReferenceEquals(c.Season, snap.Betrayal)) return;
            if (spare != null && ReferenceEquals(spare.Season, other) && ReferenceEquals(spare.Snapshot, snap)) return;
            if (seasonBuild != null && ReferenceEquals(seasonFor, other) && ReferenceEquals(seasonSnap, snap)) return;
            StartSeason(snap, other);
        }

        /// <summary>Starts a season's content build on a worker (a build for another season or snapshot is left to run out).</summary>
        void StartSeason(LandSnapshot snap, SocialSeasonResult season)
        {
            seasonFor = season;
            seasonSnap = snap;
            EconomyData data = model.Data;
            seasonBuild = Task.Run(() => Build(data, snap, season));
        }

        /// <summary>
        /// Runs the season with the viewer's betrayal on a worker: the selected player betrays its partner on the tie (else
        /// the tie's first player betrays the second) at the next round; at the season's end, at round 48 instead.
        /// </summary>
        void StartIncident(Content c, int pair)
        {
            SocialSeasonResult baseSeason = c.Snapshot.Society;
            int a = baseSeason.PairA[pair], b = baseSeason.PairB[pair];
            int from = EconomyState.SelectedPlayer == b ? b : a, to = from == a ? b : a;
            int round = LandView.Round + 1;
            if (round > baseSeason.Rounds - LandStyle.SatisfiedRounds) round = LandStyle.IncidentRound;
            Incident inc = new Incident { Round = Math.Max(1, round), From = from, To = to };
            LandSnapshot s = c.Snapshot;
            EconomyData data = model.Data;
            SocialSettings settings = baseSeason.Settings;
            incidentTask = Task.Run(() => SocialSeason.Run(data, s.Land, s.Players, settings, s.Year, inc));
        }

        // ================================================================== swaps, renderers

        /// <summary>Puts a content on screen: a new generation (older ones fade out), labels, anchors, the ready report.</summary>
        void Swap(Content c, float fade)
        {
            foreach (Generation g in generations) g.Out = true;
            Generation n = new Generation { Content = c, Fade = fade };
            string year = c.Year.ToString(LandFacts.Ci);
            n.Buffer = new TieBuffer(c.Season.PairA.Length, "SocialTies " + year);
            n.Ties = Add("SocialTies " + year, n.Buffer.Mesh, false, LandStyle.QueueTies, TiesAdditive);
            n.SignalBuffer = new SignalBuffer(SignalCapacity, "SocialSignals " + year);
            n.Signals = Add("SocialSignals " + year, n.SignalBuffer.Mesh, false, LandStyle.QueueTies + 1, true);
            n.Fills = new MeshRenderer[c.Fills.Length];
            n.Lines = new MeshRenderer[c.Lines.Length];

            generations.Add(n);
            current = n;
            Shown = c.Season;
            tiesRound = -1;
            signalsDirty = true;
            effects.Clear();
            ApplyAnchors(c);
            labelsDirty = true;
            LandService.ReportReady(ReadyName, c.Snapshot.Version);
        }

        MeshRenderer Add(string name, Mesh mesh, bool surface, int queue, bool additive)
        {
            Material m = GraphMaterials.Raw(surface
                ? GraphMaterials.Surface(Color.white, 1f, queue)
                : GraphMaterials.Line(Color.white, 1f, queue, additive));
            MeshRenderer r = AddMesh(name, mesh, m);
            frame.Place(r.transform);
            GraphMaterials.SetAlpha(m, 0);
            r.enabled = false;
            return r;
        }

        static void Release(Generation g)
        {
            ReleaseOne(ref g.Ties);
            ReleaseOne(ref g.Signals);
            for (int d = 0; d < g.Fills.Length; d++)
            {
                ReleaseOne(ref g.Fills[d]);
                ReleaseOne(ref g.Lines[d]);
            }
        }

        /// <summary>Destroys one renderer with its mesh and material (null-safe) and clears the reference.</summary>
        static void ReleaseOne(ref MeshRenderer r)
        {
            if (r == null) return;
            MeshFilter f = r.GetComponent<MeshFilter>();
            if (f != null && f.sharedMesh != null) Destroy(f.sharedMesh);
            if (r.sharedMaterial != null) Destroy(r.sharedMaterial);
            Destroy(r.gameObject);
            r = null;
        }

        // ================================================================== the tie mesh's buffers

        /// <summary>
        /// One vertex of the Why/Line shader, in the layout <see cref="LineMeshBuilder"/> writes (position, color, the
        /// polyline's previous and next points, side / width px / width world / id, intensity / flow).
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        struct TieVertex
        {
            public Vector3 Pos;
            public Color32 Color;
            public Vector3 Prev, Next;
            public Vector4 P;
            public Vector2 Q;
        }

        static readonly VertexAttributeDescriptor[] TieLayout =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord3, VertexAttributeFormat.Float32, 2),
        };

        /// <summary>
        /// The tie mesh, kept for a season's pairs and rewritten in place at each round tick (8.6: the 4 Hz rebuild then
        /// allocates nothing; a new LineMeshBuilder per round would put 2 MB a round on the large-object heap). Every tie
        /// is two polylines of <see cref="HalfPoints"/> points (its halves, meeting in the middle, or apart by a feud's
        /// gap), so the index buffer never changes: about 26K vertices for 916 pairs.
        /// </summary>
        sealed class TieBuffer
        {
            public const int HalfPoints = LandStyle.TiePoints / 2 + 1, PerTie = 4 * HalfPoints;
            public readonly Mesh Mesh;
            readonly TieVertex[] v;

            public int Vertices => v.Length;

            public TieBuffer(int ties, string name)
            {
                v = new TieVertex[Math.Max(1, ties) * PerTie];
                int[] idx = new int[Math.Max(1, ties) * 2 * (HalfPoints - 1) * 6];
                int n = 0;
                for (int half = 0; half < 2 * Math.Max(1, ties); half++)
                {
                    int b = half * 2 * HalfPoints;
                    for (int i = 0; i < HalfPoints - 1; i++)
                    {
                        int a = b + 2 * i;
                        idx[n++] = a;
                        idx[n++] = a + 1;
                        idx[n++] = a + 2;
                        idx[n++] = a + 2;
                        idx[n++] = a + 1;
                        idx[n++] = a + 3;
                    }
                }

                MeshUpdateFlags flags = MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices |
                                        MeshUpdateFlags.DontNotifyMeshUsers;
                Mesh = new Mesh { name = name };
                Mesh.MarkDynamic();
                Mesh.SetVertexBufferParams(v.Length, TieLayout);
                Mesh.SetVertexBufferData(v, 0, 0, v.Length, 0, flags);
                Mesh.SetIndexBufferParams(idx.Length, IndexFormat.UInt32);
                Mesh.SetIndexBufferData(idx, 0, 0, idx.Length, flags);
                Mesh.subMeshCount = 1;
                Mesh.SetSubMesh(0, new SubMeshDescriptor(0, idx.Length), flags);
                Mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1e6f);   // the shader places the vertices: never cull on the CPU
            }

            /// <summary>Writes a tie: its two halves, the middle <paramref name="gap"/> of the arc left out, in one style.</summary>
            public void Write(Content c, int k, float gap, Color32 color, float px, float intensity, int id)
            {
                gap = Mathf.Clamp(gap, 0, 0.9f);
                Half(c, k, k * PerTie, 0f, 0.5f - gap / 2, color, px, intensity, id);
                Half(c, k, k * PerTie + 2 * HalfPoints, 0.5f + gap / 2, 1f, color, px, intensity, id);
            }

            void Half(Content c, int k, int at, float t0, float t1, Color32 color, float px, float intensity, int id)
            {
                Vector3 prev = ArcPoint(c, k, t0), cur = prev;
                for (int i = 0; i < HalfPoints; i++)
                {
                    Vector3 next = i + 1 < HalfPoints ? ArcPoint(c, k, Mathf.Lerp(t0, t1, (i + 1) / (float)(HalfPoints - 1))) : cur;
                    TieVertex vx = new TieVertex
                    {
                        Pos = cur, Color = color, Prev = prev, Next = next, P = new Vector4(-1, px, 0, id), Q = new Vector2(intensity, 0)
                    };
                    v[at + 2 * i] = vx;
                    vx.P.x = 1;
                    v[at + 2 * i + 1] = vx;
                    prev = cur;
                    cur = next;
                }
            }

            /// <summary>Sends the rewritten vertices to the mesh.</summary>
            public void Upload() =>
                Mesh.SetVertexBufferData(v, 0, 0, v.Length, 0,
                    MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers);
        }

        /// <summary>The signals' buffer holds this many vertices: the mesh's cap plus one pain mark beyond it.</summary>
        const int SignalCapacity = MaxEventVertices + 2 * (LandStyle.TiePoints + 1) + 4 * (RingSegments + 1);

        /// <summary>
        /// The signals' mesh (WP6): one vertex and index buffer of fixed capacity, rewritten in place while signals run and
        /// drawn up to the indices written (its submesh), so the per-frame rebuild allocates nothing (it built a new
        /// <see cref="LineMeshBuilder"/> and mesh every frame). Polylines are written as <see cref="LineMeshBuilder"/> writes them.
        /// </summary>
        sealed class SignalBuffer
        {
            public readonly Mesh Mesh;
            readonly TieVertex[] v;
            readonly int[] idx;
            int vertices, indices;

            public int VertexCount => vertices;

            public SignalBuffer(int capacity, string name)
            {
                v = new TieVertex[capacity];
                idx = new int[capacity * 3];
                Mesh = new Mesh { name = name };
                Mesh.MarkDynamic();
                Mesh.SetVertexBufferParams(v.Length, TieLayout);
                Mesh.SetIndexBufferParams(idx.Length, IndexFormat.UInt32);
                Mesh.subMeshCount = 1;
                Mesh.SetSubMesh(0, new SubMeshDescriptor(0, 0), Flags);
                Mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1e6f);   // the shader places the vertices: never cull on the CPU
            }

            const MeshUpdateFlags Flags = MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices |
                                          MeshUpdateFlags.DontNotifyMeshUsers;

            /// <summary>Starts a new frame's signals.</summary>
            public void Clear() => vertices = indices = 0;

            /// <summary>A polyline in one style (dropped when the buffer is full).</summary>
            public void AddPolyline(List<Vector3> points, Color32 color, float px, float id, float intensity)
            {
                int n = points.Count;
                if (n < 2 || vertices + 2 * n > v.Length || indices + 6 * (n - 1) > idx.Length) return;
                int b = vertices;
                for (int i = 0; i < n; i++)
                {
                    TieVertex vx = new TieVertex
                    {
                        Pos = points[i], Color = color, Prev = points[i > 0 ? i - 1 : i], Next = points[i < n - 1 ? i + 1 : i],
                        P = new Vector4(-1, px, 0, id), Q = new Vector2(intensity, 0)
                    };
                    v[vertices++] = vx;
                    vx.P.x = 1;
                    v[vertices++] = vx;
                }

                for (int i = 0; i < n - 1; i++)
                {
                    int a = b + 2 * i;
                    idx[indices++] = a;
                    idx[indices++] = a + 1;
                    idx[indices++] = a + 2;
                    idx[indices++] = a + 2;
                    idx[indices++] = a + 1;
                    idx[indices++] = a + 3;
                }
            }

            /// <summary>Sends what was written to the mesh and draws only that.</summary>
            public void Upload()
            {
                if (vertices > 0) Mesh.SetVertexBufferData(v, 0, 0, vertices, 0, Flags);
                if (indices > 0) Mesh.SetIndexBufferData(idx, 0, 0, indices, Flags);
                Mesh.SetSubMesh(0, new SubMeshDescriptor(0, indices), Flags);
            }
        }

        // ================================================================== the ties (main thread, at the round tick)

        /// <summary>
        /// The tie mesh of a round (5.5, WP6): the strong ties (<see cref="StrongTies"/> per player by dealings), each an arc
        /// between the two heads styled by its mutual cooperation m = x·x' (cooperating m ≥ 0.36: alpha 0.15 + 0.6 m,
        /// intensity 1 + m, held at 1.6 once satisfied; feuding m &lt; 0.12: grey, broken in the middle, alpha 0.35,
        /// intensity 0.6; between: blended), width 0.8 + 1.6 m px, in the cooperate blue inside a coalition and violet
        /// across (narrower, dimmer); the other pairs are written invisible. With a focus player, all its ties by their
        /// openings and the rest dimmed to 0.15; a betrayal's echo and a selected tie are always drawn.
        /// </summary>
        void RebuildTies(Generation g, int round, int focus, int selectedTie)
        {
            Stopwatch sw = Stopwatch.StartNew();
            Content c = g.Content;
            SocialSeasonResult s = c.Season;
            int m = s.PairA.Length;
            float[] ab = s.CoopAB[round], ba = s.CoopBA[round], exposure = s.Exposure[round];
            int[] coalition = s.CoalitionAt[Detection(round, s.CoalitionAt.Length)];
            MarkStrong(s, exposure, c.Snapshot.Players.Players.Length);

            // a betrayal's echo against the season without it, from the incident's round on
            SocialSeasonResult baseline = c.Baseline;
            bool echoing = baseline != null && s.Incident.HasValue && round >= s.Incident.Value.Round;
            float[] b0 = echoing ? baseline.CoopAB[round] : null, ba0 = echoing ? baseline.CoopBA[round] : null;
            int incidentPair = echoing ? PairOf(s, s.Incident.Value.From, s.Incident.Value.To) : -1;
            TieBuffer buffer = g.Buffer;
            int drawn = 0;
            for (int k = 0; k < m; k++)
            {
                float mutual = ab[k] * ba[k];
                bool mine = focus >= 0 && (s.PairA[k] == focus || s.PairB[k] == focus);
                bool hit = echoing && focus < 0 &&
                           (k == incidentPair || Mathf.Max(Mathf.Abs(ab[k] - b0[k]), Mathf.Abs(ba[k] - ba0[k])) >= LandStyle.CalmDelta);
                if (!strong[k] && !mine && !hit && k != selectedTie)
                {
                    buffer.Write(c, k, 0, default, 0, 0, EconomyIds.LandTie(k));
                    continue;
                }

                drawn++;
                float dim = focus >= 0 && !mine ? LandStyle.SelectDim : 1f;
                int ca = coalition[s.PairA[k]];
                bool inGroup = ca >= 0 && ca == coalition[s.PairB[k]];
                float state; // 0 feud .. 1 cooperating
                Color hue = inGroup ? InGroup : OutGroup;
                if (mine)
                {
                    float opening = s.PairA[k] == focus ? s.Opening[0][k] : s.Opening[1][k];
                    state = Mathf.Clamp01((opening - (c.OpeningMean - OpeningSpread)) / (2 * OpeningSpread));
                    mutual = Mathf.Lerp(LandStyle.FeudMutual, 1f, state);
                    hue = EconomyStyle.Cooperate;
                }
                else
                {
                    state = Mathf.Clamp01((mutual - LandStyle.FeudMutual) / (LandStyle.CooperateMutual - LandStyle.FeudMutual));
                }

                Color color = Color.Lerp(FeudColor, hue, state);
                float dealings = Mathf.Lerp(DealingsFloor, 1f, Mathf.Clamp01(exposure[k] / DealingsFull));
                float alpha = Mathf.Lerp(LandStyle.FeudAlpha, LandStyle.TieAlpha0 + LandStyle.TieAlphaMutual * mutual, state) * dim * dealings;
                float intensity = Mathf.Lerp(LandStyle.FeudIntensity, LandStyle.TieIntensity0 + LandStyle.TieIntensityMutual * mutual, state);
                if (!mine && state >= 1f && c.Satisfied[round][k]) intensity = Mathf.Max(intensity, LandStyle.SatisfiedIntensity);
                if (!mine && inGroup) intensity = Mathf.Min(intensity, InGroupMaxIntensity);
                float px = LandStyle.TieBasePx + LandStyle.TieMutualPx * mutual;
                if (!mine && !inGroup)
                {
                    alpha *= OutGroupAlpha;
                    px *= OutGroupPx;
                }

                if (echoing && focus < 0)
                {
                    if (hit)
                    {
                        color = Color.Lerp(FeudColor, EchoColor, Mathf.Max(state, 0.5f));
                        alpha = Mathf.Max(alpha, 0.8f);
                        intensity = Mathf.Max(intensity, EchoIntensity);
                        px += EchoPx;
                    }
                    else alpha *= EchoDim;
                }

                if (k == selectedTie)
                {
                    px += SelectedTiePx;
                    intensity = SelectedTieIntensity;
                    alpha = 1f;
                    color = EconomyStyle.Cooperate;
                }

                buffer.Write(c, k, LandStyle.FeudGap * (1 - state), LandMath.Tint(color, alpha), px, intensity, EconomyIds.LandTie(k));
            }

            buffer.Upload();
            tiesRound = round;
            tiesFocus = focus;
            tiesTie = selectedTie;
            tiesSeason = s;
            tiesContent = c;
            float ms = (float)sw.Elapsed.TotalMilliseconds;
            tieVertices = buffer.Vertices;
            if (tieBuilds++ == 0)
            {
                Debug.Log("[Why] SocialLayer ties r" + round.ToString(LandFacts.Ci) + ": " + drawn.ToString(LandFacts.Ci) + " of " +
                          m.ToString(LandFacts.Ci) + " pairs drawn (each player's strongest dealing and the pairs among the " + StrongTies.ToString(LandFacts.Ci) +
                          " strongest of both), " + tieVertices.ToString(LandFacts.Ci) + " vertices, " +
                          ms.ToString("0.00", LandFacts.Ci) + " ms on the main thread (the first build)");
            }
            else if (tieBuilds <= 1 + StatBuilds)
            {
                // the cost at the round tick, once: the builds after the first (which pays for the code's first run)
                tieMs += ms;
                tieMax = Mathf.Max(tieMax, ms);
                if (tieBuilds == 1 + StatBuilds)
                {
                    Debug.Log("[Why] SocialLayer ties: " + StatBuilds.ToString(LandFacts.Ci) + " rebuilds, mean " +
                              (tieMs / StatBuilds).ToString("0.00", LandFacts.Ci) + " ms, max " + tieMax.ToString("0.00", LandFacts.Ci) +
                              " ms, " + tieVertices.ToString(LandFacts.Ci) + " vertices (budget 2.5 ms, 30K)");
                }
            }
        }

        /// <summary>Per pair: drawn as a strong tie at the round (<see cref="MarkStrong"/>; reused, main thread).</summary>
        bool[] strong = Array.Empty<bool>();

        /// <summary>
        /// Per player, its <see cref="StrongTies"/> strongest pairs at the round and their dealings, strongest first
        /// (player p's at p × StrongTies; reused, main thread).
        /// </summary>
        int[] topPair = Array.Empty<int>();

        float[] topValue = Array.Empty<float>();

        /// <summary>
        /// Marks each player's <see cref="StrongTies"/> strongest pairs by the round's dealings (ties by the lower pair
        /// index) in <see cref="strong"/>: one pass over the pairs, an insertion into each end's short list.
        /// </summary>
        void MarkStrong(SocialSeasonResult s, float[] exposure, int players)
        {
            int m = s.PairA.Length;
            if (strong.Length < m) strong = new bool[m];
            if (topPair.Length < players * StrongTies)
            {
                topPair = new int[players * StrongTies];
                topValue = new float[players * StrongTies];
            }

            for (int i = 0; i < players * StrongTies; i++)
            {
                topPair[i] = -1;
                topValue[i] = -1;
            }

            for (int k = 0; k < m; k++)
            {
                strong[k] = false;
                Offer(s.PairA[k], k, exposure[k], players);
                Offer(s.PairB[k], k, exposure[k], players);
            }

            // the strongest of either player, and pairs among the strongest of both
            for (int p = 0; p < players; p++)
            {
                if (topPair[p * StrongTies] >= 0) strong[topPair[p * StrongTies]] = true;
            }

            for (int k = 0; k < m; k++)
            {
                if (!strong[k] && InTop(s.PairA[k], k, players) && InTop(s.PairB[k], k, players)) strong[k] = true;
            }
        }

        /// <summary>Whether a pair is among a player's strongest.</summary>
        bool InTop(int player, int pair, int players)
        {
            if (player < 0 || player >= players) return false;
            for (int i = player * StrongTies; i < (player + 1) * StrongTies; i++)
            {
                if (topPair[i] == pair) return true;
            }

            return false;
        }

        /// <summary>Offers a pair to a player's list of strongest: kept when stronger than its weakest (earlier pairs win ties).</summary>
        void Offer(int player, int pair, float value, int players)
        {
            if (player < 0 || player >= players) return;
            int at = player * StrongTies, last = at + StrongTies - 1;
            if (value <= topValue[last]) return;
            int i = last;
            while (i > at && topValue[i - 1] < value)
            {
                topValue[i] = topValue[i - 1];
                topPair[i] = topPair[i - 1];
                i--;
            }

            topValue[i] = value;
            topPair[i] = pair;
        }

        /// <summary>The pair index of two players (either order), or -1.</summary>
        static int PairOf(SocialSeasonResult s, int p, int q)
        {
            int a = Math.Min(p, q), b = Math.Max(p, q);
            for (int k = 0; k < s.PairA.Length; k++)
            {
                if (s.PairA[k] == a && s.PairB[k] == b) return k;
            }

            return -1;
        }

        /// <summary>A stretch [t0, t1] of a tie's arc as one polyline of n segments.</summary>
        void Arc(SignalBuffer b, Content c, int k, float t0, float t1, int n, Color32 color, float px, float intensity, int id)
        {
            stretch.Clear();
            for (int i = 0; i <= n; i++) stretch.Add(ArcPoint(c, k, Mathf.Lerp(t0, t1, i / (float)n)));
            b.AddPolyline(stretch, color, px, id, intensity);
        }

        /// <summary>A point of a tie's arc: the chord between the heads lifted by a parabola to the apex at its middle.</summary>
        static Vector3 ArcPoint(Content c, int k, float t)
        {
            Vector3 a = c.HeadA[k], b = c.HeadB[k];
            Vector3 p = Vector3.Lerp(a, b, t);
            p.y += 4 * t * (1 - t) * (c.Apex[k] - 0.5f * (a.y + b.y));
            return p;
        }

        // ================================================================== signals (per frame while they run)

        /// <summary>
        /// Pain, relief and satisfaction (5.5). While the season plays, each round's events start their animations (pain:
        /// the tie flashes 3 → 1 over 0.5 s and a ring grows from the victim's head; relief: a white pulse runs along the
        /// tie in 1 s); a paused round shows the pain of its last <see cref="HeldRounds"/> rounds as standing rings, the
        /// newest brightest. Satisfaction is the ties' held glow (<see cref="RebuildTies"/>).
        /// </summary>
        void Signals(int round, float dt)
        {
            SocialSeasonResult s = current.Content.Season;
            bool held = !LandView.Playing;
            if (!ReferenceEquals(s, signalsSeason))
            {
                signalsSeason = s;
                signalsRound = -1;
                effects.Clear();
                signalsDirty = true;
            }

            if (round != signalsRound)
            {
                if (!held && round == signalsRound + 1) Start(s, round);
                else effects.Clear();
                signalsRound = round;
                signalsDirty = true;
            }

            if (held != signalsHeld)
            {
                signalsHeld = held;
                signalsDirty = true;
            }

            for (int i = effects.Count - 1; i >= 0; i--)
            {
                float life = effects[i].Signal == 0 ? LandStyle.PainSeconds : LandStyle.ReliefSeconds;
                if (clock - effects[i].Start > life) effects.RemoveAt(i);
            }

            if (!held && effects.Count == 0 && !signalsDirty) return;
            if (held && !signalsDirty) return;
            signalsDirty = !held && effects.Count > 0;

            SignalBuffer b = current.SignalBuffer;
            b.Clear();
            Content c = current.Content;
            if (held)
            {
                for (int age = 0; age < HeldRounds; age++)
                {
                    int t = round - age;
                    if (t < 1) break;
                    foreach ((int r, int pair, byte signal) e in EventsAt(s, t))
                    {
                        if (e.signal != 0 || b.VertexCount > MaxEventVertices) continue;
                        float weight = 1f - age / (float)HeldRounds;
                        DrawPain(b, c, s, e.pair, t, 0.55f + 0.15f * age, weight);
                    }
                }
            }
            else
            {
                foreach (Effect e in effects)
                {
                    if (b.VertexCount > MaxEventVertices) break;
                    float age = clock - e.Start;
                    if (e.Signal == 0) DrawPain(b, c, s, e.Pair, signalsRound, age / LandStyle.PainSeconds, 1f - age / LandStyle.PainSeconds);
                    else
                    {
                        float at = Mathf.Clamp01(age / LandStyle.ReliefSeconds);
                        float t0 = Mathf.Max(0, at - PulseLength), t1 = Mathf.Min(1, at + PulseLength * 0.25f);
                        if (t1 > t0) Arc(b, c, e.Pair, t0, t1, 2, LandMath.Tint(SignalWhite, 0.8f * (1 - at * 0.5f)), 2f, PulseIntensity,
                            EconomyIds.LandTie(e.Pair));
                    }
                }
            }

            b.Upload();
        }

        /// <summary>Starts the animations of a round's events: pain first (all), then relief pulses while the mesh has room.</summary>
        void Start(SocialSeasonResult s, int round)
        {
            int budget = MaxEventVertices / 8;
            foreach ((int r, int pair, byte signal) e in EventsAt(s, round))
            {
                if (e.signal == 0) effects.Add(new Effect { Signal = 0, Pair = e.pair, Start = clock });
            }

            foreach ((int r, int pair, byte signal) e in EventsAt(s, round))
            {
                if (e.signal != 1 || effects.Count >= budget) continue;
                effects.Add(new Effect { Signal = 1, Pair = e.pair, Start = clock });
            }
        }

        /// <summary>The events of one round (the list is in round order).</summary>
        static IEnumerable<(int round, int pair, byte signal)> EventsAt(SocialSeasonResult s, int round)
        {
            List<(int round, int pair, byte signal)> list = s.Events;
            int lo = 0, hi = list.Count;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (list[mid].round < round) lo = mid + 1;
                else hi = mid;
            }

            for (int i = lo; i < list.Count && list[i].round == round; i++) yield return list[i];
        }

        /// <summary>
        /// Pain on a tie: the arc flashed (intensity from 3 down to 1 with <paramref name="progress"/>) and a ring around each
        /// victim's head (the player whose partner's cooperation fell), its radius growing with progress, alpha by weight.
        /// </summary>
        void DrawPain(SignalBuffer b, Content c, SocialSeasonResult s, int pair, int round, float progress, float weight)
        {
            progress = Mathf.Clamp01(progress);
            weight = Mathf.Clamp01(weight);
            float intensity = Mathf.Lerp(LandStyle.PainIntensity0, LandStyle.PainIntensity1, progress);
            int id = EconomyIds.LandTie(pair);
            Arc(b, c, pair, 0f, 1f, LandStyle.TiePoints, LandMath.Tint(EconomyStyle.Cooperate, 0.9f * weight), 1.6f, intensity, id);
            int prev = Math.Max(0, round - 1);
            bool aFell = s.CoopAB[prev][pair] - s.CoopAB[round][pair] >= LandStyle.PainDrop;   // A's cooperation fell: B is hurt
            bool bFell = s.CoopBA[prev][pair] - s.CoopBA[round][pair] >= LandStyle.PainDrop;
            float radius = LandStyle.PainRing * Mathf.Max(0.08f, progress);
            Color32 col = LandMath.Tint(SignalWhite, LandStyle.PainRingAlpha * weight * (1 - 0.5f * progress));
            if (aFell) Ring(b, c.HeadB[pair], radius, col, id);
            if (bFell) Ring(b, c.HeadA[pair], radius, col, id);
        }

        void Ring(SignalBuffer b, Vector3 center, float radius, Color32 color, int id)
        {
            ring.Clear();
            for (int i = 0; i <= RingSegments; i++)
            {
                float a = 2 * Mathf.PI * i / RingSegments;
                ring.Add(new Vector3(center.x + radius * Mathf.Cos(a), center.y, center.z + radius * Mathf.Sin(a)));
            }

            b.AddPolyline(ring, color, RingPx, id, 1.6f);
        }

        // ================================================================== labels and anchors (main thread)

        /// <summary>
        /// Shows the coalition labels of the round's detection while the preset lists the coalitions' labels (7.2): the
        /// label specs are a pool reused across detections and years (the label system keeps every spec it was given).
        /// </summary>
        void UpdateLabels(int detection)
        {
            Content c = current.Content;
            detection = Mathf.Min(detection, c.Labels.Length - 1);
            if (!labelsDirty && detection == labelDetection && labelsShownVersion == LandView.LabelsVersion) return;
            bool textChanged = labelsDirty || detection != labelDetection;
            labelsDirty = false;
            alignDirty = true;
            labelDetection = detection;
            labelsShownVersion = LandView.LabelsVersion;
            bool shown = LandView.LabelsShown(LandGroup.Coalitions);
            if (legend != null)
            {
                legend.Hidden = !shown;
                string text = c.Season.Incident.HasValue ? EchoLegendText : LegendText;
                if (legend.Text != text)
                {
                    if (labelSystem != null) labelSystem.SetText(legend, text);
                    else legend.Text = text;
                }
            }
            List<CoalitionLabel> list = detection >= 0 ? c.Labels[detection] : new List<CoalitionLabel>();
            for (int i = 0; i < Math.Max(list.Count, labels.Count); i++)
            {
                if (i >= list.Count)
                {
                    labels[i].Hidden = true;
                    continue;
                }

                CoalitionLabel cl = list[i];
                LabelSpec spec;
                if (i < labels.Count) spec = labels[i];
                else
                {
                    spec = new LabelSpec
                    {
                        Text = cl.Text, Fixed = true, FixedRange = 0, SizePx = cl.Main ? LabelPx : RunLabelPx, Color = GraphStyle.Text,
                        Align = TMPro.TextAlignmentOptions.Center, Hidden = true
                    };
                    labels.Add(spec);
                    labelSystem?.Add(spec);
                }

                if (textChanged)
                {
                    if (labelSystem != null) labelSystem.SetText(spec, cl.Text);
                    else spec.Text = cl.Text;
                    spec.SizePx = cl.Main ? LabelPx : RunLabelPx;
                    spec.Data = frame.World(cl.Local);
                    spec.Priority = cl.Main ? LabelPriority : RunLabelPriority;
                    spec.Rank = cl.Main ? 100 - cl.Id : 50 - cl.Id;
                    spec.Color = Color.Lerp(CoalitionColor(cl.Id), Color.white, LabelWhiten);
                    spec.AnchorKey = AnchorKey(cl.Id);
                    spec.Ids = IdRange.Single(EconomyIds.LandCoalition(cl.Id));
                }

                spec.Hidden = !shown;
            }

            labelSystem?.MarkDirty();
        }

        /// <summary>A coalition label's text is its color lightened this much toward white (read on the dark sky).</summary>
        const float LabelWhiten = 0.35f;

        static string AnchorKey(int id) => "land:coalition:" + (id + 1).ToString(LandFacts.Ci);

        /// <summary>Registers (once) and updates the coalitions' anchors: name, blurb, place, ids.</summary>
        void ApplyAnchors(Content c)
        {
            foreach ((int id, string blurb, Vector3 local) a in c.Anchors)
            {
                if (!anchors.TryGetValue(a.id, out Anchor an))
                {
                    an = new Anchor { Key = AnchorKey(a.id), Level = GraphLevel.Humans, Tier = 2 };
                    anchors[a.id] = an;
                    Anchors.Register(an);
                }

                an.Label = "Coalition " + (a.id + 1).ToString(LandFacts.Ci);
                an.Blurb = a.blurb;
                an.Ids = IdRange.Single(EconomyIds.LandCoalition(a.id));
                an.Fixed = frame.World(a.local);
                an.Y = a.local.y;
                an.Rho = EconomyStyle.FramingRho;
                an.YearsAgo = an.EndYearsAgo = Math.Max(0, nowYear - (c.Year + 0.5));
            }
        }

        // ================================================================== the build (pure: any thread)

        /// <summary>The ends and apexes of every tie, the coalitions' bands, rings and labels at every detection, the anchors.</summary>
        static Content Build(EconomyData data, LandSnapshot snap, SocialSeasonResult season)
        {
            Stopwatch sw = Stopwatch.StartNew();
            Player[] ps = snap.Players.Players;
            int m = season.PairA.Length;
            Content c = new Content
            {
                Snapshot = snap, Season = season, Year = snap.Year, HeadA = new Vector3[m], HeadB = new Vector3[m], Apex = new float[m]
            };
            double open = 0;
            for (int k = 0; k < m; k++)
            {
                Vector3 a = Figure.Head(ps[season.PairA[k]]), b = Figure.Head(ps[season.PairB[k]]);
                c.HeadA[k] = a;
                c.HeadB[k] = b;
                float chord = Vector3.Distance(new Vector3(a.x, 0, a.z), new Vector3(b.x, 0, b.z));
                c.Apex[k] = Mathf.Max(a.y, b.y) + LandStyle.TieLift + LandStyle.TieSlope * chord;
                open += season.Opening[0][k] + season.Opening[1][k];
            }

            c.OpeningMean = m > 0 ? (float)(open / (2 * m)) : 0.3f;
            int[] held = new int[m];
            c.Satisfied = new bool[season.Rounds + 1][];
            for (int t = 0; t <= season.Rounds; t++)
            {
                c.Satisfied[t] = new bool[m];
                for (int k = 0; k < m; k++)
                {
                    held[k] = season.CoopAB[t][k] * season.CoopBA[t][k] >= LandStyle.CooperateMutual ? held[k] + 1 : 0;
                    c.Satisfied[t][k] = held[k] >= LandStyle.SatisfiedRounds;
                }
            }
            if (season.Incident.HasValue && snap.Society != null && !ReferenceEquals(snap.Society, season) && snap.Society.PairA.Length == m)
            {
                c.Baseline = snap.Society;
            }

            int detections = season.CoalitionAt?.Length ?? 0;
            c.Fills = new SurfaceMeshBuilder[detections];
            c.Lines = new LineMeshBuilder[detections];
            c.Labels = new List<CoalitionLabel>[detections];
            Dictionary<int, (string, Vector3)> lastSeen = new Dictionary<int, (string, Vector3)>();
            for (int d = 0; d < detections; d++)
            {
                c.Fills[d] = new SurfaceMeshBuilder();
                c.Lines[d] = new LineMeshBuilder(512);
                c.Labels[d] = new List<CoalitionLabel>();
                int round = Math.Min(season.Rounds, LandStyle.Detections[Math.Min(d, LandStyle.Detections.Length - 1)]);
                Coalitions(c, d, round, data, snap, season, lastSeen);
            }

            List<int> ids = new List<int>(lastSeen.Keys);
            ids.Sort();
            foreach (int id in ids) c.Anchors.Add((id, lastSeen[id].Item1, lastSeen[id].Item2));
            c.Ms = sw.Elapsed.TotalMilliseconds;
            return c;
        }

        /// <summary>
        /// One detection's coalitions: for each, its rim members by angle split into runs (consecutive members within
        /// <see cref="LandStyle.CoalitionRunDeg"/>), a band over each run on the rim's outer margin, the members' brightened
        /// rings, the label at the largest run's people-weighted center and short labels on the other runs of 3 or more.
        /// </summary>
        static void Coalitions(Content c, int d, int round, EconomyData data, LandSnapshot snap, SocialSeasonResult season,
            Dictionary<int, (string, Vector3)> lastSeen)
        {
            Player[] ps = snap.Players.Players;
            int[] label = season.CoalitionAt[d];
            SortedDictionary<int, List<int>> members = new SortedDictionary<int, List<int>>();
            for (int p = 0; p < label.Length && p < ps.Length; p++)
            {
                if (label[p] < 0) continue;
                if (!members.TryGetValue(label[p], out List<int> list)) members[label[p]] = list = new List<int>();
                list.Add(p);
            }

            List<Vector3> inner = new List<Vector3>(64), outer = new List<Vector3>(64), arc = new List<Vector3>(64);
            List<Color32> colors = new List<Color32>(1);
            float y = LandStyle.RimY + 0.004f;
            foreach (KeyValuePair<int, List<int>> kv in members)
            {
                int id = kv.Key;
                float cid = EconomyIds.LandCoalition(id);
                Color hue = CoalitionColor(id);
                Color32 edge = LandMath.Tint(hue, BandEdgeAlpha), bright = LandMath.Tint(hue, 0.9f);
                colors.Clear();
                colors.Add(LandMath.Tint(hue, BandAlpha));
                List<int> rim = kv.Value.FindAll(p => ps[p].Place == Place.Rim);
                rim.Sort((a, b) => ps[a].Theta != ps[b].Theta ? ps[a].Theta.CompareTo(ps[b].Theta) : a.CompareTo(b));
                List<List<int>> runs = Runs(rim, ps);
                int main = -1;
                double mainPeople = -1;
                for (int r = 0; r < runs.Count; r++)
                {
                    double people = 0;
                    foreach (int p in runs[r]) people += ps[p].People;
                    if (people > mainPeople)
                    {
                        mainPeople = people;
                        main = r;
                    }
                }

                string text = Text(id, kv.Value, round, data, ps, season, out string line);
                for (int r = 0; r < runs.Count; r++)
                {
                    List<int> run = runs[r];
                    float t0 = ps[run[0]].Theta, span = 0;
                    for (int i = 1; i < run.Count; i++) span += Mathf.Repeat(ps[run[i]].Theta - ps[run[i - 1]].Theta, 360f);
                    float a0 = t0 - BandPadDeg, a1 = t0 + span + BandPadDeg;
                    inner.Clear();
                    outer.Clear();
                    LandFrame.SampleBand(inner, outer, BandR0, BandR1, a0, a1, y, BandStepDeg);
                    c.Fills[d].AddBand(inner, outer, colors, cid);
                    arc.Clear();
                    LandFrame.SampleArc(arc, BandR0, a0, a1, y, BandStepDeg);
                    c.Lines[d].AddPolyline(arc, edge, BandEdgePx, 0, cid, BandEdgeIntensity);
                    arc.Clear();
                    LandFrame.SampleArc(arc, BandR1, a0, a1, y, BandStepDeg);
                    c.Lines[d].AddPolyline(arc, edge, BandEdgePx, 0, cid, BandEdgeIntensity);

                    // the label at the run's people-weighted center angle (unwrapped from its first member)
                    double wsum = 0, asum = 0;
                    foreach (int p in run)
                    {
                        float off = Mathf.Repeat(ps[p].Theta - t0, 360f);
                        wsum += ps[p].People;
                        asum += ps[p].People * off;
                    }

                    float center = t0 + (float)(wsum > 0 ? asum / wsum : 0);
                    Vector3 at = LandFrame.Polar(0.5f * (BandR0 + BandR1), center, y + LabelLift);
                    if (r == main) c.Labels[d].Add(new CoalitionLabel { Id = id, Text = line, Local = at, Main = true });
                    else if (run.Count >= LandStyle.CoalitionMinPlayers)
                        c.Labels[d].Add(new CoalitionLabel { Id = id, Text = "Coalition " + (id + 1).ToString(LandFacts.Ci), Local = at });
                    if (r == main) lastSeen[id] = (text, at);
                }

                // the members' rivulet-side disc rings, brightened (5.5)
                foreach (int p in rim)
                {
                    Player pl = ps[p];
                    Vector3 ctr = Figure.Center(pl) + new Vector3(0, 0.006f, 0);
                    arc.Clear();
                    for (int i = 0; i <= BrightSegments; i++)
                    {
                        float a = (pl.Theta + 90f + 180f * i / BrightSegments) * Mathf.Deg2Rad;
                        arc.Add(new Vector3(ctr.x + pl.Radius * Mathf.Cos(a), ctr.y, ctr.z + pl.Radius * Mathf.Sin(a)));
                    }

                    c.Lines[d].AddPolyline(arc, bright, BrightPx, 0, cid, LandStyle.DiscIntensity * LandStyle.CoalitionBrighten);
                }

                if (runs.Count == 0 && kv.Value.Count > 0)
                {
                    // a coalition with no rim member (crown and towers only): its label over its first member
                    Vector3 at = Figure.Head(ps[kv.Value[0]]) + new Vector3(0, LabelLift, 0);
                    c.Labels[d].Add(new CoalitionLabel { Id = id, Text = line, Local = at, Main = true });
                    lastSeen[id] = (text, at);
                }
            }
        }

        /// <summary>Rim members (sorted by angle) cut into runs where consecutive members are more than the run angle apart (circularly).</summary>
        static List<List<int>> Runs(List<int> rim, Player[] ps)
        {
            List<List<int>> runs = new List<List<int>>();
            int n = rim.Count;
            if (n == 0) return runs;

            // start after the largest gap, so a run never straddles the cut
            int start = 0;
            float largest = -1;
            for (int i = 0; i < n; i++)
            {
                float gap = Mathf.Repeat(ps[rim[i]].Theta - ps[rim[(i + n - 1) % n]].Theta, 360f);
                if (n == 1) gap = 360f;
                if (gap > largest)
                {
                    largest = gap;
                    start = i;
                }
            }

            List<int> run = new List<int> { rim[start] };
            for (int j = 1; j < n; j++)
            {
                int p = rim[(start + j) % n], prev = rim[(start + j - 1) % n];
                if (Mathf.Repeat(ps[p].Theta - ps[prev].Theta, 360f) > LandStyle.CoalitionRunDeg)
                {
                    runs.Add(run);
                    run = new List<int>();
                }

                run.Add(p);
            }

            runs.Add(run);
            return runs;
        }

        /// <summary>
        /// "Coalition 3 · 28 players · 57M adults · trade, manufacturing, construction · frontline 11, PMC 9 · 76% of dealings
        /// inside · cooperation 0.73": the anchors and groups most of its players have, the share of its members' dealings
        /// with each other (Σ Ec inside / players) and the exposure-weighted cooperation of its inside ties, at the round.
        /// <paramref name="label"/> is the one-line label (WP6): "Coalition 3 · 28 players · trade, manufacturing · 77% inside ·
        /// cooperation 0.73", without the adults, the groups and the third anchor (those stay in the hover text), so it fits
        /// a phone's width and is never split.
        /// </summary>
        static string Text(int id, List<int> members, int round, EconomyData data, Player[] ps, SocialSeasonResult season, out string label)
        {
            double adults = 0;
            Dictionary<string, int> anchorCount = new Dictionary<string, int>(StringComparer.Ordinal);
            int[] groups = new int[12];
            HashSet<int> inside = new HashSet<int>(members);
            foreach (int p in members)
            {
                adults += ps[p].Adults.Length * 1e5;
                groups[(int)ps[p].Group]++;
                string a = AnchorWord(ps[p], data);
                if (a != null) anchorCount[a] = anchorCount.TryGetValue(a, out int v) ? v + 1 : 1;
            }

            List<KeyValuePair<string, int>> anchorsBy = new List<KeyValuePair<string, int>>(anchorCount);
            anchorsBy.Sort((x, y) => x.Value != y.Value ? y.Value.CompareTo(x.Value) : string.CompareOrdinal(x.Key, y.Key));
            List<int> groupsBy = new List<int>();
            for (int g = 0; g < 12; g++)
            {
                if (groups[g] > 0) groupsBy.Add(g);
            }

            groupsBy.Sort((x, y) => groups[x] != groups[y] ? groups[y].CompareTo(groups[x]) : x.CompareTo(y));

            double ec = 0, w = 0, coop = 0;
            float[] ex = season.Exposure[round], ab = season.CoopAB[round], ba = season.CoopBA[round];
            for (int k = 0; k < season.PairA.Length; k++)
            {
                if (!inside.Contains(season.PairA[k]) || !inside.Contains(season.PairB[k])) continue;
                ec += ex[k];
                w += ex[k];
                coop += ex[k] * 0.5 * (ab[k] + ba[k]);
            }

            StringBuilder anchors = new StringBuilder(64), groupWords = new StringBuilder(48);
            for (int i = 0; i < Math.Min(3, anchorsBy.Count); i++) anchors.Append(i == 0 ? " · " : ", ").Append(anchorsBy[i].Key);
            for (int i = 0; i < Math.Min(2, groupsBy.Count); i++)
            {
                groupWords.Append(i == 0 ? " · " : ", ").Append(PlayerCensus.GroupShort[groupsBy[i]]).Append(' ').Append(groups[groupsBy[i]]);
            }

            string head = "Coalition " + (id + 1).ToString(LandFacts.Ci) + " · " + members.Count.ToString(LandFacts.Ci) + " players";
            string tail = " · " + LandFacts.Percent(ec / Math.Max(1, members.Count)) + " of dealings inside · cooperation " +
                          LandFacts.Num(w > 0 ? coop / w : 0, 2);
            StringBuilder two = new StringBuilder(48);
            for (int i = 0; i < Math.Min(2, anchorsBy.Count); i++) two.Append(i == 0 ? " · " : ", ").Append(anchorsBy[i].Key);
            label = head + two + " · " + LandFacts.Percent(ec / Math.Max(1, members.Count)) + " inside · cooperation " +
                    LandFacts.Num(w > 0 ? coop / w : 0, 2);
            return head + " · " + LandFacts.Millions(adults, 0) + " adults" + anchors + groupWords + tail;
        }

        /// <summary>The industries' words in coalition labels (the data's names are long).</summary>
        static readonly Dictionary<string, string> Words = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "state_local", "state & local" }, { "oil_gas", "oil & gas" }, { "mining_metals", "mining" }, { "real_estate", "real estate" },
            { "other_services", "other services" }, { "media_telecom", "media" }, { "social_security", "social security" },
            { "safety_net", "safety net" }
        };

        /// <summary>The word for what pays a player: its industry, else its pseudo-anchor (pensions, capital, ...).</summary>
        static string AnchorWord(Player p, EconomyData data)
        {
            if (p.Anchor >= 0 && p.Anchor < data.Industries.Count) return Word(data.Industries[p.Anchor].Id);
            string a = p.AnchorId ?? "";
            int slash = a.IndexOf('/');
            if (slash >= 0) a = a.Substring(0, slash);
            if (a.StartsWith("tier:", StringComparison.Ordinal)) return a.Substring(5);
            if (a == "rest" || a.Length == 0) return null;
            return Word(a);
        }

        static string Word(string id) => Words.TryGetValue(id, out string w) ? w : id.Replace('_', ' ');

        /// <summary>The coalitions of the last detection in one line (the log).</summary>
        static string Summary(Content c)
        {
            List<CoalitionLabel> last = c.Labels.Length > 0 ? c.Labels[c.Labels.Length - 1] : new List<CoalitionLabel>();
            StringBuilder b = new StringBuilder();
            foreach (CoalitionLabel l in last)
            {
                if (!l.Main) continue;
                if (b.Length > 0) b.Append(" | ");
                b.Append(l.Text);
            }

            return c.Year.ToString(LandFacts.Ci) + " coalitions: " + (b.Length > 0 ? b.ToString() : "none");
        }
    }
}
