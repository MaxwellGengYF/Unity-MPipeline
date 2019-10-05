using Unity.Mathematics;
using static Unity.Mathematics.math;
using Unity.Collections;
public static unsafe class SeparableSSS
{
    public static void CalculateKernel(float4* kernel, int nSamples, float3 strength, float3 falloff)
    {
        float RANGE = nSamples > 20 ? 3.0f : 2.0f;
        float EXPONENT = 2.0f;
        // Calculate the SSS_Offset_UV:
        float step = 2.0f * RANGE / (nSamples - 1);
        for (int i = 0; i < nSamples; i++)
        {
            float o = -RANGE + i * step;
            float sign = o < 0.0f ? -1.0f : 1.0f;
            float w = RANGE * sign * abs(pow(o, EXPONENT)) / pow(RANGE, EXPONENT);
            kernel[i] = float4(0, 0, 0, w);
        }
        // Calculate the SSS_Scale:
        for (int i = 0; i < nSamples; i++)
        {
            float w0 = i > 0 ? abs(kernel[i].w - kernel[i - 1].w) : 0.0f;
            float w1 = i < nSamples - 1 ? abs(kernel[i].w - kernel[i + 1].w) : 0.0f;
            float area = (w0 + w1) / 2.0f;
            float3 temp = profile(kernel[i].w, falloff);
            kernel[i] = new float4(area * temp.xyz, kernel[i].w);
        }
        float4 t = kernel[nSamples / 2];
        for (int i = nSamples / 2; i > 0; i--)
            kernel[i] = kernel[i - 1];
        kernel[0] = t;
        float4 sum = 0;

        for (int i = 0; i < nSamples; i++)
        {
            sum.xyz += kernel[i].xyz;
        }

        for (int i = 0; i < nSamples; i++)
        {
            ref float4 vecx = ref kernel[i];
            vecx.xyz /= sum.xyz;
        }

        float4 vec = kernel[0];
        vec.xyz = (1.0f - strength.xyz) * 1.0f + strength.xyz * vec.xyz;
        kernel[0] = vec;

        for (int i = 1; i < nSamples; i++)
        {
            ref var vect = ref kernel[i];
            vect.xyz *= strength.xyz;
        }
    }


    private static float3 gaussian(float variance, float r, float3 falloff)
    {
        float3 g;

        float3 rr1 = r / (0.001f + falloff.xyz);
        g = exp((-(rr1 * rr1)) / (2.0f * variance)) / (2.0f * 3.14f * variance);
        return g;
    }
    private static float3 profile(float r, float3 falloff)
    {
        return 0.100f * gaussian(0.0484f, r, falloff) +
                0.118f * gaussian(0.187f, r, falloff) +
                0.113f * gaussian(0.567f, r, falloff) +
                0.358f * gaussian(1.99f, r, falloff) +
                0.078f * gaussian(7.41f, r, falloff);
    }
}