using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using static Unity.Mathematics.math;
using Unity.Jobs;
using System.Threading;
using UnityEngine.Rendering;
namespace MPipeline
{
    public unsafe sealed class MTerrain : JobProcessEvent
    {
        public static MTerrain current { get; private set; }
        public struct TerrainChunkBuffer
        {
            public float2 worldPos;
            public float2 minMaxHeight;
            public float2 scale;
            public uint2 uvStartIndex;
        }
        public int chunkCount = 8;
        [Range(0f, 1000f)]
        public double chunkSize = 10;
        public float[] lodDistances = new float[]
        {
            150,
            100,
            50
        };
        public int planarResolution = 10;
        public Material drawTerrainMaterial;
        public double2 chunkOffset = 0;
        public float lodDeferredOffset = 2;
        public Transform cam;
        private ComputeBuffer culledResultsBuffer;
        private ComputeBuffer loadedBuffer;
        private ComputeBuffer dispatchDrawBuffer;
        private ComputeShader shader;
        private NativeList<TerrainChunkBuffer> loadedBufferList;
        private static Vector4[] planes = new Vector4[6];
        private Native2DArray<TerrainQuadTree> allTrees;
        TerrainQuadTreeSettings setting;
        private JobHandle calculateHandle;
                /*    arr[getLength(x, y)] = float2(x, y) / planarResolution;
                    arr[getLength(x, y) + 1] = float2(x, y + 1) / planarResolution;
                    arr[getLength(x, y) + 2] = float2(x + 1, y) / planarResolution;
                    arr[getLength(x, y) + 3] = float2(x, y + 1) / planarResolution;
                    arr[getLength(x, y) + 4] = float2(x + 1, y + 1) / planarResolution;
                    arr[getLength(x, y) + 5] = float2(x + 1, y) / planarResolution;*/


        public override void PrepareJob()
        {
            loadedBufferList.Clear();
            int len = allTrees.Length.x * allTrees.Length.y;
            calculateHandle = new CalculateQuadTree
            {
                allTrees = allTrees.ptr,
                cameraXZPos = double2(cam.position.x, cam.position.z),
                loadedBuffer = loadedBufferList,
                len = len
            }.Schedule();
        }

        public override void FinishJob()
        {
            calculateHandle.Complete();
            UpdateBuffer();
        }
        protected override void OnEnableFunc()
        {
            if (current && current != this)
            {
                enabled = false;
                Debug.LogError("Only One Terrain allowed!");
                return;
            }
            current = this;
            int indexMapSize = 1;
            for (int i = 1; i < lodDistances.Length; ++i)
            {
                indexMapSize *= 2;
            }
            indexMapSize *= chunkCount;

            dispatchDrawBuffer = new ComputeBuffer(5, sizeof(int), ComputeBufferType.IndirectArguments);
            const int INIT_LENGTH = 500;
            culledResultsBuffer = new ComputeBuffer(INIT_LENGTH, sizeof(int));
            loadedBuffer = new ComputeBuffer(INIT_LENGTH, sizeof(TerrainChunkBuffer));
            loadedBufferList = new NativeList<TerrainChunkBuffer>(INIT_LENGTH, Allocator.Persistent);
            shader = Resources.Load<ComputeShader>("TerrainCompute");
            NativeArray<uint> dispatchDraw = new NativeArray<uint>(5, Allocator.Temp, NativeArrayOptions.ClearMemory);
            dispatchDraw[0] = 6;
            dispatchDrawBuffer.SetData(dispatchDraw);
            allTrees = new Native2DArray<TerrainQuadTree>(chunkCount, Allocator.Persistent, true);
            setting = new TerrainQuadTreeSettings
            {
                allLodLevles = new NativeList_Float(lodDistances.Length + 1, Allocator.Persistent),
                largestChunkSize = chunkSize,
                screenOffset = chunkOffset,
                lodDeferredOffset = lodDeferredOffset
            };
            for (int i = 0; i < lodDistances.Length; ++i)
            {
                setting.allLodLevles.Add(min(lodDistances[max(0, i - 1)], lodDistances[i]));
            }
            setting.allLodLevles[lodDistances.Length] = 0;
            int2 len = allTrees.Length;
            for (int x = 0; x < len.x; ++x)
                for (int y = 0; y < len.y; ++y)
                {
                    allTrees[int2(x, y)] = new TerrainQuadTree(-1, setting.Ptr(), TerrainQuadTree.LocalPos.LeftDown, 0, int2(x, y));
                }
        }
        void UpdateBuffer()
        {
            if (!loadedBufferList.isCreated) return;
            if (loadedBufferList.Length > loadedBuffer.count)
            {
                loadedBuffer.Dispose();
                culledResultsBuffer.Dispose();
                loadedBuffer = new ComputeBuffer(loadedBufferList.Capacity, sizeof(TerrainChunkBuffer));
                culledResultsBuffer = new ComputeBuffer(loadedBufferList.Capacity, sizeof(int));
            }
            loadedBuffer.SetDataPtr(loadedBufferList.unsafePtr, loadedBufferList.Length);
        }

        public void DrawTerrain(CommandBuffer buffer, int pass, Vector4[] planes)
        {
            if (loadedBufferList.Length <= 0) return;
          
            buffer.SetComputeBufferParam(shader, 1, ShaderIDs._DispatchBuffer, dispatchDrawBuffer);
            buffer.SetComputeBufferParam(shader, 0, ShaderIDs._DispatchBuffer, dispatchDrawBuffer);
            buffer.SetComputeBufferParam(shader, 0, ShaderIDs._CullResultBuffer, culledResultsBuffer);
            buffer.SetComputeBufferParam(shader, 0, ShaderIDs._TerrainChunks, loadedBuffer);
            buffer.SetGlobalBuffer(ShaderIDs._TerrainChunks, loadedBuffer);
            buffer.SetGlobalBuffer(ShaderIDs._CullResultBuffer, culledResultsBuffer);
            buffer.SetComputeVectorArrayParam(shader, ShaderIDs.planes, planes);
            buffer.DispatchCompute(shader, 1, 1, 1, 1);
            ComputeShaderUtility.Dispatch(shader, buffer, 0, loadedBufferList.Length);
            buffer.DrawProceduralIndirect(Matrix4x4.identity, drawTerrainMaterial, pass, MeshTopology.Triangles, dispatchDrawBuffer);
        }

        public void DrawTerrain(CommandBuffer buffer, int pass, float4* planePtr)
        {
            UnsafeUtility.MemCpy(planes.Ptr(), planePtr, sizeof(float4) * 6);
            DrawTerrain(buffer, pass, planes);
        }

        protected override void OnDisableFunc()
        {
            if (current != this) return;
            current = null;
            if (culledResultsBuffer != null) culledResultsBuffer.Dispose();
            if (loadedBuffer != null) loadedBuffer.Dispose();
            if (dispatchDrawBuffer != null) dispatchDrawBuffer.Dispose();
            if (loadedBufferList.isCreated) loadedBufferList.Dispose();
            int2 len = allTrees.Length;
            for (int x = 0; x < len.x; ++x)
                for (int y = 0; y < len.y; ++y)
                {
                    allTrees[int2(x, y)].Dispose();
                }
            allTrees.Dispose();
        }
        [Unity.Burst.BurstCompile]
        private struct CalculateQuadTree : IJob
        {
            [NativeDisableUnsafePtrRestriction]
            public TerrainQuadTree* allTrees;
            public double2 cameraXZPos;
            public NativeList<TerrainChunkBuffer> loadedBuffer;
            public int len;
            public void Execute()
            {
                for (int index = 0; index < len; ++index)
                {
                    ref TerrainQuadTree tree = ref allTrees[index];
                    tree.CheckUpdate(cameraXZPos);
                    tree.PushDrawRequest(loadedBuffer);
                }
            }
        }
    }
}