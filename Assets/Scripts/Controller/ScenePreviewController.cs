using System.Collections.Generic;
using Hypocycloid.Reverie.Common;
using Hypocycloid.Reverie.Splats;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Hypocycloid.Reverie.Controller
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(200)]
    public sealed class ScenePreviewController : MonoBehaviour, IBackHandler
    {
        const float EntryDuration = 0.8f;
        readonly List<Behaviour> suspendedControllers = new List<Behaviour>();
        Camera previewCamera;
        ScenePreviewPath path;
        GsplatLodStreamer[] streamers;
        Vector3 originalPosition;
        Quaternion originalRotation;
        float originalFieldOfView;
        float entryElapsed;
        int previewScene;
        int resolvedScene = int.MinValue;

        public bool IsPreviewing { get; private set; }
        public bool IsPaused { get; private set; }
        public bool IsWaitingForScene { get; private set; }
        public float Progress { get; private set; }
        public float Duration => path ? path.Duration : 0f;
        public bool ShowPath { get; private set; }
        public bool CanShowPath => isActiveAndEnabled && ResolvePath();
        public bool CanPreview => isActiveAndEnabled && !IsPreviewing
            && !PlayerInputGate.IsBlocked && Camera.main && ResolvePath();

        ScenePreviewPath ResolvePath()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (resolvedScene != activeScene.handle)
            {
                if (path)
                    path.SetPathVisible(false);
                resolvedScene = activeScene.handle;
                path = null;
                foreach (var candidate in FindObjectsByType<ScenePreviewPath>(FindObjectsSortMode.None))
                {
                    if (candidate.gameObject.scene == activeScene && candidate.IsValid)
                    {
                        path = candidate;
                        break;
                    }
                }
            }
            return path && path.IsValid ? path : null;
        }

        public void SetShowPath(bool visible)
        {
            ShowPath = visible && CanShowPath;
            UpdatePathVisibility();
        }

        void UpdatePathVisibility()
        {
            var currentPath = ResolvePath();
            if (currentPath)
                currentPath.SetPathVisible(ShowPath && (!PlayerInputGate.IsBlocked || IsPreviewing));
        }

        public bool BeginPreview()
        {
            if (IsPreviewing)
                return true;
            resolvedScene = int.MinValue;
            if (!CanPreview || !PlayerInputGate.TryAcquire(this))
                return false;

            previewCamera = Camera.main;
            previewScene = SceneManager.GetActiveScene().handle;
            originalPosition = previewCamera.transform.position;
            originalRotation = previewCamera.transform.rotation;
            originalFieldOfView = previewCamera.fieldOfView;
            Suspend(previewCamera.GetComponentsInParent<ThirdPersonCamera>(true));
            Suspend(previewCamera.GetComponentsInParent<GravityOrbitCamera>(true));
            Suspend(previewCamera.GetComponentsInParent<FlyCameraController>(true));
            streamers = FindObjectsByType<GsplatLodStreamer>(FindObjectsSortMode.None);
            Progress = 0f;
            entryElapsed = 0f;
            IsPaused = false;
            IsPreviewing = true;
            IsWaitingForScene = !HasSceneCoverage();
            return true;
        }

        void Suspend<T>(T[] controllers) where T : Behaviour
        {
            foreach (var controller in controllers)
            {
                if (!controller.enabled)
                    continue;
                suspendedControllers.Add(controller);
                controller.enabled = false;
            }
        }

        public void EndPreview()
        {
            if (!IsPreviewing)
                return;
            IsPreviewing = false;
            IsPaused = false;
            IsWaitingForScene = false;
            if (previewCamera && SceneManager.GetActiveScene().handle == previewScene
                && previewCamera.gameObject.scene.handle == previewScene)
            {
                SceneCameraRig.SetPose(previewCamera, originalPosition, originalRotation, originalFieldOfView);
            }
            foreach (var controller in suspendedControllers)
                if (controller)
                    controller.enabled = true;
            suspendedControllers.Clear();
            previewCamera = null;
            streamers = null;
            PlayerInputGate.Release(this);
        }

        void OnEnable() => BackNavigation.Register(this);

        public bool TryGoBack()
        {
            if (!IsPreviewing)
                return false;
            EndPreview();
            return true;
        }

        public void TogglePause()
        {
            if (IsPreviewing)
                IsPaused = !IsPaused;
        }

        public void Seek(float normalized)
        {
            if (!IsPreviewing || !path || !previewCamera || float.IsNaN(normalized))
                return;
            Progress = Mathf.Clamp01(normalized);
            entryElapsed = EntryDuration;
            ApplyPose();
        }

        void Update()
        {
            UpdatePathVisibility();
            if (!IsPreviewing)
                return;
            Keyboard keyboard = Keyboard.current;
            if (!Application.isFocused || keyboard == null)
                return;
            if (keyboard.spaceKey.wasPressedThisFrame)
                TogglePause();
        }

        void LateUpdate()
        {
            if (!IsPreviewing)
                return;
            if (!previewCamera || !path || !path.IsValid
                || path.gameObject.scene != SceneManager.GetActiveScene())
            {
                EndPreview();
                return;
            }
            if (IsWaitingForScene)
            {
                if (!HasSceneCoverage())
                    return;
                // Readiness is latched; later LOD refinement must not stop the tour.
                IsWaitingForScene = false;
            }
            if (IsPaused)
                return;
            float delta = Time.unscaledDeltaTime;
            entryElapsed = Mathf.Min(EntryDuration, entryElapsed + delta);
            Progress = Mathf.Repeat(Progress + delta / Duration, 1f);
            ApplyPose();
        }

        bool HasSceneCoverage()
        {
            foreach (var streamer in streamers)
                if (streamer && streamer.isActiveAndEnabled && streamer.gameObject.scene == path.gameObject.scene
                    && !streamer.HasSceneCoverage)
                    return false;
            return true;
        }

        void ApplyPose()
        {
            path.Evaluate(Progress, out Vector3 position, out Quaternion rotation);
            float blend = Mathf.SmoothStep(0f, 1f, entryElapsed / EntryDuration);
            SceneCameraRig.SetPose(previewCamera,
                Vector3.Lerp(originalPosition, position, blend),
                Quaternion.Slerp(originalRotation, rotation, blend));
        }

        void OnDisable()
        {
            BackNavigation.Unregister(this);
            EndPreview();
            ShowPath = false;
            if (path)
                path.SetPathVisible(false);
        }
    }
}
