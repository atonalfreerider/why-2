using UnityEngine;

namespace Why.Economy
{
    /// <summary>
    /// A diagram that stands in plain world space beyond the present end of the economy timeline: the money
    /// circuit, the mind map, the games. Its local frame: x across the road (outward), y up from the ground,
    /// z along the road away from the past (a viewer standing on the road looks along +z). Geometry is built in
    /// local coordinates and drawn with raw materials (<see cref="GraphMaterials.Raw"/>) on a GameObject placed
    /// with <see cref="Origin"/> and <see cref="Rotation"/>; labels and anchors use <see cref="World"/>.
    /// </summary>
    public readonly struct Station
    {
        public readonly string Id;
        public readonly Vector3 Origin;
        public readonly Quaternion Rotation;

        public Station(string id, Vector3 origin, Quaternion rotation)
        {
            Id = id;
            Origin = origin;
            Rotation = rotation;
        }

        public Vector3 Right => Rotation * Vector3.right;
        public Vector3 Up => Rotation * Vector3.up;
        public Vector3 Forward => Rotation * Vector3.forward;

        /// <summary>World position of a local point of the station.</summary>
        public Vector3 World(Vector3 local) => Origin + Rotation * local;

        public Vector3 World(float x, float y, float z) => World(new Vector3(x, y, z));

        /// <summary>Yaw (degrees, 0 = +z) of a camera on the road looking along the station's +z.</summary>
        public float Yaw => Mathf.Atan2(Forward.x, Forward.z) * Mathf.Rad2Deg;

        /// <summary>Applies the frame to a transform (main thread).</summary>
        public void Place(Transform t) => t.SetPositionAndRotation(Origin, Rotation);
    }

    /// <summary>
    /// The economy scene's world layout: the timeline (one lens window shared by every view) and the stations
    /// beyond its present end. Pure math (no Unity objects), so layers can use it on worker threads.
    /// </summary>
    public static class EconomyStage
    {
        public const string Circuit = "circuit";
        public const string Mind = "mind";
        public const string Games = "games";

        static readonly string[] Order = { Circuit, Mind, Games };

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

        /// <summary>A station by id (<see cref="Circuit"/>, <see cref="Mind"/>, <see cref="Games"/>).</summary>
        public static Station Get(string id)
        {
            int i = System.Array.IndexOf(Order, id);
            if (i < 0) i = 0;
            Vector3 forward = RoadDirection();
            Vector3 now = OnRoad(DeepTime.NowYear, 0, EconomyStyle.FramingRho);
            now.y = 0;
            Vector3 origin = now + forward * (EconomyStyle.FirstStationGap + i * EconomyStyle.StationSpacing);
            return new Station(Order[i], origin, Quaternion.LookRotation(forward, Vector3.up));
        }
    }
}
