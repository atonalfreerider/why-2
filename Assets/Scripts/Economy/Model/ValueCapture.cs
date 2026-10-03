using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Why.Economy.Data;
using Why.Economy.Land;

namespace Why.Economy.Model
{
    /// <summary>
    /// Where a household dollar ends up. Each spending item's purchaser-price split across industries (spending.json)
    /// is run through the Leontief inverse of the BEA 2024 summary Use table (circuit.json io), so a dollar spent at a
    /// store also pays the farm, the refinery and the truck behind it. Each industry's share of that output is split by
    /// its own 2024 accounts into wages (compensation), production taxes, upkeep (depreciation) and the owners'
    /// net operating surplus (profits, proprietors' income, rent and net interest): the value captured as capital
    /// income. Item import shares leave the country first.
    ///
    /// Jev (TypeSafe) judged each item on how it is sold (purchase-judgments.json): manufactured want, fear sold,
    /// captive buyer, habit loop. They are model judgments, weighted here by the owners' surplus each item carries.
    ///
    /// Assumptions: the 2024 table's ratios apply to every year; the kept flows (at least the table's threshold) are
    /// scaled up to each buyer's total intermediate purchases, so imported intermediate inputs are treated as domestic.
    /// </summary>
    public sealed class ValueCapture
    {
        public enum Part { Wages = 0, Taxes = 1, Upkeep = 2, Owners = 3, Imports = 4 }
        public const int Parts = 5;
        public enum Lever { Manufactured = 0, Fear = 1, Captive = 2, Habit = 3 }
        public const int Levers = 4;
        public const string JudgmentsPath = "Data/economy/purchase-judgments";
        public static readonly string[] LeverNames = { "manufactured want", "fear sold", "captive buyer", "habit loop" };

        static ValueCapture shared;
        static string sharedText;

        /// <summary>The scene's instance (main thread: reads the judgments from Resources the first time).</summary>
        public static ValueCapture Get()
        {
            if (shared != null || LandService.Model == null) return shared;
            if (sharedText == null) sharedText = Resources.Load<TextAsset>(JudgmentsPath)?.text ?? "";
            return shared = new ValueCapture(LandService.Model.Data, sharedText);
        }

        /// <summary>Lets a layer hand over the judgments text it loaded (any thread), before the first Get.</summary>
        public static void Provide(string judgments) { if (shared == null && judgments != null) sharedText = judgments; }

        public readonly int N;
        /// <summary>Total requirements: output of industry i per dollar of final demand for industry j.</summary>
        public readonly double[,] Leontief;
        /// <summary>Direct requirements (inputs from i per dollar of j's output).</summary>
        public readonly double[,] Direct;
        /// <summary>Value added per dollar of output, and each value-added part per dollar of output, by industry.</summary>
        public readonly double[] VaRatio;
        public readonly double[][] PartOfOutput;     // [industry][Part 0..3]
        /// <summary>Each industry's owners' surplus in the 2024 accounts ($B): the weight of its claims in a portfolio.</summary>
        public readonly double[] OwnersSurplus;
        /// <summary>Per spending category (the six of the players): parts of a dollar, the owners' surplus and the wages
        /// it generates in each industry, its first-seller split, and its levers (spending- and owners-weighted).</summary>
        public readonly double[][] CategoryParts = new double[6][];
        public readonly double[][] CategoryOwnersBy = new double[6][], CategoryWagesBy = new double[6][];
        public readonly double[][] CategoryUpkeepBy = new double[6][], CategoryTaxesBy = new double[6][];
        public readonly double[][] CategoryOutputBy = new double[6][], CategorySeller = new double[6][];
        public readonly double[][] CategoryLevers = new double[6][], CategoryOwnerLevers = new double[6][];
        /// <summary>Owners' surplus inside owner-occupied housing (imputed rent), by industry: the homeowner owns it.</summary>
        public readonly double[][] CategorySelfOwnedBy = new double[6][];
        public readonly double[] CategorySelfOwned = new double[6];
        /// <summary>Per industry: the levers behind the owners' surplus households' spending generates there.</summary>
        public readonly double[][] IndustryLevers;
        /// <summary>Per item (id): parts of a dollar and its judgments.</summary>
        public readonly Dictionary<string, double[]> ItemParts = new Dictionary<string, double[]>();
        public readonly Dictionary<string, Judgment> Judgments = new Dictionary<string, Judgment>();
        /// <summary>Share of owners' surplus paid abroad (foreign holders of corporate equity times the corporate part of surplus).</summary>
        public readonly double AbroadShare;
        public readonly string Model, JudgedOn;
        public bool Judged => Judgments.Count > 0;
        public double MaxInverseResidual { get; private set; }

        public struct Judgment
        {
            public double Manufactured, Fear, Captive, Habit, Confidence;
            public string Motive;
            /// <summary>The sale is decided by the seller's persuasion (a manufactured want or a sold fear).</summary>
            public double Persuaded => 1 - (1 - Manufactured) * (1 - Fear);
            /// <summary>The buyer cannot easily leave (a captive purchase or a habit loop).</summary>
            public double LockedIn => 1 - (1 - Captive) * (1 - Habit);
            public double this[int lever] => lever == 0 ? Manufactured : lever == 1 ? Fear : lever == 2 ? Captive : Habit;
        }

        public ValueCapture(EconomyData data, string judgments)
        {
            N = data.Industries.Count;
            var io = data.Circuit?.Io;
            var output = new double[N];
            VaRatio = new double[N]; PartOfOutput = new double[N][]; IndustryLevers = new double[N][]; OwnersSurplus = new double[N];
            Direct = new double[N, N];
            var keptInto = new double[N];
            if (io?.Flows() != null)
                foreach (var e in io.Flows())
                {
                    var a = data.IndustryById(e.from); var b = data.IndustryById(e.to);
                    if (a == null || b == null || e.value <= 0) continue;
                    Direct[a.Index, b.Index] += e.value; keptInto[b.Index] += e.value;
                }
            for (int j = 0; j < N; j++)
            {
                var ind = data.Industries[j];
                double outJ = 0, inter = 0, va = 0, comp = 0, tax = 0, surplus = 0;
                if (io?.Accounts != null && io.Accounts.TryGetValue(ind.Id, out var acc))
                {
                    acc.TryGetValue("output", out outJ); acc.TryGetValue("intermediate", out inter); acc.TryGetValue("valueAdded", out va);
                    acc.TryGetValue("compensation", out comp); acc.TryGetValue("productionTaxes", out tax); acc.TryGetValue("surplus", out surplus);
                }
                if (outJ <= 0) { va = 1; inter = 0; outJ = 1; comp = ind.CompShare; tax = ind.TaxShare; surplus = 1 - comp - tax; }
                output[j] = outJ;
                double scale = keptInto[j] > 0 ? inter / keptInto[j] : 0;
                for (int i = 0; i < N; i++) Direct[i, j] = Direct[i, j] * scale / outJ;
                VaRatio[j] = keptInto[j] > 0 ? va / outJ : 1;
                // Value-added split from the 2024 accounts; upkeep is the industry's depreciation share of value added.
                double t = Math.Max(0, tax), upkeep = Math.Min(Math.Max(0, surplus), ind.DepShare * va);
                double owners = Math.Max(0, surplus - upkeep);
                if (ind.TierId == "gov") { upkeep += owners; owners = 0; }   // government surplus is the wear of public capital
                double sum = Math.Max(1e-9, Math.Max(0, comp) + t + upkeep + owners);
                PartOfOutput[j] = new[] { VaRatio[j] * Math.Max(0, comp) / sum, VaRatio[j] * t / sum, VaRatio[j] * upkeep / sum, VaRatio[j] * owners / sum };
                OwnersSurplus[j] = PartOfOutput[j][3] * outJ;
            }
            // A column without kept inputs keeps no inputs: its value added is its whole output (so columns sum to one).
            Leontief = Invert(Direct, N, out double residual); MaxInverseResidual = residual;

            ParseJudgments(judgments, out Model, out JudgedOn);
            double corporate = data.Circuit?.IncomeOf("corporateProfitsDomestic") ?? 0, nos = data.Circuit?.IncomeOf("netOperatingSurplus") ?? 0;
            AbroadShare = nos > 0 ? (data.Circuit?.ForeignEquityShare ?? 0) * Math.Min(1, corporate / nos) : 0;

            var levers = new double[N, Levers]; var leverWeight = new double[N];
            for (int c = 0; c < 6; c++)
            {
                var cat = data.Spending?.Categories != null && c < data.Spending.Categories.Count ? data.Spending.Categories[c] : null;
                CategoryParts[c] = new double[Parts]; CategoryOwnersBy[c] = new double[N]; CategoryWagesBy[c] = new double[N];
                CategoryUpkeepBy[c] = new double[N]; CategoryTaxesBy[c] = new double[N]; CategoryOutputBy[c] = new double[N];
                CategorySeller[c] = new double[N]; CategoryLevers[c] = new double[Levers]; CategoryOwnerLevers[c] = new double[Levers];
                CategorySelfOwnedBy[c] = new double[N];
                if (cat?.Items == null) continue;
                double total = 0, ownersTotal = 0;
                foreach (var item in cat.Items) if (item.Pce > 0) total += item.Pce;
                if (total <= 0) continue;
                foreach (var item in cat.Items)
                {
                    if (item.Pce <= 0) continue;
                    double w = item.Pce / total;
                    var parts = Decompose(data, item, out double[] ownersBy, out double[] wagesBy, out double[] upkeepBy, out double[] taxesBy, out double[] outputBy, out double[] seller);
                    ItemParts[item.Id] = parts;
                    bool ownHome = item.Id.StartsWith("owner_rent", StringComparison.Ordinal);
                    for (int k = 0; k < Parts; k++) CategoryParts[c][k] += w * parts[k];
                    for (int j = 0; j < N; j++)
                    {
                        CategoryOwnersBy[c][j] += w * ownersBy[j]; CategoryWagesBy[c][j] += w * wagesBy[j];
                        CategoryUpkeepBy[c][j] += w * upkeepBy[j]; CategoryTaxesBy[c][j] += w * taxesBy[j];
                        CategoryOutputBy[c][j] += w * outputBy[j]; CategorySeller[c][j] += w * seller[j];
                        if (ownHome) { CategorySelfOwnedBy[c][j] += w * ownersBy[j]; CategorySelfOwned[c] += w * ownersBy[j]; }
                    }
                    Judgment g = JudgmentOf(item, cat);
                    // Levers weigh the surplus captured from others; a homeowner's imputed rent is not captured from anyone.
                    double owned = ownHome ? 0 : w * parts[(int)Part.Owners]; ownersTotal += owned;
                    for (int l = 0; l < Levers; l++) { CategoryLevers[c][l] += w * g[l]; CategoryOwnerLevers[c][l] += owned * g[l]; }
                    for (int j = 0; j < N && !ownHome; j++)
                    {
                        double o = item.Pce * ownersBy[j]; leverWeight[j] += o;
                        for (int l = 0; l < Levers; l++) levers[j, l] += o * g[l];
                    }
                }
                for (int l = 0; l < Levers; l++) CategoryOwnerLevers[c][l] = ownersTotal > 0 ? CategoryOwnerLevers[c][l] / ownersTotal : CategoryLevers[c][l];
            }
            for (int j = 0; j < N; j++)
            {
                IndustryLevers[j] = new double[Levers];
                for (int l = 0; l < Levers; l++) IndustryLevers[j][l] = leverWeight[j] > 0 ? levers[j, l] / leverWeight[j] : 0;
            }
        }

        /// <summary>One item's dollar: where it is first paid, the output it requires, and the parts of its value.</summary>
        double[] Decompose(EconomyData data, SpendingItem item, out double[] ownersBy, out double[] wagesBy, out double[] upkeepBy,
            out double[] taxesBy, out double[] outputBy, out double[] seller)
        {
            ownersBy = new double[N]; wagesBy = new double[N]; upkeepBy = new double[N]; taxesBy = new double[N]; outputBy = new double[N];
            seller = new double[N];
            double weights = 0;
            if (item.Industries != null) foreach (var kv in item.Industries) if (kv.Value > 0 && data.IndustryById(kv.Key) != null) weights += kv.Value;
            double domestic = 1 - Math.Max(0, Math.Min(1, item.ImportShare));
            if (weights <= 0) return new double[] { 0, 0, 0, 0, 1 };
            foreach (var kv in item.Industries)
            {
                var ind = data.IndustryById(kv.Key); if (ind == null || kv.Value <= 0) continue;
                seller[ind.Index] += domestic * kv.Value / weights;
            }
            var parts = new double[Parts];
            for (int i = 0; i < N; i++)
            {
                double x = 0; for (int j = 0; j < N; j++) x += Leontief[i, j] * seller[j];
                outputBy[i] = x;
                wagesBy[i] = x * PartOfOutput[i][0]; taxesBy[i] = x * PartOfOutput[i][1];
                upkeepBy[i] = x * PartOfOutput[i][2]; ownersBy[i] = x * PartOfOutput[i][3];
                parts[0] += wagesBy[i]; parts[1] += taxesBy[i]; parts[2] += upkeepBy[i]; parts[3] += ownersBy[i];
            }
            parts[4] = 1 - domestic;
            return parts;
        }

        Judgment JudgmentOf(SpendingItem item, Category cat)
        {
            if (Judgments.TryGetValue(item.Id, out Judgment j)) return j;
            // Without Jev's judgments: the stored category motives (fear share) and item fantasy, with no lock-in signal.
            double fantasy = item.Fantasy > 0 ? item.Fantasy : cat.Fantasy;
            return new Judgment { Manufactured = fantasy, Fear = cat.FearShare * .6, Captive = 0, Habit = 0, Motive = "", Confidence = 0 };
        }

        void ParseJudgments(string text, out string model, out string judged)
        {
            model = judged = "";
            if (string.IsNullOrWhiteSpace(text)) return;
            try
            {
                var root = JObject.Parse(text);
                model = (string)root["model"] ?? ""; judged = (string)root["judged"] ?? "";
                if (!(root["items"] is JObject items)) return;
                foreach (var p in items.Properties())
                {
                    var o = p.Value;
                    double conf = Math.Min(Math.Min((double?)o["manufacturedConfidence"] ?? 0, (double?)o["fear_soldConfidence"] ?? 0),
                        Math.Min((double?)o["captiveConfidence"] ?? 0, (double?)o["habitConfidence"] ?? 0));
                    Judgments[p.Name] = new Judgment
                    {
                        Manufactured = (double?)o["manufactured"] ?? 0, Fear = (double?)o["fear_sold"] ?? 0,
                        Captive = (double?)o["captive"] ?? 0, Habit = (double?)o["habit"] ?? 0,
                        Motive = (string)o["motive"] ?? "", Confidence = conf
                    };
                }
            }
            catch (Exception e) { Debug.LogWarning("[Why] Purchase judgments unreadable: " + e.Message); Judgments.Clear(); }
        }

        /// <summary>Owners'-surplus-weighted lever of a category (0..1), combining the two persuasion or lock-in levers.</summary>
        public double Persuaded(int c) => 1 - (1 - CategoryOwnerLevers[c][0]) * (1 - CategoryOwnerLevers[c][1]);
        /// <summary>Of a category's dollar, the owners' surplus captured by others (excluding a homeowner's own imputed rent).</summary>
        public double CapturedRate(int c) => CategoryParts[c][(int)Part.Owners] - CategorySelfOwned[c];
        public double LockedIn(int c) => 1 - (1 - CategoryOwnerLevers[c][2]) * (1 - CategoryOwnerLevers[c][3]);
        public double IndustryPersuaded(int j) => 1 - (1 - IndustryLevers[j][0]) * (1 - IndustryLevers[j][1]);
        public double IndustryLockedIn(int j) => 1 - (1 - IndustryLevers[j][2]) * (1 - IndustryLevers[j][3]);

        static double[,] Invert(double[,] a, int n, out double residual)
        {
            // (I - A)^-1 by Gauss-Jordan with partial pivoting.
            var m = new double[n, 2 * n];
            for (int i = 0; i < n; i++) { for (int j = 0; j < n; j++) m[i, j] = (i == j ? 1 : 0) - a[i, j]; m[i, n + i] = 1; }
            for (int col = 0; col < n; col++)
            {
                int pivot = col; for (int r = col + 1; r < n; r++) if (Math.Abs(m[r, col]) > Math.Abs(m[pivot, col])) pivot = r;
                if (pivot != col) for (int k = 0; k < 2 * n; k++) { double t = m[col, k]; m[col, k] = m[pivot, k]; m[pivot, k] = t; }
                double d = m[col, col]; if (Math.Abs(d) < 1e-12) d = 1e-12;
                for (int k = 0; k < 2 * n; k++) m[col, k] /= d;
                for (int r = 0; r < n; r++)
                {
                    if (r == col) continue; double f = m[r, col]; if (f == 0) continue;
                    for (int k = 0; k < 2 * n; k++) m[r, k] -= f * m[col, k];
                }
            }
            var inv = new double[n, n]; for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) inv[i, j] = m[i, n + j];
            residual = 0;
            for (int i = 0; i < n; i++) for (int j = 0; j < n; j++)
            {
                double s = 0; for (int k = 0; k < n; k++) s += ((i == k ? 1 : 0) - a[i, k]) * inv[k, j];
                residual = Math.Max(residual, Math.Abs(s - (i == j ? 1 : 0)));
            }
            return inv;
        }

        /// <summary>The year's capture between the players: who pays the owners' surplus inside their spending, who
        /// collects it, and the money each industry's market splits. Category spending comes from the scenario ledger
        /// when given, else the census.</summary>
        public Ledger Account(LandSnapshot s, PlayerMindProgram program = null)
        {
            var players = s.Players.Players; int n = players.Length;
            var L = new Ledger(n, N);
            for (int p = 0; p < n; p++)
            {
                var spend = program != null && program.Players.Length == n ? program.Players[p].Category : null;
                for (int c = 0; c < 6; c++)
                {
                    double d = Math.Max(0, spend != null ? spend[c] : players[p].Category[c]);
                    if (d <= 0) continue;
                    L.Spent[p] += d;
                    var parts = CategoryParts[c];
                    double captured = d * (parts[(int)Part.Owners] - CategorySelfOwned[c]);
                    L.Paid[p] += captured; L.SelfOwned[p] += d * CategorySelfOwned[c];
                    L.PaidPersuaded[p] += captured * Persuaded(c);
                    L.PaidLockedIn[p] += captured * LockedIn(c);
                    for (int j = 0; j < N; j++) L.SelfOwnedBy[p][j] += d * CategorySelfOwnedBy[c][j];
                    for (int k = 0; k < Parts; k++) L.Total[k] += d * parts[k];
                    for (int j = 0; j < N; j++)
                    {
                        L.Owners[j] += d * CategoryOwnersBy[c][j]; L.Wages[j] += d * CategoryWagesBy[c][j];
                        L.Taxes[j] += d * CategoryTaxesBy[c][j]; L.Upkeep[j] += d * CategoryUpkeepBy[c][j];
                        L.Output[j] += d * CategoryOutputBy[c][j]; L.Sold[j] += d * CategorySeller[c][j];
                    }
                    L.Imports += d * parts[(int)Part.Imports];
                }
            }
            // Owners' surplus goes to those with capital claims on the industry: its own business owners (BusinessBy) and
            // holders of capital income in proportion to the industry's share of all owners' surplus (a portfolio proxy).
            double surplusAll = 0;
            for (int j = 0; j < N; j++) surplusAll += OwnersSurplus[j];
            var sigma = new double[N];
            for (int j = 0; j < N; j++) sigma[j] = surplusAll > 0 ? OwnersSurplus[j] / surplusAll : 1.0 / N;
            for (int j = 0; j < N; j++)
            {
                double self = 0; for (int p = 0; p < n; p++) { self += L.SelfOwnedBy[p][j]; L.ReceivedFrom[p][j] = L.SelfOwnedBy[p][j]; }
                double pool = Math.Max(0, L.Owners[j] - self);
                double domestic = pool * (1 - AbroadShare); L.Abroad += pool - domestic;
                double claims = 0; for (int p = 0; p < n; p++) claims += Math.Max(0, players[p].BusinessBy[j]) + Math.Max(0, players[p].Capital) * sigma[j];
                double wageBase = 0; for (int p = 0; p < n; p++) wageBase += Math.Max(0, players[p].WagesBy[j]);
                double wagesAll = 0; if (wageBase <= 0) foreach (var p in players) wagesAll += Math.Max(0, p.Wages);
                for (int p = 0; p < n; p++)
                {
                    double share = claims > 0 ? (Math.Max(0, players[p].BusinessBy[j]) + Math.Max(0, players[p].Capital) * sigma[j]) / claims : 1.0 / n;
                    L.Received[p] += domestic * share; L.ReceivedFrom[p][j] += domestic * share;
                    double w = wageBase > 0 ? Math.Max(0, players[p].WagesBy[j]) / wageBase : wagesAll > 0 ? Math.Max(0, players[p].Wages) / wagesAll : 1.0 / n;
                    L.WagesIn[p] += L.Wages[j] * w; L.WagesFrom[p][j] = L.Wages[j] * w;
                }
            }
            // Household-driven purchases between industries: the money each buyer pays its suppliers (Z_ij = A_ij x_j).
            for (int i = 0; i < N; i++) for (int j = 0; j < N; j++) L.Inputs[i, j] = Direct[i, j] * L.Output[j];
            return L;
        }

        /// <summary>One accounting of household spending through the industries back to the players ($B).</summary>
        public sealed class Ledger
        {
            /// <summary>Per player: spending; owners' surplus inside it captured by others (Paid) and by the player's own
            /// home (SelfOwned); the persuaded and locked-in parts of Paid; owners' surplus collected from everyone's
            /// spending (Received, excluding its own home's); wages generated by everyone's spending (WagesIn).</summary>
            public readonly double[] Spent, Paid, SelfOwned, PaidPersuaded, PaidLockedIn, Received, WagesIn;
            /// <summary>Per player and industry: collected surplus (including the own home's), wages, own-home surplus.</summary>
            public readonly double[][] ReceivedFrom, WagesFrom, SelfOwnedBy;
            public readonly double[] Owners, Wages, Taxes, Upkeep, Output, Sold;
            public readonly double[] Total = new double[Parts];
            public readonly double[,] Inputs;
            public double Imports, Abroad;
            public Ledger(int players, int industries)
            {
                Spent = new double[players]; Paid = new double[players]; SelfOwned = new double[players]; PaidPersuaded = new double[players]; PaidLockedIn = new double[players];
                Received = new double[players]; WagesIn = new double[players];
                ReceivedFrom = new double[players][]; WagesFrom = new double[players][]; SelfOwnedBy = new double[players][];
                for (int p = 0; p < players; p++) { ReceivedFrom[p] = new double[industries]; WagesFrom[p] = new double[industries]; SelfOwnedBy[p] = new double[industries]; }
                Owners = new double[industries]; Wages = new double[industries]; Taxes = new double[industries];
                Upkeep = new double[industries]; Output = new double[industries]; Sold = new double[industries];
                Inputs = new double[industries, industries];
            }
            /// <summary>Owners' surplus collected minus owners' surplus paid inside the player's own spending.</summary>
            public double Net(int p) => Received[p] - Paid[p];
            public double OwnersTotal { get { double s = 0; foreach (double v in Owners) s += v; return s; } }
        }
    }
}
