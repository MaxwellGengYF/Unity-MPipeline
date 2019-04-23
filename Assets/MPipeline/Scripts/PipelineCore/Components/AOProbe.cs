using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using System;
using Unity.Collections.LowLevel.Unsafe;
using static Unity.Mathematics.math;
using UnityEngine.Rendering;
namespace MPipeline
{
    public unsafe sealed class AOProbe : MonoBehaviour
    {
        public int3 resolution;
        public static NativeList<UIntPtr> allProbe { get; private set; }
        private int index;
        public RenderTexture src0 { get; private set; }
        public RenderTexture src1 { get; private set; }
        public RenderTexture src2 { get; private set; }
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
        private PipelineCamera bakeCamera;
        private RenderTexture cameraTarget;
        private Action<CommandBuffer> bakeAction;
        private CommandBuffer buffer;
        private void InitBake()
        {
            GameObject obj = new GameObject("BakeCam", typeof(Camera), typeof(PipelineCamera));
            bakeCamera = obj.GetComponent<PipelineCamera>();
            bakeCamera.cam = obj.GetComponent<Camera>();
            bakeCamera.renderingPath = PipelineResources.CameraRenderingPath.Unlit;
            bakeCamera.inverseRender = true;
            cameraTarget = new RenderTexture(new RenderTextureDescriptor
            {
                msaaSamples = 1,
                width = 256, 
                height = 256,
                depthBufferBits = 16,
                colorFormat = RenderTextureFormat.RHalf,
                dimension = TextureDimension.Tex2DArray,
                volumeDepth = 6
            });
            buffer = new CommandBuffer();
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
        private IEnumerator BakeOcclusion()
        {
            float3 left = transform.position - transform.localScale * 0.5f;
            float3 right = transform.position + transform.localScale * 0.5f;
            for(int x = 0; x < resolution.x;++x)
            {
                for(int y = 0; y < resolution.y; ++y)
                {
                    for(int z = 0; z < resolution.z; ++z)
                    {
                        float3 uv = (float3(x, y, z) + 0.5f) / resolution;
                        float3 currentPos = lerp(left, right, uv);
                        NativeList<float4x4> view, proj;
                        CalculateCubemapMatrix(currentPos, 20, 0.01f, out view, out proj);
                        RenderPipeline.AddRenderingMissionInEditor(view, proj, bakeCamera, cameraTarget, buffer);
                        
                        //TODO
                        yield return null;
                    }
                }
            }
        }
        
        private void DisposeBake()
        {
            DestroyImmediate(bakeCamera.gameObject);
            DestroyImmediate(cameraTarget);
            buffer.Dispose();
            bakeCamera = null;
        }
        #endregion
    }
}
