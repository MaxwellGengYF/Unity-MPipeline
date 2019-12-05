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
        public enum Channel
        {
            R, G, B, A
        }
        [System.Serializable]
        public struct VTMaterial
        {
            public float3 albedoColor;
            public float2 normalScale;
            public float smoothness;
            public float metallic;
            public float occlusion;
            public int antiRepeat;
            public bool splatOpen;
            public List<SplatSettings> splatSettings;
        }

        [System.Serializable]
        public struct SplatSettings
        {
            public Texture2D splatMap;
            public int2 splatStartPos;
            public Channel chan;
        }

        [System.Serializable]
        public struct BlendMaterial
        {
            public TexturePack frontPack;
            public List<VTMaterial> blendWeights;
            public bool isOpen;
        }
        public int splatTillingScale = 1;
        [HideInInspector]
        public List<BlendMaterial> allMaterials = new List<BlendMaterial>(10);
        [HideInInspector]
        public List<MaterialInfo> allMaterialsInfos = new List<MaterialInfo>();
        [HideInInspector]
        public MTerrainData targetData;
        [HideInInspector]
        public int2 renderingMaterial;

        public float2 textureTillingSize = 1;
        private RenderTexture drawAlbedoRT;
        private ComputeShader texShader;
        private RenderTexture drawNormalRT;
        private RenderTexture drawSMORT;
        private RenderTargetIdentifier[] colorBuffersIdentifier;
        private RenderBuffer[] colorBuffers;
        private Material heightBlendMaterial;
        private RenderTexture randomTileRT;
        private ComputeShader terrainEditShader;
        public string savePath;
        [HideInInspector]
        public bool matOpen;
        [HideInInspector]
        public bool splatOpen;
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
            terrainEditShader = Resources.Load<ComputeShader>("TerrainEdit");
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
            randomTileRT = new RenderTexture(256, 256, 0, GraphicsFormat.R16G16B16A16_SNorm, 0);
            randomTileRT.enableRandomWrite = true;
            randomTileRT.wrapMode = TextureWrapMode.Repeat;
            randomTileRT.filterMode = FilterMode.Point;
            randomTileRT.Create();
            texShader = Resources.Load<ComputeShader>("ProceduralTexture");
            texShader.SetTexture(6, ShaderIDs._DestTex, randomTileRT);
            texShader.SetVector(ShaderIDs._TextureSize, float4(float2(1.0 / 256), 1, 1));
            texShader.Dispatch(6, 8, 8, 1);
        }
        private void SaveToMask(RenderTexture cacheRt, Texture splat, Channel chan, int2 targetChunkOffset, float targetIndex)
        {
            terrainEditShader.SetTexture(10, ShaderIDs._MainTex, splat);
            terrainEditShader.SetVector(ShaderIDs._TextureSize, float4(targetChunkOffset.x * MTerrain.MASK_RESOLUTION + 0.5f, targetChunkOffset.y * MTerrain.MASK_RESOLUTION + 0.5f, targetIndex, 1));
            switch (chan)
            {
                case Channel.R:
                    terrainEditShader.SetVector("_Mask", float4(1, 0, 0, 0));
                    break;
                case Channel.G:
                    terrainEditShader.SetVector("_Mask", float4(0, 1, 0, 0));
                    break;
                case Channel.B:
                    terrainEditShader.SetVector("_Mask", float4(0, 0, 1, 0));
                    break;
                case Channel.A:
                    terrainEditShader.SetVector("_Mask", float4(0, 0, 0, 1));
                    break;
            }
            terrainEditShader.SetTexture(10, ShaderIDs._DestTex, cacheRt);
            const int disp = MTerrain.MASK_RESOLUTION / 8;
            terrainEditShader.Dispatch(10, disp, disp, 1);

        }
        public void UpdateMaterialIndex()
        {
            int count = 0;
            RenderTexture cacheRT = new RenderTexture(MTerrain.MASK_RESOLUTION, MTerrain.MASK_RESOLUTION, 0, GraphicsFormat.R32_SFloat, 0);
            cacheRT.dimension = TextureDimension.Tex2DArray;
            cacheRT.volumeDepth = 1;
            cacheRT.enableRandomWrite = true;
            cacheRT.Create();
            int largestChunkCount = (int)(pow(2.0, targetData.GetLodOffset()) + 0.1);
            VirtualTextureLoader loader = new VirtualTextureLoader(
             targetData.maskmapPath,
            terrainEditShader,
             largestChunkCount,
             MTerrain.MASK_RESOLUTION, false, null);
            Dictionary<int2, List<Pair<SplatSettings, int>>> commandDict = new Dictionary<int2, List<Pair<SplatSettings, int>>>(256);
            for (int i = 0; i < allMaterials.Count; ++i)
            {
                for (int j = 0; j < allMaterials[i].blendWeights.Count; ++j)
                {
                    var mat = allMaterials[i].blendWeights[j];
                    foreach (var a in mat.splatSettings)
                    {
                        Texture splat = a.splatMap;
                        if (splat)
                        {
                            int2 border = largestChunkCount - a.splatStartPos;
                            border = min(border, splatTillingScale * splat.width / MTerrain.MASK_RESOLUTION);
                            for (int x = 0; x < border.x; ++x)
                                for (int y = 0; y < border.y; ++y)
                                {
                                    if (!commandDict.ContainsKey(int2(x, y) + a.splatStartPos))
                                    {
                                        commandDict.Add(int2(x, y) + a.splatStartPos, new List<Pair<SplatSettings, int>>());
                                    }
                                    commandDict[int2(x, y) + a.splatStartPos].Add(new Pair<SplatSettings, int>(a, count));
                                }
                        }

                    }
                    count++;
                }
            }
            foreach (var i in commandDict)
            {
                foreach (var j in i.Value)
                {
                    int2 offsetValue = i.Key - j.key.splatStartPos;
                    SaveToMask(cacheRT, j.key.splatMap, j.key.chan, offsetValue, j.value / (count - 1f));
                }
                loader.WriteToDisk(cacheRT, 0, i.Key);
            }
            loader.Dispose();
            DestroyImmediate(cacheRT);
        }

        private void OnDisable()
        {

            DestroyImmediate(heightBlendMaterial);
            DestroyImmediate(drawAlbedoRT);
            DestroyImmediate(drawNormalRT);
            DestroyImmediate(drawSMORT);
            DestroyImmediate(randomTileRT);
        }
        public void DrawToMaterial()
        {
            Material mat = GetComponent<MeshRenderer>().sharedMaterial;
            if (!mat) return;

            if (!drawAlbedoRT || !drawNormalRT || !drawSMORT) return;
            CommandBuffer bf = RenderPipeline.BeforeFrameBuffer;
            bf.SetRenderTarget(colors: colorBuffersIdentifier, depth: drawAlbedoRT.depthBuffer);
            bf.ClearRenderTarget(false, true, Color.black);
            if (renderingMaterial.x < 0 || renderingMaterial.x >= allMaterials.Count) return;
            if (allMaterials.Count == 0) return;
            var heightBlend = allMaterials[renderingMaterial.x];
            if (renderingMaterial.y < 0 || renderingMaterial.y >= heightBlend.blendWeights.Count) return;
            var blendMat = heightBlend.blendWeights[renderingMaterial.y];
            heightBlendMaterial.SetTexture(ShaderIDs._NoiseTillingTexture, randomTileRT);
            heightBlendMaterial.SetTexture(ShaderIDs._NoiseTexture, targetData.noiseTex);
            heightBlendMaterial.SetVector(ShaderIDs._TextureSize, float4(textureTillingSize, 1, 1));
            heightBlendMaterial.SetTexture("_Albedo", heightBlend.frontPack.albedo);
            heightBlendMaterial.SetTexture("_Normal", heightBlend.frontPack.normal);
            heightBlendMaterial.SetTexture("_SMO", heightBlend.frontPack.smo);
            heightBlendMaterial.SetVector("_Color", float4(blendMat.albedoColor, blendMat.antiRepeat));
            heightBlendMaterial.SetFloat("_Smoothness", blendMat.smoothness);
            heightBlendMaterial.SetFloat("_Metallic", blendMat.metallic);
            heightBlendMaterial.SetFloat("_Occlusion", blendMat.occlusion);
            heightBlendMaterial.SetVector("_NormalScale", float4(blendMat.normalScale, 1, 1));
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
            var blendMat = heightBlend.blendWeights[renderingMaterial.y];
            heightBlendMaterial.SetVector(ShaderIDs._TextureSize, float4(1));
            heightBlendMaterial.SetTexture(ShaderIDs._NoiseTillingTexture, randomTileRT);
            heightBlendMaterial.SetTexture(ShaderIDs._NoiseTexture, targetData.noiseTex);
            heightBlendMaterial.SetTexture("_Albedo", heightBlend.frontPack.albedo);
            heightBlendMaterial.SetTexture("_Normal", heightBlend.frontPack.normal);
            heightBlendMaterial.SetTexture("_SMO", heightBlend.frontPack.smo);
            heightBlendMaterial.SetVector("_Color", float4(blendMat.albedoColor, 0));
            heightBlendMaterial.SetFloat("_Smoothness", blendMat.smoothness);
            heightBlendMaterial.SetFloat("_Metallic", blendMat.metallic);
            heightBlendMaterial.SetFloat("_Occlusion", blendMat.occlusion);
            heightBlendMaterial.SetVector("_NormalScale", float4(blendMat.normalScale, 1, 1));
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
            for (int i = 0; i < allMaterials.Count; ++i)
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
                if (!a.frontPack.albedo || !a.frontPack.normal || !a.frontPack.smo)
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
                    materialIndex = GetValue(i.frontPack),
                };
                foreach (var j in i.blendWeights)
                {
                    hb.smoothness = j.smoothness;
                    hb.metallic = j.metallic;
                    hb.occlusion = j.occlusion;
                    hb.normalScale = j.normalScale;
                    hb.albedoColor = j.albedoColor;
                    hb.antiRepeat = j.antiRepeat;
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
            target.matOpen = EditorGUILayout.Foldout(target.matOpen, "Materials: ");
            if (target.matOpen)
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
                                    weight.antiRepeat = EditorGUILayout.Toggle("Anti Repeat", weight.antiRepeat == 1) ? 1 : 0;
                                    weight.smoothness = EditorGUILayout.Slider("Smoothness: ", weight.smoothness, 0, 1);
                                    weight.metallic = EditorGUILayout.Slider("Metallic: ", weight.metallic, 0, 1);
                                    weight.occlusion = EditorGUILayout.Slider("Occlusion: ", weight.occlusion, 0, 1);

                                    Color albedo = EditorGUILayout.ColorField(new GUIContent("Albedo: "), new Color(weight.albedoColor.x, weight.albedoColor.y, weight.albedoColor.z), true, false, false);
                                    weight.albedoColor = float3(albedo.r, albedo.g, albedo.b);
                                    weight.normalScale = EditorGUILayout.Vector2Field("Normal Scale: ", weight.normalScale);
                                    if (weight.splatSettings == null) weight.splatSettings = new List<TerrainMaterialEditor.SplatSettings>();
                                    weight.splatOpen = EditorGUILayout.Foldout(weight.splatOpen, "Splats: ");
                                    if (weight.splatOpen)
                                    {
                                        int count = EditorGUILayout.IntField("Splat Count: ", weight.splatSettings.Count);
                                        count = max(count, 0);
                                        if (weight.splatSettings.Count > count)
                                        {
                                            int v = weight.splatSettings.Count - count;
                                            for (int b = 0; b < v; ++b)
                                            {
                                                weight.splatSettings.RemoveAt(weight.splatSettings.Count - 1);
                                            }
                                        }
                                        else if (weight.splatSettings.Count < count)
                                        {
                                            int v = count - weight.splatSettings.Count;
                                            for (int b = 0; b < v; ++b)
                                            {
                                                weight.splatSettings.Add(new TerrainMaterialEditor.SplatSettings
                                                {
                                                    chan = TerrainMaterialEditor.Channel.R,
                                                    splatMap = null,
                                                    splatStartPos = 0
                                                });
                                            }
                                        }
                                        for (int b = 0; b < weight.splatSettings.Count; ++b)
                                        {
                                            EditorGUILayout.LabelField("Splat " + b + ": ");
                                            var set = weight.splatSettings[b];
                                            EditorGUI.indentLevel++;
                                            set.splatMap = (Texture2D)EditorGUILayout.ObjectField("Splat Map: ", set.splatMap, typeof(Texture2D), false);
                                            Vector2Int rest = EditorGUILayout.Vector2IntField("Splat Start Pos: ", new Vector2Int(set.splatStartPos.x, set.splatStartPos.y));
                                            set.splatStartPos = int2(rest.x, rest.y);
                                            set.chan = (TerrainMaterialEditor.Channel)EditorGUILayout.EnumPopup("Channel: ", set.chan);
                                            EditorGUI.indentLevel--;
                                            weight.splatSettings[b] = set;
                                        }

                                    }
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
                            blendWeights = new List<TerrainMaterialEditor.VTMaterial>()
                        };
                        targetmat.blendWeights.Add(new TerrainMaterialEditor.VTMaterial
                        {
                            albedoColor = 1,
                            metallic = 1,
                            normalScale = 1,
                            occlusion = 1,
                            smoothness = 1,
                            antiRepeat = 0,
                            splatOpen = false,
                            splatSettings = new List<TerrainMaterialEditor.SplatSettings>()
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
            if (GUILayout.Button("Save Material Mask"))
            {
                target.UpdateMaterialIndex();
            }
        }
    }
}
#endif