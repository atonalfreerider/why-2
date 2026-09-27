using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Why.UI
{
    /// <summary>
    /// The controls sheet (H or ?): a centered two-column panel over a dimmed backdrop. Views are listed
    /// from the preset catalog, so new numbered presets appear automatically. Closes with H, ?, Esc or a
    /// click outside.
    /// </summary>
    public sealed class HudHelp
    {
        const float PanelPad = 28f;
        const float KeyWidth = 160f;
        const float ActionWidth = 240f;
        const float ColumnGap = 40f;
        const float RowGap = 7f;
        const float SectionGap = 16f;

        readonly UiFade fade;

        /// <summary>True while the sheet is open (or opening).</summary>
        public bool Open => fade.Shown;

        public HudHelp(Transform parent)
        {
            RectTransform root = UiFactory.Rect(parent, "Help").Fill();

            // clicking the dimmed backdrop closes the sheet
            Image backdrop = UiFactory.Rect(root, "Backdrop").Fill().gameObject.AddComponent<Image>();
            backdrop.color = new Color(0, 0, 0, 0.45f);
            Button close = backdrop.gameObject.AddComponent<Button>();
            close.transition = Selectable.Transition.None;
            close.navigation = new Navigation { mode = Navigation.Mode.None };
            close.onClick.AddListener(() => Show(false));

            RectTransform panel = HudKit.FramedPanel(root, "Panel", 0.94f, true);
            float columnWidth = KeyWidth + ActionWidth;

            TextMeshProUGUI title = HudKit.Line(panel, "Title", "Exploring the graph", HudKit.SizeTitle,
                GraphStyle.Text, FontStyles.Bold);
            Vector2 titleSize = HudKit.FitText(title);
            HudKit.PlaceTopLeft(title.rectTransform, PanelPad, PanelPad - 4, titleSize);

            TextMeshProUGUI hint = HudKit.Line(panel, "Hint", "H, ? or Esc to close", HudKit.SizeSmall, HudKit.TextFaint,
                FontStyles.Normal, TextAlignmentOptions.TopRight);
            Vector2 hintSize = HudKit.FitText(hint);

            float top = PanelPad + titleSize.y + 18;
            float left = Column(panel, PanelPad, top, columnWidth, new[]
            {
                Section("Navigate", new[]
                {
                    Row("Right drag", "orbit"),
                    Row("Middle drag", "pan"),
                    Row("Shift + left drag", "pan"),
                    Row("Scroll", "zoom toward the cursor"),
                    Row("W A S D   Q E", "move"),
                    Row("+   -", "zoom in / out"),
                }),
                Section("Views", ViewRows()),
            });
            float right = Column(panel, PanelPad + columnWidth + ColumnGap, top, columnWidth, new[]
            {
                Section("Time lens", new[]
                {
                    Row("U", "unroll the timeline around what you are looking at; again to roll it back"),
                    Row("[   ]", "widen / narrow the unrolled time window"),
                    Row("L", "cycle log, mixed and linear time"),
                }),
                Section("Guide and details", new[]
                {
                    Row("T", "guided tour: Space or Right next, Left back, P pause, Esc exit"),
                    Row("F", "famous people alive at the moment you are looking at; click one to follow their life"),
                    Row("Hover a label", "what it is, when, and why it matters"),
                    Row("Click a label", "focus and highlight it"),
                    Row("Esc", "clear the highlight, close this sheet"),
                    Row("H   ?", "this help"),
                    Row("F3", "frame rate and build stats"),
                }),
            });

            float width = 2 * columnWidth + ColumnGap + 2 * PanelPad;
            float height = Mathf.Max(left, right) + PanelPad - SectionGap;
            panel.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, height));
            HudKit.PlaceTopLeft(hint.rectTransform, width - PanelPad - hintSize.x, PanelPad, hintSize);

            // created last: a hidden fade deactivates the sheet, and text must be measured while active
            fade = new UiFade(root.gameObject, 0, 7f, true);
        }

        public void Show(bool show) => fade.Show(show);

        public void Toggle() => fade.Show(!fade.Shown);

        public void Tick(float dt) => fade.Tick(dt);

        static (string key, string action) Row(string key, string action) => (key, action);

        static (string heading, (string key, string action)[] rows) Section(string heading,
            (string key, string action)[] rows) => (heading, rows);

        /// <summary>One row per numbered preset: "1  Everything - From the Big Bang to this moment".</summary>
        static (string key, string action)[] ViewRows()
        {
            List<(string, string)> rows = new List<(string, string)>();
            foreach (ViewPreset p in ViewPresets.All)
            {
                string key = HudKit.KeyName(p.Key);
                if (key != null) rows.Add((key, p.Title));
            }

            rows.Add(("Bottom bar", "every view, including the unnumbered ones"));
            return rows.ToArray();
        }

        /// <summary>Lay out sections top-down in one column; returns the y below the last row.</summary>
        static float Column(RectTransform panel, float x, float y, float width,
            IEnumerable<(string heading, (string key, string action)[] rows)> sections)
        {
            foreach ((string heading, (string key, string action)[] rows) in sections)
            {
                TextMeshProUGUI h = HudKit.Line(panel, heading, heading.ToUpperInvariant(), HudKit.SizeSmall - 1,
                    HudKit.TextFaint, FontStyles.Bold);
                h.characterSpacing = 12;
                Vector2 hs = HudKit.FitText(h);
                HudKit.PlaceTopLeft(h.rectTransform, x, y, hs);
                y += hs.y + 8;

                foreach ((string key, string action) in rows)
                {
                    TextMeshProUGUI k = UiFactory.Text(panel, key, key, HudKit.SizeBody, GraphStyle.Text);
                    TextMeshProUGUI a = UiFactory.Text(panel, action, action, HudKit.SizeBody, GraphStyle.TextDim);
                    k.richText = a.richText = false;
                    Vector2 ks = HudKit.Measure(k, key, KeyWidth - 12);
                    Vector2 s = HudKit.Measure(a, action, width - KeyWidth);
                    HudKit.PlaceTopLeft(k.rectTransform, x, y, new Vector2(KeyWidth - 12, ks.y));
                    HudKit.PlaceTopLeft(a.rectTransform, x + KeyWidth, y, new Vector2(width - KeyWidth, s.y));
                    y += Mathf.Max(ks.y, s.y) + RowGap;
                }

                y += SectionGap;
            }

            return y;
        }
    }
}
