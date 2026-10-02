using System;
using System.Collections.Generic;
using UnityEngine;

namespace Why.Economy.Land
{
    /// <summary>
    /// The water on the ground (SPEC 4.3): spending runs by category, not by geography, and never uphill.
    /// <list type="number">
    /// <item><b>Rivulets.</b> Each rim player's outflow (its six categories, taxes and imports bundled) runs from its disc's
    /// inner edge radially inward to the canal's outer edge; processing the rim rows from the outermost, a rivulet whose
    /// radial line passes within e_q + 0.02 of an inner player's disc merges into that player's rivulet (flow
    /// accumulation), so aligned players form creeks. Tower and crown players' rivulets fall from their discs outward and
    /// down to the canal's inner edge.</item>
    /// <item><b>The lip canal.</b> A ring channel at r 4.70 outward with eight lanes (BASE, SELFISH, MATING, taxes,
    /// abroad, <see cref="LandStyle.CanalOrder"/>): each creek's dollars enter every lane at the creek's angle and run the
    /// shorter way round to the lane's exit; a lane's flow at each whole degree is the sum of the sources whose shorter arc
    /// passes it; lanes stack radially (a streamgraph around the ring).</item>
    /// <item><b>The falls.</b> A category exits at the weighted circular median of its kept sinks' center angles (whole
    /// degrees, ties to the smaller angle), falls closer than 10° pushed apart symmetrically; taxes at the federal wedge's
    /// angle; abroad in the middle of the largest gap between rim players.</item>
    /// <item><b>The rivers.</b> At its fall a lane bridges the inner lanes, pours over the lip and runs radially down the
    /// terraces; on every ring that holds its sinks it branches into level distributaries along the ring's outer edge
    /// (r = B_t − 0.05 − 0.035 c) that shed into each sink's pool at the sector's center angle (a short level stub reaches
    /// the pool when it lies beyond the distributary's radius). The river stops at the lowest ring with a kept sink
    /// (≥ 1% of the category).</item>
    /// <item><b>Monotone beds.</b> Every point's y ≤ its predecessor's + 1e-4 (counted).</item>
    /// </list>
    /// Taxes run the same way to the government floor (split federal / state &amp; local); imports pour out over the rim
    /// at the abroad exit. Every junction is checked (in = out). Reproduces the prototype synth/canal_median2.py (its falls
    /// and widths). Pure and deterministic; any thread.
    /// </summary>
    public static class CanalRouting
    {
        /// <summary>Lanes: 0-5 the categories (EconomyData.CategoryIds order), 6 taxes, 7 abroad.</summary>
        public const int Lanes = 8, CategoryLanes = 6;

        /// <summary>A bed point's lift above the surface it runs on (the rivulets' lift).</summary>
        const float Lift = LandStyle.RivuletLift;

        /// <summary>Points of a trunk segment and the angular step of a distributary's arc (degrees), its point limits.</summary>
        const int TrunkPoints = 16, ArcPointsMin = 4, ArcPointsMax = 24;

        const float ArcStepDeg = 4f;

        /// <summary>The tax river ends on the federal wedge this far out (share of the floor's radius).</summary>
        const float FloorEnd = 0.55f;

        /// <summary>The abroad pour: points down the outer wall, its offset outside the wall.</summary>
        const int PourPoints = 8;

        const float PourOut = 0.03f;

        /// <summary>One player's (or creek's) outflow by lane ($B): fear and desire of spending, fantasy, plain (taxes).</summary>
        public sealed class Outflow
        {
            public readonly double[] Fear = new double[Lanes], Desire = new double[Lanes], Fantasy = new double[Lanes],
                Plain = new double[Lanes];

            /// <summary>All dollars of every lane.</summary>
            public double Total
            {
                get
                {
                    double s = 0;
                    for (int l = 0; l < Lanes; l++) s += Fear[l] + Desire[l] + Plain[l];
                    return s;
                }
            }

            public void Add(Outflow o)
            {
                for (int l = 0; l < Lanes; l++)
                {
                    Fear[l] += o.Fear[l];
                    Desire[l] += o.Desire[l];
                    Fantasy[l] += o.Fantasy[l];
                    Plain[l] += o.Plain[l];
                }
            }

            public void SumSpending(out double fear, out double desire, out double fantasy, out double plain)
            {
                fear = desire = fantasy = plain = 0;
                for (int l = 0; l < Lanes; l++)
                {
                    fear += Fear[l];
                    desire += Desire[l];
                    fantasy += Fantasy[l];
                    plain += Plain[l];
                }
            }
        }

        /// <summary>What the routing measured: junction balance, monotone beds, where the rivers end, the canal's widths.</summary>
        public sealed class Report
        {
            /// <summary>Junctions checked and the largest |in − out| ($B) among them, with its name.</summary>
            public int Junctions;

            public double JunctionError;
            public string WorstJunction = "";

            /// <summary>Points that climb more than <see cref="LandStyle.UphillTolerance"/> above their predecessor.</summary>
            public int Uphill;

            /// <summary>River ends (deliveries to a pool) and how many of them lie inside their pool.</summary>
            public int PoolEnds, PoolEndsInside;

            /// <summary>Creek heads (rivulets entering the canal) and merged rivulets.</summary>
            public int Heads, Merged;

            /// <summary>The canal's widest and mean cross-section (u: lanes and gaps), and where it is widest (degrees).</summary>
            public double CanalMax, CanalMean;

            public int CanalMaxAt;

            /// <summary>Dollars of sinks on rings below a river's lowest kept ring, joined to its nearest kept sink ($B).</summary>
            public double Joined;

            /// <summary>Distributary segments, stubs.</summary>
            public int Segments, Stubs;

            /// <summary>Dollars each lane's river delivered to each sector ($B): [lane][industry].</summary>
            public readonly double[][] Delivered = new double[Lanes][];
        }

        /// <summary>
        /// Routes every player's outflow: falls (into <see cref="MoneyFlows.Fall"/>), rivulets and creeks, the canal
        /// lanes, rivers and distributaries into the pools (<see cref="MoneyFlows.PoolInflow"/>, fear and fantasy), the tax
        /// river to the floor and the abroad pour (paths into <see cref="MoneyFlows.Paths"/>).
        /// </summary>
        /// <param name="shares">The seller matrix C[c, k] (rows sum to 1).</param>
        /// <param name="federal">The federal wedge's industry index; <paramref name="stateLocal"/> the state &amp; local one.</param>
        /// <param name="stateLocalTaxShare">The state &amp; local share of personal taxes (the floor split).</param>
        public static Report Route(MoneyFlows m, LandGeometry land, PlayerSet players, Outflow[] outflow, double[,] shares,
            int federal, int stateLocal, double stateLocalTaxShare)
        {
            Report rep = new Report();
            int ni = land.Sectors.Length;
            for (int l = 0; l < Lanes; l++) rep.Delivered[l] = new double[ni];
            for (int l = 0; l < Lanes; l++) m.Canal[l] = new CanalLane { Lane = l };

            // ---- the river of each category: domestic dollars, sinks (pools first: they shape where the rivers end)
            double[] riverFear = new double[Lanes], riverDesire = new double[Lanes], riverFant = new double[Lanes], riverPlain = new double[Lanes];
            foreach (Outflow o in outflow)
            {
                for (int l = 0; l < Lanes; l++)
                {
                    riverFear[l] += o.Fear[l];
                    riverDesire[l] += o.Desire[l];
                    riverFant[l] += o.Fantasy[l];
                    riverPlain[l] += o.Plain[l];
                }
            }

            double[,] sink = new double[CategoryLanes, ni];   // dollars delivered by category to each sector
            int[] lowest = new int[CategoryLanes];
            for (int c = 0; c < CategoryLanes; c++)
            {
                double dom = riverFear[c] + riverDesire[c];
                lowest[c] = 4;
                for (int k = 0; k < ni; k++)
                {
                    if (shares[c, k] >= LandStyle.SinkMinShare) lowest[c] = Math.Min(lowest[c], (int)land.Sectors[k].Tier);
                }

                for (int k = 0; k < ni; k++)
                {
                    double v = dom * shares[c, k];
                    if (v <= 0) continue;
                    int t = (int)land.Sectors[k].Tier;
                    if (t >= lowest[c])
                    {
                        sink[c, k] += v;
                        continue;
                    }

                    // below the river's lowest kept ring: joins the nearest kept sink on that ring
                    int best = -1;
                    float bestD = float.MaxValue;
                    for (int q = 0; q < ni; q++)
                    {
                        if ((int)land.Sectors[q].Tier != lowest[c] || shares[c, q] < LandStyle.SinkMinShare) continue;
                        float d = Mathf.Abs(LandMath.DeltaDeg(land.Sectors[k].Mid, land.Sectors[q].Mid));
                        if (d < bestD)
                        {
                            bestD = d;
                            best = q;
                        }
                    }

                    if (best < 0) best = k;
                    sink[c, best] += v;
                    rep.Joined += v;
                }

                double fearShare = dom > 0 ? riverFear[c] / dom : 0, fantShare = dom > 0 ? riverFant[c] / dom : 0;
                for (int k = 0; k < ni; k++)
                {
                    m.PoolInflow[k] += sink[c, k];
                    m.PoolFear[k] += sink[c, k] * fearShare;
                    m.PoolFantasy[k] += sink[c, k] * fantShare;
                }
            }

            // ---- falls
            Falls(m, land, players, shares, federal);

            // ---- creeks: rim rows from the outermost; a rivulet merges into the first inner disc its radial line passes
            int np = players.Players.Length;
            int[] into = new int[np];
            Outflow[] acc = new Outflow[np];
            for (int p = 0; p < np; p++)
            {
                into[p] = -1;
                acc[p] = new Outflow();
                acc[p].Add(outflow[p]);
            }

            for (int row = LandStyle.RimRows.Length - 1; row >= 0; row--)
            {
                for (int p = 0; p < np; p++)
                {
                    Player a = players.Players[p];
                    if (a.Place != Place.Rim || a.Row != row) continue;
                    into[p] = MergeTarget(players, a);
                    if (into[p] < 0) continue;
                    acc[into[p]].Add(acc[p]);
                    rep.Merged++;
                }
            }

            // ---- the canal: every head's dollars from its angle the shorter way to each lane's exit
            List<int> heads = new List<int>();
            for (int p = 0; p < np; p++)
            {
                if (into[p] < 0 && acc[p].Total > 0) heads.Add(p);
            }

            rep.Heads = heads.Count;
            double[] entered = new double[Lanes];
            double[,] direct = new double[Lanes, 4];   // fear, desire, fantasy, plain entering right at the exit degree
            foreach (int h in heads)
            {
                double theta = LandMath.Wrap360((double)players.Players[h].Theta);
                Outflow o = acc[h];
                for (int l = 0; l < Lanes; l++)
                {
                    double fear = o.Fear[l], desire = o.Desire[l], fant = o.Fantasy[l], plain = o.Plain[l];
                    if (fear + desire + plain <= 0) continue;
                    entered[l] += fear + desire + plain;
                    CanalLane lane = m.Canal[l];
                    double da = LandMath.DeltaDeg(theta, (double)lane.Exit);
                    double end = theta + da;
                    int lo = da >= 0 ? (int)Math.Ceiling(theta - 1e-9) : (int)Math.Ceiling(end - 1e-9);
                    int hi = da >= 0 ? (int)Math.Floor(end + 1e-9) : (int)Math.Floor(theta + 1e-9);
                    sbyte dir = (sbyte)(da >= 0 ? 1 : -1);
                    int exit = Deg(lane.Exit), written = 0;
                    for (int k = lo; k <= hi; k++)
                    {
                        int phi = ((k % 360) + 360) % 360;
                        if (phi == exit) continue;   // the water leaves the canal at its exit: the lane's flow ends before it
                        written++;
                        lane.Fear[phi] += fear;
                        lane.Desire[phi] += desire;
                        lane.Fantasy[phi] += fant;
                        lane.Plain[phi] += plain;
                        lane.Direction[phi] = dir;
                    }

                    if (written > 0) continue;
                    direct[l, 0] += fear;
                    direct[l, 1] += desire;
                    direct[l, 2] += fant;
                    direct[l, 3] += plain;
                }
            }

            // at each exit the lane's two sides arrive (and the heads standing right at it): what falls; the exit degree
            // itself shows the larger side's cross-section (the water leaving)
            double[] arriving = new double[Lanes];
            for (int l = 0; l < Lanes; l++)
            {
                CanalLane lane = m.Canal[l];
                int e = Deg(lane.Exit), before = (e + 359) % 360, after = (e + 1) % 360;
                double ccw = lane.Direction[before] == 1 ? lane.Fear[before] + lane.Desire[before] + lane.Plain[before] : 0;
                double cw = lane.Direction[after] == -1 ? lane.Fear[after] + lane.Desire[after] + lane.Plain[after] : 0;
                double here = direct[l, 0] + direct[l, 1] + direct[l, 3];
                arriving[l] = ccw + cw + here;
                int side = ccw >= cw ? before : after;
                bool any = ccw > 0 || cw > 0;
                lane.Fear[e] = (any ? lane.Fear[side] : 0) + direct[l, 0];
                lane.Desire[e] = (any ? lane.Desire[side] : 0) + direct[l, 1];
                lane.Fantasy[e] = (any ? lane.Fantasy[side] : 0) + direct[l, 2];
                lane.Plain[e] = (any ? lane.Plain[side] : 0) + direct[l, 3];
                lane.Direction[e] = 0;
                Junction(rep, "canal " + l + " exit", entered[l], arriving[l]);
            }

            Stack(m, land);
            double sumW = 0;
            for (int phi = 0; phi < 360; phi++)
            {
                double w = OuterEdge(m, phi) - LandStyle.CanalR0;
                sumW += w;
                if (w > rep.CanalMax)
                {
                    rep.CanalMax = w;
                    rep.CanalMaxAt = phi;
                }
            }

            rep.CanalMean = sumW / 360;

            // ---- rivulets
            double headsSum = 0, playersSum = 0;
            for (int p = 0; p < np; p++)
            {
                Player a = players.Players[p];
                Outflow o = acc[p];
                playersSum += outflow[p].Total;
                if (o.Total <= 0) continue;
                Vector3[] pts = into[p] >= 0 ? MergedRivulet(a, players.Players[into[p]]) : a.Place == Place.Rim ? RimRivulet(m, a) : Descent(a);
                if (into[p] < 0) headsSum += o.Total;
                o.SumSpending(out double fear, out double desire, out double fant, out double plain);
                FlowPath f = Add(m, FlowKind.Rivulet, p, into[p] >= 0 ? into[p] : -1, pts, fear + desire + plain);
                f.Fear = fear;
                f.Desire = desire;
                f.Fantasy = fant;
            }

            Junction(rep, "creeks", playersSum, headsSum);

            // ---- the rivers of the six categories
            for (int c = 0; c < CategoryLanes; c++)
            {
                double dom = riverFear[c] + riverDesire[c];
                if (dom <= 0) continue;
                double fearShare = riverFear[c] / dom, fantShare = riverFant[c] / dom;
                List<(int k, double v)> sinks = new List<(int, double)>();
                for (int k = 0; k < ni; k++)
                {
                    if (sink[c, k] > 0) sinks.Add((k, sink[c, k]));
                }

                River(m, rep, land, c, c, dom, arriving[c], fearShare, fantShare, sinks, lowest[c], FlowKind.River);
            }

            // ---- taxes: to the floor, split federal / state & local
            double taxes = riverPlain[LandStyle.LaneTaxes];
            if (taxes > 0)
            {
                List<(int k, double v)> floor = new List<(int, double)> { (federal, taxes * (1 - stateLocalTaxShare)) };
                if (stateLocal >= 0 && stateLocalTaxShare > 0) floor.Add((stateLocal, taxes * stateLocalTaxShare));
                River(m, rep, land, LandStyle.LaneTaxes, LandStyle.LaneTaxes, taxes, arriving[LandStyle.LaneTaxes], 0, 0, floor, 0, FlowKind.Taxes);
            }

            // ---- imports pour over the rim
            double imports = riverFear[LandStyle.LaneAbroad] + riverDesire[LandStyle.LaneAbroad];
            if (imports > 0)
            {
                FlowPath pour = Add(m, FlowKind.Abroad, -1, -3, Pour(m), imports);
                pour.Lane = LandStyle.LaneAbroad;
                pour.Fear = riverFear[LandStyle.LaneAbroad];
                pour.Desire = riverDesire[LandStyle.LaneAbroad];
                pour.Fantasy = riverFant[LandStyle.LaneAbroad];
                Junction(rep, "abroad pour", arriving[LandStyle.LaneAbroad], imports);
            }

            m.Imports = imports;

            // ---- monotone beds
            foreach (FlowPath f in m.Paths)
            {
                if (f.Kind < FlowKind.Rivulet || f.Points == null) continue;
                for (int k = 1; k < f.Points.Length; k++)
                {
                    if (f.Points[k].y > f.Points[k - 1].y + LandStyle.UphillTolerance) rep.Uphill++;
                }
            }

            return rep;
        }

        // ================================================================== falls

        /// <summary>
        /// The exits: each category's weighted circular median over its kept sinks (whole degrees, ties to the smaller),
        /// falls closer than 10° pushed apart symmetrically about their midpoint (repeated until stable, at most 10
        /// passes, the wrap pair included); taxes at the federal wedge's center; abroad in the middle of the largest gap
        /// between rim players' angles.
        /// </summary>
        static void Falls(MoneyFlows m, LandGeometry land, PlayerSet players, double[,] shares, int federal)
        {
            int ni = land.Sectors.Length;
            double[] deg = new double[CategoryLanes];
            for (int c = 0; c < CategoryLanes; c++)
            {
                double best = double.MaxValue;
                int at = 0;
                for (int phi = 0; phi < 360; phi++)
                {
                    double cost = 0;
                    for (int k = 0; k < ni; k++)
                    {
                        if (shares[c, k] >= LandStyle.SinkMinShare) cost += shares[c, k] * Math.Abs(LandMath.DeltaDeg((double)phi, land.Sectors[k].Mid));
                    }

                    if (cost < best - 1e-12)
                    {
                        best = cost;
                        at = phi;
                    }
                }

                deg[c] = at;
            }

            double sep = LandStyle.FallSeparationDeg;
            int[] order = new int[CategoryLanes];
            for (int pass = 0; pass < 10; pass++)
            {
                bool moved = false;
                for (int c = 0; c < CategoryLanes; c++) order[c] = c;
                Array.Sort(order, (a, b) => deg[a] != deg[b] ? deg[a].CompareTo(deg[b]) : a.CompareTo(b));
                for (int i = 0; i + 1 < CategoryLanes; i++)
                {
                    int a = order[i], b = order[i + 1];
                    if (deg[b] - deg[a] >= sep) continue;
                    double mid = 0.5 * (deg[a] + deg[b]);
                    deg[a] = mid - sep / 2;
                    deg[b] = mid + sep / 2;
                    moved = true;
                }

                // the wrap pair: the last and the first a turn later
                int first = order[0], last = order[CategoryLanes - 1];
                if (deg[first] + 360 - deg[last] < sep)
                {
                    double mid = 0.5 * (deg[last] + deg[first] + 360);
                    deg[last] = mid - sep / 2;
                    deg[first] = mid + sep / 2 - 360;
                    moved = true;
                }

                if (!moved) break;
            }

            for (int c = 0; c < CategoryLanes; c++) m.Fall[c] = (float)LandMath.Wrap360(Math.Round(LandMath.Wrap360(deg[c]), MidpointRounding.ToEven));
            m.Fall[LandStyle.LaneTaxes] = federal >= 0 ? (float)LandMath.Wrap360(Math.Round(LandMath.Wrap360((double)land.Sectors[federal].Mid))) : 90f;

            List<double> angles = new List<double>();
            foreach (Player p in players.Players)
            {
                if (p.Place == Place.Rim) angles.Add(LandMath.Wrap360((double)p.Theta));
            }

            angles.Sort();
            double gap = -1, gapMid = 90;
            for (int k = 0; k < angles.Count; k++)
            {
                double a = angles[k], b = k + 1 < angles.Count ? angles[k + 1] : angles[0] + 360;
                if (b - a > gap)
                {
                    gap = b - a;
                    gapMid = 0.5 * (a + b);
                }
            }

            m.Fall[LandStyle.LaneAbroad] = (float)LandMath.Wrap360(Math.Round(LandMath.Wrap360(gapMid)));
            for (int l = 0; l < Lanes; l++)
            {
                m.Fall[l] = Deg(m.Fall[l]);
                m.Canal[l].Exit = m.Fall[l];
            }
        }

        /// <summary>A whole degree in [0, 360).</summary>
        static int Deg(float deg) => ((int)Math.Round(deg) % 360 + 360) % 360;

        // ================================================================== creeks and rivulets

        /// <summary>
        /// The inner-row player whose disc a rim player's radial line passes within e_q + 0.02 of (the first met going
        /// inward: the largest radius; ties the nearest, then the lower index), or -1.
        /// </summary>
        static int MergeTarget(PlayerSet set, Player a)
        {
            float r0 = LandStyle.CanalR0, r1 = a.R - a.Radius;
            int best = -1;
            float bestR = -1, bestD = float.MaxValue;
            foreach (Player q in set.Players)
            {
                if (q.Place != Place.Rim || q.Row < 0 || q.Row >= a.Row) continue;
                float dth = LandMath.DeltaDeg(a.Theta, q.Theta) * Mathf.Deg2Rad;
                float along = q.R * Mathf.Cos(dth), off = Mathf.Abs(q.R * Mathf.Sin(dth));
                if (along < r0 || along > r1) continue;
                float e = q.Radius + (q.Plinth ? LandStyle.PlinthPad : 0);
                if (off > e + LandStyle.CreekPad) continue;
                if (q.R > bestR + 1e-6f || Mathf.Abs(q.R - bestR) <= 1e-6f && (off < bestD - 1e-6f || Mathf.Abs(off - bestD) <= 1e-6f && q.Index < best))
                {
                    best = q.Index;
                    bestR = q.R;
                    bestD = off;
                }
            }

            return best;
        }

        static float RivuletY => LandStyle.RimY + Lift;

        /// <summary>A merged rivulet: from the disc's inner edge straight to the edge of the disc it joins (level).</summary>
        static Vector3[] MergedRivulet(Player a, Player q)
        {
            Vector3 ca = Figure.Center(a), cq = Figure.Center(q);
            ca.y = cq.y = RivuletY;
            Vector3 toQ = cq - ca;
            Vector3 start = ca + LandFrame.Radial(a.Theta) * -a.Radius;
            float eq = q.Radius + (q.Plinth ? LandStyle.PlinthPad : 0);
            Vector3 end = cq - toQ.normalized * eq;
            return Line(start, end, LandStyle.RivuletPoints);
        }

        /// <summary>A creek head on the rim: radially inward from the disc's inner edge to the canal's outer edge at its angle.</summary>
        static Vector3[] RimRivulet(MoneyFlows m, Player a)
        {
            float outer = OuterEdge(m, Deg(a.Theta));
            float r0 = Mathf.Max(outer, LandStyle.CanalR0), r1 = Mathf.Max(r0, a.R - a.Radius);
            return Line(LandFrame.Polar(r1, a.Theta, RivuletY), LandFrame.Polar(r0, a.Theta, RivuletY), LandStyle.RivuletPoints);
        }

        /// <summary>
        /// A tower or crown player's rivulet: from its disc outward and down to the canal's inner edge at its angle (a
        /// smooth monotone descent; a disc below the canal starts at the canal's height).
        /// </summary>
        static Vector3[] Descent(Player a)
        {
            float y0 = Mathf.Max(a.Y, LandStyle.CanalY + 0.002f), y1 = LandStyle.CanalY + 0.001f;
            int n = LandStyle.RivuletPoints * 2;
            Vector3[] pts = new Vector3[n];
            for (int k = 0; k < n; k++)
            {
                float t = k / (float)(n - 1);
                float r = Mathf.Lerp(a.R, LandStyle.CanalR0, t);
                float y = Mathf.Lerp(y0, y1, t * t * (3 - 2 * t));
                pts[k] = LandFrame.Polar(r, a.Theta, y);
            }

            return pts;
        }

        static Vector3[] Line(Vector3 a, Vector3 b, int n)
        {
            Vector3[] pts = new Vector3[n];
            for (int k = 0; k < n; k++) pts[k] = Vector3.Lerp(a, b, k / (float)(n - 1));
            return pts;
        }

        // ================================================================== the canal's cross-section

        /// <summary>
        /// Lanes stack radially from the canal's inner edge in <see cref="LandStyle.CanalOrder"/> (a lane's inner radius =
        /// 4.70 + the widths of the lanes inside it and a 0.004 gap after each one that carries water); a lane with both
        /// motives keeps a 0.004 gap between its ice and rose ribbons.
        /// </summary>
        static void Stack(MoneyFlows m, LandGeometry land)
        {
            for (int phi = 0; phi < 360; phi++)
            {
                float r = LandStyle.CanalR0;
                foreach (int l in LandStyle.CanalOrder)
                {
                    CanalLane lane = m.Canal[l];
                    lane.R0[phi] = r;
                    float wf = (float)(lane.Fear[phi] * land.WidthPerB), wd = (float)(lane.Desire[phi] * land.WidthPerB);
                    float wp = (float)(lane.Plain[phi] * land.WidthPerB);
                    float w = wf + wd + wp + (wf > 0 && wd > 0 ? LandStyle.LaneGap : 0);
                    lane.R1[phi] = r + w;
                    if (w > 0) r = lane.R1[phi] + LandStyle.LaneGap;
                }
            }
        }

        /// <summary>The canal's outer edge at a whole degree (the outermost lane's outer radius).</summary>
        public static float OuterEdge(MoneyFlows m, int phi)
        {
            float top = LandStyle.CanalR0;
            foreach (CanalLane l in m.Canal)
            {
                if (l != null && l.R1[phi] > l.R0[phi]) top = Mathf.Max(top, l.R1[phi]);
            }

            return top;
        }

        /// <summary>A lane's center radius at a whole degree (its inner edge when it is empty there).</summary>
        public static float LaneMid(CanalLane lane, int phi) => 0.5f * (lane.R0[phi] + lane.R1[phi]);

        // ================================================================== rivers and distributaries

        /// <summary>The level radius of a lane's distributaries on a ring: B_t − 0.05 − 0.035 × lane.</summary>
        public static float DistributaryR(int tier, int lane) =>
            LandStyle.RingB[tier] - LandStyle.DistributaryInset - LandStyle.DistributaryStep * lane;

        /// <summary>
        /// One lane's river (<paramref name="arriving"/>: what the canal brings to its exit, checked against the river's
        /// dollars): the waterfall from the canal over the lip, the trunk down the terraces in segments (each
        /// ring that holds sinks takes its share), and on each such ring the distributaries: one chain per direction,
        /// level along the ring's outer edge, shedding into each sink's pool at its center angle (a stub reaches a pool
        /// beyond the chain's radius). The trunk ends on the lowest ring with sinks.
        /// </summary>
        static void River(MoneyFlows m, Report rep, LandGeometry land, int lane, int slot, double dollars, double arriving, double fearShare,
            double fantShare, List<(int k, double v)> sinks, int lowestRing, FlowKind kind)
        {
            CanalLane canal = m.Canal[lane];
            int e = Deg(canal.Exit);
            float phi = e;
            Junction(rep, "fall " + lane, arriving, dollars);

            // the waterfall: bridge the inner lanes at the canal's height, then over the lip to the tech tread
            List<Vector3> fall = new List<Vector3>(LandStyle.WaterfallPoints + 2)
            {
                LandFrame.Polar(LaneMid(canal, e), phi, LandStyle.CanalY + 0.002f),
                LandFrame.Polar(LandStyle.LipR + 0.01f, phi, LandStyle.CanalY + 0.001f)
            };
            for (int k = 0; k < LandStyle.WaterfallPoints; k++)
            {
                float t = k / (float)(LandStyle.WaterfallPoints - 1);
                fall.Add(LandFrame.Polar(Mathf.Lerp(LandStyle.LipR, LandStyle.RingB[4], t), phi,
                    Mathf.Lerp(LandStyle.RimY, LandStyle.TerraceY[4], t * t) + Lift));
            }

            FlowPath wf = Add(m, FlowKind.Waterfall, -1, -1, fall.ToArray(), dollars);
            Motive(wf, lane, fearShare, fantShare, kind);
            if (kind == FlowKind.Taxes) wf.Kind = FlowKind.Taxes;

            // rings from tech down to the lowest with sinks
            double remaining = dollars;
            float rFrom = LandStyle.RingB[4];
            for (int t = 4; t >= lowestRing && remaining > 1e-9; t--)
            {
                double onRing = 0;
                foreach ((int k, double v) in sinks)
                {
                    if ((int)land.Sectors[k].Tier == t) onRing += v;
                }

                if (onRing <= 0 && t > lowestRing) continue;
                float rd = t == 0 && kind == FlowKind.Taxes ? LandStyle.RingB[0] * FloorEnd : DistributaryR(t, slot);
                FlowPath trunk = Add(m, kind == FlowKind.Taxes ? FlowKind.Taxes : FlowKind.River, -1, -1, Bed(rFrom, rd, phi), remaining);
                Motive(trunk, lane, fearShare, fantShare, kind);
                double after = remaining - onRing;
                double shed = Chains(m, rep, land, lane, t, rd, phi, sinks, fearShare, fantShare, kind);
                Junction(rep, "ring " + t + " lane " + lane, remaining, shed + Math.Max(0, after));
                remaining = after;
                rFrom = rd;
            }
        }

        /// <summary>
        /// The distributaries of one ring from the trunk at angle phi, radius rd: the ring's sinks split by the side of
        /// the shorter way (ccw, cw, or straight at the trunk), each side one chain of level arcs from sink to sink;
        /// returns the dollars shed on the ring.
        /// </summary>
        static double Chains(MoneyFlows m, Report rep, LandGeometry land, int lane, int tier, float rd, float phi,
            List<(int k, double v)> sinks, double fearShare, double fantShare, FlowKind kind)
        {
            float y = LandStyle.TerraceY[tier] + Lift;
            List<(int k, double v, float d)> ccw = new List<(int, double, float)>(), cw = new List<(int, double, float)>();
            double shed = 0;
            foreach ((int k, double v) in sinks)
            {
                SectorGeom s = land.Sectors[k];
                if ((int)s.Tier != tier || v <= 0) continue;
                float d = LandMath.DeltaDeg(phi, s.Mid);
                if (d >= 0) ccw.Add((k, v, d));
                else cw.Add((k, v, -d));
                shed += v;
            }

            ccw.Sort((a, b) => a.d != b.d ? a.d.CompareTo(b.d) : a.k.CompareTo(b.k));
            cw.Sort((a, b) => a.d != b.d ? a.d.CompareTo(b.d) : a.k.CompareTo(b.k));
            foreach ((List<(int k, double v, float d)> chain, float sign) in new[] { (ccw, 1f), (cw, -1f) })
            {
                double carry = 0;
                foreach ((int _, double v, float _) in chain) carry += v;
                float at = 0;
                for (int i = 0; i < chain.Count; i++)
                {
                    (int k, double v, float d) = chain[i];
                    SectorGeom s = land.Sectors[k];
                    if (d - at > 1e-3f)
                    {
                        Vector3[] arc = LevelArc(rd, phi + sign * at, phi + sign * d, y);
                        FlowPath seg = Add(m, kind == FlowKind.Taxes ? FlowKind.Taxes : FlowKind.Distributary, -1, k, arc, carry);
                        Motive(seg, lane, fearShare, fantShare, kind);
                        rep.Segments++;
                    }

                    // the delivery: at the sector's center angle, inside its pool (a level stub outward when needed)
                    Vector3 end = LandFrame.Polar(rd, s.Mid, y);
                    float r0, r1;
                    if (kind == FlowKind.Taxes)
                    {
                        r0 = 0;
                        r1 = s.R1;
                    }
                    else
                    {
                        PoolBand(s, m.PoolInflow[k], out r0, out r1);
                    }

                    if (rd < r0 - 1e-4f || rd > r1 + 1e-4f)
                    {
                        float target = 0.5f * (r0 + r1);
                        Vector3[] stub = Line(end, LandFrame.Polar(target, s.Mid, y), 4);
                        FlowPath st = Add(m, kind == FlowKind.Taxes ? FlowKind.Taxes : FlowKind.Distributary, -1, k, stub, v);
                        Motive(st, lane, fearShare, fantShare, kind);
                        end = stub[stub.Length - 1];
                        rep.Stubs++;
                    }

                    rep.Delivered[lane][k] += v;
                    rep.PoolEnds++;
                    float re = LandFrame.RadiusOf(end), te = LandFrame.ThetaOf(end);
                    if (re >= r0 - 1e-3f && re <= r1 + 1e-3f && LandFrame.InSpan(te + 1e-3f, s.Theta0, s.Theta1 + 2e-3f)) rep.PoolEndsInside++;
                    carry -= v;
                    at = d;
                }

                // a chain sheds everything it carries: nothing is left at its last node
                if (chain.Count > 0) Junction(rep, "chain end ring " + tier + " lane " + lane, 0, carry);
            }

            return shed;
        }

        /// <summary>The radial band a pool covers: from its shore (filling from the outer edge inward) to the sector's outer edge.</summary>
        public static void PoolBand(SectorGeom s, double inflow, out float r0, out float r1)
        {
            double f = s.ValueAdded > 0 ? Math.Min(1, inflow / s.ValueAdded) : 0;
            r1 = s.R1;
            r0 = Mathf.Sqrt(Mathf.Max(0, (float)(s.R1 * (double)s.R1 - f * (s.R1 * (double)s.R1 - s.R0 * (double)s.R0))));
        }

        /// <summary>A level arc at radius r from one angle to another (degrees), 4-24 points.</summary>
        static Vector3[] LevelArc(float r, float from, float to, float y)
        {
            int n = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(to - from) / ArcStepDeg) + 1, ArcPointsMin, ArcPointsMax);
            Vector3[] pts = new Vector3[n];
            for (int k = 0; k < n; k++) pts[k] = LandFrame.Polar(r, Mathf.Lerp(from, to, k / (float)(n - 1)), y);
            return pts;
        }

        /// <summary>
        /// A trunk's bed at angle phi from radius rFrom inward to rTo: nodes at the ends and at every tread edge between,
        /// on the surface + lift; a Catmull-Rom spline, any climb flattened, two Chaikin passes, resampled to
        /// <see cref="TrunkPoints"/> (monotone by construction).
        /// </summary>
        static Vector3[] Bed(float rFrom, float rTo, float phi)
        {
            List<Vector3> nodes = new List<Vector3> { new Vector3(rFrom, LandFrame.SurfaceY(rFrom) + Lift, 0) };
            for (int t = 4; t >= 0; t--)
            {
                foreach (float edge in new[] { LandStyle.RingB[t], LandStyle.RingA[t] })
                {
                    if (edge < rFrom - 1e-4f && edge > rTo + 1e-4f) nodes.Add(new Vector3(edge, LandFrame.SurfaceY(edge) + Lift, 0));
                }
            }

            nodes.Add(new Vector3(rTo, LandFrame.SurfaceY(rTo) + Lift, 0));
            List<Vector3> pts = LandMath.CatmullRom(nodes, 3);
            Flatten(pts);
            pts = LandMath.Chaikin(pts, LandStyle.ChaikinPasses);
            Vector3[] o = LandMath.Resample(pts, TrunkPoints);
            for (int k = 1; k < o.Length; k++) o[k].y = Mathf.Min(o[k].y, o[k - 1].y);
            for (int k = 0; k < o.Length; k++) o[k] = LandFrame.Polar(o[k].x, phi, o[k].y);
            return o;
        }

        /// <summary>No point above its predecessor: a spline's overshoot is cut level.</summary>
        static void Flatten(List<Vector3> pts)
        {
            for (int k = 1; k < pts.Count; k++)
            {
                if (pts[k].y > pts[k - 1].y) pts[k] = new Vector3(pts[k].x, pts[k - 1].y, pts[k].z);
            }
        }

        /// <summary>The abroad lane's pour: from the lane at its exit outward across the rim and down the bowl's outer wall.</summary>
        static Vector3[] Pour(MoneyFlows m)
        {
            CanalLane lane = m.Canal[LandStyle.LaneAbroad];
            int e = Deg(lane.Exit);
            float phi = e;
            List<Vector3> pts = new List<Vector3>(PourPoints + 2)
            {
                LandFrame.Polar(LaneMid(lane, e), phi, LandStyle.CanalY + 0.002f),
                LandFrame.Polar(LandStyle.RimR, phi, LandStyle.CanalY + 0.001f)
            };
            for (int k = 0; k < PourPoints; k++)
            {
                float t = (k + 1) / (float)PourPoints;
                pts.Add(LandFrame.Polar(LandStyle.RimR + PourOut * Mathf.Sqrt(t), phi, LandStyle.CanalY - LandStyle.AbroadFade * t));
            }

            return pts.ToArray();
        }

        // ================================================================== helpers

        static FlowPath Add(MoneyFlows m, FlowKind kind, int from, int to, Vector3[] points, double dollars)
        {
            FlowPath f = new FlowPath { Kind = kind, From = from, To = to, Points = points, Dollars = dollars };
            m.Paths.Add(f);
            return f;
        }

        /// <summary>A river edge's lane and its dollars split by motive (taxes: plain).</summary>
        static void Motive(FlowPath f, int lane, double fearShare, double fantShare, FlowKind kind)
        {
            f.Lane = lane;
            if (kind == FlowKind.Taxes) return;
            f.Fear = f.Dollars * fearShare;
            f.Desire = f.Dollars - f.Fear;
            f.Fantasy = f.Dollars * fantShare;
        }

        static void Junction(Report rep, string name, double input, double output)
        {
            rep.Junctions++;
            double err = Math.Abs(input - output);
            if (err > rep.JunctionError)
            {
                rep.JunctionError = err;
                rep.WorstJunction = name;
            }
        }
    }
}
