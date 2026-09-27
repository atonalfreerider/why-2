using System;
using System.Collections.Generic;
using UnityEngine;

namespace Why.Humans.Lives
{
    /// <summary>
    /// Where each famous figure's lifeline runs, in data space (u, y, rho) by calendar year. The streams drift
    /// outward as the human layer widens with population, so a single anchor point cannot follow a life: the
    /// figure tracker's camera and the influence threads can use this to stay on the drawn line. Filled on the
    /// worker thread by <see cref="FigureLines"/>, read-only afterwards (safe to read from any thread), and
    /// shared through the graph context as <see cref="SharedKey"/>.
    /// </summary>
    public sealed class FigurePaths
    {
        /// <summary>Graph context key (<see cref="GraphContext.Shared{T}"/>).</summary>
        public const string SharedKey = "humans.figurePaths";

        readonly Dictionary<string, Path> paths = new Dictionary<string, Path>(StringComparer.Ordinal);

        sealed class Path
        {
            public double[] Years;
            public Vector3[] Points;
        }

        /// <summary>Figures with a drawn lifeline.</summary>
        public int Count => paths.Count;

        /// <summary>Records a figure's lifeline: calendar years (ascending) and their data-space points.</summary>
        internal void Add(string figureId, List<double> years, List<Vector3> points)
        {
            if (string.IsNullOrEmpty(figureId) || years.Count == 0 || years.Count != points.Count) return;
            paths[figureId] = new Path { Years = years.ToArray(), Points = points.ToArray() };
        }

        /// <summary>
        /// The data-space point of a figure's lifeline at a calendar year (negative = BCE), interpolated between
        /// the drawn points and clamped to the life. False if the figure has no drawn lifeline.
        /// </summary>
        public bool TryGetPoint(string figureId, double year, out Vector3 data)
        {
            data = default;
            if (figureId == null || !paths.TryGetValue(figureId, out Path p)) return false;
            double[] years = p.Years;
            int n = years.Length;
            if (year <= years[0])
            {
                data = p.Points[0];
                return true;
            }

            if (year >= years[n - 1])
            {
                data = p.Points[n - 1];
                return true;
            }

            int lo = 0, hi = n - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (years[mid] <= year) lo = mid;
                else hi = mid;
            }

            float f = (float)((year - years[lo]) / Math.Max(years[hi] - years[lo], 1e-9));
            data = Vector3.Lerp(p.Points[lo], p.Points[hi], f);
            return true;
        }
    }
}
