#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
public class ShouShouTool : MonoBehaviour
{
    [EasyButtons.Button]
    void RemoveAllCollider()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>();
        for(int i = 0; i < colliders.Length; ++i)
        {
            Undo.RecordObject(colliders[i], colliders[i].GetInstanceID().ToString());
            DestroyImmediate(colliders[i]);
        }
    }
    [EasyButtons.Button]
    void RemoveAllLod()
    {
        LODGroup[] groups = GetComponentsInChildren<LODGroup>();
        List<GameObject> renderers = new List<GameObject>();
        foreach (var i in groups)
        {
            LOD[] lods = i.GetLODs();
            for (int j = 1; j < lods.Length; ++j)
            {
                foreach (var k in lods[j].renderers)
                {
                    renderers.Add(k.gameObject);
                }
            }
        }
        for (int i = 0; i < groups.Length; ++i) {
            Undo.RecordObject(groups[i], groups[i].GetInstanceID().ToString());
            DestroyImmediate(groups[i]);
        }
        for(int i = 0; i < renderers.Count; ++i)
        {
            Undo.RecordObject(renderers[i], renderers[i].GetInstanceID().ToString());
            DestroyImmediate(renderers[i]);
        }

    }
}

#endif