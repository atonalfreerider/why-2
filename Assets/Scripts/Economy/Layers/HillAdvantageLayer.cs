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
    /// The present as a contest (design Docs/hills-design/ADVANTAGE.md): the crest of 2026 opened into a hillside where
    /// <list type="bullet">
    /// <item>every actor of the economy is a peak (<see cref="AdvantageLayout"/>): its footprint its market cap, its altitude
    /// its competitive advantage (Helmer's seven powers judged by Jev from verified facts), its strata the powers that hold
    /// it up, a gold cap where its operating margin rises above normal profit; the supply chain runs in bands from energy
    /// (by the crest, the past) to the buyers (toward the future), rivals side by side; capital and the rest of the
    /// economy on the flanks, the states on plinths behind;</item>
    /// <item>rivers of money run between them on the ground, as wide as their dollars a year, in two lanes: rose for the
    /// payer's desire, ice for its fear (judged); rivers converge at each actor's mouth, and a ring marks the choke points;</item>
    /// <item>sparks between rivals mark the contests being fought (flashpoints), coloured by what decides them: violet
    /// information, orange execution, green environment;</item>
    /// <item>a scenario (the panel's buttons, <see cref="SetScenario"/>) moves every peak to its judged altitude in that world
    /// and swells or dries the rivers.</item>
    /// </list>
    /// Key I enters (the crest opens, the peaks rise, the rivers start); I again replays; Shift + I returns to the overview.
    /// Meshes are built once per land; the choreography moves transforms; a scenario rebuilds the peaks while it moves them.
    /// </summary>
    [GraphScenes(EconomyLayouts.HillsScene)]
    public sealed class HillAdvantageLayer : GraphLayer
    {
        public const string View = "advantage", Sequence = "advantage";

        public override int Order => 59;

        public override IEnumerable<string> RequiredTexts => new[] { Advantage.Path };

        enum Part { Floor, Peaks, Rivers, Marks, Sparks, Thread, Count }

        static readonly string[] PartNames = { "floor", "peaks", "rivers", "marks", "sparks", "thread" };
        static readonly int[] Keys = EconomyEmphasis.Keys("advantage", PartNames);

        /// <summary>The choreography: the crest opens (0-1.6 s), the peaks rise (1-3.6 s), the rivers and sparks show (3.2-4.6 s).</summary>
        public const float Seconds = 5f, ReverseSeconds = 1.8f;

        const float ScenarioSeconds = 1.6f;
        const int MaxRenderers = 12, RiverHighlights = 14, SparkDefault = 7, NameLabels = 18, ChokeRings = 6;

        /// <summary>The strata's colours by power (mineral tones: gold is kept for profit, rose and ice for motives).</summary>
        public static readonly Dictionary<string, Color> PowerColor = new Dictionary<string, Color>
        {
            ["scale"] = new Color(.46f, .56f, .72f), ["network"] = new Color(.26f, .64f, .58f), ["counter"] = new Color(.62f, .42f, .68f),
            ["switching"] = new Color(.45f, .42f, .84f), ["brand"] = new Color(.80f, .46f, .33f), ["cornered"] = new Color(.70f, .26f, .32f),
            ["process"] = new Color(.58f, .64f, .30f),
        };

        /// <summary>The triad's colours (sparks): information, execution, environment.</summary>
        public static readonly Color InfoColor = new Color(.68f, .54f, 1f), ExecColor = new Color(1f, .58f, .24f), EnvColor = new Color(.45f, .95f, .55f);

        /// <summary>Surface tones, authored in sRGB and stored linear (vertex colours are linear: the project renders in linear space).</summary>
        static readonly Color Rock = new Color(.26f, .28f, .32f).linear, Floor = new Color(.085f, .095f, .115f).linear, StripA = new Color(.13f, .145f, .17f).linear,
            StripB = new Color(.105f, .115f, .14f).linear, Bedrock = new Color(.22f, .26f, .31f).linear, GoldDeep = new Color(.80f, .54f, .15f),
            GoldBright = new Color(1f, .88f, .50f), ChokeColor = new Color(1f, .93f, .75f), Neutral = new Color(.75f, .71f, .67f),
            PlainColor = new Color(.34f, .47f, .72f).linear, PlinthColor = new Color(.52f, .56f, .63f).linear;

        static readonly Vector3 Light = new Vector3(-.4f, .78f, -.48f).normalized;

        static readonly bool SmokeRun = Environment.GetCommandLineArgs().Contains("-smoketest");

        /// <summary>A scenario's move is instant in a smoke run's catalog (its frames capture each world at once); the
        /// view's own sequence turns the move back on to time it.</summary>
        static bool instantMoves = SmokeRun;

        // ------------------------------------------------------------------ the state the panel reads

        static HillAdvantageLayer current;

        /// <summary>The scenario on screen (<see cref="Advantage.Now"/>, <see cref="Advantage.Y2030"/> or the file's ids).</summary>
        public static string Scenario { get; private set; } = Advantage.Now;

        /// <summary>The selected and the hovered actor (ids), or null.</summary>
        public static string Selected { get; private set; }

        public static string Hovered { get; private set; }

        public static AdvantageLayout Layout => current != null ? current.layout : null;

        /// <summary>Which part of the view the camera shows: the 2026 arena, or the history along the wave (1947-2026).</summary>
        public static bool History { get; private set; }

        /// <summary>Flies the camera to the arena or along the wave's history (the panel's buttons).</summary>
        public static void ShowHistory(bool history)
        {
            if (history == History && current != null) return;
            History = history;
            Fly(history ? HillPresets.AdvantageHistory() : ViewPresets.Get(View), 1.6f);
            if (current != null) current.dirtyLines = true;
        }

        /// <summary>Moves the landscape to a scenario (the panel's buttons, the tour's cues).</summary>
        public static void SetScenario(string id)
        {
            id = string.IsNullOrEmpty(id) ? Advantage.Now : id;
            if (id == Scenario) return;
            if (current != null) current.Retarget(id);
            Scenario = id;
        }

        /// <summary>Selects an actor (its rivers and sparks lit, the panel's detail), or none.</summary>
        public static void Select(string id)
        {
            if (id == Selected) return;
            Selected = id;
            if (current != null) current.dirtyLines = true;
        }

        public static HillPresets.ViewKey Key { get; } = new HillPresets.ViewKey(new (string, Color)[]
            {
                ("Profit above normal", GoldBright), ("Spent toward a want", EconomyStyle.Desire), ("Spent from fear", EconomyStyle.Fear),
                ("Choke point", ChokeColor), ("Contest won by information", InfoColor), ("by execution", ExecColor), ("by environment", EnvColor),
            },
            "Each peak is one actor: as wide as its market value, as high as its competitive advantage (Helmer's seven powers, judged by an AI model, Jev, " +
            "from sourced facts); its bands are the powers that hold it up. Rivers carry money a year to who sells, widest the most. Sparks: contests being fought.");

        // ------------------------------------------------------------------ the layer

        string json;
        Advantage model;
        AdvantageLayout layout;
        HillLandscape land;
        GameObject content;
        Transform arena, peaksRoot;
        readonly MeshRenderer[] partRenderer = new MeshRenderer[(int)Part.Count];
        MeshRenderer beds;
        readonly List<(Renderer renderer, Part part)> renderers = new List<(Renderer, Part)>();
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        readonly List<HillLabel> labels = new List<HillLabel>();
        bool labelsShown, dirtyLines;

        float clock = Seconds, speed = 1;
        int direction, cue;
        bool moveCamera, held;

        /// <summary>The scenario transition: from one scenario's altitudes to another's, 0..1.</summary>
        string fromScenario = Advantage.Now, toScenario = Advantage.Now;
        float mix = 1, sinceRebuild;
        Vector2 pressed;

        /// <summary>What the last build drew, for the log and the smoke checks.</summary>
        int peakCount, riverCount, sparkCount, chokeCount, unsized, unjudged;

        public override void Prepare(GraphContext ctx)
        {
            json = ctx.Text(Advantage.Path);
            Advantage.Provide(json);
        }

        public override void Upload(GraphContext ctx)
        {
            current = this;
            EconomyStage.Land().Place(transform);
            EconomyEmphasis.RegisterView(View, PartNames.Select(p => "advantage." + p), null, new[] { "wave.crest", "wave.face", "wave.lives" }, synthesis: true);
            // the worlds are the view's variants (now, 2030, then the scenarios), so a tour stop's cues can show each in turn
            ViewVariants.Register(View, () => Worlds().Count, k => SetScenario(Worlds()[Mathf.Clamp(k, 0, Worlds().Count - 1)]), k => k >= 0 && k < Worlds().Count ? Worlds()[k] : k.ToString());
            if (SmokeRun) SmokeChecks.RegisterSequence(Sequence, Smoke());
        }

        /// <summary>The worlds the view can show: now, 2030 on the most likely course, then the file's scenarios.</summary>
        static List<string> Worlds()
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
            }
        }

        public override void Tick(GraphContext ctx, CameraRig rig)
        {
            if (HillLandscapeLayer.Current == null || LandService.Current == null) return;
            HandleKeys();
            bool here = LandView.PresetId == View;
            if (!here && EconomyEmphasis.Get(Keys[(int)Part.Peaks]) <= 0)
            {
                if (content) content.SetActive(false);
                ShowLabels(false);
                return;
            }

            model = Advantage.Get();
            if (model == null) return;
            if (land != HillLandscapeLayer.Current || layout == null)
            {
                if (!HillRebuilds.Take()) return;
                using (LayerClock.Measure("advantage: build")) Build();
            }

            if (content == null) return;
            content.SetActive(true);
            if (direction != 0 && !held) Advance();
            StepScenario();
            if (dirtyLines && mix >= 1)
            {
                dirtyLines = false;
                using (LayerClock.Measure("advantage: lines")) Lines();
                MakeLabels();
                if (labelsShown) HillLabels.Set(this, labels);
            }

            Apply();
            if (here && direction == 0 && rig != null && rig.Cam != null) Pick(rig.Cam);
            ShowLabels(here && direction >= 0 && Reveal(clock) > .5f && mix >= 1);
        }

        // ------------------------------------------------------------------ the choreography

        static float Smooth(float a, float b, float t) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a, b, t));

        static float Open(float t) => Smooth(0, 1.6f, t);

        static float Rise(float t) => Smooth(1f, 3.6f, t);

        static float Reveal(float t) => Smooth(3.2f, 4.6f, t);

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

        void Hold(float seconds)
        {
            clock = Mathf.Clamp(seconds, 0, Seconds);
            direction = 0;
            held = true;
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

        void Apply()
        {
            float open = Mathf.Max(.02f, Open(clock)), rise = Mathf.Max(.002f, Rise(clock)), reveal = Reveal(clock);
            content.transform.localScale = new Vector3(1, 1, open);
            peaksRoot.localScale = new Vector3(1, rise, 1);
            foreach ((Renderer r, Part part) in renderers)
            {
                float a = EconomyEmphasis.Get(Keys[(int)part]);
                if (part == Part.Rivers || part == Part.Sparks || part == Part.Marks || part == Part.Thread) a *= reveal;
                EconomyEmphasis.Set(r, a);
            }
        }

        // ------------------------------------------------------------------ scenarios

        void Retarget(string id)
        {
            // a scenario chosen mid-move starts from where the last one was heading
            fromScenario = mix >= .5f ? toScenario : fromScenario;
            toScenario = id;
            mix = layout == null || instantMoves ? 1 : 0;
            sinceRebuild = 1;
            dirtyLines = true;
            if (instantMoves && layout != null)
            {
                fromScenario = id;
                Peaks();
            }
        }

        void StepScenario()
        {
            if (mix >= 1) return;
            mix = Mathf.Min(1, mix + GraphTime.Delta / ScenarioSeconds);
            sinceRebuild += GraphTime.Delta;
            // the peaks rebuilt about 24 times a second while they move (one mesh, ~40k vertices)
            if (sinceRebuild >= 1 / 24f || mix >= 1)
            {
                sinceRebuild = 0;
                using (LayerClock.Measure("advantage: peaks")) Peaks();
            }
        }

        float HeightNow(AdvantageLayout.Place p)
        {
            float e = Mathf.SmoothStep(0, 1, mix);
            return Mathf.Lerp(layout.Height(p, fromScenario), layout.Height(p, toScenario), e);
        }

        // ------------------------------------------------------------------ the build

        void Build()
        {
            Clear();
            land = HillLandscapeLayer.Current;
            layout = new AdvantageLayout(model, land.CrestZ);
            fromScenario = toScenario = Scenario;
            mix = 1;
            content = new GameObject("Advantage");
            content.transform.SetParent(transform, false);
            content.transform.localPosition = new Vector3(0, 0, land.CrestZ);
            arena = new GameObject("Arena").transform;
            arena.SetParent(content.transform, false);
            arena.localPosition = new Vector3(0, 0, -land.CrestZ);
            peaksRoot = new GameObject("Peaks").transform;
            peaksRoot.SetParent(arena, false);
            Ground();
            Peaks();
            Lines();
            MakeLabels();
            Log();
        }

        /// <summary>The arena's floor (bands in alternating tones), the bedrock under it and the states' terrace behind.</summary>
        void Ground()
        {
            var b = new SurfaceMeshBuilder();
            float id = EconomyEmphasis.Id(Keys[(int)Part.Floor]);
            float x0 = layout.MinX - 26, x1 = layout.MaxX + 26, zBack = layout.CrestZ + AdvantageLayout.Back - 10, zFront = layout.FrontZ + 14;
            Quad(b, id, new Vector3(x0, -.05f, zBack), new Vector3(x1, -.05f, zBack), new Vector3(x0, -.05f, zFront), new Vector3(x1, -.05f, zFront), Floor);
            for (int k = 0; k < layout.BandZ.Length; k++)
            {
                float h = layout.BandHalfDepth[k] + AdvantageLayout.BandGap * .5f, z = layout.BandZ[k];
                float bx0 = layout.EconomyX - 4 - 2 * h, bx1 = layout.CapitalX - 8;
                Quad(b, id, new Vector3(bx0, 0, z - h), new Vector3(bx1, 0, z - h), new Vector3(bx0, 0, z + h), new Vector3(bx1, 0, z + h), k % 2 == 0 ? StripA : StripB);
            }

            // the bedrock: the state under everything (its faces toward the future and both sides)
            const float depth = 12;
            Color side = Bedrock * .8f; side.a = 1;
            Quad(b, id, new Vector3(x0, -.05f, zFront), new Vector3(x1, -.05f, zFront), new Vector3(x0, -depth, zFront), new Vector3(x1, -depth, zFront), Bedrock);
            Quad(b, id, new Vector3(x0, -.05f, zBack), new Vector3(x0, -.05f, zFront), new Vector3(x0, -depth, zBack), new Vector3(x0, -depth, zFront), side);
            Quad(b, id, new Vector3(x1, -.05f, zBack), new Vector3(x1, -.05f, zFront), new Vector3(x1, -depth, zBack), new Vector3(x1, -depth, zFront), side);
            // the states' terrace between the crest and the arena
            float sz0 = layout.CrestZ + AdvantageLayout.StateZ - 18, sz1 = layout.CrestZ + AdvantageLayout.StateZ + 18;
            Quad(b, id, new Vector3(x0, -.05f, sz0), new Vector3(x1, -.05f, sz0), new Vector3(x0, -.05f, sz1), new Vector3(x1, -.05f, sz1), Bedrock * .55f + Floor * .45f);
            Surface(b, Part.Floor, arena, 3180);
        }

        /// <summary>Every actor's peak at the transition's heights (rebuilt while a scenario moves them).</summary>
        void Peaks()
        {
            Drop(partRenderer[(int)Part.Peaks]);

            var b = new SurfaceMeshBuilder();
            float id = EconomyEmphasis.Id(Keys[(int)Part.Peaks]);
            peakCount = unsized = unjudged = 0;
            foreach (var p in layout.Places)
            {
                if (p.Plain) { Cylinder(b, id, p.Base, p.R, AdvantageLayout.PlainH, PlainColor, 28); continue; }
                if (p.State) { Cylinder(b, id, p.Base, p.R, AdvantageLayout.PlinthH, PlinthColor, 6); continue; }
                peakCount++;
                if (!p.Actor.Sized) unsized++;
                if (!p.Actor.Judged) unjudged++;
                Mountain(b, id, p, HeightNow(p));
            }

            partRenderer[(int)Part.Peaks] = Surface(b, Part.Peaks, peaksRoot, 3182);
        }

        /// <summary>A faceted peak: a grey plinth, then a stratum per leading power (as thick as what it adds to the
        /// advantage), then a gold cap as deep as the operating margin above normal profit (brighter the more).</summary>
        void Mountain(SurfaceMeshBuilder b, float id, AdvantageLayout.Place p, float h)
        {
            var a = p.Actor;
            int segments = p.R > 12 ? 16 : p.R > 5 ? 12 : 9;
            float twist = (a.Id.GetHashCode() & 1023) / 1023f * Mathf.PI * 2;
            // the layers from the ground up: (top height, colour, intensity)
            var layers = new List<(float top, Color color, float intensity)>();
            float plinth = Mathf.Min(h, AdvantageLayout.H0);
            layers.Add((plinth, Rock, 1));
            if (a.Judged && h > plinth + .01f)
            {
                var strata = a.Strata();
                double total = 0;
                for (int i = 0; i < 3 && i < strata.Count; i++) total += strata[i].adds;
                float y = plinth;
                for (int i = 0; i < 3 && i < strata.Count; i++)
                {
                    float t = total > 0 ? (float)(strata[i].adds / total) : 1 / 3f;
                    y += (h - plinth) * t;
                    layers.Add((i == 2 ? h : y, PowerColor.TryGetValue(strata[i].power, out Color c) ? c.linear : Rock, 1));
                }
            }
            else layers.Add((h, Rock * 1.1f, 1));

            // gold: the top of the peak, as deep as the margin's excess over normal profit (10%), at most 30% of its height
            double margin = a.MarginPct;
            float goldFrom = h;
            float goldIntensity = 1;
            if (!double.IsNaN(margin) && margin > 10)
            {
                float excess = Mathf.Clamp01((float)(margin - 10) / 60f);
                goldFrom = h * (1 - (.08f + .22f * excess));
                goldIntensity = 1.25f + 1.4f * excess;
            }

            float lo = 0;
            foreach (var layer in layers)
            {
                float top = layer.top;
                if (top <= lo + 1e-3f) continue;
                if (goldFrom < top && goldFrom > lo)
                {
                    Frustum(b, id, p, h, lo, goldFrom, layer.color, layer.intensity, segments, twist);
                    lo = goldFrom;
                }

                bool gold = lo >= goldFrom - 1e-3f;
                Color c = gold ? Color.Lerp(GoldDeep, GoldBright, Mathf.InverseLerp(goldFrom, h, (lo + top) * .5f)) : layer.color;
                Frustum(b, id, p, h, lo, top, c, gold ? goldIntensity : layer.intensity, segments, twist);
                lo = top;
            }
        }

        /// <summary>One band of a peak's flanks, flat-shaded facets, from <paramref name="y0"/> to <paramref name="y1"/> (the
        /// last band closes at the summit). The profile is concave: radius R (1 - y/h)^1.5.</summary>
        static void Frustum(SurfaceMeshBuilder b, float id, AdvantageLayout.Place p, float h, float y0, float y1, Color color, float intensity, int segments, float twist)
        {
            const float pow = 1.5f;
            float R = p.R;
            float r0 = R * Mathf.Pow(Mathf.Clamp01(1 - y0 / h), pow), r1 = R * Mathf.Pow(Mathf.Clamp01(1 - y1 / h), pow);
            float slope = (r0 - r1) / Mathf.Max(1e-3f, y1 - y0);
            for (int i = 0; i < segments; i++)
            {
                float a0 = twist + i * Mathf.PI * 2 / segments, a1 = twist + (i + 1) * Mathf.PI * 2 / segments, am = (a0 + a1) * .5f;
                Vector3 n = new Vector3(Mathf.Cos(am), slope, Mathf.Sin(am)).normalized;
                float shade = .38f + .62f * Mathf.Max(0, Vector3.Dot(n, Light));
                Color c = color * shade;
                c.a = 1;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                Vector3 b0 = p.Base + d0 * r0 + Vector3.up * y0, b1 = p.Base + d1 * r0 + Vector3.up * y0;
                Vector3 t0 = p.Base + d0 * r1 + Vector3.up * y1, t1 = p.Base + d1 * r1 + Vector3.up * y1;
                b.AddTriangle(b0, t0, b1, c, id, intensity);
                if (r1 > 1e-3f) b.AddTriangle(b1, t0, t1, c, id, intensity);
            }
        }

        /// <summary>A flat cylinder (the buyers' plains, the states' plinths), its top lit and its side shaded.</summary>
        static void Cylinder(SurfaceMeshBuilder b, float id, Vector3 at, float r, float h, Color color, int segments)
        {
            Color top = color * .95f, side = color * .6f;
            top.a = side.a = 1;
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2 / segments, a1 = (i + 1) * Mathf.PI * 2 / segments;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * r, d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * r;
                Vector3 up = Vector3.up * h;
                b.AddTriangle(at + up, at + d1 + up, at + d0 + up, top, id);
                b.AddTriangle(at + d0, at + d0 + up, at + d1, side, id);
                b.AddTriangle(at + d1, at + d0 + up, at + d1 + up, side, id);
            }
        }

        // ------------------------------------------------------------------ rivers, rings, sparks

        /// <summary>The rivers, the rings and the sparks for the scenario and the selection (rebuilt when either changes).</summary>
        void Lines()
        {
            foreach (Part part in new[] { Part.Rivers, Part.Marks, Part.Sparks, Part.Thread })
            {
                Drop(partRenderer[(int)part]);
                partRenderer[(int)part] = null;
            }

            Drop(beds);
            beds = null;
            string scenario = toScenario;
            var rivers = new LineMeshBuilder();
            var bed = new SurfaceMeshBuilder();
            float rid = EconomyEmphasis.Id(Keys[(int)Part.Rivers]);
            var ranked = model.Flows.OrderByDescending(f => f.In(scenario)).ToList();
            riverCount = 0;
            for (int i = 0; i < ranked.Count; i++)
            {
                var f = ranked[i];
                bool lit = Selected == null ? i < RiverHighlights : f.From == Selected || f.To == Selected;
                float alpha = lit ? 1 : Selected == null ? .32f : .1f;
                River(bed, rivers, rid, f, scenario, i, alpha);
                riverCount++;
            }

            // money climbs to advantage: from each mouth, up the flank to the summit, the share of the inflow the actor keeps
            // above its costs (inflow times its operating margin), for those above normal profit
            var kept = new Dictionary<string, double>();
            foreach (var f in model.Flows) kept[f.To] = (kept.TryGetValue(f.To, out double k) ? k : 0) + f.In(scenario);
            foreach (var kv in kept)
            {
                var a = model.Get(kv.Key);
                var p = layout.Get(kv.Key);
                if (a == null || p == null || p.Plain || p.State || double.IsNaN(a.MarginPct) || a.MarginPct <= 10) continue;
                bool lit = Selected == null || Selected == a.Id;
                Climb(rivers, rid, p, kv.Value * a.MarginPct / 100, lit ? 1 : .2f);
            }

            beds = Surface(bed, Part.Rivers, arena, 3183, solid: false);
            var r = Lines(rivers, Part.Rivers, arena, 3184, true);
            r.sharedMaterial.SetFloat("_Flow", 1.3f);
            r.sharedMaterial.SetFloat("_FlowFreq", 3);
            r.sharedMaterial.SetFloat("_FlowSpeed", .35f);
            partRenderer[(int)Part.Rivers] = r;

            var marks = new LineMeshBuilder();
            float mid = EconomyEmphasis.Id(Keys[(int)Part.Marks]);
            chokeCount = 0;
            var ringed = new HashSet<string>(model.Chokes.Where(c => layout.Get(c.Actor) != null).OrderByDescending(c => double.IsNaN(c.Strength) ? 0 : c.Strength).Take(ChokeRings).Select(c => c.Id));
            foreach (var c in model.Chokes)
            {
                var p = layout.Get(c.Actor);
                if (p == null || !ringed.Contains(c.Id) && c.Actor != Selected) continue;
                chokeCount++;
                float s = double.IsNaN(c.Strength) ? .6f : (float)c.Strength;
                bool lit = Selected == null || Selected == c.Actor;
                Ring(marks, mid, p.Base + Vector3.up * .5f, p.R + 1.6f, WithAlpha(ChokeColor, lit ? 1 : .25f), 2.2f, 1.2f + 1.6f * s);
            }

            foreach (var p in layout.Places)
            {
                if (p.Plain || p.State) continue;
                if (!p.Actor.Sized) Ring(marks, mid, p.Base + Vector3.up * .3f, p.R + .8f, WithAlpha(Neutral, .7f), 1.2f, 1, dashed: true);
            }

            partRenderer[(int)Part.Marks] = Lines(marks, Part.Marks, arena, 3186, true);

            var sparks = new LineMeshBuilder();
            float sid = EconomyEmphasis.Id(Keys[(int)Part.Sparks]);
            sparkCount = 0;
            foreach (var fp in ShownSparks())
            {
                Spark(sparks, sid, fp);
                sparkCount++;
            }

            var sr = Lines(sparks, Part.Sparks, peaksRoot, 3188, true);
            sr.sharedMaterial.SetFloat("_Flow", 1.6f);
            sr.sharedMaterial.SetFloat("_FlowFreq", 1.5f);
            sr.sharedMaterial.SetFloat("_FlowSpeed", .9f);
            partRenderer[(int)Part.Sparks] = sr;

            var thread = new LineMeshBuilder();
            Thread(thread, EconomyEmphasis.Id(Keys[(int)Part.Thread]));
            // under the layer itself, not the arena: the thread runs back along the wave, which the opening does not stretch
            var tr = Lines(thread, Part.Thread, transform, 3189, true);
            tr.sharedMaterial.SetFloat("_Flow", 1.4f);
            tr.sharedMaterial.SetFloat("_FlowFreq", 2);
            tr.sharedMaterial.SetFloat("_FlowSpeed", .5f);
            partRenderer[(int)Part.Thread] = tr;
        }

        // ------------------------------------------------------------------ history: the thread and its contests

        /// <summary>The steps of the lineage that lead to the frontier (back from NVIDIA, OpenAI and Anthropic through every
        /// link that reaches them), in order of their years, from 1947 (the wave's first year).</summary>
        List<Advantage.Step> Spine()
        {
            var reach = new HashSet<string> { "nvidia", "openai", "anthropic" };
            var steps = model.Lineage.Where(x => x.Year >= 1947 && x.Year <= 2026).OrderByDescending(x => x.Year).ToList();
            var spine = new List<Advantage.Step>();
            foreach (var step in steps)
            {
                if (!reach.Contains(step.To) && !reach.Contains(step.From)) continue;
                spine.Add(step);
                reach.Add(step.From);
                reach.Add(step.To);
            }

            spine.Reverse();
            return spine;
        }

        /// <summary>Where a historical actor stood on the wave in a year (land-local): on its company's lifeline when it has
        /// one, else on the line of the frontier's lineage, a little above the wave.</summary>
        bool OnWave(string actor, int year, out Vector3 at)
        {
            at = default;
            var lives = CompanyLives.Get();
            var a = model.Get(actor);
            string life = a?.Lifeline ?? actor;
            if (lives != null)
                for (int i = 0; i < lives.Lives.Length; i++)
                    if (lives.Lives[i].Id == life && HillWaveLayer.CompanyAtYear(i, year + .5, out at))
                    {
                        at += Vector3.up * 1.5f;
                        return true;
                    }

            return false;
        }

        /// <summary>The spine's lane: beside the wave's tech edge, a little above the ground, so time reads straight along it.</summary>
        public const float SpineX = 212, SpineY = 16;

        static Vector3 OnSpine(double year) => new Vector3(SpineX, SpineY, HillLandscape.Z(year));

        /// <summary>The thread: one straight lane back along the wave from the frontier's peak, a bead at each step of its
        /// lineage (1947 on), its pulses running toward the present; sparks at the historical contests, on the companies'
        /// lifelines where both sides have one, else beside the lane at their year.</summary>
        void Thread(LineMeshBuilder b, float id)
        {
            threadBeads.Clear();
            historySparks.Clear();
            var spine = Spine();
            var top = layout.Get("nvidia");
            if (spine.Count == 0 || top == null) return;
            var onSpine = new HashSet<string>();
            foreach (var step in spine)
            {
                onSpine.Add(step.From);
                onSpine.Add(step.To);
                threadBeads.Add((OnSpine(step.Year + .5), step));
            }

            var pts = new List<Vector3> { OnSpine(1947) };
            Vector3 crest = new Vector3(SpineX, SpineY, layout.CrestZ - .5f), summit = layout.Summit(top, toScenario);
            pts.Add(crest);
            // over the states' terrace and up to the frontier's summit
            for (int k = 1; k <= 12; k++)
            {
                float t = k / 12f;
                Vector3 p = Vector3.Lerp(crest, summit + Vector3.up * .5f, t);
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
            foreach (var (at, _) in threadBeads) Ring(b, id, at, 1.3f, ThreadColor, 2, 1.8f);

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

        /// <summary>The lineage's steps on the thread (the panel lists them).</summary>
        public static List<Advantage.Step> SpineSteps => current != null ? current.threadBeads.Select(x => x.step).ToList() : new List<Advantage.Step>();

        /// <summary>The thread's beads (each step of the lineage) and the historical sparks on the wave, for the labels.</summary>
        readonly List<(Vector3 at, Advantage.Step step)> threadBeads = new List<(Vector3, Advantage.Step)>();
        readonly List<(Vector3 at, Advantage.Flashpoint fp)> historySparks = new List<(Vector3, Advantage.Flashpoint)>();
        static readonly Color ThreadColor = new Color(1f, .92f, .72f);

        /// <summary>The present contests drawn: the selected actor's, else the best-sourced few.</summary>
        IEnumerable<Advantage.Flashpoint> ShownSparks()
        {
            var present = model.Flashpoints.Where(f => f.InArena && f.Year >= 2023 && layout.Get(f.Holder) != null).ToList();
            if (Selected != null) return present.Where(f => f.Holder == Selected || f.Rivals.Contains(Selected));
            return present.OrderByDescending(f => f.Sources.Length).ThenByDescending(f => f.Year).Take(SparkDefault);
        }

        /// <summary>A river in two lanes on the ground, desire (rose, left) and fear (ice, right), together as wide as its
        /// dollars a year (0.5 √$B units); a single neutral lane where the motive was not judged. Each lane is a flat bed with a
        /// thin line down its middle whose pulses run toward the payee; the river narrows at each mouth so many can meet there.</summary>
        void River(SurfaceMeshBuilder beds, LineMeshBuilder pulses, float id, Advantage.Flow f, string scenario, int salt, float alpha)
        {
            List<Vector3> pts = layout.River(f, salt);
            if (pts.Count < 2) return;
            float width = Mathf.Max(.5f, .5f * Mathf.Sqrt((float)f.In(scenario)));
            float length = 0;
            for (int i = 1; i < pts.Count; i++) length += Vector3.Distance(pts[i - 1], pts[i]);
            float phase = 1f / Mathf.Max(1, length) * 6;
            if (double.IsNaN(f.Fear))
            {
                Lane(beds, pulses, id, pts, 0, width, Neutral, alpha, phase);
                return;
            }

            float fear = Mathf.Clamp01((float)f.Fear), wd = width * (1 - fear), wf = width * fear;
            if (wd > .05f) Lane(beds, pulses, id, pts, -wf * .5f, wd, EconomyStyle.Desire, alpha, phase);
            if (wf > .05f) Lane(beds, pulses, id, pts, wd * .5f, wf, EconomyStyle.Fear, alpha, phase);
        }

        static void Lane(SurfaceMeshBuilder beds, LineMeshBuilder pulses, float id, List<Vector3> pts, float offset, float width, Color c, float alpha, float phase)
        {
            int n = pts.Count;
            var inner = new Vector3[n];
            var outer = new Vector3[n];
            var mid = new List<LinePoint>(n);
            for (int i = 0; i < n; i++)
            {
                Vector3 d = pts[Mathf.Min(i + 1, n - 1)] - pts[Mathf.Max(i - 1, 0)];
                d.y = 0;
                Vector3 side = Vector3.Cross(Vector3.up, d.normalized);
                float t = i / (float)(n - 1), taper = Mathf.Clamp01(Mathf.Min(t, 1 - t) * 7f), w = width * Mathf.Lerp(.3f, 1, taper);
                Vector3 centre = pts[i] + side * offset * taper;
                inner[i] = centre - side * w * .5f;
                outer[i] = centre + side * w * .5f;
                mid.Add(new LinePoint(centre + Vector3.up * .05f, WithAlpha(c, alpha), 1.4f, 0, 1.7f));
            }

            beds.AddBand(inner, outer, new[] { (Color32)WithAlpha(c, .62f * alpha) }, id, 1.05f);
            pulses.AddFlowPath(mid, id, phase);
        }

        /// <summary>A gold stream up an actor's flank from its in-mouth to its summit, as wide as the profit it keeps from the
        /// rivers that reach it (0.5 √$B units at the foot, thinning to the top), pulses running upward.</summary>
        void Climb(LineMeshBuilder b, float id, AdvantageLayout.Place p, double keptBn, float alpha)
        {
            float h = HeightNow(p);
            Vector3 mouth = layout.InMouth(p.Actor.Id), dir = mouth - p.Base;
            dir.y = 0;
            dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.back;
            float w = Mathf.Max(.4f, .35f * Mathf.Sqrt((float)keptBn));
            var pts = new List<LinePoint>(14);
            for (int i = 0; i <= 12; i++)
            {
                float t = i / 12f, y = h * t, r = p.R * Mathf.Pow(Mathf.Clamp01(1 - t), 1.5f) + .35f;
                pts.Add(new LinePoint(p.Base + dir * r + Vector3.up * (y + .2f), WithAlpha(GoldBright, alpha * Mathf.Lerp(.9f, 1, t)), 0, w * Mathf.Lerp(1, .35f, t), Mathf.Lerp(1.2f, 1.9f, t)));
            }

            b.AddFlowPath(pts, id, 1f / Mathf.Max(1, h) * 3);
        }

        /// <summary>A spark between the holder and each rival: a star where they meet, a bolt to each summit, coloured by the
        /// strongest of the triad (judged).</summary>
        void Spark(LineMeshBuilder b, float id, Advantage.Flashpoint fp)
        {
            var h = layout.Get(fp.Holder);
            Color c = TriadColor(fp.Triad);
            var seed = new System.Random(fp.Id.GetHashCode());
            foreach (string rid in fp.Rivals)
            {
                var r = layout.Get(rid);
                if (r == null) continue;
                Vector3 a = layout.Summit(h, toScenario), z = layout.Summit(r, toScenario);
                Vector3 m = SparkAt(a, z);
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

        static Vector3 SparkAt(Vector3 a, Vector3 z) => Vector3.Lerp(a, z, .5f) + Vector3.up * (4 + .08f * Vector3.Distance(a, z));

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

            float length = Vector3.Distance(from, to);
            b.AddFlowPath(pts, id, 1f / Mathf.Max(1, length) * 3);
        }

        public static Color TriadColor(Dictionary<string, double> triad)
        {
            if (triad == null || triad.Count == 0) return Color.white;
            double i = triad.TryGetValue("information", out double x) ? x : 0, e = triad.TryGetValue("execution", out double y) ? y : 0,
                n = triad.TryGetValue("environment", out double z) ? z : 0;
            return i >= e && i >= n ? InfoColor : e >= n ? ExecColor : EnvColor;
        }

        // ------------------------------------------------------------------ labels

        void MakeLabels()
        {
            labels.Clear();
            string scenario = toScenario;
            var named = new HashSet<string>();
            foreach (var p in layout.Places.Where(p => !p.Plain && !p.State).OrderByDescending(p => p.R).Take(NameLabels)) named.Add(p.Actor.Id);
            foreach (var c in model.Chokes) named.Add(c.Actor);
            if (Selected != null) named.Add(Selected);
            var info = model.ScenarioById(scenario);
            if (info != null) { foreach (var x in info.Friendly.Take(3)) named.Add(x); foreach (var x in info.Unfriendly.Take(3)) named.Add(x); }
            foreach (var p in layout.Places)
            {
                var a = p.Actor;
                if (p.Plain || p.State)
                {
                    labels.Add(new HillLabel { Text = a.Name.ToUpperInvariant(), At = p.Base + Vector3.up * (p.State ? 4 : 3), Role = LabelRole.Note, Part = "advantage.peaks", Rank = p.R, Color = Roles.Encoded(Roles.InkDim) });
                    continue;
                }

                if (!named.Contains(a.Id)) continue;
                string size = a.Sized ? " " + a.SizeLabel : " <i>size unverified</i>";
                labels.Add(new HillLabel
                {
                    Text = "<b>" + a.Name + "</b>" + size, At = layout.Summit(p, scenario) + Vector3.up * 2.5f, Role = LabelRole.Name, Part = "advantage.peaks",
                    Rank = p.R + (a.Id == Selected ? 1000 : 0), Plate = new Color(0, 0, 0, .55f),
                });
            }

            foreach (var c in model.Chokes.Where(c => !double.IsNaN(c.Strength)).OrderByDescending(c => c.Strength).Take(5))
            {
                var p = layout.Get(c.Actor);
                if (p == null) continue;
                labels.Add(new HillLabel { Text = "choke point: " + Short(c.Controls, 5), At = p.Base + new Vector3(0, .8f, -(p.R + 3)), Role = LabelRole.Note, Part = "advantage.marks", Rank = 2, Color = ChokeColor, Provenance = Mark.Judged, Confidence = (float)c.Strength });
            }

            foreach (var fp in ShownSparks().Take(6))
            {
                var h = layout.Get(fp.Holder);
                var r = fp.Rivals.Select(x => layout.Get(x)).FirstOrDefault(x => x != null);
                if (h == null || r == null) continue;
                labels.Add(new HillLabel { Text = fp.Title, At = SparkAt(layout.Summit(h, scenario), layout.Summit(r, scenario)) + Vector3.up * 3, Role = LabelRole.Note, Part = "advantage.sparks", Rank = 3, Color = TriadColor(fp.Triad) });
            }

            for (int i = 0; i < threadBeads.Count && History; i++)
            {
                var (at, step) = threadBeads[i];
                string who = step.From == step.To || string.IsNullOrEmpty(step.FromName) ? step.ToName : step.FromName + " → " + step.ToName;
                labels.Add(new HillLabel { Text = step.Year + " · " + who, At = at + Vector3.up * 3, Role = LabelRole.Note, Part = "advantage.thread", Rank = 2.5f, Color = ThreadColor });
            }

            foreach (var (at, fp) in historySparks.Where(_ => History))
                labels.Add(new HillLabel { Text = fp.Year + " " + fp.Title, At = at + Vector3.up * 3.5f, Role = LabelRole.Note, Part = "advantage.thread", Rank = 1.5f, Color = TriadColor(fp.Triad) });

            for (int k = 0; k < model.Bands.Count; k++)
                labels.Add(new HillLabel { Text = model.Bands[k].Name.ToUpperInvariant(), At = new Vector3(layout.EconomyX + 30, 3, layout.BandZ[k]), Role = LabelRole.Note, Part = "advantage.floor", Rank = 1, Color = Roles.Encoded(Roles.InkDim) });
            labels.Add(new HillLabel { Text = "CAPITAL", At = new Vector3(layout.CapitalX + 10, 3, layout.FrontZ + 6), Role = LabelRole.Name, Part = "advantage.floor", Rank = 4, Color = Roles.Encoded(Roles.InkDim) });
            labels.Add(new HillLabel { Text = "THE REST OF THE ECONOMY", At = new Vector3(layout.EconomyX - 10, 3, layout.FrontZ + 6), Role = LabelRole.Name, Part = "advantage.floor", Rank = 4, Color = Roles.Encoded(Roles.InkDim) });
            labels.Add(new HillLabel { Text = "THE STATES BENEATH", At = new Vector3(AdvantageLayout.X0, 3, layout.CrestZ + AdvantageLayout.StateZ - 16), Role = LabelRole.Name, Part = "advantage.floor", Rank = 4, Color = Roles.Encoded(Roles.InkDim) });
        }

        static string Short(string s, int words)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var w = s.Split(' ');
            return w.Length <= words ? s : string.Join(" ", w.Take(words)) + "…";
        }

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
            float best = 18 * camera.pixelHeight / 1080f;
            foreach (var p in layout.Places)
            {
                // a peak answers at its summit and at its middle
                for (int k = 0; k < 2; k++)
                {
                    Vector3 at = p.Base + Vector3.up * (HeightNow(p) * (k == 0 ? 1 : .45f));
                    Vector3 screen = camera.WorldToScreenPoint(transform.TransformPoint(at));
                    if (screen.z <= 0) continue;
                    float d = Vector2.Distance(pointer, screen);
                    if (d < best) { best = d; Hovered = p.Actor.Id; }
                }
            }

            if (PointerInput.PressedThisFrame) pressed = pointer;
            if (PointerInput.ReleasedThisFrame && Vector2.Distance(pressed, pointer) < 5) Select(Hovered == Selected ? null : Hovered);
        }

        // ------------------------------------------------------------------ checks and the smoke sequence

        void Log()
        {
            string overlaps = layout.Overlaps(out int n);
            Debug.Log($"[Why] advantage {model.AsOf}: {layout.Places.Count} actors ({peakCount} peaks, {unjudged} not judged, {unsized} without a verified size), " +
                      $"{model.Flows.Count} rivers, {chokeCount} choke points, {model.Flashpoints.Count} flashpoints ({model.Flashpoints.Count(f => f.InArena)} in the arena), " +
                      $"{model.Scenarios.Count} scenarios; overlaps {n}{(n > 0 ? " (" + overlaps + ")" : "")}; arena x {layout.MinX:0}..{layout.MaxX:0}, depth {layout.FrontZ - layout.CrestZ:0}");
            if (n > 0) Debug.LogError($"[Why] advantage: {n} footprints overlap ({overlaps})");
            if (model.Actors.Count == 0) Debug.LogError("[Why] advantage: no actors");
            foreach (var a in model.Actors)
                if (a.Sized && !model.Sources.ContainsKey(a.SizeFact ?? ""))
                    Debug.LogError($"[Why] advantage: {a.Id}'s size has no verified source ({a.SizeFact})");
        }

        /// <summary>
        /// <c>-smokeextra advantage</c>: the view at rest (economy-advantage-now), a scenario (the first: its frame, and how many
        /// altitudes it moved, at least five), one actor selected (NVIDIA where present); the renderers it keeps (at most
        /// <see cref="MaxRenderers"/>) and the longest frame of the choreography and of a scenario's move (a warning over 50 ms).
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

            // the catalog may have left another world on screen: start from now, then time a real move
            SetScenario(Advantage.Now);
            instantMoves = false;
            yield return new WaitForSecondsRealtime(1.5f);
            Log();
            yield return SmokeChecks.Capture("economy-advantage-now", root.CurrentPreset);
            int active = 0;
            foreach ((Renderer r, Part _) in renderers) if (r && r.enabled && r.gameObject.activeInHierarchy) active++;
            Debug.Log($"[Why] advantage: {active} renderers active (at most {MaxRenderers})");
            if (active > MaxRenderers) Debug.LogError($"[Why] advantage: {active} renderers active, more than {MaxRenderers}");

            var first = model.Scenarios.FirstOrDefault(s => s.Id == "taiwan") ?? model.Scenarios.FirstOrDefault();
            if (first != null)
            {
                int moved = layout.Places.Count(p => Mathf.Abs(layout.Height(p, first.Id) - layout.Height(p, Advantage.Now)) > .5f);
                float longest = 0;
                SetScenario(first.Id);
                for (float t = 0; t < ScenarioSeconds + .6f; t += Time.unscaledDeltaTime)
                {
                    yield return null;
                    longest = Mathf.Max(longest, Time.unscaledDeltaTime);
                }

                Debug.Log($"[Why] advantage scenario {first.Id}: {moved} altitudes moved; longest frame {1000 * longest:0} ms");
                if (moved < 5) Debug.LogError($"[Why] advantage: scenario {first.Id} moved {moved} altitudes, fewer than 5");
                if (longest > .05f) Debug.LogWarning($"[Why] advantage: a scenario frame took {1000 * longest:0} ms, over 50");
                yield return SmokeChecks.Capture("economy-advantage-" + first.Id, root.CurrentPreset);
                SetScenario(Advantage.Now);
                yield return new WaitForSecondsRealtime(ScenarioSeconds + .3f);
            }

            ShowHistory(true);
            yield return new WaitForSecondsRealtime(2.4f);
            Debug.Log($"[Why] advantage history: {threadBeads.Count} steps on the thread, {historySparks.Count} contests on the wave");
            if (threadBeads.Count == 0) Debug.LogError("[Why] advantage: the lineage thread has no steps");
            yield return SmokeChecks.Capture("economy-advantage-history", root.CurrentPreset);
            ShowHistory(false);
            yield return new WaitForSecondsRealtime(2f);

            string pick = layout.Get("nvidia") != null ? "nvidia" : layout.Places.FirstOrDefault(p => !p.Plain && !p.State)?.Actor.Id;
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

        void Drop(MeshRenderer r)
        {
            if (!r) return;
            renderers.RemoveAll(x => x.renderer == r);
            var mf = r.GetComponent<MeshFilter>();
            if (mf && mf.sharedMesh) { owned.Remove(mf.sharedMesh); Destroy(mf.sharedMesh); }
            // its material too: a scenario's move rebuilds the peaks about 24 times a second
            if (r.sharedMaterial) { owned.Remove(r.sharedMaterial); Destroy(r.sharedMaterial); }
            Destroy(r.gameObject);
        }

        static void Quad(SurfaceMeshBuilder b, float id, Vector3 a0, Vector3 a1, Vector3 b0, Vector3 b1, Color color) =>
            b.AddBand(new[] { a0, a1 }, new[] { b0, b1 }, new[] { (Color32)color }, id);

        static void Ring(LineMeshBuilder b, float id, Vector3 at, float radius, Color c, float widthPx, float intensity, bool dashed = false)
        {
            const int n = 40;
            var pts = new List<LinePoint>(n + 1);
            for (int i = 0; i <= n; i++)
            {
                float a = i * Mathf.PI * 2 / n;
                pts.Add(new LinePoint(at + new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius), dashed && (i / 2) % 2 == 1 ? WithAlpha(c, 0) : c, widthPx, 0, intensity));
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
            // solid surfaces write depth, so a peak hides what stands behind it and its own back faces
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

        void Clear()
        {
            foreach (UnityEngine.Object o in owned) if (o) Destroy(o);
            if (content) Destroy(content);
            owned.Clear();
            renderers.Clear();
            labels.Clear();
            Array.Clear(partRenderer, 0, partRenderer.Length);
            beds = null;
            content = null;
            layout = null;
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
