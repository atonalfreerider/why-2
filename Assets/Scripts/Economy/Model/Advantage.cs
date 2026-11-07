using System;
using System.Collections.Generic;
using System.Linq;
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

            /// <summary>The balance sheet (latest quarter; NaN where research gave no figure), the P/E, the share price's
            /// uses (buybacks, dividends, stock-based pay), a loss funded by others' money, and Jev's strategy and risk.</summary>
            public readonly Balance Sheet = new Balance();
            public double Pe = double.NaN, Buybacks = double.NaN, Dividends = double.NaN, Sbc = double.NaN, Burn = double.NaN, Risk = double.NaN;
            public string Strategy, BurnWhat;
            public bool Private;

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

        /// <summary>A balance sheet in $bn (NaN: not researched), its date and the facts behind it.</summary>
        public sealed class Balance
        {
            public double TotalAssets = double.NaN, Cash = double.NaN, Ppe = double.NaN, Goodwill = double.NaN, Intangibles = double.NaN,
                Investments = double.NaN, TotalLiabilities = double.NaN, Debt = double.NaN, Leases = double.NaN, Equity = double.NaN, Commitments = double.NaN;
            public string Date, CommitmentsWhat;
            public string[] Facts = Array.Empty<string>();
            public bool Known => !double.IsNaN(Equity) || !double.IsNaN(TotalAssets);
        }

        /// <summary>An owner (a golden circle): who, what kind (fund, sovereign, pe, vc, hedge, corporate, person,
        /// households) and the stakes it holds.</summary>
        public sealed class Owner
        {
            public string Id, Name, Kind;
            public double HoldingsUsdBn;
            public readonly List<Stake> Stakes = new List<Stake>();
        }

        public sealed class Stake { public string Owner, Company, Fact, Period; public double Pct = double.NaN, UsdBn = double.NaN; }

        /// <summary>A company in one year of a simulated world: market value, book equity, cash, debt, revenue, net income
        /// ($bn), P/E (NaN for a loss), altitude (10-150) and fate (alive, absorbed by <see cref="By"/>, extinct).</summary>
        public sealed class SimState
        {
            public double V, Eq, Cash, Debt, Rev, Ni, Pe = double.NaN, Alt;
            public string Status = "alive", By;
            public bool Alive => Status == "alive";
        }

        public sealed class SimEvent { public int Year; public string Company, What; }

        /// <summary>A world of the simulation (now, the most likely course to 2030, or a scenario): its years and events.</summary>
        public sealed class World
        {
            public string Id, Title;
            public readonly SortedDictionary<int, Dictionary<string, SimState>> Years = new SortedDictionary<int, Dictionary<string, SimState>>();
            public readonly List<SimEvent> Events = new List<SimEvent>();

            /// <summary>A company's state in a year (the nearest year the world has), or null.</summary>
            public SimState At(string company, int year)
            {
                Dictionary<string, SimState> best = null;
                foreach (var kv in Years) if (kv.Key <= year || best == null) best = kv.Value;
                return best != null && best.TryGetValue(company, out SimState s) ? s : null;
            }

            public int FirstYear => Years.Count > 0 ? Years.Keys.First() : 2026;
            public int LastYear => Years.Count > 0 ? Years.Keys.Last() : 2026;
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
        public readonly List<Owner> Owners = new List<Owner>();
        public readonly List<Stake> Stakes = new List<Stake>();
        public readonly Dictionary<string, World> Worlds = new Dictionary<string, World>();
        public readonly List<string> SimAssumptions = new List<string>();

        public World WorldById(string id) => id != null && Worlds.TryGetValue(id, out World w) ? w : null;

        public Owner OwnerById(string id)
        {
            foreach (var o in Owners) if (o.Id == id) return o;
            return null;
        }
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
                if (a["balance"] is JObject bs)
                {
                    var b = x.Sheet;
                    b.TotalAssets = Num(bs["totalAssets"]); b.Cash = Num(bs["cash"]); b.Ppe = Num(bs["ppe"]); b.Goodwill = Num(bs["goodwill"]);
                    b.Intangibles = Num(bs["intangibles"]); b.Investments = Num(bs["investments"]); b.TotalLiabilities = Num(bs["totalLiabilities"]);
                    b.Debt = Num(bs["debt"]); b.Leases = Num(bs["leases"]); b.Equity = Num(bs["equity"]); b.Commitments = Num(bs["commitments"]);
                    b.Date = (string)bs["date"]; b.CommitmentsWhat = (string)bs["commitmentsWhat"]; b.Facts = Strings(bs["facts"]);
                }
                x.Pe = Num(a["pe"]); x.Buybacks = Num(a["buybacks"]); x.Dividends = Num(a["dividends"]); x.Sbc = Num(a["sbc"]);
                x.Burn = Num(a["burn"]); x.BurnWhat = (string)a["burnWhat"]; x.Risk = Num(a["risk"]); x.Strategy = (string)a["strategy"];
                x.Private = (bool?)a["private"] ?? false;
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
            foreach (JObject o in Arr(root["owners"]))
            {
                var owner = new Owner { Id = (string)o["id"], Name = (string)o["name"], Kind = (string)o["kind"], HoldingsUsdBn = Num(o["holdingsUsdBn"]) };
                foreach (JObject st in Arr(o["stakes"]))
                {
                    var stake = new Stake { Owner = owner.Id, Company = (string)st["company"], Pct = Num(st["pct"]), UsdBn = Num(st["usdBn"]), Fact = (string)st["fact"], Period = (string)st["period"] };
                    owner.Stakes.Add(stake);
                    Stakes.Add(stake);
                }

                if (double.IsNaN(owner.HoldingsUsdBn)) owner.HoldingsUsdBn = 0;
                Owners.Add(owner);
            }

            if (root["simulation"] is JObject sim)
            {
                foreach (JToken t in Arr(sim["assumptions"])) SimAssumptions.Add(t.Type == JTokenType.Object ? (string)t["name"] + ": " + (string)t["value"] + " (" + (string)t["why"] + ")" : (string)t);
                if (sim["worlds"] is JObject worlds)
                    foreach (var kv in worlds)
                    {
                        if (!(kv.Value is JObject w)) continue;
                        var world = new World { Id = kv.Key, Title = (string)w["title"] ?? kv.Key };
                        if (w["years"] is JObject years)
                            foreach (var y in years)
                            {
                                if (!int.TryParse(y.Key, out int year) || !(y.Value is JObject cs)) continue;
                                var states = new Dictionary<string, SimState>();
                                foreach (var c in cs)
                                    if (c.Value is JObject st)
                                        states[c.Key] = new SimState
                                        {
                                            V = Num(st["v"]), Eq = Num(st["eq"]), Cash = Num(st["cash"]), Debt = Num(st["debt"]), Rev = Num(st["rev"]), Ni = Num(st["ni"]),
                                            Pe = Num(st["pe"]), Alt = Num(st["alt"]), Status = (string)st["status"] ?? "alive", By = (string)st["by"],
                                        };
                                world.Years[year] = states;
                            }
                        foreach (JObject e in Arr(w["events"])) world.Events.Add(new SimEvent { Year = (int?)e["year"] ?? 0, Company = (string)e["company"], What = (string)e["what"] });
                        Worlds[world.Id] = world;
                    }
            }

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
