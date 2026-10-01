using System;
using System.Collections.Generic;

namespace Why.Economy.Data
{
    /// <summary>
    /// A time series of [calendar year, value] points, linear between them and held flat beyond its ends (so a
    /// model can ask for any year of the timeline). Unlike the domination layer's series it keeps negative values
    /// (net saving, net exports). Immutable after construction; safe to read from any thread.
    /// </summary>
    public sealed class YearSeries
    {
        readonly double[] years;
        readonly double[] values;

        public static readonly YearSeries Empty = new YearSeries(null);

        public YearSeries(IEnumerable<double[]> points)
        {
            List<double[]> sorted = new List<double[]>();
            if (points != null)
            {
                foreach (double[] p in points)
                {
                    if (p != null && p.Length >= 2 && !double.IsNaN(p[0]) && !double.IsNaN(p[1]) &&
                        !double.IsInfinity(p[1])) sorted.Add(p);
                }
            }

            sorted.Sort((a, b) => a[0].CompareTo(b[0]));
            years = new double[sorted.Count];
            values = new double[sorted.Count];
            for (int i = 0; i < sorted.Count; i++)
            {
                years[i] = sorted[i][0];
                values[i] = sorted[i][1];
            }
        }

        /// <summary>A series with one constant value.</summary>
        public static YearSeries Constant(double value) => new YearSeries(new[] { new[] { 2000.0, value } });

        public bool IsEmpty => years.Length == 0;
        public int Count => years.Length;
        public double FirstYear => years.Length > 0 ? years[0] : 0;
        public double LastYear => years.Length > 0 ? years[years.Length - 1] : 0;
        public double First => values.Length > 0 ? values[0] : 0;
        public double Last => values.Length > 0 ? values[values.Length - 1] : 0;
        public double YearAt(int i) => years[i];
        public double ValueAt(int i) => values[i];

        /// <summary>Value at a year: linear between points, held at the ends; 0 for an empty series.</summary>
        public double At(double year)
        {
            int n = years.Length;
            if (n == 0) return 0;
            if (year <= years[0]) return values[0];
            if (year >= years[n - 1]) return values[n - 1];
            int lo = 0, hi = n - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (years[mid] <= year) lo = mid;
                else hi = mid;
            }

            double t = (year - years[lo]) / Math.Max(years[hi] - years[lo], 1e-9);
            return values[lo] + (values[hi] - values[lo]) * t;
        }

        /// <summary>
        /// Value at a year, interpolated geometrically between positive points (for quantities that grow at a rate,
        /// such as dollars), linearly otherwise.
        /// </summary>
        public double GrowthAt(double year)
        {
            int n = years.Length;
            if (n == 0) return 0;
            if (year <= years[0]) return values[0];
            if (year >= years[n - 1]) return values[n - 1];
            int lo = 0, hi = n - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (years[mid] <= year) lo = mid;
                else hi = mid;
            }

            double a = values[lo], b = values[hi];
            double t = (year - years[lo]) / Math.Max(years[hi] - years[lo], 1e-9);
            if (a > 0 && b > 0) return a * Math.Pow(b / a, t);
            return a + (b - a) * t;
        }

        /// <summary>Largest value.</summary>
        public double Max()
        {
            double m = double.NegativeInfinity;
            foreach (double v in values) m = Math.Max(m, v);
            return values.Length > 0 ? m : 0;
        }
    }
}
