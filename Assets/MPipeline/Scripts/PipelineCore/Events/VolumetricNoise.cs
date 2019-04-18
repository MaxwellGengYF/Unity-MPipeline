using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Unity.Mathematics.math;
using MPipeline;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine.Rendering;
using Random = Unity.Mathematics.Random;

public struct VolumetricNoise
{
    private ComputeShader shader;
    public float value { get; private set; }
    public RenderTexture noiseTexture { get; private set; }
    private const int resolution = 48;
    private const int dispatchCount = resolution / 4;
    public void Init(PipelineResources resources)
    {
        noiseTexture = new RenderTexture(new RenderTextureDescriptor
        {
            autoGenerateMips = false,
            useMipMap = false,
            bindMS = false,
            colorFormat = RenderTextureFormat.ARGBHalf,
            depthBufferBits = 0,
            dimension = TextureDimension.Tex3D,
            enableRandomWrite = true,
            height = resolution,
            width = resolution,
            volumeDepth = resolution,
            msaaSamples = 1
        });
        value = 0;
        noiseTexture.filterMode = FilterMode.Bilinear;
        noiseTexture.Create();
        shader = resources.shaders.voxelNoise;
        Random r = new Random((uint)System.Guid.NewGuid().GetHashCode());
        Shader.SetGlobalVector(ShaderIDs._RandomSeed, (float4)(r.NextDouble4() * 10000 + 1000));
        shader.SetTexture(1, ShaderIDs._VolumetricNoise, noiseTexture);
        shader.Dispatch(1, dispatchCount, dispatchCount, dispatchCount);
    }

    public void Update(float deltaTime, CommandBuffer buffer)
    {
        value += deltaTime;
        if (value > 1)
        {
            value -= 1;
            buffer.SetComputeTextureParam(shader, 0, ShaderIDs._VolumetricNoise, noiseTexture);
            buffer.DispatchCompute(shader, 0, dispatchCount, dispatchCount, dispatchCount);
        }
    }

    public bool Check()
    {
        return noiseTexture;
    }

    public void Dispose()
    {
        Object.DestroyImmediate(noiseTexture);
        noiseTexture = null;
    }
}
