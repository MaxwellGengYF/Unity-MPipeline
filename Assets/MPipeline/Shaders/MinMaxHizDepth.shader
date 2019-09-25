Shader "Hidden/MinMaxHizDepth"
{
    SubShader
    {
CGINCLUDE

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

            Texture2D<float4> _TargetDepthTexture; SamplerState sampler_TargetDepthTexture; float4 _TargetDepthTexture_TexelSize;
            Texture2D<float4> _DepthBufferTexture; SamplerState sampler_DepthBufferTexture; float4 _DepthBufferTexture_TexelSize;
            #define TEX2D(tex, uv) tex.SampleLevel(sampler##tex, uv, 0)
ENDCG
        // No culling or depth
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            float2 frag (v2f i) : SV_Target
            {
                float2 ofst = _DepthBufferTexture_TexelSize.xy;
                float4 value = float4(
                    TEX2D(_DepthBufferTexture, i.uv + ofst).x,
                    TEX2D(_DepthBufferTexture, i.uv - ofst).x,
                    TEX2D(_DepthBufferTexture, i.uv + float2(ofst.x, -ofst.y)).x,
                    TEX2D(_DepthBufferTexture, i.uv - float2(ofst.x, -ofst.y)).x
                );
                float4 v = 0;
                v.xy = min(value.xy ,value.zw);
                v.zw = max(value.xy, value.zw);
                v.x = min(v.x, v.y);
                v.z = max(v.z, v.w);
                return v.xz;
            }
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            float2 frag (v2f i) : SV_Target
            {
                float2 ofst = _TargetDepthTexture_TexelSize.xy;
                float2 depthMinMax[4] = {
                    TEX2D(_TargetDepthTexture, i.uv + ofst).xy,
                    TEX2D(_TargetDepthTexture, i.uv - ofst).xy,
                    TEX2D(_TargetDepthTexture, i.uv + float2(ofst.x, -ofst.y)).xy,
                    TEX2D(_TargetDepthTexture, i.uv - float2(ofst.x, -ofst.y)).xy
                };
                float4 mn = float4(depthMinMax[0].x, depthMinMax[1].x, depthMinMax[2].x, depthMinMax[3].x);
                mn.xy = min(mn.xy, mn.zw);
                mn.x = min(mn.x, mn.y);
                float4 mx = float4(depthMinMax[0].y, depthMinMax[1].y, depthMinMax[2].y, depthMinMax[3].y);
                mx.xy = max(mx.xy, mx.zw);
                mx.x = max(mx.x, mx.y);
                return float2(mn.x, mx.x);
            }
            ENDCG
        }
    }
}
