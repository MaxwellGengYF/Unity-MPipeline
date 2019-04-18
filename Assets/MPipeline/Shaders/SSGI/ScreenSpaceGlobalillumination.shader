Shader "Hidden/ScreenSpaceGlobalillumination" {

	CGINCLUDE
		#include "SSGiPass.cginc"
	ENDCG

	SubShader {
		ZTest Always 
		ZWrite Off
		Cull off
//0
		Pass 
		{
			Name"Pass_HierarchicalZBuffer_Pass"
			CGPROGRAM
				#pragma vertex vert
				#pragma fragment Hierarchical_ZBuffer
			ENDCG
		} 
//1
		Pass 
		{
			Name"Pass_Hierarchical_ZTrace_SingleSampler"
			CGPROGRAM
				#pragma vertex vert
				#pragma fragment SSGi_SingleSPP
			ENDCG
		} 
//2
		Pass 
		{
			Name"Pass_Hierarchical_ZTrace_MultiSampler"
			CGPROGRAM
				#pragma vertex vert
				#pragma fragment SSGi_MultiSPP
			ENDCG
		} 




//3
		Pass 
		{
			Name"Pass_Temporalfilter_01"
			CGPROGRAM
				#pragma vertex vert
				#pragma fragment Temporalfilter_01
			ENDCG
		} 
//4
		Pass 
		{
			Name"Pass_Bilateralfilter_X_01"
			CGPROGRAM
				#pragma vertex vert
				#pragma fragment Bilateralfilter_X_01
			ENDCG
		} 
//5
		Pass 
		{
			Name"Pass_Bilateralfilter_Y_01"
			CGPROGRAM
				#pragma vertex vert
				#pragma fragment Bilateralfilter_Y_01
			ENDCG
		} 
//6
		Pass 
		{
			Name"Pass_Temporalfilter_02"
			CGPROGRAM
				#pragma vertex vert
				#pragma fragment Temporalfilter_02
			ENDCG
		} 
//7
		Pass 
		{
			Name"Pass_Bilateralfilter_X_02"
			CGPROGRAM
				#pragma vertex vert
				#pragma fragment Bilateralfilter_X_02
			ENDCG
		} 
//8
		Pass 
		{
			Name"Pass_Bilateralfilter_Y_02"
			CGPROGRAM
				#pragma vertex vert
				#pragma fragment Bilateralfilter_Y_02
			ENDCG
		} 
//9
		Pass 
		{
			Name"Pass_CombineReflection"
			CGPROGRAM
				#pragma vertex vert
				#pragma fragment CombineReflectionColor
			ENDCG
		}
//10
		Pass 
		{
			Name"Pass_DeBug_SSRColor"
			CGPROGRAM
				#pragma vertex vert
				#pragma fragment DeBug_SSRColor
			ENDCG
		}
		
	}
}
