Shader "Unlit/Decal"
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
            Texture2D<float4> _DecalAlbedo; SamplerState sampler_DecalAlbedo;
            Texture2D<float4> _DecalNormal; SamplerState sampler_DecalNormal;
            Texture2D<float4> _BackupNormalMap; SamplerState sampler_BackupNormalMap;
            Texture2D<float4> _BackupAlbedoMap; SamplerState sampler_BackupAlbedoMap;
            Texture2D<float> _CameraDepthTexture; SamplerState sampler_CameraDepthTexture;
            float4x4 _InvVP;
            float2 _OpaqueScale;
            float3 _Color;
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
                originNormal.xyz = originNormal.xyz * 2 - 1;
                float4 worldPos = mul(_InvVP, float4(screenUV * 2 - 1, depth, 1));
                worldPos /= worldPos.w;
                float3 localPos = mul(unity_WorldToObject, worldPos).xyz;
                localPos += 0.5;
                float outOfRange = dot(abs(localPos - saturate(localPos)), 1) > 1e-5 ? 1 : 0;
                float4 decalAlbedo = _DecalAlbedo.Sample(sampler_DecalAlbedo, localPos.xz);
                decalAlbedo.rgb *= _Color;
                decalAlbedo.a = lerp(decalAlbedo.a * _OpaqueScale.x, 0, outOfRange);
                float3 decalNormal =  normalize(UnpackNormal(_DecalNormal.Sample(sampler_DecalNormal, localPos.xz)));
                decalNormal.xy *= _OpaqueScale.y;
                float3 right = float3(unity_ObjectToWorld[0][0], unity_ObjectToWorld[1][0], unity_ObjectToWorld[2][0]);
                float3 up = float3(unity_ObjectToWorld[0][1], unity_ObjectToWorld[1][1], unity_ObjectToWorld[2][1]);
                float3 forward = float3(unity_ObjectToWorld[0][2], unity_ObjectToWorld[1][2], unity_ObjectToWorld[2][2]);
          /*      float3 right = float3(unity_ObjectToWorld[0][0], unity_ObjectToWorld[0][1], unity_ObjectToWorld[0][2]);
                float3 up = float3(unity_ObjectToWorld[1][0], unity_ObjectToWorld[1][1], unity_ObjectToWorld[1][2]);
                float3 forward = float3(unity_ObjectToWorld[2][0], unity_ObjectToWorld[2][1], unity_ObjectToWorld[2][2]);*/
                float3x3 wdMatrix= float3x3(right, forward, up);
                decalNormal = normalize(decalNormal);
                decalNormal = (mul(decalNormal, wdMatrix));
                albedo.rgb = lerp(originAlbedo.rgb, decalAlbedo.rgb, decalAlbedo.a);
                albedo.a = originAlbedo.a;
                normal.xyz = normalize(lerp(originNormal.xyz, decalNormal, decalAlbedo.a)) * 0.5 + 0.5;
                normal.a = originNormal.a;
            }
            ENDCG
        }
    }
}
