#ifndef _Montcalo_Library_
#define _Montcalo_Library_

#include "Common.hlsl"

uint ReverseBits32(uint bits)
{
	bits = (bits << 16) | (bits >> 16);
	bits = ((bits & 0x00ff00ff) << 8) | ((bits & 0xff00ff00) >> 8);
	bits = ((bits & 0x0f0f0f0f) << 4) | ((bits & 0xf0f0f0f0) >> 4);
	bits = ((bits & 0x33333333) << 2) | ((bits & 0xcccccccc) >> 2);
	bits = ((bits & 0x55555555) << 1) | ((bits & 0xaaaaaaaa) >> 1);
	return bits;
}

float RadicalInverseSpecialized(uint base, uint a) 
{
	const float invBase = (float)1 / (float)base;
	uint reversedDigits = 0;
	float invBaseN = 1;
	while (a > 0) {
		uint next = a / base;
		uint digit = a - next * base;
		reversedDigits = reversedDigits * base + digit;
		invBaseN *= invBase;
		a = next;
	}
	return reversedDigits * invBaseN;
}

float RadicalInverseSpecialized2(uint a) 
{
	return (float)ReverseBits32(a) / (float)0xffffffffu;
}

float RadicalInverseSpecialized3(uint a) 
{
	const uint BaseN[] = { 3,9,27,81,243,729,2187, 6561 ,19683 ,59049 ,177147 , 531441 ,1594323 , 4782969 ,14348907 ,43046721 ,129140163 ,387420489 ,1162261467 ,3486784401 };
	const float invBaseN[] = { 1.f / BaseN[0],1.f / BaseN[1],1.f / BaseN[2],1.f / BaseN[3],1.f / BaseN[4],1.f / BaseN[5],1.f / BaseN[6],1.f / BaseN[7],1.f / BaseN[8],1.f / BaseN[9],
	1.0 / BaseN[10],1.f / BaseN[11],1.f / BaseN[12], 1.f / BaseN[13], 1.f / BaseN[14], 1.f / BaseN[15], 1.f / BaseN[16], 1.f / BaseN[17], 1.f / BaseN[18], 1.f / BaseN[19] };
	const float4 inv1 = float4(invBaseN[0], invBaseN[1], invBaseN[2], invBaseN[3]);
	const float4 inv2 = float4(invBaseN[4], invBaseN[5], invBaseN[6], invBaseN[7]);
	const float4 inv3 = float4(invBaseN[8], invBaseN[9], invBaseN[10], invBaseN[11]);
	const float4 inv4 = float4(invBaseN[12], invBaseN[13], invBaseN[14], invBaseN[15]);
	const float4 inv5 = float4(invBaseN[16], invBaseN[17], invBaseN[18], invBaseN[19]);

	const uint4 A = a.xxxx;
	const uint4 digit1 = uint4 (A * (inv1 * 3)) % 3;
	const uint4 digit2 = uint4 (A * (inv2 * 3)) % 3;
	const uint4 digit3 = uint4 (A * (inv3 * 3)) % 3;
	const uint4 digit4 = uint4 (A * (inv4 * 3)) % 3;
	const uint4 digit5 = uint4 (A * (inv5 * 3)) % 3;

	const float4 reverse1 = inv1 * (float4) digit1;
	const float4 reverse2 = inv2 * (float4) digit2;
	const float4 reverse3 = inv3 * (float4) digit3;
	const float4 reverse4 = inv4 * (float4) digit4;
	const float4 reverse5 = inv5 * (float4) digit5;

	return (dot(1, reverse1) + dot(1, reverse2) + dot(1, reverse3) + dot(1, reverse4) + dot(1, reverse5));
}

uint2 SobolIndex(uint2 Base, int Index, int Bits = 10) {
	uint2 SobolNumbers[10] = {
		uint2(0x8680u, 0x4c80u), uint2(0xf240u, 0x9240u), uint2(0x8220u, 0x0e20u), uint2(0x4110u, 0x1610u), uint2(0xa608u, 0x7608u),
		uint2(0x8a02u, 0x280au), uint2(0xe204u, 0x9e04u), uint2(0xa400u, 0x4682u), uint2(0xe300u, 0xa74du), uint2(0xb700u, 0x9817u),
	};

	uint2 Result = Base;
	[roll] 
    for (int b = 0; b < 10 && b < Bits; ++b) {
		Result ^= (Index & (1 << b)) ? SobolNumbers[b] : 0;
	}
	return Result;
}

uint HaltonSequence(uint Index, uint base = 3)
{
	uint result = 0;
	uint f = 1;
	uint i = Index;
	
	[unroll(255)] 
	while (i > 0) {
		result += (f / base) * (i % base);
		i = floor(i / base);
	}
	return result;
}

float2 Hammersley(uint a) {
	return float2(RadicalInverseSpecialized2(a), RadicalInverseSpecialized3(a));
}

float2 Hammersley(uint Index, uint NumSamples)
{
	return float2((float)Index / (float)NumSamples, ReverseBits32(Index) / 0xffffffffu);
}

float2 Hammersley(uint Index, uint NumSamples, uint2 Random)
{
	float E1 = frac((float)Index / NumSamples + float(Random.x & 0xffff) / (1 << 16));
	float E2 = float(ReverseBits32(Index) ^ Random.y) * 2.3283064365386963e-10;
	return float2(E1, E2);
}

float2 Hammersley16( uint Index, uint NumSamples, uint2 Random )
{
	float E1 = frac( (float)Index / NumSamples + float( Random.x ) * (1.0 / 65536.0) );
	float E2 = float( ( ReverseBits32(Index) >> 16 ) ^ Random.y ) * (1.0 / 65536.0);
	return float2( E1, E2 );
}

float3x3 GetTangentBasis(float3 TangentZ) {
	float3 UpVector = abs(TangentZ.z) < 0.999 ? float3(0, 0, 1) : float3(1, 0, 0);
	float3 TangentX = normalize(cross( UpVector, TangentZ));
	float3 TangentY = cross(TangentZ, TangentX);
	return float3x3(TangentX, TangentY, TangentZ);
}

float3 TangentToWorld(float3 Vec, float3 TangentZ)
{
	return mul(Vec, GetTangentBasis(TangentZ));
}

float4 TangentToWorld(float3 Vec, float4 TangentZ)
{
	half3 T2W = TangentToWorld(Vec, TangentZ.rgb);
	return half4(T2W, TangentZ.a);
}

float2 RandToCircle(uint2 Rand) {
	float2 sf = float2(Rand) * (sqrt(2.) / 0xffff) - sqrt(0.5);	
	float2 sq = sf*sf;
	float root = sqrt(2.*max(sq.x, sq.y) - min(sq.x, sq.y));
	if (sq.x > sq.y) {
		sf.x = sf.x > 0 ? root : -root;
	}
	else {
		sf.y = sf.y > 0 ? root : -root;
	}
	return sf;
}

/////////////Sampler
float2 UniformSampleDisk( float2 E )
{
	float Theta = 2 * PI * E.x;
	float Radius = sqrt( E.y );
	return Radius * float2( cos( Theta ), sin( Theta ) );
}

float2 UniformSampleDiskConcentric( float2 E )
{
	float2 p = 2 * E - 1;
	float Radius;
	float Phi;
	if( abs( p.x ) > abs( p.y ) )
	{
		Radius = p.x;
		Phi = (PI/4) * (p.y / p.x);
	}
	else
	{
		Radius = p.y;
		Phi = (PI/2) - (PI/4) * (p.x / p.y);
	}
	return float2( Radius * cos( Phi ), Radius * sin( Phi ) );
}

float2 UniformSampleDiskConcentricApprox( float2 E )
{
	float2 sf = E * sqrt(2.0) - sqrt(0.5);	// map 0..1 to -sqrt(0.5)..sqrt(0.5)
	float2 sq = sf*sf;
	float root = sqrt(2.0*max(sq.x, sq.y) - min(sq.x, sq.y));
	if (sq.x > sq.y)
	{
		sf.x = sf.x > 0 ? root : -root;
	}
	else
	{
		sf.y = sf.y > 0 ? root : -root;
	}
	return sf;
}

float4 UniformSampleSphere(float2 E) {
	float Phi = 2 * PI * E.x;
	float CosTheta = 1 - 2 * E.y;
	float SinTheta = sqrt(1 - CosTheta * CosTheta);

	float3 L = float3( SinTheta * cos(Phi), SinTheta * sin(Phi), CosTheta );
	float PDF = 1 / (4 * PI);

	return float4(L, PDF);
}

float4 UniformSampleHemisphere(float2 E) {
	float Phi = 2 * PI * E.x;
	float CosTheta = E.y;
	float SinTheta = sqrt(1 - CosTheta * CosTheta);

	float3 L = float3( SinTheta * cos(Phi), SinTheta * sin(Phi), CosTheta );
	float PDF = 1.0 / (2 * PI);

	return float4(L, PDF);
}

float4 CosineSampleHemisphere(float2 E) {
	float Phi = 2 * PI * E.x;
	float CosTheta = sqrt(E.y);
	float SinTheta = sqrt(1 - CosTheta * CosTheta);

	float3 L = float3( SinTheta * cos(Phi), SinTheta * sin(Phi), CosTheta );
	float PDF = CosTheta / PI;

	return float4(L, PDF);
}

float4 CosineSampleHemisphere(float2 E, float3 N) {
	float3 L = UniformSampleSphere( E ).xyz;
	L = normalize( N + L );

	float PDF = L.z * (1.0 /  PI);

	return float4(L, PDF);
}

float4 UniformSampleCone(float2 E, float CosThetaMax) {
	float Phi = 2 * PI * E.x;
	float CosTheta = lerp(CosThetaMax, 1, E.y);
	float SinTheta = sqrt(1 - CosTheta * CosTheta);

	float3 L = float3( SinTheta * cos(Phi), SinTheta * sin(Phi), CosTheta );
	float PDF = 1.0 / (2 * PI * (1 - CosThetaMax));

	return float4(L, PDF);
}


float4 ImportanceSampleLambert(float2 E)
{
    float3 L = CosineSampleHemisphere(E).rgb;
	return float4(L, 1);
}

float4 ImportanceSampleBlinn(float2 E, float Roughness) {
	float m = Roughness * Roughness;
	float m2 = m * m;
		
	float Phi = 2 * PI * E.x;
	float n = 2 / m2 - 2;
	float CosTheta = pow(max(E.y, 0.001), 1 / (n + 1));
	float SinTheta = sqrt(1 - CosTheta * CosTheta);

	float3 H = float3( SinTheta * cos(Phi), SinTheta * sin(Phi), CosTheta );
		
	float D = (n + 2)/ (2 * PI) * saturate(pow(CosTheta, n));
	float PDF = D * CosTheta;
	return float4(H, PDF); 
}

float4 ImportanceSampleGGX(float2 E, float Roughness) {
	float m = Roughness * Roughness;
	float m2 = m * m;

	float Phi = 2 * PI * E.x;
	float CosTheta = sqrt( (1 - E.y) / ( 1 + (m2 - 1) * E.y) );
	float SinTheta = sqrt(1 - CosTheta * CosTheta);

	float3 H = float3( SinTheta * cos(Phi), SinTheta * sin(Phi), CosTheta );
			
	float d = (CosTheta * m2 - CosTheta) * CosTheta + 1;
	float D = m2 / (PI * d * d);
			
	float PDF = D * CosTheta;
	return float4(H, PDF);
}

float3 ImportanceSampleGGX(float3 N, float2 E, float Roughness)
{
	float m = Roughness * Roughness;

	float Phi = 2.0 * PI * E.x;
	float CosTheta = sqrt((1.0 - E.y) / (1.0 + (m * m - 1.0) * E.y));
	float SinTheta = sqrt(1.0 - CosTheta * CosTheta);
	
	// from spherical coordinates to cartesian coordinates - halfway vector
	float3 H = float3( SinTheta * cos(Phi), SinTheta * sin(Phi), CosTheta );
	
	// from tangent-space H vector to world-space sample vector
	float3 Up        = abs(N.z) < 0.999 ? float3(0.0, 0.0, 1.0) : float3(1.0, 0.0, 0.0);
	float3 Tangent   = normalize(cross(Up, N));
	float3 BiTangent = cross(N, Tangent);
	
	return normalize( Tangent * H.x + BiTangent * H.y + N * H.z );
}

float4 ImportanceSampleVisibleGGX( float2 E, float a2, float3 V )
{
	float a = sqrt(a2);

	float3 Vh = normalize( float3( a * V.xy, V.z ) );

	float3 Tangent0 = (Vh.z < 0.9999) ? normalize( cross( float3(0, 0, 1), Vh ) ) : float3(1, 0, 0);
	float3 Tangent1 = cross( Vh, Tangent0 );

	float Radius = sqrt( E.x );
	float Phi = 2 * PI * E.y;

	float2 p = Radius * float2( cos( Phi ), sin( Phi ) );
	float s = 0.5 + 0.5 * Vh.z;
	p.y = (1 - s) * sqrt( 1 - p.x * p.x ) + s * p.y;

	float3 H;
	H  = p.x * Tangent0;
	H += p.y * Tangent1;
	H += sqrt( saturate( 1 - dot( p, p ) ) ) * Vh;
	H = normalize( float3( a * H.xy, max(0.0, H.z) ) );

	float NoV = V.z;
	float NoH = H.z;
	float VoH = dot(V, H);

	float d = (NoH * a2 - NoH) * NoH + 1;
	float D = a2 / (PI*d*d);
	float G_SmithV = 2 * NoV / (NoV + sqrt(NoV * (NoV - NoV * a2) + a2));

	float PDF = G_SmithV * VoH * D / NoV;
	return float4(H, PDF);
}

float4 ImportanceSampleInverseGGX(float2 E, float Roughness) {
	float m = Roughness * Roughness;
	float m2 = m * m;
	float A = 4;

	float Phi = 2 * PI * E.x;
	float CosTheta = sqrt((1 - E.y) / ( 1 + (m2 - 1) * E.y));
	float SinTheta = sqrt(1 - CosTheta * CosTheta);

	float3 H;
	H.x = SinTheta * cos(Phi);
	H.y = SinTheta * sin(Phi);
	H.z = CosTheta;
			
	float d = (CosTheta - m2 * CosTheta) * CosTheta + m2;
	float D = rcp(Inv_PI * (1 + A * m2)) * (1 + 4 * m2 * m2 / (d * d));
			
	float PDF = D * CosTheta;

	return float4(H, PDF);
}

void SampleAnisoGGXDir(float2 u, float3 V, float3 N, float3 tX, float3 tY, float roughnessT, float roughnessB, out float3 H, out float3 L) {
    H = sqrt(u.x / (1 - u.x)) * (roughnessT * cos(Two_PI * u.y) * tX + roughnessB * sin(Two_PI * u.y) * tY) + N;
    H = normalize(H);
    L = 2 * saturate(dot(V, H)) * H - V;
}

void ImportanceSampleAnisoGGX(float2 u, float3 V, float3 N, float3 tX, float3 tY, float roughnessT, float roughnessB, float NoV, out float3 L, out float VoH, out float NoL, out float weightOverPdf)
{
    float3 H;
    SampleAnisoGGXDir(u, V, N, tX, tY, roughnessT, roughnessB, H, L);

    float NoH = saturate(dot(N, H));
    VoH = saturate(dot(V, H));
    NoL = saturate(dot(N, L));

    float ToV = dot(tX, V);
    float BoV = dot(tY, V);
    float ToL = dot(tX, L);
    float BoL = dot(tY, L);

    float aT = roughnessT;
    float aT2 = aT * aT;
    float aB = roughnessB;
    float aB2 = aB * aB;
    float lambdaV = NoL * sqrt(aT2 * ToV * ToV + aB2 * BoV * BoV + NoV * NoV);
    float lambdaL = NoV * sqrt(aT2 * ToL * ToL + aB2 * BoL * BoL + NoL * NoL);
    float Vis = 0.5 / (lambdaV + lambdaL);
	
    weightOverPdf = 4 * Vis * NoL * VoH / NoH;
}

float MISWeight(uint Num, float PDF, uint OtherNum, float OtherPDF) {
	float Weight = Num * PDF;
	float OtherWeight = OtherNum * OtherPDF;
	return Weight * Weight / (Weight * Weight + OtherWeight * OtherWeight);
}

#endif