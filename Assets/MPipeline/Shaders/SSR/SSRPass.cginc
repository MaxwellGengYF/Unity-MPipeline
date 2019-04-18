#include "UnityCG.cginc"
#include "SSRCommon.cginc"
#include "SSRBSDF.cginc"
#include "SSRLibrary.cginc"
#include "../CGINC/Random.cginc"
#include "../CGINC/Upsample.cginc"

sampler2D _SSR_RayCastRT, _SSR_GetSSRColor_RT, _SSR_TemporalPrev_RT, _SSR_TemporalCurr_RT, _DownSampledDepthTexture, _CameraMotionVectorsTexture, _DownSampledGBuffer1, _DownSampledGBuffer2;
Texture2D _SSR_SceneColor_RT; SamplerState sampler_SSR_SceneColor_RT;

Texture2D _SSR_HierarchicalDepth_RT; SamplerState sampler_SSR_HierarchicalDepth_RT;
sampler2D _LastFrameDepthTexture;
sampler2D _CameraDepthTexture;
sampler2D _CameraGBufferTexture2;
float4x4 _InvVP, _LastVp;
float4 _SSR_RayCastRT_TexelSize;

int _SSR_HiZ_PrevDepthLevel;
struct SSRData
{
	float brdfBias;
    float ScreenFade;
    float Thickness;
    int HiZ_RaySteps;
    int HiZ_MaxLevel;
    int HiZ_StartLevel;
    int HiZ_StopLevel;
    float MaximumBiasAllowed;
    float TemporalWeight;
    float2 ScreenSize;
    float2 rayCastSize;
    float4x4 viewProjection;
    float4x4 projection;
    float4x4 inverseProj;
    float4x4 worldToCamera;
    float4x4 cameraToWorld;
    float4x4 projToPixelMatrix;
	float4x4 inverseLastVP;
};
StructuredBuffer<SSRData> _SSRDatas;
#define DATA (_SSRDatas[0])
struct VertexInput
{
	float4 vertex : POSITION;
	float4 uv : TEXCOORD0;
};

struct PixelInput
{
	float4 vertex : SV_POSITION;
	float4 uv : TEXCOORD0;
};

PixelInput vert(VertexInput v)
{
	PixelInput o;
	o.vertex = v.vertex;
	o.uv = v.uv;
	return o;
}
float SSR_BRDF(float3 V, float3 L, float3 N, float Roughness)
{
	float3 H = normalize(L + V);

	float NoH = max(dot(N, H), 0);
	float NoL = max(dot(N, L), 0);
	float NoV = max(dot(N, V), 0);

	float D = D_GGX(NoH, Roughness);
	float G = Vis_SmithGGXCorrelated(NoL, NoV, Roughness);

	return max(0, D * G);
}
////////////////////////////////-----Get Hierarchical_ZBuffer-----------------------------------------------------------------------------
float ScreenSpaceReflection_GenerateHiZBuffer(PixelInput i) : SV_Target {
	float2 uv = i.uv.xy;
		
    float4 minDepth = float4(
        _SSR_HierarchicalDepth_RT.SampleLevel( sampler_SSR_HierarchicalDepth_RT, uv, _SSR_HiZ_PrevDepthLevel, int2(-1.0,-1.0) ).r,
        _SSR_HierarchicalDepth_RT.SampleLevel( sampler_SSR_HierarchicalDepth_RT, uv, _SSR_HiZ_PrevDepthLevel, int2(-1.0, 1.0) ).r,
        _SSR_HierarchicalDepth_RT.SampleLevel( sampler_SSR_HierarchicalDepth_RT, uv, _SSR_HiZ_PrevDepthLevel, int2(1.0, -1.0) ).r,
        _SSR_HierarchicalDepth_RT.SampleLevel( sampler_SSR_HierarchicalDepth_RT, uv, _SSR_HiZ_PrevDepthLevel, int2(1.0, 1.0) ).r
    );
	#if UNITY_REVERSED_Z
		minDepth.xy = max(minDepth.xy, minDepth.zw);
		minDepth.x = max(minDepth.x, minDepth.y);
	#else
		minDepth.xy = min(minDepth.xy, minDepth.zw);
		minDepth.x = min(minDepth.x, minDepth.y);
	#endif
	return minDepth.x;
}
////////////////////////////////-----Hierarchical_ZTrace Sampler-----------------------------------------------------------------------------
float4 ScreenSpaceReflection_HiZTrace(PixelInput i) : SV_Target
{
	/*float2 UV = i.uv.xy;

	float SceneDepth = tex2Dlod(_DownSampledDepthTexture, float4(UV, 0, 0)).r;
	float Roughness = clamp(1 - tex2D(_DownSampledGBuffer1, UV).a, 0.02, 1);
	float3 WorldNormal = normalize(tex2D(_DownSampledGBuffer2, UV) * 2 - 1);
	float3 ViewNormal = normalize(mul((float3x3)(DATA.worldToCamera), WorldNormal));

	float3 ScreenPos = float3(UV * 2 - 1, SceneDepth);
	float4 ViewPos = mul(DATA.inverseProj, float4(ScreenPos, 1));
	ViewPos /= ViewPos.w;*/
	float2 UV = i.uv.xy;
	float SceneDepth = tex2D(_DownSampledDepthTexture, UV).x;
	float EyeDepth = LinearEyeDepth(SceneDepth);
	float Roughness = clamp(1 - tex2D(_DownSampledGBuffer1, UV).a, 0.02, 1);
	float3 WorldNormal = tex2D(_DownSampledGBuffer2, UV) * 2 - 1;
	float3 ViewNormal = mul((float3x3)(DATA.worldToCamera), WorldNormal);

	float3 ScreenPos = GetScreenPos(UV, SceneDepth);
	float3 WorldPos = GetWorlPos(ScreenPos, _InvVP);
	float3 ViewPos = GetViewPos(ScreenPos, DATA.inverseProj);
	float3 ViewDir = GetViewDir(WorldPos, ViewPos);

	float2 E = MNoise(UV);
	E.y = lerp(E.y, 0.0, DATA.brdfBias);
	float4 H = TangentToWorld( ImportanceSampleGGX(E, Roughness), float4(ViewNormal, 1.0) );

	float3 ReflectionDir = reflect(normalize(ViewPos), H.xyz);

	float3 rayStart = float3(UV, ScreenPos.z);
	float4 rayProj = mul ( DATA.projection, float4(ViewPos.xyz + ReflectionDir, 1.0) );
	float3 rayDir = normalize( (rayProj.xyz / rayProj.w) - ScreenPos);
	rayDir.xy *= 0.5;

	float4 RayHitData = Hierarchical_Z_Trace(DATA.HiZ_MaxLevel, DATA.HiZ_StartLevel, DATA.HiZ_StopLevel, DATA.HiZ_RaySteps, DATA.Thickness, 1 / DATA.rayCastSize, rayStart, rayDir, _SSR_HierarchicalDepth_RT, sampler_SSR_HierarchicalDepth_RT);
	RayHitData.a = Square( RayHitData.a * GetScreenFadeBord(RayHitData.xy, DATA.ScreenFade) );

	return RayHitData;
}
static const int2 offset[9] ={int2(-1.0, -1.0), int2(0.0, -1.0), int2(1.0, -1.0), int2(-1.0, 0.0), int2(0.0, 0.0), int2(1.0, 0.0), int2(-1.0, 1.0), int2(0.0, 1.0), int2(1.0, 1.0)};

////////////////////////////////-----Solver Reflection Color-----------------------------------------------------------------------------
float4 ScreenSpaceReflection_GetColor(PixelInput i) : SV_Target
{
	float2 UV = i.uv.xy;
	float4 specular = tex2D(_DownSampledGBuffer1, UV);
	float Roughness = clamp(1 - specular.a, 0.02, 1);
	float SceneDepth = GetDepth(_DownSampledDepthTexture, UV);
	float4 WorldNormal = tex2D(_DownSampledGBuffer2, UV) * 2 - 1;
	float3 ViewNormal = GetViewNormal(WorldNormal, DATA.worldToCamera);
	float3 ScreenPos = GetScreenPos(UV, SceneDepth);
	float3 ViewPos = GetViewPos(ScreenPos, DATA.inverseProj);

	float2 BlueNoise = MNoise(UV);
	float2x2 OffsetRotationMatrix = float2x2(BlueNoise.x, BlueNoise.y, -BlueNoise.y, -BlueNoise.x);

	float NumWeight, Weight;
	float2 Offset_UV, Neighbor_UV;
	float4 SampleColor = 0;
	float4 ReflecttionColor = 0;
	float alpha = 0;
	for (int i = 0; i < 9; i++)
	{
		Offset_UV = mul(OffsetRotationMatrix, offset[i] * (1 / DATA.ScreenSize.xy));
		Neighbor_UV = UV + Offset_UV;

		float4 HitUV_PDF = tex2D(_SSR_RayCastRT, Neighbor_UV);
		float3 Hit_ViewPos = GetViewPos(GetScreenPos(HitUV_PDF.rg, HitUV_PDF.b), DATA.inverseProj);

		///SpatioSampler
		Weight = SSR_BRDF(normalize(-ViewPos), normalize(Hit_ViewPos - ViewPos), ViewNormal, Roughness);
		SampleColor.rgb = _SSR_SceneColor_RT.SampleLevel(sampler_SSR_SceneColor_RT, HitUV_PDF.xy,Roughness * 3).rgb  * HitUV_PDF.a;
		alpha += HitUV_PDF.a;
		SampleColor.rgb /= 1 + Luminance(SampleColor.rgb);
		ReflecttionColor += SampleColor * Weight;
		NumWeight += Weight;
	}

	ReflecttionColor /= NumWeight;
	ReflecttionColor.rgb *= specular.rgb;
	ReflecttionColor = max(1e-5, ReflecttionColor);
	ReflecttionColor.a = alpha / 9.0;

	return ReflecttionColor;
}
float2 _Jitter;
float2 _LastJitter;
////////////////////////////////-----Temporal Sampler-----------------------------------------------------------------------------
float4 ScreenSpaceReflection_Temporalfilter(PixelInput i) : SV_Target
{
	float2 UV = i.uv.xy;
	float HitDepth = tex2D(_SSR_RayCastRT, UV).z;
	float3 WorldNormal = tex2D(_DownSampledGBuffer2, UV).rgb * 2 - 1;

	/////Get Reprojection Velocity
	float2 Depth_Velocity = tex2D(_CameraMotionVectorsTexture, UV);
	float2 Ray_Velocity = GetRayMotionVector(HitDepth, UV, _InvVP, _LastVp, DATA.viewProjection);
	float Velocity_Weight = saturate(dot(WorldNormal, float3(0, 1, 0)));
	float2 Velocity = lerp(Depth_Velocity, Ray_Velocity, Velocity_Weight);

	float4 SSR_CurrColor = Upsampling(UV, _SSR_RayCastRT_TexelSize.zw, _CameraDepthTexture, _SSR_GetSSRColor_RT, _CameraGBufferTexture2);
	//ResolverAABB(_SSR_GetSSRColor_RT, 0, 10, DATA.TemporalScale, UV, DATA.ScreenSize, SSR_MinColor, SSR_MaxColor, SSR_CurrColor);
	float2 PrevUV = UV - Velocity + _Jitter - _LastJitter;
	float2 PrevDepthUV = UV - Depth_Velocity;
	float2 prevProjCoord = PrevDepthUV * 2  -1;
	PrevDepthUV = PrevDepthUV + _Jitter - _LastJitter;
	if(abs(dot(PrevUV - saturate(PrevUV), 1)) > 1e-5) 
		return SSR_CurrColor;

	/////Clamp TemporalColor
	float4 SSR_PrevColor = tex2D(_SSR_TemporalPrev_RT, PrevUV);
	float currentDepth = tex2D(_CameraDepthTexture, UV).x;
	float lastDepth = tex2D(_LastFrameDepthTexture, PrevDepthUV).x;
	float4 currentWorldPos = mul(_InvVP, float4(UV * 2 - 1, currentDepth, 1));
	float4 lastWorldPos = mul(DATA.inverseLastVP, float4(prevProjCoord, lastDepth, 1));
	currentWorldPos /= currentWorldPos.w;
	lastWorldPos /= lastWorldPos.w;
	/////Combine TemporalColor
	float Temporal_BlendWeight = DATA.TemporalWeight;
	float3 diff = currentWorldPos.xyz - lastWorldPos.xyz;
	float4 ReflectionColor = lerp(SSR_CurrColor, SSR_PrevColor, Temporal_BlendWeight * (dot(diff, diff) < ( DATA.MaximumBiasAllowed *  DATA.MaximumBiasAllowed)));
	return ReflectionColor;
}