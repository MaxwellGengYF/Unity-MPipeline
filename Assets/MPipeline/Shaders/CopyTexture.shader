Shader "Unlit/CopyTexture"
{
    SubShader
    {
        LOD 100
        ZWrite off ZTest Always Cull off
        CGINCLUDE
            #pragma target 5.0
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
            StructuredBuffer<uint> _TextureBuffer;
            float2 _TextureSize;
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = v.vertex;
                o.uv = v.uv;
                return o;
            }
            #define MAX_BRIGHTNESS 6
            float3 DecodeHDR(uint data)
            {
                float r = (data      ) & 0xff;
                float g = (data >>  8) & 0xff;
                float b = (data >> 16) & 0xff;
                float a = (data >> 24) & 0xff;
                return float3(r, g, b) * a * MAX_BRIGHTNESS / (255 * 255);
            }
            uint4 GetValues(uint value)
            {
                uint4 values = 0;
                values.x = value & 255;
                value >>= 8;
                values.y = value & 255;
                value >>= 8;
                values.z = value & 255;
                value >>= 8;
                values.w = value & 255;
                return values;
            }
        ENDCG
        //Pass 0: Linear Transform
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag (v2f i) : SV_Target
            {
                if(i.uv.x > 0.5)
                    i.uv.x -= 0.5;
                else if(i.uv.x < 0.5)
                    i.uv.x += 0.5;
                return ((half4)GetValues(_TextureBuffer[i.uv.y * _TextureSize.y * _TextureSize.x + i.uv.x * _TextureSize.x])) / 255.0;
            }
            ENDCG
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half3 frag (v2f i) : SV_Target
            {
                if(i.uv.x > 0.5)
                    i.uv.x -= 0.5;
                else if(i.uv.x < 0.5)
                    i.uv.x += 0.5;
                return DecodeHDR(_TextureBuffer[i.uv.y * _TextureSize.y * _TextureSize.x + i.uv.x * _TextureSize.x]);
            }
            ENDCG
        }
    }
}
