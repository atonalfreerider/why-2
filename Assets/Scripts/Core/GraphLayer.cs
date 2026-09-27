using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Why
{
    /// <summary>
    /// Shared state for building layers. Text resources are loaded on the main thread before
    /// <see cref="GraphLayer.Prepare"/> runs on a worker thread.
    /// </summary>
    public sealed class GraphContext
    {
        public double NowYear;
        public LabelSystem Labels;
        public Transform Root;

        readonly Dictionary<string, string> texts = new Dictionary<string, string>();
        readonly Dictionary<string, object> shared = new Dictionary<string, object>();
        readonly object gate = new object();

        public void SetText(string resourcePath, string text)
        {
            lock (gate) texts[resourcePath] = text;
        }

        /// <summary>Text of a Resources path requested through <see cref="GraphLayer.RequiredTexts"/>.</summary>
        public string Text(string resourcePath)
        {
            lock (gate) return texts.TryGetValue(resourcePath, out string t) ? t : null;
        }

        /// <summary>Publish data for other layers (e.g. world population) - thread safe.</summary>
        public void Share(string key, object value)
        {
            lock (gate) shared[key] = value;
        }

        public T Shared<T>(string key) where T : class
        {
            lock (gate) return shared.TryGetValue(key, out object v) ? v as T : null;
        }

        public double YearsAgo(double calendarYear) => NowYear - calendarYear;
    }

    /// <summary>
    /// Base class for a layer of the graph. Layers are discovered by reflection and built by
    /// <see cref="GraphRoot"/>: <see cref="Prepare"/> on a worker thread (no Unity object APIs), then
    /// <see cref="Upload"/> on the main thread.
    /// </summary>
    public abstract class GraphLayer : MonoBehaviour
    {
        /// <summary>Build order (lower first). Layers can read data shared by lower orders in Upload.</summary>
        public virtual int Order => 0;

        /// <summary>Resources paths (without extension) of TextAssets this layer needs.</summary>
        public virtual IEnumerable<string> RequiredTexts => System.Array.Empty<string>();

        /// <summary>Worker thread: parse, lay out, and fill mesh builders. Register anchors and labels.</summary>
        public abstract void Prepare(GraphContext ctx);

        /// <summary>Main thread: create meshes and renderers from what Prepare produced.</summary>
        public abstract void Upload(GraphContext ctx);

        /// <summary>Main thread, every frame after loading (level-of-detail, animation).</summary>
        public virtual void Tick(GraphContext ctx, CameraRig rig) { }

        /// <summary>Called when the view preset changes (after the warp animation starts).</summary>
        public virtual void OnFocus(ViewPreset preset) { }

        protected MeshRenderer AddMesh(string meshName, Mesh mesh, Material material)
        {
            GameObject go = new GameObject(meshName);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            r.allowOcclusionWhenDynamic = false;
            return r;
        }
    }

    /// <summary>Creates materials for the Why shaders. Main thread only.</summary>
    public static class GraphMaterials
    {
        public const int QueueMatter = 3000;
        public const int QueueLife = 3100;
        public const int QueueHumans = 3200;
        public const int QueueOverlay = 3300;

        static Shader lineShader, surfaceShader;

        public static Shader LineShader => lineShader ? lineShader : (lineShader = Shader.Find("Why/Line"));
        public static Shader SurfaceShader => surfaceShader ? surfaceShader : (surfaceShader = Shader.Find("Why/Surface"));

        /// <summary>Line material. Additive by default so overlapping lines accumulate light.</summary>
        public static Material Line(Color color, float intensity, int queue, bool additive = true, float rhoFade = 0,
            float flow = 0)
        {
            Material m = new Material(LineShader) { renderQueue = queue };
            m.SetColor("_Color", new Color(color.r * intensity, color.g * intensity, color.b * intensity, color.a));
            m.SetFloat("_RhoFade", rhoFade);
            m.SetFloat("_Flow", flow);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            return m;
        }

        /// <summary>Surface material. Alpha blended by default.</summary>
        public static Material Surface(Color color, float intensity, int queue, bool additive = false,
            float rhoFade = 0, float noiseScale = 3)
        {
            Material m = new Material(SurfaceShader) { renderQueue = queue };
            m.SetColor("_Color", new Color(color.r * intensity, color.g * intensity, color.b * intensity, color.a));
            m.SetFloat("_RhoFade", rhoFade);
            m.SetFloat("_NoiseScale", noiseScale);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            return m;
        }

        /// <summary>Fades a material in or out (level of detail) without touching its color.</summary>
        public static void SetAlpha(Material m, float alpha) => m.SetFloat("_Alpha", Mathf.Clamp01(alpha));
    }
}
