using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Why.Economy.Data;

namespace Why.Economy.Model
{
    /// <summary>
    /// Company lifespans for the economy wave (company-lives.json): each company is born, rises and falls in market
    /// value, and ends (bankrupt, acquired, merged, broken up) or reaches the present, as a person's lifeline does. Values
    /// are approximate recalled market capitalizations or private valuations in nominal dollars, converted here to 2025
    /// dollars with the GDP price index and interpolated geometrically between waypoints. Jev's judgments of how each
    /// company sells (company-judgments.json, Tools/judge-companies.py) color its line; its hill choice replaces the
    /// authored industry only when confident and when the company has no stored tower (towers keep their industry so
    /// lifelines land on them). Pure data: no Unity objects, safe on any thread.
    /// </summary>
    public sealed class CompanyLives
    {
        public const string LivesPath = "Data/economy/company-lives", JudgmentsPath = "Data/economy/company-judgments";
        /// <summary>Jev's hill choice is used at or above this confidence (a judgment, not authority: below it the
        /// authored industry stands).</summary>
        public const float HillConfidence = .6f;

        /// <summary>One company's life.</summary>
        public sealed class Life
        {
            public string Id, Name, Ticker, Fate, Sells, Event, SuccessorName;
            public int Industry, AuthoredIndustry, Founded, Ended = -1, Successor = -1;
            public bool Private;
            /// <summary>Market value in 2025 dollars ($B) by year.</summary>
            public YearSeries Value = YearSeries.Empty;
            public double FirstValueYear, LastValueYear;
            /// <summary>Jev's 0..1 judgments of how it sells, and whether any were found.</summary>
            public float Manufactured, FearSold, Captive, Habit; public bool Judged;
            /// <summary>Jev's hill choice and its confidence (-1: none).</summary>
            public int JudgedHill = -1; public float JudgedHillConfidence;
            public bool Alive => Ended < 0;
            /// <summary>Persuaded = 1 - (1 - manufactured)(1 - fear); locked in = 1 - (1 - captive)(1 - habit).</summary>
            public float Persuaded => 1 - (1 - Manufactured) * (1 - FearSold);
            public float LockedIn => 1 - (1 - Captive) * (1 - Habit);
            /// <summary>The fear side of its persuasion (0 = all manufactured want, 1 = all sold fear).</summary>
            public float FearShare => Manufactured + FearSold > 1e-6f ? FearSold / (Manufactured + FearSold) : .5f;
            /// <summary>Market value (2025 $B) at a year; before the first waypoint it grows from a seed at founding.</summary>
            public double ValueAt(double year) => Value.GrowthAt(year);
            /// <summary>Its largest value and the year of it.</summary>
            public double Peak(out int year)
            {
                double best = 0; year = Founded;
                for (int i = 0; i < Value.Count; i++) if (Value.ValueAt(i) > best) { best = Value.ValueAt(i); year = (int)Value.YearAt(i); }
                return best;
            }
        }

        public readonly Life[] Lives;
        public readonly string Model, JudgedOn;
        public readonly int Reassigned;

        sealed class LivesFile { [JsonProperty("companies")] public List<Row> Companies; }
        sealed class Row
        {
            public string id, name, ticker, industry, fate, sells, @event, successor, successorName;
            public int founded; public int? ended; [JsonProperty("private")] public bool isPrivate;
            public List<double[]> value;
        }
        sealed class JudgmentsFile { public string model, judged; public Dictionary<string, Judgment> companies; }
        sealed class Judgment { public float manufactured, fear_sold, captive, habit, hillConfidence; public string hill; }

        static CompanyLives current;
        /// <summary>The loaded lives (null until <see cref="Provide"/> succeeds).</summary>
        public static CompanyLives Get() => current;

        /// <summary>Parses the two files once (any thread). Missing judgments leave every company neutral gold.</summary>
        public static void Provide(EconomyData data, string livesJson, string judgmentsJson)
        {
            if (current != null || data == null || string.IsNullOrEmpty(livesJson)) return;
            current = new CompanyLives(data, livesJson, judgmentsJson);
        }

        CompanyLives(EconomyData data, string livesJson, string judgmentsJson)
        {
            var rows = JsonConvert.DeserializeObject<LivesFile>(livesJson)?.Companies ?? new List<Row>();
            var judged = string.IsNullOrEmpty(judgmentsJson) ? null : JsonConvert.DeserializeObject<JudgmentsFile>(judgmentsJson);
            Model = judged?.model; JudgedOn = judged?.judged;
            var capture = data.Circuit?.Capture;
            var list = new List<Life>(); var index = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var r in rows)
            {
                var industry = data.IndustryById(r.industry);
                if (industry == null || r.value == null || r.value.Count == 0) continue;
                var life = new Life { Id = r.id, Name = r.name, Ticker = r.ticker ?? "", Fate = r.fate ?? "alive", Sells = r.sells, Event = r.@event,
                    SuccessorName = r.successorName, Founded = r.founded, Ended = r.ended ?? -1, Private = r.isPrivate,
                    Industry = industry.Index, AuthoredIndustry = industry.Index };
                // Nominal waypoints to 2025 dollars; a company founded after 1946 starts from a seed at its founding.
                var points = new List<double[]>();
                if (r.founded >= 1946 && r.founded < r.value[0][0]) points.Add(new[] { (double)r.founded, Math.Min(.005, r.value[0][1] * .1) });
                foreach (var p in r.value) points.Add(new[] { p[0], Math.Max(1e-4, data.Real(p[1], p[0])) });
                life.Value = new YearSeries(points); life.FirstValueYear = points[0][0]; life.LastValueYear = points[points.Count - 1][0];
                if (judged?.companies != null && judged.companies.TryGetValue(r.id, out var j))
                {
                    life.Judged = true; life.Manufactured = j.manufactured; life.FearSold = j.fear_sold; life.Captive = j.captive; life.Habit = j.habit;
                    var hill = j.hill == null ? null : data.IndustryById(j.hill);
                    if (hill != null && hill.TierIndex > 0) { life.JudgedHill = hill.Index; life.JudgedHillConfidence = j.hillConfidence; }
                }
                bool tower = false;
                if (capture != null) foreach (var c in capture) if (Matches(c.Ticker, c.Name, life)) tower = true;
                if (!tower && life.JudgedHill >= 0 && life.JudgedHill != life.Industry && life.JudgedHillConfidence >= HillConfidence)
                { life.Industry = life.JudgedHill; Reassigned++; }
                index[r.id] = list.Count; list.Add(life);
            }
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].successor == null || !index.TryGetValue(rows[i].id, out int self)) continue;
                if (index.TryGetValue(rows[i].successor, out int to)) list[self].Successor = to;
            }
            Lives = list.ToArray();
        }

        /// <summary>Whether a stored company (circuit.json capture, the towers) is this life: by ticker, else by name.</summary>
        public static bool Matches(string ticker, string name, Life life) =>
            !string.IsNullOrEmpty(ticker) && !string.IsNullOrEmpty(life.Ticker) ? string.Equals(ticker, life.Ticker, StringComparison.OrdinalIgnoreCase)
            : name != null && life.Name.StartsWith(name, StringComparison.OrdinalIgnoreCase);

        /// <summary>The index of the life that matches a stored tower, or -1.</summary>
        public int OfTower(string ticker, string name)
        {
            for (int i = 0; i < Lives.Length; i++) if (Matches(ticker, name, Lives[i])) return i;
            return -1;
        }
    }
}
