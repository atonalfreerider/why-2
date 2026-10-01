using System;
using System.Runtime.CompilerServices;

namespace Why.Economy.Model
{
    /// <summary>
    /// Small numeric helpers of the economic lives: the normal distribution, the logistic curve, a Cholesky factor and
    /// the per-person seeds. Pure functions (any thread).
    /// </summary>
    internal static class LivesMath
    {
        /// <summary>
        /// MethodImplOptions.AggressiveOptimization (512; the member is missing from Unity's .NET Standard 2.1 profile):
        /// the economic lives' hot loops run once at load, so a tiering JIT (CoreCLR, the preview harness) should compile
        /// them fully optimized at once instead of starting them unoptimized; runtimes that always optimize (Mono,
        /// IL2CPP) ignore it.
        /// </summary>
        internal const MethodImplOptions Hot = (MethodImplOptions)512;

        /// <summary>Standard normal CDF (Abramowitz and Stegun 26.2.17; absolute error below 7.5e-8).</summary>
        [MethodImpl(LivesMath.Hot)]
        public static double Phi(double x)
        {
            double ax = Math.Abs(x);
            double t = 1.0 / (1.0 + 0.2316419 * ax);
            double poly = t * (0.319381530 + t * (-0.356563782 + t * (1.781477937 + t * (-1.821255978 + t * 1.330274429))));
            double tail = 0.3989422804014327 * Math.Exp(-0.5 * ax * ax) * poly;
            return x >= 0 ? 1.0 - tail : tail;
        }

        /// <summary>Inverse standard normal CDF (Acklam's rational approximation, relative error below 1.2e-9).</summary>
        [MethodImpl(LivesMath.Hot)]
        public static double InvPhi(double p)
        {
            if (p <= 0) return -8.0;
            if (p >= 1) return 8.0;
            const double plow = 0.02425;
            if (p < plow)
            {
                double q = Math.Sqrt(-2 * Math.Log(p));
                return (((((-7.784894002430293e-03 * q - 3.223964580411365e-01) * q - 2.400758277161838e+00) * q -
                          2.549732539343734e+00) * q + 4.374664141464968e+00) * q + 2.938163982698783e+00) /
                       ((((7.784695709041462e-03 * q + 3.224671290700398e-01) * q + 2.445134137142996e+00) * q +
                         3.754408661907416e+00) * q + 1);
            }

            if (p > 1 - plow)
            {
                double q = Math.Sqrt(-2 * Math.Log(1 - p));
                return -(((((-7.784894002430293e-03 * q - 3.223964580411365e-01) * q - 2.400758277161838e+00) * q -
                           2.549732539343734e+00) * q + 4.374664141464968e+00) * q + 2.938163982698783e+00) /
                       ((((7.784695709041462e-03 * q + 3.224671290700398e-01) * q + 2.445134137142996e+00) * q +
                         3.754408661907416e+00) * q + 1);
            }

            double r = p - 0.5, s = r * r;
            return (((((-3.969683028665376e+01 * s + 2.209460984245205e+02) * s - 2.759285104469687e+02) * s +
                       1.383577518672690e+02) * s - 3.066479806614716e+01) * s + 2.506628277459239e+00) * r /
                   (((((-5.447609879822406e+01 * s + 1.615858368580409e+02) * s - 1.556989798598866e+02) * s +
                      6.680131188771972e+01) * s - 1.328068155288572e+01) * s + 1);
        }

        public static double Sigmoid(double x) => 1.0 / (1.0 + Math.Exp(-x));

        public static double Logit(double p)
        {
            p = Clamp(p, 1e-6, 1 - 1e-6);
            return Math.Log(p / (1 - p));
        }

        public static double Clamp(double x, double lo, double hi) => x < lo ? lo : x > hi ? hi : x;

        public static double Clamp01(double x) => x < 0 ? 0 : x > 1 ? 1 : x;

        /// <summary>Hermite step from 0 at <paramref name="e0"/> to 1 at <paramref name="e1"/>.</summary>
        public static double SmoothStep(double e0, double e1, double x)
        {
            double t = Clamp01((x - e0) / (e1 - e0));
            return t * t * (3 - 2 * t);
        }

        /// <summary>A standard normal draw from two uniforms (Box-Muller, always two draws so streams stay aligned).</summary>
        public static double Normal(Random rng)
        {
            double u1 = 1.0 - rng.NextDouble(), u2 = rng.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        }

        /// <summary>
        /// Lower-triangular L with L L^T = <paramref name="a"/> (n x n, symmetric); false when the matrix is not
        /// positive definite (then L is the identity).
        /// </summary>
        public static bool Cholesky(double[,] a, int n, out double[,] l)
        {
            l = new double[n, n];
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j <= i; j++)
                {
                    double sum = a[i, j];
                    for (int k = 0; k < j; k++) sum -= l[i, k] * l[j, k];
                    if (i == j)
                    {
                        if (sum <= 1e-9)
                        {
                            l = new double[n, n];
                            for (int d = 0; d < n; d++) l[d, d] = 1;
                            return false;
                        }

                        l[i, i] = Math.Sqrt(sum);
                    }
                    else
                    {
                        l[i, j] = sum / l[j, j];
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Seed of a person's own random stream: the model seed and the person's birth-order index mixed by a 32-bit
        /// avalanche hash (so neighbors' streams are unrelated), one stream per purpose.
        /// </summary>
        public static int SeedOf(int seed, int index, int stream)
        {
            unchecked
            {
                uint h = (uint)seed * 0x9E3779B1u ^ (uint)(index * 7919) ^ (uint)stream * 0x85EBCA77u;
                h ^= h >> 16;
                h *= 0x7FEB352Du;
                h ^= h >> 15;
                h *= 0x846CA68Bu;
                h ^= h >> 16;
                return (int)(h & 0x7FFFFFFF);
            }
        }
    }
}
