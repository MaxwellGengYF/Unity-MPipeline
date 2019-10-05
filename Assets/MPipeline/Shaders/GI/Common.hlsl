#ifndef _BaseCommon_
#define _BaseCommon_

#include "UnityCG.cginc"

#define float half
#define float2 half2
#define float3 half3
#define float4 half4
#define float3x3 half3x3
#define float4x4 half4x4
#define float4x3 half4x3

#define PI 3.1415926
#define Inv_PI 0.3183091
#define Two_PI 6.2831852
#define Inv_Two_PI 0.15915494


half4 Texture1DSample(Texture1D Tex, SamplerState Sampler, half UV)
{
#if COMPUTESHADER
	return Tex.SampleLevel(Sampler, UV, 0);
#else
	return Tex.Sample(Sampler, UV);
#endif
}


half4 Texture2DSample(Texture2D Tex, SamplerState Sampler, half2 UV)
{
#if COMPUTESHADER
	return Tex.SampleLevel(Sampler, UV, 0);
#else
	return Tex.Sample(Sampler, UV);
#endif
}


half4 Texture3DSample(Texture3D Tex, SamplerState Sampler, half3 UV)
{
#if COMPUTESHADER
	return Tex.SampleLevel(Sampler, UV, 0);
#else
	return Tex.Sample(Sampler, UV);
#endif
}


half4 TextureCubeSample(TextureCube Tex, SamplerState Sampler, half3 UV)
{
#if COMPUTESHADER
	return Tex.SampleLevel(Sampler, UV, 0);
#else
	return Tex.Sample(Sampler, UV);
#endif
}


half4 Texture1DSampleLevel(Texture1D Tex, SamplerState Sampler, half UV, half Mip)
{
	return Tex.SampleLevel(Sampler, UV, Mip);
}


half4 Texture2DSampleLevel(Texture2D Tex, SamplerState Sampler, half2 UV, half Mip)
{
	return Tex.SampleLevel(Sampler, UV, Mip);
}


half4 Texture2DSampleBias(Texture2D Tex, SamplerState Sampler, half2 UV, half MipBias)
{
#if COMPUTESHADER
	return Tex.SampleLevel(Sampler, UV, 0);
#else
	return Tex.SampleBias(Sampler, UV, MipBias);
#endif
}


half4 Texture2DSampleGrad(Texture2D Tex, SamplerState Sampler, float2 UV, half2 DDX, half2 DDY)
{
	return Tex.SampleGrad(Sampler, UV, DDX, DDY);
}


half4 Texture3DSampleLevel(Texture3D Tex, SamplerState Sampler, float3 UV, half Mip)
{
	return Tex.SampleLevel(Sampler, UV, Mip);
}


half4 Texture3DSampleBias(Texture3D Tex, SamplerState Sampler, half3 UV, half MipBias)
{
#if COMPUTESHADER
	return Tex.SampleBias(Sampler, UV, 0);
#else
	return Tex.SampleBias(Sampler, UV, MipBias);
#endif
}


half4 Texture3DSampleGrad(Texture3D Tex, SamplerState Sampler, half3 UV, half3 DDX, half3 DDY)
{
	return Tex.SampleGrad(Sampler, UV, DDX, DDY);
}


half4 TextureCubeSampleLevel(TextureCube Tex, SamplerState Sampler, half3 UV, half Mip)
{
	return Tex.SampleLevel(Sampler, UV, Mip);
}


half TextureCubeSampleDepthLevel(TextureCube TexDepth, SamplerState Sampler, half3 UV, half Mip)
{
	return TexDepth.SampleLevel(Sampler, UV, Mip).x;
}


half4 TextureCubeSampleBias(TextureCube Tex, SamplerState Sampler, half3 UV, half MipBias)
{
#if COMPUTESHADER
	return Tex.SampleLevel(Sampler, UV, 0);
#else
	return Tex.SampleBias(Sampler, UV, MipBias);
#endif
}


half4 TextureCubeSampleGrad(TextureCube Tex, SamplerState Sampler, half3 UV, half3 DDX, half3 DDY)
{
	return Tex.SampleGrad(Sampler, UV, DDX, DDY);
}

//converts an input 1d to 2d position. Useful for locating z frames that have been laid out in a 2d grid like a flipbook.
half2 Tile1Dto2D(float xsize, float idx)
{
	float2 xyidx = 0;
	xyidx.y = floor(idx / xsize);
	xyidx.x = idx - xsize * xyidx.y;

	return xyidx;
}

half4 PseudoVolumeTexture(Texture2D Tex, SamplerState TexSampler, float3 inPos, float2 xysize, float numframes,
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
inline half min3(half a, half b, half c)
{
    return min(min(a, b), c);
}

inline half max3(half a, half b, half c)
{
    return max(a, max(b, c));
}

inline half4 min3(half4 a, half4 b, half4 c)
{
    return half4(
        min3(a.x, b.x, c.x),
        min3(a.y, b.y, c.y),
        min3(a.z, b.z, c.z),
        min3(a.w, b.w, c.w));
}

inline half4 max3(half4 a, half4 b, half4 c)
{
    return half4(
        max3(a.x, b.x, c.x),
        max3(a.y, b.y, c.y),
        max3(a.z, b.z, c.z),
        max3(a.w, b.w, c.w));
}

inline half Luma4(half3 Color)
{
    return (Color.g * 2) + (Color.r + Color.b);
}

inline half acosFast(half inX)
{
    half x = abs(inX);
    half res = -0.156583f * x + (0.5 * PI);
    res *= sqrt(1 - x);
    return (inX >= 0) ? res : PI - res;
}

inline half asinFast(half x)
{
    return (0.5 * PI) - acosFast(x);
}

inline half ClampedPow(half X, half Y)
{
	return pow(max(abs(X), 0.000001), Y);
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





inline half3 GetViewNormal(half3 normal, half4x4 _WToCMatrix)
{
    const half3 viewNormal = mul((half3x3)_WToCMatrix, normal.rgb);
    return normalize(viewNormal);
}

inline half3 GetScreenPos(half2 uv, half depth)
{
    return half3(uv.xy * 2 - 1, depth.r);
}

inline half3 GetWorlPos(half3 screenPos, half4x4 _InverseViewProjectionMatrix)
{
    half4 worldPos = mul(_InverseViewProjectionMatrix, half4(screenPos, 1));
    return worldPos.xyz / worldPos.w;
}

inline half3 GetViewRayFromUV(half2 uv, half4x4 _ProjectionMatrix)
{
    half4 _CamScreenDir = half4(1 / _ProjectionMatrix[0][0], 1 / _ProjectionMatrix[1][1], 1, 1);
    half3 ray = half3(uv.x * 2 - 1, uv.y * 2 - 1, 1);
    ray *= _CamScreenDir.xyz;
    ray = ray * (_ProjectionParams.z / ray.z);
    return ray;
}

inline half3 GetViewPos(half3 screenPos, half4x4 _InverseProjectionMatrix)
{
    half4 viewPos = mul(_InverseProjectionMatrix, half4(screenPos, 1));
    return viewPos.xyz / viewPos.w;
}

inline half3 GetViewDir(half3 worldPos, half3 ViewPos)
{
    return normalize(worldPos - ViewPos);
}

inline half2 GetRayMotionVector(half rayDepth, half2 inUV, half4x4 _InverseViewProjectionMatrix, half4x4 _PrevViewProjectionMatrix, half4x4 _ViewProjectionMatrix)
{
    half3 screenPos = GetScreenPos(inUV, rayDepth);
    half4 worldPos = half4(GetWorlPos(screenPos, _InverseViewProjectionMatrix), 1);

    half4 prevClipPos = mul(_PrevViewProjectionMatrix, worldPos);
    half4 curClipPos = mul(_ViewProjectionMatrix, worldPos);

    half2 prevHPos = prevClipPos.xy / prevClipPos.w;
    half2 curHPos = curClipPos.xy / curClipPos.w;

    half2 vPosPrev = (prevHPos.xy + 1) / 2;
    half2 vPosCur = (curHPos.xy + 1) / 2;
    return vPosCur - vPosPrev;
}

#endif
