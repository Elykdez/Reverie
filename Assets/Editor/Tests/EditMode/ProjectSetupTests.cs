using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Hypocycloid.Reverie.Tests
{
    // Project-level settings that the Android player and the single-scene layout depend on.
    public sealed class ProjectSetupTests
    {
        [Test]
        public void AndroidIsTheActiveBuildTarget() =>
            Assert.That(EditorUserBuildSettings.activeBuildTarget, Is.EqualTo(BuildTarget.Android));

        [Test]
        public void UniversalRenderPipelineIsTheDefault() =>
            Assert.That(
                GraphicsSettings.defaultRenderPipeline,
                Is.InstanceOf<UniversalRenderPipelineAsset>(),
                "URP is not assigned as the default render pipeline"
            );

        [Test]
        public void TheProjectContainsExactlyOneScene()
        {
            string[] scenes = Directory.GetFiles("Assets", "*.unity", SearchOption.AllDirectories);
            Assert.That(scenes, Has.Length.EqualTo(1), "Found: " + string.Join(", ", scenes));
        }

        [Test]
        public void TheReverieSceneIsTheOnlyEnabledBuildScene()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            Assert.That(scenes, Has.Length.EqualTo(1), "Expected one scene in build settings");
            Assert.That(scenes[0].path, Is.EqualTo(ReverieProject.ScenePath));
            Assert.That(scenes[0].enabled, Is.True, "The Reverie scene is disabled in build settings");
        }

        [Test]
        public void RetiredServiceScriptsAreGone()
        {
            Assert.That(Directory.Exists("Assets/Scripts/Dialogue"), Is.False, "Dialogue scripts remain");
            Assert.That(Directory.Exists("Assets/Scripts/Captura"), Is.False, "VL service scripts remain");
        }
    }
}
