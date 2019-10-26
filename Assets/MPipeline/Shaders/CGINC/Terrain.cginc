#ifndef TERRAIN_INCLUDE
#define TERRAIN_INCLUDE

struct Terrain_Appdata
{
    float2 position;
    float2 uv;
    float2 normalizePos;
    uint2 vtUV;
};
struct TerrainPoint
{
    float2 localCoord;
    float2 coord;
};

StructuredBuffer<TerrainPoint> verticesBuffer;
float4 _StartPos;//XY: worldpos start  Z: one chunk size W: chunk count
float2 _TextureSize;
Texture2D<float> _CullingTexture; SamplerState sampler_CullingTexture;

Terrain_Appdata GetTerrain(uint vertexID)
{
    TerrainPoint v = verticesBuffer[vertexID];
    Terrain_Appdata o;
    o.position = _StartPos.xy + v.coord * _StartPos.z;
    o.uv = v.localCoord;
    o.normalizePos = v.coord / _StartPos.w;
    o.vtUV = (uint2)(v.coord + 0.3 + _TextureSize);
    return o;
}
#endif