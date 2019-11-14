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
    public struct TerrainDrawCommand
    {
        public float2 startPos;
        public int2 startVTIndex;
        public int2 rootPos;
    }

    public struct TerrainLoadData
    {
        public enum Operator
        {
            Load, Separate, Update, Unload, Combine
        }
        public Operator ope;
        public int size;
        public LayerMask targetDecalLayer;
        public int2 startIndex;
        public int2 rootPos;
        public float3 maskScaleOffset;
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
        private int2 renderingLocalPosition;
        private double distOffset;
        public int2 VirtualTextureIndex => localPosition * (int)(0.1 + pow(2.0, MTerrain.current.allLodLevles.Length - 1 - lodLevel));
        private bool m_isRendering;
        public bool isRendering
        {
            get
            {
                return m_isRendering;
            }

            set
            {
                if (value == m_isRendering) return;
                m_isRendering = value;
                if (value)
                    MTerrain.current.textureCapacity--;
                else
                    MTerrain.current.textureCapacity++;
            }
        }
        public double worldSize { get; private set; }
        public int2 rootPos;
        public double3 maskScaleOffset;
        private LayerMask decalMask;
        private bool initializing;
        public TerrainQuadTree(int parentLodLevel, LocalPos sonPos, int2 parentPos, int2 parentRenderingPos, double worldSize, double3 maskScaleOffset, int2 rootPos)
        {
            toPoint = 0;
            initializing = true;
            separate = false;
            dist = 0;
            isInRange = false;
            this.worldSize = worldSize;
            distOffset = MTerrain.current.terrainData.lodDeferredOffset;
            m_isRendering = false;
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
            if (lodLevel >= MTerrain.current.lodOffset)
            {
                
                //Larger
                if (lodLevel > MTerrain.current.lodOffset)
                {
                    double subScale = maskScaleOffset.x * 0.5;
                    renderingLocalPosition = parentRenderingPos * 2;
                    double2 offset = maskScaleOffset.yz;
                    switch (sonPos)
                    {
                        case LocalPos.LeftUp:
                            offset += double2(0, subScale);
                            renderingLocalPosition += int2(0, 1);
                            break;
                        case LocalPos.RightDown:
                            offset += double2(subScale, 0);
                            renderingLocalPosition += int2(1, 0);
                            break;
                        case LocalPos.RightUp:
                            offset += subScale;
                            renderingLocalPosition += 1;
                            break;
                    }
                    this.maskScaleOffset = double3(subScale, offset);
                    this.rootPos = rootPos;
                }
                //Equal
                else
                {
                    this.rootPos = localPosition;
                    this.maskScaleOffset = maskScaleOffset;
                    renderingLocalPosition = 0;
                    var loadCommand = new MTerrain.MaskLoadCommand
                    {
                        load = true,
                        pos = this.rootPos
                    };
                    MTerrain.current.maskLoadList.Add(loadCommand);
                    lock (MTerrain.current)
                    {
                        MTerrain.current.boundBoxLoadList.Add(loadCommand);
                    }
                }
            }
            else
            {
                this.rootPos = localPosition;
                renderingLocalPosition = parentRenderingPos;
                this.maskScaleOffset = maskScaleOffset;
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
                if (boundedPos.x - circleRange.x < circleRange.z &&
                    boundedPos.y - circleRange.y < circleRange.z &&
                    circleRange.x - boundedPos.z < circleRange.z &&
                    circleRange.y - boundedPos.w < circleRange.z)
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
                var loadCommand = new MTerrain.MaskLoadCommand
                {
                    load = false,
                    pos = rootPos
                };
                MTerrain.current.maskLoadList.Add(loadCommand);
                lock (MTerrain.current)
                {
                    MTerrain.current.boundBoxLoadList.Add(loadCommand);
                }
            }
            if (MTerrain.current != null)
            {
                if (isRendering)
                {
                    int2 startIndex = VirtualTextureIndex;
                    MTerrain.current.loadDataList.Add(new TerrainLoadData
                    {
                        ope = TerrainLoadData.Operator.Unload,
                        startIndex = startIndex
                    });
                }
                isRendering = false;
            }
            else
            {
                m_isRendering = false;
            }
            
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
            if (MTerrain.current.textureCapacity < 1) return;
            if (!isRendering)
            {
                int3 pack = int3(VirtualTextureIndex, VirtualTextureSize);
                if (!MTerrain.current.initializing)
                {
                    MTerrain.current.loadDataList.Add(new TerrainLoadData
                    {
                        ope = TerrainLoadData.Operator.Load,
                        startIndex = pack.xy,
                        size = pack.z,
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
                int2 startIndex = VirtualTextureIndex;
                MTerrain.current.loadDataList.Add(new TerrainLoadData
                {
                    ope = TerrainLoadData.Operator.Unload,
                    startIndex = startIndex
                });
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
                *leftDown = new TerrainQuadTree(lodLevel, LocalPos.LeftDown, localPosition, renderingLocalPosition, subSize, maskScaleOffset, rootPos);
                *leftUp = new TerrainQuadTree(lodLevel, LocalPos.LeftUp, localPosition, renderingLocalPosition, subSize, maskScaleOffset, rootPos);
                *rightDown = new TerrainQuadTree(lodLevel, LocalPos.RightDown, localPosition, renderingLocalPosition, subSize, maskScaleOffset, rootPos);
                *rightUp = new TerrainQuadTree(lodLevel, LocalPos.RightUp, localPosition, renderingLocalPosition, subSize, maskScaleOffset, rootPos);
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
                if (leftDown == null && MTerrain.current.textureCapacity >= (isRendering ? 3 : 4))
                {
                    isRendering = false;
                    leftDown = MUnsafeUtility.Malloc<TerrainQuadTree>(sizeof(TerrainQuadTree) * 4, Allocator.Persistent);
                    leftUp = leftDown + 1;
                    rightDown = leftDown + 2;
                    rightUp = leftDown + 3;
                    double subSize = worldSize * 0.5;
                    *leftDown = new TerrainQuadTree(lodLevel, LocalPos.LeftDown, localPosition, renderingLocalPosition, subSize, this.maskScaleOffset, rootPos);
                    *leftUp = new TerrainQuadTree(lodLevel, LocalPos.LeftUp, localPosition, renderingLocalPosition, subSize, this.maskScaleOffset, rootPos);
                    *rightDown = new TerrainQuadTree(lodLevel, LocalPos.RightDown, localPosition, renderingLocalPosition, subSize, this.maskScaleOffset, rootPos);
                    *rightUp = new TerrainQuadTree(lodLevel, LocalPos.RightUp, localPosition, renderingLocalPosition, subSize, this.maskScaleOffset, rootPos);
                    float3 maskScaleOffset = (float3)this.maskScaleOffset;
                    maskScaleOffset.x *= 0.5f;
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
                            targetDecalLayer = leftDown->decalMask
                        });
                    }
                }
            }
            distOffset = -MTerrain.current.terrainData.lodDeferredOffset;
        }
        private void Combine(bool enableSelf)
        {
            distOffset = MTerrain.current.terrainData.lodDeferredOffset;
            if (leftDown != null)
            {
                if (!leftDown->isRendering || !leftUp->isRendering || !rightDown->isRendering || !rightUp->isRendering)
                {
                    return;
                }
                leftDown->isRendering = false;
                leftUp->isRendering = false;
                rightDown->isRendering = false;
                rightUp->isRendering = false;
                leftDown->Dispose();
                leftUp->Dispose();
                rightDown->Dispose();
                rightUp->Dispose();
                if (enableSelf)
                {
                    int3 pack = int3(VirtualTextureIndex, VirtualTextureSize);
                    MTerrain.current.loadDataList.Add(new TerrainLoadData
                    {
                        ope = TerrainLoadData.Operator.Combine,
                        startIndex = pack.xy,
                        size = pack.z
                    });
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
        }
        public void PushDrawRequest(NativeList<TerrainDrawCommand> loadedBufferList)
        {
            if (lodLevel == MTerrain.current.lodOffset)
            {
                if (isRendering || leftDown != null)
                    loadedBufferList.Add(new TerrainDrawCommand
                    {
                        startPos = (float2)CornerWorldPos,
                        startVTIndex = VirtualTextureIndex,
                        rootPos = rootPos + (int2)maskScaleOffset.yz
                    });
            }
            else if (lodLevel < MTerrain.current.lodOffset)
            {
                if (leftDown != null)
                {
                    leftDown->PushDrawRequest(loadedBufferList);
                    leftUp->PushDrawRequest(loadedBufferList);
                    rightDown->PushDrawRequest(loadedBufferList);
                    rightUp->PushDrawRequest(loadedBufferList);
                }
            }
        }
        double3 toPoint;
        // double3 toPoint3D;
        double dist;
        bool separate;
        bool isInRange;
        public void UpdateData(double3 camPos, double3 camDir, double2 heightScaleOffset, double3 camFrustumMin, double3 camFrustumMax, float4* planes)
        {
            double2 centerworldPosXZ = CornerWorldPos;
            double extent = worldSize * 0.5;
            double4 xzBounding = double4(centerworldPosXZ, centerworldPosXZ + extent * 2);
            centerworldPosXZ += extent;
            double2 texMinMax = double2(0, 1);
            lock (MTerrain.current)
            {
                MTerrainBoundingTree boundTree;
                int2 currentRootPos = rootPos + (int2)maskScaleOffset.yz;
                int targetLevel = lodLevel - MTerrain.current.lodOffset;
                if (targetLevel >= 0 && MTerrain.current.boundingDict.Get(currentRootPos, out boundTree) && boundTree.isCreate)
                {
                    texMinMax = boundTree[renderingLocalPosition, targetLevel];

                }

            }
            double2 heightMinMax = heightScaleOffset.y + texMinMax * heightScaleOffset.x;
            double2 heightCenterExtent = double2(heightMinMax.x + heightMinMax.y, heightMinMax.y - heightMinMax.x) * 0.5;
            double3 centerWorldPos = double3(centerworldPosXZ.x, heightCenterExtent.x, centerworldPosXZ.y);
            double3 centerExtent = double3(extent, heightCenterExtent.y, extent);
            
            isInRange = MathLib.BoxContactWithBox(camFrustumMin, camFrustumMax, double3(xzBounding.x, heightMinMax.x, xzBounding.y), double3(xzBounding.z, heightMinMax.y, xzBounding.w));
            if (isInRange)
            {
                
                isInRange = MathLib.BoxIntersect(centerWorldPos, centerExtent, planes, 6);
            }
            toPoint = camPos - centerWorldPos;
            dist = MathLib.DistanceToCube(centerExtent, toPoint);
            if (leftDown != null)
            {
                leftDown->UpdateData(camPos, camDir, heightScaleOffset, camFrustumMin, camFrustumMax, planes);
                leftUp->UpdateData(camPos, camDir, heightScaleOffset, camFrustumMin, camFrustumMax, planes);
                rightDown->UpdateData(camPos, camDir, heightScaleOffset, camFrustumMin, camFrustumMax, planes);
                rightUp->UpdateData(camPos, camDir, heightScaleOffset, camFrustumMin, camFrustumMax, planes);
            }
        }

        public void CombineUpdate()
        {
            if (leftDown != null)
            {
                leftDown->CombineUpdate();
                leftUp->CombineUpdate();
                rightDown->CombineUpdate();
                rightUp->CombineUpdate();
            }
            double backface = isInRange ? 1 : MTerrain.current.terrainData.backfaceCullingLevel;
            if (dist > MTerrain.current.allLodLevles[lodLevel] * backface - distOffset)
            {
                separate = false;
                Combine(lodLevel > MTerrain.current.lodOffset);
            }
            else if (dist > MTerrain.current.allLodLevles[lodLevel + 1] * backface - distOffset)
            {
                separate = false;
                Combine(lodLevel >= MTerrain.current.lodOffset);
            }
            else
                separate = true;

        }

        public void SeparateUpdate()
        {
            if (!MTerrain.current.initializing && initializing)
            {
                initializing = false;
                return;
            }
            else
                initializing = false;
            if (separate)
            {
                Separate();
                if (leftDown != null)
                {
                    leftDown->SeparateUpdate();
                    leftUp->SeparateUpdate();
                    rightDown->SeparateUpdate();
                    rightUp->SeparateUpdate();
                }

            }
        }
    }
}