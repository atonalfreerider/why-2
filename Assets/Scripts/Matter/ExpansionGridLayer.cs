using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Why.Matter
{
    /// <summary>
    /// A faint scale grid drawn on space itself beneath the RED layer: the expansion of space by 38 orders of magnitude
    /// from the Big Bang to the start of the biosphere, drawn as a pinwheel of lines around the clock that keeps
    /// subdividing with the same branching logic as the tree of life (<see cref="Why.Life.LifeLayer"/>).
    ///
    /// Lines: every grid line is a circumferential-to-radial curve, a member of one family indexed by its departure arc
    /// sigma*: rho = <see cref="Bend"/> sigma^2 / (sigma* - sigma) (<see cref="Grove.Radius"/>). Out of the cusp it hugs
    /// the clock (rho ~ sigma^2 / sigma*: flat, then bending outward ever more steeply) and at sigma* it is exactly
    /// radial and shoots straight away from the clock's centre, so every line is the same hook at the scale of its
    /// departure: tiny out of the cusp, wide later (ever expanding and scaling space). The family is ordered by sigma*
    /// at every arc length (an earlier departure always lies farther out), so lines are level sets that can never cross,
    /// however many generations are packed between two of them. The radial tail is drawn to a cap (<see cref="TailReach"/>
    /// times the red envelope) and fades to nothing on the way: the fan has no border. Our track (rho = 0, sigma* at
    /// infinity) never departs: it is the inside spiral the whole fan follows.
    ///
    /// Departures spiral around the clock: <see cref="BigBangLines"/> lines are born at the Big Bang itself with
    /// departures log-spaced from just after the cusp (<see cref="FirstDeparture"/>: the outermost, nearly radial at
    /// once) to the biosphere (the innermost, departing last). Orders of magnitude: arc k (1 .. <see cref="Orders"/>) at
    /// arc length sigma_k, geometrically spaced from <see cref="FirstArcSigma"/> to the biosphere, one per order of
    /// magnitude of expansion. At every arc, in every gap between neighbouring running lines (our track counts as the
    /// innermost) that has opened wider than <see cref="BirthGap"/>, a new line is born with its departure at the
    /// geometric mean of its neighbours' (<see cref="Grove.Grow"/>), which places it at the log-midpoint of the gap
    /// running parallel to the flow of the bundle (along the clock, deep in the bundle) and fading in over a share of
    /// the stretch to the next arc: the departure angles keep subdividing and the space between is filled in the same
    /// recursive pattern, again and again. A line is only born while it still runs along the clock
    /// (<see cref="BirthSlope"/>): the fan of spokes beyond the bundle is space that has already expanded, not a nursery.
    /// Each contained line stands for a scale one order of magnitude down from the lines that contain it
    /// (<see cref="GridLine.Depth"/>), so the hierarchy has many levels and many lines: the count is capped only by
    /// what the finest level of detail can resolve (<see cref="BirthGap"/>) and the vertex budget. A line whose
    /// departure would fall past the biosphere never departs: it eases into parallel there (<see cref="SettleLength"/>),
    /// and from the biosphere on nothing is born and nothing departs; the remaining bundle runs parallel to the handoff
    /// and dissolves ahead of the human branch.
    ///
    /// Rungs: at every order-of-magnitude arc, short dashes across the gaps between neighbouring lines (within each
    /// tier), light, hidden where the arcs or the lines crowd within a few pixels (never a solid block at the cusp).
    ///
    /// Look and level of detail: a line's weight follows the space around it (its gap to its neighbours at the same arc
    /// length; along a tail, the spacing of the spokes). As it splays out it grows thicker and fainter with that space
    /// (<see cref="LineWidthPerGap"/>, <see cref="Fainter"/>: roughly constant ink), so the outer spokes are broad, soft
    /// and faint and the contained lines thin and crisp; tails fade outward. Every line has a scale: how far its
    /// departure lies from its neighbours' at birth (<see cref="GridLine.Spacing"/>; the space between two lines is
    /// proportional to it everywhere, in the bundle and along the tails), which sorts the generations into
    /// <see cref="Tiers"/> classes of size (<see cref="CrowdGap"/> at the overview, then <see cref="TierRatio"/> finer
    /// each). Within a class only lines of that class or coarser count as neighbours, so each class is a complete,
    /// consistent grid at every arc length. The meshes are the same <see cref="Tiers"/> bands of space: every stretch
    /// of every line (and every rung) is drawn into the mesh of the space it has there, crossfading into the next finer
    /// mesh over the lower part of the band (<see cref="Share"/>), and <see cref="Tick"/> fades a mesh in once its band
    /// spans a few pixels on screen and lets it recede again as finer ones come in. So a line shows exactly where its
    /// space is resolved at the current zoom: the overview shows the coarse pinwheel of the Big Bang's lines and their
    /// first generations, with finer spokes emerging out of the expanding space between them as it opens far out, and
    /// every closer view reveals the finer contained generations deeper in the bundle while the coarse lines become the
    /// broad, faint background: constant new grid lines at every zoom, none crowding into a block.
    /// Scaffolding, not content: neutral grey (hue belongs to the levels), low alpha, far below the bloom threshold,
    /// drawn beneath the matter surfaces so the red veils it, and dimmed where it reaches beyond the red envelope into
    /// open space, where nothing veils it. The grid is schematic (the clock is not physical time, and space never
    /// stopped expanding); the anchor's blurb tells the real expansion. It builds its own <see cref="MatterLayout"/> (the
    /// Big Bang's arc and the envelope) because it prepares concurrently with <see cref="MatterLayer"/>.
    /// </summary>
    public sealed class ExpansionGridLayer : GraphLayer
    {
        /// <summary>Anchor of the grid: tooltips of its labels and the director.</summary>
        public const string AnchorKey = "matter:_expansion";

        /// <summary>
        /// Highlight id of the grid: the last matter id (<see cref="GraphIds.Matter"/> 998), above the bands and
        /// the epochs, which <see cref="MatterLayer"/> numbers upward from index 100 (a few dozen of them).
        /// </summary>
        public static readonly int GridId = GraphIds.Matter(998);

        // --- the clock stretch of the grid ---
        /// <summary>Years ago at which the biosphere starts: the last departure, the last arc, the last birth.</summary>
        public const double BiosphereYearsAgo = 4.0e9;

        /// <summary>Orders of magnitude: one arc each, from the Big Bang to the biosphere.</summary>
        public const int Orders = 38;

        /// <summary>Arc length (world units) of the first arc out of the cusp; the arcs are geometric from here to the biosphere.</summary>
        const float FirstArcSigma = 0.01f;

        // --- the lines ---
        /// <summary>Lines born at the Big Bang itself, their departures log-spaced from <see cref="FirstDeparture"/> to the biosphere.</summary>
        const int BigBangLines = 8;

        /// <summary>Departure arc length of the outermost Big Bang line: nearly radial straight out of the cusp.</summary>
        const float FirstDeparture = 0.02f;

        /// <summary>
        /// Amplitude of the family, rho = Bend sigma^2 / (sigma* - sigma): at 0.5 a line has risen a quarter of its
        /// departure arc length halfway there and turns through 45 degrees about two thirds of the way.
        /// </summary>
        const float Bend = 0.5f;

        /// <summary>
        /// A gap between neighbouring lines bears a new line at an arc once it has opened this wide (world units): what
        /// the finest tier can just resolve (<see cref="TierGap"/> of <see cref="Tiers"/> - 1), so every line that is
        /// born can be seen somewhere.
        /// </summary>
        const float BirthGap = 0.00044f;

        /// <summary>
        /// A line is only born where its outward slope (rho per unit along the clock) is still below this: it starts out
        /// running along the clock and bends out later (it departs at least 2.4 times farther along than its birth).
        /// </summary>
        const float BirthSlope = 1f;

        /// <summary>
        /// Least ratio between a new line's departure and either neighbour's: the departure angles subdivide until the
        /// spokes are this close in arc length, then the gap is full.
        /// </summary>
        const float MinDepartRatio = 1.001f;

        /// <summary>
        /// A line born next to our track departs this many times farther along the clock than the line outside it
        /// (the ladder of departures continues inward past the innermost Big Bang line, beyond the biosphere).
        /// </summary>
        const float TrackDepartRatio = 2f;

        /// <summary>A line that never departs is only born where its outward slope at the biosphere stays below this (rho per unit along the clock): it settles, it does not shoot.</summary>
        const float MaxSettleSlope = 2f;

        /// <summary>
        /// A line still bending at the biosphere eases into parallel there: its outward slope decays over this many
        /// world units along the clock (e-folding), so the bundle settles without a corner.
        /// </summary>
        const float SettleLength = 0.5f;

        /// <summary>Most lines in all (a guard; the birth gap keeps them under it).</summary>
        const int MaxLines = 6000;

        /// <summary>Most vertices over all tiers (32-bit indices; the finest, latest lines are dropped first) ...</summary>
        const int MaxVertices = 600_000;

        /// <summary>... of which this many are kept for the rungs (the finest rungs are dropped first).</summary>
        const int RungVertices = 120_000;

        // --- the radial tails ---
        /// <summary>A departing line is drawn out to this many times the red envelope at its departure ...</summary>
        const float TailReach = 2.5f;

        /// <summary>... at least this far (data units of rho: the cusp's brush of spokes) and at most <see cref="TailCap"/>.</summary>
        const float TailFloor = 1f;

        const float TailCap = 60f;

        /// <summary>A tail fades from this fraction of its reach to nothing at the cap: the spokes fan out to infinity, no border.</summary>
        const float TailFadeStart = 0.3f;

        /// <summary>Outward slope (world, rho per unit along the clock) at which a line leaves the bundle and its tail is sampled by rho.</summary>
        const float ExitSlope = 3f;

        // --- tiers: bands of space (world units), each a mesh of its own; a line's scale (how far its departure lies from
        // its neighbours') sorts it into the same bands as a class, which decides its neighbours ---
        /// <summary>Bands of space; tier 0 is the overview's, the last holds lines ~110 times finer (a 110x zoom).</summary>
        public const int Tiers = 7;

        /// <summary>Scale of tier 0 (world units): ~3 px at the overview distance. Geometry with less space around it belongs to a finer tier, so lines never pile up into a solid block.</summary>
        const float CrowdGap = 0.05f;

        /// <summary>Each finer tier holds space this many times finer than the one before.</summary>
        const float TierRatio = 2.2f;

        /// <summary>
        /// A stretch of a line whose space has just entered a tier's band is shared with the next finer tier, fading
        /// over into its own as its space grows by this ratio (below <see cref="TierRatio"/>): the seams between the
        /// meshes are invisible while both are on, and a line whose finer stretches are off at the current zoom fades
        /// out softly where its space drops below what the zoom resolves.
        /// </summary>
        const float EmergeRatio = 1.4f;

        /// <summary>A tier fades in as its scale grows from <see cref="TierOffPx"/> to this many pixels on screen ...</summary>
        const float TierOnPx = 3f;

        /// <summary>... and is gone below this many.</summary>
        const float TierOffPx = 1.5f;

        /// <summary>
        /// Once the next finer tier is fully in (this tier's scale spans TierOnPx x TierRatio pixels), a tier recedes as
        /// (pixels / that)^-Recede: zoomed in ten times past its own scale it is at a third, the broad, faint background
        /// of the crisp lines of the moment.
        /// </summary>
        const float Recede = 0.5f;

        /// <summary>Opacity falls as (space / scale)^-Fainter: a line whose space has opened 100 times beyond the scale it was born into is 10 times fainter (and, until the width cap, 100 times wider: its ink grows only as the square root of its space).</summary>
        const float Fainter = 0.5f;

        /// <summary>
        /// Within a tier, each generation (level) below the tier's shallowest is this much fainter: the pinwheel's own
        /// spokes are the boldest, the lines that formed between them lighter, the ones between those lighter still,
        /// like the marks of a ruler, at every zoom.
        /// </summary>
        const float LevelFade = 0.8f;

        /// <summary>World width of a line per unit of its space (on top of <see cref="WidthPx"/>): a line 30 px from its neighbours is ~3 px wide, the old spokes far out broader still.</summary>
        const float LineWidthPerGap = 0.06f;

        /// <summary>World width of a rung per unit of the smaller side of its cell.</summary>
        const float StrokeWidthPerGap = 0.012f;

        /// <summary>World widths are capped here (world units: ~3.5 px at the overview, ~11 px at the earth preset), so the far spokes stay soft lines, not bars.</summary>
        const float MaxWidthWorld = 0.06f;

        const float WidthPx = 1f;

        /// <summary>
        /// A newborn line fades in over this fraction of the stretch to the next arc, and its neighbours feel its
        /// presence (their gaps halve) over the same stretch: nothing pops at an arc.
        /// </summary>
        const float BirthRamp = 0.6f;

        /// <summary>Samples along a line's ramp (its steps are capped to this share of the ramp, so the fade is smooth).</summary>
        const int RampSamples = 8;

        /// <summary>Widest space a line can have to a neighbour still running along the clock (world units; a guard against a neighbour shooting off).</summary>
        const float OpenGap = 1000f;

        // --- rungs ---
        /// <summary>Weight of the rungs: quieter than the lines, so the arcs read as marks between them.</summary>
        const float StrokeWeight = 0.35f;

        /// <summary>A rung spans a gap up to this many times the arcs' own spacing there; taller cells are being torn open by a departure.</summary>
        const float RungReach = 4f;

        /// <summary>Fraction of a cell left open at either end of a rung, where the arc crosses a line.</summary>
        const float DashGap = 0.12f;

        // --- opacity: the material carries the grid's opacity, vertices a 0..1 profile (full byte precision) ---
        /// <summary>Opacity of the grid at full weight (before the level of detail and the fades).</summary>
        const float GridAlpha = 0.4f;

        /// <summary>HDR multiplier of the neutral tone: ~0.48 at most, far below the bloom threshold.</summary>
        const float GridIntensity = 0.7f;

        /// <summary>Radial e-folding of the material's fade (data units of rho): the far spokes dissolve into the dark.</summary>
        const float GridRhoFade = 30f;

        /// <summary>
        /// Opacity of the grid out in open space, beyond the red envelope, relative to inside it: there no red veils
        /// it (the envelope's fill lies over the grid), so it dims to keep the same weight on the dark and does not
        /// outshine the matter it frames.
        /// </summary>
        const float OpenSpaceAlpha = 0.45f;

        /// <summary>... reached this far beyond the envelope's edge (fraction of the envelope's width), where its fill has faded out.</summary>
        const float OpenSpaceSpan = 0.6f;

        /// <summary>
        /// Weight of the settled bundle, relative to the fan: from the biosphere on the story's scale is earthly, so the
        /// parallel lines recede (reached over <see cref="SettleSpan"/>), leaving the late red layer and the life layer
        /// above it unstriped.
        /// </summary>
        const float SettledWeight = 0.3f;

        /// <summary>World units along the clock after the biosphere over which the grid recedes to <see cref="SettledWeight"/>.</summary>
        const float SettleSpan = 1.5f;

        /// <summary>Geometry fainter than this (vertex alpha after the radial fade) is left out.</summary>
        const float CullAlpha = 0.01f;

        /// <summary>A line is split into separate polylines where it is invisible for at least this many samples in a tier.</summary>
        const int SplitSamples = 4;

        /// <summary>
        /// The grid dissolves ahead of the human branch as HandoffFade^this on top of the material's own handoff
        /// fade: the bundle is gone well before the branch.
        /// </summary>
        const float HandoffExponent = 2f;

        // --- sampling: a line is walked in world steps of RelStep times its distance out of the cusp (at most MaxStep,
        // at least MinStep), turning at most TurnDegrees per step; its tail is walked in rho ---
        const float RelStep = 0.05f;
        const float MinStep = 5e-4f;

        /// <summary>Largest step in the bundle (the turn limit takes over in the bends; along the clock the chord error is well under a pixel).</summary>
        const float MaxStep = 0.1f;

        /// <summary>Largest step once the lines run parallel (they only bend with the clock: a third of a pixel of chord error at the overview).</summary>
        const float SettledStep = 0.25f;

        /// <summary>Samples of the red envelope's width over the clock (looked up per point instead of evaluating the cosmic scale factor).</summary>
        const int EnvelopeSamples = 2048;

        const float TurnDegrees = 4f;
        const int TurnHalvings = 5;

        /// <summary>A tail is sampled every TailRel of its radius (it is straight: rho along the normal), at least <see cref="MinStep"/>.</summary>
        const float TailRel = 0.18f;

        /// <summary>Newton steps inverting a line's rise for a tail sample (its rise is convex in the arc length).</summary>
        const int TailNewton = 6;

        /// <summary>Most points on one line (a guard: the steps above give a few hundred).</summary>
        const int MaxPoints = 2048;

        // --- labels ---
        /// <summary>Arcs that carry an order-of-magnitude label ...</summary>
        static readonly int[] LabelOrders = { 38, 30, 20, 10 };

        /// <summary>... this fraction of the arc length from the Big Bang outward: at the top of the bundle, below the spokes.</summary>
        const float LabelRhoFrac = 0.35f;

        /// <summary>Radius of the "our world's scale" label at the biosphere, just outside our track (data units).</summary>
        const float WorldLabelRho = 0.05f;

        const float OrderPriority = 2.5f;
        const float WorldPriority = 2f;
        const float LabelSize = 11f;

        /// <summary>Line ids standing for our track (rho = 0, never departs) and for no line at all.</summary>
        public const int Track = -1, None = -2;

        const string Blurb =
            "A schematic grid on space itself, grown like the tree of life. Our track is our world's scale, and space " +
            "opens outward from it: every grid line starts out running along the clock and bends outward until it points " +
            "straight away from it, splaying off to infinity; each arc across the bundle marks another order of magnitude " +
            "of expansion, 38 of them from the Big Bang to the start of the biosphere, about as many as separate the " +
            "Planck length, the smallest scale current physics can describe, from our world's. At every one, wherever " +
            "the space between two lines has opened wide enough, a new line forms between them, one order of magnitude " +
            "finer than the lines around it, the way new lineages branch off in the tree of life, and it too bends " +
            "outward in its turn: the grid keeps dividing as space expands, finer lines inside finer lines, the closer " +
            "you look. The real expansion was not so even: inflation stretched space about 10^26-fold in roughly 10^-32 " +
            "seconds, space has grown about 1,100-fold since the cosmic microwave background was released 380,000 years " +
            "after the Big Bang, and today the expansion is accelerating. The grid settles into parallel lines at the " +
            "start of the biosphere only as a visual cue that the story's scale shifts from the cosmic to the earthly: " +
            "space itself has never stopped expanding.";

        public override int Order => 6;
        public override IEnumerable<string> RequiredTexts => new[] { MatterLayer.DataPath };

        /// <summary>The clock stretch of the grid: arc lengths from the Big Bang to the biosphere and to the end of the red layer (plain math, safe on any thread).</summary>
        public readonly struct Clock
        {
            /// <summary>Clock arc of the Big Bang, the cusp every line starts from.</summary>
            public readonly float BigBangArc;

            /// <summary>Clock arc of the start of the biosphere, where the fan holds still.</summary>
            public readonly float BiosphereArc;

            /// <summary>Arc length along the clock from the Big Bang to the biosphere (world units).</summary>
            public readonly float BiosphereSigma;

            /// <summary>Arc length from the Big Bang to the end of the red layer (the handoff).</summary>
            public readonly float EndSigma;

            public Clock(float bigBangArc)
            {
                BigBangArc = bigBangArc;
                BiosphereArc = Mathf.Min(DeepTime.Arc(BiosphereYearsAgo), bigBangArc - 1e-3f);
                BiosphereSigma = (bigBangArc - BiosphereArc) * GraphWarp.BasePath.SigmaPerArc;
                EndSigma = (bigBangArc - MatterLayout.EndArc) * GraphWarp.BasePath.SigmaPerArc;
            }

            /// <summary>Arc length along the clock from the Big Bang at arc u (world units; the circle part of the base path).</summary>
            public float Sigma(float u) => (BigBangArc - u) * GraphWarp.BasePath.SigmaPerArc;

            /// <summary>Arc at arc length s from the Big Bang (inverse of <see cref="Sigma"/>).</summary>
            public float ArcOf(float s) => BigBangArc - s / GraphWarp.BasePath.SigmaPerArc;
        }

        /// <summary>
        /// A line of the grid: the member of the family with departure <see cref="Depart"/> (its vertical asymptote),
        /// drawn from arc length <see cref="Born"/> on (<see cref="Grove.Radius"/>). A line whose departure lies past the
        /// biosphere never departs: it eases into parallel there.
        /// </summary>
        public struct GridLine
        {
            /// <summary>Arc length where it is born (0: the Big Bang) and where it departs (its vertical asymptote).</summary>
            public float Born, Depart;

            /// <summary>The line inside it at birth (<see cref="Track"/>: our track).</summary>
            public int Parent;

            /// <summary>The order-of-magnitude arc it is born on (0: the Big Bang).</summary>
            public int Arc;

            /// <summary>Its level: one more than the deeper of the two lines it was born between (0: out of the Big Bang). It stands for a scale 10^-Depth of theirs.</summary>
            public int Depth;

            /// <summary>
            /// Its scale (world units): the arc length between its departure and the nearer of its neighbours' at birth,
            /// which its space is proportional to along its bundle run and its tail; for a line that never departs, the
            /// space that spacing opens by the biosphere (<see cref="Grove.ScaleOf"/>) ...
            /// </summary>
            public float Spacing;

            /// <summary>... and its class: the tier that scale falls in (the coarsest whose threshold it reaches; 0 for the Big Bang's lines). Only lines of its class or coarser are its neighbours.</summary>
            public int Tier;
        }

        /// <summary>A rung: the dash of arc <see cref="Arc"/> across the gap between two lines neighbouring in class <see cref="Tier"/> (<see cref="Track"/>: our track).</summary>
        public struct Rung
        {
            public int Arc, Inner, Outer, Tier;
        }

        /// <summary>
        /// The lines, grown arc by arc (plain data, safe on any thread): their geometry and, for every stretch between
        /// two arcs, the running lines inner to outer (the neighbours of every line at every arc length).
        /// </summary>
        public sealed class Grove
        {
            /// <summary>The lines, in the order they were born (the Big Bang's first, inner to outer).</summary>
            public readonly List<GridLine> Lines = new List<GridLine>(1024);

            /// <summary>
            /// The running lines of each class (its own and the coarser ones: its neighbours), inner to outer, after the
            /// births of arc k, valid from arc k to arc k + 1: Running[t][Start[t][k] .. Start[t][k + 1]) (k = 0: out of
            /// the Big Bang, before the first arc). A line stays listed until the first arc past its departure. The finest
            /// class's list holds every line.
            /// </summary>
            public readonly List<int>[] Running = new List<int>[Tiers];

            public readonly int[][] Start = new int[Tiers][];

            /// <summary>Arc length of arc k (index 1 .. Orders; 0 is the Big Bang).</summary>
            public readonly float[] ArcSigma = new float[Orders + 1];

            /// <summary>The rungs of every arc, tier by tier, inner to outer.</summary>
            public readonly List<Rung> Rungs = new List<Rung>(16384);

            /// <summary>Lines born on arc k.</summary>
            public readonly int[] Births = new int[Orders + 1];

            /// <summary>Departure of the last line to have departed before stretch k (its tail is the space beyond the outermost running line); 0 while none has.</summary>
            public readonly float[] LastDeparture = new float[Orders + 2];

            /// <summary>Deepest level of any line, and the shallowest in each tier.</summary>
            public int MaxDepth;

            public readonly int[] MinDepth = new int[Tiers];

            readonly Clock clock;

            Grove(Clock clock)
            {
                this.clock = clock;
                for (int t = 0; t < Tiers; t++)
                {
                    Running[t] = new List<int>(4096);
                    Start[t] = new int[Orders + 2];
                    MinDepth[t] = int.MaxValue;
                }
            }

            /// <summary>Radius of line <paramref name="id"/> at arc length s (infinite from its departure on).</summary>
            public float Rho(int id, float s) => Radius(Lines[id].Depart, s);

            /// <summary>Outward slope d rho / d s of line <paramref name="id"/> at arc length s.</summary>
            public float Slope(int id, float s) => RadiusSlope(Lines[id].Depart, s);

            /// <summary>Radius of a neighbour (<see cref="Track"/>: 0, <see cref="None"/>: infinite) at arc length s.</summary>
            public float NeighbourRho(int id, float s) => id == Track ? 0 : id == None ? float.PositiveInfinity : Rho(id, s);

            /// <summary>
            /// The family: radius at arc length s of the line departing at <paramref name="depart"/>, Bend s^2 / (depart - s),
            /// decreasing in the departure at every s (lines never cross). A line departing past the biosphere follows the
            /// hyperbola to the biosphere, then its slope eases away into parallel.
            /// </summary>
            public float Radius(float depart, float s)
            {
                if (depart <= clock.BiosphereSigma)
                {
                    if (s >= depart) return float.PositiveInfinity;
                    return Bend * s * s / (depart - s);
                }

                float sb = Mathf.Min(s, clock.BiosphereSigma);
                float rho = Bend * sb * sb / (depart - sb);
                if (s <= clock.BiosphereSigma) return rho;
                return rho + RadiusSlope(depart, clock.BiosphereSigma) * SettleLength * (1 - Mathf.Exp(-(s - clock.BiosphereSigma) / SettleLength));
            }

            /// <summary>Outward slope of the family: Bend s (2 depart - s) / (depart - s)^2, decaying past the biosphere for a line that never departs.</summary>
            public float RadiusSlope(float depart, float s)
            {
                if (depart <= clock.BiosphereSigma)
                {
                    if (s >= depart) return float.PositiveInfinity;
                    float d = depart - s;
                    return Bend * s * (2 * depart - s) / (d * d);
                }

                float sb = Mathf.Min(s, clock.BiosphereSigma), db = depart - sb;
                float slope = Bend * sb * (2 * depart - sb) / (db * db);
                return s <= clock.BiosphereSigma ? slope : slope * Mathf.Exp(-(s - clock.BiosphereSigma) / SettleLength);
            }

            /// <summary>Whether a line departs (at or before the biosphere) rather than easing into parallel.</summary>
            public bool Departs(int id) => Lines[id].Depart <= clock.BiosphereSigma;

            /// <summary>
            /// Scale of a line departing at <paramref name="depart"/> whose departure lies <paramref name="spacing"/> from its
            /// neighbours': the spacing itself (its space is at least that along its tail, and nearly that in the bundle
            /// as it departs); for a line that never departs, the space that spacing opens between the family's members
            /// at the biosphere, Bend sb^2 spacing / (depart - sb)^2.
            /// </summary>
            public float ScaleOf(float depart, float spacing)
            {
                float sb = clock.BiosphereSigma;
                if (depart <= sb) return spacing;
                float d = depart - sb;
                return spacing * Mathf.Min(Bend * sb * sb / (d * d), 1f);
            }

            /// <summary>Length of the stretch from arc k to the next (the last arc's: from the one before).</summary>
            public float StretchLength(int k) => k < Orders ? ArcSigma[k + 1] - ArcSigma[k] : ArcSigma[k] - ArcSigma[k - 1];

            /// <summary>How far a line has faded in at arc length s: 1 for the Big Bang's, a newborn's ramp over <see cref="BirthRamp"/> of its stretch.</summary>
            public float Ramp(int id, float s)
            {
                GridLine line = Lines[id];
                return line.Arc == 0 ? 1f : Smooth((s - line.Born) / (BirthRamp * StretchLength(line.Arc)));
            }

            /// <summary>The stretch (index into <see cref="Start"/>) containing arc length s, from <paramref name="k"/> on.</summary>
            public int Stretch(float s, int k)
            {
                while (k < Orders && ArcSigma[k + 1] <= s) k++;
                return k;
            }

            /// <summary>
            /// Position of line <paramref name="id"/> in tier <paramref name="tier"/>'s running order of stretch
            /// <paramref name="k"/>, searched from <paramref name="from"/> on (a line's position never moves inward from
            /// one stretch to the next: births inside it push it outward, departures happen beyond it); -1 when it is not
            /// running there.
            /// </summary>
            public int Position(int tier, int id, int k, int from)
            {
                List<int> running = Running[tier];
                int end = Start[tier][k + 1];
                for (int i = Mathf.Max(from, Start[tier][k]); i < end; i++)
                {
                    if (running[i] == id) return i;
                }

                return -1;
            }

            /// <summary>The running line of tier <paramref name="tier"/> at position <paramref name="p"/> of stretch k: <see cref="Track"/> inside the first, <see cref="None"/> beyond the last.</summary>
            public int At(int tier, int k, int p) => p < Start[tier][k] ? Track : p >= Start[tier][k + 1] ? None : Running[tier][p];

            /// <summary>Relative weight of a line by its level within its tier: the shallowest generation of the tier is full, each deeper one <see cref="LevelFade"/> fainter.</summary>
            public float LevelWeight(int id)
            {
                GridLine line = Lines[id];
                return Mathf.Pow(LevelFade, line.Depth - MinDepth[line.Tier]);
            }

            /// <summary>
            /// Grows the lines: the Big Bang's out of the cusp, then arc by arc a new line in every gap between
            /// neighbouring running lines (our track the innermost) that has opened beyond <see cref="BirthGap"/>, with
            /// its departure at the geometric mean of its neighbours' (next to our track, <see cref="TrackDepartRatio"/>
            /// times farther), as long as the spokes stay <see cref="MinDepartRatio"/> apart and the newborn still runs
            /// along the clock (<see cref="BirthSlope"/>). Nothing is born on the biosphere's arc.
            /// </summary>
            public static Grove Grow(Clock clock)
            {
                Grove grove = new Grove(clock);
                float sb = clock.BiosphereSigma;
                for (int k = 1; k <= Orders; k++)
                    grove.ArcSigma[k] = sb * Mathf.Pow(FirstArcSigma / sb, (Orders - k) / (float)(Orders - 1));

                // the Big Bang's lines, inner (departing at the biosphere) to outer (nearly radial out of the cusp): the
                // pinwheel's own spokes, all in the overview's tier
                List<int>[] running = new List<int>[Tiers];
                for (int t = 0; t < Tiers; t++) running[t] = new List<int>(1024);
                for (int j = 0; j < BigBangLines; j++)
                {
                    float depart = sb * Mathf.Pow(FirstDeparture / sb, j / (float)(BigBangLines - 1));
                    float next = sb * Mathf.Pow(FirstDeparture / sb, (j + 1) / (float)(BigBangLines - 1));
                    grove.Lines.Add(new GridLine { Born = 0, Depart = depart, Parent = Track, Arc = 0, Spacing = depart - next });
                    for (int t = 0; t < Tiers; t++) running[t].Add(j);
                }

                grove.MinDepth[0] = 0;
                for (int t = 0; t < Tiers; t++) grove.Running[t].AddRange(running[t]);
                List<int> all = running[Tiers - 1];
                float[] rho = new float[MaxLines];
                int[] before = new int[Tiers];
                for (int k = 1; k <= Orders; k++)
                {
                    float s = grove.ArcSigma[k];
                    // departed lines leave from the outer end (the lists run inner to outer, departures outer first)
                    grove.LastDeparture[k] = grove.LastDeparture[k - 1];
                    for (int t = 0; t < Tiers; t++)
                    {
                        List<int> list = running[t];
                        while (list.Count > 0 && grove.Lines[list[list.Count - 1]].Depart <= s)
                        {
                            grove.LastDeparture[k] = Mathf.Max(grove.LastDeparture[k], grove.Lines[list[list.Count - 1]].Depart);
                            list.RemoveAt(list.Count - 1);
                        }
                    }

                    for (int c = 0; c < all.Count; c++) rho[c] = grove.Rho(all[c], s);
                    // the rungs of every tier: between lines neighbouring within the tier (our track the innermost)
                    for (int t = 0; t < Tiers; t++)
                    {
                        List<int> list = running[t];
                        for (int c = 0; c < list.Count; c++) grove.Rungs.Add(new Rung { Arc = k, Inner = c > 0 ? list[c - 1] : Track, Outer = list[c], Tier = t });
                    }

                    // births, outer gap first so the positions inside stay valid while inserting: a newborn joins the
                    // running lines of its tier and every finer one, inside the same neighbours
                    for (int c = all.Count - 1; c >= 0 && k < Orders; c--)
                    {
                        int inner = c > 0 ? all[c - 1] : Track, outer = all[c];
                        float gap = rho[c] - (c > 0 ? rho[c - 1] : 0);
                        if (gap < BirthGap) continue;
                        GridLine outerLine = grove.Lines[outer];
                        float departOut = outerLine.Depart, depart, spacing;
                        int depth = outerLine.Depth;
                        if (inner == Track)
                        {
                            depart = departOut * TrackDepartRatio;
                            spacing = depart - departOut;
                        }
                        else
                        {
                            GridLine innerLine = grove.Lines[inner];
                            if (innerLine.Depart < departOut * MinDepartRatio * MinDepartRatio) continue;
                            depart = Mathf.Sqrt(innerLine.Depart * departOut);
                            spacing = Mathf.Min(depart - departOut, innerLine.Depart - depart);
                            depth = Mathf.Max(depth, innerLine.Depth);
                        }

                        // born still running along the clock; a line that never departs must not be shooting out at the
                        // biosphere either (it would bend back into parallel)
                        if (grove.RadiusSlope(depart, s) > BirthSlope) continue;
                        if (depart > sb && grove.RadiusSlope(depart, sb) > MaxSettleSlope) continue;
                        if (grove.Lines.Count >= MaxLines) break;
                        spacing = grove.ScaleOf(depart, spacing);
                        int tier = TierOf(spacing), id = grove.Lines.Count;
                        grove.Lines.Add(new GridLine
                        {
                            Born = s, Depart = depart, Parent = inner, Arc = k, Depth = depth + 1, Spacing = spacing, Tier = tier
                        });
                        // its position in each tier's list: after the lines inside it that belong to the tier
                        for (int t = 0; t < Tiers; t++) before[t] = 0;
                        for (int i = 0; i < c; i++) before[grove.Lines[all[i]].Tier]++;
                        for (int t = 1; t < Tiers; t++) before[t] += before[t - 1];
                        for (int t = tier; t < Tiers; t++) running[t].Insert(before[t], id);
                        grove.Births[k]++;
                        grove.MaxDepth = Mathf.Max(grove.MaxDepth, depth + 1);
                        grove.MinDepth[tier] = Mathf.Min(grove.MinDepth[tier], depth + 1);
                    }

                    for (int t = 0; t < Tiers; t++)
                    {
                        grove.Start[t][k] = grove.Running[t].Count;
                        grove.Running[t].AddRange(running[t]);
                    }
                }

                for (int t = 0; t < Tiers; t++)
                {
                    grove.Start[t][Orders + 1] = grove.Running[t].Count;
                    if (grove.MinDepth[t] == int.MaxValue) grove.MinDepth[t] = 0;
                }

                grove.LastDeparture[Orders + 1] = grove.LastDeparture[Orders];
                return grove;
            }
        }

        /// <summary>One sample of a line: where it is, the space around it and its opacity there.</summary>
        struct Sample
        {
            /// <summary>Arc and radius (data space).</summary>
            public float U, Rho;

            /// <summary>The space around the line here (world units): its gap to a neighbour, or the spacing of the spokes along its tail. It decides the mesh the sample is drawn in (<see cref="Share"/>).</summary>
            public float Spread;

            /// <summary>Opacity profile (0..1) before the tiers' share: fainter the wider its space beyond its class's scale, times its level, the birth ramp, the tail fade, open space, the handoff and the settled bundle.</summary>
            public float Alpha;
        }

        /// <summary>The red envelope's width sampled over the clock from the handoff to the Big Bang (plain math, safe on any thread).</summary>
        readonly struct Envelope
        {
            readonly float[] width;
            readonly float u0, du;

            public Envelope(MatterLayout layout)
            {
                u0 = MatterLayout.EndArc;
                du = (layout.BigBangArc - u0) / (EnvelopeSamples - 1);
                width = new float[EnvelopeSamples];
                for (int i = 0; i < EnvelopeSamples; i++) width[i] = layout.Envelope(u0 + du * i);
            }

            /// <summary>Width of the envelope at arc u (linear between samples).</summary>
            public float At(float u)
            {
                float x = Mathf.Clamp((u - u0) / du, 0, EnvelopeSamples - 1.001f);
                int i = (int)x;
                return width[i] + (width[i + 1] - width[i]) * (x - i);
            }
        }

        /// <summary>What every sample of a line needs: the line's class scale and level, and where it is drawn.</summary>
        readonly struct Walk
        {
            public readonly Grove Grove;
            public readonly Clock Clock;
            public readonly Envelope Envelope;
            public readonly int Id;
            public readonly float Scale, Level;

            public Walk(Grove grove, Clock clock, Envelope envelope, int id)
            {
                Grove = grove;
                Clock = clock;
                Envelope = envelope;
                Id = id;
                Scale = TierGap(grove.Lines[id].Tier);
                Level = grove.LevelWeight(id);
            }
        }

        LineMeshBuilder[] tiers;
        Material[] materials;
        readonly float[] lod = new float[Tiers];

        public override void Prepare(GraphContext ctx)
        {
            Stopwatch sw = Stopwatch.StartNew();
            MatterFile file = MatterFile.Parse(ctx.Text(MatterLayer.DataPath), out string error);
            if (file == null)
            {
                Debug.LogWarning("[Why] ExpansionGridLayer: " + error);
                return;
            }

            MatterLayout layout = MatterLayout.Build(file);
            if (layout.IsEmpty) return;

            Clock clock = new Clock(layout.BigBangArc);
            Envelope envelope = new Envelope(layout);
            Grove grove = Grove.Grow(clock);
            LineMeshBuilder[] builders = new LineMeshBuilder[Tiers];
            for (int t = 0; t < Tiers; t++) builders[t] = new LineMeshBuilder(t < 3 ? 16384 : 49152);
            int[] lineCount = new int[Tiers];
            int drawn = BuildLines(builders, clock, envelope, grove, lineCount, out int departing);
            int rungCount = BuildRungs(builders, clock, envelope, grove);

            Register(ctx, clock, grove, layout.BigBangYa);
            tiers = builders;

            int vertices = 0;
            string perTier = "";
            for (int t = 0; t < Tiers; t++)
            {
                vertices += builders[t].VertexCount;
                perTier += (t > 0 ? ", " : "") + $"tier {t} (space >= {TierGap(t):0.0000}): {lineCount[t]} lines of the class, {builders[t].VertexCount} vertices";
            }

            Debug.Log($"[Why] ExpansionGridLayer.Prepare {sw.Elapsed.TotalMilliseconds:0.0} ms: {grove.Lines.Count} lines " +
                      $"({BigBangLines} out of the Big Bang, {departing} departing, {grove.Lines.Count - departing} settling into parallel, " +
                      $"{grove.MaxDepth + 1} levels), {drawn} drawn, {rungCount} rungs, {vertices} vertices; {perTier}");
        }

        /// <summary>A line's angular neighbours among the spokes: the nearest departures on either side (for the spacing of its tail).</summary>
        struct Spoke
        {
            public float Depart;
            public int Id;
        }

        static readonly Comparison<Spoke> ByDeparture = (a, b) => a.Depart.CompareTo(b.Depart);

        /// <summary>
        /// The lines. Each is walked from its birth along its curve in world steps (turning at most a few degrees per
        /// step) until it leaves the bundle (<see cref="ExitSlope"/>) or, never departing, to the end of the red layer;
        /// a departing line's tail is then walked outward in rho to its cap. A point's weight follows the space around
        /// it: its gap to its neighbours at the same arc length in the bundle, the spacing of the spokes along its tail.
        /// Every stretch is then drawn into the tier of the space it has there (<see cref="Emit"/>).
        /// </summary>
        static int BuildLines(LineMeshBuilder[] builders, Clock clock, Envelope envelope, Grove grove, int[] lineCount, out int departing)
        {
            int total = grove.Lines.Count;
            // the spokes in departure order: each tail's spacing is the arc length to its nearest spoke either side
            // among the spokes of its own tier or coarser (the ones drawn with it)
            List<Spoke> spokes = new List<Spoke>(total);
            for (int id = 0; id < total; id++)
                if (grove.Departs(id)) spokes.Add(new Spoke { Depart = grove.Lines[id].Depart, Id = id });
            spokes.Sort(ByDeparture);
            departing = spokes.Count;
            float[] spacing = new float[total];
            for (int i = 0; i < spokes.Count; i++)
            {
                int tier = grove.Lines[spokes[i].Id].Tier;
                float d = float.PositiveInfinity;
                for (int j = i - 1; j >= 0; j--)
                {
                    if (grove.Lines[spokes[j].Id].Tier > tier) continue;
                    d = spokes[i].Depart - spokes[j].Depart;
                    break;
                }

                for (int j = i + 1; j < spokes.Count; j++)
                {
                    if (grove.Lines[spokes[j].Id].Tier > tier) continue;
                    d = Mathf.Min(d, spokes[j].Depart - spokes[i].Depart);
                    break;
                }

                spacing[spokes[i].Id] = float.IsInfinity(d) ? spokes[i].Depart : d;
            }

            List<Sample> samples = new List<Sample>(512);
            List<LinePoint> pts = new List<LinePoint>(512);
            int count = 0, vertices = 0, budget = MaxVertices - RungVertices;
            for (int id = 0; id < total && vertices < budget; id++)
            {
                GridLine line = grove.Lines[id];
                bool departs = grove.Departs(id);
                float end = departs ? line.Depart : clock.EndSigma;
                samples.Clear();
                int k = line.Arc, pos = grove.Start[line.Tier][k];
                Walk walk = new Walk(grove, clock, envelope, id);

                // the run: from birth along the curve, in world steps, turning at most TurnDegrees per step
                float s = line.Born, rho = grove.Rho(id, s), heading = float.NaN;
                float ramp = line.Arc > 0 ? BirthRamp * grove.StretchLength(line.Arc) : 0;
                float spread = AddRun(samples, walk, k, ref pos, s, rho);
                while (samples.Count < MaxPoints)
                {
                    float slope = grove.Slope(id, s), h = 1 + rho / GraphStyle.R0;
                    if (departs && slope > ExitSlope * h) break;
                    if (s >= end - 1e-6f) break;
                    float step = Mathf.Clamp(RelStep * (s + rho), MinStep, s >= clock.BiosphereSigma ? SettledStep : MaxStep);
                    float ds = step / Mathf.Sqrt(h * h + slope * slope);
                    // fine steps while fading in
                    if (s - line.Born < ramp) ds = Mathf.Min(ds, ramp / RampSamples);
                    // land exactly on the biosphere and on the end; never reach a departure (the exit comes first)
                    if (s < clock.BiosphereSigma && s + ds > clock.BiosphereSigma) ds = clock.BiosphereSigma - s;
                    if (s + ds > end) ds = departs ? 0.5f * (end - s) : end - s;
                    float s1 = s + ds, rho1 = grove.Rho(id, s1), hd = Heading(s, rho, s1, rho1);
                    for (int t = 0; t < TurnHalvings && !float.IsNaN(heading) && Mathf.Abs(Mathf.DeltaAngle(heading, hd)) > TurnDegrees; t++)
                    {
                        ds *= 0.5f;
                        s1 = s + ds;
                        rho1 = grove.Rho(id, s1);
                        hd = Heading(s, rho, s1, rho1);
                    }

                    heading = hd;
                    s = s1;
                    rho = rho1;
                    int k1 = grove.Stretch(s, k);
                    if (k1 != k)
                    {
                        pos = grove.Start[line.Tier][k1] + (pos - grove.Start[line.Tier][k]);
                        k = k1;
                    }

                    spread = AddRun(samples, walk, k, ref pos, s, rho);
                }

                // the tail: outward in rho to the cap set where the line leaves the bundle (its fade ends exactly at that
                // cap), the spacing of the spokes growing with the radius
                if (departs)
                {
                    float rhoExit = rho, rhoMax = TailReachAt(envelope.At(clock.ArcOf(s)));
                    while (rho < rhoMax - 0.5f * MinStep && samples.Count < MaxPoints)
                    {
                        float target = Mathf.Min(rho + Mathf.Max(MinStep, TailRel * rho), rhoMax);
                        s = Invert(grove, id, s, target, line.Depart);
                        float next = grove.Rho(id, s);
                        // the cap within float resolution of the arc length: done
                        if (next <= rho) break;
                        rho = next;
                        float angular = (GraphStyle.R0 + rho) / GraphStyle.R0 * spacing[id];
                        float blend = Smooth((rho - rhoExit) / Mathf.Max(rhoExit, MinStep));
                        Add(samples, walk, s, rho, LogLerp(spread, angular, blend), rhoMax);
                    }
                }

                if (!Emit(builders, samples, pts, ref vertices, budget)) continue;
                lineCount[line.Tier]++;
                count++;
            }

            if (vertices >= budget) Debug.LogWarning("[Why] ExpansionGridLayer: vertex budget reached, the finest lines are left out");
            return count;
        }

        /// <summary>
        /// Appends a sample of a line in the bundle: its space is its gap to one of its neighbours at the same arc length
        /// among the lines of its class or coarser, whichever side gives it more weight at its class's scale (a line
        /// shows where it has room on either side: a packed group of lines is drawn as its two boundary lines, never as
        /// a block), a newborn neighbour counting only as far as it has faded in (its gap blends in from the gap to the
        /// line beyond it); the outermost line while no spoke has departed yet is as spaced as its inner gap. Returns
        /// the space.
        /// </summary>
        static float AddRun(List<Sample> samples, in Walk walk, int k, ref int pos, float s, float rho)
        {
            Grove grove = walk.Grove;
            int tier = grove.Lines[walk.Id].Tier;
            pos = grove.Position(tier, walk.Id, k, pos);
            float inner = Gap(grove, k, grove.At(tier, k, pos - 1), grove.At(tier, k, pos - 2), s, rho);
            float outer = grove.At(tier, k, pos + 1) == None && grove.LastDeparture[k] <= 0
                ? inner
                : Gap(grove, k, grove.At(tier, k, pos + 1), grove.At(tier, k, pos + 2), s, rho);
            // the side that gives the line more weight is its room
            float spread = Weight(inner, walk.Scale) >= Weight(outer, walk.Scale) ? inner : outer;
            Add(samples, walk, s, rho, spread, TailReachAt(walk.Envelope.At(walk.Clock.ArcOf(s))));
            return spread;
        }

        /// <summary>Weight of a line with a space of <paramref name="gap"/> around it at scale <paramref name="scale"/>: hidden while crowded, crisp once resolved, fainter the wider.</summary>
        public static float Weight(float gap, float scale) => Emerge(gap, scale, TierRatio) * Faint(gap, scale);

        /// <summary>Space (world units) from a point of a line to a neighbour, blended from the space to the line beyond it while the neighbour is fading in.</summary>
        static float Gap(Grove grove, int k, int neighbour, int beyond, float s, float rho)
        {
            float gap = Space(grove, k, neighbour, s, rho);
            if (neighbour < 0) return gap;
            float ramp = grove.Ramp(neighbour, s);
            return ramp >= 1 ? gap : LogLerp(Space(grove, k, beyond, s, rho), gap, ramp);
        }

        /// <summary>Geometric interpolation between two spaces (both floored just above zero).</summary>
        static float LogLerp(float a, float b, float t) =>
            Mathf.Exp(Mathf.Lerp(Mathf.Log(Mathf.Max(a, 1e-6f)), Mathf.Log(Mathf.Max(b, 1e-6f)), t));

        /// <summary>
        /// Space (world units) between the point (s, rho) of a line and a neighbouring line in stretch k: the difference
        /// in radius foreshortened by the neighbour's slope (the distance to its curve), which for a neighbour that has
        /// departed is the distance along the clock to its radial tail, growing from nothing where it departed; our
        /// track is flat (<see cref="Track"/>), and beyond the outermost running line lies the tail of the last spoke to
        /// have departed (<see cref="None"/>; open space before any has).
        /// </summary>
        static float Space(Grove grove, int k, int neighbour, float s, float rho)
        {
            float h = 1 + rho / GraphStyle.R0;
            if (neighbour == Track) return rho;
            if (neighbour == None) return grove.LastDeparture[k] > 0 ? (s - grove.LastDeparture[k]) * h : OpenGap;
            float depart = grove.Lines[neighbour].Depart;
            if (s >= depart) return (s - depart) * h;
            float d = Mathf.Abs(grove.Radius(depart, s) - rho), m = grove.RadiusSlope(depart, s) / h;
            return Mathf.Min(d / Mathf.Sqrt(1 + m * m), OpenGap);
        }

        /// <summary>Radius to which a spoke is drawn where the red envelope is <paramref name="envelope"/> wide.</summary>
        static float TailReachAt(float envelope) => Mathf.Clamp(TailReach * envelope, TailFloor, TailCap);

        /// <summary>
        /// Appends a sample at arc length s, radius rho, with the space <paramref name="spread"/> around it, on a tail
        /// capped at <paramref name="reach"/>: its opacity (before the tiers' share) is the line's weight for that space
        /// at its class's scale (fainter the wider) times its level, its birth ramp, the fade along the tail, open space
        /// beyond the red envelope, the handoff and the settled bundle.
        /// </summary>
        static void Add(List<Sample> samples, in Walk walk, float s, float rho, float spread, float reach)
        {
            float u = walk.Clock.ArcOf(s);
            float alpha = Faint(spread, walk.Scale) * walk.Level * walk.Grove.Ramp(walk.Id, s) * TailFade(rho, reach) *
                          Unveiled(rho, walk.Envelope.At(u)) * Handoff(u) * Settle(walk.Clock, s);
            samples.Add(new Sample { U = u, Rho = rho, Spread = spread, Alpha = alpha });
        }

        /// <summary>
        /// Draws a line into the tiers: every stretch into the mesh of the space it has there (<see cref="Share"/>), so
        /// it shows wherever the zoom resolves that space, thin and crisp where it has just been resolved, thicker and
        /// fainter as its space widens beyond its scale. Runs are trimmed to where they show (one invisible point on
        /// either side) and split where the line is invisible for a while. Returns whether anything was drawn.
        /// </summary>
        static bool Emit(LineMeshBuilder[] builders, List<Sample> samples, List<LinePoint> pts, ref int vertices, int budget)
        {
            bool drawn = false;
            for (int t = 0; t < Tiers; t++)
            {
                int first = -1, last = -1;
                for (int i = 0; i <= samples.Count; i++)
                {
                    bool visible = i < samples.Count && Alpha(samples[i], t) * Mathf.Exp(-samples[i].Rho / GridRhoFade) >= CullAlpha;
                    if (visible)
                    {
                        if (first < 0) first = Mathf.Max(i - 1, 0);
                        last = i;
                        continue;
                    }

                    if (first < 0 || (i < samples.Count && i - last < SplitSamples)) continue;
                    int to = Mathf.Min(last + 1, samples.Count - 1);
                    if (to - first >= 1 && vertices + 2 * (to - first + 1) <= budget)
                    {
                        pts.Clear();
                        for (int j = first; j <= to; j++)
                        {
                            Sample p = samples[j];
                            float width = Mathf.Min(LineWidthPerGap * p.Spread, MaxWidthWorld);
                            pts.Add(new LinePoint(new Vector3(p.U, GraphStyle.MatterY, p.Rho), Tint(Alpha(p, t)), WidthPx, width));
                        }

                        builders[t].AddPolyline(pts, GridId);
                        vertices += 2 * pts.Count;
                        drawn = true;
                    }

                    first = last = -1;
                }
            }

            return drawn;
        }

        /// <summary>Opacity of a sample in tier <paramref name="tier"/>'s mesh: its profile times the tier's share of its space.</summary>
        static float Alpha(in Sample sample, int tier) => sample.Alpha * Share(sample.Spread, tier);

        /// <summary>
        /// Share of geometry with a space of <paramref name="gap"/> around it that tier <paramref name="tier"/>'s mesh
        /// draws: the tier whose band the space falls in draws it, fading in over <see cref="EmergeRatio"/> from the
        /// bottom of the band, and the next finer tier draws the remainder, so the two sum to one while both are on
        /// and the geometry fades out softly where the finer one is off.
        /// </summary>
        public static float Share(float gap, int tier)
        {
            int own = TierOf(gap);
            if (own == tier) return Emerge(gap, TierGap(tier), EmergeRatio);
            return own == tier - 1 ? 1 - Emerge(gap, TierGap(own), EmergeRatio) : 0;
        }

        /// <summary>
        /// The arc length past <paramref name="s"/> at which line <paramref name="id"/> reaches radius <paramref name="rho"/>
        /// (Newton from an Euler guess: the rise is convex, so the iteration closes in from above).
        /// </summary>
        static float Invert(Grove grove, int id, float s, float rho, float depart)
        {
            float lo = s, hi = depart;
            float x = Mathf.Min(s + (rho - grove.Rho(id, s)) / grove.Slope(id, s), 0.5f * (s + depart));
            for (int i = 0; i < TailNewton; i++)
            {
                float f = grove.Rho(id, x) - rho;
                if (f > 0) hi = x;
                else lo = x;
                float next = x - f / grove.Slope(id, x);
                // a step out of the bracket (or past the asymptote) falls back to bisection
                x = next > lo && next < hi ? next : 0.5f * (lo + hi);
            }

            return Mathf.Max(x, s + 1e-9f);
        }

        /// <summary>
        /// The rungs: at each arc, in each class, a dash across every gap between lines neighbouring in the class (our
        /// track the innermost) that is not being torn open by a departure, open where it would touch the lines, drawn
        /// into the tier of its cell (the smaller of the gap and the arcs' own spacing there), so it appears with that
        /// tier once the zoom resolves the cell, fainter and wider with it. A dash needs no crossfade between meshes:
        /// it is drawn whole into one, tier by tier from the coarsest, so the vertex budget cuts the finest rungs first.
        /// </summary>
        static int BuildRungs(LineMeshBuilder[] builders, Clock clock, Envelope envelopes, Grove grove)
        {
            // the tier of every rung's cell first, so the meshes fill from the coarsest
            List<Rung> rungs = grove.Rungs;
            int[] tier = new int[rungs.Count];
            for (int i = 0; i < rungs.Count; i++)
            {
                Rung rung = rungs[i];
                float s = grove.ArcSigma[rung.Arc];
                float rhoIn = grove.NeighbourRho(rung.Inner, s), gap = grove.Rho(rung.Outer, s) - rhoIn;
                float spacing = (s - grove.ArcSigma[rung.Arc - 1]) * (1 + rhoIn / GraphStyle.R0), cell = Mathf.Min(gap, spacing);
                tier[i] = gap > RungReach * spacing || cell < TierGap(Tiers - 1) ? -1 : TierOf(cell);
            }

            List<LinePoint> pts = new List<LinePoint>(2);
            int count = 0, vertices = 0;
            for (int t = 0; t < Tiers; t++) vertices += builders[t].VertexCount;
            for (int t = 0; t < Tiers && vertices + 4 <= MaxVertices; t++)
            {
                for (int i = 0; i < rungs.Count && vertices + 4 <= MaxVertices; i++)
                {
                    if (tier[i] != t) continue;
                    Rung rung = rungs[i];
                    float s = grove.ArcSigma[rung.Arc], u = clock.ArcOf(s), envelope = envelopes.At(u);
                    float rhoIn = grove.NeighbourRho(rung.Inner, s), rhoOut = grove.Rho(rung.Outer, s), gap = rhoOut - rhoIn;
                    // the arcs' own spacing along the clock, in the world
                    float cell = Mathf.Min(gap, (s - grove.ArcSigma[rung.Arc - 1]) * (1 + rhoIn / GraphStyle.R0));
                    // as faint as the fainter of the two lines it joins (by level; our track counts as full)
                    float level = Mathf.Min(grove.LevelWeight(rung.Outer), rung.Inner == Track ? 1f : grove.LevelWeight(rung.Inner));
                    float alpha = StrokeWeight * level * Faint(gap, TierGap(rung.Tier)) * TailFade(rhoIn, TailReachAt(envelope)) *
                                  Unveiled(rhoIn, envelope) * Handoff(u) * Settle(clock, s);
                    if (alpha * Mathf.Exp(-rhoIn / GridRhoFade) < CullAlpha) continue;
                    float width = Mathf.Min(StrokeWidthPerGap * cell, MaxWidthWorld);
                    pts.Clear();
                    pts.Add(new LinePoint(new Vector3(u, GraphStyle.MatterY, rhoIn + DashGap * gap), Tint(alpha), WidthPx, width));
                    pts.Add(new LinePoint(new Vector3(u, GraphStyle.MatterY, rhoOut - DashGap * gap), Tint(alpha), WidthPx, width));
                    builders[t].AddPolyline(pts, GridId);
                    vertices += 4;
                    count++;
                }
            }

            if (vertices + 4 > MaxVertices) Debug.Log("[Why] ExpansionGridLayer: vertex budget reached, the finest rungs are left out");
            return count;
        }

        /// <summary>
        /// World heading (degrees, up to a constant) of the segment between two points of the clock's circle part, (arc length,
        /// rho) each: the clock turns clockwise under it.
        /// </summary>
        static float Heading(float s0, float rho0, float s1, float rho1)
        {
            float across = rho1 - rho0, along = (GraphStyle.R0 + 0.5f * (rho0 + rho1)) * (s1 - s0) / GraphStyle.R0;
            return (Mathf.Atan2(across, along) - 0.5f * (s0 + s1) / GraphStyle.R0) * Mathf.Rad2Deg;
        }

        /// <summary>The grid's anchor (the Big Bang, where space starts expanding), orders of magnitude on a few arcs and a note on our world's scale.</summary>
        static void Register(GraphContext ctx, Clock clock, Grove grove, double bigBangYa)
        {
            // the tooltip's time span reads "13.8 billion years ago - now": space has expanded ever since
            Anchors.Register(new Anchor
            {
                Key = AnchorKey, Label = "Expanding space", Blurb = Blurb, Level = GraphLevel.Matter,
                YearsAgo = bigBangYa, EndYearsAgo = 0, Y = GraphStyle.MatterY, Rho = 0,
                Ids = IdRange.Single(GridId), Tier = 2
            });

            foreach (int k in LabelOrders)
            {
                float s = grove.ArcSigma[k];
                AddLabel(ctx, "×10<sup>" + k + "</sup>", new Vector3(clock.ArcOf(s), GraphStyle.MatterY, LabelRhoFrac * s), OrderPriority);
            }

            AddLabel(ctx, "our world's scale", new Vector3(clock.BiosphereArc, GraphStyle.MatterY, WorldLabelRho), WorldPriority);
        }

        static void AddLabel(GraphContext ctx, string text, Vector3 data, float priority)
        {
            ctx.Labels.Add(new LabelSpec
            {
                Text = text,
                Data = data,
                Priority = priority,
                SizePx = LabelSize,
                Color = GraphStyle.TextDim,
                PixelOffset = new Vector2(5, 6),
                AnchorKey = AnchorKey,
                HandoffFade = true,
                Ids = IdRange.Single(GridId)
            });
        }

        public override void Upload(GraphContext ctx)
        {
            if (tiers == null) return;
            // neutral like the axes: hue is reserved for the levels, and grey reads as space, not as matter
            Color color = GraphStyle.Axis;
            color.a = GridAlpha;
            materials = new Material[Tiers];
            for (int t = 0; t < Tiers; t++)
            {
                // beneath the envelope and the band fills (QueueMatter, alpha blended: they veil it) and the matter
                // lines (QueueMatter + 1); additive like the other lines. The finer tiers start hidden; Tick reveals them
                Material m = GraphMaterials.Line(color, GridIntensity, GraphMaterials.QueueMatter - 1, true, GridRhoFade, 0f);
                GraphMaterials.FadeBeforeHumanBranch(m);
                lod[t] = t == 0 ? 1f : 0f;
                GraphMaterials.SetAlpha(m, lod[t]);
                materials[t] = m;
                AddMesh("MatterExpansionGrid" + t, tiers[t].ToMesh("MatterExpansionGrid" + t), m);
            }

            tiers = null;
        }

        /// <summary>
        /// Level of detail: a finer tier fades in once its band of space spans a few pixels on screen (at the depth of
        /// the camera's target), so every zoom reveals the stretches of lines that it resolves, the next generation of
        /// contained lines, without crowding, and a tier recedes once the next finer one is in, so the coarse, widely
        /// spaced lines become the background of the crisp ones.
        /// </summary>
        public override void Tick(GraphContext ctx, CameraRig rig)
        {
            if (materials == null || rig == null || rig.Cam == null) return;
            float pixelsPerUnit = Screen.height / (2f * Mathf.Max(rig.Pose.Distance, 1e-3f) * Mathf.Tan(rig.Cam.fieldOfView * 0.5f * Mathf.Deg2Rad));
            for (int t = 0; t < Tiers; t++)
            {
                float target = TierAlpha(t, pixelsPerUnit);
                if (Mathf.Abs(target - lod[t]) < 0.01f) continue;
                lod[t] = target;
                GraphMaterials.SetAlpha(materials[t], target);
            }
        }

        /// <summary>Opacity of a tier at a pixel density: in once its band of space spans <see cref="TierOnPx"/>, receding once the next tier is in too (tier 0 never fades out).</summary>
        public static float TierAlpha(int tier, float pixelsPerUnit)
        {
            float px = TierGap(tier) * pixelsPerUnit;
            float fadeIn = tier == 0 ? 1f : Smooth((px - TierOffPx) / (TierOnPx - TierOffPx));
            return fadeIn * Mathf.Pow(Mathf.Max(px / (TierOnPx * TierRatio), 1f), -Recede);
        }

        /// <summary>Scale of tier <paramref name="tier"/> (world units): the bottom of its band of space.</summary>
        public static float TierGap(int tier) => CrowdGap / Mathf.Pow(TierRatio, tier);

        /// <summary>The tier of a space or a scale: the coarsest whose band it reaches (the finest for anything finer).</summary>
        public static int TierOf(float scale)
        {
            int tier = Mathf.CeilToInt(Mathf.Log(CrowdGap / Mathf.Max(scale, 1e-9f)) / Mathf.Log(TierRatio) - 1e-4f);
            return Mathf.Clamp(tier, 0, Tiers - 1);
        }

        /// <summary>Opacity of a line whose space is <paramref name="gap"/> world units, for a threshold <paramref name="crowd"/>: 0 while crowded, 1 once <paramref name="ratio"/> times wider.</summary>
        public static float Emerge(float gap, float crowd, float ratio) => Smooth(Mathf.Log(Mathf.Max(gap, 1e-9f) / crowd) / Mathf.Log(ratio));

        /// <summary>Relative opacity of a resolvable line: fainter the wider its space has opened beyond its own scale.</summary>
        public static float Faint(float gap, float scale) => Mathf.Pow(Mathf.Max(gap / scale, 1f), -Fainter);

        /// <summary>Relative opacity at radius rho of a spoke capped at <paramref name="rhoMax"/>: 1 up to <see cref="TailFadeStart"/> of it, 0 at the cap.</summary>
        public static float TailFade(float rho, float rhoMax) => 1 - Smooth((rho - TailFadeStart * rhoMax) / Mathf.Max((1 - TailFadeStart) * rhoMax, 1e-3f));

        /// <summary>Relative opacity at radius rho where the red envelope is <paramref name="envelope"/> wide: 1 inside it, <see cref="OpenSpaceAlpha"/> well beyond it.</summary>
        public static float Unveiled(float rho, float envelope) =>
            1 - (1 - OpenSpaceAlpha) * Smooth((rho - envelope) / Mathf.Max(OpenSpaceSpan * envelope, 1e-3f));

        static float Handoff(float u) => Mathf.Pow(GraphStyle.HandoffFade(u), HandoffExponent);

        /// <summary>Relative weight at arc length s: 1 while the fan opens, receding to <see cref="SettledWeight"/> once it has settled.</summary>
        static float Settle(Clock clock, float s) => 1 - (1 - SettledWeight) * Smooth((s - clock.BiosphereSigma) / SettleSpan);

        static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3 - 2 * t);
        }

        static Color32 Tint(float alpha) => new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha) * 255f + 0.5f));
    }
}
