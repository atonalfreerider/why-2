using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Why.Humans.Social;

namespace Why.Economy.Model
{
    /// <summary>
    /// The real public figures of the lives view (M3, MODALITIES.md 7.3; decision 34): the picks the author approved in
    /// Data/social/visibility.json (built by Tools/lives/build.py, documented in Docs/data/lives.md), with their fortunes,
    /// readership and follower snapshot as stored, and what the view computes from them. The picks are read, not chosen:
    /// the printed rules ran in the builder and the author approved their result, so nobody is drawn from a shortlist that
    /// is not approved, and a rule without a pick (little attention: nobody in people.json meets it) keeps its wording and
    /// draws nobody. For each pick, the fortune of each compared year (the rules' list, the World's Billionaires) and the
    /// calendar year's page views are recomputed from the stored estimates and monthly counts, indexed to the first year's
    /// (2016 = 1: one log axis for both, never two), and "together in k of m years" (both rose or both fell, year on year; a
    /// year in which either did not move counts in m and not in k) is counted again, as a check on the file. Public adults
    /// only: each pick is checked again against people.json as the builder's rule states it, and a pick that fails, or
    /// cannot be checked, is left out as a pick of a shortlist that is not approved is: never drawn, printed or published,
    /// and its problems name it by its rule ("the author's pick"), never by name (Docs/data/lives.md). Pure: parses once
    /// (any thread) and is read-only after.
    /// </summary>
    public sealed class RealLives
    {
        /// <summary>The file (Resources path, without extension).</summary>
        public const string Path = "Data/social/visibility";

        /// <summary>The public-adult rule's age: 18 or older on 1 January of the first year of any data shown (Docs/data/lives.md).</summary>
        public const int AdultAge = 18;

        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        /// <summary>One Forbes estimate as stored: a list's value for a person in a year, with where it is read.</summary>
        public sealed class Estimate
        {
            /// <summary>The list ("billionaires" in spring, "forbes-400" in fall) and its name.</summary>
            public string List, ListName;

            /// <summary>The list's year and month.</summary>
            public int Year, Month;

            /// <summary>The list's date (the most common entry date, computed by the builder) and the entry's own.</summary>
            public string ListDate, EntryDate;

            /// <summary>Net worth, billions of US dollars (shown as "Forbes estimate").</summary>
            public double Value;

            /// <summary>The list data's URL and the entry's text, quoted verbatim.</summary>
            public string Url, Quote;

            /// <summary>Forbes's current estimate (false: an earlier one kept as a revision; the file keeps none today).</summary>
            public bool Current;
        }

        /// <summary>A printed rule, its pick (null: it picks nobody, or its pick is left out) and, for the author's pick, why.</summary>
        public sealed class Rule
        {
            /// <summary>together, apart, littleAttention or authorPick (the shortlist's own names).</summary>
            public string Name;

            /// <summary>The rule as the file prints it (the author's pick: the reason it was chosen).</summary>
            public string Text;

            /// <summary>Forbes's id of the pick drawn, or null: the rule picks nobody, or its pick is left out
            /// (<see cref="Withheld"/>).</summary>
            public string Pick;

            /// <summary>The rule has a pick that is not drawn: the shortlist is not approved, or the pick failed a check (see
            /// <see cref="RealLives.Problems"/>). Its id is not kept, so nothing downstream can name it.</summary>
            public bool Withheld;
        }

        /// <summary>One approved pick: their series as stored and the comparison recomputed from them.</summary>
        public sealed class Figure
        {
            /// <summary>The rule that chose them and its printed wording (<see cref="Rule"/>).</summary>
            public string Rule, RuleText;

            /// <summary>Forbes's id ("elon-musk"), the name, the people.json handle, the Wikidata item and the English
            /// Wikipedia article whose views are counted.</summary>
            public string Id, Name, Handle, Wikidata, Article;

            /// <summary>Birth year.</summary>
            public int Born;

            /// <summary>Every Forbes estimate stored for them, both lists, in the file's order (the gold line).</summary>
            public readonly List<Estimate> Forbes = new List<Estimate>();

            /// <summary>Monthly page views from <see cref="ViewsFromYear"/>/<see cref="ViewsFromMonth"/> (NaN: a month the
            /// source has no row for; never filled). The white line.</summary>
            public double[] MonthlyViews = Array.Empty<double>();

            /// <summary>The first month of <see cref="MonthlyViews"/> (its year, and 1-12).</summary>
            public int ViewsFromYear, ViewsFromMonth;

            /// <summary>The follower snapshot (followers summed across four platforms; a different measure, drawn once as a
            /// star and never joined to the page-view line), its year and its label.</summary>
            public double Followers = double.NaN;

            public int FollowersYear;
            public string FollowersLabel;

            /// <summary>For each compared year (<see cref="RealLives.From"/> .. <see cref="RealLives.To"/>): the rules' list's
            /// estimate ($B) and the calendar year's page views (NaN where either is missing), each over its first year's.</summary>
            public double[] NetWorth, PageViews, NetWorthIndex, PageViewsIndex;

            /// <summary>Each compared year's directions, net worth's then page views' ('+' rose, '-' fell, '=' unchanged; empty
            /// for the first year and where a value is missing), as the file's annual.moves writes them.</summary>
            public string[] Moves;

            /// <summary>Years both moved the same way, years compared, and years in which either did not move (in M, never in K).</summary>
            public int K, M, Flat;

            /// <summary>The file's caption: "together in k of m years", or the author's pick's computed pattern.</summary>
            public string Caption;

            /// <summary>The year and month (1-12) of the k-th monthly page-view count.</summary>
            public (int year, int month) Month(int k)
            {
                int m = ViewsFromMonth - 1 + k;
                return (ViewsFromYear + m / 12, m % 12 + 1);
            }
        }

        readonly List<string> problems = new List<string>();

        /// <summary>The picks, in the shortlist's order (together, apart, then the author's pick).</summary>
        public readonly List<Figure> Picks = new List<Figure>();

        /// <summary>Every rule of the shortlist, a pick or not, with its printed wording.</summary>
        public readonly List<Rule> Rules = new List<Rule>();

        /// <summary>The compared years (window.annual: 2016-2025, so "k of 9 years").</summary>
        public int From { get; private set; }

        public int To { get; private set; }

        /// <summary>The list whose estimate of each year the comparison reads ("billionaires") and its name.</summary>
        public string NetWorthList { get; private set; }

        public string NetWorthListName { get; private set; }

        /// <summary>The author approved the shortlist (shortlist.authorApproved); nobody is drawn otherwise.</summary>
        public bool Approved { get; private set; }

        /// <summary>The file's build date (the latest retrieval of its downloads).</summary>
        public string Built { get; private set; }

        /// <summary>The pick of a rule (null when the rule picks nobody or is not in the file).</summary>
        public Figure Of(string rule) => Picks.Find(f => f.Rule == rule);

        /// <summary>
        /// Reads the file's approved picks and recomputes their comparison (any thread). <paramref name="people"/> is
        /// people.json, for the public-adult check (null: the check fails and says so). Never throws: what cannot be read
        /// is listed in <see cref="Problems"/>.
        /// </summary>
        public static RealLives Parse(string json, SocialPeople people)
        {
            RealLives r = new RealLives();
            if (string.IsNullOrEmpty(json))
            {
                r.problems.Add("visibility.json was not read, so no real lives are drawn");
                return r;
            }

            try
            {
                // dates stay the file's text (JObject.Parse would turn an ISO timestamp into a DateTime)
                using (JsonTextReader reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None })
                {
                    r.Read(JObject.Load(reader), people);
                }
            }
            catch (Exception e)
            {
                r.problems.Add("visibility.json is unreadable: " + e.GetType().Name + ": " + e.Message);
            }

            return r;
        }

        void Read(JObject root, SocialPeople people)
        {
            Built = (string)root["built"];
            JToken annual = root["window"]?["annual"];
            JToken rules = root["rules"];
            JToken shortlist = root["shortlist"];
            if (annual?["from"] == null || annual["to"] == null || rules == null || shortlist == null)
            {
                problems.Add("visibility.json lacks window.annual, rules or shortlist");
                return;
            }

            From = (int)annual["from"];
            To = (int)annual["to"];
            if (To <= From)
            {
                problems.Add($"visibility.json compares no years ({From}-{To})");
                return;
            }

            NetWorthList = (string)rules["netWorthList"];
            Approved = (bool?)shortlist["authorApproved"] == true;
            if (!Approved) problems.Add("the shortlist is not approved by the author (shortlist.authorApproved), so no real lives are drawn");

            Dictionary<string, JToken> byId = new Dictionary<string, JToken>(StringComparer.Ordinal);
            foreach (JToken p in root["people"] as JArray ?? new JArray())
            {
                string id = (string)p["id"];
                if (id != null) byId[id] = p;
            }

            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (JToken pick in shortlist["picks"] as JArray ?? new JArray())
            {
                string rule = (string)pick["rule"], id = (string)pick["id"];
                // the rules' own wording; the author's pick has none, only the reason it was chosen
                string text = (string)rules[rule ?? ""]?["rule"] ?? (string)pick["basis"];
                Rule entry = new Rule { Name = rule, Text = text, Withheld = id != null };
                Rules.Add(entry);
                if (id == null || !Approved) continue;
                // until a pick passes every check below, the problems name it by its rule only: the log never names
                // someone who is left out
                string which = PickName(rule);
                if (!seen.Add(id))
                {
                    problems.Add($"{which} is also an earlier rule's pick, so it is not drawn for this rule");
                    continue;
                }

                if (!byId.TryGetValue(id, out JToken person))
                {
                    problems.Add($"{which} has no entry in people, so it is not drawn");
                    continue;
                }

                Figure f = ReadFigure(rule, text, pick, person);
                if (!PublicAdult(f, which, person, people)) continue;
                Compute(f);
                CheckAgainstFile(f, pick, person);
                entry.Pick = id;
                entry.Withheld = false;
                Picks.Add(f);
            }

            if (Approved && Picks.Count == 0) problems.Add("the approved shortlist draws nobody");
        }

        Figure ReadFigure(string rule, string text, JToken pick, JToken person)
        {
            Figure f = new Figure
            {
                Rule = rule, RuleText = text, Id = (string)person["id"], Name = (string)person["name"],
                Handle = (string)person["handle"], Wikidata = (string)person["wikidata"],
                Article = (string)person["wikipedia"]?["value"], Born = (int?)person["born"] ?? 0, Caption = (string)pick["caption"]
            };
            foreach (JToken e in person["forbes"] as JArray ?? new JArray())
            {
                Estimate x = new Estimate
                {
                    List = (string)e["list"], ListName = (string)e["listName"], Year = (int?)e["year"] ?? 0, Month = (int?)e["month"] ?? 0,
                    ListDate = (string)e["listDate"], EntryDate = (string)e["entryDate"], Value = (double?)e["value"] ?? double.NaN,
                    Url = (string)e["url"], Quote = (string)e["quote"], Current = (bool?)e["current"] ?? true
                };
                f.Forbes.Add(x);
                if (x.List == NetWorthList && NetWorthListName == null) NetWorthListName = x.ListName;
            }

            // without a start month ViewsFromYear stays 0, which Compute reports once the pick has passed the public-adult
            // check (a problem that names the pick waits until then)
            JToken views = person["pageViews"];
            string start = (string)views?["start"];
            if (start != null && DateTime.TryParseExact(start, "yyyy-MM", Ci, DateTimeStyles.None, out DateTime first))
            {
                f.ViewsFromYear = first.Year;
                f.ViewsFromMonth = first.Month;
            }

            JArray counts = views?["value"] as JArray ?? new JArray();
            f.MonthlyViews = new double[counts.Count];
            for (int k = 0; k < counts.Count; k++) f.MonthlyViews[k] = counts[k].Type == JTokenType.Null ? double.NaN : (double)counts[k];

            JToken followers = person["followers"];
            f.Followers = (double?)followers?["value"] ?? double.NaN;
            f.FollowersYear = (int?)followers?["year"] ?? 0;
            f.FollowersLabel = (string)followers?["label"];
            return f;
        }

        /// <summary>
        /// The comparison, as Tools/lives/build.py defines it: each compared year's estimate on the rules' list (Forbes's
        /// current value) and the calendar year's page views (twelve months, none missing); each over the first year's; and
        /// the year-on-year signs of their logs.
        /// </summary>
        void Compute(Figure f)
        {
            int n = Math.Max(0, To - From + 1);
            if (f.ViewsFromYear == 0) problems.Add($"{f.Name}: page views have no start month");
            f.NetWorth = Filled(n);
            f.PageViews = Filled(n);
            f.NetWorthIndex = Filled(n);
            f.PageViewsIndex = Filled(n);
            f.Moves = new string[n];
            foreach (Estimate e in f.Forbes)
            {
                if (!e.Current || e.List != NetWorthList || e.Year < From || e.Year > To) continue;
                if (!(e.Value > 0)) problems.Add($"{f.Name}: the {e.Year} estimate is not a positive number");
                else if (!double.IsNaN(f.NetWorth[e.Year - From])) problems.Add($"{f.Name}: two current {e.ListName} estimates for {e.Year}");
                else f.NetWorth[e.Year - From] = e.Value;
            }

            for (int k = 0; k < n; k++) f.PageViews[k] = CalendarYear(f, From + k);
            for (int k = 0; k < n; k++)
            {
                f.NetWorthIndex[k] = f.NetWorth[k] / f.NetWorth[0];
                f.PageViewsIndex[k] = f.PageViews[k] / f.PageViews[0];
                f.Moves[k] = "";
                if (k == 0 || double.IsNaN(f.NetWorth[k] + f.NetWorth[k - 1] + f.PageViews[k] + f.PageViews[k - 1])) continue;
                int a = Sign(Math.Log(f.NetWorth[k]) - Math.Log(f.NetWorth[k - 1]));
                int b = Sign(Math.Log(f.PageViews[k]) - Math.Log(f.PageViews[k - 1]));
                f.Moves[k] = Arrow(a) + Arrow(b);
                f.M++;
                if (a == 0 || b == 0) f.Flat++;
                else if (a == b) f.K++;
            }
        }

        /// <summary>A calendar year's page views: the sum of its twelve months, NaN if any is missing or outside the series.</summary>
        static double CalendarYear(Figure f, int year)
        {
            int first = (year - f.ViewsFromYear) * 12 + 1 - f.ViewsFromMonth;
            if (f.ViewsFromYear == 0 || first < 0 || first + 12 > f.MonthlyViews.Length) return double.NaN;
            double sum = 0;
            for (int m = 0; m < 12; m++) sum += f.MonthlyViews[first + m];
            return sum;
        }

        /// <summary>The file's own numbers for this pick agree with the recomputed ones: the annual series, the indices (to
        /// the file's 4 decimals), the moves, k, m and the flat years, and the numbers its caption states.</summary>
        void CheckAgainstFile(Figure f, JToken pick, JToken person)
        {
            JToken annual = person["annual"];
            JArray years = annual?["years"] as JArray;
            bool sameYears = years != null && years.Count == f.NetWorth.Length;
            for (int k = 0; sameYears && k < years.Count; k++) sameYears = (int)years[k] == From + k;
            if (!sameYears) problems.Add($"{f.Name}: annual.years is not {From}-{To}");
            else
            {
                Compare(f, "net worth", annual["netWorth"] as JArray, f.NetWorth, 1e-9);
                Compare(f, "page views", annual["pageViews"] as JArray, f.PageViews, 0.5);
                Compare(f, "net worth index", annual["netWorthIndex"] as JArray, f.NetWorthIndex, 5e-5 + 1e-9);
                Compare(f, "page views index", annual["pageViewsIndex"] as JArray, f.PageViewsIndex, 5e-5 + 1e-9);
                for (int k = 1; k < f.Moves.Length; k++)
                {
                    string stored = (string)annual["moves"]?[(From + k).ToString(Ci)] ?? "";
                    if (stored != f.Moves[k]) problems.Add($"{f.Name}: {From + k} moved '{f.Moves[k]}', the file says '{stored}'");
                }
            }

            int?[] stated = { (int?)person["together"]?["k"], (int?)person["together"]?["m"], (int?)person["together"]?["flat"] };
            if (stated[0] != f.K || stated[1] != f.M || stated[2] != f.Flat)
            {
                problems.Add($"{f.Name}: together in {f.K} of {f.M} years ({f.Flat} flat) recomputed, the file says " +
                             $"{stated[0]} of {stated[1]} ({stated[2]} flat)");
            }

            if ((int?)pick["k"] != f.K || (int?)pick["m"] != f.M)
            {
                problems.Add($"{f.Name}: the shortlist's {f.Rule} pick says {(int?)pick["k"]} of {(int?)pick["m"]}, recomputed {f.K} of {f.M}");
            }

            string caption = f.Caption ?? "";
            if (!caption.Contains($"together in {f.K} of {f.M} years")) problems.Add($"{f.Name}: the caption '{caption}' does not say together in {f.K} of {f.M} years");
            if (f.Rule != "authorPick") return;

            // the author's pick's computed pattern: its fortune's range over the compared years and, when the caption
            // gives one, where its readership ended against the first year
            double low = double.MaxValue, high = double.MinValue, last = double.NaN;
            for (int k = 0; k < f.NetWorth.Length; k++)
            {
                if (!double.IsNaN(f.NetWorth[k]))
                {
                    low = Math.Min(low, f.NetWorth[k]);
                    high = Math.Max(high, f.NetWorth[k]);
                }

                if (!double.IsNaN(f.PageViewsIndex[k])) last = f.PageViewsIndex[k];
            }

            string range = "$" + low.ToString("0.0", Ci) + "B to $" + high.ToString("0.0", Ci) + "B";
            if (!caption.Contains(range)) problems.Add($"{f.Name}: the caption '{caption}' does not give the fortune's range {range}");
            bool tells = caption.Contains(" fell to ") || caption.Contains(" grew to ");
            if (tells && !caption.Contains(" " + last.ToString("0.00", Ci) + " "))
            {
                problems.Add($"{f.Name}: the caption '{caption}' does not give the readership's last index {last.ToString("0.00", Ci)}");
            }
        }

        void Compare(Figure f, string what, JArray stored, double[] computed, double tolerance)
        {
            if (stored == null || stored.Count != computed.Length)
            {
                problems.Add($"{f.Name}: the file has no annual {what} for {From}-{To}");
                return;
            }

            for (int k = 0; k < computed.Length; k++)
            {
                bool none = stored[k].Type == JTokenType.Null;
                if (none && double.IsNaN(computed[k])) continue;
                if (none || double.IsNaN(computed[k]) || Math.Abs((double)stored[k] - computed[k]) > tolerance)
                {
                    problems.Add($"{f.Name}: {what} {From + k} is {Show(computed[k])} recomputed, {(none ? "missing" : Show((double)stored[k]))} in the file");
                }
            }
        }

        /// <summary>
        /// Public adults only (MODALITIES.md 7.3; Docs/data/lives.md): a person, not a group; a Wikidata item and an English
        /// Wikipedia article; a birth year known and not estimated; 18 or older on 1 January of the first year of any data
        /// shown (the first Forbes estimate or the first page-view month). Checked on the file's record and again on
        /// people.json, the record the builder's rule reads. Returns whether the pick passes: one that fails, or cannot be
        /// checked, is left out (never drawn, printed or published), and its problems name it as <paramref name="which"/>
        /// ("the author's pick") with the reason only, no name, handle, item or birth year, as the rule asks of anyone who
        /// fails it.
        /// </summary>
        bool PublicAdult(Figure f, string which, JToken person, SocialPeople people)
        {
            int listed = problems.Count;
            int firstData = f.ViewsFromYear > 0 ? f.ViewsFromYear : int.MaxValue;
            foreach (Estimate e in f.Forbes) firstData = Math.Min(firstData, e.Year);
            string first = firstData == int.MaxValue ? "none" : firstData.ToString(Ci);
            JToken record = person["publicAdult"];
            if ((string)record?["kind"] != "p" || (bool?)record["bornEstimated"] != false || (bool?)record["wikipedia"] != true ||
                (int?)record["born"] != f.Born || (int?)record["firstDataYear"] != firstData)
            {
                problems.Add($"{which} is not drawn: its public-adult record in the file does not show a person, a known birth year " +
                             $"matching its entry, a Wikipedia article and the first year of data shown ({first})");
            }

            if (string.IsNullOrEmpty(f.Article) || string.IsNullOrEmpty(f.Wikidata)) problems.Add($"{which} is not drawn: no Wikidata item or Wikipedia article");
            if (f.Born <= 0) problems.Add($"{which} is not drawn: no birth year, so the public-adult rule cannot be checked");
            else if (f.Born + AdultAge + 1 > firstData)
            {
                problems.Add($"{which} fails the public-adult rule and is not drawn: under {AdultAge} for some or all of {first}, the first year of data shown");
            }

            if (people?.people == null)
            {
                problems.Add($"{which} is not drawn: people.json was not read, so its public-adult record cannot be checked");
                return false;
            }

            SocialPeople.Person p = Array.Find(people.people, x => x.handle == f.Handle);
            if (p == null) problems.Add($"{which} is not drawn: no account in people.json to check its public-adult record against");
            else if (p.kind != "p" || p.q != f.Wikidata || p.born != f.Born || p.est)
            {
                problems.Add($"{which} fails the public-adult rule in people.json and is not drawn (" +
                             (p.kind != "p" ? "not a person" : "a person") + (p.est ? ", birth year estimated" : "") +
                             (p.born != f.Born ? ", another birth year" : "") + (p.q != f.Wikidata ? ", another Wikidata item" : "") + ")");
            }

            return problems.Count == listed;
        }

        /// <summary>
        /// What is wrong with the file or the picks (empty when every check passes): the shortlist is approved; every pick
        /// has an entry and is a public adult (in the file and in people.json), else it is left out and named by its rule
        /// only; and every pick drawn has annual series, indices, moves, k of m and a caption that agree with the ones
        /// recomputed from its stored estimates and monthly page views.
        /// </summary>
        public List<string> Problems() => new List<string>(problems);

        /// <summary>
        /// The body of the "[Why] Lives real" line: each rule's pick with k of m recomputed and as the file states it, the
        /// rules without a pick, and each pick's last indices. A pick left out is not named.
        /// </summary>
        public string Line()
        {
            StringBuilder b = new StringBuilder();
            b.Append(From.ToString(Ci)).Append('-').Append(To.ToString(Ci)).Append(" (").Append(From.ToString(Ci))
                .Append(" = 1; ").Append(NetWorthListName ?? NetWorthList ?? "no list").Append(", page views by calendar year; shortlist ")
                .Append(Approved ? "approved" : "NOT approved").Append("):");
            foreach (Rule r in Rules)
            {
                Figure f = r.Pick != null ? Picks.Find(x => x.Id == r.Pick) : null;
                b.Append(' ').Append(RuleName(r.Name)).Append(": ");
                if (f == null)
                {
                    b.Append(r.Withheld ? "a pick not drawn (see the checks);" : "no pick;");
                    continue;
                }

                b.Append(f.Name).Append(", together in ").Append(f.K).Append(" of ").Append(f.M).Append(" years");
                if (f.Flat > 0) b.Append(" (").Append(f.Flat).Append(" flat)");
                int last = f.NetWorthIndex.Length - 1;
                b.Append(", ").Append(To.ToString(Ci)).Append(" fortune x").Append(Show(f.NetWorthIndex[last], "0.00"))
                    .Append(" readership x").Append(Show(f.PageViewsIndex[last], "0.00")).Append(';');
            }

            b.Append(" checks ").Append(problems.Count == 0 ? "PASS" : problems.Count + " FAILED");
            return b.ToString();
        }

        /// <summary>
        /// Publishes each pick under lives.real.&lt;rule&gt;.*: name, id, k, m, flat, caption and the last year's two indices
        /// (<see cref="ModelFacts"/>; the tour's lives_visible card), and the compared years.
        /// </summary>
        public void PublishFacts()
        {
            if (Picks.Count == 0) return;
            string years = From.ToString(Ci) + "-" + To.ToString(Ci);
            ModelFacts.Set("lives.real.from", From, "year", "visibility.json window.annual.from", "measured: the first compared year, the index base (= 1)");
            ModelFacts.Set("lives.real.to", To, "year", "visibility.json window.annual.to", "measured: the last compared year");
            foreach (Figure f in Picks)
            {
                string key = "lives.real." + f.Rule;
                string source = $"visibility.json people[{f.Id}]: {NetWorthListName ?? NetWorthList} estimates and English Wikipedia " +
                                $"page views by calendar year, {years} (Tools/lives/build.py), recomputed by RealLives";
                // the author's pick is a candidate no printed rule picked (decision 34), never shown as a rule's pick
                string chosen = f.Rule == "authorPick"
                    ? "the author's pick, a candidate the author chose and approved (shortlist.authorApproved; MODALITIES-PLAN " +
                      "decision 34), not chosen by a printed rule"
                    : $"chosen by the printed {RuleName(f.Rule)} rule and approved by the author (shortlist.authorApproved)";
                string basis = "measured: published estimates and page-view counts; " + chosen;
                int last = f.NetWorthIndex.Length - 1;
                ModelFacts.Set(key + ".name", f.Name, "", source, basis);
                ModelFacts.Set(key + ".id", f.Id, "", source, basis);
                ModelFacts.Set(key + ".k", f.K, "years", source, basis + "; years in which fortune and readership rose together or fell together");
                ModelFacts.Set(key + ".m", f.M, "years", source, basis + "; year-on-year moves compared");
                ModelFacts.Set(key + ".flat", f.Flat, "years", source, basis + "; years in which either did not move (counted in m, not in k)");
                ModelFacts.Set(key + ".caption", f.Caption ?? "", "", source, basis + "; the file's caption, its numbers checked against the recomputed series");
                ModelFacts.Set(key + ".netWorthIndex", f.NetWorthIndex[last], "x " + From.ToString(Ci), source, basis + $"; {To} fortune over {From}'s");
                ModelFacts.Set(key + ".pageViewsIndex", f.PageViewsIndex[last], "x " + From.ToString(Ci), source, basis + $"; {To} page views over {From}'s");
            }
        }

        /// <summary>A rule's name in words ("the author's pick").</summary>
        public static string RuleName(string rule) =>
            rule == "littleAttention" ? "little attention" : rule == "authorPick" ? "the author's pick" : rule ?? "?";

        /// <summary>A pick named by its rule ("the together pick", "the author's pick"): how the problems name a pick until
        /// it has passed every check.</summary>
        static string PickName(string rule) => rule == "authorPick" ? "the author's pick" : "the " + RuleName(rule) + " pick";

        static double[] Filled(int n)
        {
            double[] a = new double[n];
            for (int k = 0; k < n; k++) a[k] = double.NaN;
            return a;
        }

        static int Sign(double v) => v > 0 ? 1 : v < 0 ? -1 : 0;

        static string Arrow(int s) => s > 0 ? "+" : s < 0 ? "-" : "=";

        static string Show(double v, string format = "0.####") => double.IsNaN(v) ? "missing" : v.ToString(format, Ci);
    }
}
