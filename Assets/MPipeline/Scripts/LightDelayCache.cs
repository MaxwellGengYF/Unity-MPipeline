using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[RequireComponent(typeof(MLight))]
public class LightDelayCache : MonoBehaviour
{
    private void Start()
    {
        StartCoroutine(delaySet());
    }
    IEnumerator delaySet()
    {
        MLight ml = GetComponent<MLight>();
        ml.useShadowCache = false;
        yield return null;
        yield return null;
        ml.useShadowCache = true;
    }
}
