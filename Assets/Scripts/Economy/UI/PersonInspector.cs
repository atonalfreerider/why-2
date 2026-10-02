using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.InputSystem;
using Why.Economy.Land;
using Why.Economy.Model;
using Why.Humans;
using Why.Humans.Smv;
using Why.UI;
using Debug = UnityEngine.Debug;

namespace Why.Economy.UI
{
    /// <summary>
    /// Look inside one lifeline. A left click on the road (released where it was pressed, not over the UI, not on a label,
    /// not on something of the land the land's picker took, not during the tour) picks the line nearest the cursor within
    /// about ten pixels (<see cref="LifelinePicker"/>) and selects that person (<see cref="EconomyState.Person"/>); a click
    /// on empty road clears the selection, like the HUD's own click-to-focus. In the overview and section views the click
    /// also sets the year to the one clicked (1.3). A click on a member's dot in the land (<see cref="LandPicker"/>) selects
    /// the person too. The selected person's lifeline glows, and with it their dot in the cut and in their player (they share
    /// the line's id), and is drawn once more over the bundle so it reads in its densest part (<see cref="PersonLine"/>);
    /// a panel shows their life at <see cref="EconomyState.Year"/>, or the nearest year of their life
    /// (<see cref="PersonPanel"/>), refreshed whenever the shared state changes, with a link to the player they play as
    /// (it opens the player inspector). The × button or Esc closes it.
    ///
    /// The person's and the player's inspectors share one place: the one opened last is in front (<see cref="PlayerPanel.Covers"/>).
    /// Landscape: a column at the right edge under the year chip, down to the first HUD block beneath it, scaled to fit and
    /// kept clear of the bowl. Portrait: on the road a compact full-width sheet under the HUD's title and the year's
    /// readout, scaled into two thirds of the room above the HUD's bottom blocks so a strip of the people stays in view
    /// beneath it (where the N key frames the person, see <see cref="EconomyUiLayout.PersonRegion"/>); with the land in view
    /// a bottom sheet under the bowl (<see cref="EconomyUiLayout.SheetBox"/>), which waits while the social panel's sheet is
    /// up. During the tour the director owns attention and the panel hides.
    /// </summary>
    [GraphScenes(GraphScene.Economy)]
    public sealed class PersonInspector : GraphModule
    {
        /// <summary>With the year controls above the HUD (40), below the social panel (45), the tour (50), the overlay (200).</summary>
        const int SortingOrder = 44;

        /// <summary>A left click that moves further than this (1080p pixels) is a drag, not a click (as the HUD's).</summary>
        const float ClickSlopPx = 6f;

        /// <summary>A click picks the nearest line within this many 1080p pixels.</summary>
        const float PickRadiusPx = 10f;

        /// <summary>Everything but the person's line dims this much while they are selected (the HUD's focus dims 0.6).</summary>
        const float PersonDim = 0.65f;

        const float FadeSpeed = 5f;

        /// <summary>A pick slower than this (milliseconds) is logged: the budget is a frame or two.</summary>
        const long SlowPickMs = 40;

        /// <summary>No person's line lit by this module yet (unlike -1, "nobody", it never equals a selection).</summary>
        const int NoneHighlighted = int.MinValue;

        /// <summary>
        /// The panel's canvas box while it is shown (empty otherwise), for the N key's framing of the next person (see
        /// <see cref="EconomyUiLayout.PersonRegion"/>). Canvas units of any canvas made by <see cref="UiFactory.CreateCanvas"/>.
        /// </summary>
        public static UiBox Shown { get; private set; } = UiBox.Empty;

        /// <summary>True while the panel is a portrait bottom sheet on screen (the land legend steps aside).</summary>
        public static bool SheetShown { get; private set; }

        /// <summary>Set by <see cref="Relight"/>: light the person's line again (the land's picker released the highlight).</summary>
        static bool relight;

        GraphRoot root;
        RectTransform canvasRect;
        PersonPanel panel;
        PersonLine line;
        UiFade fade;
        HudBlocks hud;
        RectTransform helpSheet;
        EconomyModel model;
        SmvPopulation pop;
        HumanWorld world;
        readonly LifelinePicker picker = new LifelinePicker();
        readonly IdRange[] highlightRanges = new IdRange[1];
        readonly List<string> problems = new List<string>();

        bool loaded, tourWasActive, ownsHighlight, pressValid, pickPending, contentDirty, checkPending, laidOutCompact, laidOutSheet;
        Vector2 pressPosition, pickPosition;
        int seenVersion = -1, factsPerson = -2, factsYear = int.MinValue, factsLand = -1, highlighted = -1;
        PersonFacts facts;
        HudFrame laidOutFrame;
        UiBox laidOutAbove, panelBox;

        /// <summary>The panel's width on this screen, its content's height laid out at that width, the width the content
        /// is laid out at now (wider than the sheet by 1 / scale in portrait), its height there, and the scale.</summary>
        float layoutWidth, contentHeight, panelScale = 1;

        /// <summary>The panel's height at a layout width, full or compact (the layout's measure; made once).</summary>
        System.Func<float, bool, float> heightAt;

        /// <summary>The compact (sheet) panel's height at a layout width.</summary>
        System.Func<float, float> sheetHeightAt;
        string loggedProblems = "";

        public override void Init(GraphRoot graphRoot)
        {
            root = graphRoot;
            heightAt = (w, compact) => panel.Layout(w, compact);
            sheetHeightAt = w => panel.Layout(w, true);
            Canvas canvas = UiFactory.CreateCanvas("PersonInspector", SortingOrder, transform);
            canvasRect = (RectTransform)canvas.transform;
            panel = new PersonPanel(canvasRect, Close, OpenPlayer);
            line = new PersonLine(transform);

            // created last: a hidden fade deactivates the panel (texts are measured while it is shown)
            fade = new UiFade(panel.Rect.gameObject, 0, FadeSpeed, true);
        }

        public override void OnLoaded(GraphRoot graphRoot)
        {
            model = root.Context.Shared<EconomyModel>(EconomyModel.SharedKey);
            pop = root.Context.Shared<SmvPopulation>(SmvPopulation.SharedKey);
            world = root.Context.Shared<HumanWorld>(HumanWorld.SharedKey);
            hud = new HudBlocks(root);
            foreach (GraphModule m in root.Modules)
            {
                if (m is Hud) helpSheet = m.transform.Find("HudOverlay/Help") as RectTransform;
            }

            loaded = model?.Lives != null && pop != null;
        }

        void OnDestroy()
        {
            Shown = UiBox.Empty;
            SheetShown = false;
            panel?.Dispose();
            line?.Dispose();
        }

        /// <summary>The × button and Esc: nobody is selected any more.</summary>
        static void Close()
        {
            HudKit.ReleaseSelection();
            EconomyState.SetPerson(-1);
        }

        /// <summary>The "Plays as" link: the person's player is selected and its inspector comes to the front.</summary>
        void OpenPlayer()
        {
            HudKit.ReleaseSelection();
            if (facts == null || facts.PlayerIndex < 0) return;
            EconomyState.SetSelection(facts.PlayerIndex, -1, EconomyState.SelectedTie);
            PlayerPanel.BringToFront();
        }

        // ------------------------------------------------------------------ per frame

        void Update()
        {
            if (!loaded) return;
            bool tour = root.TourActive;
            // the Esc that ends the tour this frame must not also close the person the viewer had open before it
            bool keysBlocked = tour || tourWasActive;
            if (tour != tourWasActive)
            {
                tourWasActive = tour;
                // the director owns attention now; never clear its highlight from here
                if (tour) ownsHighlight = false;
            }

            HandleKeys(keysBlocked);
            TrackClicks(tour);
            PlayerPanel.TrackFront();
            if (EconomyState.Version != seenVersion || LandService.Version != factsLand) Refresh();
            SyncHighlight(tour ? -1 : EconomyState.Person);

            // one inspector at a time: the player's when it is in front; on a portrait screen with the land in view the
            // social panel's sheet holds the bottom (the person waits until it has gone)
            bool socialSheet = ScreenLayout.IsPortrait && HudBlocks.SocialPanelShown && EconomyControls.IsLandView(root.CurrentPreset);
            bool show = !tour && facts != null && !PlayerPanel.Covers && !socialSheet;
            if (show != fade.Shown)
            {
                fade.Show(show); // activates the panel before its texts are measured
                if (show) contentDirty = true;
            }
        }

        void HandleKeys(bool tour)
        {
            Keyboard kb = Keyboard.current;
            if (kb == null || tour || HudKit.TypingInField()) return;

            // Esc with the help sheet open only closes the sheet (the HUD's)
            bool helpOpen = helpSheet != null && helpSheet.gameObject.activeInHierarchy;
            if (kb.escapeKey.wasPressedThisFrame && !helpOpen && EconomyState.Person >= 0 && !PlayerPanel.Covers && PlayerPanel.TakeEsc())
            {
                EconomyState.SetPerson(-1);
            }
        }

        /// <summary>
        /// A left click that is not a drag, not over the UI (the help sheet's backdrop counts), not on a label and not during
        /// the tour is picked in LateUpdate: after the HUD has handled the same click (it clears its own focus on empty
        /// space) and after the land's picker (a click it took on the land is not the road's), so the person's highlight is
        /// set last.
        /// </summary>
        void TrackClicks(bool tour)
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;
            Vector2 position = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame)
            {
                Keyboard kb = Keyboard.current;
                bool shift = kb != null && kb.shiftKey.isPressed; // shift + left drag pans
                pressValid = !shift && !tour && !HudKit.PointerOverUi();
                pressPosition = position;
            }

            if (!mouse.leftButton.wasReleasedThisFrame || !pressValid) return;
            pressValid = false;
            float slop = ClickSlopPx * LabelSystem.UiScale;
            if ((position - pressPosition).sqrMagnitude > slop * slop || root.Labels.Hovered != null) return;
            pickPending = true;
            pickPosition = position;
        }

        /// <summary>
        /// Picks the line under a click on the road. While only the zoomed-out tier of lines is drawn (one line in ten, see
        /// SmvLayer), only its lines can be picked. In the overview and section views a line's click also opens its year
        /// (1.3). A click the land's picker took (a player, a dot, a sector ...) is not the road's.
        /// </summary>
        void Pick(Vector2 position)
        {
            if (root.Rig == null || root.Rig.Cam == null || !model.Lives.Ready) return;
            if (LandPicker.TookClick) return;
            double perLine = HumansLod.PeoplePerLine;
            bool coarseOnly = !(perLine > 0 && perLine < SmvLayer.PeoplePerCoarseLine * 0.5);
            Stopwatch sw = Stopwatch.StartNew();
            bool hit = picker.Pick(pop, root.Rig.Cam, GraphWarp.Current, position, PickRadiusPx * LabelSystem.UiScale,
                coarseOnly, EconomyState.Person, out LifelinePicker.Hit found);
            if (sw.ElapsedMilliseconds > SlowPickMs)
            {
                Debug.Log($"[Why] PersonInspector: pick took {sw.ElapsedMilliseconds} ms ({picker.LastProjections} projections)");
            }

            // a click on the selected line lights it again: a label's focus may have taken the glow meanwhile (and the
            // HUD clears that focus on this same click), while the selection itself does not change
            if (hit && found.Person == highlighted) highlighted = NoneHighlighted;
            if (hit && LandPicker.YearClicks(root.CurrentPreset))
            {
                EconomyState.SetYear((int)System.Math.Floor(pop.Sim.TimeOf(found.Step)));
            }

            // on the land the road is the background: an empty click there is the land picker's to clear
            if (!hit && EconomyControls.IsLandView(root.CurrentPreset)) return;
            EconomyState.SetPerson(hit ? found.Person : -1);
        }

        /// <summary>New facts when the person, the year or the land on screen changed (the panel is filled when it is next
        /// laid out); the link to the person's player when the land shows the person's year.</summary>
        void Refresh()
        {
            seenVersion = EconomyState.Version;
            int person = EconomyState.Person, year = EconomyState.Year;
            if (person == factsPerson && year == factsYear && factsLand == LandService.Version) return;
            factsPerson = person;
            factsYear = year;
            factsLand = LandService.Version;
            facts = person >= 0 ? PersonFacts.Build(model, pop, world, person, year) : null;
            LandSnapshot s = LandService.Current;
            if (facts != null && s != null && s.Year == facts.Year)
            {
                facts.PlaysAs = PlayerFacts.PlaysAs(s, Layers.SocialLayer.Shown, LandView.Round, person, model.Data, out facts.PlayerIndex);
            }

            contentDirty = true;
        }

        /// <summary>
        /// Lights a person's lifeline (or none, -1): the highlighter's glow on their line (dimming the rest) and the line
        /// drawn once more over the bundle (<see cref="PersonLine"/>). Replaces only a highlight this module set and that
        /// is still showing; the HUD's and the director's are theirs.
        /// </summary>
        void SyncHighlight(int person)
        {
            if (relight)
            {
                relight = false;
                if (!LandPicker.OwnsHighlight) highlighted = NoneHighlighted;
            }

            if (person == highlighted) return;
            if (ownsHighlight && Highlighter.HasHighlight && Highlighter.IsHighlighted(highlightRanges[0])) Highlighter.Clear();
            ownsHighlight = false;
            highlighted = person;
            line.Show(pop, model.Lives, person);
            if (person < 0 || person >= pop.Sim.People.Count) return;

            // while the land's picker lights the land it lights the person's line with it (one highlight at a time)
            if (LandPicker.OwnsHighlight) return;
            highlightRanges[0] = IdRange.Single(pop.Id(pop.Sim.People[person]));
            Highlighter.Set(highlightRanges, GraphStyle.HighlightGlow, PersonDim);
            ownsHighlight = true;
        }

        /// <summary>
        /// The land's picker gave the highlight back: the selected person's line lights again on the next frame (their dot
        /// in the cut and in their player with it).
        /// </summary>
        public static void Relight() => relight = true;

        void LateUpdate()
        {
            fade.Tick(Time.unscaledDeltaTime);
            line.Tick(Time.unscaledDeltaTime);
            if (!loaded) return;
            if (pickPending)
            {
                pickPending = false;
                Pick(pickPosition);
                if (EconomyState.Version != seenVersion) Refresh();
            }

            if (!fade.Shown || facts == null)
            {
                Shown = UiBox.Empty;
                SheetShown = false;
                return;
            }

            if (checkPending) SelfCheck();
            Layout();
            Shown = panelBox;
            SheetShown = laidOutSheet;
        }

        // ------------------------------------------------------------------ layout

        /// <summary>
        /// Fills the panel when the facts changed, then places it whenever the HUD's blocks, the bowl or the controls moved:
        /// in landscape where <see cref="EconomyUiLayout.ColumnPlace"/> finds room clear of the bowl (the right column under
        /// the year controls, the left one, compact, a band under the controls); on a portrait screen with the land in view
        /// as a bottom sheet under the bowl (<see cref="EconomyUiLayout.SheetBox"/>); on a portrait screen with the road in
        /// view as the top sheet. A scaled portrait sheet lays its content out wider by 1 / scale, so it still spans the
        /// screen (and wraps less).
        /// </summary>
        void Layout()
        {
            HudFrame f = hud.Measure(canvasRect, UiFactory.CanvasSize);
            UiBox above = EconomyControls.Occupied;
            bool sheet = EconomyUiLayout.BottomSheets(f);
            if (!contentDirty && f.Portrait == laidOutCompact && sheet == laidOutSheet && f.Near(laidOutFrame) && above.Near(laidOutAbove)) return;
            if (contentDirty) panel.Fill(facts, pop.Sim.PeoplePerLine);
            contentDirty = false;
            laidOutCompact = f.Portrait;
            laidOutSheet = sheet;
            laidOutFrame = f;
            laidOutAbove = above;
            if (f.Portrait)
            {
                // a sheet (the land in view) or the top sheet (the road): scaled to its room, laid out wider by 1 / scale
                if (sheet)
                {
                    // under the bowl, or above it where the bowl reaches low
                    EconomyUiLayout.Placement p = EconomyUiLayout.SheetPlace(f, above, sheetHeightAt);
                    panelScale = p.Scale;
                    layoutWidth = p.LayoutWidth;
                    contentHeight = panel.Layout(layoutWidth, true);
                    panelBox = p.Box;
                }
                else
                {
                    float width = EconomyUiLayout.InspectorWidth(f);
                    panelScale = EconomyUiLayout.InspectorScale(f, above, panel.Layout(width, true));
                    layoutWidth = width / panelScale;
                    contentHeight = panel.Layout(layoutWidth, true);
                    panelBox = EconomyUiLayout.InspectorBox(f, above, layoutWidth, contentHeight, panelScale);
                }
            }
            else
            {
                // the right column, or the left one, compact, or a band where the bowl takes the column (laid out last as chosen)
                EconomyUiLayout.Placement p = EconomyUiLayout.ColumnPlace(f, above, EconomyUiLayout.LandscapeInspectorWidth, heightAt);
                layoutWidth = p.LayoutWidth;
                panelScale = p.Scale;
                contentHeight = panel.Layout(layoutWidth, p.Compact);
                panelBox = p.Box;
            }

            // anchored by its top-right corner (scaled toward it); the portrait sheets span the width
            RectTransform rt = panel.Rect;
            rt.Place(Vector2.one, Vector2.one, new Vector2(-(f.Canvas.x - panelBox.Right), -panelBox.Y),
                new Vector2(layoutWidth, contentHeight));
            rt.localScale = new Vector3(panelScale, panelScale, 1);
            checkPending = true;
        }

        /// <summary>
        /// The runtime self-check, a frame after a layout: the panel must keep clear of the HUD's blocks and of the year
        /// controls, and be drawn where the layout put it (anchors, pivot and scale as intended). Logs once per distinct
        /// problem.
        /// </summary>
        void SelfCheck()
        {
            checkPending = false;
            problems.Clear();
            EconomyUiLayout.Check(laidOutFrame, "person inspector", panelBox, problems, laidOutSheet);
            if (panelBox.Overlaps(laidOutAbove)) problems.Add("person inspector " + panelBox + " overlaps the year controls " + laidOutAbove);
            UiBox drawn = hud.Box(canvasRect, panel.Rect);
            if (!drawn.IsEmpty && !drawn.Near(panelBox) && laidOutFrame.Canvas == canvasRect.rect.size)
            {
                problems.Add("person inspector drawn at " + drawn + ", laid out at " + panelBox);
            }

            float room = laidOutSheet
                ? EconomyUiLayout.SheetBottom(laidOutFrame) - EconomyUiLayout.SheetTopLimit(laidOutFrame, laidOutAbove)
                : EconomyUiLayout.ColumnLimit(laidOutFrame, laidOutAbove, panelBox) - panelBox.Y;
            if (panelBox.H > room + 0.5f)
            {
                problems.Add("person inspector " + panelBox + " runs past its room of " +
                             room.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " units");
            }
            string text = string.Join("; ", problems);
            if (text == loggedProblems) return;
            loggedProblems = text;
            if (text.Length > 0) Debug.LogWarning("[Why] PersonInspector layout: " + text);
        }
    }
}
