using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using static Unity.Collections.LowLevel.Unsafe.UnsafeUtility;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Burst;
namespace MPipeline
{
    public unsafe struct JobCommonStruct<T> : IJob where T : unmanaged, IJob
    {
        [NativeDisableUnsafePtrRestriction]
        public T* pointer;
        public void Execute()
        {
            pointer->Execute();
        }
    }

    public unsafe struct JobCommonParallarStruct<T> : IJobParallelFor where T : unmanaged, IJobParallelFor
    {
        [NativeDisableUnsafePtrRestriction]
        public T* pointer;
        public void Execute(int index)
        {
            pointer->Execute(index);
        }
    }
    [BurstCompile]
    public unsafe struct JobCommonStructBurst<T> : IJob where T : unmanaged, IJob
    {
        [NativeDisableUnsafePtrRestriction]
        public T* pointer;
        public void Execute()
        {
            pointer->Execute();
        }
    }
    [BurstCompile]
    public unsafe struct JobCommonParallarStructBurst<T> : IJobParallelFor where T : unmanaged, IJobParallelFor
    {
        [NativeDisableUnsafePtrRestriction]
        public T* pointer;
        public void Execute(int index)
        {
            pointer->Execute(index);
        }
    }
    public unsafe static class JobUtility
    {
        public static JobHandle ScheduleRef<T>(ref this T str, JobHandle dependsOn = default) where T : unmanaged, IJob
        {
            JobCommonStruct<T> strct = new JobCommonStruct<T>
            {
                pointer = (T*)AddressOf(ref str)
            };
            return strct.Schedule(dependsOn);
        }
        public static JobHandle ScheduleRef<T>(ref this T str, int length, int innerLoop, JobHandle dependsOn = default) where T : unmanaged, IJobParallelFor
        {
            JobCommonParallarStruct<T> strct = new JobCommonParallarStruct<T>
            {
                pointer = (T*)AddressOf(ref str)
            };
            return strct.Schedule(length, innerLoop, dependsOn);
        }
        public static JobHandle ScheduleRefBurst<T>(ref this T str, JobHandle dependsOn = default) where T : unmanaged, IJob
        {
            JobCommonStructBurst<T> strct = new JobCommonStructBurst<T>
            {
                pointer = (T*)AddressOf(ref str)
            };
            return strct.Schedule(dependsOn);
        }
        public static JobHandle ScheduleRefBurst<T>(ref this T str, int length, int innerLoop, JobHandle dependsOn = default) where T : unmanaged, IJobParallelFor
        {
            JobCommonParallarStructBurst<T> strct = new JobCommonParallarStructBurst<T>
            {
                pointer = (T*)AddressOf(ref str)
            };
            return strct.Schedule(length, innerLoop, dependsOn);
        }
    }
}
