#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Unity.Mathematics.math;
using Unity.Mathematics;
using UnityEngine.Rendering;
using System;
using UnityEngine.Experimental.Rendering;
using Unity.Collections;
namespace MPipeline
{
    [RequireComponent(typeof(FreeCamera))]
    [RequireComponent(typeof(Camera))]
    public unsafe sealed class MTerrainEditorTool : MonoBehaviour
    {
        public ComputeShader terrainEditShader;
        public GeometryEvent geometryEvt;
        public float paintRange = 5;
        public int value = 20;
        private NativeQueue<bool> commandQueue;
        private float3 lastFrameWorldPos;
        FreeCamera freeCamera;
        Camera cam;
        ComputeBuffer distanceBuffer;
        Action<AsyncGPUReadbackRequest> complateFunc;
        float2 uv;
        void OnEnable()
        {
            commandQueue = new NativeQueue<bool>(100, Allocator.Persistent);
            cam = GetComponent<Camera>();
            freeCamera = GetComponent<FreeCamera>();
            distanceBuffer = new ComputeBuffer(1, sizeof(float));
            complateFunc = OnFinishRead;

        }

        void PaintMask(MTerrain terrain, TerrainQuadTree* treeNodePtr, int texIndex, int disp)
        {
            terrainEditShader.SetTexture(1, ShaderIDs._DestTex, terrain.maskVT.GetTexture(1));
            terrainEditShader.SetFloat("_TargetValue", saturate((float)((value + 0.1) / (terrain.terrainData.allMaterials.Length - 1))));
            terrainEditShader.Dispatch(1, disp, disp, 1);
        }

        void OnFinishRead(AsyncGPUReadbackRequest request)
        {
            bool useConnection = false;
            commandQueue.TryDequeue(out useConnection);
            float depth = request.GetData<float>().Element(0);
            float4x4 invvp = (GL.GetGPUProjectionMatrix(cam.projectionMatrix, false) * cam.worldToCameraMatrix).inverse;
            float4 worldPos = mul(invvp, float4(uv * 2 - 1, depth, 1));
            worldPos.xyz /= worldPos.w;
            MTerrain terrain = MTerrain.current;
            if (!terrain)
            {
                return;
            }
            NativeList<ulong> allMaskTree = new NativeList<ulong>(5, Allocator.Temp);
            value = Mathf.Clamp(value, 0, terrain.terrainData.allMaterials.Length - 1);
            terrain.treeRoot->GetMaterialMaskRoot(worldPos.xz, paintRange, ref allMaskTree);
            terrainEditShader.SetInt(ShaderIDs._Count, MTerrain.MASK_RESOLUTION);
            if (!useConnection)
            {
                //TODO
                terrainEditShader.SetVector("_Circle0", float4(worldPos.xz, paintRange, 1));
                terrainEditShader.SetVector("_Circle1", float4(worldPos.xz, paintRange, 1));
                terrainEditShader.SetMatrix("_QuadMatrix", float4x4(0));
            }
            else
            {
                terrain.treeRoot->GetMaterialMaskRoot(lastFrameWorldPos.xz, paintRange, ref allMaskTree);
                float2 moveDir = lastFrameWorldPos.xz - worldPos.xz;
                float len = length(moveDir);
                moveDir /= len;
                float dotMove = dot(moveDir, moveDir);
                float2 verticleDir = float2(-moveDir.y / dotMove, moveDir.x / dotMove);
                float3x3 localToWorld = float3x3(float3(moveDir * len, 0), float3(verticleDir * paintRange * 2, 0), float3((lastFrameWorldPos.xz + worldPos.xz) * 0.5f, 1));
                float3x3 worldToLocal = inverse(localToWorld);
                terrainEditShader.SetVector("_Circle0", float4(worldPos.xz, paintRange, 1));
                terrainEditShader.SetVector("_Circle1", float4(lastFrameWorldPos.xz, paintRange, 1));
                terrainEditShader.SetMatrix("_QuadMatrix", float4x4(float4(worldToLocal.c0, 0), float4(worldToLocal.c1, 0), float4(worldToLocal.c2, 0), 0));
            }
            const int disp = MTerrain.MASK_RESOLUTION / 8;
            foreach (var i in allMaskTree)
            {
                var treeNodePtr = (TerrainQuadTree*)i;
                if (treeNodePtr == null) continue;
                int2 maskPos = treeNodePtr->rootPos + (int2)treeNodePtr->maskScaleOffset.yz;
                int texIndex = terrain.maskVT.GetChunkIndex(maskPos);
                if (texIndex < 0) continue;
                terrainEditShader.SetVector("_SrcDestCorner", (float4)treeNodePtr->BoundedWorldPos);
                terrainEditShader.SetInt(ShaderIDs._OffsetIndex, texIndex);
                PaintMask(terrain, treeNodePtr, texIndex, disp);
            }
            if (!useConnection)
                terrain.treeRoot->UpdateChunks(double3(worldPos.xz, paintRange));
            else
                terrain.treeRoot->UpdateChunks(double3(0.5f * (worldPos.xz + lastFrameWorldPos.xz), 0.5f * distance(worldPos.xz, lastFrameWorldPos.xz) + paintRange));
            lastFrameWorldPos = worldPos.xyz;
        }
        bool lastFrameWorking = false;
        void Update()
        {
            if (MTerrain.current == null)
                return;
            freeCamera.enabled = Input.GetMouseButton(1);

            if (Input.GetMouseButton(0))
            {
                commandQueue.Add(lastFrameWorking);
                CommandBuffer bf = geometryEvt.afterGeometryBuffer;
                bf.SetComputeBufferParam(terrainEditShader, 0, "_DistanceBuffer", distanceBuffer);
                bf.SetComputeTextureParam(terrainEditShader, 0, ShaderIDs._CameraDepthTexture, new RenderTargetIdentifier(ShaderIDs._CameraDepthTexture));
                uv = ((float3)Input.mousePosition).xy / float2(Screen.width, Screen.height);
                bf.SetComputeVectorParam(terrainEditShader, "_UV", float4(uv, 1, 1));
                bf.DispatchCompute(terrainEditShader, 0, 1, 1, 1);
                bf.RequestAsyncReadback(distanceBuffer, complateFunc);
                lastFrameWorking = true;
            }
            else
                lastFrameWorking = false;
            if(Input.GetKeyDown(KeyCode.P))
            {
                MTerrain.current.SaveMask();
            }
        }
        private void OnDisable()
        {
            commandQueue.Dispose();
            distanceBuffer.Dispose();
        }
    }
}
#endif