using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Why.Economy.Data;

namespace Why.Economy.Model
{
    /// <summary>
    /// History the money circuit needs and the economy files do not (yet) carry, as shares of GDP: government
    /// purchases, exports and imports (1947-2024), personal taxes, contributions for social insurance and social
    /// benefits before the files' personal-income detail starts (1947-1988), federal interest, and the share of US
    /// corporate equity held abroad. The first group are BEA's published annual figures (NIPA Tables 1.1.5 and 2.1, the
    /// September 2025 vintage of circuit.json incomeHistory, from the research mirrors gdpgdi.csv and T20100.csv; Table
    /// 2.1's quarterly rates averaged by year); federal interest and the foreign share of equity are approximations of
    /// CBO and Federal Reserve series at a few anchor years (linear in between, good to about half a percentage point).
    /// The circuit uses the data wherever a file has the figure (see <see cref="Splice"/>) and these only to carry it
    /// back in time; they are named here so a reader can check them and a data file can replace them.
    /// </summary>
    public static class CircuitHistory
    {
        /// <summary>
        /// Government consumption expenditures and gross investment (all levels), share of GDP, 1947-2024: BEA NIPA
        /// Table 1.1.5. The Korean War peak (1952-53), the late-1960s build-up and the 2009-10 stimulus show.
        /// </summary>
        public static readonly YearSeries Purchases = Annual(1947,
            0.1596, 0.1594, 0.1828, 0.1685, 0.2113, 0.2439, 0.2486, 0.2368, 0.2185, 0.2185, 0.2261, 0.2372, 0.2271,
            0.2222, 0.2299, 0.2323, 0.2309, 0.2261, 0.2210, 0.2280, 0.2407, 0.2398, 0.2348, 0.2353, 0.2296, 0.2237,
            0.2138, 0.2209, 0.2262, 0.2157, 0.2086, 0.2026, 0.1997, 0.2063, 0.2040, 0.2128, 0.2109, 0.2050, 0.2098,
            0.2131, 0.2125, 0.2060, 0.2042, 0.2077, 0.2109, 0.2062, 0.1990, 0.1924, 0.1897, 0.1849, 0.1804, 0.1778,
            0.1786, 0.1782, 0.1843, 0.1912, 0.1930, 0.1914, 0.1898, 0.1899, 0.1928, 0.2020, 0.2125, 0.2097, 0.2018,
            0.1930, 0.1856, 0.1800, 0.1767, 0.1756, 0.1732, 0.1738, 0.1758, 0.1871, 0.1774, 0.1711, 0.1705, 0.1721);

        /// <summary>
        /// Government social benefits to persons, share of GDP, 1947-1988 (before the files' personal-income detail):
        /// BEA NIPA Table 2.1. Medicare and Medicaid start in 1966.
        /// </summary>
        public static readonly YearSeries Transfers = Annual(1947,
            0.0418, 0.0360, 0.0398, 0.0447, 0.0304, 0.0299, 0.0300, 0.0352, 0.0347, 0.0347, 0.0383, 0.0462, 0.0439,
            0.0450, 0.0500, 0.0478, 0.0476, 0.0458, 0.0457, 0.0461, 0.0532, 0.0567, 0.0580, 0.0668, 0.0733, 0.0741,
            0.0762, 0.0832, 0.0968, 0.0948, 0.0910, 0.0865, 0.0865, 0.0950, 0.0960, 0.1026, 0.1020, 0.0943, 0.0929,
            0.0936, 0.0923, 0.0911);

        /// <summary>Personal current taxes, share of GDP, 1947-1988: BEA NIPA Table 2.1.</summary>
        public static readonly YearSeries PersonalTaxes = Annual(1947,
            0.0793, 0.0700, 0.0614, 0.0631, 0.0780, 0.0871, 0.0854, 0.0772, 0.0772, 0.0814, 0.0820, 0.0801, 0.0811,
            0.0849, 0.0840, 0.0854, 0.0856, 0.0761, 0.0777, 0.0816, 0.0848, 0.0925, 0.1027, 0.0960, 0.0873, 0.0967,
            0.0929, 0.0978, 0.0876, 0.0922, 0.0951, 0.0977, 0.1024, 0.1048, 0.1078, 0.1061, 0.0971, 0.0936, 0.0963,
            0.0956, 0.1008, 0.0966);

        /// <summary>
        /// Contributions for government social insurance (domestic, employees' and employers'), share of GDP,
        /// 1947-1988: BEA NIPA Table 2.1.
        /// </summary>
        public static readonly YearSeries Contributions = Annual(1947,
            0.0223, 0.0168, 0.0179, 0.0183, 0.0191, 0.0188, 0.0183, 0.0207, 0.0214, 0.0222, 0.0241, 0.0237, 0.0265,
            0.0303, 0.0302, 0.0317, 0.0340, 0.0327, 0.0315, 0.0385, 0.0405, 0.0411, 0.0434, 0.0432, 0.0439, 0.0463,
            0.0530, 0.0551, 0.0530, 0.0541, 0.0543, 0.0558, 0.0581, 0.0582, 0.0610, 0.0625, 0.0622, 0.0638, 0.0648,
            0.0663, 0.0665, 0.0690);

        /// <summary>
        /// Federal net interest outlays, share of GDP: CBO Historical Budget Data (fiscal years), approximate. Paid to
        /// bondholders; the files have only the 2025 figure.
        /// </summary>
        public static readonly YearSeries FederalInterest = Table(
            1950, 0.017, 1955, 0.012, 1960, 0.013, 1965, 0.012, 1970, 0.014, 1975, 0.015, 1980, 0.019, 1985, 0.030,
            1990, 0.031, 1995, 0.030, 2000, 0.022, 2005, 0.014, 2010, 0.013, 2015, 0.012, 2020, 0.016, 2022, 0.019,
            2023, 0.024, 2024, 0.031);

        /// <summary>
        /// Exports of goods and services, share of GDP: BEA NIPA Table 1.1.5, 1947-2024; 2025 approximate (the files do
        /// not have it).
        /// </summary>
        public static readonly YearSeries Exports = With(Annual(1947,
            0.0751, 0.0566, 0.0532, 0.0412, 0.0493, 0.0448, 0.0393, 0.0405, 0.0415, 0.0474, 0.0507, 0.0427, 0.0436,
            0.0499, 0.0491, 0.0481, 0.0487, 0.0512, 0.0500, 0.0503, 0.0505, 0.0509, 0.0510, 0.0556, 0.0541, 0.0554,
            0.0668, 0.0820, 0.0823, 0.0798, 0.0765, 0.0795, 0.0876, 0.0983, 0.0952, 0.0847, 0.0762, 0.0749, 0.0699,
            0.0701, 0.0750, 0.0849, 0.0894, 0.0925, 0.0966, 0.0971, 0.0955, 0.0989, 0.1064, 0.1075, 0.1112, 0.1052,
            0.1031, 0.1069, 0.0970, 0.0913, 0.0904, 0.0963, 0.0998, 0.1064, 0.1146, 0.1243, 0.1093, 0.1234, 0.1356,
            0.1364, 0.1355, 0.1351, 0.1241, 0.1189, 0.1218, 0.1229, 0.1179, 0.1012, 0.1083, 0.1165, 0.1105, 0.1097),
            2025, 0.105);

        /// <summary>
        /// Imports of goods and services, share of GDP: BEA NIPA Table 1.1.5, 1947-2024; 2025 approximate (the files do
        /// not have it).
        /// </summary>
        public static readonly YearSeries Imports = With(Annual(1947,
            0.0318, 0.0367, 0.0339, 0.0387, 0.0420, 0.0416, 0.0411, 0.0395, 0.0404, 0.0421, 0.0421, 0.0416, 0.0428,
            0.0421, 0.0404, 0.0413, 0.0410, 0.0411, 0.0425, 0.0456, 0.0464, 0.0495, 0.0496, 0.0520, 0.0535, 0.0580,
            0.0640, 0.0825, 0.0728, 0.0807, 0.0876, 0.0903, 0.0962, 0.1028, 0.0991, 0.0907, 0.0904, 0.1003, 0.0962,
            0.0989, 0.1048, 0.1058, 0.1048, 0.1056, 0.1013, 0.1024, 0.1050, 0.1116, 0.1181, 0.1194, 0.1231, 0.1231,
            0.1300, 0.1441, 0.1326, 0.1315, 0.1359, 0.1482, 0.1566, 0.1633, 0.1655, 0.1744, 0.1383, 0.1588, 0.1728,
            0.1704, 0.1639, 0.1640, 0.1528, 0.1456, 0.1495, 0.1516, 0.1447, 0.1302, 0.1441, 0.1525, 0.1388, 0.1404),
            2025, 0.139);

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

        /// <summary>A table of (year, value) pairs.</summary>
        static YearSeries Table(params double[] yearValuePairs)
        {
            List<double[]> points = new List<double[]>(yearValuePairs.Length / 2);
            for (int i = 0; i + 1 < yearValuePairs.Length; i += 2) points.Add(new[] { yearValuePairs[i], yearValuePairs[i + 1] });
            return new YearSeries(points);
        }

        /// <summary>One value per year from <paramref name="firstYear"/> on.</summary>
        static YearSeries Annual(int firstYear, params double[] values)
        {
            List<double[]> points = new List<double[]>(values.Length);
            for (int i = 0; i < values.Length; i++) points.Add(new[] { firstYear + i, values[i] });
            return new YearSeries(points);
        }

        /// <summary>A series with one more point.</summary>
        static YearSeries With(YearSeries series, double year, double value)
        {
            List<double[]> points = new List<double[]>(series.Count + 1);
            for (int i = 0; i < series.Count; i++) points.Add(new[] { series.YearAt(i), series.ValueAt(i) });
            points.Add(new[] { year, value });
            return new YearSeries(points);
        }
    }
}
