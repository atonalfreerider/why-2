using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Why.UI
{
    /// <summary>
    /// Shared measurements, neutral colors and small builders for the on-screen UI. Sizes are reference
    /// pixels of the 1920x1080 canvas. Hue stays reserved for the three levels of the graph.
    /// </summary>
    public static class HudKit
    {
        /// <summary>Distance of every corner block from the screen edge.</summary>
        public const float Margin = 24f;

        /// <summary>Space between neighboring blocks.</summary>
        public const float Gap = 10f;

        /// <summary>Inner padding of panels.</summary>
        public const float Pad = 12f;

        /// <summary>Sprite pixels-per-unit multiplier: 1.6 turns the factory's 14 px corners into ~9 px.</summary>
        public const float CornerScale = 1.6f;

        public const float SizeSmall = 13f;
        public const float SizeBody = 14f;
        public const float SizeTitle = 18f;

        /// <summary>Faintest neutral, for hints and separators.</summary>
        public static readonly Color TextFaint = new Color(0.40f, 0.41f, 0.45f);

        /// <summary>A thin rounded panel: a faint 1 px rim around a dark fill. Returns the panel's rect (the rim).</summary>
        public static RectTransform FramedPanel(Transform parent, string name, float fillAlpha = 0.86f,
            bool raycast = false)
        {
            Image rim = UiFactory.Panel(parent, name, UiFactory.PanelBorder, raycast);
            rim.pixelsPerUnitMultiplier = CornerScale;
            Color fill = UiFactory.PanelColor;
            fill.a = fillAlpha;
            Image body = UiFactory.Panel(rim.transform, "Fill", fill, false);
            body.pixelsPerUnitMultiplier = CornerScale;
            body.rectTransform.Fill(1, 1, 1, 1);
            return rim.rectTransform;
        }

        /// <summary>A text element that never wraps and never takes input.</summary>
        public static TextMeshProUGUI Line(Transform parent, string name, string text, float size, Color color,
            FontStyles style = FontStyles.Normal, TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
        {
            TextMeshProUGUI t = UiFactory.Text(parent, name, text, size, color, align, style);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        /// <summary>A button whose click never leaves it selected (so Space / arrows keep driving the graph).</summary>
        public static Button Button(Transform parent, string name, string label, float fontSize, Action onClick)
        {
            Button b = UiFactory.Button(parent, name, label, fontSize, () =>
            {
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
                onClick?.Invoke();
            });
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            ((Image)b.targetGraphic).pixelsPerUnitMultiplier = CornerScale;
            return b;
        }

        /// <summary>Preferred size of a text at most <paramref name="maxWidth"/> wide (wrapping if needed).</summary>
        public static Vector2 Measure(TMP_Text t, string text, float maxWidth = 4000f)
        {
            Vector2 size = t.GetPreferredValues(text, maxWidth, 0);
            return new Vector2(Mathf.Min(Mathf.Ceil(size.x) + 1, maxWidth), Mathf.Ceil(size.y));
        }

        /// <summary>Resize a text's rect to its preferred size (wrapped at maxWidth) and return that size.</summary>
        public static Vector2 FitText(TextMeshProUGUI t, float maxWidth = 4000f)
        {
            Vector2 size = Measure(t, t.text, maxWidth);
            t.rectTransform.sizeDelta = size;
            return size;
        }

        /// <summary>Anchor a rect to the top-left of its parent at (x, -y), sized.</summary>
        public static void PlaceTopLeft(RectTransform rt, float x, float y, Vector2 size)
        {
            rt.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -y), size);
        }

        /// <summary>Digit shown for a preset key ("1".."9", "0"); null when the preset has no key.</summary>
        public static string KeyName(KeyCode key)
        {
            if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9) return ((int)(key - KeyCode.Alpha0)).ToString(CultureInfo.InvariantCulture);
            return null;
        }

        /// <summary>"100,000", "1 million", "2.5 million", "1.2 billion".</summary>
        public static string FormatCount(double n)
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            if (n >= 1e9) return (n / 1e9).ToString(n >= 1e10 ? "0" : "0.#", c) + " billion";
            if (n >= 1e6) return (n / 1e6).ToString(n >= 1e7 ? "0" : "0.#", c) + " million";
            if (n >= 1) return Math.Round(n).ToString("N0", c);
            return n.ToString("0.##", c);
        }

        /// <summary>Display name of a level (the tooltip's category line).</summary>
        public static string LevelName(GraphLevel level)
        {
            switch (level)
            {
                case GraphLevel.Matter: return "Matter";
                case GraphLevel.Life: return "Life";
                case GraphLevel.Humans: return "Humans";
                default: return null;
            }
        }

        /// <summary>True while a text input field has keyboard focus (keys must not drive the graph).</summary>
        public static bool TypingInField()
        {
            EventSystem es = EventSystem.current;
            GameObject selected = es != null ? es.currentSelectedGameObject : null;
            if (selected == null) return false;
            TMP_InputField field = selected.GetComponent<TMP_InputField>();
            return field != null && field.isFocused;
        }

        /// <summary>True while the pointer is over an input-taking UI element.</summary>
        public static bool PointerOverUi() =>
            EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
