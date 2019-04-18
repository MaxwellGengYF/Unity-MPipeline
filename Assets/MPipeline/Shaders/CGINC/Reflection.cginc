#ifndef REFLECTION
#define REFLECTION
#define MAXIMUM_PROBE 8
    int DownDimension(uint3 id, const uint2 size, const int multiply){
        const uint3 multiValue = uint3(1, size.x, size.x * size.y) * multiply;
        return dot(id, multiValue);
    }
    TextureCube<float4> _ReflectionCubeMap0; SamplerState sampler_ReflectionCubeMap0;
    TextureCube<float4> _ReflectionCubeMap1; SamplerState sampler_ReflectionCubeMap1;
    TextureCube<float4> _ReflectionCubeMap2; SamplerState sampler_ReflectionCubeMap2;
    TextureCube<float4> _ReflectionCubeMap3; SamplerState sampler_ReflectionCubeMap3;
    TextureCube<float4> _ReflectionCubeMap4; SamplerState sampler_ReflectionCubeMap4;
    TextureCube<float4> _ReflectionCubeMap5; SamplerState sampler_ReflectionCubeMap5;
    TextureCube<float4> _ReflectionCubeMap6; SamplerState sampler_ReflectionCubeMap6;
    TextureCube<float4> _ReflectionCubeMap7; SamplerState sampler_ReflectionCubeMap7;
    Texture2D<float4> _SSR_TemporalCurr_RT; SamplerState sampler_SSR_TemporalCurr_RT;
    float4 GetColor(int index, float3 normal, float lod)
    {
        switch(index)
        {
            case 0:
            return _ReflectionCubeMap0.SampleLevel(sampler_ReflectionCubeMap0, normal, lod);
            case 1:
            return _ReflectionCubeMap1.SampleLevel(sampler_ReflectionCubeMap1, normal, lod);
            case 2:
            return _ReflectionCubeMap2.SampleLevel(sampler_ReflectionCubeMap2, normal, lod);
            case 3:
            return _ReflectionCubeMap3.SampleLevel(sampler_ReflectionCubeMap3, normal, lod);
            case 4:
            return _ReflectionCubeMap4.SampleLevel(sampler_ReflectionCubeMap4, normal, lod);
            case 5:
            return _ReflectionCubeMap5.SampleLevel(sampler_ReflectionCubeMap5, normal, lod);
            case 6:
            return _ReflectionCubeMap6.SampleLevel(sampler_ReflectionCubeMap6, normal, lod);
            default:
            return _ReflectionCubeMap7.SampleLevel(sampler_ReflectionCubeMap7, normal, lod);
        }
    }
    struct ReflectionData
    {
        float3 position;
        float3 minExtent;
        float3 maxExtent;
        float4 hdr;
        float blendDistance;
        int boxProjection;
    };

#ifndef COMPUTE_SHADER
inline half3 MPipelineGI_IndirectSpecular(UnityGIInput data, half occlusion, Unity_GlossyEnvironmentData glossIn, ReflectionData reflData, int currentIndex, float lod)
{
    if(reflData.boxProjection > 0)
    {
        glossIn.reflUVW = BoxProjectedCubemapDirection (glossIn.reflUVW, data.worldPos, data.probePosition[0], data.boxMin[0], data.boxMax[0]);
    }
    float4 env0 = GetColor(currentIndex, glossIn.reflUVW, lod);
    
    /*
        #ifdef UNITY_SPECCUBE_BLENDING
            const float kBlendFactor = 0.99999;
            float blendLerp = data.boxMin[0].w;
            UNITY_BRANCH
            if (blendLerp < kBlendFactor)
            {
                #ifdef UNITY_SPECCUBE_BOX_PROJECTION
                    glossIn.reflUVW = BoxProjectedCubemapDirection (originalReflUVW, data.worldPos, data.probePosition[1], data.boxMin[1], data.boxMax[1]);
                #endif

                half3 env1 = Unity_GlossyEnvironment (UNITY_PASS_TEXCUBE_SAMPLER(unity_SpecCube1,unity_SpecCube0), data.probeHDR[1], glossIn);
                specular = lerp(env1, env0, blendLerp);
            }
            else
            {
                specular = env0;
            }
        #else
            specular = env0;
        #endif
        */
    return DecodeHDR(env0, data.probeHDR[0]) * occlusion;
}
#ifndef __LOCALLIGHTING_INCLUDE__
float2 _CameraClipDistance; //X: Near Y: Far - Near
#endif
StructuredBuffer<uint> _ReflectionIndices;
StructuredBuffer<ReflectionData> _ReflectionData;
float3 CalculateReflection(float linearDepth, float3 worldPos, float3 viewDir, float4 specular, float3 normal, float occlusion, float2 screenUV)
{
	Unity_GlossyEnvironmentData g = UnityGlossyEnvironmentSetup(specular.w, -viewDir, normal, specular.xyz);
	half perceptualRoughness = g.roughness;
	perceptualRoughness = perceptualRoughness * (1.7 - 0.7*perceptualRoughness);
	float lod = perceptualRoughnessToMipmapLevel(perceptualRoughness);;
	half oneMinusReflectivity = 1 - SpecularStrength(specular.xyz);
	UnityGIInput d;
	d.worldPos = worldPos.xyz;
	d.worldViewDir = -viewDir;
	UnityLight light;
	light.color = half3(0, 0, 0);
	light.dir = half3(0, 1, 0);
	UnityIndirect ind;
	ind.diffuse = 0;
    ind.specular = 0;
	float rate = pow(max(0, (linearDepth - _CameraClipDistance.x) / _CameraClipDistance.y), 1.0 / CLUSTERRATE);
    if(rate <= 1.0){
	float3 uv = float3(screenUV, rate);
	uint3 intUV = uv * float3(XRES, YRES, ZRES);
	int index = DownDimension(intUV, uint2(XRES, YRES), MAXIMUM_PROBE + 1);
	int target = _ReflectionIndices[index];
	[loop]
	for (int a = 1; a < target; ++a)
	{
		int currentIndex = _ReflectionIndices[index + a];
		ReflectionData data = _ReflectionData[currentIndex];
		float3 leftDown = data.position - data.maxExtent;
		float3 cubemapUV = (worldPos.xyz - leftDown) / (data.maxExtent * 2);
		if (dot(abs(cubemapUV - saturate(cubemapUV)), 1) > 1e-8) continue;
        
		d.probeHDR[0] = data.hdr;
		if (data.boxProjection > 0)
		{
			d.probePosition[0] = float4(data.position, 1);
			d.boxMin[0].xyz = leftDown;
			d.boxMax[0].xyz = (data.position + data.maxExtent);
		}
		float3 specColor = MPipelineGI_IndirectSpecular(d, occlusion, g, data, currentIndex, lod);
		float3 distanceToMin = saturate((abs(worldPos.xyz - data.position) - data.minExtent) / data.blendDistance);
		ind.specular = lerp(specColor * data.hdr.r, ind.specular, max(distanceToMin.x, max(distanceToMin.y, distanceToMin.z)));
	}
    }
    #if ENABLE_SSR
    float4 ssr = _SSR_TemporalCurr_RT.Sample(sampler_SSR_TemporalCurr_RT, screenUV);    
    ind.specular = lerp(ind.specular, max(0,ssr.rgb * occlusion), saturate(ssr.a));
    #endif
    half3 rgb = BRDF1_Unity_PBS(0, specular.xyz, oneMinusReflectivity, specular.w, normal, -viewDir, light, ind).rgb;
	return rgb;
}
#endif
#endif