Shader "Hidden/MotionVector"
{
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always
        Stencil
        {
            Ref 0
            comp equal
            pass keep
            ReadMask 4
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            Texture2D<float> _CameraDepthTexture; SamplerState sampler_CameraDepthTexture;
            float4x4 _InvNonJitterVP;
            float4x4 _LastVp;
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
                float depth = _CameraDepthTexture.Sample(sampler_CameraDepthTexture, i.uv);
                float4 worldPos = mul(_InvNonJitterVP, float4(i.uv * 2 - 1, depth, 1));
                float4 lastClip = mul(_LastVp, worldPos);
                float2 uv = lastClip.xy / lastClip.w;
                uv = uv * 0.5 + 0.5;
                return i.uv - uv;
            }
            ENDCG
        }
    }
}
