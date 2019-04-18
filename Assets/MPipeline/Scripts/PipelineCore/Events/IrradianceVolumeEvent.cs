using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using static MUnsafeUtility;
using Unity.Jobs;
namespace MPipeline
{
    [System.Serializable]
    public unsafe struct IrradianceCuller
    {
        public IrradianceResources targetResoures;
        private JobHandle handle;
        public NativeList<int> cullingResult;


        public void PreRenderFrame( ref PipelineCommandData data, PropertySetEvent proper)
        {
            if (IrradianceVolumeController.current == null)
            {
                return;
            }
            NativeList<LoadedIrradiance> allVolume = IrradianceVolumeController.current.loadedIrradiance;
            cullingResult = new NativeList<int>(allVolume.Length, Allocator.Temp);
            handle = new IrradianceVolumeCulling
            {
                cameraFrustum = (float4*)UnsafeUtility.AddressOf(ref proper.frustumPlanes[0]),
                irradiance = allVolume.unsafePtr,
                result = cullingResult
            }.Schedule(allVolume.Length, 32);
        }
        public void FrameUpdate()
        {
            if (IrradianceVolumeController.current == null)
            {
                cullingResult = new NativeList<int>();
                return;
            }
            handle.Complete();
        }
        public unsafe struct IrradianceVolumeCulling : IJobParallelFor
        {
            [NativeDisableUnsafePtrRestriction]
            public LoadedIrradiance* irradiance;
            public NativeList<int> result;
            [NativeDisableUnsafePtrRestriction]
            public float4* cameraFrustum;
            public void Execute(int index)
            {
                ref LoadedIrradiance data = ref irradiance[index];
                if (VectorUtility.BoxIntersect(data.localToWorld, data.position, cameraFrustum, 6))
                {
                    result.ConcurrentAdd(index);
                }
            }
        }
    }
}
