#ifndef __SHADING_MODEL__
#define __SHADING_MODEL__

#include "BSDF_Library.hlsl"

float3 Defult_Lit(BSDFContext LightData, float3 Energy,  float3 AlbedoColor, float3 SpecularColor, float Roughness)
{
    float3 Diffuse = Diffuse_RenormalizeBurley(LightData.LoH, LightData.NoL, LightData.NoV, AlbedoColor, Roughness);

    float pbr_GGX = D_GGX(LightData.NoH, Roughness);     
    float pbr_Vis = Vis_SmithGGXCorrelated(LightData.NoL, LightData.NoV, Roughness); 
    float3 pbr_Fresnel = F_Schlick(SpecularColor, 1, LightData.LoH);     

    float3 Specular = (pbr_Vis * pbr_GGX) * pbr_Fresnel;
    return (Diffuse + Specular) * Energy;
}

float3 Skin_Lit(BSDFContext LightData, float3 Energy,  float3 AlbedoColor, float3 SpecularColor, float Roughness, float skinRoughness)
{
    float3 Diffuse = Diffuse_RenormalizeBurley(LightData.LoH, LightData.NoL, LightData.NoV, AlbedoColor, Roughness);

	float2 pbr_GGX = D_Beckmann(LightData.NoH, float2(Roughness, skinRoughness));
	pbr_GGX.x = lerp(pbr_GGX.x, pbr_GGX.y, 0.15);
	float pbr_Vis = Vis_SmithGGXCorrelated(LightData.NoL, LightData.NoV, Roughness);
	float3 pbr_Fresnel = F_Schlick(SpecularColor, 1, LightData.LoH);

	float3 Specular = (pbr_Vis * pbr_GGX.x) * pbr_Fresnel;

	return (Diffuse + Specular) * Energy;
}

float3 ClearCoat_Lit(BSDFContext LightData, float3 Energy, float3 ClearCoat_MultiScatterEnergy, float3 AlbedoColor, float3 SpecularColor, float ClearCoat, float ClearCoat_Roughness, float Roughness)
{
	float3 Diffuse = Diffuse_RenormalizeBurley(LightData.LoH, LightData.NoL, LightData.NoV, AlbedoColor, Roughness);
	float F0 = pow5(1 - LightData.VoH);

	float ClearCoat_GGX = D_GGX(LightData.NoH, ClearCoat_Roughness);
	float ClearCoat_Vis = Vis_Kelemen(LightData.VoH);
	float ClearCoat_Fersnel = (F0 + (1 - F0) * 0.05) * ClearCoat;
	float3 ClearCoat_Specular = ClearCoat_GGX * ClearCoat_Vis * ClearCoat_Fersnel;
	ClearCoat_Specular *= ClearCoat_MultiScatterEnergy;

    float pbr_GGX = D_GGX(LightData.NoH, Roughness);     
    float pbr_Vis = Vis_SmithGGXCorrelated(LightData.NoL, LightData.NoV, Roughness);
	float3 pbr_Fresnel = saturate(50 * SpecularColor.g) * F0 + (1 - F0) * SpecularColor;
	float3 BaseSpecular = (pbr_Vis * pbr_GGX) * pbr_Fresnel;


	float LayerAttenuation = (1 - ClearCoat_Fersnel);
	return (Diffuse + BaseSpecular + ClearCoat_Specular) * Energy * LayerAttenuation;
}

float3 Cloth_Cotton(BSDFContext LightData, float3 Energy, float3 AlbedoColor, float3 SpecularColor, float Roughness)
{
	float3 Diffuse = Diffuse_Fabric(AlbedoColor, Roughness) * LightData.NoL;

	#if _Charlie_Ashikhmin
		float pbr_InvGGX = D_InverseGGX_Charlie(LightData.NoH, Roughness);
		float pbr_Vis = Vis_InverseGGX_Charlie(LightData.NoL + 1e-7, LightData.NoV + 1e-7, Roughness);
	#else
		float pbr_InvGGX = D_InverseGGX_Ashikhmin(LightData.NoH, Roughness);
		float pbr_Vis = Vis_InverseGGX_Ashikhmin(LightData.NoL, LightData.NoV);
	#endif
	float3 pbr_Fresnel = F_Schlick(SpecularColor, 1, LightData.LoH);

	float3 Specular = (pbr_Vis * pbr_InvGGX) * pbr_Fresnel;

	return (Diffuse + Specular) * Energy;
}

float3 Cloth_Silk(BSDFContext LightData, AnisoBSDFContext AnisoLightContext, float3 Energy, float3 AlbedoColor, float3 SpecularColor, float Roughness, float RoughnessT, float RoughnessB)
{
    
    float3 Diffuse = Diffuse_Fabric(AlbedoColor, Roughness) * LightData.NoL;

    float pbr_AnisoGGX = D_AnisotropyGGX(AnisoLightContext.ToH, AnisoLightContext.BoH, LightData.NoH, RoughnessT, RoughnessB);
    float pbr_Vis = Vis_AnisotropyGGX(AnisoLightContext.ToV, AnisoLightContext.BoV, LightData.NoV, AnisoLightContext.ToL, AnisoLightContext.BoL, LightData.NoL, RoughnessT, RoughnessB);
    float3 pbr_Fresnel = F_Schlick(SpecularColor, 1, LightData.LoH);

    float3 Specular = (pbr_Vis * pbr_AnisoGGX) * pbr_Fresnel;

    return (Diffuse + Specular) * Energy;
}

float3 Hair_Lit(float3 L, float3 V, float3 N, float3 SpecularColor, float Specular, float Roughness,float Backlit, float Scatter, float Area, float Shadow) {
	Scatter = Scatter / 10;
	const float VoL       = dot(V,L);
	const float SinThetaL = dot(N,L);
	const float SinThetaV = dot(N,V);
	float CosThetaD = cos(0.5 * abs(asinFast( SinThetaV ) - asinFast( SinThetaL)));

	const float3 Lp = L - SinThetaL * N;
	const float3 Vp = V - SinThetaV * N;
	const float CosPhi = dot(Lp,Vp) * rsqrt(dot(Lp,Lp) * dot(Vp,Vp) + 1e-4);
	const float CosfloatPhi = sqrt(saturate(0.5 + 0.5 * CosPhi));

    float3 S = 0;
	float n = 1.55;
	float n_prime = 1.19 / CosThetaD + 0.36 * CosThetaD;
	float Shift = 0.035;

	float Alpha[3] = {
		-Shift * 2,
		Shift,
		Shift * 4,
	};

	float B[3] = {
		Area + Square(Roughness),
		Area + Square(Roughness) / 2,
		Area + Square(Roughness) * 2,
	};

	// R
	if(1) {
		const float sa = sin(Alpha[0]);
		const float ca = cos(Alpha[0]);
		float Shift = 2*sa* (ca * CosfloatPhi * sqrt(1 - SinThetaV * SinThetaV) + sa * SinThetaV);
		float Mp = Vis_Hair(B[0] * sqrt(2.0) * CosfloatPhi, SinThetaL + SinThetaV - Shift);
		float Np = 0.25 * CosfloatPhi;
		float Fp = F_Hair(sqrt(saturate( 0.5 + 0.5 * VoL)));
		S += Specular * Mp * Np * Fp * lerp(1, Backlit, saturate(-VoL));
	}
	// TRT
	if(1) {
		float Mp = Vis_Hair(B[2], SinThetaL + SinThetaV - Alpha[2]);
		float f = F_Hair(CosThetaD * 0.5);
		float Fp = Square(1 - f) * f;
		float3 Tp = pow(SpecularColor, 0.8 / CosThetaD);
		float Np = exp(17 * CosPhi - 16.78);
		S += Mp * Np * Fp * Tp;
	}
	// TT
	if(1) {
		float Mp = Vis_Hair(B[1], SinThetaL + SinThetaV - Alpha[1]);
		float a = 1 / n_prime;
		float h = CosfloatPhi * (1 + a * (0.6 - 0.8 * CosPhi));
		float f = F_Hair(CosThetaD * sqrt(saturate( 1 - h*h)));
		float Fp = Square(1 - f);
		float3 Tp = pow(SpecularColor, 0.5 * sqrt(1 - Square(h * a)) / CosThetaD);
		float Np = exp(-3.65 * CosPhi - 3.98);
		S += Mp * Np * Fp * Tp * Backlit;
	}
    // Scatter
	if(1) {
		float3 FakeNormal = normalize(V - N * dot(V, N));
		N = FakeNormal;
		float Wrap = 1;
		float NoL = saturate((dot(N, L) + Wrap) / Square(1 + Wrap));
		float DiffuseScatter = Inv_PI * NoL * Scatter;
		float Luma = Luminance(SpecularColor);
		float3 ScatterTint = pow(SpecularColor / Luma, Shadow);
		S += sqrt(SpecularColor) * DiffuseScatter * ScatterTint;
	}
	S = -min(-S, 0);
	return S;
}
#if CLEARCOAT_LIT
struct GeometryBuffer
{
	float3 AlbedoColor;
	float3 SpecularColor;
	float Roughness;
	float3 ClearCoat_MultiScatterEnergy;
	float ClearCoat;
	float ClearCoat_Roughness;
};
#elif SKIN_LIT
struct GeometryBuffer
{
	float3 AlbedoColor;
	float3 SpecularColor;
	float Roughness;
	float Skin_Roughness;
};
#else
struct GeometryBuffer
{
	float3 AlbedoColor;
	float3 SpecularColor;
	float Roughness;
};
#endif
float3 LitFunc(BSDFContext LightData, float3 Energy, GeometryBuffer buffer)
{
#if SKIN_LIT
return Skin_Lit(LightData, Energy,  buffer.AlbedoColor, buffer.SpecularColor, buffer.Roughness, buffer.Skin_Roughness);
#elif CLOTH_LIT
return Cloth_Cotton(LightData, Energy, buffer.AlbedoColor, buffer.SpecularColor, buffer.Roughness);
#elif CLEARCOAT_LIT
return ClearCoat_Lit(LightData, Energy,  buffer.ClearCoat_MultiScatterEnergy, buffer.AlbedoColor, buffer.SpecularColor, buffer.ClearCoat, buffer.ClearCoat_Roughness, buffer.Roughness);
#else
return Defult_Lit(LightData, Energy,  buffer.AlbedoColor, buffer.SpecularColor, buffer.Roughness);
#endif
}
#endif