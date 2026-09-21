using NaughtyAttributes;
using UnityEngine;

namespace Hypocycloid.Reverie.Controller
{
    [DisallowMultipleComponent]
    public sealed class ScenePreviewPath : MonoBehaviour
    {
        [Tooltip("Closed-loop camera positions, relative to this object.")]
        public Vector3[] Positions;

        [Tooltip("One local-space look target for each camera position.")]
        public Vector3[] LookTargets;

        [Min(1f)]
        public float Duration = 60f;

        [SerializeField] Material pathMaterial;
        [SerializeField, HideInInspector] Vector3 defaultOrbitCenter = new Vector3(0f, 3.2f, 0f);
        [SerializeField, HideInInspector] Vector3 defaultLookTarget = new Vector3(0f, 1.6f, 0f);
        [SerializeField, HideInInspector] float defaultRadius = 3.5f;
        [SerializeField, HideInInspector] float defaultHeightVariation = 0.6f;
        LineRenderer pathLine;
        bool visualizationDirty;

        public bool IsPathVisible => pathLine && pathLine.enabled;

        void Reset() => GenerateDefaultRoute();

        [Button("Reset to System Default", EButtonEnableMode.Editor)]
        public void ResetToSystemDefault()
        {
#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(this, "Reset Preview Path to System Default");
#endif
            GenerateDefaultRoute();
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            if (gameObject.scene.IsValid())
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            UnityEditor.SceneView.RepaintAll();
#endif
        }

        void GenerateDefaultRoute()
        {
            // Keep the generation recipe separate from the developer's editable route.
            Positions = new Vector3[8];
            LookTargets = new Vector3[8];
            for (int i = 0; i < Positions.Length; ++i)
            {
                float angle = i * Mathf.PI / 4f;
                Positions[i] = defaultOrbitCenter + new Vector3(
                    defaultRadius * Mathf.Sin(angle), defaultHeightVariation * Mathf.Sin(angle),
                    -defaultRadius * Mathf.Cos(angle));
                LookTargets[i] = defaultLookTarget;
            }
            Duration = 60f;
            visualizationDirty = true;
        }

        public void SetPathVisible(bool visible)
        {
            visible &= Application.isPlaying && IsValid && pathMaterial;
            if (!visible)
            {
                if (pathLine)
                    pathLine.enabled = false;
                return;
            }
            if (!pathLine)
            {
                var obj = new GameObject("Preview Spline");
                obj.layer = LayerMask.NameToLayer("Ignore Raycast");
                obj.transform.SetParent(transform, false);
                pathLine = obj.AddComponent<LineRenderer>();
                pathLine.sharedMaterial = pathMaterial;
                pathLine.useWorldSpace = false;
                pathLine.loop = true;
                pathLine.widthMultiplier = 0.035f;
                pathLine.numCornerVertices = 3;
                pathLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                pathLine.receiveShadows = false;
                visualizationDirty = true;
            }
            if (!pathLine.enabled || visualizationDirty)
            {
                int samples = Positions.Length * 32;
                pathLine.positionCount = samples;
                for (int i = 0; i < samples; ++i)
                {
                    float segment = i * Positions.Length / (float)samples;
                    pathLine.SetPosition(i, Interpolate(Positions, Mathf.FloorToInt(segment), segment % 1f));
                }
                visualizationDirty = false;
            }
            pathLine.enabled = true;
        }

        void OnValidate() => visualizationDirty = true;
        void OnDisable() => SetPathVisible(false);

        public bool IsValid => isActiveAndEnabled && Positions != null && Positions.Length >= 3
            && LookTargets != null && LookTargets.Length == Positions.Length
            && Duration > 0f && !float.IsInfinity(Duration);

        public void Evaluate(float normalized, out Vector3 position, out Quaternion rotation)
        {
            if (!IsValid)
            {
                position = transform.position;
                rotation = transform.rotation;
                return;
            }

            float segment = Mathf.Repeat(normalized, 1f) * Positions.Length;
            int index = Mathf.FloorToInt(segment);
            float t = segment - index;
            position = transform.TransformPoint(Interpolate(Positions, index, t));
            Vector3 target = transform.TransformPoint(Interpolate(LookTargets, index, t));
            Vector3 direction = target - position;
            rotation = direction.sqrMagnitude > 0.000001f
                ? Quaternion.LookRotation(direction, transform.up)
                : transform.rotation;
        }

        static Vector3 Interpolate(Vector3[] points, int index, float t)
        {
            int count = points.Length;
            Vector3 a = points[(index + count - 1) % count];
            Vector3 b = points[index % count];
            Vector3 c = points[(index + 1) % count];
            Vector3 d = points[(index + 2) % count];
            // Wrapping all four neighbors preserves the tangent across the loop seam.
            return 0.5f * ((2f * b) + (-a + c) * t
                + (2f * a - 5f * b + 4f * c - d) * t * t
                + (-a + 3f * b - 3f * c + d) * t * t * t);
        }

        void OnDrawGizmosSelected()
        {
            if (!IsValid)
                return;
            Gizmos.color = new Color(0.96f, 0.78f, 0.38f);
            Evaluate(0f, out Vector3 previous, out _);
            for (int i = 1; i <= 96; ++i)
            {
                Evaluate(i / 96f, out Vector3 position, out _);
                Gizmos.DrawLine(previous, position);
                previous = position;
            }
            for (int i = 0; i < Positions.Length; ++i)
            {
                Vector3 position = transform.TransformPoint(Positions[i]);
                Gizmos.DrawSphere(position, 0.08f);
                Gizmos.DrawLine(position, transform.TransformPoint(LookTargets[i]));
            }
        }
    }
}
