using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Hypocycloid.Reverie.Tests
{
    // Shared constants and hierarchy assertions for the edit mode suites.
    public static class ReverieProject
    {
        public const string ScenePath = "Assets/Scenes/Reverie.unity";

        // Copied prefabs and converted scenes are the two places where missing scripts and
        // leftover HDRP components show up, so both suites check a hierarchy the same way.
        public static void AssertHierarchyIsClean(GameObject root, string source)
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                Assert.That(
                    GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject),
                    Is.Zero,
                    $"Missing script on {source}/{transform.name}"
                );
                foreach (Component component in transform.GetComponents<Component>())
                    Assert.That(
                        component == null || component.GetType().Namespace != "UnityEngine.Rendering.HighDefinition",
                        Is.True,
                        $"HDRP component on {source}/{transform.name}"
                    );
            }
        }
    }
}
