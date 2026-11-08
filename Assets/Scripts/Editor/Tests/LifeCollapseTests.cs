using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using Why.Life;

namespace Why.Tests
{
    /// <summary>
    /// The tree of life's collapse at the mass extinctions (<see cref="MassExtinctions"/>): B(u) is exactly 1 outside every
    /// window, 1 - loss right after a lone extinction, rebounds along an S-curve to 1 by the recovery and multiplies where
    /// windows overlap; the sampling lands on both sides of every cliff and on every window's end; the dips' highlight ids
    /// nest; and the data gives the Big Five the checked, gate-verified losses and recoveries.
    /// </summary>
    public sealed class LifeCollapseTests
    {
        // a lone extinction, and one whose window holds a second (as the end-Permian's holds the end-Triassic)
        static readonly MassExtinctions.Extinction Lone = new MassExtinctions.Extinction("lone", 300, 0.6, 20);
        static readonly MassExtinctions.Extinction Outer = new MassExtinctions.Extinction("outer", 100, 0.5, 40);
        static readonly MassExtinctions.Extinction Inner = new MassExtinctions.Extinction("inner", 80, 0.3, 5);

        /// <summary>
        /// The checked numbers (Docs/research/life/extinctions-big-five.json, each fact verified in its gate record and passed
        /// by the skeptical second reader): the share of species lost, the recovery in millions of years, and the facts they
        /// come from.
        /// </summary>
        static readonly (string id, float loss, double recoveryMyr, string lossFact, string recoveryFact)[] Checked =
        {
            ("end_ordovician_extinction", 0.85f, 5, "ext-ord-species-lome", "ext-ord-recovery-lome"),
            ("late_devonian_extinction", 0.40f, 13, "ext-dev-species-lde", "ext-dev-reef-recovery-hangenberg"),
            ("end_permian_extinction", 0.81f, 75, "ext-perm-species-pt", "ext-perm-recovery-global-diversity"),
            ("end_triassic_extinction", 0.70f, 2, "ext-tri-species-overview", "ext-tri-recovery-thibodeau2016"),
            ("end_cretaceous_extinction", 0.76f, 10, "ext-kpg-species-lowery2018", "ext-kpg-recovery-squamates"),
        };

        static float ArcMa(double mya) => DeepTime.Arc(mya * 1e6);

        static MassExtinctions Synthetic()
        {
            List<string> problems = new List<string>();
            MassExtinctions x = new MassExtinctions(new[] { Inner, Lone, Outer }, problems);
            Assert.IsEmpty(problems, string.Join("\n", problems));
            Assert.AreEqual(3, x.Count);
            return x;
        }

        [Test]
        public void OneOutsideEveryWindow()
        {
            MassExtinctions x = Synthetic();
            float uOld = ArcMa(500), uNew = ArcMa(1);
            for (int i = 0; i <= 5000; i++)
            {
                float u = Mathf.Lerp(uOld, uNew, i / 5000f);
                if (x.InWindow(u)) continue;
                Assert.AreEqual(1f, x.B(u), $"B at arc {u} ({DeepTime.YearsAgo(u) / 1e6:0.0} Ma)");
            }

            foreach (double mya in new[] { 400.0, 279.0, 200.0, 59.0, 10.0 }) Assert.AreEqual(1f, x.B(ArcMa(mya)), $"{mya} Ma");
            Assert.AreEqual(1f, x.B(Lone.UTop), "just before the cliff");
            Assert.AreEqual(1f, MassExtinctions.None.B(ArcMa(250)), "no extinctions");
        }

        [Test]
        public void DropsToOneMinusLossAtALoneExtinction()
        {
            MassExtinctions x = Synthetic();
            Assert.AreEqual(1f, x.B(Lone.UTop), "just before");
            Assert.AreEqual(1f - Lone.Loss, x.B(Lone.UEvent), 1e-6f, "at the extinction");
            Assert.AreEqual(1f - Lone.Loss, x.B(Lone.UEvent - 1e-6f), 1e-3f, "right after");
        }

        [Test]
        public void ReboundsAlongAnSCurve()
        {
            MassExtinctions x = Synthetic();
            float previous = x.B(Lone.UEvent);
            for (int i = 1; i <= 200; i++)
            {
                float b = x.B(Mathf.Lerp(Lone.UEvent, Lone.URecovered, i / 200f));
                Assert.That(b, Is.GreaterThanOrEqualTo(previous), "the rebound never falls back");
                previous = b;
            }

            Assert.AreEqual(1f, x.B(Lone.URecovered), "recovered");

            // an S-curve in arc space: half the loss back at the middle of the window, 0.156 of it at a quarter
            Assert.AreEqual(1f - 0.5f * Lone.Loss, x.B(Mathf.Lerp(Lone.UEvent, Lone.URecovered, 0.5f)), 1e-4f);
            Assert.AreEqual(1f - Lone.Loss + 0.15625f * Lone.Loss, x.B(Mathf.Lerp(Lone.UEvent, Lone.URecovered, 0.25f)), 1e-4f);
        }

        [Test]
        public void OverlappingWindowsMultiply()
        {
            MassExtinctions x = Synthetic();
            float u = ArcMa(78); // inside the outer window (100 to 60 Ma) and the inner one (80 to 75 Ma)
            Assert.IsTrue(Outer.Holds(u) && Inner.Holds(u));
            Assert.AreEqual(Outer.Factor(u) * Inner.Factor(u), x.B(u), 1e-6f);
            Assert.That(x.B(u), Is.LessThan(Mathf.Min(Outer.Factor(u), Inner.Factor(u))));

            // the inner cliff takes its own share off whatever the outer rebound has reached
            Assert.AreEqual(1f - Inner.Loss, x.B(Inner.UEvent) / x.B(Inner.UTop), 1e-3f);
        }

        [Test]
        public void SamplingLandsOnBothSidesOfEveryCliff()
        {
            MassExtinctions x = Synthetic();
            float uStart = ArcMa(500), uEnd = ArcMa(1);
            const float step = 0.0025f;
            List<float> samples = new List<float> { uStart };
            for (float u = uStart; u > uEnd;)
            {
                float next = x.Next(u, step, uEnd);
                Assert.That(next, Is.LessThan(u), "the walk moves toward the present");
                Assert.That(u - next, Is.LessThanOrEqualTo(step + 2e-7f), "never a longer step than asked");
                samples.Add(next);
                u = next;
            }

            Assert.AreEqual(uEnd, samples[samples.Count - 1]);
            for (int k = 0; k < x.Count; k++)
            {
                MassExtinctions.Extinction e = x[k];
                CollectionAssert.Contains(samples, e.UTop, $"{e.Id}: top of the cliff");
                CollectionAssert.Contains(samples, e.UEvent, $"{e.Id}: foot of the cliff");
                CollectionAssert.Contains(samples, e.UAfter, $"{e.Id}: just past the cliff");
                CollectionAssert.Contains(samples, e.URecovered, $"{e.Id}: end of the rebound");
                for (int i = 1; i < samples.Count; i++)
                {
                    if (!e.Holds(samples[i - 1])) continue;
                    Assert.That(samples[i - 1] - samples[i], Is.LessThanOrEqualTo(e.Span / MassExtinctions.WindowSteps + 2e-7f),
                        $"{e.Id}: step inside the window");
                }
            }
        }

        [Test]
        public void OnlyTheCliffIsTheCliff()
        {
            MassExtinctions x = Synthetic();
            for (int k = 0; k < x.Count; k++)
            {
                MassExtinctions.Extinction e = x[k];
                Assert.IsTrue(x.OnCliff(e.UTop) && x.OnCliff(e.UEvent), $"{e.Id}: both sides of the drop");
                Assert.IsFalse(x.OnCliff(e.UTop + MassExtinctions.Cliff) || x.OnCliff(e.UAfter) || x.OnCliff(e.URecovered),
                    $"{e.Id}: the lines around the drop stay lit");
            }

            Assert.IsFalse(x.OnCliff(ArcMa(250)));
        }

        [Test]
        public void DipIdsNest()
        {
            MassExtinctions x = Synthetic();
            int lone = x.IndexOf("lone"), outer = x.IndexOf("outer"), inner = x.IndexOf("inner");
            Assert.That(new[] { lone, outer, inner }, Is.EqualTo(new[] { 0, 1, 2 }), "oldest first");
            Assert.AreEqual(-1, x.DipAt(ArcMa(350)));
            Assert.AreEqual(lone, x.DipAt(Lone.UTop), "the top of the cliff already belongs to the dip");
            Assert.AreEqual(lone, x.DipAt(Lone.URecovered));
            Assert.AreEqual(-1, x.DipAt(Lone.URecovered - MassExtinctions.Cliff));
            Assert.AreEqual(outer, x.DipAt(ArcMa(90)));
            Assert.AreEqual(inner, x.DipAt(ArcMa(78)), "where windows overlap the latest dip carries the id");
            Assert.AreEqual(outer, x.DipAt(ArcMa(70)));
            Assert.AreEqual(lone, x.LastNested(lone));
            Assert.AreEqual(inner, x.LastNested(outer), "the outer extinction's ids reach the dip nested in it");
            Assert.AreEqual(inner, x.LastNested(inner));
        }

        [Test]
        public void RejectsUnusableData()
        {
            List<string> problems = new List<string>();
            MassExtinctions x = new MassExtinctions(new[]
            {
                new MassExtinctions.Extinction("nothing lost", 100, 0, 5),
                new MassExtinctions.Extinction("everything lost", 100, 1, 5),
                new MassExtinctions.Extinction("recovers before the start", 100, 0.5, 150),
                new MassExtinctions.Extinction("recovers within the cliff", 100, 0.5, 1e-6),
                Lone
            }, problems);
            Assert.AreEqual(1, x.Count);
            Assert.AreEqual(4, problems.Count, string.Join("\n", problems));
        }

        [Test]
        public void SelfChecksPass()
        {
            List<string> problems = new List<string>();
            Synthetic().Verify(ArcMa(4200), ArcMa(0.005), problems);
            FromData(out _).Verify(ArcMa(4200), ArcMa(0.005), problems);
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [Test]
        public void DataGivesTheCheckedLossesAndRecoveries()
        {
            MassExtinctions x = FromData(out List<JObject> events);
            Assert.AreEqual(Checked.Length, x.Count, "the Big Five, and only they, carry a loss");
            foreach ((string id, float loss, double recoveryMyr, string lossFact, string recoveryFact) in Checked)
            {
                int k = x.IndexOf(id);
                Assert.That(k, Is.GreaterThanOrEqualTo(0), id);
                Assert.AreEqual(loss, x[k].Loss, 1e-6f, id);
                Assert.AreEqual(recoveryMyr, x[k].RecoveryMyr, 1e-9, id);
                JObject e = events.Find(o => (string)o["id"] == id);
                Assert.IsFalse(string.IsNullOrWhiteSpace((string)e["lossMeasure"]), id + ": lossMeasure");
                Assert.IsFalse(string.IsNullOrWhiteSpace((string)e["recoveryMeasure"]), id + ": recoveryMeasure");
                CollectionAssert.AreEquivalent(new[] { lossFact, recoveryFact }, e["facts"]?.ToObject<string[]>(), id + ": facts");
            }

            // the end-Permian's 75-million-year rebound holds the end-Triassic: their factors multiply there
            int permian = x.IndexOf("end_permian_extinction"), triassic = x.IndexOf("end_triassic_extinction");
            Assert.IsTrue(x[permian].Holds(x[triassic].UEvent));
            Assert.AreEqual(triassic, x.LastNested(permian));
        }

        [Test]
        public void DataFactsAreGateVerified()
        {
            string root = Path.GetDirectoryName(Application.dataPath) ?? ".";
            JObject research = JObject.Parse(File.ReadAllText(Path.Combine(root, "Docs/research/life/extinctions-big-five.json")));
            JObject gate = JObject.Parse(File.ReadAllText(Path.Combine(root, "Docs/research/life/gate/extinctions-big-five.json")));
            Dictionary<string, JToken> facts = new Dictionary<string, JToken>();
            foreach (JToken f in (JArray)research["facts"]) facts[(string)f["id"]] = f;

            foreach ((string id, float loss, double recoveryMyr, string lossFact, string recoveryFact) in Checked)
            {
                foreach (string fact in new[] { lossFact, recoveryFact })
                {
                    Assert.IsTrue(facts.ContainsKey(fact), $"{id}: fact '{fact}' is not in the research file");
                    Assert.AreEqual("verified", (string)gate["facts"]?[fact]?["status"], $"{id}: fact '{fact}' is not gate-verified");
                    // the research README: a fact the skeptical second reader rejected is kept for the record, never used
                    Assert.AreEqual(true, (bool?)facts[fact]["check"]?["ok"], $"{id}: fact '{fact}' failed its skeptical check");
                }

                Assert.AreEqual(loss * 100, (double)facts[lossFact]["value"], 1e-4, $"{id}: the loss is the fact's percentage");
                Assert.AreEqual(recoveryMyr, (double)facts[recoveryFact]["value"], 1e-9, $"{id}: the recovery is the fact's");
            }
        }

        /// <summary>The events of Data/life_clades.json that carry a loss.</summary>
        static List<JObject> DataEvents()
        {
            TextAsset text = Resources.Load<TextAsset>(LifeLayer.CladesPath);
            Assert.IsNotNull(text, "Data/life_clades.json is missing");
            List<JObject> list = new List<JObject>();
            foreach (JToken e in (JArray)JObject.Parse(text.text)["events"])
            {
                if (e["loss"] != null) list.Add((JObject)e);
            }

            return list;
        }

        static MassExtinctions FromData(out List<JObject> events)
        {
            events = DataEvents();
            List<MassExtinctions.Extinction> list = new List<MassExtinctions.Extinction>();
            foreach (JObject e in events)
            {
                list.Add(new MassExtinctions.Extinction((string)e["id"], (double)e["mya"], (double)e["loss"],
                    (double?)e["recoveryMyr"] ?? 0));
            }

            List<string> problems = new List<string>();
            MassExtinctions x = new MassExtinctions(list, problems);
            Assert.IsEmpty(problems, string.Join("\n", problems));
            return x;
        }
    }
}
