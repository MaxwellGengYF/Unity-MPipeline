using System.Collections;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine;
namespace MPipeline
{
    [RequireComponent(typeof(Camera))]
    public unsafe sealed class VTDecalCamera : MonoBehaviour, IPipelineRunnable
    {
        public struct CameraState
        {
            public int cullingMask;
            public float3 position;
            public quaternion rotation;
            public float size;
            public float nearClipPlane;
            public float farClipPlane;
            public int resolution;
            public RenderTargetIdentifier albedoRT;
            public RenderTargetIdentifier normalRT;
            public RenderTargetIdentifier smoRT;
            public int depthSlice;
        }
        private Camera cam;
        private RenderTargetIdentifier[] idfs;
        public NativeList<CameraState> renderingCommand { get; private set; }
        private void Awake()
        {
            cam = GetComponent<Camera>();
            cam.enabled = false;
            cam.orthographic = true;
            cam.aspect = 1;
            renderingCommand = new NativeList<CameraState>(10, Allocator.Persistent);
            idfs = new RenderTargetIdentifier[3];
            RenderPipeline.AddRunnableObject(GetInstanceID(), this);
        }

        private void OnDestroy()
        {
            cam = null;
            renderingCommand.Dispose();
        }

        public void PipelineUpdate(ref PipelineCommandData data)
        {
            if (renderingCommand.Length <= 0) return;
            CommandBuffer buffer = data.buffer;
            for (int i = 0; i < renderingCommand.Length; ++i)
            {
                ref CameraState orthoCam = ref renderingCommand[i];
                transform.position = orthoCam.position;
                transform.rotation = orthoCam.rotation;
                cam.orthographicSize = orthoCam.size;
                cam.nearClipPlane = orthoCam.nearClipPlane;
                cam.farClipPlane = orthoCam.farClipPlane;
                #region CAMERA_RENDERING
                ScriptableCullingParameters cullParam;
                data.context.SetupCameraProperties(cam);
                if (!cam.TryGetCullingParameters(out cullParam)) continue;
                cullParam.cullingMask = (uint)orthoCam.cullingMask;
                cullParam.cullingOptions = CullingOptions.ForceEvenIfCameraIsNotActive;
                CullingResults result = data.context.Cull(ref cullParam);
                FilteringSettings filter = new FilteringSettings
                {
                    layerMask = orthoCam.cullingMask,
                    renderingLayerMask = uint.MaxValue,
                    renderQueueRange = new RenderQueueRange(1000, 5000)
                };
                SortingSettings sort = new SortingSettings(cam)
                {
                    criteria = SortingCriteria.RenderQueue
                };
                DrawingSettings drawS = new DrawingSettings(new ShaderTagId("TerrainDecal"), sort)
                {
                    perObjectData = UnityEngine.Rendering.PerObjectData.None
                };
                idfs[0] = orthoCam.albedoRT;
                idfs[1] = orthoCam.normalRT;
                idfs[2] = orthoCam.smoRT;
                buffer.GetTemporaryRT(ShaderIDs._DepthBufferTexture, orthoCam.resolution, orthoCam.resolution, 16, FilterMode.Point, RenderTextureFormat.Depth, RenderTextureReadWrite.Linear, 1, false);
                buffer.SetRenderTarget(colors: idfs, depth: idfs[0], 0, CubemapFace.Unknown, orthoCam.depthSlice);
                buffer.ClearRenderTarget(true, false, Color.black);
                data.ExecuteCommandBuffer();
                data.context.DrawRenderers(result, ref drawS, ref filter);
                data.ExecuteCommandBuffer();
                #endregion
            }
            data.context.Submit();
            renderingCommand.Clear();
        }
    }
}
