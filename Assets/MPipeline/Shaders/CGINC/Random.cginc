#ifndef __RANDOM_INCLUDED__
#define __RANDOM_INCLUDED__

float4 _RandomSeed;
static const uint k = 1103515245;
float3 hash( uint3 x )
{
    x = ((x>>8)^x.yzx)*k;
    x = ((x>>8)^x.yzx)*k;
    x = ((x>>8)^x.yzx)*k;
    return float3(x) / 0xffffffffU;
}
float2 hash( uint2 x )
{
    x = ((x>>8)^x.yx)*k;
    x = ((x>>8)^x.yx)*k;
    x = ((x>>8)^x.yx)*k;
    return float2(x)/0xffffffffU;
}
inline float2 MNoise(float2 pos) {
    uint2 seed = frac(pos * _RandomSeed.xy + _RandomSeed.zw) * 0xffffffffU;
	return hash(seed).yx;
}
inline float cellNoise_Single(float3 p)
{
	float spot = dot(p, _RandomSeed.xyz * float3(0.69752416, 0.83497501, 0.49726514));
	return (frac(sin(spot) * _RandomSeed.zxw) * 2 - 1).xzy;
}

inline float3 MNoise(float3 pos)
{
	uint3 seed = frac(pos * _RandomSeed.xyz + _RandomSeed.w) * 0xffffffffU;
	return hash(seed).yzx;
}

inline float3 static_MNoise(float3 pos)
{
    uint3 seed = frac(pos) * 0xffffffffU;
    return hash(seed).yzx;
}
inline float2 static_MNoise(float2 pos)
{
     uint2 seed = frac(pos) * 0xffffffffU;
	return hash(seed).yx;
}
#endif