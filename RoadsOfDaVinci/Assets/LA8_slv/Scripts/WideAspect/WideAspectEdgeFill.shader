Shader "LA8/WideAspectEdgeFill"
{
    Properties
    {
        _MainTex ("Source Texture", 2D) = "white" {}
        _SpriteRect ("Sprite Rect", Vector) = (0, 0, 1, 1)
        _Region ("Source Region", Vector) = (0, 0, 1, 1)
        _MaxExtend ("Max Extend", Vector) = (1, 1, 0, 0)
        _Falloff ("Falloff", Float) = 0.2
        _Aspect ("Aspect", Float) = 0.5625
        _MirrorStart ("Mirror Start", Range(0.05, 1)) = 0.3
        _EdgeInset ("Edge Inset", Range(0, 0.05)) = 0.004
        _Cover ("Edge Cover", Range(0, 0.05)) = 0.006
        _BlurFloor ("Blur Floor", Range(0, 0.5)) = 0.04
        _Blur ("Blur", Range(0, 0.5)) = 0.14
        _Darken ("Darken", Range(0, 1)) = 0.55
        _Desaturate ("Desaturate", Range(0, 1)) = 0.35
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
            Name "WideAspectEdgeFill"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "WideAspectEdgeFillCore.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 color = EdgeFillColor(input.uv);
                return half4(color.rgb * color.a, color.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Capture"

            Blend Off
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma vertex vertCapture
            #pragma fragment fragCapture
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "WideAspectEdgeFillCore.hlsl"

            struct CaptureAttributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct CaptureVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CaptureVaryings vertCapture(CaptureAttributes input)
            {
                CaptureVaryings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 fragCapture(CaptureVaryings input) : SV_Target
            {
                return EdgeFillColor(input.uv);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
