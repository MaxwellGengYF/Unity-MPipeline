
 Shader "Maxwell/StandardLit" {
	Properties {
		_Color ("Color", Color) = (1,1,1,1)
		_MultiScatter("Multi Scatter", Color) = (1,1,1,1)
		_ClearCoatRoughness("Clearcoat Roughness", Range(0, 1)) = 0.5
		_ClearCoat("Clearcoat", Range(0, 1)) = 0.5
		_ClearCoatEnergy("Clearcoat Energy", Color) = (1,1,1,1)
		_Glossiness ("Smoothness", Range(0,1)) = 0.5
		_Occlusion("Occlusion Scale", Range(0,1)) = 1
		_Cutoff("Cut off", Range(0, 1)) = 0
		_SpecularIntensity("Specular Intensity", Range(0,1)) = 0.3
		_MetallicIntensity("Metallic Intensity", Range(0, 1)) = 0.1
		_MainTex ("Albedo (RGB)AO(A)", 2D) = "white" {}
		_BumpMap("Normal Map", 2D) = "bump" {}
		_SpecularMap("R(Spec)G(Smooth)B(DetailMask)", 2D) = "white"{}
		_DetailAlbedo("Detail Albedo", 2D) = "white"{}
		_DetailNormal("Detail Normal", 2D) = "bump"{}
		_EmissionMultiplier("Emission Multiplier", Range(0, 128)) = 1
		_EmissionColor("Emission Color", Color) = (0,0,0,1)
		_EmissionMap("Emission Map", 2D) = "white"{}
		[HideInInspector]_ZTest("zw", Int) = 0
		[HideInInspector]_ZWrite("zww", Int) = 0
		[HideInInspector]_LightingModel("lm", Int) = 1
	}
	SubShader {
		Tags { "RenderType"="Opaque" }
		LOD 200

	// ------------------------------------------------------------
	// Surface shader code generated out of a CGPROGRAM block:
CGINCLUDE
#pragma shader_feature DETAIL_ON
#pragma multi_compile __ LIGHTMAP_ON
#pragma target 5.0
#define DECAL

#pragma multi_compile __ ENABLE_RAINNING
#pragma multi_compile __ USE_RANNING
#pragma multi_compile __ CUT_OFF
#pragma multi_compile __ LIT_ENABLE
#pragma multi_compile __ DEFAULT_LIT SKIN_LIT CLOTH_LIT CLEARCOAT_LIT

//#define MOTION_VECTOR
#include "UnityCG.cginc"
#include "UnityDeferredLibrary.cginc"
#include "UnityPBSLighting.cginc"
#include "CGINC/VoxelLight.cginc"
#include "CGINC/Shader_Include/Common.hlsl"
#include "CGINC/Shader_Include/BSDF_Library.hlsl"
#include "CGINC/Shader_Include/AreaLight.hlsl"
#include "CGINC/Sunlight.cginc"
#include "CGINC/Lighting.cginc"
#include "CGINC/MPipeDeferred.cginc"
ENDCG

pass
{
	Stencil
	{
		Ref [_LightingModel]
		WriteMask 127
		Pass replace
		comp always
	}
Name "GBuffer"
Tags {"LightMode" = "GBuffer" "Name" = "GBuffer"}
ZTest [_ZTest]
ZWrite [_ZWrite]
Cull back
CGPROGRAM
	#pragma multi_compile _ ENABLE_SUN
	#pragma multi_compile _ ENABLE_SUNSHADOW
	#pragma multi_compile _ POINTLIGHT
	#pragma multi_compile _ SPOTLIGHT
#pragma vertex vert_surf
#pragma fragment frag_surf
ENDCG
}
	Pass
		{
			ZTest less
			Cull back
			Tags {"LightMode" = "Shadow"}
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			// Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it uses non-square matrices
			#pragma exclude_renderers gles
			#include "UnityCG.cginc"
			#pragma multi_compile __ POINT_LIGHT_SHADOW
			
			float4x4 _ShadowMapVP;
			struct appdata_shadow
			{
				float4 vertex : POSITION;
				#if CUT_OFF
				float2 texcoord : TEXCOORD0;
				#endif
			};
			struct v2f
			{
				float4 vertex : SV_POSITION;
				#if POINT_LIGHT_SHADOW
				float3 worldPos : TEXCOORD1;
				#endif
				#if CUT_OFF
				float2 texcoord : TEXCOORD0;
				#endif
			};

			v2f vert (appdata_shadow v)
			{
				float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
				v2f o;
				#if POINT_LIGHT_SHADOW
				o.worldPos = worldPos.xyz;
				#endif
				o.vertex = mul(_ShadowMapVP, worldPos);
				#if CUT_OFF
				o.texcoord = v.texcoord;
				#endif
				return o;
			}

			
			float frag (v2f i)  : SV_TARGET
			{
				#if CUT_OFF
				i.texcoord = TRANSFORM_TEX(i.texcoord, _MainTex);
				float4 c = tex2D(_MainTex, i.texcoord);
				clip(c.a * _Color.a - _Cutoff);
				#endif
				#if POINT_LIGHT_SHADOW
				return distance(i.worldPos, _LightPos.xyz) / _LightPos.w;
				#else
				return i.vertex.z;
				#endif
			}

			ENDCG
		}
				Pass
		{
			Stencil
			{
				Ref 128
				WriteMask 128
				Comp always
				Pass replace
			}
			ZTest Equal
			Cull back
			ZWrite off
			Tags {"LightMode" = "MotionVector"}
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			// Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it uses non-square matrices
			#pragma exclude_renderers gles
			#include "UnityCG.cginc"
			
			struct appdata_shadow
			{
				float4 vertex : POSITION;
#if CUT_OFF
				float2 texcoord : TEXCOORD0;
#endif
			};
			struct v2f
			{
				float4 vertex : SV_POSITION;
#if CUT_OFF
				float2 texcoord : TEXCOORD0;
#endif
				float3 nonJitterScreenPos : TEXCOORD1;
				float3 lastScreenPos : TEXCOORD2;
			};

			v2f vert (appdata_shadow v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
			  float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
				o.nonJitterScreenPos = ComputeScreenPos(mul(_NonJitterVP, worldPos)).xyw;
				float4 lastWorldPos =  mul(_LastFrameModel, v.vertex);
        o.lastScreenPos = ComputeScreenPos(mul(_LastVp, lastWorldPos)).xyw;
#if CUT_OFF
				o.texcoord = v.texcoord;
#endif
				return o;
			}

			
			float2 frag (v2f i)  : SV_TARGET
			{
#if CUT_OFF
				i.texcoord = TRANSFORM_TEX(i.texcoord, _MainTex);
				float4 c = tex2D(_MainTex, i.texcoord);
				clip(c.a * _Color.a - _Cutoff);
#endif
				float4 velocity = float4(i.nonJitterScreenPos.xy, i.lastScreenPos.xy) / float4(i.nonJitterScreenPos.zz, i.lastScreenPos.zz);
#if UNITY_UV_STARTS_AT_TOP
				return velocity.xw - velocity.zy;
#else
				return velocity.xy - velocity.zw;
#endif

			}

			ENDCG
		}
		Pass
		{
			ZTest less
			Cull back
			Tags {"LightMode" = "Depth"}
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			// Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it uses non-square matrices
			#pragma exclude_renderers gles
			#include "UnityCG.cginc"
			
			struct appdata_depthPrePass
			{
				float4 vertex : POSITION;
				#if CUT_OFF
				float2 texcoord : TEXCOORD0;
				#endif
			};
			struct v2f
			{
				float4 vertex : SV_POSITION;
				#if CUT_OFF
				float2 texcoord : TEXCOORD0;
				#endif
			};

			v2f vert (appdata_depthPrePass v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				#if CUT_OFF
				o.texcoord = v.texcoord;
				#endif
				return o;
			}
			#if CUT_OFF
			void frag (v2f i)
			#else
			void frag ()
			#endif
			{
				#if CUT_OFF
				i.texcoord = TRANSFORM_TEX(i.texcoord, _MainTex);
				float4 c = tex2D(_MainTex, i.texcoord);
				clip(c.a * _Color.a - _Cutoff);
				#endif
			}

			ENDCG
		}
}
	CustomEditor "ShouShouEditor"
}

