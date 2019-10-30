#ifndef VIRTUAL_TEXTURE
#define VIRTUAL_TEXTURE

float _TerrainWorldPosToVTUV;
float4 _TerrainVTOffset;//XY: Integer Offset   ZW: frac offset

inline float4 WorldPosToUV(float2 worldPosXZ)
{
    float2 uv = worldPosXZ * _TerrainWorldPosToVTUV;
    uv += _TerrainVTOffset.zw;
    return float4(_TerrainVTOffset.xy + floor(uv), frac(uv));
}

/// startChunkPos: absolute index in indextex(adaptive)
///localUV: 0-1 uv
inline float3 GetVirtualTextureUV(Texture2D<float4> indexTex, float4 indexTexelSize, float2 startChunkPos, float2 localUV)
{
    startChunkPos = (0.25 + startChunkPos) % indexTexelSize.zw;
    float4 scaleOffset = indexTex[startChunkPos];
    scaleOffset.w *= 2048;
    localUV = localUV * scaleOffset.x + scaleOffset.yz;
    return float3(localUV, scaleOffset.w);
}

inline float3 GetVirtualTextureUV(Texture2D<float4> indexTex, float4 indexTexelSize, float2 startChunkPos, float2 localUV, out float4 scaleOffset)
{
    startChunkPos = (0.25 + startChunkPos) % indexTexelSize.zw;
    scaleOffset = indexTex[startChunkPos];
    scaleOffset.w *= 2048;
    localUV = localUV * scaleOffset.x + scaleOffset.yz;
    return float3(localUV, scaleOffset.w);
}

inline void GetBilinearVirtualTextureUV(Texture2D<float4> indexTex, float4 indexTexelSize, float2 startChunkPos, float2 localUV, float4 textureSize, out float3 uvs[4], out float2 weight)
{
    float2 absoluteUV = localUV * textureSize.zw;
    float2 absoluteUVFrac = frac(absoluteUV);
    weight = frac(absoluteUVFrac + 0.5);
    absoluteUV -= absoluteUVFrac > 0.5 ? 0 : 1;
    float2 uv[4];
    uv[0] = absoluteUV;
    uv[1] = uv[0] + float2(1, 0);
    uv[2] = uv[0] + float2(0, 1);
    uv[3] = uv[0] + 1;
    uv[0] *= textureSize.xy;
    uv[1] *= textureSize.xy;
    uv[2] *= textureSize.xy;
    uv[3] *= textureSize.xy;
    
    
    uvs[0] = GetVirtualTextureUV(indexTex, indexTexelSize, floor(startChunkPos + uv[0]), frac(1 + uv[0]));
    uvs[1] = GetVirtualTextureUV(indexTex, indexTexelSize, floor(startChunkPos + uv[1]), frac(1 +uv[1]%1));
    uvs[2] = GetVirtualTextureUV(indexTex, indexTexelSize, floor(startChunkPos + uv[2]), frac(1 +uv[2]%1));
    uvs[3] = GetVirtualTextureUV(indexTex, indexTexelSize, floor(startChunkPos + uv[3]), frac(1 +uv[3]%1));
}


#endif