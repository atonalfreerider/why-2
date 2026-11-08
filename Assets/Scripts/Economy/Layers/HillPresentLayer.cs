using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using Why.Economy.Emphasis;
using Why.Economy.Land;
using Why.Economy.Model;
using Why.UI;

namespace Why.Economy.Layers
{
    /// <summary>
    /// The present, taken apart (design "exploded", first slice: E1 and the core of E2): the shown year's cut face pulled
    /// out of the wave into the empty future beyond the crest and exploded into a full-scale hillside economy
    /// (<see cref="PresentLayout"/>). From the bottom up:
    /// <list type="bullet">
    /// <item>the state's bedrock, its top the valley floor, its thickness the year's outlays (all levels of government,
    /// InstantModel's NIPA 3.1 account), in strata on its faces (its own work, benefits, supplies, interest, subsidies) with a
    /// white line where receipts end (below it, borrowing);</item>
    /// <item>four terraces, each its tier's column of the cut face, as wide as the tier's share of value added and
    /// <see cref="PresentLayout.D"/> deep, lifted apart from one another;</item>
    /// <item>each industry's ridge swollen into a hill on its terrace, its footprint its value added, its relief the
    /// wave's;</item>
    /// <item>each hill a field of peaks (<see cref="PresentPeaks"/>): every named firm a peak as wide as its share of the
    /// industry and as tall as its net margin, the rest of the industry smaller peaks at its typical margin; a glass plane
    /// at normal profit on every terrace, gold where a peak rises above it (advantage);</item>
    /// <item>veins: the largest purchases between industries, branching into the buyer's largest firms; gold threads
    /// from the surplus and the most profitable firms to who holds corporate stock; and what moves along them, packets on
    /// the flow clock (<see cref="PacketMeshBuilder"/>, <see cref="FlowClock"/>): the goods bought as squares from the seller
    /// to the buyer's firms, the money paid for them coming back as dots on the largest few veins, and capital rising as
    /// gold ingots to the owners, so the present is never still, at a frozen year, a paused tour or a held replay;</item>
    /// <item>faint guides from each part's near corners back to the crest face it came from, and labels: the tiers' value
    /// added and jobs, the state's account, the largest firms and industries.</item>
    /// </list>
    /// Entering the view (key I, the bar or the catalog) plays the 6.4 s choreography: face, cut, grow, separate, rise,
    /// arrive. Shift + I plays it backward in 2 s and returns to the overview. Every mesh is built once per land, in one
    /// <see cref="HillRebuilds"/> slot, the first time the view is wanted; the choreography moves root transforms and sets
    /// materials' alpha only (no mesh rebuilds, no allocation per frame), and the packets move in the shader alone. Nothing is
    /// built or drawn outside the view.
    /// </summary>
    [GraphScenes(EconomyLayouts.HillsScene)]
    public sealed class HillPresentLayer : GraphLayer
    {
        /// <summary>The view's id (HillPresets), its smoke sequence (<c>-smokeextra present</c>) and its log's tag.</summary>
        public const string View = "present", Sequence = "present";

        public override int Order => 58;

        enum Part { Bedrock, Strata, Terraces, Hills, Guides, Firms, Flows, Capital, Count }

        /// <summary>Each part's emphasis key, by <see cref="Part"/>: its alpha in every view is the emphasis table's.</summary>
        static readonly int[] Keys = EconomyEmphasis.Keys("present", "bedrock", "strata", "terraces", "hills", "guides", "firms", "flows", "capital");

        static float Id(Part part) => EconomyEmphasis.Id(Keys[(int)part]);

        /// <summary>The tiers' colours, the bedrock (0) to tech (4): the crest face's (HillWaveLayer).</summary>
        static readonly Color[] TierColor =
        {
            EconomyStyle.Government, EconomyStyle.Matter, EconomyStyle.CapitalDim, Color.Lerp(EconomyStyle.CapitalDim, EconomyStyle.Capital, .5f), EconomyStyle.Capital
        };

        /// <summary>The state's strata, top down: its own work, benefits and other transfers, supplies bought net, interest,
        /// subsidies (InstantState's parts of its outlays).</summary>
        static readonly Color[] StratumColor =
        {
            new Color(.74f, .78f, .84f), new Color(.62f, .84f, 1f), new Color(.50f, .55f, .62f), new Color(.70f, .62f, .44f), new Color(.42f, .45f, .50f)
        };

        static readonly string[] TierName = { "THE STATE", "RAW MATERIALS", "MANUFACTURING · INFRASTRUCTURE", "SERVICES", "TECH" };

        /// <summary>Where the light comes from for the hills' shading (land-local, toward the light).</summary>
        static readonly Vector3 Light = new Vector3(-.45f, .8f, .4f).normalized;

        /// <summary>A hill mesh's grid: at most this many columns across its band (about one a crest unit) and this many rows
        /// along the depth; with the slabs and the bedrock about 20,000 vertices in all, inside the Quest's budget (design §9.3).</summary>
        const int MaxColumns = 96, Rows = 32;

        /// <summary>The most renderers the view may keep active on a desktop (design §10, A6).</summary>
        const int MaxRenderers = 16;

        /// <summary>The largest coincidence of the parts with the crest face at the clock's start (A2), land units.</summary>
        const float MaxCoincidence = .5f;

        /// <summary>The labels the view gives to firms and to industries (per terrace): the largest by market value and by value
        /// added.</summary>
        const int FirmLabels = 8, IndustryLabels = 2;

        /// <summary>Smoke runs show the view at rest when the catalog focuses it (frame-exact captures); the present's own
        /// sequence plays the choreography.</summary>
        static readonly bool SmokeRun = Environment.GetCommandLineArgs().Contains("-smoketest");

        /// <summary>The view's key for the HUD's legend (<see cref="HillPresets.Key"/>): the tiers' and the state's colours and
        /// the year's area scale, kept from the last build.</summary>
        public static HillPresets.ViewKey Key { get; private set; }

        // after every static field (the key reads the colours declared below it)
        static HillPresentLayer() => Key = MakeKey(0);

        InstantModel model;
        LandSnapshot snapshot;
        HillLandscape land;
        PresentLayout layout;
        PresentPeaks peaks;

        /// <summary>The named firms' summits at the choreography's end (land-local) and their peaks, for the capital threads.</summary>
        readonly List<(Vector3 at, PresentPeaks.Peak peak)> firmTops = new List<(Vector3, PresentPeaks.Peak)>();

        /// <summary>The colours of what a block is: imported or unallocated inputs, wages, production taxes, surplus.</summary>
        static readonly Color Surplus = EconomyStyle.Capital;

        /// <summary>Advantage's gold, from the plane (deep) to the highest margins (bright), and the normal-profit plane's glass.</summary>
        static readonly Color GoldDeep = new Color(.78f, .52f, .14f), GoldBright = new Color(1f, .88f, .52f), Glass = new Color(.80f, .90f, 1f);

        /// <summary>The margin above normal at which a peak's gold is brightest, and the contour interval (net margin).</summary>
        const float GoldSpan = .3f, Contour = .05f;

        /// <summary>A terrace's glass: its tier's earth tone greyed (gold is kept for advantage).</summary>
        static Color SlabTone(int tier) => Color.Lerp(InputTone[Mathf.Clamp(tier, 0, 4)], new Color(.55f, .58f, .62f), .55f);

        /// <summary>The terrain's grid cell (exploded units).</summary>
        const float Cell = 1.6f;

        /// <summary>The owners of corporate stock: the wealth groups (Fed DFA) and abroad (Fed Z.1).</summary>
        static readonly (string id, string name)[] Owners = { ("top1", "Top 1%"), ("next9", "Next 9%"), ("next40", "Next 40%"), ("bottom50", "Bottom 50%"), ("abroad", "Abroad") };
        GameObject content;
        readonly Transform[] roots = new Transform[5];
        readonly Transform[] hillRoots = new Transform[5];
        readonly List<(Renderer renderer, Part part)> renderers = new List<(Renderer, Part)>();
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        readonly List<HillLabel> labels = new List<HillLabel>();
        bool labelsShown;

        /// <summary>The renderer of the view's packets (the veins' goods and money and the threads' capital in one mesh, within
        /// A6's budget) and the lanes the last build gave it.</summary>
        MeshRenderer packetRenderer;

        int packetLanes;

        /// <summary>Each packet lane's rate as a build set it, by the lane's key (its flow's ends): a lane rebuilt for another
        /// year or by the replay keeps its rate while the new one stands within <see cref="RateHold"/>, and its seed is its
        /// key's, so its packets do not jump.</summary>
        readonly Dictionary<string, float> laneRates = new Dictionary<string, float>();

        /// <summary>The firms the last build drew, and those it found outside their hill's footprint (A8).</summary>
        int firmCount, firmOutside;

        /// <summary>The choreography's clock (s, 0..Seconds), its direction (+1 forward, -1 back, 0 at rest), its speed, whether
        /// it moves the camera, whether the smoke sequence holds it, and the next camera cue.</summary>
        float clock = PresentLayout.Seconds, speed = 1;
        int direction, cue;
        bool moveCamera, held;

        public override void Prepare(GraphContext ctx) { model = ctx.Shared<InstantModel>(InstantModel.SharedKey); }

        public override void Upload(GraphContext ctx)
        {
            EconomyStage.Land().Place(transform);
            if (SmokeRun) SmokeChecks.RegisterSequence(Sequence, Smoke());
        }

        public override void OnFocus(ViewPreset preset)
        {
            if (preset != null && preset.Id == View)
            {
                // the catalog's frames in a smoke run show the view at rest; everywhere else entering plays it
                if (SmokeRun) Rest();
                else Play(true);
            }
            else
            {
                direction = 0;
                held = false;
            }
        }

        public override void Tick(GraphContext ctx, CameraRig rig)
        {
            if (HillLandscapeLayer.Current == null || LandService.Current == null) return;
            HandleKeys();
            bool here = LandView.PresetId == View;
            // built and drawn only while the view is on screen or fading out of it
            if (!here && EconomyEmphasis.Get(Keys[(int)Part.Terraces]) <= 0)
            {
                if (content) content.SetActive(false);
                ShowLabels(false);
                return;
            }

            if (snapshot != LandService.Current || land != HillLandscapeLayer.Current)
            {
                if (!HillRebuilds.Take()) return;
                Build();
            }

            if (content == null) return;
            content.SetActive(true);
            if (direction != 0 && !held) Advance(rig);
            Apply();
            ShowLabels(here && direction >= 0 && PresentLayout.Reveal(clock) > .5f);
        }

        // ------------------------------------------------------------------ the choreography

        /// <summary>I enters the view (again in it: plays the choreography again); Shift + I plays it backward and returns to
        /// the overview. Not while the tour holds the keys or a text field has them.</summary>
        void HandleKeys()
        {
            Keyboard kb = Keyboard.current;
            GraphRoot root = GraphRoot.Instance;
            // key I opens the present as a contest (HillAdvantageLayer) since 7 Nov 2026; this view is reached from the bar, and
            // Shift + I still takes it back to the overview
            if (kb == null || root == null || !root.AllowPresetKeys || HudKit.TypingInField() || !kb.iKey.wasPressedThisFrame || LandView.PresetId != View || !kb.shiftKey.isPressed) return;
            bool here = LandView.PresetId == View;
            if (kb.shiftKey.isPressed)
            {
                if (here && direction >= 0) Reverse(true);
            }
            else if (!here) root.Focus(View);
            else Play(true);
        }

        /// <summary>Plays the choreography forward from its start; with <paramref name="camera"/> the camera flies its cues
        /// (the cut face, then up and back as the present grows, then the view's pose).</summary>
        void Play(bool camera)
        {
            clock = 0;
            direction = 1;
            speed = 1;
            held = false;
            moveCamera = camera;
            cue = 0;
            if (camera) Fly(HillPresets.PresentFace(), 1f);
        }

        /// <summary>Plays it backward, three times as fast, and ends on the overview.</summary>
        void Reverse(bool camera)
        {
            direction = -1;
            speed = PresentLayout.Seconds / PresentLayout.ReverseSeconds;
            held = false;
            moveCamera = camera;
            if (camera) Fly(HillPresets.PresentFace(), PresentLayout.ReverseSeconds * .8f);
        }

        /// <summary>The view at rest: the choreography's end, no camera of its own.</summary>
        void Rest()
        {
            clock = PresentLayout.Seconds;
            direction = 0;
            held = false;
        }

        /// <summary>Holds the clock (the smoke sequence's frames).</summary>
        void Hold(float seconds)
        {
            clock = Mathf.Clamp(seconds, 0, PresentLayout.Seconds);
            direction = 0;
            held = true;
        }

        void Advance(CameraRig rig)
        {
            clock += GraphTime.Delta * speed * direction;
            if (direction > 0)
            {
                // the camera's cues: up and back as the present grows, then the view's own pose as the hills rise
                if (moveCamera && cue == 0 && clock >= PresentLayout.StartOf(PresentLayout.Phase.Grow))
                {
                    cue = 1;
                    Fly(HillPresets.PresentRise(), 1.4f);
                }

                if (moveCamera && cue == 1 && clock >= PresentLayout.StartOf(PresentLayout.Phase.Rise))
                {
                    cue = 2;
                    Fly(ViewPresets.Get(View), .8f);
                }

                if (clock >= PresentLayout.Seconds)
                {
                    clock = PresentLayout.Seconds;
                    direction = 0;
                }
            }
            else if (clock <= 0)
            {
                clock = 0;
                direction = 0;
                if (moveCamera) GraphRoot.Instance?.Focus("overview", 1.2f);
            }
        }

        static void Fly(ViewPreset preset, float seconds)
        {
            GraphRoot root = GraphRoot.Instance;
            if (preset != null && root != null && root.Rig != null) root.Rig.FlyTo(preset.Pose(), seconds);
        }

        /// <summary>The parts at the clock: the roots' poses, the hills' relief, and every part's alpha (its emphasis times the
        /// choreography's share of it). Transforms and material floats only.</summary>
        void Apply()
        {
            for (int p = 0; p < 5; p++)
            {
                if (!roots[p]) continue;
                PresentLayout.RootPose pose = layout.Root(p, clock);
                roots[p].localPosition = pose.Position;
                roots[p].localScale = pose.Scale;
            }

            float rise = PresentLayout.Rise(clock);
            for (int k = 1; k <= 4; k++)
                if (hillRoots[k])
                    hillRoots[k].localScale = new Vector3(1, Mathf.Max(rise, 1e-3f), 1);
            float guides = PresentLayout.Guides(clock), reveal = PresentLayout.Reveal(clock);
            foreach ((Renderer r, Part part) in renderers)
            {
                float a = EconomyEmphasis.Get(Keys[(int)part]);
                // the veins' and the capital threads' packets share one renderer (A6's budget, filed under the flows): shown
                // while either part is, their highlight ids each part's own
                if (ReferenceEquals(r, packetRenderer)) a = Mathf.Max(a, EconomyEmphasis.Get(Keys[(int)Part.Capital]));
                if (part == Part.Hills) a *= rise > .01f ? 1 : 0;
                else if (part == Part.Guides) a *= guides;
                else if (part == Part.Firms || part == Part.Flows || part == Part.Capital) a *= reveal;
                EconomyEmphasis.Set(r, a);
            }
        }

        void ShowLabels(bool show)
        {
            if (show == labelsShown) return;
            labelsShown = show;
            if (show) HillLabels.Set(this, labels);
            else HillLabels.Clear(this);
        }

        // ------------------------------------------------------------------ the build

        void Build()
        {
            Clear();
            snapshot = LandService.Current;
            land = HillLandscapeLayer.Current;
            var data = LandService.Model?.Data;
            if (land == null || data == null) return;
            InstantYear instant = model?.Year(land.Year);
            InstantState state = instant?.State;
            layout = new PresentLayout(land, data.Industries, state?.Outlays ?? 0);
            var circuit = data.Circuit;
            double normal = circuit != null && circuit.Fortune500Revenue > 0 ? circuit.Fortune500Profits / circuit.Fortune500Revenue : .1;
            peaks = new PresentPeaks(layout, data.Industries, circuit?.Io, normal);
            // the peaks replace the relief: the parts' bounds (A3) and the labels stand on them
            if (peaks.Peaks.Count > 0)
                for (int k = 1; k <= 4; k++)
                    layout.Terraces[k - 1].MaxRelief = peaks.MaxHeight(k);
            content = new GameObject("The present, taken apart");
            content.transform.SetParent(transform, false);
            for (int p = 0; p < 5; p++)
            {
                roots[p] = new GameObject(p == 0 ? "Bedrock" : TierName[p]).transform;
                roots[p].SetParent(content.transform, false);
            }

            Bedrock(state);
            Terraces();
            Guides();
            Firms(out firmCount, out firmOutside);
            Labels(instant, data);
            // what moves: the veins' goods and money and the threads' capital, one mesh of packets on the flow clock
            var packets = new PacketMeshBuilder(4096);
            Flows(data, packets);
            Capital(data, packets);
            Packets(packets);
            Key = MakeKey(layout.PerThousand, peaks.NormalMargin, peaks.TableYear);
            Log(instant, firmCount, firmOutside);
            // the new land's parts show at once at the clock (a rebuild mid-flight, a new year in the view)
            labelsShown = false;
            Apply();
        }

        /// <summary>The state's slab under everything: its strata on both faces (the near one lay on the crest's face), the
        /// valley floor on top, the receipts line, and the strata's edges.</summary>
        void Bedrock(InstantState state)
        {
            var faces = new SurfaceMeshBuilder();
            var lines = new LineMeshBuilder();
            float x0 = layout.BedrockX0, x1 = layout.BedrockX1, depth = layout.BedrockDepth, far = PresentLayout.D + PresentLayout.Apron;
            float id = Id(Part.Bedrock), lid = Id(Part.Strata);
            double[] parts = state != null && state.Outlays > 0 ? new[] { state.OwnWork, state.Transfers, state.PurchasesNet, state.Interest, state.Subsidies } : new[] { 1.0 };
            double sum = parts.Sum();
            foreach (float d in new[] { 0f, far })
            {
                float y = 0;
                for (int s = 0; s < parts.Length; s++)
                {
                    float h = sum > 0 ? (float)(Math.Max(0, parts[s]) / sum) * depth : 0;
                    Color c = parts.Length > 1 ? StratumColor[s] : TierColor[0];
                    Quad(faces, id, new Vector3(x0, y, d), new Vector3(x1, y, d), new Vector3(x0, y - h, d), new Vector3(x1, y - h, d), WithAlpha(c, d > 0 ? .5f : .35f));
                    y -= h;
                    if (s < parts.Length - 1) lines.AddPolyline(new[] { new Vector3(x0, y, d), new Vector3(x1, y, d) }, WithAlpha(Color.white, .35f), 1, 0, lid);
                }
            }

            // the valley floor (the slab's top, its apron in front of the terraces), its sides and its bottom
            Quad(faces, id, new Vector3(x0, 0, 0), new Vector3(x1, 0, 0), new Vector3(x0, 0, far), new Vector3(x1, 0, far), WithAlpha(TierColor[0], .12f));
            foreach (float x in new[] { x0, x1 })
                Quad(faces, id, new Vector3(x, 0, 0), new Vector3(x, 0, far), new Vector3(x, -depth, 0), new Vector3(x, -depth, far), WithAlpha(TierColor[0], .16f));
            Quad(faces, id, new Vector3(x0, -depth, 0), new Vector3(x1, -depth, 0), new Vector3(x0, -depth, far), new Vector3(x1, -depth, far), WithAlpha(TierColor[0], .07f));
            // receipts: a white line across both faces where the taxes and other receipts end; below it, the year's borrowing
            if (state != null && state.Outlays > 0 && state.Receipts > 0)
            {
                float r = -(float)Math.Min(1, state.Receipts / state.Outlays) * depth;
                foreach (float d in new[] { 0f, far })
                    lines.AddPolyline(new[] { new Vector3(x0, r, d), new Vector3(x1, r, d) }, WithAlpha(new Color(.95f, .97f, 1f), .9f), 1.8f, 0, lid);
            }

            // the slab's edges: its top and its front face's outline
            lines.AddPolyline(new[] { new Vector3(x0, 0, 0), new Vector3(x0, 0, far), new Vector3(x1, 0, far), new Vector3(x1, 0, 0), new Vector3(x0, 0, 0) }, WithAlpha(EconomyStyle.Government, .5f), 1, 0, lid);
            lines.AddPolyline(new[] { new Vector3(x0, 0, far), new Vector3(x0, -depth, far), new Vector3(x1, -depth, far), new Vector3(x1, 0, far) }, WithAlpha(EconomyStyle.Government, .4f), 1, 0, lid);
            Surface(faces, Part.Bedrock, roots[0], 3184);
            Lines(lines, Part.Strata, roots[0], 3189, false);
        }

        /// <summary>The four terraces (one slab each: its strata on both faces, the plateau and its walls) and their hills (one
        /// relief mesh each, scaled from flat by the rise).</summary>
        void Terraces()
        {
            Material slabs = SurfaceMaterial(3186), hills = SurfaceMaterial(3187), solid = SurfaceMaterial(3185);
            // the terrain is solid: it writes depth, before the slabs, so a peak hides what stands behind it
            solid.SetFloat("_ZWrite", 1);
            for (int k = 1; k <= 4; k++)
            {
                PresentLayout.Terrace t = layout.Terraces[k - 1];
                var b = new SurfaceMeshBuilder();
                float id = Id(Part.Terraces);
                int n = Mathf.Clamp(Mathf.CeilToInt(t.X1 - t.X0), 2, 400);
                var low = new List<Vector3>(n + 1);
                var high = new List<Vector3>(n + 1);
                // the faces: each stratum the column carries, as on the crest's cut face
                foreach (float d in new[] { 0f, PresentLayout.D })
                    for (int j = 1; j <= k; j++)
                    {
                        low.Clear();
                        high.Clear();
                        for (int s = 0; s <= n; s++)
                        {
                            float x = Mathf.Lerp(t.X0, t.X1, s / (float)n);
                            low.Add(new Vector3(x, layout.Profile(j - 1, x), d));
                            high.Add(new Vector3(x, layout.Profile(j, x), d));
                        }

                        b.AddBand(low, high, new[] { (Color32)WithAlpha(SlabTone(j), d > 0 ? .09f : .07f) }, id);
                    }

                // the plateau (and its riser) along the depth, the walls at both ends
                low.Clear();
                high.Clear();
                for (int s = 0; s <= n; s++)
                {
                    float x = Mathf.Lerp(t.X0, t.X1, s / (float)n), y = layout.Profile(k, x);
                    low.Add(new Vector3(x, y, 0));
                    high.Add(new Vector3(x, y, PresentLayout.D));
                }

                b.AddBand(low, high, new[] { (Color32)WithAlpha(Color.Lerp(SlabTone(k), Color.white, .15f), .1f) }, id);
                // normal profit: a glass plane over the band at the normal margin's height; what rises through it is advantage
                if (peaks != null && peaks.Peaks.Count > 0)
                {
                    float y = t.Top + PresentPeaks.Height(peaks.NormalMargin) / PresentLayout.S, d0 = 6, d1 = PresentLayout.D - 6;
                    Quad(b, id, new Vector3(t.Band0, y, d0), new Vector3(t.Band1, y, d0), new Vector3(t.Band0, y, d1), new Vector3(t.Band1, y, d1), WithAlpha(Glass, .05f));
                }
                foreach (float x in new[] { t.X0, t.X1 })
                {
                    float y = layout.Profile(k, x);
                    if (y > .05f) Quad(b, id, new Vector3(x, 0, 0), new Vector3(x, 0, PresentLayout.D), new Vector3(x, y, 0), new Vector3(x, y, PresentLayout.D), WithAlpha(SlabTone(k) * .7f, .12f));
                }

                AddRenderer(b.ToMesh("Present " + TierName[k]), slabs, Part.Terraces, roots[k], "Present " + TierName[k]);
                if (t.MaxRelief <= 0) continue;
                hillRoots[k] = new GameObject("Hills " + TierName[k]).transform;
                hillRoots[k].SetParent(roots[k], false);
                hillRoots[k].localPosition = new Vector3(0, t.Top, 0);
                bool terrain = peaks != null && peaks.Peaks.Count > 0;
                AddRenderer((terrain ? Terrain(k) : Relief(k)).ToMesh("Present hills " + TierName[k]), terrain ? solid : hills, Part.Hills, hillRoots[k], "Present hills " + TierName[k]);
            }
        }

        /// <summary>Terrace k's hills over its band, crest units above its plateau: a grid along x and d, shaded by a light from
        /// the upper left (the surface shader has no light of its own), transparent where the ground is flat.</summary>
        SurfaceMeshBuilder Relief(int k)
        {
            PresentLayout.Terrace t = layout.Terraces[k - 1];
            int nx = Mathf.Clamp(Mathf.CeilToInt(t.Band1 - t.Band0), 8, MaxColumns), nd = Rows;
            var b = new SurfaceMeshBuilder();
            float id = Id(Part.Hills), e = .25f;
            Color dark = TierColor[k] * .45f, bright = Color.Lerp(TierColor[k], Color.white, .35f);
            var rows = new Vector3[2][];
            var tints = new Color32[2][];
            for (int r = 0; r < 2; r++)
            {
                rows[r] = new Vector3[nx + 1];
                tints[r] = new Color32[nx + 1];
            }

            for (int j = 0; j <= nd; j++)
            {
                Vector3[] row = rows[j & 1];
                Color32[] tint = tints[j & 1];
                float d = PresentLayout.D * j / nd;
                for (int i = 0; i <= nx; i++)
                {
                    float x = Mathf.Lerp(t.Band0, t.Band1, i / (float)nx), h = layout.Relief(k, x, d);
                    // the slope at full scale: x and y both grow by S, d does not
                    float sx = (layout.Relief(k, x + e, d) - layout.Relief(k, x - e, d)) / (2 * e);
                    float sd = PresentLayout.S * (layout.Relief(k, x, d + e) - layout.Relief(k, x, d - e)) / (2 * e);
                    float lit = Mathf.Clamp01(Vector3.Dot(new Vector3(-sx, 1, -sd).normalized, Light));
                    Color c = Color.Lerp(dark, bright, lit);
                    c.a = .6f * Mathf.Clamp01(h / .6f);
                    row[i] = new Vector3(x, h + .02f, d);
                    tint[i] = c;
                }

                if (j > 0) b.AddBand(rows[(j - 1) & 1], row, tints[(j - 1) & 1], tint, id);
            }

            return b;
        }

        /// <summary>The guides: dashed lines from each part's near corners, where they stand apart, back to the corners of the
        /// cut face on the crest they came from (built where both ends are at the separation's end; shown from then on).</summary>
        void Guides()
        {
            var b = new LineMeshBuilder();
            float id = Id(Part.Guides);
            Color c = new Color(.66f, .72f, .80f, 1);
            for (int p = 0; p < 5; p++)
            {
                Vector3[] corners;
                if (p == 0) corners = new[] { new Vector3(layout.BedrockX0, 0, 0), new Vector3(layout.BedrockX1, 0, 0), new Vector3(layout.BedrockX0, -layout.BedrockDepth, 0), new Vector3(layout.BedrockX1, -layout.BedrockDepth, 0) };
                else
                {
                    PresentLayout.Terrace t = layout.Terraces[p - 1];
                    corners = new[] { new Vector3(t.X0, 0, 0), new Vector3(t.X1, 0, 0), new Vector3(t.X0, layout.Profile(p, t.X0), 0), new Vector3(t.X1, layout.Profile(p, t.X1), 0) };
                }

                PresentLayout.RootPose from = layout.Root(p, 0), to = layout.Root(p, PresentLayout.Seconds);
                foreach (Vector3 corner in corners)
                    b.AddFlowPath(new[] { new LinePoint(from.Apply(corner), c, 1), new LinePoint(to.Apply(corner), c, 1) }, id);
            }

            MeshRenderer r = Lines(b, Part.Guides, content.transform, 3190, false);
            GraphMaterials.SetDash(r.sharedMaterial, 4, .55f);
        }

        /// <summary>
        /// The named firms' summits: a small ring on every peak that rises above normal profit, brighter and wider the more it
        /// rises (the gold of the terrain says the same; the rings let the eye find the summits and carry the capital
        /// threads). A firm below normal profit gets no ring: its share is ground, not advantage.
        /// </summary>
        void Firms(out int count, out int outside)
        {
            count = outside = 0;
            firmTops.Clear();
            if (peaks == null) return;
            outside = peaks.Outside;
            var b = new LineMeshBuilder();
            float id = Id(Part.Firms);
            foreach (PresentPeaks.Peak p in peaks.Peaks)
            {
                if (!p.Named) continue;
                count++;
                Vector3 top = InTerrace(p.Tier, peaks.Summit(p)) + Vector3.up * .3f;
                firmTops.Add((top, p));
                float excess = (float)(p.Margin - peaks.NormalMargin);
                if (!p.MarginKnown || excess <= 0) continue;
                float t = Mathf.Clamp01(excess / GoldSpan);
                Ring(b, id, top, .8f + 2.2f * t, WithAlpha(Color.Lerp(GoldDeep, GoldBright, t), .5f + .5f * t), 1 + 1.5f * t);
            }

            Lines(b, Part.Firms, content.transform, 3192, true);
        }

        // ------------------------------------------------------------------ the terrain, the purchases and the capital

        /// <summary>How far above the present's highest point the capital canopy's hub floats, and its owners above that
        /// (exploded units).</summary>
        const float CanopyRise = 22, OwnersRise = 40;

        /// <summary>The firms whose net income gets its own capital thread (the most profitable).</summary>
        const int CapitalFirms = 10;

        /// <summary>Land units a second every packet of the view travels: about 30 px a second at the view's pose, as the
        /// politics view's.</summary>
        const float PacketSpeed = 14;

        /// <summary>
        /// A lane's packets a second: <see cref="MinRate"/> at <see cref="RateFloor"/> ($B a year) and below, and
        /// <see cref="RatePerDecade"/> more for every tenfold of its dollars, never past <see cref="MaxRate"/> (faster lanes
        /// strobe at 30 frames a second). A flow's size is its band's width; the rate only grows gently with it. A lane
        /// rebuilt keeps its rate while the new one stands within <see cref="RateHold"/>.
        /// </summary>
        const float MinRate = .4f, MaxRate = 4, RatePerDecade = .6f, RateHold = .15f;

        const double RateFloor = 50;

        /// <summary>The largest few veins, drawn brighter, whose money comes back beside their goods, and the pixels each
        /// direction keeps to the right of its travel, so the two pass each other.</summary>
        const int BrightVeins = 6;

        const float TwoWayOffset = 2;

        /// <summary>A vein's packets' alpha over its band's: faint veins carry faint packets, so the terraces read first.</summary>
        const float VeinPacketAlpha = 1.4f;

        /// <summary>A point on terrace k's ground at the choreography's end (land-local): the terrain where it stands, else the
        /// plateau (or the cut face's profile off the band), lifted by <paramref name="lift"/>.</summary>
        Vector3 OnGround(int tier, float x, float d, float lift = 0)
        {
            if (peaks == null || peaks.Peaks.Count == 0) return layout.OnTerrace(tier, x, d, lift);
            PresentLayout.Terrace t = layout.Terraces[tier - 1];
            float y = x >= t.Band0 && x <= t.Band1 ? t.Top + peaks.HeightAt(tier, x, d) : layout.Profile(tier, x);
            return layout.Root(tier, PresentLayout.Seconds).Apply(new Vector3(x, y, d)) + Vector3.up * lift;
        }

        /// <summary>A point in terrace k's frame (crest x, y above its plateau, exploded d) at the choreography's end.</summary>
        Vector3 InTerrace(int tier, Vector3 local) =>
            layout.Root(tier, PresentLayout.Seconds).Apply(local + Vector3.up * layout.Terraces[tier - 1].Top);

        /// <summary>
        /// An industry's earth tone, by tier: raw materials the matter red, manufacturing bronze, services ochre, tech olive,
        /// the state slate; gold is kept for advantage. Sellers in one tier alternate a little in lightness.
        /// </summary>
        static Color InputColor(int tier, int seller)
        {
            Color c = InputTone[Mathf.Clamp(tier, 0, 4)];
            return Color.Lerp(c, Color.white, (seller * 7 % 3) * .08f);
        }

        static readonly Color[] InputTone =
        {
            new Color(.48f, .52f, .60f), new Color(.80f, .16f, .10f), new Color(.52f, .33f, .20f), new Color(.66f, .50f, .30f), new Color(.45f, .52f, .30f)
        };

        /// <summary>
        /// Terrace k's terrain (<see cref="PresentPeaks"/>) over its band, crest units above its plateau: a grid of
        /// <see cref="Cell"/> exploded units, heights first, then each vertex shaded by the view's light from the grid's own
        /// slopes and coloured by the margin its height stands for: below normal profit the tier's earth, darker toward the
        /// ground; above it gold, brighter and glowing (bloom) the further it rises; a faint contour every
        /// <see cref="Contour"/> of margin, and a pale line where the terrain crosses normal profit.
        /// </summary>
        SurfaceMeshBuilder Terrain(int k)
        {
            PresentLayout.Terrace t = layout.Terraces[k - 1];
            const float d0 = 10, d1 = PresentLayout.D - 10;
            int nx = Mathf.Clamp(Mathf.CeilToInt((t.Band1 - t.Band0) * PresentLayout.S / Cell), 4, 400), nd = Mathf.CeilToInt((d1 - d0) / Cell);
            var h = new float[nx + 1, nd + 1];
            for (int i = 0; i <= nx; i++)
            for (int j = 0; j <= nd; j++)
                h[i, j] = peaks.Exploded(k, Mathf.Lerp(t.Band0, t.Band1, i / (float)nx), Mathf.Lerp(d0, d1, j / (float)nd));

            float dx = (t.Band1 - t.Band0) * PresentLayout.S / nx, dd = (d1 - d0) / nd, normal = (float)peaks.NormalMargin;
            Color earth = InputTone[k];
            var b = new SurfaceMeshBuilder();
            float id = Id(Part.Hills);
            var pos = new Vector3[2][];
            var col = new Color32[2][];
            var glow = new float[2][];
            for (int r = 0; r < 2; r++)
            {
                pos[r] = new Vector3[nx + 1];
                col[r] = new Color32[nx + 1];
                glow[r] = new float[nx + 1];
            }

            for (int j = 0; j <= nd; j++)
            {
                int row = j & 1;
                float d = Mathf.Lerp(d0, d1, j / (float)nd);
                for (int i = 0; i <= nx; i++)
                {
                    float y = h[i, j];
                    float sx = (h[Mathf.Min(i + 1, nx), j] - h[Mathf.Max(i - 1, 0), j]) / (dx * (Mathf.Min(i + 1, nx) - Mathf.Max(i - 1, 0)));
                    float sd = (h[i, Mathf.Min(j + 1, nd)] - h[i, Mathf.Max(j - 1, 0)]) / (dd * (Mathf.Min(j + 1, nd) - Mathf.Max(j - 1, 0)));
                    float lit = .38f + .62f * Mathf.Clamp01(Vector3.Dot(new Vector3(-sx, 1, -sd).normalized, Light));
                    float m = PresentPeaks.MarginOf(y), intensity = 1;
                    Color c;
                    if (m <= normal) c = Color.Lerp(earth * .35f, earth * 1.05f, Mathf.Sqrt(Mathf.Clamp01(m / normal)));
                    else
                    {
                        float g = Mathf.Clamp01((m - normal) / GoldSpan);
                        c = Color.Lerp(GoldDeep, GoldBright, g);
                        intensity = 1 + 1.6f * g * g;
                    }

                    // contours (where the ground rises; flat valleys carry none) and normal profit's line
                    float slope = Mathf.Sqrt(sx * sx + sd * sd), f = m / Contour - Mathf.Floor(m / Contour);
                    if (m > .012f && slope > .05f && slope < 2.5f && (f < .09f || f > .97f)) c *= .7f;
                    if (Mathf.Abs(m - normal) < .0045f && slope > .05f && slope < 2.5f)
                    {
                        c = Color.Lerp(c, Glass, .7f);
                        intensity = Mathf.Max(intensity, 1.4f);
                    }

                    c *= lit;
                    c.a = 1;
                    pos[row][i] = new Vector3(Mathf.Lerp(t.Band0, t.Band1, i / (float)nx), y / PresentLayout.S + .02f, d);
                    col[row][i] = c;
                    glow[row][i] = intensity;
                }

                if (j > 0) b.AddBand(pos[row ^ 1], pos[row], col[row ^ 1], col[row], glow[row ^ 1], glow[row], id);
            }

            return b;
        }

        /// <summary>
        /// The industries' interdependence (<see cref="PresentPeaks.Flows"/>, the largest purchases between hills): each a
        /// vein from the seller's largest firm, hugging the ground on one terrace or hanging like a root between two,
        /// that branches at the buyer into its largest firms (each branch as wide as the firm's share of the purchase, by its
        /// revenue). Veins taper from seller to buyer and grow more opaque as they arrive; the largest few are brighter and
        /// the three largest are labelled. Earth tones by the seller's tier, so they read as the ground's, not advantage's.
        /// Every vein carries its goods, and the largest few the money paid back (<see cref="Goods"/>): the packets are the
        /// veins' motion, so their bands no longer pulse.
        /// </summary>
        void Flows(Data.EconomyData data, PacketMeshBuilder packets)
        {
            if (peaks == null || peaks.Flows.Count == 0) return;
            var b = new LineMeshBuilder();
            float id = Id(Part.Flows);
            double max = peaks.Flows[0].Value;
            int drawn = 0;
            foreach (PresentPeaks.Flow f in peaks.Flows)
            {
                PresentPeaks.Field seller = peaks.Of(f.Seller), buyer = peaks.Of(f.Buyer);
                if (seller == null || buyer == null || seller.Peaks.Count == 0 || buyer.Peaks.Count == 0) continue;
                float v = Mathf.Sqrt((float)(f.Value / max)), width = .5f + 2.4f * v, alpha = .16f + .4f * v;
                bool bright = drawn < BrightVeins;
                Color c = Color.Lerp(InputColor(f.SellerTier, f.Seller), Color.white, .25f);
                PresentPeaks.Peak source = seller.Peaks[0];
                // the buyer's largest firms (its rest when it names none), the branches' ends on their near flanks
                var ends = buyer.Peaks.Where(p => p.Named).Take(3).ToList();
                if (ends.Count == 0) ends = buyer.Peaks.Take(2).ToList();
                double revenue = ends.Sum(p => Math.Max(p.Revenue, p.Share));
                Vector3 from = OnGround(seller.Tier, source.X, source.D + source.Rd * .5f, .4f);
                Vector3 meet = Vector3.zero;
                var targets = new List<Vector3>();
                foreach (PresentPeaks.Peak p in ends)
                {
                    Vector3 at = OnGround(buyer.Tier, p.X, p.D - p.Rd * .55f, .35f);
                    targets.Add(at);
                    meet += at;
                }

                meet = Vector3.Lerp(meet / targets.Count, from, .3f);
                meet.y = Mathf.Max(meet.y, GroundUnder(buyer.Tier, meet) + .5f);
                float seed = drawn * 1.37f, intensity = bright ? 1.3f : 1;
                // the goods ride the trunk from the seller's firm to the branching, then each branch to its firm (its share of
                // the purchase); a lane's key is its flow's ends, so a rebuild keeps its rate and seed
                string key = f.Seller + ">" + f.Buyer;
                Vector3[] trunk = Vein(b, id, from, meet, seller.Tier == buyer.Tier ? buyer.Tier : 0, c, width, width * .7f, alpha * .45f, alpha * .8f, seed, intensity);
                Goods(packets, id, trunk, key, f.Value, v, alpha, bright);
                for (int e = 0; e < targets.Count; e++)
                {
                    float share = (float)(Math.Max(ends[e].Revenue, ends[e].Share) / Math.Max(1e-9, revenue));
                    float w0 = Mathf.Max(.4f, width * .7f * Mathf.Sqrt(share));
                    Vector3[] branch = Vein(b, id, meet, targets[e], buyer.Tier, c, w0, w0 * .4f, alpha * .8f, alpha, seed + e * 2.1f, intensity);
                    Goods(packets, id, branch, key + ">" + (ends[e].Name ?? "#" + e), f.Value * share, v * Mathf.Sqrt(share), alpha, bright);
                }

                if (drawn < 3)
                    labels.Add(new HillLabel
                    {
                        Text = data.Industries[f.Seller].Name + " → " + data.Industries[f.Buyer].Name + " · " + LandFacts.Money(f.Value, 0) + " a year",
                        At = Vector3.Lerp(from, meet, .5f) + Vector3.up * 3, Role = LabelRole.Note, Part = "present.flows", Rank = (float)f.Value
                    });
                drawn++;
            }

            Lines(b, Part.Flows, content.transform, 3193, true);
        }

        /// <summary>
        /// One vein from <paramref name="a"/> to <paramref name="b"/>: on one terrace (<paramref name="tier"/> &gt; 0) it
        /// follows the ground a little above it; between terraces (0) it sags like a hanging root. Either way it meanders
        /// (a lateral sway that vanishes at both ends), tapers from <paramref name="w0"/> to <paramref name="w1"/> and fades in
        /// from <paramref name="a0"/> to <paramref name="a1"/>. Returns its points, the path its packets take.
        /// </summary>
        Vector3[] Vein(LineMeshBuilder lines, float id, Vector3 a, Vector3 b, int tier, Color c, float w0, float w1, float a0, float a1, float seed, float intensity)
        {
            const int n = 28;
            Vector3 span = b - a, side = Vector3.Cross(Vector3.up, span).normalized;
            float distance = span.magnitude, sway = Mathf.Min(5, distance * .06f), sag = 8 + .1f * distance;
            var pts = new List<LinePoint>(n + 1);
            var path = new Vector3[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n;
                Vector3 p = Vector3.Lerp(a, b, t) + side * (sway * Mathf.Sin(Mathf.PI * t) * Mathf.Sin(2.6f * Mathf.PI * t + seed));
                if (tier > 0) p.y = Mathf.Max(p.y, GroundUnder(tier, p) + .45f);
                else p.y -= sag * 4 * t * (1 - t);
                path[i] = p;
                pts.Add(new LinePoint(p, WithAlpha(c, Mathf.Lerp(a0, a1, t)), Mathf.Lerp(w0, w1, t), 0, intensity));
            }

            lines.AddPolyline(pts, id);
            return path;
        }

        // ------------------------------------------------------------------ what moves: goods, money and capital

        /// <summary>
        /// The packets on one path of a vein: the goods bought, squares from the seller toward the buyer at a rate that grows
        /// gently with the path's dollars (<see cref="Rate"/>), as faint as the vein (<see cref="VeinPacketAlpha"/>) and
        /// 2.5 to 3.5 px; on the largest few (<paramref name="twoWay"/>) the money paid for them comes back as dots at the same
        /// rate, each direction keeping to the right of its travel so the two pass each other. Both in commerce's role (money
        /// and goods between businesses), with the vein's highlight id. A purchase not measured draws none.
        /// </summary>
        void Goods(PacketMeshBuilder packets, float id, Vector3[] path, string key, double dollars, float v, float alpha, bool twoWay)
        {
            float rate = Rate(key, dollars), seed = Organism.Hash01(key), size = 2.5f + Mathf.Clamp01(v);
            Color c = WithAlpha(EconomyStyle.Roles.Commerce, Mathf.Clamp01(VeinPacketAlpha * alpha));
            float offset = twoWay ? TwoWayOffset : 0;
            packets.AddLane(path, PacketKind.Goods, c, rate, PacketSpeed, size, id, offsetPx: offset, seed: seed);
            if (twoWay) packets.AddLane(path, PacketKind.Money, c, rate, PacketSpeed, size, id, offsetPx: offset, seed: (seed + .5f) % 1f, backward: true);
        }

        /// <summary>Capital rising along a thread (<see cref="Thread"/>): gold ingots in the owners' role from its first point to
        /// its last, at a rate that grows gently with its dollars (<see cref="Rate"/>), with the thread's highlight id.</summary>
        void Rising(PacketMeshBuilder packets, float id, Vector3[] path, string key, double dollars, float alpha, float size) =>
            packets.AddLane(path, PacketKind.Capital, WithAlpha(EconomyStyle.Roles.Owners, alpha), Rate(key, dollars), PacketSpeed, size, id, seed: Organism.Hash01(key));

        /// <summary>
        /// The packets a second of lane <paramref name="key"/>, which carries <paramref name="dollars"/> ($B a year): the view's
        /// one gentle curve over its dollars (<see cref="MinRate"/> at <see cref="RateFloor"/> and below,
        /// <see cref="RatePerDecade"/> more for every tenfold, at most <see cref="MaxRate"/>), or the rate the lane had when last
        /// built while the new one stands within <see cref="RateHold"/> of it, so a year's rebuild does not jump its pattern.
        /// NaN, which draws no packets, when the amount is not measured (NaN) or not positive.
        /// </summary>
        float Rate(string key, double dollars)
        {
            if (!(dollars > 0) || double.IsInfinity(dollars)) return float.NaN;
            float rate = Mathf.Clamp(MinRate + RatePerDecade * (float)Math.Log10(dollars / RateFloor), MinRate, MaxRate);
            if (laneRates.TryGetValue(key, out float kept) && Mathf.Abs(kept - rate) < RateHold) return kept;
            laneRates[key] = rate;
            return rate;
        }

        /// <summary>The view's packets in one renderer over the content, at the parts' end poses as the veins and threads are;
        /// shown as the flows and the capital arrive (<see cref="Apply"/>) and moved by the shader on the flow clock alone.</summary>
        void Packets(PacketMeshBuilder packets)
        {
            packetLanes = packets.LaneCount;
            if (packetLanes == 0) return;
            Material m = GraphMaterials.Raw(GraphMaterials.Packets(Color.white, 1, 3195));
            owned.Add(m);
            packetRenderer = AddRenderer(packets.ToMesh("Present packets"), m, Part.Flows, content.transform, "Present packets");
        }

        /// <summary>The ground's height (land-local y) under a land-local point on terrace k at the choreography's end.</summary>
        float GroundUnder(int tier, Vector3 world)
        {
            PresentLayout.RootPose pose = layout.Root(tier, PresentLayout.Seconds);
            float x = (world.x - pose.Position.x) / pose.Scale.x, d = (world.z - pose.Position.z) / pose.Scale.z;
            return OnGround(tier, x, d).y;
        }

        /// <summary>
        /// Capital's connections: each terrace's operating surplus (its gold crowns) rises in one thread to a hub over the
        /// present, every stored firm's ring in a faint one, and from the hub the claims fan out to who holds corporate stock
        /// (the wealth groups' shares, Fed DFA, for the year; abroad, Fed Z.1), each owner a gold disc as large as its share.
        /// Capital rises along every thread as gold ingots (<see cref="Rising"/>): the surplus's, the firm's net income, the
        /// owner's share of the surplus; the ingots are the threads' motion, so the threads no longer pulse.
        /// </summary>
        void Capital(Data.EconomyData data, PacketMeshBuilder packets)
        {
            if (peaks == null || peaks.Peaks.Count == 0) return;
            float top = 0, x0 = float.MaxValue, x1 = float.MinValue;
            for (int k = 1; k <= 4; k++)
            {
                PresentLayout.Terrace t = layout.Terraces[k - 1];
                PresentLayout.RootPose pose = layout.Root(k, PresentLayout.Seconds);
                top = Mathf.Max(top, pose.Apply(new Vector3(0, t.Top + t.MaxRelief, 0)).y);
                x0 = Mathf.Min(x0, pose.Apply(new Vector3(t.X0, 0, 0)).x);
                x1 = Mathf.Max(x1, pose.Apply(new Vector3(t.X1, 0, 0)).x);
            }

            float z = layout.NearZ + PresentLayout.D * .5f;
            Vector3 hub = new Vector3((x0 + x1) * .5f, top + CanopyRise, z);
            var lines = new LineMeshBuilder();
            var discs = new SurfaceMeshBuilder();
            float id = Id(Part.Capital);
            Color gold = Surplus, pale = new Color(1, .93f, .78f);
            double surplus = peaks.Surplus.Sum();

            // each terrace's operating surplus in one faint thread from its band's middle (the whole tier's, firms named or not)
            for (int k = 1; k <= 4; k++)
            {
                PresentLayout.Terrace t = layout.Terraces[k - 1];
                if (peaks.Surplus[k - 1] <= 0) continue;
                Vector3 from = OnGround(k, (t.Band0 + t.Band1) * .5f, PresentLayout.D * .5f, 1);
                float share = surplus > 0 ? (float)(peaks.Surplus[k - 1] / surplus) : 0, s = Mathf.Sqrt(share);
                Vector3[] path = Thread(lines, id, from, hub, WithAlpha(gold, .22f), .6f + 2.4f * s);
                Rising(packets, id, path, "surplus " + k, peaks.Surplus[k - 1], .6f + .3f * s, 3 + .6f * s);
            }

            // the most profitable firms, each from its summit, as wide as its net income: advantage paid out to owners
            var earners = firmTops.Where(f => f.peak.MarginKnown && f.peak.NetIncome > 0).OrderByDescending(f => f.peak.NetIncome).Take(CapitalFirms).ToList();
            double most = earners.Count > 0 ? earners[0].peak.NetIncome : 1;
            for (int i = 0; i < earners.Count; i++)
            {
                PresentPeaks.Peak firm = earners[i].peak;
                float v = Mathf.Sqrt((float)(firm.NetIncome / most));
                Vector3[] path = Thread(lines, id, earners[i].at, hub, WithAlpha(Color.Lerp(gold, pale, .3f), .1f + .32f * v), .4f + 2.2f * v);
                Rising(packets, id, path, "firm " + firm.Industry + " " + firm.Name, firm.NetIncome, .35f + .45f * v, 2.5f + .8f * v);
            }

            // the owners: the year's shares of household-held corporate stock, the rest of the world's share taken first
            var circuit = data.Circuit;
            double abroad = Math.Max(0, Math.Min(1, circuit?.ForeignEquityShare ?? 0));
            var shares = new double[Owners.Length];
            for (int o = 0; o < Owners.Length; o++)
            {
                if (Owners[o].id == "abroad") shares[o] = abroad;
                else
                {
                    double v = Data.EconomyData.Keyed(circuit?.GroupEquityHistory, Owners[o].id).At(layout.Year);
                    shares[o] = double.IsNaN(v) ? 0 : Math.Max(0, v) * (1 - abroad);
                }
            }

            float spread = Mathf.Max(60, (x1 - x0) * .42f);
            for (int o = 0; o < Owners.Length; o++)
            {
                if (shares[o] <= 0) continue;
                Vector3 at = new Vector3(hub.x + spread * (o / (float)(Owners.Length - 1) - .5f), top + OwnersRise, z + 30);
                float radius = 1.5f + 11 * Mathf.Sqrt((float)shares[o]);
                Vector3[] path = Thread(lines, id, hub, at, WithAlpha(gold, .65f), 1 + 6 * (float)shares[o]);
                // the owner's share of the surplus the hub gathers, as the thread's width draws it
                Rising(packets, id, path, "owner " + Owners[o].id, shares[o] * surplus, .85f, 3 + 2 * (float)shares[o]);
                Ring(lines, id, at, radius, gold, 2);
                Ring(lines, id, at + Vector3.up * .05f, radius * 1.15f, gold, 1.1f);
                Disc(discs, id, at, radius, WithAlpha(gold, .28f));
                labels.Add(new HillLabel
                {
                    Text = "<b>" + Owners[o].name + "</b> · " + (100 * shares[o]).ToString("0", LandFacts.Ci) + "% of corporate stock",
                    At = at + Vector3.up * 4, Role = LabelRole.Name, Part = "present.capital", Rank = 20 + (float)shares[o]
                });
            }

            Ring(lines, id, hub, 3, pale, 2);
            Disc(discs, id, hub, 3, WithAlpha(gold, .5f));
            labels.Add(new HillLabel
            {
                Text = "operating surplus " + LandFacts.Money(surplus, 1) + " a year, and the " + CapitalFirms + " most profitable firms' net income, owned by", At = hub + new Vector3(6, 2, 0),
                Role = LabelRole.Note, Part = "present.capital", Rank = 19
            });
            Lines(lines, Part.Capital, content.transform, 3194, true);
            Surface(discs, Part.Capital, content.transform, 3188);
        }

        /// <summary>A thread of capital: a gentle curve from <paramref name="from"/> up to <paramref name="to"/>. Returns its
        /// points, the path its ingots rise along.</summary>
        static Vector3[] Thread(LineMeshBuilder b, float id, Vector3 from, Vector3 to, Color c, float width)
        {
            Vector3 control = new Vector3(from.x, Mathf.Lerp(from.y, to.y, .75f), Mathf.Lerp(from.z, to.z, .5f));
            var pts = new List<LinePoint>(21);
            var path = new Vector3[21];
            for (int i = 0; i <= 20; i++)
            {
                float t = i / 20f;
                Vector3 p = (1 - t) * (1 - t) * from + 2 * (1 - t) * t * control + t * t * to;
                path[i] = p;
                pts.Add(new LinePoint(p, c, width, 0, 1.2f));
            }

            b.AddPolyline(pts, id);
            return path;
        }

        static void Disc(SurfaceMeshBuilder b, float id, Vector3 at, float radius, Color c)
        {
            for (int i = 0; i < 24; i++)
            {
                float a0 = i * Mathf.PI / 12, a1 = (i + 1) * Mathf.PI / 12;
                b.AddTriangle(at, at + new Vector3(Mathf.Cos(a0) * radius, 0, Mathf.Sin(a0) * radius), at + new Vector3(Mathf.Cos(a1) * radius, 0, Mathf.Sin(a1) * radius), c, id);
            }
        }

        /// <summary>The view's labels, at the parts' places at the choreography's end (the label system lays them out): the title,
        /// each tier's value added and jobs on its viewer-facing face, the state's account on its slab, the largest firms and
        /// each terrace's largest industries.</summary>
        void Labels(InstantYear instant, Data.EconomyData data)
        {
            labels.Clear();
            float far = layout.NearZ + PresentLayout.D, top = 0, x0 = float.MaxValue, x1 = float.MinValue;
            for (int k = 1; k <= 4; k++)
            {
                PresentLayout.Terrace t = layout.Terraces[k - 1];
                PresentLayout.RootPose pose = layout.Root(k, PresentLayout.Seconds);
                Vector3 face = pose.Apply(new Vector3((t.Band0 + t.Band1) * .5f, t.Top * .5f, PresentLayout.D));
                top = Mathf.Max(top, pose.Apply(new Vector3(0, t.Top + t.MaxRelief, 0)).y);
                x0 = Mathf.Min(x0, pose.Apply(new Vector3(t.X0, 0, 0)).x);
                x1 = Mathf.Max(x1, pose.Apply(new Vector3(t.X1, 0, 0)).x);
                InstantTier tier = instant != null && instant.Tiers.Length >= k ? instant.Tiers[k - 1] : null;
                string text = "<b>" + TierName[k] + "</b> · " + LandFacts.Money(tier?.ValueAdded ?? t.ValueAdded, 1) + (tier != null ? " · " + tier.Jobs.ToString("0.0", LandFacts.Ci) + "M jobs" : "");
                labels.Add(new HillLabel { Text = text, At = face, Role = LabelRole.Name, Part = "present.terraces", Rank = k });
                // the terrace's largest industries, on their summits
                foreach (PresentLayout.Hill h in t.Hills.Where(h => !h.Flat).OrderByDescending(h => h.ValueAdded).Take(IndustryLabels))
                    labels.Add(new HillLabel { Text = data.Industries[h.Industry].Name, At = OnGround(k, h.Center, PresentLayout.D * .5f, 3), Role = LabelRole.Note, Part = "present.hills", Rank = (float)h.ValueAdded });
            }

            InstantState s = instant?.State;
            PresentLayout.RootPose bed = layout.Root(0, PresentLayout.Seconds);
            Vector3 slab = bed.Apply(new Vector3((layout.BedrockX0 + layout.BedrockX1) * .5f, -layout.BedrockDepth * .5f, PresentLayout.D + PresentLayout.Apron));
            string state = "<b>THE STATE</b> · all levels of government" + (s != null && s.Outlays > 0
                ? " · spends " + LandFacts.Money(s.Outlays, 1) + " · takes in " + LandFacts.Money(s.Receipts, 1) + (s.Borrowing > 0 ? " · borrows " + LandFacts.Money(s.Borrowing, 1) : "")
                : "");
            labels.Add(new HillLabel { Text = state, At = slab, Role = LabelRole.Name, Part = "present.bedrock", Rank = 5 });
            labels.Add(new HillLabel
            {
                Text = "<b>The present, taken apart</b> · " + layout.Year, At = new Vector3((x0 + x1) * .5f, top + CanopyRise + 46, layout.NearZ + PresentLayout.D * .5f),
                Role = LabelRole.Title, Part = "present.terraces", Rank = 10
            });
            labels.Add(new HillLabel
            {
                Text = "taken from the crest: " + layout.Year + "'s cut face", At = Vector3.Lerp(new Vector3(layout.BedrockX0, 0, layout.CrestZ), bed.Apply(new Vector3(layout.BedrockX0, 0, 0)), .5f),
                Role = LabelRole.Note, Part = "present.guides"
            });
            if (peaks == null) return;
            // advantage: the largest profits, each with its margin and share; and the largest firms below normal profit,
            // whose share is scale without advantage
            var named = firmTops.Where(f => f.peak.MarginKnown).ToList();
            foreach ((Vector3 at, PresentPeaks.Peak p) in named.OrderByDescending(f => f.peak.NetIncome).Take(FirmLabels))
                labels.Add(new HillLabel { Text = "<b>" + p.Name + "</b> · " + Percent(p.Margin) + " margin · " + Percent(p.Share) + " share", At = at + Vector3.up * 2.5f, Role = LabelRole.Note, Part = "present.firms", Rank = 12 + (float)p.NetIncome / 1000 });
            foreach ((Vector3 at, PresentPeaks.Peak p) in named.Where(f => f.peak.Margin < peaks.NormalMargin).OrderByDescending(f => f.peak.Share * f.peak.Revenue).Take(2))
                labels.Add(new HillLabel { Text = p.Name + " · " + Percent(p.Share) + " share, " + Percent(p.Margin) + " margin: scale, no advantage", At = at + Vector3.up * 2, Role = LabelRole.Note, Part = "present.firms", Rank = 11 });
            // normal profit, on the plane where it meets the largest terrace's near edge
            PresentLayout.Terrace wide = layout.Terraces.OrderByDescending(t => t.Band1 - t.Band0).First();
            Vector3 plane = InTerrace(wide.Tier, new Vector3(wide.Band0 + (wide.Band1 - wide.Band0) * .15f, PresentPeaks.Height(peaks.NormalMargin) / PresentLayout.S, 8));
            labels.Add(new HillLabel { Text = "normal profit · " + Percent(peaks.NormalMargin) + " net margin (Fortune 500): gold above it is advantage", At = plane, Role = LabelRole.Note, Part = "present.hills", Rank = 13 });
        }

        static string Percent(double v) => (100 * v).ToString(Math.Abs(v) < .1 ? "0.#" : "0", LandFacts.Ci) + "%";

        /// <summary>"[Why] present YEAR: ..." with the design's checks A1-A3 (and the firms' A8), each failure an error.</summary>
        void Log(InstantYear instant, int firms, int outside)
        {
            var problems = new List<string>();
            double[] shares = instant != null && instant.Year == layout.Year ? instant.Tiers.Select(t => t != null ? t.OutputShare : double.NaN).ToArray() : null;
            float area = layout.AreaCheck(shares, out int hills, problems);
            float coincidence = layout.Coincidence();
            if (coincidence > MaxCoincidence) problems.Add($"at the clock's start a part stands {coincidence:0.00} off the crest's cut face (at most {MaxCoincidence})");
            var apart = new StringBuilder();
            float gap = layout.Disjoint(apart, problems);
            if (outside > 0) problems.Add($"{outside} firm peaks stand outside their hill's footprint");
            Debug.Log($"[Why] present {layout.Year}: tiers 4, hills {hills}, area max err {area.ToString("0.0", LandFacts.Ci)}% " +
                      $"(1,000 u² = {LandFacts.Money(layout.PerThousand, 2)} a year; tier shares from {(shares != null ? "InstantModel" : "the industries' value added")}); " +
                      $"t0 coincidence max {coincidence.ToString("0.00", LandFacts.Ci)}; apart at t1, smallest gap {gap.ToString("0.0", LandFacts.Ci)} ({apart}); " +
                      $"firms {firms} ({outside} outside their hills); bedrock {(PresentLayout.S * layout.BedrockDepth).ToString("0", LandFacts.Ci)} deep; " +
                      (peaks != null ? $"peaks {peaks.Peaks.Count} on {peaks.Fields.Count} fields ({peaks.Named} named firms, {peaks.Unplaced} without revenue), normal margin {Percent(peaks.NormalMargin)}, " +
                                       $"veins {peaks.Flows.Count} (largest {(peaks.Flows.Count > 0 ? LandFacts.Money(peaks.Flows[0].Value, 0) : "-")}), surplus {LandFacts.Money(peaks.Surplus.Sum(), 1)}, " +
                                       $"packet lanes {packetLanes} at {PacketSpeed.ToString("0", LandFacts.Ci)} u/s" : "no peaks"));
            if (peaks != null && peaks.Named == 0) problems.Add("no named firm stands on the present's hills");
            foreach (string p in problems) Debug.LogError("[Why] present check failed: " + p);
        }

        /// <summary>The view's legend: how to read a peak (footprint share, height margin, volume profit), normal profit's
        /// plane and its gold, the earth tones of the tiers and their veins, the capital threads, what moves along both, and
        /// the bedrock.</summary>
        static HillPresets.ViewKey MakeKey(double perThousand, double normal = 0, int table = 0) => new HillPresets.ViewKey(new (string, Color)[]
            {
                ("Advantage: margin above normal profit", GoldBright), ("Normal profit", Glass), ("Raw materials", InputTone[1]), ("Manufacturing · infrastructure", InputTone[2]),
                ("Services", InputTone[3]), ("Tech", InputTone[4]), ("The state", TierColor[0]), ("Owners of corporate stock", Surplus)
            },
            "Each hill's footprint: its industry's value added" + (perThousand > 0 ? " (1,000 square units = " + LandFacts.Money(perThousand, 2) + " a year)" : "") +
            ". Each peak a firm: its footprint its share of the industry (revenue over the industry's output" + (table > 0 ? ", the " + table + " BEA input-output table scaled to the year" : "") +
            "; worldwide revenue, so where the named firms pass 70% the industry is widened to fit), its height its net margin, so its volume is its profit. The small peaks: the rest of the industry at its typical margin (an illustrative spread)." +
            (normal > 0 ? " The glass plane: normal profit, the Fortune 500's " + (100 * normal).ToString("0", LandFacts.Ci) + "% net margin; gold above it is advantage, brighter the higher it rises." : "") +
            " Veins: the largest purchases between industries, branching into the buyer's largest firms. Gold threads: surplus and the most profitable firms' net income to who holds corporate stock (Fed DFA; abroad, Fed Z.1)." +
            " The marks moving along them: squares, the goods bought; dots, the money paid back on the largest veins; ingots, profit rising to its owners." +
            " The bedrock: what all levels of government spend; the white line, what they take in.");

        // ------------------------------------------------------------------ the smoke sequence

        /// <summary>
        /// <c>-smokeextra present</c>: the view at the choreography's start, middle and end, held (economy-present-t000, -t050,
        /// -t100), with the checks logged again (A1-A3); the renderers it keeps active (at most <see cref="MaxRenderers"/>, A6);
        /// then the choreography played forward and back with its camera, and its longest frame (A5, a warning past 50 ms).
        /// </summary>
        IEnumerator Smoke()
        {
            GraphRoot root = GraphRoot.Instance;
            if (root == null) yield break;
            root.Focus(View, 0f);
            for (float waited = 0; waited < 10 && (content == null || land != HillLandscapeLayer.Current); waited += Time.unscaledDeltaTime) yield return null;
            if (content == null)
            {
                Debug.LogError("[Why] present: the layer built nothing in 10 s");
                yield break;
            }

            yield return new WaitForSecondsRealtime(1.5f);
            Log(model?.Year(layout.Year), firmCount, firmOutside);
            foreach (int percent in new[] { 0, 50, 100 })
            {
                Hold(PresentLayout.Seconds * percent / 100f);
                yield return new WaitForSecondsRealtime(.8f);
                yield return SmokeChecks.Capture("economy-present-t" + percent.ToString("000", LandFacts.Ci), root.CurrentPreset);
            }

            int active = 0;
            foreach ((Renderer r, Part _) in renderers) if (r && r.enabled && r.gameObject.activeInHierarchy) active++;
            Debug.Log($"[Why] present: {active} renderers active (at most {MaxRenderers})");
            if (active > MaxRenderers) Debug.LogError($"[Why] present: {active} renderers active, more than {MaxRenderers}");
            float longest = 0;
            Play(true);
            while (direction != 0)
            {
                yield return null;
                longest = Mathf.Max(longest, Time.unscaledDeltaTime);
            }

            Reverse(true);
            while (direction != 0)
            {
                yield return null;
                longest = Mathf.Max(longest, Time.unscaledDeltaTime);
            }

            Debug.Log($"[Why] present choreography max frame {(1000 * longest).ToString("0", LandFacts.Ci)} ms (forward {PresentLayout.Seconds} s, back {PresentLayout.ReverseSeconds} s)");
            if (longest > .05f) Debug.LogWarning($"[Why] present: a choreography frame took {(1000 * longest).ToString("0", LandFacts.Ci)} ms, over 50");
            yield return new WaitForSecondsRealtime(1.5f);
        }

        // ------------------------------------------------------------------ meshes

        static void Quad(SurfaceMeshBuilder b, float id, Vector3 a0, Vector3 a1, Vector3 b0, Vector3 b1, Color color) =>
            b.AddBand(new[] { a0, a1 }, new[] { b0, b1 }, new[] { (Color32)color }, id);

        static void Ring(LineMeshBuilder b, float id, Vector3 at, float radius, Color c, float width)
        {
            var points = new Vector3[25];
            for (int i = 0; i < points.Length; i++)
            {
                float a = i * Mathf.PI / 12;
                points[i] = at + new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius);
            }

            b.AddPolyline(points, c, width, 0, id);
        }

        Material SurfaceMaterial(int queue)
        {
            Material m = GraphMaterials.Raw(GraphMaterials.Surface(Color.white, 1, queue));
            // solid faces: the bands' soft edges would stripe the slabs and the hills' rows
            m.SetFloat("_EdgeSoft", 0);
            owned.Add(m);
            return m;
        }

        void Surface(SurfaceMeshBuilder b, Part part, Transform parent, int queue) =>
            AddRenderer(b.ToMesh("Present " + part), SurfaceMaterial(queue), part, parent, "Present " + part);

        MeshRenderer Lines(LineMeshBuilder b, Part part, Transform parent, int queue, bool additive)
        {
            Material m = GraphMaterials.Raw(GraphMaterials.Line(Color.white, 1, queue, additive));
            owned.Add(m);
            return AddRenderer(b.ToMesh("Present " + part), m, part, parent, "Present " + part);
        }

        MeshRenderer AddRenderer(Mesh mesh, Material material, Part part, Transform parent, string name)
        {
            owned.Add(mesh);
            MeshRenderer r = AddMesh(name, mesh, material);
            r.transform.SetParent(parent, false);
            renderers.Add((r, part));
            return r;
        }

        static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }

        void Clear()
        {
            foreach (UnityEngine.Object o in owned) if (o) Destroy(o);
            if (content) Destroy(content);
            owned.Clear();
            renderers.Clear();
            labels.Clear();
            content = null;
            layout = null;
            peaks = null;
            packetRenderer = null;
            packetLanes = 0;
            firmTops.Clear();
            Array.Clear(roots, 0, roots.Length);
            Array.Clear(hillRoots, 0, hillRoots.Length);
            if (labelsShown) HillLabels.Clear(this);
            labelsShown = false;
        }

        void OnDestroy()
        {
            Clear();
            HillLabels.Clear(this);
        }
    }
}
