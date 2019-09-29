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
    public unsafe struct AddressableReferenceHolder
    {
        //TODO
        //Auto Disposer
        public class TextureReference
        {
            public AssetReference reference;
            public Texture loadedTexture;
            private IEnumerator loader;
            public bool isLoading { get; private set; }
            public bool Load(out IEnumerator result)
            {
                result = null;
                if (loadedTexture) return false;
                if (isLoading)
                {
                    result = loader;
                    return true;
                }
                isLoading = true;
                result = LoadFunc();
                loader = result;
                return true;
            }
            private IEnumerator LoadFunc()
            {
                var asyncLoad = reference.LoadAssetAsync<Texture>();
                yield return asyncLoad;
                isLoading = false;
                loadedTexture = asyncLoad.Result;
            }

            public void Dispose()
            {
                loadedTexture = null;
                reference.ReleaseAsset();
            }
        }
        private Dictionary<string, TextureReference> allReferences;
        private MStringBuilder msb;
        public AddressableReferenceHolder(int capacity)
        {
            allReferences = new Dictionary<string, TextureReference>(capacity);
            msb = new MStringBuilder(32);
        }

        public TextureReference GetReference(FileGUID guid)
        {
            guid.GetString(msb);
            TextureReference aref;
            if (allReferences.TryGetValue(msb.str, out aref))
            {
                return aref;
            }
            else
            {
                aref = new TextureReference();
                string guidStr = guid.ToString();
                aref.reference = new AssetReference(guidStr);
                aref.loadedTexture = null;
                allReferences.Add(guidStr, aref);
                return aref;
            }
        }
        public void Dispose()
        {
            foreach (var i in allReferences)
            {
                i.Value.Dispose();
            }
            allReferences = null;
        }
    }
    public unsafe struct VirtualTextureChunk
    {
        public FileGUID albedo0;
        public FileGUID albedo1;
        public FileGUID albedo2;
        public FileGUID albedo3;
        public FileGUID normal0;
        public FileGUID normal1;
        public FileGUID normal2;
        public FileGUID normal3;
        public FileGUID smo0;
        public FileGUID smo1;
        public FileGUID smo2;
        public FileGUID smo3;
        public FileGUID mask;
        public float4 scaleOffset0;
        public float4 scaleOffset1;
        public float4 scaleOffset2;
        public float4 scaleOffset3;
        private bool isCreate;
        public const int GUID_LENGTH = 4 * 3 + 1;
        public const int SCALEOFFSET_COUNT = 4 * 4;
        public const int CHUNK_DATASIZE = FileGUID.PTR_LENGTH * 8 * GUID_LENGTH + 4 * SCALEOFFSET_COUNT;
        public void Init(byte* arr)
        {
            isCreate = true;
            FileGUID* guidPtr = albedo0.Ptr();
            for (int i = 0; i < GUID_LENGTH; ++i)
            {
                guidPtr[i] = new FileGUID(arr, Allocator.Persistent);
                arr += FileGUID.PTR_LENGTH * sizeof(ulong);
            }
            float4* floatPtr = scaleOffset0.Ptr();
            UnsafeUtility.MemCpy(floatPtr, arr, sizeof(float) * SCALEOFFSET_COUNT);
        }

        public void Dispose()
        {
            if (!isCreate) return;
            isCreate = false;
            FileGUID* guidPtr = albedo0.Ptr();
            for (int i = 0; i < GUID_LENGTH; ++i)
            {
                guidPtr[i].Dispose();
            }
        }

        public void OutputBytes(byte* arr)
        {
            FileGUID* guidPtr = albedo0.Ptr();
            for (int i = 0; i < GUID_LENGTH; ++i)
            {
                arr += guidPtr[i].ToBytes(arr);
            }
            float4* floatPtr = scaleOffset0.Ptr();
            UnsafeUtility.MemCpy(arr, floatPtr, sizeof(float) * SCALEOFFSET_COUNT);
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