using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
namespace MPipeline {
    public unsafe abstract class VoxelFogBase : MonoBehaviour
    {
        public struct VoxelCubeData
        {
            public float4x4 localToWorldMatrix;
            public float4x4 worldToLocalMatrix;
            public float3 minPoint;
            public float3 maxPoint;
            [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction]
            public void* ptr;
        }
        public static NativeList<VoxelCubeData> allVoxelData;
        public abstract Texture GetVoxel();
        private int index = -1;
        private void GetMinMax(out float3 minPoint, out float3 maxPoint)
        {
            float3 extent = transform.localScale * 0.5f;
            float3 pos = transform.position;
            minPoint = pos + extent;
            maxPoint = minPoint;
            float3 currPoint = pos + float3(extent.x, extent.y, -extent.z);
            minPoint = min(minPoint, currPoint);
            maxPoint = max(maxPoint, currPoint);
            currPoint = pos + float3(extent.x, -extent.y, extent.z);
            minPoint = min(minPoint, currPoint);
            maxPoint = max(maxPoint, currPoint);
            currPoint = pos + float3(extent.x, -extent.y, -extent.z);
            minPoint = min(minPoint, currPoint);
            maxPoint = max(maxPoint, currPoint);
            currPoint = pos + float3(-extent.x, extent.y, extent.z);
            minPoint = min(minPoint, currPoint);
            maxPoint = max(maxPoint, currPoint);
            currPoint = pos + float3(-extent.x, extent.y, -extent.z);
            minPoint = min(minPoint, currPoint);
            maxPoint = max(maxPoint, currPoint);
            currPoint = pos + float3(-extent.x, -extent.y, extent.z);
            minPoint = min(minPoint, currPoint);
            maxPoint = max(maxPoint, currPoint);
            currPoint = pos - extent;
            minPoint = min(minPoint, currPoint);
            maxPoint = max(maxPoint, currPoint);
        }
        private void OnEnable()
        {
            if (!allVoxelData.isCreated) allVoxelData = new NativeList<VoxelCubeData>(10, Unity.Collections.Allocator.Persistent);
            index = allVoxelData.Length;
            float3 minV, maxV;
            GetMinMax(out minV, out maxV);
            allVoxelData.Add(new VoxelCubeData
            {
                localToWorldMatrix = transform.localToWorldMatrix,
                worldToLocalMatrix = transform.worldToLocalMatrix,
                ptr = MUnsafeUtility.GetManagedPtr(this),
                minPoint = minV,
                maxPoint = maxV
            });
            OnEnableFunc();
        }

        public void UpdateData()
        {
            ref VoxelCubeData data = ref allVoxelData[index];
            data.localToWorldMatrix = transform.localToWorldMatrix;
            data.worldToLocalMatrix = transform.worldToLocalMatrix;
            GetMinMax(out data.minPoint, out data.maxPoint);
        }

        private void OnDisable()
        {
            if(index >= 0)
            {
                allVoxelData[index] = allVoxelData[allVoxelData.Length - 1];
                allVoxelData.RemoveLast();
                VoxelFogBase data = MUnsafeUtility.GetObject<VoxelFogBase>(allVoxelData[index].ptr);
                data.index = index;
                index = -1;
            }
            OnDisableFunc();
        }
        protected virtual void OnEnableFunc() { }
        protected virtual void OnDisableFunc() { }
    }
}