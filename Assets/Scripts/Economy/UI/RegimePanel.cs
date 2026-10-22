using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Why.Economy.Land;
using Why.Economy.Layers;
using Why.Economy.Model;

namespace Why.Economy.UI
{
    /// <summary>
    /// The regimes view's panel ("Other foundations", <see cref="HillRegimeLayer"/>): one button per regime (the United
    /// States, Sweden, China, Saudi Arabia, the Soviet Union around 1980) and one to step through them all, then the shown
    /// regime's numbers in words: what is measured (each with its year; sources in Tools/regimes/sources.json), what Jev
    /// judged, and what the drawing chooses. Buttons, not keys, so it works by pinch in the headset. Shown in the regimes
    /// view, also during the tour (the stop steps through the regimes unless one is pinned).
    /// </summary>
    [GraphScenes(EconomyLayouts.HillsScene)]
    public sealed class RegimePanel : GraphModule
    {
        const float Width = 440, Height = 700;
        GraphRoot root;
        RectTransform panel;
        TextMeshProUGUI reading, drawing;
        readonly List<(Image background, TextMeshProUGUI label, int regime)> buttons = new List<(Image, TextMeshProUGUI, int)>();
        Image cycle; TextMeshProUGUI cycleLabel;
        string shownReading;
        int shownRegime = -2, shownPinned = -2;

        public override void Init(GraphRoot graphRoot)
        {
            root = graphRoot;
            Canvas canvas = UiFactory.CreateCanvas("Other foundations", 47, transform);
            panel = UiFactory.Panel(canvas.transform, "Regimes", new Color(.02f, .03f, .05f, .94f)).rectTransform;
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(1, 1);
            panel.anchoredPosition = new Vector2(-18, -176); panel.sizeDelta = new Vector2(Width, Height);
            var title = Text("Title", 15, 12, 10, Width - 24, 22); title.text = "<b>OTHER FOUNDATIONS</b>";
            var note = Text("Note", 10.5f, 12, 32, Width - 24, 30); note.textWrappingMode = TextWrappingModes.Normal; note.color = new Color(.7f, .76f, .84f);
            note.text = "The same US hills on another state. Pick a regime; its measured structure redraws the bedrock, the summits and the strands.";
            var step = UiFactory.Button(panel, "Cycle", "step through all", 11, () => HillRegimeLayer.Pinned = -1);
            Place((RectTransform)step.transform, 12, 98, Width - 24, 22);
            cycle = (Image)step.targetGraphic; cycleLabel = step.GetComponentInChildren<TextMeshProUGUI>();
            reading = Text("Reading", 11.5f, 12, 128, Width - 24, 420); reading.textWrappingMode = TextWrappingModes.Normal; reading.overflowMode = TextOverflowModes.Overflow;
            drawing = Text("Drawing", 10.5f, 12, Height - 128, Width - 24, 118); drawing.textWrappingMode = TextWrappingModes.Normal; drawing.overflowMode = TextOverflowModes.Overflow;
            drawing.color = new Color(.74f, .79f, .86f);
        }

        /// <summary>One button per regime, once the regimes are loaded.</summary>
        void MakeButtons(Regimes regimes)
        {
            int n = regimes.All.Length; float gap = 4, w = (Width - 24 - gap * (n - 1)) / Mathf.Max(1, n);
            for (int i = 0; i < n; i++)
            {
                int k = i; var r = regimes.All[i];
                var b = UiFactory.Button(panel, "Regime " + r.Id, r.Id == "su" ? "USSR 1980" : r.Name, 11, () => HillRegimeLayer.Pinned = k);
                Place((RectTransform)b.transform, 12 + i * (w + gap), 68, w, 24);
                buttons.Add(((Image)b.targetGraphic, b.GetComponentInChildren<TextMeshProUGUI>(), k));
            }
        }

        void Update()
        {
            if (!root || !root.IsLoaded) return;
            bool show = LandView.PresetId == HillRegimeLayer.View;
            panel.gameObject.SetActive(show);
            if (!show) return;
            var regimes = Regimes.Get();
            if (regimes != null && buttons.Count == 0) MakeButtons(regimes);
            Vector2 canvas = UiFactory.CanvasSize;
            bool portrait = ScreenLayout.IsPortrait;
            if (portrait) { panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, 0); panel.anchoredPosition = new Vector2(0, 8); panel.sizeDelta = new Vector2(Mathf.Min(Width, canvas.x - 16), Mathf.Min(Height, canvas.y * .55f)); }
            else { panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(1, 1); panel.anchoredPosition = new Vector2(-18, -176); panel.sizeDelta = new Vector2(Width, Mathf.Min(Height, canvas.y - 230)); }
            reading.fontSize = portrait ? 9 : 11.5f; drawing.fontSize = portrait ? 8.5f : 10.5f;
            // the drawing's choices sit at the panel's foot, below the reading
            Place(drawing.rectTransform, 12, panel.sizeDelta.y - 124, panel.sizeDelta.x - 24, 118);
            if (shownRegime != HillRegimeLayer.Shown || shownPinned != HillRegimeLayer.Pinned)
            {
                shownRegime = HillRegimeLayer.Shown; shownPinned = HillRegimeLayer.Pinned;
                foreach (var b in buttons)
                {
                    bool active = b.regime == shownRegime;
                    b.background.color = active ? UiFactory.ButtonActive : UiFactory.ButtonColor;
                    b.label.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
                    b.label.color = active ? Color.white : GraphStyle.Text;
                }
                bool cycling = HillRegimeLayer.Pinned < 0;
                cycle.color = cycling ? UiFactory.ButtonActive : UiFactory.ButtonColor;
                cycleLabel.text = cycling ? "stepping through all (pick one to stay)" : "step through all";
            }
            if (shownReading != HillRegimeLayer.ReadoutText) { shownReading = HillRegimeLayer.ReadoutText; reading.text = shownReading; drawing.text = HillRegimeLayer.DrawingText; }
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
