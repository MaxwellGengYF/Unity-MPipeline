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
        public ComputeShader relightProbes;
        public Shader minMaxDepthBounding;
        public Shader taaShader;
        public Shader ssrShader;
        public Shader indirectDepthShader;
        public Shader depthDownSample;
        public Shader copyShader;
        public Shader HizLodShader;
        public Shader motionVectorShader;
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
        public Shader ssgiShader;
        public Shader sssShader;
        public Shader bakePreIntShader;
        public Mesh occluderMesh;
        public Mesh sphereMesh;
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
       typeof(LPEvent),
       typeof(GeometryEvent),
       typeof(AOEvents),
       typeof(SkyboxEvent),
       typeof(ReflectionEvent),
       typeof(SeparableSSSSkinEvent),
       typeof(VolumetricLightEvent),
       typeof(TransEvent),
       typeof(TemporalAAEvent),
       typeof(FinalPostEvent)
        };
        [RenderingPath(PipelineResources.CameraRenderingPath.Unlit)]
        public static readonly Type[] unlitType =
        {
            typeof(UnlitEvent)
        };

    }
}
