using System;
using NUnit.Framework;
using UnityEngine;
using Why.Matter;

namespace Why.Tests
{
    /// <summary>
    /// The physical map of the red layer must be one consistent field, MatterLayout's comb: the trunk is a comoving
    /// kilometre and the horizon the observable universe, both scaled back by the cosmic scale factor; every lineage body's
    /// band ends where its edge lies on the comb (at its real radius once formed, near the present); the inverse map returns
    /// to the same rho and the reported scale is the true derivative. The grid's waves lie on the same field: they never
    /// cross, meet the bands at the bodies' edges and sit where the probe reads them; and the readout names the waves the
    /// shader's rule has fully opened (<see cref="SpaceLod"/>).
    /// </summary>
    public sealed class MatterScaleTests
    {
        static readonly float[] Arcs = { 0.99f, 0.95f, 0.9f, 0.8f, 0.7f, 0.6f, 0.5f, 0.4f, 0.35f, 0.31f };

        static (MatterLayout layout, MatterScale scale) Load()
        {
            TextAsset text = Resources.Load<TextAsset>(MatterLayer.DataPath);
            Assert.IsNotNull(text, "Data/matter.json is missing");
            MatterFile file = MatterFile.Parse(text.text, out string error);
            Assert.IsNotNull(file, error);
            MatterLayout layout = MatterLayout.Build(file);
            Assert.IsFalse(layout.IsEmpty);
            return (layout, new MatterScale(layout));
        }

        [Test]
        public void TrunkAndHorizonAreScaledBack()
        {
            (MatterLayout layout, MatterScale scale) = Load();
            foreach (float u in Arcs)
            {
                double logA = Math.Log10(layout.ScaleFactor(u));
                Assert.That(scale.Log10Radius(u, 0f), Is.EqualTo(MatterLayout.TrunkLog10 + logA).Within(1e-6), $"trunk at u={u}");
                Assert.That(scale.HomeLog10(u), Is.EqualTo(MatterLayout.TrunkLog10 + logA).Within(1e-9));
                double universe = Math.Log10(MatterScale.ObservableRadiusM) + logA;
                Assert.That(scale.Log10Radius(u, scale.EnvelopeAt(u)), Is.EqualTo(universe).Within(1e-4), $"horizon at u={u}");
                Assert.That(scale.Log10UniverseRadius(u), Is.EqualTo(universe).Within(1e-6));
            }
        }

        [Test]
        public void LineageBodiesEndWhereTheirEdgesLie()
        {
            (MatterLayout layout, MatterScale scale) = Load();
            float[] inner = new float[layout.Bands.Count], outer = new float[layout.Bands.Count];
            foreach (float u in Arcs)
            {
                layout.Evaluate(u, inner, outer, out _, out float envelope);
                foreach (MatterBand band in layout.Bands)
                {
                    if (band.IsRootHome || !band.InPath || band.Item.SizeM <= 0 || !band.AliveAt(u)) continue;
                    double edge = layout.EdgeLog10(band, u);
                    float rho = layout.Rho(u, edge);
                    // held between the band inside it and the horizon
                    Assert.That(outer[band.Index], Is.EqualTo(Mathf.Clamp(rho, inner[band.Index], envelope)).Within(1e-4f * Mathf.Max(1f, envelope)),
                        $"{band.Item.Id} at u={u}: outer edge off the comb");
                    if (rho <= inner[band.Index] || rho >= envelope) continue;
                    Assert.That(scale.Log10Radius(u, outer[band.Index]), Is.EqualTo(edge).Within(1e-3),
                        $"{band.Item.Id} at u={u}: outer edge should read 10^{edge:0.00} m");
                }
            }

            // near the present every body of our lineage has formed and the scale factor is 1: each ends at its real radius
            const float present = 0.31f;
            layout.Evaluate(present, inner, outer, out _, out _);
            foreach (MatterBand band in layout.Bands)
            {
                if (band.IsRootHome || !band.InPath || band.Item.SizeM <= 0 || !band.AliveAt(present)) continue;
                if (outer[band.Index] - inner[band.Index] <= 1e-5f) continue;
                double real = Math.Log10(0.5 * band.Item.SizeM);
                Assert.That(scale.Log10Radius(present, outer[band.Index]), Is.EqualTo(real).Within(0.01),
                    $"{band.Item.Id} near the present should end at its real radius, 10^{real:0.00} m");
            }
        }

        [Test]
        public void MapIsMonotoneAndInvertible()
        {
            (_, MatterScale scale) = Load();
            foreach (float u in Arcs)
            {
                float envelope = scale.EnvelopeAt(u);
                double previous = double.NegativeInfinity;
                for (int i = 0; i <= 200; i++)
                {
                    float rho = envelope * i / 200f;
                    double log10 = scale.Log10Radius(u, rho);
                    Assert.That(log10, Is.GreaterThanOrEqualTo(previous), $"not monotone at u={u}, rho={rho}");
                    previous = log10;
                    // on the horizon itself float rounding may put the way back a hair beyond it
                    if (i == 200) continue;
                    float back = scale.Rho(u, log10);
                    Assert.That(back, Is.EqualTo(rho).Within(1e-3f * Mathf.Max(envelope, 1f)), $"inverse at u={u}, rho={rho}");
                }

                Assert.That(scale.Rho(u, scale.Log10UniverseRadius(u) + 0.5), Is.EqualTo(-1f), "nothing beyond the observable universe");
            }
        }

        [Test]
        public void ReportedScaleIsTheDerivative()
        {
            (_, MatterScale scale) = Load();
            foreach (float u in Arcs)
            {
                float envelope = scale.EnvelopeAt(u);
                for (int i = 1; i < 40; i++)
                {
                    float rho = envelope * i / 40f;
                    float h = 1e-3f * envelope;
                    // the comb is linear in log10 across the whole fan, so the central difference of the log is exact and
                    // dr/drho = r ln(10) dlog10(r)/drho
                    double slope = (scale.Log10Radius(u, rho + h) - scale.Log10Radius(u, rho - h)) / (2 * h);
                    double numeric = scale.Radius(u, rho) * Math.Log(10) * slope;
                    double reported = scale.MetresPerUnit(u, rho);
                    Assert.That(reported, Is.EqualTo(numeric).Within(0.1).Percent, $"scale at u={u}, rho={rho}");
                    Assert.That(scale.Log10MetresPerUnit(u, rho), Is.EqualTo(Math.Log10(reported)).Within(1e-9), $"log scale at u={u}, rho={rho}");
                }
            }
        }

        [Test]
        public void WavesNeverCross()
        {
            (MatterLayout layout, _) = Load();
            foreach (float u in Arcs)
            {
                float previous = 0;
                for (double v = ExpansionGridLayer.MinDecade; v <= ExpansionGridLayer.MaxDecade + 0.6; v += 0.01)
                {
                    float rho = ExpansionGridLayer.CrestRho(layout, u, v);
                    Assert.That(rho, Is.GreaterThanOrEqualTo(previous - 1e-5f), $"wave 10^{v:0.00} m folds inside the one before it at u={u}");
                    previous = rho;
                }
            }
        }

        [Test]
        public void WavesMeetTheBandsAtTheBodiesEdges()
        {
            (MatterLayout layout, MatterScale scale) = Load();
            foreach (float u in Arcs)
            {
                float envelope = scale.EnvelopeAt(u);
                foreach (MatterBand band in layout.Bands)
                {
                    if (band.IsRootHome || !band.InPath || !band.Item.Bound || band.Item.SizeM <= 0) continue;
                    // the crest of the matter at a body's radius today is the body's edge, formed or not
                    double radius = Math.Log10(0.5 * band.Item.SizeM);
                    float edge = Mathf.Clamp(layout.Rho(u, layout.EdgeLog10(band, u)), 0, envelope);
                    Assert.That(ExpansionGridLayer.CrestRho(layout, u, radius), Is.EqualTo(edge).Within(1e-4f * Mathf.Max(1f, envelope)),
                        $"the wave at {band.Item.Id}'s radius should lie on its edge at u={u}");
                }
            }
        }

        [Test]
        public void WavesSitWhereTheProbeReadsThem()
        {
            (MatterLayout layout, _) = Load();
            foreach (float u in Arcs)
            {
                float step = 1e-4f * Mathf.Max(layout.Envelope(u), 1e-3f);
                for (double v = ExpansionGridLayer.MinDecade; v <= ExpansionGridLayer.MaxDecade + 0.6; v += 0.05)
                {
                    float rho = ExpansionGridLayer.CrestRho(layout, u, v);
                    bool free = ExpansionGridLayer.CrestRho(layout, u, v - 0.01) < rho - 1e-6f && ExpansionGridLayer.CrestRho(layout, u, v + 0.01) > rho + 1e-6f;
                    if (free)
                    {
                        // the waves on either side lie apart from it: it reads as itself
                        double back = ExpansionGridLayer.WaveAt(layout, u, rho);
                        Assert.That(back, Is.EqualTo(v).Within(1e-3), $"wave 10^{v:0.00} m at u={u} reads as 10^{back:0.000} m");
                        continue;
                    }

                    // a wave inside the trunk (its matter collapsed into our lineage's bodies) lies on it, below the trunk's own wave
                    if (rho <= 0)
                    {
                        double trunk = ExpansionGridLayer.WaveAt(layout, u, 0);
                        Assert.That(trunk, Is.GreaterThanOrEqualTo(v - 1e-3), $"wave 10^{v:0.00} m at u={u} lies on the trunk, which reads 10^{trunk:0.000} m");
                        continue;
                    }

                    // a wave whose matter fell into a body lies on the body's edge, between the waves read just inside and outside it
                    double inside = ExpansionGridLayer.WaveAt(layout, u, Mathf.Max(rho - step, 0)), outside = ExpansionGridLayer.WaveAt(layout, u, rho + step);
                    Assert.That(v, Is.InRange(inside - 1e-3, outside + 1e-3), $"wave 10^{v:0.00} m at u={u} should lie on an edge between 10^{inside:0.000} and 10^{outside:0.000} m");
                }
            }
        }

        [Test]
        public void ReadoutNamesTheFinestWavesOpen()
        {
            // the readout's rule against a plain count of the lines drawn around the point, level by level: the finest level
            // whose lines bounding the point are all fully open (a bound a coarser level draws is open already)
            foreach (double v in new[] { 3.0, 3.02, 5.3, 7.99, 12.5, 18.97, 21.0004, 26.3 })
            {
                for (double log10G = -6; log10G <= 0; log10G += 0.05)
                {
                    double g = Math.Pow(10, log10G);
                    int expected = -1;
                    for (int level = 0; level < SpaceLod.Levels; level++)
                    {
                        if (!BoundsOpen(v, level, g)) break;
                        expected = level;
                    }

                    int got = SpaceLod.FinestOpenLevel(v, g, out double log10Spacing);
                    Assert.That(got, Is.EqualTo(expected), $"wave 10^{v} m at {g:G3} decades a pixel");
                    if (got < 0)
                    {
                        Assert.That(double.IsNaN(log10Spacing));
                        continue;
                    }

                    double k = Math.Floor(v);
                    double spacing = got == 0 ? 9 * Math.Pow(10, k) : SpaceLod.Step(got) * Math.Pow(10, k);
                    Assert.That(log10Spacing, Is.EqualTo(Math.Log10(spacing)).Within(1e-9));
                }
            }
        }

        /// <summary>Are the lines of a level that bound the point fully open at g decades a pixel, as the shader draws them?</summary>
        static bool BoundsOpen(double v, int level, double g)
        {
            if (level == 0) return SpaceLod.Emerge(1 / g) >= 1f;
            double k = Math.Floor(v), step = SpaceLod.Step(level);
            // every line of the level and coarser inside the wave, from the inner crest (x = 1) to the outer (x = 10)
            int count = (int)Math.Round(9 / step);
            double below = 1, above = 10;
            int belowJ = (int)Math.Round(1 / step), aboveJ = (int)Math.Round(10 / step);
            for (int i = 0; i <= count; i++)
            {
                int j = (int)Math.Round(1 / step) + i;
                double x = j * step, at = k + Math.Log10(x);
                if (at <= v + 1e-12 && x > below - 1e-12) (below, belowJ) = (x, j);
                if (at > v + 1e-12 && x < above + 1e-12) (above, aboveJ) = (x, j);
            }

            // a bound of this level (not a multiple of ten on its lattice, nor the crest) must have opened by its own gap
            bool open = true;
            foreach ((double x, int j) in new[] { (below, belowJ), (above, aboveJ) })
            {
                if (j % 10 == 0 || (level == 1 && j == 1)) continue;
                open &= SpaceLod.Emerge(SpaceLod.Gap(level, x) / g) >= 1f;
            }

            return open;
        }

        [Test]
        public void ALevelHandsOverToItsFloor()
        {
            for (int level = 0; level < SpaceLod.Levels; level++)
            {
                const double x = 1.5;
                // so far out that the next level has opened too
                double g = SpaceLod.NextGap(level, x) / (10 * SpaceLod.OpenPx);
                if (level + 1 == SpaceLod.Levels) g = SpaceLod.Gap(level, x) / (10 * SpaceLod.OpenPx);
                float expected = SpaceLod.Weight[level] * (level + 1 < SpaceLod.Levels ? SpaceLod.Floor[level] : 1f);
                Assert.That(SpaceLod.Alpha(level, x, g), Is.EqualTo(expected).Within(1e-5f), $"level {level}");
                // crowded under the emerge spacing, a line is not drawn at all
                Assert.That(SpaceLod.Alpha(level, x, SpaceLod.Gap(level, x) / (0.9 * SpaceLod.EmergePx)), Is.EqualTo(0f));
            }
        }

        [Test]
        public void TimeScaleFollowsTheClock()
        {
            foreach (float u in Arcs)
            {
                double du = 1e-4;
                double expected = Math.Abs(DeepTime.YearsAgo(u + du) - DeepTime.YearsAgo(u - du)) / (2 * du * GraphWarp.BasePath.SigmaPerArc);
                Assert.That(MatterScale.YearsPerUnit(u), Is.EqualTo(expected).Within(1).Percent);
            }
        }
    }
}
