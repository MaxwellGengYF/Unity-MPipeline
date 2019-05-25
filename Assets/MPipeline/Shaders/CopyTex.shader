Shader "Hidden/CopyTex"
{
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always
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

            sampler2D _MainTex;

ENDCG
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            void frag (v2f i, out float4 color : SV_TARGET0)
            {
                color = tex2D(_MainTex, i.uv);
            }
            ENDCG
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            void frag (v2f i, out float2 color : SV_TARGET0)
            {
                color =  UnpackNormal(tex2D(_MainTex, i.uv)).xy;
            }
            ENDCG
        }
    }
}
