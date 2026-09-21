using TMPro;
using UnityEngine;

namespace Hypocycloid.Reverie.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TextMeshProUGUI))]
    public sealed class FpsDisplay : MonoBehaviour
    {
        TMP_Text label;
        int frames;
        float elapsed;

        void Awake()
        {
            label = GetComponent<TMP_Text>();
        }

        void OnEnable()
        {
            frames = 0;
            elapsed = 0f;
            label.SetText("FPS: --");
        }

        void Update()
        {
            ++frames;
            elapsed += Time.unscaledDeltaTime;
            if (elapsed < 0.5f)
                return;

            label.SetText("FPS: {0:0}", frames / elapsed);
            frames = 0;
            elapsed = 0f;
        }
    }
}
