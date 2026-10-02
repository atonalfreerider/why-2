using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Why.Economy.Data;
using Why.Economy.Land;
using Why.Economy.Model;
using Why.Humans.Smv;
using Why.UI;

namespace Why.Economy.UI
{
    /// <summary>
    /// The economy scene's year and its people, from the keyboard and a small chip (SPEC 1.3, 7.6):
    /// <list type="bullet">
    /// <item>comma and period step <see cref="EconomyState.Year"/> back and forward by one year (with Shift by ten), clamped
    /// to 1950-2026: the cut slides along the road to it and the land opens that year;</item>
    /// <item>the YEAR CHIP at the right edge under the HUD's tour and help buttons: the year with ‹ › (they click), and under
    /// them a scrubber over 1950-2026. Dragging it moves only the cut (<see cref="EconomyState.SetPreviewYear"/>);
    /// releasing it opens the land at that year (<see cref="EconomyState.SetYear"/>). Hovering the chip shows the money
    /// circuit's notes for the year (its approximations and residuals). Beneath it the year's readout (generated:
    /// "2025 · GDP $30.8T · in control 12% · fantasy 19% · fear 54% · trust 25% (GSS) · cooperation 0.73"). On a portrait
    /// screen with the land in view the bowl fills the band under the title: the chip keeps only its year row (dragging
    /// across it scrubs) and the readout moves into the land legend at the bottom, as it does wherever a view's bowl
    /// reaches the readout's place;</item>
    /// <item>N meets the next notable person of the model (owner, heir, striver, escapist, the indebted ...): it selects
    /// them (<see cref="EconomyState.Person"/>; the inspector shows them and lights their lifeline and their dot). On the
    /// road the camera flies to their line at the year, into the room the inspector leaves (<see cref="PersonFraming"/>);
    /// in a view of the land it flies to their player, whose dot lights. While the player inspector is open (or a member
    /// of its player is shown), N cycles that player's members instead.</item>
    /// </list>
    /// Everything steps aside during the guided tour, like the HUD's own controls. On a portrait screen the chip stands in
    /// the column beside the HUD's title and the readout under the title (see <see cref="EconomyUiLayout"/>).
    /// </summary>
    [GraphScenes(GraphScene.Economy)]
    public sealed class EconomyControls : GraphModule
    {
        /// <summary>Above the HUD (40), with the person inspector, below the social panel (45), the director's tour panel
        /// (50) and the HUD's overlay (200).</summary>
        const int SortingOrder = 44;

        /// <summary>
        /// The chip: its year row's height, arrow buttons, the year's font size; the scrubber's row, track and handle; the
        /// chip's width (77 years over ~170 units: about two units a year); the readout's height (reference pixels).
        /// </summary>
        const float RowHeight = 34f, ArrowSize = 24f, ArrowGap = 3f, YearSize = HudKit.SizeTitle, ScrubRow = 16f, TrackHeight = 3f,
            HandleSize = 9f, ChipWidth = 196f, ReadoutHeight = 26f;

        /// <summary>Years a step takes with Shift held.</summary>
        const int ShiftStep = 10;

        /// <summary>Seconds of the flight to a person's line or player.</summary>
        const float FlySeconds = 1.6f;

        /// <summary>
        /// Camera distance to a person's line (world units): about thirty years of the road across a landscape screen and
        /// the whole height of the population, so the lit line reads among its neighbors. Portrait steps back like the
        /// narrow views do (ViewPreset.NarrowPullback).
        /// </summary>
        const float PersonDistance = 4f, PortraitPullback = 1.25f;

        /// <summary>Camera distance to a player on the land's rim (its glyph and its neighbors fill the middle of the screen).</summary>
        const float PlayerDistance = 6.5f;

        const float FadeSpeed = 4f;

        /// <summary>
        /// The canvas box the chip and the readout take while shown (empty otherwise), for the inspectors that hang beneath
        /// them. Canvas units of any canvas made by <see cref="UiFactory.CreateCanvas"/>.
        /// </summary>
        public static UiBox Occupied { get; private set; } = UiBox.Empty;

        /// <summary>
        /// True while the year's readout has no place under the chip (the view's bowl reaches there, or a portrait screen
        /// with the land in view): the land legend shows it instead.
        /// </summary>
        public static bool ReadoutMoved { get; private set; }

        GraphRoot root;
        RectTransform canvasRect, chip, readout, track, fill, handle;
        TextMeshProUGUI yearText, readoutText;
        Button prev, next;
        UiFade chipFade, readoutFade;
        HudTooltip tooltip;
        HudBlocks hud;
        EconomyModel model;
        SmvPopulation pop;
        YearSeries trust;
        bool loaded, dirty = true, checkPending, dragging;

        /// <summary>The chip without its scrubber row (a portrait screen with the land in view): dragging across it scrubs.</summary>
        bool compactChip;
        int seenVersion = -1, shownYear = int.MinValue, seenLand = -1;
        HudFrame laidOutFrame;
        Vector2 chipSize, readoutSize;

        /// <summary>The readout's text, made again only for another year, land or screen shape (the layout also runs while
        /// the HUD's blocks move).</summary>
        string readoutLine = "";

        int readoutYear = int.MinValue, readoutLand = -1;
        bool readoutCompact;
        UiBox chipBox, readoutBox;
        string loggedProblems = "";
        readonly List<string> problems = new List<string>();
        readonly Vector3[] corners = new Vector3[4];

        public override void Init(GraphRoot graphRoot)
        {
            root = graphRoot;
            Canvas canvas = UiFactory.CreateCanvas("EconomyControls", SortingOrder, transform);
            canvasRect = (RectTransform)canvas.transform;

            chip = HudKit.FramedPanel(canvasRect, "YearChip", 0.86f, true);
            yearText = HudKit.Line(chip, "Year", "2025", YearSize, GraphStyle.Text, FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft);
            prev = HudKit.Button(chip, "Prev", "‹", HudKit.SizeBody + 2, () => Step(-1));
            next = HudKit.Button(chip, "Next", "›", HudKit.SizeBody + 2, () => Step(1));

            // the scrubber: a thin track over 1950-2026, the part up to the year filled, a handle at the year
            track = UiFactory.Panel(chip, "Scrubber", new Color(1, 1, 1, 0.12f)).rectTransform;
            fill = UiFactory.Panel(track, "Fill", new Color(1, 1, 1, 0.35f), false).rectTransform;
            Image knob = UiFactory.Panel(track, "Handle", GraphStyle.Text, false);
            knob.pixelsPerUnitMultiplier = 14f / (HandleSize * 0.5f);
            handle = knob.rectTransform;

            readout = HudKit.FramedPanel(canvasRect, "YearReadout", 0.62f);
            readoutText = HudKit.Line(readout, "Text", "", HudKit.SizeSmall, GraphStyle.TextDim, FontStyles.Normal,
                TextAlignmentOptions.MidlineLeft);
            readoutText.textWrappingMode = TextWrappingModes.Normal; // a portrait screen wraps it under the title
            tooltip = new HudTooltip(canvasRect);

            // created last: a hidden fade deactivates its panel
            chipFade = new UiFade(chip.gameObject, 0, FadeSpeed, true);
            readoutFade = new UiFade(readout.gameObject, 0, FadeSpeed, false);
        }

        public override void OnLoaded(GraphRoot graphRoot)
        {
            model = root.Context.Shared<EconomyModel>(EconomyModel.SharedKey);
            pop = root.Context.Shared<SmvPopulation>(SmvPopulation.SharedKey);
            trust = model?.Data?.Games?.Tribes?.TrustSeries();
            hud = new HudBlocks(root);
            loaded = true;
        }

        void OnDestroy()
        {
            Occupied = UiBox.Empty;
            ReadoutMoved = false;
            EconomyState.SetPreviewYear(-1);
        }

        /// <summary>
        /// Whether a preset opens the land (its transition's target is the bowl) and is not the road's overview: the views
        /// where the people are players on the rim.
        /// </summary>
        public static bool IsLandView(ViewPreset preset) =>
            preset != null && preset.Id != "overview" && EconomyViews.Get(preset.Id).MorphTarget > 0;

        // ------------------------------------------------------------------ input

        void Update()
        {
            if (!loaded) return;
            bool tour = root.TourActive;
            bool show = !tour && model != null;
            if (show != chipFade.Shown) Show(show);
            if (!tour)
            {
                HandleKeys();
                HandleScrubber();
            }
            else if (dragging) EndDrag(false);

            if (EconomyState.Version != seenVersion || LandService.Version != seenLand) Sync();
        }

        void Show(bool show)
        {
            chipFade.Show(show);
            readoutFade.Show(show && model?.Lives != null && model.Lives.Ready);
            dirty = true; // measure again now that the texts are active
        }

        void HandleKeys()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null || HudKit.TypingInField()) return;
            int step = kb.shiftKey.isPressed ? ShiftStep : 1;
            if (kb.commaKey.wasPressedThisFrame) EconomyState.SetYear(EconomyState.Year - step);
            if (kb.periodKey.wasPressedThisFrame) EconomyState.SetYear(EconomyState.Year + step);
            if (kb.nKey.wasPressedThisFrame) NextPerson();
        }

        /// <summary>The chip's arrows: a year back or forward.</summary>
        static void Step(int direction) => EconomyState.SetYear(EconomyState.Year + direction);

        // ------------------------------------------------------------------ the scrubber

        /// <summary>
        /// A press on the scrubber's row starts a drag: the year under the pointer is the preview (the cut slides there);
        /// releasing sets the year (the land opens it). Esc cancels the drag.
        /// </summary>
        void HandleScrubber()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || !chipFade.Shown) return;
            Vector2 pointer = mouse.position.ReadValue();
            if (!dragging && mouse.leftButton.wasPressedThisFrame && OverScrubber(pointer))
            {
                dragging = true;
            }

            if (!dragging) return;
            int year = YearAt(pointer);
            if (year != EconomyState.PreviewYear)
            {
                EconomyState.SetPreviewYear(year);
                dirty = true;
            }

            Keyboard kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) EndDrag(false);
            else if (!mouse.leftButton.isPressed) EndDrag(true);
        }

        void EndDrag(bool commit)
        {
            dragging = false;
            int year = EconomyState.PreviewYear;
            EconomyState.SetPreviewYear(-1);
            if (commit && year >= 0) EconomyState.SetYear(year);
            dirty = true;
        }

        /// <summary>
        /// Whether a screen point is on the scrubber's row (the track and a few units around it); on the compact chip (no
        /// track), on the chip left of its arrows.
        /// </summary>
        bool OverScrubber(Vector2 screen)
        {
            if (!ScreenRect(chip, out Rect c)) return false;
            float scale = c.height / Mathf.Max(1, chipSize.y);
            if (compactChip)
            {
                return ScreenRect((RectTransform)prev.transform, out Rect a) && screen.x >= c.xMin && screen.x < a.xMin && screen.y >= c.yMin &&
                       screen.y <= c.yMax;
            }

            return screen.x >= c.xMin && screen.x <= c.xMax && screen.y >= c.yMin && screen.y <= c.yMin + (ScrubRow + 4) * scale;
        }

        /// <summary>The year under a screen point along the track, or across the compact chip (clamped to the year range).</summary>
        int YearAt(Vector2 screen)
        {
            if (!ScreenRect(compactChip ? chip : track, out Rect t) || t.width <= 0) return EconomyState.CutYear;
            float f = Mathf.Clamp01((screen.x - t.xMin) / t.width);
            return Mathf.RoundToInt(Mathf.Lerp(EconomyState.MinYear, EconomyState.MaxYear, f));
        }

        /// <summary>A rect's box in screen pixels (the canvas is a screen-space overlay).</summary>
        bool ScreenRect(RectTransform rt, out Rect r)
        {
            r = default;
            if (rt == null || !rt.gameObject.activeInHierarchy) return false;
            rt.GetWorldCorners(corners);
            r = Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
            return true;
        }

        // ------------------------------------------------------------------ N: the next person

        /// <summary>
        /// The next member of the inspected player (while its inspector is in front, or one of its members is shown), else
        /// the notable person after the one shown (the first when nobody notable is); then the camera goes to them.
        /// </summary>
        void NextPerson()
        {
            EconomicLives lives = model?.Lives;
            if (lives == null || !lives.Ready || pop == null) return;
            int person = PlayerPanel.NextMember();
            if (person < 0)
            {
                IReadOnlyList<Notable> notables = lives.Notables;
                if (notables.Count == 0) return;
                int current = -1;
                for (int i = 0; i < notables.Count; i++)
                {
                    if (notables[i].Person == EconomyState.Person) current = i;
                }

                person = notables[(current + 1) % notables.Count].Person;
            }

            EconomyState.SetPerson(person);
            FlyToPerson(person);
        }

        /// <summary>
        /// In a view of the land: to the person's player on the rim (their dot lights with their line). On the road: to the
        /// person's line at the shown year (the nearest year of their life), keeping the view's direction, the point placed in
        /// the room the inspector leaves and moved toward the future so the life that led to the year shows.
        /// </summary>
        void FlyToPerson(int person)
        {
            if (root.Rig == null) return;
            CameraPose pose = root.Rig.Pose;
            if (IsLandView(root.CurrentPreset))
            {
                LandSnapshot s = LandService.Current;
                int[] of = s?.Players?.PlayerOfPerson;
                if (of == null || person >= of.Length || of[person] < 0) return;
                pose.Target = EconomyStage.Land().World(Figure.Head(s.Players.Players[of[person]]));
                pose.Distance = Mathf.Min(pose.Distance, PlayerDistance * (ScreenLayout.IsPortrait ? PortraitPullback : 1f));
                root.Rig.FlyTo(pose, FlySeconds);
                return;
            }

            // where the line will be once any running re-scale has finished
            WarpState warp = GraphWarp.Target;
            if (!LifelinePicker.WorldAt(pop, person, EconomyState.Year, warp, out Vector3 world)) return;
            float distance = PersonDistance * (ScreenLayout.IsPortrait ? PortraitPullback : 1f);
            pose.Target = world;
            pose.Distance = Mathf.Min(pose.Distance, distance);
            HudFrame f = hud.Measure(canvasRect, UiFactory.CanvasSize);
            Vector2 past = PersonFraming.PastDirection(pop, person, EconomyState.Year, warp, pose);
            Vector2 focus = EconomyUiLayout.PersonFocus(f, Occupied, PersonInspector.Shown, past);
            root.Rig.FlyTo(PersonFraming.Frame(pose, PersonFraming.ToScreenFraction(focus, f.Canvas), ScreenLayout.Aspect),
                FlySeconds);
        }

        // ------------------------------------------------------------------ the chip

        /// <summary>Shows the shared state's year and its readout.</summary>
        void Sync()
        {
            seenVersion = EconomyState.Version;
            seenLand = LandService.Version;
            int year = EconomyState.Year;
            prev.interactable = year > EconomyState.MinYear;
            next.interactable = year < EconomyState.MaxYear;
            if (year == shownYear && readoutLand == seenLand) return;
            shownYear = year;
            dirty = true; // the texts change size
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            chipFade.Tick(dt);
            readoutFade.Tick(dt);
            if (!loaded || !chipFade.Shown)
            {
                Occupied = UiBox.Empty;
                ReadoutMoved = false;
                tooltip.Hide();
                tooltip.Tick(Vector2.zero, UiFactory.CanvasSize, dt);
                return;
            }

            if (checkPending) SelfCheck();
            UpdateTooltip(dt);

            // the size the canvas has on this screen (on the frame the screen changes shape, the canvas itself may still
            // have its old size); the HUD's blocks as they are now (they move with the orientation, the F3 stats, ...)
            HudFrame frame = hud.Measure(canvasRect, UiFactory.CanvasSize);
            if (!dirty && frame.Near(laidOutFrame)) return;
            dirty = false;
            laidOutFrame = frame;
            Layout(frame);
        }

        /// <summary>The circuit's notes for the year while the pointer is over the chip.</summary>
        void UpdateTooltip(float dt)
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;
            Vector2 pointer = mouse.position.ReadValue();
            LandSnapshot s = LandService.Current;
            string notes = s != null && s.Year == EconomyState.Year ? s.Circuit?.Notes : null;
            bool over = !dragging && ScreenRect(chip, out Rect c) && c.Contains(pointer) && !string.IsNullOrEmpty(notes);
            if (over) tooltip.Show(chip, EconomyState.Year.ToString(LandFacts.Ci) + ": how the accounts were made", null, notes);
            else tooltip.Hide();
            float scale = canvasRect.rect.width > 0 ? Screen.width / canvasRect.rect.width : 1;
            tooltip.Tick(pointer / Mathf.Max(1e-4f, scale), canvasRect.rect.size, dt);
        }

        /// <summary>Sizes the chip and the readout to their texts and places them (see <see cref="EconomyUiLayout"/>).</summary>
        void Layout(HudFrame f)
        {
            // the year row: the year (the scrubber's preview while dragging), then the two arrows
            yearText.text = YearFacts.YearText(EconomyState.CutYear);
            yearText.color = EconomyState.PreviewYear >= 0 ? EconomyStyle.Capital : GraphStyle.Text;
            Vector2 year = HudKit.FitText(yearText);
            float pad = HudKit.Pad - 2;
            compactChip = EconomyUiLayout.ReadoutInLegend(f);
            chipSize = new Vector2(EconomyUiLayout.ChipWidth(f, ChipWidth), RowHeight + (compactChip ? 0 : ScrubRow));
            track.gameObject.SetActive(!compactChip);
            HudKit.PlaceTopLeft(yearText.rectTransform, pad, 0, new Vector2(year.x, RowHeight));
            float arrowTop = (RowHeight - ArrowSize) * 0.5f;
            HudKit.PlaceTopLeft((RectTransform)prev.transform, chipSize.x - pad + 4 - 2 * ArrowSize - ArrowGap, arrowTop,
                new Vector2(ArrowSize, ArrowSize));
            HudKit.PlaceTopLeft((RectTransform)next.transform, chipSize.x - pad + 4 - ArrowSize, arrowTop,
                new Vector2(ArrowSize, ArrowSize));

            // the scrubber's row: the track across the chip, filled to the year, the handle on it
            float trackWidth = chipSize.x - 2 * pad;
            HudKit.PlaceTopLeft(track, pad, RowHeight + (ScrubRow - TrackHeight) * 0.5f - 4, new Vector2(trackWidth, TrackHeight));
            float at = Mathf.InverseLerp(EconomyState.MinYear, EconomyState.MaxYear, EconomyState.CutYear) * trackWidth;
            HudKit.PlaceTopLeft(fill, 0, 0, new Vector2(at, TrackHeight));
            HudKit.PlaceTopLeft(handle, at - HandleSize * 0.5f, (TrackHeight - HandleSize) * 0.5f, new Vector2(HandleSize, HandleSize));
            chipBox = EconomyUiLayout.Chip(f, chipSize);
            Place(chip, chipBox, f);

            // the readout: one line, shorter on a portrait screen; measured while active (a hidden fade deactivates it,
            // and an inactive text may not measure), hidden again below when it has no place on this screen
            if (readoutYear != EconomyState.Year || readoutCompact != f.Portrait || readoutLand != LandService.Version)
            {
                readoutYear = EconomyState.Year;
                readoutCompact = f.Portrait;
                readoutLand = LandService.Version;
                readoutLine = YearFacts.Line(model?.Lives, trust, LandService.Current, model?.Data, readoutYear, readoutCompact,
                    UiFactory.Hex(GraphStyle.Text));
            }

            if (readoutText.text != readoutLine) readoutText.text = readoutLine;
            bool hasReadout = readoutLine.Length > 0;
            if (hasReadout) readoutFade.Show(true);
            Vector2 text = hasReadout ? HudKit.FitText(readoutText, EconomyUiLayout.ReadoutMaxWidth(f) - 2 * HudKit.Pad) : Vector2.zero;
            readoutSize = new Vector2(Mathf.Ceil(text.x + 2 * HudKit.Pad), Mathf.Max(ReadoutHeight, text.y + 8));
            HudKit.PlaceTopLeft(readoutText.rectTransform, HudKit.Pad, 0, new Vector2(text.x, readoutSize.y));
            readoutBox = hasReadout ? EconomyUiLayout.Readout(f, chipBox, readoutSize) : UiBox.Empty;
            ReadoutMoved = hasReadout && readoutBox.IsEmpty;
            readoutFade.Show(!readoutBox.IsEmpty);
            if (!readoutBox.IsEmpty) Place(readout, readoutBox, f);

            Occupied = UiBox.Union(chipBox, readoutBox);
            checkPending = true;
        }

        /// <summary>Anchors a panel by its top-right corner (it grows to the left), at a box of the canvas.</summary>
        static void Place(RectTransform rt, UiBox box, HudFrame f)
        {
            rt.Place(Vector2.one, Vector2.one, new Vector2(-(f.Canvas.x - box.Right), -box.Y), new Vector2(box.W, box.H));
        }

        /// <summary>
        /// The runtime self-check, a frame after a layout (the RectTransforms have settled): the chip and the readout must
        /// keep clear of the HUD's blocks and the bowl, and sit where the layout put them (anchors and pivots as intended).
        /// Logs once per distinct problem.
        /// </summary>
        void SelfCheck()
        {
            checkPending = false;
            problems.Clear();
            EconomyUiLayout.Check(laidOutFrame, "year chip", chipBox, problems);
            EconomyUiLayout.Check(laidOutFrame, "year readout", readoutBox, problems);
            Compare("year chip", chip, chipBox);
            if (!readoutBox.IsEmpty) Compare("year readout", readout, readoutBox);
            string text = string.Join("; ", problems);
            if (text == loggedProblems) return;
            loggedProblems = text;
            if (text.Length > 0) Debug.LogWarning("[Why] EconomyControls layout: " + text);
        }

        void Compare(string name, RectTransform rt, UiBox planned)
        {
            UiBox actual = hud.Box(canvasRect, rt);
            if (!actual.IsEmpty && !actual.Near(planned) && laidOutFrame.Canvas == canvasRect.rect.size)
            {
                problems.Add(name + " drawn at " + actual + ", laid out at " + planned);
            }
        }
    }
}
