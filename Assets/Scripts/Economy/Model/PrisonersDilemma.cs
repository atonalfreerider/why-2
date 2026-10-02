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
}
