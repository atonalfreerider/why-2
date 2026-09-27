using UnityEngine;

namespace Why.Humans.Lives
{
    /// <summary>
    /// A cross-section of one stream at one moment (data space), prepared on the worker thread: where its
    /// population sits and how many coarse lifelines are alive there.
    /// </summary>
    public struct LodProbe
    {
        /// <summary>Band center on the floor of the human layer.</summary>
        public Vector3 Center;

        /// <summary>Inner (women's) and outer (men's) edge of the population envelope.</summary>
        public Vector3 Inner, Outer;

        /// <summary>The top of the value range above the center (lifelines rise up to here).</summary>
        public Vector3 Top;

        /// <summary>Band center one lifetime (<see cref="LifelineLod.LifeYears"/>) away along the stream.</summary>
        public Vector3 Later;

        /// <summary>Coarse lifelines alive at this moment (expected: population / people per line).</summary>
        public float Alive;

        /// <summary><see cref="Civ.Index"/> of the stream.</summary>
        public int Stream;
    }

    /// <summary>
    /// Level of detail of the lifelines by how they would look on screen (main thread). Around the camera
    /// target it measures, at the probes nearest to it, how long a lifetime is on screen and how many pixels
    /// each line of a tier gets across the population (envelope width plus value height, over the lines alive
    /// there). A tier is readable once a life is long enough to rise and fall and its lines stand apart:
    /// zooming in adds the half and quarter tiers exactly when the lines between would be distinguishable.
    /// The typical lifetime (median) and the denser streams (lower quartile of the spacing) decide, so a sparse
    /// stream next to the target cannot switch on detail that would clutter its crowded neighbors. Independent
    /// of preset framing and of the radial and height scales, since everything is measured after the warp.
    /// </summary>
    public sealed class LifelineLod
    {
        /// <summary>The lifetime whose on-screen length says whether lifelines can show anything.</summary>
        public const double LifeYears = 30;

        // a lifetime must span this many pixels (from, full) before its rise and fall reads (coarse tier)
        const float LifeFromPx = 8f, LifeFullPx = 24f;

        // pixels per line across the population (from, full)
        const float SpacingFromPx = 1.5f, SpacingFullPx = 4f;

        // probes outside the lens window (faded out) do not count
        const float MinFocusFade = 0.3f;

        /// <summary>Probes nearest the camera target that are measured: the neighborhood the view is about.</summary>
        const int Samples = 24;

        /// <summary>The spacing that decides: this quantile of the measured probes (the denser ones).</summary>
        const float SpacingQuantile = 0.25f;

        /// <summary>Probe centers may lie this far outside the screen (pixels) and still count.</summary>
        const float ScreenMargin = 64f;

        readonly LodProbe[] probes;
        readonly int[] nearest = new int[Samples];
        readonly float[] nearestDistance = new float[Samples];
        readonly float[] lifeSamples = new float[Samples];
        readonly float[] spacingSamples = new float[Samples];
        int lastCam = -1, lastWarp = -1;

        /// <param name="probes">cross-sections of every stream (see <see cref="LifelineMeshes.Probes"/>)</param>
        public LifelineLod(LodProbe[] probes)
        {
            this.probes = probes;
        }

        /// <summary><see cref="Civ.Index"/> of the stream nearest the camera target, -1 if none.</summary>
        public int Stream => NearestIndex >= 0 ? probes[NearestIndex].Stream : -1;

        /// <summary>Index of the probe nearest the camera target (in view when possible), -1 before the first measure.</summary>
        public int NearestIndex { get; private set; } = -1;

        /// <summary>The probe nearest the camera target (valid when <see cref="NearestIndex"/> is not -1).</summary>
        public LodProbe Nearest => probes[Mathf.Max(NearestIndex, 0)];

        /// <summary>Typical on-screen length of a lifetime around the camera target (pixels, 0 when not in view).</summary>
        public float LifePx { get; private set; }

        /// <summary>Pixels per coarse line across the population around the camera target (0 when not in view).</summary>
        public float SpacingPx { get; private set; }

        /// <summary>How close the human layer is: 0 while a lifetime is a speck, 1 once it spans tens of pixels.</summary>
        public float Near => SmoothStep(LifeFromPx, LifeFullPx, LifePx);

        /// <summary>
        /// Readability of a lifeline tier (0 coarse, 1 half, 2 quarter: 2^tier times the coarse lines). Its lines
        /// must stand as far apart as coarse lines need to, and its lifetimes be 2^tier times as long on screen:
        /// while a life is a short tick, more lines would only thicken the ticks into a wall.
        /// </summary>
        public float Readable(int tier)
        {
            float k = 1 << tier;
            return SmoothStep(LifeFromPx * k, LifeFullPx * k, LifePx) *
                   SmoothStep(SpacingFromPx, SpacingFullPx, SpacingPx / k);
        }

        /// <summary>Re-measures when the camera or the warp changed since the last call.</summary>
        public void Update(CameraRig rig)
        {
            if (rig == null || rig.Cam == null || probes.Length == 0) return;
            if (rig.Version == lastCam && GraphWarp.Version == lastWarp) return;
            lastCam = rig.Version;
            lastWarp = GraphWarp.Version;

            int found = FindNearest(rig.Pose.Target);
            NearestIndex = found > 0 ? nearest[0] : -1;
            LifePx = SpacingPx = 0;

            // probes in view; zoomed in between two probes of a stream, the nearest ones in front of the camera
            // still tell the scale
            int n = Measure(rig.Cam, found, true);
            if (n == 0) n = Measure(rig.Cam, found, false);
            if (n == 0) return;
            Sort(lifeSamples, n);
            Sort(spacingSamples, n);
            LifePx = lifeSamples[n / 2];
            SpacingPx = spacingSamples[Mathf.RoundToInt(SpacingQuantile * (n - 1))];
        }

        /// <summary>
        /// Measures the first <paramref name="found"/> nearest probes (inside the lens window, in front of the
        /// camera, and on screen if <paramref name="onScreen"/>) into the sample arrays; the nearest measured probe
        /// names the stream (HUD, side labels). Returns the number of samples.
        /// </summary>
        int Measure(Camera cam, int found, bool onScreen)
        {
            WarpState warp = GraphWarp.Current;
            int n = 0;
            for (int s = 0; s < found; s++)
            {
                LodProbe p = probes[nearest[s]];
                if (GraphWarp.FocusFade(p.Center.x, warp) < MinFocusFade) continue;
                if (!Project(cam, p.Center, out Vector2 center) || (onScreen && !OnScreen(center)) ||
                    !Project(cam, p.Later, out Vector2 later) || !Project(cam, p.Inner, out Vector2 inner) ||
                    !Project(cam, p.Outer, out Vector2 outer) || !Project(cam, p.Top, out Vector2 top))
                {
                    continue;
                }

                if (n == 0) NearestIndex = nearest[s];
                float spread = Vector2.Distance(inner, outer) + Vector2.Distance(center, top);
                lifeSamples[n] = Vector2.Distance(center, later);
                spacingSamples[n] = spread / Mathf.Max(p.Alive, 1f);
                n++;
            }

            return n;
        }

        /// <summary>Fills <see cref="nearest"/> with the probes nearest to a world point, closest first.</summary>
        int FindNearest(Vector3 target)
        {
            int found = 0;
            for (int i = 0; i < probes.Length; i++)
            {
                float d = (GraphWarp.ToWorld(probes[i].Center) - target).sqrMagnitude;
                if (float.IsNaN(d) || (found == Samples && d >= nearestDistance[Samples - 1])) continue;
                int j = found < Samples ? found++ : Samples - 1;
                while (j > 0 && nearestDistance[j - 1] > d)
                {
                    nearestDistance[j] = nearestDistance[j - 1];
                    nearest[j] = nearest[j - 1];
                    j--;
                }

                nearestDistance[j] = d;
                nearest[j] = i;
            }

            return found;
        }

        static bool OnScreen(Vector2 p) =>
            p.x > -ScreenMargin && p.x < Screen.width + ScreenMargin &&
            p.y > -ScreenMargin && p.y < Screen.height + ScreenMargin;

        static bool Project(Camera cam, Vector3 data, out Vector2 screen)
        {
            Vector3 s = cam.WorldToScreenPoint(GraphWarp.ToWorld(data));
            screen = s;
            return s.z > 0;
        }

        /// <summary>Insertion sort of the first <paramref name="count"/> values (a couple of dozen; no allocation).</summary>
        static void Sort(float[] values, int count)
        {
            for (int i = 1; i < count; i++)
            {
                float v = values[i];
                int j = i - 1;
                while (j >= 0 && values[j] > v)
                {
                    values[j + 1] = values[j];
                    j--;
                }

                values[j + 1] = v;
            }
        }

        static float SmoothStep(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }
    }
}
