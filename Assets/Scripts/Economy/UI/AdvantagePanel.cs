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
        readonly List<(Image background, TextMeshProUGUI label, int focus)> focuses = new List<(Image, TextMeshProUGUI, int)>();
        int shownFocus = -2;
        Image playBack; TextMeshProUGUI playLabel;
        readonly List<GameObject> segments = new List<GameObject>();
        string shownScenario, shownSelected, hoverShown;
        int shownYear = -1;
        bool shownHistory, shownPlaying, shown;
        Vector2 laidSize;
        bool laidPortrait, laidMethod, laidBars, layoutDirty = true, barsWanted;
        float readingTop = 160;
        bool methodOpen;

        public override void Init(GraphRoot graphRoot)
        {
            root = graphRoot;
            Canvas canvas = UiFactory.CreateCanvas("The present as a contest", 47, transform);
            panel = UiFactory.Panel(canvas.transform, "Contest", new Color(.02f, .03f, .05f, .94f)).rectTransform;
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(1, 1);
            panel.anchoredPosition = new Vector2(-18, -176); panel.sizeDelta = new Vector2(Width, Height);
            HudBlocks.Register(panel);    // the hills' labels keep out from under it
            var title = Text("Title", 15, 12, 10, Width - 24, 22); title.text = "<b>THE PRESENT AS A CONTEST</b>";
            var note = Text("Note", 10.5f, 12, 32, Width - 24, 44); note.textWrappingMode = TextWrappingModes.Normal; note.color = new Color(.7f, .76f, .84f);
            note.text = "Each company is its balance sheet: equity at the core, what it owns above, what it owes below, in a dust out to its market value. Height: Jev's judged advantage (55%) and market value (45%). Gold circles: its owners.";
            reading = Text("Reading", 11, 12, readingTop, Width - 24, 300); reading.textWrappingMode = TextWrappingModes.Normal; reading.overflowMode = TextOverflowModes.Overflow;
            bars = new GameObject("Balance bars", typeof(RectTransform)).GetComponent<RectTransform>();
            bars.SetParent(panel, false);
            barLabel = UiFactory.Text(bars, "Bar legend", "", 9.5f, new Color(.74f, .79f, .86f));
            barLabel.textWrappingMode = TextWrappingModes.Normal;
            detail = Text("Detail", 11, 12, readingTop + 300, Width - 24, 300); detail.textWrappingMode = TextWrappingModes.Normal; detail.overflowMode = TextOverflowModes.Overflow;
            var m = UiFactory.Button(panel, "Method", "", 10.5f, () => { methodOpen = !methodOpen; layoutDirty = true; });
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
                var b = UiFactory.Button(panel, "World " + id, label, 10.5f, () => HillAdvantageLayer.PlayWorld(id));
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
            // the strata: fly to the hill (the view's own pose), to one stratum, or to the whole stack
            var stops = new List<(int focus, string label)> { (-1, "Whole stack"), (HillAdvantageLayer.Strata, "The hill") };
            for (int i = 0; i < ContestNetwork.StratumOrder.Length; i++) stops.Add((i, StratumShort(ContestNetwork.StratumOrder[i])));
            const int perRow2 = 6;
            float fw = (Width - 24 - gap * (perRow2 - 1)) / perRow2, focusTop = yearsTop + 28;
            for (int i = 0; i < stops.Count; i++)
            {
                var (focus, label) = stops[i];
                var b = UiFactory.Button(panel, "Focus " + focus, label, 9.5f, () => HillAdvantageLayer.FocusOn(focus));
                Place((RectTransform)b.transform, 12 + (i % perRow2) * (fw + gap), focusTop + (i / perRow2) * 24, fw, 20);
                focuses.Add(((Image)b.targetGraphic, b.GetComponentInChildren<TextMeshProUGUI>(), focus));
            }

            readingTop = focusTop + ((stops.Count + perRow2 - 1) / perRow2) * 24 + 6;
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
            if (!shown || HillAdvantageLayer.Scenario != shownScenario || HillAdvantageLayer.Year != shownYear || HillAdvantageLayer.Selected != shownSelected ||
                HillAdvantageLayer.History != shownHistory || HillAdvantageLayer.Playing != shownPlaying || HillAdvantageLayer.Focus != shownFocus)
            {
                shownFocus = HillAdvantageLayer.Focus;
                foreach (var b in focuses) Mark(b.background, b.label, b.focus == HillAdvantageLayer.Focus);
                shown = true;
                shownScenario = HillAdvantageLayer.Scenario;
                shownYear = HillAdvantageLayer.Year;
                shownSelected = HillAdvantageLayer.Selected;
                shownHistory = HillAdvantageLayer.History;
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
                layoutDirty = true;
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
                if (a != null) card.Show(id, a.Name, Meta(data, a), Card(a));
                else card.Show(id, o.Name, OwnerMeta(data, o), Stakes(data, o));
            }

            card.Tick(PointerInput.Position, canvas, Time.unscaledDeltaTime);
        }

        /// <summary>Whether the year on screen is simulated (its numbers carry ≈).</summary>
        static bool Simulated => HillAdvantageLayer.Scenario != Advantage.Now && HillAdvantageLayer.Year > 2026;

        static string Meta(Advantage data, Advantage.Actor a)
        {
            var body = HillAdvantageLayer.Network?.Get(a.Id);
            var st = body?.State;
            string sim = st != null ? "≈" : "";
            var s = new List<string>();
            if (body != null && !body.Alive)
            {
                s.Add(HillAdvantageLayer.FateText(data, HillAdvantageLayer.Scenario, a.Id, HillAdvantageLayer.Year, false));
                return string.Join(" · ", s);
            }

            double v = body != null && !double.IsNaN(body.Value) ? body.Value : a.SizeUsdBn;
            if (!double.IsNaN(v)) s.Add((a.Private ? "valued at " : "market value ") + sim + HillAdvantageLayer.Money(v));
            double pe = st != null ? st.Pe : a.Pe;
            bool loss = st != null ? st.Ni < 0 : a.LossEvidence;
            if (!double.IsNaN(pe) && pe > 0) s.Add("P/E " + sim + pe.ToString("0"));
            else if (loss) s.Add(sim + "loss-making");
            double eq = st != null && !double.IsNaN(st.Eq) ? st.Eq : a.Sheet.Equity;
            if (!double.IsNaN(eq)) s.Add((st != null ? "book ≈" : Simulated ? "2026 book " : "book ") + HillAdvantageLayer.Money(eq));
            if (Simulated && data.NotSimulated.Contains(a.Id)) s.Add("not simulated: its 2026 values");
            return string.Join(" · ", s);
        }

        static string OwnerMeta(Advantage data, Advantage.Owner o)
        {
            string held = (Simulated ? "≈" : "") + HillAdvantageLayer.Money(Holdings(data, o));
            if (o.Kind == "households" || o.Kind == "abroad")
                return (o.Kind == "abroad" ? "the rest of the world · holds " : "households · hold ") + held + " of US corporate stock (" +
                       (Simulated ? "their 2026 holdings × the simulated market" : o.Source ?? "Federal Reserve accounts") + ")";
            return KindName(o.Kind) + " · holds " + held + " in the companies shown (its verified stakes × their " + (Simulated ? "simulated values)" : "market values)");
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
                b.Append("<b>Now, November 2026</b>: the supply chain in strata under the hill, consumer products on top, government the floor.\n");
                int focus = HillAdvantageLayer.Focus;
                if (focus >= 0 && focus < HillAdvantageLayer.Strata) StratumReading(data, ContestNetwork.StratumOrder[focus], b);
                else
                {
                    var nv = data.Moats.FirstOrDefault(m => m.Actor == "nvidia");
                    if (nv != null) MoatReading(data, nv, b, 5);
                    EntryReading(data, b);
                }

                b.Append("<i>Walls: as tall as Jev judges each source keeps rivals and entrants out (0-4). Choose a stratum to see its boundaries up close; click a company for its column through the strata.</i>\n");
            }

            if (world != null)
            {
                var events = world.Events.Where(e => e.Year == year).Take(8).ToList();
                if (events.Count > 0)
                    b.Append("<b>").Append(year).Append(":</b> ").Append(string.Join("; ", events.Select(e => (data.Get(e.Company)?.Name ?? e.Company) + " " + EventText(data, e.What)))).Append('\n');
                Movers(data, world, year, b);
            }

            return b.ToString();
        }

        /// <summary>A simulated event in words: its numbers are the model's (≈), its companies named, not by their ids.</summary>
        static string EventText(Advantage data, string what)
        {
            if (string.IsNullOrEmpty(what)) return "";
            string t = System.Text.RegularExpressions.Regex.Replace(what, @"(?<![≈\w])(\$?\d[\d,.]*\s*(bn|%|tn)?)", "≈$1");
            return System.Text.RegularExpressions.Regex.Replace(t, @"\b(by|into|to) ([a-z0-9]+(?:-[a-z0-9]+)*)\b", m =>
                data.Get(m.Groups[2].Value) is Advantage.Actor a ? m.Groups[1].Value + " " + a.Name : m.Value);
        }

        static string StratumShort(string id) => id switch
        {
            "consumer" => "Consumer", "models" => "Models", "clouds" => "Clouds", "chips" => "Chips", "fabs" => "Fabs", "tools" => "Tools",
            "energy" => "Energy", "materials" => "Materials", "state" => "Government", _ => id,
        };

        /// <summary>An advantage boundary in words: the company, its market and share, and its walls tallest first, each with
        /// its evidence and Jev's height.</summary>
        static void MoatReading(Advantage data, Advantage.Moat m, StringBuilder b, int take)
        {
            string name = data.Get(m.Actor)?.Name ?? m.Actor;
            b.Append("<b>").Append(name).Append(" holds ").Append(m.Market).Append("</b>");
            if (!string.IsNullOrEmpty(m.Share)) b.Append(" (").Append(m.Share).Append(')');
            b.Append(". Its walls, tallest first:\n");
            foreach (var x in m.Sources.OrderByDescending(x => double.IsNaN(x.Wall) ? -1 : x.Wall).Take(take))
                b.Append("  <color=#").Append(ColorUtility.ToHtmlStringRGB(HillAdvantageLayer.SourceColor(x.Source))).Append("><b>").Append(HillAdvantageLayer.SourceName(x.Source))
                 .Append("</b></color> ").Append(x.What).Append(double.IsNaN(x.Wall) ? "" : " <i>(wall " + x.Wall.ToString("0.0") + ")</i>").Append('\n');
            var low = m.Sources.Where(x => !double.IsNaN(x.Wall)).OrderBy(x => x.Wall).FirstOrDefault();
            if (low != null && m.Sources.Count > take)
                b.Append("  <i>Lowest:</i> ").Append(HillAdvantageLayer.SourceName(low.Source)).Append(", ").Append(low.What).Append(" <i>(").Append(low.Wall.ToString("0.0")).Append(")</i>\n");
        }

        /// <summary>What keeps new entrants out, stratum by stratum (Jev), with the evidence where talent and the share price are
        /// most of it, and what became of the entrants.</summary>
        static void EntryReading(Advantage data, StringBuilder b)
        {
            var talent = data.Strata.Where(x => !double.IsNaN(x.TalentCapital) && x.TalentCapital >= 1.5).OrderByDescending(x => x.TalentCapital).ToList();
            foreach (var st in talent)
            {
                b.Append("<b>Keeping entrants out of ").Append(st.Name.ToLowerInvariant()).Append("</b>: talent and the share price are ")
                 .Append(st.TalentCapital >= 2.5 ? "most" : "about half").Append(" of the wall <i>(judged ").Append(st.TalentCapital.ToString("0.0")).Append(" of 4)</i>. ");
                var ev = data.Barriers.Where(x => x.Stratum == st.Id && (x.Source == "talent" || x.Source == "capital")).Take(3).Select(x => x.What).ToList();
                if (ev.Count > 0) b.Append(string.Join("; ", ev)).Append(". ");
                var es = data.Entrants.Where(x => x.Stratum == st.Id).ToList();
                if (es.Count > 0)
                {
                    var taken = es.Where(x => x.Fate == "acquired" || x.Fate == "licence-and-hire").ToList();
                    b.Append("Of ").Append(es.Count).Append(" entrants, ").Append(taken.Count).Append(" were taken");
                    if (taken.Count > 0) b.Append(" (").Append(string.Join(", ", taken.Take(4).Select(x => x.Name + " → " + (data.Get(x.By)?.Name ?? x.By)))).Append(')');
                    var free = es.Where(x => x.Fate == "independent" || x.Fate == "public").Take(4).Select(x => x.Name).ToList();
                    if (free.Count > 0) b.Append("; still independent: ").Append(string.Join(", ", free));
                    b.Append(".\n");
                }
                else b.Append('\n');
            }

            var other = data.Strata.Where(x => !double.IsNaN(x.TalentCapital) && x.TalentCapital < 1.5 && !double.IsNaN(x.Hard)).OrderByDescending(x => x.Hard).ToList();
            if (other.Count > 0)
                b.Append("<b>Elsewhere the wall is not talent</b> <i>(judged)</i>: ").Append(string.Join("; ", other.Select(x =>
                {
                    var top = data.Moats.Where(m => m.Stratum == x.Id).SelectMany(m => m.Sources.Select(src => (m, src))).Where(t => !double.IsNaN(t.src.Wall))
                        .OrderByDescending(t => t.src.Wall).FirstOrDefault();
                    return x.Name.ToLowerInvariant() + " (entry " + x.Hard.ToString("0.0") + " of 4" +
                           (top.m != null ? "; tallest wall: " + (data.Get(top.m.Actor)?.Name ?? top.m.Actor) + "'s " + HillAdvantageLayer.SourceName(top.src.Source).ToLowerInvariant() : "") + ")";
                }))).Append(".\n");
        }

        /// <summary>One stratum up close: its boundaries (every source with its evidence), its entry reading and entrants.</summary>
        static void StratumReading(Advantage data, string id, StringBuilder b)
        {
            var st = data.StratumById(id);
            if (st == null) return;
            b.Append("<b>").Append(st.Name).Append("</b>");
            if (!double.IsNaN(st.Hard)) b.Append(": entry ").Append(st.Hard.ToString("0.0")).Append(" of 4 <i>(judged)</i>");
            b.Append('\n');
            foreach (var m in data.Moats.Where(m => m.Stratum == id)) MoatReading(data, m, b, 8);
            foreach (var x in data.Barriers.Where(x => x.Stratum == id).Take(5))
                b.Append("  <i>").Append(HillAdvantageLayer.SourceName(x.Source)).Append(" barrier:</i> ").Append(x.What).Append('\n');
            var es = data.Entrants.Where(x => x.Stratum == id).ToList();
            if (es.Count > 0)
                b.Append("<b>Entrants:</b> ").Append(string.Join("; ", es.Select(x => x.Name + " (" + x.Fate + (x.By != null && x.Fate != "independent" ? " → " + (data.Get(x.By)?.Name ?? x.By) : "") + ")"))).Append('\n');
            var who = data.Actors.Where(a => a.StratumId == id && a.Sized).OrderByDescending(a => a.SizeUsdBn).Select(a => a.Name).ToList();
            var visitors = data.Actors.Where(a => a.StratumId != id && a.Presence.Any(p => p.Stratum == id)).Select(a => a.Name).ToList();
            if (who.Count > 0) b.Append("<b>Here:</b> ").Append(string.Join(", ", who)).Append('\n');
            if (visitors.Count > 0) b.Append("<b>Also working here</b> (their columns): ").Append(string.Join(", ", visitors)).Append('\n');
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
                if (!st.Alive) { fallen.Add(a.Name + " " + HillAdvantageLayer.FateText(data, world.Id, a.Id, year, false)); continue; }
                moves.Add((a, st.Alt - was.Alt));
            }

            var up = moves.Where(m => m.d > 4).OrderByDescending(m => m.d).Take(5).ToList();
            var down = moves.Where(m => m.d < -4).OrderBy(m => m.d).Take(5).ToList();
            if (up.Count > 0) b.Append("<b><color=#9BE7A8>Higher ground:</color></b> ").Append(string.Join(", ", up.Select(m => m.a.Name))).Append('\n');
            if (down.Count > 0) b.Append("<b><color=#F59A9A>Lower ground:</color></b> ").Append(string.Join(", ", down.Select(m => m.a.Name))).Append('\n');
            if (fallen.Count > 0) b.Append("<b>Gone:</b> ").Append(string.Join(", ", fallen.Take(8))).Append('\n');
        }

        /// <summary>The selected company: what it is, its market value against its books, its P/E, its uses of the share price,
        /// its losses, its owners and its largest rivers; every figure with its period, simulated ones with ≈.</summary>
        static string Detail(Advantage data, Advantage.Actor a)
        {
            var b = new StringBuilder();
            var body = HillAdvantageLayer.Network?.Get(a.Id);
            var st = body?.State;
            bool alive = body == null || body.Alive;
            b.Append("<b><size=115%>").Append(a.Name).Append("</size></b>");
            if (!string.IsNullOrEmpty(a.Country)) b.Append("  ").Append(a.Country);
            b.Append('\n');
            if (!string.IsNullOrEmpty(a.Role)) b.Append(Cap(a.Role)).Append(".\n");
            if (!alive) b.Append("<b>").Append(HillAdvantageLayer.FateText(data, HillAdvantageLayer.Scenario, a.Id, HillAdvantageLayer.Year, true)).Append("</b> in this simulated world.\n");
            string sim = st != null ? "≈" : "";
            if (st != null) b.Append("<i>Simulated ").Append(HillAdvantageLayer.Year).Append(" (≈): the model's numbers, not measured.</i>\n");
            else if (Simulated && data.NotSimulated.Contains(a.Id))
                b.Append("<i>Not simulated: its revenue, cash and debt would all have been assumed, so it stands at its verified 2026 values.</i>\n");
            double v = body != null && !double.IsNaN(body.Value) ? body.Value : a.SizeUsdBn;
            double eq = st != null && !double.IsNaN(st.Eq) ? st.Eq : a.Sheet.Equity;
            if (!double.IsNaN(v) && alive)
            {
                b.Append(a.Private ? "Valued at " : "Market value ").Append(sim).Append(HillAdvantageLayer.Money(v));
                if (st == null && a.Private) b.Append(Per(a.SizePeriod));
                if (!double.IsNaN(eq) && eq > 0) b.Append(", ").Append(sim).Append((v / eq).ToString("0.#")).Append("× its book of ").Append(HillAdvantageLayer.Money(eq));
                if (st == null) b.Append(Host(data, a.SizeFact));
                b.Append('\n');
            }

            double pe = st != null ? st.Pe : a.Pe;
            bool loss = st != null ? st.Ni < 0 : a.LossEvidence;
            if (alive && !double.IsNaN(pe) && pe > 0)
                b.Append("P/E ").Append(sim).Append(pe.ToString("0.#")).Append(st == null ? Per(a.PePeriod) : "").Append(pe > 60 ? ": a price that pays for the future" : "").Append('\n');
            else if (alive && loss) b.Append(st != null ? "≈ Loss-making this year" : "Loss-making: valued on its future").Append('\n');
            if (st != null && alive)
            {
                b.Append("≈ revenue ").Append(HillAdvantageLayer.Money(st.Rev)).Append(st.Ni < 0 ? ", loss " + HillAdvantageLayer.Money(-st.Ni) : ", net income " + HillAdvantageLayer.Money(st.Ni))
                 .Append(", cash ").Append(HillAdvantageLayer.Money(st.Cash)).Append(", debt ").Append(HillAdvantageLayer.Money(st.Debt)).Append('\n');
                if (data.Fallbacks.TryGetValue(a.Id, out var fb) && fb.Length > 0) b.Append("<i>Assumed inputs:</i> <color=#C9CED6>").Append(string.Join("; ", fb)).Append("</color>\n");
            }

            var weapons = new List<string>();
            if (!double.IsNaN(a.Buybacks)) weapons.Add((a.BuybacksReturned ? "returned to shareholders " : "buybacks ") + HillAdvantageLayer.Money(a.Buybacks) + Per(a.BuybacksPeriod));
            if (!double.IsNaN(a.Dividends)) weapons.Add("dividends " + HillAdvantageLayer.Money(a.Dividends) + Per(a.DividendsPeriod));
            if (!double.IsNaN(a.Sbc)) weapons.Add("stock pay " + HillAdvantageLayer.Money(a.Sbc) + Per(a.SbcPeriod));
            if (weapons.Count > 0) b.Append("<b>The share price at work:</b> ").Append(string.Join(", ", weapons)).Append('\n');
            if (a.Acquisitions.Count > 0) b.Append("<b>Bought:</b> ").Append(string.Join("; ", a.Acquisitions.Take(4))).Append('\n');
            if (a.Capacity.Count > 0) b.Append("<b>Data-centre power:</b> ").Append(string.Join("; ", a.Capacity.Take(3))).Append('\n');
            if (!double.IsNaN(a.Burn) && a.Burn > 0)
            {
                b.Append("<color=#FF9A40><b>Burning</b></color> ").Append(HillAdvantageLayer.Money(a.Burn)).Append(Per(a.BurnPeriod));
                if (!double.IsNaN(a.BurnYearly) && System.Math.Abs(a.BurnYearly - a.Burn) > .05) b.Append(", about ").Append(HillAdvantageLayer.Money(a.BurnYearly)).Append(" a year at that rate");
                if (!string.IsNullOrEmpty(a.BurnWhat)) b.Append(". <color=#C9CED6>").Append(a.BurnWhat).Append("</color>");
                b.Append('\n');
            }

            if (!double.IsNaN(a.SegmentLoss) && a.SegmentLoss > 0)
                b.Append("<b>Segment loss</b> ").Append(HillAdvantageLayer.Money(a.SegmentLoss)).Append(Per(a.SegmentLossPeriod))
                 .Append(string.IsNullOrEmpty(a.SegmentLossWhat) ? "" : ". <color=#C9CED6>" + a.SegmentLossWhat + "</color>").Append('\n');
            if (!string.IsNullOrEmpty(a.Strategy)) b.Append("<i>Strategy (judged):</i> ").Append(a.Strategy).Append(double.IsNaN(a.Risk) ? "" : "; <i>risk</i> " + a.Risk.ToString("0.0") + " of 4").Append('\n');
            var owners = data.Stakes.Where(s => s.Company == a.Id).OrderByDescending(s => double.IsNaN(s.Pct) ? 0 : s.Pct).Take(6).ToList();
            if (owners.Count > 0)
                b.Append("<color=#EFB146><b>Owners:</b></color> ").Append(string.Join("; ", owners.GroupBy(s => s.Period ?? "").Select(g =>
                    string.Join(", ", g.Select(s => Name(data, s.Owner) + (double.IsNaN(s.Pct) ? double.IsNaN(s.UsdBn) ? "" : " " + HillAdvantageLayer.Money(s.UsdBn) : " " + s.Pct.ToString("0.#") + "%"))) + Per(g.Key)))).Append('\n');
            var flows = data.Flows.Where(f => f.From == a.Id || f.To == a.Id).OrderByDescending(f => f.UsdBn).Take(5).ToList();
            if (flows.Count > 0) b.Append("<b>Rivers:</b> ").Append(string.Join("; ", flows.Select(f => River(data, a, f)))).Append('\n');
            if (a.Presence.Count > 0)
                b.Append("<b>Works in:</b> ").Append(string.Join("; ", a.Presence.Select(p => StratumShort(p.Stratum).ToLowerInvariant() + " (" +
                    (p.Family.Length > 0 ? string.Join(", ", p.Family.Take(6)) : Words(p.What, 10)) + ")"))).Append('\n');
            foreach (var m in data.Moats.Where(m => m.Actor == a.Id)) MoatReading(data, m, b, 8);
            if (!string.IsNullOrEmpty(a.Desire)) b.Append("<color=#EF5B98><b>Wants:</b></color> ").Append(a.Desire).Append('\n');
            if (!string.IsNullOrEmpty(a.Fear)) b.Append("<color=#79E4F0><b>Fears:</b></color> ").Append(a.Fear).Append('\n');
            return b.ToString();
        }

        /// <summary>A river in words: capital raised or invested once, with its date; money a year only where it is yearly, with
        /// its period; estimates and contracts tagged.</summary>
        static string River(Advantage data, Advantage.Actor a, Advantage.Flow f)
        {
            bool inbound = f.To == a.Id;
            string other = Name(data, inbound ? f.From : f.To), money = HillAdvantageLayer.Money(f.UsdBn);
            string tag = f.Basis == "estimate" ? "est." : f.Basis == "contracted" ? "contracted" : null;
            string per = string.IsNullOrEmpty(f.Period) ? tag : tag == null ? Flat(f.Period) : Flat(f.Period) + "; " + tag;
            per = string.IsNullOrEmpty(per) ? "" : " (" + per + ")";
            if (f.Kind == "investment") return (inbound ? "raised " + money + " from " + other : "invested " + money + " in " + other) + per;
            string what = f.Kind == "wage" ? "wages " : f.Kind == "payout" ? "payouts " : "";
            return what + (inbound ? "from " : "to ") + other + " " + money + (f.Yearly ? "/yr" : "") + per;
        }

        static string Name(Advantage data, string id) => data.Get(id)?.Name ?? data.OwnerById(id)?.Name ?? id;

        /// <summary>A period in parentheses, its own parentheses flattened to commas.</summary>
        static string Per(string period) => string.IsNullOrEmpty(period) ? "" : " (" + Flat(period) + ")";

        static string Flat(string period) => period.Replace(" (", ", ").Replace("(", "").Replace(")", "");

        /// <summary>The selected company's balance sheet as bars, in the organism's colours (equity, debt and the speculative air
        /// as their heat maps): what it owns (cash, built, bought, stakes, the rest) above what it owes and its equity (debt,
        /// leases, other liabilities, equity), and its market value: one scale for all three. A simulated year takes cash, debt
        /// and book from the model and scales the other parts with its book, as the organism does.</summary>
        void Bars(Advantage data, Advantage.Actor a)
        {
            foreach (var s in segments) if (s) Destroy(s);
            segments.Clear();
            var body = HillAdvantageLayer.Network?.Get(a?.Id);
            bool on = a != null && a.Sheet.Known && (body == null || body.Alive);
            barsWanted = on;
            bars.gameObject.SetActive(on && !methodOpen);
            if (!on) return;
            var sh = a.Sheet;
            var st = body?.State;
            double scale = st != null && !double.IsNaN(st.Eq) && sh.Equity > 0 ? System.Math.Max(.05, st.Eq / sh.Equity) : 1;
            double total = Pos(sh.TotalAssets);
            double cash = Pos(sh.Cash), ppe = Pos(sh.Ppe), bought = Pos(sh.Goodwill) + Pos(sh.Intangibles), stakes = Pos(sh.Investments);
            if (total <= 0) total = cash + ppe + bought + stakes;
            double rest = System.Math.Max(0, total - cash - ppe - bought - stakes);
            double debt = Pos(sh.Debt), leases = Pos(sh.Leases), equity = sh.Equity;
            double liabilities = !double.IsNaN(sh.TotalLiabilities) ? sh.TotalLiabilities : !double.IsNaN(equity) ? total - equity : debt + leases;
            double otherLiab = System.Math.Max(0, liabilities - debt - leases);
            if (st != null)
            {
                cash = Pos(st.Cash); debt = Pos(st.Debt); equity = st.Eq;
                ppe *= scale; bought *= scale; stakes *= scale; rest *= scale; leases *= scale; otherLiab *= scale;
            }

            double v = body != null && !double.IsNaN(body.Value) ? body.Value : a.SizeUsdBn;
            double pe = st != null ? st.Pe : a.Pe;
            bool loss = st != null ? st.Ni < 0 : a.LossEvidence;
            Color eqColor = Organism.EquityHeat(equity), debtColor = Organism.DebtHeat(debt, equity), air = Organism.SpecHeat(pe, loss), other = new Color(.4f, .43f, .48f);
            double owes = debt + leases + otherLiab + System.Math.Max(0, equity);
            double width = System.Math.Max(System.Math.Max(cash + ppe + bought + stakes + rest, owes), System.Math.Max(double.IsNaN(v) ? 0 : v, 1));
            float w = Width - 24 - 64;
            Row(0, "owns", new[] { (cash, Organism.Cash), (ppe, Organism.Built), (bought, Organism.Bought), (stakes, Organism.Stakes), (rest, other) }, width, w);
            Row(1, "owes", new[] { (debt, debtColor), (leases, Organism.Leases), (otherLiab, new Color(.45f, .3f, .3f)), (System.Math.Max(0, equity), eqColor) }, width, w);
            if (!double.IsNaN(v)) Row(2, "market", new[] { (System.Math.Max(0, equity), eqColor), (System.Math.Max(0, v - System.Math.Max(0, equity)), air) }, width, w);
            barLabel.text = Swatch(Organism.Cash) + " cash  " + Swatch(Organism.Built) + " built  " + Swatch(Organism.Bought) + " bought  " + Swatch(Organism.Stakes) + " stakes  " +
                            Swatch(debtColor) + " debt  " + Swatch(Organism.Leases) + " leases  " + Swatch(eqColor) + " equity; market: book, then " + Swatch(air) + " the air (" +
                            (st != null ? "≈" : "") + HillAdvantageLayer.Money(width) + " across). " +
                            (st != null ? "≈ Simulated " + HillAdvantageLayer.Year + ": cash, debt and book from the model, the other parts scaled with its book."
                                : string.IsNullOrEmpty(sh.Date) ? "" : Cap(sh.Date) + ".");
        }

        static string Swatch(Color c) => "<color=#" + ColorUtility.ToHtmlStringRGB(c) + ">■</color>";

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
                foreach (var f in older) b.Append(f.Year <= -10000 ? "about " + (-f.Year / 1e6).ToString("0.#") + " million years ago" : f.Year < 0 ? -f.Year + " BC" : f.Year.ToString()).Append("  <b>").Append(f.Title).Append("</b>: ").Append(Words(f.Mechanism, 16)).Append(" <color=#C9CED6>").Append(Words(f.Outcome, 12)).Append("</color>\n");
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
            "fund" => "index or asset manager", "sovereign" => "sovereign fund", "pe" => "private capital (equity and credit)", "vc" => "venture capital", "hedge" => "hedge fund",
            "corporate" => "company", "person" => "founder or individual", "households" => "households", "abroad" => "the rest of the world (Fed Z.1)", _ => kind ?? "owner",
        };

        static string Host(Advantage data, string fact)
        {
            if (fact == null || !data.Sources.TryGetValue(fact, out var s) || string.IsNullOrEmpty(s.Url)) return "";
            try { return " (" + new System.Uri(s.Url).Host.Replace("www.", "") + ")"; }
            catch (System.UriFormatException) { return ""; }
        }

        static string Method(Advantage data) =>
            "As of " + data.AsOf + ". " + data.Method + " Judgments by " + (data.Judge ?? "Jev") + " (TypeSafe System One): a model's reading of the evidence, not facts. " +
            "Strata, presence, advantage boundaries, barriers and entrants come from research round three (Docs/research/advantage: moats, entry, " +
            "integration, materials), every number verified; each company stands in the stratum where most of its revenue is earned (Tools/advantage/build.py, " +
            "HOME_STRATUM). The walls' heights (how much a source keeps rivals and entrants out) and the strata's entry readings are Jev's judgments (0-4), not facts. " +
            "Every 2026 number is a verified fact unless marked derived, est. or ≈: its quote was found on its source and Jev judged it to support the number " +
            "(Tools/advantage/verify.py); 2026 numbers were read on the as-of date, and each is shown with its period. Height and glow are Jev's judgments: " +
            "height is 55% judged advantage and 45% market value; the glow marks the three hardest choke points. " +
            "Derived, not measured: an owner's circle is the sum of its verified stakes at today's market values; the wealth groups' are the Fed's shares of households' stocks; " +
            "market value over book divides two facts; the earnings shell is net income at a P/E of 15 (assumed). Rivers marked as estimates in the research are estimates. " +
            "Years after 2026 are simulated (≈); " + data.NotSimulated.Count + " companies whose inputs would all be assumed are not simulated and stay at 2026. " +
            (data.SimAssumptionValues.Count > 0 ? "All " + data.SimAssumptionValues.Count + " of the simulation's assumptions (each with its reason in Tools/advantage/simulate.py): " +
                string.Join("; ", data.SimAssumptionValues.Select(x => x.name + " " + Short(x.value.Replace(",", ", "), 34))) + ". " : "") +
            "Research and analysis: Docs/research/advantage (" + data.Sources.Count + " sources cited here).";

        static string Words(string s, int n)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var w = s.Split(' ');
            return w.Length <= n ? s : string.Join(" ", w.Take(n)) + "…";
        }

        static string Short(string s, int n) => string.IsNullOrEmpty(s) || s.Length <= n ? s : s.Substring(0, n).TrimEnd(',', ' ') + "…";

        static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        // ------------------------------------------------------------------ layout

        /// <summary>Lays the panel out when something it depends on changed: the reading, the bars and the detail, shrunk to fit;
        /// or, with the method open, the method alone in their place.</summary>
        void Layout(bool portrait)
        {
            if (!layoutDirty && laidSize == panel.sizeDelta && laidPortrait == portrait && laidMethod == methodOpen && laidBars == barsWanted) return;
            layoutDirty = false;
            laidSize = panel.sizeDelta;
            laidPortrait = portrait;
            laidMethod = methodOpen;
            laidBars = barsWanted;
            methodLabel.text = methodOpen ? "Back to the reading" : "Show sources & method";
            method.gameObject.SetActive(methodOpen);
            reading.gameObject.SetActive(!methodOpen);
            detail.gameObject.SetActive(!methodOpen);
            bars.gameObject.SetActive(barsWanted && !methodOpen);
            float w = panel.sizeDelta.x - 24, top = readingTop;
            if (methodOpen)
            {
                float roomM = panel.sizeDelta.y - top - 12 - MethodHeight - 8, ms = portrait ? 8.5f : 9.5f, hm = 0;
                for (int i = 0; i < 10; i++, ms -= .4f)
                {
                    method.fontSize = ms;
                    hm = method.GetPreferredValues(method.text, w, 0).y;
                    if (hm <= roomM) break;
                }

                Place(methodButton, 12, top, w, MethodHeight);
                Place(method.rectTransform, 12, top + MethodHeight + 8, w, hm);
                return;
            }

            float labelH = barsWanted ? barLabel.GetPreferredValues(barLabel.text, w, 0).y + 4 : 0, barsH = barsWanted ? 3 * (BarHeight + 4) + labelH : 0;
            float room = panel.sizeDelta.y - top - 12 - MethodHeight - 8 - barsH;
            float size = portrait ? 9 : 11, scale = 1, hr = 0, hd = 0;
            for (int i = 0; i < 8; i++, scale -= .05f)
            {
                reading.fontSize = detail.fontSize = size * scale;
                hr = reading.GetPreferredValues(reading.text, w, 0).y;
                hd = string.IsNullOrEmpty(detail.text) ? 0 : detail.GetPreferredValues(detail.text, w, 0).y;
                if (hr + hd <= room) break;
            }

            Place(reading.rectTransform, 12, top, w, hr);
            float y = top + hr + 6;
            if (barsH > 0)
            {
                Place(bars, 12, y, w, barsH);
                Place(barLabel.rectTransform, 0, 3 * (BarHeight + 4), w, labelH);
                y += barsH;
            }

            Place(detail.rectTransform, 12, y, w, hd);
            y += hd + 6;
            Place(methodButton, 12, y, w, MethodHeight);
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
