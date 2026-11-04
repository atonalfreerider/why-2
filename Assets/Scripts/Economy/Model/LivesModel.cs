using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Why.Economy.Data;
using Why.Humans.Smv;

namespace Why.Economy.Model
{
    /// <summary>
    /// Lives that rise, stay or fall (M3, view "lives"; MODALITIES.md 7.2-7.4): what the lives view draws, computed from the
    /// economic lives' synthetic records and the approved real figures.
    /// <list type="bullet">
    /// <item><b>The start group.</b> Simulated adults aged 30-39 in the middle household income fifth (40th-60th percentile,
    /// <see cref="Mobility.Quintile"/>) in ⟨year−20⟩, followed every year to ⟨year⟩. If fewer than
    /// <see cref="MinRecords"/> qualify, the group widens to the neighbouring fifths (the 2nd-4th) and says so.</item>
    /// <item><b>Outcomes and base rates.</b> At ⟨year⟩ each record climbed (one or more fifths up), stayed or fell (one or
    /// more down), with n and a 90% Wilson interval for each share, and the fifth each ended in.</item>
    /// <item><b>Three typical lives.</b> For each outcome the medoid of the group's income-rank paths (each year's rank, L1
    /// distance summed over the years), among the records present every year; exact, so nothing is drawn at random: the
    /// population's and the lives' seeds are fixed, ties go to the lower record, and two runs pick the same records. Each
    /// life carries its wealth rank each year, the parent's earnings rank (<see cref="PersonTraits.ParentRank"/>), and 3-6
    /// named events (<see cref="PersonLife"/>'s kinds and conditions) with what changed: income rank and net worth the year
    /// before and in the event's own year, the change it is detected on and the one its tick states, with the year after
    /// kept beside them. When the medoid itself had fewer than <see cref="MinEvents"/> events in the years shown, the next
    /// most central record that had them (and is no farther from the median path than its group's upper quartile) is
    /// drawn, and the log says so.</item>
    /// <item><b>Your futures (7.4).</b> The odds of climbing, staying or falling over the decade to ⟨year⟩ for every adult at
    /// both ends by start fifth and age band (<see cref="Mobility"/>, the You panel's own lines), with n and 90% ranges.</item>
    /// <item><b>Real figures (7.3).</b> <see cref="RealLives"/>: the approved picks of visibility.json with their 2016 = 1
    /// indices and "together in k of m years" recomputed as a check.</item>
    /// </list>
    /// Built once at load for the economy's opening year by <see cref="LivesModelLayer"/>, which logs it, publishes its facts
    /// under lives.* and logs <see cref="Problems"/> as errors; <see cref="Build"/> is pure (any thread once the lives are
    /// ready) for another year or start, and the model is read-only after. The records are synthetic (one record stands for
    /// 100,000 people) and calibrated to national totals: the events are associations in the simulation, not causes.
    /// </summary>
    public sealed class LivesModel
    {
        /// <summary>Shared key of the opening year's model (<see cref="LivesModelLayer"/>, tier 4).</summary>
        public const string SharedKey = "economy.lives";

        /// <summary>Years from the start group to the year shown (7.1: the past corridor runs ⟨year−20⟩ → ⟨year⟩).</summary>
        public const int Span = 20;

        /// <summary>The start group's default fifth (the middle, 40th-60th percentile), its first age and the band's width:
        /// aged 30-39 at the start (age at the year's middle, <see cref="PersonYear.Age"/>).</summary>
        public const int MiddleFifth = 2, StartAge = 30, AgeBandYears = 10;

        /// <summary>Fewer records than this in the start fifth widen the group to its neighbours (7.2).</summary>
        public const int MinRecords = 60;

        /// <summary>Named events per life (7.2: 3-6 ticks).</summary>
        public const int MinEvents = 3, MaxEvents = 6;

        /// <summary>The standard normal's 95th percentile, Φ⁻¹(0.95): a two-sided 90% Wilson interval.</summary>
        public const double Z90 = 1.6448536269514722;

        /// <summary>The futures' window: the decade to the year (the You panel's, <see cref="Mobility.DefaultWindow"/>).</summary>
        public const int OddsWindow = Mobility.DefaultWindow;

        /// <summary>Lines below which a fifth's odds at one age band give way to the fifth's at every age: the default of
        /// <see cref="Mobility.OddsFrom"/>, so the futures and the You panel's odds rest on the same lines.</summary>
        public const int OddsMinLines = 20;

        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        /// <summary>Where a record ended against where it started.</summary>
        public enum Outcome { Climbed, Stayed, Fell }

        /// <summary>The outcomes as the view words them, in <see cref="Outcome"/> order.</summary>
        public static readonly string[] OutcomeNames = { "climbed", "stayed", "fell" };

        /// <summary>A share of records with its 90% Wilson interval.</summary>
        public readonly struct Rate
        {
            /// <summary>Records counted and records it is a share of.</summary>
            public readonly int Count, Of;

            /// <summary>Count / Of and the interval's ends (0..1; all 0 without records).</summary>
            public readonly double Value, Low, High;

            Rate(int count, int of, double value, double low, double high)
            {
                Count = count;
                Of = of;
                Value = value;
                Low = low;
                High = high;
            }

            /// <summary>k of n with its 90% Wilson score interval (it stays inside 0..1 and is not empty at 0 or n).</summary>
            public static Rate Wilson(int k, int n)
            {
                if (n <= 0) return new Rate(k, n, 0, 0, 0);
                double p = k / (double)n, z2 = Z90 * Z90;
                double denominator = 1 + z2 / n;
                double centre = (p + z2 / (2.0 * n)) / denominator;
                double half = Z90 * Math.Sqrt(p * (1 - p) / n + z2 / (4.0 * n * n)) / denominator;
                return new Rate(k, n, p, Math.Max(0, centre - half), Math.Min(1, centre + half));
            }

            /// <summary>"32% (24-41)": whole percents.</summary>
            public override string ToString() => Pct(Value) + "% (" + Pct(Low) + "-" + Pct(High) + ")";
        }

        /// <summary>A named event of a life, with what changed: the year before against the event's own year (the change it is
        /// detected on, which its text states), and the year after beside them.</summary>
        public sealed class LifeEvent
        {
            /// <summary>The year it happened: the change from <see cref="Before"/> to this year is the one it is detected on.</summary>
            public int Year;

            /// <summary>Its kind (<see cref="PersonLife.Kind"/>), its name ("lost a job") and any detail ("to health care").</summary>
            public PersonLife.Kind Kind;

            public string Name, Detail;

            /// <summary>The year before (the record before it, when that year has none) and the year after (the event's own
            /// year when it is the year shown).</summary>
            public int Before, Next;

            /// <summary>Household income rank (0..1) the year before, in the event's year and the year after.</summary>
            public float RankBefore, Rank, RankNext;

            /// <summary>Net worth ($ a person) the year before, in the event's year and the year after.</summary>
            public float WealthBefore, Wealth, WealthNext;

            /// <summary>"2009 · lost a job · income rank 62 → 38": the year before against the event's year.</summary>
            public string Text;
        }

        /// <summary>One typical life: the record drawn for an outcome and what the view draws of it.</summary>
        public sealed class Life
        {
            /// <summary>The record (its index in the population: "synthetic person #N") and its outcome.</summary>
            public int Person;

            public Outcome Outcome;

            /// <summary>The fifths it started and ended in (0 = the bottom .. 4 = the top).</summary>
            public int StartFifth, EndFifth;

            /// <summary>Age at the start year's middle, sex and birth (fractional year).</summary>
            public float StartAge;

            public bool Male;
            public double Birth;

            /// <summary>Household income rank (0..1) in each year from <see cref="LivesModel.From"/> to <see cref="LivesModel.Year"/>.</summary>
            public float[] Rank = Array.Empty<float>();

            /// <summary>Net worth's rank among the year's adults (0..1, the midpoint of ties; couples split evenly, as the records
            /// are), each year: the selected life's thin gold line.</summary>
            public float[] WealthRank = Array.Empty<float>();

            /// <summary>The parent's earnings rank in their own cohort (0..1, <see cref="PersonTraits.ParentRank"/>), and whether
            /// that parent is a simulated record (else the rank is a random draw, and no parents' line is drawn).</summary>
            public float ParentRank;

            public bool ParentSimulated;

            /// <summary>The named events (at most <see cref="MaxEvents"/>, by year) and how many it had in the years shown.</summary>
            public readonly List<LifeEvent> Events = new List<LifeEvent>();

            public int EventsShown;

            /// <summary>L1 distance (rank, summed over the years) to its group's median path, and to all its group's paths
            /// together (what the medoid minimizes).</summary>
            public double ToMedian, ToGroup;

            /// <summary>Its place among its group by <see cref="ToGroup"/> (0: the medoid) and the medoid's record.</summary>
            public int Rung, Medoid;

            /// <summary>"synthetic person #N · climbed: middle → fourth fifth".</summary>
            public string Label;
        }

        /// <summary>The records with one outcome and how their paths spread.</summary>
        public sealed class OutcomeGroup
        {
            public Outcome Outcome;

            /// <summary>The records (population indices, ascending).</summary>
            public int[] Members = Array.Empty<int>();

            /// <summary>The pointwise median of their income-rank paths (0..1, each year).</summary>
            public float[] MedianPath = Array.Empty<float>();

            /// <summary>Quartiles of the members' L1 distances to <see cref="MedianPath"/>.</summary>
            public double Q1, Q2, Q3;

            /// <summary>The life drawn (null when no record had this outcome).</summary>
            public Life Typical;
        }

        /// <summary>The odds of a start fifth and age band over the decade to the year, from every adult at both ends.</summary>
        public sealed class Odds
        {
            /// <summary>The fifth and the age band (<see cref="Mobility.AgeBandNames"/>) asked for, and the decade.</summary>
            public int Fifth, Band, From, Year;

            /// <summary>Fewer than <see cref="OddsMinLines"/> lines stood at that age, so the fifth's odds at every age stand in.</summary>
            public bool AllAges;

            /// <summary>Climbed, stayed and fell, of <see cref="N"/> lines (each 100,000 people).</summary>
            public Rate Climb, Stay, Fall;

            public int N;

            /// <summary>The share that ended in each fifth.</summary>
            public readonly double[] Reached = new double[Mobility.Quintiles];
        }

        /// <summary>The year shown and the start year (⟨year−20⟩ by default).</summary>
        public readonly int Year, From;

        /// <summary>The fifth and age band asked for (the start group's; <see cref="StartFifths"/> once widened).</summary>
        public readonly int Fifth, AgeFrom;

        /// <summary>People each record stands for (the population's line).</summary>
        public double PeoplePerRecord { get; private set; }

        /// <summary>The fifths the start group holds: the one asked for, or it and its neighbours when widened.</summary>
        public int[] StartFifths { get; private set; } = Array.Empty<int>();

        /// <summary>The group was widened: only <see cref="InFifth"/> records of the fifth asked for were followed to the year.</summary>
        public bool Widened { get; private set; }

        public int InFifth { get; private set; }

        /// <summary>Records in the start group in the start year; those followed every year to the year (n, what the base
        /// rates count); those who died before the year.</summary>
        public int StartRecords { get; private set; }

        public int N { get; private set; }
        public int Died { get; private set; }

        /// <summary>Of the n records: climbed, stayed, fell.</summary>
        public Rate Climbed { get; private set; }

        public Rate Stayed { get; private set; }
        public Rate Fell { get; private set; }

        /// <summary>Of the n records, how many ended in each fifth.</summary>
        public readonly int[] EndFifths = new int[Mobility.Quintiles];

        /// <summary>The groups by outcome (<see cref="Outcome"/> order).</summary>
        public readonly OutcomeGroup[] Groups = new OutcomeGroup[3];

        /// <summary>The decade behind the futures (null when the lives were not simulated).</summary>
        public Mobility Decade { get; private set; }

        /// <summary>The approved real figures (may be null when not given).</summary>
        public RealLives Real { get; private set; }

        /// <summary>Why nothing was simulated (empty when the lives were read).</summary>
        public string Note { get; private set; } = "";

        /// <summary>The model built at load for the economy's opening year (null before; <see cref="LivesModelLayer"/>).</summary>
        public static LivesModel Current => current;

        static volatile LivesModel current;

        /// <summary>Checks made while building, against the records themselves (see <see cref="Problems"/>).</summary>
        readonly List<string> checks = new List<string>();

        /// <summary>The odds' lines by start fifth, age band and outcome (climbed, stayed, fell), and the fifth reached.</summary>
        int[,,] odds = new int[Mobility.Quintiles, Mobility.AgeBands, 3];

        int[,,] reached = new int[Mobility.Quintiles, Mobility.AgeBands, Mobility.Quintiles];

        LivesModel(int year, int from, int fifth, int ageFrom)
        {
            Year = year;
            From = from;
            Fifth = fifth;
            AgeFrom = ageFrom;
            for (int o = 0; o < Groups.Length; o++) Groups[o] = new OutcomeGroup { Outcome = (Outcome)o };
        }

        /// <summary>The model on show from now on (the layer, at load).</summary>
        internal static void Show(LivesModel model) => current = model;

        /// <summary>Years drawn, from the start year to the year.</summary>
        public int Years => Year - From + 1;

        /// <summary>The share for an outcome.</summary>
        public Rate Base(Outcome o) => o == Outcome.Climbed ? Climbed : o == Outcome.Stayed ? Stayed : Fell;

        /// <summary>The life drawn for an outcome (null when no record had it).</summary>
        public Life Typical(Outcome o) => Groups[(int)o].Typical;

        /// <summary>"middle fifth", or "2nd-4th fifths" once widened.</summary>
        public string GroupName => FifthsName(StartFifths.Length > 0 ? StartFifths : new[] { Fifth });

        /// <summary>"30-39".</summary>
        public string Ages => AgeFrom.ToString(Ci) + "-" + (AgeFrom + AgeBandYears - 1).ToString(Ci);

        // ------------------------------------------------------------------ building

        /// <summary>
        /// The lives of <paramref name="year"/> (clamped to the simulated years): the start group in
        /// <paramref name="startYear"/> (0: ⟨year−20⟩), in <paramref name="fifth"/> and the ten years of age from
        /// <paramref name="ageFrom"/>; the typical lives, base rates and futures. <paramref name="real"/> is kept as given;
        /// <paramref name="decade"/> (the same year's 10-year Mobility) is reused when given. Pure; any thread once the lives
        /// are ready; a model with a <see cref="Note"/> and no records when they are not.
        /// </summary>
        public static LivesModel Build(EconomicLives lives, EconomyData data, int year, RealLives real, int startYear = 0,
            int fifth = MiddleFifth, int ageFrom = StartAge, Mobility decade = null) =>
            Assemble(lives, data, year, Task.FromResult(real), startYear, fifth, ageFrom, decade);

        /// <summary>
        /// The opening year's model, with the real figures still being parsed beside it (<see cref="LivesModelLayer"/>: their
        /// two files do not depend on the lives, so they are read while the records are); they are joined last.
        /// </summary>
        internal static LivesModel BuildAtLoad(EconomicLives lives, EconomyData data, int year, Task<RealLives> real) =>
            Assemble(lives, data, year, real, 0, MiddleFifth, StartAge, null);

        static LivesModel Assemble(EconomicLives lives, EconomyData data, int year, Task<RealLives> real, int startYear, int fifth,
            int ageFrom, Mobility decade)
        {
            fifth = Math.Max(0, Math.Min(Mobility.Quintiles - 1, fifth));
            if (lives == null || !lives.Ready || lives.Sim == null)
            {
                LivesModel none = new LivesModel(year, startYear > 0 ? startYear : year - Span, fifth, ageFrom);
                none.Note = "the economic lives were not simulated";
                none.Real = real?.GetAwaiter().GetResult();
                return none;
            }

            year = Math.Max(lives.FirstYear + 1, Math.Min(year, lives.LastYear));
            int from = Math.Max(lives.FirstYear, Math.Min(year - 1, startYear > 0 ? startYear : year - Span));
            LivesModel m = new LivesModel(year, from, fifth, ageFrom) { PeoplePerRecord = lives.Sim.PeoplePerLine };
            m.StartGroup(lives);
            for (int o = 0; o < m.Groups.Length; o++) m.Choose(lives, data, m.Groups[o]);
            m.WealthRanks(lives);
            m.Verify(lives);
            m.Decade = decade != null && decade.Year == year && decade.Window == Math.Min(OddsWindow, year - lives.FirstYear)
                ? decade
                : Mobility.Build(lives, null, year, OddsWindow);
            if (m.Decade != null)
            {
                m.odds = (int[,,])m.Decade.Odds.Clone();
                m.reached = (int[,,])m.Decade.Reached.Clone();
            }

            m.Real = real?.GetAwaiter().GetResult();
            return m;
        }

        /// <summary>One record of the start group, followed to the year.</summary>
        sealed class Record
        {
            public int Person, StartFifth;
            public float StartAge;
            public float[] Path;
        }

        /// <summary>Records of each outcome, by population index (filled by <see cref="StartGroup"/>, read by <see cref="Choose"/>).</summary>
        readonly List<Record>[] byOutcome = { new List<Record>(), new List<Record>(), new List<Record>() };

        /// <summary>
        /// The start group: adults in the age band in the start year whose fifth then is the one asked for (widened to its
        /// neighbours when fewer than <see cref="MinRecords"/> of those were followed to the year), each with its income-rank
        /// path; then each record's outcome and fifth at the year, and the base rates.
        /// </summary>
        void StartGroup(EconomicLives lives)
        {
            List<SmvPerson> people = lives.Sim.People;
            List<Record> followed = new List<Record>();
            List<(int person, int fifth)> lost = new List<(int, int)>();
            for (int i = 0; i < people.Count; i++)
            {
                if (!lives.TryGet(i, From, out PersonYear a) || !a.Adult || a.Age < AgeFrom || a.Age >= AgeFrom + AgeBandYears) continue;
                int q0 = Mobility.Quintile(a.IncomeRank);
                if (Math.Abs(q0 - Fifth) > 1) continue;
                float[] path = new float[Years];
                bool whole = true;
                for (int k = 0; k < path.Length && whole; k++)
                {
                    whole = lives.TryGet(i, From + k, out PersonYear r);
                    path[k] = r.IncomeRank;
                }

                if (whole) followed.Add(new Record { Person = i, StartFifth = q0, StartAge = a.Age, Path = path });
                else lost.Add((i, q0));
            }

            InFifth = followed.FindAll(r => r.StartFifth == Fifth).Count;
            Widened = InFifth < MinRecords;
            int lo = Widened ? Math.Max(0, Fifth - 1) : Fifth, hi = Widened ? Math.Min(Mobility.Quintiles - 1, Fifth + 1) : Fifth;
            StartFifths = new int[hi - lo + 1];
            for (int q = lo; q <= hi; q++) StartFifths[q - lo] = q;

            int died = 0, start = 0;
            foreach ((int person, int q) in lost)
            {
                if (q < lo || q > hi) continue;
                start++;
                if (people[person].Death < Year + 0.5) died++;
            }

            int[] count = new int[3];
            foreach (Record r in followed)
            {
                if (r.StartFifth < lo || r.StartFifth > hi) continue;
                start++;
                int q1 = Mobility.Quintile(r.Path[r.Path.Length - 1]);
                EndFifths[q1]++;
                Outcome o = q1 > r.StartFifth ? Outcome.Climbed : q1 == r.StartFifth ? Outcome.Stayed : Outcome.Fell;
                count[(int)o]++;
                byOutcome[(int)o].Add(r);
            }

            StartRecords = start;
            Died = died;
            N = count[0] + count[1] + count[2];
            Climbed = Rate.Wilson(count[0], N);
            Stayed = Rate.Wilson(count[1], N);
            Fell = Rate.Wilson(count[2], N);
        }

        /// <summary>
        /// An outcome's typical life: the median path and the members' distances to it (with their quartiles), then the
        /// medoid (the least total L1 distance to the group's other paths; ties: the lower record). The medoid is drawn
        /// unless it had fewer than <see cref="MinEvents"/> events in the years shown (7.2 names 3-6 per life); then the
        /// next most central record that had them and is no farther from the median path than the group's upper quartile
        /// (still typical) is, and if none is, the medoid with what it had.
        /// </summary>
        void Choose(EconomicLives lives, EconomyData data, OutcomeGroup g)
        {
            List<Record> members = byOutcome[(int)g.Outcome];
            int n = members.Count, years = Years;
            g.Members = new int[n];
            for (int k = 0; k < n; k++) g.Members[k] = members[k].Person;
            if (n == 0) return;

            // the pointwise median in double, so that distances equal in exact arithmetic (two members, say) stay equal
            double[] median = new double[years], column = new double[n];
            g.MedianPath = new float[years];
            for (int t = 0; t < years; t++)
            {
                for (int k = 0; k < n; k++) column[k] = members[k].Path[t];
                Array.Sort(column);
                median[t] = n % 2 == 1 ? column[n / 2] : 0.5 * (column[n / 2 - 1] + column[n / 2]);
                g.MedianPath[t] = (float)median[t];
            }

            double[] toMedian = new double[n], toGroup = new double[n];
            for (int a = 0; a < n; a++)
            {
                toMedian[a] = Distance(members[a].Path, median);
                for (int b = a + 1; b < n; b++)
                {
                    double d = Distance(members[a].Path, members[b].Path);
                    toGroup[a] += d;
                    toGroup[b] += d;
                }
            }

            double[] sorted = (double[])toMedian.Clone();
            Array.Sort(sorted);
            g.Q1 = Quantile(sorted, 0.25);
            g.Q2 = Quantile(sorted, 0.5);
            g.Q3 = Quantile(sorted, 0.75);

            // by centrality; members are in population order, so ties keep the lower record first
            int[] order = new int[n];
            for (int k = 0; k < n; k++) order[k] = k;
            Array.Sort(order, (x, y) => toGroup[x] != toGroup[y] ? toGroup[x].CompareTo(toGroup[y]) : x.CompareTo(y));

            Life chosen = null, medoid = null;
            for (int rung = 0; rung < n && chosen == null; rung++)
            {
                if (rung > 0 && toMedian[order[rung]] > g.Q3) continue;
                Record r = members[order[rung]];
                Life life = Read(lives, data, r, g.Outcome);
                life.Rung = rung;
                life.Medoid = members[order[0]].Person;
                life.ToMedian = toMedian[order[rung]];
                life.ToGroup = toGroup[order[rung]];
                if (rung == 0) medoid = life;
                if (life.EventsShown >= MinEvents) chosen = life;
            }

            g.Typical = chosen ?? medoid;
        }

        /// <summary>A record as a life: its label, parents' rank and events (wealth ranks come after, for the three drawn).</summary>
        Life Read(EconomicLives lives, EconomyData data, Record r, Outcome outcome)
        {
            SmvPerson p = lives.Sim.People[r.Person];
            PersonTraits traits = lives.Traits(r.Person);
            int end = Mobility.Quintile(r.Path[r.Path.Length - 1]);
            Life life = new Life
            {
                Person = r.Person, Outcome = outcome, StartFifth = r.StartFifth, EndFifth = end, StartAge = r.StartAge, Male = p.Male,
                Birth = p.Birth, Rank = r.Path, ParentRank = traits?.ParentRank ?? float.NaN,
                // TraitSampler draws the rank of a parent who is not a simulated record at random
                ParentSimulated = p.Mother >= 0 && p.Mother < r.Person || p.Father >= 0 && p.Father < r.Person,
                Label = "synthetic person #" + r.Person.ToString(Ci) + " · " + OutcomeNames[(int)outcome] + ": " +
                        (end == r.StartFifth ? Mobility.QuintileNames[end] : Short(r.StartFifth) + " → " + Mobility.QuintileNames[end])
            };
            Events(lives, data, life);
            return life;
        }

        /// <summary>
        /// The life's events in the years shown, with what changed from the year before into the event's year; at most
        /// <see cref="MaxEvents"/>, those whose year brought the largest change of income rank (ties: the kind's order, then
        /// the year), shown by year.
        /// <para>
        /// They are <see cref="PersonLife"/>'s events (its kinds and conditions, read off its adult records, in its order),
        /// named as 7.2 lists them and read again here over the years shown: PersonLife thins a whole life to 16 labels,
        /// dropping the latest job losses, moves and children first, which would take the ones these years show. A marriage
        /// that ends with an estate received that year ended with the spouse's death, as <see cref="Mobility"/> reads it: the
        /// estate is that loss, not an inheritance. PersonLife's own reading is compared: every event of these kinds it
        /// kept in these years must be here (else a check fails), so the two cannot drift apart. The start of the record,
        /// gaining control of one's path and the debt peak are PersonLife's markers, not events of 7.2's list.
        /// </para>
        /// </summary>
        void Events(EconomicLives lives, EconomyData data, Life life)
        {
            PersonLife pl = PersonLife.Build(lives, data, Year, life.Person);
            if (pl == null) return;
            string IndustryName(int k) => data != null && k >= 0 && k < data.Industries.Count ? data.Industries[k].Name : "industry " + k;

            List<LifeEvent> all = new List<LifeEvent>();
            bool job = false, home = false, business = false, retired = false;
            int kids = 0;
            for (int k = 0; k < pl.Records.Length; k++)
            {
                PersonYear r = pl.Records[k], b = k > 0 ? pl.Records[k - 1] : r;
                int y = pl.Years[k];
                bool shown = y >= From && y <= Year;
                bool ended = k > 0 && !r.Married && b.Married;
                if (r.Employed && r.Industry >= 0)
                {
                    if (!job && k > 0) Add(PersonLife.Kind.FirstJob, "first job", IndustryName(r.Industry));
                    else if (job && b.Employed && b.Industry >= 0 && b.Industry != r.Industry) Add(PersonLife.Kind.Industry, "changed industry", "to " + IndustryName(r.Industry));
                    job = true;
                }
                else if (k > 0 && b.Employed && r.Age < 60 && r.SocialSecurity <= 0) Add(PersonLife.Kind.JobLost, "lost a job", "");

                if (k > 0 && r.Married && !b.Married) Add(PersonLife.Kind.Married, "married", "");
                if (ended) Add(PersonLife.Kind.MarriageEnded, r.Inherited > 0 ? "lost a spouse" : "divorced or separated", "");
                if (r.Kids > kids) Add(PersonLife.Kind.Child, "had a child", r.Kids == 1 ? "first child" : r.Kids + " children at home");
                kids = r.Kids;
                if (r.Homeowner && !home)
                {
                    if (k > 0) Add(PersonLife.Kind.Home, "bought a home", "");
                    home = true;
                }

                if (r.SelfEmployed && !business)
                {
                    Add(PersonLife.Kind.Business, "started a business", "");
                    business = true;
                }

                if (r.Inherited > 1000 && !ended) Add(PersonLife.Kind.Inherited, "inherited from a parent", LivesReport.Money(r.Inherited));
                if (r.Discharged) Add(PersonLife.Kind.Discharged, "debt discharged", "");
                if (r.SocialSecurity > 0 && !retired)
                {
                    Add(PersonLife.Kind.Retired, "retired", "Social Security");
                    retired = true;
                }

                void Add(PersonLife.Kind kind, string name, string detail)
                {
                    if (!shown) return;
                    // what changed is the change the event is detected on, from the year before into its own year: compared
                    // across the event's year instead, a job lost one year and found again the next would read as a rise
                    int before = y - 1, next = Math.Min(Year, y + 1);
                    if (!lives.TryGet(life.Person, before, out PersonYear x))
                    {
                        x = b;
                        before = k > 0 ? pl.Years[k - 1] : y;
                    }

                    if (!lives.TryGet(life.Person, next, out PersonYear z))
                    {
                        z = r;
                        next = y;
                    }

                    all.Add(new LifeEvent
                    {
                        Year = y, Kind = kind, Name = name, Detail = detail, Before = before, Next = next, RankBefore = x.IncomeRank,
                        Rank = r.IncomeRank, RankNext = z.IncomeRank, WealthBefore = x.Wealth, Wealth = r.Wealth, WealthNext = z.Wealth,
                        Text = y.ToString(Ci) + " · " + name + " · income rank " + Pct(x.IncomeRank) + " → " + Pct(r.IncomeRank)
                    });
                }
            }

            // PersonLife's own reading of these years (after its thinning) holds nothing this one lacks
            foreach (PersonLife.Event e in pl.Events)
            {
                if (e.Year < From || e.Year > Year || e.Kind == PersonLife.Kind.Start || e.Kind == PersonLife.Kind.Control ||
                    e.Kind == PersonLife.Kind.DebtPeak) continue;
                bool spouse = e.Kind == PersonLife.Kind.Inherited && all.Exists(x => x.Year == e.Year && x.Kind == PersonLife.Kind.MarriageEnded);
                if (!spouse && !all.Exists(x => x.Year == e.Year && x.Kind == e.Kind))
                {
                    checks.Add($"synthetic person #{life.Person}: PersonLife reads '{e.Text}' in {e.Year}, the lives view does not");
                }
            }

            life.EventsShown = all.Count;
            all.Sort((x, y) =>
            {
                float dx = Math.Abs(x.Rank - x.RankBefore), dy = Math.Abs(y.Rank - y.RankBefore);
                if (dx != dy) return dy.CompareTo(dx);
                return x.Kind != y.Kind ? x.Kind.CompareTo(y.Kind) : x.Year.CompareTo(y.Year);
            });
            if (all.Count > MaxEvents) all.RemoveRange(MaxEvents, all.Count - MaxEvents);
            all.Sort((x, y) => x.Year != y.Year ? x.Year.CompareTo(y.Year) : x.Kind.CompareTo(y.Kind));
            life.Events.AddRange(all);
        }

        /// <summary>Each drawn life's net-worth rank among the adults of each year (the midpoint of tied ranks).</summary>
        void WealthRanks(EconomicLives lives)
        {
            List<Life> drawn = new List<Life>();
            foreach (OutcomeGroup g in Groups)
            {
                if (g.Typical != null) drawn.Add(g.Typical);
            }

            if (drawn.Count == 0) return;
            foreach (Life l in drawn) l.WealthRank = new float[Years];
            int people = lives.Sim.People.Count;
            float[] wealth = new float[people];
            for (int k = 0; k < Years; k++)
            {
                int n = 0;
                for (int i = 0; i < people; i++)
                {
                    if (lives.TryGet(i, From + k, out PersonYear r) && r.Adult) wealth[n++] = r.Wealth;
                }

                Array.Sort(wealth, 0, n);
                foreach (Life l in drawn)
                {
                    if (!lives.TryGet(l.Person, From + k, out PersonYear r) || n == 0)
                    {
                        l.WealthRank[k] = float.NaN;
                        continue;
                    }

                    int below = Lower(wealth, n, r.Wealth, false), atOrBelow = Lower(wealth, n, r.Wealth, true);
                    l.WealthRank[k] = (float)((below + atOrBelow) * 0.5 / n);
                }
            }
        }

        /// <summary>
        /// The drawn lives checked against the records again (not against the lists the choice was made from): each is in
        /// the start group (an adult of the age band in the start fifths in the start year, present every year to the year),
        /// its outcome is its fifths', it has 3-6 named events, and it is typical: no farther from its group's median path
        /// than the group's upper quartile of that distance. That is how "within its group's interquartile range of distance
        /// to the median path" (WP12) is read: a medoid usually lies nearer the median path than most of its group, below the
        /// lower quartile, which is what typical means; only the far side is a sign of an atypical pick.
        /// </summary>
        void Verify(EconomicLives lives)
        {
            foreach (OutcomeGroup g in Groups)
            {
                Life l = g.Typical;
                if (l == null) continue;
                string who = "the typical life who " + OutcomeNames[(int)g.Outcome] + " (#" + l.Person.ToString(Ci) + ")";
                bool start = lives.TryGet(l.Person, From, out PersonYear a) && a.Adult && a.Age >= AgeFrom && a.Age < AgeFrom + AgeBandYears &&
                             Array.IndexOf(StartFifths, Mobility.Quintile(a.IncomeRank)) >= 0;
                for (int y = From; y <= Year && start; y++) start = lives.TryGet(l.Person, y, out _);
                if (!start) checks.Add(who + " is not in the start group followed every year");
                int q0 = Mobility.Quintile(a.IncomeRank), q1 = lives.TryGet(l.Person, Year, out PersonYear z) ? Mobility.Quintile(z.IncomeRank) : -1;
                Outcome o = q1 > q0 ? Outcome.Climbed : q1 == q0 ? Outcome.Stayed : Outcome.Fell;
                if (o != g.Outcome) checks.Add(who + $" went from fifth {q0 + 1} to {q1 + 1}");
                if (l.ToMedian > g.Q3 + 1e-9)
                {
                    checks.Add(who + $" is {l.ToMedian.ToString("0.00", Ci)} from the median path, beyond the group's upper quartile " +
                               g.Q3.ToString("0.00", Ci));
                }

                if (l.Events.Count < MinEvents || l.Events.Count > MaxEvents)
                {
                    checks.Add(who + $" has {l.Events.Count} named events in {From}-{Year} (7.2: {MinEvents}-{MaxEvents})");
                }
            }
        }

        // ------------------------------------------------------------------ the futures (7.4)

        /// <summary>
        /// The odds of climbing, staying or falling over the decade to the year for adults who started in a fifth at an age
        /// band (<see cref="Mobility.AgeBand"/>), with n and 90% ranges; with fewer than <see cref="OddsMinLines"/> lines at
        /// that age, the fifth's odds at every age (<see cref="Odds.AllAges"/>), as the You panel reads them.
        /// </summary>
        public Odds ViewerOdds(int fifth, int band)
        {
            fifth = Math.Max(0, Math.Min(Mobility.Quintiles - 1, fifth));
            band = Math.Max(0, Math.Min(Mobility.AgeBands - 1, band));
            Odds o = new Odds { Fifth = fifth, Band = band, Year = Year, From = Decade?.From ?? Year - OddsWindow };
            int[] count = new int[3], ended = new int[Mobility.Quintiles];
            for (int k = 0; k < 3; k++) count[k] = odds[fifth, band, k];
            for (int q = 0; q < Mobility.Quintiles; q++) ended[q] = reached[fifth, band, q];
            if (count[0] + count[1] + count[2] < OddsMinLines)
            {
                o.AllAges = true;
                Array.Clear(count, 0, 3);
                Array.Clear(ended, 0, ended.Length);
                for (int a = 0; a < Mobility.AgeBands; a++)
                {
                    for (int k = 0; k < 3; k++) count[k] += odds[fifth, a, k];
                    for (int q = 0; q < Mobility.Quintiles; q++) ended[q] += reached[fifth, a, q];
                }
            }

            o.N = count[0] + count[1] + count[2];
            o.Climb = Rate.Wilson(count[0], o.N);
            o.Stay = Rate.Wilson(count[1], o.N);
            o.Fall = Rate.Wilson(count[2], o.N);
            for (int q = 0; q < Mobility.Quintiles && o.N > 0; q++) o.Reached[q] = ended[q] / (double)o.N;
            return o;
        }

        /// <summary>The futures of the viewer the You panel placed (its fifth and age band; null without a reading).</summary>
        public Odds ForViewer(YouModel.Result reading) => reading == null ? null : ViewerOdds(reading.Quintile, reading.AgeBand);

        // ------------------------------------------------------------------ checks, log, facts

        /// <summary>
        /// What does not hold (empty when every check passes): the lives were simulated and the start group has records;
        /// the outcomes sum to 1 and count every record once; every outcome has a typical life, in the start group and
        /// typical (see <see cref="Verify"/>), whose events agree with PersonLife's; every fifth and age band's odds sum to 1;
        /// and the real figures' checks (<see cref="RealLives.Problems"/>).
        /// </summary>
        public List<string> Problems()
        {
            List<string> list = new List<string>();
            if (Note.Length > 0) list.Add(Note);
            else if (N == 0) list.Add($"no record aged {Ages} in the {GroupName} in {From} was followed to {Year}");
            else
            {
                if (Math.Abs(Climbed.Value + Stayed.Value + Fell.Value - 1) > 1e-9) list.Add("the outcomes do not sum to 1");
                if (Climbed.Count + Stayed.Count + Fell.Count != N) list.Add("the outcomes do not count every record once");
                int ended = 0;
                foreach (int c in EndFifths) ended += c;
                if (ended != N) list.Add("the fifths reached do not count every record once");
                foreach (OutcomeGroup g in Groups)
                {
                    // nobody falls from the bottom fifth or climbs from the top: no gap when every start fifth is that one
                    bool impossible = g.Outcome == Outcome.Fell && StartFifths[StartFifths.Length - 1] == 0 ||
                                      g.Outcome == Outcome.Climbed && StartFifths[0] == Mobility.Quintiles - 1;
                    if (g.Typical == null && !impossible) list.Add($"no record {OutcomeNames[(int)g.Outcome]}, so there is no typical life to draw for it");
                }
            }

            list.AddRange(checks);
            for (int q = 0; q < Mobility.Quintiles && Decade != null; q++)
            {
                for (int band = 0; band < Mobility.AgeBands; band++)
                {
                    Odds o = ViewerOdds(q, band);
                    if (o.N > 0 && Math.Abs(o.Climb.Value + o.Stay.Value + o.Fall.Value - 1) > 1e-9)
                    {
                        list.Add($"the odds from the {Mobility.QuintileNames[q]} at {Mobility.AgeBandNames[band]} do not sum to 1");
                    }
                }
            }

            if (Real == null) list.Add("the real figures were not read");
            else list.AddRange(Real.Problems());
            return list;
        }

        /// <summary>
        /// The log lines: "[Why] Lives 2005-2025: middle fifth aged 30-39, n = N; climbed a% (lo-hi), stayed b%, fell c%;
        /// typical #i/#j/#k; ..." (the acceptance's line, then the other ranges, the start group and the fifths reached), one
        /// line per typical life with its events, the real figures and the futures' odds.
        /// </summary>
        public List<string> LogLines()
        {
            List<string> lines = new List<string>();
            string span = From.ToString(Ci) + "-" + Year.ToString(Ci);
            if (Note.Length > 0)
            {
                lines.Add("[Why] Lives " + span + ": " + Note);
                if (Real != null) lines.Add("[Why] Lives real " + Real.Line());
                return lines;
            }

            StringBuilder b = new StringBuilder("[Why] Lives ").Append(span).Append(": ").Append(GroupName).Append(" aged ").Append(Ages);
            if (Widened)
            {
                b.Append(" (widened: ").Append(InFifth).Append(" records of the ").Append(Mobility.QuintileNames[Fifth])
                    .Append(" followed, fewer than ").Append(MinRecords).Append(')');
            }

            b.Append(", n = ").Append(N).Append("; climbed ").Append(Climbed).Append(", stayed ").Append(Pct(Stayed.Value))
                .Append("%, fell ").Append(Pct(Fell.Value)).Append("%; typical");
            for (int o = 0; o < Groups.Length; o++) b.Append(o == 0 ? " #" : "/#").Append(Groups[o].Typical?.Person.ToString(Ci) ?? "-");
            b.Append("; 90% ranges: stayed ").Append(Pct(Stayed.Low)).Append('-').Append(Pct(Stayed.High)).Append(", fell ")
                .Append(Pct(Fell.Low)).Append('-').Append(Pct(Fell.High)).Append("; ").Append(StartRecords).Append(" in the start group, ")
                .Append(N).Append(" followed to ").Append(Year.ToString(Ci)).Append(", ").Append(Died).Append(" died before; ended in");
            for (int q = 0; q < Mobility.Quintiles; q++)
            {
                b.Append(q == 0 ? " " : ", ").Append(Short(q)).Append(' ').Append(EndFifths[q]);
            }

            b.Append("; 1 record = ").Append(PeoplePerRecord.ToString("#,0", Ci)).Append(" people");
            lines.Add(b.ToString());

            foreach (OutcomeGroup g in Groups)
            {
                Life l = g.Typical;
                if (l == null) continue;
                StringBuilder s = new StringBuilder("[Why] Lives typical ").Append(OutcomeNames[(int)g.Outcome]).Append(": ").Append(l.Label)
                    .Append(" (aged ").Append(((int)l.StartAge).ToString(Ci)).Append(" in ").Append(From.ToString(Ci)).Append("); income rank ")
                    .Append(Pct(l.Rank[0])).Append(" → ").Append(Pct(l.Rank[l.Rank.Length - 1]));
                if (l.WealthRank.Length > 0)
                {
                    s.Append(", wealth rank ").Append(Pct(l.WealthRank[0])).Append(" → ").Append(Pct(l.WealthRank[l.WealthRank.Length - 1]));
                }

                s.Append(l.ParentSimulated ? ", parents' rank " + Pct(l.ParentRank) : ", parent not simulated");
                s.Append(l.Rung == 0
                    ? "; the medoid of " + g.Members.Length
                    : "; the most central of " + g.Members.Length + " with " + MinEvents + "+ events (rung " + l.Rung +
                      "; the medoid #" + l.Medoid.ToString(Ci) + " had fewer)");
                s.Append(", ").Append(l.ToMedian.ToString("0.00", Ci)).Append(" from the median path (quartiles ")
                    .Append(g.Q1.ToString("0.00", Ci)).Append('-').Append(g.Q3.ToString("0.00", Ci)).Append("); events ")
                    .Append(l.Events.Count).Append(" of ").Append(l.EventsShown).Append(':');
                foreach (LifeEvent e in l.Events)
                {
                    s.Append(' ').Append(e.Text);
                    if (e.Detail.Length > 0) s.Append(" (").Append(e.Detail).Append(')');
                    s.Append(';');
                }

                lines.Add(s.ToString());
            }

            if (Real != null) lines.Add("[Why] Lives real " + Real.Line());
            if (Decade != null) lines.Add("[Why] Lives odds " + OddsLine());
            return lines;
        }

        /// <summary>"2015-2025, every adult at both ends: bottom fifth climbed 40% stayed 60% fell 0% (n 600), ...; age bands with
        /// fewer than 20 lines ...".</summary>
        string OddsLine()
        {
            StringBuilder b = new StringBuilder();
            b.Append(Decade.From.ToString(Ci)).Append('-').Append(Year.ToString(Ci)).Append(" (the you futures; every adult at both ends, by fifth then):");
            int thin = 0, cells = 0;
            for (int q = 0; q < Mobility.Quintiles; q++)
            {
                int u = 0, s = 0, d = 0;
                for (int band = 0; band < Mobility.AgeBands; band++)
                {
                    u += odds[q, band, 0];
                    s += odds[q, band, 1];
                    d += odds[q, band, 2];
                    cells++;
                    if (odds[q, band, 0] + odds[q, band, 1] + odds[q, band, 2] < OddsMinLines) thin++;
                }

                int t = Math.Max(1, u + s + d);
                b.Append(q == 0 ? " " : "; ").Append(Mobility.QuintileNames[q]).Append(" climbed ").Append(Pct(u / (double)t))
                    .Append("% stayed ").Append(Pct(s / (double)t)).Append("% fell ").Append(Pct(d / (double)t)).Append("% (n ")
                    .Append(u + s + d).Append(')');
            }

            b.Append("; ").Append(thin).Append(" of ").Append(cells).Append(" fifth and age cells under ").Append(OddsMinLines)
                .Append(" lines read the fifth at every age");
            return b.ToString();
        }

        /// <summary>
        /// Publishes the lives under lives.* (<see cref="ModelFacts"/>; the tour's lives cards): the years, the start group, n,
        /// the three shares with their 90% ranges and counts, the typical lives' records and labels, and the real figures'
        /// (lives.real.*). The layer publishes the opening year's model only, so the facts do not depend on which year was
        /// built last.
        /// </summary>
        public void PublishFacts()
        {
            Real?.PublishFacts();
            if (Note.Length > 0 || N == 0) return;
            string source = $"LivesModel {From}-{Year}: the economic lives' synthetic records (EconomicLives), adults aged {Ages} " +
                            $"in the {GroupName} in {From} followed to {Year}";
            string basis = "modelled: synthetic records calibrated to national totals, 1 record = " + PeoplePerRecord.ToString("#,0", Ci) +
                           " people; associations in the simulation, not causes";
            ModelFacts.Set("lives.year", Year, "year", source, "assumed: the lives view's year, which the start group is followed to");
            ModelFacts.Set("lives.start", From, "year", source, "assumed: the start year, " + Span + " years before the lives view's year (MODALITIES.md 7.2)");
            ModelFacts.Set("lives.group", GroupName, "", source, Widened
                ? "assumed: the neighbouring fifths too, as fewer than " + MinRecords + " records of the " + Mobility.QuintileNames[Fifth] + " were followed (7.2)"
                : "assumed: the start fifth (MODALITIES.md 7.2)");
            ModelFacts.Set("lives.ages", Ages, "years", source, "assumed: the start ages (MODALITIES.md 7.2), age at the start year's middle");
            ModelFacts.Set("lives.n", N, "records", source, basis + "; records in the start group followed every year");
            for (int o = 0; o < 3; o++)
            {
                Rate r = Base((Outcome)o);
                string key = "lives." + OutcomeNames[o];
                string how = basis + $"; {r.Count} of {r.Of} records, rounded to a whole percent";
                ModelFacts.Set(key, Percent(r.Value), "%", source, how);
                ModelFacts.Set(key + ".lo", Percent(r.Low), "%", source, how + "; the 90% Wilson interval's lower end");
                ModelFacts.Set(key + ".hi", Percent(r.High), "%", source, how + "; the 90% Wilson interval's upper end");
                ModelFacts.Set(key + ".k", r.Count, "records", source, basis);
                Life l = Groups[o].Typical;
                if (l == null) continue;
                string typical = basis + (l.Rung == 0 ? "; the medoid of the group's income-rank paths (L1)" : "; the most central record with " + MinEvents + "+ events");
                ModelFacts.Set("lives.typical." + OutcomeNames[o], l.Person, "record", source, typical);
                ModelFacts.Set("lives.typical." + OutcomeNames[o] + ".label", l.Label, "", source, typical);
                ModelFacts.Set("lives.typical." + OutcomeNames[o] + ".events", l.Events.Count, "events", source, typical + "; named events shown");
            }
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>L1 distance between two rank paths, summed over the years.</summary>
        static double Distance(float[] a, float[] b)
        {
            double d = 0;
            for (int t = 0; t < a.Length; t++) d += Math.Abs((double)a[t] - b[t]);
            return d;
        }

        /// <summary>L1 distance from a rank path to the median path, summed over the years.</summary>
        static double Distance(float[] a, double[] median)
        {
            double d = 0;
            for (int t = 0; t < a.Length; t++) d += Math.Abs(a[t] - median[t]);
            return d;
        }

        /// <summary>The p-quantile of sorted values, interpolated between order statistics (as numpy's default).</summary>
        static double Quantile(double[] sorted, double p)
        {
            if (sorted.Length == 0) return double.NaN;
            double h = (sorted.Length - 1) * p;
            int lo = (int)Math.Floor(h), hi = Math.Min(sorted.Length - 1, lo + 1);
            return sorted[lo] + (h - lo) * (sorted[hi] - sorted[lo]);
        }

        /// <summary>The number of values below v (or at or below it), of the first n sorted values.</summary>
        static int Lower(float[] sorted, int n, float v, bool orEqual)
        {
            int lo = 0, hi = n;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (sorted[mid] < v || orEqual && sorted[mid] == v) lo = mid + 1;
                else hi = mid;
            }

            return lo;
        }

        /// <summary>A share as whole percent points ("62").</summary>
        static string Pct(double share) => double.IsNaN(share) ? "-" : Percent(share).ToString("0", Ci);

        /// <summary>A share in whole percent, halves away from zero (12.5 is 13), as the log and the facts print it.</summary>
        static double Percent(double share) => Math.Round(100 * share, MidpointRounding.AwayFromZero);

        /// <summary>A fifth without the word ("middle", "fourth"), for "middle → fourth fifth".</summary>
        static string Short(int fifth) => Mobility.QuintileNames[fifth].Replace(" fifth", "");

        /// <summary>"middle fifth" for one fifth, "2nd-4th fifths" for several.</summary>
        static string FifthsName(int[] fifths)
        {
            if (fifths.Length == 1) return Mobility.QuintileNames[fifths[0]];
            return Ordinal(fifths[0] + 1) + "-" + Ordinal(fifths[fifths.Length - 1] + 1) + " fifths";
        }

        static string Ordinal(int n) => n.ToString(Ci) + (n == 1 ? "st" : n == 2 ? "nd" : n == 3 ? "rd" : "th");
    }
}
