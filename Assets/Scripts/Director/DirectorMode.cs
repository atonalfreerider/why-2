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
    /// typewriter first), Left = previous, P = pause/resume autoplay, Esc = exit. Moving the camera or
    /// re-scaling from elsewhere pauses autoplay so the viewer can look around.
    /// </summary>
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

        /// <summary>Our lineage through the three levels, lit on the end card.</summary>
        static readonly string[] LineageKeys = { "matter:_lineage", "clade:_lineage", "civ:_lineage" };

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
            BuildUi();
        }

        public override void OnLoaded(GraphRoot graphRoot)
        {
            if (startRequested) StartTour();
        }

        void OnDestroy()
        {
            if (root == null) return;
            root.TourRequested -= StartTour;
            root.FocusChanged -= OnFocusChanged;
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
            if (steps == null)
            {
                // loaded lazily: anchors only exist once every layer has built
                Stopwatch sw = Stopwatch.StartNew();
                script = TourScript.Load();
                steps = ResolvedStep.ResolveAll(script, out int unresolved);
                Debug.Log($"[Why] director: tour '{script.Title}' ({steps.Count} steps) loaded in " +
                          $"{sw.ElapsedMilliseconds} ms, " +
                          (unresolved > 0 ? $"{unresolved} unresolved keys" : "all keys resolved"));
            }

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
            if (active && index < steps.Count) GoTo(index + 1);
        }

        void Previous()
        {
            ReleaseUiFocus();
            if (active && index > -1) GoTo(index - 1);
        }

        void TogglePause()
        {
            ReleaseUiFocus();
            SetAutoplay(!autoplay, false);
        }

        void SetAutoplay(bool on, bool byViewer)
        {
            autoplay = on;
            pausedByViewer = !on && byViewer;
            if (on) autoClock = Mathf.Min(autoClock, Mathf.Max(0, stepDuration - ResumeGrace));
            RefreshTransport();
        }

        void RefreshTransport()
        {
            string status = autoplay ? "Autoplay  \u00B7  P to pause"
                : pausedByViewer ? "Exploring  \u00B7  P to resume"
                : "Paused  \u00B7  P to resume";
            panel.SetTransport(autoplay, status);
        }

        void OnFocusChanged(ViewPreset preset)
        {
            // someone else re-scaled the graph while the tour runs: let the viewer look around
            if (active && !focusing && autoplay) SetAutoplay(false, true);
        }

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
            ViewPreset first = steps.Count > 0 && steps[0].Preset != null ? steps[0].Preset : ViewPresets.All[0];
            Frame(first, null, out _);
            Highlighter.Clear();
            stepDuration = TitleCardSeconds;

            float seconds = 0;
            foreach (ResolvedStep s in steps) seconds += s.Duration;
            int minutes = Mathf.Max(1, Mathf.RoundToInt(seconds / 60f));
            panel.Present(new PanelContent
            {
                Card = true,
                Kicker = "Guided tour",
                Title = script.Title,
                Body = $"{steps.Count} stops through 13.8 billion years of cause and effect, about {minutes} minutes. " +
                       "The narration moves on by itself; move the camera at any time to pause and look around.",
                Footnote = "Space or \u2192 next      \u2190 back      P pause      Esc leave",
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
            ViewPreset overview = ResolvedStep.FindPreset("overview") ?? ViewPresets.All[0];
            Anchors.TryGet("now", out Anchor now);
            bool hasTarget = Frame(overview, now, out Vector2 placeAt);
            target = now;

            List<Anchor> lineage = new List<Anchor>();
            foreach (string key in LineageKeys)
            {
                if (Anchors.TryGet(key, out Anchor a)) lineage.Add(a);
            }

            Highlight(null, lineage, GraphStyle.HighlightGlow * 0.6f, 0.45f);
            stepDuration = 0;

            string lit = lineage.Count > 0 ? "The glowing path is the chain of causes that led to you. " : "";
            panel.Present(new PanelContent
            {
                Card = true,
                Kicker = "End of the tour",
                Title = "This moment is the result of everything before it.",
                Body = lit + "Explore it on your own: scroll to zoom, right-drag to orbit, 1 to 8 to re-scale, " +
                       "U to unroll the clock where you look, T to take the tour again.",
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

            Vector3 world = GraphWarp.ToWorld(anchor.U, anchor.Y, anchor.Rho, preset.Warp());
            CameraPose pose = preset.Pose();
            bool wholeGraph = preset.Polar && preset.PolarArc < 0;
            pose.Target = Vector3.Lerp(pose.Target, world, wholeGraph ? WholeGraphFraming : AnchorFraming);
            root.Rig.FlyTo(pose, TransitionSeconds);

            Camera cam = root.Rig.Cam;
            Vector2 size = CanvasSize;
            PanelPlacement.ViewportUnderPose(pose, cam.fieldOfView, cam.aspect, world, out Vector2 viewport);
            PanelPlacement.ClampToScreen(Vector2.Scale(viewport, size), false, size, out canvasPoint, out _);
            return true;
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
            foreach (Anchor m in arrowMarkers)
            {
                Vector3 sp = cam.WorldToScreenPoint(m.World);
                if (sp.z <= 0) continue;
                Vector2 p = new Vector2(sp.x, sp.y) / scale;
                if (p.x < 0 || p.y < 0 || p.x > size.x || p.y > size.y) continue;
                if (arrowTarget != null && (p - tip).sqrMagnitude < 24f * 24f) continue;
                arrow.Markers.Add(p);
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

        static string CaptionText(Anchor a)
        {
            // "now" and "time:" anchors are labeled with their date already
            if (a.Key == "now" || a.Key.StartsWith("time:", System.StringComparison.Ordinal)) return a.Label;
            string when = DeepTime.FormatYearsAgo(a.YearsAgo, DeepTime.NowYear);
            return $"{a.Label}    <color=#{UiFactory.Hex(GraphStyle.TextDim)}>{when}</color>";
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
