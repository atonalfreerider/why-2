using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.XR;

namespace Why.Xr
{
    /// <summary>
    /// Whether the graph runs in a headset, and the shared pieces of the headset presentation: the tracking-space
    /// rig (<see cref="Origin"/>, <see cref="CameraOffset"/>) that <see cref="CameraRig"/> moves and scales instead of
    /// the camera, and the HUD plane every canvas moves onto. On the desktop nothing here is created.
    ///
    /// In a headset the graph is a diorama: the view preset's target sits a comfortable arm's length in front of you
    /// and the whole world is scaled by the preset's distance, so the same poses that frame the Big Bang or a single
    /// lifeline on a monitor put it in front of you at any scale - and you can walk around it, grab it and pull
    /// yourself in.
    /// </summary>
    public static class XrSession
    {
        /// <summary>Virtual screen the canvases are laid out on in a headset (landscape 1080p).</summary>
        public static readonly Vector2 VirtualScreen = new Vector2(1920, 1080);

        /// <summary>Metres per canvas unit on the HUD plane (1920 units = 1.54 m).</summary>
        public const float HudMetresPerUnit = 0.0008f;

        /// <summary>Distance of the HUD plane from the eyes, metres.</summary>
        public const float HudDistance = 1.45f;

        static bool? active;

        /// <summary>True when an XR display is running (Quest), or when forced with the -xr command-line flag.</summary>
        public static bool Active
        {
            get
            {
                if (active.HasValue) return active.Value;
                // XR Management starts the OpenXR display before the first scene loads (Android only; the desktop
                // has no loader), so by the time a graph is built a running display means a headset
                List<XRDisplaySubsystem> displays = new List<XRDisplaySubsystem>();
                SubsystemManager.GetSubsystems(displays);
                bool running = displays.Exists(d => d.running);
                active = running || XRSettings.isDeviceActive ||
                         Array.IndexOf(Environment.GetCommandLineArgs(), "-xr") >= 0;
                return active.Value;
            }
        }

        /// <summary>Root of the tracking space; position, yaw and uniform scale are driven by <see cref="CameraRig"/>.</summary>
        public static Transform Origin { get; internal set; }

        /// <summary>Parent of the head, the hands and the HUD (tracking space, metres before the origin's scale).</summary>
        public static Transform CameraOffset { get; internal set; }

        /// <summary>The HUD plane: canvases made in a headset are parented here (tracking space).</summary>
        public static Transform Hud { get; internal set; }

        static readonly List<Canvas> pending = new List<Canvas>();

        /// <summary>
        /// Turns a canvas made by <see cref="UiFactory.CreateCanvas"/> into a world-space panel on the HUD plane, laid out
        /// on <see cref="VirtualScreen"/>, pointable with a tracked controller.
        /// </summary>
        public static void ToWorldSpace(Canvas canvas)
        {
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            RectTransform rt = (RectTransform)canvas.transform;
            rt.sizeDelta = VirtualScreen;
            rt.pivot = new Vector2(0.5f, 0.5f);
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null) scaler.dynamicPixelsPerUnit = 2.5f;
            if (canvas.GetComponent<TrackedDeviceRaycaster>() == null) canvas.gameObject.AddComponent<TrackedDeviceRaycaster>();
            if (Hud == null)
            {
                pending.Add(canvas);
                return;
            }

            Attach(canvas);
        }

        internal static void AttachPending()
        {
            foreach (Canvas c in pending)
            {
                if (c != null) Attach(c);
            }

            pending.Clear();
        }

        static void Attach(Canvas canvas)
        {
            Transform t = canvas.transform;
            t.SetParent(Hud, false);
            t.localPosition = new Vector3(0, 0, -0.0005f * canvas.sortingOrder); // higher orders a hair closer
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one * HudMetresPerUnit;
        }
    }
}
