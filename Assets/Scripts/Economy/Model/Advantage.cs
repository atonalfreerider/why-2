using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Why.Economy.Model
{
    /// <summary>
    /// The 2026 economy as a contest (advantage.json, built by Tools/advantage/build.py from the sourced research in
    /// Docs/research/advantage and Jev's judgments; design Docs/hills-design/ADVANTAGE.md):
    /// <list type="bullet">
    /// <item>actors in the supply chain's bands (energy, tools, fabs, chips, clouds, models, apps, buyers) or on its flanks
    /// (capital, the rest of the economy, the states), each sized by its verified market cap or valuation and raised by its
    /// competitive advantage <see cref="Actor.A"/>: Helmer's seven powers judged by Jev from the verified facts, composed in
    /// code (A = s1 + s2/2 + s3/4, 0..7);</item>
    /// <item>flows of money a year between actors (each a verified fact), split by the payer's motive (desire or fear, judged);</item>
    /// <item>choke points, flashpoints (the contests, back through history), the lineage from the transistor, and the
    /// scenarios that move each actor's altitude (<see cref="Actor.Scenario"/>) and scale flows.</item>
    /// </list>
    /// Numbers are only those the truth gate verified (2026 ones read on the as-of date); judgments are marked as judged.
    /// Pure: parses on any thread, no Unity objects.
    /// </summary>
    public sealed class Advantage
    {
        public const string Path = "Data/economy/advantage";

        /// <summary>The scenario ids the app adds to the file's: the present, and the most likely course to 2030.</summary>
        public const string Now = "now", Y2030 = "2030";

        public sealed class Band { public string Id, Name; public int Index; }

        public sealed class Actor
        {
            public string Id, Name, Kind, Country, Band, Flank, Sector, Lifeline, Role, AdvantageText, Desire, Fear, Primary, Override;
            public double SizeUsdBn = double.NaN; public string SizeLabel, SizeKind, SizePeriod, SizeFact;
            public double MarginPct = double.NaN; public string MarginPeriod; public string[] MarginFacts = Array.Empty<string>();
            public bool Judged;
            /// <summary>Advantage now (0..7), in 2030 on the most likely course, and under each scenario (NaN when not judged).</summary>
            public double A = double.NaN, A2030 = double.NaN, Durability = double.NaN, PrimaryConfidence = double.NaN;
            public readonly Dictionary<string, double> Powers = new Dictionary<string, double>();
            public readonly Dictionary<string, double> Triad = new Dictionary<string, double>();
            public readonly Dictionary<string, double> Scenario = new Dictionary<string, double>();
            public string[] Sources = Array.Empty<string>();

            public bool Sized => !double.IsNaN(SizeUsdBn) && SizeUsdBn > 0;

            /// <summary>The actor's advantage under a scenario id (<see cref="Now"/>, <see cref="Y2030"/> or the file's); its
            /// present advantage where the scenario names no answer for it.</summary>
            public double AdvantageIn(string scenario)
            {
                if (!Judged) return double.NaN;
                if (string.IsNullOrEmpty(scenario) || scenario == Now) return A;
                if (scenario == Y2030) return double.IsNaN(A2030) ? A : A2030;
                return Scenario.TryGetValue(scenario, out double v) ? v : A;
            }

            /// <summary>The seven powers sorted strongest first, with the share each adds to <see cref="A"/> (s1, s2/2, s3/4).</summary>
            public List<(string power, double score, double adds)> Strata()
            {
                var list = new List<(string, double, double)>();
                foreach (var kv in Powers) list.Add((kv.Key, kv.Value, 0));
                list.Sort((a, b) => b.Item2.CompareTo(a.Item2));
                for (int i = 0; i < list.Count; i++) list[i] = (list[i].Item1, list[i].Item2, i < 3 ? list[i].Item2 / (1 << i) : 0);
                return list;
            }
        }

        public sealed class Flow
        {
            public string Id, From, To, What, Kind, Basis, Period, Fact;
            public double UsdBn, Fear = double.NaN;
            public readonly Dictionary<string, double> Scale = new Dictionary<string, double>();
            public double In(string scenario) => scenario != null && Scale.TryGetValue(scenario, out double s) ? UsdBn * s : UsdBn;
        }

        public sealed class Choke { public string Id, Actor, Controls, Share, Alternatives; public double Strength = double.NaN; public string[] Sources = Array.Empty<string>(); }

        public sealed class Flashpoint
        {
            public string Id, Era, Holder, HolderName, Title, AdvantageText, Mechanism, Outcome, Power;
            public int Year;
            public bool InArena;
            public string[] Rivals = Array.Empty<string>(), RivalNames = Array.Empty<string>(), Sources = Array.Empty<string>();
            public readonly Dictionary<string, double> Triad = new Dictionary<string, double>();
        }

        public sealed class Step { public string From, To, FromName, ToName, How; public int Year; public string[] Sources = Array.Empty<string>(); }

        public sealed class ScenarioInfo { public string Id, Title, Premise, Reading; public string[] Friendly = Array.Empty<string>(), Unfriendly = Array.Empty<string>(), Signposts = Array.Empty<string>(); }

        public sealed class Source { public string Claim, Url; }

        public readonly string AsOf, Judge, Method;
        public readonly List<Band> Bands = new List<Band>();
        public readonly List<Actor> Actors = new List<Actor>();
        public readonly List<Flow> Flows = new List<Flow>();
        public readonly List<Choke> Chokes = new List<Choke>();
        public readonly List<Flashpoint> Flashpoints = new List<Flashpoint>();
        public readonly List<Step> Lineage = new List<Step>();
        public readonly List<ScenarioInfo> Scenarios = new List<ScenarioInfo>();
        public readonly Dictionary<string, Source> Sources = new Dictionary<string, Source>();
        public readonly Dictionary<string, string> PowerNames = new Dictionary<string, string>();
        readonly Dictionary<string, Actor> byId = new Dictionary<string, Actor>();

        public Actor Get(string id) => id != null && byId.TryGetValue(id, out Actor a) ? a : null;

        public ScenarioInfo ScenarioById(string id)
        {
            foreach (var s in Scenarios) if (s.Id == id) return s;
            return null;
        }

        public string PowerName(string key) => key != null && PowerNames.TryGetValue(key, out string n) ? n : key ?? "";

        static Advantage current;
        static string currentJson;

        /// <summary>The loaded contest (null until <see cref="Provide"/> succeeds).</summary>
        public static Advantage Get() => current;

        /// <summary>Parses the file once (any thread); a missing or unreadable file leaves the contest absent.</summary>
        public static void Provide(string json)
        {
            if (string.IsNullOrEmpty(json) || ReferenceEquals(json, currentJson)) return;
            try { current = new Advantage(json); currentJson = json; }
            catch (Exception e) { UnityEngine.Debug.LogWarning("[Why] advantage.json unreadable: " + e.Message); }
        }

        Advantage(string json)
        {
            var root = JObject.Parse(json);
            AsOf = (string)root["asOf"]; Judge = (string)root["judge"]; Method = (string)root["method"];
            int bi = 0;
            foreach (JObject b in Arr(root["bands"])) Bands.Add(new Band { Id = (string)b["id"], Name = (string)b["name"], Index = bi++ });
            if (root["powers"] is JObject pw) foreach (var kv in pw) PowerNames[kv.Key] = (string)kv.Value;
            foreach (JObject a in Arr(root["actors"]))
            {
                var x = new Actor
                {
                    Id = (string)a["id"], Name = (string)a["name"], Kind = (string)a["kind"], Country = (string)a["country"], Band = (string)a["band"],
                    Flank = (string)a["flank"] ?? "center", Sector = (string)a["sector"], Lifeline = (string)a["lifeline"], Role = (string)a["role"],
                    AdvantageText = (string)a["advantage"], Desire = (string)a["desire"], Fear = (string)a["fear"], Primary = (string)a["primary"],
                    Override = (string)a["override"], Judged = (bool?)a["judged"] ?? false, A = Num(a["A"]), A2030 = Num(a["a2030"]),
                    Durability = Num(a["durability"]), PrimaryConfidence = Num(a["primaryConfidence"]), Sources = Strings(a["sources"]),
                };
                if (a["size"] is JObject s)
                {
                    x.SizeUsdBn = Num(s["usdBn"]); x.SizeLabel = (string)s["label"]; x.SizeKind = (string)s["kind"]; x.SizePeriod = (string)s["period"]; x.SizeFact = (string)s["fact"];
                }
                if (a["margin"] is JObject m)
                {
                    x.MarginPct = Num(m["pct"]); x.MarginPeriod = (string)m["period"]; x.MarginFacts = Strings(m["facts"]);
                }
                Fill(x.Powers, a["powers"]); Fill(x.Triad, a["triad"]); Fill(x.Scenario, a["scenario"]);
                if (string.IsNullOrEmpty(x.Id) || byId.ContainsKey(x.Id)) continue;
                Actors.Add(x); byId[x.Id] = x;
            }
            foreach (JObject f in Arr(root["flows"]))
            {
                var x = new Flow
                {
                    Id = (string)f["id"], From = (string)f["from"], To = (string)f["to"], What = (string)f["what"], Kind = (string)f["kind"],
                    Basis = (string)f["basis"], Period = (string)f["period"], Fact = (string)f["fact"], UsdBn = Num(f["usdBn"]), Fear = Num(f["fear"]),
                };
                Fill(x.Scale, f["scale"]);
                if (x.UsdBn > 0 && byId.ContainsKey(x.From ?? "") && byId.ContainsKey(x.To ?? "")) Flows.Add(x);
            }
            foreach (JObject c in Arr(root["chokepoints"]))
            {
                var x = new Choke { Id = (string)c["id"], Actor = (string)c["actor"], Controls = (string)c["controls"], Share = (string)c["share"], Alternatives = (string)c["alternatives"], Strength = Num(c["strength"]), Sources = Strings(c["sources"]) };
                if (byId.ContainsKey(x.Actor ?? "")) Chokes.Add(x);
            }
            foreach (JObject p in Arr(root["flashpoints"]))
            {
                var x = new Flashpoint
                {
                    Id = (string)p["id"], Era = (string)p["era"], Holder = (string)p["holder"], HolderName = (string)p["holderName"], Title = (string)p["title"],
                    AdvantageText = (string)p["advantage"], Mechanism = (string)p["mechanism"], Outcome = (string)p["outcome"], Power = (string)p["power"],
                    Year = (int?)p["year"] ?? 0, InArena = (bool?)p["inArena"] ?? false, Rivals = Strings(p["rivals"]), RivalNames = Strings(p["rivalNames"]),
                    Sources = Strings(p["sources"]),
                };
                Fill(x.Triad, p["triad"]);
                Flashpoints.Add(x);
            }
            foreach (JObject l in Arr(root["lineage"]))
                Lineage.Add(new Step { From = (string)l["from"], To = (string)l["to"], FromName = (string)l["fromName"], ToName = (string)l["toName"], How = (string)l["how"], Year = (int?)l["year"] ?? 0, Sources = Strings(l["sources"]) });
            foreach (JObject s in Arr(root["scenarios"]))
                Scenarios.Add(new ScenarioInfo { Id = (string)s["id"], Title = (string)s["title"], Premise = (string)s["premise"], Reading = (string)s["reading"], Friendly = Strings(s["friendly"]), Unfriendly = Strings(s["unfriendly"]), Signposts = Strings(s["signposts"]) });
            if (root["sources"] is JObject src)
                foreach (var kv in src)
                    if (kv.Value is JObject o) Sources[kv.Key] = new Source { Claim = (string)o["claim"], Url = (string)o["url"] };
        }

        static JArray Arr(JToken t) => t as JArray ?? new JArray();

        static double Num(JToken t) => t == null || t.Type == JTokenType.Null ? double.NaN : (double)t;

        static string[] Strings(JToken t)
        {
            if (!(t is JArray a)) return Array.Empty<string>();
            var s = new string[a.Count];
            for (int i = 0; i < a.Count; i++) s[i] = (string)a[i];
            return s;
        }

        static void Fill(Dictionary<string, double> into, JToken t)
        {
            if (!(t is JObject o)) return;
            foreach (var kv in o) if (kv.Value != null && kv.Value.Type != JTokenType.Null) into[kv.Key] = (double)kv.Value;
        }
    }
}
