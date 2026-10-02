using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Why.Economy.Land;
using Why.Economy.Layers;
using Why.Economy.Model;
using Why.Humans.Smv;
using Why.UI;

namespace Why.Economy.UI
{
    /// <summary>
    /// Hover and click in the land (SPEC 7.5), with the pure picking of <see cref="LandPick"/>:
    /// <list type="bullet">
    /// <item><b>Hover</b> lights what is under the pointer (a player's glyph; a dot's lifeline, which glows on the road, in
    /// the cut and in its player; a tower; a tie and its two players; a river's lane; the crown and its capital flows; a
    /// sector with the wall's band, the cut's bar and its roots in and out) and shows its card: the anchor's generated
    /// blurb (sectors, pools, towers, the crown, rivers and falls, the canal, taxes, abroad, players) or a generated line
    /// (ties, dots);</item>
    /// <item><b>Click</b>: a player selects it (<see cref="EconomyState.SetSelection"/>; the player inspector opens), a dot
    /// selects its person (the person inspector opens), a tower selects it (the ownership fan) and pins its card, a tie
    /// selects it (the social panel's Betray acts on it) and pins its card; a sector, pool, river, the canal or the crown
    /// pins its card; clicking the pinned or selected thing again flies the camera closer (focus). An empty click clears
    /// the land's selections and the pinned card. In the overview and section views a click on the industry wall opens
    /// the year clicked (1.3; a lifeline's click does the same in the person inspector).</item>
    /// </list>
    /// The highlight it sets combines the hovered and pinned things with the selections (the player, the tower, the tie
    /// and the person's line), at most the highlighter's eight ranges; with nothing of the land to light it hands the
    /// highlight back to the person inspector. Selections follow a year change by the player's key (the tie is cleared,
    /// the tower too before 2024). Nothing happens during the tour, over the UI, or on a label (the HUD's).
    /// </summary>
    [GraphScenes(EconomyLayouts.BowlScene)]
    public sealed class LandPicker : GraphModule
    {
        /// <summary>Above the HUD (40) and the economy's panels (43-45): its card is a tooltip; below the tour (50).</summary>
        const int SortingOrder = 46;

        /// <summary>A left click that moves further than this (1080p pixels) is a drag, not a click (as the HUD's).</summary>
        const float ClickSlopPx = 6f;

        /// <summary>Everything but the lit things dims this much while hovering, and while something is selected.</summary>
        const float HoverDim = 0.35f, SelectDim = 0.5f;

        /// <summary>A focus click brings the camera this much closer, not nearer than <see cref="FocusMinDistance"/>.</summary>
        const float FocusZoom = 0.6f, FocusMinDistance = 3f, FocusSeconds = 1.2f;

        /// <summary>Frame on which this module took the click (the person inspector's road pick then stays out).</summary>
        static int tookFrame = -1;

        /// <summary>True while this module's highlight is on (the person inspector lights its line through it).</summary>
        public static bool OwnsHighlight { get; private set; }

        /// <summary>True on the frame a click was taken by the land (a thing of the land, or the wall's year).</summary>
        public static bool TookClick => tookFrame == Time.frameCount;

        /// <summary>The views where a click on the wall or a lifeline opens the year clicked (1.3).</summary>
        public static bool YearClicks(ViewPreset preset) => preset != null && (preset.Id == "overview" || preset.Id == "section");

        GraphRoot root;
        RectTransform canvasRect;
        HudTooltip card;
        EconomyModel model;
        SmvPopulation pop;
        WallGeometry wall;
        LandPickScene scene;
        CutPickScene cut;
        double[] rootDollars = Array.Empty<double>();

        /// <summary>Per industry: its roots in and out (RootsModel.RootsOf), made on first use per snapshot.</summary>
        List<int>[] rootsOf = Array.Empty<List<int>>();

        /// <summary>A root's dollars (to keep the largest lit when not all fit), made once.</summary>
        Func<int, double> rootDollar;
        int sceneVersion = -1, cutYear = int.MinValue, seenRig = -1;
        bool loaded, pressValid;
        Vector2 pressPosition, lastPointer = new Vector2(-1, -1);
        LandHit hover = LandHit.None, pinned = LandHit.None;
        Vector2 pinnedAt;
        string selectedKey;
        int selectionYear = -1, litSignature;
        IdRange litFirst = IdRange.Empty;
        readonly List<IdRange> ranges = new List<IdRange>(8), hoverRanges = new List<IdRange>(8), pinnedRanges = new List<IdRange>(8);

        /// <summary>The hits whose ranges <see cref="hoverRanges"/> and <see cref="pinnedRanges"/> hold, and the land they were made for.</summary>
        LandHit rangesHover = LandHit.None, rangesPinned = LandHit.None;

        int hoverRangesLand = -1, pinnedRangesLand = -1;

        /// <summary>The card's key (the tooltip compares keys by reference): a new one only when the hit or the land changes.</summary>
        object cardKey = new object();

        LandHit cardHit = LandHit.None;
        int cardLand = -1;

        public override void Init(GraphRoot graphRoot)
        {
            root = graphRoot;
            Canvas canvas = UiFactory.CreateCanvas("LandPicker", SortingOrder, transform);
            canvasRect = (RectTransform)canvas.transform;
            card = new HudTooltip(canvasRect);
            rootDollar = k => k >= 0 && k < rootDollars.Length ? rootDollars[k] : 0;
        }

        public override void OnLoaded(GraphRoot graphRoot)
        {
            model = root.Context.Shared<EconomyModel>(EconomyModel.SharedKey);
            pop = root.Context.Shared<SmvPopulation>(SmvPopulation.SharedKey);
            wall = root.Context.Shared<WallGeometry>(WallGeometry.SharedKey);
            loaded = model?.Data != null;
        }

        void OnDestroy()
        {
            Release(false);
            tookFrame = -1;
        }

        // ------------------------------------------------------------------ per frame

        void Update()
        {
            if (!loaded) return;
            if (root.TourActive)
            {
                // the director owns attention: forget hover and pins, never touch its highlight
                hover = pinned = LandHit.None;
                OwnsHighlight = false;
                litSignature = 0;
                card.Hide();
                return;
            }

            Follow();
            Mouse mouse = Mouse.current;
            Vector2 pointer = mouse != null ? mouse.position.ReadValue() : new Vector2(-1, -1);
            bool overUi = HudKit.PointerOverUi() || root.Labels.Hovered != null;

            // hover: nothing over the UI or a label
            if (overUi) hover = LandHit.None;
            else
            {
                // picked again when the pointer, the camera or the land moved
                int rig = root.Rig != null ? root.Rig.Version : 0;
                if ((pointer - lastPointer).sqrMagnitude > 0.25f || sceneVersion != LandService.Version || rig != seenRig) hover = PickAt(pointer);
                seenRig = rig;
            }

            lastPointer = pointer;

            Keyboard kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && !HudKit.TypingInField()) pinned = LandHit.None;
            if (mouse != null) TrackClicks(mouse, pointer);
            Light();
        }

        /// <summary>The snapshot's pickable things and the cut's, made again when the land or the cut's year changed.</summary>
        void Prepare()
        {
            LandSnapshot s = LandService.Current;
            if (s != null && sceneVersion != LandService.Version)
            {
                sceneVersion = LandService.Version;
                scene = LandPickScene.Build(s, model.Lives);
                RootGeom[] roots = RootsModel.Build(model.Data, s.Land, s.Year);
                rootDollars = new double[RootsModel.RootCount(model.Data)];
                foreach (RootGeom r in roots)
                {
                    if (r.Index >= 0 && r.Index < rootDollars.Length) rootDollars[r.Index] = r.Dollars;
                }

                rootsOf = new List<int>[model.Data.Industries.Count];
            }

            if (cutYear != EconomyState.CutYear && pop != null)
            {
                cutYear = EconomyState.CutYear;
                cut = CutPickScene.Build(pop, wall, cutYear, EconomyStage.TimelineWarp());
            }
        }

        LandHit PickAt(Vector2 pointer)
        {
            Camera cam = root.Rig != null ? root.Rig.Cam : null;
            if (cam == null || pointer.x < 0 || pointer.y < 0 || pointer.x > cam.pixelWidth || pointer.y > cam.pixelHeight) return LandHit.None;
            Prepare();
            LandProjector proj = new LandProjector(cam.transform.position, cam.transform.rotation, cam.fieldOfView,
                new Vector2(cam.pixelWidth, cam.pixelHeight));
            return LandPick.Pick(scene, cut, EconomyStage.Land(), proj, pointer, PickOptions.FromView(LabelSystem.UiScale), LandView.Round);
        }

        /// <summary>
        /// A left click that is not a drag, not over the UI, not on a label: act on what is under it (see the class
        /// summary). Marks the frame as taken when the land (or the wall) took it.
        /// </summary>
        void TrackClicks(Mouse mouse, Vector2 pointer)
        {
            if (mouse.leftButton.wasPressedThisFrame)
            {
                Keyboard kb = Keyboard.current;
                bool shift = kb != null && kb.shiftKey.isPressed; // shift + left drag pans
                pressValid = !shift && !HudKit.PointerOverUi();
                pressPosition = pointer;
            }

            if (!mouse.leftButton.wasReleasedThisFrame || !pressValid) return;
            pressValid = false;
            float slop = ClickSlopPx * LabelSystem.UiScale;
            if ((pointer - pressPosition).sqrMagnitude > slop * slop || root.Labels.Hovered != null) return;
            Click(PickAt(pointer), pointer);
        }

        void Click(LandHit h, Vector2 pointer)
        {
            bool again = !h.IsNone && (Same(h, pinned) || h.Kind == PickKind.Player && h.Index == EconomyState.SelectedPlayer ||
                                       (h.Kind == PickKind.Dot || h.Kind == PickKind.CutDot) && h.Person == EconomyState.Person);
            switch (h.Kind)
            {
                case PickKind.None:
                    if (YearClicks(root.CurrentPreset) && WallYear(pointer, out int year))
                    {
                        EconomyState.SetYear(year);
                        tookFrame = Time.frameCount;
                        return;
                    }

                    // an empty click on the land clears its selections and the pinned card (the person stays: × or Esc)
                    pinned = LandHit.None;
                    if (EconomyControls.IsLandView(root.CurrentPreset)) EconomyState.SetSelection(-1, -1, -1);
                    return;
                case PickKind.Player:
                    EconomyState.SetSelection(h.Index, EconomyState.SelectedTower, EconomyState.SelectedTie);
                    PlayerPanel.BringToFront();
                    break;
                case PickKind.Dot:
                case PickKind.CutDot:
                    EconomyState.SetPerson(h.Person);
                    break;
                case PickKind.Tower:
                    EconomyState.SetSelection(EconomyState.SelectedPlayer, h.Index, EconomyState.SelectedTie);
                    Pin(h, pointer);
                    break;
                case PickKind.Tie:
                    EconomyState.SetSelection(EconomyState.SelectedPlayer, EconomyState.SelectedTower, h.Index);
                    Pin(h, pointer);
                    break;
                default:
                    Pin(h, pointer);
                    break;
            }

            tookFrame = Time.frameCount;
            if (again) Focus(h);
        }

        void Pin(LandHit h, Vector2 pointer)
        {
            pinned = h;
            pinnedAt = pointer;
        }

        static bool Same(LandHit a, LandHit b) => !a.IsNone && Equal(a, b);

        /// <summary>The same thing (or both nothing): what a hit lights and its card depend on no more than this.</summary>
        static bool Equal(LandHit a, LandHit b) => a.Kind == b.Kind && a.Index == b.Index && a.Person == b.Person;

        /// <summary>Focus: the camera flies closer to the thing clicked a second time.</summary>
        void Focus(LandHit h)
        {
            if (root.Rig == null) return;
            CameraPose pose = root.Rig.Pose;
            pose.Target = h.World;
            pose.Distance = Mathf.Max(pose.Distance * FocusZoom, Mathf.Min(pose.Distance, FocusMinDistance));
            root.Rig.FlyTo(pose, FocusSeconds);
        }

        /// <summary>The whole year under a screen point on the industry wall (the timeline's lens), if any.</summary>
        bool WallYear(Vector2 pointer, out int year)
        {
            year = 0;
            Camera cam = root.Rig != null ? root.Rig.Cam : null;
            if (cam == null || wall == null) return false;
            LandProjector proj = new LandProjector(cam.transform.position, cam.transform.rotation, cam.fieldOfView,
                new Vector2(cam.pixelWidth, cam.pixelHeight));
            if (!LandPick.WallYear(wall, GraphWarp.Current, proj, pointer, out double y)) return false;
            year = (int)Math.Floor(y);
            return true;
        }

        /// <summary>
        /// Keeps the selections on the same things when another year's land is shown: the player by its key (cleared when
        /// the year has no such player), the tie cleared (pairs differ by year), the tower cleared before the towers' years.
        /// </summary>
        void Follow()
        {
            LandSnapshot s = LandService.Current;
            if (s?.Players?.Players == null) return;
            Player[] ps = s.Players.Players;
            int player = EconomyState.SelectedPlayer;
            if (s.Year == selectionYear)
            {
                selectedKey = player >= 0 && player < ps.Length ? ps[player].Key : null;
                return;
            }

            int remapped = -1;
            for (int i = 0; i < ps.Length && selectedKey != null; i++)
            {
                if (ps[i].Key == selectedKey) remapped = i;
            }

            bool towers = s.Land?.Towers != null && s.Land.Towers.Length > 0;
            if (selectionYear >= 0) EconomyState.SetSelection(remapped, towers ? EconomyState.SelectedTower : -1, -1);
            selectionYear = s.Year;
            pinned = LandHit.None;
            selectedKey = EconomyState.SelectedPlayer >= 0 && EconomyState.SelectedPlayer < ps.Length ? ps[EconomyState.SelectedPlayer].Key : null;
        }

        // ------------------------------------------------------------------ the highlight

        /// <summary>
        /// The hovered and pinned things and the selections, lit together (at most eight ranges; the hovered first). Set
        /// again only when that set changed; released, and handed back to the person inspector, when it is empty.
        /// </summary>
        void Light()
        {
            LandSnapshot s = LandService.Current;
            if (!Equal(hover, rangesHover) || hoverRangesLand != LandService.Version)
            {
                rangesHover = hover;
                hoverRangesLand = LandService.Version;
                RangesOf(hover, s, hoverRanges);
            }

            if (!Equal(pinned, rangesPinned) || pinnedRangesLand != LandService.Version)
            {
                rangesPinned = pinned;
                pinnedRangesLand = LandService.Version;
                RangesOf(pinned, s, pinnedRanges);
            }

            ranges.Clear();
            foreach (IdRange r in hoverRanges) Add(r);
            if (!Same(pinned, hover))
            {
                foreach (IdRange r in pinnedRanges) Add(r);
            }
            int player = EconomyState.SelectedPlayer, tower = EconomyState.SelectedTower, tie = EconomyState.SelectedTie;
            if (player >= 0) Add(EconomyIds.LandPlayers(player, player));
            if (tower >= 0) Add(IdRange.Single(EconomyIds.LandTower(tower)));
            if (tie >= 0) Add(IdRange.Single(EconomyIds.LandTie(tie)));
            bool land = ranges.Count > 0;
            if (land && EconomyState.Person >= 0 && pop?.Sim != null && EconomyState.Person < pop.Sim.People.Count)
            {
                Add(IdRange.Single(pop.Id(pop.Sim.People[EconomyState.Person])));
            }

            if (!land)
            {
                Release(true);
                return;
            }

            int signature = 17;
            foreach (IdRange r in ranges) signature = signature * 31 + r.Min * 7 + r.Max;
            if (OwnsHighlight && signature == litSignature) return;
            litSignature = signature;
            Highlighter.Set(ranges, GraphStyle.HighlightGlow, hover.IsNone ? SelectDim : HoverDim);
            litFirst = ranges[0];
            OwnsHighlight = true;
        }

        /// <summary>
        /// What a hit lights (<see cref="LandPick.Ranges"/>), into <paramref name="into"/>: made when the hovered or pinned
        /// thing or the land changes, not every frame (a sector's roots come from <see cref="RootsOf"/>).
        /// </summary>
        void RangesOf(LandHit h, LandSnapshot s, List<IdRange> into)
        {
            into.Clear();
            if (h.IsNone) return;
            int line = -1;
            if ((h.Kind == PickKind.Dot || h.Kind == PickKind.CutDot) && pop?.Sim != null && h.Person >= 0 && h.Person < pop.Sim.People.Count)
            {
                line = pop.Id(pop.Sim.People[h.Person]);
            }

            List<int> roots = h.Kind == PickKind.Sector || h.Kind == PickKind.Pool || h.Kind == PickKind.CutBar ? RootsOf(h.Index) : null;
            LandPick.Ranges(h, s, roots, line, into, rootDollar);
        }

        /// <summary>An industry's roots in and out, kept per snapshot.</summary>
        List<int> RootsOf(int industry)
        {
            Prepare();
            if (industry < 0 || industry >= rootsOf.Length) return null;
            return rootsOf[industry] ??= RootsModel.RootsOf(model.Data, industry);
        }

        void Add(IdRange r)
        {
            if (r.IsEmpty || ranges.Count >= LandPick.MaxRanges) return;
            foreach (IdRange q in ranges)
            {
                if (q.Min == r.Min && q.Max == r.Max) return;
            }

            ranges.Add(r);
        }

        /// <summary>Gives the highlight up: cleared when it is still this module's, and the person's line lights again.</summary>
        void Release(bool relight)
        {
            if (!OwnsHighlight) return;
            OwnsHighlight = false;
            litSignature = 0;

            // only this module's highlight: a label's focus (the HUD) or the director may have replaced it
            if (Highlighter.HasHighlight && Highlighter.IsHighlighted(litFirst)) Highlighter.Clear();
            if (relight) PersonInspector.Relight();
        }

        // ------------------------------------------------------------------ the card

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            Vector2 canvasSize = canvasRect.rect.size;
            float scale = canvasSize.x > 0 ? Screen.width / canvasSize.x : 1;
            Mouse mouse = Mouse.current;
            Vector2 pointer = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
            if (!loaded || root.TourActive)
            {
                card.Tick(pointer / Mathf.Max(1e-4f, scale), canvasSize, dt);
                return;
            }

            LandHit shown = !hover.IsNone ? hover : pinned;
            if (shown.IsNone || root.Labels.Hovered != null) card.Hide();
            else ShowCard(shown);
            Vector2 at = !hover.IsNone ? pointer : pinnedAt;
            card.Tick(at / Mathf.Max(1e-4f, scale), canvasSize, dt);
        }

        /// <summary>A hit's card: its anchor (generated blurbs) or a generated line for ties and dots.</summary>
        void ShowCard(LandHit h)
        {
            LandSnapshot s = LandService.Current;
            object key = CardKey(h);
            if (card.IsShowing(key)) return;
            string anchorKey = AnchorKey(h, s);
            if (anchorKey != null && Anchors.TryGet(anchorKey, out Anchor a))
            {
                if (h.Kind == PickKind.Tower)
                {
                    // the tower's blurb, then who owns it (the ownership fan's line, 7.5)
                    string owners = TowerFacts.Owners(s, model.Data, model.Lives, h.Index);
                    card.Show(key, a.Label, HudKit.LevelName(a.Level).ToUpperInvariant(), owners.Length > 0 ? a.Blurb + "\n\n" + owners : a.Blurb);
                    return;
                }

                card.ShowAnchor(key, a, a.Label);
                return;
            }

            switch (h.Kind)
            {
                case PickKind.Tie:
                    TieCard(key, h, s);
                    return;
                case PickKind.Dot:
                case PickKind.CutDot:
                    DotCard(key, h, s);
                    return;
                default:
                    card.Hide();
                    return;
            }
        }

        /// <summary>The card's key for a hit: the same object while the hit and the land stay the same.</summary>
        object CardKey(LandHit h)
        {
            if (Equal(h, cardHit) && cardLand == LandService.Version) return cardKey;
            cardHit = h;
            cardLand = LandService.Version;
            cardKey = new object();
            return cardKey;
        }

        /// <summary>The anchor a hit's card shows (the land layers register them with generated blurbs).</summary>
        string AnchorKey(LandHit h, LandSnapshot s)
        {
            var data = model.Data;
            string Industry(int i) => i >= 0 && i < data.Industries.Count ? data.Industries[i].Id : "";
            switch (h.Kind)
            {
                case PickKind.Player:
                    Player[] ps = s?.Players?.Players;
                    return ps != null && h.Index < ps.Length ? "land:player:" + ps[h.Index].Key : null;
                case PickKind.Tower:
                    foreach (TowerGeom t in s?.Land?.Towers ?? Array.Empty<TowerGeom>())
                    {
                        if (t.Company == h.Index) return "land:tower:" + (string.IsNullOrEmpty(t.Ticker) ? t.Name : t.Ticker);
                    }

                    return null;
                case PickKind.Fall:
                case PickKind.River:
                    return h.Index == LandStyle.LaneTaxes ? "land:taxes" : h.Index == LandStyle.LaneAbroad ? "land:abroad" : "land:river:" + LandPick.LaneId(h.Index);
                case PickKind.Canal: return "land:canal";
                case PickKind.Crown: return "land:crown";
                case PickKind.Pool:
                    return Anchors.TryGet("land:pool:" + Industry(h.Index), out _) ? "land:pool:" + Industry(h.Index) : "land:sector:" + Industry(h.Index);
                case PickKind.Sector: return "land:sector:" + Industry(h.Index);
                case PickKind.CutBar: return "industry:" + Industry(h.Index);
            }

            return null;
        }

        /// <summary>A tie: who, how they cooperate now and how they opened, whether they share a group.</summary>
        void TieCard(object key, LandHit h, LandSnapshot s)
        {
            SocialSeasonResult season = SocialLayer.Shown;
            if (season == null || s == null || season.Year != s.Year) season = s?.Society;
            Player[] ps = s?.Players?.Players;
            if (season?.PairA == null || ps == null || h.Index >= season.PairA.Length || season.PairA[h.Index] >= ps.Length ||
                season.PairB[h.Index] >= ps.Length)
            {
                card.Hide();
                return;
            }

            Player a = ps[season.PairA[h.Index]], b = ps[season.PairB[h.Index]];
            int t = Mathf.Clamp(LandView.Round, 0, season.CoopAB.Length - 1);
            float ab = season.CoopAB[t][h.Index], ba = season.CoopBA[t][h.Index];
            string body = "Round " + t.ToString(LandFacts.Ci) + ": the first cooperates " + LandFacts.Num(ab, 2) + " of the time, the second " +
                          LandFacts.Num(ba, 2) + " (mutual " + LandFacts.Num(ab * ba, 2) + "); they opened at " +
                          LandFacts.Num(season.Opening[0][h.Index], 2) + " and " + LandFacts.Num(season.Opening[1][h.Index], 2) + ". " +
                          (a.Group == b.Group ? "Same group." : "Different groups.") + " Click it, then Betray in the panel, to make the " +
                          "first betray the second for one round.";
            card.Show(key, PlayerFacts.Label(a, model.Data) + "  ~  " + PlayerFacts.Label(b, model.Data), "A TIE OF TIT FOR TAT", body);
        }

        /// <summary>A member's dot: the lifeline it is and the player it plays in.</summary>
        void DotCard(object key, LandHit h, LandSnapshot s)
        {
            EconomicLives lives = model.Lives;
            int year = s?.Year ?? EconomyState.Year;
            string body = "One lifeline of 100,000 people. Click it to look inside this life.";
            if (lives != null && lives.TryGet(h.Person, year, out PersonYear r) && r.Adult)
            {
                body = "Age " + Mathf.Floor(r.Age).ToString("0", LandFacts.Ci) + "  ·  reason " + LandFacts.Num(r.Reason, 2) + "  ·  " +
                       (r.InControl ? "in control of their path (gold)" : "not in control") + ". " + body;
            }

            int[] of = s?.Players?.PlayerOfPerson;
            string meta = of != null && h.Person < of.Length && of[h.Person] >= 0
                ? "IN " + PlayerFacts.Label(s.Players.Players[of[h.Person]], model.Data).ToUpperInvariant()
                : "IN THE CUT";
            card.Show(key, "A life in " + year.ToString(LandFacts.Ci), meta, body);
        }
    }
}
