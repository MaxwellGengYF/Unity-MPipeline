using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.AddressableAssets;
using System.IO;
namespace MPipeline
{
    public unsafe struct VirtualTextureChunk
    {
        public FileGUID mask;
        public FileGUID height;
        public int* refCount;
        public float2 minMaxHeightBounding;
        private const int GUID_LENGTH = 2;
        public const int CHUNK_DATASIZE = FileGUID.PTR_LENGTH * 8 * GUID_LENGTH + 8;
        public void Init(byte* arr)
        {
            refCount = MUnsafeUtility.Malloc<int>(sizeof(int), Allocator.Persistent);
            *refCount = 1;
            FileGUID* guidPtr = mask.Ptr();
            for (int i = 0; i < GUID_LENGTH; ++i)
            {
                guidPtr[i] = new FileGUID(arr, Allocator.Persistent);
                arr += FileGUID.PTR_LENGTH * sizeof(ulong);
            }
            UnsafeUtility.MemCpy(minMaxHeightBounding.Ptr(), arr, 8);
        }

        public void Dispose()
        {
            if (refCount != null && *refCount > 0)
            {
                (*refCount)--;
                if (*refCount <= 0)
                {
                    FileGUID* guidPtr = mask.Ptr();
                    for (int i = 0; i < GUID_LENGTH; ++i)
                    {
                        guidPtr[i].Dispose();
                    }
                    UnsafeUtility.Free(refCount, Allocator.Persistent);
                    refCount = null;
                }
            }
        }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public VirtualTextureChunk CopyTo()
        {
            VirtualTextureChunk other = new VirtualTextureChunk
            {
                height = height,
                mask = mask,
                minMaxHeightBounding = minMaxHeightBounding,
                refCount = refCount
            };
            (*refCount)++;
            return other;
        }

        public static void OutputDefaultValue(byte* arr)
        {
            const int guidOffset = FileGUID.PTR_LENGTH * 8 * GUID_LENGTH;
            UnsafeUtility.MemClear(arr, guidOffset);
            float2* floatPtr =(float2*)(arr + guidOffset);
            *floatPtr = 0;
        }

        public void OutputBytes(byte* arr)
        {
            FileGUID* guidPtr = mask.Ptr();
            for (int i = 0; i < GUID_LENGTH; ++i)
            {
                arr += guidPtr[i].ToBytes(arr);
            }
            UnsafeUtility.MemCpy(arr, minMaxHeightBounding.Ptr(), 8);
        }
    }
    public unsafe struct VirtualTextureLoader
    {
        public int mipLevel { get; private set; }
        private NativeArray<long> streamPositionOffset;
        private FileStream streamer;
        private byte[] oneChunkSize;
        public VirtualTextureLoader(int mipLevel, string path)
        {
            oneChunkSize = new byte[VirtualTextureChunk.CHUNK_DATASIZE];
            this.mipLevel = mipLevel;
            streamPositionOffset = new NativeArray<long>(mipLevel, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            long offset = 0;
            for (int i = 0; i < mipLevel; ++i)
            {
                streamPositionOffset[i] = offset;
                long curOffset = (long)(0.1 + pow(2, i));
                offset += curOffset * curOffset;
            }
#if UNITY_EDITOR
            if (!File.Exists(path))
            {
                long curOffset = (long)(0.1 + pow(2, mipLevel - 1));
                curOffset *= curOffset;
                long count = (streamPositionOffset[mipLevel - 1] + curOffset);
                byte[] arr = new byte[count * VirtualTextureChunk.CHUNK_DATASIZE];
                byte* arrPtr = arr.Ptr();
                for(long i = 0; i < count; ++i)
                {
                    VirtualTextureChunk.OutputDefaultValue(arrPtr);
                    arrPtr += VirtualTextureChunk.CHUNK_DATASIZE;
                }
                File.WriteAllBytes(path, arr);
            }
#endif
            streamer = new FileStream(path, FileMode.Open, FileAccess.Read);
        }

        public void ReadChunkData(ref VirtualTextureChunk result, int2 position, int targetMipLevel)
        {
            streamer.Position = ((long)(0.1 + pow(2, targetMipLevel)) * position.y + position.x + streamPositionOffset[targetMipLevel]) * VirtualTextureChunk.CHUNK_DATASIZE;
            streamer.Read(oneChunkSize, 0, oneChunkSize.Length);
            result.Init(oneChunkSize.Ptr());
        }

        public void Dispose()
        {
            streamer.Dispose();
            streamPositionOffset.Dispose();
            oneChunkSize = null;
        }

        public static void SaveBytesArray(Native2DArray<VirtualTextureChunk>[] allMipLevel, string targetPath)
        {
            int count = 0;
            foreach (var i in allMipLevel)
            {
                int2 len = i.Length;
                count += len.x * len.y;
            }
            byte[] result = new byte[count * VirtualTextureChunk.CHUNK_DATASIZE];
            count = 0;
            byte* resultPtr = result.Ptr();
            foreach (var i in allMipLevel)
            {
                int2 len = i.Length;
                for (int y = 0; y < len.y; ++y)
                    for (int x = 0; x < len.x; ++x)
                    {
                        i[new int2(x, y)].OutputBytes(resultPtr);
                        resultPtr += VirtualTextureChunk.CHUNK_DATASIZE;
                    }
            }
            File.WriteAllBytes(targetPath, result);
        }
    }
}