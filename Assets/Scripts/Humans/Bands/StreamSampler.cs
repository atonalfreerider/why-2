using System;
using System.Collections.Generic;
using UnityEngine;

namespace Why.Humans.Bands
{
    /// <summary>
    /// A moment of a stream: its calendar year and its clock arc. Both are kept because the last quarter of
    /// the clock (the final years shrinking to seconds and to "now") is closer to the present than a calendar
    /// year in double precision can express, so the tail is sampled in arc instead of in time.
    /// </summary>
    public struct StreamSample
    {
        public double Year;
        public float U;

        public StreamSample(double year, float u)
        {
            Year = year;
            U = u;
        }
    }

    /// <summary>
    /// Chooses the moments at which the streams of the human layer are sampled. Three limits apply at once:
    /// <list type="bullet">
    /// <item>content: every data slice (knot) exactly, at most <see cref="MaxYearStep"/> years apart inside
    /// the Histomap era, and a fine step while a stream emerges from or dissolves into its neighbors;</item>
    /// <item>unrolled time: at most <see cref="LnStep"/> in ln(yearsAgo + <see cref="LnOffset"/>), so
    /// curves stay smooth in the most stretched preset windows (log offsets down to 400 years);</item>
    /// <item>the clock: at most <see cref="ArcStep"/> in u, so polylines bend smoothly around the ring.</item>
    /// </list>
    /// Thread safe (pure math).
    /// </summary>
    public static class StreamSampler
    {
        /// <summary>Largest arc between samples (matches the life layer).</summary>
        public const float ArcStep = 0.0025f;

        /// <summary>Largest step in years inside the Histomap era.</summary>
        public const double MaxYearStep = 5;

        /// <summary>Step in years while a stream emerges or dissolves (and the smallest regular step).</summary>
        public const double FineYearStep = 1.5;

        /// <summary>Largest step in ln(yearsAgo + LnOffset).</summary>
        public const double LnStep = 0.005;

        public const double LnOffset = 300;

        /// <summary>
        /// Fills <paramref name="output"/> with samples from <paramref name="start"/> to
        /// <paramref name="gridEnd"/> (calendar years) and, when <paramref name="toPresent"/> is set, on along
        /// the clock to the present moment (<see cref="DeepTime.NowArc"/>).
        /// </summary>
        /// <param name="nowYear">the present (fractional calendar year)</param>
        /// <param name="start">first calendar year</param>
        /// <param name="gridEnd">last calendar year sampled in time</param>
        /// <param name="toPresent">continue from <paramref name="gridEnd"/> to the present moment</param>
        /// <param name="knots">ascending years that must be sampled exactly (data slices), may be null</param>
        /// <param name="fineBefore">years before this are sampled with <see cref="FineYearStep"/> (emergence)</param>
        /// <param name="fineAfter">years after this are sampled with <see cref="FineYearStep"/> (dissolution)</param>
        /// <param name="maxYearStep">cap on the regular step (PositiveInfinity for deep time)</param>
        /// <param name="output">cleared, then filled in chronological order</param>
        public static void Build(double nowYear, double start, double gridEnd, bool toPresent,
            IReadOnlyList<double> knots, double fineBefore, double fineAfter, double maxYearStep,
            List<StreamSample> output)
        {
            output.Clear();
            int k = 0;
            double year = start;
            while (true)
            {
                Append(output, nowYear, year);
                if (year >= gridEnd) break;

                bool fine = year < fineBefore || year >= fineAfter;
                double ya = Math.Max(nowYear - year, 0);
                double step = fine
                    ? FineYearStep
                    : Math.Min(maxYearStep, Math.Max(FineYearStep, LnStep * (ya + LnOffset)));
                double next = Math.Min(year + step, gridEnd);

                // never step over a knot or over the boundary between fine and regular sampling
                while (knots != null && k < knots.Count && knots[k] <= year + 1e-6) k++;
                if (knots != null && k < knots.Count && knots[k] < next) next = knots[k];
                if (year < fineBefore && fineBefore < next) next = fineBefore;
                if (year < fineAfter && fineAfter < next) next = fineAfter;
                year = next;
            }

            if (!toPresent || output.Count == 0) return;

            // the tail: constant content, sampled along the clock down to the present moment
            StreamSample last = output[output.Count - 1];
            int n = Mathf.CeilToInt((last.U - DeepTime.NowArc) / ArcStep);
            for (int i = 1; i <= n; i++)
            {
                float u = Mathf.Lerp(last.U, DeepTime.NowArc, i / (float)n);
                double y = nowYear - DeepTime.YearsAgo(u);
                output.Add(new StreamSample(Math.Min(nowYear, Math.Max(last.Year, y)), u));
            }
        }

        /// <summary>Appends a moment, first inserting arc-uniform samples if the clock gap is too large.</summary>
        static void Append(List<StreamSample> output, double nowYear, double year)
        {
            float u = DeepTime.Arc(nowYear - year);
            if (output.Count > 0)
            {
                StreamSample prev = output[output.Count - 1];
                int n = Mathf.CeilToInt(Mathf.Abs(prev.U - u) / ArcStep);
                double lo = Math.Min(prev.Year, year), hi = Math.Max(prev.Year, year);
                for (int i = 1; i < n; i++)
                {
                    float ui = Mathf.Lerp(prev.U, u, i / (float)n);
                    double yi = nowYear - DeepTime.YearsAgo(ui);
                    output.Add(new StreamSample(Math.Min(hi, Math.Max(lo, yi)), ui));
                }
            }

            output.Add(new StreamSample(year, u));
        }

        /// <summary>
        /// The last quarter of the clock is the last few years shrinking to "now": the streams are constant
        /// there, so their fills and edges calm down toward the present (our path does not).
        /// </summary>
        public static float PresentFade(float u)
        {
            const float Start = 0.245f; // about a year ago
            const float End = 0.17f;    // well under a second ago
            const float Floor = 0.35f;
            float f = Mathf.Clamp01((u - End) / (Start - End));
            return Mathf.Lerp(Floor, 1f, f * f * (3f - 2f * f));
        }
    }
}
