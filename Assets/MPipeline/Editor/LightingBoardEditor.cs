using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
public class LightingBoardEditor : ShaderGUI
{
    public enum BlendMode
    {
        Add = 0,
        Multiple
    }
    public enum UVType
    {
        UV0 = 0,
        UV1
    }
    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        Material targetMat = materialEditor.target as Material;
        Undo.RecordObject(targetMat, targetMat.name);
        bool useMotionVector = targetMat.GetShaderPassEnabled("MotionVector");
        useMotionVector = EditorGUILayout.Toggle("MotionVector", useMotionVector);
        targetMat.SetShaderPassEnabled("MotionVector", useMotionVector);
        UVType mainTexUseUV1 = targetMat.IsKeywordEnabled("FIRST_TEX_USE_UV1") ? UVType.UV1 : UVType.UV0;
        UVType secondTexUseUV1 = targetMat.IsKeywordEnabled("SECOND_TEX_USE_UV1") ? UVType.UV1 : UVType.UV0;
        BlendMode blendAdd = targetMat.IsKeywordEnabled("BLEND_ADD") ? BlendMode.Add : BlendMode.Multiple;
        mainTexUseUV1 = (UVType)EditorGUILayout.EnumPopup("MainTex UV Type", mainTexUseUV1);
        secondTexUseUV1 = (UVType)EditorGUILayout.EnumPopup("SecondaryTex UV Type", secondTexUseUV1);
        blendAdd = (BlendMode)EditorGUILayout.EnumPopup("Texture Blend Mode", blendAdd);
        bool useCutoff = targetMat.GetTexture("_CutOffTex");
        if (blendAdd == BlendMode.Add)
            targetMat.EnableKeyword("BLEND_ADD");
        else
            targetMat.DisableKeyword("BLEND_ADD");
        if(mainTexUseUV1 == UVType.UV1)
            targetMat.EnableKeyword("FIRST_TEX_USE_UV1");
        else
            targetMat.DisableKeyword("FIRST_TEX_USE_UV1");
        if (secondTexUseUV1 == UVType.UV1)
            targetMat.EnableKeyword("SECOND_TEX_USE_UV1");
        else
            targetMat.DisableKeyword("SECOND_TEX_USE_UV1");
        if (useCutoff)
        {
            targetMat.EnableKeyword("CUT_OFF");
            targetMat.SetInt("_ZWrite", 0);
            targetMat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Equal);
            targetMat.renderQueue = 2450;
        }
        else
        {
            targetMat.DisableKeyword("CUT_OFF");
            targetMat.SetInt("_ZWrite", 1);
            targetMat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Less);
            targetMat.renderQueue = 2000;
        }
        base.OnGUI(materialEditor, properties);
    }
}
