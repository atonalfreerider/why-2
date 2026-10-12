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

        Camera cam;

        /// <summary>
        /// The camera this rig moves. Resolved on first use: a rig serialized in the scene may be asked for it before its
        /// own Awake (GraphRoot and the modules wake first).
        /// </summary>
        public Camera Cam => cam != null ? cam : cam = GetComponent<Camera>();
        public CameraPose Pose => pose;

        /// <summary>
        /// In a headset: the tracking-space root this rig moves, turns and scales instead of the camera (the head owns
        /// the camera). The pose's target then sits <see cref="XrViewDistance"/> metres in front of the eyes and the
        /// world is scaled by the pose's distance, so every preset becomes a diorama at arm's length.
        /// </summary>
        public Transform XrOrigin { get; private set; }

        /// <summary>Metres from the eyes to the pose's target in a headset.</summary>
        public const float XrViewDistance = 1.1f;

        /// <summary>Steepest downward look (degrees) a headset pose asks for; presets that look straight down are tilted up.</summary>
        public const float XrMaxPitch = 28f;

        /// <summary>World units per metre in a headset (the origin's uniform scale).</summary>
        public float XrScale => pose.Distance / XrViewDistance;

        // head position in tracking space and the yaw the head faced when recentred
        Vector3 xrHead = new Vector3(0, 1.6f, 0);
        float xrHeadYaw;
        Vector3 lastCamPos;
        Quaternion lastCamRot;
        bool userActivePending;
        public bool Flying => flightT < 1;

        /// <summary>Incremented whenever the camera moves; label layout caches against it.</summary>
        public int Version { get; private set; }

        /// <summary>True while the user is actively steering (the director pauses autoplay).</summary>
        public bool UserActive { get; private set; }

        void Awake()
        {
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

        /// <summary>Hands the rig a tracking-space origin to drive (headset); see <see cref="XrOrigin"/>.</summary>
        public void AttachXr(Transform origin)
        {
            XrOrigin = origin;
            Apply();
        }

        /// <summary>
        /// Makes where the head is now (position in tracking space, facing) the reference the poses are framed from:
        /// the target appears straight ahead of the current gaze.
        /// </summary>
        public void RecenterXr(Vector3 headLocal, float headYaw)
        {
            xrHead = headLocal;
            xrHeadYaw = headYaw;
            Apply();
        }

        /// <summary>Headset locomotion: moves the target by a world-space offset.</summary>
        public void XrMove(Vector3 world)
        {
            if (world.sqrMagnitude <= 0) return;
            CancelFlight();
            pose.Target += world;
            userActivePending = true;
        }

        /// <summary>Headset zoom: scales the distance (and the world) by a factor, keeping a world point fixed.</summary>
        public void XrZoom(float factor, Vector3 pivot)
        {
            float d = Mathf.Clamp(pose.Distance * factor, MinDistance, MaxDistance);
            factor = d / pose.Distance;
            if (Mathf.Approximately(factor, 1)) return;
            CancelFlight();
            pose.Target = pivot + (pose.Target - pivot) * factor;
            pose.Distance = d;
            userActivePending = true;
        }

        /// <summary>Headset turn: orbits the world around the target.</summary>
        public void XrTurn(float degrees)
        {
            CancelFlight();
            pose.Yaw += degrees;
            userActivePending = true;
        }

        void Update()
        {
            UserActive = userActivePending;
            userActivePending = false;
            if (XrOrigin == null) HandleInput();

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
            if (XrOrigin != null)
            {
                ApplyXr();
                return;
            }

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

        /// <summary>
        /// Headset placement: the eye sits where the pose's camera would (pitch limited to a comfortable downward look),
        /// the origin is turned by the pose's yaw only (the horizon stays level) and scaled so the target is
        /// <see cref="XrViewDistance"/> metres away. The head moves freely inside it.
        /// </summary>
        void ApplyXr()
        {
            float s = XrScale;
            float pitch = Mathf.Clamp(pose.Pitch, 5f, XrMaxPitch);
            Vector3 eye = pose.Target - Quaternion.Euler(pitch, pose.Yaw, 0) * Vector3.forward * pose.Distance;
            Quaternion yaw = Quaternion.Euler(0, pose.Yaw - xrHeadYaw, 0);
            Vector3 origin = eye - yaw * (xrHead * s);
            XrOrigin.SetPositionAndRotation(origin, yaw);
            XrOrigin.localScale = Vector3.one * s;

            // clip planes in world units: 4 cm in front of the eyes, far enough for the whole graph
            Cam.nearClipPlane = Mathf.Max(1e-4f, 0.04f * s);
            Cam.farClipPlane = Mathf.Max(100f, pose.Distance * 400f);

            // the head moves every frame: labels re-place whenever the eye does
            if (transform.position != lastCamPos || transform.rotation != lastCamRot)
            {
                lastCamPos = transform.position;
                lastCamRot = transform.rotation;
                Version++;
            }
        }

        /// <summary>Screen pixels per world unit at a world position (for sizing labels).</summary>
        public float PixelsPerUnit(Vector3 world)
        {
            float depth = Vector3.Dot(world - transform.position, transform.forward);
            if (depth <= 1e-5f) return 0;
            return Cam.pixelHeight / (2f * depth * Mathf.Tan(Cam.fieldOfView * 0.5f * Mathf.Deg2Rad));
        }
    }
}
