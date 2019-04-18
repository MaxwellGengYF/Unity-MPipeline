using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace MPipeline
{
    public unsafe struct SortElement
    {
        public int leftValue;
        public int rightValue;
        public float sign;
        public int value;
    }
    public unsafe struct BinarySort
    {
        private NativeArray<SortElement> elements;
        private NativeArray<int> results;
        
        public int count;
        public BinarySort(int capacity, Allocator alloc)
        {
            elements = new NativeArray<SortElement>(capacity, alloc, NativeArrayOptions.UninitializedMemory);
            results = new NativeArray<int>(capacity, alloc, NativeArrayOptions.UninitializedMemory);
            count = 0;
        }

        public void Add(float sign, int value)
        {
            if (count > elements.Length) return;
            int last = Interlocked.Increment(ref count) - 1;
            SortElement curt;
            curt.sign = sign;
            curt.value = value;
            curt.leftValue = -1;
            curt.rightValue = -1;
            elements[last] = curt;
        }

        public void Clear()
        {
            count = 0;
        }

        public void Dispose()
        {
            elements.Dispose();
            results.Dispose();
        }

        public void Sort()
        {
            if (elements.Length == 0) return;
            for (int i = 1; i < elements.Length; ++i)
            {
                int currentIndex = 0;
                STARTFIND:
                SortElement* currentIndexValue = (SortElement*)elements.GetUnsafePtr() + currentIndex;
                if (((SortElement*)elements.GetUnsafePtr() + i)->sign < currentIndexValue->sign)
                {
                    if (currentIndexValue->leftValue < 0)
                    {
                        currentIndexValue->leftValue = i;
                    }
                    else
                    {
                        currentIndex = currentIndexValue->leftValue;
                        goto STARTFIND;
                    }
                }
                else
                {
                    if (currentIndexValue->rightValue < 0)
                    {
                        currentIndexValue->rightValue = i;
                    }
                    else
                    {
                        currentIndex = currentIndexValue->rightValue;
                        goto STARTFIND;
                    }
                }
            }
            int start = 0;
            Iterate(0, ref start);
        }

        private void Iterate(int i, ref int targetLength)
        {
            int leftValue = elements[i].leftValue;
            if (leftValue >= 0)
            {
                Iterate(leftValue, ref targetLength);
            }
            results[targetLength] = ((SortElement*)elements.GetUnsafePtr() + i)->value;
            targetLength++;
            int rightValue = elements[i].rightValue;
            if (rightValue >= 0)
            {
                Iterate(rightValue, ref targetLength);
            }
        }
    }
}