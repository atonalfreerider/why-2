using System.IO;
using NUnit.Framework;
using Why.Economy.Model;
using Why.Humans.Social;

namespace Why.EditorTests
{
    public sealed class ActorPredictorTests
    {
        static ActorPredictor.Priors LoadFromDisk()
        {
            string path = Path.Combine("Assets", "Resources", "Data", "economy", "actor-priors.json");
            Assert.IsTrue(File.Exists(path), "Expected Jev actor-priors.json at " + path);
            return ActorPredictor.ParsePriors(File.ReadAllText(path));
        }

        [Test]
        public void ActorPriors_ParseAllJevJudgmentsAndScoresInRange()
        {
            ActorPredictor.Priors priors = LoadFromDisk();
            Assert.GreaterOrEqual(priors.Channels.Count, 6);
            Assert.GreaterOrEqual(priors.FameTiers.Count, 5);
            Assert.GreaterOrEqual(priors.Archetypes.Count, 6);

            foreach (var ch in priors.Channels)
            {
                Assert.That(ch.Leverage.Score, Is.InRange(0f, 4f));
                Assert.That(ch.CapitalBarrier.Score, Is.InRange(0f, 4f));
                Assert.That(ch.NetworkDependency.Score, Is.InRange(0f, 4f));
                Assert.That(ch.DownsideRisk.Score, Is.InRange(0f, 4f));
            }
            foreach (var a in priors.Archetypes)
            {
                Assert.IsFalse(string.IsNullOrEmpty(a.PrimaryChoice));
                Assert.That(a.Cooperation.Score, Is.InRange(0f, 4f));
                Assert.That(a.DesireDominance.Score, Is.InRange(0f, 4f));
            }
        }

        [Test]
        public void FameCurve_CapturesMobilityBoostVsAutonomyAndHappinessTradeoff()
        {
            ActorPredictor.Priors priors = LoadFromDisk();
            var domain = priors.FameTier("domain_specialist");
            var icon = priors.FameTier("global_icon");
            Assert.IsNotNull(domain);
            Assert.IsNotNull(icon);

            // Global icon yields stronger economic mobility boost, while domain reputation preserves higher autonomy & happiness
            Assert.Greater(icon.MobilityBoost.Score, domain.MobilityBoost.Score);
            Assert.Greater(domain.Autonomy.Score, icon.Autonomy.Score);
            Assert.Greater(domain.Happiness.Score, icon.Happiness.Score);
            Assert.Greater(icon.FearExposure.Score, domain.FearExposure.Score);
        }

        [Test]
        public void PredictPublic_ProducesValidDeterministicForecast()
        {
            ActorPredictor.Priors priors = LoadFromDisk();
            var person = new SocialPeople.Person
            {
                Index = 1,
                name = "Test Founder",
                region = "us",
                fol = 120_000_000,
                elite = 42,
                born = 1975,
                firms = new[] { "tesla" },
                cloud = -1,
            };

            ActorPredictor.Forecast f = ActorPredictor.PredictPublic(null, person, null, 2026, priors);
            Assert.IsNotNull(f);
            Assert.AreEqual("global_icon", f.FameTier.Id);
            Assert.AreEqual("capital_owner", f.Archetype.Id);
            Assert.AreEqual(6, f.RankedRivers.Count);
            Assert.That(f.UpOdds + f.StayOdds + f.DownOdds, Is.EqualTo(1f).Within(0.01f));
            Assert.That(f.PredictedHappiness, Is.InRange(5f, 99f));
            Assert.IsTrue(f.SummaryText.Contains("FEAR vs. DESIRE VECTOR"));
            Assert.IsTrue(f.SummaryText.Contains("ECONOMIC RIVER SIPHONING"));
        }
    }
}
