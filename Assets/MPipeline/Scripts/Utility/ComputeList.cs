using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;

public class Cube
{
    
    //TODO
    public struct CubeInfo
    {
        public float value;
    }
    public Cube(float value)
    {
        info.value = value;
    }
    public CubeInfo info;
    public int currentIndex;
}

public unsafe struct ComputeList
{
    public ComputeBuffer buffer
    {
        get; private set;
    }
    public int capacity
    {
        get; private set;
    }
    public Cube[] bufferArray
    {
        get; private set;
    }
    public int count
    {
        get; private set;
    }

    public ComputeList(int maximumCapacity)
    {
        count = 0;
        capacity = maximumCapacity;
        buffer = new ComputeBuffer(maximumCapacity, sizeof(Cube.CubeInfo));
        bufferArray = new Cube[maximumCapacity];
    }
    /// <summary>
    /// Add an object to compute buffer
    /// </summary>
    /// <param name="obj"></param> target object's reference
    /// <returns></returns> if this operation avaliable
    public bool Add(ref Cube obj)
    {
        if (count >= capacity)
            return false;
        obj.currentIndex = count;
        bufferArray[count] = obj;
        NativeArray<Cube.CubeInfo> currentCubeInfo = new NativeArray<Cube.CubeInfo>(1, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        currentCubeInfo[0] = obj.info;
        buffer.SetData(currentCubeInfo, 0, count, 1);
        currentCubeInfo.Dispose();
        count++;
        return true;
    }
    /// <summary>
    /// Remove an object from compute buffer
    /// </summary>
    /// <param name="targetIndex"></param>
    /// <returns></returns>
    public bool Remove(int targetIndex)
    {
        if (count < 1)
        {
            return false;
        }
        else if (count == 1)
        {
            count = 0;
        }
        else
        {
            count--;
            bufferArray[targetIndex] = bufferArray[count];
            bufferArray[targetIndex].currentIndex = targetIndex;
            NativeArray<Cube.CubeInfo> currentCubeInfo = new NativeArray<Cube.CubeInfo>(1, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            currentCubeInfo[0] = bufferArray[targetIndex].info;
            buffer.SetData(currentCubeInfo, 0, targetIndex, 1);
            currentCubeInfo.Dispose();
        }
        return true;
    }
    /// <summary>
    /// Add an array to the cube
    /// </summary>
    /// <param name="allObjs"></param>
    /// <param name="targetLength"></param>
    /// <returns></returns>
    public bool AddArray(Cube[] allObjs, int targetLength)
    {
        if((count + targetLength) > capacity)
        {
            return false;
        }
        NativeArray<Cube.CubeInfo> currentCubeInfo = new NativeArray<Cube.CubeInfo>(targetLength, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        for(int i = 0; i < targetLength; ++i)
        {
            int currentCount = i + count;
            bufferArray[currentCount] = allObjs[i];
            bufferArray[currentCount].currentIndex = currentCount;
            currentCubeInfo[i] = allObjs[i].info;
        }
        buffer.SetData(currentCubeInfo, 0, count, targetLength);
        currentCubeInfo.Dispose();
        count += targetLength;
        return true;
    }

    public void Dispose()
    {
        buffer.Dispose();
    }
}
