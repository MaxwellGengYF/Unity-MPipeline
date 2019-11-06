using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using System.IO;
using Unity.Collections.LowLevel.Unsafe;
namespace MPipeline
{
    public unsafe struct SceneStreamLoader
    {
        FileStream fsm;
        public SceneStreamLoader(FileStream strm)
        {
            fsm = strm;
        }

        void LoadClusterData(int clusterCount, out NativeArray<Cluster> cluster, out NativeArray<Point> points, out NativeArray<int> triangleMats)
        {
            cluster = new NativeArray<Cluster>(clusterCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            points = new NativeArray<Point>(clusterCount * PipelineBaseBuffer.CLUSTERCLIPCOUNT, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            triangleMats = new NativeArray<int>(clusterCount * PipelineBaseBuffer.CLUSTERTRIANGLECOUNT, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            int length = cluster.Length * sizeof(Cluster) + points.Length * sizeof(Point) + triangleMats.Length * sizeof(int);
            byte[] bytes = SceneStreaming.GetByteArray(length);
            fsm.Read(bytes, 0, length);
            fixed (byte* b = bytes)
            {
                UnsafeUtility.MemCpy(cluster.Ptr(), b, cluster.Length * sizeof(Cluster));
                UnsafeUtility.MemCpy(points.Ptr(), b + cluster.Length * sizeof(Cluster), points.Length * sizeof(Point));
                UnsafeUtility.MemCpy(triangleMats.Ptr(), b + cluster.Length * sizeof(Cluster) + points.Length * sizeof(Point), triangleMats.Length * sizeof(int));
            }
        }

        void SaveClusterData(int clusterCount, NativeArray<Cluster> cluster, NativeArray<Point> points, NativeArray<int> triangleMats)
        {
            int length = cluster.Length * sizeof(Cluster) + points.Length * sizeof(Point) + triangleMats.Length * sizeof(int);
            byte[] bytes = SceneStreaming.GetByteArray(length);
            fixed (byte* b = bytes)
            {
                UnsafeUtility.MemCpy(b, cluster.Ptr(), cluster.Length * sizeof(Cluster));
                UnsafeUtility.MemCpy(b + cluster.Length * sizeof(Cluster), points.Ptr(), points.Length * sizeof(Point));
                UnsafeUtility.MemCpy(b + cluster.Length * sizeof(Cluster) + points.Length * sizeof(Point), triangleMats.Ptr(), triangleMats.Length * sizeof(int));
            }
            fsm.Write(bytes, 0, length);
        }

        void LoadGUIDArray(ref NativeList<int4x4> arr)
        {
            byte[] cacheArray = SceneStreaming.GetByteArray(4);
            fsm.Read(cacheArray, 0, 4);
            int len = *(int*)cacheArray.Ptr();
            if (arr.isCreated)
                arr.Clear();
            else arr = new NativeList<int4x4>(len, Allocator.Persistent);
            cacheArray = SceneStreaming.GetByteArray(sizeof(int4x4) * len);
            fsm.Read(cacheArray, 0, sizeof(int4x4) * len);
            int4x4* arrPtr = (int4x4*)cacheArray.Ptr();
            arr.AddRange(arrPtr, len);
        }

        void SaveGUIDArray(NativeList<int4x4> arr)
        {
            byte[] cacheArray = SceneStreaming.GetByteArray(arr.Length * sizeof(int4x4) + sizeof(int));
            int* intPtr = (int*)cacheArray.Ptr();
            *intPtr = arr.Length;
            UnsafeUtility.MemCpy(intPtr + 1, arr.unsafePtr, sizeof(int4x4) * arr.Length);
            fsm.Write(cacheArray, 0, arr.Length * sizeof(int4x4) + sizeof(int));
        }

        void LoadMaterialArray(ref NativeList<VirtualMaterial.MaterialProperties> arr)
        {
            byte[] cacheArray = SceneStreaming.GetByteArray(4);
            fsm.Read(cacheArray, 0, 4);
            int len = *(int*)cacheArray.Ptr();
            if (arr.isCreated)
                arr.Clear();
            else arr = new NativeList<VirtualMaterial.MaterialProperties>(len, Allocator.Persistent);
            cacheArray = SceneStreaming.GetByteArray(sizeof(VirtualMaterial.MaterialProperties) * len);
            fsm.Read(cacheArray, 0, sizeof(VirtualMaterial.MaterialProperties) * len);
            VirtualMaterial.MaterialProperties* arrPtr = (VirtualMaterial.MaterialProperties*)cacheArray.Ptr();
            arr.AddRange(arrPtr, len);
        }

        void SaveMaterialArray(NativeList<VirtualMaterial.MaterialProperties> arr)
        {
            byte[] cacheArray = SceneStreaming.GetByteArray(arr.Length * sizeof(VirtualMaterial.MaterialProperties) + sizeof(int));
            int* intPtr = (int*)cacheArray.Ptr();
            *intPtr = arr.Length;
            UnsafeUtility.MemCpy(intPtr + 1, arr.unsafePtr, sizeof(VirtualMaterial.MaterialProperties) * arr.Length);
            fsm.Write(cacheArray, 0, arr.Length * sizeof(VirtualMaterial.MaterialProperties) + sizeof(int));
        }
    }
}