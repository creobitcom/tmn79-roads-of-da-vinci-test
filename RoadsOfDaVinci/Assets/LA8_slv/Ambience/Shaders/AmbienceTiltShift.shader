Shader "TMN/AmbienceTiltShift"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "TiltShift"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _TMNTiltShiftStrength;
            float _TMNTiltShiftCenter;
            float _TMNTiltShiftWidth;
            float _TMNTiltShiftFeather;

            half3 SampleSource(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0).rgb;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                half3 color = SampleSource(uv);

                float distance = abs(uv.y - _TMNTiltShiftCenter) - _TMNTiltShiftWidth;
                float falloff = smoothstep(0.0, max(_TMNTiltShiftFeather, 0.001), distance);
                float blur = falloff * falloff * _TMNTiltShiftStrength;

                if (blur < 0.003)
                {
                    return half4(color, 1);
                }

                float2 radius = _BlitTexture_TexelSize.xy * (blur * 5.5);

                half3 sum = color * 2.0;
                sum += SampleSource(uv + float2(1.0, 0.0) * radius);
                sum += SampleSource(uv + float2(-1.0, 0.0) * radius);
                sum += SampleSource(uv + float2(0.5, 0.866) * radius);
                sum += SampleSource(uv + float2(-0.5, 0.866) * radius);
                sum += SampleSource(uv + float2(0.5, -0.866) * radius);
                sum += SampleSource(uv + float2(-0.5, -0.866) * radius);

                float2 innerRadius = radius * 0.5;
                sum += SampleSource(uv + float2(0.866, 0.5) * innerRadius);
                sum += SampleSource(uv + float2(-0.866, 0.5) * innerRadius);
                sum += SampleSource(uv + float2(0.866, -0.5) * innerRadius);
                sum += SampleSource(uv + float2(-0.866, -0.5) * innerRadius);
                sum += SampleSource(uv + float2(0.0, 1.0) * innerRadius);
                sum += SampleSource(uv + float2(0.0, -1.0) * innerRadius);

                return half4(sum / 14.0, 1);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
