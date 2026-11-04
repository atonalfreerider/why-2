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
    /// run. Registering a name again replaces it. Registration is safe on any thread; the rest runs on the main thread.
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

        /// <summary>"2 registered (emphasis, labels); sequences: palette": what this scene registered.</summary>
        internal static string Summary()
        {
            lock (gate)
            {
                string scene = GraphScene.Current;
                List<string> c = checks.Where(kv => kv.Value.scene == scene).Select(kv => kv.Key).OrderBy(k => k, StringComparer.Ordinal).ToList();
                List<string> s = sequences.Where(kv => kv.Value.scene == scene).Select(kv => kv.Key).OrderBy(k => k, StringComparer.Ordinal).ToList();
                return $"{c.Count} registered" + (c.Count > 0 ? $" ({string.Join(", ", c)})" : "") +
                       "; sequences: " + (s.Count > 0 ? string.Join(", ", s) : "none");
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
        static IEnumerator Guard(string name, IEnumerator body)
        {
            float deadline = Time.realtimeSinceStartup + SequenceSeconds;
            Stack<IEnumerator> stack = new Stack<IEnumerator>();
            stack.Push(body);
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
                    Debug.LogError($"[Why] smoke extra '{name}' failed: {error}");
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
                    Debug.LogError($"[Why] smoke extra '{name}': still running after {SequenceSeconds:0} s, stopped");
                    yield break;
                }
            }
        }
    }
}
