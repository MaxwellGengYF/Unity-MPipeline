using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class LPResources : ScriptableObject
{
    [SerializeField]
    public ComputeShader GetSurfelIntersect;

    //[MenuItem("Example/Create ExampleAsset")]
    //static void CreateExampleAsset()
    //{
    //    var exampleAsset = CreateInstance<LPResources>();

    //    AssetDatabase.CreateAsset(exampleAsset, "Assets/ExampleAsset.asset");
    //    AssetDatabase.Refresh();
    //}
}
