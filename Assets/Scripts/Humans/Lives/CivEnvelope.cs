using System;
using System.Collections.Generic;
using UnityEngine;

namespace Why.Humans.Lives
{
    /// <summary>
    /// A stream sampled on a grid of calendar years: clock arc, band center and the gender-separated
    /// population envelope (how far lifelines may spread from the center on the women's inner side and the
    /// men's outer side).
    ///
    /// The grid is dense where the graph can stretch time (a cell per ~2 years in the modern era, whose
    /// preset unrolls a couple of centuries across the screen) and sparse in antiquity (8 years), matching
    /// the ln-time sampling of the civilization bands, and dense again while a band emerges or dissolves. Cells are computed on first use, so sparse streams
    /// (300,000 years of prehistory) only pay for the moments somebody is alive; values between cells are
    /// interpolated linearly. One instance per stream and thread.
    ///
    /// Envelope = half band width x <see cref="Margin"/> x clamp(population / peak population,
    /// <see cref="MinFill"/>, 1): lifelines fill their band in proportion to the stream's population, so their
    /// outline is the population curve. The men's side narrows by <see cref="CivWars.MaleFactor"/>.
    /// </summary>
    public sealed class CivEnvelope
    {
        /// <summary>Share of the half band width the envelope may use at peak population.</summary>
        public const float Margin = 0.95f;

        /// <summary>Smallest envelope (as a share of the half band width), so small populations stay legible.</summary>
        public const float MinFill = 0.25f;

        /// <summary>Smallest cell (years), reached in the last century.</summary>
        public const double MinCellYears = 1.5;

        /// <summary>Largest cell (years), antiquity and prehistory.</summary>
        public const double MaxCellYears = 8;

        /// <summary>Largest cell while a band emerges from its parent or dissolves (it moves fast then).</summary>
        public const double TransitionCellYears = 2;

        /// <summary>Cell size = LnStep x (years ago + LnOffset), clamped.</summary>
        const double LnStep = 0.005;

        const double LnOffset = 300;

        public readonly Civ Civ;
        public readonly int Count;

        readonly HumanWorld world;
        readonly CivWars wars;
        readonly double peakPopulation;
        readonly double[] times;
        readonly float[] u, center, women, men;
        readonly bool[] done;

        public CivEnvelope(HumanWorld world, Civ civ, CivWars wars, double peakPopulation, double start, double end)
        {
            this.world = world;
            this.wars = wars;
            this.peakPopulation = peakPopulation;
            Civ = civ;

            // the band slides out of its parent (and dissolves) within EmergeYears of its first (last) sample
            double emergeEnd = double.NegativeInfinity, dissolveStart = double.PositiveInfinity;
            if (civ.Samples.Count > 0)
            {
                if (civ != world.Humanity) emergeEnd = civ.Samples[0].Year;
                if (!civ.Extant) dissolveStart = civ.Samples[civ.Samples.Count - 1].Year;
            }

            List<double> t = new List<double>(1024);
            double now = world.NowYear;
            for (double year = start; year < end;)
            {
                t.Add(year);
                double step = Math.Min(MaxCellYears, Math.Max(MinCellYears, LnStep * (now - year + LnOffset)));
                if (year < emergeEnd || year >= dissolveStart) step = Math.Min(step, TransitionCellYears);
                else if (year + step > dissolveStart) step = dissolveStart - year;
                year += step;
            }

            // end exactly on the last cell; never leave a sliver of a cell or fewer than two cells
            if (t.Count > 1 && end - t[t.Count - 1] < 0.25 * MinCellYears) t[t.Count - 1] = end;
            else t.Add(end);
            if (t.Count < 2) t.Add(end + MinCellYears);
            times = t.ToArray();
            Count = times.Length;
            u = new float[Count];
            center = new float[Count];
            women = new float[Count];
            men = new float[Count];
            done = new bool[Count];
        }

        /// <summary>Calendar year of cell <paramref name="k"/>.</summary>
        public double Time(int k) => times[k];

        /// <summary>First cell strictly after <paramref name="year"/> (<see cref="Count"/> if none).</summary>
        public int FirstAfter(double year)
        {
            int lo = 0, hi = Count;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (times[mid] <= year) lo = mid + 1;
                else hi = mid;
            }

            return lo;
        }

        /// <summary>Last cell strictly before <paramref name="year"/> (-1 if none).</summary>
        public int LastBefore(double year)
        {
            int lo = 0, hi = Count;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (times[mid] < year) lo = mid + 1;
                else hi = mid;
            }

            return lo - 1;
        }

        public float U(int k)
        {
            Ensure(k);
            return u[k];
        }

        public float Center(int k)
        {
            Ensure(k);
            return center[k];
        }

        /// <summary>Envelope of one side at cell <paramref name="k"/> (rho units).</summary>
        public float Envelope(int k, bool male)
        {
            Ensure(k);
            return male ? men[k] : women[k];
        }

        /// <summary>State at any calendar year inside the grid (clamped to it), interpolated between cells.</summary>
        public void At(double year, out float arc, out float bandCenter, out float envWomen, out float envMen) =>
            Between(LastBefore(year), year, out arc, out bandCenter, out envWomen, out envMen);

        /// <summary>
        /// State at a calendar year known to lie between cells <paramref name="k"/> and k + 1 (clamped to the
        /// grid), interpolated. Saves the search when the caller already knows the cell.
        /// </summary>
        public void Between(int k, double year, out float arc, out float bandCenter, out float envWomen, out float envMen)
        {
            k = Mathf.Clamp(k, 0, Count - 2);
            float f = Mathf.Clamp01((float)((year - times[k]) / Math.Max(times[k + 1] - times[k], 1e-9)));
            Ensure(k);
            Ensure(k + 1);
            arc = Mathf.Lerp(u[k], u[k + 1], f);
            bandCenter = Mathf.Lerp(center[k], center[k + 1], f);
            envWomen = Mathf.Lerp(women[k], women[k + 1], f);
            envMen = Mathf.Lerp(men[k], men[k + 1], f);
        }

        void Ensure(int k)
        {
            if (done[k]) return;
            done[k] = true;
            double year = times[k];
            u[k] = DeepTime.Arc(world.NowYear - year);
            if (Sample(world, Civ, wars, peakPopulation, year, out center[k], out women[k], out men[k])) return;

            // just outside the band (rounding at the ends of the span): hold the nearest moment, empty
            (double s, double e) = world.Span(Civ);
            double inside = Math.Min(Math.Max(year, s + 1e-3), e - 1e-3);
            Sample(world, Civ, wars, peakPopulation, inside, out center[k], out float _, out float _);
            women[k] = men[k] = 0;
        }

        /// <summary>Band center and envelope of a stream at a calendar year, straight from the shared model.</summary>
        public static bool Sample(HumanWorld world, Civ civ, CivWars wars, double peakPopulation, double year,
            out float bandCenter, out float envWomen, out float envMen)
        {
            bandCenter = envWomen = envMen = 0;
            if (!world.TryUnits(civ, year, out float lo, out float width)) return false;
            float perUnit = world.RhoPerUnit(year);
            bandCenter = HumanWorld.HumanRho0 + (lo + 0.5f * width) * perUnit;
            double population = world.WorldPopulation(year) * width / HumanWorld.TotalUnits;
            float fill = Mathf.Clamp((float)(population / Math.Max(peakPopulation, 1.0)), MinFill, 1f);
            envWomen = 0.5f * width * perUnit * Margin * fill;
            envMen = envWomen * wars.MaleFactor(year);
            return true;
        }
    }
}
