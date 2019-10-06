using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using static Unity.Mathematics.math;
using Unity.Jobs;
using System.IO;
using System.Threading;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.Rendering;
namespace MPipeline
{
    public unsafe sealed class MTerrain : JobProcessEvent
    {
        public static MTerrain current { get; private set; }
        public struct TerrainChunkBuffer
        {
            public float2 worldPos;
            public float2 scale;
            public uint2 uvStartIndex;
        }
        [System.Serializable]
        public struct PBRTexture
        {
            public AssetReference albedoOccTex;
            public AssetReference normalTex;
            public AssetReference SMTex;
        }
        public const int MASK_RESOLUTION = 64;
        public const int HEIGHT_RESOLUTION = 256;
        public const int COLOR_RESOLUTION = 1024;
        public MTerrainData terrainData;
#if UNITY_EDITOR
        public Texture2D maskTex;
        public Texture2D heightTex;
        [EasyButtons.Button]
        void GenerateDefaultFile()
        {
            Color[] maskColors = maskTex.GetPixels();
            Color[] heightColors = heightTex.GetPixels();
            byte[] allBytes = new byte[MASK_RESOLUTION * MASK_RESOLUTION + HEIGHT_RESOLUTION * HEIGHT_RESOLUTION * 2];
            for(int i = 0; i < MASK_RESOLUTION * MASK_RESOLUTION; ++i)
            {
                allBytes[i] = (byte)(maskColors[i].r * 255);
            }
            ushort* btPtr = (ushort*)(allBytes.Ptr() + MASK_RESOLUTION * MASK_RESOLUTION);
            for(int i = 0; i < HEIGHT_RESOLUTION * HEIGHT_RESOLUTION; ++i)
            {
                btPtr[i] = (ushort)(heightColors[i].r * 65535);
            }
            using (FileStream fsm = new FileStream(terrainData.readWritePath, FileMode.OpenOrCreate))
            {
                fsm.Position = 0;
                for (int i = 0; i < terrainData.lodDistances.Length; ++i)
                {
                    long v = (long)(0.1 + pow(2.0, i));
                    v *= v;
                    for(int j = 0; j < v; ++j)
                    {
                        fsm.Write(allBytes, 0, allBytes.Length);
                    }
                }
                fsm.Position = 0;
            }
        }
#endif
        #region QUADTREE
        public NativeList_Float allLodLevles;
        public VirtualTextureLoader loader;
        public NativeQueue<TerrainLoadData> loadDataList;
        #endregion

        public Transform cam;
        private ComputeBuffer culledResultsBuffer;
        private ComputeBuffer loadedBuffer;
        private ComputeBuffer dispatchDrawBuffer;
        private ComputeShader shader;
        private ComputeShader textureShader;

        private RenderTexture albedoTex;
        private RenderTexture normalTex;
        private RenderTexture smTex;

        private NativeList<TerrainChunkBuffer> loadedBufferList;
        private static Vector4[] planes = new Vector4[6];
        private TerrainQuadTree tree;
        private JobHandle calculateHandle;
        private MStringBuilder msb;
        private VirtualTexture vt;
        private RenderTexture mask;
        private ComputeBuffer textureBuffer;

        public override void PrepareJob()
        {
            loadedBufferList.Clear();
            calculateHandle = new CalculateQuadTree
            {
                tree = tree.Ptr(),
                cameraXZPos = double2(cam.position.x, cam.position.z),
                loadedBuffer = loadedBufferList,
            }.Schedule();
        }

        public override void FinishJob()
        {
            calculateHandle.Complete();
            loader.StartLoading();
            vt.Update(terrainData.drawTerrainMaterial);
            UpdateBuffer();
        }

        void LoadTexture(VirtualTextureLoader.LoadingHandler handler, int2 startIndex, int size)
        {
            int texElement;
            //Could Debug lefted pool
            //Debug.Log(vt.LeftedTextureElement);
            if (!vt.LoadNewTexture(startIndex, size, out texElement))
            {
                Debug.LogError("Terrain Virtual Texture Pool Not Enough!");
                return;
            }
            textureBuffer.SetDataPtr((uint*)handler.allBytes, MASK_RESOLUTION * MASK_RESOLUTION / 4);
            shader.SetBuffer(2, ShaderIDs._TextureBuffer, textureBuffer);
            shader.SetTexture(2, ShaderIDs._MainTex, mask);
            shader.SetInt(ShaderIDs._Count, MASK_RESOLUTION);
            shader.Dispatch(2, MASK_RESOLUTION / 8, MASK_RESOLUTION / 8, 1);

            textureShader.SetTexture(0, ShaderIDs._SourceTex, mask);
            textureShader.SetTexture(0, ShaderIDs._MainTex, albedoTex);
            textureShader.SetTexture(0, ShaderIDs._BumpMap, normalTex);
            textureShader.SetTexture(0, ShaderIDs._SMMap, smTex);
            textureShader.SetVector(ShaderIDs._TextureSize, float4(mask.width, mask.height, COLOR_RESOLUTION, COLOR_RESOLUTION));

            textureShader.SetTexture(0, ShaderIDs._VirtualMainTex, vt.GetTexture(1));
            textureShader.SetTexture(0, ShaderIDs._VirtualBumpMap, vt.GetTexture(2));
            textureShader.SetTexture(0, ShaderIDs._VirtualSMO, vt.GetTexture(3));
            textureShader.SetInt(ShaderIDs._OffsetIndex, texElement);
            shader.SetInt(ShaderIDs._OffsetIndex, texElement);

            const int disp = COLOR_RESOLUTION / 8;
            textureShader.Dispatch(0, disp, disp, 1);
            textureBuffer.SetDataPtr((uint*)(MASK_RESOLUTION * MASK_RESOLUTION + handler.allBytes), HEIGHT_RESOLUTION * HEIGHT_RESOLUTION / 2);
            shader.SetBuffer(3, ShaderIDs._TextureBuffer, textureBuffer);
            shader.SetTexture(3, ShaderIDs._VirtualHeightmap, vt.GetTexture(0));
            shader.SetInt(ShaderIDs._Count, HEIGHT_RESOLUTION);
            shader.Dispatch(3, HEIGHT_RESOLUTION / 8, HEIGHT_RESOLUTION / 8, 1);
        }
        static bool GetComplete(ref VirtualTextureLoader.LoadingHandler handler)
        {
            return *handler.isComplete;
        }
        IEnumerator AsyncLoader()
        {
            textureShader.SetTexture(1, ShaderIDs._VirtualMainTex, albedoTex);
            textureShader.SetTexture(1, ShaderIDs._VirtualBumpMap, normalTex);
            textureShader.SetTexture(1, ShaderIDs._VirtualSMO, smTex);
            textureShader.SetTexture(2, ShaderIDs._VirtualMainTex, albedoTex);
            textureShader.SetTexture(2, ShaderIDs._VirtualBumpMap, normalTex);
            textureShader.SetTexture(2, ShaderIDs._VirtualSMO, smTex);
            for (int i = 0; i < terrainData.textures.Length; ++i)
            {
                PBRTexture texs = terrainData.textures[i];
                AsyncOperationHandle<Texture> albedoLoader = texs.albedoOccTex.LoadAssetAsync<Texture>();
                AsyncOperationHandle<Texture> normalLoader = texs.normalTex.LoadAssetAsync<Texture>();
                AsyncOperationHandle<Texture> smLoader = texs.SMTex.LoadAssetAsync<Texture>();
                yield return albedoLoader;
                yield return normalLoader;
                yield return smLoader;
                yield return null;
                const int disp = COLOR_RESOLUTION / 8;
                textureShader.SetInt(ShaderIDs._OffsetIndex, i);
                if (albedoLoader.Result && normalLoader.Result && smLoader.Result)
                {
                    textureShader.SetTexture(1, ShaderIDs._TerrainMainTexArray, albedoLoader.Result);
                    textureShader.SetTexture(1, ShaderIDs._TerrainBumpMapArray, normalLoader.Result);
                    textureShader.SetTexture(1, ShaderIDs._TerrainSMTexArray, smLoader.Result);
                    textureShader.Dispatch(1, disp, disp, 1);
                }
                else
                {
                    textureShader.Dispatch(2, disp, disp, 1);
                }
                texs.albedoOccTex.ReleaseAsset();
                texs.normalTex.ReleaseAsset();
                texs.SMTex.ReleaseAsset();
            }
            textureShader.SetInt(ShaderIDs._Count, terrainData.textures.Length);
            while (enabled)
            {
                TerrainLoadData loadData;
                if (loadDataList.TryDequeue(out loadData))
                {
                    switch (loadData.ope)
                    {
                        case TerrainLoadData.Operator.Load:
                            while (!GetComplete(ref loadData.handler0))
                                yield return null;
                            LoadTexture(loadData.handler0, loadData.startIndex, loadData.size);
                            loadData.handler0.Dispose();
                            break;
                        case TerrainLoadData.Operator.Separate:
                            while (!GetComplete(ref loadData.handler0))
                                yield return null;
                            while (!GetComplete(ref loadData.handler1))
                                yield return null;
                            while (!GetComplete(ref loadData.handler2))
                                yield return null;
                            while (!GetComplete(ref loadData.handler3))
                                yield return null;
                            int subSize = loadData.size / 2;
                            LoadTexture(loadData.handler0, loadData.startIndex, subSize);
                            LoadTexture(loadData.handler1, loadData.startIndex + int2(0, subSize), subSize);
                            LoadTexture(loadData.handler2, loadData.startIndex + int2(subSize, 0), subSize);
                            LoadTexture(loadData.handler3, loadData.startIndex + subSize, subSize);
                            loadData.handler0.Dispose();
                            loadData.handler1.Dispose();
                            loadData.handler2.Dispose();
                            loadData.handler3.Dispose();
                            break;
                        case TerrainLoadData.Operator.Unload:
                            vt.UnloadTexture(loadData.startIndex);
                            break;
                        case TerrainLoadData.Operator.Combine:
                            vt.CombineTexture(loadData.startIndex, loadData.size, false);
                            break;
                    }
                }
                else yield return null;
            }
        }

        protected override void OnEnableFunc()
        {
            if (current && current != this)
            {
                enabled = false;
                Debug.LogError("Only One Terrain allowed!");
                return;
            }
            if (!terrainData)
            {
                enabled = false;
                Debug.LogError("No Data!");
                return;
            }
            msb = new MStringBuilder(32);
            textureShader = Resources.Load<ComputeShader>("ProceduralTexture");
            shader = Resources.Load<ComputeShader>("TerrainCompute");
            current = this;
            int indexMapSize = 1;
            for (int i = 1; i < terrainData.lodDistances.Length; ++i)
            {
                indexMapSize *= 2;
            }
            dispatchDrawBuffer = new ComputeBuffer(5, sizeof(int), ComputeBufferType.IndirectArguments);
            const int INIT_LENGTH = 500;
            culledResultsBuffer = new ComputeBuffer(INIT_LENGTH, sizeof(int));
            loadedBuffer = new ComputeBuffer(INIT_LENGTH, sizeof(TerrainChunkBuffer));
            loadedBufferList = new NativeList<TerrainChunkBuffer>(INIT_LENGTH, Allocator.Persistent);
            loader = new VirtualTextureLoader(terrainData.lodDistances.Length, terrainData.readWritePath, this);
            loadDataList = new NativeQueue<TerrainLoadData>(100, Allocator.Persistent);
            NativeArray<uint> dispatchDraw = new NativeArray<uint>(5, Allocator.Temp, NativeArrayOptions.ClearMemory);
            dispatchDraw[0] = 6;
            dispatchDrawBuffer.SetData(dispatchDraw);
            VirtualTextureFormat* formats = stackalloc VirtualTextureFormat[]
            {
                new VirtualTextureFormat((VirtualTextureSize)HEIGHT_RESOLUTION, RenderTextureFormat.R16, "_VirtualHeightmap"),
                new VirtualTextureFormat((VirtualTextureSize)COLOR_RESOLUTION, RenderTextureFormat.ARGB32, "_VirtualMainTex"),
                new VirtualTextureFormat((VirtualTextureSize)COLOR_RESOLUTION, RenderTextureFormat.RGHalf, "_VirtualBumpMap"),
                new VirtualTextureFormat((VirtualTextureSize)COLOR_RESOLUTION, RenderTextureFormat.RG16, "_VirtualSMMap")
            };
            vt = new VirtualTexture(terrainData.virtualTexCapacity, min(2048, (int)(pow(2.0, terrainData.lodDistances.Length) + 0.1)), formats, 4, "_TerrainVTIndexTex");
            allLodLevles = new NativeList_Float(terrainData.lodDistances.Length, Allocator.Persistent);
            for (int i = 0; i < terrainData.lodDistances.Length; ++i)
            {
                allLodLevles.Add(min(terrainData.lodDistances[max(0, i - 1)], terrainData.lodDistances[i]));
            }
            allLodLevles[terrainData.lodDistances.Length] = 0;

            albedoTex = new RenderTexture(new RenderTextureDescriptor
            {
                colorFormat = RenderTextureFormat.ARGB32,
                dimension = TextureDimension.Tex2DArray,
                width = COLOR_RESOLUTION,
                height = COLOR_RESOLUTION,
                volumeDepth = Mathf.Max(1, terrainData.textures.Length),
                enableRandomWrite = true,
                msaaSamples = 1
            });
            albedoTex.Create();
            normalTex = new RenderTexture(new RenderTextureDescriptor
            {
                colorFormat = RenderTextureFormat.RGHalf,
                dimension = TextureDimension.Tex2DArray,
                width = COLOR_RESOLUTION,
                height = COLOR_RESOLUTION,
                volumeDepth = Mathf.Max(1, terrainData.textures.Length),
                enableRandomWrite = true,
                msaaSamples = 1
            });
            normalTex.Create();
            smTex = new RenderTexture(new RenderTextureDescriptor
            {
                colorFormat = RenderTextureFormat.RG16,
                dimension = TextureDimension.Tex2DArray,
                width = COLOR_RESOLUTION,
                height = COLOR_RESOLUTION,
                volumeDepth = Mathf.Max(1, terrainData.textures.Length),
                enableRandomWrite = true,
                msaaSamples = 1
            });
            smTex.Create();
            mask = new RenderTexture(new RenderTextureDescriptor
            {
                colorFormat = RenderTextureFormat.R8,
                dimension = TextureDimension.Tex2D,
                width = MASK_RESOLUTION,
                height = MASK_RESOLUTION,
                volumeDepth = 1,
                enableRandomWrite = true,
                msaaSamples = 1
            });
            mask.Create();
            textureBuffer = new ComputeBuffer(max(MASK_RESOLUTION * MASK_RESOLUTION / 4, HEIGHT_RESOLUTION * HEIGHT_RESOLUTION / 2), 4);
            tree = new TerrainQuadTree(-1, TerrainQuadTree.LocalPos.LeftDown, 0);
            StartCoroutine(AsyncLoader());
        }
        void UpdateBuffer()
        {
            if (!loadedBufferList.isCreated) return;
            if (loadedBufferList.Length > loadedBuffer.count)
            {
                loadedBuffer.Dispose();
                culledResultsBuffer.Dispose();
                loadedBuffer = new ComputeBuffer(loadedBufferList.Capacity, sizeof(TerrainChunkBuffer));
                culledResultsBuffer = new ComputeBuffer(loadedBufferList.Capacity, sizeof(int));
            }
            loadedBuffer.SetDataPtr(loadedBufferList.unsafePtr, loadedBufferList.Length);
        }

        public void DrawTerrain(CommandBuffer buffer, int pass, Vector4[] planes)
        {
            if (loadedBufferList.Length <= 0) return;
            buffer.SetComputeBufferParam(shader, 1, ShaderIDs._DispatchBuffer, dispatchDrawBuffer);
            buffer.SetComputeBufferParam(shader, 0, ShaderIDs._DispatchBuffer, dispatchDrawBuffer);
            buffer.SetComputeBufferParam(shader, 0, ShaderIDs._CullResultBuffer, culledResultsBuffer);
            buffer.SetComputeBufferParam(shader, 0, ShaderIDs._TerrainChunks, loadedBuffer);
            buffer.SetGlobalBuffer(ShaderIDs._TerrainChunks, loadedBuffer);
            buffer.SetGlobalVector(ShaderIDs._HeightScaleOffset, float4(terrainData.heightScale, terrainData.heightOffset, 1, 1));
            buffer.SetGlobalBuffer(ShaderIDs._CullResultBuffer, culledResultsBuffer);
            buffer.SetComputeVectorArrayParam(shader, ShaderIDs.planes, planes);
            buffer.DispatchCompute(shader, 1, 1, 1, 1);
            ComputeShaderUtility.Dispatch(shader, buffer, 0, loadedBufferList.Length);
            buffer.DrawProceduralIndirect(Matrix4x4.identity, terrainData.drawTerrainMaterial, pass, MeshTopology.Triangles, dispatchDrawBuffer);
        }

        public void DrawTerrain(CommandBuffer buffer, int pass, float4* planePtr)
        {
            UnsafeUtility.MemCpy(planes.Ptr(), planePtr, sizeof(float4) * 6);
            DrawTerrain(buffer, pass, planes);
        }

        protected override void OnDisableFunc()
        {
            if (current != this) return;
            current = null;
            if (culledResultsBuffer != null) culledResultsBuffer.Dispose();
            if (loadedBuffer != null) loadedBuffer.Dispose();
            if (dispatchDrawBuffer != null) dispatchDrawBuffer.Dispose();
            if (loadedBufferList.isCreated) loadedBufferList.Dispose();
            tree.Dispose();
            vt.Dispose();
            loadDataList.Dispose();
            loader.Dispose();
            allLodLevles.Dispose();
            textureBuffer.Dispose();

            DestroyImmediate(albedoTex);
            DestroyImmediate(normalTex);
            DestroyImmediate(smTex);
            DestroyImmediate(mask);

        }

        private struct CalculateQuadTree : IJob
        {
            [NativeDisableUnsafePtrRestriction]
            public TerrainQuadTree* tree;
            public double2 cameraXZPos;
            public NativeList<TerrainChunkBuffer> loadedBuffer;

            public void Execute()
            {

                tree->CheckUpdate(cameraXZPos);
                tree->PushDrawRequest(loadedBuffer);
            }
        }
    }
}