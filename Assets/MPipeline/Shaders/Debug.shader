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
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            void frag (v2f i, out float4 col : SV_TARGET)
            {
                float2 albedoUV = i.uv * 2;
                float2 specularUV = i.uv * 2 - float2(0, 1);
                float2 normalUV = i.uv  * 2 - float2(1, 0);
                float2 depthUV = i.uv * 2 - 1;
                if(i.uv.x > 0.5)
                {
                    if(i.uv.y > 0.5) col = tex2D(_CameraDepthTexture, depthUV).rrrr;
                    else col = tex2D(_CameraGBufferTexture2, normalUV);
                }else
                {
                    if(i.uv.y > 0.5) col = tex2D(_CameraGBufferTexture1, specularUV);
                    else col = tex2D(_CameraGBufferTexture0, albedoUV);
                }
            }
            ENDCG
        }
    }
}
