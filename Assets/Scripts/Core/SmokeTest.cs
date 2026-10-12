using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Why.Director;
using Debug = UnityEngine.Debug;

namespace Why
{
    /// <summary>
    /// Runtime smoke test for players and CI, idle unless the player is started with <c>-smoketest</c>: once the graph is
    /// loaded it flies through every view preset, steps through every stop of the guided tour, then opens the other
    /// scene and does the same there, and quits with exit code 1 if anything logged an error or exception.
    /// <c>-screenshots DIR</c> saves a frame of every view and tour stop (<c>-smokeonly a,b</c> limits it to those views
    /// and skips the tour); add <c>-xr</c> to exercise the headset
    /// presentation on a desktop (canvases on the HUD plane, the diorama rig) without a headset.
    /// </summary>
    [GraphScenes(GraphScene.Why, GraphScene.Economy)]
    public sealed class SmokeTest : GraphModule
    {
        const float ViewSeconds = 2.6f;
        const float StopSeconds = 2.8f;

        static bool enabledByArgs, hooked;
        static string shots;
        static HashSet<string> only;
        static readonly HashSet<string> visited = new HashSet<string>();
        static readonly List<string> problems = new List<string>();
        static int errors;

        public override int Order => 1000;

        public override void Init(GraphRoot root)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            enabledByArgs = args.Contains("-smoketest") || args.Contains("-iconshot");
            if (!enabledByArgs)
            {
                Destroy(gameObject);
                return;
            }

            int i = System.Array.IndexOf(args, "-screenshots");
            shots = i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
            if (shots != null) Directory.CreateDirectory(shots);
            int o = System.Array.IndexOf(args, "-smokeonly");
            if (o >= 0 && o + 1 < args.Length) only = new HashSet<string>(args[o + 1].Split(','));
            if (!hooked)
            {
                hooked = true;
                Application.logMessageReceived += OnLog;
            }

            Debug.Log($"[Why] smoke: scene '{GraphScene.Current}' starting");
        }

        static void OnLog(string message, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            errors++;
            if (problems.Count < 40) problems.Add($"{type}: {message}\n{stack}");
        }

        public override void OnLoaded(GraphRoot root)
        {
            if (!enabledByArgs) return;
            string[] args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-iconshot");
            if (i >= 0 && i + 1 < args.Length) StartCoroutine(IconShot(root, args[i + 1]));
            else StartCoroutine(Run(root));
        }

        /// <summary>
        /// <c>-iconshot DIR</c>: the graph alone - no panels, no labels - framed on the clock of deep time, saved at twice
        /// the window's size: the source of the app's icon and splash (Tools/make-branding.sh).
        /// </summary>
        IEnumerator IconShot(GraphRoot root, string dir)
        {
            Directory.CreateDirectory(dir);
            foreach (Canvas c in FindObjectsByType<Canvas>()) c.enabled = false;
            root.Labels.gameObject.SetActive(false);
            ViewPreset overview = ViewPresets.Get("overview");
            root.Focus(overview, 0f);
            yield return new WaitForSecondsRealtime(5f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, "graph-overview.png"), 2);
            yield return new WaitForSecondsRealtime(1f);

            // closer: the clock and the branch filling the frame
            CameraPose pose = overview.Pose();
            pose.Distance *= 0.62f;
            root.Rig.FlyTo(pose, 0f);
            yield return new WaitForSecondsRealtime(3f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, "graph-close.png"), 2);
            yield return new WaitForSecondsRealtime(1f);
            Debug.Log("[Why] iconshot: saved to " + dir);
            Application.Quit(0);
        }

        IEnumerator Run(GraphRoot root)
        {
            string scene = GraphScene.Current;
            visited.Add(scene);
            yield return new WaitForSecondsRealtime(1f);

            foreach (ViewPreset p in ViewPresets.All.ToList())
            {
                if (only != null && !only.Contains(p.Id)) continue;
                root.Focus(p, 0.6f);
                yield return new WaitForSecondsRealtime(ViewSeconds);
                yield return Shot($"{scene}-view-{p.Id}");
            }

            DirectorMode director = root.Modules.OfType<DirectorMode>().FirstOrDefault();
            if (director != null && only == null)
            {
                director.StartTour();
                yield return new WaitForSecondsRealtime(StopSeconds);
                // stop at the end card: "next" there continues into the economy, which this test does itself
                for (int n = 0; n < 200 && director.TourRunning && !director.AtEndCard; n++)
                {
                    director.NextStep(); // completes the typewriter
                    director.NextStep(); // moves on
                    yield return new WaitForSecondsRealtime(StopSeconds);
                    yield return Shot($"{scene}-tour-{n + 1:00}");
                }

                director.ExitTour();
            }

            Debug.Log($"[Why] smoke: scene '{scene}' done, {errors} errors so far");
            string other = scene == GraphScene.Economy ? GraphScene.Why : GraphScene.Economy;
            if (!visited.Contains(other))
            {
                GraphRoot.OpenOtherScene();
                yield break;
            }

            foreach (string p in problems) Debug.Log("[Why] smoke problem: " + p);
            Debug.Log($"[Why] smoke: finished, {errors} errors");
            Application.Quit(errors > 0 ? 1 : 0);
        }

        /// <summary>Captures this frame and waits until it is written, so the next view does not leak into it.</summary>
        static IEnumerator Shot(string name)
        {
            if (shots == null) yield break;
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(shots, name + ".png"));
            yield return null;
            yield return null;
        }
    }
}
