using System;

namespace Why.Economy.Land
{
    /// <summary>
    /// The demo tag of the land's model logs. While the work packages built in parallel, deterministic stand-ins for the
    /// census, the money and the season lived here; each log body they wrote started with <see cref="Tag"/>, so the model
    /// layer printed its line with "(demo)" and the check script could tell a stand-in from the real model. The stand-ins
    /// were retired once PlayerCensus, MoneyRouting and SocialSeason landed; the tag stays so a log line that still
    /// carries it (a model that falls back) is printed as such.
    /// </summary>
    public static class LandDemo
    {
        /// <summary>The first word of every demo log body.</summary>
        public const string Tag = "(demo) ";

        /// <summary>Whether a builder's log comes from a stand-in.</summary>
        public static bool IsDemo(string log) => log != null && log.StartsWith(Tag, StringComparison.Ordinal);

        /// <summary>A log body without the demo tag.</summary>
        public static string Body(string log) => IsDemo(log) ? log.Substring(Tag.Length) : log ?? "";
    }
}
