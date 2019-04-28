Shader "Unlit/Debug"
{
    SubShader
    {
       Tags{ "LightMode" = "Transparent" "Queue" = "Transparent"}
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 5.0
            #include "UnityCG.cginc"
            #include "CGINC/Random.cginc"
            samplerCUBE _Cubemap;
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 normal : NORMAL;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.normal = v.normal;
                return o;
            }
            float4 frag (v2f i) : SV_Target
            {
                return texCUBE(_Cubemap, i.normal);
            }
            ENDCG
        }
    }
}
