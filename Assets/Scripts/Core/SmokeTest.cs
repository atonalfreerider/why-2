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
    /// and skips the tour, unless <c>-smoketour</c> asks for it too; <c>-smokelayers</c> re-captures each view with every
    /// layer hidden in turn, to find which layer draws what; a view with states of its own, <see cref="ViewVariants"/>, is
    /// captured in each); <c>-smokereplay</c> also plays the economy's replay of the
    /// wave from 1950 to the latest year through the overview, landscape, money and mobility views, with a frame at a few
    /// years, failing if it stalls, and logs the layers that cost the most in its long frames (<see cref="LayerClock"/>);
    /// add <c>-xr</c> to exercise the headset
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
        static int zooms;
        static readonly HashSet<string> visited = new HashSet<string>();
        static readonly List<string> problems = new List<string>();
        static int errors;

        public override int Order => 1000;

        public override void Init(GraphRoot root)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            enabledByArgs = args.Contains("-smoketest") || args.Contains("-iconshot") || args.Contains("-smokegenesis");
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
            int zi = System.Array.IndexOf(args, "-smokezoom");
            if (zi >= 0 && zi + 1 < args.Length) int.TryParse(args[zi + 1], out zooms);
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
            else if (args.Contains("-smokegenesis")) StartCoroutine(GenesisShots());
            else StartCoroutine(Run(root));
        }

        /// <summary><c>-smokegenesis [-screenshots DIR]</c>: frames of the opening as it plays, then quits.</summary>
        IEnumerator GenesisShots()
        {
            float[] at = { 0.25f, 0.6f, 1.2f, 2.5f, 4f, 5.5f, 7f, 8.5f, 10f, 12f };
            float t = 0;
            foreach (float s in at)
            {
                yield return new WaitForSecondsRealtime(s - t);
                t = s;
                yield return Shot($"genesis-{s:00.00}s");
            }

            Debug.Log($"[Why] smoke: genesis done, {errors} errors");
            Application.Quit(errors > 0 ? 1 : 0);
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
                // a view with states of its own (ViewVariants): each state in turn, then back to the default
                if (ViewVariants.TryGet(p.Id, out ViewVariants.Entry variants))
                {
                    for (int v = 0; v < variants.Count(); v++)
                    {
                        variants.Show(v);
                        yield return new WaitForSecondsRealtime(1.2f);
                        yield return Shot($"{scene}-view-{p.Id}-{variants.Name(v)}");
                    }

                    variants.Show(-1);
                }
                // -smokelayers: the view again with each layer hidden in turn, to find which layer draws what
                if (System.Environment.GetCommandLineArgs().Contains("-smokelayers") && shots != null)
                    foreach (GraphLayer layer in root.Layers.ToList())
                    {
                        if (layer == null || !layer.gameObject.activeSelf) continue;
                        layer.gameObject.SetActive(false);
                        yield return new WaitForSecondsRealtime(0.3f);
                        yield return Shot($"{scene}-view-{p.Id}-without-{layer.GetType().Name}");
                        layer.gameObject.SetActive(true);
                    }
                // -smokezoom N: after each view, N more frames each a quarter of the distance toward the target
                for (int z = 1; z <= zooms; z++)
                {
                    CameraPose pose = root.Rig.Pose;
                    pose.Distance *= 0.25f;
                    root.Rig.SetPose(pose);
                    yield return new WaitForSecondsRealtime(1.2f);
                    yield return Shot($"{scene}-view-{p.Id}-zoom{z}");
                }
            }

            if (scene == GraphScene.Economy && System.Environment.GetCommandLineArgs().Contains("-smokereplay")) yield return Replay(root);

            DirectorMode director = root.Modules.OfType<DirectorMode>().FirstOrDefault();
            // -smoketour: the tour runs even with -smokeonly (e.g. "-smokeonly none -smoketour" captures only the tours)
            if (director != null && (only == null || System.Environment.GetCommandLineArgs().Contains("-smoketour")))
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

        /// <summary>The longest the replay of the wave may take (s): about 80 years at a second or so each, with room.</summary>
        const float ReplaySeconds = 420f;

        /// <summary>
        /// <c>-smokereplay</c>: the replay of the economy's wave from its first year to the latest, changing views on the way
        /// (the overview, the landscape, the money chart, the decade's ladder, the overview again) with a frame in each; an
        /// error if it does not reach the end in time (the replay itself logs a year that never shows).
        /// </summary>
        IEnumerator Replay(GraphRoot root)
        {
            (int year, string view, string shot)[] plan =
            {
                (0, "overview", null), (1958, null, "1958-overview"), (1960, "landscape", null), (1975, null, "1975-landscape"),
                (1977, "money", null), (1983, null, "1983-money"), (1990, "mobility", null), (2009, null, "2009-mobility"),
                (2012, "overview", null), (Economy.EconomyState.MaxYear, null, "end-overview")
            };
            root.Focus("overview", 0.6f);
            yield return new WaitForSecondsRealtime(ViewSeconds);
            Economy.EconomyReplay.Play(true);
            LayerClock.Start();
            float start = Time.realtimeSinceStartup;
            int step = 0;
            while (Economy.EconomyReplay.Active && step < plan.Length && Time.realtimeSinceStartup - start < ReplaySeconds)
            {
                (int year, string view, string shot) = plan[step];
                if (Economy.EconomyReplay.ShownYear < year)
                {
                    yield return null;
                    continue;
                }

                if (view != null) root.Focus(view, 0.6f);
                if (shot != null)
                {
                    LayerClock.Pause();
                    yield return Shot($"economy-replay-{shot}");
                    LayerClock.Resume();
                }

                step++;
            }

            if (step < plan.Length && Time.realtimeSinceStartup - start >= ReplaySeconds)
            {
                Debug.LogError($"[Why] smoke: the replay did not reach {plan[step].year} in {ReplaySeconds:0} s (at {Economy.EconomyReplay.ShownYear})");
            }

            while (Economy.EconomyReplay.Active && Time.realtimeSinceStartup - start < ReplaySeconds) yield return null;
            LayerClock.Stop();
            Debug.Log($"[Why] smoke: replay done in {Time.realtimeSinceStartup - start:0.0} s, {errors} errors so far");
            Debug.Log("[Why] smoke: replay frames: " + LayerClock.Report());
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
