Shader "Maxwell/TransparentNormal"
{
	Properties
	{
		_Color("Color", Color) = (1,1,1,1)
		_MainTex("Albedo(RGB)Alpha(A)", 2D) = "white"{}
		_SpecularTex("Specular(RGB)Smoothness(A)", 2D) = "white"{}
		_SpecularColor("Specular Color", Color) = (0.3, 0.3, 0.3, 0.3)
		_NormalMap("Normal Map", 2D) = "bump"{}
		_Offset("Offset", Range(0, 10)) = 1
	}
		SubShader
		{
			Tags{ "LightMode" = "Transparent" "Queue" = "Transparent"}
						ZTest less
						ZWrite off
						Cull back
			Pass
			{
				CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag
	#pragma target 5.0
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
	sampler2D _NormalMap;
	sampler2D _GrabTexture; float4 _GrabTexture_TexelSize;
	float4 _SpecularColor;
	float _Offset;

	struct v2f {
	  UNITY_POSITION(pos);
	float2 texcoord : TEXCOORD0;
	float4 worldNormal : TEXCOORD1;
	float4 worldTangent : TEXCOORD2;
  	float4 worldBinormal : TEXCOORD3;
	float4 screenUV : TEXCOORD4;
	};
	struct appdata
	{
		float4 vertex : POSITION;
	float4 tangent : TANGENT;
	float3 normal : NORMAL;
	float2 texcoord : TEXCOORD0;
	};

	float4x4 _NonJitterTextureVP;
	v2f vert(appdata v)
	{
		v2f o;
		o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
	 	o.pos = UnityObjectToClipPos(v.vertex);
		 o.screenUV = ComputeScreenPos(o.pos);
		float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
		v.tangent.xyz = mul((float3x3)unity_ObjectToWorld, v.tangent.xyz);
  		o.worldTangent = float4( v.tangent.xyz, worldPos.x);
	  	v.normal = mul((float3x3)unity_ObjectToWorld, v.normal);
		o.worldNormal =float4(v.normal, worldPos.z);
  		o.worldBinormal = float4(cross(v.normal, o.worldTangent.xyz) * v.tangent.w, worldPos.y);
		return o;
	}
				void frag(v2f i, out float3 finalColor:SV_TARGET0, out float depth : SV_TARGET1)
				{
					
					float4 color = tex2D(_MainTex, i.texcoord) * _Color;
					float4 specular = tex2D(_SpecularTex, i.texcoord) * _SpecularColor;
					float3 worldPos = float3(i.worldTangent.w, i.worldBinormal.w, i.worldNormal.w);
					float3x3 wdMatrix= float3x3(normalize(i.worldTangent.xyz), normalize(i.worldBinormal.xyz), normalize(i.worldNormal.xyz));
					float3 viewDir = normalize(worldPos - _WorldSpaceCameraPos);
					float3 normal = UnpackNormal(tex2D(_NormalMap, i.texcoord));
					float2 screenUV = i.screenUV.xy / i.screenUV.w;
					float2 offsetScreenUV = screenUV + normal.xy * _GrabTexture_TexelSize.xy * _Offset;
					normal = normalize(mul(normal, wdMatrix));
					float linearEyeDepth = LinearEyeDepth(i.pos.z);
					float linear01Depth = Linear01Depth(i.pos.z);
					float Roughness = clamp(1 - specular.a, 0.02, 1);
					UnityStandardData standardData;
					standardData.occlusion = 1;
					standardData.diffuseColor = color.rgb;
					standardData.specularColor = specular.rgb;
					standardData.smoothness = specular.a;
					standardData.normalWorld = normal;
					float oneMinusReflectivity;
					standardData.diffuseColor = EnergyConservationBetweenDiffuseAndSpecular(standardData.diffuseColor, standardData.specularColor, /*out*/ oneMinusReflectivity);
					finalColor = 0;
					#if ENABLE_SUN
					#if ENABLE_SUNSHADOW
					finalColor += max(0, CalculateSunLight(standardData, i.pos.z, float4(worldPos, 1), viewDir));
					#else
					finalColor += max(0, CalculateSunLight_NoShadow(standardData, viewDir));
					#endif
					#endif
					#if ENABLE_REFLECTION
					finalColor += CalculateReflection(linearEyeDepth, worldPos, viewDir, specular, float4(normal, 1), color.rgb, 1, screenUV);
					#endif
					finalColor += max(0, CalculateLocalLight(screenUV, float4(worldPos, 1), linearEyeDepth, standardData.diffuseColor, normal, specular, Roughness, -viewDir));
					#if ENABLE_VOLUMETRIC
					float4 fogColor = Fog(linear01Depth, screenUV);
					finalColor = lerp(fogColor.rgb, finalColor, fogColor.a);
					#endif
					finalColor = lerp(tex2D(_GrabTexture, offsetScreenUV), finalColor, color.a);
					depth = i.pos.z;
				}
				ENDCG
			}
		}
}
