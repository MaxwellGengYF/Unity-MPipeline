using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Rendering;
using Unity.Mathematics;
using UnityEngine.Experimental.Rendering;
using System;
using static Unity.Mathematics.math;
using System.Runtime.CompilerServices;
namespace MPipeline
{
    public struct RenderClusterOptions
    {
        public Vector4[] frustumPlanes;
        public CommandBuffer command;
        public ComputeShader cullingShader;
        public ComputeShader terrainCompute;
    }
    [Serializable]
    public unsafe static class SceneController
    {
        public struct DrawSceneSettings
        {
            public RenderClusterOptions clusterOptions;
            public Camera targetCam;
            public RenderQueueRange renderRange;
            public string passName;
            public CullingOptions flag;
            public PerObjectData configure;
            public Material clusterMat;
        }
        public const int overrideShadowmapPass = 0;
        public const int overrideDepthPrePass = 1;
        public static bool gpurpEnabled { get; private set; }
        private static bool singletonReady = false;
        private static PipelineResources resources;
        public static PipelineBaseBuffer baseBuffer { get; private set; }

        public static NativeList<ulong> addList;
        private struct BufferKey
        {
            public int size;
            public ComputeBufferType type;
            public override int GetHashCode()
            {
                return size.GetHashCode() | (type.GetHashCode() & 65535);
            }
            public struct Equal : IFunction<BufferKey, BufferKey, bool>
            {
                public bool Run(ref BufferKey a, ref BufferKey b)
                {
                    return a.size == b.size && a.type == b.type;
                }
            }
        }

        private static NativeDictionary<BufferKey, int, BufferKey.Equal> allTempBuffers;
        public static void SetState()
        {
            if (singletonReady && baseBuffer.clusterCount > 0)
            {
                gpurpEnabled = true;
            }
            else
            {
                gpurpEnabled = false;
            }
        }
        public static ComputeBuffer GetTempPropertyBuffer(int length, int stride, ComputeBufferType type = ComputeBufferType.Default)
        {
            if (!allTempBuffers.isCreated)
                allTempBuffers = new NativeDictionary<BufferKey, int, BufferKey.Equal>(11, Allocator.Persistent, new BufferKey.Equal());
            ComputeBuffer target;
            int targetIndex;
            if (allTempBuffers.Get(new BufferKey { size = stride, type = type }, out targetIndex))
            {
                target = MUnsafeUtility.GetHookedObject(targetIndex) as ComputeBuffer;
                if (target.count < length)
                {
                    target.Dispose();
                    target = new ComputeBuffer(length, stride, type);
                    MUnsafeUtility.SetHookedObject(targetIndex, target);
                }
                return target;
            }
            else
            {
                target = new ComputeBuffer(length, stride);
                allTempBuffers[new BufferKey { size = stride, type = type }] = MUnsafeUtility.HookObject(target);
                return target;
            }
        }
        public static void Awake(PipelineResources resources)
        {
            int maximumClusterCount = 0;
            ClusterMatResources clusterRes = resources.clusterResources;
            if (Application.isPlaying && clusterRes)
            {
                clusterRes.Init(resources);
                maximumClusterCount = clusterRes.maximumClusterCount;
            }
            singletonReady = true;
            SceneController.resources = resources;
            addList = new NativeList<ulong>(10, Allocator.Persistent);
            baseBuffer = new PipelineBaseBuffer();
            PipelineFunctions.InitBaseBuffer(baseBuffer, maximumClusterCount);
        }

        public static int GetMoveCountBuffer()
        {
            if (baseBuffer.moveCountBuffers.Length > 0)
            {
                int index = baseBuffer.moveCountBuffers[baseBuffer.moveCountBuffers.Length - 1];
                baseBuffer.moveCountBuffers.RemoveLast();
                return index;
            }
            else
            {
                ComputeBuffer cb = new ComputeBuffer(5, sizeof(int), ComputeBufferType.IndirectArguments);
                return MUnsafeUtility.HookObject(cb);
            }
        }

        public static void ReturnMoveCountBuffer(int index)
        {
            
            baseBuffer.moveCountBuffers.Add(index);
        }

        public struct MoveCommand
        {
            public int sceneIndex;
            public float3 deltaPosition;
            public int clusterCount;
        }
        const int CLEAR_KERNEL = 7;
        const int COLLECT_KERNEL = 8;
        const int EXECUTE_CLUSTER_KERNEL = 9;
        const int EXECUTE_POINT_KERNEL = 10;
        const int EXECUTE_CLUSTER_KERNEL_MOVE_ALL = 11;
        const int EXECUTE_POINT_KERNEL_MOVE_ALL = 12;
        public static void MoveEachScenes(NativeList<MoveCommand> allCommands)
        {
            int maximumClusterCount = 0;
            if (allCommands.Length > 0)
            {
                maximumClusterCount = allCommands[0].clusterCount;
                for (int i = 1; i < allCommands.Length; ++i)
                {
                    maximumClusterCount = max(maximumClusterCount, allCommands[i].clusterCount);
                }
            }
            ComputeShader shad = resources.shaders.streamingShader;
            ComputeBuffer tempBuffer = GetTempPropertyBuffer(maximumClusterCount + 1, sizeof(uint));
            CommandBuffer cb = RenderPipeline.BeforeFrameBuffer;
            cb.SetComputeBufferParam(shad, CLEAR_KERNEL, ShaderIDs._TempPropBuffer, tempBuffer);
            cb.SetComputeBufferParam(shad, COLLECT_KERNEL, ShaderIDs._TempPropBuffer, tempBuffer);
            cb.SetComputeBufferParam(shad, COLLECT_KERNEL, ShaderIDs.clusterBuffer, baseBuffer.clusterBuffer);
            cb.SetComputeBufferParam(shad, EXECUTE_CLUSTER_KERNEL, ShaderIDs._TempPropBuffer, tempBuffer);
            cb.SetComputeBufferParam(shad, EXECUTE_CLUSTER_KERNEL, ShaderIDs.clusterBuffer, baseBuffer.clusterBuffer);
            cb.SetComputeBufferParam(shad, EXECUTE_POINT_KERNEL, ShaderIDs.verticesBuffer, baseBuffer.verticesBuffer);
            cb.SetComputeBufferParam(shad, EXECUTE_POINT_KERNEL, ShaderIDs._TempPropBuffer, tempBuffer);
            foreach (var i in allCommands)
            {
                cb.SetComputeIntParam(shad, ShaderIDs._TargetElement, i.sceneIndex);
                cb.SetComputeVectorParam(shad, ShaderIDs._OffsetDirection, float4(i.deltaPosition, 1));
                cb.DispatchCompute(shad, CLEAR_KERNEL, 1, 1, 1);
                ComputeShaderUtility.Dispatch(shad, cb, COLLECT_KERNEL, baseBuffer.clusterCount);
                ComputeShaderUtility.Dispatch(shad, cb, EXECUTE_CLUSTER_KERNEL, i.clusterCount);
                cb.DispatchCompute(shad, EXECUTE_POINT_KERNEL, i.clusterCount, 1, 1);
            }
        }
        public static void MoveAllScenes(float3 delta, int offset, int clusterCount)
        {
            if (clusterCount <= 0) return;
            ComputeShader shad = resources.shaders.streamingShader;
            CommandBuffer cb = RenderPipeline.BeforeFrameBuffer;
            cb.SetComputeIntParam(shad, ShaderIDs._Offset, offset);
            cb.SetComputeBufferParam(shad, EXECUTE_CLUSTER_KERNEL_MOVE_ALL, ShaderIDs.clusterBuffer, baseBuffer.clusterBuffer);
            cb.SetComputeBufferParam(shad, EXECUTE_POINT_KERNEL_MOVE_ALL, ShaderIDs.verticesBuffer, baseBuffer.verticesBuffer);
            cb.SetComputeVectorParam(shad, ShaderIDs._OffsetDirection, float4(delta, 1));
            ComputeShaderUtility.Dispatch(shad, cb, EXECUTE_CLUSTER_KERNEL_MOVE_ALL, clusterCount);
            cb.DispatchCompute(shad, EXECUTE_POINT_KERNEL_MOVE_ALL, clusterCount, 1, 1);
        }
        public static void MoveScene(int sceneIndex, float3 deltaPosition, int clusterCount)
        {
            ComputeShader shad = resources.shaders.streamingShader;
            ComputeBuffer tempBuffer = GetTempPropertyBuffer(clusterCount + 1, sizeof(uint));
            CommandBuffer cb = RenderPipeline.BeforeFrameBuffer;
            cb.SetComputeIntParam(shad, ShaderIDs._TargetElement, sceneIndex);
            cb.SetComputeBufferParam(shad, CLEAR_KERNEL, ShaderIDs._TempPropBuffer, tempBuffer);
            cb.SetComputeBufferParam(shad, COLLECT_KERNEL, ShaderIDs._TempPropBuffer, tempBuffer);
            cb.SetComputeBufferParam(shad, COLLECT_KERNEL, ShaderIDs.clusterBuffer, baseBuffer.clusterBuffer);
            cb.SetComputeBufferParam(shad, EXECUTE_CLUSTER_KERNEL, ShaderIDs._TempPropBuffer, tempBuffer);
            cb.SetComputeBufferParam(shad, EXECUTE_CLUSTER_KERNEL, ShaderIDs.clusterBuffer, baseBuffer.clusterBuffer);
            cb.SetComputeBufferParam(shad, EXECUTE_POINT_KERNEL, ShaderIDs.verticesBuffer, baseBuffer.verticesBuffer);
            cb.SetComputeBufferParam(shad, EXECUTE_POINT_KERNEL, ShaderIDs._TempPropBuffer, tempBuffer);
            cb.SetComputeVectorParam(shad, ShaderIDs._OffsetDirection, float4(deltaPosition, 1));
            cb.DispatchCompute(shad, CLEAR_KERNEL, 1, 1, 1);
            ComputeShaderUtility.Dispatch(shad, cb, COLLECT_KERNEL, baseBuffer.clusterCount);
            ComputeShaderUtility.Dispatch(shad, cb, EXECUTE_CLUSTER_KERNEL, clusterCount);
            cb.DispatchCompute(shad, EXECUTE_POINT_KERNEL, clusterCount, 1, 1);
        }

        public static void Dispose(PipelineResources res)
        {
            singletonReady = false;
            PipelineFunctions.Dispose(baseBuffer);
            if (Application.isPlaying && res.clusterResources)
                res.clusterResources.Dispose();
            addList.Dispose();
            if (allTempBuffers.isCreated)
            {
                foreach (var i in allTempBuffers)
                {
                    ComputeBuffer bf = MUnsafeUtility.GetHookedObject(i.value) as ComputeBuffer;
                    bf.Dispose();
                }
            }
        }
        //Press number load scene
        private static bool GetBaseBuffer(out PipelineBaseBuffer result)
        {
            result = baseBuffer;
            return result.clusterCount > 0;
        }

        public static void RenderScene(ref PipelineCommandData data, ref FilteringSettings filterSettings, ref DrawingSettings drawSettings, ref CullingResults cullResults)
        {
            data.ExecuteCommandBuffer();
            data.context.DrawRenderers(cullResults, ref drawSettings, ref filterSettings);
        }

        public static void RenderScene(ref PipelineCommandData data, ref FilteringSettings filterSettings, ref DrawingSettings drawSettings, ref CullingResults cullResults, ref RenderStateBlock stateBlock)
        {
            data.ExecuteCommandBuffer();
            data.context.DrawRenderers(cullResults, ref drawSettings, ref filterSettings, ref stateBlock);
        }
        public static void DrawSpotLight(MLight mlight, int mask, ComputeShader cullingShader, ref PipelineCommandData data, ref SpotLight spotLights, ref RenderSpotShadowCommand spotcommand, bool inverseRender, Material opaqueOverride, NativeList_Int culledResult)
        {
            if (mlight.ShadowIndex < 0) return;
            CommandBuffer buffer = data.buffer;
            ref SpotLightMatrix spotLightMatrix = ref spotcommand.shadowMatrices[spotLights.shadowIndex];
            buffer.SetInvertCulling(true);
            buffer.DisableShaderKeyword("POINT_LIGHT_SHADOW");
            ref Camera currentCam = ref spotcommand.currentCam;
            float fov = spotLights.angle * Mathf.Rad2Deg * 2;
            currentCam.orthographic = false;
            currentCam.fieldOfView = fov;

            currentCam.nearClipPlane = spotLights.nearClip;
            currentCam.farClipPlane = spotLights.lightCone.height;
            currentCam.aspect = 1;

            currentCam.cullingMatrix = spotLightMatrix.projectionMatrix * currentCam.worldToCameraMatrix;
            buffer.SetRenderTarget(spotcommand.renderTarget, 0, CubemapFace.Unknown, mlight.ShadowIndex);
            buffer.ClearRenderTarget(true, false, new Color(float.PositiveInfinity, 1, 1, 1));
            buffer.SetGlobalMatrix(ShaderIDs._ShadowMapVP, GL.GetGPUProjectionMatrix(spotLightMatrix.projectionMatrix, true) * spotLightMatrix.worldToCamera);
            ScriptableCullingParameters cullParams;
            if (!currentCam.TryGetCullingParameters(out cullParams))
            {
                buffer.SetInvertCulling(inverseRender);
                return;
            }
            float3* frustumCorners = stackalloc float3[8];
            Transform trans = currentCam.transform;
            PerspCam perspCam = new PerspCam
            {
                fov = fov,
                nearClipPlane = spotLights.nearClip,
                farClipPlane = spotLights.lightCone.height,
                aspect = 1,
                forward = trans.forward,
                right = trans.right,
                up = trans.up,
                position = trans.position,
            };
            PipelineFunctions.GetFrustumCorner(ref perspCam, frustumCorners);
            float3 minFrustumPlanes = frustumCorners[0];
            float3 maxFrustumPlanes = frustumCorners[0];
            for (int i = 1; i < 8; ++i)
            {
                minFrustumPlanes = min(minFrustumPlanes, frustumCorners[i]);
                maxFrustumPlanes = max(maxFrustumPlanes, frustumCorners[i]);
            }
            PipelineFunctions.UpdateFrustumMinMaxPoint(buffer, minFrustumPlanes, maxFrustumPlanes);
            if (gpurpEnabled)
            {
                float4* frustumPlanes = stackalloc float4[6];
                for (int i = 0; i < 6; ++i)
                {
                    Plane p = cullParams.GetCullingPlane(i);
                    frustumPlanes[i] = new float4(-p.normal, -p.distance);
                }

                buffer.SetGlobalBuffer(ShaderIDs.resultBuffer, baseBuffer.resultBuffer);
                buffer.SetGlobalBuffer(ShaderIDs.verticesBuffer, baseBuffer.verticesBuffer);
                PipelineFunctions.SetBaseBuffer(baseBuffer, cullingShader, frustumPlanes, buffer);
                PipelineFunctions.RunCullDispatching(baseBuffer, cullingShader, buffer);
                PipelineFunctions.RenderProceduralCommand(baseBuffer, spotcommand.clusterShadowMaterial, buffer);
            }
            if (culledResult.isCreated)
                foreach (var i in culledResult)
                {
                    CustomDrawRequest.allEvents[i].DrawShadow(buffer);
                }
            data.ExecuteCommandBuffer();
            FilteringSettings renderSettings = new FilteringSettings()
            {
                renderQueueRange = new RenderQueueRange(2000, 2449),
                layerMask = mask,
                renderingLayerMask = 1
            };
            DrawingSettings dsettings = new DrawingSettings(new ShaderTagId("Shadow"), new SortingSettings { criteria = SortingCriteria.QuantizedFrontToBack })
            {
                perObjectData = UnityEngine.Rendering.PerObjectData.None,
                overrideMaterial = opaqueOverride,
                overrideMaterialPassIndex = overrideShadowmapPass
            };
            cullParams.cullingOptions = CullingOptions.ForceEvenIfCameraIsNotActive;
            CullingResults results = data.context.Cull(ref cullParams);
            data.context.DrawRenderers(results, ref dsettings, ref renderSettings);
            renderSettings.renderQueueRange = new RenderQueueRange(2450, 2499);
            dsettings.overrideMaterial = null;
            dsettings.overrideMaterialPassIndex = 0;
            var sortSetting = dsettings.sortingSettings;
            sortSetting.criteria |= SortingCriteria.OptimizeStateChanges;
            dsettings.sortingSettings = sortSetting;
            data.context.DrawRenderers(results, ref dsettings, ref renderSettings);
            buffer.SetInvertCulling(inverseRender);
        }

        public static void DrawDirectionalShadow(PipelineCamera cam, ref PipelineCommandData data, ref RenderClusterOptions opts, float* clipDistances, OrthoCam* camCoords, Matrix4x4[] shadowVPs, Material opaqueOverride)
        {
            SunLight sunLight = SunLight.current;
            opts.command.DisableShaderKeyword("POINT_LIGHT_SHADOW");
            opts.command.SetInvertCulling(true);
            Camera currentCam = cam.cam;
            Vector4 bias = sunLight.bias / currentCam.farClipPlane;
            opts.command.SetGlobalVector(ShaderIDs._ShadowOffset, bias);
            for (int pass = 0; pass < SunLight.CASCADELEVELCOUNT; ++pass)
            {
                float4* vec = (float4*)opts.frustumPlanes.Ptr();
                ref OrthoCam orthoCam = ref camCoords[pass];
                SunLight.shadowCam.cullingMatrix = shadowVPs[pass];
                SunLight.shadowCam.orthographicSize = orthoCam.size;
                SunLight.shadowCam.nearClipPlane = orthoCam.nearClipPlane;
                SunLight.shadowCam.farClipPlane = orthoCam.farClipPlane;
                Transform tr = SunLight.shadowCam.transform;
                tr.position = orthoCam.position;
                tr.up = orthoCam.up;
                tr.right = orthoCam.right;
                tr.forward = orthoCam.forward;
                ScriptableCullingParameters cullParams;
                if (!SunLight.shadowCam.TryGetCullingParameters(out cullParams))
                {
                    opts.command.SetInvertCulling(cam.inverseRender);
                    return;
                }
                for (int i = 0; i < 6; ++i)
                {
                    Plane p = cullParams.GetCullingPlane(i);
                    vec[i] = -float4(p.normal, p.distance);
                }
                Matrix4x4 vpMatrix;
                PipelineFunctions.UpdateCascadeState(sunLight, ref orthoCam.projectionMatrix, ref orthoCam.worldToCameraMatrix, opts.command, pass, out vpMatrix);
                NativeList_Int culledResult = SunLight.customCullResults[pass];
                foreach (var i in culledResult)
                {
                    CustomDrawRequest.allEvents[i].DrawShadow(opts.command);
                }
                float3* frustumCorners = stackalloc float3[8];
                PipelineFunctions.GetFrustumCorner(ref orthoCam, frustumCorners);
                float3 frustumMinValue = frustumCorners[0], frustumMaxValue = frustumCorners[0];
                for (int i = 1; i < 8; ++i)
                {
                    frustumMinValue = min(frustumCorners[i], frustumMinValue);
                    frustumMaxValue = max(frustumCorners[i], frustumMaxValue);
                }
                PipelineFunctions.UpdateFrustumMinMaxPoint(opts.command, frustumMinValue, frustumMaxValue);
                if (gpurpEnabled)
                {

                    opts.command.SetGlobalBuffer(ShaderIDs.verticesBuffer, baseBuffer.verticesBuffer);
                    opts.command.SetGlobalBuffer(ShaderIDs.resultBuffer, baseBuffer.resultBuffer);
                    PipelineFunctions.SetBaseBuffer(baseBuffer, opts.cullingShader, opts.frustumPlanes, opts.command);
                    PipelineFunctions.RunCullDispatching(baseBuffer, opts.cullingShader, opts.command);
                    opts.command.DrawProceduralIndirect(Matrix4x4.identity, sunLight.shadowDepthMaterial, 0, MeshTopology.Triangles, baseBuffer.instanceCountBuffer, 0);
                }
                if (MTerrain.current)
                {
                    MTerrain.current.DrawTerrain(opts.command, 1, vec, frustumMinValue, frustumMaxValue);
                }
                data.ExecuteCommandBuffer();
                FilteringSettings renderSettings = new FilteringSettings()
                {
                    renderQueueRange = new RenderQueueRange(2000, 2449),
                    layerMask = sunLight.shadowMask,
                    renderingLayerMask = 1
                };
                SortingSettings sorting = new SortingSettings(SunLight.shadowCam);
                sorting.criteria = SortingCriteria.QuantizedFrontToBack;
                DrawingSettings dsettings = new DrawingSettings(new ShaderTagId("Shadow"), sorting)
                {
                    perObjectData = UnityEngine.Rendering.PerObjectData.None,
                    overrideMaterial = opaqueOverride,
                    overrideMaterialPassIndex = overrideShadowmapPass
                };
                cullParams.cullingOptions = CullingOptions.ForceEvenIfCameraIsNotActive;
                CullingResults results = data.context.Cull(ref cullParams);
                data.context.DrawRenderers(results, ref dsettings, ref renderSettings);
                renderSettings.renderQueueRange = new RenderQueueRange(2450, 2499);
                dsettings.overrideMaterial = null;
                dsettings.overrideMaterialPassIndex = 0;
                var sortSetting = dsettings.sortingSettings;
                sortSetting.criteria |= SortingCriteria.OptimizeStateChanges;
                dsettings.sortingSettings = sortSetting;
                data.context.DrawRenderers(results, ref dsettings, ref renderSettings);
            }
            opts.command.SetInvertCulling(cam.inverseRender);
        }

        public static void DrawPointLight(MLight lit,
            int mask,
            ref PointLightStruct light,
            Material depthMaterial,
            ComputeShader cullingShader,
            ref PipelineCommandData data,
            ref CubemapViewProjMatrix vpMatrices,
            RenderTexture renderTarget,
            bool inverseRender,
            Material opaqueOverride)
        {
            if (lit.ShadowIndex < 0) return;
            NativeList_Int culledResult = vpMatrices.customCulledResult;
            CommandBuffer cb = data.buffer;
            cb.SetGlobalVector(ShaderIDs._LightPos, light.sphere);
            cb.SetInvertCulling(true);
            cb.EnableShaderKeyword("POINT_LIGHT_SHADOW");
            FilteringSettings renderSettings = new FilteringSettings()
            {
                renderQueueRange = new RenderQueueRange(2450, 2499),
                layerMask = mask,
                renderingLayerMask = 1
            };
            FilteringSettings opaqueRenderSettings = new FilteringSettings()
            {
                renderQueueRange = new RenderQueueRange(2000, 2449),
                layerMask = mask,
                renderingLayerMask = 1
            };
            DrawingSettings dsettings = new DrawingSettings(new ShaderTagId("Shadow"), new SortingSettings { criteria = SortingCriteria.OptimizeStateChanges })
            {
                enableDynamicBatching = false,
                enableInstancing = false,
                perObjectData = UnityEngine.Rendering.PerObjectData.None,
            };
            DrawingSettings opaqueRender = dsettings;
            opaqueRender.overrideMaterial = opaqueOverride;
            opaqueRender.overrideMaterialPassIndex = overrideShadowmapPass;
            var sortSetting = opaqueRender.sortingSettings;
            sortSetting.criteria = SortingCriteria.None;
            opaqueRender.sortingSettings = sortSetting;
            int depthSlice = lit.ShadowIndex * 6;
            float size = light.sphere.w;
            lit.shadowCam.orthographic = true;
            lit.shadowCam.nearClipPlane = -size;
            lit.shadowCam.farClipPlane = size;
            lit.shadowCam.aspect = 1;
            lit.shadowCam.orthographicSize = size;
            lit.shadowCam.cullingMatrix = Matrix4x4.Ortho(-size, size, -size, size, -size, size) * lit.shadowCam.worldToCameraMatrix;
            ScriptableCullingParameters cullParams;
            if (!lit.shadowCam.TryGetCullingParameters(out cullParams))
            {
                cb.SetInvertCulling(inverseRender);
                return;
            }
            cullParams.cullingOptions = CullingOptions.ForceEvenIfCameraIsNotActive;
            CullingResults results = data.context.Cull(ref cullParams);
            PipelineFunctions.UpdateFrustumMinMaxPoint(cb, light.sphere.xyz - size, light.sphere.xyz + size);
            if (gpurpEnabled)
            {

                PipelineFunctions.SetBaseBuffer(baseBuffer, cullingShader, vpMatrices.frustumPlanes, cb);
                PipelineFunctions.RunCullDispatching(baseBuffer, cullingShader, cb);

            }
            void DrawFace(int renderSlice, ref Matrix4x4 shadowmapVP, ref PipelineCommandData commandData)
            {
                cb.SetRenderTarget(renderTarget, 0, CubemapFace.Unknown, renderSlice);
                cb.ClearRenderTarget(true, true, new Color(float.PositiveInfinity, 1, 1, 1));
                cb.SetGlobalMatrix(ShaderIDs._ShadowMapVP, shadowmapVP);
                if (gpurpEnabled)
                {
                    cb.SetGlobalBuffer(ShaderIDs.resultBuffer, baseBuffer.resultBuffer);
                    cb.SetGlobalBuffer(ShaderIDs.verticesBuffer, baseBuffer.verticesBuffer);
                    cb.DrawProceduralIndirect(Matrix4x4.identity, depthMaterial, 0, MeshTopology.Triangles, baseBuffer.instanceCountBuffer);
                }
                if (culledResult.isCreated)
                    foreach (var i in culledResult)
                    {
                        CustomDrawRequest.allEvents[i].DrawShadow(cb);
                    }
                commandData.ExecuteCommandBuffer();
                commandData.context.DrawRenderers(results, ref opaqueRender, ref opaqueRenderSettings);
                commandData.context.DrawRenderers(results, ref dsettings, ref renderSettings);
            }
            //X
            DrawFace(depthSlice + 1, ref vpMatrices.rightProjView, ref data);
            //-X
            DrawFace(depthSlice, ref vpMatrices.leftProjView, ref data);
            //Y
            DrawFace(depthSlice + 3, ref vpMatrices.upProjView, ref data);
            //-Y
            DrawFace(depthSlice + 2, ref vpMatrices.downProjView, ref data);
            //Z
            DrawFace(depthSlice + 5, ref vpMatrices.forwardProjView, ref data);
            //-Z
            DrawFace(depthSlice + 4, ref vpMatrices.backProjView, ref data);
            cb.SetInvertCulling(inverseRender);
        }
        public static void CullCluster_LastFrameDepthHiZ(ref RenderClusterOptions options, HizOcclusionData hizOpts, PipelineCamera pipeCam)
        {
            ref RenderTargets rendTargets = ref pipeCam.targets;
            Camera cam = pipeCam.cam;
            CommandBuffer buffer = options.command;
            ComputeShader gpuFrustumShader = options.cullingShader;
            buffer.SetGlobalVector(ShaderIDs._HizScreenRes, new Vector4(hizOpts.targetWidth, hizOpts.targetWidth / 2, hizOpts.mip - 0.5f, hizOpts.mip - 1));
            buffer.SetGlobalBuffer(ShaderIDs.resultBuffer, baseBuffer.resultBuffer);
            buffer.SetGlobalBuffer(ShaderIDs.verticesBuffer, baseBuffer.verticesBuffer);
            buffer.SetComputeBufferParam(gpuFrustumShader, PipelineBaseBuffer.ClearCluster_Kernel, ShaderIDs.instanceCountBuffer, baseBuffer.instanceCountBuffer);
            buffer.DispatchCompute(gpuFrustumShader, PipelineBaseBuffer.ClearCluster_Kernel, 1, 1, 1);
            PipelineFunctions.UpdateOcclusionBuffer(
baseBuffer, gpuFrustumShader,
buffer,
hizOpts,
options.frustumPlanes);
        }

        public static void DrawCluster_RecheckHiz(ref RenderClusterOptions options, ref HizDepth hizDepth, HizOcclusionData hizOpts, Material targetMat, Material linearLODMaterial, PipelineCamera pipeCam)
        {
            ref RenderTargets rendTargets = ref pipeCam.targets;
            Camera cam = pipeCam.cam;
            CommandBuffer buffer = options.command;
            ComputeShader gpuFrustumShader = options.cullingShader;

            buffer.BlitSRT(hizOpts.historyDepth, linearLODMaterial, 0);
            hizDepth.GetMipMap(hizOpts.historyDepth, buffer, hizOpts.mip);
            //double check
            PipelineFunctions.ClearOcclusionData(baseBuffer, buffer, gpuFrustumShader);
            PipelineFunctions.OcclusionRecheck(baseBuffer, gpuFrustumShader, buffer, hizOpts);
            buffer.SetGlobalBuffer(ShaderIDs.resultBuffer, baseBuffer.resultBuffer);
            buffer.SetGlobalBuffer(ShaderIDs.verticesBuffer, baseBuffer.verticesBuffer);
            //double draw
            buffer.SetRenderTarget(colors: rendTargets.gbufferIdentifier, depth: ShaderIDs._DepthBufferTexture);
            buffer.DrawProceduralIndirect(Matrix4x4.identity, targetMat, 1, MeshTopology.Triangles, baseBuffer.reCheckCount, 0);

        }
    }
}