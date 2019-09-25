#ifndef __PERLINNOISE_INCLUDE__
#define __PERLINNOISE_INCLUDE__

float Noise(float x);
float Noise(float x, float y);
float Noise(float2 coord);
float Noise(float x, float y, float z);
float Noise(float3 coord);
float Fbm(float x, int octave);
float Fbm(float2 coord, int octave);
float Fbm(float x, float y, int octave);
float Fbm(float3 coord, int octave);
float Fbm(float x, float y, float z, int octave);
float Fade(float t);
float Grad(int hash, float x);
float Grad(int hash, float x, float y);
float Grad(int hash, float x, float y, float z);
float Lerp(float t, float a, float b)
{
        return a + t * (b - a);
}
 static const int perm[257] = {
        151,160,137,91,90,15,
        131,13,201,95,96,53,194,233,7,225,140,36,103,30,69,142,8,99,37,240,21,10,23,
        190, 6,148,247,120,234,75,0,26,197,62,94,252,219,203,117,35,11,32,57,177,33,
        88,237,149,56,87,174,20,125,136,171,168, 68,175,74,165,71,134,139,48,27,166,
        77,146,158,231,83,111,229,122,60,211,133,230,220,105,92,41,55,46,245,40,244,
        102,143,54, 65,25,63,161, 1,216,80,73,209,76,132,187,208, 89,18,169,200,196,
        135,130,116,188,159,86,164,100,109,198,173,186, 3,64,52,217,226,250,124,123,
        5,202,38,147,118,126,255,82,85,212,207,206,59,227,47,16,58,17,182,189,28,42,
        223,183,170,213,119,248,152, 2,44,154,163, 70,221,153,101,155,167, 43,172,9,
        129,22,39,253, 19,98,108,110,79,113,224,232,178,185, 112,104,218,246,97,228,
        251,34,242,193,238,210,144,12,191,179,162,241, 81,51,145,235,249,14,239,107,
        49,192,214, 31,181,199,106,157,184, 84,204,176,115,121,50,45,127, 4,150,254,
        138,236,205,93,222,114,67,29,24,72,243,141,128,195,78,66,215,61,156,180,
        151
    };

     float Noise(float x)
    {
        int X = (int)(x) & 0xff;
        x -= (int)(x);
        float u = Fade(x);
        return Lerp(u, Grad(perm[X], x), Grad(perm[X+1], x-1)) * 2;
    }

    float Noise(float x, float y)
    {
        int X = (int)(x) & 0xff;
        int Y = (int)(y) & 0xff;
        x -= (int)(x);
        y -= (int)(y);
        float u = Fade(x);
        float v = Fade(y);
        int A = (perm[X  ] + Y) & 0xff;
        int B = (perm[X+1] + Y) & 0xff;
        return Lerp(v, Lerp(u, Grad(perm[A  ], x, y  ), Grad(perm[B  ], x-1, y  )),
                       Lerp(u, Grad(perm[A+1], x, y-1), Grad(perm[B+1], x-1, y-1)));
    }

    float Noise(float2 coord)
    {
        return Noise(coord.x, coord.y);
    }

    float Noise(float x, float y, float z)
    {
        int X = (int)(x) & 0xff;
        int Y = (int)(y) & 0xff;
        int Z = (int)(z) & 0xff;
        x -= (int)(x);
        y -= (int)(y);
        z -= (int)(z);
        float u = Fade(x);
        float v = Fade(y);
        float w = Fade(z);
        int A  = (perm[X  ] + Y) & 0xff;
        int B  = (perm[X+1] + Y) & 0xff;
        int AA = (perm[A  ] + Z) & 0xff;
        int BA = (perm[B  ] + Z) & 0xff;
        int AB = (perm[A+1] + Z) & 0xff;
        int BB = (perm[B+1] + Z) & 0xff;
        return Lerp(w, Lerp(v, Lerp(u, Grad(perm[AA  ], x, y  , z  ), Grad(perm[BA  ], x-1, y  , z  )),
                               Lerp(u, Grad(perm[AB  ], x, y-1, z  ), Grad(perm[BB  ], x-1, y-1, z  ))),
                       Lerp(v, Lerp(u, Grad(perm[AA+1], x, y  , z-1), Grad(perm[BA+1], x-1, y  , z-1)),
                               Lerp(u, Grad(perm[AB+1], x, y-1, z-1), Grad(perm[BB+1], x-1, y-1, z-1))));
    }

    float Noise(float3 coord)
    {
        return Noise(coord.x, coord.y, coord.z);
    }

float Fbm(float x, int octave)
    {
        float f = 0.0f;
        float w = 0.5f;
        for (int i = 0; i < octave; i++) {
            f += w * Noise(x);
            x *= 2.0f;
            w *= 0.5f;
        }
        return f;
    }

float Fbm(float2 coord, int octave)
    {
        float f = 0.0f;
        float w = 0.5f;
        for (int i = 0; i < octave; i++) {
            f += w * Noise(coord);
            coord *= 2.0f;
            w *= 0.5f;
        }
        return f;
    }

float Fbm(float x, float y, int octave)
    {
        return Fbm( float2(x, y), octave);
    }

float Fbm(float3 coord, int octave)
    {
        float f = 0.0f;
        float w = 0.5f;
        for (int i = 0; i < octave; i++) {
            f += w * Noise(coord);
            coord *= 2.0f;
            w *= 0.5f;
        }
        return f;
    }

float Fbm(float x, float y, float z, int octave)
    {
        return Fbm( float3(x, y, z), octave);
    }

      float Fade(float t)
    {
        return t * t * t * (t * (t * 6 - 15) + 10);
    }

     float Grad(int hash, float x)
    {
        return (hash & 1) == 0 ? x : -x;
    }

     float Grad(int hash, float x, float y)
    {
        return ((hash & 1) == 0 ? x : -x) + ((hash & 2) == 0 ? y : -y);
    }

     float Grad(int hash, float x, float y, float z)
    {
        int h = hash & 15;
        float u = h < 8 ? x : y;
        float v = h < 4 ? y : (h == 12 || h == 14 ? x : z);
        return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
    }

#endif