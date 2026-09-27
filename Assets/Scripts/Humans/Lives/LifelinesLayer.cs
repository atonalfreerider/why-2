using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Why.Humans.Lives
{
    /// <summary>
    /// BLUE layer, the people inside the civilization streams. Every stream (prehistoric humanity included)
    /// carries a statistical population drawn as lifelines: born in proportion to the stream's births, living
    /// a lifespan from the era's life table (men also die in the stream's wars), rising and falling with
    /// modeled social market value, women on the inner side of the band and men on the outer side. The
    /// outline of the lines is the gender-separated population curve, drawn as a faint line per sex.
    /// Famous historical figures are bright lifelines among them. (The United States from 1950 is simulated
    /// in detail by the smv layer; this layer draws only US lives that end before 1950.)
    ///
    /// Level of detail: lifelines would be noise at overview scale, so the coarse tier (each line N people)
    /// fades in for the human-scale presets or once lifelines are readable on screen (<see cref="LifelineLod"/>),
    /// and the half and quarter tiers (N / 2, N / 4 people per line) add the lines between as soon as those
    /// would stand apart. Lines are additive, so every tier dims a little as denser ones join and the crowd keeps
    /// its brightness instead of blooming into a blob. The two denser meshes are built in the background after
    /// Prepare and uploaded when first needed or once the view is idle, so they never cost load time. Figures are
    /// always drawn, brighter where humans are the subject.
    /// </summary>
    public sealed class LifelinesLayer : GraphLayer
    {
        public const string FiguresPath = "Data/figures";

        const string SmvPresetId = "smv";

        /// <summary>The smv preset belongs to the United States detail: other lifelines stay in the background.</summary>
        const float SmvPresetCap = 0.5f;

        /// <summary>
        /// A human-scale preset shows the coarse tier at least this strongly, even where lifetimes are still short
        /// ticks on screen and would pile up; full strength once they are readable.
        /// </summary>
        const float PresetAlpha = 0.5f;

        /// <summary>A human-scale preset keeps its lifelines until the camera pulls back this far (x its distance).</summary>
        const float PresetHoldFrom = 1.6f, PresetHoldTo = 3f;

        const float FiguresOverviewAlpha = 0.6f;
        const float FadeSpeed = 1.5f;

        /// <summary>A tier counts as shown (the HUD's "1 line = N people", the side labels) above this alpha.</summary>
        const float ShownAlpha = 0.3f;

        // materials
        const float LineIntensity = 0.8f;
        const float RhoFade = 5f;
        const float PreloadIdleSeconds = 1.5f;

        public override int Order => 30;

        public override IEnumerable<string> RequiredTexts => new[] { FiguresPath, HumanWorld.DemographyPath };

        LifelineBuild build;
        Task denseTiers;
        LifelineLod lod;
        double[] peoplePerLine;
        string[] names;

        readonly Tier[] tiers = new Tier[LifelineMeshes.TierCount];
        Tier curves, figures;
        LabelSpec womenLabel, menLabel;
        float uploadedAt;
        int labeledProbe = -1;

        /// <summary>One mesh with an animated level-of-detail alpha; its mesh may be uploaded later.</summary>
        sealed class Tier
        {
            public string Name;
            public Material Material;
            public MeshRenderer Renderer;
            public LineMeshBuilder Pending;

            /// <summary>Level-of-detail alpha (how much the tier is shown).</summary>
            public float Alpha;

            float applied = -1f;

            /// <summary>Pushes <see cref="Alpha"/> x <paramref name="scale"/> to the material and hides an invisible mesh.</summary>
            public void Apply(float scale)
            {
                float a = Alpha * scale;
                if (Mathf.Abs(a - applied) < 0.002f) return;
                applied = a;
                GraphMaterials.SetAlpha(Material, a);
                if (Renderer != null) Renderer.enabled = a > 0.002f;
            }

            /// <summary>Forces the next <see cref="Apply"/> (after the renderer was created).</summary>
            public void Invalidate() => applied = -1f;
        }

        public override void Prepare(GraphContext ctx)
        {
            Stopwatch sw = Stopwatch.StartNew();
            HumanWorld world = ctx.Shared<HumanWorld>(HumanWorld.SharedKey);
            if (world == null || world.Humanity == null)
            {
                Debug.LogWarning("[Why] LifelinesLayer: no shared HumanWorld; lifelines are skipped");
                return;
            }

            LabelSystem labels = ctx.Labels;
            LifelineBuild b = LifelineBuild.Run(world, ctx.Text(FiguresPath), ctx.Text(HumanWorld.DemographyPath),
                spec => labels?.Add(spec));

            // the figures' exact lifelines, for the modules that follow or connect them
            ctx.Share(FigurePaths.SharedKey, b.Figures.Paths);

            // which side is which, placed at the stream nearest the camera while lifelines are shown
            womenLabel = SideLabel("women");
            menLabel = SideLabel("men");
            labels?.Add(womenLabel);
            labels?.Add(menLabel);

            build = b;
            LifelineMeshes m = b.Meshes;
            Debug.Log($"[Why] LifelinesLayer.Prepare {sw.ElapsedMilliseconds} ms ({b.Streams} streams; coarse " +
                      $"{m.TierLines[0]} lines / {m.TierPoints[0]} points; curves {m.Curves.VertexCount / 2} points; " +
                      $"{b.Figures.Drawn} figures, {b.Figures.Skipped} skipped)");

            // the denser tiers are only needed close up: build them while the rest of the graph loads
            denseTiers = Task.Run(() =>
            {
                Stopwatch dense = Stopwatch.StartNew();
                m.EmitDenseTiers();
                Debug.Log($"[Why] LifelinesLayer dense tiers {dense.ElapsedMilliseconds} ms (half +{m.TierLines[1]} " +
                          $"lines / +{m.TierPoints[1]} points, quarter +{m.TierLines[2]} / +{m.TierPoints[2]})");
            });
        }

        static LabelSpec SideLabel(string text) => new LabelSpec
        {
            Text = text,
            Priority = 10,
            SizePx = 11,
            Color = GraphStyle.TextDim,
            PixelOffset = new Vector2(4, -8),
            Hidden = true
        };

        public override void Upload(GraphContext ctx)
        {
            if (build == null) return;
            LifelineMeshes m = build.Meshes;
            curves = CreateTier("PopulationCurves", m.Curves, GraphMaterials.QueueHumans + 1, RhoFade, 0f, true);
            figures = CreateTier("FamousFigures", build.Figures.Lines, GraphMaterials.QueueHumans + 2, 0f,
                FiguresOverviewAlpha, true);
            for (int t = 0; t < tiers.Length; t++)
            {
                // the coarse tier loads with the graph; the denser ones when first needed
                tiers[t] = CreateTier("Lifelines" + t, m.Tiers[t], GraphMaterials.QueueHumans + 1, RhoFade, 0f, t == 0);
            }

            lod = new LifelineLod(m.Probes.ToArray());
            peoplePerLine = build.PeoplePerLine;
            names = build.Names;
            uploadedAt = Time.realtimeSinceStartup;
            build = null;
        }

        Tier CreateTier(string meshName, LineMeshBuilder lines, int queue, float rhoFade, float alpha, bool uploadNow)
        {
            Tier tier = new Tier
            {
                Name = meshName,
                Material = GraphMaterials.Line(Color.white, LineIntensity, queue, true, rhoFade),
                Alpha = alpha,
                Pending = lines
            };
            if (uploadNow) Realize(tier);
            tier.Apply(1f);
            return tier;
        }

        void Realize(Tier tier)
        {
            // an empty builder (no figures file, say) gets no mesh at all
            if (tier.Pending.VertexCount > 0)
            {
                tier.Renderer = AddMesh(tier.Name, tier.Pending.ToMesh(tier.Name), tier.Material);
                tier.Renderer.enabled = false;
                tier.Invalidate();
            }

            tier.Pending = null;
        }

        public override void Tick(GraphContext ctx, CameraRig rig)
        {
            if (lod == null || rig == null) return;
            lod.Update(rig);

            // the current preset (GraphRoot sets the first one without OnFocus)
            ViewPreset current = GraphRoot.Instance != null ? GraphRoot.Instance.CurrentPreset : null;
            string presetId = current != null ? current.Id : "";
            float presetDistance = current != null ? Mathf.Max(current.Distance, 0.1f) : 1f;
            float preset = PresetWeight(presetId) *
                           (1f - SmoothStep(PresetHoldFrom * presetDistance, PresetHoldTo * presetDistance, rig.Pose.Distance));
            bool smv = presetId == SmvPresetId;
            float cap = smv ? SmvPresetCap : 1f;
            float step = Time.unscaledDeltaTime * FadeSpeed;
            bool idle = Time.realtimeSinceStartup - uploadedAt > PreloadIdleSeconds && !rig.Flying &&
                        !GraphWarp.Animating && !rig.UserActive;
            bool denseReady = DenseTiersReady();
            bool uploaded = false;

            for (int t = 0; t < tiers.Length; t++)
            {
                Tier tier = tiers[t];
                float readable = lod.Readable(t);
                float target = Mathf.Min(cap, t == 0 ? Mathf.Max(preset * PresetAlpha, readable) : readable);
                bool built = t == 0 || denseReady;
                if (tier.Pending != null && built && (target > 0f || (idle && !uploaded)))
                {
                    // one deferred upload per frame at most
                    Stopwatch sw = Stopwatch.StartNew();
                    Realize(tier);
                    uploaded = true;
                    Debug.Log($"[Why] LifelinesLayer {tier.Name} uploaded in {sw.ElapsedMilliseconds} ms");
                }

                // nothing to show before the upload (or for a tier without lines)
                tier.Alpha = Mathf.MoveTowards(tier.Alpha, tier.Renderer != null ? target : 0f, step);
            }

            // additive lines: the half tier doubles the lines and the quarter tier doubles them again, so all tiers
            // dim by the square root of the growth (denser, yet only a little brighter overall)
            float lines = 1f + tiers[1].Alpha + 2f * tiers[2].Alpha;
            float density = 1f / Mathf.Sqrt(lines);
            foreach (Tier tier in tiers) tier.Apply(density);

            float near = Mathf.Max(preset, lod.Near);
            Animate(curves, Mathf.Min(cap, near), step);
            Animate(figures, Mathf.Lerp(FiguresOverviewAlpha, 1f, near), step);
            PlaceSideLabels(ctx, smv);
            PublishLod(smv);
        }

        /// <summary>True once the background build of the half and quarter tiers has finished (drops them if it failed).</summary>
        bool DenseTiersReady()
        {
            if (denseTiers == null) return true;
            if (!denseTiers.IsCompleted) return false;
            if (denseTiers.IsFaulted)
            {
                Debug.LogError($"[Why] LifelinesLayer dense tiers failed: {denseTiers.Exception?.GetBaseException()}");
                for (int t = 1; t < tiers.Length; t++) tiers[t].Pending = null;
            }

            denseTiers = null;
            return true;
        }

        static void Animate(Tier tier, float target, float step)
        {
            tier.Alpha = Mathf.MoveTowards(tier.Alpha, target, step);
            tier.Apply(1f);
        }

        /// <summary>
        /// "women" and "men" at the two edges of the population nearest the camera, while the lifelines are shown
        /// (the smv preset has its own for the United States).
        /// </summary>
        void PlaceSideLabels(GraphContext ctx, bool smvPreset)
        {
            bool show = tiers[0].Alpha >= ShownAlpha && lod.NearestIndex >= 0 && !smvPreset;
            if (show == !womenLabel.Hidden && (!show || lod.NearestIndex == labeledProbe)) return;
            womenLabel.Hidden = menLabel.Hidden = !show;
            if (show)
            {
                labeledProbe = lod.NearestIndex;
                womenLabel.Data = lod.Nearest.Inner;
                menLabel.Data = lod.Nearest.Outer;
            }

            ctx.Labels?.MarkDirty();
        }

        /// <summary>
        /// Tells the HUD how many people a line of the densest shown tier stands for in the stream nearest the
        /// camera target, while this layer's lifelines are shown (not in the smv preset, whose lines the United
        /// States layer owns). While they are hidden it clears the readout every frame, so no value outlives the
        /// lines it described; the United States layer ticks after this one and restates its own value while its
        /// lines show.
        /// </summary>
        void PublishLod(bool smvPreset)
        {
            if (smvPreset) return;
            int stream = lod.Stream;
            if (tiers[0].Alpha < ShownAlpha || stream < 0 || peoplePerLine[stream] <= 0)
            {
                if (HumansLod.PeoplePerLine != 0) HumansLod.Publish(0, "");
                return;
            }

            int densest = 0;
            for (int t = 1; t < tiers.Length; t++)
            {
                if (tiers[t].Alpha >= ShownAlpha) densest = t;
            }

            HumansLod.Publish(peoplePerLine[stream] / (1 << densest), names[stream]);
        }

        /// <summary>1 in the presets where people are the subject.</summary>
        static float PresetWeight(string id)
        {
            switch (id)
            {
                case "prehistory":
                case "civilizations":
                case "modern":
                case "present":
                case SmvPresetId:
                    return 1f;
                default:
                    return 0f;
            }
        }

        static float SmoothStep(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }
    }
}
