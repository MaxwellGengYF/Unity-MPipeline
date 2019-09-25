#include "UnityStandardBRDF.cginc"

inline half GetScreenFadeBord(half2 pos, half value)
{
    half borderDist = min(1 - max(pos.x, pos.y), min(pos.x, pos.y));
    return saturate(borderDist > value ? 1 : borderDist / value);
}

float calcLOD(int cubeSize, float pdf, int NumSamples)
{
	float lod = (0.5 * log2( (cubeSize * cubeSize) / float(NumSamples) ) + 2.0) - 0.5 * log2(pdf); 
	return lod;
}

float specularPowerToConeAngle(float specularPower)
{
    const float xi = 0.244;
	float exponent = 1.0 / (specularPower + 1.0);
	return acos(pow(xi, exponent));
}

float isoscelesTriangleOpposite(float adjacentLength, float coneTheta)
{
	 return 2.0 * tan(coneTheta) * adjacentLength;
}
 
float isoscelesTriangleInRadius(float a, float h)
{
	float a2 = a * a;
	float fh2 = 4.0 * h * h;
	return (a * (sqrt(a2 + fh2) - a)) / (4.0 * h);
}

float isoscelesTriangleNextAdjacent(float adjacentLength, float incircleRadius)
{
	return adjacentLength - (incircleRadius * 2.0);
}

/////////////////////////////////////Hierarchical_Z Trace/////////////////////////////////////
float2 cell(float2 ray, float2 cell_count) {
	return floor(ray.xy * cell_count);
}

float2 cell_count(float level, float2 ScreenSize) {
	return ScreenSize / (level == 0 ? 1 : exp2(level));
}

float3 intersectDepth_Plane(float3 rayOrigin, float3 rayDir, float marchSize)
{
	return rayOrigin + rayDir * marchSize;
}

float3 intersect_cell_boundary(float3 rayOrigin, float3 rayDir, float2 cellIndex, float2 cellCount, float2 crossStep, float2 crossOffset)
{
	float2 index = cellIndex + crossStep;  
        index /= cellCount;  
        index += crossOffset;

	float2 delta = index - rayOrigin.xy; 
        delta /= rayDir.xy;

	float marchSize = min(delta.x, delta.y);
	return intersectDepth_Plane(rayOrigin, rayDir, marchSize);
}

bool crossed_cell_boundary(float2 cell_id_one, float2 cell_id_two) {
	return (int)cell_id_one.x != (int)cell_id_two.x || (int)cell_id_one.y != (int)cell_id_two.y;
}

float minimum_depth_plane(float2 ray, float2 cell_count, float level, Texture2D SceneDepth) {
	return SceneDepth.Load( int3( (ray * cell_count), level ) );
}

float GetMarchSize(float2 start,float2 end,float2 SamplerPos)
{
    float2 dir = abs(end - start);
    return length( float2( min(dir.x, SamplerPos.x), min(dir.y, SamplerPos.y) ) );
}

float4 Hierarchical_Z_Trace(int HiZ_Max_Level, int HiZ_Start_Level, int HiZ_Stop_Level, int NumSteps, float thickness, float2 RayCastSize, float3 rayStart, float3 rayDir, Texture2D SceneDepth, SamplerState SceneDepth_Sampler)
{
    float SamplerSize = GetMarchSize(rayStart.xy, rayStart.xy + rayDir.xy, RayCastSize);
    float3 samplePos = rayStart + rayDir * (SamplerSize);
    int level = HiZ_Start_Level; float mask = 0.0;

    UNITY_LOOP
    for (int i = 0; i < NumSteps; i++)
    {
        float2 currSamplerPos = RayCastSize * exp2(level + 1.0);
        float newSamplerSize = GetMarchSize(samplePos.xy, samplePos.xy + rayDir.xy, currSamplerPos);
        float3 newSamplePos = samplePos + rayDir * newSamplerSize;
        float sampleMinDepth = Texture2DSampleLevel(SceneDepth, SceneDepth_Sampler, newSamplePos.xy, level);

        UNITY_FLATTEN
        if (sampleMinDepth < newSamplePos.z) {
            level = min(HiZ_Max_Level, level + 1.0);
            samplePos = newSamplePos;
        } else {
            level--;
        }

        UNITY_BRANCH
        if (level < HiZ_Stop_Level) {
            float delta = (-LinearEyeDepth(sampleMinDepth)) - (-LinearEyeDepth(samplePos.z));
            mask = delta <= thickness && i > 0.0;
            return float4(samplePos, mask);
        }
    }
    return float4(samplePos, mask);
}