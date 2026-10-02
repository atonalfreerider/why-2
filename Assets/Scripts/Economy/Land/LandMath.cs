using System;
using System.Collections.Generic;
using UnityEngine;

namespace Why.Economy.Land
{
    /// <summary>
    /// Small deterministic math shared by the land's builders and layers: hashes instead of random numbers (8.7), angles
    /// on the circle, the circular mean, the minimum-displacement packing of a rim row (3.3), path smoothing (Catmull-Rom,
    /// Chaikin), arc-length sampling, eases, and the vertex-color helpers. Pure functions; safe on any thread.
    /// </summary>
    public static class LandMath
    {
        // ------------------------------------------------------------------ hashes (no System.Random, no UnityEngine.Random)

        /// <summary>A well-mixed hash of two integers (the money threads' hash, moved here unchanged).</summary>
        public static uint Hash(int a, int b)
        {
            uint h = (uint)a * 0x9E3779B1u ^ (uint)b * 0x85EBCA77u;
            h ^= h >> 15;
            h *= 0x2C1B3C6Du;
            h ^= h >> 12;
            h *= 0x297A2D39u;
            h ^= h >> 15;
            return h;
        }

        /// <summary>A hash of two integers as a number in [0, 1) (24 bits): deterministic draws, phases, jitter.</summary>
        public static float Hash01(int a, int b) => (Hash(a, b) & 0xFFFFFF) / 16777216f;

        /// <summary>One step of SplitMix64: a 64-bit mix for longer deterministic sequences (state is advanced).</summary>
        public static ulong SplitMix64(ref ulong state)
        {
            ulong z = state += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        /// <summary>A SplitMix64 draw as a double in [0, 1) (53 bits).</summary>
        public static double SplitMix01(ref ulong state) => (SplitMix64(ref state) >> 11) * (1.0 / 9007199254740992.0);

        /// <summary>The k-th point of the golden-ratio sequence in [0, 1) (evenly spread, deterministic: glitter along an edge).</summary>
        public static float Golden(int k, float start = 0) => (float)((start + k * 0.6180339887498949) % 1.0);

        // ------------------------------------------------------------------ angles (degrees)

        /// <summary>An angle in [0, 360).</summary>
        public static float Wrap360(float deg)
        {
            float r = deg % 360f;
            return r < 0 ? r + 360f : r;
        }

        public static double Wrap360(double deg)
        {
            double r = deg % 360.0;
            return r < 0 ? r + 360.0 : r;
        }

        /// <summary>The signed shorter angle from a to b (degrees, in [-180, 180)).</summary>
        public static float DeltaDeg(float a, float b) => Wrap360(b - a + 180f) - 180f;

        public static double DeltaDeg(double a, double b) => Wrap360(b - a + 180.0) - 180.0;

        /// <summary>
        /// The weighted circular mean of angles (degrees), in [0, 360); the fallback when the weights cancel or are all
        /// zero.
        /// </summary>
        public static double CircularMean(IReadOnlyList<double> degrees, IReadOnlyList<double> weights, double fallback = 0)
        {
            double x = 0, z = 0;
            for (int i = 0; i < degrees.Count; i++)
            {
                double w = weights == null ? 1 : weights[i];
                double a = degrees[i] * Math.PI / 180;
                x += w * Math.Cos(a);
                z += w * Math.Sin(a);
            }

            return x * x + z * z < 1e-24 ? fallback : Wrap360(Math.Atan2(z, x) * 180 / Math.PI);
        }

        // ------------------------------------------------------------------ the rim's row packing (3.3)

        /// <summary>
        /// Exact minimum-displacement packing of discs on a circle of radius <paramref name="radius"/> (O(n) after the
        /// sort): sort by desired angle (ties by <paramref name="tieRank"/>, then index), cut the circle at the largest gap
        /// between consecutive desired angles and unwrap, require gaps (pad + e_i + e_i+1) / R between neighbours, and
        /// fit the desired angles minus the cumulative gaps with an isotonic regression (pool-adjacent-violators), adding
        /// the gaps back. Returns each item's packed angle (degrees, [0, 360)) in input order; <paramref name="shift"/>
        /// gets each item's displacement along the circle (world units). Reproduces the prototype synth/rim_spec.py.
        /// </summary>
        public static double[] PackRow(IReadOnlyList<double> desiredDeg, IReadOnlyList<double> extent, double radius,
            double pad, IReadOnlyList<int> tieRank, out double[] shift)
        {
            int n = desiredDeg.Count;
            double[] result = new double[n];
            shift = new double[n];
            if (n == 0) return result;
            const double Turn = 2 * Math.PI;
            int[] order = new int[n];
            double[] want = new double[n];
            for (int i = 0; i < n; i++)
            {
                order[i] = i;
                want[i] = Wrap360(desiredDeg[i]) * Math.PI / 180;
            }

            Array.Sort(order, (p, q) =>
            {
                int c = want[p].CompareTo(want[q]);
                if (c != 0) return c;
                c = (tieRank == null ? 0 : tieRank[p]).CompareTo(tieRank == null ? 0 : tieRank[q]);
                return c != 0 ? c : p.CompareTo(q);
            });

            // cut at the largest gap between consecutive desired angles (the first one on ties) and unwrap after it
            int cut = 0;
            double largest = -1;
            for (int i = 0; i < n; i++)
            {
                double gap = ((want[order[(i + 1) % n]] - want[order[i]]) % Turn + Turn) % Turn;
                if (gap > largest)
                {
                    largest = gap;
                    cut = i;
                }
            }

            int[] idx = new int[n];
            double[] x = new double[n];
            for (int i = 0; i < n; i++)
            {
                idx[i] = order[(cut + 1 + i) % n];
                double w = want[idx[i]];
                if (i == 0) x[i] = w;
                else
                {
                    // numpy.unwrap: the step to the previous angle taken the shorter way
                    double d = w - (x[i - 1] % Turn + Turn) % Turn;
                    d = ((d + Math.PI) % Turn + Turn) % Turn - Math.PI;
                    x[i] = x[i - 1] + d;
                }
            }

            // required offsets, then pool adjacent violators on x - offsets
            double[] offs = new double[n];
            for (int i = 1; i < n; i++) offs[i] = offs[i - 1] + (pad + extent[idx[i - 1]] + extent[idx[i]]) / radius;
            List<double> blockValue = new List<double>(n);
            List<int> blockCount = new List<int>(n);
            for (int i = 0; i < n; i++)
            {
                blockValue.Add(x[i] - offs[i]);
                blockCount.Add(1);
                while (blockValue.Count > 1 && blockValue[blockValue.Count - 2] > blockValue[blockValue.Count - 1])
                {
                    int last = blockValue.Count - 1;
                    double v2 = blockValue[last], v1 = blockValue[last - 1];
                    int n2 = blockCount[last], n1 = blockCount[last - 1];
                    blockValue.RemoveAt(last);
                    blockCount.RemoveAt(last);
                    blockValue[last - 1] = (v1 * n1 + v2 * n2) / (n1 + n2);
                    blockCount[last - 1] = n1 + n2;
                }
            }

            int k = 0;
            for (int b = 0; b < blockValue.Count; b++)
            {
                for (int j = 0; j < blockCount[b]; j++, k++)
                {
                    double a = blockValue[b] + offs[k];
                    result[idx[k]] = Wrap360(a * 180 / Math.PI);
                    shift[idx[k]] = Math.Abs(a - x[k]) * radius;
                }
            }

            return result;
        }

        // ------------------------------------------------------------------ paths

        /// <summary>
        /// A uniform Catmull-Rom spline through the nodes, <paramref name="perSegment"/> points per segment (the nodes
        /// themselves included, the last node once). Fewer than three nodes are copied.
        /// </summary>
        public static List<Vector3> CatmullRom(IReadOnlyList<Vector3> nodes, int perSegment)
        {
            List<Vector3> o = new List<Vector3>();
            int n = nodes.Count;
            if (n < 3 || perSegment < 1)
            {
                for (int i = 0; i < n; i++) o.Add(nodes[i]);
                return o;
            }

            for (int i = 0; i < n - 1; i++)
            {
                Vector3 p0 = nodes[Math.Max(0, i - 1)], p1 = nodes[i], p2 = nodes[i + 1], p3 = nodes[Math.Min(n - 1, i + 2)];
                for (int s = 0; s < perSegment; s++)
                {
                    float t = s / (float)perSegment, t2 = t * t, t3 = t2 * t;
                    o.Add(0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3));
                }
            }

            o.Add(nodes[n - 1]);
            return o;
        }

        /// <summary>Chaikin corner cutting, the end points kept (removes the miter spikes of sharp joins).</summary>
        public static List<Vector3> Chaikin(IReadOnlyList<Vector3> points, int passes)
        {
            List<Vector3> cur = new List<Vector3>(points);
            for (int p = 0; p < passes && cur.Count >= 3; p++)
            {
                List<Vector3> next = new List<Vector3>(cur.Count * 2) { cur[0] };
                for (int i = 0; i < cur.Count - 1; i++)
                {
                    next.Add(Vector3.Lerp(cur[i], cur[i + 1], 0.25f));
                    next.Add(Vector3.Lerp(cur[i], cur[i + 1], 0.75f));
                }

                next.Add(cur[cur.Count - 1]);
                cur = next;
            }

            return cur;
        }

        /// <summary>Length of a polyline.</summary>
        public static float ArcLength(IReadOnlyList<Vector3> points)
        {
            float s = 0;
            for (int i = 1; i < points.Count; i++) s += Vector3.Distance(points[i - 1], points[i]);
            return s;
        }

        /// <summary>The point at a share (0..1) of a polyline's length, and the unit direction there.</summary>
        public static Vector3 PointAlong(IReadOnlyList<Vector3> points, float share, out Vector3 direction)
        {
            direction = Vector3.forward;
            if (points.Count == 0) return Vector3.zero;
            if (points.Count == 1) return points[0];
            float target = Mathf.Clamp01(share) * ArcLength(points), s = 0;
            for (int i = 1; i < points.Count; i++)
            {
                float d = Vector3.Distance(points[i - 1], points[i]);
                if (d > 1e-7f) direction = (points[i] - points[i - 1]) / d;
                if (s + d >= target && d > 1e-7f) return Vector3.Lerp(points[i - 1], points[i], (target - s) / d);
                s += d;
            }

            return points[points.Count - 1];
        }

        /// <summary>A polyline resampled to n points evenly spaced along its length (n ≥ 2).</summary>
        public static Vector3[] Resample(IReadOnlyList<Vector3> points, int n)
        {
            n = Math.Max(2, n);
            Vector3[] o = new Vector3[n];
            for (int k = 0; k < n; k++) o[k] = PointAlong(points, k / (float)(n - 1), out _);
            return o;
        }

        /// <summary>A quadratic Bézier point.</summary>
        public static Vector3 Bezier(Vector3 p0, Vector3 control, Vector3 p1, float t)
        {
            float u = 1 - t;
            return u * u * p0 + 2 * u * t * control + t * t * p1;
        }

        // ------------------------------------------------------------------ eases

        /// <summary>Ease in-out cubic on [0, 1].</summary>
        public static float EaseInOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.5f ? 4 * t * t * t : 1 - Mathf.Pow(-2 * t + 2, 3) / 2;
        }

        /// <summary>The progress (0..1) of a phase [start, end] at a time.</summary>
        public static float Phase(float time, float start, float end) => end > start ? Mathf.Clamp01((time - start) / (end - start)) : time >= end ? 1 : 0;

        // ------------------------------------------------------------------ colors

        /// <summary>An LDR color with an alpha, as vertex colors store it.</summary>
        public static Color32 Tint(Color c, float alpha) =>
            new Color32((byte)(Mathf.Clamp01(c.r) * 255), (byte)(Mathf.Clamp01(c.g) * 255), (byte)(Mathf.Clamp01(c.b) * 255),
                (byte)(Mathf.Clamp01(alpha) * 255));

        /// <summary>The same color with its alpha scaled (fading a shape out).</summary>
        public static Color32 Fade(Color32 c, float factor) =>
            new Color32(c.r, c.g, c.b, (byte)Mathf.Clamp(Mathf.RoundToInt(c.a * factor), 0, 255));
    }
}
