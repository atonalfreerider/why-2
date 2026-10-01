using System;

namespace Why.Economy
{
    /// <summary>Strategies of the iterated prisoner's dilemma (see <c>Model/PrisonersDilemma.cs</c>).</summary>
    public enum PdStrategy
    {
        TitForTat = 0,
        GenerousTitForTat = 1,
        WinStayLoseShift = 2,
        AlwaysCooperate = 3,
        AlwaysDefect = 4,
        Grim = 5,
        Random = 6
    }

    /// <summary>
    /// What the viewer has chosen in the economy scene, shared by the layers that draw it and the UI that changes
    /// it (main thread). Layers poll <see cref="Version"/> in Tick and rebuild what depends on it.
    /// </summary>
    public static class EconomyState
    {
        /// <summary>Range of years the circuit and the mind map can show (calendar years).</summary>
        public const int MinYear = 1950;

        public static int MaxYear { get; private set; } = 2026;

        /// <summary>Year shown by the money circuit and the mind map.</summary>
        public static int Year { get; private set; } = 2025;

        /// <summary>The inspected person (index into the population's people), or -1.</summary>
        public static int Person { get; private set; } = -1;

        /// <summary>Settings of the games station.</summary>
        public static PdStrategy StrategyA { get; private set; } = PdStrategy.TitForTat;

        public static PdStrategy StrategyB { get; private set; } = PdStrategy.AlwaysDefect;

        /// <summary>Chance that a move comes out the opposite of what was meant (misunderstanding, error).</summary>
        public static float Noise { get; private set; } = 0.02f;

        /// <summary>The shadow of the future: chance that the pair meets again after a round.</summary>
        public static float Continuation { get; private set; } = 0.95f;

        /// <summary>Incremented on every change (layers compare it with what they built).</summary>
        public static int Version { get; private set; }

        public static event Action Changed;

        /// <summary>Called by the scene's model once the data is known (the last year with data).</summary>
        public static void SetYearRange(int maxYear)
        {
            MaxYear = Math.Max(MinYear, maxYear);
            if (Year > MaxYear) SetYear(MaxYear);
        }

        public static void SetYear(int year)
        {
            year = Math.Max(MinYear, Math.Min(MaxYear, year));
            if (year == Year) return;
            Year = year;
            Bump();
        }

        public static void SetPerson(int index)
        {
            if (index == Person) return;
            Person = index;
            Bump();
        }

        public static void SetGames(PdStrategy a, PdStrategy b, float noise, float continuation)
        {
            noise = Math.Max(0f, Math.Min(0.5f, noise));
            continuation = Math.Max(0f, Math.Min(0.999f, continuation));
            if (a == StrategyA && b == StrategyB && noise == Noise && continuation == Continuation) return;
            StrategyA = a;
            StrategyB = b;
            Noise = noise;
            Continuation = continuation;
            Bump();
        }

        /// <summary>Back to the defaults (a new scene load).</summary>
        public static void Reset()
        {
            MaxYear = 2026;
            Year = 2025;
            Person = -1;
            StrategyA = PdStrategy.TitForTat;
            StrategyB = PdStrategy.AlwaysDefect;
            Noise = 0.02f;
            Continuation = 0.95f;
            Bump();
        }

        static void Bump()
        {
            Version++;
            Changed?.Invoke();
        }
    }
}
