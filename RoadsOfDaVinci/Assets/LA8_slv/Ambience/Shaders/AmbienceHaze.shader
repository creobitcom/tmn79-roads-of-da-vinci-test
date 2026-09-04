Shader "TMN/AmbienceHaze"
{
    Properties
    {
        _MainTex ("Haze Texture (optional)", 2D) = "white" {}
        _Color ("Color", Color) = (0.8, 0.85, 0.9, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.35
        _UseTexture ("Use Texture", Range(0, 1)) = 0
        _NoiseScale ("Noise Scale", Range(0.1, 20)) = 3
        _Detail ("Detail", Range(0, 1)) = 0.5
        _Coverage ("Coverage", Range(0, 1)) = 0.5
        _Softness ("Softness", Range(0.01, 1)) = 0.5
        _Scroll ("Scroll Speed", Vector) = (0.02, 0.004, 0, 0)
        _WindResponse ("Wind Response", Range(0, 2)) = 1
        _MaskBottom ("Mask Bottom", Range(0, 1)) = 0
        _MaskTop ("Mask Top", Range(0, 1)) = 1
        _MaskFeather ("Mask Feather", Range(0.001, 1)) = 0.3
        _EdgeFade ("Edge Fade", Range(0, 0.5)) = 0.08
        _Darken ("Darken", Range(0, 1)) = 0
        _LightShafts ("Light Shafts", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Blend One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half _Opacity;
                half _UseTexture;
                half _NoiseScale;
                half _Detail;
                half _Coverage;
                half _Softness;
                float4 _Scroll;
                half _WindResponse;
                half _MaskBottom;
                half _MaskTop;
                half _MaskFeather;
                half _EdgeFade;
                half _Darken;
                half _LightShafts;
            CBUFFER_END

            float4 _TMNSunDirection;
            float4 _TMNSunColor;
            float4 _TMNWind;
            float _TMNWindGust;
            float _TMNTime;
            float4 _TMNPointLightPositions[8];
            float4 _TMNPointLightColors[8];
            float _TMNPointLightCount;

            float2 Hash2(float2 p)
            {
                float3 q = frac(p.xyx * float3(0.1031, 0.1030, 0.0973));
                q += dot(q, q.yzx + 33.33);
                return frac((q.xx + q.yz) * q.zy);
            }

            float ValueNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 local = frac(p);
                float2 smoothed = local * local * (3.0 - 2.0 * local);

                float a = Hash2(cell).x;
                float b = Hash2(cell + float2(1, 0)).x;
                float c = Hash2(cell + float2(0, 1)).x;
                float d = Hash2(cell + float2(1, 1)).x;

                return lerp(lerp(a, b, smoothed.x), lerp(c, d, smoothed.x), smoothed.y);
            }

            float Fbm(float2 p, half detail)
            {
#if defined(SHADER_API_MOBILE)
                detail = 0.0;
#endif
                float value = ValueNoise(p) * 0.55;
                value += ValueNoise(p * 2.13 + 17.3) * 0.28 * detail;
                value += ValueNoise(p * 4.71 + 43.7) * 0.17 * detail * detail;
                return value / (0.55 + 0.28 * detail + 0.17 * detail * detail);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float windPush = _WindResponse * _TMNWind.z * (1.0 + _TMNWindGust * 0.8);
                float2 drift = (_Scroll.xy + _TMNWind.xy * windPush * 0.05) * _TMNTime;

                float density;
                if (_LightShafts > 0.5)
                {
                    float2 sunDir = _TMNSunDirection.xy;
                    float sunLen = max(length(sunDir), 0.001);
                    sunDir /= sunLen;
                    float2 perp = float2(-sunDir.y, sunDir.x);
                    float bandCoord = dot(IN.worldPos.xy, perp);
                    float bandNoise = Fbm(float2(bandCoord * _NoiseScale * 0.15 + drift.x, drift.y * 0.25), _Detail);
                    float shaftThreshold = 1.0 - _Coverage;
                    density = smoothstep(shaftThreshold - _Softness * 0.5, shaftThreshold + _Softness * 0.5, bandNoise);
                }
                else if (_UseTexture > 0.5)
                {
                    half layerA = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv * _MainTex_ST.xy + drift).r;
                    half layerB = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv * _MainTex_ST.xy * 1.7 - drift * 0.6).r;
                    density = layerA * 0.65 + layerB * 0.35;
                }
                else
                {
                    float2 noiseUv = IN.worldPos.xy * (_NoiseScale * 0.04);
                    float layerA = Fbm(noiseUv + drift * 6.0, _Detail);
                    float layerB = Fbm(noiseUv * 1.63 - drift * 3.7 + 11.7, _Detail);
                    density = layerA * 0.62 + layerB * 0.38;
                }

                float threshold = 1.0 - _Coverage;
                density = smoothstep(threshold - _Softness * 0.5, threshold + _Softness * 0.5, density);

                float maskLow = smoothstep(_MaskBottom - _MaskFeather, _MaskBottom + _MaskFeather, IN.uv.y);
                float maskHigh = 1.0 - smoothstep(_MaskTop - _MaskFeather, _MaskTop + _MaskFeather, IN.uv.y);
                float edge = smoothstep(0.0, _EdgeFade + 0.0001, IN.uv.x) * (1.0 - smoothstep(1.0 - _EdgeFade - 0.0001, 1.0, IN.uv.x));

                float alpha = saturate(density * maskLow * maskHigh * edge * _Opacity);
                if (alpha < 0.002)
                {
                    return half4(0, 0, 0, 0);
                }

                half3 tint = _Color.rgb * lerp(half3(1, 1, 1), _TMNSunColor.rgb * _TMNSunColor.a, 0.35);
                tint *= 1.0 - _Darken;

                half3 glow = half3(0, 0, 0);
                int lightCount = (int)_TMNPointLightCount;
                for (int li = 0; li < lightCount; li++)
                {
                    float2 delta = IN.worldPos.xy - _TMNPointLightPositions[li].xy;
                    float atten = saturate(1.0 - length(delta) / max(_TMNPointLightPositions[li].z, 0.001));
                    atten *= atten;
                    glow += _TMNPointLightColors[li].rgb * atten;
                }

                tint += glow * 0.7 * density;

                return half4(tint * alpha, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
