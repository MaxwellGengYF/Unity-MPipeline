Shader "Maxwell/Transparent"
{
	Properties
	{
		_Color("Color", Color) = (1,1,1,1)
		_MainTex("Albedo(RGB)Alpha(A)", 2D) = "white"{}
		_SpecularTex("Specular(RGB)Smoothness(A)", 2D) = "white"{}
		_SpecularColor("Specular Color", Color) = (0.3, 0.3, 0.3, 0.3)
	}
		SubShader
		{
			Tags{ "LightMode" = "Transparent" "Queue" = "Transparent"}
						ZTest less
						ZWrite off
						Cull back
						Blend srcAlpha OneMinusSrcAlpha
			Pass
			{
				CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag
	#pragma target 5.0
	#define DEFAULT_LIT
	#include "UnityCG.cginc"
	#include "UnityPBSLighting.cginc"
	#include "CGINC/VoxelLight.cginc"
	#include "CGINC/Shader_Include/Common.hlsl"
	#include "CGINC/Random.cginc"
	#include "CGINC/Shader_Include/BSDF_Library.hlsl"
	#include "CGINC/Shader_Include/AreaLight.hlsl"
	#include "CGINC/Lighting.cginc"
	#include "CGINC/Reflection.cginc"
	#include "CGINC/VolumetricLight.cginc"
	#include "CGINC/Sunlight.cginc"
	#pragma multi_compile __ ENABLE_SUN
	#pragma multi_compile __ ENABLE_SUNSHADOW
	#pragma multi_compile __ POINTLIGHT
	#pragma multi_compile __ SPOTLIGHT
	#pragma multi_compile __ ENABLE_VOLUMETRIC
	#pragma multi_compile __ ENABLE_REFLECTION

	float4 _Color;
	sampler2D _MainTex; float4 _MainTex_ST;
	sampler2D _SpecularTex;
	float4 _SpecularColor;

	struct v2f {
	  UNITY_POSITION(pos);
	  float2 texcoord : TEXCOORD0;
	  float3 worldNormal : TEXCOORD1;
	float3 worldPos : TEXCOORD3;
	  float4 screenUV : TEXCOORD4;
	};
	struct appdata
	{
		float4 vertex : POSITION;
		float3 normal : NORMAL;
		float2 texcoord : TEXCOORD0;
	};

	float4x4 _NonJitterTextureVP;
	v2f vert(appdata v)
	{
		v2f o;
		o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
		float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
		o.pos = mul(UNITY_MATRIX_VP, worldPos);
		  o.screenUV = ComputeScreenPos(o.pos);
		  worldPos /= worldPos.w;
		  o.worldPos = worldPos.xyz;
		o.worldNormal = mul((float3x3)unity_ObjectToWorld, v.normal).xyz;
		return o;
	}
				void frag(v2f i, out float4 outputColor:SV_TARGET0, out float depth : SV_TARGET1)
				{
					
					float4 color = tex2D(_MainTex, i.texcoord) * _Color;
					float4 specular = tex2D(_SpecularTex, i.texcoord) * _SpecularColor;
					float3 viewDir = normalize(i.worldPos - _WorldSpaceCameraPos);
					float3 normal = normalize(i.worldNormal);
					float2 screenUV = i.screenUV.xy / i.screenUV.w;
					float linearEyeDepth = LinearEyeDepth(i.pos.z);
					float linear01Depth = Linear01Depth(i.pos.z);
					float Roughness = clamp(1 - specular.a, 0.02, 1);
					UnityStandardData standardData;
					standardData.occlusion = 1;
					standardData.diffuseColor = color.rgb;
					standardData.specularColor = specular.rgb;
					standardData.smoothness = specular.a;
					float oneMinusReflectivity;
					standardData.diffuseColor = EnergyConservationBetweenDiffuseAndSpecular(standardData.diffuseColor, standardData.specularColor, /*out*/ oneMinusReflectivity);
					float3 finalColor = 0;
					GeometryBuffer buffer;
					buffer.AlbedoColor = standardData.diffuseColor;
					buffer.SpecularColor = specular;
					buffer.Roughness = Roughness;
					#if ENABLE_SUN
					#if ENABLE_SUNSHADOW
					finalColor += max(0, CalculateSunLight(normal, i.pos.z, float4(i.worldPos, 1), -viewDir, buffer));
					#else
					finalColor += max(0, CalculateSunLight_NoShadow(normal, -viewDir, buffer));
					#endif
					#endif
					#if ENABLE_REFLECTION
					finalColor += CalculateGI(linearEyeDepth, i.worldPos, normal, color.rgb, 1, screenUV);
					finalColor += CalculateReflection(linearEyeDepth, i.worldPos, viewDir, specular, float4(normal, 1), color.rgb, 1, screenUV);
					#endif

					finalColor += max(0, CalculateLocalLight(screenUV, float4(i.worldPos, 1), linearEyeDepth, normal, -viewDir, buffer));
					#if ENABLE_VOLUMETRIC
					float4 fogColor = Fog(linear01Depth, screenUV);
					finalColor = lerp(fogColor.rgb, finalColor, fogColor.a);
					#endif
					color.a = 1-oneMinusReflectivity + color.a*oneMinusReflectivity;
					outputColor = float4(finalColor, color.a);
					depth = i.pos.z;
				}
				ENDCG
			}
		}
}
