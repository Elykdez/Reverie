#ifndef REVERIE_ROCK_RELIEF_INPUT_INCLUDED
#define REVERIE_ROCK_RELIEF_INPUT_INCLUDED

// Keep URP's material layout and lighting, replacing only its one-sample height offset.
#define ApplyPerPixelDisplacement ApplyURPSimpleDisplacement
#define InitializeStandardLitSurfaceData InitializeURPRockSurface
#include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
#undef ApplyPerPixelDisplacement
#undef InitializeStandardLitSurfaceData

void ApplyPerPixelDisplacement(half3 viewDirTS, inout float2 uv)
{
#if defined(_PARALLAXMAP)
    float2 dx = ddx(uv);
    float2 dy = ddy(uv);
    float3 view = normalize(float3(viewDirTS));
    // Fade subpixel relief and grazing angles to avoid distant shimmer and edge streaks.
    float footprint = max(length(dx * _BaseMap_TexelSize.zw), length(dy * _BaseMap_TexelSize.zw));
    float fade = (1.0 - smoothstep(4.0, 16.0, footprint)) * smoothstep(0.05, 0.25, view.z);
    if (fade < 0.01 || _Parallax <= 0.0)
        return;

    int steps = (int)lerp(24.0, 12.0, saturate(view.z));
    float stepDepth = rcp((float)steps);
    float2 ray = view.xy / max(view.z, 0.25) * _Parallax * fade;
    float2 startUV = uv + ray * 0.5;
    float depth = 0.0;
    float surfaceDepth = 1.0 - SAMPLE_TEXTURE2D_GRAD(_ParallaxMap, sampler_ParallaxMap, startUV, dx, dy).g;
    float previousDepth = 0.0;

    // Explicit gradients keep mip selection stable inside the divergent ray march.
    [loop]
    for (int i = 0; i < steps; ++i)
    {
        if (depth >= surfaceDepth)
            break;
        previousDepth = depth;
        depth += stepDepth;
        surfaceDepth = 1.0 - SAMPLE_TEXTURE2D_GRAD(_ParallaxMap, sampler_ParallaxMap, startUV - ray * depth, dx, dy).g;
    }

    [unroll]
    for (int refinement = 0; refinement < 3; ++refinement)
    {
        float middle = (previousDepth + depth) * 0.5;
        float heightDepth = 1.0 - SAMPLE_TEXTURE2D_GRAD(_ParallaxMap, sampler_ParallaxMap, startUV - ray * middle, dx, dy).g;
        if (middle < heightDepth)
            previousDepth = middle;
        else
            depth = middle;
    }
    uv = startUV - ray * ((previousDepth + depth) * 0.5);
#endif
}

void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData surface)
{
    InitializeURPRockSurface(uv, surface);
#if defined(_PARALLAXMAP)
    half height = SAMPLE_TEXTURE2D(_ParallaxMap, sampler_ParallaxMap, uv).g;
    // Supplement the scanned AO in deep crevices; raised stone keeps its original color.
    half cavity = smoothstep(0.12h, 0.62h, height);
    surface.occlusion *= lerp(0.65h, 1.0h, cavity);
    surface.albedo *= lerp(0.88h, 1.0h, cavity);
#endif
}

#endif
