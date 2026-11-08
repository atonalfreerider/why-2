using System;
using System.Globalization;

namespace Why.Matter
{
    /// <summary>
    /// The scale of space where the centre of the screen meets the red layer, published by <see cref="ExpansionGridLayer"/>
    /// (main thread, from Tick) and shown above the HUD's legend, e.g. "Space here: 1 px² ≈ 10^6.2 m² · waves 10^5.0 m
    /// apart". The metres are physical, at that moment of the clock: the outward distance one pixel spans there (the screen's
    /// other direction is time, so the square is the outward scale squared), and the distance between the finest waves the
    /// grid has fully opened there. Hidden (not published) off the fan, where the waves there have faded (by the view, the
    /// lens or the grid's own fades), in a headset and while the graph grows from the Big Bang.
    ///
    /// Also the rule the grid's shader (Why/ScaleWaves) opens its waves by, mirrored here line for line so the readout names
    /// exactly the waves on screen. Wave k is the shell of matter 10^k .. 10^(k+1) m from us today; inside it the lines of
    /// level L (1: 2..9 x 10^k, 2: tenths, 3: hundredths, 4: thousandths) cut each cell of level L - 1 into ten equal parts,
    /// which a log map draws narrower and narrower toward the wave's outer crest. A line emerges as its gap to the next line of
    /// its level or coarser grows on screen from <see cref="EmergePx"/> to <see cref="OpenPx"/>, and fades to its level's
    /// floor as the next level opens in that gap (<see cref="Alpha"/>). The gaps on screen come from the decades of distance
    /// per pixel across the waves, which the shader takes from the screen-space derivatives of its wave coordinate.
    /// </summary>
    public static class SpaceLod
    {
        /// <summary>The scale of space at one point of the red layer, as the grid draws it there.</summary>
        public struct Reading
        {
            /// <summary>Where: clock arc and rho (data units).</summary>
            public float U, Rho;

            /// <summary>Years ago at that arc.</summary>
            public double YearsAgo;

            /// <summary>log10 of the metres one screen pixel spans outward there (physical, at that moment).</summary>
            public double Log10MetresPerPixel;

            /// <summary>The wave there: the matter lies 10^<see cref="ComovingLog10"/> m from us today.</summary>
            public double ComovingLog10;

            /// <summary>log10 of how much nearer that matter lay then than today: the scale factor and the collapse of the bodies
            /// around it still to come (the physical distance is 10^(ComovingLog10 + Log10Then) m).</summary>
            public double Log10Then;

            /// <summary>Decades of distance today per screen pixel across the waves (what the shader takes from ddx and ddy).</summary>
            public double DecadesPerPixel;

            /// <summary>The finest level fully open there (0: the decades .. <see cref="Levels"/> - 1), -1 when none is (crowded,
            /// or below the trunk where no wave is drawn).</summary>
            public int FinestLevel;

            /// <summary>log10 of the metres between neighbouring waves of that level there (physical, at that moment); NaN when
            /// no level is open.</summary>
            public double Log10WaveSpacing;

            /// <summary>The crest nearest the point (log10 m today) and its rho at that moment.</summary>
            public int CrestDecade;

            public float CrestRho;

            /// <summary>
            /// How much of the waves the grid's own fades leave at the point, 0..1, as the shader fades them per point: born on
            /// the inner track, dissolving toward the horizon, gone just outside a body's edge where the matter inside fell in.
            /// The view's emphasis, the dissolve ahead of the human branch and the lens window's fade come on top
            /// (<see cref="ExpansionGridLayer.Shows"/>).
            /// </summary>
            public float Fade;

            /// <summary>log10 of the area one screen pixel covers there (m²): the outward scale squared.</summary>
            public double Log10AreaPerPixel => 2 * Log10MetresPerPixel;
        }

        /// <summary>True while a reading of the centre of the screen is published.</summary>
        public static bool Published { get; private set; }

        /// <summary>The last reading published (meaningful while <see cref="Published"/>).</summary>
        public static Reading Current { get; private set; }

        /// <summary>Called by the grid each frame the centre of the screen lies on a part of the fan whose waves show.</summary>
        public static void Publish(Reading reading)
        {
            Current = reading;
            Published = true;
        }

        /// <summary>Nothing to read out: off the fan, faded, in a headset, during the opening, or the grid is gone.</summary>
        public static void Clear() => Published = false;

        // ------------------------------------------------------------------ the shader's rule (Why/ScaleWaves)

        /// <summary>Levels drawn: the decades, then 2..9 x 10^k, tenths, hundredths and thousandths.</summary>
        public const int Levels = 5;

        /// <summary>A line starts to emerge when its gap on screen reaches this many pixels (the old grid's crowding threshold).</summary>
        public const float EmergePx = 3f;

        /// <summary>...and is fully open at this many: the threshold the readout's "finest fully open" waves have passed.</summary>
        public const float OpenPx = 7.4f;

        /// <summary>Opacity of each level against the decades' (the finer, the fainter).</summary>
        public static readonly float[] Weight = { 1f, 0.6f, 0.3f, 0.16f, 0.09f };

        /// <summary>
        /// The share of its weight a level keeps once the next level has opened inside its cells, so the decades and the
        /// level just coarser than the open one stay legible: about three levels read at once. The finest level has none to
        /// hand over to and keeps its weight. The decades keep most of theirs, so each tenfold wave leads the ten inside it,
        /// as in the concept art's bright arcs over finer and finer cells.
        /// </summary>
        public static readonly float[] Floor = { 0.8f, 0.3f, 0.14f, 0.12f, 1f };

        /// <summary>Spacing of the lattice of level L inside a wave, in units of 10^k m today: 1 for 2..9 x 10^k, 0.1 for the
        /// tenths, down to 0.001 (the decades' lattice is the wave itself).</summary>
        public static double Step(int level) => level <= 1 ? 1.0 : Math.Pow(10, 1 - level);

        /// <summary>
        /// Decades from a line of level L at x x 10^k m today (1 &lt;= x &lt; 10) to the next line of its level or coarser:
        /// a whole decade for a crest, log10(1 + step / x) inside the wave.
        /// </summary>
        public static double Gap(int level, double x) => level == 0 ? 1.0 : Math.Log10(1 + Step(level) / x);

        /// <summary>Decades the next level opens beside a line of level L at x (its first cell); 0 for the finest level.</summary>
        public static double NextGap(int level, double x) => level + 1 >= Levels ? 0 : Math.Log10(1 + Step(level + 1) / x);

        /// <summary>0 while a gap on screen is under <see cref="EmergePx"/>, 1 from <see cref="OpenPx"/>, smooth in log between.</summary>
        public static float Emerge(double px)
        {
            double t = Math.Log(Math.Max(px, 1e-9) / EmergePx) / Math.Log(OpenPx / EmergePx);
            t = Math.Max(0, Math.Min(1, t));
            return (float)(t * t * (3 - 2 * t));
        }

        /// <summary>
        /// Opacity of a line of level L at x x 10^k m today at <paramref name="decadesPerPixel"/>, before the grid's fades: its
        /// level's weight, as far as its gap has opened, handing over to its floor as the next level opens beside it. The C#
        /// copy of the shader's level alpha.
        /// </summary>
        public static float Alpha(int level, double x, double decadesPerPixel)
        {
            double g = Math.Max(decadesPerPixel, 1e-12);
            float alpha = Weight[level] * Emerge(Gap(level, x) / g);
            if (level + 1 < Levels) alpha *= 1f + (Floor[level] - 1f) * Emerge(NextGap(level, x) / g);
            return alpha;
        }

        /// <summary>
        /// The finest level whose waves are fully open around the point <paramref name="log10Today"/> (decades today) at
        /// <paramref name="decadesPerPixel"/>: for each level, the cell that holds the point is open when the lines of that
        /// level bounding it are (a bound a coarser level draws is open already), each measured by its own gap as the shader
        /// measures it. A finer cell is always narrower, so the open levels run from the decades down. -1 when even the decades
        /// are crowded. <paramref name="log10Spacing"/>: log10 of the metres today between neighbouring waves of that level
        /// there (9 x 10^k between two crests, else the level's step x 10^k).
        /// </summary>
        public static int FinestOpenLevel(double log10Today, double decadesPerPixel, out double log10Spacing)
        {
            double g = Math.Max(decadesPerPixel, 1e-12);
            double k = Math.Floor(log10Today);
            double m = Math.Pow(10, log10Today - k);
            log10Spacing = double.NaN;
            int finest = -1;
            for (int level = 0; level < Levels; level++)
            {
                double step = Step(level);
                double x = 1;
                if (level > 0)
                {
                    // the cell's bounds on the level's lattice; the outer one has the narrower gap unless a coarser level draws it
                    double lo = Math.Max(1, Math.Floor(m / step + 1e-9)), hi = lo + 1;
                    x = (Math.Abs(hi % 10) < 1e-6 ? lo : hi) * step;
                }

                if (Gap(level, x) / g < OpenPx) break;
                finest = level;
                log10Spacing = k + Math.Log10(level == 0 ? 9 : step);
            }

            return finest;
        }

        // ------------------------------------------------------------------ text

        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        /// <summary>10^x with one decimal: "10<sup>6.2</sup>" for TextMeshPro rich text, "10^6.2" for plain text.</summary>
        public static string Power(double log10, bool rich)
        {
            string x = log10.ToString("0.0", Ci);
            return rich ? "10<sup>" + x + "</sup>" : "10^" + x;
        }

        /// <summary>
        /// "1 px² ≈ 10^6.2 m² · waves 10^5.0 m apart" (<paramref name="here"/>: "1 px² here ≈ ..."); without the waves where
        /// none is fully open.
        /// </summary>
        public static string Describe(Reading r, bool rich, bool here = false)
        {
            string text = "1 px²" + (here ? " here" : "") + " ≈ " + Power(r.Log10AreaPerPixel, rich) + " m²";
            if (r.FinestLevel >= 0 && !double.IsNaN(r.Log10WaveSpacing) && !double.IsInfinity(r.Log10WaveSpacing))
            {
                text += "  ·  waves " + Power(r.Log10WaveSpacing, rich) + " m apart";
            }

            return text;
        }
    }
}
