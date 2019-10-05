#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
public class GenerateMip : ScriptableWizard
{
    public MPipeline.MTerrainData terrainData;

    private void OnWizardCreate()
    {

    }
}
#endif