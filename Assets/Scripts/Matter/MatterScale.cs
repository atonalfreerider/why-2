using System;
using System.Collections.Generic;
using UnityEngine;

namespace Why.Matter
{
    /// <summary>
    /// The physical scale of the red layer: what a point of the matter plane stands for in metres. It reads
    /// <see cref="MatterLayout"/>'s comb, one continuous field with no knots: at every moment the distance from our lineage
    /// grows exponentially with rho (linear in log10) at <see cref="UnitsPerDecade"/> world units a decade, from the trunk at
    /// rho = 0 (a comoving kilometre, 10^3 m today, scaled back by the cosmic scale factor: <see cref="HomeLog10"/>) to the
    /// horizon at the envelope's edge (the observable universe, scaled back: <see cref="OuterLog10"/>). So the map has a
    /// definite scale everywhere, metres per world unit outward, and a body's band ends on the comb where its edge lies
    /// (<see cref="MatterLayout.EdgeLog10"/>). The grid's waves follow the same field (<see cref="ExpansionGridLayer"/>, which
    /// adds the collapse of our lineage's bound bodies to the matter they enclose) and the HUD's probe reads it.
    /// Pure data; safe on the worker thread once built, but one instance per thread for what reads the bands
    /// (<see cref="Knots"/>, <see cref="EnvelopeAt"/>, <see cref="Region"/>, <see cref="Log10BoundRadius"/>: they reuse a list).
    /// </summary>
    public sealed class MatterScale
    {
        /// <summary>Radius today of the observable universe (metres): the envelope's outer edge, comoving.</summary>
        public const double ObservableRadiusM = MatterLayout.ObservableRadiusM;

        /// <summary>
        /// A body's band on the comb at some moment, for the probe's "inside" readout: the band's outer edge (world units)
        /// and the distance from our lineage there (log10 metres), from the inner track out to the horizon.
        /// </summary>
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
            knots.Add(new Knot { Rho = 0, Log10Radius = HomeLog10(u), Name = "our lineage", Bound = true });

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

        // ------------------------------------------------------------------ the continuous scale (MatterLayout's comb)

        /// <summary>log10 of the physical distance (metres) at the trunk at arc u.</summary>
        public double HomeLog10(float u) => layout.InnerLog10(u);

        /// <summary>log10 radius (metres) of the horizon at arc u: the observable universe, scaled back.</summary>
        public double OuterLog10(float u) => layout.OuterLog10(u);

        /// <summary>World units per decade of comoving distance at arc u.</summary>
        public double UnitsPerDecade(float u) => layout.UnitsPerDecade(u);

        /// <summary>log10 of the distance from our lineage (metres) at (u, rho), reading the comb as comoving distance scaled back.</summary>
        public double Log10Radius(float u, float rho) => HomeLog10(u) + rho / UnitsPerDecade(u);

        /// <summary>Distance from our lineage (metres) at (u, rho).</summary>
        public double Radius(float u, float rho) => Math.Pow(10, Log10Radius(u, rho));

        /// <summary>Metres per world unit outward at (u, rho): the local scale of the map.</summary>
        public double MetresPerUnit(float u, float rho) => Radius(u, rho) * Math.Log(10) / UnitsPerDecade(u);

        /// <summary>log10 of <see cref="MetresPerUnit"/>, without forming the power (the grid's readout of metres per pixel).</summary>
        public double Log10MetresPerUnit(float u, float rho) => Log10Radius(u, rho) + Math.Log10(Math.Log(10)) - Math.Log10(UnitsPerDecade(u));

        /// <summary>The rho at which the distance from our lineage is 10^<paramref name="log10Radius"/> metres, or -1 beyond the envelope.</summary>
        public float Rho(float u, double log10Radius)
        {
            float rho = layout.Rho(u, log10Radius);
            if (rho > EnvelopeAt(u)) return -1f;
            return Mathf.Max(rho, 0);
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
