using Why.Economy.Model;

namespace Why.Economy.Land
{
    /// <summary>
    /// The year's money on the land (SPEC 2.7's flows, 3.5, 4.1-4.6): income arcs and wage patches, named employers, the
    /// crown's payouts, saving, credit and investment, the first-recipient seller matrix (prior + RAS), rivulets, the lip
    /// canal, the falls, rivers and distributaries, taxes and imports, with the identities of 4.6 checked every build.
    /// Pure: no Unity objects, any thread, deterministic.
    /// </summary>
    /// <remarks>
    /// WP0 stub: returns <see cref="LandDemo.Money"/> (real shapes, spending landed by value-added weights, its log
    /// tagged "(demo)"). WP3 replaces the body; the signature is the contract.
    /// </remarks>
    public static class MoneyRouting
    {
        public static MoneyFlows Build(EconomyModel model, LandGeometry land, PlayerSet players, int year) =>
            LandDemo.Money(model, land, players, year);
    }
}
