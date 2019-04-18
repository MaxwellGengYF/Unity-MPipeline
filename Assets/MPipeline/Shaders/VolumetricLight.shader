Shader "Hidden/VolumetricLight"
{
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

CGINCLUDE
#pragma target 5.0
#include "UnityCG.cginc"
Texture2D<float> _CameraDepthTexture; SamplerState sampler_CameraDepthTexture;
#include "CGINC/VolumetricLight.cginc"
float2 _Jitter;
            struct v2fScreen
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };
            v2fScreen screenVert (appdata v)
            {
                v2fScreen o;
                o.vertex = v.vertex;
                o.uv = v.uv - _Jitter;
                return o;
            }

ENDCG
        pass
        {
            Cull off ZWrite off ZTest Always
            Blend OneMinusSrcAlpha SrcAlpha
            CGPROGRAM
            #pragma vertex screenVert
            #pragma fragment frag
            float4 frag(v2fScreen i) : SV_TARGET
            {
                
                float linear01Depth = Linear01Depth(_CameraDepthTexture.Sample(sampler_CameraDepthTexture, i.uv));
		        float4 fog = Fog(linear01Depth, i.uv);
		        return fog;
            }
            ENDCG
        }
    }
}