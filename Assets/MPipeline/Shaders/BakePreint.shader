Shader "Hidden/BakePreint"
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
            #include "CGINC/Shader_Include/ImageBasedLighting.hlsl"
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

            float2 frag (v2f i) : SV_Target
            {
                return IBL_Defualt_SpecularIntegrated(i.uv.x, i.uv.y);
            }
            ENDCG
        }
    }
}
