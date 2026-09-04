Shader "Custom/AntiFog"
{
	Properties
	{
		_MainTex ("Light Mask (RT)", 2D) = "black" {}
		_CloudTex ("Cloud Texture (tiling)", 2D) = "gray" {}
		_FogColor ("Fog Shadow (A = density)", Color) = (0.58, 0.62, 0.72, 1)
		_FogColorTop ("Fog Lit", Color) = (0.98, 0.98, 1, 1)
		_EdgeStart ("Edge Start", Range(0, 1)) = 0.34
		_EdgeEnd ("Edge End", Range(0, 1)) = 0.52
		_EdgeGlowColor ("Edge Glow (A = power)", Color) = (1, 1, 1, 0)
		_EdgeGlowWidth ("Edge Glow Width", Range(0.01, 0.5)) = 0.15
		_NoiseScale ("Cloud Scale", Range(0.5, 24)) = 2.5
		_NoiseSpeed ("Cloud Speed", Range(0, 2)) = 1
		_Swirl ("Swirl", Range(0, 3)) = 1
		_SwirlScale ("Swirl Scale", Range(0.1, 3)) = 0.6
		_EdgeBreak ("Edge Break", Range(0, 1)) = 0.65
		_Relief ("Relief", Range(0, 1)) = 0.8
		_DropShadow ("Drop Shadow", Range(0, 1)) = 0.55
		_NoiseAspect ("Noise Aspect", Float) = 1.78
		_Breathing ("Breathing", Range(0, 1)) = 0.15
	}
	SubShader
	{
		Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
		Lighting Off
		ZWrite Off
		Cull Off
		Blend SrcAlpha OneMinusSrcAlpha

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			sampler2D _MainTex;
			sampler2D _CloudTex;
			fixed4 _FogColor;
			fixed4 _FogColorTop;
			fixed4 _EdgeGlowColor;
			float _EdgeStart;
			float _EdgeEnd;
			float _EdgeGlowWidth;
			float _NoiseScale;
			float _NoiseSpeed;
			float _Swirl;
			float _SwirlScale;
			float _EdgeBreak;
			float _Relief;
			float _DropShadow;
			float _NoiseAspect;
			float _Breathing;

			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				return o;
			}

			float CloudHeight(float2 uv)
			{
				float a = tex2D(_CloudTex, uv).r;
				float b = tex2D(_CloudTex, uv * 2.13 + float2(0.37, 0.71)).r;
				return a * 0.68 + b * 0.32;
			}

			fixed4 frag (v2f i) : SV_Target
			{
				float lightMask = tex2D(_MainTex, i.uv).r;
				float maskUp = tex2D(_MainTex, i.uv + float2(0.016, 0.045)).r;
				float fogRaw = lightMask;
				float fogRawUp = maskUp;

				if (fogRaw <= 0.002 && fogRawUp <= 0.002)
				{
					return fixed4(0, 0, 0, 0);
				}

				float t = _Time.y * _NoiseSpeed;
				float2 p = float2(i.uv.x * _NoiseAspect, i.uv.y) * (_NoiseScale * 0.25);

				float wA = tex2D(_CloudTex, p * _SwirlScale + float2(t * 0.020, t * 0.009)).r;
				float wB = tex2D(_CloudTex, p * _SwirlScale + float2(0.5, 0.5) - float2(t * 0.013, t * 0.017)).r;
				float2 q = p + (float2(wA, wB) - 0.5) * (_Swirl * 0.22) + float2(-t * 0.016, t * 0.006);

				float h = CloudHeight(q);
				float hLit = CloudHeight(q + float2(0.0045, 0.0125));
				float fine = tex2D(_CloudTex, q * 3.7 + float2(0.13, 0.57)).r;

				float puff = smoothstep(0.25, 0.75, h);
				float core = smoothstep(0.75, 0.98, fogRaw);

				float disp = ((h - 0.5) * 0.5 + (fine - 0.5) * 0.6) * _EdgeBreak * (1.0 - core);
				float density = saturate(fogRaw + disp);
				float shaped = max(smoothstep(_EdgeStart, max(_EdgeEnd, _EdgeStart + 0.001), density), core);

				float ao = smoothstep(0.18, 0.85, h);
				float slope = (hLit - h) * 7.6;
				float lightVal = saturate(0.52 + slope + (ao - 0.5) * 0.55);
				float lit = lerp(0.85, lightVal, _Relief);
				float breath = lerp(1.0, 0.93 + 0.14 * wA, _Breathing);

				float glow = saturate(1.0 - abs(shaped - 0.5) / _EdgeGlowWidth) * _EdgeGlowColor.a;

				float densityUp = saturate(fogRawUp + disp);
				float coreUp = smoothstep(0.75, 0.98, fogRawUp);
				float shapedUp = max(smoothstep(_EdgeStart, max(_EdgeEnd, _EdgeStart + 0.001), densityUp), coreUp);
				float dropShadow = saturate(shapedUp - shaped) * _DropShadow;

				float fogA = saturate(shaped * _FogColor.a + glow * 0.5);
				float shadowA = dropShadow * 0.6 * _FogColor.a;
				float outA = max(fogA, shadowA);

				fixed3 fogRgb = lerp(_FogColor.rgb, _FogColorTop.rgb, lit) * breath + _EdgeGlowColor.rgb * glow;
				fixed3 rgb = lerp(fixed3(0.10, 0.09, 0.14), fogRgb, saturate(fogA / max(outA, 0.001)));

				return fixed4(rgb, outA);
			}
			ENDCG
		}
	}
}
