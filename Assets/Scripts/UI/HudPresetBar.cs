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
    /// </summary>
    public sealed class HudPresetBar
    {
        /// <summary>Height of the bar in reference pixels.</summary>
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
            public Image Background;
            public TextMeshProUGUI Label;
            public Color LabelColor;
        }

        readonly List<Entry> entries = new List<Entry>();
        string activeId;

        /// <summary>The bar's rect (anchored bottom-left, positioned by <see cref="Place"/>).</summary>
        public RectTransform Rect { get; }

        /// <summary>Unscaled width in reference pixels.</summary>
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
                    Background = (Image)button.targetGraphic,
                    Label = label,
                    LabelColor = key != null ? GraphStyle.Text : GraphStyle.TextDim
                };
                label.color = entry.LabelColor;

                // measured in bold so the caption still fits when the preset is active
                label.fontStyle = FontStyles.Bold;
                float width = HudKit.Measure(label, text).x + 2 * ButtonPadX;
                label.fontStyle = FontStyles.Normal;
                ((RectTransform)button.transform).Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(x, 0),
                    new Vector2(width, ButtonHeight));
                x += width + Spacing;

                HoverRelay relay = button.gameObject.AddComponent<HoverRelay>();
                relay.Changed = over =>
                {
                    if (over) HoveredPreset = p;
                    else if (HoveredPreset == p) HoveredPreset = null;
                };
                entries.Add(entry);
            }

            Width = x - Spacing + Inset;
            Rect.Place(Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(Width, Height));
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
    }
}
