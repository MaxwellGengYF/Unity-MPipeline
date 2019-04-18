using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using System.Runtime.CompilerServices;

public static class TerrainUtility
{
    // box: xy position    zw size
    //circle: xy: position z: range
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool BoxContact(float4 box, float3 circle)
    {
        float2 v = circle.xy - box.xy;
        v = math.abs(v);
        v -= box.zw;
        return math.lengthsq(v) <= (circle.z * circle.z);
    }
}
