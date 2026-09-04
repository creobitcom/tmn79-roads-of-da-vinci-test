Shader "TMN/AmbienceParticle"
{
    Properties
    {
        _MainTex ("Particle Texture (optional)", 2D) = "white" {}
        _UseTexture ("Use Texture", Range(0, 1)) = 0
        _Feather ("Feather", Range(0.01, 1)) = 0.55
        _Hardness ("Hardness", Range(0, 1)) = 0.2
        _Brightness ("Brightness", Range(0, 4)) = 1
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
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half _UseTexture;
                half _Feather;
                half _Hardness;
                half _Brightness;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half mask;
                if (_UseTexture > 0.5)
                {
                    mask = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).a;
                }
                else
                {
                    float2 centered = IN.uv * 2.0 - 1.0;
                    float distance = length(centered);
                    mask = 1.0 - smoothstep(_Hardness, _Hardness + _Feather, distance);
                }

                half alpha = saturate(mask * IN.color.a);
                if (alpha < 0.002)
                {
                    return half4(0, 0, 0, 0);
                }

                half3 rgb = IN.color.rgb * _Brightness;
                return half4(rgb * alpha, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
