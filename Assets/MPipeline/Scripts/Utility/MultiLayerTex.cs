#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Unity.Mathematics;
using UnityEngine.Rendering;
using static Unity.Mathematics.math;
using MPipeline;
public unsafe sealed class MultiLayerTex : EditorWindow
{
    [MenuItem("MPipeline/MultiLayer Texture")]
    private static void CreateWizard()
    {
        MultiLayerTex window = (MultiLayerTex)GetWindow(typeof(MultiLayerTex));
        window.Show();
    }
    [SerializeField] private Material targetShowMat;
    [SerializeField] private ComputeShader drawShader;
    [SerializeField] private List<TextureSettings> allTextures = new List<TextureSettings>();
    [SerializeField] private RenderTexture rt;
    [SerializeField] private Vector2Int rtSize = new Vector2Int(1024, 1024);
    [SerializeField] private Color initColor = Color.black;
    [SerializeField] private RenderTextureFormat format = RenderTextureFormat.ARGB32;
    [SerializeField] private TextureFormat saveFormat = TextureFormat.ARGB32;
    [SerializeField] private bool saveIsOpen = false;
    [SerializeField] private string path = "Assets/Test.png";
    private void OnGUI()
    {
        if (!drawShader) drawShader = Resources.Load<ComputeShader>("ShaderBlend");
        targetShowMat = EditorGUILayout.ObjectField("Target Material: ", targetShowMat, typeof(Material), true) as Material;
        //TODO
        rtSize = EditorGUILayout.Vector2IntField("Texture Size: ", rtSize);
        rtSize.x = max(1, rtSize.x);
        rtSize.y = max(1, rtSize.y);
        format = (RenderTextureFormat)EditorGUILayout.EnumPopup("Texture Format: ", format);

        //Set RT
        if (!rt)
        {
            rt = new RenderTexture(new RenderTextureDescriptor
            {
                colorFormat = format,
                depthBufferBits = 0,
                enableRandomWrite = true,
                height = rtSize.y,
                width = rtSize.x,
                volumeDepth = 1,
                msaaSamples = 1,
                sRGB = false,
                dimension = TextureDimension.Tex2D
            });
            rt.Create();
        }
        else if (rt.format != format || rt.width != rtSize.x || rt.height != rtSize.y)
        {
            rt.Release();
            rt.width = rtSize.x;
            rt.height = rtSize.y;
            rt.format = format;
            rt.Create();
        }
        if (targetShowMat) targetShowMat.SetTexture(ShaderIDs._MainTex, rt);
        drawShader.SetTexture(1, ShaderIDs._MainTex, rt);
        drawShader.SetVector("_MainTex_TexelSize", float4(1f / rtSize.x, 1f / rtSize.y, rtSize.x - 0.1f, rtSize.y - 0.1f));
        initColor = EditorGUILayout.ColorField("Initial Color: ", initColor);
        drawShader.SetVector("_InitialColor", new Vector4(initColor.r, initColor.g, initColor.b, initColor.a));
        drawShader.Dispatch(1, Mathf.CeilToInt(rtSize.x / 8f), Mathf.CeilToInt(rtSize.y / 8f), 1);
        for (int i = 0; i < allTextures.Count; ++i)
        {
            var e = allTextures[i];
            EditorGUILayout.BeginHorizontal();
            e.isOpen = EditorGUILayout.Foldout(e.isOpen, "Texture Layer " + i);
            bool remove = GUILayout.Button("Remove", GUILayout.MaxWidth(100));
            EditorGUILayout.EndHorizontal();
            if (remove)
            {
                allTextures.RemoveAt(i);
                i--;
            }
            else
            {

                if (e.isOpen)
                {
                    EditorGUI.indentLevel++;
                    e.targetTexture = EditorGUILayout.ObjectField("Texture: ", e.targetTexture, typeof(Texture), true) as Texture;
                    e.voronoiSample = EditorGUILayout.Toggle("Voronoi Sample: ", e.voronoiSample);
                    e.size = saturate(EditorGUILayout.Vector2Field("Blend Size", saturate(e.size)));
                    e.blendAlpha = EditorGUILayout.Slider("Blend Alpha: ", e.blendAlpha, 0, 1);
                    e.scale = EditorGUILayout.Vector2Field("Tiling Scale: ", e.scale);
                    e.offset = EditorGUILayout.Vector2Field("Tiling Offset: ", e.offset);
                    //Start Blending

                    EditorGUI.indentLevel--;
                }
                allTextures[i] = e;
            }
        }
        foreach (var e in allTextures)
        {
            if (e.targetTexture)
            {
                int2 blendSize = (int2)(e.size * float2(rtSize.x, rtSize.y));
                if (blendSize.x > 0 && blendSize.y > 0)
                {
                    float4 blendTexelSize = float4(1f / (float2)blendSize, (float2)blendSize.xy - 0.1f);
                    float4 offsetScale = float4(floor(e.offset * float2(rtSize.x, rtSize.y) + 0.1f), e.scale);
                    int pass = e.voronoiSample ? 3 : 0;
                    drawShader.SetTexture(pass, ShaderIDs._MainTex, rt);
                    drawShader.SetTexture(pass, "_BlendTex", e.targetTexture);
                    drawShader.SetVector("_OffsetScale", offsetScale);
                    drawShader.SetVector("_BlendTex_TexelSize", blendTexelSize);
                    drawShader.SetFloat("_BlendAlpha", e.blendAlpha);
                    drawShader.Dispatch(pass, Mathf.CeilToInt(blendSize.x / 8f), Mathf.CeilToInt(blendSize.y / 8f), 1);
                }
            }
        }
        if (GUILayout.Button("Add New Texture"))
        {
            allTextures.Add(new TextureSettings
            {
                blendAlpha = 1f,
                voronoiSample = false,
                isOpen = false,
                offset = 0,
                size = 1,
                scale = 1,
                targetTexture = null
            });
        }
        saveIsOpen = EditorGUILayout.Foldout(saveIsOpen, "Save Mode: ");
        if (saveIsOpen)
        {
            EditorGUI.indentLevel++;
            path = EditorGUILayout.TextField("Save Path: ", path);
            saveFormat = (TextureFormat)EditorGUILayout.EnumPopup("Save Format: ", saveFormat);
            if (GUILayout.Button("Save To PNG"))
            {
                Texture2D tex = new Texture2D(rtSize.x, rtSize.y, saveFormat, false, true);
                ComputeBuffer dataBuffer = new ComputeBuffer(rtSize.x * rtSize.y, sizeof(float4));
                drawShader.SetTexture(2, ShaderIDs._MainTex, rt);
                drawShader.SetBuffer(2, "_ColorBuffer", dataBuffer);
                drawShader.Dispatch(2, Mathf.CeilToInt(rtSize.x / 8f), Mathf.CeilToInt(rtSize.y / 8f), 1);
                Color[] colors = new Color[rtSize.x * rtSize.y];
                dataBuffer.GetData(colors);
                tex.SetPixels(colors);
                System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            }
            EditorGUI.indentLevel--;
        }
    }
}
#endif