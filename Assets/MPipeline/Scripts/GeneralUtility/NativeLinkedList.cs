using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MPipeline;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using System;
using System.Runtime.CompilerServices;
using static MUnsafeUtility;
public unsafe struct NativeLinkData
{
    public UIntPtr start;
    public UIntPtr end;
    public int length;
}
public unsafe struct NativeLinkedList<T> : IEnumerable where T : unmanaged
{
    private Allocator alloc;
    private NativeLinkData* data;
    private void* function;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void** GetPrevious(UIntPtr node)
    {
        return (void**)node.ToPointer();
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T* GetValuePtr(UIntPtr node)
    {
        return (T*)(node + sizeof(void*)).ToPointer();
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void** GetNextPtr(UIntPtr node)
    {
        node += sizeof(void*) + sizeof(T);
        return (void**)node.ToPointer();
    }
    private static readonly int NodeSize = sizeof(void*) * 2 + sizeof(T);
    public NativeLinkedList(Allocator alloc, Func<T, T, bool> func)
    {
        isCreated = true;
        this.alloc = alloc;
        function = GetManagedPtr(func);
        data = Malloc<NativeLinkData>(sizeof(NativeLinkData), alloc);
        UnsafeUtility.MemClear(data, sizeof(NativeLinkData));
    }
    public void AddLast(T value)
    {
        AddLast(ref value);
    }
    public void AddLast(ref T value)
    {
        UIntPtr node = new UIntPtr(Malloc(NodeSize, alloc));
        *GetPrevious(node) = data->end.ToPointer();
        *GetNextPtr(node) = null;
        if (data->end.ToPointer() != null)
        {
            *GetNextPtr(data->end) = node.ToPointer();
        }
        if (Length == 0)
        {
            data->start = node;
        }
        *GetValuePtr(node) = value;
        data->length++;
        data->end = node;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IEnumerator<T> GetEnumerator()
    {
        return new LinkIenumerator<T>(data);
    }
    public void AddStart(T value)
    {
        AddStart(ref value);
    }
    public void AddStart(ref T value)
    {
        UIntPtr node = new UIntPtr(Malloc(NodeSize, alloc));
        *GetNextPtr(node) = data->start.ToPointer();
        *GetPrevious(node) = null;
        if (data->start.ToPointer() != null)
        {
            *GetPrevious(data->start) = node.ToPointer();
        }
        if (Length == 0)
        {
            data->end = node;
        }
        data->length++;
        *GetValuePtr(node) = value;
        data->start = node;
    }
    public void RemoveLast()
    {
        if (Length <= 0) return;
        data->length--;
        void** previous = GetPrevious(data->end);
        if (*previous != null)
        {
            *GetNextPtr(new UIntPtr(*previous)) = null;

        }
        UIntPtr prev = new UIntPtr(*previous);
        UnsafeUtility.Free(data->end.ToPointer(), alloc);
        data->end = prev;
    }

    public bool ContainsDatas(T* datas, int length)
    {
        bool* result = stackalloc bool[length];
        UnsafeUtility.MemClear(result, sizeof(bool) * length);
        Func<T, T, bool> func = GetObject<Func<T, T, bool>>(function);
        UIntPtr currentNode = data->start;

        while (currentNode.ToPointer() != null)
        {
            T* value = GetValuePtr(currentNode);
            for (int i = 0; i < length; ++i)
            {
                if (func(*value, datas[i]))
                {
                    result[i] = true;
                    break;
                }
            }
            currentNode = new UIntPtr(*GetNextPtr(currentNode));
        }
        for (int i = 0; i < length; ++i)
        {
            if (!result[i]) return false;
        }
        return true;
    }

    public void RemoveData(T* datas, int length)
    {
        Func<T, T, bool> func = GetObject<Func<T, T, bool>>(function);
        UIntPtr currentNode = data->start;

        while (currentNode.ToPointer() != null)
        {
            T* value = GetValuePtr(currentNode);
            bool contained = false;
            for (int i = 0; i < length; ++i)
            {
                if (func(*value, datas[i]))
                {
                    contained = true;
                    break;
                }
            }
            if (contained)
            {
                UIntPtr previous = new UIntPtr(*GetPrevious(currentNode));
                UIntPtr next = new UIntPtr(*GetNextPtr(currentNode));
                if (currentNode == data->start)
                {
                    data->start = next;
                }
                if (currentNode == data->end)
                {
                    data->end = previous;
                }
                if (previous.ToPointer() != null)
                {
                    *GetNextPtr(previous) = next.ToPointer();
                }
                if (next.ToPointer() != null)
                {
                    *GetPrevious(next) = previous.ToPointer();
                }
                data->length--;
                UnsafeUtility.Free(currentNode.ToPointer(), alloc);
                currentNode = next;
            }
            else
            {
                currentNode = new UIntPtr(*GetNextPtr(currentNode));
            }
        }

    }
    public void RemoveFirst()
    {
        if (Length <= 0) return;
        data->length--;
        void** next = GetNextPtr(data->start);
        if (*next != null)
        {
            *GetPrevious(new UIntPtr(*next)) = null;
        }
        UIntPtr ne = new UIntPtr(*next);
        UnsafeUtility.Free(data->start.ToPointer(), alloc);
        data->start = ne;
    }
    public T* GetStart()
    {
        if (Length <= 0) return null;
        return GetValuePtr(data->start);
    }

    public T* GetLast()
    {
        if (Length <= 0) return null;
        return GetValuePtr(data->end);
    }
    public void Dispose()
    {
        isCreated = false;
        UIntPtr start = data->start;
        for (int i = 0; i < Length; ++i)
        {
            UIntPtr next = new UIntPtr(*GetNextPtr(start));
            UnsafeUtility.Free(start.ToPointer(), alloc);
            start = next;
        }
        UnsafeUtility.Free(data, alloc);
        data = null;
    }
    public int Length
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            return data->length;
        }
    }
    public bool isCreated { get; private set; }

}


public unsafe struct LinkIenumerator<T> : IEnumerator<T> where T : unmanaged
{
    private UIntPtr ptr;
    private UIntPtr next;
    public LinkIenumerator(NativeLinkData* dataPtr)
    {
        next = dataPtr->start;
        ptr = new UIntPtr(null);
    }
    object IEnumerator.Current
    {
        get
        {
            return *NativeLinkedList<T>.GetValuePtr(ptr);
        }
    }

    public T Current
    {
        get
        {
            return *NativeLinkedList<T>.GetValuePtr(ptr);
        }
    }

    public bool MoveNext()
    {
        ptr = next;
        if (ptr.ToPointer() == null) return false;
        next = new UIntPtr(*NativeLinkedList<T>.GetNextPtr(next));
        return true;

    }

    public void Reset()
    {
    }

    public void Dispose()
    {
    }
}