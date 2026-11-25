using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Why.Economy.Data;
using Why.Economy.Land;
using Why.Humans.Social;

namespace Why.Economy.Model
{
    /// <summary>
    /// Causal behavioral and economic-mobility predictor for individual actors along the economic frontier
    /// (both simulated Americans in <see cref="EconomicLives"/> and public figures in the Instagram social
    /// network cloud <see cref="SocialPeople"/>).
    /// <para>
    /// Combines deterministic simulation &amp; empirical causality data (family circumstance, stratum/quintile,
    /// fear vs. desire spending shares, financial runway, debt, industry value capture, tit-for-tat cooperation,
    /// decade mobility matrices, and social-graph reach) with structured TypeSafe Jev (System One) priors in
    /// <c>Assets/Resources/Data/economy/actor-priors.json</c> (economic river siphoning channels, fame's dual
    /// impact on mobility vs. happiness, and behavioral responses under fear vs. desire).
    /// </para>
    /// Pure C# (no Unity UI dependency; safe to test in EditMode).
    /// </summary>
    public static class ActorPredictor
    {
        public const string PriorsPath = "Data/economy/actor-priors";
        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        public enum SimFilter { All, Climbers, Bottom20, Top1, Owners, HighFear, Notables }

        public struct ScoredMetric
        {
            public float Score;      // 0..4
            public float Confidence; // 0..1
        }

        public sealed class ChannelPrior
        {
            public string Id, Name, Description;
            public ScoredMetric Leverage, CapitalBarrier, NetworkDependency, DownsideRisk;
        }

        public sealed class FameTierPrior
        {
            public string Id, Name, Followers;
            public ScoredMetric MobilityBoost, Happiness, FearExposure, Autonomy;
        }

        public sealed class ArchetypePrior
        {
            public string Id, Name, PrimaryChoice;
            public float ChoiceConfidence;
            public readonly Dictionary<string, float> Probabilities = new Dictionary<string, float>(StringComparer.Ordinal);
            public ScoredMetric Cooperation, DesireDominance;
        }

        public sealed class Priors
        {
            public string AsOf, Model, Note;
            public readonly List<ChannelPrior> Channels = new List<ChannelPrior>();
            public readonly List<FameTierPrior> FameTiers = new List<FameTierPrior>();
            public readonly List<ArchetypePrior> Archetypes = new List<ArchetypePrior>();

            public FameTierPrior FameTier(string id) => FameTiers.Find(t => t.Id == id) ?? (FameTiers.Count > 0 ? FameTiers[0] : null);
            public ArchetypePrior Archetype(string id) => Archetypes.Find(a => a.Id == id) ?? (Archetypes.Count > 0 ? Archetypes[0] : null);
        }

        public sealed class SimMatch
        {
            public int PersonId;
            public string Label, Sublabel;
            public float IncomeRank;
            public double Wealth;
            public string Role;
        }

        public sealed class PublicMatch
        {
            public int Index;
            public SocialPeople.Person Person;
            public string Label, Sublabel;
        }

        public sealed class ChannelFit
        {
            public ChannelPrior Channel;
            public float FitScore; // 0..1 tailored to this actor's resources & connections
            public string Rationale;
        }

        public sealed class Forecast
        {
            public bool IsPublic;
            public int PersonId;
            public string Name, Headline, OriginSummary;
            public float FearShare, DesireShare, Reason, Agency;
            public string DominantFear, DominantDesire;
            public ArchetypePrior Archetype;
            public string PrimaryActionLabel;
            public float PrimaryActionProb;
            public float PredictedCooperation;
            public readonly List<ChannelFit> RankedRivers = new List<ChannelFit>();
            public string HighSurplusIndustry;
            public float HighSurplusShare;
            public FameTierPrior FameTier;
            public double Followers;
            public int EliteFollowers;
            public float UpOdds, StayOdds, DownOdds;
            public float PredictedHappiness; // 0..100
            public float AutonomyIndex;      // 0..100
            public float FameFearPressure;   // 0..100
            public string SummaryText;
        }

        static Priors cachedPriors;

        public static Priors LoadPriors(string jsonOverride = null)
        {
            if (jsonOverride == null && cachedPriors != null) return cachedPriors;
            string json = jsonOverride ?? Resources.Load<TextAsset>(PriorsPath)?.text;
            if (string.IsNullOrEmpty(json)) return null;
            Priors p = ParsePriors(json);
            if (jsonOverride == null) cachedPriors = p;
            return p;
        }

        public static Priors ParsePriors(string json)
        {
            JObject root = JObject.Parse(json);
            var p = new Priors
            {
                AsOf = (string)root["asOf"] ?? "",
                Model = (string)root["model"] ?? "jev",
                Note = (string)root["note"] ?? "",
            };
            foreach (JToken c in root["channels"] as JArray ?? new JArray())
            {
                p.Channels.Add(new ChannelPrior
                {
                    Id = (string)c["id"] ?? "",
                    Name = (string)c["name"] ?? "",
                    Description = (string)c["description"] ?? "",
                    Leverage = ReadScore(c["leverage"]),
                    CapitalBarrier = ReadScore(c["capitalBarrier"]),
                    NetworkDependency = ReadScore(c["networkDependency"]),
                    DownsideRisk = ReadScore(c["downsideRisk"]),
                });
            }
            foreach (JToken t in root["fameTiers"] as JArray ?? new JArray())
            {
                p.FameTiers.Add(new FameTierPrior
                {
                    Id = (string)t["id"] ?? "",
                    Name = (string)t["name"] ?? "",
                    Followers = (string)t["followers"] ?? "",
                    MobilityBoost = ReadScore(t["mobilityBoost"]),
                    Happiness = ReadScore(t["happiness"]),
                    FearExposure = ReadScore(t["fearExposure"]),
                    Autonomy = ReadScore(t["autonomy"]),
                });
            }
            foreach (JToken a in root["archetypes"] as JArray ?? new JArray())
            {
                var arch = new ArchetypePrior
                {
                    Id = (string)a["id"] ?? "",
                    Name = (string)a["name"] ?? "",
                    PrimaryChoice = (string)a["primaryResponse"]?["choice"] ?? "",
                    ChoiceConfidence = (float)(a["primaryResponse"]?["confidence"] ?? 0f),
                    Cooperation = ReadScore(a["cooperation"]),
                    DesireDominance = ReadScore(a["desireDominance"]),
                };
                if (a["primaryResponse"]?["probabilities"] is JObject probs)
                {
                    foreach (var kv in probs) arch.Probabilities[kv.Key] = (float)kv.Value;
                }
                p.Archetypes.Add(arch);
            }
            return p;
        }

        static ScoredMetric ReadScore(JToken tok) => tok == null
            ? default
            : new ScoredMetric { Score = (float)(tok["score"] ?? 0f), Confidence = (float)(tok["confidence"] ?? 0f) };

        // ------------------------------------------------------------------ Actor search

        /// <summary>Searches simulated Americans alive as adults in <paramref name="year"/> by ID, industry, role, or filter.</summary>
        public static List<SimMatch> FindSimulated(EconomyModel model, int year, string query, SimFilter filter, int limit = 16)
        {
            var results = new List<SimMatch>(limit);
            EconomicLives lives = model?.Lives;
            if (lives == null || !lives.Ready || lives.Sim == null) return results;

            string q = (query ?? "").Trim();
            bool hasId = false;
            int exactId = -1;
            if (q.Length > 0)
            {
                string num = q.StartsWith("#") ? q.Substring(1) : q;
                hasId = int.TryParse(num, NumberStyles.Integer, Ci, out exactId);
            }
            string qLower = q.ToLowerInvariant();

            var notableRoles = new Dictionary<int, string>();
            if (lives.Notables != null)
            {
                foreach (Notable n in lives.Notables)
                    if (!notableRoles.ContainsKey(n.Person)) notableRoles[n.Person] = n.Role;
            }

            var candidates = new List<(float sortKey, SimMatch match)>();
            int count = lives.Sim.People.Count;
            for (int i = 0; i < count; i++)
            {
                if (hasId && i != exactId) continue;
                if (!lives.TryGet(i, year, out PersonYear r) || !r.Adult) continue;

                notableRoles.TryGetValue(i, out string role);
                string indName = r.Employed && r.Industry >= 0 && r.Industry < model.Data.Industries.Count
                    ? model.Data.Industries[r.Industry].Name
                    : (r.SocialSecurity > 0 ? "Retired" : "Not employed");

                if (!hasId && qLower.Length > 0)
                {
                    bool hit = indName.ToLowerInvariant().Contains(qLower)
                        || (role != null && role.ToLowerInvariant().Contains(qLower))
                        || ("#" + i.ToString(Ci)).Contains(qLower);
                    if (!hit) continue;
                }

                float climb = 0;
                if (lives.TryGet(i, year - 10, out PersonYear prev) && prev.Adult) climb = r.IncomeRank - prev.IncomeRank;

                switch (filter)
                {
                    case SimFilter.Climbers:
                        if (climb < .15f) continue;
                        break;
                    case SimFilter.Bottom20:
                        if (r.IncomeRank >= .20f) continue;
                        break;
                    case SimFilter.Top1:
                        if (r.WealthGroup < 3) continue;
                        break;
                    case SimFilter.Owners:
                        if (!r.SelfEmployed && !r.InControl) continue;
                        break;
                    case SimFilter.HighFear:
                        if (r.FearShare < .60f) continue;
                        break;
                    case SimFilter.Notables:
                        if (role == null) continue;
                        break;
                }

                float score = filter == SimFilter.Climbers ? climb
                    : filter == SimFilter.HighFear ? r.FearShare
                    : filter == SimFilter.Bottom20 ? -r.IncomeRank
                    : (role != null ? 100f : 0f) + r.IncomeRank + (float)Math.Log10(1 + Math.Max(0, r.Wealth)) * .1f;

                string tag = role != null ? $" [{role}]" : r.InControl ? " [in control]" : "";
                candidates.Add((score, new SimMatch
                {
                    PersonId = i,
                    IncomeRank = r.IncomeRank,
                    Wealth = r.Wealth,
                    Role = role,
                    Label = $"Person #{i}{tag} · age {(int)r.Age}",
                    Sublabel = $"{indName} · rank {PersonLife.Pct(r.IncomeRank)} · NW {PersonLife.Money(r.Wealth)} · fear {PersonLife.Pct(r.FearShare)}"
                }));
            }

            candidates.Sort((a, b) => b.sortKey.CompareTo(a.sortKey));
            int take = Math.Min(limit, candidates.Count);
            for (int i = 0; i < take; i++) results.Add(candidates[i].match);
            return results;
        }

        /// <summary>Searches public figures in the Instagram social network cloud by name, category cloud, or company.</summary>
        public static List<PublicMatch> FindPublic(SocialPeople social, string query, int limit = 16)
        {
            var results = new List<PublicMatch>(limit);
            if (social == null || social.people == null) return results;
            string q = (query ?? "").Trim().ToLowerInvariant();
            var matches = new List<(double fol, PublicMatch match)>();

            for (int i = 0; i < social.people.Length; i++)
            {
                SocialPeople.Person p = social.people[i];
                string cloud = p.cloud >= 0 ? social.CloudLabel(p.cloud) : "";
                string firms = p.firms != null && p.firms.Length > 0 ? string.Join(", ", p.firms) : "";
                if (q.Length > 0)
                {
                    bool hit = (p.name ?? "").ToLowerInvariant().Contains(q)
                        || cloud.ToLowerInvariant().Contains(q)
                        || firms.ToLowerInvariant().Contains(q)
                        || (p.region ?? "").ToLowerInvariant().Contains(q);
                    if (!hit) continue;
                }
                string anchor = firms.Length > 0 ? $" · {firms}" : cloud.Length > 0 ? $" · {cloud}" : "";
                matches.Add((p.fol, new PublicMatch
                {
                    Index = p.Index,
                    Person = p,
                    Label = $"{p.name}{anchor}",
                    Sublabel = $"{FormatFollowers(p.fol)} followers · {p.elite} elite follows · region {p.region}"
                }));
            }

            matches.Sort((a, b) => b.fol.CompareTo(a.fol));
            int take = Math.Min(limit, matches.Count);
            for (int i = 0; i < take; i++) results.Add(matches[i].match);
            return results;
        }

        // ------------------------------------------------------------------ Causal predictions

        /// <summary>Produces a full causal behavioral, river-siphoning, and fame/happiness prediction for a simulated American.</summary>
        public static Forecast PredictSimulated(EconomyModel model, LandSnapshot snap, int personId, int year, Priors priorsOverride = null)
        {
            Priors priors = priorsOverride ?? LoadPriors();
            EconomicLives lives = model?.Lives;
            if (priors == null || lives == null || !lives.Ready || !lives.TryGet(personId, year, out PersonYear r)) return null;

            PersonTraits traits = lives.Traits(personId);
            float parentRank = traits?.ParentRank ?? .5f;
            float fear = Mathf.Clamp01(r.FearShare);
            float desire = 1f - fear;
            float runway = Mathf.Max(0f, r.Runway);

            // Classify behavioral archetype from observed simulated state
            string archId = r.WealthGroup >= 3 || (r.InControl && r.CapitalIncome > r.Earned * .35) ? "capital_owner"
                : r.SelfEmployed ? "river_builder"
                : (runway < .25f || r.DebtService > .28f || (r.IncomeRank < .35f && !r.InControl)) ? "distressed_thin_buffer"
                : (r.Homeowner && r.IncomeRank >= .45f && r.Debt > 25000) ? "leveraged_homeowner"
                : "aspiring_striver";

            ArchetypePrior arch = priors.Archetype(archId);
            // Simulated ordinary households sit at private or domain-specialist visibility
            string fameId = r.WealthGroup >= 3 ? "niche_creator"
                : (r.SelfEmployed || r.IncomeRank >= .85f) ? "domain_specialist"
                : "private";
            FameTierPrior fameTier = priors.FameTier(fameId);

            // Dominant fear & desire from MindState when available
            Layers.MindState mind = Layers.MindState.Of(lives, personId, year);
            string domDesire = "status & growth", domFear = "job or income loss";
            if (mind != null)
            {
                int bestD = 0, bestF = 0;
                for (int k = 1; k < 5; k++)
                {
                    if (mind.Desire[k] > mind.Desire[bestD]) bestD = k;
                    if (mind.Fear[k] > mind.Fear[bestF]) bestF = k;
                }
                domDesire = Layers.MindDiagram.Name(false, bestD);
                domFear = Layers.MindDiagram.Name(true, bestF);
            }

            // Resources & connections normalized 0..1 for river-channel fit
            float capitalLevel = Mathf.Clamp01((float)(Math.Log10(1 + Math.Max(0, r.Wealth)) / 6.5));
            float networkLevel = Mathf.Clamp01(0.25f + 0.35f * r.Cooperation + 0.2f * (r.Married ? 1f : 0f) + 0.2f * r.IncomeRank);
            float riskTolerance = Mathf.Clamp01(0.2f + 0.45f * Mathf.Min(1f, runway / 2f) + 0.35f * (1f - fear));

            var forecast = new Forecast
            {
                IsPublic = false,
                PersonId = personId,
                Name = $"Simulated Person #{personId}",
                Headline = $"Age {(int)r.Age} · {Mobility.QuintileNames[Mobility.Quintile(r.IncomeRank)]} (rank {PersonLife.Pct(r.IncomeRank)}) · NW {PersonLife.Money(r.Wealth)}",
                OriginSummary = $"Family origin: parents at {PersonLife.Pct(parentRank)} earnings rank" +
                                (r.Inherited > 0 ? $" · inherited {PersonLife.Money(r.Inherited)}" : "") +
                                $" · {(r.Married ? "partnered" : "single")}, {r.Kids} {(r.Kids == 1 ? "child" : "children")}" +
                                $" · strategy: {(traits != null ? PrisonersDilemma.Name(traits.Strategy) : "Tit for Tat")}",
                FearShare = fear,
                DesireShare = desire,
                Reason = r.Reason,
                Agency = r.Agency,
                DominantFear = domFear,
                DominantDesire = domDesire,
                Archetype = arch,
                PrimaryActionLabel = ActionLabel(arch?.PrimaryChoice),
                PrimaryActionProb = arch != null && arch.Probabilities.TryGetValue(arch.PrimaryChoice, out float pr) ? pr : (arch?.ChoiceConfidence ?? .5f),
                PredictedCooperation = Mathf.Clamp01(0.55f * r.Cooperation + 0.45f * ((arch?.Cooperation.Score ?? 2f) / 4f)),
                FameTier = fameTier,
                Followers = fameId == "niche_creator" ? 50000 : fameId == "domain_specialist" ? 4000 : 300,
                EliteFollowers = 0,
            };

            RankChannels(priors, capitalLevel, networkLevel, riskTolerance, 0f, forecast.RankedRivers);
            FillSurplusRiver(model?.Data, forecast);

            // 10-year mobility odds: blend empirical simulation Mobility matrix with individual family/runway/river modifiers
            int quintile = Mobility.Quintile(r.IncomeRank);
            int ageBand = Mobility.AgeBand((int)r.Age);
            float baseUp = quintile < 4 ? .28f : 0f, baseDown = quintile > 0 ? .24f : 0f;
            Mobility mob = Layers.HillMobilityLayer.Shown;
            if (mob != null)
            {
                var odds = mob.OddsFrom(quintile, ageBand);
                if (odds.lines > 0) { baseUp = (float)odds.up; baseDown = (float)odds.down; }
            }
            float lift = (r.Reason - .5f) * .12f + (parentRank - .5f) * .08f + (r.SelfEmployed ? .06f : 0f) + (runway > 1f ? .05f : -.06f) - (r.DebtService > .25f ? .08f : 0f);
            forecast.UpOdds = quintile >= 4 ? 0f : Mathf.Clamp(baseUp + lift, .02f, .88f);
            forecast.DownOdds = quintile <= 0 ? 0f : Mathf.Clamp(baseDown - lift * .75f, .02f, .85f);
            if (forecast.UpOdds + forecast.DownOdds > .95f)
            {
                float s = .95f / (forecast.UpOdds + forecast.DownOdds);
                forecast.UpOdds *= s; forecast.DownOdds *= s;
            }
            forecast.StayOdds = Mathf.Clamp01(1f - forecast.UpOdds - forecast.DownOdds);

            // Happiness & wellbeing: material security + agency + cooperation + Jev fame-tier happiness contribution - fear drag
            float security = Mathf.Clamp01((float)(Math.Log10(1 + Math.Max(0, r.Wealth) / 5000.0) / 3.0) * .5f + Mathf.Min(1f, runway / 3f) * .5f);
            float fameHappy = (fameTier?.Happiness.Score ?? 2.3f) / 4f;
            forecast.PredictedHappiness = Mathf.Clamp(100f * (0.30f * security + 0.25f * r.Agency + 0.20f * forecast.PredictedCooperation + 0.25f * fameHappy - 0.12f * Mathf.Max(0, fear - .5f)), 5f, 98f);
            forecast.AutonomyIndex = Mathf.Clamp(100f * (0.55f * r.Agency + 0.45f * ((fameTier?.Autonomy.Score ?? 3.5f) / 4f)), 2f, 99f);
            forecast.FameFearPressure = Mathf.Clamp(100f * ((fameTier?.FearExposure.Score ?? 0.3f) / 4f), 0f, 100f);
            forecast.SummaryText = FormatSummary(forecast);
            return forecast;
        }

        /// <summary>Produces a causal behavioral, river-siphoning, and fame/happiness prediction for a public figure in the social cloud.</summary>
        public static Forecast PredictPublic(SocialPeople social, SocialPeople.Person p, EconomyModel model, int year, Priors priorsOverride = null)
        {
            Priors priors = priorsOverride ?? LoadPriors();
            if (priors == null || p == null) return null;

            bool hasFirm = p.firms != null && p.firms.Length > 0;
            string fameId = p.fol >= 20_000_000 ? "global_icon"
                : p.fol >= 500_000 ? "major_figure"
                : p.fol >= 25_000 ? "niche_creator"
                : "domain_specialist";
            FameTierPrior fameTier = priors.FameTier(fameId);
            ArchetypePrior arch = priors.Archetype(hasFirm ? "capital_owner" : "attention_anchor");

            float fameNorm = Mathf.Clamp01((float)(Math.Log10(1 + Math.Max(0, p.fol)) / 9.0));
            float eliteNorm = Mathf.Clamp01(p.elite / 70f);
            float fearFromFame = (fameTier?.FearExposure.Score ?? 3f) / 4f;
            float desire = Mathf.Clamp01((arch?.DesireDominance.Score ?? 2.5f) / 4f);
            float fear = Mathf.Clamp01(0.45f * fearFromFame + 0.55f * (1f - desire));
            desire = 1f - fear;

            string cloud = social != null && p.cloud >= 0 ? social.CloudLabel(p.cloud) : "Public attention cloud";
            string firms = hasFirm ? string.Join(", ", p.firms) : "none linked";

            var forecast = new Forecast
            {
                IsPublic = true,
                PersonId = p.Index,
                Name = p.name,
                Headline = $"{FormatFollowers(p.fol)} followers · {p.elite} elite follows · {cloud}",
                OriginSummary = $"Social cloud anchor ({p.region?.ToUpperInvariant()}) · linked firms: {firms}" +
                                (p.born.HasValue ? $" · born {p.born.Value} (age {Math.Max(0, year - p.born.Value)})" : ""),
                FearShare = fear,
                DesireShare = desire,
                Reason = hasFirm ? .72f : .58f,
                Agency = hasFirm ? .88f : .70f,
                DominantFear = "reputational / status & platform risk",
                DominantDesire = hasFirm ? "capital dominance & innovation" : "attention reach & brand equity",
                Archetype = arch,
                PrimaryActionLabel = ActionLabel(arch?.PrimaryChoice),
                PrimaryActionProb = arch != null && arch.Probabilities.TryGetValue(arch.PrimaryChoice, out float pr) ? pr : (arch?.ChoiceConfidence ?? .85f),
                PredictedCooperation = Mathf.Clamp01((arch?.Cooperation.Score ?? 2.4f) / 4f),
                FameTier = fameTier,
                Followers = p.fol,
                EliteFollowers = p.elite,
            };

            float capitalLevel = hasFirm ? 1f : Mathf.Clamp01(0.45f + 0.5f * fameNorm);
            float networkLevel = Mathf.Clamp01(0.55f * fameNorm + 0.45f * eliteNorm);
            RankChannels(priors, capitalLevel, networkLevel, 0.8f, fameNorm, forecast.RankedRivers);
            FillSurplusRiver(model?.Data, forecast);

            // Already near the economic summit: high upward/compounding retention, real reputational fall risk
            float mobBoost = (fameTier?.MobilityBoost.Score ?? 3.5f) / 4f;
            forecast.UpOdds = hasFirm ? .34f : Mathf.Clamp(.22f + .25f * mobBoost, .10f, .55f);
            forecast.DownOdds = Mathf.Clamp(.08f + .16f * fearFromFame * (hasFirm ? .6f : 1f), .05f, .35f);
            forecast.StayOdds = Mathf.Clamp01(1f - forecast.UpOdds - forecast.DownOdds);

            float fameHappy = (fameTier?.Happiness.Score ?? 1.0f) / 4f;
            float autonomy = (fameTier?.Autonomy.Score ?? 0.5f) / 4f;
            // Captures the exact fame paradox Jev scored: maximal economic leverage, steep privacy/autonomy cost
            forecast.PredictedHappiness = Mathf.Clamp(100f * (0.35f * 0.90f + 0.35f * fameHappy + 0.15f * autonomy + 0.15f * forecast.PredictedCooperation), 10f, 95f);
            forecast.AutonomyIndex = Mathf.Clamp(100f * (0.35f * forecast.Agency + 0.65f * autonomy), 2f, 99f);
            forecast.FameFearPressure = Mathf.Clamp(100f * fearFromFame, 0f, 100f);
            forecast.SummaryText = FormatSummary(forecast);
            return forecast;
        }

        static void RankChannels(Priors priors, float capital, float network, float riskTol, float fame, List<ChannelFit> dst)
        {
            dst.Clear();
            foreach (ChannelPrior ch in priors.Channels)
            {
                float lev = ch.Leverage.Score / 4f;
                float bar = ch.CapitalBarrier.Score / 4f;
                float net = ch.NetworkDependency.Score / 4f;
                float risk = ch.DownsideRisk.Score / 4f;

                float barrierPenalty = Mathf.Max(0f, bar - capital) * 0.65f;
                float networkPenalty = Mathf.Max(0f, net - Mathf.Max(network, fame)) * 0.45f;
                float riskPenalty = Mathf.Max(0f, risk - riskTol) * 0.40f;
                float bonus = ch.Id == "attention_monetization" ? fame * 0.35f
                    : ch.Id == "equity_investing" ? (1f - net) * 0.18f
                    : 0f;

                float fit = Mathf.Clamp01(0.55f * lev + 0.35f * (1f - barrierPenalty) - networkPenalty - riskPenalty + bonus);
                string why = $"leverage {ch.Leverage.Score:0.0}/4 · barrier {ch.CapitalBarrier.Score:0.0}/4 · network {ch.NetworkDependency.Score:0.0}/4 · risk {ch.DownsideRisk.Score:0.0}/4";
                dst.Add(new ChannelFit { Channel = ch, FitScore = fit, Rationale = why });
            }
            dst.Sort((a, b) => b.FitScore.CompareTo(a.FitScore));
        }

        static void FillSurplusRiver(EconomyData data, Forecast f)
        {
            if (data == null || data.Industries.Count == 0) return;
            Industry best = null;
            float bestSurplus = -1;
            for (int i = 0; i < data.Industries.Count; i++)
            {
                Industry ind = data.Industries[i];
                if (ind.TierIndex <= 0) continue;
                float s = (float)Math.Max(0.0, 1.0 - ind.CompShare - ind.TaxShare - ind.DepShare);
                if (s > bestSurplus) { bestSurplus = s; best = ind; }
            }
            if (best != null)
            {
                f.HighSurplusIndustry = best.Name;
                f.HighSurplusShare = bestSurplus;
            }
        }

        static string FormatSummary(Forecast f)
        {
            var sb = new StringBuilder(512);
            sb.Append("<b>").Append(f.Name).Append("</b>\n");
            sb.Append(f.Headline).Append('\n');
            sb.Append("<color=#9AB0C8>").Append(f.OriginSummary).Append("</color>\n\n");

            sb.Append("<b>FEAR vs. DESIRE VECTOR</b>\n");
            sb.Append("• Fear ").Append(PersonLife.Pct(f.FearShare)).Append(" (moves away from: ").Append(f.DominantFear).Append(")\n");
            sb.Append("• Desire ").Append(PersonLife.Pct(f.DesireShare)).Append(" (moves toward: ").Append(f.DominantDesire).Append(")\n");
            sb.Append("• Jev archetype: <b>").Append(f.Archetype?.Name ?? "unknown").Append("</b>\n");
            sb.Append("• Predicted primary move: <b>").Append(f.PrimaryActionLabel).Append("</b> (p=")
              .Append(PersonLife.Pct(f.PrimaryActionProb)).Append(") · tit-for-tat cooperation ")
              .Append(PersonLife.Pct(f.PredictedCooperation)).Append("\n\n");

            sb.Append("<b>ECONOMIC RIVER SIPHONING (top paths for resources &amp; connections)</b>\n");
            for (int i = 0; i < Math.Min(3, f.RankedRivers.Count); i++)
            {
                ChannelFit cf = f.RankedRivers[i];
                sb.Append(i + 1).Append(". <b>").Append(cf.Channel.Name).Append("</b> (fit ")
                  .Append(PersonLife.Pct(cf.FitScore)).Append(")\n   <size=88%><color=#9AB0C8>")
                  .Append(cf.Rationale).Append("</color></size>\n");
            }
            if (!string.IsNullOrEmpty(f.HighSurplusIndustry))
            {
                sb.Append("<size=88%>Highest owners'-surplus US river: <b>").Append(f.HighSurplusIndustry)
                  .Append("</b> (").Append(PersonLife.Pct(f.HighSurplusShare)).Append(" surplus share of VA)</size>\n\n");
            }

            sb.Append("<b>FAME · MOBILITY · HAPPINESS (Jev calibrated)</b>\n");
            if (f.FameTier != null)
            {
                sb.Append("• Visibility tier: <b>").Append(f.FameTier.Name).Append("</b> (mobility boost ")
                  .Append(f.FameTier.MobilityBoost.Score.ToString("0.0", Ci)).Append("/4, fame happiness ")
                  .Append(f.FameTier.Happiness.Score.ToString("0.0", Ci)).Append("/4, autonomy ")
                  .Append(f.FameTier.Autonomy.Score.ToString("0.0", Ci)).Append("/4)\n");
            }
            sb.Append("• 10-yr mobility odds: climb <b>").Append(PersonLife.Pct(f.UpOdds))
              .Append("</b> · stay ").Append(PersonLife.Pct(f.StayOdds))
              .Append(" · fall ").Append(PersonLife.Pct(f.DownOdds)).Append('\n');
            sb.Append("• Predicted wellbeing: <b>").Append(f.PredictedHappiness.ToString("0", Ci))
              .Append("/100</b> · autonomy ").Append(f.AutonomyIndex.ToString("0", Ci))
              .Append("/100 · visibility fear ").Append(f.FameFearPressure.ToString("0", Ci)).Append("/100");
            return sb.ToString();
        }

        public static string ActionLabel(string id)
        {
            switch (id)
            {
                case "defensive_retrenchment": return "Defensive retrenchment & survival buffer";
                case "debt_financed_consumption": return "Debt-financed consumption & coping";
                case "skill_and_career_climb": return "Skill specialization & career climb";
                case "river_positioning_investment": return "Economic river positioning (property / business / IP)";
                case "capital_compounding": return "Long-horizon capital compounding";
                case "attention_monetization": return "Attention & brand equity monetization";
                default: return id ?? "balanced allocation";
            }
        }

        public static string FormatFollowers(double f)
        {
            if (f >= 1e9) return (f / 1e9).ToString("0.00", Ci) + "B";
            if (f >= 1e6) return (f / 1e6).ToString("0.0", Ci) + "M";
            if (f >= 1e3) return (f / 1e3).ToString("0.0", Ci) + "K";
            return f.ToString("0", Ci);
        }
    }
}
