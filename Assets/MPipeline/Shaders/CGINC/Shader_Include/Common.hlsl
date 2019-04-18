#ifndef _BaseCommon_
#define _BaseCommon_

#include "UnityCG.cginc"


#define MaterialFloat float
#define MaterialFloat2 float2
#define MaterialFloat3 float3
#define MaterialFloat4 float4
#define MaterialFloat3x3 float3x3
#define MaterialFloat4x4 float4x4
#define MaterialFloat4x3 float4x3

#define PI 3.1415926
#define Inv_PI 0.3183091
#define Two_PI 6.2831852
#define Inv_Two_PI 0.15915494

#define RANDOM(seed) (sin(cos(seed * 1354.135748 + 13.546184) * 1354.135716 + 32.6842317))
float GetRandomNoise(float2 uv, float RandomSeed)
{
	return RANDOM((_ScreenParams.y * uv.y + uv.x) * _ScreenParams.x + RandomSeed);
}

MaterialFloat4 Texture1DSample(Texture1D Tex, SamplerState Sampler, float UV)
{
#if COMPUTESHADER
	return Tex.SampleLevel(Sampler, UV, 0);
#else
	return Tex.Sample(Sampler, UV);
#endif
}

MaterialFloat4 Texture2DSample(Texture2D Tex, SamplerState Sampler, float2 UV)
{
#if COMPUTESHADER
	return Tex.SampleLevel(Sampler, UV, 0);
#else
	return Tex.Sample(Sampler, UV);
#endif
}

MaterialFloat4 Texture3DSample(Texture3D Tex, SamplerState Sampler, float3 UV)
{
#if COMPUTESHADER
	return Tex.SampleLevel(Sampler, UV, 0);
#else
	return Tex.Sample(Sampler, UV);
#endif
}

MaterialFloat4 TextureCubeSample(TextureCube Tex, SamplerState Sampler, float3 UV)
{
#if COMPUTESHADER
	return Tex.SampleLevel(Sampler, UV, 0);
#else
	return Tex.Sample(Sampler, UV);
#endif
}

MaterialFloat4 Texture1DSampleLevel(Texture1D Tex, SamplerState Sampler, float UV, MaterialFloat Mip)
{
	return Tex.SampleLevel(Sampler, UV, Mip);
}

MaterialFloat4 Texture2DSampleLevel(Texture2D Tex, SamplerState Sampler, float2 UV, MaterialFloat Mip)
{
	return Tex.SampleLevel(Sampler, UV, Mip);
}

MaterialFloat4 Texture2DSampleBias(Texture2D Tex, SamplerState Sampler, float2 UV, MaterialFloat MipBias)
{
#if COMPUTESHADER
	return Tex.SampleLevel(Sampler, UV, 0);
#else
	return Tex.SampleBias(Sampler, UV, MipBias);
#endif
}

MaterialFloat4 Texture2DSampleGrad(Texture2D Tex, SamplerState Sampler, float2 UV, MaterialFloat2 DDX, MaterialFloat2 DDY)
{
	return Tex.SampleGrad(Sampler, UV, DDX, DDY);
}

MaterialFloat4 Texture3DSampleLevel(Texture3D Tex, SamplerState Sampler, float3 UV, MaterialFloat Mip)
{
	return Tex.SampleLevel(Sampler, UV, Mip);
}

MaterialFloat4 Texture3DSampleBias(Texture3D Tex, SamplerState Sampler, float3 UV, MaterialFloat MipBias)
{
#if COMPUTESHADER
	return Tex.SampleBias(Sampler, UV, 0);
#else
	return Tex.SampleBias(Sampler, UV, MipBias);
#endif
}

MaterialFloat4 Texture3DSampleGrad(Texture3D Tex, SamplerState Sampler, float3 UV, MaterialFloat3 DDX, MaterialFloat3 DDY)
{
	return Tex.SampleGrad(Sampler, UV, DDX, DDY);
}

MaterialFloat4 TextureCubeSampleLevel(TextureCube Tex, SamplerState Sampler, float3 UV, MaterialFloat Mip)
{
	return Tex.SampleLevel(Sampler, UV, Mip);
}

MaterialFloat TextureCubeSampleDepthLevel(TextureCube TexDepth, SamplerState Sampler, float3 UV, MaterialFloat Mip)
{
	return TexDepth.SampleLevel(Sampler, UV, Mip).x;
}

MaterialFloat4 TextureCubeSampleBias(TextureCube Tex, SamplerState Sampler, float3 UV, MaterialFloat MipBias)
{
#if COMPUTESHADER
	return Tex.SampleLevel(Sampler, UV, 0);
#else
	return Tex.SampleBias(Sampler, UV, MipBias);
#endif
}

MaterialFloat4 TextureCubeSampleGrad(TextureCube Tex, SamplerState Sampler, float3 UV, MaterialFloat3 DDX, MaterialFloat3 DDY)
{
	return Tex.SampleGrad(Sampler, UV, DDX, DDY);
}

//converts an input 1d to 2d position. Useful for locating z frames that have been laid out in a 2d grid like a flipbook.
float2 Tile1Dto2D(float xsize, float idx)
{
	float2 xyidx = 0;
	xyidx.y = floor(idx / xsize);
	xyidx.x = idx - xsize * xyidx.y;

	return xyidx;
}

float4 PseudoVolumeTexture(Texture2D Tex, SamplerState TexSampler, float3 inPos, float2 xysize, float numframes,
	uint mipmode = 0, float miplevel = 0, float2 InDDX = 0, float2 InDDY = 0)
{
	float zframe = ceil(inPos.z * numframes);
	float zphase = frac(inPos.z * numframes);

	float2 uv = frac(inPos.xy) / xysize;

	float2 curframe = Tile1Dto2D(xysize.x, zframe) / xysize;
	float2 nextframe = Tile1Dto2D(xysize.x, zframe + 1) / xysize;

	float4 sampleA = 0, sampleB = 0;
	switch (mipmode)
	{
	case 0: // Mip level
		sampleA = Tex.SampleLevel(TexSampler, uv + curframe, miplevel);
		sampleB = Tex.SampleLevel(TexSampler, uv + nextframe, miplevel);
		break;
	case 1: // Gradients automatic from UV
		sampleA = Texture2DSample(Tex, TexSampler, uv + curframe);
		sampleB = Texture2DSample(Tex, TexSampler, uv + nextframe);
		break;
	case 2: // Deriviatives provided
		sampleA = Tex.SampleGrad(TexSampler, uv + curframe,  InDDX, InDDY);
		sampleB = Tex.SampleGrad(TexSampler, uv + nextframe, InDDX, InDDY);
		break;
	default:
		break;
	}

	return lerp(sampleA, sampleB, zphase);
}

float Square(float x)
{
    return x * x;
}

float2 Square(float2 x)
{
    return x * x;
}

float3 Square(float3 x)
{
    return x * x;
}

float4 Square(float4 x)
{
    return x * x;
}

float pow2(float x)
{
    return x * x;
}

float2 pow2(float2 x)
{
    return x * x;
}

float3 pow2(float3 x)
{
    return x * x;
}

float4 pow2(float4 x)
{
    return x * x;
}

float pow3(float x)
{
    return x * x * x;
}

float2 pow3(float2 x)
{
    return x * x * x;
}

float3 pow3(float3 x)
{
    return x * x * x;
}

float4 pow3(float4 x)
{
    return x * x * x;
}

float pow4(float x)
{
    float xx = x * x;
    return xx * xx;
}

float2 pow4(float2 x)
{
    float2 xx = x * x;
    return xx * xx;
}

float3 pow4(float3 x)
{
    float3 xx = x * x;
    return xx * xx;
}

float4 pow4(float4 x)
{
    float4 xx = x * x;
    return xx * xx;
}

float pow5(float x)
{
    float xx = x * x;
    return xx * xx * x;
}

float2 pow5(float2 x)
{
    float2 xx = x * x;
    return xx * xx * x;
}

float3 pow5(float3 x)
{
    float3 xx = x * x;
    return xx * xx * x;
}

float4 pow5(float4 x)
{
    float4 xx = x * x;
    return xx * xx * x;
}

float pow6(float x)
{
    float xx = x * x;
    return xx * xx * xx;
}

float2 pow6(float2 x)
{
    float2 xx = x * x;
    return xx * xx * xx;
}

float3 pow6(float3 x)
{
    float3 xx = x * x;
    return xx * xx * xx;
}

float4 pow6(float4 x)
{
    float4 xx = x * x;
    return xx * xx * xx;
}

inline float acosFast(float inX)
{
    float x = abs(inX);
    float res = -0.156583f * x + (0.5 * PI);
    res *= sqrt(1 - x);
    return (inX >= 0) ? res : PI - res;
}

inline float asinFast(float x)
{
    return (0.5 * PI) - acosFast(x);
}

inline float ClampedPow(float X, float Y)
{
	return pow(max(abs(X), 0.000001), Y);
}

inline float Min3(float a, float b, float c)
{
    return min(min(a, b), c);
}

inline float CharlieL(float x, float r)
{
    r = saturate(r);
    r = 1 - (1 - r) * (1 - r);

    float a = lerp(25.3245, 21.5473, r);
    float b = lerp(3.32435, 3.82987, r);
    float c = lerp(0.16801, 0.19823, r);
    float d = lerp(-1.27393, -1.97760, r);
    float e = lerp(-4.85967, -4.32054, r);

    return a / (1 + b * pow(x, c)) + d * x + e;
}

void ConvertAnisotropyToRoughness(float Roughness, float Anisotropy, out float RoughnessT, out float RoughnessB) {
	Roughness *= Roughness;
    float AnisoAspect = sqrt(1 - 0.9 * Anisotropy);
    RoughnessT = Roughness / AnisoAspect; 
    RoughnessB = Roughness * AnisoAspect; 
}

float3 ComputeGrainNormal(float3 grainDir, float3 V) {
	float3 B = cross(-V, grainDir);
	return cross(B, grainDir);
}

float3 GetAnisotropicModifiedNormal(float3 grainDir, float3 N, float3 V, float Anisotropy) {
	float3 grainNormal = ComputeGrainNormal(grainDir, V);
	return normalize(lerp(N, grainNormal, Anisotropy));
}





inline float ComputeDepth(float4 clippos)
{
#if defined(SHADER_TARGET_GLSL) || defined(SHADER_API_GLES) || defined(SHADER_API_GLES3)
    return (clippos.z / clippos.w) * 0.5 + 0.5;
#else
    return clippos.z / clippos.w;
#endif
}

inline float3 GetScreenPos(float2 uv, float depth)
{
    return float3(uv.xy * 2 - 1, depth.r);
}

inline float3 GetViewNormal(float3 normal, float4x4 _WToCMatrix)
{
    const float3 viewNormal = mul((float3x3)_WToCMatrix, normal.rgb);
    return normalize(viewNormal);
}

inline float LinearDepthReverseBack(float Depth)
{
    float z = ((1 / Depth) - _ZBufferParams.y) / _ZBufferParams.x;
#if defined(UNITY_REVERSED_Z)
    z = 1 - z;
#endif
    return z;
}

inline float GetDepth(sampler2D tex, float2 uv)
{
    float z = tex2Dlod(tex, float4(uv, 0, 0)).r;
#if defined(UNITY_REVERSED_Z)
    z = 1 - z;
#endif
    return z;
}

inline float Get01Depth(sampler2D tex, float2 uv)
{
    float z = Linear01Depth(tex2Dlod(tex, float4(uv, 0, 0)).r);
#if defined(UNITY_REVERSED_Z)
    z = 1 - z;
#endif
    return z;
}

inline float GetEyeDepth(sampler2D tex, float2 uv)
{
    float z = LinearEyeDepth(tex2Dlod(tex, float4(uv, 0, 0)).r);
#if defined(UNITY_REVERSED_Z)
    z = 1 - z;
#endif
    return z;
}

inline float3 GetSSRScreenPos(float2 uv, float depth)
{
    return float3(uv.xy * 2 - 1, depth.r);
}

inline float3 GetWorlPos(float3 screenPos, float4x4 _InverseViewProjectionMatrix)
{
    float4 worldPos = mul(_InverseViewProjectionMatrix, float4(screenPos, 1));
    return worldPos.xyz / worldPos.w;
}

inline float3 GetViewRayFromUV(float2 uv, float4x4 _ProjectionMatrix)
{
    float4 _CamScreenDir = float4(1 / _ProjectionMatrix[0][0], 1 / _ProjectionMatrix[1][1], 1, 1);
    float3 ray = float3(uv.x * 2 - 1, uv.y * 2 - 1, 1);
    ray *= _CamScreenDir.xyz;
    ray = ray * (_ProjectionParams.z / ray.z);
    return ray;
}

inline float3 GetViewPos(float3 screenPos, float4x4 _InverseProjectionMatrix)
{
    float4 viewPos = mul(_InverseProjectionMatrix, float4(screenPos, 1));
    return viewPos.xyz / viewPos.w;
}

inline float3 GetViewDir(float3 worldPos, float3 ViewPos)
{
    return normalize(worldPos - ViewPos);
}

inline float2 GetRayMotionVector(float rayDepth, float2 inUV, float4x4 _InverseViewProjectionMatrix, float4x4 _PrevViewProjectionMatrix, float4x4 _ViewProjectionMatrix)
{
    float3 screenPos = GetSSRScreenPos(inUV, rayDepth);
    float4 worldPos = float4(GetWorlPos(screenPos, _InverseViewProjectionMatrix), 1);

    float4 prevClipPos = mul(_PrevViewProjectionMatrix, worldPos);
    float4 curClipPos = mul(_ViewProjectionMatrix, worldPos);

    float2 prevHPos = prevClipPos.xy / prevClipPos.w;
    float2 curHPos = curClipPos.xy / curClipPos.w;

    float2 vPosPrev = (prevHPos.xy + 1) / 2;
    float2 vPosCur = (curHPos.xy + 1) / 2;
    return vPosCur - vPosPrev;
}

#endif
