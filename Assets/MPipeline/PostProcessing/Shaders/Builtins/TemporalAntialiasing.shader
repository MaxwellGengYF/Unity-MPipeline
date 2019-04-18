Shader "Hidden/PostProcessing/TemporalAntialiasing"
{
    HLSLINCLUDE

        #pragma exclude_renderers gles psp2
    #define half float
    #define half2 float2
    #define half3 float3
    #define half4 float4
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
        Texture2D<float> _LastFrameDepth; SamplerState sampler_LastFrameDepth;
        float4 _CameraDepthTexture_TexelSize;
        float3 _TemporalClipBounding;
        TEXTURE2D_SAMPLER2D(_CameraMotionVectorsTexture, sampler_CameraMotionVectorsTexture);

        float2 _Jitter;
        float4 _FinalBlendParameters; // x: static, y: dynamic, z: motion amplification
        float _Sharpness;

        float4 ClipToAABB(float4 color, float3 minimum, float3 maximum)
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

        OutputSolver Solve(float2 motion, float2 texcoord)
        {
            const float2 k = _MainTex_TexelSize.xy;
            float2 uv = (texcoord - _Jitter);

            float4 color = SAMPLE_TEXTURE2D(_MainTex, _MainTexSampler, uv);

            float4 topLeft = SAMPLE_TEXTURE2D(_MainTex, _MainTexSampler, (uv - k * 0.5));
            float4 bottomRight = SAMPLE_TEXTURE2D(_MainTex, _MainTexSampler, (uv + k * 0.5));

            float4 corners = 4.0 * (topLeft + bottomRight) - 2.0 * color;

            // Sharpen output
            color += (color - (corners * 0.166667)) * 2.718282 * _Sharpness;
            color = clamp(color, 0.0, HALF_MAX_MINUS1);

            // Tonemap color and history samples
            float4 average = (corners + color) * 0.142857;

            float4 history = SAMPLE_TEXTURE2D(_HistoryTex, sampler_HistoryTex, (texcoord - motion));

            float motionLength = length(motion);
            float2 luma = float2(Luminance(average), Luminance(color));
            //float nudge = 4.0 * abs(luma.x - luma.y);
            float nudge = lerp(4.0, 0.25, saturate(motionLength * 100.0)) * abs(luma.x - luma.y);

            float4 minimum = min(bottomRight, topLeft) - nudge;
            float4 maximum = max(topLeft, bottomRight) + nudge;

            // Clip history samples
            history = ClipToAABB(history, minimum.xyz, maximum.xyz);

            // Blend method
            float weight = clamp(
                lerp(_FinalBlendParameters.x, _FinalBlendParameters.y, motionLength * _FinalBlendParameters.z),
                _FinalBlendParameters.y, _FinalBlendParameters.x
            );

            color = lerp(color, history, weight);
            color = clamp(color, 0.0, HALF_MAX_MINUS1);

            OutputSolver output;
            output.destination = color;
            output.history = color;
            return output;
        }
        OutputSolver FragSolverNoDilate(VaryingsDefault i)
        {
            // Don't dilate in ortho !
            float2 motion = SAMPLE_TEXTURE2D(_CameraMotionVectorsTexture, sampler_CameraMotionVectorsTexture, i.texcoordStereo).xy;
            return Solve(motion, i.texcoordStereo);
        }

        /////////////CGBull TemporalAA
        #ifndef AA_VARIANCE
            #define AA_VARIANCE 1
        #endif

        #ifndef AA_Filter
            #define AA_Filter 1
        #endif

        inline half Luma4(half3 Color)
        {
            return (Color.g * 2) + (Color.r + Color.b);
        }

        inline half HdrWeight4(half3 Color, const half Exposure) 
        {
            return rcp(Luma4(Color) * Exposure + 4);
        }
        
        #define SAMPLE_DEPTH_OFFSET(x,y,z,a) (x.Sample(y,z,a).r )
        half2 ReprojectedMotionVectorUV(half2 uv)
        {
            const float2 k = _CameraDepthTexture_TexelSize.xy;
            half neighborhood[8];
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
            half3 result = half3(0, 0,  _CameraDepthTexture.Sample(sampler_CameraDepthTexture, uv).x);

            result = lerp(result, half3(-1, -1, neighborhood[0]), COMPARE_DEPTH(neighborhood[0], result.z));
            result = lerp(result, half3(0, -1, neighborhood[1]), COMPARE_DEPTH(neighborhood[1], result.z));
            result = lerp(result, half3(1, -1, neighborhood[2]), COMPARE_DEPTH(neighborhood[2], result.z));
            result = lerp(result, half3(-1, 0, neighborhood[3]), COMPARE_DEPTH(neighborhood[3], result.z));
            result = lerp(result, half3(1, 1, neighborhood[4]), COMPARE_DEPTH(neighborhood[4], result.z));
            result = lerp(result, half3(1, 0, neighborhood[5]), COMPARE_DEPTH(neighborhood[5], result.z));
            result = lerp(result, half3(-1, 1, neighborhood[6]), COMPARE_DEPTH(neighborhood[6], result.z));
            result = lerp(result, half3(0, -1, neighborhood[7]), COMPARE_DEPTH(neighborhood[7], result.z));

            return (uv + result.xy * k);
        }
        #define SAMPLE_TEXTURE2D_OFFSET(x,y,z,a) (x.Sample(y,z,a))
        float4 Solver_CGBullTAA(v2f i) : SV_TARGET0
        {
            const half ExposureScale = 10;
            half2 uv = (i.texcoord - _Jitter);
            half2 screenSize = _ScreenParams.xy;
            half2 closest = ReprojectedMotionVectorUV(i.texcoord);
            half2 velocity = SAMPLE_TEXTURE2D(_CameraMotionVectorsTexture, sampler_CameraMotionVectorsTexture, closest).xy;
//////////////////TemporalClamp
            half4 MiddleCenter = SAMPLE_TEXTURE2D(_MainTex, _MainTexSampler, uv);
            half2 lastFrameUV = (i.texcoord - velocity);
            if (lastFrameUV.x > 1 || lastFrameUV.y > 1 || lastFrameUV.x < 0 || lastFrameUV.y < 0)
            {
                return MiddleCenter;
            }
            half4 TopLeft = SAMPLE_TEXTURE2D_OFFSET(_MainTex, _MainTexSampler, uv, int2(-1, -1));
            half4 TopCenter = SAMPLE_TEXTURE2D_OFFSET(_MainTex, _MainTexSampler, uv, int2(0, -1));
            half4 TopRight = SAMPLE_TEXTURE2D_OFFSET(_MainTex, _MainTexSampler, uv, int2(1, -1));
            half4 MiddleLeft = SAMPLE_TEXTURE2D_OFFSET(_MainTex, _MainTexSampler, uv, int2(-1,  0));
            
            half4 MiddleRight = SAMPLE_TEXTURE2D_OFFSET(_MainTex, _MainTexSampler, uv, int2(1,  0));
            half4 BottomLeft = SAMPLE_TEXTURE2D_OFFSET(_MainTex, _MainTexSampler, uv, int2(-1, 1));
            half4 BottomCenter = SAMPLE_TEXTURE2D_OFFSET(_MainTex, _MainTexSampler, uv, int2(0, 1));
            half4 BottomRight = SAMPLE_TEXTURE2D_OFFSET(_MainTex, _MainTexSampler, uv, int2(1, 1));

            half SampleWeights[9];
            SampleWeights[0] = HdrWeight4(TopLeft.rgb, ExposureScale);
            SampleWeights[1] = HdrWeight4(TopCenter.rgb, ExposureScale);
            SampleWeights[2] = HdrWeight4(TopRight.rgb, ExposureScale);
            SampleWeights[3] = HdrWeight4(MiddleLeft.rgb, ExposureScale);
            SampleWeights[4] = HdrWeight4(MiddleCenter.rgb, ExposureScale);
            SampleWeights[5] = HdrWeight4(MiddleRight.rgb, ExposureScale);
            SampleWeights[6] = HdrWeight4(BottomLeft.rgb, ExposureScale);
            SampleWeights[7] = HdrWeight4(BottomCenter.rgb, ExposureScale);
            SampleWeights[8] = HdrWeight4(BottomRight.rgb, ExposureScale);
            half TotalWeight = SampleWeights[0] + SampleWeights[1] + SampleWeights[2] 
                               + SampleWeights[3] + SampleWeights[4] + SampleWeights[5] 
                               + SampleWeights[6] + SampleWeights[7] + SampleWeights[8];  
                                
            half4 Filtered = (TopLeft * SampleWeights[0] + TopCenter * SampleWeights[1]
                            + TopRight * SampleWeights[2] + MiddleLeft * SampleWeights[3] 
                            + MiddleCenter * SampleWeights[4] + MiddleRight * SampleWeights[5] 
                            + BottomLeft * SampleWeights[6] + BottomCenter * SampleWeights[7] 
                            + BottomRight * SampleWeights[8]) / TotalWeight;
            // Resolver Average
            half4 minColor, maxColor;
            half4 m1, m2, mean, stddev;
            float lenOfVelocity = length(velocity);
            half AABBScale = lerp(_TemporalClipBounding.x, _TemporalClipBounding.y, saturate(lenOfVelocity * _TemporalClipBounding.z));
            m1 = TopLeft + TopCenter + TopRight + MiddleLeft + MiddleCenter + MiddleRight + BottomLeft + BottomCenter + BottomRight;
            m2 = TopLeft * TopLeft + TopCenter * TopCenter
                + TopRight * TopRight + MiddleLeft * MiddleLeft
                + MiddleCenter * MiddleCenter + MiddleRight * MiddleRight + BottomLeft * BottomLeft + BottomCenter * BottomCenter + BottomRight * BottomRight;
                
            mean = m1 / 9;
            stddev = sqrt(m2 / 9 - mean * mean);  
            minColor = mean - AABBScale * stddev;
            maxColor = mean + AABBScale * stddev;
            minColor = min(minColor, Filtered);
            maxColor = max(maxColor, Filtered);

//////////////////TemporalResolver
            half4 currColor = MiddleCenter;
            // Sharpen output
            half4 corners = ((TopLeft + BottomRight + TopRight + BottomLeft) - currColor) * 2;
            currColor += (currColor - (corners * 0.166667)) * 2.718282 * _Sharpness;
            currColor = clamp(currColor, 0, HALF_MAX_MINUS1);
            // HistorySample
            half4 lastColor = SAMPLE_TEXTURE2D(_HistoryTex, sampler_HistoryTex, lastFrameUV);
            lastColor = clamp(lastColor, minColor, maxColor);
            // HistoryBlend
            half weight = clamp(lerp(_FinalBlendParameters.x, _FinalBlendParameters.y, lenOfVelocity * _FinalBlendParameters.z), _FinalBlendParameters.y, _FinalBlendParameters.x);
            half4 temporalColor = lerp(currColor, lastColor, weight);
            return temporalColor;
        }

    ENDHLSL

    SubShader
    {
        

        // 0: Perspective
        Pass
        {
            Cull Off ZWrite Off ZTest Always
            HLSLPROGRAM

                #pragma vertex vert
                //#pragma fragment FragSolverDilate
                #pragma fragment Solver_CGBullTAA

            ENDHLSL
        }

        // 1: Ortho
        Pass
        {
            Cull Off ZWrite Off ZTest Greater
            HLSLPROGRAM

                #pragma vertex VertDefault
                #pragma fragment FragSolverNoDilate

            ENDHLSL
        }
    }
}
