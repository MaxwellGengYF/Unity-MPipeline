#ifndef TERRAIN_INCLUDE
#define TERRAIN_INCLUDE
struct TerrainChunkBuffer
{
    float2 worldPos;
    float2 scale;
    uint2 vtUV;
};
struct Terrain_Appdata
{
    float3 position;
    float2 uv;
    uint2 vtUV;
    float scale;
};
static const float2 TerrainMesh[96] = {float2(0.0,0.0), float2(0.0,0.25), float2(0.25,0.0), float2(0.0,0.25), float2(0.25,0.25), float2(0.25,0.0),float2(0.0,0.25), float2(0.0,0.5), float2(0.25,0.25), float2(0.0,0.5), float2(0.25,0.5), float2(0.25,0.25),float2(0.0,0.5), float2(0.0,0.75), float2(0.25,0.5), float2(0.0,0.75), float2(0.25,0.75), float2(0.25,0.5),float2(0.0,0.75), float2(0.0,1.0), float2(0.25,0.75), float2(0.0,1.0), float2(0.25,1.0), float2(0.25,0.75),float2(0.25,0.0), float2(0.25,0.25), float2(0.5,0.0), float2(0.25,0.25), float2(0.5,0.25), float2(0.5,0.0),float2(0.25,0.25), float2(0.25,0.5), float2(0.5,0.25), float2(0.25,0.5), float2(0.5,0.5), float2(0.5,0.25),float2(0.25,0.5), float2(0.25,0.75), float2(0.5,0.5), float2(0.25,0.75), float2(0.5,0.75), float2(0.5,0.5),float2(0.25,0.75), float2(0.25,1.0), float2(0.5,0.75), float2(0.25,1.0), float2(0.5,1.0), float2(0.5,0.75),float2(0.5,0.0), float2(0.5,0.25), float2(0.75,0.0), float2(0.5,0.25), float2(0.75,0.25), float2(0.75,0.0),float2(0.5,0.25), float2(0.5,0.5), float2(0.75,0.25), float2(0.5,0.5), float2(0.75,0.5), float2(0.75,0.25),float2(0.5,0.5), float2(0.5,0.75), float2(0.75,0.5), float2(0.5,0.75), float2(0.75,0.75), float2(0.75,0.5),float2(0.5,0.75), float2(0.5,1.0), float2(0.75,0.75), float2(0.5,1.0), float2(0.75,1.0), float2(0.75,0.75),float2(0.75,0.0), float2(0.75,0.25), float2(1.0,0.0), float2(0.75,0.25), float2(1.0,0.25), float2(1.0,0.0),float2(0.75,0.25), float2(0.75,0.5), float2(1.0,0.25), float2(0.75,0.5), float2(1.0,0.5), float2(1.0,0.25),float2(0.75,0.5), float2(0.75,0.75), float2(1.0,0.5), float2(0.75,0.75), float2(1.0,0.75), float2(1.0,0.5),float2(0.75,0.75), float2(0.75,1.0), float2(1.0,0.75), float2(0.75,1.0), float2(1.0,1.0), float2(1.0,0.75)};
StructuredBuffer<TerrainChunkBuffer> _TerrainChunks;
StructuredBuffer<uint> _CullResultBuffer;
Terrain_Appdata GetTerrain(uint instanceID, uint vertexID)
{
    TerrainChunkBuffer data = _TerrainChunks[_CullResultBuffer[instanceID]];
    Terrain_Appdata o;
    float2 uv = TerrainMesh[vertexID];
    o.uv = uv * data.scale.y;
    uv *= data.scale.x;
    float3 worldPos = float3(data.worldPos + uv, 0);
    o.scale = data.scale.y;
    o.position = worldPos.xzy;
    o.vtUV = data.vtUV;
    return o;
}
#endif