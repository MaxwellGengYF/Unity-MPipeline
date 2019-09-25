Shader "Hidden/SeparableSubsurfaceScatter" {

    SubShader {
    CGINCLUDE
    #pragma target 5.0
    #include "UnityCG.cginc" 
    #include "CGINC/Random.cginc"
#define DistanceToProjectionWindow 5.671281819617709             //1.0 / tan(0.5 * radians(20));
#define DPTimes300 1701.384545885313                             //DistanceToProjectionWindow * 300
#define SamplerSteps 11

uniform float _SSSScale;
float4 _CameraDepthTexture_TexelSize;
StructuredBuffer<float4> _Kernel;
uniform sampler2D _MainTex, _CameraDepthTexture;

struct VertexInput {
    float4 vertex : POSITION;
    float2 uv : TEXCOORD0;
};
struct VertexOutput {
    float4 pos : SV_POSITION;
    float2 uv : TEXCOORD0;
};
VertexOutput vert_sss (VertexInput v) {
    VertexOutput o;
    o.pos = v.vertex;
    o.uv = v.uv;
    return o;
}
float4 SSS(float4 SceneColor, float2 UV, float2 SSSIntencity) {
    float SceneDepth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, UV));                                   
    float BlurLength = DistanceToProjectionWindow / SceneDepth;                                   
    float2 UVOffset = SSSIntencity * BlurLength;             
    float4 BlurSceneColor = SceneColor;
    BlurSceneColor.rgb *=  _Kernel[0].rgb;
    float2 currentUV = UV;
    for (int i = 1; i < SamplerSteps; i++) {
        currentUV = MNoise(currentUV) * UVOffset;
        float4 kernelValue = _Kernel[i];
        float2 SSSUV = UV +  kernelValue.a * currentUV;
        float4 SSSSceneColor = tex2D(_MainTex, SSSUV);
        float SSSDepth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, SSSUV)).r;         
        float SSSScale = saturate(DPTimes300 * SSSIntencity * abs(SceneDepth - SSSDepth));
        SSSSceneColor.rgb = lerp(SSSSceneColor.rgb, SceneColor.rgb, SSSScale);
        BlurSceneColor.rgb +=  kernelValue.rgb * SSSSceneColor.rgb;
    }
    return BlurSceneColor;
    //return float4(UVOffset, 0, 1);
}
    ENDCG
        Pass {
        ZTest Greater
        ZWrite off
        Cull off
        Stencil
        {
            Ref 2
            Pass keep
            comp equal
            ReadMask 127
        }
            CGPROGRAM
            #pragma vertex vert_sss
            #pragma fragment frag

            float4 frag(VertexOutput i) : SV_TARGET {
                float2 uv = i.uv;
                float4 SceneColor = tex2D(_MainTex, uv);
                float SSSIntencity = (_SSSScale * _CameraDepthTexture_TexelSize.x);
                float3 XBlurPlus = SSS(SceneColor, uv, float2(SSSIntencity, 0)).rgb;
                return float4(XBlurPlus, SceneColor.a);
            }
            ENDCG
        } Pass {
        ZTest Greater
        ZWrite off
        Cull off
        Stencil
        {
            Ref 2
            Pass keep
            comp equal
            ReadMask 127
        }

            CGPROGRAM
            #pragma vertex vert_sss
            #pragma fragment frag

            float4 frag(VertexOutput i) : SV_TARGET {
                float2 uv = i.uv;
                float4 SceneColor = tex2D(_MainTex, uv);
                float SSSIntencity = (_SSSScale * _CameraDepthTexture_TexelSize.y);
                float3 YBlurPlus = SSS(SceneColor, uv, float2(0, SSSIntencity)).rgb;
                //float3 YBlurNagteiv = SSS(SceneColor, i.uv, float2(0, -SSSIntencity)).rgb;
                //float3 YBlur = (YBlurPlus + YBlurNagteiv) / 2;
                return float4(YBlurPlus, SceneColor.a);
            }
            ENDCG
        }
    }
}
