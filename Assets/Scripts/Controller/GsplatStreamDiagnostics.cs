using System.Linq;
using Gsplat;
using Hypocycloid.Reverie.Common;
using Hypocycloid.Reverie.Splats;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hypocycloid.Reverie.Controller
{
    // The streamer is otherwise a black box on device. If it settles below the splat budget
    // the LOD choice is the limit; if it settles at the budget, the budget is.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GsplatLodStreamer))]
    public sealed class GsplatStreamDiagnostics : MonoBehaviour
    {
        [SerializeField, Min(0.5f)]
        float interval = 3f;

        GsplatLodStreamer streamer;
        float nextSample;
        long lastActive = -1;
        bool reportedDevice;
        int lastRendererCount = -1;

        void OnEnable()
        {
            streamer = GetComponent<GsplatLodStreamer>();
            nextSample = 0f;
            lastActive = -1;
            reportedDevice = false;
            lastRendererCount = -1;
        }

        // A build that draws nothing looks identical to one that loaded nothing, so the graphics
        // capabilities the splat path depends on are reported once per session.
        void ReportDevice()
        {
            reportedDevice = true;
            Diagnostics.Log(
                Diagnostics.Category.Rendering,
                "splat_device",
                $"api={SystemInfo.graphicsDeviceType}; compute={SystemInfo.supportsComputeShaders}; "
                    + $"vertexSSBO={SystemInfo.maxComputeBufferInputsVertex}; "
                    + $"maxBuffer={SystemInfo.maxGraphicsBufferSize}; shaderLevel={SystemInfo.graphicsShaderLevel}; "
                    + $"settingsValid={GsplatSettings.Instance != null && GsplatSettings.Instance.Valid}",
                this
            );
        }

        // Counts what is actually being drawn, rather than what LOD selection asked for.
        void ReportRenderers()
        {
            GsplatRenderer[] renderers = FindObjectsByType<GsplatRenderer>(FindObjectsSortMode.None)
                .Where(r => r && r.isActiveAndEnabled)
                .ToArray();
            if (renderers.Length == lastRendererCount)
                return;
            lastRendererCount = renderers.Length;
            long drawn = renderers.Sum(r => r.GsplatAsset ? (long)r.GsplatAsset.SplatCount : 0L);
            bool preview = streamer.PreviewRenderer && streamer.PreviewRenderer.isActiveAndEnabled;
            Diagnostics.Log(
                Diagnostics.Category.Rendering,
                "splat_renderers",
                $"renderers={renderers.Length}; splatsInAssets={drawn}; previewActive={preview}",
                this
            );
        }

        void Update()
        {
            if (Time.unscaledTime < nextSample)
                return;
            nextSample = Time.unscaledTime + interval;
            if (!reportedDevice)
                ReportDevice();
            ReportRenderers();
            long active = streamer.ActiveSplatCount;
            if (active == lastActive)
                return;
            lastActive = active;
            Diagnostics.Log(
                Diagnostics.Category.Rendering,
                "splat_stream",
                $"active={active}; budget={streamer.EffectiveSplatBudget}; files={streamer.LoadedFileCount}; "
                    + $"coverage={streamer.SceneCoverageProgress:F3}; t={Time.unscaledTime:F0}s",
                this
            );
        }
    }
}
