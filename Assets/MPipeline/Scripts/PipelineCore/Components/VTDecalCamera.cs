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
            public RenderTargetIdentifier albedoRT;
            public RenderTargetIdentifier normalRT;
            public RenderTargetIdentifier smoRT;
            public RenderTargetIdentifier heightRT;
            public float3 maskScaleOffset;
            public int depthSlice;
            public int heightIndex;
        }
        [EasyButtons.Button]
        void SetAspect()
        {
            GetComponent<Camera>().aspect = 1;
        }
        private Camera cam;
        private RenderTargetIdentifier[] idfs;
        public NativeList<CameraState> renderingCommand { get; private set; }
        private RenderTexture heightTempTex;
        private void OnEnable()
        {
            heightTempTex = new RenderTexture(MTerrain.HEIGHT_RESOLUTION * 4, MTerrain.HEIGHT_RESOLUTION * 4, 16, MTerrain.HEIGHT_FORMAT, 3);
            heightTempTex.useMipMap = true;
            heightTempTex.autoGenerateMips = false;
            heightTempTex.filterMode = FilterMode.Point;
            heightTempTex.antiAliasing = 1;
            heightTempTex.Create();
            cam = GetComponent<Camera>();
            cam.enabled = false;
            cam.orthographic = true;
            cam.aspect = 1;
            renderingCommand = new NativeList<CameraState>(10, Allocator.Persistent);
            idfs = new RenderTargetIdentifier[3];
            RenderPipeline.AddRunnableObject(GetInstanceID(), this);
        }

        private void OnDisable()
        {
            DestroyImmediate(heightTempTex);
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
                ScriptableCullingParameters cullParam;
                bool rendering = orthoCam.cullingMask != 0;
                if (rendering)
                {
                    transform.position = orthoCam.position;
                    transform.rotation = orthoCam.rotation;
                    cam.orthographicSize = orthoCam.size;
                    cam.nearClipPlane = orthoCam.nearClipPlane;
                    cam.farClipPlane = orthoCam.farClipPlane;
                    cam.cullingMask = orthoCam.cullingMask;
                    rendering = cam.TryGetCullingParameters(out cullParam);
                    if (rendering)
                    {
                        data.context.SetupCameraProperties(cam);
                        cullParam.cullingMask = (uint)orthoCam.cullingMask;
                        cullParam.cullingOptions = CullingOptions.ForceEvenIfCameraIsNotActive;
                        CullingResults result = data.context.Cull(ref cullParam);
                        FilteringSettings filter = new FilteringSettings
                        {
                            layerMask = orthoCam.cullingMask,
                            renderingLayerMask = 1,
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
                        DrawingSettings drawH = new DrawingSettings(new ShaderTagId("TerrainDisplacement"), sort)
                        {
                            perObjectData = UnityEngine.Rendering.PerObjectData.None
                        };

                        ComputeShader copyShader = data.resources.shaders.texCopyShader;
                        buffer.SetGlobalVector(ShaderIDs._MaskScaleOffset, float4(orthoCam.maskScaleOffset, (float)(1.0 / MTerrain.current.terrainData.displacementScale)));
                        buffer.SetGlobalInt(ShaderIDs._OffsetIndex, orthoCam.heightIndex);
                        var terrainData = MTerrain.current.terrainData;
                        buffer.SetGlobalVector(ShaderIDs._HeightScaleOffset, (float4)double4(terrainData.heightScale, terrainData.heightOffset, 1, 1));
                        buffer.GetTemporaryRT(RenderTargets.gbufferIndex[0], MTerrain.COLOR_RESOLUTION, MTerrain.COLOR_RESOLUTION, 16, FilterMode.Point, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear, 1, true);
                        buffer.GetTemporaryRT(RenderTargets.gbufferIndex[2], MTerrain.COLOR_RESOLUTION, MTerrain.COLOR_RESOLUTION, 0, FilterMode.Point, RenderTextureFormat.RGHalf, RenderTextureReadWrite.Linear, 1, true);
                        buffer.GetTemporaryRT(RenderTargets.gbufferIndex[1], MTerrain.COLOR_RESOLUTION, MTerrain.COLOR_RESOLUTION, 0, FilterMode.Point, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear, 1, true);
                        idfs[0] = RenderTargets.gbufferIndex[0];
                        idfs[1] = RenderTargets.gbufferIndex[2];
                        idfs[2] = RenderTargets.gbufferIndex[1];
                        buffer.SetComputeIntParam(copyShader, ShaderIDs._Count, orthoCam.depthSlice);
                        buffer.SetComputeTextureParam(copyShader, 7, ShaderIDs._VirtualMainTex, orthoCam.albedoRT);
                        buffer.SetComputeTextureParam(copyShader, 7, ShaderIDs._VirtualBumpMap, orthoCam.normalRT);
                        buffer.SetComputeTextureParam(copyShader, 7, ShaderIDs._VirtualSMO, orthoCam.smoRT);
                        buffer.SetComputeTextureParam(copyShader, 7, RenderTargets.gbufferIndex[0], RenderTargets.gbufferIndex[0]);
                        buffer.SetComputeTextureParam(copyShader, 7, RenderTargets.gbufferIndex[1], RenderTargets.gbufferIndex[1]);
                        buffer.SetComputeTextureParam(copyShader, 7, RenderTargets.gbufferIndex[2], RenderTargets.gbufferIndex[2]);
                        const int disp = MTerrain.COLOR_RESOLUTION / 8;
                        buffer.DispatchCompute(copyShader, 7, disp, disp, 1);
                        buffer.SetRenderTarget(colors: idfs, depth: idfs[0]);
                        buffer.ClearRenderTarget(true, false, new Color(0, 0, 0, 0));
                        data.ExecuteCommandBuffer();
                        data.context.DrawRenderers(result, ref drawS, ref filter);

                        buffer.SetComputeTextureParam(copyShader, 6, ShaderIDs._VirtualMainTex, orthoCam.albedoRT);
                        buffer.SetComputeTextureParam(copyShader, 6, ShaderIDs._VirtualBumpMap, orthoCam.normalRT);
                        buffer.SetComputeTextureParam(copyShader, 6, ShaderIDs._VirtualSMO, orthoCam.smoRT);
                        buffer.SetComputeTextureParam(copyShader, 6, RenderTargets.gbufferIndex[0], RenderTargets.gbufferIndex[0]);
                        buffer.SetComputeTextureParam(copyShader, 6, RenderTargets.gbufferIndex[1], RenderTargets.gbufferIndex[1]);
                        buffer.SetComputeTextureParam(copyShader, 6, RenderTargets.gbufferIndex[2], RenderTargets.gbufferIndex[2]);
                        buffer.DispatchCompute(copyShader, 6, disp, disp, 1);
                        buffer.ReleaseTemporaryRT(RenderTargets.gbufferIndex[1]);
                        buffer.ReleaseTemporaryRT(RenderTargets.gbufferIndex[2]);
                        buffer.ReleaseTemporaryRT(RenderTargets.gbufferIndex[0]);

                        buffer.SetRenderTarget(color: heightTempTex.colorBuffer, depth: heightTempTex.depthBuffer, 0);
                        buffer.ClearRenderTarget(true, true, Color.black);
                        data.ExecuteCommandBuffer();
                        data.context.DrawRenderers(result, ref drawH, ref filter);
                        buffer.GenerateMips(heightTempTex);
                        buffer.CopyTexture(heightTempTex, 0, 2, orthoCam.heightRT, orthoCam.depthSlice, 0);
                    }
                }
                if (!rendering)
                {
                    buffer.SetRenderTarget(orthoCam.heightRT, mipLevel: 0, cubemapFace: CubemapFace.Unknown, depthSlice: orthoCam.depthSlice);
                    buffer.ClearRenderTarget(false, true, Color.black);
                }
                MTerrain.current.GenerateMips(orthoCam.depthSlice, buffer);
                data.ExecuteCommandBuffer();
                data.context.Submit();
            }
            renderingCommand.Clear();
        }
    }
}
