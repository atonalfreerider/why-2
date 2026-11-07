using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
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
    /// exit. Moving the camera, re-scaling from elsewhere or clicking another panel pauses autoplay (and the
    /// voice) so the viewer can look around; Next / Back return to the narration and resume it. Each stop is
    /// spoken by its recorded clip (<see cref="TourNarrator"/>) and lasts at least as long; a stop that asks
    /// the viewer to do something waits for them. A stop's cues step its view through states of its own as
    /// the narration names them. The tour is loaded and validated one frame after the graph has loaded.
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

        /// <summary>The plate behind the arrow's caption (its alpha at full caption alpha): the scene's lines, the Now post and
        /// the anchor's own label otherwise show through the text where the ring sits on bright content. Near opaque: at 0.85
        /// the ring of one of the stop's other highlights still showed between "million" and "years" (the Cambrian's).</summary>
        static readonly Color CaptionPlateColor = new Color(0.02f, 0.027f, 0.047f, 0.95f);

        /// <summary>The caption plate's margin around the text (canvas units, across and up). Up only 1: the line box (1.35 em)
        /// already leaves the letters about 4 units of plate above and below, and the graph's labels near it stand clear of a
        /// thinner plate: at 3 its edges cut 1.5 px off the Milky Way stop's "Cosmic inflation" and 2.4 px off the asteroid
        /// stop's "100 Ma".</summary>
        static readonly Vector2 CaptionPlateMargin = new Vector2(7f, 1f);

        /// <summary>
        /// Portrait: anchors are framed this far (normalized device y) above the middle of the screen (58% of the
        /// height), so a bottom sheet of up to ~45% of the height stays clear of them.
        /// </summary>
        const float PortraitAnchorLift = 0.16f;

        /// <summary>Portrait: views pitched at least this much (degrees) lift their anchor by sliding along the ground.</summary>
        const float LiftAlongGroundPitch = 45f;

        /// <summary>Highlight markers are dropped where the lens has faded the content below this.</summary>
        const float MarkerMinVisibility = 0.15f;

        /// <summary>Our lineage through the three levels, lit on the end card.</summary>
        static readonly string[] LineageKeys = { "matter:_lineage", "clade:_lineage", "civ:_lineage" };

        /// <summary>What glows on the economy tour's end card: the players who own (the 1% and business owners) and the crown.</summary>
        static readonly string[] EconomyEndKeys = { "land:owners", "land:crown" };

        GraphRoot root;
        Canvas canvas;
        RectTransform canvasRect;
        TourPanel panel;
        AttentionArrow arrow;
        TextMeshProUGUI caption;
        Image captionPlate;
        float captionWidth;

        /// <summary>The ring at the arrow's tip as a block that moves the graph's labels aside (LabelSystem.MoveAside) while it shows.</summary>
        RectTransform ringKeepOut;

        // the step's own arrow caption, and the one the arrow shows (switched with the target while it is invisible)
        string targetCaption, arrowCaption;

        // the view state (ViewVariants) the current stop's cues have shown, so leaving the stop can return the view to its default
        ViewVariants.Entry cueView;
        int cueShown = -1;

        readonly List<RaycastResult> uiHits = new List<RaycastResult>();

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

        /// <summary>True while the guided tour runs.</summary>
        public bool TourRunning => active;

        /// <summary>True on the tour's closing card (after the last stop).</summary>
        public bool AtEndCard => active && steps != null && index >= steps.Count;

        /// <summary>The anchor the current step points at (null between steps and outside the tour); headsets mark it in 3D.</summary>
        public Anchor AttentionTarget => active ? target : null;

        /// <summary>"Next" from a controller button: completes the typewriter first, then moves on.</summary>
        public void NextStep() => Next();

        /// <summary>"Back" from a headset button.</summary>
        public void PreviousStep() => Previous();

        /// <summary>Shows the whole text of the card at once (the smoke test captures cards complete).</summary>
        public void CompleteTyping() => panel.CompleteTyping();

        /// <summary>The stop on screen (-1: the title card; the number of stops: the end card).</summary>
        public int CurrentStep => active ? index : -1;

        /// <summary>The id of the stop on screen (null on the cards and outside the tour).</summary>
        public string CurrentStepId => active && steps != null && index >= 0 && index < steps.Count ? steps[index].Step.Id : null;

        /// <summary>The time into the current stop of its last cue (0 when it has none or one at its start), for the smoke
        /// test's second frame of a stop whose picture changes with the narration.</summary>
        public float CurrentStepLastCue => active && steps != null && index >= 0 && index < steps.Count && steps[index].Step.Cues.Count > 0
            ? steps[index].Step.Cues.Max(c => c.At) : 0;

        /// <summary>The view of the stop on screen (null on the cards and outside the tour).</summary>
        public string CurrentPresetId => active && steps != null && index >= 0 && index < steps.Count ? steps[index].Preset?.Id : null;

        /// <summary>True while the tour runs but does not advance on its own.</summary>
        public bool Paused => active && !autoplay;

        /// <summary>Play / pause from a headset button (starts the tour when it is not running).</summary>
        public void PlayPause()
        {
            if (!active) StartTour();
            else TogglePause();
        }

        /// <summary>
        /// The tour's chapters: one per view, in the order the tour first reaches it, each with the view's title and that
        /// first stop. The headset menu shows one icon per chapter.
        /// </summary>
        public IReadOnlyList<TourChapter> Chapters
        {
            get
            {
                if (root == null || !root.IsLoaded) return System.Array.Empty<TourChapter>();
                EnsureTour();
                if (chapters != null) return chapters;
                chapters = new List<TourChapter>();
                for (int i = 0; i < steps.Count; i++)
                {
                    ViewPreset p = steps[i].Preset;
                    string id = p != null ? p.Id : "";
                    // one chapter per view, where the tour first reaches it (the tour returns to some views)
                    if (chapters.Exists(c => c.PresetId == id)) continue;
                    chapters.Add(new TourChapter { PresetId = id, Title = p != null ? p.Title : steps[i].Step.Title, FirstStep = i });
                }

                return chapters;
            }
        }

        /// <summary>Plays the tour from a stop (starting it when needed), with autoplay on.</summary>
        public void PlayFrom(int stop)
        {
            if (root == null || !root.IsLoaded) return;
            if (!active) StartTour();
            SetAutoplay(true, false);
            GoTo(stop);
        }

        List<TourChapter> chapters;

        Vector2 CanvasSize => canvasRect.rect.size;

        public override void Init(GraphRoot graphRoot)
        {
            Current = this;
            root = graphRoot;
            root.TourRequested += StartTour;
            root.FocusChanged += OnFocusChanged;
            root.OrientationChanged += OnOrientationChanged;
            BuildUi();
        }

        /// <summary>
        /// Set when the universe tour continues into the economy: the next scene's director starts its tour as soon as
        /// the graph has loaded, so the whole model plays as one walkthrough from the Big Bang to the economy.
        /// </summary>
        static bool continueTourInNextScene;

        public override void OnLoaded(GraphRoot graphRoot)
        {
            if (continueTourInNextScene)
            {
                continueTourInNextScene = false;
                startRequested = true;
            }

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
            if (Current == this) Current = null;
            TourScript.Story = null;
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

            // the plate first, so it draws under the caption (its sibling: SetCaptionAlpha switches the plate off while the
            // caption is hidden). The graph's labels it stands half over move aside to a candidate place (LabelSystem.MoveAside):
            // a plain keep-out hid the stop's own names instead (Chordates, Fossil fuels), and gave their room to labels under the
            // arrow's ring, so the ring moves labels aside too
            captionPlate = UiFactory.Panel(layer, "CaptionPlate", CaptionPlateColor, false);
            captionPlate.rectTransform.anchorMin = captionPlate.rectTransform.anchorMax = Vector2.zero;
            LabelSystem.MoveAside(captionPlate.rectTransform);
            ringKeepOut = UiFactory.Rect(layer, "RingKeepOut");
            ringKeepOut.anchorMin = ringKeepOut.anchorMax = Vector2.zero;
            ringKeepOut.pivot = new Vector2(0.5f, 0.5f);
            // the ring breathes 1.4 units past its radius, its stroke 1.1 wide (AttentionArrow.DrawTarget)
            ringKeepOut.sizeDelta = Vector2.one * 2f * (AttentionArrow.RingRadius + 2f);
            ringKeepOut.gameObject.SetActive(false);
            LabelSystem.MoveAside(ringKeepOut);
            caption = UiFactory.Text(layer, "Caption", "", CaptionSize, GraphStyle.Text, TextAlignmentOptions.MidlineLeft);
            caption.textWrappingMode = TextWrappingModes.NoWrap;
            caption.rectTransform.anchorMin = caption.rectTransform.anchorMax = Vector2.zero;
            caption.rectTransform.sizeDelta = new Vector2(480, 24);
            SetCaptionAlpha(0);

            panel = new TourPanel(canvas.transform, Previous, TogglePause, Advance, ExitTour);
        }

        // ------------------------------------------------------------------ tour lifecycle

        /// <summary>The director of the scene shown (set when it loads), for the panels that start a story.</summary>
        public static DirectorMode Current { get; private set; }

        /// <summary>Plays a story's walkthrough (a Resources path, <see cref="TourScript.Story"/>) instead of the scene's tour;
        /// leaving it brings the scene's own tour back.</summary>
        public void StartStory(string walkthrough)
        {
            if (Resources.Load<TextAsset>(walkthrough) == null)
            {
                Debug.LogWarning("[Why] tour: no story at Resources/" + walkthrough);
                return;
            }

            if (active) ExitTour();
            TourScript.Story = walkthrough;
            steps = null;    // the story's script loads in its place
            chapters = null;
            StartTour();
        }

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
            Genesis g = Genesis;
            if (g != null && g.Playing) g.Stop();
            ClearCues();
            target = null;
            targetCaption = null;
            markers.Clear();
            stepVersion++;
            ReleaseUiFocus();
            if (TourScript.Story != null)
            {
                // a story ends: the scene's own tour is the one the guided-tour button plays again
                TourScript.Story = null;
                steps = null;
                chapters = null;
            }
        }

        /// <summary>The universe tour's last card: open the economy scene and keep narrating there.</summary>
        void ContinueIntoEconomy()
        {
            continueTourInNextScene = true;
            ExitTour();
            GraphRoot.OpenOtherScene();
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
            if (active && index >= steps.Count && !GraphScene.IsEconomy)
            {
                ContinueIntoEconomy();
                return;
            }

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
            string status = autoplay ? (Waits ? "Take your time  \u00B7  Next to go on" : "Autoplay  \u00B7  P to pause")
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
            PanelSlot? side = PreferredSlot;
            panel.Relocate(size => PanelPlacement.Choose(size, CanvasSize, hasTarget, placeAt, card, side));
            relocateTimer = RelocateCooldown;
        }

        ViewPreset TitlePreset() => steps.Count > 0 && steps[0].Preset != null ? steps[0].Preset : ViewPresets.All[0];

        /// <summary>The current stop's own side for its card (null on the cards and for stops without one).</summary>
        PanelSlot? PreferredSlot => index >= 0 && index < steps.Count ? PanelPlacement.Preferred(steps[index].Step.Side) : null;

        /// <summary>True on a stop that waits for the viewer (it asks them to do something) instead of moving on.</summary>
        bool Waits => active && steps != null && index >= 0 && index < steps.Count && steps[index].Step.Wait;

        static ViewPreset EndPreset() => ResolvedStep.FindPreset("overview") ?? ViewPresets.All[0];

        // ------------------------------------------------------------------ steps

        void GoTo(int stop)
        {
            // leaving a stop that plays the opening ends it (everything shown)
            Genesis genesis = Genesis;
            if (genesis != null && genesis.Playing) genesis.Stop();
            ClearCues();

            index = Mathf.Clamp(stop, -1, steps.Count);
            stepAge = 0;
            autoClock = 0;
            relocateTimer = RelocateCooldown;
            target = null;
            targetCaption = null;
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
            // a view with states of its own opens on the first stop's (a story's first year), not whatever it showed last
            if (steps.Count > 0 && steps[0].Step.Cues.Count > 0 && steps[0].Preset != null && ViewVariants.TryGet(steps[0].Preset.Id, out ViewVariants.Entry first))
                for (int i = 0, n = first.Count(); i < n; i++)
                    if (first.Name(i) == steps[0].Step.Cues[0].Show) { first.Show(i); cueView = first; cueShown = i; break; }
            Highlighter.Clear();
            narrator.Stop();
            stepDuration = TitleCardSeconds;

            float seconds = 0;
            foreach (ResolvedStep s in steps) seconds += s.Duration;
            int minutes = Mathf.Max(1, Mathf.RoundToInt(seconds / 60f));
            panel.Present(new PanelContent
            {
                Card = true,
                Kicker = TourScript.Story != null ? "A story" : "Guided tour",
                Title = script.Title,
                Body = (!string.IsNullOrEmpty(script.Intro)
                           ? script.Intro + $" {steps.Count} stops, about {minutes} minutes. "
                           : GraphScene.IsEconomy
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

        Genesis Genesis => root != null ? root.Modules.OfType<Genesis>().FirstOrDefault() : null;

        void ShowStep(ResolvedStep s)
        {
            // anchors of what is built after loading (the followed person, the dilemma's groups) may exist only by now:
            // a tour started as the scene opens resolves its stops before they do
            if (s.Target == null && !string.IsNullOrWhiteSpace(s.Step.AnchorKey)) Anchors.TryGet(s.Step.AnchorKey, out s.Target);
            ViewPreset preset = s.Preset ?? root.CurrentPreset ?? ViewPresets.All[0];
            bool hasTarget;
            Vector2 placeAt;
            Genesis genesis = s.Step.Effect == "genesis" ? Genesis : null;
            if (genesis != null)
            {
                // the opening is the camera's flight: from the flash back to the whole graph, timed to the narration
                // (its re-scale is the tour's own, not the viewer taking over)
                focusing = true;
                genesis.Play(Mathf.Max(s.Duration - 2f, 8f), preset, false);
                focusing = false;
                hasTarget = false;
                placeAt = Vector2.zero;
            }
            else
            {
                hasTarget = Frame(preset, s.Target, out placeAt);
            }

            target = s.Target;
            targetCaption = s.Step.Caption;
            markers.AddRange(s.Highlights);
            Highlight(s.Target, s.Highlights, GraphStyle.HighlightGlow, HighlightDim);
            stepDuration = s.Duration;
            narrator.Play(s.Narration);

            // the view's title, unless the stop's own title already says it
            string section = string.IsNullOrEmpty(preset.Title) || string.Equals(preset.Title, s.Step.Title, System.StringComparison.OrdinalIgnoreCase)
                ? "" : "   \u00B7   " + preset.Title;
            PanelSlot? side = PanelPlacement.Preferred(s.Step.Side);
            panel.Present(new PanelContent
            {
                Kicker = $"{index + 1} / {steps.Count}{section}",
                Title = s.Step.Title,
                Key = KeyText(s.Step),
                Reading = s.Step.Reading,
                Body = s.Step.Text,
                Footnote = Evidence(s.Step),
                Typewriter = true,
                Transport = true
            }, size => PanelPlacement.Choose(size, CanvasSize, hasTarget, placeAt, false, side));
        }

        /// <summary>
        /// A stop's swatches as one rich-text line: a dot in each color and what it marks, each kept together on a line
        /// (the panel sets the sentence on how to read the view under them).
        /// </summary>
        static string KeyText(TourStep step)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (TourSwatch swatch in step.Key)
            {
                if (sb.Length > 0) sb.Append("    ");
                sb.Append("<nobr><color=#").Append(UiFactory.Hex(swatch.Resolve())).Append(">\u25CF</color> ")
                    .Append(swatch.Label).Append("</nobr>");
            }

            return sb.Length > 0 ? sb.ToString() : null;
        }

        /// <summary>
        /// Where a stop's picture comes from, in the three kinds the narration keeps apart: what is measured, what Jev (an
        /// AI model) judged, and what the drawing assumes.
        /// </summary>
        static string Evidence(TourStep step)
        {
            List<string> lines = new List<string>(3);
            if (!string.IsNullOrWhiteSpace(step.Measured)) lines.Add("Measured: " + step.Measured);
            if (!string.IsNullOrWhiteSpace(step.Judged)) lines.Add("Judged by Jev, an AI model: " + step.Judged);
            if (!string.IsNullOrWhiteSpace(step.Assumed)) lines.Add("Assumed: " + step.Assumed);
            return lines.Count > 0 ? string.Join("  \u00B7  ", lines) : null;
        }

        void ShowEndCard()
        {
            if (TourScript.Story != null && !string.IsNullOrEmpty(script.End))
            {
                // a story's end stays on its last picture, in its own words
                Highlighter.Clear();
                narrator.Stop();
                stepDuration = 0;
                panel.Present(new PanelContent
                {
                    Card = true,
                    Kicker = "The end of the story",
                    Title = script.Title,
                    Body = script.End,
                    PrimaryLabel = "Explore",
                    Primary = ExitTour,
                    SecondaryLabel = "Restart",
                    Secondary = Restart
                }, size => PanelPlacement.Choose(size, CanvasSize, false, Vector2.zero, true));
                return;
            }

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
            bool xr = Xr.XrSession.Active;
            string lit = lineage.Count == 0 ? ""
                : economy ? "The glowing players own capital: the few who steer their own path. "
                : "The glowing path is the chain of causes that led to you. ";
            // the narration's bridge: the economy is where the tour goes next, and the card says so too; the controls
            // after it are the other choice, so they say what they explore
            string next = economy ? ""
                : "The newest link, the one people build on one another, is an economy: Continue opens the economy of " +
                  "the United States, where the road of American lives goes on. ";
            string explore = economy ? "Explore it on your own" : "Or explore the universe on your own";
            panel.Present(new PanelContent
            {
                Card = true,
                Kicker = "End of the tour",
                Title = economy ? "Every dollar is someone's choice, made in fear or desire."
                    : "This moment is the result of everything before it.",
                Footnote = economy
                    ? "A model, not a measurement: public accounts, judgments by Jev (an AI model) and the drawing's own " +
                      "assumptions, kept apart on each stop's card."
                    : null,
                Body = lit + next + (xr
                    ? explore + " with your hands: pinch and pull to drag the world, pinch with both hands " +
                      "to grow, shrink or turn it, point and pinch to select. Turn your left palm toward you for the " +
                      "menu."
                    : economy
                    ? explore + ": scroll to zoom, right-drag to orbit, the number keys to change the " +
                      "view. Comma and period change the year the land opens; click a player or a lifeline to look " +
                      "inside it. H lists every control; T takes the tour again."
                    : explore + ": scroll to zoom, right-drag to orbit, the number keys to " +
                      "re-scale. U unrolls the clock where you look ([ and ] widen or narrow the window, L " +
                      "switches between log and linear time). H lists every control; T takes the tour again."),
                PrimaryLabel = economy ? "Explore" : "Continue: the economy",
                Primary = economy ? (System.Action)ExitTour : ContinueIntoEconomy,
                SecondaryLabel = economy ? "Restart" : "Explore",
                Secondary = economy ? (System.Action)Restart : ExitTour
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
            float dt = GraphTime.Delta;
            HandleKeys();
            if (active) TickAutoplay(dt);
            panel.Tick(dt);
        }

        void HandleKeys()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null || TextFieldFocused() || CameraRig.InputLocked) return;
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
            // on a screen, steering the camera or working another panel (a picker, a form) pauses the tour to explore;
            // in a headset you move all the time (the world is in your hands), so only the menu's pause stops it
            if (autoplay && !Xr.XrSession.Active && (root.Rig.UserActive || ClickedOtherPanel())) SetAutoplay(false, true);

            // the end card waits for the viewer, and so does a stop that asks them to do something
            if (autoplay && index < steps.Count && !panel.IsHidden)
            {
                autoClock += dt;
                if (autoClock >= stepDuration && !Waits) GoTo(index + 1);
            }

            ApplyCues();
            bool timed = index < steps.Count && !Waits;
            panel.SetProgress(timed ? autoClock / Mathf.Max(stepDuration, 0.01f) : -1, autoplay);
        }

        /// <summary>True on the frame the viewer presses on a panel other than the tour's own.</summary>
        bool ClickedOtherPanel()
        {
            Pointer pointer = Pointer.current;
            EventSystem es = EventSystem.current;
            if (pointer == null || es == null || !pointer.press.wasPressedThisFrame) return false;
            uiHits.Clear();
            es.RaycastAll(new PointerEventData(es) { position = pointer.position.ReadValue() }, uiHits);
            return uiHits.Count > 0 && !uiHits[0].gameObject.transform.IsChildOf(canvas.transform);
        }

        /// <summary>
        /// Shows the view state the current stop's cues name for this moment of the stop (its clock runs with the voice,
        /// so a paused tour keeps the state on screen). A view whose states are not registered yet is tried again next frame.
        /// </summary>
        void ApplyCues()
        {
            if (index < 0 || index >= steps.Count) return;
            ResolvedStep s = steps[index];
            if (s.Step.Cues.Count == 0 || s.Preset == null || !ViewVariants.TryGet(s.Preset.Id, out ViewVariants.Entry view)) return;
            TourCue due = null;
            foreach (TourCue c in s.Step.Cues)
            {
                if (c.At <= autoClock) due = c;
            }

            if (due == null) return;
            int state = -1, count = view.Count();
            for (int i = 0; i < count && state < 0; i++)
            {
                if (view.Name(i) == due.Show) state = i;
            }

            if (state < 0 || (state == cueShown && view == cueView)) return;
            view.Show(state);
            cueView = view;
            cueShown = state;
        }

        /// <summary>Leaving a stop whose cues set its view's state returns the view to its own default.</summary>
        void ClearCues()
        {
            cueView?.Show(-1);
            cueView = null;
            cueShown = -1;
        }

        void LateUpdate()
        {
            // after the camera rig and the warp have moved this frame
            if (root != null) UpdateAttention(GraphTime.Delta);
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
            // a headset marks the target in the world instead (Xr.XrBeacon): a screen arrow cannot point in stereo
            if (Xr.XrSession.Active) alpha = 0;
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
                // the ring stands at a target on screen (a clamped one gets none)
                ringKeepOut.anchoredPosition = tip;
                if (ringKeepOut.gameObject.activeSelf == clamped) ringKeepOut.gameObject.SetActive(!clamped);
                float captionAlpha = alpha * Smooth(Mathf.InverseLerp(0.5f, 1f, arrowReveal));
                PlaceCaption(tip, clamped, offscreenDir, captionAlpha, size);
                KeepPanelClear(panelRect, tip, clamped, size);
            }
            else
            {
                arrow.ClearArrow(alpha);
                if (ringKeepOut.gameObject.activeSelf) ringKeepOut.gameObject.SetActive(false);
                SetCaptionAlpha(0);
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
            arrowCaption = targetCaption;
            arrowMarkers.Clear();
            arrowMarkers.AddRange(markers);
            arrowReveal = 0;
            if (arrowTarget == null) return;
            caption.text = string.IsNullOrEmpty(arrowCaption) ? CaptionText(arrowTarget) : arrowCaption;
            captionWidth = caption.GetPreferredValues(caption.text).x;
        }

        void HideAttention(float dt)
        {
            attentionShown = false;
            arrow.ClearArrow(0);
            if (ringKeepOut.gameObject.activeSelf) ringKeepOut.gameObject.SetActive(false);
            arrow.Markers.Clear();
            arrow.Animate(dt);
            SetCaptionAlpha(0);
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
            SetCaptionAlpha(alpha);
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
            // the plate spans the text from the same side, a margin wider
            RectTransform plate = captionPlate.rectTransform;
            plate.pivot = rt.pivot;
            plate.anchoredPosition = pos + new Vector2(left ? CaptionPlateMargin.x : -CaptionPlateMargin.x, 0);
            plate.sizeDelta = new Vector2(captionWidth + 2 * CaptionPlateMargin.x, CaptionSize * 1.35f + 2 * CaptionPlateMargin.y);
        }

        /// <summary>
        /// Fades the arrow's caption and its plate together. The plate is shown only while the caption is and has text (the
        /// Now beacon's is empty, where the plate stood beside the ring as a dark pill on the bright lines).
        /// </summary>
        void SetCaptionAlpha(float alpha)
        {
            caption.alpha = alpha;
            Color c = CaptionPlateColor;
            c.a *= alpha;
            captionPlate.color = c;
            bool shown = alpha > 0.001f && captionWidth > 0;
            if (captionPlate.gameObject.activeSelf != shown) captionPlate.gameObject.SetActive(shown);
        }

        /// <summary>If the viewer moves the anchor under the panel, the panel moves to another slot.</summary>
        void KeepPanelClear(Rect panelRect, Vector2 tip, bool clamped, Vector2 size)
        {
            // only once the view has settled: mid-flight the target sweeps across the screen
            if (!active || clamped || !panel.IsSettled || relocateTimer > 0) return;
            if (root.Rig.Flying || GraphWarp.Animating) return;
            if (PanelPlacement.Distance(panelRect, tip) > 12f) return;
            bool card = index < 0 || index >= steps.Count;
            PanelSlot? side = PreferredSlot;
            panel.Relocate(s => PanelPlacement.Choose(s, size, true, tip, card, side));
            relocateTimer = RelocateCooldown;
        }

        /// <summary>The anchor's label and, unless the label already carries a date, when it happened (dim).</summary>
        static string CaptionText(Anchor a)
        {
            // the beacon names the present itself: a caption beside its ring printed "Now" a second time
            if (a.Key == "now") return "";
            string label = string.IsNullOrEmpty(a.Label) ? a.Key : a.Label;
            // "time:" and figures ("Name (born-died)") are labeled with their dates already; the economy's anchors are
            // all in its own recent years, where a deep-time age ("22 months ago") reads as noise
            if (a.Key.StartsWith("time:", System.StringComparison.Ordinal) || GraphScene.IsEconomy) return label;
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

    /// <summary>A chapter of the guided tour: a view and the first stop that shows it.</summary>
    public struct TourChapter
    {
        public string PresetId;
        public string Title;
        public int FirstStep;
    }
}
