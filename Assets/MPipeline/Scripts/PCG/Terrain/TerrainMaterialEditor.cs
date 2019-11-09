#if UNITY_EDITOR
using System.Collections;
using UnityEditor;
using UnityEngine.AddressableAssets;
using System.Collections.Generic;
using UnityEngine.Experimental.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Mathematics;
using System.IO;
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
            public bool isOpen;
            public Texture albedo;
            public Texture normal;
            public Texture smo;
            public override bool Equals(object obj)
            {
                var another = (TexturePack)obj;
                return another.albedo == albedo &&
                    another.normal == normal &&
                    another.smo == smo;
            }

            public override int GetHashCode()
            {
                return albedo.GetHashCode() ^ normal.GetHashCode() ^ smo.GetHashCode();
            }
        }
        [System.Serializable]
        public struct MaterialInfo
        {
            public string name;
            public bool foldOut;
        }
        [System.Serializable]
        public struct BlendWeight
        {
            public float offset;
            public float heightBlendScale;
        }
        [System.Serializable]
        public struct BlendMaterial
        {
            public TexturePack frontPack;
            public TexturePack backPack;
            public List<BlendWeight> blendWeights;
            public bool isOpen;
        }
        [HideInInspector]
        public List<BlendMaterial> allMaterials = new List<BlendMaterial>(10);
        [HideInInspector]
        public List<MaterialInfo> allMaterialsInfos = new List<MaterialInfo>();
        [HideInInspector]
        public MTerrainData targetData;
        [HideInInspector]
        public int2 renderingMaterial;
        private RenderTexture drawAlbedoRT;
        private RenderTexture drawNormalRT;
        private RenderTexture drawSMORT;
        private RenderTargetIdentifier[] colorBuffersIdentifier;
        private RenderBuffer[] colorBuffers;
        private Material heightBlendMaterial;
        public string savePath;
        private void OnEnable()
        {
            drawAlbedoRT = new RenderTexture(2048, 2048, 0, GraphicsFormat.R32G32B32A32_SFloat, 0);
            drawNormalRT = new RenderTexture(2048, 2048, 0, GraphicsFormat.R32G32B32A32_SFloat, 0);
            drawSMORT = new RenderTexture(2048, 2048, 0, GraphicsFormat.R32G32B32A32_SFloat, 0);
            drawAlbedoRT.wrapMode = TextureWrapMode.Repeat;
            drawNormalRT.wrapMode = TextureWrapMode.Repeat;
            drawSMORT.wrapMode = TextureWrapMode.Repeat;
            drawAlbedoRT.Create();
            drawNormalRT.Create();
            drawSMORT.Create();
            heightBlendMaterial = new Material(Shader.Find("Hidden/TerrainMaterialEdit"));
            colorBuffersIdentifier = new RenderTargetIdentifier[]
            {
                drawAlbedoRT.colorBuffer,
                drawNormalRT.colorBuffer,
                drawSMORT.colorBuffer
            };
            colorBuffers = new RenderBuffer[]
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
            bf.SetRenderTarget(colors: colorBuffersIdentifier, depth: drawAlbedoRT.depthBuffer);
            bf.ClearRenderTarget(false, true, Color.black);
            if (renderingMaterial.x < 0 || renderingMaterial.x >= allMaterials.Count) return;
            if (allMaterials.Count == 0) return;
            var heightBlend = allMaterials[renderingMaterial.x];
            if (renderingMaterial.y < 0 || renderingMaterial.y >= heightBlend.blendWeights.Count) return;
            heightBlendMaterial.SetVector("_Setting", float4(heightBlend.blendWeights[renderingMaterial.y].offset, heightBlend.blendWeights[renderingMaterial.y].heightBlendScale, 1, 1));

            heightBlendMaterial.SetTexture("_Albedo0", heightBlend.frontPack.albedo);
            heightBlendMaterial.SetTexture("_Albedo1", heightBlend.backPack.albedo);
            heightBlendMaterial.SetTexture("_Normal0", heightBlend.frontPack.normal);
            heightBlendMaterial.SetTexture("_Normal1", heightBlend.backPack.normal);
            heightBlendMaterial.SetTexture("_SMO0", heightBlend.frontPack.smo);
            heightBlendMaterial.SetTexture("_SMO1", heightBlend.backPack.smo);
            bf.DrawMesh(GraphicsUtility.mesh, Matrix4x4.identity, heightBlendMaterial, 0, 0);
            mat.SetTexture(ShaderIDs._MainTex, drawAlbedoRT);
            mat.SetTexture(ShaderIDs._BumpMap, drawNormalRT);
            mat.SetTexture("_SpecularMap", drawSMORT);
            mat.SetColor(ShaderIDs._Color, Color.white);
            mat.SetFloat("_Glossiness", 1);
            mat.SetFloat("_Occlusion", 1);
            mat.SetFloat("_MetallicIntensity", 1);
        }

        public void DrawToMaterialImmidietly()
        {
            Material mat = GetComponent<MeshRenderer>().sharedMaterial;
            if (!mat) return;
            if (!drawAlbedoRT || !drawNormalRT || !drawSMORT) return;
            Graphics.SetRenderTarget(colorBuffers, drawAlbedoRT.depthBuffer);
            GL.Clear(false, true, Color.black);
            if (renderingMaterial.x < 0 || renderingMaterial.x >= allMaterials.Count) return;
            if (allMaterials.Count == 0) return;
            var heightBlend = allMaterials[renderingMaterial.x];
            if (renderingMaterial.y < 0 || renderingMaterial.y >= heightBlend.blendWeights.Count) return;
            heightBlendMaterial.SetVector("_Setting", float4(heightBlend.blendWeights[renderingMaterial.y].offset, heightBlend.blendWeights[renderingMaterial.y].heightBlendScale, 1, 1));
            heightBlendMaterial.SetTexture("_Albedo0", heightBlend.frontPack.albedo);
            heightBlendMaterial.SetTexture("_Albedo1", heightBlend.backPack.albedo);
            heightBlendMaterial.SetTexture("_Normal0", heightBlend.frontPack.normal);
            heightBlendMaterial.SetTexture("_Normal1", heightBlend.backPack.normal);
            heightBlendMaterial.SetTexture("_SMO0", heightBlend.frontPack.smo);
            heightBlendMaterial.SetTexture("_SMO1", heightBlend.backPack.smo);
            heightBlendMaterial.SetPass(0);
            Graphics.DrawMeshNow(GraphicsUtility.mesh, Matrix4x4.identity);
            mat.SetTexture(ShaderIDs._MainTex, drawAlbedoRT);
            mat.SetTexture(ShaderIDs._BumpMap, drawNormalRT);
            mat.SetTexture("_SpecularMap", drawSMORT);
            mat.SetColor(ShaderIDs._Color, Color.white);
            mat.SetFloat("_Glossiness", 1);
            mat.SetFloat("_Occlusion", 1);
            mat.SetFloat("_MetallicIntensity", 1);
        }

        public bool SaveFile(int2 index, string targetDirectory)
        {
            renderingMaterial = index;
            DrawToMaterialImmidietly();
            return MEditorLib.GenerateWorldCreatorTexture(drawAlbedoRT, drawNormalRT, targetDirectory + "/", "TestPic");
        }

        public void SaveAllFile()
        {
            string[] dircts = Directory.GetDirectories(savePath);
            int offset = 0;
            for(int i = 0; i < allMaterials.Count; ++i)
            {
                for (int a = 0; a < allMaterials[i].blendWeights.Count; ++a)
                {
                    if (!SaveFile(int2(i, a), dircts[offset]))
                        --a;
                    offset++;
                    if (offset >= dircts.Length) return;
                }

            }
        }

        public bool CheckResources()
        {
            for (int i = 0; i < allMaterials.Count; ++i)
            {
                var a = allMaterials[i];
                if (!a.frontPack.albedo || !a.frontPack.normal || !a.frontPack.smo ||
                    !a.backPack.albedo || !a.backPack.normal || !a.backPack.smo)
                {
                    Debug.LogError("Group " + i + " has NULL");
                    return false;
                }
            }
            return true;
        }

        public void SaveData()
        {
            Dictionary<TexturePack, int> allContainedTexturePack = new Dictionary<TexturePack, int>(32);
            int count = 0;
            int GetValue(TexturePack pack)
            {
                int result;
                if (allContainedTexturePack.TryGetValue(pack, out result))
                    return result;
                result = count;
                count++;
                allContainedTexturePack.Add(pack, result);
                return result;
            }
            List<MTerrainData.HeightBlendMaterial> allMats = new List<MTerrainData.HeightBlendMaterial>(allContainedTexturePack.Count);
            foreach (var i in allMaterials)
            {
                MTerrainData.HeightBlendMaterial hb = new MTerrainData.HeightBlendMaterial
                {
                    firstMaterialIndex = GetValue(i.frontPack),
                    secondMaterialIndex = GetValue(i.backPack),
                };
                foreach (var j in i.blendWeights)
                {
                    hb.heightBlendScale = j.heightBlendScale;
                    hb.offset = j.offset;
                    allMats.Add(hb);
                }
            }
            targetData.textures = new MTerrain.PBRTexture[allContainedTexturePack.Count];
            foreach (var i in allContainedTexturePack)
            {
                targetData.textures[i.Value] = new MTerrain.PBRTexture
                {
                    albedoOccTex = MEditorLib.SetObjectAddressable(i.Key.albedo),
                    normalTex = MEditorLib.SetObjectAddressable(i.Key.normal),
                    SMTex = MEditorLib.SetObjectAddressable(i.Key.smo)
                };
            }
            targetData.allMaterials = allMats.ToArray();
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
            if (!target.enabled)
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
                        var mat = target.allMaterials[i];
                        info.foldOut = EditorGUILayout.Foldout(info.foldOut, info.name + ": ");
                        if (info.foldOut)
                        {
                            EditorGUI.indentLevel++;
                            info.name = EditorGUILayout.TextField("Name: ", info.name);
                            mat.frontPack.isOpen = EditorGUILayout.Foldout(mat.frontPack.isOpen, "Front Pack: ");
                            if (mat.frontPack.isOpen)
                            {
                                EditorGUI.indentLevel++;
                                mat.frontPack.albedo = EditorGUILayout.ObjectField("Front Albedo Textures: ", mat.frontPack.albedo, typeof(Texture), false) as Texture;
                                mat.frontPack.normal = EditorGUILayout.ObjectField("Front Normal Textures: ", mat.frontPack.normal, typeof(Texture), false) as Texture;
                                mat.frontPack.smo = EditorGUILayout.ObjectField("Front SMO Textures: ", mat.frontPack.smo, typeof(Texture), false) as Texture;
                                EditorGUI.indentLevel--;
                            }
                            mat.backPack.isOpen = EditorGUILayout.Foldout(mat.backPack.isOpen, "Back Pack: ");
                            if (mat.backPack.isOpen)
                            {
                                EditorGUI.indentLevel++;
                                mat.backPack.albedo = EditorGUILayout.ObjectField("Back Albedo Textures: ", mat.backPack.albedo, typeof(Texture), false) as Texture;
                                mat.backPack.normal = EditorGUILayout.ObjectField("Back Normal Textures: ", mat.backPack.normal, typeof(Texture), false) as Texture;
                                mat.backPack.smo = EditorGUILayout.ObjectField("Back SMO Textures: ", mat.backPack.smo, typeof(Texture), false) as Texture;
                                EditorGUI.indentLevel--;
                            }
                            mat.isOpen = EditorGUILayout.Foldout(mat.isOpen, "Blend Weights: ");
                            if (mat.isOpen)
                            {
                                EditorGUI.indentLevel++;
                                int size = mat.blendWeights.Count;
                                size = EditorGUILayout.IntSlider("Element Count: ", size, 1, 8);
                                if (size > mat.blendWeights.Count)
                                {
                                    for (int a = mat.blendWeights.Count; a < size; ++a)
                                    {
                                        mat.blendWeights.Add(mat.blendWeights[a - 1]);
                                    }
                                }
                                else if (size < mat.blendWeights.Count)
                                {
                                    for (int a = size; a < mat.blendWeights.Count; a++)
                                    {
                                        mat.blendWeights.RemoveAt(mat.blendWeights.Count - 1);
                                    }
                                }
                                for (int a = 0; a < mat.blendWeights.Count; ++a)
                                {
                                    EditorGUILayout.LabelField("Element " + a + ": ");
                                    EditorGUI.indentLevel++;
                                    var weight = mat.blendWeights[a];
                                    weight.offset = EditorGUILayout.Slider("Offset: ", weight.offset, -1, 1);
                                    weight.heightBlendScale = EditorGUILayout.Slider("Blend Scale: ", weight.heightBlendScale, -20, 20);
                                    mat.blendWeights[a] = weight;
                                    EditorGUILayout.BeginHorizontal();
                                    bool2 equal = target.renderingMaterial == int2(i, a);
                                    if (!equal.x || !equal.y)
                                    {
                                        if (GUILayout.Button("Render This"))
                                        {
                                            target.renderingMaterial = int2(i, a);
                                        }
                                    }
                                    if (GUILayout.Button("Remove This"))
                                    {
                                        mat.blendWeights.RemoveAt(a);
                                        a--;
                                    }
                                    EditorGUILayout.EndHorizontal();
                                    EditorGUI.indentLevel--;
                                }
                                EditorGUI.indentLevel--;
                            }
                            EditorGUILayout.Space();
                            EditorGUILayout.Space();
                            EditorGUILayout.BeginHorizontal();

                            if (GUILayout.Button("Delete"))
                            {
                                target.allMaterials.RemoveAt(i);
                                target.allMaterialsInfos.RemoveAt(i);
                                i--;
                                EditorGUI.indentLevel--;
                                continue;
                            }
                            if (GUILayout.Button("Insert"))
                            {
                                target.allMaterials.Insert(i + 1, target.allMaterials[i]);
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
                        var targetmat = new TerrainMaterialEditor.BlendMaterial
                        {
                            frontPack = new TerrainMaterialEditor.TexturePack(),
                            backPack = new TerrainMaterialEditor.TexturePack(),
                            blendWeights = new List<TerrainMaterialEditor.BlendWeight>()
                        };
                        targetmat.blendWeights.Add(new TerrainMaterialEditor.BlendWeight
                        {
                            heightBlendScale = 0,
                            offset = 0
                        });
                        target.allMaterials.Add(targetmat);
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
            if (GUILayout.Button("Save Data"))
            {
                if (target.CheckResources())
                    target.SaveData();
            }
            if (GUILayout.Button("Save To World Creator File"))
            {
                target.SaveAllFile();
            }
        }
    }
}
#endif