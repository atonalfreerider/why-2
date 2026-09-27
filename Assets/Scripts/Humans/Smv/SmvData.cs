using System;
using System.Collections.Generic;
using System.Globalization;

namespace Why.Humans.Smv
{
    /// <summary>A yearly series (calendar year -> value), linearly interpolated and held at both ends.</summary>
    public sealed class RateSeries
    {
        readonly double[] years;
        readonly double[] values;
        readonly double fallback;

        public RateSeries(List<(double year, double value)> points, double fallback)
        {
            points.Sort((a, b) => a.year.CompareTo(b.year));
            years = new double[points.Count];
            values = new double[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                years[i] = points[i].year;
                values[i] = points[i].value;
            }

            this.fallback = fallback;
        }

        public int Count => years.Length;

        /// <summary>Value at a (fractional) calendar year; the fallback when the series is empty.</summary>
        public double At(double year)
        {
            int n = years.Length;
            if (n == 0) return fallback;
            if (year <= years[0]) return values[0];
            if (year >= years[n - 1]) return values[n - 1];
            int lo = 0, hi = n - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (years[mid] <= year) lo = mid;
                else hi = mid;
            }

            double t = (year - years[lo]) / (years[hi] - years[lo]);
            return values[lo] + (values[hi] - values[lo]) * t;
        }
    }

    /// <summary>
    /// Lifetime sexual partner counts by age band and sex (bedbible.com / CDC NSFG, from the smv project):
    /// for each age band 15-19 .. 40-44 the share of people in the ranges 0-1, 2-4, 5-9, 10-39 and 40+.
    /// </summary>
    public sealed class PartnerTable
    {
        public const int Bands = 6;
        public const int Ranges = 5;

        /// <summary>Middle age of each band (15-19 .. 40-44).</summary>
        public static readonly float[] BandMidAge = { 17f, 22f, 27f, 32f, 37f, 42f };

        static readonly float[] RangeLo = { 0f, 2f, 5f, 10f, 40f };
        static readonly float[] RangeHi = { 1f, 4f, 9f, 39f, 60f };

        // [sex][band][range] cumulative share 0..1 (sex 0 = women, 1 = men)
        readonly float[][][] cumulative = new float[2][][];

        public bool IsEmpty { get; private set; } = true;

        PartnerTable()
        {
            for (int s = 0; s < 2; s++)
            {
                cumulative[s] = new float[Bands][];
                for (int b = 0; b < Bands; b++) cumulative[s][b] = new float[Ranges];
            }
        }

        /// <summary>Parses "ageRange,partnerRange,womenPct,menPct" rows.</summary>
        public static PartnerTable Parse(string csv)
        {
            PartnerTable table = new PartnerTable();
            float[][][] pct = new float[2][][];
            for (int s = 0; s < 2; s++)
            {
                pct[s] = new float[Bands][];
                for (int b = 0; b < Bands; b++) pct[s][b] = new float[Ranges];
            }

            foreach (string raw in SmvData.Lines(csv))
            {
                string[] parts = raw.Split(',');
                if (parts.Length < 4) continue;
                int ageLo = LeadingInt(parts[0]);
                int partnersLo = LeadingInt(parts[1]);
                int band = (ageLo - 15) / 5;
                int range = Array.IndexOf(RangeLo, (float)partnersLo);
                if (ageLo < 15 || band >= Bands || range < 0) continue;
                pct[0][band][range] = SmvData.ParseFloat(parts[2]);
                pct[1][band][range] = SmvData.ParseFloat(parts[3]);
                table.IsEmpty = false;
            }

            for (int s = 0; s < 2; s++)
            {
                for (int b = 0; b < Bands; b++)
                {
                    float total = 0;
                    for (int r = 0; r < Ranges; r++) total += pct[s][b][r];
                    float acc = 0;
                    for (int r = 0; r < Ranges; r++)
                    {
                        acc += total > 0 ? pct[s][b][r] / total : (r == 0 ? 1f : 0f);
                        table.cumulative[s][b][r] = acc;
                    }
                }
            }

            return table;
        }

        /// <summary>
        /// Lifetime partner count at the middle of each age band for a person at quantile q (0..1) of their
        /// sex's distribution. Using one quantile for all bands keeps a person's history consistent (the
        /// same person stays relatively cautious or relatively active); counts never decrease with age.
        /// </summary>
        public void Sample(bool male, float q, float[] dst, int offset)
        {
            float previous = 0;
            float[][] cum = cumulative[male ? 1 : 0];
            for (int b = 0; b < Bands; b++)
            {
                float count = 0;
                if (!IsEmpty)
                {
                    float lo = 0;
                    for (int r = 0; r < Ranges; r++)
                    {
                        float hi = cum[b][r];
                        if (q <= hi || r == Ranges - 1)
                        {
                            float f = hi > lo ? Math.Min(1f, Math.Max(0f, (q - lo) / (hi - lo))) : 0f;
                            count = RangeLo[r] + f * (RangeHi[r] - RangeLo[r]);
                            break;
                        }

                        lo = hi;
                    }
                }

                previous = Math.Max(previous, count);
                dst[offset + b] = previous;
            }
        }

        static int LeadingInt(string s)
        {
            int v = 0;
            bool any = false;
            foreach (char c in s.Trim())
            {
                if (c < '0' || c > '9') break;
                v = v * 10 + (c - '0');
                any = true;
            }

            return any ? v : -1;
        }
    }

    /// <summary>
    /// The smv project's data (Resources/Data/smv): UN World Population Prospects single-age population of
    /// the United States by sex (thousands, mid-year), US marriage and divorce rates per 1000, single-parent
    /// homes (%), and lifetime partner counts. Pure managed parsing, safe on a worker thread.
    /// </summary>
    public sealed class SmvData
    {
        public const string MenPath = "Data/smv/UN-US-MEN";
        public const string WomenPath = "Data/smv/UN-US-WOMEN";
        public const string MarriagePath = "Data/smv/US-Marriage-Per-1000";
        public const string DivorcePath = "Data/smv/US-divorce-per-1000";
        public const string SingleParentPath = "Data/smv/US-single-parent-home";
        public const string PartnersPath = "Data/smv/AgeRange-NumPartners-Women-Men";

        /// <summary>Oldest single-age column (the UN open-ended 100+ group).</summary>
        public const int MaxAge = 100;

        /// <summary>
        /// US resident population by decennial census before the UN series begins (US Census Bureau,
        /// millions): the envelope of the backfilled pre-1950 lifelines.
        /// </summary>
        static readonly double[,] CensusMillions =
        {
            { 1850, 23.19 }, { 1860, 31.44 }, { 1870, 38.56 }, { 1880, 50.19 }, { 1890, 62.98 },
            { 1900, 76.21 }, { 1910, 92.23 }, { 1920, 106.02 }, { 1930, 123.20 }, { 1940, 132.16 }
        };

        /// <summary>
        /// Median age at first marriage (US Census Bureau, historical table MS-2): year, men, women. Drives
        /// who marries when in the simulation.
        /// </summary>
        static readonly double[,] FirstMarriageAge =
        {
            { 1890, 26.1, 22.0 }, { 1900, 25.9, 21.9 }, { 1910, 25.1, 21.6 }, { 1920, 24.6, 21.2 },
            { 1930, 24.3, 21.3 }, { 1940, 24.3, 21.5 }, { 1950, 22.8, 20.3 }, { 1960, 22.8, 20.3 },
            { 1970, 23.2, 20.8 }, { 1980, 24.7, 22.0 }, { 1990, 26.1, 23.9 }, { 2000, 26.8, 25.1 },
            { 2010, 28.2, 26.1 }, { 2020, 30.5, 28.1 }
        };

        /// <summary>First year of the pyramid series (mid-year estimates).</summary>
        public int FirstYear { get; private set; }

        /// <summary>Last year with data; later years are extrapolated.</summary>
        public int LastDataYear { get; private set; }

        /// <summary>Last year of <see cref="Pyramid"/> (data plus extrapolation).</summary>
        public int LastYear => FirstYear + rows[0].Count - 1;

        public RateSeries Marriage { get; private set; }
        public RateSeries Divorce { get; private set; }
        public RateSeries SingleParent { get; private set; }
        public PartnerTable Partners { get; private set; }

        public bool IsValid => rows[0].Count > 0 && rows[1].Count > 0;

        // [sex][year - FirstYear][age], thousands (sex 0 = women, 1 = men)
        readonly List<float[]>[] rows = { new List<float[]>(), new List<float[]>() };
        double[] totals = Array.Empty<double>();
        double maxTotal;

        /// <summary>Parses all smv files; missing or broken files degrade to empty series.</summary>
        /// <param name="lastYear">extrapolate the pyramid up to this year, holding the last observed rates</param>
        public static SmvData Parse(string menCsv, string womenCsv, string marriageCsv, string divorceCsv,
            string singleParentCsv, string partnersCsv, int lastYear)
        {
            SmvData d = new SmvData();
            Dictionary<int, float[]> men = ParsePyramid(menCsv);
            Dictionary<int, float[]> women = ParsePyramid(womenCsv);
            int first = int.MaxValue, last = int.MinValue;
            foreach (int y in men.Keys)
            {
                if (!women.ContainsKey(y)) continue;
                first = Math.Min(first, y);
                last = Math.Max(last, y);
            }

            if (first <= last)
            {
                d.FirstYear = first;
                d.LastDataYear = last;
                float[] prevW = null, prevM = null;
                for (int y = first; y <= last; y++)
                {
                    // bridge a missing year with its predecessor
                    prevW = women.TryGetValue(y, out float[] w) ? w : prevW;
                    prevM = men.TryGetValue(y, out float[] m) ? m : prevM;
                    d.rows[0].Add(prevW);
                    d.rows[1].Add(prevM);
                }

                for (int s = 0; s < 2; s++) Extrapolate(d.rows[s], lastYear - last);
                d.ComputeTotals();
            }

            d.Marriage = new RateSeries(ParseSeries(marriageCsv), 9);
            d.Divorce = new RateSeries(ParseSeries(divorceCsv), 3);
            d.SingleParent = new RateSeries(ParseSeries(singleParentCsv), 7);
            d.Partners = PartnerTable.Parse(partnersCsv);
            return d;
        }

        /// <summary>People (thousands) of one sex and single age at mid-year of a calendar year.</summary>
        public float Pyramid(int year, bool male, int age)
        {
            List<float[]> r = rows[male ? 1 : 0];
            int i = year - FirstYear;
            if (i < 0 || i >= r.Count || age < 0 || age > MaxAge) return 0;
            return r[i][age];
        }

        /// <summary>
        /// US population (people) at a fractional calendar year: the census before the UN series, the
        /// pyramid totals (at mid-year) after.
        /// </summary>
        public double Population(double year)
        {
            double firstMid = FirstYear + 0.5;
            if (totals.Length == 0 || year < firstMid)
            {
                int n = CensusMillions.GetLength(0);
                if (year <= CensusMillions[0, 0]) return CensusMillions[0, 1] * 1e6;
                for (int i = 0; i < n; i++)
                {
                    double y0 = CensusMillions[i, 0], p0 = CensusMillions[i, 1] * 1e6;
                    double y1 = i + 1 < n ? CensusMillions[i + 1, 0] : firstMid;
                    double p1 = i + 1 < n ? CensusMillions[i + 1, 1] * 1e6 : totals.Length > 0 ? totals[0] : p0;
                    if (year <= y1) return Math.Exp(Math.Log(p0) + (Math.Log(p1) - Math.Log(p0)) * (year - y0) / (y1 - y0));
                }

                return CensusMillions[n - 1, 1] * 1e6;
            }

            double x = year - firstMid;
            int k = (int)Math.Floor(x);
            if (k >= totals.Length - 1) return totals[totals.Length - 1];
            return totals[k] + (totals[k + 1] - totals[k]) * (x - k);
        }

        /// <summary>Largest population of the series (people).</summary>
        public double MaxPopulation => maxTotal > 0 ? maxTotal : 3.4e8;

        /// <summary>Median age at first marriage for a calendar year.</summary>
        public static double MedianFirstMarriageAge(bool male, double year)
        {
            int n = FirstMarriageAge.GetLength(0);
            int col = male ? 1 : 2;
            if (year <= FirstMarriageAge[0, 0]) return FirstMarriageAge[0, col];
            for (int i = 0; i < n - 1; i++)
            {
                double y0 = FirstMarriageAge[i, 0], y1 = FirstMarriageAge[i + 1, 0];
                if (year <= y1)
                {
                    return FirstMarriageAge[i, col] +
                           (FirstMarriageAge[i + 1, col] - FirstMarriageAge[i, col]) * (year - y0) / (y1 - y0);
                }
            }

            return FirstMarriageAge[n - 1, col];
        }

        void ComputeTotals()
        {
            int n = rows[0].Count;
            totals = new double[n];
            maxTotal = 0;
            for (int i = 0; i < n; i++)
            {
                double sum = 0;
                for (int s = 0; s < 2; s++)
                {
                    float[] r = rows[s][i];
                    for (int a = 0; a <= MaxAge; a++) sum += r[a];
                }

                totals[i] = sum * 1000.0;
                maxTotal = Math.Max(maxTotal, totals[i]);
            }
        }

        /// <summary>
        /// Continues a pyramid for extra years holding the last observed rates: births as in the last
        /// year, and every cohort changing (deaths, immigration) by the same ratio as in the last year.
        /// </summary>
        static void Extrapolate(List<float[]> r, int years)
        {
            if (r.Count < 2 || years <= 0) return;
            float[] last = r[r.Count - 1], before = r[r.Count - 2];
            float[] ratio = new float[MaxAge + 1];
            for (int a = 1; a < MaxAge; a++)
            {
                ratio[a] = before[a - 1] > 0 ? Math.Min(1.5f, last[a] / before[a - 1]) : 1f;
            }

            float openBefore = before[MaxAge - 1] + before[MaxAge];
            ratio[MaxAge] = openBefore > 0 ? Math.Min(1f, last[MaxAge] / openBefore) : 0f;

            for (int k = 0; k < years; k++)
            {
                float[] prev = r[r.Count - 1];
                float[] next = new float[MaxAge + 1];
                next[0] = last[0];
                for (int a = 1; a < MaxAge; a++) next[a] = prev[a - 1] * ratio[a];
                next[MaxAge] = (prev[MaxAge - 1] + prev[MaxAge]) * ratio[MaxAge];
                r.Add(next);
            }
        }

        static Dictionary<int, float[]> ParsePyramid(string csv)
        {
            Dictionary<int, float[]> result = new Dictionary<int, float[]>();
            foreach (string line in Lines(csv))
            {
                string[] parts = line.Split(',');
                if (parts.Length < 2) continue;
                if (!int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int year))
                {
                    continue;
                }

                float[] ages = new float[MaxAge + 1];
                for (int a = 0; a <= MaxAge && a + 1 < parts.Length; a++) ages[a] = ParseFloat(parts[a + 1]);
                result[year] = ages;
            }

            return result;
        }

        static List<(double, double)> ParseSeries(string csv)
        {
            List<(double, double)> points = new List<(double, double)>();
            foreach (string line in Lines(csv))
            {
                string[] parts = line.Split(',');
                if (parts.Length < 2) continue;
                if (!double.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double year))
                {
                    continue;
                }

                points.Add((year, ParseFloat(parts[1])));
            }

            return points;
        }

        /// <summary>Non-empty lines of a text (tolerates CRLF and a trailing newline).</summary>
        internal static IEnumerable<string> Lines(string text)
        {
            if (string.IsNullOrEmpty(text)) yield break;
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length > 0) yield return line;
            }
        }

        /// <summary>Parses "  1 836" style numbers (spaces as thousands separators); 0 when invalid.</summary>
        internal static float ParseFloat(string s)
        {
            string compact = s.Replace(" ", "").Replace(" ", "").Replace(" ", "").Trim();
            return float.TryParse(compact, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : 0f;
        }
    }
}
