using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif
namespace MPipeline
{
    public unsafe sealed class Tex3DGenerator : MonoBehaviour
    {
        public enum TexturePreSetFormat
        {
            Color, HDR, Normal
        }
        [System.Serializable]
        public struct TextureSet
        {
            public string idName;
            [System.NonSerialized] public int id;
            public TexturePreSetFormat format;
        }
        public bool unloadAfterBlending = false;
        private Texture2D blackTex;
        private Texture2D whiteTex;
        [SerializeField] private List<Material> targetMaterials = new List<Material>();
        [HideInInspector] public int textureGroupCount = 0;
        [HideInInspector] public List<TextureSet> textureName;
        [HideInInspector] public List<Texture2D> textureContainers;
        private List<RenderTexture> allRTs;
        [HideInInspector] public Vector2Int textureSize = new Vector2Int(1024, 1024);

        public void Init()
        {
            blackTex = new Texture2D(1, 1, TextureFormat.ARGB32, false, true);
            whiteTex = new Texture2D(1, 1, TextureFormat.ARGB32, false, true);
            blackTex.SetPixel(0, 0, Color.black);
            whiteTex.SetPixel(0, 0, Color.white);
            blackTex.Apply();
            whiteTex.Apply();
        }
        private void Start()
        {

            StartCoroutine(AsyncLoadTexture());
        }
        public IEnumerator AsyncLoadTexture()
        {
            Material toNormalMaterial = new Material(Shader.Find("Hidden/NormalToRT"));
            allRTs = new List<RenderTexture>(textureName.Count);
            RenderTextureFormat ToFormat(TexturePreSetFormat ori)
            {
                switch (ori)
                {
                    case TexturePreSetFormat.Color:
                        return RenderTextureFormat.ARGB32;
                    case TexturePreSetFormat.HDR:
                        return RenderTextureFormat.RGB111110Float;
                    default:
                        return RenderTextureFormat.RGHalf;
                }
            }
            for (int i = 0; i < textureName.Count; ++i)
            {
                TextureSet set = textureName[i];
                allRTs.Add(new RenderTexture(new RenderTextureDescriptor
                {
                    colorFormat = ToFormat(set.format),
                    dimension = TextureDimension.Tex3D,
                    width = textureSize.x,
                    height = textureSize.y,
                    volumeDepth = textureContainers.Count / textureName.Count,
                    msaaSamples = 1
                }));
                allRTs[i].filterMode = FilterMode.Bilinear;
                allRTs[i].wrapMode = TextureWrapMode.Repeat;
                allRTs[i].Create();
                set.id = Shader.PropertyToID(set.idName);
                textureName[i] = set;
                yield return null;
            }
            yield return null;
            for (int i = 0, ele = 0; i < textureContainers.Count; ele++)
            {
                for (int j = 0; j < textureName.Count; ++j, ++i)
                {
                    if (allRTs[j].format != RenderTextureFormat.RGHalf)
                    {
                        if (!textureContainers[i])
                            Graphics.Blit(whiteTex, allRTs[j], 0, ele);
                        else
                            Graphics.Blit(textureContainers[i], allRTs[j], 0, ele);
                    }
                    else
                    {
                        if (!textureContainers[i])
                            Graphics.Blit(blackTex, allRTs[j], 0, ele);
                        else
                            Graphics.Blit(textureContainers[i], allRTs[j], toNormalMaterial, 0, ele);
                    }
                    yield return null;
                    if (unloadAfterBlending)
                        Resources.UnloadAsset(textureContainers[i]);
                }
            }
            yield return null;
            int _Tex3DSize = Shader.PropertyToID("_Tex3DSize");
            foreach (var mat in targetMaterials)
            {
                for (int i = 0; i < textureName.Count; ++i)
                {
                    mat.SetTexture(textureName[i].id, allRTs[i]);
                    mat.SetFloat(_Tex3DSize, allRTs[i].volumeDepth);
                }
                yield return null;
            }
            DestroyImmediate(toNormalMaterial);
        }
        public void OnDestroy()
        {
            if (allRTs != null)
            {
                foreach (var i in allRTs)
                {
                    if (i) DestroyImmediate(i);
                }
            }
            if (blackTex) DestroyImmediate(blackTex);
            if (whiteTex) DestroyImmediate(whiteTex);
        }
    }
#if UNITY_EDITOR
    [CustomEditor(typeof(Tex3DGenerator))]
    public class Tex3DGeneratorEditor : Editor
    {
        bool openTextureSet;
        bool openTextureContainers;

        public override void OnInspectorGUI()
        {
            Tex3DGenerator tar = serializedObject.targetObject as Tex3DGenerator;
            Undo.RecordObject(this, "InspectorTex3D");
            base.OnInspectorGUI();
            EditorGUILayout.Space();
            tar.textureSize = EditorGUILayout.Vector2IntField("Texture Size: ", tar.textureSize);
            openTextureSet = EditorGUILayout.Foldout(openTextureSet, "Tex3D Settings: ");
            if (openTextureSet)
            {
                EditorGUI.indentLevel++;
                for (int i = 0; i < tar.textureName.Count; ++i)
                {
                    EditorGUILayout.LabelField("Texture Element " + i + ": ");
                    EditorGUI.indentLevel++;
                    var set = tar.textureName[i];
                    set.idName = EditorGUILayout.TextField("Tex Name: ", set.idName);
                    set.format = (Tex3DGenerator.TexturePreSetFormat)EditorGUILayout.EnumPopup("Tex Format: ", set.format);
                    tar.textureName[i] = set;
                    if (GUILayout.Button("Remove"))
                    {
                        tar.textureName.RemoveAt(i);
                        i--;
                    }
                    EditorGUI.indentLevel--;
                }
                EditorGUI.indentLevel--;
                if (GUILayout.Button("Add Texture Sample"))
                {
                    tar.textureName.Add(new Tex3DGenerator.TextureSet
                    {
                        format = Tex3DGenerator.TexturePreSetFormat.Color,
                        idName = "_MainTex"
                    });
                }
            }
            EditorGUILayout.Space();
            openTextureContainers = EditorGUILayout.Foldout(openTextureContainers, "Tex Samples: ");
            if (tar.textureContainers.Count < tar.textureGroupCount * tar.textureName.Count)
            {
                int ct = tar.textureGroupCount * tar.textureName.Count - tar.textureContainers.Count;
                for (int i = 0; i < ct; ++i)
                {
                    tar.textureContainers.Add(null);
                }
            }
            else
            {
                int ct = tar.textureContainers.Count - tar.textureGroupCount * tar.textureName.Count;
                for (int i = 0; i < ct; ++i)
                {
                    tar.textureContainers.RemoveAt(tar.textureContainers.Count - 1);
                }
            }
            if (openTextureContainers)
            {
                EditorGUI.indentLevel++;
                for (int i = 0, ele = 0; i < tar.textureContainers.Count; ele++)
                {
                    EditorGUILayout.LabelField("Texture Group " + ele + ": ");
                    EditorGUI.indentLevel++;
                    int startIndex = i;
                    for (int j = 0; j < tar.textureName.Count; ++j, ++i)
                    {
                        tar.textureContainers[i] = EditorGUILayout.ObjectField(tar.textureName[j].idName + ": ", tar.textureContainers[i], typeof(Texture2D), false) as Texture2D;
                    }
                    if (GUILayout.Button("Remove"))
                    {
                        tar.textureContainers.RemoveRange(startIndex, tar.textureName.Count);
                        tar.textureGroupCount--;
                    }
                    EditorGUI.indentLevel--;
                }
                if (GUILayout.Button("Add Texture Group"))
                {
                    tar.textureGroupCount++;
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.Space();
            if (GUILayout.Button("Load In Editor"))
            {
                tar.OnDestroy();
                tar.Init();
                var async = tar.AsyncLoadTexture();
                while (async.MoveNext()) { }
            }
        }
    }
#endif
}