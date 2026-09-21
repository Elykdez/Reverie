using System;
using Gsplat;
using Hypocycloid.Reverie.Splats;
using UnityEngine;

namespace Hypocycloid.Reverie.Controller
{
    [CreateAssetMenu(fileName = "SplatCollection", menuName = "Reverie/Splat Collection")]
    public sealed class CatCollection : ScriptableObject
    {
        [InspectorName("Splats")]
        public CatEntry[] cats = Array.Empty<CatEntry>();
        public bool randomOnStartup = true;
        [Min(0)] public int defaultIndex;
        public bool shakeToSwap = true;
        [Tooltip("Acceleration above the filtered gravity vector, in g.")]
        [Range(0.5f, 4f)] public float shakeThreshold = 1.5f;
        [Range(0.2f, 1.5f)] public float shakeWindow = 0.75f;
        [Min(0.5f)] public float swapCooldown = 2.5f;
        public bool keyboardShortcut = true;

        public int ChooseIndex(int currentIndex, int randomChoice)
        {
            if (cats.Length == 0)
                return -1;
            if (currentIndex < 0 || currentIndex >= cats.Length)
                return Mathf.Clamp(randomChoice, 0, cats.Length - 1);
            if (cats.Length == 1)
                return 0;
            int next = Mathf.Clamp(randomChoice, 0, cats.Length - 2);
            return next >= currentIndex ? next + 1 : next;
        }
    }

    [Serializable]
    public sealed class CatEntry
    {
        public string displayName;
        public GsplatAsset preview;
        public GsplatLodManifestAsset manifest;
        [InspectorName("Position Offset")]
        [Tooltip("Local position in Unity units. Increase Y to raise this splat above the island.")]
        public Vector3 position;
        public Vector3 rotation = new Vector3(0f, 0f, 180f);
        [Min(0.001f)] public float scale = 1f;
        [Tooltip("Camera orbit center in world space, independent of stray splats in the capture bounds.")]
        public Vector3 focusPoint = new Vector3(0f, 0.15f, 0f);
        [TextArea] public string attribution;
    }
}
