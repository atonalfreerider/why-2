using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;
using Roles = Why.Economy.EconomyStyle.Roles;

namespace Why.Economy.Draw
{
    /// <summary>
    /// The palette's check on rendered pixels (spec 3.2, S02; decision 9), in the player's smoke test only:
    /// <c>-smoketest -smokeextra palette</c> runs it after the economy's views and tour. It flies a smoke-only "palette" view
    /// to a card standing far above the scene and draws each role alone as the layers draw it, through the same
    /// post-processing and at intensity 1: alpha-blended Why/Surface swatches coloured by their vertices, as the mark code
    /// draws them, at the frame's centre and at two spots toward its edges where marks still sit (<see cref="Spots"/>), and
    /// a Why/Line line coloured by its material, as a layer that colours its material draws it. It reads the rendered
    /// pixels back, converts them to OKLab and simulates deuteranopia and protanopia (Machado 2009, severity 1), and logs the
    /// separation of every pair that can touch, at each spot and on the lines: under 8 is an error, and under 15 for desire
    /// and fear. The known weak pairs are listed with the placement rule that keeps them apart. A role whose line reads apart
    /// from its swatch is an error too: one of the two ways a role reaches the screen converts it. Then the card
    /// (economy-view-palette.png) shows every role and ramp, the mark code's forms, hatch and dash at several settings and the
    /// shared ribbon, and the ribbon's test is measured on it: width = amount × scale within 2%, and a ribbon narrower than
    /// 1.5 px drawn as a 1.5 px hairline. The roles are measured on the scene's background with nothing highlighted. The
    /// post-processing in effect is logged first: a tonemapper that compresses light colours (URP's Neutral maps linear 1.0
    /// to 0.63) draws the pairs closer than their sRGB targets, and a vignette darkens them toward the frame's edges, which
    /// <c>Tools/check-palette.py --pairs --tonemap neutral</c> and its <c>--vignette</c> predict.
    /// </summary>
    [GraphScenes(EconomyLayouts.HillsScene)]
    public sealed class PaletteCheck : GraphModule
    {
        /// <summary>The sequence's name (<c>-smokeextra palette</c>) and the view's id (the capture economy-view-palette.png).</summary>
        public const string Name = "palette";

        /// <summary>
        /// Where the card stands: thousands of units above the wave, the land and the stage, so a level camera looking along +z
        /// sees the card on the scene's background and nothing else.
        /// </summary>
        static readonly Vector3 CardAt = new Vector3(0, 3000, 0);

        /// <summary>The card's extent (world units, its labels included) and the share of the frame's width and height it fills.</summary>
        const float CardWidth = 10.4f, CardHeight = 6.2f, FillWidth = 0.68f, FillHeight = 0.7f;

        /// <summary>Render queue of the card's fills (its lines one above): after every layer's.</summary>
        const int Queue = 3290;

        /// <summary>
        /// The probe of one role: a swatch this share of the frame's height at each of <see cref="Spots"/>, and a line this
        /// many pixels wide one swatch right of the centre's (vertically centred, so a capture read upside down would miss
        /// neither).
        /// </summary>
        const float ProbeSwatch = 0.16f, ProbeLinePx = 16f;

        /// <summary>
        /// Where a role's swatches stand, as shares of the frame's width and height from its centre: the centre, and two spots
        /// where marks still sit, most of the way to the right edge and toward the bottom right corner (clear of the HUD's
        /// blocks at 16:9). The vignette darkens the frame toward its edges and draws touching pairs closer there, so each
        /// pair must hold its floor at every spot.
        /// </summary>
        static readonly Vector2[] Spots = { Vector2.zero, new Vector2(0.42f, 0f), new Vector2(0.3f, -0.3f) };

        /// <summary>
        /// The middle share of a swatch's side that is read at the centre and at the other spots (less there, where the
        /// vignette shades across a patch), and how far (of 255) a pixel there may stray from the patch's median before the
        /// patch counts as covered by something else (a label, a panel).
        /// </summary>
        const float PatchShare = 0.4f, SpotPatchShare = 0.2f;

        const int PatchSpread = 12;

        /// <summary>
        /// How far (OKLab distance × 100) a role's line, coloured by its material, may read from its centre swatch, coloured
        /// by its vertices: the two stand a little apart in the vignette and round to bytes differently.
        /// </summary>
        const double PathTolerance = 2;

        /// <summary>Floors of a touching pair's separation, and of desire and fear's (OKLab distance × 100; spec 3.2).</summary>
        const double PairFloor = 8, DesireFearFloor = 15;

        /// <summary>
        /// The ribbon test: world units per $T on the card; the wide ribbon's amount ($T); the thin ribbon's width at its scale
        /// (px, under <see cref="FlowRibbon.HairlinePx"/>); how far the wide one's width may stray (2%, spec 3.3) and the
        /// hairline's (px).
        /// </summary>
        const float RibbonScale = 0.32f, ThinAtScalePx = 0.6f, WidthTolerance = 0.02f, HairlineTolerancePx = 0.25f;

        const double WideAmount = 1.0;

        /// <summary>How far a measured hatch or dash's drawn share may stray from its duty.</summary>
        const float DutyTolerance = 0.12f;

        /// <summary>A highlight id nothing lights (the highlighter's ranges start at 0), so no glow or dim reaches the card.</summary>
        const float NoId = -1f;

        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        /// <summary>The roles measured, by the names of spec 3.2's pairs.</summary>
        static readonly (string name, Color color)[] Measured =
        {
            ("owners", Roles.Owners), ("people", Roles.People), ("desire", Roles.Desire), ("fear", Roles.Fear),
            ("state", Roles.State), ("attention", Roles.Attention), ("commerce", Roles.Commerce), ("conflict", Roles.Conflict),
            ("matter", Roles.Matter), ("life", Roles.Life)
        };

        /// <summary>The pairs that can touch: spec 3.2's table, and people-commerce from the rules every package obeys (spec 0, rule 5).</summary>
        static readonly (string a, string b)[] Touching =
        {
            ("desire", "fear"), ("people", "fear"), ("desire", "people"), ("owners", "desire"), ("owners", "people"),
            ("state", "people"), ("desire", "state"), ("owners", "commerce"), ("people", "conflict"), ("desire", "conflict"),
            ("state", "conflict"), ("commerce", "fear"), ("people", "commerce")
        };

        /// <summary>The known weak pairs, kept apart by placement (spec 3.2): measured and listed with their rule, never failed.</summary>
        static readonly (string a, string b, string rule)[] Weak =
        {
            ("attention", "fear", "the cloud's points and the spending lanes never touch"),
            ("matter", "life", "the two never appear together in the new views")
        };

        GraphRoot root;

        public override void Init(GraphRoot root)
        {
            // a test card: it exists only in a smoke run, and runs only when -smokeextra names it
            if (!Environment.GetCommandLineArgs().Contains("-smoketest"))
            {
                Destroy(gameObject);
                return;
            }

            this.root = root;
            SmokeChecks.RegisterSequence(Name, Run());
        }

        IEnumerator Run()
        {
            if (Xr.XrSession.Active)
            {
                Debug.LogWarning("[Why] palette: not measured with -xr (the check reads a flat screen's frame)");
                yield break;
            }

            Camera cam = root.Rig.Cam;
            var made = new List<Object>();
            var labels = new List<LabelSpec>();
            GameObject probe = null, card = null;
            try
            {
                // the view, and nothing highlighted (a dim would darken every role alike); the tour's panel, which the
                // sequence follows, fades out meanwhile
                float distance = FitDistance(cam.aspect);
                ViewPreset view = Preset(distance);
                root.Focus(view, 0f);
                Highlighter.Clear();
                yield return new WaitForSecondsRealtime(1f);
                for (float waited = 0; waited < 3f && Shader.GetGlobalFloat("_WhyDim") > 0.002f; waited += Time.unscaledDeltaTime) yield return null;
                yield return null;
                float dim = Shader.GetGlobalFloat("_WhyDim");
                if (dim > 0.002f) Debug.LogError($"[Why] palette: a highlight still dims the frame by {dim:0.000}, so the roles read darker than drawn");

                // the frame's height at the card (world), from where the camera stands (a portrait screen may step it back)
                float frame = 2f * root.Rig.Pose.Distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                float ppu = root.Rig.PixelsPerUnit(CardAt);
                (string post, bool tonemapped, float vignette, float smoothness) = PostProcessing();
                Debug.Log($"[Why] palette: {cam.pixelWidth}x{cam.pixelHeight}, {ppu.ToString("0.0", Ci)} px per unit at the card; " +
                          $"post-processing: {post}; each role drawn alone as the layers draw it (alpha-blended surfaces coloured by " +
                          $"their vertices {string.Join(", ", Enumerable.Range(0, Spots.Length).Select(SpotName))} of the frame, and a " +
                          $"{ProbeLinePx.ToString("0", Ci)} px line coloured by its material, intensity 1) and read back after post-processing");

                // 1. each role alone, at the same spots
                probe = Holder("Palette probe");
                var rendered = new Dictionary<string, (Rgb[] surfaces, Rgb line)>();
                var problems = new List<string>();
                float side = ProbeSwatch * frame;
                Vector3[] spots = SpotPoints(cam);
                foreach ((string name, Color color) in Measured)
                {
                    GameObject one = Probe(probe.transform, color, side, ppu, spots, made);
                    yield return null;
                    yield return null;
                    yield return new WaitForEndOfFrame();
                    Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
                    try
                    {
                        var surfaces = new Rgb[spots.Length];
                        int spread = 0;
                        for (int s = 0; s < spots.Length; s++)
                        {
                            float half = 0.5f * (s == 0 ? PatchShare : SpotPatchShare) * side;
                            Patch patch = Read(shot, Pixels(cam, shot, spots[s], half, half));
                            surfaces[s] = patch.Linear;
                            spread = Math.Max(spread, patch.Spread);
                        }

                        Vector2 at = ToPixels(cam, shot, spots[0] + new Vector3(side, 0, 0));
                        float rows = 0.25f * side * ppu * shot.height / Mathf.Max(1, cam.pixelHeight);
                        Patch line = Read(shot, new RectInt(Mathf.RoundToInt(at.x) - 3, Mathf.RoundToInt(at.y - rows), 7, Mathf.Max(1, Mathf.RoundToInt(2 * rows))));
                        spread = Math.Max(spread, line.Spread);
                        if (spread > PatchSpread)
                            problems.Add($"{name}'s probe is not uniform where it is read (pixels stray by {spread} of 255): something covers it");
                        double drift = Distance(surfaces[0], line.Linear);
                        if (drift > PathTolerance)
                            problems.Add($"{name} reads {Hex(line.Linear)} on a line coloured by its material but {Hex(surfaces[0])} on a swatch coloured by " +
                                         $"its vertices ({drift.ToString("0.0", Ci)} apart): one of the two converts the role, which both should take as it is " +
                                         "(EconomyStyle.Roles)");
                        rendered[name] = (surfaces, line.Linear);
                        Debug.Log($"[Why] palette role {name}: target {Hex(Rgb.Of(color))}, rendered surface {Hex(surfaces[0])} (vertex colour), " +
                                  $"line {Hex(line.Linear)} (material colour); surface " +
                                  string.Join(", ", Enumerable.Range(1, spots.Length - 1).Select(s => SpotName(s) + " " + Hex(surfaces[s]))));
                    }
                    finally
                    {
                        Destroy(shot);
                    }

                    Destroy(one);
                }

                Destroy(probe);
                probe = null;

                // how much light the post-processing leaves at the spots: the attention swatch's luminance there over the centre's
                Rgb[] white = rendered["attention"].surfaces;
                Debug.Log("[Why] palette: light at the spots, of the centre's (the attention swatch's luminance): " +
                          string.Join(", ", Enumerable.Range(1, Spots.Length - 1).Select(s =>
                              SpotName(s) + " " + (Luminance(white[s]) / Math.Max(1e-6, Luminance(white[0]))).ToString("0.00", Ci))));

                // 2. the pairs, on the rendered pixels at each spot and on the lines; the targets beside them (the spec's numbers,
                // computed the same way)
                int below = 0, belowAtCentre = 0;
                foreach ((string a, string b) in Touching)
                {
                    double floor = a == "desire" && b == "fear" ? DesireFearFloor : PairFloor;
                    if (Pair(a, b, rendered, floor, problems, out bool centre)) continue;
                    below++;
                    if (!centre) belowAtCentre++;
                }

                foreach ((string a, string b, string rule) in Weak) WeakPair(a, b, rendered, rule);
                foreach (string p in problems) Debug.LogError("[Why] palette: " + p);
                // what draws the pairs closer than their targets, when they fail: the frame's post-processing, not the palette
                if (belowAtCentre > 0 && tonemapped)
                    Debug.LogWarning("[Why] palette: the frame goes through a tonemapper, which compresses light colours (URP's Neutral maps " +
                                     "linear 1.0 to 0.63) and draws the pairs closer than their targets (Tools/check-palette.py --pairs --tonemap " +
                                     "neutral predicts them); the marks are LDR and need none (GraphRoot.SetupPostProcessing)");
                if (below > belowAtCentre && vignette > 0)
                    Debug.LogWarning("[Why] palette: the vignette darkens the frame toward its edges and draws pairs under their floors where " +
                                     "marks still sit (Tools/check-palette.py --pairs --tonemap none --vignette " +
                                     string.Format(Ci, "{0:0.##},{1:0.##}", vignette, smoothness) + " predicts them); the hills need it gone or " +
                                     "weaker (GraphRoot.SetupPostProcessing)");
                int pairProblems = problems.Count;
                problems.Clear();

                // 3. the card: every role and ramp, the mark code, hatch and dash, the ribbon; then its tests on the capture
                card = Card(ppu, made, labels, out CardTargets targets);
                root.Labels.MarkDirty();
                yield return new WaitForSecondsRealtime(1.5f);
                yield return SmokeChecks.Capture($"{GraphScene.Current}-view-{Name}", view);
                yield return new WaitForEndOfFrame();
                Texture2D cardShot = ScreenCapture.CaptureScreenshotAsTexture();
                int failedTests;
                try
                {
                    failedTests = CardTests(cardShot, cam, ppu, targets, problems);
                }
                finally
                {
                    Destroy(cardShot);
                }

                foreach (string p in problems) Debug.LogError("[Why] palette: " + p);
                Debug.Log($"[Why] palette: {Touching.Length} touching pairs measured on surfaces at {Spots.Length} spots and on lines, {below} below their floor " +
                          $"({pairProblems} problems); known weak pairs listed: {string.Join(", ", Weak.Select(w => w.a + "-" + w.b))}; " +
                          "ribbon, hairline, hatch and dash tests: " + (failedTests == 0 ? "passed" : failedTests + " failed"));
            }
            finally
            {
                foreach (LabelSpec s in labels) s.Hidden = true;
                if (labels.Count > 0) root.Labels.MarkDirty();
                if (probe != null) Destroy(probe);
                if (card != null) Destroy(card);
                foreach (Object o in made)
                {
                    if (o != null) Destroy(o);
                }

                root.Focus(ViewPresets.Get("overview"), 0f);
            }
        }

        // ------------------------------------------------------------------ the view

        /// <summary>How far back the camera stands so the card fills <see cref="FillWidth"/> and <see cref="FillHeight"/> of the frame.</summary>
        static float FitDistance(float aspect)
        {
            float span = 2f * Mathf.Tan(CameraRig.FieldOfView * 0.5f * Mathf.Deg2Rad);
            return Mathf.Max(CardWidth / (FillWidth * span * Mathf.Max(0.1f, aspect)), CardHeight / (FillHeight * span));
        }

        /// <summary>
        /// The smoke-only "palette" view (not in the catalog, so no other run shows it): the overview's lens, so the warp does
        /// not move, and a level camera in front of the card.
        /// </summary>
        static ViewPreset Preset(float distance)
        {
            ViewPreset o = ViewPresets.Get("overview");
            return new ViewPreset
            {
                Id = Name, Title = "Palette check",
                Subtitle = "Each colour role as the layers draw it, the mark code, hatch and dash, and the shared ribbon (smoke test only)",
                Polar = o.Polar, PolarArc = o.PolarArc, YaOld = o.YaOld, YaNew = o.YaNew, LogOffset = o.LogOffset, Length = o.Length,
                RhoScale = o.RhoScale, YScale = o.YScale, Portrait = o.Portrait, StrataEmphasis = o.StrataEmphasis,
                MatterEmphasis = o.MatterEmphasis, TargetRho = o.TargetRho, TargetY = o.TargetY, TargetYear = o.TargetYear,
                FixedTarget = CardAt, FixedYaw = 0, Pitch = 0, Distance = distance, PortraitWidth = CardWidth / FillWidth
            };
        }

        /// <summary>
        /// The post-processing every layer is drawn through, as the root's volume sets it: a line for the log, whether a
        /// tonemapper is on, and the vignette's intensity and smoothness (0 without one).
        /// </summary>
        (string text, bool tonemapped, float vignette, float smoothness) PostProcessing()
        {
            string tone = "none", bloom = "none", vignette = "none";
            bool tonemapped = false;
            float intensity = 0, smoothness = 0;
            foreach (Volume v in root.GetComponentsInChildren<Volume>(true))
            {
                VolumeProfile p = v.sharedProfile;
                if (p == null || !v.isActiveAndEnabled) continue;
                if (p.TryGet(out Tonemapping t) && t.active)
                {
                    tone = t.mode.value.ToString();
                    tonemapped = t.mode.value != TonemappingMode.None;
                }

                if (p.TryGet(out Bloom b) && b.active)
                    bloom = string.Format(Ci, "threshold {0:0.##} intensity {1:0.##}", b.threshold.value, b.intensity.value);
                if (p.TryGet(out Vignette g) && g.active && g.intensity.value > 0)
                {
                    intensity = g.intensity.value;
                    smoothness = g.smoothness.value;
                    vignette = string.Format(Ci, "intensity {0:0.##} smoothness {1:0.##}", intensity, smoothness);
                }
            }

            return ($"tonemapping {tone}, bloom {bloom}, vignette {vignette}", tonemapped, intensity, smoothness);
        }

        /// <summary>
        /// The spots on the card's plane (world) where the camera sees them now: <see cref="Spots"/> of its frame from the
        /// frame's centre, on the plane through the card square to the view.
        /// </summary>
        static Vector3[] SpotPoints(Camera cam)
        {
            Transform t = cam.transform;
            float depth = Vector3.Dot(CardAt - t.position, t.forward);
            var points = new Vector3[Spots.Length];
            for (int s = 0; s < Spots.Length; s++) points[s] = cam.ViewportToWorldPoint(new Vector3(0.5f + Spots[s].x, 0.5f + Spots[s].y, depth));
            return points;
        }

        /// <summary>A spot as the log names it: "at the centre", "at (0.42, 0)".</summary>
        static string SpotName(int spot) =>
            spot == 0 ? "at the centre" : string.Format(Ci, "at ({0:0.##}, {1:0.##})", Spots[spot].x, Spots[spot].y);

        /// <summary>
        /// A root object at the card (no parent, so nothing scales or turns it): its children's object space is the card's
        /// plane, world = <see cref="CardAt"/> + local, as the tests project it.
        /// </summary>
        static GameObject Holder(string name)
        {
            GameObject go = new GameObject(name);
            go.transform.SetPositionAndRotation(CardAt, Quaternion.identity);
            return go;
        }

        /// <summary>
        /// One role's probe: a swatch at each spot (world) as the mark code draws it, the role in its vertex colours on a white
        /// material; and a line one swatch right of the centre's as a layer that colours its material draws it, white vertices
        /// and the role as the Why/Line material's colour. So both ways a role reaches the screen are read.
        /// </summary>
        static GameObject Probe(Transform parent, Color color, float side, float ppu, Vector3[] spots, List<Object> made)
        {
            GameObject one = new GameObject("Probe");
            one.transform.SetParent(parent, false);
            var marks = new MarkMeshes(ppu);
            foreach (Vector3 spot in spots)
                MarkCode.Add(marks, Mark.Measured, MarkQuad.Rect(spot - CardAt, Vector3.right, Vector3.up, side, side), color, NoId);
            marks.Upload(one.transform, "Probe", Queue, made);
            var line = new LineMeshBuilder();
            Vector3 at = spots[0] - CardAt + new Vector3(side, 0, 0), half = new Vector3(0, 0.5f * side, 0);
            line.AddPolyline(new[] { at - half, at + half }, Color.white, ProbeLinePx, 0, NoId);
            Show(one.transform, "Probe line", line.ToMesh("Probe line"), GraphMaterials.Raw(GraphMaterials.Line(color, 1, Queue + 1, false)), made);
            return one;
        }

        // ------------------------------------------------------------------ the card

        /// <summary>Where the card's tests read the capture (world), and what they expect.</summary>
        sealed class CardTargets
        {
            public Vector3 Wide, Thin, Hatch, DashFrom, DashTo;
            public float WideWidth, HatchHalfWidth;
            public double ThinAmount;
            public RibbonResult WideDrawn, ThinDrawn;
        }

        /// <summary>
        /// The card, in rows from the top: the roles (swatch and line, with state's base), the ramps, ink, missing and the
        /// chart series; the mark code's forms; hatch and dash at several settings beside a fill and a line at their defaults;
        /// the shared ribbon (the wide and thin test ribbons, motive lanes with a seam, a judged ribbon). Labels go through the
        /// label system, hidden again when the check ends.
        /// </summary>
        GameObject Card(float ppu, List<Object> made, List<LabelSpec> labels, out CardTargets targets)
        {
            GameObject card = Holder("Palette card");
            Transform t = card.transform;
            var marks = new MarkMeshes(ppu);
            targets = new CardTargets();

            // the roles
            (string name, Color color)[] roles =
            {
                ("owners", Roles.Owners), ("people", Roles.People), ("desire", Roles.Desire), ("fear", Roles.Fear),
                ("state", Roles.State), ("state base", Roles.StateBase), ("attention", Roles.Attention), ("commerce", Roles.Commerce),
                ("conflict", Roles.Conflict), ("matter", Roles.Matter), ("life", Roles.Life)
            };
            for (int i = 0; i < roles.Length; i++)
            {
                float x = (i - 5) * 0.93f;
                MarkCode.Add(marks, Mark.Measured, MarkQuad.Rect(new Vector3(x, 2.45f, 0), Vector3.right, Vector3.up, 0.7f, 0.5f), roles[i].color, NoId);
                MarkCode.Line(marks, new[] { new Vector3(x - 0.35f, 2.06f, 0), new Vector3(x + 0.35f, 2.06f, 0) }, roles[i].color, 4f, NoId);
                Label(labels, roles[i].name, new Vector3(x, 2.06f, 0), Roles.InkText);
            }

            // the ramps, the year shown, missing, the ink and the chart series, in groups
            (string name, Color[] colors)[] groups =
            {
                ("tier ramp: raw to tech", Roles.TierRamp.ToArray()), ("class ramp: bottom 20% to top 1%", Roles.ClassRamp.ToArray()),
                ("year shown · missing", new[] { Roles.YearShown, Roles.Missing }),
                ("ink: text, dim, rule, backing", new[] { Roles.InkText, Roles.InkDim, Roles.InkRule, Roles.InkBacking }),
                ("chart series", Roles.Series.ToArray())
            };
            const float Small = 0.34f, Step = 0.42f, GroupGap = 0.36f;
            float width = groups.Sum(g => g.colors.Length * Step - (Step - Small)) + (groups.Length - 1) * GroupGap;
            float left = -0.5f * width;
            foreach ((string name, Color[] colors) in groups)
            {
                for (int k = 0; k < colors.Length; k++)
                {
                    float x = left + 0.5f * Small + k * Step;
                    MarkCode.Add(marks, Mark.Measured, MarkQuad.Rect(new Vector3(x, 1.3f, 0), Vector3.right, Vector3.up, Small, Small), colors[k], NoId);
                }

                float groupWidth = colors.Length * Step - (Step - Small);
                Label(labels, name, new Vector3(left + 0.5f * groupWidth, 1.3f - 0.5f * Small, 0), Roles.InkDim);
                left += groupWidth + GroupGap;
            }

            // the mark code's forms
            float yForms = 0.12f;
            Vector3 Slot(int k) => new Vector3((k - 4) * 1.15f, yForms, 0);
            MarkQuad Form(int k) => MarkQuad.Rect(Slot(k), Vector3.right, Vector3.up, 0.8f, 0.46f);
            MarkCode.Add(marks, Mark.Measured, Form(0), Roles.People, NoId);
            MarkCode.Add(marks, Mark.Judged, Form(1), Roles.People, NoId, 0.74f);
            MarkCode.Add(marks, Mark.Judged, Form(2), Roles.People, NoId, 0.45f);
            MarkCode.Add(marks, Mark.Modelled, Form(3), Roles.People, NoId);
            MarkCode.Add(marks, Mark.Owed, Form(4), Roles.State, NoId);
            MarkCode.Add(marks, Mark.NoData, Form(5), Roles.Missing, NoId);
            MarkCode.Add(marks, Mark.Ghost, Form(6), Roles.People, NoId);
            MarkCode.Strand(marks, new[] { Slot(8) + new Vector3(-0.4f, -0.15f, 0), Slot(8) + new Vector3(0.4f, 0.15f, 0) }, Roles.Attention, NoId);
            string[] forms =
            {
                "measured", MarkCode.Judged("judged", 0.74f), MarkCode.JudgedTag(0.45f) + ": not drawn", MarkCode.Modelled(" modelled"),
                MarkCode.Owes + " (hatch)", MarkCode.NoData, MarkCode.Ghost(2021) + " (earlier)", null, MarkCode.NotMoney
            };
            for (int k = 0; k < forms.Length; k++)
            {
                if (forms[k] != null) Label(labels, forms[k], Slot(k) + new Vector3(0, -0.23f, 0), Roles.InkText);
            }

            LabelSpec chip = MarkCode.Chip("a drawing choice", CardAt + Slot(7));
            chip.FixedRange = 60;
            root.Labels.Add(chip);
            labels.Add(chip);

            // hatch and dash at several settings, beside a fill and a line left at the parameters' defaults
            float yStrip = -1.0f;
            Vector3 Cell(int k) => new Vector3(-4.6f + k * 1.0f, yStrip, 0);
            Swatch(t, "Strip fill", Cell(0), Roles.Owners, m => m, made);
            Swatch(t, "Strip hatch", Cell(1), Roles.Owners, m => GraphMaterials.SetHatch(m, MarkCode.HatchPeriodPx, MarkCode.HatchDuty, MarkCode.HatchStrength), made);
            Swatch(t, "Strip hatch wide", Cell(2), Roles.Owners, m => GraphMaterials.SetHatch(m, 12f, 0.3f, 1f), made);
            Swatch(t, "Strip hatch faint", Cell(3), Roles.Owners, m => GraphMaterials.SetHatch(m, MarkCode.HatchPeriodPx, MarkCode.HatchDuty, 0.5f), made);
            targets.Hatch = Cell(1);
            targets.HatchHalfWidth = 0.4f;
            float[] lineAt = { -0.4f, 0.95f, 2.3f, 3.65f };
            Strip(t, "Strip line", lineAt[0], yStrip, ppu, m => m, made);
            Strip(t, "Strip dash", lineAt[1], yStrip, ppu, m => GraphMaterials.SetDash(m, MarkCode.DashPeriodPx, MarkCode.DashDuty), made);
            Strip(t, "Strip dots", lineAt[2], yStrip, ppu, m => GraphMaterials.SetDash(m, MarkCode.DotPeriodPx, MarkCode.DotDuty), made);
            Strip(t, "Strip dash long", lineAt[3], yStrip, ppu, m => GraphMaterials.SetDash(m, 16f, 0.5f), made);
            targets.DashFrom = new Vector3(lineAt[1], yStrip, 0);
            targets.DashTo = new Vector3(lineAt[1] + 1.1f, yStrip, 0);
            string[] strip =
            {
                "fill (defaults)", Setting("hatch", MarkCode.HatchPeriodPx, MarkCode.HatchDuty), Setting("hatch", 12f, 0.3f), "hatch, faint gaps",
                "line (defaults)", Setting("dash", MarkCode.DashPeriodPx, MarkCode.DashDuty), Setting("dots", MarkCode.DotPeriodPx, MarkCode.DotDuty),
                Setting("dash", 16f, 0.5f)
            };
            for (int k = 0; k < 4; k++) Label(labels, strip[k], Cell(k) + new Vector3(0, -0.23f, 0), Roles.InkDim);
            for (int k = 0; k < 4; k++) Label(labels, strip[4 + k], new Vector3(lineAt[k] + 0.55f, yStrip - 0.08f, 0), Roles.InkDim);

            // the shared ribbon: the wide test ribbon ($ × scale), the thin one (a hairline), motive lanes, a judged ribbon
            var wide = new RibbonSpec
            {
                From = new RibbonPort(new Vector3(-4.8f, -1.95f, 0), Vector3.right), To = new RibbonPort(new Vector3(-1.6f, -1.95f, 0), Vector3.right),
                Amount = WideAmount, Scale = RibbonScale, Color = Roles.State, Id = NoId
            };
            targets.WideDrawn = FlowRibbon.Add(marks, wide);
            targets.Wide = new Vector3(-4.3f, -1.95f, 0);
            targets.WideWidth = targets.WideDrawn.Width;
            Label(labels, $"${WideAmount.ToString("0", Ci)}T × {RibbonScale.ToString("0.00", Ci)} = {targets.WideWidth.ToString("0.00", Ci)} units wide",
                new Vector3(-3.2f, -1.95f - 0.5f * targets.WideWidth, 0), Roles.InkDim);
            targets.ThinAmount = ThinAtScalePx / (RibbonScale * ppu);
            var thin = new RibbonSpec
            {
                From = new RibbonPort(new Vector3(-4.8f, -2.55f, 0), Vector3.right), To = new RibbonPort(new Vector3(-1.6f, -2.55f, 0), Vector3.right),
                Amount = targets.ThinAmount, Scale = RibbonScale, Color = Roles.State, Id = NoId
            };
            targets.ThinDrawn = FlowRibbon.Add(marks, thin);
            targets.Thin = new Vector3(-4.3f, -2.55f, 0);
            // (labels stand right of where the tests read the two ribbons, x = -4.3)
            Label(labels, FlowRibbon.Label($"${targets.ThinAmount.ToString("0.000", Ci)}T: under {FlowRibbon.HairlinePx.ToString("0.0", Ci)} px at its scale",
                targets.ThinDrawn), new Vector3(-2.9f, -2.55f, 0), Roles.InkDim);
            var split = new RibbonSpec
            {
                From = new RibbonPort(new Vector3(-0.9f, -1.9f, 0), Vector3.right), To = new RibbonPort(new Vector3(1.9f, -2.6f, 0), Vector3.right),
                Amount = 1.4, Scale = RibbonScale, Lanes = FlowRibbon.Lanes(EconomyStyle.MotiveSplit(1, 0.35f)), Seam = true, Id = NoId
            };
            RibbonResult splitDrawn = FlowRibbon.Add(marks, split);
            Label(labels, "desire and fear lanes, seam " + MarkCode.JudgedTag(0.74f), splitDrawn.Tip + new Vector3(-1.4f, -0.35f, 0), Roles.InkDim);
            var judged = new RibbonSpec
            {
                From = new RibbonPort(new Vector3(2.5f, -1.85f, 0), Vector3.right), To = new RibbonPort(new Vector3(4.6f, -2.6f, 0), new Vector3(0.5f, -1f, 0)),
                Amount = 1.0, Scale = RibbonScale, Color = Roles.Owners, Confidence = 0.74f, Id = NoId
            };
            RibbonResult judgedDrawn = FlowRibbon.Add(marks, judged);
            Label(labels, MarkCode.Judged("judged ribbon", 0.74f), judgedDrawn.Tip + new Vector3(-1.3f, -0.2f, 0), Roles.InkDim);

            marks.Upload(t, "Card", Queue, made);
            return card;
        }

        static string Setting(string what, float periodPx, float duty) =>
            string.Format(Ci, "{0} {1:0} px · {2:0.0#}", what, periodPx, duty);

        /// <summary>
        /// A label under a point of the card (card-local), at the note size, in a role's colour sRGB-encoded as labels take
        /// their colours (<see cref="EconomyStyle.Roles.Encoded"/>), hidden when the check ends.
        /// </summary>
        void Label(List<LabelSpec> labels, string text, Vector3 local, Color role)
        {
            var spec = new LabelSpec
            {
                Text = text, Data = CardAt + local, Fixed = true, FixedRange = 60, SizePx = 12, Color = Roles.Encoded(role),
                Align = TextAlignmentOptions.Center, PixelOffset = new Vector2(0, -12), Priority = 5
            };
            root.Labels.Add(spec);
            labels.Add(spec);
        }

        /// <summary>A fill swatch of the strip (0.8 by 0.46) with its own material, styled by <paramref name="style"/>.</summary>
        static void Swatch(Transform parent, string name, Vector3 centre, Color color, Func<Material, Material> style, List<Object> made)
        {
            var b = new SurfaceMeshBuilder();
            MarkQuad q = MarkQuad.Rect(centre, Vector3.right, Vector3.up, 0.8f, 0.46f);
            Color32 c = color;
            b.AddBand(new[] { q.Origin, q.Origin + q.Right }, new[] { q.Origin + q.Up, q.Origin + q.Right + q.Up }, new[] { c }, NoId);
            Show(parent, name, b.ToMesh(name), style(MarkCode.FillMaterial(Queue)), made);
        }

        /// <summary>A line of the strip (1.1 long, 3 px) built as a path with the card's pixels per unit as its phase.</summary>
        static void Strip(Transform parent, string name, float x, float y, float ppu, Func<Material, Material> style, List<Object> made)
        {
            var b = new LineMeshBuilder();
            b.AddFlowPath(new[] { new LinePoint(new Vector3(x, y, 0), Roles.InkText, 3f), new LinePoint(new Vector3(x + 1.1f, y, 0), Roles.InkText, 3f) },
                NoId, ppu);
            Show(parent, name, b.ToMesh(name), style(MarkCode.LineMaterial(Queue + 1)), made);
        }

        static void Show(Transform parent, string name, Mesh mesh, Material material, List<Object> made)
        {
            made.Add(mesh);
            made.Add(material);
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        // ------------------------------------------------------------------ the tests

        /// <summary>
        /// Logs one touching pair's separations on the rendered surfaces at each spot and on the lines, and the targets', and
        /// adds a problem for each under the floor. True when every one holds; <paramref name="centre"/> is whether those at
        /// the frame's centre (its swatches and the lines beside them) do.
        /// </summary>
        static bool Pair(string a, string b, Dictionary<string, (Rgb[] surfaces, Rgb line)> rendered, double floor, List<string> problems,
            out bool centre)
        {
            (Rgb[] surfacesA, Rgb lineA) = rendered[a];
            (Rgb[] surfacesB, Rgb lineB) = rendered[b];
            bool ok = true;
            centre = true;
            var surfaces = new string[Spots.Length];
            for (int s = 0; s < Spots.Length; s++)
            {
                (double n, double d, double p) surface = Separation(surfacesA[s], surfacesB[s]);
                bool holds = Holds(a, b, "surface " + SpotName(s), surface, floor, problems);
                ok &= holds;
                if (s == 0) centre = holds;
                surfaces[s] = SpotName(s) + " " + Triple(surface);
            }

            (double n, double d, double p) line = Separation(lineA, lineB);
            bool lineHolds = Holds(a, b, "line", line, floor, problems);
            ok &= lineHolds;
            centre &= lineHolds;
            (double n, double d, double p) target = Separation(Rgb.Of(Target(a)), Rgb.Of(Target(b)));
            Debug.Log($"[Why] palette pair {a}-{b}: surface {string.Join(", ", surfaces)}; line {Triple(line)} (normal / deutan / protan, " +
                      $"OKLab x100); targets {Triple(target)}; floor {floor.ToString("0", Ci)}: {(ok ? "ok" : "BELOW")}");
            return ok;
        }

        /// <summary>Whether a pair's separations on one form hold the floor under each vision; a problem for each that does not.</summary>
        static bool Holds(string a, string b, string form, (double n, double d, double p) s, double floor, List<string> problems)
        {
            bool ok = true;
            string[] visions = { "normal vision", "deuteranopia", "protanopia" };
            double[] values = { s.n, s.d, s.p };
            for (int v = 0; v < values.Length; v++)
            {
                if (values[v] >= floor) continue;
                ok = false;
                problems.Add($"{a}-{b} separates by {values[v].ToString("0.0", Ci)} under {visions[v]} on the rendered {form}, " +
                             $"below its floor of {floor.ToString("0", Ci)} (spec 3.2)");
            }

            return ok;
        }

        /// <summary>Logs a known weak pair at each spot with the placement rule that keeps it apart.</summary>
        static void WeakPair(string a, string b, Dictionary<string, (Rgb[] surfaces, Rgb line)> rendered, string rule)
        {
            var surfaces = new string[Spots.Length];
            for (int s = 0; s < Spots.Length; s++)
                surfaces[s] = SpotName(s) + " " + Triple(Separation(rendered[a].surfaces[s], rendered[b].surfaces[s]));
            (double n, double d, double p) target = Separation(Rgb.Of(Target(a)), Rgb.Of(Target(b)));
            Debug.Log($"[Why] palette weak pair {a}-{b}: surface {string.Join(", ", surfaces)}; targets {Triple(target)}; " +
                      $"kept apart by placement: {rule}");
        }

        static Color Target(string role) => Measured.First(r => r.name == role).color;

        static string Triple((double n, double d, double p) s) =>
            string.Format(Ci, "{0:0.0} / {1:0.0} / {2:0.0}", s.n, s.d, s.p);

        /// <summary>
        /// The card's tests on its capture: the wide ribbon's width against amount × scale × pixels per unit (2%), the thin
        /// ribbon drawn as a 1.5 px hairline, and the hatch's and the dash's drawn shares against their duties. Returns how many failed.
        /// </summary>
        static int CardTests(Texture2D shot, Camera cam, float ppu, CardTargets targets, List<string> problems)
        {
            int failed = 0;
            float scale = shot.height / (float)Mathf.Max(1, cam.pixelHeight);
            float expected = targets.WideWidth * ppu * scale;
            float wide = BandWidth(shot, ToPixels(cam, shot, CardAt + targets.Wide), expected, null, out Rgb inside);
            float error = Mathf.Abs(wide / expected - 1f);
            Debug.Log($"[Why] palette ribbon ${WideAmount.ToString("0.00", Ci)}T at {RibbonScale.ToString("0.00", Ci)} units per $T: " +
                      $"{wide.ToString("0.0", Ci)} px wide on screen, $ x scale x {(ppu * scale).ToString("0.0", Ci)} px per unit = {expected.ToString("0.0", Ci)} px, " +
                      $"within {(100 * error).ToString("0.0", Ci)}% ({(100 * WidthTolerance).ToString("0", Ci)}% allowed)");
            if (!(error <= WidthTolerance))
            {
                failed++;
                problems.Add($"the ribbon of ${WideAmount.ToString("0.00", Ci)}T is {wide.ToString("0.0", Ci)} px wide, not {expected.ToString("0.0", Ci)} px within 2% (spec 3.3)");
            }

            float atScale = targets.ThinDrawn.WidthPx * scale, hairPx = FlowRibbon.HairlinePx * scale;
            float thin = BandWidth(shot, ToPixels(cam, shot, CardAt + targets.Thin), hairPx, inside, out _);
            bool hairline = targets.ThinDrawn.Hairline && Mathf.Abs(thin - hairPx) <= HairlineTolerancePx;
            Debug.Log($"[Why] palette ribbon ${targets.ThinAmount.ToString("0.000", Ci)}T: {atScale.ToString("0.00", Ci)} px at its scale, " +
                      $"under {FlowRibbon.HairlinePx.ToString("0.0", Ci)} px, so drawn as a hairline: {thin.ToString("0.00", Ci)} px measured " +
                      $"({FlowRibbon.HairlinePx.ToString("0.0", Ci)} ± {HairlineTolerancePx.ToString("0.00", Ci)}), its label carrying '{FlowRibbon.HairlineMark}'");
            if (!hairline)
            {
                failed++;
                problems.Add($"the ribbon of ${targets.ThinAmount.ToString("0.000", Ci)}T ({atScale.ToString("0.00", Ci)} px at its scale) measures " +
                             $"{thin.ToString("0.00", Ci)} px, not a {FlowRibbon.HairlinePx.ToString("0.0", Ci)} px hairline (spec 3.3)");
            }

            Vector2 hatchLeft = ToPixels(cam, shot, CardAt + targets.Hatch - new Vector3(0.6f * targets.HatchHalfWidth, 0, 0));
            Vector2 hatchRight = ToPixels(cam, shot, CardAt + targets.Hatch + new Vector3(0.6f * targets.HatchHalfWidth, 0, 0));
            failed += Duty(shot, hatchLeft, hatchRight, MarkCode.HatchDuty, $"hatch (a stripe every {MarkCode.HatchPeriodPx.ToString("0", Ci)} px, duty {MarkCode.HatchDuty.ToString("0.00", Ci)}): ",
                "of a row through it drawn", problems);
            Vector2 dashFrom = ToPixels(cam, shot, CardAt + Vector3.Lerp(targets.DashFrom, targets.DashTo, 0.1f));
            Vector2 dashTo = ToPixels(cam, shot, CardAt + Vector3.Lerp(targets.DashFrom, targets.DashTo, 0.9f));
            failed += Duty(shot, dashFrom, dashTo, MarkCode.DashDuty, $"dash (every {MarkCode.DashPeriodPx.ToString("0", Ci)} px, duty {MarkCode.DashDuty.ToString("0.00", Ci)}): ",
                "of the line drawn", problems);
            return failed;
        }

        /// <summary>Logs the share of a row of pixels that is lit against a duty; 1 when it strays too far.</summary>
        static int Duty(Texture2D shot, Vector2 from, Vector2 to, float duty, string what, string of, List<string> problems)
        {
            float share = LitShare(shot, Mathf.RoundToInt(from.x), Mathf.RoundToInt(to.x), Mathf.RoundToInt(0.5f * (from.y + to.y)));
            Debug.Log($"[Why] palette {what}{(100 * share).ToString("0", Ci)}% {of} (±{(100 * DutyTolerance).ToString("0", Ci)}% allowed)");
            if (Mathf.Abs(share - duty) <= DutyTolerance) return 0;
            problems.Add($"the {what}{(100 * share).ToString("0", Ci)}% {of}, not {(100 * duty).ToString("0", Ci)}%");
            return 1;
        }

        // ------------------------------------------------------------------ reading the frame

        /// <summary>A patch of the frame: the median of its pixels in linear light, and how far (of 255) its pixels stray from it.</summary>
        readonly struct Patch
        {
            public readonly Rgb Linear;
            public readonly int Spread;

            public Patch(Rgb linear, int spread)
            {
                Linear = linear;
                Spread = spread;
            }
        }

        /// <summary>Where a world point is in the capture's pixels (origin at the bottom left, as the capture's rows run).</summary>
        static Vector2 ToPixels(Camera cam, Texture2D shot, Vector3 world)
        {
            Vector3 p = cam.WorldToScreenPoint(world);
            return new Vector2(p.x * shot.width / Mathf.Max(1, Screen.width), p.y * shot.height / Mathf.Max(1, Screen.height));
        }

        /// <summary>The capture's pixels under a rectangle of the card's plane (a centre and half its sides, world).</summary>
        static RectInt Pixels(Camera cam, Texture2D shot, Vector3 centre, float halfWidth, float halfHeight)
        {
            Vector2 a = ToPixels(cam, shot, centre - new Vector3(halfWidth, halfHeight, 0));
            Vector2 b = ToPixels(cam, shot, centre + new Vector3(halfWidth, halfHeight, 0));
            int x0 = Mathf.RoundToInt(Mathf.Min(a.x, b.x)), x1 = Mathf.RoundToInt(Mathf.Max(a.x, b.x));
            int y0 = Mathf.RoundToInt(Mathf.Min(a.y, b.y)), y1 = Mathf.RoundToInt(Mathf.Max(a.y, b.y));
            return new RectInt(x0, y0, Mathf.Max(1, x1 - x0), Mathf.Max(1, y1 - y0));
        }

        static RectInt Clamp(Texture2D shot, RectInt r)
        {
            int x0 = Mathf.Clamp(r.x, 0, shot.width - 1), y0 = Mathf.Clamp(r.y, 0, shot.height - 1);
            int x1 = Mathf.Clamp(r.x + r.width, x0 + 1, shot.width), y1 = Mathf.Clamp(r.y + r.height, y0 + 1, shot.height);
            return new RectInt(x0, y0, x1 - x0, y1 - y0);
        }

        static Patch Read(Texture2D shot, RectInt r)
        {
            r = Clamp(shot, r);
            Color[] px = shot.GetPixels(r.x, r.y, r.width, r.height);
            double[] red = new double[px.Length], green = new double[px.Length], blue = new double[px.Length];
            for (int i = 0; i < px.Length; i++)
            {
                red[i] = px[i].r;
                green[i] = px[i].g;
                blue[i] = px[i].b;
            }

            double mr = Median(red), mg = Median(green), mb = Median(blue), spread = 0;
            for (int i = 0; i < px.Length; i++)
            {
                spread = Math.Max(spread, Math.Max(Math.Abs(px[i].r - mr), Math.Max(Math.Abs(px[i].g - mg), Math.Abs(px[i].b - mb))));
            }

            return new Patch(Rgb.FromSrgb(mr, mg, mb), (int)Math.Round(spread * 255));
        }

        static double Median(double[] v)
        {
            Array.Sort(v);
            int n = v.Length;
            return n % 2 == 1 ? v[n / 2] : 0.5 * (v[n / 2 - 1] + v[n / 2]);
        }

        /// <summary>
        /// The width (px) of a horizontal band through a point of the capture: down a few columns, each pixel's coverage read in
        /// linear light between the background beyond the band and the band's full colour (<paramref name="full"/>, or the
        /// band's own middle), summed, then averaged over the columns. The band's full colour is returned.
        /// </summary>
        static float BandWidth(Texture2D shot, Vector2 middle, float expectedPx, Rgb? full, out Rgb inside)
        {
            const int Columns = 9, Margin = 8, Background = 3;
            int half = Mathf.CeilToInt(0.5f * Mathf.Max(expectedPx, FlowRibbon.HairlinePx)) + Margin;
            RectInt r = Clamp(shot, new RectInt(Mathf.RoundToInt(middle.x) - Columns / 2, Mathf.RoundToInt(middle.y) - half, Columns, 2 * half + 1));
            Color[] px = shot.GetPixels(r.x, r.y, r.width, r.height);
            int rows = r.height, columns = r.width;
            var lin = new Rgb[px.Length];
            for (int i = 0; i < px.Length; i++) lin[i] = Rgb.FromSrgb(px[i].r, px[i].g, px[i].b);

            // the background: the outermost rows on both sides; the band's colour: its middle rows (or the colour given)
            var edge = new List<Rgb>();
            var middleRows = new List<Rgb>();
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    if (y < Background || y >= rows - Background) edge.Add(lin[y * columns + x]);
                    else if (Mathf.Abs(y - rows / 2) <= Mathf.Max(0, 0.5f * expectedPx - 3)) middleRows.Add(lin[y * columns + x]);
                }
            }

            Rgb background = Rgb.Mean(edge);
            inside = full ?? Rgb.Mean(middleRows);
            int channel = 0;
            for (int c = 1; c < 3; c++)
            {
                if (Math.Abs(inside[c] - background[c]) > Math.Abs(inside[channel] - background[channel])) channel = c;
            }

            double span = inside[channel] - background[channel];
            if (Math.Abs(span) < 0.005) return float.NaN;
            double sum = 0;
            for (int i = 0; i < lin.Length; i++) sum += Math.Max(0, Math.Min(1, (lin[i][channel] - background[channel]) / span));
            return (float)(sum / columns);
        }

        /// <summary>The share of a row of pixels brighter than halfway between the row's darkest and brightest (NaN when flat).</summary>
        static float LitShare(Texture2D shot, int x0, int x1, int y)
        {
            RectInt r = Clamp(shot, new RectInt(Mathf.Min(x0, x1), y, Mathf.Max(1, Mathf.Abs(x1 - x0)), 1));
            Color[] px = shot.GetPixels(r.x, r.y, r.width, 1);
            var light = new double[px.Length];
            double lo = double.MaxValue, hi = double.MinValue;
            for (int i = 0; i < px.Length; i++)
            {
                Rgb c = Rgb.FromSrgb(px[i].r, px[i].g, px[i].b);
                light[i] = c.R + c.G + c.B;
                lo = Math.Min(lo, light[i]);
                hi = Math.Max(hi, light[i]);
            }

            if (hi - lo < 0.02) return float.NaN;
            double half = 0.5 * (lo + hi);
            return (float)light.Count(v => v > half) / light.Length;
        }

        // ------------------------------------------------------------------ colour

        /// <summary>A colour in linear sRGB (double precision).</summary>
        readonly struct Rgb
        {
            public readonly double R, G, B;

            public Rgb(double r, double g, double b)
            {
                R = r;
                G = g;
                B = b;
            }

            public double this[int channel] => channel == 0 ? R : channel == 1 ? G : B;

            /// <summary>A colour as Unity holds the palette: linear.</summary>
            public static Rgb Of(Color linear) => new Rgb(linear.r, linear.g, linear.b);

            /// <summary>A pixel as the capture holds it: sRGB-encoded, 0..1.</summary>
            public static Rgb FromSrgb(double r, double g, double b) => new Rgb(ToLinear(r), ToLinear(g), ToLinear(b));

            public static Rgb Mean(List<Rgb> list)
            {
                double r = 0, g = 0, b = 0;
                foreach (Rgb c in list)
                {
                    r += c.R;
                    g += c.G;
                    b += c.B;
                }

                int n = Math.Max(1, list.Count);
                return new Rgb(r / n, g / n, b / n);
            }
        }

        static double ToLinear(double s) => s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);

        /// <summary>Relative luminance of a linear colour (Rec. 709 weights).</summary>
        static double Luminance(Rgb c) => 0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B;

        static double ToSrgb(double l)
        {
            l = Math.Max(0, Math.Min(1, l));
            return l <= 0.0031308 ? 12.92 * l : 1.055 * Math.Pow(l, 1 / 2.4) - 0.055;
        }

        static string Hex(Rgb c)
        {
            int Byte(double l) => (int)Math.Round(ToSrgb(l) * 255);
            return string.Format(Ci, "#{0:X2}{1:X2}{2:X2}", Byte(c.R), Byte(c.G), Byte(c.B));
        }

        /// <summary>Machado, Oliveira and Fernandes (2009), severity 1, on linear RGB (rows of the matrix).</summary>
        static readonly double[] Deutan = { 0.367322, 0.860646, -0.227968, 0.280085, 0.672501, 0.047413, -0.011820, 0.042940, 0.968881 };

        static readonly double[] Protan = { 0.152286, 1.052583, -0.204868, 0.114503, 0.786281, 0.099216, -0.003882, -0.048116, 1.051998 };

        /// <summary>A colour as a dichromat sees it: the matrix on linear RGB, clipped to the display's range.</summary>
        static Rgb Simulate(double[] m, Rgb c) => new Rgb(
            Clip(m[0] * c.R + m[1] * c.G + m[2] * c.B),
            Clip(m[3] * c.R + m[4] * c.G + m[5] * c.B),
            Clip(m[6] * c.R + m[7] * c.G + m[8] * c.B));

        static double Clip(double v) => Math.Max(0, Math.Min(1, v));

        /// <summary>OKLab (Ottosson 2020) of a linear sRGB colour.</summary>
        static (double L, double a, double b) OkLab(Rgb c)
        {
            double l = Cbrt(0.4122214708 * c.R + 0.5363325363 * c.G + 0.0514459929 * c.B);
            double m = Cbrt(0.2119034982 * c.R + 0.6806995451 * c.G + 0.1073969566 * c.B);
            double s = Cbrt(0.0883024619 * c.R + 0.2817188376 * c.G + 0.6299787005 * c.B);
            return (0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s,
                1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s,
                0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s);
        }

        static double Cbrt(double v) => v < 0 ? -Math.Pow(-v, 1.0 / 3) : Math.Pow(v, 1.0 / 3);

        static double Distance(Rgb x, Rgb y)
        {
            (double L, double a, double b) p = OkLab(x), q = OkLab(y);
            return 100 * Math.Sqrt((p.L - q.L) * (p.L - q.L) + (p.a - q.a) * (p.a - q.a) + (p.b - q.b) * (p.b - q.b));
        }

        /// <summary>Two colours' separation (OKLab distance × 100) under normal vision, deuteranopia and protanopia.</summary>
        static (double normal, double deutan, double protan) Separation(Rgb x, Rgb y) =>
            (Distance(x, y), Distance(Simulate(Deutan, x), Simulate(Deutan, y)), Distance(Simulate(Protan, x), Simulate(Protan, y)));
    }
}
