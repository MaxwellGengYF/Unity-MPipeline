using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Collections.LowLevel.Unsafe;

namespace MPipeline
{

    public unsafe class TerrainDrawStreaming
    {
        public static TerrainDrawStreaming current;
        public const int removeKernel = 0;
        public const int frustumCullKernel = 1;
        public const int clearCullKernel = 2;
        public ComputeBuffer verticesBuffer { get; private set; }
        public ComputeBuffer clusterBuffer { get; private set; }
        public ComputeBuffer resultBuffer { get; private set; }
        public ComputeBuffer instanceCountBuffer { get; private set; }
        public ComputeBuffer removebuffer { get; private set; }
        public ComputeBuffer heightMapBuffer { get; private set; }
        public ComputeBuffer triangleBuffer { get; private set; }
        private NativeList<ulong> referenceBuffer;
        private NativeList<int> notUsedHeightmapIndices;
        private ComputeShader transformShader;
        public int meshSize { get; private set; }
        public int vertSize { get; private set; }
        public int heightMapSize { get; private set; }
        public TerrainDrawStreaming(int maximumLength, int meshSize, ComputeShader transformShader)
        {
            current = this;
            if (meshSize % 2 != 0)
            {
                Debug.LogError("Terrain panel's size should be even number!");
                meshSize++;
            }
            this.meshSize = meshSize;
            //Initialize Mesh and triangles
            int vertexCount = meshSize + 1;
            vertSize = vertexCount;
            heightMapSize = vertSize * vertSize;
            NativeArray<float2> terrainVertexArray = new NativeArray<float2>(vertexCount * vertexCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            float2* arrPtr = terrainVertexArray.Ptr();
            for (int x = 0; x < vertexCount; ++x)
            {
                for (int y = 0; y < vertexCount; ++y)
                {
                    arrPtr[y * vertexCount + x] = new float2(x, y) / meshSize - new float2(0.5f, 0.5f);
                }
            }
            verticesBuffer = new ComputeBuffer(terrainVertexArray.Length, sizeof(float2));
            verticesBuffer.SetData(terrainVertexArray);
            heightMapBuffer = new ComputeBuffer(maximumLength * (vertexCount * vertexCount), sizeof(float));
            NativeArray<int> triangles = new NativeArray<int>(6 * meshSize * meshSize, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

            int* trianglePtr = triangles.Ptr();
            for (int x = 0, count = 0; x < meshSize; ++x)
            {
                for (int y = 0; y < meshSize; ++y)
                {
                    int4 indices = new int4(vertexCount * y + x, vertexCount * (y + 1) + x, vertexCount * y + (x + 1), vertexCount * (y + 1) + (x + 1));
                    trianglePtr[count] = indices.x;
                    trianglePtr[count + 1] = indices.y;
                    trianglePtr[count + 2] = indices.z;
                    trianglePtr[count + 3] = indices.y;
                    trianglePtr[count + 4] = indices.w;
                    trianglePtr[count + 5] = indices.z;
                    count += 6;
                }
            }
            triangleBuffer = new ComputeBuffer(triangles.Length, sizeof(int));
            triangleBuffer.SetData(triangles);
            triangles.Dispose();
            terrainVertexArray.Dispose();
            removebuffer = new ComputeBuffer(100, sizeof(int2));
            //Initialize indirect
            clusterBuffer = new ComputeBuffer(maximumLength, sizeof(TerrainPanel));
            referenceBuffer = new NativeList<ulong>(maximumLength, Allocator.Persistent);
            this.transformShader = transformShader;
            resultBuffer = new ComputeBuffer(maximumLength, sizeof(int));
            instanceCountBuffer = new ComputeBuffer(5, sizeof(int), ComputeBufferType.IndirectArguments);
            NativeArray<int> indirect = new NativeArray<int>(5, Allocator.Temp, NativeArrayOptions.ClearMemory);
            indirect[0] = triangleBuffer.count;
            instanceCountBuffer.SetData(indirect);
            indirect.Dispose();
            notUsedHeightmapIndices = new NativeList<int>(maximumLength, Allocator.Persistent);
            for (int i = 0; i < maximumLength; ++i)
            {
                notUsedHeightmapIndices.Add(i);
            }
        }
        #region LOAD_AREA
        public void AddQuadTrees(NativeList<ulong> addList, NativeArray<float> heightMap)
        {
            TerrainQuadTree.QuadTreeNode** tree = (TerrainQuadTree.QuadTreeNode**)addList.unsafePtr;
            int length = addList.Length;
            NativeArray<TerrainPanel> panel = new NativeArray<TerrainPanel>(length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            TerrainPanel* panelPtr = panel.Ptr();
            for (int i = 0; i < length; ++i)
            {
                tree[i]->listPosition = referenceBuffer.Length + i;
                int heightIndex = notUsedHeightmapIndices[notUsedHeightmapIndices.Length - 1];
                tree[i]->panel.heightMapIndex = heightIndex;
                notUsedHeightmapIndices.RemoveLast();
                heightMapBuffer.SetData(heightMap, 0, heightIndex * (meshSize + 1) * (meshSize + 1), heightMap.Length);
                panelPtr[i] = tree[i]->panel;
            }
            clusterBuffer.SetData(panel, 0, referenceBuffer.Length, length);
            panel.Dispose();
            referenceBuffer.AddRange(addList);
        }

        public void RemoveQuadTrees(NativeList<ulong> removeList)
        {
            void ErasePoint(TerrainQuadTree.QuadTreeNode* node)
            {
                node->listPosition = -1;
                notUsedHeightmapIndices.Add(node->panel.heightMapIndex);
            }
            int length = removeList.Length;
            TerrainQuadTree.QuadTreeNode** tree = (TerrainQuadTree.QuadTreeNode**)removeList.unsafePtr;
            int targetLength = referenceBuffer.Length - length;
            int len = 0;
            if (targetLength <= 0)
            {
                for (int i = 0; i < length; ++i)
                {
                    ErasePoint(tree[i]);
                }
                referenceBuffer.Clear();
                return;
            }
            for (int i = 0; i < length; ++i)
            {
                TerrainQuadTree.QuadTreeNode* currentNode = tree[i];
                if (currentNode->listPosition >= targetLength)
                {
                    referenceBuffer[currentNode->listPosition] = 0;
                    ErasePoint(currentNode);
                }
            }
            NativeArray<int2> transformList = new NativeArray<int2>(length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            int2* transformPtr = transformList.Ptr();
            len = 0;
            int currentIndex = referenceBuffer.Length - 1;
            for (int i = 0; i < length; ++i)
            {
                TerrainQuadTree.QuadTreeNode* treeNode = tree[i];
                if (treeNode->listPosition < 0) continue;
                while (referenceBuffer[currentIndex] == 0)
                {
                    currentIndex--;
                    if (currentIndex < 0)
                        goto FINALIZE;
                }
                TerrainQuadTree.QuadTreeNode* lastNode = (TerrainQuadTree.QuadTreeNode*)referenceBuffer[currentIndex];
                currentIndex--;
                transformPtr[len] = new int2(treeNode->listPosition, lastNode->listPosition);
                len++;
                lastNode->listPosition = treeNode->listPosition;
                referenceBuffer[lastNode->listPosition] = (ulong)lastNode;
                ErasePoint(treeNode);
            }
            FINALIZE:
            referenceBuffer.RemoveLast(length);
            if (len <= 0) return;
            if (len > removebuffer.count)
            {
                removebuffer.Dispose();
                removebuffer = new ComputeBuffer(len, sizeof(int2));
            }
            removebuffer.SetData(transformList, 0, 0, len);
            transformShader.SetBuffer(0, ShaderIDs._IndexBuffer, removebuffer);
            transformShader.SetBuffer(0, ShaderIDs.clusterBuffer, clusterBuffer);
            ComputeShaderUtility.Dispatch(transformShader, 0, len, 64);
            transformList.Dispose();
        }

        #endregion
        public void Dispose()
        {
            verticesBuffer.Dispose();
            instanceCountBuffer.Dispose();
            resultBuffer.Dispose();
            clusterBuffer.Dispose();
            removebuffer.Dispose();
            heightMapBuffer.Dispose();
            referenceBuffer.Dispose();
            triangleBuffer.Dispose();
            notUsedHeightmapIndices.Dispose();
            current = null;
        }

        public void DrawTerrain(ref RenderClusterOptions ops, Material terrainMat, RenderTargetIdentifier colorBuffer, RenderTargetIdentifier depthBuffer)
        {
            if (referenceBuffer.Length <= 0) return;
            ComputeShader sh = ops.terrainCompute;
            CommandBuffer bf = ops.command;
            bf.SetComputeBufferParam(sh, frustumCullKernel, ShaderIDs.clusterBuffer, clusterBuffer);
            bf.SetComputeBufferParam(sh, frustumCullKernel, ShaderIDs.resultBuffer, resultBuffer);
            bf.SetComputeBufferParam(sh, frustumCullKernel, ShaderIDs.instanceCountBuffer, instanceCountBuffer);
            bf.SetComputeBufferParam(sh, clearCullKernel, ShaderIDs.instanceCountBuffer, instanceCountBuffer);
            bf.SetComputeVectorArrayParam(sh, ShaderIDs.planes, ops.frustumPlanes);
            bf.SetGlobalInt(ShaderIDs._MeshSize, meshSize);
            bf.DispatchCompute(sh, clearCullKernel, 1, 1, 1);
            ComputeShaderUtility.Dispatch(sh, bf, frustumCullKernel, referenceBuffer.Length, 64);
            bf.SetGlobalBuffer(ShaderIDs.heightMapBuffer, heightMapBuffer);
            bf.SetGlobalBuffer(ShaderIDs.triangleBuffer, triangleBuffer);
            bf.SetGlobalBuffer(ShaderIDs.verticesBuffer, verticesBuffer);
            bf.SetGlobalBuffer(ShaderIDs.clusterBuffer, clusterBuffer);
            bf.SetGlobalBuffer(ShaderIDs.resultBuffer, resultBuffer);
            bf.SetRenderTarget(colorBuffer, depthBuffer);
            bf.DrawProceduralIndirect(Matrix4x4.identity, terrainMat, 0, MeshTopology.Triangles, instanceCountBuffer);
        }
    }

    public struct TerrainPanel
    {
        public float3 extent;
        public float3 position;
        public int4 textureIndex;
        public int heightMapIndex;
        public uint edgeFlag;
    }

    public unsafe struct TerrainQuadTree
    {
        public struct QuadTreeNode
        {
            /*  public QuadTreeNode* leftUp;
              public QuadTreeNode* leftDown;
              public QuadTreeNode* rightUp;
              public QuadTreeNode* rightDown;*/
            public TerrainPanel panel;
            public int listPosition;
        }
        public NativeList<QuadTreeNode> originTrees;
    }
}
