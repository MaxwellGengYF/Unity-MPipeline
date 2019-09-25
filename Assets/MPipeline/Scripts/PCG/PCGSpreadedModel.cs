#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Mathematics;
using Unity.Collections;
using static Unity.Mathematics.math;
using Random = Unity.Mathematics.Random;
namespace MPipeline.PCG
{

    [ExecuteInEditMode]
    public unsafe class PCGSpreadedModel : PCGNodeBase
    {
        public Mesh mesh;
        public List<Material> targetMats = new List<Material>();
        private uint matIndex;
        public uint3 countPerFrame = uint3(5, 1, 1);
        public float3 localScale = 1;
        public float2 uvTileRandomRange = float2(1, 1);
        public float2 uvOffsetRandomRange = float2(0, 1);
        private NativeList<float4x4> allMatrices;
        private NativeList<float4> tileOffsets;
        private ComputeBuffer meshBuffer;
        private ComputeBuffer triangleBuffer;
        private ComputeBuffer instanceBuffer;
        public override Bounds GetBounds()
        {
            return new Bounds(Vector3.zero, Vector3.one);
        }
        public override void Init(PCGResources res)
        {
            allMatrices = new NativeList<float4x4>(20, Allocator.Persistent);
            tileOffsets = new NativeList<float4>(20, Allocator.Persistent);
            if (!mesh || targetMats.Count <= 0)
            {
                enabled = false;
                return;
            }
            NativeArray<MeshAppdata> meshData;
            int[] tri;
            int vertLen = PCGLibrary.GetAppDataFromMesh(mesh, out meshData, out tri);
            meshBuffer = new ComputeBuffer(vertLen, sizeof(MeshAppdata));
            meshBuffer.SetData(meshData);
            instanceBuffer = new ComputeBuffer(20, sizeof(PerObjectDatas));
            triangleBuffer = new ComputeBuffer(tri.Length, sizeof(int));
            triangleBuffer.SetData(tri);
        }
        public override NativeList<Point> GetPointsResult(out Material[] targetMaterials)
        {
            targetMaterials = new Material[] { targetMats[(int)(matIndex % (uint)targetMats.Count)] };
            NativeList<Point> allPoints = new NativeList<Point>(triangleBuffer.count * allMatrices.Length, Allocator.Temp);
            Vector3[] vertices, normals;
            Vector2[] uvs;
            Vector4[] tangents;
            PCGLibrary.GetTransformedMeshData(mesh, Matrix4x4.identity, out vertices, out tangents, out uvs, out normals);
            int[] triangles = mesh.triangles;
            for (int i = 0; i < allMatrices.Length; ++i)
            {
                float4x4 mat = allMatrices[i];
                float4 tileOffset = tileOffsets[i];
                foreach (var t in triangles)
                {
                    float4 tan = tangents[t];
                    allPoints.Add(new Point
                    {
                        normal = mul(mat, float4(normals[t], 0)).xyz,
                        tangent = float4(mul(mat, float4(tan.xyz, 0)).xyz, tan.w),
                        uv0 = (float2)uvs[t] * tileOffset.xy + tileOffset.zw
                    });
                }
            }
            return allPoints;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        }

        [EasyButtons.Button]
        public override void UpdateSettings()
        {
            if (!enabled) return;
            Random r = new Random(RandomSeed);
            for (int i = 0; i < tileOffsets.Length; ++i)
            {
                float tile = (r.NextFloat() * (uvTileRandomRange.y - uvTileRandomRange.x)) + uvTileRandomRange.x;
                tileOffsets[i] = (float4(tile, tile, r.NextFloat2() * (uvOffsetRandomRange.y - uvOffsetRandomRange.x) + uvOffsetRandomRange.x));
            }
            matIndex = r.NextUInt();
        }

        private void UpdateData()
        {
            float4x4 localToWorldMatrix = transform.localToWorldMatrix;
            float4x4 normalizedLocalToWorld = float4x4(float4(transform.right * localScale.x, 0), float4(transform.up * localScale.y, 0), float4(transform.forward * localScale.z, 0), float4(transform.position, 1));
            allMatrices.Clear();
            for (int x = 0; x < countPerFrame.x; ++x)
                for (int y = 0; y < countPerFrame.y; ++y)
                    for (int z = 0; z < countPerFrame.z; ++z)
                    {
                        float3 uv = (float3(x, y, z) + 0.5f) / float3(countPerFrame);
                        float3 localPos = lerp(-0.5f, 0.5f, uv);
                        allMatrices.Add(float4x4(normalizedLocalToWorld.c0, normalizedLocalToWorld.c1, normalizedLocalToWorld.c2, mul(localToWorldMatrix, float4(localPos, 1))));
                    }

            Random r = new Random(RandomSeed);
            if (allMatrices.Length > tileOffsets.Length)
            {
                for (int i = tileOffsets.Length; i < allMatrices.Length; ++i)
                {
                    float tile = (r.NextFloat() * (uvTileRandomRange.y - uvTileRandomRange.x)) + uvTileRandomRange.x;
                    tileOffsets.Add(float4(tile, tile, r.NextFloat2() * (uvOffsetRandomRange.y - uvOffsetRandomRange.x) + uvOffsetRandomRange.x));
                }
            }
            else if (allMatrices.Length < tileOffsets.Length)
            {
                tileOffsets.RemoveLast(tileOffsets.Length - allMatrices.Length);
            }
            NativeArray<PerObjectDatas> datas = new NativeArray<PerObjectDatas>(tileOffsets.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            PerObjectDatas* ptr = datas.Ptr();
            for (int i = 0; i < tileOffsets.Length; ++i)
            {
                ptr[i] = new PerObjectDatas
                {
                    localToWorldMatrix = allMatrices[i],
                    uvTileOffset = tileOffsets[i]
                };
            }
            if (datas.Length > instanceBuffer.count)
            {
                instanceBuffer.Dispose();
                instanceBuffer = new ComputeBuffer(datas.Length, sizeof(PerObjectDatas));
            }
            instanceBuffer.SetData(datas);
            datas.Dispose();
        }
        public override void DrawDepthPrepass(CommandBuffer buffer)
        {
            UpdateData();
            buffer.SetGlobalBuffer("_VertexBuffer", meshBuffer);
            buffer.SetGlobalBuffer("_InstanceBuffer", instanceBuffer);
            buffer.SetGlobalBuffer(ShaderIDs._IndexBuffer, triangleBuffer);
            buffer.DrawProcedural(Matrix4x4.identity, targetMats[(int)(matIndex % (uint)targetMats.Count)], 1, MeshTopology.Triangles, triangleBuffer.count, allMatrices.Length);

        }

        public override void DrawGBuffer(CommandBuffer buffer)
        {
            buffer.SetGlobalBuffer("_VertexBuffer", meshBuffer);
            buffer.SetGlobalBuffer("_InstanceBuffer", instanceBuffer);
            buffer.SetGlobalBuffer(ShaderIDs._IndexBuffer, triangleBuffer);
            buffer.DrawProcedural(Matrix4x4.identity, targetMats[(int)(matIndex % (uint)targetMats.Count)], 0, MeshTopology.Triangles, triangleBuffer.count, allMatrices.Length);
        }

        public override void Dispose()
        {
            if (meshBuffer != null) meshBuffer.Dispose();
            if (instanceBuffer != null) instanceBuffer.Dispose();
            allMatrices.Dispose();
            tileOffsets.Dispose();
        }
    }
}
#endif