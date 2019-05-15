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
    private struct IntEqual : IFunction<int, int, bool>
    {
        public bool Run(ref int a, ref int b)
        { return a == b; }
    }
    [EasyButtons.Button]
    void RunTest()
    {
        NativeDictionary<int, int, IntEqual> dict = new NativeDictionary<int, int, IntEqual>(5, Allocator.Temp, new IntEqual());
        for (int i = 0; i < 50; ++i)
        {
            dict.Add(i, i + 5);
        }
        for (int i = 0; i < 10; ++i) {
            dict.Remove(i);
            }
        Debug.Log(dict.Length);
         for (int i = 0; i< 50; ++i)
        {
            int value;
            if(dict.Get(i, out value))
            {
                Debug.Log(value);
            }
        }
    }
}
#endif
