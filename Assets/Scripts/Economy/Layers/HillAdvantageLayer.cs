using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Why.Economy.Draw;
using Why.Economy.Emphasis;
using Why.Economy.Land;
using Why.Economy.Model;
using Why.UI;
using Roles = Why.Economy.EconomyStyle.Roles;

namespace Why.Economy.Layers
{
    /// <summary>
    /// The present as a contest: the corporate biosphere (design Docs/hills-design/ADVANTAGE.md). The crest of 2026
    /// opens into a three-dimensional, force-directed network (<see cref="ContestNetwork"/>) where
    /// <list type="bullet">
    /// <item>each company is an organism built of its balance sheet: a core of book equity, what it owns above (cash, what
    /// it built, what it bought, its stakes), what it owes hanging below (debt, leases), promises beneath its shell
    /// (commitments), inside a translucent shell as large as its market value, coloured by its P/E: the air between the
    /// shell and the core is the speculative premium;</item>
    /// <item>companies hover at their market position, high ground for the dominant, over a fitness landscape their own
    /// positions raise (drawn as contours); rivers of money run along their dependencies (rose for desire, ice for fear,
    /// white-gold for capital); gold threads run up to the owners' golden circles above, the ownership class: households
    /// by wealth and founders over the funds, sovereigns, private equity and hedge funds; buyers and the states stand on
    /// the low ground beneath;</item>
    /// <item>a world (now, 2030, a scenario) and a year (2026-2030) of the Darwinian simulation retarget every organism:
    /// shells inflate or burst, the extinct sink and fade, the absorbed join their absorber, and the layout keeps moving
    /// until the population settles;</item>
    /// <item>sparks mark the contests; the history camera follows the lineage's gold lane along the wave from 1947.</item>
    /// </list>
    /// Key I enters (the crest opens, the organisms rise); I again replays; Shift + I returns to the overview. Meshes are
    /// rebuilt about 15 times a second while the population moves and not at all at rest.
    /// </summary>
    [GraphScenes(EconomyLayouts.HillsScene)]
    public sealed class HillAdvantageLayer : GraphLayer
    {
        public const string View = "advantage", Sequence = "advantage";

        public override int Order => 59;

        public override IEnumerable<string> RequiredTexts => new[] { Advantage.Path };

        enum Part { Organs, Shells, Rings, Rivers, Threads, Landscape, Sparks, Thread, Outlines, Columns, Walls, Count }

        static readonly string[] PartNames = { "organs", "shells", "rings", "rivers", "threads", "landscape", "sparks", "thread", "outlines", "columns", "walls" };
        static readonly int[] Keys = EconomyEmphasis.Keys("advantage", PartNames);

        public const float Seconds = 5f, ReverseSeconds = 1.8f;
        const int MaxOrganismVertices = 1200000;

        const int MaxRenderers = 12, RiverHighlights = 14, ThreadCompanies = 10, SparkDefault = 7, NameLabels = 22;

        // ------------------------------------------------------------------ colours (sRGB authored; surfaces stored linear)

        /// <summary>The balance sheet's parts.</summary>
        public static readonly Color CoreColor = new Color(.66f, .70f, .76f), CashColor = new Color(.40f, .88f, .56f), BuiltColor = new Color(.45f, .58f, .86f),
            BoughtColor = new Color(.70f, .52f, .94f), StakeColor = new Color(1f, .80f, .36f), DebtColor = new Color(.70f, .14f, .16f),
            LeaseColor = new Color(.82f, .42f, .14f), PromiseColor = new Color(1f, .46f, .30f), BurnColor = new Color(1f, .55f, .18f);

        /// <summary>The shell by P/E: under 15, 15-35, 35-80, over 80 or a loss with a valuation.</summary>
        public static readonly Color PeLow = new Color(.45f, .62f, .95f), PeMid = new Color(.90f, .92f, .96f), PeHigh = new Color(1f, .82f, .45f), PeBubble = new Color(1f, .42f, .78f);

        /// <summary>The triad's colours (sparks): information, execution, environment.</summary>
        public static readonly Color InfoColor = new Color(.68f, .54f, 1f), ExecColor = new Color(1f, .58f, .24f), EnvColor = new Color(.45f, .95f, .55f);

        /// <summary>The frontier's colour: a white heat (the only intense bloom in the view, kept for the three hardest choke points).</summary>
        static readonly Color FrontierColor = new Color(1f, .97f, .86f);

        static readonly Color Gold = EconomyStyle.Capital, GoldBright = new Color(1f, .88f, .50f), Neutral = new Color(.75f, .71f, .67f), CapitalFlow = new Color(.78f, .72f, .58f),
            Contour = new Color(.40f, .62f, .66f), BuyerColor = new Color(.34f, .47f, .72f), StateColor = new Color(.50f, .55f, .62f), ThreadColor = new Color(1f, .92f, .72f),
            Ruler = new Color(.62f, .68f, .76f);

        static readonly Vector3 Light = new Vector3(-.4f, .78f, -.48f).normalized;

        static readonly bool SmokeRun = Environment.GetCommandLineArgs().Contains("-smoketest");

        /// <summary>A move is instant in a smoke run's catalog (its frames capture each world at once); the view's own sequence
        /// turns the live settling back on to time it.</summary>
        static bool instantMoves = SmokeRun;

        // ------------------------------------------------------------------ the state the panel reads

        static HillAdvantageLayer current;

        /// <summary>The world on screen (<see cref="Advantage.Now"/>, <see cref="Advantage.Y2030"/> or a scenario id).</summary>
        public static string Scenario { get; private set; } = Advantage.Now;

        /// <summary>The simulated year on screen (2026-2030).</summary>
        public static int Year { get; private set; } = 2026;

        /// <summary>Plays the years forward, one every <see cref="YearSeconds"/>.</summary>
        public static bool Playing { get; set; }

        public const float YearSeconds = 2.6f;

        public static string Selected { get; private set; }

        public static string Hovered { get; private set; }

        public static ContestNetwork Network => current != null ? current.net : null;

        /// <summary>Which part of the view the camera shows: the 2026 network, or the history along the wave (1947-2026).</summary>
        public static bool History { get; private set; }

        public static void SetScenario(string id)
        {
            id = string.IsNullOrEmpty(id) ? Advantage.Now : id;
            if (id == Scenario) return;
            Scenario = id;
            var w = Advantage.Get()?.WorldById(id);
            // 2026 is measured in every world, so a world chosen from 2026 opens on its outcome (scrub the years back from there)
            if (id == Advantage.Now) Year = 2026;
            else if (w != null && Year <= 2026) Year = w.LastYear;
            current?.Retarget();
        }

        public static void SetYear(int year)
        {
            year = Mathf.Clamp(year, 2026, 2030);
            if (year == Year) return;
            Year = year;
            if (Scenario == Advantage.Now && year > 2026) Scenario = Advantage.Y2030;
            current?.Retarget();
        }

        public static void Select(string id)
        {
            if (id == Selected) return;
            string was = Selected;
            Selected = id;
            if (current == null) return;
            current.dirty = true;
            // its organism's frontiers bloom while it is selected: rebuild it and the one it replaces
            foreach (var x in new[] { was, id })
                if (current.net?.Get(x) is ContestNetwork.Body b && b.Kind == ContestNetwork.Kind.Company && !current.organismQueue.Contains(x))
                    current.organismQueue.Enqueue(x);
        }

        public static void ShowHistory(bool history)
        {
            if (history == History && current != null) return;
            History = history;
            Fly(history ? HillPresets.AdvantageHistory() : ViewPresets.Get(View), 1.6f);
            if (current != null) current.dirty = true;
        }

        static string Glyph(Color c) => "<color=#" + ColorUtility.ToHtmlStringRGB(c) + ">●</color>";

        public static HillPresets.ViewKey Key { get; } = new HillPresets.ViewKey(new (string, Color)[]
            {
                ("Equity: small to large " + Glyph(Land.Organism.EquityHeat(2000)), Land.Organism.EquityHeat(10)),
                ("Debt: light to heavy " + Glyph(Land.Organism.DebtHeat(500, 100)), Land.Organism.DebtHeat(5, 100)),
                ("Speculation: P/E low to high " + Glyph(Land.Organism.SpecHeat(140, false)), Land.Organism.SpecHeat(9, false)),
                ("Cash", Land.Organism.Cash), ("Built", Land.Organism.Built), ("Bought", Land.Organism.Bought), ("Stakes", Land.Organism.Stakes), ("Leases", Land.Organism.Leases),
                ("Owners", Gold), ("Advantage frontier (judged)", FrontierColor),
            },
            "Each company is one body made of its balance sheet, coloured as heat maps: its core by its equity (navy to white as it grows), what it owes below by its " +
            "leverage (red to yellow as it rises), and the dust of speculation out to its market value by its P/E (violet to pink as it inflates). " +
            "Cash, what it built, what it bought, its stakes and its leases keep muted tones. The supply chain stands in strata under the hill, consumer " +
            "products on top, government the floor; a company's column runs through the strata it works in. A dominant company's boundary is a ring as wide " +
            "as its share, walled by its sources of advantage, each as tall as Jev judges it keeps rivals and entrants out. Rose walls: talent and share price.");

        // ------------------------------------------------------------------ the layer

        Advantage model;
        ContestNetwork net;
        HillLandscape land;
        GameObject content;
        Transform arena;
        readonly MeshRenderer[] partRenderer = new MeshRenderer[(int)Part.Count];
        readonly List<(Renderer renderer, Part part)> renderers = new List<(Renderer, Part)>();
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        readonly List<HillLabel> labels = new List<HillLabel>();
        bool labelsShown, dirty, settling;
        float clock = Seconds, speed = 1, sinceRebuild, sinceLandscape, settleTime, playClock;
        int direction, cue;
        bool moveCamera, held;
        Vector2 pressed;

        public override void Prepare(GraphContext ctx) => Advantage.Provide(ctx.Text(Advantage.Path));

        public override void Upload(GraphContext ctx)
        {
            current = this;
            EconomyStage.Land().Place(transform);
            // the stack under the hill is the subject; the people on the hill, the top 1% above it and the attention cloud stay
            // (the user, 7 Nov 2026: "people still inhabit the hill and the cloud above the hill"); the hill's ground as context
            EconomyEmphasis.RegisterView(View, PartNames.Select(p => "advantage." + p),
                new[] { "activity.markers", "hills.crown", "hills.pool", "social.*" },
                new[] { "wave.crest", "wave.face", "wave.lives", "wave.ribs", "wave.ridges", "land.road" }, synthesis: true);
            ViewVariants.Register(View, () => Worlds().Count, k => SetScenario(Worlds()[Mathf.Clamp(k, 0, Worlds().Count - 1)]), k => k >= 0 && k < Worlds().Count ? Worlds()[k] : k.ToString());
            if (SmokeRun) SmokeChecks.RegisterSequence(Sequence, Smoke());
        }

        /// <summary>The worlds the view can show: now, 2030 on the most likely course, then the scenarios.</summary>
        public static List<string> Worlds()
        {
            var list = new List<string> { Advantage.Now, Advantage.Y2030 };
            var data = Advantage.Get();
            if (data != null) foreach (var s in data.Scenarios) list.Add(s.Id);
            return list;
        }

        public override void OnFocus(ViewPreset preset)
        {
            if (preset != null && preset.Id == View)
            {
                History = false;
                if (SmokeRun) Rest();
                else Play(true);
            }
            else
            {
                direction = 0;
                held = false;
                Hovered = null;
                Playing = false;
            }
        }

        public override void Tick(GraphContext ctx, CameraRig rig)
        {
            if (HillLandscapeLayer.Current == null || LandService.Current == null) return;
            HandleKeys();
            bool here = LandView.PresetId == View;
            if (!here && EconomyEmphasis.Get(Keys[(int)Part.Organs]) <= 0)
            {
                if (content) content.SetActive(false);
                ShowLabels(false);
                return;
            }

            model = Advantage.Get();
            if (model == null) return;
            if (land != HillLandscapeLayer.Current || net == null)
            {
                if (net != null && land != null && Mathf.Approximately(land.CrestZ, HillLandscapeLayer.Current.CrestZ)) land = HillLandscapeLayer.Current;
                else
                {
                    if (!HillRebuilds.Take()) return;
                    using (LayerClock.Measure("advantage: build")) Build();
                }
            }

            if (content == null) return;
            content.SetActive(true);
            if (direction != 0 && !held) Advance();
            if (here && Playing) PlayYears();
            Settle();
            if (dirty)
            {
                dirty = false;
                Rebuild(true);
            }

            if (here && !settling && Turned())
            {
                // the owners' circles and the bodies' outlines keep facing a camera that has orbited
                Rings();
                Outlines();
            }

            Apply();
            if (here && direction == 0 && !settling && rig != null && rig.Cam != null) Pick(rig.Cam);
            ShowLabels(here && direction >= 0 && Reveal(clock) > .5f && !settling);
        }

        /// <summary>One simulated year every <see cref="YearSeconds"/> to the world's last (SetYear moves "now" onto the most
        /// likely course), then stops.</summary>
        void PlayYears()
        {
            playClock += GraphTime.Delta;
            if (playClock < YearSeconds) return;
            playClock = 0;
            int last = Mathf.Min(2030, model.WorldById(Scenario == Advantage.Now ? Advantage.Y2030 : Scenario)?.LastYear ?? 2030);
            if (Year < last) SetYear(Year + 1);
            if (Year >= last) Playing = false;
        }

        /// <summary>The panel's world buttons: the chosen world from its measured 2026, its years then played forward, so the
        /// contest is seen to unfold (scrub the years to go back). Now stays still.</summary>
        public static void PlayWorld(string id)
        {
            id = string.IsNullOrEmpty(id) ? Advantage.Now : id;
            bool changed = id != Scenario || Year != 2026;
            Scenario = id;
            Year = 2026;
            Playing = id != Advantage.Now;
            if (current != null) current.playClock = YearSeconds * .6f;    // the first year comes sooner: 2026 is the same in every world
            if (changed) current?.Retarget();
        }

        // ------------------------------------------------------------------ the choreography

        static float Smooth(float a, float b, float t) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a, b, t));

        static float Open(float t) => Smooth(0, 1.6f, t);

        static float Rise(float t) => Smooth(.8f, 3.4f, t);

        static float Reveal(float t) => Smooth(3f, 4.6f, t);

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

        void Play(bool camera)
        {
            clock = 0;
            direction = 1;
            speed = 1;
            held = false;
            moveCamera = camera;
            cue = 0;
            if (camera) Fly(HillPresets.AdvantageFace(), 1f);
        }

        void Reverse(bool camera)
        {
            direction = -1;
            speed = Seconds / ReverseSeconds;
            held = false;
            moveCamera = camera;
            if (camera) Fly(HillPresets.AdvantageFace(), ReverseSeconds * .8f);
        }

        void Rest()
        {
            clock = Seconds;
            direction = 0;
            held = false;
        }

        void Advance()
        {
            clock += GraphTime.Delta * speed * direction;
            if (direction > 0)
            {
                if (moveCamera && cue == 0 && clock >= .9f)
                {
                    cue = 1;
                    Fly(ViewPresets.Get(View), 2.8f);
                }

                if (clock >= Seconds)
                {
                    clock = Seconds;
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

        // ------------------------------------------------------------------ the framing

        /// <summary>The view's camera angles (land-local yaw, pitch): from the future side, looking back at the crest and down
        /// the stack under it.</summary>
        public const float FrameYaw = 166, FramePitch = 12, StratumPitch = 34;

        /// <summary>The fitted pose: the target as an offset from the crest (land-local), and the distance; until the first fit,
        /// a pose on the crest's cut face and the stack's top.</summary>
        static Vector3 frameOffset = new Vector3(ContestNetwork.StackX, -120, 40);
        static float frameDistance = 900;

        public static Vector3 FrameTarget(float crestZ) => new Vector3(frameOffset.x, frameOffset.y, crestZ + frameOffset.z);

        public static float FrameDistance => frameDistance;

        /// <summary>The screen left free (normalized device coordinates, -1..1): clear of the title and the key on the left,
        /// the panel on the right, the preset bar below.</summary>
        const float FreeLeft = -.94f, FreeRight = .48f, FreeBottom = -.62f, FreeTop = .76f;

        /// <summary>What the camera shows: the whole stack under the hill (-1, the view's own pose), one stratum (0-8, with the
        /// strata above it as ghosts), or the hill with its people, the cloud and the consumer stratum (Strata).</summary>
        public static int Focus { get; private set; } = -1;

        public static int Strata => ContestNetwork.StratumOrder.Length;

        /// <summary>The hill at the crest (its tiers from raw materials up to tech, HillLandscape's profile), the attention cloud
        /// above it and the top 1%: the people's places, kept in view by the default framing.</summary>
        IEnumerable<(Vector3 c, float r)> HillSpheres()
        {
            float cz = net.CrestZ;
            foreach (var (x, h) in new[] { (30f, 10f), (70f, 24f), (110f, 30f), (160f, 40f), (200f, 40f), (232f, 58f) })
                yield return (new Vector3(x, h * .5f, cz - 6), Mathf.Max(18, h * .6f));
            yield return (new Vector3(84.6f, 102, cz - 10), 34);
            yield return (new Vector3(135.8f, 84, cz - 30), 22);
        }

        /// <summary>The bodies of strata lo..hi with their boundaries and walls, as spheres.</summary>
        List<(Vector3 c, float r)> StratumSpheres(int lo, int hi)
        {
            var list = new List<(Vector3, float)>();
            foreach (var b in net.Bodies)
            {
                if (b.Stratum < lo || b.Stratum > hi || !b.Alive) continue;
                if (b.Kind == ContestNetwork.Kind.Company || b.Kind == ContestNetwork.Kind.Satellite || b.Kind == ContestNetwork.Kind.State)
                {
                    list.Add((b.Pos, Mathf.Max(b.R, b.BodyR)));
                    if (b.Territory > 0)
                    {
                        float top = 0;
                        foreach (var m in model.Moats.Where(m => m.Actor == b.Id)) foreach (var x in m.Sources) top = Mathf.Max(top, WallHeight(x.Wall));
                        Vector3 g = new Vector3(b.Pos.x, net.StratumY[b.Stratum], b.Pos.z);
                        list.Add((g + Vector3.up * top * .5f, b.Territory));
                    }
                }
            }

            return list;
        }

        /// <summary>The target and distance that put every sphere inside the free screen at a yaw and pitch (land-local).</summary>
        (Vector3 target, float d) Fit(List<(Vector3 c, float r)> spheres, float yaw, float pitch, Vector3 start)
        {
            float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9, th = Mathf.Tan(CameraRig.FieldOfView * .5f * Mathf.Deg2Rad);
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0);
            Vector3 fwd = rot * Vector3.forward, right = rot * Vector3.right, up = rot * Vector3.up, target = start;
            float d = 600;
            for (int it = 0; it < 40 && spheres.Count > 0; it++)
            {
                Vector3 eye = target - fwd * d;
                float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
                foreach (var (c, r) in spheres)
                {
                    Vector3 v = c - eye;
                    float z = Mathf.Max(1, Vector3.Dot(v, fwd)), x = Vector3.Dot(v, right), y = Vector3.Dot(v, up);
                    minX = Mathf.Min(minX, (x - r) / (z * th * aspect)); maxX = Mathf.Max(maxX, (x + r) / (z * th * aspect));
                    minY = Mathf.Min(minY, (y - r) / (z * th)); maxY = Mathf.Max(maxY, (y + r) / (z * th));
                }

                float scale = Mathf.Max((maxX - minX) / (FreeRight - FreeLeft), (maxY - minY) / (FreeTop - FreeBottom));
                target += right * ((minX + maxX - FreeLeft - FreeRight) * .5f * d * th * aspect) + up * ((minY + maxY - FreeBottom - FreeTop) * .5f * d * th);
                d = Mathf.Clamp(d * Mathf.Lerp(1, scale, .5f), 120, 3000);
            }

            return (target, d);
        }

        /// <summary>Fits the view's own pose (the default focus) when the network is built: the hill with its people and the
        /// cloud, the owners above, and the stack's top four strata (consumer products down to chips). The camera stays put as
        /// worlds and years move the population; the panel's strata buttons fly to the others.</summary>
        void Frame()
        {
            if (ScreenLayout.IsPortrait || Xr.XrSession.Active || net == null) return;
            var spheres = StratumSpheres(0, Strata - 1);
            spheres.AddRange(HillSpheres());
            foreach (var b in net.Bodies)
                if ((b.Kind == ContestNetwork.Kind.Owner || b.Kind == ContestNetwork.Kind.Wealth) && b.Alive) spheres.Add((b.Pos + Vector3.up * 3, b.R + 3));
            var (target, d) = Fit(spheres, FrameYaw, FramePitch, new Vector3(ContestNetwork.StackX, -120, net.CrestZ + 40));
            frameOffset = new Vector3(target.x, target.y, target.z - net.CrestZ);
            frameDistance = d;
            Debug.Log($"[Why] advantage framing: target {target.x:0},{target.y:0},crest+{target.z - net.CrestZ:0}, distance {d:0}; strata at " +
                      string.Join(", ", net.StratumY.Select(y => y.ToString("0"))));
            var preset = ViewPresets.Get(View);
            if (preset != null && preset.Id == View)
            {
                preset.FixedTarget = EconomyStage.Land().World(FrameTarget(net.CrestZ));
                preset.Distance = frameDistance;
            }

            if (LandView.PresetId == View && direction == 0 && !History && Focus < 0) Fly(ViewPresets.Get(View), instantMoves ? 0 : .8f);
        }

        /// <summary>Flies to one stratum (0-8: its plane, bodies, boundaries and walls from above, as close as fits), the whole
        /// stack (Strata), or back to the view's own pose (-1).</summary>
        public static void FocusOn(int focus)
        {
            Focus = focus;
            if (current == null || current.net == null) return;
            if (History) ShowHistory(false);
            current.dirty = true;    // the cutaway and the close-up's labels
            current.FlyFocus(instantMoves ? 0 : 1.4f);
        }

        void FlyFocus(float seconds)
        {
            if (Focus < 0) { Fly(ViewPresets.Get(View), seconds); return; }
            bool hill = Focus >= Strata;
            var spheres = hill ? StratumSpheres(0, 0) : StratumSpheres(Focus, Focus);
            if (hill)
            {
                spheres.Clear();
                spheres.AddRange(HillSpheres());
                // the consumer stratum's nearest band: what people buy, right under the hill
                foreach (var b in net.Bodies)
                    if (b.Stratum == 0 && b.Alive && (b.Kind == ContestNetwork.Kind.Company || b.Kind == ContestNetwork.Kind.Satellite) && Mathf.Abs(b.Pos.x - 130) < 160)
                        spheres.Add((b.Pos, b.R * .6f));
            }

            float pitch = hill ? FramePitch + 4 : StratumPitch;
            var (target, d) = Fit(spheres, FrameYaw, pitch, new Vector3(ContestNetwork.StackX, hill ? 0 : net.StratumY[Focus], net.StackZ));
            Fly(HillPresets.AdvantagePose(EconomyStage.Land().World(target), pitch, d), seconds);
        }

        static void Fly(ViewPreset preset, float seconds)
        {
            GraphRoot root = GraphRoot.Instance;
            if (preset != null && root != null && root.Rig != null) root.Rig.FlyTo(preset.Pose(), seconds);
        }

        /// <summary>The opening's scales (the network grows out of the crest's plane and rises) and every part's alpha.</summary>
        void Apply()
        {
            float open = Mathf.Max(.02f, Open(clock)), rise = Mathf.Max(.002f, Rise(clock)), reveal = Reveal(clock);
            content.transform.localScale = new Vector3(1, rise, open);
            // in the history camera the present steps back: the lane and its contests are the story
            float present = History ? .06f : 1;
            foreach ((Renderer r, Part part) in renderers)
            {
                float a = EconomyEmphasis.Get(Keys[(int)part]);
                if (part != Part.Organs && part != Part.Shells && part != Part.Landscape) a *= reveal;
                if (part == Part.Landscape) a *= rise;
                if (part != Part.Thread) a *= present;
                else if (!History) a = 0;    // the lineage lane belongs to the history camera
                EconomyEmphasis.Set(r, a);
            }

            float organ = EconomyEmphasis.Get(Keys[(int)Part.Organs]) * present;
            foreach (var kv in organisms)
            {
                if (!kv.Value.renderer) continue;
                var body = net.Get(kv.Key);
                float fade = body == null || body.Alive ? 1 : body.State != null && body.State.Status == "extinct" ? .2f : .35f;
                float a = organ * fade * Lit(kv.Key);
                EconomyEmphasis.Set(kv.Value.renderer, a);
                if (occluders.TryGetValue(kv.Key, out var occ) && occ) occ.enabled = a > .6f && kv.Value.renderer.enabled;
            }
        }

        // ------------------------------------------------------------------ worlds, years and the live layout

        void Retarget()
        {
            if (net == null) return;
            net.Retarget(Scenario, Year);
            if (instantMoves)
            {
                net.Step(260);
                Organisms(true);
                Rebuild(true);
                return;
            }

            Organisms(false);

            settling = true;
            settleTime = 0;
            sinceRebuild = 1;
        }

        /// <summary>While the population moves: a few steps of the forces a frame, the meshes rebuilt about 15 times a second and
        /// the landscape 4 times, until it settles (or 7 s pass).</summary>
        void Settle()
        {
            if (net == null) return;
            BuildQueued(OrganismsPerFrame);
            if (!settling) return;
            net.Step(3);
            PlaceOrganisms();
            settleTime += GraphTime.Delta;
            sinceRebuild += GraphTime.Delta;
            sinceLandscape += GraphTime.Delta;
            bool done = net.Energy < .004f || settleTime > 7;
            if (sinceRebuild >= 1 / 15f || done)
            {
                sinceRebuild = 0;
                bool contours = sinceLandscape >= 1f || done;
                if (contours) sinceLandscape = 0;
                using (LayerClock.Measure("advantage: rebuild")) Rebuild(contours);
            }

            if (done)
            {
                settling = false;
                Organisms(false);    // the frontiers face where the neighbours finally stand
            }
        }

        // ------------------------------------------------------------------ the build

        void Build()
        {
            Clear();
            land = HillLandscapeLayer.Current;
            using (LayerClock.Measure("advantage: layout")) net = new ContestNetwork(model, land.CrestZ, Scenario, Year);
            content = new GameObject("Advantage");
            content.transform.SetParent(transform, false);
            content.transform.localPosition = new Vector3(0, 0, land.CrestZ);
            arena = new GameObject("Network").transform;
            arena.SetParent(content.transform, false);
            arena.localPosition = new Vector3(0, 0, -land.CrestZ);
            Organisms(SmokeRun);    // a normal entry builds them a few a frame while the opening condenses them
            Rebuild(true);
            Frame();
            Log();
        }

        /// <summary>Every mesh from the layout's present positions (the landscape only when asked: it is the costly one).</summary>
        void Rebuild(bool landscape)
        {
            Organs();
            PlaceOrganisms();
            Rings();
            Outlines();
            // the heavy, slow-changing parts follow the landscape's cadence while the population settles
            if (landscape || !settling || !partRenderer[(int)Part.Walls])
            {
                Columns();
                Walls();
            }
            Rivers();
            Threads();
            Sparks();
            Lineage();
            if (landscape || !partRenderer[(int)Part.Landscape]) Landscape();
            MakeLabels();
            if (labelsShown) HillLabels.Set(this, labels);
        }

        /// <summary>The low ground: the buyers' plains and the states' plaques, muted.</summary>
        void Organs()
        {
            Drop(Part.Shells);
            var b = new SurfaceMeshBuilder();
            float id = EconomyEmphasis.Id(Keys[(int)Part.Shells]);
            foreach (var body in net.Bodies)
            {
                float lit = Lit(body.Id);
                if (body.Kind == ContestNetwork.Kind.Buyer) Disc(b, id, body.Pos, body.R, BuyerColor.linear * .7f * lit, 1.2f);
                else if (body.Kind == ContestNetwork.Kind.State) Disc(b, id, body.Pos, body.R, StateColor.linear * .6f * lit, 2f);
            }

            partRenderer[(int)Part.Shells] = Surface(b, Part.Shells, arena, 3180);
        }

        // ------------------------------------------------------------------ the organisms

        /// <summary>Each company's body (<see cref="Organism"/>): its own mesh, built in its own frame and moved by its transform,
        /// so the layout can move them every frame without rebuilding. Rebuilt when its world, year or frontiers change.</summary>
        readonly Dictionary<string, (MeshRenderer renderer, string key)> organisms = new Dictionary<string, (MeshRenderer, string)>();

        /// <summary>The organisms still to rebuild after a change (a few a frame, so no frame stalls).</summary>
        readonly Queue<string> organismQueue = new Queue<string>();

        const int OrganismsPerFrame = 4;

        /// <summary>Queues every company's organism for rebuilding; <paramref name="now"/> builds them all at once.</summary>
        void Organisms(bool now)
        {
            HotChokes();
            organismQueue.Clear();
            foreach (var body in net.Bodies)
                if (body.Kind == ContestNetwork.Kind.Company) organismQueue.Enqueue(body.Id);
            if (now) BuildQueued(int.MaxValue);
        }

        /// <summary>Rebuilds queued organisms within a frame's budget: a giant (642 points, about 13k vertices) costs 3, the
        /// rest 1, so a world's change never stalls a frame.</summary>
        void BuildQueued(int budget)
        {
            while (budget > 0 && organismQueue.Count > 0)
            {
                var body = net.Get(organismQueue.Dequeue());
                if (body == null) continue;
                Organism(body);
                budget -= body.R > 30 ? 6 : body.R > 8 ? 2 : 1;
            }
        }

        /// <summary>Where a company's advantage presses (its frontiers): toward those who pay it at a choke point (as hard as
        /// the choke is hard to replace, times its judged advantage), toward the rivals it is beating in a present contest,
        /// and, more softly, toward the holder of a contest it is losing.</summary>
        /// <summary>The world's three hardest choke points (the choke's strength times the holder's judged advantage there): the
        /// only bodies that bloom, so the eye goes to them first.</summary>
        HashSet<string> hotChokes = new HashSet<string>();

        void HotChokes()
        {
            hotChokes = new HashSet<string>(model.Chokes
                .Select(c => (c, b: net.Get(c.Actor)))
                .Where(x => x.b != null && x.b.Kind == ContestNetwork.Kind.Company && x.b.Alive)
                .OrderByDescending(x => (double.IsNaN(x.c.Strength) ? .6 : x.c.Strength) * (double.IsNaN(ContestNetwork.AdvantageAt(x.b.Actor, Scenario, Year)) ? 3 : ContestNetwork.AdvantageAt(x.b.Actor, Scenario, Year)))
                .Select(x => x.b.Id).Distinct().Take(3));
        }

        /// <summary>Where a company's advantage presses: toward the living who must pay it at a choke point (bloom for the three
        /// hardest, a tint for the rest), and toward the living rivals it is beating now (a tint; bloom when it is selected).
        /// The dead press on nothing.</summary>
        List<Organism.Front> Fronts(ContestNetwork.Body body)
        {
            var fronts = new List<Organism.Front>();
            if (!body.Alive || body.Actor == null) return fronts;
            double a0 = ContestNetwork.AdvantageAt(body.Actor, Scenario, Year);
            float adv = !double.IsNaN(a0) ? Mathf.Clamp01((float)a0 / 6f) : .4f;
            foreach (var c in model.Chokes.Where(c => c.Actor == body.Id).OrderByDescending(c => double.IsNaN(c.Strength) ? 0 : c.Strength).Take(1))
            {
                Vector3 sum = Vector3.zero;
                foreach (var l in net.Links)
                    if (l.Kind == "flow" && l.B == body && l.A != body && l.A.Alive && l.A.Kind == ContestNetwork.Kind.Company)
                        sum += (l.A.Pos - body.Pos).normalized * Mathf.Sqrt((float)Math.Max(1, l.UsdBn));
                if (sum.sqrMagnitude > 1e-4f)
                    fronts.Add(new Organism.Front(sum.normalized, (double.IsNaN(c.Strength) ? .6f : (float)c.Strength) * adv, hotChokes.Contains(body.Id)));
            }

            var rivals = new HashSet<string>();
            foreach (var fp in model.Flashpoints.Where(fp => fp.InArena && fp.Year >= 2023 && fp.Holder == body.Id).OrderByDescending(fp => fp.Sources.Length))
            {
                var rv = fp.Rivals.Select(r => net.Get(r)).FirstOrDefault(x => x != null && x != body && x.Alive);
                if (rv == null || !rivals.Add(rv.Id)) continue;
                fronts.Add(new Organism.Front((rv.Pos - body.Pos).normalized, .7f * adv, Selected == body.Id));
                if (rivals.Count >= 2) break;
            }

            return fronts;
        }

        readonly LineMeshBuilder organismBuilder = new LineMeshBuilder(8192);

        static uint Fnv(string s)
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (char ch in s ?? "") h = (h ^ ch) * 16777619;
                return h;
            }
        }

        void Organism(ContestNetwork.Body body)
        {
            var fronts = Fronts(body);
            string key = Scenario + "|" + Year + "|" + body.Value.ToString("0.0") + "|" + body.Alive + "|" + (Selected == body.Id) + "|" +
                         string.Join(",", fronts.Select(f => (f.Direction * 10).ToString("0") + f.Strength.ToString("0.0") + f.Hot));
            if (organisms.TryGetValue(body.Id, out var have) && have.key == key && have.renderer) return;
            // dense enough that a body's near face reads as a cloud once its far side is hidden
            int detail = body.R > 30 ? 4 : body.R > 8 ? 3 : 2;
            var shape = new Organism(body, fronts, detail);
            body.BodyR = shape.MaxRadius;
            body.BodyMin = shape.Min;
            body.BodyMax = shape.Max;
            var b = organismBuilder;
            b.Clear();
            float id = EconomyEmphasis.Id(Keys[(int)Part.Organs]);
            var rnd = new System.Random(unchecked((int)Fnv(body.Id)));
            // the user, 7 Nov 2026: "more of a point cloud representation with a boundary outline, rather than wireframes".
            // The surface's points, as opaque as the parts there are tangible, hot where a frontier blooms ...
            for (int i = 0; i < shape.Points.Length; i++)
            {
                float f = shape.Frontier[i], g = shape.Tint[i];
                Color c = Color.Lerp(shape.Colors[i], Color.Lerp(shape.Colors[i], Color.white, .45f), g * .7f);
                c = Color.Lerp(c, FrontierColor, f * .9f);
                c.a = Mathf.Clamp01((.62f + .3f * shape.Solidity[i] + .2f * g + .3f * f) * (shape.Known ? 1 : .7f));
                Vector3 p = shape.Points[i];
                // a dot: a dash about a pixel long (a segment along its own normal vanishes where it faces the camera)
                b.AddSegment(p, p + Vector3.up * DotLength, c, 3.2f + .8f * g + 2.4f * f, 0, id, f > 0 ? .9f + 5f * f * f : .93f);
            }

            // ... and a skin of points under it, between the surface and the hidden solid inside (OccluderDepth), each coloured by
            // the parts at its place, so the body reads as a cloud of some depth while it hides what stands behind it
            int skin = detail >= 4 ? 2 : 4;
            for (int i = 0; i < shape.Points.Length; i++)
                for (int k = 0; k < skin; k++)
                {
                    float u = Mathf.Lerp(OccluderDepth + .015f, .995f, (float)rnd.NextDouble());
                    Vector3 p = shape.Points[i] * u + RandomDir(rnd) * (.025f * shape.MeanRadius * (float)rnd.NextDouble());
                    Color c = shape.ColorAt(p);
                    c.a = (.45f + .3f * (u - OccluderDepth) / (1 - OccluderDepth)) * (shape.Known ? 1 : .7f);
                    b.AddSegment(p, p + Vector3.up * DotLength, c, 2.8f, 0, id, .92f);
                }

            outlineShapes[body.Id] = shape;
            float rb = shape.MeanRadius;
            bool loss = body.State != null ? body.State.Ni < 0 : body.Actor.LossEvidence;
            double pe = body.State != null ? body.State.Pe : body.Actor.Pe;
            Color pc = Land.Organism.SpecHeat(pe, loss);
            // speculation: the market value as a dotted membrane with a sparse wire, and between what its profits justify (the
            // earnings shell, at the reference P/E) and that membrane a dust as dense as the P/E is high (loss-makers densest)
            if (!double.IsNaN(body.Value) && body.Alive && body.R > rb + .3f)
            {
                float inner = Mathf.Max(rb, body.EarningsR);
                double shell = 4.0 / 3 * Math.PI * (Math.Pow(body.R, 3) - Math.Pow(inner, 3));
                double heat = loss ? 4.5 : double.IsNaN(pe) || pe <= 0 ? 1 : Math.Max(0, Math.Min(4.5, Math.Log(pe / 15, 2)));
                int dust = shell > 0 ? Mathf.Clamp((int)(shell * .004 * heat), 0, 900) : 0;
                for (int k = 0; k < dust; k++)
                {
                    Vector3 d = RandomDir(rnd);
                    float u = (float)rnd.NextDouble(), rr = Mathf.Lerp(inner, body.R, Mathf.Pow(u, .5f));
                    Vector3 p = d * rr;
                    b.AddSegment(p, p + d * .05f, WithAlpha(pc, .3f + .25f * u), 1.6f, 0, id, .93f);
                }

            }

            // promises (purchase and compute commitments): a ghost lobe of points beneath, joined by a dotted stalk
            foreach (var part in ContestNetwork.PartsOf(body))
            {
                if (part.Kind != ContestNetwork.PartKind.Promises || part.Radius <= 0) continue;
                var (gd, _) = Land.Organism.Geodesic(2);
                Vector3 at = Vector3.down * (body.R + part.Radius + 2);
                foreach (var d in gd) b.AddSegment(at + d * part.Radius, at + d * (part.Radius + .06f), WithAlpha(PromiseColor * .8f, .42f), 1.5f, 0, id, .93f);
                for (int k = 1; k < 8; k++)
                {
                    Vector3 p = Vector3.Lerp(Vector3.down * rb, at + Vector3.up * part.Radius, k / 8f);
                    b.AddSegment(p, p + Vector3.down * .3f, WithAlpha(PromiseColor * .8f, .4f), 1.4f, 0, id, .9f);
                }
            }

            // a loss paid for by others' money (a year's, where known): a comet of points from the body toward the market it buys
            double burn = !body.Alive ? 0 : body.State != null ? Math.Max(0, -body.State.Ni) : !double.IsNaN(body.Actor.BurnYearly) ? body.Actor.BurnYearly : body.Actor.Burn;
            if (burn > 0 && !double.IsNaN(burn))
            {
                var market = net.Get("consumers") ?? net.Get("enterprises");
                Vector3 dir = market != null ? (market.Pos - body.Pos).normalized : new Vector3(.8f, .35f, -.45f).normalized;
                float len = 3 + 3.2f * Mathf.Pow((float)burn, 1f / 3);
                int n = 36;
                for (int k = 0; k < n; k++)
                {
                    float t = k / (float)(n - 1), spread = t * 1.4f;
                    Vector3 p = dir * (rb + len * t) + RandomDir(rnd) * spread * (float)rnd.NextDouble();
                    b.AddSegment(p, p + dir * .2f, WithAlpha(BurnColor, (1 - t) * .8f), Mathf.Lerp(2.6f, 1.2f, t), 0, id, .95f);
                }
            }

            var mesh = b.ToMesh("Organism " + body.Id);
            owned.Add(mesh);
            MeshRenderer mr = have.renderer;
            if (mr)
            {
                // keep the organism's object and material; swap its mesh
                var mf = mr.GetComponent<MeshFilter>();
                if (mf.sharedMesh) { owned.Remove(mf.sharedMesh); Destroy(mf.sharedMesh); }
                mf.sharedMesh = mesh;
            }
            else
            {
                // alpha-blended, not additive, so the parts' hues survive their overlaps
                Material m = GraphMaterials.Raw(GraphMaterials.Line(Color.white, 1, 3183, false));
                owned.Add(m);
                mr = AddMesh("Organism " + body.Id, mesh, m);
                mr.transform.SetParent(arena, false);
                mr.transform.localPosition = body.Pos;
            }

            organisms[body.Id] = (mr, key);
            Occluder(body, shape, detail, mr.transform);
        }

        // ------------------------------------------------------------------ occlusion

        /// <summary>The hidden solid's size against the body's surface: the skin of points lies between the two.</summary>
        const float OccluderDepth = .9f;

        /// <summary>A point's dash, in land units: about a pixel at the view's distance, so a dot reads as a dot.</summary>
        const float DotLength = .3f;

        /// <summary>Each body's hidden solid (the user, 7 Nov 2026: "we still need point cloud occlusion"): its surface drawn
        /// into depth only, before every point and line, so bodies, rivers and dust behind it are hidden while it shows
        /// as points.</summary>
        readonly Dictionary<string, MeshRenderer> occluders = new Dictionary<string, MeshRenderer>();

        Material occluderMaterial;

        void Occluder(ContestNetwork.Body body, Land.Organism shape, int detail, Transform organism)
        {
            if (occluderMaterial == null)
            {
                // the surface shader with its colour blended away (zero of the source, all of what is there) and depth written,
                // just after the labels (TextMeshPro, queue 3000: names stay readable) and before every mark of this view and of
                // the hill (3180 and up), so whatever stands behind a body is hidden
                occluderMaterial = GraphMaterials.Raw(GraphMaterials.Surface(Color.white, 1, 3001));
                occluderMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
                occluderMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                occluderMaterial.SetFloat("_ZWrite", 1);
                occluderMaterial.SetFloat("_EdgeSoft", 0);
                owned.Add(occluderMaterial);
            }

            var verts = new Vector3[shape.Points.Length];
            var cols = new Color[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                verts[i] = shape.Points[i] * OccluderDepth;
                cols[i] = Color.white;
            }

            var mesh = new Mesh { name = "Occluder " + body.Id };
            mesh.vertices = verts;
            mesh.colors = cols;
            mesh.triangles = Land.Organism.GeodesicFaces(detail);
            mesh.RecalculateBounds();
            owned.Add(mesh);
            if (occluders.TryGetValue(body.Id, out var r) && r)
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf.sharedMesh) { owned.Remove(mf.sharedMesh); Destroy(mf.sharedMesh); }
                mf.sharedMesh = mesh;
                return;
            }

            r = AddMesh("Occluder " + body.Id, mesh, occluderMaterial);
            r.transform.SetParent(organism, false);
            occluders[body.Id] = r;
        }

        /// <summary>Each living organism's shape, kept for its outline (the outline turns with the camera; the body does not).</summary>
        readonly Dictionary<string, Land.Organism> outlineShapes = new Dictionary<string, Land.Organism>();

        const int OutlineBins = 96;
        readonly float[] outlineR = new float[OutlineBins], outlineF = new float[OutlineBins];

        /// <summary>The boundaries, facing the camera: each body's silhouette as a line (lit where a frontier blooms), its market
        /// value as a dotted circle in the colour of its P/E (the speculative dust lies inside it), and what its earnings justify
        /// as a faint dotted circle. Rebuilt with the other parts, and when the camera swings.</summary>
        void Outlines()
        {
            Drop(Part.Outlines);
            var b = new LineMeshBuilder(16384);
            float id = EconomyEmphasis.Id(Keys[(int)Part.Outlines]);
            Vector3 eye = Eye();
            foreach (var body in net.Bodies)
            {
                if (body.Kind != ContestNetwork.Kind.Company || !body.Alive || !outlineShapes.TryGetValue(body.Id, out var shape) || Above(body.Id)) continue;
                float lit = Lit(body.Id);
                Vector3 v = eye - body.Pos;
                v = v.sqrMagnitude > 1e-3f ? v.normalized : Vector3.forward;
                Vector3 u = Vector3.Cross(v, Mathf.Abs(v.y) > .95f ? Vector3.right : Vector3.up).normalized, w = Vector3.Cross(v, u);
                // about five surface points to a bin, so each bin's reach comes from a point near the rim
                int bins = Mathf.Clamp(shape.Points.Length / 5 / Mathf.Max(1, shape.Points.Length / 700), 24, OutlineBins);
                Array.Clear(outlineR, 0, OutlineBins);
                Array.Clear(outlineF, 0, OutlineBins);
                Color mean = Color.black;
                int stride = Mathf.Max(1, shape.Points.Length / 700);    // the silhouette from at most ~700 points
                for (int i = 0; i < shape.Points.Length; i += stride)
                {
                    Vector3 p = shape.Points[i];
                    float a = Vector3.Dot(p, u), c = Vector3.Dot(p, w), r = Mathf.Sqrt(a * a + c * c);
                    int k = (int)((Mathf.Atan2(c, a) / (2 * Mathf.PI) + 1) * bins) % bins;
                    if (r > outlineR[k]) outlineR[k] = r;
                    if (shape.Frontier[i] > outlineF[k]) outlineF[k] = shape.Frontier[i];
                    mean += shape.Colors[i];
                }

                mean /= Mathf.Max(1, (shape.Points.Length + stride - 1) / stride);
                // bins no point fell in take their neighbours' reach; then a light smoothing that never cuts inside a point
                for (int pass = 0; pass < 3; pass++)
                    for (int k = 0; k < bins; k++)
                        if (outlineR[k] <= 0) outlineR[k] = Mathf.Max(outlineR[(k + bins - 1) % bins], outlineR[(k + 1) % bins]);
                // the outline in the body's own colour (its parts' mean), a little brighter than its points
                Color line = WithAlpha(Color.Lerp(mean, Color.white, .12f) * 1.15f, .62f * lit);
                Vector3 prev = Vector3.zero;
                for (int k = 0; k <= bins; k++)
                {
                    int kk = k % bins;
                    float r = Mathf.Max(outlineR[kk], .5f * (outlineR[(kk + bins - 1) % bins] + outlineR[(kk + 1) % bins])) * 1.03f;
                    float ang = (kk + .5f) * 2 * Mathf.PI / bins;
                    Vector3 p = body.Pos + (u * Mathf.Cos(ang) + w * Mathf.Sin(ang)) * r;
                    if (k > 0)
                    {
                        float f = outlineF[kk];
                        b.AddSegment(prev, p, f > 0 ? Color.Lerp(line, WithAlpha(FrontierColor, lit), f) : line, 1.3f + 1.8f * f, 0, id, f > 0 ? .95f + 4.5f * f * f : .95f);
                    }

                    prev = p;
                }

                // the market value: a dotted circle in the P/E's heat, the dust of speculation inside it
                if (!double.IsNaN(body.Value) && body.R > shape.MeanRadius + .3f)
                {
                    double pe = body.State != null ? body.State.Pe : body.Actor.Pe;
                    bool loss = body.State != null ? body.State.Ni < 0 : body.Actor.LossEvidence;
                    Color pc = WithAlpha(Land.Organism.SpecHeat(pe, loss), .9f * lit);
                    int n = Mathf.Clamp(Mathf.RoundToInt(body.R * 2.4f), 24, 160);
                    for (int i = 0; i < n; i++)
                    {
                        float ang = i * 2 * Mathf.PI / n;
                        Vector3 p = body.Pos + (u * Mathf.Cos(ang) + w * Mathf.Sin(ang)) * body.R, t = -u * Mathf.Sin(ang) + w * Mathf.Cos(ang);
                        b.AddSegment(p - t * DotLength * .5f, p + t * DotLength * .5f, pc, 2.8f, 0, id, .95f);
                    }

                    if (body.EarningsR > shape.MeanRadius + .5f && body.EarningsR < body.R - .5f)
                    {
                        int m = Mathf.Clamp(Mathf.RoundToInt(body.EarningsR * 1.6f), 16, 100);
                        for (int i = 0; i < m; i++)
                        {
                            float ang = (i + .5f) * 2 * Mathf.PI / m;
                            Vector3 p = body.Pos + (u * Mathf.Cos(ang) + w * Mathf.Sin(ang)) * body.EarningsR, t = -u * Mathf.Sin(ang) + w * Mathf.Cos(ang);
                            b.AddSegment(p - t * DotLength * .5f, p + t * DotLength * .5f, WithAlpha(new Color(.80f, .84f, .92f), .45f * lit), 1.4f, 0, id, .9f);
                        }
                    }
                }
            }

            partRenderer[(int)Part.Outlines] = Lines(b, Part.Outlines, arena, 3186, false);
        }

        static Vector3 RandomDir(System.Random rnd)
        {
            float z = (float)rnd.NextDouble() * 2 - 1, a = (float)rnd.NextDouble() * Mathf.PI * 2, s = Mathf.Sqrt(1 - z * z);
            return new Vector3(s * Mathf.Cos(a), z, s * Mathf.Sin(a));
        }

        /// <summary>Each organism to its body's place (every frame while the layout moves).</summary>
        void PlaceOrganisms()
        {
            foreach (var kv in organisms)
            {
                var body = net.Get(kv.Key);
                if (body != null && kv.Value.renderer) kv.Value.renderer.transform.localPosition = body.Pos;
            }
        }

        void DropRenderer(MeshRenderer r)
        {
            if (!r) return;
            var mf = r.GetComponent<MeshFilter>();
            if (mf && mf.sharedMesh) { owned.Remove(mf.sharedMesh); Destroy(mf.sharedMesh); }
            if (r.sharedMaterial) { owned.Remove(r.sharedMaterial); Destroy(r.sharedMaterial); }
            Destroy(r.gameObject);
        }


        /// <summary>The shell's colour by P/E (the world's year where simulated): cool blue under 15, white to 35, gold to 80,
        /// magenta beyond and for a loss-maker carried by a valuation.</summary>
        public static Color PeColor(ContestNetwork.Body body)
        {
            double pe = body.State != null && !double.IsNaN(body.State.Pe) ? body.State.Pe : body.Actor != null ? body.Actor.Pe : double.NaN;
            bool loss = body.State != null ? body.State.Ni < 0 : body.Actor != null && (body.Actor.Private || body.Actor.Pe < 0);
            if (double.IsNaN(pe) || pe < 0) return loss ? PeBubble : PeMid;
            if (pe < 15) return Color.Lerp(PeLow, PeMid, (float)pe / 15f);
            if (pe < 35) return Color.Lerp(PeMid, PeHigh, ((float)pe - 15) / 20f);
            return Color.Lerp(PeHigh, PeBubble, Mathf.Clamp01(((float)pe - 35) / 65f));
        }

        float Lit(string id) => (Selected == null || id == Selected || Linked(id) ? 1 : .35f) * (Above(id) ? 0 : Below(id) ? .28f : 1);

        /// <summary>Whether a body stands below the focused stratum: kept as faded context in a close-up.</summary>
        bool Below(string id)
        {
            if (Focus < 0 || Focus >= Strata || net == null) return false;
            var b = net.Get(id);
            return b != null && (b.Kind == ContestNetwork.Kind.Company || b.Kind == ContestNetwork.Kind.Satellite || b.Kind == ContestNetwork.Kind.State) && b.Stratum > Focus;
        }

        /// <summary>Whether a stratum other than the focused one is shown only as context (its walls and names left out).</summary>
        bool OffFocus(int stratum) => Focus >= 0 && Focus < Strata && stratum != Focus;

        /// <summary>Whether a body stands above the focused stratum (the cutaway: the strata above a focused one are ghosts,
        /// so the camera looks down onto its territories unobstructed).</summary>
        bool Above(string id)
        {
            if (Focus < 0 || Focus >= Strata || net == null) return false;
            var b = net.Get(id);
            if (b == null) return false;
            int st = b.Kind == ContestNetwork.Kind.Company || b.Kind == ContestNetwork.Kind.Satellite || b.Kind == ContestNetwork.Kind.State ? b.Stratum : -1;
            return st < Focus;
        }

        bool AboveStratum(int stratum) => Focus >= 0 && Focus < Strata && stratum < Focus;

        bool Linked(string id)
        {
            if (Selected == null) return true;
            foreach (var l in net.Links)
                if ((l.A.Id == Selected && l.B.Id == id) || (l.B.Id == Selected && l.A.Id == id)) return true;
            return false;
        }

        /// <summary>The ownership class and the ruler: the owners' golden circles (muted gold, the wealth groups' brighter), each
        /// company's cap table as gold arcs around it, and the altitude ruler from the low ground to the high.</summary>
        int wallTick;

        Vector3 ringsEye;
        float ringsClock;

        /// <summary>Whether the camera has swung more than 8° around the population since the circles were last turned to it
        /// (checked four times a second).</summary>
        bool Turned()
        {
            if ((ringsClock += GraphTime.Delta) < .25f || arena == null) return false;
            ringsClock = 0;
            Vector3 c = new Vector3(ContestNetwork.X0, ContestNetwork.OwnerY, net.CrestZ + (ContestNetwork.ZFrom + ContestNetwork.ZTo) * .5f);
            return Vector3.Angle(Eye() - c, ringsEye - c) > 8;
        }

        Vector3 Eye()
        {
            var cam = GraphRoot.Instance != null && GraphRoot.Instance.Rig != null ? GraphRoot.Instance.Rig.Cam : null;
            return cam != null && arena != null ? arena.InverseTransformPoint(cam.transform.position) : Vector3.zero;
        }

        void Rings()
        {
            ringsEye = Eye();
            Drop(Part.Rings);
            var b = new LineMeshBuilder();
            float id = EconomyEmphasis.Id(Keys[(int)Part.Rings]);
            foreach (var body in net.Bodies)
            {
                float lit = Lit(body.Id);
                if (body.Kind == ContestNetwork.Kind.Company)
                {
                    if (!body.Alive) continue;
                    CapTable(b, id, body, lit);
                }
                else if (body.Kind == ContestNetwork.Kind.Owner || body.Kind == ContestNetwork.Kind.Wealth)
                {
                    // a golden circle facing the camera, filled with dots, as wide as √ of what it holds; styled by the kind of owner
                    bool wealth = body.Kind == ContestNetwork.Kind.Wealth;
                    Vector3 normal = Facing(body.Pos);
                    Color c = WithAlpha(Color.Lerp(wealth ? GoldBright : OwnerColor(body.Owner?.Kind), new Color(.6f, .55f, .45f), .25f), (wealth ? .95f : .8f) * lit);
                    Circle(b, id, body.Pos, body.R, normal, c, wealth ? 2.6f : 2f, .95f);
                    DottedDisc(b, id, body.Pos, body.R, normal, WithAlpha(c, .55f * lit));
                }
            }

            partRenderer[(int)Part.Rings] = Lines(b, Part.Rings, arena, 3188, true);
        }

        /// <summary>A rise from the 2026 ground, and a fall (muted, under the bloom).</summary>
        static readonly Color Rose = new Color(.45f, .92f, .58f), Fell = new Color(.98f, .42f, .40f);

        /// <summary>The direction from a point toward the camera (land-local), so a golden circle faces the viewer.</summary>
        Vector3 Facing(Vector3 at)
        {
            var cam = GraphRoot.Instance != null && GraphRoot.Instance.Rig != null ? GraphRoot.Instance.Rig.Cam : null;
            if (cam == null || arena == null) return new Vector3(.35f, .2f, .92f).normalized;
            Vector3 local = arena.InverseTransformPoint(cam.transform.position) - at;
            return local.sqrMagnitude > 1e-3f ? local.normalized : Vector3.forward;
        }

        /// <summary>A disc of dots in the plane facing <paramref name="normal"/>, three rings inside the circle.</summary>
        static void DottedDisc(LineMeshBuilder b, float id, Vector3 at, float radius, Vector3 normal, Color c)
        {
            Vector3 u = Vector3.Cross(normal, Mathf.Abs(normal.y) > .9f ? Vector3.right : Vector3.up).normalized, w = Vector3.Cross(normal, u).normalized;
            for (int k = 1; k <= 3; k++)
            {
                float r = radius * k / 4f;
                int n = Mathf.Clamp(Mathf.RoundToInt(r * 2.2f), 8, 120);
                for (int i = 0; i < n; i++)
                {
                    float a = (i + k * .5f) * Mathf.PI * 2 / n;
                    Vector3 p = at + (u * Mathf.Cos(a) + w * Mathf.Sin(a)) * r, t = -u * Mathf.Sin(a) + w * Mathf.Cos(a);
                    b.AddSegment(p - t * DotLength * .5f, p + t * DotLength * .5f, c, 1.8f, 0, id, .92f);
                }
            }
        }

        static void DottedCircle(LineMeshBuilder b, float id, Vector3 at, float radius, Color c, float widthPx, float intensity)
        {
            int n = Mathf.Clamp(Mathf.RoundToInt(radius * 3), 24, 160);
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2 / n;
                Vector3 p = at + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius, t = new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a));
                b.AddSegment(p - t * DotLength * .5f, p + t * DotLength * .5f, c, widthPx, 0, id, intensity);
            }
        }

        /// <summary>The cap table: a gold ring just outside the shell, one arc per named owner as long as its stake (founders
        /// brightest, sovereigns amber, companies copper, funds gold), the rest of the holders faint.</summary>
        void CapTable(LineMeshBuilder b, float id, ContestNetwork.Body body, float lit)
        {
            var stakes = model.Stakes.Where(s => s.Company == body.Id && !double.IsNaN(s.Pct) && s.Pct > 0).OrderByDescending(s => s.Pct).ToList();
            if (stakes.Count == 0) return;
            float r = body.R + 1.6f, at = Hash(body.Id) * Mathf.PI * 2, used = 0;
            Vector3 c = body.Pos + Vector3.up * body.R * .18f;
            foreach (var s in stakes)
            {
                float span = Mathf.Min(1 - used, (float)s.Pct / 100f) * Mathf.PI * 2;
                if (span <= .01f) break;
                var owner = model.OwnerById(s.Owner);
                Color col = OwnerColor(owner?.Kind ?? (net.Get(s.Owner)?.Kind == ContestNetwork.Kind.Company ? "corporate" : "fund"));
                Arc(b, id, c, r, at + .03f, at + span - .03f, WithAlpha(Color.Lerp(col, new Color(.6f, .55f, .45f), .3f), .85f * lit), 2.4f, 1.1f);
                at += span;
                used += span / (Mathf.PI * 2);
            }

            if (used < .98f) Arc(b, id, c, r, at + .03f, at + (1 - used) * Mathf.PI * 2 - .03f, WithAlpha(new Color(.6f, .55f, .45f), .35f * lit), 1.4f, 1);
        }

        public static Color OwnerColor(string kind) => kind switch
        {
            "person" => new Color(1f, .93f, .62f), "households" => new Color(1f, .88f, .5f), "sovereign" => new Color(1f, .62f, .25f),
            "corporate" => new Color(.85f, .55f, .35f), "pe" => new Color(.9f, .68f, .2f), "vc" => new Color(.9f, .68f, .2f), "hedge" => new Color(.9f, .68f, .2f),
            _ => EconomyStyle.Capital,
        };

        static void Arc(LineMeshBuilder b, float id, Vector3 c, float r, float a0, float a1, Color col, float widthPx, float intensity)
        {
            int n = Mathf.Max(3, Mathf.CeilToInt((a1 - a0) / (Mathf.PI / 20)));
            var pts = new List<LinePoint>(n + 1);
            for (int i = 0; i <= n; i++)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)n);
                pts.Add(new LinePoint(c + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * r, col, widthPx, 0, intensity));
            }

            b.AddPolyline(pts, id);
        }

        static float Hash(string s)
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (char ch in s) h = (h ^ ch) * 16777619;
                return (h & 0xFFFF) / 65535f;
            }
        }

        static readonly Color ChokeColor = new Color(1f, .93f, .75f);

        /// <summary>Where a line meets a body: a company's visible body, an owner's circle, a disc's rim.</summary>
        static float Edge(ContestNetwork.Body b) => b.Kind == ContestNetwork.Kind.Company && b.BodyR > 0 ? b.BodyR : b.R;

        float RulerX() => net.Bodies.Where(x => x.Kind == ContestNetwork.Kind.Company).Select(x => x.Pos.x + x.R).DefaultIfEmpty(ContestNetwork.X0).Max() + 26;

        /// <summary>The rivers: each flow a curved band between the two shells, in two lanes (rose desire, ice fear) where the
        /// payer's motive was judged, white-gold for capital, as wide as 0.35 √$bn a year; pulses toward the payee. The largest
        /// few are lit, the rest faint, a selection lights its own.</summary>
        void Rivers()
        {
            Drop(Part.Rivers);
            var b = new LineMeshBuilder();
            float id = EconomyEmphasis.Id(Keys[(int)Part.Rivers]);
            var flows = net.Links.Where(l => l.Kind == "flow" && l.A.Alive && l.B.Alive && (l.Flow == null || l.Flow.Start <= Year)).OrderByDescending(l => l.UsdBn).ToList();
            // by default, the story, hop by hop down the chain the user named: the labs' compute into the builders, the builders'
            // capex into the chips, the chips into the fabs, the fabs into the tools; capital into the labs; and the payouts that
            // rise to the owners. At least two rivers a hop, largest first; every other river waits for a selection.
            var story = new HashSet<ContestNetwork.Link>();
            foreach (var (from, to) in StoryHops)
                foreach (var l in flows.Where(l => Hop(l.A, from) && Hop(l.B, to)).Take(3)) story.Add(l);
            for (int i = 0; i < flows.Count; i++)
            {
                var l = flows[i];
                bool lit = Selected == null ? story.Contains(l) : l.A.Id == Selected || l.B.Id == Selected;
                if (!lit) continue;
                if (Focus >= 0 && Focus < Strata && l.A.Stratum != Focus && l.B.Stratum != Focus) continue;
                if (Above(l.A.Id) && Above(l.B.Id)) continue;
                float closeUp = Focus >= 0 && Focus < Strata ? .4f : 1;
                float alpha = .85f * closeUp;
                double usd = l.Flow != null ? l.Flow.In(Scenario) : l.UsdBn;
                River(b, id, l, usd, alpha, i);
            }

            var r = Lines(b, Part.Rivers, arena, 3184, true);
            r.sharedMaterial.SetFloat("_Flow", 1.3f);
            r.sharedMaterial.SetFloat("_FlowFreq", 3);
            r.sharedMaterial.SetFloat("_FlowSpeed", .35f);
            partRenderer[(int)Part.Rivers] = r;
        }

        void River(LineMeshBuilder b, float id, ContestNetwork.Link l, double usd, float alpha, int salt)
        {
            Vector3 a = l.A.Pos, z = l.B.Pos, d = z - a;
            float dist = d.magnitude;
            if (dist < 1) return;
            Vector3 dir = d / dist, p0 = a + dir * Edge(l.A) * .95f, p2 = z - dir * Edge(l.B) * .95f;
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            side = side.sqrMagnitude < .01f ? Vector3.right : side.normalized;
            Vector3 p1 = (p0 + p2) * .5f + Vector3.up * (dist * .12f) + side * (dist * .06f * ((salt & 1) == 0 ? 1 : -1));
            float width = Mathf.Clamp(.3f * Mathf.Sqrt((float)Math.Max(0, usd)), .4f, l.A.Kind == ContestNetwork.Kind.Buyer ? 3f : 7f);
            string kind = l.Flow?.Kind ?? "";
            bool capital = kind == "investment" || kind == "financing" || kind == "equity";
            bool owner = l.A.Kind == ContestNetwork.Kind.Owner || l.A.Kind == ContestNetwork.Kind.Wealth || l.B.Kind == ContestNetwork.Kind.Owner || l.B.Kind == ContestNetwork.Kind.Wealth;
            if (owner)
            {
                // the payouts that rise to the owners and the rounds that come down from them: thin, muted gold
                Lane(b, id, p0, p1, p2, side, 0, Mathf.Min(width, 1.4f), Color.Lerp(CapitalFlow, Gold, .3f), alpha * (Focus >= 0 ? .18f : .4f));
                return;
            }
            double fear = l.Flow != null ? l.Flow.Fear : double.NaN;
            if (capital || double.IsNaN(fear))
            {
                Lane(b, id, p0, p1, p2, side, 0, width, capital ? CapitalFlow : Neutral, alpha);
                return;
            }

            float f = Mathf.Clamp01((float)fear), wd = width * (1 - f), wf = width * f;
            if (wd > .05f) Lane(b, id, p0, p1, p2, side, -wf * .5f, wd, EconomyStyle.Desire, alpha);
            if (wf > .05f) Lane(b, id, p0, p1, p2, side, wd * .5f, wf, EconomyStyle.Fear, alpha);
        }

        static void Lane(LineMeshBuilder b, float id, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 side, float offset, float width, Color c, float alpha)
        {
            const int n = 18;
            var pts = new List<LinePoint>(n + 1);
            float length = 0;
            Vector3 last = p0;
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n, taper = Mathf.Clamp01(Mathf.Min(t, 1 - t) * 6);
                Vector3 p = (1 - t) * (1 - t) * p0 + 2 * (1 - t) * t * p1 + t * t * p2 + side * offset * taper;
                length += Vector3.Distance(last, p);
                last = p;
                pts.Add(new LinePoint(p, WithAlpha(c, alpha), 0, width * Mathf.Lerp(.35f, 1, taper), .92f));
            }

            b.AddFlowPath(pts, id, 1f / Mathf.Max(1, length) * 6);
        }

        /// <summary>The gold threads: from each owner's circle down to the organisms it holds (as wide as 0.12 √$bn of the
        /// stake), and between companies where one holds a stake in another; their pulses rise (dividends and buybacks go up
        /// to the owners). By default only the stakes in the largest companies, and every stake of a selected actor.</summary>
        void Threads()
        {
            Drop(Part.Threads);
            var b = new LineMeshBuilder();
            float id = EconomyEmphasis.Id(Keys[(int)Part.Threads]);
            var big = new HashSet<string>(net.Bodies.Where(x => x.Kind == ContestNetwork.Kind.Company && x.Alive).OrderByDescending(x => x.R).Take(ThreadCompanies).Select(x => x.Id));
            double Usd(ContestNetwork.Link l) => !double.IsNaN(l.UsdBn) ? l.UsdBn : l.Stake != null && !double.IsNaN(l.Stake.Pct) && !double.IsNaN(l.B.Value) ? l.B.Value * l.Stake.Pct / 100 : 5;
            var shown = new HashSet<ContestNetwork.Link>(net.Links.Where(l => (l.Kind == "owns" || l.Kind == "stake") && big.Contains(l.B.Id))
                .GroupBy(l => l.B.Id).SelectMany(g => g.OrderByDescending(Usd).Take(2)));
            foreach (var l in net.Links)
            {
                if (l.Kind != "owns" && l.Kind != "stake") continue;
                bool lit = Selected == null ? shown.Contains(l) || l.Kind == "stake" : l.A.Id == Selected || l.B.Id == Selected;
                if (!lit || Selected == null && Focus >= 0) continue;
                if (Above(l.B.Id) || l.A.Kind == ContestNetwork.Kind.Company && Above(l.A.Id)) continue;
                double usd = Usd(l);
                float width = Mathf.Max(.15f, .035f * Mathf.Sqrt((float)Math.Max(0, usd)));
                Vector3 from = l.B.Pos + Vector3.up * Edge(l.B), to = l.A.Kind == ContestNetwork.Kind.Company ? l.A.Pos + Vector3.down * Edge(l.A) * .2f : l.A.Pos;
                Vector3 mid = (from + to) * .5f + Vector3.up * 6;
                var pts = new List<LinePoint>(13);
                float length = 0;
                Vector3 last = from;
                for (int i = 0; i <= 12; i++)
                {
                    float t = i / 12f;
                    Vector3 p = (1 - t) * (1 - t) * from + 2 * (1 - t) * t * mid + t * t * to;
                    length += Vector3.Distance(last, p);
                    last = p;
                    pts.Add(new LinePoint(p, WithAlpha(Gold, Selected == null ? .4f : .8f), 0, width, .92f));
                }

                b.AddFlowPath(pts, id, 1f / Mathf.Max(1, length) * 4);
            }

            var r = Lines(b, Part.Threads, arena, 3185, true);
            r.sharedMaterial.SetFloat("_Flow", 1.2f);
            r.sharedMaterial.SetFloat("_FlowFreq", 2);
            r.sharedMaterial.SetFloat("_FlowSpeed", .3f);
            partRenderer[(int)Part.Threads] = r;
        }

        /// <summary>The fitness landscape: contours of the height field under the population every 10 units, faint, every
        /// fiftieth a little brighter.</summary>
        // ------------------------------------------------------------------ the strata

        /// <summary>A stratum's extent on its plane: its bodies and their advantage boundaries, with a margin (land-local x, z).</summary>
        Rect StratumRect(int i)
        {
            float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
            foreach (var b in net.Bodies)
            {
                if (b.Stratum != i || (!b.InStratum && b.Kind != ContestNetwork.Kind.State) || !b.Alive) continue;
                float r = Mathf.Max(b.R, b.Territory) + 10;
                x0 = Mathf.Min(x0, b.Pos.x - r); x1 = Mathf.Max(x1, b.Pos.x + r);
                z0 = Mathf.Min(z0, b.Pos.z - r); z1 = Mathf.Max(z1, b.Pos.z + r);
            }

            if (x0 > x1) return new Rect(ContestNetwork.StackX - 60, net.StackZ - 40, 120, 80);
            return Rect.MinMaxRect(x0, z0, x1, z1);
        }

        /// <summary>A stratum's tint: warm at the consumer's end, cool toward government (muted, under the bloom).</summary>
        static Color StratumColor(int i) => Color.Lerp(new Color(.95f, .78f, .55f), new Color(.55f, .70f, .95f), i / (float)(ContestNetwork.StratumOrder.Length - 1));

        /// <summary>The strata: each a plane at its height, drawn as a dotted rim around what stands in it and a sparse lattice
        /// of points across it, so the supply chain reads as layers (consumer products on top, government the floor).</summary>
        void Landscape()
        {
            Drop(Part.Landscape);
            var b = new LineMeshBuilder(16384);
            float id = EconomyEmphasis.Id(Keys[(int)Part.Landscape]);
            for (int i = 0; i < ContestNetwork.StratumOrder.Length; i++)
            {
                if (AboveStratum(i)) continue;
                Rect r = StratumRect(i);
                float y = net.StratumY[i];
                Color c = StratumColor(i);
                // the rim: dots every 3 units around a rounded rectangle
                float rad = Mathf.Min(18, Mathf.Min(r.width, r.height) * .3f);
                var rim = RoundedRect(r, rad, 3f);
                foreach (var q in rim) b.AddSegment(new Vector3(q.x, y, q.y), new Vector3(q.x, y + DotLength, q.y), WithAlpha(c, .42f), 1.8f, 0, id, .95f);
                // the lattice: one faint point every 12 units
                for (float x = r.xMin + 6; x < r.xMax - 4; x += 12)
                    for (float z = r.yMin + 6; z < r.yMax - 4; z += 12)
                        b.AddSegment(new Vector3(x, y, z), new Vector3(x, y + DotLength, z), WithAlpha(c, .12f), 1.4f, 0, id, .9f);
            }

            partRenderer[(int)Part.Landscape] = Lines(b, Part.Landscape, arena, 3181, false);
        }

        /// <summary>Points along a rounded rectangle (x, z in a Rect's x and y), one every <paramref name="step"/> units.</summary>
        static List<Vector2> RoundedRect(Rect r, float rad, float step)
        {
            var pts = new List<Vector2>();
            void Line(Vector2 a, Vector2 c)
            {
                float len = Vector2.Distance(a, c);
                for (float t = 0; t < len; t += step) pts.Add(Vector2.Lerp(a, c, t / len));
            }

            void Arc(Vector2 centre, float a0)
            {
                int n = Mathf.Max(2, Mathf.CeilToInt(rad * Mathf.PI / 2 / step));
                for (int k = 0; k < n; k++)
                {
                    float a = a0 + k * Mathf.PI / 2 / n;
                    pts.Add(centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rad);
                }
            }

            Line(new Vector2(r.xMin + rad, r.yMin), new Vector2(r.xMax - rad, r.yMin));
            Arc(new Vector2(r.xMax - rad, r.yMin + rad), -Mathf.PI / 2);
            Line(new Vector2(r.xMax, r.yMin + rad), new Vector2(r.xMax, r.yMax - rad));
            Arc(new Vector2(r.xMax - rad, r.yMax - rad), 0);
            Line(new Vector2(r.xMax - rad, r.yMax), new Vector2(r.xMin + rad, r.yMax));
            Arc(new Vector2(r.xMin + rad, r.yMax - rad), Mathf.PI / 2);
            Line(new Vector2(r.xMin, r.yMax - rad), new Vector2(r.xMin, r.yMin + rad));
            Arc(new Vector2(r.xMin + rad, r.yMin + rad), Mathf.PI);
            return pts;
        }

        /// <summary>Vertical integration: each company's column through the strata it works in, a dotted vein from its body to
        /// its farthest presence, and at each presence a small cloud in the company's colour (horizontal integration, its
        /// family of products there, is named in its label and the panel).</summary>
        void Columns()
        {
            Drop(Part.Columns);
            var b = new LineMeshBuilder(16384);
            float id = EconomyEmphasis.Id(Keys[(int)Part.Columns]);
            var (gd, _) = Land.Organism.Geodesic(2);
            foreach (var host in net.Bodies)
            {
                if (host.Kind != ContestNetwork.Kind.Company || !host.Alive) continue;
                var sats = net.Bodies.Where(x => x.Kind == ContestNetwork.Kind.Satellite && x.Host == host).ToList();
                if (sats.Count == 0) continue;
                float lit = Lit(host.Id);
                Color c = ColumnColor(host);
                float lo = Mathf.Min(host.Pos.y, sats.Min(x => x.Pos.y)), hi = Mathf.Max(host.Pos.y, sats.Max(x => x.Pos.y));
                for (float y = lo; y < hi; y += 2.6f)
                {
                    if (Mathf.Abs(y - host.Pos.y) < Edge(host)) continue;
                    if (Focus >= 0 && Focus < Strata && y > net.StratumY[Focus] + 60) continue;    // the cutaway
                    if (sats.Any(x => Mathf.Abs(y - x.Pos.y) < x.R)) continue;
                    b.AddSegment(new Vector3(host.Pos.x, y, host.Pos.z), new Vector3(host.Pos.x, y + 1.1f, host.Pos.z), WithAlpha(c, .55f * lit), 2f, 0, id, .93f);
                }

                foreach (var sat in sats.Where(x => !AboveStratum(x.Stratum)))
                    foreach (var d in gd)
                    {
                        Vector3 p = sat.Pos + d * sat.R;
                        b.AddSegment(p, p + Vector3.up * DotLength, WithAlpha(c, .75f * lit), 2.6f, 0, id, .93f);
                    }
            }

            partRenderer[(int)Part.Columns] = Lines(b, Part.Columns, arena, 3182, false);
        }

        /// <summary>A company's column colour: its equity's heat, as its core is drawn.</summary>
        static Color ColumnColor(ContestNetwork.Body host) =>
            Color.Lerp(Land.Organism.EquityHeat(host.State != null && !double.IsNaN(host.State.Eq) ? host.State.Eq : host.Actor.Sheet.Equity), Color.white, .15f);

        // ------------------------------------------------------------------ advantage boundaries

        public static readonly string[] SourceOrder = { "technology", "supplyChain", "patents", "capital", "talent", "scale", "distribution", "regulation" };

        public static string SourceName(string source) => source switch
        {
            "technology" => "Technology", "supplyChain" => "Supply", "patents" => "Patents", "capital" => "Share price and cash",
            "talent" => "Talent", "scale" => "Scale", "distribution" => "Distribution", "regulation" => "Regulation", _ => source,
        };

        public static Color SourceColor(string source) => source switch
        {
            "technology" => new Color(.40f, .85f, 1f), "supplyChain" => new Color(1f, .68f, .28f), "patents" => new Color(.74f, .56f, 1f),
            "capital" => new Color(1f, .86f, .40f), "talent" => new Color(1f, .48f, .64f), "scale" => new Color(.62f, .72f, .88f),
            "distribution" => new Color(.48f, .95f, .60f), "regulation" => new Color(.92f, .92f, .92f), _ => Color.white,
        };

        /// <summary>The height of a wall: as tall as Jev judged that source keeps rivals and entrants out (0-4).</summary>
        static float WallHeight(double wall) => 4 + 9 * (float)(double.IsNaN(wall) ? 1 : wall);

        /// <summary>The arc of a moat's circle that each source of its advantage holds: the sources in order around the
        /// territory, each an equal share of the circle.</summary>
        static (float a0, float a1) SourceArc(int k, int n) => (k * 2 * Mathf.PI / n + .06f, (k + 1) * 2 * Mathf.PI / n - .06f);

        /// <summary>The advantage boundaries (the user, 7 Nov 2026: "I need to see competitive advantage boundaries"): each
        /// dominant company's territory on its stratum, a ring as wide as its shell and its share of the market, fenced by a
        /// wall made of its sources of advantage, one arc each, each arc as tall as Jev judged that source keeps rivals and
        /// new entrants out, in the source's colour. Rivals stand outside (the layout keeps them there). And where talent and
        /// the share price are most of what keeps entrants out (Jev), a rose wall around the stratum's incumbents, with the
        /// entrants outside it and a dotted line from each one taken to whoever took it.</summary>
        void Walls()
        {
            Drop(Part.Walls);
            var b = new LineMeshBuilder(16384);
            float id = EconomyEmphasis.Id(Keys[(int)Part.Walls]);
            foreach (var moat in model.Moats)
            {
                var body = Holder(moat);
                if (body == null || !body.Alive || body.Territory <= 0 || OffFocus(body.Stratum)) continue;
                float lit = Lit(body.Id), ground = net.StratumY[Mathf.Max(0, body.Stratum)];
                // the territory: a dotted ring on the stratum's plane
                int n = Mathf.Clamp(Mathf.RoundToInt(body.Territory * 2.2f), 40, 220);
                for (int i = 0; i < n; i++)
                {
                    float a = i * 2 * Mathf.PI / n;
                    Vector3 p = new Vector3(body.Pos.x + Mathf.Cos(a) * body.Territory, ground, body.Pos.z + Mathf.Sin(a) * body.Territory),
                        t = new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a));
                    b.AddSegment(p - t * DotLength * .5f, p + t * DotLength * .5f, WithAlpha(GoldBright, .65f * lit), 2f, 0, id, .95f);
                }

                // the wall: one arc per source, posts as tall as the judged wall, a rail along their tops
                var srcs = moat.Sources.OrderBy(x => Array.IndexOf(SourceOrder, x.Source)).ToList();
                for (int k = 0; k < srcs.Count; k++)
                {
                    var (a0, a1) = SourceArc(k, srcs.Count);
                    float h = WallHeight(srcs[k].Wall);
                    Color c = WithAlpha(SourceColor(srcs[k].Source), .7f * lit);
                    int posts = Mathf.Max(3, Mathf.RoundToInt((a1 - a0) * body.Territory / 3.2f));
                    Vector3 prevTop = Vector3.zero;
                    for (int i = 0; i <= posts; i++)
                    {
                        float a = Mathf.Lerp(a0, a1, i / (float)posts);
                        Vector3 foot = new Vector3(body.Pos.x + Mathf.Cos(a) * body.Territory, ground, body.Pos.z + Mathf.Sin(a) * body.Territory), top = foot + Vector3.up * h;
                        for (float t = 0; t < h; t += 1.6f) b.AddSegment(foot + Vector3.up * t, foot + Vector3.up * (t + .7f), WithAlpha(c, c.a * .55f), 1.3f, 0, id, .92f);
                        if (i > 0) b.AddSegment(prevTop, top, c, 2.2f, 0, id, .95f);
                        prevTop = top;
                    }
                }
            }

            foreach (var st in model.Strata)
            {
                if (double.IsNaN(st.TalentCapital) || st.TalentCapital < 1.5 || OffFocus(st.Index)) continue;
                var ring = EntryRing(st);
                if (ring.r <= 0) continue;
                float h = WallHeight(st.Hard) * 1.4f;
                Color c = WithAlpha(SourceColor("talent"), .6f);
                int n = Mathf.Clamp(Mathf.RoundToInt(ring.r * 1.6f), 60, 260);
                Vector3 prevTop = Vector3.zero;
                for (int i = 0; i <= n; i++)
                {
                    float a = i * 2 * Mathf.PI / n;
                    Vector3 foot = new Vector3(ring.c.x + Mathf.Cos(a) * ring.r, ring.c.y, ring.c.z + Mathf.Sin(a) * ring.r), top = foot + Vector3.up * h;
                    if (i % 2 == 0)
                        for (float t = 0; t < h; t += 2.2f) b.AddSegment(foot + Vector3.up * t, foot + Vector3.up * (t + .8f), WithAlpha(c, .32f), 1.2f, 0, id, .9f);
                    if (i > 0) b.AddSegment(prevTop, top, c, 2.4f, 0, id, .95f);
                    prevTop = top;
                }

                // the entrants, outside the wall: taken ones joined to whoever took them
                foreach (var (e, at) in EntrantPlaces(st, ring))
                {
                    Color ec = e.Fate == "independent" || e.Fate == "public" ? new Color(.85f, .92f, 1f) : e.Fate == "failed" ? new Color(.6f, .6f, .62f) : SourceColor("talent");
                    foreach (var d in Land.Organism.Geodesic(1).dirs)
                        b.AddSegment(at + d * 2.6f, at + d * 2.6f + Vector3.up * DotLength, WithAlpha(ec, .85f), 2.4f, 0, id, .95f);
                    var by = e.By != null ? net.Get(e.By) : null;
                    if (by != null && by.Alive && e.Fate != "independent" && e.Fate != "public")
                    {
                        Vector3 to = by.Pos, dir = (to - at).normalized;
                        float len = Vector3.Distance(at, to) - Edge(by);
                        for (float t = 3; t < len; t += 3f) b.AddSegment(at + dir * t, at + dir * (t + 1.4f), WithAlpha(ec, .5f), 1.4f, 0, id, .92f);
                    }
                }
            }

            partRenderer[(int)Part.Walls] = Lines(b, Part.Walls, arena, 3187, false);
        }

        /// <summary>Who holds a boundary in its stratum: the actor's own body there, or its satellite (its column's presence).</summary>
        ContestNetwork.Body Holder(Advantage.Moat moat)
        {
            var body = net.Get(moat.Actor);
            if (body == null) return null;
            int si = Array.IndexOf(ContestNetwork.StratumOrder, moat.Stratum);
            return body.Stratum == si ? body : net.Get(moat.Actor + "@" + moat.Stratum);
        }

        /// <summary>The ring of a stratum's incumbents (the companies whose advantage boundaries stand there, and the giants
        /// working there): its centre on the stratum's plane and its radius around them all.</summary>
        (Vector3 c, float r) EntryRing(Advantage.Stratum st)
        {
            int si = Array.IndexOf(ContestNetwork.StratumOrder, st.Id);
            if (si < 0) return (Vector3.zero, 0);
            var inside = net.Bodies.Where(x => x.Stratum == si && x.InStratum && x.Alive &&
                                               (x.Territory > 0 || x.Kind == ContestNetwork.Kind.Satellite || x.R >= 22)).ToList();
            if (inside.Count == 0) return (Vector3.zero, 0);
            Vector3 c = Vector3.zero;
            foreach (var x in inside) c += x.Pos;
            c /= inside.Count;
            c.y = net.StratumY[si];
            float r = 0;
            foreach (var x in inside) r = Mathf.Max(r, new Vector2(x.Pos.x - c.x, x.Pos.z - c.z).magnitude + Mathf.Max(x.R, x.Territory));
            return (c, r + 8);
        }

        /// <summary>Where each of a stratum's entrants stands: just outside its incumbents' wall, spread around it.</summary>
        List<(Advantage.Entrant e, Vector3 at)> EntrantPlaces(Advantage.Stratum st, (Vector3 c, float r) ring)
        {
            var list = new List<(Advantage.Entrant, Vector3)>();
            var es = model.Entrants.Where(e => e.Stratum == st.Id && net.Get(e.Id) == null).ToList();
            for (int k = 0; k < es.Count; k++)
            {
                float a = (k + .5f) * 2 * Mathf.PI / Mathf.Max(1, es.Count) + .3f;
                list.Add((es[k], ring.c + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (ring.r + 12)));
            }

            return list;
        }

        /// <summary>The present contests: a burst on the holder's face toward its living rival, coloured by the strongest of the
        /// triad (judged), with a short dashed tether toward the rival; the full bolt between them only for a selected actor.</summary>
        void Sparks()
        {
            Drop(Part.Sparks);
            var b = new LineMeshBuilder();
            float id = EconomyEmphasis.Id(Keys[(int)Part.Sparks]);
            foreach (var fp in ShownSparks())
            {
                var h = net.Get(fp.Holder);
                var r = LiveRival(fp);
                if (h == null || r == null) continue;
                Color c = TriadColor(fp.Triad);
                var seed = new System.Random(unchecked((int)Fnv(fp.Id ?? fp.Title)));
                Vector3 dir = (r.Pos - h.Pos).normalized, m = h.Pos + dir * (Edge(h) + 2);
                for (int k = 0; k < 8; k++)
                {
                    float ang = k * Mathf.PI / 4 + (float)seed.NextDouble() * .3f;
                    Vector3 ray = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang) * .8f, Mathf.Sin(ang * 1.7f) * .5f).normalized * (k % 2 == 0 ? 3.2f : 1.8f);
                    b.AddSegment(m, m + ray, c, 2.2f, 0, id, .95f);
                }

                float tether = Mathf.Min(15, Vector3.Distance(m, r.Pos) - Edge(r));
                for (float t = 2; t < tether; t += 2.5f) b.AddSegment(m + dir * t, m + dir * (t + 1.2f), WithAlpha(c, .6f), 1.4f, 0, id, .9f);
                if (Selected != null && (fp.Holder == Selected || fp.Rivals.Contains(Selected)))
                {
                    Vector3 a = h.Pos + Vector3.up * Edge(h), z = r.Pos + Vector3.up * Edge(r);
                    Bolt(b, id, SparkAt(a, z), a, c, seed);
                    Bolt(b, id, SparkAt(a, z), z, c, seed);
                }
            }

            var sr = Lines(b, Part.Sparks, arena, 3189, true);
            sr.sharedMaterial.SetFloat("_Flow", 1.6f);
            sr.sharedMaterial.SetFloat("_FlowFreq", 1.5f);
            sr.sharedMaterial.SetFloat("_FlowSpeed", .9f);
            partRenderer[(int)Part.Sparks] = sr;
        }

        /// <summary>A contest's first rival still alive in the world shown.</summary>
        ContestNetwork.Body LiveRival(Advantage.Flashpoint fp) => fp.Rivals.Select(x => net.Get(x)).FirstOrDefault(x => x != null && x.Alive && x.Id != fp.Holder);

        IEnumerable<Advantage.Flashpoint> ShownSparks()
        {
            var present = model.Flashpoints.Where(f => f.InArena && f.Year >= 2023 && net.Get(f.Holder) is ContestNetwork.Body h && h.Alive && LiveRival(f) != null).ToList();
            if (Selected != null) return present.Where(f => f.Holder == Selected || f.Rivals.Contains(Selected));
            return present.OrderByDescending(f => f.Sources.Length).ThenByDescending(f => f.Year).Take(SparkDefault);
        }

        /// <summary>The chain's hops, as (payer, payee) groups: a layer, "capital" for investors and owners, "owners" for the
        /// golden circles (payouts).</summary>
        static readonly (string from, string to)[] StoryHops =
            { ("models", "clouds"), ("clouds", "chips"), ("chips", "fabs"), ("fabs", "tools"), ("capital", "models"), ("payer", "owners") };

        static bool Hop(ContestNetwork.Body b, string group) => group switch
        {
            "capital" => b.Kind == ContestNetwork.Kind.Owner || b.Actor?.Flank == "capital",
            "owners" => b.Kind == ContestNetwork.Kind.Owner || b.Kind == ContestNetwork.Kind.Wealth,
            "payer" => b.Kind == ContestNetwork.Kind.Buyer || b.Kind == ContestNetwork.Kind.Company,
            _ => b.Kind == ContestNetwork.Kind.Company && b.Layer == group,
        };

        static Vector3 SparkAt(Vector3 a, Vector3 z) => Vector3.Lerp(a, z, .5f) + Vector3.up * (4 + .06f * Vector3.Distance(a, z));

        static void Bolt(LineMeshBuilder b, float id, Vector3 from, Vector3 to, Color c, System.Random seed)
        {
            var pts = new List<LinePoint>(9);
            for (int i = 0; i <= 8; i++)
            {
                float t = i / 8f;
                Vector3 p = Vector3.Lerp(from, to, t);
                if (i > 0 && i < 8) p += new Vector3((float)seed.NextDouble() - .5f, (float)seed.NextDouble() - .5f, (float)seed.NextDouble() - .5f) * 2.4f;
                pts.Add(new LinePoint(p, WithAlpha(c, Mathf.Lerp(1, .35f, t)), 1.8f, 0, .95f));
            }

            b.AddFlowPath(pts, id, 1f / Mathf.Max(1, Vector3.Distance(from, to)) * 3);
        }

        public static Color TriadColor(Dictionary<string, double> triad)
        {
            if (triad == null || triad.Count == 0) return Color.white;
            double i = triad.TryGetValue("information", out double x) ? x : 0, e = triad.TryGetValue("execution", out double y) ? y : 0,
                n = triad.TryGetValue("environment", out double z) ? z : 0;
            return i >= e && i >= n ? InfoColor : e >= n ? ExecColor : EnvColor;
        }

        // ------------------------------------------------------------------ history: the lineage's lane and its contests

        public const float SpineX = 212, SpineY = 16;

        static Vector3 OnSpine(double year) => new Vector3(SpineX, SpineY, HillLandscape.Z(year));

        List<Advantage.Step> Spine()
        {
            var reach = new HashSet<string> { "nvidia", "openai", "anthropic" };
            var spine = new List<Advantage.Step>();
            foreach (var step in model.Lineage.Where(x => x.Year >= 1947 && x.Year <= 2026).OrderByDescending(x => x.Year))
            {
                if (!reach.Contains(step.To) && !reach.Contains(step.From)) continue;
                spine.Add(step);
                reach.Add(step.From);
                reach.Add(step.To);
            }

            spine.Reverse();
            return spine;
        }

        bool OnWave(string actor, int year, out Vector3 at)
        {
            at = default;
            var lives = CompanyLives.Get();
            string life = model.Get(actor)?.Lifeline ?? actor;
            if (lives != null)
                for (int i = 0; i < lives.Lives.Length; i++)
                    if (lives.Lives[i].Id == life && HillWaveLayer.CompanyAtYear(i, year + .5, out at))
                    {
                        at += Vector3.up * 1.5f;
                        return true;
                    }

            return false;
        }

        /// <summary>The lineage's gold lane along the wave from 1947 to the crest, then up to NVIDIA's organism; beads at its
        /// steps; sparks at the century's contests on the companies' lifelines. Under the layer itself (the opening does not
        /// stretch the past).</summary>
        void Lineage()
        {
            Drop(Part.Thread);
            threadBeads.Clear();
            historySparks.Clear();
            var b = new LineMeshBuilder();
            float id = EconomyEmphasis.Id(Keys[(int)Part.Thread]);
            var spine = Spine();
            var top = net.Get("nvidia");
            if (spine.Count > 0 && top != null)
            {
                var onSpine = new HashSet<string>();
                foreach (var step in spine)
                {
                    onSpine.Add(step.From);
                    onSpine.Add(step.To);
                    threadBeads.Add((OnSpine(step.Year + .5), step));
                }

                var pts = new List<Vector3> { OnSpine(1947) };
                Vector3 crest = new Vector3(SpineX, SpineY, net.CrestZ - .5f), summit = top.Pos + Vector3.up * top.R;
                pts.Add(crest);
                for (int k = 1; k <= 12; k++)
                {
                    float t = k / 12f;
                    Vector3 p = Vector3.Lerp(crest, summit, t);
                    p.y += Mathf.Sin(t * Mathf.PI) * 14;
                    pts.Add(p);
                }

                var line = new List<LinePoint>(pts.Count);
                float length = 0;
                for (int i = 0; i < pts.Count; i++)
                {
                    if (i > 0) length += Vector3.Distance(pts[i - 1], pts[i]);
                    line.Add(new LinePoint(pts[i], ThreadColor, 2.2f, 0, .95f));
                }

                b.AddFlowPath(line, id, 1f / Mathf.Max(1, length) * 40);
                foreach (var (at, _) in threadBeads) Circle(b, id, at, 1.3f, Vector3.up, ThreadColor, 2, .95f);
                foreach (var fp in model.Flashpoints.Where(f => f.Year >= 1947 && f.Year < 2023))
                {
                    Vector3 at;
                    if (OnWave(fp.Holder, fp.Year, out Vector3 h))
                    {
                        at = h;
                        foreach (string r in fp.Rivals)
                            if (OnWave(r, fp.Year, out Vector3 rv)) { at = Vector3.Lerp(h, rv, .5f) + Vector3.up * 2; break; }
                    }
                    else if (onSpine.Contains(fp.Holder)) at = OnSpine(fp.Year + .5) + new Vector3(-7, 3, 0);
                    else continue;
                    Color c = TriadColor(fp.Triad);
                    for (int k = 0; k < 6; k++)
                    {
                        float ang = k * Mathf.PI / 3;
                        b.AddSegment(at, at + new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0) * 1.6f, c, 1.8f, 0, id, .95f);
                    }

                    historySparks.Add((at, fp));
                }
            }

            var tr = Lines(b, Part.Thread, transform, 3190, true);
            tr.sharedMaterial.SetFloat("_Flow", 1.4f);
            tr.sharedMaterial.SetFloat("_FlowFreq", 2);
            tr.sharedMaterial.SetFloat("_FlowSpeed", .5f);
            partRenderer[(int)Part.Thread] = tr;
        }

        /// <summary>The lineage's steps on the lane (the panel lists them).</summary>
        public static List<Advantage.Step> SpineSteps => current != null ? current.threadBeads.Select(x => x.step).ToList() : new List<Advantage.Step>();

        readonly List<(Vector3 at, Advantage.Step step)> threadBeads = new List<(Vector3, Advantage.Step)>();
        readonly List<(Vector3 at, Advantage.Flashpoint fp)> historySparks = new List<(Vector3, Advantage.Flashpoint)>();

        // ------------------------------------------------------------------ labels

        void MakeLabels()
        {
            labels.Clear();
            var named = new HashSet<string>(net.Bodies.Where(x => x.Kind == ContestNetwork.Kind.Company).OrderByDescending(x => x.Actor.Sized ? x.Actor.SizeUsdBn : 0).Take(NameLabels).Select(x => x.Id));
            bool simulated = Scenario != Advantage.Now && Year > 2026;
            // the largest owner of each kind is always named, so sovereigns, venture capital and hedge funds are seen
            var leaders = new HashSet<string>(net.Bodies.Where(x => x.Kind == ContestNetwork.Kind.Owner && x.Owner != null)
                .GroupBy(x => x.Owner.Kind).Select(g => g.OrderByDescending(x => x.Value).First().Id));
            var chokes = new HashSet<string>(model.Chokes.Select(c => c.Actor));
            named.UnionWith(chokes);
            if (Selected != null) named.Add(Selected);
            var info = model.ScenarioById(Scenario);
            if (info != null) { foreach (var x in info.Friendly.Take(3)) named.Add(x); foreach (var x in info.Unfriendly.Take(3)) named.Add(x); }
            foreach (var body in net.Bodies)
            {
                switch (body.Kind)
                {
                    case ContestNetwork.Kind.Company:
                        if (!named.Contains(body.Id) && Focus != body.Stratum || History || Above(body.Id) || Below(body.Id)) continue;
                        // a simulated year's numbers carry ≈: they are the model's, not measured
                        string sim = body.State != null ? "≈" : "";
                        string held = simulated && model.NotSimulated.Contains(body.Id) ? " (2026)" : "";
                        string value = !body.Alive ? "" : double.IsNaN(body.Value) ? " <i>value unverified</i>" : " " + sim + Money(body.Value) + held;
                        double pe = body.State != null ? body.State.Pe : body.Actor.Pe;
                        bool lossNow = body.State != null ? body.State.Ni < 0 : body.Actor.LossEvidence;
                        string peText = !body.Alive ? "" : !double.IsNaN(pe) && pe > 0 ? " · P/E " + sim + pe.ToString("0") : lossNow ? " · loss" : "";
                        string fate = body.Alive ? "" : " " + FateText(model, Scenario, body.Id, Year, true);
                        labels.Add(new HillLabel
                        {
                            Text = "<b>" + body.Name + "</b>" + value + peText + fate, At = body.Pos + Vector3.up * ((body.BodyR > 0 ? body.BodyMax.y : body.R) + 2.5f), Role = LabelRole.Name, Part = "advantage.organs",
                            Rank = body.R + (body.Id == Selected ? 1000 : 0) + (hotChokes.Contains(body.Id) ? 500 : chokes.Contains(body.Id) ? 300 : 0), Plate = new Color(0, 0, 0, .85f),
                            // the population is packed: a name whose place is taken steps just above or below, then aside
                            Fallbacks = NameFallbacks,
                        });
                        break;
                    case ContestNetwork.Kind.Owner:
                    case ContestNetwork.Kind.Wealth:
                        if (History || body.Kind == ContestNetwork.Kind.Owner && body.Value < 150 && Selected == null && !leaders.Contains(body.Id)) continue;
                        bool leader = leaders.Contains(body.Id);
                        string kindTag = body.Kind == ContestNetwork.Kind.Owner && leader ? " · " + OwnerKindWord(body.Owner?.Kind) : "";
                        labels.Add(new HillLabel
                        {
                            Text = body.Name + kindTag + " " + (simulated ? "≈" : "") + Money(body.Value), At = body.Pos + Vector3.up * (body.R + 2.5f),
                            Role = body.Kind == ContestNetwork.Kind.Wealth || leader ? LabelRole.Name : LabelRole.Note,
                            Part = "advantage.rings", Rank = (leader ? 250 : 0) + (float)Math.Log10(1 + body.Value), Color = body.Kind == ContestNetwork.Kind.Wealth ? GoldBright : Gold,
                        });
                        break;
                    case ContestNetwork.Kind.Buyer:
                    case ContestNetwork.Kind.State:
                        labels.Add(new HillLabel { Text = body.Name.ToUpperInvariant(), At = body.Pos + Vector3.up * 3.5f, Role = LabelRole.Note, Part = "advantage.organs", Rank = 1, Color = Roles.Encoded(Roles.InkDim) });
                        break;
                }
            }

            foreach (var fp in ShownSparks().Take(6))
            {
                var h = net.Get(fp.Holder);
                var r = LiveRival(fp);
                if (h == null || r == null) continue;
                Vector3 m = h.Pos + (r.Pos - h.Pos).normalized * (Edge(h) + 2);
                labels.Add(new HillLabel { Text = fp.Title, At = m + Vector3.up * 4, Role = LabelRole.Note, Part = "advantage.sparks", Rank = 3, Color = TriadColor(fp.Triad) });
            }

            // the strata, named at their left ends (from the future, the left of the screen is +x)
            for (int i = 0; i < ContestNetwork.StratumOrder.Length && !History; i++)
            {
                if (OffFocus(i)) continue;
                var st = model.StratumById(ContestNetwork.StratumOrder[i]);
                Rect r = StratumRect(i);
                string entry = st != null && !double.IsNaN(st.Hard) ? " · entry " + EntryWord(st.Hard) : "";
                labels.Add(new HillLabel
                {
                    Text = "<b>" + (st?.Name ?? ContestNetwork.StratumOrder[i]).ToUpperInvariant() + "</b>" + entry,
                    At = new Vector3(r.xMax + 6, net.StratumY[i], r.center.y), Role = LabelRole.Note, Part = "advantage.landscape", Rank = 450,
                    Color = StratumColor(i), Plate = new Color(0, 0, 0, .7f),
                });
            }

            // the advantage boundaries' sources, named with their evidence and Jev's judged wall: NVIDIA's (the user's example)
            // always, and the selected company's
            foreach (var moat in model.Moats)
            {
                var body = Holder(moat);
                if (body == null || !body.Alive || body.Territory <= 0 || History || OffFocus(body.Stratum)) continue;
                // NVIDIA's (the user's example) always; in a stratum's close-up, every boundary there; the selected company's
                bool shown = moat.Actor == "nvidia" || moat.Actor == Selected || Focus == body.Stratum;
                var srcs = moat.Sources.OrderBy(x => Array.IndexOf(SourceOrder, x.Source)).ToList();
                float ground = net.StratumY[Mathf.Max(0, body.Stratum)];
                for (int k = 0; k < srcs.Count && shown; k++)
                {
                    var (a0, a1) = SourceArc(k, srcs.Count);
                    float a = (a0 + a1) * .5f;
                    Vector3 at = new Vector3(body.Pos.x + Mathf.Cos(a) * body.Territory, ground + WallHeight(srcs[k].Wall) + 2, body.Pos.z + Mathf.Sin(a) * body.Territory);
                    labels.Add(new HillLabel
                    {
                        Text = "<b>" + SourceName(srcs[k].Source) + "</b> " + Words(srcs[k].What, 9) + (double.IsNaN(srcs[k].Wall) ? "" : " <i>· wall " + srcs[k].Wall.ToString("0.0") + "</i>"),
                        At = at, Role = LabelRole.Note, Part = "advantage.walls", Rank = 420 + (float)(double.IsNaN(srcs[k].Wall) ? 0 : srcs[k].Wall), Color = SourceColor(srcs[k].Source),
                        Plate = new Color(0, 0, 0, .75f), Fallbacks = NameFallbacks,
                    });
                }
            }

            // the walls against entrants, and the entrants with their fates
            foreach (var st in model.Strata)
            {
                if (History || double.IsNaN(st.TalentCapital) || st.TalentCapital < 1.5 || OffFocus(st.Index)) continue;
                var ring = EntryRing(st);
                if (ring.r <= 0) continue;
                labels.Add(new HillLabel
                {
                    Text = "<b>Talent and share price</b>: " + ShareWord(st.TalentCapital) + " of what keeps entrants out of " + st.Name.ToLowerInvariant() + " <i>(judged)</i>",
                    At = ring.c + new Vector3(0, WallHeight(st.Hard) * 1.4f + 3, -ring.r), Role = LabelRole.Note, Part = "advantage.walls", Rank = 440,
                    Color = SourceColor("talent"), Plate = new Color(0, 0, 0, .75f), Fallbacks = NameFallbacks,
                });
                foreach (var (e, at) in EntrantPlaces(st, ring))
                    labels.Add(new HillLabel
                    {
                        Text = e.Name + " <i>· " + FateWord(e, net.Get(e.By)?.Name ?? e.By) + "</i>", At = at + Vector3.up * 3.5f, Role = LabelRole.Note,
                        Part = "advantage.walls", Rank = 300, Color = new Color(.86f, .88f, .92f),
                    });
            }

            // the selected company's column: what it does in each stratum it works in
            foreach (var sat in net.Bodies.Where(x => x.Kind == ContestNetwork.Kind.Satellite && x.Host.Id == Selected && x.Alive && !History))
                labels.Add(new HillLabel
                {
                    Text = sat.Name + " · " + Words(sat.Presence?.What, 8), At = sat.Pos + Vector3.up * (sat.R + 2), Role = LabelRole.Note,
                    Part = "advantage.columns", Rank = 800, Color = ColumnColor(sat.Host), Plate = new Color(0, 0, 0, .75f), Fallbacks = NameFallbacks,
                });
            for (int i = 0; i < threadBeads.Count && History; i++)
            {
                var (at, step) = threadBeads[i];
                string who = step.From == step.To || string.IsNullOrEmpty(step.FromName) ? step.ToName : step.FromName + " → " + step.ToName;
                labels.Add(new HillLabel { Text = step.Year + " · " + who, At = at + Vector3.up * 3, Role = LabelRole.Note, Part = "advantage.thread", Rank = 2.5f, Color = ThreadColor });
            }

            foreach (var (at, fp) in historySparks.Where(_ => History))
                labels.Add(new HillLabel { Text = fp.Year + " " + fp.Title, At = at + Vector3.up * 3.5f, Role = LabelRole.Note, Part = "advantage.thread", Rank = 1.5f, Color = TriadColor(fp.Triad) });
            // the bodies write depth (their hidden solids): the names must not be cut by the bodies in front
            foreach (var l in labels) l.OnTop = true;
        }

        /// <summary>How and when a company left a world by a year: "† extinct 2028" or "→ absorbed by Microsoft, 2028" (the
        /// first simulated year it is not alive), or "" while it lives.</summary>
        public static string FateText(Advantage data, string world, string id, int year, bool rich)
        {
            var w = data?.WorldById(world);
            if (w == null) return "";
            foreach (int y in w.Years.Keys)
            {
                if (y > year) break;
                var st = w.At(id, y);
                if (st == null || st.Alive) continue;
                string text = st.Status == "extinct" ? "† extinct " + y : "→ absorbed by " + (data.Get(st.By)?.Name ?? st.By) + ", " + y;
                return rich ? (st.Status == "extinct" ? "<color=#F59A9A>" : "<color=#C9CED6>") + text + "</color>" : text;
            }

            return "";
        }

        static string OwnerKindWord(string kind) => kind switch
        {
            "fund" => "index and asset manager", "sovereign" => "sovereign fund", "pe" => "private capital", "vc" => "venture capital",
            "hedge" => "hedge fund", "corporate" => "company", _ => kind ?? "owner",
        };

        static string EntryWord(double hard) => hard >= 3.5 ? "closed" : hard >= 2.75 ? "nearly closed" : hard >= 2 ? "very hard" : hard >= 1 ? "hard" : "open";

        static string ShareWord(double tc) => tc >= 3.5 ? "almost all" : tc >= 2.5 ? "most" : tc >= 1.5 ? "about half" : tc >= .5 ? "a little" : "none";

        static string FateWord(Advantage.Entrant e, string by) => e.Fate switch
        {
            "acquired" => "acquired by " + by, "licence-and-hire" => "licensed and hired by " + by, "failed" => "failed",
            "public" => "listed", _ => "independent",
        };

        static string Words(string s, int n)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var w = s.Split(' ');
            return w.Length <= n ? s : string.Join(" ", w.Take(n)) + "…";
        }

        static readonly Vector2[] NameFallbacks = { new Vector2(0, 18), new Vector2(0, -18), new Vector2(0, 36), new Vector2(0, -36), new Vector2(120, 0), new Vector2(-120, 0) };

        public static string Money(double bn) => double.IsNaN(bn) ? "" : bn >= 1000 ? "$" + (bn / 1000).ToString("0.0") + "T" : bn >= 10 ? "$" + bn.ToString("0") + "B" : "$" + bn.ToString("0.0") + "B";

        void ShowLabels(bool show)
        {
            if (show == labelsShown) return;
            labelsShown = show;
            if (show) HillLabels.Set(this, labels);
            else HillLabels.Clear(this);
        }

        // ------------------------------------------------------------------ hover and click

        void Pick(Camera camera)
        {
            Hovered = null;
            if (!PointerInput.Available || EventSystem.current && EventSystem.current.IsPointerOverGameObject()) return;
            Vector2 pointer = PointerInput.Position;
            float best = float.MaxValue, min = 22 * camera.pixelHeight / 1080f;
            foreach (var body in net.Bodies)
            {
                if (body.Kind == ContestNetwork.Kind.State) continue;
                Vector3 screen = camera.WorldToScreenPoint(transform.TransformPoint(body.Pos));
                if (screen.z <= 0) continue;
                // a shell answers anywhere inside its projected radius; the nearest centre wins
                Vector3 rim = camera.WorldToScreenPoint(transform.TransformPoint(body.Pos + camera.transform.right * body.R));
                float reach = Mathf.Max(min, Vector2.Distance(rim, screen));
                float d = Vector2.Distance(pointer, screen);
                if (d < reach && d < best) { best = d; Hovered = body.Id; }
            }

            if (PointerInput.PressedThisFrame) pressed = pointer;
            if (PointerInput.ReleasedThisFrame && Vector2.Distance(pressed, pointer) < 5) Select(Hovered == Selected ? null : Hovered);
        }

        // ------------------------------------------------------------------ checks and the smoke sequence

        void Log()
        {
            int companies = net.Bodies.Count(b => b.Kind == ContestNetwork.Kind.Company), owners = net.Bodies.Count(b => b.Kind == ContestNetwork.Kind.Owner || b.Kind == ContestNetwork.Kind.Wealth);
            int sheets = net.Bodies.Count(b => b.Kind == ContestNetwork.Kind.Company && b.Actor.Sheet.Known);
            float overlap = net.Overlap(out string pair), alt = net.AltitudeError();
            Debug.Log($"[Why] advantage {model.AsOf}: {companies} organisms ({sheets} with a balance sheet), {owners} owners, {net.Links.Count} links " +
                      $"({net.Links.Count(l => l.Kind == "flow")} rivers, {net.Links.Count(l => l.Kind != "flow")} stakes), {model.Worlds.Count} simulated worlds; " +
                      $"world {Scenario} {Year}: worst shell overlap {overlap:0.0} ({pair}), worst altitude error {alt:0.0}, energy {net.Energy:0.0000}");
            // the overlap is measured by each body's widest drawn reach (lumpy, so a few units of touching do not show)
            if (overlap > 4f) Debug.LogError($"[Why] advantage: bodies interpenetrate by {overlap:0.0} ({pair})");
            if (alt > 3) Debug.LogError($"[Why] advantage: an organism stands {alt:0.0} units off its altitude");
            if (companies == 0) Debug.LogError("[Why] advantage: no organisms");
            foreach (var a in model.Actors)
                if (a.Sized && !model.Sources.ContainsKey(a.SizeFact ?? ""))
                    Debug.LogError($"[Why] advantage: {a.Id}'s size has no verified source ({a.SizeFact})");
        }

        /// <summary>
        /// <c>-smokeextra advantage</c>: the network now (economy-advantage-now) with its checks; a harsh world's last year
        /// (capex digestion, 2030: how many organisms changed fate or ground) with the live settling timed; the history
        /// camera; one organism selected; the choreography. Renderers at most <see cref="MaxRenderers"/>.
        /// </summary>
        IEnumerator Smoke()
        {
            GraphRoot root = GraphRoot.Instance;
            if (root == null) yield break;
            root.Focus(View, 0f);
            for (float waited = 0; waited < 10 && (content == null || land != HillLandscapeLayer.Current); waited += Time.unscaledDeltaTime) yield return null;
            if (content == null)
            {
                Debug.LogError("[Why] advantage: the layer built nothing in 10 s");
                yield break;
            }

            SetScenario(Advantage.Now);
            instantMoves = false;
            yield return new WaitForSecondsRealtime(1.5f);
            Log();
            yield return SmokeChecks.Capture("economy-advantage-now", root.CurrentPreset);
            int active = 0;
            foreach ((Renderer r, Part _) in renderers) if (r && r.enabled && r.gameObject.activeInHierarchy) active++;
            int companies = net.Bodies.Count(b => b.Kind == ContestNetwork.Kind.Company);
            int bodies = organisms.Values.Count(o => o.renderer && o.renderer.enabled && o.renderer.gameObject.activeInHierarchy);
            int vertices = organisms.Values.Sum(o => o.renderer ? o.renderer.GetComponent<MeshFilter>().sharedMesh.vertexCount : 0);
            Debug.Log($"[Why] advantage: {active} part renderers active (at most {MaxRenderers}); {bodies} organisms (at most {companies}), {vertices} vertices in them (at most {MaxOrganismVertices})");
            if (active > MaxRenderers) Debug.LogError($"[Why] advantage: {active} part renderers active, more than {MaxRenderers}");
            if (bodies > companies) Debug.LogError($"[Why] advantage: {bodies} organisms drawn for {companies} companies");
            if (vertices > MaxOrganismVertices) Debug.LogError($"[Why] advantage: the organisms hold {vertices} vertices, more than {MaxOrganismVertices}");

            // the strata up close: NVIDIA's boundary in chips, the wall of talent and share price in models, then the whole stack
            foreach (var (focus, name) in new[] { (3, "chips"), (1, "models"), (0, "consumer"), (Strata, "hill") })
            {
                FocusOn(focus);
                yield return new WaitForSecondsRealtime(2.2f);
                yield return SmokeChecks.Capture("economy-advantage-" + name, root.CurrentPreset);
            }

            FocusOn(-1);
            yield return new WaitForSecondsRealtime(1.5f);
            int walls = model.Moats.Count(m => Holder(m) is ContestNetwork.Body mb && mb.Alive && mb.Territory > 0);
            int columns = net.Bodies.Count(x => x.Kind == ContestNetwork.Kind.Satellite);
            Debug.Log($"[Why] advantage strata: {walls} advantage boundaries, {columns} presences in other strata, {model.Entrants.Count} entrants; strata at " +
                      string.Join(", ", net.StratumY.Select(y => y.ToString("0"))));
            if (walls == 0) Debug.LogError("[Why] advantage: no advantage boundary is drawn");
            if (columns == 0) Debug.LogError("[Why] advantage: no company shows its presence in other strata");

            // play from now steps through the years (it once jumped from 2026 to 2030)
            Playing = true;
            yield return new WaitForSecondsRealtime(YearSeconds * 1.5f);
            int played = Year;
            Playing = false;
            Debug.Log($"[Why] advantage play: {played} after {YearSeconds * 1.5f:0.0} s from 2026 ({Scenario})");
            if (played != 2027) Debug.LogError($"[Why] advantage: play reached {played}, not 2027, after one year's time");
            SetScenario(Advantage.Now);
            for (float t = 0; t < 7.5f && settling; t += Time.unscaledDeltaTime) yield return null;

            string harsh = model.WorldById("digestion") != null ? "digestion" : Worlds().Count > 2 ? Worlds()[2] : null;
            if (harsh != null)
            {
                var before = net.Bodies.Where(b => b.Kind == ContestNetwork.Kind.Company).ToDictionary(b => b.Id, b => (b.Value, b.Alive));
                float longest = 0;
                SetScenario(harsh);
                SetYear(2030);
                for (float t = 0; t < 7.5f && settling; t += Time.unscaledDeltaTime)
                {
                    yield return null;
                    longest = Mathf.Max(longest, Time.unscaledDeltaTime);
                }

                int moved = net.Bodies.Count(b => b.Kind == ContestNetwork.Kind.Company && before.TryGetValue(b.Id, out var was) &&
                                                  (was.Alive != b.Alive || !double.IsNaN(was.Value) && !double.IsNaN(b.Value) && Math.Abs(b.Value - was.Value) > .1 * was.Value));
                int dead = net.Bodies.Count(b => b.Kind == ContestNetwork.Kind.Company && !b.Alive);
                Debug.Log($"[Why] advantage world {harsh} 2030: {moved} organisms changed value (over 10%) or fate, {dead} absorbed or extinct; longest frame {1000 * longest:0} ms");
                Log();
                if (model.Worlds.Count > 0 && moved < 5) Debug.LogError($"[Why] advantage: world {harsh} moved {moved} organisms, fewer than 5");
                if (longest > .05f) Debug.LogWarning($"[Why] advantage: a settling frame took {1000 * longest:0} ms, over 50");
                yield return new WaitForSecondsRealtime(1.2f);    // the labels fade back in once the population settles
                yield return SmokeChecks.Capture("economy-advantage-" + harsh + "-2030", root.CurrentPreset);
                SetScenario(Advantage.Now);
                for (float t = 0; t < 7.5f && settling; t += Time.unscaledDeltaTime) yield return null;
            }

            ShowHistory(true);
            yield return new WaitForSecondsRealtime(2.4f);
            Debug.Log($"[Why] advantage history: {threadBeads.Count} steps on the lane, {historySparks.Count} contests on the wave");
            if (threadBeads.Count == 0) Debug.LogError("[Why] advantage: the lineage lane has no steps");
            yield return SmokeChecks.Capture("economy-advantage-history", root.CurrentPreset);
            ShowHistory(false);
            yield return new WaitForSecondsRealtime(2f);

            string pick = net.Get("nvidia") != null ? "nvidia" : net.Bodies.FirstOrDefault(b => b.Kind == ContestNetwork.Kind.Company)?.Id;
            Select(pick);
            yield return new WaitForSecondsRealtime(.8f);
            yield return SmokeChecks.Capture("economy-advantage-selected", root.CurrentPreset);
            Select(null);
            float slowest = 0;
            Play(true);
            while (direction != 0)
            {
                yield return null;
                slowest = Mathf.Max(slowest, Time.unscaledDeltaTime);
            }

            instantMoves = true;
            Debug.Log($"[Why] advantage choreography max frame {1000 * slowest:0} ms");
            if (slowest > .05f) Debug.LogWarning($"[Why] advantage: a choreography frame took {1000 * slowest:0} ms, over 50");
            yield return new WaitForSecondsRealtime(1f);
        }

        // ------------------------------------------------------------------ meshes

        static readonly List<Vector3[]> Ico0 = Icosphere(0), Ico1 = Icosphere(1);

        /// <summary>The faces of an icosahedron subdivided <paramref name="levels"/> times, on the unit sphere.</summary>
        static List<Vector3[]> Icosphere(int levels)
        {
            float t = (1 + Mathf.Sqrt(5)) / 2;
            var v = new[]
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0), new Vector3(0, -1, t), new Vector3(0, 1, t),
                new Vector3(0, -1, -t), new Vector3(0, 1, -t), new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            int[] f = { 0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8, 3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1 };
            var faces = new List<Vector3[]>();
            for (int i = 0; i < f.Length; i += 3) faces.Add(new[] { v[f[i]].normalized, v[f[i + 1]].normalized, v[f[i + 2]].normalized });
            for (int l = 0; l < levels; l++)
            {
                var next = new List<Vector3[]>(faces.Count * 4);
                foreach (var tri in faces)
                {
                    Vector3 a = tri[0], b = tri[1], c = tri[2], ab = ((a + b) * .5f).normalized, bc = ((b + c) * .5f).normalized, ca = ((c + a) * .5f).normalized;
                    next.Add(new[] { a, ab, ca }); next.Add(new[] { b, bc, ab }); next.Add(new[] { c, ca, bc }); next.Add(new[] { ab, bc, ca });
                }

                faces = next;
            }

            return faces;
        }

        /// <summary>A sphere of flat-shaded facets (or evenly shaded, for the shells) at a detail of 0 (20 faces) or 1 (80).</summary>
        static void Sphere(SurfaceMeshBuilder b, float id, Vector3 c, float r, Color color, int detail, float intensity, bool flat = true)
        {
            foreach (var f in detail > 0 ? Ico1 : Ico0)
            {
                Vector3 n = (f[0] + f[1] + f[2]).normalized;
                Color col = color;
                if (flat)
                {
                    float shade = .4f + .6f * Mathf.Max(0, Vector3.Dot(n, Light));
                    col = color * shade;
                    col.a = color.a;
                }

                b.AddTriangle(c + f[0] * r, c + f[2] * r, c + f[1] * r, col, id, intensity);
            }
        }

        /// <summary>A box (what a company built): six flat-shaded faces.</summary>
        static void Box(SurfaceMeshBuilder b, float id, Vector3 c, float h, Color color)
        {
            Vector3[] n = { Vector3.up, Vector3.down, Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
            foreach (var nn in n)
            {
                Vector3 u = Mathf.Abs(nn.y) > .5f ? Vector3.right : Vector3.up, w = Vector3.Cross(nn, u);
                Vector3 o = c + nn * h, p0 = o + (u + w) * h, p1 = o + (u - w) * h, p2 = o + (-u - w) * h, p3 = o + (-u + w) * h;
                Color col = color * (.4f + .6f * Mathf.Max(0, Vector3.Dot(nn, Light)));
                col.a = 1;
                b.AddTriangle(p0, p1, p2, col, id);
                b.AddTriangle(p0, p2, p3, col, id);
            }
        }

        /// <summary>An octahedron (what a company bought: goodwill and intangibles).</summary>
        static void Octahedron(SurfaceMeshBuilder b, float id, Vector3 c, float r, Color color)
        {
            Vector3[] v = { Vector3.up, Vector3.down, Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
            int[] f = { 0, 2, 4, 0, 4, 3, 0, 3, 5, 0, 5, 2, 1, 4, 2, 1, 3, 4, 1, 5, 3, 1, 2, 5 };
            for (int i = 0; i < f.Length; i += 3)
            {
                Vector3 a = v[f[i]], bb = v[f[i + 1]], cc = v[f[i + 2]], n = (a + bb + cc).normalized;
                Color col = color * (.4f + .6f * Mathf.Max(0, Vector3.Dot(n, Light)));
                col.a = 1;
                b.AddTriangle(c + a * r, c + cc * r, c + bb * r, col, id, 1.1f);
            }
        }

        /// <summary>A flat disc on the low ground (the buyers' plains, the states' plaques), its top lit, its side shaded.</summary>
        static void Disc(SurfaceMeshBuilder b, float id, Vector3 at, float r, Color color, float h)
        {
            Color top = color * .95f, side = color * .6f;
            top.a = side.a = 1;
            const int n = 28;
            for (int i = 0; i < n; i++)
            {
                float a0 = i * Mathf.PI * 2 / n, a1 = (i + 1) * Mathf.PI * 2 / n;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * r, d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * r, up = Vector3.up * h;
                b.AddTriangle(at + up, at + d1 + up, at + d0 + up, top, id);
                b.AddTriangle(at + d0, at + d0 + up, at + d1, side, id);
                b.AddTriangle(at + d1, at + d0 + up, at + d1 + up, side, id);
            }
        }

        static void Circle(LineMeshBuilder b, float id, Vector3 at, float radius, Vector3 normal, Color c, float widthPx, float intensity, bool dashed = false)
        {
            const int n = 40;
            Vector3 u = Vector3.Cross(normal, Mathf.Abs(normal.y) > .9f ? Vector3.right : Vector3.up).normalized, w = Vector3.Cross(normal, u).normalized;
            var pts = new List<LinePoint>(n + 1);
            for (int i = 0; i <= n; i++)
            {
                float a = i * Mathf.PI * 2 / n;
                pts.Add(new LinePoint(at + (u * Mathf.Cos(a) + w * Mathf.Sin(a)) * radius, dashed && (i / 2) % 2 == 1 ? WithAlpha(c, 0) : c, widthPx, 0, intensity));
            }

            b.AddPolyline(pts, id);
        }

        static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }

        Material SurfaceMaterial(int queue)
        {
            Material m = GraphMaterials.Raw(GraphMaterials.Surface(Color.white, 1, queue));
            m.SetFloat("_EdgeSoft", 0);
            owned.Add(m);
            return m;
        }

        MeshRenderer Surface(SurfaceMeshBuilder b, Part part, Transform parent, int queue, bool solid = true)
        {
            Material m = SurfaceMaterial(queue);
            if (solid) m.SetFloat("_ZWrite", 1);
            return AddRenderer(b.ToMesh("Advantage " + part), m, part, parent, "Advantage " + part);
        }

        MeshRenderer Lines(LineMeshBuilder b, Part part, Transform parent, int queue, bool additive)
        {
            Material m = GraphMaterials.Raw(GraphMaterials.Line(Color.white, 1, queue, additive));
            owned.Add(m);
            return AddRenderer(b.ToMesh("Advantage " + part), m, part, parent, "Advantage " + part);
        }

        MeshRenderer AddRenderer(Mesh mesh, Material material, Part part, Transform parent, string name)
        {
            owned.Add(mesh);
            MeshRenderer r = AddMesh(name, mesh, material);
            r.transform.SetParent(parent, false);
            renderers.Add((r, part));
            return r;
        }

        /// <summary>Destroys a part's renderer, its mesh and its material (a rebuild replaces them about 15 times a second
        /// while the population moves).</summary>
        void Drop(Part part)
        {
            var r = partRenderer[(int)part];
            partRenderer[(int)part] = null;
            if (!r) return;
            renderers.RemoveAll(x => x.renderer == r);
            var mf = r.GetComponent<MeshFilter>();
            if (mf && mf.sharedMesh) { owned.Remove(mf.sharedMesh); Destroy(mf.sharedMesh); }
            if (r.sharedMaterial) { owned.Remove(r.sharedMaterial); Destroy(r.sharedMaterial); }
            Destroy(r.gameObject);
        }

        void Clear()
        {
            foreach (var kv in organisms) if (kv.Value.renderer) Destroy(kv.Value.renderer.gameObject);
            organisms.Clear();
            occluders.Clear();
            occluderMaterial = null;
            organismQueue.Clear();
            outlineShapes.Clear();
            foreach (UnityEngine.Object o in owned) if (o) Destroy(o);
            if (content) Destroy(content);
            owned.Clear();
            renderers.Clear();
            labels.Clear();
            Array.Clear(partRenderer, 0, partRenderer.Length);
            content = null;
            net = null;
            if (labelsShown) HillLabels.Clear(this);
            labelsShown = false;
        }

        void OnDestroy()
        {
            Clear();
            HillLabels.Clear(this);
            if (current == this) current = null;
        }
    }
}
