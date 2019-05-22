using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Reflection;
using static Unity.Mathematics.math;
using MPipeline;
using static Unity.Collections.LowLevel.Unsafe.UnsafeUtility;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

#if UNITY_EDITOR
public unsafe class Test : MonoBehaviour
{
    [System.Serializable]
    public struct renderersData
    {
        public Renderer rend;
        public float4 scaleOffset;
        public int index;
    }
    public List<renderersData> datas = new List<renderersData>();
    public Texture2D[] lightmaps;
    [EasyButtons.Button]
    void Run()
    {
        LightmapData[] ds = LightmapSettings.lightmaps;
        lightmaps = new Texture2D[ds.Length];
        for(int i = 0; i < lightmaps.Length; ++i)
        {
            lightmaps[i] = ds[i].lightmapColor;
        }
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach(var i in renderers)
        {
            datas.Add(new renderersData
            {
                index = i.lightmapIndex,
                rend = i,
                scaleOffset = i.lightmapScaleOffset
            });
        }
    }
    [EasyButtons.Button]
    private void Set()
    {
        LightmapData[] ds = new LightmapData[lightmaps.Length];
        for(int i = 0; i < lightmaps.Length; ++i)
        {
            ds[i] = new LightmapData();
            ds[i].lightmapColor = lightmaps[i];
        }
        LightmapSettings.lightmaps = ds;
        foreach(var i in datas)
        {
            i.rend.lightmapIndex = i.index;
            i.rend.lightmapScaleOffset = i.scaleOffset;
        }
    }
}
#endif
