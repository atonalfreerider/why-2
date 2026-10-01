using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Why.Economy.Data;

namespace Why.Economy.Model
{
    /// <summary>
    /// History the money circuit needs and the economy files do not (yet) carry, as shares of GDP: government
    /// purchases, social benefits before 1988, federal interest, exports and imports, and the share of US corporate
    /// equity held abroad. Each table is an approximation of a published BEA, CBO or Federal Reserve series at a
    /// few anchor years (linear in between), good to about half a percentage point of GDP; the circuit uses the data
    /// wherever a file has the figure (see <see cref="Splice"/>) and these only to carry it back in time. They are
    /// named here so a reader can check them and a data file can replace them.
    /// </summary>
    public static class CircuitHistory
    {
        /// <summary>
        /// Government consumption expenditures and gross investment (all levels), share of GDP: BEA NIPA Table 1.1.10,
        /// approximate. The Korean War peak (1953), the late-1960s build-up and the 2009-10 stimulus show.
        /// </summary>
        public static readonly YearSeries Purchases = Table(
            1950, 0.155, 1953, 0.225, 1955, 0.195, 1960, 0.196, 1965, 0.195, 1970, 0.221, 1975, 0.217, 1980, 0.205,
            1985, 0.205, 1990, 0.206, 1995, 0.189, 2000, 0.176, 2005, 0.186, 2010, 0.203, 2015, 0.176, 2019, 0.174,
            2020, 0.186, 2021, 0.177, 2022, 0.172, 2023, 0.171);

        /// <summary>
        /// Government social benefits to persons, share of GDP, before the files' personal-income detail starts (1988):
        /// BEA NIPA Table 3.12, approximate. Medicare and Medicaid start in 1966.
        /// </summary>
        public static readonly YearSeries Transfers = Table(
            1950, 0.048, 1955, 0.039, 1960, 0.048, 1965, 0.052, 1970, 0.069, 1975, 0.100, 1980, 0.095, 1985, 0.095,
            1988, 0.091);

        /// <summary>
        /// Federal net interest outlays, share of GDP: CBO Historical Budget Data (fiscal years), approximate. Paid to
        /// bondholders; the files have only the 2025 figure.
        /// </summary>
        public static readonly YearSeries FederalInterest = Table(
            1950, 0.017, 1955, 0.012, 1960, 0.013, 1965, 0.012, 1970, 0.014, 1975, 0.015, 1980, 0.019, 1985, 0.030,
            1990, 0.031, 1995, 0.030, 2000, 0.022, 2005, 0.014, 2010, 0.013, 2015, 0.012, 2020, 0.016, 2022, 0.019,
            2023, 0.024, 2024, 0.031);

        /// <summary>Exports of goods and services, share of GDP: BEA NIPA Table 1.1.10, approximate.</summary>
        public static readonly YearSeries Exports = Table(
            1950, 0.042, 1960, 0.049, 1970, 0.055, 1975, 0.083, 1980, 0.098, 1985, 0.070, 1990, 0.092, 1995, 0.105,
            2000, 0.107, 2005, 0.099, 2010, 0.123, 2015, 0.124, 2020, 0.101, 2022, 0.116, 2024, 0.109, 2025, 0.105);

        /// <summary>Imports of goods and services, share of GDP: BEA NIPA Table 1.1.10, approximate.</summary>
        public static readonly YearSeries Imports = Table(
            1950, 0.038, 1960, 0.042, 1970, 0.053, 1975, 0.075, 1980, 0.104, 1985, 0.099, 1990, 0.106, 1995, 0.117,
            2000, 0.144, 2005, 0.155, 2010, 0.159, 2015, 0.153, 2020, 0.131, 2022, 0.154, 2024, 0.139, 2025, 0.139);

        /// <summary>
        /// Share of US corporate equity held by the rest of the world: Federal Reserve Z.1 Table L.223, approximate; the
        /// files have the 2025 level (circuit.json foreignEquityShare), which this is spliced to.
        /// </summary>
        public static readonly YearSeries ForeignEquity = Table(
            1950, 0.020, 1960, 0.025, 1970, 0.032, 1980, 0.052, 1990, 0.069, 2000, 0.090, 2010, 0.130, 2020, 0.158);

        /// <summary>
        /// A share-of-GDP series that follows the data where the data has it and the approximation before and after,
        /// scaled at each end so the two meet (no jump where the data starts or ends). A single data point only extends
        /// the table (the approximation runs into it).
        /// </summary>
        /// <param name="data">The data's share of GDP by year (may be empty).</param>
        /// <param name="approximation">One of this class's tables.</param>
        public static YearSeries Splice(YearSeries data, YearSeries approximation)
        {
            if (data.IsEmpty) return approximation;
            List<double[]> points = new List<double[]>();
            double first = data.FirstYear, last = data.LastYear;
            // a single data point (the calibration year) is a new end of the table, not a reason to rescale its past
            double before = data.Count > 1 && approximation.At(first) > 0 ? data.First / approximation.At(first) : 1;
            double after = approximation.At(last) > 0 ? data.Last / approximation.At(last) : 1;
            for (int i = 0; i < approximation.Count; i++)
            {
                double y = approximation.YearAt(i);
                if (y < first) points.Add(new[] { y, approximation.ValueAt(i) * before });
                else if (y > last) points.Add(new[] { y, approximation.ValueAt(i) * after });
            }

            for (int i = 0; i < data.Count; i++) points.Add(new[] { data.YearAt(i), data.ValueAt(i) });
            return new YearSeries(points);
        }

        /// <summary>
        /// A key of history rows as a share of GDP (the rows' own "gdp" when they have it, else the GDP series), with
        /// extra calibration points (year, $B) appended.
        /// </summary>
        public static YearSeries ShareOfGdp(List<JArray> rows, string key, YearSeries gdp, params (int year, double value)[] extra)
        {
            YearSeries values = EconomyData.Keyed(rows, key);
            YearSeries rowGdp = EconomyData.Keyed(rows, "gdp");
            List<double[]> points = new List<double[]>();
            for (int i = 0; i < values.Count; i++)
            {
                double y = values.YearAt(i);
                double g = !rowGdp.IsEmpty && y >= rowGdp.FirstYear && y <= rowGdp.LastYear ? rowGdp.At(y) : gdp.GrowthAt(y);
                if (g > 0) points.Add(new[] { y, values.ValueAt(i) / g });
            }

            foreach ((int year, double value) in extra)
            {
                double g = gdp.GrowthAt(year);
                if (g <= 0 || value == 0) continue;
                points.RemoveAll(p => p[0] == year);
                points.Add(new[] { (double)year, value / g });
            }

            return new YearSeries(points);
        }

        static YearSeries Table(params double[] yearValuePairs)
        {
            List<double[]> points = new List<double[]>(yearValuePairs.Length / 2);
            for (int i = 0; i + 1 < yearValuePairs.Length; i += 2) points.Add(new[] { yearValuePairs[i], yearValuePairs[i + 1] });
            return new YearSeries(points);
        }
    }
}
