using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Hypocycloid.Reverie.Editor
{
    [CreateAssetMenu(fileName = "BuildSettings", menuName = "Reverie/Build Settings")]
    public class BuildConfig : ScriptableObject
    {
        const string GitBranchArguments = "rev-parse --abbrev-ref HEAD";

        [Header("Version")]
        [Tooltip("Major.minor. The patch field carries the build date.")]
        public string prefix = "0.1";

        [Tooltip("{0} prefix, {1} date, {2} branch, {3} revision.")]
        public string format = "{0}.{1}-{2}.{3}";

        [Tooltip("Stamp today's date on each build. Off freezes whatever date the version already carries.")]
        public bool isRecordDate = true;

        public static string GetGitBranchName()
        {
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "git",
                        Arguments = GitBranchArguments,
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WorkingDirectory = Directory.GetCurrentDirectory(),
                    },
                };

                process.Start();
                string output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();
                return output;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Build] Failed to read the Git branch: {exception.Message}");
                return "local";
            }
        }
    }
}
