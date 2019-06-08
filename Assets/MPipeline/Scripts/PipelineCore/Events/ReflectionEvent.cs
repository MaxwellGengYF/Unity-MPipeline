using System.Collections;
using System.Collections.Generic;
using UnityEngine.Experimental.Rendering;
using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine.Rendering;
using Unity.Mathematics;
using static Unity.Mathematics.math;
namespace MPipeline
{
    [CreateAssetMenu(menuName = "GPURP Events/Reflection")]
    [RequireEvent(typeof(LightingEvent))]
    public unsafe sealed class ReflectionEvent : PipelineEvent
    {
        const int maximumProbe = 8;
        public bool reflectionEnabled { get; private set; }
        private NativeArray<VisibleReflectionProbe> reflectProbes;
        private NativeArray<ReflectionData> reflectionData;
        private RenderTargetIdentifier[] targetTexs = new RenderTargetIdentifier[2];
        private JobHandle storeDataHandler;
        private ComputeBuffer probeBuffer;
        private LightingEvent lightingEvents;
        private ComputeBuffer reflectionIndices;
        private Material reflectionMat;
        private Material preintMat;
        private RenderTexture preintDefaultRT;

        private NativeList<int> reflectionCubemapIDs;
        public int reflectionCount { get; private set; }
        private StoreReflectionData storeRef;
        private PropertySetEvent proper;
        public Cubemap backgroundCubemap;
        public float availiableDistance = 50;
        public StochasticScreenSpaceReflection ssrEvents = new StochasticScreenSpaceReflection();
        private static readonly int _ReflectionCubeMap = Shader.PropertyToID("_ReflectionCubeMap");
        private static readonly int _PreIntDefault = Shader.PropertyToID("_PreIntDefault");
        public override bool CheckProperty()
        {
            return reflectionIndices != null && ssrEvents.MaterialEnabled() && reflectionMat;
        }
        protected override void OnEnable()
        {
            RenderPipeline.ExecuteBufferAtFrameEnding((buffer) =>
            {
                buffer.EnableShaderKeyword("ENABLE_REFLECTION");
            });
        }

        protected override void OnDisable()
        {
            RenderPipeline.ExecuteBufferAtFrameEnding((buffer) =>
            {
                buffer.DisableShaderKeyword("ENABLE_REFLECTION");
            });
        }

        protected override void Init(PipelineResources resources)
        {
            preintMat = new Material(resources.shaders.bakePreIntShader);
            proper = RenderPipeline.GetEvent<PropertySetEvent>();
            probeBuffer = new ComputeBuffer(maximumProbe, sizeof(ReflectionData));
            lightingEvents = RenderPipeline.GetEvent<LightingEvent>();
            reflectionIndices = new ComputeBuffer(CBDRSharedData.XRES * CBDRSharedData.YRES * CBDRSharedData.ZRES * (maximumProbe + 1), sizeof(int));
            string old = "_ReflectionCubeMap";
            string newStr = new string(' ', old.Length + 1);
            reflectionCubemapIDs = new NativeList<int>(maximumProbe, maximumProbe, Allocator.Persistent);
            fixed (char* ctr = old)
            {
                fixed (char* newCtr = newStr)
                {
                    for (int i = 0; i < old.Length; ++i)
                    {
                        newCtr[i] = ctr[i];
                    }
                    for (int i = 0; i < reflectionCubemapIDs.Length; ++i)
                    {
                        newCtr[old.Length] = (char)(i + 48);
                        reflectionCubemapIDs[i] = Shader.PropertyToID(newStr);
                    }
                }
            }
            reflectionMat = new Material(resources.shaders.reflectionShader);
            ssrEvents.Init(resources);
            preintDefaultRT = null;
        }
        protected override void Dispose()
        {
            probeBuffer.Dispose();
            reflectionIndices.Dispose();
            reflectionCubemapIDs.Dispose();
            ssrEvents.Dispose();
            DestroyImmediate(reflectionMat);
            DestroyImmediate(preintMat);
            if (preintDefaultRT) DestroyImmediate(preintDefaultRT);
        }

        public override void PreRenderFrame(PipelineCamera cam, ref PipelineCommandData data)
        {
            reflectProbes = proper.cullResults.visibleReflectionProbes;
            reflectionData = new NativeArray<ReflectionData>(reflectProbes.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            storeRef = new StoreReflectionData
            {
                data = reflectionData.Ptr(),
                allProbes = reflectProbes.Ptr(),
                count = reflectProbes.Length,
                camPos = cam.cam.transform.position,
                dist = availiableDistance,
                localToWorldMat = MUnsafeUtility.Malloc<Matrix4x4>(sizeof(Matrix4x4) * reflectProbes.Length, Allocator.Temp)
            };
            storeDataHandler = storeRef.ScheduleRefBurst();
            if (ssrEvents.enabled && !RenderPipeline.renderingEditor)
            {
                ssrEvents.PreRender(cam);
            }
            if(!preintDefaultRT)
            {
                preintDefaultRT = new RenderTexture(512, 512, 0, RenderTextureFormat.RGHalf, RenderTextureReadWrite.Linear);
                preintDefaultRT.filterMode = FilterMode.Bilinear;
                preintDefaultRT.Create();
                data.buffer.BlitSRT(preintDefaultRT, preintMat, 0);
            }
        }
        public void SetComputeShaderIBLBuffer(ComputeShader targetShader, int kernel, CommandBuffer buffer, Cubemap defaultMap)
        {
            buffer.SetComputeBufferParam(targetShader, kernel, ShaderIDs._ReflectionIndices, reflectionIndices);
            buffer.SetComputeBufferParam(targetShader, kernel, ShaderIDs._ReflectionData, probeBuffer);
            for (int i = 0; i < reflectionCount; ++i)
            {
                Texture tx = reflectProbes[i].texture;
                if (!tx) tx = defaultMap;
                buffer.SetComputeTextureParam(targetShader, kernel, reflectionCubemapIDs[i], tx);
            }
            for (int i = reflectionCount; i < maximumProbe; ++i)
            {
                buffer.SetComputeTextureParam(targetShader, kernel, reflectionCubemapIDs[i], defaultMap);
            }
        }
        public override void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data)
        {
            storeDataHandler.Complete();
            reflectionCount = Mathf.Min(maximumProbe, storeRef.count);
            CommandBuffer buffer = data.buffer;
            ComputeShader cullingShader = data.resources.shaders.reflectionCullingShader;
            ref CBDRSharedData cbdr = ref lightingEvents.cbdr;
            if(probeBuffer.count < storeRef.count)
            {
                probeBuffer.Dispose();
                probeBuffer = new ComputeBuffer(storeRef.count, sizeof(ReflectionData));
            }
            probeBuffer.SetData(reflectionData, 0, 0, storeRef.count);
            buffer.SetComputeTextureParam(cullingShader, 0, ShaderIDs._XYPlaneTexture, cbdr.xyPlaneTexture);
            buffer.SetComputeTextureParam(cullingShader, 0, ShaderIDs._ZPlaneTexture, cbdr.zPlaneTexture);
            buffer.SetComputeBufferParam(cullingShader, 0, ShaderIDs._ReflectionIndices, reflectionIndices);
            buffer.SetComputeBufferParam(cullingShader, 0, ShaderIDs._ReflectionData, probeBuffer);
            buffer.SetComputeIntParam(cullingShader, ShaderIDs._Count, reflectionCount);
            buffer.SetGlobalTexture(_ReflectionCubeMap, backgroundCubemap);
            buffer.DispatchCompute(cullingShader, 0, 1, 1, CBDRSharedData.ZRES);
            buffer.SetGlobalBuffer(ShaderIDs._ReflectionIndices, reflectionIndices);
            buffer.SetGlobalBuffer(ShaderIDs._ReflectionData, probeBuffer);
            buffer.SetGlobalTexture(_PreIntDefault, preintDefaultRT);
            for (int i = 0; i < reflectionCount; ++i)
            {
                buffer.SetGlobalTexture(reflectionCubemapIDs[i], reflectProbes[i].texture);
            }

            //TODO
            targetTexs[0] = ShaderIDs._CameraReflectionTexture;
            targetTexs[1] = ShaderIDs._CameraGITexture;
            buffer.GetTemporaryRT(ShaderIDs._CameraReflectionTexture, cam.cam.pixelWidth, cam.cam.pixelHeight, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            buffer.GetTemporaryRT(ShaderIDs._CameraGITexture, cam.cam.pixelWidth, cam.cam.pixelHeight, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            buffer.SetRenderTarget(colors: targetTexs, depth: ShaderIDs._DepthBufferTexture);
            buffer.DrawMesh(GraphicsUtility.mesh, Matrix4x4.identity, reflectionMat, 0, 2);
            for(int i = 0; i < storeRef.count; ++i)
            {
                buffer.SetGlobalInt(ShaderIDs._ReflectionIndex, i);
                buffer.SetGlobalTexture(ShaderIDs._ReflectionTex, reflectProbes[i].texture);
                buffer.DrawMesh(GraphicsUtility.cubeMesh, storeRef.localToWorldMat[i], reflectionMat, 0, 0);
            }
            UnsafeUtility.Free(storeRef.localToWorldMat, Allocator.Temp);
            int releaseSSRID = -1;
            if (ssrEvents.enabled && !RenderPipeline.renderingEditor)
            {
                releaseSSRID = ssrEvents.Render(ref data, cam, proper);
                buffer.EnableShaderKeyword("ENABLE_SSR");

            }
            else
            {
                buffer.DisableShaderKeyword("ENABLE_SSR");
            }
            buffer.BlitSRTWithDepth(cam.targets.renderTargetIdentifier, ShaderIDs._DepthBufferTexture, reflectionMat, 1);
            buffer.ReleaseTemporaryRT(ShaderIDs._CameraReflectionTexture);
            buffer.ReleaseTemporaryRT(ShaderIDs._CameraGITexture);
            if (releaseSSRID >= 0)
            {
                buffer.ReleaseTemporaryRT(releaseSSRID);
            }
        }
        [Unity.Burst.BurstCompile]
        public unsafe struct StoreReflectionData : IJob
        {
            [NativeDisableUnsafePtrRestriction]
            public VisibleReflectionProbe* allProbes;
            [NativeDisableUnsafePtrRestriction]
            public ReflectionData* data;
            [NativeDisableUnsafePtrRestriction]
            public Matrix4x4* localToWorldMat;
            public int count;
            public float3 camPos;
            public float dist;
            public void Execute()
            {
                for (int index = 0; index < count; ++index)
                {
                    VisibleReflectionProbe vis = allProbes[index];
                    float dstSq = dist + length(vis.bounds.extents);
                    dstSq *= dstSq;
                    ref ReflectionData dt = ref data[index];
                    dt.blendDistance = vis.blendDistance;
                    float4x4 localToWorld = vis.localToWorldMatrix;
                    dt.minExtent = (float3)vis.bounds.extents - dt.blendDistance * 0.5f;
                    dt.maxExtent = (float3)vis.bounds.extents + dt.blendDistance * 0.5f;
                    dt.boxProjection = vis.isBoxProjection ? 1 : 0;
                    dt.position = localToWorld.c3.xyz;
                    dt.hdr = vis.hdrData;
                    localToWorldMat[index] = Matrix4x4.TRS(dt.position, Quaternion.identity, dt.maxExtent * 2);
                }
            }
        }

    }
}
