Shader "Custom/MaxBrightLit"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _BaseMap ("Base Map", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        
        // Enable blending
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma prefer_hlslcc gles
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
            };

            float4 _BaseColor;
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS);
                OUT.normalWS = normalize(TransformObjectToWorldNormal(IN.normalOS));
                OUT.uv = IN.uv;
                OUT.worldPos = TransformObjectToWorld(IN.positionOS).xyz;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Sample the base texture and get its alpha
                float4 baseTextureColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);

                // Check if the texture is fully transparent
                if (baseTextureColor.a <= 0.0)
                {
                    discard; // Discard the fragment if it is fully transparent
                }

                // Calculate base color
                half4 baseColor = _BaseColor * baseTextureColor;

                // Get the normal and lighting
                half3 normalWS = normalize(IN.normalWS);
                Light mainLight = GetMainLight();

                // Variable to hold the brightest light
                half3 maxLight = half3(0, 0, 0);

                // Calculate main light
                half3 mainLightDir = normalize(mainLight.direction);
                half NdotL = max(0, dot(normalWS, mainLightDir));
                maxLight = mainLight.color * NdotL;

                // Check additional lights and find the brightest
                uint pixelLightCount = GetAdditionalLightsCount();
                for (uint i = 0; i < pixelLightCount; ++i)
                {
                    Light light = GetAdditionalLight(i, IN.worldPos);
                    half3 additionalLightDir = light.direction;
                    half3 additionalLightColor = light.color;
                    half atten = light.distanceAttenuation;

                    // If this is a point or spot light
                    if (atten > 0)
                    {
                        NdotL = max(0, dot(normalWS, additionalLightDir));
                        half3 additionalLight = additionalLightColor * NdotL * atten;

                        // Compare current light to the previous max
                        if (length(additionalLight) > length(maxLight))
                        {
                            maxLight = additionalLight;
                        }
                    }
                }

                // Final lighting result
                half3 finalColor = baseColor.rgb * maxLight;
                return half4(finalColor, baseColor.a);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
