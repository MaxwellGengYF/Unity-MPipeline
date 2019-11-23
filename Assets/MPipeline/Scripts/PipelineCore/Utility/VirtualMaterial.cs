using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using UnityEngine.AddressableAssets;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.AddressableAssets;
#endif
namespace MPipeline
{
    public static class VirtualMaterial
    {
        [System.Serializable]
        public struct MaterialProperties
        {
            public float3 _Color;
            public float _Glossiness;
            public float _Occlusion;
            public float2 _NormalIntensity;
            public float _SpecularIntensity;
            public float _MetallicIntensity;
            public float4 _TileOffset;
            public int _MainTex;
            public int _BumpMap;
            public int _SpecularMap;
            public int _EmissionMap;
            public int _HeightMap;
            public float3 _EmissionColor;
            public float _HeightMapIntensity;
            public uint _DecalLayer;
            public int _SecondaryMainTex;
            public int _SecondaryBumpMap;
            public int _SecondarySpecularMap;
            public float4 _SecondaryTileOffset;
        }

        #region EDITOR_TOOLS
#if UNITY_EDITOR

        public unsafe static Dictionary<Material, int> GetMaterialsData(MeshRenderer[] allRenderers, ref SceneStreamLoader loader)
        {
            float3 ColorToVector(Color c)
            {
                return pow(float3(c.r, c.g, c.b), 2.2f);
            }
            float2 GetVector2(float4 vec)
            {
                return float2(vec.x, vec.y);
            }
            var dict = new Dictionary<Material, int>(allRenderers.Length);
            loader.allProperties = new NativeList<MaterialProperties>(allRenderers.Length, Unity.Collections.Allocator.Persistent);
            var albedoTexs = new List<Texture>(allRenderers.Length);
            var normalTexs = new List<Texture>(allRenderers.Length);
            var smoTexs = new List<Texture>(allRenderers.Length);
            var emissionTex = new List<Texture>(allRenderers.Length);
            var heightTex = new List<Texture>(allRenderers.Length);
            var secondAlbedoTex = new List<Texture>(allRenderers.Length);
            var secondBumpTex = new List<Texture>(allRenderers.Length);
            var secondSpecTex = new List<Texture>(allRenderers.Length);
            var albedoDict = new Dictionary<Texture, int>(allRenderers.Length);
            var normalDict = new Dictionary<Texture, int>(allRenderers.Length);
            var smoDict = new Dictionary<Texture, int>(allRenderers.Length);
            var emissionDict = new Dictionary<Texture, int>(allRenderers.Length);
            var heightDict = new Dictionary<Texture, int>(allRenderers.Length);
            var secondAlbedoDict = new Dictionary<Texture, int>(allRenderers.Length);
            var secondBumpDict = new Dictionary<Texture, int>(allRenderers.Length);
            var secondSpecDict = new Dictionary<Texture, int>(allRenderers.Length);
            int len = 0;
            int GetTextureIndex(List<Texture> lst, Dictionary<Texture, int> texDict, Texture tex)
            {
                int ind = -1;
                if (tex)
                {
                    if (!texDict.TryGetValue(tex, out ind))
                    {
                        ind = lst.Count;
                        lst.Add(tex);
                        texDict.Add(tex, ind);
                    }
                }
                return ind;
            }
            foreach (var r in allRenderers)
            {
                var ms = r.sharedMaterials;
                foreach (var m in ms)
                {
                    if (!m)
                    {
                        throw new System.Exception(r.name + " Has Null Mat");
                    }
                    if (!dict.ContainsKey(m))
                    {
                        dict.Add(m, len);
                        Texture albedo = m.GetTexture("_MainTex");
                        Texture normal = m.GetTexture("_BumpMap");
                        Texture smo = m.GetTexture("_SpecularMap");
                        Texture emission = m.GetTexture("_EmissionMap");
                        Texture height = m.GetTexture("_HeightMap");
                        Texture secondBump = m.GetTexture("_SecondaryBumpMap");
                        Texture secondAlbedo = m.GetTexture("_SecondaryMainTex");
                        Texture secondSpec = m.GetTexture("_SecondarySpecularMap");
                        int albedoIndex = GetTextureIndex(albedoTexs, albedoDict, albedo);
                        int normalIndex = GetTextureIndex(normalTexs, normalDict, normal);
                        int smoIndex = GetTextureIndex(smoTexs, smoDict, smo);
                        int emissionIndex = GetTextureIndex(emissionTex, emissionDict, emission);
                        int heightIndex = GetTextureIndex(heightTex, heightDict, height);
                        int secondBumpIndex = GetTextureIndex(secondBumpTex, secondBumpDict, secondBump);
                        int secondAlbedoIndex = GetTextureIndex(secondAlbedoTex, secondAlbedoDict, secondAlbedo);
                        int secondSpecIndex = GetTextureIndex(secondSpecTex, secondSpecDict, secondSpec);
                        loader.allProperties.Add(new MaterialProperties
                        {
                            _Color = ColorToVector(m.GetColor("_Color")),
                            _Glossiness = m.GetFloat("_Glossiness"),
                            _DecalLayer = (uint)m.GetInt("_DecalLayer"),
                            _EmissionColor = ColorToVector(m.GetColor("_EmissionColor") * m.GetFloat("_EmissionMultiplier")),
                            _MetallicIntensity = m.GetFloat("_MetallicIntensity"),
                            _SpecularIntensity = m.GetFloat("_SpecularIntensity"),
                            _Occlusion = m.GetFloat("_Occlusion"),
                            _NormalIntensity = GetVector2(m.GetVector("_NormalIntensity")),
                            _TileOffset = m.GetVector("_TileOffset"),
                            _BumpMap = normalIndex,
                            _EmissionMap = emissionIndex,
                            _MainTex = albedoIndex,
                            _SpecularMap = smoIndex,
                            _HeightMap = heightIndex,
                            _HeightMapIntensity = m.GetFloat("_HeightmapIntensity"),
                            _SecondaryBumpMap = secondBumpIndex,
                            _SecondaryMainTex = secondAlbedoIndex,
                            _SecondarySpecularMap = secondSpecIndex,
                            _SecondaryTileOffset = m.GetVector("_SecondaryTileOffset")
                        });
                        len++;
                    }
                }
            }
            ComputeShader readRTDataShader = Resources.Load<ComputeShader>("ReadRTData");
            void GetGUIDs(out NativeList<int4x4> strs, List<Texture> texs, int typeIndex)
            {
                strs = new NativeList<int4x4>(texs.Count, Allocator.Persistent);
                for (int i = 0; i < texs.Count; ++i)
                {
                    string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(texs[i]));
                    MEditorLib.SetObjectAddressable(texs[i], guid);
                    int4x4 value = 0;
                    fixed (char* c = guid)
                    {
                        UnsafeUtility.MemCpy(value.Ptr(), c, sizeof(int4x4));
                    }
                    strs.Add(value);
                }
            }
            GetGUIDs(out loader.albedoGUIDs, albedoTexs, 0);
            GetGUIDs(out loader.secondAlbedoGUIDs, secondAlbedoTex, 0);
            GetGUIDs(out loader.normalGUIDs, normalTexs, 1);
            GetGUIDs(out loader.secondNormalGUIDs, secondBumpTex, 1);
            GetGUIDs(out loader.smoGUIDs, smoTexs, 0);
            GetGUIDs(out loader.secondSpecGUIDs, secondSpecTex, 0);
            GetGUIDs(out loader.emissionGUIDs, emissionTex, 2);
            GetGUIDs(out loader.heightGUIDs, heightTex, 3);
            EditorUtility.SetDirty(AddressableAssetSettingsDefaultObject.Settings);
            return dict;
        }
        public unsafe static Dictionary<Material, int> GetMaterialsData(List<MeshRenderer> allRenderers, ref SceneStreamLoader loader)
        {
            float3 ColorToVector(Color c)
            {
                return pow(float3(c.r, c.g, c.b), 2.2f);
            }
            float2 GetVector2(float4 vec)
            {
                return float2(vec.x, vec.y);
            }
            var dict = new Dictionary<Material, int>(allRenderers.Count);
            loader.allProperties = new NativeList<MaterialProperties>(allRenderers.Count, Unity.Collections.Allocator.Persistent);
            var albedoTexs = new List<Texture>(allRenderers.Count);
            var normalTexs = new List<Texture>(allRenderers.Count);
            var smoTexs = new List<Texture>(allRenderers.Count);
            var emissionTex = new List<Texture>(allRenderers.Count);
            var heightTex = new List<Texture>(allRenderers.Count);
            var secondAlbedoTex = new List<Texture>(allRenderers.Count);
            var secondBumpTex = new List<Texture>(allRenderers.Count);
            var secondSpecTex = new List<Texture>(allRenderers.Count);
            var albedoDict = new Dictionary<Texture, int>(allRenderers.Count);
            var normalDict = new Dictionary<Texture, int>(allRenderers.Count);
            var smoDict = new Dictionary<Texture, int>(allRenderers.Count);
            var emissionDict = new Dictionary<Texture, int>(allRenderers.Count);
            var heightDict = new Dictionary<Texture, int>(allRenderers.Count);
            var secondAlbedoDict = new Dictionary<Texture, int>(allRenderers.Count);
            var secondBumpDict = new Dictionary<Texture, int>(allRenderers.Count);
            var secondSpecDict = new Dictionary<Texture, int>(allRenderers.Count);
            int len = 0;
            int GetTextureIndex(List<Texture> lst, Dictionary<Texture, int> texDict, Texture tex)
            {
                int ind = -1;
                if (tex)
                {
                    if (!texDict.TryGetValue(tex, out ind))
                    {
                        ind = lst.Count;
                        lst.Add(tex);
                        texDict.Add(tex, ind);
                    }
                }
                return ind;
            }
            foreach (var r in allRenderers)
            {
                var ms = r.sharedMaterials;
                foreach (var m in ms)
                {
                    if (!m)
                    {
                        throw new System.Exception(r.name + " Has Null Mat");
                    }
                    if (!dict.ContainsKey(m))
                    {
                        dict.Add(m, len);
                        Texture albedo = m.GetTexture("_MainTex");
                        Texture normal = m.GetTexture("_BumpMap");
                        Texture smo = m.GetTexture("_SpecularMap");
                        Texture emission = m.GetTexture("_EmissionMap");
                        Texture height = m.GetTexture("_HeightMap");
                        Texture secondBump = m.GetTexture("_SecondaryBumpMap");
                        Texture secondAlbedo = m.GetTexture("_SecondaryMainTex");
                        Texture secondSpec = m.GetTexture("_SecondarySpecularMap");
                        int albedoIndex = GetTextureIndex(albedoTexs, albedoDict, albedo);
                        int normalIndex = GetTextureIndex(normalTexs, normalDict, normal);
                        int smoIndex = GetTextureIndex(smoTexs, smoDict, smo);
                        int emissionIndex = GetTextureIndex(emissionTex, emissionDict, emission);
                        int heightIndex = GetTextureIndex(heightTex, heightDict, height);
                        int secondBumpIndex = GetTextureIndex(secondBumpTex, secondBumpDict, secondBump);
                        int secondAlbedoIndex = GetTextureIndex(secondAlbedoTex, secondAlbedoDict, secondAlbedo);
                        int secondSpecIndex = GetTextureIndex(secondSpecTex, secondSpecDict, secondSpec);
                        loader.allProperties.Add(new MaterialProperties
                        {
                            _Color = ColorToVector(m.GetColor("_Color")),
                            _Glossiness = m.GetFloat("_Glossiness"),
                            _DecalLayer = (uint)m.GetInt("_DecalLayer"),
                            _EmissionColor = ColorToVector(m.GetColor("_EmissionColor") * m.GetFloat("_EmissionMultiplier")),
                            _MetallicIntensity = m.GetFloat("_MetallicIntensity"),
                            _SpecularIntensity = m.GetFloat("_SpecularIntensity"),
                            _Occlusion = m.GetFloat("_Occlusion"),
                            _NormalIntensity = GetVector2(m.GetVector("_NormalIntensity")),
                            _TileOffset = m.GetVector("_TileOffset"),
                            _BumpMap = normalIndex,
                            _EmissionMap = emissionIndex,
                            _MainTex = albedoIndex,
                            _SpecularMap = smoIndex,
                            _HeightMap = heightIndex,
                            _HeightMapIntensity = m.GetFloat("_HeightmapIntensity"),
                            _SecondaryBumpMap = secondBumpIndex,
                            _SecondaryMainTex = secondAlbedoIndex,
                            _SecondarySpecularMap = secondSpecIndex,
                            _SecondaryTileOffset = m.GetVector("_SecondaryTileOffset")
                        });
                        len++;
                    }
                }
            }
            ComputeShader readRTDataShader = Resources.Load<ComputeShader>("ReadRTData");
            void GetGUIDs(out NativeList<int4x4> strs, List<Texture> texs, int typeIndex)
            {
                strs = new NativeList<int4x4>(texs.Count, Allocator.Persistent);
                for (int i = 0; i < texs.Count; ++i)
                {
                    string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(texs[i]));
                    MEditorLib.SetObjectAddressable(texs[i], guid);
                    int4x4 value = 0;
                    fixed (char* c = guid)
                    {
                        UnsafeUtility.MemCpy(value.Ptr(), c, sizeof(int4x4));
                    }
                    strs.Add(value);
                }
            }
            GetGUIDs(out loader.albedoGUIDs, albedoTexs, 0);
            GetGUIDs(out loader.secondAlbedoGUIDs, secondAlbedoTex, 0);
            GetGUIDs(out loader.normalGUIDs, normalTexs, 1);
            GetGUIDs(out loader.secondNormalGUIDs, secondBumpTex, 1);
            GetGUIDs(out loader.smoGUIDs, smoTexs, 0);
            GetGUIDs(out loader.secondSpecGUIDs, secondSpecTex, 0);
            GetGUIDs(out loader.emissionGUIDs, emissionTex, 2);
            GetGUIDs(out loader.heightGUIDs, heightTex, 3);
            EditorUtility.SetDirty(AddressableAssetSettingsDefaultObject.Settings);
            return dict;
        }
#endif
        #endregion


    }
}
