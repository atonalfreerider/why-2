using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Why.Economy.Data;
using Why.Economy.Land;
using Why.Economy.Model;
using Why.Humans.Smv;
using Debug = UnityEngine.Debug;
using Group = Why.Economy.Land.Group;

namespace Why.Economy.Layers
{
    /// <summary>
    /// The land's inhabitants (SPEC 3.4): every player of the year as a glyph where it stands (rim, tower top, crown).
    /// A disc on the ground (a ring dashed by party: D solid, R dashed, I dotted, M dash-dot; a faint veil; a gold plinth
    /// under owners, gig workers and the 1% on the rim), one dot per member lifeline (the lifeline's own highlight id, so a
    /// dot glows with its line on the road) floating at the height of the member's reason and gold while they are in
    /// control, children as faint dots on a ring around the disc, links between spouses, the OS line at reason 0.5 (the
    /// mind view only), a head ring split ice (fear's share of spending) and rose, a white mirage as large as the player's
    /// fantasy spending at the land's area scale, and the season's standing marks (alpha ring, omega's dim head,
    /// anti-alpha's tick). Labels name the largest player of each group (and the hovered or selected one); anchors
    /// <c>land:player:&lt;key&gt;</c>, <c>land:group:&lt;group&gt;</c> (its largest player) and <c>land:owners</c> (the 1% and
    /// business owners); the mind view's thesis line. Emphasis groups Glyphs, Dots, Mirages (7.2). A new snapshot (a year,
    /// the season's settings) rebuilds the meshes on a worker (a preset's blocking snapshot in the next Tick), uploads
    /// them in Tick and cross-fades from the old ones over <see cref="LandStyle.CrossFadeSeconds"/> (8.3). Order 52.
    /// </summary>
    [GraphScenes(GraphScene.Economy)]
    public sealed class InhabitantsLayer : GraphLayer
    {
        public override int Order => 52;

        /// <summary>The name this layer reports its swaps under (<see cref="LandService.ReportReady"/>).</summary>
        const string ReadyName = "InhabitantsLayer";

        // ------------------------------------------------------------------ drawing details (3.4's looks in pixels and units)

        /// <summary>A dot is a short horizontal stroke this long (land units) drawn <see cref="LandStyle.DotPx"/> wide.</summary>
        const float DotLength = 0.008f;

        /// <summary>The head ring's and the standing ring's widths (px), the OS line's width and alpha.</summary>
        const float HeadPx = 1.6f, HeadIntensity = 1.5f, StandingPx = 1.2f, OsPx = 1f, OsAlpha = 0.55f;

        /// <summary>The disc ring's alpha, the plinth rim's width and intensity, the mirage's dotted line (alpha, dashes).</summary>
        const float DiscAlpha = 0.85f, PlinthRimPx = 1f, PlinthIntensity = 1.4f, MirageStemAlpha = 0.25f;

        const int MirageStemDashes = 3;

        /// <summary>
        /// Segments of the veils, the head ring (per full turn), the mirage and the alpha's ring, the OS line's dashes and
        /// the dash-dot pairs of a mixed ring: small shapes, a few pixels across in every view, kept lean so the glyphs
        /// stay within the layer's vertex budget (8.6).
        /// </summary>
        const int VeilSegments = 12, HeadSegments = 12, MirageSegments = 12, AlphaRingSegments = 24, OsDashes = 6, DashDots = 8;

        /// <summary>Player labels: size (px), height above the head (land units).</summary>
        const float LabelPx = 12f, LabelLift = 0.08f, ThesisPx = 15f, ThesisPriority = 34f;

        /// <summary>
        /// The thesis line is one label in landscape and three stacked lines on a portrait screen (the label system
        /// measures one line; the whole sentence is wider than a phone), this far apart (px before the UI scale).
        /// </summary>
        const int ThesisLines = 3;

        const float ThesisLineGap = 26f;

        /// <summary>The group names on player labels ("Frontline · trade · R · 4.1M adults").</summary>
        static readonly string[] GroupLabel =
        {
            "The 1%", "Owners", "Gig", "PMC", "Public", "Office", "Frontline", "Working poor", "Retirees", "SS retirees",
            "Students", "Out of work"
        };

        // ------------------------------------------------------------------ state

        /// <summary>Everything one snapshot draws, built on a worker: mesh builders, label texts and places, anchors.</summary>
        sealed class Built
        {
            public int Version, Year;
            public readonly LineMeshBuilder Glyphs, Dots, Os, MirageLines;
            public readonly SurfaceMeshBuilder Fills = new SurfaceMeshBuilder(), MirageFills = new SurfaceMeshBuilder();
            public readonly List<PlayerLabel> Labels = new List<PlayerLabel>(160);
            public readonly List<Anchor> Anchors = new List<Anchor>(180);

            /// <summary>Reused point lists for rings, arcs and veils (no allocation per shape).</summary>
            public readonly List<Vector3> Ring = new List<Vector3>(LandStyle.DiscSegments + 1),
                Inner = new List<Vector3>(VeilSegments + 1), Outer = new List<Vector3>(VeilSegments + 1);

            public readonly Color32[] VeilColor = new Color32[1];
            public string Thesis;
            public int Players, DotCount;
            public double Ms;

            public int Vertices => Glyphs.VertexCount + Dots.VertexCount + Os.VertexCount + MirageLines.VertexCount +
                                   Fills.VertexCount + MirageFills.VertexCount;

            /// <summary>
            /// Builders sized for the players and lines to draw (a glyph is about 70 line points, a dot 2, a mirage 20),
            /// so a build allocates each vertex list once instead of growing it.
            /// </summary>
            public Built(int players, int lines)
            {
                Glyphs = new LineMeshBuilder(players * 72 + 64);
                Dots = new LineMeshBuilder(lines * 2 + 512);
                Os = new LineMeshBuilder(players * OsDashes * 2 + 16);
                MirageLines = new LineMeshBuilder(players * (MirageSegments + 1 + 2 * MirageStemDashes) + 16);
            }
        }

        /// <summary>A player's label: text, world position, priority, ids, and whether it always shows (its group's largest).</summary>
        struct PlayerLabel
        {
            public string Text, Anchor;
            public Vector3 World;
            public float Priority;
            public IdRange Ids;
            public bool Largest;
        }

        /// <summary>One upload of a <see cref="Built"/>: the renderers of each emphasis group, and its cross-fade.</summary>
        sealed class Generation
        {
            public MeshRenderer Glyphs, Fills, Dots, Os, MirageLines, MirageFills;
            public float Fade = 1;
            public bool Out;

            public IEnumerable<MeshRenderer> All
            {
                get
                {
                    yield return Glyphs;
                    yield return Fills;
                    yield return Dots;
                    yield return Os;
                    yield return MirageLines;
                    yield return MirageFills;
                }
            }
        }

        EconomyModel model;
        SmvPopulation pop;
        LandFrame frame;
        LabelSystem labelSystem;
        Built first;
        readonly List<Generation> generations = new List<Generation>(2);
        readonly List<LabelSpec> labels = new List<LabelSpec>(160);
        readonly List<bool> largest = new List<bool>(160);
        readonly LabelSpec[] thesis = new LabelSpec[ThesisLines];
        string thesisText;
        int thesisShown, orientationSeen = -1;
        int shownPlayers, labelsVersion = -1, selectedSeen = -2;
        bool highlightSeen;
        float osEase;

        // rebuilds (8.3): the latest snapshot wanted, a worker build, a blocking build for the next Tick
        LandSnapshot wanted;
        Task<Built> building;
        int buildingVersion = -1;

        public override void Prepare(GraphContext ctx)
        {
            Stopwatch sw = Stopwatch.StartNew();
            model = ctx.Shared<EconomyModel>(EconomyModel.SharedKey);
            pop = ctx.Shared<SmvPopulation>(SmvPopulation.SharedKey);
            LandSnapshot s = ctx.Shared<LandSnapshot>(LandService.SharedKey);
            if (model == null || s?.Players?.Players == null)
            {
                Debug.LogWarning("[Why] InhabitantsLayer: no land snapshot, the players are not drawn");
                return;
            }

            frame = EconomyStage.Land();
            first = Build(s, model, pop, frame);
            labelSystem = ctx.Labels;
            ApplyLabels(first, false);
            foreach (LabelSpec spec in labels) ctx.Labels.Add(spec);
            for (int k = 0; k < ThesisLines; k++)
            {
                thesis[k] = new LabelSpec
                {
                    Text = "", Data = frame.World(LandStyle.ThesisLocal), Fixed = true, SizePx = ThesisPx, Color = GraphStyle.Text,
                    Align = TMPro.TextAlignmentOptions.Center, Priority = ThesisPriority, Rank = ThesisLines - k, Hidden = true
                };
            }

            ApplyThesis(first.Thesis, false);   // before Add: the label system measures a spec's text when it is added
            foreach (LabelSpec spec in thesis) ctx.Labels.Add(spec);
            foreach (Anchor a in first.Anchors) Anchors.Register(a);
            Debug.Log("[Why] InhabitantsLayer.Prepare " + sw.Elapsed.TotalMilliseconds.ToString("0", LandFacts.Ci) + " ms: " +
                      first.Vertices.ToString(LandFacts.Ci) + " vertices (glyphs " + (first.Glyphs.VertexCount + first.Fills.VertexCount)
                      .ToString(LandFacts.Ci) + ", dots " + first.Dots.VertexCount.ToString(LandFacts.Ci) + ", OS lines " +
                      first.Os.VertexCount.ToString(LandFacts.Ci) + ", mirages " +
                      (first.MirageLines.VertexCount + first.MirageFills.VertexCount).ToString(LandFacts.Ci) + "); " +
                      first.Players.ToString(LandFacts.Ci) + " players, " + first.DotCount.ToString(LandFacts.Ci) + " dots, " +
                      first.Year.ToString(LandFacts.Ci) + " (build " + first.Ms.ToString("0", LandFacts.Ci) + " ms)");
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
        }

        /// <summary>A new snapshot is on screen: rebuild for it (blocking ones in the next Tick, others on a worker).</summary>
        void OnChanged(LandSnapshot s)
        {
            if (s?.Players?.Players == null || model == null) return;
            wanted = s;
        }

        public override void Tick(GraphContext ctx, CameraRig rig)
        {
            if (generations.Count == 0) return;
            Rebuild();
            float dt = Mathf.Min(0.1f, Time.unscaledDeltaTime);

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

            bool mind = LandView.PresetId == "mind";
            osEase = Mathf.MoveTowards(osEase, mind ? 1 : 0, LandStyle.EmphasisRate * dt);
            float glyphs = LandView.Alpha(LandGroup.Glyphs), dots = LandView.Alpha(LandGroup.Dots), mirages = LandView.Alpha(LandGroup.Mirages);
            foreach (Generation g in generations)
            {
                Show(g.Glyphs, glyphs * g.Fade);
                Show(g.Fills, glyphs * g.Fade);
                Show(g.Dots, dots * g.Fade);
                Show(g.Os, dots * osEase * g.Fade);
                Show(g.MirageLines, mirages * g.Fade);
                Show(g.MirageFills, mirages * g.Fade);
            }

            UpdateLabels();
            KeepLabelsInFrame(rig);
        }

        int framedCam = -1, framedW, framedLabels = -1;

        /// <summary>
        /// Keeps the shown player labels inside the frame (WP5: on a portrait screen the rim's outermost players' labels
        /// ran off its edges): whenever the camera, the screen or the labels changed, each shown label is shifted sideways
        /// just enough (<see cref="LandViewLayer.KeepInFrame"/>).
        /// </summary>
        void KeepLabelsInFrame(CameraRig rig)
        {
            if (rig == null || rig.Cam == null || labelSystem == null) return;
            int shownKey = labelsVersion * 31 + selectedSeen + (highlightSeen ? 7 : 0);
            if (rig.Version == framedCam && Screen.width == framedW && shownKey == framedLabels) return;
            framedCam = rig.Version;
            framedW = Screen.width;
            framedLabels = shownKey;
            bool changed = false;
            foreach (LabelSpec spec in labels) changed |= LandViewLayer.KeepInFrame(rig.Cam, spec, 0, labelSystem.Padding);
            if (changed) labelSystem.MarkDirty();
        }

        static void Show(MeshRenderer r, float alpha)
        {
            if (r == null) return;
            bool on = alpha > 0.003f;
            if (r.enabled != on) r.enabled = on;
            if (on) GraphMaterials.SetAlpha(r.sharedMaterial, alpha);
        }

        /// <summary>
        /// Follows the snapshot wanted: a blocking one builds here (the Tick after it was published), an asynchronous one
        /// on a worker; a finished build is uploaded only if it is still the latest (stale builds are discarded).
        /// </summary>
        void Rebuild()
        {
            if (building != null && building.IsCompleted)
            {
                Task<Built> t = building;
                building = null;
                if (t.IsFaulted) Debug.LogError("[Why] InhabitantsLayer: a rebuild failed: " + t.Exception?.GetBaseException());
                else if (wanted != null && t.Result.Version == wanted.Version)
                {
                    Swap(t.Result);
                    wanted = null;
                }
            }

            if (wanted == null || building != null && buildingVersion == wanted.Version) return;
            if (wanted.Blocking)
            {
                Built b = Build(wanted, model, pop, frame);
                wanted = null;
                Swap(b);
                return;
            }

            if (building != null) return;   // a stale build is still running: the wanted one starts when it ends
            LandSnapshot s = wanted;
            EconomyModel m = model;
            SmvPopulation p = pop;
            LandFrame f = frame;
            buildingVersion = s.Version;
            building = Task.Run(() => Build(s, m, p, f));
        }

        /// <summary>Uploads a build as the new generation; the old ones fade out; labels and anchors follow.</summary>
        void Swap(Built b)
        {
            foreach (Generation g in generations) g.Out = true;
            generations.Add(Create(b, 0f));
            ApplyLabels(b, true);
            foreach (Anchor a in b.Anchors) Anchors.Register(a);
            LandService.ReportReady(ReadyName, b.Version);
        }

        Generation Create(Built b, float fade)
        {
            Generation g = new Generation { Fade = fade };
            g.Fills = Add("InhabitantsFills", b.Fills.ToMesh("InhabitantsFills"), true, 0);
            g.Glyphs = Add("InhabitantsGlyphs", b.Glyphs.ToMesh("InhabitantsGlyphs"), false, 1);
            g.Dots = Add("InhabitantsDots", b.Dots.ToMesh("InhabitantsDots"), false, 1);
            g.Os = Add("InhabitantsOsLines", b.Os.ToMesh("InhabitantsOsLines"), false, 1);
            g.MirageFills = Add("InhabitantsMirageFills", b.MirageFills.ToMesh("InhabitantsMirageFills"), true, 0);
            g.MirageLines = Add("InhabitantsMirageLines", b.MirageLines.ToMesh("InhabitantsMirageLines"), false, 1);
            foreach (MeshRenderer r in g.All) GraphMaterials.SetAlpha(r.sharedMaterial, 0);
            return g;
        }

        MeshRenderer Add(string name, Mesh mesh, bool surface, int queueOffset)
        {
            int queue = LandStyle.QueueGlyphs + queueOffset;
            Material m = GraphMaterials.Raw(surface
                ? GraphMaterials.Surface(Color.white, 1f, queue)
                : GraphMaterials.Line(Color.white, 1f, queue));
            MeshRenderer r = AddMesh(name, mesh, m);
            frame.Place(r.transform);
            r.enabled = false;
            return r;
        }

        static void Release(Generation g)
        {
            foreach (MeshRenderer r in g.All)
            {
                if (r == null) continue;
                MeshFilter f = r.GetComponent<MeshFilter>();
                if (f != null && f.sharedMesh != null) Destroy(f.sharedMesh);
                if (r.sharedMaterial != null) Destroy(r.sharedMaterial);
                Destroy(r.gameObject);
            }
        }

        // ------------------------------------------------------------------ labels

        /// <summary>
        /// Puts a build's labels into the pool of label specs (one per player index; the label system keeps every spec
        /// it was given, so specs are reused across years and the extra ones hidden), and the thesis line's text.
        /// </summary>
        void ApplyLabels(Built b, bool live)
        {
            // new labels (a new year) keep no sideways shift of the old ones: re-frame them even with a still camera
            framedLabels = int.MinValue;
            for (int k = 0; k < b.Labels.Count; k++)
            {
                PlayerLabel pl = b.Labels[k];
                LabelSpec spec;
                if (k < labels.Count)
                {
                    spec = labels[k];
                    if (live) labelSystem.SetText(spec, pl.Text);
                    else spec.Text = pl.Text;
                    largest[k] = pl.Largest;
                }
                else
                {
                    spec = new LabelSpec
                    {
                        Text = pl.Text, Fixed = true, SizePx = LabelPx, Color = GraphStyle.Text,
                        Align = TMPro.TextAlignmentOptions.Center, Hidden = true
                    };
                    labels.Add(spec);
                    largest.Add(pl.Largest);
                    if (live) labelSystem.Add(spec);
                }

                spec.Data = pl.World;
                spec.Priority = pl.Priority;
                spec.Rank = pl.Priority;
                spec.AnchorKey = pl.Anchor;
                spec.Ids = pl.Ids;
            }

            shownPlayers = b.Labels.Count;
            if (live && !string.IsNullOrEmpty(b.Thesis)) ApplyThesis(b.Thesis, true);
            labelsVersion = -1;
            if (live) UpdateLabels();
        }

        /// <summary>
        /// Puts the thesis sentence on its label lines: whole in landscape; on a portrait screen split at its " · "
        /// breaks into three lines (the year and the share in control; fantasy; what sets them apart), stacked around
        /// the thesis point.
        /// </summary>
        void ApplyThesis(string text, bool live)
        {
            thesisText = text;
            orientationSeen = ScreenLayout.OrientationVersion;
            string whole = text ?? "";
            string[] parts = whole.Split(new[] { " · " }, StringSplitOptions.None);
            string[] lines = parts.Length >= 4
                ? new[] { parts[0] + " · " + parts[1], parts[2], string.Join(" · ", parts, 3, parts.Length - 3) }
                : new[] { whole, whole, whole };
            bool split = ScreenLayout.IsPortrait && parts.Length >= 4;
            if (!split) lines[0] = whole;   // landscape: one line; the other two keep their text (the label system skips empty specs) and stay hidden
            thesisShown = whole.Length == 0 ? 0 : split ? ThesisLines : 1;
            for (int k = 0; k < ThesisLines; k++)
            {
                LabelSpec spec = thesis[k];
                if (spec == null) continue;
                if (live && spec.Text != lines[k]) labelSystem?.SetText(spec, lines[k]);
                else spec.Text = lines[k];
                spec.PixelOffset = new Vector2(0, split ? ThesisLineGap * (1 - k) : 0);
            }

            labelsVersion = -1;
        }

        /// <summary>
        /// Shows the largest player of each group while the preset lists the glyphs' labels, and any player that is
        /// hovered (its ids highlighted) or selected while the glyphs are at least 0.3 visible (7.2); the thesis line
        /// with the mirages' labels.
        /// </summary>
        void UpdateLabels()
        {
            if (orientationSeen != ScreenLayout.OrientationVersion && thesisText != null) ApplyThesis(thesisText, true);
            bool highlight = Highlighter.HasHighlight;
            int selected = EconomyState.SelectedPlayer;
            if (labelsVersion == LandView.LabelsVersion && !highlight && !highlightSeen && selected == selectedSeen) return;
            labelsVersion = LandView.LabelsVersion;
            highlightSeen = highlight;
            selectedSeen = selected;
            bool listed = LandView.LabelsShown(LandGroup.Glyphs), visible = LandView.Alpha(LandGroup.Glyphs) >= 0.3f;
            bool changed = false;
            for (int k = 0; k < labels.Count; k++)
            {
                LabelSpec spec = labels[k];
                bool show = k < shownPlayers && (listed && largest[k] ||
                                                 visible && (k == selected || highlight && Highlighter.IsHighlighted(spec.Ids)));
                if (spec.Hidden == !show) continue;
                spec.Hidden = !show;
                changed = true;
            }

            bool thesisOn = LandView.LabelsShown(LandGroup.Mirages);
            for (int k = 0; k < ThesisLines; k++)
            {
                LabelSpec spec = thesis[k];
                bool show = thesisOn && k < thesisShown;
                if (spec == null || spec.Hidden == !show) continue;
                spec.Hidden = !show;
                changed = true;
            }

            if (changed) labelSystem?.MarkDirty();
        }

        // ================================================================== the build (pure: any thread)

        /// <summary>Every glyph, label and anchor of a snapshot's players (worker-safe: builders only, no Unity objects).</summary>
        static Built Build(LandSnapshot s, EconomyModel model, SmvPopulation pop, LandFrame frame)
        {
            Stopwatch sw = Stopwatch.StartNew();
            PlayerSet set = s.Players;
            int lines = 0;
            foreach (Player p in set.Players) lines += p.Adults.Length + p.Children.Length;
            Built b = new Built(set.Players.Length, lines) { Version = s.Version, Year = s.Year };
            EconomicLives lives = model.Lives;
            SmvSimulation sim = lives?.Sim ?? pop?.Sim;
            EconomyData data = model.Data;
            float areaPerB = s.Land?.AreaPerB ?? LandStyle.AreaGdp / 30000f;
            byte[] standing = s.Society?.Standing;
            int[] spouse = Spouses(sim, s.Year, set.PlayerOfPerson.Length);
            int[] largestOf = LargestByGroup(set);
            foreach (Player p in set.Players)
            {
                byte st = standing != null && p.Index < standing.Length ? standing[p.Index] : (byte)0;
                Glyph(b, p, st);
                Dots(b, p, lives, sim, pop, spouse, s.Year);
                Mirage(b, p, areaPerB);
                b.Labels.Add(Label(p, data, frame, largestOf[(int)p.Group] == p.Index));
                b.Anchors.Add(PlayerAnchor(p, data, frame, s.Year));
            }

            GroupAnchors(b, set, largestOf, data, frame, s.Year);
            b.Thesis = Thesis(lives, s.Year, data);
            b.Players = set.Players.Length;
            b.Ms = sw.Elapsed.TotalMilliseconds;
            return b;
        }

        /// <summary>Each person's spouse at the year's middle (MarriageLog: Start ≤ Y + 0.5 &lt; End), or -1.</summary>
        static int[] Spouses(SmvSimulation sim, int year, int n)
        {
            int[] spouse = new int[n];
            for (int i = 0; i < n; i++) spouse[i] = -1;
            if (sim == null) return spouse;
            double mid = year + 0.5;
            foreach (SmvMarriage m in sim.MarriageLog)
            {
                if (m.Start > mid || m.End <= mid || m.Wife < 0 || m.Husband < 0 || m.Wife >= n || m.Husband >= n) continue;
                spouse[m.Wife] = m.Husband;
                spouse[m.Husband] = m.Wife;
            }

            return spouse;
        }

        /// <summary>The largest player (most adults; ties: the lower index) of each group, -1 for an empty group.</summary>
        static int[] LargestByGroup(PlayerSet set)
        {
            int[] best = new int[12];
            for (int g = 0; g < 12; g++) best[g] = -1;
            foreach (Player p in set.Players)
            {
                int g = (int)p.Group;
                if (best[g] < 0 || p.Adults.Length > set.Players[best[g]].Adults.Length) best[g] = p.Index;
            }

            return best;
        }

        // ------------------------------------------------------------------ the glyph: disc, tribe, plinth, head, standing

        static void Glyph(Built b, Player p, byte standing)
        {
            Vector3 c = Figure.Center(p);
            float r = p.Radius;
            int disc = EconomyIds.LandPlayer(p.Index, EconomyIds.PlayerDisc), head = EconomyIds.LandPlayer(p.Index, EconomyIds.PlayerHead);
            Color32 blue = LandMath.Tint(EconomyStyle.People, DiscAlpha);

            // plinth: a gold veil disc and its rim under the disc ("they stand on their own business")
            if (p.Plinth)
            {
                float pr = r + LandStyle.PlinthPad;
                Veil(b, b.Fills, c + new Vector3(0, 0.002f, 0), pr, LandMath.Tint(EconomyStyle.Capital, LandStyle.PlinthAlpha), disc);
                Ring(b, b.Glyphs, c + new Vector3(0, 0.003f, 0), pr, LandStyle.DiscSegments, LandMath.Tint(EconomyStyle.Capital, 0.8f),
                    PlinthRimPx, disc, PlinthIntensity);
            }

            // the disc: veil and the ring dashed by party
            Veil(b, b.Fills, c + new Vector3(0, 0.004f, 0), r, LandMath.Tint(EconomyStyle.People, LandStyle.DiscVeilAlpha), disc);
            TribeRing(b, c + new Vector3(0, 0.005f, 0), r, p.Tribe, blue, disc);

            // the head ring: fear's share of the spending in ice from 90° counter-clockwise, the rest in rose
            double spend = 0, fear = 0;
            for (int k = 0; k < 6; k++)
            {
                spend += p.Category[k];
                fear += p.CategoryFear[k];
            }

            float fearShare = spend > 0 ? (float)(fear / spend) : p.Fear;
            float headAlpha = standing == 2 ? LandStyle.OmegaHeadAlpha : 0.95f;
            Vector3 h = Figure.Head(p);
            float hr = LandStyle.HeadRingScale * r, split = 90f + 360f * Mathf.Clamp01(fearShare);
            Arc(b, b.Glyphs, h, hr, 90f, split, LandMath.Tint(EconomyStyle.Fear, headAlpha), HeadPx, head, HeadIntensity);
            Arc(b, b.Glyphs, h, hr, split, 450f, LandMath.Tint(EconomyStyle.Desire, headAlpha), HeadPx, head, HeadIntensity);

            // standing (5.3): alpha a second ring, omega the dim head (above), anti-alpha a steel tick over the head
            if (standing == 1)
            {
                Ring(b, b.Glyphs, c + new Vector3(0, 0.006f, 0), LandStyle.AlphaRingScale * r, AlphaRingSegments,
                    LandMath.Tint(EconomyStyle.Cooperate, 0.9f), StandingPx, disc, LandStyle.AlphaRingIntensity);
            }
            else if (standing == 3)
            {
                b.Glyphs.AddSegment(h + new Vector3(0, 0.02f, 0), h + new Vector3(0, 0.02f + LandStyle.AntiAlphaTick, 0),
                    LandMath.Tint(EconomyStyle.Government, 0.9f), StandingPx, 0, head, 1.3f);
            }
        }

        /// <summary>The disc's ring: D solid, R 16 dashes, I 32 dots, M dash-dot (party without hue: blue means people).</summary>
        static void TribeRing(Built b, Vector3 c, float r, char tribe, Color32 color, int id)
        {
            LineMeshBuilder lines = b.Glyphs;
            switch (tribe)
            {
                case 'R':
                    Dashes(lines, c, r, LandStyle.TribeDashes, 0.55f, 0, color, LandStyle.DiscPx, id);
                    break;
                case 'I':
                    Dashes(lines, c, r, LandStyle.TribeDots, 0.22f, 0, color, LandStyle.DiscPx, id);
                    break;
                case 'M':
                    Dashes(lines, c, r, DashDots, 0.45f, 0, color, LandStyle.DiscPx, id);
                    Dashes(lines, c, r, DashDots, 0.10f, 0.65f, color, LandStyle.DiscPx, id);
                    break;
                default:
                    Ring(b, lines, c, r, LandStyle.DiscSegments, color, LandStyle.DiscPx, id, LandStyle.DiscIntensity);
                    break;
            }
        }

        /// <summary>A closed ring of segments around a center (horizontal).</summary>
        static void Ring(Built b, LineMeshBuilder lines, Vector3 c, float r, int segments, Color32 color, float px, int id,
            float intensity)
        {
            List<Vector3> pts = b.Ring;
            pts.Clear();
            for (int k = 0; k <= segments; k++)
            {
                float a = 2 * Mathf.PI * k / segments;
                pts.Add(new Vector3(c.x + r * Mathf.Cos(a), c.y, c.z + r * Mathf.Sin(a)));
            }

            lines.AddPolyline(pts, color, px, 0, id, intensity);
        }

        /// <summary>An arc of a horizontal ring from one angle to another (degrees, counter-clockwise).</summary>
        static void Arc(Built b, LineMeshBuilder lines, Vector3 c, float r, float from, float to, Color32 color, float px, int id,
            float intensity)
        {
            if (to - from < 0.5f) return;
            int n = Mathf.Max(2, Mathf.CeilToInt(HeadSegments * (to - from) / 360f));
            List<Vector3> pts = b.Ring;
            pts.Clear();
            for (int k = 0; k <= n; k++)
            {
                float a = Mathf.Lerp(from, to, k / (float)n) * Mathf.Deg2Rad;
                pts.Add(new Vector3(c.x + r * Mathf.Cos(a), c.y, c.z + r * Mathf.Sin(a)));
            }

            lines.AddPolyline(pts, color, px, 0, id, intensity);
        }

        /// <summary><paramref name="count"/> dashes around a ring, each <paramref name="duty"/> of its period, starting at <paramref name="phase"/> of it.</summary>
        static void Dashes(LineMeshBuilder lines, Vector3 c, float r, int count, float duty, float phase, Color32 color, float px, int id)
        {
            for (int k = 0; k < count; k++)
            {
                float a0 = 2 * Mathf.PI * (k + phase) / count, a1 = 2 * Mathf.PI * (k + phase + duty) / count;
                lines.AddSegment(new Vector3(c.x + r * Mathf.Cos(a0), c.y, c.z + r * Mathf.Sin(a0)),
                    new Vector3(c.x + r * Mathf.Cos(a1), c.y, c.z + r * Mathf.Sin(a1)), color, px, 0, id, LandStyle.DiscIntensity);
            }
        }

        /// <summary>A filled horizontal disc (a fan of <see cref="VeilSegments"/> as one band from the center).</summary>
        static void Veil(Built b, SurfaceMeshBuilder fills, Vector3 c, float r, Color32 color, int id, int segments = VeilSegments)
        {
            b.Inner.Clear();
            b.Outer.Clear();
            for (int k = 0; k <= segments; k++)
            {
                float a = 2 * Mathf.PI * k / segments;
                b.Inner.Add(c);
                b.Outer.Add(new Vector3(c.x + r * Mathf.Cos(a), c.y, c.z + r * Mathf.Sin(a)));
            }

            b.VeilColor[0] = color;
            fills.AddBand(b.Inner, b.Outer, b.VeilColor, id);
        }

        // ------------------------------------------------------------------ the dots: lifelines, children, spouses, the OS line

        /// <summary>
        /// One dot per adult member (sunflower slot, height = 0.03 + 0.25 × reason, gold while in control) carrying the
        /// lifeline's highlight id; children as faint small dots on a ring outside the disc; spouses linked; the OS line.
        /// </summary>
        static void Dots(Built b, Player p, EconomicLives lives, SmvSimulation sim, SmvPopulation pop, int[] spouse, int year)
        {
            int n = p.Adults.Length;
            Vector3[] at = new Vector3[n];
            Color32 blue = LandMath.Tint(EconomyStyle.People, 0.95f), gold = LandMath.Tint(EconomyStyle.Capital, 1f);
            Vector3 half = new Vector3(0.5f * DotLength, 0, 0);
            for (int k = 0; k < n; k++)
            {
                int i = p.Adults[k];
                float reason = 0.4f;
                bool control = false;
                if (lives != null && lives.TryGet(i, year, out PersonYear r))
                {
                    reason = r.Reason;
                    control = r.InControl;
                }

                at[k] = Figure.DotSlot(p, k, n, reason);
                b.Dots.AddSegment(at[k] - half, at[k] + half, control ? gold : blue, LandStyle.DotPx, 0, LineId(pop, sim, i),
                    control ? 1.5f : 1.2f);
            }

            Color32 child = LandMath.Tint(EconomyStyle.People, LandStyle.ChildDotAlpha);
            for (int k = 0; k < p.Children.Length; k++)
            {
                Vector3 d = Figure.ChildSlot(p, k, p.Children.Length);
                b.Dots.AddSegment(d - half, d + half, child, LandStyle.ChildDotPx, 0, LineId(pop, sim, p.Children[k]));
            }

            // spouses in the same player: the joint goal of partnership
            Color32 link = LandMath.Tint(EconomyStyle.People, LandStyle.SpouseAlpha);
            int disc = EconomyIds.LandPlayer(p.Index, EconomyIds.PlayerDisc);
            for (int k = 0; k < n; k++)
            {
                int s = p.Adults[k] < spouse.Length ? spouse[p.Adults[k]] : -1;
                if (s <= p.Adults[k]) continue;
                int j = Array.BinarySearch(p.Adults, s);
                if (j >= 0) b.Dots.AddSegment(at[k], at[j], link, 1f, 0, disc);
            }

            // the OS line: a dashed ring at the height of reason 0.5 (the mind view shows it)
            Vector3 os = Figure.Center(p) + new Vector3(0, LandStyle.OsLineY, 0);
            Dashes(b.Os, os, p.Radius, OsDashes, 0.5f, 0.25f, LandMath.Tint(GraphStyle.Text, OsAlpha), OsPx, disc);
            b.DotCount += n + p.Children.Length;
        }

        static int LineId(SmvPopulation pop, SmvSimulation sim, int person) =>
            pop != null && sim != null && person >= 0 && person < sim.People.Count ? pop.Id(sim.People[person]) : GraphIds.None;

        // ------------------------------------------------------------------ the mirage

        /// <summary>
        /// The player's fantasy spending as a white veil disc at the land's area scale (rim line 1 px), floating at
        /// 0.42 + 0.30 × fantasy's share of its spending above the disc, with a dotted stem down to the head.
        /// </summary>
        static void Mirage(Built b, Player p, float areaPerB)
        {
            double fantasy = 0;
            for (int k = 0; k < 6; k++) fantasy += p.CategoryFantasy[k];
            if (fantasy <= 0) return;
            float share = p.Spending > 0 ? (float)(fantasy / p.Spending) : p.Fantasy;
            float r = Figure.MirageRadius(fantasy, areaPerB);
            Vector3 c = Figure.Center(p);
            Vector3 m = c + new Vector3(0, Figure.MirageY(share), 0);
            int id = EconomyIds.LandPlayer(p.Index, EconomyIds.PlayerMirage);
            Veil(b, b.MirageFills, m, r, LandMath.Tint(LandStyle.Fantasy, LandStyle.MirageAlpha), id, MirageSegments);
            Ring(b, b.MirageLines, m, r, MirageSegments, LandMath.Tint(LandStyle.Fantasy, LandStyle.MirageRimAlpha), 1f, id, 1f);
            Vector3 top = m, bottom = Figure.Head(p) + new Vector3(0, 0.02f, 0);
            Color32 stem = LandMath.Tint(LandStyle.Fantasy, MirageStemAlpha);
            for (int k = 0; k < MirageStemDashes; k++)
            {
                float t0 = (k + 0.25f) / MirageStemDashes, t1 = (k + 0.6f) / MirageStemDashes;
                b.MirageLines.AddSegment(Vector3.Lerp(bottom, top, t0), Vector3.Lerp(bottom, top, t1), stem, 1f, 0, id);
            }
        }

        // ------------------------------------------------------------------ labels, anchors, the thesis line

        /// <summary>"Frontline · trade · R · 4.1M adults" above the head: priority 12 + people (millions).</summary>
        static PlayerLabel Label(Player p, EconomyData data, LandFrame frame, bool largest)
        {
            string text = GroupLabel[(int)p.Group] + " · " + AnchorName(p.AnchorId, data) + " · " + p.Tribe + " · " +
                          LandFacts.Millions(p.Adults.Length * 1e5) + " adults";
            return new PlayerLabel
            {
                Text = text, World = frame.World(Figure.Head(p) + new Vector3(0, LabelLift, 0)), Priority = 12f + (float)(p.People / 1e6),
                Ids = EconomyIds.LandPlayers(p.Index, p.Index), Anchor = "land:player:" + p.Key, Largest = largest
            };
        }

        /// <summary>An anchor's short name: "trade", "state & local", "services", "pensions (Boomers)", "other industries".</summary>
        static string AnchorName(string anchorId, EconomyData data)
        {
            if (string.IsNullOrEmpty(anchorId)) return "?";
            if (anchorId == "rest") return "other industries";
            string id = anchorId.StartsWith("tier:", StringComparison.Ordinal) ? anchorId.Substring(5) : anchorId;
            int slash = id.IndexOf('/');
            if (slash > 0)
            {
                string gen = id.Substring(slash + 1);
                GenerationInfo gi = data.GroupsFile?.Generations?.Find(x => x.Id == gen);
                return Plain(id.Substring(0, slash)) + " (" + (gi?.Name ?? gen) + ")";
            }

            return Plain(id);
        }

        /// <summary>An id in words: "state_local" → "state & local", "real_estate" → "real estate", "social_security" → "social security".</summary>
        static string Plain(string id)
        {
            switch (id)
            {
                case "state_local": return "state & local";
                case "oil_gas": return "oil & gas";
                case "mining_metals": return "mining & metals";
                case "media_telecom": return "media & telecom";
                default: return id.Replace('_', ' ');
            }
        }

        /// <summary>land:player:&lt;key&gt;: who the player is, its money and mind in one generated blurb.</summary>
        static Anchor PlayerAnchor(Player p, EconomyData data, LandFrame frame, int year)
        {
            SocialGroup g = (int)p.Group < (data.GroupsFile?.Groups?.Count ?? 0) ? data.GroupsFile.Groups[(int)p.Group] : null;
            string name = (g?.Name ?? GroupLabel[(int)p.Group]) + " · " + AnchorName(p.AnchorId, data) + " · " + TribeName(p.Tribe);
            return new Anchor
            {
                Key = "land:player:" + p.Key, Label = name, Blurb = Blurb(p, year), Level = GraphLevel.Humans, YearsAgo = 0.5,
                EndYearsAgo = 0, Y = Figure.Head(p).y, Rho = EconomyStyle.FramingRho, Ids = EconomyIds.LandPlayers(p.Index, p.Index),
                Tier = 2, Fixed = frame.World(Figure.Head(p))
            };
        }

        static string TribeName(char t) => t == 'D' ? "Democrats" : t == 'R' ? "Republicans" : t == 'I' ? "independents" : "mixed party";

        /// <summary>The player's numbers in words (generated; the comparison words follow the numbers).</summary>
        static string Blurb(Player p, int year)
        {
            double fantasy = 0, fear = 0, spend = 0;
            for (int k = 0; k < 6; k++)
            {
                fantasy += p.CategoryFantasy[k];
                fear += p.CategoryFear[k];
                spend += p.Category[k];
            }

            StringBuilder s = new StringBuilder(320);
            s.Append(year.ToString(LandFacts.Ci)).Append(": ").Append(LandFacts.Millions(p.Adults.Length * 1e5)).Append(" adults and ")
                .Append(LandFacts.Millions(p.Children.Length * 1e5)).Append(" children (")
                .Append(LandFacts.Count(p.Adults.Length + p.Children.Length)).Append(" lines). Income: wages ")
                .Append(LandFacts.Money(p.Wages)).Append(", business ").Append(LandFacts.Money(p.Business)).Append(", capital ")
                .Append(LandFacts.Money(p.Capital)).Append(", transfers ").Append(LandFacts.Money(p.Transfers)).Append("; taxes ")
                .Append(LandFacts.Money(p.Taxes)).Append(". Spends ").Append(LandFacts.Money(p.Spending)).Append(" (fear ")
                .Append(LandFacts.Percent(spend > 0 ? fear / spend : 0)).Append(", fantasy ")
                .Append(LandFacts.Percent(spend > 0 ? fantasy / spend : 0)).Append("). Reason ").Append(LandFacts.Num(p.Reason, 2))
                .Append(", ").Append(LandFacts.Percent(p.InControl)).Append(" in control. Party is drawn independently of class in this model.");
            return s.ToString();
        }

        /// <summary>land:group:&lt;group&gt; (the group's largest player) and land:owners (the 1% and business owners: one id range).</summary>
        static void GroupAnchors(Built b, PlayerSet set, int[] largestOf, EconomyData data, LandFrame frame, int year)
        {
            GroupsFile gf = data.GroupsFile;
            int lastOwner = -1;
            for (int g = 0; g < 12; g++)
            {
                int k = largestOf[g];
                if (k < 0) continue;
                Player p = set.Players[k];
                SocialGroup sg = gf != null && g < gf.Groups.Count ? gf.Groups[g] : null;
                string id = sg?.Id ?? PlayerCensus.GroupShort[g];
                int first = -1, last = -1, count = 0;
                foreach (Player q in set.Players)
                {
                    if ((int)q.Group != g) continue;
                    first = first < 0 ? q.Index : first;
                    last = q.Index;
                    count++;
                }

                b.Anchors.Add(new Anchor
                {
                    Key = "land:group:" + id, Label = sg?.Name ?? GroupLabel[g],
                    Blurb = (sg?.Rule != null ? sg.Rule + ". " : "") + count.ToString(LandFacts.Ci) + " players in " +
                            year.ToString(LandFacts.Ci) + "; the largest: " + GroupLabel[g] + " · " + AnchorName(p.AnchorId, data) +
                            " · " + p.Tribe + ". " + (sg?.Where ?? ""),
                    Level = GraphLevel.Humans, YearsAgo = 0.5, EndYearsAgo = 0, Y = Figure.Head(p).y, Rho = EconomyStyle.FramingRho,
                    Ids = EconomyIds.LandPlayers(first, last), Tier = 1, Fixed = frame.World(Figure.Head(p))
                });
                if (g <= (int)Group.Owners) lastOwner = Math.Max(lastOwner, last);
            }

            if (lastOwner < 0) return;
            Player top = set.Players[largestOf[(int)Group.Top1] >= 0 ? largestOf[(int)Group.Top1] : 0];
            b.Anchors.Add(new Anchor
            {
                Key = "land:owners", Label = "Owners",
                Blurb = "The 1% and business owners: the players who own capital, on the crown, on tower tops and on gold plinths.",
                Level = GraphLevel.Humans, YearsAgo = 0.5, EndYearsAgo = 0, Y = Figure.Head(top).y, Rho = EconomyStyle.FramingRho,
                Ids = EconomyIds.LandPlayers(0, lastOwner), Tier = 1, Fixed = frame.World(Figure.Head(top))
            });
        }

        /// <summary>
        /// The mind view's thesis line (6), from the lives: "2025 · 12% of adults are in control · fantasy is 19% of
        /// spending, as much for those in control (18%) as for the rest (17%) · what sets them apart: reason 0.70 vs 0.36,
        /// and where their money comes from". The comparison word follows the numbers (±10%).
        /// </summary>
        static string Thesis(EconomicLives lives, int year, EconomyData data)
        {
            if (lives == null || !lives.Ready || lives.Sim == null) return null;
            int n = lives.Sim.People.Count, adults = 0, control = 0;
            double spend = 0, fantasy = 0, fantC = 0, fantR = 0, reasonC = 0, reasonR = 0;
            for (int i = 0; i < n; i++)
            {
                if (!lives.TryGet(i, year, out PersonYear r) || !r.Adult) continue;
                adults++;
                spend += r.Spending;
                fantasy += r.Spending * r.Fantasy;
                if (r.InControl)
                {
                    control++;
                    fantC += r.Fantasy;
                    reasonC += r.Reason;
                }
                else
                {
                    fantR += r.Fantasy;
                    reasonR += r.Reason;
                }
            }

            int rest = adults - control;
            if (adults == 0 || control == 0 || rest == 0) return null;
            double fc = fantC / control, fr = fantR / rest;
            string how = LandFacts.Compare(fc, fr) == 0 ? "as much for those in control (" + LandFacts.Percent(fc) + ") as for the rest ("
                : (LandFacts.Compare(fc, fr) > 0 ? "more" : "less") + " for those in control (" + LandFacts.Percent(fc) + ") than for the rest (";
            bool estimate = data.Industries.Count > 0 && data.Industries[0].ValueAddedEstimatedFrom > 0 &&
                            year >= data.Industries[0].ValueAddedEstimatedFrom;
            return LandFacts.Year(year, estimate) + " · " + LandFacts.Percent(control / (double)adults) + " of adults are in control · " +
                   "fantasy is " + LandFacts.Percent(spend > 0 ? fantasy / spend : 0) + " of spending, " + how + LandFacts.Percent(fr) +
                   ") · what sets them apart: reason " + LandFacts.Num(reasonC / control, 2) + " vs " + LandFacts.Num(reasonR / rest, 2) +
                   ", and where their money comes from";
        }
    }
}
