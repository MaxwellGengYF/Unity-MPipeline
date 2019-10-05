Shader "Hidden/GetLOD"
{
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

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
            Texture2D<float> _MainTex; SamplerState sampler_MainTex;
            uint _PreviousLevel;
            float frag (v2f i) : SV_Target
            {
                float4 value = float4(
                    _MainTex.SampleLevel(sampler_MainTex, i.uv, _PreviousLevel, int2(-1,-1)),
                    _MainTex.SampleLevel(sampler_MainTex, i.uv, _PreviousLevel, int2(-1, 1)),
                    _MainTex.SampleLevel(sampler_MainTex, i.uv, _PreviousLevel, int2(1,-1)),
                    _MainTex.SampleLevel(sampler_MainTex, i.uv, _PreviousLevel, int2(1, 1))
                );
                #ifdef UNITY_REVERSED_Z
                value.xy = min(value.xy, value.zw);
                return min(value.x, value.y);
                #else
                value.xy = max(value.xy, value.zw);
                return max(value.x, value.y);
                #endif
            }
            ENDCG
        }
    }
}
