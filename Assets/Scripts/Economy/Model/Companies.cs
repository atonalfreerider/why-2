using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Newtonsoft.Json.Linq;
using Why.Economy.Data;

namespace Why.Economy.Model
{
    /// <summary>
    /// The roster of major US companies the economy's sweep through time draws (the user's eighth direction, 8 Nov 2026,
    /// verbatim in Docs/research/advantage/brief-v6.md: "add every major player in every industry"; design
    /// Docs/hills-design/SWEEP.md). Read from companies.json, which the data track writes (Tools/companies/build.py): every
    /// company Jev (TypeSafe) scored a major player of its industry ("major") and the 75 hand-picked lives of
    /// company-lives.json ("curated"), each in the industry Jev chose when confident, with its year-end market values in
    /// nominal billions of dollars: "m" a value a gated fact measured (CompaniesMarketCap's year-end history, 1996 on), "a" an
    /// approximate recalled waypoint (company-lives.json, ±30-50%), never presented as measured. Before that file lands the
    /// model falls back to company-lives.json alone, every value approximate (<see cref="Fallback"/>).
    /// <para>What a year shows of a company (<see cref="ValueAt"/>): an entry of that year (measured or approximate); between
    /// two entries at most <see cref="MaxGap"/> years apart a geometric interpolation (drawn as modelled whatever its ends);
    /// a year after the last entry the last value held (<see cref="HoldYears"/>); nothing before its first entry, across a
    /// longer gap or after it ended. Jev's judgments ride along for the hover card only (<see cref="Company.Judged"/>): they
    /// place a company through the file's own industry and size nothing.</para>
    /// Pure data: parsed on any thread (<see cref="Provide"/>), swapped in one step, read-only after.
    /// </summary>
    public sealed class Companies
    {
        /// <summary>Resources path of the roster (Tools/companies/build.py writes it).</summary>
        public const string Path = "Data/economy/companies";

        /// <summary>Resources path of the hand-picked lives, the fallback before the roster lands.</summary>
        public const string LivesPath = CompanyLives.LivesPath;

        /// <summary>The longest gap between two entries a value is interpolated across (years); a longer one draws nothing.</summary>
        public const int MaxGap = 15;

        /// <summary>A year past the last entry holds its value this many years (a company measured at the end of 2025 is
        /// drawn in 2026 with "(end of 2025)"); later, nothing.</summary>
        public const int HoldYears = 1;

        /// <summary>The fates a company's life can end in (company-lives.json's "fates").</summary>
        public const string Alive = "alive", Acquired = "acquired", Merged = "merged", Bankrupt = "bankrupt", Breakup = "breakup";

        /// <summary>How a year's value is known.</summary>
        public enum Kind
        {
            /// <summary>No value: not drawn (a grey "not measured" ring while the company exists).</summary>
            None,

            /// <summary>An "m" entry of that year: a gated, measured year-end value.</summary>
            Measured,

            /// <summary>An "a" entry of that year: a recalled waypoint, ±30-50%.</summary>
            Approx,

            /// <summary>Between two entries (geometric): modelled, whatever its ends.</summary>
            Interpolated,

            /// <summary>The last entry held a year past it ("(end of 2025)"): drawn as that entry was known.</summary>
            Held
        }

        /// <summary>Jev's answers for a company (TypeSafe System One): judgments, never measurements.</summary>
        public sealed class Judged
        {
            /// <summary>The industry Jev chose as the one earning most of its revenue, and its confidence (0-1).</summary>
            public string Industry;

            public float Confidence = float.NaN;

            /// <summary>How major a player Jev judged it in its industry at its peak (0-4), and that answer's confidence.</summary>
            public float Major = float.NaN, MajorConfidence = float.NaN;
        }

        /// <summary>One company of the roster.</summary>
        public sealed class Company
        {
            public string Id, Name, Ticker, Industry, Fate = Alive, Successor, Sells;

            /// <summary>A major player by Jev's score; one of the 75 hand-picked lives; valued privately (funding rounds).</summary>
            public bool Major, Curated, Private;

            /// <summary>Its founding and the year its life ended (null: unknown, or still alive).</summary>
            public int? Founded, Ended;

            /// <summary>Jev's judgments (null when it was not judged, and on the fallback).</summary>
            public Judged Judged;

            /// <summary>Year-end market values, nominal $bn, sorted by year: measured ("m", with its fact) or approximate.</summary>
            public (int Year, double UsdBn, bool Measured, string Fact)[] Values = Array.Empty<(int, double, bool, string)>();

            /// <summary>Its place in <see cref="All"/>.</summary>
            public int Index;

            /// <summary>The year of its first value (0 without one).</summary>
            public int FirstYear => Values.Length > 0 ? Values[0].Year : 0;

            /// <summary>The year of its last value (0 without one).</summary>
            public int LastYear => Values.Length > 0 ? Values[Values.Length - 1].Year : 0;
        }

        static volatile Companies current;

        /// <summary>The roster parsed last (null until <see cref="Provide"/> has run).</summary>
        public static Companies Get() => current;

        /// <summary>True when companies.json was missing or unreadable and the 75 lives of company-lives.json stand in (every
        /// value approximate).</summary>
        public bool Fallback { get; private set; }

        /// <summary>The roster's date (companies.json "asOf"; the fallback's: "company-lives.json").</summary>
        public string AsOf { get; private set; } = "";

        /// <summary>Every company, in the file's order.</summary>
        public IReadOnlyList<Company> All => all;

        /// <summary>What the parse skipped or could not read (logged once by the layer, on the main thread).</summary>
        public IReadOnlyList<string> Problems => problems;

        /// <summary>The parse's time (ms): the contract asks for under 20 ms for 304 companies.</summary>
        public double ParseMs { get; private set; }

        readonly List<Company> all = new List<Company>();
        readonly List<string> problems = new List<string>();
        readonly Dictionary<string, Company> byId = new Dictionary<string, Company>(StringComparer.Ordinal);

        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        /// <summary>A company by id, or null.</summary>
        public Company ById(string id) => id != null && byId.TryGetValue(id, out Company c) ? c : null;

        /// <summary>
        /// Parses the roster (any thread) and makes it the current one: companies.json when given, else the fallback from
        /// company-lives.json (every value "a", in nominal dollars, curated, not major, not judged). Companies whose industry is
        /// not one of industries.json's are skipped, each industry id named once in <see cref="Problems"/>.
        /// </summary>
        public static void Provide(EconomyData data, string companiesJson, string livesJsonFallback)
        {
            var sw = Stopwatch.StartNew();
            var m = new Companies();
            bool parsed = false;
            if (!string.IsNullOrWhiteSpace(companiesJson))
            {
                try
                {
                    m.ParseRoster(data, companiesJson, livesJsonFallback);
                    parsed = true;
                }
                catch (Exception e)
                {
                    m.Reset();
                    m.problems.Add("companies.json could not be read (" + e.GetBaseException().Message + "); company-lives.json stands in");
                }
            }

            if (!parsed && !string.IsNullOrWhiteSpace(livesJsonFallback))
            {
                try
                {
                    m.ParseLives(data, livesJsonFallback);
                }
                catch (Exception e)
                {
                    m.Reset();
                    m.problems.Add("company-lives.json could not be read either (" + e.GetBaseException().Message + "): no companies");
                }
            }

            m.ParseMs = sw.Elapsed.TotalMilliseconds;
            current = m;
        }

        void Reset()
        {
            all.Clear();
            byId.Clear();
        }

        /// <summary>companies.json, the contract of Tools/companies/build.py.</summary>
        void ParseRoster(EconomyData data, string json, string livesJson)
        {
            var root = JObject.Parse(json);
            AsOf = (string)root["asOf"] ?? "";
            // the curated lives' private valuations: the roster does not carry them, company-lives.json does
            var privately = new HashSet<string>(StringComparer.Ordinal);
            if (!string.IsNullOrWhiteSpace(livesJson))
            {
                try
                {
                    if (JObject.Parse(livesJson)["companies"] is JArray lives)
                        foreach (JToken l in lives)
                            if (l is JObject o && (bool?)o["private"] == true && (string)o["id"] != null)
                                privately.Add((string)o["id"]);
                }
                catch (Exception)
                {
                    // the fallback's own reading is not needed here: no private flags then
                }
            }

            var unknown = new HashSet<string>(StringComparer.Ordinal);
            if (!(root["companies"] is JArray rows)) throw new FormatException("no \"companies\" array");
            foreach (JToken t in rows)
            {
                if (!(t is JObject o)) continue;
                var c = Common(o, data, unknown);
                if (c == null) continue;
                c.Major = (bool?)o["major"] ?? false;
                c.Curated = (bool?)o["curated"] ?? false;
                c.Private = privately.Contains(c.Id);
                if (o["judged"] is JObject j)
                {
                    c.Judged = new Judged
                    {
                        Industry = (string)j["industry"], Confidence = Num(j["confidence"]), Major = Num(j["major"]), MajorConfidence = Num(j["majorConfidence"]),
                    };
                }

                var values = new List<(int, double, bool, string)>();
                if (o["value"] is JArray vs)
                {
                    foreach (JToken v in vs)
                    {
                        if (!(v is JArray row) || row.Count < 2) continue;
                        int year = (int?)row[0] ?? 0;
                        double usd = Num(row[1]);
                        if (year <= 0 || !(usd > 0)) continue;
                        string kind = row.Count > 2 ? (string)row[2] : "a";
                        string fact = row.Count > 3 ? (string)row[3] : null;
                        // only a gated fact's value is measured: an "m" without its fact is read as approximate
                        values.Add((year, usd, kind == "m" && !string.IsNullOrEmpty(fact), fact));
                    }
                }

                Add(c, values);
            }

            foreach (string id in unknown) problems.Add("industry '" + id + "' is not one of industries.json's: its companies are skipped");
        }

        /// <summary>company-lives.json, every value approximate (the roster has not landed).</summary>
        void ParseLives(EconomyData data, string json)
        {
            Fallback = true;
            AsOf = "company-lives.json";
            var root = JObject.Parse(json);
            var unknown = new HashSet<string>(StringComparer.Ordinal);
            if (!(root["companies"] is JArray rows)) throw new FormatException("no \"companies\" array");
            foreach (JToken t in rows)
            {
                if (!(t is JObject o)) continue;
                var c = Common(o, data, unknown);
                if (c == null) continue;
                c.Curated = true;
                c.Private = (bool?)o["private"] ?? false;
                var values = new List<(int, double, bool, string)>();
                if (o["value"] is JArray vs)
                {
                    foreach (JToken v in vs)
                    {
                        if (!(v is JArray row) || row.Count < 2) continue;
                        int year = (int?)row[0] ?? 0;
                        double usd = Num(row[1]);
                        if (year > 0 && usd > 0) values.Add((year, usd, false, null));
                    }
                }

                Add(c, values);
            }

            foreach (string id in unknown) problems.Add("industry '" + id + "' is not one of industries.json's: its companies are skipped");
        }

        /// <summary>The fields both files share; null for a row without an id or with an industry not in the data.</summary>
        static Company Common(JObject o, EconomyData data, HashSet<string> unknown)
        {
            string id = (string)o["id"];
            string industry = (string)o["industry"];
            if (string.IsNullOrEmpty(id)) return null;
            if (data != null && data.IndustryById(industry) == null)
            {
                unknown.Add(industry ?? "(none)");
                return null;
            }

            return new Company
            {
                Id = id, Name = (string)o["name"] ?? id, Ticker = (string)o["ticker"], Industry = industry, Fate = (string)o["fate"] ?? Alive,
                Successor = (string)o["successor"], Sells = (string)o["sells"], Founded = (int?)o["founded"], Ended = (int?)o["ended"],
            };
        }

        /// <summary>Sorts a company's values by year (the last of a repeated year kept) and adds it; a company with no value
        /// is left out, as the contract leaves it out of the file.</summary>
        void Add(Company c, List<(int Year, double UsdBn, bool Measured, string Fact)> values)
        {
            if (values.Count == 0 || byId.ContainsKey(c.Id)) return;
            values.Sort((a, b) => a.Year.CompareTo(b.Year));
            int kept = 0;
            for (int i = 0; i < values.Count; i++)
            {
                if (kept > 0 && values[kept - 1].Year == values[i].Year) values[kept - 1] = values[i];
                else values[kept++] = values[i];
            }

            values.RemoveRange(kept, values.Count - kept);
            c.Values = values.ToArray();
            c.Index = all.Count;
            all.Add(c);
            byId[c.Id] = c;
        }

        static float Num(JToken t)
        {
            if (t == null || t.Type == JTokenType.Null) return float.NaN;
            try
            {
                return (float)t.Value<double>();
            }
            catch (Exception)
            {
                return float.TryParse((string)t, NumberStyles.Float, Ci, out float f) ? f : float.NaN;
            }
        }

        // ------------------------------------------------------------------ what a year shows

        /// <summary>Whether the company exists in a year: founded by then (or, founding unknown, valued by then) and not
        /// past the year its life ended.</summary>
        public bool Exists(Company c, int year)
        {
            if (c == null) return false;
            int from = c.Founded ?? (c.Values.Length > 0 ? c.Values[0].Year : int.MaxValue);
            return from <= year && (c.Ended == null || year <= c.Ended.Value);
        }

        /// <summary>
        /// A company's year-end market value in a year (nominal $bn) and the year it stands for: an entry of that year
        /// (measured or approximate); between two entries at most <see cref="MaxGap"/> years apart, geometric
        /// (<see cref="Kind.Interpolated"/>); the last entry held <see cref="HoldYears"/> past it; else <see cref="Kind.None"/>
        /// (before the first entry, across a longer gap, after its life ended).
        /// </summary>
        public Kind ValueAt(Company c, int year, out double nominalBn, out int valueYear)
        {
            nominalBn = double.NaN;
            valueYear = year;
            if (c == null || c.Values.Length == 0 || !Exists(c, year)) return Kind.None;
            var v = c.Values;
            int lo = 0, hi = v.Length - 1;
            if (year < v[0].Year) return Kind.None;
            if (year > v[hi].Year)
            {
                if (year - v[hi].Year > HoldYears) return Kind.None;
                nominalBn = v[hi].UsdBn;
                valueYear = v[hi].Year;
                return Kind.Held;
            }

            // the last entry at or before the year
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (v[mid].Year <= year) lo = mid;
                else hi = mid - 1;
            }

            if (v[lo].Year == year)
            {
                nominalBn = v[lo].UsdBn;
                return v[lo].Measured ? Kind.Measured : Kind.Approx;
            }

            var a = v[lo];
            var b = v[lo + 1];
            if (b.Year - a.Year > MaxGap) return Kind.None;
            double f = (year - a.Year) / (double)(b.Year - a.Year);
            nominalBn = Math.Exp(Math.Log(a.UsdBn) + f * (Math.Log(b.UsdBn) - Math.Log(a.UsdBn)));
            return Kind.Interpolated;
        }

        /// <summary>Whether a value of this kind is drawn as measured (solid): a measured entry, or one held from it.</summary>
        public static bool Solid(Company c, Kind kind, int valueYear)
        {
            if (kind == Kind.Measured) return true;
            if (kind != Kind.Held || c == null) return false;
            foreach (var v in c.Values)
                if (v.Year == valueYear)
                    return v.Measured;
            return false;
        }

        /// <summary>Whether the company has a measured value in a year (the smoke test's honesty check).</summary>
        public static bool MeasuredIn(Company c, int year)
        {
            if (c == null) return false;
            foreach (var v in c.Values)
                if (v.Year == year)
                    return v.Measured;
            return false;
        }

        /// <summary>How many companies are major players, curated only, and measured at all (for the log).</summary>
        public (int major, int curatedOnly, int measured) Counts()
        {
            int major = 0, curated = 0, measured = 0;
            foreach (var c in all)
            {
                if (c.Major) major++;
                else if (c.Curated) curated++;
                foreach (var v in c.Values)
                {
                    if (!v.Measured) continue;
                    measured++;
                    break;
                }
            }

            return (major, curated, measured);
        }
    }
}
