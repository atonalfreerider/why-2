using UnityEngine;
using Why.Humans.Smv;

namespace Why.Economy.UI
{
    /// <summary>
    /// Where the N key puts a person on screen, as pure math (no Unity objects), so the controls and the harness share
    /// it: the direction of the past along the road at the person's point, and the camera pose that shows the point at
    /// a chosen place of the screen (<see cref="EconomyUiLayout.PersonFocus"/>: in the room the inspector leaves, moved
    /// toward the future so the life that led to the year shows).
    /// </summary>
    public static class PersonFraming
    {
        /// <summary>Years back along the road that give the direction of the past on screen.</summary>
        const double PastYears = 10;

        /// <summary>A framed point stays this share of the screen away from its edges (whatever the layout says).</summary>
        const float ScreenInset = 0.08f;

        /// <summary>
        /// The direction of the past on screen (unit; x right, y down) at a person's point under a warp and a camera
        /// pose: along the road's time axis at the point's height and offset (not along the line, which rises and falls
        /// with the person's fortunes). Past to the left when it cannot be measured.
        /// </summary>
        public static Vector2 PastDirection(SmvPopulation pop, int person, int year, WarpState warp, CameraPose pose)
        {
            Vector2 left = new Vector2(-1, 0);
            if (!LifelinePicker.StepAt(pop, person, year, out int step)) return left;
            SmvPerson p = pop.Sim.People[person];
            if (!pop.PointAt(p, step, out Vector3 data)) return left;
            double time = pop.Sim.TimeOf(step);
            Vector3 now = GraphWarp.ToWorld(data.x, data.y, data.z, warp);
            Vector3 past = GraphWarp.ToWorld(pop.U(time - PastYears), data.y, data.z, warp);
            Quaternion r = Quaternion.Euler(pose.Pitch, pose.Yaw, 0);
            Vector3 w = past - now;
            Vector2 s = new Vector2(Vector3.Dot(w, r * Vector3.right), -Vector3.Dot(w, r * Vector3.up));
            return s.sqrMagnitude > 1e-12f ? s.normalized : left;
        }

        /// <summary>
        /// Moves a pose (its target on a point) across the view plane so the point lands at a place of the screen
        /// (fractions: x from the left, y from the top) and keeps its distance, pitch and yaw. The point stays at the
        /// target's depth, so the shift is exact.
        /// </summary>
        public static CameraPose Frame(CameraPose pose, Vector2 screenFraction, float aspect)
        {
            float halfH = pose.Distance * Mathf.Tan(CameraRig.FieldOfView * 0.5f * Mathf.Deg2Rad);
            float halfW = halfH * aspect;
            Quaternion r = Quaternion.Euler(pose.Pitch, pose.Yaw, 0);
            pose.Target += r * Vector3.right * ((1 - 2 * screenFraction.x) * halfW) +
                           r * Vector3.up * ((2 * screenFraction.y - 1) * halfH);
            return pose;
        }

        /// <summary>
        /// The screen fraction (x from the left, y from the top) of a canvas point, clamped into the screen's inner part so
        /// a degenerate layout never throws the point off screen.
        /// </summary>
        public static Vector2 ToScreenFraction(Vector2 canvasPoint, Vector2 canvas)
        {
            return new Vector2(Mathf.Clamp(canvasPoint.x / Mathf.Max(1, canvas.x), ScreenInset, 1 - ScreenInset),
                Mathf.Clamp(canvasPoint.y / Mathf.Max(1, canvas.y), ScreenInset, 1 - ScreenInset));
        }
    }
}
