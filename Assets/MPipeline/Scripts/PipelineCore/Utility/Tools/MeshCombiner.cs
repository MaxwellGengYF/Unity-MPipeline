using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System.IO;
using Unity.Mathematics;
using Debug = UnityEngine.Debug;
using MStudio;
#if UNITY_EDITOR
using UnityEditor;
#endif
namespace MPipeline
{
    public unsafe class MeshCombiner : MonoBehaviour
    {
#if UNITY_EDITOR
        public ClusterMatResources res;

        public void GetPoints(NativeList<Point> points, NativeList<int> materialIndices, Mesh targetMesh, Transform meshTrans, Material[] sharedMaterials, Dictionary<Material, int> matToIndex)
        {
            Vector3[] vertices;
            Vector3[] normals;
            Vector2[] uvs;
            Vector4[] tangents;
            PCG.PCGLibrary.GetTransformedMeshData(targetMesh, meshTrans.localToWorldMatrix, out vertices, out tangents, out uvs, out normals);
            for (int i = 0; i < targetMesh.subMeshCount; ++i)
            {
                int[] triangles = targetMesh.GetTriangles(i);
                Material mat = sharedMaterials[i];
                PCG.PCGLibrary.GetPointsWithArrays(points, materialIndices, vertices, normals, uvs, tangents, triangles, matToIndex[mat]);
                //TODO
                //Material Count
            }
        }
        public CombinedModel ProcessCluster(MeshRenderer[] allRenderers, ref SceneStreamLoader loader, Dictionary<MeshRenderer, bool> lowLODLevels)
        {
            List<MeshFilter> allFilters = new List<MeshFilter>(allRenderers.Length);
            int sumVertexLength = 0;

            for (int i = 0; i < allRenderers.Length; ++i)
            {
                if (!lowLODLevels.ContainsKey(allRenderers[i]))
                {
                    MeshFilter filter = allRenderers[i].GetComponent<MeshFilter>();
                    allFilters.Add(filter);
                    sumVertexLength += (int)(filter.sharedMesh.vertexCount * 1.2f);

                }
            }
            NativeList<Point> points = new NativeList<Point>(sumVertexLength, Allocator.Temp);
            NativeList<int> triangleMaterials = new NativeList<int>(sumVertexLength / 3, Allocator.Temp);
            var matToIndexDict = VirtualMaterial.GetMaterialsData(allRenderers, ref loader);
            for (int i = 0; i < allFilters.Count; ++i)
            {
                Mesh mesh = allFilters[i].sharedMesh;
                GetPoints(points, triangleMaterials, mesh, allFilters[i].transform, allRenderers[i].sharedMaterials, matToIndexDict);
            }
            float3 less = points[0].vertex;
            float3 more = points[0].vertex;

            for (int i = 1; i < points.Length; ++i)
            {
                float3 current = points[i].vertex;
                if (less.x > current.x) less.x = current.x;
                if (more.x < current.x) more.x = current.x;
                if (less.y > current.y) less.y = current.y;
                if (more.y < current.y) more.y = current.y;
                if (less.z > current.z) less.z = current.z;
                if (more.z < current.z) more.z = current.z;
            }

            float3 center = (less + more) / 2;
            float3 extent = more - center;
            Bounds b = new Bounds(center, extent * 2);
            CombinedModel md;
            md.bound = b;
            md.allPoints = points;
            md.allMatIndex = triangleMaterials;
            return md;
        }

        public struct CombinedModel
        {
            public NativeList<Point> allPoints;
            public NativeList<int> allMatIndex;
            public Bounds bound;
        }
        public string modelName = "TestFile";
        [Range(100, 500)]
        public int voxelCount = 100;
        [EasyButtons.Button]
        public void TryThis()
        {
            bool save = false;

            if (res == null)
            {
                save = true;
                res = ScriptableObject.CreateInstance<ClusterMatResources>();
                res.name = "SceneManager";
                res.clusterProperties = new List<SceneStreaming>();
            }

            SceneStreaming property = new SceneStreaming();
            SceneStreamLoader loader = new SceneStreamLoader();
            loader.fsm = new FileStream(ClusterMatResources.infosPath + modelName + ".mpipe", FileMode.OpenOrCreate, FileAccess.ReadWrite);
            property.name = modelName;
            int containIndex = -1;
            for(int i = 0; i < res.clusterProperties.Count; ++i)
            {
                if (property.name ==  res.clusterProperties[i].name)
                {
                    containIndex = i;
                    break;
                }
            }
            LODGroup[] groups = GetComponentsInChildren<LODGroup>();
            Dictionary<MeshRenderer, bool> lowLevelDict = new Dictionary<MeshRenderer, bool>();
            foreach (var i in groups)
            {
                LOD[] lods = i.GetLODs();
                for (int j = 1; j < lods.Length; ++j)
                {
                    foreach (var k in lods[j].renderers)
                    {
                        if (k.GetType() == typeof(MeshRenderer))
                            lowLevelDict.Add(k as MeshRenderer, true);
                    }
                }
            }
            CombinedModel model = ProcessCluster(GetComponentsInChildren<MeshRenderer>(false), ref loader, lowLevelDict);
            property.clusterCount = ClusterGenerator.GenerateCluster(model.allPoints, model.allMatIndex, model.bound, voxelCount, containIndex < 0 ? res.clusterProperties.Count : containIndex, ref loader);
          
            res.maximumMaterialCount = Mathf.Max(1, res.maximumMaterialCount);
            res.maximumMaterialCount = Mathf.Max(res.maximumMaterialCount, loader.allProperties.Length);
            if (containIndex < 0) res.clusterProperties.Add(property);
            else res.clusterProperties[containIndex] = property;
            if (save)
                AssetDatabase.CreateAsset(res, "Assets/SceneManager.asset");
            else
                EditorUtility.SetDirty(res);
            loader.SaveAll(property.clusterCount);
            loader.Dispose();
        }
#endif
    }
}
