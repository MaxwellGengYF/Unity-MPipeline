using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Mathematics;
using System;
using Random = Unity.Mathematics.Random;
namespace MPipeline
{
    public unsafe sealed class RainRT : DecalAtlasBase
    {
        private ComputeShader shader;
        public float rainSpeed = 10;
        private ComputeBuffer rippleBuffer;
        private const int SIZE = 256;
        private const int RIPPLECOUNT = 128;
        private const int RIPPLESIZE = 16;
        private static readonly int _RippleBuffer = Shader.PropertyToID("_RippleBuffer");
        private struct Ripple
        {
            public uint2 position;
            public uint range;
            public float timeLine;
        };

        public override void Init()
        {

            shader = Resources.Load<ComputeShader>("WaterRippleCompute");

            rippleBuffer = new ComputeBuffer(RIPPLECOUNT, sizeof(Ripple));
            Random r = new Random((uint)Guid.NewGuid().GetHashCode());
            NativeArray<Ripple> ripplesArray = new NativeArray<Ripple>(RIPPLECOUNT, Allocator.Temp);
            for (int i = 0; i < RIPPLECOUNT; ++i)
            {
                Ripple rip = new Ripple
                {
                    position = r.NextUInt2(0, 511),
                    range = r.NextUInt(1, 5),
                    timeLine = r.NextFloat() * 0.8f
                };
                ripplesArray[i] = rip;
            }
            rippleBuffer.SetData(ripplesArray);
            ripplesArray.Dispose();
        }

        public override void Dispose()
        {
            Resources.UnloadAsset(shader);
            rippleBuffer.Dispose();
        }

        public override void FrameUpdate(CommandBuffer buffer, RenderTexture targetNormal, int targetAlbedoElement)
        {
            CommandBuffer cb = RenderPipeline.BeforeFrameBuffer;
            cb.SetComputeIntParam(shader, ShaderIDs._TextureSize, targetNormal.width);
            cb.SetComputeIntParam(shader, ShaderIDs._Count, targetAlbedoElement);
            cb.SetRenderTarget(targetNormal);
            cb.ClearRenderTarget(false, true, Color.black);
            cb.SetComputeTextureParam(shader, 0, ShaderIDs._MainTex, targetNormal);
            cb.SetComputeBufferParam(shader, 0, _RippleBuffer, rippleBuffer);
            cb.SetComputeBufferParam(shader, 1, _RippleBuffer, rippleBuffer);
            cb.SetComputeFloatParam(shader, ShaderIDs._DeltaTime, Time.deltaTime * rainSpeed);
            cb.DispatchCompute(shader, 0, 5, 5, 128);
            cb.DispatchCompute(shader, 1, RIPPLECOUNT / 64, 1, 1);
        }
        private void Update()
        {
            
        }
    }
}