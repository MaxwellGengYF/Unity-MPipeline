Shader "Hidden/DownSampleDepth"
{
    SubShader
    {
        CGINCLUDE
            #include "UnityCG.cginc"

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

            Texture2D<float> _CameraDepthTexture; SamplerState sampler_CameraDepthTexture; float4 _CameraDepthTexture_TexelSize;
            Texture2D<float4> _CameraGBufferTexture1; SamplerState sampler_CameraGBufferTexture1;
            Texture2D<float3> _CameraGBufferTexture2; SamplerState sampler_CameraGBufferTexture2;
            

        ENDCG
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            void frag (v2f i, out float depth : SV_TARGET0, out float4 specular : SV_TARGET1, out float3 normal : SV_TARGET2)
            {
                float2 offset = _CameraDepthTexture_TexelSize.xy * 0.5;
                depth =dot(float4(
                    _CameraDepthTexture.Sample(sampler_CameraDepthTexture, i.uv + offset),
                    _CameraDepthTexture.Sample(sampler_CameraDepthTexture, i.uv - offset),
                    _CameraDepthTexture.Sample(sampler_CameraDepthTexture, i.uv + float2(offset.x, -offset.y)),
                    _CameraDepthTexture.Sample(sampler_CameraDepthTexture, i.uv - float2(offset.x, -offset.y))
                ), 0.25);
                specular = (_CameraGBufferTexture1.Sample(sampler_CameraGBufferTexture1, i.uv + offset) + 
                           _CameraGBufferTexture1.Sample(sampler_CameraGBufferTexture1, i.uv - offset) + 
                           _CameraGBufferTexture1.Sample(sampler_CameraGBufferTexture1, i.uv + float2(offset.x, -offset.y)) + 
                           _CameraGBufferTexture1.Sample(sampler_CameraGBufferTexture1, i.uv - float2(offset.x, -offset.y))) * 0.25;
                normal = normalize(
                    _CameraGBufferTexture2.Sample(sampler_CameraGBufferTexture2, i.uv + offset) * 2 - 1 + 
                    _CameraGBufferTexture2.Sample(sampler_CameraGBufferTexture2, i.uv - offset) * 2 - 1 + 
                    _CameraGBufferTexture2.Sample(sampler_CameraGBufferTexture2, i.uv + float2(offset.x, -offset.y)) * 2 - 1 + 
                    _CameraGBufferTexture2.Sample(sampler_CameraGBufferTexture2, i.uv - float2(offset.x, -offset.y)) * 2 - 1
                ) * 0.5 + 0.5;
            }
            ENDCG
        }
    }
}
