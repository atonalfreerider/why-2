using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Why.Economy.Land;
using Debug = UnityEngine.Debug;

namespace Why.Economy.Model
{
    /// <summary>
    /// The one-year view's model at load (M1; WP10): builds <see cref="InstantModel"/> on the economy's data and the finished
    /// lives, builds the opening year and the replay's examples 1990 and 1972 on this worker, builds the opening year a second
    /// time to show that two builds agree, and shares the model (<see cref="InstantModel.SharedKey"/>) for the layers that
    /// will draw it. Upload prints "[Why] Instant 2025: ..." with every figure of WP10's acceptance, logs each year's problems
    /// as errors (and as warnings what holds with little room: nodes near the tolerance, the tier node's value added against
    /// its flows) and publishes the opening year's facts (instant.*): on the main thread, where the smoke test counts errors
    /// (Unity passes a worker's log messages only to threaded listeners). It also splits a household dollar with the capture
    /// view's own ledger over the land's first snapshot, so the 100-cent bar and the hills cannot disagree. Order 47: tier 4,
    /// after the population (30) that runs the lives, beside the land's model (45); nothing here waits on the land. Draws
    /// nothing.
    /// </summary>
    [GraphScenes(GraphScene.Economy)]
    public sealed class InstantModelLayer : GraphLayer
    {
        /// <summary>Years built at load besides the opening one: before the DFA and BEA's distribution (1972), and with the DFA
        /// but not the distribution (1990).</summary>
        static readonly int[] CheckYears = { 1990, 1972 };

        /// <summary>The household dollar may differ from the capture view's ledger by this much (points of a share).</summary>
        const double LedgerTolerance = 0.0005;

        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        public override int Order => 47;

        public override IEnumerable<string> RequiredTexts => InstantModel.Paths;

        InstantModel model;
        readonly List<InstantYear> years = new List<InstantYear>();
        InstantYear again;
        double prepareMs;

        public override void Prepare(GraphContext ctx)
        {
            Stopwatch sw = Stopwatch.StartNew();
            EconomyModel economy = ctx.Shared<EconomyModel>(EconomyModel.SharedKey);
            if (economy == null || economy.Data.Industries.Count == 0)
            {
                Debug.LogWarning("[Why] InstantModelLayer: no economy model, the one-year view's model is skipped");
                return;
            }

            model = new InstantModel(economy.Data, economy.Lives, ctx.Text);
            // the land's opening year (read the same way as LandModelLayer, on a worker before EconomyState.Reset), then the
            // circuit's year if it differs (the acceptance's 2025), then the examples
            int open = EconomyState.DefaultYear(Math.Min(EconomyState.MaxYear, Math.Max(economy.Data.LastYear, EconomyState.MinYear)));
            years.Add(model.Year(open));
            int circuitYear = economy.Data.Circuit?.Year ?? open;
            if (circuitYear != open) years.Add(model.Year(circuitYear));
            foreach (int y in CheckYears)
            {
                if (y != open && y != circuitYear) years.Add(model.Year(y));
            }

            again = model.Rebuild(open);
            ctx.Share(InstantModel.SharedKey, model);
            prepareMs = sw.Elapsed.TotalMilliseconds;
        }

        public override void Upload(GraphContext ctx)
        {
            if (model == null || years.Count == 0) return;
            int problems = 0;
            foreach (InstantYear y in years)
            {
                string tag = "[Why] Instant " + y.Year.ToString(Ci);
                Debug.Log(tag + ": " + InstantModel.Line(y));
                Debug.Log(tag + " flows: " + InstantModel.FlowsLine(y));
                Debug.Log(tag + " coverage: " + InstantModel.CoverageLine(y));
                foreach (string p in y.Problems) Debug.LogError("[Why] Instant check failed: " + p);
                Debug.Log(tag + " checks: " + (y.Problems.Count == 0 ? "InstantModel.Problems() empty" : y.Problems.Count.ToString(Ci) + " problems"));
                problems += y.Problems.Count;
                Margins(tag, y);
            }

            InstantYear first = years[0];
            if (again != null && again.Checksum == first.Checksum)
            {
                Debug.Log("[Why] Instant " + first.Year.ToString(Ci) + ": two builds identical (checksum " + first.Checksum + ")");
            }
            else
            {
                Debug.LogError("[Why] Instant " + first.Year.ToString(Ci) + ": two builds differ (checksums " + first.Checksum + " and " +
                               (again?.Checksum ?? "none") + ")");
            }

            Ledger(first);
            int refused = InstantModel.Publish(first);
            Debug.Log("[Why] Instant facts: " + (first.Facts.Count - refused).ToString(Ci) + " published under instant.* for " +
                      first.Year.ToString(Ci) + (refused > 0 ? ", " + refused.ToString(Ci) + " refused (see ModelFacts)" : ""));

            string builds = "";
            foreach (InstantYear y in years) builds += (builds.Length > 0 ? ", " : "") + y.Year.ToString(Ci) + " " + y.BuildMs.ToString("0.0", Ci) + " ms";
            Debug.Log("[Why] InstantModelLayer.Prepare " + prepareMs.ToString("0", Ci) + " ms: files read in " + model.ParseMs.ToString("0", Ci) +
                      " ms; builds " + builds + " (budget " + InstantModel.BuildBudgetMs.ToString("0", Ci) + " ms a year); checks " +
                      (problems == 0 ? "PASS" : problems.ToString(Ci) + " FAILED"));
            foreach (InstantYear y in years)
            {
                if (y.BuildMs > InstantModel.BuildBudgetMs)
                {
                    Debug.LogWarning("[Why] Instant " + y.Year.ToString(Ci) + " took " + y.BuildMs.ToString("0.0", Ci) + " ms to build, over the " +
                                     InstantModel.BuildBudgetMs.ToString("0", Ci) + " ms budget");
                }
            }
        }

        /// <summary>
        /// Warnings, not errors, for what holds today but has little room: a node balancing within the tolerance but past
        /// <see cref="InstantModel.NodeMargin"/> (a data refresh could push it over and draw an unexplained "other" stub). Also
        /// logs the tier node's value added against its gross flows, which spec 5.10 reports beside the node rather than
        /// checks (MODALITIES-PLAN decision 35).
        /// </summary>
        static void Margins(string tag, InstantYear y)
        {
            foreach (InstantNode n in y.Nodes)
            {
                if (!n.Checked || n.Other != null || n.Gap <= InstantModel.NodeMargin) continue;
                Debug.LogWarning(tag + ": node '" + n.Id + "' balances within " + (100 * n.Gap).ToString("0.000", Ci) + "%, near the " +
                                 (100 * InstantModel.NodeTolerance).ToString("0", Ci) + "% past which an \"other\" stub is drawn");
            }

            if (double.IsNaN(y.TierIncomeGap) || Math.Abs(y.TierIncomeGap) <= InstantModel.NodeTolerance &&
                Math.Abs(y.TierDemandGap) <= InstantModel.NodeTolerance) return;
            Debug.Log(tag + ": the tier node's value added, out $" + (y.TierIncome / 1000).ToString("0.00", Ci) + "T and in $" +
                             (y.TierDemand / 1000).ToString("0.00", Ci) + "T less imports, is " + (100 * y.TierIncomeGap).ToString("+0.0;-0.0", Ci) +
                             "%/" + (100 * y.TierDemandGap).ToString("+0.0;-0.0", Ci) + "% on the market tiers' $" +
                             (y.MarketValueAdded / 1000).ToString("0.00", Ci) + "T, more than " +
                             (100 * InstantModel.NodeTolerance).ToString("0", Ci) + "%: the ribbons are gross flows (reported, not checked: decision 35)");
        }

        /// <summary>
        /// The household dollar against the capture view's ledger (<see cref="ValueCapture.Account"/>, as HillCaptureLayer
        /// builds it) on the land's first snapshot: the model weighs the categories by every simulated household's spending,
        /// the ledger by the census's players, who are every adult line, so their shares must agree.
        /// </summary>
        void Ledger(InstantYear y)
        {
            LandSnapshot s = LandService.Current;
            if (s?.Players?.Players == null || s.Year != y.FlowYear) return;
            ValueCapture.Ledger led = model.Capture.Account(s);
            int[] order = { (int)ValueCapture.Part.Wages, (int)ValueCapture.Part.Owners, (int)ValueCapture.Part.Upkeep,
                (int)ValueCapture.Part.Imports, (int)ValueCapture.Part.Taxes };
            double sum = 0;
            foreach (double v in led.Total) sum += Math.Max(0, v);
            if (sum <= 0) return;
            double gap = 0;
            double[] shares = new double[order.Length];
            string text = "";
            for (int k = 0; k < order.Length; k++)
            {
                shares[k] = Math.Max(0, led.Total[order[k]]) / sum;
                gap = Math.Max(gap, Math.Abs(shares[k] - y.Dollar.Shares[k]));
                text += (k > 0 ? "/" : "") + (100 * shares[k]).ToString("0.00", Ci);
            }

            // the bar prints whole cents: they must be the same cents the ledger gives
            int[] cents = InstantModel.WholeCents(shares);
            bool same = true;
            for (int k = 0; k < cents.Length; k++) same &= cents[k] == y.Dollar.Cents[k];
            string line = "[Why] Instant " + y.Year.ToString(Ci) + ": the household dollar against the capture view's ledger on the land's " +
                          s.Year.ToString(Ci) + " players (" + s.Players.Players.Length.ToString(Ci) + "): " + text + " = " +
                          string.Join("/", cents) + " cents (pay, owners, upkeep, imports, taxes), largest gap " +
                          (100 * gap).ToString("0.000", Ci) + " pt";
            if (gap <= LedgerTolerance && same) Debug.Log(line + "; the model's bar is the same");
            else Debug.LogError(line + "; the model's bar reads " + string.Join("/", y.Dollar.Cents) + " (gap limit " +
                                (100 * LedgerTolerance).ToString("0.00", Ci) + " pt)");
        }
    }
}
