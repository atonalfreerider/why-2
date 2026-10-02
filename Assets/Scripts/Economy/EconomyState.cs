using System;
using System.Globalization;
using Why.Economy.Land;

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
        /// <summary>Range of years the land can open (calendar years; before 1950 too few adults are alive for stable players).</summary>
        public const int MinYear = 1950;

        public static int MaxYear { get; private set; } = 2026;

        /// <summary>The year the cut stands at and the land shows.</summary>
        public static int Year { get; private set; } = 2025;

        /// <summary>
        /// While the viewer drags the year scrubber: the year under the pointer, where the cut slides without rebuilding
        /// the land (-1 otherwise). Releasing the scrubber sets <see cref="Year"/> and clears it. Does not bump
        /// <see cref="Version"/>: the cut reads it every frame.
        /// </summary>
        public static int PreviewYear { get; private set; } = -1;

        /// <summary>The year the cut stands at now: the preview while dragging, else <see cref="Year"/>.</summary>
        public static int CutYear => PreviewYear >= 0 ? PreviewYear : Year;

        /// <summary>The inspected person (index into the population's people), or -1.</summary>
        public static int Person { get; private set; } = -1;

        /// <summary>The selected player (PlayerSet index), tower (capture index) and tie (season pair index); -1 = none.</summary>
        public static int SelectedPlayer { get; private set; } = -1;

        public static int SelectedTower { get; private set; } = -1;

        public static int SelectedTie { get; private set; } = -1;

        /// <summary>The season's settings (the social panel; the land reruns the season when they change).</summary>
        public static SocialSettings Social { get; private set; } = SocialSettings.Default;

        /// <summary>
        /// A betrayal the viewer queued on a tie (its season pair index), to happen at the next round; -1 = none. The
        /// social layer takes it with <see cref="TakeIncident"/>.
        /// </summary>
        public static int PendingIncident { get; private set; } = -1;

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

        /// <summary>Sets or clears (-1) the scrubber's preview year, clamped to the year range.</summary>
        public static void SetPreviewYear(int year)
        {
            PreviewYear = year < 0 ? -1 : Math.Max(MinYear, Math.Min(MaxYear, year));
        }

        public static void SetPerson(int index)
        {
            if (index == Person) return;
            Person = index;
            Bump();
        }

        /// <summary>Selects a player, a tower and a tie at once (each -1 for none): the player inspector, the ownership fan.</summary>
        public static void SetSelection(int player, int tower, int tie)
        {
            if (player == SelectedPlayer && tower == SelectedTower && tie == SelectedTie) return;
            SelectedPlayer = player;
            SelectedTower = tower;
            SelectedTie = tie;
            Bump();
        }

        /// <summary>Changes the season's settings (clamped to the panel's ranges).</summary>
        public static void SetSocial(SocialSettings s)
        {
            s.Noise = Math.Max(0f, Math.Min(0.2f, s.Noise));
            s.Continuation = Math.Max(0.5f, Math.Min(0.99f, s.Continuation));
            s.Polarization = Math.Max(0f, Math.Min(2f, s.Polarization));
            s.Forgive = (byte)Math.Min(2, (int)s.Forgive);
            if (s.Noise == Social.Noise && s.Continuation == Social.Continuation && s.Polarization == Social.Polarization &&
                s.Rewire == Social.Rewire && s.Forgive == Social.Forgive) return;
            Social = s;
            Bump();
        }

        /// <summary>Queues a betrayal on a tie (season pair index) for the next round.</summary>
        public static void QueueIncident(int pair)
        {
            if (pair < 0 || pair == PendingIncident) return;
            PendingIncident = pair;
            Bump();
        }

        /// <summary>Takes the queued betrayal's pair (-1 when none) and clears it.</summary>
        public static int TakeIncident()
        {
            int pair = PendingIncident;
            if (pair < 0) return -1;
            PendingIncident = -1;
            Bump();
            return pair;
        }

        /// <summary>Back to the defaults (a new scene load), with the harness's overrides (<see cref="DefaultYear"/>, <see cref="DefaultSocial"/>).</summary>
        public static void Reset()
        {
            MaxYear = 2026;
            Year = DefaultYear(MaxYear);
            PreviewYear = -1;
            Person = -1;
            SelectedPlayer = SelectedTower = SelectedTie = -1;
            Social = DefaultSocial();
            PendingIncident = -1;
            Bump();
        }

        /// <summary>
        /// The year the land opens on: 2025, or the harness's WHY_ECON_YEAR (unset in Unity), clamped to
        /// <see cref="MinYear"/> .. maxYear. Pure: the land's model reads it on a worker before Reset runs.
        /// </summary>
        public static int DefaultYear(int maxYear)
        {
            int year = 2025;
            string env = Environment.GetEnvironmentVariable("WHY_ECON_YEAR");
            if (!string.IsNullOrWhiteSpace(env) &&
                int.TryParse(env.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int y)) year = y;
            return Math.Max(MinYear, Math.Min(Math.Max(MinYear, maxYear), year));
        }

        /// <summary>
        /// The season's settings at load: the defaults, or the harness's WHY_ECON_REWIRE (0 / 1) and WHY_ECON_FORGIVE
        /// ("os", "all", "none"); both unset in Unity.
        /// </summary>
        public static SocialSettings DefaultSocial()
        {
            SocialSettings s = SocialSettings.Default;
            string rewire = Environment.GetEnvironmentVariable("WHY_ECON_REWIRE");
            if (!string.IsNullOrWhiteSpace(rewire)) s.Rewire = rewire.Trim() != "0";
            switch (Environment.GetEnvironmentVariable("WHY_ECON_FORGIVE")?.Trim().ToLowerInvariant())
            {
                case "all": s.Forgive = 1; break;
                case "none": s.Forgive = 2; break;
                case "os": s.Forgive = 0; break;
            }

            return s;
        }

        static void Bump()
        {
            Version++;
            Changed?.Invoke();
        }
    }
}
