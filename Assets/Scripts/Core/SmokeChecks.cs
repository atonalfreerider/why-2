using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Why
{
    /// <summary>
    /// What the smoke test checks beyond errors, registered by the code that knows the rules, so a package adds checks
    /// without editing <see cref="SmokeTest"/>. A check looks at the view on screen after each capture (every view, each
    /// state of a view with <see cref="ViewVariants"/>, every tour card and stop, the replay's frames; saved or not) and
    /// returns one string per problem; each is logged as an error, so the run exits 1. A sequence is a coroutine of a
    /// package's own captures (the palette card, the label stress test, transitions, budgets) that runs after the tour
    /// only when named with <c>-smokeextra a,b</c>. Checks and sequences belong to the scene that registers them
    /// (<see cref="GraphScene.Current"/> at the call): each scene builds its layers afresh, so the other scene's are not
    /// run. Registering a name again replaces it. A scene also names the views that must never stand still
    /// (<see cref="RegisterNeverStill"/>): after such a view's frame the screen is read over half a second of the flow clock,
    /// and the view fails when too few pixels changed (<see cref="NeverStill"/>). Registration is safe on any thread; the
    /// rest runs on the main thread.
    /// </summary>
    public static class SmokeChecks
    {
        /// <summary>The longest a sequence may run (s); past it, it is stopped and logged as an error.</summary>
        public const float SequenceSeconds = 600f;

        /// <summary>The most problems one check logs for one capture; the rest are counted in one more line.</summary>
        const int MaxProblems = 20;

        static readonly object gate = new object();

        static readonly Dictionary<string, (string scene, Func<ViewPreset, IEnumerable<string>> check)> checks =
            new Dictionary<string, (string, Func<ViewPreset, IEnumerable<string>>)>(StringComparer.Ordinal);

        static readonly Dictionary<string, (string scene, IEnumerator body)> sequences =
            new Dictionary<string, (string, IEnumerator)>(StringComparer.Ordinal);

        /// <summary>The sequences that have run, in either scene (a name requested but never run is an error).</summary>
        static readonly HashSet<string> ran = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Where frames are saved (<c>-screenshots DIR</c>; null when they are not).</summary>
        public static string ShotsDir { get; internal set; }

        /// <summary>
        /// Adds a check for this scene: after each capture it gets the view on screen and returns what is wrong with it, one
        /// string per problem (nothing when all is well). An exception counts as a problem.
        /// </summary>
        public static void Register(string name, Func<ViewPreset, IEnumerable<string>> check)
        {
            if (string.IsNullOrWhiteSpace(name) || check == null) throw new ArgumentException("A smoke check needs a name and a function.");
            lock (gate) checks[name.Trim()] = (GraphScene.Current, check);
        }

        /// <summary>
        /// Adds a sequence for this scene, run after the tour when <c>-smokeextra</c> names it (a coroutine runs once: a scene
        /// registers its own). It captures its frames with <see cref="Capture"/>; an exception in it, or in a coroutine it
        /// waits on, ends it with an error.
        /// </summary>
        public static void RegisterSequence(string name, IEnumerator sequence)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Contains(",") || sequence == null)
                throw new ArgumentException("A smoke sequence needs a name without commas (-smokeextra a,b) and a coroutine.");
            lock (gate) sequences[name.Trim()] = (GraphScene.Current, sequence);
        }

        /// <summary>
        /// Saves this frame as DIR/name.png when the player runs with <c>-screenshots DIR</c> (else does nothing) and waits
        /// until it is written, so the next view does not leak into it.
        /// </summary>
        public static IEnumerator Shot(string name)
        {
            if (ShotsDir == null) yield break;
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(ShotsDir, name + ".png"));
            yield return null;
            yield return null;
        }

        /// <summary>
        /// A capture of the view on screen: its frame (<see cref="Shot"/>), then every check of this scene on it
        /// (<see cref="Check"/>). The smoke test captures its views, tour stops and replay frames this way.
        /// </summary>
        public static IEnumerator Capture(string name, ViewPreset view)
        {
            yield return Shot(name);
            Check(name, view);
        }

        /// <summary>
        /// Runs every check of this scene on the view on screen, after the capture named: logs each problem as an error,
        /// naming the check and the capture, then one line of results. Returns the number of checks that failed.
        /// </summary>
        public static int Check(string capture, ViewPreset view)
        {
            List<KeyValuePair<string, (string scene, Func<ViewPreset, IEnumerable<string>> check)>> run;
            lock (gate)
            {
                string scene = GraphScene.Current;
                run = checks.Where(kv => kv.Value.scene == scene).OrderBy(kv => kv.Key, StringComparer.Ordinal).ToList();
            }

            List<string> failed = new List<string>();
            foreach (KeyValuePair<string, (string scene, Func<ViewPreset, IEnumerable<string>> check)> kv in run)
            {
                List<string> found;
                try
                {
                    found = (kv.Value.check(view) ?? Enumerable.Empty<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
                }
                catch (Exception e)
                {
                    found = new List<string> { "threw " + e };
                }

                if (found.Count == 0) continue;
                failed.Add(kv.Key);
                foreach (string problem in found.Take(MaxProblems)) Debug.LogError($"[Why] smoke check '{kv.Key}' failed on {capture}: {problem}");
                if (found.Count > MaxProblems) Debug.LogError($"[Why] smoke check '{kv.Key}' failed on {capture}: {found.Count - MaxProblems} more problems");
            }

            Debug.Log($"[Why] smoke checks {capture}: {run.Count - failed.Count} passed, {failed.Count} failed" +
                      (failed.Count > 0 ? $" ({string.Join(", ", failed)})" : ""));
            return failed.Count;
        }

        /// <summary>
        /// "2 registered (emphasis, labels); sequences: palette; never still: money, overview": what this scene registered (the
        /// views that must never stand still only when it named some).
        /// </summary>
        internal static string Summary()
        {
            lock (gate)
            {
                string scene = GraphScene.Current;
                List<string> c = checks.Where(kv => kv.Value.scene == scene).Select(kv => kv.Key).OrderBy(k => k, StringComparer.Ordinal).ToList();
                List<string> s = sequences.Where(kv => kv.Value.scene == scene).Select(kv => kv.Key).OrderBy(k => k, StringComparer.Ordinal).ToList();
                List<string> m = neverStill.Where(v => v.scene == scene).Select(v => v.view).OrderBy(k => k, StringComparer.Ordinal).ToList();
                return $"{c.Count} registered" + (c.Count > 0 ? $" ({string.Join(", ", c)})" : "") +
                       "; sequences: " + (s.Count > 0 ? string.Join(", ", s) : "none") +
                       (m.Count > 0 ? "; never still: " + string.Join(", ", m) : "");
            }
        }

        /// <summary>Runs, in the order given, each named sequence this scene registered, logging its start and its time.</summary>
        internal static IEnumerator RunSequences(IEnumerable<string> names)
        {
            foreach (string name in names)
            {
                IEnumerator sequence;
                lock (gate)
                {
                    if (!sequences.TryGetValue(name, out (string scene, IEnumerator body) entry) || entry.scene != GraphScene.Current) continue;
                    sequence = entry.body;
                    ran.Add(name);
                }

                Debug.Log($"[Why] smoke extra '{name}': starting");
                float start = Time.realtimeSinceStartup;
                yield return Guard(name, sequence);
                Debug.Log($"[Why] smoke extra '{name}': done in {Time.realtimeSinceStartup - start:0.0} s");
            }
        }

        /// <summary>The names among these that no scene has run (asked for at the end of the run: an unknown name).</summary>
        internal static List<string> NotRun(IEnumerable<string> names)
        {
            lock (gate) return names.Where(n => !ran.Contains(n)).ToList();
        }

        /// <summary>Every sequence registered in either scene, by name ("none" when there is none).</summary>
        internal static string SequenceNames()
        {
            lock (gate) return sequences.Count == 0 ? "none" : string.Join(", ", sequences.Keys.OrderBy(k => k, StringComparer.Ordinal));
        }

        /// <summary>
        /// Runs a package's sequence so that its failure cannot stop the smoke test (Unity ends a coroutine, and every
        /// coroutine waiting on it, at an exception). The coroutines it waits on (IEnumerators) are run here too, in place;
        /// an exception in any of them, or running past <see cref="SequenceSeconds"/>, ends the sequence with an error.
        /// Unity's own waits (frames, seconds, end of frame) pass through.
        /// </summary>
        static IEnumerator Guard(string name, IEnumerator body) => Guard($"smoke extra '{name}'", body, SequenceSeconds);

        /// <summary>
        /// <see cref="Guard(string, IEnumerator)"/> for any coroutine of the run, named <paramref name="what"/> in its errors and
        /// stopped after <paramref name="seconds"/>. Stopped early (an exception, the deadline), it disposes every coroutine
        /// still waiting in it, innermost first, so their finally blocks run: one that held the flow clock or stopped time gives
        /// it back, and the views after it move.
        /// </summary>
        static IEnumerator Guard(string what, IEnumerator body, float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            Stack<IEnumerator> stack = new Stack<IEnumerator>();
            stack.Push(body);
            try
            {
                while (stack.Count > 0)
                {
                    bool more;
                    object current = null;
                    Exception error = null;
                    try
                    {
                        more = stack.Peek().MoveNext();
                        if (more) current = stack.Peek().Current;
                    }
                    catch (Exception e)
                    {
                        more = false;
                        error = e;
                    }

                    if (error != null)
                    {
                        Debug.LogError($"[Why] {what} failed: {error}");
                        yield break;
                    }

                    if (!more)
                    {
                        stack.Pop();
                        continue;
                    }

                    if (current is IEnumerator nested)
                    {
                        stack.Push(nested);
                        continue;
                    }

                    yield return current;
                    if (Time.realtimeSinceStartup > deadline)
                    {
                        Debug.LogError($"[Why] {what}: still running after {seconds:0} s, stopped");
                        yield break;
                    }
                }
            }
            finally
            {
                // empty when the body ran to its end; else what an exception or the deadline left waiting
                while (stack.Count > 0)
                {
                    try
                    {
                        (stack.Pop() as IDisposable)?.Dispose();
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[Why] {what}: cleaning up after it stopped failed: {e}");
                    }
                }
            }
        }

        // ------------------------------------------------------------------ never still

        /// <summary>The never-still check's name in the log.</summary>
        public const string NeverStillName = "never-still";

        /// <summary>
        /// The fewest pixels that must change over half a second of the flow clock on a view that must never stand still. A
        /// packet is sized in pixels (2.5 to 5, <see cref="PacketMeshBuilder"/>), so this does not grow with the frame: a 2.5 px
        /// packet seen at the three reads changes about 30 px, so a view whose only motion is two small packets passes, and one
        /// where nothing moves reads 0.
        /// </summary>
        public const int StillMinPixels = 50;

        /// <summary>
        /// When a stretch reads the screen, in seconds from its first read (of the flow clock while it runs, of real time while
        /// it is held): three reads, not two, so a lane whose packets are back in their places after half a second (two or four
        /// a second) still shows its motion between the first two.
        /// </summary>
        static readonly float[] StillReads = { 0f, .2f, .5f };

        /// <summary>A pixel changed when one of its colour channels moved by more than this (of 255): the smoke diffs' measure
        /// (Tools/emphasis-diff.py).</summary>
        const int StillStep = 8;

        /// <summary>
        /// How long a stretch waits, past a read's moment, for the flow clock to reach it (s of real time): a clock that stands
        /// still this long is held, and a view that must never stand still fails.
        /// </summary>
        const float StallSeconds = 2f;

        /// <summary>The camera is at rest once it has not moved for <see cref="RestFrames"/> frames; waited for at most
        /// <see cref="RestSeconds"/> before each stretch the flow clock runs in.</summary>
        const int RestFrames = 3;

        const float RestSeconds = 2f;

        /// <summary>The longest one view's check may run (s), its waits for rest included, before it is stopped as an error.</summary>
        const float StillMostSeconds = 20f;

        /// <summary>The views that must never stand still, with the scene that named each.</summary>
        static readonly HashSet<(string scene, string view)> neverStill = new HashSet<(string scene, string view)>();

        /// <summary>
        /// Names a view of this scene that must never stand still (the user, 7 Nov 2026: "It should be a continuous animation,
        /// so there isn't any static frozen moment in time"): after its frame the smoke test reads it over half a second of the
        /// flow clock and fails it when fewer than <see cref="StillMinPixels"/> pixels changed (<see cref="NeverStill"/>).
        /// </summary>
        public static void RegisterNeverStill(string viewId)
        {
            if (string.IsNullOrWhiteSpace(viewId)) throw new ArgumentException("A view that must never stand still needs its id.");
            lock (gate) neverStill.Add((GraphScene.Current, viewId.Trim()));
        }

        /// <summary>Whether this scene named the view as one that must never stand still.</summary>
        internal static bool MustMove(string viewId)
        {
            if (viewId == null) return false;
            lock (gate) return neverStill.Contains((GraphScene.Current, viewId));
        }

        /// <summary>
        /// The never-still check of the view on screen, after its frame (<paramref name="capture"/> names it in the log). With
        /// the camera and the economy's state at rest, the screen is read three times over half a second of the flow clock
        /// (<see cref="StillReads"/>): an error when fewer than <see cref="StillMinPixels"/> pixels changed between the reads,
        /// a frozen moment (a flight or a new year in the reads would count as motion, so a stretch they moved in is read
        /// once more, and a warning says when both moved). Then the same stretch in real time with the flow clock held
        /// (<see cref="FlowClock.Hold"/>): what changes now moves on another clock, logged but never failed. One line of counts
        /// for every view checked: what changed while the clock ran, with it held, and only while it ran (with where on the
        /// frame). It reads the screen without saving it, so the frames the run writes are as they were, and takes about a
        /// second. An exception in it is an error and the run goes on, the clock released (<see cref="Guard(string, IEnumerator, float)"/>).
        /// </summary>
        internal static IEnumerator NeverStill(string capture) =>
            Guard($"smoke check '{NeverStillName}' on {capture}", Stillness(capture), StillMostSeconds);

        static IEnumerator Stillness(string capture)
        {
            CameraRig rig = GraphRoot.Instance != null ? GraphRoot.Instance.Rig : null;
            if (rig == null)
            {
                Debug.LogError($"[Why] smoke check '{NeverStillName}' failed on {capture}: no camera");
                yield break;
            }

            Stretch run = null;
            for (int attempt = 0; attempt < 2 && (run == null || (run.Problem == null && !run.AtRest)); attempt++)
            {
                yield return Rest(rig);
                run = new Stretch(rig, true);
                yield return run.Read();
            }

            if (run.Problem != null)
            {
                Debug.LogError($"[Why] smoke check '{NeverStillName}' failed on {capture}: {run.Problem}");
                yield break;
            }

            Stretch held = new Stretch(rig, false);
            FlowClock.Hold();
            try
            {
                yield return held.Read();
            }
            finally
            {
                FlowClock.Release();
            }

            int moved = Count(run.Moved, null, run.Width, out _);
            bool comparable = held.Problem == null && held.Moved != null && held.Moved.Length == run.Moved.Length;
            string other = held.Problem != null
                ? "with the flow clock held: not read (" + held.Problem + ")"
                : $"with the flow clock held {Count(held.Moved, null, held.Width, out _)} px changed in {held.RealSeconds:0.00} s (motion on another clock" +
                  (held.AtRest ? "" : ", the camera or the economy's state moving") + ")" +
                  (held.FlowSeconds != 0 ? $", the held clock moving {held.FlowSeconds:0.000} s" : "");
            string only = "";
            if (comparable)
            {
                int ran = Count(run.Moved, held.Moved, run.Width, out RectInt box);
                // from the frame's top left, as the frames' PNG rows run (the capture's rows run from the bottom)
                only = $"; {ran} px changed only while it ran" +
                       (ran > 0 ? $", within x {box.xMin}-{box.xMax - 1}, y {run.Height - box.yMax}-{run.Height - 1 - box.yMin} from the top left" : "");
            }

            Debug.Log($"[Why] smoke {NeverStillName} {capture}: {moved} px changed in {run.FlowSeconds:0.00} s of the flow clock " +
                      $"(at least {StillMinPixels}); {other}{only} ({run.Width}x{run.Height}, a channel by more than {StillStep})");
            if (!run.AtRest)
            {
                Debug.LogWarning($"[Why] smoke check '{NeverStillName}' on {capture}: the camera or the economy's state moved during " +
                                 "both stretches the flow clock ran in, so its count includes that motion");
            }

            if (moved < StillMinPixels)
            {
                Debug.LogError($"[Why] smoke check '{NeverStillName}' failed on {capture}: {moved} px changed in {run.FlowSeconds:0.00} s of " +
                               $"the flow clock, fewer than {StillMinPixels}: a frozen moment (the flows' packets and pulses run on FlowClock)");
            }
        }

        /// <summary>
        /// Waits until the camera, and the economy's state with it, have not moved for <see cref="RestFrames"/> frames, at most
        /// <see cref="RestSeconds"/>.
        /// </summary>
        static IEnumerator Rest(CameraRig rig)
        {
            float until = Time.realtimeSinceStartup + RestSeconds;
            int camera = rig.Version, state = StateVersion(), still = 0;
            while (still < RestFrames && Time.realtimeSinceStartup < until)
            {
                yield return null;
                bool moved = rig.Flying || rig.Version != camera || StateVersion() != state;
                camera = rig.Version;
                state = StateVersion();
                still = moved ? 0 : still + 1;
            }
        }

        /// <summary>
        /// The economy's state in the economy scene (its year, person, selection: <see cref="Economy.EconomyState.Version"/>),
        /// else 0: a change between two reads redraws the view, which is no motion of its flows.
        /// </summary>
        static int StateVersion() => GraphScene.IsEconomy ? Economy.EconomyState.Version : 0;

        /// <summary>The frame on screen as drawn, its rows from the bottom; read at the end of a frame.</summary>
        static Color32[] ReadScreen(out int width, out int height)
        {
            Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                width = shot.width;
                height = shot.height;
                return shot.GetPixels32();
            }
            finally
            {
                UnityEngine.Object.Destroy(shot);
            }
        }

        /// <summary>Marks the pixels where a colour channel of <paramref name="a"/> and <paramref name="b"/> differs by more than
        /// <see cref="StillStep"/>.</summary>
        static void Mark(Color32[] a, Color32[] b, bool[] moved)
        {
            for (int i = 0; i < a.Length; i++)
            {
                if (moved[i]) continue;
                Color32 p = a[i], q = b[i];
                moved[i] = Math.Abs(p.r - q.r) > StillStep || Math.Abs(p.g - q.g) > StillStep || Math.Abs(p.b - q.b) > StillStep;
            }
        }

        /// <summary>
        /// The pixels marked in <paramref name="moved"/> and not in <paramref name="except"/> (null: none), and their box (rows
        /// from the bottom; empty when none is).
        /// </summary>
        static int Count(bool[] moved, bool[] except, int width, out RectInt box)
        {
            int n = 0, x0 = int.MaxValue, y0 = int.MaxValue, x1 = -1, y1 = -1;
            for (int i = 0; i < moved.Length; i++)
            {
                if (!moved[i] || (except != null && except[i])) continue;
                n++;
                int x = i % width, y = i / width;
                if (x < x0) x0 = x;
                if (x > x1) x1 = x;
                if (y < y0) y0 = y;
                if (y > y1) y1 = y;
            }

            box = n > 0 ? new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1) : new RectInt(0, 0, 0, 0);
            return n;
        }

        /// <summary>
        /// The screen read at <see cref="StillReads"/> over a stretch of the flow clock (or of real time, while it is held), and
        /// the pixels that changed from one read to the next.
        /// </summary>
        sealed class Stretch
        {
            readonly CameraRig rig;
            readonly bool flow;

            /// <summary>The pixels that changed between two reads in a row (rows from the bottom), and the frame's size.</summary>
            public bool[] Moved;

            public int Width, Height;

            /// <summary>Seconds of the flow clock, and of real time, from the first read to the last.</summary>
            public double FlowSeconds;

            public float RealSeconds;

            /// <summary>Whether the camera and the economy's state stood still from the first read to the last.</summary>
            public bool AtRest;

            /// <summary>Why the stretch was not read to its end (null when it was).</summary>
            public string Problem;

            public Stretch(CameraRig rig, bool flow)
            {
                this.rig = rig;
                this.flow = flow;
            }

            public IEnumerator Read()
            {
                int camera = rig.Version, state = StateVersion();
                double flow0 = FlowClock.Seconds;
                float real0 = Time.realtimeSinceStartup;
                Color32[] last = null;
                foreach (float at in StillReads)
                {
                    while (Since(flow0, real0) < at && Time.realtimeSinceStartup - real0 < at + StallSeconds) yield return null;
                    if (Since(flow0, real0) < at)
                    {
                        Problem = $"the flow clock moved {FlowClock.Seconds - flow0:0.000} s in {Time.realtimeSinceStartup - real0:0.0} s (held, or no frames)";
                        yield break;
                    }

                    yield return new WaitForEndOfFrame();
                    Color32[] now = ReadScreen(out int width, out int height);
                    if (last == null)
                    {
                        Width = width;
                        Height = height;
                        Moved = new bool[now.Length];
                    }
                    else if (now.Length != last.Length)
                    {
                        Problem = $"the frame changed size between the reads ({Width}x{Height}, then {width}x{height})";
                        yield break;
                    }
                    else Mark(last, now, Moved);

                    last = now;
                }

                FlowSeconds = FlowClock.Seconds - flow0;
                RealSeconds = Time.realtimeSinceStartup - real0;
                AtRest = rig.Version == camera && StateVersion() == state;
            }

            /// <summary>Seconds since the first read on this stretch's clock.</summary>
            double Since(double flow0, float real0) => flow ? FlowClock.Seconds - flow0 : Time.realtimeSinceStartup - real0;
        }
    }
}
