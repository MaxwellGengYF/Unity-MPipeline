#ifndef __MPIPEDEFERRED_INCLUDE__
// Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it uses non-square matrices
#pragma exclude_renderers gles
#define __MPIPEDEFERRED_INCLUDE__
#define GETUV(uv) (uv * _TileOffset.xy + _TileOffset.zw)
#define UNITY_PASS_DEFERRED
#include "UnityStandardUtils.cginc"
#include "Lighting.cginc"
#include "DecalShading.cginc"
#include "Shader_Include/ImageBasedLighting.hlsl"
#ifdef TESSELLATION_SHADER
static const float _Phong = 0.5;
#ifdef UNITY_CAN_COMPILE_TESSELLATION
struct UnityTessellationFactors {
    float edge[3] : SV_TessFactor;
    float inside : SV_InsideTessFactor;
};

#endif // UNITY_CAN_COMPILE_TESSELLATION
#endif // TESSELLATION_SHADER
#define GetScreenPos(pos) ((float2(pos.x, pos.y) * 0.5) / pos.w + 0.5)

float4 ProceduralStandardSpecular_Deferred (inout SurfaceOutputStandardSpecular s, out float4 outGBuffer0, out float4 outGBuffer1, out float4 outGBuffer2)
{
    // RT0: diffuse color (rgb), occlusion (a) - sRGB rendertarget
    outGBuffer0 = float4(s.Albedo, s.Occlusion);

    // RT1: spec color (rgb), smoothness (a) - sRGB rendertarget
    outGBuffer1 = float4(s.Specular, s.Smoothness);

    // RT2: normal (rgb), --unused, very low precision-- (a)

    outGBuffer2 = float4(s.Normal * 0.5f + 0.5f, 1);


		float4 emission = float4(s.Emission, 1);
    return emission;
}
#ifdef TESSELLATION_SHADER
inline float3 UnityCalcTriEdgeTessFactors (float3 triVertexFactors)
{
    float3 tess;
    tess.x = 0.5 * (triVertexFactors.y + triVertexFactors.z);
    tess.y = 0.5 * (triVertexFactors.x + triVertexFactors.z);
    tess.z = 0.5 * (triVertexFactors.x + triVertexFactors.y);
    return tess;
}


inline float UnityCalcDistanceTessFactor (float3 wpos, float minDist, float maxDist, float tess)
{
    float dist = distance (wpos, _WorldSpaceCameraPos);
    float f = clamp(1.0 - (dist - minDist) / (maxDist - minDist), 0.01, 1.0) * tess;
    return f;
}

inline float3 tessDist (float3 v0, float3 v1, float3 v2)
{
    float3 f;
    f.x = UnityCalcDistanceTessFactor (v0,_MinDist,_MaxDist,_Tessellation);
    f.y = UnityCalcDistanceTessFactor (v1,_MinDist,_MaxDist,_Tessellation);
    f.z = UnityCalcDistanceTessFactor (v2,_MinDist,_MaxDist,_Tessellation);
   	return UnityCalcTriEdgeTessFactors (f);

}

#endif // TESSELLATION_SHADER
float4x4 _LastVp;
float4x4 _NonJitterVP;
float3 _SceneOffset;
///////////////
//Geometry Pass
///////////////
#ifdef TESSELLATION_SHADER
struct InternalTessInterp_appdata_full {
  float4 vertex : INTERNALTESSPOS;
  float4 tangent : TANGENT;
  float3 normal : NORMAL;
  float2 texcoord : TEXCOORD0;
  #if LIGHTMAP_ON
  float2 lightmapUV : TEXCOORD1;
  #else
  #ifdef USE_UV2
  float2 lightmapUV : TEXCOORD1;
  #endif
  #endif
  #ifdef USE_UV4
  float2 uv4 : TEXCOORD3;
  #endif
};
#endif // TESSELLATION_SHADER
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
	#ifdef USE_UV4
	float2 uv4 : TEXCOORD7;
	#endif
	#ifdef USE_UV2
  	float2 uv2 : TEXCOORD4;
  	#endif
	#ifdef USE_MOTIONVECTOR
	float3 lastScreenPos : TEXCOORD8;
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
	#else
	#ifdef USE_UV2
	float2 lightmapUV : TEXCOORD1;
	#endif	
	#endif
	#ifdef USE_UV4
  	float2 uv4 : TEXCOORD3;
  	#endif

};



StructuredBuffer<float3x4> _LastFrameModel;
uint _OffsetIndex;
#ifdef TESSELLATION_SHADER
inline InternalTessInterp_appdata_full tessvert_surf (appdata v) {
  InternalTessInterp_appdata_full o;
  o.vertex = v.vertex;
  o.tangent = v.tangent;
  o.normal = v.normal;
  o.texcoord = v.texcoord;
  #if LIGHTMAP_ON
  o.lightmapUV = v.lightmapUV;
  #else
  #ifdef USE_UV2
  o.lightmapUV = v.lightmapUV;
  #endif
  #endif
  #ifdef USE_UV4
  o.uv4 = v.uv4;
  #endif
  return o;
}

inline UnityTessellationFactors hsconst_surf (InputPatch<InternalTessInterp_appdata_full,3> v) {
  UnityTessellationFactors o;
  float3 worldPos0 = mul(unity_ObjectToWorld, v[0].vertex);
  float3 worldPos1 = mul(unity_ObjectToWorld, v[1].vertex);
  float3 worldPos2 = mul(unity_ObjectToWorld, v[2].vertex);
  float3 triangleNormal = cross(worldPos1 - worldPos0, worldPos2 - worldPos0);

  float3 tf = tessDist(worldPos0, worldPos1, worldPos2);
    #ifdef USE_UV4
  tf = clamp(tf * float3(v[0].uv4.x, v[1].uv4.x, v[2].uv4.x), 1, 64);
  #endif
  tf = (dot(_WorldSpaceCameraPos - worldPos0, triangleNormal) > -1e-4) ? tf : 0;
  o.edge[0] = tf.x;
  o.edge[1] = tf.y;
  o.edge[2] = tf.z;
  o.inside = dot(tf, 0.3333333);

  return o;
}

[UNITY_domain("tri")]
[UNITY_partitioning("fractional_odd")]
[UNITY_outputtopology("triangle_cw")]
[UNITY_patchconstantfunc("hsconst_surf")]
[UNITY_outputcontrolpoints(3)]
inline InternalTessInterp_appdata_full hs_surf (InputPatch<InternalTessInterp_appdata_full,3> v, uint id : SV_OutputControlPointID) {
  return v[id];
}
#endif // TESSELLATION_SHADER
v2f_surf vert_surf (appdata v) 
{
  	v2f_surf o;
  	o.pack0 = v.texcoord;
	  #ifdef USE_UV4
	  o.uv4 = v.uv4;
	  #endif
	  #ifdef USE_UV2
	  o.uv2 = v.lightmapUV;
	  #endif
/*		#if UNITY_UV_STARTS_AT_TOP
		o.pos.y = -o.pos.y;
		#endif*/
	  float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
	   o.pos = mul(UNITY_MATRIX_VP, worldPos);
		v.tangent.xyz = mul((float3x3)unity_ObjectToWorld, v.tangent.xyz);
  	o.worldTangent = float4( v.tangent.xyz, worldPos.x);
	  v.normal = mul((float3x3)unity_ObjectToWorld, v.normal);
		o.worldNormal =float4(v.normal, worldPos.z);
  	o.worldBinormal = float4(cross(v.normal, o.worldTangent.xyz) * v.tangent.w, worldPos.y);
	  o.screenUV = ComputeScreenPos(o.pos).xyw;
		#if LIGHTMAP_ON 
		o.lightmapUV = v.lightmapUV * unity_LightmapST.xy + unity_LightmapST.zw;
		#endif
		  #ifdef USE_MOTIONVECTOR
  float4 lastWorldPos = float4(mul(_LastFrameModel[_OffsetIndex], v.vertex), 1);
  lastWorldPos.xyz += _SceneOffset;
  o.lastScreenPos = ComputeScreenPos(mul(_LastVp, lastWorldPos)).xyw;
  #endif
  	return o;
}
#ifdef TESSELLATION_SHADER

[UNITY_domain("tri")]
inline v2f_surf ds_surf (UnityTessellationFactors tessFactors, const OutputPatch<InternalTessInterp_appdata_full,3> vi, float3 bary : SV_DomainLocation) {
  appdata v;
v.vertex = vi[0].vertex*bary.x + vi[1].vertex*bary.y + vi[2].vertex*bary.z;
  float3 pp[3];
  pp[0] = v.vertex.xyz - vi[0].normal * (dot(v.vertex.xyz, vi[0].normal) - dot(vi[0].vertex.xyz, vi[0].normal));
  pp[1] = v.vertex.xyz - vi[1].normal * (dot(v.vertex.xyz, vi[1].normal) - dot(vi[1].vertex.xyz, vi[1].normal));
  pp[2] = v.vertex.xyz - vi[2].normal * (dot(v.vertex.xyz, vi[2].normal) - dot(vi[2].vertex.xyz, vi[2].normal));
  v.vertex.xyz = _Phong * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-_Phong) * v.vertex.xyz;

  v.tangent = vi[0].tangent*bary.x + vi[1].tangent*bary.y + vi[2].tangent*bary.z;
  v.normal = vi[0].normal*bary.x + vi[1].normal*bary.y + vi[2].normal*bary.z;
  v.texcoord = vi[0].texcoord*bary.x + vi[1].texcoord*bary.y + vi[2].texcoord*bary.z;
  #if LIGHTMAP_ON
  v.lightmapUV = vi[0].lightmapUV*bary.x + vi[1].lightmapUV*bary.y + vi[2].lightmapUV*bary.z;
  #else
  #ifdef USE_UV2
  v.lightmapUV = vi[0].lightmapUV*bary.x + vi[1].lightmapUV*bary.y + vi[2].lightmapUV*bary.z;
  #endif
  #endif
  #ifdef USE_UV4
  v.uv4 = vi[0].uv4*bary.x + vi[1].uv4*bary.y + vi[2].uv4*bary.z;
  #endif
  VertexOffset(v.vertex, v.normal, v.texcoord);
  v2f_surf o = vert_surf (v);
  return o;
}
#endif // TESSELLATION_SHADER
uint _EnableDecal;

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
  Input surfIN;
	float2 screenUV = IN.screenUV.xy / IN.screenUV.z;
  surfIN.uv_MainTex = IN.pack0.xy;
  #ifdef USE_UV2
  surfIN.uv2 = IN.uv2;
  #endif
  float3 worldPos = float3(IN.worldTangent.w, IN.worldBinormal.w, IN.worldNormal.w);
  float4 nonJitterScreenUV = ComputeScreenPos(mul(_NonJitterVP, float4(worldPos, 1)));
  nonJitterScreenUV.xy /= nonJitterScreenUV.w;
  #ifdef USE_MOTIONVECTOR
  float2 lastClip = IN.lastScreenPos.xy / IN.lastScreenPos.z;
  #else
  float4 lastClip = ComputeScreenPos(mul(_LastVp, float4(worldPos, 1)));
  lastClip.xy /= lastClip.w;
  #endif
  float4 velocity = float4(nonJitterScreenUV.xy, lastClip.xy);
	#if UNITY_UV_STARTS_AT_TOP
				outMotionVector = velocity.xw - velocity.zy;
#else
				outMotionVector =  velocity.xy - velocity.zw;
#endif
  float3 worldViewDir = normalize(_WorldSpaceCameraPos - worldPos.xyz);
  SurfaceOutputStandardSpecular o;
  IN.worldTangent.xyz = normalize(IN.worldTangent.xyz);
  IN.worldBinormal.xyz =  normalize(IN.worldBinormal.xyz);
  IN.worldNormal.xyz = normalize(IN.worldNormal.xyz);
  float3x3 wdMatrixNormalized = float3x3(IN.worldTangent.xyz, IN.worldBinormal.xyz, IN.worldNormal.xyz);
  float3x3 wdMatrix= float3x3(IN.worldTangent.xyz * _NormalIntensity.x,IN.worldBinormal.xyz * _NormalIntensity.y, IN.worldNormal.xyz);
  surfIN.viewDir = normalize(mul(wdMatrixNormalized, worldViewDir));
  surfIN.worldPos = worldPos.xyz;
  // call surface function
  float height = surf (surfIN, o);
  uint decLayer = _DecalLayer * _EnableDecal;
  [branch]
  if(decLayer != 0)
  	CalculateDecal(screenUV, decLayer, worldPos, height, o.Albedo, o.Normal, o.Specular, o.Smoothness, o.Occlusion);
  o.Normal = normalize(mul(o.Normal, wdMatrix));
  outEmission = ProceduralStandardSpecular_Deferred (o, outGBuffer0, outGBuffer1, outGBuffer2); //GI neccessary here!
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
					[branch]
					if(dot(_LightEnabled.zw, 1) > 0.5){
                    	outEmission.xyz += max(0, CalculateLocalLight(screenUV, float4(worldPos,1 ), linearEye, o.Normal, worldViewDir, buffer));
					}
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


//////////////////
//Motion Vector Pass
//////////////////
#ifdef TESSELLATION_SHADER
			struct tessappdata_mv
			{
				float4 vertex : INTERNALTESSPOS;
				float3 normal : NORMAL;
				float2 texcoord : TEXCOORD0;
				#ifdef USE_UV4
  				float2 uv4 : TEXCOORD3;
  				#endif
			};

			#endif//TESSELLATION_SHADER
				struct appdata_mv
			{
				float4 vertex : POSITION;
				float3 normal : NORMAL;
				float2 texcoord : TEXCOORD0;
				#ifdef USE_UV4
  				float2 uv4 : TEXCOORD3;
  				#endif
			};
			#ifdef TESSELLATION_SHADER
			inline tessappdata_mv tessvert_mv (appdata_mv v) {
				tessappdata_mv o;
				o.vertex = v.vertex;
				o.normal = v.normal;
				o.texcoord = v.texcoord;
				#ifdef USE_UV4
  				o.uv4 = v.uv4;
  				#endif
				return o;
			}

			inline UnityTessellationFactors hsconst_mv (InputPatch<tessappdata_mv,3> v) {
  UnityTessellationFactors o;
  float3 worldPos0 = mul(unity_ObjectToWorld, v[0].vertex);
  float3 worldPos1 = mul(unity_ObjectToWorld, v[1].vertex);
  float3 worldPos2 = mul(unity_ObjectToWorld, v[2].vertex);
  float3 triangleNormal = cross(worldPos1 - worldPos0, worldPos2 - worldPos0);

  float3 tf = tessDist(worldPos0, worldPos1, worldPos2);
    #ifdef USE_UV4
    tf = clamp(tf * float3(v[0].uv4.x, v[1].uv4.x, v[2].uv4.x), 1, 64);
	#endif
	  tf = (dot(_WorldSpaceCameraPos - worldPos0, triangleNormal) > -1e-4) ? tf : 0;
  o.edge[0] = tf.x;
  o.edge[1] = tf.y;
  o.edge[2] = tf.z;
  o.inside = dot(tf, 0.3333333);

  return o;
			}

			[UNITY_domain("tri")]
[UNITY_partitioning("fractional_odd")]
[UNITY_outputtopology("triangle_cw")]
[UNITY_patchconstantfunc("hsconst_mv")]
[UNITY_outputcontrolpoints(3)]
inline tessappdata_mv hs_mv (InputPatch<tessappdata_mv,3> v, uint id : SV_OutputControlPointID) {
  return v[id];
}
#endif //TESSELLATION_SHADER
/////////////
//Shadow pass
/////////////
float4x4 _ShadowMapVP;
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


		

			v2f_shadow vert_shadow (appdata_mv v)
			{
				float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
				v2f_shadow o;
				#if POINT_LIGHT_SHADOW
				o.worldPos = worldPos.xyz;
				#endif
				o.vertex = mul(_ShadowMapVP, worldPos);
				#if CUT_OFF
				o.texcoord = GETUV(v.texcoord);
				#endif
				return o;
			}
#ifdef TESSELLATION_SHADER
							[UNITY_domain("tri")]
inline v2f_shadow ds_shadowmap (UnityTessellationFactors tessFactors, const OutputPatch<tessappdata_mv,3> vi, float3 bary : SV_DomainLocation) {
  appdata_mv v;
v.vertex = vi[0].vertex*bary.x + vi[1].vertex*bary.y + vi[2].vertex*bary.z;
   float3 pp[3];
  pp[0] = v.vertex.xyz - vi[0].normal * (dot(v.vertex.xyz, vi[0].normal) - dot(vi[0].vertex.xyz, vi[0].normal));
  pp[1] = v.vertex.xyz - vi[1].normal * (dot(v.vertex.xyz, vi[1].normal) - dot(vi[1].vertex.xyz, vi[1].normal));
  pp[2] = v.vertex.xyz - vi[2].normal * (dot(v.vertex.xyz, vi[2].normal) - dot(vi[2].vertex.xyz, vi[2].normal));
  v.vertex.xyz = _Phong * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-_Phong) * v.vertex.xyz;
  v.texcoord = vi[0].texcoord*bary.x + vi[1].texcoord*bary.y + vi[2].texcoord*bary.z;
  v.normal = vi[0].normal*bary.x + vi[1].normal*bary.y + vi[2].normal*bary.z;
  VertexOffset(v.vertex, v.normal, v.texcoord);
  v2f_shadow o = vert_shadow (v);
  return o;
}
#endif //TESSELLATION_SHADER
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


////////////
//Depth pass
////////////

			struct v2f_depth
			{
				float4 vertex : SV_POSITION;
				#if CUT_OFF
				float2 texcoord : TEXCOORD0;
				#endif
			};

			v2f_depth vert_depth (appdata_mv v)
			{
				v2f_depth o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				#if CUT_OFF
				o.texcoord = GETUV(v.texcoord);
				#endif
				return o;
			}
			#ifdef TESSELLATION_SHADER
			[UNITY_domain("tri")]
inline v2f_depth ds_depth (UnityTessellationFactors tessFactors, const OutputPatch<tessappdata_mv,3> vi, float3 bary : SV_DomainLocation) {
  appdata_mv v;
v.vertex = vi[0].vertex*bary.x + vi[1].vertex*bary.y + vi[2].vertex*bary.z;
  float3 pp[3];
  pp[0] = v.vertex.xyz - vi[0].normal * (dot(v.vertex.xyz, vi[0].normal) - dot(vi[0].vertex.xyz, vi[0].normal));
  pp[1] = v.vertex.xyz - vi[1].normal * (dot(v.vertex.xyz, vi[1].normal) - dot(vi[1].vertex.xyz, vi[1].normal));
  pp[2] = v.vertex.xyz - vi[2].normal * (dot(v.vertex.xyz, vi[2].normal) - dot(vi[2].vertex.xyz, vi[2].normal));
  v.vertex.xyz = _Phong * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-_Phong) * v.vertex.xyz;
  v.texcoord = vi[0].texcoord*bary.x + vi[1].texcoord*bary.y + vi[2].texcoord*bary.z;
  v.normal = vi[0].normal*bary.x + vi[1].normal*bary.y + vi[2].normal*bary.z;
  VertexOffset(v.vertex, v.normal, v.texcoord);
  v2f_depth o = vert_depth (v);
  return o;
} //TESSELLATION_SHADER
#endif


			#if CUT_OFF
			void frag_depth (v2f_depth i)
			#else
			void frag_depth ()
			#endif
			{
				#if CUT_OFF
				float4 c = tex2D(_MainTex, i.texcoord);
				clip(c.a * _Color.a - _Cutoff);
				#endif
			}
#endif