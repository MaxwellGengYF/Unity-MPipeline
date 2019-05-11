using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
public class ShouShouEditor : ShaderGUI
{
    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        Material targetMat = materialEditor.target as Material;
        bool targetMatEnabled = targetMat.IsKeywordEnabled("CUT_OFF");
        targetMatEnabled = EditorGUILayout.Toggle("Cut off", targetMatEnabled);
        bool useRainning = targetMat.IsKeywordEnabled("USE_RANNING");
        useRainning = EditorGUILayout.Toggle("Use Rain", useRainning);
        if (useRainning)
        {
            targetMat.EnableKeyword("USE_RANNING");
        }
        else
        {
            targetMat.DisableKeyword("USE_RANNING");
        }
        targetMat.SetInt("_ZTest", targetMatEnabled ? (int)UnityEngine.Rendering.CompareFunction.Equal : (int)UnityEngine.Rendering.CompareFunction.Less);
        targetMat.SetInt("_ZWrite", targetMatEnabled ? 0 : 1);
        if (!targetMatEnabled)
        {
            targetMat.DisableKeyword("CUT_OFF");
            if (targetMat.renderQueue > 2450)
                targetMat.renderQueue = 2000;
        }
        else
        {
            
            targetMat.EnableKeyword("CUT_OFF");
            if (targetMat.renderQueue < 2451)
                targetMat.renderQueue = 2451;
        }
        base.OnGUI(materialEditor, properties);
        if (targetMat.GetTexture("_DetailAlbedo") == null && targetMat.GetTexture("_DetailNormal") == null)
        {
            targetMat.DisableKeyword("DETAIL_ON");
        }
        else
        {
            targetMat.EnableKeyword("DETAIL_ON");
        }
    }
}
