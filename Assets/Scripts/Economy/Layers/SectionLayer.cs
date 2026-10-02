using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using Why.Economy.Data;
using Why.Economy.Land;
using Why.Economy.Model;
using Why.Humans.Smv;
using Debug = UnityEngine.Debug;

namespace Why.Economy.Layers
{
    /// <summary>
    /// The cut and the transition (SPEC 1.1, 1.4). A glowing frame stands across the road at the cut's year
    /// (<see cref="EconomyState.CutYear"/>: the scrubber's preview while it is dragged, else the selected year), and in it
    /// the year's section: the card of 25 industry bars (the wall's bands at the year, split as the wall into wages,
    /// upkeep and what owners keep; the wall's highlight ids, so a band lights its bar) and one dot for every lifeline
    /// alive in the year (adults 2.6 px, children faint 1.6 px; gold while in control; the lifeline's own highlight id).
    /// A readout under the frame names the year, its GDP, the adults and the share in control; in the overview four dashed
    /// callouts run from the frame's corners to the bowl's near rim. A new year slides the frame along the road over
    /// <see cref="LandStyle.YearSlideSeconds"/> (the frame rebuilt every frame from four points; the card and the dots
    /// rebuilt once, shown on arrival).
    ///
    /// While the land unfolds or folds (0 &lt; <see cref="LandView.MorphTime"/> &lt; 2.4 s) this layer alone draws the
    /// moving geometry, from two meshes of fixed topology rewritten every frame (no other layer morphs): (A) the card and
    /// its dots lift along the road to the bowl's center line; (B) every bar lies back into a tile of the staircase on
    /// the far radial line, its tier's tiles side by side, its height turning radial (w = h^(1−s) w_t^s) and its length
    /// k·A/w, so every tile's area is k(s) times its value added at every instant; (C) each ring's row curls around the
    /// bowl's axis (curvature s / r_mid) while k grows to 1 and the strip boundaries move from linear to area-true radii;
    /// (D) each ring spins by its signed angle to its run's center; the dots fly on Bézier arcs to their slots in their
    /// players' discs (<see cref="Figure.DotSlot"/>), player by player around the rim; (E) the mesh fades out as the land
    /// fades in (<see cref="LandView.Reveal"/>). The end geometry is the land's own (the sectors of
    /// <see cref="LandLayout"/>, the glyphs' dot slots), so the cross-fade does not jump. The emphasis group is
    /// <see cref="LandGroup.Cut"/>; the readout is the Cut label group's (7.2, 7.3); anchor <c>land:cut</c>. Order 56.
    /// </summary>
    [GraphScenes(GraphScene.Economy)]
    public sealed class SectionLayer : GraphLayer
    {
        public override int Order => 56;

        /// <summary>The name this layer reports its swaps under (<see cref="LandService.ReportReady"/>).</summary>
        const string ReadyName = "SectionLayer";

        // ------------------------------------------------------------------ looks (the wall's, the land's)

        /// <summary>The wall's band parts (IndustryWallLayer): alphas and intensities of wages, upkeep and owners.</summary>
        const float WallWagesAlpha = 0.22f, WallUpkeepAlpha = 0.09f, WallOwnersAlpha = 0.5f;

        const float WallWagesIntensity = 0.55f, WallUpkeepIntensity = 0.45f, WallOwnersIntensity = 1.35f;

        /// <summary>The wall's neutral grey for upkeep (taxes on production, depreciation).</summary>
        static readonly Color WallUpkeepColor = new Color(0.62f, 0.64f, 0.68f);

        /// <summary>The land's sector strips: steel upkeep, and the owners' intensity per tier (LandscapeLayer).</summary>
        static readonly Color Steel = EconomyStyle.Government;

        static readonly float[] TierIntensity = { 1f, 1f, 1f, 1.1f, 1.35f };

        /// <summary>A bar's edge line and a tile's outline: alpha and width (px).</summary>
        const float CardEdgeAlpha = 0.55f, CardEdgePx = 0.8f, TileEdgeAlpha = 0.55f, TileEdgePx = 1f;

        /// <summary>The frame's color: the cooperation hue whitened this much toward white.</summary>
        const float FrameWhiten = 0.5f;

        /// <summary>A dot is a short stroke this long (land units) across the road, drawn at its pixel width (as the glyphs' dots).</summary>
        const float DotLength = 0.008f;

        /// <summary>The callouts' dashes (land units) and their ends on the near rim: this far either side of 270°.</summary>
        const float CalloutDash = 0.25f, CalloutGap = 0.15f, CalloutSpreadDeg = 30f;

        /// <summary>The readout's size (px), its drop under the frame (px) and its priority (7.3).</summary>
        const float ReadoutPx = 13f, ReadoutDrop = 30f, ReadoutPriority = 36f;

        /// <summary>The morph's tiles are sampled at most this many degrees of their final arc apart.</summary>
        const float TileStepDeg = 4f;

        /// <summary>The strips' lift over their tread (as the land's sectors).</summary>
        const float StripLift = 0.003f;

        /// <summary>The card fades out while it slides to a new year and back in on arrival in this time (s).</summary>
        const float CardFadeSeconds = 0.2f;

        // ------------------------------------------------------------------ the card (pure: built on any thread)

        /// <summary>One industry's bar in the card: its center (land-local), world height and the wall's split.</summary>
        struct Bar
        {
            public Vector3 Center;
            public float Height, Wages, Upkeep;
        }

        /// <summary>One lifeline's dot in the cut: the person, the place (land-local), adult or child, in control, its id.</summary>
        struct Dot
        {
            public int Person;
            public Vector3 At;
            public bool Adult, Control;
            public float Id;
        }

        /// <summary>
        /// The section through one year: the bars, the dots, the frame's bounds (data rho and height), the readout and the
        /// meshes of the static card. Built on any thread from the wall, the population and the lives.
        /// </summary>
        sealed class Card
        {
            public int Year;
            public float RhoLo, RhoHi, YLo, YHi, WallRho;
            public Bar[] Bars;
            public readonly List<Dot> Dots = new List<Dot>(4096);
            public int Adults, Children, InControl;
            public string Readout;
            public readonly SurfaceMeshBuilder Fills = new SurfaceMeshBuilder();
            public readonly LineMeshBuilder Lines = new LineMeshBuilder(8192);
            public double Ms;
        }

        /// <summary>World position (land-local) of a data point of the cut's plane at a clock arc.</summary>
        static Vector3 CutPoint(LandFrame frame, float u, float y, float rho, WarpState w) =>
            frame.Local(GraphWarp.ToWorld(u, y, rho, w));

        /// <summary>
        /// The cut's plane as an affine map from data (height, rho) to land-local points: the road is straight across the
        /// cut, so three warp evaluations place every dot (thousands of warp evaluations would cost most of the card's
        /// budget). Checked against the warp at a fourth point; where the lens bends the plane, every point is warped.
        /// </summary>
        readonly struct CutMap
        {
            readonly Vector3 origin, perY, perRho;
            readonly LandFrame frame;
            readonly float u;
            readonly WarpState w;
            readonly bool affine;

            public CutMap(LandFrame frame, float u, WarpState w)
            {
                this.frame = frame;
                this.u = u;
                this.w = w;
                origin = CutPoint(frame, u, 0, 0, w);
                perY = CutPoint(frame, u, 1, 0, w) - origin;
                perRho = CutPoint(frame, u, 0, 1, w) - origin;
                Vector3 check = CutPoint(frame, u, 0.7f, 1.3f, w);
                affine = (origin + 0.7f * perY + 1.3f * perRho - check).sqrMagnitude < 1e-6f;
            }

            public Vector3 At(float y, float rho) => affine ? origin + y * perY + rho * perRho : CutPoint(frame, u, y, rho, w);
        }

        /// <summary>The clock arc of the cut through a (fractional) year: its middle.</summary>
        static float CutU(double year) => EconomyStage.U(year + 0.5);

        /// <summary>Builds the section through a year (pure, deterministic).</summary>
        static Card BuildCard(int year, EconomyModel model, SmvPopulation pop, WallGeometry wall, LandFrame frame)
        {
            Stopwatch sw = Stopwatch.StartNew();
            Card c = new Card { Year = year };
            EconomyData data = model.Data;
            EconomicLives lives = model.Lives;
            SmvSimulation sim = pop?.Sim;
            WarpState w = EconomyStage.TimelineWarp();
            double mid = year + 0.5;
            float u = CutU(year);
            c.WallRho = wall != null && wall.IsValid ? wall.RhoAt(mid) : EconomyStyle.FramingRho;
            float rhoLo = c.WallRho, rhoHi = c.WallRho, yHi = EconomyStyle.WallTopY;

            // the road's direction across the cut (the bars' width, the dots' strokes)
            Vector3 across = CutPoint(frame, u, EconomyStyle.GroundY, c.WallRho + 0.01f, w) - CutPoint(frame, u, EconomyStyle.GroundY, c.WallRho, w);
            across.y = 0;
            across = across.sqrMagnitude > 1e-12f ? across.normalized : Vector3.right;

            // the bars: the wall's bands at the year's middle, a hair apart between tiers (their edges drawn under the dots)
            IReadOnlyList<Industry> inds = data.Industries;
            c.Bars = new Bar[inds.Count];
            Vector3 half = 0.5f * LandStyle.CardWidth * across;
            List<Vector3> a = new List<Vector3>(2), b = new List<Vector3>(2);
            Color32[] one = new Color32[1];
            List<Vector3> outline = new List<Vector3>(5);
            float[] cuts = new float[4];
            for (int i = 0; i < inds.Count && wall != null && wall.IsValid; i++)
            {
                WallBand band = wall.BandAt(i, mid);
                bool tierTop = i < inds.Count - 1 && inds[i + 1].TierIndex != inds[i].TierIndex;
                float lo = band.WagesLo, hi = Mathf.Max(lo, band.Hi - (tierTop ? LandStyle.CardTierGap : 0));
                float span = Mathf.Max(1e-6f, band.Hi - band.WagesLo);
                Vector3 p0 = CutPoint(frame, u, lo, c.WallRho, w), p1 = CutPoint(frame, u, hi, c.WallRho, w);
                c.Bars[i] = new Bar
                {
                    Center = 0.5f * (p0 + p1), Height = Mathf.Max(1e-4f, p1.y - p0.y),
                    Wages = Mathf.Clamp01((band.UpkeepLo - band.WagesLo) / span), Upkeep = Mathf.Clamp01((band.OwnersLo - band.UpkeepLo) / span)
                };

                Color hue = EconomyStyle.Level(inds[i].Level);
                cuts[0] = lo;
                cuts[1] = Mathf.Min(hi, band.UpkeepLo);
                cuts[2] = Mathf.Min(hi, band.OwnersLo);
                cuts[3] = hi;
                for (int part = 0; part < 3; part++)
                {
                    if (cuts[part + 1] - cuts[part] <= 1e-6f) continue;
                    Vector3 q0 = CutPoint(frame, u, cuts[part], c.WallRho, w), q1 = CutPoint(frame, u, cuts[part + 1], c.WallRho, w);
                    a.Clear();
                    b.Clear();
                    a.Add(q0 - half);
                    a.Add(q1 - half);
                    b.Add(q0 + half);
                    b.Add(q1 + half);
                    one[0] = part == 0 ? LandMath.Tint(LandStyle.Wages, WallWagesAlpha)
                        : part == 1 ? LandMath.Tint(WallUpkeepColor, WallUpkeepAlpha) : LandMath.Tint(hue, WallOwnersAlpha);
                    float intensity = LandStyle.CardIntensity * (part == 0 ? WallWagesIntensity : part == 1 ? WallUpkeepIntensity : WallOwnersIntensity);
                    c.Fills.AddBand(a, b, one, EconomyIds.IndustryPart(i, part), intensity);
                }

                outline.Clear();
                outline.Add(p0 - half);
                outline.Add(p1 - half);
                outline.Add(p1 + half);
                outline.Add(p0 + half);
                outline.Add(p0 - half);
                c.Lines.AddPolyline(outline, LandMath.Tint(LandStyle.TierTint(inds[i].TierIndex), CardEdgeAlpha), CardEdgePx, 0,
                    EconomyIds.IndustryPart(i, EconomyIds.PartEdge), LandStyle.CardIntensity);
            }

            // the dots: every lifeline alive at the year's middle, each a short stroke across the road (one pass)
            Vector3 dh = 0.5f * DotLength * across;
            Color32 blue = LandMath.Tint(EconomyStyle.People, 0.95f), gold = LandMath.Tint(EconomyStyle.Capital, 1f);
            Color32 child = LandMath.Tint(EconomyStyle.People, LandStyle.ChildDotAlpha);
            CutMap map = new CutMap(frame, u, w);
            if (sim != null && sim.StepCount > 0)
            {
                int step = sim.StepAt(mid);
                float[] sy = sim.SampleY, sr = sim.SampleRho;
                for (int i = 0; i < sim.People.Count; i++)
                {
                    // the person's sample at the step (SmvPopulation.PointAt without the clock arc: the cut has its own)
                    SmvPerson p = sim.People[i];
                    if (step < p.FirstStep || step > p.LastStep) continue;
                    int at = p.SampleOffset + step - p.FirstStep;
                    Vector3 d = new Vector3(0, sy[at], sr[at]);
                    bool adult, control = false;
                    if (lives != null && lives.TryGet(i, year, out PersonYear r))
                    {
                        adult = r.Adult;
                        control = r.InControl && adult;
                    }
                    else adult = mid - p.Birth >= 18;

                    Dot dot = new Dot { Person = i, At = map.At(d.y, d.z), Adult = adult, Control = control, Id = pop.Id(p) };
                    c.Dots.Add(dot);
                    if (adult)
                    {
                        c.Lines.AddSegment(dot.At - dh, dot.At + dh, control ? gold : blue, LandStyle.DotPx, 0, dot.Id, control ? 1.5f : 1.2f);
                    }
                    else c.Lines.AddSegment(dot.At - dh, dot.At + dh, child, LandStyle.ChildDotPx, 0, dot.Id);

                    rhoLo = Mathf.Min(rhoLo, d.z);
                    rhoHi = Mathf.Max(rhoHi, d.z);
                    yHi = Mathf.Max(yHi, d.y);
                    if (adult)
                    {
                        c.Adults++;
                        if (control) c.InControl++;
                    }
                    else c.Children++;
                }
            }

            c.RhoLo = rhoLo - LandStyle.CutPad;
            c.RhoHi = rhoHi + LandStyle.CutPad;
            c.YLo = EconomyStyle.GroundY;
            c.YHi = yHi + LandStyle.CutTopPad;

            double gdp = data.Gdp.GrowthAt(year);
            c.Readout = year.ToString(LandFacts.Ci) + " · GDP " + LandFacts.Money(gdp, 1) + " · " +
                        LandFacts.Millions(c.Adults * 1e5, 0) + " adults in " + LandFacts.Count(c.Adults) + " lines · " +
                        LandFacts.Percent(c.Adults > 0 ? c.InControl / (double)c.Adults : 0) + " in control" +
                        (data.IsEstimate(year) ? " (estimate: H1 annualized)" : "");
            c.Ms = sw.Elapsed.TotalMilliseconds;
            return c;
        }

        // ------------------------------------------------------------------ the morph plan (pure)

        /// <summary>
        /// One bar's journey to its sector: where it starts (the card), its sizes (world height h, area A, ring width w_t,
        /// mid-radius, tread height), where its tile lies in the staircase (x at k = 1), its ring's spin, its strip
        /// boundaries (tile coordinates −½..½: the card's, the sector's linear shares, the area-true radii) and colors.
        /// </summary>
        struct Tile
        {
            public int Industry, Tier, Segments;
            public Vector3 Card;
            public float H, Area, KCard, W, RMid, Y, X1, Beta;
            public float VwCard, VuCard, VwLin, VuLin, VwArea, VuArea;
            public Color32 WagesA, UpkeepA, OwnersA, WagesB, UpkeepB, OwnersB, Edge;
            public float IWagesA, IUpkeepA, IOwnersA, IOwnersB;
        }

        /// <summary>
        /// One dot's flight: from the cut to its slot in its player's disc (none: it fades), its start time, its look. A
        /// flight starts after the lift (<see cref="LandStyle.DotsStart"/> &gt;= <see cref="LandStyle.LiftEnd"/>), so it
        /// leaves from the lifted place (the start on the center line) over a fixed control point.
        /// </summary>
        struct Flight
        {
            public Vector3 Start, End, Control;
            public float T0, Px, Intensity, Id;
            public bool HasSlot;
            public Color32 Color;
        }

        /// <summary>Everything the morph draws for one card and one snapshot (built on a worker when either changes, see <see cref="RebuildPlan"/>).</summary>
        sealed class Plan
        {
            public Tile[] Tiles;

            /// <summary>
            /// Each ring's tiles along the staircase's row (+x, decreasing final angle), the arc-length gap before each
            /// tile at k = 1 (the sectors' 1.5° gaps), and each row's middle at k = 1.
            /// </summary>
            public int[][] Rows;

            public float[] Gap1;
            public readonly float[] RowMid1 = new float[5];

            /// <summary>Per frame: each tile's length and center along the row.</summary>
            public float[] Length, X;

            public Flight[] Flights;
            public int LandYear, CardYear, Version;

            /// <summary>The line vertices of the tiles' outlines (the dots' strokes follow them in the line mesh).</summary>
            public int OutlineVertices;

            /// <summary>From this time on the dots stand still (every flight landed, the unplaced ones faded).</summary>
            public float DotsSettle;

            /// <summary>
            /// What each dot was last written at: its flight's progress (or, unplaced, its fade); the lift of that write.
            /// A dot whose state did not change since is not rewritten (NaN: never written).
            /// </summary>
            public float[] DotState;

            public float DotLift = float.NaN;
        }

        /// <summary>The bars, sectors, players and dots of a card and a snapshot, as the morph needs them.</summary>
        static Plan BuildPlan(Card card, LandSnapshot snap, EconomyModel model)
        {
            Plan plan = new Plan { CardYear = card.Year, LandYear = snap.Year, Version = snap.Version };
            LandGeometry land = snap.Land;
            EconomyData data = model.Data;
            int n = Math.Min(land.Sectors.Length, card.Bars.Length);
            List<Tile> tiles = new List<Tile>(n);
            for (int i = 0; i < n; i++)
            {
                SectorGeom s = land.Sectors[i];
                float span = s.Theta1 - s.Theta0;
                if (span <= 1e-4f) continue;
                int t = (int)s.Tier;
                Bar bar = card.Bars[i];
                float w = s.R1 - s.R0, rMid = 0.5f * (s.R0 + s.R1);
                float area = 0.5f * span * Mathf.Deg2Rad * (s.R1 * s.R1 - s.R0 * s.R0);
                float delta = LandMath.DeltaDeg(land.RingOffset[t], s.Mid);
                float r2 = s.R1 * s.R1 - s.R0 * s.R0;
                float fw = r2 > 0 ? (s.RWages * s.RWages - s.R0 * s.R0) / r2 : 0;
                float fu = r2 > 0 ? (s.RUpkeep * s.RUpkeep - s.R0 * s.R0) / r2 : 0;
                Color hue = EconomyStyle.Level(data.Industries[i].Level);
                tiles.Add(new Tile
                {
                    Industry = i, Tier = t, Segments = Math.Max(2, LandFrame.Segments(span, TileStepDeg)),
                    Card = bar.Center, H = bar.Height, Area = area, KCard = LandStyle.CardWidth * bar.Height / Mathf.Max(1e-6f, area),
                    W = w, RMid = rMid, Y = s.Y + StripLift, X1 = -delta * Mathf.Deg2Rad * rMid,
                    Beta = LandMath.DeltaDeg(90f, land.RingOffset[t]),
                    VwCard = -0.5f + bar.Wages, VuCard = -0.5f + bar.Wages + bar.Upkeep, VwLin = -0.5f + fw, VuLin = -0.5f + fu,
                    VwArea = (s.RWages - rMid) / Mathf.Max(1e-6f, w), VuArea = (s.RUpkeep - rMid) / Mathf.Max(1e-6f, w),
                    WagesA = LandMath.Tint(LandStyle.Wages, WallWagesAlpha), UpkeepA = LandMath.Tint(WallUpkeepColor, WallUpkeepAlpha),
                    OwnersA = LandMath.Tint(hue, WallOwnersAlpha), WagesB = LandMath.Tint(LandStyle.Wages, LandStyle.WagesAlpha),
                    UpkeepB = LandMath.Tint(Steel, LandStyle.UpkeepAlpha), OwnersB = LandMath.Tint(hue, LandStyle.OwnersAlpha),
                    Edge = LandMath.Tint(LandStyle.TierTint(t), TileEdgeAlpha),
                    IWagesA = LandStyle.CardIntensity * WallWagesIntensity, IUpkeepA = LandStyle.CardIntensity * WallUpkeepIntensity,
                    IOwnersA = LandStyle.CardIntensity * WallOwnersIntensity, IOwnersB = LandStyle.OwnersIntensity * TierIntensity[t] / TierIntensity[2]
                });
            }

            plan.Tiles = tiles.ToArray();
            int nt = plan.Tiles.Length;
            plan.Gap1 = new float[nt];
            plan.Length = new float[nt];
            plan.X = new float[nt];
            plan.Rows = new int[5][];
            for (int t = 0; t < 5; t++)
            {
                List<int> row = new List<int>();
                for (int k = 0; k < nt; k++)
                {
                    if (plan.Tiles[k].Tier == t) row.Add(k);
                }

                Tile[] all = plan.Tiles;
                row.Sort((a, b) =>
                {
                    int c = all[a].X1.CompareTo(all[b].X1);
                    return c != 0 ? c : a.CompareTo(b);
                });
                plan.Rows[t] = row.ToArray();
                float lo = float.MaxValue, hi = float.MinValue;
                for (int j = 0; j < row.Count; j++)
                {
                    Tile cur = all[row[j]];
                    float half = 0.5f * cur.Area / cur.W;
                    lo = Mathf.Min(lo, cur.X1 - half);
                    hi = Mathf.Max(hi, cur.X1 + half);
                    if (j == 0) continue;
                    Tile prev = all[row[j - 1]];
                    plan.Gap1[row[j]] = Mathf.Max(0, cur.X1 - prev.X1 - half - 0.5f * prev.Area / prev.W);
                }

                plan.RowMid1[t] = row.Count > 0 ? 0.5f * (lo + hi) : 0;
            }

            // the dots: each person's slot in its player (adults by member order, children on the ring), players in the
            // order of their angle around the rim
            Player[] players = snap.Players?.Players ?? Array.Empty<Player>();
            int[] rank = new int[players.Length];
            int[] order = new int[players.Length];
            for (int k = 0; k < order.Length; k++) order[k] = k;
            Array.Sort(order, (x, y) =>
            {
                int c = LandMath.Wrap360(players[x].Theta).CompareTo(LandMath.Wrap360(players[y].Theta));
                return c != 0 ? c : x.CompareTo(y);
            });
            for (int k = 0; k < order.Length; k++) rank[order[k]] = k;
            Dictionary<int, (int player, int slot, bool child)> slotOf = new Dictionary<int, (int, int, bool)>(4096);
            foreach (Player p in players)
            {
                for (int k = 0; k < p.Adults.Length; k++) slotOf[p.Adults[k]] = (p.Index, k, false);
                for (int k = 0; k < p.Children.Length; k++) slotOf[p.Children[k]] = (p.Index, k, true);
            }

            EconomicLives lives = model.Lives;
            Color32 blue = LandMath.Tint(EconomyStyle.People, 0.95f), gold = LandMath.Tint(EconomyStyle.Capital, 1f);
            Color32 childColor = LandMath.Tint(EconomyStyle.People, LandStyle.ChildDotAlpha);
            Flight[] flights = new Flight[card.Dots.Count];
            float spread = players.Length > 0 ? LandStyle.DotsSpread / players.Length : 0;
            for (int k = 0; k < card.Dots.Count; k++)
            {
                Dot d = card.Dots[k];
                Flight f = new Flight
                {
                    Start = d.At, End = d.At, T0 = LandStyle.DotsStart, Id = d.Id,
                    Px = d.Adult ? LandStyle.DotPx : LandStyle.ChildDotPx, Intensity = d.Adult ? d.Control ? 1.5f : 1.2f : 1f,
                    Color = d.Adult ? d.Control ? gold : blue : childColor
                };
                if (slotOf.TryGetValue(d.Person, out (int player, int slot, bool child) s) && s.player >= 0 && s.player < players.Length)
                {
                    Player p = players[s.player];
                    f.HasSlot = true;
                    f.T0 = LandStyle.DotsStart + spread * rank[s.player];
                    if (s.child) f.End = Figure.ChildSlot(p, s.slot, p.Children.Length);
                    else
                    {
                        float reason = 0.4f;
                        if (lives != null && lives.TryGet(d.Person, snap.Year, out PersonYear r)) reason = r.Reason;
                        f.End = Figure.DotSlot(p, s.slot, p.Adults.Length, reason);
                    }
                }

                Vector3 lifted = new Vector3(f.Start.x, f.Start.y, 0);
                f.Control = 0.5f * (lifted + f.End) + new Vector3(0, LandStyle.DotArcLift, 0);
                flights[k] = f;
            }

            plan.Flights = flights;
            float settle = Mathf.Max(LandStyle.LiftEnd, LandStyle.LieBackEnd);
            foreach (Flight f in flights)
            {
                if (f.HasSlot) settle = Mathf.Max(settle, f.T0 + LandStyle.DotFlight);
            }

            plan.DotsSettle = settle;
            plan.DotState = new float[flights.Length];
            foreach (Tile t in plan.Tiles) plan.OutlineVertices += 2 * (2 * (t.Segments + 1) + 1);
            return plan;
        }

        // ------------------------------------------------------------------ meshes of fixed topology, rewritten per frame

        /// <summary>
        /// A line mesh (the Why/Line vertex layout of <see cref="LineMeshBuilder"/>) whose vertices are rewritten in place
        /// every frame: the morph's dots and outlines, the sliding frame and callouts. The index buffer is uploaded only when
        /// the topology changed (a new plan, a different vertex count), so a frame costs one vertex upload.
        /// </summary>
        sealed class DynLines
        {
            [StructLayout(LayoutKind.Sequential)]
            struct V
            {
                public Vector3 Pos;
                public Color32 Color;
                public Vector3 Prev, Next;
                public Vector4 P;   // side, width px, width world, id
                public Vector2 Q;   // intensity, flow
            }

            static readonly VertexAttributeDescriptor[] Layout =
            {
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 4),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord3, VertexAttributeFormat.Float32, 2),
            };

            V[] v = new V[256];
            int[] idx = new int[384];
            int nv, ni, uploadedV = -1;
            bool topologyDirty = true;
            public readonly Mesh Mesh;

            public DynLines(string name)
            {
                Mesh = new Mesh { name = name };
                Mesh.MarkDynamic();
            }

            public int VertexCount => nv;

            /// <summary>Starts a rewrite; <paramref name="newTopology"/> forces the index upload.</summary>
            public void Begin(bool newTopology = false)
            {
                nv = ni = 0;
                topologyDirty |= newTopology;
            }

            /// <summary>A straight segment.</summary>
            public void Segment(Vector3 a, Vector3 b, Color32 color, float px, float id, float intensity = 1)
            {
                Grow(2, 1);
                int b0 = nv;
                Pair(a, a, b, color, px, id, intensity);
                Pair(b, a, b, color, px, id, intensity);
                Quad(b0);
            }

            /// <summary>
            /// Makes room for this many vertices, so the writes up to it never resize the buffer (a range written from
            /// another thread with <see cref="SegmentAt"/> while this one appends below it).
            /// </summary>
            public void Reserve(int vertices)
            {
                if (vertices > v.Length) Array.Resize(ref v, vertices);
            }

            /// <summary>
            /// A straight segment written at a fixed place (its four vertices from <paramref name="at"/>) of a range made
            /// room for with <see cref="Reserve"/>; threads may write disjoint segments at once. The range's indices and
            /// count are set by <see cref="SegmentsAt"/>.
            /// </summary>
            public void SegmentAt(int at, Vector3 a, Vector3 b, Color32 color, float px, float id, float intensity)
            {
                V x = new V { Pos = a, Color = color, Prev = a, Next = b, P = new Vector4(-1, px, 0, id), Q = new Vector2(intensity, 0) };
                v[at] = x;
                x.P.x = 1;
                v[at + 1] = x;
                x.Pos = b;
                v[at + 3] = x;
                x.P.x = -1;
                v[at + 2] = x;
            }

            /// <summary>Appends <paramref name="count"/> segments written with <see cref="SegmentAt"/> from the current end.</summary>
            public void SegmentsAt(int count)
            {
                Grow(2 * count, count);
                for (int k = 0; k < count; k++)
                {
                    Quad(nv);
                    nv += 4;
                }
            }

            /// <summary>A polyline of uniform style.</summary>
            public void Polyline(List<Vector3> pts, Color32 color, float px, float id, float intensity = 1)
            {
                int n = pts.Count;
                if (n < 2) return;
                Grow(n, n - 1);
                int b0 = nv;
                for (int i = 0; i < n; i++) Pair(pts[i], pts[i > 0 ? i - 1 : i], pts[i < n - 1 ? i + 1 : i], color, px, id, intensity);
                for (int i = 0; i < n - 1; i++) Quad(b0 + 2 * i);
            }

            void Pair(Vector3 pos, Vector3 prev, Vector3 next, Color32 color, float px, float id, float intensity)
            {
                V x = new V { Pos = pos, Color = color, Prev = prev, Next = next, P = new Vector4(-1, px, 0, id), Q = new Vector2(intensity, 0) };
                v[nv++] = x;
                x.P.x = 1;
                v[nv++] = x;
            }

            void Quad(int a)
            {
                idx[ni++] = a;
                idx[ni++] = a + 1;
                idx[ni++] = a + 2;
                idx[ni++] = a + 2;
                idx[ni++] = a + 1;
                idx[ni++] = a + 3;
            }

            void Grow(int points, int quads)
            {
                if (nv + 2 * points > v.Length) Array.Resize(ref v, Math.Max(v.Length * 2, nv + 2 * points));
                if (ni + 6 * quads > idx.Length) Array.Resize(ref idx, Math.Max(idx.Length * 2, ni + 6 * quads));
            }

            /// <summary>Uploads the vertices (and the indices when the topology changed).</summary>
            public void End()
            {
                const MeshUpdateFlags flags = MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices |
                                              MeshUpdateFlags.DontNotifyMeshUsers;
                if (topologyDirty || nv != uploadedV)
                {
                    Mesh.SetVertexBufferParams(nv, Layout);
                    Mesh.SetIndexBufferParams(ni, IndexFormat.UInt32);
                    Mesh.SetIndexBufferData(idx, 0, 0, ni, flags);
                    Mesh.subMeshCount = 1;
                    Mesh.SetSubMesh(0, new SubMeshDescriptor(0, ni), flags);
                    Mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1e6f);
                    uploadedV = nv;
                    topologyDirty = false;
                }

                Mesh.SetVertexBufferData(v, 0, 0, nv, 0, flags);
            }
        }

        /// <summary>
        /// A surface mesh (the Why/Surface vertex layout of <see cref="SurfaceMeshBuilder"/>) rewritten in place every frame:
        /// the morph's tiles and the frame's veil.
        /// </summary>
        sealed class DynSurface
        {
            [StructLayout(LayoutKind.Sequential)]
            struct V
            {
                public Vector3 Pos;
                public Color32 Color;
                public Vector4 P;   // id, intensity, across, noise
            }

            static readonly VertexAttributeDescriptor[] Layout =
            {
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 4),
            };

            V[] v = new V[256];
            int[] idx = new int[384];
            int nv, ni, uploadedV = -1;
            bool topologyDirty = true;
            public readonly Mesh Mesh;

            public DynSurface(string name)
            {
                Mesh = new Mesh { name = name };
                Mesh.MarkDynamic();
            }

            public int VertexCount => nv;

            /// <summary>Starts a rewrite; <paramref name="newTopology"/> forces the index upload.</summary>
            public void Begin(bool newTopology = false)
            {
                nv = ni = 0;
                topologyDirty |= newTopology;
            }

            /// <summary>A band between two edges sampled at matching indices, one color.</summary>
            public void Band(List<Vector3> inner, List<Vector3> outer, Color32 color, float id, float intensity)
            {
                int n = Math.Min(inner.Count, outer.Count);
                if (n < 2) return;
                if (nv + 2 * n > v.Length) Array.Resize(ref v, Math.Max(v.Length * 2, nv + 2 * n));
                if (ni + 6 * (n - 1) > idx.Length) Array.Resize(ref idx, Math.Max(idx.Length * 2, ni + 6 * (n - 1)));
                int b0 = nv;
                for (int i = 0; i < n; i++)
                {
                    v[nv++] = new V { Pos = inner[i], Color = color, P = new Vector4(id, intensity, 0, 0) };
                    v[nv++] = new V { Pos = outer[i], Color = color, P = new Vector4(id, intensity, 1, 0) };
                }

                for (int i = 0; i < n - 1; i++)
                {
                    int a = b0 + 2 * i;
                    idx[ni++] = a;
                    idx[ni++] = a + 2;
                    idx[ni++] = a + 1;
                    idx[ni++] = a + 1;
                    idx[ni++] = a + 2;
                    idx[ni++] = a + 3;
                }
            }

            /// <summary>Uploads the vertices (and the indices when the topology changed).</summary>
            public void End()
            {
                const MeshUpdateFlags flags = MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices |
                                              MeshUpdateFlags.DontNotifyMeshUsers;
                if (topologyDirty || nv != uploadedV)
                {
                    Mesh.SetVertexBufferParams(nv, Layout);
                    Mesh.SetIndexBufferParams(ni, IndexFormat.UInt32);
                    Mesh.SetIndexBufferData(idx, 0, 0, ni, flags);
                    Mesh.subMeshCount = 1;
                    Mesh.SetSubMesh(0, new SubMeshDescriptor(0, ni), flags);
                    Mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1e6f);
                    uploadedV = nv;
                    topologyDirty = false;
                }

                Mesh.SetVertexBufferData(v, 0, 0, nv, 0, flags);
            }
        }

        // ------------------------------------------------------------------ state

        EconomyModel model;
        SmvPopulation pop;
        WallGeometry wall;
        LandFrame frame;
        LabelSystem labelSystem;
        LabelSpec readout;
        Anchor cutAnchor;

        /// <summary>The card on screen, and the next one (built when the cut starts sliding, shown on arrival).</summary>
        Card card, next;

        /// <summary>The next card, built on a worker when the cut starts sliding.</summary>
        Task<Card> nextTask;

        MeshRenderer cardFills, cardLines;
        Material cardFillMat, cardLineMat;
        float cardFade = 1;

        // the frame (rewritten while it slides), its veil and the overview's callouts
        DynLines frameLines, callouts;
        DynSurface veil;
        MeshRenderer frameR, veilR, calloutR;
        float cutAt;                      // the year the frame stands at (fractional while sliding)
        float slideFrom, slideTo, slideT = 1;
        float framePlaced = float.NaN;    // the year the frame meshes were last written for
        float calloutEase;
        int calloutCardYear = -1;

        // the morph
        DynSurface morphFills;
        DynLines morphLines;
        MeshRenderer morphFillR, morphLineR;
        Plan plan;
        bool planDirty = true;
        Task<Plan> planTask;
        float morphDrawn = -1;

        /// <summary>The clock time the tiles and the dots were last written at (-1: not since the plan changed).</summary>
        float tilesWritten = -1, dotsWritten = -1;

        // the statistics of the current run (+1 an unfold, -1 a fold, 0 none yet)
        int morphFrames, morphSkipped, morphDirection;
        float morphLast = -1;
        double morphMaxMs, morphTotalMs, morphFirstMs, morphUploadMs;
        readonly List<Vector3> inner = new List<Vector3>(64), outer = new List<Vector3>(64), loop = new List<Vector3>(140);

        int labelsVersion = -1;

        public override void Prepare(GraphContext ctx)
        {
            Stopwatch sw = Stopwatch.StartNew();
            model = ctx.Shared<EconomyModel>(EconomyModel.SharedKey);
            pop = ctx.Shared<SmvPopulation>(SmvPopulation.SharedKey);
            wall = ctx.Shared<WallGeometry>(WallGeometry.SharedKey);
            LandSnapshot s = ctx.Shared<LandSnapshot>(LandService.SharedKey);
            if (model == null || model.Data.Industries.Count == 0 || pop?.Sim == null)
            {
                Debug.LogWarning("[Why] SectionLayer: no economy model or population, the cut is not drawn");
                model = null;
                return;
            }

            frame = EconomyStage.Land();
            int year = s?.Year ?? EconomyState.DefaultYear(EconomyState.MaxYear);
            card = BuildCard(year, model, pop, wall, frame);
            cutAt = slideFrom = slideTo = year;
            planDirty = true;   // the morph's plan builds on a worker after the upload (RebuildPlan)

            labelSystem = ctx.Labels;
            readout = new LabelSpec
            {
                Text = card.Readout, Fixed = true, Data = frame.World(FrameBottom(card, card.Year)), SizePx = ReadoutPx,
                Color = GraphStyle.Text, Align = TMPro.TextAlignmentOptions.Center, Priority = ReadoutPriority,
                Rank = ReadoutPriority, PixelOffset = new Vector2(0, -ReadoutDrop), AnchorKey = "land:cut",
                Ids = IdRange.Single(EconomyIds.LandCut), Hidden = true
            };
            ctx.Labels.Add(readout);
            cutAnchor = new Anchor
            {
                Key = "land:cut", Label = "The cut through " + year.ToString(LandFacts.Ci), Level = GraphLevel.Humans,
                Ids = IdRange.Single(EconomyIds.LandCut), Tier = 1
            };
            UpdateAnchor(card);
            Anchors.Register(cutAnchor);

            int cardVertices = card.Fills.VertexCount + card.Lines.VertexCount;
            int adults = card.Adults, children = card.Children;
            Debug.Log("[Why] SectionLayer.Prepare " + sw.Elapsed.TotalMilliseconds.ToString("0", LandFacts.Ci) + " ms: " +
                      cardVertices.ToString(LandFacts.Ci) + " vertices (card " + card.Fills.VertexCount.ToString(LandFacts.Ci) +
                      ", edges and dots " + card.Lines.VertexCount.ToString(LandFacts.Ci) + "), morph " +
                      MorphVertices(card, s).ToString(LandFacts.Ci) + " vertices per frame; " + card.Bars.Length.ToString(LandFacts.Ci) +
                      " bars, " + LandFacts.Count(adults) + " adult dots, " + LandFacts.Count(children) + " child dots; " + card.Readout +
                      " (card " + card.Ms.ToString("0.0", LandFacts.Ci) + " ms)");
        }

        public override void Upload(GraphContext ctx)
        {
            if (model == null) return;
            labelSystem = ctx.Labels;
            int queue = LandStyle.QueueSection;
            veil = new DynSurface("SectionVeil");
            veilR = Renderer("SectionVeil", veil.Mesh, true, queue);
            frameLines = new DynLines("SectionFrame");
            frameR = Renderer("SectionFrame", frameLines.Mesh, false, queue + 3);
            callouts = new DynLines("SectionCallouts");
            calloutR = Renderer("SectionCallouts", callouts.Mesh, false, queue);
            ShowCard(card);
            morphFills = new DynSurface("SectionMorphFills");
            morphFillR = Renderer("SectionMorphFills", morphFills.Mesh, true, queue + 4);
            morphLines = new DynLines("SectionMorphLines");
            morphLineR = Renderer("SectionMorphLines", morphLines.Mesh, false, queue + 5);
            morphFillR.enabled = morphLineR.enabled = false;
            WriteFrame(cutAt);
            LandService.Changed += OnChanged;
            LandService.ReportReady(ReadyName, LandService.Current?.Version ?? 0);
        }

        /// <summary>Unsubscribes from the land and frees the meshes and materials this layer made.</summary>
        void OnDestroy()
        {
            LandService.Changed -= OnChanged;
            foreach (MeshRenderer r in new[] { cardFills, cardLines, frameR, veilR, calloutR, morphFillR, morphLineR }) Release(r);
        }

        /// <summary>A new snapshot: the morph's targets (sectors, players) change; the plan is rebuilt in the next Tick.</summary>
        void OnChanged(LandSnapshot s)
        {
            planDirty = true;
        }

        /// <summary>A renderer for one of this layer's meshes: a raw (land-local) surface or line material at a queue, hidden until shown.</summary>
        MeshRenderer Renderer(string name, Mesh mesh, bool surface, int queue)
        {
            Material m = GraphMaterials.Raw(surface
                ? GraphMaterials.Surface(Color.white, 1f, queue)
                : GraphMaterials.Line(Color.white, 1f, queue));
            if (surface) m.SetFloat("_EdgeSoft", 0f);
            MeshRenderer r = AddMesh(name, mesh, m);
            frame.Place(r.transform);
            GraphMaterials.SetAlpha(m, 0);
            r.enabled = false;
            return r;
        }

        /// <summary>Destroys a renderer's mesh, material and object.</summary>
        static void Release(MeshRenderer r)
        {
            if (r == null) return;
            MeshFilter f = r.GetComponent<MeshFilter>();
            if (f != null && f.sharedMesh != null) Destroy(f.sharedMesh);
            if (r.sharedMaterial != null) Destroy(r.sharedMaterial);
            Destroy(r.gameObject);
        }

        /// <summary>Puts a card's meshes on screen (the old ones go) and points the readout and the anchor at it.</summary>
        void ShowCard(Card c)
        {
            Release(cardFills);
            Release(cardLines);
            cardFills = Renderer("SectionCard", c.Fills.ToMesh("SectionCard"), true, LandStyle.QueueSection + 1);
            cardLines = Renderer("SectionCardLines", c.Lines.ToMesh("SectionCardLines"), false, LandStyle.QueueSection + 2);
            cardFillMat = cardFills.sharedMaterial;
            cardLineMat = cardLines.sharedMaterial;
            card = c;
            if (readout != null && labelSystem != null)
            {
                if (readout.Text != c.Readout) labelSystem.SetText(readout, c.Readout);
                readout.Data = frame.World(FrameBottom(c, c.Year));
                labelSystem.MarkDirty();
            }

            UpdateAnchor(c);
            planDirty = true;
        }

        /// <summary>Points the <c>land:cut</c> anchor at a card: its title, its blurb (the readout) and its place under the frame.</summary>
        void UpdateAnchor(Card c)
        {
            if (cutAnchor == null) return;
            WarpState w = EconomyStage.TimelineWarp();
            float u = CutU(c.Year);
            cutAnchor.Label = "The cut through " + c.Year.ToString(LandFacts.Ci);
            cutAnchor.Blurb = c.Readout + ". Every industry's band of the wall and every life alive in the year, cut across the " +
                              "road; it opens into the land past the road's end.";
            cutAnchor.Fixed = GraphWarp.ToWorld(u, 0.5f * (c.YLo + c.YHi), 0.5f * (c.RhoLo + c.RhoHi), w);
            cutAnchor.YearsAgo = cutAnchor.EndYearsAgo = DeepTime.NowYear - (c.Year + 0.5);
            cutAnchor.Y = 0.5f * (c.YLo + c.YHi);
            cutAnchor.Rho = 0.5f * (c.RhoLo + c.RhoHi);
        }

        /// <summary>The frame's bottom middle (land-local) for a card at a (fractional) year.</summary>
        Vector3 FrameBottom(Card c, float year) =>
            CutPoint(frame, CutU(year), c.YLo, 0.5f * (c.RhoLo + c.RhoHi), EconomyStage.TimelineWarp());

        // ------------------------------------------------------------------ per frame

        /// <summary>Per frame: follows the cut's year (the frame slides), fades the cut with its emphasis, takes the morph's plan and draws the morph, follows the labels.</summary>
        public override void Tick(GraphContext ctx, CameraRig rig)
        {
            if (model == null || frameLines == null) return;
            float dt = Mathf.Min(0.1f, Time.unscaledDeltaTime);
            FollowYear(dt);

            float cut = LandView.Alpha(LandGroup.Cut);
            Show(frameR, cut);
            Show(veilR, cut);
            Show(cardFills, cut * cardFade);
            Show(cardLines, cut * cardFade);
            calloutEase = Mathf.MoveTowards(calloutEase, LandView.PresetId == "overview" ? 1f : 0f, LandStyle.EmphasisRate * dt);
            Show(calloutR, cut * calloutEase);

            if (planDirty) RebuildPlan();
            float time = LandView.MorphTime;
            TakePlan(time > 1e-4f && time < LandStyle.UnfoldSeconds - 1e-4f);
            Morph();
            UpdateLabel();
        }

        /// <summary>
        /// The cut follows the year: a new target starts a slide over <see cref="LandStyle.YearSlideSeconds"/> (the next
        /// card is built at once and shown on arrival); the frame is rewritten every frame it moves.
        /// </summary>
        void FollowYear(float dt)
        {
            int target = EconomyState.CutYear;
            if (target != Mathf.RoundToInt(slideTo))
            {
                slideFrom = cutAt;
                slideTo = target;
                slideT = 0;
                next = null;
                EconomyModel m = model;
                SmvPopulation p = pop;
                WallGeometry wg = wall;
                LandFrame f = frame;
                nextTask = Task.Run(() => BuildCard(target, m, p, wg, f));
            }

            if (nextTask != null && nextTask.IsCompleted) TakeNext();
            if (slideT < 1)
            {
                slideT = Mathf.Min(1, slideT + dt / LandStyle.YearSlideSeconds);
                cutAt = Mathf.Lerp(slideFrom, slideTo, LandMath.EaseInOutCubic(slideT));
                cardFade = Mathf.Max(0, cardFade - dt / CardFadeSeconds);
                if (slideT >= 1)
                {
                    if (nextTask != null) TakeNext();   // arrived before the worker: wait for it
                    if (next != null && next.Year == Mathf.RoundToInt(slideTo))
                    {
                        ShowCard(next);
                        Debug.Log("[Why] SectionLayer card " + next.Year.ToString(LandFacts.Ci) + ": " + next.Ms.ToString("0.0", LandFacts.Ci) +
                                  " ms on a worker, " + LandFacts.Count(next.Adults) + " adult and " + LandFacts.Count(next.Children) +
                                  " child dots; " + next.Readout);
                    }

                    next = null;
                }
            }
            else cardFade = Mathf.Min(1, cardFade + dt / CardFadeSeconds);

            if (!float.IsNaN(framePlaced) && Mathf.Abs(framePlaced - cutAt) < 1e-5f && calloutCardYear == card.Year) return;
            WriteFrame(cutAt);
        }

        /// <summary>Takes the next card from its worker (waiting for it when not finished).</summary>
        void TakeNext()
        {
            Task<Card> t = nextTask;
            nextTask = null;
            if (t.IsFaulted) Debug.LogError("[Why] SectionLayer: the cut's card failed: " + t.Exception?.GetBaseException());
            else next = t.Result;
        }

        /// <summary>
        /// The frame at a (fractional) year: its bounds lerped from the card shown to the next one while sliding; four
        /// corners, the veil between them and the callouts from them to the near rim (≤ 10 points from the warp).
        /// </summary>
        void WriteFrame(float year)
        {
            framePlaced = year;
            calloutCardYear = card.Year;
            Card a = card, b = next ?? card;
            float s = next != null ? LandMath.EaseInOutCubic(slideT) : 0;
            float rhoLo = Mathf.Lerp(a.RhoLo, b.RhoLo, s), rhoHi = Mathf.Lerp(a.RhoHi, b.RhoHi, s);
            float yLo = Mathf.Lerp(a.YLo, b.YLo, s), yHi = Mathf.Lerp(a.YHi, b.YHi, s);
            WarpState w = EconomyStage.TimelineWarp();
            float u = CutU(year);
            Vector3 bl = CutPoint(frame, u, yLo, rhoLo, w), br = CutPoint(frame, u, yLo, rhoHi, w);
            Vector3 tl = CutPoint(frame, u, yHi, rhoLo, w), tr = CutPoint(frame, u, yHi, rhoHi, w);
            if (bl.x > br.x)
            {
                (bl, br) = (br, bl);
                (tl, tr) = (tr, tl);
            }

            Color whiten = Color.Lerp(EconomyStyle.Cooperate, Color.white, FrameWhiten);
            frameLines.Begin();
            loop.Clear();
            loop.Add(bl);
            loop.Add(tl);
            loop.Add(tr);
            loop.Add(br);
            loop.Add(bl);
            frameLines.Polyline(loop, LandMath.Tint(whiten, 1f), LandStyle.CutPx, EconomyIds.LandCut, LandStyle.CutIntensity);
            frameLines.End();

            veil.Begin();
            inner.Clear();
            outer.Clear();
            inner.Add(bl);
            inner.Add(br);
            outer.Add(tl);
            outer.Add(tr);
            veil.Band(inner, outer, LandMath.Tint(whiten, LandStyle.CutVeilAlpha), EconomyIds.LandCut, 1f);
            veil.End();

            // the overview's callouts: from the frame's corners to the bowl's near rim (left corners to the left)
            callouts.Begin();
            Color32 dim = LandMath.Tint(whiten, LandStyle.CalloutAlpha);
            float left = 270f - CalloutSpreadDeg, right = 270f + CalloutSpreadDeg;
            Dashed(tl, LandFrame.Polar(LandStyle.RimR, left, LandStyle.RimY), dim);
            Dashed(tr, LandFrame.Polar(LandStyle.RimR, right, LandStyle.RimY), dim);
            Dashed(bl, LandFrame.Polar(LandStyle.RimR, left, 0), dim);
            Dashed(br, LandFrame.Polar(LandStyle.RimR, right, 0), dim);
            callouts.End();

            if (readout != null && labelSystem != null)
            {
                readout.Data = frame.World(0.5f * (bl + br));
                labelSystem.MarkDirty();
            }
        }

        /// <summary>A dashed callout from a to b (dashes of <see cref="CalloutDash"/>, gaps of <see cref="CalloutGap"/>).</summary>
        void Dashed(Vector3 a, Vector3 b, Color32 color)
        {
            float length = Vector3.Distance(a, b), period = CalloutDash + CalloutGap;
            int n = Mathf.Max(1, Mathf.FloorToInt(length / period));
            for (int k = 0; k < n; k++)
            {
                float t0 = k * period / length, t1 = Mathf.Min(1, (k * period + CalloutDash) / length);
                callouts.Segment(Vector3.Lerp(a, b, t0), Vector3.Lerp(a, b, t1), color, LandStyle.CalloutPx, EconomyIds.LandCut);
            }
        }

        /// <summary>Shows a renderer at an alpha (disabled when it would be invisible).</summary>
        static void Show(MeshRenderer r, float alpha)
        {
            if (r == null) return;
            bool on = alpha > 0.003f;
            if (r.enabled != on) r.enabled = on;
            if (on) GraphMaterials.SetAlpha(r.sharedMaterial, alpha);
        }

        /// <summary>The morph's plan for the card and the snapshot on screen, built on a worker (taken by <see cref="TakePlan"/>).</summary>
        void RebuildPlan()
        {
            planDirty = false;
            LandSnapshot s = LandService.Current;
            if (s?.Land == null || card == null) return;
            Card c = card;
            EconomyModel m = model;
            planTask = Task.Run(() => BuildPlan(c, s, m));
        }

        /// <summary>Takes a finished plan (or waits for it when the morph needs it now) and reports the layer ready.</summary>
        void TakePlan(bool wait)
        {
            if (planTask == null || !wait && !planTask.IsCompleted) return;
            Task<Plan> t = planTask;
            planTask = null;
            if (t.IsFaulted)
            {
                Debug.LogError("[Why] SectionLayer: the morph's plan failed: " + t.Exception?.GetBaseException());
                return;
            }

            plan = t.Result;
            morphDrawn = -1;
            LandService.ReportReady(ReadyName, plan.Version);
        }

        /// <summary>Vertices the morph rewrites per frame for a card and a snapshot: four per dot, the tiles' strips and outlines.</summary>
        static int MorphVertices(Card c, LandSnapshot s)
        {
            int v = 4 * c.Dots.Count;
            if (s?.Land == null) return v;
            foreach (SectorGeom g in s.Land.Sectors)
            {
                if (g.Theta1 - g.Theta0 <= 1e-4f) continue;
                int n = Math.Max(2, LandFrame.Segments(g.Theta1 - g.Theta0, TileStepDeg));
                v += 3 * 2 * (n + 1) + 2 * (2 * (n + 1) + 1);
            }

            return v;
        }

        /// <summary>Hides or shows the readout with the Cut label group when the view's label groups changed.</summary>
        void UpdateLabel()
        {
            if (readout == null || labelsVersion == LandView.LabelsVersion) return;
            labelsVersion = LandView.LabelsVersion;
            bool show = LandView.LabelsShown(LandGroup.Cut);
            if (readout.Hidden == !show) return;
            readout.Hidden = !show;
            labelSystem?.MarkDirty();
        }

        // ------------------------------------------------------------------ the morph (1.4)

        /// <summary>
        /// Draws the transition at <see cref="LandView.MorphTime"/>: hidden at either end (0: the card in the cut; 2.4: the
        /// land), rewritten whenever the clock moved, faded out by the land's reveal. Each run (an unfold, a fold) logs its
        /// frame times when it ends.
        /// </summary>
        void Morph()
        {
            float time = LandView.MorphTime;
            bool active = plan != null && time > 1e-4f && time < LandStyle.UnfoldSeconds - 1e-4f;
            float alpha = active ? 1f - LandView.Reveal : 0f;
            Show(morphFillR, alpha);
            Show(morphLineR, alpha);
            if (!active)
            {
                LogMorphRun();
                return;
            }

            if (Mathf.Abs(time - morphDrawn) < 1e-5f) return;

            // a new direction (an unfold after a fold, or the reverse) starts a new run of the statistics; a run that
            // starts from rest is an unfold when it starts near the card, a fold when near the land
            int direction = morphLast < 0 ? time < 0.5f * LandStyle.UnfoldSeconds ? 1 : -1 : time > morphLast ? 1 : -1;
            if (direction != morphDirection) LogMorphRun();
            morphDirection = direction;
            morphLast = time;

            long start = Stopwatch.GetTimestamp();
            bool newTopology = morphDrawn < 0;
            morphDrawn = time;
            WriteMorph(time, newTopology);
            double ms = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
            morphFrames++;
            morphTotalMs += ms;
            if (newTopology) morphFirstMs = ms;
            else morphMaxMs = Math.Max(morphMaxMs, ms);
        }

        /// <summary>Logs the frame times of the morph's last run (an unfold or a fold) and starts a new one.</summary>
        void LogMorphRun()
        {
            if (morphFrames > 0)
            {
                Debug.Log("[Why] SectionLayer morph (" + (morphDirection < 0 ? "fold" : "unfold") + "): " +
                          morphFrames.ToString(LandFacts.Ci) + " frames, " +
                          (morphTotalMs / morphFrames).ToString("0.00", LandFacts.Ci) + " ms mean (of which the vertex upload " +
                          (morphUploadMs / morphFrames).ToString("0.00", LandFacts.Ci) + " ms), " +
                          morphMaxMs.ToString("0.00", LandFacts.Ci) + " ms max (a plan's first frame, which also uploads the indices, " +
                          morphFirstMs.ToString("0.00", LandFacts.Ci) + " ms), " + morphSkipped.ToString(LandFacts.Ci) +
                          " settled frames not rewritten, " + (morphFills.VertexCount + morphLines.VertexCount).ToString(LandFacts.Ci) +
                          " vertices");
            }

            morphFrames = morphSkipped = 0;
            morphMaxMs = morphTotalMs = morphUploadMs = morphFirstMs = 0;
            morphDirection = 0;
            morphLast = -1;
        }

        /// <summary>The phases' progress at a time (each eased in and out) and the tiles' area factor k.</summary>
        struct Phases
        {
            public float A, B, C, D, K;

            public static Phases At(float time)
            {
                Phases p = new Phases
                {
                    A = LandMath.EaseInOutCubic(LandMath.Phase(time, 0, LandStyle.LiftEnd)),
                    B = LandMath.EaseInOutCubic(LandMath.Phase(time, LandStyle.LieBackStart, LandStyle.LieBackEnd)),
                    C = LandMath.EaseInOutCubic(LandMath.Phase(time, LandStyle.CurlStart, LandStyle.CurlEnd)),
                    D = LandMath.EaseInOutCubic(LandMath.Phase(time, LandStyle.SpinStart, LandStyle.SpinEnd))
                };
                return p;
            }
        }

        /// <summary>
        /// Rewrites the morph's meshes at a time: the tiles (fills and outlines), then the dots' strokes at their fixed
        /// place after the outlines (four vertices per flight). What has not moved is not rewritten: a dot waiting for its
        /// flight or landed, every dot after <see cref="Plan.DotsSettle"/>, everything after the spin (the reveal's last
        /// 0.2 s, when only the alpha changes).
        /// </summary>
        void WriteMorph(float time, bool newTopology)
        {
            if (newTopology) tilesWritten = dotsWritten = -1;
            bool dots = time < plan.DotsSettle || dotsWritten < plan.DotsSettle;
            bool tiles = dots || time < LandStyle.SpinEnd || tilesWritten < LandStyle.SpinEnd;
            if (!tiles)
            {
                morphSkipped++;
                return;
            }

            Phases ph = Phases.At(time);
            int count = plan.Flights.Length;
            morphFills.Begin(newTopology);
            morphLines.Begin(newTopology);
            morphLines.Reserve(plan.OutlineVertices + 4 * count);
            if (newTopology)
            {
                for (int k = 0; k < count; k++) plan.DotState[k] = float.NaN;
                plan.DotLift = float.NaN;
            }

            LayRows(ph);
            for (int k = 0; k < plan.Tiles.Length; k++) WriteTile(plan.Tiles[k], plan.Length[k], plan.X[k], ph);
            tilesWritten = time;
            if (dots)
            {
                WriteDots(time, ph);
                dotsWritten = time;
            }

            if (morphLines.VertexCount != plan.OutlineVertices)
            {
                Debug.LogError("[Why] SectionLayer: the morph's outlines wrote " + morphLines.VertexCount.ToString(LandFacts.Ci) +
                               " vertices, not " + plan.OutlineVertices.ToString(LandFacts.Ci));
            }

            morphLines.SegmentsAt(count);
            long computed = Stopwatch.GetTimestamp();
            morphFills.End();
            morphLines.End();
            morphUploadMs += (Stopwatch.GetTimestamp() - computed) * 1000.0 / Stopwatch.Frequency;
        }

        /// <summary>
        /// The dots: lifted with the card, then each flies to its player's disc (or fades), written at their fixed places
        /// after the outlines. A dot whose state (its flight's progress, or its fade; the lift) is the one it was last
        /// written at keeps its vertices.
        /// </summary>
        void WriteDots(float time, Phases ph)
        {
            Vector3 half = new Vector3(0.5f * DotLength, 0, 0);
            float lift = 1 - ph.A;
            float fade = 1f - LandMath.Phase(time, LandStyle.DotsStart, LandStyle.LieBackEnd);
            bool liftSame = plan.DotLift == lift;
            plan.DotLift = lift;
            Flight[] flights = plan.Flights;
            float[] state = plan.DotState;
            int at = plan.OutlineVertices;
            for (int k = 0; k < flights.Length; k++, at += 4)
            {
                Flight f = flights[k];
                float st = f.HasSlot ? Ease(LandMath.Phase(time, f.T0, f.T0 + LandStyle.DotFlight)) : fade;
                if (liftSame && state[k] == st) continue;
                state[k] = st;
                Vector3 lifted = new Vector3(f.Start.x, f.Start.y, f.Start.z * lift);
                Vector3 p = lifted;
                Color32 color = f.Color;
                if (!f.HasSlot) color = LandMath.Fade(color, st);
                else if (st >= 1) p = f.End;
                else if (st > 0) p = LandMath.Bezier(lifted, f.Control, f.End, st);
                morphLines.SegmentAt(at, p - half, p + half, color, f.Px, f.Id, f.Intensity);
            }
        }

        /// <summary><see cref="LandMath.EaseInOutCubic"/> of a progress already in 0..1, without its power call.</summary>
        static float Ease(float t)
        {
            if (t <= 0) return 0;
            if (t >= 1) return 1;
            if (t < 0.5f) return 4 * t * t * t;
            float r = 2 - 2 * t;
            return 1 - 0.5f * r * r * r;
        }

        /// <summary>The tile's area factor k(s): the card's, to 0.5 in the staircase, to 1 in the bowl.</summary>
        static float AreaFactor(Tile t, Phases ph) =>
            Mathf.Pow(t.KCard, 1 - ph.B) * Mathf.Pow(LandStyle.StaircaseK, ph.B) * Mathf.Pow(1f / LandStyle.StaircaseK, ph.C);

        /// <summary>The tile's radial size w(s) = h^(1−s) · w_t^s.</summary>
        static float RadialSize(Tile t, Phases ph) => Mathf.Pow(t.H, 1 - ph.B) * Mathf.Pow(t.W, ph.B);

        /// <summary>
        /// Lays each ring's tiles side by side along its row at their current lengths ℓ = k·A/w, the sectors' gaps scaled
        /// by k, the row centered where it ends (k × its final middle): no two tiles of a ring overlap while they lie back,
        /// and at the end every tile stands at k × its final place.
        /// </summary>
        void LayRows(Phases ph)
        {
            for (int t = 0; t < 5; t++)
            {
                int[] row = plan.Rows[t];
                if (row.Length == 0) continue;
                float x = 0, k = 1;
                for (int j = 0; j < row.Length; j++)
                {
                    Tile tile = plan.Tiles[row[j]];
                    k = AreaFactor(tile, ph);
                    float l = k * tile.Area / Mathf.Max(1e-6f, RadialSize(tile, ph));
                    if (j > 0) x += k * plan.Gap1[row[j]];
                    plan.Length[row[j]] = l;
                    plan.X[row[j]] = x + 0.5f * l;
                    x += l;
                }

                float shift = k * plan.RowMid1[t] - 0.5f * x;
                foreach (int i in row) plan.X[i] += shift;
            }
        }

        /// <summary>
        /// One tile at the phases' progress: its three strips (bands sampled along its length, curled and spun) and its
        /// outline. Tile coordinates: u along the row (−½..½ of its length ℓ), v across (−½..½ of its radial size w).
        /// </summary>
        void WriteTile(Tile t, float l, float x, Phases ph)
        {
            float w = RadialSize(t, ph);
            Vector3 from = new Vector3(t.Card.x, t.Card.y, t.Card.z * (1 - ph.A));
            Vector3 to = new Vector3(x, t.Y, t.RMid);
            Vector3 c = Vector3.LerpUnclamped(from, to, ph.B);
            float tilt = 0.5f * Mathf.PI * ph.B;
            Vector3 axis = new Vector3(0, Mathf.Cos(tilt), Mathf.Sin(tilt));
            float kappa = ph.C / t.RMid;
            float spin = t.Beta * ph.D * Mathf.Deg2Rad, cs = Mathf.Cos(spin), sn = Mathf.Sin(spin);

            float vw = Mathf.Lerp(Mathf.Lerp(t.VwCard, t.VwLin, ph.B), t.VwArea, ph.C);
            float vu = Mathf.Lerp(Mathf.Lerp(t.VuCard, t.VuLin, ph.B), t.VuArea, ph.C);
            vu = Mathf.Max(vu, vw);
            float mix = ph.C;
            int id = EconomyIds.LandSector(t.Industry, EconomyIds.SectorWages);
            Strip(t, c, l, w, axis, kappa, cs, sn, -0.5f, vw, Lerp(t.WagesA, t.WagesB, mix), Mathf.Lerp(t.IWagesA, 1f, mix), id);
            Strip(t, c, l, w, axis, kappa, cs, sn, vw, vu, Lerp(t.UpkeepA, t.UpkeepB, mix), Mathf.Lerp(t.IUpkeepA, 1f, mix),
                id + EconomyIds.SectorUpkeep);
            Strip(t, c, l, w, axis, kappa, cs, sn, vu, 0.5f, Lerp(t.OwnersA, t.OwnersB, mix), Mathf.Lerp(t.IOwnersA, t.IOwnersB, mix),
                id + EconomyIds.SectorOwners);

            // the outline: the inner edge along the row, the outer edge back, closed
            loop.Clear();
            int n = t.Segments;
            for (int j = 0; j <= n; j++) loop.Add(Place(c, l, w, axis, kappa, cs, sn, t.RMid, -0.5f + j / (float)n, -0.5f));
            for (int j = n; j >= 0; j--) loop.Add(Place(c, l, w, axis, kappa, cs, sn, t.RMid, -0.5f + j / (float)n, 0.5f));
            loop.Add(loop[0]);
            morphLines.Polyline(loop, t.Edge, TileEdgePx, EconomyIds.LandSector(t.Industry, EconomyIds.SectorEdge), 1f);
        }

        /// <summary>One strip of a tile (tile coordinates v0..v1 across, the whole length along) as a band of the fills.</summary>
        void Strip(Tile t, Vector3 c, float l, float w, Vector3 axis, float kappa, float cs, float sn, float v0, float v1, Color32 color,
            float intensity, int id)
        {
            inner.Clear();
            outer.Clear();
            int n = t.Segments;
            for (int j = 0; j <= n; j++)
            {
                float u = -0.5f + j / (float)n;
                inner.Add(Place(c, l, w, axis, kappa, cs, sn, t.RMid, u, v0));
                outer.Add(Place(c, l, w, axis, kappa, cs, sn, t.RMid, u, v1));
            }

            morphFills.Band(inner, outer, color, id, intensity);
        }

        /// <summary>
        /// A point of a tile (land-local): on the flat tile (center c, length ℓ along +x, radial size w along its height
        /// axis), curled around the bowl's axis (curvature κ: a point at row x and radial offset ρ goes to
        /// C_κ + (1/κ + ρ)(sin κx, 0, cos κx), C_κ = (0, y, r_mid − 1/κ)), then spun about the axis.
        /// </summary>
        static Vector3 Place(Vector3 c, float l, float w, Vector3 axis, float kappa, float cs, float sn, float rMid, float u, float v)
        {
            Vector3 p = c + new Vector3(u * l, 0, 0) + v * w * axis;
            if (kappa > 1e-5f)
            {
                float rho = p.z - rMid, r = 1f / kappa + rho, a = kappa * p.x;
                p = new Vector3(r * Mathf.Sin(a), p.y, rMid - 1f / kappa + r * Mathf.Cos(a));
            }

            return new Vector3(p.x * cs - p.z * sn, p.y, p.x * sn + p.z * cs);
        }

        static Color32 Lerp(Color32 a, Color32 b, float t) => Color32.Lerp(a, b, t);
    }
}
