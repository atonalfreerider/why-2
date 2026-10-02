namespace Why.Economy
{
    /// <summary>Scene discovery switch. The original torus renderers remain intact and opt out of Economy.
    /// To restore them, swap these two scene ids; the preset catalog and birth geometry follow the switch.</summary>
    public static class EconomyLayouts
    {
        public const string HillsScene = GraphScene.Economy;
        public const string BowlScene = "economy-disabled-bowl";
        public static bool UseHills => HillsScene == GraphScene.Economy;
    }
}
