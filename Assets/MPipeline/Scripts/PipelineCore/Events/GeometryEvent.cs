using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Rendering;
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
        public Material debugMat;
        private PropertySetEvent proper;
        protected override void Init(PipelineResources resources)
        {
            linearMat = new Material(resources.shaders.linearDepthShader);
            linearDrawerMat = new Material(resources.shaders.linearDrawerShader);
            if (useHiZ)
            {
                hizDepth.InitHiZ(resources, new Vector2(Screen.width, Screen.height));
                clusterMat = new Material(resources.shaders.clusterRenderShader);
            }
            proper = RenderPipeline.GetEvent<PropertySetEvent>();
        }
        public override bool CheckProperty()
        {
            
            if (useHiZ)
            {
                return linearMat && linearDrawerMat && hizDepth.Check() && clusterMat;
            }
            else
                return linearMat && linearDrawerMat;
        }
        protected override void Dispose()
        {
            DestroyImmediate(linearMat);
            DestroyImmediate(linearDrawerMat);
            
            if (useHiZ)
            {
                hizDepth.DisposeHiZ();
                DestroyImmediate(clusterMat);
            }
            linearMat = null;
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
            
            if (useHiZ)
            {
                HizOcclusionData hizOccData = IPerCameraData.GetProperty(cam, () => new HizOcclusionData());
                SceneController.DrawClusterOccDoubleCheck(ref options, ref proper.cullResults, ref hizDepth, hizOccData, clusterMat, linearMat, cam, ref data);
            }
            else SceneController.DrawCluster(ref options, ref cam.targets, ref data, cam.cam, ref proper.cullResults);
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