#ifndef _VOLUMESCATTER_
#define _VOLUMESCATTER_

float IsotropicPhase()
{
	return 1 / (4 * PI);
}

float HenyeyGreensteinPhase(float g, float CosTheta)
{
	g = -g;
	return (1 - g * g) / (4 * PI * pow(1 + g * g - 2 * g * CosTheta, 1.5f));
}

float SchlickPhase(float k, float CosTheta)
{
	float Inner = (1 + k * CosTheta);
	return (1 - k * k) / (4 * PI * Inner * Inner);
}

float RaleighPhase(float CosTheta)
{
	return 3.0f * (1.0f + CosTheta * CosTheta) / (16.0f * PI);
}

// Positive g = forward scattering
// Zero g = isotropic
// Negative g = backward scattering
float PhaseFunction(float g, float CosTheta)
{
	return HenyeyGreensteinPhase(g, CosTheta);
}

#endif