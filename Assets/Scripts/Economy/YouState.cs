using System;
using Why.Economy.Model;

namespace Why.Economy
{
    /// <summary>
    /// The viewer's own household as told to the "You" panel, shared with the view that draws it (the panel writes, the
    /// layer reads; the layers never reference the UI). Kept only in memory: nothing the viewer enters is stored or sent.
    /// </summary>
    public static class YouState
    {
        static YouModel.Inputs inputs = new YouModel.Inputs { Age = 40, Income = 85000, NetWorth = 190000, Debt = 120000, Partnered = true, Homeowner = true, Industry = -1, Saving = double.NaN };

        /// <summary>The household as entered (defaults: a median-like household until the panel seeds the year's medians).</summary>
        public static YouModel.Inputs Inputs => inputs;
        /// <summary>Incremented on every change, so the layer and the presets follow.</summary>
        public static int Version { get; private set; }
        /// <summary>False until the viewer changed something (the panel seeds the year's medians before that).</summary>
        public static bool Entered { get; private set; }
        /// <summary>The latest reading (set by whoever computed it for the shown year; null before).</summary>
        public static YouModel.Result Reading { get; set; }

        public static void Set(YouModel.Inputs value, bool entered = true)
        {
            inputs = value; Version++; if (entered) Entered = true;
        }
    }
}
