#ifndef __VOLUMETRIC_INCLUDE__
#define __VOLUMETRIC_INCLUDE__
#include "CGINC/VoxelLight.cginc"
#include "CGINC/Random.cginc"
Texture3D<float4> _VolumeTex; SamplerState sampler_VolumeTex;
float4 _Screen_TexelSize;
float _LinearFogDensity;
#ifndef __RANDOM_INCLUDED__
float4 _RandomSeed;
#endif

float4 Fog(float eyeDepth, float2 screenuv)
{
	float z = (eyeDepth - _VolumetricLightVar.x) / _VolumetricLightVar.y;
	float physicsDistance = lerp(_VolumetricLightVar.x, _VolumetricLightVar.z, z);
	z = pow(z, 1.0 / FROXELRATE);
	float3 uvw = float3(screenuv.x, screenuv.y, z);
	uvw.xy += (MNoise(screenuv) * 2 - 1) / ((float2)_FroxelSize.xy) * 0.7;
	float4 vol = _VolumeTex.Sample(sampler_VolumeTex, uvw);
	float linearFog = exp(-physicsDistance * _LinearFogDensity);
	vol.w = z > 1 ? min(linearFog, vol.w) : vol.w;
	return vol;
}
#endif