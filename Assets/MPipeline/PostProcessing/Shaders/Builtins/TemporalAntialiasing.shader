Shader "Hidden/PostProcessing/TemporalAntialiasing"
{
    HLSLINCLUDE

        #pragma exclude_renderers gles psp2

        #include "../StdLib.hlsl"
        #include "../Colors.hlsl"

        #if UNITY_VERSION >= 201710
            #define _MainTexSampler sampler_LinearClamp
        #else
            #define _MainTexSampler sampler_MainTex
        #endif

        TEXTURE2D_SAMPLER2D(_MainTex, _MainTexSampler);
        float4 _MainTex_TexelSize;

        TEXTURE2D_SAMPLER2D(_HistoryTex, sampler_HistoryTex);

        TEXTURE2D_SAMPLER2D(_CameraDepthTexture, sampler_CameraDepthTexture);
        Texture2D<float> _LastFrameDepthTexture; SamplerState sampler_LastFrameDepthTexture;
        float4 _CameraDepthTexture_TexelSize;
        float3 _TemporalClipBounding;
        TEXTURE2D_SAMPLER2D(_CameraMotionVectorsTexture, sampler_CameraMotionVectorsTexture);
        sampler2D _CameraGBufferTexture0;
        sampler2D _HistoryAlbedo;
        float2 _Jitter;
        float4 _FinalBlendParameters; // x: static, y: dynamic, z: motion amplification
        float _Sharpness;

        struct OutputSolver
        {
            float4 destination : SV_Target0;
            float4 history     : SV_Target1;
        };

        struct appdata
		{
			float4 vertex : POSITION;
			float2 texcoord : TEXCOORD0;
		};

		struct v2f
		{
			float4 vertex : SV_POSITION;
			float2 texcoord : TEXCOORD0;
		};

		v2f vert(appdata v)
		{
			v2f o;
			o.vertex = v.vertex;
			o.texcoord = v.texcoord;
			return o;
		}

/////////////////////////////////////////////////////////////////////////////////////////////CGBull TemporalAA
    #ifndef AA_VARIANCE
        #define AA_VARIANCE 1
    #endif

    #ifndef AA_Filter
        #define AA_Filter 1
    #endif

    #define SAMPLE_DEPTH_OFFSET(x,y,z,a) (x.Sample(y,z,a).r )
    #define SAMPLE_TEXTURE2D_OFFSET(x,y,z,a) (x.Sample(y,z,a))
    float2 _LastJitter;
    float4x4 _InvNonJitterVP;
    float4x4 _InvLastVp;



////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    float3 RGBToYCoCg(float3 RGB)
    {
        const float3x3 mat = float3x3(0.25,0.5,0.25,0.5,0,-0.5,-0.25,0.5,-0.25);
        float3 col =mul(mat, RGB);
        return col;
    }
    
    float3 YCoCgToRGB(float3 YCoCg)
    {
        const float3x3 mat = float3x3(1,1,-1,1,0,1,1,-1,-1);
        return mul(mat, YCoCg);
    }

    float4 RGBToYCoCg(float4 RGB)
    {
        return float4(RGBToYCoCg(RGB.xyz), RGB.w);
    }



    float4 YCoCgToRGB(float4 YCoCg)
    {
        return float4(YCoCgToRGB(YCoCg.xyz), YCoCg.w); 
    }

    static const float A = 0.15;
    static const float B = 0.50;
    static const float C = 0.10;
    static const float D = 0.20;
    static const float E = 0.02;
    static const float F = 0.30;
    static const float W = 11.2;

    float3 Tonemap(float3 x) 
    { 
        x *= 2;
        return 1.0-((x*(A*x+C*B)+D*E)/(x*(A*x+B)+D*F)-E/F);
    }

    float3 TonemapInvert(float3 x) { 
        return ((sqrt((4*x-4*x*x)*A*D*F*F*F+((4*x-4)*A*D*E+B*B*C*C+(2*x-2)*B*B*C+(x*x-2*x+1)*B*B)*F*F+((2-2*x)*B*B-2*B*B*C)*E*F+B*B*E*E)+((1-x)*B-B*C)*F+B*E)/(2*x*A*F-2*A*E)) * 0.5;
    }

    float Pow2(float x)
    {
        return x * x;
    }


    float Luma4(float3 Color)
    {
        return (Color.g * 2) + (Color.r + Color.b);
    }

    float HdrWeight4(float3 Color, const float Exposure) 
    {
        return rcp(Luma4(Color) * Exposure + 4);
    }

    float3 ClipToAABB(float3 color, float3 minimum, float3 maximum)
    {
        // Note: only clips towards aabb center (but fast!)
        float3 center = 0.5 * (maximum + minimum);
        float3 extents = 0.5 * (maximum - minimum);

        // This is actually `distance`, however the keyword is reserved
        float3 offset = color.rgb - center;

        float3 ts = abs(extents / (offset + 0.0001));
        float t = saturate(Min3(ts.x, ts.y, ts.z));
        color.rgb = center + offset * t;
        return color;
    }

    float2 ReprojectedMotionVectorUV(float2 uv, out float neighborhood[9])
    {
        const float2 k = _CameraDepthTexture_TexelSize.xy;
        neighborhood[0] = SAMPLE_DEPTH_OFFSET(_CameraDepthTexture, sampler_CameraDepthTexture, uv, int2(-1, -1));
        neighborhood[1] = SAMPLE_DEPTH_OFFSET(_CameraDepthTexture, sampler_CameraDepthTexture, uv, int2(0, -1));
        neighborhood[2] = SAMPLE_DEPTH_OFFSET(_CameraDepthTexture, sampler_CameraDepthTexture, uv, int2(1, -1));
        neighborhood[3] = SAMPLE_DEPTH_OFFSET(_CameraDepthTexture, sampler_CameraDepthTexture, uv, int2(-1, 0));
        neighborhood[4] = SAMPLE_DEPTH_OFFSET(_CameraDepthTexture, sampler_CameraDepthTexture, uv, int2(1, 1));
        neighborhood[5] = SAMPLE_DEPTH_OFFSET(_CameraDepthTexture, sampler_CameraDepthTexture, uv, int2(1, 0));
        neighborhood[6] = SAMPLE_DEPTH_OFFSET(_CameraDepthTexture, sampler_CameraDepthTexture, uv, int2(-1, 1));
        neighborhood[7] = SAMPLE_DEPTH_OFFSET(_CameraDepthTexture, sampler_CameraDepthTexture, uv, int2(0, -1));

        #if defined(UNITY_REVERSED_Z)
            #define COMPARE_DEPTH(a, b) step(b, a)
        #else
            #define COMPARE_DEPTH(a, b) step(a, b)
        #endif
        float3 result = float3(0, 0,  _CameraDepthTexture.Sample(sampler_CameraDepthTexture, uv).x);
        neighborhood[8] = result.z;
        result = lerp(result, float3(-1, -1, neighborhood[0]), COMPARE_DEPTH(neighborhood[0], result.z));
        result = lerp(result, float3(0, -1, neighborhood[1]), COMPARE_DEPTH(neighborhood[1], result.z));
        result = lerp(result, float3(1, -1, neighborhood[2]), COMPARE_DEPTH(neighborhood[2], result.z));
        result = lerp(result, float3(-1, 0, neighborhood[3]), COMPARE_DEPTH(neighborhood[3], result.z));
        result = lerp(result, float3(1, 1, neighborhood[4]), COMPARE_DEPTH(neighborhood[4], result.z));
        result = lerp(result, float3(1, 0, neighborhood[5]), COMPARE_DEPTH(neighborhood[5], result.z));
        result = lerp(result, float3(-1, 1, neighborhood[6]), COMPARE_DEPTH(neighborhood[6], result.z));
        result = lerp(result, float3(0, -1, neighborhood[7]), COMPARE_DEPTH(neighborhood[7], result.z));

        return (uv + result.xy * k);
    }

    float getClampLerp(float2 PrevCoord, float2 CurrUV, float currDepth[9], float lenOfVelocity)
    {
        const float2 offset[9] = {float2(-1,-1), float2(0, -1), float2(1, -1), float2(-1, 0), float2(1,1), float2(1,0), float2(-1, 1), float2(0, -1), float2(0, 0)};
        float maxValue = -1;
        [unroll]
        for(uint i = 0; i < 9; ++i)
        {
            float2 ofst = offset[i] * _CameraDepthTexture_TexelSize.xy;
            float2 last = PrevCoord + ofst;
            float lastFrameDepth = _LastFrameDepthTexture.SampleLevel(sampler_LastFrameDepthTexture, last - _LastJitter + _Jitter, 0);
            float4 lastFrameProj = float4(last * 2 - 1, lastFrameDepth, 1);
            float4 currFrameProj = float4((CurrUV + ofst) * 2 - 1, currDepth[i], 1);
            float4 lastFrameWorld = mul(_InvLastVp, lastFrameProj);
            float4 currFrameWorld = mul(_InvNonJitterVP, currFrameProj);
            lastFrameWorld /= lastFrameWorld.w;
            currFrameWorld /= currFrameWorld.w;
            currFrameWorld.xyz -= lastFrameWorld.xyz;
            maxValue = max(maxValue, abs(dot(currFrameWorld.xyz, currFrameWorld.xyz)));
        }
           
        float diffWeight = maxValue < 0.01;
        float weight = lerp(0.1, 1, saturate(lenOfVelocity * _FinalBlendParameters.z));

        return lerp(diffWeight, 1, weight);
    }

    float4 Solver_CGBullTAA(v2f i) : SV_TARGET0
    {
        const float ExposureScale = 10;
        float2 uv = (i.texcoord - _Jitter);
        float2 screenSize = _ScreenParams.xy;
        float currDepth[9];
        float2 closest = ReprojectedMotionVectorUV(i.texcoord, currDepth);
        float2 velocity = SAMPLE_TEXTURE2D(_CameraMotionVectorsTexture, sampler_CameraMotionVectorsTexture, closest).xy;

//////////////////TemporalClamp
        float2 PrevCoord = (i.texcoord - velocity);
        float4 MiddleCenter = SAMPLE_TEXTURE2D(_MainTex, _MainTexSampler, uv);
        if (PrevCoord.x > 1 || PrevCoord.y > 1 || PrevCoord.x < 0 || PrevCoord.y < 0) {
            return MiddleCenter;
        }
        float4 TopLeft = SAMPLE_TEXTURE2D_OFFSET(_MainTex, _MainTexSampler, uv, int2(-1, -1));
        float4 TopCenter = SAMPLE_TEXTURE2D_OFFSET(_MainTex, _MainTexSampler, uv, int2(0, -1));
        float4 TopRight = SAMPLE_TEXTURE2D_OFFSET(_MainTex, _MainTexSampler, uv, int2(1, -1));
        float4 MiddleLeft = SAMPLE_TEXTURE2D_OFFSET(_MainTex, _MainTexSampler, uv, int2(-1,  0));
        float4 MiddleRight = SAMPLE_TEXTURE2D_OFFSET(_MainTex, _MainTexSampler, uv, int2(1,  0));
        float4 BottomLeft = SAMPLE_TEXTURE2D_OFFSET(_MainTex, _MainTexSampler, uv, int2(-1, 1));
        float4 BottomCenter = SAMPLE_TEXTURE2D_OFFSET(_MainTex, _MainTexSampler, uv, int2(0, 1));
        float4 BottomRight = SAMPLE_TEXTURE2D_OFFSET(_MainTex, _MainTexSampler, uv, int2(1, 1));
        float SampleWeights[9];
        SampleWeights[0] = HdrWeight4(TopLeft.rgb, ExposureScale);
        SampleWeights[1] = HdrWeight4(TopCenter.rgb, ExposureScale);
        SampleWeights[2] = HdrWeight4(TopRight.rgb, ExposureScale);
        SampleWeights[3] = HdrWeight4(MiddleLeft.rgb, ExposureScale);
        SampleWeights[4] = HdrWeight4(MiddleCenter.rgb, ExposureScale);
        SampleWeights[5] = HdrWeight4(MiddleRight.rgb, ExposureScale);
        SampleWeights[6] = HdrWeight4(BottomLeft.rgb, ExposureScale);
        SampleWeights[7] = HdrWeight4(BottomCenter.rgb, ExposureScale);
        SampleWeights[8] = HdrWeight4(BottomRight.rgb, ExposureScale);
        TopLeft = RGBToYCoCg(TopLeft);
        TopCenter = RGBToYCoCg(TopCenter);
        TopRight = RGBToYCoCg(TopRight);
        MiddleLeft = RGBToYCoCg(MiddleLeft);
        MiddleCenter = RGBToYCoCg(MiddleCenter);
        MiddleRight = RGBToYCoCg(MiddleRight);
        BottomLeft = RGBToYCoCg(BottomLeft);
        BottomCenter = RGBToYCoCg(BottomCenter);
        BottomRight = RGBToYCoCg(BottomRight);


        float TotalWeight = SampleWeights[0] + SampleWeights[1] + SampleWeights[2] + SampleWeights[3] + SampleWeights[4] + SampleWeights[5] + SampleWeights[6] + SampleWeights[7] + SampleWeights[8];                   
        float4 Filtered = (TopLeft * SampleWeights[0] + TopCenter * SampleWeights[1] + TopRight * SampleWeights[2] + MiddleLeft * SampleWeights[3] + MiddleCenter * SampleWeights[4] + MiddleRight * SampleWeights[5] + BottomLeft * SampleWeights[6] + BottomCenter * SampleWeights[7] + BottomRight * SampleWeights[8]) / TotalWeight;
            
        // Resolver Average
        float VelocityWeight = length(velocity);
        float AABBScale = lerp(_TemporalClipBounding.x, _TemporalClipBounding.y, saturate(VelocityWeight * _TemporalClipBounding.z));

        float4 m1 = TopLeft + TopCenter + TopRight + MiddleLeft + MiddleCenter + MiddleRight + BottomLeft + BottomCenter + BottomRight;
        float4 m2 = TopLeft * TopLeft + TopCenter * TopCenter + TopRight * TopRight + MiddleLeft * MiddleLeft + MiddleCenter * MiddleCenter + MiddleRight * MiddleRight + BottomLeft * BottomLeft + BottomCenter * BottomCenter + BottomRight * BottomRight;
        
        float4 mean = m1 / 9;
        float4 stddev = sqrt(m2 / 9 - mean * mean);  

        float4 minColor = mean - AABBScale * stddev;
        float4 maxColor = mean + AABBScale * stddev;
        minColor = min(minColor, Filtered);
        maxColor = max(maxColor, Filtered);

//////////////////TemporalResolver
        float4 CurrColor = YCoCgToRGB(MiddleCenter);

        // Sharpen output
        float4 corners = ( YCoCgToRGB(TopLeft + BottomRight + TopRight + BottomLeft) - CurrColor ) * 2;
        CurrColor += ( CurrColor - (corners * 0.166667) ) * 2.718282 * _Sharpness;
        CurrColor = clamp(CurrColor, 0, HALF_MAX_MINUS1);

        // HistorySample
        float DepthBasedWeight = getClampLerp(PrevCoord, i.texcoord, currDepth, VelocityWeight);
        float4 PrevColor = SAMPLE_TEXTURE2D(_HistoryTex, sampler_HistoryTex, PrevCoord);
        PrevColor.xyz = lerp( PrevColor.xyz, YCoCgToRGB( ClipToAABB( RGBToYCoCg(PrevColor.xyz), minColor.xyz, maxColor.xyz )), DepthBasedWeight );

        // HistoryBlend
        float HistoryWeight = lerp(_FinalBlendParameters.x, _FinalBlendParameters.y, saturate(VelocityWeight * _FinalBlendParameters.z));
        CurrColor.xyz = Tonemap(CurrColor.xyz);
        PrevColor.xyz = Tonemap(PrevColor.xyz);
        float4 TemporalColor = lerp(CurrColor, PrevColor, HistoryWeight);
        TemporalColor.xyz = TonemapInvert(TemporalColor.xyz);
            
        return TemporalColor;
    }

    ENDHLSL

    SubShader
    {
    
        Pass
        {
            Cull Off ZWrite Off ZTest Always
            HLSLPROGRAM

                #pragma vertex vert
                #pragma fragment Solver_CGBullTAA

            ENDHLSL
        }

    }
}
