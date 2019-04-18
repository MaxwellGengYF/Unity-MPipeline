Shader "Hidden/PostProcessing/Uber"
{
	HLSLINCLUDE

#pragma target 3.0

#pragma multi_compile __ DISTORT
#pragma multi_compile __ CHROMATIC_ABERRATION CHROMATIC_ABERRATION_LOW
#pragma multi_compile __ BLOOM BLOOM_LOW
#pragma multi_compile __ COLOR_GRADING_LDR_2D COLOR_GRADING_HDR_2D COLOR_GRADING_HDR_3D
#pragma multi_compile __ VIGNETTE
#pragma multi_compile __ GRAIN
#pragma multi_compile __ FINALPASS
#pragma multi_compile __ RENDERING_TEXTURE
#pragma multi_compile __ ENABLE_CYBERCOLOR

#include "../StdLib.hlsl"
#include "../Colors.hlsl"
#include "../Sampling.hlsl"
#include "Distortion.hlsl"
#include "Dithering.hlsl"
#include "Cyber.cginc"

#define MAX_CHROMATIC_SAMPLES 16

		TEXTURE2D_SAMPLER2D(_MainTex, sampler_MainTex);
	float4 _MainTex_TexelSize;

	// Auto exposure / eye adaptation
	TEXTURE2D_SAMPLER2D(_AutoExposureTex, sampler_AutoExposureTex);

	// Bloom
	TEXTURE2D_SAMPLER2D(_BloomTex, sampler_BloomTex);
	TEXTURE2D_SAMPLER2D(_Bloom_DirtTex, sampler_Bloom_DirtTex);
	float4 _BloomTex_TexelSize;
	float4 _Bloom_DirtTileOffset; // xy: tiling, zw: offset
	float3 _Bloom_Settings; // x: sampleScale, y: intensity, z: dirt intensity
	float3 _Bloom_Color;

	// Chromatic aberration
	TEXTURE2D_SAMPLER2D(_ChromaticAberration_SpectralLut, sampler_ChromaticAberration_SpectralLut);
	float _ChromaticAberration_Amount;

	TEXTURE3D_SAMPLER3D(_Lut3D, sampler_Lut3D);
	float2 _Lut3D_Params;


	float _PostExposure; // EV (exp2)

	// Vignette
	float3 _Vignette_Color;
	float2 _Vignette_Center; // UV space
	float4 _Vignette_Settings; // x: intensity, y: smoothness, z: roundness, w: rounded
	float _Vignette_Opacity;
	float _Vignette_Mode; // <0.5: procedural, >=0.5: masked
	TEXTURE2D_SAMPLER2D(_Vignette_Mask, sampler_Vignette_Mask);

	// Grain
	TEXTURE2D_SAMPLER2D(_GrainTex, sampler_GrainTex);
	float2 _Grain_Params1; // x: lum_contrib, y: intensity
	float4 _Grain_Params2; // x: xscale, h: yscale, z: xoffset, w: yoffset

	// Misc
	float _LumaInAlpha;
	struct v2f
	{
		float4 vertex : SV_POSITION;
		float2 uv : TEXCOORD0;
	};
	float4 FragUber(v2f i) : SV_Target
	{
		//>>> Automatically skipped by the shader optimizer when not used
		float2 uv = i.uv;
		float2 uvDistorted = Distort(uv);
		//<<<

		float autoExposure = SAMPLE_TEXTURE2D(_AutoExposureTex, sampler_AutoExposureTex, uv).r;
		float4 color = (0.0).xxxx;

		// Inspired by the method described in "Rendering Inside" [Playdead 2016]
		// https://twitter.com/pixelmager/status/717019757766123520
		#if CHROMATIC_ABERRATION
		{
			float2 coords = 2.0 * uv - 1.0;
			float2 end = uv - coords * dot(coords, coords) * _ChromaticAberration_Amount;

			float2 diff = end - uv;
			int samples = clamp(int(length(_MainTex_TexelSize.zw * diff / 2.0)), 3, MAX_CHROMATIC_SAMPLES);
			float2 delta = diff / samples;
			float2 pos = uv;
			float4 sum = (0.0).xxxx, filterSum = (0.0).xxxx;

			for (int i = 0; i < samples; i++)
			{
				float t = (i + 0.5) / samples;
				float4 s = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, UnityStereoTransformScreenSpaceTex(Distort(pos)), 0);
				float4 filter = float4(SAMPLE_TEXTURE2D_LOD(_ChromaticAberration_SpectralLut, sampler_ChromaticAberration_SpectralLut, float2(t, 0.0), 0).rgb, 1.0);

				sum += s * filter;
				filterSum += filter;
				pos += delta;
			}

			color = sum / filterSum;
		}
		#elif CHROMATIC_ABERRATION_LOW
		{
			float2 coords = 2.0 * uv - 1.0;
			float2 end = uv - coords * dot(coords, coords) * _ChromaticAberration_Amount;
			float2 delta = (end - uv) / 3;

			float4 filterA = float4(SAMPLE_TEXTURE2D_LOD(_ChromaticAberration_SpectralLut, sampler_ChromaticAberration_SpectralLut, float2(0.5 / 3, 0.0), 0).rgb, 1.0);
			float4 filterB = float4(SAMPLE_TEXTURE2D_LOD(_ChromaticAberration_SpectralLut, sampler_ChromaticAberration_SpectralLut, float2(1.5 / 3, 0.0), 0).rgb, 1.0);
			float4 filterC = float4(SAMPLE_TEXTURE2D_LOD(_ChromaticAberration_SpectralLut, sampler_ChromaticAberration_SpectralLut, float2(2.5 / 3, 0.0), 0).rgb, 1.0);

			float4 texelA = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, UnityStereoTransformScreenSpaceTex(Distort(uv)), 0);
			float4 texelB = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, UnityStereoTransformScreenSpaceTex(Distort(delta + uv)), 0);
			float4 texelC = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, UnityStereoTransformScreenSpaceTex(Distort(delta * 2.0 + uv)), 0);

			float4 sum = texelA * filterA + texelB * filterB + texelC * filterC;
			float4 filterSum = filterA + filterB + filterC;
			color = sum / filterSum;
		}
		#else
		{
			color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvDistorted);
		}
		#endif

		// Gamma space... Gah.
		#if UNITY_COLORSPACE_GAMMA
		{
			color = SRGBToLinear(color);
		}
		#endif

		color.rgb *= autoExposure;

		#if BLOOM || BLOOM_LOW
		{
			#if BLOOM
			float4 bloom = UpsampleTent(TEXTURE2D_PARAM(_BloomTex, sampler_BloomTex), uvDistorted, _BloomTex_TexelSize.xy, _Bloom_Settings.x);
			#else
			float4 bloom = UpsampleBox(TEXTURE2D_PARAM(_BloomTex, sampler_BloomTex), uvDistorted, _BloomTex_TexelSize.xy, _Bloom_Settings.x);
			#endif

			// UVs should be Distort(uv * _Bloom_DirtTileOffset.xy + _Bloom_DirtTileOffset.zw)
			// but considering we use a cover-style scale on the dirt texture the difference
			// isn't massive so we chose to save a few ALUs here instead in case lens distortion
			// is active
			float4 dirt = float4(SAMPLE_TEXTURE2D(_Bloom_DirtTex, sampler_Bloom_DirtTex, uvDistorted * _Bloom_DirtTileOffset.xy + _Bloom_DirtTileOffset.zw).rgb, 0.0);

			// Additive bloom (artist friendly)
			bloom *= _Bloom_Settings.y;
			dirt *= _Bloom_Settings.z;
			color += bloom * float4(_Bloom_Color, 1.0);
			color += dirt * bloom;
		}
		#endif
		#if VIGNETTE
		{
			UNITY_BRANCH
			if (_Vignette_Mode < 0.5)
			{
				float2 d = abs(uvDistorted - _Vignette_Center) * _Vignette_Settings.x;
				d.x *= lerp(1.0, _ScreenParams.x / _ScreenParams.y, _Vignette_Settings.w);
				d = pow(saturate(d), _Vignette_Settings.z); // Roundness
				float vfactor = pow(saturate(1.0 - dot(d, d)), _Vignette_Settings.y);
				color.rgb *= lerp(_Vignette_Color, (1.0).xxx, vfactor);
				color.a = lerp(1.0, color.a, vfactor);
			}
			else
			{
				float vfactor = SAMPLE_TEXTURE2D(_Vignette_Mask, sampler_Vignette_Mask, uvDistorted).a;

				#if !UNITY_COLORSPACE_GAMMA
				{
					vfactor = SRGBToLinear(vfactor);
				}
				#endif

				float3 new_color = color.rgb * lerp(_Vignette_Color, (1.0).xxx, vfactor);
				color.rgb = lerp(color.rgb, new_color, _Vignette_Opacity);
				color.a = lerp(1.0, color.a, vfactor);
			}
		}
		#endif

		#if GRAIN
		{
			float3 grain = SAMPLE_TEXTURE2D(_GrainTex, sampler_GrainTex, i.uv * _Grain_Params2.xy + _Grain_Params2.zw).rgb;

			// Noisiness response curve based on scene luminance
			float lum = 1.0 - sqrt(Luminance(saturate(color)));
			lum = lerp(1.0, lum, _Grain_Params1.x);

			color.rgb += color.rgb * grain * _Grain_Params1.y * lum;
		}
		#endif

		#if COLOR_GRADING_HDR_3D
		{
			//Color 3D
						color *= _PostExposure;
						float3 colorLutSpace = saturate(LUT_SPACE_ENCODE(color.rgb));
						color.rgb = ApplyLut3D(TEXTURE3D_PARAM(_Lut3D, sampler_Lut3D), colorLutSpace, _Lut3D_Params);
						}
						#elif COLOR_GRADING_HDR_2D
						{
							color *= _PostExposure;
							float3 colorLutSpace = saturate(LUT_SPACE_ENCODE(color.rgb));
							color.rgb = ApplyLut2D(TEXTURE2D_PARAM(_Lut2D, sampler_Lut2D), colorLutSpace, _Lut2D_Params);
						}
						#elif COLOR_GRADING_LDR_2D
						{
							color = saturate(color);

							// LDR Lut lookup needs to be in sRGB - for HDR stick to linear
							color.rgb = LinearToSRGB(color.rgb);
							color.rgb = ApplyLut2D(TEXTURE2D_PARAM(_Lut2D, sampler_Lut2D), color.rgb, _Lut2D_Params);
							color.rgb = SRGBToLinear(color.rgb);
						}
						#endif

						float4 output = color;
						#ifdef ENABLE_CYBERCOLOR
						output.xyz = GetCyberColor(output.xyz);
						#endif
						#if FINALPASS
						{
							#if UNITY_COLORSPACE_GAMMA
							{
								output = LinearToSRGB(output);
							}
							#endif

							output.rgb = Dither(output.rgb, i.uv);
						}
						#else
						{
							UNITY_BRANCH
							if (_LumaInAlpha > 0.5)
							{
								// Put saturated luma in alpha for FXAA - higher quality than "green as luma" and
								// necessary as RGB values will potentially still be HDR for the FXAA pass
								float luma = Luminance(saturate(output));
								output.a = luma;
							}

							#if UNITY_COLORSPACE_GAMMA
							{
								output = LinearToSRGB(output);
							}
							#endif
						}
						#endif
						// Output RGB is still HDR at that point (unless range was crunched by a tonemapper)

						return output;
	}

		ENDHLSL

		SubShader
	{
		Cull Off ZWrite Off ZTest Always

		Pass
		{
			HLSLPROGRAM

				#pragma vertex vert
				#pragma fragment FragUber
				struct appdata
				{
					float4 vertex : POSITION;
					float2 uv : TEXCOORD0;
				};
				v2f vert(appdata v)
				{
					v2f o;
					o.vertex = v.vertex;
					o.uv = v.uv;
					#if UNITY_UV_STARTS_AT_TOP && !RENDERING_TEXTURE
					o.vertex.y = -o.vertex.y;
					#endif
					return o;
				}

			ENDHLSL
		}

	}
}