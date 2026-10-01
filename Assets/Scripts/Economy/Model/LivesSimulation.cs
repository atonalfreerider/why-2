using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Why.Humans;
using Why.Humans.Smv;

namespace Why.Economy.Model
{
    /// <summary>What one simulated year was calibrated with (the model's log reports them).</summary>
    internal sealed class YearCalibration
    {
        /// <summary>Multipliers that bring the population's raw totals to the year's data ($B targets).</summary>
        public double Wages = 1, Business = 1, Capital = 1, Taxes = 1, OtherTransfers = 1, SocialSecurity = 1;

        /// <summary>Additive shift of every household's saving rate so the aggregate matches the year's rate.</summary>
        public double SavingShift, SavingRateTarget;

        /// <summary>Per spending category: the factor that brings the population's mean share to the year's data.</summary>
        public readonly double[] Categories = new double[LivesInputs.Spend];

        public double EmploymentScaleMale = 1, EmploymentScaleFemale = 1, SelfEmploymentOffset, HomeOffset;

        /// <summary>Per sex: the factor that puts the median earner at the year's median earnings.</summary>
        public double EarningsMale = 1, EarningsFemale = 1;

        /// <summary>Common shift of the reason logits that keeps the adults' mean at os.higher.share.</summary>
        public double ReasonShift;

        public int HomeNeed;

        /// <summary>Transfers the data has beyond Social Security and Medicare ($B; negative = the ramp undershoots).</summary>
        public double OtherTransfersTarget;
    }

    /// <summary>
    /// Pass B of the economic lives: the year-major simulation from the first to the last year with data. Each year
    /// (evaluated at mid-year): deaths pass their estates on (spouse first, else living children equally, else
    /// grandchildren, parents or siblings), marriages pool households (the wife's index heads a couple), as many
    /// households as the census counts have a home of their own, then
    /// <list type="number">
    /// <item>individual income: employment (a persistent latent against the era's employment rate by sex and age),
    /// self-employment (a propensity against the era's rate), the industry worked in (sticky, tilted by earnings
    /// rank toward well-paid industries), earnings (the age profile x the person's rank on a lognormal with a Pareto
    /// top tail, scaled so each sex's median earner earns the era's median), capital income on financial wealth and
    /// homes, Social Security from the career's indexed earnings with spouse and survivor benefits;</item>
    /// <item>households: transfers (Medicare to 65+, means-tested support by income percentile), taxes by income
    /// percentile, disposable income;</item>
    /// <item>saving rate (by income percentile + traits + life stage + children, shifted so the aggregate matches the
    /// year's personal saving rate, large uninsured bills included), spending, and its split into the six categories
    /// (the income quintile's shares x personality, drive and life-stage tilts, scaled so the population's shares
    /// match the year's data);</item>
    /// <item>the state of mind: fear share and fantasy share of spending, reason (propensity + maturity - stress,
    /// centered on os.higher.share), future orientation and agency (psyche.json agency rule);</item>
    /// <item>the balance sheet: mortgage principal, consumer debt, financial assets with returns (richer households
    /// hold more stocks), debt discharge, homes (bought by renters at a pace that keeps the era's homeownership rate),
    /// business equity of the self-employed.</item>
    /// </list>
    /// Money is conserved: net worth changes only by saving, holding gains, business closures, debt discharged,
    /// balance sheets brought into the record and estates leaving it (the yearly money check).
    /// After the years, a parallel pass per year builds what nothing later depends on: tribes, cooperation (the
    /// person's strategy against the year's partners), wealth groups, children's household fields and the totals.
    /// Every income total is scaled to the year's data (compensation = labor share x GDP; other components by their
    /// yearly ratio to compensation, which makes disposable income NIPA's, exact in the circuit's year). Random draws
    /// come from each person's own stream, a fixed number per adult-year; parallel loops only write their own person or
    /// household, and every sum is taken in a fixed order, so results do not depend on threads.
    /// </summary>
    internal sealed class LivesSimulation
    {
        // ---------------------------------------------------------------- constants (evidence in comments)

        /// <summary>Uniform draws per adult-year (always all of them, so streams stay aligned): employment, industry
        /// switch, industry choice, earnings noise, self-employment noise and decision, home sale, home purchase, loss
        /// shock and its size.</summary>
        const int Draws = 10;

        /// <summary>Ages tabulated for the curves that depend on age (life stages, earnings profile, employment shape).</summary>
        const int MaxAge = 121;

        /// <summary>Year-to-year persistence of the employment latent (AR(1)); with an 80% employment rate a worker stays
        /// employed with p ~0.95, the DESIGN's ~0.93 (CPS flows: most job losses are short).</summary>
        const double EmploymentPersistence = 0.9;

        /// <summary>Higher earners are employed more often (CPS employment by education); loading of the rank on the latent.</summary>
        const double EmploymentRankLoading = 0.35;

        /// <summary>Employment by age relative to prime age 25-54 (BLS employment-population ratios 2024 by age, recalled;
        /// ages outside 25-54 are scaled per year and sex so the 16+ rate matches the data).</summary>
        static readonly double[][] EmploymentByAge =
        {
            new double[] { 18, 0.50 }, new double[] { 20, 0.80 }, new double[] { 24, 0.90 }, new double[] { 25, 1.0 },
            new double[] { 54, 1.0 }, new double[] { 58, 0.88 }, new double[] { 61, 0.78 }, new double[] { 63, 0.60 },
            new double[] { 66, 0.38 }, new double[] { 70, 0.22 }, new double[] { 75, 0.10 }, new double[] { 80, 0.05 },
            new double[] { 90, 0.02 }
        };

        /// <summary>Share of workers changing industry in a year (BLS/CPS industry mobility ~8-10%, recalled).</summary>
        const double IndustrySwitchRate = 0.08;

        /// <summary>How strongly the earnings rank steers the industry (per sd of rank, times log relative pay).</summary>
        const double IndustryPaySteer = 0.8;

        /// <summary>
        /// Dispersion of log earnings by rank (CPS all workers incl. part time: lognormal sd ~0.85); the yearly
        /// transitory part (DESIGN 0.12); above the 99th rank a Pareto tail with index <see cref="TailAlpha"/> (top
        /// earners, missing in survey medians). With the medians matched to the data, these give earners a mean of
        /// 1.6-1.7 times the median (wages + proprietors' income per worker with earnings over the CPS median, ~1.6 in
        /// 2025) and the top 1% of earners 11-15% of earnings (Piketty-Saez-Zucman wage income ~10-12% recently, less
        /// before 1980: the shape is held fixed). A sd of 0.72 with an index of 1.6 gave 1.43 and 6-12%: the national
        /// total then lifted every earner, the median ones ~18% above the data, and left too little at the top.
        /// </summary>
        const double EarningsSigma = 0.85, EarningsNoise = 0.12, TailRank = 0.99, TailAlpha = 1.4;

        /// <summary>The age profiles are relative to the peak; the all-ages median sits at 0.79 (men) / 0.82 (women) of
        /// it (history.json ageEarnings notes).</summary>
        const double ProfileMedianMale = 0.79, ProfileMedianFemale = 0.82;

        /// <summary>Self-employment: persistence (logit; spells last years: 25-30% of the self-employed stop in a year,
        /// in line with CPS year-to-year exit rates of ~20-30%; 2.5 made half of them stop every year) and the pull of
        /// earnings rank (owners are concentrated at the top, SCF).</summary>
        const double SelfEmploymentPersistence = 4.5, SelfEmploymentRank = 0.4;

        /// <summary>
        /// Private business equity: a going concern is worth this multiple of its income (DFA 2025: private business
        /// equity $16.0T against $2.1T of proprietors' income, ~7.6) and grows into that value at this rate a year; when
        /// nobody in the household runs it any more it closes or is sold for <see cref="BusinessExit"/> of it (most small
        /// businesses close; the ones sold fetch ~2-4x earnings, so ~0.3 x 7 on average; judgment). The value created
        /// while it runs is a holding gain, like a stock's; only the exit share becomes money.
        /// </summary>
        const double BusinessMultiple = 7, BusinessGrowth = 0.3, BusinessExit = 0.3;

        /// <summary>Imputed and actual net rent per dollar of home value (NIPA rental income of persons ~$1.1T on ~$48T of
        /// household real estate, 2025); scaled with the rest of capital income to the year's total.</summary>
        const double RentYield = 0.025;

        /// <summary>
        /// Stocks' share of financial assets: base, rise per log of assets in wage-index units, fall per unit of loss
        /// aversion above 2, bounds (judgment fitted to the DFA: households hold ~$55T of stocks and funds directly plus
        /// pension equity, about 40% of financial assets, concentrated at the top).
        /// </summary>
        const double EquityBase = 0.04, EquityPerLog = 0.09, EquityLossAversion = 0.04, EquityMin = 0.02, EquityMax = 0.85;

        /// <summary>Mortgage term (years) and the down payment buyers make when they can (20%; FHA minimum 3.5%).</summary>
        const double MortgageTerm = 30, DownPayment = 0.2, MinDownPayment = 0.035;

        /// <summary>Unsecured borrowing limit: a share of disposable income plus a share of home equity (judgment;
        /// credit lines ~a fifth of income, home-equity lines).</summary>
        const double DebtLimitIncome = 0.2, DebtLimitEquity = 0.3;

        /// <summary>
        /// Principal due on consumer debt per year, in the debt-service measure: installment loans of 5-10 years and card
        /// minimums of 1-3% a month, ~15% of the balance a year (the Fed's consumer debt service ratio, ~5.5% of DPI on
        /// credit of ~22% of DPI, implies ~25% a year with interest; judgment).
        /// </summary>
        const double CardPrincipal = 0.15;

        /// <summary>Homeowners from this age sell (moving to care, to family) at this yearly rate (judgment).</summary>
        const double SaleAge = 80, SaleRate = 0.04;

        /// <summary>Singles below this age without children are counted as living with family: no household of their own
        /// for housing, and no dissaving or borrowing (the family covers the gap; Census: most 18-24s live with parents).</summary>
        const double OwnHouseholdAge = 25;

        /// <summary>
        /// Who among the singles keeps a home of their own: a lasting personal draw (sd 1) plus this much per year of age
        /// up to <see cref="AloneAgeTop"/> and per sd of earnings rank (living alone rises with age and means:
        /// one-person households are 29% of households, most of them past 50; young singles share with partners,
        /// roommates and parents; the very old who sold their home move in with family or into care; judgment).
        /// </summary>
        const double AloneAge = 0.04, AloneAgeTop = 65, AloneRank = 0.3;

        /// <summary>Bounds of a household's saving rate (of disposable income; judgment).</summary>
        const double SavingRateMin = -0.8, SavingRateMax = 0.85;

        /// <summary>Saving rate per child under 18 (children add needs; CE spending of parents, judgment).</summary>
        const double ChildSaving = -0.015;

        /// <summary>Saving rate of the top 1% (circuit.json groups top1 savingRate 0.35, Saez-Zucman) at the 99.5th
        /// income percentile; between the 90th percentile and there, interpolated in log distance to the top.</summary>
        const double TopSavingRate = 0.35;

        /// <summary>Strength of the drives' pull on spending: log share per log of the motive's relative weight (judgment).</summary>
        const double DriveTiltGain = 0.5;

        /// <summary>Log tilts per child under 18 (CE: parents spend more on necessities and education, less on going out;
        /// judgment), in category order.</summary>
        static readonly double[] ChildTilt = { 0.06, -0.05, 0.0, -0.02, 0.12, 0.0 };

        /// <summary>Jeopardy (out-of-pocket health care, insurance) rises with age: log share per year from 45 (judgment).</summary>
        const double HealthAgeTilt = 0.012;

        /// <summary>Growth (education) is largest for the young: log share per year below 30 (students; judgment).</summary>
        const double StudentTilt = 0.03;

        /// <summary>Stress lowers reason (logit units): debt service (at the agency rule's zero point), a job lost in
        /// working age, a buffer under three months (judgments; sleep, scarcity and stress effects, psyche.json os).</summary>
        const double StressDebt = 1.0, StressJobless = 0.5, StressBuffer = 0.4;

        /// <summary>Who a person deals with in a year: the spouse, a coworker or neighbor, someone of another tribe
        /// (weights of the cooperation mix; judgment).</summary>
        const double CoopSpouse = 0.4, CoopCommunity = 0.35, CoopOutTribe = 0.25;

        /// <summary>Games per strategy pair when tabulating cooperation (fixed seed).</summary>
        const int CoopGames = 120;

        /// <summary>Initial wealth: dispersion of log net worth around the SCF median by age (lognormal sigma 1.7 gives
        /// top 1% / top 10% / bottom 50% shares near 1950's 24 / 65 / 3%), and the era's level relative to 2022 (net worth
        /// / income ~4.6 in 1950 vs 7.6 in 2022).</summary>
        const double InitialWealthSigma = 1.7, InitialWealthEra = 0.6;

        /// <summary>Immigrants arrive with this share of a native's balance sheet and of the homeownership chance (judgment).</summary>
        const double ImmigrantWealth = 0.25, ImmigrantOwnership = 0.3;

        /// <summary>Initial net worth is the lognormal minus this share of the median (floored at 0): the bottom of 1950
        /// held almost nothing (bottom 50% ~3% of wealth), which a lognormal alone cannot give.</summary>
        const double InitialWealthOffset = 0.35;

        /// <summary>
        /// Jeopardy realized: each household-year a large uninsured bill (an illness, a lawsuit, a repair, a fraud)
        /// strikes with this chance and costs a uniform 0.25-1.5 years of disposable income, paid from financial assets,
        /// then borrowed (judgment; ~4 in 10 adults faced a major unexpected expense in a year and ~1 in 5 households
        /// carry medical debt, SHED / Census SIPP, recalled). An outlay within jeopardy, inside the year's saving
        /// calibration: it moves money from the unlucky to the industries, and the aggregate saving rate stays the
        /// data's.
        /// </summary>
        const double ShockRate = 0.04, ShockMin = 0.25, ShockMax = 1.5;

        /// <summary>Index of jeopardy among the spending categories (EconomyData.CategoryIds).</summary>
        const int JeopardyIndex = 2;

        /// <summary>Consumer debt above this many years of disposable income is discharged down to
        /// <see cref="DefaultKeep"/> of it (bankruptcy, charge-off; ~0.5-1% of households a year, recalled).</summary>
        const double DefaultLimit = 2.0, DefaultKeep = 0.5;

        // ---------------------------------------------------------------- inputs

        readonly LivesInputs inp;
        readonly SmvSimulation sim;
        readonly List<SmvPerson> people;
        readonly PersonTraits[] traits;
        readonly TraitSampler sampler;
        readonly int seed, y0, y1, n;
        readonly double ppl;

        // ---------------------------------------------------------------- outputs

        public PersonYear[] Store;
        public int[] Offset;
        public int[] FirstYearOf, YearCount;
        public PopulationYear[] Years;
        public YearCalibration[] Calibration;

        /// <summary>Per person: bequests received from parents (2025 dollars).</summary>
        public readonly float[] InheritedReal;

        /// <summary>Cooperation of a strategy (row) against another (column) under the games' noise and continuation.</summary>
        public readonly float[,] Coop;

        /// <summary>Estates that found no heir ($B, nominal, summed over the years).</summary>
        public double UnclaimedEstates;

        /// <summary>
        /// The money check of each year ($B, nominal): the net worth of everyone alive at the year's end, and the flows
        /// that may move it: saving, holding gains (stock prices, home prices, businesses growing into their value),
        /// businesses closed below that value, consumer debt discharged, balance sheets brought into the record
        /// (1946's households, immigrants) and estates leaving it (no heir: lost; a negative estate: forgiven). Anything
        /// else that changed net worth is a leak (<see cref="MoneyResidual"/>).
        /// </summary>
        public double[] MoneyNetWorth, MoneySaving, MoneyGains, MoneyClosures, MoneyDischarged, MoneyArrivals, MoneyEstates;

        /// <summary>Per year: the change of net worth that the flows do not explain ($B; 0 up to rounding).</summary>
        public double MoneyResidual(int k) => k <= 0 || MoneyNetWorth == null ? 0
            : MoneyNetWorth[k] - MoneyNetWorth[k - 1] - (MoneySaving[k] + MoneyGains[k] - MoneyClosures[k] + MoneyDischarged[k] +
                                                       MoneyArrivals[k] + MoneyEstates[k]);

        // ---------------------------------------------------------------- per person, fixed (from the traits)

        readonly double[] zRank;

        /// <summary>The saving shift (rate units) and the six categories' log tilts at age 45, and their change per
        /// decade of age, from the traits (reason, which changes year by year, is added when used).</summary>
        readonly double[] savingTrait, savingTraitSlope, categoryTrait, categoryTraitSlope;

        readonly float[] betaAt45;
        readonly double[] firstChild;
        readonly int[][] children;

        // ---------------------------------------------------------------- per person, state

        readonly Random[] rng;
        readonly float[] draws;
        readonly bool[] alive, adult, initialized, employed, wasEmployed, selfEmployed, hasChild;
        readonly double[] age, fin, house, mort, debt, biz;
        readonly int[] spouse, prevSpouse, head;
        readonly short[] industry;
        readonly byte[] mortAge;
        readonly float[] aloneZ;
        readonly float[] empLatent, career, ssRel, survivorRel, save1, save2, prevDebtService, prevBuffer, ownReason;

        // per person, this year
        readonly double[] rawEarn, wage, business, capital, ss, medicare, inheritedNow, seScore, reasonLogit, ageShape;

        // per household (indexed by the head), this year
        readonly int[] kids, adults, heads;
        int headCount;
        readonly double[] hhMarket, hhSs, hhMed, hhOther, hhTax, hhYd, hhSaving, hhSpending, hhPct, hhS0, hhInterest;
        readonly double[] hhRaw, hhShare, hhFloor;
        readonly bool[] housing, dependent;
        readonly double[] buyScore, buyPrice, shocks, gainH, dischargedH;
        readonly bool[] defaults;

        // ---------------------------------------------------------------- tables and year scratch

        readonly double[] savingByAge = new double[MaxAge], employmentShape = new double[MaxAge];
        readonly double[] profileMale = new double[MaxAge], profileFemale = new double[MaxAge];
        readonly double[][] desireByAge = new double[5][], fearByAge = new double[5][];
        readonly double[] childlessMale = new double[MaxAge], childlessFemale = new double[MaxAge];
        readonly double[] industryShare, industryStock, industrySteer, categoryTarget = new double[LivesInputs.Spend];
        readonly double[] baseAlign = new double[LivesInputs.Spend];
        readonly List<int> aliveList = new List<int>(), adultList = new List<int>(), headList = new List<int>();
        readonly List<double> shapesMale = new List<double>(), shapesFemale = new List<double>();
        readonly List<int> heirs = new List<int>();
        int[] spouseOf, headOf, peopleCount;

        /// <summary>The people alive in each year, ascending (from <c>yearStart[y - y0]</c>), and those whose last year was
        /// the one before (from <c>deathStart[y - y0]</c>).</summary>
        int[] yearMembers, yearStart, deaths, deathStart;

        int[] sortBuffer = Array.Empty<int>();
        double[] sortKeys = Array.Empty<double>();
        double medicareRatioLast = -1;
        YearCalibration previous;

        // the year's constants for the parallel steps
        int year;
        double yearAwi, medMale, medFemale, primeMale, primeFemale, treasury, dividend, coverage, apr, mortgageRate;
        double equityPrice, homeGrowth;

        public LivesSimulation(LivesInputs inputs, SmvSimulation sim, PersonTraits[] traits, TraitSampler sampler, float[,] coop,
            int seed, int firstYear, int lastYear)
        {
            Coop = coop;
            inp = inputs;
            this.sim = sim;
            people = sim.People;
            this.traits = traits;
            this.sampler = sampler;
            this.seed = seed;
            y0 = firstYear;
            y1 = lastYear;
            n = people.Count;
            ppl = sim.PeoplePerLine;

            rng = new Random[n];
            draws = new float[n * Draws];
            alive = new bool[n];
            adult = new bool[n];
            initialized = new bool[n];
            employed = new bool[n];
            wasEmployed = new bool[n];
            selfEmployed = new bool[n];
            hasChild = new bool[n];
            age = new double[n];
            fin = new double[n];
            house = new double[n];
            mort = new double[n];
            debt = new double[n];
            biz = new double[n];
            spouse = new int[n];
            prevSpouse = new int[n];
            head = new int[n];
            industry = new short[n];
            mortAge = new byte[n];
            empLatent = new float[n];
            aloneZ = new float[n];
            career = new float[n];
            ssRel = new float[n];
            survivorRel = new float[n];
            save1 = new float[n];
            save2 = new float[n];
            prevDebtService = new float[n];
            prevBuffer = new float[n];
            ownReason = new float[n];
            rawEarn = new double[n];
            wage = new double[n];
            business = new double[n];
            capital = new double[n];
            ss = new double[n];
            medicare = new double[n];
            inheritedNow = new double[n];
            seScore = new double[n];
            reasonLogit = new double[n];
            ageShape = new double[n];
            kids = new int[n];
            adults = new int[n];
            heads = new int[n];
            hhMarket = new double[n];
            hhSs = new double[n];
            hhMed = new double[n];
            hhOther = new double[n];
            hhTax = new double[n];
            hhYd = new double[n];
            hhSaving = new double[n];
            hhSpending = new double[n];
            hhPct = new double[n];
            hhS0 = new double[n];
            hhInterest = new double[n];
            hhRaw = new double[n * LivesInputs.Spend];
            hhShare = new double[n * LivesInputs.Spend];
            hhFloor = new double[n];
            housing = new bool[n];
            dependent = new bool[n];
            buyScore = new double[n];
            buyPrice = new double[n];
            shocks = new double[n];
            gainH = new double[n];
            dischargedH = new double[n];
            defaults = new bool[n];
            InheritedReal = new float[n];
            int industries = Math.Max(1, inp.IndustryCount);
            industryShare = new double[industries];
            industryStock = new double[industries];
            industrySteer = new double[industries];

            zRank = new double[n];
            savingTrait = new double[n];
            savingTraitSlope = new double[n];
            categoryTrait = new double[n * LivesInputs.Spend];
            categoryTraitSlope = new double[n * LivesInputs.Spend];
            betaAt45 = new float[n];
            firstChild = new double[n];
            for (int i = 0; i < n; i++)
            {
                firstChild[i] = double.PositiveInfinity;
                spouse[i] = prevSpouse[i] = head[i] = -1;
                industry[i] = -1;
                ssRel[i] = -1;
                save1[i] = save2[i] = float.NaN;
                rng[i] = new Random(LivesMath.SeedOf(seed, i, 1));
                aloneZ[i] = (float)LivesMath.Normal(new Random(LivesMath.SeedOf(seed, i, 4)));
                PrepareTraits(i);
            }

            // children of each person and the time each first became a parent
            List<int>[] kidLists = new List<int>[n];
            for (int c = 0; c < n; c++)
            {
                SmvPerson p = people[c];
                AddChild(kidLists, p.Mother, c, p.Birth);
                if (p.Father != p.Mother) AddChild(kidLists, p.Father, c, p.Birth);
            }

            children = new int[n][];
            for (int i = 0; i < n; i++) children[i] = kidLists[i]?.ToArray() ?? Array.Empty<int>();

            Tabulate();
        }

        void AddChild(List<int>[] lists, int parent, int child, double birth)
        {
            if (parent < 0 || parent >= n) return;
            (lists[parent] ??= new List<int>()).Add(child);
            firstChild[parent] = Math.Min(firstChild[parent], birth);
        }

        /// <summary>
        /// The psyche.json tilts and saving shifts of a person, per unit of the variables they are given for (Big Five
        /// z, present bias in steps of -0.1 of beta, social comparison z, loss aversion z on its log scale), at age 45
        /// and per decade of age (age slopes run from 20 to 70); reason is added year by year.
        /// </summary>
        [MethodImpl(LivesMath.Hot)]
        void PrepareTraits(int i)
        {
            PersonTraits t = traits[i];
            zRank[i] = LivesMath.InvPhi(t.Rank);
            double meanBeta = inp.TraitDist[LivesInputs.TBeta].Mean > 0 ? inp.TraitDist[LivesInputs.TBeta].Mean : 0.92;
            double lambdaSlope = inp.TraitDist[LivesInputs.TLambda].AgeSlope;
            double logSd = inp.TraitDist[LivesInputs.TLambda].LogSd > 0 ? inp.TraitDist[LivesInputs.TLambda].LogSd : 0.45;
            double[] v = new double[8], dv = new double[8];
            v[0] = t.C;
            v[1] = t.N;
            v[2] = t.A;
            v[3] = t.O;
            v[4] = t.E;
            v[5] = (meanBeta - t.Beta) / 0.1;
            v[6] = t.Comparison;
            v[7] = sampler.LambdaZ(t.Lambda);
            dv[0] = inp.TraitDist[LivesInputs.TC].AgeSlope;
            dv[1] = inp.TraitDist[LivesInputs.TN].AgeSlope;
            dv[2] = inp.TraitDist[LivesInputs.TA].AgeSlope;
            dv[3] = inp.TraitDist[LivesInputs.TO].AgeSlope;
            dv[4] = inp.TraitDist[LivesInputs.TE].AgeSlope;
            dv[5] = -inp.TraitDist[LivesInputs.TBeta].AgeSlope / 0.1;
            dv[6] = inp.TraitDist[LivesInputs.TComparison].AgeSlope;
            dv[7] = lambdaSlope / Math.Max(0.1, t.Lambda) / logSd;   // d log(lambda) / d decade, in sd of log lambda
            double s = 0, ds = 0;
            for (int j = 0; j < 8; j++)
            {
                s += inp.SavingPp[j] * v[j] / 100;
                ds += inp.SavingPp[j] * dv[j] / 100;
            }

            savingTrait[i] = s;
            savingTraitSlope[i] = ds;
            for (int c = 0; c < LivesInputs.Spend; c++)
            {
                double ct = 0, dct = 0;
                for (int j = 0; j < 8; j++)
                {
                    ct += inp.Tilts[c][j] * v[j];
                    dct += inp.Tilts[c][j] * dv[j];
                }

                categoryTrait[i * LivesInputs.Spend + c] = ct;
                categoryTraitSlope[i * LivesInputs.Spend + c] = dct;
            }

            betaAt45[i] = t.Beta;
        }

        /// <summary>Curves of age, tabulated once (whole years; looked up with linear interpolation).</summary>
        [MethodImpl(LivesMath.Hot)]
        void Tabulate()
        {
            LifeStageTiltsAccess lifeStage = new LifeStageTiltsAccess(inp);
            for (int a = 0; a < MaxAge; a++)
            {
                savingByAge[a] = lifeStage.At(a) / 100;
                employmentShape[a] = Table(EmploymentByAge, a);
                profileMale[a] = LivesInputs.Rate(inp.ProfileMale, a, 0.8);
                profileFemale[a] = LivesInputs.Rate(inp.ProfileFemale, a, 0.8);
                double zm = (a - 41) / 7.0, zf = (a - 35) / 5.0;   // psyche.json fears[childless]: men 41 +- 7, women 35 +- 5
                childlessMale[a] = Math.Exp(-0.5 * zm * zm);
                childlessFemale[a] = Math.Exp(-0.5 * zf * zf);
            }

            for (int d = 0; d < 5; d++)
            {
                desireByAge[d] = new double[MaxAge];
                fearByAge[d] = new double[MaxAge];
                for (int a = 0; a < MaxAge; a++)
                {
                    desireByAge[d][a] = inp.DesireStage[d].At(a);
                    fearByAge[d][a] = inp.FearStage[d].At(a);
                }
            }

            // the population's motive alignment of each category (baseline drive weights, no life stage)
            for (int c = 0; c < LivesInputs.Spend; c++) baseAlign[c] = Align(inp, c, inp.DesireWeight, inp.FearWeight);
        }

        /// <summary>Reads psyche.json lifeStageTilts once (its lookup parses the age bands on every call).</summary>
        readonly struct LifeStageTiltsAccess
        {
            readonly Why.Economy.Data.LifeStageTilts tilts;

            public LifeStageTiltsAccess(LivesInputs inp) => tilts = inp.Data.Psyche?.LifeStage;

            public double At(double a) => tilts?.SavingAt(a) ?? 0;
        }

        [MethodImpl(LivesMath.Hot)]
        static double AtAge(double[] table, double a)
        {
            if (a <= 0) return table[0];
            if (a >= MaxAge - 1) return table[MaxAge - 1];
            int k = (int)a;
            double f = a - k;
            return table[k] + (table[k + 1] - table[k]) * f;
        }

        /// <summary>Calendar time at which a year is evaluated: its middle, or just before now for the running year.</summary>
        double TimeOf(int y) => Math.Min(y + 0.5, sim.NowYear - 1e-3);

        // ================================================================ the run

        /// <summary>Time spent in each phase of the year loop (stopwatch ticks): households, individuals, incomes,
        /// spending, the settling of minds, balance sheets and records; and in the outputs built after it.</summary>
        public readonly long[] PhaseTicks = new long[6];

        public static readonly string[] PhaseNames = { "households", "individuals", "incomes", "spending", "settle", "finish" };

        public void Run()
        {
            Layout();
            int years = y1 - y0 + 1;
            Years = new PopulationYear[years];
            Calibration = new YearCalibration[years];
            MoneyNetWorth = new double[years];
            MoneySaving = new double[years];
            MoneyGains = new double[years];
            MoneyClosures = new double[years];
            MoneyDischarged = new double[years];
            MoneyArrivals = new double[years];
            MoneyEstates = new double[years];
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            for (int y = y0; y <= y1; y++)
            {
                YearCalibration cal = new YearCalibration();
                Calibration[y - y0] = cal;
                year = y;
                double t = TimeOf(y);
                long t0 = sw.ElapsedTicks;
                Households(y, t);
                long t1 = sw.ElapsedTicks;
                Individuals(y, cal);
                long t2 = sw.ElapsedTicks;
                Incomes(y, cal);
                long t3 = sw.ElapsedTicks;
                SpendingAndSaving(y, cal);
                long t4 = sw.ElapsedTicks;
                Settle(y, cal);
                long t5 = sw.ElapsedTicks;
                PhaseTicks[0] += t1 - t0;
                PhaseTicks[1] += t2 - t1;
                PhaseTicks[2] += t3 - t2;
                PhaseTicks[3] += t4 - t3;
                PhaseTicks[4] += t5 - t4;
                previous = cal;
            }

            long f0 = sw.ElapsedTicks;
            Finish();
            PhaseTicks[5] += sw.ElapsedTicks - f0;
        }

        /// <summary>Which years each person is alive in (mid-year), and their slots in the store.</summary>
        [MethodImpl(LivesMath.Hot)]
        void Layout()
        {
            Offset = new int[n];
            FirstYearOf = new int[n];
            YearCount = new int[n];
            int total = 0;
            for (int i = 0; i < n; i++)
            {
                SmvPerson p = people[i];
                int first = -1, count = 0;
                for (int y = y0; y <= y1; y++)
                {
                    double t = TimeOf(y);
                    if (p.Enter > t || t >= p.Death) continue;
                    if (first < 0) first = y;
                    count = y - first + 1;
                }

                Offset[i] = total;
                FirstYearOf[i] = first;
                YearCount[i] = first < 0 ? 0 : count;
                total += YearCount[i];
            }

            Store = new PersonYear[total];
            spouseOf = new int[total];
            headOf = new int[total];
            peopleCount = new int[y1 - y0 + 1];

            // who is alive in each year (ascending index), and who died since the year before
            int years = y1 - y0 + 1;
            yearStart = new int[years + 1];
            deathStart = new int[years + 1];
            for (int i = 0; i < n; i++)
            {
                if (YearCount[i] == 0) continue;
                for (int y = FirstYearOf[i]; y < FirstYearOf[i] + YearCount[i]; y++) yearStart[y - y0 + 1]++;
                int gone = FirstYearOf[i] + YearCount[i];
                if (gone <= y1) deathStart[gone - y0 + 1]++;
            }

            for (int k = 0; k < years; k++)
            {
                yearStart[k + 1] += yearStart[k];
                deathStart[k + 1] += deathStart[k];
            }

            yearMembers = new int[yearStart[years]];
            deaths = new int[deathStart[years]];
            int[] fillY = new int[years], fillD = new int[years];
            for (int i = 0; i < n; i++)
            {
                if (YearCount[i] == 0) continue;
                for (int y = FirstYearOf[i]; y < FirstYearOf[i] + YearCount[i]; y++)
                {
                    yearMembers[yearStart[y - y0] + fillY[y - y0]++] = i;
                }

                int gone = FirstYearOf[i] + YearCount[i];
                if (gone <= y1) deaths[deathStart[gone - y0] + fillD[gone - y0]++] = i;
            }
        }

        // ================================================================ 0. households

        [MethodImpl(LivesMath.Hot)]
        void Households(int y, double t)
        {
            // last year's people: their spouses become the previous ones, and they leave the year's flags
            foreach (int i in aliveList)
            {
                prevSpouse[i] = spouse[i];
                spouse[i] = -1;
                alive[i] = false;
                adult[i] = false;
            }

            aliveList.Clear();
            adultList.Clear();
            for (int k = yearStart[y - y0]; k < yearStart[y - y0 + 1]; k++)
            {
                int i = yearMembers[k];
                SmvPerson p = people[i];
                alive[i] = true;
                age[i] = t - p.Birth;
                adult[i] = age[i] >= SmvModel.AdultAge;
                inheritedNow[i] = 0;
                aliveList.Add(i);
                if (adult[i]) adultList.Add(i);
                if (firstChild[i] <= t) hasChild[i] = true;
            }

            // marriages of this year (an end at +infinity whose partners died is clamped to the deaths)
            foreach (SmvMarriage m in sim.MarriageLog)
            {
                if (m.Wife < 0 || m.Husband < 0 || m.Wife >= n || m.Husband >= n) continue;
                if (m.Start > t || !adult[m.Wife] || !adult[m.Husband]) continue;
                double end = Math.Min(m.End, Math.Min(people[m.Wife].Death, people[m.Husband].Death));
                if (t >= end) continue;
                spouse[m.Wife] = m.Husband;
                spouse[m.Husband] = m.Wife;
            }

            // deaths since last year: estates to the spouse, else the living children
            for (int k = deathStart[y - y0]; k < deathStart[y - y0 + 1]; k++) Bequeath(deaths[k], y);

            // separations: the home is sold and each keeps their half
            foreach (int i in adultList)
            {
                int ex = prevSpouse[i];
                if (ex >= 0 && ex != spouse[i] && alive[ex]) SellHome(i);
            }

            // heads: the wife heads a couple; children belong to their mother's household, else their father's
            foreach (int i in aliveList)
            {
                SmvPerson p = people[i];
                if (adult[i])
                {
                    head[i] = spouse[i] >= 0 && p.Male ? spouse[i] : i;
                    continue;
                }

                head[i] = p.Mother >= 0 && p.Mother < n && alive[p.Mother] ? head[p.Mother]
                    : p.Father >= 0 && p.Father < n && alive[p.Father] ? head[p.Father] : -1;
            }

            headCount = 0;
            headList.Clear();
            foreach (int i in adultList)
            {
                kids[i] = 0;
                if (head[i] != i) continue;
                heads[headCount++] = i;
                headList.Add(i);
                adults[i] = spouse[i] >= 0 ? 2 : 1;
            }

            foreach (int i in aliveList)
            {
                if (!adult[i] && head[i] >= 0 && adult[head[i]]) kids[head[i]]++;
            }

            OwnHouseholds(y);

            // first appearance as an adult: the balance sheet the person arrives with, and the household's home
            for (int k = 0; k < headCount; k++)
            {
                int h = heads[k], sp = spouse[h];
                if (initialized[h] && (sp < 0 || initialized[sp])) continue;
                double before = NetWorth(h) + (sp >= 0 ? NetWorth(sp) : 0);
                bool brought = !initialized[h] && Arrive(h, y);
                if (sp >= 0 && !initialized[sp]) brought |= Arrive(sp, y);
                if (brought) ArriveHome(h, y);
                MoneyArrivals[y - y0] += (NetWorth(h) + (sp >= 0 ? NetWorth(sp) : 0) - before) * ppl / 1e9;
            }

            // industries: the year's employment shares, and a steer toward them for new hires
            if (inp.IndustryCount > 0)
            {
                inp.IndustryShares(y, industryShare);
                Array.Clear(industryStock, 0, inp.IndustryCount);
                double stock = 0;
                foreach (int i in adultList)
                {
                    if (!employed[i] || industry[i] < 0) continue;
                    industryStock[industry[i]]++;
                    stock++;
                }

                for (int k = 0; k < inp.IndustryCount; k++)
                {
                    double have = stock > 0 ? industryStock[k] / stock : industryShare[k];
                    industrySteer[k] = industryShare[k] <= 0 ? 0
                        : LivesMath.Clamp(Math.Pow(industryShare[k] / Math.Max(have, 1e-4), 2), 0.25, 4);
                }
            }
        }

        /// <summary>
        /// Which households have a home of their own (the unit of homeownership): couples, single parents and single
        /// homeowners always; of the other singles from <see cref="OwnHouseholdAge"/>, as many as make the year's number
        /// of households (history.json households), those with the strongest pull to live alone first (age, means and
        /// a lasting personal draw); the rest share a home (a partner, roommates, family). Without this every single
        /// adult would be a household: ~30% more households than the census counts, and as many too many homes.
        /// Singles under 25 without children live with family (dependents: no dissaving or borrowing).
        /// </summary>
        void OwnHouseholds(int y)
        {
            int housed = 0, candidates = 0;
            EnsureSort(headCount);
            for (int k = 0; k < headCount; k++)
            {
                int h = heads[k];
                bool single = spouse[h] < 0 && kids[h] == 0;
                bool owner = house[h] > 0;
                dependent[h] = single && !owner && age[h] < OwnHouseholdAge;
                housing[h] = !single || owner;
                if (housing[h])
                {
                    housed++;
                }
                else if (!dependent[h])
                {
                    sortBuffer[candidates] = h;
                    double pull = aloneZ[h] + AloneAge * (Math.Min(age[h], AloneAgeTop) - 45) + AloneRank * zRank[h];
                    sortKeys[candidates++] = -pull + h * 1e-9;
                }
            }

            double households = inp.Households.IsEmpty ? 0 : inp.Households.At(y) * 1e6 / ppl;
            int want = households > 0 ? Math.Min(candidates, Math.Max(0, (int)Math.Round(households) - housed)) : candidates;
            if (want < candidates) Array.Sort(sortKeys, sortBuffer, 0, candidates);
            for (int k = 0; k < want; k++) housing[sortBuffer[k]] = true;
        }

        void EnsureSort(int count)
        {
            if (sortBuffer.Length >= count) return;
            sortBuffer = new int[count * 2];
            sortKeys = new double[count * 2];
        }

        /// <summary>
        /// A person's first year as an adult: someone already adult when the record starts (1946) or arriving as an
        /// adult immigrant brings a balance sheet (SCF 2022 median net worth by age, moved to the year with the wage
        /// index and the lower wealth-to-income ratio of the era, spread by rank; immigrants bring a quarter) and, for
        /// those adult in 1946, a career record for Social Security. Those growing up in the model start with what they
        /// inherited.
        /// </summary>
        bool Arrive(int i, int y)
        {
            initialized[i] = true;
            SmvPerson p = people[i];
            Random r = new Random(LivesMath.SeedOf(seed, i, 2));
            empLatent[i] = (float)LivesMath.InvPhi(r.NextDouble());
            double a = age[i];
            bool brought = a >= SmvModel.AdultAge &&
                           (y == y0 || (p.Immigrant && p.Enter - p.Birth >= SmvModel.AdultAge));
            if (!brought) return false;

            double zr = zRank[i];
            double awi = inp.WageIndex(y), awi22 = inp.WageIndex(2022);
            double median = MedianEarnings(p.Male, y);
            double employ = p.Male ? 0.85 : 0.4;
            // the record of covered earnings: a career before 1946 for those already adult then; none for immigrants
            // (Social Security counts US earnings only, so those who arrive late get little or none of it)
            double working = y == y0 ? LivesMath.Clamp(a - 22, 0, 40) : 0;
            career[i] = (float)(working * 0.85 * (median > 0 && awi > 0 ? median / awi : 1) * Math.Exp(EarningsSigma * zr) * employ);

            // net worth: the SCF median at this age (2022 dollars), the era's level, the person's place; all financial
            // until ArriveHome decides on a home
            double nw2022 = inp.NetWorthByAge.IsEmpty ? 150_000 : inp.NetWorthByAge.At(a);
            double era = y < 1970 ? InitialWealthEra : y < 1990 ? 0.7 : 0.85;
            double zw = 0.6 * zr + 0.8 * LivesMath.InvPhi(r.NextDouble());
            double share = spouse[i] >= 0 ? 0.5 : 0.7;
            double nw = nw2022 * (awi22 > 0 ? awi / awi22 : 1) * era * share *
                        Math.Max(0, Math.Exp(InitialWealthSigma * zw) - InitialWealthOffset);
            if (y > y0) nw *= ImmigrantWealth;
            fin[i] += Math.Max(0, nw);
            return true;
        }

        /// <summary>
        /// The home of a household that arrived with a balance sheet: owned with the era's rate by the head's age
        /// (homeownershipByAge x homeownership of the year / of 2025; couples more, singles and immigrants less), at a
        /// price for the household's income, with a mortgage paid down with age; the equity comes out of financial
        /// assets.
        /// </summary>
        void ArriveHome(int h, int y)
        {
            int sp = spouse[h];
            Random r = new Random(LivesMath.SeedOf(seed, h, 3));
            double a = sp >= 0 ? Math.Max(age[h], age[sp]) : age[h];
            double ownRate = LivesInputs.Rate(inp.HomeownershipByAge, a, 60) / 100 *
                             LivesInputs.Rate(inp.Homeownership, y, 64) / Math.Max(1, LivesInputs.Rate(inp.Homeownership, 2025, 65));
            ownRate *= sp >= 0 ? 1.15 : 0.7;
            if (y > y0) ownRate *= ImmigrantOwnership;
            if (!housing[h] || r.NextDouble() >= ownRate) return;

            // a household that already has a home keeps it (an arriving spouse moves in)
            if (house[h] > 0 || (sp >= 0 && house[sp] > 0)) return;

            double income = Math.Exp(0.4 * zRank[h]) * MedianEarnings(people[h].Male, y) * (sp >= 0 ? 1.6 : 1.0);
            double price = HomePrice(y, income);
            double loan = price * LivesMath.Clamp(0.75 - 0.025 * (a - 30), 0, 0.75);
            double assets = fin[h] + (sp >= 0 ? fin[sp] : 0);
            double f;
            if (y > y0)
            {
                // immigrants pay the equity from what they bring (keeping 2% of the price) and borrow the rest; with
                // less than the minimum down payment they rent
                double equity = Math.Min(price - loan, assets - 0.02 * price);
                if (equity < MinDownPayment * price) return;
                loan = price - equity;
                f = assets - equity;
            }
            else
            {
                // the record's opening balance sheets (1946): the home comes with the household's place
                f = Math.Max(0.02 * price, assets - (price - loan));
            }

            Split(h, sp, f, price, loan, debt[h] + (sp >= 0 ? debt[sp] : 0), biz[h] + (sp >= 0 ? biz[sp] : 0),
                (byte)LivesMath.Clamp(a - 30, 0, MortgageTerm));
        }

        /// <summary>The era's median earnings at the peak of the age profile ($ per year).</summary>
        double MedianEarnings(bool male, int y)
        {
            double m = male ? inp.Dollars(inp.EarnMale, y) / ProfileMedianMale : inp.Dollars(inp.EarnFemale, y) / ProfileMedianFemale;
            return m > 0 ? m : (male ? 0.9 : 0.65) * inp.WageIndex(y) / (male ? ProfileMedianMale : ProfileMedianFemale);
        }

        /// <summary>A home's price for a household income: the era's median new home, more for higher incomes.</summary>
        [MethodImpl(LivesMath.Hot)]
        double HomePrice(int y, double income)
        {
            double medianPrice = inp.Dollars(inp.MedianHomePrice, y);
            double medianIncome = inp.Dollars(inp.MedianHouseholdIncome, y);
            if (medianPrice <= 0) medianPrice = 4 * Math.Max(medianIncome, 1);
            double rel = medianIncome > 0 ? Math.Max(income, 1) / medianIncome : 1;
            // existing homes sell ~15% below new ones; price rises less than income (elasticity ~0.7, judgment)
            return 0.85 * medianPrice * LivesMath.Clamp(Math.Pow(rel, 0.7), 0.35, 6);
        }

        /// <summary>
        /// A death: the estate goes to the surviving spouse; else equally to the living children; else, in the order of
        /// intestacy, to the living grandchildren, the living parents or the living siblings; else to nobody in the
        /// record (charity, relatives outside it: counted in <see cref="UnclaimedEstates"/>). A negative estate dies
        /// with its owner (the creditors' loss).
        /// </summary>
        void Bequeath(int i, int y)
        {
            int sp = prevSpouse[i];
            double estate = fin[i] + biz[i] + house[i] - mort[i] - debt[i];
            if (sp >= 0 && alive[sp])
            {
                fin[sp] += fin[i] + biz[i];
                house[sp] += house[i];
                mort[sp] += mort[i];
                debt[sp] += debt[i];
                mortAge[sp] = Math.Max(mortAge[sp], mortAge[i]);
                double own = ssRel[i] >= 0 ? ssRel[i] : PrimaryBenefit(career[i], 66);   // a survivor gets the larger benefit
                survivorRel[sp] = (float)Math.Max(survivorRel[sp], Math.Max(own, survivorRel[i]));
                inheritedNow[sp] += Math.Max(0, estate);
            }
            else if (estate > 0)
            {
                heirs.Clear();
                foreach (int c in children[i]) AddHeir(c);
                bool fromParent = heirs.Count > 0;
                if (heirs.Count == 0)
                {
                    foreach (int c in children[i])
                    {
                        foreach (int g in children[c]) AddHeir(g);
                    }
                }

                SmvPerson p = people[i];
                if (heirs.Count == 0)
                {
                    AddHeir(p.Mother);
                    AddHeir(p.Father);
                }

                if (heirs.Count == 0)
                {
                    if (p.Mother >= 0 && p.Mother < n)
                    {
                        foreach (int c in children[p.Mother]) AddHeir(c);
                    }

                    if (p.Father >= 0 && p.Father < n)
                    {
                        foreach (int c in children[p.Father]) AddHeir(c);
                    }
                }

                if (heirs.Count > 0)
                {
                    double part = estate / heirs.Count, real = part * inp.Prices(2025, y);
                    foreach (int c in heirs)
                    {
                        fin[c] += part;
                        inheritedNow[c] += part;
                        if (fromParent) InheritedReal[c] += (float)real;
                    }
                }
                else
                {
                    UnclaimedEstates += estate * ppl / 1e9;
                    MoneyEstates[y - y0] -= estate * ppl / 1e9;
                }
            }
            else
            {
                MoneyEstates[y - y0] -= estate * ppl / 1e9;   // debts that die with their owner
            }

            fin[i] = house[i] = mort[i] = debt[i] = biz[i] = 0;

            void AddHeir(int c)
            {
                if (c >= 0 && c < n && c != i && alive[c] && !heirs.Contains(c)) heirs.Add(c);
            }
        }

        double NetWorth(int i) => fin[i] + house[i] - mort[i] - debt[i] + biz[i];

        void SellHome(int i)
        {
            double equity = house[i] - mort[i];
            if (equity >= 0) fin[i] += equity;
            else debt[i] -= equity;
            house[i] = mort[i] = 0;
            mortAge[i] = 0;
        }

        // ================================================================ 1. individuals

        [MethodImpl(LivesMath.Hot)]
        void Individuals(int y, YearCalibration cal)
        {
            // the year's constants
            primeMale = LivesInputs.Rate(inp.EmpPrimeMale, y, 88) / 100;
            primeFemale = LivesInputs.Rate(inp.EmpPrimeFemale, y, 70) / 100;
            EmploymentScales(y, cal);
            yearAwi = inp.WageIndex(y);
            medMale = MedianEarnings(true, y);
            medFemale = MedianEarnings(false, y);
            treasury = LivesInputs.Rate(inp.Treasury10, y, 4) / 100;
            dividend = inp.DividendYield(y);
            coverage = inp.SocialSecurityCoverage(y);
            Parallel.For(0, adultList.Count, k => Individual(adultList[k], cal));

            // earnings: the median earner of each sex earns the year's median. The rank's lognormal is centered on the
            // whole cohort, but employment favors the higher ranks (most where few women worked), so the earners'
            // median and the female/male ratio would otherwise run high (1950: women 0.66 of men instead of 0.52)
            cal.EarningsMale = MedianScale(true, y);
            cal.EarningsFemale = MedianScale(false, y);

            // reason: the adults' mean is the population's share of decisions by the higher OS every year
            // (psyche.json os.higher.share; Newton steps on a common shift of the logits, from last year's)
            double shift = previous?.ReasonShift ?? 0;
            cal.ReasonShift = shift = adultList.Count > 0
                ? LogisticOffset(adultList, reasonLogit, inp.HigherOsShare * adultList.Count, shift)
                : shift;
            foreach (int i in adultList) ownReason[i] = (float)LivesMath.Sigmoid(reasonLogit[i] + shift);

            // self-employment: an offset on the propensities so the employed share matches the era's rate
            double seRate = LivesInputs.Rate(inp.SelfEmployment, y, 10) / 100;
            int working = 0;
            foreach (int i in adultList)
            {
                if (employed[i]) working++;
                else seScore[i] = double.NegativeInfinity;
            }

            cal.SelfEmploymentOffset = working > 0
                ? LogisticOffset(adultList, seScore, seRate * working, previous?.SelfEmploymentOffset ?? 0)
                : 0;
            foreach (int i in adultList)
            {
                selfEmployed[i] = employed[i] && draws[i * Draws + 5] < LivesMath.Sigmoid(cal.SelfEmploymentOffset + seScore[i]);
            }

            // a business nobody in the household runs any more closes or is sold: it fetches a share of its going-concern
            // value (a couple holds it half each, so it ends only when neither spouse runs it)
            foreach (int i in adultList)
            {
                int sp = spouse[i];
                if (selfEmployed[i] || (sp >= 0 && selfEmployed[sp]) || biz[i] <= 0) continue;
                fin[i] += BusinessExit * biz[i];
                MoneyClosures[y - y0] += (1 - BusinessExit) * biz[i] * ppl / 1e9;
                biz[i] = 0;
            }
        }

        /// <summary>One adult's year before the household pools: draws, job, industry, earnings, capital income, Social
        /// Security claim, and the reason logit (before the year's centering).</summary>
        [MethodImpl(LivesMath.Hot)]
        void Individual(int i, YearCalibration cal)
        {
            SmvPerson p = people[i];
            PersonTraits tr = traits[i];
            int o = i * Draws;
            Random r = rng[i];
            for (int d = 0; d < Draws; d++) draws[o + d] = (float)r.NextDouble();
            double a = age[i], zr = zRank[i];

            // employment: the persistent latent against the era's rate for the person's sex and age
            double prime = p.Male ? primeMale : primeFemale;
            double target = ageShape[i] < 0 ? prime
                : prime * ageShape[i] * (p.Male ? cal.EmploymentScaleMale : cal.EmploymentScaleFemale);
            const double rankScale = 0.93674969975975964;   // sqrt(1 - EmploymentRankLoading^2)
            const double persist = 0.43588989435406739;     // sqrt(1 - EmploymentPersistence^2)
            empLatent[i] = (float)(EmploymentPersistence * empLatent[i] + persist * LivesMath.InvPhi(draws[o]));
            double score = rankScale * empLatent[i] - EmploymentRankLoading * zr;
            wasEmployed[i] = employed[i];
            employed[i] = LivesMath.Phi(score) < Math.Min(0.98, target);

            // industry: kept unless changing jobs
            if (employed[i] && (industry[i] < 0 || !wasEmployed[i] || draws[o + 1] < IndustrySwitchRate))
            {
                industry[i] = ChooseIndustry(zr, draws[o + 2]);
            }

            // earnings (before the year's scale)
            if (employed[i])
            {
                double profile = AtAge(p.Male ? profileMale : profileFemale, a);
                double place = tr.Rank > TailRank
                    ? EarningsSigma * 2.3263478740408408 + Math.Log((1 - TailRank) / (1 - tr.Rank)) / TailAlpha
                    : EarningsSigma * zr;
                rawEarn[i] = (p.Male ? medMale : medFemale) * profile * Math.Exp(place + EarningsNoise * LivesMath.InvPhi(draws[o + 3]));
                seScore[i] = tr.Enterprise + SelfEmploymentRank * zr + (selfEmployed[i] ? SelfEmploymentPersistence : 0) +
                             0.5 * LivesMath.InvPhi(draws[o + 4]);
            }
            else
            {
                rawEarn[i] = 0;
                seScore[i] = double.NegativeInfinity;
            }

            // capital income on financial assets (interest and dividends) and homes (net rent), before the scale
            double q = EquityShare(fin[i], yearAwi, tr.Lambda);
            capital[i] = fin[i] * ((1 - q) * treasury + q * dividend) + house[i] * RentYield;

            // Social Security: claimed once, at the claim age, from the career's indexed earnings
            double claimAge = Math.Max(tr.ClaimAge, year < 1956 || (year < 1961 && p.Male) ? 65 : 62);
            if (a >= claimAge && ssRel[i] < 0 && sampler.CoverageDraw[i] < coverage)
            {
                ssRel[i] = (float)PrimaryBenefit(career[i], claimAge);
            }

            // reason (logit before the year's centering): propensity + maturity - stress (last year's debts and buffer,
            // this year's job)
            bool jobless = !employed[i] && a < 62 && a >= 22;
            double stress = StressDebt * prevDebtService[i] + (jobless ? StressJobless : 0) + StressBuffer * prevBuffer[i];
            reasonLogit[i] = sampler.ReasonLogit[i] + sampler.ReasonOffset + TraitSampler.Maturity(a) - stress;
        }

        /// <summary>
        /// Scales the raw earnings of one sex so the median of those with earnings (employees and the self-employed)
        /// is the year's median of all workers with earnings (history.json earningsMedianMale / earningsMedianFemale);
        /// the year's totals are scaled to the national accounts afterwards, which keeps the ratio of the medians.
        /// Returns the factor (1 when the series or the earners are missing).
        /// </summary>
        double MedianScale(bool male, int y)
        {
            double want = inp.Dollars(male ? inp.EarnMale : inp.EarnFemale, y);
            EnsureSort(adultList.Count);
            int count = 0;
            foreach (int i in adultList)
            {
                if (people[i].Male == male && rawEarn[i] > 0) sortKeys[count++] = rawEarn[i];
            }

            if (want <= 0 || count == 0) return 1;
            Array.Sort(sortKeys, 0, count);
            double median = count % 2 == 1 ? sortKeys[count / 2] : 0.5 * (sortKeys[count / 2 - 1] + sortKeys[count / 2]);
            if (median <= 0) return 1;
            double scale = want / median;
            foreach (int i in adultList)
            {
                if (people[i].Male == male) rawEarn[i] *= scale;
            }

            return scale;
        }

        /// <summary>
        /// The common offset a of logistic chances sigmoid(a + score) whose expected count over the list is
        /// <paramref name="target"/> (Newton steps from a start; scores of -infinity never count).
        /// </summary>
        [MethodImpl(LivesMath.Hot)]
        double LogisticOffset(List<int> list, double[] score, double target, double start)
        {
            double a = start;
            int count = list.Count;
            for (int iter = 0; iter < 30; iter++)
            {
                double total = 0, d = 0;
                for (int k = 0; k < count; k++)
                {
                    double sc = score[list[k]];
                    if (double.IsNegativeInfinity(sc)) continue;
                    double v = 1.0 / (1.0 + Math.Exp(-(a + sc)));
                    total += v;
                    d += v * (1 - v);
                }

                double err = target - total;
                if (Math.Abs(err) < 1e-2) break;
                double step = d > 1e-9 ? err / d : Math.Sign(err) * 2;
                a += step < -2 ? -2 : step > 2 ? 2 : step;
            }

            return a;
        }

        /// <summary>
        /// Each adult's employment shape by age (relative to prime age; -1 inside 25-54) and the scale of the non-prime
        /// ages per sex so the population's rate matches LFPR x (1 - unemployment) of the year (Newton steps on the
        /// piecewise-linear expected count, from last year's scale).
        /// </summary>
        [MethodImpl(LivesMath.Hot)]
        void EmploymentScales(int y, YearCalibration cal)
        {
            double unemployment = LivesInputs.Rate(inp.Unemployment, y, 5) / 100;
            shapesMale.Clear();
            shapesFemale.Clear();
            int men = 0, women = 0;
            foreach (int i in adultList)
            {
                double a = age[i];
                bool male = people[i].Male;
                if (male) men++;
                else women++;
                ageShape[i] = a >= 25 && a < 55 ? -1 : AtAge(employmentShape, a);
                if (ageShape[i] >= 0) (male ? shapesMale : shapesFemale).Add(ageShape[i]);
            }

            cal.EmploymentScaleMale = Scale(shapesMale, men, primeMale,
                LivesInputs.Rate(inp.LfprMale, y, 75) / 100 * (1 - unemployment), previous?.EmploymentScaleMale ?? 1);
            cal.EmploymentScaleFemale = Scale(shapesFemale, women, primeFemale,
                LivesInputs.Rate(inp.LfprFemale, y, 50) / 100 * (1 - unemployment), previous?.EmploymentScaleFemale ?? 1);
        }

        [MethodImpl(LivesMath.Hot)]
        static double Scale(List<double> shapes, int count, double prime, double target, double start)
        {
            if (count == 0 || shapes.Count == 0) return start;
            double primeCount = (count - shapes.Count) * Math.Min(0.98, prime), want = target * count, m = start;
            for (int iter = 0; iter < 12; iter++)
            {
                double sum = primeCount, slope = 0;
                foreach (double sh in shapes)
                {
                    double v = prime * sh * m;
                    if (v < 0.98)
                    {
                        sum += v;
                        slope += prime * sh;
                    }
                    else
                    {
                        sum += 0.98;
                    }
                }

                double err = want - sum;
                if (Math.Abs(err) < 0.05 || slope <= 0) break;
                m = LivesMath.Clamp(m + err / slope, 0.05, 10);
            }

            return m;
        }

        static double Table(double[][] rows, double x)
        {
            if (x <= rows[0][0]) return rows[0][1];
            for (int k = 1; k < rows.Length; k++)
            {
                if (x <= rows[k][0])
                {
                    double f = (x - rows[k - 1][0]) / (rows[k][0] - rows[k - 1][0]);
                    return rows[k - 1][1] + (rows[k][1] - rows[k - 1][1]) * f;
                }
            }

            return rows[rows.Length - 1][1];
        }

        [MethodImpl(LivesMath.Hot)]
        short ChooseIndustry(double zr, double u)
        {
            int count = inp.IndustryCount;
            if (count == 0) return -1;
            double sum = 0;
            for (int k = 0; k < count; k++) sum += IndustryWeight(k, zr);
            double pick = u * sum;
            for (int k = 0; k < count; k++)
            {
                pick -= IndustryWeight(k, zr);
                if (pick <= 0) return (short)k;
            }

            return (short)(count - 1);
        }

        double IndustryWeight(int k, double zr) =>
            industryShare[k] * industrySteer[k] * Math.Exp(IndustryPaySteer * inp.IndustryPayTilt[k] * zr);

        /// <summary>
        /// Share of financial assets held in stocks: rises with wealth (SCF: the top holds most stocks directly and
        /// through funds), falls with loss aversion.
        /// </summary>
        [MethodImpl(LivesMath.Hot)]
        static double EquityShare(double assets, double awi, double lambda)
        {
            double rich = Math.Log(1 + Math.Max(0, assets) / Math.Max(awi, 1));
            return LivesMath.Clamp(EquityBase + EquityPerLog * rich - EquityLossAversion * (lambda - 2), EquityMin, EquityMax);
        }

        /// <summary>
        /// Social Security's primary benefit in units of the wage index: average indexed earnings over 35 years,
        /// through the progressive formula (90% / 32% / 15% above bend points ~0.21 and ~1.3 of the wage index),
        /// reduced for early and raised for late claims around 66.
        /// </summary>
        static double PrimaryBenefit(double careerSum, double claimAge)
        {
            double aime = LivesMath.Clamp(careerSum / 35, 0, 2.4);
            double pia = 0.9 * Math.Min(aime, 0.21) + 0.32 * LivesMath.Clamp(aime - 0.21, 0, 1.09) + 0.15 * Math.Max(0, aime - 1.3);
            double adjust = claimAge < 66 ? 1 - 0.0667 * (66 - claimAge) : 1 + 0.08 * (claimAge - 66);
            return pia * adjust;
        }

        // ================================================================ 2. incomes, transfers, taxes

        [MethodImpl(LivesMath.Hot)]
        void Incomes(int y, YearCalibration cal)
        {
            double unit = ppl / 1e9;   // $ per person -> $B for the population
            double awi = yearAwi;

            // scale wages, business income and capital income to the year's totals
            double sumWage = 0, sumBiz = 0, sumCap = 0;
            foreach (int i in adultList)
            {
                if (selfEmployed[i]) sumBiz += rawEarn[i];
                else sumWage += rawEarn[i];
                sumCap += capital[i];
            }

            cal.Wages = Scale(inp.CompensationTarget(y), sumWage * unit, 1.2);
            cal.Business = Scale(inp.ProprietorsTarget(y), sumBiz * unit, 1.0);
            cal.Capital = Scale(inp.CapitalTarget(y), sumCap * unit, 1.0);
            int old = 0;
            foreach (int i in adultList)
            {
                wage[i] = selfEmployed[i] ? 0 : rawEarn[i] * cal.Wages;
                business[i] = selfEmployed[i] ? rawEarn[i] * cal.Business : 0;
                capital[i] *= cal.Capital;
                double a = age[i];
                if (a >= 22 && a < 62) career[i] += (float)Math.Min(2.4, (wage[i] + business[i]) / Math.Max(awi, 1));
                if (a >= 65) old++;
            }

            // Social Security: own, spouse's half, survivor's; scaled so beneficiaries average the year's benefit
            double sumSs = 0;
            int beneficiaries = 0;
            foreach (int i in adultList)
            {
                ss[i] = 0;
                if (ssRel[i] < 0) continue;
                double rel = Math.Max(ssRel[i], survivorRel[i]);
                int sp = spouse[i];
                if (sp >= 0 && ssRel[sp] >= 0) rel = Math.Max(rel, 0.5 * ssRel[sp]);
                ss[i] = rel * awi;
                if (ss[i] <= 0) continue;
                sumSs += ss[i];
                beneficiaries++;
            }

            double average = inp.SocialSecurityAverage(y);
            cal.SocialSecurity = beneficiaries > 0 && sumSs > 0 && average > 0 ? average * beneficiaries / sumSs : 1;
            double ssTotal = 0;
            foreach (int i in adultList)
            {
                ss[i] *= cal.SocialSecurity;
                ssTotal += ss[i];
            }

            // Medicare per person 65+ (the data's total in the circuit's year, its ratio to the wage index after)
            double perOld = inp.MedicareRatio(y) * awi;
            if (y == inp.CircuitYear && inp.CircuitMedicare > 0 && old > 0)
            {
                perOld = inp.CircuitMedicare / unit / old;
                medicareRatioLast = perOld / Math.Max(awi, 1);
            }
            else if (y > inp.CircuitYear && medicareRatioLast > 0)
            {
                perOld = medicareRatioLast * awi;
            }

            double medTotal = 0;
            foreach (int i in adultList)
            {
                medicare[i] = age[i] >= 65 ? perOld : 0;
                medTotal += medicare[i];
            }

            // households' market income and their income percentile
            for (int k = 0; k < headCount; k++)
            {
                int h = heads[k], sp = spouse[h];
                hhMarket[h] = wage[h] + business[h] + capital[h] + (sp >= 0 ? wage[sp] + business[sp] + capital[sp] : 0);
                hhSs[h] = ss[h] + (sp >= 0 ? ss[sp] : 0);
                hhMed[h] = medicare[h] + (sp >= 0 ? medicare[sp] : 0);
            }

            Percentiles();

            // other transfers (Medicaid, SNAP, EITC, SSI, unemployment, veterans): CE's transfer share of market income by
            // percentile, with a floor at half the poverty line per person for those with little market income; retirees
            // on Social Security get less of it. Scaled to the year's total beyond Social Security and Medicare.
            double poverty = inp.Dollars(inp.PovertyLine, y);
            if (poverty <= 0) poverty = 0.3 * awi;
            double sumW = 0;
            for (int k = 0; k < headCount; k++)
            {
                int h = heads[k];
                double share = LivesInputs.Rate(inp.TransferShare, hhPct[h], 20) / 100;
                double floor = 0.5 * poverty / 4 * (adults[h] + 0.6 * kids[h]);
                double w = share * Math.Max(hhMarket[h], floor) * (hhSs[h] > 0 ? 0.3 : 1);
                hhOther[h] = w;
                sumW += w;
            }

            double transfers = inp.TransfersTarget(y);
            cal.OtherTransfersTarget = transfers - (ssTotal + medTotal) * unit;
            cal.OtherTransfers = Scale(Math.Max(0, cal.OtherTransfersTarget), sumW * unit, 0);

            // taxes (income, payroll; the effective rate by percentile, scaled to the year's total), disposable income
            double sumTax = 0;
            for (int k = 0; k < headCount; k++)
            {
                int h = heads[k];
                hhOther[h] *= cal.OtherTransfers;
                double rate = LivesInputs.Rate(inp.TaxRate, hhPct[h], 20) / 100;
                hhTax[h] = rate * (hhMarket[h] + 0.5 * hhSs[h]);
                sumTax += hhTax[h];
            }

            cal.Taxes = Scale(inp.TaxesTarget(y), sumTax * unit, 1);
            for (int k = 0; k < headCount; k++)
            {
                int h = heads[k];
                double income = hhMarket[h] + hhSs[h] + hhMed[h] + hhOther[h];
                hhTax[h] = Math.Min(hhTax[h] * cal.Taxes, 0.9 * income);
                hhYd[h] = income - hhTax[h];
            }
        }

        /// <summary>target / current, or the fallback when either is missing.</summary>
        static double Scale(double target, double current, double fallback) =>
            target > 0 && current > 0 ? target / current : fallback;

        /// <summary>Each household's income percentile (0..100, mid-rank; ties broken by index).</summary>
        [MethodImpl(LivesMath.Hot)]
        void Percentiles()
        {
            EnsureSort(headCount);
            for (int k = 0; k < headCount; k++)
            {
                int h = heads[k];
                sortBuffer[k] = h;
                sortKeys[k] = hhMarket[h] + hhSs[h] + h * 1e-9;
            }

            Array.Sort(sortKeys, sortBuffer, 0, headCount);
            for (int k = 0; k < headCount; k++) hhPct[sortBuffer[k]] = 100.0 * (k + 0.5) / headCount;
        }

        // ================================================================ 3. saving and spending

        [MethodImpl(LivesMath.Hot)]
        void SpendingAndSaving(int y, YearCalibration cal)
        {
            apr = LivesInputs.Rate(inp.CardApr, y, 18) / 100;
            Parallel.For(0, headCount, () => new Scratch(), (k, state, scratch) =>
            {
                HouseholdTilts(heads[k], scratch);
                return scratch;
            }, scratch => { });

            // jeopardy realized: a large uninsured bill strikes some households. It is an outlay within jeopardy (NIPA
            // counts the care, the lawyers and the repairs it buys in PCE), paid from savings or borrowed beyond the
            // household's limit, and the year's saving calibration includes it: the money reaches the industries
            // instead of vanishing from the household sector (as a capital loss it took ~3% of DPI a year, more than
            // half of personal saving, and drained net worth)
            for (int k = 0; k < headCount; k++)
            {
                int h = heads[k], o = h * Draws;
                shocks[h] = draws[o + 8] < ShockRate ? (ShockMin + (ShockMax - ShockMin) * draws[o + 9]) * Math.Max(hhYd[h], 1) : 0;
            }

            // the year's saving rate: one shift for everyone (Newton steps on the piecewise-linear total, from last
            // year's shift; borrowing limits and the rate's bounds flatten it)
            double target = LivesInputs.Rate(inp.SavingRate, y, 7) / 100;
            cal.SavingRateTarget = target;
            double sumYd = 0;
            for (int k = 0; k < headCount; k++) sumYd += hhYd[heads[k]];
            double shift = previous?.SavingShift ?? 0;
            for (int iter = 0; iter < 20 && sumYd > 0; iter++)
            {
                double total = 0, d = 0;
                for (int k = 0; k < headCount; k++)
                {
                    int h = heads[k];
                    double yd = hhYd[h], rate = hhS0[h] + shift;
                    bool bound = rate <= SavingRateMin || rate >= SavingRateMax;
                    double saving = yd * (rate < SavingRateMin ? SavingRateMin : rate > SavingRateMax ? SavingRateMax : rate);
                    if (saving < hhFloor[h])
                    {
                        saving = hhFloor[h];
                        bound = true;
                    }

                    total += saving - shocks[h];
                    if (!bound) d += yd;
                }

                double err = target * sumYd - total;
                if (Math.Abs(err) < 1e-4 * sumYd) break;
                shift += d > 0.05 * sumYd ? err / d : err / sumYd;
            }

            cal.SavingShift = shift;
            for (int k = 0; k < headCount; k++)
            {
                int h = heads[k];
                double rate = LivesMath.Clamp(hhS0[h] + shift, SavingRateMin, SavingRateMax);
                hhSaving[h] = Math.Max(hhYd[h] * rate, hhFloor[h]) - shocks[h];
                hhSpending[h] = Math.Max(0, hhYd[h] - hhSaving[h]);
            }

            // spending shares: income's base x tilts, then one factor per category for the year's data (iterative
            // proportional fitting from last year's factors); a shock's bill is jeopardy on top of the household's mix
            Parallel.For(0, headCount, () => new Scratch(), (k, state, scratch) =>
            {
                CategoryTilts(heads[k], scratch);
                return scratch;
            }, scratch => { });

            inp.CategoryTargets(y, categoryTarget);
            double[] factor = cal.Categories, mean = new double[LivesInputs.Spend];
            for (int c = 0; c < LivesInputs.Spend; c++) factor[c] = previous?.Categories[c] ?? 1;
            for (int iter = 0; iter < 12; iter++)
            {
                Array.Clear(mean, 0, LivesInputs.Spend);
                double total = 0, fixedJeopardy = 0;
                for (int k = 0; k < headCount; k++)
                {
                    int h = heads[k];
                    double spend = hhSpending[h];
                    if (spend <= 0) continue;
                    int o = h * LivesInputs.Spend;
                    double sum = 0;
                    for (int j = 0; j < LivesInputs.Spend; j++) sum += factor[j] * hhRaw[o + j];
                    if (sum <= 0) continue;
                    double regular = Math.Max(0, spend - shocks[h]), w = regular / sum;
                    for (int j = 0; j < LivesInputs.Spend; j++) mean[j] += w * factor[j] * hhRaw[o + j];
                    mean[JeopardyIndex] += spend - regular;
                    fixedJeopardy += spend - regular;
                    total += spend;
                }

                if (total <= 0) break;
                double err = 0;
                fixedJeopardy /= total;
                for (int j = 0; j < LivesInputs.Spend; j++)
                {
                    mean[j] /= total;
                    if (mean[j] <= 0 || categoryTarget[j] <= 0) continue;
                    err = Math.Max(err, Math.Abs(mean[j] - categoryTarget[j]));
                    double fix = j == JeopardyIndex ? fixedJeopardy : 0;
                    factor[j] *= mean[j] - fix > 1e-9 && categoryTarget[j] > fix
                        ? (categoryTarget[j] - fix) / (mean[j] - fix)
                        : categoryTarget[j] / mean[j];
                }

                if (err < 2e-4) break;
            }

            for (int k = 0; k < headCount; k++)
            {
                int h = heads[k], o = h * LivesInputs.Spend;
                double sum = 0, spend = hhSpending[h];
                for (int j = 0; j < LivesInputs.Spend; j++) sum += factor[j] * hhRaw[o + j];
                if (sum <= 0 || spend <= 0)
                {
                    for (int j = 0; j < LivesInputs.Spend; j++) hhShare[o + j] = 1.0 / LivesInputs.Spend;
                    continue;
                }

                double regular = Math.Max(0, spend - shocks[h]);
                for (int j = 0; j < LivesInputs.Spend; j++) hhShare[o + j] = regular * factor[j] * hhRaw[o + j] / sum / spend;
                hhShare[o + JeopardyIndex] += (spend - regular) / spend;
            }
        }

        /// <summary>
        /// A household's own saving rate before the year's shift: the base of its income percentile, its adults' traits
        /// (psyche.json savingRatePp, reason per sd of the adult distribution), life stage (lifeStageTilts) and children;
        /// and the interest its consumer debt costs this year.
        /// </summary>
        [MethodImpl(LivesMath.Hot)]
        void HouseholdTilts(int h, Scratch scratch)
        {
            int sp = spouse[h];
            int members = sp >= 0 ? 2 : 1;
            double traitsShift = 0, ageSum = 0;
            for (int m = 0; m < members; m++)
            {
                int i = m == 0 ? h : sp;
                double dec = (LivesMath.Clamp(age[i], 20, 70) - 45) / 10;
                double reasonZ = (ownReason[i] - inp.HigherOsShare) / sampler.ReasonSd;
                traitsShift += savingTrait[i] + savingTraitSlope[i] * dec + inp.SavingPp[8] * reasonZ / 100;
                ageSum += age[i];
            }

            double a = ageSum / members;
            double s = BaseSaving(hhPct[h]) + traitsShift / members + AtAge(savingByAge, a) + ChildSaving * Math.Min(4, kids[h]);
            hhS0[h] = s;

            // the least it can save: young singles living with family neither dissave nor borrow; others can draw their
            // financial assets down and borrow up to their limit
            double assets = fin[h] + (sp >= 0 ? fin[sp] : 0);
            double owed = debt[h] + (sp >= 0 ? debt[sp] : 0);
            double equity = Math.Max(0, house[h] + (sp >= 0 ? house[sp] : 0) - mort[h] - (sp >= 0 ? mort[sp] : 0));
            double room = Math.Max(0, DebtLimitIncome * Math.Max(0, hhYd[h]) + DebtLimitEquity * equity - owed);
            hhFloor[h] = dependent[h] ? 0 : -(assets + room);

            // last year's consumer debt costs interest this year (paid within jeopardy)
            hhInterest[h] = owed * apr;
        }

        /// <summary>
        /// A household's spending shares before the year's calibration: its income's base shares x exp(trait tilts +
        /// drive pull + children + age + debt interest).
        /// </summary>
        [MethodImpl(LivesMath.Hot)]
        void CategoryTilts(int h, Scratch scratch)
        {
            double[] baseShares = scratch.Shares;
            inp.BaseShares(hhPct[h], baseShares);
            HouseholdDrives(h, scratch);
            int sp = spouse[h];
            int members = sp >= 0 ? 2 : 1;
            double a = sp >= 0 ? 0.5 * (age[h] + age[sp]) : age[h];
            int ch = Math.Min(4, kids[h]);
            double[] traitTilt = scratch.Tilt;
            Array.Clear(traitTilt, 0, LivesInputs.Spend);
            for (int m = 0; m < members; m++)
            {
                int i = m == 0 ? h : sp;
                double dec = (LivesMath.Clamp(age[i], 20, 70) - 45) / 10;
                double reasonZ = (ownReason[i] - inp.HigherOsShare) / sampler.ReasonSd;
                int o = i * LivesInputs.Spend;
                for (int c = 0; c < LivesInputs.Spend; c++)
                {
                    traitTilt[c] += (categoryTrait[o + c] + categoryTraitSlope[o + c] * dec + inp.Tilts[c][8] * reasonZ) / members;
                }
            }

            double spend = Math.Max(hhSpending[h], 1);
            for (int c = 0; c < LivesInputs.Spend; c++)
            {
                double tilt = traitTilt[c];
                tilt += DriveTiltGain * Math.Log(Math.Max(1e-6, Align(inp, c, scratch.Desire, scratch.Fear)) / Math.Max(1e-6, baseAlign[c]));
                tilt += ChildTilt[c] * ch;
                if (c == 2)
                {
                    tilt += LivesMath.Clamp(HealthAgeTilt * (a - 45), -0.25, 0.4);
                    tilt += Math.Log(1 + hhInterest[h] / (0.15 * spend));
                }

                if (c == 4) tilt += StudentTilt * Math.Max(0, 30 - a);
                hhRaw[h * LivesInputs.Spend + c] = baseShares[c] * Math.Exp(LivesMath.Clamp(tilt, -3, 3));
            }
        }

        /// <summary>
        /// Base saving rate of an income percentile: savingRateByPercentile (2024, quintile midpoints), rising above
        /// the 90th percentile to the top 1%'s rate at the 99.5th (log distance to the top).
        /// </summary>
        [MethodImpl(LivesMath.Hot)]
        double BaseSaving(double pct)
        {
            double s = LivesInputs.Rate(inp.SavingByPercentile, pct, 5) / 100;
            if (pct <= 90 || inp.SavingByPercentile.IsEmpty) return s;
            double top = inp.SavingByPercentile.At(90) / 100;
            double f = LivesMath.Clamp01(Math.Log(10 / Math.Max(0.5, 100 - pct)) / Math.Log(20));
            return top + (TopSavingRate - top) * f;
        }

        /// <summary>A category's alignment with a set of drive weights: its desire mix x (1 - fear share) + fear mix x fear share.</summary>
        [MethodImpl(LivesMath.Hot)]
        static double Align(LivesInputs inp, int c, double[] desire, double[] fear)
        {
            double fs = inp.FearShare[c], a = 0;
            double[] cd = inp.CategoryDesire[c], cf = inp.CategoryFear[c];
            for (int d = 0; d < 5; d++) a += desire[d] * cd[d] * (1 - fs);
            for (int f = 0; f < 5; f++) a += fear[f] * cf[f] * fs;
            return a;
        }

        /// <summary>The household's drives into the scratch's Desire and Fear (mean of its adults, each pole normalized).</summary>
        [MethodImpl(LivesMath.Hot)]
        void HouseholdDrives(int h, Scratch scratch)
        {
            double[] desire = scratch.Desire, fear = scratch.Fear, d = scratch.D, f = scratch.F;
            Array.Clear(desire, 0, 5);
            Array.Clear(fear, 0, 5);
            int sp = spouse[h];
            int members = sp >= 0 ? 2 : 1;
            for (int m = 0; m < members; m++)
            {
                int i = m == 0 ? h : sp;
                DrivesAt(i, scratch, d, f);
                for (int k = 0; k < 5; k++)
                {
                    desire[k] += d[k] / members;
                    fear[k] += f[k] / members;
                }
            }
        }

        /// <summary>A person's drives this year, from the tabulated life stages (the scratch holds the stages).</summary>
        [MethodImpl(LivesMath.Hot)]
        void DrivesAt(int i, Scratch s, double[] desire, double[] fear)
        {
            double a = age[i];
            for (int k = 0; k < 5; k++)
            {
                s.StageD[k] = AtAge(desireByAge[k], a);
                s.StageF[k] = AtAge(fearByAge[k], a);
            }

            Drives(traits[i], s.StageD, s.StageF, AtAge(people[i].Male ? childlessMale : childlessFemale, a), spouse[i] >= 0,
                hasChild[i], prevBuffer[i], desire, fear);
        }

        /// <summary>
        /// A person's drive weights at an age and state (each pole normalized to 1): the individual weights x the
        /// drives' life-stage curves; mate seeking (sex) eases in marriage, fear of isolation rises for singles, fear of
        /// staying childless acts only on those without children in their fertile window (women peak 35, sd 5; men 41,
        /// sd 7; psyche.json childless), fear of ruin and exposure rise with a thin buffer.
        /// </summary>
        public static void Drives(LivesInputs inp, PersonTraits t, bool male, double a, bool married, bool parent,
            double lowBuffer, double[] desire, double[] fear)
        {
            double[] stageD = new double[5], stageF = new double[5];
            for (int k = 0; k < 5; k++)
            {
                stageD[k] = inp.DesireStage[k].At(a);
                stageF[k] = inp.FearStage[k].At(a);
            }

            double zc = male ? (a - 41) / 7.0 : (a - 35) / 5.0;
            Drives(t, stageD, stageF, Math.Exp(-0.5 * zc * zc), married, parent, lowBuffer, desire, fear);
        }

        [MethodImpl(LivesMath.Hot)]
        static void Drives(PersonTraits t, double[] stageD, double[] stageF, double childless, bool married, bool parent,
            double lowBuffer, double[] desire, double[] fear)
        {
            double sum = 0;
            for (int k = 0; k < 5; k++)
            {
                double w = t.Desires[k] * stageD[k];
                if (k == 4 && married) w *= 0.7;
                desire[k] = w;
                sum += w;
            }

            for (int k = 0; k < 5; k++) desire[k] = sum > 0 ? desire[k] / sum : 0.2;
            sum = 0;
            for (int k = 0; k < 5; k++)
            {
                double w = t.Fears[k] * (k == 4 ? (parent ? 0 : childless) : stageF[k]);
                if (k == 1) w *= married ? 0.75 : 1.25;
                if (k == 0 || k == 3) w *= 1 + lowBuffer;
                fear[k] = w;
                sum += w;
            }

            for (int k = 0; k < 5; k++) fear[k] = sum > 0 ? fear[k] / sum : 0.2;
        }

        // ================================================================ 4. minds

        /// <summary>
        /// The household's motives (fear and fantasy shares of its spending) and each adult's reason, future
        /// orientation and agency (psyche.json agency.thresholdsNote: autonomy, saving, debt, reason and fantasy
        /// components, soft-scored and weighted).
        /// </summary>
        [MethodImpl(LivesMath.Hot)]
        void Mind(int h)
        {
            int sp = spouse[h];
            int o = h * LivesInputs.Spend;
            double fearShare = 0, fantasy = 0;
            for (int c = 0; c < LivesInputs.Spend; c++)
            {
                fearShare += hhShare[o + c] * inp.FearShare[c];
                fantasy += hhShare[o + c] * inp.Fantasy[c];
            }

            double yd = Math.Max(hhYd[h], 1), spend = Math.Max(hhSpending[h], 1);
            double mortgage = mort[h] + (sp >= 0 ? mort[sp] : 0), owed = debt[h] + (sp >= 0 ? debt[sp] : 0);
            double left = Math.Max(1, MortgageTerm - mortAge[h]);
            double payment = mortgage > 0
                ? mortgageRate > 1e-6 ? mortgage * mortgageRate / (1 - Math.Pow(1 + mortgageRate, -left)) : mortgage / left
                : 0;
            double service = (payment + owed * (apr + CardPrincipal)) / yd;
            double assets = fin[h] + (sp >= 0 ? fin[sp] : 0);
            double rate = hhYd[h] > 0 ? hhSaving[h] / hhYd[h] : 0;
            double meanRate = MeanSavingRate(h, rate);
            bool owner = selfEmployed[h] || (sp >= 0 && selfEmployed[sp]);
            double autonomy = owner ? 1 : Math.Min(1, assets / spend / inp.RunwayYears);
            double sScore = LivesMath.Clamp01(meanRate / inp.SavingTarget);
            double dScore = LivesMath.Clamp01((inp.DebtZero - service) / (inp.DebtZero - inp.DebtOk));
            double fScore = LivesMath.Clamp01((inp.FantasyZero - fantasy) / (inp.FantasyZero - inp.FantasyOk));
            double buffer = LivesMath.Clamp01(1 - assets / spend * 4);   // under three months of spending

            int members = sp >= 0 ? 2 : 1;
            for (int m = 0; m < members; m++)
            {
                int i = m == 0 ? h : sp;
                double r = ownReason[i];
                double rScore = LivesMath.Clamp01((r - inp.ReasonZero) / (inp.ReasonOk - inp.ReasonZero));
                double agency = inp.WAutonomy * autonomy + inp.WSaving * sScore + inp.WDebt * dScore +
                                inp.WReason * rScore + inp.WFantasy * fScore;
                double dec = (LivesMath.Clamp(age[i], 20, 70) - 45) / 10;
                double beta = betaAt45[i] + inp.TraitDist[LivesInputs.TBeta].AgeSlope * dec;
                double future = 0.6 * LivesMath.Clamp01((beta - 0.5) / 0.55) + 0.4 * LivesMath.Clamp01((meanRate + 0.1) / 0.4);

                ref PersonYear rec = ref Store[Offset[i] + (year - FirstYearOf[i])];
                rec = default;
                rec.FearShare = (float)fearShare;
                rec.Fantasy = (float)fantasy;
                rec.Reason = (float)r;
                rec.Future = (float)future;
                rec.Agency = (float)agency;
                rec.Autonomy = (float)autonomy;
                rec.DebtService = (float)service;
                prevDebtService[i] = (float)LivesMath.Clamp01(service / inp.DebtZero);
                prevBuffer[i] = (float)buffer;
            }
        }

        /// <summary>The household's saving rate over the agency rule's window (this year and up to two before).</summary>
        [MethodImpl(LivesMath.Hot)]
        double MeanSavingRate(int h, double now)
        {
            int years = (int)Math.Round(inp.SavingYears);
            double sum = now;
            int count = 1;
            if (years >= 2 && !float.IsNaN(save1[h]))
            {
                sum += save1[h];
                count++;
            }

            if (years >= 3 && !float.IsNaN(save2[h]))
            {
                sum += save2[h];
                count++;
            }

            return sum / count;
        }

        // ================================================================ 5. balance sheets

        /// <summary>
        /// The end of a household's year, one parallel pass: its mind (<see cref="Mind"/>), its balance sheet
        /// (<see cref="Balance"/>) and its adults' records; then, in order, the home purchases that keep the era's
        /// homeownership rate (their records are updated), and the year's links for the post pass.
        /// </summary>
        [MethodImpl(LivesMath.Hot)]
        void Settle(int y, YearCalibration cal)
        {
            mortgageRate = LivesInputs.Rate(inp.Mortgage30, y, 6) / 100;
            equityPrice = LivesInputs.Return(inp.EquityReturn, y, 0.1) - inp.DividendYield(y);
            double hp0 = inp.HousePrice.IsEmpty ? 0 : inp.HousePrice.GrowthAt(y - 1);
            double hp1 = inp.HousePrice.IsEmpty ? 0 : inp.HousePrice.GrowthAt(y);
            homeGrowth = y <= inp.HousePrice.FirstYear || hp0 <= 0 ? inp.Prices(y, y - 1) : hp1 / hp0;
            Parallel.For(0, headCount, k =>
            {
                int h = heads[k];
                Mind(h);
                Balance(h);
                RecordHousehold(h);
            });

            // purchases: as many as keep the era's homeownership rate (households of their own)
            int households = 0, owners = 0;
            for (int k = 0; k < headCount; k++)
            {
                int h = heads[k];
                if (!housing[h]) continue;
                households++;
                if (house[h] > 0 || (spouse[h] >= 0 && house[spouse[h]] > 0)) owners++;
            }

            double rate = LivesInputs.Rate(inp.Homeownership, y, 64) / 100;
            int need = (int)Math.Round(rate * households) - owners;
            cal.HomeNeed = need;
            if (need > 0)
            {
                cal.HomeOffset = LogisticOffset(headList, buyScore, need, previous?.HomeOffset ?? 0);
                for (int k = 0; k < headCount; k++)
                {
                    int h = heads[k];
                    if (double.IsNegativeInfinity(buyScore[h])) continue;
                    if (draws[h * Draws + 7] >= LivesMath.Sigmoid(cal.HomeOffset + buyScore[h])) continue;
                    int sp = spouse[h];
                    double f = fin[h] + (sp >= 0 ? fin[sp] : 0);
                    double price = buyPrice[h];
                    double down = Math.Min(f, DownPayment * price);
                    Split(h, sp, f - down, price, price - down, debt[h] + (sp >= 0 ? debt[sp] : 0),
                        biz[h] + (sp >= 0 ? biz[sp] : 0), 0);
                    RecordBalance(h);
                    if (sp >= 0) RecordBalance(sp);
                }
            }

            // links for the post pass, and children's own records
            foreach (int i in aliveList)
            {
                int slot = Offset[i] + (y - FirstYearOf[i]);
                spouseOf[slot] = adult[i] ? spouse[i] : -1;
                headOf[slot] = adult[i] ? head[i] : head[i] >= 0 && adult[head[i]] ? head[i] : -1;
                if (adult[i]) continue;
                ref PersonYear r = ref Store[slot];
                r.Age = (float)age[i];
                r.Adult = false;
                r.Industry = -1;
                r.Wealth = (float)fin[i];
                r.Inherited = (float)inheritedNow[i];
            }

            peopleCount[y - y0] = aliveList.Count;

            // the money check: everyone's net worth, and the year's flows in household order
            double unit = ppl / 1e9, worth = 0, saving = 0, gains = 0, discharged = 0;
            foreach (int i in aliveList) worth += NetWorth(i);
            for (int k = 0; k < headCount; k++)
            {
                int h = heads[k];
                saving += hhSaving[h];
                gains += gainH[h];
                discharged += dischargedH[h];
            }

            MoneyNetWorth[y - y0] = worth * unit;
            MoneySaving[y - y0] = saving * unit;
            MoneyGains[y - y0] = gains * unit;
            MoneyDischarged[y - y0] = discharged * unit;
        }

        /// <summary>Rewrites the balance-sheet fields of an adult's record (after a purchase).</summary>
        void RecordBalance(int i)
        {
            ref PersonYear r = ref Store[Offset[i] + (year - FirstYearOf[i])];
            r.Homeowner = house[i] > 0;
            r.Wealth = (float)(fin[i] + house[i] - mort[i] - debt[i] + biz[i]);
            r.Debt = (float)(mort[i] + debt[i]);
        }

        /// <summary>
        /// A household's balance sheet through the year: saving pays the scheduled mortgage principal, then consumer
        /// debt, then goes to financial assets (dissaving draws them down, then borrows); stock prices and home prices
        /// move; a business is valued at a multiple of its income; old owners sell; then its chance to buy a home.
        /// </summary>
        [MethodImpl(LivesMath.Hot)]
        void Balance(int h)
        {
            int sp = spouse[h];
            int members = sp >= 0 ? 2 : 1;
            double f = fin[h], hs = house[h], mg = mort[h], db = debt[h], bz = biz[h];
            double bizIncome = business[h];
            bool anySelf = selfEmployed[h];
            byte mAge = mortAge[h];
            double lambda = traits[h].Lambda;
            if (sp >= 0)
            {
                f += fin[sp];
                hs += house[sp];
                mg += mort[sp];
                db += debt[sp];
                bz += biz[sp];
                bizIncome += business[sp];
                anySelf |= selfEmployed[sp];
                mAge = Math.Max(mAge, mortAge[sp]);
                lambda = 0.5 * (lambda + traits[sp].Lambda);
            }

            // the year's saving: scheduled mortgage principal (the principal part of the level payment that Mind counts
            // as debt service: small early in the loan, large late), then consumer debt, then financial assets
            double cash = hhSaving[h];
            if (mg > 0)
            {
                double left = Math.Max(1, MortgageTerm - mAge);
                double principal = mortgageRate > 1e-6 ? mg * mortgageRate / (Math.Pow(1 + mortgageRate, left) - 1) : mg / left;
                principal = Math.Min(mg, principal);
                mg -= principal;
                cash -= principal;
                if (mAge < 255) mAge++;
            }

            if (cash >= 0)
            {
                double repay = Math.Min(db, cash);
                db -= repay;
                cash -= repay;
                f += cash;
            }
            else
            {
                double need = -cash, drawn = Math.Min(f, need);
                f -= drawn;
                db += need - drawn;
            }

            // unpayable consumer debt is discharged (a large uninsured bill was paid from the saving above)
            int o = h * Draws;
            double yd = Math.Max(hhYd[h], 1);
            dischargedH[h] = 0;
            if (db > DefaultLimit * yd && f < db)
            {
                dischargedH[h] = db - DefaultKeep * yd;
                db = DefaultKeep * yd;
                defaults[h] = true;
            }
            else
            {
                defaults[h] = false;
            }

            // returns: the price gains of stocks (dividends and interest were paid as income), home prices
            double q = EquityShare(f / members, yearAwi, lambda);
            double before = f + hs + bz;
            f = Math.Max(0, f * (1 + q * equityPrice));
            hs *= homeGrowth;
            if (mg <= 1e-6)
            {
                mg = 0;
                if (hs <= 0) mAge = 0;
            }

            // a business is worth a multiple of its income while its owner runs it
            if (anySelf) bz += BusinessGrowth * (BusinessMultiple * bizIncome - bz);
            gainH[h] = f + hs + bz - before;

            // elderly owners sell; underwater and unaffordable homes are lost
            double a = sp >= 0 ? Math.Max(age[h], age[sp]) : age[h];
            if (hs > 0 && ((a >= SaleAge && draws[o + 6] < SaleRate) || (hs < mg && db > 0.5 * Math.Max(hhYd[h], 1))))
            {
                double equity = hs - mg;
                if (equity >= 0) f += equity;
                else db -= equity;
                hs = mg = 0;
                mAge = 0;
            }

            // renters with a household of their own may buy (the year's offset is set after every household is done)
            buyScore[h] = double.NegativeInfinity;
            if (housing[h] && hs <= 0)
            {
                double price = HomePrice(year, hhYd[h]);
                buyPrice[h] = price;
                // by age: renters buy most at 25-34 (HVS 2025 ownership by age, 37 / 62 / 70 / 75 / 79%, is matched
                // within ~4 points with these weights; judgment)
                double ageFit = a < 25 ? -1.0 : a < 35 ? 0.6 : a < 45 ? -0.2 : a < 55 ? -0.7 : a < 65 ? -1.1 : -1.8;
                double afford = f >= DownPayment * price ? 1.2 : f >= MinDownPayment * price ? 0 : -1.5;
                buyScore[h] = ageFit + afford + 1.5 * (hhPct[h] / 100 - 0.5) + (sp >= 0 ? 0.6 : 0) + 0.3 * Math.Min(2, kids[h]);
            }

            Split(h, sp, f, hs, mg, db, bz, mAge);
        }

        /// <summary>Writes a household's balance sheet back to its adults, half each for a couple.</summary>
        [MethodImpl(LivesMath.Hot)]
        void Split(int h, int sp, double f, double hs, double mg, double db, double bz, byte mAge)
        {
            double share = sp >= 0 ? 0.5 : 1;
            fin[h] = f * share;
            house[h] = hs * share;
            mort[h] = mg * share;
            debt[h] = db * share;
            biz[h] = bz * share;
            mortAge[h] = mAge;
            if (sp < 0) return;
            fin[sp] = fin[h];
            house[sp] = house[h];
            mort[sp] = mort[h];
            debt[sp] = debt[h];
            biz[sp] = biz[h];
            mortAge[sp] = mAge;
        }

        // ================================================================ 6. records and aggregates

        /// <summary>Adults' records of a household: money is the household's split evenly; the mind was written in Mind.</summary>
        [MethodImpl(LivesMath.Hot)]
        void RecordHousehold(int h)
        {
            int sp = spouse[h];
            int members = sp >= 0 ? 2 : 1;
            double share = 1.0 / members;
            double rate = hhYd[h] > 0 ? hhSaving[h] / hhYd[h] : 0;
            int o = h * LivesInputs.Spend;
            for (int m = 0; m < members; m++)
            {
                int i = m == 0 ? h : sp;
                ref PersonYear r = ref Store[Offset[i] + (year - FirstYearOf[i])];
                r.Age = (float)age[i];
                r.Adult = true;
                r.Married = sp >= 0;
                r.Employed = employed[i];
                r.SelfEmployed = selfEmployed[i];
                r.Homeowner = house[i] > 0;
                r.OwnHousehold = housing[h];
                r.Industry = employed[i] ? industry[i] : (short)-1;
                r.Wages = (float)((wage[h] + (sp >= 0 ? wage[sp] : 0)) * share);
                r.Business = (float)((business[h] + (sp >= 0 ? business[sp] : 0)) * share);
                r.CapitalIncome = (float)((capital[h] + (sp >= 0 ? capital[sp] : 0)) * share);
                r.Transfers = (float)((hhSs[h] + hhMed[h] + hhOther[h]) * share);
                r.Taxes = (float)(hhTax[h] * share);
                r.Disposable = (float)(hhYd[h] * share);
                r.Spending = (float)(hhSpending[h] * share);
                r.Saving = (float)(hhSaving[h] * share);
                r.Earned = (float)(wage[i] + business[i]);
                r.SocialSecurity = (float)ss[i];
                r.Wealth = (float)(fin[i] + house[i] - mort[i] - debt[i] + biz[i]);
                r.Debt = (float)(mort[i] + debt[i]);
                r.ConsumerDebt = (float)debt[i];
                r.Necessities = (float)hhShare[o];
                r.Escapism = (float)hhShare[o + 1];
                r.Jeopardy = (float)hhShare[o + 2];
                r.Status = (float)hhShare[o + 3];
                r.Growth = (float)hhShare[o + 4];
                r.Collective = (float)hhShare[o + 5];
                r.Kids = (byte)Math.Min(255, kids[h]);
                r.IncomeRank = (float)(hhPct[h] / 100);
                r.Inherited = (float)inheritedNow[i];
                r.Loss = (float)(shocks[h] * share);
                r.Discharged = defaults[h];
                save2[i] = save1[i];
                save1[i] = (float)rate;
            }
        }

        // ================================================================ 7. after the loop: outputs of each year

        /// <summary>
        /// What each year shows but nothing later depends on, built after the loop from the records, every year in
        /// parallel (each touches only its own records): tribes, cooperation, wealth groups, children's household
        /// fields, and the population totals.
        /// </summary>
        [MethodImpl(LivesMath.Hot)]
        void Finish()
        {
            // the left-right order of everyone, once (ties by birth order)
            int[] byLean = new int[n];
            float[] leans = new float[n];
            for (int i = 0; i < n; i++)
            {
                byLean[i] = i;
                leans[i] = traits[i].Lean;
            }

            Array.Sort(leans, byLean);
            Parallel.For(0, y1 - y0 + 1, k => Years[k] = FinishYear(y0 + k, byLean));
            spouseOf = headOf = null;
        }

        [MethodImpl(LivesMath.Hot)]
        PopulationYear FinishYear(int y, int[] byLean)
        {
            // the year's adults (birth order) and its strategy mix
            int from = yearStart[y - y0], to = yearStart[y - y0 + 1];
            List<int> adultsNow = new List<int>(to - from);
            double[] mix = new double[7], vsMix = new double[7];
            for (int m = from; m < to; m++)
            {
                int i = yearMembers[m];
                if (!Store[Offset[i] + (y - FirstYearOf[i])].Adult) continue;
                adultsNow.Add(i);
                mix[(int)traits[i].Strategy]++;
            }

            int count = adultsNow.Count;
            for (int s = 0; s < 7; s++) mix[s] /= Math.Max(1, count);
            for (int s = 0; s < 7; s++)
            {
                double c = 0;
                for (int j = 0; j < 7; j++) c += mix[j] * Coop[s, j];
                vsMix[s] = c;
            }

            // tribes: each adult's place among the year's adults on the lean, cut at the year's party shares, so every
            // year's adult shares are the data's (games.json partyIdNote); children by their own lean
            double dem = inp.Dem.At(y), rep = inp.Rep.At(y);
            int rank = 0;
            foreach (int i in byLean)
            {
                int k = y - FirstYearOf[i];
                if (YearCount[i] == 0 || k < 0 || k >= YearCount[i]) continue;
                ref PersonYear r = ref Store[Offset[i] + k];
                if (!r.Adult)
                {
                    r.Tribe = TraitSampler.TribeAt(inp, traits[i].Lean, y);
                    continue;
                }

                double place = (rank++ + 0.5) / Math.Max(1, count);
                r.Tribe = (byte)(place < dem ? 0 : place >= 1 - rep ? 1 : 2);
            }

            // cooperation with the year's partners
            foreach (int i in adultsNow)
            {
                int slot = Offset[i] + (y - FirstYearOf[i]);
                Store[slot].Cooperation = (float)Cooperation(i, spouseOf[slot], Store[slot].Tribe, vsMix);
            }

            // wealth groups: adults ranked by net worth (couples hold half each), and the groups' shares
            double[] keys = new double[count];
            int[] order = new int[count];
            for (int k = 0; k < count; k++)
            {
                int i = adultsNow[k];
                order[k] = i;
                keys[k] = Store[Offset[i] + (y - FirstYearOf[i])].Wealth + i * 1e-9;
            }

            Array.Sort(keys, order);
            PopulationYear a = new PopulationYear { Year = y, AdultLines = count, People = peopleCount[y - y0] };
            double total = 0, bottom = 0, top10 = 0, top1 = 0;
            for (int k = 0; k < count; k++)
            {
                int i = order[k];
                ref PersonYear r = ref Store[Offset[i] + (y - FirstYearOf[i])];
                double place = (k + 0.5) / count;
                r.WealthGroup = (byte)(place >= 0.99 ? 3 : place >= 0.9 ? 2 : place >= 0.5 ? 1 : 0);
                double w = r.Wealth;
                total += w;
                if (place < 0.5) bottom += w;
                if (place >= 0.9) top10 += w;
                if (place >= 0.99) top1 += w;
            }

            if (total > 0)
            {
                a.WealthShares[0] = (float)(bottom / total);
                a.WealthShares[1] = (float)((total - bottom - top10) / total);
                a.WealthShares[2] = (float)((top10 - top1) / total);
                a.WealthShares[3] = (float)(top1 / total);
            }

            // children: their household's spending mind, home, wealth group and income rank
            for (int m = from; m < to; m++)
            {
                int i = yearMembers[m];
                int slot = Offset[i] + (y - FirstYearOf[i]);
                ref PersonYear r = ref Store[slot];
                int h = headOf[slot];
                if (r.Adult || h < 0) continue;
                ref PersonYear hr = ref Store[Offset[h] + (y - FirstYearOf[h])];
                r.Homeowner = hr.Homeowner;
                r.Necessities = hr.Necessities;
                r.Escapism = hr.Escapism;
                r.Jeopardy = hr.Jeopardy;
                r.Status = hr.Status;
                r.Growth = hr.Growth;
                r.Collective = hr.Collective;
                r.FearShare = hr.FearShare;
                r.Fantasy = hr.Fantasy;
                r.WealthGroup = hr.WealthGroup;
                r.Kids = hr.Kids;
                r.IncomeRank = hr.IncomeRank;
            }

            Totals(a, y, adultsNow);
            return a;
        }

        /// <summary>
        /// Share of a person's moves that cooperate this year: against the spouse (if married), a coworker or neighbor
        /// drawn from the year's mix of strategies, and someone of the other tribe, whom partisans distrust (games.json
        /// distrust; independents half of it).
        /// </summary>
        [MethodImpl(LivesMath.Hot)]
        double Cooperation(int i, int sp, byte tribeNow, double[] vsMix)
        {
            int s = (int)traits[i].Strategy;
            double sum = CoopCommunity * vsMix[s], w = CoopCommunity;
            if (sp >= 0)
            {
                sum += CoopSpouse * Coop[s, (int)traits[sp].Strategy];
                w += CoopSpouse;
            }

            double distrust = traits[i].Strategy == PdStrategy.AlwaysCooperate ? 0 : inp.Distrust * (tribeNow == 2 ? 0.5 : 1);
            sum += CoopOutTribe * vsMix[s] * (1 - distrust);
            w += CoopOutTribe;
            return sum / w;
        }

        /// <summary>Totals and means of a year over its adults.</summary>
        [MethodImpl(LivesMath.Hot)]
        void Totals(PopulationYear a, int y, List<int> adultsNow)
        {
            double unit = ppl / 1e9;
            double spendSum = 0, fantasy = 0, fear = 0, reasonSum = 0, future = 0, coop = 0, savers = 0;
            int employedCount = 0, selfCount = 0, autonomous = 0, households = 0, own = 0, owners = 0, shocked = 0, discharged = 0;
            foreach (int i in adultsNow)
            {
                ref PersonYear r = ref Store[Offset[i] + (y - FirstYearOf[i])];
                a.Wages += r.Wages * unit;
                a.Business += r.Business * unit;
                a.CapitalIncome += r.CapitalIncome * unit;
                a.Transfers += r.Transfers * unit;
                a.Taxes += r.Taxes * unit;
                a.Disposable += r.Disposable * unit;
                a.Spending += r.Spending * unit;
                a.Saving += r.Saving * unit;
                a.Wealth += r.Wealth * unit;
                a.Debt += r.Debt * unit;
                a.ConsumerDebt += r.ConsumerDebt * unit;
                double s = r.Spending * unit;
                a.Categories[0] += s * r.Necessities;
                a.Categories[1] += s * r.Escapism;
                a.Categories[2] += s * r.Jeopardy;
                a.Categories[3] += s * r.Status;
                a.Categories[4] += s * r.Growth;
                a.Categories[5] += s * r.Collective;
                a.Categories[LivesInputs.Saving] += r.Saving * unit;
                spendSum += r.Spending;
                fantasy += r.Spending * r.Fantasy;
                fear += r.Spending * r.FearShare;
                reasonSum += r.Reason;
                future += r.Future;
                coop += r.Cooperation;
                if (r.Employed) employedCount++;
                if (r.SelfEmployed) selfCount++;
                if (r.Autonomy >= 0.999f) autonomous++;
                if (r.Disposable > 0 && r.Saving >= 0.1 * r.Disposable) savers++;
                a.Losses += r.Loss * unit;
                a.Strategies[(int)traits[i].Strategy]++;
                a.Parties[Math.Min((int)r.Tribe, 2)]++;

                // households: a couple once (the wife heads it); those of their own count for homeownership
                if (r.Married && people[i].Male) continue;
                households++;
                if (r.Loss > 0) shocked++;
                if (r.Discharged) discharged++;
                if (!r.OwnHousehold) continue;
                own++;
                if (r.Homeowner) owners++;
            }

            int count = Math.Max(1, adultsNow.Count);
            a.Households = households;
            a.MeanFantasy = (float)(spendSum > 0 ? fantasy / spendSum : 0);
            a.MeanFear = (float)(spendSum > 0 ? fear / spendSum : 0);
            a.MeanReason = (float)(reasonSum / count);
            a.MeanFuture = (float)(future / count);
            a.MeanCooperation = (float)(coop / count);
            a.EmploymentRate = employedCount / (float)count;
            a.SelfEmployedShare = employedCount > 0 ? selfCount / (float)employedCount : 0;
            a.AutonomyShare = autonomous / (float)count;
            a.SaverShare = (float)(savers / count);
            a.Homeownership = own > 0 ? owners / (float)own : 0;
            a.LossShare = households > 0 ? shocked / (float)households : 0;
            a.DischargeShare = households > 0 ? discharged / (float)households : 0;
            for (int s = 0; s < 7; s++) a.Strategies[s] /= count;
            for (int s = 0; s < 3; s++) a.Parties[s] /= count;
        }

        // ================================================================ cooperation table

        /// <summary>
        /// Cooperation of each strategy (row) against each other (column) under the games' noise and chance of meeting
        /// again: games of geometric length, <see cref="CoopGames"/> per pair, one fixed stream per pair, the pairs in
        /// parallel (random play cooperates half the time). Independent of the population, so it can run beside pass A.
        /// </summary>
        public static float[,] TabulateCooperation(LivesInputs inp, int seed)
        {
            float[,] coop = new float[7, 7];
            Parallel.For(0, 49, pair => coop[pair / 7, pair % 7] = PairCooperation(inp, seed, pair));
            return coop;
        }

        [MethodImpl(LivesMath.Hot)]
        static float PairCooperation(LivesInputs inp, int seed, int pair)
        {
            int a = pair / 7, b = pair % 7;
            Random r = new Random(LivesMath.SeedOf(seed, pair, 99));
            long moves = 0, cooperative = 0;
            for (int g = 0; g < CoopGames; g++)
            {
                int rounds = PrisonersDilemma.SampleRounds(inp.Continuation, r, 400);
                float c = PrisonersDilemma.CooperationRate((PdStrategy)a, (PdStrategy)b, rounds, inp.Noise, inp.Payoff, r);
                cooperative += (long)Math.Round(c * rounds);
                moves += rounds;
            }

            return moves > 0 ? cooperative / (float)moves : 0.5f;
        }

        /// <summary>Scratch arrays of one worker of a parallel loop (no allocation per household).</summary>
        sealed class Scratch
        {
            public readonly double[] Tilt = new double[LivesInputs.Spend], Shares = new double[LivesInputs.Spend];
            public readonly double[] Desire = new double[5], Fear = new double[5], D = new double[5], F = new double[5];
            public readonly double[] StageD = new double[5], StageF = new double[5];
        }
    }
}
