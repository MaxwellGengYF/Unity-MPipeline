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
        private JobHandle storeDataHandler;
        private ComputeBuffer probeBuffer;
        private LightingEvent lightingEvents;
        private ComputeBuffer reflectionIndices;
        private Material reflectionMat;
        private NativeList<int> reflectionCubemapIDs;
        private Material irradianceVolumeMat;
        private StoreReflectionData storeRef;
        private PropertySetEvent proper;
       
        public float availiableDistance = 50;
        [SerializeField]
        public IrradianceCuller culler;
        public StochasticScreenSpaceReflection ssrEvents = new StochasticScreenSpaceReflection();
        private static readonly int _ReflectionCubeMap = Shader.PropertyToID("_ReflectionCubeMap");

        public override bool CheckProperty()
        {
            return reflectionIndices != null && ssrEvents.MaterialEnabled() && irradianceVolumeMat;
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
            proper = RenderPipeline.GetEvent<PropertySetEvent>();
            probeBuffer = new ComputeBuffer(maximumProbe, sizeof(ReflectionData));
            lightingEvents = RenderPipeline.GetEvent<LightingEvent>();
            reflectionIndices = new ComputeBuffer(CBDRSharedData.XRES * CBDRSharedData.YRES * CBDRSharedData.ZRES * (maximumProbe + 1), sizeof(int));
            string old = "_ReflectionCubeMap";
            string newStr = new string(' ', old.Length + 1);
            reflectionCubemapIDs = new NativeList<int>(maximumProbe, maximumProbe, Allocator.Persistent);
            irradianceVolumeMat = new Material(resources.shaders.irradianceVolumeShader);
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
        }
        protected override void Dispose()
        {
            DestroyImmediate(irradianceVolumeMat);
            probeBuffer.Dispose();
            reflectionIndices.Dispose();
            reflectionCubemapIDs.Dispose();
            ssrEvents.Dispose();
            DestroyImmediate(reflectionMat);
        }

        public override void PreRenderFrame(PipelineCamera cam, ref PipelineCommandData data)
        {
            culler.PreRenderFrame(ref data, proper);
            reflectProbes = proper.cullResults.visibleReflectionProbes;
            reflectionData = new NativeArray<ReflectionData>(Mathf.Min(maximumProbe, reflectProbes.Length), Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            storeRef = new StoreReflectionData
            {
                data = reflectionData.Ptr(),
                allProbes = reflectProbes.Ptr(),
                count = 0,
                camPos = cam.cam.transform.position,
                dist = availiableDistance,
                maximumProbe = maximumProbe
            };
            storeDataHandler = storeRef.ScheduleRefBurst(reflectProbes.Length, 32);
            if (ssrEvents.enabled && !RenderPipeline.renderingEditor)
            {
                ssrEvents.PreRender(cam);
            }
        }
        private void DrawGI(CommandBuffer buffer, ref CoeffTexture currentTexture, Matrix4x4 localToWorld)
        {
            buffer.SetGlobalTexture(IrradianceVolumeController.current._CoeffIDs[0], currentTexture.coeff0);
            buffer.SetGlobalTexture(IrradianceVolumeController.current._CoeffIDs[1], currentTexture.coeff1);
            buffer.SetGlobalTexture(IrradianceVolumeController.current._CoeffIDs[2], currentTexture.coeff2);
            buffer.SetGlobalTexture(IrradianceVolumeController.current._CoeffIDs[3], currentTexture.coeff3);
            buffer.SetGlobalTexture(IrradianceVolumeController.current._CoeffIDs[4], currentTexture.coeff4);
            buffer.SetGlobalTexture(IrradianceVolumeController.current._CoeffIDs[5], currentTexture.coeff5);
            buffer.SetGlobalTexture(IrradianceVolumeController.current._CoeffIDs[6], currentTexture.coeff6);
            buffer.SetGlobalMatrix(ShaderIDs._WorldToLocalMatrix, localToWorld.inverse);
            buffer.DrawMesh(GraphicsUtility.cubeMesh, localToWorld, irradianceVolumeMat, 0, 0);
        }
        public override void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data)
        {
            culler.FrameUpdate();
       /*     if (culler.cullingResult.isCreated && culler.cullingResult.Length > 0)
            {
                data.buffer.SetRenderTarget(color: cam.targets.renderTargetIdentifier, depth: cam.targets.depthBuffer);
                foreach (var i in culler.cullingResult)
                {
                    ref LoadedIrradiance irr = ref IrradianceVolumeController.current.loadedIrradiance[i];
                    CoeffTexture coefTex = IrradianceVolumeController.current.coeffTextures[irr.renderTextureIndex];
                    DrawGI(data.buffer, ref coefTex, new Matrix4x4(float4(irr.localToWorld.c0, 0), float4(irr.localToWorld.c1, 0), float4(irr.localToWorld.c2, 0), float4(irr.position, 1)));
                }
            }*/
            storeDataHandler.Complete();
            int count = Mathf.Min(maximumProbe, storeRef.count);
            
            CommandBuffer buffer = data.buffer;
            ComputeShader cullingShader = data.resources.shaders.reflectionCullingShader;
            ref CBDRSharedData cbdr = ref lightingEvents.cbdr;
            probeBuffer.SetData(reflectionData, 0, 0, count);
            buffer.SetComputeTextureParam(cullingShader, 0, ShaderIDs._XYPlaneTexture, cbdr.xyPlaneTexture);
            buffer.SetComputeTextureParam(cullingShader, 0, ShaderIDs._ZPlaneTexture, cbdr.zPlaneTexture);
            buffer.SetComputeBufferParam(cullingShader, 0, ShaderIDs._ReflectionIndices, reflectionIndices);
            buffer.SetComputeBufferParam(cullingShader, 0, ShaderIDs._ReflectionData, probeBuffer);
            buffer.SetComputeIntParam(cullingShader, ShaderIDs._Count, count);
            buffer.DispatchCompute(cullingShader, 0, 1, 1, CBDRSharedData.ZRES);
            buffer.SetGlobalBuffer(ShaderIDs._ReflectionIndices, reflectionIndices);
            buffer.SetGlobalBuffer(ShaderIDs._ReflectionData, probeBuffer);
            for (int i = 0; i < count; ++i)
            {
                buffer.SetGlobalTexture(reflectionCubemapIDs[i], reflectProbes[i].texture);
            }
            if (ssrEvents.enabled && !RenderPipeline.renderingEditor)
            {
                ssrEvents.Render(ref data, cam, proper);
                buffer.EnableShaderKeyword("ENABLE_SSR");

            }
            else
            {
                buffer.DisableShaderKeyword("ENABLE_SSR");
            }
            buffer.BlitSRTWithDepth(cam.targets.renderTargetIdentifier, cam.targets.depthBuffer, reflectionMat, 0);
            //TODO
        }
        [Unity.Burst.BurstCompile]
        public unsafe struct StoreReflectionData : IJobParallelFor
        {
            [NativeDisableUnsafePtrRestriction]
            public VisibleReflectionProbe* allProbes;
            [NativeDisableUnsafePtrRestriction]
            public ReflectionData* data;
            public int count;
            public float3 camPos;
            public float dist;
            public int maximumProbe;
            public void Execute(int index)
            {
                VisibleReflectionProbe vis = allProbes[index];
                float dstSq = dist + length(vis.bounds.extents);
                dstSq *= dstSq;
               /* if (dstSq < lengthsq(camPos - (float3)vis.bounds.center))
                    return;*/
                int i = System.Threading.Interlocked.Increment(ref count) - 1;
                if (i >= maximumProbe)
                    return;
                ref ReflectionData dt = ref data[i];
                dt.blendDistance = vis.blendDistance;
                float4x4 localToWorld = vis.localToWorldMatrix;
                dt.minExtent = (float3)vis.bounds.extents - dt.blendDistance * 0.5f;
                dt.maxExtent = (float3)vis.bounds.extents + dt.blendDistance * 0.5f;
                dt.boxProjection = vis.isBoxProjection ? 1 : 0;
                dt.position = localToWorld.c3.xyz;
                dt.hdr = vis.hdrData;
            }
        }

    }
}
