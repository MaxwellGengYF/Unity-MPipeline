using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Unity.Mathematics.math;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine.Rendering;
using UnityEngine.AddressableAssets;
using UnityEngine.Experimental.Rendering;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.ResourceManagement.AsyncOperations;
namespace MPipeline
{
    [System.Serializable]
    public struct TexturePaths
    {
        public string texName;
        public string[] instancingIDs;
    }
    [System.Serializable]
    public struct LightmapPaths
    {
        public string name;
        public int size;
    }

    public sealed unsafe class ClusterMatResources : ScriptableObject
    {
        public static ClusterMatResources current { get; private set; }
        #region SERIALIZABLE
        public enum TextureSize
        {
            x512 = 512,
            x1024 = 1024,
            x2048 = 2048,
            x4096 = 4096,
            x8192 = 8192
        };
        public TextureSize fixedTextureSize = TextureSize.x2048;
        public int maximumClusterCount = 100000;
        public int maximumMaterialCount = 1;
        public int materialPoolSize = 500;
        public TexturePool rgbaPool;
        public TexturePool emissionPool;
        public TexturePool heightPool;

        public const string infosPath = "Assets/BinaryData/MapDatas/";
        #endregion

        #region NON_SERIALIZABLE
        private struct AsyncTextureLoader
        {
            public AssetReference aref;
            public AsyncOperationHandle<Texture> loader;
            public RenderTexture targetTexArray;
            public int targetMipLevel;
            public int targetIndex;
            public bool startLoading;
            public bool isNormal;
        }
        public VirtualMaterialManager vmManager;
        private List<AsyncTextureLoader> asyncLoader = new List<AsyncTextureLoader>(100);
        private List<AssetReference> allReferenceCache = new List<AssetReference>(200);
        private struct Int4x4Equal : IFunction<int4x4, int4x4, bool>
        {
            public bool Run(ref int4x4 a, ref int4x4 b)
            {
                ulong* aPtr = (ulong*)a.Ptr();
                ulong* bPtr = (ulong*)b.Ptr();
                for (int i = 0; i < 8; ++i)
                {
                    if (aPtr[i] != bPtr[i]) return false;
                }
                return true;
            }
        }
        private NativeDictionary<int4x4, int, Int4x4Equal> referenceCacheDict;
        private NativeArray<int> mipIDs;
        public void AddLoadCommand(AssetReference aref, RenderTexture targetTexArray, int targetIndex, bool isNormal)
        {
            asyncLoader.Add(new AsyncTextureLoader
            {
                aref = aref,
                targetTexArray = targetTexArray,
                targetIndex = targetIndex,
                isNormal = isNormal,
            });
        }

        public AssetReference GetReference(ref int4x4 guid)
        {
            if (!referenceCacheDict.isCreated) referenceCacheDict = new NativeDictionary<int4x4, int, Int4x4Equal>(100, Allocator.Persistent, new Int4x4Equal());
            int index;
            if (referenceCacheDict.Get(guid, out index))
            {
                return allReferenceCache[index];
            }
            string guidCache = new string((char*)guid.Ptr(), 0, 32);
            referenceCacheDict.Add(guid, allReferenceCache.Count);
            AssetReference aref = new AssetReference(guidCache);
            allReferenceCache.Add(aref);
            return aref;
        }
        public void Init(PipelineResources res)
        {
            current = this;
            referenceCacheDict = new NativeDictionary<int4x4, int, Int4x4Equal>(200, Allocator.Persistent, new Int4x4Equal());
            mipIDs = new NativeArray<int>(2, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            mipIDs[0] = Shader.PropertyToID("_Mip0");
            mipIDs[1] = Shader.PropertyToID("_Mip1");
            /*mipIDs[2] = Shader.PropertyToID("_Mip2");
            mipIDs[3] = Shader.PropertyToID("_Mip3");
            mipIDs[4] = Shader.PropertyToID("_Mip4");
            mipIDs[5] = Shader.PropertyToID("_Mip5");*/
            rgbaPool.Init(0, GraphicsFormat.R8G8B8A8_UNorm, (int)fixedTextureSize, this);
            emissionPool.Init(2, GraphicsFormat.R16G16B16A16_SFloat, (int)fixedTextureSize, this);
            heightPool.Init(3, GraphicsFormat.R8_UNorm, (int)fixedTextureSize, this);
            vmManager = new VirtualMaterialManager(materialPoolSize, maximumMaterialCount, res.shaders.streamingShader);
            SceneStreaming.loading = false;

        }
        public void UpdateData(CommandBuffer buffer, PipelineResources res)
        {
            for (int i = 0; i < asyncLoader.Count; ++i)
            {
                var loader = asyncLoader[i];
                if (!loader.startLoading)
                {
                    loader.startLoading = true;
                    loader.loader = loader.aref.LoadAssetAsync<Texture>();
                    asyncLoader[i] = loader;
                }
                bool value = loader.loader.IsDone;
                if (value)
                {
                    ComputeShader loadShader = res.shaders.streamingShader;
                    int2 resolution = int2(loader.targetTexArray.width, loader.targetTexArray.height);
                    int blitPass;
                    if (loader.isNormal)
                        blitPass = 6;
                    //Graphics.Blit(loader.loader.Result, loader.targetTexArray, blitNormalMat, 0, loader.targetIndex);
                    else
                        blitPass = 5;
                    //Graphics.Blit(loader.loader.Result, loader.targetTexArray, 0, loader.targetIndex);
                    loadShader.SetTexture(blitPass, ShaderIDs._SourceTex, loader.loader.Result);
                    loadShader.SetTexture(blitPass, ShaderIDs._DestTex, loader.targetTexArray);
                    loadShader.SetInt(ShaderIDs._Count, loader.targetIndex);
                    int2 disp = resolution.xy / 8;
                    loadShader.Dispatch(blitPass, disp.x, disp.y, 1);
                    buffer.SetComputeIntParam(loadShader, ShaderIDs._Count, loader.targetIndex);
                    for (int mip = 0; mip < mipIDs.Length; ++mip)
                    {
                        buffer.SetComputeTextureParam(loadShader, 4, mipIDs[mip], loader.targetTexArray, mip);
                    }
                    resolution /= 16;
                    buffer.DispatchCompute(loadShader, 4, resolution.x, resolution.y, 1);
                    loader.aref.ReleaseAsset();
                    asyncLoader[i] = asyncLoader[asyncLoader.Count - 1];
                    asyncLoader.RemoveAt(asyncLoader.Count - 1);
                    i--;
                }
            }
        }

        public void Dispose()
        {
            current = null;
            rgbaPool.Dispose();
            emissionPool.Dispose();
            heightPool.Dispose();
            vmManager.Dispose();
            referenceCacheDict.Dispose();
            mipIDs.Dispose();

        }
        #endregion
    }
}