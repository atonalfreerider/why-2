using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;

namespace Why.EditorTools
{
    /// <summary>
    /// Meta Quest build: configures OpenXR (Meta Quest feature, Touch controller profiles, single-pass instanced) for
    /// Android only, sets the Android player settings Quest needs, and builds an APK of both scenes. Desktop keeps
    /// running without XR (no loader is assigned to Standalone).
    ///
    /// Menu: Why > Quest > Configure XR / Build APK. Batch:
    ///   Unity -batchmode -quit -projectPath . -buildTarget Android -executeMethod Why.EditorTools.QuestBuild.BuildFromCommandLine
    /// writes Builds/Quest/why.apk (override with -questApk path).
    /// </summary>
    public static class QuestBuild
    {
        public const string PackageId = "io.primitive.why";
        public const string DefaultApk = "Builds/Quest/why.apk";
        const string XrFolder = "Assets/XR";
        const string XrSettingsPath = XrFolder + "/XRGeneralSettingsPerBuildTarget.asset";

        static readonly string[] Scenes = { "Assets/Scenes/Why.unity", "Assets/Scenes/Economy.unity" };

        [MenuItem("Why/Quest/Configure XR")]
        public static void Configure()
        {
            ConfigurePlayer();
            ConfigureXrManagement();
            ConfigureOpenXr();
            AssetDatabase.SaveAssets();
            Debug.Log("[Why] Quest: XR and Android settings configured");
        }

        [MenuItem("Why/Quest/Build APK")]
        public static void BuildMenu() => Build(DefaultApk);

        public static void BuildFromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-questApk");
            string apk = i >= 0 && i + 1 < args.Length ? args[i + 1] : DefaultApk;
            bool configureOnly = args.Contains("-questConfigureOnly");
            try
            {
                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                {
                    EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
                }

                if (configureOnly)
                {
                    Configure();
                    EditorApplication.Exit(0);
                    return;
                }

                bool ok = Build(apk);
                EditorApplication.Exit(ok ? 0 : 1);
            }
            catch (Exception e)
            {
                Debug.LogError("[Why] Quest build failed: " + e);
                EditorApplication.Exit(1);
            }
        }

        public static bool Build(string apk)
        {
            Configure();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(apk)) ?? ".");
            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
            EditorUserBuildSettings.buildAppBundle = false;
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = apk,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary s = report.summary;
            Debug.Log($"[Why] Quest build {s.result}: {apk} ({s.totalSize / (1024f * 1024f):0.0} MB, {s.totalErrors} errors, " +
                      $"{s.totalTime.TotalMinutes:0.0} min)");
            return s.result == BuildResult.Succeeded;
        }

        static void ConfigurePlayer()
        {
            NamedBuildTarget android = NamedBuildTarget.Android;
            PlayerSettings.productName = "Why";
            PlayerSettings.companyName = "Primitive";
            PlayerSettings.SetApplicationIdentifier(android, PackageId);
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.Android.forceInternetPermission = false;
            PlayerSettings.SetManagedStrippingLevel(android, ManagedStrippingLevel.Minimal);
            EditorUserBuildSettings.SetPlatformSettings("Android", "AndroidBuildType", "Release");
        }

        static void ConfigureXrManagement()
        {
            if (!AssetDatabase.IsValidFolder(XrFolder)) AssetDatabase.CreateFolder("Assets", "XR");

            EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.settingsKey, out XRGeneralSettingsPerBuildTarget perTarget);
            if (perTarget == null)
            {
                perTarget = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(XrSettingsPath);
                if (perTarget == null)
                {
                    perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                    AssetDatabase.CreateAsset(perTarget, XrSettingsPath);
                }

                EditorBuildSettings.AddConfigObject(XRGeneralSettings.settingsKey, perTarget, true);
            }

            foreach (BuildTargetGroup group in new[] { BuildTargetGroup.Android, BuildTargetGroup.Standalone })
            {
                if (!perTarget.HasSettingsForBuildTarget(group)) perTarget.CreateDefaultSettingsForBuildTarget(group);
                if (!perTarget.HasManagerSettingsForBuildTarget(group)) perTarget.CreateDefaultManagerSettingsForBuildTarget(group);
            }

            // Quest starts in XR; the desktop never loads a provider
            XRGeneralSettings androidSettings = perTarget.SettingsForBuildTarget(BuildTargetGroup.Android);
            androidSettings.InitManagerOnStart = true;
            XRManagerSettings androidManager = perTarget.ManagerSettingsForBuildTarget(BuildTargetGroup.Android);
            if (!androidManager.activeLoaders.Any(l => l != null && l.GetType().FullName == typeof(OpenXRLoader).FullName))
            {
                XRPackageMetadataStore.AssignLoader(androidManager, typeof(OpenXRLoader).FullName, BuildTargetGroup.Android);
            }

            XRGeneralSettings standalone = perTarget.SettingsForBuildTarget(BuildTargetGroup.Standalone);
            standalone.InitManagerOnStart = false;

            EditorUtility.SetDirty(perTarget);
            EditorUtility.SetDirty(androidSettings);
            EditorUtility.SetDirty(androidManager);
        }

        static void ConfigureOpenXr()
        {
            FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);
            OpenXRSettings settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (settings == null)
            {
                throw new InvalidOperationException("OpenXR settings for Android are missing (is com.unity.xr.openxr installed?)");
            }

            settings.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
            settings.depthSubmissionMode = OpenXRSettings.DepthSubmissionMode.Depth16Bit;

            foreach (OpenXRFeature f in settings.GetFeatures())
            {
                bool want = f is MetaQuestFeature || f is OculusTouchControllerProfile ||
                            f is MetaQuestTouchPlusControllerProfile || f is MetaQuestTouchProControllerProfile;
                if (want && !f.enabled)
                {
                    f.enabled = true;
                    EditorUtility.SetDirty(f);
                }
            }

            EditorUtility.SetDirty(settings);
        }
    }
}
