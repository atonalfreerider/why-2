using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Why.Economy.Land;

namespace Why.Economy.Model
{
    /// <summary>
    /// Who moved up or down the hill over a decade, and what came with the move (the mobility view and the "You" panel).
    /// For a year and the <see cref="Window"/> years before it, every adult record alive and adult at both ends is placed
    /// on the hill's altitude at each end: the household income quintile, the same rank that sets a person's rung on the
    /// slopes (class is altitude). The life events between the two ends are read off the yearly records: a job lost or
    /// found, a change of industry, a marriage begun or ended, a child, a home, a business, an inheritance, a large
    /// uninsured bill, debt discharged, retirement.
    /// <list type="bullet">
    /// <item>the decade's transition matrix in people (each line stands for 100,000), and the wealth groups' (the clouds);</item>
    /// <item>for climbs and falls of adults of prime working age at the start (25-54: retirement and late-life
    /// inheritances would otherwise read as causes of falls), the share that came with each event and how much more often
    /// than among everyone of that age (the lift: the event's rate among the movers over its rate among all);</item>
    /// <item>the odds of moving up, staying or falling from each quintile by age at the start (what the "You" panel quotes),
    /// and the quintile each line ended in (what the "You" view draws, fifth by fifth);</item>
    /// <item>the forces at each rung in the year: the share of income saved, the share from what people own (capital and
    /// business income), transfers, debt service, the fear share of spending, the runway and the share in control, for
    /// the adults at both ends by the population's quintiles and for the closed panel by its own (so each ladder's rungs
    /// describe the people it counts);</item>
    /// <item>each player's drift: where its members stood a decade before, so the cohorts' own motion can be drawn on
    /// their hills.</item>
    /// </list>
    /// Pure and deterministic: reads the finished lives (any thread once they are ready), allocates its own arrays. The
    /// records are synthetic and calibrated to national totals, so the events co-occur with moves in the model: they are
    /// associations inside a simulation, not causal estimates.
    /// </summary>
    public sealed class Mobility
    {
        /// <summary>The events read off the records, in display order.</summary>
        public enum Cause { JobLost, JobFound, Industry, Married, MarriageEnded, Widowed, Child, Home, Business, Inherited, Loss, Discharged, Retired }

        public const int Causes = 13, Quintiles = 5, AgeBands = 4, WealthGroups = 4;
        /// <summary>The default window (years).</summary>
        public const int DefaultWindow = 10;
        /// <summary>People per lifeline.</summary>
        public const double PeoplePerLine = 1e5;

        /// <summary>Short names of the events (labels) and their sentence forms ("{0}% of falls came with ...").</summary>
        public static readonly string[] CauseNames =
            { "lost a job", "found work", "changed industry", "married", "divorced or separated", "lost a spouse", "had a child",
              "bought a home", "started a business", "inherited from a parent", "a large uninsured bill", "debt discharged", "retired" };

        /// <summary>The age bands at the window's start: 18-34, 35-49, 50-64, 65+.</summary>
        public static readonly string[] AgeBandNames = { "18-34", "35-49", "50-64", "65+" };
        public static int AgeBand(float age) => age < 35 ? 0 : age < 50 ? 1 : age < 65 ? 2 : 3;
        /// <summary>The household income quintile of a rank (0 = the bottom fifth .. 4 = the top fifth).</summary>
        public static int Quintile(float rank) => Math.Max(0, Math.Min(Quintiles - 1, (int)(rank * Quintiles)));
        public static readonly string[] QuintileNames = { "bottom fifth", "second fifth", "middle fifth", "fourth fifth", "top fifth" };

        /// <summary>The year, the window's first year and its length.</summary>
        public readonly int Year, From, Window;
        /// <summary>Adult lines alive at both ends (the panel), and the people they stand for.</summary>
        public int Lines { get; private set; }
        public double People => Lines * PeoplePerLine;

        /// <summary>People moving from quintile a (the window's start) to quintile b (the year): [a, b].</summary>
        public readonly double[,] Moves = new double[Quintiles, Quintiles];
        /// <summary>The closest analogue of the published decade matrices (the US Treasury's closed panel): adults 25 and
        /// older at the window's start, their fifths ranked within the panel itself at each end, in people [a, b].</summary>
        public readonly double[,] PanelMoves = new double[Quintiles, Quintiles];
        /// <summary>People moving between wealth groups (0 bottom 50% .. 3 top 1%): [a, b].</summary>
        public readonly double[,] WealthMoves = new double[WealthGroups, WealthGroups];

        /// <summary>Lines that climbed at least one quintile, fell at least one, stayed (every adult at both ends).</summary>
        public int Up { get; private set; }
        public int Down { get; private set; }
        public int Stay { get; private set; }
        /// <summary>The same for adults of prime working age at the start (<see cref="PrimeFrom"/>-<see cref="PrimeTo"/>),
        /// the panel the events are read against.</summary>
        public int PrimeUp { get; private set; }
        public int PrimeDown { get; private set; }
        public int PrimeStay { get; private set; }
        public int PrimeLines => PrimeUp + PrimeDown + PrimeStay;
        public const float PrimeFrom = 25, PrimeTo = 55;
        /// <summary>Per event, among prime-age adults: lines that had it among the climbers, the fallers, the stayers.</summary>
        public readonly int[] UpWith = new int[Causes], DownWith = new int[Causes], StayWith = new int[Causes];

        /// <summary>Per start quintile and age band: lines that climbed, stayed, fell ([q, band, 0 up / 1 stay / 2 down]).</summary>
        public readonly int[,,] Odds = new int[Quintiles, AgeBands, 3];
        /// <summary>Per start quintile and age band: lines that ended in each quintile ([q, band, end]); a climb of one fifth
        /// and one of four are both "up" in <see cref="Odds"/>.</summary>
        public readonly int[,,] Reached = new int[Quintiles, AgeBands, Quintiles];

        /// <summary>The forces at one rung (a quintile) in the year, over its adults (dollar-weighted where it says so).</summary>
        public sealed class Rung
        {
            public int Lines;
            /// <summary>Saving over disposable income (dollars); income from what people own (capital and own business)
            /// over income; transfers over income; mean debt service; spending-weighted fear and fantasy shares.</summary>
            public double SavingRate, OwnedShare, TransferShare, DebtService, Fear, Fantasy;
            /// <summary>Mean runway (years of spending in financial assets, capped at 10), share in control of their path,
            /// share owning a home, mean net worth and debt ($ per person), share hit by a large uninsured bill.</summary>
            public double Runway, InControl, Homeowners, Wealth, Debt, LossShare;
        }
        /// <summary>The forces by quintile: the adults at both ends (<see cref="Lines"/>) by the population's quintile they
        /// end in (<see cref="Rungs"/>: the all-adults ladder's rungs), and the closed panel's members by the quintile they
        /// end in, ranked among themselves (<see cref="PanelRungs"/>: the like-for-like ladder's rungs).</summary>
        public readonly Rung[] Rungs = new Rung[Quintiles], PanelRungs = new Rung[Quintiles];

        /// <summary>One player's drift: where its adult members stood a decade before.</summary>
        public struct Drift
        {
            /// <summary>Members' mean income rank now and at the window's start (those adult then), and how many were.</summary>
            public float RankNow, RankThen; public int Members, Tracked;
            /// <summary>The event its members had most beyond the population's rate (its lift), and the share who had it.</summary>
            public int Cause; public float CauseShare, CauseLift;
        }
        /// <summary>Per player of the year's census (same order); empty when no census was given.</summary>
        public readonly Drift[] Drifts;

        /// <summary>The body of the "[Why] Mobility" log line.</summary>
        public string Log { get; private set; } = "";

        Mobility(int year, int window, int players)
        {
            Year = year; Window = window; From = year - window;
            for (int q = 0; q < Quintiles; q++) { Rungs[q] = new Rung(); PanelRungs[q] = new Rung(); }
            Drifts = new Drift[Math.Max(0, players)];
        }

        /// <summary>The decade ending in <paramref name="year"/> (null when the lives were not simulated or the window
        /// starts before the record).</summary>
        public static Mobility Build(EconomicLives lives, PlayerSet census, int year, int window = DefaultWindow)
        {
            if (lives == null || !lives.Ready || lives.Sim == null) return null;
            year = Math.Min(year, lives.LastYear);
            window = Math.Max(1, Math.Min(window, year - lives.FirstYear));
            if (window < 1) return null;
            var m = new Mobility(year, window, census?.Players?.Length ?? 0);
            m.Run(lives, census);
            return m;
        }

        void Run(EconomicLives lives, PlayerSet census)
        {
            int n = lives.Sim.People.Count;
            var had = new bool[Causes];
            // per person: whether they were tracked and the events they had (for the players' drift)
            var tracked = new bool[n]; var events = new int[n]; var rankThen = new float[n];
            var all = new int[Causes];
            var panel = new List<(float start, float end, int person)>();
            for (int i = 0; i < n; i++)
            {
                if (!lives.TryGet(i, From, out PersonYear a) || !a.Adult || !lives.TryGet(i, Year, out PersonYear z) || !z.Adult) continue;
                Array.Clear(had, 0, Causes);
                bool whole = true;
                PersonYear b = a;
                for (int y = From + 1; y <= Year && whole; y++)
                {
                    if (!lives.TryGet(i, y, out PersonYear r)) { whole = false; break; }
                    Read(b, r, had);
                    b = r;
                }
                if (!whole) continue;
                int q0 = Quintile(a.IncomeRank), q1 = Quintile(z.IncomeRank);
                Lines++; tracked[i] = true; rankThen[i] = a.IncomeRank;
                if (a.Age >= 25) panel.Add((a.IncomeRank + i * 1e-9f, z.IncomeRank + i * 1e-9f, i));
                Moves[q0, q1] += PeoplePerLine;
                WealthMoves[Math.Min(3, (int)a.WealthGroup), Math.Min(3, (int)z.WealthGroup)] += PeoplePerLine;
                int kind = q1 > q0 ? 0 : q1 == q0 ? 1 : 2;
                Odds[q0, AgeBand(a.Age), kind]++;
                Reached[q0, AgeBand(a.Age), q1]++;
                if (kind == 0) Up++; else if (kind == 1) Stay++; else Down++;
                bool prime = a.Age >= PrimeFrom && a.Age < PrimeTo;
                if (prime) { if (kind == 0) PrimeUp++; else if (kind == 1) PrimeStay++; else PrimeDown++; }
                int mask = 0;
                for (int c = 0; c < Causes; c++)
                {
                    if (!had[c]) continue;
                    mask |= 1 << c; all[c]++;
                    if (!prime) continue;
                    if (kind == 0) UpWith[c]++; else if (kind == 1) StayWith[c]++; else DownWith[c]++;
                }
                events[i] = mask;
            }
            var ended = Ranked(panel);
            Forces(lives, tracked, panel, ended);
            if (census?.Players != null) Drifted(lives, census, tracked, events, rankThen, all);
            Log = Report();
        }

        /// <summary>The closed panel's matrix: each member's fifth by its position among the panel at each end. Returns the
        /// fifth each member ends in (same order as <paramref name="panel"/>).</summary>
        int[] Ranked(List<(float start, float end, int person)> panel)
        {
            int n = panel.Count; if (n == 0) return Array.Empty<int>();
            var byStart = new int[n]; var byEnd = new int[n];
            for (int k = 0; k < n; k++) { byStart[k] = k; byEnd[k] = k; }
            Array.Sort(byStart, (x, y) => panel[x].start.CompareTo(panel[y].start));
            Array.Sort(byEnd, (x, y) => panel[x].end.CompareTo(panel[y].end));
            var q0 = new int[n]; var q1 = new int[n];
            for (int k = 0; k < n; k++) { q0[byStart[k]] = Math.Min(Quintiles - 1, k * Quintiles / n); q1[byEnd[k]] = Math.Min(Quintiles - 1, k * Quintiles / n); }
            for (int k = 0; k < n; k++) PanelMoves[q0[k], q1[k]] += PeoplePerLine;
            return q1;
        }

        /// <summary>Of the closed panel's members who started in fifth a, the share who ended in b.</summary>
        public double PanelShare(int a, int b)
        {
            double row = 0; for (int c = 0; c < Quintiles; c++) row += PanelMoves[a, c];
            return row > 0 ? PanelMoves[a, b] / row : 0;
        }

        /// <summary>The events of one year step (b: the year before, r: the year).</summary>
        static void Read(in PersonYear b, in PersonYear r, bool[] had)
        {
            if (b.Employed && !r.Employed && r.Age < 62 && r.SocialSecurity <= 0) had[(int)Cause.JobLost] = true;
            if (!b.Employed && r.Employed) had[(int)Cause.JobFound] = true;
            if (b.Employed && r.Employed && b.Industry >= 0 && r.Industry >= 0 && b.Industry != r.Industry) had[(int)Cause.Industry] = true;
            if (!b.Married && r.Married) had[(int)Cause.Married] = true;
            // a marriage that ends with an estate received the same year ended with the spouse's death (the records' only
            // estates are a spouse's or a parent's): widowhood, not a divorce or an inheritance from a parent
            bool ended = b.Married && !r.Married, estate = r.Inherited > 0;
            if (ended && estate) had[(int)Cause.Widowed] = true;
            else if (ended) had[(int)Cause.MarriageEnded] = true;
            if (r.Kids > b.Kids) had[(int)Cause.Child] = true;
            if (!b.Homeowner && r.Homeowner) had[(int)Cause.Home] = true;
            if (!b.SelfEmployed && r.SelfEmployed) had[(int)Cause.Business] = true;
            // an inheritance from a parent worth at least a quarter of a year's disposable income
            if (estate && !ended && r.Inherited >= .25f * Math.Max(1000f, r.Disposable)) had[(int)Cause.Inherited] = true;
            if (r.Loss > 0) had[(int)Cause.Loss] = true;
            if (r.Discharged) had[(int)Cause.Discharged] = true;
            if (b.SocialSecurity <= 0 && r.SocialSecurity > 0) had[(int)Cause.Retired] = true;
        }

        /// <summary>The forces at each rung in the year: the adults the matrix tracks (<paramref name="tracked"/>: adult at
        /// both ends, every year recorded) by the population's quintile they end in, and the closed panel's members by the
        /// quintile they end in among themselves (<paramref name="ended"/>, one per member). Adults who came of age within
        /// the window are on neither ladder, so they are not counted at its rungs either.</summary>
        void Forces(EconomicLives lives, bool[] tracked, List<(float start, float end, int person)> panel, int[] ended)
        {
            var bothEnds = new Tally(Rungs);
            for (int i = 0; i < tracked.Length; i++)
                if (tracked[i] && lives.TryGet(i, Year, out PersonYear r)) bothEnds.Add(r, Quintile(r.IncomeRank));
            bothEnds.Finish();
            var members = new Tally(PanelRungs);
            for (int k = 0; k < panel.Count; k++)
                if (lives.TryGet(panel[k].person, Year, out PersonYear r)) members.Add(r, ended[k]);
            members.Finish();
        }

        /// <summary>The sums behind a set of rungs, finished into their shares and means.</summary>
        sealed class Tally
        {
            readonly Rung[] rungs;
            readonly double[] save = new double[Quintiles], disposable = new double[Quintiles], owned = new double[Quintiles],
                income = new double[Quintiles], transfers = new double[Quintiles], spend = new double[Quintiles],
                fear = new double[Quintiles], fantasy = new double[Quintiles];

            public Tally(Rung[] rungs) { this.rungs = rungs; }

            public void Add(in PersonYear r, int q)
            {
                var g = rungs[q];
                g.Lines++;
                save[q] += r.Saving; disposable[q] += Math.Max(0, r.Disposable);
                double inc = Math.Max(0, r.Wages) + Math.Max(0, r.Business) + Math.Max(0, r.CapitalIncome) + Math.Max(0, r.Transfers);
                income[q] += inc; owned[q] += Math.Max(0, r.Business) + Math.Max(0, r.CapitalIncome); transfers[q] += Math.Max(0, r.Transfers);
                spend[q] += Math.Max(0, r.Spending); fear[q] += Math.Max(0, r.Spending) * r.FearShare; fantasy[q] += Math.Max(0, r.Spending) * r.Fantasy;
                g.DebtService += r.DebtService; g.Runway += Math.Min(10f, Math.Max(0, r.Runway)); g.InControl += r.InControl ? 1 : 0;
                g.Homeowners += r.Homeowner ? 1 : 0; g.Wealth += r.Wealth; g.Debt += r.Debt; g.LossShare += r.Loss > 0 ? 1 : 0;
            }

            public void Finish()
            {
                for (int q = 0; q < Quintiles; q++)
                {
                    var g = rungs[q]; int c = Math.Max(1, g.Lines);
                    g.SavingRate = disposable[q] > 0 ? save[q] / disposable[q] : 0;
                    g.OwnedShare = income[q] > 0 ? owned[q] / income[q] : 0;
                    g.TransferShare = income[q] > 0 ? transfers[q] / income[q] : 0;
                    g.Fear = spend[q] > 0 ? fear[q] / spend[q] : 0; g.Fantasy = spend[q] > 0 ? fantasy[q] / spend[q] : 0;
                    g.DebtService /= c; g.Runway /= c; g.InControl /= c; g.Homeowners /= c; g.Wealth /= c; g.Debt /= c; g.LossShare /= c;
                }
            }
        }

        void Drifted(EconomicLives lives, PlayerSet census, bool[] tracked, int[] events, float[] rankThen, int[] all)
        {
            var players = census.Players; var count = new int[Causes];
            for (int p = 0; p < players.Length; p++)
            {
                var d = new Drift { Cause = -1 };
                double now = 0, then = 0; Array.Clear(count, 0, Causes);
                foreach (int i in players[p].Adults)
                {
                    if (!lives.TryGet(i, Year, out PersonYear r) || !r.Adult) continue;
                    d.Members++; now += r.IncomeRank;
                    if (i >= tracked.Length || !tracked[i]) continue;
                    d.Tracked++; then += rankThen[i];
                    for (int c = 0; c < Causes; c++) if ((events[i] & (1 << c)) != 0) count[c]++;
                }
                d.RankNow = d.Members > 0 ? (float)(now / d.Members) : 0;
                d.RankThen = d.Tracked > 0 ? (float)(then / d.Tracked) : d.RankNow;
                // the event that sets the cohort apart: the highest lift over the population among events at least a
                // fifth of its tracked members had
                float best = 1.15f;
                for (int c = 0; c < Causes && d.Tracked > 0; c++)
                {
                    float share = count[c] / (float)d.Tracked, base0 = Lines > 0 ? all[c] / (float)Lines : 0;
                    if (share < .2f || base0 <= 0) continue;
                    float lift = share / base0;
                    if (lift > best) { best = lift; d.Cause = c; d.CauseShare = share; d.CauseLift = lift; }
                }
                Drifts[p] = d;
            }
        }

        // ------------------------------------------------------------------ readouts

        /// <summary>Share of the prime-age climbers (up) or fallers (down) who had an event.</summary>
        public double ShareWith(Cause c, bool up) { int k = (int)c; return up ? (PrimeUp > 0 ? UpWith[k] / (double)PrimeUp : 0) : (PrimeDown > 0 ? DownWith[k] / (double)PrimeDown : 0); }
        /// <summary>How much more often the prime-age climbers (up) or fallers (down) had an event than every prime-age adult.</summary>
        public double Lift(Cause c, bool up)
        {
            int k = (int)c; double rate = PrimeLines > 0 ? (UpWith[k] + DownWith[k] + StayWith[k]) / (double)PrimeLines : 0;
            return rate > 0 ? ShareWith(c, up) / rate : 0;
        }

        /// <summary>The events that set climbs (up) or falls (down) apart: lift above <paramref name="minLift"/> and at
        /// least <paramref name="minShare"/> of the movers, most common first.</summary>
        public List<Cause> Leading(bool up, double minLift = 1.2, double minShare = .05, int max = 4)
        {
            var list = new List<Cause>();
            for (int c = 0; c < Causes; c++) if (Lift((Cause)c, up) >= minLift && ShareWith((Cause)c, up) >= minShare) list.Add((Cause)c);
            list.Sort((a, b) => { int r = ShareWith(b, up).CompareTo(ShareWith(a, up)); return r != 0 ? r : a.CompareTo(b); });
            if (list.Count > max) list.RemoveRange(max, list.Count - max);
            return list;
        }

        /// <summary>People who started in quintile a.</summary>
        public double StartedIn(int a) { double s = 0; for (int b = 0; b < Quintiles; b++) s += Moves[a, b]; return s; }
        /// <summary>People who ended in quintile b.</summary>
        public double EndedIn(int b) { double s = 0; for (int a = 0; a < Quintiles; a++) s += Moves[a, b]; return s; }
        /// <summary>Of those who started in quintile a, the share who ended in b.</summary>
        public double Share(int a, int b) { double s = StartedIn(a); return s > 0 ? Moves[a, b] / s : 0; }

        /// <summary>The odds (up, stay, down) from a quintile at an age band; falls back to the quintile's all-ages odds
        /// when the band has fewer than <paramref name="min"/> lines. Shares sum to 1 (or are all 0 without data).</summary>
        public (double up, double stay, double down, int lines) OddsFrom(int quintile, int band, int min = 20)
        {
            quintile = Math.Max(0, Math.Min(Quintiles - 1, quintile)); band = Math.Max(0, Math.Min(AgeBands - 1, band));
            int u = Odds[quintile, band, 0], s = Odds[quintile, band, 1], d = Odds[quintile, band, 2];
            if (u + s + d < min) { u = s = d = 0; for (int k = 0; k < AgeBands; k++) { u += Odds[quintile, k, 0]; s += Odds[quintile, k, 1]; d += Odds[quintile, k, 2]; } }
            int t = u + s + d; return t > 0 ? (u / (double)t, s / (double)t, d / (double)t, t) : (0, 0, 0, 0);
        }

        /// <summary>The quintile a decade later of the lines from a quintile at an age band, as shares of them (summing to
        /// 1, or all 0 without data), with the same fall back to every age as <see cref="OddsFrom"/>, so both rest on the
        /// same lines.</summary>
        public (double[] share, int lines) ReachedFrom(int quintile, int band, int min = 20)
        {
            quintile = Math.Max(0, Math.Min(Quintiles - 1, quintile)); band = Math.Max(0, Math.Min(AgeBands - 1, band));
            var count = new int[Quintiles]; int t = 0;
            for (int b = 0; b < Quintiles; b++) { count[b] = Reached[quintile, band, b]; t += count[b]; }
            if (t < min)
            {
                t = 0;
                for (int b = 0; b < Quintiles; b++) { count[b] = 0; for (int k = 0; k < AgeBands; k++) count[b] += Reached[quintile, k, b]; t += count[b]; }
            }
            var share = new double[Quintiles];
            for (int b = 0; b < Quintiles && t > 0; b++) share[b] = count[b] / (double)t;
            return (share, t);
        }

        /// <summary>What does not hold of the result (empty when every check passes): the matrix holds every tracked line
        /// once, the odds, the quintiles reached and the moves count the same lines, each start row's shares sum to one,
        /// the rungs hold the tracked lines once and the panel's rungs its members once, the rungs are finite.</summary>
        public List<string> Problems()
        {
            var list = new List<string>();
            double sum = 0; foreach (double v in Moves) sum += v;
            if (Math.Abs(sum - People) > 1) list.Add("the transition matrix does not hold every tracked adult once");
            int odds = 0; foreach (int v in Odds) odds += v;
            if (odds != Lines) list.Add("the odds do not count every tracked adult once");
            int reached = 0; foreach (int v in Reached) reached += v;
            if (reached != Lines) list.Add("the quintiles reached do not count every tracked adult once");
            int counted = 0; foreach (var g in Rungs) counted += g.Lines;
            if (counted != Lines) list.Add("the rungs do not hold every tracked adult once");
            double panel = 0; foreach (double v in PanelMoves) panel += v;
            int members = 0; foreach (var g in PanelRungs) members += g.Lines;
            if (Math.Abs(members * PeoplePerLine - panel) > 1) list.Add("the panel's rungs do not hold every panel member once");
            if (Up + Down + Stay != Lines) list.Add("climbs, falls and stays do not add up to the tracked adults");
            if (PrimeLines > Lines) list.Add("more prime-age adults than adults");
            for (int a = 0; a < Quintiles; a++)
            {
                if (StartedIn(a) <= 0) continue;
                double row = 0; for (int b = 0; b < Quintiles; b++) row += Share(a, b);
                if (Math.Abs(row - 1) > 1e-9) list.Add("the shares from " + QuintileNames[a] + " do not sum to one");
            }
            foreach (var rungs in new[] { Rungs, PanelRungs })
                foreach (var g in rungs)
                    if (double.IsNaN(g.SavingRate) || double.IsNaN(g.OwnedShare) || double.IsNaN(g.DebtService) || double.IsInfinity(g.Runway)) list.Add("a rung's forces are not finite");
            return list;
        }

        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;
        static string P(double v) => (100 * v).ToString("0", Ci) + "%";

        string Report()
        {
            var sb = new StringBuilder();
            sb.Append(From).Append("-").Append(Year).Append(": ").Append(Lines).Append(" adult lines at both ends (")
              .Append((People / 1e6).ToString("0.0", Ci)).Append("M people); climbed ").Append(P(Up / (double)Math.Max(1, Lines)))
              .Append(", stayed ").Append(P(Stay / (double)Math.Max(1, Lines))).Append(", fell ").Append(P(Down / (double)Math.Max(1, Lines)));
            sb.Append("; bottom fifth stayed ").Append(P(Share(0, 0))).Append(", top fifth stayed ").Append(P(Share(4, 4)))
              .Append(", bottom to top ").Append(P(Share(0, 4)));
            sb.Append("; closed panel 25+ (as the Treasury's 1996-2005: 55% / 61% / 4%): bottom stayed ").Append(P(PanelShare(0, 0)))
              .Append(", top stayed ").Append(P(PanelShare(4, 4))).Append(", bottom to top ").Append(P(PanelShare(0, 4)));
            double top1 = 0, top1Stay = WealthMoves[3, 3]; for (int b = 0; b < WealthGroups; b++) top1 += WealthMoves[3, b];
            sb.Append("; top 1% (wealth) still there ").Append(top1 > 0 ? P(top1Stay / top1) : "-");
            sb.Append("; prime age (25-54) climbed ").Append(P(PrimeUp / (double)Math.Max(1, PrimeLines))).Append(", fell ").Append(P(PrimeDown / (double)Math.Max(1, PrimeLines)));
            sb.Append("; falls came with");
            foreach (var c in Leading(false)) sb.Append(' ').Append(CauseNames[(int)c]).Append(' ').Append(P(ShareWith(c, false))).Append(" (x").Append(Lift(c, false).ToString("0.0", Ci)).Append(')');
            sb.Append("; climbs came with");
            foreach (var c in Leading(true)) sb.Append(' ').Append(CauseNames[(int)c]).Append(' ').Append(P(ShareWith(c, true))).Append(" (x").Append(Lift(c, true).ToString("0.0", Ci)).Append(')');
            // the all-adults ladder's rungs: the adults at both ends by the fifth they end in
            sb.Append("; adults at both ends by fifth, saving rate");
            for (int q = 0; q < Quintiles; q++) sb.Append(' ').Append(P(Rungs[q].SavingRate));
            sb.Append(", owned income");
            for (int q = 0; q < Quintiles; q++) sb.Append(' ').Append(P(Rungs[q].OwnedShare));
            sb.Append(", in control");
            for (int q = 0; q < Quintiles; q++) sb.Append(' ').Append(P(Rungs[q].InControl));
            // the like-for-like ladder's rungs: the panel's members by the fifth they end in
            sb.Append("; closed panel by its fifths, saving rate");
            for (int q = 0; q < Quintiles; q++) sb.Append(' ').Append(P(PanelRungs[q].SavingRate));
            sb.Append(", owned income");
            for (int q = 0; q < Quintiles; q++) sb.Append(' ').Append(P(PanelRungs[q].OwnedShare));
            sb.Append(", debt service");
            for (int q = 0; q < Quintiles; q++) sb.Append(' ').Append(P(PanelRungs[q].DebtService));
            sb.Append(", runway");
            for (int q = 0; q < Quintiles; q++) sb.Append(' ').Append(PanelRungs[q].Runway.ToString("0.0", Ci)).Append('y');
            sb.Append(", in control");
            for (int q = 0; q < Quintiles; q++) sb.Append(' ').Append(P(PanelRungs[q].InControl));
            // checks: the matrix holds every tracked line once, and the odds and the fifths reached every line once
            double sum = 0; foreach (double v in Moves) sum += v;
            int odds = 0; foreach (int v in Odds) odds += v;
            int reached = 0; foreach (int v in Reached) reached += v;
            bool ok = Math.Abs(sum - People) < 1 && odds == Lines && reached == Lines && Up + Down + Stay == Lines && PrimeLines <= Lines;
            sb.Append("; checks ").Append(ok ? "PASS" : "FAIL");
            return sb.ToString();
        }
    }
}
