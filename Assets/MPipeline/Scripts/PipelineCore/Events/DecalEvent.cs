using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Unity.Jobs;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using UnityEngine.Rendering;
namespace MPipeline
{
    public unsafe struct DecalEvent
    {
        private DecalCullJob cullJob;
        private NativeArray<DecalStrct> decalCullResults;
        private NativeArray<DecalIndexCompare> decalCompareResults;
        private JobHandle handle;
        private PropertySetEvent proper;
        private LightingEvent lightingEvt;
        private ComputeBuffer decalBuffer;
        private ComputeShader cbdrShader;
        private DecalSortJob sortJob;
        const int INITCOUNT = 20;
        private struct DecalStrct
        {
            public float3x4 localToWorld;
            public float3x4 worldToLocal;
            public float3 minPosition;
            public float3 maxPosition;
            public float4 albedoScaleOffset;
            public float4 normalScaleOffset;
            public float4 specularScaleOffset;
            public int3 texIndex;
            public uint layer;
            public float2 heightScaleOffset;
            public float3 opacity;
        }
        public void Init(PipelineResources res)
        {
            cbdrShader = res.shaders.cbdrShader;
            proper = RenderPipeline.GetEvent<PropertySetEvent>();
            lightingEvt = RenderPipeline.GetEvent<LightingEvent>();
            decalBuffer = new ComputeBuffer(INITCOUNT, sizeof(DecalStrct));
            sortJob.sortedDecalDatas = new NativeList<DecalStrct>(INITCOUNT, Allocator.Persistent);
            minMaxBoundMat = new Material(res.shaders.minMaxDepthBounding);
        }
      
        public void Dispose()
        {
            decalBuffer.Dispose();
            sortJob.sortedDecalDatas.Dispose();
        }
        private struct DecalIndexCompare : IFunction<DecalIndexCompare, int>
        {
            public int importance;
            public int index;
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public int Run(ref DecalIndexCompare comp)
            {
                return importance - comp.importance;
            }
        }
        public void PreRenderFrame(PipelineCamera cam, ref PipelineCommandData data)
        {
            if (!Decal.decalDatas.isCreated) return;
            decalCullResults = new NativeArray<DecalStrct>(Decal.allDecalCount, Allocator.Temp);
            decalCompareResults = new NativeArray<DecalIndexCompare>(Decal.allDecalCount, Allocator.Temp);
            cullJob = new DecalCullJob
            {
                count = 0,
                decalDatas = decalCullResults.Ptr(),
                frustumPlanes = (float4*)proper.frustumPlanes.Ptr(),
                availiableDistanceSqr = lightingEvt.cbdrDistance * lightingEvt.cbdrDistance,
                camPos = cam.cam.transform.position,
                allDatas = Decal.decalDatas.unsafePtr,
                camMinPos = cam.frustumMinPoint,
                camMaxPos = cam.frustumMaxPoint
            };
            cullJob.indexCompares = decalCompareResults.Ptr();
            handle = cullJob.ScheduleRefBurst(Decal.allDecalCount, max(1, Decal.allDecalCount / 4));

            sortJob.compares = decalCompareResults.Ptr();
            sortJob.count = cullJob.count.Ptr();
            sortJob.decalDatas = decalCullResults.Ptr();
            handle = sortJob.Schedule(handle);


        }
        private Material minMaxBoundMat;
        private static int[] tileSizeArray = new int[2];
        private static readonly int _DownSampledDepth0 = Shader.PropertyToID("_DownSampledDepth0");
        private static readonly int _DownSampledDepth1 = Shader.PropertyToID("_DownSampledDepth1");
        private static readonly int _DownSampledDepth2 = Shader.PropertyToID("_DownSampledDepth2");
        private static readonly int _DownSampledDepth3 = Shader.PropertyToID("_DownSampledDepth3");
        private static readonly int _EnableDecal = Shader.PropertyToID("_EnableDecal");
        private static Vector4[] frustumCorners = new Vector4[4];
        public void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data)
        {
            CommandBuffer buffer = data.buffer;
            if (!Decal.decalDatas.isCreated)
            {
                buffer.SetGlobalInt(_EnableDecal, 0);
                return;
            }
            buffer.SetGlobalInt(_EnableDecal, 1);
            handle.Complete();
            if (cullJob.count > decalBuffer.count)
            {
                int oldCount = decalBuffer.count;
                decalBuffer.Dispose();
                decalBuffer = new ComputeBuffer((int)max(oldCount * 1.5f, cullJob.count), sizeof(DecalStrct));
            }
            if (!minMaxBoundMat) minMaxBoundMat = new Material(data.resources.shaders.minMaxDepthBounding);
            int pixelWidth = cam.cam.pixelWidth;
            int pixelHeight = cam.cam.pixelHeight;
            decalBuffer.SetDataPtr(sortJob.sortedDecalDatas.unsafePtr, 0, cullJob.count);
            buffer.GetTemporaryRT(_DownSampledDepth0, pixelWidth / 2, pixelHeight / 2, 0, FilterMode.Point, RenderTextureFormat.RGHalf, RenderTextureReadWrite.Linear, 1, false, RenderTextureMemoryless.None, false);
            buffer.GetTemporaryRT(_DownSampledDepth1, pixelWidth / 4, pixelHeight / 4, 0, FilterMode.Point, RenderTextureFormat.RGHalf, RenderTextureReadWrite.Linear, 1, false, RenderTextureMemoryless.None, false);
            buffer.GetTemporaryRT(_DownSampledDepth2, pixelWidth / 8, pixelHeight / 8, 0, FilterMode.Point, RenderTextureFormat.RGHalf, RenderTextureReadWrite.Linear, 1, false, RenderTextureMemoryless.None, false);
            buffer.GetTemporaryRT(_DownSampledDepth3, pixelWidth / 16, pixelHeight / 16, 0, FilterMode.Point, RenderTextureFormat.RGHalf, RenderTextureReadWrite.Linear, 1, false, RenderTextureMemoryless.None, false);
            buffer.BlitSRT(_DownSampledDepth0, minMaxBoundMat, 0);
            buffer.SetGlobalTexture(ShaderIDs._TargetDepthTexture, _DownSampledDepth0);
            buffer.BlitSRT(_DownSampledDepth1, minMaxBoundMat, 1);
            buffer.SetGlobalTexture(ShaderIDs._TargetDepthTexture, _DownSampledDepth1);
            buffer.BlitSRT(_DownSampledDepth2, minMaxBoundMat, 1);
            buffer.SetGlobalTexture(ShaderIDs._TargetDepthTexture, _DownSampledDepth2);
            buffer.BlitSRT(_DownSampledDepth3, minMaxBoundMat, 1);
            int2 tileSize = int2(pixelWidth / 16, pixelHeight / 16);
            RenderTextureDescriptor lightTileDisc = new RenderTextureDescriptor
            {
                autoGenerateMips = false,
                bindMS = false,
                colorFormat = RenderTextureFormat.RInt,
                depthBufferBits = 0,
                dimension = TextureDimension.Tex3D,
                enableRandomWrite = true,
                width = tileSize.x,
                height = tileSize.y,
                msaaSamples = 1,
                volumeDepth = CBDRSharedData.MAXLIGHTPERTILE
            };
            buffer.GetTemporaryRT(ShaderIDs._DecalTile, lightTileDisc);
            tileSizeArray[0] = tileSize.x;
            tileSizeArray[1] = tileSize.y;
            float3* corners = stackalloc float3[4];
            PerspCam perspCam = new PerspCam
            {
                fov = cam.cam.fieldOfView,
                up = cam.cam.transform.up,
                right = cam.cam.transform.right,
                forward = cam.cam.transform.forward,
                position = cam.cam.transform.position,
                aspect = cam.cam.aspect,
            };
            PipelineFunctions.GetFrustumCorner(ref perspCam, 1, corners);
            for (int i = 0; i < 4; ++i)
            {
                frustumCorners[i] = float4(corners[i], 1);
            }
            buffer.SetComputeVectorArrayParam(cbdrShader, ShaderIDs._FrustumCorners, frustumCorners);
            buffer.SetComputeVectorParam(cbdrShader, ShaderIDs._CameraPos, cam.cam.transform.position);
            buffer.SetComputeIntParams(cbdrShader, ShaderIDs._TileSize, tileSizeArray);
            buffer.SetGlobalVector(ShaderIDs._TileSize, new Vector4(tileSize.x, tileSize.y));
            buffer.SetComputeVectorParam(cbdrShader, ShaderIDs._CameraForward, cam.cam.transform.forward);
            buffer.SetComputeTextureParam(cbdrShader, CBDRSharedData.DecalCull, ShaderIDs._DepthBoundTexture, new RenderTargetIdentifier(_DownSampledDepth3));
            buffer.SetComputeTextureParam(cbdrShader, CBDRSharedData.DecalCull, ShaderIDs._DecalTile, new RenderTargetIdentifier(ShaderIDs._DecalTile));
            buffer.SetComputeBufferParam(cbdrShader, CBDRSharedData.DecalCull, ShaderIDs._AllDecals, decalBuffer);
            buffer.SetComputeIntParam(cbdrShader, ShaderIDs._DecalCount, cullJob.count);
            buffer.SetGlobalBuffer(ShaderIDs._AllDecals, decalBuffer);
            buffer.DispatchCompute(cbdrShader, CBDRSharedData.DecalCull, Mathf.CeilToInt(tileSize.x / 8f), Mathf.CeilToInt(tileSize.y / 8f), 1);
            buffer.ReleaseTemporaryRT(_DownSampledDepth0);
            buffer.ReleaseTemporaryRT(_DownSampledDepth1);
            buffer.ReleaseTemporaryRT(_DownSampledDepth2);
            buffer.ReleaseTemporaryRT(_DownSampledDepth3);
            RenderPipeline.ReleaseRTAfterFrame(ShaderIDs._DecalTile);
            decalCullResults.Dispose();
            decalCompareResults.Dispose();
        }
        [Unity.Burst.BurstCompile]
        private struct DecalCullJob : IJobParallelFor
        {
            [NativeDisableUnsafePtrRestriction]
            public DecalData* allDatas;
            [NativeDisableUnsafePtrRestriction]
            public float4* frustumPlanes;
            [NativeDisableUnsafePtrRestriction]
            public DecalIndexCompare* indexCompares;
            public DecalStrct* decalDatas;
            public int count;
            public float availiableDistanceSqr;
            public float3 camPos;
            public float3 camMinPos;
            public float3 camMaxPos;
            bool BoxIntersectDecal(float3x4 localToWorld, float4* planes, float3 decalMin, float3 decalMax)
            {
                float3 position = localToWorld.c3;
                bool3 minLargerThanMax = camMinPos > decalMax;
                bool3 maxLessThanMin = camMaxPos < decalMin;
                minLargerThanMax |= maxLessThanMin;
                if (minLargerThanMax.x || minLargerThanMax.y || minLargerThanMax.z || maxLessThanMin.x || maxLessThanMin.y || maxLessThanMin.z)
                {
                    return false;
                }
                for (uint i = 0; i < 6; ++i)
                {
                    float4 plane = planes[i];
                    float3 absNormal = abs(mul(plane.xyz, float3x3(localToWorld.c0, localToWorld.c1, localToWorld.c2)));
                    if ((dot(position, plane.xyz) - dot(absNormal, 0.5f)) > -plane.w) return false;
                }
                return true;
            }
            public void Execute(int index)
            {

                ref DecalData data = ref allDatas[index];
                float dist = data.avaliableDistance * data.avaliableDistance;
                void SetMinMax(ref DecalData da, float3 ext, ref float3 minV, ref float3 maxV)
                {
                    float3 pos = mul(da.localToWorld, float4(ext, 1));
                    minV = min(pos, minV);
                    maxV = max(pos, maxV);
                }
                float3 minValue = mul(data.localToWorld, float4(0.5f, 0.5f, 0.5f, 1));
                float3 maxValue = minValue;
                SetMinMax(ref data, float3(0.5f, 0.5f, -0.5f), ref minValue, ref maxValue);
                SetMinMax(ref data, float3(0.5f, -0.5f, -0.5f), ref minValue, ref maxValue);
                SetMinMax(ref data, float3(0.5f, -0.5f, 0.5f), ref minValue, ref maxValue);
                SetMinMax(ref data, float3(-0.5f, 0.5f, 0.5f), ref minValue, ref maxValue);
                SetMinMax(ref data, float3(-0.5f, 0.5f, -0.5f), ref minValue, ref maxValue);
                SetMinMax(ref data, float3(-0.5f, -0.5f, -0.5f), ref minValue, ref maxValue);
                SetMinMax(ref data, float3(-0.5f, -0.5f, 0.5f), ref minValue, ref maxValue);
                float3 position = data.localToWorld.c3.xyz;
                if (lengthsq(camPos - position) < min(dist, availiableDistanceSqr) && BoxIntersectDecal(data.localToWorld, frustumPlanes, minValue, maxValue))
                {
                    int currentInd = System.Threading.Interlocked.Increment(ref count) - 1;
                    ref DecalStrct str = ref decalDatas[currentInd];
                    str.localToWorld = data.localToWorld;
                    str.minPosition = minValue;
                    str.maxPosition = maxValue;
                    str.albedoScaleOffset = data.albedoScaleOffset;
                    str.normalScaleOffset = data.normalScaleOffset;
                    str.specularScaleOffset = data.specularScaleOffset;
                    str.texIndex = data.texIndex;
                    str.worldToLocal = data.worldToLocal;
                    str.layer = (uint)(data.layer);
                    str.opacity = data.opacity;
                    str.heightScaleOffset = data.heightScaleOffset;
                    indexCompares[currentInd] = new DecalIndexCompare
                    {
                        importance = data.importance,
                        index = currentInd
                    };

                }
            }
        }
        [Unity.Burst.BurstCompile]
        private struct DecalSortJob : IJob
        {
            [NativeDisableUnsafePtrRestriction]
            public int* count;
            [NativeDisableUnsafePtrRestriction]
            public DecalIndexCompare* compares;
            [NativeDisableUnsafePtrRestriction]
            public DecalStrct* decalDatas;
            [NativeDisableUnsafePtrRestriction]
            public NativeList<DecalStrct> sortedDecalDatas;

            public void Execute()
            {
                MathLib.Quicksort(compares, 0, *count - 1);
                sortedDecalDatas.Clear();
                sortedDecalDatas.AddRange(*count);
                for (int i = 0; i < *count; ++i)
                {
                    sortedDecalDatas[i] = decalDatas[compares[i].index];
                }
            }
        }

    }
}