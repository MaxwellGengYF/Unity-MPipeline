using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    public static T current { get; private set; }
    protected void InitSingleton()
    {
        if(current)
        {
            Debug.LogError(name + " Already Contained!");
            return;
        }
        current = this as T;
    }

    protected void DisposeSingleton()
    {
        if(current == this)
        {
            current = null;
        }
    }
}
