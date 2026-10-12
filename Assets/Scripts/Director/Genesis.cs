using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Why.Director
{
    /// <summary>
    /// The opening of the causality graph, on screens and in a headset: a flash at the Big Bang (6 o'clock on the clock
    /// of deep time), a shock ring, and then every causal branch sprouting from that point along time - matter fanning out
    /// with the expansion of space, life rooting, civilizations streaming down the human branch to the present - while the
    /// camera pulls back from the flash to the whole graph (<see cref="GraphReveal"/> drives the growth in the shaders).
    ///
    /// It plays once when the graph has loaded (any click, key or pinch skips it; <c>-nointro</c> turns it off) and is the
    /// first stop of the guided tour (a step with <c>"effect": "genesis"</c>), timed to its narration.
    /// </summary>
    public sealed class Genesis : GraphModule
    {
        /// <summary>Length of the opening when the app starts.</summary>
        public const float IntroSeconds = 11f;

        const float FlashSeconds = 0.9f;
        const float FrontWidth = 0.45f;
        const float FrontGlow = 7f;

        /// <summary>Branches sprout in sequence: a delay per unit of radius (our lineage first) and a spread per branch.</summary>
        const float LagPerRho = 0.55f, LagSpread = 3.2f;

        /// <summary>Length of the bloom trailing each growing tip (world units along the path).</summary>
        const float Trail = 1.6f;

        /// <summary>How far the front must travel past the present for the last, outermost branch to finish.</summary>
        static float FrontEnd => GraphReveal.Length + LagPerRho * 8f + LagSpread + FrontWidth * 2;

        public override int Order => 55;

        /// <summary>True while the graph is growing.</summary>
        public bool Playing { get; private set; }

        GraphRoot root;
        Coroutine running;
        Transform flare, ring;
        Material flareMat;
        LineRenderer ringLine;
        float bloomBase;
        bool labelsWereHidden;

        static bool Disabled
        {
            get
            {
                string[] args = System.Environment.GetCommandLineArgs();
                return args.Contains("-nointro") || args.Contains("-smoketest") || args.Contains("-iconshot");
            }
        }

        public override void Init(GraphRoot graphRoot) => root = graphRoot;

        public override void OnLoaded(GraphRoot graphRoot)
        {
            if (Disabled) return;
            DirectorMode director = root.Modules.OfType<DirectorMode>().FirstOrDefault();
            if (director != null && director.TourRunning) return; // the tour's first stop plays it
            Play(IntroSeconds, null, true);
        }

        /// <summary>Plays the opening over some seconds, ending on a view (the overview by default).</summary>
        public void Play(float seconds, ViewPreset end, bool skippable)
        {
            Stop();
            running = StartCoroutine(Run(Mathf.Max(seconds, 4f), end ?? ViewPresets.Initial, skippable));
        }

        /// <summary>Ends the opening at once: everything shown.</summary>
        public void Stop()
        {
            if (running != null) StopCoroutine(running);
            running = null;
            Finish();
        }

        IEnumerator Run(float seconds, ViewPreset end, bool skippable)
        {
            Playing = true;
            labelsWereHidden = root.Labels.DataLabelsHidden;
            root.Labels.DataLabelsHidden = true;
            GraphReveal.Set(0, FrontWidth, FrontGlow, LagPerRho, LagSpread, Trail);

            // the final arrangement, then the camera put right at the flash
            root.Focus(end, 0.01f);
            GraphWarp.Set(end.Warp());
            CameraPose to = end.Pose();
            Vector3 bang = Anchors.TryGet("epoch:big_bang", out Anchor a) ? a.World : to.Target;
            CameraPose from = to;
            from.Target = bang;
            from.Distance = to.Distance * 0.06f;
            from.Yaw = to.Yaw - 30f;
            from.Pitch = Mathf.Min(to.Pitch, 40f);
            root.Rig.SetPose(from);

            // at startup, darkness until the loading card has faded (it holds 0.55 s and fades over 0.8 s)
            for (float wait = skippable ? 1.3f : 0f; wait > 0; wait -= Time.unscaledDeltaTime) yield return null;
            BuildFlash(bang, from.Distance);
            if (root.Bloom != null) bloomBase = root.Bloom.intensity.value;

            float t = 0;
            bool flying = false;
            float growStart = FlashSeconds * 0.6f, growEnd = seconds - 0.6f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                if (skippable && t > 0.3f && SkipPressed()) break;

                // the flash: a point of light swelling, the bloom surging, a shock ring racing outward
                float f = Mathf.Clamp01(t / FlashSeconds);
                float swell = Mathf.Sin(f * Mathf.PI * 0.5f);
                float fade = 1f - Mathf.Clamp01((t - FlashSeconds) / 2.5f);
                Camera cam = root.Rig.Cam;
                flare.position = bang;
                flare.rotation = cam.transform.rotation;
                flare.localScale = Vector3.one * (from.Distance * (0.12f + 1.25f * swell) * (0.45f + 0.55f * fade));
                flareMat.color = new Color(4f, 3.5f, 2.9f, Mathf.Clamp01(fade * 1.1f) * 0.9f);
                float r = to.Distance * 0.5f * Mathf.Pow(Mathf.Clamp01(t / 3f), 0.6f);
                DrawRing(bang, r, Mathf.Clamp01(1f - t / 3f));
                if (root.Bloom != null) root.Bloom.intensity.Override(bloomBase * (1f + 3.5f * swell * fade));

                // the pull back, once the flash has bloomed
                if (!flying && t > FlashSeconds * 0.5f)
                {
                    flying = true;
                    root.Rig.FlyTo(to, seconds - t);
                }

                // the growth: constant speed along the path, easing in and out
                float g = Mathf.Clamp01((t - growStart) / Mathf.Max(growEnd - growStart, 0.1f));
                g = g * g * (3f - 2f * g);
                GraphReveal.Set(g * FrontEnd, FrontWidth, FrontGlow * (1f - 0.35f * g), LagPerRho, LagSpread, Trail);
                yield return null;
            }

            if (t < seconds) root.Rig.FlyTo(to, 0.8f); // skipped
            running = null;
            Finish();
        }

        static bool SkipPressed()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.anyKey.wasPressedThisFrame) return true;
            Mouse mouse = Mouse.current;
            if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)) return true;
            return PointerInput.IsXr && PointerInput.PressedThisFrame;
        }

        void Finish()
        {
            if (!Playing) return;
            Playing = false;
            GraphReveal.Off();
            root.Labels.DataLabelsHidden = labelsWereHidden;
            if (root.Bloom != null && bloomBase > 0) root.Bloom.intensity.Override(bloomBase);
            if (flare != null) Destroy(flare.gameObject);
            if (ring != null) Destroy(ring.gameObject);
            flare = ring = null;
        }

        // ------------------------------------------------------------------ the flash

        void BuildFlash(Vector3 at, float scale)
        {
            if (flare != null) Destroy(flare.gameObject);
            if (ring != null) Destroy(ring.gameObject);

            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Big Bang";
            Destroy(quad.GetComponent<Collider>());
            flareMat = new Material(Shader.Find("Sprites/Default")) { mainTexture = FlareTexture() };
            MeshRenderer mr = quad.GetComponent<MeshRenderer>();
            mr.sharedMaterial = flareMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            flare = quad.transform;
            flare.SetParent(transform, false);
            flare.position = at;

            ringLine = new GameObject("Shock ring").AddComponent<LineRenderer>();
            ring = ringLine.transform;
            ring.SetParent(transform, false);
            ringLine.useWorldSpace = true;
            ringLine.loop = true;
            ringLine.positionCount = 96;
            ringLine.material = new Material(Shader.Find("Sprites/Default"));
            ringLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ringLine.receiveShadows = false;
            ringLine.widthMultiplier = scale * 0.08f;
        }

        void DrawRing(Vector3 c, float r, float alpha)
        {
            if (ringLine == null) return;
            ringLine.enabled = alpha > 0.01f;
            if (!ringLine.enabled) return;
            for (int i = 0; i < ringLine.positionCount; i++)
            {
                float a = i / (float)ringLine.positionCount * Mathf.PI * 2;
                ringLine.SetPosition(i, c + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r));
            }

            Color col = new Color(1f, 0.85f, 0.65f, alpha * 0.8f);
            ringLine.startColor = ringLine.endColor = col;
            ringLine.widthMultiplier = Mathf.Max(r * 0.012f, 1e-4f);
        }

        static Texture2D flareTexture;

        /// <summary>A soft white core with a halo and four faint spikes.</summary>
        static Texture2D FlareTexture()
        {
            if (flareTexture != null) return flareTexture;
            const int n = 256;
            flareTexture = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            Color32[] px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2 - 1, v = (y + 0.5f) / n * 2 - 1;
                float r2 = u * u + v * v;
                float core = Mathf.Exp(-r2 * 60f);
                float halo = Mathf.Exp(-r2 * 7f) * 0.55f;
                float spikes = (Mathf.Exp(-Mathf.Abs(v) * 70f) * Mathf.Exp(-Mathf.Abs(u) * 2.5f) +
                                Mathf.Exp(-Mathf.Abs(u) * 70f) * Mathf.Exp(-Mathf.Abs(v) * 2.5f)) * 0.5f;
                float a = Mathf.Clamp01(core + halo + spikes) * Mathf.Clamp01((1f - Mathf.Sqrt(r2)) * 4f);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }

            flareTexture.SetPixels32(px);
            flareTexture.Apply(true, true);
            return flareTexture;
        }
    }
}
