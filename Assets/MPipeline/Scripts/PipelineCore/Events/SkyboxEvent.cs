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
        private static readonly int _InvSkyVP = Shader.PropertyToID("_InvSkyVP");
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
            buffer.SetRenderTarget(color: camera.targets.renderTargetIdentifier, depth: ShaderIDs._DepthBufferTexture);
            buffer.DrawMesh(GraphicsUtility.mesh, Matrix4x4.identity, skyboxMaterial, 0, 0);
        }
    }
}
