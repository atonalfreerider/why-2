using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Why.Humans.Lives
{
    /// <summary>
    /// Turns the laid-out streams into line meshes (worker threads): three lifeline tiers and the
    /// gender-separated population curves (the envelope edges: women inside, men outside).
    ///
    /// The tiers nest: tier 0 (coarse) holds every 4th birth of a stream (N people per line), tier 1 (half)
    /// adds the births between them (every 2nd birth: N / 2 people per line with both drawn) and tier 2
    /// (quarter) the rest (N / 4). Zooming in only ever adds lines between the ones already there. Lifelines
    /// are numbered in birth order within their stream (<see cref="GraphIds.Lifeline"/>), so a range of births
    /// is a contiguous id range in every tier. Each tier is filled by its own thread.
    /// </summary>
    public sealed class LifelineMeshes
    {
        /// <summary>Number of lifeline tiers (coarse, half, quarter).</summary>
        public const int TierCount = 3;

        // lifelines are dense and additive: faint individually, bright where many overlap
        const float AdultAlpha = 0.5f;
        const float ChildAlpha = 0.28f;
        const float AdultWidthPx = 1f;
        const float ChildWidthPx = 0.7f;
        const float AdultWidthWorld = 0.0005f;
        const float ChildWidthWorld = 0.0003f;
        const float AdultIntensity = 1f;
        const float ChildIntensity = 0.6f;

        /// <summary>A war death ends in a brief spark: men's lines ending in a war read as a war.</summary>
        const float WarDeathIntensity = 2.5f;

        // population curves
        const float CurveLift = 0.0015f;
        const float CurveAlpha = 0.45f;
        const float CurveWidthPx = 1f;
        const float CurveWidthWorld = 0.0006f;
        const float CurveIntensity = 1.2f;
        const double CurveMinStep = 1.5;
        const double CurveMaxStep = 8;
        const double CurveMaxStepPrehistory = 400;
        const double CurveLnStep = 0.005;
        const double CurveLnOffset = 300;

        const int ProbesPerStream = 32;

        /// <summary>Vertex tints of the two sexes (the hue stays inside the blue of the human level).</summary>
        public static readonly Color32 WomenTint = Tint(GraphStyle.HumansFemale);

        public static readonly Color32 MenTint = Tint(GraphStyle.HumansMale);

        /// <summary>Lifeline tiers: 0 coarse, 1 half, 2 quarter (see the class summary).</summary>
        public readonly LineMeshBuilder[] Tiers = new LineMeshBuilder[TierCount];

        public readonly int[] TierLines = new int[TierCount];
        public readonly int[] TierPoints = new int[TierCount];
        public readonly LineMeshBuilder Curves = new LineMeshBuilder(24_000);
        public readonly List<LodProbe> Probes = new List<LodProbe>();

        readonly IReadOnlyList<CivLives> streams;

        /// <summary>Sizes the builders exactly for the drawn lives of <paramref name="streams"/>.</summary>
        public LifelineMeshes(IReadOnlyList<CivLives> streams)
        {
            this.streams = streams;
            int[] points = new int[TierCount];
            foreach (CivLives s in streams)
            {
                if (s == null) continue;
                Person[] people = s.People;
                for (int i = 0; i < people.Length; i++)
                {
                    if (people[i].Drawn) points[TierOf(i)] += people[i].PointCount;
                }
            }

            for (int t = 0; t < TierCount; t++) Tiers[t] = new LineMeshBuilder(Math.Max(points[t], 16));
        }

        /// <summary>Tier of the i-th birth of a stream.</summary>
        public static int TierOf(int birthIndex) =>
            birthIndex % CivLives.FineFactor == 0 ? 0 : birthIndex % (CivLives.FineFactor / 2) == 0 ? 1 : 2;

        /// <summary>
        /// Fills the half and quarter tiers, each on its own thread. Reads only the finished streams, so it may run
        /// in the background while the rest of the graph uploads.
        /// </summary>
        public void EmitDenseTiers() => Parallel.For(1, TierCount, EmitTier);

        /// <summary>Fills one lifeline tier from every stream. Touches only that tier's builder and counters.</summary>
        public void EmitTier(int tier)
        {
            List<LinePoint> pts = new List<LinePoint>(256);
            LineMeshBuilder builder = Tiers[tier];
            foreach (CivLives s in streams)
            {
                if (s == null) continue;
                Person[] people = s.People;
                for (int i = 0; i < people.Length; i++)
                {
                    ref Person p = ref people[i];
                    if (!p.Drawn || TierOf(i) != tier) continue;
                    Color32 tint = p.Male ? MenTint : WomenTint;
                    pts.Clear();
                    int last = p.PointCount - 1;
                    for (int j = 0; j <= last; j++)
                    {
                        int at = p.FirstPoint + j;
                        bool adult = s.PtAdult[at];
                        float alpha = (adult ? AdultAlpha : ChildAlpha) * s.PtFade[at];
                        float intensity = j == last && p.DiedInWar ? WarDeathIntensity
                            : adult ? AdultIntensity : ChildIntensity;
                        pts.Add(new LinePoint(s.PtData[at], WithAlpha(tint, alpha), adult ? AdultWidthPx : ChildWidthPx,
                            adult ? AdultWidthWorld : ChildWidthWorld, intensity));
                    }

                    builder.AddPolyline(pts, GraphIds.Lifeline(s.Civ.Index, s.IdOffset + i));
                    TierLines[tier]++;
                    TierPoints[tier] += pts.Count;
                }
            }
        }

        /// <summary>
        /// The population curves and camera probes of every stream. Uses the streams' grids, which are not
        /// thread safe: run this on one thread, and nothing else that samples the grids at the same time.
        /// </summary>
        public void EmitCurves(double nowYear)
        {
            List<LinePoint> women = new List<LinePoint>(1024), men = new List<LinePoint>(1024);
            List<double> times = new List<double>(1024);
            foreach (CivLives s in streams)
            {
                if (s == null || s.People.Length == 0) continue;
                AddCurves(s, nowYear, times, women, men);
                AddProbes(s, nowYear);
            }
        }

        /// <summary>
        /// The population curve of one stream, one line per sex along the edge of the lifelines' envelope:
        /// it widens with the population and the men's side dents where wars killed men.
        /// </summary>
        void AddCurves(CivLives s, double nowYear, List<double> times, List<LinePoint> women, List<LinePoint> men)
        {
            CivEnvelope grid = s.Grid;
            double maxStep = s.Prehistoric ? CurveMaxStepPrehistory : CurveMaxStep;
            CurveTimes(grid.Time(0), s.LineEnd, nowYear, maxStep, times);
            women.Clear();
            men.Clear();
            float y = GraphStyle.HumansY + CurveLift;
            foreach (double t in times)
            {
                grid.At(t, out float u, out float center, out float envWomen, out float envMen);
                float alpha = CurveAlpha * s.CurveFade(t);
                women.Add(new LinePoint(new Vector3(u, y, center - envWomen), WithAlpha(WomenTint, alpha), CurveWidthPx,
                    CurveWidthWorld, CurveIntensity));
                men.Add(new LinePoint(new Vector3(u, y, center + envMen), WithAlpha(MenTint, alpha), CurveWidthPx,
                    CurveWidthWorld, CurveIntensity));
            }

            int id = GraphIds.Civ(s.Civ.Index);
            Curves.AddPolyline(women, id);
            Curves.AddPolyline(men, id);
        }

        /// <summary>
        /// Sample times of a curve: dense in unrolled (log) time and while the band emerges or dissolves,
        /// at most <paramref name="maxStep"/> years apart.
        /// </summary>
        static void CurveTimes(double start, double end, double nowYear, double maxStep, List<double> output)
        {
            output.Clear();
            double t = start;
            while (t < end)
            {
                output.Add(t);
                double ya = Math.Max(nowYear - t, 0);
                double step = Math.Min(maxStep, Math.Max(CurveMinStep, CurveLnStep * (ya + CurveLnOffset)));
                if (t - start < HumanWorld.EmergeYears || end - t < HumanWorld.EmergeYears) step = CurveMinStep;
                t += step;
            }

            output.Add(end);
        }

        /// <summary>
        /// Cross-sections of the stream for the level of detail (<see cref="LifelineLod"/>), evenly spaced in log
        /// time: envelope, value range, the band one lifetime on, and the coarse lines alive there.
        /// </summary>
        void AddProbes(CivLives s, double nowYear)
        {
            CivEnvelope grid = s.Grid;
            double start = grid.Time(0), end = s.LineEnd;
            double lnA = Math.Log(Math.Max(nowYear - start, 0) + CurveLnOffset);
            double lnB = Math.Log(Math.Max(nowYear - end, 0) + CurveLnOffset);
            float y = GraphStyle.HumansY;
            for (int i = 0; i < ProbesPerStream; i++)
            {
                double f = (i + 0.5) / ProbesPerStream;
                double year = nowYear - (Math.Exp(lnA + (lnB - lnA) * f) - CurveLnOffset);
                grid.At(year, out float u, out float center, out float envWomen, out float envMen);

                // one lifetime on, or back where the stream's lines end sooner
                double other = year + LifelineLod.LifeYears <= end
                    ? year + LifelineLod.LifeYears
                    : Math.Max(start, year - LifelineLod.LifeYears);
                grid.At(other, out float uOther, out float centerOther, out float _, out float _);
                Probes.Add(new LodProbe
                {
                    Center = new Vector3(u, y, center),
                    Inner = new Vector3(u, y, center - envWomen),
                    Outer = new Vector3(u, y, center + envMen),
                    Top = new Vector3(u, y + GraphStyle.SmvHeight, center),
                    Later = new Vector3(uOther, y, centerOther),
                    Alive = (float)s.LinesAlive(year),
                    Stream = s.Civ.Index
                });
            }
        }

        static Color32 Tint(Color c) =>
            new Color32((byte)(Mathf.Clamp01(c.r) * 255f + 0.5f), (byte)(Mathf.Clamp01(c.g) * 255f + 0.5f),
                (byte)(Mathf.Clamp01(c.b) * 255f + 0.5f), 255);

        static Color32 WithAlpha(Color32 c, float alpha)
        {
            c.a = (byte)Mathf.Clamp(alpha * 255f + 0.5f, 0f, 255f);
            return c;
        }
    }
}
