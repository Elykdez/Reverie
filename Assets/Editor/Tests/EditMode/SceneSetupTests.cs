using Gsplat;
using Hypocycloid.Reverie.Controller;
using Hypocycloid.Reverie.Splats;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Hypocycloid.Reverie.Tests
{
    // Composition of the shipped scene, checked without entering play mode.
    public sealed class SceneSetupTests
    {
        const string IslandName = "Island";

        SceneSetup[] previousSetup;

        [OneTimeSetUp]
        public void OpenTheReverieScene()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    Assert.Ignore("Save or discard the open scene first; these tests reopen the Reverie scene.");

            previousSetup = EditorSceneManager.GetSceneManagerSetup();
            EditorSceneManager.OpenScene(ReverieProject.ScenePath, OpenSceneMode.Single);
        }

        [OneTimeTearDown]
        public void RestoreTheEditorScenes()
        {
            if (previousSetup != null && previousSetup.Length > 0)
                EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
        }

        [Test]
        public void TheMainCameraRendersThroughUrp()
        {
            Assert.That(Camera.main, Is.Not.Null, "No camera is tagged MainCamera");
            Assert.That(
                Camera.main.GetComponent<UniversalAdditionalCameraData>(),
                Is.Not.Null,
                "The main camera has no URP camera data"
            );
        }

        [Test]
        public void TheMainCameraCarriesTheFullControlStack()
        {
            Assert.That(Camera.main.GetComponent<FlyCameraController>(), Is.Not.Null, "Manual camera controls missing");
            Assert.That(
                Camera.main.GetComponent<GravityOrbitCamera>(),
                Is.Not.Null,
                "Handheld gravity camera controls missing"
            );
            Assert.That(Camera.main.GetComponent<BackButtonControl>(), Is.Not.Null, "Back button control missing");
        }

        [Test]
        public void TheSavedCinemachineRigIsWiredUp()
        {
            var rig = Camera.main.GetComponent<SceneCameraRig>();
            Assert.That(rig, Is.Not.Null, "The main camera has no scene camera rig");
            Assert.That(rig.View, Is.Not.Null, "The rig has no Cinemachine view assigned");
            Assert.That(Camera.main.GetComponent<CinemachineBrain>(), Is.Not.Null, "The main camera has no brain");
        }

        [Test]
        public void TheGeneratedSkyboxIsAssigned()
        {
            Assert.That(RenderSettings.skybox, Is.Not.Null, "No skybox material");
            Assert.That(RenderSettings.skybox.shader.name, Is.EqualTo("Reverie/Cloud Sky"));
            Assert.That(
                RenderSettings.skybox.GetTexture("_MainTex"),
                Is.Not.Null,
                "The panoramic sky texture is missing"
            );
        }

        [Test]
        public void TheIslandHasACollisionMesh()
        {
            var island = GameObject.Find(IslandName);
            Assert.That(island, Is.Not.Null, IslandName + " is missing from the scene");
            Assert.That(island.GetComponentInChildren<MeshCollider>(), Is.Not.Null, "The island has no collision mesh");
        }

        [Test]
        public void TheIslandUsesTheFullPbrMaterial()
        {
            var island = GameObject.Find(IslandName);
            Assert.That(island, Is.Not.Null, IslandName + " is missing from the scene");
            Material rock = island.GetComponentInChildren<MeshRenderer>().sharedMaterial;
            Assert.That(rock, Is.Not.Null, "The island renderer has no material");
            // The rock shader is still being iterated on, so only the maps the lighting needs are
            // pinned here. AssetIntegrityTests covers whether that shader actually compiles.
            Assert.That(rock.shader, Is.Not.Null, "The island material has no shader");
            foreach (string map in new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap", "_OcclusionMap" })
                Assert.That(rock.GetTexture(map), Is.Not.Null, "Island PBR map missing: " + map);
        }

        [Test]
        public void TheSceneHasExactlyOneAudioListener() =>
            Assert.That(
                Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None),
                Has.Length.EqualTo(1),
                "Expected one audio listener"
            );

        [Test]
        public void AmbientMusicIsConfiguredForAutomatic2DLooping()
        {
            var music = GameObject.Find("Ambient Music")?.GetComponent<AudioSource>();
            Assert.That(music, Is.Not.Null, "Ambient Music is missing from the scene");
            Assert.That(music.clip, Is.Not.Null, "Ambient Music has no clip");
            Assert.That(music.playOnAwake, Is.True, "Ambient Music does not start on its own");
            Assert.That(music.loop, Is.True, "Ambient Music does not loop");
            Assert.That(music.spatialBlend, Is.Zero, "Ambient Music is not fully 2D");
            Assert.That(music.volume, Is.GreaterThan(0f), "Ambient Music is silent");
        }

        [Test]
        public void TheSplatStreamerHasAManifest()
        {
            var streamer = Object.FindFirstObjectByType<GsplatLodStreamer>();
            Assert.That(streamer, Is.Not.Null, "The scene has no splat streamer");
            Assert.That(streamer.ManifestAsset, Is.Not.Null, "The splat streamer has no LOD manifest");
        }

        [Test]
        public void TheSplatPreviewImported()
        {
            var gsplat = Object.FindFirstObjectByType<GsplatRenderer>();
            Assert.That(gsplat, Is.Not.Null, "The scene has no splat renderer");
            Assert.That(gsplat.GsplatAsset, Is.Not.Null, "The splat renderer has no asset");
            Assert.That(gsplat.GsplatAsset.SplatCount, Is.GreaterThan(0), "The imported splat asset is empty");
        }

        [Test]
        public void SplatShaderSettingsAreValid() =>
            Assert.That(GsplatSettings.Instance.Valid, Is.True, "Splat shader or material settings are invalid");

        [Test]
        public void TheSceneHierarchyIsClean()
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                ReverieProject.AssertHierarchyIsClean(root, ReverieProject.ScenePath);
        }
    }
}
