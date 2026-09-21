using Hypocycloid.Reverie.Controller;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Hypocycloid.Reverie.Tests
{
    public sealed class CatSelectionTests
    {
        [Test]
        public void SelectionNeverRepeatsTheCurrentCatAndCanReachEveryOtherEntry()
        {
            var collection = ScriptableObject.CreateInstance<CatCollection>();
            try
            {
                collection.cats = new CatEntry[4];
                for (int current = 0; current < 4; current++)
                {
                    var seen = new System.Collections.Generic.HashSet<int>();
                    for (int choice = 0; choice < 3; choice++)
                    {
                        int index = collection.ChooseIndex(current, choice);
                        Assert.That(index, Is.Not.EqualTo(current));
                        seen.Add(index);
                    }
                    Assert.That(seen.Count, Is.EqualTo(3));
                }
                collection.cats = new CatEntry[1];
                Assert.That(collection.ChooseIndex(0, 0), Is.Zero);
                collection.cats = new CatEntry[0];
                Assert.That(collection.ChooseIndex(-1, 0), Is.EqualTo(-1));
            }
            finally { Object.DestroyImmediate(collection); }
        }

        [Test]
        public void BothShippedCatsHaveCompleteStreamingAssetsAndAttribution()
        {
            var collection = AssetDatabase.LoadAssetAtPath<CatCollection>(
                AssetDatabase.GUIDToAssetPath("8212dcdd20b2d8d47a1abf745aae503f"));
            Assert.That(collection, Is.Not.Null);
            Assert.That(collection.randomOnStartup, Is.True);
            Assert.That(collection.cats, Has.Length.EqualTo(2));
            Assert.That(collection.cats[0].displayName, Is.EqualTo("Ginger"));
            Assert.That(collection.cats[1].displayName, Is.EqualTo("Oslo"));
            foreach (CatEntry entry in collection.cats)
            {
                Assert.That(entry.preview, Is.Not.Null);
                Assert.That(entry.preview.SplatCount, Is.GreaterThan(0));
                Assert.That(entry.manifest, Is.Not.Null);
                Assert.That(entry.manifest.Manifest, Is.Not.Null);
                Assert.That(entry.scale, Is.GreaterThan(0));
                Assert.That(entry.attribution, Does.Contain("superspl.at"));
                Assert.That(entry.attribution, Does.Contain("4.0"));
            }
        }

        [Test]
        public void OpposingReleasedImpulsesTriggerOneShake()
        {
            var detector = new ShakeGesture();
            Assert.That(detector.Sample(Vector3.down, .02f, 0, 1.5f, .75f), Is.False);
            Assert.That(detector.Sample(Vector3.down + Vector3.right * 3, .02f, .02f, 1.5f, .75f), Is.False);
            Assert.That(detector.Sample(Vector3.down, .06f, .08f, 1.5f, .75f), Is.False);
            Assert.That(detector.Sample(Vector3.down + Vector3.left * 3, .02f, .1f, 1.5f, .75f), Is.True);
            Assert.That(detector.Sample(Vector3.down + Vector3.left * 3, .02f, .12f, 1.5f, .75f), Is.False);
        }

        [Test]
        public void SlowTiltAndOneSustainedBumpDoNotTrigger()
        {
            var detector = new ShakeGesture();
            for (int i = 0; i < 200; i++)
                Assert.That(detector.Sample(Quaternion.Euler(0, 0, i*.4f) * Vector3.down,
                    .02f, i*.02f, 1.5f, .75f), Is.False);
            detector.Reset();
            detector.Sample(Vector3.down, .02f, 0, 1.5f, .75f);
            for (int i = 1; i < 100; i++)
                Assert.That(detector.Sample(Vector3.down + Vector3.right * 3,
                    .02f, i*.02f, 1.5f, .75f), Is.False);
        }

        [Test]
        public void ExpiredOrResetImpulsesDoNotCompleteAShake()
        {
            var detector = new ShakeGesture();
            detector.Sample(Vector3.down, .02f, 0, 1.5f, .75f);
            detector.Sample(Vector3.down + Vector3.right * 3, .02f, .02f, 1.5f, .75f);
            detector.Sample(Vector3.down, .02f, .04f, 1.5f, .75f);
            Assert.That(detector.Sample(Vector3.down + Vector3.left * 3, .02f, 1, 1.5f, .75f), Is.False);
            detector.Reset();
            Assert.That(detector.Sample(Vector3.down + Vector3.right * 3, .02f, 1.1f, 1.5f, .75f), Is.False);
        }
    }
}
