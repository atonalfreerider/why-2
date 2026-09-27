using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Why.UI
{
    /// <summary>
    /// The on-screen UI around the graph (everything except the guided tour's own panel): the current
    /// view's title, the preset bar, the level legend with the lifeline readout, the tour and help
    /// buttons, label tooltips and click-to-focus, the controls sheet and F3 stats. All neutral grey and
    /// white; the legend swatches are the only hue. While the tour runs, the title and preset bar step
    /// aside for the director.
    /// </summary>
    public sealed class Hud : GraphModule
    {
        /// <summary>Below the director's tour panel (50), so a tour card is never covered by a corner block.</summary>
        const int SortingOrder = 40;

        /// <summary>The help sheet and tooltips sit above every panel.</summary>
        const int OverlaySortingOrder = 200;

        const float TitleWidth = 560f;
        const float TopButtonHeight = 30f;

        /// <summary>A left click that moves further than this (1080p pixels) is a drag, not a click.</summary>
        const float ClickSlopPx = 6f;

        // click-to-focus
        const float FocusSeconds = 1.3f;
        const float FocusZoom = 0.6f;
        const float FocusMinDistance = 0.3f;
        const float FocusDim = 0.6f;

        GraphRoot root;
        Canvas overlay;
        RectTransform canvasRect;
        UiFade hudFade, titleFade, presetFade, tourButtonFade;
        TextMeshProUGUI presetTitle, presetSubtitle;
        HudPresetBar presetBar;
        HudLegend legend;
        HudTooltip tooltip;
        HudHelp help;
        HudStats stats;

        Vector2 laidOutSize;
        bool loaded, tourWasActive, ownsHighlight, pressValid;
        IdRange focusIds = IdRange.Empty;
        Vector2 pressPosition;
        LabelSpec resolvedLabel;
        Anchor resolvedAnchor;

        public override void Init(GraphRoot graphRoot)
        {
            root = graphRoot;
            canvasRect = (RectTransform)UiFactory.CreateCanvas("Hud", SortingOrder, transform).transform;
            RectTransform content = UiFactory.Rect(canvasRect, "Content").Fill();

            // built while active so every text can be measured; the fades are created afterwards
            BuildTitle(content);
            float stackTop = BuildTopButtons(content);
            legend = new HudLegend(content);
            presetBar = new HudPresetBar(content, p => root.Focus(p));
            stats = new HudStats(content, stackTop);

            overlay = UiFactory.CreateCanvas("HudOverlay", OverlaySortingOrder, transform);
            help = new HudHelp(overlay.transform);
            tooltip = new HudTooltip(overlay.transform);

            presetFade = new UiFade(presetBar.Rect.gameObject, 1, 4f, true);
            hudFade = new UiFade(content.gameObject, 0, 1.6f, true);

            root.FocusChanged += OnFocusChanged;
        }

        public override void OnLoaded(GraphRoot graphRoot)
        {
            loaded = true;
            hudFade.Show(true);
            OnFocusChanged(root.CurrentPreset);
        }

        void OnDestroy()
        {
            if (root != null) root.FocusChanged -= OnFocusChanged;
        }

        // ------------------------------------------------------------------ building

        void BuildTitle(RectTransform parent)
        {
            RectTransform block = UiFactory.Rect(parent, "Title")
                .Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(HudKit.Margin, -HudKit.Margin + 2),
                    new Vector2(TitleWidth, 90));

            TextMeshProUGUI brand = HudKit.Line(block, "Brand", "WHY", HudKit.SizeSmall, GraphStyle.TextDim, FontStyles.Bold);
            brand.characterSpacing = 30;
            HudKit.PlaceTopLeft(brand.rectTransform, 0, 0, HudKit.FitText(brand));

            presetTitle = HudKit.Line(block, "Preset", "", HudKit.SizeTitle, GraphStyle.Text);
            HudKit.PlaceTopLeft(presetTitle.rectTransform, 0, 21, new Vector2(TitleWidth, 24));

            presetSubtitle = UiFactory.Text(block, "Subtitle", "", HudKit.SizeSmall, GraphStyle.TextDim);
            HudKit.PlaceTopLeft(presetSubtitle.rectTransform, 0, 47, new Vector2(TitleWidth, 20));

            titleFade = new UiFade(block.gameObject, 0, 2.5f, false);
        }

        /// <summary>Tour and help buttons, top-right. Returns the y below them (for the stats panel).</summary>
        float BuildTopButtons(RectTransform parent)
        {
            string faint = "<color=#" + UiFactory.Hex(GraphStyle.TextDim) + ">";
            Button tour = TopButton(parent, "TourButton", "Guided tour  " + faint + "(T)</color>", root.RequestTour);
            ((Image)tour.targetGraphic).color = new Color(1, 1, 1, 0.1f);
            RectTransform tourRect = (RectTransform)tour.transform;
            tourRect.anchoredPosition = new Vector2(-HudKit.Margin, -HudKit.Margin);

            Button helpButton = TopButton(parent, "HelpButton", "Help  " + faint + "(H)</color>", () => help.Toggle());
            ((RectTransform)helpButton.transform).anchoredPosition =
                new Vector2(-HudKit.Margin - tourRect.sizeDelta.x - HudKit.Gap * 0.6f, -HudKit.Margin);

            tourButtonFade = new UiFade(tour.gameObject, 1, 4f, true);
            return HudKit.Margin + TopButtonHeight + HudKit.Gap;
        }

        static Button TopButton(RectTransform parent, string name, string label, System.Action onClick)
        {
            Button b = HudKit.Button(parent, name, label, HudKit.SizeBody, onClick);
            TextMeshProUGUI text = b.GetComponentInChildren<TextMeshProUGUI>();
            float width = HudKit.Measure(text, label).x + 30;
            ((RectTransform)b.transform).Place(Vector2.one, Vector2.one, Vector2.zero, new Vector2(width, TopButtonHeight));
            return b;
        }

        // ------------------------------------------------------------------ per frame

        void Update()
        {
            if (!loaded) return;

            bool tour = root.TourActive;
            if (tour != tourWasActive)
            {
                tourWasActive = tour;
                if (tour)
                {
                    // the director owns attention now; never clear its highlight from here
                    ownsHighlight = false;
                    help.Show(false);
                }

                titleFade.Show(!tour);
                presetFade.Show(!tour);
                tourButtonFade.Show(!tour);
            }

            HandleKeys(tour);
            HandleClicks(tour);
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            hudFade.Tick(dt);
            if (!loaded) return;

            Vector2 size = canvasRect.rect.size;
            if (size != laidOutSize)
            {
                laidOutSize = size;
                Relayout(size);
            }

            titleFade.Tick(dt);
            presetFade.Tick(dt);
            tourButtonFade.Tick(dt);
            legend.Tick(dt);
            help.Tick(dt);
            stats.Tick(dt, root);
            UpdateTooltip(size, dt);
        }

        void HandleKeys(bool tour)
        {
            Keyboard kb = Keyboard.current;
            if (kb == null || HudKit.TypingInField()) return;

            if (kb.hKey.wasPressedThisFrame || kb.slashKey.wasPressedThisFrame) help.Toggle();
            if (kb.f3Key.wasPressedThisFrame) stats.Toggle();
            if (kb.escapeKey.wasPressedThisFrame)
            {
                if (help.Open) help.Show(false);
                else if (!tour) ClearFocus();
            }
        }

        /// <summary>A left click (not a drag, not over UI) on a label focuses it; on empty space it clears.</summary>
        void HandleClicks(bool tour)
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;
            Vector2 position = mouse.position.ReadValue();

            if (mouse.leftButton.wasPressedThisFrame)
            {
                Keyboard kb = Keyboard.current;
                bool shift = kb != null && kb.shiftKey.isPressed; // shift + left drag pans
                pressValid = !shift && !tour && !help.Open && !HudKit.PointerOverUi();
                pressPosition = position;
            }

            if (!mouse.leftButton.wasReleasedThisFrame || !pressValid) return;
            pressValid = false;
            float slop = ClickSlopPx * LabelSystem.UiScale;
            if ((position - pressPosition).sqrMagnitude > slop * slop) return;

            LabelSpec hovered = root.Labels.Hovered;
            if (hovered != null) FocusLabel(hovered);
            else ClearFocus();
        }

        /// <summary>Highlight what a label stands for and fly closer to its anchor.</summary>
        void FocusLabel(LabelSpec label)
        {
            bool hasAnchor = Anchors.TryGet(label.AnchorKey, out Anchor anchor);
            IdRange ids = hasAnchor && !anchor.Ids.IsEmpty ? anchor.Ids : label.Ids;
            if (!ids.IsEmpty)
            {
                Highlighter.Set(new[] { ids }, GraphStyle.HighlightGlow, FocusDim);
                ownsHighlight = true;
                focusIds = ids;
            }
            else
            {
                ClearFocus();
            }

            // aim at where the point will be once any running re-scale has finished
            Vector3 data = hasAnchor ? anchor.Data : label.Data;
            CameraPose pose = root.Rig.Pose;
            pose.Target = GraphWarp.ToWorld(data.x, data.y, data.z, GraphWarp.Target);
            pose.Distance = Mathf.Max(pose.Distance * FocusZoom, Mathf.Min(pose.Distance, FocusMinDistance));
            root.Rig.FlyTo(pose, FocusSeconds);
        }

        /// <summary>
        /// Clear the highlight set by a click, but only while it is still the active one: the director and
        /// other modules (e.g. following a figure) replace it with their own, which is theirs to clear.
        /// </summary>
        void ClearFocus()
        {
            if (!ownsHighlight) return;
            ownsHighlight = false;
            if (Highlighter.HasHighlight && Highlighter.IsHighlighted(focusIds)) Highlighter.Clear();
        }

        void OnFocusChanged(ViewPreset preset)
        {
            if (!loaded || preset == null) return;
            presetBar.SetActive(preset);

            // a new view fades its title in; re-scaling the same lens only updates the subtitle
            string title = preset.Title ?? "";
            bool newTitle = title != presetTitle.text;
            presetTitle.text = title;
            presetSubtitle.text = preset.Subtitle ?? "";
            HudKit.FitText(presetSubtitle, TitleWidth);
            if (newTitle && !root.TourActive) titleFade.Replay();
        }

        /// <summary>
        /// Keep the preset bar centered but clear of the legend; when the screen is too narrow for both on
        /// one line, the bar takes the bottom edge (scaled to fit) and the legend moves above it.
        /// </summary>
        void Relayout(Vector2 size)
        {
            float barWidth = presetBar.Width;
            float clearOfLegend = HudKit.Margin + legend.Width + HudKit.Gap * 2;
            float left = Mathf.Max((size.x - barWidth) * 0.5f, clearOfLegend);
            if (left + barWidth <= size.x - HudKit.Margin)
            {
                presetBar.Place(left, HudKit.Margin, 1);
                legend.SetBottom(HudKit.Margin);
                return;
            }

            float scale = Mathf.Min(1f, (size.x - 2 * HudKit.Margin) / barWidth);
            presetBar.Place((size.x - barWidth * scale) * 0.5f, HudKit.Margin, scale);
            legend.SetBottom(HudKit.Margin + HudPresetBar.Height * scale + HudKit.Gap);
        }

        /// <summary>Preset buttons explain their view; labels with an anchor explain what they mark.</summary>
        void UpdateTooltip(Vector2 canvasSize, float dt)
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;
            Vector2 pointer = mouse.position.ReadValue() / Mathf.Max(overlay.scaleFactor, 1e-4f);

            ViewPreset preset = presetFade.Shown ? presetBar.HoveredPreset : null;
            if (preset != null)
            {
                tooltip.Show(preset, preset.Title, null, preset.Subtitle);
            }
            else if (!help.Open && TryHoveredAnchor(out LabelSpec label, out Anchor anchor))
            {
                tooltip.ShowAnchor(label, anchor, label.Text);
            }
            else
            {
                tooltip.Hide();
            }

            tooltip.Tick(pointer, canvasSize, dt);
        }

        /// <summary>The hovered label's anchor, resolved once per label ("time:" anchors are built on demand).</summary>
        bool TryHoveredAnchor(out LabelSpec label, out Anchor anchor)
        {
            label = root.Labels.Hovered;
            anchor = null;
            if (label == null || HudKit.PointerOverUi()) return false;
            if (label != resolvedLabel)
            {
                resolvedLabel = label;
                resolvedAnchor = Anchors.TryGet(label.AnchorKey, out Anchor a) ? a : null;
            }

            anchor = resolvedAnchor;
            return anchor != null;
        }
    }
}
