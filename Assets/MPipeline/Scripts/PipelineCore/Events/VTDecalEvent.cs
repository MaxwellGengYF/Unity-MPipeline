using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace MPipeline
{
    [CreateAssetMenu(menuName = "GPURP Events/VT Decal Event")]
    public unsafe sealed class VTDecalEvent : PipelineEvent
    {
        public Color clearColor = new Color(0, 0, 0, 0);
        protected override void Init(PipelineResources resources)
        {
            
        }

        public override bool CheckProperty()
        {
            return true;
        }
        protected override void Dispose()
        {
            
        }

        public override void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data)
        {
            CommandBuffer buffer = data.buffer;
            buffer.SetRenderTarget(cam.cameraTarget);
            buffer.ClearRenderTarget(true, true, clearColor);
            ScriptableCullingParameters cullParam;
            if (!cam.cam.TryGetCullingParameters(out cullParam)) return;
            cullParam.reflectionProbeSortingCriteria = ReflectionProbeSortingCriteria.None;
            cullParam.cullingOptions = CullingOptions.None;
            CullingResults results = data.context.Cull(ref cullParam);
            data.ExecuteCommandBuffer();
            FilteringSettings filter = new FilteringSettings
            {
                excludeMotionVectorObjects = false,
                layerMask = cam.cam.cullingMask,
                renderingLayerMask = 1,
                renderQueueRange = new RenderQueueRange(1000, 5000)
            };
            DrawingSettings draw = new DrawingSettings(new ShaderTagId("Decal"),
                new SortingSettings(cam.cam) { criteria = SortingCriteria.QuantizedFrontToBack | SortingCriteria.RenderQueue })
            {
                perObjectData = UnityEngine.Rendering.PerObjectData.None,
                enableDynamicBatching = false,
                enableInstancing = false
            };
            data.context.DrawRenderers(results, ref draw, ref filter);
        }
    }
}