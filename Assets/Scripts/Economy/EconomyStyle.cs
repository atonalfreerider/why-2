using UnityEngine;

namespace Why.Economy
{
    /// <summary>
    /// Layout and palette of the economy scene. It keeps the causality graph's grammar where the two overlap
    /// (blue = people, red = matter, green = life) and adds one hue per new meaning:
    /// <list type="bullet">
    /// <item>gold = capital: corporations, profits, and the people who own the means to steer their own life;</item>
    /// <item>rose = desire: money spent moving toward what people want;</item>
    /// <item>ice = fear: money spent moving away from what people fear;</item>
    /// <item>pale steel = government.</item>
    /// </list>
    /// Raw industries keep their level color: oil, gas and metals are matter (red), agriculture is life (green).
    /// Everything else (text, axes, the UI) stays neutral grey.
    /// </summary>
    public static class EconomyStyle
    {
        // ------------------------------------------------------------------ palette (linear, LDR base)

        public static readonly Color Capital = new Color(1f, 0.70f, 0.18f);
        public static readonly Color CapitalDim = new Color(0.55f, 0.40f, 0.14f);
        public static readonly Color Desire = new Color(1f, 0.32f, 0.52f);
        public static readonly Color Fear = new Color(0.35f, 0.92f, 1f);
        public static readonly Color Government = new Color(0.72f, 0.76f, 0.84f);
        public static readonly Color People = GraphStyle.Humans;
        public static readonly Color Matter = GraphStyle.Matter;
        public static readonly Color Life = GraphStyle.Life;

        /// <summary>Color of an industry by its level ("matter", "life", "capital", "gov").</summary>
        public static Color Level(string level)
        {
            switch (level)
            {
                case "matter": return Matter;
                case "life": return Life;
                case "gov": return Government;
                default: return Capital;
            }
        }

        /// <summary>
        /// Color of money spent with a mix of motives: 0 = all desire (rose), 1 = all fear (ice), through a
        /// neutral warm white in between.
        /// </summary>
        public static Color Motive(float fearShare)
        {
            fearShare = Mathf.Clamp01(fearShare);
            Color mid = new Color(0.92f, 0.86f, 0.86f);
            return fearShare < 0.5f
                ? Color.Lerp(Desire, mid, fearShare * 2f)
                : Color.Lerp(mid, Fear, (fearShare - 0.5f) * 2f);
        }

        // ------------------------------------------------------------------ the timeline (data space)

        /// <summary>
        /// The economy timeline: from this year to now, unrolled into a straight, nearly linear road (every
        /// view of the scene shares it, so the road never moves; only the camera does).
        /// </summary>
        public const double FirstYear = 1946;

        public const double WindowLogOffset = 2500;
        public const float WindowLength = 16f;
        public const float RhoScale = 2.5f;
        public const float YScale = 3f;

        /// <summary>
        /// Data-space height of the ground the industry wall stands on, and of its top for the largest economy
        /// on the timeline (real GDP in 2025 dollars); the wall grows from the ground toward the people, whose
        /// lifelines start at <see cref="GraphStyle.HumansY"/>.
        /// </summary>
        public const float GroundY = 0.06f;

        public const float WallTopY = 0.62f;

        /// <summary>Gap (data rho) between the population's center line and the industry wall: the wall stands
        /// on the population's center line, beneath it.</summary>
        public const float WallRhoOffset = 0f;

        /// <summary>
        /// Data rho of the population's center for framing views before the population is built (the United
        /// States band of the civilization streams around 2000; layers use the simulation's real center).
        /// </summary>
        public const float FramingRho = 0.7f;

        // ------------------------------------------------------------------ the stations beyond the present

        /// <summary>
        /// World distance from the present end of the road to the first station (the money circuit), and between
        /// stations (circuit, mind, games) along the road's direction.
        /// </summary>
        public const float FirstStationGap = 6f;

        public const float StationSpacing = 12f;

        /// <summary>World size of a station's diagram (across, up).</summary>
        public const float StationWidth = 10f;

        public const float StationHeight = 5f;

        // ------------------------------------------------------------------ render queues

        public const int QueueWall = GraphMaterials.QueueLife;          // the industry wall: under the people
        public const int QueueThreads = GraphMaterials.QueueHumans + 10;  // money threads: over the people
        public const int QueueStations = GraphMaterials.QueueOverlay;   // diagrams beyond the present
    }

    /// <summary>
    /// Highlight id ranges of the economy scene (all below 2^24, clear of the causality graph's ranges; see
    /// <see cref="GraphIds"/>). People share their lifeline ids everywhere they are drawn (a point in the mind
    /// map lights up with its lifeline).
    /// </summary>
    public static class EconomyIds
    {
        /// <summary>Industry bands of the wall: + industry index (&lt; 1000).</summary>
        public const int Industry = 40_000;

        /// <summary>The profit (captured) part of an industry's band: + industry index.</summary>
        public const int IndustryProfit = 41_000;

        /// <summary>Tiers of the wall (government, raw, make, services, tech): + tier index.</summary>
        public const int Tier = 42_000;

        /// <summary>Money threads between the wall and the people: + kind (0 wages, 1 capital income, 2 transfers, 3.. spending categories).</summary>
        public const int Thread = 43_000;

        /// <summary>The money circuit: nodes + node index, links + link index.</summary>
        public const int CircuitNode = 50_000;

        public const int CircuitLink = 51_000;

        /// <summary>The mind map: spending categories, drives, neurochemicals, axes.</summary>
        public const int MindCategory = 60_000;

        public const int MindDrive = 60_500;
        public const int MindChemical = 60_800;
        public const int MindAxis = 60_900;

        /// <summary>The games: + element index.</summary>
        public const int Games = 70_000;
    }
}
