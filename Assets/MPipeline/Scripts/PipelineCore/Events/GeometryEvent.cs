using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Rendering;
using Unity.Mathematics;
using static Unity.Mathematics.math;

namespace MPipeline
{
    [CreateAssetMenu(menuName = "GPURP Events/Geometry")]
    [RequireEvent(typeof(PropertySetEvent))]
    public unsafe class GeometryEvent : PipelineEvent
    {
        public const bool useHiZ = false;
        HizDepth hizDepth;
        Material linearDrawerMat;
        Material linearMat;
        Material clusterMat;
        private PropertySetEvent proper;
        public DecalEvent decal;
        public Material debugMat;
        private AOEvents ao;
        private ReflectionEvent reflection;
        private Material downSampleMat;
        private Material motionVecMat;
        private RenderTargetIdentifier[] downSampledGBuffers = new RenderTargetIdentifier[3];
        protected override void Init(PipelineResources resources)
        {
            linearMat = new Material(resources.shaders.linearDepthShader);
            linearDrawerMat = new Material(resources.shaders.linearDrawerShader);
            motionVecMat = new Material(resources.shaders.motionVectorShader);
            if (useHiZ)
            {
                hizDepth.InitHiZ(resources, new Vector2(Screen.width, Screen.height));
                clusterMat = new Material(resources.shaders.clusterRenderShader);
            }
            ao = RenderPipeline.GetEvent<AOEvents>();
            reflection = RenderPipeline.GetEvent<ReflectionEvent>();
            proper = RenderPipeline.GetEvent<PropertySetEvent>();
            decal.Init();
            downSampleMat = new Material(resources.shaders.depthDownSample);
        }
        public override bool CheckProperty()
        {

            if (useHiZ)
            {
                return linearMat && linearDrawerMat && hizDepth.Check() && clusterMat && motionVecMat;
            }
            else
                return linearMat && linearDrawerMat && motionVecMat;
        }
        protected override void Dispose()
        {
            DestroyImmediate(downSampleMat);
            DestroyImmediate(linearMat);
            DestroyImmediate(linearDrawerMat);

            if (useHiZ)
            {
                hizDepth.DisposeHiZ();
                DestroyImmediate(clusterMat);
            }
            linearMat = null;
        }
        public override void PreRenderFrame(PipelineCamera cam, ref PipelineCommandData data)
        {
            decal.PreRenderFrame(cam, ref data);
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
            FilteringSettings mvFilter = new FilteringSettings
            {
                layerMask = cam.cam.cullingMask,
                renderingLayerMask = 1,
                renderQueueRange = RenderQueueRange.opaque
            };
            DrawingSettings depthPrePassDrawSettings = new DrawingSettings(new ShaderTagId("Depth"), new SortingSettings(cam.cam) { criteria = SortingCriteria.QuantizedFrontToBack})
            {
                perObjectData = UnityEngine.Rendering.PerObjectData.None,
                enableDynamicBatching = false,
                enableInstancing = false
            };
            DrawingSettings drawSettings = new DrawingSettings(new ShaderTagId("GBuffer"), new SortingSettings(cam.cam) { criteria = SortingCriteria.CommonOpaque })
            {
                perObjectData = UnityEngine.Rendering.PerObjectData.Lightmaps,
                enableDynamicBatching = false,
                enableInstancing = false
            };
            DrawingSettings mvDraw = new DrawingSettings(new ShaderTagId("MotionVector"), new SortingSettings { criteria = SortingCriteria.None })
            {
                perObjectData = UnityEngine.Rendering.PerObjectData.None,
                enableDynamicBatching = false,
                enableInstancing = false
            };

            data.buffer.SetRenderTarget(colors: cam.targets.gbufferIdentifier, depth: ShaderIDs._DepthBufferTexture);
            data.buffer.ClearRenderTarget(true, true, Color.black);
            HizOcclusionData hizOccData;
            if (useHiZ)
            {
                hizOccData = IPerCameraData.GetProperty(cam, () => new HizOcclusionData());
                SceneController.DrawCluster_LastFrameDepthHiZ(ref options, hizOccData, clusterMat, cam);
            }
            
            SceneController.RenderScene(ref data, ref opaqueFilter, ref drawSettings, ref proper.cullResults);
            if (useHiZ)
            {
                SceneController.DrawCluster_RecheckHiz(ref options, ref hizDepth, hizOccData, clusterMat, linearMat, cam);
            }
            data.buffer.SetRenderTarget(ShaderIDs._DepthBufferTexture);
            SceneController.RenderScene(ref data, ref alphaTestFilter, ref depthPrePassDrawSettings, ref proper.cullResults);
            data.buffer.SetRenderTarget(colors: cam.targets.gbufferIdentifier, depth: ShaderIDs._DepthBufferTexture);
            SortingSettings st = drawSettings.sortingSettings;
            st.criteria = SortingCriteria.SortingLayer | SortingCriteria.RenderQueue;
            drawSettings.sortingSettings = st;
            SceneController.RenderScene(ref data, ref alphaTestFilter, ref drawSettings, ref proper.cullResults);
            data.buffer.Blit(ShaderIDs._DepthBufferTexture, ShaderIDs._CameraDepthTexture);


            data.buffer.SetRenderTarget(color: ShaderIDs._CameraMotionVectorsTexture, depth: ShaderIDs._DepthBufferTexture);
            SceneController.RenderScene(ref data, ref mvFilter, ref mvDraw, ref proper.cullResults);
            data.buffer.DrawMesh(GraphicsUtility.mesh, Matrix4x4.identity, motionVecMat, 0, 0);
            decal.FrameUpdate(cam, ref data);
            //Generate DownSampled GBuffer
            if ((ao != null && ao.Enabled) || (reflection != null && reflection.Enabled && reflection.ssrEvents.enabled))
            {
                int2 res = int2(cam.cam.pixelWidth, cam.cam.pixelHeight) / 2;
                data.buffer.GetTemporaryRT(ShaderIDs._DownSampledGBuffer1, res.x, res.y, 0, FilterMode.Point, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear, 1, false);
                data.buffer.GetTemporaryRT(ShaderIDs._DownSampledGBuffer2, res.x, res.y, 0, FilterMode.Point, RenderTextureFormat.ARGB2101010, RenderTextureReadWrite.Linear, 1, false);
                data.buffer.GetTemporaryRT(ShaderIDs._DownSampledDepthTexture, res.x, res.y, 0, FilterMode.Point, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear, 1, false);
                downSampledGBuffers[0] = ShaderIDs._DownSampledDepthTexture;
                downSampledGBuffers[1] = ShaderIDs._DownSampledGBuffer1;
                downSampledGBuffers[2] = ShaderIDs._DownSampledGBuffer2;
                data.buffer.SetRenderTarget(colors: downSampledGBuffers, depth: downSampledGBuffers[0]);
                data.buffer.DrawMesh(GraphicsUtility.mesh, Matrix4x4.identity, downSampleMat, 0, 0);
                //TODO
            }
        }
    }
    public class HizOcclusionData : IPerCameraData
    {
        public RenderTexture historyDepth { get; private set; }
        public Vector3 lastFrameCameraUp;
        public HizOcclusionData()
        {
            historyDepth = new RenderTexture(HizDepth.depthRes.x, HizDepth.depthRes.y, 0, RenderTextureFormat.R16, RenderTextureReadWrite.Linear);
            historyDepth.useMipMap = true;
            historyDepth.autoGenerateMips = false;
            historyDepth.enableRandomWrite = false;
            historyDepth.wrapMode = TextureWrapMode.Clamp;
            historyDepth.filterMode = FilterMode.Point;
            historyDepth.Create();
            lastFrameCameraUp = Vector3.up;
        }
        public override void DisposeProperty()
        {
            Object.DestroyImmediate(historyDepth);
        }
    }
}