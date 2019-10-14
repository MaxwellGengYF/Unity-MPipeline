Shader "Unlit/Debug"
{
    Properties
    {
        _MainTex("main tex", any) = "white"{}
    }
    SubShader
    {
        ZTest Always
        ZWrite off
        Cull off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 5.0
            #include "UnityCG.cginc"
            Texture2DArray _MainTex; SamplerState sampler_MainTex;
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            sampler2D _AOROTexture;
            sampler2D _CameraMotionVectorsTexture;
            void frag (v2f i, out float4 col : SV_TARGET)
            {
                col = _MainTex.SampleLevel(sampler_MainTex, float3(i.uv, 0), 3);
            }
            ENDCG
        }

        Pass
        {
                        ZWrite off
            ZTest always
            Cull off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 5.0
            #include "UnityCG.cginc"

            Texture2D<float4> _TargetTex; SamplerState sampler_TargetTex;
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = v.vertex;
                o.uv = v.uv;
                return o;
            }
            void frag (v2f i, out float4 col : SV_TARGET)
            {
                col = _TargetTex.SampleLevel(sampler_TargetTex, i.uv, 0);
            }
            ENDCG
        }
    }
}
