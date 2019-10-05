using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using UnityEngine.Rendering;
using Unity.Jobs;

namespace MPipeline
{
    [CreateAssetMenu(menuName = "GPURP Events/Skybox")]
    [RequireEvent(typeof(PropertySetEvent))]
    public class SkyboxEvent : PipelineEvent
    {
        private static readonly int _InvSkyVP = Shader.PropertyToID("_InvSkyVP");
        private static readonly int _LastSkyVP = Shader.PropertyToID("_LastSkyVP");
        private CalculateSkyboxMatrix job;
        private JobHandle handle;
        private RenderTargetIdentifier[] targetIdentifiers;
        protected override void Dispose()
        {
        }
        protected override void Init(PipelineResources resources)
        {
            targetIdentifiers = new RenderTargetIdentifier[2];
        }

        public override bool CheckProperty()
        {
            return true;
        }
        public override void PreRenderFrame(PipelineCamera cam, ref PipelineCommandData data)
        {
            job.up = cam.cam.transform.up;
            job.forward = cam.cam.transform.forward;
            job.right = cam.cam.transform.right;
            job.proj = GL.GetGPUProjectionMatrix(cam.cam.nonJitteredProjectionMatrix, false);
            job.isOrtho = cam.cam.orthographic;
            handle = job.ScheduleRefBurst();
        }
        public override void FrameUpdate(PipelineCamera camera, ref PipelineCommandData data)
        {
            CommandBuffer buffer = data.buffer;
            handle.Complete();
            SkyboxPreviewMatrix last = IPerCameraData.GetProperty(camera, () => new SkyboxPreviewMatrix());
            buffer.SetGlobalMatrix(_LastSkyVP, last.lastViewProj);
            targetIdentifiers[0] = camera.targets.renderTargetIdentifier;
            targetIdentifiers[1] = ShaderIDs._CameraMotionVectorsTexture;
            buffer.SetRenderTarget(colors: targetIdentifiers, depth: ShaderIDs._DepthBufferTexture);
            data.ExecuteCommandBuffer();
            data.context.DrawSkybox(camera.cam);
            last.lastViewProj = job.viewProj;
        }
        [Unity.Burst.BurstCompile]
        private unsafe struct CalculateSkyboxMatrix : IJob
        {
            public float3 forward;
            public float3 up;
            public float3 right;
            public bool isOrtho;
            public float4x4 proj;
            public float4x4 viewProj;

            public void Execute()
            {
                if (isOrtho)
                {
                    OrthoCam cam = new OrthoCam
                    {
                        forward = forward,
                        up = up,
                        right = right,
                        position = 0
                    };
                    cam.UpdateTRSMatrix();
                    viewProj = mul(proj, cam.worldToCameraMatrix);
                }
                else
                {
                    PerspCam cam = new PerspCam
                    {
                        forward = forward,
                        up = up,
                        right = right,
                        position = 0
                    };
                    cam.UpdateTRSMatrix();
                    viewProj = mul(proj, cam.worldToCameraMatrix);
                }
            }
        }
        public class SkyboxPreviewMatrix : IPerCameraData
        {
            public float4x4 lastViewProj;
            public SkyboxPreviewMatrix()
            {
                lastViewProj = Matrix4x4.identity;
            }
            public override void DisposeProperty()
            {
                
            }
        }
    }
}
