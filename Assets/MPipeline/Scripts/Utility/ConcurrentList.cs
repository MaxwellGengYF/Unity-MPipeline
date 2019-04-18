using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
public class ConcurrentList<T>
{
    private T[] array;
    public int capacity {
        get
        {
            return array.Length;
        }
    }
    public int length
    {
        get
        {
            return m_length;
        }
    }
    private int m_length;
    public ref T this[int id]
    {
        get
        {
            return ref array[id];
        }
    }
    public void Clear()
    {
        Interlocked.Exchange(ref m_length, 0);
    }
    public ConcurrentList(int capacity)
    {
        capacity = Mathf.Max(1, capacity);
        array = new T[capacity];
    }
    public void Add(T value)
    {
        int currentLength = Interlocked.Increment(ref m_length);
        if (currentLength > array.Length) { 
            lock (this)
            {
                if(currentLength > array.Length)
                {
                    T[] newArray = new T[array.Length * 2];
                    for(int i = 0; i < array.Length; ++i)
                    {
                        newArray[i] = array[i];
                    }
                    array = newArray;
                }
            }
        }
        array[currentLength - 1] = value;
    }
    public ref T RemoveAndGetLast()
    {
        int last = Interlocked.Decrement(ref m_length);
        return ref array[last];
    }
}
