Shader "Hidden/Lighting"
{
    SubShader
    {
        CGINCLUDE
        #pragma target 5.0

	#include "UnityCG.cginc"
	#include "UnityPBSLighting.cginc"
	#include "CGINC/VoxelLight.cginc"
	#include "CGINC/Shader_Include/Common.hlsl"
	#include "CGINC/Random.cginc"
	#include "CGINC/Shader_Include/BSDF_Library.hlsl"
	#include "CGINC/Shader_Include/AreaLight.hlsl"
	#include "CGINC/Lighting.cginc"
	#include "CGINC/Sunlight.cginc"
	#pragma multi_compile _ ENABLE_SUN
	#pragma multi_compile _ ENABLE_SUNSHADOW
	#pragma multi_compile _ POINTLIGHT
	#pragma multi_compile _ SPOTLIGHT
    #pragma multi_compile _ EnableGTAO
			float4x4 _InvVP;
			
			Texture2D<float4> _CameraGBufferTexture0; SamplerState sampler_CameraGBufferTexture0;
			Texture2D<float4> _CameraGBufferTexture1; SamplerState sampler_CameraGBufferTexture1;
			Texture2D<float4> _CameraGBufferTexture2; SamplerState sampler_CameraGBufferTexture2;
			Texture2D<float> _CameraDepthTexture; SamplerState sampler_CameraDepthTexture;
            Texture2D<float2> _AOROTexture; SamplerState sampler_AOROTexture;
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

        ENDCG
        Pass
        {
            Stencil
            {
                Ref 0
                Comp Less
                Pass keep
                ReadMask 3
            }
            Cull Off ZWrite Off ZTest Greater
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            Texture2D<float4> _MainTex; SamplerState sampler_MainTex;
            float4 frag (v2f i) : SV_Target
            {
                float4 v = _MainTex.Sample(sampler_MainTex, i.uv);
                return float4(v.xyz * _CameraGBufferTexture0.Sample(sampler_CameraGBufferTexture0, i.uv).xyz, v.w);
            }
            ENDCG
        }

        Pass
        {
            Stencil
            {
                Ref 0
                Comp Equal
                Pass keep
                ReadMask 3
            }
            Cull Off ZWrite Off ZTest Greater
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            Texture2D<float4> _MainTex; SamplerState sampler_MainTex;
            float4 frag (v2f i) : SV_Target
            {
                float4 v = _MainTex.Sample(sampler_MainTex, i.uv);
                return v;
            }
            ENDCG
        }

        Pass
        {
            Stencil
            {
                Ref 1
                Comp Equal
                Pass keep
                ReadMask 3
            }
            Cull Off ZWrite Off ZTest Greater
            Blend one one
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
	
            float4 frag (v2f i) : SV_Target
            {
                float4 albedo = _CameraGBufferTexture0.Sample(sampler_CameraGBufferTexture0, i.uv);
                float4 specular = _CameraGBufferTexture1.Sample(sampler_CameraGBufferTexture1, i.uv);
                float3 normal = normalize(_CameraGBufferTexture2.Sample(sampler_CameraGBufferTexture2, i.uv).xyz * 2 - 1);
                float depth = _CameraDepthTexture.Sample(sampler_CameraDepthTexture, i.uv);
                float3 color = 0;
                float4 worldPos = mul(_InvVP, float4(i.uv * 2 - 1, depth, 1));
                worldPos /= worldPos.w;
                UnityStandardData standardData;
	            standardData.occlusion = albedo.a;
	            standardData.diffuseColor = albedo.rgb;
	            standardData.specularColor = specular.rgb;
	            standardData.smoothness = specular.a;
	            standardData.normalWorld = normal;
                float3 worldViewDir = normalize(worldPos.xyz - _WorldSpaceCameraPos);
	            #if ENABLE_SUN
					#if ENABLE_SUNSHADOW
					color +=max(0,  CalculateSunLight(standardData, depth, worldPos, worldViewDir));
					#else
					color +=max(0,  CalculateSunLight_NoShadow(standardData, worldViewDir));
					#endif
					#endif

					float Roughness = clamp(1 - standardData.smoothness, 0.02, 1);

					#if SPOTLIGHT || POINTLIGHT
                    float linearEye = LinearEyeDepth(depth);
                    color += max(0, CalculateLocalLight(i.uv, worldPos, linearEye, standardData.diffuseColor, normal, specular, Roughness, -worldViewDir));
                    
				//	color += max(0, CalculateTileLight(i.uv, worldPos, standardData.diffuseColor, normal, specular, Roughness, -worldViewDir));
					#endif
                    return float4(color, 0);
            }
            ENDCG
        }

    }
}
