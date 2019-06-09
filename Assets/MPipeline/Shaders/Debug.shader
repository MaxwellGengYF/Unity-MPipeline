Shader "Unlit/Debug"
{
    Properties
    {
        _ZTest("_zt", int) = 0
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
            #include "GI/GlobalIllumination.cginc"
            #include "CGINC/Random.cginc"
            sampler2D _CameraGBufferTexture0;
            sampler2D _CameraGBufferTexture1;
            sampler2D _CameraGBufferTexture2;
            sampler2D _CameraDepthTexture;
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
                o.vertex.y = -o.vertex.y;
                o.uv = v.uv;
                return o;
            }
            sampler2D _AOROTexture;
            void frag (v2f i, out float4 col : SV_TARGET)
            {
                col = tex2D(_AOROTexture, i.uv).r;
            }
            ENDCG
        }
    }
}
