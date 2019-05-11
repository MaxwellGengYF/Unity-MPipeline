Shader "Unlit/Debug"
{
    Properties
    {
        _ZTest("_zt", int) = 0
    }
    SubShader
    {
        ZTest [_ZTest]
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
            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = (v.vertex);
                return o;
            }
            void frag (v2f i, out float4 outGBuffer0 : SV_TARGET)
            {
                outGBuffer0 = 1;
            }
            ENDCG
        }
    }
}
