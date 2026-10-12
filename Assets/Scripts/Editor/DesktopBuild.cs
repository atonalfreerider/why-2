using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Why.EditorTools
{
    /// <summary>
    /// Desktop player of both scenes, mainly for the runtime smoke test (<see cref="SmokeTest"/>):
    ///   Unity -batchmode -quit -projectPath . -buildTarget Linux64 -executeMethod Why.EditorTools.DesktopBuild.BuildLinuxFromCommandLine
    ///   Builds/Linux/why.x86_64 -smoketest [-screenshots DIR] [-xr]
    /// </summary>
    public static class DesktopBuild
    {
        public const string LinuxPlayer = "Builds/Linux/why.x86_64";

        static readonly string[] Scenes = { "Assets/Scenes/Why.unity", "Assets/Scenes/Economy.unity" };

        [MenuItem("Why/Build Linux Player")]
        public static void BuildLinux() => Build(BuildTarget.StandaloneLinux64, LinuxPlayer);

        public static void BuildLinuxFromCommandLine()
        {
            bool ok = false;
            try
            {
                ok = Build(BuildTarget.StandaloneLinux64, LinuxPlayer);
            }
            catch (Exception e)
            {
                Debug.LogError("[Why] desktop build failed: " + e);
            }

            EditorApplication.Exit(ok ? 0 : 1);
        }

        static bool Build(BuildTarget target, string path)
        {
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = path,
                target = target,
                options = BuildOptions.None,
            });
            BuildSummary s = report.summary;
            Debug.Log($"[Why] desktop build {s.result}: {path} ({s.totalErrors} errors, {s.totalTime.TotalMinutes:0.0} min)");
            return s.result == BuildResult.Succeeded;
        }
    }
}
