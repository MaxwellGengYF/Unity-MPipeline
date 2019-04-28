using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine;
namespace MPipeline
{
    [CreateAssetMenu(menuName = "GPURP Events/Debug Mode")]
    public class DebugEvent : PipelineEvent
    {
        public Material debugMat;
        public override bool CheckProperty()
        {
            return true;
        }
        protected override void Dispose()
        {
            
        }

        protected override void Init(PipelineResources resources)
        {
            
        }

        public override void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data)
        {
            data.buffer.Blit(cam.targets.renderTargetIdentifier, cam.cameraTarget);
        }
    }
}
