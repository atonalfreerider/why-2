using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;
using Why.Economy.Data;

namespace Why.Economy.Model
{
    /// <summary>
    /// Everything the economic lives read from the economy data, looked up once with documented fallbacks: household
    /// history series (with nominal series extended beyond their ends), the year's income totals the population is
    /// calibrated to, spending shares by income and by year, personality tilts, drives, strategies, tribes and the
    /// agency rule. A missing or malformed series never throws: the fallback is used and a note goes to
    /// <see cref="Warnings"/> (shown in the model's log). Read-only after construction (any thread).
    /// </summary>
    internal sealed class LivesInputs
    {
        /// <summary>The nine sampled traits, in the order of psyche.json traitCorrelations (the fallback order).</summary>
        public static readonly string[] TraitIds =
        {
            "conscientiousness", "neuroticism", "agreeableness", "openness", "extraversion", "internalLocus",
            "socialComparison", "lossAversion", "presentBiasBeta"
        };

        public const int TC = 0, TN = 1, TA = 2, TO = 3, TE = 4, TLocus = 5, TComparison = 6, TLambda = 7, TBeta = 8;

        /// <summary>Keys of psyche.json tilts and savingRatePp, in the order the model's tilt vector uses.</summary>
        public static readonly string[] TiltKeys =
        {
            "conscientiousness", "neuroticism", "agreeableness", "openness", "extraversion", "presentBias",
            "socialComparison", "lossAversion", "reason"
        };

        /// <summary>Spending categories (the first six of <see cref="EconomyData.CategoryIds"/>; saving is the seventh).</summary>
        public const int Spend = 6;

        public const int Saving = 6;

        // ---------------------------------------------------------------- income ratios to compensation (fallbacks)

        /// <summary>
        /// Personal income components relative to employee compensation, every year (circuit.json "personal" gives the
        /// circuit's year exactly and replaces its row): proprietors' income, capital income (interest + dividends +
        /// rental income of persons), transfers received (government social benefits + from business) and taxes paid
        /// (personal current taxes + contributions for social insurance). Columns: year, proprietors, capital, transfers,
        /// taxes. Copied from circuit.json because EconomyData does not expose those blocks yet (replace the table with
        /// CircuitFile.IncomeHistory / PersonalHistory once they are merged in):
        /// <list type="bullet">
        /// <item>1988-2025: personalHistory (BEA NIPA 2.6, every component), exact.</item>
        /// <item>1950-1987: proprietors / compensation from incomeHistory (NIPA 1.10); transfers interpolated between
        /// recalled NIPA 2.1 anchors (1950 .088, 1955 .070, 1960 .085, 1965 .092, 1970 .120, 1975 .180 after the 1972-74
        /// benefit raises and the recession, 1980 .172, 1985 .177; about +-10%) and taxes between 1950 .170, 1960 .221,
        /// 1970 .240, 1980 .287 (+-10%); capital is what makes disposable income equal personalHistory's DPI of the year
        /// (DPI - compensation - proprietors - transfers + taxes), so disposable income, outlays and saving match NIPA
        /// every year and the recall errors of the other two land in capital income (its 1951-53 dip mirrors the
        /// Korean War tax rise that the tax anchors smooth over).</item>
        /// </list>
        /// </summary>
        static readonly double[][] IncomeRatioRows =
        {
            new[] { 1950, 0.2370, 0.2027, 0.0880, 0.1700 }, new[] { 1951, 0.2294, 0.1386, 0.0844, 0.1751 },
            new[] { 1952, 0.2138, 0.1312, 0.0808, 0.1802 }, new[] { 1953, 0.1951, 0.1485, 0.0772, 0.1853 },
            new[] { 1954, 0.1975, 0.1896, 0.0736, 0.1904 }, new[] { 1955, 0.1921, 0.1966, 0.0700, 0.1955 },
            new[] { 1956, 0.1836, 0.1922, 0.0730, 0.2006 }, new[] { 1957, 0.1820, 0.2005, 0.0760, 0.2057 },
            new[] { 1958, 0.1896, 0.2281, 0.0790, 0.2108 }, new[] { 1959, 0.1759, 0.2192, 0.0820, 0.2159 },
            new[] { 1960, 0.1679, 0.2160, 0.0850, 0.2210 }, new[] { 1961, 0.1713, 0.2315, 0.0864, 0.2229 },
            new[] { 1962, 0.1661, 0.2267, 0.0878, 0.2248 }, new[] { 1963, 0.1609, 0.2268, 0.0892, 0.2267 },
            new[] { 1964, 0.1572, 0.2470, 0.0906, 0.2286 }, new[] { 1965, 0.1571, 0.2468, 0.0920, 0.2305 },
            new[] { 1966, 0.1512, 0.2169, 0.0976, 0.2324 }, new[] { 1967, 0.1443, 0.2172, 0.1032, 0.2343 },
            new[] { 1968, 0.1391, 0.2016, 0.1088, 0.2362 }, new[] { 1969, 0.1318, 0.1826, 0.1144, 0.2381 },
            new[] { 1970, 0.1248, 0.2177, 0.1200, 0.2400 }, new[] { 1971, 0.1262, 0.2363, 0.1320, 0.2447 },
            new[] { 1972, 0.1300, 0.2071, 0.1440, 0.2494 }, new[] { 1973, 0.1384, 0.2005, 0.1560, 0.2541 },
            new[] { 1974, 0.1264, 0.2045, 0.1680, 0.2588 }, new[] { 1975, 0.1248, 0.2485, 0.1800, 0.2635 },
            new[] { 1976, 0.1250, 0.2334, 0.1784, 0.2682 }, new[] { 1977, 0.1239, 0.2256, 0.1768, 0.2729 },
            new[] { 1978, 0.1261, 0.2172, 0.1752, 0.2776 }, new[] { 1979, 0.1214, 0.2150, 0.1736, 0.2823 },
            new[] { 1980, 0.1058, 0.2574, 0.1720, 0.2870 }, new[] { 1981, 0.1002, 0.2739, 0.1730, 0.2879 },
            new[] { 1982, 0.0904, 0.3104, 0.1740, 0.2888 }, new[] { 1983, 0.0925, 0.3283, 0.1750, 0.2897 },
            new[] { 1984, 0.1030, 0.3251, 0.1760, 0.2906 }, new[] { 1985, 0.1010, 0.3141, 0.1770, 0.2915 },
            new[] { 1986, 0.1008, 0.3120, 0.1743, 0.2924 }, new[] { 1987, 0.1052, 0.2858, 0.1715, 0.2933 },
            new[] { 1988, 0.1104, 0.2953, 0.1688, 0.2942 }, new[] { 1989, 0.1086, 0.3126, 0.1733, 0.3035 },
            new[] { 1990, 0.1057, 0.3092, 0.1787, 0.3008 }, new[] { 1991, 0.1027, 0.3035, 0.1936, 0.2953 },
            new[] { 1992, 0.1091, 0.2879, 0.2039, 0.2911 }, new[] { 1993, 0.1121, 0.2861, 0.2077, 0.2950 },
            new[] { 1994, 0.1140, 0.2889, 0.2069, 0.2999 }, new[] { 1995, 0.1146, 0.2986, 0.2105, 0.3052 },
            new[] { 1996, 0.1231, 0.3014, 0.2104, 0.3152 }, new[] { 1997, 0.1240, 0.3002, 0.2028, 0.3226 },
            new[] { 1998, 0.1263, 0.2993, 0.1940, 0.3268 }, new[] { 1999, 0.1289, 0.2804, 0.1899, 0.3282 },
            new[] { 2000, 0.1289, 0.2803, 0.1860, 0.3321 }, new[] { 2001, 0.1376, 0.2756, 0.1975, 0.3266 },
            new[] { 2002, 0.1418, 0.2626, 0.2095, 0.2940 }, new[] { 2003, 0.1413, 0.2617, 0.2121, 0.2806 },
            new[] { 2004, 0.1433, 0.2592, 0.2115, 0.2795 }, new[] { 2005, 0.1386, 0.2629, 0.2146, 0.2952 },
            new[] { 2006, 0.1405, 0.2781, 0.2158, 0.3048 }, new[] { 2007, 0.1263, 0.2909, 0.2193, 0.3115 },
            new[] { 2008, 0.1192, 0.3031, 0.2427, 0.3098 }, new[] { 2009, 0.1209, 0.2817, 0.2767, 0.2728 },
            new[] { 2010, 0.1399, 0.2752, 0.2934, 0.2803 }, new[] { 2011, 0.1493, 0.2933, 0.2867, 0.2882 },
            new[] { 2012, 0.1517, 0.3079, 0.2758, 0.2871 }, new[] { 2013, 0.1530, 0.2900, 0.2744, 0.3149 },
            new[] { 2014, 0.1481, 0.3001, 0.2748, 0.3178 }, new[] { 2015, 0.1389, 0.3037, 0.2769, 0.3243 },
            new[] { 2016, 0.1354, 0.3044, 0.2786, 0.3208 }, new[] { 2017, 0.1370, 0.3121, 0.2739, 0.3211 },
            new[] { 2018, 0.1365, 0.3159, 0.2716, 0.3136 }, new[] { 2019, 0.1359, 0.3179, 0.2749, 0.3166 },
            new[] { 2020, 0.1379, 0.3146, 0.3652, 0.3188 }, new[] { 2021, 0.1448, 0.3202, 0.3711, 0.3390 },
            new[] { 2022, 0.1390, 0.3277, 0.3079, 0.3680 }, new[] { 2023, 0.1367, 0.3505, 0.2996, 0.3263 },
            new[] { 2024, 0.1346, 0.3476, 0.3031, 0.3268 }, new[] { 2025, 0.1341, 0.3390, 0.3149, 0.3340 }
        };

        /// <summary>
        /// Dividend yield of US stocks (S&amp;P 500, recalled from Shiller's data, +-0.5 pp): the part of the equity total
        /// return paid out as income; the rest is a price gain that stays in the portfolio.
        /// </summary>
        static readonly double[][] DividendYieldRows =
        {
            new[] { 1946, 0.050 }, new[] { 1955, 0.045 }, new[] { 1960, 0.033 }, new[] { 1970, 0.035 },
            new[] { 1980, 0.050 }, new[] { 1990, 0.035 }, new[] { 2000, 0.012 }, new[] { 2010, 0.019 },
            new[] { 2020, 0.016 }, new[] { 2025, 0.013 }
        };

        /// <summary>
        /// Medicare benefits per person 65+, as a share of the average wage index (none before 1966): 1967-1980 recalled
        /// (CMS), 1990-2024 from circuit.json personalHistory medicare / Census 65+ population. In-kind income: NIPA
        /// counts it in personal income and the care it buys in consumption.
        /// </summary>
        static readonly double[][] MedicareRows =
        {
            new[] { 1965, 0.0 }, new[] { 1967, 0.046 }, new[] { 1970, 0.057 }, new[] { 1980, 0.113 },
            new[] { 1990, 0.164 }, new[] { 2000, 0.195 }, new[] { 2010, 0.306 }, new[] { 2020, 0.267 },
            new[] { 2024, 0.259 }
        };

        /// <summary>
        /// Share of people 65+ entitled to Social Security (retired workers, spouses, survivors), recalled from the SSA
        /// Annual Statistical Supplement: coverage expanded with the 1950s amendments.
        /// </summary>
        static readonly double[][] SocialSecurityCoverageRows =
        {
            new[] { 1946, 0.10 }, new[] { 1950, 0.16 }, new[] { 1955, 0.40 }, new[] { 1960, 0.62 }, new[] { 1965, 0.75 },
            new[] { 1970, 0.85 }, new[] { 1975, 0.90 }, new[] { 1980, 0.92 }, new[] { 2025, 0.93 }
        };

        // ---------------------------------------------------------------- read from the data

        public readonly EconomyData Data;
        public readonly List<string> Warnings = new List<string>();

        public readonly YearSeries Cpi, Gdp, Population, LaborShare, Awi, EarnMale, EarnFemale, ProfileMale, ProfileFemale;
        public readonly YearSeries LfprMale, LfprFemale, Unemployment, EmpPrimeMale, EmpPrimeFemale, SelfEmployment;
        public readonly YearSeries SsAverage, SsReplacement, CardApr, Mortgage30, Treasury10, EquityReturn, HousePrice, MedianHomePrice;
        public readonly YearSeries Homeownership, HomeownershipByAge, PovertyLine, MedianHouseholdIncome, SavingRate, Households;
        public readonly YearSeries TaxRate, TransferShare, SavingByPercentile, NetWorthByAge;
        public readonly YearSeries Top1Wealth, Top10Wealth, Bottom50Wealth, Top1WealthWid, Top10WealthWid, Bottom50WealthWid;
        public readonly YearSeries DebtToIncome, ConsumerCreditToIncome, Top1Income;
        public readonly YearSeries Dem, Rep, Ind;

        readonly YearSeries proprietorsRatio, capitalRatio, transfersRatio, taxesRatio, dividendYield, medicareRatio, ssCoverage;

        /// <summary>
        /// Wealth shares (percent) of the top 1%, top 10% and bottom 50% of households in a year: circuit.json groupHistory
        /// (DFA from 1989, Saez-Zucman spliced before), else history.json's DFA-spliced series; -1 when neither has it.
        /// </summary>
        public double DfaShare(int group, double year)
        {
            IReadOnlyList<YearSeries> g = Data.GroupWealthHistory;
            if (g != null && g.Count == 4 && !g[3].IsEmpty && !g[2].IsEmpty && !g[0].IsEmpty)
            {
                return 100 * (group == 3 ? g[3].At(year) : group == 2 ? g[2].At(year) + g[3].At(year) : g[0].At(year));
            }

            YearSeries s = group == 3 ? Top1Wealth : group == 2 ? Top10Wealth : Bottom50Wealth;
            return s.IsEmpty ? -1 : s.At(year);
        }

        /// <summary>A psyche.json agency target's value by id (control, fantasy and structure anchors), or the fallback.</summary>
        public double Target(string id, double fallback)
        {
            AgencyTarget t = Data.Psyche?.Agency?.Targets?.Find(x => x != null && x.Id == id);
            return t != null && t.Value > 0 ? t.Value : fallback;
        }

        /// <summary>Household net worth of the wealth groups in the circuit's data ($T; DFA), 0 when missing.</summary>
        public double DfaNetWorth()
        {
            double sum = 0;
            foreach (Group g in Data.Groups) sum += Math.Max(0, g.NetWorth);
            return sum;
        }

        /// <summary>
        /// The average Social Security benefit of a year ($): socialSecurityAvgBenefit, else the replacement ratio x
        /// the wage index (history.json socialSecurityReplacement), else 0.33 of the wage index.
        /// </summary>
        public double SocialSecurityAverage(double year)
        {
            double average = Dollars(SsAverage, year);
            if (average > 0) return average;
            return (SsReplacement.IsEmpty ? 0.33 : SsReplacement.At(year)) * WageIndex(year);
        }

        /// <summary>Employee compensation of the data's year over labor share x GDP (BEA personal vs GDI basis).</summary>
        public readonly double CompensationFactor;

        /// <summary>The year whose personal income the circuit gives exactly (circuit.json "year").</summary>
        public readonly int CircuitYear;

        /// <summary>The circuit's personal income components of <see cref="CircuitYear"/> ($B; 0 when missing).</summary>
        public readonly double CircuitWages, CircuitCompensation, CircuitProprietors, CircuitCapital, CircuitTransfers,
            CircuitTaxes, CircuitDisposable, CircuitPce, CircuitOutlays, CircuitSaving, CircuitSocialSecurity, CircuitMedicare;

        /// <summary>Income quintiles' spending shares over the six spending categories (renormalized), [quintile][category].</summary>
        public readonly double[][] QuintileShares = new double[5][];

        /// <summary>Spending shares by year (saving excluded, renormalized), per category.</summary>
        readonly YearSeries[] categoryHistory = new YearSeries[7];

        public readonly double[] FearShare = new double[7], Fantasy = new double[7];

        /// <summary>Weights of the desires and fears behind each category, [category][drive].</summary>
        public readonly double[][] CategoryDesire = new double[7][], CategoryFear = new double[7][];

        /// <summary>Log-multipliers of the categories' shares per unit of each tilt variable, [category][tilt].</summary>
        public readonly double[][] Tilts = new double[7][];

        /// <summary>Saving-rate shifts, percentage points per unit of each tilt variable.</summary>
        public readonly double[] SavingPp = new double[9];

        public static readonly string[] DesireIds = { "food", "collective", "law", "shelter", "sex" };
        public static readonly string[] FearIds = { "starvation", "isolation", "murder", "exposure", "childless" };

        /// <summary>Population baseline weights of the drives (each pole sums to 1) and how they vary with age.</summary>
        public readonly double[] DesireWeight = new double[5], FearWeight = new double[5];

        public readonly LifeStageCurve[] DesireStage = new LifeStageCurve[5], FearStage = new LifeStageCurve[5];

        /// <summary>The trait distributions and the Cholesky factor of their correlations.</summary>
        public readonly TraitDistribution[] TraitDist = new TraitDistribution[9];

        public readonly double[,] TraitCholesky;
        public readonly double TraitCorrelationCO;

        /// <summary>Strategy shares of adults (PdStrategy order) the strategy softmax is calibrated to.</summary>
        public readonly double[] StrategyTargets = new double[7];

        public readonly PdPayoff Payoff;
        public readonly float Noise, Continuation, Distrust, PartyInheritance;

        /// <summary>Agency rule (psyche.json agency.thresholds; see thresholdsNote).</summary>
        public readonly double RunwayYears, SavingTarget, SavingYears, DebtOk, DebtZero, ReasonOk, ReasonZero, FantasyOk,
            FantasyZero, WAutonomy, WSaving, WDebt, WReason, WFantasy;

        public readonly bool GateAutonomy;

        /// <summary>Share of adults in control in the target year (psyche.json agency.targets "in_control").</summary>
        public readonly double InControlTarget;

        public readonly int InControlYear;
        public readonly double HigherOsShare;

        /// <summary>Industries: employment shares by year and a pay tilt (log pay relative to the mean).</summary>
        public readonly int IndustryCount;

        public readonly double[] IndustryPayTilt;
        readonly YearSeries[] industryEmployment;

        public LivesInputs(EconomyData data)
        {
            Data = data;
            Cpi = Series("cpi");
            Gdp = data.Gdp;
            Population = Series("population");
            LaborShare = data.LaborShare;
            Awi = Series("averageWageIndex");
            EarnMale = Series("earningsMedianMale");
            EarnFemale = Series("earningsMedianFemale");
            ProfileMale = Series("ageEarningsMale");
            ProfileFemale = Series("ageEarningsFemale");
            LfprMale = Series("lfprMale");
            LfprFemale = Series("lfprFemale");
            Unemployment = Series("unemployment");
            EmpPrimeMale = Series("employmentRate25to54Male");
            EmpPrimeFemale = Series("employmentRate25to54Female");
            SelfEmployment = Series("selfEmploymentRate");
            SsAverage = Series("socialSecurityAvgBenefit");
            SsReplacement = Series("socialSecurityReplacement");
            CardApr = Series("creditCardApr");
            Mortgage30 = Series("mortgageRate30y");
            Treasury10 = Series("treasury10y");
            EquityReturn = Series("equityTotalReturn");
            HousePrice = Series("housePriceIndex");
            MedianHomePrice = Series("medianHomePrice");
            Homeownership = Series("homeownership");
            Households = Series("households");
            HomeownershipByAge = Series("homeownershipByAge");
            PovertyLine = Series("povertyLineFamily4");
            MedianHouseholdIncome = Series("medianHouseholdIncome");
            SavingRate = Series("savingRate");
            TaxRate = Series("effectiveTaxRate");
            TransferShare = Series("transferShareOfIncome");
            SavingByPercentile = Series("savingRateByPercentile");
            NetWorthByAge = Series("netWorthByAge");
            Top1Wealth = Series("top1WealthShare");
            Top10Wealth = Series("top10WealthShare");
            Bottom50Wealth = Series("bottom50WealthShare");
            Top1WealthWid = Series("top1WealthShareWID");
            Top10WealthWid = Series("top10WealthShareWID");
            Bottom50WealthWid = Series("bottom50WealthShareWID");
            DebtToIncome = Series("householdDebtToIncome");
            ConsumerCreditToIncome = Series("consumerCreditToIncome");
            Top1Income = Series("top1IncomeShare");

            proprietorsRatio = RatioSeries(1);
            capitalRatio = RatioSeries(2);
            transfersRatio = RatioSeries(3);
            taxesRatio = RatioSeries(4);
            dividendYield = new YearSeries(DividendYieldRows);
            medicareRatio = new YearSeries(MedicareRows);
            ssCoverage = new YearSeries(SocialSecurityCoverageRows);

            // the circuit's year: exact personal income components
            CircuitFile c = data.Circuit ?? new CircuitFile();
            CircuitYear = c.Year > 1900 ? c.Year : 2025;
            CircuitWages = c.PersonalOf("wages");
            CircuitCompensation = c.PersonalOf("compensation");
            CircuitProprietors = c.PersonalOf("proprietors");
            CircuitCapital = c.PersonalOf("interest") + c.PersonalOf("dividends") + c.PersonalOf("rental");
            CircuitTransfers = c.PersonalOf("transfers");
            CircuitTaxes = c.PersonalOf("personalTaxes") + c.PersonalOf("socialInsuranceContributions");
            CircuitDisposable = c.PersonalOf("disposableIncome");
            CircuitPce = c.PersonalOf("pce");
            CircuitOutlays = c.PersonalOf("outlays");
            if (CircuitOutlays <= 0) CircuitOutlays = CircuitPce + c.PersonalOf("interestPaid") + c.PersonalOf("transferPaymentsPaid");
            CircuitSaving = c.PersonalOf("saving");
            CircuitSocialSecurity = c.PersonalOf("socialSecurity");
            CircuitMedicare = c.PersonalOf("medicare");
            double modeled = LaborShare.At(CircuitYear) * Gdp.GrowthAt(CircuitYear);
            CompensationFactor = CircuitCompensation > 0 && modeled > 0 ? CircuitCompensation / modeled : 1.0;
            if (CircuitCompensation <= 0) Warnings.Add("circuit.json personal.compensation missing: wages follow labor share x GDP");
            if (LaborShare.IsEmpty || Gdp.IsEmpty) Warnings.Add("labor share or GDP missing: wages follow the earnings medians");

            ReadSpending(data);
            ReadPsyche(data.Psyche ?? new PsycheFile(), out TraitCholesky, out TraitCorrelationCO);
            HigherOsShare = Clamp((data.Psyche ?? new PsycheFile()).HigherOsShare, 0.1, 0.9);

            // agency rule
            AgencyData ag = data.Psyche?.Agency ?? new AgencyData();
            RunwayYears = Math.Max(0.1, ag.Threshold("runwayYears", 3));
            SavingTarget = Math.Max(0.01, ag.Threshold("savingRate", 0.1));
            SavingYears = Clamp(ag.Threshold("savingYears", 3), 1, 3);
            DebtOk = ag.Threshold("debtService", 0.15);
            DebtZero = Math.Max(DebtOk + 0.01, ag.Threshold("debtServiceZero", 0.4));
            ReasonOk = ag.Threshold("reason", 0.6);
            ReasonZero = Math.Min(ReasonOk - 0.01, ag.Threshold("reasonZero", 0.3));
            FantasyOk = ag.Threshold("fantasy", 0.15);
            FantasyZero = Math.Max(FantasyOk + 0.01, ag.Threshold("fantasyZero", 0.3));
            GateAutonomy = ag.Threshold("gateAutonomy", 1) >= 0.5;
            WAutonomy = ag.Threshold("wAutonomy", 0.35);
            WSaving = ag.Threshold("wSaving", 0.15);
            WDebt = ag.Threshold("wDebt", 0.1);
            WReason = ag.Threshold("wReason", 0.25);
            WFantasy = ag.Threshold("wFantasy", 0.15);
            double wSum = WAutonomy + WSaving + WDebt + WReason + WFantasy;
            if (wSum <= 0)
            {
                Warnings.Add("agency weights missing: equal weights");
                WAutonomy = WSaving = WDebt = WReason = WFantasy = 0.2;
            }
            else if (Math.Abs(wSum - 1) > 0.01)
            {
                Warnings.Add($"agency weights sum to {wSum.ToString("0.00", CultureInfo.InvariantCulture)}: normalized");
                WAutonomy /= wSum;
                WSaving /= wSum;
                WDebt /= wSum;
                WReason /= wSum;
                WFantasy /= wSum;
            }

            InControlTarget = 0.12;
            InControlYear = 2025;
            AgencyTarget target = ag.Targets?.Find(t => t != null && t.Id == "in_control");
            if (target != null && target.Value > 0 && target.Value < 1)
            {
                InControlTarget = target.Value;
                if (target.Year > 1900) InControlYear = target.Year;
            }
            else
            {
                Warnings.Add("agency target 'in_control' missing: 0.12 of adults in 2025");
            }

            // games: strategy mix, payoffs, noise, the shadow of the future, tribes
            GamesFile g = data.Games ?? new GamesFile();
            double sum = 0;
            if (g.Strategies != null)
            {
                foreach (StrategyInfo s in g.Strategies)
                {
                    if (s?.Id == null || !Enum.TryParse(s.Id, false, out PdStrategy id) || s.PopulationShare <= 0) continue;
                    StrategyTargets[(int)id] += s.PopulationShare;
                    sum += s.PopulationShare;
                }
            }

            if (sum <= 0)
            {
                Warnings.Add("games strategies' populationShare missing: the games' default mix");
                for (int i = 0; i < 7; i++) StrategyTargets[i] = GamesSetup.DefaultMix[i];
                sum = 1;
            }

            for (int i = 0; i < 7; i++) StrategyTargets[i] /= sum;
            Payoff = GamesSetup.Payoff(data);
            Noise = (float)Clamp(GamesSetup.Spec(data, "noise", 0.02f), 0, 0.5);
            Continuation = (float)Clamp(GamesSetup.Spec(data, "continuation", 0.95f), 0, 0.995);
            Distrust = (float)Clamp(GamesSetup.Spec(data, "distrust", 0.22f), 0, 1);
            PartyInheritance = (float)Clamp(GamesSetup.Spec(data, "partyInheritance", 0.6f), 0, 1);
            ReadParties(g.Tribes?.PartyId, out Dem, out Rep, out Ind);

            // industries: employment shares and pay
            IReadOnlyList<Industry> inds = data.Industries;
            IndustryCount = inds.Count;
            industryEmployment = new YearSeries[IndustryCount];
            IndustryPayTilt = new double[IndustryCount];
            double payYear = 2024, logSum = 0, weightSum = 0;
            double[] pay = new double[IndustryCount];
            for (int i = 0; i < IndustryCount; i++)
            {
                industryEmployment[i] = inds[i].Employment;
                double emp = inds[i].Employment.At(payYear), va = inds[i].ValueAdded.GrowthAt(payYear);
                pay[i] = emp > 0 && va > 0 ? inds[i].CompShare * va / emp : 0;
                if (pay[i] > 0)
                {
                    logSum += emp * Math.Log(pay[i]);
                    weightSum += emp;
                }
            }

            double meanLog = weightSum > 0 ? logSum / weightSum : 0;
            for (int i = 0; i < IndustryCount; i++) IndustryPayTilt[i] = pay[i] > 0 ? Math.Log(pay[i]) - meanLog : 0;
        }

        // ---------------------------------------------------------------- lookups

        static double Clamp(double x, double lo, double hi) => x < lo ? lo : x > hi ? hi : x;

        YearSeries Series(string id)
        {
            YearSeries s = Data.HistorySeries(id);
            if (s.IsEmpty) Warnings.Add($"history series '{id}' missing: fallback used");
            return s;
        }

        static YearSeries RatioSeries(int column)
        {
            List<double[]> points = new List<double[]>();
            foreach (double[] row in IncomeRatioRows) points.Add(new[] { row[0], row[column] });
            return new YearSeries(points);
        }

        /// <summary>
        /// A dollar series at a year: geometric between points; before the first point moved with consumer prices,
        /// after the last with nominal GDP per person (the series' own trend is unknown there); 0 when empty.
        /// </summary>
        public double Dollars(YearSeries s, double year)
        {
            if (s.IsEmpty) return 0;
            if (year < s.FirstYear)
            {
                double c0 = Cpi.GrowthAt(s.FirstYear), c1 = Cpi.GrowthAt(year);
                return c0 > 0 && c1 > 0 ? s.First * c1 / c0 : s.First;
            }

            if (year > s.LastYear)
            {
                double g0 = PerPersonGdp(s.LastYear), g1 = PerPersonGdp(year);
                return g0 > 0 && g1 > 0 ? s.Last * g1 / g0 : s.Last;
            }

            return s.GrowthAt(year);
        }

        double PerPersonGdp(double year)
        {
            double gdp = Gdp.GrowthAt(year), pop = Population.IsEmpty ? 1 : Population.At(year);
            return pop > 0 ? gdp / pop : gdp;
        }

        /// <summary>A rate series at a year (held at its ends), or the fallback when the series is empty.</summary>
        public static double Rate(YearSeries s, double year, double fallback) => s.IsEmpty ? fallback : s.At(year);

        /// <summary>
        /// A yearly return (%/100) at a year; before the series starts, its mean over its first ten points (the
        /// late-1940s are not in the data).
        /// </summary>
        public static double Return(YearSeries s, double year, double fallback)
        {
            if (s.IsEmpty) return fallback;
            if (year >= s.FirstYear) return s.At(year) / 100.0;
            int n = Math.Min(10, s.Count);
            double sum = 0;
            for (int i = 0; i < n; i++) sum += s.ValueAt(i);
            return sum / n / 100.0;
        }

        /// <summary>Consumer prices relative to a reference year (1 when the CPI is missing).</summary>
        public double Prices(double year, double reference)
        {
            double a = Cpi.GrowthAt(year), b = Cpi.GrowthAt(reference);
            return a > 0 && b > 0 ? a / b : 1;
        }

        /// <summary>The SSA average wage index of a year ($; from the median earnings when missing).</summary>
        public double WageIndex(double year)
        {
            double awi = Dollars(Awi, year);
            if (awi > 0) return awi;
            double m = Dollars(EarnMale, year);
            return m > 0 ? 1.25 * m : 50_000;
        }

        /// <summary>Employee compensation of persons in a year ($B): labor share x GDP, scaled to the circuit's year.</summary>
        public double CompensationTarget(double year)
        {
            double modeled = LaborShare.At(year) * Gdp.GrowthAt(year);
            return modeled > 0 ? modeled * CompensationFactor : 0;
        }

        /// <summary>A personal income component in a year ($B) from its ratio to compensation; exact in the circuit's year.</summary>
        public double ProprietorsTarget(double year) =>
            Exact(year, CircuitProprietors) ?? proprietorsRatio.At(year) * CompensationTarget(year);

        public double CapitalTarget(double year) => Exact(year, CircuitCapital) ?? capitalRatio.At(year) * CompensationTarget(year);

        public double TransfersTarget(double year) =>
            Exact(year, CircuitTransfers) ?? transfersRatio.At(year) * CompensationTarget(year);

        public double TaxesTarget(double year) => Exact(year, CircuitTaxes) ?? taxesRatio.At(year) * CompensationTarget(year);

        double? Exact(double year, double value)
        {
            if (value <= 0 || Math.Abs(year - CircuitYear) > 0.5) return null;
            double comp = CompensationTarget(year);
            return CircuitCompensation > 0 && comp > 0 ? value * comp / CircuitCompensation : value;
        }

        public double DividendYield(double year) => dividendYield.At(year);

        /// <summary>Medicare per person 65+ as a share of the wage index (0 before 1966).</summary>
        public double MedicareRatio(double year) => year < 1966 ? 0 : medicareRatio.At(year);

        public double SocialSecurityCoverage(double year) => ssCoverage.At(year);

        /// <summary>Spending shares (six categories, summing to 1) the population is calibrated to in a year.</summary>
        public void CategoryTargets(double year, double[] shares)
        {
            double sum = 0;
            for (int c = 0; c < Spend; c++)
            {
                shares[c] = categoryHistory[c].IsEmpty ? 0 : Math.Max(0, categoryHistory[c].At(year));
                sum += shares[c];
            }

            if (sum <= 0)
            {
                // no history: the pooled quintile shares
                for (int c = 0; c < Spend; c++)
                {
                    shares[c] = 0;
                    for (int q = 0; q < 5; q++) shares[c] += QuintileShares[q][c] / 5;
                }

                return;
            }

            for (int c = 0; c < Spend; c++) shares[c] /= sum;
        }

        /// <summary>Base spending shares of an income percentile (0..100), interpolated between quintile midpoints.</summary>
        public void BaseShares(double percentile, double[] shares)
        {
            double x = Clamp((percentile - 10) / 20, 0, 4);
            int q0 = Math.Min(3, (int)Math.Floor(x));
            double f = x - q0;
            for (int c = 0; c < Spend; c++) shares[c] = QuintileShares[q0][c] * (1 - f) + QuintileShares[q0 + 1][c] * f;
        }

        /// <summary>Industry employment shares of a year (sum 1; empty industries 0).</summary>
        public void IndustryShares(double year, double[] shares)
        {
            double sum = 0;
            for (int i = 0; i < IndustryCount; i++)
            {
                shares[i] = Math.Max(0, industryEmployment[i].At(year));
                sum += shares[i];
            }

            for (int i = 0; i < IndustryCount; i++) shares[i] = sum > 0 ? shares[i] / sum : 1.0 / IndustryCount;
        }

        // ---------------------------------------------------------------- reading

        void ReadSpending(EconomyData data)
        {
            for (int c = 0; c < 7; c++) categoryHistory[c] = data.CategoryHistory.Count > c ? data.CategoryHistory[c] : YearSeries.Empty;

            // quintile base shares: the survey's shares of income, renormalized over the six spending categories (the
            // yearly calibration absorbs the survey's under-reporting of each category, see spending.json ceToPce)
            List<Quintile> qs = data.Spending?.ByQuintile;
            bool ok = qs != null && qs.Count >= 5;
            for (int q = 0; q < 5; q++)
            {
                QuintileShares[q] = new double[Spend];
                double sum = 0;
                for (int c = 0; c < Spend && ok; c++)
                {
                    QuintileShares[q][c] = Math.Max(0, qs[q]?.ShareOf(EconomyData.CategoryIds[c]) ?? 0);
                    sum += QuintileShares[q][c];
                }

                if (sum <= 0)
                {
                    if (ok) Warnings.Add($"spending.byQuintile[{q}] has no shares: flat shares");
                    for (int c = 0; c < Spend; c++) QuintileShares[q][c] = 1.0 / Spend;
                    continue;
                }

                for (int c = 0; c < Spend; c++) QuintileShares[q][c] /= sum;
            }

            if (!ok) Warnings.Add("spending.byQuintile missing: flat shares by income");

            for (int c = 0; c < 7; c++)
            {
                Category cat = data.Categories.Count > c ? data.Categories[c] : null;
                FearShare[c] = Clamp(cat?.FearShare ?? 0.5, 0, 1);
                Fantasy[c] = Clamp(cat?.Fantasy ?? 0, 0, 1);
                CategoryDesire[c] = Weights(cat?.Desire, DesireIds);
                CategoryFear[c] = Weights(cat?.Fear, FearIds);
            }
        }

        static double[] Weights(Dictionary<string, double> d, string[] ids)
        {
            double[] w = new double[ids.Length];
            double sum = 0;
            for (int i = 0; i < ids.Length; i++)
            {
                w[i] = d != null && d.TryGetValue(ids[i], out double v) ? Math.Max(0, v) : 0;
                sum += w[i];
            }

            for (int i = 0; i < ids.Length; i++) w[i] = sum > 0 ? w[i] / sum : 1.0 / ids.Length;
            return w;
        }

        void ReadPsyche(PsycheFile p, out double[,] cholesky, out double rCO)
        {
            for (int i = 0; i < 9; i++) TraitDist[i] = p.Trait(TraitIds[i]);

            // correlations in this model's trait order (the file may order them differently)
            double[,] r = new double[9, 9];
            for (int i = 0; i < 9; i++) r[i, i] = 1;
            string[] order = p.Correlations?.Order;
            double[][] m = p.Correlations?.Matrix;
            if (order != null && m != null && m.Length == order.Length)
            {
                int[] map = new int[9];
                for (int i = 0; i < 9; i++) map[i] = Array.IndexOf(order, TraitIds[i]);
                for (int i = 0; i < 9; i++)
                {
                    for (int j = 0; j < 9; j++)
                    {
                        if (i == j || map[i] < 0 || map[j] < 0 || m[map[i]] == null || m[map[i]].Length <= map[j]) continue;
                        r[i, j] = Clamp(m[map[i]][map[j]], -0.95, 0.95);
                    }
                }
            }
            else
            {
                Warnings.Add("psyche.json traitCorrelations missing: traits drawn independently");
            }

            if (!LivesMath.Cholesky(r, 9, out cholesky))
            {
                Warnings.Add("psyche.json traitCorrelations not positive definite: traits drawn independently");
                for (int i = 0; i < 9; i++)
                {
                    for (int j = 0; j < 9; j++) r[i, j] = i == j ? 1 : 0;
                }
            }

            rCO = r[TC, TO];

            for (int c = 0; c < 7; c++)
            {
                Tilts[c] = new double[TiltKeys.Length];
                for (int k = 0; k < TiltKeys.Length; k++) Tilts[c][k] = Clamp(p.Tilt(EconomyData.CategoryIds[c], TiltKeys[k]), -1, 1);
            }

            for (int k = 0; k < TiltKeys.Length; k++) SavingPp[k] = Clamp(p.SavingPp(TiltKeys[k]), -10, 10);

            ReadDrives(p.Desires, DesireIds, DesireWeight, DesireStage, "desires");
            ReadDrives(p.Fears, FearIds, FearWeight, FearStage, "fears");
        }

        void ReadDrives(List<Drive> drives, string[] ids, double[] weights, LifeStageCurve[] stages, string pole)
        {
            double sum = 0;
            for (int i = 0; i < ids.Length; i++)
            {
                Drive d = drives?.Find(x => x != null && x.Id == ids[i]);
                weights[i] = d != null && d.Weight > 0 ? d.Weight : 0;
                stages[i] = d?.LifeStage ?? new LifeStageCurve { PeakAge = 40, Spread = 40, Floor = 0.8 };
                sum += weights[i];
            }

            if (sum <= 0) Warnings.Add($"psyche.json {pole} weights missing: equal weights");
            for (int i = 0; i < ids.Length; i++) weights[i] = sum > 0 ? weights[i] / sum : 1.0 / ids.Length;
        }

        void ReadParties(List<JArray> rows, out YearSeries dem, out YearSeries rep, out YearSeries ind)
        {
            List<double[]> d = new List<double[]>(), r = new List<double[]>(), i = new List<double[]>();
            if (rows != null)
            {
                foreach (JArray row in rows)
                {
                    if (row == null || row.Count < 2 || !(row[1] is JObject o)) continue;
                    if (row[0].Type != JTokenType.Integer && row[0].Type != JTokenType.Float) continue;
                    double year = row[0].Value<double>();
                    double vd = Number(o, "dem"), vr = Number(o, "rep"), vi = Number(o, "ind");
                    double sum = vd + vr + vi;
                    if (sum <= 0) continue;
                    d.Add(new[] { year, vd / sum });
                    r.Add(new[] { year, vr / sum });
                    i.Add(new[] { year, vi / sum });
                }
            }

            if (d.Count == 0)
            {
                // Gallup's three-way split of 2025 (games.json partyIdNote), held for every year
                Warnings.Add("games.json tribes.partyId missing: 27% / 27% / 46% every year");
                d.Add(new[] { 2000.0, 0.27 });
                r.Add(new[] { 2000.0, 0.27 });
                i.Add(new[] { 2000.0, 0.46 });
            }

            dem = new YearSeries(d);
            rep = new YearSeries(r);
            ind = new YearSeries(i);
        }

        static double Number(JObject o, string key) =>
            o.TryGetValue(key, out JToken v) && (v.Type == JTokenType.Float || v.Type == JTokenType.Integer)
                ? Math.Max(0, v.Value<double>())
                : 0;
    }
}
