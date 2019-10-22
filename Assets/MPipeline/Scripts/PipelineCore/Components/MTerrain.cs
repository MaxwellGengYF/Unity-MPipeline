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
        public const int MASK_RESOLUTION = 2048;
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
        public int textureCapacity;
        [System.NonSerialized]
        public int decalLayerOffset;
        public NativeQueue<TerrainLoadData> loadDataList;
        public NativeQueue<TerrainUnloadData> unloadDataList;
        public NativeQueue<MaskLoadCommand> maskLoadList;
        public struct int3Equal : IFunction<int3, int3, bool>
        {
            public bool Run(ref int3 a, ref int3 b)
            {
                bool3 c = a == b;
                return c.x && c.y && c.z;
            }
        }
        //All Enabled Chunk: Key: XY: index Z: size
        public NativeDictionary<int3, bool, int3Equal> enabledChunk;
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
        public VirtualTexture maskVT { get; private set; }
        private NativeList<TerrainChunkBuffer> loadedBufferList;
        private static Vector4[] planes = new Vector4[6];
        private TerrainQuadTree tree;
        public TerrainQuadTree* treeRoot
        {
            get
            {
                return tree.Ptr();
            }
        }
        private JobHandle calculateHandle;
        private MStringBuilder msb;
        public VirtualTexture vt { get; private set; }
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
                cameraXZPos = (float3)cam.position,
                loadedBuffer = loadedBufferList,
                cameraDir = (float3)cam.forward
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

        bool CheckChunkEnabled(int2 startIndex, int size)
        {
            bool v = false;
            if (enabledChunk.Get(int3(startIndex, size), out v) && v)
            {
                return true;
            }
            return false;
        }

        void UpdateTexture(int2 startIndex, int size, int2 rootPos, float3 maskScaleOffset, int texElement)
        {
            rootPos += (int2)maskScaleOffset.yz;
            maskScaleOffset.yz = frac(maskScaleOffset.yz);
            textureShader.SetInt(ShaderIDs._OffsetIndex, texElement);
            textureShader.SetVector(ShaderIDs._IndexBuffer, float4(rootPos, 1, 1));
            textureShader.SetVector(ShaderIDs._IndexTextureSize, float4(MASK_RESOLUTION, min(255, terrainData.allMaterials.Length - 1), vt.indexSize));
            textureShader.SetVector(ShaderIDs._TextureSize, float4(maskScaleOffset, size * terrainData.materialTillingScale));
            const int disp = COLOR_RESOLUTION / 8;
            textureShader.Dispatch(0, disp, disp, 1);
        }

        void LoadTexture(VirtualTextureLoader.LoadingHandler handler, int2 startIndex, int size, int2 rootPos, float3 maskScaleOffset, int texElement)
        {
            shader.SetInt(ShaderIDs._OffsetIndex, texElement);
            textureBuffer.SetDataPtr((uint*)(handler.allBytes), HEIGHT_RESOLUTION * HEIGHT_RESOLUTION / 2);
            shader.Dispatch(3, HEIGHT_RESOLUTION / 8, HEIGHT_RESOLUTION / 8, 1);
            rootPos += (int2)maskScaleOffset.yz;
            maskScaleOffset.yz = frac(maskScaleOffset.yz);
            textureShader.SetInt(ShaderIDs._OffsetIndex, texElement);
            textureShader.SetVector(ShaderIDs._IndexBuffer, float4(rootPos, 1, 1));
            textureShader.SetVector(ShaderIDs._IndexTextureSize, float4(MASK_RESOLUTION, min(255, terrainData.allMaterials.Length - 1), vt.indexSize));
            textureShader.SetVector(ShaderIDs._TextureSize, float4(maskScaleOffset, size * terrainData.materialTillingScale));
            const int disp = COLOR_RESOLUTION / 8;
            textureShader.Dispatch(0, disp, disp, 1);
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
            //      textureShader.SetTexture(0, ShaderIDs._NoiseTexture, terrainData.warpNoiseTexture);
            textureShader.SetTexture(3, ShaderIDs._VirtualHeightmap, vt.GetTexture(0));
            textureShader.SetTexture(3, ShaderIDs._IndexTexture, vt.indexTex);
            textureShader.SetTexture(3, ShaderIDs._DestTex, worldNormalRT);
            textureShader.SetTexture(4, ShaderIDs._VirtualBumpMap, vt.GetTexture(2));
            textureShader.SetTexture(4, ShaderIDs._SourceTex, worldNormalRT);
            shader.SetInt(ShaderIDs._HeightResolution, HEIGHT_RESOLUTION);
            textureShader.SetInt(ShaderIDs._ColorResolution, COLOR_RESOLUTION);

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
                        else
                        {
                            Debug.LogError("No Enough Mask Position!");
                        }
                        maskTex = null;
                        arf.ReleaseAsset();
                    }
                    else
                    {
                        maskVT.UnloadTexture(maskCommand.pos);

                    }

                }
                TerrainUnloadData unloadData;
                while (unloadDataList.TryDequeue(out unloadData))
                {
                    switch (unloadData.ope)
                    {
                        case TerrainUnloadData.Operator.Unload:
                            vt.UnloadTexture(unloadData.startIndex);
                            break;
                        case TerrainUnloadData.Operator.Combine:
                            vt.CombineTexture(unloadData.startIndex, unloadData.size, false);
                            break;
                    }
                }

                if (vt.LeftedTextureElement < leastVirtualTextureLefted)
                {
                    leastVirtualTextureLefted = vt.LeftedTextureElement;
                    Debug.Log(leastVirtualTextureLefted);
                }
                if (loadDataList.TryDequeue(out loadData))
                {
                    int targetElement;
                    switch (loadData.ope)
                    {
                        case TerrainLoadData.Operator.Update:
                            targetElement = vt.GetChunkIndex(loadData.startIndex);
                            if(targetElement >= 0)
                            {
                                UpdateTexture(loadData.startIndex, loadData.size, loadData.rootPos, loadData.maskScaleOffset, targetElement);
                                ConvertNormalMap(loadData.startIndex, loadData.size, targetElement);
                                if (loadData.targetDecalLayer != 0)
                                {
                                    DrawDecal(loadData.startIndex, loadData.size, targetElement, loadData.targetDecalLayer);
                                    yield return null;
                                }
                            }
                            break;
                        case TerrainLoadData.Operator.Load:
                           
                            bool elementAva = vt.LoadNewTexture(loadData.startIndex, loadData.size, out targetElement) && CheckChunkEnabled(loadData.startIndex, loadData.size);
                            while (!GetComplete(ref loadData.handler0))
                                yield return null;
                            if (elementAva)
                            {
                                LoadTexture(loadData.handler0, loadData.startIndex, loadData.size, loadData.rootPos, loadData.maskScaleOffset, targetElement);
                                ConvertNormalMap(loadData.startIndex, loadData.size, targetElement);
                                if (loadData.targetDecalLayer != 0)
                                {
                                    DrawDecal(loadData.startIndex, loadData.size, targetElement, loadData.targetDecalLayer);
                                    yield return null;
                                }
                            }
                            loadData.handler0.Dispose();
                            break;
                        case TerrainLoadData.Operator.Separate:
                            int subSize = loadData.size / 2;
                            int2 leftDownIndex = loadData.startIndex;
                            int2 leftUpIndex = loadData.startIndex + int2(0, subSize);
                            int2 rightDownIndex = loadData.startIndex + int2(subSize, 0);
                            int2 rightUpIndex = loadData.startIndex + subSize;
                            int4 elements;
                            bool4 elementAvaliable;
                            yield return null;
                            while (!GetComplete(ref loadData.handler0))
                                yield return null;
                            while (!GetComplete(ref loadData.handler1))
                                yield return null;
                            while (!GetComplete(ref loadData.handler2))
                                yield return null;
                            while (!GetComplete(ref loadData.handler3))
                                yield return null;
                            if (vt.LeftedTextureElement >= 3)
                            {
                                vt.LoadQuadNewTextures(loadData.startIndex, subSize, out elements);
                                elementAvaliable.x = CheckChunkEnabled(leftDownIndex, subSize);
                                elementAvaliable.y = CheckChunkEnabled(leftUpIndex, subSize);
                                elementAvaliable.z = CheckChunkEnabled(rightDownIndex, subSize);
                                elementAvaliable.w = CheckChunkEnabled(rightUpIndex, subSize);
                                float subScale = loadData.maskScaleOffset.x;
                                float2 leftUpOffset = float2(loadData.maskScaleOffset.yz + float2(0, subScale));
                                float2 rightDownOffset = float2(loadData.maskScaleOffset.yz + float2(subScale, 0));
                                float2 rightUpOffset = float2(loadData.maskScaleOffset.yz + subScale);
                                if (elementAvaliable.x)
                                {
                                    LoadTexture(loadData.handler0, leftDownIndex, subSize, loadData.rootPos, loadData.maskScaleOffset, elements.x);
                                    ConvertNormalMap(leftDownIndex, subSize, elements.x);
                                }
                                if (elementAvaliable.y)
                                {
                                    LoadTexture(loadData.handler1, leftUpIndex, subSize, loadData.rootPos, float3(subScale, leftUpOffset), elements.y);
                                    ConvertNormalMap(leftUpIndex, subSize, elements.y);
                                }
                                if (elementAvaliable.z)
                                {
                                    LoadTexture(loadData.handler2, rightDownIndex, subSize, loadData.rootPos, float3(subScale, rightDownOffset), elements.z);
                                    ConvertNormalMap(rightDownIndex, subSize, elements.z);
                                }
                                if (elementAvaliable.w)
                                {
                                    LoadTexture(loadData.handler3, rightUpIndex, subSize, loadData.rootPos, float3(subScale, rightUpOffset), elements.w);
                                    ConvertNormalMap(rightUpIndex, subSize, elements.w);
                                }

                                if (loadData.targetDecalLayer != 0)
                                {
                                    if (elementAvaliable.x)
                                    {
                                        DrawDecal(leftDownIndex, subSize, elements.x, loadData.targetDecalLayer);
                                        yield return null;
                                    }
                                    if (elementAvaliable.y && CheckChunkEnabled(leftUpIndex, subSize))
                                    {
                                        DrawDecal(leftUpIndex, subSize, elements.y, loadData.targetDecalLayer);
                                        yield return null;
                                    }
                                    if (elementAvaliable.z && CheckChunkEnabled(rightDownIndex, subSize))
                                    {
                                        DrawDecal(rightDownIndex, subSize, elements.z, loadData.targetDecalLayer);
                                        yield return null;
                                    }
                                    if (elementAvaliable.w && CheckChunkEnabled(rightUpIndex, subSize))
                                    {
                                        DrawDecal(rightUpIndex, subSize, elements.w, loadData.targetDecalLayer);
                                        yield return null;
                                    }
                                }
                            }
                            loadData.handler0.Dispose();
                            loadData.handler1.Dispose();
                            loadData.handler2.Dispose();
                            loadData.handler3.Dispose();
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
            enabledChunk = new NativeDictionary<int3, bool, int3Equal>(500, Allocator.Persistent, new int3Equal());
            maskLoadList = new NativeQueue<MaskLoadCommand>(10, Allocator.Persistent);
            dispatchDrawBuffer = new ComputeBuffer(5, sizeof(int), ComputeBufferType.IndirectArguments);
            const int INIT_LENGTH = 500;
            culledResultsBuffer = new ComputeBuffer(INIT_LENGTH, sizeof(int));
            loadedBuffer = new ComputeBuffer(INIT_LENGTH, sizeof(TerrainChunkBuffer));
            loadedBufferList = new NativeList<TerrainChunkBuffer>(INIT_LENGTH, Allocator.Persistent);

            loader = new VirtualTextureLoader(lodOffset, terrainData.renderingLevelCount, terrainData.readWritePath, this);
            loadDataList = new NativeQueue<TerrainLoadData>(100, Allocator.Persistent);
            unloadDataList = new NativeQueue<TerrainUnloadData>(100, Allocator.Persistent);
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
            textureCapacity = terrainData.virtualTexCapacity;
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
            unloadDataList.Dispose();
            loader.Dispose();
            materialBuffer.Dispose();
            allLodLevles.Dispose();
            textureBuffer.Dispose();
            enabledChunk.Dispose();
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
            public double3 cameraXZPos;
            public double3 cameraDir;
            public NativeList<TerrainChunkBuffer> loadedBuffer;

            public void Execute()
            {

                tree->CheckUpdate(cameraXZPos, cameraDir);
                tree->PushDrawRequest(loadedBuffer);
            }
        }
    }
}