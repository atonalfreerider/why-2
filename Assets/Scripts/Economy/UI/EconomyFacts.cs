using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Why.Economy.Data;
using Why.Economy.Model;
using Why.Humans;
using Why.Humans.Smv;

namespace Why.Economy.UI
{
    /// <summary>
    /// Everything the person inspector shows about one simulated person in one year, read from the economic lives and
    /// worded for a general audience. Pure (no Unity objects), built once per change of person or year, so the panel only
    /// lays it out and the harness can print it.
    /// </summary>
    public sealed class PersonFacts
    {
        /// <summary>Spending categories with bars: the first six of <see cref="EconomyData.CategoryIds"/> (saving is a
        /// money row).</summary>
        public const int Categories = 6;

        /// <summary>Drives listed per pole (the strongest first).</summary>
        public const int TopDrives = 3;

        /// <summary>
        /// Money rows of an adult year, in display order: read down the columns, income and its taxes come first, then
        /// what is left and where it goes, then the balance sheet and where it ranks (two columns of six in the landscape
        /// panel, three of four in the portrait sheet).
        /// </summary>
        public const int MoneyRows = 12;

        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        static readonly string[] TribeNames = { "Democrat", "Republican", "Independent" };

        /// <summary>Generations when the human world (demography.json) has none; as the population layer draws them.</summary>
        static readonly (string name, int from, int to)[] FallbackGenerations =
        {
            ("Lost Generation", 1883, 1900), ("Greatest Generation", 1901, 1927), ("Silent Generation", 1928, 1945),
            ("Baby Boomers", 1946, 1964), ("Generation X", 1965, 1980), ("Millennials", 1981, 1996),
            ("Generation Z", 1997, 2012), ("Generation Alpha", 2013, 2030)
        };

        public int Person = -1;

        /// <summary>The year asked for (<see cref="EconomyState.Year"/>) and the year shown (the nearest year of the
        /// person's life in the record).</summary>
        public int RequestedYear, Year;

        /// <summary>The shown year is a childhood year (the money is the household's; no mind of their own yet).</summary>
        public bool Child;

        /// <summary>"Woman, born 1961"; "Generation X  ·  Democrat  ·  Tit for tat, cooperates 84%".</summary>
        public string Title = "", Meta = "";

        /// <summary>Role of a notable person (owner, heir, striver ...) and the model's sentence on why; null otherwise.</summary>
        public string Role, RoleWhy;

        /// <summary>The year the role's sentence describes (the last simulated year: notables are picked there).</summary>
        public int RoleYear;

        /// <summary>"IN 2025  ·  AGE 64" (with why another year is shown) and the year's household and work.</summary>
        public string YearHeading = "", Status = "";

        /// <summary>Money of the year: labels, shorter labels for the portrait sheet's narrow columns, and values (adults
        /// only), and how to read the dollars.</summary>
        public readonly string[] MoneyLabels = new string[MoneyRows], MoneyShort = new string[MoneyRows],
            MoneyValues = new string[MoneyRows];

        public string MoneyNote = "";

        /// <summary>Spending: names, shares of spending, each category's share of fear in its motive, and the part of
        /// each share that buys a fantasy; the person's fantasy share, and total spending (empty for a child: the shares
        /// are the household's).</summary>
        public readonly string[] CategoryNames = new string[Categories];

        public readonly float[] CategoryShares = new float[Categories], CategoryFear = new float[Categories],
            CategoryFantasy = new float[Categories];

        public float Fantasy;
        public string SpendingTotal = "";

        /// <summary>"SPENDING $122K  ·  FANTASY 22%" (for a child: the household's shares, no dollars).</summary>
        public string SpendingHeading = "";

        /// <summary>The mind: share of fear in the spending's motive, reason (the higher OS), future orientation, agency,
        /// material autonomy; the population's means of reason and future that year; the cut of agency for control.</summary>
        public float FearShare, Reason, Future, Agency, Autonomy, MeanReason, MeanFuture, ControlCut;

        public bool InControl;

        /// <summary>One sentence on control; the strongest desires and fears ("Shelter 31%  ·  Food 24%  ·  ...").</summary>
        public string Verdict = "", Wants = "", Fears = "";

        /// <summary>The life in the record: first and last year, per year agency, in control, net worth ($).</summary>
        public int LifeFirstYear, LifeLastYear;

        public float[] LifeAgency = Array.Empty<float>(), LifeWealth = Array.Empty<float>();
        public bool[] LifeControl = Array.Empty<bool>();

        /// <summary>"peak 0.71" and "peak $1.2M" (or the lowest, when the line never rises above zero).</summary>
        public string AgencyPeak = "", WealthPeak = "";

        /// <summary>
        /// The facts of a person in a year, or null when the lives were not simulated or the person has no year in the
        /// record. <paramref name="world"/> (optional) names the generations.
        /// </summary>
        public static PersonFacts Build(EconomyModel model, SmvPopulation pop, HumanWorld world, int person, int year)
        {
            EconomicLives lives = model?.Lives;
            if (lives == null || !lives.Ready || pop?.Sim == null || person < 0 || person >= pop.Sim.People.Count) return null;
            SmvPerson p = pop.Sim.People[person];
            EconomyData data = model.Data;

            // the life in the record (the lives are stored for every year the person is alive at mid-year)
            int first = -1, last = -1;
            for (int y = lives.FirstYear; y <= lives.LastYear; y++)
            {
                if (!lives.TryGet(person, y, out _)) continue;
                if (first < 0) first = y;
                last = y;
            }

            if (first < 0) return null;
            int shown = Math.Max(first, Math.Min(last, year));
            lives.TryGet(person, shown, out PersonYear s);
            lives.TryGet(person, last, out PersonYear final);

            PersonFacts f = new PersonFacts { Person = person, RequestedYear = year, Year = shown, Child = !s.Adult };
            f.Title = (final.Adult ? p.Male ? "Man" : "Woman" : p.Male ? "Boy" : "Girl") + ", born " + YearOf(p.Birth);
            f.Meta = MetaLine(lives, world, p, s);
            f.Role = lives.RoleOf(person);
            f.RoleYear = lives.LastYear;
            foreach (Notable n in lives.Notables)
            {
                if (n.Person == person) f.RoleWhy = n.Why;
            }

            f.YearHeading = Heading(p, s, shown, year, first, last, lives.LastYear);
            f.Status = StatusLine(data, s);
            f.FillMoney(data, s, shown);
            f.FillSpending(data, s);
            f.FillMind(lives, s, shown);
            f.FillDrives(lives, data, person, shown);
            f.FillLife(lives, person, first, last);
            return f;
        }

        // ------------------------------------------------------------------ who

        static string MetaLine(EconomicLives lives, HumanWorld world, SmvPerson p, PersonYear s)
        {
            StringBuilder sb = new StringBuilder(GenerationOf(world, p.Birth));
            if (p.Immigrant) sb.Append("  \u00B7  arrived ").Append(YearOf(p.Enter));
            PersonTraits t = lives.Traits(p.Index);
            if (s.Adult)
            {
                sb.Append("  \u00B7  ").Append(TribeNames[Math.Min(s.Tribe, (byte)2)]);
                if (t != null)
                {
                    sb.Append("  \u00B7  ").Append(PrisonersDilemma.Name(t.Strategy)).Append(", cooperates ")
                        .Append(Pct(s.Cooperation));
                }
            }
            else if (t != null)
            {
                sb.Append("  \u00B7  will play ").Append(PrisonersDilemma.Name(t.Strategy).ToLowerInvariant());
            }

            return sb.ToString();
        }

        /// <summary>The generation a birth year belongs to (demography.json's, else the population layer's defaults).</summary>
        public static string GenerationOf(HumanWorld world, double birth)
        {
            if (world != null && world.UsGenerations.Count > 0)
            {
                foreach (Generation g in world.UsGenerations)
                {
                    if (g != null && birth >= g.from && birth < g.to + 1) return g.name ?? g.id;
                }
            }

            foreach ((string name, int from, int to) in FallbackGenerations)
            {
                if (birth >= from && birth < to + 1) return name;
            }

            return birth < 1901 ? "Born before 1901" : "Born after 2030";
        }

        static string Heading(SmvPerson p, PersonYear s, int shown, int requested, int first, int last, int recordEnd)
        {
            string when = "IN " + shown.ToString(Ci);
            if (requested > last)
            {
                // the record holds every year a person is alive at its middle: one who dies early in a year ends before it
                when += last >= recordEnd || double.IsInfinity(p.Death)
                    ? " (THE LAST YEAR OF THE RECORD)"
                    : " (DIED " + YearOf(p.Death) + ")";
            }
            else if (requested < first)
            {
                when += " (NOT YET HERE IN " + requested.ToString(Ci) + ")";
            }

            return when + "  \u00B7  AGE " + Math.Max(0, Math.Floor(s.Age)).ToString("0", Ci);
        }

        static string StatusLine(EconomyData data, PersonYear s)
        {
            List<string> parts = new List<string>();
            if (!s.Adult)
            {
                parts.Add("a child: the household's spending and mind");
                if (s.Homeowner) parts.Add("a home the family owns");
                return string.Join("  \u00B7  ", parts);
            }

            parts.Add(s.Married ? "married" : "single");
            if (s.Kids > 0) parts.Add(s.Kids == 1 ? "1 child at home" : s.Kids.ToString(Ci) + " children at home");
            parts.Add(s.Homeowner ? "homeowner" : "renter");
            string industry = s.Industry >= 0 && s.Industry < data.Industries.Count ? data.Industries[s.Industry].Name : null;
            if (s.SelfEmployed) parts.Add(industry != null ? "own business in " + industry : "own business");
            else if (s.Employed) parts.Add(industry != null ? "works in " + industry : "employed");
            else if (s.SocialSecurity > 0) parts.Add("retired");
            else parts.Add("not working");
            return string.Join("  \u00B7  ", parts);
        }

        // ------------------------------------------------------------------ money

        void FillMoney(EconomyData data, PersonYear s, int year)
        {
            float rate = s.Disposable > 0 ? s.Saving / s.Disposable : 0;
            Set(0, "Wages", "Wages", Money(s.Wages));
            Set(1, "Business", "Business", Money(s.Business));
            Set(2, "Capital income", "Capital", Money(s.CapitalIncome));
            Set(3, "Transfers", "Transfers", Money(s.Transfers));
            Set(4, "Taxes", "Taxes", Money(s.Taxes));
            Set(5, "Disposable", "Disposable", Money(s.Disposable));
            Set(6, "Spending", "Spending", Money(s.Spending));
            Set(7, "Saving", "Saving", Money(s.Saving) + (s.Disposable > 0 ? " (" + Pct(rate) + ")" : ""));
            Set(8, "Net worth", "Net worth", Money(s.Wealth));
            Set(9, "Debt", "Debt", Money(s.Debt));
            Set(10, "Wealth group", "Wealth", GroupName(data, s.WealthGroup));
            Set(11, "Income rank", "Income", s.IncomeRank >= 0.5f
                ? "top " + Pct(Math.Max(0.01f, 1 - s.IncomeRank))
                : "bottom " + Pct(Math.Max(0.01f, s.IncomeRank)));

            // dollars of the year; how many 2025 dollars one of them buys (the GDP deflator)
            double toReal = data.Real(1, year);
            string dollars = year >= 2025 || Math.Abs(toReal - 1) < 0.05
                ? "Dollars of " + year.ToString(Ci)
                : "Dollars of " + year.ToString(Ci) + " (one is " + toReal.ToString(toReal < 10 ? "0.0" : "0", Ci) +
                  " of 2025)";
            MoneyNote = dollars + "; a couple's money is split evenly between them.";
        }

        void Set(int row, string label, string shortLabel, string value)
        {
            MoneyLabels[row] = label;
            MoneyShort[row] = shortLabel;
            MoneyValues[row] = value;
        }

        static string GroupName(EconomyData data, byte group)
        {
            int g = Math.Min((int)group, EconomyData.GroupIds.Length - 1);
            foreach (Group x in data.Groups)
            {
                if (x != null && x.Id == EconomyData.GroupIds[g] && !string.IsNullOrEmpty(x.Name)) return x.Name;
            }

            return EconomyData.GroupIds[g];
        }

        // ------------------------------------------------------------------ spending and mind

        void FillSpending(EconomyData data, PersonYear s)
        {
            for (int c = 0; c < Categories; c++)
            {
                Category cat = data.CategoryById(EconomyData.CategoryIds[c]);
                CategoryNames[c] = cat?.Name ?? EconomyData.CategoryIds[c];
                CategoryShares[c] = Clamp01(s.Category(c));
                CategoryFear[c] = Clamp01((float)(cat?.FearShare ?? 0.5));
                CategoryFantasy[c] = CategoryShares[c] * Clamp01((float)(cat?.Fantasy ?? 0));
            }

            Fantasy = Clamp01(s.Fantasy);
            SpendingTotal = s.Adult ? Money(s.Spending) : "";
            SpendingHeading = (s.Adult ? "SPENDING  " + SpendingTotal : "THE HOUSEHOLD'S SPENDING") + "  \u00B7  FANTASY " +
                              Pct(Fantasy);
        }

        void FillMind(EconomicLives lives, PersonYear s, int year)
        {
            FearShare = Clamp01(s.FearShare);
            Reason = Clamp01(s.Reason);
            Future = Clamp01(s.Future);
            Agency = Clamp01(s.Agency);
            Autonomy = Clamp01(s.Autonomy);
            InControl = s.InControl;
            ControlCut = lives.ControlThreshold;
            PopulationYear a = lives.Aggregate(year);
            MeanReason = a != null ? a.MeanReason : -1;
            MeanFuture = a != null ? a.MeanFuture : -1;

            string cut = ControlCut.ToString("0.00", Ci), agency = Agency.ToString("0.00", Ci);
            if (!s.Adult) Verdict = "";
            else if (InControl) Verdict = "In control of their path: agency " + agency + " clears the cut of " + cut + ", with material autonomy.";
            else if (s.Agency > ControlCut)
            {
                Verdict = "Not in control: agency " + agency + " clears the cut of " + cut + ", but autonomy is " +
                          Autonomy.ToString("0.00", Ci) + " (a business, or years of savings, make it 1).";
            }
            else Verdict = "Not in control: agency " + agency + ", under the cut of " + cut + ".";
        }

        void FillDrives(EconomicLives lives, EconomyData data, int person, int year)
        {
            float[] d = new float[5], f = new float[5];
            if (!lives.DriveWeights(person, year, d, f))
            {
                Wants = Fears = "";
                return;
            }

            Wants = TopOf(d, LivesInputs.DesireIds, data.Psyche?.Desires);
            Fears = TopOf(f, LivesInputs.FearIds, data.Psyche?.Fears);
        }

        /// <summary>The strongest drives of a pole: "Shelter 31%  ·  Food 24%  ·  Sex 18%".</summary>
        static string TopOf(float[] weights, string[] ids, List<Drive> names)
        {
            int n = Math.Min(weights.Length, ids.Length);
            int[] order = new int[n];
            for (int i = 0; i < n; i++) order[i] = i;
            Array.Sort(order, (a, b) => weights[b].CompareTo(weights[a]) != 0 ? weights[b].CompareTo(weights[a]) : a.CompareTo(b));
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < Math.Min(TopDrives, n); i++)
            {
                if (i > 0) sb.Append("  \u00B7  ");
                sb.Append(DriveName(ids[order[i]], names)).Append(' ').Append(Pct(weights[order[i]]));
            }

            return sb.ToString();
        }

        static string DriveName(string id, List<Drive> names)
        {
            if (names != null)
            {
                foreach (Drive d in names)
                {
                    if (d != null && d.Id == id && !string.IsNullOrEmpty(d.Name)) return d.Name;
                }
            }

            return id.Length > 0 ? char.ToUpperInvariant(id[0]) + id.Substring(1) : id;
        }

        void FillLife(EconomicLives lives, int person, int first, int last)
        {
            int n = last - first + 1;
            LifeFirstYear = first;
            LifeLastYear = last;
            LifeAgency = new float[n];
            LifeWealth = new float[n];
            LifeControl = new bool[n];
            float peakAgency = 0, peakWealth = float.NegativeInfinity;
            for (int k = 0; k < n; k++)
            {
                if (!lives.TryGet(person, first + k, out PersonYear r)) continue;
                LifeAgency[k] = r.Adult ? Clamp01(r.Agency) : 0;
                LifeControl[k] = r.InControl;
                LifeWealth[k] = r.Wealth;
                peakAgency = Math.Max(peakAgency, LifeAgency[k]);
                peakWealth = Math.Max(peakWealth, r.Wealth);
            }

            AgencyPeak = "peak " + peakAgency.ToString("0.00", Ci);
            WealthPeak = (peakWealth > 0 ? "peak " : "highest ") + Money(peakWealth);
        }

        // ------------------------------------------------------------------ formatting

        /// <summary>Dollars in words-sized units, as the model's report writes them ($850, $85K, $1.2M).</summary>
        public static string Money(double v) => double.IsNaN(v) || double.IsInfinity(v) ? "-" : LivesReport.Money(v);

        /// <summary>A share as a whole percent ("54%").</summary>
        public static string Pct(double v)
        {
            double p = Math.Round(100 * (double.IsNaN(v) ? 0 : v));
            return (p == 0 ? 0 : p).ToString("0", Ci) + "%"; // never "-0%"
        }

        static string YearOf(double t) => Math.Floor(t).ToString("0", Ci);

        static float Clamp01(float v) => float.IsNaN(v) ? 0 : v < 0 ? 0 : v > 1 ? 1 : v;
    }

    /// <summary>
    /// The year's readout under the year chip: the share of adults in control of their path, the fantasy and fear shares
    /// of the money they spend, and the share of people who say most people can be trusted (General Social Survey).
    /// </summary>
    public static class YearFacts
    {
        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        /// <summary>
        /// "In control 12%  ·  fantasy 19% of spending  ·  fear 54%  ·  trust 25%" (rich text: labels dim, numbers in
        /// <paramref name="valueHex"/>); compact drops "of spending" for narrow screens. Trust is left out before its
        /// survey begins (1972) and held at its last reading after it; everything is empty without the lives.
        /// </summary>
        public static string Line(EconomicLives lives, YearSeries trust, int year, bool compact, string valueHex)
        {
            PopulationYear a = lives != null && lives.Ready ? lives.Aggregate(year) : null;
            if (a == null) return "";
            string open = "<color=#" + valueHex + ">", close = "</color>";
            const string dot = "  \u00B7  ";
            StringBuilder sb = new StringBuilder();
            sb.Append("In control ").Append(open).Append(PersonFacts.Pct(a.InControlShare)).Append(close);
            sb.Append(dot).Append("fantasy ").Append(open).Append(PersonFacts.Pct(a.MeanFantasy)).Append(close);
            if (!compact) sb.Append(" of spending");
            sb.Append(dot).Append("fear ").Append(open).Append(PersonFacts.Pct(a.MeanFear)).Append(close);
            if (trust != null && !trust.IsEmpty && year >= trust.FirstYear)
            {
                sb.Append(dot).Append("trust ").Append(open).Append(PersonFacts.Pct(trust.At(year))).Append(close);
            }

            return sb.ToString();
        }

        /// <summary>The same readout without markup (logs, tests).</summary>
        public static string Plain(EconomicLives lives, YearSeries trust, int year, bool compact)
        {
            string rich = Line(lives, trust, year, compact, "FFFFFF");
            return rich.Replace("<color=#FFFFFF>", "").Replace("</color>", "");
        }

        /// <summary>Year as the chip shows it.</summary>
        public static string YearText(int year) => year.ToString(Ci);
    }
}
