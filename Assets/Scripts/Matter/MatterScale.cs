using System;
using System.Collections.Generic;
using UnityEngine;

namespace Why.Matter
{
    /// <summary>
    /// The physical scale of the red layer: what a point of the matter plane stands for in metres. The layout
    /// already fixes it: every body on our lineage ends at the outer edge of its band with its real radius
    /// (<see cref="MatterItem.SizeM"/> / 2, bound bodies keeping their size, unbound structures and the observable
    /// universe growing with the cosmic scale factor), and everything expands with <see cref="MatterLayout.Envelope"/>.
    /// Between those knots the distance from our lineage grows exponentially with rho (linear in log10), so the
    /// map has a definite scale everywhere: metres per world unit outward. The grid draws this field and the HUD's
    /// probe reads it. The inner track is human scale by convention (<see cref="InnerMetres"/>): where we live.
    /// Pure data; safe on the worker thread once built.
    /// </summary>
    public sealed class MatterScale
    {
        /// <summary>Radius today of the observable universe (metres): the envelope's outer edge, comoving.</summary>
        public const double ObservableRadiusM = 4.4e26;

        /// <summary>Distance from our lineage at rho = 0 (metres): a person, where we live.</summary>
        public const double InnerMetres = 1.0;

        /// <summary>A knot of the map: a band's outer edge (world units) and the real radius there (metres).</summary>
        public struct Knot
        {
            public float Rho;
            public double Log10Radius;
            public string Name;
            public bool Bound;
        }

        readonly MatterLayout layout;
        readonly float[] inner, outer;
        readonly List<Knot> knots = new List<Knot>(16);
        float knotsArc = float.NaN, knotsEnvelope;

        public MatterScale(MatterLayout layout)
        {
            this.layout = layout;
            inner = new float[layout.Bands.Count];
            outer = new float[layout.Bands.Count];
        }

        /// <summary>The knots (inner track first, envelope last) at arc u, sorted outward, radii monotone. Reused; not thread safe.</summary>
        public IReadOnlyList<Knot> Knots(float u)
        {
            if (u == knotsArc) return knots;
            knotsArc = u;
            knots.Clear();
            layout.Evaluate(u, inner, outer, out _, out knotsEnvelope);
            knots.Add(new Knot { Rho = 0, Log10Radius = Math.Log10(InnerMetres), Name = "our lineage", Bound = true });

            float a = layout.ScaleFactor(u);
            foreach (MatterBand band in layout.Bands)
            {
                if (band.IsRootHome || !band.InPath || band.Item.SizeM <= 0) continue;
                float width = outer[band.Index] - inner[band.Index];
                if (width <= 1e-5f || !band.AliveAt(u)) continue;
                double radius = 0.5 * band.Item.SizeM;
                if (!band.Item.Bound)
                {
                    // an unbound structure is carried by the expansion: its size today, scaled back
                    radius *= Mathf.Max(a, 1e-12f);
                }

                knots.Add(new Knot { Rho = outer[band.Index], Log10Radius = Math.Log10(radius), Name = band.Item.DisplayName, Bound = band.Item.Bound });
            }

            knots.Add(new Knot
            {
                Rho = knotsEnvelope, Log10Radius = Math.Log10(ObservableRadiusM * Mathf.Max(a, 1e-12f)),
                Name = "observable universe", Bound = false
            });

            knots.Sort((x, y) => x.Rho.CompareTo(y.Rho));
            // a body still forming can be narrower than its size implies: keep the map monotone outward
            for (int i = 1; i < knots.Count; i++)
            {
                Knot k = knots[i];
                Knot p = knots[i - 1];
                if (k.Log10Radius < p.Log10Radius + 1e-6) k.Log10Radius = p.Log10Radius + 1e-6;
                if (k.Rho < p.Rho + 1e-6f) k.Rho = p.Rho + 1e-6f;
                knots[i] = k;
            }

            return knots;
        }

        /// <summary>The red envelope's outer edge (world units) at the arc of the last <see cref="Knots"/> call.</summary>
        public float EnvelopeAt(float u)
        {
            Knots(u);
            return knotsEnvelope;
        }

        /// <summary>log10 of the observable universe's radius (metres) at arc u.</summary>
        public double Log10UniverseRadius(float u) => Math.Log10(ObservableRadiusM * Mathf.Max(layout.ScaleFactor(u), 1e-12f));

        /// <summary>log10 of the distance from our lineage (metres) at (u, rho); extrapolated beyond the envelope.</summary>
        public double Log10Radius(float u, float rho)
        {
            IReadOnlyList<Knot> k = Knots(u);
            int i = Segment(k, rho);
            Knot a = k[i], b = k[i + 1];
            return a.Log10Radius + (rho - a.Rho) / (b.Rho - a.Rho) * (b.Log10Radius - a.Log10Radius);
        }

        /// <summary>Distance from our lineage (metres) at (u, rho).</summary>
        public double Radius(float u, float rho) => Math.Pow(10, Log10Radius(u, rho));

        /// <summary>Metres per world unit outward at (u, rho): the local scale of the map.</summary>
        public double MetresPerUnit(float u, float rho)
        {
            IReadOnlyList<Knot> k = Knots(u);
            int i = Segment(k, rho);
            Knot a = k[i], b = k[i + 1];
            double decadesPerUnit = (b.Log10Radius - a.Log10Radius) / (b.Rho - a.Rho);
            return Radius(u, rho) * Math.Log(10) * decadesPerUnit;
        }

        /// <summary>The rho at which the distance from our lineage is 10^<paramref name="log10Radius"/> metres, or -1 beyond the envelope.</summary>
        public float Rho(float u, double log10Radius)
        {
            IReadOnlyList<Knot> k = Knots(u);
            if (log10Radius > k[k.Count - 1].Log10Radius) return -1f;
            if (log10Radius <= k[0].Log10Radius) return 0f;
            for (int i = 0; i < k.Count - 1; i++)
            {
                Knot a = k[i], b = k[i + 1];
                if (log10Radius > b.Log10Radius) continue;
                return a.Rho + (float)((log10Radius - a.Log10Radius) / (b.Log10Radius - a.Log10Radius)) * (b.Rho - a.Rho);
            }

            return k[k.Count - 1].Rho;
        }

        /// <summary>The body whose region holds rho at arc u (the knot just outside), for the probe's readout.</summary>
        public Knot Region(float u, float rho)
        {
            IReadOnlyList<Knot> k = Knots(u);
            return k[Segment(k, rho) + 1];
        }

        /// <summary>
        /// log10 of the radius (metres) of the outermost gravitationally bound region of our lineage at arc u,
        /// or -1 when none has formed: outside it matter still rides the expansion of space (the comoving rays).
        /// </summary>
        public double Log10BoundRadius(float u)
        {
            IReadOnlyList<Knot> k = Knots(u);
            double best = -1;
            for (int i = 1; i < k.Count; i++)
            {
                if (k[i].Bound) best = Math.Max(best, k[i].Log10Radius);
            }

            return best;
        }

        /// <summary>Years of the clock per world unit along the path at arc u (the map's scale along time).</summary>
        public static double YearsPerUnit(float u)
        {
            float du = 1e-4f;
            double ya0 = DeepTime.YearsAgo(Mathf.Min(u + du, 1f)), ya1 = DeepTime.YearsAgo(Mathf.Max(u - du, 1e-6f));
            return Math.Abs(ya0 - ya1) / (2 * du * GraphWarp.BasePath.SigmaPerArc);
        }

        static int Segment(IReadOnlyList<Knot> k, float rho)
        {
            int i = 0;
            while (i < k.Count - 2 && rho > k[i + 1].Rho) i++;
            return i;
        }
    }
}
