using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Why.UI
{
    /// <summary>
    /// The controls sheet (H or ?): a centered two-column panel over a dimmed backdrop; on a portrait screen
    /// the columns stack into one. Views are listed from the preset catalog, so new numbered presets appear
    /// automatically. Closes with H, ?, Esc or a click outside.
    /// </summary>
    public sealed class HudHelp
    {
        const float PanelPad = 28f;
        const float KeyWidth = 160f;
        const float ActionWidth = 240f;
        const float ColumnWidth = KeyWidth + ActionWidth;
        const float ColumnGap = 40f;
        const float RowGap = 7f;
        const float SectionGap = 16f;

        readonly UiFade fade;
        readonly RectTransform panel, leftColumn, rightColumn;
        readonly TextMeshProUGUI hint;
        readonly Vector2 hintSize;
        readonly float columnsTop, leftHeight, rightHeight;

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
            close.onClick.AddListener(() =>
            {
                HudKit.ReleaseSelection();
                Show(false);
            });

            panel = HudKit.FramedPanel(root, "Panel", 0.94f, true);

            TextMeshProUGUI title = HudKit.Line(panel, "Title", "Exploring the graph", HudKit.SizeTitle,
                GraphStyle.Text, FontStyles.Bold);
            Vector2 titleSize = HudKit.FitText(title);
            HudKit.PlaceTopLeft(title.rectTransform, PanelPad, PanelPad - 4, titleSize);

            hint = HudKit.Line(panel, "Hint", "H, ? or Esc to close", HudKit.SizeSmall, HudKit.TextFaint,
                FontStyles.Normal, TextAlignmentOptions.TopRight);
            hintSize = HudKit.FitText(hint);

            columnsTop = PanelPad + titleSize.y + 18;
            leftColumn = ColumnRect("LeftColumn");
            rightColumn = ColumnRect("RightColumn");
            leftHeight = Column(leftColumn, new[]
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
            rightHeight = Column(rightColumn, new[]
            {
                Section("Time lens", new[]
                {
                    Row("U", "unroll the timeline around what you are looking at; again to roll it back"),
                    Row("[   ]", "unrolled: widen / narrow the time window"),
                    Row("L", "unrolled: cycle log, mixed and linear time"),
                }),
                Section("Guide and details", new[]
                {
                    Row("T", "guided tour: Space or Right next, Left back, P pause, Esc exit"),
                    Row("F", "show / hide the people alive at the moment you are looking at; click one to follow their life"),
                    Row("Hover a label", "what it is, when, and why it matters"),
                    Row("Click a label", "focus and highlight it"),
                    Row("Esc", "clear the highlight, stop following, close this sheet"),
                    Row("V", "vertical 9:16 window for recording phone videos; again to go back"),
                    Row("H   ?", "this help"),
                    Row("F3", "frame rate and build stats"),
                }),
            });

            Layout(new Vector2(UiFactory.LandscapeReference.x, UiFactory.LandscapeReference.y), false);

            // created last: a hidden fade deactivates the sheet, and text must be measured while active
            fade = new UiFade(root.gameObject, 0, 7f, true);
        }

        public void Show(bool show) => fade.Show(show);

        public void Toggle() => fade.Show(!fade.Shown);

        public void Tick(float dt) => fade.Tick(dt);

        /// <summary>
        /// Two columns side by side, or on a portrait screen one above the other; the sheet shrinks to fit a
        /// canvas that is too short for it.
        /// </summary>
        public void Layout(Vector2 canvasSize, bool portrait)
        {
            float width, height;
            if (portrait)
            {
                leftColumn.anchoredPosition = new Vector2(PanelPad, -columnsTop);
                rightColumn.anchoredPosition = new Vector2(PanelPad, -(columnsTop + leftHeight));
                width = ColumnWidth + 2 * PanelPad;
                height = columnsTop + leftHeight + rightHeight + PanelPad - SectionGap;
            }
            else
            {
                leftColumn.anchoredPosition = new Vector2(PanelPad, -columnsTop);
                rightColumn.anchoredPosition = new Vector2(PanelPad + ColumnWidth + ColumnGap, -columnsTop);
                width = 2 * ColumnWidth + ColumnGap + 2 * PanelPad;
                height = columnsTop + Mathf.Max(leftHeight, rightHeight) + PanelPad - SectionGap;
            }

            panel.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, height));
            HudKit.PlaceTopLeft(hint.rectTransform, width - PanelPad - hintSize.x, PanelPad, hintSize);
            float fit = Mathf.Min(1f, (canvasSize.x - 2 * HudKit.Gap) / width, (canvasSize.y - 2 * HudKit.Gap) / height);
            panel.localScale = new Vector3(fit, fit, 1);
        }

        RectTransform ColumnRect(string name)
        {
            RectTransform rt = UiFactory.Rect(panel, name);
            HudKit.PlaceTopLeft(rt, 0, 0, new Vector2(ColumnWidth, 0));
            return rt;
        }

        static (string key, string action) Row(string key, string action) => (key, action);

        static (string heading, (string key, string action)[] rows) Section(string heading,
            (string key, string action)[] rows) => (heading, rows);

        /// <summary>
        /// One row per numbered preset ("1  Everything"), in key order: the catalog is ordered by period, and
        /// keys added later (e.g. 9) sit between others there.
        /// </summary>
        static (string key, string action)[] ViewRows()
        {
            // (key order with 0 last, catalog position as the tie-break: List.Sort is not stable)
            List<(int order, int index, string key, string title)> numbered = new List<(int, int, string, string)>();
            foreach (ViewPreset p in ViewPresets.All)
            {
                string key = HudKit.KeyName(p.Key);
                if (key == null) continue;
                int digit = p.Key - KeyCode.Alpha0;
                numbered.Add((digit == 0 ? 10 : digit, numbered.Count, key, p.Title));
            }

            numbered.Sort((a, b) => a.order != b.order ? a.order.CompareTo(b.order) : a.index.CompareTo(b.index));

            List<(string, string)> rows = new List<(string, string)>();
            foreach ((int order, int index, string key, string title) in numbered) rows.Add((key, title));
            rows.Add(("Bottom bar", "every view, including the unnumbered ones"));
            return rows.ToArray();
        }

        /// <summary>Lay out sections top-down in a column container; returns the height of its content.</summary>
        static float Column(RectTransform column, IEnumerable<(string heading, (string key, string action)[] rows)> sections)
        {
            float y = 0;
            foreach ((string heading, (string key, string action)[] rows) in sections)
            {
                TextMeshProUGUI h = HudKit.Line(column, heading, heading.ToUpperInvariant(), HudKit.SizeSmall - 1,
                    HudKit.TextFaint, FontStyles.Bold);
                h.characterSpacing = 12;
                Vector2 hs = HudKit.FitText(h);
                HudKit.PlaceTopLeft(h.rectTransform, 0, y, hs);
                y += hs.y + 8;

                foreach ((string key, string action) in rows)
                {
                    TextMeshProUGUI k = UiFactory.Text(column, key, key, HudKit.SizeBody, GraphStyle.Text);
                    TextMeshProUGUI a = UiFactory.Text(column, action, action, HudKit.SizeBody, GraphStyle.TextDim);
                    k.richText = a.richText = false;
                    Vector2 ks = HudKit.Measure(k, key, KeyWidth - 12);
                    Vector2 s = HudKit.Measure(a, action, ColumnWidth - KeyWidth);
                    HudKit.PlaceTopLeft(k.rectTransform, 0, y, new Vector2(KeyWidth - 12, ks.y));
                    HudKit.PlaceTopLeft(a.rectTransform, KeyWidth, y, new Vector2(ColumnWidth - KeyWidth, s.y));
                    y += Mathf.Max(ks.y, s.y) + RowGap;
                }

                y += SectionGap;
            }

            return y;
        }
    }
}
