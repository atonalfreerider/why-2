using System;
using System.Collections.Generic;
using UnityEngine;
using Why.Economy.Data;
using Why.Humans.Smv;

namespace Why.Economy.Model
{
    /// <summary>
    /// The industry wall at one sample year (data space): per industry, where its band's parts begin and end.
    /// </summary>
    public sealed class WallColumn
    {
        /// <summary>Calendar year of the column (whole years, the last one is now).</summary>
        public double Year;

        /// <summary>Clock arc of the year and the data rho the wall stands on (the population's center line).</summary>
        public float U, Rho;

        /// <summary>
        /// Per industry (EconomyData.Industries order), data heights: the bottom of the band (wages begin), the bottom of
        /// upkeep (production taxes and depreciation), the bottom of the owners' part, and the top of the band.
        /// </summary>
        public float[] Lo, Upkeep, Owners, Hi;
    }

    /// <summary>One industry's band of the wall at a moment (data heights and the rho of the wall's plane).</summary>
    public readonly struct WallBand
    {
        /// <summary>Bottom of the wages part, of upkeep, of the owners' part, and the top of the band.</summary>
        public readonly float WagesLo, UpkeepLo, OwnersLo, Hi;

        /// <summary>Data rho of the wall's plane.</summary>
        public readonly float Rho;

        public WallBand(float wagesLo, float upkeepLo, float ownersLo, float hi, float rho)
        {
            WagesLo = wagesLo;
            UpkeepLo = upkeepLo;
            OwnersLo = ownersLo;
            Hi = hi;
            Rho = rho;
        }

        /// <summary>Middle of the whole band.</summary>
        public float Mid => 0.5f * (WagesLo + Hi);
    }

    /// <summary>
    /// The industry wall's geometry, computed once and shared (<see cref="SharedKey"/>) by the layer that draws the wall
    /// and the layers whose money lands on it (the money threads), so a thread ends exactly on its band: per year from
    /// the first year of data to now, each industry's band of real value added (2025 dollars) stacked from the ground,
    /// tiers in data order, split into wages, upkeep and what owners keep. Its height follows the real size of the
    /// economy (the largest year reaches <see cref="EconomyStyle.WallTopY"/>); it stands on the population's center
    /// line. Read only once built; safe from any thread.
    /// </summary>
    public sealed class WallGeometry
    {
        public const string SharedKey = "economy.wall";

        /// <summary>Year the wages share is scaled to (the year of the industries' compShare).</summary>
        const double ShareYear = 2024;

        readonly List<WallColumn> columns;

        WallGeometry(List<WallColumn> columns, int industries)
        {
            this.columns = columns;
            IndustryCount = industries;
        }

        /// <summary>The wall sampled every year from the first year of data to now (empty without data).</summary>
        public IReadOnlyList<WallColumn> Columns => columns;

        /// <summary>Industries per column (EconomyData.Industries).</summary>
        public int IndustryCount { get; }

        /// <summary>True when the wall has at least two columns (something to draw and to land on).</summary>
        public bool IsValid => columns.Count >= 2;

        public double FirstYear => columns.Count > 0 ? columns[0].Year : 0;

        public double LastYear => columns.Count > 0 ? columns[columns.Count - 1].Year : 0;

        /// <summary>Builds the wall for the data on the finished population (worker thread).</summary>
        public static WallGeometry Build(EconomyData data, SmvPopulation pop, double now)
        {
            IReadOnlyList<Industry> inds = data.Industries;
            int n = inds.Count;
            List<double> years = new List<double>();
            for (int y = data.FirstYear; y < now; y++) years.Add(y);
            years.Add(now);

            // real value added (2025 dollars) per industry and year, and the largest total
            double[,] real = new double[years.Count, n];
            double max = 0;
            for (int j = 0; j < years.Count; j++)
            {
                double total = 0;
                for (int i = 0; i < n; i++)
                {
                    double v = Math.Max(0, data.Real(inds[i].ValueAdded.GrowthAt(years[j]), years[j]));
                    real[j, i] = v;
                    total += v;
                }

                max = Math.Max(max, total);
            }

            if (max <= 0 || n == 0) return new WallGeometry(new List<WallColumn>(), n);
            float scale = (float)((EconomyStyle.WallTopY - EconomyStyle.GroundY) / max);

            List<WallColumn> columns = new List<WallColumn>(years.Count);
            for (int j = 0; j < years.Count; j++)
            {
                double year = years[j];
                double labor = LaborRatio(data, year);
                WallColumn c = new WallColumn
                {
                    Year = year,
                    U = EconomyStage.U(year),
                    Rho = CenterAt(pop, year) + EconomyStyle.WallRhoOffset,
                    Lo = new float[n], Upkeep = new float[n], Owners = new float[n], Hi = new float[n]
                };

                float acc = EconomyStyle.GroundY;
                for (int i = 0; i < n; i++)
                {
                    float h = (float)real[j, i] * scale;
                    Split(inds[i], labor, out double wages, out double upkeep, out _);
                    c.Lo[i] = acc;
                    c.Upkeep[i] = acc + (float)(h * wages);
                    c.Owners[i] = c.Upkeep[i] + (float)(h * upkeep);
                    c.Hi[i] = acc + h;
                    c.Owners[i] = Mathf.Min(c.Owners[i], c.Hi[i]);
                    acc += h;
                }

                columns.Add(c);
            }

            return new WallGeometry(columns, n);
        }

        /// <summary>
        /// How an industry's value added divides into wages, upkeep and what owners keep (shares of 1): upkeep is
        /// production taxes plus depreciation (at most all of it); wages are the compensation share scaled by the labor
        /// share relative to <see cref="ShareYear"/> (<paramref name="laborRatio"/>), clamped to what upkeep leaves;
        /// owners keep the rest. The one rule behind the wall's band split and the land's sector strips.
        /// </summary>
        public static void Split(Industry ind, double laborRatio, out double wages, out double upkeep, out double owners)
        {
            upkeep = Math.Min(1, ind.TaxShare + ind.DepShare);
            wages = Math.Max(0, Math.Min(1 - upkeep, ind.CompShare * laborRatio));
            owners = Math.Max(0, 1 - upkeep - wages);
        }

        /// <summary>
        /// The labor-share ratio <see cref="Split"/> takes for a year: the labor share at the year over the labor share at
        /// <see cref="ShareYear"/> (1 without data).
        /// </summary>
        public static double LaborRatio(EconomyData data, double year)
        {
            double laborRef = data.LaborShare.IsEmpty ? 1 : data.LaborShare.At(ShareYear);
            return data.LaborShare.IsEmpty || laborRef <= 0 ? 1 : data.LaborShare.At(year) / laborRef;
        }

        /// <summary>The population's center line (data rho) at a year; the framing default before it exists.</summary>
        static float CenterAt(SmvPopulation pop, double year)
        {
            if (pop?.Sim == null || pop.Sim.StepCount == 0) return EconomyStyle.FramingRho;
            return pop.Sim.Center[pop.Sim.StepAt(year)];
        }

        /// <summary>The column nearest to a year.</summary>
        public WallColumn ColumnAt(double year)
        {
            int j = Bracket(year, out float f);
            return f < 0.5f || j + 1 >= columns.Count ? columns[j] : columns[j + 1];
        }

        /// <summary>
        /// An industry's band at any moment between the first column and now (clamped to the wall's ends), interpolated
        /// between the two columns around it exactly as the wall's surfaces are drawn between them.
        /// </summary>
        public WallBand BandAt(int industry, double year)
        {
            int j = Bracket(year, out float f);
            WallColumn a = columns[j], b = columns[Math.Min(j + 1, columns.Count - 1)];
            return new WallBand(Mathf.LerpUnclamped(a.Lo[industry], b.Lo[industry], f),
                Mathf.LerpUnclamped(a.Upkeep[industry], b.Upkeep[industry], f),
                Mathf.LerpUnclamped(a.Owners[industry], b.Owners[industry], f),
                Mathf.LerpUnclamped(a.Hi[industry], b.Hi[industry], f),
                Mathf.LerpUnclamped(a.Rho, b.Rho, f));
        }

        /// <summary>Top of the whole wall at a moment (data height).</summary>
        public float TopAt(double year)
        {
            int j = Bracket(year, out float f);
            int last = IndustryCount - 1;
            WallColumn a = columns[j], b = columns[Math.Min(j + 1, columns.Count - 1)];
            return Mathf.LerpUnclamped(a.Hi[last], b.Hi[last], f);
        }

        /// <summary>Data rho of the wall's plane at a moment.</summary>
        public float RhoAt(double year)
        {
            int j = Bracket(year, out float f);
            return Mathf.LerpUnclamped(columns[j].Rho, columns[Math.Min(j + 1, columns.Count - 1)].Rho, f);
        }

        /// <summary>The column at or before a year and the share of the way to the next one (0..1, clamped).</summary>
        int Bracket(double year, out float f)
        {
            f = 0;
            int n = columns.Count;
            if (n < 2 || year <= columns[0].Year) return 0;
            if (year >= columns[n - 1].Year) return n - 1;

            // columns are whole years from the first one, but the last (now) can be closer than a year
            int j = Math.Min(n - 2, Math.Max(0, (int)Math.Floor(year - columns[0].Year)));
            while (j > 0 && columns[j].Year > year) j--;
            while (j < n - 2 && columns[j + 1].Year <= year) j++;
            double span = columns[j + 1].Year - columns[j].Year;
            f = span > 0 ? (float)Math.Min(1, Math.Max(0, (year - columns[j].Year) / span)) : 0;
            return j;
        }
    }
}
