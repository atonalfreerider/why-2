using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Debug = UnityEngine.Debug;

namespace Why
{
    /// <summary>
    /// A UI or behavior module (HUD, director, loading screen...). Discovered by reflection and created
    /// by <see cref="GraphRoot"/>, so modules never need to be wired into the scene by hand.
    /// </summary>
    public abstract class GraphModule : MonoBehaviour
    {
        public virtual int Order => 0;

        /// <summary>Called right after creation, before loading starts.</summary>
        public virtual void Init(GraphRoot root) { }

        /// <summary>Called once every layer is uploaded.</summary>
        public virtual void OnLoaded(GraphRoot root) { }
    }

    /// <summary>
    /// Bootstraps the causality graph: camera and post-processing, layers and modules, asynchronous
    /// loading, and focus transitions between <see cref="ViewPresets"/>.
    /// </summary>
    public sealed class GraphRoot : MonoBehaviour
    {
        public static GraphRoot Instance { get; private set; }

        public CameraRig Rig { get; private set; }
        public LabelSystem Labels { get; private set; }
        public GraphContext Context { get; private set; }
        public IReadOnlyList<GraphLayer> Layers => layers;
        public IReadOnlyList<GraphModule> Modules => modules;

        public bool IsLoaded { get; private set; }
        public float LoadProgress { get; private set; }
        public string LoadStatus { get; private set; } = "Starting";
        public double LoadSeconds { get; private set; }
        public ViewPreset CurrentPreset { get; private set; }

        /// <summary>When false, number keys do not change the focus (e.g. while the director runs).</summary>
        public bool AllowPresetKeys { get; set; } = true;

        public event Action Loaded;
        public event Action<ViewPreset> FocusChanged;

        /// <summary>Raised when something (HUD button, key) asks for the guided tour.</summary>
        public event Action TourRequested;

        /// <summary>Set by the director while the guided tour runs (HUD dims its own controls).</summary>
        public bool TourActive { get; set; }

        public void RequestTour() => TourRequested?.Invoke();

        readonly List<GraphLayer> layers = new List<GraphLayer>();
        readonly List<GraphModule> modules = new List<GraphModule>();

        void Awake()
        {
            Instance = this;
            Anchors.Clear();
            GraphWarp.Set(WarpState.Polar);
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = -1;

            SetupCamera();
            SetupPostProcessing();

            Labels = new GameObject("Labels").AddComponent<LabelSystem>();
            Labels.transform.SetParent(transform, false);
            Labels.Init(Rig);

            Context = new GraphContext { NowYear = DeepTime.NowYear, Labels = Labels, Root = transform };

            foreach (Type t in FindTypes<GraphLayer>().OrderBy(t => t.FullName))
            {
                GameObject go = new GameObject(t.Name);
                go.transform.SetParent(transform, false);
                layers.Add((GraphLayer)go.AddComponent(t));
            }

            layers.Sort((a, b) => a.Order.CompareTo(b.Order));

            foreach (Type t in FindTypes<GraphModule>().OrderBy(t => t.FullName))
            {
                GameObject go = new GameObject(t.Name);
                go.transform.SetParent(transform, false);
                modules.Add((GraphModule)go.AddComponent(t));
            }

            modules.Sort((a, b) => a.Order.CompareTo(b.Order));
            foreach (GraphModule m in modules) m.Init(this);
        }

        IEnumerator Start()
        {
            CurrentPreset = ViewPresets.Get("overview");
            GraphWarp.Set(CurrentPreset.Warp());
            Rig.SetPose(CurrentPreset.Pose());

            Stopwatch sw = Stopwatch.StartNew();

            // 1. text assets (main thread)
            LoadStatus = "Reading data";
            HashSet<string> paths = new HashSet<string>();
            foreach (GraphLayer layer in layers)
            {
                foreach (string p in layer.RequiredTexts) paths.Add(p);
            }

            foreach (string p in paths)
            {
                TextAsset ta = Resources.Load<TextAsset>(p);
                if (ta == null)
                {
                    Debug.LogWarning($"[Why] missing text resource '{p}'");
                    continue;
                }

                Context.SetText(p, ta.text);
                Resources.UnloadAsset(ta);
            }

            LoadProgress = 0.1f;
            yield return null;

            // 2. prepare every layer concurrently on worker threads, in Order groups
            foreach (IGrouping<int, GraphLayer> group in layers.GroupBy(l => l.Order).OrderBy(g => g.Key))
            {
                LoadStatus = "Building " + string.Join(", ", group.Select(l => l.GetType().Name));
                List<(GraphLayer layer, Task task)> tasks = group
                    .Select(l => (l, Task.Run(() => l.Prepare(Context)))).ToList();
                while (tasks.Any(t => !t.task.IsCompleted)) yield return null;

                foreach ((GraphLayer layer, Task task) in tasks)
                {
                    if (task.IsFaulted)
                    {
                        Debug.LogError($"[Why] {layer.GetType().Name}.Prepare failed: {task.Exception?.GetBaseException()}");
                    }
                }

                LoadProgress = Mathf.Min(0.85f, LoadProgress + 0.7f / Mathf.Max(1, layers.Count));
            }

            // 3. upload (main thread)
            foreach (GraphLayer layer in layers)
            {
                LoadStatus = "Uploading " + layer.GetType().Name;
                Stopwatch lw = Stopwatch.StartNew();
                try
                {
                    layer.Upload(Context);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Why] {layer.GetType().Name}.Upload failed: {e}");
                }

                if (lw.ElapsedMilliseconds > 50) Debug.Log($"[Why] {layer.GetType().Name}.Upload {lw.ElapsedMilliseconds} ms");
            }

            LoadSeconds = sw.Elapsed.TotalSeconds;
            LoadProgress = 1;
            LoadStatus = "Ready";
            IsLoaded = true;
            Debug.Log($"[Why] graph loaded in {LoadSeconds:0.00} s ({layers.Count} layers, {Anchors.All().Count} anchors)");

            Labels.MarkDirty();
            foreach (GraphModule m in modules) m.OnLoaded(this);
            Loaded?.Invoke();
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            GraphWarp.Tick(dt);
            Highlighter.Tick(dt);

            if (IsLoaded)
            {
                foreach (GraphLayer layer in layers) layer.Tick(Context, Rig);
            }

            Keyboard kb = Keyboard.current;
            if (kb != null && AllowPresetKeys && IsLoaded)
            {
                foreach (ViewPreset p in ViewPresets.All)
                {
                    if (p.Key == KeyCode.None) continue;
                    if (KeyPressed(kb, p.Key)) Focus(p);
                }
            }
        }

        static bool KeyPressed(Keyboard kb, KeyCode code)
        {
            switch (code)
            {
                case KeyCode.Alpha1: return kb.digit1Key.wasPressedThisFrame;
                case KeyCode.Alpha2: return kb.digit2Key.wasPressedThisFrame;
                case KeyCode.Alpha3: return kb.digit3Key.wasPressedThisFrame;
                case KeyCode.Alpha4: return kb.digit4Key.wasPressedThisFrame;
                case KeyCode.Alpha5: return kb.digit5Key.wasPressedThisFrame;
                case KeyCode.Alpha6: return kb.digit6Key.wasPressedThisFrame;
                case KeyCode.Alpha7: return kb.digit7Key.wasPressedThisFrame;
                case KeyCode.Alpha8: return kb.digit8Key.wasPressedThisFrame;
                case KeyCode.Alpha9: return kb.digit9Key.wasPressedThisFrame;
                case KeyCode.Alpha0: return kb.digit0Key.wasPressedThisFrame;
                default: return false;
            }
        }

        /// <summary>Re-scale the graph to a preset: animates the warp and flies the camera.</summary>
        public void Focus(ViewPreset preset, float seconds = 2.2f)
        {
            if (preset == null) return;
            CurrentPreset = preset;
            GraphWarp.AnimateTo(preset.Warp(), seconds);
            Rig.FlyTo(preset.Pose(), seconds);
            foreach (GraphLayer layer in layers) layer.OnFocus(preset);
            FocusChanged?.Invoke(preset);
        }

        public void Focus(string presetId, float seconds = 2.2f) => Focus(ViewPresets.Get(presetId), seconds);

        void SetupCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                cam = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            }

            // retire legacy controllers that may still be on the camera
            foreach (MonoBehaviour mb in cam.GetComponents<MonoBehaviour>())
            {
                if (mb != null && mb.GetType().Name == "CameraControl") mb.enabled = false;
            }

            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = GraphStyle.Background;
            cam.allowHDR = true;
            cam.allowMSAA = true;
            cam.fieldOfView = 45;
            UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.None;
            data.renderShadows = false;

            Rig = cam.GetComponent<CameraRig>();
            if (Rig == null) Rig = cam.gameObject.AddComponent<CameraRig>();
        }

        void SetupPostProcessing()
        {
            GameObject go = new GameObject("PostProcessing");
            go.transform.SetParent(transform, false);
            Volume volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10;
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();

            Bloom bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.9f);
            bloom.intensity.Override(1.15f);
            bloom.scatter.Override(0.72f);
            bloom.highQualityFiltering.Override(true);

            Tonemapping tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.Neutral);

            Vignette vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.32f);
            vignette.smoothness.Override(0.45f);

            volume.sharedProfile = profile;
        }

        static IEnumerable<Type> FindTypes<T>()
        {
            Type baseType = typeof(T);
            return baseType.Assembly.GetTypes().Where(t => baseType.IsAssignableFrom(t) && !t.IsAbstract);
        }
    }
}
