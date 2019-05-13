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
    public Texture2D tex;
    public Renderer rend;
    [EasyButtons.Button]
    void RunTest()
    {
        LightmapData data = new LightmapData();
        data.lightmapColor = tex;
        LightmapSettings.lightmaps = new LightmapData[]
        {
            data
        };
        rend.lightmapIndex = 0;
    }
}
#endif
