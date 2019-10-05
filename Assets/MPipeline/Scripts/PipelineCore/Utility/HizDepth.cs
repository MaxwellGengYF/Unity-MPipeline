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
        
        ComputeShader sd;
        public void InitHiZ(PipelineResources resources)
        {
            sd = resources.shaders.HizLodShader;
        }
        public void GetMipMap(RenderTexture depthMip, CommandBuffer buffer, int mip)
        {
            buffer.SetGlobalTexture(ShaderIDs._MainTex, depthMip);
            int2 size = int2(depthMip.width, depthMip.height);
            for (int i = 1; i < mip; ++i)
            {
                size = max(1, size / 2);
                buffer.SetComputeTextureParam(sd, 0, ShaderIDs._SourceTex, depthMip, i - 1);
                buffer.SetComputeTextureParam(sd, 0, ShaderIDs._DestTex, depthMip, i);
                buffer.SetComputeVectorParam(sd, ShaderIDs._Count, float4(size - float2(0.5f), 0, 0));
                int x, y;
                x = Mathf.CeilToInt(size.x / 8f);
                y = Mathf.CeilToInt(size.y / 8f);
                buffer.DispatchCompute(sd, 0, x, y, 1);
            }
        }
    }
}