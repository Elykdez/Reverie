using UnityEngine;
using UnityEngine.UI;

namespace Hypocycloid.Reverie.UI
{
    // Shows a 0-1 level on a plain sprite icon, keeping the part the level has not reached as a
    // faint ghost. Sweep grows a soft ring out from sweepStart on the icon's centre line, so a
    // speaker's waves light up one by one while everything left of it always shows; Fade raises
    // the whole icon at once. The state travels in UV1 rather than material properties so every
    // icon keeps sharing the Level Icon material.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    [AddComponentMenu("UI/Reverie/Level Icon")]
    public sealed class LevelIcon : BaseMeshEffect
    {
        public enum Display
        {
            Sweep,
            Fade,
        }

        [SerializeField]
        Display display;

        [SerializeField, Range(0f, 1f)]
        float level = 1f;

        [Tooltip("Where the sweep ring starts, as a fraction of the icon width from the left.")]
        [SerializeField, Range(0f, 1f)]
        float sweepStart = 0.5f;

        public float Level
        {
            get => level;
            set
            {
                value = Mathf.Clamp01(value);
                if (Mathf.Approximately(level, value))
                    return;
                level = value;
                graphic.SetVerticesDirty();
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            EnableRevealChannel();
        }

        protected override void OnTransformParentChanged()
        {
            base.OnTransformParentChanged();
            EnableRevealChannel();
        }

        protected override void OnCanvasHierarchyChanged()
        {
            base.OnCanvasHierarchyChanged();
            EnableRevealChannel();
        }

        // Canvases strip UV1 from their meshes unless it is asked for by name.
        void EnableRevealChannel()
        {
            Canvas target = graphic.canvas;
            if (target)
                target.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0)
                return;

            UIVertex vertex = default;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                min = Vector2.Min(min, vertex.position);
                max = Vector2.Max(max, vertex.position);
            }

            // One scale on both axes keeps the ring round; r = 1 lands on the right edge.
            Vector2 origin = new Vector2(Mathf.Lerp(min.x, max.x, sweepStart), (min.y + max.y) * 0.5f);
            float scale = Mathf.Max((max.x - origin.x), 1e-5f);
            float fade = display == Display.Fade ? 1f : 0f;
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                Vector2 p = ((Vector2)vertex.position - origin) / scale;
                vertex.uv1 = new Vector4(p.x, p.y, level, fade);
                vh.SetUIVertex(vertex, i);
            }
        }
    }
}
