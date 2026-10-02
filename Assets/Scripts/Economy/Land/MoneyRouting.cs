using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Why.Economy.Data;
using Why.Economy.Model;

namespace Why.Economy.Land
{
    /// <summary>
    /// The year's money on the land (SPEC 2.7's flows, 3.5, 4.1-4.6): every player's water budget split by motive (4.1),
    /// the first recipients of each category's dollars (<see cref="SellerMatrix"/>, 4.2), the water on the ground
    /// (<see cref="CanalRouting"/>: rivulets, creeks, the lip canal, the falls, rivers and distributaries into the pools,
    /// taxes to the floor, imports over the rim), and the money in the air: income arcs from where it comes from (wage
    /// patches tiling each sector's wage strip, named employers' towers, business income from the gold strips, capital
    /// income falling from the crown, transfer fountains from the floor, borrowing through banking) and the crown's
    /// capital (payouts from every private sector by the wealth groups' mixes of <see cref="CapitalSources"/>, payouts
    /// abroad, saving rising to it, credit and investment leaving it). The identities of 4.6 are checked and logged every
    /// build ("checks 8/8 PASS", the measures on a second line). Pure: no Unity objects, any thread, deterministic.
    /// </summary>
    /// <remarks>
    /// Saving and borrowing are the members' own (a member who saves sends it to the crown; one who dissaves borrows), so a
    /// player can do both: the crown's in and out are the lives' gross flows ($2.16T saved, $0.93T borrowed in 2025), and
    /// each player's budget still closes. The players' per-category fear and fantasy dollars are re-split here by the
    /// categories' motives (4.1: fear_pc = s_pc · fs_c · k_p, clamped, the excess spread over the room left) and written
    /// back into <see cref="Player.CategoryFear"/> and <see cref="Player.CategoryFantasy"/>, so the glyphs, the inspector
    /// and the rivers agree; each player's totals are unchanged (the census already sums the members' fear and fantasy).
    /// </remarks>
    public static class MoneyRouting
    {
        /// <summary>Lane names in the log (0-5 categories, 6 taxes, 7 abroad).</summary>
        public static readonly string[] LaneNames = { "necessities", "escapism", "jeopardy", "status", "growth", "collective", "taxes", "abroad" };

        /// <summary>The Medicare share of the non-Social-Security transfers when circuit.json lacks it.</summary>
        const double MedicareShareDefault = 0.36;

        /// <summary>
        /// Builds the year's flows. The lives give each player's members' records (wealth group, gross saving); the land
        /// gives the sectors, towers and the year's width scale.
        /// </summary>
        public static MoneyFlows Build(EconomyModel model, LandGeometry land, PlayerSet players, int year)
        {
            MoneyFlows m = new MoneyFlows { Year = year };
            EconomyData data = model.Data;
            EconomicLives lives = model.Lives;
            int ni = land.Sectors.Length, np = players.Players.Length;
            const double Unit = 1e5 / 1e9;   // a line is 100,000 people; dollars in $B

            int federal = data.IndustryById("federal")?.Index ?? -1, stateLocal = data.IndustryById("state_local")?.Index ?? -1;
            int banking = data.IndustryById("banking")?.Index ?? -1;

            // ---- 4.1 each player's motives by category; the members' gross saving and borrowing, capital by wealth group
            double[] catFearShare = new double[6], catFant = new double[6];
            foreach (Category cat in data.Categories)
            {
                int c = SellerMatrix.CategoryIndex(cat);
                if (c < 0) continue;
                catFearShare[c] = cat.FearShare;
                catFant[c] = cat.Fantasy;
            }

            double[][] mix = CapitalSources.Mix(data, year);
            double[] saveGross = new double[np], borrowGross = new double[np];
            for (int p = 0; p < np; p++)
            {
                Player pl = players.Players[p];
                Split(pl.Category, pl.CategoryFear, catFearShare);
                Split(pl.Category, pl.CategoryFantasy, catFant);
                foreach (int i in pl.Adults)
                {
                    if (lives == null || !lives.TryGet(i, year, out PersonYear r)) continue;
                    if (r.Saving > 0) saveGross[p] += r.Saving * Unit;
                    else borrowGross[p] -= r.Saving * Unit;
                    double cap = r.CapitalIncome * Unit;
                    if (cap <= 0) continue;
                    double[] w = mix[Math.Min(CapitalSources.Groups - 1, (int)r.WealthGroup)];
                    for (int k = 0; k < ni && k < w.Length; k++) m.PayoutBySector[k] += cap * w[k];
                }
            }

            // ---- 4.2 the first recipients: prior once, RAS on the players' domestic category totals
            SellerMatrix.Prior prior = SellerMatrix.PriorOf(data);
            double[] catTotal = new double[6], rows = new double[6];
            foreach (Player pl in players.Players)
            {
                for (int c = 0; c < 6; c++) catTotal[c] += pl.Category[c];
            }

            for (int c = 0; c < 6; c++) rows[c] = catTotal[c] * (1 - prior.ImportShare[c]);
            SellerMatrix.Result ras = SellerMatrix.Balance(prior, rows);
            m.CategoryToSeller = ras.Shares;

            // ---- each player's outflow by lane: domestic category dollars by motive, taxes, imports
            CanalRouting.Outflow[] outflow = new CanalRouting.Outflow[np];
            for (int p = 0; p < np; p++)
            {
                Player pl = players.Players[p];
                CanalRouting.Outflow o = outflow[p] = new CanalRouting.Outflow();
                for (int c = 0; c < 6; c++)
                {
                    double imp = prior.ImportShare[c], fear = pl.CategoryFear[c], desire = pl.Category[c] - fear;
                    o.Fear[c] = fear * (1 - imp);
                    o.Desire[c] = desire * (1 - imp);
                    o.Fantasy[c] = pl.CategoryFantasy[c] * (1 - imp);
                    o.Fear[LandStyle.LaneAbroad] += fear * imp;
                    o.Desire[LandStyle.LaneAbroad] += desire * imp;
                    o.Fantasy[LandStyle.LaneAbroad] += pl.CategoryFantasy[c] * imp;
                }

                o.Plain[LandStyle.LaneTaxes] = pl.Taxes;
            }

            // ---- 4.3-4.5 the water on the ground
            double taxBase = data.Circuit.PersonalOf("personalTaxes") + data.Circuit.PersonalOf("socialInsuranceContributions");
            double slShare = taxBase > 0 ? data.Circuit.GovernmentOf("stateLocalPersonalTaxes") / taxBase : 0.13;
            CanalRouting.Report water = CanalRouting.Route(m, land, players, outflow, ras.Shares, federal, stateLocal, slShare);

            // ---- 3.5 income in the air
            Income income = Arcs(m, data, land, players, year, federal, stateLocal, banking, saveGross, borrowGross);

            // ---- 2.7 the crown
            CircuitYear circuit = model.Circuit.Build(year);
            foreach (CircuitLink l in circuit.Links) m.PayoutAbroad += l.Kind == "payout" ? l.Value : 0;
            for (int p = 0; p < np; p++)
            {
                m.SavingIn += saveGross[p];
                m.Credit += borrowGross[p];
            }

            m.Investment = m.SavingIn - m.Credit;
            double invTotal = 0;
            foreach (Industry ind in data.Industries) invTotal += Math.Max(0, FinalDemand(data, ind.Id, "investment"));
            foreach (Industry ind in data.Industries)
            {
                m.InvestmentBySector[ind.Index] = invTotal > 0 && m.Investment > 0
                    ? m.Investment * Math.Max(0, FinalDemand(data, ind.Id, "investment")) / invTotal
                    : 0;
            }

            CrownArcs(m, land, banking);

            // ---- 4.6 the identities, the log, the checksum
            double sum = 0;
            foreach (FlowPath f in m.Paths) sum += f.Dollars;
            m.Checksum = sum;
            string line = Log(m, model, land, players, year, ras, prior, water, income, saveGross, borrowGross, slShare,
                out string identities);
            m.Log = line + "; checksum " + sum.ToString("F6", LandFacts.Ci) + "\n" + identities;
            return m;
        }

        static double FinalDemand(EconomyData data, string id, string key) =>
            data.Circuit?.FinalDemand != null && data.Circuit.FinalDemand.TryGetValue(id, out Dictionary<string, double> d) &&
            d != null && d.TryGetValue(key, out double v) ? v : 0;

        /// <summary>
        /// 4.1: splits a player's motive dollars (their total is the members' own sum, already in <paramref name="part"/>)
        /// over its categories as s_c · w_c · k, k chosen so they sum to the total; a category is clamped at its own
        /// dollars and the clamped excess spread over the others in proportion to the room they have left.
        /// </summary>
        static void Split(double[] spend, double[] part, double[] weight)
        {
            double total = 0, basis = 0;
            for (int c = 0; c < 6; c++)
            {
                total += part[c];
                basis += spend[c] * weight[c];
            }

            double room = 0;
            for (int c = 0; c < 6; c++) room += spend[c];
            total = Math.Min(total, room);
            if (total <= 0 || basis <= 0)
            {
                for (int c = 0; c < 6; c++) part[c] = room > 0 ? spend[c] * total / room : 0;
                return;
            }

            double k = total / basis, placed = 0;
            for (int c = 0; c < 6; c++)
            {
                part[c] = Math.Min(spend[c], spend[c] * weight[c] * k);
                placed += part[c];
            }

            double excess = total - placed, left = 0;
            for (int c = 0; c < 6; c++) left += spend[c] - part[c];
            if (excess <= 1e-12 || left <= 0) return;
            for (int c = 0; c < 6; c++) part[c] += excess * (spend[c] - part[c]) / left;
        }

        // ================================================================== 3.5 income arcs

        /// <summary>The air's sums for the log: wages by patch and tower, the model's wages by industry.</summary>
        sealed class Income
        {
            public double Patches, Towers, Wages, Business, Capital, Transfers, Borrowing, Saving;
            public readonly double[] ModelWages = new double[25];
            public int Arcs, TowerArcs;
        }

        /// <summary>
        /// Every income arc: wage patches and their arcs, named employers' tower arcs (Y ≥ 2024), business income from
        /// the gold strips, capital income from the crown, transfers from the floor, borrowing through banking, saving to
        /// the crown. An arc below $5B merges into the player's largest arc of the same kind; nothing is dropped.
        /// </summary>
        static Income Arcs(MoneyFlows m, EconomyData data, LandGeometry land, PlayerSet players, int year, int federal,
            int stateLocal, int banking, double[] saveGross, double[] borrowGross)
        {
            Income inc = new Income();
            int ni = land.Sectors.Length;
            double min = LandStyle.StreamMinB;

            // named employers: each tower's share of its industry's 2024 employment
            List<TowerGeom>[] towersOf = new List<TowerGeom>[ni];
            double[] towerShare = new double[land.Towers?.Length ?? 0];
            if (land.Towers != null)
            {
                for (int j = 0; j < land.Towers.Length; j++)
                {
                    TowerGeom t = land.Towers[j];
                    if (t.Industry < 0 || t.Industry >= ni) continue;
                    Company co = t.Company >= 0 && t.Company < (data.Circuit?.Capture?.Count ?? 0) ? data.Circuit.Capture[t.Company] : null;
                    double emp = data.Industries[t.Industry].Employment.At(LandStyle.LayoutYear);
                    towerShare[j] = co != null && emp > 0 ? Math.Max(0, co.UsEmployees) / emp : 0;
                    (towersOf[t.Industry] ??= new List<TowerGeom>()).Add(t);
                }
            }

            // ---- wages: (player, industry) patch dollars after the towers' kept arcs
            List<Patch> patches = new List<Patch>();
            double[,] patchDollars = new double[players.Players.Length, ni];
            foreach (Player p in players.Players)
            {
                Vector3 disc = Figure.Center(p);
                int largest = -1;
                for (int k = 0; k < ni; k++)
                {
                    double w = p.WagesBy[k];
                    inc.ModelWages[k] += w;
                    if (w <= 0) continue;
                    double patch = w;
                    if (towersOf[k] != null)
                    {
                        foreach (TowerGeom t in towersOf[k])
                        {
                            double share = towerShare[Array.IndexOf(land.Towers, t)];
                            double v = w * share;
                            if (v < min) continue;
                            patch -= v;
                            FlowPath f = Add(m, FlowKind.WagesCompany, t.Company, p.Index,
                                Arc(LandFrame.Polar(t.R, t.Theta, t.BaseY), disc, false), v);
                            inc.Towers += v;
                            inc.TowerArcs++;
                            f.Lane = k;
                        }
                    }

                    patchDollars[p.Index, k] = patch;
                    if (largest < 0 || patch > patchDollars[p.Index, largest]) largest = k;
                }

                if (largest < 0) continue;
                for (int k = 0; k < ni; k++)
                {
                    if (k == largest || patchDollars[p.Index, k] <= 0 || patchDollars[p.Index, k] >= min) continue;
                    patchDollars[p.Index, largest] += patchDollars[p.Index, k];
                    patchDollars[p.Index, k] = 0;
                }
            }

            // tile each sector's wage strip with its patches, in the order of their players' angles around the sector
            foreach (SectorGeom s in land.Sectors)
            {
                int k = s.Industry;
                List<Player> here = new List<Player>();
                double total = 0;
                foreach (Player p in players.Players)
                {
                    if (patchDollars[p.Index, k] <= 0) continue;
                    here.Add(p);
                    total += patchDollars[p.Index, k];
                }

                if (total <= 0) continue;
                here.Sort((a, b) =>
                {
                    float da = LandMath.DeltaDeg(s.Mid, a.Theta), db = LandMath.DeltaDeg(s.Mid, b.Theta);
                    return da != db ? da.CompareTo(db) : a.Index.CompareTo(b.Index);
                });
                double at = s.Theta0, span = s.Theta1 - s.Theta0;
                foreach (Player p in here)
                {
                    double v = patchDollars[p.Index, k], w = span * v / total;
                    Patch patch = new Patch
                    {
                        Industry = k, Player = p.Index, Company = -1, Theta0 = (float)at, Theta1 = (float)(at + w), R0 = s.R0,
                        R1 = s.RWages, Dollars = v
                    };
                    patches.Add(patch);
                    Vector3 from = LandFrame.Polar(0.5f * (s.R0 + s.RWages) + (s.R0 <= 0 ? 0.15f * s.RWages : 0),
                        0.5f * (patch.Theta0 + patch.Theta1), s.Y + 0.004f);
                    FlowPath f = Add(m, FlowKind.Wages, k, p.Index, Arc(from, Figure.Center(p), false), v);
                    f.Lane = k;
                    inc.Patches += v;
                    at += w;
                }
            }

            m.Patches = patches.ToArray();

            foreach (Player p in players.Players)
            {
                Vector3 disc = Figure.Center(p);
                inc.Wages += p.Wages;

                // ---- business income: from the gold strip at the player's angle
                double[] biz = new double[ni];
                for (int k = 0; k < ni; k++) biz[k] = Math.Max(0, p.BusinessBy[k]);
                MergeSmall(biz, min);
                for (int k = 0; k < ni; k++)
                {
                    if (biz[k] <= 0) continue;
                    SectorGeom s = land.Sectors[k];
                    Vector3 from = LandFrame.Polar(0.5f * (s.RUpkeep + s.R1), Clamp(p.Theta, s), s.Y + 0.004f);
                    Add(m, FlowKind.Business, k, p.Index, Arc(from, disc, false), biz[k]).Lane = k;
                    inc.Business += biz[k];
                }

                // business income the census could not place by industry
                double placed = 0;
                for (int k = 0; k < ni; k++) placed += biz[k];
                if (p.Business - placed > 1e-6 && p.AngleIndustry >= 0)
                {
                    SectorGeom s = land.Sectors[p.AngleIndustry];
                    double v = p.Business - placed;
                    Add(m, FlowKind.Business, s.Industry, p.Index, Arc(LandFrame.Polar(0.5f * (s.RUpkeep + s.R1), Clamp(p.Theta, s), s.Y + 0.004f), disc, false), v);
                    inc.Business += v;
                }

                // ---- capital income falls from the crown
                if (p.Capital > 0)
                {
                    Add(m, FlowKind.Capital, -2, p.Index, Arc(CrownAt(p.Theta), disc, true), p.Capital);
                    inc.Capital += p.Capital;
                }

                // ---- transfers: Social Security and Medicare's share from the federal wedge, the rest from state & local
                if (p.Transfers > 0 && federal >= 0)
                {
                    double nonSs = Math.Max(0, p.Transfers - p.SocialSecurity);
                    double medicare = MedicareShare(data);
                    double fed = Math.Min(p.Transfers, p.SocialSecurity + medicare * nonSs), sl = p.Transfers - fed;
                    if (stateLocal < 0 || sl < min && fed >= sl)
                    {
                        fed += sl;
                        sl = 0;
                    }
                    else if (fed < min && sl > fed)
                    {
                        sl += fed;
                        fed = 0;
                    }

                    foreach ((int k, double v) in new[] { (federal, fed), (stateLocal, sl) })
                    {
                        if (v <= 0 || k < 0) continue;
                        SectorGeom s = land.Sectors[k];
                        Add(m, FlowKind.Transfers, k, p.Index, Arc(LandFrame.Polar(0.6f * s.R1, Clamp(p.Theta, s), s.Y + 0.004f), disc, false), v);
                        inc.Transfers += v;
                    }
                }

                // ---- saving rises to the crown; borrowing comes through banking (dashed)
                if (saveGross[p.Index] > 0)
                {
                    Add(m, FlowKind.Saving, p.Index, -2, Arc(disc, CrownAt(p.Theta), false), saveGross[p.Index]);
                    inc.Saving += saveGross[p.Index];
                }

                if (borrowGross[p.Index] > 0 && banking >= 0)
                {
                    SectorGeom b = land.Sectors[banking];
                    FlowPath f = Add(m, FlowKind.Borrowing, banking, p.Index,
                        Arc(LandFrame.Polar(0.5f * (b.RUpkeep + b.R1), Clamp(p.Theta, b), b.Y + 0.004f), disc, false), borrowGross[p.Index]);
                    f.Dashed = true;
                    inc.Borrowing += borrowGross[p.Index];
                }
            }

            foreach (FlowPath f in m.Paths) inc.Arcs += f.Kind <= FlowKind.Borrowing ? 1 : 0;
            return inc;
        }

        /// <summary>The Medicare share of non-Social-Security transfers: personal.medicare / (transfers − socialSecurity).</summary>
        static double MedicareShare(EconomyData data)
        {
            double t = data.Circuit.PersonalOf("transfers"), ss = data.Circuit.PersonalOf("socialSecurity"), med = data.Circuit.PersonalOf("medicare");
            return t - ss > 0 && med > 0 ? Math.Min(1, med / (t - ss)) : MedicareShareDefault;
        }

        /// <summary>Amounts below the minimum merge into the largest (which keeps them even when it is itself below it).</summary>
        static void MergeSmall(double[] v, double min)
        {
            int largest = -1;
            for (int k = 0; k < v.Length; k++)
            {
                if (v[k] > 0 && (largest < 0 || v[k] > v[largest])) largest = k;
            }

            if (largest < 0) return;
            for (int k = 0; k < v.Length; k++)
            {
                if (k == largest || v[k] <= 0 || v[k] >= min) continue;
                v[largest] += v[k];
                v[k] = 0;
            }
        }

        /// <summary>An angle clamped into a sector's span, kept a tenth of the span inside its ends.</summary>
        static float Clamp(float theta, SectorGeom s)
        {
            float span = s.Theta1 - s.Theta0, d = LandMath.DeltaDeg(s.Mid, theta);
            float half = 0.4f * span;
            return s.Mid + Mathf.Clamp(d, -half, half);
        }

        /// <summary>The crown ring's point at an angle.</summary>
        public static Vector3 CrownAt(float theta) => LandFrame.Polar(LandStyle.CrownR, theta, LandStyle.CrownY);

        // ================================================================== 2.7 the crown's capital

        /// <summary>Payouts from every sector's gold strip, investment to the sectors, credit to banking, payouts abroad.</summary>
        static void CrownArcs(MoneyFlows m, LandGeometry land, int banking)
        {
            for (int k = 0; k < land.Sectors.Length; k++)
            {
                SectorGeom s = land.Sectors[k];
                Vector3 owners = LandFrame.Polar(0.5f * (s.RUpkeep + s.R1), s.Mid, s.Y + 0.004f);
                if (m.PayoutBySector[k] > 0) Add(m, FlowKind.Payout, k, -2, Arc(owners, CrownAt(s.Mid), false), m.PayoutBySector[k]);
                if (m.InvestmentBySector[k] > 0) Add(m, FlowKind.Investment, -2, k, Arc(CrownAt(s.Mid), owners, true), m.InvestmentBySector[k]);
            }

            if (m.PayoutAbroad > 0)
            {
                Add(m, FlowKind.PayoutAbroad, -2, -3, Arc(CrownAt(90f), LandFrame.Polar(LandStyle.RimR + 1.2f, 90f, LandStyle.RimY), false), m.PayoutAbroad);
            }

            if (m.Credit > 0 && banking >= 0)
            {
                SectorGeom b = land.Sectors[banking];
                Add(m, FlowKind.Credit, -2, banking, Arc(CrownAt(b.Mid), LandFrame.Polar(0.5f * (b.RUpkeep + b.R1), b.Mid, b.Y + 0.004f), true), m.Credit);
            }
        }

        /// <summary>
        /// An arc in the air (3.5): a quadratic Bézier whose apex stands 0.25 + 0.08 × planar distance above the higher
        /// end, or, from the crown, sagging (control = midpoint − 0.2); 20 points.
        /// </summary>
        public static Vector3[] Arc(Vector3 a, Vector3 b, bool sag)
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
            FlowPath f = new FlowPath { Kind = kind, From = from, To = to, Points = points, Dollars = dollars };
            m.Paths.Add(f);
            return f;
        }

        // ================================================================== 4.6 identities and the log

        /// <summary>
        /// The Money line (8.8) and the identities of 4.6: 1 each player's in = out (≤ 0.5%), 2 the players' totals = the
        /// lives' (≤ 0.1%), 3 every junction (≤ $0.01B), 4 pools + abroad = spending (≤ 0.1%, and every river ends in its
        /// pool), 5 monotone beds, 6 the crown (≤ 0.5%), 7 the tax river and the floor split (≤ 0.1%), 8 the land's areas
        /// (24 u², ring fills ≤ 1). The measures go on a second line ("[Why] Money identities ...").
        /// </summary>
        static string Log(MoneyFlows m, EconomyModel model, LandGeometry land, PlayerSet players, int year, SellerMatrix.Result ras,
            SellerMatrix.Prior prior, CanalRouting.Report water, Income inc, double[] saveGross, double[] borrowGross, double slShare,
            out string identities)
        {
            EconomyData data = model.Data;
            double wages = 0, business = 0, capital = 0, transfers = 0, taxes = 0, spending = 0, net = 0, fear = 0, fant = 0, cats = 0;
            double worst = 0;
            foreach (Player p in players.Players)
            {
                wages += p.Wages;
                business += p.Business;
                capital += p.Capital;
                transfers += p.Transfers;
                taxes += p.Taxes;
                spending += p.Spending;
                net += p.Saving;
                for (int c = 0; c < 6; c++)
                {
                    fear += p.CategoryFear[c];
                    fant += p.CategoryFantasy[c];
                    cats += p.Category[c];
                }

                double income = p.Wages + p.Business + p.Capital + p.Transfers + borrowGross[p.Index];
                double outflow = p.Taxes + p.Spending + saveGross[p.Index];
                if (income > 0) worst = Math.Max(worst, Math.Abs(income - outflow) / income);
            }

            double saving = 0, borrowing = 0;
            foreach (double v in saveGross) saving += v;
            foreach (double v in borrowGross) borrowing += v;

            // 2: the lives' national totals (every adult alive in the year)
            Lives(model, year, out double lw, out double lb, out double lc, out double lt, out double ltx, out double ls, out double lsv);
            double t2 = Math.Max(Math.Max(Math.Max(Rel(wages, lw), Rel(business, lb)), Math.Max(Rel(capital, lc), Rel(transfers, lt))),
                Math.Max(Math.Max(Rel(taxes, ltx), Rel(spending, ls)), Math.Abs(net - lsv) / Math.Max(1, Math.Abs(ls))));

            // 4: pools + abroad = spending; every river ends in its pool
            double pools = 0;
            foreach (double v in m.PoolInflow) pools += v;
            double t4 = Rel(pools + m.Imports, spending), poolGap = 0;
            for (int k = 0; k < land.Sectors.Length; k++)
            {
                double delivered = 0;
                for (int c = 0; c < 6; c++) delivered += water.Delivered[c][k];
                poolGap = Math.Max(poolGap, Math.Abs(delivered - m.PoolInflow[k]));
            }

            // 6: the crown
            double payouts = 0;
            foreach (double v in m.PayoutBySector) payouts += v;
            double crownIn = payouts + m.PayoutAbroad + m.SavingIn, crownOut = capital + m.PayoutAbroad + m.Credit + m.Investment;
            double t6 = Rel(crownIn, crownOut);

            // 7: the tax river and the floor split
            double taxRiver = 0, floorFed = 0, floorSl = 0;
            int federal = data.IndustryById("federal")?.Index ?? -1;
            foreach (FlowPath f in m.Paths)
            {
                if (f.Kind != FlowKind.Taxes) continue;
                if (f.Points != null && f.Points.Length > 0 && f.Points[0].y >= LandStyle.CanalY - 1e-3f) taxRiver = Math.Max(taxRiver, f.Dollars);
            }

            // the floor's split: what the tax river delivered to each wedge
            int stateLocal = data.IndustryById("state_local")?.Index ?? -1;
            if (federal >= 0) floorFed = water.Delivered[LandStyle.LaneTaxes][federal];
            if (stateLocal >= 0) floorSl = water.Delivered[LandStyle.LaneTaxes][stateLocal];
            double t7 = Math.Max(Math.Max(Rel(taxRiver, taxes), Rel(floorFed + floorSl, taxes)), Math.Abs(floorSl / Math.Max(1e-9, taxes) - slShare));

            // 8: the land's areas
            double area = 0;
            foreach (SectorGeom s in land.Sectors) area += s.ValueAdded * land.AreaPerB;
            float fillMax = 0;
            foreach (float f in land.RingFill) fillMax = Mathf.Max(fillMax, f);
            double t8 = Math.Abs(area - LandStyle.AreaGdp);

            bool[] ok =
            {
                worst <= 0.005, t2 <= 0.001, water.JunctionError <= 0.01, t4 <= 0.001 && poolGap <= 0.01 && water.PoolEndsInside == water.PoolEnds,
                water.Uphill == 0, t6 <= 0.005, t7 <= 0.001, t8 <= 0.01 && fillMax <= 1
            };
            int pass = 0;
            StringBuilder failed = new StringBuilder();
            for (int k = 0; k < ok.Length; k++)
            {
                if (ok[k]) pass++;
                else failed.Append(failed.Length > 0 ? "," : " (failing ").Append(k + 1);
            }

            if (failed.Length > 0) failed.Append(')');

            // model wages against BEA by industry: the extremes
            int lo = -1, hi = -1;
            double loR = double.MaxValue, hiR = 0;
            foreach (SectorGeom s in land.Sectors)
            {
                if (s.Wages <= 0 || inc.ModelWages[s.Industry] <= 0) continue;
                double r = inc.ModelWages[s.Industry] / s.Wages;
                if (r < loR)
                {
                    loR = r;
                    lo = s.Industry;
                }

                if (r > hiR)
                {
                    hiR = r;
                    hi = s.Industry;
                }
            }

            StringBuilder b = new StringBuilder(900);
            b.Append("wages ").Append(LandFacts.Money(wages)).Append(" (patches ").Append(LandFacts.Money(inc.Patches)).Append(", towers ")
                .Append(LandFacts.Money(inc.Towers)).Append(" in ").Append(inc.TowerArcs).Append(" arcs; model/BEA by industry ");
            if (lo >= 0) b.Append(data.Industries[lo].Id).Append(' ').Append(LandFacts.Num(loR, 2)).Append(" .. ").Append(data.Industries[hi].Id).Append(' ').Append(LandFacts.Num(hiR, 2));
            b.Append("), business ").Append(LandFacts.Money(business)).Append(", capital ").Append(LandFacts.Money(capital))
                .Append(", transfers ").Append(LandFacts.Money(transfers)).Append(", taxes ").Append(LandFacts.Money(taxes))
                .Append(" (federal ").Append(LandFacts.Percent(1 - slShare)).Append("), spending ").Append(LandFacts.Money(spending))
                .Append(" (abroad ").Append(LandFacts.Money(m.Imports)).Append("), saving ").Append(LandFacts.Money(saving))
                .Append(", borrowing ").Append(LandFacts.Money(borrowing)).Append("; fear ").Append(LandFacts.Percent(fear / Math.Max(1e-9, cats)))
                .Append(", fantasy ").Append(LandFacts.Percent(fant / Math.Max(1e-9, cats))).Append("; crown in ").Append(LandFacts.Money(crownIn))
                .Append(" out ").Append(LandFacts.Money(crownOut)).Append("; RAS ").Append(ras.Iterations).Append(" it, error ")
                .Append(LandFacts.Percent(ras.Error, 1)).Append(", flagged");
            if (ras.Flagged.Count == 0) b.Append(" none");
            foreach ((int k, double f) in ras.Flagged)
            {
                b.Append(' ').Append(data.Industries[k].Id).Append(' ').Append(f >= 10 ? LandFacts.Num(f, 1) : LandFacts.Num(f, 2));
            }

            b.Append("; falls");
            for (int l = 0; l < 8; l++) b.Append(' ').Append(LaneNames[l]).Append(' ').Append(Mathf.RoundToInt(m.Fall[l]).ToString(LandFacts.Ci));
            b.Append("; canal max ").Append(LandFacts.Num(water.CanalMax, 2)).Append(" u; uphill points ").Append(water.Uphill)
                .Append("; largest player residual ").Append(LandFacts.Percent(worst, 1)).Append("; checks ").Append(pass).Append("/8 ")
                .Append(pass == 8 ? "PASS" : "FAIL").Append(failed);

            // the identities' measures, and what else the build measured
            StringBuilder d = new StringBuilder(700);
            d.Append("[Why] Money identities ").Append(year.ToString(LandFacts.Ci)).Append(": 1 player in=out max ")
                .Append(LandFacts.Percent(worst, 3)).Append("; 2 totals vs lives max ").Append(LandFacts.Percent(t2, 3))
                .Append(" (lives wages ").Append(LandFacts.Money(lw)).Append(", spending ").Append(LandFacts.Money(ls)).Append(", net saving ")
                .Append(LandFacts.Money(lsv)).Append("); 3 junctions ").Append(water.Junctions).Append(", max |in-out| $")
                .Append(LandFacts.Num(water.JunctionError, 4)).Append("B").Append(water.JunctionError > 0 ? " (" + water.WorstJunction + ")" : "")
                .Append("; 4 pools ").Append(LandFacts.Money(pools)).Append(" + abroad ").Append(LandFacts.Money(m.Imports)).Append(" vs spending ")
                .Append(LandFacts.Money(spending)).Append(" (categories ").Append(LandFacts.Money(cats)).Append("), off ").Append(LandFacts.Percent(t4, 3))
                .Append(", rivers deliver the pools within $").Append(LandFacts.Num(poolGap, 4)).Append("B, river ends in their pools ").Append(water.PoolEndsInside).Append('/').Append(water.PoolEnds).Append("; 5 uphill ")
                .Append(water.Uphill).Append("; 6 crown in ").Append(LandFacts.Money(crownIn)).Append(" (payouts ").Append(LandFacts.Money(payouts))
                .Append(", abroad ").Append(LandFacts.Money(m.PayoutAbroad)).Append(", saving ").Append(LandFacts.Money(m.SavingIn)).Append(") out ")
                .Append(LandFacts.Money(crownOut)).Append(" (capital ").Append(LandFacts.Money(capital)).Append(", credit ").Append(LandFacts.Money(m.Credit))
                .Append(", investment ").Append(LandFacts.Money(m.Investment)).Append("), off ").Append(LandFacts.Percent(t6, 3)).Append("; 7 tax river ")
                .Append(LandFacts.Money(taxRiver)).Append(", floor federal ").Append(LandFacts.Money(floorFed)).Append(" state_local ")
                .Append(LandFacts.Money(floorSl)).Append("; 8 areas ").Append(LandFacts.Num(area, 3)).Append(" u2, fill max ")
                .Append(LandFacts.Percent(fillMax)).Append(" | prior clipped ").Append(LandFacts.Percent(prior.Clipped, 1)).Append("; heads ")
                .Append(water.Heads).Append(", merged ").Append(water.Merged).Append("; canal mean ").Append(LandFacts.Num(water.CanalMean, 3))
                .Append(" u, widest at ").Append(water.CanalMaxAt).Append(" deg (").Append(CanalAt(m, water.CanalMaxAt)).Append("); distributaries ").Append(water.Segments).Append(", stubs ")
                .Append(water.Stubs).Append(", joined below the lowest kept ring ").Append(LandFacts.Money(water.Joined)).Append("; arcs ")
                .Append(inc.Arcs).Append(" (").Append(ArcCounts(m)).Append("); payouts");
            int[] top = Top(m.PayoutBySector, 5);
            foreach (int k in top) d.Append(' ').Append(data.Industries[k].Id).Append(' ').Append(LandFacts.Money(m.PayoutBySector[k]));
            identities = d.ToString();
            return b.ToString();
        }

        /// <summary>The canal's lanes at a degree: "necessities $1.55T, ..." (fear + desire + plain).</summary>
        static string CanalAt(MoneyFlows m, int phi)
        {
            StringBuilder b = new StringBuilder();
            for (int l = 0; l < 8; l++)
            {
                CanalLane lane = m.Canal[l];
                if (b.Length > 0) b.Append(", ");
                b.Append(LaneNames[l]).Append(' ').Append(LandFacts.Money(lane.Fear[phi] + lane.Desire[phi] + lane.Plain[phi]));
            }

            return b.ToString();
        }

        /// <summary>Arcs by kind: "wages 180, employers 27, ...".</summary>
        static string ArcCounts(MoneyFlows m)
        {
            int[] n = new int[(int)FlowKind.Borrowing + 1];
            foreach (FlowPath f in m.Paths)
            {
                if (f.Kind <= FlowKind.Borrowing) n[(int)f.Kind]++;
            }

            StringBuilder b = new StringBuilder();
            for (int k = 0; k < n.Length; k++)
            {
                if (n[k] == 0) continue;
                if (b.Length > 0) b.Append(", ");
                b.Append(((FlowKind)k).ToString().ToLowerInvariant()).Append(' ').Append(n[k]);
            }

            return b.ToString();
        }

        static int[] Top(double[] v, int n)
        {
            List<int> idx = new List<int>();
            for (int k = 0; k < v.Length; k++) idx.Add(k);
            idx.Sort((a, b) => v[a] != v[b] ? v[b].CompareTo(v[a]) : a.CompareTo(b));
            return idx.GetRange(0, Math.Min(n, idx.Count)).ToArray();
        }

        static double Rel(double a, double b) => Math.Abs(a - b) / Math.Max(1e-9, Math.Abs(b));

        /// <summary>The lives' national totals of a year ($B): every adult alive.</summary>
        static void Lives(EconomyModel model, int year, out double wages, out double business, out double capital, out double transfers,
            out double taxes, out double spending, out double saving)
        {
            wages = business = capital = transfers = taxes = spending = saving = 0;
            EconomicLives lives = model.Lives;
            if (lives?.Sim == null) return;
            const double Unit = 1e5 / 1e9;
            int n = lives.Sim.People.Count;
            for (int i = 0; i < n; i++)
            {
                if (!lives.TryGet(i, year, out PersonYear r) || !r.Adult) continue;
                wages += r.Wages * Unit;
                business += r.Business * Unit;
                capital += r.CapitalIncome * Unit;
                transfers += r.Transfers * Unit;
                taxes += r.Taxes * Unit;
                spending += r.Spending * Unit;
                saving += r.Saving * Unit;
            }
        }
    }
}
