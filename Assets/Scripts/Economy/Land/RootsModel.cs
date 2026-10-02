using System;
using System.Collections.Generic;
using UnityEngine;
using Why.Economy.Data;
using DataIndustry = Why.Economy.Data.Industry;

namespace Why.Economy.Land
{
    /// <summary>What a drawn root carries (2.4).</summary>
    public enum RootKind : byte
    {
        /// <summary>One supplier's purchases by one buyer of at least <see cref="LandStyle.RootMinB"/> (2024 dollars).</summary>
        Root = 0,

        /// <summary>The rest of a buyer's suppliers (every smaller flow), one dim root from the ring below.</summary>
        Mesh = 1,

        /// <summary>An industry's purchases from itself: a closed loop under its sector.</summary>
        Ball = 2
    }

    /// <summary>
    /// One drawn root of the land (2.4), land-local: a path from under its supplier's sector that travels beneath the
    /// terraces and rises vertically into its buyer's wage strip (a root ball is a closed loop under its sector instead),
    /// its world width the year's dollars at <see cref="LandStyle.RootWidthGdp"/>. Only the landscape layer and its
    /// labels use it, so the type lives here (8.2).
    /// </summary>
    public sealed class RootGeom
    {
        /// <summary>A root, a mesh root or a root ball.</summary>
        public RootKind Kind;

        /// <summary>Supplier and buyer industry (a mesh root: From = -1; a ball: both the industry).</summary>
        public int From = -1, To = -1;

        /// <summary>
        /// The root's index k (<see cref="EconomyIds.LandRoot"/>): roots ordered by buyer, then by size, so each buyer's
        /// roots are one id range; -1 for mesh roots and balls.
        /// </summary>
        public int Index = -1;

        /// <summary>Rank among all roots by 2024 dollars (0 = the largest; the six largest are labeled); -1 for the others.</summary>
        public int Largest = -1;

        /// <summary>Rank among the buyer's roots by size (its depth step below the buyer; a mesh root comes last).</summary>
        public int Rank;

        /// <summary>
        /// Direction of a root: +1 the supplier stands on a lower ring than the buyer (up), 0 the same ring, -1 a higher
        /// ring (down); 0 for mesh roots and balls.
        /// </summary>
        public int Direction;

        /// <summary>Dollars in the 2024 table, and scaled to the year by the buyer's value added ($B).</summary>
        public double Table, Dollars;

        /// <summary>World width (the year's dollars × <see cref="LandGeometry.RootWidthPerB"/>).</summary>
        public float Width;

        /// <summary>The path (land-local, <see cref="LandStyle.RootPoints"/> points; a ball repeats its first point at the end).</summary>
        public Vector3[] Points;

        /// <summary>The middle of the path's lowest stretch (a root's label stands there).</summary>
        public Vector3 Lowest;
    }

    /// <summary>
    /// The land's dependencies (2.4): the BEA 2024 input-output table on the 25 industries as roots that feed upward.
    /// Between-industry flows of at least <see cref="LandStyle.RootMinB"/> (in the table's dollars, so the same roots
    /// every year) are roots; each buyer's smaller suppliers form one mesh root from its ring below; own-industry
    /// purchases are root balls. Other years scale every flow by its buyer's value added (F_ij(Y) = F_ij(2024) ×
    /// VA_j(Y) / VA_j(2024)). The one place the table is analysed: <see cref="Line"/> (the "[Why] Roots" log line,
    /// printed through <see cref="LandLayout.RootsLine"/>) and <see cref="Build"/> (the drawn roots) read the same
    /// selection. Pure and deterministic: the table is read once per data set (under a lock); safe on any thread.
    /// </summary>
    public static class RootsModel
    {
        /// <summary>The table read once: every flow in file order and the selection made on it.</summary>
        sealed class Table
        {
            /// <summary>The table's year (the io block's, 2024 when missing).</summary>
            public int Year;

            /// <summary>Every flow between known industries as (supplier, buyer, $B), in file order (own flows included).</summary>
            public (int from, int to, double v)[] Flows;

            /// <summary>The roots (indices into <see cref="Flows"/>), ordered by buyer, then size, then supplier.</summary>
            public int[] Roots;

            /// <summary>Per root index: rank by size among all roots (ties by file order).</summary>
            public int[] Largest;

            /// <summary>Per industry: own-industry purchases, and the buyer's between-industry flows below the cut ($B).</summary>
            public double[] Own, Rest;
        }

        static readonly object Gate = new object();
        static EconomyData tableFor;
        static Table table;

        /// <summary>The table's analysis, once per data set.</summary>
        static Table Read(EconomyData data)
        {
            lock (Gate)
            {
                if (table != null && ReferenceEquals(tableFor, data)) return table;
            }

            Table t = Analyse(data);
            lock (Gate)
            {
                tableFor = data;
                table = t;
            }

            return t;
        }

        static Table Analyse(EconomyData data)
        {
            int n = data.Industries.Count;
            List<(int, int, double)> flows = new List<(int, int, double)>();
            if (data.Circuit?.Io != null)
            {
                foreach ((string from, string to, double value) in data.Circuit.Io.Flows())
                {
                    DataIndustry a = data.IndustryById(from), b = data.IndustryById(to);
                    if (a == null || b == null) continue;
                    flows.Add((a.Index, b.Index, value));
                }
            }

            Table t = new Table
            {
                Year = data.Circuit?.Io != null && data.Circuit.Io.Year > 0 ? data.Circuit.Io.Year : LandStyle.LayoutYear,
                Flows = flows.ToArray(), Own = new double[n], Rest = new double[n]
            };

            List<int> roots = new List<int>();
            for (int f = 0; f < t.Flows.Length; f++)
            {
                (int i, int j, double v) = t.Flows[f];
                if (i == j) t.Own[i] += v;
                else if (v >= LandStyle.RootMinB) roots.Add(f);
                else t.Rest[j] += v;
            }

            // by size for the labels (the six largest), ties by file order
            List<int> bySize = new List<int>(roots);
            bySize.Sort((p, q) => t.Flows[p].v != t.Flows[q].v ? t.Flows[q].v.CompareTo(t.Flows[p].v) : p.CompareTo(q));

            // by buyer, then size, then supplier: a buyer's roots are one contiguous id range
            roots.Sort((p, q) =>
            {
                var (pi, pj, pv) = t.Flows[p];
                var (qi, qj, qv) = t.Flows[q];
                if (pj != qj) return pj.CompareTo(qj);
                if (pv != qv) return qv.CompareTo(pv);
                return pi != qi ? pi.CompareTo(qi) : p.CompareTo(q);
            });
            t.Roots = roots.ToArray();
            t.Largest = new int[t.Roots.Length];
            for (int k = 0; k < t.Roots.Length; k++) t.Largest[k] = bySize.IndexOf(t.Roots[k]);
            return t;
        }

        // ------------------------------------------------------------------ the log line

        /// <summary>
        /// The body of the "[Why] Roots" line (8.8), from the table and the year's sector angles: the roots of at least
        /// <see cref="LandStyle.RootMinB"/> and their share of the between-industry dollars, the own-industry dollars, the
        /// direction shares of all kept dollars (supplier on a lower ring: up; the same ring: within; own; a higher ring:
        /// down), the share of between-industry dollars from a higher ring, the share of root dollars connecting sectors
        /// within 90 degrees, and its check (the flows add up to the table's kept total). Every year prints the table's own
        /// dollars and shares (the same roots: the cut applies to the 2024 table); only the share within 90 degrees follows
        /// the year's angles. A year other than the table's adds, before the check, the dollars scaled to that year (the
        /// totals of <see cref="Build"/>'s roots, mesh roots and balls): "; at 1972 value added $0.66T between industries,
        /// own $0.21T".
        /// </summary>
        public static string Line(EconomyData data, LandGeometry g)
        {
            Table tab = Read(data);
            double total = 0, own = 0, up = 0, within = 0, down = 0, rootDollars = 0, near = 0, scaledBetween = 0, scaledOwn = 0;
            int roots = 0;
            foreach ((int i, int j, double v) in tab.Flows)
            {
                total += v;
                double scaled = Scaled(data, v, j, tab.Year, g.Year);
                if (i == j)
                {
                    own += v;
                    scaledOwn += scaled;
                    continue;
                }

                scaledBetween += scaled;
                int ti = (int)g.Sectors[i].Tier, tj = (int)g.Sectors[j].Tier;
                if (ti < tj) up += v;
                else if (ti == tj) within += v;
                else down += v;
                if (v < LandStyle.RootMinB) continue;
                roots++;
                rootDollars += v;
                if (Math.Abs(LandMath.DeltaDeg((double)g.Sectors[i].Mid, g.Sectors[j].Mid)) <= 90) near += v;
            }

            double between = total - own, kept = data.Circuit?.Io?.KeptIntermediate ?? 0;
            bool ok = kept <= 0 || Math.Abs(total / kept - 1) <= 0.001;
            double t = Math.Max(total, 1e-9);
            return roots + " roots >= $" + LandStyle.RootMinB.ToString("0", LandFacts.Ci) + "B carry " +
                   LandFacts.Percent(rootDollars / Math.Max(between, 1e-9), 1) + " of " + LandFacts.Money(between) +
                   " between industries; own " + LandFacts.Money(own) + "; up " + LandFacts.Percent(up / t, 1) + " within " +
                   LandFacts.Percent(within / t, 1) + " own " + LandFacts.Percent(own / t, 1) + " down " +
                   LandFacts.Percent(down / t, 1) + "; " + LandFacts.Percent(down / Math.Max(between, 1e-9)) +
                   " from a higher ring; " + LandFacts.Percent(near / Math.Max(rootDollars, 1e-9)) +
                   " of root dollars within 90 deg" + (g.Year != tab.Year
                       ? "; at " + g.Year.ToString(LandFacts.Ci) + " value added " + LandFacts.Money(scaledBetween) +
                         " between industries, own " + LandFacts.Money(scaledOwn)
                       : "") + "; checks " + (ok ? "1/1 PASS" : "0/1 FAIL (flows " +
                                                                    LandFacts.Money(total) + " vs kept " + LandFacts.Money(kept) + ")");
        }

        /// <summary>A flow bought by an industry, scaled to a year: v × VA_buyer(year) / VA_buyer(table year) (0 without table value added).</summary>
        static double Scaled(EconomyData data, double v, int buyer, int tableYear, int year)
        {
            double vaTable = data.Industries[buyer].ValueAdded.At(tableYear);
            return vaTable > 0 ? v * Math.Max(0, data.Industries[buyer].ValueAdded.At(year)) / vaTable : 0;
        }

        /// <summary>Number of roots (the same every year): the size of the <see cref="EconomyIds.LandRoot"/> range used.</summary>
        public static int RootCount(EconomyData data) => Read(data).Roots.Length;

        // ------------------------------------------------------------------ the drawn roots

        /// <summary>
        /// The year's roots (index order: <see cref="RootGeom.Index"/>), then one mesh root per buyer with smaller
        /// suppliers (industry order), then one root ball per industry that buys from itself (industry order). Paths
        /// follow 2.4: leave the supplier's underside at its ring's mid-radius and center angle (tread − 0.06), travel
        /// beneath the terraces at the buyer's depth (tread − 0.25 − 0.012 × rank; never above the bowl's surface − the
        /// same clearance), the radius lerping and the angle turning the shorter way, and rise vertically into the
        /// buyer's wage strip. The rise points of a buyer's roots are spread across its span in the order of their
        /// suppliers' directions, so they neither overlap nor cross at the buyer.
        /// </summary>
        public static RootGeom[] Build(EconomyData data, LandGeometry land, int year)
        {
            Table tab = Read(data);
            int n = land.Sectors.Length;
            List<RootGeom> all = new List<RootGeom>(tab.Roots.Length + 2 * n);
            List<RootGeom>[] into = new List<RootGeom>[n];
            for (int j = 0; j < n; j++) into[j] = new List<RootGeom>();

            for (int k = 0; k < tab.Roots.Length; k++)
            {
                (int i, int j, double v) = tab.Flows[tab.Roots[k]];
                RootGeom r = new RootGeom
                {
                    Kind = RootKind.Root, From = i, To = j, Index = k, Largest = tab.Largest[k] < LandStyle.RootLabels ? tab.Largest[k] : -1,
                    Direction = Math.Sign((int)land.Sectors[j].Tier - (int)land.Sectors[i].Tier), Table = v,
                    Dollars = Scaled(data, v, j, tab.Year, year)
                };
                into[j].Add(r);
                all.Add(r);
            }

            // ranks among each buyer's roots by size (the index order is by size within a buyer)
            for (int j = 0; j < n; j++)
            {
                for (int q = 0; q < into[j].Count; q++) into[j][q].Rank = q;
            }

            List<RootGeom> mesh = new List<RootGeom>();
            for (int j = 0; j < n; j++)
            {
                if (tab.Rest[j] <= 0) continue;
                RootGeom r = new RootGeom
                {
                    Kind = RootKind.Mesh, From = -1, To = j, Rank = into[j].Count, Table = tab.Rest[j],
                    Dollars = Scaled(data, tab.Rest[j], j, tab.Year, year)
                };
                into[j].Add(r);
                mesh.Add(r);
            }

            for (int j = 0; j < n; j++) Paths(land, j, into[j]);
            all.AddRange(mesh);

            for (int j = 0; j < n; j++)
            {
                if (tab.Own[j] <= 0) continue;
                RootGeom r = new RootGeom
                {
                    Kind = RootKind.Ball, From = j, To = j, Table = tab.Own[j], Dollars = Scaled(data, tab.Own[j], j, tab.Year, year)
                };
                r.Width = (float)(r.Dollars * land.RootWidthPerB);
                r.Points = Ball(land.Sectors[j], r.Width);
                r.Lowest = r.Points[0];
                all.Add(r);
            }

            return all.ToArray();
        }

        /// <summary>
        /// Lays the paths of every root rising into buyer j: rise angles spread over the middle 70% of its span in the
        /// order of the signed angle from the buyer to each supplier (a mesh root in the middle of that order: it comes
        /// from straight below), ties by size.
        /// </summary>
        static void Paths(LandGeometry land, int j, List<RootGeom> roots)
        {
            if (roots.Count == 0) return;
            SectorGeom b = land.Sectors[j];
            float[] key = new float[roots.Count];
            int[] order = new int[roots.Count];
            for (int q = 0; q < roots.Count; q++)
            {
                order[q] = q;
                RootGeom r = roots[q];
                key[q] = r.Kind == RootKind.Mesh ? 0 : LandMath.DeltaDeg(b.Mid, land.Sectors[r.From].Mid);
            }

            Array.Sort(order, (p, q) =>
            {
                int c = key[p].CompareTo(key[q]);
                if (c != 0) return c;
                c = roots[q].Table.CompareTo(roots[p].Table);
                return c != 0 ? c : p.CompareTo(q);
            });

            float span = b.Theta1 - b.Theta0;
            float riseR = 0.5f * (b.R0 + b.RWages);
            for (int s = 0; s < order.Length; s++)
            {
                RootGeom r = roots[order[s]];
                float rise = b.Theta0 + span * (0.15f + 0.7f * (s + 0.5f) / order.Length);
                float clearance = LandStyle.RootLeave + LandStyle.RootStep * r.Rank;
                float depth = b.Y - LandStyle.RootDepth - LandStyle.RootStep * r.Rank;
                float r0, th0, y0;
                if (r.Kind == RootKind.Root)
                {
                    SectorGeom a = land.Sectors[r.From];
                    r0 = LandStyle.RingMid((int)a.Tier);
                    th0 = a.Mid;
                    y0 = a.Y - LandStyle.RootLeave;
                }
                else if (b.Tier > Tier.Gov)
                {
                    // a mesh root: from the ring below, straight under the rise point
                    int below = (int)b.Tier - 1;
                    r0 = LandStyle.RingMid(below);
                    th0 = rise;
                    y0 = LandStyle.TerraceY[below] - LandStyle.RootLeave;
                }
                else
                {
                    // the floor has no ring below: the mesh root comes up from deeper down
                    r0 = riseR;
                    th0 = rise;
                    y0 = depth - LandStyle.RootDepth;
                }

                r.Width = (float)(r.Dollars * land.RootWidthPerB);
                r.Points = Path(r0, th0, y0, riseR, rise, b.Y, depth, clearance);
                r.Lowest = Lowest(r.Points);
            }
        }

        /// <summary>
        /// A path's lowest point: the middle point of its lowest stretch (the points within a few millimetres of its
        /// lowest height), so a root travelling flat is labeled halfway along its travel, not where it reaches that depth.
        /// </summary>
        static Vector3 Lowest(Vector3[] points)
        {
            float min = float.MaxValue;
            foreach (Vector3 p in points) min = Mathf.Min(min, p.y);
            int first = -1, last = -1;
            for (int k = 0; k < points.Length; k++)
            {
                if (points[k].y > min + LowestBand) continue;
                if (first < 0) first = k;
                last = k;
            }

            return points[(first + last) / 2];
        }

        /// <summary>Height band of a path's lowest stretch (world units).</summary>
        const float LowestBand = 0.005f;

        /// <summary>Samples of the travel beneath the terraces before smoothing.</summary>
        const int TravelSamples = 32;

        /// <summary>
        /// A root's path: from (r0, th0, y0) to the travel depth, around the bowl (radius lerped, angle the shorter way,
        /// eased) at min(depth, surface − clearance), then straight up into (r1, th1, y1); corners rounded (Chaikin) and
        /// resampled to <see cref="LandStyle.RootPoints"/> points evenly spaced along its length.
        /// </summary>
        static Vector3[] Path(float r0, float th0, float y0, float r1, float th1, float y1, float depth, float clearance)
        {
            List<Vector3> dense = new List<Vector3>(TravelSamples + 3) { LandFrame.Polar(r0, th0, y0) };
            float turn = LandMath.DeltaDeg(th0, th1);
            for (int s = 0; s <= TravelSamples; s++)
            {
                float e = Mathf.SmoothStep(0, 1, s / (float)TravelSamples);
                float r = Mathf.Lerp(r0, r1, e);
                dense.Add(LandFrame.Polar(r, th0 + turn * e, Mathf.Min(depth, LandFrame.SurfaceY(r) - clearance)));
            }

            dense.Add(LandFrame.Polar(r1, th1, y1));
            return Resample(LandMath.Chaikin(dense, LandStyle.ChaikinPasses), LandStyle.RootPoints);
        }

        /// <summary>
        /// A polyline resampled to n points evenly spaced along its length (n ≥ 2), the ends kept: one walk along the
        /// cumulative length (the same points as <see cref="LandMath.Resample"/>, which measures the whole length again
        /// for every point).
        /// </summary>
        static Vector3[] Resample(List<Vector3> points, int n)
        {
            n = Math.Max(2, n);
            int m = points.Count;
            Vector3[] o = new Vector3[n];
            if (m == 0) return o;
            float[] cum = new float[m];
            for (int i = 1; i < m; i++) cum[i] = cum[i - 1] + Vector3.Distance(points[i - 1], points[i]);
            float total = cum[m - 1];
            int seg = 1;
            for (int k = 0; k < n; k++)
            {
                float target = total * k / (n - 1);
                while (seg < m - 1 && cum[seg] < target) seg++;
                float d = m > 1 ? cum[seg] - cum[seg - 1] : 0;
                o[k] = m == 1 ? points[0] : d > 1e-7f ? Vector3.Lerp(points[seg - 1], points[seg], (target - cum[seg - 1]) / d) : points[seg];
            }

            o[n - 1] = points[m - 1];
            return o;
        }

        /// <summary>
        /// A root ball: a horizontal closed loop under the sector's middle (mid-radius and center angle, the tread −
        /// <see cref="LandStyle.RootBallDepth"/>), its radius a third of the ring's width (at most a third of the sector's
        /// arc there), never less than the loop's own width so the loop stays open.
        /// </summary>
        static Vector3[] Ball(SectorGeom s, float width)
        {
            float rm = LandStyle.RingMid((int)s.Tier);
            float arc = (s.Theta1 - s.Theta0) * Mathf.Deg2Rad * rm;
            float rho = Mathf.Max(width, Mathf.Min((LandStyle.RingB[(int)s.Tier] - LandStyle.RingA[(int)s.Tier]) / 3f, arc / 3f));
            Vector3 c = LandFrame.Polar(rm, s.Mid, s.Y - LandStyle.RootBallDepth);
            Vector3 radial = LandFrame.Radial(s.Mid), tangent = LandFrame.Tangent(s.Mid);
            int n = LandStyle.RootPoints;
            Vector3[] p = new Vector3[n + 1];
            for (int k = 0; k < n; k++)
            {
                float a = 2 * Mathf.PI * k / n;
                p[k] = c + rho * (Mathf.Cos(a) * radial + Mathf.Sin(a) * tangent);
            }

            p[n] = p[0];
            return p;
        }
    }
}
