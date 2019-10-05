Shader "Hidden/NormalToRT"
{
    Properties
    {
        _MainTex("main tex", 2D) = "white"{}
    }
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
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            sampler2D _MainTex;

            float2 frag (v2f i) : SV_Target
            {
                return UnpackNormal(tex2Dlod(_MainTex, float4(i.uv, 0, 0))).xy;
            }
            ENDCG
        }
    }
}
