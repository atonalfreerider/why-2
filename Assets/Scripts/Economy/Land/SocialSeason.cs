using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using Why.Economy.Data;
using Why.Economy.Model;

namespace Why.Economy.Land
{
    /// <summary>
    /// A season of plain tit for tat between the players (SPEC 5.1-5.4): pairs by exposure, openings from affinity and
    /// GSS trust, 96 rounds with forgiveness from the higher OS, tribal memory from the default OS and partner choice,
    /// coalitions (CNM) at five detections, standing and signals; an optional incident (one betrayal) measured against
    /// the season without it. Deterministic expected values: no random numbers, index-order loops and tie-breaks.
    /// Pure: no Unity objects, any thread.
    /// </summary>
    /// <remarks>
    /// <para>A player is a crowd of millions, so a tie carries shares, not moves: x[p→q] is the share of p's people who
    /// cooperate with q's. Everything is computed in doubles on directed pairs (2k: A→B, 2k + 1: B→A of the undirected
    /// pair k, A &lt; B) and stored as floats per round (<see cref="SocialSeasonResult"/>).</para>
    /// <para>The members' ages (their GSS trust offsets), their reason (the higher-OS share) and the industries of the
    /// employed members (the anchor industry the exposure and affinity compare) come from the lives the players were
    /// built from (<see cref="LandService.Model"/>); without them (a stand-alone call) the player's means and anchor stand
    /// in. Everything that does not depend on the season's settings (the inputs, exposure, pairs, the affinity's parts) is
    /// computed once per player set and kept with it, so the controls and a settings change only replay the rounds.</para>
    /// <para>Reproduces <c>synth/season.py</c> (the spec's prototype) with its implicit rules made explicit: a player's
    /// pairs are its <c>pairsPerPlayer</c> highest exposures counting its own cell (as the prototype's sort does, so a
    /// player names about 9 partners and the symmetrized graph has about 8 per player); the anchor industry is the most
    /// common industry of the employed members (ties: the lower index); exposure ties go to the lower index.</para>
    /// </remarks>
    public static class SocialSeason
    {
        // ------------------------------------------------------------------ fixed rules (not parameters of games.json)

        /// <summary>Standing (5.3): anti-alphas come from the top 30% by payoff whose received-minus-given cooperation is in
        /// the top quartile; the shares of anti-alpha, alpha and omega are psyche.json's hierarchy (defaults here).</summary>
        const double AntiAlphaTop = 0.30, AntiAlphaQuartile = 0.25, AntiAlphaShare = 0.10, AlphaShare = 0.20, OmegaShare = 0.15;

        /// <summary>The betrayal's fallback tie (5.4): when no cross-party tie cooperates (mutual ≥ 0.36), a weaker one.</summary>
        const double FallbackMutual = 0.20;

        /// <summary>Bisection of θ(Y): range and steps (5.1).</summary>
        const double ThetaLo = -5, ThetaHi = 5;

        const int ThetaSteps = 60;

        /// <summary>The generations (groups.json): Silent, Boomer, Gen X, Millennial, Gen Z.</summary>
        const int Generations = 5;

        /// <summary>The checks: the openings' people-weighted mean equals p0 within this; Ec rows sum to 1 within this.</summary>
        const double CalibrationTol = 1e-3, RowTol = 1e-9;

        // ------------------------------------------------------------------ what a season is made of

        /// <summary>
        /// Everything of a year's players that the settings do not change (5.1): inputs, exposure, the pairs, the
        /// affinity without its party term and the party term's dot products, own trust, the stranger level p0.
        /// </summary>
        sealed class Base
        {
            public EconomyData Data;
            public int Year, N, M, D;
            public int[] PairA, PairB;
            public int[] From, To;                    // directed pair d: From[d] → To[d]; its reverse is d ^ 1
            public int[][] Out;                       // per player: its directed pairs, by partner index
            public int[][] Kin;                       // per player: its partners of its own majority tribe (gossip), ascending
            public double[] E0, O;                    // per directed pair: exposure (normalized per player), out-party degree
            public byte[] Class;                      // per directed pair: 1 same group; +2 co-partisan, +4 cross-partisan
            public int[] Maj, Group, Industry;        // per player: majority tribe (0 D, 1 R, 2 I), group, anchor industry
            public string[] AnchorLabel;              // per player: the anchor industry's id, else its anchor id (NMI)
            public double[] Lines, H, Own;            // per player: adult lines, higher-OS share, own trust
            public double[] ABase, TribeDot, Weight;  // per ordered pair p * N + q: affinity without the party term, τ_pᵀ M τ_q, n_p n_q / N²
            public double P0, Pol;                    // the stranger level; pol(Y) before the settings' multiplier
        }

        /// <summary>The base of a player set, kept with it (the settings' reruns and the controls replay only the rounds).</summary>
        static readonly ConditionalWeakTable<PlayerSet, Base> Bases = new ConditionalWeakTable<PlayerSet, Base>();

        /// <summary>e^(own_p + A(p, q)) of <see cref="Apply"/>, one per thread, reused (n² doubles: the large-object heap).</summary>
        [ThreadStatic] static double[] expBuffer;

        /// <summary>A season's settings applied to its base: the affinity, θ(Y), the levers, the dynamics.</summary>
        sealed class Setup
        {
            public Base B;
            public double[] A0, C0;                   // per directed pair: affinity; e^(θ + own_p + A0)
            public double[] G, Mu;                    // per player: forgiveness, tribal memory weight
            public double Theta, Pol, Eps, W, Eta, Kappa, Bound, Calibrated;
            public bool Rewire;
        }

        /// <summary>
        /// One played season: every round's cooperation and exposure (doubles), or, for a control season (<see cref="Light"/>),
        /// only the readouts each round gives (no per-round arrays).
        /// </summary>
        sealed class Play
        {
            public double[][] X;                      // [round][directed pair]; null for a control season
            public double[][] Ec;                     // [round][directed pair]; null for a control season

            /// <summary>A control season's readouts, filled round by round (null for a full season).</summary>
            public SocialSeasonResult Readouts;

            /// <summary>Σ x over rounds and directed pairs, in round then pair order (the checksum).</summary>
            public double Sum;

            /// <summary>Cooperation stayed in [0, 1] and every player's exposure summed to 1 in every round (the log's second check).</summary>
            public bool Sane = true;
        }

        // ------------------------------------------------------------------ the API

        /// <summary>
        /// The season of a year's players with the given settings; with an incident, the season in which From betrays
        /// To for one round (x[From→To] = 0 after round R's update, before partner choice), with <see cref="SocialSeasonResult.Hit"/>
        /// and <see cref="SocialSeasonResult.CalmAfter"/> measured against the same season without it.
        /// </summary>
        public static SocialSeasonResult Run(EconomyData data, LandGeometry land, PlayerSet players, SocialSettings settings,
            int year, Incident? incident = null)
        {
            SocialData sd = data?.Games?.Social ?? new SocialData();
            int rounds = Math.Max(1, sd.Rounds);
            SocialSeasonResult r = new SocialSeasonResult { Year = year, Rounds = rounds, Settings = settings, Incident = incident };
            Base b = BaseOf(data, players, year, sd);
            Setup s = Apply(b, settings, sd);
            Play play = Simulate(s, rounds, incident);
            if (incident.HasValue)
            {
                Play baseline = Simulate(s, rounds, null);
                Compare(b, play, baseline, incident.Value.Round, rounds, out r.Hit, out r.CalmAfter);
            }

            Fill(r, b, play, rounds);
            int[] detections = sd.Detections != null && sd.Detections.Length > 0 ? sd.Detections : LandStyle.Detections;
            List<int[]>[] found = Detect(r, b, play, detections, rounds);
            Standing(r, b, play, rounds, data);
            Signals(r, b, play, rounds);

            double checksum = 0;
            for (int t = 0; t <= rounds; t++)
            {
                double[] x = play.X[t];
                for (int d = 0; d < b.D; d++) checksum += x[d];
            }

            r.Checksum = checksum;
            r.Log = Log(r, s, play, found[found.Length - 1], rounds);
            return r;
        }

        /// <summary>
        /// A control season of 5.3 (log only): the same rounds as <see cref="Run"/> with the given settings, light: no
        /// coalitions, standing, signals or per-round pair arrays, only the readouts per round (<see cref="SocialSeasonResult.Cooperation"/>,
        /// <see cref="SocialSeasonResult.SameGroup"/>, <see cref="SocialSeasonResult.OtherGroup"/>,
        /// <see cref="SocialSeasonResult.CoPartisan"/>, <see cref="SocialSeasonResult.CrossPartisan"/>) and the checksum.
        /// Its numbers equal a full season's with the same settings (the same rounds, the same order of operations). Reuses
        /// the player set's base, so after the default season it only replays the rounds (about 5 ms).
        /// </summary>
        public static SocialSeasonResult Light(EconomyData data, PlayerSet players, SocialSettings settings, int year)
        {
            SocialData sd = data?.Games?.Social ?? new SocialData();
            int rounds = Math.Max(1, sd.Rounds);
            SocialSeasonResult r = new SocialSeasonResult
            {
                Year = year, Rounds = rounds, Settings = settings,
                Cooperation = new float[rounds + 1], SameGroup = new float[rounds + 1], OtherGroup = new float[rounds + 1],
                CoPartisan = new float[rounds + 1], CrossPartisan = new float[rounds + 1]
            };
            Base b = BaseOf(data, players, year, sd);
            Play play = Simulate(Apply(b, settings, sd), rounds, null, r);
            r.Checksum = play.Sum;
            r.Log = "control season: " + b.N + " players, " + b.M + " pairs; cooperation r" + rounds + " " +
                    LandFacts.Num(r.Cooperation[rounds], 3) + "; checks " + (play.Sane ? "1/1 PASS" : "0/1 FAIL") + "; checksum " +
                    play.Sum.ToString("F6", LandFacts.Ci);
            return r;
        }

        /// <summary>
        /// The betrayal the betrayal view and the tour show (5.4): in round 48, the live cross-party tie (majority D with
        /// τ_D &gt; 0.5 betraying majority R with τ_R &gt; 0.5) with mutual cooperation ≥ 0.36 at round 47 and the largest
        /// √(n_p n_q) (n: adult lines), ties by lower p, then q; without one, a D → R tie with mutual ≥ 0.20; null when the
        /// season has none. Used by LandService.Betrayal.
        /// </summary>
        public static Incident? BetrayalIncident(SocialSeasonResult baseline, PlayerSet players)
        {
            if (baseline?.PairA == null || players?.Players == null) return null;
            int round = Math.Min(LandStyle.IncidentRound, baseline.Rounds);
            int at = Math.Max(0, round - 1);
            Player[] ps = players.Players;
            (double size, int p, int q) best = (-1, -1, -1), fallback = (-1, -1, -1);
            for (int k = 0; k < baseline.PairA.Length; k++)
            {
                int a = baseline.PairA[k], b = baseline.PairB[k];
                double mutual = (double)baseline.CoopAB[at][k] * baseline.CoopBA[at][k];
                double size = Math.Sqrt((double)ps[a].Adults.Length * ps[b].Adults.Length);
                for (int dir = 0; dir < 2; dir++)
                {
                    int p = dir == 0 ? a : b, q = dir == 0 ? b : a;
                    if (Majority(ps[p]) != 0 || Majority(ps[q]) != 1) continue;
                    if (mutual >= LandStyle.CooperateMutual && ps[p].TribeShares[0] > 0.5f && ps[q].TribeShares[1] > 0.5f &&
                        Better(size, p, q, best)) best = (size, p, q);
                    if (mutual >= FallbackMutual && Better(size, p, q, fallback)) fallback = (size, p, q);
                }
            }

            (double, int p, int q) pick = best.p >= 0 ? best : fallback;
            return pick.p >= 0 ? new Incident { Round = round, From = pick.p, To = pick.q } : (Incident?)null;
        }

        /// <summary>The larger tie, or the same size and the lower (p, q): the prototype's scan order.</summary>
        static bool Better(double size, int p, int q, (double size, int p, int q) cur) =>
            cur.p < 0 || size > cur.size + 1e-9 || Math.Abs(size - cur.size) <= 1e-9 && (p < cur.p || p == cur.p && q < cur.q);

        /// <summary>A player's majority tribe: 0 D, 1 R, 2 I (the first of equal shares).</summary>
        public static int Majority(Player p)
        {
            int m = 0;
            for (int t = 1; t < 3; t++)
            {
                if (p.TribeShares[t] > p.TribeShares[m]) m = t;
            }

            return m;
        }

        // ------------------------------------------------------------------ 5.1: who meets whom (once per player set)

        /// <summary>The base of a player set: kept with it, built the first time (any thread; a race builds it twice, the same).</summary>
        static Base BaseOf(EconomyData data, PlayerSet players, int year, SocialData sd)
        {
            if (players == null) return BuildBase(data, Array.Empty<Player>(), year, sd);
            if (Bases.TryGetValue(players, out Base b) && ReferenceEquals(b.Data, data) && b.Year == year) return b;
            b = BuildBase(data, players.Players ?? Array.Empty<Player>(), year, sd);
            Bases.AddOrUpdate(players, b);
            return b;
        }

        static Base BuildBase(EconomyData data, Player[] ps, int year, SocialData sd)
        {
            int n = ps.Length;
            Base b = new Base { Data = data, Year = year, N = n };
            SocialAffinity af = sd.Affinity ?? new SocialAffinity();
            SocialExposure ex = sd.Exposure ?? new SocialExposure();

            // the players' inputs: lines, tribe and generation shares, the higher OS, age offsets, anchor industry
            double[] lines = new double[n], h = new double[n], age = new double[n];
            double[][] tau = new double[n][], gam = new double[n][];
            int[] group = new int[n], rung = new int[n], industry = new int[n];
            string[] tier = new string[n];
            EconomyModel model = LandService.Model;
            EconomicLives lives = model != null && ReferenceEquals(model.Data, data) ? model.Lives : null;
            bool records = lives != null && lives.Ready;
            int ni = data?.Industries?.Count ?? 0;
            int[] count = new int[Math.Max(1, ni)];
            for (int p = 0; p < n; p++)
            {
                Player pl = ps[p];
                lines[p] = Math.Max(1, pl.Adults.Length);
                tau[p] = new double[] { pl.TribeShares[0], pl.TribeShares[1], pl.TribeShares[2] };
                gam[p] = new double[Generations];
                for (int g = 0; g < Generations; g++) gam[p][g] = pl.Generations[g];
                group[p] = (int)pl.Group;
                rung[p] = pl.Rung;
                h[p] = pl.HigherOs;
                industry[p] = pl.Anchor;
                age[p] = AgeOffset(af, pl.Age);
                if (records && pl.Adults.Length > 0)
                {
                    // members: the mean of their GSS age offsets, the higher OS, the most common industry of the employed
                    Array.Clear(count, 0, count.Length);
                    double sum = 0, higher = 0;
                    int counted = 0;
                    foreach (int i in pl.Adults)
                    {
                        if (!lives.TryGet(i, year, out PersonYear rec)) continue;
                        counted++;
                        sum += AgeOffset(af, rec.Age);
                        higher += rec.Reason >= sd.HigherOsReason ? 1 : 0;
                        if (rec.Employed && rec.Industry >= 0 && rec.Industry < ni) count[rec.Industry]++;
                    }

                    if (counted > 0)
                    {
                        age[p] = sum / counted;
                        h[p] = higher / counted;
                        int best = -1;
                        for (int k = 0; k < ni; k++)
                        {
                            if (count[k] > 0 && (best < 0 || count[k] > count[best])) best = k;
                        }

                        industry[p] = best;
                    }
                }

                tier[p] = industry[p] >= 0 && industry[p] < ni ? data.Industries[industry[p]].TierId : null;
            }

            b.Lines = lines;
            b.H = h;
            b.Group = group;
            b.Industry = industry;
            b.AnchorLabel = new string[n];
            b.Maj = new int[n];
            for (int p = 0; p < n; p++)
            {
                b.AnchorLabel[p] = industry[p] >= 0 ? data.Industries[industry[p]].Id : ps[p].AnchorId ?? "";
                b.Maj[p] = Majority(ps[p]);
            }

            // pol(Y), trust and the stranger level p0(Y)
            b.Pol = (data?.Games?.Tribes?.AffectivePolarization?.DistrustSeries() ?? YearSeries.Empty).At(year);
            double trust = data?.Games?.Tribes?.TrustSeries().At(year) ?? 0.3;
            StrangerCooperation sc = sd.StrangerCooperation ?? new StrangerCooperation();
            b.P0 = Sigmoid(Logit(sc.Lab) + Logit(Clamp(trust)) - Logit(sc.LabEraTrust));

            // affinity A(p, q) without its party term, the party term's τ_pᵀ M τ_q, and exposure E(p, q), every ordered
            // pair (the diagonal too: the calibration and the pair choice count a player's own cell)
            double[,] tm = Matrix3(af.TribeMatrix);
            double[][] mtau = new double[n][], vgen = new double[n][];
            for (int q = 0; q < n; q++)
            {
                // M τ_q and the generation kernel applied to γ_q (+1 same, 0 adjacent, −1 farther): one dot product per pair
                mtau[q] = new double[3];
                for (int i = 0; i < 3; i++)
                {
                    for (int j = 0; j < 3; j++) mtau[q][i] += tm[i, j] * tau[q][j];
                }

                vgen[q] = new double[Generations];
                for (int i = 0; i < Generations; i++)
                {
                    for (int j = 0; j < Generations; j++)
                    {
                        int dg = Math.Abs(i - j);
                        vgen[q][i] += (dg == 0 ? 1 : dg == 1 ? 0 : -1) * gam[q][j];
                    }
                }
            }

            double total = 0;
            for (int p = 0; p < n; p++) total += lines[p];
            b.ABase = new double[n * n];
            b.TribeDot = new double[n * n];
            b.Weight = new double[n * n];
            double[] E = new double[n * n];
            for (int p = 0; p < n; p++)
            {
                for (int q = 0; q < n; q++)
                {
                    int pq = p * n + q;
                    bool same = group[p] == group[q];
                    int dr = Math.Abs(rung[p] - rung[q]);
                    b.TribeDot[pq] = tau[p][0] * mtau[q][0] + tau[p][1] * mtau[q][1] + tau[p][2] * mtau[q][2];
                    double a = same ? af.SameGroup : dr <= 1 ? af.AdjacentRung : dr >= 3 ? af.FarRung : 0;
                    double gs = 0;
                    for (int i = 0; i < Generations; i++) gs += gam[p][i] * vgen[q][i];
                    a += gs > 0 ? af.SameGeneration * gs : -af.FarGeneration * gs;
                    bool sameInd = industry[p] >= 0 && industry[p] == industry[q];
                    bool sameTier = tier[p] != null && tier[p] == tier[q];
                    if (sameInd) a += af.SameIndustry;
                    else if (sameTier) a += af.SameTier;
                    if (Role(group[p]) + Role(group[q]) == 3) a += af.OwnerWorker;   // owner (1) with worker (2)
                    b.ABase[pq] = a;
                    b.Weight[pq] = lines[p] * lines[q] / (total * total);
                    E[pq] = lines[q] * (1 + ex.SameIndustry * (sameInd ? 1 : 0) + ex.SameTier * (sameTier ? 1 : 0)) *
                            (1 + ex.SameGroup * (same ? 1 : 0)) * (1 + ex.Generation * Math.Max(gs, 0));
                }
            }

            // own trust: the members' GSS age offsets and the class slope around the people-weighted mean rung
            double rungMean = 0;
            for (int p = 0; p < n; p++) rungMean += rung[p] * lines[p];
            rungMean /= Math.Max(1, total);
            b.Own = new double[n];
            for (int p = 0; p < n; p++) b.Own[p] = age[p] + af.ClassSlope * (rung[p] - rungMean);

            // pairs: each player's highest exposures (ties: the lower index), its own cell among them, symmetrized
            int per = Math.Max(1, sd.PairsPerPlayer);
            bool[] linked = new bool[n * n], chosen = new bool[n];
            for (int p = 0; p < n; p++)
            {
                Array.Clear(chosen, 0, n);
                for (int k = 0; k < Math.Min(per, n); k++)
                {
                    int best = -1;
                    for (int q = 0; q < n; q++)
                    {
                        if (!chosen[q] && (best < 0 || E[p * n + q] > E[p * n + best])) best = q;
                    }

                    chosen[best] = true;
                    if (best != p) linked[p * n + best] = linked[best * n + p] = true;
                }
            }

            List<int> pa = new List<int>(), pb = new List<int>();
            for (int a = 0; a < n; a++)
            {
                for (int c = a + 1; c < n; c++)
                {
                    if (!linked[a * n + c]) continue;
                    pa.Add(a);
                    pb.Add(c);
                }
            }

            b.M = pa.Count;
            b.D = 2 * b.M;
            b.PairA = pa.ToArray();
            b.PairB = pb.ToArray();
            b.From = new int[b.D];
            b.To = new int[b.D];
            List<int>[] outs = new List<int>[n];
            for (int p = 0; p < n; p++) outs[p] = new List<int>();
            for (int k = 0; k < b.M; k++)
            {
                b.From[2 * k] = b.PairA[k];
                b.To[2 * k] = b.PairB[k];
                b.From[2 * k + 1] = b.PairB[k];
                b.To[2 * k + 1] = b.PairA[k];
                outs[b.PairA[k]].Add(2 * k);
                outs[b.PairB[k]].Add(2 * k + 1);
            }

            b.Out = new int[n][];
            b.Kin = new int[n][];
            for (int p = 0; p < n; p++)
            {
                outs[p].Sort((x, y) => b.To[x].CompareTo(b.To[y]));
                b.Out[p] = outs[p].ToArray();
                List<int> kin = new List<int>();
                foreach (int d in b.Out[p])
                {
                    if (b.Maj[b.To[d]] == b.Maj[p]) kin.Add(b.To[d]);
                }

                b.Kin[p] = kin.ToArray();
            }

            // E0: exposure over the pairs, normalized per player; the out-party degree; the pair's class (5.3's rows)
            b.E0 = new double[b.D];
            b.O = new double[b.D];
            b.Class = new byte[b.D];
            for (int p = 0; p < n; p++)
            {
                double sum = 0;
                foreach (int d in b.Out[p]) sum += E[p * n + b.To[d]];
                foreach (int d in b.Out[p])
                {
                    int q = b.To[d];
                    b.E0[d] = sum > 0 ? E[p * n + q] / sum : 0;
                    b.O[d] = tau[p][0] * tau[q][1] + tau[p][1] * tau[q][0];
                    int tp = b.Maj[p], tq = b.Maj[q];
                    b.Class[d] = (byte)((group[p] == group[q] ? 1 : 0) + (tp < 2 && tp == tq ? 2 : 0) + (tp < 2 && tq < 2 && tp != tq ? 4 : 0));
                }
            }

            return b;
        }

        /// <summary>A group's role: 1 owner (the 1%, business owners), 2 worker (employees and gig), 0 dependent.</summary>
        static int Role(int group) => group <= (int)Group.Owners ? 1 : group <= (int)Group.WorkingPoor ? 2 : 0;

        /// <summary>The GSS trust offset (logit) of an age: the row with the highest starting age at or below it.</summary>
        static double AgeOffset(SocialAffinity af, double age)
        {
            double[][] rows = af.AgeTrustLogit;
            if (rows == null || rows.Length == 0) return 0;
            double v = rows[0][1];
            foreach (double[] row in rows)
            {
                if (age >= row[0]) v = row[1];
            }

            return v;
        }

        static double[,] Matrix3(double[][] m)
        {
            double[,] r = { { 1, -1, -0.25 }, { -1, 1, -0.25 }, { -0.25, -0.25, 0.25 } };
            if (m == null || m.Length < 3) return r;
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3 && m[i] != null && j < m[i].Length; j++) r[i, j] = m[i][j];
            }

            return r;
        }

        static double Clamp(double v) => Math.Max(1e-3, Math.Min(1 - 1e-3, v));
        static double Logit(double p) => Math.Log(p / (1 - p));
        static double Sigmoid(double x) => 1 / (1 + Math.Exp(-x));

        // ------------------------------------------------------------------ 5.1: how they open (per settings)

        /// <summary>
        /// The settings on a base: A(p, q) with the party term at pol(Y) × the polarization setting; θ(Y) solved by
        /// bisection (60 steps) so the people-weighted mean opening over all ordered pairs equals p0; forgiveness g = q h
        /// (everyone: h = 1; nobody: g = 0) and tribal memory μ = μ0 min(1, pol) (1 − h); the dynamics.
        /// </summary>
        static Setup Apply(Base b, SocialSettings settings, SocialData sd)
        {
            int n = b.N;
            SocialAffinity af = sd.Affinity ?? new SocialAffinity();
            Setup s = new Setup { B = b, Pol = b.Pol * settings.Polarization };
            double tribe = af.Tribe * s.Pol;

            // σ(θ + z) = e^θ e^z / (1 + e^θ e^z): e^z once, then one exponential per bisection step
            if (expBuffer == null || expBuffer.Length < n * n) expBuffer = new double[n * n];
            double[] ez = expBuffer;
            for (int p = 0; p < n; p++)
            {
                for (int q = 0; q < n; q++) ez[p * n + q] = Math.Exp(b.Own[p] + b.ABase[p * n + q] + tribe * b.TribeDot[p * n + q]);
            }

            double lo = ThetaLo, hi = ThetaHi;
            for (int step = 0; step < ThetaSteps; step++)
            {
                double th = 0.5 * (lo + hi);
                if (th == lo || th == hi) break;   // the bracket cannot shrink further: the remaining steps change nothing
                if (MeanOpening(Math.Exp(th), ez, b.Weight) < b.P0) lo = th;
                else hi = th;
            }

            s.Theta = 0.5 * (lo + hi);
            s.Calibrated = MeanOpening(Math.Exp(s.Theta), ez, b.Weight);

            s.A0 = new double[b.D];
            s.C0 = new double[b.D];
            for (int d = 0; d < b.D; d++)
            {
                int pq = b.From[d] * n + b.To[d];
                s.A0[d] = b.ABase[pq] + tribe * b.TribeDot[pq];
                s.C0[d] = Math.Exp(s.Theta + b.Own[b.From[d]] + s.A0[d]);
            }

            s.G = new double[n];
            s.Mu = new double[n];
            for (int p = 0; p < n; p++)
            {
                double hp = settings.Forgive == 1 ? 1 : b.H[p];
                s.G[p] = settings.Forgive == 2 ? 0 : sd.Forgiveness * hp;
                s.Mu[p] = sd.TribalMemory * Math.Min(1, s.Pol) * (1 - hp);
            }

            s.Eps = settings.Noise;
            s.W = settings.Continuation;
            s.Rewire = settings.Rewire;
            s.Eta = sd.Learning;
            s.Kappa = sd.PartnerChoice;
            s.Bound = sd.AffinityBound;
            return s;
        }

        /// <summary>The people-weighted mean opening over all ordered pairs, from e^θ, e^(own_p + A(p, q)) and the weights.</summary>
        static double MeanOpening(double eTheta, double[] ez, double[] weight)
        {
            double sum = 0;
            for (int i = 0; i < weight.Length; i++)
            {
                double v = eTheta * ez[i];
                sum += v / (1 + v) * weight[i];
            }

            return sum;
        }

        // ------------------------------------------------------------------ 5.2: the rounds

        /// <summary>
        /// Plays the rounds (5.2): every directed pair from the last round's state, tribal tit for tat toward the other
        /// party (one hop of what one's own side received per round), forgiveness, mistakes, new dealings opening on the
        /// learned affinity; the incident; partner choice. Keeps every round's cooperation and exposure, or, given
        /// <paramref name="readouts"/> (a control season), writes only each round's readouts into it.
        /// </summary>
        static Play Simulate(Setup s, int rounds, Incident? incident, SocialSeasonResult readouts = null)
        {
            Base b = s.B;
            int n = b.N, D = b.D;
            bool keep = readouts == null;
            Play play = keep ? new Play { X = new double[rounds + 1][], Ec = new double[rounds + 1][] } : new Play { Readouts = readouts };
            double[] A = (double[])s.A0.Clone(), Ec = (double[])b.E0.Clone();

            // a new dealing's opening σ(θ + own_p + A) = c / (1 + c) with c = e^(θ + own_p + A0) e^(A − A0): the second
            // factor is also partner choice's weight (κ = 1), so a round costs one exponential per directed pair
            double[] c = (double[])s.C0.Clone(), x = new double[D], next = new double[D];
            for (int d = 0; d < D; d++) x[d] = c[d] / (1 + c[d]);
            Keep(play, b, 0, x, Ec);

            int incidentPair = -1;
            if (incident.HasValue && incident.Value.From >= 0 && incident.Value.From < n)
            {
                foreach (int d in b.Out[incident.Value.From])
                {
                    if (b.To[d] == incident.Value.To) incidentPair = d;
                }
            }

            // what each player received from each party (D, R) and what its own side received (one hop), 2 per player
            double[] received = new double[2 * n], gossip = new double[2 * n];
            bool[] hasReceived = new bool[2 * n], hasGossip = new bool[2 * n];
            for (int t = 1; t <= rounds; t++)
            {
                for (int k = 0; k < n; k++)
                {
                    double num0 = 0, den0 = 0, num1 = 0, den1 = 0;
                    foreach (int d in b.Out[k])
                    {
                        int tq = b.Maj[b.To[d]];
                        if (tq == 0)
                        {
                            num0 += b.E0[d] * x[d ^ 1];
                            den0 += b.E0[d];
                        }
                        else if (tq == 1)
                        {
                            num1 += b.E0[d] * x[d ^ 1];
                            den1 += b.E0[d];
                        }
                    }

                    hasReceived[2 * k] = den0 > 0;
                    received[2 * k] = den0 > 0 ? num0 / den0 : 0;
                    hasReceived[2 * k + 1] = den1 > 0;
                    received[2 * k + 1] = den1 > 0 ? num1 / den1 : 0;
                }

                for (int p = 0; p < n; p++)
                {
                    for (int tribe = 0; tribe < 2; tribe++)
                    {
                        double sum = 0;
                        int cnt = 0;
                        if (hasReceived[2 * p + tribe])
                        {
                            sum += received[2 * p + tribe];
                            cnt++;
                        }

                        foreach (int q in b.Kin[p])
                        {
                            if (!hasReceived[2 * q + tribe]) continue;
                            sum += received[2 * q + tribe];
                            cnt++;
                        }

                        hasGossip[2 * p + tribe] = cnt > 0;
                        gossip[2 * p + tribe] = cnt > 0 ? sum / cnt : 0;
                    }
                }

                bool sane = true;
                for (int d = 0; d < D; d++)
                {
                    int p = b.From[d];
                    double r = x[d ^ 1];                                    // what q did to p
                    int tq = b.Maj[b.To[d]];
                    if (tq < 2 && hasGossip[2 * p + tq])
                    {
                        double w = s.Mu[p] * b.O[d];
                        r = (1 - w) * r + w * gossip[2 * p + tq];
                    }

                    double tft = r + (1 - r) * s.G[p];                     // forgiving tit for tat
                    tft = (1 - s.Eps) * tft + s.Eps * (1 - tft);            // mistakes
                    double v = s.W * tft + (1 - s.W) * (c[d] / (1 + c[d]));   // new dealings open on the learned affinity
                    next[d] = v;
                    sane &= v >= 0 && v <= 1;
                }

                if (incidentPair >= 0 && t == incident.Value.Round) next[incidentPair] = 0;
                double[] swap = x;
                x = next;
                next = swap;
                if (!sane) play.Sane = false;

                if (s.Rewire)
                {
                    // partner choice: partners better than one's average gain affinity, and are met more
                    for (int p = 0; p < n; p++)
                    {
                        int[] outs = b.Out[p];
                        if (outs.Length == 0) continue;
                        double mean = 0;
                        foreach (int d in outs) mean += Ec[d] * x[d ^ 1];
                        double sum = 0;
                        foreach (int d in outs)
                        {
                            A[d] = Math.Max(s.A0[d] - s.Bound, Math.Min(s.A0[d] + s.Bound, A[d] + s.Eta * (x[d ^ 1] - mean)));
                            double u = Math.Exp(A[d] - s.A0[d]);
                            c[d] = s.C0[d] * u;
                            Ec[d] = b.E0[d] * (s.Kappa == 1 ? u : Math.Exp(s.Kappa * (A[d] - s.A0[d])));
                            sum += Ec[d];
                        }

                        double norm = 0;
                        foreach (int d in outs)
                        {
                            Ec[d] = sum > 0 ? Ec[d] / sum : 0;
                            norm += Ec[d];
                        }

                        if (!(Math.Abs(norm - 1) <= RowTol)) play.Sane = false;
                    }
                }

                Keep(play, b, t, x, Ec);
            }

            return play;
        }

        /// <summary>Stores round t (a full season) or its readouts (a control season); adds Σ x to the checksum.</summary>
        static void Keep(Play play, Base b, int t, double[] x, double[] ec)
        {
            for (int d = 0; d < b.D; d++) play.Sum += x[d];
            if (play.Readouts == null)
            {
                play.X[t] = (double[])x.Clone();
                play.Ec[t] = (double[])ec.Clone();
                return;
            }

            SocialSeasonResult r = play.Readouts;
            Classes(b, x, ec, out double all, out double same, out double other, out double co, out double cross);
            r.Cooperation[t] = (float)all;
            r.SameGroup[t] = (float)same;
            r.OtherGroup[t] = (float)other;
            r.CoPartisan[t] = (float)co;
            r.CrossPartisan[t] = (float)cross;
        }

        /// <summary>
        /// The incident's outcome against the season without it (5.4): players with any tie differing by at least
        /// <see cref="LandStyle.HitDelta"/> at any round ≥ R; the last round with any tie differing by at least
        /// <see cref="LandStyle.CalmDelta"/>, minus R.
        /// </summary>
        static void Compare(Base b, Play with, Play without, int round, int rounds, out int hit, out int calm)
        {
            bool[] touched = new bool[b.N];
            int last = round;
            for (int t = Math.Max(0, round); t <= rounds; t++)
            {
                double[] x = with.X[t], y = without.X[t];
                for (int d = 0; d < b.D; d++)
                {
                    double diff = Math.Abs(x[d] - y[d]);
                    if (diff >= LandStyle.HitDelta) touched[b.From[d]] = touched[b.To[d]] = true;
                    if (diff >= LandStyle.CalmDelta) last = t;
                }
            }

            hit = 0;
            foreach (bool v in touched) hit += v ? 1 : 0;
            calm = last - round;
        }

        // ------------------------------------------------------------------ 5.3: what the season shows

        /// <summary>
        /// The result's per-round arrays (pairs, cooperation both ways, exposure, openings) and its readouts: cooperation
        /// (the mean over players of the exposure-weighted cooperation each gives, with that round's exposure) and the same
        /// mean over the pairs of a class: same / other group, co- / cross-partisan (majorities).
        /// </summary>
        static void Fill(SocialSeasonResult r, Base b, Play play, int rounds)
        {
            r.PairA = b.PairA;
            r.PairB = b.PairB;
            r.CoopAB = new float[rounds + 1][];
            r.CoopBA = new float[rounds + 1][];
            r.Exposure = new float[rounds + 1][];
            r.Cooperation = new float[rounds + 1];
            r.SameGroup = new float[rounds + 1];
            r.OtherGroup = new float[rounds + 1];
            r.CoPartisan = new float[rounds + 1];
            r.CrossPartisan = new float[rounds + 1];
            for (int t = 0; t <= rounds; t++)
            {
                float[] ab = new float[b.M], ba = new float[b.M], ex = new float[b.M];
                double[] x = play.X[t], ec = play.Ec[t];
                for (int k = 0; k < b.M; k++)
                {
                    ab[k] = (float)x[2 * k];
                    ba[k] = (float)x[2 * k + 1];
                    ex[k] = (float)(ec[2 * k] + ec[2 * k + 1]);
                }

                r.CoopAB[t] = ab;
                r.CoopBA[t] = ba;
                r.Exposure[t] = ex;
                Classes(b, x, ec, out double all, out double same, out double other, out double co, out double cross);
                r.Cooperation[t] = (float)all;
                r.SameGroup[t] = (float)same;
                r.OtherGroup[t] = (float)other;
                r.CoPartisan[t] = (float)co;
                r.CrossPartisan[t] = (float)cross;
            }

            r.Opening = new[] { new float[b.M], new float[b.M] };
            for (int k = 0; k < b.M; k++)
            {
                r.Opening[0][k] = (float)play.X[0][2 * k];
                r.Opening[1][k] = (float)play.X[0][2 * k + 1];
            }
        }

        static void Classes(Base b, double[] x, double[] ec, out double all, out double same, out double other, out double co,
            out double cross)
        {
            double xa = 0, wa = 0, xs = 0, ws = 0, xo = 0, wo = 0, xc = 0, wc = 0, xx = 0, wx = 0;
            for (int d = 0; d < b.D; d++)
            {
                double w = ec[d], v = x[d] * w;
                xa += v;
                wa += w;
                int cls = b.Class[d];
                if ((cls & 1) != 0)
                {
                    xs += v;
                    ws += w;
                }
                else
                {
                    xo += v;
                    wo += w;
                }

                if ((cls & 2) != 0)
                {
                    xc += v;
                    wc += w;
                }
                else if ((cls & 4) != 0)
                {
                    xx += v;
                    wx += w;
                }
            }

            all = wa > 0 ? xa / wa : 0;
            same = ws > 0 ? xs / ws : all;
            other = wo > 0 ? xo / wo : all;
            co = wc > 0 ? xc / wc : all;
            cross = wx > 0 ? xx / wx : all;
        }

        /// <summary>
        /// Coalitions at each detection round: CNM on the pair graph weighted (Ec(p,q) + Ec(q,p)) x[p→q] x[q→p] (dealings ×
        /// mutual cooperation), communities of at least <see cref="LandStyle.CoalitionMinPlayers"/>, numbered across the
        /// detections (<see cref="Coalitions.Number"/>). Returns each detection's coalitions.
        /// </summary>
        static List<int[]>[] Detect(SocialSeasonResult r, Base b, Play play, int[] detections, int rounds)
        {
            List<int[]>[] found = new List<int[]>[detections.Length];
            double[] w = new double[b.M];
            for (int i = 0; i < detections.Length; i++)
            {
                int t = Math.Max(0, Math.Min(rounds, detections[i]));
                double[] x = play.X[t], ec = play.Ec[t];
                for (int k = 0; k < b.M; k++) w[k] = (ec[2 * k] + ec[2 * k + 1]) * x[2 * k] * x[2 * k + 1];
                List<int[]> all = Coalitions.Detect(b.N, b.PairA, b.PairB, w);
                found[i] = all.FindAll(c => c.Length >= LandStyle.CoalitionMinPlayers);
            }

            r.CoalitionAt = Coalitions.Number(b.N, found);
            return found;
        }

        /// <summary>
        /// Standing at the last round from each player's payoff π_p = Σ_q Ec(p,q) (R a b + S a (1 − b) + T (1 − a) b +
        /// P (1 − a)(1 − b)) with a = x[p→q], b = x[q→p] (5.3): anti-alpha = up to 10% of the players, from the top 30% by
        /// π whose received-minus-given cooperation is in the top quartile, highest π first (exploiters who prosper); alpha =
        /// the top 20% of the rest by π; omega = the bottom 15%; beta the rest (shares: psyche.json hierarchy).
        /// </summary>
        static void Standing(SocialSeasonResult r, Base b, Play play, int rounds, EconomyData data)
        {
            int n = b.N;
            PayoffData pay = data?.Games?.Payoff ?? new PayoffData();
            double[] x = play.X[rounds], ec = play.Ec[rounds];
            double[] pi = new double[n], rmg = new double[n];
            for (int d = 0; d < b.D; d++)
            {
                double a = x[d], v = x[d ^ 1];
                int p = b.From[d];
                pi[p] += ec[d] * (pay.R * a * v + pay.S * a * (1 - v) + pay.T * (1 - a) * v + pay.P * (1 - a) * (1 - v));
                rmg[p] += ec[d] * (v - a);
            }

            double anti = Share(data, "anti_alpha", AntiAlphaShare), alpha = Share(data, "alpha", AlphaShare),
                omega = Share(data, "omega", OmegaShare);
            int[] byPi = Ranked(pi), byRmg = Ranked(rmg);
            int top = (int)Math.Round(AntiAlphaTop * n, MidpointRounding.AwayFromZero);
            int quartile = (int)Math.Ceiling(AntiAlphaQuartile * n);
            bool[] inTop = new bool[n], inQuartile = new bool[n];
            for (int i = 0; i < Math.Min(top, n); i++) inTop[byPi[i]] = true;
            for (int i = 0; i < Math.Min(quartile, n); i++) inQuartile[byRmg[i]] = true;

            r.Standing = new byte[n];
            int antiMax = (int)Math.Round(anti * n, MidpointRounding.AwayFromZero), antiCount = 0;
            foreach (int p in byPi)
            {
                if (antiCount >= antiMax || !inTop[p] || !inQuartile[p]) continue;
                r.Standing[p] = 3;
                antiCount++;
            }

            List<int> rest = new List<int>(n);
            foreach (int p in byPi)
            {
                if (r.Standing[p] != 3) rest.Add(p);
            }

            int alphas = Math.Min(rest.Count, (int)Math.Round(alpha * n, MidpointRounding.AwayFromZero));
            int omegas = Math.Min(rest.Count - alphas, (int)Math.Round(omega * n, MidpointRounding.AwayFromZero));
            for (int i = 0; i < alphas; i++) r.Standing[rest[i]] = 1;
            for (int i = rest.Count - omegas; i < rest.Count; i++) r.Standing[rest[i]] = 2;
        }

        /// <summary>A rank's share of the population from psyche.json hierarchy (the default when missing).</summary>
        static double Share(EconomyData data, string id, double fallback)
        {
            if (data?.Psyche?.Hierarchy == null) return fallback;
            foreach (Drive d in data.Psyche.Hierarchy)
            {
                if (d.Id == id && d.Share > 0) return d.Share;
            }

            return fallback;
        }

        /// <summary>Indices by value, highest first (ties: the lower index).</summary>
        static int[] Ranked(double[] v)
        {
            int[] order = new int[v.Length];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            Array.Sort(order, (a, c) => v[a] != v[c] ? v[c].CompareTo(v[a]) : a.CompareTo(c));
            return order;
        }

        /// <summary>
        /// The tie events (5.3): pain when a tie's cooperation either way falls by at least <see cref="LandStyle.PainDrop"/>
        /// from the last round; relief when a feud (mutual &lt; <see cref="LandStyle.FeudMutual"/>) ends (mutual &gt;
        /// <see cref="LandStyle.ReliefEnd"/>); satisfaction when mutual ≥ <see cref="LandStyle.CooperateMutual"/> has held
        /// <see cref="LandStyle.SatisfiedRounds"/> rounds (once per stretch). In round order, then pair order.
        /// </summary>
        static void Signals(SocialSeasonResult r, Base b, Play play, int rounds)
        {
            bool[] feud = new bool[b.M];
            int[] held = new int[b.M];
            double[] x0 = play.X[0];
            for (int k = 0; k < b.M; k++)
            {
                double m = x0[2 * k] * x0[2 * k + 1];
                feud[k] = m < LandStyle.FeudMutual;
                held[k] = m >= LandStyle.CooperateMutual ? 1 : 0;
            }

            for (int t = 1; t <= rounds; t++)
            {
                double[] prev = play.X[t - 1], x = play.X[t];
                for (int k = 0; k < b.M; k++)
                {
                    double ab = x[2 * k], ba = x[2 * k + 1], m = ab * ba;
                    if (prev[2 * k] - ab >= LandStyle.PainDrop || prev[2 * k + 1] - ba >= LandStyle.PainDrop) r.Events.Add((t, k, (byte)0));
                    if (feud[k] && m > LandStyle.ReliefEnd)
                    {
                        feud[k] = false;
                        r.Events.Add((t, k, (byte)1));
                    }
                    else if (m < LandStyle.FeudMutual) feud[k] = true;

                    held[k] = m >= LandStyle.CooperateMutual ? held[k] + 1 : 0;
                    if (held[k] == LandStyle.SatisfiedRounds) r.Events.Add((t, k, (byte)2));
                }
            }
        }

        // ------------------------------------------------------------------ the log line (8.8)

        /// <summary>
        /// "116 players, 916 pairs; p0 0.308; cooperation r0 .. r96; same/other group; co/cross-partisan (and at the
        /// opening); coalitions; dealings inside; NMI; standing; checks; checksum". Checks: the openings' people-weighted
        /// mean is p0 (θ solved); cooperation stays in [0, 1] and the exposure of every player sums to 1 in every round.
        /// </summary>
        static string Log(SocialSeasonResult r, Setup s, Play play, List<int[]> final, int rounds)
        {
            Base b = s.B;
            Classes(b, play.X[0], play.Ec[0], out _, out double same0, out double other0, out double co0, out double cross0);

            // coalitions at the last detection, by size; their share of dealings inside (Σ_{p,q in c} Ec(p,q) / |c|)
            List<int[]> bySize = new List<int[]>(final);
            bySize.Sort((x, y) => x.Length != y.Length ? y.Length.CompareTo(x.Length) : x[0].CompareTo(y[0]));
            double[] ec = play.Ec[rounds];
            bool[] member = new bool[b.N];
            double insideMin = 1, insideMax = 0;
            foreach (int[] c in bySize)
            {
                Array.Clear(member, 0, member.Length);
                foreach (int p in c) member[p] = true;
                double inside = 0;
                foreach (int p in c)
                {
                    foreach (int d in b.Out[p]) inside += member[b.To[d]] ? ec[d] : 0;
                }

                inside /= c.Length;
                insideMin = Math.Min(insideMin, inside);
                insideMax = Math.Max(insideMax, inside);
            }

            // NMI over all players: a player outside a coalition is its own label
            int[] coal = new int[b.N], anchor = new int[b.N];
            Dictionary<string, int> anchorIds = new Dictionary<string, int>(StringComparer.Ordinal);
            int[] lastLabel = r.CoalitionAt[r.CoalitionAt.Length - 1];
            for (int p = 0; p < b.N; p++)
            {
                coal[p] = lastLabel[p] >= 0 ? lastLabel[p] : 100000 + p;
                if (!anchorIds.TryGetValue(b.AnchorLabel[p], out int id)) anchorIds[b.AnchorLabel[p]] = id = anchorIds.Count;
                anchor[p] = id;
            }

            int alpha = 0, omega = 0, anti = 0, beta = 0;
            foreach (byte v in r.Standing)
            {
                alpha += v == 1 ? 1 : 0;
                omega += v == 2 ? 1 : 0;
                anti += v == 3 ? 1 : 0;
                beta += v == 0 ? 1 : 0;
            }

            bool calibrated = b.N == 0 || Math.Abs(s.Calibrated - b.P0) <= CalibrationTol;
            int pass = (calibrated ? 1 : 0) + (play.Sane ? 1 : 0);
            string F(double v) => LandFacts.Num(v, 3);
            float At(float[] a, int t) => a[Math.Min(t, a.Length - 1)];
            StringBuilder sb = new StringBuilder(512);
            sb.Append(b.N).Append(" players, ").Append(b.M).Append(" pairs; p0 ").Append(F(b.P0)).Append("; cooperation r0 ")
                .Append(F(At(r.Cooperation, 0))).Append(" r4 ").Append(F(At(r.Cooperation, 4))).Append(" r12 ").Append(F(At(r.Cooperation, 12)))
                .Append(" r").Append(rounds).Append(' ').Append(F(At(r.Cooperation, rounds)))
                .Append("; same/other group ").Append(F(At(r.SameGroup, rounds))).Append('/').Append(F(At(r.OtherGroup, rounds)))
                .Append("; co/cross-partisan ").Append(F(At(r.CoPartisan, rounds))).Append('/').Append(F(At(r.CrossPartisan, rounds)))
                .Append(" (opened at same/other ").Append(F(same0)).Append('/').Append(F(other0)).Append(", co/cross ").Append(F(co0))
                .Append('/').Append(F(cross0)).Append("); coalitions ").Append(bySize.Count).Append(" (");
            for (int c = 0; c < bySize.Count; c++) sb.Append(c > 0 ? " " : "").Append(bySize[c].Length);
            sb.Append("), dealings inside ");
            if (bySize.Count > 0) sb.Append(LandFacts.Pct(insideMin)).Append('-').Append(LandFacts.Pct(insideMax)).Append('%');
            else sb.Append("n/a");
            sb.Append("; NMI anchor ").Append(LandFacts.Num(Coalitions.Nmi(coal, anchor), 2)).Append(" group ")
                .Append(LandFacts.Num(Coalitions.Nmi(coal, b.Group), 2)).Append(" tribe ").Append(LandFacts.Num(Coalitions.Nmi(coal, b.Maj), 2))
                .Append("; standing anti-alpha ").Append(anti).Append(" alpha ").Append(alpha).Append(" omega ").Append(omega)
                .Append(" beta ").Append(beta).Append("; checks ").Append(pass).Append("/2 ").Append(pass == 2 ? "PASS" : "FAIL")
                .Append("; checksum ").Append(r.Checksum.ToString("F6", LandFacts.Ci));
            return sb.ToString();
        }
    }
}
