#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using UnityEngine.Experimental.Rendering;
namespace MPipeline
{

    public unsafe class TextureHavenImporter : ScriptableWizard
    {
        public int2 resolution = 1024;
        public struct TexturePack
        {
            public Texture2D roughness;
            public Texture2D metallic;
            public Texture2D occlusion;
            public Texture2D disp;
        }
        public Texture2D[] allTextures;
        [MenuItem("MPipeline/Terrain/Texture Haven Importer")]
        private static void CreateWizard()
        {
            DisplayWizard<TextureHavenImporter>("Importer", "Import");
        }

        private void OnWizardCreate()
        {
            ComputeShader cs = Resources.Load<ComputeShader>("ReadRTData");
            Dictionary<string, TexturePack> allPack = new Dictionary<string, TexturePack>(100);
            RenderTexture smoRT = new RenderTexture(resolution.x, resolution.y, 0, GraphicsFormat.R8G8B8A8_UNorm, 0);
            Texture2D tex = new Texture2D(resolution.x, resolution.y, GraphicsFormat.R8G8B8A8_UNorm, TextureCreationFlags.None);
            smoRT.enableRandomWrite = true;
            smoRT.Create();
            foreach (var i in allTextures)
            {
                int index;
                string name;
                
                if ((index = i.name.IndexOf("_rough")) >= 0)
                {
                    name = new string(i.name.Ptr(), 0, index);
                    if (!allPack.ContainsKey(name))
                    {
                        allPack.Add(name, new TexturePack());
                    }
                    TexturePack pack = allPack[name];
                    pack.roughness = i;
                    allPack[name] = pack;
                }
                else if ((index = i.name.IndexOf("_ao")) >= 0)
                {
                    name = new string(i.name.Ptr(), 0, index);
                    if (!allPack.ContainsKey(name))
                    {
                        allPack.Add(name, new TexturePack());
                    }
                    TexturePack pack = allPack[name];
                    pack.occlusion = i;
                    allPack[name] = pack;
                }
                else if ((index = i.name.IndexOf("_spec")) >= 0)
                {
                    name = new string(i.name.Ptr(), 0, index);
                    if (!allPack.ContainsKey(name))
                    {
                        allPack.Add(name, new TexturePack());
                    }
                    TexturePack pack = allPack[name];
                    pack.metallic = i;
                    allPack[name] = pack;
                }
                else if ((index = i.name.IndexOf("_disp")) >= 0)
                {
                    name = new string(i.name.Ptr(), 0, index);
                    if (!allPack.ContainsKey(name))
                    {
                        allPack.Add(name, new TexturePack());
                    }
                    TexturePack pack = allPack[name];
                    pack.disp = i;
                    allPack[name] = pack;
                }
            }
            foreach (var i in allPack)
            {
                Graphics.SetRenderTarget(smoRT);
                GL.Clear(false, true, new Color(1, 0.1f, 1, 0.5f));
                cs.SetTexture(6, ShaderIDs._DestTex, smoRT);
                cs.SetTexture(7, ShaderIDs._DestTex, smoRT);
                int2 disp = resolution / 8;
                Texture2D exampleTex = null;
                if (i.Value.roughness)
                {
                    cs.SetTexture(7, ShaderIDs._SourceTex, i.Value.roughness);
                    cs.SetVector("_Mask", float4(1, 0, 0, 0));
                    cs.Dispatch(7, disp.x, disp.y, 1);
                    exampleTex = i.Value.roughness;
                }

                if (i.Value.metallic)
                {
                    cs.SetTexture(6, ShaderIDs._SourceTex, i.Value.metallic);
                    cs.SetVector("_Mask", float4(0, 1, 0, 0));
                    cs.Dispatch(6, disp.x, disp.y, 1);
                    exampleTex = i.Value.metallic;
                }

                if (i.Value.occlusion)
                {
                    cs.SetTexture(6, ShaderIDs._SourceTex, i.Value.occlusion);
                    cs.SetVector("_Mask", float4(0, 0, 1, 0));
                    cs.Dispatch(6, disp.x, disp.y, 1);
                    exampleTex = i.Value.occlusion;
                }

                if (i.Value.disp)
                {
                    cs.SetTexture(6, ShaderIDs._SourceTex, i.Value.disp);
                    cs.SetVector("_Mask", float4(0, 0, 0, 1));
                    cs.Dispatch(6, disp.x, disp.y, 1);
                    exampleTex = i.Value.disp;
                }

                RenderTexture.active = smoRT;
                tex.ReadPixels(new Rect(0, 0, resolution.x, resolution.y), 0, 0);
                string assetPath = AssetDatabase.GetAssetPath(exampleTex);
                assetPath = new string(assetPath.Ptr(), 0, assetPath.LastIndexOfAny(new char[] { '/', '\\' }));
                System.IO.File.WriteAllBytes(assetPath + "/" + i.Key + "_smo.png", tex.EncodeToPNG());
            }
            DestroyImmediate(smoRT);
            DestroyImmediate(tex);

        }
    }
}
#endif