Shader "Skybox/MaxwellPipelineSkybox"
{
    Properties
    {
        _MainTex ("Cubemap", Cube) = "white" {}
    }
    SubShader
    {
        // No culling or depth
       
        Pass
        {
             Cull Off ZWrite Off ZTest LEqual
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 5.0
            #include "UnityCG.cginc"
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
            float4x4 _InvSkyVP;
            float4x4 _LastSkyVP;
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = v.vertex;
                o.uv = v.uv * 2  -1;
                return o;
            } 
            samplerCUBE _MainTex;

            void frag (v2f i, out float3 color : SV_TARGET0, out float2 velocity : SV_TARGET1)
            {
                #if UNITY_REVERSED_Z
                float4 worldPos = mul(_InvSkyVP, float4(i.uv, 0, 1));
                #else
                float4 worldPos = mul(_InvSkyVP, float4(i.uv, 1, 1));
                #endif
                worldPos /= worldPos.w;
                color = texCUBE(_MainTex, worldPos.xyz);
                float4 lastProj = mul(_LastSkyVP, worldPos);
                lastProj.xy /= lastProj.w;
                lastProj.xy = lastProj.xy * 0.5 + 0.5;
                velocity = (i.uv * 0.5 + 0.5) - lastProj.xy;
                #if UNITY_UV_STARTS_AT_TOP
                velocity.y = -velocity.y;
                #endif
            }
            ENDCG
        }
    }
}
