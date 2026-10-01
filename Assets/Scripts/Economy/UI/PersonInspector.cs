using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.InputSystem;
using Why.Economy.Model;
using Why.Humans;
using Why.Humans.Smv;
using Why.UI;
using Debug = UnityEngine.Debug;

namespace Why.Economy.UI
{
    /// <summary>
    /// Look inside one lifeline. A left click on the road (released where it was pressed, not over the UI, not on a label,
    /// not during the tour) picks the line nearest the cursor within about ten pixels (<see cref="LifelinePicker"/>) and
    /// selects that person (<see cref="EconomyState.Person"/>); a click on empty road clears the selection, like the HUD's
    /// own click-to-focus. The selected person's lifeline glows (their cross in the mind map too: it shares the line's id)
    /// and is drawn once more over the bundle so it reads in its densest part (<see cref="PersonLine"/>), and a panel
    /// shows their life at <see cref="EconomyState.Year"/>, or the nearest year of their life
    /// (<see cref="PersonPanel"/>), refreshed whenever the shared state changes. The × button or Esc closes it.
    ///
    /// Landscape: a column at the right edge under the year chip, down to the first HUD block beneath it, scaled to fit.
    /// Portrait: a compact full-width sheet under the HUD's title and the year's readout, scaled into two thirds of the
    /// room above the HUD's bottom blocks so a strip of the people stays in view beneath it (where the N key frames the
    /// person, see <see cref="EconomyUiLayout.PersonRegion"/>). The panel and the glow belong to
    /// the road and the mind map: at the other stations (the money circuit and the games) they step aside, and they come
    /// back with the camera. The panel also waits while the games panel is up (it docks at the same edge, in the games and
    /// tribes views). During the tour the director owns attention and the panel hides.
    /// </summary>
    [GraphScenes(GraphScene.Economy)]
    public sealed class PersonInspector : GraphModule
    {
        /// <summary>With the year controls above the HUD (40), below the games panel (45), the tour (50), the overlay (200).</summary>
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
        Station circuitStation, mindStation;
        readonly LifelinePicker picker = new LifelinePicker();
        readonly IdRange[] highlightRanges = new IdRange[1];
        readonly List<string> problems = new List<string>();

        bool loaded, tourWasActive, ownsHighlight, pressValid, pickPending, contentDirty, checkPending, laidOutCompact;
        Vector2 pressPosition, pickPosition;
        int seenVersion = -1, factsPerson = -2, factsYear = int.MinValue, highlighted = -1;
        PersonFacts facts;
        HudFrame laidOutFrame;
        UiBox laidOutAbove, panelBox;

        /// <summary>The panel's width on this screen, its content's height laid out at that width, the width the content
        /// is laid out at now (wider than the sheet by 1 / scale in portrait), its height there, and the scale.</summary>
        float baseWidth, baseHeight, layoutWidth, contentHeight, panelScale = 1;
        string loggedProblems = "";

        public override void Init(GraphRoot graphRoot)
        {
            root = graphRoot;
            Canvas canvas = UiFactory.CreateCanvas("PersonInspector", SortingOrder, transform);
            canvasRect = (RectTransform)canvas.transform;
            panel = new PersonPanel(canvasRect, Close);
            line = new PersonLine(transform);

            // created last: a hidden fade deactivates the panel (texts are measured while it is shown)
            fade = new UiFade(panel.Rect.gameObject, 0, FadeSpeed, true);
        }

        public override void OnLoaded(GraphRoot graphRoot)
        {
            model = root.Context.Shared<EconomyModel>(EconomyModel.SharedKey);
            pop = root.Context.Shared<SmvPopulation>(SmvPopulation.SharedKey);
            world = root.Context.Shared<HumanWorld>(HumanWorld.SharedKey);
            circuitStation = EconomyStage.Get(EconomyStage.Circuit);
            mindStation = EconomyStage.Get(EconomyStage.Mind);
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
            panel?.Dispose();
            line?.Dispose();
        }

        /// <summary>The × button and Esc: nobody is selected any more.</summary>
        static void Close()
        {
            HudKit.ReleaseSelection();
            EconomyState.SetPerson(-1);
        }

        // ------------------------------------------------------------------ per frame

        void Update()
        {
            if (!loaded) return;
            bool tour = root.TourActive;
            if (tour != tourWasActive)
            {
                tourWasActive = tour;
                // the director owns attention now; never clear its highlight from here
                if (tour) ownsHighlight = false;
            }

            HandleKeys(tour);
            TrackClicks(tour);
            if (EconomyState.Version != seenVersion) Refresh();

            // the person belongs to the road and the mind map (where their cross is drawn)
            Vector3 target = root.Rig != null ? root.Rig.Pose.Target : Vector3.zero;
            bool here = !EconomyControls.AtStations(target, circuitStation) || EconomyControls.NearMind(target, mindStation);
            SyncHighlight(tour ? -1 : here ? EconomyState.Person : -1);

            // the games panel docks at the right edge too (in the games and tribes views, wherever the camera has
            // since wandered): the person waits until it has gone
            bool show = !tour && here && facts != null && !hud.GamesPanelShown;
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
            if (kb.escapeKey.wasPressedThisFrame && !helpOpen && EconomyState.Person >= 0) EconomyState.SetPerson(-1);
        }

        /// <summary>
        /// A left click that is not a drag, not over the UI (the help sheet's backdrop counts), not on a label and not during
        /// the tour is picked in LateUpdate: after the HUD has handled the same click (it clears its own focus on empty
        /// space), so the person's highlight is set last.
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
        /// Picks the line under a click on the road. At the stations nothing is picked (the road stands behind their
        /// diagrams). While only the zoomed-out tier of lines is drawn (one line in ten, see SmvLayer), only its lines can
        /// be picked.
        /// </summary>
        void Pick(Vector2 position)
        {
            if (root.Rig == null || root.Rig.Cam == null || !model.Lives.Ready) return;
            if (EconomyControls.AtStations(root.Rig.Pose.Target, circuitStation)) return;
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
            EconomyState.SetPerson(hit ? found.Person : -1);
        }

        /// <summary>New facts when the person or the year changed (the panel is filled when it is next laid out).</summary>
        void Refresh()
        {
            seenVersion = EconomyState.Version;
            int person = EconomyState.Person, year = EconomyState.Year;
            if (person == factsPerson && year == factsYear) return;
            factsPerson = person;
            factsYear = year;
            facts = person >= 0 ? PersonFacts.Build(model, pop, world, person, year) : null;
            contentDirty = true;
        }

        /// <summary>
        /// Lights a person's lifeline (or none, -1): the highlighter's glow on their line (dimming the rest) and the line
        /// drawn once more over the bundle (<see cref="PersonLine"/>). Replaces only a highlight this module set and that
        /// is still showing; the HUD's and the director's are theirs.
        /// </summary>
        void SyncHighlight(int person)
        {
            if (person == highlighted) return;
            if (ownsHighlight && Highlighter.HasHighlight && Highlighter.IsHighlighted(highlightRanges[0])) Highlighter.Clear();
            ownsHighlight = false;
            highlighted = person;
            line.Show(pop, model.Lives, person);
            if (person < 0 || person >= pop.Sim.People.Count) return;
            highlightRanges[0] = IdRange.Single(pop.Id(pop.Sim.People[person]));
            Highlighter.Set(highlightRanges, GraphStyle.HighlightGlow, PersonDim);
            ownsHighlight = true;
        }

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
                return;
            }

            if (checkPending) SelfCheck();
            Layout();
            Shown = panelBox;
        }

        // ------------------------------------------------------------------ layout

        /// <summary>
        /// Fills and sizes the panel for the screen's shape when the facts changed, then scales and places it under the
        /// year controls whenever the HUD's blocks or the controls moved (see <see cref="EconomyUiLayout.InspectorScale"/>).
        /// A scaled portrait sheet lays its content out wider by 1 / scale, so it still spans the screen (and wraps less).
        /// </summary>
        void Layout()
        {
            HudFrame f = hud.Measure(canvasRect, UiFactory.CanvasSize);
            UiBox above = EconomyControls.Occupied;
            float width = EconomyUiLayout.InspectorWidth(f);
            bool refill = contentDirty || f.Portrait != laidOutCompact || Mathf.Abs(width - baseWidth) > 0.5f;
            if (!refill && f.Near(laidOutFrame) && above.Near(laidOutAbove)) return;

            if (refill)
            {
                if (contentDirty) panel.Fill(facts, pop.Sim.PeoplePerLine);
                contentDirty = false;
                laidOutCompact = f.Portrait;
                baseWidth = layoutWidth = width;
                baseHeight = contentHeight = panel.Layout(width, f.Portrait);
            }

            laidOutFrame = f;
            laidOutAbove = above;
            panelScale = EconomyUiLayout.InspectorScale(f, above, baseHeight);
            float wanted = f.Portrait ? baseWidth / panelScale : baseWidth;
            if (Mathf.Abs(wanted - layoutWidth) > 0.5f)
            {
                layoutWidth = wanted;
                contentHeight = panel.Layout(layoutWidth, f.Portrait);
            }

            panelBox = EconomyUiLayout.InspectorBox(f, above, layoutWidth, contentHeight, panelScale);

            // anchored by its top-right corner at the right margin (scaled toward it); the portrait sheet spans the width
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
            EconomyUiLayout.Check(laidOutFrame, "person inspector", panelBox, problems);
            if (panelBox.Overlaps(laidOutAbove)) problems.Add("person inspector " + panelBox + " overlaps the year controls " + laidOutAbove);
            UiBox drawn = hud.Box(canvasRect, panel.Rect);
            if (!drawn.IsEmpty && !drawn.Near(panelBox) && laidOutFrame.Canvas == canvasRect.rect.size)
            {
                problems.Add("person inspector drawn at " + drawn + ", laid out at " + panelBox);
            }

            float room = EconomyUiLayout.InspectorLimit(laidOutFrame, laidOutAbove) - panelBox.Y;
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
