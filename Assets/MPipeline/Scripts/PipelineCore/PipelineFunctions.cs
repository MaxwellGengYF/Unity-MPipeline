using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using UnityEngine.Rendering;
using Unity.Collections.LowLevel.Unsafe;
using System;
using System.Text;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using MPipeline;

public unsafe static class PipelineFunctions
{

    public static void GetOrthoCullingPlanes(ref OrthoCam orthoCam, float4* planes)
    {
        planes[0] = VectorUtility.GetPlane(orthoCam.forward, orthoCam.position + orthoCam.forward * orthoCam.farClipPlane);
        planes[1] = VectorUtility.GetPlane(-orthoCam.forward, orthoCam.position + orthoCam.forward * orthoCam.nearClipPlane);
        planes[2] = VectorUtility.GetPlane(-orthoCam.up, orthoCam.position - orthoCam.up * orthoCam.size);
        planes[3] = VectorUtility.GetPlane(orthoCam.up, orthoCam.position + orthoCam.up * orthoCam.size);
        planes[4] = VectorUtility.GetPlane(orthoCam.right, orthoCam.position + orthoCam.right * orthoCam.size);
        planes[5] = VectorUtility.GetPlane(-orthoCam.right, orthoCam.position - orthoCam.right * orthoCam.size);
    }
    public static void RunPostProcess(ref RenderTargets targets, out RenderTargetIdentifier source, out RenderTargetIdentifier dest)
    {
        source = targets.renderTargetIdentifier;
        dest = targets.backupIdentifier;
        targets.backupIdentifier = targets.renderTargetIdentifier;
        targets.renderTargetIdentifier = dest;
    }
    public static void GetFrustumCorner(ref PerspCam perspCam, float distance, float3* corners)
    {
        perspCam.fov = Mathf.Deg2Rad * perspCam.fov * 0.5f;
        float upLength = distance * tan(perspCam.fov);
        float rightLength = upLength * perspCam.aspect;
        float3 farPoint = perspCam.position + distance * perspCam.forward;
        float3 upVec = upLength * perspCam.up;
        float3 rightVec = rightLength * perspCam.right;
        corners[0] = farPoint - upVec - rightVec;
        corners[1] = farPoint - upVec + rightVec;
        corners[2] = farPoint + upVec - rightVec;
        corners[3] = farPoint + upVec + rightVec;
    }

    public static void GetFrustumPlanes(ref PerspCam perspCam, float4* planes)
    {
        float3* corners = stackalloc float3[4];
        GetFrustumCorner(ref perspCam, perspCam.farClipPlane, corners);
        planes[0] = VectorUtility.GetPlane(corners[1], corners[0], perspCam.position);
        planes[1] = VectorUtility.GetPlane(corners[2], corners[3], perspCam.position);
        planes[2] = VectorUtility.GetPlane(corners[0], corners[2], perspCam.position);
        planes[3] = VectorUtility.GetPlane(corners[3], corners[1], perspCam.position);
        planes[4] = VectorUtility.GetPlane(perspCam.forward, perspCam.position + perspCam.forward * perspCam.farClipPlane);
        planes[5] = VectorUtility.GetPlane(-perspCam.forward, perspCam.position + perspCam.forward * perspCam.nearClipPlane);
    }
    public static void GetFrustumPlanes(ref OrthoCam ortho, float4* planes)
    {
        planes[0] = VectorUtility.GetPlane(ortho.up, ortho.position + ortho.up * ortho.size);
        planes[1] = VectorUtility.GetPlane(-ortho.up, ortho.position - ortho.up * ortho.size);
        planes[2] = VectorUtility.GetPlane(ortho.right, ortho.position + ortho.right * ortho.size);
        planes[3] = VectorUtility.GetPlane(-ortho.right, ortho.position - ortho.right * ortho.size);
        planes[4] = VectorUtility.GetPlane(ortho.forward, ortho.position + ortho.forward * ortho.farClipPlane);
        planes[5] = VectorUtility.GetPlane(-ortho.forward, ortho.position + ortho.forward * ortho.nearClipPlane);
    }

    //TODO: Streaming Loading
    /// <summary>
    /// Initialize pipeline buffers
    /// </summary>
    /// <param name="baseBuffer"></param> pipeline base buffer
    public static void InitBaseBuffer(PipelineBaseBuffer baseBuffer, ClusterMatResources materialResources, string name, int maximumLength)
    {
        if (maximumLength <= 0) return;
        baseBuffer.clusterBuffer = new ComputeBuffer(maximumLength, sizeof(Cluster));
        baseBuffer.resultBuffer = new ComputeBuffer(maximumLength, sizeof(uint));
        baseBuffer.instanceCountBuffer = new ComputeBuffer(5, 4, ComputeBufferType.IndirectArguments);
        NativeArray<uint> instanceCountBufferValue = new NativeArray<uint>(5, Allocator.Temp);
        instanceCountBufferValue[0] = PipelineBaseBuffer.CLUSTERVERTEXCOUNT;
        baseBuffer.instanceCountBuffer.SetData(instanceCountBufferValue);
        baseBuffer.moveCountBuffer = new ComputeBuffer(5, sizeof(int), ComputeBufferType.IndirectArguments);
        baseBuffer.verticesBuffer = new ComputeBuffer(maximumLength * PipelineBaseBuffer.CLUSTERCLIPCOUNT, sizeof(Point));
        baseBuffer.clusterCount = 0;
        if (GeometryEvent.useHiZ)
        {
            baseBuffer.reCheckCount = new ComputeBuffer(5, sizeof(int), ComputeBufferType.IndirectArguments);
            baseBuffer.dispatchBuffer = new ComputeBuffer(5, sizeof(int), ComputeBufferType.IndirectArguments);
            baseBuffer.reCheckResult = new ComputeBuffer(baseBuffer.resultBuffer.count, sizeof(uint));
            baseBuffer.reCheckCount.SetData(instanceCountBufferValue);
            UnsafeUtility.MemClear(instanceCountBufferValue.GetUnsafePtr(), 5 * sizeof(int));
            instanceCountBufferValue[1] = 1;
            instanceCountBufferValue[2] = 1;
            baseBuffer.dispatchBuffer.SetData(instanceCountBufferValue);
        }
    }

    public static void GetfrustumCorners(float* planes, int planesCount, Camera cam, float3* frustumCorners)
    {
        for (int i = 0; i < planesCount; ++i)
        {
            int index = i * 4;
            float p = planes[i];
            frustumCorners[index] = cam.ViewportToWorldPoint(new Vector3(0, 0, p));
            frustumCorners[1 + index] = cam.ViewportToWorldPoint(new Vector3(0, 1, p));
            frustumCorners[2 + index] = cam.ViewportToWorldPoint(new Vector3(1, 1, p));
            frustumCorners[3 + index] = cam.ViewportToWorldPoint(new Vector3(1, 0, p));
        }
    }

    public static int DownDimension(int3 coord, int2 xysize)
    {
        return coord.z * xysize.y * xysize.x + coord.y * xysize.x + coord.x;
    }

    public static int3 UpDimension(int coord, int2 xysize)
    {
        int xy = (xysize.x * xysize.y);
        return int3(coord % xysize.x, (coord % xy) / xysize.x, coord / xy);
    }

    public static bool FrustumCulling(ref Matrix4x4 ObjectToWorld, Vector3 extent, Vector4* frustumPlanes)
    {
        Vector3 right = new Vector3(ObjectToWorld.m00, ObjectToWorld.m10, ObjectToWorld.m20);
        Vector3 up = new Vector3(ObjectToWorld.m01, ObjectToWorld.m11, ObjectToWorld.m21);
        Vector3 forward = new Vector3(ObjectToWorld.m02, ObjectToWorld.m12, ObjectToWorld.m22);
        Vector3 position = new Vector3(ObjectToWorld.m03, ObjectToWorld.m13, ObjectToWorld.m23);
        for (int i = 0; i < 6; ++i)
        {
            ref Vector4 plane = ref frustumPlanes[i];
            Vector3 normal = new Vector3(plane.x, plane.y, plane.z);
            float distance = plane.w;
            float r = Vector3.Dot(position, normal);
            Vector3 absNormal = new Vector3(Mathf.Abs(Vector3.Dot(normal, right)), Mathf.Abs(Vector3.Dot(normal, up)), Mathf.Abs(Vector3.Dot(normal, forward)));
            float f = Vector3.Dot(absNormal, extent);
            if ((r - f) >= -distance)
                return false;
        }
        return true;
    }

    public static bool FrustumCulling(Vector3 position, float range, Vector4* frustumPlanes)
    {
        for (int i = 0; i < 5; ++i)
        {
            ref Vector4 plane = ref frustumPlanes[i];
            Vector3 normal = new Vector3(plane.x, plane.y, plane.z);
            float rayDist = Vector3.Dot(normal, position);
            rayDist += plane.w;
            if (rayDist > range)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Initialize per cascade shadowmap buffers
    /// </summary>
    public static void UpdateCascadeState(SunLight comp, ref float4x4 projection, ref float4x4 worldToCamera, CommandBuffer buffer, int pass, out Matrix4x4 rtVp)
    {
        buffer.SetRenderTarget(comp.shadowmapTexture, 0, CubemapFace.Unknown, depthSlice: pass);
        buffer.ClearRenderTarget(true, true, SystemInfo.usesReversedZBuffer ? Color.black : Color.white);
        rtVp = mul(GraphicsUtility.GetGPUProjectionMatrix(projection, true), worldToCamera);
        buffer.SetGlobalMatrix(ShaderIDs._ShadowMapVP, rtVp);
    }
    /// <summary>
    /// Initialize shadowmask per frame buffers
    /// </summary>


    public static void Dispose(PipelineBaseBuffer baseBuffer)
    {
        void DisposeBuffer(ComputeBuffer bf)
        {
            if (bf != null) bf.Dispose();
        }
        DisposeBuffer(baseBuffer.verticesBuffer);
        DisposeBuffer(baseBuffer.clusterBuffer);
        if (GeometryEvent.useHiZ)
        {
            DisposeBuffer(baseBuffer.reCheckCount);
            DisposeBuffer(baseBuffer.reCheckResult);
            DisposeBuffer(baseBuffer.dispatchBuffer);
        }
        DisposeBuffer(baseBuffer.instanceCountBuffer);
        DisposeBuffer(baseBuffer.resultBuffer);
        DisposeBuffer(baseBuffer.moveCountBuffer);
    }
    /// <summary>
    /// Set Basement buffers
    /// </summary>
    public static void SetBaseBuffer(PipelineBaseBuffer baseBuffer, ComputeShader gpuFrustumShader, Vector4[] frustumCullingPlanes, CommandBuffer buffer)
    {
        var compute = gpuFrustumShader;
        buffer.SetComputeVectorArrayParam(compute, ShaderIDs.planes, frustumCullingPlanes);
        buffer.SetComputeBufferParam(compute, PipelineBaseBuffer.ClusterCull_Kernel, ShaderIDs.clusterBuffer, baseBuffer.clusterBuffer);
        buffer.SetComputeBufferParam(compute, PipelineBaseBuffer.ClusterCull_Kernel, ShaderIDs.instanceCountBuffer, baseBuffer.instanceCountBuffer);
        buffer.SetComputeBufferParam(compute, PipelineBaseBuffer.ClearCluster_Kernel, ShaderIDs.instanceCountBuffer, baseBuffer.instanceCountBuffer);
        buffer.DispatchCompute(compute, PipelineBaseBuffer.ClearCluster_Kernel, 1, 1, 1);
        buffer.SetComputeBufferParam(compute, PipelineBaseBuffer.ClusterCull_Kernel, ShaderIDs.resultBuffer, baseBuffer.resultBuffer);
    }

    public static void SetBaseBufferOcc(PipelineBaseBuffer baseBuffer, ComputeShader gpuFrustumShader, Vector4[] frustumCullingPlanes, CommandBuffer buffer)
    {
        var compute = gpuFrustumShader;
        buffer.SetComputeVectorArrayParam(compute, ShaderIDs.planes, frustumCullingPlanes);
        buffer.SetComputeBufferParam(compute, PipelineBaseBuffer.UnsafeCull_Kernel, ShaderIDs.clusterBuffer, baseBuffer.clusterBuffer);
        buffer.SetComputeBufferParam(compute, PipelineBaseBuffer.UnsafeCull_Kernel, ShaderIDs.instanceCountBuffer, baseBuffer.instanceCountBuffer);
        buffer.SetComputeBufferParam(compute, PipelineBaseBuffer.ClearCluster_Kernel, ShaderIDs.instanceCountBuffer, baseBuffer.instanceCountBuffer);
        buffer.DispatchCompute(compute, PipelineBaseBuffer.ClearCluster_Kernel, 1, 1, 1);
        buffer.SetComputeBufferParam(compute, PipelineBaseBuffer.UnsafeCull_Kernel, ShaderIDs.resultBuffer, baseBuffer.resultBuffer);
    }
    private static Vector4[] backupFrustumArray = new Vector4[6];

    public static void SetBaseBuffer(PipelineBaseBuffer baseBuffer, ComputeShader gpuFrustumShader, float4* frustumCullingPlanes, CommandBuffer buffer)
    {
        var compute = gpuFrustumShader;
        UnsafeUtility.MemCpy(backupFrustumArray.Ptr(), frustumCullingPlanes, sizeof(float4) * 6);
        buffer.SetComputeVectorArrayParam(compute, ShaderIDs.planes, backupFrustumArray);
        buffer.SetComputeBufferParam(compute, PipelineBaseBuffer.ClusterCull_Kernel, ShaderIDs.clusterBuffer, baseBuffer.clusterBuffer);
        buffer.SetComputeBufferParam(compute, PipelineBaseBuffer.ClusterCull_Kernel, ShaderIDs.instanceCountBuffer, baseBuffer.instanceCountBuffer);
        buffer.SetComputeBufferParam(compute, PipelineBaseBuffer.ClearCluster_Kernel, ShaderIDs.instanceCountBuffer, baseBuffer.instanceCountBuffer);
        buffer.DispatchCompute(compute, PipelineBaseBuffer.ClearCluster_Kernel, 1, 1, 1);
        buffer.SetComputeBufferParam(compute, PipelineBaseBuffer.ClusterCull_Kernel, ShaderIDs.resultBuffer, baseBuffer.resultBuffer);
    }

    public static void SetBaseBufferOcc(PipelineBaseBuffer baseBuffer, ComputeShader gpuFrustumShader, float4* frustumCullingPlanes, CommandBuffer buffer)
    {
        var compute = gpuFrustumShader;
        UnsafeUtility.MemCpy(backupFrustumArray.Ptr(), frustumCullingPlanes, sizeof(float4) * 6);
        buffer.SetComputeVectorArrayParam(compute, ShaderIDs.planes, backupFrustumArray);
        buffer.SetComputeBufferParam(compute, PipelineBaseBuffer.UnsafeCull_Kernel, ShaderIDs.clusterBuffer, baseBuffer.clusterBuffer);
        buffer.SetComputeBufferParam(compute, PipelineBaseBuffer.UnsafeCull_Kernel, ShaderIDs.instanceCountBuffer, baseBuffer.instanceCountBuffer);
        buffer.SetComputeBufferParam(compute, PipelineBaseBuffer.ClearCluster_Kernel, ShaderIDs.instanceCountBuffer, baseBuffer.instanceCountBuffer);
        buffer.DispatchCompute(compute, PipelineBaseBuffer.ClearCluster_Kernel, 1, 1, 1);
        buffer.SetComputeBufferParam(compute, PipelineBaseBuffer.UnsafeCull_Kernel, ShaderIDs.resultBuffer, baseBuffer.resultBuffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RunCullDispatching(PipelineBaseBuffer baseBuffer, ComputeShader computeShader, CommandBuffer buffer)
    {
        ComputeShaderUtility.Dispatch(computeShader, buffer, PipelineBaseBuffer.ClusterCull_Kernel, baseBuffer.clusterCount);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RunCullDispatchingOcc(PipelineBaseBuffer baseBuffer, ComputeShader computeShader, CommandBuffer buffer)
    {
        ComputeShaderUtility.Dispatch(computeShader, buffer, PipelineBaseBuffer.UnsafeCull_Kernel, baseBuffer.clusterCount);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RenderProceduralCommand(PipelineBaseBuffer buffer, Material material, CommandBuffer cb)
    {
        cb.DrawProceduralIndirect(Matrix4x4.identity, material, 0, MeshTopology.Triangles, buffer.instanceCountBuffer, 0);
    }
    public static void InitRenderTarget(ref RenderTargets tar, Camera tarcam, CommandBuffer buffer)
    {
        buffer.GetTemporaryRT(tar.gbufferIndex[0], tarcam.pixelWidth, tarcam.pixelHeight, 0, FilterMode.Point, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear, 1, false);
        buffer.GetTemporaryRT(tar.gbufferIndex[1], tarcam.pixelWidth, tarcam.pixelHeight, 0, FilterMode.Point, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear, 1, false);
        buffer.GetTemporaryRT(tar.gbufferIndex[2], tarcam.pixelWidth, tarcam.pixelHeight, 0, FilterMode.Point, RenderTextureFormat.ARGB2101010, RenderTextureReadWrite.Linear, 1, false);
        buffer.GetTemporaryRT(tar.gbufferIndex[3], tarcam.pixelWidth, tarcam.pixelHeight, 0, FilterMode.Bilinear, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear, 1, false);
        buffer.GetTemporaryRT(ShaderIDs._DepthBufferTexture, tarcam.pixelWidth, tarcam.pixelHeight, 32, FilterMode.Point, RenderTextureFormat.Depth, RenderTextureReadWrite.Linear, 1, false);
        buffer.GetTemporaryRT(ShaderIDs._CameraDepthTexture, tarcam.pixelWidth, tarcam.pixelHeight, 0, FilterMode.Point, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear, 1, false);
        buffer.GetTemporaryRT(ShaderIDs._BackupMap, tarcam.pixelWidth, tarcam.pixelHeight, 0, FilterMode.Bilinear, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear, 1, false);
        buffer.GetTemporaryRT(ShaderIDs._CameraMotionVectorsTexture, tarcam.pixelWidth, tarcam.pixelHeight, 0, FilterMode.Bilinear, RenderTextureFormat.RGHalf, RenderTextureReadWrite.Linear, 1, false);
        foreach (var i in tar.gbufferIndex)
            MPipeline.RenderPipeline.ReleaseRTAfterFrame(i);
        MPipeline.RenderPipeline.ReleaseRTAfterFrame(ShaderIDs._DepthBufferTexture);
        MPipeline.RenderPipeline.ReleaseRTAfterFrame(ShaderIDs._CameraDepthTexture);
        MPipeline.RenderPipeline.ReleaseRTAfterFrame(ShaderIDs._BackupMap);
        MPipeline.RenderPipeline.ReleaseRTAfterFrame(ShaderIDs._CameraMotionVectorsTexture);
        tar.renderTargetIdentifier = tar.gbufferIndex[3];
        tar.backupIdentifier = ShaderIDs._BackupMap;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ExecuteCommandBuffer(ref this PipelineCommandData data)
    {
        data.context.ExecuteCommandBuffer(data.buffer);
        data.buffer.Clear();
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ExecuteCommandBufferAsync(ref this PipelineCommandData data, CommandBuffer asyncBuffer, ComputeQueueType queueType)
    {
        data.context.ExecuteCommandBufferAsync(asyncBuffer, queueType);
        asyncBuffer.Clear();
    }

    public static void UpdateOcclusionBuffer(
PipelineBaseBuffer basebuffer
, ComputeShader coreShader
, CommandBuffer buffer
, HizOcclusionData occlusionData
, Vector4[] frustumCullingPlanes)
    {
        buffer.SetComputeVectorArrayParam(coreShader, ShaderIDs.planes, frustumCullingPlanes);
        buffer.SetComputeVectorParam(coreShader, ShaderIDs._CameraUpVector, occlusionData.lastFrameCameraUp);
        buffer.SetComputeBufferParam(coreShader, PipelineBaseBuffer.FrustumFilter_Kernel, ShaderIDs.clusterBuffer, basebuffer.clusterBuffer);
        buffer.SetComputeTextureParam(coreShader, PipelineBaseBuffer.FrustumFilter_Kernel, ShaderIDs._HizDepthTex, occlusionData.historyDepth);
        buffer.SetComputeBufferParam(coreShader, PipelineBaseBuffer.FrustumFilter_Kernel, ShaderIDs.dispatchBuffer, basebuffer.dispatchBuffer);
        buffer.SetComputeBufferParam(coreShader, PipelineBaseBuffer.FrustumFilter_Kernel, ShaderIDs.resultBuffer, basebuffer.resultBuffer);
        buffer.SetComputeBufferParam(coreShader, PipelineBaseBuffer.FrustumFilter_Kernel, ShaderIDs.instanceCountBuffer, basebuffer.instanceCountBuffer);
        buffer.SetComputeBufferParam(coreShader, PipelineBaseBuffer.FrustumFilter_Kernel, ShaderIDs.reCheckResult, basebuffer.reCheckResult);
        ComputeShaderUtility.Dispatch(coreShader, buffer, PipelineBaseBuffer.FrustumFilter_Kernel, basebuffer.clusterCount);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ClearOcclusionData(
        PipelineBaseBuffer baseBuffer, CommandBuffer buffer
        , ComputeShader coreShader)
    {
        buffer.SetComputeBufferParam(coreShader, PipelineBaseBuffer.ClearOcclusionData_Kernel, ShaderIDs.dispatchBuffer, baseBuffer.dispatchBuffer);
        buffer.SetComputeBufferParam(coreShader, PipelineBaseBuffer.ClearOcclusionData_Kernel, ShaderIDs.instanceCountBuffer, baseBuffer.instanceCountBuffer);
        buffer.SetComputeBufferParam(coreShader, PipelineBaseBuffer.ClearOcclusionData_Kernel, ShaderIDs.reCheckCount, baseBuffer.reCheckCount);
        buffer.DispatchCompute(coreShader, PipelineBaseBuffer.ClearOcclusionData_Kernel, 1, 1, 1);
    }

    public static void OcclusionRecheck(
        PipelineBaseBuffer baseBuffer
        , ComputeShader coreShader, CommandBuffer buffer
        , HizOcclusionData hizData)
    {
        buffer.SetComputeVectorParam(coreShader, ShaderIDs._CameraUpVector, hizData.lastFrameCameraUp);
        buffer.SetComputeBufferParam(coreShader, PipelineBaseBuffer.OcclusionRecheck_Kernel, ShaderIDs.dispatchBuffer, baseBuffer.dispatchBuffer);
        buffer.SetComputeBufferParam(coreShader, PipelineBaseBuffer.OcclusionRecheck_Kernel, ShaderIDs.reCheckResult, baseBuffer.reCheckResult);
        buffer.SetComputeBufferParam(coreShader, PipelineBaseBuffer.OcclusionRecheck_Kernel, ShaderIDs.clusterBuffer, baseBuffer.clusterBuffer);
        buffer.SetComputeTextureParam(coreShader, PipelineBaseBuffer.OcclusionRecheck_Kernel, ShaderIDs._HizDepthTex, hizData.historyDepth);
        buffer.SetComputeBufferParam(coreShader, PipelineBaseBuffer.OcclusionRecheck_Kernel, ShaderIDs.reCheckCount, baseBuffer.reCheckCount);
        buffer.SetComputeBufferParam(coreShader, PipelineBaseBuffer.OcclusionRecheck_Kernel, ShaderIDs.resultBuffer, baseBuffer.resultBuffer);
        buffer.DispatchCompute(coreShader, PipelineBaseBuffer.OcclusionRecheck_Kernel, baseBuffer.dispatchBuffer, 0);
    }

    public static void InsertTo<T>(this List<T> targetArray, T value, Func<T, T, int> compareResult)
    {
        Vector2Int range = new Vector2Int(0, targetArray.Count);
        while (true)
        {
            if (targetArray.Count == 0)
            {
                targetArray.Add(value);
                return;
            }
            else if (abs(range.x - range.y) == 1)
            {
                int compareX = compareResult(targetArray[range.x], value);
                if (compareX < 0)
                {
                    targetArray.Insert(range.x, value);
                    return;
                }
                else if (compareX > 0)
                {
                    if (range.y < targetArray.Count && compareResult(targetArray[range.y], value) == 0)
                    {
                        return;
                    }
                    else
                    {
                        targetArray.Insert(range.y, value);
                        return;
                    }
                }
                else
                {
                    return;
                }
            }
            else
            {
                int currentIndex = (int)((range.x + range.y) / 2f);
                int compare = compareResult(targetArray[currentIndex], value);
                if (compare == 0)
                {
                    return;
                }
                else
                {
                    if (compare < 0)
                    {
                        range.y = currentIndex;
                    }
                    else if (compare > 0)
                    {
                        range.x = currentIndex;
                    }
                }
            }
        }
    }

    public static void CopyToCubeMap(RenderTexture cubemapArray, RenderTexture texArray, CommandBuffer buffer, int offset)
    {
        offset *= 6;
        for (int i = 0; i < 6; ++i)
        {
            buffer.CopyTexture(texArray, i, cubemapArray, offset + i);
        }
    }
}