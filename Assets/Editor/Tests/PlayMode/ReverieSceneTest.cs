using System;
using System.Collections;
using Hypocycloid.Reverie.Controller;
using Hypocycloid.Splats;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Hypocycloid.Reverie.Tests
{
    // Base fixture for the scene suites. They compile into the editor assembly, so each test
    // enters Play Mode here, and the runner exits Play Mode after every [UnityTest]. The scene
    // is therefore loaded per test, which costs tens of seconds each time.
    public abstract class ReverieSceneTest
    {
        protected const string ScenePath = "Assets/Scenes/Reverie.unity";
        protected const float StreamingTimeout = 180f;

        [UnitySetUp]
        public IEnumerator LoadTheReverieScene()
        {
            if (!Application.isPlaying)
            {
                // The runner stalls if it waits for a domain reload that the project settings skip.
                bool reloadsDomain = !EditorSettings.enterPlayModeOptionsEnabled
                    || (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) == 0;
                yield return new EnterPlayMode(reloadsDomain);
            }
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

        // Under the editor test runner `yield return null` waits one editor tick, and several
        // ticks can pass within one player frame, so frame-counted waits use this instead.
        protected static IEnumerator NextFrame()
        {
            int frame = Time.frameCount;
            while (Time.frameCount == frame)
                yield return null;
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
