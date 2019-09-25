
 Shader "Maxwell/ParticleUnlit" {
	Properties {
		_EmissionMultiplier("Emission Multiplier", Range(0, 128)) = 1
		_EmissionColor("Emission Color", Color) = (0,0,0,1)
		_BumpMap("Bump Map", 2D) = "bump"{}
	}
	SubShader {
		Tags { "RenderType"="Opaque" }
		LOD 200

	// ------------------------------------------------------------
	// Surface shader code generated out of a CGPROGRAM block:
CGINCLUDE
#pragma shader_feature DETAIL_ON
#pragma target 5.0

//#define MOTION_VECTOR
#include "UnityCG.cginc"
#include "UnityPBSLighting.cginc"
cbuffer UnityPerMaterial
{
    float4 _EmissionColor;
	float _EmissionMultiplier;
}
		sampler2D _BumpMap;
#include "../CGINC/ParticleDeferred.cginc"
ENDCG
//GBuffer Pass
pass
{
	Stencil
	{
		Ref 0
		WriteMask 15
		Pass replace
		comp always
	}
ZTest Equal
ZWrite off
Cull back
CGPROGRAM
#pragma vertex vert_surf
#pragma fragment frag_surf
ENDCG
}
//Motion Vector Pass
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
			#pragma vertex vert_mv
			#pragma fragment frag_mv
			// Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it uses non-square matrices
			#pragma exclude_renderers gles

			ENDCG
		}
//Depth prepass
		Pass{
			ZTest Less
			ZWrite on
			Cull back
			CGPROGRAM
			#pragma vertex vert_depth
			#pragma fragment frag_depth
			ENDCG
		}
	}
}

