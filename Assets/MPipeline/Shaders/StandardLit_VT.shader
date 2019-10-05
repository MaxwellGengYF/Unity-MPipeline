 Shader "Maxwell/StandardLit_VT" {
	Properties {
		_ClearCoat("Clearcoat", Range(0, 1)) = 0.5
		_ClearCoatSmoothness("Secondary Smoothness", Range(0, 1)) = 0.5
		_Cutoff("Cut off", Range(0, 1)) = 0
		_NormalIntensity("Normal Intensity", Vector) = (1,1,0,0)
		_MinDist("Min Tessellation Dist", float) = 20
		_MaxDist("Max Tessellation Dist", float) = 50
		_Tessellation("Tessellation Intensity", Range(1, 63)) = 1
		_HeightmapIntensity("Heightmap Intensity", Range(0, 10)) = 0
		_TileOffset("Texture ScaleOffset", Vector) = (1,1,0,0)
		[NoScaleOffset]_MainTex ("Albedo (RGB)Mask(A)", any) = "white" {}
		[NoScaleOffset]_BumpMap("Normal Map", any) = "bump" {}
		[NoScaleOffset]_SpecularMap("R(Smooth)G(Spec)B(Occ)", any) = "white"{}
		[NoScaleOffset]_HeightMap("Height Map", any) = "black"{}
		_EmissionMultiplier("Emission Multiplier", Range(0, 128)) = 1
		_EmissionColor("Emission Color", Color) = (0,0,0,1)
		[NoScaleOffset]_EmissionMap("Emission Map", any) = "white"{}
		[HideInInspector]_LightingModel("lm", Int) = 1
		[HideInInspector]_DecalLayer("dl", Int) = 0
	}
	SubShader {
		Tags { "RenderType"="Opaque" }
		LOD 200

	// ------------------------------------------------------------
	// Surface shader code generated out of a CGPROGRAM block:
CGINCLUDE


#pragma target 5.0
#define DECAL
#define TESSELLATION_SHADER
//#define USE_UV4
#pragma shader_feature CUT_OFF

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
#include "CGINC/StandardSurface_VT.cginc"
#include "CGINC/MPipeDeferred.cginc"
ENDCG

pass
{
	Stencil
	{
		Ref [_LightingModel]
		WriteMask 15
		Pass replace
		comp always
	}
Name "GBuffer"
Tags {"LightMode" = "GBuffer" "Name" = "GBuffer"}
ZTest Equal
ZWrite off
Cull off
CGPROGRAM
#pragma shader_feature LIT_ENABLE
#pragma shader_feature DEFAULT_LIT 
#pragma shader_feature SKIN_LIT
#pragma shader_feature  CLOTH_LIT
#pragma shader_feature  CLEARCOAT_LIT
#pragma shader_feature USE_MOTIONVECTOR
#pragma multi_compile __ LIGHTMAP_ON
#pragma shader_feature USE_SECONDARY_MAP
#pragma vertex tessvert_surf
#pragma hull hs_surf
#pragma domain ds_surf
#pragma fragment frag_surf
ENDCG
}


	Pass
		{
			ZTest less
			Cull off
			Tags {"LightMode" = "Shadow"}
			CGPROGRAM
			#pragma vertex tessvert_mv
			#pragma hull hs_mv
			#pragma domain ds_shadowmap
			#pragma fragment frag_shadow
			// Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it uses non-square matrices
			#pragma exclude_renderers gles
			#pragma multi_compile __ POINT_LIGHT_SHADOW

			ENDCG
		}
		Pass
		{
			ZTest less
			Cull off
			Tags {"LightMode" = "Depth"}
			CGPROGRAM
			#pragma vertex tessvert_mv
			#pragma hull hs_mv
			#pragma domain ds_depth
			#pragma fragment frag_depth
			// Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it uses non-square matrices
			#pragma exclude_renderers gles
			ENDCG
		}

		Pass
		{
					Name "Meta"
		Tags { "LightMode" = "Meta" }
		Cull Off

CGPROGRAM
// compile directives
#pragma vertex vert_meta
#pragma fragment frag_meta
#pragma target 5.0
#pragma multi_compile_instancing
#pragma skip_variants FOG_LINEAR FOG_EXP FOG_EXP2
#pragma shader_feature EDITOR_VISUALIZATION
#pragma shader_feature LIT_ENABLE
#pragma shader_feature DEFAULT_LIT 
#pragma shader_feature SKIN_LIT
#pragma shader_feature  CLOTH_LIT
#pragma shader_feature  CLEARCOAT_LIT
#include "CGINC/MPipeMetaPass.cginc"
ENDCG
		}
}
}

