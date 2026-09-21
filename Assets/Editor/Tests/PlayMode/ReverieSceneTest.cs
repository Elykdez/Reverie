using System;
using System.Collections;
using Hypocycloid.Reverie.Controller;
using Hypocycloid.Reverie.Splats;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Hypocycloid.Reverie.Tests
{
    // Base fixture for the play mode suites. Loading and streaming the scene costs tens of
    // seconds, so the scene is loaded once and reused by every test in the run.
    public abstract class ReverieSceneTest
    {
        protected const string ScenePath = "Assets/Scenes/Reverie.unity";
        protected const float StreamingTimeout = 180f;

        [UnitySetUp]
        public IEnumerator LoadTheReverieScene()
        {
            if (SceneManager.GetActiveScene().path != ScenePath)
                yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            yield return WaitUntil(() => Camera.main != null, 30f, () => "the main camera to be present");
        }

        protected static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds, Func<string> describe)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline)
                    Assert.Fail($"Timed out after {timeoutSeconds:F0}s waiting for {describe()}.");
                yield return null;
            }
        }

        // Splats stream in progressively and the loading blur lifts once coverage is complete,
        // so anything that inspects the rendered scene waits on both.
        protected static IEnumerator WaitForStreamedScene()
        {
            var streamer = UnityEngine.Object.FindFirstObjectByType<GsplatLodStreamer>();
            Assert.That(streamer, Is.Not.Null, "The scene has no splat streamer.");
            var blur = UnityEngine.Object.FindFirstObjectByType<SceneLoadingBlur>();
            Assert.That(blur, Is.Not.Null, "The scene has no loading blur.");
            yield return WaitUntil(
                () => streamer.HasVisibleData && streamer.SceneCoverageProgress >= 0.99f && !blur.IsBlurring,
                StreamingTimeout,
                () => $"splat coverage to complete (coverage={streamer.SceneCoverageProgress:F3}; "
                    + $"visible={streamer.HasVisibleData}; blurring={blur.IsBlurring})"
            );
        }
    }
}
