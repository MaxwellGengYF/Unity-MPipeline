#ifndef _Filtter_Library_
#define _Filtter_Library_

#include "Common.hlsl"

inline half min3(half a, half b, half c)
{
    return min(min(a, b), c);
}

inline half max3(half a, half b, half c)
{
    return max(a, max(b, c));
}

inline half4 min3(half4 a, half4 b, half4 c)
{
    return half4(
        min3(a.x, b.x, c.x),
        min3(a.y, b.y, c.y),
        min3(a.z, b.z, c.z),
        min3(a.w, b.w, c.w));
}

inline half4 max3(half4 a, half4 b, half4 c)
{
    return half4(
        max3(a.x, b.x, c.x),
        max3(a.y, b.y, c.y),
        max3(a.z, b.z, c.z),
        max3(a.w, b.w, c.w));
}

inline half Luma4(half3 Color)
{
    return (Color.g * 2) + (Color.r + Color.b);
}

inline half HdrWeight4(half3 Color, half Exposure)
{
    return rcp(Luma4(Color) * Exposure + 4);
}

inline half HdrWeightY(half Color, half Exposure)
{
    return rcp(Color * Exposure + 4);
}

inline half3 RGBToYCoCg(half3 RGB)
{
    half Y = dot(RGB, half3(1, 2, 1));
    half Co = dot(RGB, half3(2, 0, -2));
    half Cg = dot(RGB, half3(-1, 2, -1));

    half3 YCoCg = half3(Y, Co, Cg);
    return YCoCg;
}

inline half3 YCoCgToRGB(half3 YCoCg)
{
    half Y = YCoCg.x * 0.25;
    half Co = YCoCg.y * 0.25;
    half Cg = YCoCg.z * 0.25;

    half R = Y + Co - Cg;
    half G = Y + Cg;
    half B = Y - Co - Cg;

    half3 RGB = half3(R, G, B);
    return RGB;
}

#define RANDOM(seed) (sin(cos(seed * 1354.135748 + 13.546184) * 1354.135716 + 32.6842317))
half2 GetRandomSequencer(half2 uv, half RandomSeed)
{
	return RANDOM((_ScreenParams.y * uv.y + uv.x) * _ScreenParams.x + RandomSeed);
}

half2 GetRandomSequencer(half2 uv, half2 screenSize, half RandomSeed)
{
	return RANDOM((screenSize.y * uv.y + uv.x) * screenSize.x + RandomSeed);
}


//////Sharpe
inline half Sharpe(sampler2D sharpColor, half sharpness, half2 Resolution, half2 UV)
{
    half2 step = 1 / Resolution.xy;

    half3 texA = tex2D(sharpColor, UV + half2(-step.x, -step.y) * 1.5);
    half3 texB = tex2D(sharpColor, UV + half2(step.x, -step.y) * 1.5);
    half3 texC = tex2D(sharpColor, UV + half2(-step.x, step.y) * 1.5);
    half3 texD = tex2D(sharpColor, UV + half2(step.x, step.y) * 1.5);

    half3 around = 0.25 * (texA + texB + texC + texD);
    half4 center = tex2D(sharpColor, UV);

    half3 color = center.rgb + (center.rgb - around) * sharpness;
    return half4(color, center.a);
}

//////Gaussian
inline half4 draw(half2 uv, sampler2D Color)
{
    return tex2D(Color, uv);
}

inline half grid(half var, half size)
{
    return floor(var * size) / size;
}

inline half rand(half2 co)
{
    return frac(sin(dot(co.xy, half2(12.9898, 78.233))) * 43758.5453);
}

inline half4 GaussianBlur(half bluramount, half2 uv, sampler2D Color)
{
    half4 blur_Color = 0;
#define repeats 60.
    for (float i = 0.; i < repeats; i++)
    {
        half2 q = half2(cos(degrees((i / repeats) * 360)), sin(degrees((i / repeats) * 360))) * (rand(half2(i, uv.x + uv.y)) + bluramount);
        half2 uv2 = uv + (q * bluramount);
        blur_Color += draw(uv2, Color) / 2;

        q = half2(cos(degrees((i / repeats) * 360)), sin(degrees((i / repeats) * 360))) * (rand(half2(i + 2, uv.x + uv.y + 24)) + bluramount);
        uv2 = uv + (q * bluramount);
        blur_Color += draw(uv2, Color) / 2;
    }
    blur_Color /= repeats;
    return blur_Color;
}

//////Bilateral
#define Blur_Sharpness 5
#define Blur_Radius 0.05
#define Blur_Size 12

inline half CrossBilateralWeight_1(half x, half Sharp)
{
    return 0.39894 * exp(-0.5 * x * x / (Sharp * Sharp)) / Sharp;
}

inline half CrossBilateralWeight_2(half3 v, half Sharp)
{
    return 0.39894 * exp(-0.5 * dot(v, v) / (Sharp * Sharp)) / Sharp;
}

inline half4 BilateralClearUp(sampler2D Color, half2 Resolution, half2 uv)
{
    half4 originColor = tex2D(Color, uv);

    half kernel[Blur_Size];
    const int kernelSize = (Blur_Size - 1) / 2;

    //UNITY_UNROLL
    for (int j = 0; j <= kernelSize; j++)
    {
        kernel[kernelSize + j] = kernel[kernelSize - j] = CrossBilateralWeight_1(half(j), Blur_Sharpness);
    }

    half weight, Num_Weight;
    half4 blurColor, final_colour;

    //UNITY_UNROLL
    for (int i = -kernelSize; i <= kernelSize; i++)
    {
        //UNITY_UNROLL
        for (int j = -kernelSize; j <= kernelSize; j++)
        {
            blurColor = tex2Dlod(Color, half4( ( (uv * Resolution) + half2( half(i), half(j) ) ) / Resolution, 0, 0) );
            weight = CrossBilateralWeight_2(blurColor - originColor, Blur_Radius) * kernel[kernelSize + j] * kernel[kernelSize + i];
            Num_Weight += weight;
            final_colour += weight * blurColor;
        }
    }
    return final_colour / Num_Weight;
}

///////////////Temporal filtter
#ifndef AA_VARIANCE
	#define AA_VARIANCE 1
#endif

#ifndef AA_Filter
    #define AA_Filter 0
#endif

half2 ReprojectedMotionVectorUV(sampler2D _DepthTexture, half2 uv, half2 screenSize)
{
    half neighborhood[9];
    neighborhood[0] = tex2D(_DepthTexture, uv + (int2(-1, -1) / screenSize)).z;
    neighborhood[1] = tex2D(_DepthTexture, uv + (int2(0, -1) / screenSize)).z;
    neighborhood[2] = tex2D(_DepthTexture, uv + (int2(1, -1) / screenSize)).z;
    neighborhood[3] = tex2D(_DepthTexture, uv + (int2(-1, 0) / screenSize)).z;
    neighborhood[5] = tex2D(_DepthTexture, uv + (int2(1, 0) / screenSize)).z;
    neighborhood[6] = tex2D(_DepthTexture, uv + (int2(-1, 1) / screenSize)).z;
    neighborhood[7] = tex2D(_DepthTexture, uv + (int2(0, -1) / screenSize)).z;
    neighborhood[8] = tex2D(_DepthTexture, uv + (int2(1, 1) / screenSize)).z;

#if defined(UNITY_REVERSED_Z)
    #define COMPARE_DEPTH(a, b) step(b, a)
#else
    #define COMPARE_DEPTH(a, b) step(a, b)
#endif

    half3 result = half3(0, 0, tex2D(_DepthTexture, uv).z);

    result = lerp(result, half3(-1, -1, neighborhood[0]), COMPARE_DEPTH(neighborhood[0], result.z));
    result = lerp(result, half3(0, -1, neighborhood[1]), COMPARE_DEPTH(neighborhood[1], result.z));
    result = lerp(result, half3(1, -1, neighborhood[2]), COMPARE_DEPTH(neighborhood[2], result.z));
    result = lerp(result, half3(-1, 0, neighborhood[3]), COMPARE_DEPTH(neighborhood[3], result.z));
    result = lerp(result, half3(1, 0, neighborhood[5]), COMPARE_DEPTH(neighborhood[5], result.z));
    result = lerp(result, half3(-1, 1, neighborhood[6]), COMPARE_DEPTH(neighborhood[6], result.z));
    result = lerp(result, half3(0, -1, neighborhood[7]), COMPARE_DEPTH(neighborhood[7], result.z));
    result = lerp(result, half3(1, 1, neighborhood[8]), COMPARE_DEPTH(neighborhood[8], result.z));

    return (uv + result.xy * screenSize);
}

inline void ResolverAABB(sampler2D currColor, half Sharpness, half ExposureScale, half AABBScale, half2 uv, half2 screenSize, inout half4 minColor, inout half4 maxColor, inout half4 filterColor)
{
    half4 TopLeft = tex2D(currColor, uv + (int2(-1, -1) / screenSize));
    half4 TopCenter = tex2D(currColor, uv + (int2(0, -1) / screenSize));
    half4 TopRight = tex2D(currColor, uv + (int2(1, -1) / screenSize));
    half4 MiddleLeft = tex2D(currColor, uv + (int2(-1,  0) / screenSize));
    half4 MiddleCenter = tex2D(currColor, uv + (int2(0,  0) / screenSize));
    half4 MiddleRight = tex2D(currColor, uv + (int2(1,  0) / screenSize));
    half4 BottomLeft = tex2D(currColor, uv + (int2(-1,  1) / screenSize));
    half4 BottomCenter = tex2D(currColor, uv + (int2(0,  1) / screenSize));
    half4 BottomRight = tex2D(currColor, uv + (int2(1,  1) / screenSize));

    //
        minColor = min(TopLeft, min(TopCenter, min(TopRight, min(MiddleLeft, min(MiddleCenter, min(MiddleRight, min(BottomLeft, min(BottomCenter, BottomRight))))))));
        maxColor = max(TopLeft, max(TopCenter, max(TopRight, max(MiddleLeft, max(MiddleCenter, max(MiddleRight, max(BottomLeft, max(BottomCenter, BottomRight))))))));
            
        half4 center = (minColor + maxColor) * 0.5;
        minColor = (minColor - center) * AABBScale + center;
        maxColor = (maxColor - center) * AABBScale + center;

    //


        filterColor = MiddleCenter;
        minColor = min(minColor, MiddleCenter);
        maxColor = max(maxColor, MiddleCenter);

}


inline void ResolverAABB(sampler2D currColor, half Sharpness, half ExposureScale, half AABBScale, half2 uv, half2 TexelSize, inout half Variance, inout half4 MinColor, inout half4 MaxColor, inout half4 FilterColor)
{
    const int2 SampleOffset[9] = {int2(-1.0, -1.0), int2(0.0, -1.0), int2(1.0, -1.0), int2(-1.0, 0.0), int2(0.0, 0.0), int2(1.0, 0.0), int2(-1.0, 1.0), int2(0.0, 1.0), int2(1.0, 1.0)};

    half4 SampleColors[9];

    for(uint i = 0; i < 9; i++) {
        #if AA_BicubicFilter
            SampleColors[i] = Texture2DSampleBicubic(currColor, uv + ( SampleOffset[i] / TexelSize), BicubicSize.xy, BicubicSize.zw);
        #else
            SampleColors[i] = tex2D( currColor, uv + ( SampleOffset[i] / TexelSize) );
        #endif
    }

    #if AA_Filter
        half SampleWeights[9];
        for(uint j = 0; j < 9; j++) {
            SampleWeights[j] = HdrWeight4(SampleColors[j].rgb, ExposureScale);
        }

        half TotalWeight = 0;
        for(uint k = 0; k < 9; k++) {
            TotalWeight += SampleWeights[k];
        }  

        SampleColors[4] = (SampleColors[0] * SampleWeights[0] + SampleColors[1] * SampleWeights[1] + SampleColors[2] * SampleWeights[2] 
                        +  SampleColors[3] * SampleWeights[3] + SampleColors[4] * SampleWeights[4] + SampleColors[5] * SampleWeights[5] 
                        +  SampleColors[6] * SampleWeights[6] + SampleColors[7] * SampleWeights[7] + SampleColors[8] * SampleWeights[8]) / TotalWeight;
    #endif

    half4 m1 = 0.0; half4 m2 = 0.0;
    for(uint x = 0; x < 9; x++)
    {
        m1 += SampleColors[x];
        m2 += SampleColors[x] * SampleColors[x];
    }

    half4 mean = m1 / 9.0;
    half4 stddev = sqrt( (m2 / 9.0) - pow2(mean) );
        
    MinColor = mean - AABBScale * stddev;
    MaxColor = mean + AABBScale * stddev;

    FilterColor = SampleColors[4];
    MinColor = min(MinColor, FilterColor);
    MaxColor = max(MaxColor, FilterColor);

    half4 TotalVariance = 0;
    for(uint z = 0; z < 9; z++)
    {
        TotalVariance += pow2(SampleColors[z] - mean);
    }
    Variance = saturate( Luminance(TotalVariance / 4) * 256 );
    Variance *= FilterColor.a;
}

    //half4 corners = 4 * (TopLeft + BottomRight) - 2 * filterColor;
    //filterColor += (filterColor - (corners * 0.166667)) * 2.718282 * (Sharpness * 0.25);
    
#endif