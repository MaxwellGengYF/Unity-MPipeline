using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Rendering;
using Unity.Mathematics;
using Unity.Jobs;
using static Unity.Mathematics.math;

namespace MPipeline
{
    [CreateAssetMenu(menuName = "GPURP Events/Geometry")]
    [RequireEvent(typeof(PropertySetEvent))]
    public unsafe sealed class GeometryEvent : PipelineEvent
    {
        public const bool useHiZ = true;
        HizDepth hizDepth;
        Material linearDrawerMat;
        Material linearMat;
        Material clusterMat;
        private PropertySetEvent proper;
        private AOEvents ao;
        private ReflectionEvent reflection;
        private NativeList_Int gbufferCullResults;
        private JobHandle cullHandle;
        private DecalEvent decalEvt;
        private CommandBuffer m_afterGeometryBuffer = null;
        private bool needUpdateGeometryBuffer = false;
        public CommandBuffer afterGeometryBuffer
        {
            get
            {
                if (m_afterGeometryBuffer == null) m_afterGeometryBuffer = new CommandBuffer();
                needUpdateGeometryBuffer = true;
                return m_afterGeometryBuffer;
            }
        }
        protected override void Init(PipelineResources resources)
        {
            decalEvt.Init(resources);
            linearMat = new Material(resources.shaders.linearDepthShader);
            linearDrawerMat = new Material(resources.shaders.linearDrawerShader);
            if (useHiZ)
            {
#if UNITY_EDITOR
                if (Application.isPlaying)
                {
#endif
                    hizDepth.InitHiZ(resources);
                    clusterMat = new Material(resources.shaders.clusterRenderShader);
#if UNITY_EDITOR
                }
#endif
            }
            ao = RenderPipeline.GetEvent<AOEvents>();
            reflection = RenderPipeline.GetEvent<ReflectionEvent>();
            proper = RenderPipeline.GetEvent<PropertySetEvent>();
        }
        public override bool CheckProperty()
        {

            if (useHiZ && Application.isPlaying)
            {
                return linearMat && linearDrawerMat && clusterMat;
            }
            else
                return linearMat && linearDrawerMat;
        }
        protected override void Dispose()
        {
            DestroyImmediate(linearMat);
            DestroyImmediate(linearDrawerMat);
            decalEvt.Dispose();
            if (useHiZ)
            {
                DestroyImmediate(clusterMat);
            }
            if (m_afterGeometryBuffer != null)
            {
                m_afterGeometryBuffer.Dispose();
                m_afterGeometryBuffer = null;
            }
            linearMat = null;
        }
        public override void PreRenderFrame(PipelineCamera cam, ref PipelineCommandData data)
        {
            gbufferCullResults = new NativeList_Int(CustomDrawRequest.drawGBufferList.Length, Allocator.Temp);
            cullHandle = new CustomRendererCullJob
            {
                cullResult = gbufferCullResults,
                frustumPlanes = (float4*)proper.frustumPlanes.Ptr(),
                indexBuffer = CustomDrawRequest.drawGBufferList
            }.Schedule(CustomDrawRequest.drawGBufferList.Length, max(1, CustomDrawRequest.drawGBufferList.Length / 4));
            decalEvt.PreRenderFrame(cam, ref data);
        }

        public override void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data)
        {
            CommandBuffer buffer = data.buffer;
            RenderClusterOptions options = new RenderClusterOptions
            {
                command = buffer,
                frustumPlanes = proper.frustumPlanes,
                cullingShader = data.resources.shaders.gpuFrustumCulling,
                terrainCompute = data.resources.shaders.terrainCompute
            };
            FilteringSettings alphaTestFilter = new FilteringSettings
            {
                layerMask = cam.cam.cullingMask,
                renderingLayerMask = 1,
                renderQueueRange = new RenderQueueRange(2450, 2499)
            };
            FilteringSettings opaqueFilter = new FilteringSettings
            {
                layerMask = cam.cam.cullingMask,
                renderingLayerMask = 1,
                renderQueueRange = new RenderQueueRange(2000, 2449)
            };
            DrawingSettings depthAlphaTestDrawSettings = new DrawingSettings(new ShaderTagId("Depth"),
                new SortingSettings(cam.cam) { criteria = SortingCriteria.OptimizeStateChanges })
            {
                perObjectData = UnityEngine.Rendering.PerObjectData.None,
                enableDynamicBatching = true,
                enableInstancing = false
            };
            DrawingSettings depthOpaqueDrawSettings = new DrawingSettings(new ShaderTagId("Depth"),
                new SortingSettings(cam.cam) { criteria = SortingCriteria.None })
            {
                perObjectData = UnityEngine.Rendering.PerObjectData.None,
                enableDynamicBatching = true,
                enableInstancing = false,
                overrideMaterial = proper.overrideOpaqueMaterial,
                overrideMaterialPassIndex = 1
            };

            DrawingSettings drawSettings = new DrawingSettings(new ShaderTagId("GBuffer"), new SortingSettings(cam.cam) { criteria = SortingCriteria.RenderQueue | SortingCriteria.OptimizeStateChanges })
            {
                perObjectData = UnityEngine.Rendering.PerObjectData.Lightmaps,
                enableDynamicBatching = true,
                enableInstancing = false
            };

            //Draw Depth Prepass
            data.buffer.SetRenderTarget(ShaderIDs._DepthBufferTexture);
            data.buffer.ClearRenderTarget(true, false, Color.black);
            cullHandle.Complete();
            var lst = CustomDrawRequest.allEvents;
            foreach (var i in gbufferCullResults)
            {
                lst[i].DrawDepthPrepass(buffer);
            }
            HizOcclusionData hizOccData = null;
            PipelineFunctions.UpdateFrustumMinMaxPoint(buffer, cam.frustumMinPoint, cam.frustumMaxPoint);
            if (useHiZ && SceneController.gpurpEnabled)
            {
#if UNITY_EDITOR
                if (Application.isPlaying)
                {
#endif
                    HizOcclusionData.GetHizOcclusionData getter = new HizOcclusionData.GetHizOcclusionData
                    {
                        screenWidth = cam.cam.pixelWidth
                    };

                    hizOccData = IPerCameraData.GetProperty<HizOcclusionData, HizOcclusionData.GetHizOcclusionData>(cam, getter);
                    hizOccData.UpdateWidth(cam.cam.pixelWidth);
                    SceneController.CullCluster_LastFrameDepthHiZ(ref options, hizOccData, cam);
                    buffer.DrawProceduralIndirect(Matrix4x4.identity, clusterMat, 2, MeshTopology.Triangles, SceneController.baseBuffer.instanceCountBuffer, 0);

#if UNITY_EDITOR
                }
#endif
            }
            RenderStateBlock depthBlock = new RenderStateBlock
            {
                depthState = new DepthState(true, CompareFunction.Less),
                mask = RenderStateMask.Depth
            };
            SceneController.RenderScene(ref data, ref opaqueFilter, ref depthOpaqueDrawSettings, ref proper.cullResults, ref depthBlock);
            data.context.DrawRenderers(proper.cullResults, ref depthAlphaTestDrawSettings, ref alphaTestFilter);
            decalEvt.FrameUpdate(cam, ref data);
            //Draw GBuffer
            data.buffer.SetRenderTarget(colors: cam.targets.gbufferIdentifier, depth: ShaderIDs._DepthBufferTexture);
            data.buffer.ClearRenderTarget(false, true, Color.black);


            foreach (var i in gbufferCullResults)
            {
                lst[i].DrawGBuffer(buffer);
            }
            if (useHiZ && SceneController.gpurpEnabled)
            {
#if UNITY_EDITOR
                if (Application.isPlaying)
                {
#endif
                    buffer.SetGlobalBuffer(ShaderIDs._MaterialBuffer, data.resources.clusterResources.vmManager.materialBuffer);
                    buffer.SetGlobalBuffer(ShaderIDs._TriangleMaterialBuffer, SceneController.baseBuffer.triangleMaterialBuffer);
                    buffer.SetGlobalTexture(ShaderIDs._GPURPMainTex, data.resources.clusterResources.rgbaPool.rt);
                    buffer.SetGlobalTexture(ShaderIDs._GPURPEmissionMap, data.resources.clusterResources.emissionPool.rt);
                    buffer.SetGlobalTexture(ShaderIDs._GPURPHeightMap, data.resources.clusterResources.heightPool.rt);
                    buffer.DrawProceduralIndirect(Matrix4x4.identity, clusterMat, 0, MeshTopology.Triangles, SceneController.baseBuffer.instanceCountBuffer, 0);
#if UNITY_EDITOR
                }
#endif
            }
            SceneController.RenderScene(ref data, ref opaqueFilter, ref drawSettings, ref proper.cullResults);
            if (MTerrain.current)
            {
                MTerrain.current.DrawTerrain(buffer, 0, proper.frustumPlanes, cam.frustumMinPoint, cam.frustumMaxPoint);
            }

            //Draw AlphaTest
            /* SortingSettings st = drawSettings.sortingSettings;
             st.criteria = SortingCriteria.SortingLayer | SortingCriteria.RenderQueue | SortingCriteria.OptimizeStateChanges;
             drawSettings.sortingSettings = st;*/
            SceneController.RenderScene(ref data, ref alphaTestFilter, ref drawSettings, ref proper.cullResults);
            //Draw Recheck HIZ Occlusion
            if (useHiZ && SceneController.gpurpEnabled)
            {
#if UNITY_EDITOR
                if (Application.isPlaying)
#endif
                    SceneController.DrawCluster_RecheckHiz(ref options, ref hizDepth, hizOccData, clusterMat, linearMat, cam);
            }
            //Draw Depth
            data.buffer.Blit(ShaderIDs._DepthBufferTexture, ShaderIDs._CameraDepthTexture);
            if (needUpdateGeometryBuffer)
            {
                needUpdateGeometryBuffer = false;
                data.ExecuteCommandBuffer();
                data.context.ExecuteCommandBuffer(m_afterGeometryBuffer);
                m_afterGeometryBuffer.Clear();
            }
        }
    }
    public class HizOcclusionData : IPerCameraData
    {
        public struct GetHizOcclusionData : IGetCameraData
        {
            public int screenWidth;
            public IPerCameraData Run()
            {
                return new HizOcclusionData(screenWidth);
            }
        }
        public RenderTexture historyDepth { get; private set; }
        public int targetWidth { get; private set; }
        public int mip { get; private set; }
        private int GetWidthFromScreen(int screenWidth)
        {
            int targetWidth;
            if (screenWidth >= 2048)
            {
                targetWidth = 1024;
                mip = 9;
            }
            else if (screenWidth >= 1024)
            {
                targetWidth = 512;
                mip = 8;
            }
            else
            {
                targetWidth = 256;
                mip = 7;
            }
            return targetWidth;
        }
        public HizOcclusionData(int screenWidth)
        {
            targetWidth = GetWidthFromScreen(screenWidth);
            historyDepth = new RenderTexture(targetWidth, targetWidth / 2, 0, RenderTextureFormat.RHalf, 9);
            historyDepth.useMipMap = true;
            historyDepth.autoGenerateMips = false;
            historyDepth.enableRandomWrite = true;
            historyDepth.wrapMode = TextureWrapMode.Clamp;
            historyDepth.filterMode = FilterMode.Point;
            historyDepth.Create();
        }

        public void UpdateWidth(int screenWidth)
        {
            int tar = GetWidthFromScreen(screenWidth);
            if (tar != targetWidth)
            {

                targetWidth = tar;
                historyDepth.Release();
                historyDepth.width = tar;
                historyDepth.height = tar / 2;
                historyDepth.Create();
            }
        }
        public override void DisposeProperty()
        {
            Object.DestroyImmediate(historyDepth);
        }
    }
}