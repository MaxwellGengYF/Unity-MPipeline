using UnityEngine;
using System.Collections.Generic;
using Unity.Jobs;
using System;
using UnityEngine.Rendering;
using System.Reflection;
using Unity.Collections;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Unity.Collections.LowLevel.Unsafe;
using System.Threading;
using System.IO;
using UnityEngine.AddressableAssets;
namespace MPipeline
{
    [Serializable]
    public sealed unsafe class TexturePool
    {
        public int maximumPoolCapacity = 50;
        public RenderTexture rt { get; private set; }
        private int streamingIndex;
        private NativeArray<int> usageCount;
        private NativeList<int> indexPool;
        private Dictionary<AssetReference, int> guidToIndex;
        private ClusterMatResources clusterRes;
        private bool isNormal;
        public void Init(int streamingIndex, RenderTextureFormat format, int resolution, ClusterMatResources clusterRes, bool isNormal)
        {
            this.isNormal = isNormal;
            this.clusterRes = clusterRes;
            this.streamingIndex = streamingIndex;
            const int targetLevel = 6;
            rt = new RenderTexture(resolution, resolution, 0, format, targetLevel);
            rt.useMipMap = targetLevel > 1;
            rt.autoGenerateMips = false;
            rt.dimension = TextureDimension.Tex2DArray;
            rt.volumeDepth = maximumPoolCapacity;
            rt.enableRandomWrite = true;
            rt.filterMode = FilterMode.Trilinear;
            rt.wrapMode = TextureWrapMode.Repeat;
            rt.Create();
            indexPool = new NativeList<int>(maximumPoolCapacity, maximumPoolCapacity, Allocator.Persistent);
            for (int i = 0; i < maximumPoolCapacity; ++i)
            {
                indexPool[i] = i;
            }
            usageCount = new NativeArray<int>(maximumPoolCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            guidToIndex = new Dictionary<AssetReference, int>(maximumPoolCapacity);
        }

        public void Dispose()
        {
            UnityEngine.Object.DestroyImmediate(rt);
            usageCount.Dispose();
            indexPool.Dispose();
            guidToIndex = null;
        }



        public int GetTex(AssetReference guid)
        {
            
            int index;
            if (guidToIndex.TryGetValue(guid, out index))
            {
                usageCount[index]++;
            }
            else
            {
                if (indexPool.Length <= 0)
                {
                    throw new Exception("Texture Pool out of Range!!");
                }
                index = indexPool[indexPool.Length - 1];
                indexPool.RemoveLast();
                usageCount[index] = 1;
                guidToIndex.Add(guid, index);
                clusterRes.AddLoadCommand(guid, rt, index, isNormal);
                //TODO
                //Streaming Load Texture
            }
            
            return index;
        }

        public void RemoveTex(AssetReference guid)
        {
            int index;
            if (guidToIndex.TryGetValue(guid, out index))
            {
                usageCount[index]--;
                if (usageCount[index] <= 0)
                {
                    indexPool.Add(index);
                    guidToIndex.Remove(guid);
                }
            }
        }
    }
}
