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
    /// <item>the 25 stored firms as gold rings on stems on their hills' viewer-facing slopes, sized by market value
    /// (HillActivityLayer's halo);</item>
    /// <item>faint guides from each part's near corners back to the crest face it came from, and labels: the tiers' value
    /// added and jobs, the state's account, the largest firms and industries.</item>
    /// </list>
    /// Entering the view (key I, the bar or the catalog) plays the 6.4 s choreography: face, cut, grow, separate, rise,
    /// arrive. Shift + I plays it backward in 2 s and returns to the overview. Every mesh is built once per land, in one
    /// <see cref="HillRebuilds"/> slot, the first time the view is wanted; the choreography moves root transforms and sets
    /// materials' alpha only (no mesh rebuilds, no allocation per frame). Nothing is built or drawn outside the view.
    /// </summary>
    [GraphScenes(EconomyLayouts.HillsScene)]
    public sealed class HillPresentLayer : GraphLayer
    {
        /// <summary>The view's id (HillPresets), its smoke sequence (<c>-smokeextra present</c>) and its log's tag.</summary>
        public const string View = "present", Sequence = "present";

        public override int Order => 58;

        enum Part { Bedrock, Strata, Terraces, Hills, Guides, Firms, Count }

        /// <summary>Each part's emphasis key, by <see cref="Part"/>: its alpha in every view is the emphasis table's.</summary>
        static readonly int[] Keys = EconomyEmphasis.Keys("present", "bedrock", "strata", "terraces", "hills", "guides", "firms");

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
        public static HillPresets.ViewKey Key { get; private set; } = MakeKey(0);

        InstantModel model;
        LandSnapshot snapshot;
        HillLandscape land;
        PresentLayout layout;
        GameObject content;
        readonly Transform[] roots = new Transform[5];
        readonly Transform[] hillRoots = new Transform[5];
        readonly List<(Renderer renderer, Part part)> renderers = new List<(Renderer, Part)>();
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        readonly List<HillLabel> labels = new List<HillLabel>();
        bool labelsShown;

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
            if (kb == null || root == null || !root.AllowPresetKeys || HudKit.TypingInField() || !kb.iKey.wasPressedThisFrame) return;
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
                if (part == Part.Hills) a *= rise > .01f ? 1 : 0;
                else if (part == Part.Guides) a *= guides;
                else if (part == Part.Firms) a *= reveal;
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
            Key = MakeKey(layout.PerThousand);
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
            Material slabs = SurfaceMaterial(3186), hills = SurfaceMaterial(3187);
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

                        b.AddBand(low, high, new[] { (Color32)WithAlpha(TierColor[j], d > 0 ? .5f : .4f) }, id);
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

                b.AddBand(low, high, new[] { (Color32)WithAlpha(Color.Lerp(TierColor[k], Color.white, .15f), .3f) }, id);
                foreach (float x in new[] { t.X0, t.X1 })
                {
                    float y = layout.Profile(k, x);
                    if (y > .05f) Quad(b, id, new Vector3(x, 0, 0), new Vector3(x, 0, PresentLayout.D), new Vector3(x, y, 0), new Vector3(x, y, PresentLayout.D), WithAlpha(TierColor[k] * .7f, .24f));
                }

                AddRenderer(b.ToMesh("Present " + TierName[k]), slabs, Part.Terraces, roots[k], "Present " + TierName[k]);
                if (t.MaxRelief <= 0) continue;
                hillRoots[k] = new GameObject("Hills " + TierName[k]).transform;
                hillRoots[k].SetParent(roots[k], false);
                hillRoots[k].localPosition = new Vector3(0, t.Top, 0);
                AddRenderer(Relief(k).ToMesh("Present hills " + TierName[k]), hills, Part.Hills, hillRoots[k], "Present hills " + TierName[k]);
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

        /// <summary>The 25 stored firms as gold rings on stems (HillActivityLayer's halo: a ring, an outer ring and an inner
        /// one), on the viewer-facing slope of their hill, largest nearest the summit; the ring's radius grows with the square
        /// root of market value, its width with net income, the stem with the log of market value as the wave's beams.</summary>
        void Firms(out int count, out int outside)
        {
            count = outside = 0;
            TowerGeom[] towers = snapshot?.Land?.Towers;
            if (towers == null || towers.Length == 0) return;
            var b = new LineMeshBuilder();
            float id = Id(Part.Firms);
            Color gold = EconomyStyle.Capital, pale = new Color(1, .95f, .85f);
            foreach (IGrouping<int, TowerGeom> group in towers.GroupBy(t => t.Industry))
            {
                PresentLayout.Hill hill = layout.Hills.FirstOrDefault(h => h.Industry == group.Key);
                if (hill == null) continue;
                float d = PresentLayout.HillCenter * PresentLayout.D, previous = 0;
                int seat = 0;
                foreach (TowerGeom f in group.OrderByDescending(t => t.MarketCap))
                {
                    float r = Radius(f.MarketCap);
                    d = seat == 0 ? d + r * .5f : d + previous + r + 2;
                    d = Mathf.Min(d, PresentLayout.D - 2);
                    float side = seat % 2 == 0 ? 1 : -1, x = hill.Center + side * Mathf.Min(PresentLayout.S * hill.Band * .3f, 5) / PresentLayout.S;
                    Vector3 foot = layout.OnTerrace(hill.Tier, x, d, .05f), top = foot + Vector3.up * Stem(f.MarketCap);
                    float width = 1.6f + Mathf.Min(2, Mathf.Sqrt((float)Math.Max(0, f.NetIncome)) * .12f);
                    b.AddSegment(foot, top, WithAlpha(gold, .8f), 1.4f, 0, id);
                    Ring(b, id, top, r, gold, width);
                    Ring(b, id, top + Vector3.up * .05f, r * 1.18f, gold, 1.2f);
                    Ring(b, id, top, r * .72f, pale, 1.1f);
                    if (Mathf.Abs(x - hill.Center) > hill.Band + 1e-3f || d < 0 || d > PresentLayout.D) outside++;
                    previous = r;
                    seat++;
                    count++;
                }
            }

            Lines(b, Part.Firms, content.transform, 3192, false);
        }

        /// <summary>A firm ring's radius (exploded units) and its stem (HillActivityLayer's beam), by market value ($B).</summary>
        static float Radius(double marketCap) => 1.2f + .08f * Mathf.Sqrt((float)Math.Max(0, marketCap));

        static float Stem(double marketCap) => 2 + Mathf.Log10(1 + (float)Math.Max(0, marketCap)) * 2.4f;

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
                    labels.Add(new HillLabel { Text = data.Industries[h.Industry].Name, At = layout.OnTerrace(k, h.Center, PresentLayout.HillCenter * PresentLayout.D, 3), Role = LabelRole.Note, Part = "present.hills", Rank = (float)h.ValueAdded });
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
                Text = "<b>The present, taken apart</b> · " + layout.Year, At = new Vector3((x0 + x1) * .5f, top + 30, layout.NearZ + PresentLayout.D * .5f),
                Role = LabelRole.Title, Part = "present.terraces", Rank = 10
            });
            labels.Add(new HillLabel
            {
                Text = "taken from the crest: " + layout.Year + "'s cut face", At = Vector3.Lerp(new Vector3(layout.BedrockX0, 0, layout.CrestZ), bed.Apply(new Vector3(layout.BedrockX0, 0, 0)), .5f),
                Role = LabelRole.Note, Part = "present.guides"
            });
            TowerGeom[] towers = snapshot?.Land?.Towers;
            if (towers == null) return;
            foreach (TowerGeom f in towers.OrderByDescending(t => t.MarketCap).Take(FirmLabels))
            {
                PresentLayout.Hill hill = layout.Hills.FirstOrDefault(h => h.Industry == f.Industry);
                if (hill == null) continue;
                // over the hill's summit side, where its firms stand (the rings' own places are Firms')
                Vector3 at = layout.OnTerrace(hill.Tier, hill.Center, PresentLayout.HillCenter * PresentLayout.D + 10, Stem(f.MarketCap) + Radius(f.MarketCap) + 2);
                labels.Add(new HillLabel { Text = f.Name, At = at, Role = LabelRole.Note, Part = "present.firms", Rank = (float)f.MarketCap });
            }
        }

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
            if (outside > 0) problems.Add($"{outside} firm rings stand outside their hill's footprint");
            Debug.Log($"[Why] present {layout.Year}: tiers 4, hills {hills}, area max err {area.ToString("0.0", LandFacts.Ci)}% " +
                      $"(1,000 u² = {LandFacts.Money(layout.PerThousand, 2)} a year; tier shares from {(shares != null ? "InstantModel" : "the industries' value added")}); " +
                      $"t0 coincidence max {coincidence.ToString("0.00", LandFacts.Ci)}; apart at t1, smallest gap {gap.ToString("0.0", LandFacts.Ci)} ({apart}); " +
                      $"firms {firms} ({outside} outside their hills); bedrock {(PresentLayout.S * layout.BedrockDepth).ToString("0", LandFacts.Ci)} deep");
            foreach (string p in problems) Debug.LogError("[Why] present check failed: " + p);
        }

        /// <summary>The view's legend: the tiers' and the state's colours, how to read area and height, and the year's scale.</summary>
        static HillPresets.ViewKey MakeKey(double perThousand) => new HillPresets.ViewKey(new (string, Color)[]
            {
                ("Raw materials", TierColor[1]), ("Manufacturing · infrastructure", TierColor[2]), ("Services", TierColor[3]), ("Tech", TierColor[4]),
                ("The state", TierColor[0]), ("Firms, by market value", EconomyStyle.Capital)
            },
            "Area: value added a year" + (perThousand > 0 ? " (1,000 square units = " + LandFacts.Money(perThousand, 2) + ")" : "") +
            ". Height: an assumed order of dependence, not measured. The bedrock: what all levels of government spend; the white line, what they take in. Rings: the 25 stored firms, larger with market value.");

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
