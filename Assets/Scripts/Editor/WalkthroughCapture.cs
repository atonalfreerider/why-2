using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using Why.Director;

namespace Why.EditorTools
{
    /// <summary>
    /// Records the narrated walkthrough with the Unity Recorder: opens the universe, sizes the Game view to the video
    /// (landscape 1920 x 1080 or portrait 1080 x 1920, whose layout the graph re-flows to) and lets the scene settle at
    /// that size, takes the desk's mouse and keyboard out of the game, then plays the guided tour from its first stop (the
    /// Big Bang) at 30 fps: to its end card and 10 s of the card, through its first stops, or for a set number of seconds.
    /// The Recorder writes the picture (H.264 MP4; VP8 WebM on Linux, where it has no H.264 encoder). The voice is laid in
    /// afterwards from its clips by Tools/capture-walkthrough.sh, which also transcodes to MP4 and marks the chapters, from
    /// the cue log written beside the video (&lt;out&gt;.cues.json): the frame each stop starts on, the tour's chapters and the
    /// video's real length, to which the script cuts the picture.
    ///
    /// Menu: Why > Capture (the whole tour, or its opening). Command line (needs the Editor's window, not -batchmode):
    ///   Unity -projectPath . -executeMethod Why.EditorTools.WalkthroughCapture.FromCommandLine
    ///         -captureOrientation landscape|portrait [-captureSeconds 0|opening|SECONDS] [-captureOut Captures/why-landscape-raw]
    /// -captureSeconds 0, the default, records the whole tour; opening, its first three stops (to the frame before the fourth
    /// appears, about 114.5 s). The Editor quits when the video is written, with exit code 1 when no complete video was made
    /// (the cue log, when there is one, says why). While a capture plays, the Editor's scripts do not reload (a script saved
    /// meanwhile compiles once it has ended), and a paused play mode (the pause button, or Error Pause on an error) is resumed.
    /// </summary>
    [InitializeOnLoad]
    public static class WalkthroughCapture
    {
        const string Key = "Why.WalkthroughCapture";

        /// <summary>Frames per second of the recording (and of the game clock while recording).</summary>
        const int Fps = 30;

        /// <summary>
        /// The opening: the tour's first three stops (genesis, origin, welcome). It ends at the stop boundary, on the frame
        /// before read_clock, the fourth stop, appears, whatever the clips' lengths: with the clips voiced on 2026-10-12 the
        /// three stops last 114.47 s (each its clip and TourNarrator.PauseAfter, 1.5 s, more), so the fixed 114.5 s the
        /// opening used to record ran into read_clock.
        /// </summary>
        const int OpeningStops = 3;

        /// <summary>How long a recording of the whole tour shows its end card.</summary>
        const float EndCardSeconds = 10f;

        /// <summary>
        /// Real seconds the scene runs at the video's size before the tour starts, at the least: the loading card holds
        /// 0.55 s and fades over 0.8 s (LoadingOverlay), and a change of orientation re-frames the view over 0.9 s
        /// (GraphRoot.ReframeSeconds).
        /// </summary>
        const double SettleSeconds = 2;

        /// <summary>Real seconds more the capture waits for the loading card, the camera and the warp to come to rest.</summary>
        const double StillSeconds = 30;

        /// <summary>Real seconds the Game view gets to take the video's size before the next way of sizing it is tried.</summary>
        const double SizeSeconds = 5;

        /// <summary>
        /// Player frames from StartRecording to the video's first frame: the Recorder (5.1.7, _FrameRequestComponent) lets
        /// the first frame after the call pass for the Game view to take its size and records from the second.
        /// </summary>
        const int LeadInFrames = 2;

        /// <summary>
        /// Seconds of video one stop may run before the capture gives up on a stalled tour: far beyond the longest stop
        /// (origin, 43.4 s with the clips of 2026-10-12).
        /// </summary>
        const float StallSeconds = 300f;

        /// <summary>
        /// Real seconds from the start of a capture to its first recorded frame (entering play mode, building the graph,
        /// sizing the Game view, settling) before it gives up: an unattended run must not wait forever.
        /// </summary>
        const double StartSeconds = 600;

        /// <summary>
        /// Real seconds the player may go without a new frame while recording before the capture gives up (a hung or paused
        /// play mode): far beyond the slowest frame (the whole tour records at about 0.6x real time, 55 ms a frame).
        /// </summary>
        const double NoProgressSeconds = 120;

        [Serializable]
        sealed class Job
        {
            public bool Portrait;

            /// <summary>
            /// Seconds of video; 0 or less records the whole tour, until <see cref="EndCardSeconds"/> into its end card (or
            /// through its first <see cref="Stops"/>).
            /// </summary>
            public float Seconds;

            /// <summary>
            /// Stops to record from the tour's first, ending on the frame before the next stop appears (and before its cue);
            /// 0 or less: no such limit.
            /// </summary>
            public int Stops;

            /// <summary>The video's path without its extension, from the project folder; the cue log is this + .cues.json.</summary>
            public string Output;

            /// <summary>Quit the Editor afterwards (the command line), with exit code 1 when no complete video was made.</summary>
            public bool Exit;

            /// <summary>EditorApplication.timeSinceStartup when the capture began (the job outlives play mode's domain reload).</summary>
            public double BeganAt;

            /// <summary>True once the Recorder runs: a job found recording without its controller lost it to a domain reload.</summary>
            public bool Recording;

            public int Width => Portrait ? 1080 : 1920;
            public int Height => Portrait ? 1920 : 1080;

            public string Describe() => $"{Width}x{Height}, " + (Stops > 0 ? $"the tour's first {Stops} stops"
                : Seconds > 0 ? Seconds.ToString("0.###", CultureInfo.InvariantCulture) + " s"
                : $"the whole tour and {EndCardSeconds:0} s of its end card");
        }

        static RecorderController controller;
        static DirectorMode director;
        static int phase;

        // settling: when the size was last asked for, how many ways were tried, and since when the Game view has had it
        static double sizeAskedAt, sizedAt;
        static int sizeTries;

        // recording: the player frame that is the video's first, the frames the video gets (0 until the end card sets them
        // for the whole tour), the video frames the end card and the current stop appeared on, the next progress line
        static int startFrame, endFrames, endCardFrame, stopFrame, nextReport;
        static double recordingSince;
        static string lastStep;
        static readonly List<string> cues = new List<string>();

        // the watchdog: the player frame last seen while recording, and when (real time) it was first seen
        static int progressFrame;
        static double progressAt;

        /// <summary>
        /// True while this capture holds the Editor's assemblies from reloading (<see cref="LockReload"/>): a reload in play
        /// mode would cut the recording and lose <see cref="removedInput"/>. EditorApplication counts its locks, so each is
        /// released exactly once.
        /// </summary>
        static bool reloadLocked;

        /// <summary>The tour's chapters by their first stop: a view's title where the tour first reaches it (DirectorMode.Chapters).</summary>
        static readonly Dictionary<int, string> chapters = new Dictionary<int, string>();

        /// <summary>The desk's pointers and keyboards, out of the input system while a capture runs (<see cref="TakeInput"/>).</summary>
        static readonly List<InputDevice> removedInput = new List<InputDevice>();

        static WalkthroughCapture()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("Why/Capture/Walkthrough, landscape 1920x1080", priority = 1)]
        static void Landscape() => Begin(new Job { Portrait = false, Seconds = 0, Output = Output(false, false) });

        [MenuItem("Why/Capture/Walkthrough, portrait 1080x1920", priority = 2)]
        static void Portrait() => Begin(new Job { Portrait = true, Seconds = 0, Output = Output(true, false) });

        [MenuItem("Why/Capture/Walkthrough opening (first three stops), landscape 1920x1080", priority = 20)]
        static void OpeningLandscape() => Begin(new Job { Portrait = false, Stops = OpeningStops, Output = Output(false, true) });

        [MenuItem("Why/Capture/Walkthrough opening (first three stops), portrait 1080x1920", priority = 21)]
        static void OpeningPortrait() => Begin(new Job { Portrait = true, Stops = OpeningStops, Output = Output(true, true) });

        /// <summary>
        /// The Recorder's file without its extension, as Tools/capture-walkthrough.sh names it (so it can lay the voice into a
        /// capture made from the menu): never the name of a finished MP4.
        /// </summary>
        static string Output(bool portrait, bool opening) =>
            "Captures/why-" + (portrait ? "portrait" : "landscape") + (opening ? "-opening" : "") + "-raw";

        /// <summary>The command line's capture (see the class summary); quits the Editor when it is done.</summary>
        public static void FromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            string Arg(string name, string fallback)
            {
                int i = Array.IndexOf(args, name);
                return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
            }

            string orientation = Arg("-captureOrientation", "landscape");
            string length = Arg("-captureSeconds", "0");
            bool opening = length == "opening";
            float seconds = opening ? 0
                : float.TryParse(length, NumberStyles.Float, CultureInfo.InvariantCulture, out float s) && !float.IsNaN(s) && !float.IsInfinity(s) ? s
                : float.NaN;
            if ((orientation != "landscape" && orientation != "portrait") || float.IsNaN(seconds))
            {
                Debug.LogError("[Why] capture: -captureOrientation takes landscape or portrait, and -captureSeconds 0 (the whole tour), " +
                               $"opening or a number of seconds; got '{orientation}' and '{length}'");
                EditorApplication.Exit(1);
                return;
            }

            bool portrait = orientation == "portrait";
            Begin(new Job
            {
                Portrait = portrait, Seconds = seconds, Stops = opening ? OpeningStops : 0,
                Output = Arg("-captureOut", Output(portrait, opening)), Exit = true
            });
        }

        static void Begin(Job job)
        {
            if (Current != null || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Why] capture: " + (Current != null ? "a capture is already running" :
                    "leave play mode first (the capture opens the universe and plays it itself)"));
                if (job.Exit) EditorApplication.Exit(1);
                return;
            }

            // from the menu, unsaved changes in the open scene are offered for saving first; the command line opens a fresh Editor
            if (!job.Exit && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(job.Output)) ?? ".");
            // an earlier cue log must not be paired with this capture's video: a capture that ends early writes its own
            File.Delete(job.Output + ".cues.json");
            job.BeganAt = EditorApplication.timeSinceStartup;
            job.Recording = false;
            Save(job);
            phase = 0;
            try
            {
                EditorSceneManager.OpenScene("Assets/Scenes/Why.unity");
                // sized before play mode too, so the graph is laid out for the video from its first frame
                AskSize(job, 0);
                Debug.Log($"[Why] capture: {job.Output} ({job.Describe()}), entering play mode");
                EditorApplication.EnterPlaymode();
            }
            catch (Exception e)
            {
                Fail(job, "could not open the universe: " + e.GetBaseException().Message);
            }
        }

        static Job Current
        {
            get
            {
                string json = SessionState.GetString(Key, "");
                return string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<Job>(json);
            }
        }

        static void Save(Job job)
        {
            if (job == null) SessionState.EraseString(Key);
            else SessionState.SetString(Key, JsonUtility.ToJson(job));
        }

        static void Tick()
        {
            Job job = Current;
            if (job == null || phase >= 3) return;
            try
            {
                if (job.Recording && controller == null)
                {
                    Fail(job, "the recording was cut off by a domain reload (a script changed while it ran?)");
                    return;
                }

                if (!job.Recording && EditorApplication.timeSinceStartup - job.BeganAt > StartSeconds)
                {
                    Fail(job, $"nothing was recorded within {StartSeconds:0} s of starting (play mode, the graph or the Game view's size; see above)");
                    return;
                }

                if (!EditorApplication.isPlaying) return;
                TakeInput();
                if (phase == 0)
                {
                    // no domain reload from here to the end (a script saved meanwhile compiles once the capture has ended)
                    LockReload();
                    // the Game view at the video's size first, so the layout (and a portrait re-flow) settles before anything plays
                    CameraRig.InputLocked = true;
                    AskSize(job, 0);
                    sizeTries = 1;
                    sizedAt = -1;
                    phase = 1;
                    return;
                }

                // recording follows the tour itself, so a scene that went away ends it rather than leaving it running
                if (phase == 2)
                {
                    Record(job);
                    return;
                }

                GraphRoot root = GraphRoot.Instance;
                if (root != null && root.IsLoaded) Settle(job, root);
            }
            catch (Exception e)
            {
                if (phase == 2) Finish(job, Math.Max(0, Time.frameCount - startFrame + 1), "the capture failed: " + e.GetBaseException().Message);
                else Fail(job, "the capture failed: " + e);
            }
        }

        /// <summary>
        /// Sets the Game view to the video's size: first through PlayModeWindow, as the Recorder itself does when it starts
        /// (left to the Recorder, the view took its size only as the first frame was recorded, after the opening had framed
        /// itself for the old shape), then through Why > Game View (PortraitGameView), whose two sizes are the video's.
        /// </summary>
        static void AskSize(Job job, int way)
        {
            sizeAskedAt = EditorApplication.timeSinceStartup;
            try
            {
                if (way == 0)
                {
                    if (PlayModeWindow.GetViewType() != PlayModeWindow.PlayModeViewTypes.GameView)
                        PlayModeWindow.SetViewType(PlayModeWindow.PlayModeViewTypes.GameView);
                    PlayModeWindow.SetCustomRenderingResolution((uint)job.Width, (uint)job.Height, "Recording Resolution");
                }
                else if (!EditorApplication.ExecuteMenuItem(job.Portrait ? "Why/Game View/Portrait 1080x1920" : "Why/Game View/Landscape 1920x1080"))
                {
                    ScreenLayout.EditorGameView?.Invoke(job.Portrait);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Why] capture: could not size the Game view ({e.GetBaseException().Message})");
            }
        }

        /// <summary>True when the Game view renders at the video's size, so the Recorder will not resize it when it starts.</summary>
        static bool RendersAt(Job job)
        {
            // what the graph lays itself out for (ScreenLayout reads the Game view once a frame), and what the Recorder checks
            if (ScreenLayout.Width != job.Width || ScreenLayout.Height != job.Height || ScreenLayout.IsPortrait != job.Portrait) return false;
            try
            {
                PlayModeWindow.GetRenderingResolution(out uint width, out uint height);
                return width == job.Width && height == job.Height;
            }
            catch (Exception)
            {
                return true; // unknown: the screen's own size decides
            }
        }

        /// <summary>
        /// Waits until the Game view has had the video's size for <see cref="SettleSeconds"/> of real time and the scene is at
        /// rest (the loading card gone, any re-framing done), then starts the recording.
        /// </summary>
        static void Settle(Job job, GraphRoot root)
        {
            double now = EditorApplication.timeSinceStartup;
            if (!RendersAt(job))
            {
                sizedAt = -1;
                if (now - sizeAskedAt < SizeSeconds) return;
                if (sizeTries >= 2)
                {
                    Fail(job, $"the Game view stays {ScreenLayout.Width}x{ScreenLayout.Height}, not {job.Width}x{job.Height}; " +
                              "pick that size in its resolution menu and try again");
                    return;
                }

                Debug.LogWarning($"[Why] capture: the Game view is {ScreenLayout.Width}x{ScreenLayout.Height}; sizing it through Why > Game View");
                AskSize(job, 1);
                sizeTries = 2;
                return;
            }

            if (sizedAt < 0) sizedAt = now;
            if (now - sizedAt < SettleSeconds) return;

            Why.UI.LoadingOverlay overlay = root.Modules.OfType<Why.UI.LoadingOverlay>().FirstOrDefault();
            string moving = overlay != null && overlay.enabled ? "the loading card"
                : root.Rig != null && root.Rig.Flying ? "the camera"
                : GraphWarp.Animating ? "the warp"
                : null;
            if (moving != null && now - sizedAt < SettleSeconds + StillSeconds) return;
            if (moving != null) Debug.LogWarning($"[Why] capture: {moving} still moves after {SettleSeconds + StillSeconds:0} s; recording anyway");
            StartRecording(job, root);
        }

        static void StartRecording(Job job, GraphRoot root)
        {
            director = root.Modules.OfType<DirectorMode>().FirstOrDefault();
            if (director == null)
            {
                Fail(job, "the scene has no guided tour (DirectorMode)");
                return;
            }

            chapters.Clear();
            foreach (TourChapter chapter in director.Chapters) chapters[chapter.FirstStep] = chapter.Title;

            controller = new RecorderController(Settings(job));
            controller.PrepareRecording();
            if (!controller.StartRecording())
            {
                Fail(job, "the Recorder did not start (see its messages above)");
                return;
            }

            // the Recorder runs the game clock at 1 / Fps a frame from here on, so the tour plays on the video's clock from its start
            startFrame = Time.frameCount + LeadInFrames;
            director.PlayFrom(0);
            endFrames = job.Seconds > 0 ? Mathf.Max(1, Mathf.RoundToInt(job.Seconds * Fps)) : 0;
            endCardFrame = -1;
            stopFrame = 0;
            nextReport = 60 * Fps;
            recordingSince = EditorApplication.timeSinceStartup;
            progressFrame = Time.frameCount;
            progressAt = recordingSince;
            lastStep = null;
            cues.Clear();
            job.Recording = true;
            Save(job);
            phase = 2;
            Debug.Log($"[Why] capture: recording {job.Output} ({job.Describe()})");
        }

        static void Record(Job job)
        {
            // the video frame the player rendered last (negative while the Recorder lets its first frames pass): every frame
            // is 1 / Fps of video, however long the Editor takes to render it, so time is counted in frames
            int shown = Time.frameCount - startFrame;

            // a paused play mode (the pause button, or Error Pause on an error) makes no frames and nothing else would resume
            // it: the video loses nothing to a pause, so the capture resumes it and says so
            if (EditorApplication.isPaused)
            {
                Debug.LogWarning($"[Why] capture: play mode was paused at {Clock(Math.Max(0, shown))} of video (the pause button, " +
                                 "or Error Pause on an error above); resumed (stop play mode to end the capture)");
                EditorApplication.isPaused = false;
            }

            // and a player that makes no frame in real time (hung, or paused again at once) ends the capture
            double now = EditorApplication.timeSinceStartup;
            if (Time.frameCount != progressFrame)
            {
                progressFrame = Time.frameCount;
                progressAt = now;
            }
            else if (now - progressAt > NoProgressSeconds)
            {
                Finish(job, Math.Max(0, shown + 1),
                    $"the player made no frame in {NoProgressSeconds:0} s of real time (play mode hung or paused)");
                return;
            }

            DirectorMode d = director;
            if (d == null || !d.TourRunning)
            {
                Finish(job, Math.Max(0, shown + 1), "the tour stopped before its end card");
                return;
            }

            // a capture of the first stops ends where the next stop appears, before its cue is noted: on the frame before (that
            // frame is the next stop's first, which the Recorder has taken already; Tools/capture-walkthrough.sh cuts the
            // picture to the frames the cue log gives)
            if (job.Stops > 0 && d.CurrentStep >= job.Stops)
            {
                Finish(job, Math.Max(0, shown), null);
                return;
            }

            // each stop's first frame, where Tools/capture-walkthrough.sh lays in its narration (and a chapter, at a view's first stop)
            string step = d.CurrentStepId;
            if (step != null && step != lastStep)
            {
                lastStep = step;
                stopFrame = Math.Max(0, shown);
                StringBuilder cue = new StringBuilder("{\"id\":").Append(Quote(step))
                    .Append(",\"frame\":").Append(stopFrame)
                    .Append(",\"t\":").Append(Number(stopFrame / (float)Fps));
                if (chapters.TryGetValue(d.CurrentStep, out string chapter)) cue.Append(",\"chapter\":").Append(Quote(chapter));
                cues.Add(cue.Append('}').ToString());
            }

            if (d.AtEndCard && endCardFrame < 0)
            {
                endCardFrame = Math.Max(0, shown);
                if (job.Seconds <= 0) endFrames = endCardFrame + Mathf.RoundToInt(EndCardSeconds * Fps);
            }

            // nothing resumes a paused or stalled tour while the input is locked: stop instead of recording it for hours
            if (d.Paused)
            {
                Finish(job, Math.Max(0, shown + 1), $"the tour paused at {step ?? "a card"} (a click, a key or a change of view took over)");
                return;
            }

            if (!d.AtEndCard && shown - stopFrame > StallSeconds * Fps)
            {
                Finish(job, Math.Max(0, shown + 1), $"stop {step ?? "(none)"} ran {StallSeconds:0} s without moving on");
                return;
            }

            // a line a minute of video: where the tour is, how fast the Editor records, and how much memory it holds (a long
            // play session on a machine shared with other work)
            if (shown >= nextReport)
            {
                nextReport += 60 * Fps;
                double real = EditorApplication.timeSinceStartup - recordingSince;
                long resident;
                using (System.Diagnostics.Process self = System.Diagnostics.Process.GetCurrentProcess()) resident = self.WorkingSet64;
                string where = d.AtEndCard ? "the end card" : $"stop {d.CurrentStep + 1}, {step}";
                Debug.Log($"[Why] capture: {Clock(shown)} recorded, at {where}, " +
                          $"{(real > 0 ? shown / (float)Fps / real : 0).ToString("0.00", CultureInfo.InvariantCulture)}x real time, " +
                          $"the Editor holds {(resident / 1073741824.0).ToString("0.0", CultureInfo.InvariantCulture)} GB");
            }

            if (endFrames > 0 && shown + 1 >= endFrames) Finish(job, shown + 1, null);
        }

        /// <summary>
        /// Stops the Recorder and writes the cue log beside the video: its real length, the end card's time and each stop's
        /// first frame. A problem marks the video incomplete (Tools/capture-walkthrough.sh then keeps the earlier MP4).
        /// </summary>
        static void Finish(Job job, int frames, string problem)
        {
            controller?.StopRecording();
            controller = null;
            StringBuilder log = new StringBuilder("{\"fps\":").Append(Fps)
                .Append(",\"width\":").Append(job.Width).Append(",\"height\":").Append(job.Height)
                .Append(",\"frames\":").Append(frames).Append(",\"seconds\":").Append(Number(frames / (float)Fps))
                .Append(",\"endCard\":").Append(endCardFrame >= 0 ? Number(endCardFrame / (float)Fps) : "null")
                .Append(",\"complete\":").Append(problem == null ? "true" : "false");
            if (problem != null) log.Append(",\"problem\":").Append(Quote(problem));
            log.Append(",\"cues\":[\n").Append(string.Join(",\n", cues)).Append("\n]}\n");
            File.WriteAllText(job.Output + ".cues.json", log.ToString());

            string summary = $"{job.Output} ({frames} frames, {Clock(frames)}; {cues.Count} stops" +
                             (endCardFrame >= 0 ? $", end card at {Clock(endCardFrame)})" : ")");
            if (problem == null) Debug.Log("[Why] capture: wrote " + summary);
            else Debug.LogError($"[Why] capture: {problem}; wrote {summary}, incomplete");
            End(job, problem == null ? 0 : 1);
        }

        /// <summary>Ends a capture that has no cue log to write (it never recorded, or lost its Recorder).</summary>
        static void Fail(Job job, string problem)
        {
            Debug.LogError($"[Why] capture: {problem}; no video was made");
            try
            {
                controller?.StopRecording();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Why] capture: stopping the Recorder: " + e.GetBaseException().Message);
            }

            controller = null;
            End(job, 1);
        }

        static void End(Job job, int exitCode)
        {
            phase = 3;
            Save(null);
            director = null;
            CameraRig.InputLocked = false;
            GiveBackInput();
            UnlockReload();
            // false while play mode is already ending (stopped by hand)
            if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.ExitPlaymode();
            if (job.Exit) EditorApplication.delayCall += () => EditorApplication.Exit(exitCode);
        }

        /// <summary>
        /// Holds the Editor's assemblies from reloading until <see cref="UnlockReload"/> (once per capture, from its first
        /// update in play mode, after play mode's own reload): a script saved while the capture plays would otherwise reload
        /// the domain, cutting the recording and losing the devices taken out of the input system.
        /// </summary>
        static void LockReload()
        {
            if (reloadLocked) return;
            EditorApplication.LockReloadAssemblies();
            reloadLocked = true;
        }

        /// <summary>Lets the Editor reload again (a script saved meanwhile compiles now); balances <see cref="LockReload"/>.</summary>
        static void UnlockReload()
        {
            if (!reloadLocked) return;
            reloadLocked = false;
            EditorApplication.UnlockReloadAssemblies();
        }

        /// <summary>Play mode stopped by hand ends the capture: what was recorded is kept, marked incomplete.</summary>
        static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.ExitingPlayMode || phase >= 3) return;
            Job job = Current;
            if (job == null) return;
            if (phase == 2) Finish(job, Math.Max(0, Time.frameCount - startFrame + 1), "play mode ended before the capture did");
            else Fail(job, "play mode ended before the recording started");
        }

        /// <summary>
        /// Takes the desk's pointers (mouse, touch, pen) and keyboards out of the input system while a capture runs, as the
        /// smoke test does with pointers (SmokeTest.ParkPointer): a mouse resting over the Game view hovers labels into the
        /// frames, a click pauses the tour for good (the keys that would resume it are locked), and a stray key changes what is
        /// recorded (V turns the view portrait, Tab opens the other scene). Checked every update, so a device plugged in
        /// meanwhile goes too; <see cref="GiveBackInput"/> returns them all when the capture ends.
        /// </summary>
        static void TakeInput()
        {
            bool any = false;
            foreach (InputDevice device in InputSystem.devices) any |= device is Pointer || device is Keyboard;
            if (!any) return;
            foreach (InputDevice device in InputSystem.devices.ToArray())
            {
                if (!(device is Pointer) && !(device is Keyboard)) continue;
                InputSystem.RemoveDevice(device);
                removedInput.Add(device);
            }
        }

        static void GiveBackInput()
        {
            foreach (InputDevice device in removedInput)
            {
                try
                {
                    if (!device.added) InputSystem.AddDevice(device);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Why] capture: could not give {device.name} back to the input system ({e.GetBaseException().Message})");
                }
            }

            removedInput.Clear();
        }

        /// <summary>Video frames as minutes, seconds and tenths (33:14.7).</summary>
        static string Clock(int frames)
        {
            int tenths = frames * 10 / Fps;
            return $"{tenths / 600}:{tenths % 600 / 10:00}.{tenths % 10}";
        }

        static string Number(float value) => value.ToString("0.000", CultureInfo.InvariantCulture);

        /// <summary>A JSON string.</summary>
        static string Quote(string s)
        {
            StringBuilder sb = new StringBuilder("\"");
            foreach (char c in s)
            {
                if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                else if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                else sb.Append(c);
            }

            return sb.Append('"').ToString();
        }

        static RecorderControllerSettings Settings(Job job)
        {
            MovieRecorderSettings movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name = "Walkthrough";
            movie.Enabled = true;
            movie.EncoderSettings = new CoreEncoderSettings
            {
                // the Recorder's H.264 encoder exists on Windows and macOS only; VP8 WebM everywhere (Tools/capture-walkthrough.sh
                // transcodes it to H.264 MP4 with ffmpeg)
                Codec = Application.platform == RuntimePlatform.LinuxEditor
                    ? CoreEncoderSettings.OutputCodec.WEBM
                    : CoreEncoderSettings.OutputCodec.MP4,
                EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High,
            };
            // the narration is laid in afterwards from its clips at the noted times (an Editor slower than real time
            // would otherwise record the voice out of step with the picture)
            movie.CaptureAudio = false;
            movie.ImageInputSettings = new GameViewInputSettings
            {
                OutputWidth = job.Width,
                OutputHeight = job.Height,
            };
            movie.OutputFile = job.Output;

            RecorderControllerSettings settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            settings.AddRecorderSettings(movie);
            settings.SetRecordModeToManual();
            settings.FrameRatePlayback = FrameRatePlayback.Constant;
            settings.FrameRate = Fps;
            settings.CapFrameRate = true;
            return settings;
        }
    }
}
