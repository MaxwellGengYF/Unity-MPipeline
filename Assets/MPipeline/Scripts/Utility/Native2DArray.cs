using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Collections;
using System.Runtime.CompilerServices;
namespace MPipeline
{
    public unsafe struct Native2DArray<T> where T : unmanaged
    {
        public uint2 Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private set;
        }
        public T* ptr
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private set;
        }
        private Allocator alloc;
        public Native2DArray(uint2 size, Allocator alloc)
        {
            this.alloc = alloc;
            Length = size;
            ptr = (T*)UnsafeUtility.Malloc(size.x * size.y * sizeof(T), 16, alloc);
        }

        public void Dispose()
        {
            UnsafeUtility.Free(ptr, alloc);
        }

        public T* AddressOf(uint x, uint y)
        {
            return ptr + y * Length.x + x;
        }

        public ref T this[uint x, uint y]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return ref ptr[y * Length.x + x];
            }
        }

        public ref T this[int x, int y]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return ref ptr[y * Length.x + x];
            }
        }

        public ref T this[int2 value]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return ref ptr[value.y * Length.x + value.x];
            }
        }

        public ref T this[uint2 value]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return ref ptr[value.y * Length.x + value.x];
            }
        }
    }
}