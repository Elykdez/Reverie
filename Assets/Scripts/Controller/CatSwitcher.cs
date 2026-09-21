using Gsplat;
using Hypocycloid.Reverie.Common;
using Hypocycloid.Reverie.Splats;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hypocycloid.Reverie.Controller
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-1100)]
    public sealed class CatSwitcher : MonoBehaviour
    {
        [SerializeField] CatCollection collection;
        [SerializeField] GsplatRenderer splatRenderer;
        [SerializeField] GsplatLodStreamer streamer;
        [SerializeField] SceneLoadingBlur loadingBlur;
        [SerializeField] FlyCameraController flyCamera;
        [SerializeField] GravityOrbitCamera gravityCamera;

        readonly ShakeGesture shake = new ShakeGesture();
        Accelerometer sensor;
        double lastSampleTime = -1;
        float nextSwapTime;
        float nextSensorCheck;
        int currentIndex = -1;

        public CatCollection Collection => collection;
        public int CurrentIndex => currentIndex;
        public string CurrentName => currentIndex >= 0 ? collection.cats[currentIndex].displayName : "";
        public bool CanSwap => collection.cats.Length > 1 && !loadingBlur.IsBlurring
            && Time.unscaledTime >= nextSwapTime;

        void Awake()
        {
            if (!collection || collection.cats.Length == 0 || !splatRenderer || !streamer
                || !loadingBlur || !flyCamera || !gravityCamera)
            {
                Debug.LogError("[Cats] Assign a collection and all scene references.", this);
                enabled = false;
                return;
            }
            int index = collection.randomOnStartup
                ? Random.Range(0, collection.cats.Length)
                : Mathf.Clamp(collection.defaultIndex, 0, collection.cats.Length - 1);
            ApplyCat(index, false);
        }

        void Update()
        {
            if (!Application.isFocused || Time.timeScale == 0f || PlayerInputGate.IsBlocked || !CanSwap)
            {
                shake.Reset();
                return;
            }
            if (collection.keyboardShortcut && Keyboard.current != null && Keyboard.current.nKey.wasPressedThisFrame)
            {
                SwapCat();
                return;
            }
            if (!collection.shakeToSwap)
                return;
            if ((sensor == null || !sensor.added) && Time.unscaledTime >= nextSensorCheck)
            {
                sensor = Accelerometer.current;
                nextSensorCheck = Time.unscaledTime + 1f;
                lastSampleTime = -1;
                shake.Reset();
                if (sensor != null && !sensor.enabled)
                    InputSystem.EnableDevice(sensor);
            }
            if (sensor == null || !sensor.added || sensor.lastUpdateTime == lastSampleTime)
                return;
            float dt = lastSampleTime < 0 ? 0f : (float)(sensor.lastUpdateTime - lastSampleTime);
            lastSampleTime = sensor.lastUpdateTime;
            if (shake.Sample(sensor.acceleration.ReadValue(), dt, Time.unscaledTime,
                collection.shakeThreshold, collection.shakeWindow))
                SwapCat();
        }

        public bool SwapCat()
        {
            if (!enabled || !CanSwap || PlayerInputGate.IsBlocked || Time.timeScale == 0f)
                return false;
            int index = collection.ChooseIndex(currentIndex, Random.Range(0, collection.cats.Length - 1));
            return ApplyCat(index, true);
        }

        public bool SelectCat(int index)
        {
            if (!enabled || index == currentIndex || !CanSwap || PlayerInputGate.IsBlocked)
                return false;
            return ApplyCat(index, true);
        }

        bool ApplyCat(int index, bool reveal)
        {
            if (index < 0 || index >= collection.cats.Length)
                return false;
            CatEntry entry = collection.cats[index];
            if (entry == null || !entry.preview || !entry.manifest || entry.scale <= 0f)
            {
                Debug.LogError("[Cats] The selected cat needs a preview, manifest and positive scale.", this);
                return false;
            }
            // Shutdown cancels pending loads and releases the outgoing cat's GPU data first.
            streamer.enabled = false;
            splatRenderer.enabled = false;
            splatRenderer.GsplatAsset = entry.preview;
            splatRenderer.transform.SetLocalPositionAndRotation(entry.position, Quaternion.Euler(entry.rotation));
            splatRenderer.transform.localScale = Vector3.one * entry.scale;
            splatRenderer.gameObject.name = entry.displayName;
            streamer.ManifestAsset = entry.manifest;
            streamer.ManifestUri = "";
            streamer.SourceCoordinates = entry.manifest.SourceCoordinates;
            splatRenderer.enabled = true;
            splatRenderer.ForceRefresh();
            streamer.enabled = true;
            flyCamera.SetOrbitSubject(splatRenderer, entry.focusPoint);
            gravityCamera.SetSubject(splatRenderer, entry.focusPoint);
            currentIndex = index;
            nextSwapTime = Time.unscaledTime + collection.swapCooldown;
            shake.Reset();
            if (reveal)
                loadingBlur.BeginReveal();
            return true;
        }

        void OnApplicationFocus(bool focused) => shake.Reset();
        void OnApplicationPause(bool paused) => shake.Reset();
        // The gravity camera can share this device; do not disable it when this component stops.
        void OnDisable() => shake.Reset();
    }
}
