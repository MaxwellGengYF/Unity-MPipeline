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
	float softValue = dot(_SoftParam, eyeRange);
	float offst = dot(eyeRange, _ShadowOffset);
	#if UNITY_REVERSED_Z
	float dist = shadowPos.z + offst;
	#else
	float dist = shadowPos.z - offst;
	#endif
	float atten = 0;
	for (int i = 0; i < SAMPLECOUNT; ++i)
	{
		seed = MNoise(seed) * 2 - 1;
		//float2 angle;
		//sincos(seed.x, angle.x, angle.y);
		atten += _DirShadowMap.SampleCmpLevelZero(sampler_DirShadowMap, float3(shadowUV +  seed * softValue, zAxisUV), dist);
	}
	atten /= SAMPLECOUNT;
	float fadeDistance = saturate((_ShadowDisableDistance.w - eyeDistance) / (_ShadowDisableDistance.w * 0.05));
	atten = lerp(1, atten, fadeDistance);
	return atten;
}

float4 CalculateSunLight(UnityStandardData data, float depth, float4 wpos, float3 viewDir)
{
	float atten = GetShadow(wpos, depth);
	float oneMinusReflectivity = 1 - SpecularStrength(data.specularColor.rgb);
	UnityIndirect ind;
	UNITY_INITIALIZE_OUTPUT(UnityIndirect, ind);
	ind.diffuse = 0;
	ind.specular = 0;
	UnityLight light;
	light.dir = _DirLightPos;
	light.color = _DirLightFinalColor * atten;

	return UNITY_BRDF_PBS(data.diffuseColor, data.specularColor, oneMinusReflectivity, data.smoothness, data.normalWorld, -viewDir, light, ind);

}
float4 CalculateSunLight_NoShadow(UnityStandardData data, float3 viewDir)
{
	float oneMinusReflectivity = 1 - SpecularStrength(data.specularColor.rgb);
	UnityIndirect ind;
	UNITY_INITIALIZE_OUTPUT(UnityIndirect, ind);
	ind.diffuse = 0;
	ind.specular = 0;
	UnityLight light;
	light.dir = _DirLightPos;
	light.color = _DirLightFinalColor;
	return UNITY_BRDF_PBS(data.diffuseColor, data.specularColor, oneMinusReflectivity, data.smoothness, data.normalWorld, -viewDir, light, ind);

}
#endif