using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Why.Economy.Data;

namespace Why.Economy.Model
{
    /// <summary>
    /// Other governments as model changes (regimes.json, Tools/regimes/build.py): the same US hills stood on another
    /// state's foundation, each state drawn from its measured structure. For every regime (the United States as the
    /// baseline, Sweden, China, Saudi Arabia and the Soviet Union around 1980):
    /// <list type="bullet">
    /// <item>government spending and revenue, shares of GDP (IMF Fiscal Monitor; the Soviet state budget from the
    /// research), and what the state lives on (<see cref="Regime.Mix"/>: shares of its revenue by source);</item>
    /// <item>the state-owned share of the country's largest firms, and the state's holdings among the leading firms, each on
    /// one of the scene's industries (<see cref="Regime.Firms"/>: the summits it owns, with owner, share and source);</item>
    /// <item>trade openness (World Bank), the top 1%'s shares of wealth and income (WID), the electoral democracy index
    /// (V-Dem);</item>
    /// <item>how heavily the state's rules bind each industry and how much the industry shapes them (Jev Scores: model
    /// judgments; the US keeps the government file's own).</item>
    /// </list>
    /// Every measure keeps its year and source; a missing one is null in the file and NaN here. Pure: parses on any thread,
    /// no Unity objects.
    /// </summary>
    public sealed class Regimes
    {
        public const string Path = "Data/economy/regimes";

        /// <summary>The parts of a state's revenue, in the file's order (the bedrock's strata, top down).</summary>
        public enum Part { Personal = 0, Corporate, Social, Goods, Property, Resources, Enterprises, Other }
        public const int Parts = 8;
        public static readonly string[] PartIds = { "personal", "corporate", "social", "goods", "property", "resources", "enterprises", "other" };
        /// <summary>Where each part comes from on the landscape (the drawing's grammar, not data).</summary>
        public static readonly string[] PartFrom = { "income taxes from the people", "profit taxes from the private summits", "contributions from wages",
            "taxes on spending from the markets", "property taxes from homes", "resource revenue from the ground under the raw hills",
            "state enterprises' profits from the steel summits", "the rest (fees, interest, other) stays in the stratum" };

        /// <summary>One measured value: its value (NaN when not found), year and source.</summary>
        public sealed class Measure
        {
            public double Value = double.NaN; public int Year; public string Source, Url, Note, Count;
            /// <summary>What a share is of when not GDP (the Soviet budget: "GNP"); null for GDP.</summary>
            public string Basis;
            public string Of => string.IsNullOrEmpty(Basis) ? "GDP" : Basis;
            /// <summary>The year before's value (V-Dem), NaN when absent.</summary>
            public double Previous = double.NaN; public int PreviousYear;
            /// <summary>A source's model imputation where no measurement exists (WID's grade-0 wealth shares): context for
            /// the panel, never drawn; NaN when absent.</summary>
            public double Modelled = double.NaN; public int ModelledYear;
            public bool Has => !double.IsNaN(Value);
        }

        /// <summary>A firm named on an industry: a holding of the state, or the largest private firm beside it.</summary>
        public sealed class Firm
        {
            public string Name, IndustryId, Holder, AsOf, Url, Note;
            /// <summary>The industry index (EconomyData.Industries), -1 when unknown.</summary>
            public int Industry = -1;
            /// <summary>The state's share (0..1; NaN when the source gives control without a share) and whether it controls the firm.</summary>
            public double StateShare = double.NaN; public bool Controlled;
            /// <summary>Whether it is the largest firm named on its industry (by revenue, computed by the build): the summit.</summary>
            public bool Leads;
            /// <summary>The state's share for drawing: all of it where the state controls the firm (Fortune: government owned
            /// 50% or more; or a controlling stake in the ownership research), else its minority stake, else none.</summary>
            public double Share => Controlled ? 1 : double.IsNaN(StateShare) ? 0 : StateShare;
        }

        /// <summary>One regime.</summary>
        public sealed class Regime
        {
            public string Id, Name, Short, Form, Era, Country, Notes;
            public int Year; public bool Baseline;
            /// <summary>StateFirms: the public sector's share of listed companies' market value (the USSR's: by law);
            /// Fortune: the state-controlled share of the country's Fortune Global 500 firms (Count says "79 of 116").</summary>
            public Measure Spending, Revenue, Trade, Democracy, Top1Wealth, Top1Income, StateFirms, Fortune;
            /// <summary>Shares of the state's revenue by <see cref="Part"/> (sum 1), null when not found; its year, basis and source.</summary>
            public double[] Mix; public int MixYear; public string MixBasis, MixSource, MixUrl, MixNote;
            /// <summary>The ownership of a summit no firm is named for (0: private, 1: the state's by law) and why.</summary>
            public double DefaultOwnership; public string DefaultNote;
            public Firm[] Firms = Array.Empty<Firm>();
            /// <summary>Per industry index: how heavily the state's rules bind it and how much it shapes them (0..1; NaN: not judged).</summary>
            public float[] Regulation, Capture; public string Judged;

            /// <summary>The state's share of an industry's summit: its share of the industry's largest named firm, else the
            /// regime's default (private, or the state's by law).</summary>
            public double StateShare(int industry) { var f = SummitFirm(industry); return f == null ? DefaultOwnership : f.Share; }

            /// <summary>The industry's largest named firm (its summit), or null.</summary>
            public Firm SummitFirm(int industry) { foreach (var f in Firms) if (f.Industry == industry && f.Leads) return f; return null; }
        }

        public readonly Regime[] All;
        public readonly string Built, JevModel;

        static Regimes current;
        /// <summary>The loaded regimes (null until <see cref="Provide"/> succeeds).</summary>
        public static Regimes Get() => current;

        /// <summary>Parses the file once (any thread); a missing or unreadable file leaves the regimes absent.</summary>
        public static void Provide(EconomyData data, string json)
        {
            if (current != null || data == null || string.IsNullOrEmpty(json)) return;
            try { current = new Regimes(data, json); }
            catch (Exception e) { UnityEngine.Debug.LogWarning("[Why] Regimes unreadable: " + e.Message); }
        }

        Regimes(EconomyData data, string json)
        {
            var root = JObject.Parse(json);
            Built = (string)root["built"]; JevModel = (string)root["jev"]?["model"];
            var list = new List<Regime>();
            foreach (JObject r in root["regimes"] as JArray ?? new JArray())
            {
                var g = new Regime
                {
                    Id = (string)r["id"], Name = (string)r["name"], Short = (string)r["short"], Form = (string)r["form"], Era = (string)r["era"],
                    Country = (string)r["country"], Year = (int?)r["year"] ?? 0, Baseline = (bool?)r["baseline"] ?? false, Notes = Text(r["notes"]),
                    Spending = Read(r["spending"]), Revenue = Read(r["revenue"]), Trade = Read(r["trade"]), Democracy = Read(r["democracy"]),
                    Top1Wealth = Read(r["top1Wealth"]), Top1Income = Read(r["top1Income"]), StateFirms = Read(r["stateFirms"]), Fortune = Read(r["fortune"]),
                };
                if (r["revenueMix"] is JObject mix && mix["parts"] is JObject parts)
                {
                    g.Mix = new double[Parts];
                    for (int k = 0; k < Parts; k++) g.Mix[k] = (double?)parts[PartIds[k]] ?? 0;
                    g.MixYear = (int?)mix["year"] ?? 0; g.MixBasis = (string)mix["basis"]; g.MixSource = (string)mix["source"]; g.MixUrl = (string)mix["url"];
                    g.MixNote = mix["computed"] is JArray notes && notes.Count > 0 ? (string)notes[0] : null;
                }
                if (r["ownership"] is JObject own)
                {
                    g.DefaultOwnership = (double?)own["default"] ?? 0; g.DefaultNote = (string)own["defaultNote"];
                    var firms = new List<Firm>();
                    foreach (JObject f in own["firms"] as JArray ?? new JArray())
                    {
                        var firm = new Firm { Name = (string)f["name"], IndustryId = (string)f["industry"], Holder = (string)f["holder"], AsOf = Text(f["asOf"]),
                            Url = (string)f["url"], Note = (string)f["note"], StateShare = (double?)f["stateShare"] ?? double.NaN, Controlled = (bool?)f["controlled"] ?? false,
                            Leads = (bool?)f["leads"] ?? false };
                        var ind = data.IndustryById(firm.IndustryId); if (ind != null) firm.Industry = ind.Index;
                        firms.Add(firm);
                    }
                    g.Firms = firms.ToArray();
                }
                int n = data.Industries.Count;
                g.Regulation = new float[n]; g.Capture = new float[n];
                for (int i = 0; i < n; i++) { g.Regulation[i] = float.NaN; g.Capture[i] = float.NaN; }
                if (r["industries"] is JObject judged)
                {
                    g.Judged = (string)r["industriesJudged"];
                    foreach (var kv in judged)
                    {
                        var ind = data.IndustryById(kv.Key); if (ind == null || !(kv.Value is JObject v)) continue;
                        g.Regulation[ind.Index] = (float?)v["regulation"] ?? float.NaN; g.Capture[ind.Index] = (float?)v["capture"] ?? float.NaN;
                    }
                }
                list.Add(g);
            }
            All = list.ToArray();
        }

        static string Text(JToken t) => t == null || t.Type == JTokenType.Null ? null : t.Type == JTokenType.String ? (string)t : t.ToString();

        static Measure Read(JToken t)
        {
            var m = new Measure();
            if (!(t is JObject o)) return m;
            if (o["value"] != null && (o["value"].Type == JTokenType.Float || o["value"].Type == JTokenType.Integer)) m.Value = (double)o["value"];
            m.Year = o["year"] != null && o["year"].Type == JTokenType.Integer ? (int)o["year"] : 0;
            m.Source = (string)o["source"]; m.Url = (string)o["url"]; m.Note = Text(o["note"]); m.Count = Text(o["count"]); m.Basis = (string)o["basis"];
            if (o["previous"] is JObject p && p["value"] != null && p["value"].Type != JTokenType.Null) { m.Previous = (double)p["value"]; m.PreviousYear = (int?)p["year"] ?? 0; }
            if (o["modelled"] is JObject q && q["value"] != null && q["value"].Type != JTokenType.Null) { m.Modelled = (double)q["value"]; m.ModelledYear = (int?)q["year"] ?? 0; }
            return m;
        }

        /// <summary>The regime with an id, or null.</summary>
        public Regime ById(string id) { foreach (var r in All) if (r.Id == id) return r; return null; }

        /// <summary>The baseline regime (the United States), else the first.</summary>
        public Regime Baseline { get { foreach (var r in All) if (r.Baseline) return r; return All.Length > 0 ? All[0] : null; } }

        /// <summary>What does not hold of the regimes (empty when every check passes): there is one baseline and it is the US;
        /// every regime has its spending and revenue; shares lie in range; a revenue mix sums to one; every named firm stands
        /// on a known production industry with a share in [0, 1]; an industry has at most one leading firm; and a summit is
        /// never more than wholly the state's.</summary>
        public List<string> Problems(EconomyData data)
        {
            var list = new List<string>();
            int baselines = 0;
            foreach (var r in All)
            {
                if (r.Baseline) { baselines++; if (r.Id != "us") list.Add(r.Name + " is the baseline, not the United States"); }
                if (!r.Spending.Has || !r.Revenue.Has) list.Add(r.Name + " lacks its spending or revenue");
                foreach (var (m, name) in new[] { (r.Spending, "spending"), (r.Revenue, "revenue"), (r.Top1Wealth, "top 1% wealth"), (r.Top1Income, "top 1% income"),
                    (r.Democracy, "democracy"), (r.StateFirms, "state firms"), (r.Fortune, "Fortune Global 500 share") })
                    if (m.Has && (m.Value < 0 || m.Value > 1)) list.Add(r.Name + "'s " + name + " " + m.Value + " is not a share");
                if (r.Trade.Has && (r.Trade.Value < 0 || r.Trade.Value > 4)) list.Add(r.Name + "'s trade share " + r.Trade.Value + " is out of range");
                if (r.Mix != null)
                {
                    double sum = 0; foreach (double v in r.Mix) { sum += v; if (v < 0) list.Add(r.Name + " has a negative revenue part"); }
                    if (Math.Abs(sum - 1) > 1e-3) list.Add(r.Name + "'s revenue mix sums to " + sum.ToString("0.000"));
                }
                foreach (var f in r.Firms)
                {
                    if (f.Industry < 0) list.Add(r.Name + ": " + f.Name + " stands on no known industry");
                    else if (data.Industries[f.Industry].TierIndex == 0) list.Add(r.Name + ": " + f.Name + " stands on a government industry");
                    if (!double.IsNaN(f.StateShare) && (f.StateShare < 0 || f.StateShare > 1)) list.Add(r.Name + ": " + f.Name + "'s state share is not a share");
                }
                var led = new HashSet<int>();
                foreach (var f in r.Firms) if (f.Leads && f.Industry >= 0 && !led.Add(f.Industry)) list.Add(r.Name + " names two leading firms on " + data.Industries[f.Industry].Name);
                for (int i = 0; i < data.Industries.Count; i++)
                {
                    double s = r.StateShare(i); if (s < 0 || s > 1) list.Add(r.Name + "'s summit of " + data.Industries[i].Name + " is " + s + " the state's");
                    if (!float.IsNaN(r.Regulation[i]) && (r.Regulation[i] < 0 || r.Regulation[i] > 1)) list.Add(r.Name + "'s regulation of " + data.Industries[i].Name + " is out of range");
                }
            }
            if (baselines != 1) list.Add(baselines + " baselines (one expected)");
            return list;
        }
    }
}
