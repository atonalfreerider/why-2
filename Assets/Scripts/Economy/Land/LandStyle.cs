using UnityEngine;

namespace Why.Economy.Land
{
    /// <summary>
    /// Every constant of the land (SPEC sections 0-6): the unit system, the bowl's radii and heights, the looks of each
    /// element, the transition's timings, the money and the season's drawing. One place, so the layers of different work
    /// packages agree; worker-safe (plain values). Section numbers refer to the landscape spec and Docs/ECONOMY.md.
    /// </summary>
    public static class LandStyle
    {
        // ------------------------------------------------------------------ 0.1 the unit system (per GDP of the year)

        /// <summary>
        /// Area of the year's GDP (u²), world width of the year's GDP as a money flow and as a root (purchases between
        /// industries, drawn 4x wider: "roots x4"), height of the year's GDP as market value, and the plaza's distance past
        /// the road's end.
        /// </summary>
        public const float AreaGdp = 24f, WidthGdp = 1.0f, RootWidthGdp = 4.0f, HeightGdp = 6f, PlazaGap = 10f;

        /// <summary>Every dollar-coded line is at least this wide (px); a flat lane carries a center line this wide.</summary>
        public const float LineFloorPx = 0.8f;

        /// <summary>Below <see cref="PxFadeB"/> a dollar-coded line's alpha scales with its dollars, down to this.</summary>
        public const float FadeMinAlpha = 0.3f;

        // ------------------------------------------------------------------ 2.1 strata: rings, terraces, lip, canal, rim

        /// <summary>Inner and outer radius of each ring (gov is the floor disc), and each terrace's tread height.</summary>
        public static readonly float[] RingA = { 0f, 1.38f, 2.13f, 3.11f, 3.90f }, RingB = { 1.18f, 1.93f, 2.91f, 3.70f, 4.45f };

        public static readonly float[] TerraceY = { 0f, 0.35f, 0.70f, 1.05f, 1.40f };

        /// <summary>
        /// The lip's outer radius (the riser from the tech tread climbs to it), the canal zone, the rim's outer edge and
        /// the rim's height.
        /// </summary>
        public const float LipR = 4.65f, CanalR0 = 4.70f, CanalR1 = 5.08f, RimR = 6.15f, RimY = 1.75f;

        /// <summary>The rim's four rows of players: rungs 4-5, 3, 2, 0-1 (class: distance from the bowl).</summary>
        public static readonly float[] RimRows = { 5.20f, 5.47f, 5.74f, 6.01f };            // rungs 4-5, 3, 2, 0-1

        /// <summary>
        /// The ring radii were chosen once so the largest tier share of 1947-2026 fills its ring to this (fills exclude
        /// the gaps between sectors).
        /// </summary>
        public const float FillMax = 0.92f;

        /// <summary>Width of a riser between two treads (ring gaps) and its height (terrace step).</summary>
        public const float RiserWidth = 0.20f, TerraceStep = 0.35f;

        /// <summary>Treads: a translucent annulus per ring in the tier color.</summary>
        public const float TreadAlpha = 0.07f;

        public const int TreadSegments = 96;

        /// <summary>Risers: alpha, shaded by angle by (RiserShadeBase + RiserShadeCos cos(θ − 90°)) so the far wall reads lighter.</summary>
        public const float RiserAlpha = 0.10f, RiserShadeBase = 0.75f, RiserShadeCos = 0.25f;

        /// <summary>Inner and outer edge line of every tread.</summary>
        public const float TreadEdgePx = 1f, TreadEdgeAlpha = 0.5f;

        /// <summary>The bowl's outer wall at <see cref="RimR"/>: five bands of the tier colors.</summary>
        public const float OuterWallAlpha = 0.05f;

        /// <summary>The rim tread (people blue) and its edge lines.</summary>
        public const float RimAlpha = 0.04f, RimEdgePx = 1f;

        /// <summary>Angular gap between neighbouring sectors of a ring (rings 1-4; the gov floor has none).</summary>
        public const float SectorGapDeg = 1.5f;

        /// <summary>
        /// The strata's own colors (WP6): each tier's hue at low saturation, steel for government (gov, raw clay, make warm
        /// stone, services cool slate, tech lavender). Treads, risers, the outer wall, tread and strip edges, sector
        /// outlines and the tier labels take them, so gold is capital's alone: the owners' strips, the towers and plinths,
        /// the crown and the capital flows.
        /// </summary>
        static readonly Color[] tierTints =
        {
            new Color(0.72f, 0.76f, 0.84f), new Color(0.80f, 0.64f, 0.60f), new Color(0.76f, 0.73f, 0.67f),
            new Color(0.66f, 0.74f, 0.80f), new Color(0.76f, 0.71f, 0.88f)
        };

        /// <summary>
        /// The roots' colors by the supplier's tier (WP6): the tier hues, saturated and at a middle value (they are the roots
        /// view's story; the wide roots under the services ring overlap, and at the tints' near-white value they added up to
        /// a white band that buried the root labels), never gold: purchases between industries are not capital. Raw keeps
        /// matter red and life green.
        /// </summary>
        static readonly Color[] rootTints =
        {
            new Color(0.38f, 0.45f, 0.60f), new Color(0.66f, 0.30f, 0.24f), new Color(0.60f, 0.48f, 0.30f),
            new Color(0.22f, 0.46f, 0.60f), new Color(0.46f, 0.34f, 0.68f)
        };

        /// <summary>A tier's tint (0 gov .. 4 tech).</summary>
        public static Color TierTint(int tier) => tierTints[Mathf.Clamp(tier, 0, tierTints.Length - 1)];

        /// <summary>The color of a root from a supplier of a tier (0 gov .. 4 tech).</summary>
        public static Color RootTint(int tier) => rootTints[Mathf.Clamp(tier, 0, rootTints.Length - 1)];

        // ------------------------------------------------------------------ 2.2 sectors and their strips

        /// <summary>Strip looks: wages (light blue), upkeep (steel), owners (gold; raw: red / green; gov: steel).</summary>
        public const float WagesAlpha = 0.20f, UpkeepAlpha = 0.12f, OwnersAlpha = 0.45f, OwnersIntensity = 1.6f;

        /// <summary>Strip edges: concentric arcs.</summary>
        public const float StripEdgePx = 1f;

        // ------------------------------------------------------------------ 2.3 layout

        /// <summary>Barycentric ordering sweeps and offset scan passes, the scan's step (degrees), tech's run center.</summary>
        public const int OrderSweeps = 10, OffsetPasses = 4, OffsetStepDeg = 2;

        public const float TechOffsetDeg = 90f;

        /// <summary>The year of the input-output table the order and offsets are computed on (kept for every year).</summary>
        public const int LayoutYear = 2024;

        // ------------------------------------------------------------------ 2.4 roots

        /// <summary>Roots leave the supplier this far below its tread, travel this far (plus RootStep per rank) below the buyer's.</summary>
        public const float RootLeave = 0.06f, RootDepth = 0.25f, RootStep = 0.012f;

        /// <summary>Own-industry purchases: a closed loop this far below the tread.</summary>
        public const float RootBallDepth = 0.08f;

        /// <summary>A root's alpha, intensity (WP6: 1.2 bloomed the overlapping roots white) and pulses per unit.</summary>
        public const float RootAlpha = 0.35f, RootIntensity = 0.85f, RootPulsePerUnit = 0.6f;

        public const int RootPoints = 24;

        /// <summary>The roots labeled in the roots view (the largest).</summary>
        public const int RootLabels = 6;

        // ------------------------------------------------------------------ 2.5 pools

        /// <summary>The pool veil (neutral white-blue), its lift above the tread, the overflow rise per excess (capped).</summary>
        public static readonly Color PoolVeil = new Color(0.80f, 0.88f, 1.0f);

        public const float PoolAlpha = 0.18f, PoolLift = 0.012f, OverflowRise = 0.05f, OverflowMax = 2f;

        /// <summary>The two-tone shoreline (rose then ice), never blended.</summary>
        public const float ShorePx = 2f, ShoreIntensity = 1.6f;

        /// <summary>Pools labeled from this inflow ($B).</summary>
        public const double PoolLabelMinB = 300;

        // ------------------------------------------------------------------ 2.6 corporations

        /// <summary>Towers from this year (the company data are 2025's).</summary>
        public const int TowersFromYear = 2024;

        /// <summary>Tower walls fade from the base to the top; four edge lines; the top square's intensity is 1 + 2 × margin.</summary>
        public const float TowerAlphaBase = 0.18f, TowerAlphaTop = 0.04f, TowerEdgePx = 0.8f, TowerMarginIntensity = 2f;

        /// <summary>
        /// Arc between adjacent towers (beyond their half sides), the plinth's veil alpha, the side of a private company's
        /// outline (it has no net income to size it).
        /// </summary>
        public const float TowerGap = 0.06f, PlinthAlpha = 0.15f, PrivateTowerSide = 0.12f;

        // ------------------------------------------------------------------ 2.7 the crown

        /// <summary>The gold ring over the bowl's center.</summary>
        public const float CrownY = 2.90f, CrownR = 0.55f, CrownPx = 2f, CrownIntensity = 2.0f;

        public const int CrownSegments = 64;

        // ------------------------------------------------------------------ 2.8 overlays

        /// <summary>The AI scaffold: width, height per $B of capex, from this year.</summary>
        public const float AiWidth = 0.6f, AiHeightPerB = 0.004f;

        public const int AiFromYear = 2023;

        /// <summary>The ads halo stands this far outside the internet sector.</summary>
        public const float AdsHaloGap = 0.1f;

        // ------------------------------------------------------------------ 1.1 - 1.4 the cut and the transition

        /// <summary>The cut's frame: padding (data rho and y), line, intensity, veil.</summary>
        public const float CutPad = 0.15f, CutTopPad = 0.05f, CutPx = 1.5f, CutIntensity = 1.4f, CutVeilAlpha = 0.05f;

        /// <summary>The card: bar width across the road, intensity factor, gap between tiers.</summary>
        public const float CardWidth = 0.6f, CardIntensity = 1.4f, CardTierGap = 0.004f;

        /// <summary>Lifeline dots in the cut and in the players' discs.</summary>
        public const float DotPx = 2.6f, ChildDotPx = 1.6f, ChildDotAlpha = 0.35f;

        /// <summary>The overview's callouts from the frame to the bowl.</summary>
        public const float CalloutPx = 0.8f, CalloutAlpha = 0.18f;

        /// <summary>The road and the wall while the land is open (LandGroup.Road).</summary>
        public const float RoadDimAlpha = 0.35f;

        /// <summary>The cut slides to a new year in this time.</summary>
        public const float YearSlideSeconds = 0.4f;

        /// <summary>Snapshots kept (LRU) and the build budget (ms).</summary>
        public const int CacheYears = 4, BuildBudgetMs = 150;

        /// <summary>The morph clock: unfold (1x), fold (2x), the cross-fade of a year change, the emphasis easing rate (per s).</summary>
        public const float UnfoldSeconds = 2.4f, FoldSeconds = 1.2f, CrossFadeSeconds = 0.4f, EmphasisRate = 2.5f;

        /// <summary>The transition's phases (MorphTime, s): lift, lie back, curl, spin, dots, reveal.</summary>
        public const float LiftEnd = 0.40f, LieBackStart = 0.30f, LieBackEnd = 1.00f, CurlStart = 0.90f, CurlEnd = 1.70f,
            SpinStart = 1.50f, SpinEnd = 2.20f, DotsStart = 0.40f, DotsSpread = 0.90f, DotFlight = 0.6f, RevealStart = 2.20f;

        /// <summary>A folding land fades out in this much of the fold's time (s).</summary>
        public const float FoldRevealSeconds = 0.2f;

        /// <summary>The staircase's area factor while lying back (k(1) = 0.5) and the dots' flight lift.</summary>
        public const float StaircaseK = 0.5f, DotArcLift = 1.2f;

        // ------------------------------------------------------------------ 3.3 places

        /// <summary>Gap between neighbouring discs of a row, a plinth's extra radius, controllers' stacking step on a tower.</summary>
        public const float PackGap = 0.03f, PlinthPad = 0.04f, ControllerStep = 0.06f;

        /// <summary>The capital-only 1% sit on the crown ring at this angle + 360° × k / n (k by key): the first faces the road.</summary>
        public const float CrownSeatDeg = 270f;

        // ------------------------------------------------------------------ 3.4 the player glyph

        /// <summary>
        /// Disc radius per √(million people), dot heights (base and per unit of reason), head ring and mirage heights
        /// (base and per unit of fantasy share).
        /// </summary>
        public const float DiscK = 0.035f, DotY0 = 0.03f, DotYReason = 0.25f, HeadY = 0.32f, MirageY0 = 0.42f, MirageYFantasy = 0.30f;

        public const int DiscSegments = 32;

        public const float DiscPx = 1.2f, DiscIntensity = 1.2f, DiscVeilAlpha = 0.12f;

        /// <summary>Sunflower packing of the dots: outer radius share and the golden angle (radians).</summary>
        public const float DotSpread = 0.85f, GoldenAngle = 2.39996f;

        /// <summary>Children's dots stand on a ring this far outside the disc.</summary>
        public const float ChildRingGap = 0.02f;

        /// <summary>Spouse links, the OS line (reason 0.5), the head ring's radius share, the mirage's veil and rim.</summary>
        public const float SpouseAlpha = 0.3f, OsLineY = 0.155f, HeadRingScale = 0.8f, MirageAlpha = 0.06f, MirageRimAlpha = 0.25f;

        /// <summary>Standing marks: the alpha's second ring, the omega's dimmed head, the anti-alpha's tick.</summary>
        public const float AlphaRingScale = 1.4f, AlphaRingIntensity = 2f, OmegaHeadAlpha = 0.3f, AntiAlphaTick = 0.05f;

        /// <summary>Tribe dash counts: R dashed, I dotted (D solid, M dash-dot).</summary>
        public const int TribeDashes = 16, TribeDots = 32;

        // ------------------------------------------------------------------ 3.5 income arcs

        /// <summary>Arc apex lift above the higher end, and per unit of planar distance; capital arcs sag instead.</summary>
        public const float ArcLift = 0.25f, ArcSlope = 0.08f, CapitalSag = 0.2f;

        public const int ArcPoints = 20;

        /// <summary>Borrowing is dashed; capital income arcs and wage patches.</summary>
        public const float BorrowDashOn = 0.06f, BorrowDashOff = 0.04f, BorrowAlpha = 0.5f, CapitalArcAlpha = 0.6f, PatchAlpha = 0.30f;

        // ------------------------------------------------------------------ 4 rivers

        /// <summary>
        /// Smallest drawn stream ($B: arcs, patches, players' capital income), smallest root ($B, 2024 dollars), smallest
        /// sink (share of a category), the dollars below which a line fades.
        /// </summary>
        public const double StreamMinB = 5, RootMinB = 50, SinkMinShare = 0.01, PxFadeB = 20;

        /// <summary>The canal's height, the gap between lanes, and a rivulet's lift above the rim.</summary>
        public const float CanalY = 1.76f, LaneGap = 0.004f, RivuletLift = 0.01f, CreekPad = 0.02f;

        /// <summary>
        /// The canal's lanes from the lip outward, grouped by the notebook's brackets: BASE (necessities, collective),
        /// SELFISH (jeopardy, escapism), MATING (status, growth), then taxes and abroad. Lane ids: 0-5 the categories in
        /// EconomyData.CategoryIds order (necessities, escapism, jeopardy, status, growth, collective), 6 taxes, 7 abroad.
        /// </summary>
        public static readonly int[] CanalOrder = { 0, 5, 2, 1, 3, 4, 6, 7 };

        /// <summary>Lane ids of taxes and abroad.</summary>
        public const int LaneTaxes = 6, LaneAbroad = 7;

        /// <summary>Points of a rivulet edge and of a waterfall; falls are kept this far apart (degrees).</summary>
        public const int RivuletPoints = 8, WaterfallPoints = 6;

        public const float FallSeparationDeg = 10f;

        /// <summary>Distributaries run level this far inside the ring's outer edge, plus this much per category.</summary>
        public const float DistributaryInset = 0.05f, DistributaryStep = 0.035f;

        /// <summary>Monotone beds: allowed climb per point.</summary>
        public const float UphillTolerance = 1e-4f;

        /// <summary>Path smoothing: Chaikin passes after the Catmull-Rom spline.</summary>
        public const int ChaikinPasses = 2;

        /// <summary>Lanes: alpha-blended ribbons, intensity cap, pulse phase per unit.</summary>
        public const float LaneAlpha = 0.75f, LaneIntensity = 1.0f, LanePulsePerUnit = 0.8f;

        /// <summary>Glitter: one sparkle per this many $B of fantasy, its size, intensity and height range above the lane.</summary>
        public const double GlitterPerB = 5;

        public const float GlitterPx = 5f, GlitterIntensity = 2.6f, GlitterY0 = 0.04f, GlitterY1 = 0.24f;

        /// <summary>The canal's bracket labels stand at this angle.</summary>
        public const float BracketThetaDeg = 250f;

        /// <summary>The five tier names stand on the outer wall at this angle, at each terrace's height (7.3).</summary>
        public const float TierLabelThetaDeg = 250f;

        /// <summary>The abroad pour fades out over this length.</summary>
        public const float AbroadFade = 1.5f;

        /// <summary>
        /// The colors of fantasy (white-rose glitter, mirages) and of wages (light blue): the scene palette's
        /// (<see cref="EconomyStyle.Wages"/>, <see cref="EconomyStyle.Fantasy"/>), one definition.
        /// </summary>
        public static readonly Color Wages = EconomyStyle.Wages, Fantasy = EconomyStyle.Fantasy;

        // ------------------------------------------------------------------ 5 the season

        /// <summary>Ties: apex lift above the higher head and per unit of chord; points; width 0.8 + 1.6 m px.</summary>
        public const float TieLift = 0.30f, TieSlope = 0.10f, TieBasePx = 0.8f, TieMutualPx = 1.6f;

        public const int TiePoints = 12;

        /// <summary>Mutual cooperation above which a tie cooperates, below which it is a feud.</summary>
        public const float CooperateMutual = 0.36f, FeudMutual = 0.12f;

        /// <summary>A feud: neutral grey, the middle of the arc missing, dim.</summary>
        public const float FeudGrey = 0.35f, FeudGap = 0.30f, FeudAlpha = 0.35f, FeudIntensity = 0.6f;

        /// <summary>Coalition members within this angle on the rim form one run (one band; the bands' looks are SocialLayer's).</summary>
        public const float CoalitionRunDeg = 25f;

        /// <summary>Signals: pain flash and ring, relief pulse, satisfaction's intensity; the dimming of a selection.</summary>
        public const float PainSeconds = 0.5f, PainRing = 0.25f, ReliefSeconds = 1.0f, SatisfiedIntensity = 1.6f, SelectDim = 0.15f;

        /// <summary>A cooperating tie (m ≥ <see cref="CooperateMutual"/>): alpha 0.15 + 0.6 m, intensity 1 + m (5.5).</summary>
        public const float TieAlpha0 = 0.15f, TieAlphaMutual = 0.6f, TieIntensity0 = 1f, TieIntensityMutual = 1f;

        /// <summary>A pain flash's intensity, from this down to <see cref="PainIntensity1"/> over <see cref="PainSeconds"/>; the ring's alpha from this to 0.</summary>
        public const float PainIntensity0 = 3f, PainIntensity1 = 1f, PainRingAlpha = 0.6f;

        /// <summary>
        /// Signals (5.3): pain when a tie's cooperation (either direction) falls by at least <see cref="PainDrop"/> from the
        /// last round; relief when a feud (mutual &lt; <see cref="FeudMutual"/>) ends (mutual &gt; <see cref="ReliefEnd"/>).
        /// </summary>
        public const float PainDrop = 0.05f, ReliefEnd = 0.20f;

        /// <summary>Satisfaction: mutual ≥ <see cref="CooperateMutual"/> held this many rounds (5.3).</summary>
        public const int SatisfiedRounds = 12;

        /// <summary>A coalition member's rivulet-side disc ring is brightened by this factor (5.5).</summary>
        public const float CoalitionBrighten = 1.5f;

        /// <summary>The smallest coalition (CNM community) drawn and logged, in players (5.3).</summary>
        public const int CoalitionMinPlayers = 3;

        /// <summary>
        /// The betrayal's measures against the season without it (5.4): players hit = players with a tie differing by at
        /// least <see cref="HitDelta"/> at any round ≥ R; calm after = the last round with a tie differing by at least
        /// <see cref="CalmDelta"/>, minus R.
        /// </summary>
        public const float HitDelta = 0.05f, CalmDelta = 0.02f;

        /// <summary>Playback: rounds per second and the hold at the season's end (s).</summary>
        public const float RoundsPerSecond = 4f, SeasonHoldSeconds = 2f;

        /// <summary>A season's rounds; the betrayal view's incident round, held round and loop.</summary>
        public const int SeasonRounds = 96, IncidentRound = 48, BetrayalHoldRound = 50, BetrayalLoop0 = 40, BetrayalLoop1 = 60;

        /// <summary>Rounds at which coalitions are detected.</summary>
        public static readonly int[] Detections = { 0, 24, 48, 72, 96 };

        // ------------------------------------------------------------------ 6 the mind

        /// <summary>The mind view's thesis line (land-local) and the neurochemicals' offset above each fall.</summary>
        public static readonly Vector3 ThesisLocal = new Vector3(0, 3.2f, 6.2f), ChemicalOffset = new Vector3(0, 0.25f, 0);

        /// <summary>A "Higher OS" adult: reason at or above this.</summary>
        public const float HigherOsReason = 0.5f;

        // ------------------------------------------------------------------ render queues (land-local, raw materials)

        /// <summary>
        /// Render queues of the land, above the road (the wall 3100, the people 3200): the bowl under the roots under
        /// the pools, then towers, rivers, glyphs, arcs, ties, and the cut over everything.
        /// </summary>
        public const int QueueRoots = GraphMaterials.QueueOverlay + 10, QueueTerraces = QueueRoots + 2, QueueSectors = QueueRoots + 4,
            QueuePools = QueueRoots + 6, QueueTowers = QueueRoots + 8, QueueRivers = QueueRoots + 10, QueueGlyphs = QueueRoots + 12,
            QueueArcs = QueueRoots + 14, QueueTies = QueueRoots + 16, QueueSection = QueueRoots + 20;

        // ------------------------------------------------------------------ derived

        /// <summary>Mid-radius of a ring (gov: half the disc's radius).</summary>
        public static float RingMid(int tier) => 0.5f * (RingA[tier] + RingB[tier]);

        /// <summary>
        /// The angle (degrees) a ring spans per u² of area: an arc of the annulus, or a wedge of the gov floor disc
        /// (span = area / (B² / 2)).
        /// </summary>
        public static double DegreesPerArea(int tier)
        {
            double a = RingA[tier], b = RingB[tier];
            double perRadian = tier == 0 ? b * b / 2 : (b - a) * (a + b) / 2;
            return 180.0 / System.Math.PI / perRadian;
        }

        /// <summary>A dollar-coded line's alpha factor: dollars / <see cref="PxFadeB"/> below it, never under <see cref="FadeMinAlpha"/>.</summary>
        public static float DollarFade(double dollars) =>
            dollars >= PxFadeB ? 1f : Mathf.Max(FadeMinAlpha, (float)(dollars / PxFadeB));
    }

    /// <summary>
    /// The player glyph's geometry (3.4), shared by the glyphs, the transition (whose dots fly to these slots) and
    /// picking. Land-local; worker-safe.
    /// </summary>
    public static class Figure
    {
        /// <summary>A disc's radius for a number of people.</summary>
        public static float DiscRadius(double people) => LandStyle.DiscK * Mathf.Sqrt((float)(people / 1e6));

        /// <summary>The disc's center, land-local.</summary>
        public static Vector3 Center(Player p) => LandFrame.Polar(p.R, p.Theta, p.Y);

        /// <summary>
        /// Land-local position of a member's dot: slot k of n of the sunflower (radius 0.85 r √((k + 0.5) / n), angle
        /// 2.39996 k from the land's +x axis), at 0.03 + 0.25 × reason above the disc.
        /// </summary>
        public static Vector3 DotSlot(Player p, int k, int n, float reason)
        {
            float rr = LandStyle.DotSpread * p.Radius * Mathf.Sqrt((k + 0.5f) / Mathf.Max(1, n));
            float a = LandStyle.GoldenAngle * k;
            Vector3 c = Center(p);
            return new Vector3(c.x + rr * Mathf.Cos(a), c.y + LandStyle.DotY0 + LandStyle.DotYReason * Mathf.Clamp01(reason),
                c.z + rr * Mathf.Sin(a));
        }

        /// <summary>The head: the disc's center + (0, HeadY, 0).</summary>
        public static Vector3 Head(Player p) => Center(p) + new Vector3(0, LandStyle.HeadY, 0);

        /// <summary>Land-local position of a child's dot: slot k of n on the ring at r + 0.02, at the disc's height.</summary>
        public static Vector3 ChildSlot(Player p, int k, int n)
        {
            float a = 2 * Mathf.PI * (k + 0.5f) / Mathf.Max(1, n);
            float rr = p.Radius + LandStyle.ChildRingGap;
            Vector3 c = Center(p);
            return new Vector3(c.x + rr * Mathf.Cos(a), c.y + LandStyle.DotY0, c.z + rr * Mathf.Sin(a));
        }

        /// <summary>The mirage's height above the disc for a fantasy share of spending.</summary>
        public static float MirageY(float fantasyShare) => LandStyle.MirageY0 + LandStyle.MirageYFantasy * Mathf.Clamp01(fantasyShare);

        /// <summary>The mirage's radius: the player's fantasy dollars at the year's area scale.</summary>
        public static float MirageRadius(double fantasyB, float areaPerB) =>
            Mathf.Sqrt((float)System.Math.Max(0, fantasyB) * areaPerB / Mathf.PI);
    }
}
