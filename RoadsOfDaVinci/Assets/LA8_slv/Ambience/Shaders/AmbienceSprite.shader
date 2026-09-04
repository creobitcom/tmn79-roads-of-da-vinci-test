Shader "TMN/AmbienceSprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _LitAmount ("Lit Amount", Range(0, 1)) = 1
        _VolumeAmount ("Volume", Range(0, 4)) = 0.6
        _RimAmount ("Rim", Range(0, 2)) = 0.3
        _GroundShade ("Ground Shade", Range(0, 1)) = 0.25
        _GroundShadeHeight ("Ground Shade Height", Range(0.05, 6)) = 1
        _WindStrength ("Wind Strength", Range(0, 2)) = 0
        _HazeBlend ("Haze Blend", Range(0, 1)) = 0
        _Saturation ("Saturation", Range(0, 2)) = 1
        _GradeInfluence ("Grade Influence", Range(0, 1)) = 1
        _GradeOverride ("Grade Override", Vector) = (1, 1, 0, 0)
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
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 groundFade : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float4 _MainTex_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half _LitAmount;
                half _VolumeAmount;
                half _RimAmount;
                half _GroundShade;
                half _GroundShadeHeight;
                half _WindStrength;
                half _HazeBlend;
                half _Saturation;
                half _GradeInfluence;
                half4 _GradeOverride;
            CBUFFER_END

            float4 _TMNSunDirection;
            float4 _TMNSunColor;
            float4 _TMNAmbientColor;
            float4 _TMNWind;
            float4 _TMNHazeColor;
            float _TMNWindGust;
            float _TMNTime;
            float4 _TMNGrade;
            float4 _TMNPointLightPositions[8];
            float4 _TMNPointLightColors[8];
            float _TMNPointLightCount;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float3 pivot = TransformObjectToWorld(float3(0, 0, 0));
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float yRel = positionWS.y - pivot.y;

                float windAmount = _WindStrength * _TMNWind.z;
                if (windAmount > 0.0005)
                {
                    float phase = pivot.x * 0.83 + pivot.y * 0.37;
                    float t = _TMNTime * max(_TMNWind.w, 0.0001);
                    float wave = sin(t * 6.2831 + phase) * 0.62 + sin(t * 3.11 + phase * 1.73) * 0.38;
                    float amp = windAmount * (1.0 + _TMNWindGust) * max(yRel, 0.0) * 0.08;
                    positionWS.xy += _TMNWind.xy * wave * amp;
                }

                OUT.positionCS = TransformWorldToHClip(positionWS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color;
                OUT.groundFade = float2(saturate(yRel / max(_GroundShadeHeight, 0.0001)), 0);
                OUT.worldPos = positionWS;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                half4 baseColor = texel * _Color * IN.color;

                half3 sunRgb = _TMNSunColor.rgb * _TMNSunColor.a;
                half3 sunTerm = sunRgb;

                if (_VolumeAmount > 0.0005)
                {
                    float2 step = _MainTex_TexelSize.xy * 2.0;
                    half alphaRight = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(step.x, 0)).a;
                    half alphaLeft = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv - float2(step.x, 0)).a;
                    half alphaUp = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(0, step.y)).a;
                    half alphaDown = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv - float2(0, step.y)).a;

                    float2 gradient = float2(alphaLeft - alphaRight, alphaDown - alphaUp);
                    float edge = saturate(length(gradient) * 2.2) * saturate(_VolumeAmount);
                    edge *= smoothstep(0.25, 0.75, texel.a);

                    float2 gradientDir = gradient / max(length(gradient), 0.0001);
                    float3 normalWS = normalize(float3(gradientDir, -1.0));
                    float3 sunWS = normalize(float3(_TMNSunDirection.xy, -0.75));
                    half ndl = saturate(dot(normalWS, sunWS));

                    half shade = lerp(1.0, 0.7 + 0.3 * ndl, edge);
                    half rim = saturate(dot(gradientDir, _TMNSunDirection.xy)) * edge;
                    sunTerm = sunRgb * shade + sunRgb * rim * _RimAmount;
                }

                half3 lightRgb = _TMNAmbientColor.rgb + sunTerm;

                int lightCount = (int)_TMNPointLightCount;
                for (int li = 0; li < lightCount; li++)
                {
                    float2 delta = IN.worldPos.xy - _TMNPointLightPositions[li].xy;
                    float atten = saturate(1.0 - length(delta) / max(_TMNPointLightPositions[li].z, 0.001));
                    atten *= atten;
                    lightRgb += _TMNPointLightColors[li].rgb * atten;
                }

                half3 rgb = baseColor.rgb * lerp(half3(1, 1, 1), lightRgb, _LitAmount);
                rgb *= lerp(1.0 - _GroundShade, 1.0, IN.groundFade.x);

                half3 grade = lerp(_TMNGrade.xyz, _GradeOverride.xyz, _GradeOverride.w);
                grade = lerp(half3(1.0, 1.0, 0.0), grade, _GradeInfluence);

                half luminance = dot(rgb, half3(0.2126, 0.7152, 0.0722));
                rgb = lerp(half3(luminance, luminance, luminance), rgb, _Saturation * grade.y);
                rgb = lerp(rgb, _TMNHazeColor.rgb * max(luminance, 0.35), _HazeBlend * _TMNHazeColor.a);

                rgb = (rgb - 0.5) * grade.x + 0.5;
                rgb.r *= 1.0 + grade.z * 0.15;
                rgb.b *= 1.0 - grade.z * 0.15;
                rgb = max(rgb, 0.0);

                clip(baseColor.a - 0.002);
                return half4(rgb * baseColor.a, baseColor.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
