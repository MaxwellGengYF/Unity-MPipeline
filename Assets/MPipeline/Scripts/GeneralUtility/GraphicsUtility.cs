using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using UnityEngine.Rendering;

public static class GraphicsUtility
{
    /// <summary>
    /// Full Screen triangle Mesh
    /// </summary>
    public static Mesh mesh
    {
        get
        {
            if (m_mesh != null)
                return m_mesh;
            UpdatePlatform();
            m_mesh = new Mesh();
            if (isD3D)
            {
                m_mesh.vertices = new Vector3[] {
                    new Vector3(-3, -1, 0f),
                    new Vector3(1, -1, 0f),
                    new Vector3(1, 3, 0f)
                };
            }
            else
            {
                m_mesh.vertices = new Vector3[] {
                    new Vector3(-3, 1, 0f),
                    new Vector3(1, 1, 0f),
                    new Vector3(1, -3, 0f)
                };
            }

            m_mesh.uv = new Vector2[] {
                new Vector2(-1,1),
                new Vector2(1, 1),
                new Vector2(1, -1)
            };

            m_mesh.SetIndices(new int[] { 0, 1, 2 }, MeshTopology.Triangles, 0);
            return m_mesh;
        }
    }

    public static Mesh cubeMesh
    {
        get
        {
            if (m_cubeMesh != null) return m_cubeMesh;
            m_cubeMesh = new Mesh();
            m_cubeMesh.vertices = new Vector3[]
            {
                new Vector3(0.5f, -0.5f, 0.5f),
                new Vector3(-0.5f, -0.5f, 0.5f),
                new Vector3(0.5f, 0.5f, 0.5f),
                new Vector3(-0.5f, 0.5f, 0.5f),
                new Vector3(0.5f, 0.5f, -0.5f),
                new Vector3(-0.5f, 0.5f, -0.5f),
                new Vector3(0.5f, -0.5f, -0.5f),
                new Vector3(-0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, 0.5f, 0.5f),
                new Vector3(-0.5f, 0.5f, 0.5f),
                new Vector3(0.5f, 0.5f, -0.5f),
                new Vector3(-0.5f, 0.5f, -0.5f),
                new Vector3(0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, -0.5f, 0.5f),
                new Vector3(-0.5f, -0.5f, 0.5f),
                new Vector3(-0.5f, -0.5f, -0.5f),
                new Vector3(-0.5f, -0.5f, 0.5f),
                new Vector3(-0.5f, 0.5f, 0.5f),
                new Vector3(-0.5f, 0.5f, -0.5f),
                new Vector3(-0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, 0.5f, -0.5f),
                new Vector3(0.5f, 0.5f, 0.5f),
                new Vector3(0.5f, -0.5f, 0.5f),
            };
            m_cubeMesh.normals = new Vector3[]
            {
                new Vector3(0f, 0f, 1f),
new Vector3(0f, 0f, 1f),
new Vector3(0f, 0f, 1f),
new Vector3(0f, 0f, 1f),
new Vector3(0f, 1f, 0f),
new Vector3(0f, 1f, 0f),
new Vector3(0f, 0f, -1f),
new Vector3(0f, 0f, -1f),
new Vector3(0f, 1f, 0f),
new Vector3(0f, 1f, 0f),
new Vector3(0f, 0f, -1f),
new Vector3(0f, 0f, -1f),
new Vector3(0f, -1f, 0f),
new Vector3(0f, -1f, 0f),
new Vector3(0f, -1f, 0f),
new Vector3(0f, -1f, 0f),
new Vector3(-1f, 0f, 0f),
new Vector3(-1f, 0f, 0f),
new Vector3(-1f, 0f, 0f),
new Vector3(-1f, 0f, 0f),
new Vector3(1f, 0f, 0f),
new Vector3(1f, 0f, 0f),
new Vector3(1f, 0f, 0f),
new Vector3(1f, 0f, 0f)

            };
            m_cubeMesh.triangles = new int[]
            {
                0, 2, 3, 0, 3, 1, 8, 4, 5, 8, 5, 9, 10, 6, 7, 10, 7, 11, 12, 13, 14, 12, 14, 15, 16, 17, 18, 16, 18, 19, 20, 21, 22, 20, 22, 23,
            };
            return m_cubeMesh;
        }
    }
    public static bool platformIsD3D { get { return isD3D; } }
    private static bool isD3D = true;
    private static Mesh m_cubeMesh;
    private static Mesh m_mesh;
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void UpdatePlatform()
    {
        isD3D = SystemInfo.graphicsDeviceVersion.IndexOf("Direct3D") > -1;
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void BlitMRT(this CommandBuffer buffer, RenderTargetIdentifier[] colorIdentifier, RenderTargetIdentifier depthIdentifier, Material mat, int pass)
    {
        buffer.SetRenderTarget(colorIdentifier, depthIdentifier);
        buffer.DrawMesh(mesh, Matrix4x4.identity, mat, 0, pass);
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void BlitSRT(this CommandBuffer buffer, RenderTargetIdentifier destination, Material mat, int pass)
    {
        buffer.SetRenderTarget(destination);
        buffer.DrawMesh(mesh, Matrix4x4.identity, mat, 0, pass);
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void BlitSRTWithDepth(this CommandBuffer buffer, RenderTargetIdentifier destination, RenderTargetIdentifier depth, Material mat, int pass)
    {
        buffer.SetRenderTarget(destination, depth);
        buffer.DrawMesh(mesh, Matrix4x4.identity, mat, 0, pass);
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void BlitMRT(this CommandBuffer buffer, Texture source, RenderTargetIdentifier[] colorIdentifier, RenderTargetIdentifier depthIdentifier, Material mat, int pass)
    {
        buffer.SetRenderTarget(colorIdentifier, depthIdentifier);
        buffer.DrawMesh(mesh, Matrix4x4.identity, mat, 0, pass);
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void BlitSRT(this CommandBuffer buffer, Texture source, RenderTargetIdentifier destination, Material mat, int pass)
    {
        buffer.SetGlobalTexture(ShaderIDs._MainTex, source);
        buffer.SetRenderTarget(destination);
        buffer.DrawMesh(mesh, Matrix4x4.identity, mat, 0, pass);
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void BlitSRT(this CommandBuffer buffer, RenderTargetIdentifier source, RenderTargetIdentifier destination, Material mat, int pass)
    {
        buffer.SetGlobalTexture(ShaderIDs._MainTex, source);
        buffer.SetRenderTarget(destination);
        buffer.DrawMesh(mesh, Matrix4x4.identity, mat, 0, pass);
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void BlitSRT(this CommandBuffer buffer, RenderTargetIdentifier source, RenderTargetIdentifier destination, RenderTargetIdentifier depth, Material mat, int pass)
    {
        buffer.SetGlobalTexture(ShaderIDs._MainTex, source);
        buffer.SetRenderTarget(destination, depth);
        buffer.DrawMesh(mesh, Matrix4x4.identity, mat, 0, pass);
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void BlitSRT(this CommandBuffer buffer, RenderTargetIdentifier source, RenderTargetIdentifier destination, Material mat, int pass, MaterialPropertyBlock block)
    {
        buffer.SetGlobalTexture(ShaderIDs._MainTex, source);
        buffer.SetRenderTarget(destination);
        buffer.DrawMesh(mesh, Matrix4x4.identity, mat, 0, pass, block);
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void BlitSRT(this CommandBuffer buffer, RenderTargetIdentifier source, RenderTargetIdentifier destination, RenderTargetIdentifier depth, Material mat, int pass, MaterialPropertyBlock block)
    {
        buffer.SetGlobalTexture(ShaderIDs._MainTex, source);
        buffer.SetRenderTarget(destination, depth);
        buffer.DrawMesh(mesh, Matrix4x4.identity, mat, 0, pass, block);
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void BlitSRT(this CommandBuffer buffer, MaterialPropertyBlock block, RenderTargetIdentifier source, RenderTargetIdentifier destination, Material mat, int pass)
    {
        buffer.SetGlobalTexture(ShaderIDs._MainTex, source);
        buffer.SetRenderTarget(destination);
        buffer.DrawMesh(mesh, Matrix4x4.identity, mat, 0, pass, block);
    }//Use This
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void BlitStencil(this CommandBuffer buffer, RenderTargetIdentifier colorSrc, RenderTargetIdentifier colorBuffer, RenderTargetIdentifier depthStencilBuffer, Material mat, int pass)
    {
        buffer.SetGlobalTexture(ShaderIDs._MainTex, colorSrc);
        buffer.SetRenderTarget(colorBuffer, depthStencilBuffer);
        buffer.DrawMesh(mesh, Matrix4x4.identity, mat, 0, pass);
    }//UseThis
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void SetKeyword(this CommandBuffer buffer, string keyword, bool value)
    {
        if (value) buffer.EnableShaderKeyword(keyword);
        else buffer.DisableShaderKeyword(keyword);
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void BlitStencil(this CommandBuffer buffer, RenderTargetIdentifier colorBuffer, RenderTargetIdentifier depthStencilBuffer, Material mat, int pass)
    {
        buffer.SetRenderTarget(colorBuffer, depthStencilBuffer);
        buffer.DrawMesh(mesh, Matrix4x4.identity, mat, 0, pass);
    }
    public static float4x4 GetGPUProjectionMatrix(float4x4 projection, bool renderTexture)
    {
        if (isD3D)
        {
            if (projection.c3.w < 0.5f)
            {
                float m22 = -projection.c2.z;
                float m32 = -projection.c3.z;
                float far = (2.0f * m32) / (2.0f * m22 - 2.0f);
                float near = ((m22 - 1.0f) * far) / (m22 + 1.0f);
                projection.c2.z = m22 * (near / (near + far));
                projection.c3.z = m32 * 0.5f;
                if (renderTexture)
                {
                    projection.c1 = -projection.c1;
                }
            }
            else
            {
                projection.c2.z *= -0.5f;
                projection.c3.z = (-projection.c3.z - 1) * 0.5f + 1;
                if (renderTexture)
                {
                    projection.c1 = -projection.c1;
                }
            }
            return projection;
        }
        return projection;
    }
    public static float4x4 GetGPUProjectionMatrix(float4x4 projection, bool renderTexture, bool isD3D)
    {
        if (isD3D)
        {
            if (projection.c3.w < 0.5f)
            {
                float m22 = -projection.c2.z;
                float m32 = -projection.c3.z;
                float far = (2.0f * m32) / (2.0f * m22 - 2.0f);
                float near = ((m22 - 1.0f) * far) / (m22 + 1.0f);
                projection.c2.z = m22 * (near / (near + far));
                projection.c3.z = m32 * 0.5f;
                if (renderTexture)
                {
                    projection.c1 = -projection.c1;
                }
            }
            else
            {
                projection.c2.z *= -0.5f;
                projection.c3.z = (-projection.c3.z - 1) * 0.5f + 1;
                if (renderTexture)
                {
                    projection.c1 = -projection.c1;
                }
            }
            return projection;
        }
        return projection;
    }

    public static void CopyToTexture2D(RenderTexture source, Texture2D dest)
    {
        RenderTexture.active = source;
        dest.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
    }
}
