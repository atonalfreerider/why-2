using UnityEngine;

namespace Why
{
    /// <summary>
    /// The graph's clock: real time (unscaled), except while the Unity Recorder captures with a fixed frame rate - then
    /// every animation (camera flights, the warp, the opening, the tour's autoplay, fades) advances by the recording's
    /// step, so a capture shows exactly what a viewer sees however fast the Editor renders its frames.
    /// </summary>
    public static class GraphTime
    {
        static bool capturing;
        static float offset;

        /// <summary>Seconds since the last frame.</summary>
        public static float Delta => Time.captureDeltaTime > 0 ? Time.captureDeltaTime : Time.unscaledDeltaTime;

        /// <summary>Seconds since startup on this clock (continuous when a capture starts).</summary>
        public static float Now
        {
            get
            {
                bool c = Time.captureDeltaTime > 0;
                if (c != capturing)
                {
                    capturing = c;
                    offset = c ? Time.unscaledTime - Time.time : 0;
                }

                return c ? Time.time + offset : Time.unscaledTime;
            }
        }
    }
}
