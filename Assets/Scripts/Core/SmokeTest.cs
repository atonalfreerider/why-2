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
    /// <c>-screenshots DIR</c> saves a frame of every view and tour stop; add <c>-xr</c> to exercise the headset
    /// presentation on a desktop (canvases on the HUD plane, the diorama rig) without a headset.
    /// </summary>
    [GraphScenes(GraphScene.Why, GraphScene.Economy)]
    public sealed class SmokeTest : GraphModule
    {
        const float ViewSeconds = 2.6f;
        const float StopSeconds = 2.8f;

        static bool enabledByArgs, hooked;
        static string shots;
        static readonly HashSet<string> visited = new HashSet<string>();
        static readonly List<string> problems = new List<string>();
        static int errors;

        public override int Order => 1000;

        public override void Init(GraphRoot root)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            enabledByArgs = args.Contains("-smoketest");
            if (!enabledByArgs)
            {
                Destroy(gameObject);
                return;
            }

            int i = System.Array.IndexOf(args, "-screenshots");
            shots = i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
            if (shots != null) Directory.CreateDirectory(shots);
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

        public override void OnLoaded(GraphRoot root) => StartCoroutine(Run(root));

        IEnumerator Run(GraphRoot root)
        {
            string scene = GraphScene.Current;
            visited.Add(scene);
            yield return new WaitForSecondsRealtime(1f);

            foreach (ViewPreset p in ViewPresets.All.ToList())
            {
                root.Focus(p, 0.6f);
                yield return new WaitForSecondsRealtime(ViewSeconds);
                Shot($"{scene}-view-{p.Id}");
            }

            DirectorMode director = root.Modules.OfType<DirectorMode>().FirstOrDefault();
            if (director != null)
            {
                director.StartTour();
                yield return new WaitForSecondsRealtime(StopSeconds);
                // stop at the end card: "next" there continues into the economy, which this test does itself
                for (int n = 0; n < 200 && director.TourRunning && !director.AtEndCard; n++)
                {
                    director.NextStep(); // completes the typewriter
                    director.NextStep(); // moves on
                    yield return new WaitForSecondsRealtime(StopSeconds);
                    Shot($"{scene}-tour-{n + 1:00}");
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

        static void Shot(string name)
        {
            if (shots == null) return;
            ScreenCapture.CaptureScreenshot(Path.Combine(shots, name + ".png"));
        }
    }
}
