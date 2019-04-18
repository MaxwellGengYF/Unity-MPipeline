#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using MPipeline;
using Unity.Mathematics;
using static Unity.Mathematics.math;
public class CombineMesh : ScriptableWizard
{
    [MenuItem("MPipeline/Combine Mesh")]
    private static void CreateWizard()
    {
        DisplayWizard<CombineMesh>("Scene Tools", "Create");
    }
    public string combineMeshPath = "Assets/";
    private static Mesh CombineAllMesh(List<MeshFilter> meshes)
    {
        List<Vector3> verts = new List<Vector3>(1000);
        List<Vector3> norms = new List<Vector3>(1000);
        List<Vector4> tans = new List<Vector4>(1000);
        List<Vector2> uv0s = new List<Vector2>(1000);
        List<int> tris = new List<int>(1000);
        float4x4 worldToLocal = meshes[0].transform.worldToLocalMatrix;

        foreach (var i in meshes)
        {
            float4x4 localToWorld = mul(worldToLocal, i.transform.localToWorldMatrix);
            float3x3 localToWorldRot = float3x3(localToWorld.c0.xyz, localToWorld.c1.xyz, localToWorld.c2.xyz);
            Vector3[] vertices = i.sharedMesh.vertices;
            for (int j = 0; j < vertices.Length; ++j)
            {
                vertices[j] = mul(localToWorld, float4(vertices[j], 1)).xyz;
            }
            Vector3[] normals = i.sharedMesh.normals;
            for (int j = 0; j < vertices.Length; ++j)
            {
                normals[j] = mul(localToWorldRot, normals[j]);
            }
            Vector4[] tangents = i.sharedMesh.tangents;
            for (int j = 0; j < vertices.Length; ++j)
            {
                float3 tan = (Vector3)tangents[j];
                float tanW = tangents[j].w;
                tangents[j] = (Vector3)mul(localToWorldRot, tan);
                tangents[j].w = tanW;
            }
            Vector2[] uv0 = i.sharedMesh.uv;
            int[] triangles = i.sharedMesh.triangles;
            for (int j = 0; j < triangles.Length; ++j)
            {
                triangles[j] += verts.Count;
            }
            tris.AddRange(triangles);
            verts.AddRange(vertices);
            norms.AddRange(normals.Length == vertices.Length ? normals : new Vector3[vertices.Length]);
            tans.AddRange(tangents.Length == vertices.Length ? tangents : new Vector4[vertices.Length]);
            uv0s.AddRange(uv0.Length == vertices.Length ? uv0 : new Vector2[vertices.Length]);
        }
        Mesh newMesh = new Mesh();
        newMesh.SetVertices(verts);
        newMesh.SetUVs(0, uv0s);
        newMesh.SetNormals(norms);
        newMesh.SetTangents(tans);
        newMesh.SetTriangles(tris, 0);
        Unwrapping.GenerateSecondaryUVSet(newMesh);
        return newMesh;
    }
    private void OnWizardCreate()
    {
        Transform[] transes = Selection.GetTransforms(SelectionMode.Unfiltered);
        List<MeshFilter> renderers = new List<MeshFilter>();
        foreach (var i in transes)
        {
            renderers.AddRange(i.GetComponentsInChildren<MeshFilter>());
        }
        if (renderers.Count == 0) return;
        Mesh combinedMesh = CombineAllMesh(renderers);
        AssetDatabase.CreateAsset(combinedMesh, combineMeshPath + combinedMesh.GetInstanceID() + ".asset");
        renderers[0].sharedMesh = combinedMesh;
        for (int i = 1; i < renderers.Count; ++i)
        {
            DestroyImmediate(renderers[i].gameObject);
        }
    }
}
/*
public class InitBakery : ScriptableWizard
{
    [MenuItem("MPipeline/Init Bakery")]
    private static void CreateWizard()
    {
        DisplayWizard<InitBakery>("Bakery", "Create");
    }
    private void OnWizardCreate()
    {
        Transform[] trans = Selection.GetTransforms(SelectionMode.Unfiltered);
        List<Light> lights = new List<Light>();
        foreach (var i in trans)
            lights.AddRange(i.GetComponentsInChildren<Light>());
        foreach(var i in lights)
        {
            var lt = i.gameObject.AddComponent<BakeryPointLight>();
            lt.color = i.color;
            lt.projMode = i.type == LightType.Point ? BakeryPointLight.ftLightProjectionMode.Omni : BakeryPointLight.ftLightProjectionMode.Cookie;
            lt.angle = i.spotAngle;
            lt.cutoff = i.range;
            lt.intensity = i.intensity;
            lt.realisticFalloff = true;
        }
    }
}*/
public class TransformShader : ScriptableWizard
{
    public Shader originShader;
    public Shader targetShader;
    [MenuItem("MPipeline/Transform Shader")]
    private static void CreateWizard()
    {
        DisplayWizard<TransformShader>("Change Shader", "Change");
    }
    private void OnWizardCreate()
    {
        Transform[] trans = Selection.GetTransforms(SelectionMode.Unfiltered);
        List<MeshRenderer> lights = new List<MeshRenderer>();
        foreach (var i in trans)
            lights.AddRange(i.GetComponentsInChildren<MeshRenderer>());
        Dictionary<Material, MeshRenderer> allMats = new Dictionary<Material, MeshRenderer>();
        foreach(var i in lights)
        {
            var mats = i.sharedMaterials;
            foreach(var j in mats)
            {
                allMats[j] = i;
            }
        }
        foreach(var i in allMats.Keys)
        {
            if (i.shader == originShader)
                i.shader = targetShader;
        }
    }
}
#endif
