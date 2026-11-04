using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Why.Economy.Land;
using Why.Economy.Layers;

namespace Why.Economy.UI
{
    /// <summary>
    /// The mobility view's switch of whom the ladder counts (<see cref="HillMobilityLayer.AllAdults"/>): adults 25 and
    /// older ranked among themselves, as the tax records rank them (the default, which the records' outlines and dot plots
    /// compare like for like), or every adult, young adults leaving home included. Buttons, not keys, so it works by pinch
    /// in the headset. Bottom right, clear of the ladder's labels; hidden during the tour, which shows the default.
    /// </summary>
    [GraphScenes(EconomyLayouts.HillsScene)]
    public sealed class MobilityPanel : GraphModule
    {
        const float Width = 300, Height = 84;
        GraphRoot root;
        RectTransform panel;
        Image like, all;
        TextMeshProUGUI likeLabel, allLabel;
        int shownState = -1;

        public override void Init(GraphRoot graphRoot)
        {
            root = graphRoot;
            Canvas canvas = UiFactory.CreateCanvas("Who the ladder counts", 47, transform);
            panel = UiFactory.Panel(canvas.transform, "Mobility", new Color(.02f, .03f, .05f, .94f)).rectTransform;
            var title = UiFactory.Text(panel, "Title", "<b>WHO THE LADDER COUNTS</b>", 11, GraphStyle.Text);
            Place(title.rectTransform, 12, 7, Width - 24, 18);
            var a = UiFactory.Button(panel, "Like for like", "Adults 25+, ranked as the tax records rank", 10.5f, () => HillMobilityLayer.AllAdults = false);
            Place((RectTransform)a.transform, 12, 28, Width - 24, 22);
            like = (Image)a.targetGraphic; likeLabel = a.GetComponentInChildren<TextMeshProUGUI>();
            var b = UiFactory.Button(panel, "All adults", "All adults, young adults leaving home too", 10.5f, () => HillMobilityLayer.AllAdults = true);
            Place((RectTransform)b.transform, 12, 54, Width - 24, 22);
            all = (Image)b.targetGraphic; allLabel = b.GetComponentInChildren<TextMeshProUGUI>();
        }

        void Update()
        {
            if (!root || !root.IsLoaded) return;
            bool show = LandView.PresetId == "mobility" && !root.TourActive;
            panel.gameObject.SetActive(show);
            if (!show) return;
            // portrait: above the preset bar in the middle; landscape: the bottom right corner, below the ladder's labels
            if (ScreenLayout.IsPortrait) { panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, 0); panel.anchoredPosition = new Vector2(0, 64); }
            else { panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(1, 0); panel.anchoredPosition = new Vector2(-18, 100); }
            panel.sizeDelta = new Vector2(Width, Height);
            int state = HillMobilityLayer.AllAdults ? 1 : 0;
            if (state == shownState) return;
            shownState = state;
            Style(like, likeLabel, state == 0); Style(all, allLabel, state == 1);
        }

        static void Style(Image background, TextMeshProUGUI label, bool active)
        {
            background.color = active ? RegimePanel.ActiveFill : UiFactory.ButtonColor;
            label.color = active ? RegimePanel.ActiveText : GraphStyle.Text;
            label.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
        }

        static void Place(RectTransform r, float x, float y, float width, float height)
        {
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1); r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(width, height);
        }
    }
}
