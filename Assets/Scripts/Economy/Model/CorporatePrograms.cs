using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Why.Economy.Data;

namespace Why.Economy.Model
{
    /// <summary>
    /// Companies run the same program as people (corporate-programs.json, Tools/corporate/build.py): money comes in, a
    /// mind weighs desire against fear, money goes out, and the spending moves them. For each stored firm (the towers) its
    /// latest fiscal year's uses of money from its audited filing, sorted into the directions they flow on the landscape:
    /// <list type="bullet">
    /// <item><b>operations</b> (cost of revenue and overhead, less advertising and stock pay): down the hill to its
    /// suppliers and to its own workers, split by its industry's accounts (the BEA 2024 use table's inputs against its
    /// compensation, <see cref="ValueCapture"/>);</item>
    /// <item><b>investment</b> (capital expenditures and finance-leased assets) and <b>research</b>: forward over the crest
    /// into the future, split between fear and desire by Jev's judgment of what drives it;</item>
    /// <item><b>attention</b> (advertising, where disclosed): up to the attention platforms;</item>
    /// <item><b>deals</b> (acquisitions and stakes in other companies): across to other firms;</item>
    /// <item><b>owners</b> (buybacks and dividends): up to the owners;</item>
    /// <item><b>the state</b> (cash income taxes) down into the bedrock, and <b>lobbying</b> (calendar 2025, LDA filings) to
    /// the officeholders.</item>
    /// </list>
    /// Jev also judged which way each firm's money moves it in the hierarchy (climb, reach down, spread across, lean on
    /// the state, harvest; the probability of each is kept) and how much of its investment goes to AI. The economy-wide
    /// totals (advertising, business investment by type, payouts, lobbying, M&amp;A, hyperscaler capex) come with sources.
    /// <para>Behind the latest year each firm has its <see cref="Firm.History"/>: every fiscal year mostly in calendar
    /// 2015-2025 from the XBRL data of its 10-Ks on SEC EDGAR (Tools/corporate/history.py; SpaceX from its prospectus,
    /// 2023-2025; Palantir from 2018), with the same three judgments made on each year's numbers alone, so the trail is
    /// judged one way throughout and its last year differs from the crest's judgment only by what the firm's words add.</para>
    /// Pure: parses on any thread, no Unity objects.
    /// </summary>
    public sealed class CorporatePrograms
    {
        public const string Path = "Data/economy/corporate-programs";

        public enum Direction { Climb = 0, IntegrateDown = 1, ExpandAcross = 2, LeanOnState = 3, Harvest = 4 }
        public const int Directions = 5;
        public static readonly string[] DirectionIds = { "climb", "integrate_down", "expand_across", "lean_on_state", "harvest" };
        public static readonly string[] DirectionNames = { "climbing into software, platforms and AI", "reaching down into its own supply",
            "spreading across its own layer", "leaning on the state", "harvesting cash for its owners" };
        public static readonly string[] DirectionShort = { "climb", "reach down", "spread across", "lean on the state", "harvest" };

        /// <summary>The first and last calendar years of the history.</summary>
        public const int FirstYear = 2015, LastYear = 2025;

        /// <summary>One firm's year (the crest's latest year, or a year of its history).</summary>
        public sealed class Firm
        {
            public string Ticker, Name, IndustryId, Period, Strategy, Notes, Source;
            /// <summary>A history year's calendar year (the one holding most of its days), its fiscal year's own name, and
            /// where it sits on the time axis (its midpoint less half a year, so calendar year Y sits at Y); the crest's: 0.</summary>
            public int Year; public string FiscalYear; public double Position;
            /// <summary>What a history year's judgments were made on ("numbers": its reported numbers alone).</summary>
            public string JudgedOn;
            /// <summary>The fiscal years behind the latest one, oldest first (empty for a history year).</summary>
            public Firm[] History = Array.Empty<Firm>();
            /// <summary>The industry index (EconomyData.Industries) and the tower it stands for (capture index, -1: none).</summary>
            public int Industry = -1, Tower = -1;
            /// <summary>Its filing's lines, USD billions (NaN: not disclosed); lobbying USD millions; employees thousands.</summary>
            public double Revenue, CostOfRevenue, Rd, Sga, Advertising, Capex, FinanceLeases, Buybacks, Dividends, Acquisitions, Stakes,
                TaxesPaid, StockComp, NetIncome, Compensation, NoninterestExpense, ContentSpend, TechInfrastructure, Fulfillment,
                Lobbying, Employees;
            /// <summary>Total operating costs from the filing's own breakdown where it reports one (a conglomerate, an oil
            /// major), less interest and non-income taxes; NaN otherwise.</summary>
            public double OperatingCosts = double.NaN;
            /// <summary>Jev: probability of each direction, the investment's fear share (0 desire .. 1 fear), the AI share of
            /// investment, and whether the judgments were found.</summary>
            public readonly float[] Direction = new float[Directions];
            public float Defensive = .5f, Ai, DirectionConfidence, DefensiveConfidence, AiConfidence; public bool Judged;

            static double Z(double v) => double.IsNaN(v) ? 0 : Math.Max(0, v);
            public double Owners => Z(Buybacks) + Z(Dividends);
            public double Invest => Z(Capex) + Z(FinanceLeases);
            public double Research => Z(Rd);
            public double Attention => Z(Advertising);
            public double Deals => Z(Acquisitions) + Z(Stakes);
            public double State => Z(TaxesPaid);
            /// <summary>Running the business: cost of revenue and overhead (with Amazon's fulfillment and technology lines; a
            /// bank's noninterest expense), less advertising (its own stream) and stock pay (not cash). Depreciation of past
            /// investment is inside it, as the filings report.</summary>
            public double Operations
            {
                get
                {
                    double o = Z(CostOfRevenue) + Z(Sga) + Z(Fulfillment) + Z(TechInfrastructure);
                    if (double.IsNaN(CostOfRevenue) && !double.IsNaN(NoninterestExpense)) o = Z(NoninterestExpense);
                    if (!double.IsNaN(OperatingCosts)) o = OperatingCosts;
                    return Math.Max(0, o - Attention - Z(StockComp));
                }
            }
            /// <summary>Everything it paid out in the year (the streams' sum, lobbying included).</summary>
            public double Total => Operations + Invest + Research + Attention + Deals + Owners + State + Z(Lobbying) / 1000;
            /// <summary>The direction it most leans to, and its probability.</summary>
            public int Main { get { int best = 0; for (int d = 1; d < Directions; d++) if (Direction[d] > Direction[best]) best = d; return best; } }
        }

        public readonly Firm[] Firms;
        public readonly string Model, Built, HistoryCollected;
        /// <summary>What the history lacks (a year a firm did not file, a line it did not report), as the collector named it.</summary>
        public readonly string[] HistoryGaps;
        /// <summary>The US totals (economy.json), each as value with unit, year and source; read with <see cref="Total"/>.</summary>
        readonly JObject economy;

        sealed class FileRow
        {
            public string ticker, name, industry, period, strategy, notes, directionChoice, fiscalYear, judgedOn;
            public double? revenue, costOfRevenue, rd, sga, advertising, capex, financeLeases, buybacks, dividends, acquisitions, stakes,
                incomeTaxesPaid, stockComp, netIncome, compensation, noninterestExpense, contentSpend, technologyAndInfrastructure, fulfillment,
                lobbying, employees, defensive, ai, directionConfidence, defensiveConfidence, aiConfidence, operatingCosts, position;
            public int year;
            public Dictionary<string, double> direction;
            /// <summary>Source links by kind ("financials", "lobbying", "strategy"; some kinds hold lists).</summary>
            public JObject sources;
            /// <summary>The fiscal years behind the latest (the crest's row only).</summary>
            public List<FileRow> history;
        }
        sealed class FileRoot { public string model, built, historyCollected; public List<string> historyGaps; public List<FileRow> firms; public JObject economy; }

        static CorporatePrograms current;
        /// <summary>The loaded programs (null until <see cref="Provide"/> succeeds).</summary>
        public static CorporatePrograms Get() => current;

        /// <summary>Parses the file once (any thread); a missing or unreadable file leaves the programs absent.</summary>
        public static void Provide(EconomyData data, string json)
        {
            if (current != null || data == null || string.IsNullOrEmpty(json)) return;
            try { current = new CorporatePrograms(data, json); }
            catch (Exception e) { UnityEngine.Debug.LogWarning("[Why] Corporate programs unreadable: " + e.Message); }
        }

        CorporatePrograms(EconomyData data, string json)
        {
            var root = JsonConvert.DeserializeObject<FileRoot>(json);
            Model = root?.model; Built = root?.built; economy = root?.economy;
            HistoryCollected = root?.historyCollected; HistoryGaps = root?.historyGaps?.ToArray() ?? Array.Empty<string>();
            var list = new List<Firm>(); var capture = data.Circuit?.Capture;
            foreach (var r in root?.firms ?? new List<FileRow>())
            {
                var f = Parse(r, data);
                if (capture != null)
                    for (int c = 0; c < capture.Count; c++)
                        if (!string.IsNullOrEmpty(capture[c].Ticker) && string.Equals(capture[c].Ticker, f.Ticker, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(capture[c].Name, f.Name, StringComparison.OrdinalIgnoreCase)) { f.Tower = c; break; }
                var years = new List<Firm>();
                foreach (var h in r.history ?? new List<FileRow>())
                {
                    var y = Parse(h, data);
                    y.Ticker = f.Ticker; y.Name = f.Name; y.IndustryId = f.IndustryId; y.Industry = f.Industry; y.Tower = f.Tower;
                    y.Period = h.fiscalYear + " (" + h.year + ")";
                    years.Add(y);
                }
                f.History = years.ToArray();
                list.Add(f);
            }
            Firms = list.ToArray();
        }

        /// <summary>One row of the file (the crest's latest year, or a history year): its lines and Jev's judgments.</summary>
        static Firm Parse(FileRow r, EconomyData data)
        {
            double N(double? v) => v ?? double.NaN;
            var f = new Firm
            {
                Ticker = r.ticker ?? "", Name = r.name, IndustryId = r.industry, Period = r.period, Strategy = r.strategy, Notes = r.notes,
                Source = r.sources?["financials"]?.Type == JTokenType.String ? (string)r.sources["financials"] : null,
                Year = r.year, FiscalYear = r.fiscalYear, Position = r.position ?? r.year, JudgedOn = r.judgedOn,
                Revenue = N(r.revenue), CostOfRevenue = N(r.costOfRevenue), Rd = N(r.rd), Sga = N(r.sga), Advertising = N(r.advertising),
                Capex = N(r.capex), FinanceLeases = N(r.financeLeases), Buybacks = N(r.buybacks), Dividends = N(r.dividends),
                Acquisitions = N(r.acquisitions), Stakes = N(r.stakes), TaxesPaid = N(r.incomeTaxesPaid), StockComp = N(r.stockComp),
                NetIncome = N(r.netIncome), Compensation = N(r.compensation), NoninterestExpense = N(r.noninterestExpense),
                ContentSpend = N(r.contentSpend), TechInfrastructure = N(r.technologyAndInfrastructure), Fulfillment = N(r.fulfillment),
                Lobbying = N(r.lobbying), Employees = N(r.employees), OperatingCosts = N(r.operatingCosts)
            };
            var industry = r.industry == null ? null : data.IndustryById(r.industry); if (industry != null) f.Industry = industry.Index;
            if (r.direction != null)
            {
                f.Judged = true; double sum = 0;
                for (int d = 0; d < Directions; d++) { r.direction.TryGetValue(DirectionIds[d], out double p); f.Direction[d] = (float)p; sum += p; }
                if (sum > 0) for (int d = 0; d < Directions; d++) f.Direction[d] = (float)(f.Direction[d] / sum);
                f.Defensive = (float)(r.defensive ?? .5); f.Ai = (float)(r.ai ?? 0); f.DirectionConfidence = (float)(r.directionConfidence ?? 0);
                f.DefensiveConfidence = (float)(r.defensiveConfidence ?? 0); f.AiConfidence = (float)(r.aiConfidence ?? 0);
            }
            return f;
        }

        /// <summary>What does not hold of the programs (empty when every check passes): every stored firm has its filing and
        /// stands on a known industry, its directions sum to one, it paid out something, its operating costs do not exceed
        /// half again its revenue, and no stream is negative. Of each firm's history: it has years, they run in order within
        /// 2015-2025 each at its place on the time axis (within half a year of its calendar year), every year is judged
        /// (directions sum to one, fear and AI shares in 0..1) and no line it draws is negative, and its last year is the
        /// crest's year: calendar 2025, investing, researching and paying owners what the crest's filing says within 5%
        /// (or $0.1B).</summary>
        public List<string> Problems(int towers)
        {
            var list = new List<string>();
            var seen = new HashSet<int>();
            foreach (var f in Firms)
            {
                if (f.Tower < 0) list.Add(f.Name + " has no tower"); else seen.Add(f.Tower);
                if (f.Industry < 0) list.Add(f.Name + " stands on no known industry");
                if (f.Judged) { double sum = 0; foreach (float p in f.Direction) sum += p; if (Math.Abs(sum - 1) > 1e-3) list.Add(f.Name + "'s directions do not sum to one"); }
                if (!(f.Total > 0)) list.Add(f.Name + " paid out nothing");
                if (!double.IsNaN(f.Revenue) && f.Operations > 1.5 * f.Revenue) list.Add(f.Name + "'s operating costs exceed half again its revenue");
                if (f.Owners < 0 || f.Invest < 0 || f.Deals < 0 || f.State < 0 || f.Operations < 0) list.Add(f.Name + " has a negative stream");
                HistoryProblems(f, list);
            }
            if (seen.Count != towers) list.Add(seen.Count + " of " + towers + " towers have a program");
            return list;
        }

        static void HistoryProblems(Firm f, List<string> list)
        {
            if (f.History.Length == 0) { list.Add(f.Name + " has no history"); return; }
            int previous = int.MinValue;
            foreach (var y in f.History)
            {
                string at = f.Name + " " + y.FiscalYear;
                if (y.Year <= previous) list.Add(at + " is out of order");
                previous = y.Year;
                if (y.Year < FirstYear || y.Year > LastYear) list.Add(at + " lies outside " + FirstYear + "-" + LastYear);
                if (Math.Abs(y.Position - y.Year) > .5 + 1e-6) list.Add(at + " sits more than half a year from " + y.Year);
                if (!y.Judged) list.Add(at + " has no judgment");
                else
                {
                    double sum = 0; foreach (float p in y.Direction) sum += p;
                    if (Math.Abs(sum - 1) > 1e-3) list.Add(at + "'s directions do not sum to one");
                    if (y.Defensive < 0 || y.Defensive > 1 || y.Ai < 0 || y.Ai > 1) list.Add(at + "'s fear or AI share lies outside 0..1");
                }
                foreach (double v in new[] { y.Capex, y.FinanceLeases, y.Rd, y.Buybacks, y.Dividends })
                    if (v < 0) { list.Add(at + " has a negative line it draws"); break; }
                if (!(y.Invest + y.Research + y.Owners > 0)) list.Add(at + " invested, researched and paid owners nothing");
            }
            var last = f.History[f.History.Length - 1];
            if (last.Year != LastYear) list.Add(f.Name + "'s history ends in " + last.Year + ", not the crest's " + LastYear);
            else
            {
                bool Near(double a, double b) => Math.Abs(a - b) <= Math.Max(.05 * Math.Abs(b), .1);
                if (!Near(last.Invest, f.Invest) || !Near(last.Research, f.Research) || !Near(last.Owners, f.Owners))
                    list.Add(f.Name + "'s last history year (" + last.FiscalYear + ") disagrees with its filing: invest " + last.Invest.ToString("0.00") + " vs " +
                             f.Invest.ToString("0.00") + ", research " + last.Research.ToString("0.00") + " vs " + f.Research.ToString("0.00") +
                             ", owners " + last.Owners.ToString("0.00") + " vs " + f.Owners.ToString("0.00"));
            }
        }

        /// <summary>The number of history years over all firms.</summary>
        public int HistoryYears { get { int n = 0; foreach (var f in Firms) n += f.History.Length; return n; } }

        /// <summary>The firm of a tower (capture index), or null.</summary>
        public Firm OfTower(int capture) { foreach (var f in Firms) if (f.Tower == capture) return f; return null; }

        /// <summary>A US total from economy.json by path ("totalAdSpend", "digitalAdRevenue.socialMedia",
        /// "hyperscalerCapex.amazon.calendar2025"): its value, NaN when absent or not published.</summary>
        public double Total(string path)
        {
            JToken t = economy;
            foreach (string part in path.Split('.')) { if (!(t is JObject o) || !o.TryGetValue(part, out t)) return double.NaN; }
            if (t is JObject leaf && leaf.TryGetValue("value", out JToken v) && v.Type != JTokenType.Null) return (double)v;
            return t != null && (t.Type == JTokenType.Float || t.Type == JTokenType.Integer) ? (double)t : double.NaN;
        }

        /// <summary>The hyperscalers' capex in calendar 2025 and their latest guidance for 2026 ($B): Microsoft, Alphabet,
        /// Amazon and Meta by calendar year, Oracle by its nearest fiscal year (the file says which).</summary>
        public (double y2025, double y2026) Hyperscalers()
        {
            double a = 0, b = 0;
            foreach (string firm in new[] { "microsoft", "alphabet", "amazon", "meta" })
            {
                double x = Total("hyperscalerCapex." + firm + ".calendar2025"), y = Total("hyperscalerCapex." + firm + ".guidance2026");
                if (!double.IsNaN(x)) a += x; if (!double.IsNaN(y)) b += y;
            }
            double o25 = Total("hyperscalerCapex.oracle.trailing4Q_Dec2024toNov2025"), o26 = Total("hyperscalerCapex.oracle.guidance");
            if (!double.IsNaN(o25)) a += o25; if (!double.IsNaN(o26)) b += o26;
            return (a, b);
        }

        /// <summary>
        /// A firm's operations split by its industry's 2024 accounts (<see cref="ValueCapture"/>): what goes to suppliers
        /// (the industry's purchased inputs per dollar of output) and what to workers (its compensation per dollar of output),
        /// in proportion; and the suppliers' share by supplying industry (other industries only; its own industry's purchases
        /// stay home). Shares sum to the suppliers' dollars. An estimate from the industry, not the firm's own books.
        /// </summary>
        public static void SplitOperations(Firm f, ValueCapture capture, out double suppliers, out double workers, double[] bySupplier)
        {
            suppliers = workers = 0;
            if (bySupplier != null) Array.Clear(bySupplier, 0, bySupplier.Length);
            int j = f.Industry; double o = f.Operations;
            if (capture == null || j < 0 || j >= capture.N || o <= 0) return;
            double inputs = 0, others = 0;
            for (int i = 0; i < capture.N; i++) { inputs += capture.Direct[i, j]; if (i != j) others += capture.Direct[i, j]; }
            double wages = capture.PartOfOutput[j][(int)ValueCapture.Part.Wages];
            if (inputs + wages <= 0) return;
            suppliers = o * inputs / (inputs + wages); workers = o - suppliers;
            if (bySupplier != null && others > 0)
                for (int i = 0; i < capture.N && i < bySupplier.Length; i++) bySupplier[i] = i == j ? 0 : suppliers * capture.Direct[i, j] / inputs;
        }
    }
}
