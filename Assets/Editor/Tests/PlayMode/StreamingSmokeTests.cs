using System.Collections;
using System.IO;
using System.Linq;
using Hypocycloid.Reverie.Controller;
using Hypocycloid.Reverie.Splats;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hypocycloid.Reverie.Tests
{
    // End to end smoke coverage: the scene streams in, clears its loading blur, keeps
    // Cinemachine in charge of the output camera, and renders a frame with real content.
    public sealed class StreamingSmokeTests : ReverieSceneTest
    {
        const int CaptureWidth = 960;
        const int CaptureHeight = 540;
        const int MinimumContrast = 40;
        const string CapturePath = "Logs/reverie-preview.png";

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator StreamingCoversTheSceneAndClearsTheLoadingBlur()
        {
            yield return WaitForStreamedScene();

            var streamer = Object.FindFirstObjectByType<GsplatLodStreamer>();
            var blur = Object.FindFirstObjectByType<SceneLoadingBlur>();
            Assert.That(streamer.LoadedFileCount, Is.GreaterThan(0), "No splat files were loaded.");
            Assert.That(streamer.ActiveSplatCount, Is.GreaterThan(0), "No splats are active.");
            Assert.That(streamer.SceneCoverageProgress, Is.GreaterThanOrEqualTo(0.99f));
            Assert.That(blur.IsBlurring, Is.False, "The loading blur never cleared.");
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator CinemachineDrivesTheOutputCamera()
        {
            Camera camera = Camera.main;
            var rig = camera.GetComponent<SceneCameraRig>();
            var brain = camera.GetComponent<CinemachineBrain>();
            Assert.That(rig, Is.Not.Null, "The main camera has no scene camera rig.");
            Assert.That(rig.View, Is.Not.Null, "The rig has no Cinemachine view assigned.");
            Assert.That(brain, Is.Not.Null, "The main camera has no Cinemachine brain.");

            yield return WaitUntil(
                // ActiveVirtualCamera is an interface, so compare through Object to keep Unity's
                // destroyed-object aware equality rather than a plain reference check.
                () => (Object)brain.ActiveVirtualCamera == rig.View,
                10f,
                () => $"the brain to activate the saved view (active={brain.ActiveVirtualCamera})"
            );
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator TheAutomaticOrbitMovesTheCamera()
        {
            yield return WaitForStreamedScene();

            Transform camera = Camera.main.transform;
            Vector3 startPosition = camera.position;
            Quaternion startRotation = camera.rotation;
            yield return WaitUntil(
                () => Vector3.Distance(startPosition, camera.position) > 0.1f
                    && Quaternion.Angle(startRotation, camera.rotation) > 1f,
                30f,
                () => $"the orbit to circle the subject (moved {Vector3.Distance(startPosition, camera.position):F2}m; "
                    + $"turned {Quaternion.Angle(startRotation, camera.rotation):F2} degrees)"
            );
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator TheRenderedFrameHasMeaningfulContrast()
        {
            yield return WaitForStreamedScene();

            Camera camera = Camera.main;
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            var target = new RenderTexture(CaptureWidth, CaptureHeight, 24, RenderTextureFormat.ARGB32);
            Texture2D frame = null;
            try
            {
                target.Create();
                camera.targetTexture = target;
                camera.aspect = (float)CaptureWidth / CaptureHeight;
                // The splat renderer needs a few frames to settle into the new target.
                for (int i = 0; i < 6; i++)
                    yield return null;

                RenderTexture.active = target;
                frame = new Texture2D(CaptureWidth, CaptureHeight, TextureFormat.RGB24, false);
                frame.ReadPixels(new Rect(0, 0, CaptureWidth, CaptureHeight), 0, 0);
                frame.Apply();

                Directory.CreateDirectory(Path.GetDirectoryName(CapturePath));
                File.WriteAllBytes(CapturePath, frame.EncodeToPNG());

                Color32[] pixels = frame.GetPixels32();
                int darkest = pixels.Min(pixel => pixel.r + pixel.g + pixel.b);
                int brightest = pixels.Max(pixel => pixel.r + pixel.g + pixel.b);
                Assert.That(
                    brightest - darkest,
                    Is.GreaterThanOrEqualTo(MinimumContrast),
                    $"The captured frame is flat (range {brightest - darkest}); see {CapturePath}."
                );
            }
            finally
            {
                RenderTexture.active = previousActive;
                camera.targetTexture = previousTarget;
                // Assigning Camera.aspect pins it; without this the editor keeps rendering the
                // capture's 16:9 frame into whatever viewport follows, stretching the image.
                camera.ResetAspect();
                if (frame != null)
                    Object.Destroy(frame);
                target.Release();
                Object.Destroy(target);
            }
        }
    }
}
