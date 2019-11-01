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
        public TexturePool dxtPool;
        public TexturePool hdrPool;
        public TexturePool r8Pool;
        public const string infosPath = "Assets/BinaryData/MapDatas/";
        #endregion

        #region NON_SERIALIZABLE
        private struct AsyncTextureLoader
        {
            public AssetReference aref;
            public AsyncOperationHandle<Texture> loader;
            public Texture2DArray targetTexArray;
            public int targetIndex;
            public bool startLoading;
            public bool isNormal;
        }
        public VirtualMaterialManager vmManager;
        private MStringBuilder msbForCluster;
        private List<AsyncTextureLoader> asyncLoader = new List<AsyncTextureLoader>(100);
        public void AddLoadCommand(AssetReference aref, Texture2DArray targetTexArray, int targetIndex, bool isNormal)
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
            msbForCluster = new MStringBuilder(100);
            for (int i = 0; i < clusterProperties.Count; ++i)
            {
                var cur = clusterProperties[i];
                cur.Init(i, msbForCluster, this);
            }

            dxtPool.Init(0, GraphicsFormat.RGBA_DXT5_UNorm, (int)fixedTextureSize, this, false);
            hdrPool.Init(2, GraphicsFormat.R16G16B16A16_SFloat, (int)fixedTextureSize, this, false);
            r8Pool.Init(3, GraphicsFormat.R8_UNorm, (int)fixedTextureSize, this, true);
            vmManager = new VirtualMaterialManager(materialPoolSize, maximumMaterialCount, res.shaders.streamingShader);
        }

        public void UpdateData(PipelineResources res)
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
                    int2 resolution = int2(loader.loader.Result.width, loader.loader.Result.height);
                    int targetLevel = min(6, loader.loader.Result.mipmapCount);
                    for(int x = 0; x < targetLevel; ++x)
                    {
                        Graphics.CopyTexture(loader.loader.Result, 0, x, 0, 0, resolution.x, resolution.y, loader.targetTexArray, loader.targetIndex, x, 0, 0);
                        resolution /= 2;
                    }
                    
                    loader.aref.ReleaseAsset();
                    asyncLoader[i] = asyncLoader[asyncLoader.Count - 1];
                    asyncLoader.RemoveAt(asyncLoader.Count - 1);
                    i--;
                }
            }
        }

        public void Dispose()
        {
            dxtPool.Dispose();
            r8Pool.Dispose();
            hdrPool.Dispose();
            vmManager.Dispose();
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
