using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Why.Economy.Model
{
    /// <summary>
    /// Competitive advantage through history (Docs/hills-design/HISTORY.md): the stories' actors (states, armies, navies,
    /// populations, faiths, alliances, companies, labs) with their parts by year, the moments and periods, the advantage
    /// boundaries with their sources by period, and the people. Built by Tools/advantage/history.py from the verified
    /// research (Docs/research/advantage/story-*.json) into Resources/Data/economy/stories.json (not history.json: that is the households' calibration series); every number here rests on
    /// a fact whose quote was found on its source.
    /// </summary>
    public sealed class History
    {
        public const string Path = "Data/economy/stories";

        /// <summary>One measured part of an actor at a year: its value in the unit's base (people, troops, ships, or money in
        /// billions of its currency), the number and unit as the fact gives them, its size for drawing (history.py's method:
        /// NaN where the row is no size at its year, such as a quarter's flow or a total over years), and the fact.</summary>
        public sealed class Value
        {
            public float Year;
            public string Part, Unit, Fact;
            public double V, Raw = double.NaN, Size = double.NaN;
        }

        public sealed class Actor
        {
            public string Id, Name, Kind, Qid, Successor, Role, Image;
            public float From = float.NaN, To = float.NaN;
            public string[] Facts = Array.Empty<string>();
            public readonly List<Value> Series = new List<Value>();

            /// <summary>The part an actor's size is drawn from: people for a state or a population, troops for an army, ships
            /// for a navy, followers for a faith, market value or valuation for a company or a lab.</summary>
            public string MainPart
            {
                get
                {
                    string[] order = Kind switch
                    {
                        "state" or "population" => new[] { "population", "army", "gdp", "revenue" },
                        "army" => new[] { "army", "troops" },
                        "navy" => new[] { "navy", "ships" },
                        "religion" => new[] { "adherents" },
                        "company" => new[] { "marketValue", "revenue", "employees" },
                        "lab" => new[] { "valuation", "users", "revenue", "employees", "parameters" },
                        "alliance" => new[] { "army", "troops", "population" },
                        _ => new[] { "population", "army", "marketValue", "valuation", "adherents" },
                    };
                    foreach (string p in order) if (Series.Any(v => v.Part == p)) return p;
                    return Series.Count > 0 ? Series[0].Part : null;
                }
            }

            /// <summary>A part's value at a year: linear between the measured years, held flat beyond them within the actor's
            /// life; NaN where the part was never measured.</summary>
            public double At(string part, float year)
            {
                Value lo = null, hi = null;
                foreach (var v in Series)
                {
                    if (v.Part != part) continue;
                    if (v.Year <= year && (lo == null || v.Year > lo.Year)) lo = v;
                    if (v.Year >= year && (hi == null || v.Year < hi.Year)) hi = v;
                }

                if (lo == null && hi == null) return double.NaN;
                if (lo == null) return hi.V;
                if (hi == null || hi == lo || hi.Year <= lo.Year) return lo.V;
                double t = (year - lo.Year) / (hi.Year - lo.Year);
                return lo.V + (hi.V - lo.V) * t;
            }

            /// <summary>The measured value nearest a year for a part (for labels: what the fact says, never an interpolation).</summary>
            public Value Nearest(string part, float year) =>
                Series.Where(v => v.Part == part).OrderBy(v => Math.Abs(v.Year - year)).FirstOrDefault();

            /// <summary>The value a label shows for a part at a year: the latest measured in or before the year, else the
            /// nearest (a year's value stands for that calendar year: 1805's for December 1805, not 1806's).</summary>
            public Value Shown(string part, float year) =>
                Series.Where(v => v.Part == part && Math.Floor(v.Year) <= Math.Floor(year) && !double.IsNaN(v.Size)).OrderByDescending(v => v.Year).FirstOrDefault() ??
                Series.Where(v => v.Part == part && !double.IsNaN(v.Size)).OrderBy(v => Math.Abs(v.Year - year)).FirstOrDefault();

            /// <summary>A part's size at a year as far as the facts reach, never between them: the latest measured in or before
            /// the year, held for <paramref name="hold"/> years; before the first, the first when it is at most
            /// <paramref name="ahead"/> years later; otherwise NaN (not measured). No line is drawn between two measurements:
            /// most are snapshots of different things (Austerlitz's army and Jena's), not one quantity sampled.</summary>
            public double SizeAt(string part, float year, float hold = 8, float ahead = 3)
            {
                // an event's measure (a battle's force or fleet, its losses, the Terror's sentences, a year's spending) stands
                // for its moment only, not for the years after it, and never before it; a company's or a lab's value moves too fast
                // to be read back before it was measured; only a state's or a people's slow measures may be (a 1950 GDP for 1947)
                if (Event(part)) { hold = Math.Min(hold, 1); ahead = 0; }
                else if (!Slow) ahead = 0;
                Value lo = null, hi = null;
                foreach (var v in Series)
                {
                    if (v.Part != part || double.IsNaN(v.Size)) continue;
                    if (Math.Floor(v.Year) <= Math.Floor(year) && (lo == null || v.Year > lo.Year)) lo = v;
                    if (v.Year > year && (hi == null || v.Year < hi.Year)) hi = v;
                }

                if (lo != null) return year - lo.Year <= hold ? lo.Size : double.NaN;
                if (hi != null) return hi.Year - year <= ahead ? hi.Size : double.NaN;
                return double.NaN;
            }

            /// <summary>A state, a people or a church: measures that change slowly (people, GDP), which may stand a few years
            /// before they were measured.</summary>
            bool Slow => Kind == "state" || Kind == "population" || Kind == "religion";

            /// <summary>Whether a part measures an event (a battle's force or fleet, its losses, sentences, a year's spending)
            /// rather than a standing quantity (people, an army, a navy, money held or owed, market value).</summary>
            public static bool Event(string part) => part == "troops" || part == "ships" || part == "casualties" || part == "executions" ||
                                                     part == "spending" || part == "deficit" || part == "defections";

            /// <summary>The part an actor is drawn by at a year: the first of its kind's measures measured there, or null where
            /// none is. A measure outside its kind's list (a model's training compute, a chip's transistors on a company) never
            /// sizes a body: it is a property of a product, not a size of the actor.</summary>
            public string MainPartAt(float year)
            {
                foreach (string p in Order())
                    if (!double.IsNaN(SizeAt(p, year))) return p;
                return null;
            }

            string[] Order() => Kind switch
            {
                "state" or "population" => new[] { "population", "gdp", "army", "revenue", "debt" },
                "army" => new[] { "army", "troops" },
                "navy" => new[] { "navy", "ships", "sailors" },
                "religion" => new[] { "adherents", "revenue" },
                "company" => new[] { "marketValue", "revenue", "capital", "employees" },
                "lab" => new[] { "valuation", "users", "parameters", "revenue", "capital", "employees" },
                "alliance" => new[] { "army", "troops", "population" },
                _ => new[] { "population", "army", "marketValue", "valuation", "adherents" },
            };

            public bool AliveAt(float year) => (float.IsNaN(From) || year >= From - .01f) && (float.IsNaN(To) || year <= To + .01f);
        }

        public sealed class Moment
        {
            public string Id, Title, Holder, Advantage, Power, Triad, Mechanism, Outcome, Date;
            public float Year;
            public string[] Rivals = Array.Empty<string>(), Allies = Array.Empty<string>(), Facts = Array.Empty<string>();
        }

        public sealed class Period
        {
            public string Id, Title, What;
            public float From, To;
            public string[] Actors = Array.Empty<string>(), Facts = Array.Empty<string>();
        }

        public sealed class MoatSource
        {
            public string Source, What;
            public double Wall = double.NaN;
            public string[] Facts = Array.Empty<string>();
        }

        public sealed class Moat
        {
            public string Id, Actor, Market;
            public float From, To;
            public readonly List<MoatSource> Sources = new List<MoatSource>();
            public string[] Rivals = Array.Empty<string>(), Facts = Array.Empty<string>();
        }

        public sealed class Person
        {
            public string Id, Name, Qid, Role, Image;
            public int Born, Died;
            public string[] Actors = Array.Empty<string>(), Facts = Array.Empty<string>();
        }

        public sealed class Story
        {
            public string Id, Title;
            public float From, To;
            public readonly List<Actor> Actors = new List<Actor>();
            public readonly List<Moment> Moments = new List<Moment>();
            public readonly List<Period> Periods = new List<Period>();
            public readonly List<Moat> Moats = new List<Moat>();
            public readonly List<Person> People = new List<Person>();

            public Actor Get(string id) => Actors.FirstOrDefault(a => a.Id == id);
            public Person PersonById(string id) => People.FirstOrDefault(p => p.Id == id);
        }

        public sealed class Source { public string Claim, Url; }

        public readonly string AsOf;
        public readonly List<Story> Stories = new List<Story>();
        public readonly Dictionary<string, Source> Sources = new Dictionary<string, Source>();

        public Story StoryById(string id) => Stories.FirstOrDefault(s => s.Id == id);

        static History current;

        public static History Get() => current;

        /// <summary>Parses stories.json (any thread) and swaps it in, in one step.</summary>
        public static void Provide(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try { current = new History(json); }
            catch (Exception e) { UnityEngine.Debug.LogError("[Why] history: could not parse stories.json: " + e.Message); }
        }

        History(string json)
        {
            var root = JObject.Parse(json);
            AsOf = (string)root["asOf"];
            foreach (JObject s in Arr(root["stories"]))
            {
                var story = new Story { Id = (string)s["id"], Title = (string)s["title"], From = F(s["from"]), To = F(s["to"]) };
                foreach (JObject a in Arr(s["actors"]))
                {
                    var actor = new Actor
                    {
                        Id = (string)a["id"], Name = (string)a["name"], Kind = (string)a["kind"], Qid = (string)a["qid"], Successor = (string)a["successor"],
                        Role = (string)a["role"], Image = (string)a["image"], From = F(a["from"]), To = F(a["to"]), Facts = Strings(a["facts"]),
                    };
                    foreach (JObject v in Arr(a["series"]))
                        actor.Series.Add(new Value { Year = F(v["year"]), Part = (string)v["part"], Unit = (string)v["unit"], Fact = (string)v["fact"], V = D(v["value"]), Raw = D(v["raw"]), Size = D(v["size"]) });
                    story.Actors.Add(actor);
                }

                foreach (JObject m in Arr(s["moments"]))
                    story.Moments.Add(new Moment
                    {
                        Id = (string)m["id"], Title = (string)m["title"], Holder = (string)m["holder"], Advantage = (string)m["advantage"], Power = (string)m["power"],
                        Triad = (string)m["triad"], Mechanism = (string)m["mechanism"], Outcome = (string)m["outcome"], Date = (string)m["date"], Year = F(m["year"]),
                        Rivals = Strings(m["rivals"]), Allies = Strings(m["allies"]), Facts = Strings(m["facts"]),
                    });
                foreach (JObject p in Arr(s["periods"]))
                    story.Periods.Add(new Period
                    {
                        Id = (string)p["id"], Title = (string)p["title"], What = (string)p["what"], From = F(p["from"]), To = F(p["to"]),
                        Actors = Strings(p["actors"]), Facts = Strings(p["facts"]),
                    });
                foreach (JObject m in Arr(s["moats"]))
                {
                    var moat = new Moat { Id = (string)m["id"], Actor = (string)m["actor"], Market = (string)m["market"], From = F(m["from"]), To = F(m["to"]), Rivals = Strings(m["rivals"]), Facts = Strings(m["facts"]) };
                    foreach (JObject x in Arr(m["sources"]))
                        moat.Sources.Add(new MoatSource { Source = (string)x["source"], What = (string)x["what"], Wall = D(x["wall"]), Facts = Strings(x["facts"]) });
                    story.Moats.Add(moat);
                }

                foreach (JObject p in Arr(s["people"]))
                    story.People.Add(new Person
                    {
                        Id = (string)p["id"], Name = (string)p["name"], Qid = (string)p["qid"], Role = (string)p["role"], Image = (string)p["image"],
                        Born = (int)F(p["born"]), Died = float.IsNaN(F(p["died"])) ? 0 : (int)F(p["died"]), Actors = Strings(p["actors"]), Facts = Strings(p["facts"]),
                    });
                Stories.Add(story);
            }

            if (root["sources"] is JObject src)
                foreach (var kv in src)
                    if (kv.Value is JObject o) Sources[kv.Key] = new Source { Claim = (string)o["claim"], Url = (string)o["url"] };
        }

        static IEnumerable<JToken> Arr(JToken t) => t is JArray a ? a : Enumerable.Empty<JToken>();

        static string[] Strings(JToken t) => t is JArray a ? a.Select(x => (string)x).Where(x => x != null).ToArray() : Array.Empty<string>();

        static float F(JToken t)
        {
            if (t == null || t.Type == JTokenType.Null) return float.NaN;
            if (t.Type == JTokenType.Float || t.Type == JTokenType.Integer) return (float)t;
            return float.TryParse((string)t, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : float.NaN;
        }

        static double D(JToken t)
        {
            if (t == null || t.Type == JTokenType.Null) return double.NaN;
            if (t.Type == JTokenType.Float || t.Type == JTokenType.Integer) return (double)t;
            return double.TryParse((string)t, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? d : double.NaN;
        }
    }
}
