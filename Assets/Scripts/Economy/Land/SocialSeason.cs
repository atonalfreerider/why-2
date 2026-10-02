using Why.Economy.Data;

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
    /// WP0 stub: returns <see cref="LandDemo.Season"/> (real shapes, an eased climb to the prototype's levels, its log
    /// tagged "(demo)"). WP4 replaces the bodies; the signatures are the contract.
    /// </remarks>
    public static class SocialSeason
    {
        public static SocialSeasonResult Run(EconomyData data, LandGeometry land, PlayerSet players, SocialSettings settings,
            int year, Incident? incident = null) =>
            LandDemo.Season(data, land, players, settings, year, incident);

        /// <summary>
        /// The betrayal the betrayal view and the tour show (5.4): in round 48, the live cross-party tie (majority D with
        /// τ_D &gt; 0.5 betraying majority R with τ_R &gt; 0.5) with mutual cooperation ≥ 0.36 at round 47 and the largest
        /// √(n_p n_q), ties by lower p, then q; null when the season has none. Used by LandService.Betrayal.
        /// </summary>
        public static Incident? BetrayalIncident(SocialSeasonResult baseline, PlayerSet players) =>
            LandDemo.DefaultIncident(baseline, players);
    }
}
