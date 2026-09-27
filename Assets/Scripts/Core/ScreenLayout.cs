using System;
using UnityEngine;

namespace Why
{
    /// <summary>
    /// The shape of the screen: the one source of truth for the landscape and the portrait (vertical)
    /// layout. Portrait exists for tutorial videos recorded at 9:16 and watched on phones: the canvases,
    /// the HUD, the tour panel, the figure tracker and the view presets all ask this class, and re-lay
    /// themselves out when <see cref="Version"/> or <see cref="OrientationVersion"/> changes.
    ///
    /// <see cref="Refresh"/> re-reads the screen at most once per frame (it is cheap and never allocates);
    /// every canvas calls it before its scaler runs, and GraphRoot calls it before reacting to a change.
    /// V (<see cref="ToggleVertical"/>) switches a player window or the Editor's Game view to 9:16 and
    /// back, but a window resized to portrait by hand is laid out exactly the same way.
    /// </summary>
    public static class ScreenLayout
    {
        /// <summary>Height / width above which a landscape screen turns portrait.</summary>
        public const float PortraitEnterRatio = 1.05f;

        /// <summary>
        /// Height / width below which a portrait screen turns landscape again. Between the two the
        /// orientation stays as it was, so dragging a window edge near square does not flip it back and forth.
        /// </summary>
        public const float PortraitExitRatio = 1f / PortraitEnterRatio;

        /// <summary>
        /// Graph labels in portrait are drawn this much larger than their 1080p size: a portrait recording is
        /// watched on a phone, where 13 px text on a 1080 px wide frame is barely legible.
        /// </summary>
        public const float PortraitLabelZoom = 1.5f;

        /// <summary>Fraction of the display height a vertical player window takes.</summary>
        const float WindowFraction = 0.9f;

        /// <summary>Smallest vertical player window (pixels high).</summary>
        const int MinWindowHeight = 640;

        static int width, height, refreshedFrame = -1;
        static Rect safeArea;
        static bool initialized;

        // the player window to return to when vertical mode ends
        static int restoreWidth, restoreHeight;
        static FullScreenMode restoreMode;
        static bool hasRestore;

        /// <summary>True while the screen is taller than wide (with hysteresis, see <see cref="PortraitExitRatio"/>).</summary>
        public static bool IsPortrait { get; private set; }

        /// <summary>Screen width / height (the camera's aspect).</summary>
        public static float Aspect { get; private set; } = 16f / 9f;

        /// <summary>
        /// Screen pixels per 1080p reference pixel: the short side over 1080 (at least 0.5), so a 1080x1920
        /// portrait frame has the same scale as a 1920x1080 landscape one.
        /// </summary>
        public static float UiScale { get; private set; } = 1f;

        /// <summary>Scale of graph labels: <see cref="UiScale"/>, and <see cref="PortraitLabelZoom"/> times more in portrait.</summary>
        public static float LabelScale => IsPortrait ? UiScale * PortraitLabelZoom : UiScale;

        /// <summary>Screen width in pixels.</summary>
        public static int Width => width;

        /// <summary>Screen height in pixels.</summary>
        public static int Height => height;

        /// <summary>The part of the screen not covered by notches or rounded corners (pixels, origin bottom-left).</summary>
        public static Rect SafeArea => safeArea;

        /// <summary>Incremented whenever the screen size, the safe area or the orientation changes.</summary>
        public static int Version { get; private set; }

        /// <summary>Incremented whenever <see cref="IsPortrait"/> flips.</summary>
        public static int OrientationVersion { get; private set; }

        /// <summary>
        /// Set by the editor-only Why.EditorTools.PortraitGameView: switches the Game view to a fixed portrait
        /// size (true) or back to landscape (false) and returns whether it could. Null in players.
        /// </summary>
        public static Func<bool, bool> EditorGameView { get; set; }

        /// <summary>Re-reads the screen (once per frame at most) and bumps the versions when it changed.</summary>
        public static void Refresh()
        {
            int frame = Time.frameCount;
            if (initialized && frame == refreshedFrame) return;
            refreshedFrame = frame;

            int w = Screen.width, h = Screen.height;
            Rect safe = Screen.safeArea;
            if (initialized && w == width && h == height && safe == safeArea) return;

            width = w;
            height = h;
            safeArea = safe;
            float ratio = h / (float)Mathf.Max(w, 1);
            bool portrait = !initialized ? ratio > 1f
                : IsPortrait ? ratio > PortraitExitRatio
                : ratio > PortraitEnterRatio;
            if (initialized && portrait != IsPortrait) OrientationVersion++;
            initialized = true;

            IsPortrait = portrait;
            Aspect = w / (float)Mathf.Max(h, 1);
            UiScale = Mathf.Max(0.5f, Mathf.Min(w, h) / 1080f);
            Version++;
        }

        /// <summary>
        /// V: toggles vertical mode for recording phone videos. In the Editor the Game view switches to a fixed
        /// 1080x1920 size and back (see <see cref="EditorGameView"/>); in a player the window becomes a 9:16
        /// window 90% of the display's height, and then returns to the size and mode it had.
        /// </summary>
        public static void ToggleVertical()
        {
            Refresh();
            bool portrait = !IsPortrait;
            if (Application.isEditor)
            {
                Func<bool, bool> gameView = EditorGameView;
                if (gameView == null || !gameView(portrait))
                {
                    Debug.LogWarning("[Why] vertical mode: could not resize the Game view; pick a 1080x1920 size " +
                                     "in its resolution menu (or use Why > Game View).");
                }

                return;
            }

            DisplayInfo display = Screen.mainWindowDisplayInfo;
            int displayWidth = display.width > 0 ? display.width : Screen.currentResolution.width;
            int displayHeight = display.height > 0 ? display.height : Screen.currentResolution.height;
            if (portrait)
            {
                restoreWidth = Screen.width;
                restoreHeight = Screen.height;
                restoreMode = Screen.fullScreenMode;
                hasRestore = true;
                int h = Mathf.Max(MinWindowHeight, Mathf.RoundToInt(displayHeight * WindowFraction));
                Screen.SetResolution(Mathf.RoundToInt(h * 9f / 16f), h, FullScreenMode.Windowed);
            }
            else if (hasRestore)
            {
                hasRestore = false;
                Screen.SetResolution(restoreWidth, restoreHeight, restoreMode);
            }
            else
            {
                // started in portrait (e.g. on a portrait monitor): a 16:9 window that fits the display
                int w = Mathf.RoundToInt(Mathf.Min(displayWidth * WindowFraction, displayHeight * WindowFraction * 16f / 9f));
                Screen.SetResolution(w, Mathf.RoundToInt(w * 9f / 16f), FullScreenMode.Windowed);
            }
        }
    }
}
