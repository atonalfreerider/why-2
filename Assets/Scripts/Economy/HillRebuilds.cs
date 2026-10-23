using UnityEngine;
using Why.Economy.Land;

namespace Why.Economy
{
    /// <summary>
    /// Spreads the hills layers' rebuilds over frames. When a year's land comes in, every layer that draws it rebuilds;
    /// doing all of them in the frame of the swap froze the screen for a moment each year (worst in the replay, which swaps
    /// a year about every second). The landscape rebuilds in the frame the land is published and uses that frame's slot
    /// (<see cref="Use"/>); every other layer takes the next free frame (<see cref="Take"/>) and, until it has one, skips its
    /// Tick and keeps showing what it drew. A swap then costs one layer a frame for about a dozen frames. A land published
    /// blocking (a view's own year, the tour, the smoke test's views) and the first land of a scene still rebuild everything
    /// at once, so presets stay frame-exact. Main thread.
    /// </summary>
    public static class HillRebuilds
    {
        static int frame = -1;

        /// <summary>Takes this frame's rebuild: false when another layer has rebuilt in it (try again next frame).</summary>
        public static bool Take()
        {
            LandSnapshot s = LandService.Current;
            if (s == null || s.Blocking || s.Version <= 1) return true;
            int now = Time.frameCount;
            if (now == frame) return false;
            frame = now;
            return true;
        }

        /// <summary>Spends this frame's rebuild without asking (the landscape's, which every other layer waits for).</summary>
        public static void Use() => frame = Time.frameCount;
    }
}
