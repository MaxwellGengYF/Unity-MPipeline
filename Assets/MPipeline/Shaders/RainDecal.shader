Shader "Unlit/RainDecal"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            Stencil
            {
                Ref 1
                Comp Equal
                Pass keep
            }
            ZWrite off
            Cull Front
            ZTest Greater
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 5.0

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 screenUV : TEXCOORD0;
            };
            Texture2D<float4> _BackupNormalMap; SamplerState sampler_BackupNormalMap;
            Texture2D<float4> _BackupAlbedoMap; SamplerState sampler_BackupAlbedoMap;
            Texture2D<float> _CameraDepthTexture; SamplerState sampler_CameraDepthTexture;
            Texture2D<float2> _RainTexture; SamplerState sampler_RainTexture;
            float4x4 _InvVP;
            float3 _Color;
            float2 _OpaqueScale;
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.screenUV = ComputeScreenPos(o.vertex);
                return o;
            }

            void frag (v2f i, out float4 albedo : SV_TARGET0, out float4 normal : SV_TARGET1)
            {
                float2 screenUV = i.screenUV.xy / i.screenUV.w;
                float depth = _CameraDepthTexture.Sample(sampler_CameraDepthTexture, screenUV);
                float4 originAlbedo = _BackupAlbedoMap.Sample(sampler_BackupAlbedoMap, screenUV);
                float4 originNormal = _BackupNormalMap.Sample(sampler_BackupNormalMap, screenUV);
                float4 worldPos = mul(_InvVP, float4(screenUV * 2 - 1, depth, 1));
                worldPos /= worldPos.w;
                float3 localPos = mul(unity_WorldToObject, worldPos).xyz;
                localPos += 0.5;
                albedo = originAlbedo;
                if(dot(abs(localPos - saturate(localPos)), 1) > 1e-5)
                {
                    normal = originNormal;
                    return;
                }
                originNormal.xyz = originNormal.xyz * 2 - 1;
                float3 decalNormal;
                decalNormal.xy =  _RainTexture.Sample(sampler_RainTexture, localPos.xz) * _OpaqueScale.y;
                decalNormal.z = sqrt(1 - decalNormal.xy * decalNormal.xy);
                decalNormal = normalize(decalNormal);
                decalNormal = mul((float3x3)unity_ObjectToWorld, decalNormal).xzy;
                albedo.a = originAlbedo.a;
                normal.xyz = normalize(lerp(originNormal.xyz, decalNormal, _OpaqueScale.x)) * 0.5 + 0.5;
                normal.a = originNormal.a;
            }
            ENDCG
        }
    }
}
