#ifndef __ANIMATIONDEFERRED_INCLUDE__
// Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it uses non-square matrices
#pragma exclude_renderers gles
#define __ANIMATIONDEFERRED_INCLUDE__

#define UNITY_PASS_DEFERRED
#include "UnityStandardUtils.cginc"
float4x4 _LastVp;
float4x4 _NonJitterVP;
float3 _SceneOffset;

struct v2f_surf {
  UNITY_POSITION(pos);
  float2 pack0 : TEXCOORD0; 
  float4 worldTangent : TEXCOORD1;
  float4 worldBinormal : TEXCOORD2;
  float4 worldNormal : TEXCOORD3;
 float3 screenUV : TEXCOORD6;
 float3 nonJitterScreenPos : TEXCOORD4;
float3 lastScreenPos : TEXCOORD5;
};
struct Vertex
{
	float4 tangent;
	float3 normal;
	float3 position;
	float2 uv;
};
StructuredBuffer<float3x4> _TransformMatrices;
StructuredBuffer<float3x4> _LastTransformMatrices;
StructuredBuffer<Vertex> verticesBuffer;
v2f_surf vert_surf (uint vertID : SV_VERTEXID, uint instID : SV_INSTANCEID)
{
	Vertex v = verticesBuffer[vertID];
	float3x4 localToWorld = _TransformMatrices[instID];
	float3x4 lastLocalToWorld = _LastTransformMatrices[instID];
  	v2f_surf o;
  	o.pack0 = v.uv;
	float4 worldPos = float4(mul(localToWorld, float4(v.position, 1)), 1);
  	o.pos = mul(UNITY_MATRIX_VP, worldPos);
	v.tangent.xyz = mul((float3x3)localToWorld, v.tangent.xyz);
  	o.worldTangent = float4( v.tangent.xyz, worldPos.x);
	v.normal = mul((float3x3)localToWorld, v.normal);
	o.worldNormal =float4(v.normal, worldPos.z);
  	o.worldBinormal = float4(cross(v.normal, o.worldTangent.xyz) * v.tangent.w, worldPos.y);
	o.screenUV = ComputeScreenPos(o.pos).xyw;
	o.nonJitterScreenPos = ComputeScreenPos(mul(_NonJitterVP, worldPos)).xyw;
				float4 lastWorldPos = float4(mul(lastLocalToWorld, float4(v.position, 1)), 1);
        o.lastScreenPos = ComputeScreenPos(mul(_LastVp, lastWorldPos)).xyw;
  	return o;
}

void frag_surf (v2f_surf IN,
    out float4 outEmission : SV_Target3,
	out float2 outMotionVector : SV_TARGET4
) {
	float4 velocity = float4(IN.nonJitterScreenPos.xy,IN.lastScreenPos.xy) / float4(IN.nonJitterScreenPos.zz, IN.lastScreenPos.zz);
#if UNITY_UV_STARTS_AT_TOP
				outMotionVector = velocity.xw - velocity.zy;
#else
				outMotionVector = velocity.xy - velocity.zw;
#endif
  // prepare and unpack data
  float depth = IN.pos.z;
  float2 screenUV = IN.screenUV.xy / IN.screenUV.z;
  float3 worldPos = float3(IN.worldTangent.w, IN.worldBinormal.w, IN.worldNormal.w);
  float3 worldViewDir = normalize(_WorldSpaceCameraPos - worldPos.xyz);
  float3x3 wdMatrix= float3x3(normalize(IN.worldTangent.xyz), normalize(IN.worldBinormal.xyz), normalize(IN.worldNormal.xyz));
  float3 normal = UnpackNormal(tex2D(_BumpMap, IN.pack0));
  normal = normalize(mul(normal, wdMatrix));
  float NoV = dot(normal, worldViewDir);
  outEmission = max(_EmissionColor * _EmissionMultiplier, 0);
}


//////////////////
//Motion Vector Pass
//////////////////
			struct v2f_mv
			{
				float4 vertex : SV_POSITION;
				float3 nonJitterScreenPos : TEXCOORD1;
				float3 lastScreenPos : TEXCOORD2;
			};

			v2f_mv vert_mv (uint vertID : SV_VERTEXID, uint instID : SV_INSTANCEID)
			{
				v2f_mv o;
				Vertex v = verticesBuffer[vertID];
				float3x4 localToWorld = _TransformMatrices[instID];
				float3x4 lastLocalToWorld = _LastTransformMatrices[instID];
			    float4 worldPos = float4(mul(localToWorld, float4(v.position, 1)), 1);
				o.vertex = mul(UNITY_MATRIX_VP, worldPos);
				o.nonJitterScreenPos = ComputeScreenPos(mul(_NonJitterVP, worldPos)).xyw;
				float4 lastWorldPos = float4(mul(lastLocalToWorld, float4(v.position, 1)), 1);
        o.lastScreenPos = ComputeScreenPos(mul(_LastVp, lastWorldPos)).xyw;
				return o;
			}

			
			float2 frag_mv (v2f_mv i)  : SV_TARGET
			{
				float4 velocity = float4(i.nonJitterScreenPos.xy, i.lastScreenPos.xy) / float4(i.nonJitterScreenPos.zz, i.lastScreenPos.zz);
#if UNITY_UV_STARTS_AT_TOP
				return velocity.xw - velocity.zy;
#else
				return velocity.xy - velocity.zw;
#endif

			}
////////////////
//Depth Pre Pass
////////////////
float4 vert_depth(uint vertID : SV_VERTEXID, uint instID : SV_INSTANCEID) : SV_POSITION
{
	Vertex v = verticesBuffer[vertID];
	float3x4 localToWorld = _TransformMatrices[instID];
	float4 worldPos = float4(mul(localToWorld, float4(v.position, 1)), 1);
	return mul(UNITY_MATRIX_VP, worldPos);
}

float frag_depth(float4 i : SV_POSITION) : SV_TARGET
{
	return i.z;
}

#endif