#ifndef __STANDARDSURFACE_INCLUDE__
#define __STANDARDSURFACE_INCLUDE__
sampler2D _PreIntDefault;
Texture2DArray<float> _VirtualHeightMap; SamplerState sampler_VirtualHeightMap;
Texture2DArray<float4> _VirtualAlbedoMap; SamplerState sampler_VirtualAlbedoMap;
Texture2DArray<float4> _VirtualNormalMap; SamplerState sampler_VirtualNormalMap;
Texture2DArray<float4> _VirtualSMOMap; SamplerState sampler_VirtualSMOMap;
#include "VirtualTexture.cginc"
float3 ProcessNormal(float4 value)
{
    value.x *= value.w > 0.01 ? value.w : 1;
    float3 n;
    n.xy = value.xy * 2 - 1;
    n.z = sqrt(1 - dot(n.xy, n.xy));
    return n;
}
		void surf (float2 uv, uint2 vtIndex, inout SurfaceOutputStandardSpecular o) {
			float3 vtUV = GetVirtualTextureUV(vtIndex + floor(uv), frac(uv));
#ifdef LIT_ENABLE
			float4 spec = float4(0, 0, 1, 1);//_VirtualSMOMap.SampleLevel(sampler_VirtualSMOMap, vtUV, 0);
			float4 c =  float4(uv.xy, 1, 1);//_VirtualAlbedoMap.SampleLevel(sampler_VirtualAlbedoMap, vtUV, 0);
			o.Normal = float3(0, 0, 1);// ProcessNormal(_VirtualNormalMap.SampleLevel(sampler_VirtualNormalMap, vtUV, 0));
			o.Albedo = c.rgb;

			o.Alpha = 1;
			o.Occlusion = spec.b;
			float metallic =  spec.g;
			o.Specular = lerp(0.04, o.Albedo, metallic); 
			o.Albedo *= lerp(1 - 0.04, 0, metallic);
			o.Smoothness = spec.r;
			#else
			o = (SurfaceOutputStandardSpecular)0;
#endif
			o.Emission = 0;
		}


void VertexOffset(inout float4 vertex, float3 normal, float2 uv)
{
	#ifdef USE_TESSELLATION
	vertex.xyz += _HeightMap.SampleLevel(sampler_HeightMap, uv, 0) * normal * _HeightmapIntensity;
	#endif
}

#endif