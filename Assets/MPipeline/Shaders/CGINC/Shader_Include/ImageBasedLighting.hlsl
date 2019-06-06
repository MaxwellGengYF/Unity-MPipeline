#ifndef _ImageBasedLighting_
#define _ImageBasedLighting_

#include "BSDF_Library.hlsl"
#include "ShadingModel.hlsl"
#include "UnityImageBasedLighting.cginc"

float PBR_G(float NoL, float NoV, float a) {
    float a2 = pow4(a);
    float GGXL = NoV * sqrt((-NoL * a2 + NoL) * NoL + a2);
    float GGXV = NoL * sqrt((-NoV * a2 + NoV) * NoV + a2);
    return (2 * NoL) / (GGXV + GGXL);
}

//////////////////////////Environment LUT 
float Standard_Burley(float Roughness, float NoV) {
    float3 V;
    V.x = sqrt(1 - NoV * NoV);
    V.y = 0;
    V.z = NoV;

    float r = 0; 
	const uint NumSamples = 64;
    for (uint i = 0; i < NumSamples; i++) {
        float2 E = Hammersley(i, NumSamples); 
        float3 H = CosineSampleHemisphere(E).rgb;
        float3 L = 2 * dot(V, H) * H - V;

        float NoL = saturate(L.b);
        float LoH = saturate(dot(L, H));
        float Diffuse = Diffuse_Burley_NoPi(LoH, NoL, NoV, Roughness);
        //float Diffuse = Diffuse_RenormalizeBurley_NoPi(LoH, NoL, NoV, Roughness);
        //float Diffuse = Diffuse_OrenNayar_NoPi(LoH, NoL, NoV, Roughness);
        r += Diffuse;
    }
    return r / NumSamples;
}


float2 Standard_Karis(float Roughness, float NoV) {
    float3 V;
    V.x = sqrt(1 - NoV * NoV);
    V.y = 0;
    V.z = NoV;

    float2 r = 0;
	const uint NumSamples = 64;
    for (uint i = 0; i < NumSamples; i++) {
        float2 E = Hammersley(i, NumSamples); 
        float3 H = ImportanceSampleGGX(E, Roughness).rgb;
        float3 L = 2 * dot(V, H) * H - V;

        float VoH = saturate(dot(V, H));
        float NoL = saturate(L.z);
        float NoH = saturate(H.z);

        if (NoL > 0) {
            float G = PBR_G(NoL, NoV, Roughness);
            float Gv = G * VoH / NoH;
            float Fc = pow(1 - VoH, 5);
            //r.x += Gv * (1 - Fc);
            r.x += Gv;
            r.y += Gv * Fc;
        }
    }
    return r / NumSamples;
}

float2 Standard_Karis_Approx(float Roughness, float NoV) {
    const float4 c0 = float4(-1.0, -0.0275, -0.572,  0.022);
    const float4 c1 = float4( 1.0,  0.0425,  1.040, -0.040);
    float4 r = Roughness * c0 + c1;
    float a004 = min(r.x * r.x, exp2(-9.28 * NoV)) * r.x + r.y;
    return float2(-1.04, 1.04) * a004 + r.zw;
}

float Standard_Karis_Approx_Nonmetal(float Roughness, float NoV) {
	const float2 c0 = { -1, -0.0275 };
	const float2 c1 = { 1, 0.0425 };
	float2 r = Roughness * c0 + c1;
	return min( r.x * r.x, exp2( -9.28 * NoV ) ) * r.x + r.y;
}


float2 Cloth_Ashikhmin_Approx(float Roughness, float NoV) {
    const float4 c0 = float4(0.24,  0.93, 0.01, 0.20);
    const float4 c1 = float4(2, -1.30, 0.40, 0.03);

    float s = 1 - NoV;
    float e = s - c0.y;
    float g = c0.x * exp2(-(e * e) / (2 * c0.z)) + s * c0.w;
    float n = Roughness * c1.x + c1.y;
    float r = max(1 - n * n, c1.z) * g;

    return float2(r, r * c1.w);
}

float2 Cloth_Charlie_Approx(float Roughness, float NoV) {
    const float3 c0 = float3(0.95, 1250, 0.0095);
    const float4 c1 = float4(0.04, 0.2, 0.3, 0.2);

    float a = 1 - NoV;
    float b = 1 - (Roughness);

    float n = pow(c1.x + a, 64);
    float e = b - c0.x;
    float g = exp2(-(e * e) * c0.y);
    float f = b + c1.y;
    float a2 = a * a;
    float a3 = a2 * a;
    float c = n * g + c1.z * (a + c1.w) * Roughness + f * f * a3 * a3 * a2;
    float r = min(c, 18);

    return float2(r, r * c0.z);
}


float3 ImageBasedLighting_Hair(float3 V, float3 N, float3 specularColor, float Roughness, float Scatter) {
	float3 Lighting = 0;
	uint NumSamples = 32;
	
	UNITY_LOOP
	for( uint i = 0; i < NumSamples; i++ ) {
		float2 E = Hammersley(i, NumSamples);
		float3 L = UniformSampleSphere(E).rgb;
		{
			float PDF = 1 / (4 * PI);
			float InvWeight = PDF * NumSamples;
			float Weight = rcp(InvWeight);

			float3 Shading = 0;
            Shading = Hair_Lit(L, V, N, specularColor, 0.5, Roughness, 0, Scatter, 0, 0);

            Lighting += Shading * Weight;
		}
	}
	return Lighting;
}

float3 PreintegratedDGF_LUT(sampler2D PreintegratedLUT, out float3 EnergyCompensation, float3 SpecularColor, float Roughness, float NoV)
{
    float2 AB = tex2D(PreintegratedLUT, float2(Roughness, NoV)).rg;
    float3 ReflectionGF = lerp(saturate(50 * SpecularColor.g) * AB.ggg, AB.rrr, SpecularColor);
    EnergyCompensation = 1 + SpecularColor * (1 / AB.r - 1);
    return ReflectionGF;
}

float3 PreintegratedGF_ClothAshikhmin(float3 SpecularColor, float Roughness, float NoV)
{
    float2 AB = Cloth_Ashikhmin_Approx(Roughness, NoV);
    //float3 EnergyCompensation = 1 + SpecularColor * (1 / AB.r - 1);
    return SpecularColor * AB.r + AB.g;
}

float3 PreintegratedGF_ClothCharlie(float3 SpecularColor, float Roughness, float NoV)
{
    float2 AB = Cloth_Charlie_Approx(Roughness, NoV);
    //float3 EnergyCompensation = 1 + SpecularColor * (1 / AB.r - 1);
    return SpecularColor * AB.r + AB.g;
}

float IBL_PBR_Diffuse(float LoH, float NoL, float NoV, float Roughness)
{
	float F90 = lerp( 0, 0.5, Roughness ) + ( 2 * pow2(LoH) * Roughness );
	return F_Schlick(1, F90, NoL) * F_Schlick(1, F90, NoV) * lerp(1, 1 / 0.662, Roughness);
}
float IBL_Defualt_DiffuseIntegrated(float Roughness, float NoV) {
    float3 V;
    V.x = sqrt(1 - NoV * NoV);
    V.y = 0;
    V.z = NoV;

    float r = 0; 
	const uint NumSamples = 512;

    for (uint i = 0; i < NumSamples; i++) {
        float2 E = Hammersley( i, NumSamples, HaltonSequence(i) ); 
        float4 H = CosineSampleHemisphere(E);
        float3 L = 2 * dot(V, H.xyz) * H.xyz - V;

        float NoL = saturate(L.b);
        float LoH = saturate( dot(L, H.xyz) );
        
        if (NoL > 0) {
            float Diffuse = IBL_PBR_Diffuse(LoH, NoL, NoV, Roughness);
            r += Diffuse;
        }
    }
    return r / NumSamples;
}

float IBL_PBR_Specular_G(float NoL, float NoV, float a) {
    float a2 = pow4(a);
    float GGXL = NoV * sqrt((-NoL * a2 + NoL) * NoL + a2);
    float GGXV = NoL * sqrt((-NoV * a2 + NoV) * NoV + a2);
    return (2 * NoL) / (GGXV + GGXL);
}

float2 IBL_Defualt_SpecularIntegrated(float Roughness, float NoV) {
    float3 V;
    V.x = sqrt(1 - NoV * NoV);
    V.y = 0;
    V.z = NoV;

    float2 r = 0;
	const uint NumSamples = 512;

    for (uint i = 0; i < NumSamples; i++) {
        float2 E = Hammersley(i, NumSamples, HaltonSequence(i)); 
        float4 H = ImportanceSampleGGX(E, Roughness);
        float3 L = 2 * dot(V, H) * H.xyz - V;

        float VoH = saturate(dot(V, H.xyz));
        float NoL = saturate(L.z);
        float NoH = saturate(H.z);

        if (NoL > 0) {
            float G = IBL_PBR_Specular_G(NoL, NoV, Roughness);
            float Gv = G * VoH / NoH;
            float Fc = pow(1 - VoH, 5);
            //r.x += Gv * (1 - Fc);
            r.x += Gv;
            r.y += Gv * Fc;
        }
    }
    return r / NumSamples;
}

#endif
