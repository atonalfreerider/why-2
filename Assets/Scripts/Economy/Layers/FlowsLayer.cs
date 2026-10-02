using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Why.Economy.Data;
using Why.Economy.Land;
using Why.Economy.Model;
using Debug = UnityEngine.Debug;

namespace Why.Economy.Layers
{
    /// <summary>
    /// The land's money (SPEC 2.7, 3.5, 4): what reaches people moves in the air, what leaves them runs on the ground.
    /// In the air: income arcs from where the money comes from (light-blue wage arcs from each player's patch on the
    /// sectors' wage strips, which the patches tile; thin arcs from named employers' towers with a gold dot at the
    /// tower; gold business income from the gold strips to the plinths; steel transfer fountains from the government
    /// floor; gold capital income falling from the crown; dashed borrowing through banking) and the crown's capital
    /// (payouts rising from the private sectors' gold strips as a few streams, payouts abroad over the far rim, saving
    /// rising from the savers, credit and investment leaving the crown). The arcs to players are drawn one per player in
    /// the people view and one per kind, group and cluster of players on the rim in every other view (WP6: hundreds of
    /// arcs buried the land); widths stay dollars, and a note over the crown and the flows' hover text say so. On the ground: every player's rivulet (ice for the dollars spent
    /// from fear, rose for desire, steel for taxes, never blended), the creeks, the lip canal's eight lanes (BASE,
    /// SELFISH, MATING, taxes, abroad) streaming around the rim to their falls, the waterfalls over the lip, the rivers
    /// running down the terraces and their distributaries along the rings into the pools, the steel tax river to the
    /// government floor, the abroad pour over the rim; white glitter over every lane, one sparkle per $5B of fantasy.
    /// Selecting a tower draws its ownership fan; selecting a player brightens its arcs and logs them.
    ///
    /// Labels: the falls ("Jeopardy · $6.38T · 85% fear · fantasy 10%"), taxes, abroad, the canal's bracket names, the
    /// neurochemicals beside the falls (mind view); anchors land:river:&lt;category&gt;, land:canal, land:taxes,
    /// land:abroad and land:flow:&lt;kind&gt;. Emphasis groups Income, CapitalFlows, Rivers, Glitter, Taxes (7.2). A new
    /// snapshot rebuilds on a worker (a preset's blocking one in the next Tick), uploads in Tick and cross-fades over
    /// <see cref="LandStyle.CrossFadeSeconds"/> (8.3), like the other land layers. Order 53.
    /// </summary>
    [GraphScenes(GraphScene.Economy)]
    public sealed class FlowsLayer : GraphLayer
    {
        public override int Order => 53;

        /// <summary>The name this layer reports its swaps under (<see cref="LandService.ReportReady"/>).</summary>
        const string ReadyName = "FlowsLayer";

        // ------------------------------------------------------------------ groups and looks

        /// <summary>The emphasis groups drawn, each a fill (ribbons, patches) and a line renderer.</summary>
        static readonly LandGroup[] Groups = { LandGroup.Income, LandGroup.CapitalFlows, LandGroup.Rivers, LandGroup.Glitter, LandGroup.Taxes };

        const int GIncome = 0, GCapital = 1, GRivers = 2, GGlitter = 3, GTaxes = 4, GroupCount = 5;

        /// <summary>The groups in the air that reach players (income, capital): the first two, each drawn per player and aggregated.</summary>
        const int AirGroups = 2;

        /// <summary>
        /// Only the glitter is additive light: the arcs are alpha-blended (hundreds of additive arcs over the bowl bloom
        /// into a haze), and so is the water (additive rose next to ice turns white).
        /// </summary>
        static readonly bool[] Additive = { false, false, false, true, false };

        static readonly int[] Queues =
        {
            LandStyle.QueueArcs, LandStyle.QueueArcs, LandStyle.QueueRivers, LandStyle.QueueRivers + 1, LandStyle.QueueRivers
        };

        /// <summary>Arcs: alpha and intensity of wage, transfer and gold arcs; the tower dot's size (px) and length (u).</summary>
        const float ArcAlpha = 0.5f, ArcIntensity = 0.85f, GoldIntensity = 1.5f, TowerDotPx = 4f, TowerDotLength = 0.012f;

        /// <summary>
        /// Wage arcs: a deeper blue than the wage strips (LandStyle.Wages). Hundreds of per-player arcs in the people view,
        /// in the strips' light blue at the old intensity (1.2), bloomed into white and could not be told from the steel
        /// transfers; income arcs now stay under the bloom threshold (<see cref="ArcIntensity"/>).
        /// </summary>
        static readonly Color WageArc = new Color(0.22f, 0.50f, 1.0f);

        /// <summary>Patches: the border's alpha and width.</summary>
        const float PatchBorderAlpha = 0.45f, PatchBorderPx = 1f;

        /// <summary>
        /// The lanes' ice and rose: the scene's fear and desire hues (<see cref="EconomyStyle.Fear"/>, 187°;
        /// <see cref="EconomyStyle.Desire"/>, 340°) deepened to full saturation. The palette's pastel versions, converted
        /// from linear to the screen's sRGB and laid at alpha 0.75 over the bright terraces, read grey (saturation below
        /// 0.3 in the harness renders); these keep their hue and stay clearly ice and rose. Never blended with each other.
        /// </summary>
        static readonly Color Ice = new Color(0.06f, 0.78f, 1.0f), Rose = new Color(1.0f, 0.10f, 0.36f);

        /// <summary>The abroad lane's ribbons are dimmer (money leaving the country).</summary>
        const float AbroadAlpha = 0.45f;

        /// <summary>The lanes' center lines (px from LandStyle.LineFloorPx), their alpha and intensity.</summary>
        const float CenterAlpha = 0.9f, CenterIntensity = 1.0f;

        /// <summary>Flow pulses: the materials' frequency and speed (pulses per phase unit; the path sets the phase per unit).</summary>
        const float FlowFreq = 3f, FlowSpeed = 0.6f, ArcPhasePerUnit = 0.6f;

        /// <summary>A sparkle: the arm length (u) of its cross, line width (px).</summary>
        const float GlitterArm = 0.022f, GlitterLinePx = 1.2f;

        /// <summary>The ownership fan: the stubs' length (u), the fan's alpha.</summary>
        const float FanStub = 1.6f, FanAlpha = 0.8f;

        /// <summary>A selected player's arcs: drawn again over everything at this alpha and intensity.</summary>
        const float SelectedAlpha = 0.95f, SelectedIntensity = 2f;

        // ------------------------------------------------------------------ labels

        const float LabelPx = 12f, BracketPx = 11f, ChemPx = 10f;

        const float FallPriority = 28, BracketPriority = 26, TaxPriority = 26, AbroadPriority = 26, ChemPriority = 16,
            FanPriority = 32, NotePriority = 21;

        /// <summary>
        /// The note on the aggregated arcs (WP6): the views that show the capital flows' labels say which arcs are drawn per
        /// group, so the widths read honestly. It is a caption at the top of the frame (this share of the screen's height
        /// from its top edge, at the crown's depth), where the land leaves room at both aspects (WP6 review: over the crown it
        /// met the crown's and the towers' labels, and under every sector's priority it never won a place).
        /// </summary>
        const float NoteFromTop = 0.12f;

        const string NoteText = "Arcs drawn per group and cluster of players, not per player; payouts as sector streams";

        /// <summary>Category names on the falls.</summary>
        static readonly string[] LaneTitle = { "Necessities", "Escapism", "Jeopardy", "Status", "Growth", "Collective", "Taxes", "Abroad" };

        /// <summary>The canal's lane groups (the notebook's brackets): lanes and name, in the canal's order.</summary>
        static readonly (int[] lanes, string name)[] Brackets =
        {
            (new[] { 0, 5 }, "BASE"), (new[] { 2, 1 }, "SELFISH"), (new[] { 3, 4 }, "MATING (+ saving rises to the crown)")
        };

        /// <summary>
        /// The bracket labels stand at these angles: around LandStyle.BracketThetaDeg on the near side, spread so they do
        /// not stack, the longest (MATING) farthest from the jeopardy fall that faces the viewer. WP6: their own row, 12° to
        /// 32° short of 250°, clear of the jeopardy fall's label (BASE lost to it on a phone at 248°).
        /// </summary>
        static readonly float[] BracketTheta = { LandStyle.BracketThetaDeg - 12f, LandStyle.BracketThetaDeg - 22f, LandStyle.BracketThetaDeg - 32f };

        /// <summary>The neurochemicals beside each category's fall (section 6; psyche.json ids).</summary>
        static readonly string[][] Chemicals =
        {
            new[] { "serotonin" }, new[] { "dopamine", "endorphins" }, new[] { "cortisol", "adrenaline" },
            new[] { "dopamine", "testosterone" }, Array.Empty<string>(), new[] { "oxytocin" }
        };

        /// <summary>The air's flow kinds with an anchor (land:flow:&lt;id&gt;): id, name, group.</summary>
        static readonly (FlowKind kind, string id, string name)[] FlowAnchors =
        {
            (FlowKind.Wages, "wages", "Wages"), (FlowKind.WagesCompany, "employers", "Wages from named companies"),
            (FlowKind.Business, "business", "Business income"), (FlowKind.Capital, "capital", "Capital income"),
            (FlowKind.Transfers, "transfers", "Transfers"), (FlowKind.Payout, "payouts", "Payouts"),
            (FlowKind.PayoutAbroad, "abroad", "Payouts abroad"), (FlowKind.Saving, "saving", "Saving"),
            (FlowKind.Investment, "investment", "Investment"), (FlowKind.Credit, "credit", "Credit"),
            (FlowKind.Borrowing, "borrowing", "Borrowing")
        };

        // ------------------------------------------------------------------ the build

        /// <summary>Everything one snapshot draws, built on a worker: mesh builders, labels, anchors.</summary>
        sealed class Built
        {
            public int Version, Year;
            public MoneyFlows Money;
            public readonly LineMeshBuilder[] Lines = new LineMeshBuilder[GroupCount];
            public readonly SurfaceMeshBuilder[] Fills = new SurfaceMeshBuilder[GroupCount];

            /// <summary>
            /// The arcs that reach players, income then capital (capital income, saving, borrowing): one per player and
            /// source (<see cref="Detail"/>, the people view) and aggregated, one per group, source and cluster of the
            /// group's players on the rim (<see cref="Aggregated"/>, every other view). The sector-level capital arcs
            /// (payouts, investment, credit, abroad) and the patches' borders stay in <see cref="Lines"/>.
            /// </summary>
            public readonly LineMeshBuilder[] Detail = new LineMeshBuilder[AirGroups], Aggregated = new LineMeshBuilder[AirGroups];

            public readonly List<LabelItem> Labels = new List<LabelItem>(24);
            public readonly List<Anchor> Anchors = new List<Anchor>(24);
            public int Sparkles, Undrawn, DetailArcs, AggregatedArcs, Streams, CrownLoops;
            public double Ms, UndrawnDollars, CrownLoopDollars;

            /// <summary>Reused point lists (no allocation per shape).</summary>
            public readonly List<Vector3> A = new List<Vector3>(400), B = new List<Vector3>(400);

            public readonly List<LinePoint> Points = new List<LinePoint>(400);
            public readonly List<Color32> Colors = new List<Color32>(400);

            public Built()
            {
                for (int g = 0; g < GroupCount; g++)
                {
                    Lines[g] = new LineMeshBuilder(4096);
                    Fills[g] = new SurfaceMeshBuilder();
                }

                for (int g = 0; g < AirGroups; g++)
                {
                    Detail[g] = new LineMeshBuilder(8192);
                    Aggregated[g] = new LineMeshBuilder(2048);
                }
            }

            public int Vertices
            {
                get
                {
                    int n = 0;
                    for (int g = 0; g < GroupCount; g++) n += Lines[g].VertexCount + Fills[g].VertexCount;
                    for (int g = 0; g < AirGroups; g++) n += Detail[g].VertexCount + Aggregated[g].VertexCount;
                    return n;
                }
            }

            /// <summary>Vertices of a group's renderers (its share of the detail and aggregated arcs included).</summary>
            public int GroupVertices(int g) =>
                Lines[g].VertexCount + Fills[g].VertexCount + (g < AirGroups ? Detail[g].VertexCount + Aggregated[g].VertexCount : 0);
        }

        /// <summary>A label of the build: its slot (stable across years), text, world point, group, ids, whether it exists.</summary>
        struct LabelItem
        {
            public int Slot;
            public string Text, Anchor;
            public Vector3 World;
            public LandGroup Group;
            public IdRange Ids;
            public bool Present;
        }

        /// <summary>Label slots: 6 falls, taxes, abroad, 3 brackets, 6 neurochemical lines, the note on the aggregated arcs.</summary>
        const int SlotFalls = 0, SlotTaxes = 6, SlotAbroad = 7, SlotBrackets = 8, SlotChems = 11, SlotNote = 17, SlotCount = 18;

        /// <summary>One upload of a build: the renderers of each group, and its cross-fade.</summary>
        sealed class Generation
        {
            public readonly MeshRenderer[] Lines = new MeshRenderer[GroupCount], Fills = new MeshRenderer[GroupCount];

            /// <summary>The arcs to players, income and capital: per player (the people view) and aggregated (the others).</summary>
            public readonly MeshRenderer[] Detail = new MeshRenderer[AirGroups], Aggregated = new MeshRenderer[AirGroups];

            public Built Source;
            public float Fade = 1;
            public bool Out;
        }

        EconomyModel model;
        LandFrame frame;
        LabelSystem labelSystem;
        Built first;
        readonly List<Generation> generations = new List<Generation>(2);
        readonly LabelSpec[] specs = new LabelSpec[SlotCount];
        readonly LandGroup[] specGroup = new LandGroup[SlotCount];
        readonly bool[] specPresent = new bool[SlotCount];
        LabelSpec fanLabel;
        int labelsVersion = -1;
        bool highlightSeen;

        // rebuilds (8.3)
        LandSnapshot wanted;
        Task<Built> building;
        int buildingVersion = -1;

        // the ownership fan and the selected player's arcs (main thread, small)
        MeshRenderer fan, chosen;
        int fanFor = -2, chosenFor = -2, fanVersion = -1, chosenVersion = -1;

        public override void Prepare(GraphContext ctx)
        {
            Stopwatch sw = Stopwatch.StartNew();
            model = ctx.Shared<EconomyModel>(EconomyModel.SharedKey);
            LandSnapshot s = ctx.Shared<LandSnapshot>(LandService.SharedKey);
            if (model == null || s?.Money == null || s.Land == null || s.Players?.Players == null)
            {
                Debug.LogWarning("[Why] FlowsLayer: no land snapshot, the money is not drawn");
                return;
            }

            frame = EconomyStage.Land();
            first = Build(s, model.Data, frame);
            labelSystem = ctx.Labels;
            for (int k = 0; k < SlotCount; k++)
            {
                specs[k] = new LabelSpec
                {
                    Text = "", Fixed = true, SizePx = k == SlotNote ? BracketPx : k >= SlotChems ? ChemPx : k >= SlotBrackets ? BracketPx : LabelPx,
                    Color = k >= SlotChems ? GraphStyle.TextDim : GraphStyle.Text, Align = TMPro.TextAlignmentOptions.Center,
                    Priority = SlotPriority(k), Rank = SlotPriority(k) - k * 0.01f, Hidden = true
                };
            }

            ApplyLabels(first, false);
            foreach (LabelSpec spec in specs) ctx.Labels.Add(spec);
            fanLabel = new LabelSpec
            {
                Text = " ", Fixed = true, SizePx = LabelPx, Color = GraphStyle.Text, Align = TMPro.TextAlignmentOptions.Center,
                Priority = FanPriority, Rank = FanPriority, Hidden = true
            };
            ctx.Labels.Add(fanLabel);
            foreach (Anchor a in first.Anchors) Anchors.Register(a);
            Debug.Log("[Why] FlowsLayer.Prepare " + sw.Elapsed.TotalMilliseconds.ToString("0", LandFacts.Ci) + " ms: " + Describe(first));
        }

        /// <summary>A build in the log: vertices by group, paths, patches, sparkles, the arcs per player and aggregated, the year.</summary>
        static string Describe(Built b) =>
            b.Vertices.ToString(LandFacts.Ci) + " vertices (" + Breakdown(b) + "); " + b.Money.Paths.Count.ToString(LandFacts.Ci) + " paths, " +
            (b.Money.Patches?.Length ?? 0).ToString(LandFacts.Ci) + " patches, " + b.Sparkles.ToString(LandFacts.Ci) + " sparkles; " + b.Streams.ToString(LandFacts.Ci) +
            " payout streams into the crown; arcs to players: " + b.DetailArcs.ToString(LandFacts.Ci) + " per player (people view; " + b.Undrawn.ToString(LandFacts.Ci) + " below $5B not drawn, " +
            LandFacts.Money(b.UndrawnDollars) + "), " + b.AggregatedArcs.ToString(LandFacts.Ci) + " by kind, group and rim cluster (sources averaged; other views; " +
            b.CrownLoops.ToString(LandFacts.Ci) + " from the crown back to it not drawn, " + LandFacts.Money(b.CrownLoopDollars) + "), " +
            b.Year.ToString(LandFacts.Ci) + " (build " + b.Ms.ToString("0", LandFacts.Ci) + " ms)";

        static float SlotPriority(int k) =>
            k < SlotTaxes ? FallPriority : k == SlotTaxes ? TaxPriority : k == SlotAbroad ? AbroadPriority : k < SlotChems ? BracketPriority
            : k == SlotNote ? NotePriority : ChemPriority;

        static string Breakdown(Built b)
        {
            StringBuilder s = new StringBuilder();
            for (int g = 0; g < GroupCount; g++)
            {
                if (g > 0) s.Append(", ");
                s.Append(Groups[g].ToString().ToLowerInvariant()).Append(' ').Append(b.GroupVertices(g).ToString(LandFacts.Ci));
            }

            return s.ToString();
        }

        public override void Upload(GraphContext ctx)
        {
            if (first == null) return;
            labelSystem = ctx.Labels;
            generations.Add(Create(first, 1f));
            int version = first.Version;
            first = null;
            LandService.Changed += OnChanged;
            LandService.ReportReady(ReadyName, version);
        }

        void OnDestroy()
        {
            LandService.Changed -= OnChanged;
            foreach (Generation g in generations) Release(g);
            generations.Clear();
            ReleaseOne(ref fan);
            ReleaseOne(ref chosen);
        }

        void OnChanged(LandSnapshot s)
        {
            if (s?.Money == null || model == null) return;
            wanted = s;
        }

        public override void Tick(GraphContext ctx, CameraRig rig)
        {
            if (generations.Count == 0) return;
            Rebuild();
            float dt = Mathf.Min(0.1f, Time.unscaledDeltaTime);
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

            Selection();

            // the arcs to players: one per player in the people view, aggregated by group and source elsewhere (eased)
            float wanted = LandView.PresetId == DetailPreset ? 1f : 0f;
            detail = detail < 0 ? wanted : Mathf.MoveTowards(detail, wanted, LandStyle.EmphasisRate * dt);

            // a selected player or tower: the other arcs step back so its own arcs (or the ownership fan) read
            float dim = fan != null || chosen != null ? LandStyle.SelectDim : 1f;
            foreach (Generation g in generations)
            {
                for (int i = 0; i < GroupCount; i++)
                {
                    float a = LandView.Alpha(Groups[i]) * g.Fade * (i == GIncome || i == GCapital ? dim : 1f);
                    Show(g.Lines[i], a);
                    Show(g.Fills[i], a);
                    if (i >= AirGroups) continue;
                    Show(g.Detail[i], a * detail);
                    Show(g.Aggregated[i], a * (1f - detail));
                }
            }

            UpdateLabels();
            KeepLabelsInFrame(rig);
        }

        int framedCam = -1, framedW, framedLabels = -1;

        /// <summary>The view whose arcs to players are drawn one per player (every other view draws them aggregated).</summary>
        const string DetailPreset = "people";

        /// <summary>The per-player arcs' share of the arcs to players (eased toward 1 in the people view, 0 elsewhere; -1 before the first frame).</summary>
        float detail = -1;

        /// <summary>
        /// Keeps the shown labels inside the frame (WP5: on a portrait screen the abroad pour's label ran off the left
        /// edge): whenever the camera, the screen or the labels changed, each is shifted sideways just enough
        /// (<see cref="LandViewLayer.KeepInFrame"/>).
        /// </summary>
        void KeepLabelsInFrame(CameraRig rig)
        {
            if (rig == null || rig.Cam == null || labelSystem == null) return;
            int shownKey = labelsVersion * 2 + (highlightSeen ? 1 : 0);
            if (rig.Version == framedCam && Screen.width == framedW && shownKey == framedLabels) return;
            framedCam = rig.Version;
            framedW = Screen.width;
            framedLabels = shownKey;
            bool changed = PlaceNote(rig.Cam);
            foreach (LabelSpec spec in specs) changed |= LandViewLayer.KeepInFrame(rig.Cam, spec, 0, labelSystem.Padding);
            if (changed) labelSystem.MarkDirty();
        }

        /// <summary>The note's home (the crown, world): the depth its caption stands at.</summary>
        Vector3 noteHome;

        /// <summary>Puts the note on the aggregated arcs at the top center of the frame (<see cref="NoteFromTop"/>); true when it moved.</summary>
        bool PlaceNote(Camera cam)
        {
            LabelSpec note = specs[SlotNote];
            if (note == null) return false;
            float depth = cam.WorldToScreenPoint(noteHome).z;
            if (depth <= cam.nearClipPlane) return false;
            Vector3 at = cam.ScreenToWorldPoint(new Vector3(0.5f * Screen.width, (1f - NoteFromTop) * Screen.height, depth));
            if ((at - note.Data).sqrMagnitude < 1e-8f) return false;
            note.Data = at;
            return true;
        }

        static void Show(MeshRenderer r, float alpha)
        {
            if (r == null) return;
            bool on = alpha > 0.003f;
            if (r.enabled != on) r.enabled = on;
            if (on) GraphMaterials.SetAlpha(r.sharedMaterial, alpha);
        }

        /// <summary>Follows the snapshot wanted (blocking ones here, others on a worker); stale builds are discarded.</summary>
        void Rebuild()
        {
            if (building != null && building.IsCompleted)
            {
                Task<Built> t = building;
                building = null;
                if (t.IsFaulted) Debug.LogError("[Why] FlowsLayer: a rebuild failed: " + t.Exception?.GetBaseException());
                else if (wanted != null && t.Result.Version == wanted.Version)
                {
                    Swap(t.Result);
                    wanted = null;
                }
            }

            if (wanted == null || building != null && buildingVersion == wanted.Version) return;
            Generation newest = generations.Count > 0 ? generations[generations.Count - 1] : null;
            if (newest?.Source != null && ReferenceEquals(newest.Source.Money, wanted.Money))
            {
                // the same money (a new season of the same year): nothing to redraw
                newest.Source.Version = wanted.Version;
                LandService.ReportReady(ReadyName, wanted.Version);
                wanted = null;
                return;
            }

            if (wanted.Blocking)
            {
                // cleared first: a build that throws must not be retried every frame (that would stop every later layer)
                LandSnapshot w = wanted;
                wanted = null;
                Built b;
                try
                {
                    b = Build(w, model.Data, frame);
                }
                catch (Exception e)
                {
                    Debug.LogError("[Why] FlowsLayer: a blocking rebuild failed: " + e);
                    LandService.ReportReady(ReadyName, w.Version);
                    return;
                }

                Swap(b);
                return;
            }

            if (building != null) return;
            LandSnapshot s = wanted;
            EconomyData data = model.Data;
            LandFrame f = frame;
            buildingVersion = s.Version;
            building = Task.Run(() => Build(s, data, f));
        }

        void Swap(Built b)
        {
            Debug.Log("[Why] FlowsLayer rebuild " + b.Year.ToString(LandFacts.Ci) + ": " + Describe(b));
            foreach (Generation g in generations) g.Out = true;
            generations.Add(Create(b, 0f));
            ApplyLabels(b, true);
            foreach (Anchor a in b.Anchors) Anchors.Register(a);
            fanFor = chosenFor = -2;
            LandService.ReportReady(ReadyName, b.Version);
        }

        Generation Create(Built b, float fade)
        {
            Generation g = new Generation { Fade = fade, Source = b };
            string year = b.Year.ToString(LandFacts.Ci);
            for (int i = 0; i < GroupCount; i++)
            {
                string name = "Flows" + Groups[i] + " " + year;
                if (b.Fills[i].VertexCount > 0)
                {
                    Material m = GraphMaterials.Raw(GraphMaterials.Surface(Color.white, 1f, Queues[i], Additive[i] && i != GRivers && i != GTaxes));
                    m.SetFloat("_EdgeSoft", 0f);
                    g.Fills[i] = Place(AddMesh(name, b.Fills[i].ToMesh(name), m));
                }

                if (b.Lines[i].VertexCount > 0) g.Lines[i] = Lines(name + " lines", b.Lines[i], Queues[i] + 1, Additive[i], i == GGlitter ? 0f : 1f);
                if (i >= AirGroups) continue;
                if (b.Detail[i].VertexCount > 0) g.Detail[i] = Lines(name + " per player", b.Detail[i], Queues[i] + 1, false, 1f);
                if (b.Aggregated[i].VertexCount > 0) g.Aggregated[i] = Lines(name + " by group", b.Aggregated[i], Queues[i] + 1, false, 1f);
            }

            return g;
        }

        /// <summary>A line renderer of a builder (flow pulses on), placed in the land's frame, hidden until Tick shows it.</summary>
        MeshRenderer Lines(string name, LineMeshBuilder lines, int queue, bool additive, float flow)
        {
            Material m = GraphMaterials.Raw(GraphMaterials.Line(Color.white, 1f, queue, additive, 0, flow));
            m.SetFloat("_FlowFreq", FlowFreq);
            m.SetFloat("_FlowSpeed", FlowSpeed);
            return Place(AddMesh(name, lines.ToMesh(name), m));
        }

        MeshRenderer Place(MeshRenderer r)
        {
            frame.Place(r.transform);
            GraphMaterials.SetAlpha(r.sharedMaterial, 0);
            r.enabled = false;
            return r;
        }

        static void Release(Generation g)
        {
            foreach (MeshRenderer[] set in new[] { g.Lines, g.Fills, g.Detail, g.Aggregated })
            {
                for (int i = 0; i < set.Length; i++) ReleaseOne(ref set[i]);
            }
        }

        static void ReleaseOne(ref MeshRenderer r)
        {
            if (r == null) return;
            MeshFilter f = r.GetComponent<MeshFilter>();
            if (f != null && f.sharedMesh != null) Destroy(f.sharedMesh);
            if (r.sharedMaterial != null) Destroy(r.sharedMaterial);
            Destroy(r.gameObject);
            r = null;
        }

        // ------------------------------------------------------------------ selection: the ownership fan, a player's arcs

        /// <summary>
        /// The ownership fan of the selected tower and the selected player's arcs, rebuilt when the selection or the
        /// money on screen changes (main thread: a few hundred points).
        /// </summary>
        void Selection()
        {
            Generation newest = generations[generations.Count - 1];
            Built b = newest.Source;
            if (b?.Money == null) return;
            LandSnapshot s = LandService.Current;
            int tower = EconomyState.SelectedTower, player = EconomyState.SelectedPlayer;
            if (tower != fanFor || fanVersion != b.Version)
            {
                fanFor = tower;
                fanVersion = b.Version;
                ReleaseOne(ref fan);
                string text = null;
                Vector3 at = default;
                if (tower >= 0 && s?.Land?.Towers != null && s.Players?.Players != null)
                {
                    LineMeshBuilder lines = new LineMeshBuilder(2048);
                    text = Fan(lines, s, model, tower, out at);
                    if (lines.VertexCount > 0)
                    {
                        Material m = GraphMaterials.Raw(GraphMaterials.Line(Color.white, 1f, LandStyle.QueueArcs + 3, true, 0, 1f));
                        m.SetFloat("_FlowFreq", FlowFreq);
                        m.SetFloat("_FlowSpeed", FlowSpeed);
                        fan = AddMesh("FlowsOwnershipFan", lines.ToMesh("FlowsOwnershipFan"), m);
                        frame.Place(fan.transform);
                    }
                }

                if (fanLabel != null)
                {
                    bool show = text != null;
                    if (show)
                    {
                        labelSystem?.SetText(fanLabel, text);
                        fanLabel.Data = frame.World(at);
                    }

                    if (fanLabel.Hidden == show)
                    {
                        fanLabel.Hidden = !show;
                        labelSystem?.MarkDirty();
                    }
                }
            }

            if (player != chosenFor || chosenVersion != b.Version)
            {
                chosenFor = player;
                chosenVersion = b.Version;
                ReleaseOne(ref chosen);
                if (player >= 0 && s?.Players?.Players != null && player < s.Players.Players.Length)
                {
                    LineMeshBuilder lines = new LineMeshBuilder(1024);
                    string log = Chosen(lines, b.Money, s, model.Data, player);
                    if (lines.VertexCount > 0)
                    {
                        Material m = GraphMaterials.Raw(GraphMaterials.Line(Color.white, 1f, LandStyle.QueueArcs + 3, true, 0, 1f));
                        m.SetFloat("_FlowFreq", FlowFreq);
                        m.SetFloat("_FlowSpeed", FlowSpeed);
                        chosen = AddMesh("FlowsSelectedPlayer", lines.ToMesh("FlowsSelectedPlayer"), m);
                        frame.Place(chosen.transform);
                    }

                    Debug.Log(log);
                }
            }

            if (fan != null) Show(fan, Mathf.Max(LandView.Alpha(LandGroup.CapitalFlows), LandView.Alpha(LandGroup.Towers)) > 0.01f ? LandView.Reveal : 0f);
            if (chosen != null) Show(chosen, LandView.Reveal);
        }

        /// <summary>
        /// The ownership fan (7.5): gold threads from the tower's top to every player in proportion to its equity dollars
        /// (capital income × the equity share of its members' wealth groups), the stubs to abroad and to pensions; the
        /// hover text with the owners' shares.
        /// </summary>
        static string Fan(LineMeshBuilder lines, LandSnapshot s, EconomyModel model, int company, out Vector3 at)
        {
            at = default;
            TowerGeom tower = null;
            foreach (TowerGeom t in s.Land.Towers) tower = t.Company == company ? t : tower;
            if (tower == null) return null;
            EconomyData data = model.Data;
            EconomicLives lives = model.Lives;
            const double Unit = 1e5 / 1e9;
            double[] equity = new double[s.Players.Players.Length], byGroup = new double[CapitalSources.Groups];
            double[] fraction = new double[CapitalSources.Groups];
            for (int g = 0; g < CapitalSources.Groups; g++) fraction[g] = CapitalSources.EquityFraction(data, g);
            double total = 0;
            foreach (Player p in s.Players.Players)
            {
                foreach (int i in p.Adults)
                {
                    if (lives == null || !lives.TryGet(i, s.Year, out PersonYear r) || r.CapitalIncome <= 0) continue;
                    int g = Math.Min(CapitalSources.Groups - 1, (int)r.WealthGroup);
                    double e = r.CapitalIncome * Unit * fraction[g];
                    equity[p.Index] += e;
                    byGroup[g] += e;
                    total += e;
                }
            }

            double foreign = data.Circuit?.ForeignEquityShare ?? 0, pension = data.Circuit?.PensionEquityShare ?? 0;
            double profit = tower.Private ? tower.MarketCap * 0.02 : Math.Max(0, tower.NetIncome);
            Vector3 top = LandFrame.Polar(tower.R, tower.Theta, tower.TopY);
            at = top + new Vector3(0, 0.12f, 0);
            float perB = s.Land.WidthPerB;
            List<LinePoint> pts = new List<LinePoint>(LandStyle.ArcPoints);
            Color32 gold = LandMath.Tint(EconomyStyle.Capital, FanAlpha);
            int id = EconomyIds.LandTower(company);
            foreach (Player p in s.Players.Players)
            {
                if (equity[p.Index] <= 0 || total <= 0) continue;
                double v = profit * (1 - foreign - pension) * equity[p.Index] / total;
                ArcLine(lines, pts, MoneyRouting.Arc(top, Figure.Center(p), false), v, perB, gold, id, GoldIntensity, false);
            }

            // stubs: abroad over the far rim, pensions toward the retirees' side (fading)
            foreach ((double share, float theta) in new[] { (foreign, 90f), (pension, 270f) })
            {
                if (share <= 0) continue;
                Vector3 end = top + LandFrame.Radial(theta) * FanStub + new Vector3(0, 0.3f, 0);
                ArcLine(lines, pts, MoneyRouting.Arc(top, end, false), profit * share, perB, gold, id, GoldIntensity, false, true);
            }

            string[] names = { "the bottom half", "the next 40%", "the next 9%", "the 1%" };
            StringBuilder b = new StringBuilder();
            b.Append(tower.Name).Append("'s owners in the model: abroad ").Append(LandFacts.Percent(foreign)).Append(", pensions ")
                .Append(LandFacts.Percent(pension)).Append("; of the rest");
            for (int g = CapitalSources.Groups - 1; g >= 0; g--)
            {
                b.Append(g < CapitalSources.Groups - 1 ? ", " : " ").Append(names[g]).Append(' ').Append(LandFacts.Percent(total > 0 ? byGroup[g] / total : 0));
            }

            return b.ToString();
        }

        /// <summary>A selected player's arcs drawn bright over the rest; returns the log line listing them.</summary>
        static string Chosen(LineMeshBuilder lines, MoneyFlows money, LandSnapshot s, EconomyData data, int player)
        {
            Player p = s.Players.Players[player];
            StringBuilder log = new StringBuilder("[Why] Flows of " + p.Key + " " + s.Year.ToString(LandFacts.Ci) + ":");
            List<LinePoint> pts = new List<LinePoint>(LandStyle.ArcPoints);
            float perB = s.Land.WidthPerB;
            int n = 0;
            foreach (FlowPath f in money.Paths)
            {
                bool mine = f.Kind <= FlowKind.Borrowing && f.Kind != FlowKind.Payout && f.Kind != FlowKind.PayoutAbroad &&
                            f.Kind != FlowKind.Investment && f.Kind != FlowKind.Credit && (f.To == player || f.Kind == FlowKind.Saving && f.From == player);
                if (!mine) continue;
                Color c = ArcColor(f.Kind);
                ArcLine(lines, pts, f.Points, f.Dollars, perB, LandMath.Tint(c, SelectedAlpha), EconomyIds.LandPlayer(player, EconomyIds.PlayerIncome),
                    SelectedIntensity, f.Dashed);
                log.Append(n++ > 0 ? ";" : "").Append(' ').Append(KindName(f.Kind)).Append(' ').Append(Source(f, s, data)).Append(' ')
                    .Append(LandFacts.Money(f.Dollars));
            }

            return log.ToString();
        }

        static string KindName(FlowKind k)
        {
            foreach ((FlowKind kind, string id, string _) in FlowAnchors)
            {
                if (kind == k) return id;
            }

            return k.ToString().ToLowerInvariant();
        }

        static string Source(FlowPath f, LandSnapshot s, EconomyData data)
        {
            switch (f.Kind)
            {
                case FlowKind.WagesCompany:
                    return f.From >= 0 && f.From < (data.Circuit?.Capture?.Count ?? 0) ? "from " + data.Circuit.Capture[f.From].Name + " (" + data.Industries[f.Lane].Id + ")" : "from a tower";
                case FlowKind.Wages:
                    return "from " + data.Industries[f.From].Id + "'s patch";
                case FlowKind.Capital: return "from the crown";
                case FlowKind.Saving: return "to the crown";
                default: return f.From >= 0 && f.From < data.Industries.Count ? "from " + data.Industries[f.From].Id : "";
            }
        }

        // ------------------------------------------------------------------ labels

        void ApplyLabels(Built b, bool live)
        {
            // new labels (a new year) keep no sideways shift of the old ones: re-frame them even with a still camera
            framedLabels = int.MinValue;
            for (int k = 0; k < SlotCount; k++) specPresent[k] = false;
            foreach (LabelItem it in b.Labels)
            {
                LabelSpec spec = specs[it.Slot];
                if (spec == null) continue;
                if (live) labelSystem?.SetText(spec, it.Text);
                else spec.Text = it.Text;
                spec.Data = it.World;
                if (it.Slot == SlotNote) noteHome = it.World;
                spec.AnchorKey = it.Anchor;
                spec.Ids = it.Ids;
                specGroup[it.Slot] = it.Group;
                specPresent[it.Slot] = it.Present;
            }

            labelsVersion = -1;
            if (live) UpdateLabels();
        }

        /// <summary>A label shows while its group is listed and visible, or when hovered and its group is at least 0.3 visible.</summary>
        void UpdateLabels()
        {
            bool highlight = Highlighter.HasHighlight;
            if (labelsVersion == LandView.LabelsVersion && !highlight && !highlightSeen) return;
            labelsVersion = LandView.LabelsVersion;
            highlightSeen = highlight;
            bool changed = false;
            for (int k = 0; k < SlotCount; k++)
            {
                LabelSpec spec = specs[k];
                if (spec == null) continue;
                LandGroup g = specGroup[k];
                bool show = specPresent[k] && (LandView.LabelsShown(g) ||
                                               LandView.Alpha(g) >= 0.3f && highlight && Highlighter.IsHighlighted(spec.Ids));
                if (spec.Hidden == !show) continue;
                spec.Hidden = !show;
                changed = true;
            }

            if (changed) labelSystem?.MarkDirty();
        }

        // ================================================================== the build (pure: any thread)

        static Built Build(LandSnapshot s, EconomyData data, LandFrame frame)
        {
            Stopwatch sw = Stopwatch.StartNew();
            Built b = new Built { Version = s.Version, Year = s.Year, Money = s.Money };
            MoneyFlows m = s.Money;
            LandGeometry land = s.Land;
            float perB = land.WidthPerB;
            Patches(b, m, land);
            Air(b, m, perB);
            Aggregate(b, m, s.Players, perB);
            PayoutStreams(b, m, land, perB);
            Canal(b, m, land);
            Ground(b, m, perB);
            Glitter(b, m, land, s.Players);
            Texts(b, s, data, frame);
            b.Ms = sw.Elapsed.TotalMilliseconds;
            return b;
        }

        // ---- the air

        static Color ArcColor(FlowKind k)
        {
            switch (k)
            {
                case FlowKind.Wages:
                case FlowKind.WagesCompany:
                    return WageArc;
                case FlowKind.Transfers: return EconomyStyle.Government;
                default: return EconomyStyle.Capital;
            }
        }

        /// <summary>
        /// Whether an arc reaches (or, saving, leaves) one player: income (wages, employers, business, transfers), capital
        /// income, saving and borrowing. The rest (payouts, payouts abroad, investment, credit) are the crown's sector-level
        /// arcs, drawn once in every view.
        /// </summary>
        static bool PerPlayer(FlowKind k) => k <= FlowKind.Transfers || k == FlowKind.Saving || k == FlowKind.Borrowing;

        /// <summary>Income (light blue and steel arcs: wages, employers, business, transfers), as against capital (gold).</summary>
        static bool IsIncome(FlowKind k) => k <= FlowKind.Business || k == FlowKind.Transfers;

        /// <summary>The highlight id of an arc: the crown's account for sector-level arcs, else its player's income id.</summary>
        static int ArcId(FlowKind k, int player) =>
            k == FlowKind.Payout ? EconomyIds.LandCapital(EconomyIds.CapitalPayouts)
            : k == FlowKind.Investment ? EconomyIds.LandCapital(EconomyIds.CapitalInvestment)
            : k == FlowKind.Credit ? EconomyIds.LandCapital(EconomyIds.CapitalCredit)
            : k == FlowKind.PayoutAbroad ? EconomyIds.LandCapital(EconomyIds.CapitalAbroad)
            : EconomyIds.LandPlayer(player, EconomyIds.PlayerIncome);

        /// <summary>An arc's alpha by kind: capital income, borrowing, the rest.</summary>
        static float KindAlpha(FlowKind k) => k == FlowKind.Capital ? LandStyle.CapitalArcAlpha : k == FlowKind.Borrowing ? LandStyle.BorrowAlpha : ArcAlpha;

        /// <summary>
        /// Every arc in the air as the model routes it: the crown's sector-level arcs (payouts, abroad, investment, credit)
        /// into the shared capital lines, every arc of a player into the per-player builders (the people view).
        /// </summary>
        static void Air(Built b, MoneyFlows m, float perB)
        {
            List<LinePoint> pts = b.Points;
            foreach (FlowPath f in m.Paths)
            {
                if (f.Kind > FlowKind.Borrowing || f.Kind == FlowKind.Payout || f.Points == null) continue;   // payouts: PayoutStreams
                bool perPlayer = PerPlayer(f.Kind);
                if (perPlayer && f.Dollars < LandStyle.StreamMinB && (f.Kind == FlowKind.Capital || f.Kind == FlowKind.Saving || f.Kind == FlowKind.Borrowing))
                {
                    // a player's only arc of its kind below $5B (3.5): too thin to see; its dollars stay in the flows, the log
                    // and the aggregated arcs
                    b.Undrawn++;
                    b.UndrawnDollars += f.Dollars;
                    continue;
                }

                bool income = IsIncome(f.Kind);
                LineMeshBuilder lines = perPlayer ? b.Detail[income ? GIncome : GCapital] : b.Lines[GCapital];
                bool gold = ArcColor(f.Kind) == EconomyStyle.Capital;
                int id = ArcId(f.Kind, f.Kind == FlowKind.Saving ? f.From : f.To);
                ArcLine(lines, pts, perPlayer ? Thin(f.Points, DetailPoints) : f.Points, f.Dollars, perB, LandMath.Tint(ArcColor(f.Kind), KindAlpha(f.Kind)), id, gold ? GoldIntensity : ArcIntensity,
                    f.Dashed, f.Kind == FlowKind.PayoutAbroad);
                if (perPlayer) b.DetailArcs++;
                if (f.Kind == FlowKind.WagesCompany)
                {
                    Vector3 a = f.Points[0];
                    lines.AddSegment(a - new Vector3(TowerDotLength, 0, 0), a + new Vector3(TowerDotLength, 0, 0),
                        LandMath.Tint(EconomyStyle.Capital, 1f), TowerDotPx, 0, id, GoldIntensity);
                }
            }
        }

        /// <summary>
        /// Players of one group and kind share one aggregated arc while each stands within <see cref="AggregateRunDeg"/> of
        /// the previous and within <see cref="AggregateSpanDeg"/> of the first (degrees): a cluster on the rim.
        /// </summary>
        const float AggregateRunDeg = 30f, AggregateSpanDeg = 45f;

        /// <summary>
        /// The crown's arcs to and from the players (capital income, saving) and borrowing gather wider clusters (degrees:
        /// neighbours, span): one source, the crown, so a few arcs per group say where its capital goes (WP6: 45° clusters
        /// still made a dome of about 140 gold arcs over the towers).
        /// </summary>
        const float CrownRunDeg = 60f, CrownSpanDeg = 120f;

        /// <summary>An aggregated arc shorter than this (both ends at the crown: the capital-only 1%'s own income) is not drawn.</summary>
        const float AggregateMinChord = 0.05f;

        /// <summary>
        /// The arcs to players outside the people view (WP6): one arc per kind (wages from named employers count as wages),
        /// group and cluster of the group's receiving players (by place, then by angle: <see cref="AggregateRunDeg"/>,
        /// <see cref="AggregateSpanDeg"/>), from the dollar-weighted mean of its sources (the patches, strips, the crown,
        /// banking or the floor near the cluster) to the dollar-weighted mean of its players (angles averaged on the circle,
        /// radii and heights linearly), as wide as their summed dollars. The arcs below $5B a player are in their group's; a
        /// cluster whose both ends sit on the crown (the capital-only 1%'s capital income and saving) has no arc to draw and
        /// is counted in the log. Widths stay dollars; the crown's note and the flows' hover text say the arcs are aggregated.
        /// </summary>
        static void Aggregate(Built b, MoneyFlows m, PlayerSet players, float perB)
        {
            Player[] ps = players?.Players;
            if (ps == null) return;

            // bins by (kind, group, place), in key order; each holds its paths in the model's order (sources are averaged)
            SortedDictionary<long, List<FlowPath>> bins = new SortedDictionary<long, List<FlowPath>>();
            foreach (FlowPath f in m.Paths)
            {
                if (f.Points == null || f.Points.Length < 2 || !PerPlayer(f.Kind)) continue;
                int player = Receiver(f);
                if (player < 0 || player >= ps.Length) continue;
                Player p = ps[player];
                FlowKind kind = f.Kind == FlowKind.WagesCompany ? FlowKind.Wages : f.Kind;
                long key = ((long)kind * 16 + (int)p.Group) * 4 + (int)p.Place;
                if (!bins.TryGetValue(key, out List<FlowPath> list)) bins[key] = list = new List<FlowPath>();
                list.Add(f);
            }

            List<LinePoint> pts = b.Points;
            List<FlowPath> run = new List<FlowPath>(64);
            foreach (KeyValuePair<long, List<FlowPath>> bin in bins)
            {
                List<FlowPath> list = bin.Value;
                FlowKind kind = (FlowKind)(bin.Key / 64);

                // the bin's players by angle (index tie-break), cut after the largest gap, split where neighbours stand apart
                list.Sort((x, y) =>
                {
                    float tx = ps[Receiver(x)].Theta, ty = ps[Receiver(y)].Theta;
                    return tx != ty ? tx.CompareTo(ty) : Receiver(x).CompareTo(Receiver(y));
                });
                int n = list.Count, start = 0;
                float largest = -1;
                for (int i = 0; i < n; i++)
                {
                    float gap = n == 1 ? 360f : Mathf.Repeat(ps[Receiver(list[i])].Theta - ps[Receiver(list[(i + n - 1) % n])].Theta, 360f);
                    if (gap <= largest) continue;
                    largest = gap;
                    start = i;
                }

                bool crown = kind == FlowKind.Capital || kind == FlowKind.Saving || kind == FlowKind.Borrowing;
                float gapDeg = crown ? CrownRunDeg : AggregateRunDeg, spanDeg = crown ? CrownSpanDeg : AggregateSpanDeg;
                run.Clear();
                for (int j = 0; j <= n; j++)
                {
                    FlowPath f = j < n ? list[(start + j) % n] : null;
                    if (f != null && run.Count > 0 &&
                        Mathf.Repeat(ps[Receiver(f)].Theta - ps[Receiver(run[run.Count - 1])].Theta, 360f) <= gapDeg &&
                        Mathf.Repeat(ps[Receiver(f)].Theta - ps[Receiver(run[0])].Theta, 360f) <= spanDeg)
                    {
                        run.Add(f);
                        continue;
                    }

                    if (run.Count > 0) AggregateArc(b, pts, run, kind, perB);
                    run.Clear();
                    if (f != null) run.Add(f);
                }
            }
        }

        /// <summary>A sector paying at least this much into the crown has its own stream ($B); the others share their ring's.</summary>
        const double PayoutStreamMinB = 250;

        /// <summary>Streams into the crown rise this far above their higher end at the middle (land units).</summary>
        const float StreamLift = 0.2f;

        /// <summary>
        /// The payouts into the crown as a few streams (WP6): each sector paying at least <see cref="PayoutStreamMinB"/>
        /// (2025: real estate, banking, trade, manufacturing, professional services) its own, the rest of each ring one
        /// stream from the dollar-weighted mean of its sectors' strips; each rises gently into the crown at its angle (the
        /// 23 tall loops of one arc per sector made a dome over the towers). Widths stay dollars.
        /// </summary>
        static void PayoutStreams(Built b, MoneyFlows m, LandGeometry land, float perB)
        {
            List<FlowPath> rest = new List<FlowPath>(8);
            int id = EconomyIds.LandCapital(EconomyIds.CapitalPayouts);
            for (int tier = 0; tier < 5; tier++)
            {
                rest.Clear();
                foreach (FlowPath f in m.Paths)
                {
                    if (f.Kind != FlowKind.Payout || f.Points == null || f.From < 0 || f.From >= land.Sectors.Length) continue;
                    if ((int)land.Sectors[f.From].Tier != tier) continue;
                    if (f.Dollars >= PayoutStreamMinB) Stream(b, f.Points[0], f.Dollars, perB, id);
                    else rest.Add(f);
                }

                double dollars = 0;
                foreach (FlowPath f in rest) dollars += f.Dollars;
                if (dollars > 0) Stream(b, PolarMean(rest, true, dollars), dollars, perB, id);
            }
        }

        /// <summary>One stream into the crown: from a point to the crown ring at its angle, a gentle rise (<see cref="AggregatePoints"/> points).</summary>
        static void Stream(Built b, Vector3 from, double dollars, float perB, int id)
        {
            Vector3 to = StreamEnd(from);
            Vector3 control = new Vector3(0.5f * (from.x + to.x), Mathf.Max(from.y, to.y) + StreamLift, 0.5f * (from.z + to.z));
            Vector3[] pts = new Vector3[AggregatePoints];
            for (int k = 0; k < pts.Length; k++) pts[k] = LandMath.Bezier(from, control, to, k / (float)(pts.Length - 1));
            ArcLine(b.Lines[GCapital], b.Points, pts, dollars, perB, LandMath.Tint(EconomyStyle.Capital, ArcAlpha), id, GoldIntensity, false);
            b.Streams++;
        }

        /// <summary>The crown ring's point at a land point's angle.</summary>
        static Vector3 StreamEnd(Vector3 from) =>
            MoneyRouting.CrownAt(Mathf.Atan2(from.z, from.x) * Mathf.Rad2Deg);

        /// <summary>The player an arc reaches (saving: the player it leaves).</summary>
        static int Receiver(FlowPath f) => f.Kind == FlowKind.Saving ? f.From : f.To;

        /// <summary>One aggregated arc: the run's dollars from the mean of its sources to the mean of its players.</summary>
        static void AggregateArc(Built b, List<LinePoint> pts, List<FlowPath> run, FlowKind kind, float perB)
        {
            double dollars = 0;
            foreach (FlowPath f in run) dollars += f.Dollars;
            if (dollars <= 0) return;
            Vector3 from = PolarMean(run, true, dollars), to = PolarMean(run, false, dollars);
            if (new Vector2(to.x - from.x, to.z - from.z).magnitude < AggregateMinChord && Mathf.Abs(to.y - from.y) < AggregateMinChord)
            {
                b.CrownLoops++;
                b.CrownLoopDollars += dollars;
                return;
            }

            bool sag = kind == FlowKind.Capital;
            bool gold = ArcColor(kind) == EconomyStyle.Capital;
            int id = ArcId(kind, Receiver(run[0]));
            LineMeshBuilder lines = b.Aggregated[IsIncome(kind) ? GIncome : GCapital];
            Vector3[] path;
            if (kind == FlowKind.Saving)
            {
                // saving rises into the crown as a gentle stream, not a loop over it
                Vector3 control = new Vector3(0.5f * (from.x + to.x), Mathf.Max(from.y, to.y) + StreamLift, 0.5f * (from.z + to.z));
                path = new Vector3[AggregatePoints];
                for (int k = 0; k < path.Length; k++) path[k] = LandMath.Bezier(from, control, to, k / (float)(path.Length - 1));
            }
            else path = Thin(MoneyRouting.Arc(from, to, sag), AggregatePoints);

            ArcLine(lines, pts, path, dollars, perB, LandMath.Tint(ArcColor(kind), KindAlpha(kind)), id, gold ? GoldIntensity : ArcIntensity,
                kind == FlowKind.Borrowing);
            b.AggregatedArcs++;
        }

        /// <summary>
        /// Points of the arcs drawn (8.6, WP6 review): the aggregated arcs and the payout streams are wide, smooth quadratic
        /// curves (12 points draw them), the per-player arcs thin ones (14); the model routes every arc with
        /// <see cref="LandStyle.ArcPoints"/> (20).
        /// </summary>
        const int AggregatePoints = 12, DetailPoints = 14;

        /// <summary>A polyline resampled to n points evenly along its index (its ends kept); itself when it has n or fewer.</summary>
        static Vector3[] Thin(Vector3[] points, int n)
        {
            if (points.Length <= n) return points;
            Vector3[] r = new Vector3[n];
            float last = points.Length - 1;
            for (int k = 0; k < n; k++)
            {
                float at = last * k / (n - 1);
                int i = Mathf.Min((int)at, points.Length - 2);
                r[k] = Vector3.Lerp(points[i], points[i + 1], at - i);
            }

            return r;
        }

        /// <summary>The dollar-weighted mean of the runs' first (or last) points: angle on the circle, radius and height linearly.</summary>
        static Vector3 PolarMean(List<FlowPath> run, bool first, double dollars)
        {
            double c = 0, sn = 0, r = 0, y = 0;
            foreach (FlowPath f in run)
            {
                Vector3 p = first ? f.Points[0] : f.Points[f.Points.Length - 1];
                double rr = Math.Sqrt(p.x * p.x + p.z * p.z), w = f.Dollars / dollars;
                if (rr > 1e-6)
                {
                    c += w * p.x / rr;
                    sn += w * p.z / rr;
                }

                r += w * rr;
                y += w * p.y;
            }

            float theta = (float)(Math.Atan2(sn, c) * 180.0 / Math.PI);
            return LandFrame.Polar((float)r, theta, (float)y);
        }

        /// <summary>
        /// One arc as a flow line: world width = dollars × the year's width per $B on a 0.8 px floor, alpha scaled below
        /// $20B, pulses toward the end (more pulses for more dollars); dashed (0.06 on, 0.04 off), or fading out to its end.
        /// </summary>
        static void ArcLine(LineMeshBuilder lines, List<LinePoint> pts, Vector3[] points, double dollars, float perB, Color32 color, int id,
            float intensity, bool dashed, bool fadeOut = false)
        {
            color = LandMath.Fade(color, LandStyle.DollarFade(dollars));
            float w = (float)(dollars * perB), phase = ArcPhasePerUnit * Pulses(dollars);
            if (!dashed)
            {
                pts.Clear();
                for (int k = 0; k < points.Length; k++)
                {
                    Color32 c = fadeOut ? LandMath.Fade(color, 1f - k / (float)(points.Length - 1)) : color;
                    pts.Add(new LinePoint(points[k], c, LandStyle.LineFloorPx, w, intensity));
                }

                lines.AddFlowPath(pts, id, phase, LandMath.Hash01(id, points.Length));
                return;
            }

            // dashes along the arc length, each a straight segment (0.06 on, 0.04 off): one walk along the arc
            float on = LandStyle.BorrowDashOn, period = on + LandStyle.BorrowDashOff;
            float segStart = 0, next = 0;
            bool drawing = false;
            Vector3 dashStart = points[0];
            for (int k = 1; k < points.Length; k++)
            {
                Vector3 a = points[k - 1], c = points[k];
                float d = Vector3.Distance(a, c);
                if (d <= 1e-7f) continue;
                while (next <= segStart + d)
                {
                    Vector3 p = Vector3.Lerp(a, c, (next - segStart) / d);
                    if (drawing) lines.AddSegment(dashStart, p, color, LandStyle.LineFloorPx, w, id, intensity);
                    else dashStart = p;
                    next += drawing ? period - on : on;
                    drawing = !drawing;
                }

                segStart += d;
            }

            if (drawing) lines.AddSegment(dashStart, points[points.Length - 1], color, LandStyle.LineFloorPx, w, id, intensity);
        }



        /// <summary>Pulses rise with an edge's dollars (a factor of 1 at $50B, about 2.5 at $1T).</summary>
        static float Pulses(double dollars) => 1f + Mathf.Log10(1f + (float)(dollars / 50));

        /// <summary>The wage patches: brighter light-blue slices of the sectors' wage strips, each with a border.</summary>
        static void Patches(Built b, MoneyFlows m, LandGeometry land)
        {
            if (m.Patches == null) return;
            Color32 veil = LandMath.Tint(LandStyle.Wages, LandStyle.PatchAlpha), border = LandMath.Tint(LandStyle.Wages, PatchBorderAlpha);
            b.Colors.Clear();
            b.Colors.Add(veil);
            foreach (Patch p in m.Patches)
            {
                SectorGeom s = land.Sectors[p.Industry];
                int id = EconomyIds.LandSector(p.Industry, 6);
                float y = s.Y + 0.006f;
                b.A.Clear();
                b.B.Clear();
                LandFrame.SampleBand(b.A, b.B, p.R0 + 0.004f, p.R1 - 0.004f, p.Theta0, p.Theta1, y, 5f);
                b.Fills[GIncome].AddBand(b.A, b.B, b.Colors, id);
                b.A.Clear();
                LandFrame.SampleArc(b.A, p.R1 - 0.004f, p.Theta0, p.Theta1, y + 0.001f, 5f);
                if (p.R0 > 1e-3f) LandFrame.SampleArc(b.A, p.R0 + 0.004f, p.Theta1, p.Theta0, y + 0.001f, 5f);
                else b.A.Add(new Vector3(0, y + 0.001f, 0));
                b.A.Add(b.A[0]);
                b.Lines[GIncome].AddPolyline(b.A, border, PatchBorderPx, 0, id);
            }
        }

        // ---- the ground

        /// <summary>The lip canal: each lane's ice and rose (or steel) ribbons per whole degree, center lines toward the exit.</summary>
        static void Canal(Built b, MoneyFlows m, LandGeometry land)
        {
            float y = LandStyle.CanalY, perB = land.WidthPerB;
            for (int l = 0; l < CanalRouting.Lanes; l++)
            {
                CanalLane lane = m.Canal[l];
                if (lane == null) continue;
                int group = l == LandStyle.LaneTaxes ? GTaxes : GRivers;
                int id = EconomyIds.LandRiver(l);
                float alpha = l == LandStyle.LaneAbroad ? AbroadAlpha : LandStyle.LaneAlpha;

                // runs of degrees that carry water (a lane all the way round starts after its exit)
                int start = -1;
                for (int k = 0; k < 360; k++)
                {
                    int phi = (Deg(lane.Exit) + 1 + k) % 360;
                    if (!Wet(lane, phi))
                    {
                        start = phi;
                        break;
                    }
                }

                bool ring = start < 0;
                if (ring) start = (Deg(lane.Exit) + 1) % 360;
                int k0 = 0;
                while (k0 < 360)
                {
                    int phi0 = (start + k0) % 360;
                    if (!Wet(lane, phi0))
                    {
                        k0++;
                        continue;
                    }

                    int k1 = k0;
                    while (k1 + 1 < 360 && Wet(lane, (start + k1 + 1) % 360)) k1++;
                    LaneRun(b, lane, start + k0, start + k1 + (ring ? 1 : 0), y, perB, alpha, group, id, l);
                    k0 = k1 + 1;
                }

                // center lines: each direction's stretch, drawn toward the exit
                CenterLines(b, lane, y, alpha, group, id);
            }
        }

        /// <summary>The canal's ribbons and center lines are sampled every this many degrees (their ends exactly).</summary>
        const int CanalStepDeg = 2;

        static int Deg(float deg) => ((int)Math.Round(deg) % 360 + 360) % 360;

        static bool Wet(CanalLane lane, int phi) => lane.R1[phi] - lane.R0[phi] > 1e-6f;

        /// <summary>One run of a lane, degrees d0..d1 (may pass 360): the ice ribbon inside, the rose (or steel) one outside.</summary>
        static void LaneRun(Built b, CanalLane lane, int d0, int d1, float y, float perB, float alpha, int group, int id, int l)
        {
            SurfaceMeshBuilder fills = b.Fills[group];
            Color32 ice = LandMath.Tint(Ice, alpha), rose = LandMath.Tint(Rose, alpha);
            Color32 steel = LandMath.Tint(EconomyStyle.Government, alpha);
            for (int part = 0; part < 3; part++)
            {
                b.A.Clear();
                b.B.Clear();
                bool any = false;
                for (int d = d0; d <= d1; d = d == d1 ? d1 + 1 : Math.Min(d1, d + CanalStepDeg))
                {
                    int phi = d % 360;
                    float wf = (float)(lane.Fear[phi] * perB), wd = (float)(lane.Desire[phi] * perB), wp = (float)(lane.Plain[phi] * perB);
                    float gap = wf > 0 && wd > 0 ? LandStyle.LaneGap : 0;
                    float r0 = lane.R0[phi], a, c;
                    if (part == 0)
                    {
                        a = r0;
                        c = r0 + wf;
                    }
                    else if (part == 1)
                    {
                        a = r0 + wf + gap;
                        c = a + wd;
                    }
                    else
                    {
                        a = r0 + wf + gap + wd;
                        c = a + wp;
                    }

                    any |= c - a > 1e-6f;
                    b.A.Add(LandFrame.Polar(a, d, y));
                    b.B.Add(LandFrame.Polar(c, d, y));
                }

                if (!any) continue;
                b.Colors.Clear();
                b.Colors.Add(part == 0 ? ice : part == 1 ? rose : steel);
                fills.AddBand(b.A, b.B, b.Colors, id, LandStyle.LaneIntensity);
            }
        }

        /// <summary>A lane's center lines: every stretch of one direction, from its far end to the exit (pulses toward the fall).</summary>
        static void CenterLines(Built b, CanalLane lane, float y, float alpha, int group, int id)
        {
            int exit = Deg(lane.Exit);
            List<LinePoint> pts = b.Points;
            foreach (int dir in new[] { 1, -1 })
            {
                // walk away from the exit against the flow while the lane flows toward it
                pts.Clear();
                int phi = ((exit - dir) % 360 + 360) % 360;
                List<int> run = new List<int>();
                while (run.Count < 359 && lane.Direction[phi] == dir && Wet(lane, phi))
                {
                    run.Add(phi);
                    phi = ((phi - dir) % 360 + 360) % 360;
                }

                if (run.Count == 0) continue;
                run.Reverse();
                run.Add(exit);
                double dollars = 0;
                for (int i = 0; i < run.Count; i++)
                {
                    int d = run[i];
                    double v = lane.Fear[d] + lane.Desire[d] + lane.Plain[d];
                    if (i % CanalStepDeg != 0 && i != run.Count - 1)
                    {
                        dollars = Math.Max(dollars, v);
                        continue;
                    }

                    dollars = Math.Max(dollars, v);
                    Color32 c = LandMath.Tint(Mix(lane, d), CenterAlpha * alpha / LandStyle.LaneAlpha);
                    pts.Add(new LinePoint(LandFrame.Polar(CanalRouting.LaneMid(lane, d), d, y + 0.002f), c, LandStyle.LineFloorPx, 0, CenterIntensity));
                }

                b.Lines[group].AddFlowPath(pts, id, LandStyle.LanePulsePerUnit * Pulses(dollars));
            }
        }

        /// <summary>A center line's hue at a degree: the lane's larger motive (steel for taxes); never a blend.</summary>
        static Color Mix(CanalLane lane, int phi) =>
            lane.Plain[phi] > 0 ? EconomyStyle.Government : lane.Fear[phi] >= lane.Desire[phi] ? Ice : Rose;

        /// <summary>
        /// Rivulets, waterfalls, rivers, distributaries, the tax river and the abroad pour: each edge as adjacent flat
        /// ribbons (ice inside, a 0.004 gap, rose, then steel for taxes) along the path's horizontal normal, each with a
        /// 0.8 px center line pulsing toward the end.
        /// </summary>
        static void Ground(Built b, MoneyFlows m, float perB)
        {
            foreach (FlowPath f in m.Paths)
            {
                if (f.Kind < FlowKind.Rivulet || f.Points == null || f.Points.Length < 2) continue;
                bool taxes = f.Kind == FlowKind.Taxes;
                int group = taxes ? GTaxes : GRivers;
                int id = f.Kind == FlowKind.Rivulet ? EconomyIds.LandPlayer(f.From, EconomyIds.PlayerRivulet) : EconomyIds.LandRiver(Math.Max(0, f.Lane));
                double fear = taxes ? 0 : f.Fear, desire = taxes ? 0 : f.Desire;
                double plain = Math.Max(0, f.Dollars - fear - desire);
                float alpha = f.Kind == FlowKind.Abroad ? AbroadAlpha : LandStyle.LaneAlpha;
                Ribbons(b, f.Points, fear, desire, plain, perB, alpha, group, id, f.Kind == FlowKind.Abroad);
            }
        }

        static void Ribbons(Built b, Vector3[] p, double fear, double desire, double plain, float perB, float alpha, int group, int id, bool fadeOut)
        {
            int n = p.Length;
            float wf = (float)(fear * perB), wd = (float)(desire * perB), wp = (float)(plain * perB);
            float gap1 = wf > 0 && wd > 0 ? LandStyle.LaneGap : 0, gap2 = wp > 0 && wf + wd > 0 ? LandStyle.LaneGap : 0;
            float total = wf + gap1 + wd + gap2 + wp;
            float[] edges = { -total / 2, -total / 2 + wf, -total / 2 + wf + gap1, -total / 2 + wf + gap1 + wd, total / 2 - wp, total / 2 };
            Color[] hues = { Ice, Rose, EconomyStyle.Government };
            double[] dollars = { fear, desire, plain };
            Vector3[] normal = new Vector3[n];
            float[] fade = new float[n];
            Vector3 last = Vector3.right;
            for (int k = 0; k < n; k++)
            {
                Vector3 t = p[Math.Min(n - 1, k + 1)] - p[Math.Max(0, k - 1)];
                Vector3 nn = new Vector3(-t.z, 0, t.x);
                if (nn.sqrMagnitude > 1e-12f) last = nn.normalized;
                normal[k] = last;
                fade[k] = fadeOut ? Mathf.Clamp01((p[k].y - (LandStyle.CanalY - LandStyle.AbroadFade)) / LandStyle.AbroadFade) : 1f;
            }

            int main = -1;
            for (int part = 0; part < 3; part++)
            {
                if (dollars[part] <= 0) continue;
                if (main < 0 || dollars[part] > dollars[main]) main = part;
                float e0 = edges[part * 2], e1 = edges[part * 2 + 1];
                b.A.Clear();
                b.B.Clear();
                b.Colors.Clear();
                Color32 color = LandMath.Fade(LandMath.Tint(hues[part], alpha), LandStyle.DollarFade(dollars[part]));
                for (int k = 0; k < n; k++)
                {
                    b.A.Add(p[k] + normal[k] * e0);
                    b.B.Add(p[k] + normal[k] * e1);
                    b.Colors.Add(LandMath.Fade(color, fade[k]));
                }

                b.Fills[group].AddBand(b.A, b.B, b.Colors, id, LandStyle.LaneIntensity);
            }

            if (main < 0) return;

            // one 0.8 px center line on the edge's largest ribbon, pulsing toward the end (faster for more dollars)
            float mid = 0.5f * (edges[main * 2] + edges[main * 2 + 1]);
            Color32 line = LandMath.Tint(hues[main], CenterAlpha * alpha / LandStyle.LaneAlpha);
            b.Points.Clear();
            for (int k = 0; k < n; k++)
            {
                b.Points.Add(new LinePoint(p[k] + normal[k] * mid + new Vector3(0, 0.002f, 0), LandMath.Fade(line, fade[k]), LandStyle.LineFloorPx, 0,
                    CenterIntensity));
            }

            b.Lines[group].AddFlowPath(b.Points, id, LandStyle.LanePulsePerUnit * Pulses(fear + desire + plain), LandMath.Hash01(id, n));
        }

        /// <summary>
        /// Glitter = fantasy: one sparkle per $5B of the spending's fantasy dollars, spread over every lane (rivulets, the
        /// canal, waterfalls, rivers, distributaries, the pour) in proportion to each edge's fantasy dollars × length, at
        /// golden-ratio positions along them (deterministic), 0.04-0.24 above the lane, across its width; a 4-point cross.
        /// </summary>
        static void Glitter(Built b, MoneyFlows m, LandGeometry land, PlayerSet players)
        {
            // the edges as weighted pieces: (a, b, half width, fantasy per unit length, lane id)
            List<(Vector3 a, Vector3 c, float half, double weight, int id)> pieces = new List<(Vector3, Vector3, float, double, int)>(4096);
            double fantasy = 0;
            foreach (FlowPath f in m.Paths)
            {
                if (f.Kind < FlowKind.Rivulet || f.Kind == FlowKind.Taxes || f.Fantasy <= 0 || f.Points == null) continue;
                float half = (float)(f.Dollars * land.WidthPerB) * 0.5f;
                int id = f.Kind == FlowKind.Rivulet ? EconomyIds.LandPlayer(f.From, EconomyIds.PlayerRivulet) : EconomyIds.LandRiver(Math.Max(0, f.Lane));
                for (int k = 1; k < f.Points.Length; k++)
                {
                    float len = Vector3.Distance(f.Points[k - 1], f.Points[k]);
                    pieces.Add((f.Points[k - 1], f.Points[k], half, f.Fantasy * len, id));
                }
            }

            foreach (CanalLane lane in m.Canal)
            {
                if (lane == null || lane.Lane == LandStyle.LaneTaxes) continue;
                for (int phi = 0; phi < 360; phi++)
                {
                    if (lane.Fantasy[phi] <= 0) continue;
                    float r = CanalRouting.LaneMid(lane, phi);
                    Vector3 a = LandFrame.Polar(r, phi - 0.5f, LandStyle.CanalY), c = LandFrame.Polar(r, phi + 0.5f, LandStyle.CanalY);
                    pieces.Add((a, c, 0.5f * (lane.R1[phi] - lane.R0[phi]), lane.Fantasy[phi] * Vector3.Distance(a, c), EconomyIds.LandRiver(lane.Lane)));
                }
            }

            foreach (Player p in players.Players)
            {
                for (int c = 0; c < 6; c++) fantasy += p.CategoryFantasy[c];
            }

            int count = (int)Math.Round(fantasy / LandStyle.GlitterPerB);
            double total = 0;
            foreach (var p in pieces) total += p.weight;
            if (count <= 0 || total <= 0) return;
            Color32 white = LandMath.Tint(LandStyle.Fantasy, 1f);
            LineMeshBuilder lines = b.Lines[GGlitter];
            double acc = 0;
            int piece = 0;
            for (int i = 0; i < count; i++)
            {
                double target = (i + LandMath.Golden(i, 0.5f)) / count * total;
                while (piece < pieces.Count - 1 && acc + pieces[piece].weight < target)
                {
                    acc += pieces[piece].weight;
                    piece++;
                }

                var pc = pieces[piece];
                float t = pc.weight > 0 ? Mathf.Clamp01((float)((target - acc) / pc.weight)) : 0.5f;
                Vector3 dir = pc.c - pc.a;
                Vector3 side = new Vector3(-dir.z, 0, dir.x);
                side = side.sqrMagnitude > 1e-12f ? side.normalized : Vector3.right;
                Vector3 at = Vector3.Lerp(pc.a, pc.c, t) + side * (pc.half * (2 * LandMath.Hash01(i, 0x61) - 1)) +
                             new Vector3(0, Mathf.Lerp(LandStyle.GlitterY0, LandStyle.GlitterY1, LandMath.Hash01(i, 0x62)), 0);
                lines.AddSegment(at - new Vector3(GlitterArm, 0, 0), at + new Vector3(GlitterArm, 0, 0), white, GlitterLinePx, 0, pc.id, LandStyle.GlitterIntensity);
                lines.AddSegment(at - new Vector3(0, GlitterArm, 0), at + new Vector3(0, GlitterArm, 0), white, GlitterLinePx, 0, pc.id, LandStyle.GlitterIntensity);
                b.Sparkles++;
            }
        }

        // ---- labels and anchors

        /// <summary>The falls', taxes', abroad's, the brackets' and the neurochemicals' labels; every anchor.</summary>
        static void Texts(Built b, LandSnapshot s, EconomyData data, LandFrame frame)
        {
            MoneyFlows m = s.Money;
            double[] cat = new double[6], fear = new double[6], fant = new double[6];
            foreach (Player p in s.Players.Players)
            {
                for (int c = 0; c < 6; c++)
                {
                    cat[c] += p.Category[c];
                    fear[c] += p.CategoryFear[c];
                    fant[c] += p.CategoryFantasy[c];
                }
            }

            bool estimate = data.Industries.Count > 0 && data.Industries[0].ValueAddedEstimatedFrom > 0 && s.Year >= data.Industries[0].ValueAddedEstimatedFrom;
            string year = LandFacts.Year(s.Year, estimate);
            Vector3[] fallAt = new Vector3[8];
            foreach (FlowPath f in m.Paths)
            {
                if (f.Lane < 0 || f.Points == null) continue;
                if (f.Kind == FlowKind.Waterfall || f.Kind == FlowKind.Taxes && f.Points[0].y >= LandStyle.CanalY - 1e-3f)
                {
                    fallAt[f.Lane] = f.Points[Math.Min(f.Points.Length - 1, 2 + LandStyle.WaterfallPoints / 2)];
                }
                else if (f.Kind == FlowKind.Abroad) fallAt[LandStyle.LaneAbroad] = f.Points[1];
            }

            for (int c = 0; c < 6; c++)
            {
                string id = EconomyData.CategoryIds[c];
                string text = LaneTitle[c] + " · " + LandFacts.Money(cat[c]) + " · " + LandFacts.Percent(cat[c] > 0 ? fear[c] / cat[c] : 0) +
                              " fear · fantasy " + LandFacts.Percent(cat[c] > 0 ? fant[c] / cat[c] : 0);
                b.Labels.Add(new LabelItem
                {
                    Slot = SlotFalls + c, Text = text, Anchor = "land:river:" + id, World = frame.World(fallAt[c]), Group = LandGroup.Rivers,
                    Ids = IdRange.Single(EconomyIds.LandRiver(c)), Present = cat[c] > 0
                });
                b.Anchors.Add(new Anchor
                {
                    Key = "land:river:" + id, Label = LaneTitle[c] + " river",
                    Blurb = year + ": households spend " + LandFacts.Money(cat[c]) + " on " + LaneTitle[c].ToLowerInvariant() + ", " +
                            LandFacts.Percent(cat[c] > 0 ? fear[c] / cat[c] : 0) + " of it from fear and " +
                            LandFacts.Percent(cat[c] > 0 ? fant[c] / cat[c] : 0) + " of it fantasy. It falls at " +
                            Mathf.RoundToInt(m.Fall[c]).ToString(LandFacts.Ci) + "°, next to its main recipients: " + TopSellers(m, data, c) + ".",
                    Level = GraphLevel.Humans, YearsAgo = 0.5, EndYearsAgo = 0, Y = fallAt[c].y, Rho = EconomyStyle.FramingRho,
                    Ids = IdRange.Single(EconomyIds.LandRiver(c)), Tier = 2, Fixed = frame.World(fallAt[c])
                });

                // the neurochemicals beside the fall (mind view)
                List<string> names = new List<string>();
                foreach (string chem in Chemicals[c])
                {
                    Chemical ch = data.Psyche?.Chemicals?.Find(x => x.Id == chem);
                    if (ch != null) names.Add(ch.Name.ToLowerInvariant());
                }

                b.Labels.Add(new LabelItem
                {
                    Slot = SlotChems + c, Text = names.Count > 0 ? string.Join(" · ", names) : " ", Anchor = "land:river:" + id,
                    World = frame.World(fallAt[c] + LandStyle.ChemicalOffset), Group = LandGroup.Mirages,
                    Ids = IdRange.Single(EconomyIds.LandRiver(c)), Present = names.Count > 0 && cat[c] > 0
                });
            }

            double taxes = 0, spending = 0;
            foreach (Player p in s.Players.Players)
            {
                taxes += p.Taxes;
                spending += p.Spending;
            }

            b.Labels.Add(new LabelItem
            {
                Slot = SlotTaxes, Text = "Taxes " + LandFacts.Money(taxes) + " → the state", Anchor = "land:taxes",
                World = frame.World(fallAt[LandStyle.LaneTaxes]), Group = LandGroup.Taxes, Ids = IdRange.Single(EconomyIds.LandRiver(LandStyle.LaneTaxes)),
                Present = taxes > 0
            });
            b.Labels.Add(new LabelItem
            {
                Slot = SlotAbroad, Text = "Abroad " + LandFacts.Money(m.Imports), Anchor = "land:abroad",
                World = frame.World(fallAt[LandStyle.LaneAbroad]), Group = LandGroup.Rivers,
                Ids = IdRange.Single(EconomyIds.LandRiver(LandStyle.LaneAbroad)), Present = m.Imports > 0
            });

            // brackets over the canal at the near side
            for (int k = 0; k < Brackets.Length; k++)
            {
                float theta = BracketTheta[k];
                int phi = Deg(theta);
                float r0 = float.MaxValue, r1 = 0;
                foreach (int l in Brackets[k].lanes)
                {
                    r0 = Mathf.Min(r0, m.Canal[l].R0[phi]);
                    r1 = Mathf.Max(r1, m.Canal[l].R1[phi]);
                }

                Vector3 at = LandFrame.Polar(r1 > r0 ? 0.5f * (r0 + r1) : LandStyle.CanalR0, theta, LandStyle.CanalY + 0.06f);
                b.Labels.Add(new LabelItem
                {
                    Slot = SlotBrackets + k, Text = Brackets[k].name, Anchor = "land:canal", World = frame.World(at), Group = LandGroup.Rivers,
                    Ids = new IdRange(EconomyIds.LandRiver(0), EconomyIds.LandRiver(5)), Present = true
                });
            }

            // the note on the aggregated arcs, over the crown
            b.Labels.Add(new LabelItem
            {
                Slot = SlotNote, Text = NoteText, Anchor = "land:flow:capital",
                World = frame.World(new Vector3(0, LandStyle.CrownY, 0)), Group = LandGroup.CapitalFlows,
                Ids = new IdRange(EconomyIds.LandCapital(0), EconomyIds.LandCapital(4)), Present = true
            });

            // anchors: the canal, taxes, abroad, the flows in the air
            Vector3 canalAt = LandFrame.Polar(LandStyle.CanalR0 + 0.1f, LandStyle.BracketThetaDeg, LandStyle.CanalY);
            b.Anchors.Add(Anchor("land:canal", "The lip canal",
                year + ": every player's spending runs into the canal along the rim and streams round, lane by lane (BASE: necessities and collective; SELFISH: jeopardy and escapism; MATING: status and growth; then taxes and imports), to each category's fall. " +
                LandFacts.Money(spending) + " a year in all.", canalAt, new IdRange(EconomyIds.LandRiver(0), EconomyIds.LandRiver(7)), frame, 2));
            b.Anchors.Add(Anchor("land:taxes", "Taxes",
                year + ": " + LandFacts.Money(taxes) + " of personal taxes run down to the government floor, split between the federal wedge and state & local.",
                fallAt[LandStyle.LaneTaxes], IdRange.Single(EconomyIds.LandRiver(LandStyle.LaneTaxes)), frame, 2));
            b.Anchors.Add(Anchor("land:abroad", "Abroad",
                year + ": " + LandFacts.Money(m.Imports) + " of household spending (imports) pours out over the rim.",
                fallAt[LandStyle.LaneAbroad], IdRange.Single(EconomyIds.LandRiver(LandStyle.LaneAbroad)), frame, 2));
            foreach ((FlowKind kind, string id, string name) in FlowAnchors)
            {
                double dollars = 0;
                FlowPath biggest = null;
                foreach (FlowPath f in m.Paths)
                {
                    if (f.Kind != kind) continue;
                    dollars += f.Dollars;
                    if (biggest == null || f.Dollars > biggest.Dollars) biggest = f;
                }

                if (biggest == null) continue;
                Vector3 mid = biggest.Points[biggest.Points.Length / 2];
                string crown = (kind == FlowKind.Capital || kind == FlowKind.Saving) && b.CrownLoopDollars > 0
                    ? "; the capital-only 1% stand on the crown, so their own capital income and saving (" +
                      LandFacts.Money(b.CrownLoopDollars) + " together) start and end there and are not drawn"
                    : "";
                b.Anchors.Add(Anchor("land:flow:" + id, name, year + ": " + name.ToLowerInvariant() + " " + LandFacts.Money(dollars) + " a year" + FlowNote(kind) + DrawnNote(kind) + crown + ".",
                    mid, IdRange.Empty, frame, 3));
            }
        }

        /// <summary>
        /// How a kind's arcs are drawn (WP6: the arcs to players are aggregated outside the people view, the payouts are a
        /// few streams), so the hover text says which arcs are aggregated.
        /// </summary>
        static string DrawnNote(FlowKind k) =>
            PerPlayer(k) ? "; drawn one arc per player in the people view, one per group and cluster of players elsewhere (widths in dollars)"
            : k == FlowKind.Payout ? "; drawn as a few streams: one per sector paying $250B or more, one per ring for the rest (widths in dollars)"
            : "";

        static string FlowNote(FlowKind k)
        {
            switch (k)
            {
                case FlowKind.Wages: return ", from each player's patch of the sectors' wage strips";
                case FlowKind.WagesCompany: return ", the named companies' expected share of their industry's US employment (no employer is invented per person)";
                case FlowKind.Business: return ", from the sectors' gold strips to the owners' plinths";
                case FlowKind.Capital: return ", falling from the crown onto those who own";
                case FlowKind.Transfers: return ", rising from the government floor";
                case FlowKind.Payout: return ", rising from the private sectors' gold strips into the crown";
                case FlowKind.PayoutAbroad: return ", leaving over the far rim";
                case FlowKind.Saving: return ", rising from the savers to the crown: it becomes ownership";
                case FlowKind.Investment: return ", the net saving, to the industries investment buys from";
                case FlowKind.Credit: return ", lent through banking to those who dissave";
                case FlowKind.Borrowing: return ", through banking to those who spend more than their income";
                default: return "";
            }
        }

        static Anchor Anchor(string key, string label, string blurb, Vector3 local, IdRange ids, LandFrame frame, int tier) => new Anchor
        {
            Key = key, Label = label, Blurb = blurb, Level = GraphLevel.Humans, YearsAgo = 0.5, EndYearsAgo = 0, Y = local.y,
            Rho = EconomyStyle.FramingRho, Ids = ids, Tier = tier, Fixed = frame.World(local)
        };

        /// <summary>A category's three largest first recipients: "health 54%, finance 10%, manufacturing 8%".</summary>
        static string TopSellers(MoneyFlows m, EconomyData data, int c)
        {
            if (m.CategoryToSeller == null) return "";
            int n = m.CategoryToSeller.GetLength(1);
            List<int> idx = new List<int>(n);
            for (int k = 0; k < n; k++) idx.Add(k);
            idx.Sort((a, bb) => m.CategoryToSeller[c, a] != m.CategoryToSeller[c, bb]
                ? m.CategoryToSeller[c, bb].CompareTo(m.CategoryToSeller[c, a])
                : a.CompareTo(bb));
            StringBuilder s = new StringBuilder();
            for (int i = 0; i < 3 && i < n; i++)
            {
                if (i > 0) s.Append(", ");
                s.Append(data.Industries[idx[i]].Name.ToLowerInvariant()).Append(' ').Append(LandFacts.Percent(m.CategoryToSeller[c, idx[i]]));
            }

            return s.ToString();
        }
    }
}
