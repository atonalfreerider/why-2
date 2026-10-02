using System;
using System.Globalization;

namespace Why.Economy.Land
{
    /// <summary>
    /// How the land writes numbers and comparisons into its labels, blurbs and log lines (section 6: every sentence is
    /// generated from the year's numbers). Comparison words come from the numbers with a ±10% band for "about the same",
    /// so a tooltip can never contradict the figure it prints. Invariant culture throughout; pure, any thread.
    /// </summary>
    public static class LandFacts
    {
        /// <summary>The culture of every number in the land's text.</summary>
        public static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        /// <summary>Two numbers within this relative band read as "about the same".</summary>
        public const double SameBand = 0.10;

        /// <summary>
        /// Dollars given in $B: "$30.76T" from a trillion on (<paramref name="trillionDecimals"/> decimals), "$654B" below,
        /// "$4.3B" below ten billion; negative amounts get a leading minus.
        /// </summary>
        public static string Money(double billions, int trillionDecimals = 2)
        {
            string sign = billions < 0 ? "-" : "";
            double b = Math.Abs(billions);
            if (b >= 999.5) return sign + "$" + (b / 1000).ToString("F" + trillionDecimals, Ci) + "T";
            if (b >= 9.95) return sign + "$" + b.ToString("0", Ci) + "B";
            return sign + "$" + b.ToString("0.0", Ci) + "B";
        }

        /// <summary>A share as a percentage: 0.483 → "48%" (or with decimals: "48.3%").</summary>
        public static string Percent(double share, int decimals = 0) => (100 * share).ToString("F" + decimals, Ci) + "%";

        /// <summary>A share as a percentage number without the sign: 0.483 → "48".</summary>
        public static string Pct(double share, int decimals = 0) => (100 * share).ToString("F" + decimals, Ci);

        /// <summary>A plain number with a fixed number of decimals.</summary>
        public static string Num(double v, int decimals) => v.ToString("F" + decimals, Ci);

        /// <summary>A ratio: 9.24 → "9.2x".</summary>
        public static string Times(double ratio, int decimals = 1) => ratio.ToString("F" + decimals, Ci) + "x";

        /// <summary>A count with thousands separators: 2742 → "2,742".</summary>
        public static string Count(long n) => n.ToString("#,0", Ci);

        /// <summary>People from lines (100,000 each), in millions: "4.1M".</summary>
        public static string Millions(double people, int decimals = 1) => (people / 1e6).ToString("F" + decimals, Ci) + "M";

        /// <summary>
        /// -1 when a is clearly less than b, +1 when clearly more, 0 when they are about the same (within ±10% of the
        /// larger magnitude).
        /// </summary>
        public static int Compare(double a, double b)
        {
            double scale = Math.Max(Math.Abs(a), Math.Abs(b));
            if (scale <= 0 || Math.Abs(a - b) <= SameBand * scale) return 0;
            return a < b ? -1 : 1;
        }

        /// <summary>A comparison word chosen by the numbers (<see cref="Compare"/>).</summary>
        public static string CompareWord(double a, double b, string less = "less than", string same = "about as much as",
            string more = "more than")
        {
            int c = Compare(a, b);
            return c < 0 ? less : c > 0 ? more : same;
        }

        /// <summary>A year with the estimate mark the land carries for H1-annualized years: "2026 (estimate)".</summary>
        public static string Year(int year, bool estimate) => year.ToString(Ci) + (estimate ? " (estimate)" : "");
    }
}
