#if UNITY_EDITOR
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

public class CombineMesh : ScriptableWizard
{
    [MenuItem("MPipeline/Combine Mesh")]
    private static void CreateWizard()
    {
        DisplayWizard<CombineMesh>("Scene Tools", "Create");
    }
    public string combineMeshPath = "Assets/";
    private static Mesh CombineAllMesh(List<MeshFilter> meshes)
    {
        List<Vector3> verts = new List<Vector3>(1000);
        List<Vector3> norms = new List<Vector3>(1000);
        List<Vector4> tans = new List<Vector4>(1000);
        List<Vector2> uv0s = new List<Vector2>(1000);
        List<int> tris = new List<int>(1000);
        float4x4 worldToLocal = meshes[0].transform.worldToLocalMatrix;

        foreach (var i in meshes)
        {
            float4x4 localToWorld = mul(worldToLocal, i.transform.localToWorldMatrix);
            float3x3 localToWorldRot = float3x3(localToWorld.c0.xyz, localToWorld.c1.xyz, localToWorld.c2.xyz);
            Vector3[] vertices = i.sharedMesh.vertices;
            for (int j = 0; j < vertices.Length; ++j)
            {
                vertices[j] = mul(localToWorld, float4(vertices[j], 1)).xyz;
            }
            Vector3[] normals = i.sharedMesh.normals;
            for (int j = 0; j < vertices.Length; ++j)
            {
                normals[j] = mul(localToWorldRot, normals[j]);
            }
            Vector4[] tangents = i.sharedMesh.tangents;
            for (int j = 0; j < vertices.Length; ++j)
            {
                float3 tan = (Vector3)tangents[j];
                float tanW = tangents[j].w;
                tangents[j] = (Vector3)mul(localToWorldRot, tan);
                tangents[j].w = tanW;
            }
            Vector2[] uv0 = i.sharedMesh.uv;
            int[] triangles = i.sharedMesh.triangles;
            for (int j = 0; j < triangles.Length; ++j)
            {
                triangles[j] += verts.Count;
            }
            tris.AddRange(triangles);
            verts.AddRange(vertices);
            norms.AddRange(normals.Length == vertices.Length ? normals : new Vector3[vertices.Length]);
            tans.AddRange(tangents.Length == vertices.Length ? tangents : new Vector4[vertices.Length]);
            uv0s.AddRange(uv0.Length == vertices.Length ? uv0 : new Vector2[vertices.Length]);
        }
        Mesh newMesh = new Mesh();
        newMesh.SetVertices(verts);
        newMesh.SetUVs(0, uv0s);
        newMesh.SetNormals(norms);
        newMesh.SetTangents(tans);
        newMesh.SetTriangles(tris, 0);
        Unwrapping.GenerateSecondaryUVSet(newMesh);
        return newMesh;
    }
    private void OnWizardCreate()
    {
        Transform[] transes = Selection.GetTransforms(SelectionMode.Unfiltered);
        List<MeshFilter> renderers = new List<MeshFilter>();
        foreach (var i in transes)
        {
            renderers.AddRange(i.GetComponentsInChildren<MeshFilter>());
        }
        if (renderers.Count == 0) return;
        Mesh combinedMesh = CombineAllMesh(renderers);
        AssetDatabase.CreateAsset(combinedMesh, combineMeshPath + combinedMesh.GetInstanceID() + ".asset");
        renderers[0].sharedMesh = combinedMesh;
        for (int i = 1; i < renderers.Count; ++i)
        {
            DestroyImmediate(renderers[i].gameObject);
        }
    }
}

public class ColliderClear : ScriptableWizard
{
    [MenuItem("MPipeline/Collider Cleaner")]
    private static void CreateWizard()
    {
        DisplayWizard<ColliderClear>("Collider", "Clean");
    }
    private void OnWizardCreate()
    {
        Transform[] trans = Selection.GetTransforms(SelectionMode.Unfiltered);
        foreach (var t in trans)
        {
            Collider[] cs = t.GetComponentsInChildren<Collider>();
            foreach (var c in cs)
            {
                DestroyImmediate(c);
            }
        }
    }
}

public class TileClear : ScriptableWizard
{
    [MenuItem("MPipeline/Clear Scene Tile")]
    private static void CreateWizard()
    {
        DisplayWizard<TileClear>("Tile", "Clean");
    }
    private void OnWizardCreate()
    {
        MeshRenderer[] allRenderers = FindObjectsOfType<MeshRenderer>();
        foreach (var i in allRenderers)
        {
            Material[] mats = i.sharedMaterials;
            foreach (var j in mats)
            {
                j.SetTextureOffset(ShaderIDs._MainTex, Vector2.zero);
                j.SetTextureScale(ShaderIDs._MainTex, Vector2.one);
            }
        }
    }
}


public class ColliderHelper : EditorWindow
{
    [MenuItem("MPipeline/Collider Helper")]
    private static void CreateWizard()
    {
        ColliderHelper window = (ColliderHelper)GetWindow(typeof(ColliderHelper));
        window.Show();
    }
    [SerializeField]
    private Transform parent;
    private void OnGUI()
    {
        parent = (Transform)EditorGUILayout.ObjectField("Parent", parent, typeof(Transform), true);

        Transform[] trans = Selection.GetTransforms(SelectionMode.Unfiltered);
        if (GUILayout.Button("Disable Without"))
        {
            Transform[] ts = parent.GetComponentsInChildren<Transform>(true);
            foreach (var i in ts)
            {
                i.gameObject.SetActive(false);
            }
            foreach (var i in trans)
            {
                i.gameObject.SetActive(true);
            }
            parent.gameObject.SetActive(true);
        }
        if (GUILayout.Button("Enable All"))
        {
            Transform[] ts = parent.GetComponentsInChildren<Transform>(true);
            foreach (var i in ts)
            {
                i.gameObject.SetActive(true);
            }
            parent.gameObject.SetActive(true);
        }
    }
}

public unsafe sealed class GenerateTex2DArray : ScriptableWizard
{
    public enum Resolution
    {
        x128 = 128,
        x256 = 256,
        x512 = 512,
        x1024 = 1024,
        x2048 = 2048
    };
    public enum TextureType
    {
        Color,
        Normal,
        HDR
    }
    public string path = "Assets/Textures/";
    [System.Serializable]
    public struct GenerateSettings
    {
        public string fileName;
        public Texture2D[] allTextures;
        public Resolution targetResolution;
        public TextureType textureType;
    }
    public GenerateSettings[] allSettings;
    [MenuItem("MPipeline/Generate Texture Array")]
    private static void CreateWizard()
    {
        DisplayWizard<GenerateTex2DArray>("Tex Array Tools", "Generate");
    }

    private void OnWizardCreate()
    {
        ComputeShader shader = Resources.Load<ComputeShader>("ReadRTData");
        foreach (var i in allSettings)
        {
            int res = (int)i.targetResolution;
            ComputeBuffer buffer = new ComputeBuffer(res * res * i.allTextures.Length, sizeof(float4));
            int pass = i.textureType == TextureType.Normal ? 2 : 1;
            shader.SetBuffer(pass, "_TextureDatas", buffer);
            shader.SetInt("_Width", res);
            shader.SetInt("_Height", res);
            int offst = 0;
            foreach (var tx in i.allTextures)
            {
                shader.SetTexture(pass, "_TargetTexture", tx);
                shader.SetInt("_Offset", offst);
                offst++;
                shader.Dispatch(pass, res / 8, res / 8, 1);
            }

            Texture2DArray tex;
            switch (i.textureType)
            {
                case TextureType.Color:
                    tex = new Texture2DArray(res, res, i.allTextures.Length, TextureFormat.ARGB32, false, true);
                    break;
                case TextureType.HDR:
                    tex = new Texture2DArray(res, res, i.allTextures.Length, TextureFormat.RGBAHalf, false, true);
                    break;
                default:
                    tex = new Texture2DArray(res, res, i.allTextures.Length, TextureFormat.RGHalf, false, true);
                    break;
            }

            Color[] allCols = new Color[res * res];
            for (int a = 0; a < i.allTextures.Length; ++a)
            {
                buffer.GetData(allCols, 0, allCols.Length * a, allCols.Length);
                tex.SetPixels(allCols, a);
            }

            buffer.Dispose();

            if (path[path.Length - 1] != '/' && path[path.Length - 1] != '\\')
            {
                path += '/';
            }
            AssetDatabase.CreateAsset(tex, path + i.fileName + ".asset");
        }
    }
}
public class TextureToSMO : ScriptableWizard
{
    [MenuItem("MPipeline/Generate SMO Texture")]
    private static void CreateWizard()
    {
        DisplayWizard<TextureToSMO>("SMO", "Generate");
    }
    public List<Material> allStandardMats;
    private void OnWizardCreate()
    {
        string[] allPaths = new string[allStandardMats.Count];
        Material mat = new Material(Shader.Find("Hidden/ToSMO"));
        Texture2D whiteTex = new Texture2D(1, 1, TextureFormat.ARGB32, false, true);
        whiteTex.SetPixel(0, 0, Color.white);
        whiteTex.Apply();
        for (int a = 0; a < allStandardMats.Count; ++a)
        {
            var i = allStandardMats[a];
            Texture metallicTex = i.GetTexture("_MetallicGlossMap");
            metallicTex = metallicTex ? metallicTex : whiteTex;
            mat.SetTexture("_MetallicTexture", metallicTex);
            Vector2Int size = new Vector2Int(metallicTex.width, metallicTex.height);
            Texture occlusionTex = i.GetTexture("_OcclusionMap");
            occlusionTex = occlusionTex ? occlusionTex : whiteTex;
            mat.SetTexture("_OcclusionTexture", occlusionTex);
            size = new Vector2Int(max(occlusionTex.width, size.x), max(occlusionTex.height, size.y));
            RenderTexture rt = RenderTexture.GetTemporary(size.x, size.y, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Graphics.Blit(null, rt, mat, 0);
            RenderTexture.active = rt;
            Texture2D resultTex = new Texture2D(size.x, size.y, TextureFormat.ARGB32, false, true);
            resultTex.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
            if (metallicTex || occlusionTex)
            {
                string path;
                if (metallicTex)
                {
                    path = AssetDatabase.GetAssetPath(metallicTex);
                }
                else if (occlusionTex)
                {
                    path = AssetDatabase.GetAssetPath(occlusionTex);
                }
                else
                {
                    path = "Assets/";
                }
                path += "_GeneratedSMO.png";
                File.WriteAllBytes(path, resultTex.EncodeToPNG());
                allPaths[a] = path;
            }
            else
                allPaths[a] = null;
            RenderTexture.ReleaseTemporary(rt);
        }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Shader shader = Shader.Find("ShouShouPBR");
        for (int a = 0; a < allStandardMats.Count; ++a)
        {
            var i = allStandardMats[a];
            i.SetTexture("_MetallicTexture", null);
            i.SetTexture("_OcclusionTexture", null);
            i.shader = shader;
            if (allPaths[a] != null)
            {
                Texture tex = AssetDatabase.LoadAssetAtPath(allPaths[a], typeof(Texture)) as Texture;
                i.SetTexture("_SpecularMap", tex);
            }
        }
    }
}

public unsafe class GenerateDecal : ScriptableWizard
{
    public enum AtlasType
    {
        Color, Normal, HDR
    }
    public string path = "Assets/Textures/";
    public int2 atlasSize = int2(2048, 2048);
    public int2 targetSize = int2(1024, 1024);
    [System.Serializable]
    public class AtlasSettings
    {
        public string name = "_";
        public AtlasType atlasType = AtlasType.Color;
        public List<Texture2D> allTextures = new List<Texture2D>(16);
    }
    private static RenderTextureFormat GetFormat(AtlasType type)
    {
        switch (type)
        {
            case AtlasType.Color:
                return RenderTextureFormat.ARGB32;
            case AtlasType.HDR:
                return RenderTextureFormat.ARGBHalf;
            default:
                return RenderTextureFormat.RGHalf;
        }
    }

    private static TextureFormat GetTexFormat(AtlasType type)
    {
        switch (type)
        {
            case AtlasType.Color:
                return TextureFormat.ARGB32;
            case AtlasType.HDR:
                return TextureFormat.RGBAHalf;
            default:
                return TextureFormat.RGHalf;
        }
    }
    public List<AtlasSettings> allSettings = new List<AtlasSettings>(3);
    [MenuItem("MPipeline/Generate Atlas")]
    private static void CreateWizard()
    {
        DisplayWizard<GenerateDecal>("Atlas", "Generate");
    }

    private void OnWizardCreate()
    {
        ComputeShader shader = Resources.Load<ComputeShader>("ReadRTData");
        ComputeBuffer dataBuffer = new ComputeBuffer(atlasSize.x * atlasSize.y, sizeof(float4));
        Color[] colorArr = new Color[dataBuffer.count];
        int2 rate = atlasSize / targetSize;
        int maxCount = rate.x * rate.y;
        foreach (var i in allSettings)
        {
            RenderTexture currentAtlas = new RenderTexture(new RenderTextureDescriptor
            {
                colorFormat = GetFormat(i.atlasType),
                dimension = UnityEngine.Rendering.TextureDimension.Tex2D,
                enableRandomWrite = true,
                width = atlasSize.x,
                height = atlasSize.y,
                volumeDepth = 1,
                msaaSamples = 1
            });
            currentAtlas.Create();
            int count = min(maxCount, i.allTextures.Count);
            int pass = i.atlasType == AtlasType.Normal ? 4 : 3;
            shader.SetTexture(pass, "_TargetRT", currentAtlas);
            for (int a = 0; a < count; ++a)
            {
                int2 currentPos = int2(a / rate.x, a % rate.x);
                shader.SetInt("_Width", currentPos.x * targetSize.x);
                shader.SetInt("_Height", currentPos.y * targetSize.y);
                shader.SetTexture(pass, "_TargetTexture", i.allTextures[a]);
                shader.Dispatch(pass, targetSize.x / 8, targetSize.y / 8, 1);
            }
            shader.SetInt("_Width", atlasSize.x);
            shader.SetInt("_Height", atlasSize.y);
            shader.SetTexture(0, "_TargetTexture", currentAtlas);
            shader.SetBuffer(0, "_TextureDatas", dataBuffer);
            shader.Dispatch(0, atlasSize.x / 8, atlasSize.y / 8, 1);
            dataBuffer.GetData(colorArr);
            Texture2D tex = new Texture2D(atlasSize.x, atlasSize.y, GetTexFormat(i.atlasType), false, true);
            tex.SetPixels(colorArr);
            AssetDatabase.CreateAsset(tex, path + i.name + ".asset");
            currentAtlas.Release();
            DestroyImmediate(currentAtlas);
        }
        dataBuffer.Dispose();
        Resources.UnloadAsset(shader);
    }
}

public unsafe class Generate3DLut : ScriptableWizard
{
    public Texture2D lut2D;
    [MenuItem("MPipeline/Generate 3D Lut")]
    private static void CreateWizard()
    {
        DisplayWizard<Generate3DLut>("Lut", "Generate");
    }

    private void OnWizardCreate()
    {
        int volumeSize = lut2D.width / lut2D.height;
        Texture3D tarTex = new Texture3D(lut2D.height, lut2D.height, volumeSize, TextureFormat.ARGB32, false);
        tarTex.wrapMode = TextureWrapMode.Clamp;
        for (int z = 0; z < volumeSize; ++z)
        {
            int widthOffset = z * lut2D.height;
            for (int x = 0; x < lut2D.height; ++x)
            {
                for (int y = 0; y < lut2D.height; ++y)
                {
                    tarTex.SetPixel(x, y, z, lut2D.GetPixel(x + widthOffset, y, 0));
                }
            }
        }
        string originPath = AssetDatabase.GetAssetPath(lut2D);
        AssetDatabase.CreateAsset(tarTex, originPath + "_3DLut.asset");
    }
}

public unsafe class TransformTextureSettings : ScriptableWizard
{
    public Texture2D[] allTextures;
    public bool useTrilinear = true;
    [MenuItem("MPipeline/PBR Texture")]
    private static void CreateWizard()
    {
        DisplayWizard<TransformTextureSettings>("PBR Texture", "Transform");
    }
    private void OnWizardCreate()
    {
        foreach (var i in allTextures)
        {
            TextureImporter imp = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(i)) as TextureImporter;
            if (useTrilinear)
            {
                imp.mipmapEnabled = true;
                imp.filterMode = FilterMode.Trilinear;
            }
            else
            {
                imp.mipmapEnabled = false;
                imp.filterMode = FilterMode.Bilinear;
            }
 
            if (i.name.ToLower().Contains("_smo"))
            {
                imp.sRGBTexture = false;
                
            }
            else if (i.name.ToLower().Contains("_normal"))
            {
                imp.textureType = TextureImporterType.NormalMap;
            }
            else if(i.name.ToLower().Contains("_height"))
            {
                imp.sRGBTexture = false;
            }
            else
            {
                imp.sRGBTexture = true;
            }
            imp.SaveAndReimport();
        }
    }
}
#endif
