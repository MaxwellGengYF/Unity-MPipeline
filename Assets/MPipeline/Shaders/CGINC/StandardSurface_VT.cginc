#ifndef __STANDARDSURFACE_INCLUDE__
#define __STANDARDSURFACE_INCLUDE__
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
    float4 _EmissionColor;
		float4 _TileOffset;
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
}

		Texture2DArray<float4> _BumpMap; SamplerState sampler_BumpMap;
		Texture2DArray<float4> _SpecularMap; SamplerState sampler_SpecularMap;
		Texture2DArray<float4> _MainTex;  SamplerState sampler_MainTex;
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
			float4 spec = _SpecularMap.Sample(sampler_SpecularMap ,uv);
			float4 c =  _MainTex.Sample(sampler_MainTex, uv);
			o.Normal = ProcessNormal( _BumpMap.SampleLevel(sampler_BumpMap, uv));
			#ifdef USE_SECONDARY_MAP
			float2 secUV = originUv * _SecondaryTileOffset.xy + _SecondaryTileOffset.zw;
			float4 secondCol = tex2D(_SecondaryMainTex, secUV); 	
			c.xyz = lerp(c.xyz, secondCol.xyz, secondCol.w);
			o.Normal = lerp(o.Normal, ProcessNormal(tex2D(_SecondaryBumpMap, secUV)), secondCol.w);
			spec.xyz = lerp(spec.xyz, tex2D(_SecondarySpecularMap, secUV).xyz, secondCol.w);
			#endif
			o.Albedo = c.rgb;
			o.Alpha = 1;
			o.Occlusion = spec.b;
			float metallic =  spec.g;
			o.Specular = lerp(0.04, o.Albedo, metallic); 
			o.Albedo *= lerp(1 - 0.04, 0, metallic);
			o.Smoothness = _Glossiness * spec.r;
			#else
			o = (SurfaceOutputStandardSpecular)0;
#endif
#ifdef USE_SECONDARY_MAP
			o.Emission = _EmissionColor;
#else
			o.Emission = _EmissionColor * tex2D(_EmissionMap, uv) * _EmissionMultiplier;
#endif
			return height;
		}

#ifdef TESSELLATION_SHADER
void VertexOffset(inout float4 vertex, float3 normal, float2 uv)
{
	vertex.xyz += _HeightMap.SampleLevel(sampler_HeightMap, uv * _TileOffset.xy + _TileOffset.zw, 0) * normal * _HeightmapIntensity;
}
#endif
#endif