using System;
using System.Collections.Generic;
using Why.Economy.Data;

namespace Why.Economy.Model
{
    /// <summary>
    /// The viewer's own place in the year's economy ("You" panel and view): from a few numbers about their household it
    /// finds where they would stand among the simulated population of the shown year and reads off what the model knows
    /// about people standing there.
    /// <list type="bullet">
    /// <item><b>Altitude.</b> The household income percentile (the rank that sets a rung on the hill: the model's
    /// households ranked by market income plus Social Security, as <see cref="PersonYear.IncomeRank"/>) and the net worth
    /// percentile among adults (couples split evenly, as the records are), so the viewer's rung and wealth group are the
    /// model's own.</item>
    /// <item><b>Your dollar.</b> The spending mix, fear share and saving of the model's adults at the same income rank;
    /// each category's dollar split by <see cref="ValueCapture"/> (the Leontief inverse of the BEA use table and each
    /// industry's accounts) into wages, owners' surplus (with the part Jev judged persuaded), taxes, upkeep and imports;
    /// and the share of all wages and income from ownership in the simulated population that people ranked above the
    /// viewer receive (uphill), weighted by that split. No purchase is traced: the viewer's spending is assumed to pay
    /// people the way everyone's does (a dollar's wages like all wages, its surplus like all income from ownership).</item>
    /// <item><b>Two distances.</b> The cushion: years of spending the net worth would cover, counting the home (a few
    /// numbers about a household cannot separate the financial assets the model's own three-year rule counts). And
    /// independence: living on what one owns takes 25 times a year's spending (a 4% yield), reached in some years at the
    /// viewer's saving and a 4% real return.</item>
    /// <item><b>Odds.</b> What happened over the last decade to adults who stood in the same fifth at the same age
    /// (<see cref="Mobility"/>, every adult): the shares who climbed, stayed and fell, and the fifth each of them ended in,
    /// with the simulated adults behind them and a 95% range, and what came with the climbs and falls.</item>
    /// </list>
    /// Pure: reads the finished lives on any thread once they are ready. Inputs are the shown year's nominal dollars. The
    /// placement compares a real household with synthetic records calibrated to national totals; it is a reading of the
    /// model, not financial advice.
    /// </summary>
    public sealed class YouModel
    {
        /// <summary>What the viewer says about their household (nominal dollars of the shown year).</summary>
        public struct Inputs
        {
            public int Age;
            /// <summary>Household income before taxes: wages, business, investment income and Social Security ($ a year).</summary>
            public double Income;
            /// <summary>Household net worth (what it owns minus what it owes) and its debt (mortgage and other), $.</summary>
            public double NetWorth, Debt;
            /// <summary>Lives with a spouse or partner (the household's figures are split in two, as the records are).</summary>
            public bool Partnered, Homeowner, Owner;
            /// <summary>The industry the viewer works in (EconomyData.Industries), -1 for none.</summary>
            public int Industry;
            /// <summary>Share of after-tax income saved, or NaN for the typical saving at the viewer's rank.</summary>
            public double Saving;
        }

        /// <summary>The reading.</summary>
        public sealed class Result
        {
            public int Year;
            /// <summary>Household income percentile (0..1) and net worth percentile among adults (0..1).</summary>
            public double IncomeRank, WealthRank;
            /// <summary>Income fifth (0..4), wealth group (0 bottom 50% .. 3 top 1%), age band (Mobility.AgeBands).</summary>
            public int Quintile, WealthGroup, AgeBand;
            /// <summary>Adults the reading rests on (neighbors in rank), and the medians of the year (household income, net worth per adult).</summary>
            public int Neighbors; public double MedianIncome, MedianWealth;
            /// <summary>The industry most of the employed neighbors work in (-1: none employed): the panel's first guess.</summary>
            public int ModalIndustry = -1;
            /// <summary>The neighbors' spending mix (EconomyData.Categories order), fear and fantasy shares of spending,
            /// saving rate (of after-tax income) and debt service.</summary>
            public readonly double[] Mix = new double[6];
            public double Fear, Fantasy, TypicalSaving, DebtService;
            /// <summary>The saving used (the viewer's, else typical), after-tax income (the neighbors' tax rate) and spending, $.</summary>
            public double Saving, Disposable, Spending;
            /// <summary>Of a dollar of the viewer's spending: wages, taxes, upkeep, owners' surplus (captured by others),
            /// imports; the persuaded share of the owners' part; the fear share of what is bought.</summary>
            public double Wages, Taxes, Upkeep, Owners, Imports, Persuaded, SoldFear;
            /// <summary>Of all wages and income from ownership in the simulated population: the share received by people
            /// ranked above the viewer, weighted by the dollar's wages and owners' parts (no purchase is traced); and the
            /// share of income from ownership held by the top 1% by wealth.</summary>
            public double Uphill, OwnersToTop1;
            /// <summary>A year's owners' surplus inside the viewer's spending, $; income from what they own at 4%, $.</summary>
            public double PaysOwners, OwnedIncome;
            /// <summary>The cushion: years of spending the net worth covers, the home counted; the capital to live on at 4%
            /// (25x spending); years to reach it at the saving and a 4% real return (NaN: never at this saving; 0: already).</summary>
            public double Cushion, Independence, YearsToIndependence;
            /// <summary>Odds over the last decade for the same fifth and age band (up, stay, down), the simulated lines
            /// behind them, and whether too few stood at the viewer's age, so the fifth's odds at every age stand in.</summary>
            public double Up, Stay, Down; public int OddsLines; public bool OddsAllAges;
            /// <summary>Of the same lines, the share in each fifth a decade later (Mobility.QuintileNames order): Up, Stay and
            /// Down are its sums above, at and below the viewer's fifth.</summary>
            public readonly double[] Reached = new double[Mobility.Quintiles];

            /// <summary>Half the 95% range of one of the odds (a share of <see cref="OddsLines"/> simulated lines; NaN
            /// without lines).</summary>
            public double Margin(double share) => OddsLines > 0 ? 1.96 * Math.Sqrt(Math.Max(0, share * (1 - share)) / OddsLines) : double.NaN;
        }

        /// <summary>Real return assumed for the path to independence, and the yield that sets it (4%: 25x spending).</summary>
        public const double RealReturn = .04, Yield = .04;

        readonly int year;
        /// <summary>Adults of the year: household income (market + Social Security, the household's) with their rank;
        /// net worth per adult, sorted; wages and income from ownership by rank and wealth group.</summary>
        readonly double[] incomeKey; readonly float[] incomeRank;
        readonly double[] wealthSorted;
        readonly List<(float rank, PersonYear r)> adults = new List<(float, PersonYear)>();
        readonly double medianIncome, medianWealth;
        /// <summary>Wages and income from ownership received by adults ranked at or below each hundredth of rank (cumulative).</summary>
        readonly double[] wagesBelow = new double[101], ownedBelow = new double[101];
        double ownedTop1, ownedAll;

        YouModel(int year) { this.year = year; incomeKey = Array.Empty<double>(); incomeRank = Array.Empty<float>(); wealthSorted = Array.Empty<double>(); }

        YouModel(int year, List<(double key, float rank)> income, List<double> wealth, List<(float rank, PersonYear r)> adults)
        {
            this.year = year;
            income.Sort((a, b) => a.key.CompareTo(b.key));
            incomeKey = new double[income.Count]; incomeRank = new float[income.Count];
            for (int i = 0; i < income.Count; i++) { incomeKey[i] = income[i].key; incomeRank[i] = income[i].rank; }
            wealth.Sort(); wealthSorted = wealth.ToArray();
            this.adults = adults;
            medianIncome = incomeKey.Length > 0 ? incomeKey[incomeKey.Length / 2] : 0;
            medianWealth = wealthSorted.Length > 0 ? wealthSorted[wealthSorted.Length / 2] : 0;
            var wagesAt = new double[101]; var ownedAt = new double[101];
            foreach (var (rank, r) in adults)
            {
                int k = Math.Max(0, Math.Min(100, (int)(rank * 100)));
                wagesAt[k] += Math.Max(0, r.Wages); double owned = Math.Max(0, r.CapitalIncome) + Math.Max(0, r.Business);
                ownedAt[k] += owned; ownedAll += owned; if (r.WealthGroup >= 3) ownedTop1 += owned;
            }
            double w = 0, o = 0;
            for (int k = 0; k <= 100; k++) { w += wagesAt[k]; o += ownedAt[k]; wagesBelow[k] = w; ownedBelow[k] = o; }
        }

        /// <summary>The year's model (null when the lives were not simulated).</summary>
        public static YouModel Build(EconomicLives lives, int year)
        {
            if (lives == null || !lives.Ready || lives.Sim == null) return null;
            year = Math.Max(lives.FirstYear, Math.Min(lives.LastYear, year));
            var income = new List<(double, float)>(); var wealth = new List<double>(); var adults = new List<(float, PersonYear)>();
            for (int i = 0; i < lives.Sim.People.Count; i++)
            {
                if (!lives.TryGet(i, year, out PersonYear r) || !r.Adult) continue;
                int members = r.Married ? 2 : 1;
                // the household's market income and Social Security (a couple's own benefits taken as twice this one's)
                double key = (Math.Max(0, r.Wages) + Math.Max(0, r.Business) + Math.Max(0, r.CapitalIncome) + Math.Max(0, r.SocialSecurity)) * members;
                income.Add((key, r.IncomeRank)); wealth.Add(r.Wealth); adults.Add((r.IncomeRank, r));
            }
            return adults.Count == 0 ? new YouModel(year) : new YouModel(year, income, wealth, adults);
        }

        /// <summary>The year's median household income and net worth per adult (the panel's starting values).</summary>
        public double MedianIncome => medianIncome;
        public double MedianWealth => medianWealth;
        public int Year => year;

        /// <summary>Reads the viewer's place. <paramref name="capture"/> and <paramref name="mobility"/> may be null.</summary>
        public Result Read(Inputs you, EconomyData data, ValueCapture capture, Mobility mobility)
        {
            var res = new Result { Year = year, MedianIncome = medianIncome, MedianWealth = medianWealth };
            if (incomeKey.Length == 0) return res;
            // Income rank: the model's own rank at the viewer's household income (the median rank of the records whose
            // household income is nearest, so it matches how the hill places people).
            int at = Lower(incomeKey, you.Income);
            int from = Math.Max(0, at - 6), to = Math.Min(incomeKey.Length, at + 6);
            var near = new List<float>(); for (int k = from; k < to; k++) near.Add(incomeRank[k]);
            near.Sort(); res.IncomeRank = near.Count > 0 ? near[near.Count / 2] : at / (double)incomeKey.Length;
            if (at == 0 && you.Income <= incomeKey[0]) res.IncomeRank = Math.Min(res.IncomeRank, .5 / incomeKey.Length);
            if (at >= incomeKey.Length) res.IncomeRank = 1 - .5 / incomeKey.Length;
            double perAdult = you.NetWorth / (you.Partnered ? 2 : 1);
            res.WealthRank = Lower(wealthSorted, perAdult) / (double)Math.Max(1, wealthSorted.Length);
            res.Quintile = Mobility.Quintile((float)res.IncomeRank);
            // the group follows the percentile as shown (rounded), so "50th of net worth" never reads "the bottom half"
            double shown = Math.Round(res.WealthRank * 100) / 100;
            res.WealthGroup = shown >= .99 ? 3 : shown >= .9 ? 2 : shown >= .5 ? 1 : 0;
            res.AgeBand = Mobility.AgeBand(you.Age);

            // The neighbors: adults within a band of rank, widened until there are enough.
            double band = .03; var neighbors = new List<PersonYear>();
            while (neighbors.Count < 40 && band < .5)
            {
                neighbors.Clear();
                foreach (var (rank, r) in adults) if (Math.Abs(rank - res.IncomeRank) <= band) neighbors.Add(r);
                band *= 1.6;
            }
            res.Neighbors = neighbors.Count;
            var jobs = new Dictionary<int, int>(); int modal = 0;
            foreach (var r in neighbors)
            {
                if (!r.Employed || r.Industry < 0) continue;
                jobs.TryGetValue(r.Industry, out int n); jobs[r.Industry] = ++n;
                if (n > modal || n == modal && r.Industry < res.ModalIndustry) { modal = n; res.ModalIndustry = r.Industry; }
            }
            double spend = 0, fear = 0, fantasy = 0, service = 0, taxes = 0, gross = 0; var savings = new List<double>();
            foreach (var r in neighbors)
            {
                double s = Math.Max(0, r.Spending); spend += s; fear += s * r.FearShare; fantasy += s * r.Fantasy; service += r.DebtService;
                for (int c = 0; c < 6; c++) res.Mix[c] += s * r.Category(c);
                if (r.Disposable > 0) savings.Add(r.Saving / r.Disposable);
                taxes += Math.Max(0, r.Taxes); gross += Math.Max(0, r.Wages) + Math.Max(0, r.Business) + Math.Max(0, r.CapitalIncome) + Math.Max(0, r.Transfers);
            }
            if (spend > 0) { for (int c = 0; c < 6; c++) res.Mix[c] /= spend; res.Fear = fear / spend; res.Fantasy = fantasy / spend; }
            res.DebtService = neighbors.Count > 0 ? service / neighbors.Count : 0;
            savings.Sort(); res.TypicalSaving = savings.Count > 0 ? savings[savings.Count / 2] : 0;
            res.Saving = double.IsNaN(you.Saving) ? res.TypicalSaving : you.Saving;
            double taxRate = gross > 0 ? Math.Min(.6, taxes / gross) : .2;
            res.Disposable = Math.Max(0, you.Income) * (1 - taxRate);
            res.Spending = res.Disposable * (1 - res.Saving);

            // The dollar's parts, by the neighbors' mix through the industries.
            if (capture != null)
            {
                double persuaded = 0, soldFear = 0, weight = 0;
                for (int c = 0; c < 6; c++)
                {
                    double w = res.Mix[c]; if (w <= 0) continue;
                    var parts = capture.CategoryParts[c];
                    res.Wages += w * parts[(int)ValueCapture.Part.Wages]; res.Taxes += w * parts[(int)ValueCapture.Part.Taxes];
                    res.Upkeep += w * parts[(int)ValueCapture.Part.Upkeep]; res.Imports += w * parts[(int)ValueCapture.Part.Imports];
                    double owners = capture.CapturedRate(c); res.Owners += w * owners;
                    persuaded += w * owners * capture.Persuaded(c);
                    soldFear += w * capture.CategoryFear[c]; weight += w;
                }
                res.Persuaded = res.Owners > 0 ? persuaded / res.Owners : 0;
                res.SoldFear = weight > 0 ? soldFear / weight : res.Fear;
            }
            res.PaysOwners = res.Spending * res.Owners;
            // Uphill: of all wages and of all income from ownership, the shares received above the viewer's rank, weighted
            // by the dollar's split; the population's distribution stands in for where this household's money goes.
            int k0 = Math.Max(0, Math.Min(100, (int)(res.IncomeRank * 100)));
            double wagesAll = wagesBelow[100], ownedAllC = ownedBelow[100];
            double wagesUp = wagesAll > 0 ? (wagesAll - wagesBelow[k0]) / wagesAll : 0, ownedUp = ownedAllC > 0 ? (ownedAllC - ownedBelow[k0]) / ownedAllC : 0;
            double flow = res.Wages + res.Owners;
            res.Uphill = flow > 0 ? (res.Wages * wagesUp + res.Owners * ownedUp) / flow : 0;
            res.OwnersToTop1 = ownedAll > 0 ? ownedTop1 / ownedAll : 0;

            // The two distances: the cushion (net worth, the home counted, in years of spending) and independence.
            double yearSpend = Math.Max(1, res.Spending);
            res.Cushion = Math.Max(0, you.NetWorth) / yearSpend;
            res.Independence = yearSpend / Yield;
            res.OwnedIncome = Math.Max(0, you.NetWorth) * Yield;
            res.YearsToIndependence = YearsTo(Math.Max(0, you.NetWorth), res.Disposable * res.Saving, res.Independence);

            if (mobility != null)
            {
                var odds = mobility.OddsFrom(res.Quintile, res.AgeBand);
                res.Up = odds.up; res.Stay = odds.stay; res.Down = odds.down; res.OddsLines = odds.lines;
                var reached = mobility.ReachedFrom(res.Quintile, res.AgeBand);
                Array.Copy(reached.share, res.Reached, Mobility.Quintiles);
                // the odds fall back to the fifth's at every age when too few stood at this one: say so, not "aged 35-49"
                int atAge = 0; for (int k = 0; k < 3; k++) atAge += mobility.Odds[res.Quintile, res.AgeBand, k];
                res.OddsAllAges = odds.lines > 0 && odds.lines != atAge;
            }
            return res;
        }

        /// <summary>What does not hold of the readings (empty when every check passes): the income rank never falls as
        /// income rises, the dollar's parts add up to its whole (with the owner-occupier's own part), the uphill share stays
        /// in 0..1 and does not rise from the bottom of the ladder to the top, the odds sum to one, and the fifths reached
        /// sum to the odds above, at and below the viewer's fifth.</summary>
        public List<string> Problems(EconomyData data, ValueCapture capture, Mobility mobility)
        {
            var list = new List<string>();
            if (incomeKey.Length == 0) { list.Add("no adults in the year"); return list; }
            double[] ladder = { 5e3, 20e3, 40e3, 80e3, 150e3, 300e3, 1e6 };
            Result previous = null, first = null;
            foreach (double income in ladder)
            {
                var r = Read(new Inputs { Age = 40, Income = income, NetWorth = income * 2, Debt = 0, Partnered = true, Industry = -1, Saving = double.NaN }, data, capture, mobility);
                first ??= r;
                if (previous != null && r.IncomeRank + 1e-9 < previous.IncomeRank) list.Add("the income rank falls as income rises (" + income.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + ")");
                if (r.Uphill < -1e-9 || r.Uphill > 1 + 1e-9) list.Add("the uphill share leaves 0..1");
                if (capture != null)
                {
                    double self = 0; for (int c = 0; c < 6; c++) self += r.Mix[c] * capture.CategorySelfOwned[c];
                    double parts = r.Wages + r.Taxes + r.Upkeep + r.Owners + r.Imports + self;
                    if (Math.Abs(parts - 1) > .02) list.Add("the dollar's parts add up to " + parts.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
                }
                if (r.OddsLines > 0 && Math.Abs(r.Up + r.Stay + r.Down - 1) > 1e-9) list.Add("the odds do not sum to one");
                if (r.OddsLines > 0)
                {
                    double up = 0, down = 0; for (int q = 0; q < Mobility.Quintiles; q++) { if (q > r.Quintile) up += r.Reached[q]; if (q < r.Quintile) down += r.Reached[q]; }
                    if (Math.Abs(up - r.Up) > 1e-9 || Math.Abs(down - r.Down) > 1e-9 || Math.Abs(r.Reached[r.Quintile] - r.Stay) > 1e-9)
                        list.Add("the fifths reached disagree with the odds");
                }
                previous = r;
            }
            if (previous != null && first != null && previous.Uphill > first.Uphill + 1e-9) list.Add("the top of the ladder sends more uphill than the bottom");
            return list;
        }

        /// <summary>Years for wealth w, adding s a year at <see cref="RealReturn"/>, to reach a target (0 when already
        /// there; NaN when it never does within a century).</summary>
        public static double YearsTo(double w, double s, double target)
        {
            if (w >= target) return 0;
            for (int t = 1; t <= 100; t++)
            {
                w = w * (1 + RealReturn) + s;
                if (w >= target) return t;
                if (w <= 0 && s <= 0) return double.NaN;
            }
            return double.NaN;
        }

        /// <summary>Index of the first element at least v (binary search).</summary>
        static int Lower(double[] a, double v)
        {
            int lo = 0, hi = a.Length;
            while (lo < hi) { int mid = (lo + hi) >> 1; if (a[mid] < v) lo = mid + 1; else hi = mid; }
            return lo;
        }
    }
}
