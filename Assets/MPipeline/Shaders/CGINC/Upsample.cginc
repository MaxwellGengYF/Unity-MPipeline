#ifndef __UPSAMPLE_INCLUDE__
#define __UPSAMPLE_INCLUDE__
#ifdef COMPUTE_SHADER


#else
#include "UnityCG.cginc"
float4 Upsampling(float2 UV, float2 SamplerSize, sampler2D depthTexture, sampler2D targetRT, sampler2D normalTex)
{
	float SceneDepth = tex2D(depthTexture, UV).r;
	float LinearDepth = Linear01Depth(SceneDepth);
	float EyeDepth = LinearEyeDepth(SceneDepth);
	float3 WorldNormal = tex2D(normalTex, UV);

				
	float4 TopLeft_Color = tex2D( targetRT, UV );
	float4 TopRight_Color = tex2D( targetRT, UV + ( float2(-0.5, 0.5) / SamplerSize ) );
	float4 BottomLeft_Color = tex2D( targetRT, UV + ( float2(0.5, -0.5) / SamplerSize ) );
	float4 BottomRight_Color = tex2D( targetRT, UV + ( float2(0.5, 0.5) / SamplerSize ) );

	float TopLeft_Depth = LinearEyeDepth( tex2D( depthTexture, UV ).r );
	float TopRight_Depth = LinearEyeDepth( tex2D( depthTexture, UV + ( float2(-0.5, 0.5) / SamplerSize ) ).r );
	float BottomLeft_Depth = LinearEyeDepth( tex2D( depthTexture, UV + ( float2(0.5, -0.5) / SamplerSize ) ).r );
	float BottomRight_Depth = LinearEyeDepth( tex2D( depthTexture, UV + ( float2(0.5, 0.5) / SamplerSize ) ).r );
				
	//float4 offsetDepths = float4(TopLeft_Color.z, TopRight_Color.z, BottomLeft_Color.z, BottomRight_Color.z);	
	float4 offsetDepths = float4(TopLeft_Depth, TopRight_Depth, BottomLeft_Depth, BottomRight_Depth);	
	float4 weights = saturate( 1.0 - abs(offsetDepths - EyeDepth) );
				
	float2 fractCoord = frac(UV * SamplerSize);
				
	float4 filteredX0 = lerp(TopLeft_Color * weights.x, TopRight_Color * weights.y, fractCoord.x);
	float4 filteredX1 = lerp(BottomRight_Color * weights.w, BottomLeft_Color * weights.z, fractCoord.x);
	float4 filtered = lerp(filteredX0, filteredX1, fractCoord.y);
		
	return filtered;
}
#endif
#endif