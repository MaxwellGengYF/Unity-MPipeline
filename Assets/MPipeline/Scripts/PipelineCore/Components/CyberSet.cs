using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using UnityEngine.Rendering;
using static Unity.Mathematics.math;
using Unity.Collections;
namespace MPipeline
{
    public struct CyberSetData
    {
        public float scanLineJitter;
        public float horizontalShake;
        public float2 colorDrift;
    }
    [System.Serializable]
    public unsafe struct CyberSet
    {
        public bool enabled;
        public float scanLineJitter;
        public float horizontalShake;
        public float colorDrift;
        private float time;
        private ComputeBuffer constantBuffer;
        private Material glitchMat;
        private static int _CyberData = Shader.PropertyToID("_CyberData");
        public void Init(PipelineResources res)
        {
            constantBuffer = new ComputeBuffer(1, sizeof(CyberSetData), ComputeBufferType.Constant);
            glitchMat = new Material(res.shaders.cyberGlitchShader);
        }

        public bool Check()
        {
            return constantBuffer != null && constantBuffer.IsValid();
        }

        public void Render(CommandBuffer buffer, ref RenderTargets targets)
        {
            if (!enabled) return;
            buffer.SetGlobalConstantBuffer(constantBuffer, _CyberData, 0, sizeof(CyberSetData));
            SetValue();
            RenderTargetIdentifier source, dest;
            PipelineFunctions.RunPostProcess(ref targets, out source, out dest);
            buffer.Blit(source, dest, glitchMat);
        }

        public void Dispose()
        {
            constantBuffer.Dispose();
        }

        private void SetValue()
        {
            NativeArray<CyberSetData> data = new NativeArray<CyberSetData>(1, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            CyberSetData* value = data.Ptr();
            time += Time.deltaTime * 60.611f;
            time = Mathf.Repeat(time, Mathf.PI * 2);
            value->scanLineJitter = scanLineJitter;
            value->horizontalShake = horizontalShake * 0.2f;
            value->colorDrift = float2(colorDrift * 0.04f, time);
            constantBuffer.SetData(data);
        }
    }
}