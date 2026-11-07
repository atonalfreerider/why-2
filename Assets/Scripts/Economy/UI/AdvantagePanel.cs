using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Why.Economy.Land;
using Why.Economy.Layers;
using Why.Economy.Model;
using Why.UI;

namespace Why.Economy.UI
{
    /// <summary>
    /// The corporate biosphere's panel (<see cref="HillAdvantageLayer"/>): where the camera looks (the 2026 network or its
    /// history), the worlds (now, 2030 on the most likely course, the strategist's scenarios) and the simulated years with
    /// play; the world's reading and the year's events (who raised capital, whose shell burst, who was absorbed, who went
    /// extinct); and the selected company in full: its balance sheet as two bars (what it owns, against what it owes and
    /// its equity) beside its market value, its P/E, its owners, its rivers. A card follows the pointer. Buttons, not keys,
    /// so it works by pinch in the headset.
    /// </summary>
    [GraphScenes(EconomyLayouts.HillsScene)]
    public sealed class AdvantagePanel : GraphModule
    {
        const float Width = 450, Height = 860, MethodHeight = 22, ModeTop = 78, WorldsTop = 106, BarHeight = 12;
        GraphRoot root;
        RectTransform panel, methodButton, bars;
        TextMeshProUGUI reading, detail, method, methodLabel, barLabel;
        HudTooltip card;
        readonly List<(Image background, TextMeshProUGUI label, string id)> worlds = new List<(Image, TextMeshProUGUI, string)>();
        readonly List<(Image background, TextMeshProUGUI label, int year)> years = new List<(Image, TextMeshProUGUI, int)>();
        readonly List<(Image background, TextMeshProUGUI label, bool history)> modes = new List<(Image, TextMeshProUGUI, bool)>();
        Image playBack; TextMeshProUGUI playLabel;
        readonly List<GameObject> segments = new List<GameObject>();
        string shownKey, laidOut, hoverShown;
        float readingTop = 160;
        bool methodOpen, shownPlaying;

        public override void Init(GraphRoot graphRoot)
        {
            root = graphRoot;
            Canvas canvas = UiFactory.CreateCanvas("The present as a contest", 47, transform);
            panel = UiFactory.Panel(canvas.transform, "Contest", new Color(.02f, .03f, .05f, .94f)).rectTransform;
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(1, 1);
            panel.anchoredPosition = new Vector2(-18, -176); panel.sizeDelta = new Vector2(Width, Height);
            var title = Text("Title", 15, 12, 10, Width - 24, 22); title.text = "<b>THE PRESENT AS A CONTEST</b>";
            var note = Text("Note", 10.5f, 12, 32, Width - 24, 44); note.textWrappingMode = TextWrappingModes.Normal; note.color = new Color(.7f, .76f, .84f);
            note.text = "Each company is its balance sheet: equity at the core, what it owns above, what it owes below, inside a shell as large as its market value. Height is market position. Gold circles: its owners.";
            reading = Text("Reading", 11, 12, readingTop, Width - 24, 300); reading.textWrappingMode = TextWrappingModes.Normal; reading.overflowMode = TextOverflowModes.Overflow;
            bars = new GameObject("Balance bars", typeof(RectTransform)).GetComponent<RectTransform>();
            bars.SetParent(panel, false);
            barLabel = UiFactory.Text(bars, "Bar legend", "", 9.5f, new Color(.74f, .79f, .86f));
            barLabel.textWrappingMode = TextWrappingModes.Normal;
            detail = Text("Detail", 11, 12, readingTop + 300, Width - 24, 300); detail.textWrappingMode = TextWrappingModes.Normal; detail.overflowMode = TextOverflowModes.Overflow;
            var m = UiFactory.Button(panel, "Method", "", 10.5f, () => { methodOpen = !methodOpen; laidOut = null; });
            methodButton = (RectTransform)m.transform; methodLabel = m.GetComponentInChildren<TextMeshProUGUI>();
            method = Text("Method", 9.5f, 12, Height - 128, Width - 24, 118); method.textWrappingMode = TextWrappingModes.Normal; method.overflowMode = TextOverflowModes.Overflow;
            method.color = new Color(.74f, .79f, .86f);
            card = new HudTooltip(canvas.transform);
        }

        void MakeButtons(Advantage data)
        {
            float half = (Width - 24 - 4) / 2;
            foreach (bool history in new[] { false, true })
            {
                bool h = history;
                var b = UiFactory.Button(panel, history ? "History" : "Network", history ? "History, 1947 to now" : "The 2026 network", 11, () => HillAdvantageLayer.ShowHistory(h));
                Place((RectTransform)b.transform, 12 + (history ? half + 4 : 0), ModeTop, half, 22);
                modes.Add(((Image)b.targetGraphic, b.GetComponentInChildren<TextMeshProUGUI>(), history));
            }

            var list = new List<(string id, string label)> { (Advantage.Now, "Now (2026)"), (Advantage.Y2030, "Most likely") };
            foreach (var s in data.Scenarios) list.Add((s.Id, s.Title));
            const int perRow = 3;
            float gap = 4, w = (Width - 24 - gap * (perRow - 1)) / perRow;
            for (int i = 0; i < list.Count; i++)
            {
                var (id, label) = list[i];
                var b = UiFactory.Button(panel, "World " + id, label, 10.5f, () => HillAdvantageLayer.SetScenario(id));
                Place((RectTransform)b.transform, 12 + (i % perRow) * (w + gap), WorldsTop + (i / perRow) * 26, w, 22);
                worlds.Add(((Image)b.targetGraphic, b.GetComponentInChildren<TextMeshProUGUI>(), id));
            }

            float yearsTop = WorldsTop + ((list.Count + perRow - 1) / perRow) * 26 + 4, yw = (Width - 24 - 5 * gap - 80) / 5;
            for (int k = 0; k < 5; k++)
            {
                int year = 2026 + k;
                var b = UiFactory.Button(panel, "Year " + year, year.ToString(), 10.5f, () => { HillAdvantageLayer.Playing = false; HillAdvantageLayer.SetYear(year); });
                Place((RectTransform)b.transform, 12 + k * (yw + gap), yearsTop, yw, 22);
                years.Add(((Image)b.targetGraphic, b.GetComponentInChildren<TextMeshProUGUI>(), year));
            }

            var play = UiFactory.Button(panel, "Play", "Play", 10.5f, () =>
            {
                if (!HillAdvantageLayer.Playing && HillAdvantageLayer.Year >= 2030) HillAdvantageLayer.SetYear(2026);
                HillAdvantageLayer.Playing = !HillAdvantageLayer.Playing;
            });
            Place((RectTransform)play.transform, Width - 12 - 80, yearsTop, 80, 22);
            playBack = (Image)play.targetGraphic; playLabel = play.GetComponentInChildren<TextMeshProUGUI>();
            readingTop = yearsTop + 30;
        }

        void Update()
        {
            if (!root || !root.IsLoaded) return;
            bool show = LandView.PresetId == HillAdvantageLayer.View;
            panel.gameObject.SetActive(show);
            if (!show) { card.Hide(); hoverShown = null; return; }
            var data = Advantage.Get();
            if (data == null) return;
            if (worlds.Count == 0) MakeButtons(data);
            Vector2 canvas = UiFactory.CanvasSize;
            bool portrait = ScreenLayout.IsPortrait;
            if (portrait) { panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, 0); panel.anchoredPosition = new Vector2(0, 8); panel.sizeDelta = new Vector2(Mathf.Min(Width, canvas.x - 16), Mathf.Min(Height, canvas.y * .55f)); }
            else { float top = EconomyUiLayout.SideColumnTop; panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(1, 1); panel.anchoredPosition = new Vector2(-18, -top); panel.sizeDelta = new Vector2(Width, Mathf.Min(Height, canvas.y - top - 54)); }
            string key = HillAdvantageLayer.Scenario + "|" + HillAdvantageLayer.Year + "|" + HillAdvantageLayer.Selected + "|" + HillAdvantageLayer.History;
            if (key != shownKey || shownPlaying != HillAdvantageLayer.Playing)
            {
                shownKey = key;
                shownPlaying = HillAdvantageLayer.Playing;
                foreach (var b in worlds) Mark(b.background, b.label, b.id == HillAdvantageLayer.Scenario);
                foreach (var b in years) Mark(b.background, b.label, b.year == HillAdvantageLayer.Year);
                foreach (var b in modes) Mark(b.background, b.label, b.history == HillAdvantageLayer.History);
                Mark(playBack, playLabel, HillAdvantageLayer.Playing);
                playLabel.text = HillAdvantageLayer.Playing ? "Pause" : "Play";
                reading.text = HillAdvantageLayer.History ? HistoryReading(data) : Reading(data);
                var sel = data.Get(HillAdvantageLayer.Selected);
                detail.text = sel != null && !HillAdvantageLayer.History ? Detail(data, sel) : "";
                Bars(data, HillAdvantageLayer.History ? null : sel);
                method.text = Method(data);
                laidOut = null;
            }

            Layout(portrait);
            Hover(data, canvas);
        }

        static void Mark(Image back, TextMeshProUGUI label, bool active)
        {
            back.color = active ? RegimePanel.ActiveFill : UiFactory.ButtonColor;
            label.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
            label.color = active ? RegimePanel.ActiveText : GraphStyle.Text;
        }

        // ------------------------------------------------------------------ the card

        void Hover(Advantage data, Vector2 canvas)
        {
            string id = HillAdvantageLayer.Hovered;
            var a = data.Get(id);
            var o = a == null ? data.OwnerById(id) : null;
            if (a == null && o == null)
            {
                if (hoverShown != null) card.Hide();
                hoverShown = null;
                card.Tick(PointerInput.Position, canvas, Time.unscaledDeltaTime);
                return;
            }

            if (hoverShown != id)
            {
                hoverShown = id;
                if (a != null) card.Show(id, a.Name, Meta(a), Card(a));
                else card.Show(id, o.Name, KindName(o.Kind) + " · holds " + HillAdvantageLayer.Money(Holdings(data, o)), Stakes(data, o));
            }

            card.Tick(PointerInput.Position, canvas, Time.unscaledDeltaTime);
        }

        static string Meta(Advantage.Actor a)
        {
            var net = HillAdvantageLayer.Network;
            var body = net?.Get(a.Id);
            var s = new List<string>();
            double v = body != null && !double.IsNaN(body.Value) ? body.Value : a.SizeUsdBn;
            if (!double.IsNaN(v)) s.Add((a.Private ? "valued at " : "market value ") + HillAdvantageLayer.Money(v));
            double pe = body?.State != null && !double.IsNaN(body.State.Pe) ? body.State.Pe : a.Pe;
            if (!double.IsNaN(pe)) s.Add(pe > 0 ? "P/E " + pe.ToString("0") : "loss-making");
            if (!double.IsNaN(a.Sheet.Equity)) s.Add("book " + HillAdvantageLayer.Money(a.Sheet.Equity));
            return string.Join(" · ", s);
        }

        static string Card(Advantage.Actor a)
        {
            var b = new StringBuilder();
            if (!string.IsNullOrEmpty(a.Role)) b.Append(Cap(a.Role)).Append(". ");
            if (!string.IsNullOrEmpty(a.AdvantageText)) b.Append(a.AdvantageText);
            b.Append("\n<i>Click for its balance sheet, owners and rivers.</i>");
            return b.ToString();
        }

        // ------------------------------------------------------------------ the reading

        /// <summary>The world and year: its premise and reading, the year's events from the simulation, who rises and falls.</summary>
        static string Reading(Advantage data)
        {
            var b = new StringBuilder();
            string s = HillAdvantageLayer.Scenario;
            int year = HillAdvantageLayer.Year;
            var info = data.ScenarioById(s);
            var world = data.WorldById(s);
            if (info != null)
            {
                b.Append("<b>").Append(info.Title).Append(", ").Append(year).Append("</b>  <i>a simulation, not a forecast</i>\n");
                b.Append(info.Premise).Append('\n');
                if (!string.IsNullOrEmpty(info.Reading)) b.Append("<color=#C9CED6>").Append(info.Reading).Append("</color>\n");
            }
            else if (s == Advantage.Y2030)
                b.Append("<b>The most likely course, ").Append(year).Append("</b>  <i>a simulation, not a forecast</i>\nEach company follows the AI model's judgment of whether its position holds, grows or erodes, read from the sourced facts.\n");
            else
            {
                b.Append("<b>Now, November 2026</b>\n");
                var giants = data.Actors.Where(a => a.Sized && a.Flank != "state").OrderByDescending(a => a.SizeUsdBn).Take(6).ToList();
                if (giants.Count > 0) b.Append("<b>The giants:</b> ").Append(string.Join(", ", giants.Select(a => a.Name + " " + HillAdvantageLayer.Money(a.SizeUsdBn)))).Append('\n');
                var air = data.Actors.Where(a => a.Sized && !double.IsNaN(a.Sheet.Equity) && a.Sheet.Equity > 0).Select(a => (a, x: a.SizeUsdBn / a.Sheet.Equity)).OrderByDescending(t => t.x).Take(4).ToList();
                if (air.Count > 0) b.Append("<b>Most air in the shell</b> (market value over book): ").Append(string.Join(", ", air.Select(t => t.a.Name + " " + t.x.ToString("0") + "×"))).Append('\n');
                b.Append("<i>Hover over a company for its card; click it to light its rivers and owners. Choose a world, then a year, or press play.</i>\n");
            }

            if (world != null)
            {
                var events = world.Events.Where(e => e.Year == year).Take(8).ToList();
                if (events.Count > 0)
                    b.Append("<b>").Append(year).Append(":</b> ").Append(string.Join("; ", events.Select(e => (data.Get(e.Company)?.Name ?? e.Company) + " " + e.What))).Append('\n');
                Movers(data, world, year, b);
            }

            return b.ToString();
        }

        /// <summary>Who stands highest above, and lowest below, where they stood in 2026; and the fallen.</summary>
        static void Movers(Advantage data, Advantage.World world, int year, StringBuilder b)
        {
            var now = data.WorldById(Advantage.Now);
            var moves = new List<(Advantage.Actor a, double d)>();
            var fallen = new List<string>();
            foreach (var a in data.Actors)
            {
                var st = world.At(a.Id, year);
                var was = now?.At(a.Id, 2026) ?? world.At(a.Id, world.FirstYear);
                if (st == null || was == null) continue;
                if (!st.Alive) { fallen.Add(a.Name + (st.Status == "extinct" ? " (extinct)" : " → " + (data.Get(st.By)?.Name ?? st.By))); continue; }
                moves.Add((a, st.Alt - was.Alt));
            }

            var up = moves.Where(m => m.d > 4).OrderByDescending(m => m.d).Take(5).ToList();
            var down = moves.Where(m => m.d < -4).OrderBy(m => m.d).Take(5).ToList();
            if (up.Count > 0) b.Append("<b><color=#9BE7A8>Higher ground:</color></b> ").Append(string.Join(", ", up.Select(m => m.a.Name))).Append('\n');
            if (down.Count > 0) b.Append("<b><color=#F59A9A>Lower ground:</color></b> ").Append(string.Join(", ", down.Select(m => m.a.Name))).Append('\n');
            if (fallen.Count > 0) b.Append("<b>Gone:</b> ").Append(string.Join(", ", fallen.Take(8))).Append('\n');
        }

        /// <summary>The selected company: what it is, its market value against its books, its P/E, its uses of the share price,
        /// its losses, its owners and its largest rivers.</summary>
        static string Detail(Advantage data, Advantage.Actor a)
        {
            var b = new StringBuilder();
            var body = HillAdvantageLayer.Network?.Get(a.Id);
            var st = body?.State;
            b.Append("<b><size=115%>").Append(a.Name).Append("</size></b>");
            if (!string.IsNullOrEmpty(a.Country)) b.Append("  ").Append(a.Country);
            b.Append('\n');
            if (!string.IsNullOrEmpty(a.Role)) b.Append(Cap(a.Role)).Append(".\n");
            double v = body != null && !double.IsNaN(body.Value) ? body.Value : a.SizeUsdBn;
            double eq = st != null && !double.IsNaN(st.Eq) ? st.Eq : a.Sheet.Equity;
            if (!double.IsNaN(v))
            {
                b.Append(a.Private ? "Valued at " : "Market value ").Append(HillAdvantageLayer.Money(v));
                if (!double.IsNaN(eq) && eq > 0) b.Append(", ").Append((v / eq).ToString("0.#")).Append("× its book of ").Append(HillAdvantageLayer.Money(eq));
                b.Append(Host(data, a.SizeFact)).Append('\n');
            }

            double pe = st != null && !double.IsNaN(st.Pe) ? st.Pe : a.Pe;
            if (!double.IsNaN(pe)) b.Append(pe > 0 ? "P/E " + pe.ToString("0.#") + (pe > 60 ? " — a price that pays for the future" : "") : "Loss-making: valued on its future").Append('\n');
            var weapons = new List<string>();
            if (!double.IsNaN(a.Buybacks)) weapons.Add("buybacks " + HillAdvantageLayer.Money(a.Buybacks));
            if (!double.IsNaN(a.Dividends)) weapons.Add("dividends " + HillAdvantageLayer.Money(a.Dividends));
            if (!double.IsNaN(a.Sbc)) weapons.Add("stock pay " + HillAdvantageLayer.Money(a.Sbc));
            if (weapons.Count > 0) b.Append("<b>The share price at work:</b> ").Append(string.Join(", ", weapons)).Append('\n');
            if (a.Acquisitions.Count > 0) b.Append("<b>Bought:</b> ").Append(string.Join("; ", a.Acquisitions.Take(4))).Append('\n');
            if (a.Capacity.Count > 0) b.Append("<b>Data-centre power:</b> ").Append(string.Join("; ", a.Capacity.Take(3))).Append('\n');
            if (!double.IsNaN(a.Burn) && a.Burn > 0) b.Append("<color=#FF9A40><b>Burning</b></color> ").Append(HillAdvantageLayer.Money(a.Burn)).Append(" a year").Append(string.IsNullOrEmpty(a.BurnWhat) ? "" : " (" + a.BurnWhat + ")").Append('\n');
            if (!string.IsNullOrEmpty(a.Strategy)) b.Append("<i>Strategy (judged):</i> ").Append(a.Strategy).Append(double.IsNaN(a.Risk) ? "" : "; <i>risk</i> " + a.Risk.ToString("0.0") + " of 4").Append('\n');
            var owners = data.Stakes.Where(s => s.Company == a.Id).OrderByDescending(s => double.IsNaN(s.Pct) ? 0 : s.Pct).Take(6).ToList();
            if (owners.Count > 0)
                b.Append("<color=#EFB146><b>Owners:</b></color> ").Append(string.Join(", ", owners.Select(s => (data.OwnerById(s.Owner)?.Name ?? data.Get(s.Owner)?.Name ?? s.Owner) + (double.IsNaN(s.Pct) ? "" : " " + s.Pct.ToString("0.#") + "%")))).Append('\n');
            var flows = data.Flows.Where(f => f.From == a.Id || f.To == a.Id).OrderByDescending(f => f.UsdBn).Take(4).ToList();
            if (flows.Count > 0)
                b.Append("<b>Rivers:</b> ").Append(string.Join("; ", flows.Select(f => (f.To == a.Id ? "from " + (data.Get(f.From)?.Name ?? f.From) : "to " + (data.Get(f.To)?.Name ?? f.To)) + " " + HillAdvantageLayer.Money(f.UsdBn) + "/yr"))).Append('\n');
            if (!string.IsNullOrEmpty(a.Desire)) b.Append("<color=#EF5B98><b>Wants:</b></color> ").Append(a.Desire).Append('\n');
            if (!string.IsNullOrEmpty(a.Fear)) b.Append("<color=#79E4F0><b>Fears:</b></color> ").Append(a.Fear).Append('\n');
            return b.ToString();
        }

        /// <summary>The selected company's balance sheet as bars: what it owns (cash, built, bought, stakes, the rest) above what
        /// it owes and its equity (debt, leases, other liabilities, equity), and its market value: one scale for all three.</summary>
        void Bars(Advantage data, Advantage.Actor a)
        {
            foreach (var s in segments) if (s) Destroy(s);
            segments.Clear();
            bars.gameObject.SetActive(a != null && a.Sheet.Known);
            if (a == null || !a.Sheet.Known) return;
            var sh = a.Sheet;
            double total = Pos(sh.TotalAssets);
            double cash = Pos(sh.Cash), ppe = Pos(sh.Ppe), bought = Pos(sh.Goodwill) + Pos(sh.Intangibles), stakes = Pos(sh.Investments);
            if (total <= 0) total = cash + ppe + bought + stakes;
            double rest = System.Math.Max(0, total - cash - ppe - bought - stakes);
            double debt = Pos(sh.Debt), leases = Pos(sh.Leases), equity = sh.Equity;
            double liabilities = !double.IsNaN(sh.TotalLiabilities) ? sh.TotalLiabilities : !double.IsNaN(equity) ? total - equity : debt + leases;
            double otherLiab = System.Math.Max(0, liabilities - debt - leases);
            var body = HillAdvantageLayer.Network?.Get(a.Id);
            double v = body != null && !double.IsNaN(body.Value) ? body.Value : a.SizeUsdBn;
            double scale = System.Math.Max(total, System.Math.Max(double.IsNaN(v) ? 0 : v, 1));
            float w = Width - 24 - 64;
            Row(0, "owns", new[] { (cash, HillAdvantageLayer.CashColor), (ppe, HillAdvantageLayer.BuiltColor), (bought, HillAdvantageLayer.BoughtColor), (stakes, HillAdvantageLayer.StakeColor), (rest, new Color(.4f, .43f, .48f)) }, scale, w);
            Row(1, "owes", new[] { (debt, HillAdvantageLayer.DebtColor), (leases, HillAdvantageLayer.LeaseColor), (otherLiab, new Color(.45f, .3f, .3f)), (System.Math.Max(0, equity), HillAdvantageLayer.CoreColor) }, scale, w);
            if (!double.IsNaN(v)) Row(2, "market", new[] { (System.Math.Max(0, equity), HillAdvantageLayer.CoreColor), (System.Math.Max(0, v - System.Math.Max(0, equity)), HillAdvantageLayer.PeColor(body ?? new ContestNetwork.Body { Actor = a })) }, scale, w);
            barLabel.text = "<color=#66E08F>■</color> cash  <color=#7394DB>■</color> built  <color=#B385F0>■</color> bought  <color=#FFCC5C>■</color> stakes  " +
                            "<color=#B32429>■</color> debt  <color=#D16B24>■</color> leases  <color=#A8B3C2>■</color> equity; market: book, then the air (" + HillAdvantageLayer.Money(scale) + " across). " +
                            (string.IsNullOrEmpty(sh.Date) ? "" : "Balance sheet " + sh.Date + ".");
        }

        void Row(int row, string name, (double value, Color color)[] parts, double scale, float width)
        {
            var label = UiFactory.Text(bars, "Row " + name, name, 9.5f, new Color(.74f, .79f, .86f));
            Place(label.rectTransform, 0, row * (BarHeight + 4), 60, BarHeight);
            segments.Add(label.gameObject);
            float x = 64;
            foreach (var (value, color) in parts)
            {
                if (!(value > 0)) continue;
                float wv = (float)(value / scale) * width;
                if (wv < .5f) continue;
                var img = new GameObject("Segment", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                img.transform.SetParent(bars, false);
                img.color = color;
                img.raycastTarget = false;
                Place(img.rectTransform, x, row * (BarHeight + 4), wv - 1, BarHeight);
                segments.Add(img.gameObject);
                x += wv;
            }
        }

        static double Pos(double v) => double.IsNaN(v) || v < 0 ? 0 : v;

        static string HistoryReading(Advantage data)
        {
            var b = new StringBuilder();
            b.Append("<b>The lineage, 1947 to now</b>  <i>the gold lane along the wave</i>\n");
            var spine = HillAdvantageLayer.SpineSteps;
            foreach (var step in (spine.Count > 0 ? spine : data.Lineage.Where(x => x.Year >= 1947).ToList()).OrderBy(x => x.Year).Take(14))
                b.Append(step.Year).Append("  ").Append(step.FromName == step.ToName ? step.ToName : step.FromName + " → " + step.ToName).Append(": <color=#C9CED6>").Append(Words(step.How, 12)).Append("</color>\n");
            var century = data.Flashpoints.Where(f => f.Year >= 1900 && f.Year < 2023).OrderByDescending(f => f.Sources.Length).Take(6).OrderBy(f => f.Year).ToList();
            if (century.Count > 0)
            {
                b.Append("\n<b>Contests of the century</b>  <i>sparks on the companies' lifelines</i>\n");
                foreach (var f in century) b.Append(f.Year).Append("  <b>").Append(f.Title).Append("</b>: ").Append(f.AdvantageText).Append(". <color=#C9CED6>").Append(Words(f.Outcome, 14)).Append("</color>\n");
            }

            var older = data.Flashpoints.Where(f => f.Era == "archetype" || f.Era == "biosphere" || f.Year < 1900 && f.Year != 0).OrderBy(f => f.Year).Take(6).ToList();
            if (older.Count > 0)
            {
                b.Append("\n<b>The older pattern</b>  <i>before the wave begins</i>\n");
                foreach (var f in older) b.Append(f.Year < 0 ? -f.Year + " BC" : f.Year.ToString()).Append("  <b>").Append(f.Title).Append("</b>: ").Append(Words(f.Mechanism, 16)).Append(" <color=#C9CED6>").Append(Words(f.Outcome, 12)).Append("</color>\n");
            }

            return b.ToString();
        }

        static double Holdings(Advantage data, Advantage.Owner o)
        {
            var body = HillAdvantageLayer.Network?.Get(o.Id);
            return body != null ? body.Value : o.HoldingsUsdBn;
        }

        static string Stakes(Advantage data, Advantage.Owner o) =>
            string.Join(", ", o.Stakes.OrderByDescending(s => double.IsNaN(s.Pct) ? 0 : s.Pct).Take(8).Select(s => (data.Get(s.Company)?.Name ?? s.Company) + (double.IsNaN(s.Pct) ? "" : " " + s.Pct.ToString("0.#") + "%")));

        static string KindName(string kind) => kind switch
        {
            "fund" => "index or asset manager", "sovereign" => "sovereign fund", "pe" => "private equity", "vc" => "venture capital", "hedge" => "hedge fund",
            "corporate" => "company", "person" => "founder or individual", "households" => "households", _ => kind ?? "owner",
        };

        static string Host(Advantage data, string fact)
        {
            if (fact == null || !data.Sources.TryGetValue(fact, out var s) || string.IsNullOrEmpty(s.Url)) return "";
            try { return " (" + new System.Uri(s.Url).Host.Replace("www.", "") + ")"; }
            catch (System.UriFormatException) { return ""; }
        }

        static string Method(Advantage data) =>
            "As of " + data.AsOf + ". " + data.Method + " Judgments by " + (data.Judge ?? "Jev") + " (TypeSafe System One): a model's reading of the evidence, not facts. " +
            "Every number shown is a fact whose quote was found on its source and that Jev judged to support it (Tools/advantage/verify.py); 2026 numbers were read on the as-of date. " +
            (data.SimAssumptions.Count > 0 ? "The simulation's assumptions: " + string.Join("; ", data.SimAssumptions.Take(12)) + ". " : "") +
            "Research and analysis: Docs/research/advantage (" + data.Sources.Count + " sources cited here).";

        static string Words(string s, int n)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var w = s.Split(' ');
            return w.Length <= n ? s : string.Join(" ", w.Take(n)) + "…";
        }

        static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        // ------------------------------------------------------------------ layout

        void Layout(bool portrait)
        {
            string key = reading.text + "|" + detail.text + "|" + panel.sizeDelta + "|" + portrait + "|" + methodOpen + "|" + bars.gameObject.activeSelf;
            if (key == laidOut) return;
            laidOut = key;
            methodLabel.text = methodOpen ? "Hide sources & method" : "Show sources & method";
            method.gameObject.SetActive(methodOpen);
            float w = panel.sizeDelta.x - 24, top = readingTop, barsH = bars.gameObject.activeSelf ? 3 * (BarHeight + 4) + 30 : 0;
            float room = panel.sizeDelta.y - top - 12 - MethodHeight - 8 - barsH;
            float size = portrait ? 9 : 11, msize = portrait ? 8.5f : 9.5f, scale = 1, hr = 0, hd = 0, hm = 0;
            for (int i = 0; i < 8; i++, scale -= .05f)
            {
                reading.fontSize = detail.fontSize = size * scale; method.fontSize = msize * scale;
                hr = reading.GetPreferredValues(reading.text, w, 0).y;
                hd = string.IsNullOrEmpty(detail.text) ? 0 : detail.GetPreferredValues(detail.text, w, 0).y;
                hm = methodOpen ? method.GetPreferredValues(method.text, w, 0).y : 0;
                if (hr + hd + hm + (methodOpen ? 8 : 0) <= room) break;
            }

            Place(reading.rectTransform, 12, top, w, hr);
            float y = top + hr + 6;
            if (barsH > 0)
            {
                Place(bars, 12, y, w, barsH);
                Place(barLabel.rectTransform, 0, 3 * (BarHeight + 4), w, 30);
                y += barsH;
            }

            Place(detail.rectTransform, 12, y, w, hd);
            y += hd + 6;
            Place(methodButton, 12, y, w, MethodHeight);
            Place(method.rectTransform, 12, y + MethodHeight + 8, w, hm);
        }

        TextMeshProUGUI Text(string name, float size, float x, float y, float w, float h)
        {
            var t = UiFactory.Text(panel, name, "", size, GraphStyle.Text); Place(t.rectTransform, x, y, w, h); return t;
        }

        static void Place(RectTransform r, float x, float y, float width, float height)
        {
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1); r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(width, height);
        }
    }
}
