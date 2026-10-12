using System;
using System.Collections.Generic;
using Why.Humans.Smv;

namespace Why.Economy.Model
{
    /// <summary>
    /// One simulated person followed through the economy views (atom, dilemma, ladder): who is followed, their yearly
    /// records up to the shown year and the events of their life read off those records (a first job, a change of
    /// industry, marriage, children, a home, the debt peak, gaining control of their path, retirement). Pure: reads
    /// the finished lives (any thread once they are ready), no Unity objects. These are synthetic records, each standing
    /// for 100,000 people, not identified people.
    /// </summary>
    public sealed class PersonLife
    {
        /// <summary>The roles of <see cref="EconomicLives.Notables"/> tried in order when nobody is selected: the striver
        /// climbs the ladder, the others change rung or industry in other ways.</summary>
        public static readonly string[] Roles = { "striver", "young", "indebted", "owner", "escapist", "forgiver", "heir", "avenger", "retiree" };

        public enum Kind { Start, FirstJob, Industry, JobLost, Married, MarriageEnded, Child, Home, Business, Control, DebtPeak, Inherited, Discharged, Retired }

        /// <summary>A life event: its year, its index in <see cref="Records"/>, what it is and a short label.</summary>
        public struct Event { public int Year, Index; public Kind Kind; public string Text; }

        /// <summary>The person (index into the population), why they are followed ("the selected person", "the striver").</summary>
        public readonly int Person;
        public readonly string Why;
        /// <summary>Calendar year of each record (adult years only, ascending, up to the shown year) and the records.</summary>
        public readonly int[] Years;
        public readonly PersonYear[] Records;
        /// <summary>The spouse at each record's mid-year (-1: none) and the spouse's industry when employed (-1: none).</summary>
        public readonly int[] Spouse, SpouseIndustry;
        public readonly List<Event> Events = new List<Event>();
        /// <summary>The person's traits (strategy, parent's rank), sex and birth year.</summary>
        public readonly PersonTraits Traits;
        public readonly bool Male;
        public readonly double Birth;
        /// <summary>The shown year's record (the last one).</summary>
        public PersonYear Now => Records[Records.Length - 1];
        public int Year => Years[Years.Length - 1];

        PersonLife(int person, string why, List<int> years, List<PersonYear> records, PersonTraits traits, SmvPerson p)
        {
            Person = person; Why = why; Years = years.ToArray(); Records = records.ToArray(); Traits = traits;
            Spouse = new int[Years.Length]; SpouseIndustry = new int[Years.Length];
            Male = p != null && p.Male; Birth = p?.Birth ?? 0;
        }

        /// <summary>
        /// The person to follow in a year: the inspected person (<see cref="EconomyState.Person"/>) when they are an adult
        /// then; else the first notable of <see cref="Roles"/> who is; else the adult whose household income rank rose the
        /// most over the ten years before (ties: the lower index). -1 when the lives were not simulated.
        /// </summary>
        public static int Choose(EconomicLives lives, int year, int selected, out string why)
        {
            why = "";
            if (lives == null || !lives.Ready || lives.Sim == null) return -1;
            if (selected >= 0 && lives.TryGet(selected, year, out PersonYear s) && s.Adult) { why = "the selected person"; return selected; }
            foreach (string role in Roles)
                foreach (Notable n in lives.Notables)
                    if (n.Role == role && lives.TryGet(n.Person, year, out PersonYear r) && r.Adult) { why = "the " + role; return n.Person; }
            int best = -1; float rise = float.MinValue;
            for (int i = 0; i < lives.Sim.People.Count; i++)
            {
                if (!lives.TryGet(i, year, out PersonYear r) || !r.Adult || r.Age > 55) continue;
                if (!lives.TryGet(i, year - 10, out PersonYear b) || !b.Adult) continue;
                float d = r.IncomeRank - b.IncomeRank;
                if (d > rise) { rise = d; best = i; }
            }
            why = best >= 0 ? "the fastest climber of the last ten years" : "";
            return best;
        }

        /// <summary>The followed person's adult life up to <paramref name="year"/> (null when nobody can be followed).</summary>
        public static PersonLife Build(EconomicLives lives, Data.EconomyData data, int year, int selected)
        {
            int person = Choose(lives, year, selected, out string why);
            if (person < 0) return null;
            var years = new List<int>(); var records = new List<PersonYear>();
            for (int y = lives.FirstYear; y <= year; y++)
                if (lives.TryGet(person, y, out PersonYear r) && r.Adult) { years.Add(y); records.Add(r); }
            if (records.Count == 0) return null;
            var life = new PersonLife(person, why, years, records, lives.Traits(person), lives.Sim.People[person]);
            for (int k = 0; k < life.Years.Length; k++)
            {
                life.Spouse[k] = life.SpouseIndustry[k] = -1;
                double mid = life.Years[k] + .5;
                foreach (SmvMarriage m in lives.Sim.MarriageLog)
                {
                    if (m.Start > mid || m.End <= mid) continue;
                    int other = m.Wife == person ? m.Husband : m.Husband == person ? m.Wife : -1;
                    if (other < 0) continue;
                    life.Spouse[k] = other;
                    if (lives.TryGet(other, life.Years[k], out PersonYear o) && o.Employed && o.Industry >= 0) life.SpouseIndustry[k] = o.Industry;
                    break;
                }
            }
            life.ReadEvents(data);
            return life;
        }

        /// <summary>The household's hill in a year: the person's own industry when employed, else the spouse's (the
        /// census's dominance rule: a non-employed partner takes the earner's industry), else -1 (the valley floor).</summary>
        public int IndustryAt(int index)
        {
            var r = Records[index];
            if (r.Employed && r.Industry >= 0) return r.Industry;
            return SpouseIndustry[index];
        }

        void ReadEvents(Data.EconomyData data)
        {
            string Name(int industry) => data != null && industry >= 0 && industry < data.Industries.Count ? data.Industries[industry].Name : "industry " + industry;
            int peak = -1; float peakDebt = 0;
            bool job = false, home = false, business = false, control = false, retired = false;
            int kids = 0;
            for (int k = 0; k < Records.Length; k++)
            {
                var r = Records[k]; var b = k > 0 ? Records[k - 1] : r; int y = Years[k];
                if (k == 0) Add(k, Kind.Start, "age " + (int)r.Age + " · rank " + Pct(r.IncomeRank) + (r.Employed && r.Industry >= 0 ? " · " + Name(r.Industry) : ""));
                if (r.Employed && r.Industry >= 0)
                {
                    if (!job && k > 0) Add(k, Kind.FirstJob, "first job · " + Name(r.Industry));
                    else if (job && b.Employed && b.Industry >= 0 && b.Industry != r.Industry) Add(k, Kind.Industry, "→ " + Name(r.Industry));
                    job = true;
                }
                else if (k > 0 && b.Employed && r.Age < 60 && r.SocialSecurity <= 0) Add(k, Kind.JobLost, "out of work");
                if (k > 0 && r.Married && !b.Married) Add(k, Kind.Married, "marries");
                if (k > 0 && !r.Married && b.Married) Add(k, Kind.MarriageEnded, "marriage ends");
                if (r.Kids > kids) { Add(k, Kind.Child, r.Kids == 1 ? "first child" : r.Kids + " children"); }
                kids = r.Kids;
                if (r.Homeowner && !home) { if (k > 0) Add(k, Kind.Home, "buys a home"); home = true; }
                if (r.SelfEmployed && !business) { Add(k, Kind.Business, "own business"); business = true; }
                if (r.InControl && !control) { Add(k, Kind.Control, "IN CONTROL of their path"); control = true; }
                if (r.Debt > peakDebt) { peakDebt = r.Debt; peak = k; }
                if (r.Inherited > 1000) Add(k, Kind.Inherited, "inherits " + Money(r.Inherited));
                if (r.Discharged) Add(k, Kind.Discharged, "debt discharged");
                if (r.SocialSecurity > 0 && !retired) { Add(k, Kind.Retired, "retires · Social Security"); retired = true; }
            }
            if (peak >= 0 && peakDebt > 1000) Add(peak, Kind.DebtPeak, "debt peak " + Money(peakDebt));
            Events.Sort((a, b) => a.Index != b.Index ? a.Index.CompareTo(b.Index) : a.Kind.CompareTo(b.Kind));
            // Few enough to label: children after the first, repeated industry moves and inheritances thin out first.
            while (Events.Count > 16)
            {
                int drop = Events.FindLastIndex(e => e.Kind == Kind.Child && e.Text != "first child");
                if (drop < 0) drop = Events.FindLastIndex(e => e.Kind == Kind.Inherited || e.Kind == Kind.MarriageEnded || e.Kind == Kind.JobLost);
                if (drop < 0) drop = Events.FindLastIndex(e => e.Kind == Kind.Industry);
                if (drop < 0) break;
                Events.RemoveAt(drop);
            }

            void Add(int index, Kind kind, string text) => Events.Add(new Event { Year = Years[index], Index = index, Kind = kind, Text = text });
        }

        public static string Pct(double share) => (100 * share).ToString("0", Land.LandFacts.Ci) + "%";

        /// <summary>Dollars in words-sized units: $850, $85K, $1.2M.</summary>
        public static string Money(double v) => LivesReport.Money(v);
    }
}
