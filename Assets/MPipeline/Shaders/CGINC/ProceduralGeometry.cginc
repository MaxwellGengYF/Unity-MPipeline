#ifndef PROCEDURAL_GEOMETRY
#define PROCEDURAL_GEOMETRY
#define LIT_ENABLE
#define UNITY_PASS_DEFERRED
#define DEFAULT_LIT
#include "UnityDeferredLibrary.cginc"
#include "UnityPBSLighting.cginc"
#include "VoxelLight.cginc"
#include "Shader_Include/Common.hlsl"
#include "Shader_Include/BSDF_Library.hlsl"
#include "Shader_Include/AreaLight.hlsl"
#include "Sunlight.cginc"
#include "Lighting.cginc"
#include "UnityStandardUtils.cginc"
#include "DecalShading.cginc"
#include "Shader_Include/ImageBasedLighting.hlsl"

StructuredBuffer<MaterialProperties> _MaterialBuffer;
Texture2DArray<float4> _GPURPMainTex; SamplerState sampler_GPURPMainTex;
Texture2DArray<float4> _GPURPEmissionMap; SamplerState sampler_GPURPEmissionMap;
Texture2DArray<float4> _GPURPHeightMap; SamplerState sampler_GPURPHeightMap;

float4 SampleTex(Texture2DArray<float4> tex, SamplerState samp, float2 uv, int index, float4 defaultValue)
{
	[branch]
	if(index < 0) return defaultValue;
	return tex.Sample(samp, float3(uv, index));
}

float4 SampleTexNoCheck(Texture2DArray<float4> tex, SamplerState samp, float2 uv, int index)
{
	return tex.Sample(samp, float3(uv, index));
}

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
			struct v2f
			{
				float4 vertex : SV_POSITION;
				float2 uv : TEXCOORD0; 
                float4 worldTangent : TEXCOORD1;
                float4 worldBinormal : TEXCOORD2;
                float4 worldNormal : TEXCOORD3;
	            float3 screenUV : TEXCOORD4;
                nointerpolation uint materialID : TEXCOORD5;
			};

			v2f vert (uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
			{
				uint materialID;
				Point v = getVertexWithMat(vertexID, instanceID, /*out*/materialID); 
				float4 worldPos = float4(v.vertex, 1);
				v2f o;
				o.vertex = mul(UNITY_MATRIX_VP, worldPos);
				o.worldTangent = float4( v.tangent.xyz, worldPos.x);
                o.worldNormal = float4(v.normal, worldPos.z);
                o.worldBinormal = float4(cross(o.worldNormal.xyz, o.worldTangent.xyz) * v.tangent.w, worldPos.y);
                o.screenUV = ComputeScreenPos(o.vertex).xyw;
                o.uv = v.uv0;
                o.materialID = materialID;
				return o;
			}
			
uint _EnableDecal;
sampler2D _PreIntDefault;
float4x4 _NonJitterVP;
float4x4 _LastVp;
float3 ProcessNormal(float2 n)
{
	 return float3(n, sqrt(1 - dot(n,n)));
}
void frag (v2f IN,
		out float4 outGBuffer0 : SV_Target0,
    out float4 outGBuffer1 : SV_Target1,
    out float4 outGBuffer2 : SV_Target2,
    out float4 outEmission : SV_Target3,
	out float2 outMotionVector : SV_TARGET4
) {
	/*outGBuffer0 = 0;
	outGBuffer1 = 0;
	outGBuffer2 = float4(0,0,1,0);
	//float3 col = frac(sin(IN.materialID * float3(13.7621,38.164,41.9871) + float3(64.138,1793,43.168)) * float3(4725.1638, 2346.168126, 7964.23975));
	float3 col = (float)(499 - IN.materialID) * 0.1;
	outEmission =  float4(col, 1);
	return;*/
	  // prepare and unpack data
	float depth = IN.vertex.z;
	float linearEye = LinearEyeDepth(depth);
	float2 screenUV = IN.screenUV.xy / IN.screenUV.z;
  float3 worldPos = float3(IN.worldTangent.w, IN.worldBinormal.w, IN.worldNormal.w);
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
  MaterialProperties matProp = _MaterialBuffer[IN.materialID];
   IN.worldTangent.xyz = normalize(IN.worldTangent.xyz);
  IN.worldBinormal.xyz =  normalize(IN.worldBinormal.xyz);
  IN.worldNormal.xyz = normalize(IN.worldNormal.xyz);
  float3x3 wdMatrixNormalized = float3x3(IN.worldTangent.xyz, IN.worldBinormal.xyz, IN.worldNormal.xyz);
  float3x3 wdMatrix= float3x3((IN.worldTangent.xyz) * matProp._NormalIntensity.x, (IN.worldBinormal.xyz)* matProp._NormalIntensity.y, (IN.worldNormal.xyz));//TODO
  uint decLayer = matProp._DecalLayer * _EnableDecal;
  ///////////Surface Shader
			float2 originUv = IN.uv;
			float2 uv = originUv * matProp._TileOffset.xy + matProp._TileOffset.zw;
			float3 tangentViewDir = normalize(mul(wdMatrixNormalized, worldViewDir));
			float height = SampleTex(_GPURPHeightMap, sampler_GPURPHeightMap, uv, matProp._HeightMap, 0).x;
			uv += ParallaxOffset(height, matProp._HeightMapIntensity, tangentViewDir);

			float4 spec = SampleTex(_GPURPMainTex, sampler_GPURPMainTex,uv, matProp._SpecularMap, 1);
			float4 c =  SampleTex (_GPURPMainTex, sampler_GPURPMainTex, uv, matProp._MainTex, 1);
			c.xyz = pow(c.xyz, 2.2);
			o.Normal = UnpackNormal(SampleTex(_GPURPMainTex, sampler_GPURPMainTex, uv, matProp._BumpMap, 0));
			if(matProp._SecondaryMainTex >= 0){
				float2 secUV = originUv * matProp._SecondaryTileOffset.xy + matProp._SecondaryTileOffset.zw;
				float4 secondCol = SampleTexNoCheck(_GPURPMainTex,sampler_GPURPMainTex, secUV, matProp._SecondaryMainTex);
				secondCol.xyz = pow(secondCol.xyz, 2.2);
				c.xyz = lerp(c.xyz, secondCol.xyz, secondCol.w);
				o.Normal = lerp(o.Normal, UnpackNormal(SampleTex(_GPURPMainTex,sampler_GPURPMainTex, secUV, matProp._SecondaryBumpMap, float4(0,0,1,1))), secondCol.w);
				spec.xyz = lerp(spec.xyz, SampleTex(_GPURPMainTex,sampler_GPURPMainTex, secUV, matProp._SecondarySpecularMap, 1).xyz, secondCol.w);
				o.Emission = matProp._EmissionColor;
			}
			else{
				o.Emission =matProp._EmissionColor * SampleTex(_GPURPEmissionMap, sampler_GPURPEmissionMap, uv, matProp._EmissionMap, 1);
			}
			o.Albedo = c.rgb;

			o.Albedo *= matProp._Color.rgb;
			o.Alpha = 1;
			o.Occlusion = lerp(1, spec.b, matProp._Occlusion);
			float metallic = matProp._MetallicIntensity * spec.g;
			o.Specular = lerp(matProp._SpecularIntensity, o.Albedo, metallic); 
			o.Albedo *= lerp(1 - matProp._SpecularIntensity, 0, metallic);
			o.Smoothness = matProp._Glossiness * spec.r;

			
  ///////////
  [branch]
  if(decLayer != 0)
  	CalculateDecal(screenUV, decLayer, worldPos, height, o.Albedo, o.Normal, o.Specular, o.Smoothness, o.Occlusion);
  o.Normal = normalize(mul(o.Normal, wdMatrix));
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
#if CLEARCOAT_LIT
					buffer.ClearCoat_MultiScatterEnergy = multiScatter;
					buffer.ClearCoat = _ClearCoat;
					buffer.ClearCoat_Roughness = clamp(1 - _ClearCoatSmoothness, 0.02, 1);
#endif
#if SKIN_LIT
					buffer.Skin_Roughness = clamp(1 - _ClearCoatSmoothness, 0.02, 1);
#endif
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

#endif