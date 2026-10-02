using Why.Economy.Model;
using Why.Humans.Smv;

namespace Why.Economy.Land
{
    /// <summary>
    /// The players of a year (SPEC 3.1-3.3): every adult alive in the year by the twelve groups' priority rule
    /// (groups.json), cells of group × anchor × party merged up to the size test, children with their mother's player,
    /// the cells' money and minds, and their places (rim rows by rung packed by industry angle, plinths, controllers on
    /// towers, rentiers on the crown). Pure: no Unity objects, any thread, deterministic.
    /// </summary>
    /// <remarks>
    /// WP0 stub: returns <see cref="LandDemo.Players"/> (real shapes, a simplified census, its log tagged "(demo)").
    /// WP2 replaces the body; the signature is the contract.
    /// </remarks>
    public static class PlayerCensus
    {
        public static PlayerSet Build(EconomyModel model, SmvPopulation pop, LandGeometry land, int year) =>
            LandDemo.Players(model, pop, land, year);
    }
}
