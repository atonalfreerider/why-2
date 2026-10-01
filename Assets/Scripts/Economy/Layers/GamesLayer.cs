using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using Why.Economy.Data;
using Why.Economy.Model;
using Debug = UnityEngine.Debug;
using static Why.Economy.EconomyGeometry;

namespace Why.Economy.Layers
{
    /// <summary>
    /// The games station: the prisoner's dilemma as page 4 of the user's notebook draws it, played live.
    ///
    /// Left half, person against person. One round alone ("single vacuum") is a payoff diamond, the 2x2 square turned
    /// 45 degrees: top both cooperate, bottom both defect (shaded red: the Nash equilibrium, since defecting pays more
    /// whatever the other does), left and right one exploits the other. Then Person A and Person B (two brains) play
    /// the strategies chosen in the games panel round after round: each round is a small diamond on a ladder between
    /// them with its outcome cell filled, light for cooperation and red for defection, a spark where a move came out
    /// wrong (a mistake). Beside the ladder the Pareto arrow rises to the share of the best joint payoff the pair has
    /// earned so far (always both cooperating = the full arrow). After every round they meet again with chance w, the
    /// shadow of the future; a game ends, holds and the next begins.
    ///
    /// Right half, tribe against tribe. Two pyramids of members, the alpha on top, each member colored by its last move
    /// toward the other tribe; between them a ladder of tribe rounds whose four cells are filled by how likely each
    /// outcome was (the share of cooperative moves on each side), so feuds read as red runs and reconciliations as light
    /// ones. Members' strategies come from the modeled population when its economic lives have run, else from the data.
    ///
    /// The floor, evolution. A stream of strategy shares over 200 generations of the replicator dynamics with the
    /// chosen mistakes and shadow of the future: strategies spread by the payoff they earn against the current mix.
    ///
    /// The diagram stands in the station's plane z = 0; the floor lies in front of it (toward the viewer), because
    /// from the views' cameras, 10 degrees above the station, a floor behind the diagram would sit behind its lower
    /// rungs. The labels show only while the camera looks at this station (<see cref="LabelDistance"/>). One round of
    /// both games is played every <see cref="RoundSeconds"/> while the station is in view; a change of the games'
    /// settings (<see cref="EconomyState.SetGames"/>) restarts both games and recomputes the evolution (on a worker
    /// thread). Everything is deterministic: games are seeded by their number.
    /// </summary>
    [GraphScenes(GraphScene.Economy)]
    public sealed class GamesLayer : GraphLayer
    {
        // ------------------------------------------------------------------ layout (station-local: x -5..5 across, y 0..5 up)

        /// <summary>The one-round diamond: its center and half-diagonal.</summary>
        static readonly Vector3 NashCenter = new Vector3(-4.3f, 1.55f, 0);

        const float NashHalf = 0.72f;

        /// <summary>Person A and Person B, either side of their ladder; the brains' radius.</summary>
        static readonly Vector3 BrainA = new Vector3(-3.6f, 3.75f, 0), BrainB = new Vector3(-0.8f, 3.75f, 0);

        const float BrainRadius = 0.42f;

        /// <summary>Points along the curved thread from a brain to its latest move.</summary>
        const int ThreadSegments = 16;

        /// <summary>
        /// The ladders: one diamond per round stacked upward from <see cref="LadderBaseY"/>, rounds touching tip to tip
        /// (<see cref="LadderStep"/> = twice <see cref="RungHalf"/>). <see cref="LadderRows"/> rounds fit; then the
        /// ladder scrolls down and its lowest rounds fade.
        /// </summary>
        const float PersonLadderX = -2.2f, TribeLadderX = 2.8f, LadderBaseY = 0.4f, LadderStep = 0.3f, RungHalf = 0.15f;

        const int LadderRows = 14;

        /// <summary>Alpha of the lowest rows once older rounds have scrolled away (bottom row first).</summary>
        static readonly float[] ScrollFade = { 0.3f, 0.55f, 0.8f };

        /// <summary>The Pareto arrow beside the persons' ladder: its full height is "always both cooperate".</summary>
        const float ParetoX = -1.75f, ParetoBottom = 0.25f, ParetoTop = 4.45f, ArrowHead = 0.12f;

        /// <summary>The tribes' pyramids: centers, apex and base heights, half the base; member dot radii.</summary>
        const float TribeAX = 1.2f, TribeBX = 4.4f, ApexY = 4.05f, BaseY = 1.45f, HalfBase = 0.85f;

        const float MemberRadius = 0.052f, AlphaRadius = 0.085f;

        /// <summary>Largest dot radius as a share of the spacing between members (large tribes get smaller dots).</summary>
        const float MemberFill = 0.42f, AlphaFill = 0.55f;

        /// <summary>Space kept free inside a pyramid's outline around the member dots.</summary>
        const float PyramidInset = 0.2f;

        /// <summary>The evolution stream on the floor in front of the diagram (negative z is toward the viewer).</summary>
        const float FloorY = 0.02f, FloorLeft = -5f, FloorRight = 5f, FloorNear = -1.7f, FloorFar = -0.3f;

        /// <summary>Each band's label sits where the band is widest, but not in the first or last tenth of the run.</summary>
        const float LabelMargin = 0.1f;

        /// <summary>Bands that never reach this share stay unlabeled.</summary>
        const float MinBandLabelShare = 0.05f;

        /// <summary>
        /// The floor's caption sits behind the stream between the two ladders' feet, where both views have room for it.
        /// </summary>
        const float FloorCaptionX = 0.6f;

        /// <summary>Headings and captions above each half.</summary>
        const float HeadingY = 5.25f, CaptionY = 4.98f;

        /// <summary>
        /// The station's center (for "is it in view") and how close the camera's target must be to play: the games' own
        /// views and the mind station's next door (one station spacing away, the games standing behind it) play; the
        /// circuit's views, two spacings away, do not.
        /// </summary>
        static readonly Vector3 CenterLocal = new Vector3(0, EconomyStyle.StationHeight * 0.5f, 0);

        const float NearDistance = 1.5f * EconomyStyle.StationSpacing;

        /// <summary>
        /// The station's labels show only while the camera's target is this close to its center (the games' own views and
        /// their anchors). Fixed labels never fade with distance, and from the next station's views the games stand small
        /// in the background, where their labels would crowd that station's own.
        /// </summary>
        const float LabelDistance = 0.75f * EconomyStyle.StationSpacing;

        // ------------------------------------------------------------------ the games

        /// <summary>One round of both games every this many seconds (unscaled time).</summary>
        const float RoundSeconds = 0.4f;

        /// <summary>A finished game stays on the ladder this long before the next one starts.</summary>
        const float HoldSeconds = 2.5f;

        /// <summary>Longest frame step counted (a stall does not fast-forward the games).</summary>
        const float MaxFrameSeconds = 0.1f;

        /// <summary>A person game lasts a sampled number of rounds (mean 1 / (1 - w)), at most this many.</summary>
        const int MaxPersonRounds = 40;

        const int TribeRoundsPerGame = 30;

        /// <summary>Initial line capacity (points) of the live mesh: both ladders, sparks, threads, alpha rings.</summary>
        const int LivePoints = 512;

        /// <summary>Seeds of the games (game k uses seed + k * stride), the tribes' members and the evolution.</summary>
        const int PersonSeed = 7001, TribeSeed = 7103, MembersSeed = 7207, EvolutionSeed = 7307, SeedStride = 7919;

        /// <summary>Tribe settings when games.json has none: members per tribe, distrust of the other tribe, alpha sway.</summary>
        const int DefaultTribeSize = 24;

        const float DefaultDistrust = 0.08f, DefaultAlphaSway = 0.25f;

        /// <summary>The replicator run: generations, games per pair of strategies for the payoff matrix, mutation.</summary>
        const int Generations = 200, GamesPerPair = 200;

        const float DefaultMutation = 0.01f;

        /// <summary>
        /// The ranges <see cref="EconomyState.SetGames"/> keeps the chance of a mistake and of meeting again in; the data's
        /// values are clamped the same way, so the first evolution is the one the state will ask for.
        /// </summary>
        const float MaxNoise = 0.5f, MaxContinuation = 0.999f;

        /// <summary>The evolving strategies in the stream's order from the front (nearest the viewer) to the back.</summary>
        static readonly PdStrategy[] StreamOrder =
        {
            PdStrategy.AlwaysDefect, PdStrategy.Grim, PdStrategy.TitForTat, PdStrategy.GenerousTitForTat,
            PdStrategy.WinStayLoseShift, PdStrategy.AlwaysCooperate
        };

        // ------------------------------------------------------------------ highlight ids (EconomyIds.Games + element)

        const int IdNash = EconomyIds.Games, IdLadder = EconomyIds.Games + 1, IdPareto = EconomyIds.Games + 2;
        const int IdPersonA = EconomyIds.Games + 3, IdPersonB = EconomyIds.Games + 4;
        const int IdTribeA = EconomyIds.Games + 5, IdTribeB = EconomyIds.Games + 6, IdTribeLadder = EconomyIds.Games + 7;
        const int IdTitForTat = EconomyIds.Games + 8;

        /// <summary>The evolution's bands (+ index in <see cref="StreamOrder"/>) and its frame.</summary>
        const int IdEvolution = EconomyIds.Games + 10, IdEvolutionFrame = IdEvolution + 6;

        // ------------------------------------------------------------------ colors

        static readonly Color Light = EconomyStyle.Cooperate;
        static readonly Color Red = EconomyStyle.Defect;
        static readonly Color Gold = EconomyStyle.Capital;
        static readonly Color SparkColor = new Color(1f, 0.95f, 0.72f);
        static readonly Color GrimColor = new Color(0.85f, 0.42f, 0.12f);
        static readonly Color WarmWhite = new Color(1f, 0.92f, 0.8f);
        static readonly Color PaleSteel = new Color(0.58f, 0.64f, 0.74f);
        static readonly Color PaleBlue = new Color(0.55f, 0.72f, 1f);

        public override int Order => 44;

        /// <summary>The viewer's choices the games depend on; any change restarts them.</summary>
        readonly struct Settings
        {
            public readonly PdStrategy A, B;
            public readonly float Noise, Continuation;

            public Settings(PdStrategy a, PdStrategy b, float noise, float continuation)
            {
                A = a;
                B = b;
                Noise = noise;
                Continuation = continuation;
            }

            public static Settings Current => new Settings(EconomyState.StrategyA, EconomyState.StrategyB, EconomyState.Noise,
                EconomyState.Continuation);

            public bool Same(Settings o) => A == o.A && B == o.B && Noise == o.Noise && Continuation == o.Continuation;

            /// <summary>The evolution depends on mistakes and the shadow of the future only.</summary>
            public bool SameEvolution(Settings o) => Noise == o.Noise && Continuation == o.Continuation;
        }

        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        /// <summary>"0%" .. "100%", so live labels do not format numbers every round.</summary>
        static readonly string[] Percents = BuildPercents();

        Station station;
        LabelSystem labelSystem;
        PdPayoff pay;
        float[] mix;
        GamesSetup.Source mixSource, membersSource;
        PdStrategy[] membersA, membersB;
        Vector3[] dotsA, dotsB;
        float memberRadius = MemberRadius, alphaRadius = AlphaRadius;
        float distrust, alphaSway, mutation;
        bool specNoise, specContinuation;
        float dataNoise, dataContinuation;
        Settings settings;
        int seenVersion = -1;

        // built in Prepare, uploaded once
        SurfaceMeshBuilder staticFills, floorFills;
        LineMeshBuilder staticLines, floorLines;
        bool uploaded;
        Material fillMat, liveFillMat, floorFillMat, lineMat, liveLineMat, floorLineMat;
        MeshFilter liveFill, liveLine, floorFill, floorLine;

        // the person game
        readonly List<PdRound> rounds = new List<PdRound>(MaxPersonRounds);
        System.Random personRng;
        PdMemory memoryA, memoryB;
        int personGame, personLength;
        float personHold, totalA, totalB;

        // the tribe game
        TribeGame tribe;
        int tribeGame;
        float tribeHold;

        float roundClock;
        bool liveDirty = true;

        // the evolution (computed on a worker thread)
        List<float[]> evolution;
        Settings evolvedFor, evolvingFor;
        Task<List<float[]>> evolving;

        // live labels
        LabelSpec personHeading, personALabel, personBLabel, scoreLabel, efficiencyLabel, shadowLabel;
        LabelSpec tribeHeading, tribeCaption, tribeALabel, tribeBLabel, tribeRoundLabel, floorCaption;
        readonly LabelSpec[] rowLabelsA = new LabelSpec[LadderRows], rowLabelsB = new LabelSpec[LadderRows];
        readonly LabelSpec[] bandLabels = new LabelSpec[StreamOrder.Length];
        readonly List<LabelSpec> labels = new List<LabelSpec>(80);
        bool labelsShown = true, hidePersonHeadings, hideTribeHeadings;
        Anchor evolutionAnchor, forgivenessAnchor;

        // ------------------------------------------------------------------ prepare (worker thread)

        public override void Prepare(GraphContext ctx)
        {
            Stopwatch sw = Stopwatch.StartNew();
            station = EconomyStage.Get(EconomyStage.Games);
            labelSystem = ctx.Labels;
            EconomyModel model = ctx.Shared<EconomyModel>(EconomyModel.SharedKey);
            EconomyData data = model?.Data;

            pay = GamesSetup.Payoff(data);
            distrust = Mathf.Clamp01(GamesSetup.Spec(data, "distrust", DefaultDistrust));
            alphaSway = Mathf.Clamp01(GamesSetup.Spec(data, "alphaSway", DefaultAlphaSway));
            mutation = Mathf.Clamp(GamesSetup.Spec(data, "mutation", DefaultMutation), 0f, 0.5f);
            int tribeSize = Mathf.Clamp(Mathf.RoundToInt(GamesSetup.Spec(data, "tribeSize", DefaultTribeSize)), 6, 120);
            specNoise = GamesSetup.HasSpec(data, "noise");
            specContinuation = GamesSetup.HasSpec(data, "continuation");
            dataNoise = Mathf.Clamp(GamesSetup.Spec(data, "noise", EconomyState.Noise), 0f, MaxNoise);
            dataContinuation = Mathf.Clamp(GamesSetup.Spec(data, "continuation", EconomyState.Continuation), 0f,
                MaxContinuation);

            mix = GamesSetup.Mix(model, out mixSource);
            membersA = GamesSetup.Tribe(model, 0, tribeSize, mix, MembersSeed, out membersSource);
            membersB = GamesSetup.Tribe(model, 1, tribeSize, mix, MembersSeed + SeedStride, out GamesSetup.Source sourceB);
            if (sourceB != membersSource) membersSource = GamesSetup.Source.Default;
            dotsA = MemberDots(TribeAX, membersA.Length, out float spacing);
            dotsB = MemberDots(TribeBX, membersB.Length, out _);
            memberRadius = Mathf.Min(MemberRadius, MemberFill * spacing);
            alphaRadius = Mathf.Min(AlphaRadius, AlphaFill * spacing);

            // the settings the scene will most likely start with (EconomyState's choices, the data's noise and shadow of
            // the future); Upload takes them again once EconomyLoaderLayer has reset the state
            settings = new Settings(EconomyState.StrategyA, EconomyState.StrategyB, specNoise ? dataNoise : EconomyState.Noise,
                specContinuation ? dataContinuation : EconomyState.Continuation);
            evolution = Evolve(settings);
            evolvedFor = settings;

            staticFills = new SurfaceMeshBuilder();
            staticLines = new LineMeshBuilder(2048);
            DrawNash(staticFills, staticLines);
            DrawPeople(staticFills, staticLines);
            DrawLadderFrames(staticLines);
            DrawPyramids(staticLines);

            floorFills = new SurfaceMeshBuilder();
            floorLines = new LineMeshBuilder(Generations * (StreamOrder.Length + 2) + 16);
            DrawFloor(evolution, floorFills, floorLines);

            AddLabels(ctx);
            PlaceBandLabels(evolution);
            foreach (LabelSpec band in bandLabels)
            {
                ctx.Labels.Add(band); // placed and ranked first (see AddLabels)
                labels.Add(band);
            }
            RegisterAnchors(tribeSize);
            Debug.Log($"[Why] GamesLayer.Prepare {sw.ElapsedMilliseconds} ms: mix from {mixSource}, tribes of {tribeSize} " +
                      $"from {membersSource}, evolution {evolution.Count} generations");
        }

        List<float[]> Evolve(Settings s) => GamesSetup.Evolve(StreamOrder, pay, mix, s.Noise, s.Continuation, Generations,
            GamesPerPair, mutation, EvolutionSeed);

        /// <summary>
        /// Member dots of a pyramid, top row first: the alpha alone at the apex, then rows one wider each. A last row that
        /// is not full spreads its members over the full row's width, so the pyramid keeps its base corners. The spacing
        /// is the smallest distance between neighbors (for the dots' size).
        /// </summary>
        static Vector3[] MemberDots(float centerX, int count, out float spacing)
        {
            int rows = 1;
            while (rows * (rows + 1) / 2 < count) rows++;
            float top = ApexY - PyramidInset * 1.4f, bottom = BaseY + PyramidInset;
            float rowStep = rows > 1 ? (top - bottom) / (rows - 1) : 0;
            float halfAtBottom = HalfBase * (ApexY - bottom) / (ApexY - BaseY) - PyramidInset;
            float dx = rows > 1 ? 2 * halfAtBottom / (rows - 1) : 0;
            spacing = rows > 1 ? Mathf.Min(dx, rowStep) : float.MaxValue;
            Vector3[] dots = new Vector3[count];
            int k = 0;
            for (int r = 0; r < rows && k < count; r++)
            {
                int full = r + 1, inRow = Math.Min(full, count - k);
                float step = inRow > 1 ? dx * (full - 1) / (inRow - 1) : 0;
                float y = top - r * rowStep;
                for (int j = 0; j < inRow; j++) dots[k++] = new Vector3(centerX + (j - (inRow - 1) * 0.5f) * step, y, 0);
            }

            return dots;
        }

        // ------------------------------------------------------------------ the fixed drawing

        /// <summary>
        /// The one-round diamond: the Nash cell (both defect) shaded red and outlined; each side cell split between the
        /// exploited player (light) and the exploiter (red), as on the ladders; mutual cooperation a faint light.
        /// </summary>
        void DrawNash(SurfaceMeshBuilder fills, LineMeshBuilder lines)
        {
            Vector3 c = NashCenter;
            float h = NashHalf;
            FillCell(fills, c, h, h, DiamondCell.Bottom, Tint(Red, 0.62f), IdNash, 1.1f);
            FillCell(fills, c, h, h, DiamondCell.Top, Tint(Light, 0.07f), IdNash);
            FillHalfCell(fills, c, h, h, DiamondCell.Left, true, Tint(Light, 0.07f), IdNash);
            FillHalfCell(fills, c, h, h, DiamondCell.Left, false, Tint(Red, 0.1f), IdNash);
            FillHalfCell(fills, c, h, h, DiamondCell.Right, false, Tint(Red, 0.1f), IdNash);
            FillHalfCell(fills, c, h, h, DiamondCell.Right, true, Tint(Light, 0.07f), IdNash);
            DiamondOutline(lines, c, h, h, Tint(GraphStyle.Text, 0.85f), 1.6f, IdNash, 1.1f);
            DiamondCross(lines, c, h, h, Tint(GraphStyle.Axis, 0.45f), 1f, IdNash);
            DiamondOutline(lines, CellCenter(c, h, h, DiamondCell.Bottom), h * 0.5f, h * 0.5f, Tint(Red, 0.9f), 1.4f, IdNash,
                1.4f);
        }

        /// <summary>Person A and Person B: a cloud of a brain with a fissure down the middle.</summary>
        void DrawPeople(SurfaceMeshBuilder fills, LineMeshBuilder lines)
        {
            DrawBrain(fills, lines, BrainA, IdPersonA);
            DrawBrain(fills, lines, BrainB, IdPersonB);
        }

        static void DrawBrain(SurfaceMeshBuilder fills, LineMeshBuilder lines, Vector3 c, int id)
        {
            Color blue = EconomyStyle.People;
            Disc(fills, c, BrainRadius * 0.92f, 32, Tint(blue, 0.12f), id);
            Cloud(lines, c, BrainRadius, 9, 0.12f, Tint(blue, 0.95f), 1.6f, id, 1.5f);
            // the fissure between the hemispheres, and a fold in each
            float r = BrainRadius;
            Arc(lines, new Vector3(c.x + r * 0.55f, c.y, 0), r * 0.55f, 145, 215, 10, Tint(blue, 0.7f), 1.1f, id, 1.3f);
            Arc(lines, new Vector3(c.x - r * 0.15f, c.y + r * 0.05f, 0), r * 0.42f, 110, 175, 8, Tint(blue, 0.5f), 1f, id, 1.2f);
            Arc(lines, new Vector3(c.x + r * 0.15f, c.y - r * 0.1f, 0), r * 0.42f, -70, 5, 8, Tint(blue, 0.5f), 1f, id, 1.2f);
        }

        /// <summary>Faint slots of both ladders, the Pareto arrow's track and the tribes' tit-for-tat spine.</summary>
        void DrawLadderFrames(LineMeshBuilder lines)
        {
            for (int row = 0; row < LadderRows; row++)
            {
                DiamondOutline(lines, Rung(PersonLadderX, row), RungHalf, RungHalf, Tint(GraphStyle.Axis, 0.12f), 1f, IdLadder);
                DiamondOutline(lines, Rung(TribeLadderX, row), RungHalf, RungHalf, Tint(GraphStyle.Axis, 0.12f), 1f,
                    IdTribeLadder);
            }

            // the Pareto arrow's full height (always both cooperating), and the level of always both defecting
            Arrow(lines, new Vector3(ParetoX, ParetoBottom, 0), new Vector3(ParetoX, ParetoTop + ArrowHead, 0), ArrowHead,
                Tint(Gold, 0.4f), 1.2f, IdPareto);
            float y = Mathf.Lerp(ParetoBottom, ParetoTop, DefectionShare);
            lines.AddSegment(new Vector3(ParetoX - 0.1f, y, 0), new Vector3(ParetoX + 0.1f, y, 0), Tint(Red, 0.9f), 1.6f, 0,
                IdPareto, 1.3f);

            // tit for tat: the tribes' ladder climbs a spine that ends in an arrow
            Arrow(lines, new Vector3(TribeLadderX, LadderBaseY - RungHalf - 0.1f, 0),
                new Vector3(TribeLadderX, Rung(TribeLadderX, LadderRows - 1).y + RungHalf + 0.22f, 0), ArrowHead,
                Tint(WarmWhite, 0.32f), 1.1f, IdTitForTat);
        }

        static Vector3 Rung(float x, int row) => new Vector3(x, LadderBaseY + row * LadderStep, 0);

        /// <summary>Share of the best joint payoff that always both defecting earns: the Pareto arrow's red tick.</summary>
        float DefectionShare => pay.R > 0 ? Mathf.Clamp01(pay.P / pay.R) : 0;

        void DrawPyramids(LineMeshBuilder lines)
        {
            DrawPyramid(lines, TribeAX, IdTribeA);
            DrawPyramid(lines, TribeBX, IdTribeB);
        }

        /// <summary>A tribe's pyramid outline (people blue), its apex a little above the alpha.</summary>
        static void DrawPyramid(LineMeshBuilder lines, float x, int id)
        {
            Vector3[] corners =
            {
                new Vector3(x, ApexY + 0.12f, 0), new Vector3(x + HalfBase, BaseY, 0), new Vector3(x - HalfBase, BaseY, 0)
            };
            Polygon(lines, corners, true, Tint(EconomyStyle.People, 0.8f), 1.4f, id, 1.4f);
        }

        // ------------------------------------------------------------------ the floor: evolution

        /// <summary>
        /// The evolution stream: generations run along x, each strategy a band whose depth (z) is its share, stacked in
        /// <see cref="StreamOrder"/> from the front; thin edges between bands and a frame.
        /// </summary>
        void DrawFloor(List<float[]> gens, SurfaceMeshBuilder fills, LineMeshBuilder lines)
        {
            int count = gens.Count, n = StreamOrder.Length;
            if (count < 2) return;
            float depth = FloorFar - FloorNear;
            float[] below = new float[count];
            List<Vector3> inner = new List<Vector3>(count), outer = new List<Vector3>(count);
            List<Color32> colors = new List<Color32>(1) { default };
            List<LinePoint> edge = new List<LinePoint>(count);
            for (int j = 0; j < n; j++)
            {
                Color hue = StrategyColor(StreamOrder[j]);
                colors[0] = Tint(hue, 0.42f);
                inner.Clear();
                outer.Clear();
                edge.Clear();
                for (int k = 0; k < count; k++)
                {
                    float x = Mathf.Lerp(FloorLeft, FloorRight, k / (float)(count - 1));
                    float z0 = FloorNear + depth * below[k];
                    below[k] += gens[k][j];
                    float z1 = FloorNear + depth * Mathf.Min(1f, below[k]);
                    inner.Add(new Vector3(x, FloorY, z0));
                    outer.Add(new Vector3(x, FloorY, z1));
                    edge.Add(new LinePoint(new Vector3(x, FloorY, z1), Tint(hue, 0.55f), 1f));
                }

                fills.AddBand(inner, outer, colors, IdEvolution + j, 1.1f);
                if (j < n - 1) lines.AddPolyline(edge, IdEvolution + j);
            }

            // the frame: front and back edges, generation 0 and the last generation
            Color32 frame = Tint(GraphStyle.Axis, 0.4f);
            lines.AddSegment(new Vector3(FloorLeft, FloorY, FloorNear), new Vector3(FloorRight, FloorY, FloorNear), frame, 1f, 0,
                IdEvolutionFrame);
            lines.AddSegment(new Vector3(FloorLeft, FloorY, FloorFar), new Vector3(FloorRight, FloorY, FloorFar), frame, 1f, 0,
                IdEvolutionFrame);
            lines.AddSegment(new Vector3(FloorLeft, FloorY, FloorNear), new Vector3(FloorLeft, FloorY, FloorFar), frame, 1f, 0,
                IdEvolutionFrame);
            lines.AddSegment(new Vector3(FloorRight, FloorY, FloorNear), new Vector3(FloorRight, FloorY, FloorFar), frame, 1f, 0,
                IdEvolutionFrame);
        }

        /// <summary>Colors of the evolving strategies: defection red, reciprocity warm, kindness cool.</summary>
        static Color StrategyColor(PdStrategy s)
        {
            switch (s)
            {
                case PdStrategy.AlwaysDefect: return Red;
                case PdStrategy.Grim: return GrimColor;
                case PdStrategy.TitForTat: return WarmWhite;
                case PdStrategy.GenerousTitForTat: return Gold;
                case PdStrategy.WinStayLoseShift: return PaleSteel;
                case PdStrategy.AlwaysCooperate: return PaleBlue;
                default: return GraphStyle.TextDim;
            }
        }

        /// <summary>
        /// Each band's label where it is widest (in Prepare, or on the main thread after a recompute). Priorities are set
        /// only the first time: the label system sorts by priority when it takes the labels, not afterwards.
        /// </summary>
        void PlaceBandLabels(List<float[]> gens)
        {
            int count = gens.Count;
            if (count < 2) return;
            float depth = FloorFar - FloorNear;
            int from = Mathf.FloorToInt(LabelMargin * (count - 1)), to = Mathf.CeilToInt((1 - LabelMargin) * (count - 1));
            for (int j = 0; j < StreamOrder.Length; j++)
            {
                int best = from;
                for (int k = from; k <= to; k++)
                {
                    if (gens[k][j] > gens[best][j]) best = k;
                }

                float below = 0;
                for (int i = 0; i < j; i++) below += gens[best][i];
                float share = gens[best][j];
                float x = Mathf.Lerp(FloorLeft, FloorRight, best / (float)(count - 1));
                LabelSpec label = bandLabels[j];
                label.Data = station.World(x, FloorY, FloorNear + depth * (below + share * 0.5f));
                label.Hidden = !labelsShown || share < MinBandLabelShare;
                if (!uploaded) label.Priority = 12 + 20 * share;
                if (StreamOrder[j] == PdStrategy.GenerousTitForTat && forgivenessAnchor != null) forgivenessAnchor.Fixed = label.Data;
            }
        }

        // ------------------------------------------------------------------ labels and anchors

        void AddLabels(GraphContext ctx)
        {
            Color text = GraphStyle.Text, dim = GraphStyle.TextDim;
            TextAlignmentOptions center = TextAlignmentOptions.Center, left = TextAlignmentOptions.Left,
                right = TextAlignmentOptions.Right;
            IdRange nash = IdRange.Single(IdNash);

            // one round
            Vector3 c = NashCenter;
            float h = NashHalf;
            Add(ctx, "One round: the Nash equilibrium", new Vector3(c.x, c.y + h + 0.28f, 0), 14, text, center, 34, "game:nash",
                nash);
            Add(ctx, "Defecting always pays more, so both defect", new Vector3(c.x, c.y - h - 0.24f, 0), 12, dim, center, 30,
                "game:nash", nash);
            Add(ctx, Pair(pay.R, pay.R), CellCenter(c, h, h, DiamondCell.Top), 12, text, center, 26, "game:nash", nash);
            Add(ctx, Pair(pay.P, pay.P), CellCenter(c, h, h, DiamondCell.Bottom), 12, Color.white, center, 26, "game:nash", nash);
            Add(ctx, Pair(pay.S, pay.T), CellCenter(c, h, h, DiamondCell.Left), 12, text, center, 26, "game:nash", nash);
            Add(ctx, Pair(pay.T, pay.S), CellCenter(c, h, h, DiamondCell.Right), 12, text, center, 26, "game:nash", nash);
            // the moves along the edges: the upper-left edge holds A's cooperating cells, the lower-right A's defecting
            // ones; B's run the other way
            Add(ctx, "A cooperates", EdgeMid(c, h, h, DiamondCell.Left), 11, dim, right, 20, "game:nash", nash, new Vector2(-4, 6));
            Add(ctx, "B cooperates", EdgeMid(c, h, h, DiamondCell.Top), 11, dim, left, 20, "game:nash", nash, new Vector2(4, 6));
            Add(ctx, "A defects", EdgeMid(c, h, h, DiamondCell.Right), 11, dim, left, 20, "game:nash", nash, new Vector2(4, -6));
            Add(ctx, "B defects", EdgeMid(c, h, h, DiamondCell.Bottom), 11, dim, right, 20, "game:nash", nash, new Vector2(-4, -6));

            // many rounds: two people
            IdRange ladder = new IdRange(IdLadder, IdPersonB);
            float midLeft = 0.5f * (BrainA.x + BrainB.x);
            personHeading = Add(ctx, "Many rounds: tit for tat and the shadow of the future", new Vector3(midLeft, HeadingY, 0),
                14, text, center, 34, "game:ladder", ladder);
            shadowLabel = Add(ctx, ShadowText(settings), new Vector3(midLeft, CaptionY, 0), 12, dim, center, 30, "game:ladder",
                ladder);
            personALabel = Add(ctx, PersonText("A", settings.A), new Vector3(BrainA.x, BrainA.y - BrainRadius - 0.2f, 0), 13,
                text, center, 32, "game:ladder", IdRange.Single(IdPersonA));
            personBLabel = Add(ctx, PersonText("B", settings.B), new Vector3(BrainB.x, BrainB.y - BrainRadius - 0.2f, 0), 13,
                text, center, 32, "game:ladder", IdRange.Single(IdPersonB));
            scoreLabel = Add(ctx, ScoreText(), new Vector3(PersonLadderX, LadderBaseY - RungHalf - 0.17f, 0), 12, text, center,
                29, "game:ladder", ladder);
            IdRange pareto = IdRange.Single(IdPareto);
            Add(ctx, "PARETO EFFICIENT", new Vector3(ParetoX, ParetoTop + ArrowHead + 0.13f, 0), 12, Gold, center, 31,
                "game:pareto", pareto);
            efficiencyLabel = Add(ctx, Percents[0], new Vector3(ParetoX, ParetoBottom, 0), 12, Gold, left, 28, "game:pareto",
                pareto, new Vector2(11, 0), true);

            // many rounds: two tribes
            IdRange tribes = new IdRange(IdTribeA, IdTitForTat);
            float midRight = 0.5f * (TribeAX + TribeBX);
            tribeHeading = Add(ctx, "Many rounds: tribe against tribe", new Vector3(midRight, HeadingY, 0), 14, text, center, 34,
                "game:tribes", tribes);
            tribeCaption = Add(ctx, TribeCaption(), new Vector3(midRight, CaptionY, 0), 12, dim, center, 30, "game:tribes",
                tribes);
            tribeALabel = Add(ctx, TribeText("A", -1), new Vector3(TribeAX, BaseY - 0.22f, 0), 13, text, center, 32,
                "game:tribes", IdRange.Single(IdTribeA));
            tribeBLabel = Add(ctx, TribeText("B", -1), new Vector3(TribeBX, BaseY - 0.22f, 0), 13, text, center, 32,
                "game:tribes", IdRange.Single(IdTribeB));
            Add(ctx, GamesSetup.Describe(membersA, 3), new Vector3(TribeAX, BaseY - 0.45f, 0), 11, dim, center, 22,
                "game:tribes", IdRange.Single(IdTribeA));
            Add(ctx, GamesSetup.Describe(membersB, 3), new Vector3(TribeBX, BaseY - 0.45f, 0), 11, dim, center, 22,
                "game:tribes", IdRange.Single(IdTribeB));
            Add(ctx, "alpha", dotsA[0], 11, dim, right, 18, "game:tribes", IdRange.Single(IdTribeA), new Vector2(-24, 0));
            Add(ctx, "alpha", dotsB[0], 11, dim, left, 18, "game:tribes", IdRange.Single(IdTribeB), new Vector2(24, 0));
            float spineTop = Rung(TribeLadderX, LadderRows - 1).y + RungHalf + 0.22f;
            Add(ctx, "TIT FOR TAT", new Vector3(TribeLadderX, spineTop + 0.13f, 0), 12, WarmWhite, center, 31, "game:tribes",
                IdRange.Single(IdTitForTat));
            tribeRoundLabel = Add(ctx, "Round 0 of " + TribeRoundsPerGame.ToString(Ci),
                new Vector3(TribeLadderX, LadderBaseY - RungHalf - 0.17f, 0), 12, text, center, 29, "game:tribes", tribes);
            for (int row = 0; row < LadderRows; row++)
            {
                Vector3 rung = Rung(TribeLadderX, row);
                IdRange ids = IdRange.Single(IdTribeLadder);
                rowLabelsA[row] = Add(ctx, Percents[0], new Vector3(rung.x - RungHalf - 0.05f, rung.y, 0), 11, dim, right, 12,
                    "game:tribes", ids, default, true);
                rowLabelsB[row] = Add(ctx, Percents[0], new Vector3(rung.x + RungHalf + 0.05f, rung.y, 0), 11, dim, left, 12,
                    "game:tribes", ids, default, true);
            }

            // the floor
            IdRange evolutionIds = new IdRange(IdEvolution, IdEvolutionFrame);
            floorCaption = Add(ctx, FloorText(settings), new Vector3(FloorCaptionX, FloorY, FloorFar + 0.2f), 12, dim, center, 27,
                "game:evolution", evolutionIds);
            // the bands' labels are handed to the label system by Prepare once PlaceBandLabels has placed and ranked them:
            // the label system sorts labels by priority when it takes them, possibly while this thread is still running
            for (int j = 0; j < StreamOrder.Length; j++)
            {
                PdStrategy s = StreamOrder[j];
                string anchor = s == PdStrategy.GenerousTitForTat ? "game:forgiveness" : "game:evolution";
                bandLabels[j] = Label(PrisonersDilemma.Name(s), new Vector3(0, FloorY, FloorNear), 12, text, center, 12, anchor,
                    IdRange.Single(IdEvolution + j));
            }
        }

        /// <summary>A label at a local point of the station, handed to the label system finished (hidden or not).</summary>
        LabelSpec Add(GraphContext ctx, string text, Vector3 local, float size, Color color, TextAlignmentOptions align,
            float priority, string anchor, IdRange ids, Vector2 offset = default, bool hidden = false)
        {
            LabelSpec spec = Label(text, local, size, color, align, priority, anchor, ids, offset);
            spec.Hidden = hidden;
            ctx.Labels.Add(spec);
            labels.Add(spec);
            return spec;
        }

        /// <summary>A label at a local point of the station (a world position: the station is not warped).</summary>
        LabelSpec Label(string text, Vector3 local, float size, Color color, TextAlignmentOptions align, float priority,
            string anchor, IdRange ids, Vector2 offset = default) =>
            new LabelSpec
            {
                Text = text,
                Data = station.World(local),
                Fixed = true,
                SizePx = size,
                Color = color,
                Align = align,
                Priority = priority,
                AnchorKey = anchor,
                Ids = ids,
                PixelOffset = offset
            };

        void RegisterAnchors(int tribeSize)
        {
            string r = Num(pay.R), t = Num(pay.T), p = Num(pay.P), s = Num(pay.S);
            Register("game:nash", "One round: the Nash equilibrium",
                $"The prisoner's dilemma played once. Whatever the other does, defecting pays more ({t} instead of {r} " +
                $"when the other cooperates, {p} instead of {s} when the other defects), so two players who look only at " +
                $"this round both defect and get {p} each, although cooperating would give both {r}. Both defecting is the " +
                "Nash equilibrium: neither gains by changing alone.",
                NashCenter, IdRange.Single(IdNash));
            Register("game:ladder", "Many rounds: the shadow of the future",
                "Two people who expect to meet again. After every round they meet once more with chance w, so a game " +
                "lasts 1 / (1 - w) rounds on average. Each diamond is a round: the light top cell means both cooperated, " +
                "the red bottom both defected, a split side cell that one exploited the other (light on the side that " +
                "cooperated). A spark marks a mistake: a move that came out the opposite of what was meant. Tit for tat " +
                $"cannot be beaten by defectors once w is at least {Num(pay.ShadowThreshold, "0.00")} (Axelrod 1984).",
                Rung(PersonLadderX, LadderRows / 2), new IdRange(IdLadder, IdPersonB));
            Register("game:pareto", "Pareto efficient",
                $"How close the pair comes to the best they can do together: their joint payoff so far divided by what " +
                $"always cooperating pays ({r} each per round). No outcome makes both better off than mutual cooperation; " +
                $"defecting forever earns {Percent(DefectionShare)} of it (the red tick).",
                new Vector3(ParetoX, ParetoTop, 0), IdRange.Single(IdPareto));
            string members = membersSource == GamesSetup.Source.Population
                ? "Their members are adults of the modeled population in 2025, Democrats in tribe A and Republicans in " +
                  "tribe B, playing the strategies their personalities give them; the highest earner leads."
                : "Their members play the population's mix of strategies" +
                  (mixSource == GamesSetup.Source.Data ? " from the games data." : " (an illustrative default).");
            Register("game:tribes", "Tribe against tribe",
                $"Two tribes of {tribeSize}. Every round each member of tribe A meets a random member of tribe B. Members " +
                "follow their own strategy, but what they remember is what their tribe received last round, so a few " +
                "defections are repaid by many. Groups compete harder than individuals: distrust of the other tribe adds " +
                $"{Percent(distrust)} to every member's chance of defecting, and after the alpha defects the members " +
                $"follow with {Percent(alphaSway)} more. " + members +
                " Each diamond's cells are filled by how likely each outcome was.",
                new Vector3(TribeLadderX, 2.5f, 0), new IdRange(IdTribeA, IdTitForTat));
            evolutionAnchor = Register("game:evolution", "Evolution of strategies", EvolutionBlurb(evolution),
                new Vector3(0, FloorY, 0.5f * (FloorNear + FloorFar)), new IdRange(IdEvolution, IdEvolutionFrame));
            int generous = Array.IndexOf(StreamOrder, PdStrategy.GenerousTitForTat);
            forgivenessAnchor = Register("game:forgiveness", "Forgiveness",
                "Generous tit for tat forgives a defection with chance " + Num(pay.Forgiveness, "0.00") + ": the most " +
                "forgiveness that still keeps exploiters from profiting (Nowak and Sigmund 1992). With mistakes, plain tit " +
                "for tat falls into echoes of retaliation and grim never forgives at all; forgiving ends the feud a mistake " +
                "started, which is why the generous do well when errors are common.",
                bandLabels[generous].Data, IdRange.Single(IdEvolution + generous), true);
        }

        Anchor Register(string key, string label, string blurb, Vector3 local, IdRange ids, bool world = false)
        {
            Anchor a = new Anchor
            {
                Key = key,
                Label = label,
                Blurb = blurb,
                Level = GraphLevel.Humans,
                YearsAgo = 0.5,
                EndYearsAgo = 0,
                Y = local.y,
                Rho = EconomyStyle.FramingRho,
                Ids = ids,
                Tier = 1,
                Fixed = world ? local : station.World(local)
            };
            Anchors.Register(a);
            return a;
        }

        // ------------------------------------------------------------------ texts

        static string Num(float v, string format = "0.#") => v.ToString(format, Ci);

        static string Pair(float a, float b) => Num(a) + ", " + Num(b);

        static string PersonText(string who, PdStrategy s) => "Person " + who + ": " + PrisonersDilemma.Name(s);

        string ShadowText(Settings s)
        {
            float w = s.Continuation;
            int mean = w < MaxContinuation ? Mathf.RoundToInt(1f / (1f - w)) : 0;
            string length = mean == 1 ? "a single round" : mean > 1 ? "about " + mean.ToString(Ci) + " rounds" : "many rounds";
            return "Meet again with chance w = " + w.ToString("0.00", Ci) + " (" + length + "); tit for tat holds when w >= " +
                   pay.ShadowThreshold.ToString("0.00", Ci);
        }

        string TribeCaption() =>
            "Members answer what their tribe received last round  \u00B7  distrust " + Percent(distrust) +
            "  \u00B7  alpha sway " + Percent(alphaSway);

        static string TribeText(string who, float cooperation) =>
            cooperation < 0 ? "Tribe " + who : "Tribe " + who + "  \u00B7  cooperates " + Percent(cooperation);

        static string FloorText(Settings s) =>
            "Evolution: strategies spread by their payoff (noise " + Percent(s.Noise) + ", w " +
            s.Continuation.ToString("0.00", Ci) + ")";

        string ScoreText()
        {
            int n = rounds.Count;
            string head = "Round " + n.ToString(Ci) + (n > 0 && n >= personLength ? ", the last" : "");
            return head + "     A " + totalA.ToString("0", Ci) + " : B " + totalB.ToString("0", Ci);
        }

        string EvolutionBlurb(List<float[]> gens)
        {
            StringBuilder sb = new StringBuilder(
                "Strategies spread in proportion to the payoff they earn against the current mix (replicator dynamics, " +
                Num(100 * mutation) + "% mutation), starting from the population's mix on the left. Each band's width " +
                "is a strategy's share. ");
            if (gens != null && gens.Count > 0)
            {
                float[] last = gens[gens.Count - 1];
                sb.Append("After ").Append((gens.Count - 1).ToString(Ci)).Append(" generations: ");
                bool[] used = new bool[last.Length];
                for (int item = 0; item < 3; item++)
                {
                    int best = -1;
                    for (int i = 0; i < last.Length; i++)
                    {
                        if (!used[i] && (best < 0 || last[i] > last[best])) best = i;
                    }

                    if (best < 0) break;
                    used[best] = true;
                    if (item > 0) sb.Append(", ");
                    sb.Append(GamesSetup.ShortName(StreamOrder[best])).Append(' ').Append(Percent(last[best]));
                }

                sb.Append('.');
            }

            return sb.ToString();
        }

        /// <summary>A share as a whole percent, clamped to 0% .. 100% (no number formatting).</summary>
        static string Percent(float share) => Percents[Mathf.Clamp(Mathf.RoundToInt(100 * share), 0, 100)];

        static string[] BuildPercents()
        {
            string[] s = new string[101];
            for (int i = 0; i <= 100; i++) s[i] = i.ToString(CultureInfo.InvariantCulture) + "%";
            return s;
        }

        // ------------------------------------------------------------------ upload (main thread)

        public override void Upload(GraphContext ctx)
        {
            if (staticFills == null) return;
            int q = EconomyStyle.QueueStations;
            fillMat = Fills(q);
            floorFillMat = Fills(q + 1);
            liveFillMat = Fills(q + 2);
            floorLineMat = Lines(q + 3);
            lineMat = Lines(q + 4);
            liveLineMat = Lines(q + 5);

            AddStationMesh("GamesFrame", staticFills.ToMesh("GamesFrame"), fillMat);
            AddStationMesh("GamesFrameLines", staticLines.ToMesh("GamesFrameLines"), lineMat);
            floorFill = AddStationMesh("GamesEvolution", floorFills.ToMesh("GamesEvolution"), floorFillMat);
            floorLine = AddStationMesh("GamesEvolutionLines", floorLines.ToMesh("GamesEvolutionLines"), floorLineMat);
            staticFills = floorFills = null;
            staticLines = floorLines = null;

            // the games start from the data's mistakes and shadow of the future. EconomyLoaderLayer reset the state after
            // Prepare had read it (it may have held a previous visit's choices), so the settings are taken again here;
            // StartGames recomputes the evolution if they differ from the ones Prepare evolved
            if (specNoise || specContinuation)
            {
                EconomyState.SetGames(EconomyState.StrategyA, EconomyState.StrategyB,
                    specNoise ? dataNoise : EconomyState.Noise, specContinuation ? dataContinuation : EconomyState.Continuation);
            }

            settings = Settings.Current;
            seenVersion = EconomyState.Version;
            StartGames();
            BuildLive(out Mesh fill, out Mesh line);
            liveFill = AddStationMesh("GamesLive", fill, liveFillMat);
            liveLine = AddStationMesh("GamesLiveLines", line, liveLineMat);
            liveDirty = false;
            uploaded = true;
        }

        static Material Fills(int queue)
        {
            Material m = GraphMaterials.Raw(GraphMaterials.Surface(Color.white, 1f, queue));
            m.SetFloat("_EdgeSoft", 0f);
            return m;
        }

        static Material Lines(int queue) => GraphMaterials.Raw(GraphMaterials.Line(Color.white, 1f, queue));

        MeshFilter AddStationMesh(string meshName, Mesh mesh, Material material)
        {
            MeshRenderer r = AddMesh(meshName, mesh, material);
            station.Place(r.transform);
            return r.GetComponent<MeshFilter>();
        }

        /// <summary>Shows a rebuilt mesh and destroys the one it replaces.</summary>
        void Replace(MeshFilter filter, Mesh mesh)
        {
            if (filter == null)
            {
                Destroy(mesh);
                return;
            }

            Mesh old = filter.sharedMesh;
            filter.sharedMesh = mesh;
            if (old != null) Destroy(old);
        }

        void OnDestroy()
        {
            foreach (MeshFilter f in new[] { liveFill, liveLine, floorFill, floorLine })
            {
                if (f != null && f.sharedMesh != null) Destroy(f.sharedMesh);
            }
        }

        // ------------------------------------------------------------------ live (main thread)

        public override void Tick(GraphContext ctx, CameraRig rig)
        {
            if (!uploaded) return;
            if (EconomyState.Version != seenVersion)
            {
                seenVersion = EconomyState.Version;
                Settings now = Settings.Current;
                if (!now.Same(settings))
                {
                    settings = now;
                    StartGames();
                }
            }

            PollEvolution();

            // play only while the station is in view; otherwise the last state stays drawn
            Vector3 fromCenter = rig != null ? rig.Pose.Target - station.World(CenterLocal) : Vector3.zero;
            FrameLabels(fromCenter);
            if (fromCenter.sqrMagnitude < NearDistance * NearDistance)
            {
                Advance(Mathf.Min(Time.unscaledDeltaTime, MaxFrameSeconds));
            }

            if (!liveDirty) return;
            liveDirty = false;
            RebuildLive();
        }

        /// <summary>
        /// Which labels the view has room for, from the camera's target relative to the station's center: none while the
        /// camera looks at another station (<see cref="LabelDistance"/>); on a portrait screen, where only the half the
        /// camera looks at fits across and the two halves' captions would run into each other at its edge, not the other
        /// half's heading and caption.
        /// </summary>
        void FrameLabels(Vector3 fromCenter)
        {
            bool shown = fromCenter.sqrMagnitude < LabelDistance * LabelDistance;
            bool portrait = ScreenLayout.IsPortrait, lookingRight = Vector3.Dot(fromCenter, station.Right) > 0;
            bool hidePersons = portrait && lookingRight, hideTribes = portrait && !lookingRight;
            if (shown == labelsShown && hidePersons == hidePersonHeadings && hideTribes == hideTribeHeadings) return;
            labelsShown = shown;
            hidePersonHeadings = hidePersons;
            hideTribeHeadings = hideTribes;

            foreach (LabelSpec label in labels) label.Hidden = !shown;
            if (shown)
            {
                personHeading.Hidden = shadowLabel.Hidden = hidePersons;
                tribeHeading.Hidden = tribeCaption.Hidden = hideTribes;
            }

            PlaceBandLabels(evolution); // the bands' labels by their width
            liveDirty = true; // the live labels (efficiency, the tribes' rows) with this frame's rebuild
            labelSystem.MarkDirty();
        }

        /// <summary>Restarts both games for the current settings and, if they changed it, the evolution.</summary>
        void StartGames()
        {
            personGame = 0;
            tribeGame = 0;
            StartPersonGame();
            StartTribeGame();
            roundClock = 0;
            labelSystem.SetText(personALabel, PersonText("A", settings.A));
            labelSystem.SetText(personBLabel, PersonText("B", settings.B));
            labelSystem.SetText(shadowLabel, ShadowText(settings));
            if (!settings.SameEvolution(evolvedFor)) StartEvolution();
        }

        void StartPersonGame()
        {
            personRng = new System.Random(PersonSeed + personGame * SeedStride);
            personLength = PrisonersDilemma.SampleRounds(settings.Continuation, personRng, MaxPersonRounds);
            rounds.Clear();
            memoryA = default;
            memoryB = default;
            totalA = totalB = 0;
            personHold = 0;
            liveDirty = true;
        }

        void StartTribeGame()
        {
            tribe = new TribeGame(membersA, membersB, pay, settings.Noise, TribeSeed + tribeGame * SeedStride)
            {
                Distrust = distrust,
                AlphaSway = alphaSway
            };
            tribeHold = 0;
            liveDirty = true;
        }

        void Advance(float dt)
        {
            // a finished game holds, then the next one (a new seed) starts
            if (rounds.Count >= personLength)
            {
                personHold += dt;
                if (personHold >= HoldSeconds)
                {
                    personGame++;
                    StartPersonGame();
                }
            }

            if (tribe.Rounds.Count >= TribeRoundsPerGame)
            {
                tribeHold += dt;
                if (tribeHold >= HoldSeconds)
                {
                    tribeGame++;
                    StartTribeGame();
                }
            }

            roundClock += dt;
            if (roundClock < RoundSeconds) return;
            roundClock = Mathf.Min(roundClock - RoundSeconds, RoundSeconds);

            if (rounds.Count < personLength)
            {
                PdRound r = PrisonersDilemma.Step(settings.A, settings.B, ref memoryA, ref memoryB, settings.Noise, pay, personRng);
                rounds.Add(r);
                totalA += r.PayA;
                totalB += r.PayB;
                liveDirty = true;
            }

            if (tribe.Rounds.Count < TribeRoundsPerGame)
            {
                tribe.Step();
                liveDirty = true;
            }
        }

        void RebuildLive()
        {
            BuildLive(out Mesh fill, out Mesh line);
            Replace(liveFill, fill);
            Replace(liveLine, line);
        }

        /// <summary>Both games as they stand (meshes and live labels).</summary>
        void BuildLive(out Mesh fill, out Mesh line)
        {
            SurfaceMeshBuilder fills = new SurfaceMeshBuilder();
            LineMeshBuilder lines = new LineMeshBuilder(LivePoints);
            DrawPersonGame(fills, lines);
            DrawTribeGame(fills, lines);
            fill = fills.ToMesh("GamesLive");
            line = lines.ToMesh("GamesLiveLines");
            labelSystem.MarkDirty();
        }

        /// <summary>The persons' ladder (latest round on top), each brain's thread to its move, the Pareto arrow.</summary>
        void DrawPersonGame(SurfaceMeshBuilder fills, LineMeshBuilder lines)
        {
            int n = rounds.Count, first = Math.Max(0, n - LadderRows);
            for (int k = first; k < n; k++)
            {
                int row = k - first;
                float fade = first > 0 && row < ScrollFade.Length ? ScrollFade[row] : 1f;
                Vector3 c = Rung(PersonLadderX, row);
                PdRound r = rounds[k];
                FillOutcome(fills, c, r.A == PdMove.Cooperate ? 1f : 0f, r.B == PdMove.Cooperate ? 1f : 0f, fade, IdLadder);
                bool latest = k == n - 1;
                DiamondOutline(lines, c, RungHalf, RungHalf, Tint(GraphStyle.Text, (latest ? 0.95f : 0.42f) * fade),
                    latest ? 1.6f : 1f, IdLadder, latest ? 1.3f : 1f);
                if (r.ErrorA) Spark(lines, Tip(c, RungHalf, RungHalf, DiamondCell.Left) + new Vector3(-0.07f, 0, 0), 0.06f,
                    Tint(SparkColor, fade), 1.4f, IdLadder, 2.2f);
                if (r.ErrorB) Spark(lines, Tip(c, RungHalf, RungHalf, DiamondCell.Right) + new Vector3(0.07f, 0, 0), 0.06f,
                    Tint(SparkColor, fade), 1.4f, IdLadder, 2.2f);
            }

            if (n > 0)
            {
                PdRound last = rounds[n - 1];
                Vector3 c = Rung(PersonLadderX, n - 1 - first);
                MoveThread(lines, BrainA, Tip(c, RungHalf, RungHalf, DiamondCell.Left), last.A, IdPersonA);
                MoveThread(lines, BrainB, Tip(c, RungHalf, RungHalf, DiamondCell.Right), last.B, IdPersonB);
            }

            // the Pareto arrow: joint payoff so far over always cooperating
            float best = 2 * pay.R * n;
            float efficiency = best > 0 ? Mathf.Clamp01((totalA + totalB) / best) : 0;
            float tip = Mathf.Lerp(ParetoBottom, ParetoTop, efficiency);
            if (n > 0)
            {
                lines.AddSegment(new Vector3(ParetoX, ParetoBottom, 0), new Vector3(ParetoX, tip, 0), Tint(Gold, 0.9f), 4f, 0,
                    IdPareto, 1.25f);
                lines.AddSegment(new Vector3(ParetoX - 0.09f, tip, 0), new Vector3(ParetoX + 0.09f, tip, 0), Tint(Gold, 1f), 1.6f,
                    0, IdPareto, 1.5f);
            }

            labelSystem.SetText(scoreLabel, ScoreText());
            labelSystem.SetText(efficiencyLabel, Percent(efficiency));
            efficiencyLabel.Data = station.World(ParetoX, tip, 0);
            efficiencyLabel.Hidden = !labelsShown || n == 0;
        }

        /// <summary>
        /// A thread from a brain to the tip of its latest move, in the move's color: it leaves the brain sideways toward the
        /// ladder and bends down (or up) to the tip, so it never runs through the person's label under the brain.
        /// </summary>
        static void MoveThread(LineMeshBuilder lines, Vector3 brain, Vector3 tip, PdMove move, int id)
        {
            float side = Mathf.Sign(tip.x - brain.x);
            Vector3 start = brain + new Vector3(side * (BrainRadius + 0.04f), 0, 0);
            Vector3 end = tip + new Vector3(-side * 0.03f, 0, 0);
            Color hue = move == PdMove.Cooperate ? Light : Red;
            Curve(lines, start, new Vector3(end.x, start.y, 0), end, ThreadSegments, Tint(hue, 0.6f), 1.3f, id, 1.2f);
        }

        /// <summary>
        /// Fills a round's cells by how likely each outcome was, given the chance that A and B cooperated (0 or 1 for a
        /// played round of two people): top both cooperate (light), bottom both defect (red), the side cells one each,
        /// light on the cooperator's side (A left, B right) and red on the defector's.
        /// </summary>
        static void FillOutcome(SurfaceMeshBuilder fills, Vector3 c, float coopA, float coopB, float fade, int id)
        {
            const float strength = 0.85f;
            float both = coopA * coopB, neither = (1 - coopA) * (1 - coopB);
            float onlyA = coopA * (1 - coopB), onlyB = (1 - coopA) * coopB;
            if (both > 0.01f) FillCell(fills, c, RungHalf, RungHalf, DiamondCell.Top, Tint(Light, strength * both * fade), id);
            if (neither > 0.01f)
            {
                FillCell(fills, c, RungHalf, RungHalf, DiamondCell.Bottom, Tint(Red, strength * neither * fade), id, 1.15f);
            }

            if (onlyA > 0.01f)
            {
                float a = strength * onlyA * fade;
                FillHalfCell(fills, c, RungHalf, RungHalf, DiamondCell.Left, true, Tint(Light, a), id);
                FillHalfCell(fills, c, RungHalf, RungHalf, DiamondCell.Left, false, Tint(Red, a), id, 1.15f);
            }

            if (onlyB > 0.01f)
            {
                float a = strength * onlyB * fade;
                FillHalfCell(fills, c, RungHalf, RungHalf, DiamondCell.Right, false, Tint(Red, a), id, 1.15f);
                FillHalfCell(fills, c, RungHalf, RungHalf, DiamondCell.Right, true, Tint(Light, a), id);
            }
        }

        /// <summary>The tribes' ladder with each round's cooperation shares, and every member colored by its last move.</summary>
        void DrawTribeGame(SurfaceMeshBuilder fills, LineMeshBuilder lines)
        {
            List<TribeGame.Round> played = tribe.Rounds;
            int n = played.Count, first = Math.Max(0, n - LadderRows);
            for (int row = 0; row < LadderRows; row++)
            {
                int k = first + row;
                bool shown = k < n;
                rowLabelsA[row].Hidden = rowLabelsB[row].Hidden = !labelsShown || !shown;
                if (!shown) continue;
                TribeGame.Round r = played[k];
                float fade = first > 0 && row < ScrollFade.Length ? ScrollFade[row] : 1f;
                Vector3 c = Rung(TribeLadderX, row);
                FillOutcome(fills, c, r.CoopAB, r.CoopBA, fade, IdTribeLadder);
                bool latest = k == n - 1;
                DiamondOutline(lines, c, RungHalf, RungHalf, Tint(GraphStyle.Text, (latest ? 0.95f : 0.42f) * fade),
                    latest ? 1.6f : 1f, IdTribeLadder, latest ? 1.3f : 1f);
                labelSystem.SetText(rowLabelsA[row], Percent(r.CoopAB));
                labelSystem.SetText(rowLabelsB[row], Percent(r.CoopBA));
            }

            DrawMembers(fills, lines, true, dotsA, IdTribeA);
            DrawMembers(fills, lines, false, dotsB, IdTribeB);

            TribeGame.Round now = n > 0 ? played[n - 1] : default;
            labelSystem.SetText(tribeALabel, TribeText("A", n > 0 ? now.CoopAB : -1));
            labelSystem.SetText(tribeBLabel, TribeText("B", n > 0 ? now.CoopBA : -1));
            labelSystem.SetText(tribeRoundLabel, "Round " + n.ToString(Ci) + " of " + TribeRoundsPerGame.ToString(Ci));
        }

        /// <summary>A tribe's members: light after cooperating, red after defecting, grey before their first round.</summary>
        void DrawMembers(SurfaceMeshBuilder fills, LineMeshBuilder lines, bool tribeA, Vector3[] dots, int id)
        {
            for (int i = 0; i < dots.Length; i++)
            {
                bool played = tribe.TryLastMove(tribeA, i, out PdMove move);
                Color32 color = !played ? Tint(GraphStyle.TextDim, 0.55f)
                    : move == PdMove.Cooperate ? Tint(Light, 0.95f) : Tint(Red, 0.95f);
                bool alpha = i == 0;
                Disc(fills, dots[i], alpha ? alphaRadius : memberRadius, alpha ? 20 : 12, color, id, played ? 1.2f : 1f);
                if (alpha) Circle(lines, dots[i], alphaRadius * 1.4f, 24, Tint(GraphStyle.Text, 0.7f), 1.2f, id, 1.2f);
            }
        }

        // ------------------------------------------------------------------ the evolution, recomputed off the main thread

        void StartEvolution()
        {
            if (evolving != null) return; // one at a time; PollEvolution starts the next for the latest settings
            Settings s = settings;
            evolvingFor = s;
            evolving = Task.Run(() => Evolve(s));
        }

        void PollEvolution()
        {
            if (evolving == null || !evolving.IsCompleted) return;
            Task<List<float[]>> done = evolving;
            evolving = null;
            if (done.IsFaulted)
            {
                Debug.LogError("[Why] GamesLayer: evolution failed: " + done.Exception?.GetBaseException());
                evolvedFor = evolvingFor; // do not retry the same settings every frame
                return;
            }

            if (!settings.SameEvolution(evolvingFor))
            {
                // the settings moved on while this ran: evolve for them, unless they came back to what is drawn
                if (!settings.SameEvolution(evolvedFor)) StartEvolution();
                return;
            }

            evolution = done.Result;
            evolvedFor = evolvingFor;
            SurfaceMeshBuilder fills = new SurfaceMeshBuilder();
            LineMeshBuilder lines = new LineMeshBuilder(Generations * (StreamOrder.Length + 2) + 16);
            DrawFloor(evolution, fills, lines);
            Replace(floorFill, fills.ToMesh("GamesEvolution"));
            Replace(floorLine, lines.ToMesh("GamesEvolutionLines"));
            PlaceBandLabels(evolution);
            labelSystem.SetText(floorCaption, FloorText(evolvedFor));
            if (evolutionAnchor != null) evolutionAnchor.Blurb = EvolutionBlurb(evolution);
            labelSystem.MarkDirty();
        }
    }
}
