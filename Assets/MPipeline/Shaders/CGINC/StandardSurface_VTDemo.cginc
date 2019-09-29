#ifndef __STANDARDSURFACE_INCLUDE__
#define __STANDARDSURFACE_INCLUDE__
#include "VirtualTexture.cginc"
	struct Input {
			float2 uv_MainTex;
			float3 viewDir;
			float3 worldPos;
			#ifdef USE_UV2
			float2 uv2;
			#endif
		};
cbuffer UnityPerMaterial
{
    float _SpecularIntensity;
		float _MetallicIntensity;
    float4 _EmissionColor;
		float _Occlusion;
		float4 _TileOffset;
		float _Glossiness;
		float4 _Color;
		float _EmissionMultiplier;
		float _Cutoff;
		float _ClearCoatSmoothness;
		float _ClearCoat;
		#ifdef TESSELLATION_SHADER
		float _MinDist;
		float _MaxDist;
		float _Tessellation;
		#endif
		float _HeightmapIntensity;
		float2 _NormalIntensity;
		int _DecalLayer;
		float4 _SecondaryTileOffset;
}
Texture2DArray<float4> _VirtualAlbedo; SamplerState sampler_VirtualAlbedo;
Texture2DArray<float4> _VirtualNormal; SamplerState sampler_VirtualNormal;
Texture2DArray<float4> _VirtualSmo; SamplerState sampler_VirtualSmo;
		sampler2D _EmissionMap;
		sampler2D _PreIntDefault;
		Texture2D<float> _HeightMap; SamplerState sampler_HeightMap;

float3 ProcessNormal(float4 value)
{
	float z = sqrt(1 - dot(value.xy, value.xy));
	return float3(value.xy, z);
}
		float surf (Input IN, inout SurfaceOutputStandardSpecular o) {
			float2 originUv = IN.uv_MainTex;
			float2 uv = originUv * _TileOffset.xy + _TileOffset.zw;
			float height = _HeightMap.SampleLevel(sampler_HeightMap, uv, 0);
			#ifndef TESSELLATION_SHADER
			uv += ParallaxOffset(height, _HeightmapIntensity,IN.viewDir);
			#endif
#if LIT_ENABLE
			float4 spec = SampleVirtualTextureLevel(_VirtualSmo, sampler_VirtualSmo, floor(uv), frac(uv), 0);
			float4 c =  SampleVirtualTextureLevel(_VirtualAlbedo, sampler_VirtualAlbedo, floor(uv), frac(uv), 0);
			o.Normal =  ProcessNormal( SampleVirtualTextureLevel(_VirtualNormal, sampler_VirtualNormal, floor(uv), frac(uv), 0));
			
			o.Albedo = c.rgb;

			o.Albedo *= _Color.rgb;
			o.Alpha = 1;
			o.Occlusion = lerp(1, spec.b, _Occlusion);
			float metallic =  _MetallicIntensity * spec.g;
			o.Specular = lerp(_SpecularIntensity, o.Albedo, metallic); 
			o.Albedo *= lerp(1 - _SpecularIntensity, 0, metallic);
			o.Smoothness = lerp(spec.r, 1, _Glossiness);
			#else
			o = (SurfaceOutputStandardSpecular)0;
#endif

			o.Emission = _EmissionColor * tex2D(_EmissionMap, uv) * _EmissionMultiplier;

			return height;
		}

#ifdef TESSELLATION_SHADER
void VertexOffset(inout float4 vertex, float3 normal, float2 uv)
{
	vertex.xyz += _HeightMap.SampleLevel(sampler_HeightMap, uv * _TileOffset.xy + _TileOffset.zw, 0) * normal * _HeightmapIntensity;
}
#endif
#endif