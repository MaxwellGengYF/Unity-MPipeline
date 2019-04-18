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
static const float _ShadowSampler = 8.0;
float3 CalculateLocalLight(float2 uv, float4 WorldPos, float linearDepth, float3 AlbedoColor, float3 WorldNormal, float4 SpecularColor, float Roughness, float3 ViewDir)
{
	float ShadowTrem = 0;
	float3 ShadingColor = 0;
	float rate = pow(max(0, (linearDepth - _CameraClipDistance.x) / _CameraClipDistance.y), 1.0 / CLUSTERRATE);
    if(rate > 1) return 0;
	uint3 voxelValue = uint3((uint2)(uv * float2(XRES, YRES)), (uint)(rate * ZRES));
	uint sb = GetIndex(voxelValue, VOXELSIZE, (MAXLIGHTPERCLUSTER + 1));
	uint2 LightIndex;// = uint2(sb + 1, _PointLightIndexBuffer[sb]);
	uint c;



#if SPOTLIGHT
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

		float LightAngle = cos(Light.angle);
		float3 LightForward = SpotCone.direction;
		float3 Un_LightDir = LightPos - WorldPos.xyz;
		float lightDirLen = length(Un_LightDir);
		float3 LightDir = Un_LightDir / lightDirLen;
		float3 floatDir = normalize(ViewDir + LightDir);
		float ldh = -dot(LightDir, SpotCone.direction);
		float isNear =  dot(-Un_LightDir, SpotCone.direction) > Light.nearClip;
		//////BSDF Variable
		BSDFContext LightData;
		Init(LightData, WorldNormal, ViewDir, LightDir, floatDir);
		float3 Energy = Spot_Energy(ldh, lightDirLen, LightColor, cos(Light.smallAngle), LightAngle, 1.0 / LightRange, LightData.NoL) * isNear;
		
		if(dot(Energy, 1) < 1e-5) continue;
		//////Shadow
		const float ShadowResolution = 512.0;
		
		if (Light.shadowIndex >= 0)
		{
			float4 offsetPos = float4(WorldPos.xyz + LightDir * 0.25, 1);
			float4 clipPos = mul(Light.vpMatrix, offsetPos);
			clipPos /= clipPos.w;
			ShadowTrem = 0;
			[loop]
			for(int i = 0; i < _ShadowSampler; ++i)
			{
				JitterSpot = MNoise(JitterSpot) * 6.283185307179586 - 3.1415927;
				float2 angle;
				sincos(JitterSpot.x, angle.x, angle.y);
				ShadowTrem += _SpotMapArray.SampleCmpLevelZero( sampler_SpotMapArray, float3( (clipPos.xy * 0.5 + 0.5) + ( (angle * JitterSpot.y) / (ShadowResolution) ), Light.shadowIndex), clipPos.z);

			}
			ShadowTrem /= _ShadowSampler;
		}else
			ShadowTrem = 1;

		//////Shading
		
		ShadingColor += max(0, Defult_Lit(LightData, Energy, 1, AlbedoColor, SpecularColor, Roughness))* ShadowTrem;


	}
#endif
#if POINTLIGHT
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
		BSDFContext LightData;
		Init(LightData, WorldNormal, ViewDir, LightDir, floatDir);
		float3 Energy = Point_Energy(Un_LightDir, LightColor, 1 / LightRange, LightData.NoL);
		if(dot(Energy, 1) < 1e-5) continue;
		//////Shadow
		
		const float ShadowResolution = 192;
		
		if (Light.shadowIndex >= 0) {
			
			float DepthMap = (Length_LightDir - 0.25) / LightRange;
			ShadowTrem = 0;
			for(int i = 0; i < _ShadowSampler; ++i)
			{
				JitterPoint = MNoise(JitterPoint) * 2 - 1;
				float ShadowMap = _CubeShadowMapArray.Sample( sampler_CubeShadowMapArray, float4( ( LightDir + ( JitterPoint /  ShadowResolution) ), Light.shadowIndex ) );
				ShadowTrem += DepthMap < ShadowMap;
			}
			ShadowTrem /= _ShadowSampler;
		}else
		 	ShadowTrem = 1;
		
		//////BSDF Variable


		//////Shading
		
		ShadingColor += max(0, Defult_Lit(LightData, Energy, 1, AlbedoColor, SpecularColor, Roughness)) * ShadowTrem;
	}
#endif

	return ShadingColor;
}
Texture3D<int> _PointLightTile;
Texture3D<int> _SpotLightTile;
float2 _TileSize;
float3 CalculateTileLight(float2 uv, float4 WorldPos, float3 AlbedoColor, float3 WorldNormal, float4 SpecularColor, float Roughness, float3 ViewDir)
{
	float ShadowTrem = 0;
	float3 ShadingColor = 0;
	int c;
	int targetCount;
	float2 index = uv * _TileSize + 0.01;

#if SPOTLIGHT
	float2 JitterSpot = uv;
	targetCount = _SpotLightTile[int3(index, 0)];
	[loop]
	for (c = 1; c < targetCount; c++)
	{
		SpotLight Light = _AllSpotLight[_SpotLightTile[int3(index, c)]];
		Cone SpotCone = Light.lightCone;

		float LightRange = SpotCone.height;
		float3 LightPos = SpotCone.vertex;
		float3 LightColor = Light.lightColor;

		float LightAngle = cos(Light.angle);
		float3 LightForward = SpotCone.direction;
		float3 Un_LightDir = LightPos - WorldPos.xyz;
		float lightDirLen = length(Un_LightDir);
		float3 LightDir = Un_LightDir / lightDirLen;
		float3 floatDir = normalize(ViewDir + LightDir);
		float ldh = -dot(LightDir, SpotCone.direction);
		float isNear =  dot(-Un_LightDir, SpotCone.direction) > Light.nearClip;
		//////BSDF Variable
		BSDFContext LightData;
		Init(LightData, WorldNormal, ViewDir, LightDir, floatDir);
		float3 Energy = Spot_Energy(ldh, lightDirLen, LightColor, cos(Light.smallAngle), LightAngle, 1.0 / LightRange, LightData.NoL) * isNear;
		
		if(dot(Energy, 1) < 1e-5) continue;
		//////Shadow
		const float ShadowResolution = 512.0;
		
		if (Light.shadowIndex >= 0)
		{
			float4 offsetPos = float4(WorldPos.xyz + LightDir * 0.25, 1);
			float4 clipPos = mul(Light.vpMatrix, offsetPos);
			clipPos /= clipPos.w;
			ShadowTrem = 0;
			[loop]
			for(int i = 0; i < _ShadowSampler; ++i)
			{
				JitterSpot = MNoise(JitterSpot) * 6.283185307179586 - 3.1415927;
				float2 angle;
				sincos(JitterSpot.x, angle.x, angle.y);
				ShadowTrem += _SpotMapArray.SampleCmpLevelZero( sampler_SpotMapArray, float3( (clipPos.xy * 0.5 + 0.5) + ( (angle * JitterSpot.y) / (ShadowResolution) ), Light.shadowIndex), clipPos.z);

			}
			ShadowTrem /= _ShadowSampler;
		}else
			ShadowTrem = 1;

		//////Shading
		
		ShadingColor += max(0, Defult_Lit(LightData, Energy, 1, AlbedoColor, SpecularColor, Roughness))* ShadowTrem;


	}
#endif
#if POINTLIGHT
	float3 JitterPoint = ViewDir;
	targetCount = _PointLightTile[int3(index, 0)];
	[loop]
	for (c = 1; c < targetCount; c++)
	{		
		PointLight Light = _AllPointLight[_PointLightTile[int3(index, c)]];
		float LightRange = Light.sphere.a;
		float3 LightPos = Light.sphere.rgb;
		float3 LightColor = Light.lightColor;

		float3 Un_LightDir = LightPos - WorldPos.xyz;
		float Length_LightDir = length(Un_LightDir);
		float3 LightDir = Un_LightDir / Length_LightDir;
		float3 floatDir = normalize(ViewDir + LightDir);
		BSDFContext LightData;
		Init(LightData, WorldNormal, ViewDir, LightDir, floatDir);
		float3 Energy = Point_Energy(Un_LightDir, LightColor, 1 / LightRange, LightData.NoL);
		if(dot(Energy, 1) < 1e-5) continue;
		//////Shadow
		
		const float ShadowResolution = 192;
		
		if (Light.shadowIndex >= 0) {
			
			float DepthMap = (Length_LightDir - 0.25) / LightRange;
			ShadowTrem = 0;
			for(int i = 0; i < _ShadowSampler; ++i)
			{
				JitterPoint = MNoise(JitterPoint) * 2 - 1;
				float ShadowMap = _CubeShadowMapArray.Sample( sampler_CubeShadowMapArray, float4( ( LightDir + ( JitterPoint /  ShadowResolution) ), Light.shadowIndex ) );
				ShadowTrem += DepthMap < ShadowMap;
			}
			ShadowTrem /= _ShadowSampler;
		}else
		 	ShadowTrem = 1;
		
		//////BSDF Variable


		//////Shading
		
		ShadingColor += max(0, Defult_Lit(LightData, Energy, 1, AlbedoColor, SpecularColor, Roughness)) * ShadowTrem;
	}
#endif

	return ShadingColor;
}
#endif