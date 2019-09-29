using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using MPipeline;
using Unity.Jobs;
using Unity.Collections;
using System.IO;
using Unity.Collections.LowLevel.Unsafe;
using Random = UnityEngine.Random;
using UnityEngine.AddressableAssets;
using MPipeline.PCG;


public unsafe sealed class Test : MonoBehaviour
{
    public static Dictionary<string, bool> allGUIDs;
    public Texture[] objs;
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

    public Texture tex;
    public Material mat;
    private void Awake()
    {
        allGUIDs = new Dictionary<string, bool>(objs.Length);
        foreach(var i in objs)
        {
            allGUIDs.Add(UnityEditor.AssetDatabase.AssetPathToGUID(UnityEditor.AssetDatabase.GetAssetPath(i)), true);
        }
    }

    private void Update()
    {
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

}

