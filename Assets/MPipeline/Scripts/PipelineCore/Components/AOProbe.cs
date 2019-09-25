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
        public int3 resolution = int3(1,1,1);
        public static NativeList<UIntPtr> allProbe { get; private set; }
        private int index;
        public RenderTexture src0 { get; private set; }
        public RenderTexture src1 { get; private set; }
        public RenderTexture src2 { get; private set; }
        public PipelineResources resources;
        public float radius;
        private ComputeBuffer shBuffer;
        [SerializeField]
        private string probeName;
        private static MStringBuilder msb;
        private static int[] resolutionArray = new int[3];
        private static byte[] reuseByteArray = null;
        
        private static byte[] GetByteArray(long targetLength)
        {
            if(reuseByteArray == null || reuseByteArray.Length < targetLength)
            {
                reuseByteArray = new byte[targetLength];
            }
            return reuseByteArray;
        }
        private void Awake()
        {
            if (!msb.isCreated)
                msb = new MStringBuilder(30);
            msb.Clear();
            msb.Add("Assets/BinaryData/Irradiance/");
            msb.Add(probeName);
            msb.Add(".mpipe");
            if (!System.IO.File.Exists(msb.str))
            {
                Debug.LogError("Probe: " + probeName + "read Error! ");
                Destroy(gameObject);
                return;
            }
            else
            {
                using (System.IO.FileStream fs = new System.IO.FileStream(msb.str, System.IO.FileMode.Open, System.IO.FileAccess.Read))
                {
                    byte[] arr = GetByteArray(fs.Length);
                    fs.Read(arr, 0, (int)fs.Length);
                    int3* res = (int3*)arr.Ptr();
                    resolution = *res;
                    if(resolution.x * resolution.y * resolution.z * sizeof(float3x3) != fs.Length - sizeof(int3))
                    {
                        Debug.LogError("Data size incorrect!");
                        Destroy(gameObject);
                        return;
                    }
                    NativeArray<float3x3> allDatas = new NativeArray<float3x3>(resolution.x * resolution.y * resolution.z, Allocator.Temp);
                    UnsafeUtility.MemCpy(allDatas.GetUnsafePtr(), res + 1, sizeof(float3x3) * allDatas.Length);
                    RenderTextureDescriptor desc = new RenderTextureDescriptor
                    {
                        colorFormat = RenderTextureFormat.ARGBHalf,
                        dimension = TextureDimension.Tex3D,
                        enableRandomWrite = true,
                        width = resolution.x,
                        height = resolution.y,
                        volumeDepth = resolution.z,
                        msaaSamples = 1
                    };
                    src0 = new RenderTexture(desc);
                    src1 = new RenderTexture(desc);
                    desc.colorFormat = RenderTextureFormat.RHalf;
                    src2 = new RenderTexture(desc);
                    shBuffer = new ComputeBuffer(allDatas.Length, sizeof(float3x3));
                    shBuffer.SetData(allDatas);
                    ComputeShader shader = resources.shaders.occlusionProbeCalculate;
                    int3* arrPtr = (int3*)resolutionArray.Ptr();
                    *arrPtr = resolution;
                    shader.SetBuffer(2, "_SHBuffer", shBuffer);
                    shader.SetTexture(2, "_Src0", src0);
                    shader.SetTexture(2, "_Src1", src1);
                    shader.SetTexture(2, "_Src2", src2);
                    shader.SetInts("_Resolution", resolutionArray);
                    shader.Dispatch(2, Mathf.CeilToInt(resolution.x / 4f), Mathf.CeilToInt(resolution.y / 4f), Mathf.CeilToInt(resolution.z / 4f));
                    shBuffer.Dispose();
                    allDatas.Dispose();
                }
            }
        }
        private void OnDestroy()
        {
            Destroy(src0);
            Destroy(src1);
            Destroy(src2);
        }
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
        private Action<AsyncGPUReadbackRequest> readBackAction;
        private CommandBuffer buffer;
        private ComputeBuffer occlusionBuffer;
        private ComputeBuffer finalBuffer;
        private NativeList<float3x3> finalData;
        private JobHandle lastHandle;
        private int count;
        [EasyButtons.Button]
        private void BakeTest()
        {
            InitBake();
            count = 0;
            StartCoroutine(BakeOcclusion());
        }
        private void InitBake()
        {
            GameObject obj = new GameObject("BakeCam", typeof(Camera), typeof(PipelineCamera));
            bakeCamera = obj.GetComponent<PipelineCamera>();
            bakeCamera.cam = obj.GetComponent<Camera>();
            bakeCamera.cam.enabled = false;
            bakeCamera.renderingPath = PipelineResources.CameraRenderingPath.Unlit;
            bakeCamera.inverseRender = true;
            readBackAction = ReadBack;
            finalData = new NativeList<float3x3>(resolution.x * resolution.y * resolution.z + 1024, Allocator.Persistent);
            cameraTarget = new RenderTexture(new RenderTextureDescriptor
            {
                msaaSamples = 1,
                width = 1024,
                height = 1024,
                depthBufferBits = 32,
                colorFormat = RenderTextureFormat.RFloat,
                dimension = TextureDimension.Cube,
                volumeDepth = 1
            });
            buffer = new CommandBuffer();
            occlusionBuffer = new ComputeBuffer(1024 * 1024, sizeof(float3x3));
            finalBuffer = new ComputeBuffer(1024, sizeof(float3x3));
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
            viewMatrices[5] = cam.worldToCameraMatrix;
            //Back
            cam.right = float3(-1, 0, 0);
            cam.up = float3(0, 1, 0);
            cam.forward = float3(0, 0, -1);
            cam.UpdateTRSMatrix();

            viewMatrices[4] = cam.worldToCameraMatrix;
            //Up
            cam.right = float3(-1, 0, 0);
            cam.up = float3(0, 0, 1);
            cam.forward = float3(0, 1, 0);
            cam.UpdateTRSMatrix();
            viewMatrices[3] = cam.worldToCameraMatrix;
            //Down
            cam.right = float3(-1, 0, 0);
            cam.up = float3(0, 0, -1);
            cam.forward = float3(0, -1, 0);
            cam.UpdateTRSMatrix();
            viewMatrices[2] = cam.worldToCameraMatrix;
            //Right
            cam.up = float3(0, 1, 0);
            cam.right = float3(0, 0, -1);
            cam.forward = float3(1, 0, 0);
            cam.UpdateTRSMatrix();
            viewMatrices[1] = cam.worldToCameraMatrix;
            //Left
            cam.up = float3(0, 1, 0);
            cam.right = float3(0, 0, 1);
            cam.forward = float3(-1, 0, 0);
            cam.UpdateTRSMatrix();
            viewMatrices[0] = cam.worldToCameraMatrix;
        }
        private float3 currentPos;
        private IEnumerator BakeOcclusion()
        {
            float3 left = transform.position - transform.localScale * 0.5f;
            float3 right = transform.position + transform.localScale * 0.5f;
            for (int x = 0; x < resolution.x; ++x)
            {
                for (int y = 0; y < resolution.y; ++y)
                {
                    for (int z = 0; z < resolution.z; ++z)
                    {
                        float3 uv = (float3(x, y, z) + 0.5f) / float3(resolution.x, resolution.y, resolution.z);
                        currentPos = lerp(left, right, uv);
                        NativeList<float4x4> view, proj;
                        CalculateCubemapMatrix(currentPos, radius, 0.01f, out view, out proj);
                        RenderPipeline.AddRenderingMissionInEditor(view, proj, bakeCamera, cameraTarget, buffer);
                        BakeOcclusionData(RenderPipeline.AfterFrameBuffer);
                        yield return null;
                    }
                }
            }
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
            finalData.AddRange(values.Ptr(), values.Length);
            int start = finalData.Length - values.Length;
            finalData[start] /= values.Length;
            for (int i = start; i < finalData.Length; ++i)
            {
                finalData[start] += finalData[i] / values.Length;
            }
            finalData.RemoveLast(values.Length - 1);
            count++;
            if (count >= resolution.x * resolution.y * resolution.z)
               DisposeBake();
        }
        private void OutputData()
        {
            int len = resolution.x * resolution.y * resolution.z * sizeof(float3x3);
            byte[] allBytes = new byte[len + sizeof(int3)];
            int3* startPtr = (int3*)allBytes.Ptr();
            *startPtr = resolution;
            ++startPtr;
            UnsafeUtility.MemCpy(startPtr, finalData.unsafePtr, len);
            System.IO.File.WriteAllBytes("Assets/BinaryData/Irradiance/" + probeName + ".mpipe", allBytes);
        }

        private void DisposeBake()
        {
            OutputData();
            finalData.Dispose();
            occlusionBuffer.Dispose();
            finalBuffer.Dispose();
            buffer.Dispose();
            DestroyImmediate(bakeCamera.gameObject);
            DestroyImmediate(cameraTarget);
            bakeCamera = null;
            Debug.Log("Baking Finished");
        }
#endif
        #endregion
    }
}
