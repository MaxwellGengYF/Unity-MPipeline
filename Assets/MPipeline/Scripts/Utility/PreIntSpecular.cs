using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[ExecuteInEditMode]
public class PreIntSpecular : MonoBehaviour
{
    private static int _PreintegratedLUT = Shader.PropertyToID("_PreintegratedLUT");
    static Texture2D preintIBL = null;
#if UNITY_EDITOR
    private void Update()
#else
    private void Awake()
#endif
    {
        if(!preintIBL)
        {
            preintIBL = Resources.Load<Texture2D>("PreIntIBL");
        }
        Shader.SetGlobalTexture(_PreintegratedLUT, preintIBL);
    }
}
