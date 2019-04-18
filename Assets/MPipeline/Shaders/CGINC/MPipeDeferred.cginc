#ifndef __MPIPEDEFERRED_INCLUDE__
#define __MPIPEDEFERRED_INCLUDE__

#define UNITY_PASS_DEFERRED
#include "UnityStandardUtils.cginc"

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

    outGBuffer2 = float4(s.Normal * 0.5f + 0.5f, 0);


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
  float3 worldViewDir : TEXCOORD4;
	#if LIGHTMAP_ON
	float2 lightmapUV : TEXCOORD5;
	#endif
  float3 lastScreenPos : TEXCOORD6;
	float3 nonJitterScreenPos : TEXCOORD7;
	float3 screenPos : TEXCOORD8;
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
  	o.worldViewDir = UnityWorldSpaceViewDir(worldPos);
		#if LIGHTMAP_ON 
		o.lightmapUV = v.lightmapUV * unity_LightmapST.xy + unity_LightmapST.zw;
		#endif
				o.nonJitterScreenPos = ComputeScreenPos(mul(_NonJitterVP, worldPos)).xyw;
				#ifdef MOTION_VECTOR
				float4 lastWorldPos =  mul(_LastFrameModel, v.vertex);
				lastWorldPos = lerp(worldPos, lastWorldPos, _LastFrameModel[3][3]);
        o.lastScreenPos = ComputeScreenPos(mul(_LastVp, lastWorldPos)).xyw;
        #else
				o.lastScreenPos = ComputeScreenPos(mul(_LastVp, worldPos)).xyw;
				#endif
				o.screenPos = ComputeScreenPos(o.pos).xyw;
  	return o;
}

void frag_surf (v2f_surf IN,
    out float4 outGBuffer0 : SV_Target0,
    out float4 outGBuffer1 : SV_Target1,
    out float4 outGBuffer2 : SV_Target2,
    out float4 outEmission : SV_Target3,
	out float2 outMotionVector : SV_Target4,
  out float depth : SV_TARGET5
) {
	depth = IN.pos.z;
	float2 screenUV = IN.screenPos.xy / IN.screenPos.z;
  // prepare and unpack data
  Input surfIN;
  surfIN.uv_MainTex = IN.pack0.xy;
  float3 worldPos = float3(IN.worldTangent.w, IN.worldBinormal.w, IN.worldNormal.w);
  float3 worldViewDir = normalize(IN.worldViewDir);
  SurfaceOutputStandardSpecular o;
  float3x3 wdMatrix= float3x3(normalize(IN.worldTangent.xyz), normalize(IN.worldBinormal.xyz), normalize(IN.worldNormal.xyz));
  // call surface function
  surf (surfIN, o);
  o.Normal = normalize(mul(normalize(o.Normal), wdMatrix));
  outEmission = ProceduralStandardSpecular_Deferred (o, worldViewDir, outGBuffer0, outGBuffer1, outGBuffer2); //GI neccessary here!
	#if LIGHTMAP_ON
	UnityGIInput giInput = (UnityGIInput)0;
	giInput.atten = 1;
	giInput.worldPos = worldPos;
	giInput.lightmapUV = float4(IN.lightmapUV, 1, 1);
	UnityGI giResult = UnityGI_Base(giInput, o.Occlusion, o.Normal);
	outEmission.xyz += giResult.indirect.diffuse;
  //outEmission.xyz += unity_Lightmap.Sample(samplerunity_Lightmap, IN.lightmapUV).xyz* o.Albedo;
	#endif

	float4 velocity = float4(IN.nonJitterScreenPos.xy, IN.lastScreenPos.xy) / float4(IN.nonJitterScreenPos.zz, IN.lastScreenPos.zz);
	outMotionVector = velocity.xy - velocity.zw;
  #if UNITY_UV_STARTS_AT_TOP
	outMotionVector.y = -outMotionVector.y;
	#endif
}

#endif