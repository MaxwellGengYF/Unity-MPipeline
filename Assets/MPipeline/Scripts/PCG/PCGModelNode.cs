#if UNITY_EDITOR
using UnityEngine;
using Unity.Mathematics;
using System.Collections.Generic;
using static Unity.Mathematics.math;
using UnityEngine.Rendering;
using Random = Unity.Mathematics.Random;
namespace MPipeline.PCG
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [ExecuteInEditMode]
    public unsafe class PCGModelNode : PCGNodeBase
    {
        private MeshRenderer mr;
        public List<Material> allMats = new List<Material>();
        private uint matIndex;
        public override void Init(PCGResources res)
        {
            mr = GetComponent<MeshRenderer>();
            if(allMats.Count <= 0)
            {
                enabled = false;
                return;
            }
            mr.enabled = true;
            UpdateSettings();
        }

        public override void UpdateSettings()
        {
            Random r = new Random(RandomSeed);
            matIndex = r.NextUInt();
            mr.sharedMaterial = allMats[(int)(matIndex % (uint)allMats.Count)];
        }

        public override NativeList<Point> GetPointsResult(out Material[] targetMaterials)
        {
            targetMaterials = new Material[] { mr.sharedMaterial };
            Mesh targetMesh = GetComponent<MeshFilter>().sharedMesh;
            Vector3[] vertices, normals;
            Vector2[] uvs;
            Vector4[] tangents;
            PCGLibrary.GetTransformedMeshData(targetMesh, transform.localToWorldMatrix, out vertices, out tangents, out uvs, out normals);
            NativeList<Point> points = new NativeList<Point>(targetMesh.vertexCount * 2, Unity.Collections.Allocator.Temp);
            NativeList<int> mats = new NativeList<int>(targetMesh.vertexCount, Unity.Collections.Allocator.Temp);
            for (int i = 0; i < targetMesh.subMeshCount; ++i)
            {
                int[] triangles = targetMesh.GetTriangles(i);
                PCGLibrary.GetPointsWithArrays(points, mats, vertices, normals, uvs, tangents, triangles, i);
                //TODO
                //Material Count
            }
            return points;
        }

        public override Bounds GetBounds()
        {
            return GetComponent<MeshFilter>().sharedMesh.bounds;
        }

        public override void Dispose()
        {
            mr.enabled = false;
        }
    }
}
#endif