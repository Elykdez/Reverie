using Hypocycloid.Reverie.UI;
using Hypocycloid.Splats;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Reverie.Editor
{
    public static class SplatDetailSetup
    {
        const string HudPath = "Assets/Bundles/Resources/UI/HUD.prefab";
        const string IconButtonPath = "Assets/Bundles/Resources/UI/Component/Button.prefab";
        const string SplatIconPath = "Assets/Bundles/Textures/Sprites/ic_splat.png";
        static readonly Color Ink = new Color(0.95f, 0.96f, 0.96f);

        [MenuItem("Reverie/Install Splat Detail Controls")]
        public static void Install()
        {
            if (EditorApplication.isPlaying)
                throw new System.InvalidOperationException("Exit Play Mode before installing controls.");
            var hud = PrefabUtility.LoadPrefabContents(HudPath);
            try
            {
                if (!hud.GetComponent<SplatDetailView>())
                {
                    var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                        "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
                    var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Scripts/UI/Capsule.mat");
                    var view = hud.AddComponent<SplatDetailView>();
                    Assign(view, "toggleButton", IconButton("Splats", hud.transform, new Vector2(28, 28), out var icon));
                    Assign(view, "icon", icon);
                    var panel = Rect("Splat Simplify Panel", hud.transform, new Vector2(28, 124), new Vector2(440, 350));
                    Plate(panel, material, 24);
                    Assign(view, "panel", panel);
                    Label("Splats", panel, new Vector2(24, 294), new Vector2(210, 36), 28, font);
                    Assign(view, "resetButton", Button("Reset", panel, new Vector2(246, 286), new Vector2(106, 52), font, material));
                    Assign(view, "closeButton", Button("X", panel, new Vector2(364, 286), new Vector2(52, 52), font, material));
                    Assign(view, "budgetLabel", Label("Budget", panel, new Vector2(24, 248), new Vector2(392, 32), 24, font));
                    Assign(view, "budgetSlider", Slider("Target Slider", panel, 198, 1f, 2000000f, true, material));
                    Assign(view, "lodLabel", Label("LOD", panel, new Vector2(24, 170), new Vector2(392, 32), 24, font));
                    Assign(view, "lodSlider", Slider("LOD Slider", panel, 120, -1f, 2f, false, material));
                    Assign(view, "opacityLabel", Label("Opacity", panel, new Vector2(24, 92), new Vector2(392, 32), 24, font));
                    Assign(view, "opacitySlider", Slider("Opacity Slider", panel, 42, 0f, 1f, false, material));
                    panel.gameObject.SetActive(false);
                    PrefabUtility.SaveAsPrefabAsset(hud, HudPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(hud);
            }
            var streamers = Object.FindObjectsByType<GsplatLodStreamer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var views = Object.FindObjectsByType<SplatDetailView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (streamers.Length != 1 || views.Length != 1)
                throw new System.InvalidOperationException("Open the Reverie scene with one streamer and HUD to wire controls.");
            Assign(views[0], "streamer", streamers[0]);
            PrefabUtility.RecordPrefabInstancePropertyModifications(views[0]);
            EditorSceneManager.MarkSceneDirty(views[0].gameObject.scene);
        }

        static Slider Slider(string name, Transform parent, float y, float min, float max, bool whole, Material material)
        {
            var rect = Rect(name, parent, new Vector2(24, y), new Vector2(392, 48));
            rect.gameObject.AddComponent<Image>().color = Color.clear;
            var track = Rect("Track", rect, new Vector2(16, 21), new Vector2(360, 6));
            track.gameObject.AddComponent<Image>().color = new Color(0.4f, 0.47f, 0.5f);
            var area = Rect("Handle Area", rect, new Vector2(16, 0), new Vector2(360, 48));
            var handle = Rect("Handle", area, Vector2.zero, new Vector2(32, -12));
            handle.pivot = new Vector2(0.5f, 0.5f);
            var graphic = Plate(handle, material, 16);
            graphic.color = Ink;
            var slider = rect.gameObject.AddComponent<Slider>();
            slider.handleRect = handle;
            slider.targetGraphic = graphic;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = whole;
            return slider;
        }

        static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = 5;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        static CapsuleGraphic Plate(RectTransform rect, Material material, float radius)
        {
            var graphic = rect.gameObject.AddComponent<CapsuleGraphic>();
            graphic.material = material;
            graphic.Radius = radius;
            graphic.color = new Color(0.06f, 0.09f, 0.1f, 0.94f);
            return graphic;
        }

        static TMP_Text Label(string text, Transform parent, Vector2 position, Vector2 size, float fontSize, TMP_FontAsset font)
        {
            var label = Rect("Label", parent, position, size).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = fontSize;
            label.color = Ink;
            label.text = text;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;
            return label;
        }

        static Button Button(string text, Transform parent, Vector2 position, Vector2 size, TMP_FontAsset font, Material material)
        {
            var rect = Rect(text, parent, position, size);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = Plate(rect, material, 40);
            var label = Label(text, rect, Vector2.zero, size, 26, font);
            label.alignment = TextAlignmentOptions.Center;
            return button;
        }

        // The shared icon button, showing the splat sprite and fading with the opacity prune.
        static Button IconButton(string name, Transform parent, Vector2 position, out LevelIcon icon)
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(IconButtonPath), parent);
            root.name = name;
            var rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(80, 80);
            icon = root.GetComponentInChildren<LevelIcon>();
            icon.GetComponent<Image>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SplatIconPath);
            var serialized = new SerializedObject(icon);
            serialized.FindProperty("display").enumValueIndex = (int)LevelIcon.Display.Fade;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return root.GetComponent<Button>();
        }

        static void Assign(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
