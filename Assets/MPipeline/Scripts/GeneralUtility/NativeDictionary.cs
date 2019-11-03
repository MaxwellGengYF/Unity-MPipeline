using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System.Runtime.CompilerServices;
using System;
using System.Collections;
using System.Collections.Generic;
namespace MPipeline
{
    public unsafe struct PtrList
    {
        public int length { get; private set; }
        public int capacity { get; private set; }
        public int stride { get; private set; }
        public UIntPtr dataPtr { get; private set; }
        public Allocator alloc { get; private set; }

        public bool isCreated { get { return dataPtr.ToPointer() != null; } }
        public PtrList(int initCapacity, int stride, Allocator alloc)
        {
            length = 0;
            capacity = initCapacity;
            this.stride = stride;
            this.alloc = alloc;
            dataPtr = new UIntPtr(MUnsafeUtility.Malloc(stride * capacity, alloc));
        }
        public UIntPtr this[int index]
        {
            get
            {
                return dataPtr + index * stride;
            }
            set
            {
                UIntPtr targetPtr = dataPtr + index * stride;
                UnsafeUtility.MemCpy(targetPtr.ToPointer(), value.ToPointer(), stride);
            }
        }
        public void Add(void* targetPtr)
        {
            if (length >= capacity)
            {
                int newCapacity = capacity * 2;
                UIntPtr newPtr = new UIntPtr(MUnsafeUtility.Malloc(newCapacity * stride, alloc));
                UnsafeUtility.MemCpy(newPtr.ToPointer(), dataPtr.ToPointer(), capacity * stride);
                UnsafeUtility.Free(dataPtr.ToPointer(), alloc);
                capacity = newCapacity;
                dataPtr = newPtr;
            }
            this[length++] = new UIntPtr(targetPtr);
        }
        public int Add()
        {
            if (length >= capacity)
            {
                int newCapacity = capacity * 2;
                UIntPtr newPtr = new UIntPtr(MUnsafeUtility.Malloc(newCapacity * stride, alloc));
                UnsafeUtility.MemCpy(newPtr.ToPointer(), dataPtr.ToPointer(), capacity * stride);
                UnsafeUtility.Free(dataPtr.ToPointer(), alloc);
                capacity = newCapacity;
                dataPtr = newPtr;
            }
            return length++;
        }
        public void RemoveAt(int index)
        {
            this[index] = this[--length];
        }
        public void Clear()
        {
            length = 0;
        }
        public void Dispose()
        {
            if (dataPtr.ToPointer() != null)
            {
                length = 0;
                UnsafeUtility.Free(dataPtr.ToPointer(), alloc);
                dataPtr = new UIntPtr(null);
            }
        }
    }
    public unsafe struct DictData
    {
        public int capacity;
        public PtrList* hashArray;
        public PtrList keyValueList;
    }
    public unsafe struct NativeDictionary<K, V, F> : IEnumerable<Pair<K, V>> where K : unmanaged where V : unmanaged where F : unmanaged, IFunction<K, K, bool>
    {
        static readonly int stride = sizeof(K) + sizeof(int);
        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return data->keyValueList.length; }
        }
        public int Capacity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return data->capacity; }
        }
        [NativeDisableUnsafePtrRestriction]
        private DictData* data;
        private F equalFunc;
        public Allocator alloc { get; private set; }
        public bool isCreated { get; private set; }
        private void AddKey(PtrList* targetHashArray, uint hash, ref K key, int valueIndex, PtrList* pooledList, ref int count)
        {
            ref PtrList lst = ref targetHashArray[hash];
            if (!lst.isCreated)
            {
                count--;
                if (count < 0) lst = new PtrList(5, stride, alloc);
                else lst = pooledList[count];
            }
            K* keyPtr = (K*)lst[lst.Add()];
            *keyPtr = key;
            int* valuePtr = (int*)(keyPtr + 1);
            *valuePtr = valueIndex;
        }
        private void Resize(int targetSize)
        {
            if (targetSize <= Capacity) return;
            PtrList* newHashArray = MUnsafeUtility.Malloc<PtrList>(sizeof(PtrList) * targetSize, alloc);
            UnsafeUtility.MemClear(newHashArray, sizeof(PtrList) * targetSize);
            PtrList* pooledList = stackalloc PtrList[Capacity];
            int pooledCount = 0;
            for (int i = 0; i < Capacity; ++i)
            {
                ref PtrList poolLst = ref data->hashArray[i];
                if (poolLst.isCreated)
                {
                    poolLst.Clear();
                    pooledList[pooledCount] = poolLst;
                    pooledCount++;
                }
            }
            //  
            for (int i = 0; i < data->keyValueList.length; ++i)
            {
                K* keyPtr = (K*)data->keyValueList[i];
                V* valuePtr = (V*)(keyPtr + 1);
                uint hash = (uint)keyPtr->GetHashCode();
                uint newHash = hash % (uint)targetSize;
                AddKey(newHashArray, newHash, ref *keyPtr, i, pooledList, ref pooledCount);
            }

            UnsafeUtility.Free(data->hashArray, alloc);
            data->hashArray = newHashArray;
            data->capacity = targetSize;
        }

        public NativeDictionary(int capacity, Allocator alloc, F equalFunc)
        {
            capacity = Mathf.Max(capacity, 1);
            this.equalFunc = equalFunc;
            isCreated = true;
            data = (DictData*)MUnsafeUtility.Malloc(sizeof(DictData), alloc);
            data->capacity = capacity;
            data->keyValueList = new PtrList(capacity, sizeof(K) + sizeof(V), alloc);
            this.alloc = alloc;
            data->hashArray = MUnsafeUtility.Malloc<PtrList>(sizeof(PtrList) * capacity, alloc);
            UnsafeUtility.MemClear(data->hashArray, sizeof(PtrList) * capacity);
        }
        private void ResetValue(int index)
        {
            K* targetKey = (K*)data->keyValueList[index];
            uint hash = (uint)targetKey->GetHashCode() % (uint)Capacity;
            ref PtrList lst = ref data->hashArray[hash];
            for (int i = 0; i < lst.length; ++i)
            {
                K* keyPtr = (K*)lst[i];
                if (equalFunc.Run(ref *keyPtr, ref *targetKey))
                {
                    int* indexPtr = (int*)(keyPtr + 1);
                    *indexPtr = index;
                    return;
                }
            }
        }
        private int GetIndex(ref K key)
        {
            uint hash = (uint)key.GetHashCode() % (uint)Capacity;
            ref PtrList lst = ref data->hashArray[hash];
            for (int i = 0; i < lst.length; ++i)
            {
                K* keyPtr = (K*)lst[i];
                if (equalFunc.Run(ref *keyPtr, ref key))
                {
                    int* indexPtr = (int*)(keyPtr + 1);
                    return *indexPtr;
                }
            }
            return -1;
        }
        public void Remove(K key)
        {
            uint hash = (uint)key.GetHashCode() % (uint)Capacity;
            ref PtrList lst = ref data->hashArray[hash];
            for (int i = 0; i < lst.length; ++i)
            {
                K* keyPtr = (K*)lst[i];
                if (equalFunc.Run(ref *keyPtr, ref key))
                {
                    int index = *(int*)(keyPtr + 1);
                    data->keyValueList.RemoveAt(index);
                    ResetValue(index);
                    lst.RemoveAt(i);
                    return;
                }
            }
        }

        public bool Contains(K key)
        {
            uint hash = (uint)key.GetHashCode() % (uint)Capacity;
            ref PtrList lst = ref data->hashArray[hash];
            for (int i = 0; i < lst.length; ++i)
            {
                K* keyPtr = (K*)lst[i];
                if (equalFunc.Run(ref *keyPtr, ref key))
                {
                    return true;
                }
            }
            return false;
        }

        public ref V this[K key]
        {
            get
            {
                int ind = GetIndex(ref key);
                if (ind < 0)
                {
                    return ref AddDefault(key);
                }
                return ref *(V*)(data->keyValueList[ind] + sizeof(K));
            }
        }

        private ref V AddDefault(K key)
        {
            Resize(Length + 1);
            uint hash = (uint)key.GetHashCode() % (uint)Capacity;
            ref PtrList lst = ref data->hashArray[hash];
            if (!lst.isCreated) lst = new PtrList(5, stride, alloc);
            K* newKeyPtr = (K*)lst[lst.Add()];
            *newKeyPtr = key;
            int* indPtr = (int*)(newKeyPtr + 1);
            *indPtr = data->keyValueList.length;
            K* keyValuePtr = (K*)data->keyValueList[data->keyValueList.Add()];
            *keyValuePtr = key;
            V* valuePtr = (V*)(keyValuePtr + 1);
            *valuePtr = default;
            return ref *valuePtr;
        }

        public void Add(K key, V value)
        {
            Resize(Length + 1);
            uint hash = (uint)key.GetHashCode() % (uint)Capacity;
            ref PtrList lst = ref data->hashArray[hash];
            if (!lst.isCreated) lst = new PtrList(5, stride, alloc);
            for (int i = 0; i < lst.length; ++i)
            {
                K* keyPtr = (K*)lst[i];
                if (equalFunc.Run(ref *keyPtr, ref key))
                {
                    return;
                }
            }
            K* newKeyPtr = (K*)lst[lst.Add()];
            *newKeyPtr = key;
            int* indPtr = (int*)(newKeyPtr + 1);
            *indPtr = data->keyValueList.length;
            K* keyValuePtr = (K*)data->keyValueList[data->keyValueList.Add()];
            *keyValuePtr = key;
            V* valuePtr = (V*)(keyValuePtr + 1);
            *valuePtr = value;
        }

        public void Dispose()
        {
            if (data == null) return;
            for (int i = 0; i < Capacity; ++i)
            {
                data->hashArray[i].Dispose();
            }
            UnsafeUtility.Free(data->hashArray, alloc);
            data->keyValueList.Dispose();
            isCreated = false;
            data->hashArray = null;
            UnsafeUtility.Free(data, alloc);
            data = null;
        }

        public bool Get(K key, out V value)
        {
            int ind = GetIndex(ref key);
            if (ind < 0)
            {
                value = default;
                return false;
            }
            value = *(V*)(data->keyValueList[ind] + sizeof(K));
            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IEnumerator<Pair<K, V>> GetEnumerator()
        {
            DictionaryIEnumerator dict = new DictionaryIEnumerator
            {
                keyValueArray = &data->keyValueList,
                iteIndex = -1
            };
            return dict;
        }
        public struct DictionaryIEnumerator : IEnumerator<Pair<K, V>>
        {
            public PtrList* keyValueArray;
            public int iteIndex;
            public Pair<K, V> Current
            {
                get
                {
                    Pair<K, V> pair;
                    K* keyPtr = (K*)(*keyValueArray)[iteIndex];
                    pair.key = *keyPtr;
                    pair.value = *(V*)(keyPtr + 1);
                    return pair;
                }
            }
            object IEnumerator.Current
            {
                get
                {
                    Pair<K, V> pair;
                    K* keyPtr = (K*)(*keyValueArray)[iteIndex];
                    pair.key = *keyPtr;
                    pair.value = *(V*)(keyPtr + 1);
                    return pair;
                }
            }
            public bool MoveNext()
            {
                return (++iteIndex < keyValueArray->length);
            }
            public void Reset()
            {
                iteIndex = -1;
            }
            public void Dispose()
            {

            }
        }
    }
}