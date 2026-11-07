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
    /// The contest's panel (<see cref="HillAdvantageLayer"/>): a button per world (now, 2030 on the most likely course, and
    /// the strategist's scenarios), the chosen world's premise and reading with the actors it raises and sinks most (from
    /// the judged altitudes), and the selected actor in full: what it is, its market value and margin with their sources,
    /// the powers that hold it up, what it wants and what it fears, the contests it is in. A card follows the pointer over
    /// a peak. Buttons, not keys, so it works by pinch in the headset. "Sources &amp; method" stays folded.
    /// </summary>
    [GraphScenes(EconomyLayouts.HillsScene)]
    public sealed class AdvantagePanel : GraphModule
    {
        const float Width = 440, Height = 820, MethodHeight = 22, ModeTop = 76, ButtonsTop = 104;
        GraphRoot root;
        RectTransform panel, methodButton;
        TextMeshProUGUI reading, method, methodLabel;
        HudTooltip card;
        readonly List<(Image background, TextMeshProUGUI label, string id)> buttons = new List<(Image, TextMeshProUGUI, string)>();
        readonly List<(Image background, TextMeshProUGUI label, bool history)> modes = new List<(Image, TextMeshProUGUI, bool)>();
        string shownKey, laidOut, hoverShown;
        float readingTop = 150;
        bool methodOpen;

        public override void Init(GraphRoot graphRoot)
        {
            root = graphRoot;
            Canvas canvas = UiFactory.CreateCanvas("The present as a contest", 47, transform);
            panel = UiFactory.Panel(canvas.transform, "Contest", new Color(.02f, .03f, .05f, .94f)).rectTransform;
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(1, 1);
            panel.anchoredPosition = new Vector2(-18, -176); panel.sizeDelta = new Vector2(Width, Height);
            var title = Text("Title", 15, 12, 10, Width - 24, 22); title.text = "<b>THE PRESENT AS A CONTEST</b>";
            var note = Text("Note", 10.5f, 12, 32, Width - 24, 40); note.textWrappingMode = TextWrappingModes.Normal; note.color = new Color(.7f, .76f, .84f);
            note.text = "Peaks: market value (width) and competitive advantage (height, judged by an AI model from sourced facts). Choose a world to see who rises and who falls.";
            reading = Text("Reading", 11.5f, 12, readingTop, Width - 24, 420); reading.textWrappingMode = TextWrappingModes.Normal; reading.overflowMode = TextOverflowModes.Overflow;
            var m = UiFactory.Button(panel, "Method", "", 10.5f, () => { methodOpen = !methodOpen; laidOut = null; });
            methodButton = (RectTransform)m.transform; methodLabel = m.GetComponentInChildren<TextMeshProUGUI>();
            method = Text("Method", 10, 12, Height - 128, Width - 24, 118); method.textWrappingMode = TextWrappingModes.Normal; method.overflowMode = TextOverflowModes.Overflow;
            method.color = new Color(.74f, .79f, .86f);
            card = new HudTooltip(canvas.transform);
        }

        void MakeButtons(Advantage data)
        {
            float half = (Width - 24 - 4) / 2;
            foreach (bool history in new[] { false, true })
            {
                bool h = history;
                var b = UiFactory.Button(panel, history ? "History" : "Arena", history ? "History, 1947 to now" : "The 2026 arena", 11, () => HillAdvantageLayer.ShowHistory(h));
                Place((RectTransform)b.transform, 12 + (history ? half + 4 : 0), ModeTop, half, 22);
                modes.Add(((Image)b.targetGraphic, b.GetComponentInChildren<TextMeshProUGUI>(), history));
            }

            var worlds = new List<(string id, string label)> { (Advantage.Now, "Now (2026)"), (Advantage.Y2030, "2030, most likely") };
            foreach (var s in data.Scenarios) worlds.Add((s.Id, s.Title));
            const int perRow = 3;
            float gap = 4, w = (Width - 24 - gap * (perRow - 1)) / perRow;
            for (int i = 0; i < worlds.Count; i++)
            {
                var (id, label) = worlds[i];
                var b = UiFactory.Button(panel, "World " + id, label, 10.5f, () => HillAdvantageLayer.SetScenario(id));
                Place((RectTransform)b.transform, 12 + (i % perRow) * (w + gap), ButtonsTop + (i / perRow) * 26, w, 22);
                buttons.Add(((Image)b.targetGraphic, b.GetComponentInChildren<TextMeshProUGUI>(), id));
            }

            readingTop = ButtonsTop + ((worlds.Count + perRow - 1) / perRow) * 26 + 8;
        }

        void Update()
        {
            if (!root || !root.IsLoaded) return;
            bool show = LandView.PresetId == HillAdvantageLayer.View;
            panel.gameObject.SetActive(show);
            if (!show) { card.Hide(); hoverShown = null; return; }
            var data = Advantage.Get();
            if (data == null) return;
            if (buttons.Count == 0) MakeButtons(data);
            Vector2 canvas = UiFactory.CanvasSize;
            bool portrait = ScreenLayout.IsPortrait;
            if (portrait) { panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, 0); panel.anchoredPosition = new Vector2(0, 8); panel.sizeDelta = new Vector2(Mathf.Min(Width, canvas.x - 16), Mathf.Min(Height, canvas.y * .55f)); }
            else { float top = EconomyUiLayout.SideColumnTop; panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(1, 1); panel.anchoredPosition = new Vector2(-18, -top); panel.sizeDelta = new Vector2(Width, Mathf.Min(Height, canvas.y - top - 54)); }
            string key = HillAdvantageLayer.Scenario + "|" + HillAdvantageLayer.Selected + "|" + HillAdvantageLayer.History;
            if (key != shownKey)
            {
                shownKey = key;
                foreach (var b in buttons)
                {
                    bool active = b.id == HillAdvantageLayer.Scenario;
                    b.background.color = active ? RegimePanel.ActiveFill : UiFactory.ButtonColor;
                    b.label.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
                    b.label.color = active ? RegimePanel.ActiveText : GraphStyle.Text;
                }

                foreach (var b in modes)
                {
                    bool active = b.history == HillAdvantageLayer.History;
                    b.background.color = active ? RegimePanel.ActiveFill : UiFactory.ButtonColor;
                    b.label.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
                    b.label.color = active ? RegimePanel.ActiveText : GraphStyle.Text;
                }

                reading.text = HillAdvantageLayer.History ? HistoryReading(data) : Reading(data);
                method.text = Method(data);
                laidOut = null;
            }

            Layout(portrait);
            Hover(data, canvas);
        }

        /// <summary>The card over the hovered peak: name, value and margin, its role and its advantage.</summary>
        void Hover(Advantage data, Vector2 canvas)
        {
            string id = HillAdvantageLayer.Hovered;
            var a = data.Get(id);
            if (a == null) { if (hoverShown != null) card.Hide(); hoverShown = null; card.Tick(PointerInput.Position, canvas, Time.unscaledDeltaTime); return; }
            if (hoverShown != id)
            {
                hoverShown = id;
                card.Show(id, a.Name, Meta(a), Body(a));
            }

            card.Tick(PointerInput.Position, canvas, Time.unscaledDeltaTime);
        }

        static string Meta(Advantage.Actor a)
        {
            var s = new List<string>();
            if (a.Sized) s.Add((a.SizeKind == "valuation" ? "valued at " : "market value ") + a.SizeLabel);
            if (!double.IsNaN(a.MarginPct)) s.Add("operating margin " + a.MarginPct.ToString("0") + "%");
            if (a.Judged) s.Add("advantage " + a.A.ToString("0.0") + " of 7 (judged)");
            return string.Join(" · ", s);
        }

        static string Body(Advantage.Actor a)
        {
            var b = new StringBuilder();
            if (!string.IsNullOrEmpty(a.Role)) b.Append(Cap(a.Role)).Append(". ");
            if (!string.IsNullOrEmpty(a.AdvantageText)) b.Append(a.AdvantageText);
            b.Append("\n<i>Click for the detail.</i>");
            return b.ToString();
        }

        /// <summary>The world's reading, then the selected actor in full.</summary>
        static string Reading(Advantage data)
        {
            var b = new StringBuilder();
            string s = HillAdvantageLayer.Scenario;
            var info = data.ScenarioById(s);
            if (info != null)
            {
                b.Append("<b>").Append(info.Title).Append("</b>  <i>a scenario, not a forecast</i>\n");
                b.Append(info.Premise).Append('\n');
                if (!string.IsNullOrEmpty(info.Reading)) b.Append("<color=#C9CED6>").Append(info.Reading).Append("</color>\n");
                Movers(data, s, b);
                if (info.Signposts.Length > 0) b.Append("<b>Watch:</b> ").Append(string.Join("; ", info.Signposts)).Append('\n');
            }
            else if (s == Advantage.Y2030)
            {
                b.Append("<b>2030, the most likely course</b>  <i>judged, not a forecast</i>\n");
                b.Append("Each peak moves by the AI model's judgment of whether its position holds, grows or erodes to 2030, read from the sourced facts.\n");
                Movers(data, s, b);
            }
            else
            {
                b.Append("<b>Now, November 2026</b>\n");
                var top = data.Actors.Where(a => a.Judged).OrderByDescending(a => a.A).Take(6).ToList();
                if (top.Count > 0)
                {
                    b.Append("<b>Highest:</b> ");
                    b.Append(string.Join(", ", top.Select(a => a.Name + " (" + data.PowerName(a.Primary) + ")"))).Append('\n');
                }

                var chokes = data.Chokes.Where(c => !double.IsNaN(c.Strength)).OrderByDescending(c => c.Strength).Take(5).ToList();
                if (chokes.Count > 0)
                    b.Append("<b>Choke points:</b> ").Append(string.Join("; ", chokes.Select(c => (data.Get(c.Actor)?.Name ?? c.Actor) + ", " + c.Controls))).Append('\n');
                b.Append(StrataKey(data));
                b.Append("<i>Hover over a peak for its card; click it to light its rivers and contests.</i>\n");
            }

            var sel = data.Get(HillAdvantageLayer.Selected);
            if (sel != null) Actor(data, sel, b);
            return b.ToString();
        }

        /// <summary>The key to a peak's bands: the colour of each of the seven powers (the strongest at the bottom).</summary>
        static string StrataKey(Advantage data)
        {
            var b = new StringBuilder("<b>Bands up a peak</b> (strongest power lowest): ");
            bool first = true;
            foreach (var kv in HillAdvantageLayer.PowerColor)
            {
                if (!first) b.Append("  ");
                first = false;
                b.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(kv.Value)).Append(">■</color> ").Append(data.PowerName(kv.Key));
            }

            return b.Append(". <color=#FFE08A>Gold</color>: operating margin above normal profit (10%).\n").ToString();
        }

        /// <summary>The history: the lineage from the transistor to the frontier, the century's contests, and the older patterns
        /// (armies, empires, the biosphere) that the economy's wave (from 1947) cannot hold.</summary>
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

        /// <summary>The actors a world raises and sinks most (their judged altitudes, relative to now).</summary>
        static void Movers(Advantage data, string scenario, StringBuilder b)
        {
            var moves = data.Actors.Where(a => a.Judged).Select(a => (a, d: a.AdvantageIn(scenario) - a.A)).ToList();
            var up = moves.Where(m => m.d > .25).OrderByDescending(m => m.d).Take(5).ToList();
            var down = moves.Where(m => m.d < -.25).OrderBy(m => m.d).Take(5).ToList();
            if (up.Count > 0) b.Append("<b><color=#9BE7A8>Rise:</color></b> ").Append(string.Join(", ", up.Select(m => m.a.Name))).Append('\n');
            if (down.Count > 0) b.Append("<b><color=#F59A9A>Fall:</color></b> ").Append(string.Join(", ", down.Select(m => m.a.Name))).Append('\n');
        }

        static void Actor(Advantage data, Advantage.Actor a, StringBuilder b)
        {
            b.Append("\n<b><size=115%>").Append(a.Name).Append("</size></b>");
            if (!string.IsNullOrEmpty(a.Country)) b.Append("  ").Append(a.Country);
            b.Append('\n');
            if (!string.IsNullOrEmpty(a.Role)) b.Append(Cap(a.Role)).Append(".\n");
            if (a.Sized) b.Append(a.SizeKind == "valuation" ? "Valued at " : "Market value ").Append(a.SizeLabel).Append(" (").Append(a.SizePeriod).Append(Host(data, a.SizeFact)).Append(")\n");
            else b.Append("<i>No verified market value or valuation.</i>\n");
            if (!double.IsNaN(a.MarginPct)) b.Append("Operating margin ").Append(a.MarginPct.ToString("0.#")).Append("% (").Append(a.MarginPeriod).Append(a.MarginFacts.Length > 0 ? Host(data, a.MarginFacts[0]) : "").Append(")\n");
            if (a.Judged)
            {
                b.Append("<i>Advantage ").Append(a.A.ToString("0.0")).Append(" of 7, judged by Jev:</i> ");
                b.Append(string.Join(", ", a.Strata().Take(3).Select(x => data.PowerName(x.power) + " " + x.score.ToString("0.0"))));
                if (!double.IsNaN(a.A2030)) b.Append("; 2030: ").Append(a.A2030 > a.A + .2 ? "rising" : a.A2030 < a.A - .2 ? "eroding" : "holding");
                b.Append('\n');
                if (a.Triad.Count > 0)
                    b.Append("<i>Rests on</i> information ").Append(a.Triad.TryGetValue("information", out double i) ? i.ToString("0.0") : "-")
                     .Append(", execution ").Append(a.Triad.TryGetValue("execution", out double e) ? e.ToString("0.0") : "-")
                     .Append(", environment ").Append(a.Triad.TryGetValue("environment", out double n) ? n.ToString("0.0") : "-").Append(" (of 3)\n");
                if (!string.IsNullOrEmpty(a.Override)) b.Append("<i>Strategist's override: ").Append(a.Override).Append("</i>\n");
            }

            if (!string.IsNullOrEmpty(a.AdvantageText)) b.Append(a.AdvantageText).Append('\n');
            if (!string.IsNullOrEmpty(a.Desire)) b.Append("<color=#EF5B98><b>Wants:</b></color> ").Append(a.Desire).Append('\n');
            if (!string.IsNullOrEmpty(a.Fear)) b.Append("<color=#79E4F0><b>Fears:</b></color> ").Append(a.Fear).Append('\n');
            var fights = data.Flashpoints.Where(f => f.Holder == a.Id || f.Rivals.Contains(a.Id)).OrderByDescending(f => f.Year).Take(4).ToList();
            if (fights.Count > 0) b.Append("<b>Contests:</b> ").Append(string.Join("; ", fights.Select(f => f.Year + " " + f.Title))).Append('\n');
            var flows = data.Flows.Where(f => f.From == a.Id || f.To == a.Id).OrderByDescending(f => f.UsdBn).Take(4).ToList();
            if (flows.Count > 0)
                b.Append("<b>Money:</b> ").Append(string.Join("; ", flows.Select(f => (f.To == a.Id ? "from " + (data.Get(f.From)?.Name ?? f.From) : "to " + (data.Get(f.To)?.Name ?? f.To)) + " $" + f.UsdBn.ToString("0.#") + "B/yr"))).Append('\n');
        }

        static string Host(Advantage data, string fact)
        {
            if (fact == null || !data.Sources.TryGetValue(fact, out var s) || string.IsNullOrEmpty(s.Url)) return "";
            try { return ", " + new System.Uri(s.Url).Host.Replace("www.", ""); }
            catch (System.UriFormatException) { return ""; }
        }

        static string Method(Advantage data) =>
            "As of " + data.AsOf + ". " + data.Method + " Judgments by " + (data.Judge ?? "Jev") + " (TypeSafe System One), a model's reading of the evidence, not facts; " +
            "every number shown is a fact whose quote was found on its source and that Jev judged to support it (Tools/advantage/verify.py), and 2026 numbers were read on the as-of date. " +
            "Research and analysis: Docs/research/advantage (" + data.Sources.Count + " sources cited here).";

        static string Words(string s, int n)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var w = s.Split(' ');
            return w.Length <= n ? s : string.Join(" ", w.Take(n)) + "…";
        }

        static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        void Layout(bool portrait)
        {
            string key = reading.text + "|" + panel.sizeDelta + "|" + portrait + "|" + methodOpen;
            if (key == laidOut) return;
            laidOut = key;
            methodLabel.text = methodOpen ? "Hide sources & method" : "Show sources & method";
            method.gameObject.SetActive(methodOpen);
            float w = panel.sizeDelta.x - 24, top = readingTop, room = panel.sizeDelta.y - top - 12 - MethodHeight - 8;
            float readingSize = portrait ? 9 : 11, methodSize = portrait ? 8.5f : 10, scale = 1, hr = 0, hm = 0;
            for (int i = 0; i < 8; i++, scale -= .05f)
            {
                reading.fontSize = readingSize * scale; method.fontSize = methodSize * scale;
                hr = reading.GetPreferredValues(reading.text, w, 0).y; hm = methodOpen ? method.GetPreferredValues(method.text, w, 0).y : 0;
                if (hr + hm + (methodOpen ? 8 : 0) <= room) break;
            }

            Place(reading.rectTransform, 12, top, w, hr);
            Place(methodButton, 12, top + hr + 6, w, MethodHeight);
            Place(method.rectTransform, 12, top + hr + 6 + MethodHeight + 8, w, hm);
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
