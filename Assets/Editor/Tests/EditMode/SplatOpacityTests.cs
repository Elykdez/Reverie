using System;
using System.Collections.Generic;
using System.Reflection;
using Gsplat;
using Hypocycloid.Splats;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Reverie.Editor
{
    public sealed class SplatOpacityTests
    {
        static GsplatAsset CreateAsset(bool spark)
        {
            byte[] alphas = { 0, 25, 26, 128, 255 };
            GsplatAsset asset = spark ? ScriptableObject.CreateInstance<GsplatAssetSpark>()
                : ScriptableObject.CreateInstance<GsplatAssetUncompressed>();
            asset.SplatCount = 5;
            asset.Allocate();
            for (int i = 0; i < alphas.Length; ++i)
            {
                if (asset is GsplatAssetSpark packed)
                    packed.PackedSplats[i] = new uint4((uint)alphas[i] << 24, 0, 0, 0);
                else
                    ((GsplatAssetUncompressed)asset).Colors[i] = new Vector4(0, 0, 0, alphas[i] / 255f);
            }
            return asset;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PruningUsesSourceOpacityAndRestoresTheSelection(bool spark)
        {
            GsplatAsset asset = CreateAsset(spark);
            try
            {
                var whole = new[] { new GsplatActiveRange { Offset = 0, Count = 5 } };
                Assert.That(SplatOpacity.CountPassing(asset, whole, 0f), Is.EqualTo(5));
                Assert.That(SplatOpacity.CountPassing(asset, whole, 0.1f), Is.EqualTo(3));
                Assert.That(SplatOpacity.CountPassing(asset, whole, 128f / 255f), Is.EqualTo(2));
                Assert.That(SplatOpacity.CountPassing(asset, whole, 1f), Is.EqualTo(1));
                var selected = new[]
                {
                    new GsplatActiveRange { Offset = 1, Count = 2 },
                    new GsplatActiveRange { Offset = 4, Count = 1 },
                };
                Assert.That(SplatOpacity.CountPassing(asset, selected, 0.1f), Is.EqualTo(2));
                Assert.That(SplatOpacity.CountPassing(asset, Array.Empty<GsplatActiveRange>(), 0.1f), Is.Zero);
                Assert.That(SplatOpacity.CountPassing(asset, whole, 0f), Is.EqualTo(5));

                var runs = new List<GsplatActiveRange>();
                SplatOpacity.AppendPassing(asset, whole, 128f / 255f, runs);
                Assert.That(runs, Is.EqualTo(new[] { new GsplatActiveRange(3, 2) }));
                runs.Clear();
                SplatOpacity.AppendPassing(asset, selected, 0.1f, runs);
                Assert.That(runs, Is.EqualTo(new[] { new GsplatActiveRange(2, 1), new GsplatActiveRange(4, 1) }));
                runs.Clear();
                SplatOpacity.AppendPassing(asset, selected, 0f, runs);
                Assert.That(runs, Is.EqualTo(selected), "Zero threshold keeps the selection unchanged.");
                Assert.That(asset.SplatCount, Is.EqualTo(5), "Pruning must not modify source data.");
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void LiveThresholdClampsAndRejectsNonFiniteInputWithoutChangingBudget()
        {
            var root = new GameObject("Opacity threshold test");
            root.SetActive(false);
            GsplatAsset asset = CreateAsset(true);
            try
            {
                var streamer = root.AddComponent<GsplatLodStreamer>();
                streamer.PreviewRenderer = root.AddComponent<GsplatRenderer>();
                streamer.PreviewRenderer.GsplatAsset = asset;
                int budget = streamer.SplatBudget;
                streamer.SetOpacityPrune(2f);
                Assert.That(streamer.OpacityPrune, Is.EqualTo(1f));
                Assert.That(streamer.PreviewRenderer.ActiveSplatCount, Is.EqualTo(1));
                streamer.SetOpacityPrune(float.NaN);
                streamer.SetOpacityPrune(float.PositiveInfinity);
                streamer.SetOpacityPrune(float.NegativeInfinity);
                Assert.That(streamer.OpacityPrune, Is.EqualTo(1f));
                streamer.SetOpacityPrune(-1f);
                Assert.That(streamer.OpacityPrune, Is.Zero);
                Assert.That(streamer.PreviewRenderer.HasActiveRanges, Is.False);
                Assert.That(streamer.SplatBudget, Is.EqualTo(budget));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void BudgetCannotScaleAboveUserTargetAndRequestsImmediateLodEvaluation()
        {
            var root = new GameObject("Splat budget test");
            root.SetActive(false);
            try
            {
                var streamer = root.AddComponent<GsplatLodStreamer>();
                var force = typeof(GsplatLodStreamer).GetField("m_forceEvaluate", BindingFlags.NonPublic | BindingFlags.Instance);
                force.SetValue(streamer, false);
                streamer.SetSplatBudget(1234);
                Assert.That(streamer.EffectiveSplatBudget, Is.EqualTo(1234), "Device scaling cannot raise a small user target.");
                Assert.That(force.GetValue(streamer), Is.True);
                streamer.SetSplatBudget(int.MinValue);
                Assert.That(streamer.SplatBudget, Is.EqualTo(1));
                Assert.That(streamer.EffectiveSplatBudget, Is.EqualTo(1));
                streamer.ScaleBudgetToDevice = false;
                streamer.SetSplatBudget(2_000_000);
                Assert.That(streamer.EffectiveSplatBudget, Is.EqualTo(2_000_000));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LodBaseRejectsNonFiniteValuesAndRequestsImmediateLodEvaluation()
        {
            var root = new GameObject("LOD base test");
            root.SetActive(false);
            try
            {
                var streamer = root.AddComponent<GsplatLodStreamer>();
                var force = typeof(GsplatLodStreamer).GetField("m_forceEvaluate", BindingFlags.NonPublic | BindingFlags.Instance);
                force.SetValue(streamer, false);
                streamer.SetLodBaseDistance(2f);
                Assert.That(streamer.LodBaseDistance, Is.EqualTo(2f));
                Assert.That(force.GetValue(streamer), Is.True);
                force.SetValue(streamer, false);
                streamer.SetLodBaseDistance(float.NaN);
                streamer.SetLodBaseDistance(float.PositiveInfinity);
                streamer.SetLodBaseDistance(float.NegativeInfinity);
                Assert.That(streamer.LodBaseDistance, Is.EqualTo(2f));
                Assert.That(force.GetValue(streamer), Is.False);
                streamer.SetLodBaseDistance(-5f);
                Assert.That(streamer.LodBaseDistance, Is.EqualTo(0.1f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void OriginalCountUsesFinestAvailableLeafAndPrefersLiveManifest()
        {
            const string json = "{\"lodLevels\":2,\"filenames\":[\"test.ply\"],\"environment\":\"sky.ply\"," +
                "\"tree\":{\"bound\":{\"min\":[0,0,0],\"max\":[1,1,1]}," +
                "\"lods\":{\"1\":{\"file\":0,\"offset\":0,\"count\":42}}}}";
            var root = new GameObject("Finest leaf count test");
            root.SetActive(false);
            var asset = ScriptableObject.CreateInstance<GsplatLodManifestAsset>();
            try
            {
                asset.Initialize(json, "");
                var streamer = root.AddComponent<GsplatLodStreamer>();
                Assert.That(streamer.FinestSplatCount, Is.Zero);
                streamer.ManifestAsset = asset;
                Assert.That(streamer.FinestSplatCount, Is.EqualTo(42));
                typeof(GsplatLodStreamer).GetField("m_manifest", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(streamer, GsplatLodManifest.Parse(json.Replace("42", "84")));
                Assert.That(streamer.FinestSplatCount, Is.EqualTo(84));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void StreamedChunksSelectPassingSplatsAndRestoreWithoutChangingSourceAsset()
        {
            var root = new GameObject("Opacity streamed chunk test");
            root.SetActive(false);
            GsplatAsset asset = CreateAsset(true);
            try
            {
                var streamer = root.AddComponent<GsplatLodStreamer>();
                streamer.SetOpacityPrune(0.1f);
                Type slotType = typeof(GsplatLodStreamer).GetNestedType("FileSlot", BindingFlags.NonPublic);
                object slot = Activator.CreateInstance(slotType, new object[] { 0, "test.ply" });
                slotType.GetField("Asset").SetValue(slot, asset);
                typeof(GsplatLodStreamer).GetMethod("SyncRendererSettings", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(streamer, new[] { slot });
                var renderer = (GsplatRenderer)slotType.GetField("Renderer").GetValue(slot);
                renderer.GsplatAsset = asset;
                MethodInfo apply = typeof(GsplatLodStreamer).GetMethod("SetRendererRanges",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                var selected = new[] { new GsplatActiveRange(1, 2), new GsplatActiveRange(4, 1) };
                apply.Invoke(streamer, new[] { slot, selected });
                Assert.That(renderer.ActiveRanges,
                    Is.EqualTo(new[] { new GsplatActiveRange(2, 1), new GsplatActiveRange(4, 1) }));

                streamer.SetOpacityPrune(0f);
                apply.Invoke(streamer, new[] { slot, new[] { new GsplatActiveRange(0, 5) } });
                Assert.That(renderer.HasActiveRanges, Is.False, "Zero threshold restores the full-file path.");
                Assert.That(SplatOpacity.CountPassing(asset, new[] { new GsplatActiveRange(0, 5) }, 0.1f),
                    Is.EqualTo(3), "Pruning must not modify source data.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(asset);
            }
        }
    }
}
