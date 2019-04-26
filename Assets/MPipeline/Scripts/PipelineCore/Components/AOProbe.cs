using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using System;
using Unity.Collections.LowLevel.Unsafe;
using static Unity.Mathematics.math;
using UnityEngine.Rendering;
namespace MPipeline
{
    public unsafe sealed class AOProbe : MonoBehaviour
    {
        public int3 resolution = int3(1, 1, 1);
        public static NativeList<UIntPtr> allProbe { get; private set; }
        private int index;
        public RenderTexture src0 { get; private set; }
        public RenderTexture src1 { get; private set; }
        public RenderTexture src2 { get; private set; }
        public PipelineResources resources;
        public float radius;
        [HideInInspector]
        [SerializeField]
        private string fileName;
        private void OnEnable()
        {
            if (!allProbe.isCreated) allProbe = new NativeList<UIntPtr>(10, Allocator.Persistent);
            index = allProbe.Length;
            allProbe.Add(new UIntPtr(MUnsafeUtility.GetManagedPtr(this)));

        }

        private void OnDisable()
        {
            allProbe[index] = allProbe[allProbe.Length - 1];
            AOProbe prb = MUnsafeUtility.GetObject<AOProbe>(allProbe[index].ToPointer());
            prb.index = index;
        }
        #region BAKE
#if UNITY_EDITOR
        private PipelineCamera bakeCamera;
        private RenderTexture cameraTarget;
        private Action<CommandBuffer> bakeAction;
        private Action<AsyncGPUReadbackRequest> readBackAction;
        private CommandBuffer buffer;
        private ComputeBuffer occlusionBuffer;
        private ComputeBuffer finalBuffer;
        private NativeList<float3x3> finalData;
        private JobHandle lastHandle;
        [EasyButtons.Button]
        private void BakeTest()
        {
            StartCoroutine(BakeOcclusion());
        }
        private void InitBake()
        {
            GameObject obj = new GameObject("BakeCam", typeof(Camera), typeof(PipelineCamera));
            bakeCamera = obj.GetComponent<PipelineCamera>();
            bakeCamera.cam = obj.GetComponent<Camera>();
            bakeCamera.renderingPath = PipelineResources.CameraRenderingPath.Unlit;
            bakeCamera.inverseRender = true;
            bakeAction = BakeOcclusionData;
            readBackAction = ReadBack;
            finalData = new NativeList<float3x3>(resolution.x * resolution.y * resolution.z * 1024, Allocator.Persistent);
            cameraTarget = new RenderTexture(new RenderTextureDescriptor
            {
                msaaSamples = 1,
                width = 256,
                height = 256,
                depthBufferBits = 16,
                colorFormat = RenderTextureFormat.RHalf,
                dimension = TextureDimension.Cube,
                volumeDepth = 1
            });
            buffer = new CommandBuffer();
            occlusionBuffer = new ComputeBuffer(1024 * 1024, sizeof(float) * 9);
            finalBuffer = new ComputeBuffer(1024, sizeof(float) * 9);
        }
        private static void CalculateCubemapMatrix(float3 position, float farClipPlane, float nearClipPlane, out NativeList<float4x4> viewMatrices, out NativeList<float4x4> projMatrices)
        {
            viewMatrices = new NativeList<float4x4>(6, 6, Allocator.Temp);
            projMatrices = new NativeList<float4x4>(6, 6, Allocator.Temp);
            PerspCam cam = new PerspCam();
            cam.aspect = 1;
            cam.farClipPlane = farClipPlane;
            cam.nearClipPlane = nearClipPlane;
            cam.position = position;
            cam.fov = 90f;
            //Forward
            cam.right = float3(1, 0, 0);
            cam.up = float3(0, 1, 0);
            cam.forward = float3(0, 0, 1);
            cam.UpdateTRSMatrix();
            cam.UpdateProjectionMatrix();
            for (int i = 0; i < 6; ++i)
                projMatrices[i] = cam.projectionMatrix;
            float4x4 proj = GraphicsUtility.GetGPUProjectionMatrix(cam.projectionMatrix, true);
            viewMatrices[5] = mul(proj, cam.worldToCameraMatrix);
            //Back
            cam.right = float3(-1, 0, 0);
            cam.up = float3(0, 1, 0);
            cam.forward = float3(0, 0, -1);
            cam.UpdateTRSMatrix();

            viewMatrices[4] = mul(proj, cam.worldToCameraMatrix);
            //Up
            cam.right = float3(-1, 0, 0);
            cam.up = float3(0, 0, 1);
            cam.forward = float3(0, 1, 0);
            cam.UpdateTRSMatrix();
            viewMatrices[3] = mul(proj, cam.worldToCameraMatrix);
            //Down
            cam.right = float3(-1, 0, 0);
            cam.up = float3(0, 0, -1);
            cam.forward = float3(0, -1, 0);
            cam.UpdateTRSMatrix();
            viewMatrices[2] = mul(proj, cam.worldToCameraMatrix);
            //Right
            cam.up = float3(0, 1, 0);
            cam.right = float3(0, 0, -1);
            cam.forward = float3(1, 0, 0);
            cam.UpdateTRSMatrix();
            viewMatrices[1] = mul(proj, cam.worldToCameraMatrix);
            //Left
            cam.up = float3(0, 1, 0);
            cam.right = float3(0, 0, 1);
            cam.forward = float3(-1, 0, 0);
            cam.UpdateTRSMatrix();
            viewMatrices[0] = mul(proj, cam.worldToCameraMatrix);
        }
        private float3 currentPos;
        private IEnumerator BakeOcclusion()
        {
            InitBake();
            yield return null;
            yield return null;
            float3 left = transform.position - transform.localScale * 0.5f;
            float3 right = transform.position + transform.localScale * 0.5f;
            for (int x = 0; x < resolution.x; ++x)
            {
                for (int y = 0; y < resolution.y; ++y)
                {
                    for (int z = 0; z < resolution.z; ++z)
                    {
                        float3 uv = (float3(x, y, z) + 0.5f) / resolution;
                        currentPos = lerp(left, right, uv);
                        NativeList<float4x4> view, proj;
                        CalculateCubemapMatrix(currentPos, 50, 0.01f, out view, out proj);
                        RenderPipeline.AddRenderingMissionInEditor(view, proj, bakeCamera, cameraTarget, buffer);
                        RenderPipeline.ExecuteBufferAtFrameEnding(bakeAction);
                        yield return null;
                    }
                }
            }
            yield return null;
            yield return null;
            DisposeBake();
        }
        private void BakeOcclusionData(CommandBuffer buffer)
        {
            ComputeShader shader = resources.shaders.occlusionProbeCalculate;
            buffer.SetComputeTextureParam(shader, 0, "_DepthCubemap", cameraTarget);
            buffer.SetComputeBufferParam(shader, 0, "_OcclusionResult", occlusionBuffer);
            buffer.SetComputeBufferParam(shader, 1, "_OcclusionResult", occlusionBuffer);
            buffer.SetComputeBufferParam(shader, 1, "_FinalBuffer", finalBuffer);
            buffer.SetComputeVectorParam(shader, "_VoxelPosition", float4(currentPos, 1));
            buffer.SetComputeFloatParam(shader, "_Radius", radius);
            buffer.DispatchCompute(shader, 0, 1024, 1, 1);
            buffer.DispatchCompute(shader, 1, 1, 1, 1);
            buffer.RequestAsyncReadback(finalBuffer, readBackAction);
        }

        private void ReadBack(AsyncGPUReadbackRequest request)
        {
            NativeArray<float3x3> values = request.GetData<float3x3>();
            Debug.Log(values.Length);
            finalData.AddRange(values.Ptr(), values.Length);
            ProcessJob jb = new ProcessJob
            {
                finalData = finalData,
                count = values.Length
            };
            lastHandle = jb.Schedule(lastHandle);
            JobHandle.ScheduleBatchedJobs();
        }
        private struct ProcessJob : IJob
        {
            public NativeList<float3x3> finalData;
            public int count;
            public void Execute()
            {
                int start = finalData.Length - count;
                finalData[start] /= count;
                for(int i = start; i < finalData.Length; ++i)
                {
                    finalData[start] += finalData[i] / count;
                }
                finalData.RemoveLast(count - 1);
            }
        }

        private void DisposeBake()
        {
            Debug.Log(finalData[0]);
            finalData.Dispose();
            occlusionBuffer.Dispose();
            finalBuffer.Dispose();
            DestroyImmediate(bakeCamera.gameObject);
            DestroyImmediate(cameraTarget);
            buffer.Dispose();
            bakeCamera = null;
        }
#endif
#endregion
    }
}
