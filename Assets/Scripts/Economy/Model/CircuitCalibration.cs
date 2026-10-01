using System;
using System.Collections.Generic;
using Why.Economy.Data;

namespace Why.Economy.Model
{
    /// <summary>
    /// Everything the money circuit derives once from the economy files: the calibration year's ratios (how owners'
    /// income splits into profits, proprietors' income, rent and interest; how profits split into taxes, dividends and
    /// retained earnings), the history of each flow as a share of GDP, where each use of money returns to the
    /// industries (spending items' supply chains; the value added that government purchases, investment and exports
    /// buy, from the input-output table), and each wealth group's spending mix. Built in the constructor, read-only
    /// afterwards, so any thread may read it.
    /// </summary>
    sealed class CircuitCalibration
    {
        // ------------------------------------------------------------------ constants

        /// <summary>
        /// Dependence between household income rank and wealth rank: a Gumbel copula with this theta (Kendall tau 0.47),
        /// fitted by the data's authors to the Fed's distributional accounts (circuit.json groupNotes.incomeWealthModel).
        /// It maps the spending survey's income quintiles onto the wealth groups.
        /// </summary>
        public const double CopulaTheta = 1.87;

        /// <summary>The wealth groups' ranges of the wealth distribution (bottom 50%, 50-90%, 90-99%, top 1%).</summary>
        static readonly double[] GroupLow = { 0, 0.5, 0.9, 0.99 }, GroupHigh = { 0.5, 0.9, 0.99, 1 };

        /// <summary>
        /// Share of private investment and of government purchases that buys imports (BEA input-output accounts,
        /// approximate: equipment is about a third imported, structures and software little; the state buys mostly
        /// domestic services). With the spending items' import shares they say which uses buy the economy's imports;
        /// the sum is scaled to the year's imports.
        /// </summary>
        public const double InvestmentImportShare = 0.15, PublicImportShare = 0.03;

        /// <summary>
        /// Net share buybacks start with the SEC's rule 10b-18 (November 1982), which made them safe from manipulation
        /// charges; they ramp up to the calibration year's share of GDP over <see cref="BuybackRampYears"/>.
        /// </summary>
        public const int BuybackStartYear = 1982, BuybackRampYears = 3;

        // ------------------------------------------------------------------ the data

        public readonly EconomyData Data;
        public readonly CircuitFile File;
        public readonly int Year;
        public readonly int IndustryCount;

        /// <summary>Industry index -> tier index (data order: gov 0 .. tech 4).</summary>
        public readonly int[] TierOf;

        /// <summary>Each industry's share of value added in the calibration year (the reference of the time tilt).</summary>
        public readonly double[] ReferenceVaShare;

        // ------------------------------------------------------------------ ratios over time

        /// <summary>Compensation, production taxes and depreciation as shares of GDP (BEA NIPA 1.10).</summary>
        public readonly YearSeries Compensation, ProductionTaxes, Depreciation;

        /// <summary>Owners' share split: corporate profits, proprietors, rental, net interest, other (shares of their sum).</summary>
        public readonly YearSeries[] OwnersSplit = new YearSeries[OwnersParts];

        public const int OwnersParts = 5, Corporate = 0, Proprietors = 1, Rental = 2, NetInterest = 3, Other = 4;

        /// <summary>Corporate profits split: taxes, dividends, retained (shares of their sum).</summary>
        public readonly YearSeries CorporateTaxes, Dividends, Retained;

        /// <summary>Net buybacks as a share of GDP in the calibration year.</summary>
        public readonly double BuybackShare;

        /// <summary>
        /// Personal current taxes, contributions for social insurance, social benefits, disposable income (shares of GDP;
        /// the first three carried back before the files' personal-income detail with BEA's history, see
        /// <see cref="CircuitHistory"/>).
        /// </summary>
        public readonly YearSeries PersonalTaxes, Contributions, Transfers, Disposable;

        /// <summary>First year of the files' personal-income detail (taxes and benefits before it: CircuitHistory).</summary>
        public readonly int PersonalDataYear;

        /// <summary>Saving / disposable income (spending.json history, else personal history).</summary>
        public readonly YearSeries SavingRate;

        /// <summary>Government purchases and federal interest (shares of GDP), the share of equity held abroad.</summary>
        public readonly YearSeries Purchases, PublicInterest, ForeignEquity;

        /// <summary>Exports and imports (shares of GDP), and imports relative to the calibration year.</summary>
        public readonly YearSeries Exports, Imports;

        /// <summary>The spending categories' shares of disposable income over time (spending.json history).</summary>
        public readonly YearSeries[] CategoryHistory;

        // ------------------------------------------------------------------ wealth groups

        public readonly Group[] Groups;
        public readonly YearSeries[] NetWorth, Equity;

        /// <summary>
        /// Spending mix of each group over the six spending categories (calibration cross-section), [group, category].
        /// </summary>
        public readonly double[,] GroupMix;

        // ------------------------------------------------------------------ uses -> industries

        /// <summary>Number of spending categories (the seven categories without saving).</summary>
        public const int SpendingCategories = 6;

        /// <summary>Per spending category: domestic industry weights (sum 1) and the share that buys imports.</summary>
        public readonly double[][] CategoryWeights = new double[SpendingCategories][];

        public readonly double[] CategoryImports = new double[SpendingCategories];

        /// <summary>Value added (by industry, sum 1) that a dollar of government purchases, investment and exports pays for.</summary>
        public readonly double[] PublicWeights, InvestmentWeights, ExportWeights;

        /// <summary>Notes on what the calibration could not use (shown in the circuit's notes).</summary>
        public readonly List<string> Warnings = new List<string>();

        public CircuitCalibration(EconomyData data)
        {
            Data = data;
            File = data.Circuit ?? new CircuitFile();
            Year = File.Year > 0 ? File.Year : 2025;
            IReadOnlyList<Industry> inds = data.Industries;
            IndustryCount = inds.Count;
            TierOf = new int[IndustryCount];
            ReferenceVaShare = new double[IndustryCount];
            double refTotal = data.ValueAddedTotal(Year);
            for (int i = 0; i < IndustryCount; i++)
            {
                TierOf[i] = inds[i].TierIndex;
                ReferenceVaShare[i] = refTotal > 0 ? inds[i].ValueAdded.GrowthAt(Year) / refTotal : 0;
            }

            YearSeries gdp = data.Gdp;
            List<Newtonsoft.Json.Linq.JArray> income = File.IncomeHistory, personal = File.PersonalHistory;
            double calGdp = Value(File.Income, "gdp", gdp.GrowthAt(Year));

            Compensation = CircuitHistory.ShareOfGdp(income, "compensation", gdp, (Year, File.IncomeOf("compensation")));
            ProductionTaxes = CircuitHistory.ShareOfGdp(income, "productionTaxes", gdp, (Year, File.IncomeOf("productionTaxes")));
            Depreciation = CircuitHistory.ShareOfGdp(income, "depreciation", gdp, (Year, File.IncomeOf("depreciation")));

            // owners' share: corporate profits (domestic), proprietors, rental, net interest, and the rest (business
            // transfers + government enterprises' surplus), each year's mix
            string[] owners = { "corporateProfits", "proprietors", "rental", "netInterest" };
            OwnersSplit = Mix(income, owners, true, null);
            (CorporateTaxes, Dividends, Retained) = CorporateMix(income);
            BuybackShare = calGdp > 0 ? File.IncomeOf("buybacks") / calGdp : 0;

            YearSeries taxData = CircuitHistory.ShareOfGdp(personal, "personalTaxes", gdp,
                (Year, File.PersonalOf("personalTaxes")));
            PersonalTaxes = CircuitHistory.Splice(taxData, CircuitHistory.PersonalTaxes);
            Contributions = CircuitHistory.Splice(
                CircuitHistory.ShareOfGdp(personal, "socialInsuranceContributions", gdp,
                    (Year, File.PersonalOf("socialInsuranceContributions"))), CircuitHistory.Contributions);
            Transfers = CircuitHistory.Splice(
                CircuitHistory.ShareOfGdp(personal, "governmentSocialBenefits", gdp,
                    (Year, File.PersonalOf("governmentSocialBenefits"))), CircuitHistory.Transfers);
            Disposable = CircuitHistory.ShareOfGdp(personal, "disposableIncome", gdp, (Year, File.PersonalOf("disposableIncome")));
            PersonalDataYear = taxData.Count > 1 ? (int)taxData.FirstYear : Year;

            int saving = Array.IndexOf(EconomyData.CategoryIds, "saving");
            YearSeries spendingSaving = saving >= 0 && saving < data.CategoryHistory.Count
                ? data.CategoryHistory[saving]
                : YearSeries.Empty;
            SavingRate = !spendingSaving.IsEmpty ? spendingSaving : EconomyData.Keyed(personal, "savingRate");
            if (SavingRate.IsEmpty)
            {
                SavingRate = YearSeries.Constant(0.06);
                Warnings.Add("no saving-rate history: 6% assumed");
            }

            Purchases = CircuitHistory.Splice(
                CircuitHistory.ShareOfGdp(null, "purchases", gdp, (2024, Value(File.Government, "purchases2024", 0)),
                    (Year, File.GovernmentOf("purchases"))), CircuitHistory.Purchases);
            PublicInterest = CircuitHistory.Splice(
                CircuitHistory.ShareOfGdp(null, "interest", gdp, (Year, File.GovernmentOf("netInterest"))),
                CircuitHistory.FederalInterest);
            ForeignEquity = File.ForeignEquityShare > 0
                ? CircuitHistory.Splice(new YearSeries(new[] { new[] { (double)Year, File.ForeignEquityShare } }),
                    CircuitHistory.ForeignEquity)
                : CircuitHistory.ForeignEquity;
            Exports = CircuitHistory.Exports;
            Imports = CircuitHistory.Imports;

            CategoryHistory = new YearSeries[SpendingCategories];
            for (int c = 0; c < SpendingCategories; c++)
            {
                CategoryHistory[c] = c < data.CategoryHistory.Count ? data.CategoryHistory[c] : YearSeries.Empty;
                if (CategoryHistory[c].IsEmpty)
                {
                    double share = Math.Max(data.Categories[c].Total(), 1);
                    CategoryHistory[c] = YearSeries.Constant(share);
                    Warnings.Add($"no history for '{data.Categories[c].Id}': its {Year} share held");
                }
            }

            // wealth groups
            Groups = new Group[data.Groups.Count];
            NetWorth = new YearSeries[Groups.Length];
            Equity = new YearSeries[Groups.Length];
            for (int g = 0; g < Groups.Length; g++)
            {
                Groups[g] = data.Groups[g];
                NetWorth[g] = g < data.GroupWealthHistory.Count ? data.GroupWealthHistory[g] : YearSeries.Empty;
                Equity[g] = EconomyData.Keyed(File.GroupEquityHistory, Groups[g].Id);
            }

            GroupMix = BuildGroupMix(data);

            // uses -> industries
            for (int c = 0; c < SpendingCategories; c++)
            {
                CategoryWeights[c] = SpendingWeights(data, data.Categories[c], out CategoryImports[c]);
            }

            PublicWeights = ValueAddedContent(data, "government");
            InvestmentWeights = ValueAddedContent(data, "investment");
            ExportWeights = ValueAddedContent(data, "exports");
        }

        // ------------------------------------------------------------------ helpers

        static double Value(Dictionary<string, double> d, string key, double fallback) =>
            d != null && d.TryGetValue(key, out double v) ? v : fallback;

        /// <summary>
        /// Shares of a few keys in their sum per history row (only rows that have all of them); with
        /// <paramref name="other"/> the row's business transfers + government enterprises' surplus as one more part.
        /// </summary>
        YearSeries[] Mix(List<Newtonsoft.Json.Linq.JArray> rows, string[] keys, bool other, double[] calibration)
        {
            int parts = keys.Length + (other ? 1 : 0);
            List<double[]>[] points = new List<double[]>[parts];
            for (int k = 0; k < parts; k++) points[k] = new List<double[]>();
            YearSeries[] series = new YearSeries[keys.Length];
            for (int k = 0; k < keys.Length; k++) series[k] = EconomyData.Keyed(rows, keys[k]);
            YearSeries transfers = EconomyData.Keyed(rows, "businessTransfers");
            YearSeries enterprises = EconomyData.Keyed(rows, "govEnterpriseSurplus");
            YearSeries first = series[0];
            for (int i = 0; i < first.Count; i++)
            {
                double y = first.YearAt(i);
                double[] v = new double[parts];
                bool complete = true;
                for (int k = 0; k < keys.Length; k++)
                {
                    if (!HasYear(series[k], y)) complete = false;
                    else v[k] = series[k].At(y);
                }

                if (!complete) continue;
                if (other)
                {
                    v[keys.Length] = (HasYear(transfers, y) ? transfers.At(y) : 0) + (HasYear(enterprises, y) ? enterprises.At(y) : 0);
                    v[keys.Length] = Math.Max(0, v[keys.Length]);
                }

                double sum = 0;
                foreach (double x in v) sum += Math.Max(0, x);
                if (sum <= 0) continue;
                for (int k = 0; k < parts; k++) points[k].Add(new[] { y, Math.Max(0, v[k]) / sum });
            }

            YearSeries[] result = new YearSeries[parts];
            for (int k = 0; k < parts; k++) result[k] = new YearSeries(points[k]);
            if (result[0].IsEmpty)
            {
                // no history: the calibration year's national income (circuit.json income)
                double[] cal = calibration ?? new[]
                {
                    File.IncomeOf("corporateProfitsDomestic") > 0
                        ? File.IncomeOf("corporateProfitsDomestic")
                        : File.IncomeOf("corporateProfits"),
                    File.IncomeOf("proprietors"), File.IncomeOf("rental"), File.IncomeOf("netInterest"),
                    Math.Max(0, File.IncomeOf("businessTransfers") + File.IncomeOf("govEnterpriseSurplus"))
                };
                double sum = 0;
                foreach (double x in cal) sum += x;
                for (int k = 0; k < parts; k++) result[k] = YearSeries.Constant(sum > 0 ? cal[k] / sum : 1.0 / parts);
                Warnings.Add("no income history: owners' income split held at the calibration year");
            }

            return result;
        }

        /// <summary>
        /// How corporate profits split into taxes, dividends and retained earnings each year (domestic industries,
        /// BEA NIPA 1.10), with the calibration year from circuit.json income (all US corporations). A year with
        /// negative retained earnings keeps none: taxes and dividends are scaled to the whole profit.
        /// </summary>
        (YearSeries taxes, YearSeries dividends, YearSeries retained) CorporateMix(List<Newtonsoft.Json.Linq.JArray> rows)
        {
            YearSeries t = EconomyData.Keyed(rows, "corporateTaxes"), d = EconomyData.Keyed(rows, "dividends"),
                r = EconomyData.Keyed(rows, "retained");
            List<double[]> pt = new List<double[]>(), pd = new List<double[]>(), pr = new List<double[]>();
            for (int i = 0; i < t.Count; i++)
            {
                double y = t.YearAt(i);
                if (y == Year || !HasYear(d, y) || !HasYear(r, y)) continue;
                Add(y, t.At(y), d.At(y), r.At(y));
            }

            double ct = File.IncomeOf("corporateTaxes"), cd = File.IncomeOf("dividends"), cr = File.IncomeOf("retained");
            if (ct + cd + cr > 0) Add(Year, ct, cd, cr);
            if (pt.Count == 0) Add(Year, 0.2, 0.5, 0.3);
            return (new YearSeries(pt), new YearSeries(pd), new YearSeries(pr));

            void Add(double year, double tax, double div, double ret)
            {
                ret = Math.Max(0, ret);
                double sum = Math.Max(0, tax) + Math.Max(0, div) + ret;
                if (sum <= 0) return;
                pt.Add(new[] { year, Math.Max(0, tax) / sum });
                pd.Add(new[] { year, Math.Max(0, div) / sum });
                pr.Add(new[] { year, ret / sum });
            }
        }

        static bool HasYear(YearSeries s, double year)
        {
            for (int i = 0; i < s.Count; i++)
            {
                if (s.YearAt(i) == year) return true;
            }

            return false;
        }

        /// <summary>
        /// Where a category's money goes: each item's money less its imports, spread over the industries by the item's
        /// weights (the value added it pays for along the supply chain), summed and normalized; the import share is the
        /// money-weighted share of the items' imports.
        /// </summary>
        double[] SpendingWeights(EconomyData data, Category category, out double importShare)
        {
            double[] w = new double[IndustryCount];
            double total = 0, imports = 0;
            Accumulate(category.Items);
            Accumulate(category.OutsidePce);
            double sum = 0;
            foreach (double x in w) sum += x;
            importShare = total > 0 ? imports / total : 0;
            if (sum <= 0)
            {
                Warnings.Add($"category '{category.Id}' has no industry weights: spread like GDP");
                return (double[])ReferenceVaShare.Clone();
            }

            for (int i = 0; i < w.Length; i++) w[i] /= sum;
            return w;

            void Accumulate(List<SpendingItem> items)
            {
                if (items == null) return;
                foreach (SpendingItem item in items)
                {
                    double amount = item.Amount;
                    if (amount <= 0) continue;
                    double m = Math.Max(0, Math.Min(1, item.ImportShare));
                    total += amount;
                    imports += amount * m;
                    if (item.Industries == null) continue;
                    foreach (KeyValuePair<string, double> kv in item.Industries)
                    {
                        Industry ind = data.IndustryById(kv.Key);
                        if (ind != null) w[ind.Index] += amount * (1 - m) * kv.Value;
                    }
                }
            }
        }

        /// <summary>
        /// The value added, by industry, that one dollar of a final-demand column (government, investment, exports)
        /// pays for: the column's purchases by commodity (circuit.json finalDemand) run through the Leontief inverse of
        /// the input-output table (circuit.json io), times each industry's value added per dollar of output; normalized.
        /// Without the table, the column's commodity shares themselves.
        /// </summary>
        double[] ValueAddedContent(EconomyData data, string column)
        {
            int n = IndustryCount;
            double[] f = new double[n];
            double fSum = 0;
            if (File.FinalDemand != null)
            {
                foreach (KeyValuePair<string, Dictionary<string, double>> kv in File.FinalDemand)
                {
                    Industry ind = data.IndustryById(kv.Key);
                    if (ind == null || kv.Value == null || !kv.Value.TryGetValue(column, out double v)) continue;
                    f[ind.Index] = Math.Max(0, v);
                    fSum += f[ind.Index];
                }
            }

            if (fSum <= 0)
            {
                Warnings.Add($"no final demand for '{column}': spread like GDP");
                return (double[])ReferenceVaShare.Clone();
            }

            double[] output = new double[n], valueAdded = new double[n];
            bool table = File.Io?.Accounts != null && File.Io.FlowRows != null;
            if (table)
            {
                for (int i = 0; i < n; i++)
                {
                    if (!File.Io.Accounts.TryGetValue(data.Industries[i].Id, out Dictionary<string, double> acc) || acc == null ||
                        !acc.TryGetValue("output", out output[i]) || !acc.TryGetValue("valueAdded", out valueAdded[i]) ||
                        output[i] <= 0)
                    {
                        table = false;
                        break;
                    }
                }
            }

            double[] content;
            if (table)
            {
                // (I - A) x = f, A[s, u] = flow(s -> u) / output(u)
                double[,] m = new double[n, n + 1];
                for (int i = 0; i < n; i++) m[i, i] = 1;
                foreach ((string from, string to, double value) in File.Io.Flows())
                {
                    Industry s = data.IndustryById(from), u = data.IndustryById(to);
                    if (s == null || u == null) continue;
                    m[s.Index, u.Index] -= value / output[u.Index];
                }

                for (int i = 0; i < n; i++) m[i, n] = f[i];
                double[] x = Solve(m, n);
                content = new double[n];
                for (int i = 0; i < n; i++) content[i] = x == null ? f[i] : Math.Max(0, x[i] * valueAdded[i] / output[i]);
            }
            else
            {
                Warnings.Add("no input-output table: returns follow final demand by commodity");
                content = f;
            }

            double sum = 0;
            foreach (double v in content) sum += v;
            for (int i = 0; i < n; i++) content[i] = sum > 0 ? content[i] / sum : ReferenceVaShare[i];
            return content;
        }

        /// <summary>Gauss-Jordan elimination with partial pivoting on an augmented n x (n + 1) matrix; null if singular.</summary>
        static double[] Solve(double[,] m, int n)
        {
            for (int c = 0; c < n; c++)
            {
                int pivot = c;
                for (int r = c + 1; r < n; r++)
                {
                    if (Math.Abs(m[r, c]) > Math.Abs(m[pivot, c])) pivot = r;
                }

                if (Math.Abs(m[pivot, c]) < 1e-12) return null;
                if (pivot != c)
                {
                    for (int k = 0; k <= n; k++) (m[c, k], m[pivot, k]) = (m[pivot, k], m[c, k]);
                }

                for (int r = 0; r < n; r++)
                {
                    if (r == c || m[r, c] == 0) continue;
                    double factor = m[r, c] / m[c, c];
                    for (int k = c; k <= n; k++) m[r, k] -= factor * m[c, k];
                }
            }

            double[] x = new double[n];
            for (int i = 0; i < n; i++) x[i] = m[i, n] / m[i, i];
            return x;
        }

        /// <summary>
        /// Each wealth group's spending mix: the survey's income quintiles (spending.json byQuintile, PCE basis) weighted
        /// by how the group's households spread over the income quintiles under the copula and by each quintile's mean
        /// spending. The top 1% spend like the top income quintile (the survey barely reaches them).
        /// </summary>
        double[,] BuildGroupMix(EconomyData data)
        {
            int groups = Groups.Length;
            double[,] mix = new double[groups, SpendingCategories];
            List<Quintile> quintiles = data.Spending?.ByQuintile;
            for (int g = 0; g < groups; g++)
            {
                double sum = 0;
                if (quintiles != null && quintiles.Count > 0)
                {
                    int q = quintiles.Count;
                    for (int k = 0; k < q; k++)
                    {
                        double w = HouseholdsIn(g, k / (double)q, (k + 1) / (double)q) * Math.Max(quintiles[k].SpendingMean, 1);
                        for (int c = 0; c < SpendingCategories; c++)
                        {
                            double share = quintiles[k].PceBasisShareOf(EconomyData.CategoryIds[c]);
                            mix[g, c] += w * share;
                            sum += w * share;
                        }
                    }
                }

                if (sum <= 0)
                {
                    // no survey: the national mix
                    for (int c = 0; c < SpendingCategories; c++)
                    {
                        mix[g, c] = Math.Max(data.Categories[c].Total(), 0);
                        sum += mix[g, c];
                    }
                }

                for (int c = 0; c < SpendingCategories; c++) mix[g, c] = sum > 0 ? mix[g, c] / sum : 1.0 / SpendingCategories;
            }

            return mix;
        }

        /// <summary>Share of a wealth group's households whose income rank lies in [lo, hi) under the Gumbel copula.</summary>
        double HouseholdsIn(int group, double lo, double hi)
        {
            int g = Math.Min(group, GroupLow.Length - 1);
            double a = GroupLow[g], b = GroupHigh[g];
            double p = Copula(hi, b) - Copula(lo, b) - Copula(hi, a) + Copula(lo, a);
            return Math.Max(0, p / (b - a));
        }

        static double Copula(double u, double v)
        {
            if (u <= 0 || v <= 0) return 0;
            u = Math.Min(u, 1);
            v = Math.Min(v, 1);
            double s = Math.Pow(-Math.Log(u), CopulaTheta) + Math.Pow(-Math.Log(v), CopulaTheta);
            return Math.Exp(-Math.Pow(s, 1 / CopulaTheta));
        }
    }
}
