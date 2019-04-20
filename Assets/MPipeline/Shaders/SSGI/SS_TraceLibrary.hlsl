#ifndef _SSTrace_IBRARY_
#define _SSTrace_IBRARY_

#include "UnityStandardBRDF.cginc"

inline half GetScreenFadeBord(half2 pos, half value)
{
    half borderDist = min(1 - max(pos.x, pos.y), min(pos.x, pos.y));
    return saturate(borderDist > value ? 1 : borderDist / value);
}

uint3 Rand3DPCG16(int3 p)
{
	uint3 v = uint3(p);

	v = v * 1664525u + 1013904223u;
	v.x += v.y*v.z;
	v.y += v.z*v.x;
	v.z += v.x*v.y;
	v.x += v.y*v.z;
	v.y += v.z*v.x;
	v.z += v.x*v.y;

	return v >> 16u;
}


/////////////////////////////////////LInear3DTrace/////////////////////////////////////
float4 LinearTraceRay3DSpace(Texture2D _DepthTexture, SamplerState sampler_DepthTexture, int NumSteps, float2 BlueNoise, float3 rayPos, float3 rayDir) {
	float mask = 0.0, endDepth = 0.0;
    float rayDepth = rayPos.z;

	float2 jitter = BlueNoise + 0.5;
	float StepSize = 1 / (float)NumSteps;
	StepSize = StepSize * (jitter.x + jitter.y) + StepSize;

    UNITY_LOOP
	for (int i = 0;  i < NumSteps; i++) {
		endDepth = Texture2DSampleLevel(_DepthTexture, sampler_DepthTexture, rayPos.xy, 0.0);
		if (rayDepth > endDepth) {
			mask = 1;
			break;
		}
		rayPos += rayDir * StepSize;
        rayDepth = rayPos.z;
	}
	return float4(rayPos, mask);
}

/////////////////////////////////////Hierarchical_Z Trace/////////////////////////////////////
float3 intersectDepth_Plane(float3 rayOrigin, float3 rayDir, float marchSize)
{
	return rayOrigin + rayDir * marchSize;
}

float2 cell(float2 ray, float2 cell_count) {
	return floor(ray.xy * cell_count);
}

float2 cell_count(float level, float2 ScreenSize) {
	return ScreenSize / (level == 0 ? 1 : exp2(level));
}

float3 intersect_cell_boundary(float3 rayOrigin, float3 rayDir, float2 cellIndex, float2 cellCount, float2 crossStep, float2 crossOffset)
{
    float2 cell_size = 1.0 / cellCount;
    float2 planes = cellIndex / cellCount + cell_size * crossStep;

    float2 solutions = (planes - rayOrigin) / rayDir.xy;
    float3 intersection_pos = rayOrigin + rayDir * min(solutions.x, solutions.y);

    intersection_pos.xy += (solutions.x < solutions.y) ? float2(crossOffset.x, 0.0) : float2(0.0, crossOffset.y);

    return intersection_pos;
}

bool crossed_cell_boundary(float2 cell_id_one, float2 cell_id_two) {
	return (int)cell_id_one.x != (int)cell_id_two.x || (int)cell_id_one.y != (int)cell_id_two.y;
}

float minimum_depth_plane(float2 ray, float level, float2 cell_count, Texture2D SceneDepth) {
	return -SceneDepth.Load( int3( (ray * cell_count), level ) );
}

float4 Hierarchical_Z_Trace_SSGI(int HiZ_Max_Level, int HiZ_Start_Level, int HiZ_Stop_Level, int NumSteps, float Thickness, float2 screenSize, float3 rayOrigin, float3 rayDir, Texture2D SceneDepth)
{
    HiZ_Max_Level = clamp(HiZ_Max_Level, 0.0, 7.0);
    rayOrigin = half3(rayOrigin.x, rayOrigin.y, -rayOrigin.z);
    rayDir = half3(rayDir.x, rayDir.y, -rayDir.z);

    float level = HiZ_Start_Level;
    float2 hi_z_size = cell_count(level, screenSize);
    float3 ray = rayOrigin;

    float2 cross_step = float2(rayDir.x >= 0.0 ? 1.0 : -1.0, rayDir.y >= 0.0 ? 1.0 : -1.0);
    float2 cross_offset = cross_step * 0.00001;
    cross_step = saturate(cross_step);

    float2 ray_cell = cell(ray.xy, hi_z_size.xy);
    ray = intersect_cell_boundary(ray, rayDir, ray_cell, hi_z_size, cross_step, cross_offset);

    int iterations = 0;
    float mask = 1.0;

    while(level >= HiZ_Stop_Level && iterations < NumSteps) {
        float3 tmp_ray = ray;
        float2 current_cell_count = cell_count(level, screenSize);
        float2 old_cell_id = cell(ray.xy, current_cell_count);
        float min_z = minimum_depth_plane(ray.xy, level, current_cell_count, SceneDepth);

        if(rayDir.z > 0) 
        {
            float min_minus_ray = min_z - ray.z;
            tmp_ray = min_minus_ray > 0 ? ray + (rayDir / rayDir.z) * min_minus_ray : tmp_ray;
            float2 new_cell_id = cell(tmp_ray.xy, current_cell_count);
        
            if(crossed_cell_boundary(old_cell_id, new_cell_id)) {
                tmp_ray = intersect_cell_boundary(ray, rayDir, old_cell_id, current_cell_count, cross_step, cross_offset);
                level = min(HiZ_Max_Level, level + 2.0);
            }/* else {
                if(level == 1.0 && abs(min_minus_ray) > 0.0001) {
                    tmp_ray = intersect_cell_boundary(ray, rayDir, old_cell_id, current_cell_count, cross_step, cross_offset);
                    level = 2.0;
                    //mask = 0.0;
                }
            }*/
        } else if(ray.z < min_z) {
            tmp_ray = intersect_cell_boundary(ray, rayDir, old_cell_id, current_cell_count, cross_step, cross_offset);
            level = min(HiZ_Max_Level, level + 2.0);
            //mask = 0.0;
        }

        ray.xyz = tmp_ray.xyz;
        level--;
        iterations++;
    }

    return half4(ray.xy, -ray.z, mask);
}
/*
float3 Hierarchical_Z_Trace(int HiZ_Max_Level, int HiZ_Start_Level, int HiZ_Stop_Level, int NumSteps, float2 screenSize, float3 rayOrigin, float3 rayDir, Texture2D SceneDepth)
{
	float level = HiZ_Start_Level;
	float2 crossStep = float2(rayDir.x >= 0.0 ? 1.0 : -1.0, rayDir.y >= 0.0 ? 1.0 : -1.0);
	float2 crossOffset = float2(crossStep.xy * screenSize); 
    crossStep.xy = saturate(crossStep.xy);

	float3 ray = rayOrigin.xyz;

	float3 marchSize = rayDir.xyz / rayDir.z;
	float3 curr_RayOrigin = intersectDepth_Plane(rayOrigin, marchSize, -rayOrigin.z);
	float2 rayCell = cell(ray.xy, screenSize);

	ray = intersect_cell_boundary(curr_RayOrigin, marchSize, rayCell.xy, screenSize, crossStep.xy, crossOffset.xy);
        
    int iterations = 0.0; float3 tmpRay = 0.0;
    
    [loop]
	while(level >=  HiZ_Stop_Level && iterations < NumSteps)
	{
        float2 cellCount = cell_count(level, screenSize);
		float minZ = minimum_depth_plane(ray.xy, cellCount, level, SceneDepth);
		float2 oldCellIdx = cell(ray.xy, cellCount);

		tmpRay = intersectDepth_Plane(curr_RayOrigin, marchSize, max(ray.z, minZ));
		float2 newCellIdx = cell(tmpRay.xy, cellCount);

        [branch]
		if(crossed_cell_boundary(oldCellIdx, newCellIdx))
		{
		    tmpRay = intersect_cell_boundary(curr_RayOrigin, rayDir, oldCellIdx, cellCount.xy, crossStep.xy, crossOffset.xy);
			level = min(HiZ_Max_Level, level + 2.0);
		}

		ray.xyz = tmpRay.xyz;
		level--;
		iterations++;
	}
	return ray;
}
*/
float GetMarchSize(float2 start,float2 end,float2 SamplerPos)
{
    float2 dir = abs(end - start);
    return length( float2( min(dir.x, SamplerPos.x), min(dir.y, SamplerPos.y) ) );
}

float4 Hierarchical_Z_Trace_SSGI(int HiZ_Max_Level, int HiZ_Start_Level, int HiZ_Stop_Level, int NumSteps, float thickness, float2 RayCastSize, float3 rayStart, float3 rayDir, Texture2D SceneDepth, SamplerState SceneDepth_Sampler)
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

#endif