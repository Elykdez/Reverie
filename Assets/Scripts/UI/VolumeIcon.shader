// Speaker icon assembled from signed distance fields. The level arrives per vertex in UV1 so
// one shared material can drive every icon: arcs fade in as the level climbs and a slash, with
// a gap punched through the shape underneath it, fades in as the level reaches silence.
Shader "Reverie/UI/Volume Icon"
{
    Properties
    {
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float2 level : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float level : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float4 _ClipRect;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color;
                o.level = v.level.x;
                return o;
            }

            float SdBox(float2 p, float2 halfSize)
            {
                float2 d = abs(p) - halfSize;
                return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0);
            }

            float SdSegment(float2 p, float2 a, float2 b)
            {
                float2 pa = p - a;
                float2 ba = b - a;
                float h = saturate(dot(pa, ba) / dot(ba, ba));
                return length(pa - ba * h);
            }

            // Exact inside a convex outline and conservative outside, which is all the one
            // pixel coverage ramp below ever reads.
            float Edge(float2 p, float2 a, float2 b)
            {
                float2 e = b - a;
                return dot(p - a, normalize(float2(e.y, -e.x)));
            }

            // Arc of half aperture sc = (sin, cos), opening towards +y, mirrored across x.
            float SdArc(float2 p, float2 sc, float radius, float thickness)
            {
                p.x = abs(p.x);
                float d = sc.y * p.x > sc.x * p.y
                    ? length(p - sc * radius)
                    : abs(length(p) - radius);
                return d - thickness;
            }

            float Coverage(float d, float pixel)
            {
                return saturate(0.5 - d / pixel);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = i.texcoord * 2.0 - 1.0;
                float pixel = max(fwidth(p.x), 1e-5);

                float neck = SdBox(p - float2(-0.56, 0.0), float2(0.20, 0.25));
                float horn = max(
                    max(Edge(p, float2(-0.40, -0.26), float2(-0.02, -0.80)),
                        Edge(p, float2(-0.02, -0.80), float2(-0.02, 0.80))),
                    max(Edge(p, float2(-0.02, 0.80), float2(-0.40, 0.26)),
                        Edge(p, float2(-0.40, 0.26), float2(-0.40, -0.26))));
                float body = min(neck, horn) - 0.06;

                // Arcs are authored opening towards +x, so the axes swap into SdArc's frame.
                float2 wave = float2(p.y, p.x - 0.02);
                const float2 aperture = float2(0.743, 0.669);
                float arcNear = SdArc(wave, aperture, 0.30, 0.075);
                float arcMid = SdArc(wave, aperture, 0.56, 0.075);
                float arcFar = SdArc(wave, aperture, 0.82, 0.075);

                float slash = SdSegment(p, float2(-0.78, 0.70), float2(0.78, -0.70)) - 0.085;

                float level = i.level;
                float muted = 1.0 - smoothstep(0.0, 0.05, level);
                float shape = Coverage(body, pixel);
                shape = max(shape, Coverage(arcNear, pixel) * smoothstep(0.03, 0.22, level));
                shape = max(shape, Coverage(arcMid, pixel) * smoothstep(0.34, 0.55, level));
                shape = max(shape, Coverage(arcFar, pixel) * smoothstep(0.66, 0.88, level));

                float gap = Coverage(slash - 0.10, pixel) * muted;
                float alpha = max(shape * (1.0 - gap), Coverage(slash, pixel) * muted);

                fixed4 color = i.color;
                color.a *= alpha;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                clip(color.a - 0.001);
                return color;
            }
        ENDCG
        }
    }
}
