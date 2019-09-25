#ifndef _AREA_LIGHT_
#define _AREA_LIGHT_

#include "ShadingModel.hlsl"

#define SHARP_EDGE_FIX 1
#define WITHOUT_CORRECT_HORIZON 0
#define WITH_GG_Sphere 1

float GetSphereLight(float radiusTan, float NoL, float NoV, float VoL)
{
    // radiusCos can be precalculated if radiusTan is a directional light
    float radiusCos = rsqrt(1 + pow2(radiusTan));
    
    // Early out if R falls within the disc
    float RoL = 2 * NoL * NoV - VoL;
    if (RoL >= radiusCos)
        return 1;

    float rOverLengthT = radiusCos * radiusTan * rsqrt(1 - RoL * RoL);
    float NoTr = rOverLengthT * (NoV - RoL * NoL);
    float VoTr = rOverLengthT * (2 * NoV * NoV - 1 - RoL * VoL);

#if WITH_GG_Sphere
    // Calculate dot(cross(N, L), V). This could already be calculated and available.
    float triple = sqrt(saturate(1 - NoL * NoL - NoV * NoV - VoL * VoL + 2 * NoL * NoV * VoL));
    // Do one Newton iteration to improve the bent light vector
    float NoBr = rOverLengthT * triple, VoBr = rOverLengthT * (2 * triple * NoV);
    float NoLVTr = NoL * radiusCos + NoV + NoTr, VoLVTr = VoL * radiusCos + 1 + VoTr;
    float p = NoBr * VoLVTr, q = NoLVTr * VoLVTr, s = VoBr * NoLVTr;    
    float xNum = q * (-0.5 * p + 0.25 * VoBr * NoLVTr);
    float xDenom = p * p + s * ((s - 2 * p)) + NoLVTr * ((NoL * radiusCos + NoV) * VoLVTr * VoLVTr + q * (-0.5 * (VoLVTr + VoL * radiusCos) - 0.5));
    float twoX1 = 2 * xNum / (xDenom * xDenom + xNum * xNum);
    float sinTheta = twoX1 * xDenom;
    float cosTheta = 1 - twoX1 * xNum;
    NoTr = cosTheta * NoTr + sinTheta * NoBr; // use new T to update NoTr
    VoTr = cosTheta * VoTr + sinTheta * VoBr; // use new T to update VoTr
#endif

    // Calculate (N.H)^2 based on the bent light vector
    float newNoL = NoL * radiusCos + NoTr;
    float newVoL = VoL * radiusCos + VoTr;
    float NoH = NoV + newNoL;
    float HoH = 2 * newVoL + 2;
    return max(0, NoH * NoH / HoH);
}

/*
//Init_Sphere( LightData, saturate( lightRadius * rsqrt( dot(_LightPos.rgb - worldPos, _LightPos.rgb - worldPos) ) * (1 - Pow2(Roughness) ) ) );
void Init_Sphere(inout BSDFContext Context, float SinAlpha)
{
    if (SinAlpha > 0)
    {
        float CosAlpha = sqrt(1 - Pow2(SinAlpha));

        float RoL = 2 * Context.NoL * Context.NoV - Context.VoL;
        if (RoL >= CosAlpha)
        {
            Context.NoH = 1;
            Context.VoH = abs(Context.NoV);
        }
        else
        {
            float rInvLengthT = SinAlpha * rsqrt(1 - RoL * RoL);
            float NoTr = rInvLengthT * (Context.NoV - RoL * Context.NoL);
            float VoTr = rInvLengthT * (2 * Context.NoV * Context.NoV - 1 - RoL * Context.VoL);

#if WITH_GG_Sphere
                // dot( cross(N,L), V )
                float NxLoV = sqrt(saturate(1 - Pow2(Context.NoL) - Pow2(Context.NoV) - Pow2(Context.VoL) + 2 * Context.NoL * Context.NoV * Context.VoL));

                float NoBr = rInvLengthT * NxLoV;
                float VoBr = rInvLengthT * NxLoV * 2 * Context.NoV;
                float NoLVTr = Context.NoL * CosAlpha + Context.NoV + NoTr;
                float VoLVTr = Context.VoL * CosAlpha + 1 + VoTr;

                float p = NoBr * VoLVTr;
                float q = NoLVTr * VoLVTr;
                float s = VoBr * NoLVTr;

                float xNum = q * (-0.5 * p + 0.25 * VoBr * NoLVTr);
                float xDenom = p * p + s * (s - 2 * p) + NoLVTr * ((Context.NoL * CosAlpha + Context.NoV) * Pow2(VoLVTr) + q * (-0.5 * (VoLVTr + Context.VoL * CosAlpha) - 0.5));
                float TwoX1 = 2 * xNum / (Pow2(xDenom) + Pow2(xNum));
                float SinTheta = TwoX1 * xDenom;
                float CosTheta = 1.0 - TwoX1 * xNum;
                NoTr = CosTheta * NoTr + SinTheta * NoBr;
                VoTr = CosTheta * VoTr + SinTheta * VoBr;
#endif

            Context.NoL = Context.NoL * CosAlpha + NoTr;
            Context.VoL = Context.VoL * CosAlpha + VoTr;
            float InvLenH = rsqrt(2 + 2 * Context.VoL);
            Context.NoH = saturate((Context.NoL + Context.NoV) * InvLenH);
            Context.VoH = saturate(InvLenH + InvLenH * Context.VoL);
        }
    }
}
*/

void AreaLightIntegrated(float3 pos, float3 tubeStart, float3 tubeEnd, float3 normal, float tubeRad, float3 ReflectionDir, out float3 outLightDir, out float outNdotL, out float outLightDist)
{
    float3 N = normal;
    float3 L0 = tubeStart - pos;
    float3 L1 = tubeEnd - pos;
    float L0dotL0 = dot(L0, L0);
    float distL0 = sqrt(L0dotL0);
    float distL1 = length(L1);

    float NdotL0 = dot(L0, N) / (2 * distL0);
    float NdotL1 = dot(L1, N) / (2 * distL1);
    outNdotL = saturate(NdotL0 + NdotL1);

    float3 Ldir = L1 - L0;
    float RepdotL0 = dot(ReflectionDir, L0);
    float RepdotLdir = dot(ReflectionDir, Ldir);
    float L0dotLdir = dot(L0, Ldir);
    float LdirdotLdir = dot(Ldir, Ldir);
    float distLdir = sqrt(LdirdotLdir);

#if SHARP_EDGE_FIX
    float t = (L0dotLdir * RepdotL0 - L0dotL0 * RepdotLdir) / (L0dotLdir * RepdotLdir - LdirdotLdir * RepdotL0);
    t = saturate(t);

    float3 L0xLdir = cross(L0, Ldir);
    float3 LdirxR = cross(Ldir, ReflectionDir);
    float RepAtLdir = dot(L0xLdir, LdirxR);

    t = lerp(1 - t, t, step(0, RepAtLdir));

#else
    float t = (RepdotL0 * RepdotLdir - L0dotLdir) / (distLdir * distLdir - RepdotLdir * RepdotLdir);
    t = saturate(t);

#endif

    float3 closestPoint = L0 + Ldir * t;
    float3 centerToRay = dot(closestPoint, ReflectionDir) * ReflectionDir - closestPoint;

    closestPoint = closestPoint + centerToRay * saturate(tubeRad / length(centerToRay));

    outLightDist = length(closestPoint);
    outLightDir = closestPoint / outLightDist;
}

float SmoothFalloff(float squaredDistance, float invSqrAttRadius)
{
    return Square( saturate(1 - Square(squaredDistance * Square(invSqrAttRadius))) );
}

float DistanceFalloff(float3 unLightDir, float invSqrAttRadius)
{
    float sqrDist = dot(unLightDir, unLightDir);
    float attenuation = 1 / (max(sqrDist, 0.01 * 0.01));
    attenuation *= SmoothFalloff(sqrDist, invSqrAttRadius);
    return attenuation;
}

float DistanceFalloff(float sqrDist, float invSqrAttRadius)
{
    float attenuation = 1 / (max(sqrDist, 0.01 * 0.01));
    attenuation *= SmoothFalloff(sqrDist, invSqrAttRadius);
    return attenuation;
}


float AngleFalloff(float3 lightDir, float3 coneDir, float lightAngleScale, float lightAngleOffset)
{
    // On the CPU
    // float lightAngleScale = 1 / max ( 0.001, (cosInner - cosOuter) );
    // float lightAngleOffset = -cosOuter * lightAngleScale ;

    float cd = dot(coneDir, lightDir);
    float attenuation = saturate(cd * lightAngleScale + lightAngleOffset);
    attenuation *= attenuation;
    return attenuation;
}

float AngleFalloff(float cd, float lightAngleScale, float lightAngleOffset)
{
    // On the CPU
    // float lightAngleScale = 1 / max ( 0.001, (cosInner - cosOuter) );
    // float lightAngleOffset = -cosOuter * lightAngleScale ;
    float attenuation = saturate(cd * lightAngleScale + lightAngleOffset);
    attenuation *= attenuation;
    return attenuation;
}

// Apply IES light profile texture
float ComputeLightProfileMultiplier(float3 WorldPosition, float3 LightPosition, float3 LightDirection, float lightAngle)
{
	float3 ToLight = normalize(WorldPosition-LightPosition);
	// -1..1
	float DotProd = dot(ToLight, LightDirection);
	// -PI..PI (this distortion could be put into the texture but not without quality loss or more memory)
	float Angle = acos(DotProd) / lightAngle;
    return Angle;
}

/////////////////////////////////////////////////////////////////////////***Energy***/////////////////////////////////////////////////////////////////////////

//////Punctual Energy

float3 Point_Energy(float3 Un_LightDir,float3 lightColor, float range)
{
    float Falloff = DistanceFalloff(Un_LightDir, range);

    // i.e with point light and luminous power unit : lightColor = color * phi / (4 * PI)
    float3 luminance = Falloff *  lightColor;
    return luminance;
}

float3 Spot_Energy(float ldh, float lightDist, float3 lightColor, float innerCone, float outerCone, float range)
{
    float Falloff = DistanceFalloff(lightDist * lightDist, range);

    float lightAngleScale = 1 / max ( 0.001, (innerCone - outerCone) );
    float lightAngleOffset = -outerCone * lightAngleScale;
    Falloff *= AngleFalloff(ldh, lightAngleScale, lightAngleOffset);

    // i.e with point light and luminous power unit : lightColor = color * phi / (4 * PI)
    float3 luminance = Falloff *  lightColor;
    return luminance;
}


//////Area Energy
float Sphere_Energy(float3 worldNormal, float3 Un_LightDir, float3 lightPos, float3 lightColor, float radius, float range)
{
    float3 L = normalize(Un_LightDir);
    float sqrDist = dot (Un_LightDir , Un_LightDir);
    float illuminance = 0;

#if WITHOUT_CORRECT_HORIZON // Analytical solution above horizon

    // Patch to Sphere frontal equation ( Quilez version )
    float sqrLightRadius = radius * radius;
    // Do not allow object to penetrate the light ( max )
    // Form factor equation include a (1 / PI ) that need to be cancel
    // thus the " PI *"
    illuminance = PI * (sqrLightRadius / (max(sqrLightRadius , sqrDist)));

#else // Analytical solution with horizon

    // Tilted patch to sphere equation
    float Beta = acos(saturate(dot(worldNormal, L)));
    float H = sqrt (sqrDist);
    float h = H / radius;
    float x = sqrt (h * h - 1);
    float y = -x * (1 / tan (Beta));

    if (h * cos (Beta) > 1) {
        illuminance = cos ( Beta ) / (h * h);
    } else {
        illuminance = (1 / (PI * h * h)) * (cos(Beta) * acos (y) - x * sin(Beta) * sqrt (1 - y * y)) + (1 / PI) * atan (sin (Beta) * sqrt (1 - y * y) / x);
    }
    illuminance *= PI;

#endif
    float RangeFalloff = DistanceFalloff(Un_LightDir, range);
    return illuminance * RangeFalloff * lightColor;
}

float3 TemperatureToRGB(float Temperature) {
    float Kelvin = clamp(Temperature, 1000, 40000) / 1000;
    float KelvinSqrt = Kelvin * Kelvin;
    float3 leftCol = float3(1, saturate( (-399.809 + 414.271 * Kelvin + 111.543 * KelvinSqrt) / (2779.24 + 164.143 * Kelvin + 84.7356 * KelvinSqrt)), 1);
    float3 rightCol = saturate((float3(1.35651, 1370.38, 348.963) + float3(0.216422, 734.616, -523.53) * Kelvin + float3(0.0006337, 0.68995, 183.62) * KelvinSqrt) / (float3(-3.24223, -4625.69, 2848.82) + float3(0.918711, 1699.87, -214.52) * Kelvin + float3(0, 0, 78.8614) * KelvinSqrt));
    return lerp(rightCol, leftCol, float3((Kelvin < 6.57).xx, Kelvin > 6.57));
}

#endif