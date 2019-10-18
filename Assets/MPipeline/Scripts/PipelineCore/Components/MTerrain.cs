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
using UnityEngine.Experimental.Rendering;
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
        public const int MASK_RESOLUTION = 512;
        public const int HEIGHT_RESOLUTION = 256;
        public const int COLOR_RESOLUTION = 1024;
        public const GraphicsFormat HEIGHT_FORMAT = GraphicsFormat.R16_UNorm;
        public MTerrainData terrainData;
        public VTDecalCamera decalCamera;
        #region QUADTREE
        public NativeList_Float allLodLevles;
        public VirtualTextureLoader loader;
        [System.NonSerialized]
        public int lodOffset;
        [System.NonSerialized]
        public int decalLayerOffset;
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
        private RenderTexture heightTex;
        private VirtualTexture maskVT;
        private NativeList<TerrainChunkBuffer> loadedBufferList;
        private static Vector4[] planes = new Vector4[6];
        private TerrainQuadTree tree;
        private JobHandle calculateHandle;
        private MStringBuilder msb;
        private VirtualTexture vt;
        private double oneVTPixelWorldLength;
        private RenderTexture worldNormalRT;
        private ComputeBuffer textureBuffer;
        private ComputeBuffer materialBuffer;
        private int leastVirtualTextureLefted = int.MaxValue;

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
            int2 FloorToInt(double2 value)
            {
                bool2 lz = value < 0;
                int2 v = (int2)value;
                if (lz.x)
                    v.x -= 1;
                if (lz.y)
                    v.y -= 1;
                return v;
            }
            calculateHandle.Complete();
            loader.StartLoading();
            vt.Update();
            CommandBuffer beforeBuffer = RenderPipeline.BeforeFrameBuffer;
            double2 pixelOffset = -terrainData.screenOffset / oneVTPixelWorldLength;
            beforeBuffer.SetGlobalVector(ShaderIDs._TerrainVTOffset, float4(
                FloorToInt(pixelOffset),
                (float2)frac(pixelOffset)
                ));
            beforeBuffer.SetGlobalFloat(ShaderIDs._TerrainWorldPosToVTUV, (float)(1.0 / oneVTPixelWorldLength));
            UpdateBuffer();
        }

        void ConvertNormalMap(int2 startIndex, int size, int texElement)
        {
            textureShader.SetInt(ShaderIDs._OffsetIndex, texElement);
            textureShader.SetVector(ShaderIDs._IndexTextureSize, float4(float2((float)size / HEIGHT_RESOLUTION), vt.indexSize));
            double oneHeightPixelSize = size * oneVTPixelWorldLength / HEIGHT_RESOLUTION;
            textureShader.SetVector(ShaderIDs._IndexBuffer, float4(startIndex, terrainData.heightScale, (float)(1.0 / oneHeightPixelSize)));
            const int heightDisp = HEIGHT_RESOLUTION / 8;
            const int disp = COLOR_RESOLUTION / 8;
            textureShader.Dispatch(3, heightDisp, heightDisp, 1);

            textureShader.Dispatch(4, disp, disp, 1);
        }

        int LoadTexture(VirtualTextureLoader.LoadingHandler handler, int2 startIndex, int size, int2 rootPos, float3 maskScaleOffset)
        {
            int texElement;
            //Could Debug lefted pool
            if (leastVirtualTextureLefted > vt.LeftedTextureElement)
            {
                leastVirtualTextureLefted = vt.LeftedTextureElement;
                Debug.Log(vt.LeftedTextureElement);
            }

            if (!vt.LoadNewTexture(startIndex, size, out texElement))
            {
                Debug.LogError("Terrain Virtual Texture Pool Not Enough!");
                return -1;
            }
            shader.SetInt(ShaderIDs._OffsetIndex, texElement);
            textureBuffer.SetDataPtr((uint*)(handler.allBytes), HEIGHT_RESOLUTION * HEIGHT_RESOLUTION / 2);
            

            shader.Dispatch(3, HEIGHT_RESOLUTION / 8, HEIGHT_RESOLUTION / 8, 1);

            rootPos += (int2)maskScaleOffset.yz;
            maskScaleOffset.yz = frac(maskScaleOffset.yz);
            textureShader.SetInt(ShaderIDs._OffsetIndex, texElement);
            textureShader.SetVector(ShaderIDs._IndexBuffer, float4(rootPos, 1, 1));
            textureShader.SetVector(ShaderIDs._IndexTextureSize, float4(MASK_RESOLUTION, min(255, terrainData.allMaterials.Length), vt.indexSize));
            textureShader.SetVector(ShaderIDs._TextureSize, float4(maskScaleOffset, size * terrainData.materialTillingScale));
            const int disp = COLOR_RESOLUTION / 8;
            textureShader.Dispatch(0, disp, disp, 1);
            return texElement;
        }
        static bool GetComplete(ref VirtualTextureLoader.LoadingHandler handler)
        {
            return *handler.isComplete;
        }
        double2 VTPosToWorldPos(int2 vtPos)
        {
            double2 vtPosDouble = (double2)vtPos * oneVTPixelWorldLength;
            vtPosDouble -= terrainData.screenOffset;
            return vtPosDouble;
        }
        void DrawDecal(int2 startIndex, int size, int targetElement, LayerMask decalCullingMask)
        {
            double cameraSize = size * oneVTPixelWorldLength * 0.5;
            double2 centerPos = VTPosToWorldPos(startIndex) + cameraSize;
            decalCamera.renderingCommand.Add(new VTDecalCamera.CameraState
            {
                albedoRT = vt.GetTexture(1),
                normalRT = vt.GetTexture(2),
                smoRT = vt.GetTexture(3),
                cullingMask = decalCullingMask,
                depthSlice = targetElement,
                farClipPlane = 50,
                nearClipPlane = -50,
                heightRT = vt.GetTexture(0),
                position = float3((float)centerPos.x, terrainData.heightOffset + terrainData.heightScale * 0.5f, (float)centerPos.y),
                rotation = Quaternion.Euler(90, 0, 0),
                size = (float)cameraSize
            });
        }
        IEnumerator AsyncLoader()
        {
            textureShader.SetTexture(1, ShaderIDs._VirtualMainTex, albedoTex);
            textureShader.SetTexture(1, ShaderIDs._VirtualBumpMap, normalTex);
            textureShader.SetTexture(1, ShaderIDs._VirtualSMO, smTex);
            textureShader.SetTexture(1, ShaderIDs._MaskTex, heightTex);
            textureShader.SetTexture(2, ShaderIDs._VirtualMainTex, albedoTex);
            textureShader.SetTexture(2, ShaderIDs._VirtualBumpMap, normalTex);
            textureShader.SetTexture(2, ShaderIDs._VirtualSMO, smTex);
            textureShader.SetTexture(2, ShaderIDs._MaskTex, heightTex);
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
            //textureShader.SetInt(ShaderIDs._Count, terrainData.textures.Length);
            while (enabled)
            {
                TerrainLoadData loadData;
                MaskLoadCommand maskCommand;
                shader.SetInt(ShaderIDs._HeightResolution, HEIGHT_RESOLUTION);
                textureShader.SetInt(ShaderIDs._ColorResolution, COLOR_RESOLUTION);
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
                    if (loadData.ope == TerrainLoadData.Operator.Load || loadData.ope == TerrainLoadData.Operator.Separate)
                    {
                        shader.SetTexture(3, ShaderIDs._VirtualHeightmap, vt.GetTexture(0));
                        shader.SetBuffer(3, ShaderIDs._TextureBuffer, textureBuffer);
                        textureShader.SetBuffer(0, ShaderIDs._MaterialBuffer, materialBuffer);
                        textureShader.SetTexture(0, ShaderIDs._VirtualMainTex, vt.GetTexture(1));
                        textureShader.SetTexture(0, ShaderIDs._VirtualBumpMap, vt.GetTexture(2));
                        textureShader.SetTexture(0, ShaderIDs._VirtualSMO, vt.GetTexture(3));
                        textureShader.SetTexture(0, ShaderIDs._HeightMap, heightTex);
                        textureShader.SetTexture(0, ShaderIDs._MainTex, albedoTex);
                        textureShader.SetTexture(0, ShaderIDs._BumpMap, normalTex);
                        textureShader.SetTexture(0, ShaderIDs._SMMap, smTex);
                        int rtID = maskVT.GetTextureFormat(0).rtPropertyID;
                        textureShader.SetTexture(0, rtID, maskVT.GetTexture(0));
                        textureShader.SetTexture(0, maskVT.indexTexID, maskVT.indexTex);
                        textureShader.SetTexture(0, ShaderIDs._NoiseTexture, terrainData.warpNoiseTexture);
                        textureShader.SetTexture(3, ShaderIDs._VirtualHeightmap, vt.GetTexture(0));
                        textureShader.SetTexture(3, ShaderIDs._IndexTexture, vt.indexTex);
                        textureShader.SetTexture(3, ShaderIDs._DestTex, worldNormalRT);
                        textureShader.SetTexture(4, ShaderIDs._VirtualBumpMap, vt.GetTexture(2));
                        textureShader.SetTexture(4, ShaderIDs._SourceTex, worldNormalRT);

                    }
                    switch (loadData.ope)
                    {
                        case TerrainLoadData.Operator.Load:
                            while (!GetComplete(ref loadData.handler0))
                                yield return null; int targetElement = LoadTexture(loadData.handler0, loadData.startIndex, loadData.size, loadData.rootPos, loadData.maskScaleOffset);
                            ConvertNormalMap(loadData.startIndex, loadData.size, targetElement);
                            if (loadData.targetDecalLayer != 0)
                            {
                                DrawDecal(loadData.startIndex, loadData.size, targetElement, loadData.targetDecalLayer);
                                yield return null;
                            }
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
                            int ele0 = LoadTexture(loadData.handler0, loadData.startIndex, subSize, loadData.rootPos, loadData.maskScaleOffset);
                            int ele1 = LoadTexture(loadData.handler1, loadData.startIndex + int2(0, subSize), subSize, loadData.rootPos, float3(subScale, leftUpOffset));
                            int ele2 = LoadTexture(loadData.handler2, loadData.startIndex + int2(subSize, 0), subSize, loadData.rootPos, float3(subScale, rightDownOffset));
                            int ele3 = LoadTexture(loadData.handler3, loadData.startIndex + subSize, subSize, loadData.rootPos, float3(subScale, rightUpOffset));

                            ConvertNormalMap(loadData.startIndex, subSize, ele0);
                            ConvertNormalMap(loadData.startIndex + int2(0, subSize), subSize, ele1);
                            ConvertNormalMap(loadData.startIndex + int2(subSize, 0), subSize, ele2);
                            ConvertNormalMap(loadData.startIndex + subSize, subSize, ele3);

                            if (loadData.targetDecalLayer != 0)
                            {
                                DrawDecal(loadData.startIndex, subSize, ele0, loadData.targetDecalLayer);
                                yield return null;
                                DrawDecal(loadData.startIndex + int2(0, subSize), subSize, ele1, loadData.targetDecalLayer);
                                yield return null;
                                DrawDecal(loadData.startIndex + int2(subSize, 0), subSize, ele2, loadData.targetDecalLayer);
                                yield return null;
                                DrawDecal(loadData.startIndex + subSize, subSize, ele3, loadData.targetDecalLayer);
                                yield return null;
                            }
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
            oneVTPixelWorldLength = terrainData.largestChunkSize / pow(2.0, terrainData.lodDistances.Length - 1);
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
                new VirtualTextureFormat((VirtualTextureSize)HEIGHT_RESOLUTION, HEIGHT_FORMAT, "_VirtualHeightmap"),
                new VirtualTextureFormat((VirtualTextureSize)COLOR_RESOLUTION, GraphicsFormat.R8G8B8A8_UNorm, "_VirtualMainTex"),
                new VirtualTextureFormat((VirtualTextureSize)COLOR_RESOLUTION, GraphicsFormat.R16G16_SNorm, "_VirtualBumpMap"),
                new VirtualTextureFormat((VirtualTextureSize)COLOR_RESOLUTION, GraphicsFormat.R8G8_UNorm, "_VirtualSMMap")
            };
            vt = new VirtualTexture(terrainData.virtualTexCapacity, min(MASK_RESOLUTION, (int)(pow(2.0, terrainData.lodDistances.Length) + 0.1)), formats, 4, "_TerrainVTIndexTex");
            VirtualTextureFormat* maskFormats = stackalloc VirtualTextureFormat[]
            {
                new VirtualTextureFormat((VirtualTextureSize)MASK_RESOLUTION, GraphicsFormat.R8_UNorm, "_VirtualMaskmap")
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
                graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm,
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
                graphicsFormat = GraphicsFormat.R16G16_SNorm,
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
                graphicsFormat = GraphicsFormat.R16G16_UNorm,
                dimension = TextureDimension.Tex2DArray,
                width = COLOR_RESOLUTION,
                height = COLOR_RESOLUTION,
                volumeDepth = Mathf.Max(1, terrainData.textures.Length),
                enableRandomWrite = true,
                msaaSamples = 1,
                useMipMap = false,
                autoGenerateMips = false,
                mipCount = 0,
                depthBufferBits = 0,
                useDynamicScale = false
            });
            smTex.Create();
            heightTex = new RenderTexture(new RenderTextureDescriptor
            {
                graphicsFormat = GraphicsFormat.R8_UNorm,
                dimension = TextureDimension.Tex2DArray,
                width = COLOR_RESOLUTION,
                height = COLOR_RESOLUTION,
                volumeDepth = Mathf.Max(1, terrainData.textures.Length),
                enableRandomWrite = true,
                msaaSamples = 1,
                useMipMap = false,
                autoGenerateMips = false,
                mipCount = 0,
                depthBufferBits = 0,
                useDynamicScale = false
            });
            heightTex.Create();
            worldNormalRT = new RenderTexture(HEIGHT_RESOLUTION, HEIGHT_RESOLUTION, 0, RenderTextureFormat.RGFloat, 0);
            worldNormalRT.useMipMap = false;
            worldNormalRT.autoGenerateMips = false;
            worldNormalRT.filterMode = FilterMode.Bilinear;
            worldNormalRT.enableRandomWrite = true;
            worldNormalRT.Create();
            smTex.wrapMode = TextureWrapMode.Repeat;
            normalTex.wrapMode = TextureWrapMode.Repeat;
            albedoTex.wrapMode = TextureWrapMode.Repeat;
            heightTex.wrapMode = TextureWrapMode.Repeat;
            materialBuffer = new ComputeBuffer(max(1, terrainData.allMaterials.Length), sizeof(MTerrainData.HeightBlendMaterial));
            materialBuffer.SetData(terrainData.allMaterials);
            textureBuffer = new ComputeBuffer(HEIGHT_RESOLUTION * HEIGHT_RESOLUTION / 2, 4);
            decalLayerOffset = max(0, terrainData.lodDistances.Length - terrainData.allDecalLayers.Length);
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
            materialBuffer.Dispose();
            allLodLevles.Dispose();
            textureBuffer.Dispose();
            maskLoadList.Dispose();
            DestroyImmediate(worldNormalRT);
            DestroyImmediate(albedoTex);
            DestroyImmediate(normalTex);
            DestroyImmediate(smTex);
            DestroyImmediate(heightTex);
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