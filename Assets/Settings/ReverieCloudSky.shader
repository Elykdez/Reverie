Shader "Reverie/Cloud Sky"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Panorama", 2D) = "grey" {}
        _Tint ("Tint", Color) = (0.5, 0.5, 0.5, 1)
        _Exposure ("Exposure", Range(0, 4)) = 1
        _Rotation ("Rotation", Range(0, 360)) = 0
        _SeamBlend ("Wrap Blend", Range(0.001, 0.05)) = 0.025
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _Tint;
            float _Exposure, _Rotation, _SeamBlend;

            struct Attributes
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 position : SV_POSITION;
                float3 direction : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float angle = radians(_Rotation);
                float sine, cosine;
                sincos(angle, sine, cosine);
                float3 rotated = input.vertex.xyz;
                rotated.xz = float2(cosine * rotated.x - sine * rotated.z,
                    sine * rotated.x + cosine * rotated.z);
                output.position = UnityObjectToClipPos(float4(rotated, 1));
                output.direction = input.vertex.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 direction = normalize(input.direction);
                float2 uv = float2(0.5 - atan2(direction.z, direction.x) / (2 * UNITY_PI),
                    0.5 + asin(clamp(direction.y, -1, 1)) / UNITY_PI);
                // Correct the longitude derivative at wrap to avoid a low-mip seam.
                float2 gradientX = ddx(uv), gradientY = ddy(uv);
                gradientX.x -= round(gradientX.x);
                gradientY.x -= round(gradientY.x);
                half3 color = tex2Dgrad(_MainTex, uv, gradientX, gradientY).rgb;
                // AI panoramas can differ at their outer columns. Feather both sides
                // symmetrically so the 360-degree join has the same color on each side.
                float edgeDistance = min(uv.x, 1 - uv.x);
                float blend = 0.5 * (1 - smoothstep(0, _SeamBlend, edgeDistance));
                half3 opposite = tex2Dgrad(_MainTex, float2(1 - uv.x, uv.y),
                    float2(-gradientX.x, gradientX.y), float2(-gradientY.x, gradientY.y)).rgb;
                color = lerp(color, opposite, blend);
                return half4(color * _Tint.rgb * (2 * _Exposure), 1);
            }
            ENDCG
        }
    }
}
