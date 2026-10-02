using System;
using System.Collections.Generic;
using UnityEngine;
using Why.Economy.Data;
using Why.Economy.Model;

namespace Why.Economy.Land
{
    /// <summary>
    /// Where each player stands (SPEC 3.3): the capital-only 1% on the crown ring (270° + 360°·k/n, by key); from 2024
    /// the working 1% whose industry (or tier) has named companies on top of its tallest tower (several stacked
    /// <see cref="LandStyle.ControllerStep"/> apart); everyone else on the rim, in the row of their class rung (rungs
    /// 4-5, 3, 2, 0-1 at <see cref="LandStyle.RimRows"/>) at the center angle of the sector that pays them (pseudo-anchors:
    /// their angle industry; tier and rest cells: the dollar-weighted circular mean of their members' industries), each
    /// row packed by the exact minimum-displacement packing of <see cref="LandMath.PackRow"/> (sorted by angle, group and
    /// key). Owners, gig workers and the 1% on the rim stand on gold plinths (<see cref="LandStyle.PlinthPad"/> wider).
    /// Also the dot slots of the glyph (<see cref="Figure"/>) by member. Reproduces the prototype synth/rim_spec.py. Pure:
    /// any thread, deterministic.
    /// </summary>
    public static class PlayerPlacement
    {
        /// <summary>What the placement did: places by kind, the largest shift along a row (u), and its failures.</summary>
        public struct Stats
        {
            public int Rim, Plinths, Tower, Crown;
            public double MaxShift;

            /// <summary>Players without a place; pairs of neighbouring rim discs closer than the packing's gap.</summary>
            public int Unplaced, Overlaps;
        }

        /// <summary>
        /// Places every player of a set on the year's land: <see cref="Player.Place"/>, <see cref="Player.Theta"/>,
        /// <see cref="Player.R"/>, <see cref="Player.Y"/>, <see cref="Player.Row"/>, <see cref="Player.Radius"/>,
        /// <see cref="Player.Plinth"/>, <see cref="Player.Tower"/> and <see cref="Player.AngleIndustry"/>.
        /// <paramref name="angleOf"/> gives, per person, the industry whose angle that adult would take alone (its
        /// industry, its pseudo-anchor's, finance for capital), and <paramref name="rec"/> their records of the year (the
        /// circular mean weighs members by wages + business income).
        /// </summary>
        public static Stats PlaceAll(PlayerSet set, EconomyData data, LandGeometry land, PersonYear[] rec, int[] angleOf)
        {
            Stats st = default;
            GroupsFile gf = data.GroupsFile ?? new GroupsFile();
            int ni = land.Sectors.Length;
            List<Player> crown = new List<Player>();
            List<Player>[] rows = { new List<Player>(), new List<Player>(), new List<Player>(), new List<Player>() };
            Dictionary<int, int> stacked = new Dictionary<int, int>();
            foreach (Player p in set.Players)
            {
                p.Radius = Figure.DiscRadius(p.People);
                p.Plinth = false;
                p.Tower = -1;
                p.Row = -1;
                if (p.Group == Group.Top1 && (p.AnchorId == "capital" || p.AnchorId == "tier:capital"))
                {
                    p.Place = Place.Crown;
                    crown.Add(p);
                    continue;
                }

                int tower = p.Group == Group.Top1 ? TallestTower(land, p) : -1;
                if (tower >= 0)
                {
                    TowerGeom t = land.Towers[tower];
                    stacked.TryGetValue(tower, out int k);
                    stacked[tower] = k + 1;
                    p.Place = Place.Tower;
                    p.Tower = t.Company;
                    p.Theta = t.Theta;
                    p.R = t.R;
                    p.Y = t.TopY + k * LandStyle.ControllerStep;
                    p.AngleIndustry = t.Industry;
                    st.Tower++;
                    continue;
                }

                p.Place = Place.Rim;
                p.Plinth = p.Group == Group.Top1 || p.Group == Group.Owners || p.Group == Group.Gig;
                p.Row = RowOf(p.Rung);
                p.R = LandStyle.RimRows[p.Row];
                p.Y = LandStyle.RimY;
                p.Theta = DesiredAngle(p, gf, data, land, rec, angleOf, ni, out int angleIndustry);
                p.AngleIndustry = angleIndustry;
                rows[p.Row].Add(p);
                st.Rim++;
                st.Plinths += p.Plinth ? 1 : 0;
            }

            // the crown: by key, the first facing the road
            crown.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            for (int k = 0; k < crown.Count; k++)
            {
                Player p = crown[k];
                p.Theta = LandMath.Wrap360(LandStyle.CrownSeatDeg + 360f * k / crown.Count);
                p.R = LandStyle.CrownR;
                p.Y = LandStyle.CrownY;
                p.AngleIndustry = -1;
                st.Crown++;
            }

            // the rim rows
            for (int r = 0; r < rows.Length; r++) Pack(rows[r], LandStyle.RimRows[r], ref st);
            foreach (Player p in set.Players)
            {
                bool ok = p.Place == Place.Crown || p.Place == Place.Tower && p.Tower >= 0 || p.Place == Place.Rim && p.Row >= 0;
                if (!ok || float.IsNaN(p.Theta)) st.Unplaced++;
            }

            return st;
        }

        /// <summary>The rim row of a class rung: rungs 4-5 → 0 (nearest the bowl), 3 → 1, 2 → 2, 0-1 → 3.</summary>
        public static int RowOf(int rung) => rung >= 4 ? 0 : rung == 3 ? 1 : rung == 2 ? 2 : 3;

        /// <summary>
        /// The tallest tower of a working 1% player's industry, or of its tier for a tier cell (from 2024, when the land
        /// has towers); -1 when there is none (the player then stands on a plinth on the rim).
        /// </summary>
        static int TallestTower(LandGeometry land, Player p)
        {
            if (land.Towers == null || land.Towers.Length == 0) return -1;
            int tier = -1;
            if (p.Anchor < 0)
            {
                if (p.AnchorId == null || !p.AnchorId.StartsWith("tier:", StringComparison.Ordinal)) return -1;
                tier = TierOf(p.AnchorId.Substring(5));
                if (tier < 0) return -1;
            }

            int best = -1;
            for (int k = 0; k < land.Towers.Length; k++)
            {
                TowerGeom t = land.Towers[k];
                if (t == null || t.Industry < 0 || t.Industry >= land.Sectors.Length) continue;
                bool here = p.Anchor >= 0 ? t.Industry == p.Anchor : (int)land.Sectors[t.Industry].Tier == tier;
                if (here && (best < 0 || t.Height > land.Towers[best].Height)) best = k;
            }

            return best;
        }

        static int TierOf(string id)
        {
            for (int t = 0; t < 5; t++)
            {
                if (PlayerCensus.TierId((Tier)t) == id) return t;
            }

            return -1;
        }

        /// <summary>
        /// A rim player's desired angle (degrees): its industry's sector center; a pseudo-anchor's angle industry; for
        /// tier, rest and merged cells the circular mean of the members' industries' centers weighted by their wages and
        /// business income (+1e-6, so a cell without earnings takes the plain mean). <paramref name="angleIndustry"/> gets
        /// the industry the angle comes from (for a mean: the members' industry with the largest weight).
        /// </summary>
        static float DesiredAngle(Player p, GroupsFile gf, EconomyData data, LandGeometry land, PersonYear[] rec, int[] angleOf,
            int ni, out int angleIndustry)
        {
            if (p.Anchor >= 0 && p.Anchor < ni)
            {
                angleIndustry = p.Anchor;
                return LandMath.Wrap360(land.Sectors[p.Anchor].Mid);
            }

            string pseudo = p.AnchorId ?? "";
            int slash = pseudo.IndexOf('/');
            if (slash > 0)
            {
                string id = pseudo.Substring(0, slash);
                PseudoAnchor pa = null;
                for (int k = 0; k < gf.PseudoAnchors.Count && pa == null; k++) pa = gf.PseudoAnchors[k].Id == id ? gf.PseudoAnchors[k] : null;
                Industry ind = pa != null ? data.IndustryById(pa.AngleIndustry) : null;
                if (ind != null && ind.Index < ni)
                {
                    angleIndustry = ind.Index;
                    return LandMath.Wrap360(land.Sectors[ind.Index].Mid);
                }
            }

            double x = 0, z = 0;
            double[] byIndustry = new double[ni];
            foreach (int i in p.Adults)
            {
                int k = angleOf[i] >= 0 && angleOf[i] < ni ? angleOf[i] : 0;
                double w = Math.Max(0, (double)rec[i].Wages + rec[i].Business) + 1e-6;
                double a = land.Sectors[k].Mid * Math.PI / 180;
                x += w * Math.Cos(a);
                z += w * Math.Sin(a);
                byIndustry[k] += w;
            }

            angleIndustry = 0;
            for (int k = 1; k < ni; k++) angleIndustry = byIndustry[k] > byIndustry[angleIndustry] ? k : angleIndustry;
            return x * x + z * z < 1e-24
                ? LandMath.Wrap360(land.Sectors[angleIndustry].Mid)
                : (float)LandMath.Wrap360(Math.Atan2(z, x) * 180 / Math.PI);
        }

        /// <summary>
        /// Packs one rim row (3.3): extents are the disc radius (+ the plinth's pad), neighbours at least
        /// <see cref="LandStyle.PackGap"/> apart; ties in angle by group, then key. Counts discs that still overlap.
        /// </summary>
        static void Pack(List<Player> row, float radius, ref Stats st)
        {
            int n = row.Count;
            if (n == 0) return;
            double[] want = new double[n], ext = new double[n];
            int[] rank = new int[n];
            int[] order = new int[n];
            for (int k = 0; k < n; k++)
            {
                want[k] = row[k].Theta;
                ext[k] = Extent(row[k]);
                order[k] = k;
            }

            Array.Sort(order, (a, b) =>
            {
                int c = row[a].Group.CompareTo(row[b].Group);
                if (c != 0) return c;
                c = string.CompareOrdinal(row[a].Key, row[b].Key);
                return c != 0 ? c : row[a].Index.CompareTo(row[b].Index);
            });
            for (int k = 0; k < n; k++) rank[order[k]] = k;
            double[] packed = LandMath.PackRow(want, ext, radius, LandStyle.PackGap, rank, out double[] shift);
            for (int k = 0; k < n; k++)
            {
                row[k].Theta = (float)packed[k];
                st.MaxShift = Math.Max(st.MaxShift, shift[k]);
            }

            if (n < 2) return;
            for (int k = 0; k < n; k++) order[k] = k;
            Array.Sort(order, (a, b) =>
            {
                int c = packed[a].CompareTo(packed[b]);
                return c != 0 ? c : a.CompareTo(b);
            });
            for (int k = 0; k < n; k++)
            {
                int a = order[k], b = order[(k + 1) % n];
                double gap = LandMath.Wrap360(packed[b] - packed[a]) * Math.PI / 180 * radius;
                if (gap < LandStyle.PackGap + ext[a] + ext[b] - 1e-3) st.Overlaps++;
            }
        }

        /// <summary>A rim player's half-width along its row: the disc's radius, plus the plinth's pad.</summary>
        public static float Extent(Player p) => p.Radius + (p.Plinth ? LandStyle.PlinthPad : 0);

        // ------------------------------------------------------------------ the glyph's slots (Figure, by member)

        /// <summary>
        /// Land-local position of a member's dot (3.4): an adult's sunflower slot at the height of their reason, or a
        /// child's slot on the ring outside the disc; false when the person is not in the set's year.
        /// </summary>
        public static bool SlotOf(PlayerSet set, int person, float reason, out Vector3 local)
        {
            local = default;
            if (set?.PlayerOfPerson == null || person < 0 || person >= set.PlayerOfPerson.Length) return false;
            int pi = set.PlayerOfPerson[person];
            if (pi < 0 || pi >= set.Players.Length) return false;
            Player p = set.Players[pi];
            int k = Array.BinarySearch(p.Adults, person);
            if (k >= 0)
            {
                local = Figure.DotSlot(p, k, p.Adults.Length, reason);
                return true;
            }

            k = Array.BinarySearch(p.Children, person);
            if (k < 0) return false;
            local = Figure.ChildSlot(p, k, p.Children.Length);
            return true;
        }
    }
}
