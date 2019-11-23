#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using Unity.Mathematics;
using static Unity.Mathematics.math;
namespace MPipeline
{
    public class HLODGenerator : ScriptableWizard
    {
        public string levelName = "FirstLevel";
        [Range(100, 300)]
        public int voxelCount = 100;
        public HLOD hlodComponent;
        public List<Transform> allLODParent = new List<Transform>();
        [MenuItem("MPipeline/Terrain/HLOD Generate")]
        private static void CreateWizard()
        {
            DisplayWizard<HLODGenerator>("HLOD Generator", "Generate");
        }

        private void OnWizardUpdate()
        {
            if (string.IsNullOrEmpty(levelName))
            {
                errorString = "Level Name is Empty!";
            }
            else if(!hlodComponent)
            {
                errorString = "HLOD Component is Empty!";
            }
            else if(allLODParent .Count != hlodComponent.allLodDistances.Length)
            {
                errorString = "LOD Object Count: " + allLODParent.Count + " LOD Distances Count: " + hlodComponent.allLodDistances.Length;
            }
            else
                errorString = "";
        }

        public void Pack(SceneStreaming property, string modelName, List<MeshRenderer> meshRenderers, ClusterMatResources res)
        {
            string fileName = ClusterMatResources.infosPath + modelName + ".mpipe";

            if (string.IsNullOrEmpty(modelName))
            {
                Debug.LogError("Name Empty!");
                return;
            }
           
           
            SceneStreamLoader loader = new SceneStreamLoader();

            loader.fsm = new FileStream(fileName, FileMode.Create, FileAccess.ReadWrite);
            property.fileName = modelName;
            MeshCombiner.CombinedModel model = MeshCombiner.ProcessCluster(meshRenderers, ref loader);
            loader.clusterCount = ClusterGenerator.GenerateCluster(model.allPoints, model.allMatIndex, model.bound, voxelCount, ref loader);

            res.maximumMaterialCount = Mathf.Max(1, res.maximumMaterialCount);
            res.maximumMaterialCount = Mathf.Max(res.maximumMaterialCount, loader.allProperties.Length);
            loader.SaveAll();
            loader.Dispose();
        }
        private void CollectMeshRenderersInChunk(MeshRenderer[] allMeshRenderers, Dictionary<int2, List<MeshRenderer>> collectResult, double3 extent, double3 center, int size)
        {
            foreach (var i in collectResult)
            {
                if (null != i.Value) i.Value.Clear();
            }
            collectResult.Clear();
            double3 boxSize = extent * 2;
            double3 cornerPos = center - extent;
            foreach (var i in allMeshRenderers)
            {
                double3 position = (float3)i.transform.position;
                double2 rate = (position.xz - cornerPos.xz) / boxSize.xz;
                int2 localPos = (int2)(rate * size);
                localPos = clamp(localPos, 0, size - 1);
                List<MeshRenderer> meshList;
                if(!collectResult.TryGetValue(localPos, out meshList))
                {
                    meshList = new List<MeshRenderer>();
                    collectResult.Add(localPos, meshList);
                }
                meshList.Add(i);
            }
        }

        private void OnWizardCreate()
        {
            if (!string.IsNullOrEmpty(errorString)) return;
            if (hlodComponent.allGPURPScene != null)
            {
                foreach (var i in hlodComponent.allGPURPScene)
                {
                    if (i) DestroyImmediate(i.gameObject);
                }
                hlodComponent.allGPURPScene.Clear();
            }
            else
            {
                hlodComponent.allGPURPScene = new List<SceneStreaming>();
            }
            ClusterMatResources res = AssetDatabase.LoadAssetAtPath<ClusterMatResources>("Assets/SceneManager.asset");
            bool save = false;
            if (!res)
            {
                save = true;
                res = ScriptableObject.CreateInstance<ClusterMatResources>();
                res.name = "SceneManager";
            }
            Dictionary<int2, List<MeshRenderer>> chunkRenderers = new Dictionary<int2, List<MeshRenderer>>();
            Undo.RecordObject(hlodComponent, hlodComponent.GetInstanceID().ToString());
            for (int i = 0; i < hlodComponent.allLodDistances.Length; ++i)
            {
                int size = (int)(0.1 + pow(2.0, i));
                CollectMeshRenderersInChunk(allLODParent[i].GetComponentsInChildren<MeshRenderer>(), chunkRenderers, hlodComponent.extent, (float3)hlodComponent.transform.position, size);
                for (int x = 0; x < size; ++x)
                    for (int y = 0; y < size; ++y)
                    {
                        List<MeshRenderer> inChunkRenderer;
                        if(chunkRenderers.TryGetValue(int2(x,y), out inChunkRenderer))
                        {
                            if (inChunkRenderer.Count == 0) hlodComponent.allGPURPScene.Add(null);
                            else
                            {
                                //TODO
                                //Generate Scene Stream Object
                                string objName = levelName + "_LOD" + i + "_X" + x + "_Y" + y;
                                GameObject currentObj = new GameObject(objName, typeof(SceneStreaming));
                                currentObj.transform.SetParent(hlodComponent.transform);
                                SceneStreaming sc = currentObj.GetComponent<SceneStreaming>();
                                Pack(sc, objName, inChunkRenderer, res);
                                hlodComponent.allGPURPScene.Add(sc);
                            }
                        }
                        else
                        {
                            hlodComponent.allGPURPScene.Add(null);
                        }
                    }
            }
           
            if (save)
                AssetDatabase.CreateAsset(res, "Assets/SceneManager.asset");
            else
                EditorUtility.SetDirty(res);
        }
    }
}
#endif