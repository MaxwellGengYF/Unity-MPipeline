#ifndef CUSTOM_MESH_INCLUDE
#define CUSTOM_MESH_INCLUDE
struct appdata_input
{
    float3 vertex;
    float3 normal;
    float4 tangent;
    float2 uv;
};

struct PerObjectDatas
{
    float4x4 localToWorldMatrix;
    float4 uvTileOffset;
};

/////////////
//Use Compute Shader
#ifdef COMPUTE_SHADER
RWStructuredBuffer<appdata_input> _VertexBuffer;
RWStructuredBuffer<uint> _IndexBuffer;
/////////////
//Use Shader
#else
StructuredBuffer<appdata_input> _VertexBuffer;
StructuredBuffer<PerObjectDatas> _InstanceBuffer;
StructuredBuffer<uint> _IndexBuffer;
struct v2f_example{
  float4 pos;
  float2 uv; 
  float4 worldTangent;
  float4 worldBinormal;
  float4 worldNormal;
  float3 screenUV;
};

v2f_example vert(uint vertexID, uint instanceID)
{
    v2f_example o;
    PerObjectDatas data = _InstanceBuffer[instanceID];
    appdata_input v = _VertexBuffer[_IndexBuffer[vertexID]];
    o.uv = v.uv * data.uvTileOffset.xy + data.uvTileOffset.zw;
	float4 worldPos = mul(data.localToWorldMatrix, float4(v.vertex, 1));
    o.pos = mul(UNITY_MATRIX_VP, worldPos);
	v.tangent.xyz = mul((float3x3)data.localToWorldMatrix, v.tangent.xyz);
  	o.worldTangent = float4( v.tangent.xyz, worldPos.x);
	v.normal = mul((float3x3)data.localToWorldMatrix, v.normal);
	o.worldNormal =float4(v.normal, worldPos.z);
  	o.worldBinormal = float4(cross(v.normal, o.worldTangent.xyz) * v.tangent.w, worldPos.y);
	o.screenUV = ComputeScreenPos(o.pos).xyw;
  	return o;
}
#endif


#endif