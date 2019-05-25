#ifndef __MPIPEDEFERRED_INCLUDE__
#define __MPIPEDEFERRED_INCLUDE__

#define UNITY_PASS_DEFERRED
#include "UnityStandardUtils.cginc"
#include "Lighting.cginc"
#include "DecalShading.cginc"

#define GetScreenPos(pos) ((float2(pos.x, pos.y) * 0.5) / pos.w + 0.5)


float4 ProceduralStandardSpecular_Deferred (inout SurfaceOutputStandardSpecular s, float3 viewDir, out float4 outGBuffer0, out float4 outGBuffer1, out float4 outGBuffer2)
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
	#if LIGHTMAP_ON
	float2 lightmapUV : TEXCOORD5;
	#endif
};
struct appdata
{
	float4 vertex : POSITION;
	float4 tangent : TANGENT;
	float3 normal : NORMAL;
	float2 texcoord : TEXCOORD0;
	#if LIGHTMAP_ON
	float2 lightmapUV : TEXCOORD1;
	#endif
};

float4x4 _LastFrameModel;

v2f_surf vert_surf (appdata v) 
{
  	v2f_surf o;
  	o.pack0 = v.texcoord;
  	o.pos = UnityObjectToClipPos(v.vertex);
/*		#if UNITY_UV_STARTS_AT_TOP
		o.pos.y = -o.pos.y;
		#endif*/
	  float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
		v.tangent.xyz = mul((float3x3)unity_ObjectToWorld, v.tangent.xyz);
  	o.worldTangent = float4( v.tangent.xyz, worldPos.x);
	  v.normal = mul((float3x3)unity_ObjectToWorld, v.normal);
		o.worldNormal =float4(v.normal, worldPos.z);
  	o.worldBinormal = float4(cross(v.normal, o.worldTangent.xyz) * v.tangent.w, worldPos.y);
		o.screenUV = ComputeScreenPos(o.pos).xyw;
		#if LIGHTMAP_ON 
		o.lightmapUV = v.lightmapUV * unity_LightmapST.xy + unity_LightmapST.zw;
		#endif
		/*
				o.nonJitterScreenPos = ComputeScreenPos(mul(_NonJitterVP, worldPos)).xyw;
				#ifdef MOTION_VECTOR
				float4 lastWorldPos =  mul(_LastFrameModel, v.vertex);
				lastWorldPos = lerp(worldPos, lastWorldPos, _LastFrameModel[3][3]);
        o.lastScreenPos = ComputeScreenPos(mul(_LastVp, lastWorldPos)).xyw;
        #else
				o.lastScreenPos = ComputeScreenPos(mul(_LastVp, worldPos)).xyw;
				#endif
				o.screenPos = ComputeScreenPos(o.pos).xyw;*/
  	return o;
}

void frag_surf (v2f_surf IN,
    out float4 outGBuffer1 : SV_Target0,
    out float4 outGBuffer2 : SV_Target1,
    out float4 outEmission : SV_Target2
) {
	
  // prepare and unpack data
	float depth = IN.pos.z;
	float linearEye = LinearEyeDepth(depth);
  Input surfIN;
	float2 screenUV = IN.screenUV.xy / IN.screenUV.z;
  surfIN.uv_MainTex = IN.pack0.xy;
  float3 worldPos = float3(IN.worldTangent.w, IN.worldBinormal.w, IN.worldNormal.w);
  float3 worldViewDir = normalize(worldPos.xyz - _WorldSpaceCameraPos);
  SurfaceOutputStandardSpecular o;
  float3x3 wdMatrix= float3x3(normalize(IN.worldTangent.xyz), normalize(IN.worldBinormal.xyz), normalize(IN.worldNormal.xyz));
  // call surface function
  surf (surfIN, o);
	CalculateDecal(screenUV, linearEye, worldPos, o.Albedo, o.Normal);
  o.Normal = normalize(mul(normalize(o.Normal), wdMatrix));
	float4 outGBuffer0 = 0;
  outEmission = ProceduralStandardSpecular_Deferred (o, worldViewDir, outGBuffer0, outGBuffer1, outGBuffer2); //GI neccessary here!

	#if LIT_ENABLE
	#if LIGHTMAP_ON
	outGBuffer2.w = 0;
	UnityGIInput giInput = (UnityGIInput)0;
	giInput.atten = 1;
	giInput.worldPos = worldPos;
	giInput.lightmapUV = float4(IN.lightmapUV, 1, 1);
	UnityGI giResult = UnityGI_Base(giInput, o.Occlusion, o.Normal);
	outEmission.xyz += giResult.indirect.diffuse * outGBuffer0;
  //outEmission.xyz += unity_Lightmap.Sample(samplerunity_Lightmap, IN.lightmapUV).xyz* o.Albedo;
	#endif

	UnityStandardData standardData;
	            standardData.occlusion = outGBuffer0.a;
	            standardData.diffuseColor = outGBuffer0.rgb;
	            standardData.specularColor = outGBuffer1.rgb;
	            standardData.smoothness = outGBuffer1.a;
	            standardData.normalWorld = o.Normal;

	            #if ENABLE_SUN
					#if ENABLE_SUNSHADOW
					outEmission.xyz +=max(0,  CalculateSunLight(standardData, depth, float4(worldPos,1 ), worldViewDir));
					#else
					outEmission.xyz +=max(0,  CalculateSunLight_NoShadow(standardData, worldViewDir));
					#endif
					#endif

					float Roughness = clamp(1 - standardData.smoothness, 0.02, 1);

					#if SPOTLIGHT || POINTLIGHT
                    outEmission.xyz += max(0, CalculateLocalLight(screenUV, float4(worldPos,1 ), linearEye, standardData.diffuseColor, o.Normal, outGBuffer1, Roughness, -worldViewDir));
					#endif
	#endif
}

#endif