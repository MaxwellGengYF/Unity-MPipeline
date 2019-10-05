#ifndef __TEXTURE_INCLUDE__
#define __TEXTURE_INCLUDE__
  

   #define MAX_BRIGHTNESS 6
    uint EncodeColor(float3 rgb)
    {
        rgb = clamp(rgb, 0, MAX_BRIGHTNESS);
        float y = max(max(rgb.r, rgb.g), rgb.b);
        y = clamp(ceil(y * 255 / MAX_BRIGHTNESS), 1, 255);
        rgb *= 255 * 255 / (y * MAX_BRIGHTNESS);
        uint4 i = float4(rgb, y);
        return i.x | (i.y << 8) | (i.z << 16) | (i.w << 24);
    }

    float4 Decode(uint value)
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
uint Encode(float4 value)
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

    float3 DecodeColor(uint data)
    {
        float r = (data      ) & 0xff;
        float g = (data >>  8) & 0xff;
        float b = (data >> 16) & 0xff;
        float a = (data >> 24) & 0xff;
        return float3(r, g, b) * a * MAX_BRIGHTNESS / (255 * 255);
    }

#endif