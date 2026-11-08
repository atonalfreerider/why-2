using System;
using UnityEngine;

namespace Why
{
    /// <summary>
    /// The flow clock: seconds that never stop while the app runs. Everything that moves without being an event of the story
    /// runs on it - the packets of money, goods, capital, people and promises (<see cref="PacketMeshBuilder"/>) and Why/Line's
    /// flow pulses - because the user asked, on 7 Nov 2026: "whenever the time is frozen on a particular instantaneous moment,
    /// I still want to see an animation of trade between companies, countries, movement of capital. It should be a continuous
    /// animation, so there isn't any static frozen moment in time." So it ignores whatever holds the story still: a frozen year,
    /// a paused replay or tour, <c>Time.timeScale</c>.
    /// <list type="bullet">
    /// <item>It advances once a frame, before any layer ticks (<see cref="Tick"/>, called by <see cref="GraphRoot"/>), by the
    /// frame's unscaled time, at most <c>Time.maximumDeltaTime</c> as Unity's own <c>Time.time</c> is: a hitch while a year
    /// rebuilds moves the packets on by a third of a second instead of throwing them along their paths.</item>
    /// <item>A capture repeats frame for frame: on the first frame the Unity Recorder's fixed step is set the clock reads 0,
    /// and n frames later exactly n / fps, however long the Editor takes to render each frame. (<c>Time.time</c>, which the
    /// pulses ran on before, stood wherever loading had left it when the recording began.)</item>
    /// <item>Shaders read it as the global <c>_WhyFlowTime</c> (WhyCommon.hlsl): <see cref="Seconds"/> modulo
    /// <see cref="WrapSeconds"/>, which a float holds to half a millisecond however long the app has run (an unbounded float
    /// clock loses that within hours). A periodic motion takes its phase from <c>WhyFlowCycles(rate)</c>, which rounds the rate
    /// to a whole number of cycles per wrap (<see cref="SeamlessRate"/>), so the wrap never shows.</item>
    /// <item><see cref="Hold"/> stops it for a check that compares frames (EconomyEmphasis.HighlightCheck) until the matching
    /// <see cref="Release"/>; holds nest.</item>
    /// </list>
    /// On the CPU, <see cref="Seconds"/> drives decorative motion that must not stop either, e.g. a marker walking its loop:
    /// <code>float lap = (float)(FlowClock.Seconds % LoopSeconds / LoopSeconds);</code>
    /// and in a shader: <code>float ph = frac(along * _FlowFreq + WhyFlowCycles(_FlowSpeed));</code>
    /// Main thread only, except <see cref="SeamlessRate"/>, which mesh builders call on workers.
    /// </summary>
    public static class FlowClock
    {
        /// <summary>
        /// The period of the shaders' copy, in seconds: 2^12, below which a float keeps seconds to 2^-11 (half a millisecond).
        /// </summary>
        public const double WrapSeconds = 4096;

        // Unity marks Shader.PropertyToID thread-safe, so it does not matter that the class may first be touched on a mesh
        // builder's worker thread (SeamlessRate)
        static readonly int FlowTimeId = Shader.PropertyToID("_WhyFlowTime");

        static int holds;
        static int lastFrame = -1;
        static bool capturing;

        // while capturing, the clock is where it stood when the capture began (or at the last Reset) plus the frames since, so
        // frame n reads exactly n / fps instead of a sum of n rounded steps
        static double frameBase;
        static long frames;

        /// <summary>Seconds on the flow clock: since the app started, or since the last capture began or <see cref="Reset"/>.</summary>
        public static double Seconds { get; private set; }

        /// <summary>The shaders' copy, <c>_WhyFlowTime</c>: <see cref="Seconds"/> modulo <see cref="WrapSeconds"/>.</summary>
        public static float Wrapped => (float)((Seconds % WrapSeconds + WrapSeconds) % WrapSeconds);

        /// <summary>
        /// Advances the clock by this frame and publishes it to the shaders. <see cref="GraphRoot"/> calls it first thing in
        /// its Update, so every layer's Tick and every material this frame see the same moment; a second call in the same frame
        /// (another root while a scene switches) does nothing, so the clock never runs double.
        /// </summary>
        public static void Tick()
        {
            if (Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;

            // the Recorder sets a fixed step for as long as it records: its first frame is the capture's zero
            float step = Time.captureDeltaTime;
            bool began = step > 0 && !capturing;
            capturing = step > 0;
            if (began) Reset();
            else if (holds == 0 && capturing)
            {
                frames++;
                Seconds = frameBase + FramesToSeconds(frames, step);
            }
            else if (holds == 0)
            {
                Seconds += Math.Min(Time.unscaledDeltaTime, Time.maximumDeltaTime);
            }

            Publish();
        }

        /// <summary>
        /// Sets the clock to <paramref name="seconds"/> and publishes it at once (a capture's first frame resets it to 0 by
        /// itself). A capture counts its frames from here.
        /// </summary>
        public static void Reset(double seconds = 0)
        {
            Seconds = seconds;
            frameBase = seconds;
            frames = 0;
            Publish();
        }

        /// <summary>Stops the clock until the matching <see cref="Release"/>: everything that moves on it holds its place.</summary>
        public static void Hold() => holds++;

        /// <summary>Releases one <see cref="Hold"/>; the clock runs on from where it stood once none is left.</summary>
        public static void Release() => holds = Math.Max(0, holds - 1);

        /// <summary>
        /// The rate nearest <paramref name="perSecond"/> (cycles a second, any sign) that repeats a whole number of times in
        /// <see cref="WrapSeconds"/>, at most 1/8192 Hz away: a motion at this rate runs on unbroken when the shaders' copy of
        /// the clock wraps. <c>WhyFlowCycles</c> rounds the same way (half up), so a rate taken through here is drawn exactly.
        /// </summary>
        public static float SeamlessRate(float perSecond) => (float)(Math.Floor(perSecond * WrapSeconds + 0.5) / WrapSeconds);

        /// <summary>
        /// Seconds in <paramref name="count"/> frames of a fixed <paramref name="step"/>: exactly count / fps when the step is a
        /// whole frame rate's (the Recorder pads its first steps by a ten-millionth of a second against capturing a frame twice,
        /// and a float holds 1/30 to about seven digits), else count steps.
        /// </summary>
        static double FramesToSeconds(long count, float step)
        {
            double fps = Math.Round(1.0 / step);
            return fps >= 1 && Math.Abs(1.0 / step - fps) < 0.01 ? count / fps : count * (double)step;
        }

        /// <summary>Hands the shaders their copy.</summary>
        static void Publish() => Shader.SetGlobalFloat(FlowTimeId, Wrapped);
    }
}
