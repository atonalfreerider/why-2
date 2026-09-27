namespace Why.Humans
{
    /// <summary>
    /// What one drawn lifeline currently stands for, published by the lifeline layers (main thread,
    /// from Tick) and shown by the HUD, e.g. "1 line = 100,000 people (United States, 1950 - now)".
    /// </summary>
    public static class HumansLod
    {
        /// <summary>People represented by one lifeline in the most visible tier; 0 = lifelines hidden.</summary>
        public static double PeoplePerLine;

        /// <summary>Where that holds, e.g. "United States 1950 - now" or "Civilizations".</summary>
        public static string Context = "";

        /// <summary>Called by a lifeline layer each frame it is the most visible one.</summary>
        public static void Publish(double peoplePerLine, string context)
        {
            PeoplePerLine = peoplePerLine;
            Context = context ?? "";
        }
    }
}
