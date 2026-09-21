using System.Collections;
using System.Linq;
using Hypocycloid.Reverie.Splats;
using UnityEngine;

namespace Hypocycloid.Reverie.Controller
{
    [DisallowMultipleComponent]
    public sealed class SceneLoadingBlur : MonoBehaviour
    {
        [SerializeField, Min(0f), Tooltip("Maximum blur radius at 1080p; scales with the output resolution.")] float blurRadius = 48f;
        [SerializeField, Min(0f)] float fadeDuration = 0.8f;
        [SerializeField, Min(1f)] float maxLoadingSeconds = 120f;

        static SceneLoadingBlur active;
        float radius;
        internal static float CurrentRadius => active ? active.radius : 0f;
        public bool IsBlurring => radius > 0f;
        public float LoadingProgress { get; private set; }

        void Start() => BeginReveal();

        public void BeginReveal()
        {
            StopAllCoroutines();
            Release();
            StartCoroutine(Reveal());
        }

        IEnumerator Reveal()
        {
            var streamers = FindObjectsByType<GsplatLodStreamer>(FindObjectsSortMode.None)
                .Where(s => s.gameObject.scene == gameObject.scene && s.isActiveAndEnabled).ToArray();
            LoadingProgress = GetCoverageProgress(streamers);
            if (LoadingProgress >= 1f || blurRadius <= 0f)
                yield break;

            active = this;
            radius = RadiusForProgress(LoadingProgress);

            // Reveal on startup or an explicit cat swap, not on camera-driven LOD changes.
            float started = Time.realtimeSinceStartup;
            while (radius > 0f)
            {
                // Never increase blur when LOD selection temporarily changes during startup.
                LoadingProgress = Mathf.Max(LoadingProgress, GetCoverageProgress(streamers));
                bool timedOut = Time.realtimeSinceStartup - started >= maxLoadingSeconds;
                float targetRadius = timedOut ? 0f : RadiusForProgress(LoadingProgress);
                radius = fadeDuration <= 0f ? targetRadius : Mathf.MoveTowards(
                    radius, targetRadius, blurRadius * Time.unscaledDeltaTime / fadeDuration);
                yield return null;
            }
            Release();
        }

        float RadiusForProgress(float progress) => blurRadius * (1f - Mathf.SmoothStep(0f, 1f, progress));

        static float GetCoverageProgress(GsplatLodStreamer[] streamers)
        {
            float total = 0f;
            int count = 0;
            foreach (var streamer in streamers)
            {
                if (!streamer || !streamer.isActiveAndEnabled)
                    continue;
                total += streamer.SceneCoverageProgress;
                ++count;
            }
            return count == 0 ? 1f : total / count;
        }

        void OnDisable()
        {
            StopAllCoroutines();
            Release();
        }

        void Release()
        {
            if (active == this)
                active = null;
            radius = 0f;
        }
    }
}
