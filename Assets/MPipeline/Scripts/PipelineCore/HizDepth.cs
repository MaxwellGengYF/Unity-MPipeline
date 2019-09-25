using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Mathematics;
using static Unity.Mathematics.math;
namespace MPipeline
{
    public unsafe struct HizDepth
    {
        private Material getLodMat;
        public bool Check()
        {
            return getLodMat != null;
        }
        public void InitHiZ(PipelineResources resources)
        {
            getLodMat = new Material(resources.shaders.HizLodShader);
        }
        public void GetMipMap(RenderTexture depthMip, RenderTexture backupMip, CommandBuffer buffer, int mip)
        {
            buffer.SetGlobalTexture(ShaderIDs._MainTex, depthMip);
            for (int i = 1; i < mip; ++i)
            {
                buffer.SetGlobalInt(ShaderIDs._PreviousLevel, i - 1);
                buffer.SetRenderTarget(backupMip, i - 1);
                buffer.DrawMesh(GraphicsUtility.mesh, Matrix4x4.identity, getLodMat, 0, 0);
                buffer.CopyTexture(backupMip, 0, i - 1, depthMip, 0, i);
            }
        }
        public void DisposeHiZ()
        {
            Object.DestroyImmediate(getLodMat);
        }
    }
}