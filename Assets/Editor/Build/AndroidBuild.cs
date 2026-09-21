using System;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Hypocycloid.Reverie.Editor
{
    // Command line entry point:
    // Unity -batchmode -quit -projectPath . -executeMethod Hypocycloid.Reverie.Editor.AndroidBuild.Build
    public static class AndroidBuild
    {
        public const string ScenePath = "Assets/Scenes/Reverie.unity";
        const string OutputPath = "Builds/Android/Reverie.apk";
        const string ReportPath = "Logs/android-build-result.txt";

        [MenuItem("Reverie/Android/Build APK")]
        public static void Build()
        {
            RequireUsableSigning();

            AddressableAssetSettings.BuildPlayerContent(out var addressables);
            if (!string.IsNullOrEmpty(addressables.Error))
                throw new BuildFailedException("Addressables build failed: " + addressables.Error);

            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));

            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = OutputPath,
                    target = BuildTarget.Android,
                    options = BuildOptions.Development,
                });
            }
            catch (Exception exception)
            {
                // BuildPlayer throws instead of returning a report when the build backend itself
                // fails, so the report is written here too or CI is left with no record at all.
                File.WriteAllText(ReportPath, $"Threw; {exception.GetType().Name}: {exception.Message}\n");
                throw;
            }

            var summary = report.summary;
            File.WriteAllText(ReportPath,
                $"{summary.result}; errors={summary.totalErrors}; warnings={summary.totalWarnings}; " +
                $"bytes={summary.totalSize}; duration={summary.totalTime}\n");
            if (summary.result != BuildResult.Succeeded)
                throw new BuildFailedException($"Android build {summary.result} with {summary.totalErrors} errors");

            Debug.Log($"[Build] Android APK written to {OutputPath} ({summary.totalSize} bytes)");
        }

        // Unity reports a half-configured keystore as a bare "Can not sign the application" after
        // it has already spent the whole build, so the same conditions are checked up front.
        static void RequireUsableSigning()
        {
            if (!PlayerSettings.Android.useCustomKeystore)
            {
                Debug.Log("[Build] Signing with the debug keystore.");
                return;
            }

            string keystore = PlayerSettings.Android.keystoreName;
            if (string.IsNullOrEmpty(keystore))
                throw new BuildFailedException(
                    "Custom keystore signing is enabled but no keystore is set. "
                        + "Set one in Project Settings > Player > Publishing Settings, or disable Custom Keystore.");

            // Unity 6 keeps keystores in a dedicated location and stores only the file name, so a
            // bare name is not project-relative and cannot be checked here. Only a rooted path can.
            if (Path.IsPathRooted(keystore) && !File.Exists(keystore))
                throw new BuildFailedException(
                    $"Keystore '{keystore}' does not exist on this machine. Point "
                        + "Hypocycloid.Reverie_KEYSTORE_PATH at your copy and rerun Reverie/Android/Apply Player Settings.");

            if (string.IsNullOrEmpty(PlayerSettings.Android.keystorePass)
                || string.IsNullOrEmpty(PlayerSettings.Android.keyaliasName)
                || string.IsNullOrEmpty(PlayerSettings.Android.keyaliasPass))
                throw new BuildFailedException(
                    "Custom keystore signing is enabled but the keystore or alias password is empty. "
                        + "Unity does not persist these between sessions: either enter them in "
                        + "Publishing Settings, or set Hypocycloid.Reverie_KEYSTORE_PASS, "
                        + "Hypocycloid.Reverie_KEYALIAS_NAME and Hypocycloid.Reverie_KEYALIAS_PASS "
                        + "and run Reverie/Android/Apply Player Settings first.");

            Debug.Log($"[Build] Signing with '{keystore}' alias '{PlayerSettings.Android.keyaliasName}'.");
        }
    }
}
