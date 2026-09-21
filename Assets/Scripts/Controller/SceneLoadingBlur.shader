Shader "Hidden/Reverie/SceneLoadingBlur"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        float2 _BlurRadiusUV;

        half4 Blur(Varyings input, float2 axis)
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            half4 color = 0;
            float weightSum = 0;
            [unroll]
            for (int i = -12; i <= 12; ++i)
            {
                float offset = i / 12.0;
                float weight = exp(-4.5 * offset * offset);
                color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp,
                    input.texcoord + axis * offset) * weight;
                weightSum += weight;
            }
            return color / weightSum;
        }

        half4 Horizontal(Varyings input) : SV_Target
        {
            return Blur(input, float2(_BlurRadiusUV.x, 0));
        }

        half4 Vertical(Varyings input) : SV_Target
        {
            return Blur(input, float2(0, _BlurRadiusUV.y));
        }
        ENDHLSL

        Pass
        {
            Name "Horizontal"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Horizontal
            ENDHLSL
        }
        Pass
        {
            Name "Vertical"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Vertical
            ENDHLSL
        }
    }
}
