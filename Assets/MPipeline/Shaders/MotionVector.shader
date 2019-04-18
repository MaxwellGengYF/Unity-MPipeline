Shader "Hidden/MotionVector"
{
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Greater

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            Texture2D _CameraGBufferTexture2; SamplerState sampler_CameraGBufferTexture2;
            float4x4 _InvVP;
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
                float depth = _CameraGBufferTexture2.Sample(sampler_CameraGBufferTexture2, i.uv).w;
                float4 worldPos = mul(_InvVP, float4(i.uv * 2 - 1, depth, 1));
                float4 lastClip = mul(_LastVp, worldPos);
                float2 uv = lastClip.xy / lastClip.w;
                uv = uv * 0.5 + 0.5;
                return i.uv - uv;
            }
            ENDCG
        }
    }
}
