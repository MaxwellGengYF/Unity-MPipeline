using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Random = Unity.Mathematics.Random;
namespace MPipeline.PCG
{
    public class BoxTransformer : EditorWindow
    {
        private PCGResources res;
        [MenuItem("PCG/Box Transformer")]
        private static void CreateWizard()
        {
            BoxTransformer window = (BoxTransformer)GetWindow(typeof(BoxTransformer));
            window.Show();
        }
        private string propertyName;
        private Transform boxParent;
        private bool updateRandom;
        private void OnGUI()
        {
            if (!PCGMain.current) {
                GameObject pcgMain = new GameObject("PCG Main", typeof(PCGMain));
                PCGMain.current = pcgMain.GetComponent<PCGMain>();
            }
            res = PCGMain.current.pcgRes;
            if (!res)
            {
                EditorGUILayout.LabelField("PCG Resource is NULL!");
                return;
            }
            if (!PCGMain.current.enabled)
            {
                EditorGUILayout.LabelField("PCG Main Component is disabled!");
                return;
            }
            propertyName = EditorGUILayout.TextField("Target Model Name: ", propertyName);
            boxParent = EditorGUILayout.ObjectField("Box Parent: ", boxParent, typeof(Transform), true) as Transform;
            updateRandom = EditorGUILayout.Toggle("Update Random Seed: ", updateRandom);
            if (!boxParent) return;
            Random rd = new Random((uint)System.Guid.NewGuid().GetHashCode());
            if (GUILayout.Button("Generate Random Nodes"))
            {
                int index = -1;
                for (int i = 0; i < res.allNodes.Count; ++i)
                {
                    if (res.allNodes[i].name == propertyName)
                    {
                        index = i;
                        break;
                    }
                }
                if (index < 0)
                {
                    Debug.Log("No Such Node Name!");
                    return;
                }
                int childCount = boxParent.childCount;
                for (int i = 0; i < childCount; ++i)
                {
                    Transform tr = boxParent.GetChild(i);
                    if (!tr.gameObject.activeSelf) continue;
                    tr.gameObject.SetActive(false);
                    int randIndex = abs(rd.NextInt());
                    randIndex %= res.allNodes[index].objs.Count;
                    GameObject newObj = Instantiate(res.allNodes[index].objs[randIndex], tr.position, tr.rotation, boxParent);
                    PCGNodeBase node = newObj.GetComponent<PCGNodeBase>();
                    if (updateRandom) node.RandomSeed = rd.NextUInt();
                    float3 extent = node.GetBounds().extents;
                    float3 scale = tr.localScale * 0.5f;
                    scale /= extent;
                    node.transform.localScale = scale;
                    node.UpdateSettings();
                }
            }
            if (GUILayout.Button("Clear Nodes"))
            {
                int childCount = boxParent.childCount;
                for (int i = 0; i < min(boxParent.childCount, childCount); ++i)
                {
                    Transform tr = boxParent.GetChild(i);
                    if (tr.GetComponent<PCGNodeBase>())
                    {
                        DestroyImmediate(tr.gameObject);
                        i--;
                    }
                    else
                    {
                        tr.gameObject.SetActive(true);
                    }
                }
            }
        }
    }
}
