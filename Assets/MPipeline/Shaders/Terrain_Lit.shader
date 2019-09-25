
 Shader "Maxwell/Terrain_Lit" {
	SubShader {
		Tags { "RenderType"="Opaque" }
		LOD 200

	// ------------------------------------------------------------
	// Surface shader code generated out of a CGPROGRAM block:
CGINCLUDE
#pragma target 5.0
#define DECAL
#define LIT_ENABLE
#define DEFAULT_LIT

#include "UnityCG.cginc"
#include "UnityDeferredLibrary.cginc"
#include "UnityPBSLighting.cginc"
#include "CGINC/VoxelLight.cginc"
#include "CGINC/Shader_Include/Common.hlsl"
#include "CGINC/Shader_Include/BSDF_Library.hlsl"
#include "CGINC/Shader_Include/AreaLight.hlsl"
#include "CGINC/Sunlight.cginc"
#include "CGINC/Lighting.cginc"
#include "CGINC/StandardSurface_Terrain.cginc"
#include "CGINC/TerrainDeferred.cginc"
ENDCG

pass
{
	Stencil
	{
		Ref 1
		WriteMask 15
		Pass replace
		comp always
	}
Name "GBuffer"
Tags {"LightMode" = "GBuffer" "Name" = "GBuffer"}
ZTest Less
ZWrite on
Cull back
CGPROGRAM
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
			#pragma vertex vert_shadow
			#pragma fragment frag_shadow
			ENDCG
		}


}
}

