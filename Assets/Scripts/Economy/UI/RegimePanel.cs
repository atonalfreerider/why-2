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
    /// The regimes view's panel ("What if: another government", <see cref="HillRegimeLayer"/>): one button per regime
    /// (the United States, Sweden, China, Saudi Arabia, the Soviet Union around 1980) and one to step through them all,
    /// then the shown regime in three plain lines (who owns the biggest firms, who pays for the state, who chooses the government) and
    /// its numbers in words: what is measured (each with its year), what Jev judged. The drawing's choices and the
    /// sources (files, model versions, links) stay folded under "Sources &amp; method". Buttons, not keys, so it works by
    /// pinch in the headset. Shown in the regimes view, also during the tour (the stop's cues pin each regime as its card
    /// names it).
    /// </summary>
    [GraphScenes(EconomyLayouts.HillsScene)]
    public sealed class RegimePanel : GraphModule
    {
        const float Width = 440, Height = 780, MethodHeight = 22;
        /// <summary>The selected button: a light slate fill under near-black text (UiFactory.Button doubles the fill, so it
        /// shows about (.84, .90, 1)), far above 4.5:1 where white on the default light grey was under 2:1.</summary>
        public static readonly Color ActiveFill = new Color(.42f, .45f, .50f, .5f), ActiveText = new Color(.04f, .05f, .07f);
        GraphRoot root;
        RectTransform panel, methodButton;
        TextMeshProUGUI reading, drawing, methodLabel;
        readonly List<(Image background, TextMeshProUGUI label, int regime)> buttons = new List<(Image, TextMeshProUGUI, int)>();
        Image cycle; TextMeshProUGUI cycleLabel;
        string shownReading, laidOut;
        int shownRegime = -2, shownPinned = -2;
        bool methodOpen;

        public override void Init(GraphRoot graphRoot)
        {
            root = graphRoot;
            Canvas canvas = UiFactory.CreateCanvas("What if: another government", 47, transform);
            panel = UiFactory.Panel(canvas.transform, "Regimes", new Color(.02f, .03f, .05f, .94f)).rectTransform;
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(1, 1);
            panel.anchoredPosition = new Vector2(-18, -176); panel.sizeDelta = new Vector2(Width, Height);
            var title = Text("Title", 15, 12, 10, Width - 24, 22); title.text = "<b>WHAT IF: ANOTHER GOVERNMENT</b>";
            var note = Text("Note", 10.5f, 12, 32, Width - 24, 30); note.textWrappingMode = TextWrappingModes.Normal; note.color = new Color(.7f, .76f, .84f);
            note.text = "A thought experiment: the US hills on another state's foundation. Pick a state; its measured structure redraws the bedrock, the summits and the strands.";
            var step = UiFactory.Button(panel, "Cycle", "step through all", 11, () => HillRegimeLayer.Pinned = -1);
            Place((RectTransform)step.transform, 12, 98, Width - 24, 22);
            cycle = (Image)step.targetGraphic; cycleLabel = step.GetComponentInChildren<TextMeshProUGUI>();
            reading = Text("Reading", 11.5f, 12, 128, Width - 24, 420); reading.textWrappingMode = TextWrappingModes.Normal; reading.overflowMode = TextOverflowModes.Overflow;
            var method = UiFactory.Button(panel, "Method", "", 10.5f, () => { methodOpen = !methodOpen; laidOut = null; });
            methodButton = (RectTransform)method.transform; methodLabel = method.GetComponentInChildren<TextMeshProUGUI>();
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
            else { float top = EconomyUiLayout.SideColumnTop; panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(1, 1); panel.anchoredPosition = new Vector2(-18, -top); panel.sizeDelta = new Vector2(Width, Mathf.Min(Height, canvas.y - top - 54)); }
            Layout(portrait);
            if (shownRegime != HillRegimeLayer.Shown || shownPinned != HillRegimeLayer.Pinned)
            {
                shownRegime = HillRegimeLayer.Shown; shownPinned = HillRegimeLayer.Pinned;
                foreach (var b in buttons)
                {
                    bool active = b.regime == shownRegime;
                    b.background.color = active ? ActiveFill : UiFactory.ButtonColor;
                    b.label.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
                    b.label.color = active ? ActiveText : GraphStyle.Text;
                }
                bool cycling = HillRegimeLayer.Pinned < 0;
                cycle.color = cycling ? ActiveFill : UiFactory.ButtonColor;
                cycleLabel.color = cycling ? ActiveText : GraphStyle.Text;
                cycleLabel.text = cycling ? "stepping through all (pick one to stay)" : "step through all";
            }
            if (shownReading != HillRegimeLayer.ReadoutText) { shownReading = HillRegimeLayer.ReadoutText; reading.text = shownReading; drawing.text = HillRegimeLayer.DrawingText; laidOut = null; }
        }

        /// <summary>The reading under the buttons, the "Sources &amp; method" button right below it and, when that is open,
        /// the drawing's choices and sources, by their measured heights; the type shrinks (to three quarters at most)
        /// until what is open fits the panel.</summary>
        void Layout(bool portrait)
        {
            string key = shownReading + "|" + panel.sizeDelta + "|" + portrait + "|" + methodOpen;
            if (key == laidOut) return;
            laidOut = key;
            methodLabel.text = methodOpen ? "Hide sources & method" : "Show sources & method";
            drawing.gameObject.SetActive(methodOpen);
            float w = panel.sizeDelta.x - 24, top = 128, room = panel.sizeDelta.y - top - 12 - MethodHeight - 8;
            float readingSize = portrait ? 9 : 11.5f, drawingSize = portrait ? 8.5f : 10.5f, scale = 1, hr = 0, hd = 0;
            for (int i = 0; i < 6; i++, scale -= .05f)
            {
                reading.fontSize = readingSize * scale; drawing.fontSize = drawingSize * scale;
                hr = reading.GetPreferredValues(reading.text, w, 0).y; hd = methodOpen ? drawing.GetPreferredValues(drawing.text, w, 0).y : 0;
                if (hr + hd + (methodOpen ? 8 : 0) <= room) break;
            }
            Place(reading.rectTransform, 12, top, w, hr);
            Place(methodButton, 12, top + hr + 6, w, MethodHeight);
            Place(drawing.rectTransform, 12, top + hr + 6 + MethodHeight + 8, w, hd);
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
