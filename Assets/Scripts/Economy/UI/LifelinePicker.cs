using System;
using UnityEngine;
using Why.Humans.Smv;

namespace Why.Economy.UI
{
    /// <summary>
    /// Finds the lifeline nearest a point of the screen, and where a person's line is at a year. Pure math on the
    /// population's samples (no Unity objects but the camera), so the inspector's click and the N key share it and the
    /// harness can test it.
    ///
    /// Every quarter-year sample of a line sits at <c>GraphWarp.ToWorld(u, y, rho)</c>, which is linear in y and rho for a
    /// fixed time (the warp moves a point of the road and offsets it along the road's normal and up). So the clip-space
    /// position of any sample is <c>C0[k] + rho * CRho[k] + y * CY[k]</c> with three vectors per simulation step k,
    /// computed once per click: projecting a sample is then nine multiply-adds instead of a warp evaluation.
    ///
    /// A click projects every line coarsely (one sample every <see cref="MinStride"/> steps, i.e. every two years, more
    /// sparsely if that would exceed <see cref="MaxProjections"/>), measures the cursor's distance to each projected
    /// segment, keeps the <see cref="Candidates"/> nearest lines, and refines them at the full quarter-year resolution
    /// around their nearest segment. Where lines lie a pixel apart (the bundle seen from afar) the nearest is a matter
    /// of chance, so a line the viewer already selected wins within <see cref="PreferSlackPx"/>: a click on the glowing
    /// line keeps it.
    /// </summary>
    public sealed class LifelinePicker
    {
        /// <summary>Most samples projected by one pick (the coarse pass; the refinement adds a few dozen).</summary>
        public const int MaxProjections = 200_000;

        /// <summary>
        /// Simulation steps (quarter years) between the samples of the coarse pass: two years. A line's thinned mesh
        /// keeps a point at least every six years and bends gently between them, so a chord two years long stays
        /// within a pixel or two of the drawn line in every view of the scene.
        /// </summary>
        public const int MinStride = 8;

        /// <summary>
        /// Lines refined at full resolution after the coarse pass (the nearest by their chords). A two-year chord strays
        /// a pixel or more from a bending line, and in the zoomed-out bundle a dozen lines pass within a pixel of a
        /// click: with four (or eight) candidates the line under the cursor was sometimes not among them (a click right
        /// on a sample found another line 1.1 px away in the overview). Sixteen cost ~270 projections more per click.
        /// </summary>
        public const int Candidates = 16;

        /// <summary>A preferred line (the selected person's) wins when it is at most this many pixels further away than
        /// the nearest line: about the width of a drawn line.</summary>
        public const float PreferSlackPx = 1.5f;

        /// <summary>Clip w below which a sample counts as behind the camera.</summary>
        const float MinClipW = 1e-4f;

        /// <summary>What a pick found: the person, the simulation step of the nearest sample, the distance in pixels.</summary>
        public struct Hit
        {
            public int Person;
            public int Step;
            public float DistancePx;
        }

        Vector4[] c0 = Array.Empty<Vector4>(), cRho = Array.Empty<Vector4>(), cY = Array.Empty<Vector4>();
        readonly int[] candidate = new int[Candidates];
        readonly int[] candidateStep = new int[Candidates];
        readonly float[] candidateDistance = new float[Candidates];
        SmvPopulation pop;
        Vector2 screen;

        /// <summary>Samples projected by the last pick (coarse pass and refinement).</summary>
        public int LastProjections { get; private set; }

        /// <summary>Step stride of the last pick's coarse pass.</summary>
        public int LastStride { get; private set; }

        /// <summary>
        /// Projects the population for a camera and a warp (what is on screen: <c>GraphWarp.Current</c>). Picks and
        /// <see cref="ScreenAt"/> use this until the next call.
        /// </summary>
        public bool Prepare(SmvPopulation population, Camera cam, WarpState warp)
        {
            pop = population;
            if (pop?.Sim == null || cam == null || pop.Sim.People.Count == 0 || pop.Sim.StepCount <= 0)
            {
                pop = null;
                return false;
            }

            screen = new Vector2(Mathf.Max(1, cam.pixelWidth), Mathf.Max(1, cam.pixelHeight));
            Matrix4x4 viewProjection = cam.projectionMatrix * cam.worldToCameraMatrix;
            SmvSimulation sim = pop.Sim;
            int n = sim.StepCount;
            if (c0.Length != n)
            {
                c0 = new Vector4[n];
                cRho = new Vector4[n];
                cY = new Vector4[n];
            }

            for (int k = 0; k < n; k++)
            {
                float u = pop.U(sim.TimeOf(k));
                Vector3 w0 = GraphWarp.ToWorld(u, 0, 0, warp);
                Vector3 wRho = GraphWarp.ToWorld(u, 0, 1, warp) - w0;
                Vector3 wY = GraphWarp.ToWorld(u, 1, 0, warp) - w0;
                c0[k] = viewProjection * new Vector4(w0.x, w0.y, w0.z, 1);
                cRho[k] = viewProjection * new Vector4(wRho.x, wRho.y, wRho.z, 0);
                cY[k] = viewProjection * new Vector4(wY.x, wY.y, wY.z, 0);
            }

            return true;
        }

        /// <summary>
        /// The person whose line passes nearest the screen point (pixels, origin bottom-left, like the mouse) under a camera
        /// and a warp, within <paramref name="radiusPx"/>. <paramref name="coarseOnly"/> limits the search to the lines of
        /// the zoomed-out tier (<see cref="SmvGeometry.InCoarseTier"/>), the only ones drawn while it shows.
        /// <paramref name="prefer"/> (a person, or -1) wins over a nearer line within <see cref="PreferSlackPx"/>.
        /// </summary>
        public bool Pick(SmvPopulation population, Camera cam, WarpState warp, Vector2 point, float radiusPx,
            bool coarseOnly, int prefer, out Hit hit)
        {
            hit = new Hit { Person = -1, Step = -1, DistancePx = float.PositiveInfinity };
            LastProjections = 0;
            return Prepare(population, cam, warp) && Pick(point, radiusPx, coarseOnly, prefer, out hit);
        }

        /// <summary>A pick with the projection of the last <see cref="Prepare"/>.</summary>
        public bool Pick(Vector2 point, float radiusPx, bool coarseOnly, int prefer, out Hit hit)
        {
            hit = new Hit { Person = -1, Step = -1, DistancePx = float.PositiveInfinity };
            LastProjections = 0;
            if (pop == null) return false;
            SmvSimulation sim = pop.Sim;

            // stride: two years, or sparser so the coarse pass stays under the budget
            long samples = 0;
            foreach (SmvPerson p in sim.People)
            {
                if (!coarseOnly || SmvGeometry.InCoarseTier(p)) samples += p.SampleCount;
            }

            int stride = Math.Max(MinStride, (int)Math.Ceiling(samples / (double)MaxProjections));
            LastStride = stride;
            for (int i = 0; i < Candidates; i++)
            {
                candidate[i] = -1;
                candidateDistance[i] = float.PositiveInfinity;
            }

            // coarse pass: chords between samples `stride` steps apart (and the last sample of each line)
            int projections = 0;
            foreach (SmvPerson p in sim.People)
            {
                if (p.SampleCount <= 0 || (coarseOnly && !SmvGeometry.InCoarseTier(p))) continue;
                float best = Nearest(sim, p, p.FirstStep, p.LastStep, stride, point, out int bestStep, ref projections);
                Offer(p.Index, best, bestStep);
            }

            // refinement: every step around each candidate's nearest chord
            for (int i = 0; i < Candidates; i++)
            {
                if (candidate[i] < 0) continue;
                float d = Refine(sim, sim.People[candidate[i]], candidateStep[i], stride, point, out int step, ref projections);
                if (d < hit.DistancePx)
                {
                    hit.DistancePx = d;
                    hit.Person = candidate[i];
                    hit.Step = step;
                }
            }

            // the selected line keeps the click when it is about as near as the nearest
            if (prefer >= 0 && prefer < sim.People.Count && prefer != hit.Person)
            {
                SmvPerson p = sim.People[prefer];
                if (p.SampleCount > 0 && (!coarseOnly || SmvGeometry.InCoarseTier(p)))
                {
                    Nearest(sim, p, p.FirstStep, p.LastStep, stride, point, out int near, ref projections);
                    float d = Refine(sim, p, near, stride, point, out int step, ref projections);
                    if (d <= radiusPx && d <= hit.DistancePx + PreferSlackPx)
                    {
                        hit.DistancePx = d;
                        hit.Person = prefer;
                        hit.Step = step;
                    }
                }
            }

            LastProjections = projections;
            return hit.Person >= 0 && hit.DistancePx <= radiusPx;
        }

        /// <summary>A line's distance at full resolution within a stride of a step (its nearest coarse segment).</summary>
        float Refine(SmvSimulation sim, SmvPerson p, int around, int stride, Vector2 point, out int step, ref int projections)
        {
            int from = Math.Max(p.FirstStep, around - stride);
            int to = Math.Min(p.LastStep, around + stride);
            return Nearest(sim, p, from, to, 1, point, out step, ref projections);
        }

        /// <summary>Screen position (pixels, origin bottom-left) of a person's line at a step under the last
        /// <see cref="Prepare"/>; false outside the life or behind the camera.</summary>
        public bool ScreenAt(int person, int step, out Vector2 screenPoint)
        {
            screenPoint = default;
            if (pop == null || person < 0 || person >= pop.Sim.People.Count) return false;
            SmvPerson p = pop.Sim.People[person];
            if (step < p.FirstStep || step > p.LastStep || step >= c0.Length) return false;
            return Project(pop.Sim, p, step, out screenPoint);
        }

        /// <summary>A person's sample at a step on screen (pixels); false behind the camera.</summary>
        bool Project(SmvSimulation sim, SmvPerson p, int step, out Vector2 s)
        {
            int i = p.SampleOffset + step - p.FirstStep;
            float y = sim.SampleY[i], rho = sim.SampleRho[i];
            Vector4 a = c0[step], b = cRho[step], c = cY[step];
            float w = a.w + b.w * rho + c.w * y;
            if (w <= MinClipW)
            {
                s = default;
                return false;
            }

            float x = a.x + b.x * rho + c.x * y;
            float yy = a.y + b.y * rho + c.y * y;
            s = new Vector2((x / w * 0.5f + 0.5f) * screen.x, (yy / w * 0.5f + 0.5f) * screen.y);
            return true;
        }

        /// <summary>
        /// Distance (pixels) from the point to a stretch of a line drawn through every <paramref name="stride"/>-th step
        /// from <paramref name="from"/> to <paramref name="to"/> (always ending on <paramref name="to"/>), and the step of the
        /// nearer end of the nearest segment.
        /// </summary>
        float Nearest(SmvSimulation sim, SmvPerson p, int from, int to, int stride, Vector2 point, out int bestStep,
            ref int projections)
        {
            float best = float.PositiveInfinity;
            bestStep = from;
            bool hasPrev = false;
            Vector2 prev = default;
            int prevStep = from;
            for (int k = from;; k += stride)
            {
                if (k > to) k = to;
                projections++;
                if (Project(sim, p, k, out Vector2 s))
                {
                    float d;
                    int near = k;
                    if (hasPrev)
                    {
                        d = SegmentDistance(point, prev, s, out float t);
                        near = t < 0.5f ? prevStep : k;
                    }
                    else
                    {
                        d = Vector2.Distance(point, s);
                    }

                    if (d < best)
                    {
                        best = d;
                        bestStep = near;
                    }

                    prev = s;
                    prevStep = k;
                    hasPrev = true;
                }
                else
                {
                    hasPrev = false;
                }

                if (k >= to) break;
            }

            return best;
        }

        /// <summary>Keeps the <see cref="Candidates"/> nearest lines (sorted, nearest first).</summary>
        void Offer(int person, float distance, int step)
        {
            if (!(distance < candidateDistance[Candidates - 1])) return;
            int i = Candidates - 1;
            while (i > 0 && distance < candidateDistance[i - 1])
            {
                candidate[i] = candidate[i - 1];
                candidateStep[i] = candidateStep[i - 1];
                candidateDistance[i] = candidateDistance[i - 1];
                i--;
            }

            candidate[i] = person;
            candidateStep[i] = step;
            candidateDistance[i] = distance;
        }

        /// <summary>Distance from p to the segment a-b, and where along it the nearest point lies (0 = a, 1 = b).</summary>
        public static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b, out float t)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            t = len2 > 1e-8f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }

        // ------------------------------------------------------------------ a person at a year

        /// <summary>
        /// The step of a person's line nearest the middle of a calendar year, clamped to their life (the year they
        /// entered or the last step they lived); false for a person without samples.
        /// </summary>
        public static bool StepAt(SmvPopulation pop, int person, double year, out int step)
        {
            step = -1;
            if (pop?.Sim == null || person < 0 || person >= pop.Sim.People.Count) return false;
            SmvPerson p = pop.Sim.People[person];
            if (p.SampleCount <= 0) return false;
            step = Mathf.Clamp(pop.Sim.StepAt(year + 0.5), p.FirstStep, p.LastStep);
            return true;
        }

        /// <summary>World position of a person's line at a year (clamped to their life) under a warp.</summary>
        public static bool WorldAt(SmvPopulation pop, int person, double year, WarpState warp, out Vector3 world)
        {
            world = default;
            if (!StepAt(pop, person, year, out int step)) return false;
            if (!pop.PointAt(pop.Sim.People[person], step, out Vector3 data)) return false;
            world = GraphWarp.ToWorld(data.x, data.y, data.z, warp);
            return true;
        }
    }
}
