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
        private bool isCreate;
        private const int GUID_LENGTH = 2;
        public const int CHUNK_DATASIZE = FileGUID.PTR_LENGTH * 8 * GUID_LENGTH;
        public void Init(byte* arr)
        {
            isCreate = true;
            FileGUID* guidPtr = mask.Ptr();
            for (int i = 0; i < GUID_LENGTH; ++i)
            {
                guidPtr[i] = new FileGUID(arr, Allocator.Persistent);
                arr += FileGUID.PTR_LENGTH * sizeof(ulong);
            }
        }

        public void Dispose()
        {
            if (!isCreate) return;
            isCreate = false;
            FileGUID* guidPtr = mask.Ptr();
            for (int i = 0; i < GUID_LENGTH; ++i)
            {
                guidPtr[i].Dispose();
            }
        }

        public void OutputBytes(byte* arr)
        {
            FileGUID* guidPtr = mask.Ptr();
            for (int i = 0; i < GUID_LENGTH; ++i)
            {
                arr += guidPtr[i].ToBytes(arr);
            }
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
            streamer = new FileStream(path, FileMode.Open, FileAccess.Read);
            this.mipLevel = mipLevel;
            streamPositionOffset = new NativeArray<long>(mipLevel, Allocator.Persistent);
            long offset = 0;
            for (int i = 0; i < mipLevel; ++i)
            {
                streamPositionOffset[i] = offset;
                long curOffset = (long)(0.1 + pow(2, i));
                offset += curOffset * curOffset;
            }
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