using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using UnityEngine.Rendering;
namespace MPipeline
{
    [CreateAssetMenu(menuName = "GPURP Events/SSGI")]
    [RequireEvent(typeof(PropertySetEvent))]
    public class ScreenSpaceIndirectDiffuse : PipelineEvent
    {


        private enum DebugPass
        {
            Combine = 9,
            IndirectColor = 10
        };

        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        [Header("Common Property")]

        [Range(1, 16)]
        [SerializeField]
        int RayNums = 2;


        [Range(0.05f, 5)]
        [SerializeField]
        float Thickness = 0.05f;


        [Range(0, 0.5f)]
        [SerializeField]
        float ScreenFade = 0.05f;



        [Header("Trace Property")]

        [Range(32, 512)]
        [SerializeField]
        int HiZ_RaySteps = 58;


        [Range(4, 10)]
        [SerializeField]
        int HiZ_MaxLevel = 10;


        [Range(0, 2)]
        [SerializeField]
        int HiZ_StartLevel = 1;


        [Range(0, 2)]
        [SerializeField]
        int HiZ_StopLevel = 0;



        [Header("Filtter Property")]

        [SerializeField]
        bool TwoPass_Denoise = true;


        [SerializeField]
        Texture2D BlueNoise_LUT = null;


        [Range(1, 128)]
        [SerializeField]
        float Gi_Intensity = 1;


        [Range(0, 0.99f)]
        [SerializeField]
        float TemporalWeight = 0.99f;


        [Range(1, 5)]
        [SerializeField]
        float TemporalScale = 1.25f;



        [Header("DeBug Property")]

        [SerializeField]
        bool Denoise = true;


        [SerializeField]
        bool RunTimeDebugMod = true;


        [SerializeField]
        DebugPass DeBugPass = DebugPass.IndirectColor;


        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private const int RenderPass_HiZ_Depth = 0;
        private const int RenderPass_HiZ3D_SingelSpp = 1;
        private const int RenderPass_HiZ3D_MultiSpp = 2;
        private const int RenderPass_Temporalfilter_01 = 3;
        private const int RenderPass_Bilateralfilter_X_01 = 4;
        private const int RenderPass_Bilateralfilter_Y_01 = 5;
        private const int RenderPass_Temporalfilter_02 = 6;
        private const int RenderPass_Bilateralfilter_X_02 = 7;
        private const int RenderPass_Bilateralfilter_Y_02 = 8;
        private Material SSGi_Material;

        private Vector2 RandomSampler = new Vector2(1, 1);
        private Vector2 CameraSize;

        private static int SSGi_Jitter_ID = Shader.PropertyToID("_SSGi_Jitter");
        private static int SSGi_GiIntensity_ID = Shader.PropertyToID("_SSGi_GiIntensity");
        private static int SSGi_NumSteps_HiZ_ID = Shader.PropertyToID("_SSGi_NumSteps_HiZ");
        private static int SSGi_NumRays_ID = Shader.PropertyToID("_SSGi_NumRays");
        private static int SSGi_ScreenFade_ID = Shader.PropertyToID("_SSGi_ScreenFade");
        private static int SSGi_Thickness_ID = Shader.PropertyToID("_SSGi_Thickness");
        private static int SSGi_TemporalScale_ID = Shader.PropertyToID("_SSGi_TemporalScale");
        private static int SSGi_TemporalWeight_ID = Shader.PropertyToID("_SSGi_TemporalWeight");
        private static int SSGi_ScreenSize_ID = Shader.PropertyToID("_SSGi_ScreenSize");
        private static int SSGi_RayCastSize_ID = Shader.PropertyToID("_SSGi_RayCastSize");
        private static int SSGi_NoiseSize_ID = Shader.PropertyToID("_SSGi_NoiseSize");
        private static int SSGi_HiZ_PrevDepthLevel_ID = Shader.PropertyToID("_SSGi_HiZ_PrevDepthLevel");
        private static int SSGi_HiZ_MaxLevel_ID = Shader.PropertyToID("_SSGi_HiZ_MaxLevel");
        private static int SSGi_HiZ_StartLevel_ID = Shader.PropertyToID("_SSGi_HiZ_StartLevel");
        private static int SSGi_HiZ_StopLevel_ID = Shader.PropertyToID("_SSGi_HiZ_StopLevel");



        private static int SSGi_Noise_ID = Shader.PropertyToID("_SSGi_Noise");

        private static int SSGi_HierarchicalDepth_ID = Shader.PropertyToID("_SSGi_HierarchicalDepth_RT");
        private static int SSGi_SceneColor_ID = Shader.PropertyToID("_SSGi_SceneColor_RT");
        private static int SSGi_CombineScene_ID = Shader.PropertyToID("_SSGi_CombienReflection_RT");



        private static int SSGi_Trace_ID = Shader.PropertyToID("_SSGi_RayCastRT");
        private static int SSGi_TemporalPrev_ID_01 = Shader.PropertyToID("_SSGi_TemporalPrev_RT_01");
        private static int SSGi_TemporalCurr_ID_01 = Shader.PropertyToID("_SSGi_TemporalCurr_RT_01");
        private static int SSGi_Bilateral_ID_01 = Shader.PropertyToID("_SSGi_Bilateral_RT_01");

        private static int SSGi_TemporalPrev_ID_02 = Shader.PropertyToID("_SSGi_TemporalPrev_RT_02");
        private static int SSGi_TemporalCurr_ID_02 = Shader.PropertyToID("_SSGi_TemporalCurr_RT_02");
        private static int SSGi_Bilateral_ID_02 = Shader.PropertyToID("_SSGi_Bilateral_RT_02");



        private static int SSGi_ProjectionMatrix_ID = Shader.PropertyToID("_SSGi_ProjectionMatrix");
        private static int SSGi_ViewProjectionMatrix_ID = Shader.PropertyToID("_SSGi_ViewProjectionMatrix");
        private static int SSGi_LastFrameViewProjectionMatrix_ID = Shader.PropertyToID("_SSGi_LastFrameViewProjectionMatrix");
        private static int SSGi_InverseProjectionMatrix_ID = Shader.PropertyToID("_SSGi_InverseProjectionMatrix");
        private static int SSGi_InverseViewProjectionMatrix_ID = Shader.PropertyToID("_SSGi_InverseViewProjectionMatrix");
        private static int SSGi_WorldToCameraMatrix_ID = Shader.PropertyToID("_SSGi_WorldToCameraMatrix");
        private static int SSGi_CameraToWorldMatrix_ID = Shader.PropertyToID("_SSGi_CameraToWorldMatrix");
        private static int SSGi_ProjectToPixelMatrix_ID = Shader.PropertyToID("_SSGi_ProjectToPixelMatrix");
        private PropertySetEvent proper;
        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        protected override void Init(PipelineResources resources)
        {
            proper = RenderPipeline.GetEvent<PropertySetEvent>();
            SSGi_Material = new Material(resources.shaders.ssgiShader);
        }
        protected override void Dispose()
        {
            DestroyImmediate(SSGi_Material);
        }
        public override bool CheckProperty()
        {
            return SSGi_Material;
        }
        public override void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data)
        {
            if (RenderPipeline.renderingEditor) return;
            RandomSampler = GenerateRandomOffset();
            SSR_UpdateVariable(cam.cam, ref data);
            RenderScreenSpaceReflection(data.buffer, cam, ref cam.targets);
        }

        ////////////////////////////////////////////////////////////////SSR Function////////////////////////////////////////////////////////////////
        private int m_SampleIndex = 0;
        private const int k_SampleCount = 64;
        private static float GetHaltonValue(int index, int radix)
        {
            float result = 0f;
            float fraction = 1f / radix;

            while (index > 0)
            {
                result += (index % radix) * fraction;
                index /= radix;
                fraction /= radix;
            }
            return result;
        }
        private Vector2 GenerateRandomOffset()
        {
            var offset = new Vector2(GetHaltonValue(m_SampleIndex & 1023, 2), GetHaltonValue(m_SampleIndex & 1023, 3));
            if (m_SampleIndex++ >= k_SampleCount)
                m_SampleIndex = 0;
            return offset;
        }
        private void SSR_UpdateUniformVariable(CommandBuffer buffer)
        {
            buffer.SetGlobalTexture(SSGi_Noise_ID, BlueNoise_LUT);
            buffer.SetGlobalVector(SSGi_ScreenSize_ID, CameraSize);
            buffer.SetGlobalVector(SSGi_RayCastSize_ID, CameraSize);
            buffer.SetGlobalVector(SSGi_NoiseSize_ID, new Vector2(1024, 1024));
            buffer.SetGlobalFloat(SSGi_ScreenFade_ID, ScreenFade);
            buffer.SetGlobalFloat(SSGi_Thickness_ID, Thickness);
            buffer.SetGlobalFloat(SSGi_GiIntensity_ID, Gi_Intensity);
            buffer.SetGlobalInt(SSGi_NumSteps_HiZ_ID, HiZ_RaySteps);
            buffer.SetGlobalInt(SSGi_NumRays_ID, RayNums);
            buffer.SetGlobalInt(SSGi_HiZ_MaxLevel_ID, HiZ_MaxLevel);
            buffer.SetGlobalInt(SSGi_HiZ_StartLevel_ID, HiZ_StartLevel);
            buffer.SetGlobalInt(SSGi_HiZ_StopLevel_ID, HiZ_StopLevel);
            buffer.SetGlobalFloat(SSGi_TemporalScale_ID, TemporalScale);
            buffer.SetGlobalFloat(SSGi_TemporalWeight_ID, TemporalWeight);
        }

        private void SSR_UpdateVariable(Camera RenderCamera, ref PipelineCommandData data)
        {
            CommandBuffer buffer = data.buffer;
            Vector2 CurrentCameraSize = new Vector2(RenderCamera.pixelWidth, RenderCamera.pixelHeight);
            SSR_UpdateUniformVariable(buffer);
            buffer.SetGlobalVector(SSGi_Jitter_ID, new Vector4((float)CameraSize.x / 1024, (float)CameraSize.y / 1024, RandomSampler.x, RandomSampler.y));
            Matrix4x4 SSGi_ProjectionMatrix = GL.GetGPUProjectionMatrix(RenderCamera.projectionMatrix, false);
            buffer.SetGlobalMatrix(SSGi_ProjectionMatrix_ID, SSGi_ProjectionMatrix);
            buffer.SetGlobalMatrix(SSGi_ViewProjectionMatrix_ID, proper.VP);
            buffer.SetGlobalMatrix(SSGi_InverseProjectionMatrix_ID, SSGi_ProjectionMatrix.inverse);
            buffer.SetGlobalMatrix(SSGi_InverseViewProjectionMatrix_ID, proper.inverseVP);
            buffer.SetGlobalMatrix(SSGi_WorldToCameraMatrix_ID, RenderCamera.worldToCameraMatrix);
            buffer.SetGlobalMatrix(SSGi_CameraToWorldMatrix_ID, RenderCamera.cameraToWorldMatrix);
            buffer.SetGlobalMatrix(SSGi_LastFrameViewProjectionMatrix_ID, proper.lastViewProjection);

            Matrix4x4 warpToScreenSpaceMatrix = Matrix4x4.identity;
            warpToScreenSpaceMatrix.m00 = CurrentCameraSize.x; warpToScreenSpaceMatrix.m03 = CurrentCameraSize.x;
            warpToScreenSpaceMatrix.m11 = CurrentCameraSize.y; warpToScreenSpaceMatrix.m13 = CurrentCameraSize.y;
            Matrix4x4 SSGi_ProjectToPixelMatrix = warpToScreenSpaceMatrix * SSGi_ProjectionMatrix;
            buffer.SetGlobalMatrix(SSGi_ProjectToPixelMatrix_ID, SSGi_ProjectToPixelMatrix);
        }



        private void RenderScreenSpaceReflection(CommandBuffer SSGi_Buffer, PipelineCamera cam, ref RenderTargets targets)
        {
            Camera RenderCamera = cam.cam;
            SSGIData data = IPerCameraData.GetProperty(cam, (cc) => new SSGIData(int2(cc.cam.pixelWidth, cc.cam.pixelHeight)));
            data.UpdateResolution(int2(cam.cam.pixelWidth, cam.cam.pixelHeight));
            //////Set HierarchicalDepthRT//////
            SSGi_Buffer.CopyTexture(targets.depthTexture, 0, 0, data.SSGi_HierarchicalDepth_RT, 0, 0);
            for (int i = 1; i < HiZ_MaxLevel; ++i)
            {
                SSGi_Buffer.SetGlobalInt(SSGi_HiZ_PrevDepthLevel_ID, i - 1);
                SSGi_Buffer.SetRenderTarget(data.SSGi_HierarchicalDepth_BackUp_RT, i);
                SSGi_Buffer.DrawMesh(GraphicsUtility.mesh, Matrix4x4.identity, SSGi_Material, 0, RenderPass_HiZ_Depth);
                SSGi_Buffer.CopyTexture(data.SSGi_HierarchicalDepth_BackUp_RT, 0, i, data.SSGi_HierarchicalDepth_RT, 0, i);
            }
            SSGi_Buffer.SetGlobalTexture(SSGi_HierarchicalDepth_ID, data.SSGi_HierarchicalDepth_RT);

            SSGi_Buffer.GetTemporaryRT(SSGi_SceneColor_ID, RenderCamera.pixelWidth, RenderCamera.pixelHeight, 0, FilterMode.Bilinear, RenderTextureFormat.ARGBHalf);
            SSGi_Buffer.CopyTexture(targets.renderTargetIdentifier, 0, 0, SSGi_SceneColor_ID, 0, 0);


            //////RayCasting//////
            SSGi_Buffer.GetTemporaryRT(SSGi_Trace_ID, RenderCamera.pixelWidth, RenderCamera.pixelHeight, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf);
            SSGi_Buffer.BlitSRT(SSGi_Trace_ID, SSGi_Material, RenderPass_HiZ3D_MultiSpp);
            SSGi_Buffer.Blit(SSGi_Trace_ID, targets.renderTargetIdentifier);
        }
    }
    public class SSGIData : IPerCameraData
    {
        public RenderTexture SSGi_TemporalPrev_RT_01, SSGi_HierarchicalDepth_RT, SSGi_HierarchicalDepth_BackUp_RT;
        public int2 resolution;
        public SSGIData(int2 resolution)
        {
            this.resolution = resolution;
            SSGi_HierarchicalDepth_RT = new RenderTexture(resolution.x, resolution.y, 0, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear);
            SSGi_HierarchicalDepth_RT.filterMode = FilterMode.Point;
            SSGi_HierarchicalDepth_RT.useMipMap = true;
            SSGi_HierarchicalDepth_RT.autoGenerateMips = false;

            SSGi_HierarchicalDepth_BackUp_RT = new RenderTexture(resolution.x, resolution.y, 0, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear);
            SSGi_HierarchicalDepth_BackUp_RT.filterMode = FilterMode.Point;
            SSGi_HierarchicalDepth_BackUp_RT.useMipMap = true;
            SSGi_HierarchicalDepth_BackUp_RT.autoGenerateMips = false;

            SSGi_TemporalPrev_RT_01 = new RenderTexture(resolution.x, resolution.y, 0, RenderTextureFormat.ARGBHalf);
            SSGi_TemporalPrev_RT_01.filterMode = FilterMode.Bilinear;
            SSGi_TemporalPrev_RT_01.useMipMap = false;
            SSGi_TemporalPrev_RT_01.Create();
            SSGi_HierarchicalDepth_BackUp_RT.Create();
            SSGi_HierarchicalDepth_RT.Create();
        }

        public void UpdateResolution(int2 targetRes)
        {
            void ChangeResolution(RenderTexture rt)
            {
                rt.Release();
                rt.width = targetRes.x;
                rt.height = targetRes.y;
                rt.Create();
            }
            bool2 s = resolution == targetRes;
            if (s.x && s.y) return;
            resolution = targetRes;
            ChangeResolution(SSGi_TemporalPrev_RT_01);
            ChangeResolution(SSGi_HierarchicalDepth_RT);
            ChangeResolution(SSGi_HierarchicalDepth_BackUp_RT);
        }
        public override void DisposeProperty()
        {
            Object.DestroyImmediate(SSGi_TemporalPrev_RT_01);
            Object.DestroyImmediate(SSGi_HierarchicalDepth_RT);
            Object.DestroyImmediate(SSGi_HierarchicalDepth_BackUp_RT);
        }
    }
}