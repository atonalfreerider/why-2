using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEditor.SceneManagement;
using UnityEngine;
using Why.Director;

namespace Why.EditorTools
{
    /// <summary>
    /// Records the narrated walkthrough with the Unity Recorder: opens the universe, sizes the Game view (landscape
    /// 1920 x 1080 or portrait 1080 x 1920, whose layout the graph re-flows to), plays the guided tour from its first stop
    /// (the Big Bang) and writes an H.264 MP4 with the narration at 30 fps.
    ///
    /// Menu: Why > Capture. Command line (needs the Editor's window, not -batchmode):
    ///   Unity -projectPath . -executeMethod Why.EditorTools.WalkthroughCapture.FromCommandLine
    ///         -captureOrientation landscape|portrait -captureSeconds 110 -captureOut Captures/why-landscape
    /// </summary>
    [InitializeOnLoad]
    public static class WalkthroughCapture
    {
        const string Key = "Why.WalkthroughCapture";

        [Serializable]
        sealed class Job
        {
            public bool Portrait;
            public float Seconds;
            public string Output;
            public bool Exit;
        }

        static RecorderController controller;
        static int phase, frames;
        static double stopAt;

        static WalkthroughCapture()
        {
            EditorApplication.update += Tick;
        }

        [MenuItem("Why/Capture/Walkthrough opening, landscape 1920x1080")]
        static void Landscape() => Begin(new Job { Portrait = false, Seconds = 110, Output = "Captures/why-landscape" });

        [MenuItem("Why/Capture/Walkthrough opening, portrait 1080x1920")]
        static void Portrait() => Begin(new Job { Portrait = true, Seconds = 110, Output = "Captures/why-portrait" });

        public static void FromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            string Arg(string name, string fallback)
            {
                int i = Array.IndexOf(args, name);
                return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
            }

            bool portrait = Arg("-captureOrientation", "landscape") == "portrait";
            Begin(new Job
            {
                Portrait = portrait,
                Seconds = float.Parse(Arg("-captureSeconds", "110"), System.Globalization.CultureInfo.InvariantCulture),
                Output = Arg("-captureOut", portrait ? "Captures/why-portrait" : "Captures/why-landscape"),
                Exit = true,
            });
        }

        static void Begin(Job job)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(job.Output)) ?? ".");
            SessionState.SetString(Key, JsonUtility.ToJson(job));
            phase = 0;
            EditorSceneManager.OpenScene("Assets/Scenes/Why.unity");
            EditorApplication.EnterPlaymode();
        }

        static Job Current
        {
            get
            {
                string json = SessionState.GetString(Key, "");
                return string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<Job>(json);
            }
        }

        static void Tick()
        {
            Job job = Current;
            if (job == null || !EditorApplication.isPlaying) return;
            GraphRoot root = GraphRoot.Instance;
            if (root == null || !root.IsLoaded) return;

            switch (phase)
            {
                case 0:
                    // size the Game view first, so the layout (and a portrait re-flow) settles before anything plays
                    controller = new RecorderController(Settings(job));
                    controller.PrepareRecording();
                    frames = 0;
                    phase = 1;
                    break;
                case 1:
                    if (++frames < 45) break;
                    DirectorMode director = root.Modules.OfType<DirectorMode>().FirstOrDefault();
                    director?.PlayFrom(0);
                    controller.StartRecording();
                    stopAt = EditorApplication.timeSinceStartup + job.Seconds;
                    Debug.Log($"[Why] capture: recording {job.Output}.mp4 ({(job.Portrait ? "1080x1920" : "1920x1080")}, {job.Seconds} s)");
                    phase = 2;
                    break;
                case 2:
                    if (EditorApplication.timeSinceStartup < stopAt) break;
                    controller.StopRecording();
                    Debug.Log($"[Why] capture: wrote {job.Output}.mp4");
                    SessionState.EraseString(Key);
                    phase = 3;
                    EditorApplication.ExitPlaymode();
                    if (job.Exit) EditorApplication.delayCall += () => EditorApplication.Exit(0);
                    break;
            }
        }

        static RecorderControllerSettings Settings(Job job)
        {
            MovieRecorderSettings movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name = "Walkthrough";
            movie.Enabled = true;
            movie.EncoderSettings = new CoreEncoderSettings
            {
                Codec = CoreEncoderSettings.OutputCodec.MP4,
                EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High,
            };
            movie.CaptureAudio = true;
            movie.ImageInputSettings = new GameViewInputSettings
            {
                OutputWidth = job.Portrait ? 1080 : 1920,
                OutputHeight = job.Portrait ? 1920 : 1080,
            };
            movie.OutputFile = job.Output;

            RecorderControllerSettings settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            settings.AddRecorderSettings(movie);
            settings.SetRecordModeToManual();
            settings.FrameRatePlayback = FrameRatePlayback.Constant;
            settings.FrameRate = 30;
            settings.CapFrameRate = true;
            return settings;
        }
    }
}
