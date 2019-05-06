using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace MPipeline
{
    [CreateAssetMenu(menuName = "GPURP Events/Unlit")]
    public class UnlitEvent : PipelineEvent
    {
        public string passName = "Depth";
       // public Color defaultColor = Color.black;
        protected override void Init(PipelineResources resources)
        {

        }
        protected override void Dispose()
        {

        }

        public override bool CheckProperty()
        {
            return true;
        }
        public override void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data)
        {
            ScriptableCullingParameters cullParams;
            if (!cam.cam.TryGetCullingParameters(out cullParams)) return;
            cullParams.cullingOptions = cam.cam.useOcclusionCulling ? CullingOptions.OcclusionCull: CullingOptions.None;
            CullingResults cullReslt = data.context.Cull(ref cullParams);
            data.buffer.SetRenderTarget(cam.cameraTarget);
            data.buffer.ClearRenderTarget(true, true, new Color(float.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue));
            FilteringSettings filterSettings = new FilteringSettings
            {
                layerMask = cam.cam.cullingMask,
                renderingLayerMask = 1,
                renderQueueRange = RenderQueueRange.opaque
            };
            DrawingSettings drawSettings = new DrawingSettings(new ShaderTagId("GBuffer"), new SortingSettings(cam.cam) { criteria = SortingCriteria.CommonOpaque })
            {
                perObjectData = UnityEngine.Rendering.PerObjectData.Lightmaps
            };
            SceneController.RenderScene(ref data, ref filterSettings, ref drawSettings, ref cullReslt);
        }
    }
}