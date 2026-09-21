using Hypocycloid.Reverie.Common;
using Hypocycloid.Splats;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hypocycloid.Reverie.UI
{
    [DisallowMultipleComponent]
    public sealed class SplatDetailView : MonoBehaviour, IBackHandler
    {
        [SerializeField] GsplatLodStreamer streamer;
        [SerializeField] Button toggleButton;
        [SerializeField] LevelIcon icon;
        [SerializeField] Button closeButton;
        [SerializeField] Button resetButton;
        [SerializeField] RectTransform panel;
        [SerializeField] Slider budgetSlider;
        [SerializeField] Slider lodSlider;
        [SerializeField] Slider opacitySlider;
        [SerializeField] TMP_Text budgetLabel;
        [SerializeField] TMP_Text lodLabel;
        [SerializeField] TMP_Text opacityLabel;

        int initialBudget;
        float initialLod;
        float initialOpacity;
        float nextApply;
        float nextRefresh;
        bool budgetPending;
        bool lodPending;
        bool opacityPending;

        public bool IsOpen => panel.gameObject.activeSelf;

        void Awake()
        {
            initialBudget = streamer.SplatBudget;
            initialLod = streamer.LodBaseDistance;
            initialOpacity = streamer.OpacityPrune;
            ShowOpacity(initialOpacity);
            panel.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            toggleButton.onClick.AddListener(Toggle);
            closeButton.onClick.AddListener(Close);
            resetButton.onClick.AddListener(ResetSettings);
            budgetSlider.onValueChanged.AddListener(SetBudget);
            lodSlider.onValueChanged.AddListener(SetLod);
            opacitySlider.onValueChanged.AddListener(SetOpacity);
            BackNavigation.Register(this);
        }

        void OnDisable()
        {
            Close();
            toggleButton.onClick.RemoveListener(Toggle);
            closeButton.onClick.RemoveListener(Close);
            resetButton.onClick.RemoveListener(ResetSettings);
            budgetSlider.onValueChanged.RemoveListener(SetBudget);
            lodSlider.onValueChanged.RemoveListener(SetLod);
            opacitySlider.onValueChanged.RemoveListener(SetOpacity);
            BackNavigation.Unregister(this);
        }

        void Update()
        {
            if (Time.unscaledTime >= nextApply)
                ApplyPending();
            bool ready = streamer.isActiveAndEnabled && streamer.IsInitialized;
            toggleButton.interactable = ready && (!PlayerInputGate.IsBlocked || IsOpen);
            if (!IsOpen) return;
            if (!ready)
            {
                Close();
                return;
            }
            var space = (RectTransform)panel.parent;
            float scale = Mathf.Min(1f, (space.rect.width - 56f) / panel.rect.width,
                (space.rect.height - 144f) / panel.rect.height);
            panel.localScale = Vector3.one * Mathf.Max(0.1f, scale);
            if (Time.unscaledTime >= nextRefresh) Refresh();
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (!streamer.isActiveAndEnabled || !streamer.IsInitialized
                || !PlayerInputGate.TryAcquire(this)) return;
            panel.gameObject.SetActive(true);
            Refresh();
        }

        public void Close()
        {
            ApplyPending();
            panel.gameObject.SetActive(false);
            PlayerInputGate.Release(this);
        }

        public bool TryGoBack()
        {
            if (!IsOpen) return false;
            Close();
            return true;
        }

        void SetBudget(float value)
        {
            budgetPending = true;
            budgetLabel.text = $"Budget: {Mathf.RoundToInt(value):N0}";
        }

        void SetLod(float value)
        {
            lodPending = true;
            lodLabel.text = $"LOD: {Mathf.Pow(10f, value):0.0}";
        }

        void SetOpacity(float value)
        {
            opacityPending = true;
            opacityLabel.text = $"Opacity: {value:0.00}";
            ShowOpacity(value);
        }

        // A higher prune threshold hides more splats, so the icon fades with it.
        void ShowOpacity(float prune) => icon.Level = 1f - prune;

        void ApplyPending()
        {
            if (!budgetPending && !lodPending && !opacityPending) return;
            if (budgetPending) streamer.SetSplatBudget(Mathf.RoundToInt(budgetSlider.value));
            if (lodPending) streamer.SetLodBaseDistance(Mathf.Pow(10f, lodSlider.value));
            if (opacityPending) streamer.SetOpacityPrune(opacitySlider.value);
            budgetPending = lodPending = opacityPending = false;
            nextApply = Time.unscaledTime + 0.1f;
        }

        public void ResetSettings()
        {
            budgetPending = lodPending = opacityPending = false;
            streamer.SetSplatBudget(initialBudget);
            streamer.SetLodBaseDistance(initialLod);
            streamer.SetOpacityPrune(initialOpacity);
            Refresh();
        }

        void Refresh()
        {
            if (!budgetPending)
            {
                // Range changes must not invoke the slider and silently edit the scene target.
                budgetSlider.SetValueWithoutNotify(budgetSlider.minValue);
                budgetSlider.maxValue = streamer.FinestSplatCount > 0
                    ? Mathf.Min(int.MaxValue - 128f, streamer.FinestSplatCount)
                    : Mathf.Max(1, streamer.SplatBudget);
                budgetSlider.SetValueWithoutNotify(streamer.SplatBudget);
                budgetLabel.text = $"Budget: {Mathf.RoundToInt(budgetSlider.value):N0}";
            }
            if (!lodPending)
            {
                lodSlider.SetValueWithoutNotify(lodSlider.minValue);
                // LOD thresholds are multiplicative; a linear 0.1-100 slider wastes most of its travel.
                lodSlider.maxValue = Mathf.Log10(Mathf.Max(100f, streamer.LodBaseDistance));
                lodSlider.SetValueWithoutNotify(Mathf.Log10(streamer.LodBaseDistance));
                lodLabel.text = $"LOD: {streamer.LodBaseDistance:0.0}";
            }
            if (!opacityPending)
            {
                opacitySlider.SetValueWithoutNotify(streamer.OpacityPrune);
                opacityLabel.text = $"Opacity: {streamer.OpacityPrune:0.00}";
                ShowOpacity(streamer.OpacityPrune);
            }
            nextRefresh = Time.unscaledTime + 0.25f;
        }
    }
}
