using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Why.Economy.Data;

namespace Why.Economy.Model
{
    /// <summary>The columns of the money circuit, left to right (the return arcs close the loop to the first).</summary>
    public enum CircuitColumn
    {
        /// <summary>Industries (one node per tier of the wall) and the value they add.</summary>
        Industries = 0,

        /// <summary>How that value is split: wages, owners' share, production taxes, depreciation.</summary>
        Income = 1,

        /// <summary>Who receives it: the four wealth groups, the government, holders abroad, reinvestment.</summary>
        Recipients = 2,

        /// <summary>The four wealth groups again after taxes and transfers: what households have to spend.</summary>
        Households = 3,

        /// <summary>What the money is used for: the seven categories, public goods, investment.</summary>
        Uses = 4
    }

    /// <summary>A node of the circuit: a sum of money in a year ($B, nominal).</summary>
    public sealed class CircuitNode
    {
        public string Id;
        public string Name;
        public CircuitColumn Column;

        /// <summary>Position in the node list (link endpoints refer to it). Nodes are listed column by column, each
        /// column top to bottom, and every year has the same nodes in the same order.</summary>
        public int Index;

        /// <summary>
        /// Money through the node in the year, $B nominal: the value added for an industry tier, otherwise the larger of
        /// what enters and what leaves (they differ by the node's share of the circuit's imbalance).
        /// </summary>
        public double Value;

        /// <summary>Money entering and leaving the node through its links ($B).</summary>
        public double In, Out;

        /// <summary>
        /// What the node is: "tier", "wages", "owners", "taxes", "depreciation", "group", "government", "abroad", "reinvest",
        /// "credit", "household", "purse", "borrowing", "category", "saving", "public", "investment", "exports".
        /// </summary>
        public string Kind;

        /// <summary>Hue: "capital", "gov", "matter", "life", "people", "desire", "fear", "neutral".</summary>
        public string Level = "neutral";

        /// <summary>For spending nodes: share of the motive that is fear (0..1), -1 otherwise.</summary>
        public float FearShare = -1;

        /// <summary>One or two sentences with the year's numbers for the tooltip.</summary>
        public string Blurb;
    }

    /// <summary>A flow of money between two nodes in a year ($B, nominal).</summary>
    public sealed class CircuitLink
    {
        public int From, To;
        public double Value;

        /// <summary>
        /// What the money is: "value" (industry -> income type), "wages", "owners" (capital income to a group), "payout"
        /// (to holders abroad), "retained", "tax", "transfer", "keep" (recipient -> household), "credit", "spend", "save",
        /// "public", "invest", "export", "lend", "repay", "return" (use -> industry), "import" (use -> abroad).
        /// </summary>
        public string Kind;

        /// <summary>For spending and return links: share of the motive that is fear (0..1), -1 otherwise.</summary>
        public float FearShare = -1;

        /// <summary>
        /// True for the arcs that carry money from its uses back to where it came from: into the industries (closing
        /// the loop), abroad (imports) and to the lenders' nodes (credit, borrowing).
        /// </summary>
        public bool Return;
    }

    /// <summary>The circuit of one year: nodes by column, links between them, and how well it balances.</summary>
    public sealed class CircuitYear
    {
        public int Year;
        public readonly List<CircuitNode> Nodes = new List<CircuitNode>();
        public readonly List<CircuitLink> Links = new List<CircuitLink>();

        /// <summary>GDP of the year ($B nominal): the size of the first column (the industries' value added).</summary>
        public double Gdp;

        /// <summary>Largest gap between what enters and leaves a node, as a share of GDP (0 = balanced).</summary>
        public double Imbalance;

        /// <summary>The node with that gap.</summary>
        public string ImbalanceNode;

        /// <summary>Household disposable income in the circuit relative to BEA's for the year (1 = equal).</summary>
        public double DisposableRatio;

        /// <summary>How the year was derived (calibration year, scaled history, approximations, residuals).</summary>
        public string Notes;

        public CircuitNode Node(string id)
        {
            foreach (CircuitNode n in Nodes)
            {
                if (n.Id == id) return n;
            }

            return null;
        }

        public IEnumerable<CircuitNode> InColumn(CircuitColumn c)
        {
            foreach (CircuitNode n in Nodes)
            {
                if (n.Column == c) yield return n;
            }
        }
    }

    /// <summary>
    /// The money circuit: for any year since 1947, how the value industries add is paid out (wages, owners' share,
    /// production taxes, depreciation), who receives it (wealth groups after income and payroll taxes, the state,
    /// holders abroad, firms' reinvestment), what households have after social benefits and interest, what they spend
    /// it on (the notebook's categories) and which industries the spending returns to. Saving loops back as lending
    /// (to households who spend more than they earn, to the state's deficit, to firms' investment), imports leave for
    /// the rest of the world and come back as exports and foreign lending; the loop closes when every use returns to
    /// the industries that produced what it bought.
    ///
    /// Calibrated on circuit.json's year (2025) and carried through time with BEA's income and personal accounts
    /// (circuit.json incomeHistory, personalHistory), the industries' value added, the wealth groups' history and the
    /// spending history; what the files lack is approximated (<see cref="CircuitHistory"/>) and said in each year's
    /// <see cref="CircuitYear.Notes"/>. Years are built on demand and cached; thread safe.
    /// </summary>
    public sealed class MoneyCircuit
    {
        /// <summary>Iterations of the trade and lending loop (imports of investment depend on what is lent to firms).</summary>
        const int LendingIterations = 24;

        /// <summary>Iterations fitting the groups' spending mixes to the year's national category shares.</summary>
        const int MixIterations = 24;

        /// <summary>Largest share of a use that can buy imports once scaled to the economy's imports.</summary>
        const double MaxImportShare = 0.6;

        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        readonly EconomyData data;
        readonly CircuitCalibration cal;
        readonly Dictionary<int, CircuitYear> cache = new Dictionary<int, CircuitYear>();
        readonly object gate = new object();

        // node indices (the same every year); columns top to bottom
        readonly List<(string id, string name, CircuitColumn column, string kind, string level, float fear)> template =
            new List<(string, string, CircuitColumn, string, string, float)>();

        readonly int[] tierNode, groupNode, householdNode, categoryNode;
        readonly int wagesNode, ownersNode, taxesNode, depreciationNode;
        readonly int governmentNode, reinvestNode, abroadNode, creditNode, purseNode, borrowingNode;
        readonly int savingNode, publicNode, investmentNode, exportsNode;

        public MoneyCircuit(EconomyData data)
        {
            this.data = data;
            cal = new CircuitCalibration(data);

            int tiers = data.Tiers.Count, groups = cal.Groups.Length;
            tierNode = new int[tiers];
            groupNode = new int[groups];
            householdNode = new int[groups];
            categoryNode = new int[CircuitCalibration.SpendingCategories];

            // industries: the notebook's order, tech on top and the state at the foundation
            for (int t = tiers - 1; t >= 0; t--)
            {
                Tier tier = data.Tiers[t];
                string level = tier.Id == "gov" ? "gov" : tier.Id == "raw" ? "matter" : "capital";
                tierNode[t] = Add("tier:" + tier.Id, tier.Name ?? tier.Id, CircuitColumn.Industries, "tier", level);
            }

            wagesNode = Add("wages", "Wages", CircuitColumn.Income, "wages", "people");
            ownersNode = Add("owners", "Owners' share", CircuitColumn.Income, "owners", "capital");
            taxesNode = Add("prodtaxes", "Taxes on production", CircuitColumn.Income, "taxes", "gov");
            depreciationNode = Add("depreciation", "Depreciation", CircuitColumn.Income, "depreciation", "capital");

            for (int g = 0; g < groups; g++)
            {
                groupNode[g] = Add(cal.Groups[g].Id, cal.Groups[g].Name ?? cal.Groups[g].Id, CircuitColumn.Recipients, "group",
                    "people");
            }

            governmentNode = Add("government", "Government", CircuitColumn.Recipients, "government", "gov");
            reinvestNode = Add("reinvestment", "Reinvestment", CircuitColumn.Recipients, "reinvest", "capital");
            abroadNode = Add("abroad", "Abroad", CircuitColumn.Recipients, "abroad", "neutral");
            creditNode = Add("credit", "Credit", CircuitColumn.Recipients, "credit", "neutral");

            for (int g = 0; g < groups; g++)
            {
                householdNode[g] = Add("hh:" + cal.Groups[g].Id, cal.Groups[g].Name ?? cal.Groups[g].Id, CircuitColumn.Households,
                    "household", "people");
            }

            purseNode = Add("purse", "Public purse", CircuitColumn.Households, "purse", "gov");
            borrowingNode = Add("borrowing", "Borrowing", CircuitColumn.Households, "borrowing", "neutral");

            for (int c = 0; c < CircuitCalibration.SpendingCategories; c++)
            {
                Category cat = data.Categories[c];
                float fear = (float)cat.FearShare;
                categoryNode[c] = Add(cat.Id, cat.Name ?? cat.Id, CircuitColumn.Uses, "category", fear >= 0.5f ? "fear" : "desire",
                    fear);
            }

            Category saving = data.CategoryById("saving");
            float savingFear = saving != null ? (float)saving.FearShare : 0.5f;
            savingNode = Add("saving", saving?.Name ?? "Saving", CircuitColumn.Uses, "saving", savingFear >= 0.5f ? "fear" : "desire",
                savingFear);
            publicNode = Add("public", "Public goods", CircuitColumn.Uses, "public", "gov");
            investmentNode = Add("investment", "Investment", CircuitColumn.Uses, "investment", "capital");
            exportsNode = Add("exports", "Exports", CircuitColumn.Uses, "exports", "neutral");

            // personal taxes before BEA's personal-income detail: from the circuit's own disposable income, spliced
            cal.SpliceTaxes(y => Run(y, true).TaxEstimate);
        }

        int Add(string id, string name, CircuitColumn column, string kind, string level, float fear = -1)
        {
            template.Add((id, name, column, kind, level, fear));
            return template.Count - 1;
        }

        public EconomyData Data => data;

        /// <summary>Year of the calibration (circuit.json "year").</summary>
        public int CalibrationYear => cal.Year;

        /// <summary>The circuit of a calendar year (clamped to the data's years).</summary>
        public CircuitYear Build(int year)
        {
            year = Math.Max(data.FirstYear, Math.Min(data.LastYear, year));
            lock (gate)
            {
                if (cache.TryGetValue(year, out CircuitYear cached)) return cached;
            }

            CircuitYear built = Compute(year);
            lock (gate) cache[year] = built;
            return built;
        }

        // ------------------------------------------------------------------ the year's money

        /// <summary>Every amount of a year ($B), computed by <see cref="Run"/> before nodes and links are made.</summary>
        sealed class Flows
        {
            public int Year;
            public double Gdp, Labor, WageFactor;
            public double[] Va, TierVa, TierWages, TierTaxes, TierDepreciation, TierOwners;
            public double Wages, ProductionTaxes, Depreciation, DepreciationGov, Owners;
            public double Corporate, Proprietors, Rental, NetInterest, Other;
            public double CorporateTaxes, Dividends, Buybacks, Retained, Payouts, PayoutsAbroad, ForeignShare;
            public double[] GWages, GPayouts, GBusiness, GRent, GInterest, GPretax, GTax, GTaxOnWages, GAfterTax;
            public double PersonalTaxes, Contributions, Transfers, PublicInterest, TaxEstimate;
            public double[] GTransfers, GPublicInterest, GHousehold, GSpend, GSaving;
            public double Government, Purse, Purchases, Borrow, Repay;
            public double Disposable, SavingRate, Spending, DisposableData;
            public double[] Category;
            public double[,] Mix;
            public double PositiveSaving, Credit, SavingLentAbroad, SavingToBorrowing, ForeignLending, LentToFirms;
            public double Exports, ExportDemand, ImportScale, Investment;
            public double[] CategoryImports;
            public double PublicImports, InvestmentImports, Imports;
        }

        /// <summary>
        /// The year's amounts. With <paramref name="incomeOnly"/> it stops once households' income before taxes is known
        /// (enough for <see cref="Flows.TaxEstimate"/>, the tax splice).
        /// </summary>
        Flows Run(int year, bool incomeOnly = false)
        {
            Flows f = new Flows { Year = year };
            IReadOnlyList<Industry> inds = data.Industries;
            int n = inds.Count, tiers = data.Tiers.Count, groups = cal.Groups.Length;

            // 1. value added by industry; the industries' sum is the year's GDP
            f.Va = new double[n];
            for (int i = 0; i < n; i++)
            {
                f.Va[i] = Math.Max(0, inds[i].ValueAdded.GrowthAt(year));
                f.Gdp += f.Va[i];
            }

            double gdp = Math.Max(f.Gdp, 1e-9);

            // 2. the split of each industry's value added: production taxes and depreciation at the industry's latest
            // shares scaled to BEA's totals for the year; wages at its latest share scaled by the labor share (as the
            // wall draws them), then by one factor so they add up to BEA's compensation; owners keep the rest
            double rawTaxes = 0, rawDepreciation = 0;
            for (int i = 0; i < n; i++)
            {
                rawTaxes += f.Va[i] * inds[i].TaxShare;
                rawDepreciation += f.Va[i] * inds[i].DepShare;
            }

            double taxFactor = Factor(cal.ProductionTaxes, year, gdp, rawTaxes);
            double depFactor = Factor(cal.Depreciation, year, gdp, rawDepreciation);
            double laborRef = data.LaborShare.IsEmpty ? 1 : data.LaborShare.At(IndustryShareYear);
            f.Labor = data.LaborShare.IsEmpty || laborRef <= 0 ? 1 : data.LaborShare.At(year) / laborRef;
            double wallWages = 0;
            for (int i = 0; i < n; i++)
            {
                Industry ind = inds[i];
                double upkeep = Math.Min(1, ind.TaxShare * taxFactor + ind.DepShare * depFactor);
                wallWages += f.Va[i] * Math.Max(0, Math.Min(1 - upkeep, ind.CompShare * f.Labor));
            }

            double compensation = cal.Compensation.IsEmpty ? 0 : cal.Compensation.At(year) * gdp;
            f.WageFactor = compensation > 0 && wallWages > 0 ? compensation / wallWages : 1;

            f.TierVa = new double[tiers];
            f.TierWages = new double[tiers];
            f.TierTaxes = new double[tiers];
            f.TierDepreciation = new double[tiers];
            f.TierOwners = new double[tiers];
            int govTier = TierIndex("gov");
            for (int i = 0; i < n; i++)
            {
                Industry ind = inds[i];
                double v = f.Va[i];
                double tax = ind.TaxShare * taxFactor, dep = ind.DepShare * depFactor;
                double upkeep = Math.Min(1, tax + dep);
                double wage = Math.Max(0, Math.Min(1 - upkeep, ind.CompShare * f.Labor * f.WageFactor));
                int t = ind.TierIndex;
                f.TierVa[t] += v;
                f.TierWages[t] += v * wage;
                f.TierTaxes[t] += v * Math.Min(tax, 1);
                f.TierDepreciation[t] += v * Math.Min(dep, 1 - Math.Min(tax, 1));
                f.TierOwners[t] += v * Math.Max(0, 1 - wage - upkeep);
            }

            for (int t = 0; t < tiers; t++)
            {
                f.Wages += f.TierWages[t];
                f.ProductionTaxes += f.TierTaxes[t];
                f.Depreciation += f.TierDepreciation[t];
                f.Owners += f.TierOwners[t];
            }

            f.DepreciationGov = govTier >= 0 ? f.TierDepreciation[govTier] : 0;

            // 3. owners' share by type of income (BEA's mix for the year; the statistical discrepancy rides along)
            double[] parts = new double[CircuitCalibration.OwnersParts];
            double partsSum = 0;
            for (int p = 0; p < parts.Length; p++)
            {
                parts[p] = Math.Max(0, cal.OwnersSplit[p].At(year));
                partsSum += parts[p];
            }

            for (int p = 0; p < parts.Length; p++) parts[p] = partsSum > 0 ? parts[p] / partsSum : 0;
            f.Corporate = f.Owners * parts[CircuitCalibration.Corporate];
            f.Proprietors = f.Owners * parts[CircuitCalibration.Proprietors];
            f.Rental = f.Owners * parts[CircuitCalibration.Rental];
            f.NetInterest = f.Owners * parts[CircuitCalibration.NetInterest];
            f.Other = f.Owners * parts[CircuitCalibration.Other];

            // corporate profits: tax, dividends, retained; net buybacks are paid out of retained earnings
            double ct = cal.CorporateTaxes.At(year), cd = cal.Dividends.At(year), cr = cal.Retained.At(year);
            double cs = Math.Max(ct + cd + cr, 1e-9);
            f.CorporateTaxes = f.Corporate * ct / cs;
            f.Dividends = f.Corporate * cd / cs;
            double retained = f.Corporate * cr / cs;
            double ramp = Clamp01((year - CircuitCalibration.BuybackStartYear) / (double)CircuitCalibration.BuybackRampYears);
            f.Buybacks = Math.Min(retained, cal.BuybackShare * ramp * gdp);
            f.Retained = retained - f.Buybacks;
            f.Payouts = f.Dividends + f.Buybacks;
            f.ForeignShare = Clamp01(cal.ForeignEquity.At(year));
            f.PayoutsAbroad = f.Payouts * f.ForeignShare;
            double payoutsHome = f.Payouts - f.PayoutsAbroad;

            // 4. who receives it: wages by the groups' shares of wages; payouts by equity (direct and through pensions),
            // proprietors' income by business equity, rent by real estate, interest by deposits and bonds; the capital
            // shares follow each group's wealth over time (equity from the Fed's accounts since 1989)
            double[] equity = EquityShares(year);
            double[] business = WealthScaled(year, g => g.BusinessShare);
            double[] estate = WealthScaled(year, g => g.RealEstateShare);
            double[] interest = WealthScaled(year, g => g.InterestShare);
            double pensions = Clamp01(cal.File.PensionEquityShare);
            f.GWages = new double[groups];
            f.GPayouts = new double[groups];
            f.GBusiness = new double[groups];
            f.GRent = new double[groups];
            f.GInterest = new double[groups];
            f.GPretax = new double[groups];
            double pretax = 0;
            for (int g = 0; g < groups; g++)
            {
                Group grp = cal.Groups[g];
                f.GWages[g] = f.Wages * grp.WageShare;
                f.GPayouts[g] = payoutsHome * (pensions * grp.PensionShare + (1 - pensions) * equity[g]);
                f.GBusiness[g] = f.Proprietors * business[g];
                f.GRent[g] = f.Rental * estate[g];
                f.GInterest[g] = f.NetInterest * interest[g];
                f.GPretax[g] = f.GWages[g] + f.GPayouts[g] + f.GBusiness[g] + f.GRent[g] + f.GInterest[g];
                pretax += f.GPretax[g];
            }

            f.Transfers = cal.Transfers.At(year) * gdp;
            f.PublicInterest = cal.PublicInterest.At(year) * gdp;
            f.DisposableData = cal.Disposable.At(year) * gdp;
            f.TaxEstimate = Math.Max(0.1 * pretax, pretax + f.Transfers + f.PublicInterest - f.DisposableData);
            if (incomeOnly) return f;

            // personal current taxes (by the groups' tax shares) and contributions for social insurance (by wages)
            if (year >= cal.TaxDataYear)
            {
                f.PersonalTaxes = cal.PersonalTaxes.At(year) * gdp;
                f.Contributions = cal.Contributions.At(year) * gdp;
            }
            else
            {
                double total = f.TaxEstimate * cal.TaxSplice;
                f.Contributions = total * cal.ContributionFraction;
                f.PersonalTaxes = total - f.Contributions;
            }

            f.GTax = new double[groups];
            f.GTaxOnWages = new double[groups];
            f.GAfterTax = new double[groups];
            for (int g = 0; g < groups; g++)
            {
                Group grp = cal.Groups[g];
                double tax = Math.Min(f.GPretax[g], f.PersonalTaxes * grp.TaxShare + f.Contributions * grp.WageShare);
                f.GTax[g] = tax;
                f.GTaxOnWages[g] = f.GPretax[g] > 0 ? tax * f.GWages[g] / f.GPretax[g] : 0;
                f.GAfterTax[g] = f.GPretax[g] - tax;
            }

            // the state: every tax, its own capital's wear; it pays social benefits and interest, buys public goods with
            // the rest and borrows what is missing (or repays debt in a surplus)
            double householdTaxes = 0;
            foreach (double x in f.GTax) householdTaxes += x;
            f.Government = f.ProductionTaxes + f.CorporateTaxes + f.Other + f.DepreciationGov + householdTaxes;
            f.Purse = Math.Max(0, f.Government - f.Transfers - f.PublicInterest);
            f.Purchases = cal.Purchases.At(year) * gdp;
            f.Borrow = Math.Max(0, f.Purchases - f.Purse);
            f.Repay = Math.Max(0, f.Purse - f.Purchases);

            // 5. households after taxes, social benefits and interest on public debt
            f.GTransfers = new double[groups];
            f.GPublicInterest = new double[groups];
            f.GHousehold = new double[groups];
            for (int g = 0; g < groups; g++)
            {
                f.GTransfers[g] = f.Transfers * cal.Groups[g].TransferShare;
                f.GPublicInterest[g] = f.PublicInterest * interest[g];
                f.GHousehold[g] = f.GAfterTax[g] + f.GTransfers[g] + f.GPublicInterest[g];
                f.Disposable += f.GHousehold[g];
            }

            // 6. spending: BEA's saving rate for the year on the circuit's disposable income; each group spends with its
            // calibration-year propensity (1 - its saving rate), scaled so the groups add up
            f.SavingRate = cal.SavingRate.At(year);
            f.Spending = f.Disposable * (1 - f.SavingRate);
            double[] propensity = Propensities();
            double weighted = 0;
            for (int g = 0; g < groups; g++) weighted += propensity[g] * f.GHousehold[g];
            double scale = weighted > 0 ? f.Spending / weighted : 0;
            f.GSpend = new double[groups];
            f.GSaving = new double[groups];
            for (int g = 0; g < groups; g++)
            {
                f.GSpend[g] = propensity[g] * f.GHousehold[g] * scale;
                f.GSaving[g] = f.GHousehold[g] - f.GSpend[g];
                if (f.GSaving[g] >= 0) f.PositiveSaving += f.GSaving[g];
                else f.Credit -= f.GSaving[g];
            }

            // categories: the year's national shares of disposable income (without saving), split among the groups by
            // their spending mixes fitted to those totals
            int cats = CircuitCalibration.SpendingCategories;
            f.Category = new double[cats];
            double shares = 0;
            for (int c = 0; c < cats; c++) shares += Math.Max(0, cal.CategoryHistory[c].At(year));
            for (int c = 0; c < cats; c++)
            {
                f.Category[c] = shares > 0 ? f.Spending * Math.Max(0, cal.CategoryHistory[c].At(year)) / shares : f.Spending / cats;
            }

            f.Mix = FitMix(f.GSpend, f.Category);

            // 7. trade and lending: uses buy imports (their 2025 import shares scaled by the economy's imports); abroad
            // the dollars buy exports and the rest is lent back; savings go to those who spend more than they earn,
            // abroad when the world buys more from the US than it sells, to the state's deficit and to firms
            // (the uses' own import shares say who buys imports; their sum is scaled to the economy's imports, which
            // also come in through the supply chains of domestic products)
            double reinvest = f.Retained + (f.Depreciation - f.DepreciationGov);
            double raw = cal.InvestmentImportShareOf(reinvest) + f.Purchases * CircuitCalibration.PublicImportShare;
            for (int c = 0; c < cats; c++) raw += f.Category[c] * cal.CategoryImports[c];
            f.ImportScale = raw > 0 ? cal.Imports.At(year) * gdp / raw : 1;
            f.CategoryImports = new double[cats];
            for (int c = 0; c < cats; c++)
            {
                f.CategoryImports[c] = f.Category[c] * Math.Min(MaxImportShare, cal.CategoryImports[c] * f.ImportScale);
                f.Imports += f.CategoryImports[c];
            }

            f.PublicImports = f.Purchases * Math.Min(MaxImportShare, CircuitCalibration.PublicImportShare * f.ImportScale);
            f.Imports += f.PublicImports;
            double investmentImportShare = Math.Min(MaxImportShare, CircuitCalibration.InvestmentImportShare * f.ImportScale);
            f.ExportDemand = cal.Exports.At(year) * gdp;
            double available = Math.Max(0, f.PositiveSaving + f.Repay - f.Credit);
            for (int it = 0; it < LendingIterations; it++)
            {
                f.Investment = reinvest + f.LentToFirms;
                f.InvestmentImports = f.Investment * investmentImportShare;
                double abroadIn = f.PayoutsAbroad + f.Imports + f.InvestmentImports;
                f.SavingLentAbroad = Math.Min(available, Math.Max(0, f.ExportDemand - abroadIn));
                abroadIn += f.SavingLentAbroad;
                f.Exports = Math.Min(f.ExportDemand, abroadIn);
                f.ForeignLending = abroadIn - f.Exports;
                f.SavingToBorrowing = available - f.SavingLentAbroad;
                f.LentToFirms = Math.Max(0, f.ForeignLending + f.SavingToBorrowing - f.Borrow);
            }

            f.Investment = reinvest + f.LentToFirms;
            f.InvestmentImports = f.Investment * investmentImportShare;
            f.Imports += f.InvestmentImports;
            return f;
        }

        /// <summary>Year of the industries' compShare (as the wall scales wages by the labor share).</summary>
        const double IndustryShareYear = 2024;

        /// <summary>The factor that brings a sum of industry shares to BEA's total for the year (1 when unknown).</summary>
        static double Factor(YearSeries share, int year, double gdp, double raw)
        {
            if (share.IsEmpty || raw <= 0) return 1;
            return share.At(year) * gdp / raw;
        }

        int TierIndex(string id)
        {
            foreach (Tier t in data.Tiers)
            {
                if (t.Id == id) return t.Index;
            }

            return -1;
        }

        /// <summary>
        /// Groups' shares of corporate equity: the Fed's accounts from 1989; before, the 1989 shares moved with each
        /// group's share of net worth; without history, the calibration shares.
        /// </summary>
        double[] EquityShares(int year)
        {
            int groups = cal.Groups.Length;
            double[] s = new double[groups];
            bool history = true;
            for (int g = 0; g < groups; g++) history &= !cal.Equity[g].IsEmpty;
            if (!history) return WealthScaled(year, g => g.EquityShare);
            double first = cal.Equity[0].FirstYear;
            double sum = 0;
            for (int g = 0; g < groups; g++)
            {
                double v = cal.Equity[g].At(Math.Max(year, first));
                if (year < first)
                {
                    double then = cal.NetWorth[g].At(first);
                    v *= then > 0 ? cal.NetWorth[g].At(year) / then : 1;
                }

                s[g] = Math.Max(0, v);
                sum += s[g];
            }

            for (int g = 0; g < groups; g++) s[g] = sum > 0 ? s[g] / sum : 1.0 / groups;
            return s;
        }

        /// <summary>A calibration share moved with each group's share of net worth since the calibration year, normalized.</summary>
        double[] WealthScaled(int year, Func<Group, double> share)
        {
            int groups = cal.Groups.Length;
            double[] s = new double[groups];
            double sum = 0;
            for (int g = 0; g < groups; g++)
            {
                double now = cal.NetWorth[g].At(cal.Year);
                double factor = now > 0 && !cal.NetWorth[g].IsEmpty ? cal.NetWorth[g].At(year) / now : 1;
                s[g] = Math.Max(0, share(cal.Groups[g]) * factor);
                sum += s[g];
            }

            for (int g = 0; g < groups; g++) s[g] = sum > 0 ? s[g] / sum : 1.0 / groups;
            return s;
        }

        /// <summary>
        /// Each group's propensity to spend out of its disposable income: 1 - its saving rate in the calibration year
        /// (circuit.json groups); from the consumption shares when the file has no saving rates.
        /// </summary>
        double[] Propensities()
        {
            int groups = cal.Groups.Length;
            double[] p = new double[groups];
            bool any = false;
            for (int g = 0; g < groups; g++)
            {
                p[g] = 1 - cal.Groups[g].SavingRate;
                any |= cal.Groups[g].SavingRate != 0;
            }

            if (any) return p;
            for (int g = 0; g < groups; g++) p[g] = Math.Max(cal.Groups[g].ConsumptionShare, 1e-3) * groups;
            return p;
        }

        /// <summary>
        /// The groups' category mixes for the year: their survey mixes scaled, category by category and group by group,
        /// until the groups' spending adds up to the year's national category totals (iterative proportional fitting).
        /// </summary>
        double[,] FitMix(double[] spend, double[] totals)
        {
            int groups = spend.Length, cats = totals.Length;
            double[,] m = new double[groups, cats];
            for (int g = 0; g < groups; g++)
            {
                for (int c = 0; c < cats; c++) m[g, c] = Math.Max(cal.GroupMix[g, c], 1e-6);
            }

            for (int it = 0; it < MixIterations; it++)
            {
                for (int c = 0; c < cats; c++)
                {
                    double sum = 0;
                    for (int g = 0; g < groups; g++) sum += spend[g] * m[g, c];
                    double factor = sum > 0 ? totals[c] / sum : 1;
                    for (int g = 0; g < groups; g++) m[g, c] *= factor;
                }

                for (int g = 0; g < groups; g++)
                {
                    double sum = 0;
                    for (int c = 0; c < cats; c++) sum += m[g, c];
                    for (int c = 0; c < cats; c++) m[g, c] = sum > 0 ? m[g, c] / sum : 1.0 / cats;
                }
            }

            return m;
        }

        /// <summary>
        /// A use's money spread over the industries: the use's weights tilted toward the year's industry mix (each
        /// industry's weight times its share of GDP in the year over its share in the calibration year), normalized.
        /// </summary>
        double[] Tilted(double[] weights, Flows f)
        {
            int n = weights.Length;
            double[] w = new double[n];
            double sum = 0;
            for (int i = 0; i < n; i++)
            {
                double reference = cal.ReferenceVaShare[i];
                double now = f.Gdp > 0 ? f.Va[i] / f.Gdp : 0;
                w[i] = reference > 0 ? weights[i] * now / reference : 0;
                sum += w[i];
            }

            for (int i = 0; i < n; i++) w[i] = sum > 0 ? w[i] / sum : weights[i];
            return w;
        }

        static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;

        // ------------------------------------------------------------------ nodes and links

        CircuitYear Compute(int year)
        {
            Flows f = Run(year);
            CircuitYear c = new CircuitYear { Year = year, Gdp = f.Gdp };
            foreach ((string id, string name, CircuitColumn column, string kind, string level, float fear) in template)
            {
                c.Nodes.Add(new CircuitNode
                {
                    Id = id, Name = name, Column = column, Index = c.Nodes.Count, Kind = kind, Level = level, FearShare = fear
                });
            }

            Links links = new Links(c);
            int tiers = data.Tiers.Count, groups = cal.Groups.Length, cats = CircuitCalibration.SpendingCategories;

            // industries -> the split of their value
            for (int t = 0; t < tiers; t++)
            {
                links.Add(tierNode[t], wagesNode, f.TierWages[t], "value");
                links.Add(tierNode[t], ownersNode, f.TierOwners[t], "value");
                links.Add(tierNode[t], taxesNode, f.TierTaxes[t], "value");
                links.Add(tierNode[t], depreciationNode, f.TierDepreciation[t], "value");
            }

            // the split -> who receives it (income and payroll taxes withheld on the way to the government)
            double wageTax = 0, capitalTax = 0;
            for (int g = 0; g < groups; g++)
            {
                double capital = f.GPretax[g] - f.GWages[g], taxOnCapital = f.GTax[g] - f.GTaxOnWages[g];
                links.Add(wagesNode, groupNode[g], f.GWages[g] - f.GTaxOnWages[g], "wages");
                links.Add(ownersNode, groupNode[g], capital - taxOnCapital, "owners");
                wageTax += f.GTaxOnWages[g];
                capitalTax += taxOnCapital;
            }

            links.Add(wagesNode, governmentNode, wageTax, "tax");
            links.Add(ownersNode, governmentNode, capitalTax + f.CorporateTaxes + f.Other, "tax");
            links.Add(ownersNode, abroadNode, f.PayoutsAbroad, "payout");
            links.Add(ownersNode, reinvestNode, f.Retained, "retained");
            links.Add(taxesNode, governmentNode, f.ProductionTaxes, "tax");
            links.Add(depreciationNode, governmentNode, f.DepreciationGov, "value");
            links.Add(depreciationNode, reinvestNode, f.Depreciation - f.DepreciationGov, "retained");

            // who receives it -> households after social benefits and interest; the state's purse; lending
            for (int g = 0; g < groups; g++)
            {
                links.Add(groupNode[g], householdNode[g], f.GAfterTax[g], "keep");
                links.Add(governmentNode, householdNode[g], f.GTransfers[g] + f.GPublicInterest[g], "transfer");
                if (f.GSaving[g] < 0) links.Add(creditNode, householdNode[g], -f.GSaving[g], "credit");
            }

            links.Add(governmentNode, purseNode, f.Purse, "keep");
            links.Add(abroadNode, borrowingNode, f.ForeignLending, "lend");
            links.Add(reinvestNode, investmentNode, f.Investment - f.LentToFirms, "invest");
            links.Add(abroadNode, exportsNode, f.Exports, "export");

            // households -> what they spend it on and save
            for (int g = 0; g < groups; g++)
            {
                for (int k = 0; k < cats; k++)
                {
                    links.Add(householdNode[g], categoryNode[k], f.GSpend[g] * f.Mix[g, k], "spend",
                        c.Nodes[categoryNode[k]].FearShare);
                }

                if (f.GSaving[g] > 0) links.Add(householdNode[g], savingNode, f.GSaving[g], "save", c.Nodes[savingNode].FearShare);
            }

            links.Add(purseNode, publicNode, f.Purse - f.Repay, "public");
            links.Add(purseNode, savingNode, f.Repay, "repay");
            links.Add(borrowingNode, publicNode, f.Borrow, "public");
            links.Add(borrowingNode, investmentNode, f.LentToFirms, "lend");

            // the returns: every use back to the industries that made what it bought (and abroad for imports); saving
            // back to the lenders' nodes
            for (int k = 0; k < cats; k++)
            {
                float fear = c.Nodes[categoryNode[k]].FearShare;
                Return(links, f, categoryNode[k], f.Category[k] - f.CategoryImports[k], cal.CategoryWeights[k], fear);
                links.Add(categoryNode[k], abroadNode, f.CategoryImports[k], "import", fear, true);
            }

            Return(links, f, publicNode, f.Purchases - f.PublicImports, cal.PublicWeights, -1);
            links.Add(publicNode, abroadNode, f.PublicImports, "import", -1, true);
            Return(links, f, investmentNode, f.Investment - f.InvestmentImports, cal.InvestmentWeights, -1);
            links.Add(investmentNode, abroadNode, f.InvestmentImports, "import", -1, true);
            Return(links, f, exportsNode, f.Exports, cal.ExportWeights, -1);
            float savingFear = c.Nodes[savingNode].FearShare;
            links.Add(savingNode, creditNode, f.Credit, "lend", savingFear, true);
            links.Add(savingNode, abroadNode, f.SavingLentAbroad, "lend", savingFear, true);
            links.Add(savingNode, borrowingNode, f.SavingToBorrowing, "lend", savingFear, true);

            Balance(c);
            c.DisposableRatio = f.DisposableData > 0 ? f.Disposable / f.DisposableData : 1;
            Describe(c, f);
            return c;
        }

        void Return(Links links, Flows f, int from, double amount, double[] weights, float fear)
        {
            if (amount <= 0) return;
            double[] w = Tilted(weights, f);
            double[] perTier = new double[data.Tiers.Count];
            for (int i = 0; i < w.Length; i++) perTier[cal.TierOf[i]] += amount * w[i];
            for (int t = perTier.Length - 1; t >= 0; t--) links.Add(from, tierNode[t], perTier[t], "return", fear, true);
        }

        /// <summary>Links by endpoints, merged when the same pair carries the same kind of money twice.</summary>
        sealed class Links
        {
            /// <summary>Flows smaller than this ($B) are left out (rounding dust).</summary>
            const double MinValue = 1e-6;

            readonly CircuitYear year;
            readonly Dictionary<long, CircuitLink> byPair = new Dictionary<long, CircuitLink>();

            public Links(CircuitYear year) => this.year = year;

            public void Add(int from, int to, double value, string kind, float fear = -1, bool isReturn = false)
            {
                if (!(value > MinValue)) return;
                long key = ((long)from << 32) | (uint)to;
                if (isReturn) key = ~key;
                if (byPair.TryGetValue(key, out CircuitLink existing) && existing.Kind == kind)
                {
                    existing.Value += value;
                    return;
                }

                CircuitLink link = new CircuitLink
                {
                    From = from, To = to, Value = value, Kind = kind, FearShare = fear, Return = isReturn
                };
                byPair[key] = link;
                year.Links.Add(link);
            }
        }

        /// <summary>Sums every node's inflow and outflow, sets its value and the year's largest gap.</summary>
        static void Balance(CircuitYear c)
        {
            foreach (CircuitLink l in c.Links)
            {
                c.Nodes[l.From].Out += l.Value;
                c.Nodes[l.To].In += l.Value;
            }

            double worst = 0;
            foreach (CircuitNode n in c.Nodes)
            {
                n.Value = n.Column == CircuitColumn.Industries ? n.Out : Math.Max(n.In, n.Out);
                double gap = Math.Abs(n.In - n.Out);
                if (gap <= worst) continue;
                worst = gap;
                c.ImbalanceNode = n.Id;
            }

            c.Imbalance = c.Gdp > 0 ? worst / c.Gdp : 0;
        }

        // ------------------------------------------------------------------ texts

        void Describe(CircuitYear c, Flows f)
        {
            int y = c.Year;
            string ys = y.ToString(Ci);
            double gdp = Math.Max(f.Gdp, 1e-9);
            int tiers = data.Tiers.Count, groups = cal.Groups.Length, cats = CircuitCalibration.SpendingCategories;

            for (int t = 0; t < tiers; t++)
            {
                CircuitNode n = c.Nodes[tierNode[t]];
                Tier tier = data.Tiers[t];
                double va = f.TierVa[t];
                StringBuilder s = new StringBuilder();
                s.Append(n.Name).Append(": ").Append(Money(va)).Append(" of value added in ").Append(ys).Append(", ")
                    .Append(Pct(va / gdp)).Append(" of GDP. Of each dollar, wages take ").Append(Pct(Div(f.TierWages[t], va)))
                    .Append(", owners keep ").Append(Pct(Div(f.TierOwners[t], va))).Append(", taxes on production ")
                    .Append(Pct(Div(f.TierTaxes[t], va))).Append(" and depreciation ").Append(Pct(Div(f.TierDepreciation[t], va)))
                    .Append(". ").Append(IndustryList(t)).Append(" Spending returns ").Append(Money(n.In)).Append(" to it.");
                n.Blurb = s.ToString();
            }

            Node(c, wagesNode).Blurb =
                $"Wages: {Money(f.Wages)} in {ys}, {Pct(f.Wages / gdp)} of GDP (pay and benefits). {GroupSplit(f.GWages, f.Wages)} " +
                $"{Money(Sum(f.GTaxOnWages))} of it is withheld as income and payroll taxes.";
            Node(c, ownersNode).Blurb =
                $"Owners' share: {Money(f.Owners)} in {ys}, {Pct(f.Owners / gdp)} of GDP: corporate profits {Money(f.Corporate)}, " +
                $"proprietors' income {Money(f.Proprietors)}, rent {Money(f.Rental)}, net interest {Money(f.NetInterest)}. " +
                $"Corporations pay {Money(f.CorporateTaxes)} in profit tax, pay out {Money(f.Payouts)} as dividends" +
                (f.Buybacks > 0
                    ? $" and buybacks ({Money(f.PayoutsAbroad)} to owners abroad)"
                    : $" ({Money(f.PayoutsAbroad)} abroad)") +
                $" and keep {Money(f.Retained)}.";
            Node(c, taxesNode).Blurb =
                $"Taxes on production: {Money(f.ProductionTaxes)} in {ys}, {Pct(f.ProductionTaxes / gdp)} of GDP: sales, excise and " +
                "property taxes and customs duties, less subsidies, paid by the industries to the state.";
            Node(c, depreciationNode).Blurb =
                $"Depreciation: {Money(f.Depreciation)} in {ys}, {Pct(f.Depreciation / gdp)} of GDP: buildings, machines and " +
                $"software worn out in the year. Firms set aside {Money(f.Depreciation - f.DepreciationGov)} to replace theirs, " +
                $"the state {Money(f.DepreciationGov)}.";

            for (int g = 0; g < groups; g++)
            {
                Group grp = cal.Groups[g];
                CircuitNode n = Node(c, groupNode[g]);
                string households = y == cal.Year && grp.Households > 0
                    ? " (" + grp.Households.ToString("0.0", Ci) + " million households)"
                    : "";
                n.Blurb = $"{n.Name}{households}: {Money(f.GPretax[g])} before taxes in {ys}, {Pct(f.GPretax[g] / gdp)} of GDP: " +
                          $"wages {Money(f.GWages[g])}, dividends and buybacks {Money(f.GPayouts[g])}, business income " +
                          $"{Money(f.GBusiness[g])}, rent {Money(f.GRent[g])}, interest {Money(f.GInterest[g])}. They pay " +
                          $"{Money(f.GTax[g])} in income and payroll taxes and keep {Money(f.GAfterTax[g])}.";
                CircuitNode h = Node(c, householdNode[g]);
                double rate = Div(f.GSaving[g], f.GHousehold[g]);
                h.Blurb = $"{h.Name} after taxes and transfers: {Money(f.GHousehold[g])} in {ys} (social benefits " +
                          $"{Money(f.GTransfers[g])}, interest on public debt {Money(f.GPublicInterest[g])}). They spend " +
                          $"{Money(f.GSpend[g])} and " +
                          (f.GSaving[g] >= 0
                              ? $"save {Money(f.GSaving[g])} ({Pct(rate)})."
                              : $"borrow or sell assets for the other {Money(-f.GSaving[g])} ({Pct(-rate)} more than they have).");
            }

            Node(c, governmentNode).Blurb =
                $"Government: {Money(f.Government)} in {ys}, {Pct(f.Government / gdp)} of GDP: taxes on production " +
                $"{Money(f.ProductionTaxes)}, on profits {Money(f.CorporateTaxes)}, on income and payrolls " +
                $"{Money(Sum(f.GTax))}, other {Money(f.Other)}, and {Money(f.DepreciationGov)} of its own capital's wear. It pays " +
                $"{Money(f.Transfers)} in social benefits and {Money(f.PublicInterest)} in interest; {Money(f.Purse)} is left for " +
                "public goods.";
            Node(c, reinvestNode).Blurb =
                $"Reinvestment: {Money(f.Investment - f.LentToFirms)} in {ys} kept by firms: depreciation " +
                $"{Money(f.Depreciation - f.DepreciationGov)} and retained profits {Money(f.Retained)} (after " +
                $"{Money(f.Buybacks)} of buybacks), to replace and add buildings, machines and software.";
            Node(c, abroadNode).Blurb =
                $"Abroad: {Money(c.Nodes[abroadNode].In)} reaches the rest of the world in {ys}: {Money(f.Imports)} for imports and " +
                $"{Money(f.PayoutsAbroad)} of payouts to foreign owners ({Pct(f.ForeignShare)} of US shares)" +
                (f.SavingLentAbroad > 0 ? $", and {Money(f.SavingLentAbroad)} lent abroad" : "") +
                $". It comes back as {Money(f.Exports)} of exports and {Money(f.ForeignLending)} lent to the US.";
            Node(c, creditNode).Blurb = f.Credit > 0
                ? $"Credit: {Money(f.Credit)} in {ys} lent to households that spend more than they have " +
                  $"({DissaverList(f)}), out of the savings of others."
                : $"Credit: in {ys} every group saves something; no group spends more than it has.";
            Node(c, purseNode).Blurb =
                $"Public purse: {Money(f.Purse)} of taxes left in {ys} after social benefits and interest" +
                (f.Repay > 0 ? $"; {Money(f.Repay)} more than the state buys repays its debt." : ", for public goods.");
            Node(c, borrowingNode).Blurb =
                $"Borrowing: in {ys} the state borrows {Money(f.Borrow)} (its deficit, {Pct(f.Borrow / gdp)} of GDP) and firms " +
                $"{Money(f.LentToFirms)}, from {Money(f.SavingToBorrowing)} of household savings and {Money(f.ForeignLending)} " +
                "lent from abroad, through banks, funds and bond markets." +
                (c.Nodes[borrowingNode].Out > c.Nodes[borrowingNode].In * 1.001
                    ? $" The other {Money(c.Nodes[borrowingNode].Out - c.Nodes[borrowingNode].In)} comes from firms' own " +
                      "savings and the Federal Reserve, which the circuit does not draw."
                    : "");

            double spend = Math.Max(f.Spending, 1e-9);
            for (int k = 0; k < cats; k++)
            {
                Category cat = data.Categories[k];
                CircuitNode n = Node(c, categoryNode[k]);
                int top = groups - 1;
                double topShare = f.GSpend[top] > 0 ? f.Mix[top, k] : 0, bottomShare = f.Mix[0, k];
                n.Blurb = $"{n.Name}: {Money(f.Category[k])} in {ys}, {Pct(f.Category[k] / spend)} of household spending" +
                          $" ({Pct(cat.FearShare)} fear, {Pct(1 - cat.FearShare)} desire). " +
                          (string.IsNullOrEmpty(cat.Blurb) ? "" : cat.Blurb + " ") +
                          $"{cal.Groups[0].Name} spend {Pct(bottomShare)} of their budget on it, {cal.Groups[top].Name} " +
                          $"{Pct(topShare)}. {Pct(Div(f.CategoryImports[k], f.Category[k]))} buys imports.";
            }

            Node(c, savingNode).Blurb =
                $"Saving: {Money(f.PositiveSaving)} saved in {ys} by the groups that spend less than they have" +
                (f.Repay > 0 ? $", plus {Money(f.Repay)} of public debt repaid" : "") +
                $". {Money(f.Credit)} is lent to households that spend more; household saving is " +
                $"{Money(f.Disposable - f.Spending)}, " +
                $"{Pct(f.SavingRate)} of disposable income.";
            Node(c, publicNode).Blurb =
                $"Public goods: {Money(f.Purchases)} of government purchases in {ys}, {Pct(f.Purchases / gdp)} of GDP: schools, " +
                $"defense, roads, police, courts and the state's own work; {Money(f.Borrow)} of it borrowed.";
            Node(c, investmentNode).Blurb =
                $"Investment: {Money(f.Investment)} in {ys}, {Pct(f.Investment / gdp)} of GDP: new buildings, machines and " +
                $"software, paid from firms' own reinvestment and {Money(f.LentToFirms)} of loans.";
            Node(c, exportsNode).Blurb =
                $"Exports: {Money(f.Exports)} of US products bought by the rest of the world in {ys}, " +
                $"{Pct(f.Exports / gdp)} of GDP, " +
                "with the dollars it earned selling imports and from US owners' payouts.";

            c.Notes = Notes(c, f);
        }

        string Notes(CircuitYear c, Flows f)
        {
            StringBuilder s = new StringBuilder();
            int y = c.Year;
            double gdp = Math.Max(f.Gdp, 1e-9);
            s.Append(y.ToString(Ci)).Append(": GDP ").Append(Money(f.Gdp))
                .Append(" (the industries' value added, BEA GDP by industry");
            if (y == cal.Year)
            {
                double update = cal.File.IncomeOf("gdpAnnualUpdate");
                if (update > 0) s.Append("; BEA's September 2026 update puts it at ").Append(Money(update));
            }

            s.Append(").\n");
            string calYear = cal.Year.ToString(Ci);
            if (y == cal.Year)
            {
                s.Append("Calibration year (circuit.json): national income, personal income and the wealth groups' " +
                         "shares as published.\n");
            }
            else if (y > cal.Year)
            {
                s.Append($"Estimate: the industries' value added for {y.ToString(Ci)} with every other share of {calYear} " +
                         "(the accounts end there), except the spending history's saving rate.\n");
            }
            else
            {
                s.Append($"Carried from {calYear}: income by type and personal income as BEA reports them for the year; the " +
                         $"groups' shares of wages, taxes, social benefits and spending held at their {calYear} values; their " +
                         "shares of business, real estate and deposits moved with their share of net worth, of equity with the " +
                         "Fed's accounts.\n");
            }
            s.Append("Wages: industry shares scaled by the labor share, then x").Append(f.WageFactor.ToString("0.000", Ci))
                .Append(" to match BEA compensation. Net buybacks: the 2025 share of GDP (")
                .Append(Pct(cal.BuybackShare)).Append(") from 1985, none before 1982; larger in 1985-2019 in reality.\n");
            if (y < cal.TaxDataYear)
            {
                s.Append("Personal taxes before ").Append(cal.TaxDataYear.ToString(Ci))
                    .Append(": what matches BEA's disposable income, spliced (x").Append(cal.TaxSplice.ToString("0.00", Ci))
                    .Append("); social benefits from an approximate history.\n");
            }

            s.Append("Approximate history (CircuitHistory): government purchases, federal interest, exports and imports, " +
                     "the foreign share of equity");
            s.Append(y < cal.Year
                ? ".\n"
                : $" ({calYear}: the data's {Pct(cal.File.ForeignEquityShare)}; ~22% of US-issued equity by another reading).\n");
            s.Append("Household disposable income is ").Append(Pct(Math.Abs(1 - c.DisposableRatio)))
                .Append(c.DisposableRatio < 1 ? " below" : " above")
                .Append(" BEA's: interest paid in kind by banks and insurers and income from abroad are not drawn. Saving rate ")
                .Append(Pct(f.SavingRate))
                .Append(y == cal.Year ? " (pre-update vintage; 5.4% after BEA's 2026 update)." : ".").Append('\n');
            CircuitNode worst = c.Node(c.ImbalanceNode);
            if (worst != null)
            {
                s.Append("Largest gap: ").Append(worst.Name).Append(' ')
                    .Append((worst.In >= worst.Out ? "+" : "-") + Pct(Math.Abs(worst.In - worst.Out) / gdp))
                    .Append(" of GDP (").Append(GapReason(worst)).Append(").");
            }

            return s.ToString();
        }

        string GapReason(CircuitNode n)
        {
            switch (n.Kind)
            {
                case "borrowing":
                    return "the deficit and loans exceed what households and the world lend in the circuit; firms' own savings and " +
                           "the Federal Reserve cover the rest";
                case "tier":
                    return n.In > n.Out
                        ? "spending's supply chains send it more than it adds, by the 2024 input-output structure tilted to the year"
                        : "it adds more than spending's supply chains send it, by the 2024 input-output structure tilted to the year";
                default:
                    return "rounding of the routing shares";
            }
        }

        string IndustryList(int tier)
        {
            StringBuilder s = new StringBuilder();
            foreach (Industry i in data.Industries)
            {
                if (i.TierIndex != tier) continue;
                if (s.Length > 0) s.Append(", ");
                s.Append(i.Name);
            }

            return s.Length > 0 ? s.Append('.').ToString() : "";
        }

        string GroupSplit(double[] parts, double total)
        {
            StringBuilder s = new StringBuilder();
            for (int g = 0; g < parts.Length; g++)
            {
                s.Append(g == 0 ? "" : g == parts.Length - 1 ? " and " : ", ");
                s.Append(cal.Groups[g].Name).Append(' ').Append(Pct(Div(parts[g], total)));
            }

            return s.Append('.').ToString();
        }

        string DissaverList(Flows f)
        {
            StringBuilder s = new StringBuilder();
            for (int g = 0; g < f.GSaving.Length; g++)
            {
                if (f.GSaving[g] >= 0) continue;
                if (s.Length > 0) s.Append(", ");
                s.Append(cal.Groups[g].Name).Append(' ').Append(Money(-f.GSaving[g]));
            }

            return s.ToString();
        }

        static CircuitNode Node(CircuitYear c, int index) => c.Nodes[index];

        static double Div(double a, double b) => b != 0 ? a / b : 0;

        static double Sum(double[] values)
        {
            double s = 0;
            foreach (double v in values) s += v;
            return s;
        }

        /// <summary>$B as "$480B", "$9.6B", "$2.94T" or "$15.7T".</summary>
        public static string Money(double billions)
        {
            double a = Math.Abs(billions);
            if (a >= 1000) return "$" + (billions / 1000).ToString(a >= 10000 ? "0.0" : "0.00", Ci) + "T";
            if (a >= 10) return "$" + billions.ToString("0", Ci) + "B";
            return "$" + billions.ToString("0.0", Ci) + "B";
        }

        /// <summary>A share as "51%" (from 10%) or "6.9%".</summary>
        public static string Pct(double share)
        {
            double p = 100 * share;
            return p.ToString(Math.Abs(p) >= 9.95 ? "0" : "0.0", Ci) + "%";
        }
    }
}
