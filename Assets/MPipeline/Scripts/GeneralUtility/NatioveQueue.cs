using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using System.Collections;
using System.Collections.Generic;
namespace MPipeline
{
    public unsafe struct NativeQueueData
    {
        public void* startNode;
        public void* endNode;
        public int length;
        public Allocator alloc;
    }
    public unsafe struct NativeQueue<T>where T : unmanaged
    {
        public struct Node
        {
            T value;
            void* next;
            public static ref void* GetNext(void* nodePtr)
            {
                void** nextPtr = (void**)(1 + (T*)nodePtr);
                return ref *nextPtr;
            }
        }
        [NativeDisableUnsafePtrRestriction]
        private NativeQueueData* queueData;
        private NativeList<ulong> nodes;
        private int capacity;
        public int Length => queueData->length;
        public bool isCreated => queueData != null;
        public NativeQueue(int capacity, Allocator alloc)
        {
            capacity = Unity.Mathematics.math.max(1, capacity);
            this.capacity = capacity;
            nodes = new NativeList<ulong>(capacity, alloc);
            for (int i = 0; i < capacity; ++i)
            {
                void* ptr = MUnsafeUtility.Malloc(sizeof(T) + sizeof(ulong), alloc);
                UnsafeUtility.MemClear(ptr, sizeof(T) + sizeof(ulong));
                nodes.Add((ulong)ptr);
            }
            queueData = MUnsafeUtility.Malloc<NativeQueueData>(sizeof(NativeQueueData), alloc);
            UnsafeUtility.MemClear(queueData, sizeof(NativeQueueData));
            queueData->alloc = alloc;
        }

        public void Add(T value)
        {
            if (!isCreated) return;
            if (nodes.Length <= 0)
            {
                for (int i = 0; i < capacity; ++i)
                {
                    void* ptr = MUnsafeUtility.Malloc(sizeof(T) + sizeof(ulong), queueData->alloc);
                    UnsafeUtility.MemClear(ptr, sizeof(T) + sizeof(ulong));
                    nodes.Add((ulong)ptr);
                }
            }
            void* nodePtr = (void*)nodes[nodes.Length - 1];
            *(T*)nodePtr = value;
            nodes.RemoveLast();
            if (queueData->length <= 0)
            {
                queueData->startNode = nodePtr;
                queueData->endNode = nodePtr;
            }
            else
            {
                ref void* lastNodeNextPos = ref Node.GetNext(queueData->endNode);
                lastNodeNextPos = nodePtr;
                queueData->endNode = nodePtr;
            }
            queueData->length++;
        }

        public T Dequeue()
        {
            if (!isCreated || queueData->length <= 0 || queueData->startNode == null)
            {
                return default;
            }
            T value = *(T*)queueData->startNode;
            ref void* next = ref Node.GetNext(queueData->startNode);
            nodes.Add((ulong)queueData->startNode);
            queueData->startNode = next;
            next = null;
            queueData->length--;
            return value;
        }

        public bool TryDequeue(out T value)
        {
            if (!isCreated || queueData->length <= 0 || queueData->startNode == null)
            {
                value = default;
                return false;
            }
            value = *(T*)queueData->startNode;
            ref void* next = ref Node.GetNext(queueData->startNode);
            nodes.Add((ulong)queueData->startNode);
            queueData->startNode = next;
            next = null;
            queueData->length--;
            return true;
        }

        public void Dispose()
        {
            if (!isCreated) return;
            Allocator alloc = queueData->alloc;
            void* ptr = queueData->startNode;
            while (ptr != null)
            {
                ref void* nextPtr = ref Node.GetNext(ptr);
                UnsafeUtility.Free(ptr, alloc);
                ptr = nextPtr;
            }
            foreach (var i in nodes)
            {
                UnsafeUtility.Free((void*)i, alloc);
            }
            nodes.Dispose();
            MUnsafeUtility.SafeFree(ref queueData, alloc);
        }
    }
}