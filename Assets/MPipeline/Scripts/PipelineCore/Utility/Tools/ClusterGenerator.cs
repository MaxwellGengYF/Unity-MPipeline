#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System.IO;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Unity.Jobs;
using System;
namespace MPipeline
{
    public unsafe static class ClusterGenerator
    {
        struct Triangle
        {
            public Point a;
            public Point b;
            public Point c;
            public int materialID;
            public Triangle* last;
            public Triangle* next;
        }
        struct Voxel
        {
            public Triangle* start;
            public int count;
            public void Add(Triangle* ptr)
            {
                if (start != null)
                {
                    start->last = ptr;
                    ptr->next = start;
                }
                start = ptr;
                count++;
            }
            public Triangle* Pop()
            {
                if (start->next != null)
                {
                    start->next->last = null;
                }
                Triangle* last = start;
                start = start->next;
                count--;
                return last;
            }
        }
        /// <returns></returns> Cluster Count
        public static int GenerateCluster(NativeList<Point> pointsFromMesh, NativeList<int> mats, Bounds bd, int voxelCount, ref SceneStreamLoader loader)
        {
            NativeList<Cluster> boxes; NativeList<Point> points; NativeList<int> outMats;
            GetCluster(pointsFromMesh, mats, bd, out boxes, out points, out outMats, voxelCount);
            loader.cluster = boxes;
            loader.points = points;
            loader.triangleMats = outMats;
            //Dispose Native Array
            return boxes.Length;
        }

        public static void GetCluster(NativeList<Point> pointsFromMesh, NativeList<int> materialsFromMesh, Bounds bd, out NativeList<Cluster> boxes, out NativeList<Point> points, out NativeList<int> outMats, int voxelCount)
        {
            NativeList<Triangle> trs = GenerateTriangle(pointsFromMesh, materialsFromMesh);
            Voxel[,,] voxels = GetVoxelData(trs, voxelCount, bd);
            GetClusterFromVoxel(voxels, out boxes, out points, out outMats, pointsFromMesh.Length, voxelCount);
        }

        private static NativeList<Triangle> GenerateTriangle(NativeList<Point> points, NativeList<int> materialID)
        {
            NativeList<Triangle> retValue = new NativeList<Triangle>(points.Length / 3, Allocator.Temp);
            for (int i = 0; i < points.Length; i += 3)
            {
                Triangle tri = new Triangle
                {
                    a = points[i],
                    b = points[i + 1],
                    c = points[i + 2],
                    materialID = materialID[i / 3],
                    last = null,
                    next = null
                };
                retValue.Add(tri);
            }
            return retValue;
        }

        private static Voxel[,,] GetVoxelData(NativeList<Triangle> trianglesFromMesh, int voxelCount, Bounds bound)
        {
            Voxel[,,] voxels = new Voxel[voxelCount, voxelCount, voxelCount];
            for (int x = 0; x < voxelCount; ++x)
                for (int y = 0; y < voxelCount; ++y)
                    for (int z = 0; z < voxelCount; ++z)
                    {
                        voxels[x, y, z] = new Voxel();
                    }
            float3 downPoint = bound.center - bound.extents;
            for (int i = 0; i < trianglesFromMesh.Length; ++i)
            {
                ref Triangle tr = ref trianglesFromMesh[i];
                float3 position = (tr.a.vertex + tr.b.vertex + tr.c.vertex) / 3;
                float3 localPos = saturate((position - downPoint) / bound.size);
                int3 coord = (int3)(localPos * voxelCount);
                coord = min(coord, voxelCount - 1);
                voxels[coord.x, coord.y, coord.z].Add(tr.Ptr());
            }
            return voxels;
        }

        private static void GetClusterFromVoxel(Voxel[,,] voxels, out NativeList<Cluster> Clusteres, out NativeList<Point> points, out NativeList<int> matIndex, int vertexCount, int voxelSize)
        {
            int3 voxelCoord = 0;
            float3 lessPoint = float.MaxValue;
            float3 morePoint = float.MinValue;
            int clusterCount = Mathf.CeilToInt((float)vertexCount / PipelineBaseBuffer.CLUSTERCLIPCOUNT);
            points = new NativeList<Point>(clusterCount * PipelineBaseBuffer.CLUSTERCLIPCOUNT, Allocator.Temp);
            matIndex = new NativeList<int>(clusterCount * PipelineBaseBuffer.CLUSTERTRIANGLECOUNT, Allocator.Temp);
            Clusteres = new NativeList<Cluster>(clusterCount, Allocator.Temp);
            //Collect all full
            for (int i = 0; i < clusterCount - 1; ++i)
            {
                NativeList<Point> currentPoints = new NativeList<Point>(PipelineBaseBuffer.CLUSTERCLIPCOUNT, Allocator.Temp);
                NativeList<int> currentMatIndex = new NativeList<int>(PipelineBaseBuffer.CLUSTERTRIANGLECOUNT, Allocator.Temp);
                int lastedVertex = PipelineBaseBuffer.CLUSTERCLIPCOUNT / 3;
                ref Voxel currentVoxel = ref voxels[voxelCoord.x, voxelCoord.y, voxelCoord.z];
                int loopStart = min(currentVoxel.count, max(lastedVertex - currentVoxel.count, 0));
                for (int j = 0; j < loopStart; j++)
                {
                    Triangle* tri = currentVoxel.Pop();
                    currentPoints.Add(tri->a);
                    currentPoints.Add(tri->b);
                    currentPoints.Add(tri->c);
                    currentMatIndex.Add(tri->materialID);
                }
                lastedVertex -= loopStart;

                for (int size = 1; lastedVertex > 0; size++)
                {
                    int3 leftDown = max(voxelCoord - size, 0);
                    int3 rightUp = min(voxelSize, voxelCoord + size);
                    for (int x = leftDown.x; x < rightUp.x; ++x)
                        for (int y = leftDown.y; y < rightUp.y; ++y)
                            for (int z = leftDown.z; z < rightUp.z; ++z)
                            {
                                ref Voxel vxl = ref voxels[x, y, z];
                                int vxlCount = vxl.count;
                                for (int j = 0; j < vxlCount; ++j)
                                {
                                    voxelCoord = int3(x, y, z);
                                    Triangle* tri = vxl.Pop();
                                    //   try
                                    // {
                                    currentPoints.Add(tri->a);
                                    currentPoints.Add(tri->b);
                                    currentPoints.Add(tri->c);
                                    currentMatIndex.Add(tri->materialID);
                                    /* }
                                     catch
                                     {
                                         Debug.Log(vxlCount);
                                         Debug.Log(tri->a);
                                         Debug.Log(tri->b);
                                         Debug.Log(tri->c);
                                         Debug.Log(currentPoints.Length);
                                         return;
                                     }*/
                                    lastedVertex--;
                                    if (lastedVertex <= 0) goto CONTINUE;
                                }
                            }

                }
            CONTINUE:
                points.AddRange(currentPoints);
                matIndex.AddRange(currentMatIndex);
                lessPoint = float.MaxValue;
                morePoint = float.MinValue;
                foreach (var j in currentPoints)
                {
                    lessPoint = lerp(lessPoint, j.vertex, (int3)(lessPoint > j.vertex));
                    morePoint = lerp(morePoint, j.vertex, (int3)(morePoint < j.vertex));
                }
                Cluster cb = new Cluster
                {
                    extent = (morePoint - lessPoint) / 2,
                    position = (morePoint + lessPoint) / 2
                };
                Clusteres.Add(cb);
                currentPoints.Dispose();
                currentMatIndex.Dispose();
            }
            //Collect and degenerate
            NativeList<Point> leftedPoints = new NativeList<Point>(PipelineBaseBuffer.CLUSTERCLIPCOUNT, Allocator.Temp);
            NativeList<int> leftedMatID = new NativeList<int>(PipelineBaseBuffer.CLUSTERTRIANGLECOUNT, Allocator.Temp);
            for (int x = 0; x < voxelSize; ++x)
                for (int y = 0; y < voxelSize; ++y)
                    for (int z = 0; z < voxelSize; ++z)
                    {
                        ref Voxel vxl = ref voxels[x, y, z];
                        int vxlCount = vxl.count;
                        for (int j = 0; j < vxlCount; ++j)
                        {
                            Triangle* tri = vxl.Pop();
                            leftedPoints.Add(tri->a);
                            leftedPoints.Add(tri->b);
                            leftedPoints.Add(tri->c);
                            leftedMatID.Add(tri->materialID);

                        }
                    }
            if (leftedPoints.Length <= 0) return;
            lessPoint = float.MaxValue;
            morePoint = float.MinValue;
            foreach (var j in leftedPoints)
            {
                lessPoint = lerp(lessPoint, j.vertex, (int3)(lessPoint > j.vertex));
                morePoint = lerp(morePoint, j.vertex, (int3)(morePoint < j.vertex));
            }
            Cluster lastBox = new Cluster
            {
                extent = (morePoint - lessPoint) / 2,
                position = (morePoint + lessPoint) / 2
            };
            Clusteres.Add(lastBox);
            for (int i = leftedPoints.Length; i < PipelineBaseBuffer.CLUSTERCLIPCOUNT; i++)
            {
                leftedPoints.Add(new Point());
            }
            for(int i = leftedMatID.Length; i < PipelineBaseBuffer.CLUSTERTRIANGLECOUNT; ++i)
            {
                leftedMatID.Add(0);
            }
            points.AddRange(leftedPoints);
            matIndex.AddRange(leftedMatID);
        }
    }
}
#endif