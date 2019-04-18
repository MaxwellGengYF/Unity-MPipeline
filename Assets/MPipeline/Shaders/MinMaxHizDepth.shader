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

            sampler2D _TargetDepthTexture; float4 _TargetDepthTexture_TexelSize;
            sampler2D _CameraDepthTexture; float4 _CameraDepthTexture_TexelSize;
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
                float2 ofst = _CameraDepthTexture_TexelSize.xy;
                float4 value = float4(
                    tex2D(_CameraDepthTexture, i.uv + ofst).x,
                    tex2D(_CameraDepthTexture, i.uv - ofst).x,
                    tex2D(_CameraDepthTexture, i.uv + float2(ofst.x, -ofst.y)).x,
                    tex2D(_CameraDepthTexture, i.uv - float2(ofst.x, -ofst.y)).x
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
                    tex2D(_TargetDepthTexture, i.uv + ofst).xy,
                    tex2D(_TargetDepthTexture, i.uv - ofst).xy,
                    tex2D(_TargetDepthTexture, i.uv + float2(ofst.x, -ofst.y)).xy,
                    tex2D(_TargetDepthTexture, i.uv - float2(ofst.x, -ofst.y)).xy
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
