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
    CommandBuffer bf;
    public Material testMat;
    Mesh mesh;
    private void Awake()
    {
        bf = new CommandBuffer();
        GetComponent<Camera>().AddCommandBuffer(CameraEvent.AfterImageEffects, bf);
        mesh = new Mesh();
        List<Vector3> vertices = new List<Vector3>(4000);
        for (int i = 0; i < 1000; ++i)
        {
            vertices.Add(new Vector3(-1, -1, i / 1000f));
            vertices.Add(new Vector3(-1, 1, i / 1000f));
            vertices.Add(new Vector3(1, 1, i / 1000f));
            vertices.Add(new Vector3(1, -1, i / 1000f));
        }
        mesh.SetVertices(vertices);
        List<int> indices = new List<int>(6000);
        for(int i = 0; i < 1000; ++i)
        {
            indices.Add(0 + i * 4);
            indices.Add(1 + i * 4);
            indices.Add(2 + i * 4);
            indices.Add(0 + i * 4);
            indices.Add(3 + i * 4);
            indices.Add(2 + i * 4);
        }
        mesh.SetTriangles(indices, 0);
        bf.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
        bf.DrawMesh(mesh, Matrix4x4.identity, testMat);
    }


    private void OnDestroy()
    {
        bf.Dispose();
        GetComponent<Camera>().RemoveAllCommandBuffers();
    }
}
