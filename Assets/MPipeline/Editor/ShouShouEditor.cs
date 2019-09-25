using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
public class ShouShouEditor : ShaderGUI
{
    public enum LightingModelType
    {
        Unlit = 0, DefaultLit = 1, SkinLit = 2, ClothLit = 3, ClearCoat = 4
    }
    private static string[] ops = null;
    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        if (ops == null)
        {
            ops = new string[23];
            for (int i = 0; i < 23; ++i)
            {
                ops[i] = "Decal Layer " + i;
            }
        }
        Material targetMat = materialEditor.target as Material;
        Undo.RecordObject(targetMat, targetMat.name);
        bool useMotionVector = targetMat.GetShaderPassEnabled("MotionVector");
        useMotionVector = EditorGUILayout.Toggle("MotionVector", useMotionVector);
        targetMat.SetShaderPassEnabled("MotionVector", useMotionVector);
        bool targetMatEnabled = targetMat.IsKeywordEnabled("CUT_OFF");
        bool targetUseTessellation = targetMat.shader.name.Contains("Tessellation");
        targetMatEnabled = EditorGUILayout.Toggle("Cut off", targetMatEnabled);
        int targetDecalLayer = targetMat.GetInt("_DecalLayer");
        targetDecalLayer = EditorGUILayout.MaskField("Decal Layer", targetDecalLayer, ops);
        targetMat.SetInt("_DecalLayer", targetDecalLayer);
        LightingModelType currentType = (LightingModelType)targetMat.GetInt("_LightingModel");
        currentType = (LightingModelType)EditorGUILayout.EnumPopup("Lighting Model", currentType);
        bool disableEmission = false;
        if (targetMat.GetTexture("_SecondaryMainTex"))
        {
            targetMat.EnableKeyword("USE_SECONDARY_MAP");
            disableEmission = true;
        }
        else
        {
            targetMat.DisableKeyword("USE_SECONDARY_MAP");
        }
        targetMat.SetInt("_LightingModel", (int)currentType);
        if (currentType != LightingModelType.Unlit)
        {
            targetMat.EnableKeyword("LIT_ENABLE");
        }
        else
        {
            targetMat.DisableKeyword("LIT_ENABLE");
        }



        switch (currentType)
        {
            case LightingModelType.DefaultLit:
                targetMat.EnableKeyword("DEFAULT_LIT");
                targetMat.DisableKeyword("SKIN_LIT");
                targetMat.DisableKeyword("CLOTH_LIT");
                targetMat.DisableKeyword("CLEARCOAT_LIT");
                break;
            case LightingModelType.SkinLit:
                targetMat.DisableKeyword("DEFAULT_LIT");
                targetMat.EnableKeyword("SKIN_LIT");
                targetMat.DisableKeyword("CLOTH_LIT");
                targetMat.DisableKeyword("CLEARCOAT_LIT");
                break;
            case LightingModelType.ClothLit:
                targetMat.DisableKeyword("DEFAULT_LIT");
                targetMat.DisableKeyword("SKIN_LIT");
                targetMat.EnableKeyword("CLOTH_LIT");
                targetMat.DisableKeyword("CLEARCOAT_LIT");
                break;
            case LightingModelType.ClearCoat:
                targetMat.DisableKeyword("DEFAULT_LIT");
                targetMat.DisableKeyword("SKIN_LIT");
                targetMat.DisableKeyword("CLOTH_LIT");
                targetMat.EnableKeyword("CLEARCOAT_LIT");
                break;
            default:
                targetMat.DisableKeyword("DEFAULT_LIT");
                targetMat.DisableKeyword("SKIN_LIT");
                targetMat.DisableKeyword("CLOTH_LIT");
                targetMat.DisableKeyword("CLEARCOAT_LIT");
                break;
        }
        if (!targetMatEnabled)
        {
            targetMat.DisableKeyword("CUT_OFF");
            if (targetUseTessellation)
                targetMat.renderQueue = 2450;
            else
                targetMat.renderQueue = 2000;
        }
        else
        {
            targetMat.EnableKeyword("CUT_OFF");
            targetMat.renderQueue = 2451;
        }
        base.OnGUI(materialEditor, properties);
        if (disableEmission)
        {
            targetMat.SetTexture("_EmissionMap", null);
            EditorGUILayout.LabelField("Emission Forced close with secondary maps!");
        }
    }
}
