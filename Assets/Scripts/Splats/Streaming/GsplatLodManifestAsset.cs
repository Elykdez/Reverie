// Adapted from ARLOOPA UnitySplats 1.2.0 LOD code; project-owned integration.
// Copyright (c) 2026 ARLOOPA
// SPDX-License-Identifier: MIT


using System;
using UnityEngine;

using Gsplat;

namespace Hypocycloid.Reverie.Splats
{
    /// <summary>Unity asset wrapper for validated PlayCanvas <c>lod-meta.json</c> data.</summary>
    [CreateAssetMenu(fileName = "Gsplat LOD Manifest", menuName = "UnitySplats/LOD Manifest")]
    public sealed class GsplatLodManifestAsset : ScriptableObject
    {
        [SerializeField, TextArea(4, 12)] string m_json;
        [SerializeField, Tooltip("Directory or absolute URI used to resolve manifest filenames.")]
        string m_baseUri;
        [SerializeField] SourceCoordinates m_sourceCoordinates = SourceCoordinates.RUB;
        [Tooltip("Original source asset GUID for editor baking; avoids loading the full PLY in players.")]
        public string SourcePlyGuid;

        [NonSerialized] GsplatLodManifest m_manifest;

        public string Json => m_json;
        public string BaseUri => m_baseUri;
        public SourceCoordinates SourceCoordinates => m_sourceCoordinates;
        public GsplatLodManifest Manifest => m_manifest ??= GsplatLodManifest.Parse(m_json, m_sourceCoordinates);

        public void Initialize(string json, string baseUri,
            SourceCoordinates sourceCoordinates = SourceCoordinates.RUB)
        {
            m_json = json ?? throw new ArgumentNullException(nameof(json));
            m_baseUri = baseUri ?? string.Empty;
            m_sourceCoordinates = sourceCoordinates;
            m_manifest = GsplatLodManifest.Parse(m_json, m_sourceCoordinates);
        }

        void OnEnable() => m_manifest = null;
        void OnValidate() => m_manifest = null;
    }
}
