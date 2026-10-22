using TMPro;
using UnityEngine;
using Why.Economy.Land;
using Why.Economy.Layers;

namespace Why.Economy.UI
{
    /// <summary>
    /// The strategy view's readout ("Companies spend too", <see cref="HillStrategyLayer"/>): the focused firm's year of
    /// money in words (what it ran the business on and who that paid, what it invested and whether fear or desire drove
    /// it, its owners, taxes and lobbying, which way Jev judged its money moves it, and what it says it is doing), its
    /// trail in numbers (each year 2015-2025: investment and research, owners, fear and AI shares, the way its money moved
    /// it), buttons to step through the firms from the largest spender down, and the US totals they stand against. Text in
    /// screen space, so it reads at any distance; the 3D view keeps only names, arrows and the trail's turns. The three
    /// blocks stack by their measured heights. Shown in the strategy view.
    /// </summary>
    [GraphScenes(EconomyLayouts.HillsScene)]
    public sealed class StrategyPanel : GraphModule
    {
        GraphRoot root;
        RectTransform panel;
        TextMeshProUGUI firm, history, totals;
        string shownFocus, shownHistory, shownTotals;
        bool shownPortrait;

        public override void Init(GraphRoot graphRoot)
        {
            root = graphRoot;
            Canvas canvas = UiFactory.CreateCanvas("Companies spend too", 47, transform);
            panel = UiFactory.Panel(canvas.transform, "Strategy", new Color(.02f, .03f, .05f, .94f)).rectTransform;
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(1, 1);
            panel.anchoredPosition = new Vector2(-18, -176); panel.sizeDelta = new Vector2(420, 640);
            var title = Text("Title", 15, 12, 10, 396, 22); title.text = "<b>COMPANIES SPEND TOO</b>";
            var note = Text("Note", 10.5f, 12, 32, 396, 30); note.textWrappingMode = TextWrappingModes.Normal; note.color = new Color(.7f, .76f, .84f);
            note.text = "Each firm's latest fiscal year from its audited filing; directions and motives judged by Jev (model evidence, not measurement).";
            var previous = UiFactory.Button(panel, "Previous", "‹ larger", 11, () => Step(-1)); Place((RectTransform)previous.transform, 12, 66, 120, 24);
            var next = UiFactory.Button(panel, "Next", "smaller ›", 11, () => Step(1)); Place((RectTransform)next.transform, 288, 66, 120, 24);
            var free = UiFactory.Button(panel, "Follow", "largest investor", 11, () => HillStrategyLayer.Pinned = -1); Place((RectTransform)free.transform, 150, 66, 120, 24);
            firm = Text("Firm", 12, 12, 98, 396, 330); firm.textWrappingMode = TextWrappingModes.Normal; firm.overflowMode = TextOverflowModes.Overflow;
            history = Text("History", 10.5f, 12, 432, 396, 180); history.textWrappingMode = TextWrappingModes.Normal; history.overflowMode = TextOverflowModes.Overflow;
            totals = Text("Totals", 11, 12, 616, 396, 200); totals.textWrappingMode = TextWrappingModes.Normal; totals.overflowMode = TextOverflowModes.Overflow;
            totals.color = new Color(.78f, .83f, .9f);
        }

        /// <summary>Pins the next larger (-1) or smaller (+1) spender after the focused firm.</summary>
        static void Step(int d)
        {
            var ranked = HillStrategyLayer.Ranked; if (ranked.Count == 0) return;
            int k = ranked.IndexOf(HillStrategyLayer.Focused);
            HillStrategyLayer.Pinned = ranked[k < 0 ? 0 : (k + d + ranked.Count) % ranked.Count];
        }

        void Update()
        {
            if (!root || !root.IsLoaded) return;
            bool show = LandView.PresetId == "strategy";   // also during the tour: the firm's numbers are the stop's evidence
            panel.gameObject.SetActive(show);
            if (!show) return;
            Vector2 canvas = UiFactory.CanvasSize;
            bool portrait = ScreenLayout.IsPortrait;
            firm.fontSize = portrait ? 9.5f : 12; history.fontSize = portrait ? 8.5f : 10.5f; totals.fontSize = portrait ? 9 : 11;
            bool changed = portrait != shownPortrait; shownPortrait = portrait;
            if (shownFocus != HillStrategyLayer.FocusText) { shownFocus = HillStrategyLayer.FocusText; firm.text = string.IsNullOrEmpty(shownFocus) ? "Point at a company line or step through the firms." : shownFocus; changed = true; }
            if (shownHistory != HillStrategyLayer.HistoryText) { shownHistory = HillStrategyLayer.HistoryText; history.text = shownHistory; changed = true; }
            if (shownTotals != HillStrategyLayer.ReadoutText) { shownTotals = HillStrategyLayer.ReadoutText; totals.text = shownTotals; changed = true; }
            float width = portrait ? Mathf.Min(440, canvas.x - 16) : 420, inner = width - 24;
            if (changed) Stack(inner);
            float content = totals.rectTransform.anchoredPosition.y * -1 + totals.rectTransform.sizeDelta.y + 12;
            if (portrait) { panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, 0); panel.anchoredPosition = new Vector2(0, 8); panel.sizeDelta = new Vector2(width, Mathf.Min(content, canvas.y * .62f)); }
            else { panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(1, 1); panel.anchoredPosition = new Vector2(-18, -176); panel.sizeDelta = new Vector2(width, Mathf.Min(content, canvas.y - 196)); }
        }

        /// <summary>Stacks the firm's text, its trail's rows and the totals under the buttons by their measured heights.</summary>
        void Stack(float width)
        {
            float y = 98;
            foreach (var t in new[] { firm, history, totals })
            {
                float h = string.IsNullOrEmpty(t.text) ? 0 : t.GetPreferredValues(t.text, width, 10000).y;
                Place(t.rectTransform, 12, y, width, h);
                if (h > 0) y += h + 10;
            }
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
