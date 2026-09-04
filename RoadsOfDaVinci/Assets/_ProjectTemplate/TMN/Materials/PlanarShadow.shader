Shader "TMN/PlanarShadow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (0, 0, 0, 0.35)
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.1
        _Softness ("Softness", Range(0, 1)) = 0.35
        _TipSoftness ("Tip Softness", Range(0, 1)) = 0.25
        _TipFade ("Tip Fade", Range(0, 1)) = 0.2
        _Taper ("Taper", Range(-1, 1)) = 0.0
        _ScaleX ("Scale X", Range(0.2, 3)) = 1.0
        _ShadowOffset ("Offset", Vector) = (0, 0, 0, 0)
        _ShadowShear ("Shear", Vector) = (0.2, -0.3, 0, 0)
        _ProjectionMode ("Projection Mode", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Offset -1, -1

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 heightFade : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half _Cutoff;
                half _Softness;
                half _TipSoftness;
                half _TipFade;
                half _Taper;
                half _ScaleX;
                float4 _ShadowOffset;
                float4 _ShadowShear;
                float _ProjectionMode;
            CBUFFER_END

            float _PlanarShadowZBias;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float3 pivot = TransformObjectToWorld(float3(0, 0, 0));
                float3 worldPos = TransformObjectToWorld(IN.positionOS.xyz);

                float heightNorm = 0.0;
                if (_ProjectionMode < 0.5)
                {
                    float yRel = worldPos.y - pivot.y;
                    heightNorm = saturate(abs(yRel) * 0.8);
                    float taper = 1.0 + yRel * _Taper;
                    worldPos.x = pivot.x + (worldPos.x - pivot.x) * _ScaleX * taper + _ShadowOffset.x + yRel * _ShadowShear.x;
                    worldPos.y = pivot.y + _ShadowOffset.y + yRel * _ShadowShear.y;
                }
                else
                {
                    float scaleY = _ShadowShear.z > 0.001 ? _ShadowShear.z : _ScaleX;
                    worldPos.x = pivot.x + (worldPos.x - pivot.x) * _ScaleX + _ShadowOffset.x;
                    worldPos.y = pivot.y + (worldPos.y - pivot.y) * scaleY + _ShadowOffset.y;
                }

                worldPos.z = pivot.z + _PlanarShadowZBias;
                OUT.positionCS = TransformWorldToHClip(worldPos);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.heightFade = float2(heightNorm, 1.0 - heightNorm * _TipFade);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float currentSoftness = _Softness + IN.heightFade.x * _TipSoftness;
                if (currentSoftness <= 0.01)
                {
                    half a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).a;
                    clip(a - _Cutoff);
                    return half4(_Color.rgb, _Color.a * IN.heightFade.y);
                }

                float blur = currentSoftness * 0.009;
                half a0 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).a;
                half a1 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(blur, blur)).a;
                half a2 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(-blur, blur)).a;
                half a3 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(blur, -blur)).a;
                half a4 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(-blur, -blur)).a;
                half a5 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(blur * 1.5, 0)).a;
                half a6 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(-blur * 1.5, 0)).a;
                half a7 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(0, blur * 1.5)).a;
                half a8 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(0, -blur * 1.5)).a;

                half avg = (a0 * 2.0 + a1 + a2 + a3 + a4 + a5 + a6 + a7 + a8) / 10.0;
                half smoothA = smoothstep(_Cutoff * 0.25, _Cutoff + currentSoftness * 0.6, avg);
                clip(smoothA - 0.005);
                return half4(_Color.rgb, _Color.a * smoothA * IN.heightFade.y);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
