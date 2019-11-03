using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using System.IO;
using static Unity.Mathematics.math;
using Unity.Collections.LowLevel.Unsafe;
namespace MPipeline
{
    public unsafe struct MTerrainBoundingTree
    {
        public int treeLevel { get; private set; }
        private long* offset;
        private float2* boundingValue;
        private long byteSize;
        public bool isReading { get; private set; }
        private static byte[] byteCache = null;
        public MTerrainBoundingTree(int treeLevel)
        {
            isReading = false;
            this.treeLevel = treeLevel;
            offset = MUnsafeUtility.Malloc<long>(sizeof(long) * treeLevel, Unity.Collections.Allocator.Persistent);
            offset[0] = 0;
            int j = 1;
            for (int i = 1; i < treeLevel; ++i, j *= 2)
            {
                int ofst = (int)(0.1 + pow(2, i));
                offset[i] = offset[i - 1] + j * j;
            }
            byteSize = (offset[treeLevel - 1] + j * j) * sizeof(float2);
            boundingValue = MUnsafeUtility.Malloc<float2>(byteSize, Unity.Collections.Allocator.Persistent);

        }

        public void ReadFromDisk(FileStream fstrm, int chunkOffset)
        {
            isReading = true;
            if (byteCache == null || byteCache.Length < byteSize)
            {
                byteCache = new byte[byteSize];
            }

            fstrm.Position = chunkOffset * byteSize;
            fstrm.Read(byteCache, 0, (int)byteSize);

            UnsafeUtility.MemCpy(boundingValue, byteCache.Ptr(), byteSize);
            isReading = false;
        }

        public void WriteToDisk(FileStream fstrm, int chunkOffset)
        {
            if (byteCache == null || byteCache.Length < byteSize)
            {
                byteCache = new byte[byteSize];
            }
            UnsafeUtility.MemCpy(byteCache.Ptr(), boundingValue, byteSize);

            fstrm.Position = chunkOffset * byteSize;
            fstrm.Write(byteCache, 0, (int)byteSize);

        }

        public ref float2 this[int2 pos, int mip]
        {
            get
            {
                if (mip < 0 || mip >= treeLevel) throw new System.IndexOutOfRangeException("Out!");
                return ref boundingValue[offset[mip] + pos.x + pos.y * (int)(0.1 + pow(2, mip))];
            }
        }

        public ref float2 this[int pos, int mip]
        {
            get
            {
                if (mip < 0 || mip >= treeLevel) throw new System.IndexOutOfRangeException("Out!");
                return ref boundingValue[offset[mip] + pos];
            }
        }

        public void Dispose()
        {
            MUnsafeUtility.SafeFree(ref boundingValue, Unity.Collections.Allocator.Persistent);
            MUnsafeUtility.SafeFree(ref offset, Unity.Collections.Allocator.Persistent);
        }
    }
}