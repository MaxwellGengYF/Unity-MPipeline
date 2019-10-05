#ifndef __ANIMATIONDEFERRED_INCLUDE__
// Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it uses non-square matrices
#pragma exclude_renderers gles
#define __ANIMATIONDEFERRED_INCLUDE__

#define UNITY_PASS_DEFERRED
#include "UnityStandardUtils.cginc"
#include "Lighting.cginc"
#include "DecalShading.cginc"
#include "Skin.cginc"
#include "Shader_Include/ImageBasedLighting.hlsl"

#define GetScreenPos(pos) ((float2(pos.x, pos.y) * 0.5) / pos.w + 0.5)

float4 ProceduralStandardSpecular_Deferred (inout SurfaceOutputStandardSpecular s, out float4 outGBuffer0, out float4 outGBuffer1, out float4 outGBuffer2)
{
    // energy conservation
    float oneMinusReflectivity;
    s.Albedo = EnergyConservationBetweenDiffuseAndSpecular (s.Albedo, s.Specular, /*out*/ oneMinusReflectivity);
    // RT0: diffuse color (rgb), occlusion (a) - sRGB rendertarget
    outGBuffer0 = float4(s.Albedo, s.Occlusion);

    // RT1: spec color (rgb), smoothness (a) - sRGB rendertarget
    outGBuffer1 = float4(s.Specular, s.Smoothness);

    // RT2: normal (rgb), --unused, very low precision-- (a)

    outGBuffer2 = float4(s.Normal * 0.5f + 0.5f, 1);


		float4 emission = float4(s.Emission, 1);
    return emission;
}
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
	#ifdef USE_UV4
	float2 uv4 : TEXCOORD7;
	#endif
	float3 nonJitterScreenPos : TEXCOORD4;
	float3 lastScreenPos : TEXCOORD5;
};
StructuredBuffer<Vertex> verticesBuffer;
StructuredBuffer<Vertex> _LastVerticesBuffer;
StructuredBuffer<uint> triangleBuffer;
v2f_surf vert_surf (uint vertID : SV_VERTEXID) 
{
	Vertex v = verticesBuffer[triangleBuffer[vertID]];
  	v2f_surf o;
  	o.pack0 = v.uv;
	#ifdef USE_UV4
	o.uv4 = 0;
	#endif
	float4 worldPos =  float4(v.position, 1);
  	o.pos = mul(UNITY_MATRIX_VP, worldPos);
  	o.worldTangent = float4( v.tangent.xyz, worldPos.x);
	o.worldNormal =float4(v.normal, worldPos.z);
  	o.worldBinormal = float4(cross(v.normal, o.worldTangent.xyz) * v.tangent.w, worldPos.y);
	o.screenUV = ComputeScreenPos(o.pos).xyw;
	o.nonJitterScreenPos = ComputeScreenPos(mul(_NonJitterVP, worldPos)).xyw;
				Vertex lastV = _LastVerticesBuffer[triangleBuffer[vertID]];
				float4 lastWorldPos = float4(lastV.position, 1);
        		o.lastScreenPos = ComputeScreenPos(mul(_LastVp, lastWorldPos)).xyw;
  	return o;
}

void frag_surf (v2f_surf IN,
		out float4 outGBuffer0 : SV_Target0,
    out float4 outGBuffer1 : SV_Target1,
    out float4 outGBuffer2 : SV_Target2,
    out float4 outEmission : SV_Target3,
	out float2 outMotionVector : SV_TARGET4
) {
	float4 velocity = float4(IN.nonJitterScreenPos.xy, IN.lastScreenPos.xy) / float4(IN.nonJitterScreenPos.zz, IN.lastScreenPos.zz);
#if UNITY_UV_STARTS_AT_TOP
				outMotionVector =  velocity.xw - velocity.zy;
#else
				outMotionVector = velocity.xy - velocity.zw;
#endif
  // prepare and unpack data
	float depth = IN.pos.z;
	float linearEye = LinearEyeDepth(depth);
  Input surfIN;
	float2 screenUV = IN.screenUV.xy / IN.screenUV.z;
  surfIN.uv_MainTex = IN.pack0.xy;
  #ifdef USE_UV4
  surfIN.uv4 = IN.uv4.xy;
  #endif
  float3 worldPos = float3(IN.worldTangent.w, IN.worldBinormal.w, IN.worldNormal.w);
  float3 worldViewDir = normalize(_WorldSpaceCameraPos - worldPos.xyz);
  SurfaceOutputStandardSpecular o;
  float3x3 wdMatrix= float3x3(normalize(IN.worldTangent.xyz), normalize(IN.worldBinormal.xyz), normalize(IN.worldNormal.xyz));
	surfIN.viewDir = normalize(mul(wdMatrix, worldViewDir));
  // call surface function
  surf (surfIN, o);
  o.Normal = normalize(mul(normalize(o.Normal), wdMatrix));
  outEmission = ProceduralStandardSpecular_Deferred (o, outGBuffer0, outGBuffer1, outGBuffer2); //GI neccessary here!

	#if LIT_ENABLE
	float Roughness = clamp(1 - outGBuffer1.a, 0.02, 1);
					  float3 multiScatter;
  					float3 preint = PreintegratedDGF_LUT(_PreIntDefault, multiScatter, outGBuffer1.xyz, Roughness, dot(o.Normal, worldViewDir));
					outGBuffer1.xyz *= multiScatter;
					GeometryBuffer buffer;
					buffer.AlbedoColor = outGBuffer0.rgb;
					buffer.SpecularColor = outGBuffer1.rgb;
					buffer.Roughness = Roughness;
#if CLEARCOAT_LIT
					buffer.ClearCoat_MultiScatterEnergy = multiScatter;
					buffer.ClearCoat = _ClearCoat;
					buffer.ClearCoat_Roughness = clamp(1 - _ClearCoatSmoothness, 0.02, 1);
#endif
#if SKIN_LIT
					buffer.Skin_Roughness = clamp(1 - _ClearCoatSmoothness, 0.02, 1);
#endif

#if ENABLE_SUN
#if ENABLE_SUNSHADOW
					outEmission.xyz +=max(0,  CalculateSunLight(o.Normal, depth, float4(worldPos,1 ), worldViewDir, buffer));
#else
					outEmission.xyz +=max(0,  CalculateSunLight_NoShadow(o.Normal, worldViewDir, buffer));
#endif
#endif

#if SPOTLIGHT || POINTLIGHT
                    outEmission.xyz += max(0, CalculateLocalLight(screenUV, float4(worldPos,1 ), linearEye, o.Normal, worldViewDir, buffer));
#endif
					outGBuffer1.xyz = preint * multiScatter;
#endif
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

			v2f_mv vert_mv (uint vertID : SV_VERTEXID)
			{
				v2f_mv o;
				Vertex v = verticesBuffer[triangleBuffer[vertID]];
				
			    float4 worldPos = float4(v.position, 1);
				o.vertex = mul(UNITY_MATRIX_VP, worldPos);
				o.nonJitterScreenPos = ComputeScreenPos(mul(_NonJitterVP, worldPos)).xyw;
				Vertex lastV = _LastVerticesBuffer[triangleBuffer[vertID]];
				float4 lastWorldPos = float4(lastV.position, 1);
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
/////////////
//Shadow pass
/////////////
float4x4 _ShadowMapVP;
			struct appdata_shadow
			{
				float4 vertex : POSITION;
			};
			struct v2f_shadow
			{
				float4 vertex : SV_POSITION;
				#if POINT_LIGHT_SHADOW
				float3 worldPos : TEXCOORD1;
				#endif

			};

			v2f_shadow vert_shadow (uint vertID : SV_VERTEXID)
			{
				Vertex v = verticesBuffer[triangleBuffer[vertID]];
				float4 worldPos = float4(v.position, 1);
				v2f_shadow o;
				#if POINT_LIGHT_SHADOW
				o.worldPos = worldPos.xyz;
				#endif
				o.vertex = mul(_ShadowMapVP, worldPos);
				return o;
			}

			
			float frag_shadow (v2f_shadow i)  : SV_TARGET
			{
				#if POINT_LIGHT_SHADOW
				return distance(i.worldPos, _LightPos.xyz) / _LightPos.w;
				#else
				return i.vertex.z;
				#endif
			}
#endif