using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using UnityEngine.Rendering;
using UnityEngine.AddressableAssets;
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
        public int fixedTextureSize = 2048;
        public int maximumClusterCount = 100000;
        public int maximumMaterialCount = 1;
        public int materialPoolSize = 500;
        public const int mipCount = 4;
        public List<SceneStreaming> clusterProperties;
        public TexturePool rgbaPool;
        public TexturePool normalPool;
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
            public int targetIndex;
            public bool startLoading;
            public bool isNormal;
        }
        public VirtualMaterialManager vmManager;
        private Material blitNormalMat;
        private MStringBuilder msbForCluster;
        private List<AsyncTextureLoader> asyncLoader = new List<AsyncTextureLoader>(100);
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
            blitNormalMat = new Material(res.shaders.blitNormalShader);
            msbForCluster = new MStringBuilder(100);
            for (int i = 0; i < clusterProperties.Count; ++i)
            {
                var cur = clusterProperties[i];
                cur.Init(i, msbForCluster, this);
            }

            rgbaPool.Init(0, RenderTextureFormat.ARGB32, fixedTextureSize, this, false);
            normalPool.Init(1, RenderTextureFormat.RGHalf, fixedTextureSize, this, true);
            emissionPool.Init(2, RenderTextureFormat.ARGBHalf, fixedTextureSize, this, false);
            heightPool.Init(3, RenderTextureFormat.R8, fixedTextureSize, this, true);
            vmManager = new VirtualMaterialManager(materialPoolSize, maximumMaterialCount, res.shaders.streamingShader);
        }

        public void UpdateData()
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
                    if (loader.isNormal)
                        Graphics.Blit(loader.loader.Result, loader.targetTexArray, blitNormalMat, 0, loader.targetIndex);
                    else
                        Graphics.Blit(loader.loader.Result, loader.targetTexArray, 0, loader.targetIndex);
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
            normalPool.Dispose();
            emissionPool.Dispose();
            heightPool.Dispose();
            vmManager.Dispose();
            DestroyImmediate(blitNormalMat);
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
