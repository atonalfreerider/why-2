using System;
using System.Collections.Generic;

namespace Why.Economy.Model
{
    /// <summary>A move in the prisoner's dilemma.</summary>
    public enum PdMove : byte
    {
        Cooperate = 0,
        Defect = 1
    }

    /// <summary>
    /// The payoffs of one round (Axelrod's tournaments: T = 5, R = 3, P = 1, S = 0). A dilemma needs
    /// T &gt; R &gt; P &gt; S (defecting always pays more in one round) and 2R &gt; T + S (but taking turns
    /// exploiting each other pays less than cooperating).
    /// </summary>
    public readonly struct PdPayoff
    {
        /// <summary>Temptation (I defect, you cooperate), reward (both cooperate), punishment (both defect), sucker (I cooperate, you defect).</summary>
        public readonly float T, R, P, S;

        public PdPayoff(float t, float r, float p, float s)
        {
            T = t;
            R = r;
            P = p;
            S = s;
        }

        public static PdPayoff Axelrod => new PdPayoff(5, 3, 1, 0);

        public bool IsDilemma => T > R && R > P && P > S && 2 * R > T + S;

        /// <summary>My payoff for a pair of moves.</summary>
        public float Pay(PdMove mine, PdMove theirs) =>
            mine == PdMove.Cooperate
                ? theirs == PdMove.Cooperate ? R : S
                : theirs == PdMove.Cooperate ? T : P;

        /// <summary>
        /// Generous tit for tat's forgiveness: the largest chance of cooperating after the other defected that still
        /// keeps exploiters from profiting, q = min(1 - (T - R) / (R - S), (R - P) / (T - P)) (Nowak and Sigmund
        /// 1992); 1/3 for Axelrod's payoffs.
        /// </summary>
        public float Forgiveness => Math.Max(0f, Math.Min(1f - (T - R) / (R - S), (R - P) / (T - P)));

        /// <summary>
        /// The shadow of the future: tit for tat cannot be invaded by always defecting when the chance w of meeting
        /// again satisfies w &gt;= (T - R) / (T - P) (0.5 for Axelrod's payoffs), nor by alternating defection and
        /// cooperation when w &gt;= (T - R) / (R - S) (2/3) (Axelrod 1984).
        /// </summary>
        public float ShadowThreshold => Math.Max((T - R) / (T - P), (T - R) / (R - S));
    }

    /// <summary>One round between two players: what each did, whether it was a mistake, what each earned.</summary>
    public struct PdRound
    {
        public PdMove A, B;
        public bool ErrorA, ErrorB;
        public float PayA, PayB;

        public bool BothCooperate => A == PdMove.Cooperate && B == PdMove.Cooperate;
        public bool BothDefect => A == PdMove.Defect && B == PdMove.Defect;
    }

    /// <summary>What one player remembers between rounds (enough for every strategy here).</summary>
    public struct PdMemory
    {
        public bool Started;
        public PdMove Mine, Theirs;
        public float LastPay;
        public bool Triggered;   // grim: the other has defected once

        public void Record(PdMove mine, PdMove theirs, float pay)
        {
            Started = true;
            Mine = mine;
            Theirs = theirs;
            LastPay = pay;
            if (theirs == PdMove.Defect) Triggered = true;
        }
    }

    /// <summary>
    /// The iterated prisoner's dilemma: the strategies of <see cref="PdStrategy"/>, games between two players
    /// with mistakes (noise) and an uncertain end (the shadow of the future), and the expected payoffs between
    /// strategies. Deterministic for a given <see cref="Random"/>. Pure managed code (any thread).
    /// </summary>
    public static class PrisonersDilemma
    {
        /// <summary>Every strategy, in the order of <see cref="PdStrategy"/>.</summary>
        public static readonly PdStrategy[] All =
        {
            PdStrategy.TitForTat, PdStrategy.GenerousTitForTat, PdStrategy.WinStayLoseShift,
            PdStrategy.AlwaysCooperate, PdStrategy.AlwaysDefect, PdStrategy.Grim, PdStrategy.Random
        };

        /// <summary>The strategies that play the evolutionary game (the random player is left out).</summary>
        public static readonly PdStrategy[] Evolving =
        {
            PdStrategy.AlwaysCooperate, PdStrategy.AlwaysDefect, PdStrategy.TitForTat, PdStrategy.GenerousTitForTat,
            PdStrategy.WinStayLoseShift, PdStrategy.Grim
        };

        public static string Name(PdStrategy s)
        {
            switch (s)
            {
                case PdStrategy.TitForTat: return "Tit for tat";
                case PdStrategy.GenerousTitForTat: return "Generous tit for tat";
                case PdStrategy.WinStayLoseShift: return "Win-stay, lose-shift";
                case PdStrategy.AlwaysCooperate: return "Always cooperate";
                case PdStrategy.AlwaysDefect: return "Always defect";
                case PdStrategy.Grim: return "Grim trigger";
                default: return "Random";
            }
        }

        /// <summary>One line on how a strategy plays and which way of deciding it stands for.</summary>
        public static string Rule(PdStrategy s)
        {
            switch (s)
            {
                case PdStrategy.TitForTat: return "Cooperates first, then repeats the other's last move: nice, retaliatory, forgiving, clear.";
                case PdStrategy.GenerousTitForTat: return "Tit for tat that forgives a defection one time in three: mistakes do not start feuds.";
                case PdStrategy.WinStayLoseShift: return "Repeats a move that paid (R or T), switches after one that did not (P or S).";
                case PdStrategy.AlwaysCooperate: return "Always cooperates: kind, and easy to exploit.";
                case PdStrategy.AlwaysDefect: return "Always defects: the one-round equilibrium, played forever.";
                case PdStrategy.Grim: return "Cooperates until the other defects once, then never again: revenge without end.";
                default: return "Flips a coin every round.";
            }
        }

        /// <summary>The move a strategy means to make (before mistakes), given its memory.</summary>
        public static PdMove Intend(PdStrategy s, ref PdMemory m, PdPayoff pay, Random rng)
        {
            switch (s)
            {
                case PdStrategy.TitForTat:
                    return m.Started ? m.Theirs : PdMove.Cooperate;
                case PdStrategy.GenerousTitForTat:
                    if (!m.Started || m.Theirs == PdMove.Cooperate) return PdMove.Cooperate;
                    return rng.NextDouble() < pay.Forgiveness ? PdMove.Cooperate : PdMove.Defect;
                case PdStrategy.WinStayLoseShift:
                    if (!m.Started) return PdMove.Cooperate;
                    bool won = m.LastPay >= pay.R; // R or T
                    return won ? m.Mine : Flip(m.Mine);
                case PdStrategy.AlwaysCooperate:
                    return PdMove.Cooperate;
                case PdStrategy.AlwaysDefect:
                    return PdMove.Defect;
                case PdStrategy.Grim:
                    return m.Triggered ? PdMove.Defect : PdMove.Cooperate;
                default:
                    return rng.NextDouble() < 0.5 ? PdMove.Cooperate : PdMove.Defect;
            }
        }

        public static PdMove Flip(PdMove m) => m == PdMove.Cooperate ? PdMove.Defect : PdMove.Cooperate;

        /// <summary>
        /// One round between two players with memories: each intends a move, each move comes out the other way with
        /// chance <paramref name="noise"/>, both are paid and remember.
        /// </summary>
        public static PdRound Step(PdStrategy a, PdStrategy b, ref PdMemory ma, ref PdMemory mb, float noise, PdPayoff pay,
            Random rng)
        {
            PdMove ia = Intend(a, ref ma, pay, rng);
            PdMove ib = Intend(b, ref mb, pay, rng);
            PdRound r = new PdRound();
            r.ErrorA = noise > 0 && rng.NextDouble() < noise;
            r.ErrorB = noise > 0 && rng.NextDouble() < noise;
            r.A = r.ErrorA ? Flip(ia) : ia;
            r.B = r.ErrorB ? Flip(ib) : ib;
            r.PayA = pay.Pay(r.A, r.B);
            r.PayB = pay.Pay(r.B, r.A);
            ma.Record(r.A, r.B, r.PayA);
            mb.Record(r.B, r.A, r.PayB);
            return r;
        }

        /// <summary>A game of a fixed number of rounds.</summary>
        public static List<PdRound> Play(PdStrategy a, PdStrategy b, int rounds, float noise, PdPayoff pay, Random rng)
        {
            List<PdRound> list = new List<PdRound>(Math.Max(0, rounds));
            PdMemory ma = default, mb = default;
            for (int i = 0; i < rounds; i++) list.Add(Step(a, b, ref ma, ref mb, noise, pay, rng));
            return list;
        }

        /// <summary>
        /// How many rounds a game lasts when after every round the pair meets again with chance
        /// <paramref name="continuation"/> (a geometric number, mean 1 / (1 - w)), at most <paramref name="max"/>.
        /// </summary>
        public static int SampleRounds(float continuation, Random rng, int max)
        {
            int n = 1;
            while (n < max && rng.NextDouble() < continuation) n++;
            return n;
        }

        /// <summary>
        /// Expected payoff per round of row strategy against column strategy in games whose length follows
        /// <paramref name="continuation"/> (estimated over <paramref name="games"/> games, fixed seed): the
        /// payoff matrix of the evolutionary game.
        /// </summary>
        public static float[,] PayoffMatrix(IReadOnlyList<PdStrategy> strategies, PdPayoff pay, float continuation,
            float noise, int games, int seed)
        {
            int n = strategies.Count;
            float[,] m = new float[n, n];
            Random rng = new Random(seed);
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    double sum = 0;
                    long rounds = 0;
                    for (int g = 0; g < games; g++)
                    {
                        int len = SampleRounds(continuation, rng, 400);
                        PdMemory ma = default, mb = default;
                        for (int k = 0; k < len; k++)
                        {
                            PdRound r = Step(strategies[i], strategies[j], ref ma, ref mb, noise, pay, rng);
                            sum += r.PayA;
                        }

                        rounds += len;
                    }

                    m[i, j] = rounds > 0 ? (float)(sum / rounds) : 0f;
                }
            }

            return m;
        }

        /// <summary>Share of a strategy's own moves that cooperate against a partner, over a game (noise included).</summary>
        public static float CooperationRate(PdStrategy mine, PdStrategy theirs, int rounds, float noise, PdPayoff pay,
            Random rng)
        {
            if (rounds <= 0) return 0;
            PdMemory ma = default, mb = default;
            int c = 0;
            for (int k = 0; k < rounds; k++)
            {
                PdRound r = Step(mine, theirs, ref ma, ref mb, noise, pay, rng);
                if (r.A == PdMove.Cooperate) c++;
            }

            return c / (float)rounds;
        }
    }

    /// <summary>
    /// Two tribes repaying each other in kind. Every round each member of tribe A meets a random member of tribe B;
    /// members follow their own strategy, but what they remember of the other side is the tribe's experience
    /// (last round's share of defections against them) rather than their own: a member of a tit-for-tat tribe
    /// defects with that share as its chance. The leader (alpha) of each tribe sways its members: after the alpha
    /// defected, members defect a little more. Groups compete harder than individuals (the interindividual-
    /// intergroup discontinuity), modeled as a distrust of the other tribe that adds to every member's chance of
    /// defecting. Noise (mistakes) starts feuds; forgiveness ends them.
    /// </summary>
    public sealed class TribeGame
    {
        public struct Round
        {
            /// <summary>Share of A's moves toward B that cooperated, and of B's toward A.</summary>
            public float CoopAB, CoopBA;

            /// <summary>Average payoff per member this round.</summary>
            public float PayA, PayB;

            /// <summary>What the two leaders did.</summary>
            public PdMove AlphaA, AlphaB;
        }

        /// <summary>Extra chance that a member defects because the other side is another tribe.</summary>
        public float Distrust = 0.08f;

        /// <summary>Extra chance that members defect after their own alpha defected.</summary>
        public float AlphaSway = 0.25f;

        public readonly PdStrategy[] A, B;
        public readonly List<Round> Rounds = new List<Round>();

        readonly PdPayoff pay;
        readonly float noise;
        readonly Random rng;
        readonly PdMemory[] memA, memB;
        float grievanceA, grievanceB;   // share of defections each tribe received last round
        bool alphaADefected, alphaBDefected;

        /// <param name="a">strategies of tribe A's members; member 0 is the alpha</param>
        /// <param name="b">strategies of tribe B's members; member 0 is the alpha</param>
        public TribeGame(PdStrategy[] a, PdStrategy[] b, PdPayoff pay, float noise, int seed)
        {
            A = a;
            B = b;
            this.pay = pay;
            this.noise = noise;
            rng = new Random(seed);
            memA = new PdMemory[a.Length];
            memB = new PdMemory[b.Length];
        }

        /// <summary>
        /// The last move a member made toward the other tribe (member 0 is the alpha); false while the member has not
        /// played yet (members of tribe B are drawn at random, so some sit out a round).
        /// </summary>
        public bool TryLastMove(bool tribeA, int member, out PdMove move)
        {
            PdMemory[] m = tribeA ? memA : memB;
            bool played = member >= 0 && member < m.Length && m[member].Started;
            move = played ? m[member].Mine : PdMove.Cooperate;
            return played;
        }

        /// <summary>Plays one round and returns it (also appended to <see cref="Rounds"/>).</summary>
        public Round Step()
        {
            int n = Math.Min(A.Length, B.Length);
            int coopA = 0, coopB = 0;
            float payA = 0, payB = 0;
            PdMove alphaA = PdMove.Cooperate, alphaB = PdMove.Cooperate;
            for (int i = 0; i < n; i++)
            {
                int j = rng.Next(n);
                PdMove ma = Move(A[i], ref memA[i], grievanceA, alphaADefected && i != 0);
                PdMove mb = Move(B[j], ref memB[j], grievanceB, alphaBDefected && j != 0);
                float pa = pay.Pay(ma, mb), pb = pay.Pay(mb, ma);
                memA[i].Record(ma, mb, pa);
                memB[j].Record(mb, ma, pb);
                if (ma == PdMove.Cooperate) coopA++;
                if (mb == PdMove.Cooperate) coopB++;
                payA += pa;
                payB += pb;
                if (i == 0) alphaA = ma;
                if (j == 0) alphaB = mb;
            }

            Round r = new Round
            {
                CoopAB = n > 0 ? coopA / (float)n : 0,
                CoopBA = n > 0 ? coopB / (float)n : 0,
                PayA = n > 0 ? payA / n : 0,
                PayB = n > 0 ? payB / n : 0,
                AlphaA = alphaA,
                AlphaB = alphaB
            };

            // what each tribe will remember of the other next round
            grievanceA = 1 - r.CoopBA;
            grievanceB = 1 - r.CoopAB;
            alphaADefected = alphaA == PdMove.Defect;
            alphaBDefected = alphaB == PdMove.Defect;
            Rounds.Add(r);
            return r;
        }

        PdMove Move(PdStrategy s, ref PdMemory m, float grievance, bool sway)
        {
            PdMove intended;
            switch (s)
            {
                case PdStrategy.TitForTat:
                    intended = m.Started && rng.NextDouble() < grievance ? PdMove.Defect : PdMove.Cooperate;
                    break;
                case PdStrategy.GenerousTitForTat:
                    intended = m.Started && rng.NextDouble() < grievance * (1 - pay.Forgiveness)
                        ? PdMove.Defect
                        : PdMove.Cooperate;
                    break;
                case PdStrategy.Grim:
                    if (grievance > 0.5f) m.Triggered = true;
                    intended = m.Triggered ? PdMove.Defect : PdMove.Cooperate;
                    break;
                default:
                    intended = PrisonersDilemma.Intend(s, ref m, pay, rng);
                    break;
            }

            if (intended == PdMove.Cooperate)
            {
                float extra = Distrust + (sway ? AlphaSway : 0);
                if (s != PdStrategy.AlwaysCooperate && rng.NextDouble() < extra) intended = PdMove.Defect;
            }

            return noise > 0 && rng.NextDouble() < noise ? PrisonersDilemma.Flip(intended) : intended;
        }
    }

    /// <summary>
    /// Evolution of strategies: each generation every strategy's share grows with its payoff against the current
    /// mix (discrete replicator dynamics, x_i' = x_i f_i / f_mean), with a little mutation toward every strategy
    /// so none dies out for good. With mistakes, the classic cycle appears: cooperators are exploited by defectors,
    /// defectors are beaten by reciprocators, reciprocators drift toward the generous and the kind, and the kind
    /// let defectors back in.
    /// </summary>
    public sealed class Replicator
    {
        public readonly PdStrategy[] Strategies;
        public readonly float[,] Payoffs;
        public readonly List<float[]> Generations = new List<float[]>();

        readonly float mutation;

        public Replicator(PdStrategy[] strategies, float[,] payoffs, float[] initial, float mutation)
        {
            Strategies = strategies;
            Payoffs = payoffs;
            this.mutation = mutation;
            float[] x = new float[strategies.Length];
            float sum = 0;
            for (int i = 0; i < x.Length; i++)
            {
                x[i] = initial != null && i < initial.Length ? Math.Max(0, initial[i]) : 1;
                sum += x[i];
            }

            for (int i = 0; i < x.Length; i++) x[i] = sum > 0 ? x[i] / sum : 1f / x.Length;
            Generations.Add(x);
        }

        public float[] Current => Generations[Generations.Count - 1];

        /// <summary>Advances one generation and returns the new shares.</summary>
        public float[] Step()
        {
            float[] x = Current;
            int n = x.Length;
            float[] f = new float[n];
            float mean = 0;
            for (int i = 0; i < n; i++)
            {
                float fi = 0;
                for (int j = 0; j < n; j++) fi += Payoffs[i, j] * x[j];
                f[i] = fi;
                mean += x[i] * fi;
            }

            float[] y = new float[n];
            float sum = 0;
            for (int i = 0; i < n; i++)
            {
                float grown = mean > 1e-6f ? x[i] * f[i] / mean : x[i];
                y[i] = (1 - mutation) * grown + mutation / n;
                sum += y[i];
            }

            for (int i = 0; i < n; i++) y[i] /= sum;
            Generations.Add(y);
            return y;
        }

        /// <summary>Runs a number of generations.</summary>
        public void Run(int generations)
        {
            for (int g = 0; g < generations; g++) Step();
        }
    }
}
