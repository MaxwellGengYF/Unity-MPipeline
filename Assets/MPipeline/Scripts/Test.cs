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
using Random = UnityEngine.Random;
[ExecuteInEditMode]
public unsafe class Test : MonoBehaviour
{
    private void Update()
    {
        Vector3 vec = transform.position;
        vec.y = sin(Time.time * 10) * 3;
        transform.position = vec;
    }
}
