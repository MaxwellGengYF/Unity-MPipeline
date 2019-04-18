using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System.Runtime.CompilerServices;
using System;
using System.Collections;
using System.Collections.Generic;
namespace MPipeline
{
    public unsafe struct DictData
    {
        public int capacity;
        public int length;
        public void* start;
        public Allocator alloc;
    }
    public unsafe struct NativeDictionary<K, V, F> : IEnumerable<V> where K : unmanaged where V : unmanaged where F : unmanaged, IFunction<K, K, bool>
    {
        static readonly int stride = sizeof(K) + sizeof(V) + 8;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static V* GetV(K* ptr)
        {
            return (V*)(ptr + 1);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static K** GetNextPtr(K* ptr)
        {
            UIntPtr num = new UIntPtr(ptr);
            num += (sizeof(K) + sizeof(V));
            return (K**)num;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private K** GetK(int index)
        {
            return (K**)data->start + index;
        }
        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return data->length; }
        }
        public int Capacity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return data->capacity; }
        }
        [NativeDisableUnsafePtrRestriction]
        private DictData* data;
        private F equalFunc;
        public bool isCreated { get; private set; }
        private void Resize(int targetSize)
        {
            K** newData = (K**)MUnsafeUtility.Malloc(targetSize * 8, data->alloc);
            UnsafeUtility.MemClear(newData, targetSize * 8);
            K** oldPtr = (K**)data->start;
            for (int i = 0; i < data->capacity; ++i)
            {
                K* currentPtr = oldPtr[i];
                while (currentPtr != null)
                {
                    AddTo(*currentPtr, *GetV(currentPtr), targetSize, newData);
                    currentPtr = *GetNextPtr(currentPtr);
                }
                currentPtr = oldPtr[i];
                while (currentPtr != null)
                {
                    K* next = *GetNextPtr(currentPtr);
                    UnsafeUtility.Free(currentPtr, data->alloc);

                    currentPtr = next;
                }
            }
            UnsafeUtility.Free(data->start, data->alloc);
            data->start = newData;
            data->capacity = targetSize;
        }

        public NativeDictionary(int capacity, Allocator alloc, F equalFunc)
        {
            capacity = Mathf.Max(capacity, 1);
            this.equalFunc = equalFunc;
            isCreated = true;
            data = (DictData*)MUnsafeUtility.Malloc(sizeof(DictData), alloc);
            data->capacity = capacity;
            data->length = 0;
            data->alloc = alloc;
            data->start = MUnsafeUtility.Malloc(8 * capacity, alloc);
            UnsafeUtility.MemClear(data->start, 8 * capacity);
        }

        private void AddTo(K key, V value, int capacity, K** origin)
        {
            int index = Mathf.Abs(key.GetHashCode()) % capacity;
            K** currentPos = origin + index;
            while ((*currentPos) != null)
            {
                currentPos = GetNextPtr(*currentPos);
            }
            (*currentPos) = (K*)MUnsafeUtility.Malloc(stride, data->alloc);
            (**currentPos) = key;
            (*GetV(*currentPos)) = value;
            (*GetNextPtr(*currentPos)) = null;
        }

        public void Remove(K key)
        {
            int index = Mathf.Abs(key.GetHashCode()) % data->capacity;
            K** currentPtr = GetK(index);
            while ((*currentPtr) != null)
            {
                K** next = GetNextPtr(*currentPtr);
                if (equalFunc.Run(ref **currentPtr, ref key))
                {
                    K* prev = *currentPtr;
                    *currentPtr = *next;
                    UnsafeUtility.Free(prev, data->alloc);
                    data->length--;
                    return;
                }
                else
                {
                    currentPtr = next;
                }
            }
        }

        public bool Contains(K key)
        {
            int index = Mathf.Abs(key.GetHashCode()) % data->capacity;
            K** currentPos = GetK(index);
            while ((*currentPos) != null)
            {
                if (equalFunc.Run(ref **currentPos, ref key))
                {
                    return true;
                }
                currentPos = GetNextPtr(*currentPos);
            }
            return false;
        }

        public V this[K key]
        {
            get
            {
                int index = Mathf.Abs(key.GetHashCode()) % data->capacity;
                K** currentPos = GetK(index);
                while ((*currentPos) != null)
                {
                    if (equalFunc.Run(ref **currentPos, ref key))
                    {
                        return *GetV(*currentPos);
                    }
                    currentPos = GetNextPtr(*currentPos);
                }
                return default;
            }
            set
            {
                int hashCode = key.GetHashCode();
                hashCode = Mathf.Abs(hashCode);
                int index = hashCode % data->capacity;
                K** currentPos = GetK(index);
                while ((*currentPos) != null)
                {
                    if (equalFunc.Run(ref **currentPos, ref key))
                    {
                        *GetV(*currentPos) = value;
                        return;
                    }
                    currentPos = GetNextPtr(*currentPos);
                }
                Add(ref key, ref value, hashCode);
            }
        }

        public void Add(K key, V value)
        {
            Add(ref key, ref value, Mathf.Abs(key.GetHashCode()));
        }

        private void Add(ref K key, ref V value, int hashCode)
        {
            if (data->capacity <= data->length)
            {
                Resize(Mathf.Max(data->length + 1, (int)(data->length * 1.5f)));
            }
            int index = hashCode % data->capacity;
            K** currentPos = GetK(index);
            while ((*currentPos) != null)
            {
                currentPos = GetNextPtr(*currentPos);
            }
            (*currentPos) = (K*)MUnsafeUtility.Malloc(stride, data->alloc);
            (**currentPos) = key;
            (*GetV(*currentPos)) = value;
            (*GetNextPtr(*currentPos)) = null;
            data->length++;
        }

        public void Dispose()
        {
            Allocator alloc = data->alloc;
            for (int i = 0; i < data->capacity; ++i)
            {
                K* currentPtr = *GetK(i);
                while (currentPtr != null)
                {
                    K* next = *GetNextPtr(currentPtr);
                    UnsafeUtility.Free(currentPtr, alloc);
                    currentPtr = next;
                }
            }
            UnsafeUtility.Free(data->start, alloc);
            UnsafeUtility.Free(data, alloc);
            isCreated = false;
        }

        public bool Get(K key, out V value)
        {
            int index = Mathf.Abs(key.GetHashCode()) % data->capacity;
            K** currentPos = GetK(index);
            while ((*currentPos) != null)
            {
                if (equalFunc.Run(ref **currentPos, ref key))
                {
                    value = *GetV(*currentPos);
                    return true;
                }
                currentPos = GetNextPtr(*currentPos);
            }
            value = default;
            return false;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IEnumerator<V> GetEnumerator()
        {
            if (!isCreated || Length == 0) return new DictionaryNullIEnumerator<V>();
            return new DictionaryIenumerator<K, V>((K**)data->start, Capacity);
        }
    }

    public unsafe struct DictionaryNullIEnumerator<V> : IEnumerator<V>
    {
        public V Current
        {
            get
            {
                return default;
            }
        }

        object IEnumerator.Current
        {
            get
            {
                return default;
            }
        }

        public void Dispose()
        {

        }
        public void Reset()
        {

        }
        public bool MoveNext() { return false; }
    }

    public unsafe struct DictionaryIenumerator<K, V> : IEnumerator<V> where V : unmanaged where K : unmanaged
    {
        [NativeDisableUnsafePtrRestriction]
        K** data;
        [NativeDisableUnsafePtrRestriction]
        K** start;
        int offset;
        int capacity;

        public DictionaryIenumerator(K** data, int capacity)
        {
            offset = -1;
            start = data;
            this.data = null;
            this.capacity = capacity;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static V* GetV(K* ptr)
        {
            return (V*)(ptr + 1);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static K** GetNextPtr(K* ptr)
        {
            UIntPtr num = new UIntPtr(ptr);
            num += (sizeof(K) + sizeof(V));
            return (K**)num;
        }

        public V Current
        {
            get
            {
                return *GetV(*data);
            }
        }

        object IEnumerator.Current
        {
            get
            {
                return *GetV(*data);
            }
        }

        public void Reset()
        {
            data = null;
            offset = -1;
        }

        public bool MoveNext()
        {
            if (data == null)
            {
                do
                {
                    offset++;
                    data = start + offset;
                }
                while (*data == null);
            }
            else
            {
                data = GetNextPtr(*data);
                while (*data == null)
                {
                    offset++;
                    data = start + offset;
                }
            }
            return offset < capacity;
        }

        public void Dispose()
        {

        }
    }
}