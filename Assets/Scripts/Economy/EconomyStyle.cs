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

        /// <summary>
        /// The games' two moves, as the notebook draws them: cooperation light (the people's blue, whitened) and
        /// defection in the red pen.
        /// </summary>
        public static readonly Color Cooperate = new Color(0.78f, 0.86f, 1f);

        public static readonly Color Defect = new Color(1f, 0.24f, 0.2f);
        public static readonly Color People = GraphStyle.Humans;

        /// <summary>Wages: the people's blue, lighter (wage strips, wage arcs and patches on the land).</summary>
        public static readonly Color Wages = new Color(0.40f, 0.62f, 1.00f);

        /// <summary>Fantasy: white with a breath of rose (glitter over the rivers, mirages over the players).</summary>
        public static readonly Color Fantasy = new Color(1.00f, 0.95f, 0.97f);

        /// <summary>The state: pale steel (the government floor, taxes, transfers); the same hue as <see cref="Government"/>.</summary>
        public static readonly Color State = Government;

        /// <summary>A tie in a feud: neutral grey (defection is the light going out, never red: red is raw matter).</summary>
        public static readonly Color Feud = new Color(Land.LandStyle.FeudGrey, Land.LandStyle.FeudGrey, Land.LandStyle.FeudGrey);
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

        // ------------------------------------------------------------------ render queues

        public const int QueueWall = GraphMaterials.QueueLife;          // the industry wall: under the people
    }

    /// <summary>
    /// Highlight id ranges of the economy scene (all below 2^24, clear of the causality graph's ranges; see
    /// <see cref="GraphIds"/>). People share their lifeline ids everywhere they are drawn (a dot in the cut or in a
    /// player lights up with its lifeline). The land's ranges (80,000-95,000) follow the landscape spec's table (7.3).
    /// </summary>
    public static class EconomyIds
    {
        /// <summary>
        /// Industry bands of the wall, <see cref="IndustryParts"/> ids per industry so an industry is one contiguous range
        /// and a tier (whose industries are consecutive) is too: see <see cref="IndustryPart"/>.
        /// </summary>
        public const int Industry = 40_000;

        /// <summary>Ids per industry: wages, upkeep (production taxes and depreciation), owners' share, edge line.</summary>
        public const int IndustryParts = 4;

        public const int PartWages = 0, PartUpkeep = 1, PartOwners = 2, PartEdge = 3;

        /// <summary>Id of one part of an industry's band.</summary>
        public static int IndustryPart(int industry, int part) => Industry + industry * IndustryParts + part;

        /// <summary>Every id of a run of consecutive industries (one industry, a tier, the whole wall).</summary>
        public static IdRange Industries(int first, int last) =>
            new IdRange(IndustryPart(first, 0), IndustryPart(last, IndustryParts - 1));

        /// <summary>The line along the top of the wall.</summary>
        public const int WallTop = 42_000;

        // ------------------------------------------------------------------ the land (SPEC 7.3)

        /// <summary>Sectors: 8 ids per industry (<see cref="LandSector"/>).</summary>
        public const int LandSectorBase = 80_000, LandSectorParts = 8;

        /// <summary>Parts of a sector's id block.</summary>
        public const int SectorWages = 0, SectorUpkeep = 1, SectorOwners = 2, SectorEdge = 3, SectorPool = 4, SectorRootsIn = 5,
            SectorPatches = 6, SectorLabel = 7;

        public const int LandTierBase = 80_200, LandRootBase = 80_300, LandRootMax = 400, LandTowerBase = 80_700, LandCrown = 80_750,
            LandOverlayBase = 80_760;

        /// <summary>Players: 8 ids per player (<see cref="LandPlayer"/>), fewer than <see cref="LandPlayerMax"/> players.</summary>
        public const int LandPlayerBase = 81_000, LandPlayerParts = 8, LandPlayerMax = 1_000;

        /// <summary>Parts of a player's id block (1 and 6 are spare).</summary>
        public const int PlayerDisc = 0, PlayerHead = 2, PlayerMirage = 3, PlayerIncome = 4, PlayerRivulet = 5, PlayerLabel = 7;

        public const int LandRiverBase = 89_000, LandCapitalBase = 89_100, LandTieBase = 90_000, LandTieMax = 4_000,
            LandCoalitionBase = 94_000, LandCut = 95_000;

        /// <summary>Kinds of capital flows (<see cref="LandCapital"/>).</summary>
        public const int CapitalPayouts = 0, CapitalSaving = 1, CapitalInvestment = 2, CapitalCredit = 3, CapitalAbroad = 4;

        /// <summary>One part of an industry's sector (wages, upkeep, owners, edge, pool, roots in, patches, label).</summary>
        public static int LandSector(int industry, int part) => LandSectorBase + LandSectorParts * industry + part;

        /// <summary>Every id of a run of consecutive industries' sectors (one sector, a ring, the whole bowl).</summary>
        public static IdRange LandSectors(int first, int last) =>
            new IdRange(LandSector(first, 0), LandSector(last, LandSectorParts - 1));

        /// <summary>A ring's tread (tier 0 gov .. 4 tech).</summary>
        public static int LandTier(int tier) => LandTierBase + tier;

        /// <summary>A drawn root (k &lt; 400).</summary>
        public static int LandRoot(int k) => LandRootBase + k;

        /// <summary>A company's tower (capture index).</summary>
        public static int LandTower(int company) => LandTowerBase + company;

        /// <summary>An overlay (0 the AI scaffold, 1 the ads halo).</summary>
        public static int LandOverlay(int k) => LandOverlayBase + k;

        /// <summary>One part of a player's glyph (disc, head, mirage, income, rivulet, label).</summary>
        public static int LandPlayer(int player, int part) => LandPlayerBase + LandPlayerParts * player + part;

        /// <summary>
        /// Every id of a run of consecutive players: players are sorted by group first, so a group is one range (the 1%
        /// and business owners, players 0 .. n − 1, are <c>land:owners</c>).
        /// </summary>
        public static IdRange LandPlayers(int first, int last) =>
            new IdRange(LandPlayer(first, 0), LandPlayer(last, LandPlayerParts - 1));

        /// <summary>A river and its canal lane (0-5 categories, 6 taxes, 7 abroad).</summary>
        public static int LandRiver(int lane) => LandRiverBase + lane;

        /// <summary>A kind of capital flow (payouts, saving, investment, credit, abroad).</summary>
        public static int LandCapital(int kind) => LandCapitalBase + kind;

        /// <summary>A tie of the season (pair index &lt; 4,000).</summary>
        public static int LandTie(int pair) => LandTieBase + pair;

        /// <summary>A coalition (its matched number).</summary>
        public static int LandCoalition(int k) => LandCoalitionBase + k;
    }
}
