using UnityEngine;

namespace Hypocycloid.Reverie.Common
{
    // The 9RT runs its panel at 120Hz, and rendering a splat scene that fast burns battery
    // and heat for no visible gain. vSync has to go first or it overrides the target.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-2000)]
    public sealed class FrameRateLimiter : MonoBehaviour
    {
        [SerializeField, Range(30, 120)]
        int maxFrameRate = 60;

        void OnEnable()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = maxFrameRate;
            Diagnostics.Log(
                Diagnostics.Category.Rendering,
                "frame_rate_capped",
                $"target={maxFrameRate}; refreshRate={Screen.currentResolution.refreshRateRatio.value:F0}",
                this
            );
        }
    }
}
