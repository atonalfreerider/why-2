using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Why
{
    /// <summary>
    /// Builds uGUI at runtime so modules need no prefabs or scene wiring. All UI is neutral grey/white:
    /// hue is reserved for the three levels of the graph (the legend is the only exception).
    /// </summary>
    public static class UiFactory
    {
        public static readonly Color PanelColor = new Color(0.035f, 0.038f, 0.05f, 0.86f);
        public static readonly Color PanelBorder = new Color(1f, 1f, 1f, 0.08f);
        public static readonly Color ButtonColor = new Color(1f, 1f, 1f, 0.06f);
        public static readonly Color ButtonHover = new Color(1f, 1f, 1f, 0.14f);
        public static readonly Color ButtonActive = new Color(1f, 1f, 1f, 0.24f);

        static Sprite rounded;

        /// <summary>A 9-sliced rounded rectangle sprite generated once.</summary>
        public static Sprite Rounded
        {
            get
            {
                if (rounded != null) return rounded;
                const int size = 64, radius = 14;
                Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "WhyRounded", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
                };
                Color32[] px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = Mathf.Max(0, Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius)));
                        float dy = Mathf.Max(0, Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius)));
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        byte a = (byte)(255 * Mathf.Clamp01(radius - d + 0.5f));
                        px[y * size + x] = new Color32(255, 255, 255, a);
                    }
                }

                tex.SetPixels32(px);
                tex.Apply(false, true);
                rounded = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0,
                    SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
                return rounded;
            }
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            GameObject go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        /// <summary>A screen-space overlay canvas scaled from a 1920x1080 reference.</summary>
        public static Canvas CreateCanvas(string name, int sortingOrder, Transform parent = null)
        {
            EnsureEventSystem();
            GameObject go = new GameObject(name, typeof(RectTransform));
            if (parent != null) go.transform.SetParent(parent, false);
            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static RectTransform Rect(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>Anchor a rect: anchors/pivot in 0..1, position and size in reference pixels.</summary>
        public static RectTransform Place(this RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position,
            Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>Stretch to fill the parent with an inset (left, bottom, right, top).</summary>
        public static RectTransform Fill(this RectTransform rt, float left = 0, float bottom = 0, float right = 0,
            float top = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static Image Panel(Transform parent, string name, Color? color = null, bool raycast = true)
        {
            RectTransform rt = Rect(parent, name);
            Image img = rt.gameObject.AddComponent<Image>();
            img.sprite = Rounded;
            img.type = Image.Type.Sliced;
            img.color = color ?? PanelColor;
            img.raycastTarget = raycast;
            return img;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, string text, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.TopLeft, FontStyles style = FontStyles.Normal)
        {
            RectTransform rt = Rect(parent, name);
            TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = style;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.raycastTarget = false;
            return t;
        }

        public static Button Button(Transform parent, string name, string label, float fontSize, Action onClick)
        {
            Image bg = Panel(parent, name, ButtonColor);
            Button b = bg.gameObject.AddComponent<Button>();
            ColorBlock cb = b.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.8f, 1.8f, 1.8f, 1);
            cb.pressedColor = new Color(2.5f, 2.5f, 2.5f, 1);
            cb.selectedColor = Color.white;
            cb.colorMultiplier = 2f;
            cb.fadeDuration = 0.08f;
            b.colors = cb;
            b.targetGraphic = bg;
            if (onClick != null) b.onClick.AddListener(() => onClick());
            TextMeshProUGUI t = Text(bg.transform, "Label", label, fontSize, GraphStyle.Text, TextAlignmentOptions.Center);
            t.rectTransform.Fill(6, 2, 6, 2);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return b;
        }

        /// <summary>Hex string for TMP rich text color tags.</summary>
        public static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);
    }
}
