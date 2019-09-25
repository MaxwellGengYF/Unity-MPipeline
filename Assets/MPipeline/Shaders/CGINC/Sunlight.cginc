#ifndef __SUNLIGHT_INCLUDE__
#define __SUNLIGHT_INCLUDE__
#include "Random.cginc"
//16 for mobile
		//32 for console
		//64 for PC
		//#define SAMPLECOUNT 16
		//#define SAMPLECOUNT 32
#define SAMPLECOUNT 8

float4 _SoftParam;
float4x4 _ShadowMapVPs[4];
float4 _ShadowDisableDistance;
float4 _DirLightPos;
Texture2DArray<float> _DirShadowMap; SamplerComparisonState sampler_DirShadowMap;
float3 _DirLightFinalColor;
float4 _ShadowOffset;
float4 _CascadeShadowWeight;

inline void GetShadowPos(float eyeDistance, float4 worldPos, out float zAxisUV, out float4 shadowPos, out float softValue, out float shadowOffset)
{
	float4 eyeRange = eyeDistance < _ShadowDisableDistance;
	eyeRange.yzw -= eyeRange.xyz;
	zAxisUV = dot(eyeRange, _CascadeShadowWeight);
	float4x4 vpMat = _ShadowMapVPs[zAxisUV];
	shadowPos = mul(vpMat, worldPos);
	softValue = dot(_SoftParam, eyeRange);
	shadowOffset = dot(eyeRange, _ShadowOffset);
}

float GetShadow(float4 worldPos, float depth, float nol)
{
	float eyeDistance = LinearEyeDepth(depth);
	float zAxisUV, PCSSValue, offst;
	float4 shadowPos;
	GetShadowPos(eyeDistance, worldPos, zAxisUV, shadowPos, PCSSValue, offst);
	float2 shadowUV = shadowPos.xy;
	shadowUV = shadowUV * 0.5 + 0.5;
	float2 seed = shadowUV;
	#if UNITY_REVERSED_Z
		float dist = shadowPos.z + offst;
	#else
		float dist = shadowPos.z - offst;
	#endif
	float atten = 0;

	float ShadowMapDistance = _DirShadowMap.SampleCmpLevelZero(sampler_DirShadowMap, float3(shadowUV, zAxisUV), dist);
	PCSSValue *= lerp(0.5, 1, abs(nol));
	for (int i = 0; i < SAMPLECOUNT; ++i) {
		seed = MNoise(seed) * 2 - 1;
		atten += _DirShadowMap.SampleCmpLevelZero(sampler_DirShadowMap, float3(shadowUV +  seed * PCSSValue, zAxisUV), dist);
	}
	atten /= SAMPLECOUNT;
	float fadeDistance = saturate((_ShadowDisableDistance.w - eyeDistance) / (_ShadowDisableDistance.w * 0.05));
	atten = lerp(1, atten, fadeDistance);
	return atten;
}

float3 CalculateSunLight(float3 N, float depth, float4 wpos, float3 V, GeometryBuffer buffer)
{
	BSDFContext LightData = (BSDFContext)0;
	float3 L = _DirLightPos.xyz;
	float3 H =normalize(V + L);
	InitGeoData(LightData, N, V);
	InitLightingData(LightData, N, V, L, H);
	float atten = GetShadow(wpos, depth, LightData.NoL);
	return LitFunc(LightData, _DirLightFinalColor * atten, buffer);
}
float3 CalculateSunLight_NoShadow(float3 N, float3 V, GeometryBuffer buffer)
{
	BSDFContext LightData = (BSDFContext)0;
	float3 L = _DirLightPos.xyz;
	float3 H =normalize(V + L);
	InitGeoData(LightData, N, V);
	InitLightingData(LightData, N, V, L, H);
//	return LightData.NoL * _DirLightFinalColor;
	return LitFunc(LightData, _DirLightFinalColor, buffer);

}
#endif