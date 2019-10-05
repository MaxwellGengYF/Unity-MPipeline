using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using MPipeline;
using Unity.Mathematics;
using System;
using UnityEngine.Rendering;
using System.IO;
using static Unity.Mathematics.math;
public class TransformShader : EditorWindow
{
    [MenuItem("MPipeline/Transform Shader")]
    private static void CreateWizard()
    {
        TransformShader window = (TransformShader)GetWindow(typeof(TransformShader));
        window.Show();
    }
    public enum LightingModelType
    {
        Unlit = 0, DefaultLit = 1, SkinLit = 2, ClothLit = 3, ClearCoat = 4
    }

    public static void SetMat(Material targetMat)
    {
        bool targetMatEnabled = targetMat.IsKeywordEnabled("CUT_OFF");
        bool targetUseTessellation = targetMat.shader.name.Contains("Tessellation");
        LightingModelType currentType = (LightingModelType)targetMat.GetInt("_LightingModel");
        Undo.RecordObject(targetMat, targetMat.name);
        targetMat.SetInt("_LightingModel", (int)currentType);
        if (targetUseTessellation)
        {
            targetMat.EnableKeyword("USE_TESSELLATION");
        }
        else
        {
            targetMat.DisableKeyword("USE_TESSELLATION");
        }
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
        if (targetMat.GetTexture("_SecondaryMainTex"))
        {
            targetMat.EnableKeyword("USE_SECONDARY_MAP");
        }
        else
        {
            targetMat.DisableKeyword("USE_SECONDARY_MAP");
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
    }
    private void Execute(Action<Material, MeshRenderer> func)
    {
        var lights = FindObjectsOfType<MeshRenderer>();
        Dictionary<Material, MeshRenderer> allMats = new Dictionary<Material, MeshRenderer>();
        foreach (var i in lights)
        {
            var mats = i.sharedMaterials;
            foreach (var j in mats)
            {
                if (!j) continue;
                if (allMats.ContainsKey(j))
                {
                    if (i.lightmapIndex >= 0) allMats[j] = i;
                }
                else
                    allMats[j] = i;
            }
        }

        foreach (var i in allMats.Keys)
        {
            func(i, allMats[i]);
        }
    }
    private RenderPipelineAsset asset;
    private string path = "Assets/MPipeline/Prefabs/DefaultResources.asset";
    private void OnGUI()
    {
        Shader defaultShader = Shader.Find("ShouShouPBR");
        Shader srpLightmapShader = Shader.Find("Maxwell/StandardLit(Lightmap)");
        Shader srpLightmapTessellationShader = Shader.Find("Maxwell/StandardLit_Tessellation(Lightmap)");
        path = EditorGUILayout.TextField("Pipeline Asset Path: ", path);
        if (GUILayout.Button("To Built-in Pipeline"))
        {
            asset = GraphicsSettings.renderPipelineAsset;
            GraphicsSettings.renderPipelineAsset = null;
            Execute((mat, r) =>
            {
                if (mat.shader == srpLightmapShader)
                {
                    mat.shader = defaultShader;
                    mat.SetInt("_UseTessellation", 0);
                }
                else if (mat.shader == srpLightmapTessellationShader)
                {
                    mat.shader = defaultShader;
                    mat.SetInt("_UseTessellation", 1);
                }
            });
        }
        if (GUILayout.Button("To MPipeline"))
        {
            if (!asset)
            {
                asset = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(path);
            }
            GraphicsSettings.renderPipelineAsset = asset;
            Execute((mat, rend) =>
            {
                if (mat.shader == defaultShader)
                {
                    if (mat.GetInt("_UseTessellation") > 0.5f)
                    {
                        mat.shader = srpLightmapTessellationShader;
                    }
                    else
                        mat.shader = srpLightmapShader;
                    SetMat(mat);
                }
            });
        }
        if(GUILayout.Button("Disable All Tessellation"))
        {
            Execute((mat, rend) =>
            {
                if (mat.shader == srpLightmapTessellationShader)
                {
                    mat.shader = srpLightmapShader;
                }
            });
        }

        if(GUILayout.Button("Use DX Normal"))
        {
            Execute((mat, rend) =>
            {
                if (mat.shader == srpLightmapShader || mat.shader == srpLightmapTessellationShader)
                {
                    Vector4 normalIntensity = mat.GetVector("_NormalIntensity");
                    if (normalIntensity.y > 0) normalIntensity.y *= -1;
                    mat.SetVector("_NormalIntensity", normalIntensity);
                }
            });
        }

        if (GUILayout.Button("Use OpenGL Normal"))
        {
            Execute((mat, rend) =>
            {
                if (mat.shader == srpLightmapShader || mat.shader == srpLightmapTessellationShader)
                {
                    Vector4 normalIntensity = mat.GetVector("_NormalIntensity");
                    if (normalIntensity.y < 0) normalIntensity.y *= -1;
                    mat.SetVector("_NormalIntensity", normalIntensity);
                }
            });
        }
    }
}