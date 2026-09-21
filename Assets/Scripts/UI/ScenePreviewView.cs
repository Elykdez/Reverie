using Hypocycloid.Reverie.Controller;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hypocycloid.Reverie.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ScenePreviewController))]
    public sealed class ScenePreviewView : MonoBehaviour
    {
        [SerializeField] Button previewButton;
        [SerializeField] GameObject panel;
        [SerializeField] PreviewProgressBar progressBar;
        [SerializeField] Button playPauseButton;
        [SerializeField] TMP_Text playPauseLabel;
        [SerializeField] Button exitButton;
        [SerializeField] TMP_Text timeLabel;
        [SerializeField] Toggle pathToggle;
        [SerializeField] Toggle previewPathToggle;
        [SerializeField] GameObject[] explorationControls;

        ScenePreviewController controller;
        bool[] controlsWereActive;
        bool showing;
        bool resumeAfterScrub;
        int displayedSecond = -1;
        string displayedState;

        void Awake()
        {
            controller = GetComponent<ScenePreviewController>();
            controlsWereActive = new bool[explorationControls.Length];
            panel.SetActive(false);
        }

        void OnEnable()
        {
            previewButton.onClick.AddListener(BeginPreview);
            playPauseButton.onClick.AddListener(TogglePause);
            exitButton.onClick.AddListener(EndPreview);
            progressBar.Slider.onValueChanged.AddListener(Seek);
            progressBar.ScrubStarted += BeginScrub;
            progressBar.ScrubEnded += EndScrub;
            pathToggle.onValueChanged.AddListener(SetShowPath);
            previewPathToggle.onValueChanged.AddListener(SetShowPath);
        }

        void OnDisable()
        {
            previewButton.onClick.RemoveListener(BeginPreview);
            playPauseButton.onClick.RemoveListener(TogglePause);
            exitButton.onClick.RemoveListener(EndPreview);
            progressBar.Slider.onValueChanged.RemoveListener(Seek);
            progressBar.ScrubStarted -= BeginScrub;
            progressBar.ScrubEnded -= EndScrub;
            pathToggle.onValueChanged.RemoveListener(SetShowPath);
            previewPathToggle.onValueChanged.RemoveListener(SetShowPath);
            controller.SetShowPath(false);
            controller.EndPreview();
            Show(false);
        }

        void Update()
        {
            Show(controller.IsPreviewing);
            previewButton.interactable = controller.CanPreview;
            pathToggle.SetIsOnWithoutNotify(controller.ShowPath);
            previewPathToggle.SetIsOnWithoutNotify(controller.ShowPath);
            pathToggle.interactable = previewPathToggle.interactable = controller.CanShowPath;
            if (!showing)
                return;

            progressBar.Slider.SetValueWithoutNotify(controller.Progress);
            progressBar.Slider.interactable = !controller.IsWaitingForScene;
            playPauseButton.interactable = !progressBar.IsDragging;
            string state = controller.IsPaused ? "Play" : "Pause";
            if (displayedState != state)
            {
                playPauseLabel.text = state;
                displayedState = state;
            }
            int second = Mathf.FloorToInt(controller.Progress * controller.Duration);
            if (controller.IsWaitingForScene)
            {
                timeLabel.text = "Loading scene...";
                displayedSecond = -1;
            }
            else if (displayedSecond != second)
            {
                int duration = Mathf.CeilToInt(controller.Duration);
                timeLabel.text = $"{second / 60:00}:{second % 60:00} / {duration / 60:00}:{duration % 60:00}";
                displayedSecond = second;
            }
        }

        void Show(bool visible)
        {
            if (showing == visible)
                return;
            showing = visible;
            for (int i = 0; i < explorationControls.Length; ++i)
            {
                if (!explorationControls[i])
                    continue;
                if (visible)
                    controlsWereActive[i] = explorationControls[i].activeSelf;
                explorationControls[i].SetActive(visible ? false : controlsWereActive[i]);
            }
            panel.SetActive(visible);
            displayedSecond = -1;
            displayedState = null;
            if (!visible)
                resumeAfterScrub = false;
        }

        void BeginPreview()
        {
            if (controller.BeginPreview())
                Show(true);
        }

        void EndPreview() => controller.EndPreview();
        void TogglePause() => controller.TogglePause();
        void Seek(float value) => controller.Seek(value);
        void SetShowPath(bool visible) => controller.SetShowPath(visible);

        void BeginScrub()
        {
            resumeAfterScrub = controller.IsPreviewing && !controller.IsPaused;
            if (resumeAfterScrub)
                controller.TogglePause();
        }

        void EndScrub()
        {
            if (resumeAfterScrub && controller.IsPreviewing && controller.IsPaused)
                controller.TogglePause();
            resumeAfterScrub = false;
        }
    }
}
