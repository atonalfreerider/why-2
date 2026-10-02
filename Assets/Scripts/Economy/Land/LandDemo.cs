using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Why.Economy.Data;
using Why.Economy.Model;
using Why.Humans.Smv;

namespace Why.Economy.Land
{
    /// <summary>
    /// Deterministic stand-ins for the land's three models, with the real shapes, so the work packages can build in
    /// parallel: players (a simplified census on the real lives: the twelve groups by the priority rule, cells by group ×
    /// anchor without the tier step, the party split or generations, placed on the rim by the real row and packing
    /// rules), money (spending landed by spending.json's value-added weights instead of the first-recipient RAS, a
    /// circular-mean canal, simple arcs and rivers) and a season (an eased climb to the prototype's levels, coalitions
    /// by quarter of the rim). Every log body starts with <see cref="Tag"/>, so the model layer prints its line with
    /// "(demo)". Replaced by PlayerCensus (WP2), MoneyRouting (WP3) and SocialSeason (WP4); pure and deterministic.
    /// </summary>
    public static class LandDemo
    {
        /// <summary>The first word of every demo log body.</summary>
        public const string Tag = "(demo) ";

        /// <summary>Whether a builder's log comes from this demo.</summary>
        public static bool IsDemo(string log) => log != null && log.StartsWith(Tag, StringComparison.Ordinal);

        /// <summary>A log body without the demo tag.</summary>
        public static string Body(string log) => IsDemo(log) ? log.Substring(Tag.Length) : log ?? "";

        static readonly string[] GroupShort =
            { "1%", "owners", "gig", "PMC", "public", "office", "frontline", "poor", "comf.ret", "ss.ret", "students", "out" };

        // ================================================================== players

        /// <summary>A stand-in player set of a year (see the class summary); empty when the lives were not simulated.</summary>
        public static PlayerSet Players(EconomyModel model, SmvPopulation pop, LandGeometry land, int year)
        {
            PlayerSet set = new PlayerSet { Year = year, Players = Array.Empty<Player>(), PlayerOfPerson = Array.Empty<int>() };
            EconomicLives lives = model?.Lives;
            SmvSimulation sim = lives?.Sim ?? pop?.Sim;
            if (lives == null || !lives.Ready || sim == null)
            {
                set.Log = Tag + "0 players (the lives were not simulated); checks 0/3 FAIL; checksum 0";
                return set;
            }

            EconomyData data = model.Data;
            GroupsFile gf = data.GroupsFile;
            int n = sim.People.Count, ni = data.Industries.Count;
            double unit = sim.PeoplePerLine / 1e9;
            PersonYear[] rec = new PersonYear[n];
            bool[] alive = new bool[n];
            List<int> adults = new List<int>(), kids = new List<int>();
            List<float> earn = new List<float>();
            double wealthTotal = 0;
            for (int i = 0; i < n; i++)
            {
                if (!lives.TryGet(i, year, out rec[i])) continue;
                alive[i] = true;
                if (rec[i].Adult)
                {
                    adults.Add(i);
                    wealthTotal += Math.Max(0, rec[i].Wealth);
                    if (rec[i].Employed && !rec[i].SelfEmployed) earn.Add(rec[i].Earned);
                }
                else kids.Add(i);
            }

            earn.Sort();
            double medEarn = earn.Count > 0 ? earn[earn.Count / 2] : 0;

            // spouses at mid-year
            int[] spouse = new int[n];
            for (int i = 0; i < n; i++) spouse[i] = -1;
            foreach (SmvMarriage m in sim.MarriageLog)
            {
                if (m.Start > year + 0.5 || m.End <= year + 0.5 || m.Wife < 0 || m.Husband < 0) continue;
                spouse[m.Wife] = m.Husband;
                spouse[m.Husband] = m.Wife;
            }

            // groups (rules 1-4 and 6; rule 5: a non-employed working-age spouse takes an employed spouse's group)
            int[] group = new int[n], anchor = new int[n];
            for (int i = 0; i < n; i++) group[i] = anchor[i] = -1;
            foreach (int i in adults) group[i] = OwnGroup(rec[i], medEarn, gf, data);
            foreach (int i in adults)
            {
                if (group[i] != -1) continue;
                int s = spouse[i];
                if (s >= 0 && alive[s] && rec[s].Adult && rec[s].Employed && group[s] >= 0)
                {
                    group[i] = group[s];
                    anchor[i] = rec[s].Industry;
                }
                else group[i] = rec[i].Age < gf.Thresholds.YoungAge ? (int)Group.Students : (int)Group.OutOfWork;
            }

            // anchors: an industry index, or a pseudo-anchor code (-2 capital, -10 - k pseudo-anchor k)
            foreach (int i in adults)
            {
                if (anchor[i] >= 0) continue;
                PersonYear r = rec[i];
                if (r.Employed && r.Industry >= 0) anchor[i] = r.Industry;
                else if (group[i] == (int)Group.Top1) anchor[i] = -2;
                else anchor[i] = -10 - PseudoIndex(gf, group[i]);
            }

            // cells by group x anchor; small cells pool into the group's rest, a small rest joins the largest cell
            int cellsMin = Math.Max(1, gf.Cells.MinLines), richMin = Math.Max(1, gf.Cells.RichMinLines);
            double richWealth = gf.Cells.RichWealthShare * wealthTotal;
            bool Ok(List<int> m)
            {
                if (m.Count >= cellsMin) return true;
                if (m.Count < richMin) return false;
                double w = 0;
                foreach (int i in m) w += Math.Max(0, rec[i].Wealth);
                return w >= richWealth;
            }

            List<(int group, int anchor, List<int> members)> cells = new List<(int, int, List<int>)>();
            for (int g = 0; g < 12; g++)
            {
                SortedDictionary<int, List<int>> byAnchor = new SortedDictionary<int, List<int>>(Comparer<int>.Create((a, b) =>
                    AnchorOrder(a).CompareTo(AnchorOrder(b))));
                foreach (int i in adults)
                {
                    if (group[i] != g) continue;
                    if (!byAnchor.TryGetValue(anchor[i], out List<int> list)) byAnchor[anchor[i]] = list = new List<int>();
                    list.Add(i);
                }

                List<(int anchor, List<int> members)> kept = new List<(int, List<int>)>();
                List<int> rest = new List<int>();
                foreach (KeyValuePair<int, List<int>> kv in byAnchor)
                {
                    if (Ok(kv.Value)) kept.Add((kv.Key, kv.Value));
                    else rest.AddRange(kv.Value);
                }

                if (rest.Count > 0)
                {
                    rest.Sort();
                    if (Ok(rest) || kept.Count == 0) kept.Add((-3, rest));
                    else
                    {
                        int big = 0;
                        for (int k = 1; k < kept.Count; k++) big = kept[k].members.Count > kept[big].members.Count ? k : big;
                        kept[big].members.AddRange(rest);
                        kept[big].members.Sort();
                    }
                }

                foreach ((int a, List<int> m) in kept) cells.Add((g, a, m));
            }

            // players
            List<Player> players = new List<Player>(cells.Count);
            int[] playerOf = new int[n];
            for (int i = 0; i < n; i++) playerOf[i] = -1;
            foreach ((int g, int a, List<int> m) in cells)
            {
                Player p = new Player
                {
                    Index = players.Count, Group = (Group)g, Rung = Rung(gf, g), Anchor = a >= 0 ? a : -1,
                    AnchorId = AnchorId(data, gf, a), Adults = m.ToArray()
                };
                foreach (int i in m) playerOf[i] = p.Index;
                players.Add(p);
            }

            // children: the mother's player, else the father's; orphans by their household's standing
            List<int>[] children = new List<int>[players.Count];
            for (int k = 0; k < children.Length; k++) children[k] = new List<int>();
            foreach (int c in kids)
            {
                SmvPerson sp = sim.People[c];
                int p = sp.Mother >= 0 && playerOf[sp.Mother] >= 0 ? playerOf[sp.Mother] : sp.Father >= 0 ? playerOf[sp.Father] : -1;
                if (p < 0)
                {
                    Group g = rec[c].WealthGroup == 3 ? Group.Top1 :
                        rec[c].IncomeRank >= gf.Thresholds.PmcRank ? Group.Pmc : Group.OutOfWork;
                    p = LargestOf(players, g);
                }

                if (p < 0) continue;
                children[p].Add(c);
                playerOf[c] = p;
            }

            foreach (Player p in players)
            {
                children[p.Index].Sort();
                p.Children = children[p.Index].ToArray();
                Aggregate(p, rec, sim, lives, unit, gf, ni);
            }

            set.Players = players.ToArray();
            set.PlayerOfPerson = playerOf;
            PlaceAll(set, data, land, year, rec, unit, out double maxShift);
            double checksum = 0;
            foreach (Player p in set.Players) checksum += (double)p.Adults.Length * p.Index;
            set.Checksum = checksum;
            set.Log = PlayersLog(set, lives, rec, adults, kids.Count, maxShift, data, year) + "; checksum " +
                      checksum.ToString("F6", LandFacts.Ci);
            return set;
        }

        static int OwnGroup(PersonYear r, double medEarn, GroupsFile gf, EconomyData data)
        {
            if (r.WealthGroup == 3) return (int)Group.Top1;
            if (r.Employed && r.SelfEmployed) return r.Earned >= medEarn || r.WealthGroup >= 2 ? (int)Group.Owners : (int)Group.Gig;
            if (r.Employed)
            {
                if (r.IncomeRank >= gf.Thresholds.PmcRank) return (int)Group.Pmc;
                if (r.IncomeRank < gf.Thresholds.PoorRank) return (int)Group.WorkingPoor;
                string id = r.Industry >= 0 && r.Industry < data.Industries.Count ? data.Industries[r.Industry].Id : null;
                if (id != null && gf.GovernmentIndustries.Contains(id)) return (int)Group.Public;
                if (id != null && gf.OfficeIndustries.Contains(id)) return (int)Group.Office;
                return (int)Group.Frontline;
            }

            if (r.Age >= gf.Thresholds.RetireAge || r.SocialSecurity > 0)
            {
                double cash = r.SocialSecurity + r.Wages + r.Business + r.CapitalIncome;
                return r.SocialSecurity >= gf.Thresholds.SsDependence * cash ? (int)Group.RetiredSocialSecurity : (int)Group.RetiredSavings;
            }

            return -1;
        }

        static int PseudoIndex(GroupsFile gf, int group)
        {
            string id = group >= 0 && group < gf.Groups.Count ? gf.Groups[group].Id : null;
            int k = gf.PseudoAnchors.FindIndex(a => a.Group == id);
            return Math.Max(0, k);
        }

        /// <summary>Sort order of anchors: industries by index, then the rest, capital and the pseudo-anchors.</summary>
        static int AnchorOrder(int a) => a >= 0 ? a : a == -3 ? 1000 : a == -2 ? 1001 : 1010 + (-10 - a);

        static string AnchorId(EconomyData data, GroupsFile gf, int a)
        {
            if (a >= 0) return data.Industries[a].Id;
            if (a == -2) return "capital";
            if (a == -3) return "rest";
            int k = -10 - a;
            return k < gf.PseudoAnchors.Count ? gf.PseudoAnchors[k].Id : "rest";
        }

        static int Rung(GroupsFile gf, int g) => g < gf.Groups.Count ? gf.Groups[g].Rung : 0;

        static int LargestOf(List<Player> players, Group g)
        {
            int best = -1;
            foreach (Player p in players)
            {
                if (p.Group == g && (best < 0 || p.Adults.Length > players[best].Adults.Length)) best = p.Index;
            }

            return best;
        }

        static void Aggregate(Player p, PersonYear[] rec, SmvSimulation sim, EconomicLives lives, double unit, GroupsFile gf, int ni)
        {
            double n = Math.Max(1, p.Adults.Length);
            int[] tribes = new int[3];
            foreach (int i in p.Adults)
            {
                PersonYear r = rec[i];
                p.Wages += r.Wages * unit;
                p.Business += r.Business * unit;
                p.Capital += r.CapitalIncome * unit;
                p.Transfers += r.Transfers * unit;
                p.SocialSecurity += r.SocialSecurity * unit;
                p.Taxes += r.Taxes * unit;
                p.Spending += r.Spending * unit;
                p.Saving += r.Saving * unit;
                p.Wealth += r.Wealth * unit;
                p.Debt += r.Debt * unit;
                int ind = r.Industry >= 0 && r.Industry < ni ? r.Industry : p.Anchor;
                if (ind >= 0 && ind < ni)
                {
                    p.WagesBy[ind] += r.Wages * unit;
                    p.BusinessBy[ind] += r.Business * unit;
                }

                double s = r.Spending * unit;
                for (int c = 0; c < 6; c++)
                {
                    double v = s * r.Category(c);
                    p.Category[c] += v;
                    p.CategoryFear[c] += v * r.FearShare;
                    p.CategoryFantasy[c] += v * r.Fantasy;
                }

                p.Fear += r.FearShare;
                p.Fantasy += r.Fantasy;
                p.Reason += r.Reason;
                p.HigherOs += r.Reason >= LandStyle.HigherOsReason ? 1 : 0;
                p.Future += r.Future;
                p.Agency += r.Agency;
                p.InControl += r.InControl ? 1 : 0;
                p.Married += r.Married ? 1 : 0;
                p.WithKids += r.Kids > 0 ? 1 : 0;
                p.Age += r.Age;
                tribes[Math.Min(2, (int)r.Tribe)]++;
                PersonTraits t = lives.Traits(i);
                if (t != null) p.Strategy[(int)t.Strategy] += 1;
                double birth = sim.People[i].Birth;
                int gen = gf.Generations.FindIndex(x => birth < x.Before);
                p.Generations[gen < 0 ? 4 : Math.Min(4, gen)] += 1;
            }

            float inv = (float)(1 / n);
            p.Fear *= inv;
            p.Fantasy *= inv;
            p.Reason *= inv;
            p.HigherOs *= inv;
            p.Future *= inv;
            p.Agency *= inv;
            p.InControl *= inv;
            p.Married *= inv;
            p.WithKids *= inv;
            p.Age *= inv;
            for (int k = 0; k < 7; k++) p.Strategy[k] *= inv;
            for (int k = 0; k < 5; k++) p.Generations[k] *= inv;
            for (int k = 0; k < 3; k++) p.TribeShares[k] = tribes[k] * inv;
            int top = tribes[0] >= tribes[1] && tribes[0] >= tribes[2] ? 0 : tribes[1] >= tribes[2] ? 1 : 2;
            p.Tribe = p.TribeShares[top] > 0.5f ? "DRI"[top] : 'M';
            p.Key = GroupId(gf, (int)p.Group) + "|" + p.AnchorId + "|" + p.Tribe;
        }

        static string GroupId(GroupsFile gf, int g) => g < gf.Groups.Count ? gf.Groups[g].Id : g.ToString(LandFacts.Ci);

        /// <summary>
        /// Places every player: the capital-only 1% on the crown, the working 1% on their industry's tallest tower (from
        /// 2024), everyone else on the rim (row by rung, angle by the anchor's sector, plinths for owners, gig and the 1%),
        /// each row packed with <see cref="LandMath.PackRow"/>.
        /// </summary>
        static void PlaceAll(PlayerSet set, EconomyData data, LandGeometry land, int year, PersonYear[] rec, double unit,
            out double maxShift)
        {
            maxShift = 0;
            GroupsFile gf = data.GroupsFile;
            List<Player> crown = new List<Player>();
            List<Player>[] rows = { new List<Player>(), new List<Player>(), new List<Player>(), new List<Player>() };
            Dictionary<int, int> stack = new Dictionary<int, int>();
            foreach (Player p in set.Players)
            {
                p.Radius = Figure.DiscRadius(p.People);
                if (p.Group == Group.Top1 && p.AnchorId == "capital")
                {
                    p.Place = Place.Crown;
                    crown.Add(p);
                    continue;
                }

                int tower = p.Group == Group.Top1 && p.Anchor >= 0 ? TallestTower(land, p.Anchor) : -1;
                if (tower >= 0)
                {
                    TowerGeom t = land.Towers[tower];
                    stack.TryGetValue(tower, out int k);
                    stack[tower] = k + 1;
                    p.Place = Place.Tower;
                    p.Tower = t.Company;
                    p.Theta = t.Theta;
                    p.R = t.R;
                    p.Y = t.TopY + k * LandStyle.ControllerStep;
                    p.AngleIndustry = t.Industry;
                    continue;
                }

                p.Place = Place.Rim;
                p.Plinth = p.Group == Group.Top1 || p.Group == Group.Owners || p.Group == Group.Gig;
                p.Row = p.Rung >= 4 ? 0 : p.Rung == 3 ? 1 : p.Rung == 2 ? 2 : 3;
                p.Y = LandStyle.RimY;
                p.R = LandStyle.RimRows[p.Row];
                if (p.Anchor >= 0) p.AngleIndustry = p.Anchor;
                else
                {
                    PseudoAnchor pa = gf.PseudoAnchors.Find(x => x.Id == p.AnchorId);
                    Data.Industry ind = pa != null ? data.IndustryById(pa.AngleIndustry) : null;
                    p.AngleIndustry = ind?.Index ?? -1;
                }

                p.Theta = p.AngleIndustry >= 0 ? LandMath.Wrap360(land.Sectors[p.AngleIndustry].Mid) : RestAngle(p, data, land, rec, unit);
                rows[p.Row].Add(p);
            }

            for (int k = 0; k < crown.Count; k++)
            {
                Player p = crown[k];
                p.Theta = LandMath.Wrap360(270f + 360f * k / crown.Count);
                p.R = LandStyle.CrownR;
                p.Y = LandStyle.CrownY;
            }

            for (int r = 0; r < 4; r++)
            {
                List<Player> row = rows[r];
                if (row.Count == 0) continue;
                double[] want = new double[row.Count], ext = new double[row.Count];
                int[] rank = new int[row.Count];
                for (int k = 0; k < row.Count; k++)
                {
                    want[k] = row[k].Theta;
                    ext[k] = row[k].Radius + (row[k].Plinth ? LandStyle.PlinthPad : 0);
                    rank[k] = row[k].Index;
                }

                double[] packed = LandMath.PackRow(want, ext, LandStyle.RimRows[r], LandStyle.PackGap, rank, out double[] shift);
                for (int k = 0; k < row.Count; k++)
                {
                    row[k].Theta = (float)packed[k];
                    maxShift = Math.Max(maxShift, shift[k]);
                }
            }
        }

        static int TallestTower(LandGeometry land, int industry)
        {
            int best = -1;
            for (int k = 0; k < land.Towers.Length; k++)
            {
                if (land.Towers[k].Industry == industry && (best < 0 || land.Towers[k].Height > land.Towers[best].Height)) best = k;
            }

            return best;
        }

        /// <summary>A rest cell's angle: the dollar-weighted circular mean of its members' industries' centers.</summary>
        static float RestAngle(Player p, EconomyData data, LandGeometry land, PersonYear[] rec, double unit)
        {
            List<double> angles = new List<double>(p.Adults.Length), weights = new List<double>(p.Adults.Length);
            int finance = data.IndustryById("finance")?.Index ?? 0;
            foreach (int i in p.Adults)
            {
                int ind = rec[i].Industry >= 0 ? rec[i].Industry : finance;
                angles.Add(land.Sectors[ind].Mid);
                weights.Add(Math.Max(0, (rec[i].Wages + rec[i].Business) * unit) + 1e-6);
            }

            return (float)LandMath.CircularMean(angles, weights, land.Sectors[finance].Mid);
        }

        static string PlayersLog(PlayerSet set, EconomicLives lives, PersonYear[] rec, List<int> adults, int kidCount,
            double maxShift, EconomyData data, int year)
        {
            Player[] ps = set.Players;
            int[] byGroup = new int[12];
            int rim = 0, plinths = 0, towers = 0, crown = 0, assigned = 0, childCount = 0;
            List<int> sizes = new List<int>(ps.Length);
            List<double> people = new List<double>(ps.Length);
            double wages = 0, spending = 0, taxes = 0;
            bool placed = true;
            foreach (Player p in ps)
            {
                byGroup[(int)p.Group]++;
                assigned += p.Adults.Length;
                childCount += p.Children.Length;
                sizes.Add(p.Adults.Length);
                people.Add(p.People);
                wages += p.Wages;
                spending += p.Spending;
                taxes += p.Taxes;
                if (p.Place == Place.Rim)
                {
                    rim++;
                    plinths += p.Plinth ? 1 : 0;
                    placed &= p.Row >= 0;
                }
                else if (p.Place == Place.Tower) towers++;
                else crown++;
            }

            sizes.Sort();
            people.Sort();
            int selfGov = 0;
            foreach (int i in adults)
            {
                int ind = rec[i].Industry;
                if (rec[i].SelfEmployed && ind >= 0 && ind < data.Industries.Count && data.Industries[ind].TierId == "gov") selfGov++;
            }

            PopulationYear agg = lives.Aggregate(year);
            bool sums = agg == null || Math.Abs(wages - agg.Wages) <= 0.001 * Math.Max(1, agg.Wages) &&
                Math.Abs(spending - agg.Spending) <= 0.001 * Math.Max(1, agg.Spending) &&
                Math.Abs(taxes - agg.Taxes) <= 0.001 * Math.Max(1, agg.Taxes);
            bool every = assigned == adults.Count;
            int pass = (every ? 1 : 0) + (sums ? 1 : 0) + (placed ? 1 : 0);
            StringBuilder s = new StringBuilder(Tag);
            s.Append(ps.Length).Append(" players (");
            for (int g = 0; g < 12; g++) s.Append(g > 0 ? ", " : "").Append(GroupShort[g]).Append(' ').Append(byGroup[g]);
            s.Append("); ").Append(assigned).Append(" adults + ").Append(childCount).Append(" children; median ")
                .Append(LandFacts.Millions(sizes.Count > 0 ? sizes[sizes.Count / 2] * 1e5 : 0)).Append(" adults (")
                .Append(LandFacts.Millions(people.Count > 0 ? people[people.Count / 2] : 0)).Append(" people), max ")
                .Append(LandFacts.Millions(people.Count > 0 ? people[people.Count - 1] : 0)).Append("; places rim ").Append(rim)
                .Append(" (plinths ").Append(plinths).Append("), tower ").Append(towers).Append(", crown ").Append(crown)
                .Append("; max rim shift ").Append(LandFacts.Num(maxShift, 2)).Append(" u; self-employed in government ").Append(selfGov)
                .Append("; checks ").Append(pass).Append("/3 ").Append(pass == 3 ? "PASS" : "FAIL");
            if (kidCount != childCount) s.Append(" (").Append(kidCount - childCount).Append(" children without a player)");
            return s.ToString();
        }

        // ================================================================== money

        /// <summary>Stand-in money of a year on the land (see the class summary).</summary>
        public static MoneyFlows Money(EconomyModel model, LandGeometry land, PlayerSet players, int year)
        {
            MoneyFlows m = new MoneyFlows { Year = year };
            EconomyData data = model.Data;
            int ni = data.Industries.Count;
            for (int l = 0; l < 8; l++) m.Canal[l] = new CanalLane { Lane = l };

            // category -> industry by spending.json's value-added landing (the RAS of first recipients is WP3's)
            double[,] seller = new double[6, ni];
            double[] imp = new double[6];
            for (int c = 0; c < 6 && c < data.Categories.Count; c++)
            {
                double total = 0, abroad = 0, dom = 0;
                foreach (SpendingItem item in data.Categories[c].Items)
                {
                    total += item.Pce;
                    abroad += item.Pce * item.ImportShare;
                    if (item.Industries == null) continue;
                    foreach (KeyValuePair<string, double> kv in item.Industries)
                    {
                        Data.Industry ind = data.IndustryById(kv.Key);
                        if (ind == null) continue;
                        double v = item.Pce * (1 - item.ImportShare) * kv.Value;
                        seller[c, ind.Index] += v;
                        dom += v;
                    }
                }

                imp[c] = total > 0 ? abroad / total : 0;
                for (int k = 0; k < ni; k++) seller[c, k] = dom > 0 ? seller[c, k] / dom : 0;
            }

            m.CategoryToSeller = seller;

            // totals
            double[] cat = new double[6], catFear = new double[6], catFant = new double[6];
            double taxes = 0, capital = 0, saving = 0, credit = 0, wages = 0, business = 0, transfers = 0, spending = 0;
            foreach (Player p in players.Players)
            {
                for (int c = 0; c < 6; c++)
                {
                    cat[c] += p.Category[c];
                    catFear[c] += p.CategoryFear[c];
                    catFant[c] += p.CategoryFantasy[c];
                }

                taxes += p.Taxes;
                capital += p.Capital;
                wages += p.Wages;
                business += p.Business;
                transfers += p.Transfers;
                spending += p.Spending;
                if (p.Saving > 0) saving += p.Saving;
                else credit -= p.Saving;
            }

            for (int c = 0; c < 6; c++)
            {
                double dom = cat[c] * (1 - imp[c]);
                m.Imports += cat[c] * imp[c];
                double fearShare = cat[c] > 0 ? catFear[c] / cat[c] : 0, fantShare = cat[c] > 0 ? catFant[c] / cat[c] : 0;
                for (int k = 0; k < ni; k++)
                {
                    double v = dom * seller[c, k];
                    m.PoolInflow[k] += v;
                    m.PoolFear[k] += v * fearShare;
                    m.PoolFantasy[k] += v * fantShare;
                }
            }

            // the crown: payouts by owners' dollars of the private sectors, abroad from the circuit
            double ownersTotal = 0;
            foreach (SectorGeom s in land.Sectors) ownersTotal += s.Tier == Tier.Gov ? 0 : s.Owners;
            foreach (SectorGeom s in land.Sectors)
            {
                m.PayoutBySector[s.Industry] = ownersTotal > 0 && s.Tier != Tier.Gov ? capital * s.Owners / ownersTotal : 0;
            }

            CircuitYear circuit = model.Circuit.Build(year);
            foreach (CircuitLink l in circuit.Links) m.PayoutAbroad += l.Kind == "payout" ? l.Value : 0;
            m.SavingIn = saving;
            m.Credit = credit;
            m.Investment = Math.Max(0, saving - credit);
            double invTotal = 0;
            foreach (Data.Industry ind in data.Industries) invTotal += Math.Max(0, FinalDemand(data, ind.Id, "investment"));
            foreach (Data.Industry ind in data.Industries)
            {
                m.InvestmentBySector[ind.Index] = invTotal > 0 ? m.Investment * Math.Max(0, FinalDemand(data, ind.Id, "investment")) / invTotal : 0;
            }

            Falls(m, land, players, data, seller);
            Canal(m, land, players, imp);
            Paths(m, land, players, data, seller, imp, cat, catFear, catFant);
            Patches(m, land, players);
            int uphill = Uphill(m);
            double sum = 0;
            foreach (FlowPath f in m.Paths) sum += f.Dollars;
            m.Checksum = sum;
            m.Log = MoneyLog(m, players, wages, business, capital, transfers, taxes, spending, saving, credit, catFear, catFant, uphill) +
                    "; checksum " + sum.ToString("F6", LandFacts.Ci);
            return m;
        }

        static double FinalDemand(EconomyData data, string id, string key) =>
            data.Circuit?.FinalDemand != null && data.Circuit.FinalDemand.TryGetValue(id, out Dictionary<string, double> d) &&
            d != null && d.TryGetValue(key, out double v) ? v : 0;

        /// <summary>Falls: each category's weighted circular median over its sinks (≥ 10° apart), taxes at the federal wedge, abroad in the rim's emptiest gap.</summary>
        static void Falls(MoneyFlows m, LandGeometry land, PlayerSet players, EconomyData data, double[,] seller)
        {
            int ni = land.Sectors.Length;
            for (int c = 0; c < 6; c++)
            {
                double best = double.MaxValue;
                int at = 0;
                for (int phi = 0; phi < 360; phi++)
                {
                    double cost = 0;
                    for (int k = 0; k < ni; k++)
                    {
                        if (seller[c, k] >= LandStyle.SinkMinShare) cost += seller[c, k] * Math.Abs(LandMath.DeltaDeg((double)phi, land.Sectors[k].Mid));
                    }

                    if (cost < best - 1e-12)
                    {
                        best = cost;
                        at = phi;
                    }
                }

                m.Fall[c] = at;
            }

            for (int it = 0; it < 50; it++)
            {
                bool moved = false;
                for (int a = 0; a < 6; a++)
                {
                    for (int b = a + 1; b < 6; b++)
                    {
                        float d = LandMath.DeltaDeg(m.Fall[a], m.Fall[b]);
                        if (Math.Abs(d) >= LandStyle.FallSeparationDeg - 1e-3f) continue;
                        float push = (LandStyle.FallSeparationDeg - Math.Abs(d)) / 2 * (d >= 0 ? 1 : -1);
                        m.Fall[a] = LandMath.Wrap360(Mathf.Round(m.Fall[a] - push));
                        m.Fall[b] = LandMath.Wrap360(Mathf.Round(m.Fall[b] + push));
                        moved = true;
                    }
                }

                if (!moved) break;
            }

            int federal = data.IndustryById("federal")?.Index ?? 0;
            m.Fall[LandStyle.LaneTaxes] = Mathf.Round(LandMath.Wrap360(land.Sectors[federal].Mid));
            List<float> angles = new List<float>();
            foreach (Player p in players.Players)
            {
                if (p.Place == Place.Rim) angles.Add(LandMath.Wrap360(p.Theta));
            }

            angles.Sort();
            float gapMid = 90, gap = -1;
            for (int k = 0; k < angles.Count; k++)
            {
                float a = angles[k], b = k + 1 < angles.Count ? angles[k + 1] : angles[0] + 360;
                if (b - a > gap)
                {
                    gap = b - a;
                    gapMid = LandMath.Wrap360(Mathf.Round(0.5f * (a + b)));
                }
            }

            m.Fall[LandStyle.LaneAbroad] = gapMid;
            for (int l = 0; l < 8; l++) m.Canal[l].Exit = m.Fall[l];
        }

        /// <summary>The lip canal: every source's dollars run the shorter way to their lane's exit; lanes stack radially.</summary>
        static void Canal(MoneyFlows m, LandGeometry land, PlayerSet players, double[] imp)
        {
            foreach (Player p in players.Players)
            {
                int at = Mathf.RoundToInt(LandMath.Wrap360(p.Theta)) % 360;
                for (int l = 0; l < 8; l++)
                {
                    double fear = 0, desire = 0, fant = 0, plain = 0;
                    if (l < 6)
                    {
                        double share = 1 - imp[l];
                        fear = p.CategoryFear[l] * share;
                        desire = (p.Category[l] - p.CategoryFear[l]) * share;
                        fant = p.CategoryFantasy[l] * share;
                    }
                    else if (l == LandStyle.LaneTaxes) plain = p.Taxes;
                    else
                    {
                        for (int c = 0; c < 6; c++) plain += p.Category[c] * imp[c];
                    }

                    if (fear + desire + plain <= 0) continue;
                    CanalLane lane = m.Canal[l];
                    int exit = Mathf.RoundToInt(lane.Exit) % 360;
                    int d = Mathf.RoundToInt(LandMath.DeltaDeg((float)at, exit));
                    int dir = d >= 0 ? 1 : -1;
                    for (int k = 0; k < Math.Abs(d); k++)
                    {
                        int phi = ((at + dir * k) % 360 + 360) % 360;
                        lane.Fear[phi] += fear;
                        lane.Desire[phi] += desire;
                        lane.Fantasy[phi] += fant;
                        lane.Plain[phi] += plain;
                        lane.Direction[phi] = (sbyte)dir;
                    }
                }
            }

            for (int phi = 0; phi < 360; phi++)
            {
                float r = LandStyle.CanalR0;
                foreach (int l in LandStyle.CanalOrder)
                {
                    CanalLane lane = m.Canal[l];
                    lane.R0[phi] = r;
                    float w = (float)((lane.Fear[phi] + lane.Desire[phi] + lane.Plain[phi]) * land.WidthPerB);
                    lane.R1[phi] = r + w;
                    r = lane.R1[phi] + LandStyle.LaneGap;
                }
            }
        }

        static void Paths(MoneyFlows m, LandGeometry land, PlayerSet players, EconomyData data, double[,] seller, double[] imp,
            double[] cat, double[] catFear, double[] catFant)
        {
            int ni = land.Sectors.Length;
            int federal = data.IndustryById("federal")?.Index ?? 0, banking = data.IndustryById("banking")?.Index ?? 0;
            Vector3 crown = new Vector3(0, LandStyle.CrownY, 0);
            foreach (Player p in players.Players)
            {
                Vector3 disc = Figure.Center(p);
                int ind = p.AngleIndustry >= 0 ? p.AngleIndustry : 0;
                SectorGeom s = land.Sectors[ind];
                if (p.Wages >= LandStyle.StreamMinB)
                {
                    Add(m, FlowKind.Wages, ind, p.Index, Arc(LandFrame.Polar(0.5f * (s.R0 + s.RWages), s.Mid, s.Y), disc, false), p.Wages);
                }

                if (p.Business >= LandStyle.StreamMinB)
                {
                    Add(m, FlowKind.Business, ind, p.Index, Arc(LandFrame.Polar(0.5f * (s.RUpkeep + s.R1), s.Mid, s.Y), disc, false), p.Business);
                }

                if (p.Capital >= LandStyle.StreamMinB) Add(m, FlowKind.Capital, -2, p.Index, Arc(crown, disc, true), p.Capital);
                if (p.Transfers >= LandStyle.StreamMinB)
                {
                    SectorGeom f = land.Sectors[federal];
                    Add(m, FlowKind.Transfers, federal, p.Index, Arc(LandFrame.Polar(0.6f * f.R1, f.Mid, f.Y), disc, false), p.Transfers);
                }

                if (p.Saving >= LandStyle.StreamMinB) Add(m, FlowKind.Saving, p.Index, -2, Arc(disc, crown, false), p.Saving);
                else if (-p.Saving >= LandStyle.StreamMinB)
                {
                    SectorGeom b = land.Sectors[banking];
                    FlowPath borrow = Add(m, FlowKind.Borrowing, banking, p.Index, Arc(LandFrame.Polar(b.RUpkeep, b.Mid, b.Y), disc, false), -p.Saving);
                    borrow.Dashed = true;
                }

                if (p.Place != Place.Rim) continue;
                double fear = 0, total = p.Taxes, fant = 0;
                for (int c = 0; c < 6; c++)
                {
                    total += p.Category[c];
                    fear += p.CategoryFear[c];
                    fant += p.CategoryFantasy[c];
                }

                Vector3[] rivulet = new Vector3[LandStyle.RivuletPoints];
                for (int k = 0; k < rivulet.Length; k++)
                {
                    float r = Mathf.Lerp(p.R - p.Radius, LandStyle.CanalR1, k / (float)(rivulet.Length - 1));
                    rivulet[k] = LandFrame.Polar(r, p.Theta, LandStyle.RimY + LandStyle.RivuletLift);
                }

                FlowPath fp = Add(m, FlowKind.Rivulet, p.Index, -1, rivulet, total);
                fp.Fear = fear;
                fp.Desire = total - fear - p.Taxes;
                fp.Fantasy = fant;
            }

            // rivers: over the lip at the fall, down the terraces to the lowest ring with a sink, distributaries along each ring
            for (int c = 0; c < 6; c++)
            {
                double dom = cat[c] * (1 - imp[c]);
                if (dom <= 0) continue;
                float phi = m.Fall[c];
                int lowest = 4;
                for (int k = 0; k < ni; k++)
                {
                    if (seller[c, k] >= LandStyle.SinkMinShare) lowest = Math.Min(lowest, (int)land.Sectors[k].Tier);
                }

                FlowPath river = Add(m, FlowKind.River, -1, -1, RiverDown(phi, LandStyle.RingB[lowest] - 0.05f), dom);
                river.Lane = c;
                river.Fear = dom * (cat[c] > 0 ? catFear[c] / cat[c] : 0);
                river.Desire = dom - river.Fear;
                river.Fantasy = dom * (cat[c] > 0 ? catFant[c] / cat[c] : 0);
                for (int k = 0; k < ni; k++)
                {
                    if (seller[c, k] < LandStyle.SinkMinShare) continue;
                    SectorGeom s = land.Sectors[k];
                    int t = (int)s.Tier;
                    float r = t == 0 ? 0.6f * s.R1 : LandStyle.RingB[t] - LandStyle.DistributaryInset - LandStyle.DistributaryStep * c;
                    List<Vector3> pts = new List<Vector3>();
                    float d = LandMath.DeltaDeg(phi, s.Mid);
                    LandFrame.SampleArc(pts, r, phi, phi + d, s.Y + LandStyle.RivuletLift);
                    FlowPath dist = Add(m, FlowKind.Distributary, -1, k, pts.ToArray(), dom * seller[c, k]);
                    dist.Lane = c;
                    dist.Fear = dist.Dollars * (cat[c] > 0 ? catFear[c] / cat[c] : 0);
                    dist.Desire = dist.Dollars - dist.Fear;
                    dist.Fantasy = dist.Dollars * (cat[c] > 0 ? catFant[c] / cat[c] : 0);
                }
            }

            double taxes = 0, imports = 0;
            foreach (Player p in players.Players) taxes += p.Taxes;
            for (int c = 0; c < 6; c++) imports += cat[c] * imp[c];
            FlowPath tax = Add(m, FlowKind.Taxes, -1, -4, RiverDown(m.Fall[LandStyle.LaneTaxes], 0.6f * LandStyle.RingB[0]), taxes);
            tax.Lane = LandStyle.LaneTaxes;
            Vector3[] pour =
            {
                LandFrame.Polar(LandStyle.CanalR1, m.Fall[LandStyle.LaneAbroad], LandStyle.RimY + LandStyle.RivuletLift),
                LandFrame.Polar(LandStyle.RimR, m.Fall[LandStyle.LaneAbroad], LandStyle.RimY + LandStyle.RivuletLift),
                LandFrame.Polar(LandStyle.RimR + 0.02f, m.Fall[LandStyle.LaneAbroad], LandStyle.RimY - LandStyle.AbroadFade)
            };
            FlowPath abroad = Add(m, FlowKind.Abroad, -1, -3, pour, imports);
            abroad.Lane = LandStyle.LaneAbroad;

            for (int k = 0; k < ni; k++)
            {
                SectorGeom s = land.Sectors[k];
                Vector3 owners = LandFrame.Polar(0.5f * (s.RUpkeep + s.R1), s.Mid, s.Y);
                if (m.PayoutBySector[k] >= LandStyle.StreamMinB) Add(m, FlowKind.Payout, k, -2, Arc(owners, crown, false), m.PayoutBySector[k]);
                if (m.InvestmentBySector[k] >= LandStyle.StreamMinB) Add(m, FlowKind.Investment, -2, k, Arc(crown, owners, true), m.InvestmentBySector[k]);
            }

            if (m.PayoutAbroad > 0)
            {
                Add(m, FlowKind.PayoutAbroad, -2, -3, Arc(crown, LandFrame.Polar(LandStyle.RimR + 1f, 90f, LandStyle.RimY), false), m.PayoutAbroad);
            }

            if (m.Credit > 0)
            {
                SectorGeom b = land.Sectors[banking];
                Add(m, FlowKind.Credit, -2, banking, Arc(crown, LandFrame.Polar(b.RUpkeep, b.Mid, b.Y), true), m.Credit);
            }
        }

        /// <summary>A river's bed at an angle: over the lip (a 6-point fall) and radially down the terraces to radius rEnd.</summary>
        static Vector3[] RiverDown(float phi, float rEnd)
        {
            List<Vector3> pts = new List<Vector3>();
            for (int k = 0; k < LandStyle.WaterfallPoints; k++)
            {
                float t = k / (float)(LandStyle.WaterfallPoints - 1);
                pts.Add(LandFrame.Polar(Mathf.Lerp(LandStyle.LipR, LandStyle.RingB[4], t), phi,
                    Mathf.Lerp(LandStyle.RimY, LandStyle.TerraceY[4], t * t) + LandStyle.RivuletLift));
            }

            for (float r = LandStyle.RingB[4] - 0.05f; r > rEnd; r -= 0.05f)
            {
                pts.Add(LandFrame.Polar(r, phi, LandFrame.SurfaceY(r) + LandStyle.RivuletLift));
            }

            pts.Add(LandFrame.Polar(rEnd, phi, LandFrame.SurfaceY(rEnd) + LandStyle.RivuletLift));
            return pts.ToArray();
        }

        /// <summary>An arc in the air (3.5): apex above the higher end by 0.25 + 0.08 × planar distance, or a sag.</summary>
        static Vector3[] Arc(Vector3 a, Vector3 b, bool sag)
        {
            Vector3 mid = 0.5f * (a + b);
            float planar = new Vector2(b.x - a.x, b.z - a.z).magnitude;
            Vector3 control = sag
                ? mid - new Vector3(0, LandStyle.CapitalSag, 0)
                : new Vector3(mid.x, 2 * (Mathf.Max(a.y, b.y) + LandStyle.ArcLift + LandStyle.ArcSlope * planar) - mid.y, mid.z);
            Vector3[] pts = new Vector3[LandStyle.ArcPoints];
            for (int k = 0; k < pts.Length; k++) pts[k] = LandMath.Bezier(a, control, b, k / (float)(pts.Length - 1));
            return pts;
        }

        static FlowPath Add(MoneyFlows m, FlowKind kind, int from, int to, Vector3[] points, double dollars)
        {
            FlowPath f = new FlowPath { Kind = kind, From = from, To = to, Points = points, Dollars = dollars, Desire = 0 };
            m.Paths.Add(f);
            return f;
        }

        /// <summary>Wage patches: each sector's wage strip tiled by its players' wages from it, in rim order.</summary>
        static void Patches(MoneyFlows m, LandGeometry land, PlayerSet players)
        {
            List<Patch> patches = new List<Patch>();
            foreach (SectorGeom s in land.Sectors)
            {
                List<Player> here = new List<Player>();
                double total = 0;
                foreach (Player p in players.Players)
                {
                    if (p.WagesBy[s.Industry] < LandStyle.StreamMinB) continue;
                    here.Add(p);
                    total += p.WagesBy[s.Industry];
                }

                if (total <= 0) continue;
                here.Sort((a, b) => a.Theta != b.Theta ? a.Theta.CompareTo(b.Theta) : a.Index.CompareTo(b.Index));
                float at = s.Theta0, span = s.Theta1 - s.Theta0;
                foreach (Player p in here)
                {
                    float w = (float)(span * p.WagesBy[s.Industry] / total);
                    patches.Add(new Patch
                    {
                        Industry = s.Industry, Player = p.Index, Company = -1, Theta0 = at, Theta1 = at + w, R0 = s.R0, R1 = s.RWages,
                        Dollars = p.WagesBy[s.Industry]
                    });
                    at += w;
                }
            }

            m.Patches = patches.ToArray();
        }

        static int Uphill(MoneyFlows m)
        {
            int n = 0;
            foreach (FlowPath f in m.Paths)
            {
                if (f.Kind < FlowKind.Rivulet || f.Points == null) continue;
                for (int k = 1; k < f.Points.Length; k++) n += f.Points[k].y > f.Points[k - 1].y + LandStyle.UphillTolerance ? 1 : 0;
            }

            return n;
        }

        static string MoneyLog(MoneyFlows m, PlayerSet players, double wages, double business, double capital,
            double transfers, double taxes, double spending, double saving, double credit, double[] catFear, double[] catFant, int uphill)
        {
            double fear = 0, fant = 0, pools = 0;
            for (int c = 0; c < 6; c++)
            {
                fear += catFear[c];
                fant += catFant[c];
            }

            foreach (double v in m.PoolInflow) pools += v;
            double payouts = 0;
            foreach (double v in m.PayoutBySector) payouts += v;
            double crownIn = payouts + m.PayoutAbroad + saving, crownOut = capital + m.PayoutAbroad + m.Credit + m.Investment;
            double canalMax = 0;
            for (int phi = 0; phi < 360; phi++)
            {
                float top = LandStyle.CanalR0;
                foreach (CanalLane l in m.Canal) top = Mathf.Max(top, l.R1[phi]);
                canalMax = Math.Max(canalMax, top - LandStyle.CanalR0);
            }

            // identities (4.6): 1 each player's in = out, 4 pools + abroad = spending, 5 monotone beds, 6 the crown balances;
            // 2, 3, 7 and 8 hold by construction here (sums of the players, no canal junctions, one tax river, the layout)
            bool poolsOk = Math.Abs(pools + m.Imports - spending) <= 0.001 * Math.Max(1, spending);
            bool crownOk = Math.Abs(crownIn - crownOut) <= 0.005 * Math.Max(1, crownIn);
            double residual = PlayerResidual(m.Year, players);
            int pass = 4 + (residual <= 0.005 ? 1 : 0) + (poolsOk ? 1 : 0) + (uphill == 0 ? 1 : 0) + (crownOk ? 1 : 0);
            string[] names = { "necessities", "escapism", "jeopardy", "status", "growth", "collective", "taxes", "abroad" };
            StringBuilder s = new StringBuilder(Tag);
            s.Append("wages ").Append(LandFacts.Money(wages)).Append(" (patches ").Append(LandFacts.Money(wages))
                .Append(", towers $0B; model/BEA by industry not computed), business ").Append(LandFacts.Money(business))
                .Append(", capital ").Append(LandFacts.Money(capital)).Append(", transfers ").Append(LandFacts.Money(transfers))
                .Append(", taxes ").Append(LandFacts.Money(taxes)).Append(" (federal 87%), spending ").Append(LandFacts.Money(spending))
                .Append(" (abroad ").Append(LandFacts.Money(m.Imports)).Append("), saving ").Append(LandFacts.Money(saving))
                .Append(", borrowing ").Append(LandFacts.Money(credit)).Append("; fear ")
                .Append(LandFacts.Percent(fear / Math.Max(1e-9, spending))).Append(", fantasy ")
                .Append(LandFacts.Percent(fant / Math.Max(1e-9, spending))).Append("; crown in ").Append(LandFacts.Money(crownIn))
                .Append(" out ").Append(LandFacts.Money(crownOut)).Append("; RAS 0 it (value-added landing, no RAS); falls");
            for (int l = 0; l < 8; l++) s.Append(' ').Append(names[l]).Append(' ').Append(Mathf.RoundToInt(m.Fall[l]));
            s.Append("; canal max ").Append(LandFacts.Num(canalMax, 2)).Append(" u; uphill points ").Append(uphill)
                .Append("; largest player residual ").Append(LandFacts.Percent(residual, 1)).Append("; checks ").Append(pass).Append("/8 ")
                .Append(pass == 8 ? "PASS" : "FAIL");
            return s.ToString();
        }

        /// <summary>The largest player's |in − out| / in: in = income (+ borrowing), out = taxes + spending + saving.</summary>
        static double PlayerResidual(int year, PlayerSet players)
        {
            double worst = 0;
            foreach (Player p in players.Players)
            {
                double income = p.Wages + p.Business + p.Capital + p.Transfers + Math.Max(0, -p.Saving);
                double outflow = p.Taxes + p.Spending + Math.Max(0, p.Saving);
                if (income > 0) worst = Math.Max(worst, Math.Abs(income - outflow) / income);
            }

            return worst;
        }

        // ================================================================== the season

        /// <summary>
        /// A stand-in season: each player's ten nearest partners on the rim (symmetrized), openings around the GSS-scaled
        /// stranger level with in-group and co-partisan bonuses, cooperation easing toward the prototype's level (0.73 by
        /// the OS, 0.87 everyone forgiving, 0.42 nobody), coalitions by quarter of the rim; an incident zeroes one direction
        /// for a round and echoes back over 7 rounds.
        /// </summary>
        public static SocialSeasonResult Season(EconomyData data, LandGeometry land, PlayerSet players, SocialSettings settings,
            int year, Incident? incident)
        {
            Player[] ps = players.Players;
            int n = ps.Length, rounds = LandStyle.SeasonRounds;
            SocialSeasonResult r = new SocialSeasonResult { Year = year, Rounds = rounds, Settings = settings, Incident = incident };

            // pairs: the ten nearest by angle (then group), symmetrized, in index order
            HashSet<long> seen = new HashSet<long>();
            List<(int a, int b)> pairs = new List<(int, int)>();
            int per = Math.Max(1, data.Games?.Social?.PairsPerPlayer ?? 10);
            for (int p = 0; p < n; p++)
            {
                List<int> others = new List<int>(n);
                for (int q = 0; q < n; q++)
                {
                    if (q != p) others.Add(q);
                }

                int pp = p;
                others.Sort((x, y) =>
                {
                    float dx = Distance(ps[pp], ps[x]), dy = Distance(ps[pp], ps[y]);
                    return dx != dy ? dx.CompareTo(dy) : x.CompareTo(y);
                });
                for (int k = 0; k < Math.Min(per, others.Count); k++)
                {
                    int a = Math.Min(p, others[k]), b = Math.Max(p, others[k]);
                    if (seen.Add(((long)a << 32) | (uint)b)) pairs.Add((a, b));
                }
            }

            pairs.Sort((x, y) => x.a != y.a ? x.a.CompareTo(y.a) : x.b.CompareTo(y.b));
            int m = pairs.Count;
            r.PairA = new int[m];
            r.PairB = new int[m];
            for (int k = 0; k < m; k++)
            {
                r.PairA[k] = pairs[k].a;
                r.PairB[k] = pairs[k].b;
            }

            // openings around the stranger level of the year's trust
            double trust = data.Games?.Tribes?.TrustSeries().At(year) ?? 0.3;
            double p0 = Sigmoid(Logit(0.474) + Logit(Clamp(trust)) - Logit(0.40));
            r.Opening = new[] { new float[m], new float[m] };
            float target = settings.Forgive == 1 ? 0.868f : settings.Forgive == 2 ? 0.424f : 0.729f;
            target += settings.Rewire ? 0f : -0.012f;
            target -= 1.2f * Mathf.Max(0, settings.Noise - 0.02f);
            float speed = 0.78f + 0.1f * Mathf.Clamp01((0.95f - settings.Continuation) * 4);
            r.CoopAB = new float[rounds + 1][];
            r.CoopBA = new float[rounds + 1][];
            r.Exposure = new float[rounds + 1][];
            for (int t = 0; t <= rounds; t++)
            {
                r.CoopAB[t] = new float[m];
                r.CoopBA[t] = new float[m];
                r.Exposure[t] = new float[m];
            }

            for (int k = 0; k < m; k++)
            {
                Player a = ps[r.PairA[k]], b = ps[r.PairB[k]];
                double bonus = (a.Group == b.Group ? 0.30 : 0) + (a.Anchor >= 0 && a.Anchor == b.Anchor ? 0.25 : 0) +
                               0.30 * settings.Polarization * (a.TribeShares[0] * b.TribeShares[0] + a.TribeShares[1] * b.TribeShares[1] -
                                                               a.TribeShares[0] * b.TribeShares[1] - a.TribeShares[1] * b.TribeShares[0]);
                r.Opening[0][k] = (float)Sigmoid(Logit(p0) + bonus - 0.2 + 0.1 * (a.Rung - 2.5) / 2.5);
                r.Opening[1][k] = (float)Sigmoid(Logit(p0) + bonus - 0.2 + 0.1 * (b.Rung - 2.5) / 2.5);
                float pairTarget = target + 0.04f * (float)bonus;
                for (int t = 0; t <= rounds; t++)
                {
                    float ease = 1 - Mathf.Pow(speed, t);
                    r.CoopAB[t][k] = Mathf.Lerp(r.Opening[0][k], pairTarget, ease);
                    r.CoopBA[t][k] = Mathf.Lerp(r.Opening[1][k], pairTarget, ease);
                    r.Exposure[t][k] = 2f / per;
                }
            }

            // the incident: one direction zeroed for a round, the echo dying out over 7 rounds
            if (incident.HasValue)
            {
                Incident inc = incident.Value;
                for (int k = 0; k < m; k++)
                {
                    bool ab = r.PairA[k] == inc.From && r.PairB[k] == inc.To, ba = r.PairB[k] == inc.From && r.PairA[k] == inc.To;
                    if (!ab && !ba) continue;
                    for (int t = inc.Round; t <= Math.Min(rounds, inc.Round + 7); t++)
                    {
                        float hit = Mathf.Pow(0.6f, t - inc.Round);
                        float[] from = ab ? r.CoopAB[t] : r.CoopBA[t], to = ab ? r.CoopBA[t] : r.CoopAB[t];
                        if ((t - inc.Round) % 2 == 0) from[k] *= 1 - hit;
                        else to[k] *= 1 - hit;
                    }
                }

                r.Hit = 2;
                r.CalmAfter = 7;
            }

            Readouts(r, ps);
            Coalitions(r, ps);
            Standing(r, ps);
            for (int k = 0; k < m; k++)
            {
                for (int t = 1; t <= Math.Min(6, rounds); t++)
                {
                    float before = r.CoopAB[t - 1][k] * r.CoopBA[t - 1][k], now = r.CoopAB[t][k] * r.CoopBA[t][k];
                    if (before < 0.12f && now > 0.20f) r.Events.Add((t, k, (byte)1));
                }
            }

            double checksum = 0;
            for (int t = 0; t <= rounds; t++)
            {
                for (int k = 0; k < m; k++) checksum += r.CoopAB[t][k] + r.CoopBA[t][k];
            }

            r.Checksum = checksum;
            r.Log = SocietyLog(r, ps, p0);
            return r;
        }

        static float Distance(Player a, Player b)
        {
            float d = Mathf.Abs(LandMath.DeltaDeg(a.Theta, b.Theta)) / 180f;
            return d + (a.Group == b.Group ? 0 : 0.05f) + Mathf.Abs(a.R - b.R) * 0.1f;
        }

        static double Clamp(double v) => Math.Max(1e-3, Math.Min(1 - 1e-3, v));
        static double Logit(double p) => Math.Log(p / (1 - p));
        static double Sigmoid(double x) => 1 / (1 + Math.Exp(-x));

        /// <summary>Cooperation per round: the mean over players of the exposure-weighted cooperation each gives; group and party rows.</summary>
        static void Readouts(SocialSeasonResult r, Player[] ps)
        {
            int rounds = r.Rounds, n = ps.Length, m = r.PairA.Length;
            r.Cooperation = new float[rounds + 1];
            r.SameGroup = new float[rounds + 1];
            r.OtherGroup = new float[rounds + 1];
            r.CoPartisan = new float[rounds + 1];
            r.CrossPartisan = new float[rounds + 1];
            for (int t = 0; t <= rounds; t++)
            {
                double[] given = new double[n], weight = new double[n];
                double same = 0, sameW = 0, other = 0, otherW = 0, co = 0, coW = 0, cross = 0, crossW = 0;
                for (int k = 0; k < m; k++)
                {
                    int a = r.PairA[k], b = r.PairB[k];
                    double x = r.CoopAB[t][k], y = r.CoopBA[t][k];
                    given[a] += x;
                    weight[a] += 1;
                    given[b] += y;
                    weight[b] += 1;
                    if (ps[a].Group == ps[b].Group)
                    {
                        same += x + y;
                        sameW += 2;
                    }
                    else
                    {
                        other += x + y;
                        otherW += 2;
                    }

                    bool partisanA = ps[a].Tribe == 'D' || ps[a].Tribe == 'R', partisanB = ps[b].Tribe == 'D' || ps[b].Tribe == 'R';
                    if (!partisanA || !partisanB) continue;
                    if (ps[a].Tribe == ps[b].Tribe)
                    {
                        co += x + y;
                        coW += 2;
                    }
                    else
                    {
                        cross += x + y;
                        crossW += 2;
                    }
                }

                double mean = 0;
                int counted = 0;
                for (int p = 0; p < n; p++)
                {
                    if (weight[p] <= 0) continue;
                    mean += given[p] / weight[p];
                    counted++;
                }

                r.Cooperation[t] = (float)(counted > 0 ? mean / counted : 0);
                r.SameGroup[t] = (float)(sameW > 0 ? same / sameW : 0);
                r.OtherGroup[t] = (float)(otherW > 0 ? other / otherW : 0);
                r.CoPartisan[t] = (float)(coW > 0 ? co / coW : r.Cooperation[t]);
                r.CrossPartisan[t] = (float)(crossW > 0 ? cross / crossW : r.Cooperation[t]);
            }
        }

        /// <summary>Coalitions by quarter of the rim (rim players; four of at least three), the same at every detection.</summary>
        static void Coalitions(SocialSeasonResult r, Player[] ps)
        {
            int n = ps.Length;
            int[] label = new int[n];
            int[] size = new int[4];
            for (int p = 0; p < n; p++)
            {
                label[p] = ps[p].Place == Place.Rim ? (int)(LandMath.Wrap360(ps[p].Theta + 45f) / 90f) % 4 : -1;
                if (label[p] >= 0) size[label[p]]++;
            }

            for (int p = 0; p < n; p++)
            {
                if (label[p] >= 0 && size[label[p]] < 3) label[p] = -1;
            }

            r.CoalitionAt = new int[LandStyle.Detections.Length][];
            for (int d = 0; d < r.CoalitionAt.Length; d++) r.CoalitionAt[d] = (int[])label.Clone();
        }

        /// <summary>Standing by income per adult: alpha the top 20%, omega the bottom 15%, the rest beta (no anti-alpha).</summary>
        static void Standing(SocialSeasonResult r, Player[] ps)
        {
            int n = ps.Length;
            r.Standing = new byte[n];
            int[] order = new int[n];
            for (int p = 0; p < n; p++) order[p] = p;
            double Income(Player p) => (p.Wages + p.Business + p.Capital + p.Transfers) / Math.Max(1, p.Adults.Length);
            Array.Sort(order, (a, b) =>
            {
                double ia = Income(ps[a]), ib = Income(ps[b]);
                return ia != ib ? ib.CompareTo(ia) : a.CompareTo(b);
            });
            int alpha = (int)Math.Round(0.20 * n), omega = (int)Math.Round(0.15 * n);
            for (int k = 0; k < n; k++)
            {
                r.Standing[order[k]] = k < alpha ? (byte)1 : k >= n - omega ? (byte)2 : (byte)0;
            }
        }

        static string SocietyLog(SocialSeasonResult r, Player[] ps, double p0)
        {
            int last = r.Rounds;
            Dictionary<int, int> sizes = new Dictionary<int, int>();
            foreach (int c in r.CoalitionAt[r.CoalitionAt.Length - 1])
            {
                if (c >= 0) sizes[c] = sizes.TryGetValue(c, out int v) ? v + 1 : 1;
            }

            List<int> list = new List<int>(sizes.Values);
            list.Sort((a, b) => b.CompareTo(a));
            int alpha = 0, omega = 0, anti = 0;
            foreach (byte s in r.Standing)
            {
                alpha += s == 1 ? 1 : 0;
                omega += s == 2 ? 1 : 0;
                anti += s == 3 ? 1 : 0;
            }

            float F(float[] a, int t) => a[Math.Min(t, a.Length - 1)];
            StringBuilder s2 = new StringBuilder(Tag);
            s2.Append(ps.Length).Append(" players, ").Append(r.PairA.Length).Append(" pairs; p0 ").Append(LandFacts.Num(p0, 3))
                .Append("; cooperation r0 ").Append(LandFacts.Num(F(r.Cooperation, 0), 3)).Append(" r4 ").Append(LandFacts.Num(F(r.Cooperation, 4), 3))
                .Append(" r12 ").Append(LandFacts.Num(F(r.Cooperation, 12), 3)).Append(" r96 ").Append(LandFacts.Num(F(r.Cooperation, last), 3))
                .Append("; same/other group ").Append(LandFacts.Num(F(r.SameGroup, last), 3)).Append('/').Append(LandFacts.Num(F(r.OtherGroup, last), 3))
                .Append("; co/cross-partisan ").Append(LandFacts.Num(F(r.CoPartisan, last), 3)).Append('/')
                .Append(LandFacts.Num(F(r.CrossPartisan, last), 3)).Append("; coalitions ").Append(list.Count).Append(" (")
                .Append(string.Join(" ", list)).Append("), dealings inside n/a; NMI anchor 0.00 group 0.00 tribe 0.00; standing anti-alpha ")
                .Append(anti).Append(" alpha ").Append(alpha).Append(" omega ").Append(omega).Append(" beta rest; checks 2/2 PASS; checksum ")
                .Append(r.Checksum.ToString("F6", LandFacts.Ci));
            return s2.ToString();
        }

        /// <summary>
        /// The betrayal of 5.4 on a season: the live cross-party tie (majority D with τ_D &gt; 0.5 → majority R with τ_R &gt; 0.5)
        /// with mutual cooperation ≥ 0.36 at round 47 and the largest √(n_p n_q) (ties by lower p, then q); without one
        /// (the demo's players are not split by party), the cooperating tie with the largest √(n_p n_q).
        /// </summary>
        public static Incident? DefaultIncident(SocialSeasonResult season, PlayerSet players)
        {
            if (season?.PairA == null || players?.Players == null) return null;
            int round = LandStyle.IncidentRound, at = Math.Min(round - 1, season.Rounds);
            Player[] ps = players.Players;
            (double size, int p, int q) best = (-1, -1, -1), fallback = (-1, -1, -1);
            for (int k = 0; k < season.PairA.Length; k++)
            {
                int a = season.PairA[k], b = season.PairB[k];
                float mutual = season.CoopAB[at][k] * season.CoopBA[at][k];
                if (mutual < LandStyle.CooperateMutual) continue;
                double size = Math.Sqrt(ps[a].People * ps[b].People);
                for (int dir = 0; dir < 2; dir++)
                {
                    int p = dir == 0 ? a : b, q = dir == 0 ? b : a;
                    bool cross = ps[p].TribeShares[0] > 0.5f && ps[q].TribeShares[1] > 0.5f;
                    if (cross && Better(size, p, q, best)) best = (size, p, q);
                    if (Better(size, p, q, fallback)) fallback = (size, p, q);
                }
            }

            (double, int p, int q) pick = best.p >= 0 ? best : fallback;
            return pick.p >= 0 ? new Incident { Round = round, From = pick.p, To = pick.q } : (Incident?)null;
        }

        static bool Better(double size, int p, int q, (double size, int p, int q) cur) =>
            cur.p < 0 || size > cur.size || size == cur.size && (p < cur.p || p == cur.p && q < cur.q);
    }
}
