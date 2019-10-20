using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using UnityEngine.AddressableAssets;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.AddressableAssets;
#endif
[System.Serializable]
public unsafe sealed class VirtualMaterial
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

    public Dictionary<Material, int> GetMaterialsData(MeshRenderer[] allRenderers)
    {
        float3 ColorToVector(Color c)
        {
            return float3(c.r, c.b, c.g);
        }
        float2 GetVector2(float4 vec)
        {
            return float2(vec.x, vec.y);
        }
        var dict = new Dictionary<Material, int>(allRenderers.Length);
        allProperties = new List<MaterialProperties>(allRenderers.Length);
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
                    allProperties.Add(new MaterialProperties
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
        void GetGUIDs(out AssetReference[] strs, List<Texture> texs, int typeIndex)
        {
            strs = new AssetReference[texs.Count];
            for (int i = 0; i < texs.Count; ++i)
            {
                string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(texs[i]));
                MPipeline.MEditorLib.SetObjectAddressable(texs[i], guid);
                strs[i] = new AssetReference(guid);
            }
        }
        GetGUIDs(out albedoGUIDs, albedoTexs, 0);
        GetGUIDs(out secondAlbedoGUIDs, secondAlbedoTex, 0);
        GetGUIDs(out normalGUIDs, normalTexs, 1);
        GetGUIDs(out secondNormalGUIDs, secondBumpTex, 1);
        GetGUIDs(out smoGUIDs, smoTexs, 0);
        GetGUIDs(out secondSpecGUIDs, secondSpecTex, 0);
        GetGUIDs(out emissionGUIDs, emissionTex, 2);
        GetGUIDs(out heightGUIDs, heightTex, 3);
        EditorUtility.SetDirty(AddressableAssetSettingsDefaultObject.Settings);
        return dict;
    }
#endif
    #endregion

    #region RUNTIME
    public AssetReference [] albedoGUIDs;
    public AssetReference[] normalGUIDs;
    public AssetReference[] smoGUIDs;
    public AssetReference[] emissionGUIDs;
    public AssetReference[] heightGUIDs;
    public AssetReference[] secondAlbedoGUIDs;
    public AssetReference[] secondNormalGUIDs;
    public AssetReference[] secondSpecGUIDs;
    public List<MaterialProperties> allProperties;
    #endregion
}
