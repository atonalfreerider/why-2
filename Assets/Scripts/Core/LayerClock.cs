using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Why
{
    /// <summary>
    /// Main-thread time by layer, for diagnostics (the smoke test's replay): while <see cref="On"/>, <see cref="GraphRoot"/>
    /// times every layer's Tick and code can time a section of its own (<see cref="Measure"/>). A frame that took longer
    /// than <see cref="LongFrameMs"/> adds what each layer and section spent in it to their totals; <see cref="Report"/>
    /// lists the costliest. Off, it costs one branch per layer per frame.
    /// </summary>
    public static class LayerClock
    {
        /// <summary>A frame at least this long (ms, wall clock) counts as long.</summary>
        public const double LongFrameMs = 50;

        static readonly double MsPerTick = 1000.0 / Stopwatch.Frequency;
        static readonly Dictionary<string, double> frame = new Dictionary<string, double>(StringComparer.Ordinal);
        static readonly Dictionary<string, (double total, double max, int frames)> slow = new Dictionary<string, (double, double, int)>(StringComparer.Ordinal);
        static long frameStart;
        static int longFrames;
        static double longest, longTotal;

        /// <summary>Whether layers and sections are timed.</summary>
        public static bool On { get; private set; }

        /// <summary>Starts timing (forgets earlier totals).</summary>
        public static void Start()
        {
            frame.Clear();
            slow.Clear();
            longFrames = 0;
            longest = longTotal = 0;
            frameStart = Stopwatch.GetTimestamp();
            On = true;
        }

        public static void Stop() => On = false;

        /// <summary>Stops timing for a while (e.g. around a screenshot) without forgetting the totals.</summary>
        public static void Pause() => On = false;

        /// <summary>Times again after <see cref="Pause"/>; the paused stretch does not count as a frame.</summary>
        public static void Resume()
        {
            frame.Clear();
            layerTicks = 0;
            frameStart = Stopwatch.GetTimestamp();
            On = true;
        }

        /// <summary>Adds main-thread time (Stopwatch ticks) to a layer or section in this frame.</summary>
        public static void Add(string name, long ticks)
        {
            if (!On) return;
            frame.TryGetValue(name, out double ms);
            frame[name] = ms + ticks * MsPerTick;
        }

        /// <summary>Adds a layer's Tick (GraphRoot): like <see cref="Add"/>, and counted toward the frame's layer total, so a
        /// long frame also reports the time spent outside every layer (rendering, text meshes, other scripts, collection).</summary>
        public static void AddLayer(string name, long ticks)
        {
            if (!On) return;
            Add(name, ticks);
            layerTicks += ticks;
        }

        static long layerTicks;

        /// <summary>Times a section until disposed: <c>using (LayerClock.Measure("wave: mesh")) { ... }</c>.</summary>
        public static Section Measure(string name) => new Section(On ? name : null);

        /// <summary>Closes a frame (GraphRoot, after every layer ticked): a long frame adds its times to the totals.</summary>
        public static void EndFrame()
        {
            if (!On) return;
            long now = Stopwatch.GetTimestamp();
            // the previous frame's wall time: from this point last frame to this point now
            double wall = (now - frameStart) * MsPerTick;
            frameStart = now;
            if (wall >= LongFrameMs)
            {
                longFrames++;
                longTotal += wall;
                longest = Math.Max(longest, wall);
                frame["(outside layers)"] = Math.Max(0, wall - layerTicks * MsPerTick);
                foreach (KeyValuePair<string, double> kv in frame)
                {
                    slow.TryGetValue(kv.Key, out (double total, double max, int frames) s);
                    slow[kv.Key] = (s.total + kv.Value, Math.Max(s.max, kv.Value), s.frames + 1);
                }
            }

            frame.Clear();
            layerTicks = 0;
        }

        /// <summary>"12 long frames (≥ 50 ms), 1.9 s in all, longest 420 ms; in them: HillWaveLayer 610 ms (max 120) ..."</summary>
        public static string Report(int top = 14)
        {
            StringBuilder b = new StringBuilder();
            b.Append($"{longFrames} long frames (>= {LongFrameMs:0} ms), {longTotal / 1000:0.00} s in all, longest {longest:0} ms");
            if (slow.Count == 0) return b.ToString();
            b.Append("; in them:");
            foreach (KeyValuePair<string, (double total, double max, int frames)> kv in slow.OrderByDescending(kv => kv.Value.total).Take(top))
            {
                b.Append($" {kv.Key} {kv.Value.total:0} ms (max {kv.Value.max:0}, {kv.Value.frames} frames);");
            }

            return b.ToString();
        }

        /// <summary>A timed section (see <see cref="Measure"/>).</summary>
        public readonly struct Section : IDisposable
        {
            readonly string name;
            readonly long start;

            public Section(string name)
            {
                this.name = name;
                start = name != null ? Stopwatch.GetTimestamp() : 0;
            }

            public void Dispose()
            {
                if (name != null) Add(name, Stopwatch.GetTimestamp() - start);
            }
        }
    }
}
