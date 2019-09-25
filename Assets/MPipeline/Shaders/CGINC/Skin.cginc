#ifndef __SKIN_INCLUDE__
#define __SKIN_INCLUDE__
        struct Vertex
        {
            float4 tangent;
            float3 normal;
            float3 position;
            float2 uv;
        };
        struct SkinVertex
        {
            float4 tangent;
            float3 normal;
            float3 position;
            float2 uv;
            int4 boneIndex;
            float4 boneWeight;
        };

#endif