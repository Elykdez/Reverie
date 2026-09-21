using Hypocycloid.Reverie.Common;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Hypocycloid.Reverie.UI
{
    // One HUD button owns the whole mix: AudioListener.volume scales every source, so the
    // ambient loop and anything added later follow it. Tapping the speaker unfolds a vertical
    // slider above it, and the camera rigs poll PlayerInputGate, so the gate is held for as
    // long as the slider is up or dragging it would spin the scene at the same time.
    [DisallowMultipleComponent]
    public sealed class VolumeControl : MonoBehaviour, IBackHandler
    {
        const string VolumeKey = "Hypocycloid.Reverie.MasterVolume";

        [SerializeField]
        Button toggleButton;

        [SerializeField]
        VolumeIcon icon;

        [SerializeField]
        RectTransform panel;

        [SerializeField]
        CanvasGroup panelGroup;

        [SerializeField]
        Slider slider;

        // The catcher sits outside the HUD so it also covers the cutout, which puts it
        // beyond a prefab of this control: left empty, only the button folds the slider away.
        [Tooltip(
            "Optional full screen catcher that closes the slider when anything else is tapped."
        )]
        [SerializeField]
        Button dismissArea;

        [SerializeField, Range(0f, 1f)]
        float defaultVolume = 0.7f;

        [SerializeField, Min(0.01f)]
        float openDuration = 0.18f;

        [Tooltip("Seconds without a touch before the slider folds away. Zero keeps it open.")]
        [SerializeField, Min(0f)]
        float autoCloseDelay = 4f;

        bool open;
        float openAmount;
        float idleTime;
        bool unsavedVolume;

        public bool IsOpen => open;

        void Awake()
        {
            float volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, defaultVolume));
            AudioListener.volume = volume;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.SetValueWithoutNotify(volume);
            icon.Level = volume;
            openAmount = 0f;
            ApplyOpenAmount();
            ShowDismissArea(false);
        }

        void OnEnable()
        {
            toggleButton.onClick.AddListener(Toggle);
            if (dismissArea)
                dismissArea.onClick.AddListener(Close);
            slider.onValueChanged.AddListener(SetVolume);
            BackNavigation.Register(this);
        }

        void OnDisable()
        {
            toggleButton.onClick.RemoveListener(Toggle);
            if (dismissArea)
                dismissArea.onClick.RemoveListener(Close);
            slider.onValueChanged.RemoveListener(SetVolume);
            BackNavigation.Unregister(this);
            Close();
            openAmount = 0f;
            ApplyOpenAmount();
            SaveVolume();
        }

        void Update()
        {
            float target = open ? 1f : 0f;
            if (!Mathf.Approximately(openAmount, target))
            {
                openAmount = Mathf.MoveTowards(
                    openAmount,
                    target,
                    Time.unscaledDeltaTime / openDuration
                );
                ApplyOpenAmount();
            }

            if (!open || autoCloseDelay <= 0f)
                return;
            // While the slider is up, any press is either a drag on it or the tap that closes
            // it, so holding the handle still is enough to keep the panel alive.
            Pointer pointer = Pointer.current;
            idleTime =
                pointer != null && pointer.press.isPressed ? 0f : idleTime + Time.unscaledDeltaTime;
            if (idleTime >= autoCloseDelay)
                Close();
        }

        public void Toggle()
        {
            if (open)
                Close();
            else
                Open();
        }

        public void Open()
        {
            if (open || !PlayerInputGate.TryAcquire(this))
                return;
            open = true;
            idleTime = 0f;
            ShowDismissArea(true);
            ApplyOpenAmount();
            Diagnostics.Log(
                Diagnostics.Category.Interaction,
                "volume_opened",
                $"volume={AudioListener.volume:F2}",
                this
            );
        }

        public void Close()
        {
            if (!open)
                return;
            open = false;
            ShowDismissArea(false);
            ApplyOpenAmount();
            PlayerInputGate.Release(this);
            SaveVolume();
            Diagnostics.Log(
                Diagnostics.Category.Interaction,
                "volume_closed",
                $"volume={AudioListener.volume:F2}",
                this
            );
        }

        public bool TryGoBack()
        {
            if (!open)
                return false;
            Close();
            return true;
        }

        void SetVolume(float value)
        {
            value = Mathf.Clamp01(value);
            AudioListener.volume = value;
            icon.Level = value;
            PlayerPrefs.SetFloat(VolumeKey, value);
            unsavedVolume = true;
            idleTime = 0f;
        }

        void ShowDismissArea(bool visible)
        {
            if (dismissArea)
                dismissArea.gameObject.SetActive(visible);
        }

        void ApplyOpenAmount()
        {
            float eased = openAmount * openAmount * (3f - 2f * openAmount);
            panelGroup.alpha = eased;
            panelGroup.blocksRaycasts = open;
            panelGroup.interactable = open;
            // The panel pivots on its bottom edge, so this grows it out of the button.
            panel.localScale = new Vector3(
                Mathf.Lerp(0.88f, 1f, eased),
                Mathf.Lerp(0.72f, 1f, eased),
                1f
            );
            bool live = openAmount > 0f;
            if (panel.gameObject.activeSelf != live)
                panel.gameObject.SetActive(live);
        }

        // Android can kill the process straight from the background, so the write is flushed
        // on the way out rather than left for OnDisable.
        void OnApplicationPause(bool paused)
        {
            if (paused)
                SaveVolume();
        }

        void SaveVolume()
        {
            if (!unsavedVolume)
                return;
            unsavedVolume = false;
            PlayerPrefs.Save();
        }
    }
}
