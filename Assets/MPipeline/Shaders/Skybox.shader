Shader "Skybox/MaxwellPipelineSkybox"
{
   Properties {
    _Tint ("Tint Color", Color) = (.5, .5, .5, .5)
    [Gamma] _Exposure ("Exposure", Range(0, 8)) = 1.0
    _Rotation ("Rotation", Range(0, 360)) = 0
    [NoScaleOffset] _Tex ("Cubemap   (HDR)", Cube) = "grey" {}
}
    SubShader
    {
       Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
    Cull Off ZWrite Off ZTest less

    Pass {

        CGPROGRAM
// Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it uses non-square matrices
#pragma exclude_renderers gles

        #pragma vertex vert
        #pragma fragment frag
        #pragma target 5.0
        #include "UnityCG.cginc"
        samplerCUBE _Tex;
        float4 _Tex_HDR;
        float4 _Tint;
        float _SkyDistance;
        float _Exposure;
        float _Rotation;

        float3 RotateAroundYInDegrees (float3 vertex, float degrees)
        {
            float alpha = degrees * UNITY_PI / 180.0;
            float sina, cosa;
            sincos(alpha, sina, cosa);
            float2x2 m = float2x2(cosa, -sina, sina, cosa);
            return float3(mul(m, vertex.xz), vertex.y).xzy;
        }

        struct appdata_t {
            float4 vertex : POSITION;
        };

        struct v2f {
            float4 vertex : SV_POSITION;
            float3 texcoord : TEXCOORD0;
            float3 worldPos : TEXCOORD1;
            float3 screenUV : TEXCOORD2;
        };

        v2f vert (appdata_t v)
        {
            v2f o;
            float3 rotated = RotateAroundYInDegrees(v.vertex, _Rotation);
            o.vertex = UnityObjectToClipPos(rotated);
            o.texcoord = v.vertex.xyz;
            o.worldPos = rotated;
            o.screenUV = ComputeScreenPos(o.vertex).xyw;
            return o;
        }
float4x4 _LastSkyVP;
float2 _Jitter;
            void frag (v2f i, out float3 color : SV_TARGET0, out float2 mv : SV_TARGET1)
            {
                float2 uv = i.screenUV.xy / i.screenUV.z;
                float4 lastProj = mul(_LastSkyVP, float4(i.worldPos, 1));
                lastProj /= lastProj.w;
                mv = uv - (lastProj.xy * 0.5 + 0.5) + _Jitter;
                float4 tex = texCUBElod(_Tex, float4(i.texcoord, 0));
                color = DecodeHDR(tex, _Tex_HDR);
                color = color * _Tint.rgb * unity_ColorSpaceDouble.rgb * _Exposure;
            }
            ENDCG
        }
    }
}
