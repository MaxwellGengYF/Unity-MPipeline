#ifndef SH_RUNTIME
#define SH_RUNTIME
#include "GlobalIllumination.cginc"
            float4x4 _WorldToLocalMatrix;
            
            float3 GetSHUV(float3 worldPos)
            {
                 float4 localPos = mul(_WorldToLocalMatrix, float4(worldPos, 1));
                 localPos /= localPos.w;
                 return localPos.xyz + 0.5;
                 
            }

            float3 GetSHColor(float3 color[9], float3 worldNormal)
            {
                const float A0 = 3.1415927;
				const float A1 = 2.094395;
				const float A2 = 0.785398;
                SH9 sh = SHCosineLobe(worldNormal);
                float3 irradiance = 0;
                [unroll]
                for(int i = 0; i < 9; ++i)
                {
                    irradiance += sh.c[i] * color[i];
                }
                return irradiance;
            }
           
#endif