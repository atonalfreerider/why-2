using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Why.Economy.Data;

namespace Why.Economy.Model
{
    /// <summary>A flow of money in one year ($B): a ribbon or a stub of MODALITIES.md section 5.4.</summary>
    public sealed class InstantFlow
    {
        /// <summary>Stable id ("pay", "taxes-households", "other-state"), unique within a year.</summary>
        public string Id;

        /// <summary>What a label or hover calls it.</summary>
        public string Name;

        /// <summary>The nodes it leaves and enters: "classes", "fork", "tiers", "state", "abroad", or "stub" (a loose end:
        /// reinvestment, lenders, saving).</summary>
        public string From, To;

        /// <summary>Dollars a year ($B, current).</summary>
        public double Value;

        /// <summary>The palette role it is drawn in (spec 3.2): "desire", "fear", "people", "owners", "state", "commerce",
        /// "stone".</summary>
        public string Role;

        /// <summary>How it is known (spec 3.3): "measured", "judged", "modelled" or "assumed".</summary>
        public string Basis;

        /// <summary>Where the number comes from: the file, table and line.</summary>
        public string Source;

        /// <summary>What a reader must know to quote it (starts with "≈" for a modelled split).</summary>
        public string Note;

        /// <summary>Each class group's part ($B, in <see cref="InstantYear.Groups"/> order); null when not split by group.</summary>
        public double[] ByGroup;

        /// <summary>True for a grey "other" stub carrying a node's residual over <see cref="InstantModel.NodeTolerance"/>.</summary>
        public bool Other;
    }

    /// <summary>A node of the year's money (a tower, the state, the gate): what enters and leaves it through flows.</summary>
    public sealed class InstantNode
    {
        public string Id, Name;

        /// <summary>Money entering and leaving through its flows, the "other" stub included when it has one ($B).</summary>
        public double In, Out;

        /// <summary>In less out before any "other" stub ($B), and that gap as a share of the larger side.</summary>
        public double Residual, Gap;

        /// <summary>Whether the node must balance. The gate does not: its gap is imports and payments abroad less exports, not
        /// the current account, since income from abroad, compensation and transfers are not flows of the model.</summary>
        public bool Checked;

        /// <summary>The stub that carries <see cref="Residual"/> when the gap is over the tolerance (null otherwise).</summary>
        public InstantFlow Other;
    }

    /// <summary>One floor of the class tower: a household income group's year and its balance sheet.</summary>
    public sealed class InstantGroup
    {
        public string Id, Name, Short;

        /// <summary>Gross income a year ($B): personal income plus social-insurance contributions, the floor's height.</summary>
        public double GrossIncome;

        /// <summary>Where the income comes from ($B): pay from every employer, owning (proprietors', rental, interest and
        /// dividend income), the state (social benefits), other (transfers from business).</summary>
        public double Pay, Owning, State, OtherIncome;

        /// <summary>Owning by kind ($B).</summary>
        public double Proprietors, Rental, Interest, Dividends;

        /// <summary>Uses ($B): personal taxes, contributions, spending (PCE), interest and transfers paid, saving (negative:
        /// borrowing or drawing down assets). They add up to <see cref="GrossIncome"/>.</summary>
        public double Taxes, Contributions, Spending, OtherOutlays, Saving;

        /// <summary>What the group borrowed or drew down (a negative saving) and what it saved ($B).</summary>
        public double Borrowed => Math.Max(0, -Saving);

        public double Saved => Math.Max(0, Saving);

        /// <summary>The source mosaic (area-true across the floor): pay, owning, the state, other, borrowed, as shares of
        /// income plus borrowing.</summary>
        public readonly double[] SourceShares = new double[5];

        /// <summary>The use mosaic of the spending step: toward a want, away from a fear, taxes and contributions, interest
        /// and transfers paid, saving; shares of the same total as the sources.</summary>
        public readonly double[] UseShares = new double[5];

        /// <summary>Households (millions, DFA) and the balance sheet ($B) of <see cref="InstantStocks.Label"/>; NaN without
        /// one. <see cref="Instruments"/> follows <see cref="InstantStocks.Instruments"/>.</summary>
        public double Households = double.NaN, Assets = double.NaN, Liabilities = double.NaN, NetWorth = double.NaN,
            NetWorthShare = double.NaN;

        public double[] Instruments = Array.Empty<double>();

        /// <summary>Gold threads (1 per $1T of listed claims) that reach this row in the balance state.</summary>
        public int Threads;

        /// <summary>Simulated households by main income source (shares of <see cref="InstantModel.Roles"/>: pay, owning, the
        /// state, none), the records behind them (n) and the households they stand for (millions, ≈).</summary>
        public readonly double[] RoleShares = new double[4];

        public int RoleN;
        public double SimulatedHouseholds;

        /// <summary>The people state's dots (1 per 1M households, DFA counts where the year has them) by role.</summary>
        public readonly int[] Dots = new int[4];
    }

    /// <summary>One floor of the tier tower, a market tier of industries.</summary>
    public sealed class InstantTier
    {
        public string Id, Name;

        /// <summary>Value added a year ($B, industries.json): the floor's height.</summary>
        public double ValueAdded;

        /// <summary>Full-time equivalent jobs (millions, industries.json, held at its last point after 2024).</summary>
        public double Jobs;

        /// <summary>The face mosaic ($B): pay, taxes on production, upkeep (≈ estimated depreciation shares), owners.</summary>
        public double Pay, Taxes, Upkeep, Owners;

        /// <summary>The comparison strip (0..1; NaN where the year has no data): output, jobs, corporate profits (NIPA 6.16D by
        /// the crosswalk, assumed), listed market value (the stored firms only: a lower bound).</summary>
        public double OutputShare, JobsShare, ProfitsShare = double.NaN, MarketValueShare = double.NaN;

        /// <summary>Corporate profits and the stored firms' market value in the tier ($B; NaN without data).</summary>
        public double Profits = double.NaN, MarketValue = double.NaN;

        /// <summary>The stored firms in the tier, largest first ("NVIDIA 5.49").</summary>
        public readonly List<string> Firms = new List<string>();
    }

    /// <summary>The state's account in a year (NIPA Table 3.1, all levels of government) and the money it moves.</summary>
    public sealed class InstantState
    {
        /// <summary>The current account ($B): receipts, expenditures (the base's depth), and the borrowing (expenditures less
        /// receipts, the hatched band).</summary>
        public double Receipts, Outlays, Borrowing;

        /// <summary>The total account, investment included ($B): receipts, expenditures, net borrowing (Table 3.1 lines 34, 37
        /// and 43; before 1960, when BEA prints lines 37 and 43 empty, from their parts, which start in 1947).</summary>
        public double TotalReceipts, TotalExpenditures, NetBorrowing;

        /// <summary>The outlays' strata ($B; they add up to <see cref="Outlays"/>): the state's own work (general government
        /// value added) = pay + wear of public capital; supplies bought net of what it sells; benefits and other transfers;
        /// interest; subsidies.</summary>
        public double OwnWork, Pay, Wear, PurchasesNet, Transfers, Interest, Subsidies;

        /// <summary>What it buys from industries ($B): supplies (Table 3.10.5 line 6) and investment (Table 3.1 line 39).</summary>
        public double Purchases;

        /// <summary>The same read from the 2024 use table, government enterprises included (MODALITIES-PLAN decision 22's
        /// $3.16T; $B, 2024 dollars), kept for comparison.</summary>
        public double PurchasesIo;

        /// <summary>General government's full-time equivalent jobs (millions; NaN before 1998, when 6.5D starts).</summary>
        public double Jobs = double.NaN;

        /// <summary>Government enterprises (the Postal Service, transit, public utilities): full-time equivalent jobs (millions,
        /// NIPA 6.5D lines 91 + 96) and pay ($B, Table 6.2D line 86 less Table 3.10.5 line 4); NaN before 1998. Decision 32
        /// counts both with the market's: the pay ribbon carries their pay, and the people state draws their jobs as a
        /// labelled remainder beside the tier floors (industries.json keeps them, and their value added, in its government
        /// tier).</summary>
        public double EnterpriseJobs = double.NaN, EnterprisePay = double.NaN;

        /// <summary>Social benefits to persons, and interest to persons (≈), to business and abroad ($B).</summary>
        public double Benefits, InterestToPersons, InterestToBusiness, InterestAbroad;

        /// <summary>Taxes it takes from households (personal and social insurance) and from industries (production, import and
        /// corporate income taxes), $B.</summary>
        public double TaxesHouseholds, TaxesIndustries;

        /// <summary>Its other receipts (interest and rents, transfers, fees and sales, enterprises' wear) and other outlays
        /// (interest to business, subsidies, transfers abroad, capital transfers, land), $B.</summary>
        public double OtherReceipts, OtherOutlays;

        /// <summary>
        /// In circuit.json's year only ($B; 0 otherwise): how far the drawn taxes and benefits, circuit.json's vintage so the
        /// base and the class tower agree (decision 30), sit from the current Table 3.1, whose vintage the rest of the state's
        /// account comes from. Taxes from households and from industries are Table 3.1 less drawn, benefits drawn less Table
        /// 3.1; their sum is the "≈ vintage difference" flow into the state that closes its node.
        /// </summary>
        public double VintageTaxesHouseholds, VintageTaxesIndustries, VintageBenefits, Vintage;
    }

    /// <summary>One household dollar at the market: the cents that become pay, owners' income, upkeep, imports and taxes.</summary>
    public sealed class InstantDollar
    {
        /// <summary>The parts in the 100-cent bar's order.</summary>
        public static readonly string[] Parts = { "pay", "owners", "upkeep", "imports", "taxes" };

        /// <summary>Exact shares (sum 1) and whole cents (sum 100, largest remainders).</summary>
        public readonly double[] Shares = new double[5];

        public readonly int[] Cents = new int[5];

        /// <summary>The ledger's parts before normalising (1 up to the Leontief inverse's rounding).</summary>
        public double RawSum;

        /// <summary>Whose spending mix weighs the categories ("simulated households 2025" or the file's).</summary>
        public string Weights;
    }

    /// <summary>The money between industries (the stored use table, in the money direction: buyer to supplier).</summary>
    public sealed class InstantBetween
    {
        /// <summary>The use table's year (2024), assumed for every year (spec 5.9).</summary>
        public int Year;

        /// <summary>$B paid by the buyer tier (row) to the supplier tier (column), the state as tier 0, then raw, make,
        /// services, tech; an industry's purchases from itself are left out.</summary>
        public readonly double[,] Matrix = new double[5, 5];

        /// <summary>Totals ($B) and shares (0..1): with the state as the bottom tier, and among market tiers.</summary>
        public double Total, Up, Down, Within, MarketTotal, MarketUp, MarketDown, MarketWithin;

        /// <summary>At each market boundary (raw|make, make|services, services|tech): money crossing it downward and upward
        /// ($B), the state left out.</summary>
        public readonly double[] BoundaryDown = new double[3], BoundaryUp = new double[3];

        /// <summary>Tech's purchases from the tiers below, its sales to them and to the state ($B).</summary>
        public double TechBuys, TechSells, TechToState;

        /// <summary>Purchases inside one industry (left out) and the state's purchases of supplies ($B).</summary>
        public double OwnIndustry, StateSupplies;

        /// <summary>The factor that carries the table's dollars to the year shown (market value added, year over table year).</summary>
        public double ScaleToYear = 1;
    }

    /// <summary>The balance sheet of a year (DFA by income group), listed value, the state's debt and the threads.</summary>
    public sealed class InstantStocks
    {
        /// <summary>Which DFA figures: "2026:Q1" (the latest quarter, for 2025 on) or "1990 (annual mean)"; empty without.</summary>
        public string Label = "";

        /// <summary>The DFA instruments in the groups' order of <see cref="InstantGroup.Instruments"/>, with their names and
        /// whether each is a debt.</summary>
        public string[] Instruments = Array.Empty<string>(), InstrumentNames = Array.Empty<string>();

        public bool[] Liability = Array.Empty<bool>();

        /// <summary>Totals over the groups ($B; households in millions; NaN without data).</summary>
        public double Assets = double.NaN, Liabilities = double.NaN, NetWorth = double.NaN, Households = double.NaN;

        /// <summary>Listed US companies (the Wilshire 5000 total, $B), the part the stored firms do not split by industry, and
        /// the threads: 1 per $1T, those to holders abroad (Fed Z.1's foreign share). NaN and 0 outside the snapshot's years.</summary>
        public double Listed = double.NaN, ListedNotSplit = double.NaN;

        public int Threads, ThreadsAbroad;

        /// <summary>Federal debt held by the public and its holders ($B): the Federal Reserve, abroad, other US holders.</summary>
        public double PublicDebt = double.NaN, PublicDebtFed = double.NaN, PublicDebtAbroad = double.NaN, PublicDebtOther = double.NaN;

        public string PublicDebtAsOf = "";
    }

    /// <summary>Whether a year has one kind of data (spec 5.9), with the note the view prints when it does not.</summary>
    public sealed class InstantCoverage
    {
        public string Id, Note;
        public bool Covered;
    }

    /// <summary>A number the model publishes to <see cref="ModelFacts"/> (Text for a word or a date).</summary>
    public sealed class InstantFact
    {
        public string Key, Unit, Source, Basis, Text;
        public double Value = double.NaN;
    }

    /// <summary>One year of the US economy as M1 draws it (built by <see cref="InstantModel"/>; read-only once built).</summary>
    public sealed class InstantYear
    {
        public readonly int Year;

        /// <summary>The year of the national accounts drawn: the year itself, or the tables' last year after it (2026 shows
        /// 2025, spec 5.9).</summary>
        public int FlowYear;

        /// <summary>The flows use circuit.json's vintage (its year, decision 30), else instant.json's NIPA tables.</summary>
        public bool CircuitVintage;

        /// <summary>The value added is an estimate (industries.json: H1 annualized).</summary>
        public bool Estimate;

        public readonly List<InstantCoverage> Coverage = new List<InstantCoverage>();

        /// <summary>The class tower's floors: six income groups, or one undivided block before BEA's distribution starts.</summary>
        public InstantGroup[] Groups = Array.Empty<InstantGroup>();

        public bool ClassSplit;

        /// <summary>Where the class statements come from (the source of every flow they split).</summary>
        public string StatementSource = "";

        /// <summary>Gross income of all households and personal income plus contributions in the same vintage ($B).</summary>
        public double ClassTotal, PersonalIncomePlusContributions;

        public readonly InstantTier[] Tiers = new InstantTier[InstantModel.Tiers];

        /// <summary>Value added of the market tiers and of government (industries.json), and GDP (its gdp series), $B.</summary>
        public double MarketValueAdded, PublicValueAdded, Gdp;

        public readonly InstantState State = new InstantState();

        public readonly List<InstantFlow> Flows = new List<InstantFlow>();

        public readonly List<InstantNode> Nodes = new List<InstantNode>();

        public readonly InstantDollar Dollar = new InstantDollar();

        /// <summary>Households' spending (PCE as the class statements add it up) and NIPA's PCE in the same vintage, $B.</summary>
        public double Pce, NipaPce;

        /// <summary>Spending's fear share (judged items shrunk to their category's prior, spending-weighted), the judgments'
        /// spending-weighted confidence and the share of spending judged.</summary>
        public double FearShare, FearConfidence, JudgedShare;

        /// <summary>Exports and imports of goods and services, private fixed investment ($B, NIPA Table 1.1.5).</summary>
        public double Exports, Imports, Investment;

        /// <summary>
        /// The share of households' spending the imports branch peels off at the fork, and the factor it carries from the
        /// household dollar's imports share. In the ledger's year (circuit.json's) it is the dollar's own share, factor 1; other
        /// years scale it by their imports' share of GDP against that year's (≈), since at the ledger's 2024 input-output
        /// ratios households alone would import more than the whole economy did in every year to 1973. The 100-cent bar keeps
        /// the ledger's share (spec 5.9: the 2024 table assumed for every year). Capped is true when even the scaled share
        /// would pass all imports and the branch is held at them (a problem of the year).
        /// </summary>
        public double ImportsShare, ImportsScale = 1;

        public bool ImportsCapped;

        /// <summary>
        /// The tier node's value-added content ($B): its out-flows (the income side) and in-flows (final demand) less the
        /// imports bought by firms and the state, and each as a share above <see cref="MarketValueAdded"/>. Spec 5.10 asks
        /// for in = value added = out within 1%; spec 5.4's gross flows give 4.6% in 2025, so it is reported (the load logs a
        /// warning past 1%) until the spec settles which (MODALITIES-PLAN WP10 review hand-off).
        /// </summary>
        public double TierIncome, TierDemand, TierIncomeGap, TierDemandGap;

        /// <summary>
        /// The people state's jobs (millions): the tier floors (industries.json), government enterprises (with the market, a
        /// labelled remainder) and general government (the base), NaN before NIPA 6.5D starts (1998); and 6.5D's own count
        /// of every industry's full-time equivalents (line 1), which industries.json's market jobs need not match.
        /// </summary>
        public double JobsDrawn = double.NaN, JobsNipa = double.NaN;

        public readonly InstantBetween Between = new InstantBetween();

        public readonly InstantStocks Stocks = new InstantStocks();

        /// <summary>The frontier (latest fiscal years of the stored tech-tier firms): capital spending, research ($B), firms.</summary>
        public double FrontierCapex = double.NaN, FrontierResearch = double.NaN;

        public int FrontierFirms;

        /// <summary>The five hyperscalers' capital spending, calendar 2025, on one definition and as each firm's headline ($B).</summary>
        public double HyperscalerCapex = double.NaN, HyperscalerHeadline = double.NaN;

        /// <summary>The attention loop: digital ads, creators' ad income ($B), minutes a day on social media; the cloud's
        /// people (US, 10M+ followers), the capital holders among them and the most followed.</summary>
        public double Ads = double.NaN, Creators = double.NaN, Minutes = double.NaN;

        public int CloudPeople;
        public readonly List<string> CapitalHolders = new List<string>(), MostFollowed = new List<string>();

        /// <summary>What does not hold, one line each; each is logged as an error at load.</summary>
        public readonly List<string> Problems = new List<string>();

        public readonly List<InstantFact> Facts = new List<InstantFact>();

        /// <summary>A hash of every number above: two builds of a year agree exactly when theirs do.</summary>
        public string Checksum = "";

        /// <summary>How long the build took (ms, on the calling thread).</summary>
        public double BuildMs;

        public InstantYear(int year) => Year = year;

        /// <summary>The flow with this id (null when the year has none).</summary>
        public InstantFlow Flow(string id)
        {
            foreach (InstantFlow f in Flows)
            {
                if (f.Id == id) return f;
            }

            return null;
        }

        /// <summary>The node with this id (null when the year has none).</summary>
        public InstantNode Node(string id)
        {
            foreach (InstantNode n in Nodes)
            {
                if (n.Id == id) return n;
            }

            return null;
        }

        /// <summary>Whether the year has a kind of data ("classes", "balance", "cloud", ...).</summary>
        public bool Covers(string id)
        {
            foreach (InstantCoverage c in Coverage)
            {
                if (c.Id == id) return c.Covered;
            }

            return false;
        }

        /// <summary>A flow's value ($B; 0 when the year has none).</summary>
        public double ValueOf(string id) => Flow(id)?.Value ?? 0;
    }

    /// <summary>
    /// The US economy in one year, for the view "One year, all at once" (M1; MODALITIES.md sections 5.4, 5.5 and 5.10): the
    /// households ranked by income in the Fed DFA's six groups (one undivided block before BEA's distribution of personal
    /// income starts), the four market tiers, the state and the rest of the world; every flow of section 5.4 in dollars with
    /// its colour role, provenance and per-group parts; one household dollar's split at the market; the money between
    /// industries in the money direction; the comparison strip; the balance sheet with the threads; households by main income
    /// source; the frontier and the attention loop. Built per year on demand and cached (<see cref="Year"/>), from the data
    /// alone: instant.json, circuit.json, industries.json and spending.json (<see cref="EconomyData"/>), the item judgments
    /// through <see cref="ValueCapture"/>, corporate-programs.json, people.json, and the simulated lives for the spending mix
    /// and the head counts. Pure and deterministic; safe on any thread.
    /// <para><see cref="InstantYear.Problems"/> lists the identities that fail (spec 5.10: nodes balance, the class total,
    /// the tiers' mosaics and value added against GDP, PCE, the direction between industries against its captions, every
    /// fact with a source and a year); the loader logs each as an error. The tier node's value added against its flows is
    /// reported, not enforced (<see cref="InstantYear.TierIncomeGap"/>). <see cref="Publish"/> puts a year's numbers into
    /// <see cref="ModelFacts"/> under <c>instant.*</c>.</para>
    /// </summary>
    /// <remarks>
    /// One vintage of the personal and business accounts per year (MODALITIES-PLAN decision 30): circuit.json's year (2025)
    /// draws circuit.json's accounts, the hills' calibration from before BEA's September 2026 update (PCE $20.95T; the class
    /// statements are instant.json's group shares on them, classes.flows.circuitVintage); every other year draws instant.json's
    /// NIPA tables, the current vintage. What circuit.json does not hold (the state's account, foreign transactions,
    /// investment) comes from the tables in 2025 too, so the state's node takes a labelled "≈ vintage difference" flow: how
    /// far the drawn taxes and benefits sit from the current Table 3.1's. The state's base pays general government only
    /// (decision 32): its pay ($2.46T in 2025) and its full-time equivalent jobs; government enterprises' pay and jobs count
    /// with the market.
    /// </remarks>
    public sealed class InstantModel
    {
        /// <summary>Resources path of instant.json (Tools/instant/build.py; Docs/data/instant.md).</summary>
        public const string Path = "Data/economy/instant";

        /// <summary>Resources path of the attention cloud's people (Data/social/people.json).</summary>
        public const string PeoplePath = "Data/social/people";

        /// <summary>The key the loader shares the scene's model under (GraphContext.Share).</summary>
        public const string SharedKey = "economy.instant";

        /// <summary>Every text the model reads besides <see cref="EconomyData"/>'s (a layer's RequiredTexts).</summary>
        public static readonly string[] Paths = { Path, ValueCapture.JudgmentsPath, CorporatePrograms.Path, PeoplePath };

        /// <summary>The market tiers of the tier tower, bottom to top (industries.json ids; "gov" is the base).</summary>
        public static readonly string[] TierIds = { "raw", "make", "services", "tech" };

        /// <summary>How the floors are named on screen (spec 5.2).</summary>
        public static readonly string[] TierNames = { "RAW MATERIALS", "MANUFACTURING & INFRASTRUCTURE", "SERVICES", "TECH" };

        public const int Tiers = 4;

        /// <summary>Main income sources of the people state's dots (spec 5.8).</summary>
        public static readonly string[] Roles = { "pay", "owning", "the state", "none" };

        /// <summary>The first and last year a model can be built for (value added 1947-2026).</summary>
        public const int FirstYear = 1947, LastYear = 2026;

        /// <summary>The share of a node's larger side its in and out may differ by; past it an "other" stub carries the rest
        /// (spec 5.4: never forced).</summary>
        public const double NodeTolerance = 0.01;

        /// <summary>A node whose gap passes this share still balances, but near <see cref="NodeTolerance"/>: the load warns,
        /// so a data refresh that would push it over and draw an unexplained "other" stub is seen first.</summary>
        public const double NodeMargin = 0.008;

        /// <summary>The class tower's total against personal income plus contributions (spec 5.10).</summary>
        public const double ClassTotalTolerance = 0.005;

        /// <summary>Households' spending against NIPA's PCE, and the tiers' mosaics against their value added (the shares fit
        /// in the floor: owners' share is clamped at 0, so the mosaic overruns only where compShare + taxShare + depShare
        /// pass 1).</summary>
        public const double PceTolerance = 0.001, MosaicTolerance = 0.01;

        /// <summary>Household debt drawn: every DFA liability, $20.0T at 2026:Q1 within 3% (decision 29).</summary>
        public const double DebtTarget = 20000, DebtTolerance = 0.03;

        /// <summary>The equity share of pension entitlements in the threads (spec 5.7 "≈ pensions 45% equity"; circuit.json
        /// equityHoldersNote: recalled ICI and Z.1 asset mixes).</summary>
        public const double PensionEquity = 0.45;

        /// <summary>One gold thread is this much listed value ($B).</summary>
        public const double ThreadValue = 1000;

        /// <summary>The cloud's floor: US people with at least this many followers (spec 5.2).</summary>
        public const double CloudFollowers = 10e6;

        /// <summary>A year's build on a worker should take less than this (ms; MODALITIES-PLAN WP10).</summary>
        public const double BuildBudgetMs = 40;

        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        /// <summary>The nine components of a class statement, in instant.json's order (classes.flows.components).</summary>
        static readonly string[] Components =
            { "compensation", "proprietors", "rental", "interest", "dividends", "benefits", "fromBusiness", "contributions", "taxes" };

        /// <summary>Each component's line in NIPA Table 2.1 (scaling the shares of a year).</summary>
        static readonly int[] ComponentLines = { 2, 9, 12, 14, 15, 17, 24, 25, 26 };

        /// <summary>
        /// What the captions say about the money between industries (MODALITIES.md 5.5 and section 2; MODALITIES-PLAN decision
        /// 21 and WP10's acceptance), at the precision they print. <see cref="InstantYear.Problems"/> lists any the stored use
        /// table no longer gives, so a rebuilt circuit.json cannot leave a stale caption on screen.
        /// </summary>
        static readonly (string what, double value, int decimals, Func<InstantBetween, double> of)[] CaptionFacts =
        {
            ("up, the state as tier 0 (%)", 37.2, 1, b => 100 * b.Up),
            ("down (%)", 21.9, 1, b => 100 * b.Down),
            ("within (%)", 41.0, 1, b => 100 * b.Within),
            ("up among market tiers (%)", 27.7, 1, b => 100 * b.MarketUp),
            ("down among market tiers (%)", 24.9, 1, b => 100 * b.MarketDown),
            ("within among market tiers (%)", 47.4, 1, b => 100 * b.MarketWithin),
            ("raw|make down ($T)", 1.15, 2, b => b.BoundaryDown[0] / 1000),
            ("raw|make up ($T)", 0.60, 2, b => b.BoundaryUp[0] / 1000),
            ("make|services down ($T)", 1.76, 2, b => b.BoundaryDown[1] / 1000),
            ("make|services up ($T)", 2.88, 2, b => b.BoundaryUp[1] / 1000),
            ("services|tech down ($T)", 0.89, 2, b => b.BoundaryDown[2] / 1000),
            ("services|tech up ($T)", 0.80, 2, b => b.BoundaryUp[2] / 1000),
            ("tech buys from the tiers below ($T)", 0.89, 2, b => b.TechBuys / 1000),
            ("tech sells to them ($T)", 0.80, 2, b => b.TechSells / 1000),
            ("tech sells to the state ($T)", 0.29, 2, b => b.TechToState / 1000),
            ("between industries ($T)", 15.85, 2, b => b.Total / 1000)
        };

        readonly EconomyData data;
        readonly EconomicLives lives;
        readonly ValueCapture capture;
        readonly Dictionary<int, InstantYear> cache = new Dictionary<int, InstantYear>();
        readonly object gate = new object();

        /// <summary>What could not be read at construction (each is a problem of every year).</summary>
        readonly List<string> loadProblems = new List<string>();

        // ---- instant.json, read once (immutable after construction)
        sealed class NipaLine
        {
            public int First;
            public double[] Values;
        }

        readonly Dictionary<(string table, int line), NipaLine> nipa = new Dictionary<(string, int), NipaLine>();
        string nipaVintage = "", classBasis = "";
        int nipaLast, sourceYear;
        (string id, string name, string shortName)[] groups = Array.Empty<(string, string, string)>();

        /// <summary>circuitVintage: the components (9 x groups), PCE, other outlays and saving by group, $B.</summary>
        double[][] cvComponents = Array.Empty<double[]>();
        double[] cvPce = Array.Empty<double>(), cvOther = Array.Empty<double>(), cvSaving = Array.Empty<double>();

        /// <summary>The main block's interest and transfers paid by group: how a byYear year splits them (as circuitVintage).</summary>
        double[] otherOutlayShares = Array.Empty<double>();

        /// <summary>byYear: income component shares [component][year - incomeFirst][group]; PCE shares [year - spendFirst][group].</summary>
        int incomeFirst, incomeLast, spendFirst, spendLast;
        double[][][] byYearIncome = Array.Empty<double[][]>();
        double[][] byYearPce = Array.Empty<double[]>();

        string dfaLatestQuarter = "", dfaSource = "";
        int dfaFirst, dfaLast;
        (string id, string name, bool liability)[] instruments = Array.Empty<(string, string, bool)>();
        Dictionary<string, double>[] dfaLatest = Array.Empty<Dictionary<string, double>>();
        Dictionary<string, double[]>[] dfaAnnual = Array.Empty<Dictionary<string, double[]>>();

        (string tier, int[] plus, int[] minus)[] profitsCrosswalk = Array.Empty<(string, int[], int[])>();
        double listedTechShareStored = double.NaN;
        string listedDate = "";
        double debtHeld = double.NaN, debtFed = double.NaN, debtAbroad = double.NaN, debtOther = double.NaN;
        string debtAsOf = "";
        double capexReconciled = double.NaN, capexHeadline = double.NaN;
        string capexYear = "";

        // ---- the other files
        readonly Dictionary<string, double> judgedConfidence = new Dictionary<string, double>(StringComparer.Ordinal);
        double frontierCapex = double.NaN, frontierResearch = double.NaN, ads = double.NaN, creators = double.NaN, minutes = double.NaN;
        int frontierFirms, cloudPeople;

        /// <summary>The years the frontier firms' fiscal years end in ("2025-2026"), from their filings' periods.</summary>
        string frontierYears = "";
        readonly List<string> capitalHolders = new List<string>(), mostFollowed = new List<string>();

        /// <summary>How long reading the files took (ms).</summary>
        public readonly double ParseMs;

        /// <summary>The ledger the household dollar is split with (the model's own <see cref="ValueCapture"/>, built from the
        /// same data and judgments as the capture view's).</summary>
        public ValueCapture Capture => capture;

        /// <summary>
        /// Reads every input once. <paramref name="text"/> returns a Resources text by path (GraphContext.Text), or null when it
        /// is missing; a missing or unreadable file leaves its part of the model empty and is listed in every year's problems.
        /// <paramref name="lives"/> may be null or not ready: the spending mix then falls back to spending.json's and there
        /// are no head counts.
        /// </summary>
        public InstantModel(EconomyData data, EconomicLives lives, Func<string, string> text)
        {
            Stopwatch sw = Stopwatch.StartNew();
            this.data = data ?? throw new ArgumentNullException(nameof(data));
            this.lives = lives;
            string judgments = text?.Invoke(ValueCapture.JudgmentsPath), corporate = text?.Invoke(CorporatePrograms.Path);
            string people = text?.Invoke(PeoplePath), instant = text?.Invoke(Path);
            // the side files do not depend on instant.json: read beside it, so a load waits on the longest, not the sum
            Task side = Task.Run(() =>
            {
                Guarded("corporate-programs.json", () => ReadCorporate(corporate));
                Guarded("people.json", () => ReadPeople(people));
            });
            capture = new ValueCapture(data, judgments ?? "");
            Guarded("instant.json", () => ReadInstant(instant));
            Guarded("purchase-judgments.json", () => ReadJudgments(judgments));
            side.Wait();
            ParseMs = sw.Elapsed.TotalMilliseconds;
        }

        /// <summary>
        /// The year's model (clamped to <see cref="FirstYear"/>..<see cref="LastYear"/>), built the first time it is asked
        /// for and cached; any thread.
        /// </summary>
        public InstantYear Year(int year)
        {
            year = Math.Max(FirstYear, Math.Min(LastYear, year));
            lock (gate)
            {
                if (cache.TryGetValue(year, out InstantYear built)) return built;
                InstantYear y = Build(year);
                cache[year] = y;
                return y;
            }
        }

        /// <summary>A fresh build of a year that bypasses the cache (to show that two builds agree).</summary>
        public InstantYear Rebuild(int year) => Build(Math.Max(FirstYear, Math.Min(LastYear, year)));

        /// <summary>The problems of a year (built if needed).</summary>
        public IReadOnlyList<string> Problems(int year) => Year(year).Problems;

        /// <summary>
        /// Publishes a year's facts to <see cref="ModelFacts"/> under <c>instant.*</c> (one year only, so the keys carry no
        /// year: the loader publishes the default year's). Returns how many were refused.
        /// </summary>
        public static int Publish(InstantYear y)
        {
            int refused = 0;
            foreach (InstantFact f in y.Facts)
            {
                bool ok = f.Text != null
                    ? ModelFacts.Set(f.Key, f.Text, f.Unit, f.Source, f.Basis)
                    : ModelFacts.Set(f.Key, f.Value, f.Unit, f.Source, f.Basis);
                if (!ok) refused++;
            }

            return refused;
        }

        // =================================================================================================== reading

        void Guarded(string what, Action read)
        {
            try
            {
                read();
            }
            catch (Exception e)
            {
                lock (loadProblems) loadProblems.Add($"{what} unreadable: {e.GetType().Name}: {e.Message}");
            }
        }

        static double Num(JToken t) =>
            t != null && (t.Type == JTokenType.Float || t.Type == JTokenType.Integer) ? t.Value<double>() : double.NaN;

        static double[] Nums(JToken t)
        {
            if (!(t is JArray a)) return Array.Empty<double>();
            double[] v = new double[a.Count];
            for (int i = 0; i < a.Count; i++) v[i] = Num(a[i]);
            return v;
        }

        static int[] Ints(JToken t)
        {
            if (!(t is JArray a)) return Array.Empty<int>();
            int[] v = new int[a.Count];
            for (int i = 0; i < a.Count; i++) v[i] = (int)Num(a[i]);
            return v;
        }

        static JObject Parse(string json) => JObject.Parse(json.TrimStart('\uFEFF'));

        /// <summary>The last year (four digits standing alone, 1900-2099) a text names, or 0: a filing's period, a fact's source.</summary>
        static int LastYearIn(string text)
        {
            int found = 0;
            if (string.IsNullOrEmpty(text)) return 0;
            for (int i = 0; i + 4 <= text.Length; i++)
            {
                if (i > 0 && IsDigit(text[i - 1]) || i + 4 < text.Length && IsDigit(text[i + 4])) continue;
                if (!IsDigit(text[i]) || !IsDigit(text[i + 1]) || !IsDigit(text[i + 2]) || !IsDigit(text[i + 3])) continue;
                int y = (text[i] - '0') * 1000 + (text[i + 1] - '0') * 100 + (text[i + 2] - '0') * 10 + (text[i + 3] - '0');
                if (y >= 1900 && y <= 2099) found = y;
            }

            return found;
        }

        static bool IsDigit(char c) => c >= '0' && c <= '9';

        void ReadInstant(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                loadProblems.Add($"missing '{Path}' (instant.json): no class statements, state accounts or balance sheets");
                return;
            }

            JObject root = Parse(json);
            JToken classes = root["classes"];
            classBasis = (string)classes?["classBasis"] ?? "";
            if (classes?["groups"] is JArray gs)
            {
                groups = new (string, string, string)[gs.Count];
                for (int g = 0; g < gs.Count; g++) groups[g] = ((string)gs[g]["id"], (string)gs[g]["name"], (string)gs[g]["short"] ?? (string)gs[g]["name"]);
            }

            // the class statements: 2025 on circuit.json's totals (decision 30), and every BEA year's shares
            JToken flows = classes?["flows"];
            sourceYear = (int)Num(flows?["sourceYear"]);
            JToken cv = flows?["circuitVintage"];
            cvComponents = new double[Components.Length][];
            for (int k = 0; k < Components.Length; k++) cvComponents[k] = Nums(cv?["components"]?[Components[k]]);
            cvPce = Nums(cv?["derived"]?["pce"]);
            cvOther = Nums(cv?["derived"]?["otherOutlays"]);
            cvSaving = Nums(cv?["derived"]?["saving"]);
            otherOutlayShares = Shares(Nums(flows?["derived"]?["otherOutlays"]?["byGroup"]));

            JToken income = flows?["byYear"]?["income"], spending = flows?["byYear"]?["spending"];
            int[] iy = Ints(income?["years"]), sy = Ints(spending?["years"]);
            if (iy.Length == 2 && sy.Length == 2)
            {
                incomeFirst = iy[0];
                incomeLast = iy[1];
                spendFirst = sy[0];
                spendLast = sy[1];
                byYearIncome = new double[Components.Length][][];
                for (int k = 0; k < Components.Length; k++) byYearIncome[k] = Rows(income["components"]?[Components[k]]);
                byYearPce = Rows(spending["series"]?["pce"]);
            }

            // the balance sheets: the latest quarter and the annual means, by income group and instrument
            JToken dfa = classes?["dfa"];
            dfaSource = (string)dfa?["source"] ?? "";
            if (dfa?["instruments"] is JArray ins)
            {
                instruments = new (string, string, bool)[ins.Count];
                for (int i = 0; i < ins.Count; i++) instruments[i] = ((string)ins[i]["id"], (string)ins[i]["name"], (string)ins[i]["side"] == "liability");
            }

            dfaLatestQuarter = (string)dfa?["latest"]?["quarter"] ?? "";
            int[] ay = Ints(dfa?["annual"]?["years"]);
            if (ay.Length == 2)
            {
                dfaFirst = ay[0];
                dfaLast = ay[1];
            }

            dfaLatest = new Dictionary<string, double>[groups.Length];
            dfaAnnual = new Dictionary<string, double[]>[groups.Length];
            for (int g = 0; g < groups.Length; g++)
            {
                dfaLatest[g] = new Dictionary<string, double>(StringComparer.Ordinal);
                dfaAnnual[g] = new Dictionary<string, double[]>(StringComparer.Ordinal);
                if (dfa?["latest"]?["groups"]?[groups[g].id] is JObject lo)
                {
                    foreach (JProperty p in lo.Properties()) dfaLatest[g][p.Name] = Num(p.Value);
                }

                if (dfa?["annual"]?["groups"]?[groups[g].id] is JObject ao)
                {
                    foreach (JProperty p in ao.Properties()) dfaAnnual[g][p.Name] = Nums(p.Value);
                }
            }

            // NIPA: every stored line of every table, annual from its first year
            JToken n = root["nipa"];
            nipaVintage = (string)n?["vintage"] ?? "";
            if (n?["tables"] is JObject tables)
            {
                foreach (JProperty t in tables.Properties())
                {
                    if (!(t.Value["lines"] is JObject lines)) continue;
                    foreach (JProperty l in lines.Properties())
                    {
                        if (!int.TryParse(l.Name, NumberStyles.Integer, Ci, out int line)) continue;
                        nipa[(t.Name, line)] = new NipaLine { First = (int)Num(l.Value["first"]), Values = Nums(l.Value["values"]) };
                    }
                }
            }

            if (nipa.TryGetValue(("T20100", 1), out NipaLine pi)) nipaLast = pi.First + pi.Values.Length - 1;

            if (root["profitsByTier"]?["crosswalk"] is JArray cw)
            {
                profitsCrosswalk = new (string, int[], int[])[cw.Count];
                for (int i = 0; i < cw.Count; i++) profitsCrosswalk[i] = ((string)cw[i]["tier"], Ints(cw[i]["plus"]), Ints(cw[i]["minus"]));
            }

            listedDate = (string)root["listedValue"]?["total"]?["year"] ?? "";
            if (root["listedValue"]?["fallback"]?["tiers"] is JArray lt)
            {
                foreach (JToken t in lt)
                {
                    if ((string)t["tier"] == "tech") listedTechShareStored = Num(t["share"]);
                }
            }

            JToken debt = root["publicDebt"];
            debtAsOf = (string)debt?["asOf"] ?? "";
            debtHeld = Num(debt?["heldByPublic"]?["value"]);
            debtFed = Num(debt?["federalReserve"]?["value"]);
            debtAbroad = Num(debt?["foreign"]?["value"]);
            debtOther = Num(debt?["otherUsHolders"]?["value"]);

            JToken capex = root["hyperscalerCapex"];
            capexReconciled = Num(capex?["value"]);
            capexYear = (string)capex?["year"] ?? "";
            if (capex?["alternatives"] is JArray alt && alt.Count > 0) capexHeadline = Num(alt[0]["value"]);
        }

        /// <summary>Rows of numbers (a byYear series: one row of group shares per year).</summary>
        static double[][] Rows(JToken t)
        {
            if (!(t is JArray a)) return Array.Empty<double[]>();
            double[][] rows = new double[a.Count][];
            for (int i = 0; i < a.Count; i++) rows[i] = Nums(a[i]);
            return rows;
        }

        static double[] Shares(double[] v)
        {
            double sum = 0;
            foreach (double x in v) sum += double.IsNaN(x) ? 0 : x;
            double[] s = new double[v.Length];
            for (int i = 0; i < v.Length; i++) s[i] = sum != 0 && !double.IsNaN(v[i]) ? v[i] / sum : 0;
            return s;
        }

        /// <summary>
        /// The confidence behind the fear split of each judged item: the lower of Jev's confidences in the two levers the
        /// fear share is made of (fear sold, manufactured want; <see cref="ValueCapture"/>).
        /// </summary>
        void ReadJudgments(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return;
            if (!(Parse(json)["items"] is JObject items)) return;
            foreach (JProperty p in items.Properties())
            {
                double c = Math.Min(Num(p.Value["fear_soldConfidence"]), Num(p.Value["manufacturedConfidence"]));
                if (!double.IsNaN(c)) judgedConfidence[p.Name] = c;
            }
        }

        /// <summary>
        /// The frontier (spec 5.2: the stored tech-tier firms' capital spending and research, latest fiscal year) and the
        /// attention loop's money and time (economy block: IAB/PwC digital ads, IAB creator ad spend, eMarketer minutes).
        /// </summary>
        void ReadCorporate(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            JObject root = Parse(json);
            double capex = 0, research = 0;
            int firms = 0, firstEnd = int.MaxValue, lastEnd = 0;
            if (root["firms"] is JArray fs)
            {
                foreach (JToken f in fs)
                {
                    Industry ind = data.IndustryById((string)f["industry"]);
                    if (ind?.TierId != "tech") continue;
                    double c = Num(f["capex"]), r = Num(f["rd"]);
                    capex += double.IsNaN(c) ? 0 : c;
                    research += double.IsNaN(r) ? 0 : r;
                    firms++;
                    // the period names its end date last ("FY2026 (52 weeks ended 2026-01-25)"): its year is the fiscal year's
                    int end = LastYearIn((string)f["period"]);
                    if (end <= 0) continue;
                    firstEnd = Math.Min(firstEnd, end);
                    lastEnd = Math.Max(lastEnd, end);
                }
            }

            if (firms > 0)
            {
                frontierCapex = capex;
                frontierResearch = research;
                frontierFirms = firms;
                if (lastEnd > 0) frontierYears = firstEnd == lastEnd ? lastEnd.ToString(Ci) : firstEnd.ToString(Ci) + "-" + lastEnd.ToString(Ci);
            }

            JToken e = root["economy"];
            ads = Num(e?["digitalAdRevenue"]?["total"]?["value"]);
            creators = Num(e?["influencerMarketing"]?["usCreatorEconomyAdSpend_IAB"]?["value"]);
            minutes = Num(e?["socialMediaTime"]?["usAdultSocialNetworkUsers_eMarketer"]?["value"]);
        }

        /// <summary>The cloud (spec 5.2, 2025 snapshot): US people with 10M+ followers, the capital holders, the most followed.</summary>
        void ReadPeople(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            if (!(Parse(json)["people"] is JArray people)) return;
            List<(double fol, string name)> cloud = new List<(double, string)>();
            foreach (JToken p in people)
            {
                double fol = Num(p["fol"]);
                if ((string)p["region"] != "us" || (string)p["kind"] != "p" || !(fol >= CloudFollowers)) continue;
                string name = (string)p["name"] ?? "";
                cloud.Add((fol, name));
                if (p["firms"] is JArray firms && firms.Count > 0) capitalHolders.Add(name);
            }

            cloud.Sort((a, b) => a.fol != b.fol ? b.fol.CompareTo(a.fol) : string.CompareOrdinal(a.name, b.name));
            cloudPeople = cloud.Count;
            for (int i = 0; i < cloud.Count && i < 12; i++) mostFollowed.Add(cloud[i].name + " " + (cloud[i].fol / 1e6).ToString("0", Ci) + "M");
        }

        /// <summary>A NIPA line's first year (0 when the table does not hold it).</summary>
        int First(string table, int line) => nipa.TryGetValue((table, line), out NipaLine l) ? l.First : 0;

        /// <summary>A NIPA line's value in a year ($B or million persons; NaN when the table has no value).</summary>
        double L(string table, int line, int year)
        {
            if (!nipa.TryGetValue((table, line), out NipaLine l)) return double.NaN;
            int i = year - l.First;
            return i >= 0 && i < l.Values.Length ? l.Values[i] : double.NaN;
        }

        static double Z(double v) => double.IsNaN(v) ? 0 : v;

        double CircuitPersonal(string key) => data.Circuit?.PersonalOf(key) ?? 0;

        string NipaSource(string what) => $"BEA NIPA {what} (instant.json nipa.tables; {nipaVintage})";

        // =================================================================================================== building

        /// <summary>The year's national accounts behind the class statements and the flows ($B, one vintage).</summary>
        struct Personal
        {
            public double Compensation, Proprietors, Rental, Interest, Dividends, Benefits, FromBusiness, Contributions, Taxes;
            public double Pce, Saving, InterestPaid, TransfersPaid, PersonalIncome;
            public string Source;
        }

        InstantYear Build(int year)
        {
            Stopwatch sw = Stopwatch.StartNew();
            InstantYear r = new InstantYear(year);
            r.FlowYear = nipaLast > 0 ? Math.Min(year, nipaLast) : year;
            r.CircuitVintage = data.Circuit != null && r.FlowYear == data.Circuit.Year && data.Circuit.Personal.Count > 0 &&
                               cvPce.Length == groups.Length && groups.Length > 0;
            r.Estimate = data.IsEstimate(year);
            Personal p = Accounts(r);
            BuildTiers(r);
            BuildClasses(r, p);
            BuildSpending(r, p);
            Mosaics(r);
            BuildState(r, p);
            BuildImports(r);
            BuildFlows(r, p);
            BuildNodes(r);
            BuildBetween(r);
            BuildStrip(r);
            BuildStocks(r);
            BuildRoles(r);
            BuildSnapshots(r);
            BuildCoverage(r);
            Check(r, p);
            BuildFacts(r);
            r.Checksum = Checksum(r);
            r.BuildMs = sw.Elapsed.TotalMilliseconds;
            return r;
        }

        /// <summary>The personal accounts of the flows' year: circuit.json's in its year (decision 30), else NIPA Table 2.1.</summary>
        Personal Accounts(InstantYear r)
        {
            int y = r.FlowYear;
            if (r.CircuitVintage)
            {
                return new Personal
                {
                    Compensation = CircuitPersonal("compensation"), Proprietors = CircuitPersonal("proprietors"),
                    Rental = CircuitPersonal("rental"), Interest = CircuitPersonal("interest"), Dividends = CircuitPersonal("dividends"),
                    Benefits = CircuitPersonal("governmentSocialBenefits"), FromBusiness = CircuitPersonal("transfersFromBusiness"),
                    Contributions = CircuitPersonal("socialInsuranceContributions"), Taxes = CircuitPersonal("personalTaxes"),
                    Pce = CircuitPersonal("pce"), Saving = CircuitPersonal("saving"), InterestPaid = CircuitPersonal("interestPaid"),
                    TransfersPaid = CircuitPersonal("transferPaymentsPaid"), PersonalIncome = CircuitPersonal("personalIncome"),
                    Source = $"circuit.json personal ({data.Circuit.Year}, before BEA's September 2026 update; MODALITIES-PLAN decision 30)"
                };
            }

            return new Personal
            {
                Compensation = L("T20100", 2, y), Proprietors = L("T20100", 9, y), Rental = L("T20100", 12, y),
                Interest = L("T20100", 14, y), Dividends = L("T20100", 15, y), Benefits = L("T20100", 17, y),
                FromBusiness = L("T20100", 24, y), Contributions = L("T20100", 25, y), Taxes = L("T20100", 26, y),
                Pce = L("T20100", 29, y), Saving = L("T20100", 34, y), InterestPaid = L("T20100", 30, y),
                TransfersPaid = L("T20100", 31, y), PersonalIncome = L("T20100", 1, y),
                Source = NipaSource($"Table 2.1, {y}")
            };
        }

        /// <summary>The tier tower: value added, jobs and the face mosaic of each market tier (industries.json).</summary>
        void BuildTiers(InstantYear r)
        {
            int y = r.Year;
            double gov = 0;
            for (int t = 0; t < Tiers; t++) r.Tiers[t] = new InstantTier { Id = TierIds[t], Name = TierNames[t] };
            foreach (Industry ind in data.Industries)
            {
                double va = ind.ValueAdded.At(y);
                if (ind.TierId == "gov")
                {
                    gov += va;
                    continue;
                }

                int t = Array.IndexOf(TierIds, ind.TierId);
                if (t < 0) continue;
                InstantTier tier = r.Tiers[t];
                tier.ValueAdded += va;
                tier.Jobs += ind.Employment.At(y) / 1000;   // thousands of FTE -> millions
                tier.Pay += va * ind.CompShare;
                tier.Taxes += va * ind.TaxShare;
                tier.Upkeep += va * ind.DepShare;
                tier.Owners += va * ind.OwnersShare;
            }

            double market = 0, jobs = 0;
            foreach (InstantTier t in r.Tiers)
            {
                market += t.ValueAdded;
                jobs += t.Jobs;
            }

            foreach (InstantTier t in r.Tiers)
            {
                t.OutputShare = market > 0 ? t.ValueAdded / market : 0;
                t.JobsShare = jobs > 0 ? t.Jobs / jobs : 0;
            }

            r.MarketValueAdded = market;
            r.PublicValueAdded = gov;
            r.Gdp = data.Gdp.At(y);
        }

        /// <summary>
        /// The class tower's floors. In circuit.json's year: instant.json's statements on circuit.json's totals
        /// (classes.flows.circuitVintage). In BEA's distribution years (2000-2024): that year's group shares on the year's NIPA
        /// Table 2.1 totals, spending shares carried from their last year (2023) to 2024, interest and transfers paid split as
        /// in instant.json's main block and saving the residual (as circuitVintage does: BEA's own saving shares leave the top
        /// 1% paying negative interest in 2022-2024). Before: one undivided block, NIPA Table 2.1's totals.
        /// </summary>
        void BuildClasses(InstantYear r, Personal p)
        {
            int y = r.FlowYear, n = groups.Length;
            bool split = classBasis == "income" && n > 0 && (r.CircuitVintage || y >= incomeFirst && y <= incomeLast && byYearIncome.Length > 0);
            r.ClassSplit = split;
            string fy = y.ToString(Ci);
            r.StatementSource = r.CircuitVintage
                ? "instant.json classes.flows.circuitVintage (BEA's " + sourceYear.ToString(Ci) + " group shares on circuit.json's " + fy +
                  " personal accounts)"
                : split
                    ? "instant.json classes.flows.byYear (BEA's " + fy + " group shares) x " + NipaSource("Table 2.1, " + fy)
                    : NipaSource("Table 2.1, " + fy);
            if (!split)
            {
                InstantGroup all = new InstantGroup { Id = "all", Name = "All households", Short = "all" };
                all.Pay = p.Compensation;
                all.Proprietors = p.Proprietors;
                all.Rental = p.Rental;
                all.Interest = p.Interest;
                all.Dividends = p.Dividends;
                all.State = p.Benefits;
                all.OtherIncome = p.FromBusiness;
                all.Contributions = p.Contributions;
                all.Taxes = p.Taxes;
                all.Spending = p.Pce;
                all.OtherOutlays = p.InterestPaid + p.TransfersPaid;
                r.Groups = new[] { all };
            }
            else
            {
                r.Groups = new InstantGroup[n];
                for (int g = 0; g < n; g++) r.Groups[g] = new InstantGroup { Id = groups[g].id, Name = groups[g].name, Short = groups[g].shortName };
                double[][] comp = new double[Components.Length][];
                double[] pce = new double[n], other = new double[n];
                if (r.CircuitVintage)
                {
                    for (int k = 0; k < Components.Length; k++) comp[k] = cvComponents[k];
                    pce = cvPce;
                    other = cvOther;
                }
                else
                {
                    for (int k = 0; k < Components.Length; k++)
                    {
                        double[] s = byYearIncome[k][y - incomeFirst];
                        double total = L("T20100", ComponentLines[k], y);
                        comp[k] = new double[n];
                        for (int g = 0; g < n && g < s.Length; g++) comp[k][g] = s[g] * total;
                    }

                    double[] ps = byYearPce[Math.Min(y, spendLast) - spendFirst];
                    double outlays = p.InterestPaid + p.TransfersPaid;
                    for (int g = 0; g < n; g++)
                    {
                        pce[g] = g < ps.Length ? ps[g] * p.Pce : 0;
                        other[g] = g < otherOutlayShares.Length ? otherOutlayShares[g] * outlays : 0;
                    }
                }

                for (int g = 0; g < n; g++)
                {
                    InstantGroup x = r.Groups[g];
                    x.Pay = At(comp[0], g);
                    x.Proprietors = At(comp[1], g);
                    x.Rental = At(comp[2], g);
                    x.Interest = At(comp[3], g);
                    x.Dividends = At(comp[4], g);
                    x.State = At(comp[5], g);
                    x.OtherIncome = At(comp[6], g);
                    x.Contributions = At(comp[7], g);
                    x.Taxes = At(comp[8], g);
                    x.Spending = At(pce, g);
                    x.OtherOutlays = At(other, g);
                }
            }

            double total2 = 0;
            foreach (InstantGroup x in r.Groups)
            {
                x.Owning = x.Proprietors + x.Rental + x.Interest + x.Dividends;
                x.GrossIncome = x.Pay + x.Owning + x.State + x.OtherIncome;
                // saving is what is left (the statement balances by construction); the undivided block checks it against NIPA's
                x.Saving = x.GrossIncome - x.Taxes - x.Contributions - x.Spending - x.OtherOutlays;
                total2 += x.GrossIncome;
            }

            r.ClassTotal = total2;
            r.PersonalIncomePlusContributions = p.PersonalIncome + p.Contributions;
        }

        static double At(double[] v, int i) => v != null && i < v.Length && !double.IsNaN(v[i]) ? v[i] : 0;

        /// <summary>
        /// Spending and the household dollar. The fork splits each category's dollar through <see cref="ValueCapture"/>'s
        /// Leontief ledger (2024 input-output ratios, ≈) and weighs the categories by the simulated households' spending of the
        /// year, which makes it the capture view's "of each household dollar" (the same ledger over the census's players, who
        /// are every adult line); without the simulation, spending.json's mix. The fear lane is the judged fear share of the
        /// same mix.
        /// </summary>
        void BuildSpending(InstantYear r, Personal p)
        {
            double pce = 0;
            foreach (InstantGroup g in r.Groups) pce += g.Spending;
            r.Pce = pce;
            r.NipaPce = p.Pce;

            int cats = Math.Min(6, capture.CategoryParts.Length);
            double[] w = new double[cats];
            PopulationYear lived = lives != null && lives.Ready ? lives.Aggregate(r.FlowYear) : null;
            if (lived != null)
            {
                for (int c = 0; c < cats; c++) w[c] = Math.Max(0, lived.Categories[c]);
                r.Dollar.Weights = $"the simulated households' spending by category in {r.FlowYear.ToString(Ci)}";
            }
            else
            {
                for (int c = 0; c < cats && c < data.Categories.Count; c++) w[c] = Math.Max(0, data.Categories[c].Total());
                r.Dollar.Weights = "spending.json's " + (data.Spending?.Year ?? 0).ToString(Ci) + " spending by category (no simulation)";
            }

            // ValueCapture's parts: wages, taxes, upkeep, owners, imports -> the bar's order: pay, owners, upkeep, imports, taxes
            int[] order = { (int)ValueCapture.Part.Wages, (int)ValueCapture.Part.Owners, (int)ValueCapture.Part.Upkeep,
                (int)ValueCapture.Part.Imports, (int)ValueCapture.Part.Taxes };
            double[] parts = new double[5];
            double wsum = 0, fear = 0, judged = 0, conf = 0;
            for (int c = 0; c < cats; c++)
            {
                wsum += w[c];
                for (int k = 0; k < 5; k++) parts[k] += w[c] * capture.CategoryParts[c][order[k]];
                fear += w[c] * capture.CategoryFear[c];
                Category cat = c < data.Categories.Count ? data.Categories[c] : null;
                double items = cat?.Total() ?? 0;
                if (items <= 0 || cat.Items == null) continue;
                foreach (SpendingItem item in cat.Items)
                {
                    if (item.Pce <= 0 || !judgedConfidence.TryGetValue(item.Id, out double ic)) continue;
                    double share = w[c] * item.Pce / items;
                    judged += share;
                    conf += share * ic;
                }
            }

            double sum = 0;
            for (int k = 0; k < 5; k++) sum += parts[k];
            r.Dollar.RawSum = wsum > 0 ? sum / wsum : 0;
            for (int k = 0; k < 5; k++) r.Dollar.Shares[k] = sum > 0 ? parts[k] / sum : 0;
            Cents(r.Dollar.Shares, r.Dollar.Cents);
            r.FearShare = wsum > 0 ? fear / wsum : 0;
            r.JudgedShare = wsum > 0 ? judged / wsum : 0;
            r.FearConfidence = judged > 0 ? conf / judged : double.NaN;
        }

        /// <summary>
        /// The class floors' face mosaics (spec 5.2), area-true over what a group had to spend, its income plus what it
        /// borrowed: the sources (pay, owning, the state, other, borrowed) and, for the spending step, the uses (toward a want,
        /// away from a fear, taxes and contributions, interest and transfers paid, saving). The fear share is the year's, the
        /// same for every group (≈: the judgments are not split by income).
        /// </summary>
        static void Mosaics(InstantYear r)
        {
            foreach (InstantGroup g in r.Groups)
            {
                double total = g.GrossIncome + g.Borrowed;
                if (!(total > 0)) continue;
                double[] sources = { g.Pay, g.Owning, g.State, g.OtherIncome, g.Borrowed };
                double[] uses = { g.Spending * (1 - r.FearShare), g.Spending * r.FearShare, g.Taxes + g.Contributions, g.OtherOutlays, g.Saved };
                for (int k = 0; k < 5; k++)
                {
                    g.SourceShares[k] = sources[k] / total;
                    g.UseShares[k] = uses[k] / total;
                }
            }
        }

        /// <summary>Whole cents summing to 100 for shares summing to 1 (the 100-cent bar's rounding).</summary>
        public static int[] WholeCents(double[] shares)
        {
            int[] cents = new int[shares.Length];
            Cents(shares, cents);
            return cents;
        }

        /// <summary>Whole cents summing to 100: each share's floor, then the largest remainders (ties in the bar's order).</summary>
        static void Cents(double[] shares, int[] cents)
        {
            int n = shares.Length, left = 100;
            double[] rest = new double[n];
            for (int k = 0; k < n; k++)
            {
                double c = 100 * shares[k];
                cents[k] = (int)Math.Floor(c);
                rest[k] = c - cents[k];
                left -= cents[k];
            }

            for (; left > 0; left--)
            {
                int best = 0;
                for (int k = 1; k < n; k++)
                {
                    if (rest[k] > rest[best]) best = k;
                }

                cents[best]++;
                rest[best] = -1;
            }
        }

        /// <summary>
        /// The state: NIPA Table 3.1's current account for the base (receipts, outlays, the borrowing between them) and its
        /// strata, which add up to the outlays; the money it moves, which add up on a money basis (purchases gross of its fees
        /// and sales, investment and its net borrowing included). Taxes and benefits are the class statements' and circuit.json's
        /// in its year, so the base and the class tower agree; the rest is Table 3.1's.
        /// </summary>
        void BuildState(InstantYear r, Personal p)
        {
            int y = r.FlowYear;
            InstantState s = r.State;
            s.Receipts = L("T30100", 1, y);
            s.Outlays = L("T30100", 20, y);
            s.Borrowing = s.Outlays - s.Receipts;
            s.TotalReceipts = L("T30100", 34, y);
            s.TotalExpenditures = L("T30100", 37, y);
            // before 1960 BEA leaves total expenditures and net lending empty, but their parts start in 1947: current
            // expenditures, gross investment, capital transfers paid and land bought (from 1960), less the wear of public
            // capital; receipts add capital transfers received. The current account's borrowing alone would leave the
            // investment the state buys unpaid, an "other" stub of up to 12% on the base and some years drawn as lending
            if (double.IsNaN(s.TotalExpenditures))
            {
                s.TotalExpenditures = s.Outlays + L("T30100", 39, y) + L("T30100", 40, y) + Z(L("T30100", 41, y)) - L("T30100", 42, y);
            }

            if (double.IsNaN(s.TotalReceipts)) s.TotalReceipts = s.Receipts + L("T30100", 36, y);
            double lending = L("T30100", 43, y);
            s.NetBorrowing = double.IsNaN(lending) ? s.TotalExpenditures - s.TotalReceipts : -lending;

            s.Pay = L("T31005", 4, y);
            s.Wear = L("T31005", 5, y);
            s.OwnWork = L("T31005", 3, y);
            s.PurchasesNet = L("T30100", 21, y) - s.OwnWork;
            s.Transfers = L("T30100", 22, y);
            s.Interest = L("T30100", 27, y);
            s.Subsidies = L("T30100", 30, y);
            s.Purchases = L("T31005", 6, y) + L("T30100", 39, y);
            s.Jobs = L("T60500D", 88, y) + L("T60500D", 93, y);
            s.EnterpriseJobs = L("T60500D", 91, y) + L("T60500D", 96, y);
            s.EnterprisePay = L("T60200D", 86, y) - s.Pay;

            // the people state: the tier floors, the enterprises beside them (with the market) and the base
            double tierJobs = 0;
            foreach (InstantTier t in r.Tiers) tierJobs += t.Jobs;
            r.JobsDrawn = tierJobs + s.EnterpriseJobs + s.Jobs;
            r.JobsNipa = L("T60500D", 1, y);

            // what the class statements receive from and pay to the state, so the base and the class tower agree
            s.Benefits = Sum(PerGroup(r, g => g.State));
            s.TaxesHouseholds = Sum(PerGroup(r, g => g.Taxes + g.Contributions));

            // interest by holder: NIPA joins persons and business; federal interest to US residents other than general
            // government stands in for persons (≈, the spec's "≈ $0.9T"), the rest of interest to US residents is business'
            s.InterestAbroad = Z(L("T30100", 29, y));
            double residents = L("T30100", 28, y);
            if (double.IsNaN(residents)) residents = s.Interest - s.InterestAbroad;
            double persons = L("T30200", 34, y);
            if (double.IsNaN(persons)) persons = residents;
            s.InterestToPersons = Math.Max(0, Math.Min(Math.Min(persons, residents), Sum(PerGroup(r, g => g.Interest))));
            s.InterestToBusiness = Math.Max(0, residents - s.InterestToPersons);

            s.TaxesIndustries = r.CircuitVintage
                ? data.Circuit.IncomeOf("productionTaxes") + data.Circuit.IncomeOf("corporateTaxes")
                : L("T30100", 4, y) + L("T30100", 5, y);

            // in circuit.json's year the taxes and benefits drawn are its vintage and the rest of the account Table 3.1's: the
            // node would miss their difference, so it is carried as its own labelled flow, never left to the 1% tolerance or
            // to an unexplained "other" (in other years the class statements are Table 2.1's, which Table 3.1 matches)
            if (r.CircuitVintage)
            {
                s.VintageTaxesHouseholds = Z(L("T30100", 3, y) + L("T30100", 8, y) - s.TaxesHouseholds);
                s.VintageTaxesIndustries = Z(L("T30100", 4, y) + L("T30100", 5, y) - s.TaxesIndustries);
                s.VintageBenefits = Z(s.Benefits - L("T30100", 24, y));
                s.Vintage = s.VintageTaxesHouseholds + s.VintageTaxesIndustries + s.VintageBenefits;
            }

            // other receipts: Table 3.1's non-tax current receipts (taxes and contributions from abroad, interest and rents,
            // transfers, enterprises' surplus), the enterprises' wear (their surplus is net of it), fees and sales of public
            // services with own-account investment (Table 3.10.5: gross output less consumption) and capital transfers
            double nonTax = s.Receipts - L("T30100", 3, y) - L("T30100", 4, y) - L("T30100", 5, y) - L("T30100", 8, y);
            double enterpriseWear = Z(L("T30100", 42, y)) - Z(s.Wear);
            double sales = L("T31005", 2, y) - L("T31005", 1, y);
            s.OtherReceipts = Z(nonTax) + Math.Max(0, enterpriseWear) + Z(sales) + Z(L("T30100", 36, y));
            double abroad = Z(L("T30100", 23, y) - L("T30100", 24, y)) + Z(L("T30100", 26, y));
            s.OtherOutlays = s.InterestToBusiness + Z(s.Subsidies) + abroad + Z(L("T30100", 40, y)) + Z(L("T30100", 41, y));
            s.PurchasesIo = StatePurchasesIo();

            r.Exports = L("T10105", 16, y);
            r.Imports = L("T10105", 19, y);
            r.Investment = L("T10105", 8, y);
        }

        /// <summary>
        /// The imports branch's share of households' spending (<see cref="InstantYear.ImportsShare"/>): the household dollar's
        /// imports share in the ledger's year (circuit.json's), and in other years that share scaled by the year's imports'
        /// share of GDP against the ledger year's (NIPA Table 1.1.5 lines 19 and 1, one vintage; ≈). A modelled split never
        /// exceeds its measured total: should households still import more than all imports, they are capped there and the
        /// year has a problem.
        /// </summary>
        void BuildImports(InstantYear r)
        {
            int y = r.FlowYear, ledger = LedgerYear;
            double here = L("T10105", 19, y) / L("T10105", 1, y), there = L("T10105", 19, ledger) / L("T10105", 1, ledger);
            r.ImportsScale = y != ledger && here > 0 && there > 0 ? here / there : 1;
            r.ImportsShare = r.Dollar.Shares[3] * r.ImportsScale;
            if (r.Pce > 0 && r.Pce * r.ImportsShare > r.Imports)
            {
                r.Problems.Add($"{y.ToString(Ci)} households' imports {Money(r.Pce * r.ImportsShare)} exceed all imports {Money(r.Imports)} " +
                               "(NIPA Table 1.1.5 line 19): capped at all imports");
                r.ImportsShare = Math.Max(0, r.Imports) / r.Pce;
                r.ImportsCapped = true;
            }
        }

        /// <summary>The household dollar's own year: the ledger is the land's snapshot of circuit.json's year (spec 5.2's "2025
        /// ledger"), else the national accounts' last year.</summary>
        int LedgerYear => data.Circuit?.Year ?? nipaLast;

        /// <summary>Where a year's net borrowing comes from: Table 3.1 line 43, or before it starts (1960) its parts.</summary>
        string NetBorrowingLines(int year) =>
            !double.IsNaN(L("T30100", 43, year))
                ? "Table 3.1 line 43 (net lending or net borrowing)"
                : "Table 3.1 lines 20 + 39 + 40 + 41 - 42 less 1 + 36 (total expenditures less total receipts; line 43 starts in " +
                  First("T30100", 43).ToString(Ci) + ")";

        /// <summary>
        /// The state's purchases in the use table (circuit.json io: the government industries' supplies from market
        /// industries, and finalDemand: government final purchases of market commodities), government enterprises included:
        /// MODALITIES-PLAN decision 22's $3.16T for 2024.
        /// </summary>
        double StatePurchasesIo()
        {
            double v = 0;
            IoTable io = data.Circuit?.Io;
            if (io != null)
            {
                foreach ((string from, string to, double value) in io.Flows())
                {
                    Industry s = data.IndustryById(from), u = data.IndustryById(to);
                    if (s != null && u != null && from != to && u.TierId == "gov" && s.TierId != "gov") v += value;
                }
            }

            if (data.Circuit?.FinalDemand != null)
            {
                foreach (KeyValuePair<string, Dictionary<string, double>> kv in data.Circuit.FinalDemand)
                {
                    Industry ind = data.IndustryById(kv.Key);
                    if (ind != null && ind.TierId != "gov" && kv.Value != null && kv.Value.TryGetValue("government", out double g)) v += g;
                }
            }

            return v;
        }

        InstantFlow Add(InstantYear r, string id, string name, string from, string to, double value, string role, string basis,
            string source, string note = null, double[] byGroup = null)
        {
            InstantFlow f = new InstantFlow
            {
                Id = id, Name = name, From = from, To = to, Value = double.IsNaN(value) ? 0 : value, Role = role, Basis = basis,
                Source = source, Note = note, ByGroup = byGroup
            };
            if (f.Value < 0)
            {
                // a flow that runs the other way this year (the state's lending in a surplus year) is drawn the way it runs
                f.From = to;
                f.To = from;
                f.Value = -f.Value;
                if (f.ByGroup != null)
                {
                    for (int g = 0; g < f.ByGroup.Length; g++) f.ByGroup[g] = -f.ByGroup[g];
                }

                f.Note = (note != null ? note + "; " : "") + "runs the other way in " + r.FlowYear.ToString(Ci);
            }

            r.Flows.Add(f);
            if (double.IsNaN(value)) r.Problems.Add($"flow '{id}' has no value in {r.FlowYear.ToString(Ci)} ({source})");
            return f;
        }

        static double[] PerGroup(InstantYear r, Func<InstantGroup, double> of)
        {
            double[] v = new double[r.Groups.Length];
            for (int g = 0; g < v.Length; g++) v[g] = of(r.Groups[g]);
            return v;
        }

        static double Sum(double[] v)
        {
            double s = 0;
            foreach (double x in v) s += x;
            return s;
        }

        /// <summary>Every flow of spec 5.4, with its per-group parts where the class statements split it.</summary>
        void BuildFlows(InstantYear r, Personal p)
        {
            int y = r.FlowYear;
            string fy = y.ToString(Ci);
            InstantState s = r.State;
            string statement = r.StatementSource;
            string groupBasis = r.ClassSplit ? "measured total; ≈ group split" : "measured";

            // spending: two lanes into the fork (rose toward a want, ice away from a fear), the total measured
            double fear = r.FearShare, m = r.ImportsShare;
            Group(r, "spending-want", "spending toward a want", "classes", "fork", PerGroup(r, g => g.Spending * (1 - fear)), "desire",
                "judged", statement + "; fear share: ValueCapture's item judgments (Jev) shrunk to category priors",
                "the total is measured; the split is judged");
            Group(r, "spending-fear", "spending away from a fear", "classes", "fork", PerGroup(r, g => g.Spending * fear), "fear",
                "judged", statement + "; fear share: ValueCapture's item judgments (Jev) shrunk to category priors",
                "the total is measured; the split is judged");

            // the imports branch at the fork: the household dollar's imports share, scaled outside the ledger's year and never
            // over all imports (BuildImports)
            string scaled = r.ImportsCapped
                ? ", capped at all imports"
                : r.ImportsScale != 1 ? ", x imports' share of GDP " + fy + " over " + LedgerYear.ToString(Ci) : "";
            string scaledBy = r.ImportsCapped
                ? " (" + NipaSource("Table 1.1.5 line 19, " + fy) + ")"
                : r.ImportsScale != 1 ? " (" + NipaSource("Table 1.1.5 lines 19 and 1") + ")" : "";
            Group(r, "spending-market", "spending's value added at home", "fork", "tiers", PerGroup(r, g => g.Spending * (1 - m)),
                "desire", "modelled", "ValueCapture ledger (2024 input-output ratios)" + scaled + scaledBy + " on " + statement,
                "≈ less the household dollar's imports share" + scaled);
            Group(r, "imports-households", "imports in households' spending", "fork", "abroad", PerGroup(r, g => g.Spending * m),
                "commerce", "modelled", "ValueCapture ledger (item import shares, 2024 input-output ratios)" + scaled + scaledBy + " on " + statement,
                "≈ the household dollar's imports share" + scaled);
            double otherImports = r.Imports - r.Pce * m;
            Add(r, "imports-other", "imports bought by firms and the state", "tiers", "abroad", Math.Max(0, otherImports), "commerce",
                "modelled", NipaSource("Table 1.1.5 line 19, " + fy) + " less imports-households",
                "≈ all imports less the households' share; the conduit carries all imports");

            // pay: the market's (every employer but general government, enterprises included: decision 32) and the state's,
            // split across the groups by their pay (BEA does not split public from private pay by income)
            double publicPay = Z(s.Pay), payAll = Sum(PerGroup(r, g => g.Pay));
            double marketShare = payAll > 0 ? Math.Max(0, payAll - publicPay) / payAll : 0;
            Group(r, "pay", "pay from industries", "tiers", "classes", PerGroup(r, g => g.Pay * marketShare), "people", groupBasis,
                statement + " compensation less " + NipaSource("Table 3.10.5 line 4, " + fy),
                "all compensation but general government's: government enterprises' pay counts with the market's" +
                (double.IsNaN(s.EnterprisePay) ? "" : " ($" + (s.EnterprisePay / 1000).ToString("0.00", Ci) + "T, NIPA Table 6.2D line 86 less " +
                                                      "Table 3.10.5 line 4; industries.json counts their value added in its government tier)"));
            Group(r, "public-pay", "pay from the state", "state", "classes", PerGroup(r, g => g.Pay * (1 - marketShare)), "state",
                "measured; ≈ group split", NipaSource("Table 3.10.5 line 4 (compensation of general government employees), " + fy),
                "≈ split by the groups' pay");

            // owners: proprietors', rental and dividend income and private interest (personal interest less the state's)
            double interestAll = Sum(PerGroup(r, g => g.Interest)), persons = s.InterestToPersons;
            double privateShare = interestAll > 0 ? (interestAll - persons) / interestAll : 1;
            Group(r, "owners", "owners' income to persons", "tiers", "classes",
                PerGroup(r, g => g.Proprietors + g.Rental + g.Dividends + g.Interest * privateShare), "owners", groupBasis,
                statement + " (proprietors' income, rental income, dividends, interest) less the state's interest to persons",
                "private interest = personal interest less the state's interest to persons (≈)");
            Group(r, "transfers-business", "transfers from business", "tiers", "classes", PerGroup(r, g => g.OtherIncome), "stone",
                groupBasis, statement + " (other current transfer receipts from business)", null);
            Group(r, "benefits", "social benefits", "state", "classes", PerGroup(r, g => g.State), "state", groupBasis,
                statement + " (government social benefits to persons)", null);
            Group(r, "interest-persons", "the state's interest to persons", "state", "classes",
                PerGroup(r, g => g.Interest * (1 - privateShare)), "state", "modelled",
                NipaSource("Table 3.2 line 34 (federal interest to residents other than general government), " + fy),
                "≈ NIPA does not split persons from business: federal interest to US residents stands in");

            // the state's other links
            Group(r, "taxes-households", "taxes from households", "classes", "state", PerGroup(r, g => g.Taxes + g.Contributions),
                "state", groupBasis, statement + " (personal current taxes and contributions for government social insurance)", null);
            Add(r, "taxes-industries", "taxes from industries", "tiers", "state", s.TaxesIndustries, "state", "measured",
                r.CircuitVintage
                    ? "circuit.json income.productionTaxes + income.corporateTaxes (" + fy + ")"
                    : NipaSource("Table 3.1 lines 4 + 5, " + fy),
                "taxes on production and imports (customs duties included) and taxes on corporate income");
            Add(r, "state-buys", "the state buys from industries", "state", "tiers", s.Purchases, "state", "measured",
                NipaSource("Table 3.10.5 line 6 + Table 3.1 line 39, " + fy),
                "general government's supplies and all government investment (the use table, enterprises included: $" +
                (s.PurchasesIo / 1000).ToString("0.00", Ci) + "T in " + (data.Circuit?.Io?.Year ?? 0).ToString(Ci) + ")");
            Add(r, "interest-abroad", "the state's interest abroad", "state", "abroad", s.InterestAbroad, "state", "measured",
                NipaSource("Table 3.1 line 29, " + fy));
            Add(r, "other-receipts", "the state's other receipts", "stub", "state", s.OtherReceipts, "stone", "measured",
                NipaSource("Table 3.1 lines 1 less 3, 4, 5 and 8, plus 36 and 42; Table 3.10.5 lines 2 less 1, " + fy),
                "interest and rents, transfers, fees and sales of public services, enterprises' surplus and wear");
            Add(r, "other-outlays", "the state's other outlays", "state", "stub", s.OtherOutlays, "stone", "measured",
                NipaSource("Table 3.1 lines 28, 30, 23 less 24, 26, 40 and 41, " + fy),
                "interest to business (≈), subsidies, transfers abroad, capital transfers, land");
            Add(r, "state-borrowing", "the state's borrowing", "stub", "state", s.NetBorrowing, "state", "measured",
                NipaSource(NetBorrowingLines(y) + ", " + fy), "hatched: owed");
            if (r.CircuitVintage)
            {
                Add(r, "state-vintage", "vintage difference (circuit.json against NIPA Table 3.1)", "stub", "state", s.Vintage, "stone", "modelled",
                    NipaSource("Table 3.1 lines 3 + 8 + 4 + 5 less 24, " + fy) + " against circuit.json's taxes and benefits (" + fy +
                    "; MODALITIES-PLAN decision 30)",
                    "≈ vintage difference: Table 3.1 less the drawn taxes from households $" + (s.VintageTaxesHouseholds / 1000).ToString("0.000", Ci) +
                    "T and from industries $" + (s.VintageTaxesIndustries / 1000).ToString("0.000", Ci) + "T, the drawn benefits less Table 3.1's $" +
                    (s.VintageBenefits / 1000).ToString("0.000", Ci) + "T; the class tower and the hills keep circuit.json's");
            }

            // industries' other links
            Add(r, "owners-abroad", "owners abroad", "tiers", "abroad", Z(L("T40100", 27, y)) + Z(L("T40100", 28, y)), "owners", "measured",
                NipaSource("Table 4.1 lines 27 + 28 (dividends and reinvested earnings paid abroad), " + fy));
            double upkeep = 0;
            foreach (InstantTier t in r.Tiers) upkeep += t.Upkeep;
            Add(r, "upkeep", "upkeep", "tiers", "stub", upkeep, "stone", "modelled",
                "industries.json value added " + r.Year.ToString(Ci) + " x depShare (estimated depreciation shares)", "≈ estimated depreciation shares");
            double retained = r.CircuitVintage
                ? data.Circuit.IncomeOf("retained")
                : EconomyData.Keyed(data.Circuit?.IncomeHistory, "retained").At(y);
            Add(r, "retained", "kept by firms", "tiers", "stub", retained, "owners", "measured",
                r.CircuitVintage ? "circuit.json income.retained (" + fy + ")" : "circuit.json incomeHistory retained (NIPA Table 1.10), " + fy,
                "undistributed corporate profits");
            Add(r, "investment", "investment", "stub", "tiers", r.Investment, "owners", "measured",
                NipaSource("Table 1.1.5 line 8 (private fixed investment), " + fy), "the import part of investment is not split here");
            Add(r, "exports", "exports", "abroad", "tiers", r.Exports, "commerce", "measured", NipaSource("Table 1.1.5 line 16, " + fy));

            // the households' loose ends
            Group(r, "saving", "saving", "classes", "stub", PerGroup(r, g => g.Saved), "owners", "modelled", statement,
                "≈ the groups that save");
            Group(r, "borrowing", "borrowing or drawing down", "stub", "classes", PerGroup(r, g => g.Borrowed), "stone", "modelled",
                statement, "≈ hatched: the groups that spend more than their income");
            Group(r, "other-payments", "interest and transfers paid", "classes", "stub", PerGroup(r, g => g.OtherOutlays), "stone",
                groupBasis, statement + " (personal interest payments and current transfer payments)", null);
        }

        /// <summary>A flow split by class group: its value is the sum of the groups' parts.</summary>
        void Group(InstantYear r, string id, string name, string from, string to, double[] byGroup, string role, string basis,
            string source, string note) =>
            Add(r, id, name, from, to, Sum(byGroup), role, basis, source, note, byGroup);

        /// <summary>
        /// The nodes and their balance: each class group and the class tower, the fork, the tier tower and the state must
        /// balance within <see cref="NodeTolerance"/>, or an "other" stub carries the residual (spec 5.4). The gate is not
        /// checked: its gap is imports and payments abroad less exports, not the current account (income from abroad,
        /// compensation and transfers are not flows here). Then the tier node's value-added content (spec 5.10).
        /// </summary>
        void BuildNodes(InstantYear r)
        {
            foreach (InstantGroup g in r.Groups)
            {
                // a group's income in = its uses out, saving and borrowing included
                double gin = g.GrossIncome + g.Borrowed, gout = g.Taxes + g.Contributions + g.Spending + g.OtherOutlays + g.Saved;
                Node(r, "class:" + g.Id, g.Name, gin, gout, true, false);
            }

            string[] ids = { "classes", "fork", "tiers", "state", "abroad" };
            string[] names = { "the class tower", "the fork", "the tier tower", "the state", "abroad" };
            for (int k = 0; k < ids.Length; k++)
            {
                double fin = 0, fout = 0;
                foreach (InstantFlow f in r.Flows)
                {
                    if (f.To == ids[k]) fin += f.Value;
                    if (f.From == ids[k]) fout += f.Value;
                }

                Node(r, ids[k], names[k], fin, fout, ids[k] != "abroad", true);
            }

            // the tier node's value added: what it pays out and what it is paid, less the imports firms and the state buy (the
            // branch at the fork already took households' imports) and less its "other" stub, against the market's value added
            InstantNode tiers = r.Node("tiers");
            double imports = r.ValueOf("imports-other"), other = tiers?.Other?.Value ?? 0;
            bool otherOut = tiers?.Other != null && tiers.Other.From == "tiers";
            r.TierIncome = (tiers?.Out ?? 0) - imports - (otherOut ? other : 0);
            r.TierDemand = (tiers?.In ?? 0) - imports - (otherOut ? 0 : other);
            r.TierIncomeGap = r.MarketValueAdded > 0 ? r.TierIncome / r.MarketValueAdded - 1 : double.NaN;
            r.TierDemandGap = r.MarketValueAdded > 0 ? r.TierDemand / r.MarketValueAdded - 1 : double.NaN;
        }

        void Node(InstantYear r, string id, string name, double fin, double fout, bool check, bool stub)
        {
            InstantNode n = new InstantNode { Id = id, Name = name, In = fin, Out = fout, Checked = check, Residual = fin - fout };
            double side = Math.Max(Math.Abs(fin), Math.Abs(fout));
            n.Gap = side > 0 ? Math.Abs(fin - fout) / side : 0;
            if (check && stub && n.Gap > NodeTolerance)
            {
                // never forced: the residual is drawn, labelled, on the side it is missing from
                bool more = fin > fout;
                n.Other = Add(r, "other-" + id, "other (" + name + ")", more ? id : "stub", more ? "stub" : id, Math.Abs(fin - fout),
                    "stone", "modelled", "InstantModel: the node's residual", "≈ what the measured flows leave unexplained");
                n.Other.Other = true;
                if (more) n.Out += n.Other.Value;
                else n.In += n.Other.Value;
            }

            r.Nodes.Add(n);
        }

        /// <summary>
        /// The money between industries (spec 5.5) from the stored use table: rows are suppliers and columns buyers, so money
        /// runs from the column's industry to the row's; an industry's purchases from itself are left out.
        /// </summary>
        void BuildBetween(InstantYear r)
        {
            InstantBetween b = r.Between;
            IoTable io = data.Circuit?.Io;
            if (io == null) return;
            b.Year = io.Year;
            foreach ((string from, string to, double value) in io.Flows())
            {
                Industry s = data.IndustryById(from), u = data.IndustryById(to);
                if (s == null || u == null) continue;
                if (from == to)
                {
                    b.OwnIndustry += value;
                    continue;
                }

                b.Matrix[Level(u), Level(s)] += value;
            }

            double total = 0, up = 0, down = 0, within = 0, mt = 0, mu = 0, md = 0, mw = 0;
            for (int buyer = 0; buyer < 5; buyer++)
            {
                for (int seller = 0; seller < 5; seller++)
                {
                    double v = b.Matrix[buyer, seller];
                    total += v;
                    if (seller > buyer) up += v;
                    else if (seller < buyer) down += v;
                    else within += v;
                    if (buyer == 0 || seller == 0) continue;
                    mt += v;
                    if (seller > buyer) mu += v;
                    else if (seller < buyer) md += v;
                    else mw += v;
                }
            }

            b.Total = total;
            b.Up = total > 0 ? up / total : 0;
            b.Down = total > 0 ? down / total : 0;
            b.Within = total > 0 ? within / total : 0;
            b.MarketTotal = mt;
            b.MarketUp = mt > 0 ? mu / mt : 0;
            b.MarketDown = mt > 0 ? md / mt : 0;
            b.MarketWithin = mt > 0 ? mw / mt : 0;

            // a boundary k sits between market tiers k and k + 1 (1 raw .. 4 tech): what crosses it, the state left out
            for (int k = 1; k <= 3; k++)
            {
                for (int buyer = 1; buyer < 5; buyer++)
                {
                    for (int seller = 1; seller < 5; seller++)
                    {
                        if (buyer > k && seller <= k) b.BoundaryDown[k - 1] += b.Matrix[buyer, seller];
                        if (buyer <= k && seller > k) b.BoundaryUp[k - 1] += b.Matrix[buyer, seller];
                    }
                }
            }

            for (int t = 1; t < 4; t++)
            {
                b.TechBuys += b.Matrix[4, t];
                b.TechSells += b.Matrix[t, 4];
            }

            b.TechToState = b.Matrix[0, 4];
            for (int t = 1; t < 5; t++) b.StateSupplies += b.Matrix[0, t];

            double tableYear = 0;
            foreach (Industry ind in data.Industries)
            {
                if (ind.TierId != "gov") tableYear += ind.ValueAdded.At(io.Year);
            }

            b.ScaleToYear = tableYear > 0 ? r.MarketValueAdded / tableYear : 1;
        }

        /// <summary>An industry's level in the money direction: the state 0, then raw 1 .. tech 4 (industries.json order).</summary>
        static int Level(Industry i) => i.TierId == "gov" ? 0 : Array.IndexOf(TierIds, i.TierId) + 1;

        /// <summary>
        /// The comparison strip beyond output and jobs: corporate profits by tier (NIPA 6.16D through instant.json's crosswalk,
        /// assumed: mapping; 1998 on) and listed market value (the stored firms over the Wilshire total, a lower bound: the
        /// snapshot of September 2026, for 2025 on).
        /// </summary>
        void BuildStrip(InstantYear r)
        {
            int y = r.FlowYear;
            double sum = 0;
            bool any = false;
            foreach ((string tier, int[] plus, int[] minus) in profitsCrosswalk)
            {
                int t = Array.IndexOf(TierIds, tier);
                if (t < 0) continue;
                double v = 0;
                foreach (int l in plus) v += L("T61600D", l, y);
                foreach (int l in minus) v -= L("T61600D", l, y);
                r.Tiers[t].Profits = v;
                if (!double.IsNaN(v))
                {
                    sum += v;
                    any = true;
                }
            }

            foreach (InstantTier t in r.Tiers) t.ProfitsShare = any && sum > 0 && !double.IsNaN(t.Profits) ? t.Profits / sum : double.NaN;

            if (r.Year < (data.Circuit?.Year ?? 2025) || data.Circuit?.Capture == null || !(data.Circuit.MarketCapTotal > 0)) return;
            double total = data.Circuit.MarketCapTotal, firms = 0;
            foreach (InstantTier t in r.Tiers) t.MarketValue = 0;
            List<Company> sorted = new List<Company>(data.Circuit.Capture);
            sorted.Sort((a, b) => a.MarketCap != b.MarketCap ? b.MarketCap.CompareTo(a.MarketCap) : string.CompareOrdinal(a.Name, b.Name));
            foreach (Company c in sorted)
            {
                int t = Array.IndexOf(TierIds, data.IndustryById(c.Industry)?.TierId ?? "");
                if (t < 0) continue;
                r.Tiers[t].MarketValue += c.MarketCap;
                r.Tiers[t].Firms.Add(c.Name + " " + (c.MarketCap / 1000).ToString("0.00", Ci));
                firms += c.MarketCap;
            }

            foreach (InstantTier t in r.Tiers) t.MarketValueShare = t.MarketValue / total;
            r.Stocks.Listed = total;
            r.Stocks.ListedNotSplit = total - firms;
        }

        /// <summary>
        /// The balance sheet (spec 5.7): the DFA by income group, the latest quarter from circuit.json's year on (the spec's
        /// "DFA 2026 Q1"), annual means before (1989 on); the threads (1 per $1T listed, the foreign share abroad, the rest to
        /// the rows by corporate equities and funds plus 45% of pensions, ≈); federal debt by holder (August 2026).
        /// </summary>
        void BuildStocks(InstantYear r)
        {
            InstantStocks st = r.Stocks;
            int y = r.Year, n = groups.Length;
            bool latest = y >= (data.Circuit?.Year ?? 2025) && dfaLatestQuarter.Length > 0 && n > 0;
            bool annual = !latest && n > 0 && y >= dfaFirst && y <= dfaLast && dfaFirst > 0;
            st.Instruments = new string[instruments.Length];
            st.InstrumentNames = new string[instruments.Length];
            st.Liability = new bool[instruments.Length];
            for (int i = 0; i < instruments.Length; i++)
            {
                st.Instruments[i] = instruments[i].id;
                st.InstrumentNames[i] = instruments[i].name;
                st.Liability[i] = instruments[i].liability;
            }

            if (latest || annual)
            {
                st.Label = latest ? dfaLatestQuarter : y.ToString(Ci) + " (annual mean)";
                double[][] ins = new double[n][];
                double[] households = new double[n], assets = new double[n], liabilities = new double[n], worth = new double[n];
                for (int g = 0; g < n; g++)
                {
                    ins[g] = new double[instruments.Length];
                    for (int i = 0; i < instruments.Length; i++) ins[g][i] = Dfa(g, instruments[i].id, y, latest);
                    households[g] = Dfa(g, "households", y, latest);
                    assets[g] = Dfa(g, "assets", y, latest);
                    liabilities[g] = Dfa(g, "liabilities", y, latest);
                    worth[g] = Dfa(g, "netWorth", y, latest);
                }

                st.Households = Sum(households);
                st.Assets = Sum(assets);
                st.Liabilities = Sum(liabilities);
                st.NetWorth = Sum(worth);
                if (r.ClassSplit && r.Groups.Length == n)
                {
                    for (int g = 0; g < n; g++)
                    {
                        InstantGroup x = r.Groups[g];
                        x.Instruments = ins[g];
                        x.Households = households[g];
                        x.Assets = assets[g];
                        x.Liabilities = liabilities[g];
                        x.NetWorth = worth[g];
                        x.NetWorthShare = st.NetWorth > 0 ? worth[g] / st.NetWorth : double.NaN;
                    }
                }
                else if (r.Groups.Length == 1)
                {
                    InstantGroup all = r.Groups[0];
                    all.Instruments = new double[instruments.Length];
                    for (int i = 0; i < instruments.Length; i++)
                    {
                        for (int g = 0; g < n; g++) all.Instruments[i] += ins[g][i];
                    }

                    all.Households = st.Households;
                    all.Assets = st.Assets;
                    all.Liabilities = st.Liabilities;
                    all.NetWorth = st.NetWorth;
                    all.NetWorthShare = 1;
                }

                // threads: only with the listed snapshot (2025 on) and the six rows
                if (latest && st.Listed > 0 && r.ClassSplit && r.Groups.Length == n)
                {
                    st.Threads = (int)Math.Round(st.Listed / ThreadValue, MidpointRounding.AwayFromZero);
                    st.ThreadsAbroad = (int)Math.Round(st.Threads * (data.Circuit?.ForeignEquityShare ?? 0), MidpointRounding.AwayFromZero);
                    int eq = Array.FindIndex(instruments, i => i.id == "corporateEquities"), pen = Array.FindIndex(instruments, i => i.id == "pensions");
                    double[] w = new double[n];
                    for (int g = 0; g < n; g++) w[g] = (eq >= 0 ? ins[g][eq] : 0) + PensionEquity * (pen >= 0 ? ins[g][pen] : 0);
                    int[] t = Apportion(w, st.Threads - st.ThreadsAbroad);
                    for (int g = 0; g < n; g++) r.Groups[g].Threads = t[g];
                }
            }

            if (y >= (data.Circuit?.Year ?? 2025))
            {
                st.PublicDebt = debtHeld;
                st.PublicDebtFed = debtFed;
                st.PublicDebtAbroad = debtAbroad;
                st.PublicDebtOther = debtOther;
                st.PublicDebtAsOf = debtAsOf;
            }
        }

        /// <summary>A DFA value of a group: the latest quarter, or the year's annual mean.</summary>
        double Dfa(int g, string key, int year, bool latest)
        {
            if (latest) return dfaLatest[g].TryGetValue(key, out double v) ? v : double.NaN;
            if (!dfaAnnual[g].TryGetValue(key, out double[] a)) return double.NaN;
            int i = year - dfaFirst;
            return i >= 0 && i < a.Length ? a[i] : double.NaN;
        }

        /// <summary>Whole numbers summing to <paramref name="total"/> in proportion to the weights (largest remainders).</summary>
        static int[] Apportion(double[] weights, int total)
        {
            int n = weights.Length;
            int[] k = new int[n];
            double sum = Sum(weights);
            if (n == 0 || sum <= 0 || total <= 0) return k;
            double[] rest = new double[n];
            int left = total;
            for (int i = 0; i < n; i++)
            {
                double q = total * weights[i] / sum;
                k[i] = (int)Math.Floor(q);
                rest[i] = q - k[i];
                left -= k[i];
            }

            for (; left > 0; left--)
            {
                int best = 0;
                for (int i = 1; i < n; i++)
                {
                    if (rest[i] > rest[best]) best = i;
                }

                k[best]++;
                rest[best] = -1;
            }

            return k;
        }

        /// <summary>
        /// Households by main income source (spec 5.8; the simulation, ≈): every adult heading a household of their own
        /// (a couple counts half per spouse), ranked into the six groups by their household's income rank, by the largest of
        /// pay (private employers), owning (capital and business income) and the state (transfers and pay from government);
        /// none when there is no income. A couple's records carry the household's money split evenly, so a couple of a public
        /// and a private employee counts half to each.
        /// </summary>
        void BuildRoles(InstantYear r)
        {
            if (lives == null || !lives.Ready || lives.Sim == null) return;
            int y = r.FlowYear, n = groups.Length > 0 ? groups.Length : 6;
            double[,] count = new double[n, 4];
            int[] records = new int[n];
            double[] weight = new double[n];
            int people = lives.Sim.People.Count;
            for (int i = 0; i < people; i++)
            {
                if (!lives.TryGet(i, y, out PersonYear p) || !p.Adult || !p.OwnHousehold) continue;
                double rank = p.IncomeRank;
                int g = rank < 0.2 ? 0 : rank < 0.4 ? 1 : rank < 0.6 ? 2 : rank < 0.8 ? 3 : rank < 0.99 ? 4 : 5;
                g = Math.Min(g, n - 1);
                bool publicJob = p.Industry >= 0 && p.Industry < data.Industries.Count && data.Industries[p.Industry].TierId == "gov";
                double pay = Math.Max(0, publicJob ? 0 : p.Wages), owning = Math.Max(0, p.CapitalIncome + p.Business);
                double state = Math.Max(0, p.Transfers + (publicJob ? p.Wages : 0));
                int role = pay + owning + state <= 0 ? 3 : pay >= owning && pay >= state ? 0 : owning >= state ? 1 : 2;
                double w = p.Married ? 0.5 : 1;
                count[g, role] += w;
                weight[g] += w;
                records[g]++;
            }

            double perLine = lives.Sim.PeoplePerLine / 1e6;
            double[] roleTotals = new double[4];
            for (int g = 0; g < n; g++)
            {
                for (int k = 0; k < 4; k++) roleTotals[k] += count[g, k];
            }

            for (int gi = 0; gi < r.Groups.Length; gi++)
            {
                InstantGroup x = r.Groups[gi];
                double[] c = new double[4];
                double wsum = 0;
                int nsum = 0;
                if (r.ClassSplit)
                {
                    for (int k = 0; k < 4; k++) c[k] = count[gi, k];
                    wsum = weight[gi];
                    nsum = records[gi];
                }
                else
                {
                    for (int k = 0; k < 4; k++) c[k] = roleTotals[k];
                    wsum = Sum(weight);
                    foreach (int k in records) nsum += k;
                }

                for (int k = 0; k < 4; k++) x.RoleShares[k] = wsum > 0 ? c[k] / wsum : 0;
                x.RoleN = nsum;
                x.SimulatedHouseholds = wsum * perLine;
                double households = !double.IsNaN(x.Households) ? x.Households : x.SimulatedHouseholds;
                int[] dots = Apportion(x.RoleShares, (int)Math.Round(households, MidpointRounding.AwayFromZero));
                for (int k = 0; k < 4; k++) x.Dots[k] = dots[k];
            }
        }

        /// <summary>The 2025 snapshots (spec 5.2, 5.6): the frontier, the hyperscalers' capital spending and the attention loop.</summary>
        void BuildSnapshots(InstantYear r)
        {
            if (r.Year < (data.Circuit?.Year ?? 2025)) return;
            r.FrontierCapex = frontierCapex;
            r.FrontierResearch = frontierResearch;
            r.FrontierFirms = frontierFirms;
            r.HyperscalerCapex = capexReconciled;
            r.HyperscalerHeadline = capexHeadline;
            r.Ads = ads;
            r.Creators = creators;
            r.Minutes = minutes;
            if (r.Year != (data.Circuit?.Year ?? 2025)) return;
            r.CloudPeople = cloudPeople;
            r.CapitalHolders.AddRange(capitalHolders);
            r.MostFollowed.AddRange(mostFollowed);
        }

        void Cover(InstantYear r, string id, bool covered, string note) =>
            r.Coverage.Add(new InstantCoverage { Id = id, Covered = covered, Note = note });

        /// <summary>What the year's data cover (spec 5.9), each with the note the view prints.</summary>
        void BuildCoverage(InstantYear r)
        {
            int y = r.Year, cy = data.Circuit?.Year ?? 2025;
            string ys = y.ToString(Ci);
            Cover(r, "valueAdded", true, r.Estimate ? "value added " + ys + " is an estimate (first half annualized)" : "value added " + ys);
            Cover(r, "flows", y == r.FlowYear,
                y == r.FlowYear
                    ? (r.CircuitVintage ? "national accounts " + ys + ", circuit.json's vintage (before BEA's Sept 2026 update)" : "NIPA " + ys + " (" + nipaVintage + ")")
                    : "the national accounts end in " + r.FlowYear.ToString(Ci) + ": " + ys + " shows " + r.FlowYear.ToString(Ci));
            Cover(r, "classes", r.ClassSplit,
                r.ClassSplit
                    ? (r.CircuitVintage ? "≈ BEA's " + sourceYear.ToString(Ci) + " group shares scaled to " + r.FlowYear.ToString(Ci) : "≈ BEA's " + r.FlowYear.ToString(Ci) + " group shares")
                    : "households · not split by income before " + incomeFirst.ToString(Ci));
            Cover(r, "spending", r.ClassSplit && (r.CircuitVintage || r.FlowYear <= spendLast),
                r.ClassSplit && !r.CircuitVintage && r.FlowYear > spendLast
                    ? "≈ spending by group carried from " + spendLast.ToString(Ci)
                    : r.ClassSplit ? "spending by group: BEA and BLS joint distribution" : "spending not split by income");
            string ly = LedgerYear.ToString(Ci), fys = r.FlowYear.ToString(Ci);
            Cover(r, "imports", r.FlowYear == LedgerYear && !r.ImportsCapped,
                r.ImportsCapped
                    ? "≈ imports in households' spending capped at all imports (" + fys + "): even scaled, the ledger's share passed them"
                    : r.FlowYear == LedgerYear
                        ? "imports in households' spending: the household dollar's share (≈ 2024 input-output ratios)"
                        : r.ImportsScale != 1
                            ? "≈ imports in households' spending: the household dollar's " + Pct(r.Dollar.Shares[3]).ToString("0.0", Ci) +
                              "% (2024 input-output ratios) scaled by imports' share of GDP (" + fys + " against " + ly + ") to " +
                              Pct(r.ImportsShare).ToString("0.0", Ci) + "%"
                            : "≈ imports in households' spending: the household dollar's share, not scaled (no imports or GDP for " + fys + ")");
            Cover(r, "balance", r.Stocks.Label.Length > 0,
                r.Stocks.Label.Length > 0 ? "DFA " + r.Stocks.Label : "no data: the DFA starts in " + dfaFirst.ToString(Ci));
            Cover(r, "publicJobs", !double.IsNaN(r.State.Jobs),
                !double.IsNaN(r.State.Jobs)
                    ? "general government full-time equivalents, NIPA 6.5D (government enterprises' with the market's)"
                    : "no general government jobs before " + First("T60500D", 88).ToString(Ci) + " (NIPA 6.5D)");
            Cover(r, "profits", r.Tiers[0] != null && !double.IsNaN(r.Tiers[0].ProfitsShare),
                "corporate profits by industry (NIPA 6.16D, from " + First("T61600D", 9).ToString(Ci) + "; assumed: mapping)");
            Cover(r, "io", true, "input-output ratios: the " + r.Between.Year.ToString(Ci) + " table, ≈ assumed for every year");
            Cover(r, "marketValue", !double.IsNaN(r.Stocks.Listed),
                "listed value: the " + (data.Circuit?.Capture?.Count ?? 0).ToString(Ci) + " stored firms over the total of " + listedDate +
                ", for " + cy.ToString(Ci) + " on");
            Cover(r, "publicDebt", !double.IsNaN(r.Stocks.PublicDebt), "federal debt by holder, " + debtAsOf);
            Cover(r, "frontier", y >= cy && !double.IsNaN(r.FrontierCapex), "the frontier: latest fiscal years of the stored tech firms");
            Cover(r, "cloud", y == cy && r.CloudPeople > 0,
                y == cy
                    ? "followers summed across Instagram, TikTok, YouTube and X (" + cy.ToString(Ci) + " snapshot)"
                    : "attention: one " + cy.ToString(Ci) + " snapshot");
            Cover(r, "roles", r.Groups.Length > 0 && r.Groups[0].RoleN > 0, "households by main income source: the simulation (≈)");
        }

        // =================================================================================================== checks

        /// <summary>The identities of spec 5.10; each failure is one line in <see cref="InstantYear.Problems"/>.</summary>
        void Check(InstantYear r, Personal p)
        {
            string ys = r.Year.ToString(Ci);
            foreach (string lp in loadProblems) r.Problems.Add(lp);

            // each class node balances, and the class total is personal income plus contributions
            foreach (InstantGroup g in r.Groups)
            {
                double uses = g.Taxes + g.Contributions + g.Spending + g.OtherOutlays + g.Saving;
                if (Math.Abs(uses - g.GrossIncome) > NodeTolerance * Math.Max(1, Math.Abs(g.GrossIncome)))
                {
                    r.Problems.Add($"{ys} class '{g.Id}': uses {Money(uses)} vs gross income {Money(g.GrossIncome)}");
                }
            }

            if (!(Math.Abs(r.ClassTotal - r.PersonalIncomePlusContributions) <= ClassTotalTolerance * r.PersonalIncomePlusContributions))
            {
                r.Problems.Add($"{ys} class total {Money(r.ClassTotal)} vs personal income + contributions " +
                               $"{Money(r.PersonalIncomePlusContributions)} ({p.Source}): beyond {ClassTotalTolerance:P1}");
            }

            if (r.CircuitVintage)
            {
                // the stored statements' own saving (instant.json's residual) is what the class statements leave here too
                for (int g = 0; g < r.Groups.Length && g < cvSaving.Length; g++)
                {
                    if (Math.Abs(r.Groups[g].Saving - cvSaving[g]) > 1)
                    {
                        r.Problems.Add($"{ys} class '{r.Groups[g].Id}': saving {Money(r.Groups[g].Saving)} vs instant.json's {Money(cvSaving[g])}");
                    }
                }
            }
            else if (!r.ClassSplit && Math.Abs(r.Groups[0].Saving - p.Saving) > NodeTolerance * Math.Max(1, Math.Abs(r.Groups[0].GrossIncome)))
            {
                r.Problems.Add($"{ys} households' saving {Money(r.Groups[0].Saving)} vs NIPA Table 2.1 line 34 {Money(p.Saving)}");
            }

            // PCE is NIPA's PCE in the same vintage
            if (!(Math.Abs(r.Pce - r.NipaPce) <= PceTolerance * r.NipaPce))
            {
                r.Problems.Add($"{ys} PCE {Money(r.Pce)} (the class statements) vs NIPA PCE {Money(r.NipaPce)} ({p.Source})");
            }

            // the floors: each has value added and its face mosaic fits it (owners' share is the rest of the floor, clamped at 0,
            // so this fails only where an industry's pay, tax and upkeep shares pass 1), and every industry adds up to GDP. The
            // tier node's own value added against its flows is reported, not checked (InstantYear.TierIncomeGap): spec 5.4's
            // gross flows are measured and stay on the ribbons (MODALITIES-PLAN decision 35; 4.6% in 2025)
            foreach (InstantTier t in r.Tiers)
            {
                double mosaic = t.Pay + t.Taxes + t.Upkeep + t.Owners;
                if (!(t.ValueAdded > 0) || Math.Abs(mosaic - t.ValueAdded) > MosaicTolerance * t.ValueAdded)
                {
                    r.Problems.Add($"{ys} tier '{t.Id}': face mosaic {Money(mosaic)} overruns value added {Money(t.ValueAdded)} " +
                                   "(an industry's compShare + taxShare + depShare above 1, industries.json)");
                }
            }

            double all = r.MarketValueAdded + r.PublicValueAdded;
            if (!(Math.Abs(all - r.Gdp) <= ClassTotalTolerance * r.Gdp))
            {
                r.Problems.Add($"{ys} value added of every tier {Money(all)} vs GDP {Money(r.Gdp)} (industries.json)");
            }

            // every node balances, or a labelled stub carries what does not
            foreach (InstantNode n in r.Nodes)
            {
                if (!n.Checked) continue;
                double side = Math.Max(Math.Abs(n.In), Math.Abs(n.Out));
                if (side > 0 && Math.Abs(n.In - n.Out) > NodeTolerance * side)
                {
                    r.Problems.Add($"{ys} node '{n.Id}' in {Money(n.In)} vs out {Money(n.Out)}: unbalanced beyond {NodeTolerance:P0}");
                }

                if (n.Other != null && string.IsNullOrEmpty(n.Other.Name)) r.Problems.Add($"{ys} node '{n.Id}': its other stub has no label");
            }

            // the state's strata add up to its outlays
            InstantState s = r.State;
            double strata = s.OwnWork + s.PurchasesNet + s.Transfers + s.Interest + s.Subsidies;
            if (!(Math.Abs(strata - s.Outlays) <= 0.001 * s.Outlays))
            {
                r.Problems.Add($"{ys} the state's strata {Money(strata)} vs its outlays {Money(s.Outlays)} (NIPA Table 3.1 line 20)");
            }

            // the people state drops no public job: the base's general government and the enterprises drawn with the market
            // add up to 6.5D's government, so its jobs are the tier floors' plus every government job
            if (!double.IsNaN(s.Jobs))
            {
                double gov = L("T60500D", 86, r.FlowYear), tiers = 0;
                foreach (InstantTier t in r.Tiers) tiers += t.Jobs;
                if (!(Math.Abs(s.Jobs + s.EnterpriseJobs - gov) <= 0.01) || !(Math.Abs(r.JobsDrawn - tiers - gov) <= 0.01))
                {
                    r.Problems.Add($"{ys} the people state's jobs {r.JobsDrawn.ToString("0.000", Ci)}M: tier floors {tiers.ToString("0.000", Ci)}M + " +
                                   $"general government {s.Jobs.ToString("0.000", Ci)}M + enterprises {s.EnterpriseJobs.ToString("0.000", Ci)}M, " +
                                   $"vs government {gov.ToString("0.000", Ci)}M (NIPA 6.5D line 86)");
                }
            }

            // the fork sums to 100
            int cents = 0;
            foreach (int c in r.Dollar.Cents) cents += c;
            if (cents != 100 || Math.Abs(Sum(r.Dollar.Shares) - 1) > 1e-9 || Math.Abs(r.Dollar.RawSum - 1) > 0.01)
            {
                r.Problems.Add($"{ys} the household dollar: {cents} cents, shares sum {Sum(r.Dollar.Shares):0.000000}, ledger parts sum {r.Dollar.RawSum:0.0000}");
            }

            // the direction between industries against what the captions print
            if (r.Between.Total > 0)
            {
                foreach ((string what, double value, int decimals, Func<InstantBetween, double> of) in CaptionFacts)
                {
                    double v = Math.Round(of(r.Between), decimals, MidpointRounding.AwayFromZero);
                    if (Math.Abs(v - value) > 0.5 * Math.Pow(10, -decimals))
                    {
                        r.Problems.Add($"money between industries ({r.Between.Year.ToString(Ci)}): {what} is {v.ToString("F" + decimals, Ci)} " +
                                       $"in the use table, the captions say {value.ToString("F" + decimals, Ci)}");
                    }
                }
            }
            else
            {
                r.Problems.Add("no use table (circuit.json io): the money between industries is missing");
            }

            // profits by tier: the crosswalk covers every domestic industry (NIPA 6.16D line 9)
            double profits = 0, domestic = L("T61600D", 9, r.FlowYear);
            foreach (InstantTier t in r.Tiers) profits += Z(t.Profits);
            if (!double.IsNaN(domestic) && !double.IsNaN(r.Tiers[0].Profits) && Math.Abs(profits - domestic) > ClassTotalTolerance * Math.Abs(domestic))
            {
                r.Problems.Add($"{ys} profits by tier {Money(profits)} vs domestic industries {Money(domestic)} (NIPA 6.16D line 9): the crosswalk misses a line");
            }

            // listed value: the stored firms' tech share is the one instant.json's fallback states
            if (!double.IsNaN(r.Stocks.Listed) && !double.IsNaN(listedTechShareStored) &&
                Math.Abs(r.Tiers[3].MarketValueShare - listedTechShareStored) > 0.0005)
            {
                r.Problems.Add($"{ys} tech's listed share {r.Tiers[3].MarketValueShare:P2} vs instant.json listedValue.fallback {listedTechShareStored:P2}");
            }

            // household debt is every DFA liability, $20.0T at 2026:Q1 (decision 29)
            if (r.Stocks.Label == dfaLatestQuarter && r.Stocks.Label.Length > 0 &&
                !(Math.Abs(r.Stocks.Liabilities - DebtTarget) <= DebtTolerance * DebtTarget))
            {
                r.Problems.Add($"{ys} household debt {Money(r.Stocks.Liabilities)} vs {Money(DebtTarget)} within {DebtTolerance:P0} (decision 29)");
            }

            // every flow has its provenance
            foreach (InstantFlow f in r.Flows)
            {
                if (string.IsNullOrEmpty(f.Source) || string.IsNullOrEmpty(f.Basis) || string.IsNullOrEmpty(f.Role))
                {
                    r.Problems.Add($"{ys} flow '{f.Id}' lacks its source, basis or colour role");
                }

                if (f.ByGroup != null && Math.Abs(Sum(f.ByGroup) - f.Value) > 0.005 * Math.Max(1, Math.Abs(f.Value)))
                {
                    r.Problems.Add($"{ys} flow '{f.Id}': its groups add up to {Money(Sum(f.ByGroup))}, not {Money(f.Value)}");
                }
            }
        }

        static string Money(double billions) =>
            double.IsNaN(billions) ? "n/a" : "$" + (billions / 1000).ToString("0.000", Ci) + "T";

        // =================================================================================================== facts

        void Fact(InstantYear r, string key, double value, string unit, string source, string basis)
        {
            if (double.IsNaN(value)) return;
            r.Facts.Add(new InstantFact { Key = "instant." + key, Value = value, Unit = unit, Source = source, Basis = basis });
        }

        void Text(InstantYear r, string key, string text, string source, string basis) =>
            r.Facts.Add(new InstantFact { Key = "instant." + key, Text = text, Unit = "", Source = source, Basis = basis });

        static double T(double billions) => Math.Round(billions / 1000, 2);

        static double Pct(double share, int decimals = 1) => Math.Round(100 * share, decimals, MidpointRounding.AwayFromZero);

        /// <summary>
        /// The numbers the tour's cards and the docs quote (MODALITIES.md section 11, stops 4-13), rounded as printed, each with
        /// its source and basis. Keys carry no year: the loader publishes one year.
        /// </summary>
        void BuildFacts(InstantYear r)
        {
            string ys = r.Year.ToString(Ci), fy = r.FlowYear.ToString(Ci), ind = "industries.json value added " + ys, model = "InstantModel";
            string jobs = "industries.json employment (full-time equivalents, " + JobsYear(r.Year) + ")";
            Fact(r, "year", r.Year, "", model + " (" + ys + ")", "the year shown");
            Fact(r, "classes.groups", r.Groups.Length, "", "instant.json classes.groups (the DFA's income groups), " + fy,
                r.ClassSplit ? "measured (DFA income groups)" : "one undivided block");
            Fact(r, "classes.grossIncome", T(r.ClassTotal), "$T", r.StatementSource, "measured total; ≈ group splits");
            Fact(r, "classes.dpiYear", sourceYear, "", "instant.json classes.flows.sourceYear (BEA distribution of personal income, " +
                                                     sourceYear.ToString(Ci) + ")", "measured");
            string[] keys = { "raw", "make", "services", "tech" };
            for (int t = 0; t < Tiers; t++)
            {
                Fact(r, "tiers." + keys[t] + ".va", T(r.Tiers[t].ValueAdded), "$T", ind, "measured");
                Fact(r, "tiers." + keys[t] + ".jobs", Math.Round(r.Tiers[t].Jobs, 1), "million", jobs, "measured");
            }

            Fact(r, "tiers.privateVA", T(r.MarketValueAdded), "$T", ind, "measured");
            Fact(r, "tiers.tech.vaShare", Pct(r.Tiers[3].OutputShare), "%", ind, "measured");
            Fact(r, "tiers.tech.jobsShare", Pct(r.Tiers[3].JobsShare), "%", jobs, "measured");
            Fact(r, "tiers.tech.profitsShare", Pct(r.Tiers[3].ProfitsShare), "%", NipaSource("Table 6.16D, " + fy) + " via instant.json profitsByTier crosswalk",
                "measured; assumed: mapping");
            Fact(r, "tech.mvShare", Pct(r.Tiers[3].MarketValueShare), "%", "circuit.json capture over marketCapTotal (" + listedDate + ")",
                "measured lower bound: the stored firms only (at least)");
            Fact(r, "mv.notSplit", T(r.Stocks.ListedNotSplit), "$T", "circuit.json marketCapTotal less the stored firms (" + listedDate + ")", "measured");
            Fact(r, "mv.total", T(r.Stocks.Listed), "$T", "circuit.json marketCapTotal (Wilshire 5000, " + listedDate + ")", "measured");

            Fact(r, "pce", T(r.Pce), "$T", r.StatementSource, "measured");
            Fact(r, "spending.fear", Pct(r.FearShare, 0), "%", "ValueCapture: Jev item judgments shrunk to category priors, weighted by " + r.Dollar.Weights,
                "judged");
            Fact(r, "spending.fearConf", Math.Round(r.FearConfidence, 2), "", "purchase-judgments.json fear_soldConfidence and manufacturedConfidence, " +
                                                                             "weighted by " + r.Dollar.Weights, "judged");
            for (int k = 0; k < 5; k++)
            {
                Fact(r, "fork." + (k == 0 ? "wages" : InstantDollar.Parts[k]), r.Dollar.Cents[k], "cents",
                    "ValueCapture ledger (Leontief split of household purchases) weighted by " + r.Dollar.Weights,
                    "modelled: ≈ input-output ratios of 2024; before income taxes");
            }

            InstantState s = r.State;
            Fact(r, "state.receipts", T(s.Receipts), "$T", NipaSource("Table 3.1 line 1, " + fy), "measured, all levels of government");
            Fact(r, "state.outlays", T(s.Outlays), "$T", NipaSource("Table 3.1 line 20, " + fy), "measured, all levels of government");
            Fact(r, "state.deficit", T(s.Borrowing), "$T", NipaSource("Table 3.1 line 20 less line 1, " + fy), "measured");
            Fact(r, "state.netBorrowing", T(s.NetBorrowing), "$T", NipaSource(NetBorrowingLines(r.FlowYear) + ", " + fy), "measured, investment included");
            Fact(r, "state.benefits", T(s.Benefits), "$T", r.StatementSource, "measured");
            Fact(r, "state.publicPay", T(s.Pay), "$T", NipaSource("Table 3.10.5 line 4, " + fy), "measured, general government (decision 32)");
            Fact(r, "state.publicJobs", Math.Round(s.Jobs, 1), "million", NipaSource("Table 6.5D lines 88 + 93, " + fy),
                "measured full-time equivalents, general government");
            Fact(r, "state.enterprisePay", T(s.EnterprisePay), "$T", NipaSource("Table 6.2D line 86 less Table 3.10.5 line 4, " + fy),
                "measured; counted with the market's pay (decision 32)");
            Fact(r, "state.enterpriseJobs", Math.Round(s.EnterpriseJobs, 2), "million", NipaSource("Table 6.5D lines 91 + 96, " + fy),
                "measured full-time equivalents, government enterprises; drawn with the market's jobs (decision 32)");
            Fact(r, "state.purchases", T(s.Purchases), "$T", NipaSource("Table 3.10.5 line 6 + Table 3.1 line 39, " + fy), "measured");
            Fact(r, "state.purchasesNet", T(s.PurchasesNet), "$T", NipaSource("Table 3.1 line 21 less Table 3.10.5 line 3, " + fy),
                "measured, net of the state's sales");
            Fact(r, "state.interest", T(s.Interest), "$T", NipaSource("Table 3.1 line 27, " + fy), "measured");

            InstantBetween b = r.Between;
            string io = "circuit.json io (BEA " + b.Year.ToString(Ci) + " use table), own-industry purchases left out";
            Fact(r, "io.year", b.Year, "", io, "measured");
            Fact(r, "io.total", T(b.Total), "$T", io, "measured");
            Fact(r, "io.up", Pct(b.Up), "%", io, "measured; the state as tier 0");
            Fact(r, "io.down", Pct(b.Down), "%", io, "measured; the state as tier 0");
            Fact(r, "io.within", Pct(b.Within), "%", io, "measured; the state as tier 0");
            Fact(r, "io.marketUp", Pct(b.MarketUp), "%", io, "measured; market tiers only");
            Fact(r, "io.marketDown", Pct(b.MarketDown), "%", io, "measured; market tiers only");
            Fact(r, "io.marketWithin", Pct(b.MarketWithin), "%", io, "measured; market tiers only");
            Fact(r, "io.techBuys", T(b.TechBuys), "$T", io, "measured");
            Fact(r, "io.techSells", T(b.TechSells), "$T", io, "measured");
            Fact(r, "io.techToState", T(b.TechToState), "$T", io, "measured");
            string[] bounds = { "rawMake", "makeServices", "servicesTech" };
            for (int k = 0; k < 3; k++)
            {
                Fact(r, "io.boundary." + bounds[k] + ".down", T(b.BoundaryDown[k]), "$T", io, "measured; the state left out");
                Fact(r, "io.boundary." + bounds[k] + ".up", T(b.BoundaryUp[k]), "$T", io, "measured; the state left out");
            }

            if (r.Stocks.Label.Length > 0)
            {
                string dfa = dfaSource + " (instant.json classes.dfa, " + r.Stocks.Label + ")";
                Text(r, "dfa.quarter", r.Stocks.Label, dfa, "measured");
                Fact(r, "debt.households", T(r.Stocks.Liabilities), "$T", dfa, "measured: every DFA liability (decision 29)");
                Fact(r, "nw.total", T(r.Stocks.NetWorth), "$T", dfa, "measured");
                if (r.ClassSplit)
                {
                    Fact(r, "nw.top1", Pct(r.Groups[r.Groups.Length - 1].NetWorthShare), "%", dfa, "measured");
                    Fact(r, "nw.bottom20", Pct(r.Groups[0].NetWorthShare), "%", dfa, "measured");
                }
            }

            if (r.Stocks.Threads > 0)
            {
                Fact(r, "threads.total", r.Stocks.Threads, "", "circuit.json marketCapTotal (" + listedDate + "), 1 thread per $1T", "measured");
                Fact(r, "threads.abroad", r.Stocks.ThreadsAbroad, "", "circuit.json foreignEquityShare (Fed Z.1 L.224, " +
                                                                     (data.Circuit?.Year ?? 0).ToString(Ci) + ")",
                    "measured; ≈ assumed: every household holds the market's mix");
            }

            string firms = "corporate-programs.json firms in the tech tier (fiscal years ending " + frontierYears + ")";
            Fact(r, "frontier", Math.Round(r.FrontierCapex + r.FrontierResearch, 1), "$B", firms + ": capex + rd", "measured (filings)");
            Fact(r, "frontier.capex", Math.Round(r.FrontierCapex, 1), "$B", firms, "measured (filings)");
            Fact(r, "frontier.rd", Math.Round(r.FrontierResearch, 1), "$B", firms, "measured (filings)");
            Fact(r, "hyperscalers.capex", Math.Round(r.HyperscalerCapex, 1), "$B", "instant.json hyperscalerCapex (corporate-programs.json filings, calendar " +
                                                                               capexYear + ")",
                "measured: cash purchases of property and equipment, finance leases out (decision 31)");
            Fact(r, "hyperscalers.headline", Math.Round(r.HyperscalerHeadline, 1), "$B", "instant.json hyperscalerCapex.alternatives (calendar " + capexYear + ")",
                "each firm's own headline measure, not one definition");
            Fact(r, "ads", r.Ads, "$B", "corporate-programs.json economy.digitalAdRevenue (IAB/PwC 2025)", "measured");
            Fact(r, "creators", r.Creators, "$B", "corporate-programs.json economy.influencerMarketing (IAB 2025 creator ad spend)", "measured (projection)");
            Fact(r, "minutes", r.Minutes, "minutes a day", "corporate-programs.json economy.socialMediaTime (eMarketer 2025)", "measured (forecast); not money");
            if (r.CloudPeople > 0)
            {
                string snapshot = (data.Circuit?.Year ?? r.Year).ToString(Ci) + " snapshot";
                Fact(r, "cloud.people", r.CloudPeople, "", "people.json: US people with 10M+ followers (" + snapshot + ")", "measured, one " + snapshot);
                Fact(r, "cloud.capitalHolders", r.CapitalHolders.Count, "", "people.json firms (matched by Jev; " + snapshot + ")", "judged");
            }

            Fact(r, "exports", T(r.Exports), "$T", NipaSource("Table 1.1.5 line 16, " + fy), "measured");
            Fact(r, "imports", T(r.Imports), "$T", NipaSource("Table 1.1.5 line 19, " + fy), "measured");
            Fact(r, "gap", T(r.Imports - r.Exports), "$T", NipaSource("Table 1.1.5 lines 19 less 16, " + fy), "measured");
            foreach (InstantFlow f in r.Flows)
            {
                if (!f.Other) Fact(r, "flows." + f.Id, T(f.Value), "$T", f.Source, f.Basis);
            }

            // every fact a card or a label can quote names its source, how it is known and a year (spec 5.10). The NIPA flat
            // files' release date is no year of the data, so it does not count: a fact from the tables names its own
            foreach (InstantFact f in r.Facts)
            {
                if (string.IsNullOrWhiteSpace(f.Source) || string.IsNullOrWhiteSpace(f.Basis))
                {
                    r.Problems.Add($"fact '{f.Key}' has no source or basis");
                }
                else if (LastYearIn(nipaVintage.Length > 0 ? f.Source.Replace(nipaVintage, "") : f.Source) == 0 && LastYearIn(f.Basis) == 0)
                {
                    r.Problems.Add($"fact '{f.Key}' names no year in its source or basis ({f.Source})");
                }
            }
        }

        /// <summary>The year a tier's jobs stand for: industries.json's employment points, held at the first and the last.</summary>
        string JobsYear(int year)
        {
            double first = double.MaxValue, last = 0;
            foreach (Industry ind in data.Industries)
            {
                if (ind.TierId == "gov" || ind.Employment.IsEmpty) continue;
                first = Math.Min(first, ind.Employment.FirstYear);
                last = Math.Max(last, ind.Employment.LastYear);
            }

            string ys = year.ToString(Ci);
            if (last <= 0) return ys;
            if (year > last) return ys + ", held at its " + last.ToString("0", Ci) + " point";
            return year < first ? ys + ", held at its " + first.ToString("0", Ci) + " point" : ys;
        }

        // =================================================================================================== checksum

        /// <summary>FNV-1a over every number the year holds, in a fixed order and an exact form ("R").</summary>
        static string Checksum(InstantYear r)
        {
            StringBuilder b = new StringBuilder(8192);
            void N(double v) => b.Append(v.ToString("R", Ci)).Append(';');
            foreach (InstantGroup g in r.Groups)
            {
                b.Append(g.Id).Append(':');
                N(g.GrossIncome); N(g.Pay); N(g.Owning); N(g.State); N(g.OtherIncome); N(g.Taxes); N(g.Contributions); N(g.Spending);
                N(g.OtherOutlays); N(g.Saving); N(g.Households); N(g.NetWorth); N(g.Threads); N(g.RoleN);
                foreach (double v in g.Instruments) N(v);
                foreach (double v in g.RoleShares) N(v);
                foreach (double v in g.SourceShares) N(v);
                foreach (double v in g.UseShares) N(v);
                foreach (int v in g.Dots) N(v);
            }

            foreach (InstantTier t in r.Tiers)
            {
                N(t.ValueAdded); N(t.Jobs); N(t.Pay); N(t.Taxes); N(t.Upkeep); N(t.Owners); N(t.Profits); N(t.MarketValue);
            }

            InstantState s = r.State;
            foreach (double v in new[] { s.Receipts, s.Outlays, s.Borrowing, s.TotalReceipts, s.TotalExpenditures, s.NetBorrowing, s.OwnWork,
                         s.PurchasesNet, s.Transfers, s.Interest, s.Subsidies, s.Purchases, s.PurchasesIo, s.Jobs, s.EnterpriseJobs,
                         s.EnterprisePay, s.OtherReceipts, s.OtherOutlays, s.Vintage }) N(v);
            N(r.ImportsShare); N(r.ImportsScale); N(r.TierIncome); N(r.TierDemand); N(r.JobsDrawn); N(r.JobsNipa);
            foreach (InstantFlow f in r.Flows)
            {
                b.Append(f.Id).Append('=');
                N(f.Value);
                if (f.ByGroup != null)
                {
                    foreach (double v in f.ByGroup) N(v);
                }
            }

            foreach (InstantNode n in r.Nodes)
            {
                N(n.In); N(n.Out);
            }

            foreach (double v in r.Dollar.Shares) N(v);
            N(r.FearShare); N(r.FearConfidence);
            foreach (double v in r.Between.Matrix) N(v);
            N(r.Stocks.Liabilities); N(r.Stocks.Threads); N(r.Stocks.ThreadsAbroad);
            ulong h = 14695981039346656037UL;
            for (int i = 0; i < b.Length; i++)
            {
                h ^= b[i];
                h *= 1099511628211UL;
            }

            return h.ToString("x16", Ci);
        }

        // =================================================================================================== log lines

        static string D2(double billions) => (billions / 1000).ToString("0.00", Ci);

        static string P1(double share) => (100 * share).ToString("0.0", Ci);

        /// <summary>
        /// The year in one line (the load's "[Why] Instant 2025: ..."), every figure WP10's acceptance names: the class
        /// tower, the tiers, the base (with its net borrowing) and the people state's jobs, PCE, the fork, the money between
        /// industries, its boundaries, tech's trade and listed value, then the nodes (the tier node's value added beside it)
        /// and the build.
        /// </summary>
        public static string Line(InstantYear r)
        {
            StringBuilder b = new StringBuilder(1024);
            b.Append("class gross income $").Append(D2(r.ClassTotal)).Append("T in ").Append(r.Groups.Length.ToString(Ci))
                .Append(r.ClassSplit ? " groups" : " block").Append(" (PI + contributions $").Append(D2(r.PersonalIncomePlusContributions))
                .Append("T, ").Append(r.CircuitVintage ? "circuit.json's vintage" : "NIPA tables").Append(")");
            b.Append("; tiers raw/make/services/tech ");
            for (int t = 0; t < Tiers; t++) b.Append(t > 0 ? "/" : "").Append(D2(r.Tiers[t].ValueAdded));
            b.Append(" $T (market $").Append(D2(r.MarketValueAdded)).Append("T; government $").Append(D2(r.PublicValueAdded)).Append("T)");
            InstantState s = r.State;
            b.Append("; base outlays $").Append(D2(s.Outlays)).Append("T, receipts $").Append(D2(s.Receipts)).Append("T, borrows $")
                .Append(D2(s.Borrowing)).Append("T (NIPA 3.1 ").Append(r.FlowYear.ToString(Ci)).Append("; strata own work ")
                .Append(D2(s.OwnWork)).Append(" = pay ").Append(D2(s.Pay)).Append(" + wear ").Append(D2(s.Wear)).Append(", supplies net ")
                .Append(D2(s.PurchasesNet)).Append(", benefits and transfers ").Append(D2(s.Transfers)).Append(", interest ")
                .Append(D2(s.Interest)).Append(", subsidies ").Append(D2(s.Subsidies)).Append("; net borrowing with investment $")
                .Append(D2(s.NetBorrowing)).Append("T; ")
                .Append(double.IsNaN(s.Jobs) ? "no jobs data" : s.Jobs.ToString("0.00", Ci) + "M general government jobs").Append(")");
            if (!double.IsNaN(r.JobsDrawn))
            {
                b.Append("; people state ").Append(r.JobsDrawn.ToString("0.00", Ci)).Append("M jobs = tier floors ")
                    .Append((r.JobsDrawn - s.EnterpriseJobs - s.Jobs).ToString("0.00", Ci)).Append(" + government enterprises ")
                    .Append(s.EnterpriseJobs.ToString("0.00", Ci)).Append(" (with the market; their pay $").Append(D2(s.EnterprisePay))
                    .Append("T in the pay ribbon) + general government ").Append(s.Jobs.ToString("0.00", Ci)).Append(" (NIPA 6.5D all industries ")
                    .Append(r.JobsNipa.ToString("0.00", Ci)).Append("M)");
            }
            b.Append("; PCE $").Append(D2(r.Pce)).Append("T (NIPA PCE $").Append(D2(r.NipaPce)).Append("T)");
            b.Append("; fork ");
            int sum = 0;
            for (int k = 0; k < 5; k++)
            {
                b.Append(k > 0 ? "/" : "").Append(r.Dollar.Cents[k].ToString(Ci));
                sum += r.Dollar.Cents[k];
            }

            b.Append(" = ").Append(sum.ToString(Ci)).Append(" (pay, owners, upkeep, imports, taxes; largest remainders of ");
            for (int k = 0; k < 5; k++) b.Append(k > 0 ? "/" : "").Append((100 * r.Dollar.Shares[k]).ToString("0.00", Ci));
            b.Append("; fear ").Append(P1(r.FearShare)).Append("% J ").Append(r.FearConfidence.ToString("0.00", Ci)).Append(")");
            InstantBetween io = r.Between;
            b.Append("; io ").Append(io.Year.ToString(Ci)).Append(" $").Append(D2(io.Total)).Append("T up/down/within ")
                .Append(P1(io.Up)).Append('/').Append(P1(io.Down)).Append('/').Append(P1(io.Within)).Append(" and market ")
                .Append(P1(io.MarketUp)).Append('/').Append(P1(io.MarketDown)).Append('/').Append(P1(io.MarketWithin));
            b.Append("; boundaries down/up ");
            string[] bn = { "raw|make", "make|services", "services|tech" };
            for (int k = 0; k < 3; k++)
            {
                b.Append(k > 0 ? ", " : "").Append(bn[k]).Append(' ').Append(D2(io.BoundaryDown[k])).Append('/').Append(D2(io.BoundaryUp[k]));
            }

            b.Append(" $T; tech buys ").Append(D2(io.TechBuys)).Append(", sells ").Append(D2(io.TechSells)).Append(" + ")
                .Append(D2(io.TechToState)).Append(" to the state");
            b.Append("; listed value tech ")
                .Append(double.IsNaN(r.Tiers[3].MarketValueShare) ? "n/a" : "at least " + P1(r.Tiers[3].MarketValueShare) + "%")
                .Append(double.IsNaN(r.Stocks.Listed) ? "" : " of $" + D2(r.Stocks.Listed) + "T ($" + D2(r.Stocks.ListedNotSplit) + "T not split)");
            b.Append("; nodes");
            foreach (InstantNode n in r.Nodes)
            {
                if (n.Id.StartsWith("class:", StringComparison.Ordinal)) continue;
                b.Append(' ').Append(n.Id).Append(" in ").Append(D2(n.In)).Append(" out ").Append(D2(n.Out))
                    .Append(n.Other != null ? " (other " + D2(n.Other.Value) + ")" : n.Checked ? "" : " (unchecked: imports and payments abroad less exports)");
                if (n.Id == "tiers")
                {
                    b.Append(" (value added: out ").Append(D2(r.TierIncome)).Append(" and in ").Append(D2(r.TierDemand))
                        .Append(" less imports vs market ").Append(D2(r.MarketValueAdded)).Append(", ")
                        .Append((100 * r.TierIncomeGap).ToString("+0.0;-0.0", Ci)).Append("%/").Append((100 * r.TierDemandGap).ToString("+0.0;-0.0", Ci))
                        .Append("%)");
                }
            }

            b.Append("; checksum ").Append(r.Checksum).Append("; built in ").Append(r.BuildMs.ToString("0.0", Ci)).Append(" ms");
            return b.ToString();
        }

        /// <summary>The year's coverage flags in one line ("classes: households · not split by income before 2000; ...").</summary>
        public static string CoverageLine(InstantYear r)
        {
            StringBuilder b = new StringBuilder(512);
            foreach (InstantCoverage c in r.Coverage)
            {
                if (b.Length > 0) b.Append("; ");
                b.Append(c.Id).Append(c.Covered ? " yes" : " no").Append(" (").Append(c.Note).Append(')');
            }

            return b.ToString();
        }

        /// <summary>The flows in one line ("pay 13.27, owners 6.53, ..." $T), for the log.</summary>
        public static string FlowsLine(InstantYear r)
        {
            StringBuilder b = new StringBuilder(1024);
            foreach (InstantFlow f in r.Flows)
            {
                if (b.Length > 0) b.Append(", ");
                b.Append(f.Id).Append(' ').Append(D2(f.Value));
            }

            return b.Append(" ($T)").ToString();
        }
    }
}
