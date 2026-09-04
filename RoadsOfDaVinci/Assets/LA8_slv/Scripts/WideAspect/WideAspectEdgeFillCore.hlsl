#ifndef WIDE_ASPECT_EDGE_FILL_INCLUDED
#define WIDE_ASPECT_EDGE_FILL_INCLUDED

#define TAP_COUNT 32
#define GOLDEN_ANGLE 2.39996323

TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);

CBUFFER_START(UnityPerMaterial)
    float4 _MainTex_ST;
    float4 _SpriteRect;
    float4 _Region;
    float4 _MaxExtend;
    float _Falloff;
    float _Aspect;
    float _MirrorStart;
    float _EdgeInset;
    float _Cover;
    float _BlurFloor;
    float _Blur;
    float _Darken;
    float _Desaturate;
CBUFFER_END

float2 MirrorFold(float2 value)
{
    float2 folded = fmod(abs(value), 2.0);
    return 1.0 - abs(1.0 - folded);
}

half4 SampleEdgeFill(float2 sourceUv, float mirrorMix)
{
    float inset = _EdgeInset;
    float span = 1.0 - 2.0 * inset;
    float2 stretched = clamp(sourceUv, inset, 1.0 - inset);
    float2 mirrored = inset + MirrorFold(sourceUv) * span;
    float2 uv = _SpriteRect.xy + lerp(stretched, mirrored, mirrorMix) * _SpriteRect.zw;
    return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
}

half4 EdgeFillColor(float2 quadUv)
{
    float2 sourceUv = (quadUv - _Region.xy) / max(_Region.zw, 1e-5);
    float2 outside = max(0.0, max(-sourceUv, sourceUv - 1.0));
    float edgeDistance = max(outside.x, outside.y);
    float edge = saturate(edgeDistance / max(_Falloff, 1e-4));

    float2 inward = min(sourceUv, 1.0 - sourceUv);
    float coverage = 1.0 - smoothstep(0.0, max(_Cover, 1e-5), min(inward.x, inward.y));
    float2 reach = 1.0 - smoothstep(_MaxExtend.xy * 0.6, _MaxExtend.xy, outside);
    float alphaGate = coverage * min(reach.x, reach.y);
    if (alphaGate <= 0.0001)
    {
        return half4(0, 0, 0, 0);
    }

    float radius = _Blur * lerp(_BlurFloor, 1.0, edge);
    float mirrorMix = smoothstep(0.0, max(_MirrorStart, 1e-3), edge);
    float noise = frac(sin(dot(quadUv, float2(12.9898, 78.233))) * 43758.5453);
    float baseAngle = noise * 6.2831853;

    half4 color = 0.0;

    UNITY_UNROLL
    for (int i = 0; i < TAP_COUNT; i++)
    {
        float ring = (i + 0.5) / TAP_COUNT;
        float angle = baseAngle + i * GOLDEN_ANGLE;
        float offset = pow(ring, 0.65) * radius;
        float2 tap = float2(cos(angle) * offset * _Aspect, sin(angle) * offset);
        color += SampleEdgeFill(sourceUv + tap, mirrorMix);
    }

    color /= TAP_COUNT;

    half luminance = dot(color.rgb, half3(0.299, 0.587, 0.114));
    color.rgb = lerp(color.rgb, luminance.xxx, _Desaturate * edge);
    color.rgb *= lerp(1.0, 1.0 - _Darken, edge);

    color.a *= alphaGate;
    return color;
}

#endif
