using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Why.Humans
{
    /// <summary>
    /// The state beneath every civilization (Data/government.json, built by Tools/government): its fiscal size and
    /// what it spends on through history, the United States budget year by year since 1947, how heavily each US
    /// industry is regulated and how much it shapes its own rules, and the political actors who held office. Shares are
    /// fractions of the economy's output (GDP). Modern samples are published data; most of history is Jev judgments
    /// calibrated on the data, flagged by <see cref="Sample.basis"/>.
    /// </summary>
    public sealed class GovernmentHistory
    {
        public const string Path = "Data/government";
        public const string SharedKey = "humans.government";

        public sealed class Sample { public int year; public float revenue, military, social, other, confidence, regulation; public string basis; }
        public sealed class UsYear
        {
            public int year;
            public float receipts, individual, payroll, corporate, otherReceipts, defense, socialSecurity, health, incomeSecurity, netInterest, otherOutlays, stateLocal;
            public float Social => socialSecurity + health + incomeSecurity;
            public float Outlays => defense + Social + netInterest + otherOutlays;
        }
        public sealed class Industry { public float regulation, capture, regulationConfidence, captureConfidence; }
        public sealed class Actor { public string name, civ, office, figure; public int from; public int? to; }

        public Dictionary<string, Sample[]> civs = new Dictionary<string, Sample[]>();
        public UsYear[] us = Array.Empty<UsYear>();
        public Dictionary<string, Industry> industries = new Dictionary<string, Industry>();
        public Actor[] actors = Array.Empty<Actor>();
        public string note;

        public static GovernmentHistory Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                var g = JsonConvert.DeserializeObject<GovernmentHistory>(json);
                if (g == null) return null;
                foreach (var kv in g.civs) Array.Sort(kv.Value, (a, b) => a.year.CompareTo(b.year));
                Array.Sort(g.us, (a, b) => a.year.CompareTo(b.year));
                return g;
            }
            catch (Exception e) { Debug.LogWarning("[Why] Government history unreadable: " + e.Message); return null; }
        }

        /// <summary>A civ's state at a year, interpolated between samples (false outside its series).</summary>
        public bool At(string civ, double year, out Sample s)
        {
            s = null;
            if (civ == null || !civs.TryGetValue(civ, out var series) || series.Length == 0) return false;
            if (year < series[0].year || year > series[series.Length - 1].year + 5) return false;
            int k = 0; while (k < series.Length - 1 && series[k + 1].year <= year) k++;
            var a = series[k]; var b = k + 1 < series.Length ? series[k + 1] : a;
            float t = b.year > a.year ? Mathf.Clamp01((float)((year - a.year) / (b.year - a.year))) : 0;
            s = new Sample { year = (int)year, revenue = Mathf.Lerp(a.revenue, b.revenue, t), military = Mathf.Lerp(a.military, b.military, t),
                social = Mathf.Lerp(a.social, b.social, t), other = Mathf.Lerp(a.other, b.other, t),
                confidence = Mathf.Lerp(a.confidence, b.confidence, t), regulation = Mathf.Lerp(a.regulation, b.regulation, t), basis = t < .5f ? a.basis : b.basis };
            return true;
        }

        /// <summary>The US budget of a year (the nearest stored year).</summary>
        public UsYear Us(int year)
        {
            if (us.Length == 0) return null;
            UsYear best = us[0];
            foreach (var y in us) if (Math.Abs(y.year - year) < Math.Abs(best.year - year)) best = y;
            return best;
        }

        /// <summary>The civ's actors in office at a year.</summary>
        public IEnumerable<Actor> InOffice(string civ, double year)
        {
            foreach (var a in actors)
                if (a.civ == civ && a.from <= year && (a.to ?? 9999) >= year) yield return a;
        }
    }
}
