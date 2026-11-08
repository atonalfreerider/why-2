using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Why.Economy.Land;
using Why.Economy.Layers;
using Why.Economy.Model;

namespace Why.Economy.UI
{
    /// <summary>
    /// The politics view's panel (<see cref="HillPoliticsLayer"/>; the user's seventh direction is
    /// Docs/research/advantage/brief-v5.md, the plan Docs/hills-design/POLITICS.md). At the top, the year the view shows,
    /// stepped a year at a time, jumped to by decade or left to follow the year chip (1945-1949, before the chip's first year,
    /// only by stepping back). Below, five tabs read the research at that year: who holds power (the president, each party's
    /// power by the stated rule with the seats and the popular vote behind it), the offices cascading below the president,
    /// the standing army as relative power (shares of the world's military spending, warheads, ships, alliances and Jev's
    /// walls), trade (the US balance and its partners') and the key moments around the year. Every number is read from
    /// <see cref="Politics"/> with the date it was measured, and what was not measured says so. A row is a button that
    /// highlights its subject in the view (a moment's row shows the view at its date), the highlighted row marked: buttons,
    /// not keys, so it works by pinch in the headset. One tab at a time keeps it short beside the view; it hides during a
    /// tour, whose card takes the right side.
    /// <para>
    /// In landscape it stands in the right column, never past the HUD's preset bar. In portrait the whole sheet would cover
    /// the view, so there it starts collapsed to its header row (the title, and the year between its steps) just above the
    /// HUD's legend and preset bar: the header is the only block the view's labels keep out of. Its More button opens the
    /// sheet downward over the legend and the bar, the header staying in place, and Hide gives them back.
    /// </para>
    /// </summary>
    [GraphScenes(EconomyLayouts.HillsScene)]
    public sealed class PoliticsPanel : GraphModule
    {
        const float Width = 420, Height = 520, Pad = 12, Gap = 4, ControlsTop = 54, DecadesTop = 82, TabsTop = 108,
            ContentTop = 140, RowMin = 22, RowInset = 8, MoreHeight = 16, StepWidth = 84;

        /// <summary>The landscape column's inset from the right edge, where the history and regimes panels stand.</summary>
        const float ColumnInset = 18;

        /// <summary>
        /// The portrait header row: a button's height with a margin above and below (the collapsed panel's whole height),
        /// its steps narrower than the column's, and the year between them as wide as a month's date ("Sep 2025", where a
        /// moment's row pins the view).
        /// </summary>
        const float HeaderHeight = 40, HeaderTop = 8, HeaderStep = 56, YearWidth = 60, ToggleWidth = 60;

        /// <summary>The least room the open portrait sheet takes below its top: the controls and four rows. Where the HUD's
        /// bottom blocks leave less below the header, the header rises as the sheet opens.</summary>
        const float SheetRoom = 256;

        /// <summary>
        /// The pools the tabs fill: the cascade needs the most rows (the president and eight offices in the years of a
        /// Secretary of War and of the Navy, with room for an acting holder beside a confirmed one), a tab at most three blocks.
        /// </summary>
        const int MaxRows = 12, MaxBlocks = 3;

        /// <summary>Key moments this many years either side of the year shown are listed: at most the eight nearest it, so
        /// the later moments of a busy stretch, listed first, do not push the year's own out of the panel; the rest are
        /// counted.</summary>
        const int MomentYears = 3, MaxMoments = 8;

        static readonly string MoreMoments = "more within " + MomentYears + " years";

        /// <summary>A partner's benchmark year (the research measured them about every five years from 1985) stands for the year
        /// shown within this many years.</summary>
        const float PartnerYears = 3;

        /// <summary>politics.json gives trade in millions of current US dollars (its trade unit, which the model does not read);
        /// the panel writes billions.</summary>
        const double MillionsPerBillion = 1000;

        const string PresidentId = "president", ArmyId = "army";

        enum Tab { Power, Offices, Army, Trade, Moments }

        static readonly string[] TabNames = { "Power", "Offices", "Army", "Trade", "Moments" };

        /// <summary>The rivals' ids as the view highlights them; the trade partner China is "china-trade" (<see cref="PartnerId"/>).</summary>
        static readonly string[] Rivals = { "ussr", "russia", "china" };

        /// <summary>The trading partners' names by the research's ids (politics.json names them by id alone).</summary>
        static readonly Dictionary<string, string> PartnerNames = new Dictionary<string, string>
        {
            { "china", "China" }, { "mexico", "Mexico" }, { "canada", "Canada" }, { "japan", "Japan" }, { "germany", "Germany" },
            { "eu", "European Union" },
        };

        /// <summary>The ink of text and of dimmed text, as UI takes the palette's roles (sRGB-encoded).</summary>
        static readonly Color Ink = EconomyStyle.Roles.Encoded(EconomyStyle.Roles.InkText), Dim = EconomyStyle.Roles.Encoded(EconomyStyle.Roles.InkDim);

        /// <summary>The panel's backing: the ink's backing, made opaque while the portrait sheet is open, because it then
        /// covers the HUD's legend and preset bar, whose text would show through it.</summary>
        static readonly Color Backing = EconomyStyle.Roles.Encoded(EconomyStyle.Roles.InkBacking), SheetBacking = WithAlpha(Backing, 1);

        /// <summary>A row of the shown tab: a button whose label is the reading, and the id it highlights in the view or, for a
        /// key moment, the date it shows.</summary>
        sealed class Row
        {
            public Button Button;
            public Image Back;
            public TextMeshProUGUI Label;
            public string Id;
            public float Date = float.NaN;
        }

        /// <summary>A row or a block of text of the shown tab, top down, as the layout places it.</summary>
        readonly struct Item
        {
            public readonly RectTransform Rect;
            public readonly TextMeshProUGUI Text;
            public readonly bool Row;

            public Item(RectTransform rect, TextMeshProUGUI text, bool row)
            {
                Rect = rect;
                Text = text;
                Row = row;
            }
        }

        GraphRoot root;
        HudBlocks hud;

        /// <summary>The panel's canvas, its box, and the sheet below the header row: the controls and the tab's readings,
        /// shown in portrait only while the sheet is open.</summary>
        RectTransform canvasRect, panel, body;

        Image backing, followBack, toggleBack;
        TextMeshProUGUI heading, yearLine, more, prevLabel, nextLabel, followLabel, toggleLabel;
        Button prev, next, follow, toggle;
        readonly List<(Button button, Image back, TextMeshProUGUI label, int decade)> decades = new List<(Button, Image, TextMeshProUGUI, int)>();
        readonly List<(Button button, Image back, TextMeshProUGUI label)> tabs = new List<(Button, Image, TextMeshProUGUI)>();
        readonly List<Row> rows = new List<Row>(MaxRows);
        readonly List<TextMeshProUGUI> blocks = new List<TextMeshProUGUI>(MaxBlocks);
        readonly List<Item> flow = new List<Item>(MaxRows + MaxBlocks);
        readonly List<Politics.Moment> moments = new List<Politics.Moment>();
        readonly StringBuilder sb = new StringBuilder(256);
        Tab tab = Tab.Power, shownTab;
        Politics shownModel;
        string shownSelected, moreWord = "more";
        float shownYear = float.NaN, shownPinned = float.NaN, maxHeight = Height;

        /// <summary>Where the panel was placed (canvas units from the top-left: its corner, its width and the most height it
        /// may take), in which orientation and with the portrait sheet open or not, so it is placed again only when one of
        /// them moves.</summary>
        UiBox placed = UiBox.Empty;

        bool dirty = true, laidOut, placedPortrait, placedOpen;

        /// <summary>The portrait sheet is open below its header row. It starts collapsed, so the view keeps the screen until
        /// the viewer asks for the readings.</summary>
        bool open;

        /// <summary>Rows the shown tab left out (the moments beyond the eight nearest the year), counted at the foot with those
        /// the panel's room hides.</summary>
        int usedRows, usedBlocks, leftOut;

        public override void Init(GraphRoot graphRoot)
        {
            root = graphRoot;
            Canvas canvas = UiFactory.CreateCanvas("Who holds power", 47, transform);
            canvasRect = (RectTransform)canvas.transform;
            backing = UiFactory.Panel(canvas.transform, "Politics", Backing);
            panel = backing.rectTransform;
            // the hills' labels keep out from under it: the panel's box, so the collapsed portrait sheet's header row alone
            HudBlocks.Register(panel);
            heading = Text(panel, "Title", 15, Ink);
            heading.text = "<b>WHO HOLDS POWER</b>";
            yearLine = Text(panel, "Year", 12, Ink);
            // one line each, as the portrait header row holds them beside the steps
            heading.textWrappingMode = yearLine.textWrappingMode = TextWrappingModes.NoWrap;
            heading.overflowMode = yearLine.overflowMode = TextOverflowModes.Ellipsis;
            body = UiFactory.Rect(panel, "Sheet");
            body.Fill();
            more = Text(body, "More", 9.5f, Dim);
            HillPoliticsLayer.Changed += OnChanged;
            // hidden until the view is shown, so it never stands empty over the loading screen
            panel.gameObject.SetActive(false);
        }

        void OnChanged() => dirty = true;

        void OnDestroy() => HillPoliticsLayer.Changed -= OnChanged;

        /// <summary>The year controls, the tabs and the tabs' pools, once the data gives the span the decade buttons cover. The
        /// steps and the portrait sheet's button stand in the header row; the rest in the sheet below it.</summary>
        void Make(Politics model)
        {
            prev = UiFactory.Button(panel, "Previous year", "", 10.5f, () => Step(-1));
            prevLabel = Label(prev);
            next = UiFactory.Button(panel, "Next year", "", 10.5f, () => Step(1));
            nextLabel = Label(next);
            toggle = UiFactory.Button(panel, "Open or close", "", 10.5f, () => open = !open);
            toggleBack = (Image)toggle.targetGraphic;
            toggleLabel = Label(toggle);
            follow = UiFactory.Button(body, "Follow the chip", "", 10.5f, () => HillPoliticsLayer.Pin(float.NaN));
            followBack = (Image)follow.targetGraphic;
            followLabel = Label(follow);
            // the whole decades within the span: from 1950, which is also the year chip's first year
            if (!float.IsNaN(model.From) && !float.IsNaN(model.To))
            {
                int last = Mathf.FloorToInt(model.To / 10) * 10;
                for (int d = Mathf.CeilToInt(model.From / 10) * 10; d <= last && decades.Count < 10; d += 10)
                {
                    int decade = d;
                    var b = UiFactory.Button(body, "Decade " + d, d.ToString(LandFacts.Ci), 10.5f, () => HillPoliticsLayer.Pin(decade + .5f));
                    decades.Add((b, (Image)b.targetGraphic, Label(b), d));
                }
            }

            for (int i = 0; i < TabNames.Length; i++)
            {
                var t = (Tab)i;
                var b = UiFactory.Button(body, "Tab " + TabNames[i], TabNames[i], 10.5f, () =>
                {
                    tab = t;
                    dirty = true;
                });
                tabs.Add((b, (Image)b.targetGraphic, Label(b)));
            }

            for (int i = 0; i < MaxRows; i++)
            {
                int k = i;
                var b = UiFactory.Button(body, "Row " + i, "", 10.5f, () => Click(k));
                TextMeshProUGUI label = Label(b);
                label.alignment = TextAlignmentOptions.Left;
                label.textWrappingMode = TextWrappingModes.Normal;
                label.overflowMode = TextOverflowModes.Ellipsis;
                label.rectTransform.Fill(RowInset, 3, RowInset, 3);
                rows.Add(new Row { Button = b, Back = (Image)b.targetGraphic, Label = label });
            }

            for (int i = 0; i < MaxBlocks; i++) blocks.Add(Text(body, "Block " + i, 9.5f, Dim));
        }

        /// <summary>The year before or after the one shown, pinned at its middle, where the research stands a year's measures
        /// (Tools/politics/build.py): a whole year's officeholders and seats show, not those of its first days.</summary>
        static void Step(int direction) => HillPoliticsLayer.Pin(Mathf.Floor(HillPoliticsLayer.Year) + direction + .5f);

        /// <summary>A row highlights its subject in the view, or clears it when it is the one highlighted; a key moment's row
        /// shows the view at the moment's date.</summary>
        void Click(int k)
        {
            Row r = rows[k];
            if (r.Id != null) HillPoliticsLayer.Select(HillPoliticsLayer.Selected == r.Id ? null : r.Id);
            else if (!float.IsNaN(r.Date)) HillPoliticsLayer.Pin(r.Date);
        }

        void Update()
        {
            if (!root || !root.IsLoaded) return;
            bool show = LandView.PresetId == HillPoliticsLayer.View && !root.TourActive;
            if (panel.gameObject.activeSelf != show) panel.gameObject.SetActive(show);
            if (!show) return;
            Dock();
            Politics model = Politics.Get();
            if (model != null && rows.Count == 0) Make(model);
            // the view's year follows the chip without a word from the layer, so the panel compares it too
            float year = HillPoliticsLayer.Year, pinned = HillPoliticsLayer.Pinned;
            string selected = HillPoliticsLayer.Selected;
            if (dirty || model != shownModel || !Same(year, shownYear) || !Same(pinned, shownPinned) || selected != shownSelected || tab != shownTab)
            {
                // a selection made in the view or by a cue opens the tab that lists it
                if (selected != null && selected != shownSelected) tab = TabOf(model, selected, tab);
                dirty = false;
                shownModel = model;
                shownYear = year;
                shownPinned = pinned;
                shownSelected = selected;
                shownTab = tab;
                Refresh(model, year, pinned, selected);
            }

            if (!laidOut) Layout();
        }

        /// <summary>
        /// Where the panel stands, measured against the HUD's blocks as they are now (the preset bar wraps to the screen, the
        /// legend to the view's key) and placed again only when that moved: in landscape the right column under the year
        /// chip and its readout (<see cref="Column"/>), in portrait a sheet across the screen (<see cref="Sheet"/>). Allocates
        /// nothing.
        /// </summary>
        void Dock()
        {
            hud ??= new HudBlocks(root);
            HudFrame f = hud.Measure(canvasRect, UiFactory.CanvasSize);
            UiBox box = f.Portrait ? Sheet(f) : Column(f);
            if (f.Portrait == placedPortrait && open == placedOpen && box.Near(placed)) return;
            if (f.Portrait != placedPortrait)
            {
                // the portrait header row centres the title and the year on its buttons; the year line reads differently there
                heading.alignment = f.Portrait ? TextAlignmentOptions.Left : TextAlignmentOptions.TopLeft;
                yearLine.alignment = f.Portrait ? TextAlignmentOptions.Center : TextAlignmentOptions.TopLeft;
                dirty = true;
            }

            placedPortrait = f.Portrait;
            placedOpen = open;
            placed = box;
            Place(panel, box.X, box.Y, box.W, panel.sizeDelta.y);
            maxHeight = box.H;
            backing.color = f.Portrait && open ? SheetBacking : Backing;
            laidOut = false;
        }

        /// <summary>The landscape column: at the right edge under the year chip and its readout, as tall as its content up to
        /// <see cref="Height"/>, and never past the first of the HUD's bottom blocks in its column (the preset bar).</summary>
        static UiBox Column(HudFrame f)
        {
            float top = EconomyUiLayout.SideColumnTop, right = f.Canvas.x - ColumnInset;
            return new UiBox(right - Width, top, Width, Mathf.Min(Height, EconomyUiLayout.RoomBelow(f, top, right - Width, right) - top));
        }

        /// <summary>
        /// The portrait sheet, across the screen between the margins. Collapsed, its header row stands just above the HUD's
        /// bottom blocks (the legend and the preset bar), where the view's own screen ends. Open, it runs from there down to
        /// the bottom margin over those blocks, as the economy's portrait sheets do, so it covers none of the view; the header
        /// rises only where that leaves less than <see cref="SheetRoom"/>. Never above the title and the year chip.
        /// </summary>
        UiBox Sheet(HudFrame f)
        {
            float floor = Mathf.Max(EconomyUiLayout.TitleBottom(f), EconomyControls.Occupied.Bottom) + f.Gap, bottom = EconomyUiLayout.SheetBottom(f);
            float header = Mathf.Max(floor, EconomyUiLayout.RoomBelow(f, floor, f.Margin, f.Canvas.x - f.Margin) - HeaderHeight);
            float top = open ? Mathf.Max(floor, Mathf.Min(header, bottom - SheetRoom)) : header;
            return new UiBox(f.Margin, top, f.Canvas.x - 2 * f.Margin, open ? bottom - top : HeaderHeight);
        }

        /// <summary>The header, the controls' marks and the shown tab's rows and blocks for the year, the pin and the selection.</summary>
        void Refresh(Politics model, float year, float pinned, string selected)
        {
            usedRows = usedBlocks = leftOut = 0;
            flow.Clear();
            moreWord = "more";
            if (model == null) yearLine.text = "No politics data is loaded.";
            else if (float.IsNaN(year)) yearLine.text = "The view has no year yet.";
            else
            {
                // in portrait the year stands alone between its steps, and the follow button in the sheet says whether it is pinned
                sb.Clear().Append("<b>").Append(When(year)).Append("</b>");
                if (!placedPortrait)
                {
                    sb.Append("  <size=85%><alpha=#B0>").Append(model.Name).Append(" · ").Append(float.IsNaN(pinned) ? "the year chip's year" : "pinned")
                        .Append("</size>");
                }

                yearLine.text = sb.ToString();
                MarkControls(model, year, pinned);
                switch (tab)
                {
                    case Tab.Power: PowerTab(model, year); break;
                    case Tab.Offices: OfficesTab(model, year); break;
                    case Tab.Army: ArmyTab(model, year); break;
                    case Tab.Trade: TradeTab(model, year); break;
                    default: MomentsTab(model, year, pinned); break;
                }
            }

            for (int i = usedRows; i < rows.Count; i++) rows[i].Button.gameObject.SetActive(false);
            for (int i = usedBlocks; i < blocks.Count; i++) blocks[i].gameObject.SetActive(false);
            for (int i = 0; i < usedRows; i++)
            {
                Row r = rows[i];
                Mark(r.Back, r.Label, r.Id != null ? r.Id == selected : AtDate(r.Date, pinned), false);
            }

            laidOut = false;
        }

        /// <summary>The steps' years (off past the span's ends), the pin's state, the decade shown and the tab shown.</summary>
        void MarkControls(Politics model, float year, float pinned)
        {
            int y = Mathf.FloorToInt(year);
            Enable(prev, prevLabel, "< " + (y - 1).ToString(LandFacts.Ci), y - .5f >= model.From);
            Enable(next, nextLabel, (y + 1).ToString(LandFacts.Ci) + " >", y + 1.5f <= model.To);
            bool following = float.IsNaN(pinned);
            followLabel.text = following ? "Following the year chip" : "Follow the year chip";
            Mark(followBack, followLabel, following, true);
            int decade = Mathf.FloorToInt(year / 10) * 10;
            foreach (var (_, back, label, d) in decades) Mark(back, label, d == decade, true);
            for (int i = 0; i < tabs.Count; i++) Mark(tabs[i].back, tabs[i].label, i == (int)tab, true);
        }

        // ------------------------------------------------------------------ the tabs

        /// <summary>Who holds power: the president, each party's power by the stated rule with its seats and the popular vote
        /// it won, the chambers' rows with their dates, the election the votes come from, and the rule in words.</summary>
        void PowerTab(Politics p, float t)
        {
            Politics.Term president = p.PresidentAt(t);
            sb.Clear().Append("<b>President</b>  ");
            if (president == null) sb.Append("none in the data");
            else sb.Append(president.Name).Append(" · ").Append(Swatch(p, president.Party)).Append(PartyName(p, president.Party)).Append(" · from ").Append(When(president.From));
            AddRow(PresidentId, sb.ToString());

            Politics.Seats house = p.HouseAt(t), senate = p.SenateAt(t);
            Politics.Election vote = p.ElectionAt(t);
            foreach (Politics.Party party in p.Parties)
            {
                // power as the view's label gives it ("82% of power"); a rule the data does not state, and a share an election
                // lacks, are not written as numbers
                float power = p.Power(party.Id, t), share = vote != null ? vote.ShareOf(party.Id) : float.NaN;
                sb.Clear().Append(Swatch(p, party.Id)).Append("<b>").Append(party.Short ?? party.Name).Append("</b>  ");
                if (float.IsNaN(power)) sb.Append("power not stated");
                else sb.Append(Mathf.RoundToInt(power * 100).ToString(LandFacts.Ci)).Append("% of power");
                sb.Append("\n<alpha=#B0>House ").Append(house != null ? LandFacts.Count(house.Of(party.Id)) : "no row")
                    .Append(" · Senate ").Append(senate != null ? LandFacts.Count(senate.Of(party.Id)) : "no row").Append(" · ");
                if (vote == null) sb.Append("votes not measured");
                else
                {
                    sb.Append(LandFacts.Millions(p.Represented(party.Id, t))).Append(" votes");
                    if (!float.IsNaN(share)) sb.Append(", ").Append(share.ToString("0.##", LandFacts.Ci)).Append('%');
                }

                AddRow(party.Id, sb.ToString());
            }

            sb.Clear();
            Chamber(p, "House", house);
            sb.Append('\n');
            Chamber(p, "Senate", senate);
            AddBlock(sb.ToString(), false);
            sb.Clear();
            if (vote != null)
            {
                sb.Append("Votes: the ").Append(vote.Presidential ? "presidential" : "House").Append(" election of ").Append(When(vote.Date))
                    .Append(", standing until the next national election.");
            }
            else if (p.Elections.Count > 0) sb.Append("Votes: none measured before the election of ").Append(When(p.Elections[0].Date)).Append('.');
            else sb.Append("Votes: none measured.");
            AddBlock(sb.ToString(), false);
            if (!string.IsNullOrEmpty(p.PowerRule)) AddBlock(p.PowerRule, false);
        }

        /// <summary>A chamber's row in words: from when, which Congress, its seats, its majority, others and vacancies.</summary>
        void Chamber(Politics p, string name, Politics.Seats s)
        {
            sb.Append(name);
            if (s == null)
            {
                sb.Append(": no row in the data");
                return;
            }

            sb.Append(" from ").Append(When(s.From)).Append(" (").Append(Ordinal(s.Congress)).Append(" Congress): ").Append(LandFacts.Count(s.Total)).Append(" seats");
            Politics.Party majority = p.PartyById(s.Majority);
            if (majority != null) sb.Append(", ").Append(Adjective(majority)).Append(" majority");
            if (s.Other > 0) sb.Append(", ").Append(LandFacts.Count(s.Other)).Append(" other");
            if (s.Vacant > 0) sb.Append(", ").Append(LandFacts.Count(s.Vacant)).Append(" vacant");
        }

        /// <summary>The cascade: the president, then each office held at the year in rank order (the vice president, the
        /// Speaker, the Senate's leader, State, the Treasury, Defense or War and the Navy, the Joint Chiefs' chairman).</summary>
        void OfficesTab(Politics p, float t)
        {
            Politics.Term president = p.PresidentAt(t);
            sb.Clear();
            AppendTitle("President");
            if (president == null) sb.Append("none in the data");
            else sb.Append(president.Name).Append(" · ").Append(Swatch(p, president.Party)).Append(PartyName(p, president.Party));
            AddRow(PresidentId, sb.ToString());
            foreach (Politics.Office o in p.OfficesAt(t))
            {
                sb.Clear();
                AppendTitle(o.Title);
                if (o.Vacant) sb.Append("vacant from ").Append(When(o.From));
                else
                {
                    sb.Append(o.Name);
                    if (o.Acting) sb.Append(" (acting)");
                    sb.Append(" · ").Append(o.Military ? "military" : OfficeParty(p, o, president));
                }

                AddRow(o.Id, sb.ToString());
            }

            moreWord = "lower in rank";
        }

        /// <summary>
        /// The standing army as relative power, not a head count: the United States' share of the world's military spending
        /// with its warheads and ships; the main rival's and China's; the alliances; the walls Jev judged; and the bases the
        /// shares and counts were measured on, which change over the years.
        /// </summary>
        void ArmyTab(Politics p, float t)
        {
            sb.Clear().Append("<b>").Append(p.Name).Append("</b>  ");
            Share(p, "usa", t);
            sb.Append("\n<alpha=#B0>");
            Tally(p.Warheads, "usa", t, "warheads");
            sb.Append(" · ");
            Tally(p.Navy, "usShips", t, "ships");
            AddRow(ArmyId, sb.ToString());

            // the rival the view draws, by its rule (the Soviet Union until its dissolution, then Russia), so the row's highlight
            // id is the drawn one even at the dissolution's own date; Politics.Warheads counts the Soviet Union's under russia
            bool soviet = t < HillPoliticsLayer.SovietEnd(p);
            string rival = soviet ? "ussr" : "russia";
            sb.Clear().Append("<b>").Append(soviet ? "Soviet Union" : "Russia").Append("</b>  ");
            Share(p, rival, t);
            sb.Append("\n<alpha=#B0>");
            Tally(p.Warheads, "russia", t, "warheads");
            AddRow(rival, sb.ToString());

            sb.Clear().Append("<b>China</b>  ");
            Share(p, "china", t);
            sb.Append("\n<alpha=#B0>");
            Tally(p.Warheads, "china", t, "warheads");
            sb.Append(" · ");
            Tally(p.Navy, "chinaShips", t, "ships");
            AddRow("china", sb.ToString());

            sb.Clear();
            Alliances(p, t);
            AddBlock(sb.ToString(), false);
            sb.Clear();
            Walls(p, t);
            AddBlock(sb.ToString(), false);
            sb.Clear();
            Basis(p.MilitaryShare, "usa", t, "Spending shares: ");
            Basis(p.Navy, "usShips", t, "US ships: ");
            Basis(p.Navy, "chinaShips", t, "China's ships: ");
            if (sb.Length > 0) AddBlock(sb.ToString(), false);
        }

        /// <summary>NATO's members after the last accession by the year, with its share of the world's military spending, and the
        /// Warsaw Pact's share while it stood.</summary>
        void Alliances(Politics p, float t)
        {
            Politics.Accession last = null;
            foreach (Politics.Accession a in p.Nato)
            {
                if (a.Date > t) break;
                last = a;
            }

            if (last != null)
            {
                sb.Append("NATO: ").Append(LandFacts.Count(last.Members)).Append(" members from ").Append(When(last.Date)).Append(", ");
                Share(p, "nato", t);
            }
            else if (p.Nato.Count > 0) sb.Append("NATO: not founded until ").Append(When(p.Nato[0].Date));
            else sb.Append("NATO: not in the data");
            if (!p.WarsawPactAt(t)) return;
            sb.Append("\nWarsaw Pact, from ").Append(When(p.WarsawFrom)).Append(": ");
            Share(p, "wto", t);
        }

        /// <summary>The walls of the army's advantage Jev judged at the benchmark nearest the year (<see cref="Politics.WallsAt"/>,
        /// as the view draws them), each in its own reading with its year, or in the source's name and its level where the
        /// research gives no reading (a level not judged is said so, never written as a number); where none stands near the
        /// year the view draws none, and the panel says so.</summary>
        void Walls(Politics p, float t)
        {
            int n = 0;
            foreach (Politics.Wall w in p.WallsAt(t))
            {
                if (n++ == 0) sb.Append("Walls, as Jev judged them (0-4):");
                sb.Append('\n');
                if (w.Reading != null) sb.Append(w.Reading);
                else if (float.IsNaN(w.Height)) sb.Append(HillHistoryLayer.SourceWord(w.Source)).Append(": not judged");
                else sb.Append(HillHistoryLayer.SourceWord(w.Source)).Append(": wall ").Append(w.Height.ToString("0.0", LandFacts.Ci)).Append(" of 4");
                sb.Append(" (").Append(When(w.T)).Append(')');
            }

            if (n == 0) sb.Append(p.Walls.Count == 0 ? "Walls: none judged yet, so none are drawn." : "Walls: none judged near this year, so none are drawn.");
        }

        /// <summary>The US balance in goods and services at the year, then each partner's goods balance at its benchmark nearest
        /// the year, in the research's order. A partner not measured near the year is not drawn, so its row highlights nothing
        /// (a highlight of it would only dim the whole view).</summary>
        void TradeTab(Politics p, float t)
        {
            Politics.TradeYear y = p.TradeAt(t);
            sb.Clear().Append("<b>").Append(p.Name);
            if (y == null)
            {
                sb.Append("</b>: trade in goods and services not measured");
                if (p.Trade.Count > 0) Around(p.Trade[0].T, p.Trade[p.Trade.Count - 1].T, t);
            }
            else
            {
                sb.Append(", ").Append(When(y.T)).Append("</b>: ").Append(Balance(y.Balance)).Append(" in goods and services")
                    .Append("\nexports ").Append(Money(y.Exports)).Append(" · imports ").Append(Money(y.Imports))
                    .Append("\ngoods: ").Append(Balance(y.Goods)).Append(" · services: ").Append(Balance(y.Services));
            }

            AddBlock(sb.ToString(), true);
            sb.Clear().Append("Its trade in goods with each partner at the benchmark nearest ").Append(When(t)).Append(" (within ")
                .Append(PartnerYears.ToString("0", LandFacts.Ci)).Append(" years), in current dollars.");
            AddBlock(sb.ToString(), false);
            foreach (KeyValuePair<string, List<Politics.PartnerYear>> kv in p.Partners)
            {
                Politics.PartnerYear row = NearestPartner(kv.Value, t);
                sb.Clear().Append("<b>").Append(PartnerNames.TryGetValue(kv.Key, out string name) ? name : kv.Key).Append("</b>  ");
                if (row != null)
                {
                    sb.Append(Balance(row.Balance)).Append(" (").Append(When(row.T)).Append(")<alpha=#B0>: exports ").Append(Money(row.Exports))
                        .Append(", imports ").Append(Money(row.Imports));
                }
                else
                {
                    sb.Append("<alpha=#B0>not measured");
                    if (kv.Value.Count > 0) Around(kv.Value[0].T, kv.Value[kv.Value.Count - 1].T, t);
                }

                AddRow(row != null ? PartnerId(kv.Key) : null, sb.ToString());
            }
        }

        /// <summary>The key moments within a few years of the year (the eight nearest it), newest first, those after it dimmed;
        /// the one the view is pinned to is told in the research's words above them. The note says where the view draws them,
        /// as sparks on the government's track (the presidency through time) and not on a party's stream, so a moment is not
        /// read as one party's act. Only moments within the data's span are listed: the view draws none before it, and a row's
        /// pin to an earlier date (Bretton Woods, 1944) would land on the span's first day, its moment never marked.</summary>
        void MomentsTab(Politics p, float t, float pinned)
        {
            moments.Clear();
            foreach (Politics.Moment m in p.MomentsBetween(Mathf.Max(t - MomentYears, p.From), Mathf.Min(t + MomentYears, p.To))) moments.Add(m);
            if (moments.Count > MaxMoments)
            {
                moments.Sort((a, b) =>
                {
                    int nearer = Mathf.Abs(a.Date - t).CompareTo(Mathf.Abs(b.Date - t));
                    return nearer != 0 ? nearer : a.Date.CompareTo(b.Date);
                });
                leftOut = moments.Count - MaxMoments;
                moments.RemoveRange(MaxMoments, leftOut);
            }

            moments.Sort((a, b) => b.Date.CompareTo(a.Date));
            sb.Clear();
            if (moments.Count == 0) sb.Append("No key moment in the research within ").Append(MomentYears).Append(" years of ").Append(When(t)).Append('.');
            else
            {
                sb.Append("Within ").Append(MomentYears).Append(" years of ").Append(When(t))
                    .Append(", newest first; those after it dimmed. In the view each is a spark on the government's track (the presidency")
                    .Append(" through time), not on a party. Pick one to show the view at its date.");
            }

            AddBlock(sb.ToString(), false);
            foreach (Politics.Moment m in moments)
            {
                if (!AtDate(m.Date, pinned) || string.IsNullOrEmpty(m.Claim)) continue;
                AddBlock(m.Claim, true);
                break;
            }

            foreach (Politics.Moment m in moments)
            {
                bool later = m.Date > t + 1e-3f;
                sb.Clear().Append(later ? "<alpha=#80>" : "<alpha=#A8>").Append(When(m.Date)).Append(" · ").Append(m.Kind)
                    .Append(later ? "  " : "<alpha=#FF>  ").Append(m.Title);
                AddRow(null, sb.ToString(), m.Date);
            }

            moreWord = MoreMoments;
        }

        // ------------------------------------------------------------------ readings

        /// <summary>Appends a spender's share of the world's military spending at the measured point nearest the year, with its
        /// year, or where it was not measured.</summary>
        void Share(Politics p, string who, float t)
        {
            p.MilitaryShare.TryGetValue(who, out List<Politics.Point> series);
            if (double.IsNaN(Politics.At(series, t, out Politics.Point at)))
            {
                sb.Append("share of world military spending ");
                Unmeasured(series, t);
                return;
            }

            sb.Append(at.V.ToString("0.#", LandFacts.Ci)).Append("% of world military spending (").Append(When(at.T)).Append(')');
        }

        /// <summary>Appends a count (warheads, ships) at the measured point nearest the year, with its date, or where it was not
        /// measured.</summary>
        void Tally(Dictionary<string, List<Politics.Point>> table, string key, float t, string noun)
        {
            table.TryGetValue(key, out List<Politics.Point> series);
            if (double.IsNaN(Politics.At(series, t, out Politics.Point at)))
            {
                sb.Append(noun).Append(' ');
                Unmeasured(series, t);
                return;
            }

            // one is singular: China's first warhead (1964)
            long n = (long)Math.Round(at.V);
            sb.Append(LandFacts.Count(n)).Append(' ').Append(noun, 0, n == 1 && noun.EndsWith("s", StringComparison.Ordinal) ? noun.Length - 1 : noun.Length)
                .Append(" (").Append(When(at.T)).Append(')');
        }

        /// <summary>Appends where a series has no measure at the year: before its first point, after its last, or in a gap
        /// between two, where the model holds nothing rather than a guess.</summary>
        void Unmeasured(List<Politics.Point> series, float t)
        {
            sb.Append("not measured");
            if (series == null || series.Count == 0) return;
            int i = series.Count - 1;
            while (i > 0 && series[i].T > t) i--;
            if (t < series[0].T) Span(series[0].T, float.NaN);
            else if (i == series.Count - 1) Span(float.NaN, series[i].T);
            else sb.Append(" between ").Append(When(series[i].T)).Append(" and ").Append(When(series[i + 1].T));
        }

        /// <summary>Appends, after "not measured", where the measures that exist begin or end: "(first in 1955)" for a year
        /// before them, "(last in 2023)" for one after; an annual measure stands at the middle of its year, so a year's own
        /// first months come before it.</summary>
        void Span(float first, float last)
        {
            if (!float.IsNaN(first)) sb.Append(" (first in ").Append(When(first)).Append(')');
            else sb.Append(" (last in ").Append(When(last)).Append(')');
        }

        /// <summary>The same for a yearly or benchmark series (trade) whose first and last measures are given, when the year lies
        /// outside them.</summary>
        void Around(float first, float last, float t)
        {
            if (t < first) Span(first, float.NaN);
            else if (t > last) Span(float.NaN, last);
        }

        /// <summary>Appends a line on what the point shown measures, where the research's series change basis over the years
        /// (SIPRI's constant prices or exchange rates, the Navy's counting rules).</summary>
        void Basis(Dictionary<string, List<Politics.Point>> table, string key, float t, string lead)
        {
            if (!table.TryGetValue(key, out List<Politics.Point> series) || double.IsNaN(Politics.At(series, t, out Politics.Point at)) || string.IsNullOrEmpty(at.Basis)) return;
            if (sb.Length > 0) sb.Append('\n');
            sb.Append(lead).Append(at.Basis).Append('.');
        }

        /// <summary>A partner's benchmark nearest the year, within <see cref="PartnerYears"/>, the earlier of two as near (as
        /// <see cref="Politics.At"/> cites); null where none is that near.</summary>
        static Politics.PartnerYear NearestPartner(List<Politics.PartnerYear> series, float t)
        {
            Politics.PartnerYear best = null;
            float gap = PartnerYears;
            foreach (Politics.PartnerYear y in series)
            {
                float d = Mathf.Abs(y.T - t);
                if (d > gap || best != null && d >= gap) continue;
                gap = d;
                best = y;
            }

            return best;
        }

        /// <summary>The id the view highlights a trading partner by: China the partner is "china-trade", apart from China the
        /// rival.</summary>
        static string PartnerId(string key) => key == "china" ? "china-trade" : key;

        /// <summary>An officeholder's party as the research gives it, after the key of the colour the view draws the holder in:
        /// stated; the chamber majority's; or, for an appointee whose party the research takes from the appointing president or
        /// does not record, the administration's, worded as serving in it and never as a member (as the view's label does, for
        /// Morgenthau and Forrestal under Truman too).</summary>
        static string OfficeParty(Politics p, Politics.Office o, Politics.Term president)
        {
            bool appointee = o.PartyBasis == "appointing president";
            Politics.Party party = p.PartyById(o.Party ?? (appointee ? president?.Party : null));
            if (party == null) return "party not recorded";
            if (appointee) return Swatch(p, party.Id) + "in the " + Adjective(party) + " administration";
            return Swatch(p, party.Id) + (o.PartyBasis == "majority of the chamber" ? "of the " + Adjective(party) + " majority" : party.Name);
        }

        static string PartyName(Politics p, string id) => p.PartyById(id)?.Name ?? "no party recorded";

        /// <summary>A party's key before its name: a square in the colour the view draws the party in (sRGB-encoded for text, as
        /// the legend's swatches are), so the panel's parties read in the view's colours, left before right as the view
        /// stands them; none for an id the data has no party of. The colour is the palette's role itself
        /// (<see cref="HillPoliticsLayer.PartyColor"/> returns it), read at every refresh and never kept, so a change of the
        /// role reaches the panel as it reaches the view and the legend.</summary>
        static string Swatch(Politics p, string party) =>
            p.PartyById(party) == null ? "" : "<color=#" + UiFactory.Hex(EconomyStyle.Roles.Encoded(HillPoliticsLayer.PartyColor(party))) + ">■</color> ";

        /// <summary>A party's name as an adjective ("Democratic" of "Democratic Party").</summary>
        static string Adjective(Politics.Party party) =>
            party.Name != null && party.Name.EndsWith(" Party", StringComparison.Ordinal) ? party.Name.Substring(0, party.Name.Length - 6) : party.Short;

        /// <summary>A balance in millions of dollars, written in billions: "deficit $901B", "surplus $3.5B".</summary>
        static string Balance(double millions) =>
            double.IsNaN(millions) ? "not measured" : millions < 0 ? "deficit " + Money(-millions) : millions > 0 ? "surplus " + Money(millions) : "balanced";

        static string Money(double millions) => double.IsNaN(millions) ? "not measured" : LandFacts.Money(millions / MillionsPerBillion);

        static string Ordinal(int n)
        {
            int h = n % 100, d = n % 10;
            return n.ToString(LandFacts.Ci) + (h >= 11 && h <= 13 ? "th" : d == 1 ? "st" : d == 2 ? "nd" : d == 3 ? "rd" : "th");
        }

        /// <summary>
        /// A date the research gives as a decimal year: the bare year where it stands at the middle of its year (an annual
        /// measure, or a moment the research dates by its year alone: Tools/politics/build.py), else its month and year, so no
        /// month is claimed that the research did not give. The year shown is written the same way: the chip's year, a step and
        /// a decade all stand at the middle of their year (<see cref="HillPoliticsLayer.Year"/>), so they read "2021", and a
        /// moment's pin reads its month.
        /// </summary>
        static string When(float t)
        {
            if (float.IsNaN(t) || t < 1 || t >= 9999) return "?";
            int y = Mathf.FloorToInt(t);
            double f = t - y;
            if (Math.Abs(f - .5) < 1e-4) return y.ToString(LandFacts.Ci);
            int days = DateTime.IsLeapYear(y) ? 366 : 365;
            return new DateTime(y, 1, 1).AddDays(Math.Min(days - 1, Math.Round(f * days))).ToString("MMM yyyy", LandFacts.Ci);
        }

        /// <summary>The tab that lists a highlighted id: the president's in either of the two that name him, then the parties',
        /// the army's and rivals', the offices' and the partners'; the shown tab for an id none lists.</summary>
        static Tab TabOf(Politics model, string id, Tab current)
        {
            if (id == PresidentId) return current == Tab.Offices ? Tab.Offices : Tab.Power;
            if (model == null) return current;
            if (model.PartyById(id) != null) return Tab.Power;
            if (id == ArmyId || Array.IndexOf(Rivals, id) >= 0) return Tab.Army;
            foreach (Politics.Office o in model.Offices)
            {
                if (o.Id == id) return Tab.Offices;
            }

            foreach (string key in model.Partners.Keys)
            {
                if (PartnerId(key) == id) return Tab.Trade;
            }

            return current;
        }

        static bool Same(float a, float b) => a == b || float.IsNaN(a) && float.IsNaN(b);

        /// <summary>Whether the view is pinned to this date (a key moment's row marks itself so).</summary>
        static bool AtDate(float date, float pinned) => !float.IsNaN(date) && !float.IsNaN(pinned) && Mathf.Abs(date - pinned) < 1e-3f;

        // ------------------------------------------------------------------ the layout

        void AppendTitle(string title) => sb.Append("<alpha=#A8><size=90%>").Append(title).Append("</size><alpha=#FF>  ");

        void AddRow(string id, string text, float date = float.NaN)
        {
            if (usedRows >= rows.Count)
            {
                leftOut++;
                return;
            }

            Row r = rows[usedRows++];
            r.Id = id;
            r.Date = date;
            r.Label.text = text;
            flow.Add(new Item((RectTransform)r.Button.transform, r.Label, true));
        }

        /// <summary>A block of text: bright for a reading (the US balance, a moment's claim), dim for a note on what is shown.</summary>
        void AddBlock(string text, bool bright)
        {
            if (usedBlocks >= blocks.Count) return;
            TextMeshProUGUI b = blocks[usedBlocks++];
            b.text = text;
            b.color = bright ? Ink : Dim;
            b.fontSize = bright ? 10.5f : 9.5f;
            flow.Add(new Item((RectTransform)b.transform, b, false));
        }

        /// <summary>
        /// Places the controls across the panel's width and the shown tab top down by their measured heights, as far as the
        /// panel's room goes: what does not fit is hidden and its rows counted at the foot ("+3 earlier"). The landscape
        /// column then ends under the last thing shown, so a short tab leaves the column below it to the view; the open
        /// portrait sheet keeps its whole height, so it covers the HUD's bottom blocks whole rather than cut a row of the
        /// preset bar, and the collapsed one is its header row alone.
        /// </summary>
        void Layout()
        {
            laidOut = true;
            float w = panel.sizeDelta.x - 2 * Pad;
            bool made = rows.Count > 0;
            if (placedPortrait) HeaderRow(w, made);
            else
            {
                Place(heading.rectTransform, Pad, 10, w, 22);
                Place(yearLine.rectTransform, Pad, 32, w, 20);
            }

            if (!made)
            {
                more.gameObject.SetActive(false);
                Resize(placedPortrait ? HeaderHeight : 58);
                return;
            }

            bool sheet = !placedPortrait || open;
            toggle.gameObject.SetActive(placedPortrait);
            body.gameObject.SetActive(sheet);
            if (!sheet)
            {
                Resize(HeaderHeight);
                return;
            }

            // in portrait the steps stand in the header row: the follow button has the controls' row to itself, and it and all
            // below it rise by the height the one-row header saves over the title and year lines
            float rise = placedPortrait ? ControlsTop - HeaderHeight : 0, y = ContentTop - rise;
            if (placedPortrait) Place((RectTransform)follow.transform, Pad, ControlsTop - rise, w, 24);
            else
            {
                Place((RectTransform)prev.transform, Pad, ControlsTop, StepWidth, 24);
                Place((RectTransform)next.transform, Pad + StepWidth + Gap, ControlsTop, StepWidth, 24);
                Place((RectTransform)follow.transform, Pad + 2 * (StepWidth + Gap), ControlsTop, w - 2 * (StepWidth + Gap), 24);
            }

            float dw = (w - Gap * (decades.Count - 1)) / Mathf.Max(1, decades.Count);
            for (int i = 0; i < decades.Count; i++) Place((RectTransform)decades[i].button.transform, Pad + i * (dw + Gap), DecadesTop - rise, dw, 22);
            float tw = (w - Gap * (tabs.Count - 1)) / tabs.Count;
            for (int i = 0; i < tabs.Count; i++) Place((RectTransform)tabs[i].button.transform, Pad + i * (tw + Gap), TabsTop - rise, tw, 24);

            float bottom = maxHeight - 10;
            int hidden = leftOut;
            bool full = false;
            for (int i = 0; i < flow.Count; i++)
            {
                Item it = flow[i];
                it.Rect.gameObject.SetActive(!full);
                if (!full)
                {
                    float h = it.Row
                        ? Mathf.Max(RowMin, it.Text.GetPreferredValues(it.Text.text, w - 2 * RowInset, 0).y + 8)
                        : it.Text.GetPreferredValues(it.Text.text, w, 0).y;
                    // the count at the foot needs its line unless this is the last thing and nothing else is left out
                    float reserve = i == flow.Count - 1 && hidden == 0 ? 0 : MoreHeight;
                    if (y + h <= bottom - reserve)
                    {
                        Place(it.Rect, Pad, y, w, h);
                        y += h + (it.Row ? Gap : 2 * Gap);
                        continue;
                    }

                    full = true;
                    it.Rect.gameObject.SetActive(false);
                }

                if (it.Row) hidden++;
            }

            more.gameObject.SetActive(hidden > 0);
            if (hidden > 0)
            {
                more.text = "+" + hidden.ToString(LandFacts.Ci) + " " + moreWord;
                Place(more.rectTransform, Pad, y, w, MoreHeight);
                y += MoreHeight;
            }

            Resize(placedPortrait ? maxHeight : y + 6);
        }

        /// <summary>The portrait header row: the title at the left; at the right the year between its steps and the button that
        /// opens and closes the sheet. Before the data, the year's line takes the right half to say there is none.</summary>
        void HeaderRow(float w, bool made)
        {
            float x = Pad + w * .5f;
            if (made)
            {
                toggleLabel.text = open ? "Hide ▼" : "More ►";
                Mark(toggleBack, toggleLabel, open, true);
                x = Pad + w - ToggleWidth;
                Place((RectTransform)toggle.transform, x, HeaderTop, ToggleWidth, 24);
                x -= 2 * Gap + HeaderStep;
                Place((RectTransform)next.transform, x, HeaderTop, HeaderStep, 24);
                x -= Gap + YearWidth;
                Place(yearLine.rectTransform, x, HeaderTop, YearWidth, 24);
                x -= Gap + HeaderStep;
                Place((RectTransform)prev.transform, x, HeaderTop, HeaderStep, 24);
                x -= 2 * Gap;
            }
            else Place(yearLine.rectTransform, x, HeaderTop, Pad + w - x, 24);

            Place(heading.rectTransform, Pad, HeaderTop, x - Pad, 24);
        }

        void Resize(float height) => panel.sizeDelta = new Vector2(panel.sizeDelta.x, Mathf.Min(maxHeight, height));

        /// <summary>A button marked as the one chosen: the light slate fill under near-black text the other panels use; the
        /// controls' labels turn bold too, a row's keep their weight so their lines do not reflow.</summary>
        static void Mark(Image back, TextMeshProUGUI label, bool active, bool bold)
        {
            back.color = active ? RegimePanel.ActiveFill : UiFactory.ButtonColor;
            label.color = active ? RegimePanel.ActiveText : Ink;
            if (bold) label.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
        }

        /// <summary>A step's label and whether it can be pressed: past the span's ends it is greyed out.</summary>
        static void Enable(Button button, TextMeshProUGUI label, string text, bool on)
        {
            label.text = text;
            button.interactable = on;
            label.color = on ? Ink : Dim;
        }

        /// <summary>A button's label, found even under the collapsed portrait sheet.</summary>
        static TextMeshProUGUI Label(Button b) => b.GetComponentInChildren<TextMeshProUGUI>(true);

        static TextMeshProUGUI Text(Transform parent, string name, float size, Color color) => UiFactory.Text(parent, name, "", size, color);

        static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }

        static void Place(RectTransform r, float x, float y, float width, float height)
        {
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1);
            r.anchoredPosition = new Vector2(x, -y);
            r.sizeDelta = new Vector2(width, height);
        }
    }
}
