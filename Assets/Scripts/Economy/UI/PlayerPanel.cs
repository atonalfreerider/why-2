using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Why.Economy.Land;
using Why.Economy.Layers;
using Why.Economy.Model;
using Why.UI;

namespace Why.Economy.UI
{
    /// <summary>
    /// The player inspector (SPEC 7.5): click a player's disc, head or dot cluster in the land (<see cref="LandPicker"/>)
    /// and this panel shows the player in its year (<see cref="PlayerFacts"/>):
    /// <list type="number">
    /// <item><b>Who</b>: its group and the group's rule, its anchor (what pays it) and the named employers with their
    /// shares of its wages, party and generation shares, adults and children;</item>
    /// <item><b>Money</b>: income by source as one bar (wages light blue, business and capital gold, transfers steel) and in
    /// words, taxes; spending by the six categories, each bar split into its fear (ice) and desire (rose) dollars with the
    /// fantasy part underlined, and its three first recipients; saving or borrowing, wealth; the water budget's identity in
    /// words ("in $412B = spending $338B + saving $21B + taxes $91B · gap $0.0B");</item>
    /// <item><b>Mind</b>: reason, the higher-OS share, future, agency, in control, fear and fantasy, each against the
    /// population's (the tick); the strongest desires and fears; the modes of thought (population shares, labeled so);
    /// married and with-children shares;</item>
    /// <item><b>Society</b>: tit for tat with its forgiveness, the members' own strategy mix, its partners in and out of
    /// its group, cooperation given and received at the round shown, its coalition and standing, its tribal memory;</item>
    /// <item><b>Members</b>: "Show a member" opens the person inspector on the member with the median income; N then
    /// cycles the members (by income).</item>
    /// </list>
    /// The person's and the player's inspectors share one place, the one opened last in front (<see cref="Covers"/>): the
    /// person's "Plays as" link brings this one back. Landscape: the right column under the year chip, scaled to its room
    /// and clear of the bowl. Portrait with the land in view: a bottom sheet under the bowl, more compact (the money and
    /// mind in fewer rows, no recipients, rule or modes), waiting while the social panel's sheet is up. Esc or × closes it.
    /// Hidden during the tour.
    /// </summary>
    [GraphScenes(GraphScene.Economy)]
    public sealed class PlayerPanel : GraphModule
    {
        /// <summary>With the person inspector above the HUD (40), below the social panel (45) and the tour (50).</summary>
        const int SortingOrder = 44;

        const float Pad = HudKit.Pad, Row = 17f, HeadingRow = 16f, SectionGap = 9f, CompactGap = 5f, CloseSize = 22f, ColumnGap = 14f;
        const float BarHeight = 7f, FantasyLine = 2f, TickWidth = 2f, TickOverhang = 3f, ButtonHeight = 24f;
        const float CategoryName = 112f, MoneyWidth = 50f, GaugeName = 82f, GaugeValue = 40f, FadeSpeed = 5f;
        const float FontSmall = HudKit.SizeSmall, FontBody = HudKit.SizeBody;

        static readonly Color Track = new Color(1, 1, 1, 0.07f);
        static readonly Color GaugeColor = new Color(0.86f, 0.87f, 0.90f, 0.6f);
        static readonly Color TickColor = new Color(1, 1, 1, 0.92f);
        static readonly Color FantasyColor = new Color(1, 1, 1, 0.75f);
        const float BarAlpha = 0.85f;

        /// <summary>The income bar's colors: wages (light blue), business (gold), capital (dim gold), transfers (steel).</summary>
        static readonly Color[] IncomeColors = { EconomyStyle.Wages, EconomyStyle.Capital, EconomyStyle.CapitalDim, EconomyStyle.State };

        // ------------------------------------------------------------------ which inspector is in front

        static int frontPerson = -2, frontPlayer = -2;
        static bool playerFront = true;
        static PlayerPanel instance;

        /// <summary>True while a player is selected and its inspector is in front of the person's (the one opened last).</summary>
        public static bool Covers => EconomyState.SelectedPlayer >= 0 && playerFront;

        /// <summary>The canvas box the panel takes while shown (empty otherwise).</summary>
        public static UiBox Occupied { get; private set; } = UiBox.Empty;

        /// <summary>True while the panel is a portrait bottom sheet on screen (the land legend steps aside).</summary>
        public static bool SheetShown { get; private set; }

        /// <summary>
        /// Follows the selections (both inspectors call it every frame): a newly selected player comes to the front, a newly
        /// selected person goes in front of it; with nobody selected the player's is in front.
        /// </summary>
        public static void TrackFront()
        {
            int person = EconomyState.Person, player = EconomyState.SelectedPlayer;
            if (person != frontPerson)
            {
                frontPerson = person;
                if (person >= 0) playerFront = false;
            }

            if (player != frontPlayer)
            {
                frontPlayer = player;
                if (player >= 0) playerFront = true;
            }

            if (person < 0) playerFront = true;
        }

        /// <summary>Frame on which an inspector took Esc (one Esc closes one inspector: the one in front).</summary>
        static int escFrame = -1;

        /// <summary>Takes this frame's Esc for an inspector; false when the other one already took it.</summary>
        public static bool TakeEsc()
        {
            if (escFrame == Time.frameCount) return false;
            escFrame = Time.frameCount;
            return true;
        }

        /// <summary>The person inspector's "Plays as" link: the player's inspector comes to the front.</summary>
        public static void BringToFront()
        {
            TrackFront();
            playerFront = true;
        }

        /// <summary>
        /// The N key with a player inspected (in front, or one of its members shown): the next member by income (the
        /// median-income member first), or -1 when no player's members are being cycled.
        /// </summary>
        public static int NextMember()
        {
            PlayerFacts f = instance?.facts;
            if (f == null || f.Player != EconomyState.SelectedPlayer || f.MembersByIncome.Length == 0) return -1;
            int at = Array.IndexOf(f.MembersByIncome, EconomyState.Person);
            if (!Covers && at < 0) return -1;
            return at < 0 ? f.MedianMember : f.MembersByIncome[(at + 1) % f.MembersByIncome.Length];
        }

        // ------------------------------------------------------------------ the panel

        sealed class Bar
        {
            public TextMeshProUGUI Name, Value, Note;
            public Image Track, Fill, Second, Extra;
        }

        GraphRoot root;
        RectTransform canvasRect, panel, close;
        TextMeshProUGUI kicker, title, rule, whoHeading, people, party, generations, employers;
        TextMeshProUGUI moneyHeading, incomeLine, identity, balance, spendingHeading, mindHeading, wants, fears, modes, family;
        TextMeshProUGUI societyHeading, play, mix, dealings, standing, hint;
        Image incomeTrack;
        readonly Image[] incomeParts = new Image[4];
        readonly Bar[] categories = new Bar[6];
        readonly Bar[] gauges = new Bar[PlayerFacts.MindNames.Length];
        Button member;
        UiFade fade;
        HudBlocks hud;
        EconomyModel model;
        PlayerFacts facts;
        bool loaded, contentDirty, laidOutSheet, checkPending, tourWasActive;
        RectTransform helpSheet;
        int factsPlayer = -2, factsLand = -1, factsRound = -1, seenVersion = -1;
        HudFrame laidOutFrame;
        UiBox laidOutAbove, panelBox;
        float layoutWidth, contentHeight, panelScale = 1;

        /// <summary>The content's height at a layout width, full or compact (the layout's measure; made once).</summary>
        Func<float, bool, float> heightAt;

        /// <summary>The compact (sheet) content's height at a layout width.</summary>
        Func<float, float> sheetHeightAt;
        string loggedProblems = "";
        readonly List<string> problems = new List<string>();

        public override void Init(GraphRoot graphRoot)
        {
            root = graphRoot;
            instance = this;
            heightAt = (w, compact) => Content(w, compact);
            sheetHeightAt = w => Content(w, true);
            Canvas canvas = UiFactory.CreateCanvas("PlayerPanel", SortingOrder, transform);
            canvasRect = (RectTransform)canvas.transform;
            panel = HudKit.FramedPanel(canvasRect, "PlayerInspector", 0.9f, true);
            Color dim = GraphStyle.TextDim, text = GraphStyle.Text;

            kicker = Heading("Kicker");
            close = (RectTransform)HudKit.Button(panel, "Close", "×", FontBody + 2, Close).transform;
            title = UiFactory.Text(panel, "Title", "", HudKit.SizeTitle, text, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            rule = Wrapped("Rule", FontSmall - 0.5f, dim);

            whoHeading = Heading("WhoHeading");
            people = Wrapped("People", FontSmall, text);
            party = Wrapped("Party", FontSmall - 0.5f, dim);
            generations = Wrapped("Generations", FontSmall - 0.5f, dim);
            employers = Wrapped("Employers", FontSmall - 0.5f, dim);

            moneyHeading = Heading("MoneyHeading");
            incomeTrack = Pill(panel, "IncomeTrack", Track, BarHeight + 2);
            for (int k = 0; k < 4; k++) incomeParts[k] = Plain(incomeTrack.transform, "Income" + k, IncomeColors[k]);
            incomeLine = Wrapped("Income", FontSmall, text);
            identity = Wrapped("Identity", FontSmall - 0.5f, dim);
            balance = Wrapped("Balance", FontSmall - 0.5f, dim);

            spendingHeading = Heading("SpendingHeading");
            for (int c = 0; c < categories.Length; c++) categories[c] = NewBar("Category" + c, true);

            mindHeading = Heading("MindHeading");
            mindHeading.text = "MIND  ·  TICK: THE POPULATION";
            for (int g = 0; g < gauges.Length; g++)
            {
                gauges[g] = NewBar("Gauge" + g, false);
                gauges[g].Name.text = PlayerFacts.MindNames[g];
                gauges[g].Extra = Plain(gauges[g].Track.transform, "Mean", TickColor);
            }

            wants = Wrapped("Wants", FontSmall - 0.5f, text);
            fears = Wrapped("Fears", FontSmall - 0.5f, text);
            modes = Wrapped("Modes", FontSmall - 1, HudKit.TextFaint);
            family = Wrapped("Family", FontSmall - 0.5f, dim);

            societyHeading = Heading("SocietyHeading");
            societyHeading.text = "SOCIETY";
            play = Wrapped("Play", FontSmall, text);
            mix = Wrapped("Mix", FontSmall - 1, HudKit.TextFaint);
            dealings = Wrapped("Dealings", FontSmall - 0.5f, dim);
            standing = Wrapped("Standing", FontSmall - 0.5f, dim);

            member = HudKit.Button(panel, "Member", "Show a member (median income)", FontSmall, ShowMember);
            hint = Wrapped("Hint", FontSmall - 1, HudKit.TextFaint);
            hint.text = "N: the next member  ·  Esc or × closes";

            // created last: a hidden fade deactivates the panel (texts are measured while it is shown)
            fade = new UiFade(panel.gameObject, 0, FadeSpeed, true);
        }

        public override void OnLoaded(GraphRoot graphRoot)
        {
            model = root.Context.Shared<EconomyModel>(EconomyModel.SharedKey);
            hud = new HudBlocks(root);
            foreach (GraphModule m in root.Modules)
            {
                if (m is Hud) helpSheet = m.transform.Find("HudOverlay/Help") as RectTransform;
            }

            loaded = model?.Data != null;
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
            Occupied = UiBox.Empty;
            SheetShown = false;
        }

        TextMeshProUGUI Heading(string name)
        {
            TextMeshProUGUI t = HudKit.Line(panel, name, "", FontSmall - 1, HudKit.TextFaint, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            t.characterSpacing = 10;
            t.overflowMode = TextOverflowModes.Ellipsis;
            return t;
        }

        TextMeshProUGUI Wrapped(string name, float size, Color color)
        {
            TextMeshProUGUI t = UiFactory.Text(panel, name, "", size, color);
            t.overflowMode = TextOverflowModes.Overflow;
            t.richText = false; // generated from data: never markup
            return t;
        }

        Bar NewBar(string name, bool category)
        {
            Bar b = new Bar
            {
                Name = HudKit.Line(panel, name + "Name", "", FontSmall, GraphStyle.TextDim, FontStyles.Normal, TextAlignmentOptions.MidlineLeft),
                Value = HudKit.Line(panel, name + "Value", "", FontSmall, GraphStyle.Text, FontStyles.Normal, TextAlignmentOptions.MidlineRight),
                Track = Pill(panel, name + "Track", Track, BarHeight)
            };
            b.Name.overflowMode = TextOverflowModes.Ellipsis;
            b.Fill = Plain(b.Track.transform, "Fill", GaugeColor);
            if (category)
            {
                b.Second = Plain(b.Track.transform, "Desire", EconomyStyle.Desire);
                b.Extra = Plain(b.Track.transform, "Fantasy", FantasyColor);
                b.Note = Wrapped(name + "Recipients", FontSmall - 1.5f, HudKit.TextFaint);
            }

            return b;
        }

        static Image Pill(Transform parent, string name, Color color, float height)
        {
            Image img = UiFactory.Panel(parent, name, color, false);
            img.pixelsPerUnitMultiplier = 14f / (height * 0.5f);
            return img;
        }

        static Image Plain(Transform parent, string name, Color color)
        {
            Image img = UiFactory.Rect(parent, name).gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        // ------------------------------------------------------------------ actions

        /// <summary>The × button and Esc: no player is selected any more (the tower and tie selections stay).</summary>
        static void Close()
        {
            HudKit.ReleaseSelection();
            EconomyState.SetSelection(-1, EconomyState.SelectedTower, EconomyState.SelectedTie);
        }

        /// <summary>"Show a member": the person inspector opens on the member with the median income.</summary>
        void ShowMember()
        {
            HudKit.ReleaseSelection();
            if (facts != null && facts.MedianMember >= 0) EconomyState.SetPerson(facts.MedianMember);
        }

        // ------------------------------------------------------------------ per frame

        void Update()
        {
            if (!loaded) return;
            bool tour = root.TourActive;
            TrackFront();
            Keyboard kb = Keyboard.current;
            bool helpOpen = helpSheet != null && helpSheet.gameObject.activeInHierarchy; // Esc closes the HUD's help first
            if (!tour && !tourWasActive && kb != null && kb.escapeKey.wasPressedThisFrame && Covers && !helpOpen && !HudKit.TypingInField() &&
                TakeEsc()) Close();
            tourWasActive = tour;

            LandSnapshot s = LandService.Current;
            int player = EconomyState.SelectedPlayer;
            int round = LandView.Round;
            if (player != factsPlayer || LandService.Version != factsLand || round != factsRound && FromSeason())
            {
                factsPlayer = player;
                factsLand = LandService.Version;
                factsRound = round;
                facts = player >= 0 ? PlayerFacts.Build(s, model, SocialLayer.Shown, round, player) : null;
                contentDirty = true;
            }

            bool socialSheet = ScreenLayout.IsPortrait && HudBlocks.SocialPanelShown && EconomyControls.IsLandView(root.CurrentPreset);
            bool show = !tour && facts != null && Covers && !socialSheet;
            if (show != fade.Shown)
            {
                fade.Show(show);
                if (show) contentDirty = true;
            }
        }

        /// <summary>The society rows follow the round shown (the season plays); refreshed at most at its 4 Hz tick.</summary>
        static bool FromSeason() => LandView.PresetId == "society" || LandView.PresetId == "betrayal";

        void LateUpdate()
        {
            fade.Tick(Time.unscaledDeltaTime);
            if (!loaded || !fade.Shown || facts == null)
            {
                Occupied = UiBox.Empty;
                SheetShown = false;
                return;
            }

            if (checkPending) SelfCheck();
            Layout();
            Occupied = panelBox;
            SheetShown = laidOutSheet;
        }

        /// <summary>
        /// Fills the panel when the facts changed and places it: in landscape where
        /// <see cref="EconomyUiLayout.ColumnPlace"/> finds room clear of the bowl (the right column, the left one, compact,
        /// a band under the controls); on a portrait screen a bottom sheet under the bowl, laid out wider by 1 / scale so it
        /// spans the screen.
        /// </summary>
        void Layout()
        {
            HudFrame f = hud.Measure(canvasRect, UiFactory.CanvasSize);
            UiBox above = EconomyControls.Occupied;
            bool sheet = EconomyUiLayout.BottomSheets(f) || f.Portrait;
            if (!contentDirty && sheet == laidOutSheet && f.Near(laidOutFrame) && above.Near(laidOutAbove)) return;
            if (contentDirty) Fill(facts);
            contentDirty = false;
            laidOutSheet = sheet;
            laidOutFrame = f;
            laidOutAbove = above;
            if (sheet)
            {
                // under the bowl, or above it where the bowl reaches low (laid out last at the chosen width)
                EconomyUiLayout.Placement p = EconomyUiLayout.SheetPlace(f, above, sheetHeightAt);
                panelScale = p.Scale;
                layoutWidth = p.LayoutWidth;
                contentHeight = Content(layoutWidth, true);
                panelBox = p.Box;
            }
            else
            {
                // the right column, or the left one, compact, or a band where the bowl takes the column (laid out last as chosen)
                EconomyUiLayout.Placement p = EconomyUiLayout.ColumnPlace(f, above, EconomyUiLayout.LandscapeInspectorWidth, heightAt);
                layoutWidth = p.LayoutWidth;
                panelScale = p.Scale;
                contentHeight = Content(layoutWidth, p.Compact);
                panelBox = p.Box;
            }

            panel.Place(Vector2.one, Vector2.one, new Vector2(-(f.Canvas.x - panelBox.Right), -panelBox.Y), new Vector2(layoutWidth, contentHeight));
            panel.localScale = new Vector3(panelScale, panelScale, 1);
            checkPending = true;
        }

        void SelfCheck()
        {
            checkPending = false;
            problems.Clear();
            EconomyUiLayout.Check(laidOutFrame, "player inspector", panelBox, problems, laidOutSheet);
            if (panelBox.Overlaps(laidOutAbove)) problems.Add("player inspector " + panelBox + " overlaps the year controls " + laidOutAbove);
            string text = string.Join("; ", problems);
            if (text == loggedProblems) return;
            loggedProblems = text;
            if (text.Length > 0) Debug.LogWarning("[Why] PlayerPanel layout: " + text);
        }

        // ------------------------------------------------------------------ content

        /// <summary>The facts' texts and colors (the sizes depend on the width: <see cref="Content"/>).</summary>
        void Fill(PlayerFacts f)
        {
            kicker.text = "A PLAYER OF THE LAND  ·  " + f.Year.ToString(LandFacts.Ci);
            title.text = f.Title;
            rule.text = f.Rule;
            whoHeading.text = "WHO";
            people.text = f.People;
            party.text = f.Party;
            generations.text = f.Generations;
            employers.text = f.Employers;
            double income = f.Income[0] + f.Income[1] + f.Income[2] + f.Income[3];
            moneyHeading.text = "MONEY  ·  INCOME " + LandFacts.Money(income).ToUpperInvariant();
            incomeLine.text = f.IncomeLine;
            identity.text = f.Identity;
            balance.text = f.Balance;
            spendingHeading.text = "SPENDING " + LandFacts.Money(f.Spending).ToUpperInvariant() + "  ·  FEAR " + PersonFacts.Pct(f.Fear) +
                                   "  ·  FANTASY " + PersonFacts.Pct(f.Fantasy);
            for (int c = 0; c < categories.Length; c++)
            {
                Bar b = categories[c];
                b.Name.text = f.CategoryNames[c];
                b.Value.text = LandFacts.Money(f.Category[c]);
                b.Fill.color = new Color(EconomyStyle.Fear.r, EconomyStyle.Fear.g, EconomyStyle.Fear.b, BarAlpha);
                b.Second.color = new Color(EconomyStyle.Desire.r, EconomyStyle.Desire.g, EconomyStyle.Desire.b, BarAlpha);
                b.Note.text = f.Recipients[c].Length > 0 ? "paid first to " + f.Recipients[c] : "";
            }

            for (int g = 0; g < gauges.Length; g++)
            {
                gauges[g].Value.text = g == 0 || g == 2 || g == 3 ? f.Mind[g].ToString("0.00", LandFacts.Ci) : PersonFacts.Pct(f.Mind[g]);
                gauges[g].Fill.color = g == 4 ? new Color(EconomyStyle.Capital.r, EconomyStyle.Capital.g, EconomyStyle.Capital.b, BarAlpha)
                    : g == 5 ? new Color(EconomyStyle.Fear.r, EconomyStyle.Fear.g, EconomyStyle.Fear.b, BarAlpha)
                    : g == 6 ? FantasyColor : GaugeColor;
            }

            wants.text = f.Wants.Length > 0 ? "Wants  " + f.Wants : "";
            fears.text = f.Fears.Length > 0 ? "Fears  " + f.Fears : "";
            modes.text = f.Modes;
            family.text = f.Family;
            play.text = f.Play;
            mix.text = f.Mix;
            dealings.text = f.Dealings;
            standing.text = f.Standing;
        }

        /// <summary>Places everything for a width and returns the height; compact (a portrait sheet) leaves out details.</summary>
        float Content(float width, bool compact)
        {
            PlayerFacts f = facts;
            if (f == null) return 0;
            float inner = width - 2 * Pad, x = Pad, y = Pad - 2, gap = compact ? CompactGap : SectionGap;
            HudKit.PlaceTopLeft(close, width - Pad - CloseSize + 4, Pad - 6, new Vector2(CloseSize, CloseSize));
            if (Show(kicker, !compact)) y = Line(kicker, x, y, inner - CloseSize - 6) + 2;
            title.fontSize = compact ? HudKit.SizeTitle - 3 : HudKit.SizeTitle;
            y = Block(title, x, y, inner - CloseSize - 6) + 2;
            y = Show(rule, !compact && f.Rule.Length > 0) ? Block(rule, x, y, inner) : y;

            // who
            y += gap;
            if (Show(whoHeading, !compact)) y = Line(whoHeading, x, y, inner) + 2;
            y = Block(people, x, y, inner);
            y = Show(party, !compact) ? Block(party, x, y, inner) : y;
            y = Show(generations, !compact && f.Generations.Length > 0) ? Block(generations, x, y, inner) : y;
            y = Show(employers, f.Employers.Length > 0) ? Block(employers, x, y, inner) : y;

            // money: the income bar, the lines
            y += gap;
            y = Line(moneyHeading, x, y, inner) + 3;
            double income = f.Income[0] + f.Income[1] + f.Income[2] + f.Income[3];
            HudKit.PlaceTopLeft(incomeTrack.rectTransform, x, y, new Vector2(inner, BarHeight + 2));
            float at = 0;
            for (int k = 0; k < 4; k++)
            {
                float w = income > 0 ? (float)(f.Income[k] / income) * inner : 0;
                HudKit.PlaceTopLeft(incomeParts[k].rectTransform, at, 0, new Vector2(w, BarHeight + 2));
                at += w;
            }

            y += BarHeight + 6;
            y = Block(incomeLine, x, y, inner);
            y = Block(identity, x, y + 2, inner);
            y = Block(balance, x, y, inner);

            // spending: fear (ice) then desire (rose) dollars as one bar per category, the fantasy part underlined
            y += gap;
            y = Line(spendingHeading, x, y, inner) + 2;
            double most = 0;
            for (int c = 0; c < 6; c++) most = Math.Max(most, f.Category[c]);
            int columns = compact ? 2 : 1, rows = (6 + columns - 1) / columns;
            float cell = (inner - (columns - 1) * ColumnGap) / columns, rowStep = compact ? Row : Row + 13;
            for (int c = 0; c < 6; c++)
            {
                float cx = x + c / rows * (cell + ColumnGap), cy = y + c % rows * rowStep;
                PlaceCategory(categories[c], cx, cy, cell, most > 0 ? (float)(f.Category[c] / most) : 0, f.CategoryFear[c], f.CategoryFantasy[c]);
                if (Show(categories[c].Note, !compact && categories[c].Note.text.Length > 0))
                {
                    HudKit.PlaceTopLeft(categories[c].Note.rectTransform, cx + 8, cy + Row - 2, new Vector2(cell - 8, 13));
                }
            }

            y += rows * rowStep;

            // the mind: gauges against the population
            y += gap;
            y = Line(mindHeading, x, y, inner) + 2;
            int gcols = compact ? 2 : 1, grows = (gauges.Length + gcols - 1) / gcols;
            float gcell = (inner - (gcols - 1) * ColumnGap) / gcols;
            for (int g = 0; g < gauges.Length; g++)
            {
                PlaceGauge(gauges[g], x + g / grows * (gcell + ColumnGap), y + g % grows * Row, gcell, f.Mind[g], f.MindMean[g]);
            }

            y += grows * Row;
            y = Show(wants, f.Wants.Length > 0) ? Block(wants, x, y + 3, inner) : y;
            y = Show(fears, f.Fears.Length > 0) ? Block(fears, x, y, inner) : y;
            y = Show(family, !compact) ? Block(family, x, y + 2, inner) : y;
            y = Show(modes, !compact) ? Block(modes, x, y + 2, inner) : y;

            // society
            y += gap;
            if (Show(societyHeading, !compact)) y = Line(societyHeading, x, y, inner) + 2;
            y = Block(play, x, y, inner);
            y = Show(mix, !compact) ? Block(mix, x, y, inner) : y;
            y = Show(dealings, f.Dealings.Length > 0) ? Block(dealings, x, y, inner) : y;
            y = Show(standing, f.Standing.Length > 0) ? Block(standing, x, y, inner) : y;

            // members
            y += gap;
            Show(member, f.MedianMember >= 0);
            Show(hint, f.MedianMember >= 0);
            if (f.MedianMember >= 0)
            {
                float bw = compact ? inner * 0.5f : inner;
                HudKit.PlaceTopLeft((RectTransform)member.transform, x, y, new Vector2(bw, ButtonHeight));
                if (compact)
                {
                    // the hint beside the button
                    HudKit.PlaceTopLeft(hint.rectTransform, x + bw + 8, y + 5, new Vector2(inner - bw - 8, ButtonHeight));
                    y += ButtonHeight;
                }
                else y = Block(hint, x, y + ButtonHeight + 4, inner);
            }

            return Mathf.Ceil(y + Pad - 2);
        }

        static float Line(TextMeshProUGUI t, float x, float y, float width)
        {
            t.gameObject.SetActive(true);
            HudKit.PlaceTopLeft(t.rectTransform, x, y, new Vector2(width, HeadingRow));
            return y + HeadingRow;
        }

        static float Block(TextMeshProUGUI t, float x, float y, float width)
        {
            t.gameObject.SetActive(true);
            float h = t.text.Length > 0 ? HudKit.Measure(t, t.text, width).y : 0;
            HudKit.PlaceTopLeft(t.rectTransform, x, y, new Vector2(width, h));
            return y + h;
        }

        static bool Show(Component c, bool show)
        {
            if (c.gameObject.activeSelf != show) c.gameObject.SetActive(show);
            return show;
        }

        /// <summary>A category: name, a bar as long as its dollars (relative to the largest) split fear | desire, the fantasy underline, dollars.</summary>
        static void PlaceCategory(Bar b, float x, float y, float width, float share, float fear, float fantasy)
        {
            float nameWidth = Mathf.Min(CategoryName, width * 0.42f);
            HudKit.PlaceTopLeft(b.Name.rectTransform, x, y, new Vector2(nameWidth - 4, Row));
            HudKit.PlaceTopLeft(b.Value.rectTransform, x + width - MoneyWidth, y, new Vector2(MoneyWidth, Row));
            float track = Mathf.Max(10, width - nameWidth - MoneyWidth - 6), mid = y + Row * 0.5f;
            HudKit.PlaceTopLeft(b.Track.rectTransform, x + nameWidth, mid - BarHeight * 0.5f - 1, new Vector2(track, BarHeight));
            float len = track * Mathf.Clamp01(share), iceW = len * Mathf.Clamp01(fear);
            HudKit.PlaceTopLeft(b.Fill.rectTransform, 0, 0, new Vector2(iceW, BarHeight));
            HudKit.PlaceTopLeft(b.Second.rectTransform, iceW, 0, new Vector2(len - iceW, BarHeight));
            Show(b.Extra, fantasy > 0.0005f);
            HudKit.PlaceTopLeft(b.Extra.rectTransform, 0, BarHeight + 1, new Vector2(len * Mathf.Clamp01(fantasy), FantasyLine));
        }

        /// <summary>A gauge from 0 to 1 with the population's value as a tick.</summary>
        static void PlaceGauge(Bar b, float x, float y, float width, float value, float mean)
        {
            float nameWidth = Mathf.Min(GaugeName, width * 0.4f);
            HudKit.PlaceTopLeft(b.Name.rectTransform, x, y, new Vector2(nameWidth - 4, Row));
            HudKit.PlaceTopLeft(b.Value.rectTransform, x + width - GaugeValue, y, new Vector2(GaugeValue, Row));
            float track = Mathf.Max(10, width - nameWidth - GaugeValue - 6);
            HudKit.PlaceTopLeft(b.Track.rectTransform, x + nameWidth, y + (Row - BarHeight) * 0.5f, new Vector2(track, BarHeight));
            HudKit.PlaceTopLeft(b.Fill.rectTransform, 0, 0, new Vector2(track * Mathf.Clamp01(value), BarHeight));
            HudKit.PlaceTopLeft(b.Extra.rectTransform, track * Mathf.Clamp01(mean) - TickWidth * 0.5f, -TickOverhang,
                new Vector2(TickWidth, BarHeight + 2 * TickOverhang));
        }
    }
}
