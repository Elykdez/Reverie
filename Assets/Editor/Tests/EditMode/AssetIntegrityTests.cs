using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace Hypocycloid.Reverie.Tests
{
    // One case per asset so a failure names the file that needs attention.
    public sealed class AssetIntegrityTests
    {
        static IEnumerable<string> Prefabs => FindAssets("t:Prefab", ".prefab");
        static IEnumerable<string> Materials => FindAssets("t:Material", ".mat");

        static IEnumerable<string> FindAssets(string filter, string extension) =>
            AssetDatabase.FindAssets(filter, new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(extension))
                .Distinct()
                .OrderBy(path => path);

        [Test]
        public void PrefabHierarchyIsClean([ValueSource(nameof(Prefabs))] string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, "Could not load " + path);
            ReverieProject.AssertHierarchyIsClean(prefab, path);
        }

        [Test]
        public void MaterialCompilesAgainstAnActiveShader([ValueSource(nameof(Materials))] string path)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Assert.That(material, Is.Not.Null, "Could not load " + path);
            Assert.That(material.shader, Is.Not.Null, "No shader assigned: " + path);
            Assert.That(
                material.shader.name,
                Is.Not.EqualTo("Hidden/InternalErrorShader"),
                "Shader failed to compile or is missing: " + path
            );
            Assert.That(material.shader.name, Does.Not.StartWith("HDRP/"), "Unconverted HDRP material: " + path);

            string[] errors = ShaderUtil.GetShaderMessages(material.shader)
                .Where(message => message.severity == ShaderCompilerMessageSeverity.Error)
                .Select(message => message.message)
                .ToArray();
            Assert.That(errors, Is.Empty, $"Shader errors for {path}:\n" + string.Join("\n", errors));
        }
    }
}
