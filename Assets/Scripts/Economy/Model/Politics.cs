using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Why.Economy.Model
{
    /// <summary>
    /// Government as the terrain the economy adapts to (Docs/hills-design/POLITICS.md; the user's seventh direction is
    /// Docs/research/advantage/brief-v5.md), for the United States from 1945: the parties as collectives left to right, their
    /// power (the presidency and each chamber's seats, by the stated rule), the people they represent (their national
    /// popular vote), the executive and the offices cascading below it, the standing army as the country's relative power
    /// (its share of the world's military spending, its warheads, its navy, its alliances), its trade and deficits, and the
    /// key moments (laws, regulations, trade actions, monetary turns, treaties, conflicts). Built by Tools/politics/build.py
    /// from the gated research in Docs/research/politics into Resources/Data/economy/politics.json; every number names the
    /// fact it came from. Times are decimal years.
    /// </summary>
    public sealed class Politics
    {
        public const string Path = "Data/economy/politics";

        /// <summary>A party: its place on the left-right line (-1 left, +1 right) orders the streams.</summary>
        public sealed class Party
        {
            public string Id, Name, Short;
            public int Side;
        }

        /// <summary>A president's term; <see cref="To"/> is NaN while in office.</summary>
        public sealed class Term
        {
            public string Name, Party, Fact;
            public float From, To = float.NaN;
        }

        /// <summary>A chamber's seats by party from <see cref="From"/> until its next row (the opening of a Congress, or a
        /// change within one).</summary>
        public sealed class Seats
        {
            public int Congress, Democratic, Republican, Other, Vacant, Total;
            public float From;
            public string Majority, Fact, Note;

            public int Of(string party) => party == Democrats ? Democratic : party == Republicans ? Republican : 0;

            /// <summary>The party's share of the chamber's seats, vacancies counting in the whole.</summary>
            public float Share(string party) => Total > 0 ? Of(party) / (float)Total : 0;
        }

        /// <summary>A national election's popular vote for the two parties (a presidential one, or the House's).</summary>
        public sealed class Election
        {
            public string Kind, Fact;
            public float Date;
            public long Democratic, Republican;
            public float ShareDemocratic, ShareRepublican;

            public bool Presidential => Kind == "president";
            public long Of(string party) => party == Democrats ? Democratic : party == Republicans ? Republican : 0;
            public float ShareOf(string party) => party == Democrats ? ShareDemocratic : party == Republicans ? ShareRepublican : 0;
        }

        /// <summary>
        /// A holder of an office in the executive cascade (rank 1 the vice president, then the Speaker, the Senate's majority
        /// leader, the secretaries of State, the Treasury and Defense, and the Chairman of the Joint Chiefs, who is
        /// <see cref="Military"/>). <see cref="Name"/> is null for a vacancy; <see cref="Party"/> is null for the military, and
        /// <see cref="PartyBasis"/> says where it came from ("stated", "majority of the chamber", "appointing president").
        /// </summary>
        public sealed class Office
        {
            public string Id, Title, Name, Party, PartyBasis, Fact;
            public int Rank;
            public bool Military, Acting;
            public float From, To = float.NaN;

            public bool Vacant => Name == null;

            /// <summary>Whether the office is held at <paramref name="t"/>. A term still open, or one the research closed on the
            /// span's last day (an incumbent's end is the date it was read), is held through <paramref name="end"/>, so the
            /// view's last day still has its holders.</summary>
            public bool HeldAt(float t, float end) => From <= t && t < (float.IsNaN(To) || To >= end ? end + 1e-3f : To);
        }

        /// <summary>A measured value at a time (a share, a count), with the basis it was measured on where series mix them.</summary>
        public struct Point
        {
            public float T;
            public double V;
            public string Fact, Basis;
        }

        public sealed class TradeYear
        {
            public float T;
            public double Balance, Goods, Services, Exports, Imports;
            public string Fact;
        }

        public sealed class PartnerYear
        {
            public float T;
            public double Exports, Imports, Balance;
            public string Fact;
        }

        /// <summary>A key moment: its kind is law, regulation, trade, monetary, treaty or conflict.</summary>
        public sealed class Moment
        {
            public string Id, Kind, Title, Claim, Url, Fact;
            public float Date;

            /// <summary>The industries the moment touched, highest score first; empty when Jev did not judge it (see
            /// <see cref="Touch"/>).</summary>
            public readonly List<Touch> Touches = new List<Touch>();

            /// <summary>Whether Jev judged the moment (politics.json gives it "touches"): a judged moment may touch no
            /// industry.</summary>
            public bool Judged;
        }

        /// <summary>
        /// An industry a key moment touched: <see cref="Industry"/> is an id in industries.json (a ridge of the landscape view),
        /// and <see cref="Score"/> how directly the moment changed the rules, costs, demand or competition that industry faced
        /// in the United States in the five years after it, 0-4 (1 indirectly, through a minor input or another industry; 2 a
        /// clear but secondary effect; 3 a direct effect on a large part of it; 4 written for, or aimed squarely at, it), with
        /// the <see cref="Confidence"/> of the answer (0-1). These are Jev's judgments over the moment's gated claim
        /// (Tools/politics/touches.py, Docs/research/politics/touches.json), not measurements; only industries scored 1 or
        /// more are listed.
        /// </summary>
        public sealed class Touch
        {
            public string Industry;
            public float Score, Confidence;
        }

        public sealed class Accession
        {
            public float Date;
            public int Joined, Members;
            public string Claim, Fact;
        }

        /// <summary>
        /// One wall of the standing army's advantage at a benchmark time: how far one source of it (seaControl, nuclear,
        /// alliances, spending) kept the strongest rival of the time from matching the United States, 0-4, as Jev judged it
        /// from the gated facts (Tools/politics/judge.py, Docs/research/politics/walls.json), with its confidence and the facts
        /// it read. The history view's walls use the same levels.
        /// </summary>
        public sealed class Wall
        {
            public string Source, Reading;
            public float T, Height = float.NaN, Confidence = float.NaN;
            public string[] Facts = Array.Empty<string>();
        }

        /// <summary>The sources of the standing army's advantage, in the order its ring draws them.</summary>
        public static readonly string[] WallSources = { "seaControl", "nuclear", "alliances", "spending" };

        public const string Democrats = "democratic", Republicans = "republican";

        public readonly string AsOf, Country, Name, PowerRule;
        public readonly float From, To;
        public readonly float PresidencyWeight, HouseWeight, SenateWeight;
        public readonly List<Party> Parties = new List<Party>();
        public readonly List<Term> Presidents = new List<Term>();
        public readonly List<Seats> House = new List<Seats>(), Senate = new List<Seats>();
        public readonly List<Election> Elections = new List<Election>();
        public readonly List<Office> Offices = new List<Office>();
        public readonly List<Point> Population = new List<Point>();

        /// <summary>Share of the world's military spending (%) by spender: usa, ussr, russia, china, nato, wto.</summary>
        public readonly Dictionary<string, List<Point>> MilitaryShare = new Dictionary<string, List<Point>>();

        /// <summary>Nuclear warhead stockpiles by state: usa, russia (the Soviet Union's before 1992), china.</summary>
        public readonly Dictionary<string, List<Point>> Warheads = new Dictionary<string, List<Point>>();

        /// <summary>Fleets: usShips (by fiscal year, battle force from FY1978), usCarriers, chinaShips, chinaBattleForce.</summary>
        public readonly Dictionary<string, List<Point>> Navy = new Dictionary<string, List<Point>>();

        public readonly List<Accession> Nato = new List<Accession>();

        /// <summary>The army's walls by benchmark time (<see cref="WallsAt"/>); empty until Jev's judgments are built in.</summary>
        public readonly List<Wall> Walls = new List<Wall>();
        public readonly float WarsawFrom = float.NaN, WarsawTo = float.NaN;
        public readonly List<TradeYear> Trade = new List<TradeYear>();
        public readonly Dictionary<string, List<PartnerYear>> Partners = new Dictionary<string, List<PartnerYear>>();
        public readonly List<Point> ChinaBalance = new List<Point>();
        public readonly List<Moment> Moments = new List<Moment>();

        static Politics current;

        public static Politics Get() => current;

        /// <summary>Parses politics.json (any thread) and swaps it in, in one step; logs what its self-check finds.</summary>
        public static void Provide(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                var parsed = new Politics(json);
                foreach (string problem in parsed.Check()) UnityEngine.Debug.LogError("[Why] politics: " + problem);
                current = parsed;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[Why] politics: could not parse politics.json: " + e.Message);
            }
        }

        Politics(string json)
        {
            var root = JObject.Parse(json);
            AsOf = (string)root["asOf"];
            Country = (string)root["country"];
            Name = (string)root["name"];
            From = F(root["from"]);
            To = F(root["to"]);
            var power = root["power"] as JObject;
            PresidencyWeight = F(power?["presidency"]);
            HouseWeight = F(power?["house"]);
            SenateWeight = F(power?["senate"]);
            PowerRule = (string)power?["rule"];

            foreach (JObject p in Arr(root["parties"]))
                Parties.Add(new Party { Id = (string)p["id"], Name = (string)p["name"], Short = (string)p["short"], Side = (int)p["side"] });
            Parties.Sort((a, b) => a.Side.CompareTo(b.Side));

            foreach (JObject p in Arr(root["presidents"]))
                Presidents.Add(new Term { Name = (string)p["name"], Party = (string)p["party"], From = F(p["from"]), To = F(p["to"]), Fact = (string)p["fact"] });

            ReadSeats(root["house"], House);
            ReadSeats(root["senate"], Senate);

            foreach (JObject v in Arr(root["votes"]))
            {
                Elections.Add(new Election
                {
                    Kind = (string)v["kind"], Date = F(v["date"]), Fact = (string)v["fact"],
                    Democratic = (long)v["votes"]?["democratic"], Republican = (long)v["votes"]?["republican"],
                    ShareDemocratic = F(v["share"]?["democratic"]), ShareRepublican = F(v["share"]?["republican"]),
                });
            }

            // a presidential and a House election on the same day: the presidential one stands for the year
            Elections.Sort((a, b) => a.Date != b.Date ? a.Date.CompareTo(b.Date) : a.Presidential.CompareTo(b.Presidential));

            foreach (JObject o in Arr(root["officials"]))
            {
                Offices.Add(new Office
                {
                    Id = (string)o["office"], Title = (string)o["title"], Rank = (int)o["rank"], Military = (bool?)o["military"] ?? false,
                    Name = (string)o["name"], Party = (string)o["party"], PartyBasis = (string)o["partyBasis"], Acting = (bool?)o["acting"] ?? false,
                    From = F(o["from"]), To = F(o["to"]), Fact = (string)o["fact"],
                });
            }

            Offices.Sort((a, b) => a.Rank != b.Rank ? a.Rank.CompareTo(b.Rank) : a.From.CompareTo(b.From));
            ReadPoints(root["population"], Population, "t", "v");

            if (root["military"] is JObject mil)
            {
                ReadSeries(mil["share"], MilitaryShare);
                ReadSeries(mil["warheads"], Warheads);
                ReadSeries(mil["navy"], Navy);
                foreach (JObject w in Arr(mil["walls"]))
                {
                    Walls.Add(new Wall
                    {
                        Source = (string)w["source"], T = F(w["t"]), Height = F(w["wall"]), Confidence = F(w["confidence"]), Reading = (string)w["reading"],
                        Facts = w["facts"] is JArray fa ? fa.Select(x => (string)x).Where(x => x != null).ToArray() : Array.Empty<string>(),
                    });
                }

                Walls.Sort((a, b) => a.T.CompareTo(b.T));
                if (mil["alliances"] is JObject al)
                {
                    foreach (JObject a in Arr(al["nato"]))
                        Nato.Add(new Accession { Date = F(a["date"]), Joined = (int)a["joined"], Members = (int)a["members"], Claim = (string)a["claim"], Fact = (string)a["fact"] });
                    if (al["warsawPact"] is JObject w)
                    {
                        WarsawFrom = F(w["from"]);
                        WarsawTo = F(w["to"]);
                    }
                }
            }

            if (root["trade"] is JObject tr)
            {
                foreach (JObject y in Arr(tr["balance"]))
                {
                    Trade.Add(new TradeYear
                    {
                        T = F(y["t"]), Balance = D(y["balance"]), Goods = D(y["goods"]), Services = D(y["services"]), Exports = D(y["exports"]),
                        Imports = D(y["imports"]), Fact = (string)y["fact"],
                    });
                }

                if (tr["partners"] is JObject ps)
                {
                    foreach (KeyValuePair<string, JToken> kv in ps)
                    {
                        var list = new List<PartnerYear>();
                        foreach (JObject y in Arr(kv.Value))
                            list.Add(new PartnerYear { T = F(y["t"]), Exports = D(y["exports"]), Imports = D(y["imports"]), Balance = D(y["balance"]), Fact = (string)y["fact"] });
                        Partners[kv.Key] = list;
                    }
                }

                ReadPoints(tr["chinaBalance"], ChinaBalance, "t", "v");
            }

            foreach (JObject m in Arr(root["moments"]))
            {
                var moment = new Moment
                {
                    Id = (string)m["id"], Kind = (string)m["kind"], Title = (string)m["title"], Claim = (string)m["claim"], Url = (string)m["url"],
                    Fact = (string)m["fact"], Date = F(m["date"]), Judged = m["touches"] is JArray,
                };
                // in build.py's order: highest score first, industries.json's order between equal scores
                // a row that is not an industry's score is skipped (build.py fails on one; this keeps the rest of the data)
                foreach (JToken row in Arr(m["touches"]))
                {
                    if (!(row is JObject t) || !(t["industry"] is JValue id) || id.Type != JTokenType.String) continue;
                    float score = t["score"] is JValue s && (s.Type == JTokenType.Float || s.Type == JTokenType.Integer) ? (float)s : float.NaN;
                    if (float.IsNaN(score)) continue;
                    float confidence = t["confidence"] is JValue c && (c.Type == JTokenType.Float || c.Type == JTokenType.Integer) ? (float)c : 0;
                    moment.Touches.Add(new Touch { Industry = (string)id, Score = score, Confidence = confidence });
                }
                Moments.Add(moment);
            }
        }

        // ------------------------------------------------------------------ at a time

        public Party PartyById(string id) => Parties.FirstOrDefault(p => p.Id == id);

        /// <summary>The president in office at <paramref name="t"/> (the last term begun by then), null before the first.</summary>
        public Term PresidentAt(float t)
        {
            Term found = null;
            foreach (Term p in Presidents)
            {
                if (p.From > t) break;
                found = p;
            }

            return found;
        }

        public Seats HouseAt(float t) => RowAt(House, t);
        public Seats SenateAt(float t) => RowAt(Senate, t);

        /// <summary>
        /// A party's power at <paramref name="t"/>, 0..1, by the stated rule (<see cref="PowerRule"/>): the presidency's weight
        /// if it holds it, plus each chamber's weight times its share of that chamber's seats. Vacancies and other parties'
        /// seats count for no one, so the two parties' powers add up to a little under 1 when there are some.
        /// </summary>
        public float Power(string party, float t)
        {
            float p = 0;
            if (PresidentAt(t)?.Party == party) p += PresidencyWeight;
            Seats h = HouseAt(t), s = SenateAt(t);
            if (h != null) p += HouseWeight * h.Share(party);
            if (s != null) p += SenateWeight * s.Share(party);
            return p;
        }

        /// <summary>The latest national election held by <paramref name="t"/> (a presidential one in its year, else the House's),
        /// whose popular vote is the population each party represents until the next; null before the first.</summary>
        public Election ElectionAt(float t)
        {
            Election found = null;
            foreach (Election e in Elections)
            {
                if (e.Date > t) break;
                found = e;
            }

            return found;
        }

        /// <summary>The people a party represents at <paramref name="t"/>: its national popular vote in <see cref="ElectionAt"/>;
        /// 0 before the first election measured.</summary>
        public long Represented(string party, float t) => ElectionAt(t)?.Of(party) ?? 0;

        /// <summary>The offices' holders at <paramref name="t"/>, by rank (a vacancy included as a holder without a name).</summary>
        public IEnumerable<Office> OfficesAt(float t) => Offices.Where(o => o.HeldAt(t, To));

        /// <summary>The key moments between two times, in order.</summary>
        public IEnumerable<Moment> MomentsBetween(float from, float to) => Moments.Where(m => m.Date >= from && m.Date <= to);

        /// <summary>The NATO members at <paramref name="t"/>: the count after the last accession by then, 0 before the treaty.</summary>
        public int NatoMembersAt(float t)
        {
            int n = 0;
            foreach (Accession a in Nato)
            {
                if (a.Date > t) break;
                n = a.Members;
            }

            return n;
        }

        public bool WarsawPactAt(float t) => !float.IsNaN(WarsawFrom) && t >= WarsawFrom && t < WarsawTo;

        /// <summary>
        /// For each source of the army's advantage, the wall judged at the benchmark nearest <paramref name="t"/>: the
        /// benchmarks are five years apart, so within two and a half years, and a source the judge skipped at its nearest
        /// benchmark (no US measurement) shows none rather than a neighbour's. The alliances wall stands only while NATO does
        /// (the 1950 benchmark must not show in 1946). Sources with none are left out.
        /// </summary>
        public IEnumerable<Wall> WallsAt(float t)
        {
            foreach (string source in WallSources)
            {
                if (source == "alliances" && NatoMembersAt(t) == 0) continue;
                Wall best = null;
                float gap = float.MaxValue;
                foreach (Wall w in Walls)
                {
                    if (w.Source != source) continue;
                    float d = Math.Abs(w.T - t);
                    if (d < gap)
                    {
                        gap = d;
                        best = w;
                    }
                }

                if (best != null && gap <= 2.5f) yield return best;
            }
        }

        /// <summary>
        /// The portrait atlas id of a person the view names (a president or an officeholder): "pol-" and the name in lower
        /// case, each run of other characters a single dash ("Harry S. Truman" is pol-harry-s-truman). Tools/images' politics
        /// items use the same rule (Tools/politics/portraits.py), so <c>ActorImages.TryGet(ImageId(name))</c> finds the portrait
        /// when it was fetched and packed, and falls back to a monogram when it was not.
        /// </summary>
        public static string ImageId(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            var sb = new System.Text.StringBuilder("pol-");
            bool dash = false;
            foreach (char ch in name.Normalize(System.Text.NormalizationForm.FormD))
            {
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
                char c = char.ToLowerInvariant(ch);
                if (c >= 'a' && c <= 'z' || c >= '0' && c <= '9')
                {
                    if (dash && sb.Length > 4) sb.Append('-');
                    sb.Append(c);
                    dash = false;
                }
                else dash = true;
            }

            return sb.Length > 4 ? sb.ToString() : null;
        }

        /// <summary>The US trade year that holds <paramref name="t"/>, or the nearest measured one within a year; null else.</summary>
        public TradeYear TradeAt(float t)
        {
            TradeYear best = null;
            float gap = float.MaxValue;
            foreach (TradeYear y in Trade)
            {
                float d = Math.Abs(y.T - t);
                if (d < gap)
                {
                    gap = d;
                    best = y;
                }
            }

            return gap <= 1f ? best : null;
        }

        /// <summary>
        /// A series at <paramref name="t"/>: linear between measured points no more than <paramref name="maxGap"/> years apart,
        /// the last point held for up to <paramref name="hold"/> years after it, NaN before the first point and across a wider
        /// gap (unmeasured is not drawn as measured). <paramref name="nearest"/> is the measured point closest to t, for the
        /// words that cite it.
        /// </summary>
        public static double At(List<Point> series, float t, out Point nearest, float maxGap = 6f, float hold = 2f)
        {
            nearest = default;
            if (series == null || series.Count == 0) return double.NaN;
            int i = series.FindLastIndex(p => p.T <= t);
            if (i < 0)
            {
                nearest = series[0];
                return double.NaN;
            }

            Point a = series[i];
            if (i == series.Count - 1)
            {
                nearest = a;
                return t - a.T <= hold ? a.V : double.NaN;
            }

            Point b = series[i + 1];
            nearest = t - a.T <= b.T - t ? a : b;
            if (b.T - a.T > maxGap) return t - a.T <= 0.5f ? a.V : double.NaN;
            double k = (t - a.T) / Math.Max(b.T - a.T, 1e-4f);
            return a.V + (b.V - a.V) * k;
        }

        // ------------------------------------------------------------------ checks

        /// <summary>
        /// What would make the view draw something false: a gap or an overlap between presidents, a chamber with more seats
        /// filed than it has or without a row at a time the view covers, an election without both parties' votes, a series out
        /// of order, NATO's members not adding up.
        /// </summary>
        public IEnumerable<string> Check()
        {
            for (int i = 1; i < Presidents.Count; i++)
            {
                Term a = Presidents[i - 1], b = Presidents[i];
                if (float.IsNaN(a.To) || Math.Abs(a.To - b.From) > 0.003f) yield return $"presidents: {a.Name}'s term does not meet {b.Name}'s";
            }

            if (Presidents.Count == 0 || Presidents[0].From > From + 0.5f) yield return "presidents: none at the start of the span";
            foreach ((string name, List<Seats> rows) in new[] { ("house", House), ("senate", Senate) })
            {
                if (rows.Count == 0 || rows[0].From > From + 0.01f) yield return $"{name}: no row at the start of the span";
                foreach (Seats s in rows)
                {
                    if (s.Democratic + s.Republican + s.Other + s.Vacant > s.Total) yield return $"{name}: {s.Fact} files more seats than {s.Total}";
                }

                for (int i = 1; i < rows.Count; i++)
                {
                    if (rows[i].From <= rows[i - 1].From) yield return $"{name}: rows out of order at {rows[i].Fact}";
                    if (rows[i].From - rows[i - 1].From > 2.01f) yield return $"{name}: no row between {rows[i - 1].Fact} and {rows[i].Fact}";
                }
            }

            foreach (Election e in Elections)
            {
                if (e.Democratic <= 0 || e.Republican <= 0) yield return $"votes: {e.Fact} lacks a party's votes";
            }

            foreach (var series in MilitaryShare.Concat(Warheads).Concat(Navy))
            {
                for (int i = 1; i < series.Value.Count; i++)
                {
                    if (series.Value[i].T <= series.Value[i - 1].T) yield return $"military {series.Key}: points out of order at {series.Value[i].Fact}";
                }
            }

            int members = 0;
            foreach (Accession a in Nato) members += a.Joined;
            if (Nato.Count > 0 && members != Nato[Nato.Count - 1].Members) yield return "nato: the accessions do not add up to the members";
        }

        // ------------------------------------------------------------------ parsing

        static Seats RowAt(List<Seats> rows, float t)
        {
            Seats found = null;
            foreach (Seats s in rows)
            {
                if (s.From > t) break;
                found = s;
            }

            return found;
        }

        static void ReadSeats(JToken token, List<Seats> into)
        {
            foreach (JObject r in Arr(token))
            {
                var seats = r["seats"] as JObject;
                into.Add(new Seats
                {
                    Congress = (int)r["congress"], From = F(r["from"]), Total = (int)r["total"], Majority = (string)r["majority"], Fact = (string)r["fact"],
                    Note = (string)r["note"], Democratic = (int?)seats?["democratic"] ?? 0, Republican = (int?)seats?["republican"] ?? 0,
                    Other = (int?)seats?["other"] ?? 0, Vacant = (int?)seats?["vacant"] ?? 0,
                });
            }

            into.Sort((a, b) => a.From.CompareTo(b.From));
        }

        static void ReadSeries(JToken token, Dictionary<string, List<Point>> into)
        {
            if (!(token is JObject o)) return;
            foreach (KeyValuePair<string, JToken> kv in o)
            {
                var list = new List<Point>();
                ReadPoints(kv.Value, list, "t", "v");
                into[kv.Key] = list;
            }
        }

        static void ReadPoints(JToken token, List<Point> into, string t, string v)
        {
            foreach (JObject p in Arr(token)) into.Add(new Point { T = F(p[t]), V = D(p[v]), Fact = (string)p["fact"], Basis = (string)p["basis"] });
            into.Sort((a, b) => a.T.CompareTo(b.T));
        }

        static IEnumerable<JToken> Arr(JToken t) => t is JArray a ? a : Enumerable.Empty<JToken>();

        static float F(JToken t) => t == null || t.Type == JTokenType.Null ? float.NaN : (float)t;

        static double D(JToken t) => t == null || t.Type == JTokenType.Null ? double.NaN : (double)t;
    }
}
