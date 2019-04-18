using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using UnityEngine.Rendering;
namespace MPipeline
{
    [CreateAssetMenu(menuName = "GPURP Events/Skybox")]
    [RequireEvent(typeof(PropertySetEvent))]
    public class SkyboxEvent : PipelineEvent
    {
        public Material skyboxMaterial;
        private static readonly int _LastSkyVP = Shader.PropertyToID("_LastSkyVP");
        private static readonly int _InvSkyVP = Shader.PropertyToID("_InvSkyVP");
        private RenderTargetIdentifier[] targets = new RenderTargetIdentifier[2];
        protected override void Dispose()
        {
        }
        protected override void Init(PipelineResources resources)
        {
        }

        public override bool CheckProperty()
        {
            return true;
        }
        public override void FrameUpdate(PipelineCamera camera, ref PipelineCommandData data)
        {
            SkyboxMatrixData skyData = IPerCameraData.GetProperty(camera, () => new SkyboxMatrixData());
            float4x4 proj = GL.GetGPUProjectionMatrix(camera.cam.nonJitteredProjectionMatrix, false);
            float4x4 viewProj;
            if (camera.cam.orthographic)
            {
                OrthoCam cam = new OrthoCam
                {
                    forward = camera.cam.transform.forward,
                    up = camera.cam.transform.up,
                    right = camera.cam.transform.right,
                    position = 0
                };
                cam.UpdateTRSMatrix();
                viewProj = mul(proj, cam.worldToCameraMatrix);
            }
            else
            {
                PerspCam cam = new PerspCam
                {
                    forward = camera.cam.transform.forward,
                    up = camera.cam.transform.up,
                    right = camera.cam.transform.right,
                    position = 0
                };
                cam.UpdateTRSMatrix();
                viewProj = mul(proj, cam.worldToCameraMatrix);
            }
            CommandBuffer buffer = data.buffer;
            buffer.SetGlobalMatrix(_InvSkyVP, inverse(viewProj));
            buffer.SetGlobalMatrix(_LastSkyVP, skyData.lastVP);
            targets[0] = camera.targets.renderTargetIdentifier;
            targets[1] = camera.targets.motionVectorTexture;
            buffer.SetRenderTarget(colors: targets, depth: camera.targets.depthBuffer);
            buffer.DrawMesh(GraphicsUtility.mesh, Matrix4x4.identity, skyboxMaterial, 0, 0);
            skyData.lastVP = viewProj;
        }

        public class SkyboxMatrixData : IPerCameraData
        {
            public float4x4 lastVP;
            public SkyboxMatrixData()
            {
                lastVP = Matrix4x4.identity;
            }
            public override void DisposeProperty()
            {

            }
        }
    }
}
