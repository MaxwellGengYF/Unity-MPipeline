#ifndef __TERRAIN_INCLUDE__
#define __TERRAIN_INCLUDE__

struct TerrainPanel
{
    float3 extent;
    float3 position;
    int4 textureIndex;
    int heightMapIndex;
    uint edgeFlag;
};

shared float4 planes[6];
float PlaneTest(float3 position, float3 extent){
    float result = 1;
    [unroll]
    for(uint i = 0; i < 6; ++i)
    {
        float4 plane = planes[i];
        float3 absNormal = abs(plane.xyz);
        result *= ((dot(position, plane.xyz) - dot(absNormal, extent)) < -plane.w) ? 1.0 : 0.0;
    }
    return result;
}

inline uint2 GetIndex(uint id, const uint width)
{
    return int2(id % width, id / width);
}

inline uint GetIndex(uint2 id, const uint width)
{
    return id.y * width + id.x;
}
#endif