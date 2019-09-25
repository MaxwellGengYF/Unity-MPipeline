Shader "Hidden/TileGenerator"
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
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            Texture2D<float2> _RandomTex;
            float4 _RandomTex_TexelSize;
            float _TileAlias;
            float2 _UVScale;
            float2 frag (v2f i) : SV_Target
            {
                float2 sampUV = i.uv * _RandomTex_TexelSize.zw;
                float2 v = 1;
                if(((uint)sampUV.y) % 2 > 0)
                {
                    sampUV.x += _TileAlias;
                }
                uint2 sampUVInt = (uint2)sampUV;
                uint2 roundUV = sampUVInt > (_RandomTex_TexelSize.zw - 0.5) ?(uint2)(sampUVInt - _RandomTex_TexelSize.zw) : sampUVInt;
                float2 randOffset = _RandomTex[roundUV];
                float2 result = (sampUV - sampUVInt) * _UVScale;
                return result + randOffset;
            }
            ENDCG
        }
    }
}
