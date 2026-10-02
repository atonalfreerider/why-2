using UnityEngine;
using Why.Economy.Land;
using Why.UI;

namespace Why.Economy.UI
{
    /// <summary>
    /// Where the HUD's blocks are on screen right now, in the canvas units of another canvas, with the economy's own
    /// panels (the social panel and the land legend report their boxes) and the bowl's box in the view's pose: the runtime
    /// input of the economy UI's layout, so it keeps clear of the HUD however it has laid itself out (landscape or
    /// portrait, a scaled or wrapped preset bar, the legend moved up, the F3 stats shown) and never covers the land. The
    /// HUD's blocks are found once by the names the HUD gives them; a block that is inactive (faded out) or missing
    /// measures as empty, and the layout then falls back to the HUD's authored sizes (<see cref="EconomyUiLayout"/>).
    /// Measuring allocates nothing (a few corner transforms and the bowl's rim projected per frame).
    /// </summary>
    public sealed class HudBlocks
    {
        readonly RectTransform title, subtitle, tour, help, stats, legend, readout, presetBar;
        readonly GraphRoot root;
        readonly Vector3[] corners = new Vector3[4];

        /// <summary>The missing HUD is reported once per session, not by every module that measures it.</summary>
        static bool reportedMissing;

        public HudBlocks(GraphRoot root)
        {
            this.root = root;
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
            }

            if ((title == null || presetBar == null) && !reportedMissing)
            {
                reportedMissing = true;
                Debug.LogWarning("[Why] economy UI: the HUD's blocks were not found by name; laying out with its authored sizes");
            }
        }

        /// <summary>True while the social panel is on screen (shown, or still fading out).</summary>
        public static bool SocialPanelShown => !SocialPanel.Occupied.IsEmpty;

        static RectTransform Find(Transform parent, string path) => parent.Find(path) as RectTransform;

        /// <summary>
        /// A frame for a canvas: its size (<paramref name="canvasSize"/>, which may be the size the canvas is about to
        /// have on the frame the screen changes), orientation, safe insets and the HUD's spacing, and the boxes of the
        /// blocks now shown; the social panel's and the land legend's boxes; the bowl's box in the current view's pose.
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
                TopButtons = UiBox.Union(Box(canvas, tour), Box(canvas, help)),
                DevStats = Box(canvas, stats),
                Legend = Box(canvas, legend),
                Readout = Box(canvas, readout),
                PresetBar = Box(canvas, presetBar),
                SocialPanel = SocialPanel.Occupied,
                LandLegend = LandLegend.Occupied
            };

            // the subtitle may wrap past the title block's authored height
            f.Title = UiBox.Union(Box(canvas, title), Box(canvas, subtitle));
            f.Bowl = EconomyUiLayout.BowlBox(f, BowlFraction(root?.CurrentPreset));
            return f;
        }

        /// <summary>
        /// The bowl's box (fractions of the screen, top-left origin) in a preset's pose, for a view that opens the land (its
        /// transition's target is the bowl); empty for the section view and before the land.
        /// </summary>
        public static Rect BowlFraction(ViewPreset preset)
        {
            if (preset == null || EconomyViews.Get(preset.Id).MorphTarget <= 0 || LandService.Current == null) return new Rect(0, 0, 0, 0);
            LandProjector cam = LandProjector.FromPose(preset.Pose(), CameraRig.FieldOfView, new Vector2(Screen.width, Screen.height));
            return LandPick.BowlBox(EconomyStage.Land(), cam);
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
    }
}
