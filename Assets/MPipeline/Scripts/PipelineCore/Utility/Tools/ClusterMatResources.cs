using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Unity.Mathematics.math;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine.Rendering;
using UnityEngine.AddressableAssets;
using UnityEngine.Experimental.Rendering;
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

    public class ClusterMatResources : ScriptableObject
    {
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

        public List<SceneStreaming> clusterProperties;
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
        private MStringBuilder msbForCluster;
        private List<AsyncTextureLoader> asyncLoader = new List<AsyncTextureLoader>(100);
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
        public void Init(PipelineResources res)
        {
            mipIDs = new NativeArray<int>(6, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            mipIDs[0] = Shader.PropertyToID("_Mip0");
            mipIDs[1] = Shader.PropertyToID("_Mip1");
            mipIDs[2] = Shader.PropertyToID("_Mip2");
            mipIDs[3] = Shader.PropertyToID("_Mip3");
            mipIDs[4] = Shader.PropertyToID("_Mip4");
            mipIDs[5] = Shader.PropertyToID("_Mip5");
            msbForCluster = new MStringBuilder(100);
            for (int i = 0; i < clusterProperties.Count; ++i)
            {
                var cur = clusterProperties[i];
                cur.Init(i, msbForCluster, this);
            }

            rgbaPool.Init(0, GraphicsFormat.R8G8B8A8_UNorm, (int)fixedTextureSize, this);
            emissionPool.Init(2, GraphicsFormat.R16G16B16A16_SFloat, (int)fixedTextureSize, this);
            heightPool.Init(3, GraphicsFormat.R8_UNorm, (int)fixedTextureSize, this);
            vmManager = new VirtualMaterialManager(materialPoolSize, maximumMaterialCount, res.shaders.streamingShader);
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
                    const int targetLevel = 6;
                    buffer.SetComputeIntParam(loadShader, ShaderIDs._Count, loader.targetIndex);
                    for (int mip = 0; mip < targetLevel; ++mip)
                    {
                        buffer.SetComputeTextureParam(loadShader, 4, mipIDs[mip], loader.targetTexArray, mip);
                    }
                    resolution /= 32;
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
            rgbaPool.Dispose();
            emissionPool.Dispose();
            heightPool.Dispose();
            vmManager.Dispose();
            mipIDs.Dispose();
        }
        public void TransformScene(uint value, MonoBehaviour behavior)
        {
            if (value < clusterProperties.Count)
            {
                SceneStreaming str = clusterProperties[(int)value];
                if (str.state == SceneStreaming.State.Loaded)
                    // str.DeleteSync();
                    behavior.StartCoroutine(str.Delete());
                else if (str.state == SceneStreaming.State.Unloaded)
                    // str.GenerateSync();
                    behavior.StartCoroutine(str.Generate());

            }
        }
        #endregion
    }
}