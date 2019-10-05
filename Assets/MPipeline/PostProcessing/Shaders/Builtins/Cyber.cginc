#ifndef __CYBER_INCLUDE__
#define __CYBER_INCLUDE__

#define ISIX 0.1666667
#define  red 0
#define orange 0.0833333
#define yellow 0.1666667
#define green 0.3333333
#define cyan 0.5
#define blue 0.66666667
#define purple 0.75
#define magenta 0.8333333
struct CyberStruct
{
float _Red;
float _Orange;
float _Yellow;
float _Green;
float _Cyan;
float _Blue;
float _Purple;
float _Magenta;
float _Pow_S;
float _Value;
};
StructuredBuffer<CyberStruct> _CyberVar;
inline float3 RGBtoHSV(float3 arg1)
{
	float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
	float4 P = lerp(float4(arg1.bg, K.wz), float4(arg1.gb, K.xy), step(arg1.b, arg1.g));
	float4 Q = lerp(float4(P.xyw, arg1.r), float4(arg1.r, P.yzx), step(P.x, arg1.r));
	float D = Q.x - min(Q.w, Q.y);
	float E = 1e-10;
	return float3(abs(Q.z + (Q.w - Q.y) / (6.0 * D + E)), D / (Q.x + E), Q.x);
}

inline float3 HSVtoRGB(float3 arg1)
{
	float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
	float3 P = abs(frac(arg1.xxx + K.xyz) * 6.0 - K.www);
	return arg1.z * lerp(K.xxx, saturate(P - K.xxx), arg1.y);
}
inline float cycle(float value) {
    [flatten]
	if (value > 1)
		return value - 1;
    [flatten]
	if (value < 0)
		return 1 + value;
	return value;
}
inline float Change(float s0, float e0, float s1, float e1, float value) {
	return (value - s0) / (e0 - s0) * (e1 - s1) + s1;
}
float3 GetCyberColor(float3 col)
{
    float3 hsl = RGBtoHSV(col);
    float h = hsl.x;
    CyberStruct o = _CyberVar[0];
    float newRed = red + o._Red * ISIX;
	float newOrange = orange + o._Orange * ISIX;
	float newYellow = yellow + o._Yellow * ISIX;
	float newGreen = green + o._Green * ISIX;
	float newCyan = cyan + o._Cyan * ISIX;
	float newBlue = blue + o._Blue * ISIX;
	float newPurple = purple + o._Purple * ISIX;
	float newMagenta = magenta + o._Magenta * ISIX;
    [flatten]
    if (h > magenta) { h = Change(magenta, 1, newMagenta, 1 + newRed, h); }
	else if (h > purple) { h = Change(purple, magenta, newPurple, newMagenta, h); }
	else if (h > blue) { h = Change(blue, purple, newBlue, newPurple, h); }
	else if (h > cyan) { h = Change(cyan, blue, newCyan, newBlue, h); }
	else if (h > green) { h = Change(green, cyan, newGreen, newCyan, h); }
	else if (h > yellow) { h = Change(yellow, green, newYellow, newGreen, h); }
	else if (h > orange) { h = Change(orange, yellow, newOrange, newYellow, h); }
	else { h = Change(red, orange, newRed, newOrange, h); }
    hsl.x = cycle(h);
	hsl.y = 1 - pow(1 - hsl.y, o._Pow_S);
	hsl.z = pow(hsl.z, o._Value);
	return HSVtoRGB(hsl);
}
#endif