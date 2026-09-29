using System;
using TMPro;
using UnityEngine;

namespace Why.UI
{
    /// <summary>
    /// A small panel that follows the mouse and explains what is under it: an anchor's label (bold), its
    /// level and time, and its blurb wrapped to at most <see cref="MaxWidth"/>. It never takes input and
    /// is kept fully on screen, flipping to the other side of the cursor near the edges.
    /// </summary>
    public sealed class HudTooltip
    {
        const float MaxWidth = 360f;
        const float MinWidth = 140f;
        const float CursorOffset = 18f;
        const float LineGap = 4f;

        readonly RectTransform rect;
        readonly TextMeshProUGUI title, meta, body;
        readonly UiFade fade;
        object source;

        public HudTooltip(Transform parent)
        {
            rect = HudKit.FramedPanel(parent, "Tooltip", 0.92f);
            rect.anchorMin = rect.anchorMax = Vector2.zero;

            title = MakeText("Title", HudKit.SizeBody + 1, GraphStyle.Text, FontStyles.Bold);
            meta = MakeText("Meta", HudKit.SizeSmall - 0.5f, GraphStyle.TextDim, FontStyles.Normal);
            body = MakeText("Body", HudKit.SizeSmall, new Color(GraphStyle.Text.r, GraphStyle.Text.g, GraphStyle.Text.b, 0.82f),
                FontStyles.Normal);
            body.lineSpacing = 4;

            fade = new UiFade(rect.gameObject, 0, 9f, false);
        }

        TextMeshProUGUI MakeText(string name, float size, Color color, FontStyles style)
        {
            TextMeshProUGUI t = UiFactory.Text(rect, name, "", size, color, TextAlignmentOptions.TopLeft, style);
            t.richText = false; // anchor names and blurbs are data, never markup
            return t;
        }

        /// <summary>True while showing (or fading in) the content registered under this key.</summary>
        public bool IsShowing(object key) => ReferenceEquals(source, key) && fade.Shown;

        /// <summary>Show an anchor (label, level and time span, blurb) under a key (e.g. the hovered label).</summary>
        public void ShowAnchor(object key, Anchor anchor, string fallbackLabel)
        {
            if (IsShowing(key)) return;
            string label = string.IsNullOrEmpty(anchor.Label) ? fallbackLabel : anchor.Label;

            // "time:" and "now" anchors are moments on the axis, not things of a level
            bool moment = anchor.Key == "now" || anchor.Key.StartsWith("time:", StringComparison.Ordinal);
            string level = moment ? null : HudKit.LevelName(anchor.Level);
            string time = TimeSpan(anchor);
            if (string.Equals(time, label, StringComparison.OrdinalIgnoreCase)) time = "";
            string metaText = level == null ? time : time.Length == 0 ? level.ToUpperInvariant()
                : level.ToUpperInvariant() + "    " + time;
            Show(key, label, metaText, anchor.Blurb);
        }

        /// <summary>Show free text (e.g. a view preset's title and subtitle) under a key.</summary>
        public void Show(object key, string heading, string metaText, string bodyText)
        {
            if (IsShowing(key)) return;
            source = key;
            fade.Show(true); // activates the panel before its text is measured
            title.text = heading ?? "";
            meta.text = metaText ?? "";
            body.text = bodyText ?? "";
            Layout();
        }

        /// <summary>Show free text that changes every frame (the scale probe): re-lays out while already showing.</summary>
        public void Update(object key, string heading, string metaText, string bodyText)
        {
            if (!IsShowing(key))
            {
                Show(key, heading, metaText, bodyText);
                return;
            }

            if (title.text != heading) title.text = heading ?? "";
            if (meta.text != metaText) meta.text = metaText ?? "";
            if (body.text != bodyText) body.text = bodyText ?? "";
            Layout();
        }

        public void Hide()
        {
            source = null;
            fade.Show(false);
        }

        /// <summary>Follow the mouse (canvas units) and ease the fade.</summary>
        public void Tick(Vector2 mouse, Vector2 canvasSize, float dt)
        {
            fade.Tick(dt);
            if (fade.Alpha <= 0 && !fade.Shown) return;

            Vector2 size = rect.sizeDelta;
            float x = mouse.x + CursorOffset;
            if (x + size.x > canvasSize.x - HudKit.Gap) x = mouse.x - CursorOffset - size.x;
            float top = mouse.y - CursorOffset;
            if (top - size.y < HudKit.Gap) top = mouse.y + CursorOffset + size.y;
            x = Mathf.Clamp(x, HudKit.Gap, Mathf.Max(HudKit.Gap, canvasSize.x - HudKit.Gap - size.x));
            top = Mathf.Clamp(top, Mathf.Min(size.y + HudKit.Gap, canvasSize.y - HudKit.Gap), canvasSize.y - HudKit.Gap);
            rect.anchoredPosition = new Vector2(Mathf.Round(x), Mathf.Round(top - size.y));
        }

        void Layout()
        {
            bool hasMeta = meta.text.Length > 0, hasBody = body.text.Length > 0;
            meta.gameObject.SetActive(hasMeta);
            body.gameObject.SetActive(hasBody);

            float inner = MaxWidth - 2 * HudKit.Pad;
            Vector2 t = HudKit.Measure(title, title.text, inner);
            Vector2 m = hasMeta ? HudKit.Measure(meta, meta.text, inner) : Vector2.zero;
            Vector2 b = hasBody ? HudKit.Measure(body, body.text, inner) : Vector2.zero;
            float width = Mathf.Clamp(Mathf.Max(t.x, Mathf.Max(m.x, b.x)), MinWidth - 2 * HudKit.Pad, inner);

            float y = HudKit.Pad - 2;
            y = Stack(title, t, width, y);
            if (hasMeta) y = Stack(meta, m, width, y + 1);
            if (hasBody) y = Stack(body, b, width, y + LineGap + 2);

            rect.pivot = Vector2.zero;
            rect.sizeDelta = new Vector2(width + 2 * HudKit.Pad, y - LineGap + HudKit.Pad);
        }

        static float Stack(TextMeshProUGUI t, Vector2 size, float width, float y)
        {
            HudKit.PlaceTopLeft(t.rectTransform, HudKit.Pad, y, new Vector2(width, size.y));
            return y + size.y + LineGap;
        }

        /// <summary>"66 million years ago - now", "1452 - 1519", or a single moment.</summary>
        static string TimeSpan(Anchor a)
        {
            double now = DeepTime.NowYear;
            string start = DeepTime.FormatYearsAgo(a.YearsAgo, now);
            double span = Math.Abs(a.YearsAgo - a.EndYearsAgo);
            if (span <= Math.Max(0.5, 0.01 * a.YearsAgo)) return start;
            string end = DeepTime.FormatYearsAgo(a.EndYearsAgo, now);
            return end == start ? start : start + " - " + end;
        }
    }
}
