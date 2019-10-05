Shader "Unlit/Reflection"
{

CGINCLUDE
#pragma target 5.0
#include "UnityCG.cginc"
#include "CGINC/VoxelLight.cginc"
#include "UnityGBuffer.cginc"
#include "UnityPBSLighting.cginc"
#pragma multi_compile _ UNITY_HDR_ON
#pragma multi_compile _ EnableGTAO
#pragma multi_compile _ ENABLE_SSR
#include "CGINC/Reflection.cginc"


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

            struct v2f_cube
            {
                float3 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };
            
            float4x4 _InvVP;    //Inverse View Project Matrix
			Texture2D<half4> _CameraGBufferTexture0; SamplerState sampler_CameraGBufferTexture0;       //RGB Diffuse A AO
			Texture2D<half4> _CameraGBufferTexture1; SamplerState sampler_CameraGBufferTexture1;       //RGB Specular A Smoothness
			Texture2D<float4> _CameraGBufferTexture2; SamplerState sampler_CameraGBufferTexture2;       //RGB Normal
			Texture2D<float> _CameraDepthTexture; SamplerState sampler_CameraDepthTexture;
			Texture2D<float2> _AOROTexture; SamplerState sampler_AOROTexture;
            int _ReflectionIndex;
            samplerCUBE _ReflectionTex;
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = v.vertex;
                o.uv = v.uv;
                return o;
            }
            v2f_cube vert_cube(appdata v)
            {
                v2f_cube o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = ComputeScreenPos(o.vertex).xyw;
                return o;
            }

            
ENDCG
    SubShader
    {

        Tags { "RenderType"="Opaque" }
        LOD 100
//Pass 0 Regular Projection
        Pass
        {
                    Cull front ZWrite off ZTest Greater
        Blend oneMinusSrcAlpha srcAlpha
        Stencil
        {
            Ref 1
            ReadMask 15
            Pass keep
            Comp LEqual
        }
            CGPROGRAM
            #pragma vertex vert_cube
            #pragma fragment frag
            void frag (v2f_cube i, out float4 reflection : SV_TARGET0, out float4 gi : SV_TARGET1)
            {
                float2 screenUV = i.uv.xy / i.uv.z;
                float depth = _CameraDepthTexture.Sample(sampler_CameraDepthTexture, screenUV);
                float4 worldPos = mul(_InvVP, float4(screenUV * 2 - 1, depth, 1));
                worldPos /= worldPos.w;
                float4 gbuffer1 = _CameraGBufferTexture2.Sample(sampler_CameraGBufferTexture2, screenUV);
                float3 normal = normalize(gbuffer1.xyz * 2 - 1);
                float4 gbuffer0 = _CameraGBufferTexture0.Sample(sampler_CameraGBufferTexture0, screenUV);
				float4 specular = _CameraGBufferTexture1.Sample(sampler_CameraGBufferTexture1, screenUV);

#if EnableGTAO
				float2 aoro = _AOROTexture.Sample(sampler_AOROTexture, screenUV);
				aoro = min(aoro, gbuffer0.ww);
#else
                float2 aoro = gbuffer0.ww;
#endif
                float3 viewDir = normalize(worldPos.xyz - _WorldSpaceCameraPos);
                //float3 CalculateReflection(float linearDepth, float3 worldPos, float3 viewDir, float4 specular, float3 normal, float occlusion)
				reflection = CalculateReflection_Deferred(worldPos.xyz, viewDir, specular, float4(normal, gbuffer1.w), gbuffer0.xyz, aoro, _ReflectionTex, _ReflectionIndex, gi.xyz);
                gi.xyz *= gbuffer1.w;
                gi.w = reflection.w;
            }
            ENDCG
        }
        Pass
        {
                    Cull off ZWrite off ZTest Greater
        Blend one one
        Stencil
        {
            Ref 1
            ReadMask 15
            Pass keep
            Comp LEqual
        }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            sampler2D _CameraReflectionTexture;
            sampler2D _SSR_TemporalCurr_RT;
            float3 frag (v2f i) : SV_Target
            {
                float3 reflect = tex2D(_CameraReflectionTexture, i.uv).xyz;
                float4 gbuffer0 = _CameraGBufferTexture0.Sample(sampler_CameraGBufferTexture0, i.uv);
                #if EnableGTAO
				float2 aoro = _AOROTexture.Sample(sampler_AOROTexture, i.uv);
                #else
                float2 aoro = 1;
                #endif
                aoro = min(aoro, gbuffer0.ww);
                #if ENABLE_SSR
                float4 ssr = tex2D(_SSR_TemporalCurr_RT, i.uv);
                reflect = lerp(reflect, max(0,ssr.rgb * aoro.y), saturate(ssr.a));
                #endif
                return reflect;
            }
            ENDCG
        }

        Pass
        {
                    Cull off ZWrite off ZTest Greater
        Stencil
        {
            Ref 1
            ReadMask 15
            Pass keep
            Comp LEqual
        }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            sampler2D _CameraReflectionTexture;
            sampler2D _CameraGITexture;
            sampler2D _SSR_TemporalCurr_RT;
            void frag (v2f i, out float3 reflection : SV_TARGET0, out float3 gi : SV_TARGET1)
            {
                float4 worldPos = mul(_InvVP, float4(i.uv * 2 - 1, 1, 1));
                worldPos /= worldPos.w;
                float4 gbuffer1 = _CameraGBufferTexture2.Sample(sampler_CameraGBufferTexture2, i.uv);
                float3 normal = normalize(gbuffer1.xyz * 2 - 1);
                float4 gbuffer0 = _CameraGBufferTexture0.Sample(sampler_CameraGBufferTexture0,  i.uv);
				float4 specular = _CameraGBufferTexture1.Sample(sampler_CameraGBufferTexture1,  i.uv);

#if EnableGTAO
				float2 aoro = _AOROTexture.Sample(sampler_AOROTexture,  i.uv);
				aoro = min(aoro, gbuffer0.ww);
#else
                float2 aoro = gbuffer0.ww;
#endif
                float3 viewDir = normalize(worldPos.xyz - _WorldSpaceCameraPos);
                reflection = CalculateReflection_Skybox(viewDir, specular, float4(normal, gbuffer1.w), gbuffer0.xyz, aoro, gi);
                gi *= gbuffer1.w;
            }
            ENDCG
        }

        Pass
        {
                    Cull off ZWrite off ZTest Greater
        Blend one one
        Stencil
        {
            Ref 1
            ReadMask 15
            Pass keep
            Comp LEqual
        }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            sampler2D _CameraGITexture;
            float3 frag (v2f i) : SV_Target
            {
                return tex2D(_CameraGITexture, i.uv).xyz;
            }
            ENDCG
        }
    }
}
