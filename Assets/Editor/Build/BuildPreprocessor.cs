using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Hypocycloid.Reverie.Editor
{
    // Stamps every build with prefix.date-branch.revision, so an APK on a device can be
    // traced back to the day and branch it came from without tagging anything by hand.
    public class BuildPreprocessor : IPreprocessBuildWithReport
    {
        const string ConfigPath = "Assets/Editor/BuildSettings.asset";
        const string VersionDateFormat = "yyyyMMdd";
        const string VersionCodeDateFormat = "yyMMdd";
        const int RevisionsPerDay = 100;
        const string ExtendedSemanticVersionPattern =
            @"^(?<major>0|[1-9][0-9]*)\.(?<minor>0|[1-9][0-9]*)\.(?<patch>0|[1-9][0-9]*)(?:-(?<prerelease>(?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*)(?:\.(?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*))*))?(?:\+(?<buildmetadata>[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?$";

        public int callbackOrder => 0;

        public static BuildConfig Config => AssetDatabase.LoadAssetAtPath<BuildConfig>(ConfigPath);

        public void OnPreprocessBuild(BuildReport report) => SetVersion(report.summary.platform);

        public static void SetVersion(BuildTarget buildTarget)
        {
            BuildConfig config = Config;
            if (config == null)
                throw new FileNotFoundException(
                    $"Build config not found: {ConfigPath}",
                    ConfigPath
                );

            string today = DateTime.Now.ToString(VersionDateFormat, CultureInfo.InvariantCulture);
            string versionDate = today;
            string prefix = config.prefix;
            int revision = 0;

            string previous = PlayerSettings.bundleVersion?.Trim() ?? string.Empty;
            if (
                TryReadVersion(
                    previous,
                    out string oldPrefix,
                    out string oldDate,
                    out int oldRevision
                )
            )
            {
                prefix = oldPrefix;
                if (!string.IsNullOrEmpty(oldDate) && (oldDate == today || !config.isRecordDate))
                    versionDate = oldDate;
                if (oldDate == versionDate)
                    revision = oldRevision + 1;
            }

            string branch = SanitizeSemVerIdentifier(BuildConfig.GetGitBranchName());
            string version = string.Format(
                CultureInfo.InvariantCulture,
                config.format,
                prefix,
                versionDate,
                branch,
                revision
            );

            if (!Regex.IsMatch(version, ExtendedSemanticVersionPattern))
                throw new InvalidOperationException(
                    $"Generated build version is not SemVer: {version}"
                );

            PlayerSettings.bundleVersion = version;
            if (buildTarget == BuildTarget.Android)
                PlayerSettings.Android.bundleVersionCode = NextVersionCode(versionDate, revision);

            Debug.Log(
                $"[Build] version={PlayerSettings.bundleVersion}; "
                    + $"versionCode={PlayerSettings.Android.bundleVersionCode}"
            );
        }

        // Android refuses to install over a package with a higher code, so the value has to
        // rise monotonically. Date plus revision does that on its own; the comparison covers
        // a version string that was edited by hand and lost the build history.
        static int NextVersionCode(string versionDate, int revision)
        {
            int code = PlayerSettings.Android.bundleVersionCode + 1;
            if (
                DateTime.TryParseExact(
                    versionDate,
                    VersionDateFormat,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateTime parsed
                )
                && int.TryParse(
                    parsed.ToString(VersionCodeDateFormat, CultureInfo.InvariantCulture),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out int dateCode
                )
            )
            {
                code = Math.Max(
                    code,
                    dateCode * RevisionsPerDay + Math.Min(revision, RevisionsPerDay - 1)
                );
            }
            return code;
        }

        static bool TryReadVersion(
            string version,
            out string prefix,
            out string date,
            out int revision
        )
        {
            prefix = string.Empty;
            date = string.Empty;
            revision = 0;

            Match match = Regex.Match(version, ExtendedSemanticVersionPattern);
            if (!match.Success)
                return false;

            prefix = $"{match.Groups["major"].Value}.{match.Groups["minor"].Value}";
            string patch = match.Groups["patch"].Value;
            if (IsBuildDate(patch))
                date = patch;

            string prerelease = match.Groups["prerelease"].Value;
            if (!string.IsNullOrEmpty(prerelease))
            {
                string[] identifiers = prerelease.Split('.');
                int.TryParse(
                    identifiers[^1],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out revision
                );
            }
            return true;
        }

        static bool IsBuildDate(string value) =>
            value.Length == 8
            && DateTime.TryParseExact(
                value,
                VersionDateFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _
            );

        static string SanitizeSemVerIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "local";

            var builder = new StringBuilder(value.Length);
            bool previousWasHyphen = false;
            foreach (char c in value.Trim())
            {
                if (IsAsciiAlphaNumeric(c) || c == '-')
                {
                    builder.Append(c);
                    previousWasHyphen = c == '-';
                    continue;
                }
                if (previousWasHyphen)
                    continue;
                builder.Append('-');
                previousWasHyphen = true;
            }

            string identifier = builder.ToString().Trim('-');
            if (string.IsNullOrEmpty(identifier))
                return "local";
            return IsNumericWithLeadingZero(identifier) ? $"branch-{identifier}" : identifier;
        }

        static bool IsAsciiAlphaNumeric(char c) =>
            c is >= '0' and <= '9' or >= 'A' and <= 'Z' or >= 'a' and <= 'z';

        static bool IsNumericWithLeadingZero(string value)
        {
            if (value.Length < 2 || value[0] != '0')
                return false;
            foreach (char c in value)
                if (c < '0' || c > '9')
                    return false;
            return true;
        }
    }
}
