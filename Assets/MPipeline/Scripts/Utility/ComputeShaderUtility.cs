using UnityEngine;
using UnityEngine.Rendering;
using System.Runtime.CompilerServices;


public static class ComputeShaderUtility
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Dispatch(ComputeShader shader, CommandBuffer buffer, int kernal, int count, float threadGroupCount)
    {
        int threadPerGroup = Mathf.CeilToInt(count / threadGroupCount);
        buffer.SetComputeIntParam(shader, ShaderIDs._Count, count);
        buffer.DispatchCompute(shader, kernal, threadPerGroup, 1, 1);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Dispatch(ComputeShader shader, int kernal, int count, float threadGroupCount)
    {
        int threadPerGroup = Mathf.CeilToInt(count / threadGroupCount);
        shader.SetInt(ShaderIDs._Count, count);
        shader.Dispatch(kernal, threadPerGroup, 1, 1);
    }
}