using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
namespace MPipeline
{
    [CreateAssetMenu(menuName = "GPURP Events/Transparent")]
    [RequireEvent(typeof(PropertySetEvent))]
    public unsafe sealed class TransEvent : PipelineEvent
    {
        private RenderTargetIdentifier[] transparentOutput = new RenderTargetIdentifier[2];
        private PropertySetEvent proper;
        private NativeList<System.UIntPtr> commandBuffers;
        public void AddCommand(CommandBuffer buffer)
        {
            commandBuffers.Add(new System.UIntPtr(MUnsafeUtility.GetManagedPtr(buffer)));
        }
        public void RemoveCommand(CommandBuffer buffer)
        {
            commandBuffers.RemoveElement(new System.UIntPtr(MUnsafeUtility.GetManagedPtr(buffer)), (a, b) => a == b);
        }
        protected override void Init(PipelineResources resources)
        {
            commandBuffers = new NativeList<System.UIntPtr>(10, Unity.Collections.Allocator.Persistent);
            proper = RenderPipeline.GetEvent<PropertySetEvent>();
        }
        public override bool CheckProperty()
        {
            return true;
        }
        protected override void Dispose()
        {
            commandBuffers.Dispose();
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
            data.buffer.CopyTexture(cam.targets.renderTargetIdentifier, 0, 0, cam.targets.backupIdentifier, 0, 0);
            data.buffer.SetGlobalTexture(ShaderIDs._GrabTexture, cam.targets.backupIdentifier);
            transparentOutput[0] = cam.targets.renderTargetIdentifier;
            transparentOutput[1] = ShaderIDs._CameraDepthTexture;
            data.buffer.SetRenderTarget(colors: transparentOutput, depth: ShaderIDs._DepthBufferTexture);
            data.ExecuteCommandBuffer();
            foreach(var i in commandBuffers)
            {
                CommandBuffer cb = MUnsafeUtility.GetObject<CommandBuffer>(i.ToPointer());
                data.context.ExecuteCommandBuffer(cb);
            }
            data.context.DrawRenderers(proper.cullResults, ref drawSettings, ref filter);
        }
    }
}