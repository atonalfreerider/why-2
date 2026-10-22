using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;
using Why.Economy.Data;
using Why.Humans;

namespace Why.Economy.Model
{
    /// <summary>
    /// What changed in each year of the wave, for the replay's readout (<see cref="EconomyReplay"/>), from data already in
    /// the project: GDP and its real growth (industries.json), the market's real value added (the wave's own width: every
    /// industry but government, deflated) and the tiers' shares of it, the federal funds and effective tariff rates
    /// (money-and-trade.json), federal outlays and receipts (government.json, OMB fiscal years) and the companies born and
    /// ended (company-lives.json). Nothing here is authored: the turning points are read off the same series by fixed
    /// rules (<see cref="TurningPoint"/>), so a revised series moves them. Pure and immutable once built; any thread.
    /// </summary>
    public sealed class ReplayFacts
    {
        public const string MoneyPath = "Data/economy/money-and-trade";

        /// <summary>How many of each kind of turning point are kept: the deepest recessions, the largest swings of the
        /// federal funds rate, the largest move of the tariff rate.</summary>
        public const int DeepestRecessions = 4, LargestRateSwings = 4, LargestTariffMoves = 1;

        /// <summary>A year is quiet (the replay moves through it faster) when none of these moved: the federal funds or
        /// tariff rate by a point, federal outlays by a point of GDP, a tier's share by a point; and the market grew, and no
        /// stored company was born or ended.</summary>
        public const double QuietRateMove = 1, QuietOutlaysMove = .01, QuietShareMove = .01;

        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        public enum Kind { Recession, RatePeak, RateTrough, Tariff }

        /// <summary>
        /// A turning point read off the data. A recession is a run of years in which the market's real value added fell
        /// (marked at its last year, with the run's whole fall); a rate swing is a run of annual federal funds averages
        /// moving one way (marked where it turns: its peak or trough); a tariff move is the same for the effective tariff
        /// rate. Each kind is ranked by size and only the largest are kept.
        /// </summary>
        public sealed class TurningPoint
        {
            public Kind Kind;
            /// <summary>The year it is marked at, and the run's first year (the year before the first move, for rates).</summary>
            public int Year, From;
            /// <summary>The level before the run and at its end (percent; for a recession the market's real value added in
            /// 2025 $B), and the run's size: percentage points, or the fall as a share for a recession.</summary>
            public double Before, After, Size;
            /// <summary>A short tag ("rates peak 16.4%") and the readout's sentence.</summary>
            public string Tag, Text;
        }

        /// <summary>One year's facts and its change from the year before (NaN where a series has no value).</summary>
        public sealed class Year
        {
            public int Value;
            public bool Estimate;
            /// <summary>Nominal GDP ($B), its real growth, and the market's real value added growth (shares; NaN for the
            /// first year).</summary>
            public double Gdp, RealGrowth = double.NaN, MarketGrowth = double.NaN;
            /// <summary>Each market tier's share of the market's value added (raw, manufacturing and infrastructure,
            /// services, tech) and its change in points of share.</summary>
            public readonly double[] TierShare = new double[4], TierChange = new double[4];
            /// <summary>Annual averages (percent) and their change from the year before (points).</summary>
            public double FedFunds = double.NaN, FedFundsChange = double.NaN, Tariff = double.NaN, TariffChange = double.NaN;
            /// <summary>Federal outlays and receipts (shares of GDP, fiscal year), the outlays' social, military and interest
            /// parts, and the outlays' change.</summary>
            public double Outlays = double.NaN, Receipts = double.NaN, Social = double.NaN, Military = double.NaN, Interest = double.NaN,
                OutlaysChange = double.NaN;
            /// <summary>Stored companies founded and ended this year ("Enron (bankrupt)").</summary>
            public readonly List<string> Born = new List<string>(), Ended = new List<string>();
            /// <summary>Turning points marked at this year.</summary>
            public readonly List<TurningPoint> Turns = new List<TurningPoint>();

            public bool Quiet { get; internal set; }
            public bool Fell => MarketGrowth < 0;
        }

        /// <summary>The first and last year with facts (the wave's first rib to the last year with data).</summary>
        public readonly int First, Last;

        /// <summary>The market tiers' names, ground first (raw .. tech).</summary>
        public readonly string[] TierNames = new string[4];

        readonly Year[] years;
        public readonly List<TurningPoint> TurningPoints = new List<TurningPoint>();

        /// <summary>The body of the "[Why] Replay facts" log line.</summary>
        public string Log { get; private set; } = "";

        ReplayFacts(int first, int last)
        {
            First = first;
            Last = Math.Max(first, last);
            years = new Year[Last - First + 1];
            for (int i = 0; i < years.Length; i++) years[i] = new Year { Value = First + i };
        }

        /// <summary>A year's facts (clamped to the range).</summary>
        public Year At(int year) => years[Math.Max(0, Math.Min(years.Length - 1, year - First))];

        /// <summary>
        /// Builds every year's facts from <paramref name="first"/> to <paramref name="last"/> and the turning points. The
        /// money file's text, the government history and the company lives may be missing (their lines are left out).
        /// </summary>
        public static ReplayFacts Build(EconomyData data, string moneyJson, GovernmentHistory government, CompanyLives companies, int first, int last)
        {
            var f = new ReplayFacts(first, last);
            f.Fill(data, moneyJson, government, companies);
            return f;
        }

        void Fill(EconomyData data, string moneyJson, GovernmentHistory government, CompanyLives companies)
        {
            // the market tiers (government is the bedrock, not a ridge of the wave)
            if (data != null)
            {
                foreach (Tier t in data.Tiers)
                {
                    if (t.Index >= 1 && t.Index <= 4) TierNames[t.Index - 1] = t.Name ?? t.Id;
                }
            }

            for (int k = 0; k < 4; k++) TierNames[k] ??= "tier " + (k + 1).ToString(Ci);

            // the economy: GDP, its real growth, the market's real value added and the tiers' shares of it
            double[] market = new double[years.Length];
            for (int i = 0; i < years.Length; i++)
            {
                Year y = years[i];
                if (data == null) continue;
                y.Estimate = data.IsEstimate(y.Value);
                y.Gdp = data.Gdp.At(y.Value);
                double total = 0;
                double[] tier = new double[4];
                foreach (Industry ind in data.Industries)
                {
                    if (ind.TierIndex < 1 || ind.TierIndex > 4) continue;
                    double va = Math.Max(0, ind.ValueAdded.GrowthAt(y.Value));
                    tier[ind.TierIndex - 1] += va;
                    total += va;
                }

                for (int k = 0; k < 4; k++) y.TierShare[k] = total > 0 ? tier[k] / total : 0;
                market[i] = data.Real(total, y.Value);
                if (i == 0) continue;
                Year p = years[i - 1];
                double realNow = data.Real(y.Gdp, y.Value), realThen = data.Real(p.Gdp, p.Value);
                if (realThen > 0) y.RealGrowth = realNow / realThen - 1;
                if (market[i - 1] > 0) y.MarketGrowth = market[i] / market[i - 1] - 1;
                for (int k = 0; k < 4; k++) y.TierChange[k] = y.TierShare[k] - p.TierShare[k];
            }

            // the state's levers: the price of money, the edge of trade, the federal budget
            List<(int year, double value)> funds = Series(moneyJson, "fedFunds"), tariffs = Series(moneyJson, "tariffRate");
            Assign(funds, (y, v, d) => { y.FedFunds = v; y.FedFundsChange = d; });
            Assign(tariffs, (y, v, d) => { y.Tariff = v; y.TariffChange = d; });
            if (government?.us != null)
            {
                foreach (GovernmentHistory.UsYear u in government.us)
                {
                    if (u == null || u.year < First || u.year > Last) continue;
                    Year y = At(u.year);
                    y.Outlays = u.Outlays;
                    y.Receipts = u.receipts;
                    y.Social = u.Social;
                    y.Military = u.defense;
                    y.Interest = u.netInterest;
                }

                for (int i = 1; i < years.Length; i++)
                {
                    if (!double.IsNaN(years[i].Outlays) && !double.IsNaN(years[i - 1].Outlays)) years[i].OutlaysChange = years[i].Outlays - years[i - 1].Outlays;
                }
            }

            // companies born and ended on the wave
            if (companies != null)
            {
                foreach (CompanyLives.Life life in companies.Lives)
                {
                    if (life.Founded >= First && life.Founded <= Last) At(life.Founded).Born.Add(life.Name);
                    if (!life.Alive && life.Ended >= First && life.Ended <= Last) At(life.Ended).Ended.Add(life.Name + " (" + life.Fate + ")");
                }
            }

            Recessions(market);
            Swings(funds, false, LargestRateSwings);
            Swings(tariffs, true, LargestTariffMoves);
            TurningPoints.Sort((a, b) => a.Year != b.Year ? a.Year.CompareTo(b.Year) : a.Kind.CompareTo(b.Kind));
            foreach (TurningPoint t in TurningPoints) At(t.Year).Turns.Add(t);

            int quiet = 0;
            foreach (Year y in years)
            {
                bool moved = y.Fell || y.Born.Count > 0 || y.Ended.Count > 0 || y.Turns.Count > 0 ||
                             Math.Abs(Or0(y.FedFundsChange)) >= QuietRateMove || Math.Abs(Or0(y.TariffChange)) >= QuietRateMove ||
                             Math.Abs(Or0(y.OutlaysChange)) >= QuietOutlaysMove;
                for (int k = 0; k < 4; k++) moved |= Math.Abs(y.TierChange[k]) >= QuietShareMove;
                y.Quiet = !moved;
                if (y.Quiet) quiet++;
            }

            var tags = new List<string>();
            foreach (TurningPoint t in TurningPoints) tags.Add(t.Year.ToString(Ci) + " " + t.Tag);
            Log = First.ToString(Ci) + "-" + Last.ToString(Ci) + ": " + quiet.ToString(Ci) + " quiet years; turning points " +
                  string.Join(", ", tags);
        }

        static double Or0(double v) => double.IsNaN(v) ? 0 : v;

        /// <summary>Puts a series' values on the years it covers, with the change from the year before when both exist.</summary>
        void Assign(List<(int year, double value)> s, Action<Year, double, double> set)
        {
            for (int i = 0; i < s.Count; i++)
            {
                if (s[i].year < First || s[i].year > Last) continue;
                double change = i > 0 && s[i - 1].year == s[i].year - 1 ? s[i].value - s[i - 1].value : double.NaN;
                set(At(s[i].year), s[i].value, change);
            }
        }

        /// <summary>A [[year, value], ...] series of the money file, in year order (empty when missing).</summary>
        static List<(int year, double value)> Series(string json, string key)
        {
            var list = new List<(int, double)>();
            if (string.IsNullOrEmpty(json)) return list;
            JObject root;
            try
            {
                root = JObject.Parse(json);
            }
            catch (Exception)
            {
                return list;
            }

            if (root[key] is JArray a)
            {
                foreach (JToken p in a)
                {
                    if (p is JArray pair && pair.Count >= 2 && pair[0].Type != JTokenType.Null && pair[1].Type != JTokenType.Null)
                    {
                        list.Add(((int)pair[0], (double)pair[1]));
                    }
                }
            }

            list.Sort((x, y) => x.Item1.CompareTo(y.Item1));
            return list;
        }

        /// <summary>
        /// Recessions: runs of years in which the market's real value added fell, each with its whole fall; the
        /// <see cref="DeepestRecessions"/> deepest are kept, marked at the run's last year.
        /// </summary>
        void Recessions(double[] market)
        {
            var runs = new List<TurningPoint>();
            for (int i = 1; i < years.Length; i++)
            {
                if (!(years[i].MarketGrowth < 0)) continue;
                int j = i;
                while (j + 1 < years.Length && years[j + 1].MarketGrowth < 0) j++;
                double before = market[i - 1], after = market[j];
                if (before > 0)
                {
                    double fall = 1 - after / before;
                    string span = i == j ? "in " + years[i].Value.ToString(Ci) : "over " + years[i].Value.ToString(Ci) + "-" + (years[j].Value % 100).ToString("00", Ci);
                    runs.Add(new TurningPoint
                    {
                        Kind = Kind.Recession, Year = years[j].Value, From = years[i].Value, Before = before, After = after, Size = fall,
                        Tag = "recession " + Signed(-100 * fall, 1) + "%",
                        Text = "Recession: the market's real value added fell " + (100 * fall).ToString("0.0", Ci) + "% " + span + ", and the wave narrowed"
                    });
                }

                i = j;
            }

            runs.Sort((a, b) => b.Size.CompareTo(a.Size));
            for (int k = 0; k < runs.Count && k < DeepestRecessions; k++) TurningPoints.Add(runs[k]);
        }

        /// <summary>
        /// Swings of an annual rate (the federal funds rate, or the tariff rate): runs of consecutive years moving the same
        /// way, from the year before the first move to the turn; the <paramref name="keep"/> largest (in points) are kept,
        /// marked at the turn.
        /// </summary>
        void Swings(List<(int year, double value)> s, bool tariff, int keep)
        {
            var runs = new List<TurningPoint>();
            for (int i = 1; i < s.Count; i++)
            {
                double d = s[i].value - s[i - 1].value;
                if (d == 0 || s[i].year != s[i - 1].year + 1) continue;
                int sign = Math.Sign(d), j = i;
                while (j + 1 < s.Count && s[j + 1].year == s[j].year + 1 && Math.Sign(s[j + 1].value - s[j].value) == sign) j++;
                var t = new TurningPoint
                {
                    Kind = tariff ? Kind.Tariff : sign > 0 ? Kind.RatePeak : Kind.RateTrough,
                    Year = s[j].year, From = s[i - 1].year, Before = s[i - 1].value, After = s[j].value, Size = s[j].value - s[i - 1].value
                };
                string end = s[j].year >= First && s[j].year <= Last && At(s[j].year).Estimate ? " (estimate)" : "";
                string move = t.Before.ToString("0.0", Ci) + "% in " + t.From.ToString(Ci) + " to " + t.After.ToString("0.0", Ci) + "%" + end;
                if (t.Kind == Kind.Tariff)
                {
                    t.Tag = "tariffs " + t.After.ToString("0.0", Ci) + "%";
                    t.Text = "Tariffs " + (sign > 0 ? "jump" : "fall") + ": duties " + (sign > 0 ? "rose" : "fell") + " from " + move + " of goods imports";
                }
                else if (t.Kind == Kind.RatePeak)
                {
                    t.Tag = "rates peak " + t.After.ToString("0.0", Ci) + "%";
                    t.Text = "Rates peak: the federal funds rate rose from " + move + ", and new borrowing cost more";
                }
                else
                {
                    t.Tag = "rates bottom " + t.After.ToString("0.0", Ci) + "%";
                    t.Text = "Rates bottom: the federal funds rate fell from " + move + ", and borrowing grew cheaper";
                }

                runs.Add(t);
                i = j;
            }

            runs.Sort((a, b) => Math.Abs(b.Size).CompareTo(Math.Abs(a.Size)));
            for (int k = 0; k < runs.Count && k < keep; k++)
            {
                if (runs[k].Year >= First && runs[k].Year <= Last) TurningPoints.Add(runs[k]);
            }
        }

        /// <summary>"+2.6" / "−1.8" (a true minus sign).</summary>
        public static string Signed(double v, int decimals) =>
            (v > 0 ? "+" : v < 0 ? "−" : "") + Math.Abs(v).ToString("F" + decimals, Ci);

        /// <summary>
        /// What does not hold (empty when every check passes): every year has a GDP and its tier shares sum to one, the
        /// changes are the differences of the levels, each kept turning point lies in the range and is marked on its year,
        /// recessions are falls and rate swings run the way their kind says.
        /// </summary>
        public List<string> Problems()
        {
            var list = new List<string>();
            for (int i = 0; i < years.Length; i++)
            {
                Year y = years[i];
                if (!(y.Gdp > 0)) list.Add(y.Value.ToString(Ci) + " has no GDP");
                double sum = 0;
                for (int k = 0; k < 4; k++) sum += y.TierShare[k];
                if (Math.Abs(sum - 1) > 1e-6) list.Add(y.Value.ToString(Ci) + "'s tier shares sum to " + sum.ToString("0.000", Ci));
                if (i > 0 && !double.IsNaN(y.FedFundsChange) && Math.Abs(y.FedFundsChange - (y.FedFunds - years[i - 1].FedFunds)) > 1e-9)
                {
                    list.Add(y.Value.ToString(Ci) + "'s federal funds change is not the difference of the levels");
                }
            }

            foreach (TurningPoint t in TurningPoints)
            {
                if (t.Year < First || t.Year > Last || !At(t.Year).Turns.Contains(t)) list.Add(t.Tag + " is not marked on its year");
                if (t.Kind == Kind.Recession && !(t.Size > 0 && At(t.Year).Fell)) list.Add(t.Year.ToString(Ci) + " is marked a recession without a fall");
                if (t.Kind == Kind.RatePeak && !(t.Size > 0)) list.Add(t.Year.ToString(Ci) + " is marked a rate peak without a rise");
                if (t.Kind == Kind.RateTrough && !(t.Size < 0)) list.Add(t.Year.ToString(Ci) + " is marked a rate trough without a fall");
            }

            return list;
        }
    }
}
