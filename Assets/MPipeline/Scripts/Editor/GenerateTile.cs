using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using MPipeline;
using Unity.Mathematics;
using System;
using System.IO;
using static Unity.Mathematics.math;
using Random = Unity.Mathematics.Random;
public class GenerateTile : EditorWindow
{
    [MenuItem("Generator/Tile Generator")]
    private static void CreateWizard()
    {
        GenerateTile window = (GenerateTile)GetWindow(typeof(GenerateTile));
        window.Show();
    }
    private Vector2Int tileScale = new Vector2Int(16, 32);
    private float alias = 0.5f;
    private Vector2 randomUVScale = new Vector2(0.2f, 0.2f);
    private Vector2Int resolution = new Vector2Int(1024, 1024);
    private string path = "Assets/Textures/Test.asset";
    private Material testMat;
    private void OnGUI()
    {
        tileScale = EditorGUILayout.Vector2IntField("Tile count: ", tileScale);
        alias = EditorGUILayout.Slider("Alias Offset: ", alias, 0, 1);
        randomUVScale = EditorGUILayout.Vector2Field("Tile UV's Scale: ", randomUVScale);
        resolution = EditorGUILayout.Vector2IntField("Resolution: ", resolution);
        path = EditorGUILayout.TextField("Path: ", path);
        testMat = EditorGUILayout.ObjectField("Test Material: ", testMat, typeof(Material), false) as Material;
        if (GUILayout.Button("Generate Tile Map"))
        {
            Texture2D tex = new Texture2D(tileScale.x, tileScale.y, TextureFormat.RGHalf, false, true);
            Color[] cols = new Color[tileScale.x * tileScale.y];
            Random rand = new Random((uint)Guid.NewGuid().GetHashCode());
            for (int x = 0; x < tileScale.x; ++x)
                for (int y = 0; y < tileScale.y; ++y)
                {
                    cols[y * tileScale.x + x] = new Color(rand.NextFloat() * (1 - randomUVScale.x), rand.NextFloat() * (1 - randomUVScale.y), 0);
                }
            tex.SetPixels(cols);
            tex.Apply();
            RenderTexture rt = new RenderTexture(new RenderTextureDescriptor
            {
                width = resolution.x,
                height = resolution.y,
                volumeDepth = 1,
                dimension = UnityEngine.Rendering.TextureDimension.Tex2D,
                colorFormat = RenderTextureFormat.RGHalf,
                msaaSamples = 1,
                enableRandomWrite = true
            });
            rt.Create();
            Material mat = new Material(Shader.Find("Hidden/TileGenerator"));
            mat.SetTexture("_RandomTex", tex);
            mat.SetFloat("_TileAlias", alias);
            mat.SetVector("_UVScale", randomUVScale);
            Graphics.Blit(null, rt, mat, 0);
            Texture2D resultTex = new Texture2D(resolution.x, resolution.y, TextureFormat.RGHalf, false, true);
            ComputeBuffer dataBuffer = new ComputeBuffer(resolution.x * resolution.y, 16);
            ComputeShader dataShader = Resources.Load<ComputeShader>("ReadRTData");
            dataShader.SetTexture(0, "_TargetTexture", rt);
            dataShader.SetBuffer(0, "_TextureDatas", dataBuffer);
            dataShader.SetInt("_Width", resolution.x);
            dataShader.SetInt("_Height", resolution.y);
            dataShader.Dispatch(0, resolution.x / 8, resolution.y / 8, 1);
            Color[] allColors = new Color[resolution.x * resolution.y];
            dataBuffer.GetData(allColors);
            resultTex.SetPixels(allColors);
            AssetDatabase.CreateAsset(resultTex, path);
            DestroyImmediate(tex);
            DestroyImmediate(mat);
            DestroyImmediate(rt);
            dataBuffer.Dispose();
            if (testMat) testMat.SetTexture("_UVTex", resultTex);
        }
    }
}