using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Debug = UnityEngine.Debug;

namespace Why.Director
{
    /// <summary>
    /// Director mode: a tutor that takes the viewer through the whole graph, from the Big Bang to this
    /// moment, as a narrated guide (Data/tour.json). Each step re-scales the graph to a view preset, flies
    /// the camera to frame the step's anchor, makes the anchor and related things glow (everything else
    /// dims), and shows a narration popup on the side of the screen away from the anchor with an animated
    /// attention arrow pointing at it.
    ///
    /// Controls: T (or GraphRoot.RequestTour) starts; Right / Space / Enter = next (completes the
    /// typewriter first), Left = previous, P = pause/resume autoplay, M = mute the spoken narration, Esc =
    /// exit. Moving the camera or re-scaling from elsewhere pauses autoplay (and the voice) so the viewer
    /// can look around; Next / Back return to the narration and resume it. Each stop is spoken by its
    /// recorded clip (<see cref="TourNarrator"/>) and lasts at least as long. The tour is loaded and
    /// validated one frame after the graph has loaded.
    ///
    /// On a portrait screen the popup is a full-width sheet docked at the bottom (or at the top when the
    /// anchor sits low), and anchors are framed a little above the middle so the sheet rarely has to move.
    /// When the screen flips orientation mid-tour, the current stop is re-framed and the panel re-placed.
    /// </summary>
    [GraphScenes(GraphScene.Why, GraphScene.Economy)]
    public sealed class DirectorMode : GraphModule
    {
        /// <summary>Canvas sorting order: above the HUD.</summary>
        const int SortingOrder = 50;

        const float TransitionSeconds = 2.4f;

        /// <summary>How far the camera target moves from the preset's framing toward the anchor.</summary>
        const float AnchorFraming = 0.7f;

        /// <summary>Gentler for the whole-graph overview, which must keep the clock and the branch in view.</summary>
        const float WholeGraphFraming = 0.35f;

        const float HighlightDim = 0.72f;
        const float TitleCardSeconds = 6f;

        /// <summary>The arrow appears once the camera flight has mostly settled.</summary>
        const float ArrowDelay = 1.1f;

        const float ArrowFadeIn = 0.35f;
        const float ArrowFadeOut = 0.15f;
        const float ArrowRevealSeconds = 0.7f;

        /// <summary>Seconds left on a step when autoplay resumes after it had run out.</summary>
        const float ResumeGrace = 2.5f;

        const float RelocateCooldown = 1.5f;
        const float CaptionSize = 14f;

        /// <summary>
        /// Portrait: anchors are framed this far (normalized device y) above the middle of the screen (58% of the
        /// height), so the bottom sheet (up to ~45% of the height) stays clear of them.
        /// </summary>
        const float PortraitAnchorLift = 0.16f;

        /// <summary>Portrait: views pitched at least this much (degrees) lift their anchor by sliding along the ground.</summary>
        const float LiftAlongGroundPitch = 45f;

        /// <summary>Highlight markers are dropped where the lens has faded the content below this.</summary>
        const float MarkerMinVisibility = 0.15f;

        /// <summary>Our lineage through the three levels, lit on the end card.</summary>
        static readonly string[] LineageKeys = { "matter:_lineage", "clade:_lineage", "civ:_lineage" };

        /// <summary>What glows on the economy tour's end card: the owners' lines and the circuit.</summary>
        static readonly string[] EconomyEndKeys = { "people:owners", "circuit:loop" };

        GraphRoot root;
        Canvas canvas;
        RectTransform canvasRect;
        TourPanel panel;
        AttentionArrow arrow;
        TextMeshProUGUI caption;
        float captionWidth;

        TourScript script;
        List<ResolvedStep> steps;

        /// <summary>Current stop: -1 = title card, steps.Count = end card.</summary>
        int index;

        bool active, startRequested, autoplay, pausedByViewer, focusing, savedPresetKeys, attentionShown;
        float stepAge, autoClock, stepDuration, relocateTimer;
        TourNarrator narrator;

        // what the step wants the arrow to show, and what it currently shows (switched while invisible)
        Anchor target, arrowTarget;
        readonly List<Anchor> markers = new List<Anchor>();
        readonly List<Anchor> arrowMarkers = new List<Anchor>();
        int stepVersion, arrowVersion;
        float arrowAlpha, arrowReveal;

        readonly List<IdRange> ranges = new List<IdRange>();

        public override int Order => 50;

        Vector2 CanvasSize => canvasRect.rect.size;

        public override void Init(GraphRoot graphRoot)
        {
            root = graphRoot;
            root.TourRequested += StartTour;
            root.FocusChanged += OnFocusChanged;
            root.OrientationChanged += OnOrientationChanged;
            BuildUi();
        }

        public override void OnLoaded(GraphRoot graphRoot)
        {
            if (startRequested) StartTour();
            else StartCoroutine(ValidateNextFrame());
        }

        /// <summary>Loads and validates the tour right after loading, so tour problems are logged without starting it.</summary>
        IEnumerator ValidateNextFrame()
        {
            yield return null; // keep the few milliseconds off the frame that finishes loading
            EnsureTour();
        }

        /// <summary>Loads Data/tour.json and resolves it against the graph once (anchors exist only after loading).</summary>
        void EnsureTour()
        {
            if (steps != null) return;
            Stopwatch sw = Stopwatch.StartNew();
            script = TourScript.Load();
            steps = ResolvedStep.ResolveAll(script, out int unresolved);

            // the spoken narration: a stop lasts at least as long as its clip
            narrator ??= new TourNarrator(gameObject, root.Rig != null ? root.Rig.Cam : Camera.main);
            int spoken = 0;
            foreach (ResolvedStep s in steps)
            {
                s.Narration = TourNarrator.ClipFor(s.Step.Id);
                if (s.Narration == null) continue;
                spoken++;
                s.Duration = Mathf.Max(s.Duration, s.Narration.length + TourNarrator.PauseAfter);
            }

            Debug.Log($"[Why] director: {spoken} of {steps.Count} stops have narration clips");
            Debug.Log($"[Why] director: tour '{script.Title}' ({steps.Count} steps) loaded in " +
                      $"{sw.ElapsedMilliseconds} ms, " +
                      (unresolved > 0 ? $"{unresolved} unresolved keys" : "all keys resolved"));
        }

        void OnDestroy()
        {
            if (root == null) return;
            root.TourRequested -= StartTour;
            root.FocusChanged -= OnFocusChanged;
            root.OrientationChanged -= OnOrientationChanged;
            if (active) ExitTour();
        }

        void BuildUi()
        {
            canvas = UiFactory.CreateCanvas("DirectorCanvas", SortingOrder, transform);
            canvasRect = (RectTransform)canvas.transform;

            // the arrow re-meshes every frame: its own sub-canvas keeps the panel from being re-batched
            RectTransform layer = UiFactory.Rect(canvas.transform, "Attention").Fill();
            layer.gameObject.AddComponent<Canvas>();
            RectTransform arrowRect = UiFactory.Rect(layer, "Arrow").Fill();
            arrowRect.pivot = Vector2.zero; // local coordinates = canvas units from the bottom-left corner
            // custom graphics do not get a CanvasRenderer automatically: without one nothing is drawn
            arrowRect.gameObject.AddComponent<CanvasRenderer>();
            arrow = arrowRect.gameObject.AddComponent<AttentionArrow>();
            arrow.raycastTarget = false;

            caption = UiFactory.Text(layer, "Caption", "", CaptionSize, GraphStyle.Text, TextAlignmentOptions.MidlineLeft);
            caption.textWrappingMode = TextWrappingModes.NoWrap;
            caption.rectTransform.anchorMin = caption.rectTransform.anchorMax = Vector2.zero;
            caption.rectTransform.sizeDelta = new Vector2(480, 24);
            caption.alpha = 0;

            panel = new TourPanel(canvas.transform, Previous, TogglePause, Advance, ExitTour);
        }

        // ------------------------------------------------------------------ tour lifecycle

        /// <summary>Starts the guided tour. Requests made while the graph is loading start it once loaded.</summary>
        public void StartTour()
        {
            if (active || root == null) return;
            if (!root.IsLoaded)
            {
                startRequested = true;
                return;
            }

            startRequested = false;
            EnsureTour();
            // a HUD button that started the tour must not keep the selection (Space / Enter would click it)
            ReleaseUiFocus();

            active = true;
            savedPresetKeys = root.AllowPresetKeys;
            root.TourActive = true;
            root.AllowPresetKeys = false;
            autoplay = true;
            pausedByViewer = false;
            GoTo(-1);
        }

        /// <summary>Leaves the tour where it is; the viewer keeps the current view.</summary>
        public void ExitTour()
        {
            if (!active) return;
            active = false;
            root.TourActive = false;
            root.AllowPresetKeys = savedPresetKeys;
            Highlighter.Clear();
            panel.Hide();
            narrator?.Stop();
            target = null;
            markers.Clear();
            stepVersion++;
            ReleaseUiFocus();
        }

        void Restart()
        {
            ReleaseUiFocus();
            SetAutoplay(true, false);
            GoTo(0);
        }

        /// <summary>Keyboard "next": completes the typewriter first, then moves on.</summary>
        void Next()
        {
            if (panel.IsTyping)
            {
                panel.CompleteTyping();
                return;
            }

            Advance();
        }

        void Advance()
        {
            ReleaseUiFocus();
            if (!active || index >= steps.Count) return;
            ResumeAfterExploring();
            GoTo(index + 1);
        }

        void Previous()
        {
            ReleaseUiFocus();
            if (!active || index <= -1) return;
            ResumeAfterExploring();
            GoTo(index - 1);
        }

        /// <summary>
        /// Navigating back to the narration ends a pause that looking around caused; a pause the viewer chose
        /// (P or the pause button) stays until they resume.
        /// </summary>
        void ResumeAfterExploring()
        {
            if (!pausedByViewer) return;
            autoplay = true;
            pausedByViewer = false;
        }

        void TogglePause()
        {
            ReleaseUiFocus();
            SetAutoplay(!autoplay, false);
        }

        void ToggleNarration()
        {
            ReleaseUiFocus();
            narrator?.ToggleMute();
            RefreshTransport();
        }

        void SetAutoplay(bool on, bool byViewer)
        {
            autoplay = on;
            pausedByViewer = !on && byViewer;
            // the narration pauses with the tour, so voice and progress stay together
            if (on) narrator?.Resume();
            else narrator?.Pause();
            if (on) autoClock = Mathf.Min(autoClock, Mathf.Max(0, stepDuration - ResumeGrace));
            RefreshTransport();
        }

        void RefreshTransport()
        {
            string status = autoplay ? "Autoplay  \u00B7  P to pause"
                : pausedByViewer ? "Exploring  \u00B7  P to resume"
                : "Paused  \u00B7  P to resume";
            if (narrator != null && narrator.Muted) status += "  \u00B7  muted (M)";
            panel.SetTransport(autoplay, status);
        }

        void OnFocusChanged(ViewPreset preset)
        {
            // someone else re-scaled the graph while the tour runs: let the viewer look around
            if (active && !focusing && autoplay) SetAutoplay(false, true);
        }

        /// <summary>
        /// The screen flipped between landscape and portrait (V, or a window resized by hand): frame the current
        /// stop again for the new shape and move the panel (re-laid out at its new width) to a fitting slot. The
        /// step keeps its clock, typewriter and highlight.
        /// </summary>
        void OnOrientationChanged()
        {
            if (!active || steps == null) return;
            ViewPreset preset;
            Anchor anchor = null;
            bool card = true;
            if (index < 0)
            {
                preset = TitlePreset();
            }
            else if (index >= steps.Count)
            {
                preset = EndPreset();
                Anchors.TryGet("now", out anchor);
            }
            else
            {
                ResolvedStep s = steps[index];
                preset = s.Preset ?? root.CurrentPreset ?? ViewPresets.All[0];
                anchor = s.Target;
                card = false;
            }

            bool hasTarget = Frame(preset, anchor, out Vector2 placeAt);
            panel.Relocate(size => PanelPlacement.Choose(size, CanvasSize, hasTarget, placeAt, card));
            relocateTimer = RelocateCooldown;
        }

        ViewPreset TitlePreset() => steps.Count > 0 && steps[0].Preset != null ? steps[0].Preset : ViewPresets.All[0];

        static ViewPreset EndPreset() => ResolvedStep.FindPreset("overview") ?? ViewPresets.All[0];

        // ------------------------------------------------------------------ steps

        void GoTo(int stop)
        {
            index = Mathf.Clamp(stop, -1, steps.Count);
            stepAge = 0;
            autoClock = 0;
            relocateTimer = RelocateCooldown;
            target = null;
            markers.Clear();
            stepVersion++;

            if (index < 0) ShowTitleCard();
            else if (index >= steps.Count) ShowEndCard();
            else ShowStep(steps[index]);
            RefreshTransport();
        }

        void ShowTitleCard()
        {
            Frame(TitlePreset(), null, out _);
            Highlighter.Clear();
            narrator.Stop();
            stepDuration = TitleCardSeconds;

            float seconds = 0;
            foreach (ResolvedStep s in steps) seconds += s.Duration;
            int minutes = Mathf.Max(1, Mathf.RoundToInt(seconds / 60f));
            panel.Present(new PanelContent
            {
                Card = true,
                Kicker = "Guided tour",
                Title = script.Title,
                Body = (GraphScene.IsEconomy
                           ? $"{steps.Count} stops through the economy of the United States, from 1946 to this year, about {minutes} minutes. "
                           : $"{steps.Count} stops through 13.8 billion years of cause and effect, about {minutes} minutes. ") +
                       "The narration moves on by itself; move the camera at any time to pause and look around.",
                Footnote = GraphScene.IsEconomy
                    ? "Space or \u2192 next      \u2190 back      P pause      Esc leave"
                    : "Space or \u2192 next      \u2190 back      P pause      M mute      Esc leave",
                PrimaryLabel = "Begin",
                Primary = Advance,
                SecondaryLabel = "Close",
                Secondary = ExitTour
            }, size => PanelPlacement.Choose(size, CanvasSize, false, Vector2.zero, true));
        }

        void ShowStep(ResolvedStep s)
        {
            ViewPreset preset = s.Preset ?? root.CurrentPreset ?? ViewPresets.All[0];
            bool hasTarget = Frame(preset, s.Target, out Vector2 placeAt);
            target = s.Target;
            markers.AddRange(s.Highlights);
            Highlight(s.Target, s.Highlights, GraphStyle.HighlightGlow, HighlightDim);
            stepDuration = s.Duration;
            narrator.Play(s.Narration);

            string section = string.IsNullOrEmpty(preset.Title) ? "" : "   \u00B7   " + preset.Title;
            panel.Present(new PanelContent
            {
                Kicker = $"{index + 1} / {steps.Count}{section}",
                Title = s.Step.Title,
                Body = s.Step.Text,
                Typewriter = true,
                Transport = true
            }, size => PanelPlacement.Choose(size, CanvasSize, hasTarget, placeAt, false));
        }

        void ShowEndCard()
        {
            Anchors.TryGet("now", out Anchor now);
            bool hasTarget = Frame(EndPreset(), now, out Vector2 placeAt);
            target = now;

            List<Anchor> lineage = new List<Anchor>();
            foreach (string key in GraphScene.IsEconomy ? EconomyEndKeys : LineageKeys)
            {
                if (Anchors.TryGet(key, out Anchor a)) lineage.Add(a);
            }

            Highlight(null, lineage, GraphStyle.HighlightGlow * 0.6f, 0.45f);
            narrator.Stop();
            stepDuration = 0;

            bool economy = GraphScene.IsEconomy;
            string lit = lineage.Count == 0 ? ""
                : economy ? "The glowing lines own capital: the few who steer their own path. "
                : "The glowing path is the chain of causes that led to you. ";
            panel.Present(new PanelContent
            {
                Card = true,
                Kicker = "End of the tour",
                Title = economy ? "Every dollar is someone's choice, made in fear or desire."
                    : "This moment is the result of everything before it.",
                Body = lit + (economy
                    ? "Explore it on your own: scroll to zoom, right-drag to orbit, the number keys to change the " +
                      "view. Click a lifeline to look inside one person; comma and period change the year of the " +
                      "circuit and the mind map. H lists every control; T takes the tour again."
                    : "Explore it on your own: scroll to zoom, right-drag to orbit, the number keys to " +
                      "re-scale. U unrolls the clock where you look ([ and ] widen or narrow the window, L " +
                      "switches between log and linear time). H lists every control; T takes the tour again."),
                PrimaryLabel = "Explore",
                Primary = ExitTour,
                SecondaryLabel = "Restart",
                Secondary = Restart
            }, size => PanelPlacement.Choose(size, CanvasSize, hasTarget, placeAt, true));
        }

        /// <summary>
        /// Re-scales the graph to a preset, then overrides the camera flight to frame the anchor, evaluated
        /// under the preset's final warp (the view keeps some of the preset's context). Returns false
        /// without an anchor; otherwise canvasPoint is where the anchor will be when the flight ends.
        /// </summary>
        bool Frame(ViewPreset preset, Anchor anchor, out Vector2 canvasPoint)
        {
            canvasPoint = Vector2.zero;
            focusing = true;
            root.Focus(preset, TransitionSeconds);
            focusing = false;
            if (anchor == null) return false;

            Vector3 world = anchor.WorldUnder(preset.Warp());
            CameraPose pose = preset.Pose();
            bool wholeGraph = preset.Polar && preset.PolarArc < 0;
            pose.Target = Vector3.Lerp(pose.Target, world, wholeGraph ? WholeGraphFraming : AnchorFraming);
            Camera cam = root.Rig.Cam;
            if (ScreenLayout.IsPortrait) pose.Target += PortraitLift(pose, cam.fieldOfView);
            root.Rig.FlyTo(pose, TransitionSeconds);

            // the screen's aspect and canvas size, not the camera's and the canvas rect's: right after an orientation
            // flip those may not have caught up yet
            Vector2 size = UiFactory.CanvasSize;
            PanelPlacement.ViewportUnderPose(pose, cam.fieldOfView, ScreenLayout.Aspect, world, out Vector2 viewport);
            PanelPlacement.ClampToScreen(Vector2.Scale(viewport, size), false, size, out canvasPoint, out _);
            return true;
        }

        /// <summary>
        /// Moves a pose's target so what was at the middle of the screen shows exactly <see cref="PortraitAnchorLift"/>
        /// higher (normalized device y). Steep views slide back along the ground, toward the camera: the old middle
        /// is then d sin(pitch) / ((distance + d cos(pitch)) tan(fov / 2)) above the new one, solved for d. Shallow
        /// views (below <see cref="LiftAlongGroundPitch"/>, where that slide would be long and change the depth a lot)
        /// slide straight down the screen instead, which keeps the depth.
        /// </summary>
        static Vector3 PortraitLift(CameraPose pose, float fovDegrees)
        {
            float tan = Mathf.Tan(fovDegrees * 0.5f * Mathf.Deg2Rad);
            if (pose.Pitch < LiftAlongGroundPitch)
            {
                return Quaternion.Euler(pose.Pitch, pose.Yaw, 0) * Vector3.down * (PortraitAnchorLift * pose.Distance * tan);
            }

            float sin = Mathf.Sin(pose.Pitch * Mathf.Deg2Rad), cos = Mathf.Cos(pose.Pitch * Mathf.Deg2Rad);
            float d = PortraitAnchorLift * tan * pose.Distance / (sin - PortraitAnchorLift * tan * cos);
            return Quaternion.Euler(0, pose.Yaw, 0) * Vector3.back * d;
        }

        void Highlight(Anchor main, List<Anchor> others, float glow, float dim)
        {
            ranges.Clear();
            if (main != null && !main.Ids.IsEmpty) ranges.Add(main.Ids);
            foreach (Anchor a in others)
            {
                if (!a.Ids.IsEmpty) ranges.Add(a.Ids);
            }

            if (ranges.Count == 0) Highlighter.Clear();
            else Highlighter.Set(ranges, glow, dim);
        }

        // ------------------------------------------------------------------ per frame

        void Update()
        {
            if (root == null) return;
            float dt = Time.unscaledDeltaTime;
            HandleKeys();
            if (active) TickAutoplay(dt);
            panel.Tick(dt);
        }

        void HandleKeys()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null || TextFieldFocused()) return;
            if (!active)
            {
                if (kb.tKey.wasPressedThisFrame && root.IsLoaded) StartTour();
                return;
            }

            if (kb.escapeKey.wasPressedThisFrame) ExitTour();
            else if (kb.rightArrowKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame ||
                     kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) Next();
            else if (kb.leftArrowKey.wasPressedThisFrame) Previous();
            else if (kb.pKey.wasPressedThisFrame) TogglePause();
            else if (kb.mKey.wasPressedThisFrame) ToggleNarration();
        }

        void TickAutoplay(float dt)
        {
            stepAge += dt;
            if (autoplay && root.Rig.UserActive) SetAutoplay(false, true);

            // the end card waits for the viewer
            bool timed = index < steps.Count;
            if (autoplay && timed && !panel.IsHidden)
            {
                autoClock += dt;
                if (autoClock >= stepDuration)
                {
                    GoTo(index + 1);
                    timed = index < steps.Count;
                }
            }

            panel.SetProgress(timed ? autoClock / Mathf.Max(stepDuration, 0.01f) : -1, autoplay);
        }

        void LateUpdate()
        {
            // after the camera rig and the warp have moved this frame
            if (root != null) UpdateAttention(Time.unscaledDeltaTime);
        }

        /// <summary>Tracks the anchor on screen: arrow, target ring, caption and markers.</summary>
        void UpdateAttention(float dt)
        {
            relocateTimer -= dt;
            if (arrowVersion != stepVersion)
            {
                // fade the old arrow out before it re-targets
                arrowAlpha = Mathf.MoveTowards(arrowAlpha, 0, dt / ArrowFadeOut);
                if (arrowAlpha <= 0) AdoptStepTarget();
            }
            else
            {
                bool want = active && (arrowTarget != null || arrowMarkers.Count > 0) && stepAge >= ArrowDelay &&
                            !panel.IsHidden;
                arrowAlpha = Mathf.MoveTowards(arrowAlpha, want ? 1 : 0, dt / (want ? ArrowFadeIn : ArrowFadeOut));
                if (want) arrowReveal = Mathf.MoveTowards(arrowReveal, 1, dt / ArrowRevealSeconds);
            }

            float alpha = arrowAlpha * panel.Visibility;
            if (alpha <= 0.001f)
            {
                if (attentionShown) HideAttention(dt);
                return;
            }

            attentionShown = true;
            Camera cam = root.Rig.Cam;
            float scale = Mathf.Max(canvas.scaleFactor, 1e-4f);
            Vector2 size = CanvasSize;
            Vector2 tip = Vector2.zero;

            if (arrowTarget != null)
            {
                tip = Project(cam, arrowTarget.World, scale, size, out bool clamped, out Vector2 offscreenDir);
                Rect panelRect = panel.CanvasRect(scale);
                Vector2 start = PanelPlacement.EdgePoint(panelRect, tip, 10f, out Vector2 outward);
                // the viewer steered the target under the panel: no curve until the panel moves away
                if (panelRect.Contains(tip)) start = tip;
                arrow.SetArrow(start, outward, tip, offscreenDir, clamped, alpha, arrowReveal);
                float captionAlpha = alpha * Smooth(Mathf.InverseLerp(0.5f, 1f, arrowReveal));
                PlaceCaption(tip, clamped, offscreenDir, captionAlpha, size);
                KeepPanelClear(panelRect, tip, clamped, size);
            }
            else
            {
                arrow.ClearArrow(alpha);
                caption.alpha = 0;
            }

            arrow.Markers.Clear();
            WarpState warp = GraphWarp.Current;
            foreach (Anchor m in arrowMarkers)
            {
                // content beyond a lens window has faded away: a ring there would mark empty space
                float u = m.U;
                float visible = GraphWarp.FocusFade(u, warp);
                if (visible < MarkerMinVisibility) continue;
                Vector3 sp = cam.WorldToScreenPoint(GraphWarp.ToWorld(u, m.Y, m.Rho));
                if (sp.z <= 0) continue;
                Vector2 p = new Vector2(sp.x, sp.y) / scale;
                if (p.x < 0 || p.y < 0 || p.x > size.x || p.y > size.y) continue;
                if (arrowTarget != null && (p - tip).sqrMagnitude < 24f * 24f) continue;
                arrow.Markers.Add(new Vector3(p.x, p.y, visible));
            }

            arrow.Animate(dt);
        }

        /// <summary>Switches the arrow to the current step's anchor while it is invisible.</summary>
        void AdoptStepTarget()
        {
            arrowVersion = stepVersion;
            arrowTarget = target;
            arrowMarkers.Clear();
            arrowMarkers.AddRange(markers);
            arrowReveal = 0;
            if (arrowTarget == null) return;
            caption.text = CaptionText(arrowTarget);
            captionWidth = caption.GetPreferredValues(caption.text).x;
        }

        void HideAttention(float dt)
        {
            attentionShown = false;
            arrow.ClearArrow(0);
            arrow.Markers.Clear();
            arrow.Animate(dt);
            caption.alpha = 0;
        }

        /// <summary>Canvas position of a world point, clamped to the screen edge when off screen or behind.</summary>
        static Vector2 Project(Camera cam, Vector3 world, float scale, Vector2 canvasSize, out bool clamped,
            out Vector2 offscreenDir)
        {
            Vector3 sp = cam.WorldToScreenPoint(world);
            Vector2 p = new Vector2(sp.x, sp.y) / scale;
            clamped = PanelPlacement.ClampToScreen(p, sp.z <= 0, canvasSize, out Vector2 tip, out offscreenDir);
            return tip;
        }

        /// <summary>
        /// Names the target beside its ring, on the far side from the incoming arrow so the line never
        /// crosses the text (for clamped targets: inward from the screen edge).
        /// </summary>
        void PlaceCaption(Vector2 tip, bool clamped, Vector2 offscreenDir, float alpha, Vector2 size)
        {
            caption.alpha = alpha;
            if (alpha <= 0.001f) return;

            float gap = clamped ? 20f : AttentionArrow.RingRadius + 10f;
            bool left = clamped ? offscreenDir.x > 0.3f : arrow.TipDirection.x < -0.3f;
            if (!left && tip.x + gap + captionWidth > size.x - 12f) left = true;
            else if (left && tip.x - gap - captionWidth < 12f) left = false;

            float y = clamped ? tip.y - offscreenDir.y * 24f : tip.y;
            Vector2 pos = new Vector2(tip.x + (left ? -gap : gap), Mathf.Clamp(y, 16f, size.y - 16f));
            RectTransform rt = caption.rectTransform;
            rt.pivot = new Vector2(left ? 1 : 0, 0.5f);
            rt.anchoredPosition = pos;
            caption.alignment = left ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
        }

        /// <summary>If the viewer moves the anchor under the panel, the panel moves to another slot.</summary>
        void KeepPanelClear(Rect panelRect, Vector2 tip, bool clamped, Vector2 size)
        {
            // only once the view has settled: mid-flight the target sweeps across the screen
            if (!active || clamped || !panel.IsSettled || relocateTimer > 0) return;
            if (root.Rig.Flying || GraphWarp.Animating) return;
            if (PanelPlacement.Distance(panelRect, tip) > 12f) return;
            bool card = index < 0 || index >= steps.Count;
            panel.Relocate(s => PanelPlacement.Choose(s, size, true, tip, card));
            relocateTimer = RelocateCooldown;
        }

        /// <summary>The anchor's label and, unless the label already carries a date, when it happened (dim).</summary>
        static string CaptionText(Anchor a)
        {
            string label = string.IsNullOrEmpty(a.Label) ? a.Key : a.Label;
            // "now", "time:" and figures ("Name (born-died)") are labeled with their dates already
            if (a.Key == "now" || a.Key.StartsWith("time:", System.StringComparison.Ordinal)) return label;
            foreach (char ch in label)
            {
                if (char.IsDigit(ch)) return label;
            }

            string when = DeepTime.FormatYearsAgo(a.YearsAgo, DeepTime.NowYear);
            return $"{label}    <color=#{UiFactory.Hex(GraphStyle.TextDim)}>{when}</color>";
        }

        static bool TextFieldFocused()
        {
            EventSystem es = EventSystem.current;
            GameObject selected = es != null ? es.currentSelectedGameObject : null;
            if (selected == null) return false;
            TMP_InputField field = selected.GetComponent<TMP_InputField>();
            return field != null && field.isFocused;
        }

        /// <summary>Clicked buttons keep the selection; Space / Enter would then click them a second time.</summary>
        static void ReleaseUiFocus()
        {
            EventSystem es = EventSystem.current;
            if (es != null && es.currentSelectedGameObject != null) es.SetSelectedGameObject(null);
        }

        static float Smooth(float t) => t * t * (3 - 2 * t);
    }
}
