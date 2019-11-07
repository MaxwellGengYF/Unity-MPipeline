#ifndef __MPIPEDEFERRED_INCLUDE__
// Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it uses non-square matrices
#pragma exclude_renderers gles
#define __MPIPEDEFERRED_INCLUDE__
#define TESS_COUNT 63
#define SHADOW_TESS_COUNT 15
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
float4 _HeightScaleOffset;
Texture2D<float4> _MaskIndexMap;
float4 _MaskIndexMap_TexelSize;

struct InternalTessInterp_appdata_full {
  float4 pos : INTERNALTESSPOS;
  float4 pack0 : TEXCOORD0; 
  float2 normalizePos : TEXCOORD2;
	float3 screenUV : TEXCOORD1;
	nointerpolation uint3 vtUV : TEXCOORD3;
   float2 absoluteUV : TEXCOORD5;
};

struct v2f_surf {
  UNITY_POSITION(pos);
	float4 screenUV : TEXCOORD1;
	float4 worldPos : TEXCOORD2;
	nointerpolation uint vtUV : TEXCOORD3;

 
};
struct UnityTessellationFactors {
    float edge[3] : SV_TessFactor;
    float inside : SV_InsideTessFactor;
};

inline float3 UnityCalcTriEdgeTessFactors (float3 triVertexFactors)
{
    float3 tess;
    tess.x = 0.5 * (triVertexFactors.y + triVertexFactors.z);
    tess.y = 0.5 * (triVertexFactors.x + triVertexFactors.z);
    tess.z = 0.5 * (triVertexFactors.x + triVertexFactors.y);
    return tess;
}


inline float UnityCalcDistanceTessFactor (float2 wpos, float minDist, float maxDist, float tess)
{
    float dist = distance (wpos, _WorldSpaceCameraPos.xz);
    float f = clamp(1.0 - (dist - minDist) / (maxDist - minDist), 0.01, 1.0) * tess;
    return f;
}

inline float3 tessDist (float2 v0, float2 v1, float2 v2, float minDist, float maxDist, float tess)
{
    float3 f;
    f.x = UnityCalcDistanceTessFactor (v0,minDist, maxDist, tess);
    f.y = UnityCalcDistanceTessFactor (v1,minDist, maxDist, tess);
    f.z = UnityCalcDistanceTessFactor (v2,minDist, maxDist, tess);
   	return UnityCalcTriEdgeTessFactors (f);

}

InternalTessInterp_appdata_full tessvert_surf (uint vertexID : SV_VERTEXID) 
{
	Terrain_Appdata v = GetTerrain(vertexID);
  	InternalTessInterp_appdata_full o;
  	o.pack0 = float4(v.uv,1,1);
		o.vtUV = uint3(v.vtUV, 0);
    o.absoluteUV = (v.uv + v.indexCoord) / _StartPos.w;
    o.normalizePos = v.normalizePos;
  		o.pos = float4(v.position.x, 0, v.position.y, 1);
/*		#if UNITY_UV_STARTS_AT_TOP
		o.pos.y = -o.pos.y;
		#endif*/
	o.screenUV = ComputeScreenPos(o.pos).xyw;
  	return o;
}


inline UnityTessellationFactors hsconst_surf (InputPatch<InternalTessInterp_appdata_full,3> v) {
  UnityTessellationFactors o;
  float2 pos = v[0].normalizePos + v[1].normalizePos + v[2].normalizePos;
  pos *= 0.333333333;
  float3 tess = tessDist(v[0].pos.xz, v[1].pos.xz, v[2].pos.xz, _TessellationFactors.x, _TessellationFactors.y, TESS_COUNT);
  tess *= _CullingTexture.SampleLevel(sampler_CullingTexture, pos, 0);
  o.edge[0] = tess.x;
  o.edge[1] = tess.y;
  o.edge[2] = tess.z;
  o.inside = dot(tess, 0.333333333);

  return o;
}
float4 _VirtualHeightmap_TexelSize;
inline float SampleHeight(float3 uvs[4], float2 weight)
{
  float4 result = float4(
    _VirtualHeightmap.SampleLevel(sampler_VirtualHeightmap, uvs[0], 0),
    _VirtualHeightmap.SampleLevel(sampler_VirtualHeightmap, uvs[1], 0),
    _VirtualHeightmap.SampleLevel(sampler_VirtualHeightmap, uvs[2], 0),
    _VirtualHeightmap.SampleLevel(sampler_VirtualHeightmap, uvs[3], 0)
  );
  result.xy = lerp(result.xy, result.zw, weight.y);
  return lerp(result.x, result.y, weight.x);
}

[UNITY_domain("tri")]
[UNITY_partitioning("fractional_odd")]
[UNITY_outputtopology("triangle_cw")]
[UNITY_patchconstantfunc("hsconst_surf")]
[UNITY_outputcontrolpoints(3)]
inline InternalTessInterp_appdata_full hs_surf (InputPatch<InternalTessInterp_appdata_full,3> v, uint id : SV_OutputControlPointID) {
  InternalTessInterp_appdata_full o = v[id];
  float3 vtUV = GetVirtualTextureUV(_TerrainVTIndexTex, _TerrainVTIndexTex_TexelSize,o.vtUV.xy, o.pack0.xy);
  o.vtUV.z = vtUV.z;
  o.pack0.zw = vtUV.xy;
  return o;
}
[UNITY_domain("tri")]
inline v2f_surf ds_surf (UnityTessellationFactors tessFactors, const OutputPatch<InternalTessInterp_appdata_full,3> vi, float3 bary : SV_DomainLocation) {
  v2f_surf o;
  float4 worldPos =  vi[0].pos*bary.x + vi[1].pos*bary.y + vi[2].pos*bary.z;
  worldPos.y += _HeightScaleOffset.y;
o.screenUV.xyz = vi[0].screenUV*bary.x + vi[1].screenUV*bary.y + vi[2].screenUV*bary.z;
float4 pack0 = vi[0].pack0*bary.x + vi[1].pack0*bary.y + vi[2].pack0*bary.z;
o.vtUV =  vi[0].vtUV.z;
float2 absoluteUV = vi[0].absoluteUV * bary.x + vi[1].absoluteUV * bary.y +vi[2].absoluteUV * bary.z;
float3 uvs[4];
float2 weight;
GetBilinearVirtualTextureUV(_MaskIndexMap,_MaskIndexMap_TexelSize, _HeightScaleOffset.zw, absoluteUV, _VirtualHeightmap_TexelSize, uvs, weight);
  worldPos.y += SampleHeight(uvs, weight) * _HeightScaleOffset.x;
  float2 uvNextOne = pack0.xy > 0.999 ? 1 : 0;
 float3 dispVtUV = GetVirtualTextureUV(_TerrainVTIndexTex, _TerrainVTIndexTex_TexelSize, vi[0].vtUV.xy + uvNextOne, saturate(pack0.xy - uvNextOne));
worldPos.y += _VirtualDisplacement.SampleLevel(sampler_VirtualDisplacement, dispVtUV, 0) * _TessellationFactors.w;
o.pos= mul(UNITY_MATRIX_VP, worldPos);
o.worldPos.xyz = worldPos.xyz;
o.screenUV.w = pack0.z;
o.worldPos.w = pack0.w;
return o;
}

void frag_surf (v2f_surf IN,
		out float4 outGBuffer0 : SV_Target0,
    out float4 outGBuffer1 : SV_Target1,
    out float4 outGBuffer2 : SV_Target2,
    out float4 outEmission : SV_Target3,
	out float2 outMotionVector : SV_TARGET4
) {
	
  // prepare and unpack data
	float depth = IN.pos.z;
	float linearEye = LinearEyeDepth(depth);
	float2 screenUV = IN.screenUV.xy / IN.screenUV.z;
  float3 worldPos = IN.worldPos;
  float4 nonJitterScreenUV = ComputeScreenPos(mul(_NonJitterVP, float4(worldPos, 1)));
  nonJitterScreenUV.xy /= nonJitterScreenUV.w;
  float4 lastClip = ComputeScreenPos(mul(_LastVp, float4(worldPos, 1)));
  lastClip.xy /= lastClip.w;
  float4 velocity = float4(nonJitterScreenUV.xy, lastClip.xy);
	#if UNITY_UV_STARTS_AT_TOP
				outMotionVector = velocity.xw - velocity.zy;
#else
				outMotionVector =  velocity.xy - velocity.zw;
#endif
  float3 worldViewDir = normalize(_WorldSpaceCameraPos - worldPos.xyz);
  SurfaceOutputStandardSpecular o;
  surf (float2(IN.screenUV.w, IN.worldPos.w), IN.vtUV, o);
  //TODO
  //Get TBN From height
  o.Normal = o.Normal.xzy;
  
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


struct InternalTessInterp_appdata_shadow {
  float4 pos : INTERNALTESSPOS;
  float2 pack0 : TEXCOORD0; 
  float2 normalizePos : TEXCOORD1;
  nointerpolation uint2 vtUV : TEXCOORD2;
  float2 absoluteUV : TEXCOORD3;
};

struct v2f_shadow {
  UNITY_POSITION(pos);
};

InternalTessInterp_appdata_shadow tessvert_shadow (uint vertexID : SV_VERTEXID) 
{
	Terrain_Appdata v = GetTerrain(vertexID);
  	InternalTessInterp_appdata_shadow o;
  	o.pack0 = v.uv;
    o.vtUV = v.vtUV;
    o.absoluteUV = (v.uv + v.indexCoord) / _StartPos.w;
    o.normalizePos = v.normalizePos;
  	o.pos = float4(v.position.x, 0, v.position.y, 1);
  	return o;
}


inline UnityTessellationFactors hsconst_shadow (InputPatch<InternalTessInterp_appdata_shadow,3> v) {
  UnityTessellationFactors o;
  float2 pos = v[0].normalizePos + v[1].normalizePos + v[2].normalizePos;
  pos *= 0.333333333;
  float3 tess = tessDist(v[0].pos.xz, v[1].pos.xz, v[2].pos.xz, _TessellationFactors.x, _TessellationFactors.y, SHADOW_TESS_COUNT);//9
  tess *= _CullingTexture.SampleLevel(sampler_CullingTexture, pos, 0);
  o.edge[0] = tess.x;
  o.edge[1] = tess.y;
  o.edge[2] = tess.z;
  o.inside = dot(tess, 0.333333333);

  return o;
}

[UNITY_domain("tri")]
[UNITY_partitioning("fractional_odd")]
[UNITY_outputtopology("triangle_cw")]
[UNITY_patchconstantfunc("hsconst_shadow")]
[UNITY_outputcontrolpoints(3)]
inline InternalTessInterp_appdata_shadow hs_shadow (InputPatch<InternalTessInterp_appdata_shadow,3> v, uint id : SV_OutputControlPointID) {
  return v[id];
}

[UNITY_domain("tri")]
inline v2f_shadow ds_shadow (UnityTessellationFactors tessFactors, const OutputPatch<InternalTessInterp_appdata_shadow,3> vi, float3 bary : SV_DomainLocation) {
  v2f_shadow o;
  float4 worldPos =  vi[0].pos*bary.x + vi[1].pos*bary.y + vi[2].pos*bary.z;
  worldPos.y += _HeightScaleOffset.y;
float2 absoluteUV = vi[0].absoluteUV * bary.x + vi[1].absoluteUV * bary.y +vi[2].absoluteUV * bary.z;
float3 uvs[4];
float2 weight;
GetBilinearVirtualTextureUV(_MaskIndexMap,_MaskIndexMap_TexelSize, _HeightScaleOffset.zw, absoluteUV, _VirtualHeightmap_TexelSize, uvs, weight);
  worldPos.y += SampleHeight(uvs, weight) * _HeightScaleOffset.x;
  float2 pack0 = vi[0].pack0*bary.x + vi[1].pack0*bary.y + vi[2].pack0*bary.z;
  float2 uvNextOne = pack0 > 0.999 ? 1 : 0;
  float3 dispVtUV = GetVirtualTextureUV(_TerrainVTIndexTex, _TerrainVTIndexTex_TexelSize, vi[0].vtUV + uvNextOne, saturate(pack0 - uvNextOne));
worldPos.y += _VirtualDisplacement.SampleLevel(sampler_VirtualDisplacement, dispVtUV, 0) * _TessellationFactors.w;
o.pos= mul(_ShadowMapVP, worldPos);
return o;
}
			
			float frag_shadow (v2f_shadow i)  : SV_TARGET
			{
				return i.pos.z;
			}
#endif