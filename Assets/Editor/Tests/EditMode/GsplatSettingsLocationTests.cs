using System.Linq;
using Gsplat;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Reverie.Editor
{
    public sealed class GsplatSettingsLocationTests
    {
        const string SettingsPath = "Assets/Settings/Resources/GsplatSettings.asset";

        [Test]
        public void PublicLookupUsesTheOnlyProjectSettingsAsset()
        {
            var settings = GsplatSettings.Instance;
            Assert.That(settings, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(settings), Is.EqualTo(SettingsPath));
            Assert.That(AssetDatabase.AssetPathToGUID(SettingsPath),
                Is.EqualTo("218c39b34310bca469976894572408ae"), "Moving settings must preserve asset references.");
            Assert.That(AssetDatabase.FindAssets("t:GsplatSettings", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath), Is.EquivalentTo(new[] { SettingsPath }));
        }

        [Test]
        public void ResourcesLookupLoadsValidSettingsWithoutPreloadRegistration()
        {
            var settings = Resources.Load<GsplatSettings>("GsplatSettings");
            Assert.That(settings, Is.Not.Null);
            Assert.That(settings, Is.SameAs(GsplatSettings.Instance));
            Assert.That(settings.Valid, Is.True);
            CollectionAssert.DoesNotContain(PlayerSettings.GetPreloadedAssets(), settings);
        }
    }
}
