using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Why
{
    /// <summary>
    /// Orbit camera for a graph spanning many orders of magnitude: exponential zoom toward the cursor,
    /// orbit (right drag), pan (middle drag / shift+left drag / WASD), smooth flights between poses.
    /// Near/far planes follow the zoom level to keep depth precision. Everything works in screen pixels and
    /// the vertical field of view, so it behaves the same on a portrait screen; when the screen flips
    /// orientation, GraphRoot flies it to the current preset's framing for the new shape.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraRig : MonoBehaviour
    {
        /// <summary>Vertical field of view (degrees), set by GraphRoot; view presets frame against it.</summary>
        public const float FieldOfView = 45f;

        public float OrbitSensitivity = 0.25f;
        public float ZoomPerNotch = 0.18f;
        public float MinDistance = 0.02f;
        public float MaxDistance = 400f;

        CameraPose pose, flightFrom, flightTo;
        float flightT = 1, flightDuration = 1;
        bool dragging;

        public Camera Cam { get; private set; }
        public CameraPose Pose => pose;
        public bool Flying => flightT < 1;

        /// <summary>Incremented whenever the camera moves; label layout caches against it.</summary>
        public int Version { get; private set; }

        /// <summary>True while the user is actively steering (the director pauses autoplay).</summary>
        public bool UserActive { get; private set; }

        void Awake()
        {
            Cam = GetComponent<Camera>();
            pose = new CameraPose { Target = Vector3.zero, Yaw = 0, Pitch = 60, Distance = 10 };
            Apply();
        }

        public void SetPose(CameraPose p)
        {
            pose = p;
            flightT = 1;
            Apply();
        }

        public void FlyTo(CameraPose p, float seconds)
        {
            if (seconds <= 0)
            {
                SetPose(p);
                return;
            }

            flightFrom = pose;
            flightTo = p;
            // take the short way around
            flightTo.Yaw = flightFrom.Yaw + Mathf.DeltaAngle(flightFrom.Yaw, flightTo.Yaw);
            flightDuration = seconds;
            flightT = 0;
        }

        void Update()
        {
            UserActive = false;
            HandleInput();

            if (flightT < 1)
            {
                flightT = Mathf.Min(1, flightT + Time.unscaledDeltaTime / flightDuration);
                float e = flightT * flightT * flightT * (flightT * (flightT * 6 - 15) + 10);
                pose.Target = Vector3.Lerp(flightFrom.Target, flightTo.Target, e);
                pose.Yaw = Mathf.Lerp(flightFrom.Yaw, flightTo.Yaw, e);
                pose.Pitch = Mathf.Lerp(flightFrom.Pitch, flightTo.Pitch, e);
                // zoom out a little mid-flight so long journeys keep context
                float lift = 1f + 0.35f * Mathf.Sin(e * Mathf.PI) *
                    Mathf.Clamp01(Vector3.Distance(flightFrom.Target, flightTo.Target) / Mathf.Max(flightTo.Distance, 0.01f));
                pose.Distance = Mathf.Exp(Mathf.Lerp(Mathf.Log(flightFrom.Distance), Mathf.Log(flightTo.Distance), e)) * lift;
            }

            Apply();
        }

        void HandleInput()
        {
            Mouse mouse = Mouse.current;
            Keyboard kb = Keyboard.current;
            if (mouse == null) return;

            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            Vector2 delta = mouse.delta.ReadValue();
            bool shift = kb != null && kb.shiftKey.isPressed;

            if (mouse.rightButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame ||
                (shift && mouse.leftButton.wasPressedThisFrame))
            {
                dragging = !overUi;
            }

            if (!mouse.rightButton.isPressed && !mouse.middleButton.isPressed && !(shift && mouse.leftButton.isPressed))
            {
                dragging = false;
            }

            if (dragging && mouse.rightButton.isPressed && delta.sqrMagnitude > 0)
            {
                CancelFlight();
                pose.Yaw += delta.x * OrbitSensitivity;
                pose.Pitch = Mathf.Clamp(pose.Pitch - delta.y * OrbitSensitivity, 5, 89.5f);
                UserActive = true;
            }

            if (dragging && (mouse.middleButton.isPressed || (shift && mouse.leftButton.isPressed)) &&
                delta.sqrMagnitude > 0)
            {
                CancelFlight();
                float unitsPerPixel = 2f * pose.Distance * Mathf.Tan(Cam.fieldOfView * 0.5f * Mathf.Deg2Rad) /
                                      Mathf.Max(Screen.height, 1);
                Vector3 right = transform.right;
                Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
                if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.ProjectOnPlane(transform.up, Vector3.up).normalized;
                pose.Target -= (right * delta.x + fwd * delta.y) * unitsPerPixel;
                UserActive = true;
            }

            float scroll = overUi ? 0 : mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                CancelFlight();
                // Windows reports 120 per wheel notch, other platforms ~1
                float notches = Mathf.Abs(scroll) > 20 ? scroll / 120f : scroll;
                float factor = Mathf.Exp(-notches * ZoomPerNotch);
                ZoomTowardCursor(factor, mouse.position.ReadValue());
                UserActive = true;
            }

            if (kb != null && !overUi)
            {
                Vector3 move = Vector3.zero;
                Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
                if (kb.wKey.isPressed) move += fwd;
                if (kb.sKey.isPressed) move -= fwd;
                if (kb.dKey.isPressed) move += transform.right;
                if (kb.aKey.isPressed) move -= transform.right;
                if (kb.eKey.isPressed) move += Vector3.up;
                if (kb.qKey.isPressed) move -= Vector3.up;
                if (move.sqrMagnitude > 0)
                {
                    CancelFlight();
                    pose.Target += move.normalized * (pose.Distance * 0.8f * Time.unscaledDeltaTime);
                    UserActive = true;
                }

                if (kb.equalsKey.isPressed || kb.numpadPlusKey.isPressed) Zoom(Mathf.Exp(-1.5f * Time.unscaledDeltaTime));
                if (kb.minusKey.isPressed || kb.numpadMinusKey.isPressed) Zoom(Mathf.Exp(1.5f * Time.unscaledDeltaTime));
            }
        }

        void Zoom(float factor)
        {
            CancelFlight();
            pose.Distance = Mathf.Clamp(pose.Distance * factor, MinDistance, MaxDistance);
            UserActive = true;
        }

        /// <summary>Exponential zoom that keeps the point under the cursor fixed (on the target's height plane).</summary>
        void ZoomTowardCursor(float factor, Vector2 screen)
        {
            float newDistance = Mathf.Clamp(pose.Distance * factor, MinDistance, MaxDistance);
            factor = newDistance / pose.Distance;
            Ray ray = Cam.ScreenPointToRay(screen);
            Plane plane = new Plane(Vector3.up, new Vector3(0, pose.Target.y, 0));
            if (plane.Raycast(ray, out float hit) && hit < pose.Distance * 50f)
            {
                Vector3 p = ray.GetPoint(hit);
                pose.Target = p + (pose.Target - p) * factor;
            }

            pose.Distance = newDistance;
        }

        void CancelFlight() => flightT = 1;

        void Apply()
        {
            Quaternion rot = Quaternion.Euler(pose.Pitch, pose.Yaw, 0);
            Vector3 pos = pose.Target - rot * Vector3.forward * pose.Distance;
            if (pos != transform.position || rot != transform.rotation)
            {
                transform.SetPositionAndRotation(pos, rot);
                Version++;
            }

            Cam.nearClipPlane = Mathf.Max(0.0005f, pose.Distance * 0.01f);
            Cam.farClipPlane = Mathf.Max(100f, pose.Distance * 400f);
        }

        /// <summary>Screen pixels per world unit at a world position (for sizing labels).</summary>
        public float PixelsPerUnit(Vector3 world)
        {
            float depth = Vector3.Dot(world - transform.position, transform.forward);
            if (depth <= 1e-5f) return 0;
            return Screen.height / (2f * depth * Mathf.Tan(Cam.fieldOfView * 0.5f * Mathf.Deg2Rad));
        }
    }
}
