using System.Collections;
using Hypocycloid.Reverie.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Hypocycloid.Reverie.Tests
{
    public sealed class IconButtonTests : ReverieSceneTest
    {
        // The volume button is used because nothing else drives its interactable flag; the splat
        // button's view rewrites it every frame.
        [UnityTest]
        public IEnumerator DisablingTheButtonDimsItsIcon()
        {
            var volume = Object.FindFirstObjectByType<VolumeControl>();
            Assert.That(volume, Is.Not.Null, "The scene has no volume control.");
            var button = (Button)new SerializedObject(volume).FindProperty("toggleButton").objectReferenceValue;
            var tint = button.GetComponentInChildren<DisabledTint>();
            Assert.That(tint, Is.Not.Null, "The volume button's icon has no disabled tint.");
            Assert.That(
                new SerializedObject(tint).FindProperty("selectable").objectReferenceValue,
                Is.SameAs(button),
                "The icon's disabled tint follows a different selectable."
            );
            var icon = tint.GetComponent<CanvasRenderer>();
            float disabledAlpha = button.colors.disabledColor.a * button.colors.colorMultiplier;

            button.interactable = false;
            yield return WaitUntil(
                () => Mathf.Approximately(icon.GetAlpha(), disabledAlpha),
                2f,
                () => $"the icon to dim to {disabledAlpha:F2} (alpha={icon.GetAlpha():F2})"
            );

            button.interactable = true;
            yield return WaitUntil(
                () => Mathf.Approximately(icon.GetAlpha(), 1f),
                2f,
                () => $"the icon to recover (alpha={icon.GetAlpha():F2})"
            );
        }
    }
}
