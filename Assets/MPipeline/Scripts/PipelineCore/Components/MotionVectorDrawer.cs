using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MPipeline;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
[ExecuteInEditMode]
[RequireComponent(typeof(Renderer))]
public unsafe sealed class MotionVectorDrawer : MonoBehaviour
{
    private Renderer rend;
    private static MaterialPropertyBlock block = null;
    private static NativeArray<float3x4> allMatrices;
    private static List<MotionVectorDrawer> allDrawers = new List<MotionVectorDrawer>(50);

    public static int Capacity { get { return allDrawers.Capacity; } }
    public static void ExecuteBeforeFrame(ComputeBuffer structuredBuffer)
    {
        if(allMatrices.IsCreated)
            structuredBuffer.SetData(allMatrices, 0, 0, allDrawers.Count);
    }

    public static void Dispose()
    {
        allDrawers.Clear();
        if(allMatrices.IsCreated)
        allMatrices.Dispose();
    }

    public static void ExecuteAfterFrame()
    {
        if (!allMatrices.IsCreated) return;
        float3x4* ptr = allMatrices.Ptr();
        for (int i = 0; i < allDrawers.Count; ++i)
        {
            float4x4 mat = allDrawers[i].transform.localToWorldMatrix;
            ptr->c0 = mat.c0.xyz;
            ptr->c1 = mat.c1.xyz;
            ptr->c2 = mat.c2.xyz;
            ptr->c3 = mat.c3.xyz;
            ptr++;
        }
    }

    private int index;
    private void OnEnable()
    {
        if (!rend) rend = GetComponent<Renderer>();
        if (block == null) block = new MaterialPropertyBlock();
        if (!allMatrices.IsCreated) allMatrices = new NativeArray<float3x4>(50, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        UpdateIndex(allDrawers.Count);
        allDrawers.Add(this);
        if (allMatrices.Length < allDrawers.Count)
        {
            NativeArray<float3x4> newArr = new NativeArray<float3x4>(allDrawers.Capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            UnsafeUtility.MemCpy(newArr.GetUnsafePtr(), allMatrices.GetUnsafePtr(), sizeof(float3x4) * allMatrices.Length);
            allMatrices.Dispose();
            allMatrices = newArr;
        }
        float4x4 ltw = transform.localToWorldMatrix;
        allMatrices[index] = new float3x4(ltw.c0.xyz, ltw.c1.xyz, ltw.c2.xyz, ltw.c3.xyz);
    }

    private void UpdateIndex(int targetIndex)
    {
        index = targetIndex;
        rend.GetPropertyBlock(block);
        block.SetInt(ShaderIDs._OffsetIndex, targetIndex);
        rend.SetPropertyBlock(block);
    }

    private void OnDisable()
    {
        if (allDrawers == null || !allMatrices.IsCreated) return;
        var lastOne = allDrawers[allDrawers.Count - 1];
        allDrawers[index] = lastOne;
        lastOne.UpdateIndex(index);
        allDrawers.RemoveAt(allDrawers.Count - 1);
        allMatrices[index] = allMatrices[allDrawers.Count];
    }
}
