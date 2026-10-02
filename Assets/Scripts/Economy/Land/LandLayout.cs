using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Why.Economy.Data;
using Why.Economy.Model;
using DataIndustry = Why.Economy.Data.Industry;

namespace Why.Economy.Land
{
    /// <summary>
    /// The bowl of one year (2.1-2.3, 2.6): every industry a sector of its tier's ring, its area the value it adds
    /// (<see cref="LandStyle.AreaGdp"/> u² = the year's GDP), split radially into wages, upkeep and the owners' share; the
    /// rings' order and offsets chosen once on the 2024 input-output table so each sector sits over the suppliers that
    /// feed it most, and kept for every year (only spans change, so sectors grow and shrink in place); the 25 most
    /// valuable companies as towers on their sector's owners' strip, on a plinth where a sector is too narrow for them.
    /// Reproduces the prototype design-agents/bowl2.py at the spec's rounded radii (offsets gov 60, raw 0, make 38,
    /// services 60, tech 90). Pure and deterministic: the order is computed once per data set (under a lock) and every
    /// year's layout is O(25); safe on any thread.
    /// </summary>
    public static class LandLayout
    {
        /// <summary>The rings' fixed arrangement: order of each ring and its run center (radians), from the 2024 table.</summary>
        sealed class Arrangement
        {
            public int[][] Rings;
            public double[] Offset;
            public double Cost;
            public int[] TierOf;
        }

        static readonly object Gate = new object();
        static EconomyData arrangedFor;
        static Arrangement arranged;

        const double Gap = LandStyle.SectorGapDeg * Math.PI / 180;

        /// <summary>The bowl of a calendar year (towers only from <see cref="LandStyle.TowersFromYear"/>).</summary>
        public static LandGeometry Build(EconomyData data, int year)
        {
            Arrangement a = Arrange(data);
            IReadOnlyList<DataIndustry> inds = data.Industries;
            int n = inds.Count;
            double[] va = new double[n];
            double gdp = 0;
            for (int i = 0; i < n; i++)
            {
                va[i] = Math.Max(0, inds[i].ValueAdded.At(year));
                gdp += va[i];
            }

            double gdpSafe = Math.Max(gdp, 1e-9);
            LandGeometry g = new LandGeometry
            {
                Year = year, Gdp = gdp,
                AreaPerB = (float)(LandStyle.AreaGdp / gdpSafe), WidthPerB = (float)(LandStyle.WidthGdp / gdpSafe),
                RootWidthPerB = (float)(LandStyle.RootWidthGdp / gdpSafe), HeightPerB = (float)(LandStyle.HeightGdp / gdpSafe),
                Sectors = new SectorGeom[n], RingOrder = new int[5][], LayoutCost = a.Cost
            };

            double[] span = Spans(va, a.TierOf);
            double[] th0 = new double[n];
            Place(a, span, th0, new double[n]);
            double labor = WallGeometry.LaborRatio(data, year);
            for (int i = 0; i < n; i++)
            {
                int t = a.TierOf[i];
                WallGeometry.Split(inds[i], labor, out double fw, out double fu, out double fo);
                float r0 = LandStyle.RingA[t], r1 = LandStyle.RingB[t];
                float theta0 = (float)LandMath.Wrap360(th0[i] * 180 / Math.PI);
                g.Sectors[i] = new SectorGeom
                {
                    Industry = i, Tier = (Tier)t,
                    Theta0 = theta0, Theta1 = theta0 + (float)(span[i] * 180 / Math.PI),
                    R0 = t == 0 ? 0 : r0, R1 = r1, Y = LandStyle.TerraceY[t],
                    RWages = LandFrame.AreaRadius(t == 0 ? 0 : r0, r1, fw),
                    RUpkeep = LandFrame.AreaRadius(t == 0 ? 0 : r0, r1, fw + fu),
                    ValueAdded = va[i], Wages = va[i] * fw, Upkeep = va[i] * fu, Owners = va[i] * fo
                };
            }

            for (int t = 0; t < 5; t++)
            {
                g.RingOrder[t] = (int[])a.Rings[t].Clone();
                g.RingOffset[t] = (float)LandMath.Wrap360(a.Offset[t] * 180 / Math.PI);
                double s = 0;
                foreach (int i in a.Rings[t]) s += span[i];
                g.RingFill[t] = (float)(s / (2 * Math.PI));
            }

            g.Towers = year >= LandStyle.TowersFromYear ? Towers(data, g) : Array.Empty<TowerGeom>();
            double checksum = 0;
            foreach (SectorGeom s in g.Sectors) checksum += LandMath.Wrap360(s.Mid);
            g.Checksum = checksum;
            g.Log = Describe(data, g);
            return g;
        }

        // ------------------------------------------------------------------ the arrangement (once, on the 2024 table)

        /// <summary>
        /// Computes the rings' fixed arrangement (2.3: the order and the offset scan on the 2024 table, about 20 ms) once
        /// per data set, so that <see cref="Build"/> then only places the year's sectors (well under 1 ms). Build calls it
        /// itself; the land's model layer calls it first to time the two apart. Any thread.
        /// </summary>
        public static void EnsureArranged(EconomyData data) => Arrange(data);

        static Arrangement Arrange(EconomyData data)
        {
            lock (Gate)
            {
                if (arranged != null && ReferenceEquals(arrangedFor, data)) return arranged;
            }

            Arrangement a = Compute(data);
            lock (Gate)
            {
                arrangedFor = data;
                arranged = a;
            }

            return a;
        }

        /// <summary>The ring of an industry: its tier's position gov, raw, make, services, tech.</summary>
        public static int TierIndex(DataIndustry ind)
        {
            switch (ind.TierId)
            {
                case "gov": return 0;
                case "raw": return 1;
                case "make": return 2;
                case "services": return 3;
                case "tech": return 4;
                default: return Math.Max(0, Math.Min(4, ind.TierIndex));
            }
        }

        static Arrangement Compute(EconomyData data)
        {
            IReadOnlyList<DataIndustry> inds = data.Industries;
            int n = inds.Count;
            int[] tier = new int[n];
            double[] va24 = new double[n];
            for (int i = 0; i < n; i++)
            {
                tier[i] = TierIndex(inds[i]);
                va24[i] = Math.Max(0, inds[i].ValueAdded.At(LandStyle.LayoutYear));
            }

            // the table once as arrays (the offset scan evaluates the cost 2,880 times); W = F + F^T between different
            // industries, summed in file order
            List<(int from, int to, double v)> flows = new List<(int, int, double)>(Flows(data));
            double[,] w = new double[n, n];
            foreach ((int from, int to, double v) in flows)
            {
                if (from == to) continue;
                w[from, to] += v;
                w[to, from] += v;
            }

            // ORDER: rings sorted by 2024 value added (largest first), then barycentric sweeps against the rings beside them
            List<int>[] rings = new List<int>[5];
            for (int t = 0; t < 5; t++)
            {
                rings[t] = new List<int>();
                for (int i = 0; i < n; i++)
                {
                    if (tier[i] == t) rings[t].Add(i);
                }

                List<int> r = rings[t];
                r.Sort((p, q) => va24[q] != va24[p] ? va24[q].CompareTo(va24[p]) : p.CompareTo(q));
            }

            double[] ang = new double[n];
            void SetAngles()
            {
                for (int t = 0; t < 5; t++)
                {
                    List<int> r = rings[t];
                    if (t == 0)
                    {
                        // the gov floor: the larger wedge faces the road, the other the far side
                        if (r.Count > 0) ang[r[0]] = Math.PI * 1.5;
                        for (int k = 1; k < r.Count; k++) ang[r[k]] = Math.PI * 0.5;
                        continue;
                    }

                    double tot = 0, acc = 0;
                    foreach (int i in r) tot += va24[i];
                    foreach (int i in r)
                    {
                        double s = tot > 0 ? va24[i] / tot * 2 * Math.PI : 0;
                        ang[i] = acc + s / 2;
                        acc += s;
                    }
                }
            }

            SetAngles();
            int[] even = { 1, 2, 3, 4 }, odd = { 3, 2, 1 };
            double[] key = new double[n];
            for (int sweep = 0; sweep < LandStyle.OrderSweeps; sweep++)
            {
                foreach (int t in sweep % 2 == 0 ? even : odd)
                {
                    foreach (int i in rings[t])
                    {
                        double x = 0, y = 0;
                        for (int j = 0; j < n; j++)
                        {
                            if (Math.Abs(tier[j] - t) != 1) continue;
                            x += w[i, j] * Math.Cos(ang[j]);
                            y += w[i, j] * Math.Sin(ang[j]);
                        }

                        key[i] = PyMod(Math.Atan2(y, x), 2 * Math.PI);
                    }

                    rings[t].Sort((p, q) => key[p] != key[q] ? key[p].CompareTo(key[q]) : p.CompareTo(q));
                    SetAngles();
                }
            }

            Arrangement a = new Arrangement { Rings = new int[5][], TierOf = tier };
            for (int t = 0; t < 5; t++) a.Rings[t] = rings[t].ToArray();

            // OFFSETS: tech's run centered on the far side; scan the others for the least flow-weighted angular distance
            double[] span24 = Spans(va24, tier);
            Near near = new Near(flows, tier);
            double[] off = { 0, 0, 0, 0, LandStyle.TechOffsetDeg * Math.PI / 180 };
            a.Offset = off;
            int[] scanOrder = { 3, 2, 1, 0 };
            double[] th0 = new double[n], mid = new double[n];
            Place(a, span24, th0, mid);
            double best = 0;
            for (int pass = 0; pass < LandStyle.OffsetPasses; pass++)
            {
                foreach (int t in scanOrder)
                {
                    // only ring t moves: compare the cost of the flows that touch it (the rest is a constant)
                    double keep = off[t];
                    bool found = false;
                    best = 0;
                    for (int d = 0; d < 360; d += LandStyle.OffsetStepDeg)
                    {
                        off[t] = d * Math.PI / 180;
                        Place(a, span24, th0, mid, t);
                        double c = near.Cost(mid, t);
                        if (!found || c < best - 1e-6)
                        {
                            found = true;
                            best = c;
                            keep = off[t];
                        }
                    }

                    off[t] = keep;
                    Place(a, span24, th0, mid, t);
                }
            }

            a.Cost = near.Cost(mid, -1);
            return a;
        }

        /// <summary>Python's float modulo (the result has the divisor's sign), as the prototype computes it.</summary>
        static double PyMod(double v, double m)
        {
            double r = v % m;
            return r != 0 && r < 0 != m < 0 ? r + m : r;
        }

        /// <summary>The between-industry flows of the IO table as (supplier, buyer, $B), in file order; unknown ids skipped.</summary>
        static IEnumerable<(int from, int to, double v)> Flows(EconomyData data)
        {
            if (data.Circuit?.Io == null) yield break;
            foreach ((string from, string to, double value) in data.Circuit.Io.Flows())
            {
                DataIndustry a = data.IndustryById(from), b = data.IndustryById(to);
                if (a == null || b == null) continue;
                yield return (a.Index, b.Index, value);
            }
        }

        /// <summary>
        /// The flows the layout cost counts (between different industries on the same or adjacent rings, file order) as
        /// arrays: the offset scan evaluates the cost 2,880 times.
        /// </summary>
        sealed class Near
        {
            const double Turn = 2 * Math.PI;
            readonly int[] from, to;
            readonly double[] value;
            readonly int[][] touching;

            public Near(List<(int from, int to, double v)> flows, int[] tier)
            {
                List<(int, int, double)> kept = flows.FindAll(f => f.from != f.to && Math.Abs(tier[f.from] - tier[f.to]) <= 1);
                from = new int[kept.Count];
                to = new int[kept.Count];
                value = new double[kept.Count];
                for (int k = 0; k < kept.Count; k++) (from[k], to[k], value[k]) = kept[k];
                touching = new int[6][];
                for (int t = -1; t < 5; t++)
                {
                    List<int> ks = new List<int>();
                    for (int k = 0; k < kept.Count; k++)
                    {
                        if (t < 0 || tier[from[k]] == t || tier[to[k]] == t) ks.Add(k);
                    }

                    touching[t + 1] = ks.ToArray();
                }
            }

            /// <summary>Σ F[i][j] |Δθ(mid_i, mid_j)| (radians, the shorter way) over the flows touching ring t (-1: all).</summary>
            public double Cost(double[] mid, int t)
            {
                double c = 0;
                foreach (int k in touching[t + 1])
                {
                    double x = mid[from[k]] - mid[to[k]] + Math.PI;
                    c += value[k] * Math.Abs(x - Turn * Math.Floor(x / Turn) - Math.PI);
                }

                return c;
            }
        }

        /// <summary>Angular span (radians) of every sector for value added va: area / (ring width × mid-radius); gov wedges area / (B² / 2).</summary>
        static double[] Spans(double[] va, int[] tier)
        {
            double tot = 0;
            foreach (double v in va) tot += v;
            double[] sp = new double[va.Length];
            for (int i = 0; i < va.Length; i++)
            {
                double area = tot > 0 ? va[i] / tot * LandStyle.AreaGdp : 0;
                int t = tier[i];
                double a = LandStyle.RingA[t], b = LandStyle.RingB[t];
                sp[i] = t == 0 ? area / (b * b / 2) : area / ((b - a) * (a + b) / 2);
            }

            return sp;
        }

        /// <summary>
        /// Lays every ring's sectors in order from its offset minus half the run (Σ spans plus one gap per sector on
        /// rings 1-4; the gov floor has no gaps), gaps after each sector; fills th0 and mid (radians, unwrapped) for every
        /// ring, or only for ring <paramref name="only"/>.
        /// </summary>
        static void Place(Arrangement a, double[] span, double[] th0, double[] mid, int only = -1)
        {
            for (int t = 0; t < 5; t++)
            {
                if (only >= 0 && t != only) continue;
                int[] ring = a.Rings[t];
                double gap = t > 0 ? Gap : 0, tot = 0;
                foreach (int i in ring) tot += span[i];
                tot += gap * ring.Length;
                double acc = a.Offset[t] - tot / 2;
                foreach (int i in ring)
                {
                    th0[i] = acc;
                    mid[i] = acc + span[i] / 2;
                    acc += span[i] + gap;
                }
            }
        }

        // ------------------------------------------------------------------ towers (2.6)

        static TowerGeom[] Towers(EconomyData data, LandGeometry g)
        {
            List<Company> cap = data.Circuit?.Capture;
            if (cap == null || cap.Count == 0) return Array.Empty<TowerGeom>();
            List<TowerGeom> towers = new List<TowerGeom>(cap.Count);
            for (int c = 0; c < cap.Count; c++)
            {
                Company co = cap[c];
                DataIndustry ind = data.IndustryById(co?.Industry);
                if (co == null || ind == null) continue;
                SectorGeom s = g.Sectors[ind.Index];
                bool priv = co.Revenue <= 0 && co.NetIncome <= 0;
                towers.Add(new TowerGeom
                {
                    Company = c, Industry = ind.Index, Ticker = co.Ticker ?? "", Name = co.Name,
                    R = 0.5f * (s.RUpkeep + s.R1), BaseY = s.Y,
                    Height = (float)(Math.Max(0, co.MarketCap) * g.HeightPerB),
                    Side = priv ? LandStyle.PrivateTowerSide : (float)Math.Sqrt(Math.Max(0, co.NetIncome) * g.AreaPerB),
                    FootR = (float)Math.Sqrt(Math.Max(0, co.Revenue) * g.AreaPerB / Math.PI),
                    Margin = co.Revenue > 0 ? (float)(co.NetIncome / co.Revenue) : 0,
                    Private = priv, MarketCap = co.MarketCap, Revenue = co.Revenue, NetIncome = co.NetIncome
                });
            }

            // per sector: by market value, alternating sides from the center angle (clockwise first), adjacent towers
            // half a side + half a side + TowerGap apart in arc length; a run longer than the sector's arc stands on a plinth
            foreach (SectorGeom s in g.Sectors)
            {
                List<TowerGeom> here = towers.FindAll(t => t.Industry == s.Industry);
                if (here.Count == 0) continue;
                here.Sort((p, q) => p.MarketCap != q.MarketCap ? q.MarketCap.CompareTo(p.MarketCap) : p.Company.CompareTo(q.Company));
                float radius = here[0].R;
                double right = here[0].Side / 2.0, left = -right;
                double[] at = new double[here.Count];
                for (int k = 1; k < here.Count; k++)
                {
                    double side = here[k].Side;
                    if (k % 2 == 1)
                    {
                        at[k] = left - LandStyle.TowerGap - side / 2;
                        left = at[k] - side / 2;
                    }
                    else
                    {
                        at[k] = right + LandStyle.TowerGap + side / 2;
                        right = at[k] + side / 2;
                    }
                }

                double toDeg = 180 / Math.PI / radius;
                double arc = (s.Theta1 - s.Theta0) / toDeg;
                bool plinth = right - left > arc;
                for (int k = 0; k < here.Count; k++)
                {
                    here[k].Theta = s.Mid + (float)(at[k] * toDeg);
                    here[k].OnPlinth = plinth;
                }

                if (plinth)
                {
                    s.Plinth0 = s.Mid + (float)((left - LandStyle.TowerGap / 2) * toDeg);
                    s.Plinth1 = s.Mid + (float)((right + LandStyle.TowerGap / 2) * toDeg);
                }
            }

            Separate(g, towers);

            // in capture order: Towers[k].Company == k whenever every company's industry is known (all 25 are)
            return towers.ToArray();
        }

        /// <summary>Passes of the ring-wide separation (a run moved can only close in on the run beyond it).</summary>
        const int SeparatePasses = 8;

        /// <summary>
        /// The towers' ring-wide minimum gap: a plinth run reaches past its sector and can touch the towers of the sector
        /// beside it (2025: hardware's Broadcom and internet's Meta). On each ring, sector runs are moved as blocks (each
        /// sector keeps its order and spacing) until neighbouring towers of different sectors stand at least
        /// <see cref="LandStyle.TowerGap"/> apart (arc length at the smaller of their radii, <see cref="Deficit"/>), in up
        /// to <see cref="SeparatePasses"/> sweeps over the ring's runs in angular order: a plinth run moves away from
        /// a run that fits its sector, two runs alike share the move. A moved plinth run's plinth follows it (it still
        /// includes the sector's own arc).
        /// </summary>
        static void Separate(LandGeometry g, List<TowerGeom> towers)
        {
            for (int t = 0; t < 5; t++)
            {
                List<(SectorGeom s, List<TowerGeom> run)> runs = new List<(SectorGeom, List<TowerGeom>)>();
                foreach (int i in g.RingOrder[t])
                {
                    List<TowerGeom> run = towers.FindAll(x => x.Industry == i);
                    if (run.Count > 0) runs.Add((g.Sectors[i], run));
                }

                if (runs.Count < 2) continue;

                // angles unwrapped from the side of the ring opposite its run's center (the ring's free arc is there)
                float start = g.RingOffset[t] - 180f;
                float Lo(List<TowerGeom> run)
                {
                    float v = float.MaxValue;
                    foreach (TowerGeom x in run) v = Mathf.Min(v, LandMath.Wrap360(x.Theta - start) - HalfDeg(x));
                    return v;
                }

                runs.Sort((p, q) => Lo(p.run).CompareTo(Lo(q.run)));
                for (int pass = 0; pass < SeparatePasses; pass++)
                {
                    bool moved = false;
                    for (int k = 0; k + 1 < runs.Count; k++)
                    {
                        (SectorGeom sa, List<TowerGeom> a) = runs[k];
                        (SectorGeom sb, List<TowerGeom> b) = runs[k + 1];
                        float deficit = Deficit(a, b, start);
                        if (deficit <= 1e-4f) continue;
                        bool ma = !float.IsNaN(sa.Plinth0), mb = !float.IsNaN(sb.Plinth0);
                        float shareA = ma == mb ? 0.5f : ma ? 1f : 0f;
                        foreach (TowerGeom x in a) x.Theta -= deficit * shareA;
                        foreach (TowerGeom x in b) x.Theta += deficit * (1 - shareA);
                        moved = true;
                    }

                    if (!moved) break;
                }

                foreach ((SectorGeom s, List<TowerGeom> run) in runs)
                {
                    if (float.IsNaN(s.Plinth0)) continue;
                    float pad = (float)(LandStyle.TowerGap / 2 / run[0].R * 180 / Math.PI);
                    float lo = float.MaxValue, hi = float.MinValue;
                    foreach (TowerGeom x in run)
                    {
                        // tower angles relative to the sector's middle (the run never reaches half a turn away)
                        float rel = LandMath.DeltaDeg(s.Mid, x.Theta);
                        lo = Mathf.Min(lo, rel - HalfDeg(x));
                        hi = Mathf.Max(hi, rel + HalfDeg(x));
                    }

                    s.Plinth0 = Mathf.Min(s.Theta0, s.Mid + lo - pad);
                    s.Plinth1 = Mathf.Max(s.Theta1, s.Mid + hi + pad);
                }
            }
        }

        /// <summary>
        /// How far (degrees) run b must move counter-clockwise from run a (angles unwrapped from start) so that every pair
        /// of their towers stands at least half a side + half a side + <see cref="LandStyle.TowerGap"/> apart in arc length
        /// at the smaller of the two radii; 0 or less when they already do.
        /// </summary>
        static float Deficit(List<TowerGeom> a, List<TowerGeom> b, float start)
        {
            float worst = float.MinValue;
            foreach (TowerGeom x in a)
            {
                foreach (TowerGeom y in b)
                {
                    float need = (float)((0.5 * (x.Side + y.Side) + LandStyle.TowerGap) / Math.Max(Math.Min(x.R, y.R), 1e-3) * 180 / Math.PI);
                    worst = Mathf.Max(worst, need - (LandMath.Wrap360(y.Theta - start) - LandMath.Wrap360(x.Theta - start)));
                }
            }

            return worst;
        }

        /// <summary>Half a tower's side as an angle at its radius (degrees).</summary>
        static float HalfDeg(TowerGeom x) => (float)(x.Side / 2 / Math.Max(x.R, 1e-3) * 180 / Math.PI);

        // ------------------------------------------------------------------ the log line and the checks

        static readonly string[] TierNames = { "gov", "raw", "make", "services", "tech" };

        /// <summary>
        /// The body of the "[Why] Land" line (8.8): GDP and the area unit, ring fills, offsets, the owners' share, the
        /// towers and the plinths, the named companies' profit against their sectors' owners' share (the three largest
        /// ratios), the layout cost, the checks (Σ areas = 24 u², fills ≤ 1) and the checksum.
        /// </summary>
        static string Describe(EconomyData data, LandGeometry g)
        {
            StringBuilder s = new StringBuilder();
            s.Append("GDP ").Append(LandFacts.Money(g.Gdp)).Append(" = 24 u2 (1 u2 = ")
                .Append(LandFacts.Money(g.Gdp / LandStyle.AreaGdp)).Append("); fill");
            for (int t = 0; t < 5; t++) s.Append(' ').Append(TierNames[t]).Append(' ').Append(LandFacts.Percent(g.RingFill[t]));
            s.Append("; offsets");
            for (int t = 0; t < 5; t++)
            {
                s.Append(' ').Append(TierNames[t]).Append(' ').Append(Math.Round(g.RingOffset[t]).ToString(LandFacts.Ci));
            }

            double owners = 0, area = 0;
            foreach (SectorGeom x in g.Sectors)
            {
                owners += x.Owners;
                double span = x.Theta1 - x.Theta0;
                area += span / LandStyle.DegreesPerArea((int)x.Tier);
            }

            s.Append("; owners keep ").Append(LandFacts.Percent(owners / Math.Max(g.Gdp, 1e-9))).Append(" of GDP; ");
            if (g.Towers.Length == 0) s.Append("no towers (company data from ").Append(LandStyle.TowersFromYear).Append(')');
            else
            {
                s.Append(g.Towers.Length).Append(" towers (");
                List<string> plinths = new List<string>();
                foreach (SectorGeom x in g.Sectors)
                {
                    if (float.IsNaN(x.Plinth0)) continue;
                    int k = 0;
                    foreach (TowerGeom t in g.Towers) k += t.Industry == x.Industry ? 1 : 0;
                    plinths.Add(data.Industries[x.Industry].Id + " " + k);
                }

                s.Append(plinths.Count == 0 ? "none" : string.Join(", ", plinths)).Append(" on a plinth)");
                TowerGeom tall = g.Towers[0];
                foreach (TowerGeom t in g.Towers) tall = t.Height > tall.Height ? t : tall;
                s.Append(", tallest ").Append(tall.Name).Append(' ').Append(LandFacts.Money(tall.MarketCap)).Append(' ')
                    .Append(LandFacts.Num(tall.Height, 2)).Append(" u; named profit / owners' share");
                List<(double ratio, int industry)> ratios = new List<(double, int)>();
                foreach (SectorGeom x in g.Sectors)
                {
                    double named = 0;
                    foreach (TowerGeom t in g.Towers) named += t.Industry == x.Industry ? Math.Max(0, t.NetIncome) : 0;
                    if (named > 0 && x.Owners > 0) ratios.Add((named / x.Owners, x.Industry));
                }

                ratios.Sort((p, q) => p.ratio != q.ratio ? q.ratio.CompareTo(p.ratio) : p.industry.CompareTo(q.industry));
                for (int k = 0; k < Math.Min(3, ratios.Count); k++)
                {
                    s.Append(' ').Append(data.Industries[ratios[k].industry].Id).Append(' ').Append(LandFacts.Times(ratios[k].ratio));
                }
            }

            bool areaOk = Math.Abs(area - LandStyle.AreaGdp) <= 0.01;
            bool fillOk = true;
            foreach (float f in g.RingFill) fillOk &= f <= 1f;
            int pass = (areaOk ? 1 : 0) + (fillOk ? 1 : 0);
            s.Append("; cost ").Append(g.LayoutCost.ToString("0", LandFacts.Ci)).Append("; checks ").Append(pass).Append("/2 ")
                .Append(pass == 2 ? "PASS" : "FAIL");
            if (!areaOk) s.Append(" (sector areas ").Append(LandFacts.Num(area, 3)).Append(" u2)");
            if (!fillOk) s.Append(" (a ring overfills)");
            s.Append("; checksum ").Append(g.Checksum.ToString("F6", LandFacts.Ci));
            return s.ToString();
        }

        /// <summary>
        /// The body of the "[Why] Roots" line (8.8) for a year's layout: the input-output table's analysis lives in
        /// <see cref="RootsModel"/>, whose drawn roots read the same selection (<see cref="RootsModel.Line"/>).
        /// </summary>
        public static string RootsLine(EconomyData data, LandGeometry g) => RootsModel.Line(data, g);
    }
}
