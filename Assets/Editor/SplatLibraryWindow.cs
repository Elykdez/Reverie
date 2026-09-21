using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Gsplat;
using Hypocycloid.Reverie.Controller;
using Hypocycloid.Reverie.Splats;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Hypocycloid.Reverie.Editor
{
    public sealed class SplatLibraryWindow : EditorWindow
    {
        [SerializeField] CatCollection collection;
        [SerializeField] string sourcePath;
        [SerializeField] string splatName = "New Splat";
        [SerializeField] int tab;
        [SerializeField] int selectedIndex;
        Vector2 scroll;
        static Task<int> conversion;
        static string importFolder;
        static string outputFolder;
        static string importedSource;
        static string importedName;
        static CatCollection targetCollection;
        static string lastResult;

        [MenuItem("Reverie/Splats")]
        public static void Open() => Open(null);

        public static void Open(CatCollection selectedCollection)
        {
            var window = GetWindow<SplatLibraryWindow>("Splat Library");
            window.minSize = new Vector2(380f, 460f);
            if (selectedCollection) window.collection = selectedCollection;
            window.Show();
        }

        void OnEnable()
        {
            if (collection) return;
            var switcher = FindFirstObjectByType<CatSwitcher>();
            if (switcher) collection = switcher.Collection;
            if (!collection)
            {
                string[] assets = AssetDatabase.FindAssets("t:CatCollection");
                if (assets.Length == 1)
                    collection = AssetDatabase.LoadAssetAtPath<CatCollection>(AssetDatabase.GUIDToAssetPath(assets[0]));
            }
        }

        void OnGUI()
        {
            using (new EditorGUI.DisabledScope(conversion != null))
                collection = (CatCollection)EditorGUILayout.ObjectField("Splat Collection", collection, typeof(CatCollection), false);
            tab = GUILayout.Toolbar(tab, new[] { "Collection", "Import PLY" });
            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (tab == 0) DrawCollection();
            else DrawImport();
            EditorGUILayout.EndScrollView();
            if (!string.IsNullOrEmpty(lastResult))
                EditorGUILayout.HelpBox(lastResult, MessageType.None);
        }

        void DrawCollection()
        {
            if (!collection)
            {
                EditorGUILayout.HelpBox("Assign a Splat Collection to configure its captures.", MessageType.Info);
                return;
            }
            var settings = new SerializedObject(collection);
            settings.Update();
            var switcher = FindFirstObjectByType<CatSwitcher>();
            bool canPreview = switcher && switcher.Collection == collection;
            using (new EditorGUI.DisabledScope(Application.isPlaying || conversion != null))
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Playback", EditorStyles.boldLabel);
                foreach (string field in new[] { "randomOnStartup", "defaultIndex", "shakeToSwap", "shakeThreshold", "shakeWindow", "swapCooldown", "keyboardShortcut" })
                    EditorGUILayout.PropertyField(settings.FindProperty(field));
                settings.ApplyModifiedProperties();
                if (collection.cats.Length == 0)
                {
                    EditorGUILayout.HelpBox("No splats yet. Use Import PLY to add a capture.", MessageType.Info);
                    return;
                }
                selectedIndex = Mathf.Clamp(selectedIndex, 0, collection.cats.Length - 1);
                string[] names = Array.ConvertAll(collection.cats, entry => entry?.displayName ?? "Missing entry");
                EditorGUILayout.Space();
                selectedIndex = EditorGUILayout.Popup("Splat", selectedIndex, names);
                var entry = settings.FindProperty("cats").GetArrayElementAtIndex(selectedIndex);
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("displayName"));
                EditorGUILayout.LabelField("Placement", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                foreach (string field in new[] { "position", "rotation", "scale", "focusPoint" })
                    EditorGUILayout.PropertyField(entry.FindPropertyRelative(field));
                bool placementChanged = EditorGUI.EndChangeCheck();
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Assets and Credit", EditorStyles.boldLabel);
                foreach (string field in new[] { "preview", "manifest", "attribution" })
                    EditorGUILayout.PropertyField(entry.FindPropertyRelative(field));
                if (settings.ApplyModifiedProperties() && placementChanged && canPreview)
                    CatSwitcherEditor.ShowInEditor(switcher, collection.cats[selectedIndex]);
            }
            EditorGUILayout.HelpBox("Increase Position Offset Y to raise the splat. Placement edits preview in Edit Mode; save the collection to keep them.", MessageType.Info);
            using (new EditorGUI.DisabledScope(!canPreview || conversion != null || (Application.isPlaying && !switcher.CanSwap)))
                if (GUILayout.Button("Show Selected Splat"))
                {
                    if (Application.isPlaying) switcher.SelectCat(selectedIndex);
                    else CatSwitcherEditor.ShowInEditor(switcher, collection.cats[selectedIndex]);
                }
            if (!canPreview)
                EditorGUILayout.HelpBox("Open the Reverie scene to preview this collection.", MessageType.Info);
            using (new EditorGUI.DisabledScope(Application.isPlaying || conversion != null))
                if (GUILayout.Button("Save Collection")) AssetDatabase.SaveAssetIfDirty(collection);
        }

        void DrawImport()
        {
            EditorGUILayout.HelpBox("Import a Gaussian splat PLY into the collection. The source and adjacent license.txt are preserved. Requires Node.js and PowerShell.", MessageType.Info);
            EditorGUILayout.LabelField("PLY", sourcePath ?? "None", EditorStyles.wordWrappedLabel);
            using (new EditorGUI.DisabledScope(conversion != null || Application.isPlaying))
            {
                splatName = EditorGUILayout.TextField("Splat name", splatName);
                if (GUILayout.Button("Choose PLY"))
                {
                    string chosen = EditorUtility.OpenFilePanel("Gaussian splat PLY", "", "ply");
                    if (!string.IsNullOrEmpty(chosen)) sourcePath = chosen;
                }
                using (new EditorGUI.DisabledScope(!collection || !File.Exists(sourcePath)))
                    if (GUILayout.Button("Import into collection"))
                    {
                        try { BeginImport(sourcePath, splatName, collection); }
                        catch (Exception exception) { lastResult = exception.Message; Debug.LogException(exception); }
                    }
            }
        }

        public static void BeginImport(string source, string displayName, CatCollection collection)
        {
            if (conversion != null || EditorApplication.isPlaying)
                throw new InvalidOperationException("Exit Play Mode and wait for any active import.");
            if (!collection || !File.Exists(source) || !source.EndsWith(".ply", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Choose a collection and an existing PLY.");
            displayName = displayName.Trim();
            if (string.IsNullOrEmpty(displayName) || displayName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || displayName == "." || displayName == "..")
                throw new ArgumentException("Use a valid folder name for the splat.");
            importFolder = "Assets/SceneData/Splats/" + displayName;
            outputFolder = "Assets/StreamingAssets/Splats/" + displayName;
            if (Directory.Exists(importFolder) || Directory.Exists(outputFolder))
                throw new InvalidOperationException("That splat folder already exists. Choose a new name.");
            Directory.CreateDirectory(importFolder + "/Source");
            importedSource = importFolder + "/Source/" + displayName + ".ply";
            File.Copy(source, importedSource);
            string license = Path.Combine(Path.GetDirectoryName(source), "license.txt");
            if (File.Exists(license)) File.Copy(license, importFolder + "/Source/" + displayName + ".license.txt");
            importedName = displayName;
            targetCollection = collection;
            var start = new ProcessStartInfo
            {
                FileName = Application.platform == RuntimePlatform.WindowsEditor ? "powershell.exe" : "pwsh",
                Arguments = "-NoProfile -NonInteractive -File " + Quote(Path.GetFullPath("Tools/Build-StreamedSplat.ps1"))
                    + " -Source " + Quote(Path.GetFullPath(importedSource)) + " -Output " + Quote(Path.GetFullPath(outputFolder)),
                WorkingDirectory = Path.GetFullPath("."),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            var process = new Process { StartInfo = start };
            EditorApplication.LockReloadAssemblies();
            AssetDatabase.DisallowAutoRefresh();
            try
            {
                process.Start();
                conversion = Task.Run(async () =>
                {
                    using (process)
                    {
                        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
                        Task<string> stderr = process.StandardError.ReadToEndAsync();
                        await Task.WhenAll(stdout, stderr);
                        process.WaitForExit();
                        Directory.CreateDirectory("Logs");
                        File.WriteAllText("Logs/splat-import.log", stdout.Result + "\n" + stderr.Result);
                        return process.ExitCode;
                    }
                });
                lastResult = "Generating six LOD levels. Output is recorded in Logs/splat-import.log on completion.";
                EditorApplication.update += Poll;
            }
            catch
            {
                process.Dispose();
                AssetDatabase.AllowAutoRefresh();
                EditorApplication.UnlockReloadAssemblies();
                throw;
            }
        }

        static void Poll()
        {
            if (!conversion.IsCompleted) return;
            EditorApplication.update -= Poll;
            try
            {
                if (conversion.GetAwaiter().GetResult() != 0)
                    throw new InvalidOperationException("Conversion failed. See Logs/splat-import.log; copied source files are retained.");
                CatEntry entry = ImportGenerated(importedName, importFolder, outputFolder, importedSource);
                Undo.RecordObject(targetCollection, "Add imported splat");
                ArrayUtility.Add(ref targetCollection.cats, entry);
                EditorUtility.SetDirty(targetCollection);
                AssetDatabase.SaveAssetIfDirty(targetCollection);
                lastResult = "Imported " + importedName + ". Set its placement in the collection.";
                foreach (var window in Resources.FindObjectsOfTypeAll<SplatLibraryWindow>())
                {
                    window.collection = targetCollection;
                    window.selectedIndex = targetCollection.cats.Length - 1;
                    window.tab = 0;
                    window.Repaint();
                }
            }
            catch (Exception exception) { lastResult = exception.Message; Debug.LogException(exception); }
            finally
            {
                conversion = null;
                AssetDatabase.AllowAutoRefresh();
                EditorApplication.UnlockReloadAssemblies();
                AssetDatabase.Refresh();
            }
        }

        public static CatEntry ImportGenerated(string displayName, string assetFolder, string streamingFolder, string source)
        {
            string json = File.ReadAllText(streamingFolder + "/lod-meta.json");
            GsplatLodManifest.Parse(json, SourceCoordinates.RUB);
            Directory.CreateDirectory(assetFolder + "/Streaming");
            string previewPath = assetFolder + "/Streaming/Preview.sog";
            File.Copy(streamingFolder + "/preview.sog", previewPath, true);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var preview = AssetDatabase.LoadAssetAtPath<GsplatAsset>(previewPath);
            if (!preview) throw new InvalidOperationException("Preview import failed; check the Console.");
            string manifestPath = assetFolder + "/Streaming/StreamingManifest.asset";
            var manifest = AssetDatabase.LoadAssetAtPath<GsplatLodManifestAsset>(manifestPath);
            if (!manifest)
            {
                manifest = CreateInstance<GsplatLodManifestAsset>();
                AssetDatabase.CreateAsset(manifest, manifestPath);
            }
            manifest.Initialize(json, GsplatLodUri.StreamingAssetsScheme + streamingFolder.Substring("Assets/StreamingAssets/".Length), SourceCoordinates.RUB);
            manifest.SourcePlyGuid = AssetDatabase.AssetPathToGUID(source);
            EditorUtility.SetDirty(manifest);
            AssetDatabase.SaveAssetIfDirty(manifest);
            string license = Path.ChangeExtension(source, ".license.txt");
            string attribution = File.Exists(license) ? File.ReadAllText(license) : "Attribution not supplied. Check the source license.";
            File.WriteAllText(streamingFolder + "/Attribution.txt", attribution + "\nAdaptation: converted to streamed SOG LODs; placement adjusted for Reverie.\n");
            return new CatEntry { displayName = displayName, preview = preview, manifest = manifest, attribution = attribution };
        }

        static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
    }
}
