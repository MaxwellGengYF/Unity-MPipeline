#ifndef PROCEDURAL
#define PROCEDURAL

#define CLUSTERCLIPCOUNT 384
#define CLUSTERTRIANGLECOUNT 128
#define PLANECOUNT 6

struct Point{
    float3 vertex;
    float3 normal;
    float4 tangent;
    float2 uv0;
};

    struct MaterialProperties
    {
        float3 _Color;
        float _Glossiness;
        float _Occlusion;
        float2 _NormalIntensity;
        float _SpecularIntensity;
        float _MetallicIntensity;
        float4 _TileOffset;
        int _MainTex;
        int _BumpMap;
        int _SpecularMap;
        int _EmissionMap;
        int _HeightMap;
        float3 _EmissionColor;
        float _HeightMapIntensity;
        uint _DecalLayer;
        int _SecondaryMainTex;
        int _SecondaryBumpMap;
        int _SecondarySpecularMap;
        float4 _SecondaryTileOffset;
    };
#ifndef COMPUTESHADER		//Below is Not for compute shader
StructuredBuffer<Point> verticesBuffer;
StructuredBuffer<uint> _TriangleMaterialBuffer;
StructuredBuffer<uint> resultBuffer;
inline float3 getVertex(uint vertexID, uint instanceID)
{
    instanceID = resultBuffer[instanceID];
	uint vertID = instanceID * CLUSTERCLIPCOUNT + vertexID;
	return verticesBuffer[vertID].vertex;
}

inline Point getVertexWithMat(uint vertexID, uint instanceID, out uint matID)
{
    instanceID = resultBuffer[instanceID];
	uint vertID = instanceID * CLUSTERCLIPCOUNT + vertexID;
    uint triID = instanceID * CLUSTERTRIANGLECOUNT + vertexID / 3;
    matID = _TriangleMaterialBuffer[triID];
	return verticesBuffer[vertID];
}
#endif
#endif