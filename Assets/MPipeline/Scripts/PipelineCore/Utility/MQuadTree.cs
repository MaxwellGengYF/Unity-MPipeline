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
    public unsafe struct TerrainQuadTreeSettings
    {
        public double largestChunkSize;
        public double2 screenOffset;
        public NativeList_Float allLodLevles;
        public float lodDeferredOffset;
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
        private TerrainQuadTreeSettings* setting;
        private bool isCreated;
        public int lodLevel;
        public int2 localPosition;
        public int2 rootPosition;
        private float distOffset;
        public int2 VirtualTextureIndex
        {
            get
            {
                return localPosition * (int)(0.5 + pow(2.0, setting->allLodLevles.Length - 1 - lodLevel)) + rootPosition * (int)(0.5 + pow(2.0, setting->allLodLevles.Length - 1));
            }
        }
        public bool isRendering { get; private set; }
        public TerrainQuadTree(int parentLodLevel, TerrainQuadTreeSettings* setting, LocalPos sonPos, int2 parentPos, int2 rootPosition)
        {
            distOffset = setting->lodDeferredOffset;
            this.setting = setting;
            this.rootPosition = rootPosition;
            isCreated = true;
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
        }
        public float2 CornerWorldPos
        {
            get
            {
                double2 chunkPos = rootPosition + setting->screenOffset;
                chunkPos *= setting->largestChunkSize;
                chunkPos += (setting->largestChunkSize / pow(2, lodLevel)) * (double2)localPosition;
                return (float2)chunkPos;
            }
        }
        public float2 CenterWorldPos
        {
            get
            {
                double2 chunkPos = rootPosition + setting->screenOffset;
                chunkPos *= setting->largestChunkSize;
                chunkPos += (setting->largestChunkSize / pow(2, lodLevel)) * ((double2)(localPosition) + 0.5);
                return (float2)chunkPos;
            }
        }
        public void Dispose()
        {
            DisableSelfRendering();
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
            isCreated = false;
        }
        private void DisableSelfRendering()
        {
            isRendering = false;
            //TODO
            //Release self's virtual texture
        }
        private void EnableSelfRendering()
        {
            isRendering = true;

            //TODO
            //Load virtual texture
        }
        private void Separate()
        {
            if (lodLevel >= setting->allLodLevles.Length - 1)
            {
                EnableSelfRendering();
            }
            else
            {
                DisableSelfRendering();
                if (leftDown == null)
                {
                    leftDown = MUnsafeUtility.Malloc<TerrainQuadTree>(sizeof(TerrainQuadTree) * 4, Allocator.Persistent);
                    leftUp = leftDown + 1;
                    rightDown = leftDown + 2;
                    rightUp = leftDown + 3;
                    *leftDown = new TerrainQuadTree(lodLevel, setting, LocalPos.LeftDown, localPosition, rootPosition);
                    *leftUp = new TerrainQuadTree(lodLevel, setting, LocalPos.LeftUp, localPosition, rootPosition);
                    *rightDown = new TerrainQuadTree(lodLevel, setting, LocalPos.RightDown, localPosition, rootPosition);
                    *rightUp = new TerrainQuadTree(lodLevel, setting, LocalPos.RightUp, localPosition, rootPosition);
                }
                leftDown->EnableSelfRendering();
                leftUp->EnableSelfRendering();
                rightDown->EnableSelfRendering();
                rightUp->EnableSelfRendering();
            }
            distOffset = -setting->lodDeferredOffset;
        }
        private void Combine(bool enableSelf)
        {
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
                isRendering = true;
            }
            distOffset = setting->lodDeferredOffset;
            if (enableSelf) EnableSelfRendering();
            else DisableSelfRendering();
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
                    scale = float2((float)(setting->largestChunkSize / pow(2, lodLevel)), pow(2, setting->allLodLevles.Length - 1 - lodLevel)),
                    worldPos = CornerWorldPos,
                    uvStartIndex = (uint2)VirtualTextureIndex
                });
            }
        }
        public void CheckUpdate(double2 camXZPos)
        {
            if (!isCreated) return;
            double2 worldPos = CenterWorldPos;
            double dist = distance(worldPos, camXZPos);
            if (dist > setting->allLodLevles[lodLevel] - distOffset)
            {
                Combine(lodLevel != 0);
            }
            else if (dist > setting->allLodLevles[lodLevel + 1] - distOffset)
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