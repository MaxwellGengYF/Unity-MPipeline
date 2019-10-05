using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Jobs;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Collections;
using static Unity.Mathematics.math;
namespace MPipeline
{
    public unsafe sealed class ParticleDraw : CustomDrawRequest
    {
#if UNITY_EDITOR
        [EasyButtons.Button]
        void SetTransformToPoints()
        {
            var targetPositions = new List<Transform>(transform.childCount);
            for(int i= 0; i < transform.childCount; ++i)
            {
                targetPositions.Add(transform.GetChild(i));
            }
            instancePositions = new float3x4[targetPositions.Count];
            for (int i = 0; i < targetPositions.Count; ++i)
            {
                Matrix4x4 local = targetPositions[i].localToWorldMatrix;
                instancePositions[i] = new float3x4((Vector3)local.GetColumn(0), (Vector3)local.GetColumn(1), (Vector3)local.GetColumn(2), (Vector3)local.GetColumn(3));
            }
        }
#endif
        public Material drawMat;
        public Mesh instanceMesh;
        public float3x4[] instancePositions;
        public ComputeShader movingShader;
        public Texture noiseTexture;
        public float runSpeed;
        private ComputeBuffer currentPosBuffer;
        private ComputeBuffer lastPosBuffer;
        private ComputeBuffer originPosBuffer;
        private ComputeBuffer verticesBuffer;
        private float offset;
        
        struct Vertex
        {
            public float4 tangent;
            public float3 normal;
            public float3 position;
            public float2 uv;
        }
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.white;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(boundingBoxPosition, boundingBoxExtents * 2);
        }
        protected override void OnEnableFunc()
        {
            localToWorldMatrix = transform.localToWorldMatrix;
        }

        private void Awake()
        {
            int[] tris = instanceMesh.triangles;
            NativeArray<Vertex> allVertices = new NativeArray<Vertex>(tris.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            Vector4[] tangents = instanceMesh.tangents;
            Vector3[] normals = instanceMesh.normals;
            Vector3[] positions = instanceMesh.vertices;
            Vector2[] uvs = instanceMesh.uv;
            for (int j = 0; j < tris.Length; ++j)
            {
                int tri = tris[j];
                Vertex v = new Vertex
                {
                    normal = normals[tri],
                    position = positions[tri],
                    tangent = tangents[tri],
                    uv = uvs[tri]
                };
                allVertices[j] = v;
            }

            verticesBuffer = new ComputeBuffer(allVertices.Length, sizeof(Vertex));
            currentPosBuffer = new ComputeBuffer(instancePositions.Length, sizeof(float3x4));
            lastPosBuffer = new ComputeBuffer(instancePositions.Length, sizeof(float3x4));
            originPosBuffer = new ComputeBuffer(instancePositions.Length, sizeof(float3x4));
            originPosBuffer.SetData(instancePositions);
            verticesBuffer.SetData(allVertices);
            allVertices.Dispose();
        }
        private void OnDestroy()
        {
            verticesBuffer.Dispose();
            currentPosBuffer.Dispose();
            lastPosBuffer.Dispose();
            originPosBuffer.Dispose();
        }
        private void DrawPass(CommandBuffer buffer, int targetPass)
        {
            buffer.SetGlobalBuffer(ShaderIDs._TransformMatrices, currentPosBuffer);
            buffer.SetGlobalBuffer(ShaderIDs.verticesBuffer, verticesBuffer);
            buffer.DrawProcedural(Matrix4x4.identity, drawMat, targetPass, MeshTopology.Triangles, verticesBuffer.count, currentPosBuffer.count);
        }
        public override void DrawGBuffer(CommandBuffer buffer)
        {
            DrawPass(buffer, 0);
        }
        public override void DrawShadow(CommandBuffer buffer)
        {
        }
        protected override void DrawCommand(out bool drawGBuffer, out bool drawShadow, out bool drawTransparent)
        {
            drawGBuffer = true;
            drawShadow = false;
            drawTransparent = false;
        }
        
        public override void FinishJob()
        {
            CommandBuffer buffer = RenderPipeline.BeforeFrameBuffer;
            ComputeBuffer temp = currentPosBuffer;
            currentPosBuffer = lastPosBuffer;
            lastPosBuffer = temp;
            buffer.SetComputeBufferParam(movingShader, 0, ShaderIDs._TransformMatrices, currentPosBuffer);
            buffer.SetComputeBufferParam(movingShader, 0, ShaderIDs._OriginTransformMatrices, originPosBuffer);
            buffer.SetComputeTextureParam(movingShader, 0, ShaderIDs._NoiseTexture, noiseTexture);
            buffer.SetComputeVectorParam(movingShader, ShaderIDs._OffsetDirection, transform.forward);
            offset += Time.deltaTime * runSpeed;
            offset = frac(offset);
            buffer.SetComputeFloatParam(movingShader, ShaderIDs._Offset, offset * noiseTexture.height);
            buffer.SetComputeVectorParam(movingShader, ShaderIDs._NoiseTexture_Size, new Vector2(noiseTexture.width, noiseTexture.height));
            ComputeShaderUtility.Dispatch(movingShader, buffer, 0, currentPosBuffer.count);
        }


        public override void DrawDepthPrepass(CommandBuffer buffer)
        {
            DrawPass(buffer, 2);
        }
    }
}