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

        enum Part { Organs, Shells, Rings, Rivers, Threads, Landscape, Sparks, Thread, Count }

        static readonly string[] PartNames = { "organs", "shells", "rings", "rivers", "threads", "landscape", "sparks", "thread" };
        static readonly int[] Keys = EconomyEmphasis.Keys("advantage", PartNames);

        public const float Seconds = 5f, ReverseSeconds = 1.8f;
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

        static readonly Color Gold = EconomyStyle.Capital, GoldBright = new Color(1f, .88f, .50f), Neutral = new Color(.75f, .71f, .67f), CapitalFlow = new Color(1f, .93f, .78f),
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
            if (id == Advantage.Now) Year = 2026;
            else if (w != null && Year < w.FirstYear) Year = w.FirstYear;
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
            Selected = id;
            if (current != null) current.dirty = true;
        }

        public static void ShowHistory(bool history)
        {
            if (history == History && current != null) return;
            History = history;
            Fly(history ? HillPresets.AdvantageHistory() : ViewPresets.Get(View), 1.6f);
            if (current != null) current.dirty = true;
        }

        public static HillPresets.ViewKey Key { get; } = new HillPresets.ViewKey(new (string, Color)[]
            {
                ("Equity", Land.Organism.Equity), ("Cash", Land.Organism.Cash), ("What it built", Land.Organism.Built), ("What it bought", Land.Organism.Bought),
                ("Debt", Land.Organism.Debt), ("Advantage frontier", FrontierColor), ("Owners", Gold),
            },
            "Each company is one body made of its balance sheet: equity at its core, what it owns swelling above, what it owes sagging below; solid where it is built, " +
            "ghostly where it was bought. The dust around it is speculation, out to its market value. It glows only where its advantage presses on others. Height is market position.");

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
            EconomyEmphasis.RegisterView(View, PartNames.Select(p => "advantage." + p), null, new[] { "wave.crest", "wave.face", "wave.lives" }, synthesis: true);
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
                if (!HillRebuilds.Take()) return;
                using (LayerClock.Measure("advantage: build")) Build();
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

            Apply();
            if (here && direction == 0 && !settling && rig != null && rig.Cam != null) Pick(rig.Cam);
            ShowLabels(here && direction >= 0 && Reveal(clock) > .5f && !settling);
        }

        void PlayYears()
        {
            playClock += GraphTime.Delta;
            if (playClock < YearSeconds) return;
            playClock = 0;
            if (Scenario == Advantage.Now) SetScenario(Advantage.Y2030);
            if (Year >= 2030) { Playing = false; return; }
            SetYear(Year + 1);
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
            foreach ((Renderer r, Part part) in renderers)
            {
                float a = EconomyEmphasis.Get(Keys[(int)part]);
                if (part != Part.Organs && part != Part.Shells && part != Part.Landscape) a *= reveal;
                if (part == Part.Landscape) a *= rise;
                EconomyEmphasis.Set(r, a);
            }

            float organ = EconomyEmphasis.Get(Keys[(int)Part.Organs]);
            foreach (var kv in organisms)
            {
                if (!kv.Value.renderer) continue;
                var body = net.Get(kv.Key);
                float fade = body == null || body.Alive ? 1 : body.State != null && body.State.Status == "extinct" ? .2f : .35f;
                EconomyEmphasis.Set(kv.Value.renderer, organ * fade * Lit(kv.Key));
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
            Organisms(true);
            Rebuild(true);
            Log();
        }

        /// <summary>Every mesh from the layout's present positions (the landscape only when asked: it is the costly one).</summary>
        void Rebuild(bool landscape)
        {
            Organs();
            PlaceOrganisms();
            Rings();
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
                budget -= body.R > 14 ? 3 : 1;
            }
        }

        /// <summary>Where a company's advantage presses (its frontiers): toward those who pay it at a choke point (as hard as
        /// the choke is hard to replace, times its judged advantage), toward the rivals it is beating in a present contest,
        /// and, more softly, toward the holder of a contest it is losing.</summary>
        List<Organism.Front> Fronts(ContestNetwork.Body body)
        {
            var fronts = new List<Organism.Front>();
            float adv = body.Actor != null && !double.IsNaN(body.Actor.A) ? Mathf.Clamp01((float)body.Actor.A / 6f) : .4f;
            // a choke point: one front, toward the dollar-weighted middle of those who must pay it
            foreach (var c in model.Chokes.Where(c => c.Actor == body.Id).OrderByDescending(c => double.IsNaN(c.Strength) ? 0 : c.Strength).Take(1))
            {
                Vector3 sum = Vector3.zero;
                foreach (var l in net.Links)
                    if (l.Kind == "flow" && l.B == body && l.A != body) sum += (l.A.Pos - body.Pos).normalized * Mathf.Sqrt((float)Math.Max(1, l.UsdBn));
                if (sum.sqrMagnitude > 1e-4f) fronts.Add(new Organism.Front(sum.normalized, (double.IsNaN(c.Strength) ? .6f : (float)c.Strength) * adv));
            }

            // the contests it is winning now: a front toward each of its two strongest rivals
            var won = model.Flashpoints.Where(fp => fp.InArena && fp.Year >= 2023 && fp.Holder == body.Id).OrderByDescending(fp => fp.Sources.Length).Take(2);
            foreach (var fp in won)
            {
                var rv = fp.Rivals.Select(r => net.Get(r)).FirstOrDefault(x => x != null && x != body);
                if (rv != null) fronts.Add(new Organism.Front((rv.Pos - body.Pos).normalized, .7f * adv));
            }

            return fronts;
        }

        void Organism(ContestNetwork.Body body)
        {
            var fronts = Fronts(body);
            string key = Scenario + "|" + Year + "|" + body.Value.ToString("0.0") + "|" + body.Alive + "|" + string.Join(",", fronts.Select(f => (f.Direction * 10).ToString("0") + f.Strength.ToString("0.0")));
            if (organisms.TryGetValue(body.Id, out var have) && have.key == key && have.renderer) return;
            if (have.renderer) DropRenderer(have.renderer);
            int detail = body.R > 14 ? 3 : 2;
            var shape = new Organism(body, fronts, detail);
            var b = new LineMeshBuilder(shape.Points.Length * 3 + shape.Edges.Length * 2);
            float id = EconomyEmphasis.Id(Keys[(int)Part.Organs]);
            // the wire: each edge as opaque as the parts there are tangible, hot where it meets a frontier
            for (int e = 0; e < shape.Edges.Length; e += 2)
            {
                int i = shape.Edges[e], j = shape.Edges[e + 1];
                float f = Mathf.Max(shape.Frontier[i], shape.Frontier[j]), solid = (shape.Solidity[i] + shape.Solidity[j]) * .5f;
                Color c = Color.Lerp(shape.Colors[i], shape.Colors[j], .5f);
                c = Color.Lerp(c, FrontierColor, f * .8f);
                c.a = Mathf.Clamp01(.05f + .26f * solid + .6f * f) * (shape.Known ? 1 : .6f);
                b.AddSegment(shape.Points[i], shape.Points[j], c, 1f + 1.1f * f, 0, id, .85f + 4.2f * f * f);
            }

            // the points: a dot at each vertex, brighter at the frontier
            for (int i = 0; i < shape.Points.Length; i++)
            {
                float f = shape.Frontier[i];
                Color c = Color.Lerp(shape.Colors[i], FrontierColor, f * .85f);
                c.a = Mathf.Clamp01(.35f + .3f * shape.Solidity[i] + .35f * f);
                Vector3 p = shape.Points[i];
                b.AddSegment(p, p + p.normalized * .06f, c, 2.1f + 2.2f * f, 0, id, .9f + 5f * f * f);
            }

            float rb = shape.MeanRadius;
            var rnd = new System.Random(body.Id.GetHashCode());
            // speculation: a dust of points between what its profits justify (the earnings shell, at the reference P/E) and its
            // market value, as many as the premium's cube root allows; the market value itself a dotted membrane
            if (!double.IsNaN(body.Value) && body.R > rb + .3f)
            {
                Color pc = Color.Lerp(PeColor(body), new Color(.55f, .57f, .62f), .45f);
                float inner = Mathf.Max(rb, body.EarningsR);
                double premium = body.Value - Math.Max(body.Actor.Sheet.Equity > 0 ? body.Actor.Sheet.Equity : 0, double.IsNaN(body.EarningsValue) ? 0 : body.EarningsValue);
                int dust = premium > 0 ? Mathf.Clamp((int)(Math.Pow(premium, 1.0 / 3) * 26), 12, 620) : 0;
                for (int k = 0; k < dust; k++)
                {
                    Vector3 d = RandomDir(rnd);
                    float u = (float)rnd.NextDouble(), rr = Mathf.Lerp(inner, body.R, Mathf.Pow(u, .55f));
                    Vector3 p = d * rr;
                    b.AddSegment(p, p + d * .05f, WithAlpha(pc, .2f + .18f * u), 1.5f, 0, id, .9f);
                }

                var (dirs, _) = Land.Organism.Geodesic(2);
                foreach (var d in dirs)
                    b.AddSegment(d * body.R, d * (body.R + .06f), WithAlpha(pc, .3f), 1.3f, 0, id, 1);
                if (body.EarningsR > rb + .5f && body.EarningsR < body.R - .5f)
                {
                    var (ed, _) = Land.Organism.Geodesic(1);
                    foreach (var d in ed) b.AddSegment(d * body.EarningsR, d * (body.EarningsR + .05f), WithAlpha(new Color(.80f, .84f, .92f), .35f), 1.2f, 0, id, 1);
                }
            }

            // promises (purchase and compute commitments): a ghost lobe of points beneath, joined by a dotted stalk
            foreach (var part in ContestNetwork.PartsOf(body))
            {
                if (part.Kind != ContestNetwork.PartKind.Promises || part.Radius <= 0) continue;
                var (gd, _) = Land.Organism.Geodesic(2);
                foreach (var d in gd) b.AddSegment(part.Offset + d * part.Radius, part.Offset + d * (part.Radius + .06f), WithAlpha(PromiseColor * .8f, .38f), 1.5f, 0, id, 1.05f);
                for (int k = 1; k < 8; k++)
                {
                    Vector3 p = Vector3.Lerp(Vector3.down * rb, part.Offset + Vector3.up * part.Radius, k / 8f);
                    b.AddSegment(p, p + Vector3.down * .3f, WithAlpha(PromiseColor * .8f, .4f), 1.4f, 0, id, 1);
                }
            }

            // a loss paid for by others' money: a comet of points from the body toward the market it buys, its head hot
            double burn = body.Actor.Burn;
            if (double.IsNaN(burn) && body.State != null && body.State.Ni < 0) burn = -body.State.Ni;
            if (burn > 0)
            {
                var market = net.Get("consumers") ?? net.Get("enterprises");
                Vector3 dir = market != null ? (market.Pos - body.Pos).normalized : new Vector3(.8f, .35f, -.45f).normalized;
                float len = 3 + 3.2f * Mathf.Pow((float)burn, 1f / 3);
                int n = 36;
                for (int k = 0; k < n; k++)
                {
                    float t = k / (float)(n - 1), spread = t * 1.4f;
                    Vector3 p = dir * (rb + len * t) + RandomDir(rnd) * spread * (float)rnd.NextDouble();
                    b.AddSegment(p, p + dir * .2f, WithAlpha(BurnColor, (1 - t) * .75f), Mathf.Lerp(2.6f, 1.2f, t), 0, id, Mathf.Lerp(3.2f, 1f, t));
                }
            }

            var mesh = b.ToMesh("Organism " + body.Id);
            Material m = GraphMaterials.Raw(GraphMaterials.Line(Color.white, 1, 3183, true));
            owned.Add(m);
            owned.Add(mesh);
            MeshRenderer mr = AddMesh("Organism " + body.Id, mesh, m);
            mr.transform.SetParent(arena, false);
            mr.transform.localPosition = body.Pos;
            organisms[body.Id] = (mr, key);
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

        /// <summary>The frontier's colour: a white heat with gold in it (the only intense bloom in the view).</summary>
        static readonly Color FrontierColor = new Color(1f, .86f, .58f);

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

        float Lit(string id) => Selected == null || id == Selected || Linked(id) ? 1 : .35f;

        bool Linked(string id)
        {
            if (Selected == null) return true;
            foreach (var l in net.Links)
                if ((l.A.Id == Selected && l.B.Id == id) || (l.B.Id == Selected && l.A.Id == id)) return true;
            return false;
        }

        /// <summary>The ownership class and the ruler: the owners' golden circles (muted gold, the wealth groups' brighter), each
        /// company's cap table as gold arcs around it, and the altitude ruler from the low ground to the high.</summary>
        void Rings()
        {
            Drop(Part.Rings);
            var b = new LineMeshBuilder();
            float id = EconomyEmphasis.Id(Keys[(int)Part.Rings]);
            foreach (var body in net.Bodies)
            {
                float lit = Lit(body.Id);
                if (body.Kind == ContestNetwork.Kind.Company)
                {
                    if (body.Alive) CapTable(b, id, body, lit);
                }
                else if (body.Kind == ContestNetwork.Kind.Owner || body.Kind == ContestNetwork.Kind.Wealth)
                {
                    bool wealth = body.Kind == ContestNetwork.Kind.Wealth;
                    Color c = WithAlpha(Color.Lerp(wealth ? GoldBright : Gold, new Color(.6f, .55f, .45f), .35f), (wealth ? .9f : .7f) * lit);
                    DottedCircle(b, id, body.Pos, body.R, c, wealth ? 2.4f : 1.8f, wealth ? 1.25f : 1.05f);
                    Circle(b, id, body.Pos, body.R, Vector3.up, WithAlpha(c, .25f * lit), 1f, 1);
                }
            }

            // the low ground: a dotted hairline where workers and consumers stand, and the state's beneath it
            float gx0 = net.Bodies.Where(x => x.Kind == ContestNetwork.Kind.Company).Select(x => x.Pos.x - x.R).DefaultIfEmpty(ContestNetwork.X0 - 200).Min() - 10;
            float gx1 = net.Bodies.Where(x => x.Kind == ContestNetwork.Kind.Company).Select(x => x.Pos.x + x.R).DefaultIfEmpty(ContestNetwork.X0 + 200).Max() + 10;
            float gz = net.CrestZ + (ContestNetwork.ZFrom + ContestNetwork.ZTo) * .5f;
            foreach (var (y, a) in new[] { (0f, .22f), (ContestNetwork.StateY, .16f) })
                for (float x = gx0; x <= gx1; x += 3) b.AddSegment(new Vector3(x, y, gz), new Vector3(x + .05f, y, gz), WithAlpha(Ruler, a), 1.2f, 0, id, 1);

            // the altitude ruler: low ground to high ground, at the near left of the population
            float rx = RulerX(), rz = net.CrestZ + ContestNetwork.ZTo - 20;
            b.AddSegment(new Vector3(rx, 0, rz), new Vector3(rx, ContestNetwork.MaxAlt, rz), WithAlpha(Ruler, .4f), 1.2f, 0, id, 1);
            for (int y = 0; y <= ContestNetwork.MaxAlt; y += 25)
                b.AddSegment(new Vector3(rx - 2.5f, y, rz), new Vector3(rx + 2.5f, y, rz), WithAlpha(Ruler, .5f), 1.2f, 0, id, 1);
            partRenderer[(int)Part.Rings] = Lines(b, Part.Rings, arena, 3188, true);
        }

        static void DottedCircle(LineMeshBuilder b, float id, Vector3 at, float radius, Color c, float widthPx, float intensity)
        {
            int n = Mathf.Clamp(Mathf.RoundToInt(radius * 3), 24, 160);
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2 / n;
                Vector3 p = at + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius;
                b.AddSegment(p, p + Vector3.up * .05f, c, widthPx, 0, id, intensity);
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

        float RulerX() => net.Bodies.Where(x => x.Kind == ContestNetwork.Kind.Company).Select(x => x.Pos.x - x.R).DefaultIfEmpty(ContestNetwork.X0).Min() - 26;

        /// <summary>The rivers: each flow a curved band between the two shells, in two lanes (rose desire, ice fear) where the
        /// payer's motive was judged, white-gold for capital, as wide as 0.35 √$bn a year; pulses toward the payee. The largest
        /// few are lit, the rest faint, a selection lights its own.</summary>
        void Rivers()
        {
            Drop(Part.Rivers);
            var b = new LineMeshBuilder();
            float id = EconomyEmphasis.Id(Keys[(int)Part.Rivers]);
            var flows = net.Links.Where(l => l.Kind == "flow" && l.A.Alive && l.B.Alive).OrderByDescending(l => l.UsdBn).ToList();
            // by default, the story: money converging on the supply chain (tools, fabs, chips, clouds, models) and capital into
            // the labs, largest first; the buyers' spending stays faint until a company is chosen
            var story = new HashSet<ContestNetwork.Link>(flows.Where(l => l.A.Kind != ContestNetwork.Kind.Buyer && StoryLayers.Contains(l.B.Layer ?? "")).Take(RiverHighlights));
            for (int i = 0; i < flows.Count; i++)
            {
                var l = flows[i];
                bool lit = Selected == null ? story.Contains(l) : l.A.Id == Selected || l.B.Id == Selected;
                float alpha = lit ? .9f : Selected == null ? .16f : .06f;
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
            Vector3 dir = d / dist, p0 = a + dir * l.A.R * .95f, p2 = z - dir * l.B.R * .95f;
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            side = side.sqrMagnitude < .01f ? Vector3.right : side.normalized;
            Vector3 p1 = (p0 + p2) * .5f + Vector3.up * (dist * .12f) + side * (dist * .06f * ((salt & 1) == 0 ? 1 : -1));
            float width = Mathf.Clamp(.22f * Mathf.Sqrt((float)Math.Max(0, usd)), .3f, 6f);
            string kind = l.Flow?.Kind ?? "";
            bool capital = kind == "investment" || kind == "financing" || kind == "equity";
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
                pts.Add(new LinePoint(p, WithAlpha(c, alpha), 0, width * Mathf.Lerp(.35f, 1, taper), 1.25f));
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
                if (!lit) continue;
                double usd = Usd(l);
                float width = Mathf.Max(.15f, .035f * Mathf.Sqrt((float)Math.Max(0, usd)));
                Vector3 from = l.B.Pos + Vector3.up * l.B.R, to = l.A.Kind == ContestNetwork.Kind.Company ? l.A.Pos + Vector3.down * l.A.R * .2f : l.A.Pos;
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
                    pts.Add(new LinePoint(p, WithAlpha(Gold, Selected == null ? .45f : .8f), 0, width, 1.25f));
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
        void Landscape()
        {
            Drop(Part.Landscape);
            var b = new LineMeshBuilder();
            float id = EconomyEmphasis.Id(Keys[(int)Part.Landscape]);
            foreach (var (p, q, level) in net.Contours(8, 10))
            {
                if (level < 20) continue;
                bool major = Mathf.Abs(level % 50) < .01f;
                Color c = WithAlpha(Color.Lerp(Contour, GoldBright, level / ContestNetwork.MaxAlt * .5f), major ? .2f : .08f);
                b.AddSegment(p, q, c, major ? 1.3f : 1f, 0, id, 1);
            }

            partRenderer[(int)Part.Landscape] = Lines(b, Part.Landscape, arena, 3181, true);
        }

        /// <summary>The present contests: a spark between the shells of holder and rival, coloured by the strongest of the
        /// triad (judged). The selected actor's, else the best-sourced few.</summary>
        void Sparks()
        {
            Drop(Part.Sparks);
            var b = new LineMeshBuilder();
            float id = EconomyEmphasis.Id(Keys[(int)Part.Sparks]);
            foreach (var fp in ShownSparks())
            {
                var h = net.Get(fp.Holder);
                Color c = TriadColor(fp.Triad);
                var seed = new System.Random(fp.Id.GetHashCode());
                foreach (string rid in fp.Rivals)
                {
                    var r = net.Get(rid);
                    if (h == null || r == null || !h.Alive || !r.Alive) continue;
                    Vector3 a = h.Pos + Vector3.up * h.R, z = r.Pos + Vector3.up * r.R, m = SparkAt(a, z);
                    Bolt(b, id, m, a, c, seed);
                    Bolt(b, id, m, z, c, seed);
                    for (int k = 0; k < 8; k++)
                    {
                        float ang = k * Mathf.PI / 4 + (float)seed.NextDouble() * .3f;
                        Vector3 ray = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang) * .8f, Mathf.Sin(ang * 1.7f) * .5f).normalized * (k % 2 == 0 ? 3.2f : 1.8f);
                        b.AddSegment(m, m + ray, c, 2.2f, 0, id, 1.7f);
                    }
                }
            }

            var sr = Lines(b, Part.Sparks, arena, 3189, true);
            sr.sharedMaterial.SetFloat("_Flow", 1.6f);
            sr.sharedMaterial.SetFloat("_FlowFreq", 1.5f);
            sr.sharedMaterial.SetFloat("_FlowSpeed", .9f);
            partRenderer[(int)Part.Sparks] = sr;
        }

        IEnumerable<Advantage.Flashpoint> ShownSparks()
        {
            var present = model.Flashpoints.Where(f => f.InArena && f.Year >= 2023 && net.Get(f.Holder) != null).ToList();
            if (Selected != null) return present.Where(f => f.Holder == Selected || f.Rivals.Contains(Selected));
            return present.OrderByDescending(f => f.Sources.Length).ThenByDescending(f => f.Year).Take(SparkDefault);
        }

        static readonly HashSet<string> StoryLayers = new HashSet<string> { "tools", "fabs", "chips", "clouds", "models" };

        static Vector3 SparkAt(Vector3 a, Vector3 z) => Vector3.Lerp(a, z, .5f) + Vector3.up * (4 + .06f * Vector3.Distance(a, z));

        static void Bolt(LineMeshBuilder b, float id, Vector3 from, Vector3 to, Color c, System.Random seed)
        {
            var pts = new List<LinePoint>(9);
            for (int i = 0; i <= 8; i++)
            {
                float t = i / 8f;
                Vector3 p = Vector3.Lerp(from, to, t);
                if (i > 0 && i < 8) p += new Vector3((float)seed.NextDouble() - .5f, (float)seed.NextDouble() - .5f, (float)seed.NextDouble() - .5f) * 2.4f;
                pts.Add(new LinePoint(p, WithAlpha(c, Mathf.Lerp(1, .35f, t)), 1.8f, 0, Mathf.Lerp(1.5f, 1f, t)));
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
                    line.Add(new LinePoint(pts[i], ThreadColor, 2.2f, 0, 1.5f));
                }

                b.AddFlowPath(line, id, 1f / Mathf.Max(1, length) * 40);
                foreach (var (at, _) in threadBeads) Circle(b, id, at, 1.3f, Vector3.up, ThreadColor, 2, 1.8f);
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
                        b.AddSegment(at, at + new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0) * 1.6f, c, 1.8f, 0, id, 1.6f);
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
            var named = new HashSet<string>(net.Bodies.Where(x => x.Kind == ContestNetwork.Kind.Company).OrderByDescending(x => x.R).Take(NameLabels).Select(x => x.Id));
            foreach (var c in model.Chokes) named.Add(c.Actor);
            if (Selected != null) named.Add(Selected);
            var info = model.ScenarioById(Scenario);
            if (info != null) { foreach (var x in info.Friendly.Take(3)) named.Add(x); foreach (var x in info.Unfriendly.Take(3)) named.Add(x); }
            foreach (var body in net.Bodies)
            {
                switch (body.Kind)
                {
                    case ContestNetwork.Kind.Company:
                        if (!named.Contains(body.Id)) continue;
                        string value = double.IsNaN(body.Value) ? " <i>value unverified</i>" : " " + Money(body.Value);
                        double pe = body.State != null && !double.IsNaN(body.State.Pe) ? body.State.Pe : body.Actor.Pe;
                        string peText = !double.IsNaN(pe) && pe > 0 ? " · P/E " + pe.ToString("0") : (body.State != null && body.State.Ni < 0) || body.Actor.Private || pe < 0 ? " · loss" : "";
                        string fate = body.State == null || body.State.Alive ? "" : body.State.Status == "extinct" ? " <color=#F59A9A>extinct</color>" : " <color=#C9CED6>absorbed by " + (net.Get(body.State.By)?.Name ?? body.State.By) + "</color>";
                        labels.Add(new HillLabel
                        {
                            Text = "<b>" + body.Name + "</b>" + value + peText + fate, At = body.Pos + Vector3.up * (body.R + 2.5f), Role = LabelRole.Name, Part = "advantage.organs",
                            Rank = body.R + (body.Id == Selected ? 1000 : 0), Plate = new Color(0, 0, 0, .55f),
                        });
                        break;
                    case ContestNetwork.Kind.Owner:
                    case ContestNetwork.Kind.Wealth:
                        if (body.Kind == ContestNetwork.Kind.Owner && body.Value < 150 && Selected == null) continue;
                        labels.Add(new HillLabel
                        {
                            Text = body.Name + " " + Money(body.Value), At = body.Pos + Vector3.up * 2.5f, Role = body.Kind == ContestNetwork.Kind.Wealth ? LabelRole.Name : LabelRole.Note,
                            Part = "advantage.rings", Rank = (float)Math.Log10(1 + body.Value), Color = body.Kind == ContestNetwork.Kind.Wealth ? GoldBright : Gold,
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
                var r = fp.Rivals.Select(x => net.Get(x)).FirstOrDefault(x => x != null);
                if (h == null || r == null) continue;
                labels.Add(new HillLabel { Text = fp.Title, At = SparkAt(h.Pos + Vector3.up * h.R, r.Pos + Vector3.up * r.R) + Vector3.up * 3, Role = LabelRole.Note, Part = "advantage.sparks", Rank = 3, Color = TriadColor(fp.Triad) });
            }

            float rx = RulerX(), rz = net.CrestZ + ContestNetwork.ZTo - 20;
            labels.Add(new HillLabel { Text = "HIGH GROUND · dominant", At = new Vector3(rx, ContestNetwork.MaxAlt + 4, rz), Role = LabelRole.Note, Part = "advantage.rings", Rank = 5, Color = Roles.Encoded(Roles.InkDim) });
            labels.Add(new HillLabel { Text = "LOW GROUND · dependent", At = new Vector3(rx, 4, rz), Role = LabelRole.Note, Part = "advantage.rings", Rank = 5, Color = Roles.Encoded(Roles.InkDim) });
            labels.Add(new HillLabel { Text = "THE OWNERS", At = new Vector3(rx, ContestNetwork.OwnerY, rz), Role = LabelRole.Name, Part = "advantage.rings", Rank = 5, Color = Gold });
            labels.Add(new HillLabel { Text = "WORKERS AND CONSUMERS", At = new Vector3(rx, -3, rz), Role = LabelRole.Note, Part = "advantage.rings", Rank = 4, Color = Roles.Encoded(Roles.InkDim) });
            labels.Add(new HillLabel { Text = "THE STATE · rule of law, property, export controls", At = new Vector3(rx, ContestNetwork.StateY - 3, rz), Role = LabelRole.Note, Part = "advantage.rings", Rank = 4, Color = Roles.Encoded(Roles.InkDim) });
            for (int i = 0; i < threadBeads.Count && History; i++)
            {
                var (at, step) = threadBeads[i];
                string who = step.From == step.To || string.IsNullOrEmpty(step.FromName) ? step.ToName : step.FromName + " → " + step.ToName;
                labels.Add(new HillLabel { Text = step.Year + " · " + who, At = at + Vector3.up * 3, Role = LabelRole.Note, Part = "advantage.thread", Rank = 2.5f, Color = ThreadColor });
            }

            foreach (var (at, fp) in historySparks.Where(_ => History))
                labels.Add(new HillLabel { Text = fp.Year + " " + fp.Title, At = at + Vector3.up * 3.5f, Role = LabelRole.Note, Part = "advantage.thread", Rank = 1.5f, Color = TriadColor(fp.Triad) });
        }

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
            if (overlap > 1.5f) Debug.LogError($"[Why] advantage: shells interpenetrate by {overlap:0.0} ({pair})");
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
            Debug.Log($"[Why] advantage: {active} renderers active (at most {MaxRenderers}), and {organisms.Count} organisms, {organisms.Values.Sum(o => o.renderer ? o.renderer.GetComponent<MeshFilter>().sharedMesh.vertexCount : 0)} vertices in them");
            if (active > MaxRenderers) Debug.LogError($"[Why] advantage: {active} renderers active, more than {MaxRenderers}");

            string harsh = model.WorldById("digestion") != null ? "digestion" : Worlds().Count > 2 ? Worlds()[2] : null;
            if (harsh != null)
            {
                var before = net.Bodies.Where(b => b.Kind == ContestNetwork.Kind.Company).ToDictionary(b => b.Id, b => (b.TargetY, b.Alive));
                float longest = 0;
                SetScenario(harsh);
                SetYear(2030);
                for (float t = 0; t < 7.5f && settling; t += Time.unscaledDeltaTime)
                {
                    yield return null;
                    longest = Mathf.Max(longest, Time.unscaledDeltaTime);
                }

                int moved = net.Bodies.Count(b => b.Kind == ContestNetwork.Kind.Company && before.TryGetValue(b.Id, out var was) && (Mathf.Abs(was.TargetY - b.TargetY) > 3 || was.Alive != b.Alive));
                int dead = net.Bodies.Count(b => b.Kind == ContestNetwork.Kind.Company && !b.Alive);
                Debug.Log($"[Why] advantage world {harsh} 2030: {moved} organisms changed ground or fate, {dead} absorbed or extinct; longest frame {1000 * longest:0} ms");
                Log();
                if (model.Worlds.Count > 0 && moved < 5) Debug.LogError($"[Why] advantage: world {harsh} moved {moved} organisms, fewer than 5");
                if (longest > .05f) Debug.LogWarning($"[Why] advantage: a settling frame took {1000 * longest:0} ms, over 50");
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
            organismQueue.Clear();
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
