using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Jobs;
using System.Threading;
using Unity.Collections;
using UnityEngine.Jobs;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using System;
using static Unity.Mathematics.math;
namespace MPipeline
{
    public unsafe struct ShadowAvaliableCheck : IJob
    {
        public Cone* allSpotLightPosition;
        public float4* allPointLightPosition;
        public float3 cameraPos;
        public float shadowDistance;
        public bool spotExists;
        public bool pointExists;
        public void Init(Camera cam, float shadowDist)
        {
            cameraPos = cam.transform.position;
            shadowDistance = shadowDist;
            List<MLight> allSpotLight = MLight.avaliableSpotShadowIndices;
            List<MLight> allPointLight = MLight.avaliableCubemapIndices;
            if (allSpotLight != null && allSpotLight.Count > 0)
            {
                allSpotLightPosition = new NativeArray<Cone>(allSpotLight.Count, Allocator.Temp, NativeArrayOptions.UninitializedMemory).Ptr();

                for (int i = 0; i < allSpotLight.Count; ++i)
                {
                    MLight ml = allSpotLight[i];
                    Transform tr = ml.transform;
                    allSpotLightPosition[i] = new Cone(tr.position, ml.light.range, tr.forward, ml.light.spotAngle);
                }
                spotExists = true;
            }
            else
            {
                spotExists = false;
            }
            if (allPointLight != null && allPointLight.Count > 0)
            {
                allPointLightPosition = new NativeArray<float4>(allPointLight.Count, Allocator.Temp, NativeArrayOptions.UninitializedMemory).Ptr();
                for (int i = 0; i < allPointLight.Count; ++i)
                {
                    MLight ml = allPointLight[i];
                    Transform tr = ml.transform;
                    allPointLightPosition[i] = float4(tr.position, ml.light.range);
                }
                pointExists = true;
            }
            else
            {
                pointExists = false;
            }
        }
        public void Execute()
        {
            List<MLight> allSpotLight = MLight.avaliableSpotShadowIndices;
            List<MLight> allPointLight = MLight.avaliableCubemapIndices;
            UIntPtr* allPtr = stackalloc UIntPtr[max(spotExists ? allSpotLight.Count : 0, pointExists ? allPointLight.Count : 0)];
            int ptrCount = 0;
            if (spotExists)
            {
                for (int i = 0; i < allSpotLight.Count; ++i)
                {
                    ref Cone c = ref allSpotLightPosition[i];
                    float3 dir = c.vertex - cameraPos;
                    if (!MathLib.ConeIntersect(allSpotLightPosition[i], MathLib.GetPlane(dir, cameraPos + dir * shadowDistance)))
                    {
                        allPtr[ptrCount] = new UIntPtr(MUnsafeUtility.GetManagedPtr(allSpotLight[i]));
                        ptrCount++;
                    }
                }
                for (int i = 0; i < ptrCount; ++i)
                {
                    MLight ml = MUnsafeUtility.GetObject<MLight>(allPtr[i].ToPointer());
                    ml.RemoveLightFromAtlas(false);
                    ml.updateShadowCache = true;
                }
            }
            if (pointExists)
            {
                ptrCount = 0;
                for (int i = 0; i < allPointLight.Count; ++i)
                {
                    ref float4 p = ref allPointLightPosition[i];
                    float dist = p.w + shadowDistance;
                    if (lengthsq(p.xyz - cameraPos) > (dist * dist))
                    {
                        allPtr[ptrCount] = new UIntPtr(MUnsafeUtility.GetManagedPtr(allPointLight[i]));
                        ptrCount++;
                    }
                }
                for (int i = 0; i < ptrCount; ++i)
                {
                    MLight ml = MUnsafeUtility.GetObject<MLight>(allPtr[i].ToPointer());
                    ml.RemoveLightFromAtlas(true);
                    
                    ml.updateShadowCache = true;
                }
            }
        }
    }
}