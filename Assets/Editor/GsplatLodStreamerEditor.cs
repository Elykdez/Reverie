using System;
using Hypocycloid.Splats;
using UnityEditor;
using UnityEngine;

namespace Reverie.Editor
{
    [CustomEditor(typeof(GsplatLodStreamer))]
    public sealed class GsplatLodStreamerEditor : UnityEditor.Editor
    {
        static readonly GUIContent TargetLabel = new GUIContent(
            "Target",
            "LOD splat budget. Coarse leaf coverage can exceed this target."
        );
        static readonly GUIContent LodLabel = new GUIContent(
            "LOD Base",
            "Distance at which the LOD selector begins reducing detail."
        );
        static readonly GUIContent OpacityLabel = new GUIContent(
            "Opacity Prune",
            "Hide whole splats below their source opacity. Zero disables pruning."
        );

        SerializedProperty budget;
        SerializedProperty lodBase;
        SerializedProperty opacity;
        bool simplifyExpanded = true;
        bool advancedExpanded;
        double nextStatsRefresh;
        long finestCount;
        long activeCount;
        long passingCount;

        void OnEnable()
        {
            budget = serializedObject.FindProperty("SplatBudget");
            lodBase = serializedObject.FindProperty("LodBaseDistance");
            opacity = serializedObject.FindProperty("opacityPrune");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var streamer = (GsplatLodStreamer)target;
            simplifyExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(
                simplifyExpanded,
                "Splat Simplify"
            );
            if (simplifyExpanded)
            {
                RefreshStats(streamer);
                string source =
                    streamer.PreviewRenderer && streamer.PreviewRenderer.GsplatAsset
                        ? streamer.PreviewRenderer.GsplatAsset.name
                    : streamer.ManifestAsset ? streamer.ManifestAsset.name
                    : streamer.ManifestUri;
                EditorGUILayout.LabelField(
                    "Source",
                    string.IsNullOrEmpty(source) ? "Unassigned" : source
                );
                int maximum =
                    finestCount > 0
                        ? (int)Math.Min(int.MaxValue, finestCount)
                        : Math.Max(1, budget.intValue);
                EditorGUI.BeginChangeCheck();
                int selectedBudget = EditorGUILayout.IntSlider(
                    TargetLabel,
                    Mathf.Min(budget.intValue, maximum),
                    1,
                    maximum
                );
                if (EditorGUI.EndChangeCheck())
                    budget.intValue = selectedBudget;
                DrawLodSlider();
                EditorGUILayout.Slider(opacity, 0f, 1f, OpacityLabel);

                EditorGUILayout.BeginHorizontal();
                DrawCount(
                    "Original (finest)",
                    finestCount > 0 ? finestCount.ToString("N0") : "Unavailable"
                );
                DrawCount("Target budget", budget.intValue.ToString("N0"));
                EditorGUILayout.EndHorizontal();
                if (Application.isPlaying)
                {
                    EditorGUILayout.LabelField(
                        "Device budget",
                        streamer.EffectiveSplatBudget.ToString("N0")
                    );
                    EditorGUILayout.LabelField("Active before prune", activeCount.ToString("N0"));
                    EditorGUILayout.LabelField("Pass opacity", passingCount.ToString("N0"));
                }
                EditorGUILayout.HelpBox(
                    "Changes are reversible. Target controls streamed LOD; opacity hides faint splats without editing the source or reducing sort memory. Counts exclude the environment.",
                    MessageType.None
                );
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            advancedExpanded = EditorGUILayout.Foldout(advancedExpanded, "Advanced", true);
            if (advancedExpanded)
                DrawPropertiesExcluding(
                    serializedObject,
                    "m_Script",
                    "SplatBudget",
                    "LodBaseDistance",
                    "opacityPrune"
                );

            if (serializedObject.ApplyModifiedProperties())
            {
                streamer.SetSplatBudget(streamer.SplatBudget);
                streamer.SetLodBaseDistance(streamer.LodBaseDistance);
                streamer.SetOpacityPrune(streamer.OpacityPrune);
            }
        }

        void RefreshStats(GsplatLodStreamer streamer)
        {
            if (EditorApplication.timeSinceStartup < nextStatsRefresh)
                return;
            nextStatsRefresh = EditorApplication.timeSinceStartup + 0.25;
            finestCount = streamer.FinestSplatCount;
            activeCount = streamer.ActiveSplatCount;
            passingCount = streamer.OpacityPassingSplatCount;
        }

        void DrawLodSlider()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(LodLabel);
                float value = Mathf.Max(0.1f, lodBase.floatValue);
                EditorGUI.BeginChangeCheck();
                float exponent = GUILayout.HorizontalSlider(
                    Mathf.Log10(value),
                    -1f,
                    Mathf.Log10(Mathf.Max(100f, value))
                );
                if (EditorGUI.EndChangeCheck())
                    lodBase.floatValue = Mathf.Pow(10f, exponent);
                EditorGUI.BeginChangeCheck();
                float typed = EditorGUILayout.FloatField(lodBase.floatValue, GUILayout.Width(60f));
                if (EditorGUI.EndChangeCheck() && float.IsFinite(typed))
                    lodBase.floatValue = Mathf.Max(0.1f, typed);
            }
        }

        static void DrawCount(string label, string value)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(label, EditorStyles.miniLabel);
            EditorGUILayout.LabelField(value, EditorStyles.boldLabel);
            EditorGUILayout.EndVertical();
        }

        public override bool RequiresConstantRepaint() => Application.isPlaying && simplifyExpanded;
    }
}
