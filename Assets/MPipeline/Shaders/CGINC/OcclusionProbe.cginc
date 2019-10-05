#ifndef __OCCLUSIONPROBE_INCLUDE__
#define __OCCLUSIONPROBE_INCLUDE__
#include "../GI/GlobalIllumination.cginc"
Texture3D<float4> _OcclusionSrc0; SamplerState sampler_OcclusionSrc0;
Texture3D<float4> _OcclusionSrc1;SamplerState sampler_OcclusionSrc1;
Texture3D<float> _OcclusionSrc2;SamplerState sampler_OcclusionSrc2;
float4x4 _OcclusionMatrix;
float GetOcclusion(float3 position, float3 normal)
{
	float3 localPos = mul(float4(position, 1), _OcclusionMatrix).xyz;
	if(dot(abs(localPos) > 0.5, 2) > 1) return;
	localPos += 0.5;
	float4 src0 = _OcclusionSrc0.Sample(sampler_OcclusionSrc0, localPos);
	float4 src1 = _OcclusionSrc0.Sample(sampler_OcclusionSrc1, localPos);
	float src2 = _OcclusionSrc0.Sample(sampler_OcclusionSrc2, localPos);

	float4 sh0, sh1;
	float sh2;
	SHCosineLobe(normal, sh0, sh1, sh2);
	return dot(src0 * sh0, 1) + dot(src1 * sh1, 1) + src2 * sh2;
}
#endif