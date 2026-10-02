using System;
using System.Collections.Generic;
using Why.Economy.Data;

namespace Why.Economy.Land
{
    /// <summary>
    /// Where household spending lands first (SPEC 4.2): the direct seller of each category's dollars, not the value
    /// added along the supply chain (the roots carry that). spending.json gives each item's value-added landing; the
    /// prior inverts the Leontief attribution on the 2024 Use table, d = clip((I − A)(w ⊘ v), 0) (A = F / output,
    /// v = VA / output), so an item's dollars reach the industries that sell it; the RAS then balances the six categories'
    /// domestic totals against BEA PCE by commodity (circuit.json finalDemand[k].pce), so every seller receives what
    /// households pay it. Construction has no PCE (homes are investment): no river ends there. Reproduces the prototype
    /// synth/sellers.py. The prior is computed once per data set; the RAS per year (it depends on the players' category
    /// totals). Pure and deterministic; any thread.
    /// </summary>
    public static class SellerMatrix
    {
        /// <summary>The six spending categories (EconomyData.CategoryIds 0-5) and the 25 industries.</summary>
        public const int Categories = 6;

        /// <summary>The RAS stops when every row and live column is within this relative error, or after this many sweeps.</summary>
        public const double RasTolerance = 1e-3;

        public const int RasMaxIterations = 50;

        /// <summary>The floor that keeps every live seller reachable from every category (share of the outer product).</summary>
        public const double RasFloor = 0.01;

        /// <summary>Column factors (RAS / prior) outside this band are logged.</summary>
        public const double FlagLow = 0.5, FlagHigh = 2.0;

        /// <summary>The prior of a data set (computed once).</summary>
        public sealed class Prior
        {
            /// <summary>C0[c, k]: domestic dollars of category c first paid to industry k ($B, the file year's PCE).</summary>
            public double[,] C0;

            /// <summary>Each category's import share (imports / PCE of its items), its domestic and import PCE.</summary>
            public double[] ImportShare, Domestic, Abroad;

            /// <summary>BEA 2024 PCE by commodity ($B; 0 for construction).</summary>
            public double[] Pce;

            /// <summary>Share of the inverted mass clipped to 0 (negative entries of (I − A)(w ⊘ v)).</summary>
            public double Clipped;
        }

        /// <summary>One year's balanced matrix.</summary>
        public sealed class Result
        {
            /// <summary>C[c, k]: the share of category c's domestic dollars first paid to industry k (rows sum to 1).</summary>
            public double[,] Shares;

            public int Iterations;
            public double Error;

            /// <summary>Live columns whose factor (RAS / prior, both normalized) lies outside [0.5, 2]: (industry, factor).</summary>
            public readonly List<(int industry, double factor)> Flagged = new List<(int, double)>();
        }

        static readonly object Gate = new object();
        static EconomyData cachedFor;
        static Prior cached;

        /// <summary>The prior for a data set: computed on first use, then cached.</summary>
        public static Prior PriorOf(EconomyData data)
        {
            lock (Gate)
            {
                if (ReferenceEquals(cachedFor, data) && cached != null) return cached;
            }

            Prior p = BuildPrior(data);
            lock (Gate)
            {
                cachedFor = data;
                cached = p;
            }

            return p;
        }

        /// <summary>The category index (0-5, EconomyData.CategoryIds order) of a spending.json category, or -1 (saving).</summary>
        public static int CategoryIndex(Category c)
        {
            for (int k = 0; k < Categories; k++)
            {
                if (EconomyData.CategoryIds[k] == c.Id) return k;
            }

            return -1;
        }

        static Prior BuildPrior(EconomyData data)
        {
            int n = data.Industries.Count;
            Prior p = new Prior
            {
                C0 = new double[Categories, n], ImportShare = new double[Categories], Domestic = new double[Categories],
                Abroad = new double[Categories], Pce = new double[n]
            };

            // the 2024 Use table: A[i][j] = F[i][j] / output_j, v = VA / output (own-industry flows included, as the prototype)
            double[] output = new double[n], vr = new double[n];
            double[,] a = new double[n, n];
            Dictionary<string, Dictionary<string, double>> acc = data.Circuit?.Io?.Accounts;
            for (int i = 0; i < n; i++)
            {
                string id = data.Industries[i].Id;
                double o = 0, va = 0;
                if (acc != null && acc.TryGetValue(id, out Dictionary<string, double> d) && d != null)
                {
                    d.TryGetValue("output", out o);
                    d.TryGetValue("valueAdded", out va);
                }

                output[i] = o;
                vr[i] = o > 0 ? va / o : 1;
                p.Pce[i] = FinalDemand(data, id, "pce");
            }

            if (data.Circuit?.Io != null)
            {
                foreach ((string from, string to, double value) in data.Circuit.Io.Flows())
                {
                    Industry fi = data.IndustryById(from), ti = data.IndustryById(to);
                    if (fi == null || ti == null || output[ti.Index] <= 0) continue;
                    a[fi.Index, ti.Index] += value / output[ti.Index];
                }
            }

            double clipped = 0, kept = 0;
            double[] w = new double[n], x = new double[n], dd = new double[n];
            foreach (Category cat in data.Categories)
            {
                int c = CategoryIndex(cat);
                if (c < 0 || cat.Items == null) continue;
                double pce = 0, abroad = 0;
                foreach (SpendingItem item in cat.Items)
                {
                    pce += item.Pce;
                    abroad += item.Pce * item.ImportShare;
                    if (item.Industries == null) continue;
                    double sw = 0;
                    for (int i = 0; i < n; i++)
                    {
                        w[i] = item.Industries.TryGetValue(data.Industries[i].Id, out double v) ? v : 0;
                        sw += w[i];
                    }

                    if (sw <= 0) continue;
                    for (int i = 0; i < n; i++) x[i] = w[i] / sw / vr[i];
                    double pos = 0, neg = 0;
                    for (int i = 0; i < n; i++)
                    {
                        double s = x[i];
                        for (int j = 0; j < n; j++) s -= a[i, j] * x[j];
                        dd[i] = s;
                        if (s > 0) pos += s;
                        else neg -= s;
                    }

                    clipped += neg * item.Pce;
                    kept += pos * item.Pce;
                    if (pos <= 0) continue;
                    double dom = item.Pce * (1 - item.ImportShare);
                    for (int i = 0; i < n; i++) p.C0[c, i] += dd[i] > 0 ? dom * dd[i] / pos : 0;
                }

                p.Abroad[c] = abroad;
                p.Domestic[c] = pce - abroad;
                p.ImportShare[c] = pce > 0 ? abroad / pce : 0;
            }

            p.Clipped = clipped + kept > 0 ? clipped / (clipped + kept) : 0;
            return p;
        }

        static double FinalDemand(EconomyData data, string id, string key) =>
            data.Circuit?.FinalDemand != null && data.Circuit.FinalDemand.TryGetValue(id, out Dictionary<string, double> d) &&
            d != null && d.TryGetValue(key, out double v) ? v : 0;

        /// <summary>
        /// The RAS of a year: rows = the categories' domestic dollars (the players' category totals × (1 − import share));
        /// columns = BEA PCE by commodity scaled to the rows' sum (0-PCE columns stay 0). X = (C0 + 0.01 · rows/Σrows ⊗
        /// cols) on the live columns; scale rows to their targets, then live columns, until both are within 0.1% (≤ 50
        /// sweeps), then rows once more; C = X row-normalized. Logs the sweeps, the error and the flagged column factors.
        /// </summary>
        public static Result Balance(Prior prior, double[] rows)
        {
            int n = prior.Pce.Length;
            Result r = new Result { Shares = new double[Categories, n] };
            double rowSum = 0, pceSum = 0, c0Sum = 0;
            for (int c = 0; c < Categories; c++) rowSum += Math.Max(0, rows[c]);
            for (int k = 0; k < n; k++) pceSum += Math.Max(0, prior.Pce[k]);
            if (rowSum <= 0 || pceSum <= 0) return r;
            double[] cols = new double[n];
            bool[] live = new bool[n];
            for (int k = 0; k < n; k++)
            {
                cols[k] = Math.Max(0, prior.Pce[k]) / pceSum * rowSum;
                live[k] = cols[k] > 0;
            }

            double[,] x = new double[Categories, n];
            for (int c = 0; c < Categories; c++)
            {
                for (int k = 0; k < n; k++)
                {
                    c0Sum += prior.C0[c, k];
                    x[c, k] = live[k] ? prior.C0[c, k] + RasFloor * Math.Max(0, rows[c]) / rowSum * cols[k] : 0;
                }
            }

            double[] rs = new double[Categories], cs = new double[n];
            double err = double.MaxValue;
            int it = 0;
            while (it < RasMaxIterations)
            {
                it++;
                ScaleRows(x, rows, n);
                for (int k = 0; k < n; k++) cs[k] = 0;
                for (int c = 0; c < Categories; c++)
                {
                    for (int k = 0; k < n; k++) cs[k] += x[c, k];
                }

                for (int k = 0; k < n; k++)
                {
                    double f = live[k] && cs[k] > 0 ? cols[k] / cs[k] : 0;
                    for (int c = 0; c < Categories; c++) x[c, k] *= f;
                }

                err = 0;
                for (int c = 0; c < Categories; c++)
                {
                    rs[c] = 0;
                    for (int k = 0; k < n; k++) rs[c] += x[c, k];
                    if (rows[c] > 0) err = Math.Max(err, Math.Abs(rs[c] / rows[c] - 1));
                }

                for (int k = 0; k < n; k++)
                {
                    if (!live[k]) continue;
                    double s = 0;
                    for (int c = 0; c < Categories; c++) s += x[c, k];
                    err = Math.Max(err, Math.Abs(s / cols[k] - 1));
                }

                if (err < RasTolerance) break;
            }

            ScaleRows(x, rows, n);
            r.Iterations = it;
            r.Error = err;
            double xSum = 0;
            for (int c = 0; c < Categories; c++)
            {
                double s = 0;
                for (int k = 0; k < n; k++) s += x[c, k];
                xSum += s;
                for (int k = 0; k < n; k++) r.Shares[c, k] = s > 0 ? x[c, k] / s : 0;
            }

            // column factors: the balanced column's share of the total against the prior's
            for (int k = 0; k < n; k++)
            {
                if (!live[k]) continue;
                double xk = 0, pk = 0;
                for (int c = 0; c < Categories; c++)
                {
                    xk += x[c, k];
                    pk += prior.C0[c, k];
                }

                double f = pk > 0 && c0Sum > 0 && xSum > 0 ? xk / xSum / (pk / c0Sum) : double.PositiveInfinity;
                if (f < FlagLow || f > FlagHigh) r.Flagged.Add((k, f));
            }

            return r;
        }

        static void ScaleRows(double[,] x, double[] rows, int n)
        {
            for (int c = 0; c < Categories; c++)
            {
                double s = 0;
                for (int k = 0; k < n; k++) s += x[c, k];
                double f = s > 0 ? Math.Max(0, rows[c]) / s : 0;
                for (int k = 0; k < n; k++) x[c, k] *= f;
            }
        }
    }
}
