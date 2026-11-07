using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Why.Director;
using Why.Economy.Land;
using Why.Economy.Layers;
using Why.Economy.Model;
using Why.UI;

namespace Why.Economy.UI
{
    /// <summary>
    /// The history view's panel (<see cref="HillHistoryLayer"/>): the two stories, the moments one by one, the
    /// cross-section opened or closed, and the narrated walkthrough of the story shown; then the reading of the moment at the
    /// year: who held the advantage over whom and with whom, what decided it and what came of it, the advantages documented
    /// for the period with their sources (and Jev's walls), and the periods it falls in. Buttons, not keys, so it works by
    /// pinch in the headset.
    /// </summary>
    [GraphScenes(EconomyLayouts.HillsScene)]
    public sealed class HistoryPanel : GraphModule
    {
        const float Width = 430, Height = 760;

        /// <summary>The stories' walkthroughs (Resources paths) by story id.</summary>
        public static readonly Dictionary<string, string> Walkthroughs = new Dictionary<string, string>
        {
            { "ai", "Data/Walkthrough/story_ai_tour" }, { "rev", "Data/Walkthrough/story_revolutions_tour" },
        };

        GraphRoot root;
        RectTransform panel;
        TextMeshProUGUI reading, method;
        readonly List<(Image background, TextMeshProUGUI label, string id)> stories = new List<(Image, TextMeshProUGUI, string)>();
        Image openBack; TextMeshProUGUI openLabel;
        bool dirty = true, made;

        public override void Init(GraphRoot graphRoot)
        {
            root = graphRoot;
            Canvas canvas = UiFactory.CreateCanvas("Advantage through history", 47, transform);
            panel = UiFactory.Panel(canvas.transform, "History", new Color(.02f, .03f, .05f, .94f)).rectTransform;
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(1, 1);
            panel.anchoredPosition = new Vector2(-18, -176); panel.sizeDelta = new Vector2(Width, Height);
            HudBlocks.Register(panel);
            var title = Text("Title", 15, 12, 10, Width - 24, 22); title.text = "<b>ADVANTAGE THROUGH HISTORY</b>";
            var note = Text("Note", 10.5f, 12, 32, Width - 24, 44); note.textWrappingMode = TextWrappingModes.Normal; note.color = new Color(.7f, .76f, .84f);
            note.text = "Each actor is a tube through time, as thick as what it measured then. Open a moment to see the actors side by side, each advantage a ring walled by its sources.";
            reading = Text("Reading", 11, 12, 168, Width - 24, Height - 168 - 70); reading.textWrappingMode = TextWrappingModes.Normal; reading.overflowMode = TextOverflowModes.Ellipsis;
            method = Text("Method", 9, 12, Height - 64, Width - 24, 58); method.textWrappingMode = TextWrappingModes.Normal; method.color = new Color(.62f, .67f, .74f);
            method.text = "Every number is a fact whose quote was found on its cited page. Sizes are the cube root of a measure: a thousand under arms draws as a hundred thousand civilians; money of 1775-1815 at £1 = 24 livres (assumed). Walls: Jev's judgments, 0-4.";
            HillHistoryLayer.Changed += OnChanged;
        }

        void OnChanged() => dirty = true;

        void OnDestroy() => HillHistoryLayer.Changed -= OnChanged;

        void Make()
        {
            made = true;
            float half = (Width - 24 - 4) / 2;
            var list = new[] { ("ai", "The AI revolution, 1945-2026"), ("rev", "The age of revolutions, 1775-1815") };
            for (int i = 0; i < list.Length; i++)
            {
                var (id, label) = list[i];
                var b = UiFactory.Button(panel, "Story " + id, label, 10.5f, () => HillHistoryLayer.ShowStory(id));
                Place((RectTransform)b.transform, 12 + i * (half + 4), 80, half, 24);
                stories.Add(((Image)b.targetGraphic, b.GetComponentInChildren<TextMeshProUGUI>(), id));
            }

            float w = (Width - 24 - 3 * 4) / 4;
            var prev = UiFactory.Button(panel, "Previous", "< Moment", 10.5f, () => HillHistoryLayer.Step(-1));
            Place((RectTransform)prev.transform, 12, 110, w, 24);
            var next = UiFactory.Button(panel, "Next", "Moment >", 10.5f, () => HillHistoryLayer.Step(1));
            Place((RectTransform)next.transform, 12 + (w + 4), 110, w, 24);
            var open = UiFactory.Button(panel, "Open", "Open", 10.5f, () => HillHistoryLayer.Explode(!HillHistoryLayer.Exploded));
            Place((RectTransform)open.transform, 12 + 2 * (w + 4), 110, w, 24);
            openBack = (Image)open.targetGraphic; openLabel = open.GetComponentInChildren<TextMeshProUGUI>();
            var play = UiFactory.Button(panel, "Play", "Play the story", 10.5f, () =>
            {
                if (DirectorMode.Current != null && Walkthroughs.TryGetValue(HillHistoryLayer.StoryId, out string path)) DirectorMode.Current.StartStory(path);
            });
            Place((RectTransform)play.transform, 12 + 3 * (w + 4), 110, w, 24);
            var hint = Text("Hint", 9.5f, 12, 140, Width - 24, 22); hint.color = new Color(.62f, .67f, .74f);
            hint.text = "The story is narrated; every portrait is free-licensed, its credit under it.";
        }

        void Update()
        {
            if (!root || !root.IsLoaded) return;
            // a story's tour card takes the right side while it plays; the panel returns when the tour ends
            bool show = LandView.PresetId == HillHistoryLayer.View && !root.TourActive;
            panel.gameObject.SetActive(show);
            if (!show) return;
            if (!made) Make();
            Vector2 canvas = UiFactory.CanvasSize;
            if (ScreenLayout.IsPortrait) { panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, 0); panel.anchoredPosition = new Vector2(0, 8); panel.sizeDelta = new Vector2(Mathf.Min(Width, canvas.x - 16), Mathf.Min(Height, canvas.y * .5f)); }
            else { float top = EconomyUiLayout.SideColumnTop; panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(1, 1); panel.anchoredPosition = new Vector2(-18, -top); panel.sizeDelta = new Vector2(Width, Mathf.Min(Height, canvas.y - top - 54)); }
            var size = panel.sizeDelta;
            Place(reading.rectTransform, 12, 168, Width - 24, Mathf.Max(60, size.y - 168 - 70));
            Place(method.rectTransform, 12, size.y - 64, Width - 24, 58);
            if (!dirty) return;
            dirty = false;
            foreach (var (background, label, id) in stories)
            {
                bool on = id == HillHistoryLayer.StoryId;
                background.color = on ? RegimePanel.ActiveFill : UiFactory.ButtonColor;
                label.color = on ? RegimePanel.ActiveText : GraphStyle.Text;
            }

            if (openLabel != null)
            {
                openLabel.text = HillHistoryLayer.Exploded ? "Close" : "Open";
                openBack.color = HillHistoryLayer.Exploded ? RegimePanel.ActiveFill : UiFactory.ButtonColor;
                openLabel.color = HillHistoryLayer.Exploded ? RegimePanel.ActiveText : GraphStyle.Text;
            }

            reading.text = Reading();
        }

        static string Reading()
        {
            var s = History.Get()?.StoryById(HillHistoryLayer.StoryId);
            if (s == null) return "";
            float year = HillHistoryLayer.Year;
            var sb = new StringBuilder();
            var m = HillHistoryLayer.NearestMoment(s, year, 1.5f);
            string Name(string id) => s.Get(id)?.Name ?? s.PersonById(id)?.Name ?? id;
            sb.Append("<b><size=120%>").Append(HillHistoryLayer.YearText(m?.Year ?? year)).Append(m != null ? " · " + m.Title : "").Append("</size></b>\n");
            if (m != null)
            {
                // who held the advantage over whom is the research's framing of the moment, not a fact: say so
                var head = new StringBuilder("<size=86%><color=#9AA4B2>As the research frames it:</color></size> <color=#" + UiFactory.Hex(HillHistoryLayer.TriadColor(m.Triad)) + ">" + Name(m.Holder) + "</color>");
                if (m.Rivals.Length > 0) head.Append(" against ").Append(string.Join(", ", m.Rivals.Select(Name)));
                if (m.Allies.Length > 0) head.Append(", with ").Append(string.Join(", ", m.Allies.Select(Name)));
                sb.Append(head).Append('\n');
                if (!string.IsNullOrEmpty(m.Advantage)) sb.Append("<b>The advantage:</b> ").Append(m.Advantage).Append('\n');
                if (!string.IsNullOrEmpty(m.Mechanism)) sb.Append("<b>How:</b> ").Append(m.Mechanism).Append('\n');
                if (!string.IsNullOrEmpty(m.Outcome)) sb.Append("<b>What came of it:</b> ").Append(m.Outcome).Append('\n');
                sb.Append("<size=86%><color=#9AA4B2>").Append(m.Facts.Length).Append(m.Facts.Length == 1 ? " fact" : " facts").Append(" behind this moment</color></size>\n");
            }

            var moats = s.Moats.Where(x => year >= x.From - .01f && year <= (Mathf.Approximately(x.To, Mathf.Round(x.To)) ? x.To + 1 : x.To) + .01f).ToList();
            if (moats.Count > 0)
            {
                sb.Append("\n<b>Advantages of the period</b> <size=86%><color=#9AA4B2>(walls 0-4, judged by Jev on all the evidence, some of it later; ? where Jev was unsure)</color></size>\n");
                foreach (var moat in moats)
                {
                    sb.Append("<b>").Append(Name(moat.Actor)).Append("</b>");
                    if (!string.IsNullOrEmpty(moat.Market)) sb.Append(" <size=88%>(").Append(moat.Market).Append(")</size>");
                    sb.Append(": ");
                    sb.Append(string.Join("; ", moat.Sources.GroupBy(x => x.Source).Select(g =>
                    {
                        var x = g.OrderByDescending(v => double.IsNaN(v.Wall) ? -1 : v.Wall).First();
                        return "<color=#" + UiFactory.Hex(HillAdvantageLayer.SourceColor(x.Source)) + ">" + HillHistoryLayer.SourceWord(x.Source) + "</color>" +
                               (double.IsNaN(x.Wall) ? "" : " " + x.Wall.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + (HillHistoryLayer.Uncertain(x) ? "?" : ""));
                    })));
                    sb.Append('\n');
                }
            }

            var periods = s.Periods.Where(p => year >= p.From - .01f && year <= p.To + .01f).ToList();
            if (periods.Count > 0)
            {
                sb.Append("\n<b>In</b> ");
                sb.Append(string.Join(", ", periods.Select(p => p.Title + " (" + Mathf.FloorToInt(p.From) + "-" + Mathf.FloorToInt(p.To) + ")")));
                sb.Append('\n');
                var p0 = periods.OrderBy(p => p.To - p.From).First();
                if (!string.IsNullOrEmpty(p0.What)) sb.Append("<size=92%><color=#9AA4B2>The research's summary:</color> ").Append(p0.What).Append("</size>\n");
            }

            return sb.ToString();
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
