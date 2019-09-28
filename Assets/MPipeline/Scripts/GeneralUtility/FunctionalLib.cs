using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
namespace MPipeline
{
    public static class FunctionalLib
    {
        public static void SetValues<T>(this T[] arrays, Func<int, T> func)
        {
            for (int i = 0; i < arrays.Length; ++i)
            {
                arrays[i] = func(i);
            }
        }

        public static void SetValues<T, P>(this T[] arrays, P obj, Func<int, P, T> func)
        {
            for (int i = 0; i < arrays.Length; ++i)
            {
                arrays[i] = func(i, obj);
            }
        }

        public static void Iterate<T>(this T[] arrays, Action<T> func)
        {
            for (int i = 0; i < arrays.Length; ++i)
            {
                func(arrays[i]);
            }
        }

        public static void Iterate<T, P>(this T[] arrays, P obj, Action<T, P> func)
        {
            for (int i = 0; i < arrays.Length; ++i)
            {
                func(arrays[i], obj);
            }
        }
    }
}