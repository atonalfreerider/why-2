using UnityEngine;
using Why.UI;

namespace Why.Economy.UI
{
    /// <summary>
    /// Measures where the HUD's blocks (and the games panel) are, in the canvas units of another canvas, so the economy's
    /// controls and inspector keep clear of them however the HUD has laid itself out (landscape or portrait, a scaled or
    /// wrapped preset bar, the legend moved up, the F3 stats shown). The blocks are found once by the names the HUD gives
    /// them; a block that is inactive (faded out) or missing measures as empty, so the layout falls back to the HUD's
    /// authored sizes (<see cref="EconomyUiLayout"/>). Reading the boxes allocates nothing.
    /// </summary>
    public sealed class HudProbe
    {
        readonly RectTransform title, subtitle, tour, help, stats, legend, readout, presetBar, games;
        readonly Vector3[] corners = new Vector3[4];

        public HudProbe(GraphRoot root)
        {
            if (root == null) return;
            foreach (GraphModule m in root.Modules)
            {
                if (m is Hud)
                {
                    Transform content = m.transform.Find("Hud/Content");
                    if (content == null) continue;
                    title = Find(content, "Title");
                    subtitle = Find(content, "Title/Subtitle");
                    tour = Find(content, "TopButtons/TourButton");
                    help = Find(content, "TopButtons/HelpButton");
                    stats = Find(content, "Stats");
                    legend = Find(content, "Legend");
                    readout = Find(content, "LifelineReadout");
                    presetBar = Find(content, "PresetBar");
                }
                else if (m is GamesPanel)
                {
                    games = Find(m.transform, "GamesPanel/Games");
                }
            }
        }

        /// <summary>True when the HUD itself was found (otherwise every block measures empty).</summary>
        public bool FoundHud => title != null && presetBar != null;

        static RectTransform Find(Transform parent, string path) => parent.Find(path) as RectTransform;

        /// <summary>
        /// A frame for a canvas: its size (<paramref name="canvasSize"/>, which may be the size the canvas is about to
        /// have on the frame the screen changes), orientation, safe insets and the HUD's spacing, and the boxes of the
        /// blocks now shown.
        /// </summary>
        public HudFrame Measure(RectTransform canvas, Vector2 canvasSize)
        {
            bool portrait = ScreenLayout.IsPortrait;
            HudKit.SafeInsets(UiFactory.CanvasScale, out float safeBottom, out float safeTop);
            HudFrame f = new HudFrame
            {
                Canvas = canvasSize,
                Portrait = portrait,
                SafeTop = portrait ? safeTop : 0,
                SafeBottom = portrait ? safeBottom : 0,
                Margin = portrait ? HudKit.PortraitMargin : HudKit.Margin,
                Gap = HudKit.Gap,
                TopButtons = Union(Box(canvas, tour), Box(canvas, help)),
                DevStats = Box(canvas, stats),
                Legend = Box(canvas, legend),
                Readout = Box(canvas, readout),
                PresetBar = Box(canvas, presetBar),
                GamesPanel = Box(canvas, games)
            };

            // the subtitle may wrap past the title block's authored height
            f.Title = Union(Box(canvas, title), Box(canvas, subtitle));
            return f;
        }

        /// <summary>A block's box in the canvas's units, from its top-left corner; empty while the block is not shown.</summary>
        public UiBox Box(RectTransform canvas, RectTransform rt)
        {
            if (rt == null || canvas == null || !rt.gameObject.activeInHierarchy) return UiBox.Empty;
            rt.GetWorldCorners(corners);
            Vector3 a = canvas.InverseTransformPoint(corners[0]); // bottom-left
            Vector3 b = canvas.InverseTransformPoint(corners[2]); // top-right
            Rect r = canvas.rect;
            float x0 = Mathf.Min(a.x, b.x) - r.xMin, x1 = Mathf.Max(a.x, b.x) - r.xMin;
            float top = r.yMax - Mathf.Max(a.y, b.y), bottom = r.yMax - Mathf.Min(a.y, b.y);
            return new UiBox(x0, top, x1 - x0, bottom - top);
        }

        static UiBox Union(UiBox a, UiBox b)
        {
            if (a.IsEmpty) return b;
            if (b.IsEmpty) return a;
            float x = Mathf.Min(a.X, b.X), y = Mathf.Min(a.Y, b.Y);
            return new UiBox(x, y, Mathf.Max(a.Right, b.Right) - x, Mathf.Max(a.Bottom, b.Bottom) - y);
        }
    }
}
