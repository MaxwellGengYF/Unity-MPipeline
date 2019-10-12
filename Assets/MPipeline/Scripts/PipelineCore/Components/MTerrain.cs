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
        public struct MaskLoadCommand
        {
            public bool load;
            public int2 pos;
        }
        public const int MASK_RESOLUTION = 64;
        public const int HEIGHT_RESOLUTION = 256;
        public const int COLOR_RESOLUTION = 1024;
        public MTerrainData terrainData;

        #region QUADTREE
        public NativeList_Float allLodLevles;
        public VirtualTextureLoader loader;
        [System.NonSerialized]
        public int lodOffset;
        public NativeQueue<TerrainLoadData> loadDataList;
        public NativeQueue<MaskLoadCommand> maskLoadList;
        #endregion

        public Transform cam;
        private ComputeBuffer culledResultsBuffer;
        private ComputeBuffer loadedBuffer;
        private ComputeBuffer dispatchDrawBuffer;
        private ComputeShader shader;
        private ComputeShader textureShader;
        private int largestChunkCount;
        private RenderTexture albedoTex;
        private RenderTexture normalTex;
        private RenderTexture smTex;
        private VirtualTexture maskVT;
        private NativeList<TerrainChunkBuffer> loadedBufferList;
        private static Vector4[] planes = new Vector4[6];
        private TerrainQuadTree tree;
        private JobHandle calculateHandle;
        private MStringBuilder msb;
        private VirtualTexture vt;
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

        void LoadTexture(VirtualTextureLoader.LoadingHandler handler, int2 startIndex, int size, int2 rootPos, float3 maskScaleOffset)
        {
            int texElement;
            //Could Debug lefted pool
            //Debug.Log(vt.LeftedTextureElement);
            if (!vt.LoadNewTexture(startIndex, size, out texElement))
            {
                Debug.LogError("Terrain Virtual Texture Pool Not Enough!");
                return;
            }
            rootPos += (int2)maskScaleOffset.yz;
            maskScaleOffset.yz = frac(maskScaleOffset.yz);
            textureShader.SetInt(ShaderIDs._OffsetIndex, texElement);
            textureShader.SetTexture(0, ShaderIDs._VirtualMainTex, vt.GetTexture(1));
            textureShader.SetTexture(0, ShaderIDs._VirtualBumpMap, vt.GetTexture(2));
            textureShader.SetTexture(0, ShaderIDs._VirtualSMO, vt.GetTexture(3));
            textureShader.SetInt(ShaderIDs._Count, COLOR_RESOLUTION);
            textureShader.SetVector(ShaderIDs._IndexBuffer, float4(rootPos, 1, 1));
            textureShader.SetVector(ShaderIDs._IndexTextureSize, float4(MASK_RESOLUTION, terrainData.allMaskTextures.Length, vt.indexSize));
            textureShader.SetVector(ShaderIDs._TextureSize, float4(maskScaleOffset, size));
            int rtID = maskVT.GetTextureFormat(0).rtPropertyID;
            textureShader.SetTexture(0, rtID, maskVT.GetTexture(0));
            textureShader.SetTexture(0, maskVT.indexTexID, maskVT.indexTex);
            textureShader.SetTexture(0, ShaderIDs._MainTex, albedoTex);
            textureShader.SetTexture(0, ShaderIDs._BumpMap, normalTex);
            textureShader.SetTexture(0, ShaderIDs._SMMap, smTex);

            const int disp = COLOR_RESOLUTION / 8;
            textureShader.Dispatch(0, disp, disp, 1);
            shader.SetInt(ShaderIDs._OffsetIndex, texElement);
            /*   textureBuffer.SetDataPtr((uint*)handler.allBytes, MASK_RESOLUTION * MASK_RESOLUTION / 4);
               shader.SetBuffer(2, ShaderIDs._TextureBuffer, textureBuffer);
               shader.SetTexture(2, ShaderIDs._MainTex, mask);
               shader.SetInt(ShaderIDs._Count, MASK_RESOLUTION);
               shader.Dispatch(2, MASK_RESOLUTION / 8, MASK_RESOLUTION / 8, 1);

               textureShader.SetTexture(0, ShaderIDs._SourceTex, mask);
               textureShader.SetTexture(0, ShaderIDs._MainTex, albedoTex);
               textureShader.SetTexture(0, ShaderIDs._BumpMap, normalTex);
               textureShader.SetTexture(0, ShaderIDs._SMMap, smTex);
               textureShader.SetVector(ShaderIDs._TextureSize, float4(MASK_RESOLUTION, 1f / COLOR_RESOLUTION, size, 1));
               
            */
            textureBuffer.SetDataPtr((uint*)(handler.allBytes), HEIGHT_RESOLUTION * HEIGHT_RESOLUTION / 2);
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
                MaskLoadCommand maskCommand;
                while (maskLoadList.TryDequeue(out maskCommand))
                {
                    if (maskCommand.load)
                    {
                        AssetReference arf = terrainData.allMaskTextures[maskCommand.pos.y * largestChunkCount + maskCommand.pos.x];
                        var maskLoader = arf.LoadAssetAsync<Texture>();
                        yield return maskLoader;
                        Texture maskTex = maskLoader.Result;
                        int maskEle;
                        if (maskVT.LoadNewTexture(maskCommand.pos, 1, out maskEle))
                        {
                            Graphics.Blit(maskTex, maskVT.GetTexture(0), 0, maskEle);
                        }
                        maskTex = null;
                        arf.ReleaseAsset();
                    }
                    else
                    {
                        maskVT.UnloadTexture(maskCommand.pos);

                    }

                }
                if (loadDataList.TryDequeue(out loadData))
                {
                    switch (loadData.ope)
                    {
                        case TerrainLoadData.Operator.Load:
                            while (!GetComplete(ref loadData.handler0))
                                yield return null;
                            textureShader.SetTexture(0, ShaderIDs._NoiseTexture, terrainData.warpNoiseTexture);
                            LoadTexture(loadData.handler0, loadData.startIndex, loadData.size, loadData.rootPos, loadData.maskScaleOffset);
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
                            float subScale = loadData.maskScaleOffset.x;
                            float2 leftUpOffset = float2(loadData.maskScaleOffset.yz + float2(0, subScale));
                            float2 rightDownOffset = float2(loadData.maskScaleOffset.yz + float2(subScale, 0));
                            float2 rightUpOffset = float2(loadData.maskScaleOffset.yz + subScale);
                            textureShader.SetTexture(0, ShaderIDs._NoiseTexture, terrainData.warpNoiseTexture);
                            LoadTexture(loadData.handler0, loadData.startIndex, subSize, loadData.rootPos, loadData.maskScaleOffset);
                            LoadTexture(loadData.handler1, loadData.startIndex + int2(0, subSize), subSize, loadData.rootPos, float3(subScale, leftUpOffset));
                            LoadTexture(loadData.handler2, loadData.startIndex + int2(subSize, 0), subSize, loadData.rootPos, float3(subScale, rightDownOffset));
                            LoadTexture(loadData.handler3, loadData.startIndex + subSize, subSize, loadData.rootPos, float3(subScale, rightUpOffset));
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
            lodOffset = terrainData.lodDistances.Length - terrainData.renderingLevelCount;
            largestChunkCount = (int)(0.1 + pow(2.0, lodOffset));
            if (terrainData.allMaskTextures.Length < largestChunkCount * largestChunkCount)
            {
                enabled = false;
                Debug.LogError("Mask Texture Not Enough!");
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
            maskLoadList = new NativeQueue<MaskLoadCommand>(10, Allocator.Persistent);
            dispatchDrawBuffer = new ComputeBuffer(5, sizeof(int), ComputeBufferType.IndirectArguments);
            const int INIT_LENGTH = 500;
            culledResultsBuffer = new ComputeBuffer(INIT_LENGTH, sizeof(int));
            loadedBuffer = new ComputeBuffer(INIT_LENGTH, sizeof(TerrainChunkBuffer));
            loadedBufferList = new NativeList<TerrainChunkBuffer>(INIT_LENGTH, Allocator.Persistent);

            loader = new VirtualTextureLoader(lodOffset, terrainData.renderingLevelCount, terrainData.readWritePath, this);
            loadDataList = new NativeQueue<TerrainLoadData>(100, Allocator.Persistent);
            NativeArray<uint> dispatchDraw = new NativeArray<uint>(5, Allocator.Temp, NativeArrayOptions.ClearMemory);
            dispatchDraw[0] = 96;
            dispatchDrawBuffer.SetData(dispatchDraw);
            VirtualTextureFormat* formats = stackalloc VirtualTextureFormat[]
            {
                new VirtualTextureFormat((VirtualTextureSize)HEIGHT_RESOLUTION, RenderTextureFormat.R16, "_VirtualHeightmap"),
                new VirtualTextureFormat((VirtualTextureSize)COLOR_RESOLUTION, RenderTextureFormat.ARGB32, "_VirtualMainTex"),
                new VirtualTextureFormat((VirtualTextureSize)COLOR_RESOLUTION, RenderTextureFormat.RGHalf, "_VirtualBumpMap"),
                new VirtualTextureFormat((VirtualTextureSize)COLOR_RESOLUTION, RenderTextureFormat.RG16, "_VirtualSMMap")
            };
            vt = new VirtualTexture(terrainData.virtualTexCapacity, min(MASK_RESOLUTION, (int)(pow(2.0, terrainData.lodDistances.Length) + 0.1)), formats, 4, "_TerrainVTIndexTex");
            VirtualTextureFormat* maskFormats = stackalloc VirtualTextureFormat[]
            {
                new VirtualTextureFormat((VirtualTextureSize)MASK_RESOLUTION, RenderTextureFormat.R8, "_VirtualMaskmap")
            };
            maskVT = new VirtualTexture(6, largestChunkCount, maskFormats, 1, "_MaskIndexMap");
            maskVT.GetTexture(0).filterMode = FilterMode.Point;
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
            smTex.wrapMode = TextureWrapMode.Repeat;
            normalTex.wrapMode = TextureWrapMode.Repeat;
            albedoTex.wrapMode = TextureWrapMode.Repeat;
            textureBuffer = new ComputeBuffer(HEIGHT_RESOLUTION * HEIGHT_RESOLUTION / 2, 4);
            tree = new TerrainQuadTree(-1, TerrainQuadTree.LocalPos.LeftDown, 0, terrainData.largestChunkSize, double3(2, 0, 0), 0);
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
            tree.Dispose();
            current = null;
            if (culledResultsBuffer != null) culledResultsBuffer.Dispose();
            if (loadedBuffer != null) loadedBuffer.Dispose();
            if (dispatchDrawBuffer != null) dispatchDrawBuffer.Dispose();
            if (loadedBufferList.isCreated) loadedBufferList.Dispose();
            vt.Dispose();
            maskVT.Dispose();
            loadDataList.Dispose();
            loader.Dispose();
            allLodLevles.Dispose();
            textureBuffer.Dispose();
            maskLoadList.Dispose();
            DestroyImmediate(albedoTex);
            DestroyImmediate(normalTex);
            DestroyImmediate(smTex);
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