using System;
using System.Collections.Generic;
using Why.Economy.Land;

namespace Why.Economy.Model
{
    /// <summary>
    /// A real iterated prisoner's dilemma between the followed person and a handful of counterparts from the same year's
    /// population: the spouse, a coworker, a neighbor (someone of the same cohort), the boss or firm (a business owner of
    /// the same industry) and someone of the other tribe. Each plays the strategy the lives model drew for them
    /// (<see cref="PersonTraits.Strategy"/>); every pair keeps its own memory (<see cref="PdMemory"/>), so tit for tat
    /// answers each partner's last move separately. Matches last <see cref="MatchRounds"/> rounds and replay
    /// identically (fixed seed, no noise): in round <see cref="SlipRound"/> the person defects once against the coworker
    /// (a slip), so the retaliation or forgiveness that follows is legible. The payoffs are games.json's (Axelrod's by
    /// default). The ledger converts the cooperation dividend over mutual defection into an illustrative sum on screen;
    /// historical records are not changed. Pure: no Unity objects.
    /// </summary>
    public sealed class DilemmaArena
    {
        public const int MatchRounds = 36, SlipRound = 8;
        /// <summary>Illustrative value of one payoff point: this share of the person's yearly disposable income.</summary>
        public const double PointValue = .0002;

        public sealed class Seat
        {
            public string Role, Note;
            public int Person = -1;
            public PdStrategy Strategy;
            /// <summary>The person's memory of this partner and the partner's memory of the person.</summary>
            public PdMemory Mine, Theirs;
            /// <summary>This match's moves (the person's, the partner's), payoffs so far and whether a move was the slip.</summary>
            public readonly List<PdMove> MyMoves = new List<PdMove>(), TheirMoves = new List<PdMove>();
            public float MyScore, TheirScore;
        }

        public readonly Seat[] Seats;
        public readonly PdStrategy Strategy;
        public readonly PdPayoff Pay;
        /// <summary>Seat the person slips against (the coworker, else the first seat).</summary>
        public readonly int SlipSeat;
        /// <summary>Rounds played in this match, matches played, all rounds played (the clock of the group level).</summary>
        public int Round { get; private set; }
        public int Matches { get; private set; }
        public int Total { get; private set; }
        /// <summary>The person's payoff points over the matches, against mutual defection (P a round with everyone).</summary>
        public double Points { get; private set; }
        public double Dividend { get; private set; }
        readonly int seed;
        Random rng;

        DilemmaArena(PdStrategy strategy, Seat[] seats, PdPayoff pay, int seed)
        {
            Strategy = strategy; Seats = seats; Pay = pay; this.seed = seed;
            SlipSeat = Math.Max(0, Array.FindIndex(seats, s => s.Role == "Coworker"));
            Reset();
        }

        /// <summary>The arena of a followed person in their last record's year (null without lives or a person).</summary>
        public static DilemmaArena Build(EconomicLives lives, PlayerSet players, Data.EconomyData data, PersonLife life)
        {
            if (lives == null || !lives.Ready || life == null || life.Traits == null) return null;
            int year = life.Year, me = life.Person, n = lives.Sim.People.Count;
            var now = life.Now; var used = new HashSet<int> { me };
            int[] playerOf = players?.PlayerOfPerson; int myPlayer = playerOf != null && me < playerOf.Length ? playerOf[me] : -1;
            int industry = life.IndustryAt(life.Records.Length - 1);
            var seats = new List<Seat>();
            // Deterministic picks: scan the population from an offset set by the person, the first match wins.
            int start = (int)((uint)(me * 7919 + year * 31) % (uint)Math.Max(1, n));
            int Find(Func<int, PersonYear, bool> ok)
            {
                for (int k = 0; k < n; k++)
                {
                    int i = (start + k) % n;
                    if (used.Contains(i) || !lives.TryGet(i, year, out PersonYear r) || !r.Adult || lives.Traits(i) == null) continue;
                    if (ok(i, r)) return i;
                }
                return -1;
            }
            void Add(string role, int person, string note)
            {
                if (person < 0) return;
                used.Add(person);
                seats.Add(new Seat { Role = role, Person = person, Strategy = lives.Traits(person).Strategy, Note = note });
            }
            int spouse = life.Spouse[life.Spouse.Length - 1];
            if (spouse >= 0 && lives.Traits(spouse) != null) Add("Spouse", spouse, "shares the household");
            else Add("Family", Find((i, r) => i == lives.Sim.People[me].Mother || i == lives.Sim.People[me].Father), "a parent");
            if (industry >= 0) Add("Coworker", Find((i, r) => r.Employed && !r.SelfEmployed && r.Industry == industry), "same industry");
            else Add("Coworker", Find((i, r) => r.Employed && Math.Abs(r.IncomeRank - now.IncomeRank) < .15f), "a work contact");
            Add("Neighbor", Find((i, r) => myPlayer >= 0 && i < playerOf.Length && playerOf[i] == myPlayer), "same cohort");
            int boss = Find((i, r) => r.SelfEmployed && r.IncomeRank > .8f && (industry < 0 || r.Industry == industry));
            if (boss < 0) boss = Find((i, r) => r.SelfEmployed && r.IncomeRank > .7f);
            Add(now.SelfEmployed ? "Client firm" : "Boss / firm", boss, industry >= 0 && data != null ? data.Industries[industry].Name : "an owner");
            byte other = now.Tribe == 0 ? (byte)1 : (byte)0;
            Add("Other tribe", Find((i, r) => r.Tribe == other), other == 0 ? "a Democrat" : "a Republican");
            if (seats.Count == 0) return null;
            return new DilemmaArena(life.Traits.Strategy, seats.ToArray(), GamesSetup.Payoff(data), me * 131 + year);
        }

        /// <summary>A new match: memories, moves and the match's scores cleared, the same random stream again.</summary>
        void Reset()
        {
            rng = new Random(seed);
            Round = 0;
            foreach (var s in Seats) { s.Mine = default; s.Theirs = default; s.MyMoves.Clear(); s.TheirMoves.Clear(); s.MyScore = s.TheirScore = 0; }
        }

        /// <summary>One round with every partner at once (a new match after <see cref="MatchRounds"/>).</summary>
        public void Step()
        {
            if (Round >= MatchRounds) { Reset(); Matches++; }
            for (int k = 0; k < Seats.Length; k++)
            {
                var s = Seats[k];
                PdMove mine = PrisonersDilemma.Intend(Strategy, ref s.Mine, Pay, rng);
                PdMove theirs = PrisonersDilemma.Intend(s.Strategy, ref s.Theirs, Pay, rng);
                if (Round == SlipRound && k == SlipSeat) mine = PdMove.Defect;   // the one-off defection
                float a = Pay.Pay(mine, theirs), b = Pay.Pay(theirs, mine);
                s.Mine.Record(mine, theirs, a); s.Theirs.Record(theirs, mine, b);
                s.MyMoves.Add(mine); s.TheirMoves.Add(theirs); s.MyScore += a; s.TheirScore += b;
                Points += a; Dividend += a - Pay.P;
            }
            Round++; Total++;
        }

        /// <summary>The person's score in this match (all partners).</summary>
        public float Score { get { float s = 0; foreach (var seat in Seats) s += seat.MyScore; return s; } }

        /// <summary>Whether a partner's move in a round repeated the person's move of the round before (tit for tat's signature).</summary>
        public static bool Copied(Seat s, int round) => round > 0 && round < s.TheirMoves.Count && s.TheirMoves[round] == s.MyMoves[round - 1];

        /// <summary>Short names of the strategies (TFT, GTFT, ...).</summary>
        public static string Short(PdStrategy s)
        {
            switch (s)
            {
                case PdStrategy.TitForTat: return "TFT";
                case PdStrategy.GenerousTitForTat: return "GTFT";
                case PdStrategy.WinStayLoseShift: return "WSLS";
                case PdStrategy.AlwaysCooperate: return "ALLC";
                case PdStrategy.AlwaysDefect: return "ALLD";
                case PdStrategy.Grim: return "Grim";
                default: return "Random";
            }
        }
    }
}
