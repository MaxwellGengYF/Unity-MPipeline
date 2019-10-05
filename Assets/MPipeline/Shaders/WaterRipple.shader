Shader "Unlit/WaterRipple"
{
    Properties
    {
        _TimeLine("Time line", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"  ="Transparent+2"}
        LOD 100
        ZWrite off
        Cull off
        ZTest Always
        Blend oneMinusSrcAlpha srcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            static const float pi = 3.14159265;
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
            float _TimeLine;


            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float2 uvDiff = i.uv - 0.5;
                float dist = pow(dot(uvDiff, uvDiff), 0.5);
                float value = sin((1 - exp(-dist * 7) * 30) - _TimeLine * pi * 15);
                return float4(normalize(uvDiff) * value * 0.5 + 0.5, 0, saturate(dist * 2 - sqrt(_TimeLine) + 1));
            }
            ENDCG
        }
    }
}
