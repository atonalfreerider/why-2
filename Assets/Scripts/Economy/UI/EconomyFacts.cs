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

        /// <summary>
        /// The player of the land the person plays as (<see cref="PlayerFacts.PlaysAs"/>) and its index; "" and -1 when the
        /// land's snapshot is of another year or the person is in no player.
        /// </summary>
        public string PlaysAs = "";

        public int PlayerIndex = -1;

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

        /// <summary>Heading of the life charts: "LIFETIME 1961 - 2026", or "SINCE ARRIVING" / "IN THE RECORD" when the
        /// record starts after birth (an immigrant's arrival, or 1946 for someone born before).</summary>
        public string LifeHeading = "";

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
            bool fromBirth = first <= Math.Floor(p.Birth) + 1;
            f.LifeHeading = (fromBirth ? "LIFETIME  " : p.Immigrant ? "SINCE ARRIVING  " : "IN THE RECORD  ") +
                            first.ToString(Ci) + " - " + last.ToString(Ci);
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
                // the record starts at birth (the first year alive at its middle), or at arrival for an immigrant born
                // before the year asked for
                when += p.Immigrant && p.Birth < requested + 1
                    ? " (ARRIVED " + YearOf(p.Enter) + ")"
                    : requested == (int)Math.Floor(p.Birth)
                        ? " (BORN LATE IN " + requested.ToString(Ci) + ")"
                        : " (NOT YET BORN IN " + requested.ToString(Ci) + ")";
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
                : "Dollars of " + year.ToString(Ci) + " ($1 then bought what $" +
                  toReal.ToString(toReal < 10 ? "0.00" : "0", Ci) + " buys in 2025)";
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
    /// The year's readout under the year chip (7.6): "2025 · GDP $30.8T · in control 12% · fantasy 19% · fear 54% · trust 25%
    /// (GSS) · cooperation 0.73", with "estimate (H1 annualized)" for an estimated year and "companies: 2025 data" while
    /// the towers show; the chip's tooltip is the circuit's notes for the year.
    /// </summary>
    public static class YearFacts
    {
        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        /// <summary>
        /// The readout for a year (rich text: labels dim, numbers in <paramref name="valueHex"/>). GDP, the fantasy and fear
        /// shares of the money spent and the season's cooperation come from the land's snapshot when it shows that year
        /// (<paramref name="land"/>; otherwise fantasy and fear are the adults' means from the lives, and GDP and
        /// cooperation are left out); in control from the lives; trust from the General Social Survey (left out before its
        /// first year, held at its last reading after it). Compact drops the GSS mark and the companies' note for narrow
        /// screens. Empty without the lives.
        /// </summary>
        public static string Line(EconomicLives lives, YearSeries trust, Land.LandSnapshot land, EconomyData data, int year,
            bool compact, string valueHex)
        {
            PopulationYear a = lives != null && lives.Ready ? lives.Aggregate(year) : null;
            if (a == null) return "";
            string open = "<color=#" + valueHex + ">", close = "</color>";
            const string dot = "  \u00B7  ";
            bool shown = land != null && land.Year == year && land.Players != null;
            StringBuilder sb = new StringBuilder();
            sb.Append(open).Append(year.ToString(Ci)).Append(close);
            if (shown && land.Land != null) sb.Append(dot).Append("GDP ").Append(open).Append(Land.LandFacts.Money(land.Land.Gdp, 1)).Append(close);
            sb.Append(dot).Append("in control ").Append(open).Append(PersonFacts.Pct(a.InControlShare)).Append(close);
            double fantasy = a.MeanFantasy, fear = a.MeanFear;
            if (shown) Shares(land.Players, out fear, out fantasy);
            sb.Append(dot).Append("fantasy ").Append(open).Append(PersonFacts.Pct(fantasy)).Append(close);
            sb.Append(dot).Append("fear ").Append(open).Append(PersonFacts.Pct(fear)).Append(close);
            if (trust != null && !trust.IsEmpty && year >= trust.FirstYear)
            {
                sb.Append(dot).Append("trust ").Append(open).Append(PersonFacts.Pct(trust.At(year))).Append(close);
                if (!compact) sb.Append(" (GSS)");
            }

            float[] coop = shown ? land.Society?.Cooperation : null;
            if (coop != null && coop.Length > 0)
            {
                sb.Append(dot).Append("cooperation ").Append(open).Append(coop[coop.Length - 1].ToString("0.00", Ci)).Append(close);
            }

            if (data != null && data.IsEstimate(year)) sb.Append(dot).Append("estimate (H1 annualized)");
            if (!compact && shown && land.Land?.Towers != null && land.Land.Towers.Length > 0) sb.Append(dot).Append("companies: 2025 data");
            return sb.ToString();
        }

        /// <summary>The fear and fantasy shares of the players' spending dollars (the rivers' motive split).</summary>
        public static void Shares(Land.PlayerSet players, out double fear, out double fantasy)
        {
            double spend = 0, f = 0, x = 0;
            foreach (Land.Player p in players.Players)
            {
                for (int c = 0; c < 6; c++)
                {
                    spend += p.Category[c];
                    f += p.CategoryFear[c];
                    x += p.CategoryFantasy[c];
                }
            }

            fear = spend > 0 ? f / spend : 0;
            fantasy = spend > 0 ? x / spend : 0;
        }

        /// <summary>The same readout without markup (logs, tests).</summary>
        public static string Plain(EconomicLives lives, YearSeries trust, Land.LandSnapshot land, EconomyData data, int year, bool compact)
        {
            string rich = Line(lives, trust, land, data, year, compact, "FFFFFF");
            return rich.Replace("<color=#FFFFFF>", "").Replace("</color>", "");
        }

        /// <summary>Year as the chip shows it.</summary>
        public static string YearText(int year) => year.ToString(Ci);
    }

    /// <summary>
    /// The land legend's text for a year (0.1, 7.6): the unit system ("1 square = $1.28T a year · width 0.1 = $3.1T a year ·
    /// roots ×4 · height 1 = $5.1T of market value · a dot = 100,000 people") and the accounts readout from the year's money
    /// circuit ("GDP $30.8T · the circuit balances within 1.1% (tier:raw)" and its notes: the approximations and residuals
    /// are printed, never hidden behind the headline).
    /// </summary>
    public static class LandLegendFacts
    {
        /// <summary>Lines of the circuit's notes the legend prints (the rest are in the year chip's tooltip).</summary>
        public const int NoteLines = 3;

        /// <summary>The unit system of the year's land.</summary>
        public static string Units(Land.LandSnapshot s, double peoplePerLine)
        {
            double gdp = s?.Land?.Gdp ?? 0;
            if (gdp <= 0) return "";
            const string dot = "  \u00B7  ";
            return "1 square = " + Land.LandFacts.Money(gdp / Land.LandStyle.AreaGdp) + " a year" + dot +
                   "width 0.1 = " + Land.LandFacts.Money(0.1 * gdp / Land.LandStyle.WidthGdp, 1) + " a year" + dot +
                   "roots \u00D7" + Land.LandStyle.RootWidthGdp.ToString("0", CultureInfo.InvariantCulture) + dot +
                   "height 1 = " + Land.LandFacts.Money(gdp / Land.LandStyle.HeightGdp, 1) + " of market value" + dot +
                   "a dot = " + HudCount(peoplePerLine) + " people";
        }

        /// <summary>"GDP $30.8T · the circuit balances within 1.1% (tier:raw)", with the estimate mark.</summary>
        public static string Accounts(Land.LandSnapshot s, EconomyData data)
        {
            CircuitYear c = s?.Circuit;
            if (c == null) return "";
            string line = "GDP " + Land.LandFacts.Money(c.Gdp, 1) + "  \u00B7  the circuit balances within " +
                          Land.LandFacts.Percent(c.Imbalance, 1) + (string.IsNullOrEmpty(c.ImbalanceNode) ? "" : " (" + c.ImbalanceNode + ")");
            if (data != null && data.IsEstimate(c.Year)) line += "  \u00B7  estimate (H1 annualized)";
            return line;
        }

        /// <summary>The first <paramref name="lines"/> lines of the circuit's notes after its GDP line (empty when none).</summary>
        public static string Notes(Land.LandSnapshot s, int lines)
        {
            string n = s?.Circuit?.Notes;
            if (string.IsNullOrEmpty(n)) return "";
            string[] all = n.Split('\n');
            List<string> keep = new List<string>();
            for (int i = 1; i < all.Length && keep.Count < lines; i++)
            {
                if (all[i].Trim().Length > 0) keep.Add(all[i].Trim());
            }

            return string.Join("\n", keep);
        }

        /// <summary>100000 → "100,000".</summary>
        static string HudCount(double n) => n.ToString("#,0", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Everything the player inspector shows about one player of the land in its year (7.5), generated from the snapshot's
    /// numbers: who it is (its group and the group's rule, its anchor and the named employers paying its wages, party and
    /// generation shares, adults and children), its money (income by source, taxes, spending by the six categories with
    /// their fear and fantasy shares and first recipients, saving or borrowing, wealth, and the water budget's identity in
    /// words), its mind against the population's, and its place in the season of tit for tat. Pure (no Unity objects but
    /// colors), so the harness can print it; the panel only lays it out.
    /// </summary>
    public sealed class PlayerFacts
    {
        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;
        const string Dot = "  ·  ";

        /// <summary>The group names on player labels (the inhabitants' labels say the same).</summary>
        public static readonly string[] GroupLabel =
        {
            "The 1%", "Owners", "Gig", "PMC", "Public", "Office", "Frontline", "Working poor", "Retirees", "SS retirees",
            "Students", "Out of work"
        };

        static readonly string[] GenerationNames = { "Silent", "Boomers", "Gen X", "Millennials", "Gen Z" };
        static readonly string[] StandingNames = { "beta", "alpha", "omega", "anti-alpha" };

        /// <summary>The mind's gauges: names, the player's values and the population's (adults; spending-weighted for fear and fantasy).</summary>
        public static readonly string[] MindNames = { "Reason", "Higher OS", "Future", "Agency", "In control", "Fear", "Fantasy" };

        public int Player = -1, Year;

        /// <summary>"Frontline & trades workers · trade · Republicans" and "Frontline · trade · R" (the label's).</summary>
        public string Title = "", Short = "";

        /// <summary>The group's rule (groups.json) and where it stands.</summary>
        public string Rule = "";

        /// <summary>"4.1M adults and 1.0M children (51 lines)", party and generation shares, the named employers.</summary>
        public string People = "", Party = "", Generations = "", Employers = "";

        /// <summary>Income by source (wages, business, capital, transfers) in $B, and the money lines.</summary>
        public readonly double[] Income = new double[4];

        public string IncomeLine = "", Identity = "", Balance = "";

        /// <summary>Spending by the six categories: names, dollars, fear and fantasy shares, the top three first recipients.</summary>
        public readonly string[] CategoryNames = new string[6], Recipients = new string[6];

        public readonly double[] Category = new double[6];
        public readonly float[] CategoryFear = new float[6], CategoryFantasy = new float[6];
        public double Spending;
        public float Fear, Fantasy;

        public readonly float[] Mind = new float[7], MindMean = new float[7];

        /// <summary>The strongest desires and fears (members' mean drive weights), the modes of thought, family.</summary>
        public string Wants = "", Fears = "", Modes = "", Family = "";

        /// <summary>The season: strategy, the members' own strategy mix, pairs, cooperation, coalition, standing, memory.</summary>
        public string Play = "", Mix = "", Dealings = "", Standing = "";

        /// <summary>Members: the one with the median income (the panel's "Show a member"), and the members by income.</summary>
        public int MedianMember = -1;

        public int[] MembersByIncome = Array.Empty<int>();

        /// <summary>
        /// The facts of player <paramref name="index"/> of a snapshot, with the season shown (<paramref name="season"/>, else
        /// the snapshot's) at <paramref name="round"/>; null when there is no such player.
        /// </summary>
        public static PlayerFacts Build(Land.LandSnapshot s, EconomyModel model, Land.SocialSeasonResult season, int round, int index)
        {
            Land.Player[] ps = s?.Players?.Players;
            if (ps == null || index < 0 || index >= ps.Length || model?.Data == null) return null;
            EconomyData data = model.Data;
            EconomicLives lives = model.Lives;
            Land.Player p = ps[index];
            PlayerFacts f = new PlayerFacts { Player = index, Year = s.Year };
            f.Who(p, s, data);
            f.Money(p, s, data);
            f.MindOf(p, ps, lives, data, s.Year);
            f.SocietyOf(p, ps, season ?? s.Society, round, data, s.Year);
            f.MembersOf(p, lives, s.Year);
            return f;
        }

        /// <summary>A player's name on its label: "Frontline · trade · R".</summary>
        public static string Label(Land.Player p, EconomyData data) =>
            GroupLabel[Math.Min((int)p.Group, GroupLabel.Length - 1)] + " · " + AnchorName(p.AnchorId, data) + " · " + p.Tribe;

        /// <summary>
        /// The person inspector's link (7.5): "Plays as: Frontline · trade · R (4.1M adults) · Coalition 3 · standing beta"
        /// for a person of the snapshot's year, or "" when the person is in no player (a child of nobody, another year).
        /// </summary>
        public static string PlaysAs(Land.LandSnapshot s, Land.SocialSeasonResult season, int round, int person, EconomyData data,
            out int player)
        {
            player = -1;
            int[] of = s?.Players?.PlayerOfPerson;
            if (of == null || person < 0 || person >= of.Length || of[person] < 0) return "";
            player = of[person];
            Land.Player p = s.Players.Players[player];
            StringBuilder sb = new StringBuilder("Plays as: ");
            sb.Append(Label(p, data)).Append(" (").Append(Land.LandFacts.Millions(p.Adults.Length * 1e5)).Append(" adults)");
            season = season ?? s.Society;
            int coalition = CoalitionOf(season, player, round);
            if (coalition >= 0) sb.Append(Dot).Append("Coalition ").Append((coalition + 1).ToString(Ci));
            if (season?.Standing != null && player < season.Standing.Length)
            {
                sb.Append(Dot).Append("standing ").Append(StandingNames[Math.Min(3, (int)season.Standing[player])]);
            }

            return sb.ToString();
        }

        // ------------------------------------------------------------------ who

        void Who(Land.Player p, Land.LandSnapshot s, EconomyData data)
        {
            SocialGroup g = (int)p.Group < (data.GroupsFile?.Groups?.Count ?? 0) ? data.GroupsFile.Groups[(int)p.Group] : null;
            Title = (g?.Name ?? GroupLabel[(int)p.Group]) + Dot + AnchorName(p.AnchorId, data) + Dot + TribeName(p.Tribe);
            Short = Label(p, data);
            Rule = (string.IsNullOrEmpty(g?.Rule) ? "" : "Rule: " + g.Rule.TrimEnd('.') + ".") +
                   (string.IsNullOrEmpty(g?.Where) ? "" : " Stands: " + char.ToLowerInvariant(g.Where[0]) + g.Where.Substring(1));
            People = Land.LandFacts.Millions(p.Adults.Length * 1e5) + " adults and " + Land.LandFacts.Millions(p.Children.Length * 1e5) +
                     " children (" + Land.LandFacts.Count(p.Adults.Length + p.Children.Length) + " lines of 100,000)";
            Party = "Democrats " + Land.LandFacts.Percent(p.TribeShares[0]) + Dot + "Republicans " + Land.LandFacts.Percent(p.TribeShares[1]) +
                    Dot + "independents " + Land.LandFacts.Percent(p.TribeShares[2]) + " (party is drawn independently of class in this model)";
            StringBuilder gen = new StringBuilder();
            for (int k = 0; k < 5; k++)
            {
                if (p.Generations[k] < 0.005f) continue;
                if (gen.Length > 0) gen.Append(Dot);
                gen.Append(GenerationNames[k]).Append(' ').Append(Land.LandFacts.Percent(p.Generations[k]));
            }

            Generations = gen.ToString();

            // the named employers: the wage arcs from the towers (expected US employment shares, 3.5)
            Dictionary<int, double> byCompany = new Dictionary<int, double>();
            double named = 0;
            if (s.Money != null)
            {
                foreach (Land.FlowPath fp in s.Money.Paths)
                {
                    if (fp.Kind != Land.FlowKind.WagesCompany || fp.To != p.Index) continue;
                    byCompany.TryGetValue(fp.From, out double v);
                    byCompany[fp.From] = v + fp.Dollars;
                    named += fp.Dollars;
                }
            }

            List<KeyValuePair<int, double>> list = new List<KeyValuePair<int, double>>(byCompany);
            list.Sort((a, b) => b.Value != a.Value ? b.Value.CompareTo(a.Value) : a.Key.CompareTo(b.Key));
            StringBuilder e = new StringBuilder();
            if (p.Wages > 0)
            {
                e.Append("Wages from ").Append(TopIndustries(p.WagesBy, p.Wages, data, 3));
                foreach (KeyValuePair<int, double> kv in list)
                {
                    e.Append(Dot).Append(CompanyName(s, kv.Key)).Append(' ').Append(Land.LandFacts.Percent(kv.Value / p.Wages));
                }

                if (list.Count > 0) e.Append(" (named employers: expected shares of US employment)");
            }

            Employers = e.ToString();
        }

        static string CompanyName(Land.LandSnapshot s, int company)
        {
            foreach (Land.TowerGeom t in s.Land?.Towers ?? Array.Empty<Land.TowerGeom>())
            {
                if (t.Company == company) return t.Name;
            }

            return "company " + company.ToString(Ci);
        }

        /// <summary>"trade 86%, manufacturing 9%" from dollars by industry.</summary>
        static string TopIndustries(double[] by, double total, EconomyData data, int n)
        {
            int[] order = new int[by.Length];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            Array.Sort(order, (a, b) => by[b] != by[a] ? by[b].CompareTo(by[a]) : a.CompareTo(b));
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < Math.Min(n, order.Length); i++)
            {
                if (by[order[i]] <= 0 || total <= 0 || by[order[i]] / total < 0.01) break;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(IndustryName(data, order[i])).Append(' ').Append(Land.LandFacts.Percent(by[order[i]] / total));
            }

            return sb.ToString();
        }

        // ------------------------------------------------------------------ money

        void Money(Land.Player p, Land.LandSnapshot s, EconomyData data)
        {
            Income[0] = p.Wages;
            Income[1] = p.Business;
            Income[2] = p.Capital;
            Income[3] = p.Transfers;
            string M(double v) => Land.LandFacts.Money(v);
            IncomeLine = "Wages " + M(p.Wages) + Dot + "business " + M(p.Business) + Dot + "capital " + M(p.Capital) + Dot +
                         "transfers " + M(p.Transfers) + (p.SocialSecurity > 0.05 ? " (Social Security " + M(p.SocialSecurity) + ")" : "") +
                         Dot + "taxes " + M(p.Taxes);

            Spending = 0;
            double fear = 0, fantasy = 0;
            for (int c = 0; c < 6; c++)
            {
                Category cat = data.CategoryById(EconomyData.CategoryIds[c]);
                CategoryNames[c] = cat?.Name ?? EconomyData.CategoryIds[c];
                Category[c] = p.Category[c];
                CategoryFear[c] = p.Category[c] > 0 ? (float)(p.CategoryFear[c] / p.Category[c]) : 0;
                CategoryFantasy[c] = p.Category[c] > 0 ? (float)(p.CategoryFantasy[c] / p.Category[c]) : 0;
                Spending += p.Category[c];
                fear += p.CategoryFear[c];
                fantasy += p.CategoryFantasy[c];
                Recipients[c] = Sellers(s.Money?.CategoryToSeller, c, data);
            }

            Fear = Spending > 0 ? (float)(fear / Spending) : 0;
            Fantasy = Spending > 0 ? (float)(fantasy / Spending) : 0;

            // the water budget (4.1): in = out
            double borrow = p.Saving < 0 ? -p.Saving : 0;
            double inflow = p.Wages + p.Business + p.Capital + p.Transfers + borrow;
            double outflow = p.Taxes + Spending + Math.Max(0, p.Saving);
            Identity = "in " + M(inflow) + " = spending " + M(Spending) + (p.Saving > 0 ? " + saving " + M(p.Saving) : "") + " + taxes " +
                       M(p.Taxes) + (borrow > 0 ? " (borrowing " + M(borrow) + " of it)" : "") + Dot + "gap " +
                       Land.LandFacts.Money(Math.Abs(inflow - outflow) < 0.05 ? 0 : inflow - outflow);
            double adults = Math.Max(1, p.Adults.Length * 1e5);
            Balance = (p.Saving >= 0 ? "Saves " + M(p.Saving) : "Borrows " + M(-p.Saving)) + Dot + "wealth " + M(p.Wealth) + " ($" +
                      Thousands(p.Wealth * 1e9 / adults) + " per adult)" + Dot + "debt " + M(p.Debt);
        }

        /// <summary>A category's top three first recipients: "health 54%, finance 10%, manufacturing 8%".</summary>
        static string Sellers(double[,] seller, int c, EconomyData data)
        {
            if (seller == null || c >= seller.GetLength(0)) return "";
            int n = seller.GetLength(1);
            double[] row = new double[n];
            for (int k = 0; k < n; k++) row[k] = seller[c, k];
            return TopIndustries(row, 1, data, 3);
        }

        static string Thousands(double dollars) =>
            dollars >= 999_500 ? (dollars / 1e6).ToString("0.0", Ci) + "M" : (dollars / 1e3).ToString("0", Ci) + "K";

        // ------------------------------------------------------------------ mind

        void MindOf(Land.Player p, Land.Player[] all, EconomicLives lives, EconomyData data, int year)
        {
            Mind[0] = p.Reason;
            Mind[1] = p.HigherOs;
            Mind[2] = p.Future;
            Mind[3] = p.Agency;
            Mind[4] = p.InControl;
            Mind[5] = Fear;
            Mind[6] = Fantasy;

            // the population's: adult-weighted means over the players, spending-weighted for fear and fantasy
            double w = 0, spend = 0;
            double[] sum = new double[7];
            foreach (Land.Player q in all)
            {
                double a = q.Adults.Length;
                w += a;
                sum[0] += a * q.Reason;
                sum[1] += a * q.HigherOs;
                sum[2] += a * q.Future;
                sum[3] += a * q.Agency;
                sum[4] += a * q.InControl;
                for (int c = 0; c < 6; c++)
                {
                    spend += q.Category[c];
                    sum[5] += q.CategoryFear[c];
                    sum[6] += q.CategoryFantasy[c];
                }
            }

            for (int k = 0; k < 5; k++) MindMean[k] = w > 0 ? (float)(sum[k] / w) : 0;
            MindMean[5] = spend > 0 ? (float)(sum[5] / spend) : 0;
            MindMean[6] = spend > 0 ? (float)(sum[6] / spend) : 0;

            // the strongest desires and fears: the members' mean drive weights
            float[] d = new float[5], fr = new float[5], ds = new float[5], fs = new float[5];
            int n = 0;
            if (lives != null && lives.Ready)
            {
                foreach (int person in p.Adults)
                {
                    if (!lives.DriveWeights(person, year, d, fr)) continue;
                    for (int k = 0; k < 5; k++)
                    {
                        ds[k] += d[k];
                        fs[k] += fr[k];
                    }

                    n++;
                }
            }

            if (n > 0)
            {
                for (int k = 0; k < 5; k++)
                {
                    ds[k] /= n;
                    fs[k] /= n;
                }

                Wants = Top(ds, LivesInputs.DesireIds, data.Psyche?.Desires);
                Fears = Top(fs, LivesInputs.FearIds, data.Psyche?.Fears);
            }

            StringBuilder m = new StringBuilder("Modes of thought (population shares of waking time, not modeled per player): ");
            List<Drive> modes = data.Psyche?.Modes;
            for (int k = 0; modes != null && k < modes.Count; k++)
            {
                if (k > 0) m.Append(Dot);
                m.Append(modes[k].Name ?? modes[k].Id).Append(' ').Append(Land.LandFacts.Percent(modes[k].Share));
            }

            Modes = m.ToString();
            Family = "Married " + Land.LandFacts.Percent(p.Married) + Dot + "with children " + Land.LandFacts.Percent(p.WithKids) + Dot +
                     "mean age " + p.Age.ToString("0", Ci);
        }

        static string Top(float[] weights, string[] ids, List<Drive> names)
        {
            int[] order = { 0, 1, 2, 3, 4 };
            Array.Sort(order, (a, b) => weights[b] != weights[a] ? weights[b].CompareTo(weights[a]) : a.CompareTo(b));
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < 3; i++)
            {
                if (i > 0) sb.Append(Dot);
                string id = ids[order[i]], name = id;
                foreach (Drive dr in names ?? new List<Drive>())
                {
                    if (dr != null && dr.Id == id && !string.IsNullOrEmpty(dr.Name)) name = dr.Name;
                }

                sb.Append(name).Append(' ').Append(PersonFacts.Pct(weights[order[i]]));
            }

            return sb.ToString();
        }

        // ------------------------------------------------------------------ society

        void SocietyOf(Land.Player p, Land.Player[] all, Land.SocialSeasonResult s, int round, EconomyData data, int year)
        {
            Land.SocialSettings set = s?.Settings ?? Land.SocialSettings.Default;
            float h = p.HigherOs;
            float forgive = set.Forgive == 1 ? 1f / 3 : set.Forgive == 2 ? 0 : h / 3;
            double pol = (data.Games?.Tribes?.AffectivePolarization?.DistrustSeries() ?? YearSeries.Empty).At(year) * set.Polarization;
            double memory = 0.5 * Math.Min(1, pol) * (1 - h);
            Play = "Plays tit for tat; forgives " + Land.LandFacts.Percent(forgive) +
                   (set.Forgive == 0 ? " of defections (a third of its " + Land.LandFacts.Percent(h) + " on the higher OS)" : " of defections") +
                   Dot + "tribal memory " + memory.ToString("0.00", Ci);
            StringBuilder mix = new StringBuilder("Members' own strategies (shown, not played): ");
            int[] order = { 0, 1, 2, 3, 4, 5, 6 };
            Array.Sort(order, (a, b) => p.Strategy[b] != p.Strategy[a] ? p.Strategy[b].CompareTo(p.Strategy[a]) : a.CompareTo(b));
            for (int i = 0; i < order.Length; i++)
            {
                if (p.Strategy[order[i]] < 0.005f) break;
                if (i > 0) mix.Append(Dot);
                mix.Append(PrisonersDilemma.Name((PdStrategy)order[i])).Append(' ').Append(PersonFacts.Pct(p.Strategy[order[i]]));
            }

            Mix = mix.ToString();
            if (s?.PairA == null || s.CoopAB == null)
            {
                Dealings = Standing = "";
                return;
            }

            int t = Math.Max(0, Math.Min(round, s.CoopAB.Length - 1));
            int inGroup = 0, outGroup = 0;
            double given = 0, received = 0, weight = 0;
            for (int k = 0; k < s.PairA.Length; k++)
            {
                int a = s.PairA[k], b = s.PairB[k];
                if (a != p.Index && b != p.Index) continue;
                int q = a == p.Index ? b : a;
                if (all[q].Group == p.Group) inGroup++;
                else outGroup++;
                double e = s.Exposure != null && t < s.Exposure.Length ? s.Exposure[t][k] : 1;
                float ab = s.CoopAB[t][k], ba = s.CoopBA[t][k];
                given += e * (a == p.Index ? ab : ba);
                received += e * (a == p.Index ? ba : ab);
                weight += e;
            }

            Dealings = "Round " + t.ToString(Ci) + ": " + (inGroup + outGroup).ToString(Ci) + " partners (" + inGroup.ToString(Ci) +
                       " in its group, " + outGroup.ToString(Ci) + " outside)" + Dot + "cooperation given " +
                       (weight > 0 ? given / weight : 0).ToString("0.00", Ci) + ", received " + (weight > 0 ? received / weight : 0).ToString("0.00", Ci);
            int coalition = CoalitionOf(s, p.Index, t);
            Standing = (coalition >= 0 ? "Coalition " + (coalition + 1).ToString(Ci) : "In no coalition") +
                       (s.Standing != null && p.Index < s.Standing.Length ? Dot + "standing " + StandingNames[Math.Min(3, (int)s.Standing[p.Index])] : "");
        }

        /// <summary>A player's coalition at the detection a round shows (the last at or before it), or -1.</summary>
        public static int CoalitionOf(Land.SocialSeasonResult s, int player, int round)
        {
            if (s?.CoalitionAt == null || s.CoalitionAt.Length == 0) return -1;
            int d = 0;
            int[] at = Land.LandStyle.Detections;
            for (int i = 0; i < Math.Min(at.Length, s.CoalitionAt.Length); i++)
            {
                if (at[i] <= round) d = i;
            }

            int[] labels = s.CoalitionAt[d];
            return player >= 0 && player < labels.Length ? labels[player] : -1;
        }

        // ------------------------------------------------------------------ members

        void MembersOf(Land.Player p, EconomicLives lives, int year)
        {
            int n = p.Adults.Length;
            float[] income = new float[n];
            int[] order = new int[n];
            for (int k = 0; k < n; k++)
            {
                order[k] = k;
                if (lives != null && lives.TryGet(p.Adults[k], year, out PersonYear r)) income[k] = r.Wages + r.Business + r.CapitalIncome + r.Transfers;
            }

            Array.Sort(order, (a, b) => income[a] != income[b] ? income[a].CompareTo(income[b]) : p.Adults[a].CompareTo(p.Adults[b]));
            MembersByIncome = new int[n];
            for (int k = 0; k < n; k++) MembersByIncome[k] = p.Adults[order[k]];
            MedianMember = n > 0 ? MembersByIncome[n / 2] : -1;
        }

        // ------------------------------------------------------------------ names

        /// <summary>An anchor's short name: "trade", "state & local", "services", "pensions (Boomers)", "other industries".</summary>
        public static string AnchorName(string anchorId, EconomyData data)
        {
            if (string.IsNullOrEmpty(anchorId)) return "?";
            if (anchorId == "rest") return "other industries";
            string id = anchorId.StartsWith("tier:", StringComparison.Ordinal) ? anchorId.Substring(5) : anchorId;
            int slash = id.IndexOf('/');
            if (slash > 0)
            {
                string gen = id.Substring(slash + 1);
                GenerationInfo gi = data?.GroupsFile?.Generations?.Find(x => x.Id == gen);
                return Plain(id.Substring(0, slash)) + " (" + (gi?.Name ?? gen) + ")";
            }

            return Plain(id);
        }

        static string Plain(string id)
        {
            switch (id)
            {
                case "state_local": return "state & local";
                case "oil_gas": return "oil & gas";
                case "mining_metals": return "mining & metals";
                case "media_telecom": return "media & telecom";
                default: return id.Replace('_', ' ');
            }
        }

        static string IndustryName(EconomyData data, int i) => i >= 0 && i < data.Industries.Count ? Plain(data.Industries[i].Id) : "?";

        static string TribeName(char t) => t == 'D' ? "Democrats" : t == 'R' ? "Republicans" : t == 'I' ? "independents" : "mixed party";
    }
}
