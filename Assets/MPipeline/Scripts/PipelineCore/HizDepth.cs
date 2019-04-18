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
        public RenderTexture backupMip { get; private set; }
        private Material getLodMat;
        public static int2 depthRes;
        public bool Check()
        {
            return backupMip != null && getLodMat != null;
        }
        public void InitHiZ(PipelineResources resources, float2 screenRes)
        {
            depthRes.x = 512;
            depthRes.y = (int)((screenRes.y / screenRes.x) * depthRes.x);
            backupMip = new RenderTexture(depthRes.x, depthRes.y, 0, RenderTextureFormat.R16, RenderTextureReadWrite.Linear);
            backupMip.useMipMap = true;
            backupMip.autoGenerateMips = false;
            backupMip.enableRandomWrite = false;
            backupMip.wrapMode = TextureWrapMode.Clamp;
            backupMip.filterMode = FilterMode.Point;
            backupMip.Create();
            getLodMat = new Material(resources.shaders.HizLodShader);
        }
        public void GetMipMap(RenderTexture depthMip, CommandBuffer buffer)
        {
            buffer.SetGlobalTexture(ShaderIDs._MainTex, depthMip);
            for (int i = 1; i < 8; ++i)
            {
                buffer.SetGlobalInt(ShaderIDs._PreviousLevel, i - 1);
                buffer.SetRenderTarget(backupMip, i);
                buffer.DrawMesh(GraphicsUtility.mesh, Matrix4x4.identity, getLodMat, 0, 0);
                buffer.CopyTexture(backupMip, 0, i, depthMip, 0, i);
            }
        }
        public void DisposeHiZ()
        {
            Object.DestroyImmediate(backupMip);
        }
    }
}