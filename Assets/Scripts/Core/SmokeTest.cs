using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Why.Director;
using Debug = UnityEngine.Debug;

namespace Why
{
    /// <summary>
    /// Runtime smoke test for players and CI, idle unless the player is started with <c>-smoketest</c>: once the graph is
    /// loaded it flies through every view preset, steps through every stop of the guided tour, then opens the other
    /// scene and does the same there, and quits with exit code 1 if anything logged an error or exception.
    /// <c>-screenshots DIR</c> saves a frame of every view (each flown to from the overview, so no view inherits the
    /// previous one's state) and of every tour stop with its card complete, named by stop number and id
    /// (<c>economy-tour-01-hills_bridge</c>), then the title and end cards (<c>-smokeonly a,b</c> limits it to those views
    /// and skips the tour, unless <c>-smoketour</c> asks for it too; <c>-smokelayers</c> re-captures each view with every
    /// layer hidden in turn, to find which layer draws what; a view with states of its own, <see cref="ViewVariants"/>, is
    /// captured in each); <c>-smokereplay</c> also plays the economy's replay of the
    /// wave from 1950 to the latest year through the overview, landscape, money and mobility views, with a frame at a few
    /// years (each once the hills layers show the year, <see cref="CatchUp"/>), failing if it stalls, and logs the layers
    /// that cost the most in its long frames (<see cref="LayerClock"/>);
    /// add <c>-xr</c> to exercise the headset
    /// presentation on a desktop (canvases on the HUD plane, the diorama rig) without a headset.
    /// After every view, view state, tour card and stop and replay frame (saved or not), the checks the packages registered
    /// run on it (<see cref="SmokeChecks"/>; each problem is an error); <c>-smokeextra a,b</c> runs the packages' own
    /// sequences of those names after the tour (a name no scene ran is an error); <c>-smokecheckfail</c> registers a check
    /// that always fails, to prove that a failing check fails the run; <c>-smokefacts</c> logs every fact the models
    /// published at the end of the run (<see cref="ModelFacts"/>, read by Tools/export-facts.py), and
    /// <c>-smoketestfacts</c> publishes two test facts to try that without a model. The run sees no pointer, so nothing in
    /// its frames is hovered, unless <c>-smokepointer X,Y</c> parks one at that pixel from the frame's top left
    /// (<see cref="ParkPointer"/>).
    /// </summary>
    [GraphScenes(GraphScene.Why, GraphScene.Economy)]
    public sealed class SmokeTest : GraphModule
    {
        const float ViewSeconds = 2.6f;
        const float StopSeconds = 2.8f;

        /// <summary>
        /// Time at the overview between two views: a view reached from the one before it in the bar can inherit that
        /// one's state (the 1972 wave left the next view in a stale pose), which a frame of the view must not show.
        /// </summary>
        const float OverviewSeconds = 0.8f;

        static bool enabledByArgs, hooked, argsRead, logFacts;
        static HashSet<string> only;
        static int zooms;
        static readonly HashSet<string> visited = new HashSet<string>();
        static readonly List<string> problems = new List<string>();
        static int errors;

        /// <summary>
        /// The packages' sequences to run after the tour (<c>-smokeextra a,b</c>, in that order), read once for the run; empty,
        /// never null, so the run can go on even when Init failed before reading them.
        /// </summary>
        static readonly List<string> extras = new List<string>();

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

            // first, so that every error from here on counts, an exception in this Init too (GraphRoot logs it and still
            // calls OnLoaded, so the run goes on and must exit 1)
            if (!hooked)
            {
                hooked = true;
                Application.logMessageReceived += OnLog;
            }

            if (!argsRead)
            {
                // once for the run (the other scene has the same arguments): the sequences after the tour, the facts at the end
                argsRead = true;
                int x = System.Array.IndexOf(args, "-smokeextra");
                if (x >= 0 && x + 1 < args.Length) extras.AddRange(args[x + 1].Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).Distinct());
                if (x >= 0 && extras.Count == 0) Debug.LogError("[Why] smoke: -smokeextra names no sequence (-smokeextra a,b)");
                logFacts = args.Contains("-smokefacts");
                if (args.Contains("-smoketestfacts")) TestFacts();
                ParkPointer(args);
            }

            int i = System.Array.IndexOf(args, "-screenshots");
            SmokeChecks.ShotsDir = i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
            if (SmokeChecks.ShotsDir != null)
            {
                // a directory that cannot be made is an error (the run exits 1); the run goes on without frames
                try
                {
                    Directory.CreateDirectory(SmokeChecks.ShotsDir);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[Why] smoke: -screenshots '{SmokeChecks.ShotsDir}' cannot be made, so no frames are saved: {e.Message}");
                    SmokeChecks.ShotsDir = null;
                }
            }

            int o = System.Array.IndexOf(args, "-smokeonly");
            if (o >= 0 && o + 1 < args.Length) only = new HashSet<string>(args[o + 1].Split(','));
            int zi = System.Array.IndexOf(args, "-smokezoom");
            if (zi >= 0 && zi + 1 < args.Length) int.TryParse(args[zi + 1], out zooms);

            // in each scene (checks belong to the scene that registers them)
            if (args.Contains("-smokecheckfail"))
            {
                SmokeChecks.Register("smokecheckfail", view => new[] { "registered by -smokecheckfail, so the run must exit 1" });
            }

            Debug.Log($"[Why] smoke: scene '{GraphScene.Current}' starting");
        }

        /// <summary>
        /// Takes the desk's pointers out of the run: the OS pointer rests wherever it was left, and what it happened to hover
        /// (a hover card, the strategy view's focused firm, a label lifted in the tour) changed the frames from one run to the
        /// next. Removed from the input system, every hover and click path sees no pointer, as on a touch screen before a
        /// touch. With <c>-smokepointer X,Y</c> a virtual mouse is parked at that pixel from the frame's top left instead, so a
        /// run's hovers are the same every time. Once for the run: the input system outlives the scene.
        /// </summary>
        static void ParkPointer(string[] args)
        {
            foreach (InputDevice device in InputSystem.devices.ToArray())
            {
                if (device is Pointer) InputSystem.RemoveDevice(device);
            }

            int p = System.Array.IndexOf(args, "-smokepointer");
            if (p < 0)
            {
                Debug.Log("[Why] smoke: no pointer");
                return;
            }

            string[] xy = p + 1 < args.Length ? args[p + 1].Split(',') : new string[0];
            if (xy.Length != 2 || !float.TryParse(xy[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) ||
                !float.TryParse(xy[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
            {
                Debug.LogError("[Why] smoke: -smokepointer takes the pixel from the frame's top left as X,Y (-smokepointer 1267,392)");
                return;
            }

            Mouse mouse = InputSystem.AddDevice<Mouse>("Smoke pointer");
            mouse.MakeCurrent();
            // the input system counts pixels up from the bottom
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(x, Screen.height - y) });
            Debug.Log($"[Why] smoke: pointer parked at ({x:0}, {y:0}) from the top left of {Screen.width}x{Screen.height}");
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
                yield return SmokeChecks.Shot($"genesis-{s:00.00}s");
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
            Debug.Log("[Why] smoke checks: " + SmokeChecks.Summary());

            foreach (ViewPreset p in ViewPresets.All.ToList())
            {
                if (only != null && !only.Contains(p.Id)) continue;
                if (p.Id != "overview")
                {
                    root.Focus("overview", 0.6f);
                    yield return new WaitForSecondsRealtime(OverviewSeconds);
                }

                root.Focus(p, 0.6f);
                yield return new WaitForSecondsRealtime(ViewSeconds);
                yield return SmokeChecks.Capture($"{scene}-view-{p.Id}", p);
                // a view with states of its own (ViewVariants): each state in turn, then back to the default
                if (ViewVariants.TryGet(p.Id, out ViewVariants.Entry variants))
                {
                    for (int v = 0; v < variants.Count(); v++)
                    {
                        variants.Show(v);
                        yield return new WaitForSecondsRealtime(1.2f);
                        yield return SmokeChecks.Capture($"{scene}-view-{p.Id}-{variants.Name(v)}", p);
                    }

                    variants.Show(-1);
                }
                // -smokelayers: the view again with each layer hidden in turn, to find which layer draws what (diagnostic
                // frames, like -smokezoom's: no checks)
                if (System.Environment.GetCommandLineArgs().Contains("-smokelayers") && SmokeChecks.ShotsDir != null)
                    foreach (GraphLayer layer in root.Layers.ToList())
                    {
                        if (layer == null || !layer.gameObject.activeSelf) continue;
                        layer.gameObject.SetActive(false);
                        yield return new WaitForSecondsRealtime(0.3f);
                        yield return SmokeChecks.Shot($"{scene}-view-{p.Id}-without-{layer.GetType().Name}");
                        layer.gameObject.SetActive(true);
                    }
                // -smokezoom N: after each view, N more frames each a quarter of the distance toward the target
                for (int z = 1; z <= zooms; z++)
                {
                    CameraPose pose = root.Rig.Pose;
                    pose.Distance *= 0.25f;
                    root.Rig.SetPose(pose);
                    yield return new WaitForSecondsRealtime(1.2f);
                    yield return SmokeChecks.Shot($"{scene}-view-{p.Id}-zoom{z}");
                }
            }

            if (scene == GraphScene.Economy && System.Environment.GetCommandLineArgs().Contains("-smokereplay")) yield return Replay(root);

            DirectorMode director = root.Modules.OfType<DirectorMode>().FirstOrDefault();
            // -smoketour: the tour runs even with -smokeonly (e.g. "-smokeonly none -smoketour" captures only the tours)
            if (director != null && (only == null || System.Environment.GetCommandLineArgs().Contains("-smoketour")))
            {
                director.StartTour();
                yield return new WaitForSecondsRealtime(StopSeconds);
                yield return SmokeChecks.Capture($"{scene}-tour-00-title", root.CurrentPreset);
                // each stop: let the camera settle, finish the card's typewriter, let the whole text lay out, capture, and
                // only then move on (the frame shows the stop it is named after, with its card complete); stop at the end
                // card: "next" there continues into the economy, which this test does itself
                director.NextStep();
                for (int n = 0; n < 200 && director.TourRunning && !director.AtEndCard; n++)
                {
                    yield return new WaitForSecondsRealtime(StopSeconds);
                    director.CompleteTyping();
                    // the stop's picture cards in their settled state (faded in, at their places), checked by "tour-pictures"
                    UI.TourPictures.Settle();
                    yield return null;
                    yield return SmokeChecks.Capture($"{scene}-tour-{n + 1:00}-{director.CurrentStepId}", root.CurrentPreset);
                    director.NextStep();
                }

                if (director.AtEndCard)
                {
                    yield return new WaitForSecondsRealtime(StopSeconds);
                    yield return SmokeChecks.Capture($"{scene}-tour-end", root.CurrentPreset);
                }

                director.ExitTour();
            }

            // -smokestory ai,revolutions: the history stories (economy scene), each stop captured once it settles and, when its
            // picture changes with the narration, again after its last cue
            int si = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-smokestory");
            if (scene == GraphScene.Economy && director != null && si >= 0 && si + 1 < System.Environment.GetCommandLineArgs().Length)
                foreach (string story in System.Environment.GetCommandLineArgs()[si + 1].Split(','))
                {
                    director.StartStory("Data/Walkthrough/story_" + story + "_tour");
                    yield return new WaitForSecondsRealtime(StopSeconds);
                    yield return SmokeChecks.Capture($"{scene}-story-{story}-00-title", root.CurrentPreset);
                    director.NextStep();
                    for (int n = 0; n < 60 && director.TourRunning && !director.AtEndCard; n++)
                    {
                        yield return new WaitForSecondsRealtime(StopSeconds);
                        director.CompleteTyping();
                        yield return null;
                        string id = director.CurrentStepId;
                        yield return SmokeChecks.Capture($"{scene}-story-{story}-{n + 1:00}-{id}", root.CurrentPreset);
                        float last = director.CurrentStepLastCue;
                        if (last > 0)
                        {
                            yield return new WaitForSecondsRealtime(Mathf.Max(.5f, last + 3.2f - StopSeconds));
                            if (director.CurrentStepId == id) yield return SmokeChecks.Capture($"{scene}-story-{story}-{n + 1:00}-{id}-b", root.CurrentPreset);
                        }

                        director.NextStep();
                    }

                    if (director.AtEndCard)
                    {
                        yield return new WaitForSecondsRealtime(StopSeconds);
                        yield return SmokeChecks.Capture($"{scene}-story-{story}-end", root.CurrentPreset);
                    }

                    director.ExitTour();
                }

            // -smokeextra a,b: the packages' own sequences this scene registered, after everything else in it
            if (extras.Count > 0) yield return SmokeChecks.RunSequences(extras);

            Debug.Log($"[Why] smoke: scene '{scene}' done, {errors} errors so far");
            string other = scene == GraphScene.Economy ? GraphScene.Why : GraphScene.Economy;
            if (!visited.Contains(other))
            {
                GraphRoot.OpenOtherScene();
                yield break;
            }

            // the end of the run: what only both scenes together can tell, then the facts
            foreach (string name in SmokeChecks.NotRun(extras))
            {
                Debug.LogError($"[Why] smoke: -smokeextra '{name}': no sequence of that name ran in either scene (registered: {SmokeChecks.SequenceNames()})");
            }

            foreach (string problem in ModelFacts.Problems()) Debug.LogError("[Why] ModelFacts " + problem);
            if (logFacts) LogFacts();
            foreach (string p in problems) Debug.Log("[Why] smoke problem: " + p);
            Debug.Log($"[Why] smoke: finished, {errors} errors");
            Application.Quit(errors > 0 ? 1 : 0);
        }

        /// <summary><c>-smokefacts</c>: every fact the models published, sorted by key, one line each (Tools/export-facts.py).</summary>
        static void LogFacts()
        {
            IReadOnlyList<ModelFacts.Fact> all = ModelFacts.All();
            Debug.Log($"[Why] facts: {all.Count} published");
            foreach (ModelFacts.Fact f in all) Debug.Log(ModelFacts.Line(f));
        }

        /// <summary>
        /// <c>-smoketestfacts</c>: two facts that are not a model's, to try the fact lines and Tools/export-facts.py without
        /// one: a number with a unit of two words, and a text with quotes and a bar in it and no unit.
        /// </summary>
        static void TestFacts()
        {
            const string source = "SmokeTest -smoketestfacts", basis = "a test value, not a model's";
            ModelFacts.Set("smoke.test.number", 1234.5, "test units", source, basis);
            ModelFacts.Set("smoke.test.text", "a \"quoted\" | text", "", source, basis);
        }

        /// <summary>The longest the replay of the wave may take (s): about 80 years at a second or so each, with room.</summary>
        const float ReplaySeconds = 420f;

        /// <summary>
        /// The most frames a replay frame waits for the hills layers to catch up with its year (they take about a dozen,
        /// <see cref="CatchUp"/>): a second at 60 frames a second.
        /// </summary>
        const int CatchUpFrames = 60;

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
                    yield return CatchUp(year, shot);
                    LayerClock.Pause();
                    yield return SmokeChecks.Capture($"economy-replay-{shot}", root.CurrentPreset);
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

        /// <summary>
        /// Waits until the hills layers show the replay's year. The land swaps first and the chip follows it; every other
        /// hills layer then rebuilds in a frame of its own (<see cref="Economy.HillRebuilds"/>, about a dozen frames), so the
        /// first frame of a year shows last year's labels under this year's chip. Caught up is a frame in which no layer took
        /// the frame's rebuild: asked here, after every layer's Tick (where they all rebuild), <c>HillRebuilds.Take</c> spends
        /// nothing a layer could still use. At most <see cref="CatchUpFrames"/> frames, and only while the replay still shows
        /// the year: the replay keeps its own pace and is never held. A warning when the frame is taken before.
        /// </summary>
        static IEnumerator CatchUp(int year, string shot)
        {
            int frames = 0;
            for (; frames < CatchUpFrames && Economy.EconomyReplay.ShownYear == year; frames++)
            {
                if (Economy.HillRebuilds.Take())
                {
                    Debug.Log($"[Why] smoke: replay frame {shot}: the hills layers caught up with {year} in {frames} frames");
                    yield break;
                }

                yield return null;
            }

            Debug.LogWarning($"[Why] smoke: replay frame {shot} taken before the hills layers caught up with {year} " +
                             $"({frames} frames; the replay shows {Economy.EconomyReplay.ShownYear})");
        }
    }
}
