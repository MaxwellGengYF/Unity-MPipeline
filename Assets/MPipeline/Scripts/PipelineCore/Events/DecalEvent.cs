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
        private NativeArray<DecalStrct> decalCullResults;
        private JobHandle handle;
        private PropertySetEvent proper;
        private LightingEvent lightingEvt;
        private ComputeBuffer decalBuffer;
        private ComputeBuffer decalIndexBuffer;
        private ComputeShader cbdrShader;
        public DecalTexture[] textures;
        private RenderTexture decalAlbedoAtlas;
        private RenderTexture decalNormalAtlas;
        private Material copyMat;
        const int INITCOUNT = 20;
        public int atlasSize;
        private struct DecalStrct
        {
            public float3x3 rotation;
            public float4x4 worldToLocal;
            public float3 position;
            public int index;
        }
        [System.Serializable]
        public struct DecalTexture
        {
            public Texture albedoTex;
            public Texture normalTex;
        }

        public void Init(PipelineResources res)
        {
            cbdrShader = res.shaders.cbdrShader;
            copyMat = new Material(res.shaders.copyShader);
            proper = RenderPipeline.GetEvent<PropertySetEvent>();
            lightingEvt = RenderPipeline.GetEvent<LightingEvent>();
            decalBuffer = new ComputeBuffer(INITCOUNT, sizeof(DecalStrct));
            decalIndexBuffer = new ComputeBuffer(CBDRSharedData.XRES * CBDRSharedData.YRES * CBDRSharedData.ZRES * (CBDRSharedData.MAXLIGHTPERCLUSTER + 1), sizeof(int));
            decalAlbedoAtlas = new RenderTexture(new RenderTextureDescriptor
            {
                colorFormat = RenderTextureFormat.ARGB32,
                dimension = TextureDimension.Tex2DArray,
                msaaSamples = 1,
                width = atlasSize,
                height = atlasSize,
                volumeDepth = max(1, textures.Length)
            });
            decalNormalAtlas = new RenderTexture(new RenderTextureDescriptor
            {
                colorFormat = RenderTextureFormat.RGHalf,
                dimension = TextureDimension.Tex2DArray,
                msaaSamples = 1,
                width = atlasSize,
                height = atlasSize,
                volumeDepth = max(1, textures.Length)
            });
            for(int i = 0; i < textures.Length; ++i)
            {
                Graphics.SetRenderTarget(decalAlbedoAtlas, 0, CubemapFace.Unknown, i);
                copyMat.SetTexture(ShaderIDs._MainTex, textures[i].albedoTex);
                copyMat.SetPass(0);
                Graphics.DrawMeshNow(GraphicsUtility.mesh, Matrix4x4.identity);
                Graphics.SetRenderTarget(decalNormalAtlas, 0, CubemapFace.Unknown, i);
                copyMat.SetTexture(ShaderIDs._MainTex, textures[i].normalTex);
                copyMat.SetPass(1);
                Graphics.DrawMeshNow(GraphicsUtility.mesh, Matrix4x4.identity);
            }
            Object.DestroyImmediate(copyMat);
        }

        public void Dispose()
        {
            decalBuffer.Dispose();
            decalIndexBuffer.Dispose();
            Object.DestroyImmediate(decalNormalAtlas);
            Object.DestroyImmediate(decalAlbedoAtlas);
            
        }

        public void PreRenderFrame(PipelineCamera cam, ref PipelineCommandData data)
        {
            decalCullResults = new NativeArray<DecalStrct>(Decal.allDecalCount, Allocator.Temp);
            cullJob = new DecalCullJob
            {
                count = 0,
                decalDatas = (DecalStrct*)decalCullResults.GetUnsafePtr(),
                frustumPlanes = (float4*)proper.frustumPlanes.Ptr(),
                availiableDistanceSqr = lightingEvt.cbdrDistance,
                camPos = cam.cam.transform.position
            };
            handle = cullJob.ScheduleRef(Decal.allDecalCount, 32);
        }

        public void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data)
        {
            CommandBuffer buffer = data.buffer;
            handle.Complete();
            DecalStrct* resulPtr = decalCullResults.Ptr();
            if (cullJob.count > decalBuffer.count)
            {
                int oldCount = decalBuffer.count;
                decalBuffer.Dispose();
                decalBuffer = new ComputeBuffer((int)max(oldCount * 1.5f, cullJob.count), sizeof(DecalStrct));
            }
            decalBuffer.SetData(decalCullResults, 0, 0, cullJob.count);
            buffer.SetComputeTextureParam(cbdrShader, CBDRSharedData.DecalCull, ShaderIDs._XYPlaneTexture, lightingEvt.cbdr.xyPlaneTexture);
            buffer.SetComputeTextureParam(cbdrShader, CBDRSharedData.DecalCull, ShaderIDs._ZPlaneTexture, lightingEvt.cbdr.zPlaneTexture);
            buffer.SetComputeBufferParam(cbdrShader, CBDRSharedData.DecalCull, ShaderIDs._AllDecals, decalBuffer);
            buffer.SetComputeBufferParam(cbdrShader, CBDRSharedData.DecalCull, ShaderIDs._DecalIndexBuffer, decalIndexBuffer);
            buffer.SetComputeIntParam(cbdrShader, ShaderIDs._DecalCount, cullJob.count);
            buffer.DispatchCompute(cbdrShader, CBDRSharedData.DecalCull, 1, 1, CBDRSharedData.ZRES);
            buffer.SetGlobalTexture(ShaderIDs._DecalAtlas, decalAlbedoAtlas);
            buffer.SetGlobalTexture(ShaderIDs._DecalNormalAtlas, decalNormalAtlas);
            buffer.SetGlobalBuffer(ShaderIDs._DecalIndexBuffer, decalIndexBuffer);
            buffer.SetGlobalBuffer(ShaderIDs._AllDecals, decalBuffer);
        }
        private struct DecalCullJob : IJobParallelFor
        {
            [NativeDisableUnsafePtrRestriction]
            public float4* frustumPlanes;
            public DecalStrct* decalDatas;
            public int count;
            public float availiableDistanceSqr;
            public float3 camPos;
            public void Execute(int index)
            {
                ref DecalData data = ref Decal.GetData(index);
                float3x3 rotation = float3x3(data.rotation.c0.xyz, data.rotation.c1.xyz, data.rotation.c2.xyz);
                if (lengthsq(camPos - data.position) < availiableDistanceSqr && VectorUtility.BoxIntersect(rotation, data.position, frustumPlanes, 6))
                {
                    int currentInd = System.Threading.Interlocked.Increment(ref count) - 1;
                    ref DecalStrct str = ref decalDatas[currentInd];
                    str.rotation = rotation;
                    str.position = data.position;
                    str.index = data.index;
                    str.worldToLocal = data.worldToLocal;
                }
            }
        }
    }
}
