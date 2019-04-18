#ifndef __DECALSHADING_INCLUDE__
#define __DECALSHADING_INCLUDE__
#include "Decal.cginc"
#include "VoxelLight.cginc"
StructuredBuffer<uint> _DecalCountBuffer;
StructuredBuffer<DecalData> _DecalBuffer;
Texture2DArray<float4> _DecalAtlas; SamplerState sampler_DecalAtlas;
Texture2DArray<float2> _DecalNormalAtlas; SamplerState sampler_DecalNormalAtlas;
void CalculateDecal(float2 uv, float linearDepth, float3 worldPos, inout float3 color, inout float3 normal)
{
    float zdepth = ((linearDepth - _CameraClipDistance.x) / _CameraClipDistance.y);
    uint3 clusterValue = (uint3)(float3(uv, zdepth) * uint3(XRES, YRES, ZRES));

    uint startIndex = From3DTo1D(clusterValue, uint2(XRES, YRES)) * (MAX_DECAL_PER_CLUSTER + 1);
    uint count = _DecalCountBuffer[startIndex];
    [loop]
    for(uint i = 1; i < count; ++i)
    {
        DecalData data = _DecalBuffer[_DecalCountBuffer[i + startIndex]];
        float3 localPos = mul(data.worldToLocal, float4(worldPos, 1));
        float3 lp = localPos.xyz + 0.5;
        if(dot(abs(lp - saturate(lp)), 1) > 1e-5) continue;
        float4 col = _DecalAtlas.Sample(sampler_DecalAtlas, float3(lp.xz, data.texIndex));
        color = lerp(color, col.rgb, col.a);
        float2 texNorm = _DecalNormalAtlas.Sample(sampler_DecalNormalAtlas, float3(lp.xz, data.texIndex));
        float normZ = sqrt(1 - dot(texNorm, texNorm));
        normal = lerp(normal,  float3(texNorm, normZ), col.a);
    }
}
#endif