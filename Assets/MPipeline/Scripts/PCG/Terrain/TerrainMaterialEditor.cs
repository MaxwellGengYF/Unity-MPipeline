#if UNITY_EDITOR
using System.Collections;
using UnityEditor;
using UnityEngine.AddressableAssets;
using System.Collections.Generic;
using UnityEngine.Experimental.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Mathematics;
using static Unity.Mathematics.math;
namespace MPipeline
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed unsafe class TerrainMaterialEditor : MonoBehaviour
    {
        [System.Serializable]
        public struct TexturePack
        {
            public Texture albedo;
            public Texture normal;
            public Texture smo;
        }
        [System.Serializable]
        public struct MaterialInfo
        {
            public string name;
            public bool foldOut;
        }
        public List<TexturePack> allTextures = new List<TexturePack>();
        [HideInInspector]
        public List<MTerrainData.HeightBlendMaterial> allMaterials = new List<MTerrainData.HeightBlendMaterial>();
        [HideInInspector]
        public List<MaterialInfo> allMaterialsInfos = new List<MaterialInfo>();
        [HideInInspector]
        public MTerrainData targetData;
        [HideInInspector]
        public int renderingMaterial;
        private RenderTexture drawAlbedoRT;
        private RenderTexture drawNormalRT;
        private RenderTexture drawSMORT;
        private RenderTargetIdentifier[] colorBuffers;
        private Material heightBlendMaterial;
        private void OnEnable()
        {
            drawAlbedoRT = new RenderTexture(2048, 2048, 0, GraphicsFormat.R32G32B32A32_SFloat, 0);
            drawNormalRT = new RenderTexture(2048, 2048, 0, GraphicsFormat.R32G32_SFloat, 0);
            drawSMORT = new RenderTexture(2048, 2048, 0, GraphicsFormat.R32G32B32A32_SFloat, 0);
            drawAlbedoRT.wrapMode = TextureWrapMode.Repeat;
            drawNormalRT.wrapMode = TextureWrapMode.Repeat;
            drawSMORT.wrapMode = TextureWrapMode.Repeat;
            drawAlbedoRT.Create();
            drawNormalRT.Create();
            drawSMORT.Create();
            heightBlendMaterial = new Material(Shader.Find("Hidden/TerrainMaterialEdit"));
            colorBuffers = new RenderTargetIdentifier[]
            {
                drawAlbedoRT.colorBuffer,
                drawNormalRT.colorBuffer,
                drawSMORT.colorBuffer
            };
        }

        private void OnDisable()
        {
            DestroyImmediate(heightBlendMaterial);
            DestroyImmediate(drawAlbedoRT);
            DestroyImmediate(drawNormalRT);
            DestroyImmediate(drawSMORT);
        }
        public void DrawToMaterial()
        {
            Material mat = GetComponent<MeshRenderer>().sharedMaterial;
            if (!mat) return;
            CommandBuffer bf = RenderPipeline.BeforeFrameBuffer;
            if (!drawAlbedoRT || !drawNormalRT || !drawSMORT) return;
            bf.SetRenderTarget(colors: colorBuffers, depth: drawAlbedoRT.depthBuffer);
            bf.ClearRenderTarget(false, true, Color.black);
            if (renderingMaterial < 0 || renderingMaterial >= allMaterials.Count) return;
            if (allTextures.Count == 0) return;
            var heightBlend = allMaterials[renderingMaterial];
            heightBlendMaterial.SetVector("_Setting", *(float4*)heightBlend.Ptr());
            TexturePack firstPack = allTextures[Mathf.Clamp((int)heightBlend.firstMaterialIndex, 0, allTextures.Count - 1)];
            TexturePack secondPack = allTextures[Mathf.Clamp((int)heightBlend.secondMaterialIndex, 0, allTextures.Count - 1)];
            heightBlendMaterial.SetTexture("_Albedo0", firstPack.albedo);
            heightBlendMaterial.SetTexture("_Albedo1", secondPack.albedo);
            heightBlendMaterial.SetTexture("_Normal0", firstPack.normal);
            heightBlendMaterial.SetTexture("_Normal1", secondPack.normal);
            heightBlendMaterial.SetTexture("_SMO0", firstPack.smo);
            heightBlendMaterial.SetTexture("_SMO1", secondPack.smo);
            bf.DrawMesh(GraphicsUtility.mesh, Matrix4x4.identity, heightBlendMaterial, 0, 0);
            mat.SetTexture(ShaderIDs._MainTex, drawAlbedoRT);
            mat.SetTexture(ShaderIDs._BumpMap, drawNormalRT);
            mat.SetTexture("_SpecularMap", drawSMORT);
            mat.SetColor(ShaderIDs._Color, Color.white);
            mat.SetFloat("_Glossiness", 1);
            mat.SetFloat("_Occlusion", 1);
            mat.SetFloat("_MetallicIntensity", 1);
        }
        public int CheckResources()
        {
            for(int i = 0; i < allTextures.Count; ++i)
            {
                var a = allTextures[i];
                if (!a.albedo || !a.normal || !a.smo)
                {
                    Debug.LogError("Group " + i + " has NULL");
                    return 1;
                }
            }
            foreach (var i in allTextures)
            {
                
                   
            }
            return 0;
        }

        public void SaveData()
        {
            targetData.textures = new MTerrain.PBRTexture[allTextures.Count];

            for (int i = 0; i < allTextures.Count; ++i)
            {
                TexturePack pack = allTextures[i];
                targetData.textures[i] = new MTerrain.PBRTexture
                {
                    albedoOccTex = MEditorLib.SetObjectAddressable(pack.albedo),
                    normalTex = MEditorLib.SetObjectAddressable(pack.normal),
                    SMTex = MEditorLib.SetObjectAddressable(pack.smo)
                };
            }
            targetData.allMaterials = allMaterials.ToArray();
            EditorUtility.SetDirty(targetData);
            AssetDatabase.SaveAssets();
        }
    }
    [CustomEditor(typeof(TerrainMaterialEditor))]
    public sealed class TerrainMaterialEditorWindow : Editor
    {
        bool matFolder;
        private void OnEnable()
        {
            matFolder = false;
        }
        public override void OnInspectorGUI()
        {
            TerrainMaterialEditor target = serializedObject.targetObject as TerrainMaterialEditor;
            Undo.RecordObject(target, target.GetInstanceID().ToString());
            target.targetData = EditorGUILayout.ObjectField("Terrian Data: ", target.targetData, typeof(MTerrainData), false) as MTerrainData;
            if (!target.targetData)
            {
                EditorGUILayout.LabelField("No Avaliable Data!");
                return;
            }
            if(!target.enabled)
            {
                EditorGUILayout.LabelField("Not Enabled!");
                return;
            }
            base.OnInspectorGUI();
            matFolder = EditorGUILayout.Foldout(matFolder, "Materials: ");
            if (matFolder)
            {
                EditorGUI.indentLevel++;
                try
                {
                    if (target.allMaterialsInfos.Count < target.allMaterials.Count)
                    {
                        int len = target.allMaterials.Count - target.allMaterialsInfos.Count;
                        for (int i = 0; i < len; ++i)
                        {
                            target.allMaterialsInfos.Add(new TerrainMaterialEditor.MaterialInfo
                            {
                                name = "New Material",
                                foldOut = false
                            });
                        }
                    }
                    else if (target.allMaterialsInfos.Count > target.allMaterials.Count)
                    {
                        target.allMaterialsInfos.RemoveRange(target.allMaterials.Count, target.allMaterialsInfos.Count - target.allMaterials.Count);
                    }
                    for (int i = 0; i < target.allMaterials.Count; ++i)
                    {
                        TerrainMaterialEditor.MaterialInfo info = target.allMaterialsInfos[i];
                        MTerrainData.HeightBlendMaterial mat = target.allMaterials[i];
                        info.foldOut = EditorGUILayout.Foldout(info.foldOut, info.name + ": ");
                        if (info.foldOut)
                        {
                            EditorGUI.indentLevel++;
                            info.name = EditorGUILayout.TextField("Name: ", info.name);
                            int firstIndex = (int)mat.firstMaterialIndex;
                            int secondIndex = (int)mat.secondMaterialIndex;
                            firstIndex = EditorGUILayout.IntField("First Textures Index: ", firstIndex);
                            secondIndex = EditorGUILayout.IntField("Second Textures Index: ", secondIndex);
                            mat.firstMaterialIndex = firstIndex + 0.2f;
                            mat.secondMaterialIndex = secondIndex + 0.2f;
                            mat.offset = EditorGUILayout.Slider("Offset: ", mat.offset, -2, 2);
                            mat.heightBlendScale = EditorGUILayout.Slider("Height Blend Scale: ", mat.heightBlendScale, -20, 20);
                            EditorGUILayout.BeginHorizontal();
                            if(target.renderingMaterial != i && GUILayout.Button("Render This"))
                            {
                                target.renderingMaterial = i;
                            }
                            if (GUILayout.Button("Delete"))
                            {
                                target.allMaterials.RemoveAt(i);
                                target.allMaterialsInfos.RemoveAt(i);
                                i--;
                                EditorGUI.indentLevel--;
                                continue;
                            }
                            if(GUILayout.Button("Insert"))
                            {
                                target.allMaterials.Insert(i + 1, new MTerrainData.HeightBlendMaterial
                                {
                                    firstMaterialIndex = 0.2f,
                                    secondMaterialIndex = 0.2f,
                                    heightBlendScale = 1,
                                    offset = 0
                                });
                                target.allMaterialsInfos.Insert(i + 1, new TerrainMaterialEditor.MaterialInfo
                                {
                                    name = "New Material",
                                    foldOut = false
                                });
                            }
                            EditorGUILayout.EndHorizontal();
                            EditorGUI.indentLevel--;
                        }
                        target.allMaterialsInfos[i] = info;
                        target.allMaterials[i] = mat;
                    }
                    EditorGUILayout.Space();

                    if (target.allMaterials.Count == 0 && GUILayout.Button("Add"))
                    {
                        target.allMaterials.Add(new MTerrainData.HeightBlendMaterial
                        {
                            firstMaterialIndex = 0.2f,
                            secondMaterialIndex = 0.2f,
                            heightBlendScale = 1,
                            offset = 0
                        });
                        target.allMaterialsInfos.Add(new TerrainMaterialEditor.MaterialInfo
                        {
                            name = "New Material",
                            foldOut = false
                        });
                    }
                }
                catch { }
                EditorGUI.indentLevel--;
            }
            target.DrawToMaterial();
            if(GUILayout.Button("Save Data"))
            {
                if (target.CheckResources() == 0)
                    target.SaveData();
            }
        }
    }
}
#endif