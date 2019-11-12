Shader "Hidden/TerrainMaterialEdit"
{
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #pragma target 5.0
            #include "CGINC/HeightBlendMaterial.cginc"
            
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

            Texture2D<float3> _Albedo; SamplerState sampler_Albedo;
            Texture2D<float4> _SMO; SamplerState sampler_SMO;
            Texture2D<float4> _Normal; SamplerState sampler_Normal;
            float4 _Color;
            float _Smoothness;
            float _Metallic;
            float _Occlusion;
            float2 _NormalScale;
            float2 _TextureSize;



            void frag (v2f i, out float4 albedoOutput : SV_TARGET0, out float4 normalOutput : SV_TARGET1, out float4 smoOutput : SV_TARGET2)
            {
                [branch]
                if(_Color.w > 0.5){
                float2 noiseValue = _NoiseTexture.SampleLevel(sampler_NoiseTexture, i.uv, 0) - 0.5;
                i.uv *= _TextureSize;
                float4 tillingOffsetScale = _NoiseTillingTexture.SampleLevel(sampler_NoiseTillingTexture, (i.uv + noiseValue) * 0.015625, 0);
                i.uv = i.uv * tillingOffsetScale.zw + tillingOffsetScale.xy;
                }
                else
                {
                    i.uv *= _TextureSize;
                }
                float3 albedo;
                float3 smo;
                float2 normal =  UnpackNormal(_Normal.SampleLevel(sampler_Normal, i.uv, 0)).xy * _NormalScale;
                albedo = _Albedo.SampleLevel(sampler_Albedo, i.uv, 0) * _Color.xyz;
                smo = _SMO.SampleLevel(sampler_SMO, i.uv, 0);
                smo.xy *= float2(_Smoothness, _Metallic);
                smo.z = lerp(1, smo.z, _Occlusion);

                normalOutput = float4(normal * 0.5 + 0.5, 1, 1);
                albedoOutput = float4(albedo, 1);
                smoOutput = float4(smo, 1);
            }
            ENDCG
        }
    }
}
