using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using MPipeline;
using UnityEngine.Rendering;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Random = UnityEngine.Random;
public unsafe sealed class RainDrop : CustomDrawRequest
{
    public PipelineCamera cam;
    private CommandBuffer buffer;
    private ComputeBuffer posBuffer;
    private ComputeBuffer speedBuffer;
    public Material mat;
    public RenderTexture depthTex;
    public ComputeShader runShader;
    public Vector2Int rtSize = new Vector2Int(256, 256);
    public bool updateDepth = false;
    public float speed = 2;
    private PipelineCamera depthCam;
    private static readonly int _InstancePos = Shader.PropertyToID("_InstancePos");
    private static readonly int _RainDepthTex = Shader.PropertyToID("_RainDepthTex");
    private static readonly int _DepthVPMatrix = Shader.PropertyToID("_DepthVPMatrix");
    private static readonly int _InvDepthVPMatrix = Shader.PropertyToID("_InvDepthVPMatrix");
    private static readonly int _RunSpeedBuffer = Shader.PropertyToID("_RunSpeedBuffer");
    private const int size = 8192;
    
    private void Awake()
    {
        posBuffer = new ComputeBuffer(size, sizeof(float3));
        speedBuffer = new ComputeBuffer(size, sizeof(float));
        depthTex = new RenderTexture(rtSize.x, rtSize.y, 0, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear);
        Camera cam = GetComponent<Camera>();
        if (!cam) cam = gameObject.AddComponent<Camera>();
        depthCam = GetComponent<PipelineCamera>();
        if (!depthCam) depthCam = gameObject.AddComponent<PipelineCamera>();
        depthCam.cam = cam;
        cam.orthographic = true;
        cam.nearClipPlane = -transform.localScale.z * 0.5f;
        cam.farClipPlane = transform.localScale.z * 0.5f;
        cam.orthographicSize = transform.localScale.y * 0.5f;
        cam.aspect = transform.localScale.x / transform.localScale.y;
        cam.targetTexture = depthTex;
        cam.enabled = false;
        depthCam.renderingPath = PipelineResources.CameraRenderingPath.Unlit;
        NativeArray<float3> data = new NativeArray<float3>(size, Allocator.Temp);
        NativeArray<float> speeds = new NativeArray<float>(size, Allocator.Temp);
        Matrix4x4 vp = GL.GetGPUProjectionMatrix(cam.projectionMatrix, false) * cam.worldToCameraMatrix;
        Matrix4x4 invvp = vp.inverse;
        for (int i = 0; i < data.Length; ++i)
        {
            data[i] = invvp.MultiplyPoint(new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f)));
            speeds[i] = Random.Range(0.7f, 1f);
        }
        speedBuffer.SetData(speeds);
        posBuffer.SetData(data);
        MPipeline.RenderPipeline.AddPreRenderCamera(depthCam);
    }
    protected override void DrawCommand(out bool drawGBuffer, out bool drawShadow, out bool drawTransparent)
    {
        drawGBuffer = false;
        drawShadow = false;
        drawTransparent = true;
    }
    public override void FinishJob()
    {
        localToWorldMatrix = transform.localToWorldMatrix;
    }
    Matrix4x4 vp; Matrix4x4 invvp;
    private void Update()
    {
        if(cam.cam)
        transform.position = cam.cam.transform.position;
        transform.eulerAngles = new Vector3(90, 0, 0);
        vp = GL.GetGPUProjectionMatrix(depthCam.cam.projectionMatrix, false) * depthCam.cam.worldToCameraMatrix;
        invvp = vp.inverse;
        if(updateDepth)
            MPipeline.RenderPipeline.AddPreRenderCamera(depthCam);
        buffer = MPipeline.RenderPipeline.BeforeFrameBuffer;
        buffer.SetGlobalFloat(ShaderIDs._DeltaTime, Time.deltaTime * speed);
        buffer.SetComputeBufferParam(runShader, 0, _RunSpeedBuffer, speedBuffer);
        buffer.SetComputeTextureParam(runShader, 0, _RainDepthTex, depthTex);
        buffer.SetComputeBufferParam(runShader, 0, _InstancePos, posBuffer);
        buffer.DispatchCompute(runShader, 0, size / 64, 1, 1);
        
    }
    public override void DrawTransparent(CommandBuffer buffer)
    {
        buffer.SetGlobalFloat(ShaderIDs._DeltaTime, Time.deltaTime * speed);
        buffer.SetGlobalBuffer(_InstancePos, posBuffer);
        buffer.SetGlobalMatrix(_InvDepthVPMatrix, invvp);
        buffer.SetGlobalMatrix(_DepthVPMatrix, vp);
        buffer.DrawProcedural(Matrix4x4.identity, mat, 0, MeshTopology.Triangles, 6, size);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.4f, 0.6f, 1);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
    }

    private void OnDestroy()
    {
        Destroy(depthTex);
        posBuffer.Dispose();
        speedBuffer.Dispose();
    }
}
