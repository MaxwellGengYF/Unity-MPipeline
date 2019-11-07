using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using System.Runtime.CompilerServices;
using static Unity.Mathematics.math;
using Random = Unity.Mathematics.Random;
using System.Threading;
using Unity.Collections.LowLevel.Unsafe;
namespace MPipeline
{
    [RequireEvent(typeof(LightingEvent), typeof(ReflectionEvent))]
    [CreateAssetMenu(menuName = "GPURP Events/Volumetric Scattering")]
    public unsafe class VolumetricLightEvent : PipelineEvent
    {
        public bool enableSuperSample = false;
        public float availableDistance = 64;
        [Range(0.01f, 100f)]
        public float indirectIntensity = 1;
        const int marchStep = 64;
        const int scatterPass = 16;
        const int accumulatePass = 17;
        const int clearPass = 18;
        static readonly int3 exampleSize = new int3(160, 90, 128);
        public float linearFogDensity = 0.02f;
        private JobHandle jobHandle;
        private NativeArray<FogVolume> resultVolume;
        private int fogCount = 0;
        private LightingEvent lightingData;
        private ReflectionEvent reflectData;
        private Material lightingMat;
        private PropertySetEvent proper;
        private ComputeBuffer cameraNormalBuffer;
        private Cubemap blackCB;
        private RenderTextureDescriptor volumeDesc;
        private RenderTexture volumeTex;
        private JobHandle voxelVolumeCullHandle;
        private NativeList<int> resultIndices;
        [Range(0f, 1f)]
        public float darkerWeight = 0.75f;
        [Range(0f, 1f)]
        public float brighterWeight = 0.95f;

        public override bool CheckProperty()
        {
            return lightingMat && blackCB;
        }
        private int3 downSampledSize;
        protected override void Init(PipelineResources resources)
        {
            proper = RenderPipeline.GetEvent<PropertySetEvent>();
            lightingData = RenderPipeline.GetEvent<LightingEvent>();
            reflectData = RenderPipeline.GetEvent<ReflectionEvent>();
            lightingMat = new Material(resources.shaders.volumetricShader);
            cameraNormalBuffer = new ComputeBuffer(3, sizeof(float3));
            blackCB = new Cubemap(1, TextureFormat.ARGB32, false);
            blackCB.SetPixel(CubemapFace.NegativeX, 0, 0, Color.black);
            blackCB.SetPixel(CubemapFace.NegativeY, 0, 0, Color.black);
            blackCB.SetPixel(CubemapFace.NegativeZ, 0, 0, Color.black);
            blackCB.SetPixel(CubemapFace.PositiveX, 0, 0, Color.black);
            blackCB.SetPixel(CubemapFace.PositiveX, 0, 0, Color.black);
            blackCB.SetPixel(CubemapFace.PositiveX, 0, 0, Color.black);
            downSampledSize = exampleSize * (enableSuperSample ? 2 : 1);

            volumeDesc = new RenderTextureDescriptor
            {
                autoGenerateMips = false,
                bindMS = false,
                colorFormat = RenderTextureFormat.ARGBHalf,
                depthBufferBits = 0,
                dimension = TextureDimension.Tex3D,
                enableRandomWrite = true,
                height = downSampledSize.y,
                width = downSampledSize.x,
                memoryless = RenderTextureMemoryless.None,
                msaaSamples = 1,
                shadowSamplingMode = ShadowSamplingMode.None,
                sRGB = false,
                useMipMap = false,
                volumeDepth = downSampledSize.z,
                vrUsage = VRTextureUsage.None
            };
            volumeTex = new RenderTexture(volumeDesc);
            volumeTex.filterMode = FilterMode.Bilinear;
            volumeTex.wrapMode = TextureWrapMode.Clamp;
            volumeTex.Create();
        }

        public override void PreRenderFrame(PipelineCamera cam, ref PipelineCommandData data)
        {
            ref CBDRSharedData cbdr = ref lightingData.cbdr;
            cbdr.availiableDistance = availableDistance;
            fogCount = 0;

            if (FogVolumeComponent.allVolumes.isCreated && FogVolumeComponent.allVolumes.Length > 0)
            {
                resultVolume = new NativeArray<FogVolume>(FogVolumeComponent.allVolumes.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                float4* frustumPlanes = (float4*)UnsafeUtility.Malloc(6 * sizeof(float4), 16, Allocator.Temp);
                UnsafeUtility.MemCpy(frustumPlanes, proper.frustumPlanes.Ptr(), 6 * sizeof(float4));
                Transform camTrans = cam.transform;
                float3 inPoint = camTrans.position + camTrans.forward * cbdr.availiableDistance;
                float3 normal = camTrans.forward;
                float4 plane = float4(normal, -dot(normal, inPoint));
                frustumPlanes[5] = plane;
                jobHandle = (new FogVolumeCalculate
                {
                    allVolume = resultVolume.Ptr(),
                    frustumPlanes = frustumPlanes,
                    fogVolumeCount = fogCount.Ptr(),
                    fogVolume = FogVolumeComponent.allVolumes.unsafePtr
                }).Schedule(FogVolumeComponent.allVolumes.Length, max(1, FogVolumeComponent.allVolumes.Length / 4));

            }
            if (VoxelFogBase.allVoxelData.isCreated)
            {
                resultIndices = new NativeList<int>(VoxelFogBase.allVoxelData.Length, Allocator.Temp);
                voxelVolumeCullHandle = new VoxelFogVolumeCull
                {
                    allDatas = VoxelFogBase.allVoxelData,
                    maxPoint = cam.frustumMaxPoint,
                    minPoint = cam.frustumMinPoint,
                    planes = (float4*)proper.frustumPlanes.Ptr(),
                    resultIndices = resultIndices
                }.Schedule(VoxelFogBase.allVoxelData.Length, max(1, VoxelFogBase.allVoxelData.Length / 4));
            }
        }
        public override void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data)
        {
            bool useIBLIndirect = reflectData.reflectionCount > 0;
            CommandBuffer buffer = data.buffer;
            ComputeShader scatter = data.resources.shaders.volumetricScattering;
            ref CBDRSharedData cbdr = ref lightingData.cbdr;
            int pass = 0;
            if (cbdr.dirLightShadowmap != null)
                pass |= 0b0010;
            if (cbdr.pointshadowCount > 0)
                pass |= 0b0001;
            if (cbdr.spotShadowCount > 0)
                pass |= 0b0100;
            if (useIBLIndirect)
                pass |= 0b1000;
            //TODO
            //Enable fourth bit as Global Illumination
            HistoryVolumetric.GetHisotryVolumetric getter = new HistoryVolumetric.GetHisotryVolumetric();
            HistoryVolumetric historyVolume = IPerCameraData.GetProperty<HistoryVolumetric, HistoryVolumetric.GetHisotryVolumetric>(cam, getter);
            //Volumetric Light
            buffer.SetGlobalVector(ShaderIDs._FroxelSize, float4(downSampledSize, 1));
            buffer.SetGlobalTexture(ShaderIDs._VolumeTex, volumeTex);
            if (!historyVolume.lastVolume)
            {
                historyVolume.lastVolume = new RenderTexture(volumeDesc);
                historyVolume.lastVolume.filterMode = FilterMode.Bilinear;
                historyVolume.lastVolume.wrapMode = TextureWrapMode.Clamp;
                historyVolume.lastVolume.Create();
                buffer.SetGlobalVector(ShaderIDs._TemporalWeight, Vector4.zero);
            }
            else
            {
                buffer.SetGlobalVector(ShaderIDs._TemporalWeight, new Vector4(darkerWeight, brighterWeight));
            }

            jobHandle.Complete();
            if (fogCount > 0)
            {
                cbdr.allFogVolumeBuffer.SetData(resultVolume, 0, 0, fogCount);
            }
            buffer.SetGlobalFloat(ShaderIDs._LinearFogDensity, linearFogDensity);
            buffer.SetGlobalVector(ShaderIDs._VolumetricLightVar, new Vector4(cam.cam.nearClipPlane, availableDistance - cam.cam.nearClipPlane, availableDistance, indirectIntensity));
            buffer.SetGlobalVector(ShaderIDs._Screen_TexelSize, new Vector4(1f / cam.cam.pixelWidth, 1f / cam.cam.pixelHeight, cam.cam.pixelWidth, cam.cam.pixelHeight));
            buffer.SetComputeBufferParam(scatter, pass, ShaderIDs._AllFogVolume, cbdr.allFogVolumeBuffer);
            buffer.SetComputeBufferParam(scatter, pass, ShaderIDs._AllPointLight, cbdr.allPointLightBuffer);
            buffer.SetComputeBufferParam(scatter, pass, ShaderIDs._AllSpotLight, cbdr.allSpotLightBuffer);
            buffer.SetComputeIntParam(scatter, ShaderIDs._FogVolumeCount, fogCount);
            buffer.SetComputeTextureParam(scatter, pass, ShaderIDs._VolumeTex, volumeTex);
            buffer.SetComputeTextureParam(scatter, clearPass, ShaderIDs._VolumeTex, volumeTex);
            buffer.SetComputeTextureParam(scatter, scatterPass, ShaderIDs._VolumeTex, volumeTex);
            buffer.SetComputeTextureParam(scatter, pass, ShaderIDs._LastVolume, historyVolume.lastVolume);
            buffer.SetComputeTextureParam(scatter, pass, ShaderIDs._DirShadowMap, cbdr.dirLightShadowmap);
            buffer.SetComputeTextureParam(scatter, pass, ShaderIDs._SpotMapArray, cbdr.spotArrayMap);
            buffer.SetComputeTextureParam(scatter, pass, ShaderIDs._CubeShadowMapArray, cbdr.cubeArrayMap);
            buffer.SetComputeTextureParam(scatter, pass, ShaderIDs._IESAtlas, lightingData.iesAtlas);
            int3 dispatchCount = int3(downSampledSize.x / 2, downSampledSize.y / 2, downSampledSize.z / marchStep);
            buffer.DispatchCompute(scatter, clearPass, dispatchCount.x, dispatchCount.y, dispatchCount.z);
            if (VoxelFogBase.allVoxelData.isCreated)
            {
                buffer.SetComputeTextureParam(scatter, accumulatePass, ShaderIDs._VolumeTex, volumeTex);
                voxelVolumeCullHandle.Complete();
                foreach(var i in resultIndices)
                {
                    ref VoxelFogBase.VoxelCubeData voxelData = ref VoxelFogBase.allVoxelData[i];
                    VoxelFogBase fogBase = MUnsafeUtility.GetObject<VoxelFogBase>(voxelData.ptr);
                    buffer.SetComputeTextureParam(scatter, accumulatePass, ShaderIDs._AlbedoVoxel, fogBase.GetVoxel());
                    buffer.SetComputeMatrixParam(scatter, ShaderIDs._VoxelWorldToLocal, voxelData.worldToLocalMatrix);
                    buffer.DispatchCompute(scatter, accumulatePass, dispatchCount.x, dispatchCount.y, dispatchCount.z);
                }
                resultIndices.Dispose();
            }
            if (useIBLIndirect)
            {
                NativeArray<float3> cameraNormals = new NativeArray<float3>(3, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                cameraNormals[0] = cam.cam.transform.forward;
                cameraNormals[1] = cam.cam.transform.right;
                cameraNormals[2] = cam.cam.transform.up;
                cameraNormalBuffer.SetData(cameraNormals);
                cameraNormals.Dispose();
                buffer.SetComputeBufferParam(scatter, pass, ShaderIDs._CameraNormals, cameraNormalBuffer);
                reflectData.SetComputeShaderIBLBuffer(scatter, pass, buffer, blackCB);
            }
            cbdr.dirLightShadowmap = null;
            buffer.SetComputeIntParam(scatter, ShaderIDs._LightFlag, (int)cbdr.lightFlag);

            buffer.DispatchCompute(scatter, pass, dispatchCount.x, dispatchCount.y, dispatchCount.z);
            buffer.CopyTexture(volumeTex, historyVolume.lastVolume);
            buffer.DispatchCompute(scatter, scatterPass, downSampledSize.x / 32, downSampledSize.y / 2, 1);
            cbdr.lightFlag = 0;
            buffer.BlitSRT(cam.targets.renderTargetIdentifier, lightingMat, 0);
        }

        protected override void OnEnable()
        {
            RenderPipeline.AfterFrameBuffer.EnableShaderKeyword("ENABLE_VOLUMETRIC");
        }

        protected override void OnDisable()
        {
            RenderPipeline.AfterFrameBuffer.DisableShaderKeyword("ENABLE_VOLUMETRIC");
        }

        protected override void Dispose()
        {
            DestroyImmediate(lightingMat);
            cameraNormalBuffer.Dispose();
            DestroyImmediate(blackCB);
            DestroyImmediate(volumeTex);
        }
        [Unity.Burst.BurstCompile]
        public unsafe struct FogVolumeCalculate : IJobParallelFor
        {
            [NativeDisableUnsafePtrRestriction]
            public FogVolume* allVolume;
            [NativeDisableUnsafePtrRestriction]
            public int* fogVolumeCount;
            [NativeDisableUnsafePtrRestriction]
            public float4* frustumPlanes;
            [NativeDisableUnsafePtrRestriction]
            public FogVolumeComponent.FogVolumeContainer* fogVolume;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool BoxUnderPlane(ref float4 plane, ref FogVolume fog, int i)
            {
                float3 absNormal = abs(normalize(mul(plane.xyz, fog.localToWorld)));
                return dot(fog.position, plane.xyz) - dot(absNormal, fog.extent) < -plane.w;
            }
            public void Execute(int index)
            {
                ref FogVolume vol = ref fogVolume[index].volume;
                for (int i = 0; i < 6; ++i)
                {
                    if (!BoxUnderPlane(ref frustumPlanes[i], ref vol, i))
                        return;
                }
                int last = Interlocked.Increment(ref *fogVolumeCount) - 1;
                allVolume[last] = vol;
            }
        }
        [Unity.Burst.BurstCompile]
        public unsafe struct VoxelFogVolumeCull : IJobParallelFor
        {
            public NativeList<VoxelFogBase.VoxelCubeData> allDatas;
            public float3 minPoint;
            public float3 maxPoint;
            [NativeDisableUnsafePtrRestriction]
            public float4* planes;
            public NativeList<int> resultIndices;
            public void Execute(int index)
            {
                ref VoxelFogBase.VoxelCubeData data = ref allDatas[index];
                bool3 jump = minPoint > data.maxPoint;
                jump |= maxPoint < data.minPoint;
                if (jump.x || jump.y || jump.z) return;
                if (MathLib.BoxIntersect(ref data.localToWorldMatrix, 0, 0.5f, planes, 6))
                {
                    resultIndices.ConcurrentAdd(index);
                }
            }
        }
    }
    public class HistoryVolumetric : IPerCameraData
    {
        public struct GetHisotryVolumetric : IGetCameraData
        {
            public IPerCameraData Run()
            {
                return new HistoryVolumetric();
            }
        }
        public RenderTexture lastVolume = null;
        public override void DisposeProperty()
        {
            if (lastVolume != null)
            {
                lastVolume.Release();
                lastVolume = null;
            }
        }
    }
}
