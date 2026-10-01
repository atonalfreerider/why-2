using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Why.Economy.Data
{
    /// <summary>
    /// Everything the economy scene knows, parsed from <c>Resources/Data/economy/*.json</c> (schemas in
    /// Docs/DATA.md): the industries and their value added since 1946, the calibration of the money circuit, where
    /// households' money goes, the psychology and the games, and household history. Parsed once on a worker thread;
    /// read-only afterwards. Problems found while validating (sums that do not add up, unknown ids) are collected in
    /// <see cref="Warnings"/> instead of throwing, so the scene still loads with partial data.
    /// </summary>
    public sealed class EconomyData
    {
        public const string IndustriesPath = "Data/economy/industries";
        public const string CircuitPath = "Data/economy/circuit";
        public const string SpendingPath = "Data/economy/spending";
        public const string PsychePath = "Data/economy/psyche";
        public const string GamesPath = "Data/economy/games";
        public const string HistoryPath = "Data/economy/history";

        /// <summary>Resources paths of every economy file (a layer's RequiredTexts).</summary>
        public static readonly string[] Paths = { IndustriesPath, CircuitPath, SpendingPath, PsychePath, GamesPath, HistoryPath };

        /// <summary>The seven uses of household money, in the notebook's terms (see <see cref="Category"/>).</summary>
        public static readonly string[] CategoryIds =
            { "necessities", "escapism", "jeopardy", "status", "growth", "collective", "saving" };

        /// <summary>The four wealth groups of the Federal Reserve's distributional accounts, poorest first.</summary>
        public static readonly string[] GroupIds = { "bottom50", "next40", "next9", "top1" };

        public IndustriesFile IndustriesFile { get; private set; }
        public CircuitFile Circuit { get; private set; }
        public SpendingFile Spending { get; private set; }
        public PsycheFile Psyche { get; private set; }
        public GamesFile Games { get; private set; }
        public Dictionary<string, HistorySeries> History { get; private set; } = new Dictionary<string, HistorySeries>();

        public IReadOnlyList<Tier> Tiers => tiers;
        public IReadOnlyList<Industry> Industries => industries;
        public IReadOnlyList<Category> Categories => categories;
        public IReadOnlyList<Group> Groups => groups;

        /// <summary>Nominal GDP ($B) and the GDP price index (2025 = 100).</summary>
        public YearSeries Gdp { get; private set; } = YearSeries.Empty;

        public YearSeries Deflator { get; private set; } = YearSeries.Empty;
        public YearSeries LaborShare { get; private set; } = YearSeries.Empty;

        /// <summary>Spending shares by category over time (each point's shares sum to about 1).</summary>
        public IReadOnlyList<YearSeries> CategoryHistory => categoryHistory;

        /// <summary>Wealth group shares of net worth over time (DFA from 1989, earlier estimates).</summary>
        public IReadOnlyList<YearSeries> GroupWealthHistory => groupWealthHistory;

        /// <summary>First and last calendar year with industry data.</summary>
        public int FirstYear { get; private set; } = 1947;

        public int LastYear { get; private set; } = 2025;

        public IReadOnlyList<string> Warnings => warnings;

        readonly List<Tier> tiers = new List<Tier>();
        readonly List<Industry> industries = new List<Industry>();
        readonly List<Category> categories = new List<Category>();
        readonly List<Group> groups = new List<Group>();
        readonly List<YearSeries> categoryHistory = new List<YearSeries>();
        readonly List<YearSeries> groupWealthHistory = new List<YearSeries>();
        readonly List<string> warnings = new List<string>();
        readonly Dictionary<string, Industry> industryById = new Dictionary<string, Industry>(StringComparer.Ordinal);
        readonly Dictionary<string, Category> categoryById = new Dictionary<string, Category>(StringComparer.Ordinal);

        // ------------------------------------------------------------------ queries

        public Industry IndustryById(string id) => id != null && industryById.TryGetValue(id, out Industry i) ? i : null;

        public Category CategoryById(string id) => id != null && categoryById.TryGetValue(id, out Category c) ? c : null;

        public int CategoryIndex(string id) => Array.IndexOf(CategoryIds, id);

        /// <summary>A history series by id (empty when missing).</summary>
        public YearSeries HistorySeries(string id) =>
            id != null && History.TryGetValue(id, out HistorySeries s) && s.Series != null ? s.Series : YearSeries.Empty;

        /// <summary>Nominal dollars of a year in 2025 dollars.</summary>
        public double Real(double nominal, double year)
        {
            double d = Deflator.GrowthAt(year);
            return d > 0 ? nominal * 100.0 / d : nominal;
        }

        /// <summary>Sum of every industry's value added in a year (nominal $B).</summary>
        public double ValueAddedTotal(double year)
        {
            double sum = 0;
            foreach (Industry i in industries) sum += i.ValueAdded.GrowthAt(year);
            return sum;
        }

        // ------------------------------------------------------------------ parsing

        /// <summary>
        /// Parses every economy file. <paramref name="text"/> returns a resource's text (GraphContext.Text) or null
        /// when it is missing.
        /// </summary>
        public static EconomyData Parse(Func<string, string> text)
        {
            EconomyData d = new EconomyData();
            d.IndustriesFile = d.Read<IndustriesFile>(text, IndustriesPath) ?? new IndustriesFile();
            d.Circuit = d.Read<CircuitFile>(text, CircuitPath) ?? new CircuitFile();
            d.Spending = d.Read<SpendingFile>(text, SpendingPath) ?? new SpendingFile();
            d.Psyche = d.Read<PsycheFile>(text, PsychePath) ?? new PsycheFile();
            d.Games = d.Read<GamesFile>(text, GamesPath) ?? new GamesFile();
            HistoryFile history = d.Read<HistoryFile>(text, HistoryPath);
            d.History = history?.Series ?? new Dictionary<string, HistorySeries>();
            d.Build();
            d.Validate();
            return d;
        }

        T Read<T>(Func<string, string> text, string path) where T : class
        {
            string json = text?.Invoke(path);
            if (string.IsNullOrEmpty(json))
            {
                warnings.Add($"missing '{path}'");
                return null;
            }

            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                // a value of the wrong type skips that member instead of losing the whole file
                Error = (sender, args) =>
                {
                    if (args.ErrorContext.Error is JsonReaderException) return;
                    warnings.Add($"'{path}' at {args.ErrorContext.Path}: {args.ErrorContext.Error.Message}");
                    args.ErrorContext.Handled = true;
                }
            };

            try
            {
                return JsonConvert.DeserializeObject<T>(json.TrimStart('﻿'), settings);
            }
            catch (Exception e)
            {
                warnings.Add($"could not parse '{path}': {e.Message}");
                return null;
            }
        }

        void Build()
        {
            IndustriesFile f = IndustriesFile;
            Gdp = new YearSeries(f.Gdp);
            Deflator = new YearSeries(f.Deflator);
            LaborShare = new YearSeries(f.LaborShare);

            // tiers in their stacking order (ground first)
            if (f.Tiers != null) tiers.AddRange(f.Tiers);
            tiers.RemoveAll(t => t == null || string.IsNullOrEmpty(t.Id));
            tiers.Sort((a, b) => a.Order.CompareTo(b.Order));
            for (int i = 0; i < tiers.Count; i++) tiers[i].Index = i;

            // industries grouped by tier, in file order within a tier
            List<Industry> all = new List<Industry>();
            if (f.Industries != null) all.AddRange(f.Industries);
            all.RemoveAll(i => i == null || string.IsNullOrEmpty(i.Id));
            foreach (Tier t in tiers)
            {
                foreach (Industry i in all)
                {
                    if (i.TierId == t.Id) industries.Add(i);
                }
            }

            foreach (Industry i in all)
            {
                if (tiers.Find(t => t.Id == i.TierId) == null) warnings.Add($"industry '{i.Id}' has an unknown tier '{i.TierId}'");
            }

            double first = double.MaxValue, last = double.MinValue;
            for (int k = 0; k < industries.Count; k++)
            {
                Industry i = industries[k];
                i.Index = k;
                i.Tier = tiers.Find(t => t.Id == i.TierId);
                i.TierIndex = i.Tier?.Index ?? 0;
                i.ValueAdded = new YearSeries(i.ValueAddedPoints);
                i.Employment = new YearSeries(i.EmploymentPoints);
                i.Companies = i.Companies ?? new List<Company>();
                industryById[i.Id] = i;
                if (!i.ValueAdded.IsEmpty)
                {
                    first = Math.Min(first, i.ValueAdded.FirstYear);
                    last = Math.Max(last, i.ValueAdded.LastYear);
                }
            }

            if (first < double.MaxValue) FirstYear = (int)Math.Round(first);
            if (last > double.MinValue) LastYear = (int)Math.Round(last);

            // spending categories in the fixed order of CategoryIds
            foreach (string id in CategoryIds)
            {
                Category c = Spending.Categories?.Find(x => x != null && x.Id == id);
                if (c == null)
                {
                    warnings.Add($"spending category '{id}' missing");
                    c = new Category { Id = id, Name = id };
                }

                c.Index = categories.Count;
                c.Items = c.Items ?? new List<SpendingItem>();
                categories.Add(c);
                categoryById[id] = c;
            }

            for (int c = 0; c < CategoryIds.Length; c++)
            {
                categoryHistory.Add(new YearSeries(KeyedPoints(Spending.History, CategoryIds[c])));
            }

            // wealth groups in the fixed order of GroupIds
            foreach (string id in GroupIds)
            {
                Group g = Circuit.Groups?.Find(x => x != null && x.Id == id);
                if (g == null)
                {
                    warnings.Add($"wealth group '{id}' missing");
                    g = new Group { Id = id, Name = id };
                }

                g.Index = groups.Count;
                groups.Add(g);
                groupWealthHistory.Add(new YearSeries(KeyedPoints(Circuit.GroupHistory, id)));
            }

            foreach (KeyValuePair<string, HistorySeries> kv in History)
            {
                if (kv.Value == null) continue;
                kv.Value.Series = new YearSeries(kv.Value.Points);
            }
        }

        /// <summary>
        /// One key of rows shaped [year, {"key": value, ...}] as a series (empty when no row has it): how the files
        /// write several quantities over time (circuit.json incomeHistory, personalHistory, groupEquityHistory).
        /// </summary>
        public static YearSeries Keyed(List<JArray> rows, string key) => new YearSeries(KeyedPoints(rows, key));

        /// <summary>
        /// Points of one key from rows shaped [year, {"key": value, ...}] (how shares over time are written in the
        /// files).
        /// </summary>
        static List<double[]> KeyedPoints(List<JArray> rows, string key)
        {
            List<double[]> points = new List<double[]>();
            if (rows == null) return points;
            foreach (JArray row in rows)
            {
                if (row == null || row.Count < 2) continue;
                if (row[0].Type != JTokenType.Integer && row[0].Type != JTokenType.Float) continue;
                if (!(row[1] is JObject o) || !o.TryGetValue(key, out JToken v)) continue;
                if (v.Type != JTokenType.Integer && v.Type != JTokenType.Float) continue;
                points.Add(new[] { row[0].Value<double>(), v.Value<double>() });
            }

            return points;
        }

        void Validate()
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            if (industries.Count == 0) warnings.Add("no industries");
            if (Gdp.IsEmpty) warnings.Add("no GDP series");
            if (Deflator.IsEmpty) warnings.Add("no GDP price index (real dollars equal nominal)");

            // value added adds up to GDP
            for (int year = 1950; year <= LastYear; year += year < 2020 ? 10 : 5)
            {
                double gdp = Gdp.GrowthAt(year), va = ValueAddedTotal(year);
                if (gdp > 0 && Math.Abs(va / gdp - 1) > 0.03)
                {
                    warnings.Add($"value added {va.ToString("0", c)} vs GDP {gdp.ToString("0", c)} $B in {year} " +
                                 $"({(100 * (va / gdp - 1)).ToString("+0.0;-0.0", c)}%)");
                }
            }

            foreach (Industry i in industries)
            {
                if (i.ValueAdded.IsEmpty) warnings.Add($"industry '{i.Id}' has no value added");
                if (i.CompShare + i.TaxShare + i.DepShare > 1.05) warnings.Add($"industry '{i.Id}' shares exceed value added");
            }

            // spending items point at industries that exist and spread their money fully
            foreach (Category cat in categories)
            {
                foreach (SpendingItem item in cat.Items)
                {
                    double w = 0;
                    if (item.Industries != null)
                    {
                        foreach (KeyValuePair<string, double> kv in item.Industries)
                        {
                            if (!industryById.ContainsKey(kv.Key)) warnings.Add($"item '{item.Id}' -> unknown industry '{kv.Key}'");
                            w += kv.Value;
                        }
                    }

                    if (Math.Abs(w - 1) > 0.02) warnings.Add($"item '{item.Id}' industry weights sum to {w.ToString("0.00", c)}");
                }
            }

            // wealth group shares sum to one
            string[] shareFields = { "wage", "business", "equity", "realEstate", "interest", "transfer", "tax", "consumption" };
            foreach (string field in shareFields)
            {
                double sum = 0;
                foreach (Group g in groups) sum += g.Share(field);
                if (Math.Abs(sum - 1) > 0.03) warnings.Add($"group {field} shares sum to {sum.ToString("0.00", c)}");
            }
        }
    }

    // ---------------------------------------------------------------------- industries.json

    public sealed class IndustriesFile
    {
        [JsonProperty("vintage")] public string Vintage;
        [JsonProperty("sources")] public List<string> Sources;
        [JsonProperty("gdp")] public List<double[]> Gdp;
        [JsonProperty("deflator")] public List<double[]> Deflator;
        [JsonProperty("laborShare")] public List<double[]> LaborShare;
        [JsonProperty("tiers")] public List<Tier> Tiers;
        [JsonProperty("industries")] public List<Industry> Industries;
        [JsonProperty("overlays")] public List<Overlay> Overlays;
    }

    /// <summary>A floor of the industry wall: government, raw, make, services, tech (the notebook's corporate page).</summary>
    public sealed class Tier
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("name")] public string Name;
        [JsonProperty("order")] public int Order;
        [JsonProperty("blurb")] public string Blurb;

        [JsonIgnore] public int Index;
    }

    /// <summary>An industry of the wall, with its value added since 1946 and how it splits that value.</summary>
    public sealed class Industry
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("name")] public string Name;
        [JsonProperty("tier")] public string TierId;

        /// <summary>"matter", "life", "capital" or "gov": the hue of the band.</summary>
        [JsonProperty("level")] public string Level = "capital";

        /// <summary>The words of the notebook this industry stands for (e.g. "Oil", "Social, AI").</summary>
        [JsonProperty("notebook")] public string Notebook;

        [JsonProperty("naics")] public string Naics;
        [JsonProperty("blurb")] public string Blurb;
        [JsonProperty("valueAdded")] public List<double[]> ValueAddedPoints;

        /// <summary>Shares of value added: compensation of employees, taxes on production, depreciation (latest year).</summary>
        [JsonProperty("compShare")] public double CompShare = 0.55;

        [JsonProperty("taxShare")] public double TaxShare = 0.06;
        [JsonProperty("depShare")] public double DepShare = 0.15;

        /// <summary>Net income over revenue, typical for the industry (latest).</summary>
        [JsonProperty("profitMargin")] public double ProfitMargin;

        [JsonProperty("employment")] public List<double[]> EmploymentPoints;

        /// <summary>Corporate profits (NIPA 6.16D), latest year, $B; 0 when unknown.</summary>
        [JsonProperty("corporateProfits")] public double CorporateProfits;

        [JsonProperty("companies")] public List<Company> Companies;
        [JsonProperty("note")] public string Note;

        [JsonIgnore] public int Index, TierIndex;
        [JsonIgnore] public Tier Tier;
        [JsonIgnore] public YearSeries ValueAdded = YearSeries.Empty;
        [JsonIgnore] public YearSeries Employment = YearSeries.Empty;

        /// <summary>Share of value added left to owners after wages, production taxes and depreciation.</summary>
        [JsonIgnore] public double OwnersShare => Math.Max(0, 1 - CompShare - TaxShare - DepShare);
    }

    public sealed class Company
    {
        [JsonProperty("name")] public string Name;
        [JsonProperty("industry")] public string Industry;
        [JsonProperty("revenue")] public double Revenue;
        [JsonProperty("netIncome")] public double NetIncome;
        [JsonProperty("marketCap")] public double MarketCap;
        [JsonProperty("year")] public int Year;
        [JsonProperty("note")] public string Note;
    }

    /// <summary>A figure that is not a BEA industry (social media ad revenue, AI investment), kept for labels.</summary>
    public sealed class Overlay
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("name")] public string Name;
        [JsonProperty("blurb")] public string Blurb;
        [JsonProperty("year")] public int Year;

        /// <summary>Every other numeric field of the overlay ($B unless its name says otherwise).</summary>
        [JsonExtensionData] public IDictionary<string, JToken> Figures;

        public double Figure(string key) =>
            Figures != null && Figures.TryGetValue(key, out JToken t) &&
            (t.Type == JTokenType.Float || t.Type == JTokenType.Integer)
                ? t.Value<double>()
                : 0;
    }

    // ---------------------------------------------------------------------- circuit.json

    public sealed class CircuitFile
    {
        [JsonProperty("vintage")] public string Vintage;
        [JsonProperty("year")] public int Year = 2025;
        [JsonProperty("sources")] public List<string> Sources;

        /// <summary>National income side ($B): compensation, wages, proprietors, rental, netInterest, corporateProfits, ...</summary>
        [JsonProperty("income")] public Dictionary<string, double> Income = new Dictionary<string, double>();

        /// <summary>Personal income and outlays ($B): transfers, personalTaxes, disposableIncome, pce, saving, ...</summary>
        [JsonProperty("personal")] public Dictionary<string, double> Personal = new Dictionary<string, double>();

        /// <summary>Government receipts and outlays ($B).</summary>
        [JsonProperty("government")] public Dictionary<string, double> Government = new Dictionary<string, double>();

        [JsonProperty("groups")] public List<Group> Groups;
        [JsonProperty("foreignEquityShare")] public double ForeignEquityShare;
        [JsonProperty("pensionEquityShare")] public double PensionEquityShare;
        [JsonProperty("groupHistory")] public List<JArray> GroupHistory;
        [JsonProperty("io")] public IoTable Io;
        [JsonProperty("finalDemand")] public Dictionary<string, Dictionary<string, double>> FinalDemand;
        [JsonProperty("capture")] public List<Company> Capture;
        [JsonProperty("marketCapTotal")] public double MarketCapTotal;
        [JsonProperty("top10MarketCapShare")] public double Top10MarketCapShare;

        /// <summary>
        /// Rows [year, {"gdp", "compensation", "corporateProfits", "dividends", ...}]: GDI by type of income, $B (BEA NIPA
        /// 1.10; profits, dividends and retained earnings of domestic industries).
        /// </summary>
        [JsonProperty("incomeHistory")] public List<JArray> IncomeHistory;

        /// <summary>
        /// Rows [year, {"pce", "disposableIncome", "saving", "savingRate", "personalTaxes", "governmentSocialBenefits",
        /// ...}]: personal income and outlays, $B (BEA NIPA 2.1 / 2.6; the income detail from 1988).
        /// </summary>
        [JsonProperty("personalHistory")] public List<JArray> PersonalHistory;

        /// <summary>Rows [year, {"top1": share, ...}]: wealth groups' shares of corporate equity (DFA, from 1989).</summary>
        [JsonProperty("groupEquityHistory")] public List<JArray> GroupEquityHistory;

        public double IncomeOf(string key) => Income != null && Income.TryGetValue(key, out double v) ? v : 0;
        public double PersonalOf(string key) => Personal != null && Personal.TryGetValue(key, out double v) ? v : 0;
        public double GovernmentOf(string key) => Government != null && Government.TryGetValue(key, out double v) ? v : 0;
    }

    /// <summary>One wealth group of the distributional accounts and its shares of each flow (0..1).</summary>
    public sealed class Group
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("name")] public string Name;

        /// <summary>Households (millions) and net worth ($T).</summary>
        [JsonProperty("households")] public double Households;

        [JsonProperty("netWorth")] public double NetWorth;
        [JsonProperty("wageShare")] public double WageShare;
        [JsonProperty("businessShare")] public double BusinessShare;
        [JsonProperty("equityShare")] public double EquityShare;
        [JsonProperty("realEstateShare")] public double RealEstateShare;
        [JsonProperty("interestShare")] public double InterestShare;
        [JsonProperty("transferShare")] public double TransferShare;
        [JsonProperty("taxShare")] public double TaxShare;
        [JsonProperty("consumptionShare")] public double ConsumptionShare;
        [JsonProperty("debtShare")] public double DebtShare;

        /// <summary>Share of pension entitlements (DB + DC): how equity held through pensions is attributed.</summary>
        [JsonProperty("pensionShare")] public double PensionShare;

        /// <summary>The group's saving / disposable income in the file's year (may be negative).</summary>
        [JsonProperty("savingRate")] public double SavingRate;

        [JsonProperty("note")] public string Note;

        [JsonIgnore] public int Index;

        /// <summary>A share by field name ("wage", "equity", ...).</summary>
        public double Share(string field)
        {
            switch (field)
            {
                case "wage": return WageShare;
                case "business": return BusinessShare;
                case "equity": return EquityShare;
                case "realEstate": return RealEstateShare;
                case "interest": return InterestShare;
                case "transfer": return TransferShare;
                case "tax": return TaxShare;
                case "consumption": return ConsumptionShare;
                case "debt": return DebtShare;
                default: return 0;
            }
        }
    }

    /// <summary>Intermediate flows between industries (input-output use table, $B, supplier -> user).</summary>
    public sealed class IoTable
    {
        [JsonProperty("year")] public int Year;
        [JsonProperty("flows")] public List<JArray> FlowRows;

        /// <summary>Per industry: "output", "intermediate", "valueAdded", "compensation", ... ($B, the table's year).</summary>
        [JsonProperty("accounts")] public Dictionary<string, Dictionary<string, double>> Accounts;

        /// <summary>Flows as (supplier id, user id, $B).</summary>
        public IEnumerable<(string from, string to, double value)> Flows()
        {
            if (FlowRows == null) yield break;
            foreach (JArray r in FlowRows)
            {
                if (r == null || r.Count < 3) continue;
                if (r[2].Type != JTokenType.Float && r[2].Type != JTokenType.Integer) continue;
                yield return (r[0].Value<string>(), r[1].Value<string>(), r[2].Value<double>());
            }
        }
    }

    // ---------------------------------------------------------------------- spending.json

    public sealed class SpendingFile
    {
        [JsonProperty("vintage")] public string Vintage;
        [JsonProperty("year")] public int Year = 2025;
        [JsonProperty("sources")] public List<string> Sources;
        [JsonProperty("categories")] public List<Category> Categories;
        [JsonProperty("history")] public List<JArray> History;
        [JsonProperty("byQuintile")] public List<Quintile> ByQuintile;
        [JsonProperty("markets")] public List<Market> Markets;
    }

    /// <summary>
    /// A use of household money in the notebook's terms (necessities, escapism, jeopardy, status, growth, collective,
    /// saving), with the motives behind it and where in the mind map it sits.
    /// </summary>
    public sealed class Category
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("name")] public string Name;
        [JsonProperty("notebook")] public string Notebook;

        /// <summary>The notebook's bracket: "selfish", "mating" or "base".</summary>
        [JsonProperty("bracket")] public string Bracket;

        /// <summary>Weights of the desires (food, collective, law, shelter, sex) and fears (starvation, isolation, murder, exposure, childless) behind it.</summary>
        [JsonProperty("desire")] public Dictionary<string, double> Desire;

        [JsonProperty("fear")] public Dictionary<string, double> Fear;

        /// <summary>Share of the motive that is fear: 0 = pure desire, 1 = pure fear.</summary>
        [JsonProperty("fearShare")] public double FearShare = 0.5;

        /// <summary>Position in the mind map: x desire (-1) .. fear (+1), y emotion (0) .. reason (1), z present (0) .. future (1).</summary>
        [JsonProperty("mind")] public MindPoint Mind = new MindPoint();

        /// <summary>How much of what it buys is a fantasy (an escape, a status signal, a lottery ticket): 0..1.</summary>
        [JsonProperty("fantasy")] public double Fantasy;

        [JsonProperty("items")] public List<SpendingItem> Items;

        /// <summary>Flows that are not consumption but belong to the category (interest on consumer debt, transfers paid).</summary>
        [JsonProperty("outsidePce")] public List<SpendingItem> OutsidePce;

        [JsonProperty("blurb")] public string Blurb;
        [JsonProperty("note")] public string Note;

        [JsonIgnore] public int Index;

        /// <summary>Spending of the category in its file year ($B, sum of its items).</summary>
        public double Total()
        {
            double sum = 0;
            if (Items != null)
            {
                foreach (SpendingItem i in Items) sum += i.Pce;
            }

            return sum;
        }

        /// <summary>Share of the category's money that reaches an industry (weighted by its items).</summary>
        public double IndustryShare(string industryId)
        {
            double total = 0, part = 0;
            if (Items == null) return 0;
            foreach (SpendingItem i in Items)
            {
                total += i.Pce;
                if (i.Industries != null && i.Industries.TryGetValue(industryId, out double w)) part += i.Pce * w;
            }

            return total > 0 ? part / total : 0;
        }
    }

    public sealed class MindPoint
    {
        [JsonProperty("x")] public float X;
        [JsonProperty("y")] public float Y = 0.5f;
        [JsonProperty("z")] public float Z = 0.5f;
    }

    public sealed class SpendingItem
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("name")] public string Name;

        /// <summary>Spending in the file year, $B.</summary>
        [JsonProperty("pce")] public double Pce;

        /// <summary>Which industries receive the money (weights sum to 1).</summary>
        [JsonProperty("industries")] public Dictionary<string, double> Industries;

        /// <summary>Money outside personal consumption (interest paid, transfers paid), $B; used when there is no pce.</summary>
        [JsonProperty("usd")] public double Usd;

        /// <summary>Share of the money that buys imports (leaves the country); the industry weights cover the rest.</summary>
        [JsonProperty("importShare")] public double ImportShare;

        /// <summary>The item's money in the file year, $B (pce, or usd for flows outside consumption).</summary>
        public double Amount => Pce != 0 ? Pce : Usd;

        [JsonProperty("note")] public string Note;
    }

    public sealed class Quintile
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("incomeMean")] public double IncomeMean;
        [JsonProperty("spendingMean")] public double SpendingMean;
        [JsonProperty("shares")] public Dictionary<string, double> Shares;

        /// <summary>
        /// Shares of the quintile's spending by category rescaled to PCE (the survey misses most vice and third-party
        /// health spending); the six spending categories, without saving.
        /// </summary>
        [JsonProperty("pceBasisShares")] public Dictionary<string, double> PceBasisShares;

        public double ShareOf(string category) => Shares != null && Shares.TryGetValue(category, out double v) ? v : 0;

        public double PceBasisShareOf(string category) =>
            PceBasisShares != null && PceBasisShares.TryGetValue(category, out double v) ? v : 0;
    }

    /// <summary>A headline market (lottery, sports betting, gyms ...) for labels.</summary>
    public sealed class Market
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("name")] public string Name;
        [JsonProperty("category")] public string Category;
        [JsonProperty("usd")] public double Usd;
        [JsonProperty("year")] public int Year;
        [JsonProperty("source")] public string Source;
    }

    // ---------------------------------------------------------------------- psyche.json

    public sealed class PsycheFile
    {
        [JsonProperty("vintage")] public string Vintage;
        [JsonProperty("sources")] public List<string> Sources;
        [JsonProperty("desires")] public List<Drive> Desires = new List<Drive>();
        [JsonProperty("fears")] public List<Drive> Fears = new List<Drive>();
        [JsonProperty("chemicals")] public List<Chemical> Chemicals = new List<Chemical>();
        [JsonProperty("modes")] public List<Drive> Modes = new List<Drive>();
        [JsonProperty("os")] public JObject Os;
        [JsonProperty("signals")] public List<Drive> Signals = new List<Drive>();
        [JsonProperty("hierarchy")] public List<Drive> Hierarchy = new List<Drive>();
        [JsonProperty("biases")] public List<Bias> Biases = new List<Bias>();
        [JsonProperty("traits")] public Dictionary<string, TraitDistribution> Traits = new Dictionary<string, TraitDistribution>();

        /// <summary>Per category: log-multipliers of its share per +1 sd of a trait.</summary>
        [JsonProperty("tilts")] public Dictionary<string, Dictionary<string, double>> Tilts =
            new Dictionary<string, Dictionary<string, double>>();

        [JsonProperty("agency")] public AgencyData Agency = new AgencyData();

        /// <summary>Percentage points of saving rate per +1 sd of a trait (presentBias: per -0.1 of beta; reason: per +0.1 of share).</summary>
        [JsonProperty("savingRatePp")] public Dictionary<string, double> SavingRatePp = new Dictionary<string, double>();

        /// <summary>Saving rate shifts by age band (percentage points around the mean).</summary>
        [JsonProperty("lifeStageTilts")] public LifeStageTilts LifeStage = new LifeStageTilts();

        /// <summary>Correlations between the traits, in <see cref="TraitCorrelations.Order"/>.</summary>
        [JsonProperty("traitCorrelations")] public TraitCorrelations Correlations = new TraitCorrelations();

        /// <summary>Memory, world state and strategy: the mind's eye of the notebook (free-form).</summary>
        [JsonProperty("mindsEye")] public JToken MindsEye;

        public double SavingPp(string trait) =>
            SavingRatePp != null && SavingRatePp.TryGetValue(trait, out double v) ? v : 0;

        /// <summary>Share of decisions made by the higher OS (reason) in the population, from os.higher.share.</summary>
        public double HigherOsShare
        {
            get
            {
                JToken t = Os?.SelectToken("higher.share");
                return t != null && (t.Type == JTokenType.Float || t.Type == JTokenType.Integer) ? t.Value<double>() : 0.4;
            }
        }

        public TraitDistribution Trait(string id) =>
            Traits != null && Traits.TryGetValue(id, out TraitDistribution t) && t != null ? t : new TraitDistribution();

        public double Tilt(string category, string trait) =>
            Tilts != null && Tilts.TryGetValue(category, out Dictionary<string, double> d) && d != null &&
            d.TryGetValue(trait, out double v)
                ? v
                : 0;
    }

    /// <summary>A named element of the mind (a desire, a fear, a mode of thought, a signal, a rank).</summary>
    public sealed class Drive
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("name")] public string Name;
        [JsonProperty("blurb")] public string Blurb;
        [JsonProperty("chemicals")] public List<string> Chemicals;

        /// <summary>Share of waking time (modes) or of the population (ranks) where known; 0 otherwise.</summary>
        [JsonProperty("share")] public double Share;

        /// <summary>For desires and fears: the population's baseline weight within its pole (each pole sums to 1).</summary>
        [JsonProperty("weight")] public double Weight;

        /// <summary>For desires and fears: when in life it is strongest.</summary>
        [JsonProperty("lifeStage")] public LifeStageCurve LifeStage;

        [JsonProperty("note")] public string Note;
        [JsonProperty("source")] public string Source;
    }

    /// <summary>
    /// How strongly a drive acts over a life: 1 at the peak age, falling as a Gaussian of the given spread (years) to
    /// the floor.
    /// </summary>
    public sealed class LifeStageCurve
    {
        [JsonProperty("peakAge")] public double PeakAge = 35;
        [JsonProperty("spread")] public double Spread = 30;
        [JsonProperty("floor")] public double Floor = 0.5;

        public double At(double age)
        {
            double d = (age - PeakAge) / Math.Max(Spread, 1e-3);
            return Floor + (1 - Floor) * Math.Exp(-0.5 * d * d);
        }
    }

    public sealed class LifeStageTilts
    {
        /// <summary>Age bands like "18-24" ... "75+".</summary>
        [JsonProperty("ageBands")] public string[] AgeBands;

        [JsonProperty("savingRatePp")] public double[] SavingRatePp;
        [JsonProperty("note")] public string Note;

        /// <summary>Saving rate shift (percentage points) at an age, from the band it falls in (0 when unknown).</summary>
        public double SavingAt(double age)
        {
            if (AgeBands == null || SavingRatePp == null) return 0;
            for (int i = 0; i < AgeBands.Length && i < SavingRatePp.Length; i++)
            {
                string band = AgeBands[i];
                int dash = band.IndexOf('-');
                double lo, hi;
                if (band.EndsWith("+", StringComparison.Ordinal))
                {
                    if (!double.TryParse(band.TrimEnd('+'), NumberStyles.Float, CultureInfo.InvariantCulture, out lo)) continue;
                    hi = double.MaxValue;
                }
                else if (dash > 0 &&
                         double.TryParse(band.Substring(0, dash), NumberStyles.Float, CultureInfo.InvariantCulture, out lo) &&
                         double.TryParse(band.Substring(dash + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out hi))
                {
                    hi += 1;
                }
                else continue;

                if (age >= lo && age < hi) return SavingRatePp[i];
            }

            return 0;
        }
    }

    public sealed class TraitCorrelations
    {
        [JsonProperty("order")] public string[] Order;
        [JsonProperty("matrix")] public double[][] Matrix;
        [JsonProperty("note")] public string Note;
    }

    public sealed class Chemical
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("name")] public string Name;

        /// <summary>"desire" or "fear": the side of the mind it serves.</summary>
        [JsonProperty("pole")] public string Pole;

        [JsonProperty("function")] public string Function;
        [JsonProperty("markets")] public List<string> Markets;
        [JsonProperty("caveat")] public string Caveat;
        [JsonProperty("source")] public string Source;
    }

    public sealed class Bias
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("name")] public string Name;
        [JsonProperty("param")] public string Param;
        [JsonProperty("value")] public double Value;
        [JsonProperty("range")] public double[] Range;
        [JsonProperty("effect")] public string Effect;
        [JsonProperty("exploit")] public string Exploit;
        [JsonProperty("category")] public string Category;
        [JsonProperty("pole")] public string Pole;
        [JsonProperty("replication")] public string Replication;
        [JsonProperty("source")] public string Source;
    }

    public sealed class TraitDistribution
    {
        [JsonProperty("mean")] public double Mean;
        [JsonProperty("sd")] public double Sd = 1;

        /// <summary>Change of the mean per decade of age after 20 (in the trait's units).</summary>
        [JsonProperty("ageSlope")] public double AgeSlope;

        /// <summary>Women's mean minus men's (in the trait's units).</summary>
        [JsonProperty("sexDiff")] public double SexDiff;

        /// <summary>For yes/no traits: the population share.</summary>
        [JsonProperty("share")] public double Share;

        /// <summary>"normal" (default), "lognormal" (logMean, logSd) or "mixture" (see <see cref="Mixture"/>).</summary>
        [JsonProperty("distribution")] public string Distribution;

        [JsonProperty("median")] public double Median;
        [JsonProperty("logMean")] public double LogMean;
        [JsonProperty("logSd")] public double LogSd;

        /// <summary>[min, max] of sampled values, or null.</summary>
        [JsonProperty("clamp")] public double[] Clamp;

        /// <summary>Mixture weights and component parameters (e.g. timeConsistent, presentBiased, presentBiasedMean ...).</summary>
        [JsonProperty("mixture")] public Dictionary<string, double> Mixture;

        [JsonProperty("source")] public string Source;

        public double MixtureOf(string key, double fallback) =>
            Mixture != null && Mixture.TryGetValue(key, out double v) ? v : fallback;
    }

    public sealed class AgencyData
    {
        [JsonProperty("thresholds")] public Dictionary<string, double> Thresholds = new Dictionary<string, double>();
        [JsonProperty("targets")] public List<AgencyTarget> Targets = new List<AgencyTarget>();
        [JsonProperty("note")] public string Note;

        public double Threshold(string key, double fallback) =>
            Thresholds != null && Thresholds.TryGetValue(key, out double v) ? v : fallback;
    }

    public sealed class AgencyTarget
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("name")] public string Name;
        [JsonProperty("value")] public double Value;
        [JsonProperty("year")] public int Year;
        [JsonProperty("source")] public string Source;
    }

    // ---------------------------------------------------------------------- games.json

    public sealed class GamesFile
    {
        [JsonProperty("payoff")] public PayoffData Payoff = new PayoffData();
        [JsonProperty("strategies")] public List<StrategyInfo> Strategies = new List<StrategyInfo>();
        [JsonProperty("empirical")] public Dictionary<string, JToken> Empirical;
        [JsonProperty("tribes")] public TribesData Tribes = new TribesData();

        /// <summary>Simulation settings: noise, continuation, tribeSize, mutation, generations ...</summary>
        [JsonProperty("spec")] public Dictionary<string, double> Spec = new Dictionary<string, double>();

        [JsonProperty("sources")] public List<string> Sources;

        public double SpecOf(string key, double fallback) => Spec != null && Spec.TryGetValue(key, out double v) ? v : fallback;
    }

    public sealed class PayoffData
    {
        [JsonProperty("T")] public float T = 5;
        [JsonProperty("R")] public float R = 3;
        [JsonProperty("P")] public float P = 1;
        [JsonProperty("S")] public float S = 0;
    }

    public sealed class StrategyInfo
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("name")] public string Name;
        [JsonProperty("rule")] public string Rule;

        /// <summary>"higher" or "default": the way of deciding it stands for.</summary>
        [JsonProperty("os")] public string Os;

        /// <summary>Share of people modeled as playing it (adults), 0..1.</summary>
        [JsonProperty("populationShare")] public double PopulationShare;

        [JsonProperty("source")] public string Source;
    }

    public sealed class TribesData
    {
        /// <summary>Rows [year, {"dem": share, "rep": share, "ind": share}].</summary>
        [JsonProperty("partyId")] public List<JArray> PartyId;

        /// <summary>Share saying most people can be trusted (General Social Survey).</summary>
        [JsonProperty("trust")] public List<double[]> Trust;

        [JsonProperty("note")] public string Note;

        public YearSeries TrustSeries() => new YearSeries(Trust);
    }

    // ---------------------------------------------------------------------- history.json

    public sealed class HistoryFile
    {
        [JsonProperty("vintage")] public string Vintage;
        [JsonProperty("sources")] public List<string> Sources;
        [JsonProperty("series")] public Dictionary<string, HistorySeries> Series = new Dictionary<string, HistorySeries>();
    }

    /// <summary>One household time series of history.json (e.g. median family income, homeownership).</summary>
    public sealed class HistorySeries
    {
        [JsonProperty("unit")] public string Unit;
        [JsonProperty("points")] public List<double[]> Points;
        [JsonProperty("source")] public string Source;
        [JsonProperty("note")] public string Note;

        [JsonIgnore] public YearSeries Series = YearSeries.Empty;
    }
}
