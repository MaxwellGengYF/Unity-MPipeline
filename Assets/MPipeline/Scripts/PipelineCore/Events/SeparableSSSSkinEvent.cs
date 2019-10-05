using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Random = Unity.Mathematics.Random;
public static partial class ShaderIDs
{
    public static int _Kernel = Shader.PropertyToID("_Kernel");
    public static int _SSSScale = Shader.PropertyToID("_SSSScale");
}
namespace MPipeline
{
    [CreateAssetMenu(menuName = "GPURP Events/SSS Skin")]
    [RequireEvent(typeof(PropertySetEvent))]
    public unsafe sealed class SeparableSSSSkinEvent : PipelineEvent
    {
        [Range(0, 5)]
        public float SubsurfaceScaler;
        public Color SubsurfaceColor = Color.red;
        public Color SubsurfaceFalloff = Color.red;
        private NativeArray<float4> kernel;
        private ComputeBuffer kernelBuffer;
        private Random rand;
        private JobHandle handle;
        private Material mat;


        protected override void Init(PipelineResources resources)
        {
            kernelBuffer = new ComputeBuffer(11, sizeof(float4));
            rand = new Random((uint)System.Guid.NewGuid().GetHashCode());
            mat = new Material(resources.shaders.sssShader);
        }

        protected override void Dispose()
        {
            DestroyImmediate(mat);
            kernelBuffer.Dispose();
        }

        public override bool CheckProperty()
        {
            return mat;
        }

        public override void PreRenderFrame(PipelineCamera cam, ref PipelineCommandData data)
        {
            kernel = new NativeArray<float4>(11, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            handle = new CalculateKernel
            {
                kernel = kernel.Ptr(),
                sssc = float3(SubsurfaceColor.r, SubsurfaceColor.g, SubsurfaceColor.b),
                sssfc = float3(SubsurfaceFalloff.r, SubsurfaceFalloff.g, SubsurfaceFalloff.b)
            }.Schedule();
        }

        public override void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data)
        {
            CommandBuffer buffer = data.buffer;
            handle.Complete();
            kernelBuffer.SetData(kernel);
            kernel.Dispose();

            buffer.SetGlobalFloat(ShaderIDs._SSSScale, SubsurfaceScaler);
            buffer.SetGlobalBuffer(ShaderIDs._Kernel, kernelBuffer);
            RenderTargetIdentifier source, dest;
            PipelineFunctions.RunPostProcess(ref cam.targets, out source, out dest);
            buffer.BlitSRT(source, dest, ShaderIDs._DepthBufferTexture, mat, 0);
            PipelineFunctions.RunPostProcess(ref cam.targets, out source, out dest);
            buffer.BlitSRT(source, dest, ShaderIDs._DepthBufferTexture, mat, 1);
        }

        [BurstCompile]
        private struct CalculateKernel : IJob
        {
            [NativeDisableUnsafePtrRestriction]
            public float4* kernel;
            public float3 sssc;
            public float3 sssfc;
            public void Execute()
            {
                SeparableSSS.CalculateKernel(kernel, 11, sssc, sssfc);
            }
        }
    }
}
