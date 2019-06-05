#ifndef __SUNLIGHT_INCLUDE__
#define __SUNLIGHT_INCLUDE__
#include "Random.cginc"
//16 for mobile
		//32 for console
		//64 for PC
		//#define SAMPLECOUNT 16
		//#define SAMPLECOUNT 32
#define SAMPLECOUNT 12

float4 _SoftParam;
float4x4 _ShadowMapVPs[4];
float4 _ShadowDisableDistance;
float3 _DirLightPos;
Texture2DArray<float> _DirShadowMap; SamplerComparisonState sampler_DirShadowMap;
float3 _DirLightFinalColor;
float4 _ShadowOffset;
float GetShadow(float4 worldPos, float depth)
{
	float eyeDistance = LinearEyeDepth(depth);
	float4 eyeRange = eyeDistance < _ShadowDisableDistance;
	eyeRange.yzw -= eyeRange.xyz;
	float zAxisUV = dot(eyeRange, float4(0, 1, 2, 3));
	float4x4 vpMat = _ShadowMapVPs[zAxisUV];
	float4 shadowPos = mul(vpMat, worldPos);
	float2 shadowUV = shadowPos.xy;
	shadowUV = shadowUV * 0.5 + 0.5;
	float2 seed = shadowUV;
	float offst = dot(eyeRange, _ShadowOffset);
	#if UNITY_REVERSED_Z
		float dist = shadowPos.z + offst;
	#else
		float dist = shadowPos.z - offst;
	#endif
	float atten = 0;

	float ShadowMapDistance = _DirShadowMap.SampleCmpLevelZero(sampler_DirShadowMap, float3(shadowUV, zAxisUV), dist);
	float PCSSDistance = lerp( 0.05, 1, saturate(dist - ShadowMapDistance) );
	float PCSSValue = dot(_SoftParam, eyeRange * PCSSDistance);

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
	float atten = GetShadow(wpos, depth);
	BSDFContext LightData;
	float3 L = _DirLightPos;
	float3 H = normalize(V + L);
	Init(LightData, N, V, L, H);
	return max(0, LitFunc(LightData, _DirLightFinalColor * atten, buffer));
}
float3 CalculateSunLight_NoShadow(float3 N, float3 V, GeometryBuffer buffer)
{
	BSDFContext LightData;
	float3 L = normalize(_DirLightPos);
	float3 H = normalize(V + L);
	Init(LightData, N, V, L, H);
//	return LightData.NoL * _DirLightFinalColor;
	return max(0, LitFunc(LightData, _DirLightFinalColor, buffer));

}
#endif