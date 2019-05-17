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
    [System.Serializable]
    public unsafe struct DecalEvent
    {
        private const int maxDecalPerCluster = 16;
        private DecalCullJob cullJob;
        private NativeArray<DecalData> decalCullResults;
        private JobHandle handle;
        public float availiableDistance;
        private RenderTargetIdentifier[] decalTargets;
        private PropertySetEvent proper;
        public void Init()
        {
            proper = RenderPipeline.GetEvent<PropertySetEvent>();
            decalTargets = new RenderTargetIdentifier[2];
        }

        public void PreRenderFrame(PipelineCamera cam, ref PipelineCommandData data)
        {
            decalCullResults = new NativeArray<DecalData>(DecalBase.allDecalCount, Allocator.Temp);
            cullJob = new DecalCullJob
            {
                count = 0,
                decalDatas = (DecalData*)decalCullResults.GetUnsafePtr(),
                frustumPlanes = (float4*)proper.frustumPlanes.Ptr(),
                availiableDistanceSqr = availiableDistance * availiableDistance,
                camPos = cam.cam.transform.position
            };
            handle = cullJob.ScheduleRef(DecalBase.allDecalCount, 32);
        }

        public void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data)
        {
            CommandBuffer buffer = data.buffer;
            handle.Complete();
            buffer.GetTemporaryRT(ShaderIDs._BackupAlbedoMap, cam.cam.pixelWidth, cam.cam.pixelHeight, 0, FilterMode.Point, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear, 1, false);
            buffer.GetTemporaryRT(ShaderIDs._BackupNormalMap, cam.cam.pixelWidth, cam.cam.pixelHeight, 0, FilterMode.Point, RenderTextureFormat.ARGB2101010, RenderTextureReadWrite.Linear, 1, false);
            buffer.CopyTexture(cam.targets.gbufferIndex[2], 0, 0, ShaderIDs._BackupNormalMap, 0, 0);
            buffer.CopyTexture(cam.targets.gbufferIndex[0], 0, 0, ShaderIDs._BackupAlbedoMap, 0, 0);
            decalTargets[0] = cam.targets.gbufferIndex[0];
            decalTargets[1] = cam.targets.gbufferIndex[2];
            buffer.SetRenderTarget(colors: decalTargets, depth: ShaderIDs._DepthBufferTexture);
            DecalData* resulPtr = decalCullResults.Ptr();
            for (int i = 0; i < cullJob.count; ++i)
            {
                ref DecalData decal = ref resulPtr[i];
                DecalBase dec = MUnsafeUtility.GetObject<DecalBase>(decal.comp);
                dec.DrawDecal(buffer);
            }
            buffer.ReleaseTemporaryRT(ShaderIDs._BackupAlbedoMap);
            buffer.ReleaseTemporaryRT(ShaderIDs._BackupNormalMap);
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
                ref DecalData data = ref DecalBase.GetData(index);
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
