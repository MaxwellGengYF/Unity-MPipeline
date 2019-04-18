#ifndef __VOLUMETRIC_INCLUDE__
#define __VOLUMETRIC_INCLUDE__
#include "CGINC/VoxelLight.cginc"
#include "CGINC/Random.cginc"
Texture3D<float4> _VolumeTex; SamplerState sampler_VolumeTex;
float4 _Screen_TexelSize;
#ifndef __RANDOM_INCLUDED__
float4 _RandomSeed;
#endif

float4 Fog(float linear01Depth, float2 screenuv)
{
	float z = linear01Depth * _NearFarClip.x;
	z = (z - _NearFarClip.y) / (1 - _NearFarClip.y);
	if (z < 0.0)
		return float4(0, 0, 0, 1);
    z = pow(z, 1.0 / FROXELRATE);
	float3 uvw = float3(screenuv.x, screenuv.y, z);
	uvw.xy += (MNoise(screenuv) * 2 - 1) / ((float2)_ScreenSize.xy) * 0.7;
	return _VolumeTex.Sample(sampler_VolumeTex, uvw);
}
#endif