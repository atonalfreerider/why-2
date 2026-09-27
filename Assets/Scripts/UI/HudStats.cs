using System.Globalization;
using TMPro;
using UnityEngine;

namespace Why.UI
{
    /// <summary>
    /// F3 stats: smoothed frame rate, graph build time and the warp's unroll amount, refreshed a few times
    /// per second. Anchored top-right below the buttons.
    /// </summary>
    public sealed class HudStats
    {
        const float Refresh = 0.25f;

        readonly RectTransform panel;
        readonly TextMeshProUGUI text;
        readonly UiFade fade;
        float smoothDt = 1f / 60f;
        float sinceRefresh = Refresh;

        public HudStats(Transform parent, float top)
        {
            panel = HudKit.FramedPanel(parent, "Stats", 0.8f);
            panel.Place(Vector2.one, Vector2.one, new Vector2(-HudKit.Margin, -top), new Vector2(170, 72));
            text = UiFactory.Text(panel, "Text", "", HudKit.SizeSmall, GraphStyle.TextDim);
            text.rectTransform.Fill(HudKit.Pad, 6, HudKit.Pad, 8);
            text.lineSpacing = 6;
            fade = new UiFade(panel.gameObject, 0, 8f, false);
        }

        public void Toggle() => fade.Show(!fade.Shown);

        public void Tick(float dt, GraphRoot root)
        {
            fade.Tick(dt);
            if (dt > 0) smoothDt = Mathf.Lerp(smoothDt, dt, 0.05f);
            if (!fade.Shown) return;

            sinceRefresh += dt;
            if (sinceRefresh < Refresh) return;
            sinceRefresh = 0;

            CultureInfo c = CultureInfo.InvariantCulture;
            string value = "<pos=64><color=#" + UiFactory.Hex(GraphStyle.Text) + ">";
            text.text =
                "fps" + value + (1f / Mathf.Max(smoothDt, 1e-4f)).ToString("0", c) + "</color>\n" +
                "built" + value + root.LoadSeconds.ToString("0.00", c) + " s</color>\n" +
                "unroll" + value + GraphWarp.Current.Unroll.ToString("0.00", c) + "</color>";
        }
    }
}
