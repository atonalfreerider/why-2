using System.Collections.Generic;
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
    /// The semantic palette of the new views (spec 3.2, S02) is <see cref="Roles"/>: one colour per meaning, each an sRGB
    /// target converted to linear here. The layers move from their local colours and from the older fields of this class
    /// to the roles in WP15-WP18 (Tools/check-palette.py counts what is left); until then those fields keep their values, so
    /// no view changes colour before its layer is migrated.
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
        [System.Obsolete("Fantasy has no colour in the semantic palette (spec 3.2, EconomyStyle.Roles); WP23 removes it.")]
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
        [System.Obsolete("The motive blend is retired (spec 3.2): draw the two lanes of MotiveSplit instead; WP23 removes it.")]
        public static Color Motive(float fearShare)
        {
            fearShare = Mathf.Clamp01(fearShare);
            Color mid = new Color(0.92f, 0.86f, 0.86f);
            return fearShare < 0.5f
                ? Color.Lerp(Desire, mid, fearShare * 2f)
                : Color.Lerp(mid, Fear, (fearShare - 0.5f) * 2f);
        }

        /// <summary>
        /// Money spent with a mix of motives (spec 3.2), as two adjacent lanes across a band <paramref name="width"/> wide:
        /// the share spent toward a want in <see cref="Roles.Desire"/> and the share spent away from a fear
        /// (<paramref name="fear"/>, 0..1) in <see cref="Roles.Fear"/>, side by side and never blended into one hue. A mark too
        /// small for lanes draws the same shares as a two-part ring. Any thread.
        /// </summary>
        public static MotiveLanes MotiveSplit(float width, float fear) => new MotiveLanes(width, fear);

        // ------------------------------------------------------------------ the semantic palette (S02)

        /// <summary>
        /// The economy's semantic palette (spec 3.2, S02): one colour per meaning and only that meaning, each the sRGB target
        /// of the spec's table (owners #EFB146) converted to linear. A mesh's vertex colour, or the colour of a Why/Surface or
        /// Why/Line material at intensity 1 (an [HDR] colour property, which Unity passes to the shader as it is), shows its
        /// target where the post-processing leaves LDR colour as it is. Text and sprites decode their colours from sRGB in
        /// this linear-colour project, so a label's colour and its plate take <see cref="Encoded"/>. Marks that carry
        /// meaning draw the roles LDR and alpha-blended; direction and sign are arrowheads and position, never a hue; motive is
        /// a split (<see cref="MotiveSplit"/>), never a blend. PaletteCheck measures the touching pairs on rendered pixels
        /// under simulated deuteranopia and protanopia in the player's smoke test (<c>-smokeextra palette</c>), and
        /// Tools/check-palette.py reads the targets back from this class. Any thread.
        /// </summary>
        public static class Roles
        {
            /// <summary>Owners' income and claims: profit, dividends, rent, interest received, market value, ownership threads, net creditors.</summary>
            public static readonly Color Owners = Srgb(0xEFB146);

            /// <summary>People and pay: people, households, jobs, wages and salaries, synthetic lives.</summary>
            public static readonly Color People = Srgb(0x6796EE);

            /// <summary>Money spent toward a want; always beside <see cref="Fear"/>.</summary>
            public static readonly Color Desire = Srgb(0xEF5B98);

            /// <summary>Money spent away from a fear; always beside <see cref="Desire"/>, apart from it by lightness, so it needs no dash.</summary>
            public static readonly Color Fear = Srgb(0x79E4F0);

            /// <summary>Every government mark's flows and lines: taxes, benefits, public pay, purchases, public debt, military spending.</summary>
            public static readonly Color State = Srgb(0x546577);

            /// <summary>The state's base fill: the same slate as <see cref="State"/>, darker.</summary>
            public static readonly Color StateBase = Srgb(0x3A4654);

            /// <summary>The most-followed Americans, visibility, attention given.</summary>
            public static readonly Color Attention = Srgb(0xF5F5F5);

            /// <summary>
            /// Money between businesses, at home or across a border: purchases between industries, advertising, imports,
            /// exports, trade lanes.
            /// </summary>
            public static readonly Color Commerce = Srgb(0xC0B5AA);

            /// <summary>Tariffs, sanctions, export controls, war; always in a conflict form (a wall, a cut, a valve, a war zone).</summary>
            public static readonly Color Conflict = Srgb(0x864AD2);

            /// <summary>
            /// The Democratic Party (the politics view): its conventional blue, darker than <see cref="People"/>'s and bluer than
            /// <see cref="State"/>'s slate and <see cref="Conflict"/>'s violet, so it stays apart from all three under every
            /// vision Tools/check-palette.py measures (a lighter blue met people's, and a more violet one met conflict's).
            /// </summary>
            public static readonly Color Democratic = Srgb(0x3A4E98);

            /// <summary>The Republican Party (the politics view): its conventional red, apart from <see cref="Desire"/>'s rose.</summary>
            public static readonly Color Republican = Srgb(0xDC2626);

            /// <summary>Raw matter: M2's energy exporters only, and the universe scene. It never touches <see cref="Life"/> in the new views.</summary>
            public static readonly Color Matter = Srgb(0xCA4B20);

            /// <summary>Life: the universe scene only.</summary>
            public static readonly Color Life = Srgb(0x33903C);

            /// <summary>The year shown: the crest, tagged "the year shown".</summary>
            public static readonly Color YearShown = Srgb(0xC9CED6);

            /// <summary>No data: a total not split, a year not covered (the mark code's dashed outline).</summary>
            public static readonly Color Missing = Srgb(0x6B7280);

            /// <summary>Ink of labels and frames: text, dimmed text, rules and leaders, and the backing behind text (alpha .82).</summary>
            public static readonly Color InkText = Srgb(0xE8ECF2), InkDim = Srgb(0xA3ACBA), InkRule = Srgb(0x5A6372),
                InkBacking = Srgb(0x0A0E14, .82f);

            /// <summary>
            /// The tiers' warm stone, lighter higher: raw materials, manufacturing and infrastructure, services, tech (structure,
            /// not data; <see cref="Tier"/> reads it by the land's tier).
            /// </summary>
            public static readonly IReadOnlyList<Color> TierRamp =
                System.Array.AsReadOnly(new[] { Srgb(0x534C44), Srgb(0x69625A), Srgb(0x817970), Srgb(0x999188) });

            /// <summary>The classes' cool grey, lighter higher: the Fed DFA's six income groups, bottom 20% (0) to the top 1% (5).</summary>
            public static readonly IReadOnlyList<Color> ClassRamp = System.Array.AsReadOnly(new[]
                { Srgb(0x3F4953), Srgb(0x4F5964), Srgb(0x606A75), Srgb(0x717C87), Srgb(0x828E99), Srgb(0x94A0AC) });

            /// <summary>
            /// Series of self-contained charts only (the money chart, the mobility streams, the regime strata, the history wall,
            /// the life strip), each with a direct label.
            /// </summary>
            public static readonly IReadOnlyList<Color> Series =
                System.Array.AsReadOnly(new[] { Srgb(0xF08A4B), Srgb(0xE6CF6A), Srgb(0x4FB3E8), Srgb(0xB0B7C3) });

            /// <summary>A tier's fill: the stone of its step, and the state's base for the government (the land's tier 0).</summary>
            public static Color Tier(Land.Tier tier) => tier == Land.Tier.Gov ? StateBase : TierRamp[Mathf.Clamp((int)tier - 1, 0, 3)];

            /// <summary>
            /// A role as text and sprites take it: sRGB-encoded, its alpha kept (the dim ink back to #A3ACBA). TextMeshPro's
            /// labels and their plates decode their colours from sRGB to linear in this linear-colour project, so a role given
            /// to <see cref="LabelSpec.Color"/> or <see cref="LabelSpec.Plate"/> as it is would be decoded twice and draw
            /// darker (the dim ink as #5D697D). Vertex colours and the Why materials' colour take the role itself. Any thread.
            /// </summary>
            public static Color Encoded(Color role) =>
                new Color(LinearToSrgb(role.r), LinearToSrgb(role.g), LinearToSrgb(role.b), role.a);
        }

        /// <summary>An sRGB colour (0xRRGGBB, as the spec writes it) in linear, through the exact sRGB transfer function.</summary>
        static Color Srgb(int rgb, float alpha = 1f) =>
            new Color(SrgbToLinear((rgb >> 16) & 0xFF), SrgbToLinear((rgb >> 8) & 0xFF), SrgbToLinear(rgb & 0xFF), alpha);

        static float SrgbToLinear(int channel)
        {
            double s = channel / 255.0;
            return (float)(s <= 0.04045 ? s / 12.92 : System.Math.Pow((s + 0.055) / 1.055, 2.4));
        }

        /// <summary>The exact sRGB encoding of a linear value, clamped to 0..1 (the inverse of <see cref="SrgbToLinear"/>).</summary>
        static float LinearToSrgb(float linear)
        {
            double l = linear <= 0 ? 0 : linear >= 1 ? 1 : linear;
            return (float)(l <= 0.0031308 ? 12.92 * l : 1.055 * System.Math.Pow(l, 1 / 2.4) - 0.055);
        }

        // ------------------------------------------------------------------ the timeline (data space)

        /// <summary>
        /// The economy timeline: from this year to now, unrolled into a straight, nearly linear road (every
        /// view of the scene shares it, so the road never moves; only the camera does).
        /// </summary>
        public const double FirstYear = 1946;

        public const double WindowLogOffset = 2500;

        /// <summary>The road's length (world units) from <see cref="FirstYear"/> to now. With the hills it is the depth of
        /// the wave, whose land-local z is the road's time; the retired bowl used a 16-unit road.</summary>
        public static float WindowLength => EconomyLayouts.UseHills ? 160f : 16f;
        /// <summary>The road's cross-section scale (rho across, y up). Along the wave's 160-unit road the lifelines get a
        /// taller, wider cross-section so their rise and fall and the family junctions still read beside the wave.</summary>
        public static float RhoScale => EconomyLayouts.UseHills ? 5f : 2.5f;
        public static float YScale => EconomyLayouts.UseHills ? 7f : 3f;

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
    /// The two lanes of money spent with a mix of motives (<see cref="EconomyStyle.MotiveSplit"/>, spec 3.2): desire's lane
    /// along the band's left edge (looking along the flow), fear's along its right edge, touching, their widths adding up
    /// to the band's. A mark too small for lanes draws the same shares as a two-part ring: <see cref="DesireShare"/> of its
    /// turn in desire's colour, the rest in fear's.
    /// </summary>
    public readonly struct MotiveLanes
    {
        /// <summary>The band's whole width (world units, or pixels: whatever the caller measures it in).</summary>
        public readonly float Width;

        /// <summary>The share spent away from a fear (0..1; one that is not a number counts as none).</summary>
        public readonly float FearShare;

        public MotiveLanes(float width, float fear)
        {
            Width = float.IsNaN(width) || float.IsInfinity(width) ? 0 : Mathf.Max(0, width);
            FearShare = float.IsNaN(fear) ? 0 : Mathf.Clamp01(fear);
        }

        /// <summary>The share spent toward a want.</summary>
        public float DesireShare => 1f - FearShare;

        public float DesireWidth => Width * DesireShare;

        /// <summary>Fear's width: the rest of the band, so the two lanes cover it exactly.</summary>
        public float FearWidth => Width - DesireWidth;

        /// <summary>The centre of desire's lane, as an offset from the band's centre line toward its left edge.</summary>
        public float DesireOffset => 0.5f * (Width - DesireWidth);

        /// <summary>The centre of fear's lane, as an offset from the band's centre line toward its left edge (so negative).</summary>
        public float FearOffset => -0.5f * (Width - FearWidth);

        public Color DesireColor => EconomyStyle.Roles.Desire;
        public Color FearColor => EconomyStyle.Roles.Fear;
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
