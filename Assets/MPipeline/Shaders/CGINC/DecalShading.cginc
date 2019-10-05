#ifndef __DECALSHADING_INCLUDE__
// Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it uses non-square matrices
#pragma exclude_renderers gles
#define __DECALSHADING_INCLUDE__
#include "VoxelLight.cginc"
Texture3D<int> _DecalTile; float2 _TileSize;
StructuredBuffer<Decal> _AllDecals;
Texture2DArray<float4> _DecalAtlas; SamplerState sampler_DecalAtlas;
Texture2DArray<float2> _DecalNormalAtlas; SamplerState sampler_DecalNormalAtlas;
Texture2DArray<float4> _DecalSpecularAtlas; SamplerState sampler_DecalSpecularAtlas;
void CalculateDecal(float2 uv, int layer, float3 worldPos, float height, inout float3 color, inout float3 normal, inout float3 specular, inout float smoothness, inout float occlusion)
{
    uint2 id = (uint2)(uv * _TileSize);
    uint count = _DecalTile[uint3(id, 0)];
    [loop]
    for(uint i = 1; i < count; ++i)
    {
        Decal data = _AllDecals[_DecalTile[uint3(id, i)]];
        uint a = (uint)layer;
        if((data.layer & a) == 0) continue;
        float3 localPos = mul(data.worldToLocal, float4(worldPos, 1));
        float3 lp = localPos.xyz + 0.5;
		lp.y = 1-lp.y;
        if(dot(abs(lp - saturate(lp)), 1) > 1e-5) continue;
        float4 uv = lp.xyxy * float4(data.albedoScaleOffset.xy, data.normalScaleOffset.xy) + float4(data.albedoScaleOffset.zw, data.normalScaleOffset.zw);
        #if UNITY_UV_STARTS_AT_TOP
        uv.yw = 1 - uv.yw;
        #endif
        float4 col;
        float normalWeight;
        if(data.texIndex.x >= 0){
        col = _DecalAtlas.Sample(sampler_DecalAtlas, float3(uv.xy, data.texIndex.x));
        normalWeight = col.a;
        }
        else {
            col = 0;
            normalWeight = 1;
        }
        float heightMask =saturate(height * data.heightmapScaleOffset.x + data.heightmapScaleOffset.y);
        normalWeight *= heightMask; 
        col.a *= heightMask;
        if(data.texIndex.y >= 0){
        float2 texNorm = _DecalNormalAtlas.Sample(sampler_DecalNormalAtlas, float3(uv.zw,data.texIndex.y));
        float normZ = sqrt(abs(1 - dot(texNorm,texNorm)));
        normal = lerp(normal,  float3(texNorm, normZ), normalWeight * data.opacity.y);
        }
        if(data.texIndex.z >= 0)
        {
            float2 specUV = lp.xy * data.specularScaleOffset.xy + data.specularScaleOffset.zw;
            #if UNITY_UV_STARTS_AT_TOP
        specUV.y = 1 - specUV.y;
        #endif
            float4 specSmo = _DecalSpecularAtlas.Sample(sampler_DecalSpecularAtlas, float3(specUV, data.texIndex.z));
            float specWeight = normalWeight * data.opacity.z;
            float specValue = specSmo.a * 0.08;
            float3 spec = lerp(specValue, col.xyz, specSmo.g);
            col.xyz *= lerp(1 - specValue, 0, specSmo.g);
            specular = lerp(specular, spec, specWeight);
            smoothness = lerp(smoothness, specSmo.r, specWeight);
            occlusion = lerp(occlusion, specSmo.b, specWeight);
        }
        color = lerp(color, col.rgb, col.a * data.opacity.x);
    }
}
#endif