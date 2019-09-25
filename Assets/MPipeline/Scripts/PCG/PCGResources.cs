#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace MPipeline.PCG
{
    [CreateAssetMenu(menuName = "PCG/PCGResource")]
    public class PCGResources : ScriptableObject
    {
        [System.Serializable]
        public struct PCGNode
        {
            public string name;
            public List<GameObject> objs;
        }
        public List<PCGNode> allNodes = new List<PCGNode>();
        [EasyButtons.Button]
        private void CheckNode()
        {
            for (int i = 0; i < allNodes.Count; ++i)
            {
                PCGNode node = allNodes[i];
                if (string.IsNullOrEmpty(node.name))
                {
                    Debug.Log("Index: " + i + " name is empty! ");
                    allNodes[i] = allNodes[allNodes.Count - 1];
                    i--;
                    allNodes.RemoveAt(allNodes.Count - 1);
                }
                else
                {
                    bool isBad = false;
                    foreach (var o in allNodes[i].objs)
                    {
                        if (!o || !o.GetComponent<PCGNodeBase>())
                        {
                            isBad = true;
                            break;
                        }
                    }
                    if (isBad)
                    {
                        Debug.Log("Index: " + i + " " + node.name + " is a useless node!");
                        allNodes[i] = allNodes[allNodes.Count - 1];
                        i--;
                        allNodes.RemoveAt(allNodes.Count - 1);
                    }
                }
            }
            Debug.Log("Check Complete!");
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
}
#endif