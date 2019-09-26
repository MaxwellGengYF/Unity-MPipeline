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
    private VirtualTexture vt;

    [SerializeField]
    private Texture[] allTextures;
    [SerializeField]
    private Material[] mats;

    private void Start()
    {
        int minRes = 256;
        int maxRes = 0;
        int capacity = 0;
        foreach (var i in allTextures)
        {
           
            maxRes = max(i.width, maxRes);
        }
        foreach (var i in allTextures)
        {
            capacity += (i.width / minRes) * (i.height / minRes);
        }
        VirtualTextureFormat* formats = stackalloc VirtualTextureFormat[]
    {
            new VirtualTextureFormat((VirtualTextureSize)minRes, RenderTextureFormat.ARGB32, "_ColorVT")
        };
        vt = new VirtualTexture(2048, int2(capacity, maxRes), formats, 1);

        for (int i = 0, offset = 0; i < allTextures.Length; ++i)
        {
            int size = allTextures[i].width / minRes;
            int ele = vt.LoadNewTexture(int2(offset, 0), size);
            mats[i].SetVector("_TileOffset", new Vector4(size, size, offset, 0));
            offset += size;
            Graphics.Blit(allTextures[i], vt.GetTexture(0), 0, ele);
        }
        Debug.Log(vt.LeftedTextureElement);     //8
    }
    private void Update()
    {
        vt.Update();
        /*if (Input.GetKeyDown(KeyCode.Space))
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
        }*/
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
        vt.Dispose();
    }
}

