using System.Collections;
using System.Collections.Generic;
using System;
using System.Threading;
public static class ObjectPool
{
    private static Dictionary<Type, ArrayList> allObject = new Dictionary<Type, ArrayList>();
    public static int initSize = 50;
    public static T Get<T>() where T : class, new()
    {
        ArrayList arrayList;
        if (allObject.TryGetValue(typeof(T), out arrayList))
        {
            if (arrayList.Count > 0)
            {
                int count = arrayList.Count - 1;
                T value = (T)arrayList[count];
                arrayList.RemoveAt(count);
                return value;
            }
            else
            {
                for (int i = 0; i < initSize; ++i)
                {
                    arrayList.Add(new T());
                }
                return new T();
            }
        }
        else
        {
            arrayList = new ArrayList(initSize);
            for (int i = 0; i < initSize; ++i)
            {
                arrayList.Add(new T());
            }
            allObject.Add(typeof(T), arrayList);
            return new T();
        }
    }

    public static bool Release<T>(T target) where T : class
    {
        ArrayList ar;
        if (allObject.TryGetValue(typeof(T), out ar))
        {
            ar.Add(target);
            return true;
        }
        else
        {
            return false;
        }
    }

    public static bool ClearType<T>()
    {
        ArrayList ar;
        if (allObject.TryGetValue(typeof(T), out ar))
        {
            allObject[typeof(T)] = new ArrayList(initSize);
            ThreadPool.QueueUserWorkItem(ClearCurrent, ar);
            return true;
        }
        return false;
    }

    private static void ClearCurrent(object target)
    {
        var ar = (ArrayList)target;
        for (int i = 0; i < ar.Count; ++i)
        {
            ar[i] = null;
        }
        target = null;
        ar = null;
        Thread.Sleep(500);
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, false, false);
    }

    private static void ClearCurrent(ArrayList ar)
    {
        for (int i = 0; i < ar.Count; ++i)
        {
            ar[i] = null;
        }
    }

    public static void Clear()
    {
        ThreadPool.QueueUserWorkItem(Clear, allObject);
        allObject = new Dictionary<Type, ArrayList>();
    }

    private static void Clear(object targetDict)
    {
        Dictionary<Type, ArrayList> dict = (Dictionary<Type, ArrayList>)targetDict;
        foreach (var i in dict.Values)
        {
            ClearCurrent(i);
        }
        dict = null;
        Thread.Sleep(500);
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, false, false);
    }
}
