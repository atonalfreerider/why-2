using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Why.Humans.Smv
{
    /// <summary>
    /// BLUE layer detail: the United States population from 1950 to now as individual lifelines inside the
    /// United States stream, integrated from the smv ("social market value") project.
    ///
    /// Every line stands for many people (100,000 close up, 1,000,000 zoomed out). Women run on the inner
    /// side of the stream and men on the outer side, so the envelope of all lines is a gender-separated
    /// population curve. From age 18 a line's height is its modeled social market value and its distance
    /// from the center its value rank; married couples are drawn together, children start on their mother's
    /// line. People alive in 1950 are backfilled from birth.
    ///
    /// The simulation and all geometry run on a worker thread in a few hundred milliseconds (the original
    /// took minutes: one GameObject per segment, LINQ over the population every step).
    /// </summary>
    public sealed class SmvLayer : GraphLayer
    {
        public const string CivId = "united_states";

        /// <summary>People per line in the fine tier; the coarse tier keeps one line in <see cref="SmvGeometry.CoarseStride"/>.</summary>
        public const double PeoplePerLine = 100_000;

        public const double PeoplePerCoarseLine = PeoplePerLine * SmvGeometry.CoarseStride;

        const string LodContext = "United States 1950 - now";
        const string PresetId = "smv";
        const int Seed = 1950;

        /// <summary>Civilization block used when the United States stream is missing from the human world.</summary>
        const int FallbackCivIndex = 99;

        // level of detail: on-screen length (px) of 1950 -> now along the stream
        const float CoarseFromPx = 80, CoarseFullPx = 220;
        const float FineFromPx = 950, FineFullPx = 1600;
        const float FadeSpeed = 1.8f;

        static readonly Generation[] FallbackGenerations =
        {
            new Generation { id = "greatest", name = "Greatest Generation", from = 1901, to = 1927 },
            new Generation { id = "silent", name = "Silent Generation", from = 1928, to = 1945 },
            new Generation { id = "boomers", name = "Baby Boomers", from = 1946, to = 1964 },
            new Generation { id = "genx", name = "Generation X", from = 1965, to = 1980 },
            new Generation { id = "millennials", name = "Millennials", from = 1981, to = 1996 },
            new Generation { id = "genz", name = "Generation Z", from = 1997, to = 2012 },
            new Generation { id = "alpha", name = "Generation Alpha", from = 2013, to = 2024 }
        };

        public override int Order => 30;

        public override IEnumerable<string> RequiredTexts => new[]
        {
            SmvData.MenPath, SmvData.WomenPath, SmvData.MarriagePath, SmvData.DivorcePath,
            SmvData.SingleParentPath, SmvData.PartnersPath
        };

        SmvGeometry geometry;
        Material fineMat, coarseMat, markerMat, surfaceMat;
        MeshRenderer fineRenderer, coarseRenderer, markerRenderer, surfaceRenderer;
        float fineAlpha, coarseAlpha;

        readonly List<LabelSpec> labels = new List<LabelSpec>();
        bool labelsShown = true;

        /// <summary>Data-space points along the stream from 1950 to now, projected to measure the on-screen era.</summary>
        Vector3[] eraProbe = Array.Empty<Vector3>();

        // band lookup state (worker thread)
        HumanWorld world;
        Civ civ;
        float lastCenter = 0.45f, lastHalf = 0.2f;

        public override void Prepare(GraphContext ctx)
        {
            Stopwatch sw = Stopwatch.StartNew();
            world = ctx.Shared<HumanWorld>(HumanWorld.SharedKey);
            civ = null;
            if (world == null || !world.ById.TryGetValue(CivId, out civ))
            {
                Debug.LogWarning($"[Why] SmvLayer: '{CivId}' stream not found; drawing in a fallback band");
            }

            int civIndex = civ?.Index ?? FallbackCivIndex;
            SmvData data = SmvData.Parse(ctx.Text(SmvData.MenPath), ctx.Text(SmvData.WomenPath),
                ctx.Text(SmvData.MarriagePath), ctx.Text(SmvData.DivorcePath), ctx.Text(SmvData.SingleParentPath),
                ctx.Text(SmvData.PartnersPath), (int)Math.Floor(ctx.NowYear - 0.5));
            if (!data.IsValid)
            {
                Debug.LogWarning("[Why] SmvLayer: UN population pyramids missing, layer skipped");
                return;
            }

            SmvSimulation sim = new SmvSimulation(data, BandAt, PeoplePerLine, ctx.NowYear, Seed);
            sim.Run();
            if (sim.People.Count == 0) return;
            long simMs = sw.ElapsedMilliseconds;

            SmvGeometry geo = new SmvGeometry(sim, civIndex);
            geo.BuildLifelines();
            geo.BuildParentLinks();
            double firstMid = data.FirstYear + 0.5;
            geo.BuildPopulationCurves(firstMid - 4, GraphIds.Civ(civIndex));

            IReadOnlyList<Generation> generations =
                world != null && world.UsGenerations.Count > 0 ? world.UsGenerations : FallbackGenerations;
            foreach (Generation g in generations) geo.BuildGenerationPlane(g.from);

            RegisterPopulation(ctx, sim, geo, civIndex, firstMid);
            RegisterGenerations(ctx, sim, geo, civIndex, generations);
            BuildEraProbe(sim, geo, firstMid);
            geometry = geo;

            Debug.Log($"[Why] SmvLayer.Prepare {sw.ElapsedMilliseconds} ms (simulation {simMs} ms): " +
                      $"{sim.People.Count} lines x {PeoplePerLine:N0} people ({geo.CoarseLines} coarse), " +
                      $"{sim.StepCount} steps from {sim.StartTime:0.#}, {geo.RawSamples} samples -> " +
                      $"{geo.FinePoints} fine / {geo.CoarsePoints} coarse points; {sim.Marriages} marriages, " +
                      $"{sim.Divorces} divorces, {sim.BirthsWithMother} births with a mother, {sim.Immigrants} immigrants");
        }

        /// <summary>The United States band at a year (holds the last known band where it is undefined).</summary>
        void BandAt(double year, out float center, out float halfWidth)
        {
            if (civ != null && world.TryBand(civ, year, out float lo, out float hi) && hi > lo)
            {
                lastCenter = 0.5f * (lo + hi);
                lastHalf = 0.5f * (hi - lo);
            }

            center = lastCenter;
            halfWidth = lastHalf;
        }

        void RegisterPopulation(GraphContext ctx, SmvSimulation sim, SmvGeometry geo, int civIndex, double firstMid)
        {
            int k0 = sim.StepAt(firstMid);
            IdRange all = new IdRange(GraphIds.Lifeline(civIndex, 0), GraphIds.Lifeline(civIndex, sim.People.Count - 1));
            Anchor anchor = new Anchor
            {
                Key = "smv:us",
                Label = "United States population 1950 - now",
                Blurb = $"Each line stands for {PeoplePerLine:N0} people ({PeoplePerCoarseLine:N0} when zoomed out): " +
                        "men on the outer side, women on the inner side, height = modeled social market value. " +
                        "Children's lines start on their mothers' lines, married couples run parallel toward the " +
                        "middle. Simulated from UN age pyramids with US marriage, divorce and birth statistics.",
                Level = GraphLevel.Humans,
                YearsAgo = ctx.NowYear - firstMid,
                EndYearsAgo = 0,
                Y = GraphStyle.HumansY + GraphStyle.SmvHeight * 0.5f,
                Rho = sim.Center[k0],
                Ids = all,
                Tier = 1
            };
            Anchors.Register(anchor);

            AddLabel(ctx, new LabelSpec
            {
                Text = "United States 1950 - now",
                Data = new Vector3(geo.U(firstMid), GraphStyle.HumansY + GraphStyle.SmvHeight + 0.05f, sim.Center[k0]),
                Priority = 26,
                SizePx = 14,
                Color = GraphStyle.Text,
                PixelOffset = new Vector2(0, 8),
                AnchorKey = anchor.Key,
                Ids = all
            });

            // which side is which
            double sideYear = firstMid + 12;
            int ks = sim.StepAt(sideYear);
            float us = geo.U(sideYear);
            AddLabel(ctx, new LabelSpec
            {
                Text = "women",
                Data = new Vector3(us, GraphStyle.HumansY, sim.Center[ks] - sim.Envelope[ks]),
                Priority = 9,
                SizePx = 11,
                Color = GraphStyle.TextDim,
                PixelOffset = new Vector2(4, -8),
                AnchorKey = anchor.Key
            });
            AddLabel(ctx, new LabelSpec
            {
                Text = "men",
                Data = new Vector3(us, GraphStyle.HumansY, sim.Center[ks] + sim.Envelope[ks]),
                Priority = 9,
                SizePx = 11,
                Color = GraphStyle.TextDim,
                PixelOffset = new Vector2(4, -8),
                AnchorKey = anchor.Key
            });
        }

        void RegisterGenerations(GraphContext ctx, SmvSimulation sim, SmvGeometry geo, int civIndex,
            IReadOnlyList<Generation> generations)
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            foreach (Generation g in generations)
            {
                if (g == null || string.IsNullOrEmpty(g.id)) continue;
                int first = sim.FirstBornAtOrAfter(g.from);
                int last = sim.FirstBornAtOrAfter(g.to + 1) - 1;
                int lines = Math.Max(0, last - first + 1);
                IdRange ids = lines > 0
                    ? new IdRange(GraphIds.Lifeline(civIndex, first), GraphIds.Lifeline(civIndex, last))
                    : IdRange.Empty;
                int k = sim.StepAt(g.from);
                double millions = lines * PeoplePerLine / 1e6;
                string span = g.from.ToString("0", c) + "-" + g.to.ToString("0", c);
                Anchor anchor = new Anchor
                {
                    Key = "gen:" + g.id,
                    Label = g.name ?? g.id,
                    Blurb = $"Born {span}: {lines} lines here, about {millions.ToString("0", c)} million people " +
                            "who lived in the United States from 1950 on (the older generations: those still alive in 1950).",
                    Level = GraphLevel.Humans,
                    YearsAgo = ctx.NowYear - g.from,
                    EndYearsAgo = Math.Max(0, ctx.NowYear - (g.to + 1)),
                    Y = GraphStyle.HumansY + GraphStyle.SmvHeight * 0.1f,
                    Rho = sim.Center[k],
                    Ids = ids,
                    Tier = 2
                };
                Anchors.Register(anchor);

                AddLabel(ctx, new LabelSpec
                {
                    Text = anchor.Label,
                    Data = new Vector3(geo.U(g.from), GraphStyle.HumansY + GraphStyle.SmvHeight + 0.02f, sim.Center[k]),
                    Priority = 12,
                    SizePx = 12,
                    Color = GraphStyle.TextDim,
                    PixelOffset = new Vector2(4, 8),
                    AnchorKey = anchor.Key,
                    Ids = ids
                });
            }
        }

        void AddLabel(GraphContext ctx, LabelSpec spec)
        {
            labels.Add(spec);
            ctx.Labels.Add(spec);
        }

        void BuildEraProbe(SmvSimulation sim, SmvGeometry geo, double firstMid)
        {
            const int n = 12;
            double end = sim.NowYear - 1;
            eraProbe = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                double year = firstMid + (end - firstMid) * i / (n - 1);
                int k = sim.StepAt(year);
                eraProbe[i] = new Vector3(geo.U(year), GraphStyle.HumansY + GraphStyle.SmvHeight * 0.4f, sim.Center[k]);
            }
        }

        public override void Upload(GraphContext ctx)
        {
            if (geometry == null) return;
            int queue = GraphMaterials.QueueHumans;
            fineMat = GraphMaterials.Line(Color.white, 0.9f, queue + 2);
            coarseMat = GraphMaterials.Line(Color.white, 0.9f, queue + 2);
            markerMat = GraphMaterials.Line(Color.white, 1f, queue + 1);
            surfaceMat = GraphMaterials.Surface(Color.white, 1f, queue + 1);
            surfaceMat.SetFloat("_EdgeSoft", 0.2f);

            fineRenderer = AddMesh("SmvLifelines", geometry.Fine.ToMesh("SmvLifelines"), fineMat);
            coarseRenderer = AddMesh("SmvLifelinesCoarse", geometry.Coarse.ToMesh("SmvLifelinesCoarse"), coarseMat);
            markerRenderer = AddMesh("SmvMarkers", geometry.Markers.ToMesh("SmvMarkers"), markerMat);
            surfaceRenderer = AddMesh("SmvSurfaces", geometry.Surfaces.ToMesh("SmvSurfaces"), surfaceMat);
            geometry = null;

            Apply(fineMat, fineRenderer, 0);
            Apply(coarseMat, coarseRenderer, 0);
            Apply(markerMat, markerRenderer, 0);
            Apply(surfaceMat, surfaceRenderer, 0);
            ShowLabels(ctx, false);
        }

        /// <summary>
        /// Level of detail by what is on screen: nothing while 1950 - now is a sliver (the whole clock, the
        /// civilizations), the coarse tier once it spans a couple of hundred pixels, the fine tier in the smv
        /// view or when the camera is close to the stream.
        /// </summary>
        public override void Tick(GraphContext ctx, CameraRig rig)
        {
            if (fineMat == null) return;
            float px = EraPixels(rig, GraphWarp.Current);

            GraphRoot root = GraphRoot.Instance;
            bool preset = root != null && root.CurrentPreset != null && root.CurrentPreset.Id == PresetId;
            float fineTarget = Mathf.Max(preset ? SmoothStep(CoarseFullPx, 2 * CoarseFullPx, px) : 0,
                SmoothStep(FineFromPx, FineFullPx, px));
            float coarseTarget = SmoothStep(CoarseFromPx, CoarseFullPx, px) * (1 - fineTarget);

            float step = Time.unscaledDeltaTime * FadeSpeed;
            fineAlpha = Mathf.MoveTowards(fineAlpha, fineTarget, step);
            coarseAlpha = Mathf.MoveTowards(coarseAlpha, coarseTarget, step);
            float any = Mathf.Max(fineAlpha, coarseAlpha);

            Apply(fineMat, fineRenderer, fineAlpha);
            Apply(coarseMat, coarseRenderer, coarseAlpha);
            Apply(markerMat, markerRenderer, any);
            Apply(surfaceMat, surfaceRenderer, any);
            if (any > 0.3f != labelsShown) ShowLabels(ctx, any > 0.3f);

            if (fineAlpha > 0.5f) HumansLod.Publish(PeoplePerLine, LodContext);
            else if (coarseAlpha > 0.5f) HumansLod.Publish(PeoplePerCoarseLine, LodContext);
        }

        /// <summary>
        /// Screen length (px) of the stream from 1950 to now, counting only the parts in front of the camera
        /// and overlapping the screen; 0 when the era is out of the focused window.
        /// </summary>
        float EraPixels(CameraRig rig, WarpState warp)
        {
            if (eraProbe.Length < 2 || rig == null || rig.Cam == null) return 0;
            if (GraphWarp.FocusFade(eraProbe[eraProbe.Length / 2].x, warp) < 0.3f) return 0;

            Camera cam = rig.Cam;
            Rect screen = new Rect(0, 0, Screen.width, Screen.height);
            float total = 0;
            Vector3 prev = cam.WorldToScreenPoint(GraphWarp.ToWorld(eraProbe[0]));
            for (int i = 1; i < eraProbe.Length; i++)
            {
                Vector3 s = cam.WorldToScreenPoint(GraphWarp.ToWorld(eraProbe[i]));
                if (prev.z > 0 && s.z > 0)
                {
                    Rect box = Rect.MinMaxRect(Mathf.Min(prev.x, s.x), Mathf.Min(prev.y, s.y),
                        Mathf.Max(prev.x, s.x), Mathf.Max(prev.y, s.y));
                    if (box.Overlaps(screen)) total += Vector2.Distance(prev, s);
                }

                prev = s;
            }

            return total;
        }

        void ShowLabels(GraphContext ctx, bool show)
        {
            labelsShown = show;
            foreach (LabelSpec s in labels) s.Hidden = !show;
            if (ctx.Labels != null) ctx.Labels.MarkDirty();
        }

        static void Apply(Material m, MeshRenderer r, float alpha)
        {
            if (m == null || r == null) return;
            bool visible = alpha > 0.003f;
            if (r.enabled != visible) r.enabled = visible;
            if (visible) GraphMaterials.SetAlpha(m, alpha);
        }

        static float SmoothStep(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3 - 2 * t);
        }
    }
}
