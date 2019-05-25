#ifndef __DECALSHADING_INCLUDE__
// Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it uses non-square matrices
#pragma exclude_renderers gles
#define __DECALSHADING_INCLUDE__
#include "VoxelLight.cginc"
StructuredBuffer<uint> _DecalIndexBuffer;
StructuredBuffer<Decal> _AllDecals;
Texture2DArray<float4> _DecalAtlas; SamplerState sampler_DecalAtlas;
Texture2DArray<float2> _DecalNormalAtlas; SamplerState sampler_DecalNormalAtlas;
void CalculateDecal(float2 uv, float linearDepth, float3 worldPos, inout float3 color, inout float3 normal)
{
    float zdepth =  pow(max(0,(linearDepth - _CameraClipDistance.x) / _CameraClipDistance.y), 1.0 / CLUSTERRATE);
    if(zdepth < 0 || zdepth > 1) return;
    uint3 voxelValue = (uint3)(float3(uv, zdepth) * uint3(XRES, YRES, ZRES));
    uint startIndex = GetIndex(voxelValue, VOXELSIZE, (MAXLIGHTPERCLUSTER + 1));
    uint count = _DecalIndexBuffer[startIndex];
    [loop]
    for(uint i = startIndex + 1; i < count; ++i)
    {
        Decal data = _AllDecals[_DecalIndexBuffer[i + startIndex]];
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