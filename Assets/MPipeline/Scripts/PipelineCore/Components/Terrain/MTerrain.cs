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
        public const int COLOR_RESOLUTION = 1024;
        public const int HEIGHT_RESOLUTION = 128;
        public const GraphicsFormat HEIGHT_FORMAT = GraphicsFormat.R16_UNorm;
        public MTerrainData terrainData;
        public VTDecalCamera decalCamera;
        #region QUADTREE
        [System.NonSerialized]
        public bool initializing;
        public NativeList_Float allLodLevles;
        private MTerrainLoadingThread loadingThread;
        public VirtualTextureLoader maskLoader;
        public VirtualTextureLoader heightLoader;
        [System.NonSerialized]
        public int lodOffset;
        [System.NonSerialized]
        public int textureCapacity;
        [System.NonSerialized]
        public int decalLayerOffset;
        public NativeQueue<TerrainLoadData> initializeLoadList;
        public NativeQueue<TerrainLoadData> loadDataList;
        public NativeQueue<MaskLoadCommand> maskLoadList;
        public NativeQueue<MaskLoadCommand> boundBoxLoadList;
        public struct Int2Equal : IFunction<int2, int2, bool>
        {
            public bool Run(ref int2 a, ref int2 b)
            {
                return a.x == b.x && a.y == b.y;
            }
        }
        public NativeDictionary<int2, MTerrainBoundingTree, Int2Equal> boundingDict;
        public FileStream boundingLoadStream;
        #endregion
        #region MESH_RENDERING
        private struct TerrainPoint
        {
            public float2 localCoord;
            public float2 coord;
        }

        private NativeList<TerrainDrawCommand> allDrawCommand;
        private RenderTexture cullingFlags;
        private int meshResolution;
        private ComputeBuffer meshBuffer;

        #endregion
        public PipelineCamera cam;
        private RenderTexture randomTileRT;
        private ComputeShader shader;
        private ComputeShader textureShader;
        private int largestChunkCount;
        private RenderTexture albedoTex;
        private RenderTexture normalTex;
        private RenderTexture smTex;
        //  private RenderTexture heightTex;
        private RenderTexture heightloadingCacheRT;
        private NativeArray<int> mipIDs;
        private NativeDictionary<int2, bool, Int2Equal> loadedChunk;
        public VirtualTexture maskVT { get; private set; }
        private static Vector4[] planes = new Vector4[6];
        private TerrainQuadTree tree;
        private int chunkCount;
        public TerrainQuadTree* treeRoot
        {
            get
            {
                return tree.Ptr();
            }
        }
        private JobHandle calculateHandle;
        private JobHandle boundingLoadHandle;
        private MStringBuilder msb;
        public VirtualTexture vt { get; private set; }
        private double oneVTPixelWorldLength;
        private ComputeBuffer materialBuffer;
        private NativeArray<int> vtContainer;
        private int leastVirtualTextureLefted = int.MaxValue;
        private double3 moveOffset;
        private void Awake()
        {
            moveOffset = 0;
        }

        public void MoveTerrain(double3 deltaOffset)
        {
            moveOffset += deltaOffset;
            if (terrainData)
            {
                terrainData.heightOffset += deltaOffset.y;
                terrainData.screenOffset += deltaOffset.xz;
            }
        }
        private void OnDestroy()
        {
            if (terrainData)
            {
                terrainData.heightOffset -= moveOffset.y;
                terrainData.screenOffset -= moveOffset.xz;
            }
        }
        public override void PrepareJob()
        {
            allDrawCommand.Clear();
            calculateHandle = new CalculateQuadTree
            {
                allDrawCommand = allDrawCommand,
                tree = tree.Ptr(),
                cameraXZPos = (float3)cam.transform.position,
                cameraDir = (float3)cam.transform.forward,
                camFrustumMax = cam.frustumMaxPoint,
                camFrustumMin = cam.frustumMinPoint,
                frustumPlanes = cam.frustumArray.unsafePtr
            }.Schedule();
            boundingLoadHandle = new LoadBoundBox().Schedule(boundingLoadHandle);
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
            vt.Update();
            maskVT.Update();
            CommandBuffer beforeBuffer = RenderPipeline.BeforeFrameBuffer;
            double2 pixelOffset = -terrainData.screenOffset / oneVTPixelWorldLength;
            beforeBuffer.SetGlobalVector(ShaderIDs._TerrainVTOffset, float4(
                FloorToInt(pixelOffset),
                (float2)frac(pixelOffset)
                ));
            beforeBuffer.SetGlobalFloat(ShaderIDs._TerrainWorldPosToVTUV, (float)(1.0 / oneVTPixelWorldLength));
        }

        void LoadTexture(int2 startIndex, int size, int2 rootPos, float3 maskScaleOffset, int texElement, CommandBuffer buffer)
        {
            double oneHeightPixelSize = size * oneVTPixelWorldLength / COLOR_RESOLUTION;

            buffer.SetComputeIntParam(textureShader, ShaderIDs._OffsetIndex, texElement);

            buffer.SetComputeVectorParam(textureShader, ShaderIDs._IndexBuffer, (float4)double4(rootPos, terrainData.heightScale, (2.0 / oneHeightPixelSize)));
            buffer.SetComputeVectorParam(textureShader, ShaderIDs._IndexTextureSize, float4(MASK_RESOLUTION, min(255, terrainData.allMaterials.Length - 1), vt.indexSize));
            buffer.SetComputeVectorParam(textureShader, ShaderIDs._TextureSize, (float4)double4(maskScaleOffset, size * terrainData.materialTillingScale));
            buffer.SetComputeVectorParam(textureShader, ShaderIDs._Offset, float4(startIndex % 256, 1, 1));
            const int disp = COLOR_RESOLUTION / 8;
            const int heightdisp = HEIGHT_RESOLUTION / 8;
            buffer.DispatchCompute(textureShader, 0, disp, disp, 1);
            buffer.SetComputeVectorParam(textureShader, ShaderIDs._IndexTextureSize, float4(float2(1) / maskVT.indexSize, maskVT.indexSize));
            buffer.SetComputeIntParam(textureShader, ShaderIDs._Count, COLOR_RESOLUTION + 2);

            int floatDisp = Mathf.CeilToInt((COLOR_RESOLUTION + 2f) / 8f);
            buffer.DispatchCompute(textureShader, 3, floatDisp, floatDisp, 1);
            buffer.DispatchCompute(textureShader, 4, disp, disp, 1);
            buffer.SetComputeIntParam(textureShader, ShaderIDs._Count, HEIGHT_RESOLUTION);
            buffer.DispatchCompute(textureShader, 7, heightdisp, heightdisp, 1);
        }
        public void GenerateMips(int targetElement, CommandBuffer buffer)
        {
            buffer.SetComputeIntParam(textureShader, ShaderIDs._OffsetIndex, targetElement);
            for (int i = 0; i < 3; ++i)
            {
                buffer.SetComputeTextureParam(textureShader, 5, mipIDs[0], vt.GetTexture(i), 0);
                buffer.SetComputeTextureParam(textureShader, 5, mipIDs[1], vt.GetTexture(i), 1);
                const int disp = COLOR_RESOLUTION / 16;
                buffer.DispatchCompute(textureShader, 5, disp, disp, 1);
            }
        }

        double2 VTPosToWorldPos(int2 vtPos)
        {
            double2 vtPosDouble = (double2)vtPos * oneVTPixelWorldLength;
            vtPosDouble += terrainData.screenOffset;
            return vtPosDouble;
        }
        void DrawDecal(int2 startIndex, int size, int targetElement, LayerMask decalCullingMask, int2 heightIndex)
        {
            double cameraSize = size * oneVTPixelWorldLength * 0.5;
            double2 centerPos = VTPosToWorldPos(startIndex) + cameraSize;
            decalCamera.renderingCommand.Add(new VTDecalCamera.CameraState
            {
                albedoRT = vt.GetTexture(0),
                normalRT = vt.GetTexture(1),
                smoRT = vt.GetTexture(2),
                cullingMask = decalCullingMask,
                depthSlice = targetElement,
                farClipPlane = (float)(terrainData.heightScale * 0.5),
                nearClipPlane = (float)(-terrainData.heightScale * 0.5),
                position = float3((float)centerPos.x, (float)(terrainData.heightOffset + terrainData.heightScale * 0.5), (float)centerPos.y),
                rotation = Quaternion.Euler(90, 0, 0),
                size = (float)cameraSize,
                heightRT = vt.GetTexture(3),
                startIndex = heightIndex
            });
        }
        IEnumerator AsyncLoader()
        {
            textureShader.SetTexture(1, ShaderIDs._VirtualMainTex, albedoTex);
            textureShader.SetTexture(1, ShaderIDs._VirtualBumpMap, normalTex);
            textureShader.SetTexture(1, ShaderIDs._VirtualSMO, smTex);
            //     textureShader.SetTexture(1, ShaderIDs._MaskTex, heightTex);
            textureShader.SetTexture(2, ShaderIDs._VirtualMainTex, albedoTex);
            textureShader.SetTexture(2, ShaderIDs._VirtualBumpMap, normalTex);
            textureShader.SetTexture(2, ShaderIDs._VirtualSMO, smTex);
            //    textureShader.SetTexture(2, ShaderIDs._MaskTex, heightTex);
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
            albedoTex.GenerateMips();
            smTex.GenerateMips();
            normalTex.GenerateMips();
            //   heightTex.GenerateMips();
            CommandBuffer buffer = RenderPipeline.BeforeFrameBuffer;
            Unity.Mathematics.Random r = new Unity.Mathematics.Random((uint)System.Guid.NewGuid().GetHashCode());
            buffer.SetComputeTextureParam(textureShader, 6, ShaderIDs._DestTex, randomTileRT);
            buffer.SetComputeVectorParam(textureShader, ShaderIDs._TextureSize, float4(float2(1.0 / 256), 1, 1));
            buffer.SetComputeVectorParam(textureShader, ShaderIDs._RandomSeed, r.NextFloat4() + float4(0, 0, 0.5f, 0.5f));
            buffer.DispatchCompute(textureShader, 6, 8, 8, 1);
            while (enabled)
            {
                buffer = RenderPipeline.BeforeFrameBuffer;
                buffer.SetComputeTextureParam(textureShader, 0, ShaderIDs._NoiseTillingTexture, randomTileRT);
                buffer.SetComputeBufferParam(textureShader, 0, ShaderIDs._MaterialBuffer, materialBuffer);
                buffer.SetComputeTextureParam(textureShader, 0, ShaderIDs._VirtualMainTex, vt.GetTexture(0));
                buffer.SetComputeTextureParam(textureShader, 0, ShaderIDs._VirtualBumpMap, vt.GetTexture(1));
                buffer.SetComputeTextureParam(textureShader, 0, ShaderIDs._VirtualSMO, vt.GetTexture(2));
                //  buffer.SetComputeTextureParam(textureShader, 0, ShaderIDs._HeightMap, heightTex);
                buffer.SetComputeTextureParam(textureShader, 0, ShaderIDs._MainTex, albedoTex);
                buffer.SetComputeTextureParam(textureShader, 0, ShaderIDs._BumpMap, normalTex);
                buffer.SetComputeTextureParam(textureShader, 0, ShaderIDs._SMMap, smTex);
                buffer.SetComputeTextureParam(textureShader, 0, ShaderIDs._NoiseTexture, terrainData.noiseTex);
                buffer.SetComputeIntParam(textureShader, ShaderIDs._HeightResolution, MASK_RESOLUTION);
                int rtID = maskVT.GetTextureFormat(0).rtPropertyID;
                buffer.SetComputeTextureParam(textureShader, 0, rtID, maskVT.GetTexture(0));
                buffer.SetComputeTextureParam(textureShader, 0, maskVT.indexTexID, maskVT.indexTex);
                buffer.SetComputeTextureParam(textureShader, 4, ShaderIDs._VirtualBumpMap, vt.GetTexture(1));
                buffer.SetComputeTextureParam(textureShader, 3, ShaderIDs._IndexTexture, maskVT.indexTex);
                buffer.SetComputeTextureParam(textureShader, 3, ShaderIDs._VirtualHeightmap, maskVT.GetTexture(1));
                buffer.SetComputeTextureParam(textureShader, 3, ShaderIDs._DestTex, heightloadingCacheRT);
                buffer.SetComputeTextureParam(textureShader, 4, ShaderIDs._SourceTex, heightloadingCacheRT);
                buffer.SetComputeTextureParam(textureShader, 7, ShaderIDs._IndexTexture, maskVT.indexTex);
                buffer.SetComputeTextureParam(textureShader, 7, ShaderIDs._VirtualHeightmap, maskVT.GetTexture(1));
                buffer.SetComputeTextureParam(textureShader, 7, vt.GetTextureFormat(3).rtPropertyID, vt.GetTexture(3));

                buffer.SetComputeIntParam(textureShader, ShaderIDs._ColorResolution, COLOR_RESOLUTION);
                buffer.SetGlobalVector(ShaderIDs._HeightScaleOffset, (float4)double4(terrainData.heightScale, terrainData.heightOffset, 1, 1));
                TerrainLoadData loadData;
                MaskLoadCommand maskCommand;
                int targetElement;
                while (maskLoadList.TryDequeue(out maskCommand))
                {
                    if (maskCommand.load)
                    {
                        int maskEle;
                        if (maskVT.LoadNewTexture(maskCommand.pos, 1, out maskEle))
                        {
                            VirtualTextureLoader.MaskBuffer maskLoadBuffer = maskLoader.ScheduleLoadingJob(maskCommand.pos);
                            VirtualTextureLoader.MaskBuffer heightLoadBuffer = heightLoader.ScheduleLoadingJob(maskCommand.pos);
                            loadingThread.Schedule();
                            const int frameSepar = (MASK_RESOLUTION / 2048) * (MASK_RESOLUTION / 2048);
                            yield return maskLoader.ReadToTexture(maskVT.GetTexture(0), maskEle, maskLoadBuffer, frameSepar);
                            yield return heightLoader.ReadToTexture(maskVT.GetTexture(1), maskEle, heightLoadBuffer, frameSepar * 2);
                        }
                        else
                        {
                            Debug.LogError("No Enough Mask Position!");
                        }
                        loadedChunk[maskCommand.pos] = true;
                    }
                    else
                    {
                        maskVT.UnloadTexture(maskCommand.pos, RenderPipeline.BeforeFrameBuffer);
                        loadedChunk.Remove(maskCommand.pos);
                    }

                }

                int initializedListLength = initializeLoadList.Length;
                if (initializedListLength > 0)
                {
                    NativeArray<int> allTextureElements = new NativeArray<int>(initializedListLength, Allocator.Persistent);

                    for (int i = 0; i < initializedListLength; ++i)
                    {
                        if (initializeLoadList.TryDequeue(out loadData))
                        {
                            switch (loadData.ope)
                            {
                                case TerrainLoadData.Operator.Load:
                                    bool elementAva = vt.LoadNewTexture(loadData.startIndex, loadData.size, out targetElement);

                                    if (elementAva)
                                    {
                                        LoadTexture(loadData.startIndex, loadData.size, loadData.rootPos, loadData.maskScaleOffset, targetElement, RenderPipeline.BeforeFrameBuffer);

                                    }

                                    break;

                            }
                            initializeLoadList.Add(loadData);

                        }
                    }
                    int elementCount = 0;
                    while (initializeLoadList.TryDequeue(out loadData))
                    {
                        switch (loadData.ope)
                        {
                            case TerrainLoadData.Operator.Load:
                                targetElement = allTextureElements[elementCount];
                                elementCount++;
                                if (targetElement >= 0)
                                {

                                    if (loadData.targetDecalLayer != 0)
                                    {
                                        DrawDecal(loadData.startIndex, loadData.size, targetElement, loadData.targetDecalLayer, loadData.rootPos);
                                        yield return null;
                                    }
                                }
                                break;
                        }
                    }
                    allTextureElements.Dispose();
                }


                if (vt.LeftedTextureElement < leastVirtualTextureLefted)
                {
                    leastVirtualTextureLefted = vt.LeftedTextureElement;
                    Debug.Log(leastVirtualTextureLefted);
                }
                if (loadDataList.TryDequeue(out loadData))
                {

                    switch (loadData.ope)
                    {
                        case TerrainLoadData.Operator.Update:
                            targetElement = vt.GetChunkIndex(loadData.startIndex);
                            if (targetElement >= 0)
                            {
                                LoadTexture(loadData.startIndex, loadData.size, loadData.rootPos, loadData.maskScaleOffset, targetElement, RenderPipeline.BeforeFrameBuffer);
                                DrawDecal(loadData.startIndex, loadData.size, targetElement, loadData.targetDecalLayer, loadData.rootPos);
                            }
                            break;
                        case TerrainLoadData.Operator.Load:

                            bool elementAva = vt.LoadNewTexture(loadData.startIndex, loadData.size, out targetElement);

                            if (elementAva)
                            {
                                LoadTexture(loadData.startIndex, loadData.size, loadData.rootPos, loadData.maskScaleOffset, targetElement, RenderPipeline.BeforeFrameBuffer);
                                DrawDecal(loadData.startIndex, loadData.size, targetElement, loadData.targetDecalLayer, loadData.rootPos);
                            }
                            else
                            {
                                Debug.LogError("Virtual Texture No Capacity in loading  " + textureCapacity);
                            }
                            yield return null;
                            break;
                        case TerrainLoadData.Operator.Separate:
                            int subSize = loadData.size / 2;
                            int2 leftDownIndex = loadData.startIndex;
                            int2 leftUpIndex = loadData.startIndex + int2(0, subSize);
                            int2 rightDownIndex = loadData.startIndex + int2(subSize, 0);
                            int2 rightUpIndex = loadData.startIndex + subSize;
                            if (vt.LeftedTextureElement >= 3)
                            {
                                vt.LoadNewTextureChunks(loadData.startIndex, subSize, 2, vtContainer);
                                float subScale = loadData.maskScaleOffset.x;
                                float2 leftUpOffset = float2(loadData.maskScaleOffset.yz + float2(0, subScale));
                                float2 rightDownOffset = float2(loadData.maskScaleOffset.yz + float2(subScale, 0));
                                float2 rightUpOffset = float2(loadData.maskScaleOffset.yz + subScale);
                                float3 leftDownScaleOffset = loadData.maskScaleOffset;
                                float3 leftUpScaleOffset = float3(subScale, leftUpOffset);
                                float3 rightDownScaleOffset = float3(subScale, rightDownOffset);
                                float3 rightUpScaleOffset = float3(subScale, rightUpOffset);
                                LoadTexture(leftDownIndex, subSize, loadData.rootPos, leftDownScaleOffset, vtContainer[0], RenderPipeline.BeforeFrameBuffer);
                                LoadTexture(leftUpIndex, subSize, loadData.rootPos, leftUpScaleOffset, vtContainer[2], RenderPipeline.BeforeFrameBuffer);
                                LoadTexture(rightDownIndex, subSize, loadData.rootPos, rightDownScaleOffset, vtContainer[1], RenderPipeline.BeforeFrameBuffer);
                                LoadTexture(rightUpIndex, subSize, loadData.rootPos, rightUpScaleOffset, vtContainer[3], RenderPipeline.BeforeFrameBuffer);
                                DrawDecal(leftDownIndex, subSize, vtContainer[0], loadData.targetDecalLayer, loadData.rootPos);
                                DrawDecal(leftUpIndex, subSize, vtContainer[2], loadData.targetDecalLayer, loadData.rootPos);
                                DrawDecal(rightDownIndex, subSize, vtContainer[1], loadData.targetDecalLayer, loadData.rootPos);
                                DrawDecal(rightUpIndex, subSize, vtContainer[3], loadData.targetDecalLayer, loadData.rootPos);
                            }
                            else
                            {
                                Debug.LogError("Virtual Texture No Capacity  in separate   " + textureCapacity + "  " + vt.LeftedTextureElement);
                            }
                            yield return null;
                            break;
                        case TerrainLoadData.Operator.Unload:
                            vt.UnloadTexture(loadData.startIndex, RenderPipeline.BeforeFrameBuffer);
                            break;
                        case TerrainLoadData.Operator.Combine:
                            subSize = loadData.size / 2;
                            leftDownIndex = loadData.startIndex;
                            leftUpIndex = loadData.startIndex + int2(0, subSize);
                            rightDownIndex = loadData.startIndex + int2(subSize, 0);
                            rightUpIndex = loadData.startIndex + subSize;
                            targetElement = vt.CombineQuadTextures(leftDownIndex, rightDownIndex, leftUpIndex, rightUpIndex, leftDownIndex, loadData.size, RenderPipeline.BeforeFrameBuffer);
                            GenerateMips(targetElement, RenderPipeline.BeforeFrameBuffer);

                            yield return null;
                            break;
                    }
                }
                else yield return null;
            }
        }

        public void SaveMask()
        {
            foreach (var i in maskVT.PoolDict)
            {
                maskLoader.WriteToDisk(maskVT.GetTexture(0), i.value.y, i.key);
                heightLoader.WriteToDisk(maskVT.GetTexture(1), i.value.y, i.key);
            }
        }
        private void InitializeMeshData()
        {
            NativeArray<TerrainPoint> allPoints = new NativeArray<TerrainPoint>(meshResolution * meshResolution * 6, Allocator.Temp);

            meshBuffer = new ComputeBuffer(allPoints.Length, sizeof(TerrainPoint));
            for (int x = 0, count = 0; x < meshResolution; x++)
                for (int y = 0; y < meshResolution; y++)
                {
                    TerrainPoint tp;
                    tp.coord = float2(x, y);
                    tp.localCoord = 0;
                    allPoints[count] = tp;
                    tp.localCoord = float2(0, 1);
                    allPoints[count + 1] = tp;
                    tp.localCoord = float2(1, 0);
                    allPoints[count + 2] = tp;
                    tp.localCoord = float2(0, 1);
                    allPoints[count + 3] = tp;
                    tp.localCoord = 1;
                    allPoints[count + 4] = tp;
                    tp.localCoord = float2(1, 0);
                    allPoints[count + 5] = tp;
                    count += 6;
                }
            meshBuffer.SetData(allPoints);
            allPoints.Dispose();
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
            if (!cam || !decalCamera)
            {
                enabled = false;
                Debug.LogError("No Decal Camera!");
                return;
            }
            initializing = true;
            lodOffset = terrainData.GetLodOffset();
            largestChunkCount = (int)(0.1 + pow(2.0, lodOffset));
            msb = new MStringBuilder(32);
            oneVTPixelWorldLength = terrainData.VTTexelLength();
            textureShader = Resources.Load<ComputeShader>("ProceduralTexture");
            shader = Resources.Load<ComputeShader>("TerrainCompute");
            current = this;
            int indexMapSize = 1;
            for (int i = 1; i < terrainData.lodDistances.Length; ++i)
            {
                indexMapSize *= 2;
            }
            vtContainer = new NativeArray<int>(4, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            maskLoadList = new NativeQueue<MaskLoadCommand>(10, Allocator.Persistent);
            ComputeShader editShader = Resources.Load<ComputeShader>("TerrainEdit");
            loadingThread = new MTerrainLoadingThread(10);
            maskLoader = new VirtualTextureLoader(terrainData.maskmapPath, editShader, largestChunkCount, MASK_RESOLUTION, false, loadingThread);
            heightLoader = new VirtualTextureLoader(terrainData.heightmapPath, editShader, largestChunkCount, MASK_RESOLUTION, true, loadingThread);
            loadedChunk = new NativeDictionary<int2, bool, Int2Equal>(100, Allocator.Persistent, new Int2Equal());
            loadDataList = new NativeQueue<TerrainLoadData>(100, Allocator.Persistent);
            initializeLoadList = new NativeQueue<TerrainLoadData>(100, Allocator.Persistent);
            NativeArray<uint> dispatchDraw = new NativeArray<uint>(5, Allocator.Temp, NativeArrayOptions.ClearMemory);
            dispatchDraw[0] = 6;
            VirtualTextureFormat* formats = stackalloc VirtualTextureFormat[]
            {
                new VirtualTextureFormat((VirtualTextureSize)COLOR_RESOLUTION, GraphicsFormat.R8G8B8A8_UNorm, "_VirtualMainTex", 2),
                new VirtualTextureFormat((VirtualTextureSize)COLOR_RESOLUTION, GraphicsFormat.R16G16_SNorm, "_VirtualBumpMap", 2),
                new VirtualTextureFormat((VirtualTextureSize)COLOR_RESOLUTION, GraphicsFormat.R8G8_UNorm, "_VirtualSMMap", 2),
                new VirtualTextureFormat((VirtualTextureSize)HEIGHT_RESOLUTION, HEIGHT_FORMAT, "_VirtualDisplacement", 0)
            };
            mipIDs = new NativeArray<int>(2, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            mipIDs[0] = Shader.PropertyToID("_Mip0");
            mipIDs[1] = Shader.PropertyToID("_Mip1");
            chunkCount = (int)(pow(2.0, terrainData.lodDistances.Length - 1) + 0.1);
            vt = new VirtualTexture(terrainData.virtualTexCapacity, min(2048, chunkCount), formats, 4, "_TerrainVTIndexTex");
            textureCapacity = terrainData.virtualTexCapacity;
            VirtualTextureFormat* maskFormats = stackalloc VirtualTextureFormat[]
            {
                new VirtualTextureFormat((VirtualTextureSize)MASK_RESOLUTION, GraphicsFormat.R8_UNorm, "_VirtualMaskmap"),
                new VirtualTextureFormat((VirtualTextureSize)MASK_RESOLUTION, GraphicsFormat.R16_UNorm, "_VirtualHeightmap"),
            };
            maskVT = new VirtualTexture(terrainData.heightmapTexCapacity, largestChunkCount, maskFormats, 2, "_MaskIndexMap");
            maskVT.GetTexture(0).filterMode = FilterMode.Point;
            maskVT.GetTexture(1).filterMode = FilterMode.Point;
            vt.GetTexture(0).filterMode = FilterMode.Trilinear;
            vt.GetTexture(1).filterMode = FilterMode.Trilinear;
            vt.GetTexture(2).filterMode = FilterMode.Trilinear;
            vt.GetTexture(3).filterMode = FilterMode.Bilinear;
            allLodLevles = new NativeList_Float(terrainData.lodDistances.Length, Allocator.Persistent);
            for (int i = 0; i < terrainData.lodDistances.Length; ++i)
            {
                allLodLevles.Add((float)min(terrainData.lodDistances[max(0, i - 1)], terrainData.lodDistances[i]));
            }
            allLodLevles[terrainData.lodDistances.Length] = 0;
            meshResolution = terrainData.GetMeshResolution();
            cullingFlags = new RenderTexture(new RenderTextureDescriptor
            {
                graphicsFormat = GraphicsFormat.R8_UNorm,
                dimension = TextureDimension.Tex2D,
                width = meshResolution,
                height = meshResolution,
                volumeDepth = 1,
                enableRandomWrite = true,
                msaaSamples = 1,
                useMipMap = false
            });
            cullingFlags.filterMode = FilterMode.Point;
            cullingFlags.Create();
            albedoTex = new RenderTexture(new RenderTextureDescriptor
            {
                graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm,
                dimension = TextureDimension.Tex2DArray,
                width = COLOR_RESOLUTION,
                height = COLOR_RESOLUTION,
                volumeDepth = Mathf.Max(1, terrainData.textures.Length),
                enableRandomWrite = true,
                msaaSamples = 1,
                autoGenerateMips = false,
                useMipMap = true,
                mipCount = 6,
            });
            albedoTex.Create();
            normalTex = new RenderTexture(new RenderTextureDescriptor
            {
                graphicsFormat = GraphicsFormat.R16G16_SNorm,
                dimension = TextureDimension.Tex2DArray,
                width = COLOR_RESOLUTION,
                height = COLOR_RESOLUTION,
                autoGenerateMips = false,
                useMipMap = true,
                volumeDepth = Mathf.Max(1, terrainData.textures.Length),
                enableRandomWrite = true,
                msaaSamples = 1,
                mipCount = 6,
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
                useMipMap = true,
                autoGenerateMips = false,
                mipCount = 6,
                depthBufferBits = 0,
                useDynamicScale = false
            });
            smTex.Create();
            /*   heightTex = new RenderTexture(new RenderTextureDescriptor
               {
                   graphicsFormat = GraphicsFormat.R8_UNorm,
                   dimension = TextureDimension.Tex2DArray,
                   width = COLOR_RESOLUTION,
                   height = COLOR_RESOLUTION,
                   volumeDepth = Mathf.Max(1, terrainData.textures.Length),
                   enableRandomWrite = true,
                   msaaSamples = 1,
                   useMipMap = true,
                   autoGenerateMips = false,
                   mipCount = 6,
                   depthBufferBits = 0,
                   useDynamicScale = false
               });*/
            heightloadingCacheRT = new RenderTexture(new RenderTextureDescriptor
            {
                width = COLOR_RESOLUTION + 2,
                height = COLOR_RESOLUTION + 2,
                volumeDepth = 1,
                enableRandomWrite = true,
                dimension = TextureDimension.Tex2D,
                graphicsFormat = GraphicsFormat.R16_UNorm,
                msaaSamples = 1,
                useMipMap = false,
                autoGenerateMips = false,
                mipCount = 0,
                depthBufferBits = 0,
                useDynamicScale = false,
            });
            randomTileRT = new RenderTexture(256, 256, 0, GraphicsFormat.R16G16B16A16_SNorm, 0);
            randomTileRT.enableRandomWrite = true;
            randomTileRT.wrapMode = TextureWrapMode.Repeat;
            randomTileRT.filterMode = FilterMode.Point;
            randomTileRT.Create();

            heightloadingCacheRT.Create();
            //    heightTex.Create();
            smTex.wrapMode = TextureWrapMode.Repeat;
            normalTex.wrapMode = TextureWrapMode.Repeat;
            albedoTex.wrapMode = TextureWrapMode.Repeat;
            //   heightTex.wrapMode = TextureWrapMode.Repeat;
            boundBoxLoadList = new NativeQueue<MaskLoadCommand>(10, Allocator.Persistent);
            boundingLoadStream = new FileStream(terrainData.boundPath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            boundingDict = new NativeDictionary<int2, MTerrainBoundingTree, Int2Equal>(20, Allocator.Persistent, new Int2Equal());
            InitializeMeshData();
            allDrawCommand = new NativeList<TerrainDrawCommand>(20, Allocator.Persistent);
            materialBuffer = new ComputeBuffer(max(1, terrainData.allMaterials.Length), sizeof(MTerrainData.HeightBlendMaterial));
            materialBuffer.SetData(terrainData.allMaterials);
            decalLayerOffset = max(0, terrainData.lodDistances.Length - terrainData.allDecalLayers.Length);
            tree = new TerrainQuadTree(-1, TerrainQuadTree.LocalPos.LeftDown, 0, 0, terrainData.largestChunkSize, double3(1, 0, 0), 0);

            StartCoroutine(AsyncLoader());
        }


        public void DrawTerrain(CommandBuffer buffer, int pass, Vector4[] planes, float3 frustumMinPoint, float3 frustumMaxPoint)
        {
            foreach (var i in allDrawCommand)
            {
                if (loadedChunk[i.rootPos])
                {
                    buffer.SetComputeIntParam(shader, ShaderIDs._Count, meshResolution);
                    buffer.SetComputeVectorParam(shader, ShaderIDs._StartPos, float4(i.startPos, (float)oneVTPixelWorldLength, 1));
                    buffer.SetComputeVectorArrayParam(shader, ShaderIDs.planes, planes);
                    buffer.SetComputeVectorParam(shader, ShaderIDs._HeightScaleOffset, (float4)double4(terrainData.heightScale, terrainData.heightOffset, 1, 1));
                    buffer.SetComputeVectorParam(shader, ShaderIDs._FrustumMaxPoint, float4(frustumMaxPoint, 1));
                    buffer.SetComputeVectorParam(shader, ShaderIDs._FrustumMinPoint, float4(frustumMinPoint, 1));
                    buffer.SetComputeTextureParam(shader, 0, ShaderIDs._CullingTexture, cullingFlags);
                    int dispCount = meshResolution / 8;
                    buffer.DispatchCompute(shader, 0, dispCount, dispCount, 1);
                    int lastElement = clamp(terrainData.lodDistances.Length - 6, 0, terrainData.lodDistances.Length - 1);
                    buffer.SetGlobalVector(ShaderIDs._HeightScaleOffset, (float4)double4(terrainData.heightScale, terrainData.heightOffset, i.rootPos));
                    buffer.SetGlobalVector(ShaderIDs._TessellationFactors, float4(allLodLevles[terrainData.lodDistances.Length - 1], allLodLevles[lastElement], 0, 0));
                    buffer.SetGlobalBuffer(ShaderIDs.verticesBuffer, meshBuffer);
                    buffer.SetGlobalVector(ShaderIDs._StartPos, float4(i.startPos, (float)oneVTPixelWorldLength, meshResolution));
                    buffer.SetGlobalVector(ShaderIDs._TextureSize, float4(i.startVTIndex, 0, chunkCount - 0.5f));
                    buffer.SetGlobalTexture(ShaderIDs._CullingTexture, cullingFlags);
                    buffer.DrawProcedural(Matrix4x4.identity, terrainData.drawTerrainMaterial, pass, MeshTopology.Triangles, meshBuffer.count);
                }
            }
        }

        public void DrawTerrain(CommandBuffer buffer, int pass, float4* planePtr, float3 frustumMinPoint, float3 frustumMaxPoint)
        {
            UnsafeUtility.MemCpy(planes.Ptr(), planePtr, sizeof(float4) * 6);
            DrawTerrain(buffer, pass, planes, frustumMinPoint, frustumMaxPoint);
        }

        protected override void OnDisableFunc()
        {
            if (current != this) return;
            boundingLoadHandle.Complete();
            tree.Dispose();
            current = null;
            vt.Dispose();
            boundingDict.Dispose();
            maskVT.Dispose();
            boundBoxLoadList.Dispose();
            loadDataList.Dispose();
            initializeLoadList.Dispose();
            materialBuffer.Dispose();
            allLodLevles.Dispose();
            maskLoader.Dispose();
            heightLoader.Dispose();
            maskLoadList.Dispose();
            vtContainer.Dispose();
            loadedChunk.Dispose();
            Destroy(albedoTex);
            Destroy(cullingFlags);
            Destroy(normalTex);
            Destroy(smTex);
            //    DestroyImmediate(heightTex);
            Destroy(heightloadingCacheRT);
            Destroy(randomTileRT);
            meshBuffer.Dispose();
            allDrawCommand.Dispose();
            mipIDs.Dispose();
            loadingThread.Dispose();
            boundingLoadStream.Dispose();
        }

        private struct CalculateQuadTree : IJob
        {
            [NativeDisableUnsafePtrRestriction]
            public TerrainQuadTree* tree;
            public double3 cameraXZPos;
            public double3 cameraDir;
            [NativeDisableUnsafePtrRestriction]
            public float4* frustumPlanes;
            public float3 camFrustumMin;
            public float3 camFrustumMax;
            public NativeList<TerrainDrawCommand> allDrawCommand;

            public void Execute()
            {
                double2 heightScaleOffset = double2(current.terrainData.heightScale, current.terrainData.heightOffset);
                tree->UpdateData(cameraXZPos, cameraDir, heightScaleOffset, camFrustumMin, camFrustumMax, frustumPlanes);
                tree->CombineUpdate();
                tree->SeparateUpdate();
                if (current.initializing)
                {
                    tree->InitializeRenderingCommand();
                    current.initializing = false;
                }
                tree->PushDrawRequest(allDrawCommand);
            }
        }

        private struct LoadBoundBox : IJob
        {
            public void Execute()
            {
                MaskLoadCommand loadCommand;
                lock (current)
                {
                    if (!current.boundBoxLoadList.TryDequeue(out loadCommand))
                    {
                        return;
                    }
                    if (loadCommand.load)
                        current.boundingDict[loadCommand.pos] = new MTerrainBoundingTree(current.terrainData.renderingLevelCount);
                    else
                    {
                        current.boundingDict[loadCommand.pos].Dispose();
                        current.boundingDict.Remove(loadCommand.pos);
                    }
                }

                if (loadCommand.load)
                {
                    current.boundingDict[loadCommand.pos].ReadFromDisk(current.boundingLoadStream, current.largestChunkCount * loadCommand.pos.y + loadCommand.pos.x);
                }
            }
        }
    }
}