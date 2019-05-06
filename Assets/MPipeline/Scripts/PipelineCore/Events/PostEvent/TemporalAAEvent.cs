using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.Rendering;
namespace MPipeline
{
    [CreateAssetMenu(menuName = "GPURP Events/Temporal AA")]
    [RequireEvent(typeof(PropertySetEvent))]
    public sealed class TemporalAAEvent : PipelineEvent
    {
        [Tooltip("The diameter (in texels) inside which jitter samples are spread. Smaller values result in crisper but more aliased output, while larger values result in more stable but blurrier output.")]
        [Range(0f, 1f)]
        public float jitterSpread = 0.75f;

        [Tooltip("Controls the amount of sharpening applied to the color buffer. High values may introduce dark-border artifacts.")]
        [Range(0f, 3f)]
        public float sharpness = 0.25f;

        [Tooltip("The blend coefficient for a stationary fragment. Controls the percentage of history sample blended into the final color.")]
        [Range(0f, 0.99f)]
        public float stationaryBlending = 0.95f;

        [Tooltip("The blend coefficient for a fragment with significant motion. Controls the percentage of history sample blended into the final color.")]
        [Range(0f, 0.99f)]
        public float motionBlending = 0.9f;
        [Tooltip("Screen Space AABB Bounding for stationary state(Larger will take less flask but more ghost)")]
        [Range(0.05f, 6f)]
        public float stationaryAABBScale = 1.25f;
        [Tooltip("Screen Space AABB Bounding for motion state(Larger will take less flask but more ghost)")]
        [Range(0.05f, 6f)]
        public float motionAABBScale = 0.5f;

       
        private int sampleIndex = 0;
        private const int k_SampleCount = 8;
        private Material taaMat;
        protected override void Init(PipelineResources resources)
        {
            taaMat = new Material(resources.shaders.taaShader);
        }
        public override bool CheckProperty()
        {
            return taaMat != null;
        }
        protected override void Dispose()
        {
            DestroyImmediate(taaMat);
        }
        HistoryTexture texComponent;
        PreviousDepthData prevDepthData;
        public override void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data)
        {
            CommandBuffer buffer = data.buffer;
            texComponent.UpdateProperty(cam);
            SetHistory(cam.cam, buffer, ref texComponent.historyTex, cam.targets.renderTargetIdentifier);
            RenderTexture historyTex = texComponent.historyTex;
            //TAA Start
            const float kMotionAmplification_Blending = 100f * 60f;
            const float kMotionAmplification_Bounding = 100f * 30f;
            buffer.SetGlobalFloat(ShaderIDs._Sharpness, sharpness);
            buffer.SetGlobalVector(ShaderIDs._TemporalClipBounding, new Vector4(stationaryAABBScale, motionAABBScale, kMotionAmplification_Bounding, 0f));
            buffer.SetGlobalVector(ShaderIDs._FinalBlendParameters, new Vector4(stationaryBlending, motionBlending, kMotionAmplification_Blending, 0f));
            buffer.SetGlobalTexture(ShaderIDs._HistoryTex, historyTex);
            RenderTargetIdentifier source, dest;
            PipelineFunctions.RunPostProcess(ref cam.targets, out source, out dest);
            buffer.BlitSRT(source, dest, cam.targets.depthBuffer, taaMat, 0);
            buffer.CopyTexture(dest, historyTex);
            prevDepthData.UpdateCameraSize(new Vector2Int(cam.cam.pixelWidth, cam.cam.pixelHeight));
            buffer.CopyTexture(ShaderIDs._CameraDepthTexture, 0, 0, prevDepthData.SSR_PrevDepth_RT, 0, 0);
        }

        public override void PreRenderFrame(PipelineCamera cam, ref PipelineCommandData data)
        {
            texComponent = IPerCameraData.GetProperty(cam, (c) => new HistoryTexture(c.cam));
            prevDepthData = IPerCameraData.GetProperty(cam, (cc) => new PreviousDepthData(new Vector2Int(cc.cam.pixelWidth, cc.cam.pixelHeight)));
            prevDepthData.targetObject = this;
            data.buffer.SetGlobalVector(ShaderIDs._LastJitter, texComponent.jitter);
            cam.cam.ResetProjectionMatrix();
            ConfigureJitteredProjectionMatrix(cam.cam,ref texComponent.jitter);
            data.buffer.SetGlobalVector(ShaderIDs._Jitter, texComponent.jitter);
        }

        Vector2 GenerateRandomOffset()
        {
            var offset = new Vector2(
                    HaltonSeq.Get((sampleIndex & 1023) + 1, 2) - 0.5f,
                    HaltonSeq.Get((sampleIndex & 1023) + 1, 3) - 0.5f
                );

            if (++sampleIndex >= k_SampleCount)
                sampleIndex = 0;

            return offset;
        }
        protected override void OnDisable()
        {
            Shader.SetGlobalVector(ShaderIDs._Jitter, Vector4.zero);
        }
        public Matrix4x4 GetJitteredProjectionMatrix(Camera camera, ref Vector2 jitter)
        {
            Matrix4x4 cameraProj;
            jitter = GenerateRandomOffset();
            jitter *= jitterSpread;
            cameraProj = camera.orthographic
                ? RuntimeUtilities.GetJitteredOrthographicProjectionMatrix(camera, jitter)
                : RuntimeUtilities.GetJitteredPerspectiveProjectionMatrix(camera, jitter);
            jitter = new Vector2(jitter.x / camera.pixelWidth, jitter.y / camera.pixelHeight);
            return cameraProj;
        }

        public void ConfigureJitteredProjectionMatrix(Camera camera, ref Vector2 jitter)
        {
            camera.nonJitteredProjectionMatrix = camera.projectionMatrix;
            camera.projectionMatrix = GetJitteredProjectionMatrix(camera, ref jitter);
            camera.useJitteredProjectionMatrixForTransparentRendering = false;
        }

        public void SetHistory(Camera cam, CommandBuffer buffer, ref RenderTexture history, RenderTargetIdentifier renderTarget)
        {
            if (history == null)
            {
                history = new RenderTexture(cam.pixelWidth, cam.pixelHeight, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
                history.filterMode = FilterMode.Bilinear;
                buffer.CopyTexture(renderTarget, history);
            }
            else if (history.width != cam.pixelWidth || history.height != cam.pixelHeight)
            {
                history.Release();
                Destroy(history);
                history = new RenderTexture(cam.pixelWidth, cam.pixelHeight, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
                history.filterMode = FilterMode.Bilinear;
                buffer.CopyTexture(renderTarget, history);
            }
        }
    }

    public class HistoryTexture : IPerCameraData
    {
        public RenderTexture historyTex;
        public Vector2 jitter;
        public HistoryTexture(Camera cam)
        {
            historyTex = new RenderTexture(cam.pixelWidth, cam.pixelHeight, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        }

        public override void DisposeProperty()
        {
            historyTex.Release();
            Object.DestroyImmediate(historyTex);
        }
        public void UpdateProperty(PipelineCamera camera)
        {
            int camWidth = camera.cam.pixelWidth;
            int camHeight = camera.cam.pixelHeight;
            if (historyTex.width != camWidth || historyTex.height != camHeight)
            {
                historyTex.Release();
                historyTex.width = camWidth;
                historyTex.height = camHeight;
                historyTex.Create();
            }
        }
    }
}