using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Hypocycloid.Reverie.Editor
{
    public static class IOSBuild
    {
        const string OutputPath = "Builds/iOS/Reverie";
        const string ReportPath = "Logs/ios-build-result.txt";

        [MenuItem("Reverie/iOS/Export Xcode Project")]
        public static void Export()
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.hypocycloid.reverie");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.iOS.buildNumber = "1";

            // The build preprocessor sets an iOS version; preserve Android's release tag locally.
            string originalVersion = PlayerSettings.bundleVersion;
            try
            {
                AddressableAssetSettings.BuildPlayerContent(out var addressables);
                if (!string.IsNullOrEmpty(addressables.Error))
                    throw new BuildFailedException("Addressables build failed: " + addressables.Error);

                Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
                Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { AndroidBuild.ScenePath },
                    locationPathName = OutputPath,
                    target = BuildTarget.iOS,
                    options = BuildOptions.None,
                });
                var summary = report.summary;
                File.WriteAllText(ReportPath,
                    $"{summary.result}; errors={summary.totalErrors}; warnings={summary.totalWarnings}; " +
                    $"bytes={summary.totalSize}; duration={summary.totalTime}\n");
                if (summary.result != BuildResult.Succeeded)
                    throw new BuildFailedException($"iOS export {summary.result} with {summary.totalErrors} errors");

                Debug.Log($"[Build] iOS Xcode project written to {OutputPath}");
            }
            finally
            {
                PlayerSettings.bundleVersion = originalVersion;
            }
        }
    }
}
