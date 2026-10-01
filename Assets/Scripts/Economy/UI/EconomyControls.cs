using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Why.Economy.Data;
using Why.Economy.Model;
using Why.Humans.Smv;
using Why.UI;

namespace Why.Economy.UI
{
    /// <summary>
    /// The economy scene's year and its people, from the keyboard and a small chip:
    /// <list type="bullet">
    /// <item>comma and period step <see cref="EconomyState.Year"/> back and forward by one year (with Shift by ten): the
    /// year of the money circuit, the mind map and the person inspector;</item>
    /// <item>N meets the next notable person of the model (owner, heir, striver, escapist, the indebted ...): it selects
    /// them (<see cref="EconomyState.Person"/>; the inspector shows them and lights their lifeline) and flies the camera to
    /// their line at the year, unless the mind map is the view (their cross lights up there). From the other stations the
    /// view turns to the people first, where every line is drawn in detail. The point lands in the room the inspector
    /// leaves (left of its column; in portrait the strip under its sheet), moved toward the future so the life that led
    /// to the year shows (<see cref="PersonFraming"/>);</item>
    /// <item>the YEAR CHIP ("2025  ‹ ›", the arrows click) at the right edge under the HUD's tour and help buttons, and
    /// beneath it the year's readout: the share of adults in control of their path, the fantasy and fear shares of the
    /// money spent, and trust in others (General Social Survey).</item>
    /// </list>
    /// Everything steps aside during the guided tour, like the HUD's own controls. On a portrait screen the chip stands in
    /// the column beside the HUD's title and the readout under the title (see <see cref="EconomyUiLayout"/>).
    /// </summary>
    [GraphScenes(GraphScene.Economy)]
    public sealed class EconomyControls : GraphModule
    {
        /// <summary>Above the HUD (40), with the person inspector, below the games panel (45), the director's tour panel
        /// (50) and the HUD's overlay (200).</summary>
        const int SortingOrder = 44;

        /// <summary>Chip height, arrow buttons, the year's font size, and the readout's height (reference pixels).</summary>
        const float ChipHeight = 34f, ArrowSize = 24f, ArrowGap = 3f, YearSize = HudKit.SizeTitle, ReadoutHeight = 26f;

        /// <summary>Years a step takes with Shift held.</summary>
        const int ShiftStep = 10;

        /// <summary>Seconds of the flight to a person's line.</summary>
        const float FlySeconds = 1.6f;

        /// <summary>
        /// Camera distance to a person's line (world units): about thirty years of the road across a landscape screen and
        /// the whole height of the population, so the lit line reads among its neighbors. Portrait steps back like the
        /// narrow views do (ViewPreset.NarrowPullback).
        /// </summary>
        const float PersonDistance = 4f, PortraitPullback = 1.25f;

        const float FadeSpeed = 4f;

        /// <summary>The road's view with every line drawn in detail; the station whose diagram shows the person (their
        /// cross in the mind map).</summary>
        const string PeopleView = "people", MindView = "mind";

        /// <summary>
        /// The camera looks at the stations once its target is less than this far before the first station along the road
        /// (world units): half the gap between the road's end and the money circuit, as the circuit's own test.
        /// </summary>
        const float StationsAhead = EconomyStyle.FirstStationGap * 0.5f;

        /// <summary>Within this distance of the mind map's center (world units) the camera is at the mind station.</summary>
        const float MindRange = EconomyStyle.StationSpacing * 0.5f;

        /// <summary>
        /// The canvas box the chip and the readout take while shown (empty otherwise), for the person inspector that hangs
        /// beneath them. Canvas units of any canvas made by <see cref="UiFactory.CreateCanvas"/>.
        /// </summary>
        public static UiBox Occupied { get; private set; } = UiBox.Empty;

        GraphRoot root;
        RectTransform canvasRect, chip, readout;
        TextMeshProUGUI yearText, readoutText;
        Button prev, next;
        UiFade chipFade, readoutFade;
        HudBlocks hud;
        EconomyModel model;
        SmvPopulation pop;
        YearSeries trust;
        Station circuitStation, mindStation;
        bool loaded, dirty = true, checkPending;
        int seenVersion = -1, shownYear = int.MinValue;
        HudFrame laidOutFrame;
        Vector2 chipSize, readoutSize;

        /// <summary>The readout's text, made again only for another year or screen shape (the layout also runs while the
        /// HUD's blocks move).</summary>
        string readoutLine = "";

        int readoutYear = int.MinValue;
        bool readoutCompact;
        UiBox chipBox, readoutBox;
        string loggedProblems = "";
        readonly List<string> problems = new List<string>();

        public override void Init(GraphRoot graphRoot)
        {
            root = graphRoot;
            Canvas canvas = UiFactory.CreateCanvas("EconomyControls", SortingOrder, transform);
            canvasRect = (RectTransform)canvas.transform;

            chip = HudKit.FramedPanel(canvasRect, "YearChip", 0.86f, true);
            yearText = HudKit.Line(chip, "Year", "2025", YearSize, GraphStyle.Text, FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft);
            prev = HudKit.Button(chip, "Prev", "\u2039", HudKit.SizeBody + 2, () => Step(-1));
            next = HudKit.Button(chip, "Next", "\u203A", HudKit.SizeBody + 2, () => Step(1));

            readout = HudKit.FramedPanel(canvasRect, "YearReadout", 0.62f);
            readoutText = HudKit.Line(readout, "Text", "", HudKit.SizeSmall, GraphStyle.TextDim, FontStyles.Normal,
                TextAlignmentOptions.MidlineLeft);

            // created last: a hidden fade deactivates its panel
            chipFade = new UiFade(chip.gameObject, 0, FadeSpeed, true);
            readoutFade = new UiFade(readout.gameObject, 0, FadeSpeed, false);
        }

        public override void OnLoaded(GraphRoot graphRoot)
        {
            model = root.Context.Shared<EconomyModel>(EconomyModel.SharedKey);
            pop = root.Context.Shared<SmvPopulation>(SmvPopulation.SharedKey);
            trust = model?.Data?.Games?.Tribes?.TrustSeries();
            circuitStation = EconomyStage.Get(EconomyStage.Circuit);
            mindStation = EconomyStage.Get(EconomyStage.Mind);
            hud = new HudBlocks(root);
            loaded = true;
        }

        void OnDestroy() => Occupied = UiBox.Empty;

        // ------------------------------------------------------------------ input

        void Update()
        {
            if (!loaded) return;
            bool tour = root.TourActive;
            bool show = !tour && model != null;
            if (show != chipFade.Shown) Show(show);
            if (!tour) HandleKeys();
            if (EconomyState.Version != seenVersion) Sync();
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
            if (kb.nKey.wasPressedThisFrame) NextNotable();
        }

        /// <summary>The chip's arrows: a year back or forward.</summary>
        static void Step(int direction) => EconomyState.SetYear(EconomyState.Year + direction);

        /// <summary>Selects the notable person after the one shown (the first when nobody notable is), and flies to them.</summary>
        void NextNotable()
        {
            EconomicLives lives = model?.Lives;
            if (lives == null || !lives.Ready || pop == null) return;
            IReadOnlyList<Notable> notables = lives.Notables;
            if (notables.Count == 0) return;
            int current = -1;
            for (int i = 0; i < notables.Count; i++)
            {
                if (notables[i].Person == EconomyState.Person) current = i;
            }

            int person = notables[(current + 1) % notables.Count].Person;
            EconomyState.SetPerson(person);
            FlyToPerson(person);
        }

        /// <summary>
        /// Flies the camera to a person's line at the shown year (the nearest year of their life), keeping the view's
        /// direction on the road; from a station other than the mind map the view turns to the people first. In the mind
        /// map the camera stays: the person's cross lights up there.
        /// </summary>
        void FlyToPerson(int person)
        {
            if (root.Rig == null) return;
            CameraPose current = root.Rig.Pose;
            bool atStations = AtStations(current.Target, circuitStation);
            bool toMind = root.CurrentPreset != null && root.CurrentPreset.Id == MindView && root.Rig.Flying;
            if (toMind || atStations && NearMind(current.Target, mindStation)) return;

            CameraPose pose = current;
            if (atStations)
            {
                // the people's view sets the lens and the direction; the flight below replaces its camera move
                ViewPreset people = ViewPresets.Get(PeopleView);
                if (people == null) return;
                root.Focus(people);
                pose = people.Pose();
            }

            // where the line will be once any running re-scale (e.g. the portrait people view's lens) has finished
            WarpState warp = GraphWarp.Target;
            if (!LifelinePicker.WorldAt(pop, person, EconomyState.Year, warp, out Vector3 world)) return;
            float distance = PersonDistance * (ScreenLayout.IsPortrait ? PortraitPullback : 1f);
            pose.Target = world;
            pose.Distance = Mathf.Min(pose.Distance, distance);

            // the point in the room the inspector leaves (left of its column; under the portrait sheet), moved toward
            // the future: more of the life that led to the year shows, less of the void past the present
            HudFrame f = hud.Measure(canvasRect, UiFactory.CanvasSize);
            Vector2 past = PersonFraming.PastDirection(pop, person, EconomyState.Year, warp, pose);
            Vector2 focus = EconomyUiLayout.PersonFocus(f, Occupied, PersonInspector.Shown, past);
            root.Rig.FlyTo(PersonFraming.Frame(pose, PersonFraming.ToScreenFraction(focus, f.Canvas), ScreenLayout.Aspect),
                FlySeconds);
        }

        /// <summary>True while the camera looks at the stations beyond the road's end rather than at the road.</summary>
        public static bool AtStations(Vector3 target, Station circuit) =>
            Vector3.Dot(target - circuit.Origin, circuit.Forward) > -StationsAhead;

        /// <summary>True while the camera's target is at the mind map (where people are drawn as crosses).</summary>
        public static bool NearMind(Vector3 target, Station mind) =>
            Vector3.Distance(target, mind.World(0, EconomyStyle.StationHeight * 0.5f, 0)) < MindRange;

        // ------------------------------------------------------------------ the chip

        /// <summary>Shows the shared state's year and its readout.</summary>
        void Sync()
        {
            seenVersion = EconomyState.Version;
            int year = EconomyState.Year;
            prev.interactable = year > EconomyState.MinYear;
            next.interactable = year < EconomyState.MaxYear;
            if (year == shownYear) return;
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
                return;
            }

            if (checkPending) SelfCheck();

            // the size the canvas has on this screen (on the frame the screen changes shape, the canvas itself may still
            // have its old size); the HUD's blocks as they are now (they move with the orientation, the F3 stats, ...)
            HudFrame frame = hud.Measure(canvasRect, UiFactory.CanvasSize);
            if (!dirty && frame.Near(laidOutFrame)) return;
            dirty = false;
            laidOutFrame = frame;
            Layout(frame);
        }

        /// <summary>Sizes the chip and the readout to their texts and places them (see <see cref="EconomyUiLayout"/>).</summary>
        void Layout(HudFrame f)
        {
            // the chip: the year, then the two arrows
            yearText.text = YearFacts.YearText(EconomyState.Year);
            Vector2 year = HudKit.FitText(yearText);
            float pad = HudKit.Pad - 2;
            chipSize = new Vector2(Mathf.Ceil(pad + year.x + 10 + 2 * ArrowSize + ArrowGap + pad - 4), ChipHeight);
            HudKit.PlaceTopLeft(yearText.rectTransform, pad, 0, new Vector2(year.x, ChipHeight));
            float arrowTop = (ChipHeight - ArrowSize) * 0.5f;
            HudKit.PlaceTopLeft((RectTransform)prev.transform, chipSize.x - pad + 4 - 2 * ArrowSize - ArrowGap, arrowTop,
                new Vector2(ArrowSize, ArrowSize));
            HudKit.PlaceTopLeft((RectTransform)next.transform, chipSize.x - pad + 4 - ArrowSize, arrowTop,
                new Vector2(ArrowSize, ArrowSize));
            chipBox = EconomyUiLayout.Chip(f, chipSize);
            Place(chip, chipBox, f);

            // the readout: one line, shorter on a portrait screen; measured while active (a hidden fade deactivates it,
            // and an inactive text may not measure), hidden again below when it has no place on this screen
            if (readoutYear != EconomyState.Year || readoutCompact != f.Portrait)
            {
                readoutYear = EconomyState.Year;
                readoutCompact = f.Portrait;
                readoutLine = YearFacts.Line(model?.Lives, trust, readoutYear, readoutCompact, UiFactory.Hex(GraphStyle.Text));
            }

            if (readoutText.text != readoutLine) readoutText.text = readoutLine;
            bool hasReadout = readoutLine.Length > 0;
            if (hasReadout) readoutFade.Show(true);
            Vector2 text = hasReadout ? HudKit.FitText(readoutText) : Vector2.zero;
            readoutSize = new Vector2(Mathf.Ceil(text.x + 2 * HudKit.Pad), ReadoutHeight);
            HudKit.PlaceTopLeft(readoutText.rectTransform, HudKit.Pad, 0, new Vector2(text.x, ReadoutHeight));
            readoutBox = hasReadout ? EconomyUiLayout.Readout(f, chipBox, readoutSize) : UiBox.Empty;
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
        /// keep clear of the HUD's blocks and sit where the layout put them (anchors and pivots as intended). Logs once per
        /// distinct problem.
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
