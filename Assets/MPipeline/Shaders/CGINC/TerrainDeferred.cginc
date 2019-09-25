#ifndef __MPIPEDEFERRED_INCLUDE__
// Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it uses non-square matrices
#pragma exclude_renderers gles
#define __MPIPEDEFERRED_INCLUDE__

#define UNITY_PASS_DEFERRED
#include "UnityStandardUtils.cginc"
#include "Lighting.cginc"
#include "DecalShading.cginc"
#include "Shader_Include/ImageBasedLighting.hlsl"
#include "Terrain.cginc"

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
	float3 screenUV : TEXCOORD1;
	float3 worldPos : TEXCOORD2;
	nointerpolation uint2 vtUV : TEXCOORD3;
	nointerpolation float scale : TEXCOORD4;
};

v2f_surf vert_surf (uint instanceID : SV_INSTANCEID, uint vertexID : SV_VERTEXID) 
{
	Terrain_Appdata v = GetTerrain(instanceID, vertexID);
  	v2f_surf o;
  	o.pack0 = v.uv;
		o.vtUV = v.vtUV;
  	o.pos = mul(UNITY_MATRIX_VP, float4(v.position, 1));
/*		#if UNITY_UV_STARTS_AT_TOP
		o.pos.y = -o.pos.y;
		#endif*/
	  o.worldPos = v.position;
	  o.scale = v.scale;
	o.screenUV = ComputeScreenPos(o.pos).xyw;

  	return o;
}

void frag_surf (v2f_surf IN,
		out float4 outGBuffer0 : SV_Target0,
    out float4 outGBuffer1 : SV_Target1,
    out float4 outGBuffer2 : SV_Target2,
    out float4 outEmission : SV_Target3
) {
	
  // prepare and unpack data
	float depth = IN.pos.z;
	float linearEye = LinearEyeDepth(depth);
	float2 screenUV = IN.screenUV.xy / IN.screenUV.z;
  float3 worldPos = IN.worldPos;
  float3 worldViewDir = normalize(_WorldSpaceCameraPos - worldPos.xyz);
  SurfaceOutputStandardSpecular o;
  //float3x3 wdMatrix= float3x3(float3(1, 0, 0), float3(0, 1, 0), float3(0, 0, 1));
  // call surface function
  surf (IN.pack0.xy / IN.scale, IN.vtUV + (uint2)IN.pack0.xy, o);
  o.Normal = normalize(o.Normal);
  outEmission = ProceduralStandardSpecular_Deferred (o, outGBuffer0, outGBuffer1, outGBuffer2); //GI neccessary here!


	#ifdef LIT_ENABLE
	float Roughness = clamp(1 - outGBuffer1.a, 0.02, 1);
					  float3 multiScatter;
  					float3 preint = PreintegratedDGF_LUT(_PreIntDefault, multiScatter, outGBuffer1.xyz, Roughness, dot(o.Normal, worldViewDir));
					  outGBuffer1.xyz *= multiScatter;
					
					GeometryBuffer buffer;
					buffer.AlbedoColor = outGBuffer0.rgb;
					buffer.SpecularColor = outGBuffer1.rgb;
					buffer.Roughness = Roughness;
					[branch]
					if(dot(_LightEnabled.zw, 1) > 0.5)
                    	outEmission.xyz += max(0, CalculateLocalLight(screenUV, float4(worldPos,1 ), linearEye, o.Normal, worldViewDir, buffer));
[branch]
if(_LightEnabled.x > 0.5){
	[branch]
if(_LightEnabled.y > 0.5)
					outEmission.xyz +=max(0,  CalculateSunLight(o.Normal, depth, float4(worldPos,1 ), worldViewDir, buffer));
else
					outEmission.xyz +=max(0,  CalculateSunLight_NoShadow(o.Normal, worldViewDir, buffer));
}
					outGBuffer1.xyz = preint * multiScatter;
#endif
}

/////////////
//Shadow pass
/////////////
float4x4 _ShadowMapVP;
			struct appdata_shadow
			{
				float4 vertex : POSITION;
				#if CUT_OFF
				float2 texcoord : TEXCOORD0;
				#endif
			};
			struct v2f_shadow
			{
				float4 vertex : SV_POSITION;
				#if POINT_LIGHT_SHADOW
				float3 worldPos : TEXCOORD1;
				#endif
				#if CUT_OFF
				float2 texcoord : TEXCOORD0;
				#endif
			};

			v2f_shadow vert_shadow (uint instanceID : SV_INSTANCEID, uint vertexID : SV_VERTEXID) 
			{
				Terrain_Appdata v = GetTerrain(instanceID, vertexID);
				v2f_shadow o;
				#if POINT_LIGHT_SHADOW
				o.worldPos = v.position;
				#endif
				o.vertex = mul(_ShadowMapVP, float4(v.position, 1));
				#if CUT_OFF
				o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
				#endif
				return o;
			}

			
			float frag_shadow (v2f_shadow i)  : SV_TARGET
			{
				#if CUT_OFF
				float4 c = tex2D(_MainTex, i.texcoord);
				clip(c.a * _Color.a - _Cutoff);
				#endif
				#if POINT_LIGHT_SHADOW
				return distance(i.worldPos, _LightPos.xyz) / _LightPos.w;
				#else
				return i.vertex.z;
				#endif
			}
#endif