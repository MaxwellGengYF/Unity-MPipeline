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
    public struct TerrainLoadData
    {
        public enum Operator
        {
            Combine, Load, Unload, Separate
        }
        public Operator ope;
        public int2 startIndex;
        public int size;
        public VirtualTextureChunk targetLoadChunk;
        public VirtualTextureChunk nextChunk0;
        public VirtualTextureChunk nextChunk1;
        public VirtualTextureChunk nextChunk2;
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
        private VirtualTextureChunk textureChunk;
        private float2 minMaxBounding;
        public int2 VirtualTextureIndex => localPosition * (int)(0.5 + pow(2.0, MTerrain.current.allLodLevles.Length - 1 - lodLevel));
        public bool isRendering { get; private set; }
        public TerrainQuadTree(int parentLodLevel, LocalPos sonPos, int2 parentPos)
        {
            distOffset = MTerrain.current.terrainData.lodDeferredOffset;
            textureChunk = new VirtualTextureChunk();
            minMaxBounding = 0;
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
            MTerrain.current.loader.ReadChunkData(ref textureChunk, localPosition, lodLevel);
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
        public void Dispose()
        {
            if (isRendering && MTerrain.current != null)
            {
                MTerrain.current.loadDataList.Add(new TerrainLoadData
                {
                    ope = TerrainLoadData.Operator.Unload,
                    startIndex = VirtualTextureIndex
                });
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
            textureChunk.Dispose();
        }
        private void EnableRendering()
        {
            if (!isRendering)
            {
                MTerrain.current.loadDataList.Add(new TerrainLoadData
                {
                    ope = TerrainLoadData.Operator.Load,
                    size = VirtualTextureSize,
                    startIndex = VirtualTextureIndex,
                    targetLoadChunk = textureChunk.CopyTo()
                });
            }

            isRendering = true;
        }

        private void DisableRendering()
        {

            if (isRendering)
            {
                MTerrain.current.loadDataList.Add(new TerrainLoadData
                {
                    ope = TerrainLoadData.Operator.Unload,
                    size = VirtualTextureSize,
                    startIndex = VirtualTextureIndex
                });
            }

            isRendering = false;
        }

        private void Separate()
        {
            if (lodLevel >= MTerrain.current.allLodLevles.Length - 1)
            {
                EnableRendering();
            }
            else
            {
                DisableRendering();
                if (leftDown == null)
                {
                    leftDown = MUnsafeUtility.Malloc<TerrainQuadTree>(sizeof(TerrainQuadTree) * 4, Allocator.Persistent);
                    leftUp = leftDown + 1;
                    rightDown = leftDown + 2;
                    rightUp = leftDown + 3;
                    *leftDown = new TerrainQuadTree(lodLevel, LocalPos.LeftDown, localPosition);
                    *leftUp = new TerrainQuadTree(lodLevel, LocalPos.LeftUp, localPosition);
                    *rightDown = new TerrainQuadTree(lodLevel, LocalPos.RightDown, localPosition);
                    *rightUp = new TerrainQuadTree(lodLevel, LocalPos.RightUp, localPosition);
                    MTerrain.current.loadDataList.Add(new TerrainLoadData
                    {
                        targetLoadChunk = leftDown->textureChunk.CopyTo(),
                        nextChunk0 = leftUp->textureChunk.CopyTo(),
                        nextChunk1 = rightDown->textureChunk.CopyTo(),
                        nextChunk2 = rightUp->textureChunk.CopyTo(),
                        ope = TerrainLoadData.Operator.Separate,
                        size = VirtualTextureSize,
                        startIndex = VirtualTextureIndex
                    });

                    leftDown->isRendering = true;
                    leftUp->isRendering = true;
                    rightDown->isRendering = true;
                    rightUp->isRendering = true;
                }

            }
            distOffset = -MTerrain.current.terrainData.lodDeferredOffset;
        }

        private void Combine(bool enableSelf)
        {
            if (leftDown != null)
            {
                float min0 = min(leftDown->minMaxBounding.x, leftUp->minMaxBounding.x);
                float min1 = min(rightDown->minMaxBounding.x, rightUp->minMaxBounding.x);
                float max0 = max(leftDown->minMaxBounding.y, leftUp->minMaxBounding.y);
                float max1 = max(rightDown->minMaxBounding.y, rightUp->minMaxBounding.y);
                minMaxBounding.x = min(min0, min1);
                minMaxBounding.y = max(max0, max1);
                leftDown->Dispose();
                leftUp->Dispose();
                rightDown->Dispose();
                rightUp->Dispose();
                if (enableSelf)
                {
                    MTerrain.current.loadDataList.Add(new TerrainLoadData
                    {
                        ope = TerrainLoadData.Operator.Combine,
                        startIndex = VirtualTextureIndex,
                        size = VirtualTextureSize 
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
                loadedBufferList.Add(new MTerrain.TerrainChunkBuffer
                {
                    minMaxHeight = 0,
                    scale = float2((float)(MTerrain.current.terrainData.largestChunkSize / pow(2, lodLevel)), (float)pow(2.0, MTerrain.current.allLodLevles.Length - 1 - lodLevel)),
                    worldPos = (float2)CornerWorldPos,
                    uvStartIndex = (uint2)VirtualTextureIndex
                });
            }
        }
        public void CheckUpdate(double2 camXZPos)
        {
            double2 worldPos = CenterWorldPos;
            double dist = distance(worldPos, camXZPos);
            if (dist > MTerrain.current.allLodLevles[lodLevel] - distOffset)
            {
                Combine(lodLevel != 0);
            }
            else if (dist > MTerrain.current.allLodLevles[lodLevel + 1] - distOffset)
            {
                Combine(true);

            }
            else
            {
                Separate();

            }

            if (leftDown != null)
            {
                leftDown->CheckUpdate(camXZPos);
                leftUp->CheckUpdate(camXZPos);
                rightDown->CheckUpdate(camXZPos);
                rightUp->CheckUpdate(camXZPos);
            }
        }
    }
}