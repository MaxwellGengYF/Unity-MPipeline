using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using System.Runtime.CompilerServices;
namespace MPipeline
{
    public unsafe struct Native2DArray<T> where T : unmanaged
    {
        [NativeDisableUnsafePtrRestriction]
        private T* m_ptr;
        public T* ptr { get { return m_ptr; } }
        private bool isCreated;
        public int2 Length { get; private set; }
        public Allocator allocator { get; private set; }
        private int getLength(int2 index)
        {
            return Length.x * index.y + index.x;
        }
        public ref T this[int2 index]
        {
            get
            {
                index %= Length;
                return ref m_ptr[getLength(index)];
            }
        }
        public Native2DArray(int2 len, Allocator alloc, bool clearMemory = false)
        {
            long size = len.x * len.y * sizeof(T);
            m_ptr = MUnsafeUtility.Malloc<T>(size, alloc);
            if (clearMemory) UnsafeUtility.MemClear(m_ptr, size);
            isCreated = true;
            allocator = alloc;
            Length = len;
        }
        public void Dispose()
        {
            if (isCreated)
            {
                UnsafeUtility.Free(m_ptr, allocator);
                isCreated = false;
            }
        }
        public void SetAll(T defaultValue)
        {
            int len = Length.x * Length.y;
            new ParallarSet
            {
                ptr = m_ptr,
                value = defaultValue
            }.Schedule(len, len / 8).Complete();
        }
        [Unity.Burst.BurstCompile]
        private struct ParallarSet : IJobParallelFor
        {
            [NativeDisableUnsafePtrRestriction]
            public T* ptr;
            public T value;
            public void Execute(int index)
            {
                ptr[index] = value;
            }
        }
    }
}