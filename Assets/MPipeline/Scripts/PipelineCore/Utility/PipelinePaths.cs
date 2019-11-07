using System;
using UnityEngine;
namespace MPipeline
{
    [Serializable]
    public struct PipelineShaders
    {
        public ComputeShader cbdrShader;
        public ComputeShader gpuFrustumCulling;
        public ComputeShader gpuSkin;
        public ComputeShader streamingShader;
        public ComputeShader pointLightFrustumCulling;
        public ComputeShader terrainCompute;
        public ComputeShader volumetricScattering;
        public ComputeShader texCopyShader;
        public ComputeShader reflectionCullingShader;
        public ComputeShader voxelNoise;
        public ComputeShader occlusionProbeCalculate;
        public ComputeShader minMaxDepthCompute;
        public ComputeShader HizLodShader;
        public Shader minMaxDepthBounding;
        public Shader taaShader;
        public Shader ssrShader;
        public Shader indirectDepthShader;
        public Shader reflectionShader;
        public Shader linearDepthShader;
        public Shader linearDrawerShader;
        public Shader cubeDepthShader;
        public Shader clusterRenderShader;
        public Shader volumetricShader;
        public Shader terrainShader;
        public Shader spotLightDepthShader;
        public Shader gtaoShader;
        public Shader overrideOpaqueShader;
        public Shader sssShader;
        public Shader bakePreIntShader;
        public Shader rapidBlurShader;
        public Shader cyberGlitchShader;
    }

    public unsafe static class AllEvents
    {
        [RenderingPath(PipelineResources.CameraRenderingPath.Bake)]
        public static readonly Type[] bakeType =
        {
        typeof(PropertySetEvent),
        typeof(LightingEvent),
        typeof(GeometryEvent),
        typeof(SkyboxEvent),
        typeof(DebugEvent)
        };
        [RenderingPath(PipelineResources.CameraRenderingPath.GPUDeferred)]
        public static readonly Type[] gpuDeferredType =
        {
       typeof(PropertySetEvent),
       typeof(LightingEvent),
       typeof(GeometryEvent),
       typeof(AOEvents),
       typeof(SkyboxEvent),
       typeof(ReflectionEvent),
       typeof(SeparableSSSSkinEvent),
       typeof(VolumetricLightEvent),
       typeof(TemporalAAEvent),
       typeof(TransEvent),
       typeof(FinalPostEvent)
        };
        [RenderingPath(PipelineResources.CameraRenderingPath.Unlit)]
        public static readonly Type[] unlitType =
        {
            typeof(UnlitEvent)
        };

    }
}
