using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Why.UI
{
    /// <summary>
    /// The controls sheet (H or ?): a centered panel of columns over a dimmed backdrop (two in the universe,
    /// three in the economy, whose third lists its views); on a portrait screen the columns stack into one.
    /// Views are listed from the preset catalog, so new presets appear automatically: the universe's numbered
    /// ones, and every one of the economy's, by chapter (most have no key, so the sheet is where a viewer
    /// learns they exist). Closes with H, ?, Esc or a click outside.
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
        readonly RectTransform panel, leftColumn, rightColumn, viewsColumn;
        readonly TextMeshProUGUI hint;
        readonly Vector2 hintSize;
        readonly float columnsTop, leftHeight, rightHeight, viewsHeight;

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
            var navigate = Section("Navigate", new[]
            {
                Row("Right drag", "orbit"),
                Row("Middle drag", "pan"),
                Row("Shift + left drag", "pan"),
                Row("Scroll", "zoom toward the cursor"),
                Row("W A S D   Q E", "move"),
                Row("+   -", "zoom in / out"),
                Row("Tab", GraphScene.IsEconomy ? "back to the universe (or ← Universe in the bar)" : "on to the economy (or Economy → in the bar)"),
            });
            if (GraphScene.IsEconomy)
            {
                // the economy's 22 views need a column of their own to read at full size
                viewsColumn = ColumnRect("ViewsColumn");
                leftHeight = Column(leftColumn, new[] { navigate, EconomyGuide() });
                viewsHeight = Column(viewsColumn, EconomyViewSections());
            }
            else leftHeight = Column(leftColumn, new[] { navigate, Section("Views", ViewRows()) });
            rightHeight = Column(rightColumn, GraphScene.IsEconomy ? EconomySections() : new[]
            {
                Section("Time lens", new[]
                {
                    Row("U", "unroll the timeline around what you are looking at; again to roll it back"),
                    Row("[   ]", "unrolled: widen / narrow the time window"),
                    Row("L", "unrolled: cycle log, mixed and linear time"),
                }),
                Section("Guide and details", new[]
                {
                    Row("T", "guided tour: Space or Right next, Left back, P pause, Esc exit; it ends by continuing into the economy"),
                    Row("M", "in the tour: mute / unmute the spoken narration"),
                    Row("F", "show / hide the people alive at the moment you are looking at; click one to follow their life"),
                    Row("Hover a label", "what it is, when, and why it matters"),
                    Row("Click a label", "focus and highlight it"),
                    Row("Esc", "clear the highlight, stop following, close this sheet"),
                    Row("V", "vertical 9:16 window for recording phone videos; again to go back"),
                    Row("H   ?", "this help"),
                    Row("G", "scale probe: point at the red layer to read metres from us and metres per unit"),
                    Row("F3", "frame rate and build stats"),
                }),
            });

            Layout(new Vector2(UiFactory.LandscapeReference.x, UiFactory.LandscapeReference.y), false);

            // created last: a hidden fade deactivates the sheet, and text must be measured while active
            fade = new UiFade(root.gameObject, 0, 7f, true);
        }

        /// <summary>The right column of the economy scene: the hills' own controls instead of the lens and the figures.</summary>
        static (string heading, (string key, string action)[] rows)[] EconomySections()
        {
            return new[]
            {
                Section("The year", new[]
                {
                    Row(",   .", "one year back / forward (with Shift: ten); the wave opens that year"),
                    Row("R", "replay the wave from 1950, year by year, with what changed; again to pause (Shift: from the start)"),
                    Row("Year chip", "play / pause the replay, ‹ › step; drag the scrubber to pick a year; ticks mark turning points"),
                }),
                Section("On the hills", new[]
                {
                    Row("Click a hilltop", "fly to that industry"),
                    Row("Click a gold ring", "fly to that company: its money, its owners and the groups in its industry"),
                    Row("Click a moving dot", "its group of households: who they are, their money, their minds, their encounters"),
                    Row("Double-click a dot", "the simulated people in that group; click one to follow them"),
                    Row("Click a line on the road", "look inside one simulated person (in views 1 and 2 it also opens that year)"),
                    Row("N", "the next notable person (owner, striver, the indebted ...); the person views follow them; with a group open, its next member"),
                    Row("What if? ►", "change one group's reason, fear or the pressure from sellers, and step the years; Reset undoes it"),
                    Row("Explore the hill ►", "the tiers and their industries, down to one company, one group and one person"),
                    Row("Other states", "the tabs choose the government the US hills stand on"),
                    Row("Encounters panel", "views 8 and Betrayal: play the rounds, forgiveness, mistakes, partner choice, betray a tie"),
                    Row("Firm spending", "‹ › step through the firms"),
                    Row("You", "the panel's steppers tell the model about your household"),
                }),
            };
        }

        /// <summary>The economy's guide and details (under Navigate: its right column holds the hills' controls).</summary>
        static (string heading, (string key, string action)[] rows) EconomyGuide() => Section("Guide and details", new[]
        {
            Row("T", "guided tour: Space or Right next, Left back, P pause, Esc exit"),
            Row("Hover a label", "what it is, how large, and where the numbers come from"),
            Row("Click a label", "focus and highlight it"),
            Row("Esc", "close the panel in front, clear the highlight, close this sheet"),
            Row("V", "vertical 9:16 window for recording phone videos; again to go back"),
            Row("H   ?", "this help"),
            Row("F3", "frame rate and build stats"),
            Row("Jev", "an AI model; numbers marked with it are its judgments, not measurements"),
        });

        /// <summary>
        /// Every view of the economy by chapter (<see cref="Economy.HillPresets.Chapters"/>): its caption in the bar with its
        /// number key where it has one, and its title; views the chapters miss are listed last, so none goes unlisted.
        /// </summary>
        static IEnumerable<(string heading, (string key, string action)[] rows)> EconomyViewSections()
        {
            HashSet<string> listed = new HashSet<string>();
            List<(string, (string, string)[])> sections = new List<(string, (string, string)[])>();
            foreach ((string chapter, string[] ids) in Economy.HillPresets.Chapters)
            {
                List<(string, string)> rows = new List<(string, string)>();
                foreach (string id in ids)
                {
                    ViewPreset p = Find(id);
                    if (p == null || !listed.Add(id)) continue;
                    rows.Add(ViewRow(p));
                }

                if (rows.Count > 0) sections.Add(("Views: " + chapter.ToLowerInvariant(), rows.ToArray()));
            }

            List<(string, string)> rest = new List<(string, string)>();
            foreach (ViewPreset p in ViewPresets.All)
            {
                if (listed.Add(p.Id)) rest.Add(ViewRow(p));
            }

            if (rest.Count > 0) sections.Add(("Views: more", rest.ToArray()));
            return sections;

            static ViewPreset Find(string id)
            {
                foreach (ViewPreset p in ViewPresets.All)
                {
                    if (p.Id == id) return p;
                }

                return null;
            }

            static (string, string) ViewRow(ViewPreset p)
            {
                string key = HudKit.KeyName(p.Key);
                return ((key != null ? key + "   " : "") + Economy.HillPresets.ShortTitle(p), p.Title);
            }
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
                // a phone's sheet stacks its columns: the views' list would shrink it past reading, and the bar lists them
                if (viewsColumn != null) viewsColumn.gameObject.SetActive(false);
                width = ColumnWidth + 2 * PanelPad;
                height = columnsTop + leftHeight + rightHeight + PanelPad - SectionGap;
            }
            else
            {
                int columns = viewsColumn != null ? 3 : 2;
                if (viewsColumn != null) viewsColumn.gameObject.SetActive(true);
                leftColumn.anchoredPosition = new Vector2(PanelPad, -columnsTop);
                rightColumn.anchoredPosition = new Vector2(PanelPad + ColumnWidth + ColumnGap, -columnsTop);
                if (viewsColumn != null) viewsColumn.anchoredPosition = new Vector2(PanelPad + 2 * (ColumnWidth + ColumnGap), -columnsTop);
                width = columns * ColumnWidth + (columns - 1) * ColumnGap + 2 * PanelPad;
                height = columnsTop + Mathf.Max(leftHeight, Mathf.Max(rightHeight, viewsHeight)) + PanelPad - SectionGap;
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
