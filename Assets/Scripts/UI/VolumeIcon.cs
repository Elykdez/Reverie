using UnityEngine;
using UnityEngine.UI;

namespace Hypocycloid.Reverie.UI
{
    // A speaker drawn from a distance field instead of a sprite: it stays sharp on any screen
    // density and the arcs can fade in one at a time with the level. The level travels in UV1
    // rather than a material property so every icon keeps sharing one material.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("UI/Reverie/Volume Icon")]
    public sealed class VolumeIcon : MaskableGraphic
    {
        [SerializeField, Range(0f, 1f)]
        float level = 1f;

        public float Level
        {
            get => level;
            set
            {
                value = Mathf.Clamp01(value);
                if (Mathf.Approximately(level, value))
                    return;
                level = value;
                SetVerticesDirty();
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            EnableLevelChannel();
        }

        protected override void OnTransformParentChanged()
        {
            base.OnTransformParentChanged();
            EnableLevelChannel();
        }

        protected override void OnCanvasHierarchyChanged()
        {
            base.OnCanvasHierarchyChanged();
            EnableLevelChannel();
        }

        // Canvases strip UV1 from their meshes unless it is asked for by name.
        void EnableLevelChannel()
        {
            Canvas target = canvas;
            if (target)
                target.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = GetPixelAdjustedRect();
            // The shader works in a square [-1, 1] space, so a non-square rect is centre cropped.
            float extent = Mathf.Min(rect.width, rect.height) * 0.5f;
            Vector2 centre = rect.center;

            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;
            vertex.uv1 = new Vector4(level, 0f, 0f, 0f);

            AddCorner(vh, vertex, centre + new Vector2(-extent, -extent), new Vector2(0f, 0f));
            AddCorner(vh, vertex, centre + new Vector2(-extent, extent), new Vector2(0f, 1f));
            AddCorner(vh, vertex, centre + new Vector2(extent, extent), new Vector2(1f, 1f));
            AddCorner(vh, vertex, centre + new Vector2(extent, -extent), new Vector2(1f, 0f));
            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }

        static void AddCorner(VertexHelper vh, UIVertex vertex, Vector2 position, Vector2 uv)
        {
            vertex.position = position;
            vertex.uv0 = uv;
            vh.AddVert(vertex);
        }
    }
}
