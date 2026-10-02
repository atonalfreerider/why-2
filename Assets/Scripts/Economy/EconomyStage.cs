using UnityEngine;
using Why.Economy.Land;

namespace Why.Economy
{
    /// <summary>
    /// The economy scene's world layout: the timeline (one lens window shared by every view), the land on the plaza
    /// beyond its present end (<see cref="Land"/>). Pure math (no Unity objects), so layers can use it on worker threads.
    /// </summary>
    public static class EconomyStage
    {
        /// <summary>The lens every economy view uses: <see cref="EconomyStyle.FirstYear"/> to now on a straight road.</summary>
        public static WarpState TimelineWarp() => WarpState.Window(DeepTime.NowYear - EconomyStyle.FirstYear, 0,
            EconomyStyle.WindowLogOffset, EconomyStyle.WindowLength, 1, EconomyStyle.RhoScale, EconomyStyle.YScale);

        /// <summary>Clock arc of a calendar year on the economy timeline (as the lifelines are drawn).</summary>
        public static float U(double year) => Mathf.Max(DeepTime.Arc(DeepTime.NowYear - year), DeepTime.NowArc);

        /// <summary>World position of a data-space point under the timeline warp.</summary>
        public static Vector3 OnRoad(double year, float y, float rho) => GraphWarp.ToWorld(U(year), y, rho, TimelineWarp());

        /// <summary>Unit vector along the road toward the present (world, horizontal).</summary>
        public static Vector3 RoadDirection()
        {
            WarpState w = TimelineWarp();
            Vector3 a = GraphWarp.ToWorld(U(DeepTime.NowYear - 10), 0, EconomyStyle.FramingRho, w);
            Vector3 b = GraphWarp.ToWorld(U(DeepTime.NowYear), 0, EconomyStyle.FramingRho, w);
            Vector3 d = b - a;
            d.y = 0;
            return d.sqrMagnitude > 1e-10f ? d.normalized : Vector3.forward;
        }

        /// <summary>
        /// The land's frame (1.2): the bowl's center on the plaza <see cref="LandStyle.PlazaGap"/> units past the road's end
        /// (on the ground, y = 0), local +z along the road away from the past.
        /// </summary>
        public static LandFrame Land()
        {
            Vector3 forward = RoadDirection();
            Vector3 now = OnRoad(DeepTime.NowYear, 0, EconomyStyle.FramingRho);
            now.y = 0;
            return new LandFrame(now + forward * LandStyle.PlazaGap, Quaternion.LookRotation(forward, Vector3.up));
        }
    }
}
