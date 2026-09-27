using System;
using System.Collections.Generic;

namespace Why.Domination
{
    /// <summary>A piecewise-linear time series of [calendar year, value] pairs.</summary>
    public sealed class Series
    {
        readonly List<double> years = new List<double>();
        readonly List<double> values = new List<double>();

        public Series(IEnumerable<double[]> pairs)
        {
            List<double[]> sorted = new List<double[]>();
            if (pairs != null)
            {
                foreach (double[] p in pairs)
                {
                    if (p != null && p.Length >= 2 && !double.IsNaN(p[0]) && !double.IsNaN(p[1])) sorted.Add(p);
                }
            }

            sorted.Sort((a, b) => a[0].CompareTo(b[0]));
            foreach (double[] p in sorted)
            {
                years.Add(p[0]);
                values.Add(Math.Max(0, p[1]));
            }
        }

        public bool Empty => years.Count == 0;
        public double FirstYear => years.Count > 0 ? years[0] : 0;
        public double LastYear => years.Count > 0 ? years[years.Count - 1] : 0;
        public double Last => values.Count > 0 ? values[values.Count - 1] : 0;

        /// <summary>Value at a year: 0 before the series, held after it, linear in between.</summary>
        public double At(double year)
        {
            int n = years.Count;
            if (n == 0 || year < years[0]) return 0;
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

        /// <summary>Maximum value.</summary>
        public double Max()
        {
            double m = 0;
            foreach (double v in values) m = Math.Max(m, v);
            return m;
        }

        /// <summary>Sample years for geometry: every data point plus a regular grid, from start to end.</summary>
        public static List<double> SampleYears(double start, double end, IEnumerable<Series> series, double step)
        {
            SortedSet<double> set = new SortedSet<double>();
            for (double y = start; y < end; y += step) set.Add(y);
            set.Add(end);
            foreach (Series s in series)
            {
                foreach (double y in s.years)
                {
                    if (y >= start && y <= end) set.Add(y);
                }
            }

            return new List<double>(set);
        }
    }
}
