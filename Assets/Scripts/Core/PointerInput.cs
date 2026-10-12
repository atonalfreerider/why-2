using UnityEngine;
using UnityEngine.InputSystem;

namespace Why
{
    /// <summary>
    /// The one pointer every picking and hovering path reads: the mouse on the desktop, or the aim ray of a tracked
    /// controller in a headset (the XR rig installs itself as the <see cref="Source"/>). The XR source
    /// reports its ray as a point on the main camera's screen, so screen-space picking code (labels, lifelines,
    /// hills) works unchanged in both.
    /// </summary>
    public static class PointerInput
    {
        public interface ISource
        {
            /// <summary>False when the pointer is not available this frame (controller not tracked).</summary>
            bool Valid { get; }
            /// <summary>World-space aim ray.</summary>
            Ray Ray { get; }
            bool Pressed { get; }
            bool PressedThisFrame { get; }
            bool ReleasedThisFrame { get; }
        }

        /// <summary>Set while a headset session runs; null on the desktop.</summary>
        public static ISource Source { get; set; }

        public static bool IsXr => Source != null;

        static Camera Cam => Camera.main;

        /// <summary>Pointer position in screen pixels of the main camera (mouse, or the aim ray's projection).</summary>
        public static Vector2 Position
        {
            get
            {
                if (Source == null) return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
                Camera cam = Cam;
                if (cam == null || !Source.Valid) return new Vector2(-10000, -10000);
                Ray r = Source.Ray;
                // a point far along the ray projects to where the ray "points" on screen
                Vector3 far = r.origin + r.direction * Mathf.Max(cam.farClipPlane * 0.5f, 10f);
                Vector3 sp = cam.WorldToScreenPoint(far);
                bool ok = sp.z > 0 && !float.IsNaN(sp.x) && !float.IsNaN(sp.y);
                return ok ? (Vector2)sp : new Vector2(-10000, -10000);
            }
        }

        public static bool Available => Source != null ? Source.Valid : Mouse.current != null;

        public static bool Pressed => Source != null ? Source.Valid && Source.Pressed
            : Mouse.current != null && Mouse.current.leftButton.isPressed;

        public static bool PressedThisFrame => Source != null ? Source.Valid && Source.PressedThisFrame
            : Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;

        public static bool ReleasedThisFrame => Source != null ? Source.ReleasedThisFrame
            : Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame;

        /// <summary>World ray under the pointer.</summary>
        public static Ray WorldRay(Camera cam)
        {
            if (Source != null) return Source.Ray;
            return cam.ScreenPointToRay(Position);
        }
    }
}
