Shader "Unlit/IrradianceVolume"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100
        Cull front ZWrite off ZTest Greater
        Blend one one
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 5.0
            #include "UnityCG.cginc"
            #include "GI/SHRuntime.cginc"
            #pragma multi_compile _ EnableGTAO

            Texture2D<float4> _CameraGBufferTexture0; SamplerState sampler_CameraGBufferTexture0;
			Texture2D<float4> _CameraGBufferTexture1; SamplerState sampler_CameraGBufferTexture1;
			Texture2D<float4> _CameraGBufferTexture2; SamplerState sampler_CameraGBufferTexture2;
			Texture2D<float> _CameraDepthTexture; SamplerState sampler_CameraDepthTexture;
            Texture2D<float> _AOROTexture; SamplerState sampler_AOROTexture;
            float4x4 _InvVP;
            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = ComputeScreenPos(o.vertex);
                return o;
            }

            float3 frag (v2f i) : SV_Target
            {
               
                float2 screenUV = i.uv.xy / i.uv.w;
                float4 normal_mask = _CameraGBufferTexture2.Sample(sampler_CameraGBufferTexture2, screenUV);
                if(normal_mask.w < 1e-2) return 0;
                 
                float depth = _CameraDepthTexture.Sample(sampler_CameraDepthTexture, screenUV);
                float4 albedo_occ = _CameraGBufferTexture0.Sample(sampler_CameraGBufferTexture0, screenUV);
                #if EnableGTAO
                float ao = _AOROTexture.Sample(sampler_AOROTexture,screenUV);
                albedo_occ.w = min(albedo_occ.w, ao);
                #endif
                float4 worldPos = mul(_InvVP, float4(screenUV * 2 - 1, depth, 1));
                worldPos /= worldPos.w;
                 float3 shUV = GetSHUV(worldPos.xyz);
                float offset = dot(abs(shUV - saturate(shUV)), 1);
                if(offset > 1e-6) return 0;

                SHColor sh = GetSHFromTex(shUV);                
                return GetSHColor(sh.c, normal_mask.xyz * 2 - 1) * albedo_occ.rgb * albedo_occ.w; 
            }
            ENDCG
        }
    }
}
