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
        if(useRainning)
        {
            targetMat.EnableKeyword("USE_RANNING");
        }
        else
        {
            targetMat.DisableKeyword("USE_RANNING");
        }
        if (!targetMatEnabled)
        {
            targetMat.DisableKeyword("CUT_OFF");
        }
        else
        {
            targetMat.EnableKeyword("CUT_OFF");
        }
        base.OnGUI(materialEditor, properties);
        if(targetMat.GetTexture("_DetailAlbedo") == null && targetMat.GetTexture("_DetailNormal") == null)
        {
            targetMat.DisableKeyword("DETAIL_ON");
        }else
        {
            targetMat.EnableKeyword("DETAIL_ON");
        }
    }
}
