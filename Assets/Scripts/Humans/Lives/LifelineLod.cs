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
    /// Level of detail of the lifelines by how they would look on screen (main thread). Near the camera
    /// target it measures how long a lifetime is on screen and how many pixels each line of a tier gets across
    /// the population (envelope width plus value height, over the lines alive there). A tier is readable once a
    /// life is long enough to rise and fall and its lines stand apart: zooming in adds the half and quarter
    /// tiers exactly when the lines between would be distinguishable. Independent of preset framing and of
    /// the radial and height scales, since everything is measured after the warp.
    /// </summary>
    public sealed class LifelineLod
    {
        /// <summary>The lifetime whose on-screen length says whether lifelines can show anything.</summary>
        public const double LifeYears = 30;

        // a lifetime must span this many pixels (from, full) before its rise and fall reads
        const float LifeFromPx = 8f, LifeFullPx = 24f;

        // pixels per line across the population (from, full)
        const float SpacingFromPx = 1.5f, SpacingFullPx = 4f;

        // probes outside the lens window (faded out) do not count
        const float MinFocusFade = 0.3f;

        readonly LodProbe[] probes;
        int lastCam = -1, lastWarp = -1;

        /// <param name="probes">cross-sections of every stream (see <see cref="LifelineMeshes.Probes"/>)</param>
        public LifelineLod(LodProbe[] probes)
        {
            this.probes = probes;
        }

        /// <summary><see cref="Civ.Index"/> of the stream nearest the camera target, -1 if none.</summary>
        public int Stream => NearestIndex >= 0 ? probes[NearestIndex].Stream : -1;

        /// <summary>Index of the probe nearest the camera target, -1 before the first measure.</summary>
        public int NearestIndex { get; private set; } = -1;

        /// <summary>The probe nearest the camera target (valid when <see cref="NearestIndex"/> is not -1).</summary>
        public LodProbe Nearest => probes[Mathf.Max(NearestIndex, 0)];

        /// <summary>On-screen length of a lifetime near the camera target (pixels, 0 when not in view).</summary>
        public float LifePx { get; private set; }

        /// <summary>Pixels per coarse line across the population near the camera target (0 when not in view).</summary>
        public float SpacingPx { get; private set; }

        /// <summary>How close the human layer is: 0 while a lifetime is a speck, 1 once it spans tens of pixels.</summary>
        public float Near => SmoothStep(LifeFromPx, LifeFullPx, LifePx);

        /// <summary>Readability of a lifeline tier (0 coarse, 1 half, 2 quarter: 2^tier times the coarse lines).</summary>
        public float Readable(int tier) =>
            Near * SmoothStep(SpacingFromPx, SpacingFullPx, SpacingPx / (1 << tier));

        /// <summary>Re-measures when the camera or the warp changed since the last call.</summary>
        public void Update(CameraRig rig)
        {
            if (rig == null || rig.Cam == null || probes.Length == 0) return;
            if (rig.Version == lastCam && GraphWarp.Version == lastWarp) return;
            lastCam = rig.Version;
            lastWarp = GraphWarp.Version;

            Vector3 target = rig.Pose.Target;
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < probes.Length; i++)
            {
                float d = (GraphWarp.ToWorld(probes[i].Center) - target).sqrMagnitude;
                if (d >= bestDistance) continue;
                bestDistance = d;
                best = i;
            }

            NearestIndex = best;
            LodProbe p = probes[best];
            LifePx = SpacingPx = 0;
            if (GraphWarp.FocusFade(p.Center.x, GraphWarp.Current) < MinFocusFade) return;

            Camera cam = rig.Cam;
            if (!Project(cam, p.Center, out Vector2 center) || !Project(cam, p.Later, out Vector2 later) ||
                !Project(cam, p.Inner, out Vector2 inner) || !Project(cam, p.Outer, out Vector2 outer) ||
                !Project(cam, p.Top, out Vector2 top))
            {
                return;
            }

            LifePx = Vector2.Distance(center, later);
            float spread = Vector2.Distance(inner, outer) + Vector2.Distance(center, top);
            SpacingPx = spread / Mathf.Max(p.Alive, 1f);
        }

        static bool Project(Camera cam, Vector3 data, out Vector2 screen)
        {
            Vector3 s = cam.WorldToScreenPoint(GraphWarp.ToWorld(data));
            screen = s;
            return s.z > 0;
        }

        static float SmoothStep(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }
    }
}
