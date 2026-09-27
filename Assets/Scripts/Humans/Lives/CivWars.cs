using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Why.Humans.Lives
{
    /// <summary>One war as it touched one stream: when, and what share of its military-age men it killed.</summary>
    public struct WarExposure
    {
        /// <summary>Calendar year the war began.</summary>
        public double Start;

        /// <summary>Calendar year it ended, at least one year after <see cref="Start"/>.</summary>
        public double End;

        /// <summary>Share of the stream's military-age men killed over the whole war (0..1).</summary>
        public float Mortality;
    }

    /// <summary>
    /// The wars a stream fought (demography.json), chronological, with the per-civ male excess mortality where
    /// the data gives one (<c>maleExcessByCiv</c>) and the blended value otherwise. Drives the war deaths of the
    /// men's lifelines and the dent a war leaves in the men's side of the population curve: "the gender
    /// difference, especially with war".
    /// </summary>
    public sealed class CivWars
    {
        /// <summary>Youngest age exposed to war mortality.</summary>
        public const float MilitaryAgeMin = 16f;

        /// <summary>Oldest age exposed to war mortality.</summary>
        public const float MilitaryAgeMax = 45f;

        /// <summary>Approximate share of all men who are of military age (young pre-modern populations).</summary>
        const float MilitaryShareOfMen = 0.45f;

        /// <summary>Years over which the cohorts a war thinned out age out of the population curve.</summary>
        const double RecoveryYears = 25;

        /// <summary>Beyond this many recovery constants a war no longer affects the curve.</summary>
        const double RecoveryHorizon = 6;

        const float MinMaleFactor = 0.6f;

        /// <summary>Wars touching this stream, sorted by start year.</summary>
        public readonly IReadOnlyList<WarExposure> Wars;

        CivWars(List<WarExposure> wars)
        {
            Wars = wars;
        }

        /// <summary>Collects the wars that list <paramref name="civ"/>.</summary>
        /// <param name="world">the shared human model</param>
        /// <param name="civ">the stream</param>
        /// <param name="byCiv">per-war, per-civ mortality overrides (war id -> civ id -> share), may be empty</param>
        public static CivWars For(HumanWorld world, Civ civ, Dictionary<string, Dictionary<string, double>> byCiv)
        {
            List<WarExposure> list = new List<WarExposure>();
            foreach (War w in world.Wars)
            {
                if (w?.civs == null || Array.IndexOf(w.civs, civ.Id) < 0) continue;
                double m = w.maleExcessMortality;
                if (w.id != null && byCiv.TryGetValue(w.id, out Dictionary<string, double> map) &&
                    map != null && map.TryGetValue(civ.Id, out double own))
                {
                    m = own;
                }

                if (m <= 0) continue;
                list.Add(new WarExposure
                {
                    Start = w.startYear,
                    End = Math.Max(w.endYear, w.startYear) + 1, // a war dated to a single year lasts that year
                    Mortality = Mathf.Clamp01((float)m)
                });
            }

            list.Sort((a, b) => a.Start.CompareTo(b.Start));
            return new CivWars(list);
        }

        /// <summary>
        /// Men relative to women in the population curve at a calendar year (1 in peace): a war removes
        /// its share of military-age men as it goes, and the gap closes as those cohorts age out.
        /// </summary>
        public float MaleFactor(double year)
        {
            double deficit = 0;
            for (int i = 0; i < Wars.Count; i++)
            {
                WarExposure w = Wars[i];
                if (year <= w.Start) break;
                double after = year - w.End;
                if (after > RecoveryHorizon * RecoveryYears) continue;
                double elapsed = after < 0 ? (year - w.Start) / (w.End - w.Start) : Math.Exp(-after / RecoveryYears);
                deficit += w.Mortality * elapsed;
            }

            return Mathf.Max(MinMaleFactor, 1f - MilitaryShareOfMen * (float)deficit);
        }

        /// <summary>
        /// Draws whether a man dies in one of this stream's wars. Each war he is of military age during
        /// kills him with its mortality, scaled by how much of the war (or of his military age) he was
        /// exposed to; the death date falls inside that exposure.
        /// </summary>
        /// <param name="birth">calendar year of birth</param>
        /// <param name="naturalDeath">calendar year he would otherwise die</param>
        /// <param name="rng">deterministic random source (two draws per exposure)</param>
        /// <param name="death">the war death, when there is one</param>
        public bool DrawWarDeath(double birth, double naturalDeath, System.Random rng, out double death)
        {
            death = naturalDeath;
            for (int i = 0; i < Wars.Count; i++)
            {
                WarExposure w = Wars[i];
                double a = Math.Max(w.Start, birth + MilitaryAgeMin);
                double b = Math.Min(Math.Min(w.End, birth + MilitaryAgeMax), naturalDeath);
                if (b <= a) continue;
                double window = Math.Min(w.End - w.Start, MilitaryAgeMax - MilitaryAgeMin);
                double p = w.Mortality * Math.Min(1.0, (b - a) / window);
                double hit = rng.NextDouble(), when = rng.NextDouble();
                if (hit >= p) continue;
                death = a + when * (b - a);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Parses the optional per-civ war mortality (<c>wars[].maleExcessByCiv</c>) from demography.json,
        /// which the shared <see cref="HumanWorld"/> model does not keep. Thread safe.
        /// </summary>
        public static Dictionary<string, Dictionary<string, double>> ParseOverrides(string demographyJson)
        {
            Dictionary<string, Dictionary<string, double>> result = new Dictionary<string, Dictionary<string, double>>();
            if (string.IsNullOrEmpty(demographyJson)) return result;
            try
            {
                OverrideFile file = JsonConvert.DeserializeObject<OverrideFile>(demographyJson);
                if (file?.wars == null) return result;
                foreach (OverrideDto w in file.wars)
                {
                    if (w?.id != null && w.maleExcessByCiv != null) result[w.id] = w.maleExcessByCiv;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Why] LifelinesLayer could not read per-civ war mortality: {e.Message}");
            }

            return result;
        }

        sealed class OverrideDto
        {
            public string id;
            public Dictionary<string, double> maleExcessByCiv;
        }

        sealed class OverrideFile
        {
            public List<OverrideDto> wars = new List<OverrideDto>();
        }
    }
}
