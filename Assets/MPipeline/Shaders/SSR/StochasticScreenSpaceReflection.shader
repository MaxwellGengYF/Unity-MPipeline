Shader "Hidden/StochasticScreenSpaceReflection" {

	CGINCLUDE
		#include "SSRPass.cginc"
	ENDCG

	SubShader {
		ZTest Always 
		ZWrite Off
		Cull Off

		Pass 
		{
			Blend one zero
			Name"SSRPass_HiZ_Depth"
			CGPROGRAM
				#pragma vertex vert
				#pragma fragment ScreenSpaceReflection_GenerateHiZBuffer
			ENDCG
		}

		Pass 
		{
			Blend one zero
			Name"SSRPass_HiZ_Trace"
			CGPROGRAM
				#pragma vertex vert
				#pragma fragment ScreenSpaceReflection_HiZTrace
			ENDCG
		} 

		Pass 
		{
			Blend one one
			Name"SSRPass_GetSSRColor"
			CGPROGRAM
				#pragma vertex vert
				#pragma fragment ScreenSpaceReflection_GetColor
			ENDCG
		} 

		Pass 
		{
			Blend one zero
			Name"SSRPass_Temporalfilter"
			CGPROGRAM
				#pragma vertex vert
				#pragma fragment ScreenSpaceReflection_Temporalfilter
			ENDCG
		} 
	
	}
}
