using UnityEngine;
using UnityEngine.Rendering;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
namespace MPipeline
{

    public unsafe static class ComputeShaderUtility
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Dispatch(ComputeShader shader, CommandBuffer buffer, int kernal, int count)
        {
            uint x, y, z;
            shader.GetKernelThreadGroupSizes(kernal, out x, out y, out z);
            int threadPerGroup = Mathf.CeilToInt(count / (float)x);
            buffer.SetComputeIntParam(shader, ShaderIDs._Count, count);
            buffer.DispatchCompute(shader, kernal, threadPerGroup, 1, 1);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DispatchDirect(ComputeShader shader, int kernal, int count)
        {
            uint x, y, z;
            shader.GetKernelThreadGroupSizes(kernal, out x, out y, out z);
            int threadPerGroup = Mathf.CeilToInt(count / (float)x);
            shader.SetInt(ShaderIDs._Count, count);
            shader.Dispatch(kernal, threadPerGroup, 1, 1);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetThreadPerGroup(ComputeShader shader, int kernal, int count)
        {
            uint x, y, z;
            shader.GetKernelThreadGroupSizes(kernal, out x, out y, out z);
            return Mathf.CeilToInt(count / (float)x);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetDataPtr<T>(this ComputeBuffer buffer, T* dataPoint, int length) where T : unmanaged
        {
            if (length <= 0) return;
            NativeArray<T> arr = new NativeArray<T>(0, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            UnsafeUtility.Free(arr.GetUnsafePtr(), Allocator.Temp);
            ulong* arrPtr = (ulong*)UnsafeUtility.AddressOf(ref arr);
            arrPtr[0] = (ulong)dataPoint;
            arrPtr[1] = (ulong)length;
            arrPtr[2] = (ulong)length - 1;
            buffer.SetData(arr);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetDataPtr<T>(this ComputeBuffer buffer, T* dataPoint, int bufferStart, int length) where T : unmanaged
        {
            if (length <= 0) return;
            NativeArray<T> arr = new NativeArray<T>(0, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            UnsafeUtility.Free(arr.GetUnsafePtr(), Allocator.Temp);
            ulong* arrPtr = (ulong*)UnsafeUtility.AddressOf(ref arr);
            arrPtr[0] = (ulong)dataPoint;
            arrPtr[1] = (ulong)length;
            arrPtr[2] = (ulong)length - 1;
            buffer.SetData(arr, 0, bufferStart, length);
        }
    }
}