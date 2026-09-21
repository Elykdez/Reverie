using System;
using Gsplat;
using Hypocycloid.Splats;
using UnityEngine;

namespace Hypocycloid.Reverie.Controller
{
    [CreateAssetMenu(fileName = "SplatCollection", menuName = "Reverie/Splat Collection")]
    public sealed class SplatCollection : ScriptableObject
    {
        [InspectorName("Splats")]
        public SplatEntry[] splats = Array.Empty<SplatEntry>();
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
            if (splats.Length == 0)
                return -1;
            if (currentIndex < 0 || currentIndex >= splats.Length)
                return Mathf.Clamp(randomChoice, 0, splats.Length - 1);
            if (splats.Length == 1)
                return 0;
            int next = Mathf.Clamp(randomChoice, 0, splats.Length - 2);
            return next >= currentIndex ? next + 1 : next;
        }
    }

    [Serializable]
    public sealed class SplatEntry
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
