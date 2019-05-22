using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Unity.Mathematics.math;
using MPipeline;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
namespace MPipeline {
    public unsafe struct VolumetricGeometry
    {
        private ComputeBuffer areaLightBuffer;
        private const int INIT_BUFFER_CAPACITY = 20;
        private NativeArray<AreaLight> areaCullResult;
        private VolJob job;
        private JobHandle jobHandle;
        
        public void Init()
        {
            areaLightBuffer = new ComputeBuffer(INIT_BUFFER_CAPACITY, sizeof(AreaLight));
            
        }

        public bool Check()
        {
            return areaLightBuffer.IsValid();
        }

        public void PreRender(ref PipelineCommandData data, PropertySetEvent proper)
        {
            if (!AreaVolumeProbe.allAreaLight.isCreated) return;
            areaCullResult = new NativeArray<AreaLight>(AreaVolumeProbe.allAreaLight.Length, Allocator.Temp);
            job = new VolJob
            {
                count = 0,
                light = areaCullResult.Ptr(),
                allArea = AreaVolumeProbe.allAreaLight.unsafePtr,
                planes = (float4*)proper.frustumPlanes.Ptr()
            };
            jobHandle = job.ScheduleRefBurst(AreaVolumeProbe.allAreaLight.Length, 32);
        }

        public void Dispose()
        {
            if(areaLightBuffer != null)
            {
                areaLightBuffer.Dispose();
                areaLightBuffer = null;
            }
        }

        public void GetAreaLightBuffer(out ComputeBuffer buffer, out int count)
        {
            if (!AreaVolumeProbe.allAreaLight.isCreated)
            {
                buffer = areaLightBuffer;
                count = 0;
                return;
            }
            jobHandle.Complete();
            if(job.count > areaLightBuffer.count)
            {
                areaLightBuffer.Dispose();
                areaLightBuffer = new ComputeBuffer(job.count, sizeof(AreaLight));
            }
            areaLightBuffer.SetData(areaCullResult, 0, 0, job.count);
            buffer = areaLightBuffer;
            count = job.count;
        }
        [BurstCompile]
        public struct VolJob : IJobParallelFor
        {
            [NativeDisableUnsafePtrRestriction]
            public AreaLight* light;
            [NativeDisableUnsafePtrRestriction]
            public AreaVolumeProbe.AreaLightComponent* allArea;
            [NativeDisableUnsafePtrRestriction]
            public float4* planes;
            public int count;
            public void Execute(int index)
            {
                ref AreaVolumeProbe.AreaLightComponent currentLit = ref allArea[index];
                if (VectorUtility.BoxIntersect(ref currentLit.localToWorld, currentLit.center, currentLit.extent, planes, 6))
                {
                    int currentCount = System.Threading.Interlocked.Increment(ref count) - 1;
                    light[currentCount] = currentLit.area;
                }
            }
        }
    }
}