Shader "Unlit/Debug"
{
    SubShader
    {
        
       
        Pass
        {
            Tags {"LightMode" = "GBuffer"}
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
                o.vertex = UnityObjectToClipPos(v.vertex);
                return o;
            }
            void frag (v2f i, out float4 outGBuffer0 : SV_Target0,
    out float4 outGBuffer1 : SV_Target1,
    out float4 outGBuffer2 : SV_Target2,
    out float4 outEmission : SV_Target3)
            {
                outGBuffer0 = float4(0,0,0,1);
				outGBuffer1 = float4(0.5,0.5,0.5,0.3);
				outGBuffer2 = float4(0, 0, 1, 1);
				outEmission = i.vertex.z;
            }
            ENDCG
        }
    }
}
