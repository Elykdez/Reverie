using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hypocycloid.Reverie.Tests
{
    public sealed class AmbientMusicTests : ReverieSceneTest
    {
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator AmbientMusicPlaysAndSurvivesItsLoopBoundary()
        {
            var music = GameObject.Find("Ambient Music")?.GetComponent<AudioSource>();
            Assert.That(music, Is.Not.Null, "Ambient Music is missing from the scene.");
            yield return WaitUntil(() => music.isPlaying, 15f, () => "ambient music to start on its own");

            // Seek near the end to exercise the real wrap without waiting out the whole track.
            music.time = Mathf.Max(0f, music.clip.length - 1f);
            float seeked = music.time;
            var samples = new float[256];
            float peak = 0f;

            yield return WaitUntil(
                () =>
                {
                    music.GetOutputData(samples, 0);
                    peak = Mathf.Max(peak, samples.Max(sample => Mathf.Abs(sample)));
                    return music.time < seeked;
                },
                30f,
                () => $"the loop to wrap (playhead {music.time:F1}s of {music.clip.length:F1}s)"
            );

            Assert.That(music.isPlaying, Is.True, "Playback stopped at the loop boundary.");
            Assert.That(peak, Is.GreaterThan(0.0001f), "The music source produced no audible output.");
        }
    }
}
