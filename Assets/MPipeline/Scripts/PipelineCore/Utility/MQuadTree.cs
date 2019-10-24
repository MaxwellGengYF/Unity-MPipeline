using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using static Unity.Mathematics.math;
using Unity.Jobs;
using System.Threading;
using UnityEngine.Rendering;
namespace MPipeline
{
    public struct TerrainUnloadData
    {
        public enum Operator
        {
            Unload, Combine
        }
        public Operator ope;
        public int size;
        public int2 startIndex;
    }
    public struct TerrainLoadData
    {
        public enum Operator
        {
            Load, Separate, Update
        }
        public Operator ope;
        public int size;
        public LayerMask targetDecalLayer;
        public int2 startIndex;
        public int2 rootPos;
        public float3 maskScaleOffset;
        public VirtualTextureLoader.LoadingHandler handler0;
        public VirtualTextureLoader.LoadingHandler handler1;
        public VirtualTextureLoader.LoadingHandler handler2;
        public VirtualTextureLoader.LoadingHandler handler3;
    }
    public unsafe struct TerrainQuadTree
    {
        public enum LocalPos
        {
            LeftDown, LeftUp, RightDown, RightUp
        };
        public TerrainQuadTree* leftDown { get; private set; }
        public TerrainQuadTree* leftUp { get; private set; }
        public TerrainQuadTree* rightDown { get; private set; }
        public TerrainQuadTree* rightUp { get; private set; }
        public int lodLevel;
        public int2 localPosition;
        private double distOffset;
        public int2 VirtualTextureIndex => localPosition * (int)(0.1 + pow(2.0, MTerrain.current.allLodLevles.Length - 1 - lodLevel));
        public bool isRendering { get; private set; }
        public double worldSize { get; private set; }
        public int2 rootPos;
        public double3 maskScaleOffset;
        private LayerMask decalMask;
        public TerrainQuadTree(int parentLodLevel, LocalPos sonPos, int2 parentPos, double worldSize, double3 maskScaleOffset, int2 rootPos)
        {
            this.worldSize = worldSize;
            distOffset = MTerrain.current.terrainData.lodDeferredOffset;
            isRendering = false;
            lodLevel = parentLodLevel + 1;
            leftDown = null;
            leftUp = null;
            rightDown = null;
            rightUp = null;
            localPosition = parentPos * 2;
            switch (sonPos)
            {
                case LocalPos.LeftUp:
                    localPosition += int2(0, 1);
                    break;
                case LocalPos.RightDown:
                    localPosition += int2(1, 0);
                    break;
                case LocalPos.RightUp:
                    localPosition += 1;
                    break;
            }
            int decalLayer = lodLevel - MTerrain.current.decalLayerOffset;
            decalMask = decalLayer < 0 ? (LayerMask)0 : MTerrain.current.terrainData.allDecalLayers[decalLayer];
            if (lodLevel > MTerrain.current.lodOffset - 1)
            {
                this.rootPos = rootPos;
                double subScale = maskScaleOffset.x * 0.5;
                double2 offset = maskScaleOffset.yz;
                switch (sonPos)
                {
                    case LocalPos.LeftUp:
                        offset += double2(0, subScale);
                        break;
                    case LocalPos.RightDown:
                        offset += double2(subScale, 0);
                        break;
                    case LocalPos.RightUp:
                        offset += subScale;
                        break;
                }
                this.maskScaleOffset = double3(subScale, offset);
            }
            else
            {
                this.rootPos = localPosition / 2;
                this.maskScaleOffset = maskScaleOffset;
            }
            if (lodLevel == MTerrain.current.lodOffset)
            {
                MTerrain.current.maskLoadList.Add(new MTerrain.MaskLoadCommand
                {
                    load = true,
                    pos = this.rootPos + (int2)this.maskScaleOffset.yz
                });
            }
        }
        public int VirtualTextureSize => (int)(0.1 + pow(2.0, MTerrain.current.allLodLevles.Length - 1 - lodLevel));
        public double2 CornerWorldPos
        {
            get
            {
                double2 chunkPos = MTerrain.current.terrainData.screenOffset;
                //    chunkPos *= MTerrain.current.largestChunkSize;
                chunkPos += (MTerrain.current.terrainData.largestChunkSize / pow(2, lodLevel)) * (double2)localPosition;
                return chunkPos;
            }
        }
        public double4 BoundedWorldPos
        {
            get
            {
                double2 leftCorner = CornerWorldPos;
                double2 rightCorner = MTerrain.current.terrainData.screenOffset + (MTerrain.current.terrainData.largestChunkSize / pow(2, lodLevel)) * ((double2)(localPosition + 1));
                return double4(leftCorner, rightCorner);
            }
        }
        public double2 CenterWorldPos
        {
            get
            {
                double2 chunkPos = MTerrain.current.terrainData.screenOffset;
                //chunkPos *= MTerrain.current.largestChunkSize;
                chunkPos += (MTerrain.current.terrainData.largestChunkSize / pow(2, lodLevel)) * ((double2)(localPosition) + 0.5);
                return chunkPos;
            }
        }
        public void GetMaterialMaskRoot(double2 xzPosition, double radius, ref NativeList<ulong> allTreeNode)
        {
            if (lodLevel == MTerrain.current.lodOffset)
            {
                double4 bounded = BoundedWorldPos;
                bool4 v = bool4(xzPosition + radius > bounded.xy, xzPosition - radius < bounded.zw);
                if (v.x && v.y && v.z && v.w)
                {
                    allTreeNode.Add((ulong)this.Ptr());
                }
            }
            else if (lodLevel < MTerrain.current.lodOffset)
            {
                if (leftDown != null)
                {
                    leftDown->GetMaterialMaskRoot(xzPosition, radius, ref allTreeNode);
                    rightDown->GetMaterialMaskRoot(xzPosition, radius, ref allTreeNode);
                    leftUp->GetMaterialMaskRoot(xzPosition, radius, ref allTreeNode);
                    rightUp->GetMaterialMaskRoot(xzPosition, radius, ref allTreeNode);
                }
            }
        }

        public void UpdateChunks(double3 circleRange)
        {
            if (isRendering)
            {
                double4 boundedPos = BoundedWorldPos;
                if ((boundedPos.x - circleRange.x < circleRange.z &&
                    boundedPos.y - circleRange.y < circleRange.z &&
                    circleRange.x - boundedPos.z < circleRange.z &&
                    circleRange.y - boundedPos.w < circleRange.z))
                {
                    MTerrain.current.loadDataList.Add(new TerrainLoadData
                    {
                        ope = TerrainLoadData.Operator.Update,
                        startIndex = VirtualTextureIndex,
                        size = VirtualTextureSize,
                        rootPos = rootPos,
                        maskScaleOffset = (float3)maskScaleOffset,
                        targetDecalLayer = decalMask
                    });
                }
            }
            if (leftDown != null)
            {
                leftDown->UpdateChunks(circleRange);
                leftUp->UpdateChunks(circleRange);
                rightDown->UpdateChunks(circleRange);
                rightUp->UpdateChunks(circleRange);
            }
        }

        public void Dispose()
        {
            if (lodLevel == MTerrain.current.lodOffset)
            {
                MTerrain.current.maskLoadList.Add(new MTerrain.MaskLoadCommand
                {
                    load = false,
                    pos = rootPos + (int2)maskScaleOffset.yz
                });
            }
            if (MTerrain.current != null)
            {
                int2 startIndex = VirtualTextureIndex;
                if (isRendering)
                {
                    MTerrain.current.textureCapacity++;
                    MTerrain.current.unloadDataList.Add(new TerrainUnloadData
                    {
                        ope = TerrainUnloadData.Operator.Unload,
                        startIndex = startIndex
                    });
                }

            }
            isRendering = false;
            if (leftDown != null)
            {
                leftDown->Dispose();
                leftUp->Dispose();
                rightDown->Dispose();
                rightUp->Dispose();
                UnsafeUtility.Free(leftDown, Allocator.Persistent);
                leftDown = null;
                leftUp = null;
                rightDown = null;
                rightUp = null;
            }
        }
        private void EnableRendering()
        {
            if (!isRendering)
            {
                MTerrain.current.textureCapacity--;
                int3 pack = int3(VirtualTextureIndex, VirtualTextureSize);
                if (!MTerrain.current.initializing)
                {
                    MTerrain.current.loadDataList.Add(new TerrainLoadData
                    {
                        ope = TerrainLoadData.Operator.Load,
                        startIndex = pack.xy,
                        size = pack.z,
                        handler0 = MTerrain.current.loader.LoadChunk(localPosition, lodLevel),
                        rootPos = rootPos,
                        maskScaleOffset = (float3)maskScaleOffset,
                        targetDecalLayer = decalMask
                    });
                }
            }

            isRendering = true;
        }

        public void InitializeRenderingCommand()
        {
            if (isRendering)
            {
                int3 pack = int3(VirtualTextureIndex, VirtualTextureSize);
                MTerrain.current.initializeLoadList.Add(new TerrainLoadData
                {
                    ope = TerrainLoadData.Operator.Load,
                    startIndex = pack.xy,
                    size = pack.z,
                    handler0 = MTerrain.current.loader.LoadChunk(localPosition, lodLevel),
                    rootPos = rootPos,
                    maskScaleOffset = (float3)maskScaleOffset,
                    targetDecalLayer = decalMask
                });
            }
            if (leftDown != null)
            {
                leftDown->InitializeRenderingCommand();
                leftUp->InitializeRenderingCommand();
                rightDown->InitializeRenderingCommand();
                rightUp->InitializeRenderingCommand();
            }
        }

        private void DisableRendering()
        {
            if (isRendering)
            {
                MTerrain.current.textureCapacity++;
                int2 startIndex = VirtualTextureIndex;
                MTerrain.current.unloadDataList.Add(new TerrainUnloadData
                {
                    ope = TerrainUnloadData.Operator.Unload,
                    startIndex = startIndex
                });
            }

            isRendering = false;
        }

        private void DisableRenderingWithoutUnload()
        {

            if (isRendering)
            {
                int2 startIndex = VirtualTextureIndex;
            }

            isRendering = false;
        }

        private void LogicSeparate()
        {
            if (leftDown == null)
            {
                leftDown = MUnsafeUtility.Malloc<TerrainQuadTree>(sizeof(TerrainQuadTree) * 4, Allocator.Persistent);
                leftUp = leftDown + 1;
                rightDown = leftDown + 2;
                rightUp = leftDown + 3;
                double subSize = worldSize * 0.5;
                *leftDown = new TerrainQuadTree(lodLevel, LocalPos.LeftDown, localPosition, subSize, maskScaleOffset, rootPos);
                *leftUp = new TerrainQuadTree(lodLevel, LocalPos.LeftUp, localPosition, subSize, maskScaleOffset, rootPos);
                *rightDown = new TerrainQuadTree(lodLevel, LocalPos.RightDown, localPosition, subSize, maskScaleOffset, rootPos);
                *rightUp = new TerrainQuadTree(lodLevel, LocalPos.RightUp, localPosition, subSize, maskScaleOffset, rootPos);
            }
        }

        private void Separate()
        {
            if (lodLevel >= MTerrain.current.allLodLevles.Length - 1)
            {
                EnableRendering();
            }
            else
            {
                if (MTerrain.current.textureCapacity >= 3)
                {
                    DisableRenderingWithoutUnload();
                    if (leftDown == null)
                    {
                        leftDown = MUnsafeUtility.Malloc<TerrainQuadTree>(sizeof(TerrainQuadTree) * 4, Allocator.Persistent);
                        leftUp = leftDown + 1;
                        rightDown = leftDown + 2;
                        rightUp = leftDown + 3;
                        double subSize = worldSize * 0.5;
                        *leftDown = new TerrainQuadTree(lodLevel, LocalPos.LeftDown, localPosition, subSize, this.maskScaleOffset, rootPos);
                        *leftUp = new TerrainQuadTree(lodLevel, LocalPos.LeftUp, localPosition, subSize, this.maskScaleOffset, rootPos);
                        *rightDown = new TerrainQuadTree(lodLevel, LocalPos.RightDown, localPosition, subSize, this.maskScaleOffset, rootPos);
                        *rightUp = new TerrainQuadTree(lodLevel, LocalPos.RightUp, localPosition, subSize, this.maskScaleOffset, rootPos);
                        float3 maskScaleOffset = (float3)this.maskScaleOffset;
                        maskScaleOffset.x *= 0.5f;
                        MTerrain.current.textureCapacity -= 3;

                        leftDown->isRendering = true;
                        leftUp->isRendering = true;
                        rightDown->isRendering = true;
                        rightUp->isRendering = true;
                        if (!MTerrain.current.initializing)
                        {
                            MTerrain.current.loadDataList.Add(new TerrainLoadData
                            {
                                ope = TerrainLoadData.Operator.Separate,
                                startIndex = VirtualTextureIndex,
                                size = VirtualTextureSize,
                                rootPos = rootPos,
                                maskScaleOffset = maskScaleOffset,
                                handler0 = MTerrain.current.loader.LoadChunk(leftDown->localPosition, leftDown->lodLevel),
                                handler1 = MTerrain.current.loader.LoadChunk(leftUp->localPosition, leftUp->lodLevel),
                                handler2 = MTerrain.current.loader.LoadChunk(rightDown->localPosition, rightDown->lodLevel),
                                handler3 = MTerrain.current.loader.LoadChunk(rightUp->localPosition, rightUp->lodLevel),
                                targetDecalLayer = leftDown->decalMask
                            });
                        }

                    }
                }

            }
            distOffset = -MTerrain.current.terrainData.lodDeferredOffset;
        }

        private void Combine(bool enableSelf)
        {
            if (leftDown != null)
            {
                leftDown->Dispose();
                leftUp->Dispose();
                rightDown->Dispose();
                rightUp->Dispose();
                if (enableSelf)
                {
                    int3 pack = int3(VirtualTextureIndex, VirtualTextureSize);
                    MTerrain.current.unloadDataList.Add(new TerrainUnloadData
                    {
                        ope = TerrainUnloadData.Operator.Combine,
                        startIndex = pack.xy,
                        size = pack.z
                    });
                    MTerrain.current.textureCapacity += 3;
                    isRendering = true;
                }
                else
                {
                    DisableRendering();
                }
                UnsafeUtility.Free(leftDown, Allocator.Persistent);
                leftDown = null;
                leftUp = null;
                rightDown = null;
                rightUp = null;
            }
            else
            {
                if (enableSelf)
                    EnableRendering();
                else
                    DisableRendering();
            }
            distOffset = MTerrain.current.terrainData.lodDeferredOffset;
        }
        public void PushDrawRequest(NativeList<MTerrain.TerrainChunkBuffer> loadedBufferList)
        {
            if (leftDown != null)
            {
                leftDown->PushDrawRequest(loadedBufferList);
                leftUp->PushDrawRequest(loadedBufferList);
                rightDown->PushDrawRequest(loadedBufferList);
                rightUp->PushDrawRequest(loadedBufferList);
            }

            if (isRendering)
            {
                MTerrain.current.enabledChunk[int3(VirtualTextureIndex, VirtualTextureSize)] = true;
                loadedBufferList.Add(new MTerrain.TerrainChunkBuffer
                {
                    scale = float2((float)(MTerrain.current.terrainData.largestChunkSize / pow(2, lodLevel)), (float)pow(2.0, MTerrain.current.allLodLevles.Length - 1 - lodLevel)),
                    worldPos = (float2)CornerWorldPos,
                    uvStartIndex = (uint2)VirtualTextureIndex
                });
            }
            else
                MTerrain.current.enabledChunk.Remove(int3(VirtualTextureIndex, VirtualTextureSize));
        }
        public void CheckUpdate(double3 camPos, double3 camDir)
        {
            double2 centerworldPosXZ = CenterWorldPos;
            double2 toPoint = camPos.xz - centerworldPosXZ;
            double3 toPoint3D = normalize(double3(centerworldPosXZ.x, camPos.y + MTerrain.current.terrainData.terrainLocalYPositionToGround, centerworldPosXZ.y) - camPos);
            double dotValue = dot(toPoint3D, camDir);
            double dist = MathLib.DistanceToQuad(worldSize, toPoint);
            double scale = dotValue > 0 ? 1 : MTerrain.current.terrainData.backfaceCullingScale;
            if (dist > MTerrain.current.allLodLevles[lodLevel] * scale - distOffset)
            {
                Combine(lodLevel > MTerrain.current.lodOffset);
            }
            else if (dist > MTerrain.current.allLodLevles[lodLevel + 1] * scale - distOffset)
            {
                Combine(lodLevel >= MTerrain.current.lodOffset);

            }
            else
            {
                Separate();

            }
            if (leftDown != null)
            {
                leftDown->CheckUpdate(camPos, camDir);
                leftUp->CheckUpdate(camPos, camDir);
                rightDown->CheckUpdate(camPos, camDir);
                rightUp->CheckUpdate(camPos, camDir);
            }
        }
    }
}