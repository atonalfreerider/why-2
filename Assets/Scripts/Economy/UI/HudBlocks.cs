using System.Collections.Generic;
using UnityEngine;
using Why.Economy.Land;
using Why.UI;

namespace Why.Economy.UI
{
    /// <summary>
    /// Where the HUD's blocks are on screen right now, in the canvas units of another canvas, with the economy's own
    /// panels (the social panel and the land legend report their boxes) and the land's silhouette in the view's pose: the runtime
    /// input of the economy UI's layout, so it keeps clear of the HUD however it has laid itself out (landscape or
    /// portrait, a scaled or wrapped preset bar, the legend moved up, the F3 stats shown) and never covers the land. The
    /// HUD's blocks are found once by the names the HUD gives them; a block that is inactive (faded out) or missing
    /// measures as empty, and the layout then falls back to the HUD's authored sizes (<see cref="EconomyUiLayout"/>).
    /// Measuring allocates nothing (a few corner transforms; the silhouette is made again only when the view's pose, the
    /// screen or the land changes).
    /// <para>
    /// The same blocks, with every panel of the economy and the tour's card, are the hills' labels' keep-outs (spec 3.5):
    /// <see cref="ScreenRects"/> gives them in screen pixels for <see cref="LabelSystem.HudRects"/>.
    /// </para>
    /// </summary>
    public sealed class HudBlocks
    {
        readonly RectTransform title, subtitle, tour, help, stats, legend, readout, presetBar;
        readonly GraphRoot root;
        readonly Vector3[] corners = new Vector3[4];

        /// <summary>The economy's panels and the tour's card, found by the names their modules give them (<see cref="PanelPaths"/>).</summary>
        readonly List<RectTransform> panels = new List<RectTransform>();

        /// <summary>The missing HUD is reported once per session, not by every module that measures it.</summary>
        static bool reportedMissing;

        /// <summary>
        /// The panels the labels keep out from under besides the HUD's own blocks, as "canvas/panel" under their module: the
        /// year chip and its readout, the replay's readout, the social panel, the land legend, the inspectors, the views'
        /// side panels (strategy, mobility, you, regimes), the explorer and its chip, the landscape program, and the tour's
        /// narration card. A panel that is not found is simply not kept clear (a module of the other scene).
        /// </summary>
        static readonly string[] PanelPaths =
        {
            "EconomyControls/YearChip", "EconomyControls/YearReadout", ReplayPanel.CanvasName + "/Replay", "SocialPanel/Social",
            "LandLegend/LandLegend", "PlayerPanel/PlayerInspector", "PersonInspector/PersonInspector", "Companies spend too/Strategy",
            "Who the ladder counts/Mobility", "Where you stand/You", "What if: another government/Regimes",
            "Explore economic hierarchy/Explore", "Explore economic hierarchy/Explore the hill", "Landscape program/Readout",
            "DirectorCanvas/NarrationPanel"
        };

        /// <summary>Panels added from their own code (<see cref="Register"/>), destroyed ones pruned as they are met.</summary>
        static readonly List<RectTransform> registered = new List<RectTransform>();

        public HudBlocks(GraphRoot root)
        {
            this.root = root;
            if (root == null) return;
            foreach (GraphModule m in root.Modules)
            {
                // a module that destroyed itself (one idle in this run, as the smoke test outside a smoke run) is gone:
                // its transform throws
                if (m == null) continue;
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

                foreach (string path in PanelPaths)
                {
                    RectTransform panel = Find(m.transform, path);
                    if (panel != null) panels.Add(panel);
                }
            }

            if ((title == null || presetBar == null) && !reportedMissing)
            {
                reportedMissing = true;
                Debug.LogWarning("[Why] economy UI: the HUD's blocks were not found by name; laying out with its authored sizes");
            }
        }

        /// <summary>
        /// Main thread: a panel the hills' labels keep out from under while it is shown, besides the HUD's blocks and the
        /// economy's panels found by name: a new view's panel (spec 4's stage panels) registers itself once.
        /// </summary>
        public static void Register(RectTransform panel)
        {
            if (panel != null && !registered.Contains(panel)) registered.Add(panel);
        }

        /// <summary>
        /// Main thread: adds to <paramref name="into"/> the screen rect (pixels, origin bottom-left, as
        /// <paramref name="cam"/> projects) of every block shown now: the title (with its subtitle), the tour and help
        /// buttons, the F3 stats, the legend and the lifeline readout, the preset bar, the economy's panels and the tour's card
        /// (<see cref="PanelPaths"/>) and the registered panels. A block counts while it is active (a faded-out one is not) on
        /// an enabled canvas: a screen-space one's corners are screen pixels, at any resolution and in portrait; a world-space
        /// one (the headset's HUD plane, where only the kept canvases show) is projected by the camera. Allocates nothing.
        /// </summary>
        public void ScreenRects(List<Rect> into, Camera cam)
        {
            AddRect(into, cam, title);
            AddRect(into, cam, subtitle);
            AddRect(into, cam, tour);
            AddRect(into, cam, help);
            AddRect(into, cam, stats);
            AddRect(into, cam, legend);
            AddRect(into, cam, readout);
            AddRect(into, cam, presetBar);
            for (int i = 0; i < panels.Count; i++) AddRect(into, cam, panels[i]);
            for (int i = registered.Count - 1; i >= 0; i--)
            {
                if (registered[i] == null) registered.RemoveAt(i); // destroyed with its scene
                else AddRect(into, cam, registered[i]);
            }
        }

        void AddRect(List<Rect> into, Camera cam, RectTransform rt)
        {
            if (rt == null || !rt.gameObject.activeInHierarchy) return;
            Canvas canvas = rt.GetComponentInParent<Canvas>();
            Canvas screen = canvas != null ? canvas.rootCanvas : null;
            if (screen == null || !screen.enabled) return;
            if (screen.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                rt.GetWorldCorners(corners); // an overlay canvas's world corners are screen pixels
                into.Add(Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y));
            }
            else if (screen.renderMode == RenderMode.WorldSpace && LabelSystem.Project(rt, cam, out Rect r))
            {
                into.Add(r);
            }
        }

        /// <summary>True while the social panel is on screen (shown, or still fading out).</summary>
        public static bool SocialPanelShown => !SocialPanel.Occupied.IsEmpty;

        static RectTransform Find(Transform parent, string path) => parent.Find(path) as RectTransform;

        /// <summary>
        /// A frame for a canvas: its size (<paramref name="canvasSize"/>, which may be the size the canvas is about to
        /// have on the frame the screen changes), orientation, safe insets and the HUD's spacing, and the boxes of the
        /// blocks now shown; the social panel's and the land legend's boxes; the land's silhouette and its box in the current
        /// view's pose.
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
            f.Shape = BowlOf(root?.CurrentPreset, canvasSize);
            f.Bowl = EconomyUiLayout.BowlBox(f, f.Shape);
            if (f.Bowl.IsEmpty) f.Shape = null;
            return f;
        }

        static BowlShape cachedShape;
        static ViewPreset cachedPreset;
        static CameraPose cachedPose;
        static Vector2 cachedScreen, cachedCanvas;
        static int cachedLand = -1;
        static readonly List<Vector2> hull = new List<Vector2>(160), scratch = new List<Vector2>(160);

        /// <summary>
        /// The land's silhouette on a canvas in a preset's pose (<see cref="LandPick.BowlOutline"/> with the towers), for a
        /// view that opens the land (its transition's target is the bowl); null for the section view and before the land.
        /// Made again only when the preset, its pose, the screen, the canvas or the land changes (every module measures each
        /// frame; they share it).
        /// </summary>
        public static BowlShape BowlOf(ViewPreset preset, Vector2 canvas)
        {
            LandSnapshot s = LandService.Current;
            if (preset == null || s == null || EconomyViews.Get(preset.Id).MorphTarget <= 0) return null;
            CameraPose pose = preset.Pose();
            Vector2 screen = new Vector2(Screen.width, Screen.height);
            if (cachedPreset == preset && cachedLand == LandService.Version && cachedScreen == screen && cachedCanvas == canvas &&
                cachedPose.Target == pose.Target && cachedPose.Yaw == pose.Yaw && cachedPose.Pitch == pose.Pitch && cachedPose.Distance == pose.Distance)
            {
                return cachedShape;
            }

            cachedPreset = preset;
            cachedLand = LandService.Version;
            cachedScreen = screen;
            cachedCanvas = canvas;
            cachedPose = pose;
            LandProjector cam = LandProjector.FromPose(pose, CameraRig.FieldOfView, screen);
            LandPick.BowlOutline(EconomyStage.Land(), cam, s.Land?.Towers, hull, scratch);
            cachedShape = hull.Count >= 3 ? new BowlShape(hull, canvas) : null;
            return cachedShape;
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
