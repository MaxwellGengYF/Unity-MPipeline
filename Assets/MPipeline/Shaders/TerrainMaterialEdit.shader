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

            Texture2D<float3> _Albedo0; SamplerState sampler_Albedo0;
            Texture2D<float4> _Normal0; SamplerState sampler_Normal0;
            Texture2D<float4> _SMO0; SamplerState sampler_SMO0;
            Texture2D<float3> _Albedo1; SamplerState sampler_Albedo1;
            Texture2D<float4> _Normal1; SamplerState sampler_Normal1;
            Texture2D<float4> _SMO1; SamplerState sampler_SMO1;
            float4 _Setting;

            void frag (v2f i, out float3 albedo : SV_TARGET0, out float2 normal : SV_TARGET1, out float3 smo : SV_TARGET2)
            {
                HeightBlendMaterial mat;
                mat.firstMaterialIndex = 0;
                mat.secondMaterialIndex = 0;
                mat.offset = _Setting.x;
                mat.heightBlendScale = _Setting.y;
                GetHeightBlendInEditor(mat, 
                _Albedo0.SampleLevel(sampler_Albedo0, i.uv, 0),
                UnpackNormal(_Normal0.SampleLevel(sampler_Normal0, i.uv, 0)),
                _SMO0.SampleLevel(sampler_SMO0, i.uv, 0),
                _Albedo1.SampleLevel(sampler_Albedo1, i.uv, 0),
                UnpackNormal(_Normal1.SampleLevel(sampler_Normal1, i.uv, 0)),
                _SMO1.SampleLevel(sampler_SMO1, i.uv, 0),
                albedo, normal, smo
                );
            }
            ENDCG
        }
    }
}
