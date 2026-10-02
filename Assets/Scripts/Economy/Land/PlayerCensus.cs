using System;
using System.Collections.Generic;
using System.Text;
using Why.Economy.Data;
using Why.Economy.Model;
using Why.Humans.Smv;

namespace Why.Economy.Land
{
    /// <summary>
    /// The players of a year (SPEC 3.1-3.2): every adult alive in the year lands in one of the twelve 2026 groups by the
    /// priority rule of groups.json (rule 5, Erikson's dominance: a non-employed adult under the retirement age married
    /// to an employed spouse takes the spouse's group and industry); within a group the adults form cells by their
    /// anchor (the industry that pays them, the spouse's industry, capital for the 1% who do not work, or a pseudo-anchor
    /// split by generation for retirees, students and the out of work). A cell is a player when it passes the size test
    /// (≥ 10 lines, or ≥ 3 lines holding ≥ 1% of net worth); place cells too small join their tier, tier cells too small
    /// pool into the group's rest, a rest too small joins the group's largest cell; within each cell a D, R or I part
    /// that passes the test is its own player and the rest pool into one mixed (M) player, or join the largest part.
    /// Children join their mother's player, else their father's, else (no parent alive in the year) the largest player
    /// of the 1% (wealth group 3), the PMC (income rank ≥ 0.8) or the out of work. Players are sorted by (group, anchor,
    /// tribe) and carry their members' money and minds; <see cref="PlayerPlacement"/> gives them their places (3.3).
    /// Reproduces the prototypes groups/groups.py and groups/players.py on the same lives. Pure: no Unity objects, any
    /// thread, deterministic (every order has an index tie-break).
    /// </summary>
    public static class PlayerCensus
    {
        /// <summary>The groups' short names in the log line (8.8), in group order.</summary>
        public static readonly string[] GroupShort =
            { "1%", "owners", "gig", "PMC", "public", "office", "frontline", "poor", "comf.ret", "ss.ret", "students", "out" };

        // anchor codes: the cell keys within a group, in the players' sort order (industries by index, capital, the
        // pseudo-anchors by generation, the tier cells, the merged pseudo-anchor cells, the rest)
        const int CodeCapital = 100, CodePseudo = 200, CodeTier = 300, CodeTierCapital = 400, CodeTierPseudo = 500, CodeRest = 600;

        /// <summary>A cell of a group before the party split: its anchor code and members (person indices, ascending).</summary>
        sealed class Cell
        {
            public int Code;
            public List<int> Members = new List<int>();
        }

        /// <summary>The players of a year; an empty set (and a failing log line) when the lives were not simulated.</summary>
        public static PlayerSet Build(EconomyModel model, SmvPopulation pop, LandGeometry land, int year)
        {
            PlayerSet set = new PlayerSet { Year = year, Players = Array.Empty<Player>(), PlayerOfPerson = Array.Empty<int>() };
            EconomicLives lives = model?.Lives;
            SmvSimulation sim = lives?.Sim ?? pop?.Sim;
            if (lives == null || !lives.Ready || sim == null || land == null)
            {
                set.Log = "0 players (the lives were not simulated); checks 0/3 FAIL; checksum 0";
                return set;
            }

            EconomyData data = model.Data;
            GroupsFile gf = data.GroupsFile ?? new GroupsFile();
            int n = sim.People.Count, ni = data.Industries.Count;
            double unit = sim.PeoplePerLine / 1e9;   // $ per person-year of a line -> $B

            // ---- the year's records
            PersonYear[] rec = new PersonYear[n];
            bool[] alive = new bool[n];
            List<int> adults = new List<int>(n), kids = new List<int>(n);
            List<float> earn = new List<float>(n);
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
            double medEarn = earn.Count == 0 ? 0
                : earn.Count % 2 == 1 ? earn[earn.Count / 2] : 0.5 * ((double)earn[earn.Count / 2 - 1] + earn[earn.Count / 2]);

            // spouses at mid-year (MarriageLog: Start <= Y + 0.5 < End)
            int[] spouse = new int[n];
            for (int i = 0; i < n; i++) spouse[i] = -1;
            double mid = year + 0.5;
            foreach (SmvMarriage m in sim.MarriageLog)
            {
                if (m.Start > mid || m.End <= mid || m.Wife < 0 || m.Husband < 0 || m.Wife >= n || m.Husband >= n) continue;
                spouse[m.Wife] = m.Husband;
                spouse[m.Husband] = m.Wife;
            }

            // ---- groups: rules 1-4 and 6 on the person's own record, then rule 5 (dominance) on the spouse's
            bool[] gov = new bool[ni], office = new bool[ni];
            for (int k = 0; k < ni; k++)
            {
                gov[k] = gf.GovernmentIndustries != null && gf.GovernmentIndustries.Contains(data.Industries[k].Id);
                office[k] = gf.OfficeIndustries != null && gf.OfficeIndustries.Contains(data.Industries[k].Id);
            }

            int[] own = new int[n], group = new int[n], code = new int[n], angleOf = new int[n];
            bool[] dominance = new bool[n];
            for (int i = 0; i < n; i++) own[i] = group[i] = code[i] = angleOf[i] = -1;
            foreach (int i in adults) own[i] = OwnGroup(rec[i], medEarn, gf.Thresholds, gov, office);
            foreach (int i in adults)
            {
                group[i] = own[i];
                if (own[i] >= 0) continue;
                int s = spouse[i];
                if (s >= 0 && alive[s] && rec[s].Adult && rec[s].Employed && own[s] >= 0)
                {
                    group[i] = own[s];
                    dominance[i] = true;
                }
                else group[i] = rec[i].Age < gf.Thresholds.YoungAge ? (int)Group.Students : (int)Group.OutOfWork;
            }

            // ---- anchors (cell codes) and the industry whose angle each adult would take
            int[] pseudoOfGroup = new int[12];
            for (int g = 0; g < 12; g++) pseudoOfGroup[g] = -1;
            int[] pseudoAngle = new int[gf.PseudoAnchors?.Count ?? 0];
            for (int k = 0; k < pseudoAngle.Length; k++)
            {
                PseudoAnchor pa = gf.PseudoAnchors[k];
                for (int g = 0; g < 12 && g < gf.Groups.Count; g++)
                {
                    if (gf.Groups[g].Id == pa.Group && pseudoOfGroup[g] < 0) pseudoOfGroup[g] = k;
                }

                pseudoAngle[k] = data.IndustryById(pa.AngleIndustry)?.Index ?? -1;
            }

            int finance = data.IndustryById("finance")?.Index ?? 0;
            foreach (int i in adults)
            {
                PersonYear r = rec[i];
                int g = group[i];
                if (r.Employed && r.Industry >= 0 && r.Industry < ni) code[i] = r.Industry;
                else if (dominance[i] && rec[spouse[i]].Industry >= 0 && rec[spouse[i]].Industry < ni) code[i] = rec[spouse[i]].Industry;
                else if (g == (int)Group.Top1 || pseudoOfGroup[g] < 0) code[i] = CodeCapital;
                else code[i] = CodePseudo + 10 * pseudoOfGroup[g] + Generation(gf, sim.People[i].Birth);
                int c = code[i];
                angleOf[i] = c < ni ? c : c >= CodePseudo && c < CodeTier ? pseudoAngle[(c - CodePseudo) / 10] : finance;
                if (angleOf[i] < 0) angleOf[i] = finance;
            }

            // ---- cells: places, tiers, the rest; then the party split
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

            List<Player> players = new List<Player>(160);
            List<int>[] groupAdults = new List<int>[12];
            for (int g = 0; g < 12; g++) groupAdults[g] = new List<int>();
            foreach (int i in adults) groupAdults[group[i]].Add(i);
            for (int g = 0; g < 12; g++)
            {
                if (groupAdults[g].Count == 0) continue;
                List<Cell> cells = Cells(groupAdults[g], code, land, ni, Ok);
                foreach (Cell cell in cells) Split(cell, g, rec, Ok, players);
            }

            // ---- order, keys, membership
            for (int k = 0; k < players.Count; k++)
            {
                Player p = players[k];
                p.Rung = RungOf(gf, (int)p.Group);
                p.AnchorId = AnchorId(p.Anchor, data, gf, land);
                p.Key = GroupId(gf, (int)p.Group) + "|" + p.AnchorId + "|" + p.Tribe;
            }

            players.Sort((a, b) =>
            {
                int c = a.Group.CompareTo(b.Group);
                if (c != 0) return c;
                c = a.Anchor.CompareTo(b.Anchor);   // the anchor code while sorting
                if (c != 0) return c;
                c = TribeOrder(a.Tribe).CompareTo(TribeOrder(b.Tribe));
                return c != 0 ? c : a.Adults[0].CompareTo(b.Adults[0]);
            });
            int[] playerOf = new int[n];
            for (int i = 0; i < n; i++) playerOf[i] = -1;
            for (int k = 0; k < players.Count; k++)
            {
                Player p = players[k];
                p.Index = k;
                p.Anchor = p.Anchor < ni ? p.Anchor : -1;
                foreach (int i in p.Adults) playerOf[i] = k;
            }

            // ---- children: the mother's player, else the father's, else by their household's standing
            List<int>[] children = new List<int>[players.Count];
            for (int k = 0; k < children.Length; k++) children[k] = new List<int>();
            foreach (int c in kids)
            {
                SmvPerson sp = sim.People[c];
                int p = sp.Mother >= 0 && sp.Mother < n && playerOf[sp.Mother] >= 0 ? playerOf[sp.Mother]
                    : sp.Father >= 0 && sp.Father < n && playerOf[sp.Father] >= 0 ? playerOf[sp.Father] : -1;
                if (p < 0)
                {
                    Group g = rec[c].WealthGroup == 3 ? Group.Top1 : rec[c].IncomeRank >= gf.Thresholds.PmcRank ? Group.Pmc : Group.OutOfWork;
                    p = LargestOf(players, g);
                }

                if (p < 0) continue;
                children[p].Add(c);
                playerOf[c] = p;
            }

            // ---- money and minds
            int[] industryOf = new int[n];   // the industry a person's household wages and business income are paid by
            foreach (int i in adults)
            {
                PersonYear r = rec[i];
                int s = spouse[i];
                industryOf[i] = r.Employed && r.Industry >= 0 && r.Industry < ni ? r.Industry
                    : s >= 0 && alive[s] && rec[s].Employed && rec[s].Industry >= 0 && rec[s].Industry < ni ? rec[s].Industry
                    : code[i] < ni ? code[i] : -1;
            }

            foreach (Player p in players)
            {
                children[p.Index].Sort();
                p.Children = children[p.Index].ToArray();
                Aggregate(p, rec, sim, lives, unit, gf, industryOf);
            }

            set.Players = players.ToArray();
            set.PlayerOfPerson = playerOf;

            // ---- places (3.3)
            PlayerPlacement.Stats places = PlayerPlacement.PlaceAll(set, data, land, rec, angleOf);

            double checksum = 0;
            foreach (Player p in set.Players) checksum += (double)p.Adults.Length * p.Index;
            set.Checksum = checksum;
            set.Log = Log(set, lives, rec, adults, kids.Count, places, data, year) + "; checksum " +
                      checksum.ToString("F6", LandFacts.Ci);
            return set;
        }

        // ================================================================== groups

        /// <summary>
        /// A person's own group by the priority rule (groups.json rules 1-4 and 6's first half), or -1 for a
        /// non-employed adult under the retirement age who is not on Social Security (decided by the household).
        /// </summary>
        static int OwnGroup(PersonYear r, double medEarn, GroupThresholds t, bool[] gov, bool[] office)
        {
            if (r.WealthGroup == 3) return (int)Group.Top1;
            if (r.Employed && r.SelfEmployed) return r.Earned >= medEarn || r.WealthGroup >= 2 ? (int)Group.Owners : (int)Group.Gig;
            if (r.Employed)
            {
                if (r.IncomeRank >= t.PmcRank) return (int)Group.Pmc;
                if (r.IncomeRank < t.PoorRank) return (int)Group.WorkingPoor;
                int k = r.Industry;
                if (k >= 0 && k < gov.Length && gov[k]) return (int)Group.Public;
                if (k >= 0 && k < office.Length && office[k]) return (int)Group.Office;
                return (int)Group.Frontline;
            }

            if (r.Age >= t.RetireAge || r.SocialSecurity > 0)
            {
                double cash = (double)r.SocialSecurity + r.Wages + r.Business + r.CapitalIncome;
                return r.SocialSecurity >= t.SsDependence * cash ? (int)Group.RetiredSocialSecurity : (int)Group.RetiredSavings;
            }

            return -1;
        }

        /// <summary>The generation (groups.json, by birth year) of a birth time.</summary>
        static int Generation(GroupsFile gf, double birth)
        {
            int count = gf.Generations?.Count ?? 0;
            for (int k = 0; k < count; k++)
            {
                if (birth < gf.Generations[k].Before) return k;
            }

            return Math.Max(0, count - 1);
        }

        static int RungOf(GroupsFile gf, int g) => g < gf.Groups.Count ? gf.Groups[g].Rung : 0;

        static string GroupId(GroupsFile gf, int g) => g < gf.Groups.Count ? gf.Groups[g].Id : g.ToString(LandFacts.Ci);

        // ================================================================== cells

        /// <summary>
        /// A group's cells (3.1 rules 3-4): places that pass the size test, the rest by tier, tiers too small pooled into
        /// the rest, a rest too small joined to the largest cell (ties: the lower anchor code). In anchor-code order.
        /// </summary>
        static List<Cell> Cells(List<int> members, int[] code, LandGeometry land, int ni, Func<List<int>, bool> ok)
        {
            SortedDictionary<int, Cell> places = new SortedDictionary<int, Cell>();
            foreach (int i in members)
            {
                if (!places.TryGetValue(code[i], out Cell c)) places[code[i]] = c = new Cell { Code = code[i] };
                c.Members.Add(i);
            }

            SortedDictionary<int, Cell> cells = new SortedDictionary<int, Cell>();
            foreach (KeyValuePair<int, Cell> kv in places)
            {
                int key = ok(kv.Value.Members) ? kv.Key : TierCode(kv.Key, land, ni);
                if (!cells.TryGetValue(key, out Cell c)) cells[key] = c = new Cell { Code = key };
                c.Members.AddRange(kv.Value.Members);
            }

            List<Cell> kept = new List<Cell>(cells.Count);
            Cell rest = new Cell { Code = CodeRest };
            foreach (Cell c in cells.Values)
            {
                if (ok(c.Members)) kept.Add(c);
                else rest.Members.AddRange(c.Members);
            }

            if (rest.Members.Count > 0)
            {
                if (ok(rest.Members) || kept.Count == 0) kept.Add(rest);
                else
                {
                    int big = 0;
                    for (int k = 1; k < kept.Count; k++) big = kept[k].Members.Count > kept[big].Members.Count ? k : big;
                    kept[big].Members.AddRange(rest.Members);
                }
            }

            foreach (Cell c in kept) c.Members.Sort();
            return kept;
        }

        /// <summary>The tier cell a place cell too small joins: its industry's tier, tier:capital, or its pseudo-anchor.</summary>
        static int TierCode(int place, LandGeometry land, int ni)
        {
            if (place < ni) return CodeTier + (int)land.Sectors[place].Tier;
            if (place == CodeCapital) return CodeTierCapital;
            if (place >= CodePseudo && place < CodeTier) return CodeTierPseudo + (place - CodePseudo) / 10;
            return place;
        }

        /// <summary>
        /// The party split of a cell (3.1 rule 5): each of D, R, I that passes the size test is a player; the others pool
        /// into one mixed (M) player, or join the largest kept part (ties in the order D, I, R) when the pool fails.
        /// </summary>
        static void Split(Cell cell, int g, PersonYear[] rec, Func<List<int>, bool> ok, List<Player> into)
        {
            List<int>[] parts = { new List<int>(), new List<int>(), new List<int>() };   // D, R, I
            foreach (int i in cell.Members) parts[Math.Min(2, (int)rec[i].Tribe)].Add(i);
            int[] alphabetical = { 0, 2, 1 };   // D, I, R
            List<int> keep = new List<int>(3);
            List<int> pool = new List<int>();
            foreach (int t in alphabetical)
            {
                if (parts[t].Count == 0) continue;
                if (ok(parts[t])) keep.Add(t);
                else pool.AddRange(parts[t]);
            }

            if (pool.Count > 0)
            {
                if (ok(pool) || keep.Count == 0)
                {
                    pool.Sort();
                    into.Add(NewPlayer(g, cell.Code, 'M', pool));
                }
                else
                {
                    int big = keep[0];
                    foreach (int t in keep) big = parts[t].Count > parts[big].Count ? t : big;
                    parts[big].AddRange(pool);
                    parts[big].Sort();
                }
            }

            foreach (int t in keep) into.Add(NewPlayer(g, cell.Code, "DRI"[t], parts[t]));
        }

        /// <summary>A player of a cell (Anchor holds the cell's anchor code until the players are sorted).</summary>
        static Player NewPlayer(int g, int anchorCode, char tribe, List<int> members) =>
            new Player { Group = (Group)g, Anchor = anchorCode, Tribe = tribe, Adults = members.ToArray() };

        static int TribeOrder(char t) => t == 'D' ? 0 : t == 'R' ? 1 : t == 'I' ? 2 : 3;

        /// <summary>The id of an anchor code: an industry, "capital", "pensions/boomer", "tier:services", "tier:pensions", "rest".</summary>
        static string AnchorId(int c, EconomyData data, GroupsFile gf, LandGeometry land)
        {
            int ni = data.Industries.Count;
            if (c >= 0 && c < ni) return data.Industries[c].Id;
            if (c == CodeCapital) return "capital";
            if (c >= CodePseudo && c < CodeTier)
            {
                int k = (c - CodePseudo) / 10, gen = (c - CodePseudo) % 10;
                string pa = k < gf.PseudoAnchors.Count ? gf.PseudoAnchors[k].Id : "pseudo" + k.ToString(LandFacts.Ci);
                string gn = gen < gf.Generations.Count ? gf.Generations[gen].Id : gen.ToString(LandFacts.Ci);
                return pa + "/" + gn;
            }

            if (c >= CodeTier && c < CodeTierCapital) return "tier:" + TierId((Tier)(c - CodeTier));
            if (c == CodeTierCapital) return "tier:capital";
            if (c >= CodeTierPseudo && c < CodeRest)
            {
                int k = c - CodeTierPseudo;
                return "tier:" + (k < gf.PseudoAnchors.Count ? gf.PseudoAnchors[k].Id : "pseudo" + k.ToString(LandFacts.Ci));
            }

            return "rest";
        }

        /// <summary>The tiers' ids (industries.json tiers).</summary>
        public static string TierId(Tier t) => t switch
        {
            Tier.Gov => "gov", Tier.Raw => "raw", Tier.Make => "make", Tier.Services => "services", _ => "tech"
        };

        static int LargestOf(List<Player> players, Group g)
        {
            int best = -1;
            foreach (Player p in players)
            {
                if (p.Group == g && (best < 0 || p.Adults.Length > players[best].Adults.Length)) best = p.Index;
            }

            return best;
        }

        // ================================================================== money and minds

        /// <summary>
        /// The members' money ($B: sums over the adults, whose records carry their household's money split evenly) and
        /// minds (adult means). Wages and business income are attributed to the industry that pays the household: the
        /// person's own, else the employed spouse's, else the cell's industry anchor.
        /// </summary>
        static void Aggregate(Player p, PersonYear[] rec, SmvSimulation sim, EconomicLives lives, double unit, GroupsFile gf,
            int[] industryOf)
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
                int ind = industryOf[i];
                if (ind >= 0 && ind < p.WagesBy.Length)
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
                if (t != null) p.Strategy[Math.Min(6, (int)t.Strategy)] += 1;
                p.Generations[Math.Min(4, Generation(gf, sim.People[i].Birth))] += 1;
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
        }

        // ================================================================== the log line

        /// <summary>
        /// The body of "[Why] Players" (8.8): players by group, adults and children, sizes, places, the largest rim shift,
        /// the self-employed in government (3.6: 0), each group's share of adults, and three checks: every adult in one
        /// player and every child with a player; the players' wages, spending and taxes equal the lives' totals (0.1%);
        /// every player placed and no two discs of a rim row overlapping.
        /// </summary>
        static string Log(PlayerSet set, EconomicLives lives, PersonYear[] rec, List<int> adults, int kidCount,
            PlayerPlacement.Stats places, EconomyData data, int year)
        {
            Player[] ps = set.Players;
            int[] byGroup = new int[12], adultsByGroup = new int[12];
            int assigned = 0, childCount = 0;
            List<int> sizes = new List<int>(ps.Length);
            List<double> people = new List<double>(ps.Length);
            double wages = 0, spending = 0, taxes = 0;
            foreach (Player p in ps)
            {
                byGroup[(int)p.Group]++;
                adultsByGroup[(int)p.Group] += p.Adults.Length;
                assigned += p.Adults.Length;
                childCount += p.Children.Length;
                sizes.Add(p.Adults.Length);
                people.Add(p.People);
                wages += p.Wages;
                spending += p.Spending;
                taxes += p.Taxes;
            }

            sizes.Sort();
            people.Sort();
            int selfGov = 0;
            foreach (int i in adults)
            {
                int ind = rec[i].Industry;
                if (rec[i].Employed && rec[i].SelfEmployed && ind >= 0 && ind < data.Industries.Count && data.Industries[ind].TierId == "gov")
                {
                    selfGov++;
                }
            }

            int unique = 0;
            foreach (int i in adults) unique += set.PlayerOfPerson[i] >= 0 ? 1 : 0;
            PopulationYear agg = lives.Aggregate(year);
            bool Near(double a, double b) => Math.Abs(a - b) <= 0.001 * Math.Max(1, Math.Abs(b));
            bool sums = agg == null || Near(wages, agg.Wages) && Near(spending, agg.Spending) && Near(taxes, agg.Taxes);
            bool every = assigned == adults.Count && unique == adults.Count && childCount == kidCount;
            bool placed = places.Unplaced == 0 && places.Overlaps == 0;
            int pass = (every ? 1 : 0) + (sums ? 1 : 0) + (placed ? 1 : 0);

            StringBuilder s = new StringBuilder(512);
            s.Append(ps.Length).Append(" players (");
            for (int g = 0; g < 12; g++) s.Append(g > 0 ? ", " : "").Append(GroupShort[g]).Append(' ').Append(byGroup[g]);
            s.Append("); ").Append(assigned).Append(" adults + ").Append(childCount).Append(" children; median ")
                .Append(LandFacts.Millions(Median(sizes) * 1e5)).Append(" adults (")
                .Append(LandFacts.Millions(Median(people))).Append(" people), max ")
                .Append(LandFacts.Millions(people.Count > 0 ? people[people.Count - 1] : 0)).Append("; places rim ").Append(places.Rim)
                .Append(" (plinths ").Append(places.Plinths).Append("), tower ").Append(places.Tower).Append(", crown ").Append(places.Crown)
                .Append("; max rim shift ").Append(LandFacts.Num(places.MaxShift, 2)).Append(" u; self-employed in government ")
                .Append(selfGov).Append("; shares");
            for (int g = 0; g < 12; g++)
            {
                s.Append(g > 0 ? ", " : " ").Append(GroupShort[g]).Append(' ')
                    .Append(LandFacts.Num(assigned > 0 ? 100.0 * adultsByGroup[g] / assigned : 0, 1));
            }

            s.Append("; checks ").Append(pass).Append("/3 ").Append(pass == 3 ? "PASS" : "FAIL");
            if (!every) s.Append(" (assigned ").Append(assigned).Append(" of ").Append(adults.Count).Append(" adults, ")
                .Append(childCount).Append(" of ").Append(kidCount).Append(" children)");
            if (!sums && agg != null) s.Append(" (wages ").Append(LandFacts.Money(wages)).Append(" vs ").Append(LandFacts.Money(agg.Wages)).Append(')');
            if (!placed) s.Append(" (unplaced ").Append(places.Unplaced).Append(", overlaps ").Append(places.Overlaps).Append(')');
            return s.ToString();
        }

        static double Median(List<int> sorted) => sorted.Count == 0 ? 0
            : sorted.Count % 2 == 1 ? sorted[sorted.Count / 2] : 0.5 * (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]);

        static double Median(List<double> sorted) => sorted.Count == 0 ? 0
            : sorted.Count % 2 == 1 ? sorted[sorted.Count / 2] : 0.5 * (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]);
    }
}
