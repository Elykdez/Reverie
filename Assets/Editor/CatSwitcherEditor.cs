using Hypocycloid.Reverie.Controller;
using Hypocycloid.Reverie.Splats;
using Gsplat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Hypocycloid.Reverie.Editor
{
    [CustomEditor(typeof(CatSwitcher))]
    public sealed class CatSwitcherEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var switcher = (CatSwitcher)target;
            if (!switcher.Collection) return;
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Random selection runs on Play. These buttons preview a specific splat. "
                + "Adjust Position Offset Y to raise or lower it. Placement edits update the collection and preview immediately in Edit Mode.", MessageType.Info);
            if (GUILayout.Button("Open Splat Library"))
                SplatLibraryWindow.Open(switcher.Collection);
            var collectionSettings = new SerializedObject(switcher.Collection);
            collectionSettings.Update();
            var cats = collectionSettings.FindProperty("cats");
            for (int i = 0; i < switcher.Collection.cats.Length; i++)
            {
                CatEntry entry = switcher.Collection.cats[i];
                if (entry == null) continue;
                EditorGUILayout.Space();
                EditorGUILayout.LabelField(entry.displayName + " Placement", EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(Application.isPlaying))
                {
                    var cat = cats.GetArrayElementAtIndex(i);
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(cat.FindPropertyRelative("position"),
                        new GUIContent("Position Offset", "Local position in Unity units. Increase Y to raise this splat."));
                    EditorGUILayout.PropertyField(cat.FindPropertyRelative("rotation"));
                    EditorGUILayout.PropertyField(cat.FindPropertyRelative("scale"));
                    if (EditorGUI.EndChangeCheck())
                    {
                        collectionSettings.ApplyModifiedProperties();
                        ShowInEditor(switcher, switcher.Collection.cats[i]);
                    }
                }
                using (new EditorGUI.DisabledScope(Application.isPlaying && !switcher.CanSwap))
                    if (GUILayout.Button("Show " + entry.displayName))
                    {
                        if (Application.isPlaying) switcher.SelectCat(i);
                        else ShowInEditor(switcher, entry);
                    }
            }
        }

        internal static void ShowInEditor(CatSwitcher switcher, CatEntry entry)
        {
            if (!entry.preview || !entry.manifest) return;
            var settings = new SerializedObject(switcher);
            var renderer = (GsplatRenderer)settings.FindProperty("splatRenderer").objectReferenceValue;
            var streamer = (GsplatLodStreamer)settings.FindProperty("streamer").objectReferenceValue;
            if (!renderer || !streamer) return;
            Undo.RecordObjects(new Object[] { renderer, renderer.transform, renderer.gameObject, streamer }, "Preview splat");
            renderer.GsplatAsset = entry.preview;
            renderer.transform.SetLocalPositionAndRotation(entry.position, Quaternion.Euler(entry.rotation));
            renderer.transform.localScale = Vector3.one * entry.scale;
            renderer.gameObject.name = entry.displayName;
            streamer.ManifestAsset = entry.manifest;
            streamer.ManifestUri = "";
            streamer.SourceCoordinates = entry.manifest.SourceCoordinates;
            // Keep GPU buffers alive while dragging placement fields in the Inspector.
            renderer.ForceRefresh();
            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(streamer);
            EditorSceneManager.MarkSceneDirty(renderer.gameObject.scene);
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }
    }
}
