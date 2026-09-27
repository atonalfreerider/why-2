using System;
using System.Collections.Generic;
using UnityEngine;

namespace Why.Domination
{
    /// <summary>One stream of a stacked layer (e.g. cattle, or metal ores).</summary>
    public sealed class Stream
    {
        public string Id;
        public string Name;
        public Series Values;
        public string Blurb;
    }

    /// <summary>
    /// Builds a stacked, exploding layer along the human era: streams are stacked outward from the inner
    /// track in order, the total width follows a power of the total quantity (like the human layer follows
    /// population), and the layer fans out to transparent black - no outer border.
    /// Pure managed code (worker thread).
    /// </summary>
    public static class StackedStreams
    {
        /// <summary>
        /// Width (world units at rhoScale 1) for a quantity, on the same power law as the human layer:
        /// reference quantity -> reference width.
        /// </summary>
        public static float Width(double quantity, double reference, float referenceWidth, double exponent = 0.8) =>
            quantity <= 0 ? 0 : (float)(referenceWidth * Math.Pow(quantity / Math.Max(reference, 1e-12), exponent));

        /// <summary>Result: per-stream label points and id ranges, and the band of any stream at any year.</summary>
        public sealed class Built
        {
            public readonly List<(Stream stream, Vector3 labelData, IdRange ids, double labelYear)> Streams =
                new List<(Stream, Vector3, IdRange, double)>();

            internal IReadOnlyList<Stream> Source;
            internal Func<double, float> Total;
            internal Func<double, float> Base;

            /// <summary>Radial band [lo, hi] of stream i at a calendar year (same math as the geometry).</summary>
            public (float lo, float hi) BandAt(int i, double year)
            {
                double sum = 0;
                for (int k = 0; k < Source.Count; k++) sum += Source[k].Values.At(year);
                float b = Base(year);
                if (sum <= 0) return (b, b);
                float w = Total(year);
                float acc = b;
                for (int k = 0; k < i; k++) acc += (float)(w * Source[k].Values.At(year) / sum);
                return (acc, acc + (float)(w * Source[i].Values.At(year) / sum));
            }

            /// <summary>Outer edge of the whole stack at a year.</summary>
            public float OuterAt(double year)
            {
                double sum = 0;
                for (int k = 0; k < Source.Count; k++) sum += Source[k].Values.At(year);
                return Base(year) + (sum > 0 ? Total(year) : 0);
            }
        }

        /// <param name="baseRho">inner edge of the stack over time (null = the inner track, rho 0)</param>
        /// <param name="fadeOuter">whether the outermost stream fades to transparent (no border)</param>
        public static Built Build(IReadOnlyList<Stream> streams, Func<double, float> totalWidth, double startYear,
            double nowYear, float y, int idBase, float fadeRadius, SurfaceMeshBuilder fills, LineMeshBuilder lines,
            Func<double, float> baseRho = null, bool fadeOuter = true)
        {
            baseRho ??= _ => 0f;
            Built result = new Built { Source = streams, Total = totalWidth, Base = baseRho };
            List<Series> all = new List<Series>();
            foreach (Stream s in streams) all.Add(s.Values);

            // sampling: coarse in antiquity, dense in the last centuries where the explosion happens
            List<double> years = new List<double>();
            foreach (double yr in Series.SampleYears(startYear, nowYear, all, 250))
            {
                years.Add(yr);
            }

            SortedSet<double> dense = new SortedSet<double>(years);
            for (double yr = Math.Max(startYear, 1500); yr < nowYear; yr += yr < 1800 ? 25 : yr < 1900 ? 10 : 2) dense.Add(yr);
            dense.Add(nowYear);
            years = new List<double>(dense);

            int n = streams.Count;
            int m = years.Count;
            float[,] lo = new float[n, m], hi = new float[n, m];
            float[] us = new float[m];
            float[] widths = new float[n];
            float maxWidth = 0;
            for (int j = 0; j < m; j++)
            {
                double year = years[j];
                us[j] = DeepTime.Arc(Math.Max(nowYear - year, 1e-6));
                double sum = 0;
                for (int i = 0; i < n; i++) sum += streams[i].Values.At(year);
                float w = sum > 0 ? totalWidth(year) : 0;
                maxWidth = Mathf.Max(maxWidth, w);
                float acc = baseRho(year);
                for (int i = 0; i < n; i++)
                {
                    float wi = sum > 0 ? (float)(w * streams[i].Values.At(year) / sum) : 0;
                    lo[i, j] = acc;
                    hi[i, j] = acc + wi;
                    acc += wi;
                    if (wi > widths[i]) widths[i] = wi;
                }
            }

            fadeRadius = Mathf.Max(fadeRadius, 1e-3f);
            List<Vector3> inner = new List<Vector3>(m), outer = new List<Vector3>(m);
            List<Color32> cols = new List<Color32>(m);
            List<LinePoint> edge = new List<LinePoint>(m);
            for (int i = 0; i < n; i++)
            {
                inner.Clear();
                outer.Clear();
                cols.Clear();
                edge.Clear();
                int bestJ = 0;
                float best = 0;
                bool outermost = fadeOuter && i == n - 1;
                for (int j = 0; j < m; j++)
                {
                    float mid = 0.5f * (lo[i, j] + hi[i, j]);
                    float fall = Mathf.Exp(-Mathf.Pow(mid / fadeRadius, 2f));
                    inner.Add(new Vector3(us[j], y, lo[i, j]));
                    outer.Add(new Vector3(us[j], y, hi[i, j]));
                    cols.Add(new Color32(255, 255, 255, (byte)(255 * Mathf.Clamp01(0.55f * fall + 0.05f))));

                    float edgeFall = Mathf.Exp(-Mathf.Pow(lo[i, j] / fadeRadius, 2f));
                    byte ea = (byte)(255 * Mathf.Clamp01((i == 0 && lo[i, j] < 1e-4f ? 0.9f : 0.45f) * edgeFall));
                    edge.Add(new LinePoint(new Vector3(us[j], y + 0.001f, lo[i, j]), new Color32(255, 255, 255, ea),
                        i == 0 ? 1.4f : 0.9f, 0, i == 0 ? 1.6f : 1f));

                    float w = hi[i, j] - lo[i, j];
                    // label where the stream is wide but not yet lost in the fade
                    float score = w * fall;
                    if (score > best)
                    {
                        best = score;
                        bestJ = j;
                    }
                }

                int id = idBase + i;
                if (outermost)
                {
                    // the outer boundary dissolves: the last band fades to transparent across its width
                    List<Color32> clear = new List<Color32>(cols.Count);
                    foreach (Color32 c in cols) clear.Add(new Color32(c.r, c.g, c.b, 0));
                    fills.AddBand(inner, outer, cols, clear, id, 1f, 0.15f);
                }
                else
                {
                    fills.AddBand(inner, outer, cols, id, 1f, 0.15f);
                }

                lines.AddPolyline(edge, id);
                Vector3 label = new Vector3(us[bestJ], y, 0.5f * (lo[i, bestJ] + hi[i, bestJ]));
                result.Streams.Add((streams[i], label, IdRange.Single(id), years[bestJ]));
            }

            return result;
        }
    }
}
