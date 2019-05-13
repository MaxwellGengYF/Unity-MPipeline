using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Jobs;
using System.Threading;
using Unity.Collections;
using UnityEngine.Jobs;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using System;

using static Unity.Mathematics.math;
namespace MPipeline
{
    [CreateAssetMenu(menuName = "GPURP Events/Lighting")]
    [RequireEvent(typeof(PropertySetEvent))]
    public unsafe sealed class LightingEvent : PipelineEvent
    {
        public float cbdrDistance = 500;
        public float shadowDistance = 50;
        public LayerMask localLightShadowLayer;
        #region DIR_LIGHT
        private static int[] _Count = new int[2];
        private Matrix4x4[] cascadeShadowMapVP = new Matrix4x4[SunLight.CASCADELEVELCOUNT];
        private Vector4[] shadowFrustumVP = new Vector4[6];
        private struct ShadowAvaliable
        {
            public UIntPtr mlight;
            public bool isAvaliable;
        }
        private NativeList<ShadowAvaliable> needCheckedShadows;
        public CBDRSharedData cbdr;
        #endregion
        #region POINT_LIGHT
        private Material cubeDepthMaterial;
        private RenderSpotShadowCommand spotBuffer;
        private NativeList<CubemapViewProjMatrix> cubemapVPMatrices;
        private NativeArray<PointLightStruct> pointLightArray;
        private NativeArray<SpotLight> spotLightArray;
        private NativeList<SpotLightMatrix> spotLightMatrices;
        private List<Light> addMLightCommandList = new List<Light>(30);
        private List<Light> allLights = new List<Light>(30);
        private JobHandle lightingHandle;
        private JobHandle csmHandle;
        private JobHandle shadowCullHandle;
        private ShadowAvaliableCheck shadowCheckJob;
        private CascadeShadowmap csmStruct;
        private StaticFit staticFit;
        private float* clipDistances;
        private OrthoCam* sunShadowCams;
        private Material lightingMaterial;
        
        private PropertySetEvent proper;

        public override bool CheckProperty()
        {
            if (!cbdr.CheckAvailiable())
            {
                try
                {
                    cbdr.Dispose();
                }
                catch { }
                return false;
            }
            return cubeDepthMaterial;
        }
        #endregion
        private RenderTexture pointLightTileIndices;
        private RenderTexture spotLightTileIndices;
        private int[] tileSizeArray = new int[2];
        private Material minMaxBoundMat;
        private const bool useTBDR = false;
        protected override void Init(PipelineResources resources)
        {
            proper = RenderPipeline.GetEvent<PropertySetEvent>();
            if (useTBDR)
                minMaxBoundMat = new Material(resources.shaders.minMaxDepthBounding);
            needCheckedShadows = new NativeList<ShadowAvaliable>(20, Allocator.Persistent);
            cbdr = new CBDRSharedData(resources);
            for (int i = 0; i < cascadeShadowMapVP.Length; ++i)
            {
                cascadeShadowMapVP[i] = Matrix4x4.identity;
            }
            cubeDepthMaterial = new Material(resources.shaders.cubeDepthShader);
            spotBuffer = new RenderSpotShadowCommand();
            spotBuffer.Init(resources.shaders.spotLightDepthShader);
            lightingMaterial = new Material(resources.shaders.lightingShader);
        }

        protected override void Dispose()
        {
            needCheckedShadows.Dispose();
            DestroyImmediate(cubeDepthMaterial);
            DestroyImmediate(lightingMaterial);
            spotBuffer.Dispose();
            cbdr.Dispose();
        }
        private static StaticFit DirectionalShadowStaticFit(Camera cam, SunLight sunlight, float* outClipDistance)
        {
            StaticFit staticFit;
            staticFit.resolution = sunlight.resolution;
            staticFit.mainCamTrans = cam;
            staticFit.frustumCorners = new NativeArray<float3>((SunLight.CASCADELEVELCOUNT + 1) * 4, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            outClipDistance[0] = cam.nearClipPlane;
            outClipDistance[1] = sunlight.firstLevelDistance;
            outClipDistance[2] = sunlight.secondLevelDistance;
            outClipDistance[3] = sunlight.thirdLevelDistance;
            outClipDistance[4] = sunlight.farestDistance;
            return staticFit;
        }
        protected override void OnDisable()
        {
            RenderPipeline.ExecuteBufferAtFrameEnding((buffer) =>
            {
                buffer.DisableShaderKeyword("ENABLE_SUN");
                buffer.DisableShaderKeyword("SPOTLIGHT");
                buffer.DisableShaderKeyword("POINTLIGHT");
            });
        }
        public override void PreRenderFrame(PipelineCamera cam, ref PipelineCommandData data)
        {
            float shadowDist = clamp(shadowDistance, 0, cam.cam.farClipPlane);
            if (SunLight.current && SunLight.current.enabled && SunLight.current.gameObject.activeSelf)
            {
                data.buffer.EnableShaderKeyword("ENABLE_SUN");
                data.buffer.SetKeyword("ENABLE_SUNSHADOW", SunLight.current.enableShadow);
            }
            else
            {
                data.buffer.DisableShaderKeyword("ENABLE_SUN");
            }
            var visLights = proper.cullResults.visibleLights;
            LightFilter.allVisibleLight = visLights.Ptr();
            allLights.Clear();
            foreach (var i in visLights)
            {
                allLights.Add(i.light);
            }
            shadowCheckJob.Init(cam.cam, shadowDistance);
            shadowCullHandle = shadowCheckJob.ScheduleRef();
            addMLightCommandList.Clear();
            LightFilter.allMLightCommandList = addMLightCommandList;
            pointLightArray = new NativeArray<PointLightStruct>(visLights.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            spotLightArray = new NativeArray<SpotLight>(visLights.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            cubemapVPMatrices = new NativeList<CubemapViewProjMatrix>(CBDRSharedData.MAXIMUMPOINTLIGHTCOUNT, Allocator.Temp);
            spotLightMatrices = new NativeList<SpotLightMatrix>(CBDRSharedData.MAXIMUMSPOTLIGHTCOUNT, Allocator.Temp);
            LightFilter.allLights = allLights;
            LightFilter.pointLightArray = pointLightArray;
            LightFilter.spotLightArray = spotLightArray;
            LightFilter.cubemapVPMatrices = cubemapVPMatrices;
            LightFilter.spotLightMatrices = spotLightMatrices;
            Transform camTrans = cam.cam.transform;
            needCheckedShadows.Clear();
            needCheckedShadows.AddCapacityTo(allLights.Count);
            lightingHandle = (new LightFilter
            {
                camPos = cam.cam.transform.position,
                shadowDist = shadowDist,
                lightDist = cbdrDistance,
                needCheckLight = needCheckedShadows
            }).Schedule(allLights.Count, 1, shadowCullHandle);
            if (SunLight.current != null && SunLight.current.enabled && SunLight.current.enableShadow)
            {
                clipDistances = (float*)UnsafeUtility.Malloc(SunLight.CASCADECLIPSIZE * sizeof(float), 16, Allocator.Temp);
                staticFit = DirectionalShadowStaticFit(cam.cam, SunLight.current, clipDistances);
                sunShadowCams = MUnsafeUtility.Malloc<OrthoCam>(SunLight.CASCADELEVELCOUNT * sizeof(OrthoCam), Allocator.Temp);
                Matrix4x4 proj = cam.cam.projectionMatrix;
                //      cam.cam.projectionMatrix = cam.cam.nonJitteredProjectionMatrix;
                PipelineFunctions.GetfrustumCorners(clipDistances, SunLight.CASCADELEVELCOUNT + 1, cam.cam, staticFit.frustumCorners.Ptr());
                //   cam.cam.projectionMatrix = proj;
                csmStruct = new CascadeShadowmap
                {
                    cascadeShadowmapVPs = (float4x4*)cascadeShadowMapVP.Ptr(),
                    results = sunShadowCams,
                    orthoCam = (OrthoCam*)UnsafeUtility.AddressOf(ref SunLight.current.shadCam),
                    farClipPlane = SunLight.current.farestZ,
                    frustumCorners = staticFit.frustumCorners.Ptr(),
                    resolution = staticFit.resolution,
                    isD3D = GraphicsUtility.platformIsD3D
                };
                csmHandle = csmStruct.ScheduleRefBurst(SunLight.CASCADELEVELCOUNT, 1);
            }
        }
        public override void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data)
        {
            RenderTargetIdentifier source, dest;
            PipelineFunctions.RunPostProcess(ref cam.targets, out source, out dest);
            data.buffer.BlitSRT(source, dest, ShaderIDs._DepthBufferTexture, lightingMaterial, 0);
            data.buffer.BlitSRT(source, dest, ShaderIDs._DepthBufferTexture, lightingMaterial, 1);
            //Calculate CBDR
            DirLight(cam, ref data);
            PointLight(cam, ref data);
            //Calculate Lighting
            data.buffer.BlitSRTWithDepth(cam.targets.renderTargetIdentifier, ShaderIDs._DepthBufferTexture, lightingMaterial, 2);
            LightFilter.Clear();
        }
        private void DirLight(PipelineCamera cam, ref PipelineCommandData data)
        {
            if (SunLight.current == null || !SunLight.current.enabled)
            {
                return;
            }
            cbdr.lightFlag |= 0b100;
            if (SunLight.current.enableShadow)
                cbdr.lightFlag |= 0b010;
            CommandBuffer buffer = data.buffer;
            if (SunLight.current.enableShadow)
            {
                RenderClusterOptions opts = new RenderClusterOptions
                {
                    frustumPlanes = shadowFrustumVP,
                    command = buffer,
                    cullingShader = data.resources.shaders.gpuFrustumCulling,
                };
                buffer.SetGlobalVector(ShaderIDs._ShadowDisableDistance, new Vector4(SunLight.current.firstLevelDistance,
                    SunLight.current.secondLevelDistance,
                    SunLight.current.thirdLevelDistance,
                    SunLight.current.farestDistance));//Only Mask
                buffer.SetGlobalVector(ShaderIDs._SoftParam, SunLight.current.cascadeSoftValue / SunLight.current.resolution);
                csmHandle.Complete();
                SceneController.DrawDirectionalShadow(cam, ref staticFit, ref data, ref opts, clipDistances, sunShadowCams, cascadeShadowMapVP, proper.overrideOpaqueMaterial);
                buffer.SetGlobalMatrixArray(ShaderIDs._ShadowMapVPs, cascadeShadowMapVP);
                buffer.SetGlobalTexture(ShaderIDs._DirShadowMap, SunLight.current.shadowmapTexture);
                cbdr.dirLightShadowmap = SunLight.current.shadowmapTexture;
                staticFit.frustumCorners.Dispose();
            }

            buffer.SetGlobalVector(ShaderIDs._DirLightFinalColor, SunLight.current.light.color * SunLight.current.light.intensity);
            buffer.SetGlobalVector(ShaderIDs._DirLightPos, -(Vector3)SunLight.current.shadCam.forward);
        }
        private void PointLight(PipelineCamera cam, ref PipelineCommandData data)
        {
            shadowCullHandle.Complete();
            CommandBuffer buffer = data.buffer;
            VoxelLightCommonData(buffer, cam.cam);
            lightingHandle.Complete();
            foreach (var i in addMLightCommandList)
            {
                MLight.AddMLight(i);
            }
            foreach (var i in needCheckedShadows)
            {
                MLight mlt = MUnsafeUtility.GetObject<MLight>(i.mlight.ToPointer());
                mlt.CheckShadowSetting(i.isAvaliable);
            }
            addMLightCommandList.Clear();
            int count = Mathf.Min(cubemapVPMatrices.Length, CBDRSharedData.MAXIMUMPOINTLIGHTCOUNT);
            cbdr.pointshadowCount = count;
            if (LightFilter.pointLightCount > 0)
            {
                if (count > 0)
                {
                    var cullShader = data.resources.shaders.gpuFrustumCulling;
                    buffer.SetGlobalTexture(ShaderIDs._CubeShadowMapArray, cbdr.cubeArrayMap);
                    NativeArray<VisibleLight> allLights = proper.cullResults.visibleLights;
                    PointLightStruct* pointLightPtr = pointLightArray.Ptr();

                    for (int i = 0; i < count; ++i)
                    {
                        ref CubemapViewProjMatrix vpMatrices = ref cubemapVPMatrices[i];
                        int2 lightIndex = vpMatrices.index;
                        Light lt = allLights[lightIndex.y].light;
                        MLight light = MUnsafeUtility.GetObject<MLight>(vpMatrices.mLightPtr);
                        light.CheckShadowCamera();
                        ref PointLightStruct ptLitStr = ref pointLightPtr[lightIndex.x];
                        if (!light.useShadowCache || light.updateShadowCache)
                        {
                            light.updateShadowCache = false;
                            SceneController.DrawPointLight(light, localLightShadowLayer, ref ptLitStr, cubeDepthMaterial, buffer, cullShader, i, ref data, cubemapVPMatrices.unsafePtr, cbdr.cubeArrayMap, cam.inverseRender, proper.overrideOpaqueMaterial);
                        }
                        ptLitStr.shadowIndex = light.ShadowIndex;
                        //TODO
                        //Multi frame shadowmap
                    }
                }
                SetPointLightBuffer(pointLightArray, LightFilter.pointLightCount);
                buffer.EnableShaderKeyword("POINTLIGHT");
                cbdr.lightFlag |= 1;
            }
            else
            {
                buffer.DisableShaderKeyword("POINTLIGHT");
            }
            count = Mathf.Min(spotLightMatrices.Length, CBDRSharedData.MAXIMUMSPOTLIGHTCOUNT);
            cbdr.spotShadowCount = count;
            if (LightFilter.spotLightCount > 0)
            {
                if (count > 0)
                {
                    SpotLight* allSpotLightPtr = spotLightArray.Ptr();
                    buffer.SetGlobalTexture(ShaderIDs._SpotMapArray, cbdr.spotArrayMap);
                    spotBuffer.renderTarget = cbdr.spotArrayMap;
                    spotBuffer.shadowMatrices = spotLightMatrices.unsafePtr;
                    NativeArray<VisibleLight> allLights = proper.cullResults.visibleLights;
                    for (int i = 0; i < count; ++i)
                    {
                        ref SpotLightMatrix vpMatrices = ref spotLightMatrices[i];
                        int2 index = vpMatrices.index;
                        MLight mlight = MUnsafeUtility.GetObject<MLight>(vpMatrices.mLightPtr);
                        ref SpotLight spot = ref allSpotLightPtr[index.x];
                        mlight.CheckShadowCamera();
                        ref SpotLightMatrix spotLightMatrix = ref spotBuffer.shadowMatrices[spot.shadowIndex];
                        spot.vpMatrix = GL.GetGPUProjectionMatrix(spotLightMatrix.projectionMatrix, false) * spotLightMatrix.worldToCamera;
                        if (!mlight.useShadowCache || mlight.updateShadowCache)
                        {
                            mlight.updateShadowCache = false;
                            SceneController.DrawSpotLight(buffer, mlight, localLightShadowLayer, data.resources.shaders.gpuFrustumCulling, ref data, mlight.shadowCam, ref spot, ref spotBuffer, cam.inverseRender, proper.overrideOpaqueMaterial);
                        }
                        spot.shadowIndex = mlight.ShadowIndex;
                    }
                }
                SetSpotLightBuffer(spotLightArray, LightFilter.spotLightCount, buffer);
                buffer.EnableShaderKeyword("SPOTLIGHT");
                cbdr.lightFlag |= 0b1000;
            }
            else
            {
                buffer.DisableShaderKeyword("SPOTLIGHT");
            }
            VoxelLightCalculate(buffer, cam.cam, LightFilter.pointLightCount, LightFilter.spotLightCount);
            if (useTBDR) TBDR(buffer, cam.cam, ref cam.targets);
        }
        private void VoxelLightCommonData(CommandBuffer buffer, Camera cam)
        {
            ComputeShader cbdrShader = cbdr.cbdrShader;
            buffer.SetComputeTextureParam(cbdrShader, CBDRSharedData.SetXYPlaneKernel, ShaderIDs._XYPlaneTexture, cbdr.xyPlaneTexture);
            buffer.SetComputeTextureParam(cbdrShader, CBDRSharedData.SetZPlaneKernel, ShaderIDs._ZPlaneTexture, cbdr.zPlaneTexture);
            Transform camTrans = cam.transform;
            float currentCBDRDistance = clamp(cbdrDistance, 0, cam.farClipPlane);
            buffer.SetComputeVectorParam(cbdrShader, ShaderIDs._CameraFarPos, camTrans.position + currentCBDRDistance * camTrans.forward);
            buffer.SetComputeVectorParam(cbdrShader, ShaderIDs._CameraNearPos, camTrans.position + cam.nearClipPlane * camTrans.forward);
            buffer.SetComputeVectorParam(cbdrShader, ShaderIDs._CameraForward, camTrans.forward);
            buffer.SetGlobalVector(ShaderIDs._CameraClipDistance, new Vector4(cam.nearClipPlane, currentCBDRDistance - cam.nearClipPlane));
            buffer.DispatchCompute(cbdrShader, CBDRSharedData.SetXYPlaneKernel, 1, 1, 1);
            buffer.DispatchCompute(cbdrShader, CBDRSharedData.SetZPlaneKernel, 1, 1, 1);
        }

        private void SetPointLightBuffer(NativeArray<PointLightStruct> pointLightArray, int pointLightLength)
        {
            CBDRSharedData.ResizeBuffer(ref cbdr.allPointLightBuffer, pointLightLength);
            cbdr.allPointLightBuffer.SetData(pointLightArray, 0, 0, pointLightLength);

        }

        private void SetSpotLightBuffer(NativeArray<SpotLight> spotLightArray, int spotLightLength, CommandBuffer buffer)
        {
            CBDRSharedData.ResizeBuffer(ref cbdr.allSpotLightBuffer, spotLightLength);
            ComputeShader cbdrShader = cbdr.cbdrShader;
            cbdr.allSpotLightBuffer.SetData(spotLightArray, 0, 0, spotLightLength);
        }

        private void VoxelLightCalculate(CommandBuffer buffer, Camera cam, int pointLightLength, int spotLightLength)
        {
            ComputeShader cbdrShader = cbdr.cbdrShader;
            buffer.SetComputeBufferParam(cbdrShader, CBDRSharedData.DeferredCBDR, ShaderIDs._AllPointLight, cbdr.allPointLightBuffer);
            buffer.SetComputeBufferParam(cbdrShader, CBDRSharedData.DeferredCBDR, ShaderIDs._AllSpotLight, cbdr.allSpotLightBuffer);
            buffer.SetComputeIntParam(cbdrShader, ShaderIDs._PointLightCount, pointLightLength);
            buffer.SetComputeIntParam(cbdrShader, ShaderIDs._SpotLightCount, spotLightLength);
            buffer.SetComputeTextureParam(cbdrShader, CBDRSharedData.DeferredCBDR, ShaderIDs._XYPlaneTexture, cbdr.xyPlaneTexture);
            buffer.SetComputeTextureParam(cbdrShader, CBDRSharedData.DeferredCBDR, ShaderIDs._ZPlaneTexture, cbdr.zPlaneTexture);
            buffer.SetComputeBufferParam(cbdrShader, CBDRSharedData.DeferredCBDR, ShaderIDs._PointLightIndexBuffer, cbdr.pointlightIndexBuffer);
            buffer.SetComputeBufferParam(cbdrShader, CBDRSharedData.DeferredCBDR, ShaderIDs._SpotLightIndexBuffer, cbdr.spotlightIndexBuffer);
            buffer.DispatchCompute(cbdrShader, CBDRSharedData.DeferredCBDR, 1, 1, CBDRSharedData.ZRES);
            buffer.SetGlobalBuffer(ShaderIDs._AllPointLight, cbdr.allPointLightBuffer);
            buffer.SetGlobalBuffer(ShaderIDs._AllSpotLight, cbdr.allSpotLightBuffer);
            buffer.SetGlobalBuffer(ShaderIDs._PointLightIndexBuffer, cbdr.pointlightIndexBuffer);
            buffer.SetGlobalBuffer(ShaderIDs._SpotLightIndexBuffer, cbdr.spotlightIndexBuffer);
        }
        private static readonly int _DownSampledDepth0 = Shader.PropertyToID("_DownSampledDepth0");
        private static readonly int _DownSampledDepth1 = Shader.PropertyToID("_DownSampledDepth1");
        private static readonly int _DownSampledDepth2 = Shader.PropertyToID("_DownSampledDepth2");
        private static readonly int _DownSampledDepth3 = Shader.PropertyToID("_DownSampledDepth3");
        private void TBDR(CommandBuffer buffer, Camera cam, ref RenderTargets tar)
        {
            ComputeShader tbdrShader = cbdr.cbdrShader;
            buffer.GetTemporaryRT(_DownSampledDepth0, cam.pixelWidth / 2, cam.pixelHeight / 2, 0, FilterMode.Point, RenderTextureFormat.RGHalf, RenderTextureReadWrite.Linear, 1, false, RenderTextureMemoryless.None, false);
            buffer.GetTemporaryRT(_DownSampledDepth1, cam.pixelWidth / 4, cam.pixelHeight / 4, 0, FilterMode.Point, RenderTextureFormat.RGHalf, RenderTextureReadWrite.Linear, 1, false, RenderTextureMemoryless.None, false);
            buffer.GetTemporaryRT(_DownSampledDepth2, cam.pixelWidth / 8, cam.pixelHeight / 8, 0, FilterMode.Point, RenderTextureFormat.RGHalf, RenderTextureReadWrite.Linear, 1, false, RenderTextureMemoryless.None, false);
            buffer.GetTemporaryRT(_DownSampledDepth3, cam.pixelWidth / 16, cam.pixelHeight / 16, 0, FilterMode.Point, RenderTextureFormat.RGHalf, RenderTextureReadWrite.Linear, 1, false, RenderTextureMemoryless.None, false);
            buffer.BlitSRT(_DownSampledDepth0, minMaxBoundMat, 0);
            buffer.SetGlobalTexture(ShaderIDs._TargetDepthTexture, _DownSampledDepth0);
            buffer.BlitSRT(_DownSampledDepth1, minMaxBoundMat, 1);
            buffer.SetGlobalTexture(ShaderIDs._TargetDepthTexture, _DownSampledDepth1);
            buffer.BlitSRT(_DownSampledDepth2, minMaxBoundMat, 1);
            buffer.SetGlobalTexture(ShaderIDs._TargetDepthTexture, _DownSampledDepth2);
            buffer.BlitSRT(_DownSampledDepth3, minMaxBoundMat, 1);
            int2 tileSize = int2(cam.pixelWidth / 16, cam.pixelHeight / 16);
            RenderTextureDescriptor lightTileDisc = new RenderTextureDescriptor
            {
                autoGenerateMips = false,
                bindMS = false,
                colorFormat = RenderTextureFormat.RInt,
                depthBufferBits = 0,
                dimension = TextureDimension.Tex3D,
                enableRandomWrite = true,
                width = cam.pixelWidth / 16,
                height = cam.pixelHeight / 16,
                msaaSamples = 1,
                volumeDepth = CBDRSharedData.MAXLIGHTPERCLUSTER + 1
            };
            buffer.GetTemporaryRT(ShaderIDs._PointLightTile, lightTileDisc);
            buffer.GetTemporaryRT(ShaderIDs._SpotLightTile, lightTileDisc);
            tileSizeArray[0] = tileSize.x;
            tileSizeArray[1] = tileSize.y;
            buffer.SetComputeVectorParam(tbdrShader, ShaderIDs._CameraPos, cam.transform.position);
            buffer.SetComputeIntParams(tbdrShader, ShaderIDs._TileSize, tileSizeArray);
            buffer.SetComputeBufferParam(tbdrShader, CBDRSharedData.DeferredTBDR, ShaderIDs._AllPointLight, cbdr.allPointLightBuffer);
            buffer.SetComputeBufferParam(tbdrShader, CBDRSharedData.DeferredTBDR, ShaderIDs._AllSpotLight, cbdr.allSpotLightBuffer);
            buffer.SetComputeTextureParam(tbdrShader, CBDRSharedData.DeferredTBDR, ShaderIDs._DepthBoundTexture, new RenderTargetIdentifier(_DownSampledDepth3));
            buffer.SetComputeTextureParam(tbdrShader, CBDRSharedData.DeferredTBDR, ShaderIDs._PointLightTile, new RenderTargetIdentifier(ShaderIDs._PointLightTile));
            buffer.SetComputeTextureParam(tbdrShader, CBDRSharedData.DeferredTBDR, ShaderIDs._SpotLightTile, new RenderTargetIdentifier(ShaderIDs._SpotLightTile));
            buffer.DispatchCompute(tbdrShader, CBDRSharedData.DeferredTBDR, Mathf.CeilToInt(cam.pixelWidth / 16f), Mathf.CeilToInt(cam.pixelHeight / 16f), 1);
            buffer.ReleaseTemporaryRT(_DownSampledDepth0);
            buffer.ReleaseTemporaryRT(_DownSampledDepth1);
            buffer.ReleaseTemporaryRT(_DownSampledDepth2);
            buffer.ReleaseTemporaryRT(_DownSampledDepth3);
            buffer.SetGlobalVector(ShaderIDs._TileSize, new Vector2(tileSize.x, tileSize.y));
        }

        [Unity.Burst.BurstCompile]
        public unsafe struct CascadeShadowmap : IJobParallelFor
        {
            public int resolution;
            public float farClipPlane;
            [NativeDisableUnsafePtrRestriction]
            public OrthoCam* orthoCam;
            [NativeDisableUnsafePtrRestriction]
            public float4x4* cascadeShadowmapVPs;
            [NativeDisableUnsafePtrRestriction]
            public float3* frustumCorners;
            [NativeDisableUnsafePtrRestriction]
            public OrthoCam* results;
            public bool isD3D;
            public void Execute(int index)
            {
                OrthoCam shadCam = new OrthoCam
                {
                    forward = orthoCam->forward,
                    up = orthoCam->up,
                    right = orthoCam->right
                };
                int frustumStartPos = index * 4;
                int2 farCompare = int2(4, 6);
                int2 nearFarCompare = int2(0, 6);
                double nearFarDist = distance((double3)frustumCorners[nearFarCompare.x + frustumStartPos], (double3)frustumCorners[nearFarCompare.y + frustumStartPos]);
                double farDist = distance((double3)frustumCorners[farCompare.x + frustumStartPos], (double3)frustumCorners[farCompare.y + frustumStartPos]);
                float3* farestPoses = stackalloc float3[2];
                if (nearFarDist > farDist)
                {
                    farDist = nearFarDist;
                    farestPoses[0] = frustumCorners[nearFarCompare.x + frustumStartPos];
                    farestPoses[1] = frustumCorners[nearFarCompare.y + frustumStartPos];
                }
                else
                {
                    farestPoses[0] = frustumCorners[farCompare.x + frustumStartPos];
                    farestPoses[1] = frustumCorners[farCompare.y + frustumStartPos];
                }
                shadCam.size = (float)farDist * 0.5f;
                float3 targetPosition = (farestPoses[0] + farestPoses[1]) * 0.5f;// - shadCam.forward * farClipPlane * 0.5;
                shadCam.nearClipPlane = -farClipPlane;
                shadCam.farClipPlane = shadCam.size;
                ref float4x4 shadowVP = ref cascadeShadowmapVPs[index];
                float4x4 invShadowVP = inverse(shadowVP);

                float4 ndcPos = mul(shadowVP, new float4(targetPosition, 1));
                ndcPos /= ndcPos.w;
                float2 uv = new float2(ndcPos.x, ndcPos.y) * 0.5f + new float2(0.5f, 0.5f);
                uv.x = (int)(uv.x * resolution + 0.5);
                uv.y = (int)(uv.y * resolution + 0.5);
                uv /= resolution;
                uv = uv * 2f - 1;
                ndcPos = new float4(uv.x, uv.y, ndcPos.z, 1);
                float4 targetPos_4 = mul(invShadowVP, ndcPos);
                targetPosition = targetPos_4.xyz / targetPos_4.w;
                shadCam.position = targetPosition;
                shadCam.UpdateProjectionMatrix();
                shadCam.UpdateTRSMatrix();
                shadowVP = mul(GraphicsUtility.GetGPUProjectionMatrix(shadCam.projectionMatrix, false, isD3D), shadCam.worldToCameraMatrix);
                results[index] = shadCam;
            }
        }

        private unsafe struct LightFilter : IJobParallelFor
        {
            public static VisibleLight* allVisibleLight;
            public static List<Light> allLights;
            public static List<Light> allMLightCommandList;
            public static NativeList<CubemapViewProjMatrix> cubemapVPMatrices;
            public static NativeArray<PointLightStruct> pointLightArray;
            public static NativeArray<SpotLight> spotLightArray;
            public static NativeList<SpotLightMatrix> spotLightMatrices;
            public static int pointLightCount = 0;
            public static int spotLightCount = 0;
            public NativeList<ShadowAvaliable> needCheckLight;
            public float3 camPos;
            public float shadowDist;
            public float lightDist;
            public static void Clear()
            {
                pointLightCount = 0;
                spotLightCount = 0;
                allLights = null;
            }
            public static void CalculateCubemapMatrix(PointLightStruct* allLights, CubemapViewProjMatrix* allMatrix, int index)
            {
                ref CubemapViewProjMatrix cube = ref allMatrix[index];
                int2 shadowIndex = cube.index;
                PointLightStruct str = allLights[shadowIndex.x];
                PerspCam cam = new PerspCam();
                cam.aspect = 1;
                cam.farClipPlane = str.sphere.w;
                cam.nearClipPlane = 0.3f;
                cam.position = str.sphere.xyz;
                cam.fov = 90f;
                //Forward
                cam.right = float3(1, 0, 0);
                cam.up = float3(0, 1, 0);
                cam.forward = float3(0, 0, 1);
                cam.UpdateTRSMatrix();
                cam.UpdateProjectionMatrix();
                float4x4 proj = GraphicsUtility.GetGPUProjectionMatrix(cam.projectionMatrix, true);
                cube.forwardProjView = mul(proj, cam.worldToCameraMatrix);
                //Back
                cam.right = float3(-1, 0, 0);
                cam.up = float3(0, 1, 0);
                cam.forward = float3(0, 0, -1);
                cam.UpdateTRSMatrix();

                cube.backProjView = mul(proj, cam.worldToCameraMatrix);
                //Up
                cam.right = float3(-1, 0, 0);
                cam.up = float3(0, 0, 1);
                cam.forward = float3(0, 1, 0);
                cam.UpdateTRSMatrix();
                cube.upProjView = mul(proj, cam.worldToCameraMatrix);
                //Down
                cam.right = float3(-1, 0, 0);
                cam.up = float3(0, 0, -1);
                cam.forward = float3(0, -1, 0);
                cam.UpdateTRSMatrix();
                cube.downProjView = mul(proj, cam.worldToCameraMatrix);
                //Right
                cam.up = float3(0, 1, 0);
                cam.right = float3(0, 0, -1);
                cam.forward = float3(1, 0, 0);
                cam.UpdateTRSMatrix();
                cube.rightProjView = mul(proj, cam.worldToCameraMatrix);
                //Left
                cam.up = float3(0, 1, 0);
                cam.right = float3(0, 0, 1);
                cam.forward = float3(-1, 0, 0);
                cam.UpdateTRSMatrix();
                cube.leftProjView = mul(proj, cam.worldToCameraMatrix);
                NativeArray<float4> frustumArray = new NativeArray<float4>(6, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                cube.frustumPlanes = frustumArray.Ptr();
                cube.frustumPlanes[0] = VectorUtility.GetPlane(float3(0, 1, 0), str.sphere.xyz + float3(0, str.sphere.w, 0));
                cube.frustumPlanes[1] = VectorUtility.GetPlane(float3(0, -1, 0), str.sphere.xyz + float3(0, -str.sphere.w, 0));
                cube.frustumPlanes[2] = VectorUtility.GetPlane(float3(1, 0, 0), str.sphere.xyz + float3(str.sphere.w, 0, 0));
                cube.frustumPlanes[3] = VectorUtility.GetPlane(float3(-1, 0, 0), str.sphere.xyz + float3(-str.sphere.w, 0, 0));
                cube.frustumPlanes[4] = VectorUtility.GetPlane(float3(0, 0, 1), str.sphere.xyz + float3(0, 0, str.sphere.w));
                cube.frustumPlanes[5] = VectorUtility.GetPlane(float3(0, 0, -1), str.sphere.xyz + float3(0, 0, -str.sphere.w));
            }
            public static void CalculatePersMatrix(SpotLight* allLights, SpotLightMatrix* projectionMatrices, int index)
            {
                ref SpotLightMatrix matrices = ref projectionMatrices[index];
                int2 shadowIndex = matrices.index;
                ref SpotLight lit = ref allLights[shadowIndex.x];
                PerspCam cam = new PerspCam
                {
                    aspect = 1,
                    farClipPlane = lit.lightCone.height,
                    fov = lit.angle * 2 * Mathf.Rad2Deg,
                    nearClipPlane = Mathf.Max(0.3f, allLights->nearClip)
                };
                cam.UpdateViewMatrix(lit.vpMatrix);
                cam.UpdateProjectionMatrix();
                matrices.projectionMatrix = cam.projectionMatrix;
                matrices.worldToCamera = cam.worldToCameraMatrix;
            }
            public void Execute(int index)
            {
                PointLightStruct* indStr = pointLightArray.Ptr();
                SpotLight* spotStr = spotLightArray.Ptr();
                VisibleLight i = allVisibleLight[index];
                MLight mlight;

                switch (i.lightType)
                {
                    case LightType.Point:
                        float dist = lightDist + i.range;
                        float3 pos = (Vector3)i.localToWorldMatrix.GetColumn(3);
                        if (lengthsq(pos - camPos) > (dist * dist)) return;
                        if (!MLight.GetPointLight(allLights[index], out mlight))
                        {
                            lock (allMLightCommandList)
                            {
                                allMLightCommandList.Add(allLights[index]);
                            }
                            break;
                        }
                        int currentPointCount = Interlocked.Increment(ref pointLightCount) - 1;
                        PointLightStruct* currentPtr = indStr + currentPointCount;
                        Color col = i.finalColor;
                        currentPtr->lightColor = new float3(col.r, col.g, col.b) / (4 * Mathf.PI);
                        currentPtr->sphere = float4(pos, i.range);
                        dist = shadowDist + currentPtr->sphere.w;
                        if (mlight.useShadow && lengthsq(currentPtr->sphere.xyz - camPos) < (dist * dist))
                        {
                            currentPtr->shadowIndex = cubemapVPMatrices.ConcurrentAdd(new CubemapViewProjMatrix
                            {
                                index = new int2(currentPointCount, index),
                                mLightPtr = MUnsafeUtility.GetManagedPtr(mlight)
                            });
                            needCheckLight.ConcurrentAdd(new ShadowAvaliable
                            {
                                mlight = new UIntPtr(MUnsafeUtility.GetManagedPtr(mlight)),
                                isAvaliable = true
                            });
                        }
                        else
                        {
                            currentPtr->shadowIndex = -1;
                            needCheckLight.ConcurrentAdd(new ShadowAvaliable
                            {
                                mlight = new UIntPtr(MUnsafeUtility.GetManagedPtr(mlight)),
                                isAvaliable = false
                            });
                        }
                       
                        if (currentPtr->shadowIndex >= 0)
                        {
                            CalculateCubemapMatrix(indStr, cubemapVPMatrices.unsafePtr, currentPtr->shadowIndex);
                        }
                        break;
                    case LightType.Spot:
                        float3 spotPos = (Vector3)i.localToWorldMatrix.GetColumn(3);
                        float deg = Mathf.Deg2Rad * i.spotAngle * 0.5f;
                        Cone c = new Cone(spotPos, i.range, normalize((Vector3)i.localToWorldMatrix.GetColumn(2)), deg);
                        float3 dir = normalize(c.vertex - camPos);
                        if (!VectorUtility.ConeIntersect(c, VectorUtility.GetPlane(dir, camPos + dir * lightDist))) return;
                        if (!MLight.GetPointLight(allLights[index], out mlight))
                        {
                            lock (allMLightCommandList)
                            {
                                allMLightCommandList.Add(allLights[index]);
                            }
                            break;
                        }
                        int currentSpotCount = Interlocked.Increment(ref spotLightCount) - 1;
                        SpotLight* currentSpot = spotStr + currentSpotCount;
                        Color spotCol = i.finalColor;
                        currentSpot->lightCone = c;
                        currentSpot->lightColor = new float3(spotCol.r, spotCol.g, spotCol.b) / (4 * Mathf.PI);
                        currentSpot->angle = deg;
                        currentSpot->smallAngle = Mathf.Deg2Rad * mlight.smallSpotAngle * 0.5f;
                        currentSpot->nearClip = mlight.spotNearClip;

                        if (mlight.useShadow && VectorUtility.ConeIntersect(c, VectorUtility.GetPlane(dir, camPos + dir * shadowDist)))
                        {
                            currentSpot->vpMatrix = i.localToWorldMatrix;
                            currentSpot->shadowIndex = spotLightMatrices.ConcurrentAdd(new SpotLightMatrix
                            {
                                index = new int2(currentSpotCount, index),
                                mLightPtr = MUnsafeUtility.GetManagedPtr(mlight)
                            });
                            needCheckLight.ConcurrentAdd(new ShadowAvaliable
                            {
                                mlight = new UIntPtr(MUnsafeUtility.GetManagedPtr(mlight)),
                                isAvaliable = true
                            });
                        }
                        else
                        {
                            currentSpot->shadowIndex = -1;
                            needCheckLight.ConcurrentAdd(new ShadowAvaliable
                            {
                                mlight = new UIntPtr(MUnsafeUtility.GetManagedPtr(mlight)),
                                isAvaliable = false
                            });
                        }
                        if (currentSpot->shadowIndex >= 0)
                        {
                            CalculatePersMatrix(spotStr, spotLightMatrices.unsafePtr, currentSpot->shadowIndex);
                        }
                        break;

                }
            }
        }
    }
}