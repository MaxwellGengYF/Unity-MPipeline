#ifndef __LOCALLIGHTING_INCLUDE__
#define __LOCALLIGHTING_INCLUDE__
StructuredBuffer<PointLight> _AllPointLight;
StructuredBuffer<uint> _PointLightIndexBuffer;
StructuredBuffer<SpotLight> _AllSpotLight;
StructuredBuffer<uint> _SpotLightIndexBuffer;
float2 _CameraClipDistance; //X: Near Y: Far - Near
TextureCubeArray<float> _CubeShadowMapArray; SamplerState sampler_CubeShadowMapArray;
//UNITY_SAMPLE_SHADOW
Texture2DArray<float> _SpotMapArray; SamplerComparisonState sampler_SpotMapArray;
Texture2DArray<float> _IESAtlas; SamplerState sampler_IESAtlas;
static const float _ShadowSampler = 8.0;
float4 _LightEnabled;
float3 CalculateLocalLight(float2 uv, float4 WorldPos, float linearDepth, float3 WorldNormal, float3 ViewDir, GeometryBuffer buffer)
{
	float ShadowTrem = 0;
	float3 ShadingColor = 0;
	float rate = pow(max(0, (linearDepth - _CameraClipDistance.x) / _CameraClipDistance.y), 1.0 / CLUSTERRATE);
    if(rate > 1) return 0;
	uint3 voxelValue = uint3((uint2)(uv * float2(XRES, YRES)), (uint)(rate * ZRES));
	uint sb = GetIndex(voxelValue, VOXELSIZE, (MAXLIGHTPERCLUSTER + 1));
	uint2 LightIndex;// = uint2(sb + 1, _PointLightIndexBuffer[sb]);
	uint c;
	BSDFContext LightData = (BSDFContext)0;
	InitGeoData(LightData, WorldNormal, ViewDir);

if(_LightEnabled.w > 0.5){
	float2 JitterSpot = uv;
	LightIndex = uint2(sb + 1, _SpotLightIndexBuffer[sb]);
	[loop]
	for (c = LightIndex.x; c < LightIndex.y; c++)
	{
		SpotLight Light = _AllSpotLight[_SpotLightIndexBuffer[c]];
		Cone SpotCone = Light.lightCone;

		float LightRange = SpotCone.height;
		float3 LightPos = SpotCone.vertex;
		float3 LightColor = Light.lightColor;
		int iesIndex = Light.iesIndex;
		float LightAngle = cos(Light.angle);
		float3 LightForward = SpotCone.direction;
		float3 Un_LightDir = LightPos - WorldPos.xyz;
		float lightDirLen = length(Un_LightDir);
		float3 LightDir = Un_LightDir / lightDirLen;
		float3 floatDir = normalize(ViewDir + LightDir);
		float ldh = -dot(LightDir, SpotCone.direction);
		float isNear =  dot(-Un_LightDir, SpotCone.direction) > Light.nearClip;
		if(iesIndex >= 0)
		{
			float iesUV  = ComputeLightProfileMultiplier(WorldPos.xyz, LightPos, LightForward, Light.angle);
			LightColor *= _IESAtlas.SampleLevel(sampler_IESAtlas, float3(iesUV, 0.5, iesIndex), 0);
		}
		//////BSDF Variable
		
		InitLightingData(LightData, WorldNormal, ViewDir, LightDir, floatDir);
		float3 Energy = Spot_Energy(ldh, lightDirLen, LightColor, cos(Light.smallAngle), LightAngle, 1.0 / LightRange) * isNear;
		
		if(dot(Energy, 1) < 1e-5) continue;
		//////Shadow
		const float ShadowResolution = 512.0;
		
		if (Light.shadowIndex >= 0)
		{
			float4 offsetPos = float4(WorldPos.xyz + LightDir * Light.shadowBias, 1);
			float4 clipPos = mul(Light.vpMatrix, offsetPos);
			clipPos /= clipPos.w;
			ShadowTrem = 0;
			float softValue = ShadowResolution / lerp(0.5, 1, LightData.NoL);
			[loop]
			for(int i = 0; i < _ShadowSampler; ++i)
			{
				JitterSpot = MNoise(JitterSpot) * 6.283185307179586 - 3.1415927;
				float2 angle;
				sincos(JitterSpot.x, angle.x, angle.y);
				ShadowTrem += _SpotMapArray.SampleCmpLevelZero( sampler_SpotMapArray, float3( (clipPos.xy * 0.5 + 0.5) + ( (angle * JitterSpot.y) / (softValue) ), Light.shadowIndex), clipPos.z);

			}
			ShadowTrem /= _ShadowSampler;
		}else
			ShadowTrem = 1;

		//////Shading
		
		ShadingColor += max(0, LitFunc(LightData, Energy, buffer))* ShadowTrem;


	}
}
if(_LightEnabled.z > 0.5){
	float3 JitterPoint = ViewDir;
	LightIndex = uint2(sb + 1, _PointLightIndexBuffer[sb]);
	[loop]
	for (c = LightIndex.x; c < LightIndex.y; c++)
	{		
		PointLight Light = _AllPointLight[_PointLightIndexBuffer[c]];
		float LightRange = Light.sphere.a;
		float3 LightPos = Light.sphere.rgb;
		float3 LightColor = Light.lightColor;

		float3 Un_LightDir = LightPos - WorldPos.xyz;
		float Length_LightDir = length(Un_LightDir);
		float3 LightDir = Un_LightDir / Length_LightDir;

		float3 floatDir = normalize(ViewDir + LightDir);
		InitLightingData(LightData, WorldNormal, ViewDir, LightDir, floatDir);
		float3 Energy = Point_Energy(Un_LightDir, LightColor, 1 / LightRange);
		if(dot(Energy, 1) < 1e-5) continue;
		//////Shadow
		
		const float ShadowResolution = 128;
		
		if (Light.shadowIndex >= 0) {
			
			float DepthMap = (Length_LightDir - Light.shadowBias) / LightRange;
			ShadowTrem = 0;
			float softValue = ShadowResolution / lerp(0.5, 1, LightData.NoL);
			[loop]
			for(int i = 0; i < _ShadowSampler; ++i)
			{
				JitterPoint = MNoise(JitterPoint) * 2 - 1;
				float ShadowMap = _CubeShadowMapArray.Sample( sampler_CubeShadowMapArray, float4( ( LightDir + ( JitterPoint /  softValue) ), Light.shadowIndex ) );
				ShadowTrem += DepthMap < ShadowMap;
			}
			ShadowTrem /= _ShadowSampler;
		}else
		 	ShadowTrem = 1;
		
		//////BSDF Variable


		//////Shading
		
		ShadingColor += max(0, LitFunc(LightData, Energy, buffer)) * ShadowTrem;
	}
}

	return ShadingColor;
}
#endif