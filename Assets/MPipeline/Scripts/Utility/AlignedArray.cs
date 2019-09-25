using UnityEngine;
using System.Collections.Generic;
using Unity.Jobs;
using System;
using UnityEngine.Rendering;
using System.Reflection;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;

namespace MPipeline
{
    public unsafe struct AlignedArray<T> where T : unmanaged
    {
        private Allocator constAlloc;
        public int Length { get; private set; }
        private T* ptr;
        public bool isCreated
        {
            get
            {
                return ptr != null;
            }
        }
        public int stride { get; private set; }
        public AlignedArray(int count, int alignedSize, Allocator alloc)
        {
            stride = (sizeof(T) % alignedSize > 0) ? (sizeof(T) / alignedSize + 1) * alignedSize : sizeof(T);
            ptr = MUnsafeUtility.Malloc<T>(stride * count, alloc);
            constAlloc = alloc;
            Length = count;
        }

        public void Dispose()
        {
            MUnsafeUtility.SafeFree(ref ptr, constAlloc);
            Length = 0;
        }

        public ref T this[int index]
        {
            get
            {
                if (index < 0 || index >= Length) throw new IndexOutOfRangeException("Index out of Range in Aligned Array!");
                ulong num = (ulong)ptr;
                num += (ulong)(index * stride);
                return ref *((T*)num);
            }
        }

        public ref T this[uint index]
        {
            get
            {
                if (index >= Length) throw new IndexOutOfRangeException("Index out of Range in Aligned Array!");
                ulong num = (ulong)ptr;
                num += (ulong)(index * stride);
                return ref *((T*)num);
            }
        }
    }
}
