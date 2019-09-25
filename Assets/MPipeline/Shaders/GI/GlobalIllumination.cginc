#ifndef LIGHTBAKER
#define LIGHTBAKER
#define RESOLUTION 128
half4 Decode(uint value)
{
    uint4 values = 0;
    values.x = value & 255;
    value >>= 8;
    values.y = value & 255;
    value >>= 8;
    values.z = value & 255;
    value >>= 8;
    values.w = value & 255;
    return values / 255.0;
}
uint Encode(half4 value)
{
    uint4 v = value * 255;
    uint result = 0;
    result |= v.w & 255;
    result <<= 8;
    result |= v.z & 255;
    result <<= 8;
    result |= v.y & 255;
    result <<= 8;
    result |= v.x & 255;
    return result;
}
static const float Pi = 3.141592654;

struct SH9
{
    float c[9];
};

SH9 SHCosineLobe(float3 normal)
{
    float x = normal.x; float y = normal.y; float z = normal.z;
	float x2 = x * x; float y2 = y * y; float z2 = z * z;
    SH9 sh;
				sh.c[0] = 1.0 / (2.0 * sqrt(Pi));
				sh.c[1] = 0.5 * sqrt(3 / Pi) * z;
				sh.c[2] = 0.5 * sqrt(3 / Pi) * x;
				sh.c[3] = 0.5 * sqrt(3 / Pi) * y;
				sh.c[4] = 0.25 * sqrt(5 / Pi) * (2 * z2 - x2 - y2);
				sh.c[5] = 0.5 * sqrt(15/Pi) * z * x;
				sh.c[6] = 0.5 * sqrt(15/Pi) * z * y;
				sh.c[7] = 0.25 * sqrt(15 / Pi) * (x2 - y2);
				sh.c[8] = 0.5 * sqrt(15/Pi) * y * x;

    return sh;
}

void SHCosineLobe(float3 normal, out float4 first, out float4 second, out float third)
{
    float x = normal.x; float y = normal.y; float z = normal.z;
	float x2 = x * x; float y2 = y * y; float z2 = z * z;
				first = float4(1.0 / (2.0 * sqrt(Pi)),
				 0.5 * sqrt(3 / Pi) * z,
				 0.5 * sqrt(3 / Pi) * x,
				 0.5 * sqrt(3 / Pi) * y);
				second =float4( 0.25 * sqrt(5 / Pi) * (2 * z2 - x2 - y2),
				 0.5 * sqrt(15/Pi) * z * x,
				 0.5 * sqrt(15/Pi) * z * y,
				 0.25 * sqrt(15 / Pi) * (x2 - y2));
				third = 0.5 * sqrt(15/Pi) * y * x;
}

#define GETCOEFF(normal)\
float Y00     = 0.282095;\
float Y11     = 0.488603 * normal.x;\
float Y10     = 0.488603 * normal.z;\
float Y1_1    = 0.488603 * normal.y;\
float Y21     = 1.092548 * normal.x*normal.z;\
float Y2_1    = 1.092548 * normal.y*normal.z;\
float Y2_2    = 1.092548 * normal.y*normal.x;\
float Y20     = 0.946176 * normal.z * normal.z - 0.315392;\
float Y22     = 0.546274 * (normal.x*normal.x - normal.y*normal.y);

    int DownDimension(int3 coord, int2 xysize)
    {
        int3 multi = int3(xysize.y * xysize.x, xysize.x, 1);
        return dot(coord.zyx, multi);
    }

    int3 UpDimension(int coord, int2 xysize)
    {
        int xy = xysize.x * xysize.y;
        return int3(coord % xysize.x, (coord % xy) / xysize.x, coord / xy);
    }


#endif