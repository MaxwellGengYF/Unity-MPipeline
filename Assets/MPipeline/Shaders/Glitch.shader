Shader "Hidden/Glitch"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
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
            #pragma target 5.0
            #include "UnityCG.cginc"
            #include "CGINC/Random.cginc"

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
    cbuffer _CyberData
    {
        float scanLineJitter;
        float horizontalShake;
        float2 colorDrift;
    }
    float nrand(float x, float y)
    {
        return frac(sin(dot(float2(x,y), float2(12.9898, 78.233))) * 43758.5453);
    }
            float4 frag (v2f i) : SV_Target
            {
                float jitter = dot(MNoise(float2(i.uv.y, _Time.x)), 1) - 1;
                jitter *= scanLineJitter;
                jitter *= scanLineJitter;
                float shake = nrand(_Time.x, 2) * horizontalShake;
                float drift = sin(colorDrift.y) * colorDrift.x;
                float4 src1 = tex2D(_MainTex, frac(float2(i.uv.x + jitter + shake, i.uv.y)));
                float4 src2 = tex2D(_MainTex, frac(float2(i.uv.x + jitter + shake + drift, i.uv.y)));
                return float4(src1.r, src2.g, src1.b, 1);
            }
            ENDCG
        }
    }
}
