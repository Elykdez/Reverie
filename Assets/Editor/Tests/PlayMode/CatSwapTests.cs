using System.Collections;
using System.Linq;
using Gsplat;
using Hypocycloid.Reverie.Controller;
using Hypocycloid.Reverie.Splats;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hypocycloid.Reverie.Tests
{
    public sealed class CatSwapTests : ReverieSceneTest
    {
        [UnityTest, Timeout(600000)]
        public IEnumerator BothCatsStreamAndSwapWithoutKeepingThePreviousChunks()
        {
            var switcher = Object.FindFirstObjectByType<CatSwitcher>();
            Assert.That(switcher, Is.Not.Null);
            yield return WaitForStreamedScene();
            Assert.That(switcher.CurrentIndex, Is.InRange(0, 1));
            var streamer = Object.FindFirstObjectByType<GsplatLodStreamer>();
            var blur = Object.FindFirstObjectByType<SceneLoadingBlur>();
            var music = GameObject.Find("Ambient Music").GetComponent<AudioSource>();
            for (int i = 0; i < 2; i++)
            {
                yield return WaitUntil(() => switcher.CanSwap, 10f, () => "cat swap cooldown");
                int previous = switcher.CurrentIndex;
                var oldChunks = streamer.GetComponentsInChildren<GsplatRenderer>()
                    .Where(r => r != streamer.PreviewRenderer).ToArray();
                Assert.That(switcher.SwapCat(), Is.True);
                Assert.That(switcher.CurrentIndex, Is.Not.EqualTo(previous));
                Assert.That(streamer.ManifestAsset, Is.EqualTo(switcher.Collection.cats[switcher.CurrentIndex].manifest));
                Assert.That(blur.IsBlurring, Is.True, "A swap should conceal the streaming transition.");
                Assert.That(switcher.SwapCat(), Is.False, "A second request during reveal must be ignored.");
                yield return null;
                yield return null;
                Assert.That(oldChunks.All(r => r == null), Is.True, "Outgoing chunks survived the swap.");
                yield return WaitForStreamedScene();
                Assert.That(music.isPlaying, Is.True);
                Assert.That(Camera.main.GetComponent<SceneCameraRig>().View, Is.Not.Null);
                Assert.That(Object.FindObjectsByType<GsplatLodStreamer>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
            }
        }
    }
}
