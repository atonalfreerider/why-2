using System.Collections.Generic;
using UnityEngine;

namespace Why.Economy.Land
{
    /// <summary>
    /// Where the land stands in the world (1.2): the bowl's center on the plaza <see cref="LandStyle.PlazaGap"/> units past
    /// the road's end, turned so local +z runs along the road away from the past and +y is up. Every land mesh is built
    /// in local coordinates and drawn with raw materials (<see cref="GraphMaterials.Raw"/>) on a GameObject placed with
    /// <see cref="Place"/>; labels and anchors use <see cref="World(Vector3)"/> (the economy lens never changes, so these
    /// world positions hold in every view). Bowl coordinates (r, θ, y): local x = r cos θ, z = r sin θ; θ = 270° faces
    /// the road (the near side), 90° is the far side; angles grow counter-clockwise seen from above.
    /// Pure math: the frame is a value and the helpers are static, so builders use them on any thread.
    /// </summary>
    public readonly struct LandFrame
    {
        /// <summary>The bowl's center (world, y = 0).</summary>
        public readonly Vector3 Origin;

        /// <summary>The frame's rotation: local +z along the road (away from the past), +y up.</summary>
        public readonly Quaternion Rotation;

        public LandFrame(Vector3 origin, Quaternion rotation)
        {
            Origin = origin;
            Rotation = rotation;
        }

        /// <summary>The frame's local +x in world space (θ = 0°: to the right, seen from the road).</summary>
        public Vector3 Right => Rotation * Vector3.right;

        /// <summary>The frame's local +y in world space (up).</summary>
        public Vector3 Up => Rotation * Vector3.up;

        /// <summary>The frame's local +z in world space: the road's direction, away from the past (θ = 90°, the far side).</summary>
        public Vector3 Forward => Rotation * Vector3.forward;

        /// <summary>World position of a land-local point.</summary>
        public Vector3 World(Vector3 local) => Origin + Rotation * local;

        public Vector3 World(float x, float y, float z) => World(new Vector3(x, y, z));

        /// <summary>Land-local position of a world point.</summary>
        public Vector3 Local(Vector3 world) => Quaternion.Inverse(Rotation) * (world - Origin);

        /// <summary>World position of a bowl point (radius, degrees, height).</summary>
        public Vector3 WorldPolar(float r, float thetaDeg, float y) => World(Polar(r, thetaDeg, y));

        /// <summary>Yaw (degrees, 0 = +z) of a camera looking along the land's +z: from the road toward the bowl.</summary>
        public float Yaw => Mathf.Atan2(Forward.x, Forward.z) * Mathf.Rad2Deg;

        /// <summary>Applies the frame to a transform (main thread).</summary>
        public void Place(Transform t) => t.SetPositionAndRotation(Origin, Rotation);

        // ------------------------------------------------------------------ polar helpers (land-local)

        /// <summary>Land-local point of bowl coordinates: x = r cos θ, z = r sin θ.</summary>
        public static Vector3 Polar(float r, float thetaDeg, float y)
        {
            float a = thetaDeg * Mathf.Deg2Rad;
            return new Vector3(r * Mathf.Cos(a), y, r * Mathf.Sin(a));
        }

        /// <summary>Radius of a land-local point (distance from the bowl's axis).</summary>
        public static float RadiusOf(Vector3 local) => Mathf.Sqrt(local.x * local.x + local.z * local.z);

        /// <summary>Angle of a land-local point, degrees in [0, 360).</summary>
        public static float ThetaOf(Vector3 local) => LandMath.Wrap360(Mathf.Atan2(local.z, local.x) * Mathf.Rad2Deg);

        /// <summary>The unit radial direction at an angle (horizontal, land-local).</summary>
        public static Vector3 Radial(float thetaDeg) => Polar(1, thetaDeg, 0);

        /// <summary>The unit tangent at an angle, pointing counter-clockwise (horizontal, land-local).</summary>
        public static Vector3 Tangent(float thetaDeg)
        {
            float a = thetaDeg * Mathf.Deg2Rad;
            return new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a));
        }

        /// <summary>
        /// Height of the bowl's surface at a radius: flat treads (the gov floor, rings 1-4, the rim beyond the lip),
        /// linear risers between them (the last riser climbs from the tech tread to the rim at the lip).
        /// </summary>
        public static float SurfaceY(float r)
        {
            float[] a = LandStyle.RingA, b = LandStyle.RingB, y = LandStyle.TerraceY;
            r = Mathf.Abs(r);
            for (int t = 0; t < 5; t++)
            {
                if (r <= b[t]) return y[t];
                float next = t < 4 ? a[t + 1] : LandStyle.LipR;
                float top = t < 4 ? y[t + 1] : LandStyle.RimY;
                if (r < next) return Mathf.Lerp(y[t], top, (r - b[t]) / (next - b[t]));
            }

            return LandStyle.RimY;
        }

        /// <summary>The tread height of a ring.</summary>
        public static float TreadY(Tier tier) => LandStyle.TerraceY[(int)tier];

        // ------------------------------------------------------------------ sampling

        /// <summary>Segments for an arc of a span (degrees) so no segment exceeds stepDeg (at least 1).</summary>
        public static int Segments(float spanDeg, float stepDeg = 3.75f) => Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(spanDeg) / stepDeg));

        /// <summary>
        /// Appends the points of an arc at radius r and height y from theta0 to theta1 (degrees, either direction), both
        /// ends included, at most stepDeg apart (96 segments per turn by default: the treads' resolution).
        /// </summary>
        public static void SampleArc(List<Vector3> into, float r, float theta0, float theta1, float y, float stepDeg = 3.75f)
        {
            int n = Segments(theta1 - theta0, stepDeg);
            for (int k = 0; k <= n; k++) into.Add(Polar(r, Mathf.Lerp(theta0, theta1, k / (float)n), y));
        }

        /// <summary>
        /// Appends a sector's closed outline at its tread height (inner arc, outer arc back, closed at the start): for a
        /// wedge (R0 = 0) the inner arc collapses to the center. The first point is repeated at the end.
        /// </summary>
        public static void SectorOutline(List<Vector3> into, SectorGeom s, float lift = 0, float stepDeg = 3.75f)
        {
            float y = s.Y + lift;
            int start = into.Count;
            if (s.R0 <= 1e-4f) into.Add(new Vector3(0, y, 0));
            else SampleArc(into, s.R0, s.Theta0, s.Theta1, y, stepDeg);
            SampleArc(into, s.R1, s.Theta1, s.Theta0, y, stepDeg);
            into.Add(into[start]);
        }

        /// <summary>
        /// Appends the inner and outer edges of a band of a sector (radii r0 .. r1, angles theta0 .. theta1) as two
        /// matching point lists for SurfaceMeshBuilder.AddBand.
        /// </summary>
        public static void SampleBand(List<Vector3> inner, List<Vector3> outer, float r0, float r1, float theta0, float theta1,
            float y, float stepDeg = 3.75f)
        {
            int n = Segments(theta1 - theta0, stepDeg);
            for (int k = 0; k <= n; k++)
            {
                float t = Mathf.Lerp(theta0, theta1, k / (float)n);
                inner.Add(Polar(r0, t, y));
                outer.Add(Polar(r1, t, y));
            }
        }

        /// <summary>
        /// The radius at which a share f of a ring's area (from r0 outward to r1) is reached: sqrt(r0² + f (r1² − r0²)),
        /// the area-true strip boundary.
        /// </summary>
        public static float AreaRadius(float r0, float r1, double f) =>
            Mathf.Sqrt((float)(r0 * (double)r0 + System.Math.Max(0, System.Math.Min(1, f)) * (r1 * (double)r1 - r0 * (double)r0)));

        /// <summary>Whether an angle (degrees, any turn) lies within a sector's span.</summary>
        public static bool InSpan(float thetaDeg, float theta0, float theta1) =>
            LandMath.Wrap360(thetaDeg - theta0) <= theta1 - theta0;
    }
}
