using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gsplat;
using Hypocycloid.Reverie.Controller;
using Hypocycloid.Splats;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hypocycloid.Reverie.Tests
{
    public sealed class SplatSwapTests : ReverieSceneTest
    {
        [UnityTest, Timeout(600000)]
        public IEnumerator EverySplatStreamsAndSwapsWithoutKeepingThePreviousChunks()
        {
            var switcher = Object.FindFirstObjectByType<SplatSwitcher>();
            Assert.That(switcher, Is.Not.Null);
            yield return WaitForStreamedScene();
            int count = switcher.Collection.splats.Length;
            Assert.That(switcher.CurrentIndex, Is.InRange(0, count - 1));
            var streamer = Object.FindFirstObjectByType<GsplatLodStreamer>();
            var blur = Object.FindFirstObjectByType<SceneLoadingBlur>();
            var music = GameObject.Find("AudioManager").GetComponent<AudioSource>();
            var shown = new HashSet<int> { switcher.CurrentIndex };
            // The first swap takes the random path; each later one selects a splat not shown yet,
            // so every entry in the collection streams once.
            while (shown.Count < count)
            {
                yield return WaitUntil(() => switcher.CanSwap, 10f, () => "splat swap cooldown");
                int previous = switcher.CurrentIndex;
                var oldChunks = streamer.GetComponentsInChildren<GsplatRenderer>()
                    .Where(r => r != streamer.PreviewRenderer).ToArray();
                bool swapped = shown.Count == 1
                    ? switcher.SwapSplat()
                    : switcher.SelectSplat(Enumerable.Range(0, count).First(index => !shown.Contains(index)));
                Assert.That(swapped, Is.True);
                Assert.That(switcher.CurrentIndex, Is.Not.EqualTo(previous));
                shown.Add(switcher.CurrentIndex);
                Assert.That(streamer.ManifestAsset, Is.EqualTo(switcher.Collection.splats[switcher.CurrentIndex].manifest));
                Assert.That(blur.IsBlurring, Is.True, "A swap should conceal the streaming transition.");
                Assert.That(switcher.SwapSplat(), Is.False, "A second request during reveal must be ignored.");
                yield return NextFrame();
                yield return NextFrame();
                Assert.That(oldChunks.All(r => r == null), Is.True, "Outgoing chunks survived the swap.");
                yield return WaitForStreamedScene();
                Assert.That(music.isPlaying, Is.True);
                Assert.That(Camera.main.GetComponent<SceneCameraRig>().View, Is.Not.Null);
                Assert.That(Object.FindObjectsByType<GsplatLodStreamer>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
            }
        }
    }
}
