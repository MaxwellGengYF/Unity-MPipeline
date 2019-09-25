using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Jobs;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using UnityEngine.Experimental.Rendering;
namespace MPipeline
{
    [CreateAssetMenu(menuName = "GPURP Events/Transparent")]
    [RequireEvent(typeof(PropertySetEvent))]
    public unsafe sealed class TransEvent : PipelineEvent
    {
        private RenderTargetIdentifier[] transparentOutput = new RenderTargetIdentifier[2];
        private PropertySetEvent proper;
       // public RapidBlur blur;
        private JobHandle cullJob;
        private NativeList_Int customCullResults;
        protected override void Init(PipelineResources resources)
        {
            proper = RenderPipeline.GetEvent<PropertySetEvent>();
         //   blur.Init(resources.shaders.rapidBlurShader);
        }
        public override bool CheckProperty()
        {
            return true;//blur.Check();
        }
        protected override void Dispose()
        {
           // blur.Dispose();
        }
        public override void PreRenderFrame(PipelineCamera cam, ref PipelineCommandData data)
        {
            customCullResults = new NativeList_Int(CustomDrawRequest.drawTransparentList.Length, Unity.Collections.Allocator.Temp);
            cullJob = new CustomRendererCullJob
            {
                cullResult = customCullResults,
                frustumPlanes = (float4*)proper.frustumPlanes.Ptr(),
                indexBuffer = CustomDrawRequest.drawTransparentList
            }.Schedule(CustomDrawRequest.drawTransparentList.Length, max(1, CustomDrawRequest.drawTransparentList.Length / 4));
        }
        public override void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data)
        {
            SortingSettings sortSettings = new SortingSettings(cam.cam);
            sortSettings.criteria = SortingCriteria.CommonTransparent;
            DrawingSettings drawSettings = new DrawingSettings(new ShaderTagId("Transparent"), sortSettings)
            {
                enableDynamicBatching = false,
                enableInstancing = false,
                perObjectData = UnityEngine.Rendering.PerObjectData.None
            };
            FilteringSettings filter = new FilteringSettings
            {
                excludeMotionVectorObjects = false,
                layerMask = cam.cam.cullingMask,
                renderQueueRange = RenderQueueRange.transparent,
                renderingLayerMask = (uint)cam.cam.cullingMask,
                sortingLayerRange = SortingLayerRange.all
            };
          //  int blurTex = blur.Render(data.buffer, new Vector2Int(cam.cam.pixelWidth, cam.cam.pixelHeight), cam.targets.renderTargetIdentifier);
         //   data.buffer.SetGlobalTexture(ShaderIDs._GrabTexture, blurTex);
            transparentOutput[0] = cam.targets.renderTargetIdentifier;
            transparentOutput[1] = ShaderIDs._CameraDepthTexture;
            data.buffer.SetRenderTarget(colors: transparentOutput, depth: ShaderIDs._DepthBufferTexture);
            cullJob.Complete();
            var lst = CustomDrawRequest.allEvents;
            foreach (var i in customCullResults)
            {
                lst[i].DrawTransparent(data.buffer);
            }
            data.ExecuteCommandBuffer();
            data.context.DrawRenderers(proper.cullResults, ref drawSettings, ref filter);
           // data.buffer.ReleaseTemporaryRT(blurTex);
        }
    }
}