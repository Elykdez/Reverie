using System.Collections;
using System.Linq;
using Gsplat;
using Hypocycloid.Reverie.Common;
using Hypocycloid.Reverie.Controller;
using Hypocycloid.Reverie.Tests;
using Hypocycloid.Reverie.UI;
using Hypocycloid.Splats;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Reverie.Editor
{
    public sealed class SplatDetailTests : ReverieSceneTest
    {
        [UnityTest, Timeout(600000)]
        public IEnumerator BudgetAndLodIndependentlyReduceAndRestoreActiveSplats()
        {
            yield return WaitForStreamedScene();
            var streamer = Object.FindFirstObjectByType<GsplatLodStreamer>();
            var view = Object.FindFirstObjectByType<SplatDetailView>();
            var serialized = new SerializedObject(view);
            var budget = (Slider)serialized.FindProperty("budgetSlider").objectReferenceValue;
            var lod = (Slider)serialized.FindProperty("lodSlider").objectReferenceValue;
            var opacity = (Slider)serialized.FindProperty("opacitySlider").objectReferenceValue;
            view.Open();
            try
            {
                Assert.That(budget.maxValue, Is.EqualTo((float)streamer.FinestSplatCount));
                budget.value = budget.maxValue;
                lod.value = lod.maxValue;
                opacity.value = 0f;
                view.Close();
                view.Open();
                yield return WaitUntil(() => streamer.IsSettled, StreamingTimeout, () => "full detail baseline");
                long fullCount = streamer.ActiveSplatCount;
                Assert.That(fullCount, Is.GreaterThan(0));

                budget.value = Mathf.Max(1, fullCount / 10);
                view.Close();
                view.Open();
                yield return WaitUntil(() => streamer.IsSettled, StreamingTimeout, () => "reduced budget");
                long budgetCount = streamer.ActiveSplatCount;
                Assert.That(budgetCount, Is.LessThan(fullCount), "Budget must change the loaded selection, with opacity disabled.");

                budget.value = budget.maxValue;
                view.Close();
                view.Open();
                yield return WaitUntil(() => streamer.IsSettled, StreamingTimeout, () => "restored budget");
                Assert.That(streamer.ActiveSplatCount, Is.EqualTo(fullCount));

                lod.value = lod.minValue;
                view.Close();
                view.Open();
                yield return WaitUntil(() => streamer.IsSettled, StreamingTimeout, () => "reduced LOD distance");
                long lodCount = streamer.ActiveSplatCount;
                Assert.That(lodCount, Is.LessThan(fullCount), "LOD must change the loaded selection, with budget unrestricted.");

                lod.value = lod.maxValue;
                view.Close();
                view.Open();
                yield return WaitUntil(() => streamer.IsSettled, StreamingTimeout, () => "restored LOD distance");
                Assert.That(streamer.ActiveSplatCount, Is.EqualTo(fullCount));
                Assert.That(streamer.OpacityPrune, Is.Zero);
                Debug.Log($"Splat controls: full={fullCount}, reduced budget={budgetCount}, reduced LOD={lodCount}, restored={streamer.ActiveSplatCount}");
            }
            finally
            {
                view.ResetSettings();
                view.Close();
            }
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator HudAndInspectorShareLiveSettingsAcrossCaptureSwaps()
        {
            yield return WaitForStreamedScene();
            var streamer = Object.FindFirstObjectByType<GsplatLodStreamer>();
            var switcher = Object.FindFirstObjectByType<SplatSwitcher>();
            var view = Object.FindFirstObjectByType<SplatDetailView>();
            Assert.That(view, Is.Not.Null);
            var serialized = new SerializedObject(view);
            var budget = (Slider)serialized.FindProperty("budgetSlider").objectReferenceValue;
            var lod = (Slider)serialized.FindProperty("lodSlider").objectReferenceValue;
            var opacity = (Slider)serialized.FindProperty("opacitySlider").objectReferenceValue;
            Assert.That(serialized.FindProperty("streamer").objectReferenceValue, Is.SameAs(streamer));
            int initialBudget = streamer.SplatBudget;
            float initialLod = streamer.LodBaseDistance;
            float initialOpacity = streamer.OpacityPrune;
            view.Open();
            Assert.That(PlayerInputGate.IsBlocked, Is.True);
            Assert.That(streamer.SplatBudget, Is.EqualTo(initialBudget));
            Assert.That(streamer.LodBaseDistance, Is.EqualTo(initialLod));
            Assert.That(streamer.OpacityPrune, Is.EqualTo(initialOpacity));
            budget.value = 300000;
            lod.value = Mathf.Log10(2f);
            opacity.value = 0.95f;
            yield return WaitUntil(() => streamer.SplatBudget == 300000
                && Mathf.Approximately(streamer.LodBaseDistance, 2f) && streamer.OpacityPrune == 0.95f,
                2f, () => "HUD slider updates");
            yield return WaitUntil(() => streamer.IsSettled, StreamingTimeout, () => "LOD selection to settle");
            Assert.That(streamer.OpacityPassingSplatCount, Is.LessThan(streamer.ActiveSplatCount));
            Assert.That(streamer.EffectiveSplatBudget, Is.LessThanOrEqualTo(300000));

            var inspector = new SerializedObject(streamer);
            inspector.FindProperty("LodBaseDistance").floatValue = 3f;
            inspector.FindProperty("opacityPrune").floatValue = 0.5f;
            inspector.ApplyModifiedProperties();
            yield return WaitUntil(() => Mathf.Approximately(lod.value, Mathf.Log10(3f)) && opacity.value == 0.5f,
                2f, () => "Inspector changes reflected in HUD");
            Assert.That(BackNavigation.TryGoBack(), Is.True);
            Assert.That(view.IsOpen, Is.False);
            Assert.That(PlayerInputGate.IsBlocked, Is.False);
            yield return WaitUntil(() => switcher.CanSwap, 10f, () => "capture swap cooldown");
            Assert.That(switcher.SwapSplat(), Is.True);
            yield return WaitForStreamedScene();
            Assert.That(streamer.OpacityPrune, Is.EqualTo(0.5f));
            Assert.That(streamer.SplatBudget, Is.EqualTo(300000));
            var streamed = streamer.GetComponentsInChildren<GsplatRenderer>().Where(r => r.isActiveAndEnabled).ToArray();
            Assert.That(streamed.Sum(r => (long)r.ActiveSplatCount), Is.GreaterThan(0));
            Assert.That(streamed.All(r => r.HasActiveRanges
                && SplatOpacity.CountPassing(r.GsplatAsset, r.ActiveRanges, 0.5f) == r.ActiveSplatCount), Is.True,
                "Swapped-in renderers must select only splats passing the live threshold.");
            view.Open();
            view.ResetSettings();
            Assert.That(streamer.SplatBudget, Is.EqualTo(initialBudget));
            Assert.That(streamer.LodBaseDistance, Is.EqualTo(initialLod));
            Assert.That(streamer.OpacityPrune, Is.EqualTo(initialOpacity));
            opacity.value = 0.4f;
            view.Close();
            Assert.That(streamer.OpacityPrune, Is.EqualTo(0.4f), "Closing must flush the final drag.");
            view.ResetSettings();
        }
    }
}
