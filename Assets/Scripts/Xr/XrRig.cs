using System.Linq;
using UnityEngine;
using Why.Director;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem.XR;

namespace Why.Xr
{
    /// <summary>
    /// The headset presentation of both graphs (Meta Quest through OpenXR). Builds the tracking-space rig the camera
    /// rig drives (<see cref="CameraRig.XrOrigin"/>), tracks the head and both controllers, and maps the controllers:
    ///
    /// * right trigger: point and select (labels, people, companies, panels) - it is <see cref="PointerInput"/>
    /// * grip one hand and pull: drag the world; grip both and pull apart / together: grow or shrink it around your hands
    /// * left stick: fly over the graph; right stick up / down: zoom, left / right: turn the world 30 degrees
    /// * A: next stop of the guided tour (starts it), B: previous stop, Y: leave the tour
    /// * X: the menu on your left hand (views, tour, the other scene, recenter); left stick click: recenter
    ///
    /// Every canvas moves onto a HUD plane that lazily follows your gaze (<see cref="XrSession"/>). On the desktop
    /// this module removes itself.
    /// </summary>
    [GraphScenes(GraphScene.Why, GraphScene.Economy)]
    public sealed class XrRig : GraphModule, PointerInput.ISource
    {
        /// <summary>Snap turn step, degrees.</summary>
        const float TurnStep = 30f;

        /// <summary>The HUD re-centres on the gaze when you look further away from it than this (degrees).</summary>
        const float HudFollowAngle = 38f;

        public override int Order => -100;

        GraphRoot root;
        CameraRig rig;
        Transform head, leftHand, rightHand;
        InputAction leftStick, rightStick, rightTrigger, leftGrip, rightGrip, buttonA, buttonB, buttonX, buttonY, recenter;
        InputAction rightPosition, leftPosition;
        LineRenderer ray;
        Transform rayDot;
        XrMenu menu;
        XrBeacon beacon;
        DirectorMode director;

        // grip dragging (tracking space)
        Vector3 lastLeftLocal, lastRightLocal;
        bool leftWasGripping, rightWasGripping;
        bool turnArmed = true;
        float hudYaw;
        Vector3 hudPosition;
        bool hudPlaced;
        int recenterFrames = -1;

        public static XrRig Instance { get; private set; }

        // ------------------------------------------------------------------ PointerInput.ISource

        public bool Valid => rightHand != null && rightPosition != null && rightPosition.activeControl != null;
        public Ray Ray => new Ray(rightHand.position, rightHand.forward);
        public bool Pressed => rightTrigger != null && rightTrigger.IsPressed();
        public bool PressedThisFrame => rightTrigger != null && rightTrigger.WasPressedThisFrame();
        public bool ReleasedThisFrame => rightTrigger != null && rightTrigger.WasReleasedThisFrame();

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
            Track(head.gameObject, "<XRHMD>/centerEyePosition", "<XRHMD>/centerEyeRotation", "<XRHMD>/trackingState");

            leftHand = Hand(offset, "Left Hand", "LeftHand");
            rightHand = Hand(offset, "Right Hand", "RightHand");
            leftPosition = new InputAction("LeftPosition", InputActionType.Value, "<XRController>{LeftHand}/pointerPosition");
            rightPosition = new InputAction("RightPosition", InputActionType.Value, "<XRController>{RightHand}/pointerPosition");

            leftStick = Value("LeftStick", "<XRController>{LeftHand}/{Primary2DAxis}");
            rightStick = Value("RightStick", "<XRController>{RightHand}/{Primary2DAxis}");
            rightTrigger = Button("RightTrigger", "<XRController>{RightHand}/{TriggerButton}");
            leftGrip = Button("LeftGrip", "<XRController>{LeftHand}/{GripButton}");
            rightGrip = Button("RightGrip", "<XRController>{RightHand}/{GripButton}");
            buttonA = Button("A", "<XRController>{RightHand}/{PrimaryButton}");
            buttonB = Button("B", "<XRController>{RightHand}/{SecondaryButton}");
            buttonX = Button("X", "<XRController>{LeftHand}/{PrimaryButton}");
            buttonY = Button("Y", "<XRController>{LeftHand}/{SecondaryButton}");
            recenter = Button("Recenter", "<XRController>{LeftHand}/{Primary2DAxisClick}");
            foreach (InputAction a in Actions()) a.Enable();

            // the HUD plane, in tracking space so it keeps its size in metres whatever the world's scale
            XrSession.Hud = new GameObject("HUD").transform;
            XrSession.Hud.SetParent(offset, false);
            XrSession.AttachPending();

            ConfigureUiInput(offset);
            BuildRay();
            PointerInput.Source = this;

            rig.AttachXr(origin);
            beacon = new XrBeacon(transform);
            menu = gameObject.AddComponent<XrMenu>();
            menu.Init(root, leftHand);

            // OpenXR starts tracking a few frames in; recenter once the head reports a pose
            recenterFrames = 0;
        }

        public override void OnLoaded(GraphRoot graphRoot)
        {
            director = graphRoot.Modules.OfType<DirectorMode>().FirstOrDefault();
        }

        static void Track(GameObject go, string position, string rotation, string state)
        {
            TrackedPoseDriver tpd = go.AddComponent<TrackedPoseDriver>();
            tpd.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            tpd.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            tpd.positionInput = new InputActionProperty(new InputAction(go.name + "Position", InputActionType.Value, position));
            tpd.rotationInput = new InputActionProperty(new InputAction(go.name + "Rotation", InputActionType.Value, rotation));
            if (state != null)
            {
                tpd.trackingStateInput = new InputActionProperty(new InputAction(go.name + "State", InputActionType.Value, state));
            }
        }

        static Transform Hand(Transform parent, string name, string usage)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Track(go, $"<XRController>{{{usage}}}/pointerPosition", $"<XRController>{{{usage}}}/pointerRotation",
                $"<XRController>{{{usage}}}/trackingState");
            return go.transform;
        }

        static InputAction Value(string name, string binding) => new InputAction(name, InputActionType.Value, binding);
        static InputAction Button(string name, string binding) => new InputAction(name, InputActionType.Button, binding);

        InputAction[] Actions() => new[]
        {
            leftStick, rightStick, rightTrigger, leftGrip, rightGrip, buttonA, buttonB, buttonX, buttonY, recenter,
            leftPosition, rightPosition,
        };

        /// <summary>The UI module points with the right controller's aim pose and clicks with its trigger.</summary>
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
            click.AddBinding("<XRController>{RightHand}/{TriggerButton}");
            click.AddBinding("<Mouse>/leftButton");
            InputAction position = map.AddAction("TrackedDevicePosition", InputActionType.PassThrough,
                "<XRController>{RightHand}/pointerPosition");
            InputAction orientation = map.AddAction("TrackedDeviceOrientation", InputActionType.PassThrough,
                "<XRController>{RightHand}/pointerRotation");
            module.actionsAsset = asset;
            module.point = InputActionReference.Create(point);
            module.leftClick = InputActionReference.Create(click);
            module.trackedDevicePosition = InputActionReference.Create(position);
            module.trackedDeviceOrientation = InputActionReference.Create(orientation);
            module.xrTrackingOrigin = trackingSpace;
            asset.Enable();
        }

        void BuildRay()
        {
            Material mat = new Material(Shader.Find("Sprites/Default"));
            ray = new GameObject("Pointer Ray").AddComponent<LineRenderer>();
            ray.transform.SetParent(transform, false);
            ray.useWorldSpace = true;
            ray.positionCount = 2;
            ray.material = mat;
            ray.numCapVertices = 2;
            ray.startColor = new Color(0.85f, 0.9f, 1f, 0.55f);
            ray.endColor = new Color(0.85f, 0.9f, 1f, 0f);
            ray.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ray.receiveShadows = false;
        }

        void OnDestroy()
        {
            if (ReferenceEquals(PointerInput.Source, this)) PointerInput.Source = null;
            if (Instance == this) Instance = null;
            if (leftStick == null) return;
            foreach (InputAction a in Actions()) a.Dispose();
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

            if (recenter.WasPressedThisFrame()) Recenter();
            Locomotion(dt);
            Buttons();
            FollowHud(dt);
            UpdateRay();
            beacon.Update(director, rig);
        }

        public void Recenter()
        {
            Vector3 local = head.localPosition;
            float yaw = head.localEulerAngles.y;
            rig.RecenterXr(local, yaw);
            hudPlaced = false;
        }

        void Locomotion(float dt)
        {
            Transform origin = XrSession.Origin;
            float s = rig.XrScale;

            // left stick: fly over the graph, relative to where you look
            Vector2 move = leftStick.ReadValue<Vector2>();
            if (move.sqrMagnitude > 0.02f)
            {
                Vector3 fwd = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
                Vector3 right = Vector3.ProjectOnPlane(head.right, Vector3.up).normalized;
                rig.XrMove((fwd * move.y + right * move.x) * (rig.Pose.Distance * 0.9f * dt));
            }

            // right stick: zoom (up = closer), snap turn
            Vector2 look = rightStick.ReadValue<Vector2>();
            if (Mathf.Abs(look.y) > 0.25f && Mathf.Abs(look.y) > Mathf.Abs(look.x))
            {
                rig.XrZoom(Mathf.Exp(-look.y * 1.6f * dt), rig.Pose.Target);
            }

            if (turnArmed && Mathf.Abs(look.x) > 0.75f && Mathf.Abs(look.x) > Mathf.Abs(look.y))
            {
                rig.XrTurn(Mathf.Sign(look.x) * TurnStep);
                turnArmed = false;
            }
            else if (Mathf.Abs(look.x) < 0.35f)
            {
                turnArmed = true;
            }

            // grips: drag the world with one hand, scale it with two
            bool lg = leftGrip.IsPressed(), rg = rightGrip.IsPressed();
            Vector3 l = leftHand.localPosition, r = rightHand.localPosition;
            if (lg && rg && leftWasGripping && rightWasGripping)
            {
                float before = Vector3.Distance(lastLeftLocal, lastRightLocal);
                float now = Vector3.Distance(l, r);
                if (before > 0.02f && now > 0.02f)
                {
                    Vector3 pivot = (leftHand.position + rightHand.position) * 0.5f;
                    rig.XrZoom(before / now, pivot);
                }
            }
            else if (rg && rightWasGripping)
            {
                rig.XrMove(-origin.TransformVector(XrSession.CameraOffset.TransformVector(r - lastRightLocal) / s) );
            }
            else if (lg && leftWasGripping)
            {
                rig.XrMove(-origin.TransformVector(XrSession.CameraOffset.TransformVector(l - lastLeftLocal) / s));
            }

            leftWasGripping = lg;
            rightWasGripping = rg;
            lastLeftLocal = l;
            lastRightLocal = r;
        }

        void Buttons()
        {
            if (director == null) director = root.Modules.OfType<DirectorMode>().FirstOrDefault();
            if (buttonX.WasPressedThisFrame()) menu.Toggle();
            if (director == null) return;
            if (buttonA.WasPressedThisFrame())
            {
                if (director.TourRunning) director.NextStep();
                else if (root.IsLoaded) director.StartTour();
            }

            if (buttonB.WasPressedThisFrame() && director.TourRunning) director.PreviousStep();
            if (buttonY.WasPressedThisFrame() && director.TourRunning) director.ExitTour();
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

        void UpdateRay()
        {
            bool show = Valid;
            ray.enabled = show;
            if (!show) return;
            float s = rig.XrScale;
            float length = 2.5f * s;
            Vector3 a = rightHand.position;
            Vector3 b = a + rightHand.forward * length;
            ray.SetPosition(0, a);
            ray.SetPosition(1, b);
            ray.widthMultiplier = 0.004f * s;
            ray.startColor = Pressed ? new Color(1f, 0.85f, 0.5f, 0.8f) : new Color(0.85f, 0.9f, 1f, 0.55f);
        }
    }
}
