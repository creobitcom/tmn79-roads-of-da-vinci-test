Shader "TMN/ScanSweep"
{
    Properties
    {
        _MainTex ("Sprite", 2D) = "white" {}
        _BandColor ("Color", Color) = (0.42, 0.92, 1, 0.85)
        _Progress ("Progress", Float) = 0
        _SweepAxis ("Sweep Axis", Vector) = (1, 0, 0, 1)
        _Intensity ("Intensity", Range(0, 2)) = 1.0
        _BandWidth ("Band Width", Range(0.01, 1)) = 0.25
        _Softness ("Softness", Range(0, 1)) = 0.8
        _FlashColor ("Flash Color", Color) = (1, 1, 1, 1)
        _FlashAmount ("Flash Amount", Float) = 0
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

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Lighting Off

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 positionWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _BandColor;
                float _Progress;
                float4 _SweepAxis;
                half _Intensity;
                half _BandWidth;
                half _Softness;
                half4 _FlashColor;
                half _FlashAmount;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(positionWS);
                OUT.positionWS = positionWS.xy;
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 source = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                half spriteAlpha = source.a * IN.color.a;

                if (spriteAlpha < 0.002)
                {
                    return half4(0, 0, 0, 0);
                }

                float outer = max(_BandWidth * 0.5, 0.0001);
                float inner = outer * (1.0 - _Softness);

                float2 direction = _SweepAxis.xy;
                float axisLength = max(_SweepAxis.w, 0.0001);
                float position = (dot(IN.positionWS, direction) - _SweepAxis.z) / axisLength;
                float bandDistance = abs(position - _Progress);

                float band = 1.0 - smoothstep(inner, outer, bandDistance);

                half bandAlpha = saturate(spriteAlpha * _BandColor.a * band * min(1.0h, _Intensity));
                half3 bandRgb = _BandColor.rgb * max(1.0h, _Intensity);

                half flashAlpha = saturate(spriteAlpha * _FlashColor.a * min(1.0h, _FlashAmount));
                half3 flashRgb = _FlashColor.rgb * max(1.0h, _FlashAmount);

                half finalAlpha = saturate(bandAlpha + flashAlpha);
                half3 finalRgb = finalAlpha > 0.001h ? lerp(bandRgb, flashRgb, flashAlpha / finalAlpha) : half3(0, 0, 0);

                return half4(finalRgb, finalAlpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
