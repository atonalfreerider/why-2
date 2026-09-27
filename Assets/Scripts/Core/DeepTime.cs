using System;
using System.Globalization;

namespace Why
{
    /// <summary>
    /// The clock: maps "years ago" onto the ring arc u in [0, 1] with the super-logarithmic scale
    /// Ft(u) = u^(u^(-1.4 - 2.39u)), where yearsAgo = AgeU * Ft(u). u = 1 is the Big Bang (6 o'clock),
    /// u = 0 is the present moment (back at 6 o'clock after a full clockwise turn).
    /// Thread safe; everything is computed in double precision.
    /// </summary>
    public static class DeepTime
    {
        /// <summary>Age of the universe used for the clock layout (years).</summary>
        public const double AgeU = 1.38e10;

        public static readonly double LnAgeU = Math.Log(AgeU);

        /// <summary>Arc below which everything is "now" (well under a microsecond ago).</summary>
        public const float NowArc = 0.001f;

        const double UMin = 0.02;
        const int LutSize = 4096;
        static readonly double[] lutU = new double[LutSize];
        static readonly double[] lutG = new double[LutSize];

        static DeepTime()
        {
            for (int i = 0; i < LutSize; i++)
            {
                double u = UMin + (1.0 - UMin) * i / (LutSize - 1);
                lutU[i] = u;
                lutG[i] = G(u);
            }
        }

        /// <summary>ln Ft(u) = u^a(u) * ln u with a(u) = -1.4 - 2.39u. Monotonically increasing, 0 at u = 1.</summary>
        public static double G(double u)
        {
            double a = -1.4 - 2.39 * u;
            return Math.Pow(u, a) * Math.Log(u);
        }

        static double DG(double u)
        {
            double a = -1.4 - 2.39 * u;
            double lnU = Math.Log(u);
            double ua = Math.Pow(u, a);
            return ua * ((-2.39 * lnU + a / u) * lnU + 1.0 / u);
        }

        /// <summary>The original arbitrary mapping Ft(t) (fraction of the age of the universe).</summary>
        public static double Ft(double u) => u <= 0 ? 0 : Math.Exp(G(u));

        /// <summary>Years ago for a clock arc.</summary>
        public static double YearsAgo(double u) => u <= 0 ? 0 : AgeU * Math.Exp(G(u));

        /// <summary>Clock arc for a number of years ago (inverse of <see cref="YearsAgo"/>).</summary>
        public static float Arc(double yearsAgo) => (float)ArcD(yearsAgo);

        public static double ArcD(double yearsAgo)
        {
            if (yearsAgo >= AgeU) return 1.0;
            if (yearsAgo <= 0 || double.IsNaN(yearsAgo)) return 0.0;

            double target = Math.Log(yearsAgo) - LnAgeU; // = G(u)
            if (target <= lutG[0]) return 0.0;

            // binary search the LUT, interpolate, then polish with Newton steps
            int lo = 0, hi = LutSize - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (lutG[mid] < target) lo = mid;
                else hi = mid;
            }

            double t = (target - lutG[lo]) / (lutG[hi] - lutG[lo]);
            double u = lutU[lo] + (lutU[hi] - lutU[lo]) * t;
            for (int i = 0; i < 3; i++)
            {
                double d = DG(u);
                if (d <= 0) break;
                double next = u - (G(u) - target) / d;
                if (next <= lutU[lo] || next >= lutU[hi]) break;
                u = next;
            }

            return u;
        }

        /// <summary>Fractional calendar year of "now" (UTC), e.g. 2026.74.</summary>
        public static double NowYear
        {
            get
            {
                DateTime now = DateTime.UtcNow;
                double days = DateTime.IsLeapYear(now.Year) ? 366 : 365;
                return now.Year + (now.DayOfYear - 1 + now.TimeOfDay.TotalDays) / days;
            }
        }

        /// <summary>Years ago of a (fractional) calendar year; negative years are BCE.</summary>
        public static double YearsAgoFromCalendar(double year, double nowYear) => nowYear - year;

        public static double CalendarFromYearsAgo(double yearsAgo, double nowYear) => nowYear - yearsAgo;

        /// <summary>Human readable "time ago" for axis labels and tooltips.</summary>
        public static string FormatYearsAgo(double ya, double nowYear)
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            if (ya >= 1e9) return (ya / 1e9).ToString(ya >= 1e10 ? "0.0" : "0.##", c) + " billion years ago";
            if (ya >= 1e6) return (ya / 1e6).ToString(ya >= 1e8 ? "0" : "0.#", c) + " million years ago";
            if (ya >= 1e4) return (ya / 1e3).ToString("0", c) + ",000 years ago";
            if (ya >= 5000) return (ya / 1e3).ToString("0.#", c) + "k years ago";
            if (ya >= 30)
            {
                double year = Math.Round(nowYear - ya);
                return year < 1 ? (1 - year).ToString("0", c) + " BCE" : year.ToString("0", c);
            }

            if (ya >= 2) return Math.Round(ya).ToString("0", c) + " years ago";
            double days = ya * 365.25;
            if (days >= 60) return Math.Round(days / 30.44).ToString("0", c) + " months ago";
            if (days >= 14) return Math.Round(days / 7).ToString("0", c) + " weeks ago";
            if (days >= 2) return Math.Round(days).ToString("0", c) + " days ago";
            double hours = days * 24;
            if (hours >= 2) return Math.Round(hours).ToString("0", c) + " hours ago";
            double minutes = hours * 60;
            if (minutes >= 2) return Math.Round(minutes).ToString("0", c) + " minutes ago";
            double seconds = minutes * 60;
            if (seconds >= 1) return Math.Round(seconds).ToString("0", c) + " seconds ago";
            if (seconds >= 1e-3) return Math.Round(seconds * 1e3).ToString("0", c) + " ms ago";
            return "now";
        }

        /// <summary>Short axis label, e.g. "13.8 Ga", "66 Ma", "300 ka", "1492", "5 y", "3 d", "now".</summary>
        public static string FormatShort(double ya, double nowYear)
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            if (ya >= 1e9) return (ya / 1e9).ToString("0.##", c) + " Ga";
            if (ya >= 1e6) return (ya / 1e6).ToString(ya >= 1e7 ? "0" : "0.#", c) + " Ma";
            if (ya >= 1e4) return (ya / 1e3).ToString("0", c) + " ka";
            if (ya >= 30)
            {
                double year = Math.Round(nowYear - ya);
                return year < 1 ? (1 - year).ToString("0", c) + " BCE" : year.ToString("0", c);
            }

            if (ya >= 1) return Math.Round(ya).ToString("0", c) + " y ago";
            double days = ya * 365.25;
            if (days >= 1) return Math.Round(days).ToString("0", c) + " d ago";
            double hours = days * 24;
            if (hours >= 1) return Math.Round(hours).ToString("0", c) + " h ago";
            double minutes = hours * 60;
            if (minutes >= 1) return Math.Round(minutes).ToString("0", c) + " min ago";
            double seconds = minutes * 60;
            if (seconds >= 1) return Math.Round(seconds).ToString("0", c) + " s ago";
            return "now";
        }
    }
}
