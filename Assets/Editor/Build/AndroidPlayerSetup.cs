using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hypocycloid.Reverie.Editor
{
    /// <summary>
    /// Applies the Android player settings this project expects. Written as a script rather
    /// than hand-edited into ProjectSettings.asset so it stays reproducible and can run from
    /// CI through -executeMethod.
    ///
    /// Keystore secrets are read from the environment so they never land in the repository.
    /// Leave them unset to fill the passwords in via the Editor's publishing settings instead.
    /// </summary>
    public static class AndroidPlayerSetup
    {
        const string ApplicationIdentifier = "com.hypocycloid.reverie";
        // Machine-specific, so the environment wins over the checked-in default. A clone on any
        // other machine has to point at its own copy rather than edit this file.
        const string DefaultKeystorePath = @"C:\Users\Elykdez\hypozykloid.keystore";
        const string EnvKeystorePath = "Hypocycloid.Reverie_KEYSTORE_PATH";

        static string KeystorePath
        {
            get
            {
                string configured = Environment.GetEnvironmentVariable(EnvKeystorePath);
                return string.IsNullOrWhiteSpace(configured) ? DefaultKeystorePath : configured;
            }
        }

        const string EnvKeystorePass = "Hypocycloid.Reverie_KEYSTORE_PASS";
        const string EnvKeyaliasName = "Hypocycloid.Reverie_KEYALIAS_NAME";
        const string EnvKeyaliasPass = "Hypocycloid.Reverie_KEYALIAS_PASS";

        [MenuItem("Reverie/Android/Apply Player Settings")]
        public static void Apply()
        {
            ApplyOrientation();
            ApplyIdentity();
            ApplyScripting();
            ApplyGraphics();
            ApplyKeystore();

            AssetDatabase.SaveAssets();
            Debug.Log("[Android] Player settings applied.");
        }

        // The camera reads device attitude, so letting the OS rotate the screen underneath
        // would re-map the tilt axes mid-gesture. See GravityOrbitCamera.
        static void ApplyOrientation()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
        }

        // companyName and productName are left alone; they are set in the Editor.
        static void ApplyIdentity() =>
            PlayerSettings.SetApplicationIdentifier(
                NamedBuildTarget.Android,
                ApplicationIdentifier
            );

        static void ApplyScripting()
        {
            PlayerSettings.SetScriptingBackend(
                NamedBuildTarget.Android,
                ScriptingImplementation.IL2CPP
            );
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            // Mirror the desktop build so platform-conditional code compiles the same way
            // rather than silently diverging.
            PlayerSettings.SetApiCompatibilityLevel(
                NamedBuildTarget.Android,
                PlayerSettings.GetApiCompatibilityLevel(NamedBuildTarget.Standalone)
            );
            PlayerSettings.SetScriptingDefineSymbols(
                NamedBuildTarget.Android,
                PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Standalone)
            );

            // Pinned rather than left on Automatic so the build does not drift onto whichever
            // SDK platform happens to be installed. 34, 35 and 36 are present locally.
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel35;
        }

        // The splat renderer needs compute, so Vulkan leads and GLES3 is the fallback.
        static void ApplyGraphics()
        {
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(
                BuildTarget.Android,
                new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 }
            );
        }

        static void ApplyKeystore()
        {
            string aliasName = Environment.GetEnvironmentVariable(EnvKeyaliasName);
            string keystorePass = Environment.GetEnvironmentVariable(EnvKeystorePass);
            string aliasPass = Environment.GetEnvironmentVariable(EnvKeyaliasPass);
            bool hasEnvironmentCredentials =
                File.Exists(KeystorePath)
                && !string.IsNullOrEmpty(aliasName)
                && !string.IsNullOrEmpty(keystorePass);

            if (!hasEnvironmentCredentials)
            {
                // Naming a keystore without a password makes Unity refuse the build outright
                // rather than fall back, so only keep custom signing when it can succeed:
                // either passwords were entered in Publishing Settings, or use the debug key.
                if (string.IsNullOrEmpty(PlayerSettings.Android.keystorePass))
                {
                    PlayerSettings.Android.useCustomKeystore = false;
                    Debug.LogWarning(
                        $"[Android] Signing with the debug keystore. Set {EnvKeyaliasName}, "
                            + $"{EnvKeystorePass} and {EnvKeyaliasPass} to sign with {KeystorePath}."
                    );
                }
                return;
            }

            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = KeystorePath;
            PlayerSettings.Android.keystorePass = keystorePass;
            PlayerSettings.Android.keyaliasName = aliasName;
            PlayerSettings.Android.keyaliasPass = string.IsNullOrEmpty(aliasPass)
                ? keystorePass
                : aliasPass;
        }
    }
}
