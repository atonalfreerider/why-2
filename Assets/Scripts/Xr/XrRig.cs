using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Hands;
using Why.Director;

namespace Why.Xr
{
    /// <summary>
    /// The headset presentation of both graphs (Meta Quest through OpenXR), with hands only - no controllers. Builds the
    /// tracking-space rig the camera rig drives (<see cref="CameraRig.XrOrigin"/>), tracks the head and both hands, draws
    /// the hands, and reads gestures:
    ///
    /// * right hand: point (Meta's aim ray) and pinch to select - labels, people, companies, panels (<see cref="PointerInput"/>)
    /// * pinch with either hand and move: drag the world
    /// * pinch with both hands: pull apart / together to grow or shrink the world, twist to turn it
    /// * turn your left palm toward your face: the menu (views, the guided tour and its next / back, the other scene,
    ///   recenter), pressed with a right-hand pinch
    ///
    /// Every canvas moves onto a HUD plane that lazily follows your gaze (<see cref="XrSession"/>). On the desktop this
    /// module removes itself.
    /// </summary>
    [GraphScenes(GraphScene.Why, GraphScene.Economy)]
    public sealed class XrRig : GraphModule, PointerInput.ISource
    {
        /// <summary>The HUD re-centres on the gaze when you look further away from it than this (degrees).</summary>
        const float HudFollowAngle = 38f;

        /// <summary>A pinch becomes a drag of the world once the hand has moved this far (metres).</summary>
        const float DragStart = 0.025f;

        public override int Order => -100;

        GraphRoot root;
        CameraRig rig;
        Transform head, rightAim, palmAnchor;
        readonly XrHand left = new XrHand(Handedness.Left), right = new XrHand(Handedness.Right);
        InputAction aimPosition, aimRotation, aimTracked;
        XRHandSubsystem hands;
        readonly List<XRHandSubsystem> found = new List<XRHandSubsystem>();
        LineRenderer ray;
        XrMenu menu;
        XrBeacon beacon;
        DirectorMode director;

        bool twoHanded;
        float lastSpan, lastAngle;
        float hudYaw, palmFacing;
        Vector3 hudPosition;
        bool hudPlaced, aimValid;
        int recenterFrames = -1;

        public static XrRig Instance { get; private set; }

        // ------------------------------------------------------------------ PointerInput.ISource

        public bool Valid => aimValid;
        public Ray Ray => new Ray(rightAim.position, rightAim.forward);
        public bool Pressed => right.Pinching;
        public bool PressedThisFrame => right.PinchStarted;
        public bool ReleasedThisFrame => right.PinchEnded;

        // ------------------------------------------------------------------ setup

        public override void Init(GraphRoot graphRoot)
        {
            if (!XrSession.Active)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            root = graphRoot;
            rig = root.Rig;
            Camera cam = rig.Cam;

            Transform origin = new GameObject("XR Origin").transform;
            Transform offset = new GameObject("Camera Offset").transform;
            offset.SetParent(origin, false);
            XrSession.Origin = origin;
            XrSession.CameraOffset = offset;

            head = cam.transform;
            head.SetParent(offset, false);
            head.localPosition = new Vector3(0, 1.6f, 0);
            head.localRotation = Quaternion.identity;
            TrackedPoseDriver tpd = head.gameObject.AddComponent<TrackedPoseDriver>();
            tpd.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            tpd.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            tpd.positionInput = new InputActionProperty(new InputAction("HeadPosition", InputActionType.Value, "<XRHMD>/centerEyePosition"));
            tpd.rotationInput = new InputActionProperty(new InputAction("HeadRotation", InputActionType.Value, "<XRHMD>/centerEyeRotation"));
            tpd.trackingStateInput = new InputActionProperty(new InputAction("HeadState", InputActionType.Value, "<XRHMD>/trackingState"));

            rightAim = new GameObject("Right Aim").transform;
            rightAim.SetParent(offset, false);
            palmAnchor = new GameObject("Left Palm").transform;
            palmAnchor.SetParent(offset, false);

            // Meta's hand aim (XR_FB_hand_tracking_aim): a stable pointing ray from the shoulder through the hand
            aimPosition = new InputAction("AimPosition", InputActionType.Value, "<MetaAimHand>{RightHand}/devicePosition");
            aimRotation = new InputAction("AimRotation", InputActionType.Value, "<MetaAimHand>{RightHand}/deviceRotation");
            aimTracked = new InputAction("AimTracked", InputActionType.Button, "<MetaAimHand>{RightHand}/isTracked");
            aimPosition.Enable();
            aimRotation.Enable();
            aimTracked.Enable();

            XrSession.Hud = new GameObject("HUD").transform;
            XrSession.Hud.SetParent(offset, false);
            XrSession.AttachPending();

            ConfigureUiInput(offset);
            Material mat = new Material(Shader.Find("Sprites/Default"));
            ray = Line("Pointer Ray", mat, 2);
            left.BuildSkeleton(transform, mat);
            right.BuildSkeleton(transform, mat);
            PointerInput.Source = this;

            rig.AttachXr(origin);
            beacon = new XrBeacon(transform);
            menu = gameObject.AddComponent<XrMenu>();
            menu.Init(root, palmAnchor);

            // OpenXR starts tracking a few frames in; recenter once the head reports a pose
            recenterFrames = 0;
        }

        public override void OnLoaded(GraphRoot graphRoot)
        {
            director = graphRoot.Modules.OfType<DirectorMode>().FirstOrDefault();
        }

        LineRenderer Line(string name, Material mat, int points)
        {
            LineRenderer lr = new GameObject(name).AddComponent<LineRenderer>();
            lr.transform.SetParent(transform, false);
            lr.useWorldSpace = true;
            lr.positionCount = points;
            lr.material = mat;
            lr.numCapVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            return lr;
        }

        /// <summary>The UI module points with the right hand's aim ray and clicks with its index pinch.</summary>
        static void ConfigureUiInput(Transform trackingSpace)
        {
            UiFactory.EnsureEventSystem();
            InputSystemUIInputModule module = EventSystem.current != null
                ? EventSystem.current.GetComponent<InputSystemUIInputModule>()
                : null;
            if (module == null) return;

            InputActionAsset asset = ScriptableObject.CreateInstance<InputActionAsset>();
            InputActionMap map = asset.AddActionMap("UI");
            InputAction point = map.AddAction("Point", InputActionType.PassThrough, "<Mouse>/position");
            InputAction click = map.AddAction("Click", InputActionType.PassThrough);
            click.AddBinding("<MetaAimHand>{RightHand}/indexPressed");
            click.AddBinding("<Mouse>/leftButton");
            InputAction position = map.AddAction("TrackedDevicePosition", InputActionType.PassThrough,
                "<MetaAimHand>{RightHand}/devicePosition");
            InputAction orientation = map.AddAction("TrackedDeviceOrientation", InputActionType.PassThrough,
                "<MetaAimHand>{RightHand}/deviceRotation");
            module.actionsAsset = asset;
            module.point = InputActionReference.Create(point);
            module.leftClick = InputActionReference.Create(click);
            module.trackedDevicePosition = InputActionReference.Create(position);
            module.trackedDeviceOrientation = InputActionReference.Create(orientation);
            module.xrTrackingOrigin = trackingSpace;
            asset.Enable();
        }

        void OnDestroy()
        {
            if (ReferenceEquals(PointerInput.Source, this)) PointerInput.Source = null;
            if (Instance == this) Instance = null;
            aimPosition?.Dispose();
            aimRotation?.Dispose();
            aimTracked?.Dispose();
        }

        // ------------------------------------------------------------------ per frame

        void Update()
        {
            if (rig == null) return;
            float dt = Time.unscaledDeltaTime;

            if (recenterFrames >= 0 && ++recenterFrames > 20)
            {
                recenterFrames = -1;
                Recenter();
            }

            if (director == null) director = root.Modules.OfType<DirectorMode>().FirstOrDefault();
            ReadHands();
            Aim();
            Gestures();
            Palm(dt);
            FollowHud(dt);
            Draw();
            beacon.Update(director, rig);
        }

        public void Recenter()
        {
            rig.RecenterXr(head.localPosition, head.localEulerAngles.y);
            hudPlaced = false;
        }

        void ReadHands()
        {
            if (hands == null || !hands.running)
            {
                found.Clear();
                SubsystemManager.GetSubsystems(found);
                hands = found.FirstOrDefault(h => h.running);
            }

            bool available = hands != null;
            left.Read(available ? hands.leftHand : default, available);
            right.Read(available ? hands.rightHand : default, available);
        }

        /// <summary>The right hand's pointing ray: Meta's aim pose, or a ray from the shoulder through the hand.</summary>
        void Aim()
        {
            if (aimTracked.IsPressed())
            {
                rightAim.localPosition = aimPosition.ReadValue<Vector3>();
                rightAim.localRotation = aimRotation.ReadValue<Quaternion>();
                aimValid = true;
                return;
            }

            if (!right.Tracked)
            {
                aimValid = false;
                return;
            }

            // shoulder estimate: below and to the right of the head, turned with it
            Quaternion yaw = Quaternion.Euler(0, head.localEulerAngles.y, 0);
            Vector3 shoulder = head.localPosition + yaw * new Vector3(0.17f, -0.2f, 0);
            Vector3 through = right.Joint(XRHandJointID.IndexProximal);
            Vector3 dir = through - shoulder;
            if (dir.sqrMagnitude < 1e-6f)
            {
                aimValid = false;
                return;
            }

            rightAim.localPosition = through;
            rightAim.localRotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            aimValid = true;
        }

        /// <summary>Pinch-drag moves the world, two pinches scale and turn it; a pinch that stays put is a select.</summary>
        void Gestures()
        {
            Transform offset = XrSession.CameraOffset;

            // a pinch that starts on a panel belongs to the panel
            if (right.PinchStarted) right.OnUi = UI.HudKit.PointerOverUi();
            if (left.PinchStarted) left.OnUi = false;

            if (left.Pinching && right.Pinching && !right.OnUi)
            {
                Vector3 a = left.Pinch, b = right.Pinch, span = b - a;
                // tracking space turns with the origin, so these angles and lengths never feed back on themselves
                float angle = Mathf.Atan2(span.x, span.z) * Mathf.Rad2Deg;
                float length = span.magnitude;
                if (twoHanded && lastSpan > 0.03f && length > 0.03f)
                {
                    Vector3 pivot = offset.TransformPoint((a + b) * 0.5f);
                    rig.XrZoom(lastSpan / length, pivot);
                    rig.XrTwist(Mathf.DeltaAngle(lastAngle, angle), pivot);
                }

                twoHanded = true;
                lastSpan = length;
                lastAngle = angle;
                left.Dragging = right.Dragging = true; // neither hand selects after a two-hand gesture
                return;
            }

            twoHanded = false;
            foreach (XrHand h in new[] { left, right })
            {
                if (!h.Pinching || h.OnUi) continue;
                if (!h.Dragging && (h.Pinch - h.PinchStart).magnitude > DragStart) h.Dragging = true;
                // the world follows the hand: move the eye the other way by the hand's motion, in world units
                if (h.Dragging) rig.XrMove(-offset.TransformVector(h.Pinch - h.LastPinch));
            }
        }

        /// <summary>The menu shows while the left palm faces the eyes.</summary>
        void Palm(float dt)
        {
            bool facing = false;
            if (left.Tracked)
            {
                Pose palm = left.PalmPose;
                // the joint's up is the back of the hand; the palm faces the other way
                Vector3 palmNormal = -(palm.rotation * Vector3.up);
                Vector3 toHead = (head.localPosition - palm.position).normalized;
                float d = Vector3.Dot(palmNormal, toHead);
                facing = menu.Hidden ? d > 0.65f : d > 0.35f;
                palmAnchor.localPosition = palm.position + palmNormal * 0.05f + Vector3.up * 0.1f;
            }

            palmFacing = Mathf.MoveTowards(palmFacing, facing ? 1 : 0, dt / 0.25f);
            if (menu.Hidden && palmFacing >= 1) menu.SetShown(true);
            else if (!menu.Hidden && palmFacing <= 0) menu.SetShown(false);
        }

        /// <summary>The HUD stays put while you look around it, and swings back in front when you look away.</summary>
        void FollowHud(float dt)
        {
            Transform hud = XrSession.Hud;
            if (hud == null) return;
            float headYaw = head.localEulerAngles.y;
            Vector3 headPos = head.localPosition;
            if (!hudPlaced)
            {
                hudPlaced = true;
                hudYaw = headYaw;
                hudPosition = headPos;
            }

            float off = Mathf.DeltaAngle(hudYaw, headYaw);
            if (Mathf.Abs(off) > HudFollowAngle) hudYaw = Mathf.MoveTowardsAngle(hudYaw, headYaw, Mathf.Abs(off) * 3f * dt);
            hudPosition = Vector3.Lerp(hudPosition, headPos, 1f - Mathf.Exp(-2.5f * dt));
            Quaternion rot = Quaternion.Euler(8f, hudYaw, 0);
            hud.localPosition = hudPosition + rot * new Vector3(0, -0.05f, XrSession.HudDistance);
            hud.localRotation = rot;
        }

        void Draw()
        {
            float s = rig.XrScale;
            Transform offset = XrSession.CameraOffset;
            left.DrawSkeleton(offset, s);
            right.DrawSkeleton(offset, s);

            ray.enabled = aimValid && !right.Dragging && !twoHanded;
            if (!ray.enabled) return;
            Vector3 a = rightAim.position;
            ray.SetPosition(0, a);
            ray.SetPosition(1, a + rightAim.forward * (2.5f * s));
            ray.widthMultiplier = 0.003f * s;
            Color c = right.Pinching ? new Color(1f, 0.85f, 0.5f, 0.85f) : new Color(0.85f, 0.9f, 1f, 0.5f);
            ray.startColor = c;
            ray.endColor = new Color(c.r, c.g, c.b, 0);
        }
    }

    /// <summary>One tracked hand: its joints (tracking space), its pinch with hysteresis, and its drawn skeleton.</summary>
    sealed class XrHand
    {
        /// <summary>A pinch closes below this thumb-index tip distance and opens above the next (metres).</summary>
        const float PinchClose = 0.018f, PinchOpen = 0.035f;

        static readonly XRHandJointID[][] Chains =
        {
            new[] { XRHandJointID.Wrist, XRHandJointID.ThumbMetacarpal, XRHandJointID.ThumbProximal, XRHandJointID.ThumbDistal, XRHandJointID.ThumbTip },
            new[] { XRHandJointID.Wrist, XRHandJointID.IndexMetacarpal, XRHandJointID.IndexProximal, XRHandJointID.IndexIntermediate, XRHandJointID.IndexDistal, XRHandJointID.IndexTip },
            new[] { XRHandJointID.Wrist, XRHandJointID.MiddleMetacarpal, XRHandJointID.MiddleProximal, XRHandJointID.MiddleIntermediate, XRHandJointID.MiddleDistal, XRHandJointID.MiddleTip },
            new[] { XRHandJointID.Wrist, XRHandJointID.RingMetacarpal, XRHandJointID.RingProximal, XRHandJointID.RingIntermediate, XRHandJointID.RingDistal, XRHandJointID.RingTip },
            new[] { XRHandJointID.Wrist, XRHandJointID.LittleMetacarpal, XRHandJointID.LittleProximal, XRHandJointID.LittleIntermediate, XRHandJointID.LittleDistal, XRHandJointID.LittleTip },
            new[] { XRHandJointID.IndexProximal, XRHandJointID.MiddleProximal, XRHandJointID.RingProximal, XRHandJointID.LittleProximal },
        };

        readonly Handedness side;
        readonly Vector3[] joints = new Vector3[(int)XRHandJointID.EndMarker];
        LineRenderer[] bones;

        public bool Tracked { get; private set; }
        public Pose PalmPose { get; private set; }
        public bool Pinching { get; private set; }
        public bool PinchStarted { get; private set; }
        public bool PinchEnded { get; private set; }

        /// <summary>The pinch point (between thumb and index tips) this frame and last frame, tracking space.</summary>
        public Vector3 Pinch { get; private set; }
        public Vector3 LastPinch { get; private set; }
        public Vector3 PinchStart { get; private set; }

        /// <summary>The current pinch moves the world (it will not select).</summary>
        public bool Dragging { get; set; }

        /// <summary>The current pinch started on a panel.</summary>
        public bool OnUi { get; set; }

        public XrHand(Handedness side) => this.side = side;

        public Vector3 Joint(XRHandJointID id) => joints[(int)id];

        public void Read(XRHand hand, bool available)
        {
            bool was = Pinching;
            PinchStarted = PinchEnded = false;
            Tracked = available && hand.isTracked;
            if (!Tracked)
            {
                PinchEnded = was;
                Pinching = Dragging = false;
                return;
            }

            for (int i = (int)XRHandJointID.BeginMarker; i < (int)XRHandJointID.EndMarker; i++)
            {
                if (hand.GetJoint((XRHandJointID)i).TryGetPose(out Pose p)) joints[i] = p.position;
            }

            if (hand.GetJoint(XRHandJointID.Palm).TryGetPose(out Pose palm)) PalmPose = palm;
            Vector3 thumb = Joint(XRHandJointID.ThumbTip), index = Joint(XRHandJointID.IndexTip);
            float gap = Vector3.Distance(thumb, index);
            LastPinch = Pinch;
            Pinch = (thumb + index) * 0.5f;
            Pinching = was ? gap < PinchOpen : gap < PinchClose;
            if (Pinching && !was)
            {
                PinchStarted = true;
                PinchStart = LastPinch = Pinch;
                Dragging = false;
            }
            else if (!Pinching && was)
            {
                PinchEnded = true;
            }
            else if (!Pinching)
            {
                Dragging = false;
            }
        }

        public void BuildSkeleton(Transform parent, Material mat)
        {
            bones = new LineRenderer[Chains.Length];
            for (int c = 0; c < Chains.Length; c++)
            {
                LineRenderer lr = new GameObject($"{side} hand {c}").AddComponent<LineRenderer>();
                lr.transform.SetParent(parent, false);
                lr.useWorldSpace = true;
                lr.positionCount = Chains[c].Length;
                lr.material = mat;
                lr.numCapVertices = 3;
                lr.numCornerVertices = 2;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.enabled = false;
                bones[c] = lr;
            }
        }

        public void DrawSkeleton(Transform trackingSpace, float scale)
        {
            if (bones == null) return;
            Color c = Pinching ? new Color(1f, 0.85f, 0.55f, 0.75f) : new Color(0.85f, 0.9f, 1f, 0.45f);
            foreach (LineRenderer lr in bones) lr.enabled = Tracked;
            if (!Tracked) return;
            for (int b = 0; b < bones.Length; b++)
            {
                LineRenderer lr = bones[b];
                XRHandJointID[] chain = Chains[b];
                for (int i = 0; i < chain.Length; i++) lr.SetPosition(i, trackingSpace.TransformPoint(Joint(chain[i])));
                lr.widthMultiplier = 0.007f * scale;
                lr.startColor = lr.endColor = c;
            }
        }
    }
}
