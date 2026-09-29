using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Why.Matter;

namespace Why.Tests
{
    /// <summary>
    /// The physical map of the red layer must be exact where it is pinned and consistent everywhere else:
    /// every lineage body's band ends at its real radius, the envelope is the observable universe scaled by
    /// the cosmic scale factor, the inverse map returns to the same rho, the reported scale is the true
    /// derivative, and the probe's numbers agree with the grid's lines.
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
        public void LineageBodiesEndAtTheirRealRadius()
        {
            (MatterLayout layout, MatterScale scale) = Load();
            float[] inner = new float[layout.Bands.Count], outer = new float[layout.Bands.Count];
            foreach (float u in Arcs)
            {
                layout.Evaluate(u, inner, outer, out _, out float envelope);
                float a = layout.ScaleFactor(u);
                foreach (MatterBand band in layout.Bands)
                {
                    if (band.IsRootHome || !band.InPath || band.Item.SizeM <= 0 || !band.AliveAt(u)) continue;
                    if (outer[band.Index] - inner[band.Index] <= 1e-5f) continue;
                    double expected = Math.Log10(0.5 * band.Item.SizeM * (band.Item.Bound ? 1 : a));
                    double got = scale.Log10Radius(u, outer[band.Index]);
                    // forming bodies narrower than their size are nudged to keep the map monotone; otherwise exact
                    Assert.That(got, Is.EqualTo(expected).Within(0.01).Or.GreaterThan(expected),
                        $"{band.Item.Id} at u={u}: outer edge should read 10^{expected:0.00} m, reads 10^{got:0.00}");
                }

                double universe = Math.Log10(MatterScale.ObservableRadiusM * a);
                Assert.That(scale.Log10Radius(u, envelope), Is.EqualTo(universe).Within(1e-4), $"envelope at u={u}");
                Assert.That(scale.Log10UniverseRadius(u), Is.EqualTo(universe).Within(1e-9));
            }
        }

        [Test]
        public void InnerTrackIsHumanScale()
        {
            (_, MatterScale scale) = Load();
            foreach (float u in Arcs) Assert.That(scale.Radius(u, 0f), Is.EqualTo(MatterScale.InnerMetres).Within(1e-6));
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
                    float back = scale.Rho(u, log10);
                    Assert.That(back, Is.EqualTo(rho).Within(1e-3f * Mathf.Max(envelope, 1f)), $"inverse at u={u}, rho={rho}");
                }
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
                    // the map is linear in log10 between knots, so the central difference of the log is exact
                    // there and dr/drho = r ln(10) dlog10(r)/drho; a knot inside the step is skipped
                    bool nearKnot = false;
                    foreach (MatterScale.Knot k in scale.Knots(u)) nearKnot |= Mathf.Abs(k.Rho - rho) <= h;
                    if (nearKnot) continue;
                    double slope = (scale.Log10Radius(u, rho + h) - scale.Log10Radius(u, rho - h)) / (2 * h);
                    double numeric = scale.Radius(u, rho) * Math.Log(10) * slope;
                    double reported = scale.MetresPerUnit(u, rho);
                    Assert.That(reported, Is.EqualTo(numeric).Within(0.1).Percent, $"scale at u={u}, rho={rho}");
                }
            }
        }

        [Test]
        public void GridShellsSitWhereTheProbeReadsThem()
        {
            (_, MatterScale scale) = Load();
            foreach (float u in Arcs)
            {
                double universe = scale.Log10UniverseRadius(u);
                for (int n = 0; n < universe; n++)
                {
                    float rho = scale.Rho(u, n);
                    Assert.That(rho, Is.GreaterThanOrEqualTo(0f));
                    Assert.That(scale.Log10Radius(u, rho), Is.EqualTo(n).Within(1e-3), $"shell 10^{n} m at u={u}");
                }

                Assert.That(scale.Rho(u, universe + 0.5), Is.EqualTo(-1f), "no shell beyond the observable universe");
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
