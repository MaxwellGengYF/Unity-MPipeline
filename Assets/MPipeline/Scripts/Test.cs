using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Reflection;
using static Unity.Mathematics.math;
using MPipeline;
using static Unity.Collections.LowLevel.Unsafe.UnsafeUtility;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;
public unsafe class Test : MonoBehaviour
{
    public Material mat;
    public Mesh mesh;
    public Transform trans;
    Camera cam;
    CommandBuffer bf;
    private void Awake()
    {
        cam = GetComponent<Camera>();
        bf = new CommandBuffer();
        bf.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
        bf.DrawMesh(GraphicsUtility.mesh, trans.localToWorldMatrix, mat, 0, 0);
        cam.AddCommandBuffer(CameraEvent.AfterForwardOpaque, bf);
    }
}
