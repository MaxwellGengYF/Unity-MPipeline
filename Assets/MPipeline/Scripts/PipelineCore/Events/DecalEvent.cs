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
    [CreateAssetMenu(menuName = "GPURP Events/Decal")]
    [RequireEvent(typeof(PropertySetEvent))]
    public unsafe sealed class DecalEvent : PipelineEvent
    {
        private const int maxDecalPerCluster = 16;
        public const bool useRain = false;
        public RainRT rain;
        private DecalCullJob cullJob;
        private NativeArray<DecalData> decalCullResults;
        private JobHandle handle;
        public float availiableDistance = 30;
        private Material decalMat;
        private RenderTargetIdentifier[] decalTargets;
        private PropertySetEvent proper;
        public override bool CheckProperty()
        {
            if (useRain && !rain.Check())
                return false;
            return decalMat;
        }

        protected override void Init(PipelineResources resources)
        {
            proper = RenderPipeline.GetEvent<PropertySetEvent>();
            decalMat = new Material(resources.shaders.decalShader);
            decalTargets = new RenderTargetIdentifier[2];
            if (useRain)
            {
                rain.Init(resources);
            }
        }

        public override void PreRenderFrame(PipelineCamera cam, ref PipelineCommandData data)
        {
            decalCullResults = new NativeArray<DecalData>(Decal.allDecalCount, Allocator.Temp);
            cullJob = new DecalCullJob
            {
                count = 0,
                decalDatas = (DecalData*)decalCullResults.GetUnsafePtr(),
                frustumPlanes = (float4*)proper.frustumPlanes.Ptr(),
                availiableDistanceSqr = availiableDistance * availiableDistance,
                camPos = cam.cam.transform.position
            };
            handle = cullJob.ScheduleRef(Decal.allDecalCount, 32);
        }

        public override void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data)
        {
            CommandBuffer buffer = data.buffer;
            if (useRain)
                rain.Update(buffer);
            handle.Complete();
            buffer.GetTemporaryRT(ShaderIDs._BackupAlbedoMap, cam.cam.pixelWidth, cam.cam.pixelHeight, 0, FilterMode.Point, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear, 1, false);
            buffer.GetTemporaryRT(ShaderIDs._BackupNormalMap, cam.cam.pixelWidth, cam.cam.pixelHeight, 0, FilterMode.Point, RenderTextureFormat.ARGB2101010, RenderTextureReadWrite.Linear, 1, false);
            buffer.CopyTexture(cam.targets.gbufferIndex[2], 0, 0, ShaderIDs._BackupNormalMap, 0, 0);
            buffer.CopyTexture(cam.targets.gbufferIndex[0], 0, 0, ShaderIDs._BackupAlbedoMap, 0, 0);
            decalTargets[0] = cam.targets.gbufferIndex[0];
            decalTargets[1] = cam.targets.gbufferIndex[2];
            buffer.SetRenderTarget(decalTargets, cam.targets.depthBuffer);
            DecalData* resulPtr = decalCullResults.Ptr();
            for (int i = 0; i < cullJob.count; ++i)
            {
                ref DecalData decal = ref resulPtr[i];
                Decal dec = MUnsafeUtility.GetObject<Decal>(decal.comp);
                buffer.SetGlobalTexture(ShaderIDs._DecalAlbedo, dec.decalTex);
                buffer.SetGlobalTexture(ShaderIDs._DecalNormal, dec.normalTex);
                buffer.SetGlobalVector(ShaderIDs._Color, (Vector3)decal.color);
                buffer.SetGlobalVector(ShaderIDs._OpaqueScale, new Vector2(decal.opaque, decal.normalScale));
                buffer.DrawMesh(GraphicsUtility.cubeMesh, decal.rotation, decalMat, 0, 0);
            }
        }

        protected override void Dispose()
        {
            if (useRain)
                rain.Dispose();
            DestroyImmediate(decalMat);
        }
        private struct DecalCullJob : IJobParallelFor
        {
            [NativeDisableUnsafePtrRestriction]
            public float4* frustumPlanes;
            public DecalData* decalDatas;
            public int count;
            public float availiableDistanceSqr;
            public float3 camPos;
            public void Execute(int index)
            {
                ref DecalData data = ref Decal.GetData(index);
                float3x3 rotation = float3x3(data.rotation.c0.xyz, data.rotation.c1.xyz, data.rotation.c2.xyz);
                if(lengthsq(camPos - data.position) < availiableDistanceSqr && VectorUtility.BoxIntersect(rotation, data.position, frustumPlanes, 6))
                {
                    int currentInd = System.Threading.Interlocked.Increment(ref count) - 1;
                    decalDatas[currentInd] = data;
                }
            }
        }
    }
}
