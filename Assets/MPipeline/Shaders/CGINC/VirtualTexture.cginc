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

inline float4 GetVirtualTextureScaleOffset(Texture2D<float4> indexTex, float4 indexTexelSize, float2 startChunkPos)
{
    startChunkPos = (0.25 + startChunkPos) % indexTexelSize.zw;
    float4 scaleOffset = indexTex[startChunkPos];
    scaleOffset.w *= 2048;
    return scaleOffset;
}

inline void GetBilinearVirtualTextureUV(Texture2D<float4> indexTex, float4 indexTexelSize, float2 startChunkPos, float2 localUV, float4 textureSize, out float3 uvs[4], out float2 weight)
{
    float2 absoluteUV = localUV * textureSize.zw;
    float4 inBounding = float4(absoluteUV > 0.5, absoluteUV < textureSize.zw - 0.5);
    float2 absoluteUVFrac = frac(absoluteUV);
    weight = absoluteUVFrac - 0.5;
    float2 sampleLength = weight < 0 ? -1 : 1;
    weight = abs(weight);
    float2 uv[4];
    uv[0] = absoluteUV;
    uv[1] = absoluteUV + float2(sampleLength.x, 0);
    uv[2] = absoluteUV + float2(0, sampleLength.y);
    uv[3] = absoluteUV + sampleLength;
    uv[0] *= textureSize.xy;
    uv[1] *= textureSize.xy;
    uv[2] *= textureSize.xy;
    uv[3] *= textureSize.xy;
    [branch]
    if(dot(inBounding, 0.25) > 0.99)
    {
        float4 scaleOffset = GetVirtualTextureScaleOffset(indexTex, indexTexelSize, startChunkPos);
        uvs[0] = float3(uv[0] * scaleOffset.x + scaleOffset.yz, scaleOffset.w);
        uvs[1] = float3(uv[1] * scaleOffset.x + scaleOffset.yz, scaleOffset.w);
        uvs[2] = float3(uv[2] * scaleOffset.x + scaleOffset.yz, scaleOffset.w);
        uvs[3] = float3(uv[3] * scaleOffset.x + scaleOffset.yz, scaleOffset.w);
    }
    else
    {
    uvs[0] = GetVirtualTextureUV(indexTex, indexTexelSize, floor(startChunkPos + uv[0]), frac(1 + uv[0]));
    uvs[1] = GetVirtualTextureUV(indexTex, indexTexelSize, floor(startChunkPos + uv[1]), frac(1 +uv[1]));
    uvs[2] = GetVirtualTextureUV(indexTex, indexTexelSize, floor(startChunkPos + uv[2]), frac(1 +uv[2]));
    uvs[3] = GetVirtualTextureUV(indexTex, indexTexelSize, floor(startChunkPos + uv[3]), frac(1 +uv[3]));
    }
}


#endif