Shader "Hidden/ShadowMask_GPURP"
{
	SubShader
	{

CGINCLUDE
#pragma target 5.0
#define _CameraDepthTexture __
#include "UnityCG.cginc"
#include "UnityDeferredLibrary.cginc"
#include "UnityPBSLighting.cginc"
#include "UnityStandardUtils.cginc"
#include "UnityGBuffer.cginc"
#include "UnityStandardBRDF.cginc"
#include "CGINC/Sunlight.cginc"
#undef _CameraDepthTexture
#define UNITY_PASS_DEFERRED
			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};
			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = v.vertex;
				o.uv = v.uv;
				return o;
			}
			
			float4x4 _InvNonJitterVP;
			
			Texture2D<float4> _CameraGBufferTexture0; SamplerState sampler_CameraGBufferTexture0;
			Texture2D<float4> _CameraGBufferTexture1; SamplerState sampler_CameraGBufferTexture1;
			Texture2D<float4> _CameraGBufferTexture2; SamplerState sampler_CameraGBufferTexture2;
			Texture2D<float> _CameraDepthTexture; SamplerState sampler_CameraDepthTexture;
			
ENDCG

		Pass
		{
		Cull Off ZWrite Off ZTest Greater
		Blend one one
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			float4 frag (v2f i) : SV_Target
			{
				float4 gbuffer0 = _CameraGBufferTexture0.Sample(sampler_CameraGBufferTexture0, i.uv);
    			float4 gbuffer1 = _CameraGBufferTexture1.Sample(sampler_CameraGBufferTexture1, i.uv);
    			float4 gbuffer2 = _CameraGBufferTexture2.Sample(sampler_CameraGBufferTexture2, i.uv);
				float depth = _CameraDepthTexture.Sample(sampler_CameraDepthTexture, i.uv);
				float4 wpos = mul(_InvNonJitterVP, float4(i.uv * 2 - 1, depth, 1));
				wpos /= wpos.w;
				UnityStandardData data = UnityStandardDataFromGbuffer(gbuffer0, gbuffer1, gbuffer2);
				float3 viewDir = normalize(wpos.xyz - _WorldSpaceCameraPos);
				return CalculateSunLight(data, depth, wpos, viewDir);
			}
			ENDCG
		}

		Pass
		{
		Cull Off ZWrite Off ZTest Greater
		Blend one one
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			float4 frag (v2f i) : SV_Target
			{
				float4 gbuffer0 = _CameraGBufferTexture0.Sample(sampler_CameraGBufferTexture0, i.uv);
    			float4 gbuffer1 = _CameraGBufferTexture1.Sample(sampler_CameraGBufferTexture1, i.uv);
    			float4 gbuffer2 = _CameraGBufferTexture2.Sample(sampler_CameraGBufferTexture2, i.uv);
				float depth = _CameraDepthTexture.Sample(sampler_CameraDepthTexture, i.uv);
				float4 wpos = mul(_InvNonJitterVP, float4(i.uv * 2 - 1, depth, 1));
				wpos /= wpos.w;
				float3 viewDir = normalize(wpos.xyz - _WorldSpaceCameraPos);
				UnityStandardData data = UnityStandardDataFromGbuffer(gbuffer0, gbuffer1, gbuffer2);
				return CalculateSunLight_NoShadow(data, viewDir);
							}
			ENDCG
		}
	}
}
