using UnityEngine;
using UnityEngine.UI;

namespace Hypocycloid.Reverie.UI
{
    // Every plate in the HUD is a pill or a circle. Unity's default UI sprites are 32 px, so
    // stretching one across a 112 px button softens its edge; a distance field stays sharp at
    // any density. The rect and the corner travel in UV1 because the shader needs both to work
    // out the coverage ramp, and that keeps one shared material behind every plate.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("UI/Hypocycloid.Reverie/Capsule")]
    public sealed class CapsuleGraphic : MaskableGraphic
    {
        [Tooltip("Corner radius in canvas units, clamped to half the shorter side.")]
        [SerializeField, Min(0f)]
        float radius = 1000f;

        public float Radius
        {
            get => radius;
            set
            {
                value = Mathf.Max(0f, value);
                if (Mathf.Approximately(radius, value))
                    return;
                radius = value;
                SetVerticesDirty();
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            EnableShapeChannel();
        }

        protected override void OnTransformParentChanged()
        {
            base.OnTransformParentChanged();
            EnableShapeChannel();
        }

        protected override void OnCanvasHierarchyChanged()
        {
            base.OnCanvasHierarchyChanged();
            EnableShapeChannel();
        }

        // Canvases strip UV1 from their meshes unless it is asked for by name.
        void EnableShapeChannel()
        {
            Canvas target = canvas;
            if (target)
                target.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = GetPixelAdjustedRect();
            float corner = Mathf.Clamp(radius, 0f, Mathf.Min(rect.width, rect.height) * 0.5f);

            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;
            vertex.uv1 = new Vector4(rect.width, rect.height, corner, 0f);

            AddCorner(vh, vertex, new Vector2(rect.xMin, rect.yMin), new Vector2(0f, 0f));
            AddCorner(vh, vertex, new Vector2(rect.xMin, rect.yMax), new Vector2(0f, 1f));
            AddCorner(vh, vertex, new Vector2(rect.xMax, rect.yMax), new Vector2(1f, 1f));
            AddCorner(vh, vertex, new Vector2(rect.xMax, rect.yMin), new Vector2(1f, 0f));
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
