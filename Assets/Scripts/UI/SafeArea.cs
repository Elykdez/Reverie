using Hypocycloid.Reverie.Common;
using UnityEngine;

namespace Hypocycloid.Reverie.UI
{
    // The player renders outside the safe area on Android, so HUD content sits under the
    // cutout and the gesture bar until something pushes it back in. Drive this from a direct
    // child of the canvas: the anchors below are screen fractions, which only line up when the
    // parent rect covers the whole canvas.
    [DisallowMultipleComponent]
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeArea : MonoBehaviour
    {
        [Tooltip("Edges that follow the cutout. Clear one to let content stay flush with it.")]
        [SerializeField] bool conformLeft = true;

        [SerializeField] bool conformRight = true;
        [SerializeField] bool conformTop = true;
        [SerializeField] bool conformBottom = true;

        [Tooltip("Extra inset in canvas units, applied inside the safe area.")]
        [SerializeField] RectOffset padding = new RectOffset();

        RectTransform area;
        Rect appliedSafeArea;
        Vector2Int appliedResolution;
        bool applied;

        void OnEnable()
        {
            area = (RectTransform)transform;
            applied = false;
            Apply();
        }

        // Rotation, multi-window resizes and the Device Simulator all move the safe area
        // without an event, so it is read every frame and written only when it moves.
        void Update() => Apply();

        void OnValidate() => applied = false;

        void Apply()
        {
            Rect safe = Screen.safeArea;
            var resolution = new Vector2Int(Screen.width, Screen.height);
            if (resolution.x <= 0 || resolution.y <= 0 || safe.width <= 0f || safe.height <= 0f)
                return;
            if (applied && safe == appliedSafeArea && resolution == appliedResolution)
                return;
            appliedSafeArea = safe;
            appliedResolution = resolution;
            applied = true;

            // The editor hands back a safe area from whichever device was last simulated, which
            // can reach past the game view, so the fractions are clamped before they anchor.
            var min = new Vector2(
                Mathf.Clamp01(safe.xMin / resolution.x),
                Mathf.Clamp01(safe.yMin / resolution.y)
            );
            var max = new Vector2(
                Mathf.Clamp01(safe.xMax / resolution.x),
                Mathf.Clamp01(safe.yMax / resolution.y)
            );
            min = Vector2.Min(min, max);
            if (!conformLeft)
                min.x = 0f;
            if (!conformBottom)
                min.y = 0f;
            if (!conformRight)
                max.x = 1f;
            if (!conformTop)
                max.y = 1f;

            if (!area)
                area = (RectTransform)transform;
            area.anchorMin = min;
            area.anchorMax = max;
            RectOffset inset = padding ?? new RectOffset();
            area.offsetMin = new Vector2(inset.left, inset.bottom);
            area.offsetMax = new Vector2(-inset.right, -inset.top);

            Diagnostics.Log(
                Diagnostics.Category.Interaction,
                "safe_area_applied",
                $"screen={resolution.x}x{resolution.y}; safe={safe}",
                this
            );
        }
    }
}
