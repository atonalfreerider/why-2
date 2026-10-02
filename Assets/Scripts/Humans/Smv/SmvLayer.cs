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
    [GraphScenes(GraphScene.Why, GraphScene.Economy)]
    public sealed class SmvLayer : GraphLayer
    {
        public const string CivId = "united_states";

        /// <summary>People per line in the fine tier; the coarse tier keeps one line in <see cref="SmvGeometry.CoarseStride"/>.</summary>
        public const double PeoplePerLine = 100_000;

        public const double PeoplePerCoarseLine = PeoplePerLine * SmvGeometry.CoarseStride;

        const string LodContext = "United States 1950 - now";
        const string PresetId = "smv";
        const int Seed = 1950;

        /// <summary>Civilization block used when neither the human world nor the United States stream exists.</summary>
        const int FallbackCivIndex = 99;

        /// <summary>Lifeline numbers from here on belong to the older US lives drawn by the lifelines layer.</summary>
        const int MaxLines = 90_000;

        // level of detail. Visible at all once 1950 -> now spans a couple of hundred pixels along the stream;
        // the fine tier once the population's cross-section (envelope width plus value height) spreads over
        // several hundred pixels, or when the camera is deep inside the era.
        const float CoarseFromPx = 80, CoarseFullPx = 220;
        const float FineFromSpreadPx = 380, FineFullSpreadPx = 650;
        const float FineFromEraPx = 3500, FineFullEraPx = 6000;
        const float FadeSpeed = 1.8f;

        /// <summary>Material intensity of the lifelines (the vertex colors carry hue, alpha and per-point intensity).</summary>
        const float LineIntensity = 0.9f;

        /// <summary>
        /// The lines' per-point alpha is tuned for the population's cross-section spreading over this many
        /// pixels (the smv view). Seen smaller, the same lines overlap more, so alpha follows
        /// (spread / reference)^CrowdExponent, down to CrowdMin: the stream keeps about the same brightness
        /// per pixel at every zoom instead of saturating when it is small on screen.
        /// </summary>
        const float CrowdReferencePx = 450f;

        const float CrowdExponent = 0.75f;
        const float CrowdMin = 0.3f;

        /// <summary>
        /// When the director highlights the whole population (the smv:us or civ:united_states anchors), the
        /// shader multiplies thousands of overlapping additive lines by the highlight glow at once, which
        /// would burn the stream into a white slab. The lines' own intensity is lowered by up to this factor,
        /// in proportion to the share of lines highlighted, so a whole-population highlight glows about twice
        /// as bright instead of five times while a single generation still stands out.
        /// </summary>
        const float HighlightCompensation = 0.4f;

        /// <summary>Lifeline ids probed each frame to estimate the highlighted share.</summary>
        const int HighlightProbes = 16;

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
        MeshRenderer[] fineRenderers = Array.Empty<MeshRenderer>();
        MeshRenderer coarseRenderer, markerRenderer, surfaceRenderer;
        float fineAlpha, coarseAlpha, crowd = 1f;

        /// <summary>
        /// A scene's dimming of the lifelines (1 = none), multiplied into the level-of-detail alphas: the economy's land
        /// views set it each frame to the road's emphasis (0.35 while the land is open); the causality scene never sets it.
        /// </summary>
        public static float SceneAlpha = 1f;
        bool published;

        // highlight compensation (see HighlightCompensation)
        IdRange lineIds = IdRange.Empty;
        float intensityScale = 1f;
        int colorId;

        readonly List<LabelSpec> labels = new List<LabelSpec>();
        bool labelsShown = true;

        /// <summary>
        /// Cross-sections of the population along the stream from 1950 to now (data space), projected every
        /// frame to measure how large the era and its population appear on screen.
        /// </summary>
        struct EraProbe
        {
            public Vector3 Mid, Inner, Outer, Base, Top;
        }

        EraProbe[] eraProbe = Array.Empty<EraProbe>();

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

            // without the stream, a block past every civilization's (no id collides with another stream)
            int civIndex = civ?.Index ?? world?.Civs.Count ?? FallbackCivIndex;
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

            // another scene's model of the same people restyles their lines (none in the causality graph)
            ISmvLineStyle style = ctx.Shared<ISmvLineStyle>(SmvPopulation.StyleKey);
            style?.Prepare(sim);
            if (sim.People.Count > MaxLines)
            {
                Debug.LogWarning($"[Why] SmvLayer: {sim.People.Count} lines overflow the lifeline ids reserved " +
                                 $"for 1950 - now ({MaxLines}); raise PeoplePerLine");
            }

            long simMs = sw.ElapsedMilliseconds;

            SmvGeometry geo = new SmvGeometry(sim, civIndex) { Style = style };
            geo.BuildLifelines();
            geo.BuildParentLinks();
            double firstMid = data.FirstYear + 0.5;
            geo.BuildPopulationCurves(firstMid - 4, GraphIds.Civ(civIndex));

            IReadOnlyList<Generation> generations =
                world != null && world.UsGenerations.Count > 0 ? world.UsGenerations : FallbackGenerations;
            foreach (Generation g in generations) geo.BuildGenerationPlane(g.from);

            lineIds = new IdRange(GraphIds.Lifeline(civIndex, 0), GraphIds.Lifeline(civIndex, sim.People.Count - 1));
            ctx.Share(SmvPopulation.SharedKey, new SmvPopulation { Sim = sim, CivIndex = civIndex, LineIds = lineIds });
            RegisterPopulation(ctx, sim, geo, firstMid);
            RegisterGenerations(ctx, sim, geo, civIndex, generations);
            BuildEraProbe(sim, geo, firstMid);
            geometry = geo;

            Debug.Log($"[Why] SmvLayer.Prepare {sw.ElapsedMilliseconds} ms (simulation {simMs} ms): " +
                      $"{sim.People.Count} lines x {PeoplePerLine:N0} people ({geo.CoarseLines} coarse), " +
                      $"{sim.StepCount} steps from {sim.StartTime:0.#}, {geo.RawSamples} samples -> " +
                      $"{geo.FinePoints} fine / {geo.CoarsePoints} coarse points in {geo.FineParts.Count} + 1 meshes, " +
                      $"{geo.LinkCount} parent links; {sim.Marriages} marriages, {sim.Divorces} divorces, " +
                      $"{sim.BirthsWithMother} births with a mother, {sim.Immigrants} immigrants");
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

        void RegisterPopulation(GraphContext ctx, SmvSimulation sim, SmvGeometry geo, double firstMid)
        {
            int k0 = sim.StepAt(firstMid);
            IdRange all = lineIds;
            CultureInfo c = CultureInfo.InvariantCulture;
            Anchor anchor = new Anchor
            {
                Key = "smv:us",
                Label = "United States population 1950 - now",
                Blurb = $"Each line stands for {PeoplePerLine.ToString("N0", c)} people " +
                        $"({PeoplePerCoarseLine.ToString("N0", c)} when zoomed out): " +
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
            eraProbe = new EraProbe[n];
            for (int i = 0; i < n; i++)
            {
                double year = firstMid + (end - firstMid) * i / (n - 1);
                int k = sim.StepAt(year);
                float u = geo.U(year), c = sim.Center[k], e = sim.Envelope[k];
                float y = GraphStyle.HumansY;
                eraProbe[i] = new EraProbe
                {
                    Mid = new Vector3(u, y + GraphStyle.SmvHeight * 0.4f, c),
                    Inner = new Vector3(u, y, c - e),
                    Outer = new Vector3(u, y, c + e),
                    Base = new Vector3(u, y, c),
                    Top = new Vector3(u, y + GraphStyle.SmvHeight, c)
                };
            }
        }

        public override void Upload(GraphContext ctx)
        {
            if (geometry == null) return;
            int queue = GraphMaterials.QueueHumans;
            colorId = Shader.PropertyToID("_Color");
            fineMat = GraphMaterials.Line(Color.white, LineIntensity, queue + 2);
            coarseMat = GraphMaterials.Line(Color.white, LineIntensity, queue + 2);
            markerMat = GraphMaterials.Line(Color.white, 1f, queue + 1);
            surfaceMat = GraphMaterials.Surface(Color.white, 1f, queue + 1);
            surfaceMat.SetFloat("_EdgeSoft", 0.2f);

            // the fine tier: one mesh per worker that built it, plus the parent links (all one material)
            IReadOnlyList<LineMeshBuilder> parts = geometry.FineParts;
            fineRenderers = new MeshRenderer[parts.Count + 1];
            for (int i = 0; i < parts.Count; i++)
            {
                string name = "SmvLifelines" + i;
                fineRenderers[i] = AddMesh(name, parts[i].ToMesh(name), fineMat);
            }

            fineRenderers[parts.Count] = AddMesh("SmvParentLinks", geometry.Links.ToMesh("SmvParentLinks"), fineMat);
            coarseRenderer = AddMesh("SmvLifelinesCoarse", geometry.Coarse.ToMesh("SmvLifelinesCoarse"), coarseMat);
            markerRenderer = AddMesh("SmvMarkers", geometry.Markers.ToMesh("SmvMarkers"), markerMat);
            surfaceRenderer = AddMesh("SmvSurfaces", geometry.Surfaces.ToMesh("SmvSurfaces"), surfaceMat);
            geometry = null;

            Apply(fineMat, fineRenderers, 0);
            Apply(coarseMat, coarseRenderer, 0);
            Apply(markerMat, markerRenderer, 0);
            Apply(surfaceMat, surfaceRenderer, 0);
            ShowLabels(ctx, false);
        }

        /// <summary>
        /// Level of detail by what is on screen: nothing while 1950 - now is a sliver (the whole clock, the
        /// civilizations), the coarse tier (1 line = 1,000,000 people) once it spans a couple of hundred
        /// pixels, the fine tier (1 line = 100,000) in the smv view or once the camera is close enough that
        /// the population spreads over several hundred pixels - lines per pixel stay readable.
        /// </summary>
        public override void Tick(GraphContext ctx, CameraRig rig)
        {
            if (fineMat == null) return;
            Measure(rig, GraphWarp.Current, out float eraPx, out float spreadPx, out float centerPx);

            GraphRoot root = GraphRoot.Instance;
            bool preset = root != null && root.CurrentPreset != null &&
                          (root.CurrentPreset.Id == PresetId || root.CurrentPreset.PopulationDetail);
            float visible = SmoothStep(CoarseFromPx, CoarseFullPx, eraPx);
            float close = Mathf.Max(SmoothStep(FineFromSpreadPx, FineFullSpreadPx, spreadPx),
                SmoothStep(FineFromEraPx, FineFullEraPx, eraPx));
            float fineTarget = visible * (preset ? 1f : close);
            float coarseTarget = visible * (1 - fineTarget);

            float dt = Time.unscaledDeltaTime;
            float step = dt * FadeSpeed;
            fineAlpha = Mathf.MoveTowards(fineAlpha, fineTarget, step);
            coarseAlpha = Mathf.MoveTowards(coarseAlpha, coarseTarget, step);
            float any = Mathf.Max(fineAlpha, coarseAlpha);

            // no spread measured (the era's probes are all off screen, e.g. very close up): nothing to thin out
            float crowdTarget = spreadPx > 0
                ? Mathf.Clamp(Mathf.Pow(spreadPx / CrowdReferencePx, CrowdExponent), CrowdMin, 1f)
                : 1f;
            crowd = Mathf.Lerp(crowd, crowdTarget, 1f - Mathf.Exp(-6f * dt));

            float scene = SceneAlpha;
            Apply(fineMat, fineRenderers, fineAlpha * crowd * scene);
            Apply(coarseMat, coarseRenderer, coarseAlpha * crowd * scene);
            Apply(markerMat, markerRenderer, any * scene);
            Apply(surfaceMat, surfaceRenderer, any * scene);
            if (any > 0.3f != labelsShown) ShowLabels(ctx, any > 0.3f);
            if (any > 0.003f) CompensateHighlight(dt);

            // the coarse tier shares its views with the other streams' lifelines: claim the readout only while
            // the view is centered on this population
            bool centered = centerPx < 0.6f * spreadPx + 0.1f * Screen.height;
            if (fineAlpha > 0.5f) Publish(PeoplePerLine);
            else if (coarseAlpha > 0.5f && centered) Publish(PeoplePerCoarseLine);
            else if (published)
            {
                // hand the readout back (unless another lifeline layer has taken it over meanwhile)
                if (HumansLod.Context == LodContext) HumansLod.Publish(0, "");
                published = false;
            }
        }

        void Publish(double peoplePerLine)
        {
            HumansLod.Publish(peoplePerLine, LodContext);
            published = true;
        }

        /// <summary>
        /// Eases the lines' intensity down while a large share of them is highlighted (see
        /// <see cref="HighlightCompensation"/>); at the pace of the highlighter's own fade.
        /// </summary>
        void CompensateHighlight(float dt)
        {
            float share = 0;
            if (Highlighter.HasHighlight && !lineIds.IsEmpty)
            {
                int hits = 0;
                long span = (long)lineIds.Max - lineIds.Min;
                for (int i = 0; i < HighlightProbes; i++)
                {
                    int id = lineIds.Min + (int)(span * (2 * i + 1) / (2 * HighlightProbes));
                    if (Highlighter.IsHighlighted(IdRange.Single(id))) hits++;
                }

                share = hits / (float)HighlightProbes;
            }

            float target = Mathf.Lerp(1f, HighlightCompensation, share);
            if (Mathf.Approximately(target, intensityScale)) return;
            intensityScale = Mathf.MoveTowards(intensityScale, target, dt * 1.5f);
            float c = LineIntensity * intensityScale;
            Color color = new Color(c, c, c, 1f);
            fineMat.SetColor(colorId, color);
            coarseMat.SetColor(colorId, color);
        }

        /// <summary>
        /// On-screen size of the era: <paramref name="eraPx"/> is the length of the stream from 1950 to now
        /// (only the parts in front of the camera and overlapping the screen), <paramref name="spreadPx"/>
        /// the largest cross-section of the population near the screen (envelope width plus value height),
        /// <paramref name="centerPx"/> the distance from the screen center to the stream's middle line.
        /// The sizes are 0 (and the distance infinite) when the era is outside the lens window.
        /// </summary>
        void Measure(CameraRig rig, WarpState warp, out float eraPx, out float spreadPx, out float centerPx)
        {
            eraPx = spreadPx = 0;
            centerPx = float.PositiveInfinity;
            if (eraProbe.Length < 2 || rig == null || rig.Cam == null) return;
            if (GraphWarp.FocusFade(eraProbe[eraProbe.Length / 2].Mid.x, warp) < 0.3f) return;

            Camera cam = rig.Cam;
            Rect screen = new Rect(0, 0, Screen.width, Screen.height);
            Rect near = new Rect(-0.25f * screen.width, -0.25f * screen.height, 1.5f * screen.width, 1.5f * screen.height);
            Vector2 middle = screen.center;
            Vector3 prev = Vector3.zero;
            for (int i = 0; i < eraProbe.Length; i++)
            {
                EraProbe probe = eraProbe[i];
                Vector3 s = cam.WorldToScreenPoint(GraphWarp.ToWorld(probe.Mid));
                if (i > 0 && prev.z > 0 && s.z > 0)
                {
                    Rect box = Rect.MinMaxRect(Mathf.Min(prev.x, s.x), Mathf.Min(prev.y, s.y),
                        Mathf.Max(prev.x, s.x), Mathf.Max(prev.y, s.y));
                    if (box.Overlaps(screen)) eraPx += Vector2.Distance(prev, s);
                    centerPx = Mathf.Min(centerPx, DistanceToSegment(middle, prev, s));
                }

                if (s.z > 0 && near.Contains(s))
                {
                    float across = ScreenDistance(cam, probe.Inner, probe.Outer) + ScreenDistance(cam, probe.Base, probe.Top);
                    spreadPx = Mathf.Max(spreadPx, across);
                }

                prev = s;
            }
        }

        static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }

        static float ScreenDistance(Camera cam, Vector3 a, Vector3 b)
        {
            Vector3 sa = cam.WorldToScreenPoint(GraphWarp.ToWorld(a));
            Vector3 sb = cam.WorldToScreenPoint(GraphWarp.ToWorld(b));
            return sa.z > 0 && sb.z > 0 ? Vector2.Distance(sa, sb) : 0;
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

        static void Apply(Material m, MeshRenderer[] renderers, float alpha)
        {
            if (m == null) return;
            bool visible = alpha > 0.003f;
            foreach (MeshRenderer r in renderers)
            {
                if (r != null && r.enabled != visible) r.enabled = visible;
            }

            if (visible) GraphMaterials.SetAlpha(m, alpha);
        }

        static float SmoothStep(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3 - 2 * t);
        }
    }
}
