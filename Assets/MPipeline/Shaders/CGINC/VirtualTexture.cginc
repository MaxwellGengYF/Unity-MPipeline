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
#endif