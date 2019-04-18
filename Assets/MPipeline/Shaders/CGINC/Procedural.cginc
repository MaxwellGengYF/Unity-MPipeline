#ifndef PROCEDURAL
#define PROCEDURAL

#define CLUSTERCLIPCOUNT 255
#define CLUSTERVERTEXCOUNT 255
#define PLANECOUNT 6

struct Point{
    float3 vertex;
};
#ifndef COMPUTESHADER		//Below is Not for compute shader
StructuredBuffer<Point> verticesBuffer;
StructuredBuffer<uint> resultBuffer;
inline Point getVertex(uint vertexID, uint instanceID)
{
    instanceID = resultBuffer[instanceID];
	uint vertID = instanceID * CLUSTERCLIPCOUNT;
	return verticesBuffer[vertID + vertexID];
}
#endif
#endif