using System;
using System.Collections.Generic;
using UnityEngine;

namespace Why.Humans.Smv
{
    /// <summary>
    /// Turns a finished <see cref="SmvSimulation"/> into a few big meshes in data space (worker thread):
    /// <list type="bullet">
    /// <item><see cref="Fine"/> - every lifeline (1 line = the simulation's people per line) plus the thin
    /// links from fathers to their newborns;</item>
    /// <item><see cref="Coarse"/> - every <see cref="CoarseStride"/>-th person of each sex, drawn bolder, for
    /// the zoomed-out view (the same lines, so the two tiers crossfade without anything jumping);</item>
    /// <item><see cref="Markers"/> - the gender-separated population curves and the generation planes'
    /// outlines; <see cref="Surfaces"/> - their faint fills.</item>
    /// </list>
    /// Samples are thinned where a line barely changes, so quarter-year simulation steps cost only where
    /// something happens (a marriage, a child, a rank change).
    /// </summary>
    public sealed class SmvGeometry
    {
        /// <summary>The coarse tier keeps one person in this many (per sex, in birth order).</summary>
        public const int CoarseStride = 10;

        // thinning tolerances in data space, and the longest kept gap in samples (6 years)
        const float TolY = 0.0012f;
        const float TolRho = 0.0015f;
        const int MaxGap = 24;

        // fine tier: dense, so each line is faint and density reads as brightness
        const float FineAdultAlpha = 0.16f;
        const float FineChildAlpha = 0.06f;
        const float FineAdultPx = 1.0f;
        const float FineChildPx = 0.6f;

        // coarse tier: ten times fewer lines, each one bolder (as bold as the other streams' lifelines)
        const float CoarseAdultAlpha = 0.55f;
        const float CoarseChildAlpha = 0.22f;
        const float CoarseAdultPx = 1.4f;
        const float CoarseChildPx = 0.9f;

        // a little world width so lines thicken when the camera comes very close
        const float AdultWidthWorld = 0.0004f;
        const float ChildWidthWorld = 0.00025f;

        // Lines crowd near the band center (high value ranks, married couples, children): per-line alpha
        // there drops to CoreAlpha and rises to EdgeAlpha where lines spread out, so the core glows without
        // burning out and the sparse outskirts still read as individual lives.
        const float CoreAlpha = 0.5f;
        const float EdgeAlpha = 1.25f;
        const float CoreOffset = 0.04f;
        const float SpreadOffset = 0.45f;

        const float ChildIntensity = 0.6f;
        const float LinkAlpha = 0.16f;
        const float LinkPx = 0.8f;

        public readonly LineMeshBuilder Fine;
        public readonly LineMeshBuilder Coarse;
        public readonly LineMeshBuilder Markers = new LineMeshBuilder(4096);
        public readonly SurfaceMeshBuilder Surfaces = new SurfaceMeshBuilder();

        /// <summary>Samples before thinning, and points kept per tier (for the timing log).</summary>
        public int RawSamples { get; private set; }

        public int FinePoints { get; private set; }
        public int CoarsePoints { get; private set; }
        public int CoarseLines { get; private set; }

        readonly SmvSimulation sim;
        readonly int civIndex;
        readonly float[] stepU;

        // per-line scratch buffers (reused)
        double[] bt = new double[512];
        float[] bu = new float[512], by = new float[512], br = new float[512];
        int[] bk = new int[512];
        bool[] keep = new bool[512];
        readonly List<LinePoint> finePts = new List<LinePoint>(512);
        readonly List<LinePoint> coarsePts = new List<LinePoint>(512);

        public SmvGeometry(SmvSimulation sim, int civIndex)
        {
            this.sim = sim;
            this.civIndex = civIndex;
            // thinning keeps about one sample in seven (measured); the coarse tier holds a tenth of the lines
            Fine = new LineMeshBuilder(Math.Max(1024, sim.SampleY.Length / 6));
            Coarse = new LineMeshBuilder(Math.Max(1024, sim.SampleY.Length / 60));
            stepU = new float[sim.StepCount];
            for (int k = 0; k < sim.StepCount; k++) stepU[k] = U(sim.TimeOf(k));
        }

        /// <summary>Clock arc of a calendar year (never below the "now" arc).</summary>
        public float U(double year) => Mathf.Max(DeepTime.Arc(sim.NowYear - year), DeepTime.NowArc);

        /// <summary>A person's lifeline id: civilization block, lifeline range, birth order.</summary>
        public int Id(SmvPerson p) => GraphIds.Lifeline(civIndex, p.Index);

        public static bool InCoarseTier(SmvPerson p) => p.SexIndex % CoarseStride == 0;

        // ------------------------------------------------------------------ lifelines

        /// <summary>Every lifeline into <see cref="Fine"/>, the coarse subset also into <see cref="Coarse"/>.</summary>
        public void BuildLifelines()
        {
            foreach (SmvPerson p in sim.People)
            {
                int n = p.SampleCount;
                if (n == 0) continue;
                int m = Gather(p, n);
                RawSamples += n;
                Thin(m);

                bool coarse = InCoarseTier(p);
                Color color = p.Male ? GraphStyle.HumansMale : GraphStyle.HumansFemale;
                finePts.Clear();
                coarsePts.Clear();
                for (int j = 0; j < m; j++)
                {
                    if (!keep[j]) continue;
                    Vector3 data = new Vector3(bu[j], by[j], br[j]);
                    bool child = bt[j] - p.Birth < SmvModel.AdultAge;
                    float lift = Mathf.Clamp01((by[j] - GraphStyle.HumansY) / GraphStyle.SmvHeight);
                    float intensity = child ? ChildIntensity : 0.65f + 0.7f * lift;
                    int k = bk[j];
                    float offset = Mathf.Abs(br[j] - sim.Center[k]) / Mathf.Max(sim.Envelope[k], 1e-4f);
                    float spread = Mathf.Lerp(CoreAlpha, EdgeAlpha, Mathf.SmoothStep(0, 1,
                        (offset - CoreOffset) / (SpreadOffset - CoreOffset)));

                    float widthWorld = child ? ChildWidthWorld : AdultWidthWorld;
                    finePts.Add(new LinePoint(data, Tint(color, spread * (child ? FineChildAlpha : FineAdultAlpha)),
                        child ? FineChildPx : FineAdultPx, widthWorld, intensity));
                    if (coarse)
                    {
                        coarsePts.Add(new LinePoint(data,
                            Tint(color, spread * (child ? CoarseChildAlpha : CoarseAdultAlpha)),
                            child ? CoarseChildPx : CoarseAdultPx, widthWorld, intensity));
                    }
                }

                Fine.AddPolyline(finePts, Id(p));
                FinePoints += finePts.Count;
                if (coarse)
                {
                    Coarse.AddPolyline(coarsePts, Id(p));
                    CoarsePoints += coarsePts.Count;
                    CoarseLines++;
                }
            }
        }

        /// <summary>
        /// Fills the scratch buffers with a line's points: its start (birth or arrival), one sample per
        /// step, and its end (death, or now). Returns the point count.
        /// </summary>
        int Gather(SmvPerson p, int n)
        {
            int m = n + 2;
            if (bt.Length < m)
            {
                int size = m * 2;
                bt = new double[size];
                bu = new float[size];
                by = new float[size];
                br = new float[size];
                bk = new int[size];
                keep = new bool[size];
            }

            bt[0] = p.Enter;
            bu[0] = U(p.Enter);
            by[0] = p.StartY;
            br[0] = p.StartRho;
            bk[0] = p.FirstStep;
            for (int i = 0; i < n; i++)
            {
                int k = p.FirstStep + i;
                bt[i + 1] = sim.TimeOf(k);
                bu[i + 1] = stepU[k];
                by[i + 1] = sim.SampleY[p.SampleOffset + i];
                br[i + 1] = sim.SampleRho[p.SampleOffset + i];
                bk[i + 1] = k;
            }

            double end = Math.Min(p.Death, sim.NowYear);
            bt[m - 1] = end;
            bu[m - 1] = U(end);
            by[m - 1] = by[m - 2];
            br[m - 1] = br[m - 2];
            bk[m - 1] = p.LastStep;
            if (end - bt[m - 2] < 1e-6) m--;
            if (bt[1] - bt[0] < 1e-6)
            {
                // the first sample falls exactly on the start: drop the duplicate
                Array.Copy(bt, 1, bt, 0, m - 1);
                Array.Copy(bu, 1, bu, 0, m - 1);
                Array.Copy(by, 1, by, 0, m - 1);
                Array.Copy(br, 1, br, 0, m - 1);
                Array.Copy(bk, 1, bk, 0, m - 1);
                m--;
            }

            return m;
        }

        /// <summary>
        /// Keeps only the points a straight segment cannot stand in for. Each segment from the last kept
        /// point (the anchor) is extended while every skipped point stays within the tolerance of it; the
        /// allowed slopes form a "sleeve" that narrows with each skipped point, so the test is O(1) per
        /// point (exact for the vertical-distance criterion) and the whole pass is linear.
        /// </summary>
        void Thin(int m)
        {
            for (int j = 0; j < m; j++) keep[j] = false;
            keep[0] = true;
            keep[m - 1] = true;
            int a = 0;
            float loY = float.NegativeInfinity, hiY = float.PositiveInfinity;
            float loR = float.NegativeInfinity, hiR = float.PositiveInfinity;
            for (int i = 1; i < m; i++)
            {
                double dt = bt[i] - bt[a];
                if (dt <= 1e-9) continue;
                float inv = (float)(1.0 / dt);
                float dy = by[i] - by[a], dr = br[i] - br[a];
                float sy = dy * inv, sr = dr * inv;
                if (i - a > MaxGap || sy < loY || sy > hiY || sr < loR || sr > hiR)
                {
                    // the previous point becomes the anchor; the segment to its neighbor always fits
                    a = i - 1;
                    keep[a] = true;
                    loY = loR = float.NegativeInfinity;
                    hiY = hiR = float.PositiveInfinity;
                    dt = bt[i] - bt[a];
                    if (dt <= 1e-9) continue;
                    inv = (float)(1.0 / dt);
                    dy = by[i] - by[a];
                    dr = br[i] - br[a];
                }

                // later segments from the anchor must pass within the tolerance of point i
                loY = Math.Max(loY, (dy - TolY) * inv);
                hiY = Math.Min(hiY, (dy + TolY) * inv);
                loR = Math.Max(loR, (dr - TolRho) * inv);
                hiR = Math.Min(hiR, (dr + TolRho) * inv);
            }
        }

        // ------------------------------------------------------------------ cause and effect

        /// <summary>
        /// A faint link from each father's line to his newborn's line (the child's own line already starts
        /// on the mother's line): two parents, one new life.
        /// </summary>
        public void BuildParentLinks()
        {
            Color32 tint = Tint(GraphStyle.HumansMale, LinkAlpha);
            foreach (SmvPerson c in sim.People)
            {
                if (c.Father < 0 || c.BirthStep < 0 || c.SampleCount == 0) continue;
                SmvPerson f = sim.People[c.Father];
                int kf = Math.Max(f.FirstStep, Math.Min(f.LastStep, c.BirthStep - 1));
                int kc = Math.Min(c.LastStep, c.BirthStep + 2);
                if (f.SampleCount == 0 || kc < c.FirstStep) continue;
                Vector3 from = new Vector3(stepU[kf], sim.SampleY[f.SampleOffset + kf - f.FirstStep],
                    sim.SampleRho[f.SampleOffset + kf - f.FirstStep]);
                Vector3 to = new Vector3(stepU[kc], sim.SampleY[c.SampleOffset + kc - c.FirstStep],
                    sim.SampleRho[c.SampleOffset + kc - c.FirstStep]);
                Fine.AddSegment(from, to, tint, LinkPx, 0, Id(c), 0.8f);
                FinePoints += 2;
            }
        }

        // ------------------------------------------------------------------ population and generations

        /// <summary>
        /// The gender-separated population curve: the envelope of the women's (inner) and men's (outer)
        /// halves from <paramref name="fromYear"/> to now, as edge lines over faint fills. The envelope
        /// widens with the population, so its shape is the population curve.
        /// </summary>
        public void BuildPopulationCurves(double fromYear, float id)
        {
            List<Vector3> center = new List<Vector3>(), women = new List<Vector3>(), men = new List<Vector3>();
            List<Color32> womenFill = new List<Color32>(), menFill = new List<Color32>();
            List<LinePoint> womenEdge = new List<LinePoint>(), menEdge = new List<LinePoint>();
            float y = GraphStyle.HumansY - 0.004f;

            void AddSample(float u, float c, float e, float fade)
            {
                center.Add(new Vector3(u, y, c));
                women.Add(new Vector3(u, y, c - e));
                men.Add(new Vector3(u, y, c + e));
                womenFill.Add(Tint(GraphStyle.HumansFemale, 0.07f * fade));
                menFill.Add(Tint(GraphStyle.HumansMale, 0.07f * fade));
                womenEdge.Add(new LinePoint(women[women.Count - 1], Tint(GraphStyle.HumansFemale, 0.5f * fade), 1.2f, 0, 1.2f));
                menEdge.Add(new LinePoint(men[men.Count - 1], Tint(GraphStyle.HumansMale, 0.5f * fade), 1.2f, 0, 1.2f));
            }

            int first = sim.StepAt(fromYear);
            int last = sim.StepCount - 1;
            for (int k = first; k <= last; k += 4)
            {
                float fade = Mathf.SmoothStep(0, 1, (float)((sim.TimeOf(k) - fromYear) / 4.0));
                AddSample(stepU[k], sim.Center[k], sim.Envelope[k], fade);
            }

            if ((last - first) % 4 != 0) AddSample(stepU[last], sim.Center[last], sim.Envelope[last], 1);
            AddSample(DeepTime.NowArc, sim.Center[last], sim.Envelope[last], 1);

            // fills: inner edge -> center (women), center -> outer edge (men)
            Surfaces.AddBand(women, center, womenFill, id);
            Surfaces.AddBand(center, men, menFill, id);
            Markers.AddPolyline(womenEdge, id);
            Markers.AddPolyline(menEdge, id);
        }

        /// <summary>
        /// A generation plane (as in the smv project): a faint neutral wall across the population at the
        /// first birth year of a generation, from the floor of the human layer to the top of the value axis.
        /// </summary>
        public void BuildGenerationPlane(double year)
        {
            int k = sim.StepAt(year);
            float u = U(year);
            float c = sim.Center[k], e = sim.Envelope[k] * 1.08f;
            float y0 = GraphStyle.HumansY - 0.01f, y1 = GraphStyle.HumansY + GraphStyle.SmvHeight + 0.02f;
            Vector3 innerLow = new Vector3(u, y0, c - e), innerHigh = new Vector3(u, y1, c - e);
            Vector3 outerLow = new Vector3(u, y0, c + e), outerHigh = new Vector3(u, y1, c + e);

            Color32 fill = Tint(GraphStyle.Axis, 0.05f);
            Surfaces.AddBand(new[] { innerLow, innerHigh }, new[] { outerLow, outerHigh }, new[] { fill, fill }, GraphIds.None);

            Color32 edge = Tint(GraphStyle.AxisDim, 0.55f);
            Markers.AddPolyline(new[] { innerLow, outerLow, outerHigh, innerHigh, innerLow }, edge, 1f, 0, GraphIds.None);
        }

        static Color32 Tint(Color c, float alpha) =>
            new Color32((byte)(Mathf.Clamp01(c.r) * 255f), (byte)(Mathf.Clamp01(c.g) * 255f),
                (byte)(Mathf.Clamp01(c.b) * 255f), (byte)(Mathf.Clamp01(alpha) * 255f));
    }
}
