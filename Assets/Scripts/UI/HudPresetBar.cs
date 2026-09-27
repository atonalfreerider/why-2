using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Why.UI
{
    /// <summary>
    /// One button per view preset, in the catalog's (chronological) order, with the number key where the
    /// preset has one. Numbered presets are the main stops and read brighter; the active preset is lit.
    /// Landscape shows one row; a portrait screen gets the same buttons in balanced, full-width rows
    /// (<see cref="Flow"/>), since a recorded video cannot be scrolled.
    /// </summary>
    public sealed class HudPresetBar
    {
        /// <summary>Height of the bar in reference pixels (one row).</summary>
        public const float Height = 36f;

        const float ButtonHeight = 28f;
        const float ButtonPadX = 11f;
        const float Spacing = 3f;
        const float Inset = 4f;
        const float FontSize = HudKit.SizeSmall;

        /// <summary>Captions for presets whose titles are too long for the bar (the tooltip shows the full title).</summary>
        static readonly Dictionary<string, string> ShortTitles = new Dictionary<string, string>
        {
            { "smv", "United States" },
            { "present", "Present" },
        };

        sealed class Entry
        {
            public ViewPreset Preset;
            public RectTransform Rect;
            public Image Background;
            public TextMeshProUGUI Label;
            public Color LabelColor;

            /// <summary>Natural (single-row) width.</summary>
            public float Width;
        }

        readonly List<Entry> entries = new List<Entry>();
        readonly int[] rowOf;
        int rowCount;
        string activeId;

        /// <summary>The bar's rect (anchored bottom-left, positioned by <see cref="Place"/>).</summary>
        public RectTransform Rect { get; }

        /// <summary>Unscaled single-row width in reference pixels.</summary>
        public float Width { get; }

        /// <summary>The preset whose button is under the pointer, if any.</summary>
        public ViewPreset HoveredPreset { get; private set; }

        public HudPresetBar(Transform parent, Action<ViewPreset> onClick)
        {
            Rect = HudKit.FramedPanel(parent, "PresetBar", 0.8f);
            string keyColor = UiFactory.Hex(HudKit.TextFaint);

            float x = Inset;
            foreach (ViewPreset preset in ViewPresets.All)
            {
                ViewPreset p = preset;
                string key = HudKit.KeyName(p.Key);
                string caption = ShortTitles.TryGetValue(p.Id, out string shortTitle) ? shortTitle : p.Title;
                string text = key != null ? "<color=#" + keyColor + ">" + key + "</color>  " + caption : caption;

                Button button = HudKit.Button(Rect, "Preset " + p.Id, text, FontSize, () => onClick(p));
                TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
                Entry entry = new Entry
                {
                    Preset = p,
                    Rect = (RectTransform)button.transform,
                    Background = (Image)button.targetGraphic,
                    Label = label,
                    LabelColor = key != null ? GraphStyle.Text : GraphStyle.TextDim
                };
                label.color = entry.LabelColor;

                // measured in bold so the caption still fits when the preset is active
                label.fontStyle = FontStyles.Bold;
                entry.Width = HudKit.Measure(label, text).x + 2 * ButtonPadX;
                label.fontStyle = FontStyles.Normal;
                x += entry.Width + Spacing;

                HoverRelay relay = button.gameObject.AddComponent<HoverRelay>();
                relay.Changed = over =>
                {
                    if (over) HoveredPreset = p;
                    else if (HoveredPreset == p) HoveredPreset = null;
                };
                entries.Add(entry);
            }

            rowOf = new int[entries.Count];
            Width = x - Spacing + Inset;
            Rect.Place(Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(Width, Height));
            SingleRow();
        }

        /// <summary>Light the button of the active preset (none for free-form lens views).</summary>
        public void SetActive(ViewPreset preset)
        {
            string id = preset != null ? preset.Id : null;
            if (id == activeId) return;
            activeId = id;
            foreach (Entry e in entries)
            {
                bool active = e.Preset.Id == id;
                e.Background.color = active ? UiFactory.ButtonActive : UiFactory.ButtonColor;
                e.Label.color = active ? Color.white : e.LabelColor;
                e.Label.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
            }
        }

        /// <summary>Position the bar's bottom-left corner (canvas units) at a uniform scale.</summary>
        public void Place(float left, float bottom, float scale)
        {
            Rect.anchoredPosition = new Vector2(Mathf.Round(left), Mathf.Round(bottom));
            Rect.localScale = new Vector3(scale, scale, 1);
        }

        /// <summary>Landscape: every button at its natural width on one line (<see cref="Width"/> x <see cref="Height"/>).</summary>
        public void SingleRow()
        {
            float x = Inset;
            foreach (Entry e in entries)
            {
                PlaceButton(e.Rect, x, Inset, e.Width);
                x += e.Width + Spacing;
            }

            Rect.sizeDelta = new Vector2(Width, Height);
        }

        /// <summary>
        /// Portrait: breaks the buttons (in catalog order) into the fewest balanced rows that fit maxWidth and
        /// widens every row to the full width, so the bar reads as one deliberate block. Returns its height.
        /// </summary>
        public float Flow(float maxWidth)
        {
            float inner = Mathf.Max(1f, maxWidth - 2 * Inset);
            float total = -Spacing;
            foreach (Entry e in entries) total += e.Width + Spacing;

            int rows = Mathf.Max(1, Mathf.CeilToInt(total / inner));
            while (rows <= entries.Count && !BreakRows(rows, inner, total / rows)) rows++;
            if (rows > entries.Count)
            {
                // a caption wider than the screen: one button per row, clipped to the width
                for (int i = 0; i < rowOf.Length; i++) rowOf[i] = i;
                rowCount = entries.Count;
            }

            float top = Inset;
            for (int start = 0, row = 0; row < rowCount; row++)
            {
                int end = start;
                float natural = -Spacing;
                while (end < entries.Count && rowOf[end] == row)
                {
                    natural += entries[end].Width + Spacing;
                    end++;
                }

                float extra = end > start ? Mathf.Max(0, inner - natural) / (end - start) : 0;
                float x = Inset;
                for (int i = start; i < end; i++)
                {
                    float w = Mathf.Min(entries[i].Width + extra, inner);
                    PlaceButton(entries[i].Rect, x, top, w);
                    x += w + Spacing;
                }

                top += ButtonHeight + Spacing;
                start = end;
            }

            float height = 2 * Inset + rowCount * ButtonHeight + (rowCount - 1) * Spacing;
            Rect.sizeDelta = new Vector2(maxWidth, height);
            return height;
        }

        /// <summary>
        /// Greedy line breaking toward a target row width: a button starts a new row when it would overflow, or
        /// would end its row further past the target than the row already falls short of it (while rows are
        /// left). Fills <see cref="rowOf"/>; false when the buttons do not fit in this many rows.
        /// </summary>
        bool BreakRows(int rows, float inner, float target)
        {
            int row = 0;
            float x = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                float w = entries[i].Width;
                if (w > inner) return false;
                if (x > 0)
                {
                    float next = x + Spacing + w;
                    bool overflow = next > inner;
                    bool pastTarget = next - target > target - x;
                    if (overflow || (pastTarget && row < rows - 1))
                    {
                        row++;
                        x = 0;
                        if (row >= rows) return false;
                    }
                }

                x = x > 0 ? x + Spacing + w : w;
                rowOf[i] = row;
            }

            rowCount = row + 1;
            return true;
        }

        static void PlaceButton(RectTransform rt, float x, float yFromTop, float width)
        {
            rt.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -yFromTop), new Vector2(width, ButtonHeight));
        }
    }
}
