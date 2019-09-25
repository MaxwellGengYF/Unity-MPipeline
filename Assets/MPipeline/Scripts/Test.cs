using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using MPipeline;
using Unity.Jobs;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Random = UnityEngine.Random;
using UnityEngine.AddressableAssets;
using MPipeline.PCG;


public unsafe sealed class Test : MonoBehaviour
{
    public GeometryEvent evt;
    public ComputeShader shad;
    [EasyButtons.Button]
    void EnableWhiteModel()
    {
        Shader.EnableKeyword("USE_WHITE");
    }

    [EasyButtons.Button]
    void DisableWhiteModel()
    {
        Shader.DisableKeyword("USE_WHITE");
    }
    ComputeBuffer buff;
    public Mesh mesh;
    private VirtualTexture vt;
    public Texture[] allTextures;
    public Texture[] allTexturesCopy;
    private struct Int4Equal : IFunction<int4, int4, bool>
    {
        public bool Run(ref int4 a, ref int4 b)
        {
            bool4 v = a == b;
            return v.x && v.y && v.z && v.w;
        }
    }


    private void Start()
    {
        buff = new ComputeBuffer(1, sizeof(float));
        VirtualTextureFormat* formats = stackalloc VirtualTextureFormat[]
        {
            new VirtualTextureFormat(VirtualTextureSize.x256, RenderTextureFormat.ARGB32, "_ColorVT")
        };
        vt = new VirtualTexture(9, 3, formats, 1);
        var arr = vt.LoadNewTextureChunks(0, 3, Allocator.Temp);
        for (int i = 0; i < arr.Length; ++i)
        {
            Graphics.Blit(allTextures[i], vt.GetTexture(0), 0, arr[i]);
        }
        Debug.Log(vt.LeftedTextureElement);
    }
    private void Update()
    {
        vt.Update();
        if (Input.GetKeyDown(KeyCode.Space))
        {
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                {
                    vt.UnloadTexture(int2(i, j));
                    int ele = vt.LoadNewTexture(int2(i, j), 1);
                    Graphics.Blit(allTexturesCopy[ele], vt.GetTexture(0), 0, ele);
                }
            Debug.Log(vt.LeftedTextureElement);
        }
        if(Input.GetKeyDown(KeyCode.X))
        {
            vt.CombineTexture(0, 3, true);
            Debug.Log(vt.LeftedTextureElement);
        }
        /*
        if(Input.GetKeyDown(KeyCode.Space))
        {
            float2 ma = float3(Input.mousePosition).xy / float2(Screen.width, Screen.height);
            //GeometryEvent evt
            var bf = evt.afterGeometryBuffer;
            bf.SetComputeVectorParam(shad, "_UV", float4(ma, 1, 1));
            bf.SetComputeBufferParam(shad, 0, "_DistanceBuffer", buff);
            bf.SetComputeTextureParam(shad, 0, ShaderIDs._CameraDepthTexture, ShaderIDs._CameraDepthTexture);
            bf.DispatchCompute(shad, 0, 1, 1, 1);
            bf.RequestAsyncReadback(buff, (asc) =>
            {
                var arr = asc.GetData<float>();
                Debug.Log("Depth: " + arr[0]);
            });

        }*/
        ///////////GPURP Manual Load Test

        int value;
        if (int.TryParse(Input.inputString, out value))
        {
            var clusterResources = RenderPipeline.current.resources.clusterResources;
            clusterResources.TransformScene((uint)value, this);
        }
    }

    private void OnDestroy()
    {
        buff.Dispose();
        vt.Dispose();
    }
}

