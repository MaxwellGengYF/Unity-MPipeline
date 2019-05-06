using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MPipeline;
[ExecuteInEditMode]
[RequireComponent(typeof(Renderer))]
public class MotionVectorDrawer : MonoBehaviour
{
    private Renderer rend;
    private static MaterialPropertyBlock block = null;
    private Matrix4x4 lastLocalToWorld;
    private void OnEnable()
    {
        if (!rend) rend = GetComponent<Renderer>();
        if (block == null) block = new MaterialPropertyBlock();
        lastLocalToWorld = transform.localToWorldMatrix;
    }

    private void OnDisable()
    {
        if(rend) rend.SetPropertyBlock(null);
    }

    private void Update()
    {
        block.SetMatrix(ShaderIDs._LastFrameModel, lastLocalToWorld);
        rend.SetPropertyBlock(block);
        lastLocalToWorld = transform.localToWorldMatrix;
    }
}
