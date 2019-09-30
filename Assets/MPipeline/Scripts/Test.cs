using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using MPipeline;
using Unity.Jobs;
using UnityEngine.Rendering;
using Unity.Collections;
using System.IO;
using Unity.Collections.LowLevel.Unsafe;
using Random = UnityEngine.Random;
using UnityEngine.AddressableAssets;
using MPipeline.PCG;
namespace MPipeline
{

    public unsafe sealed class Test : MonoBehaviour
    {
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
        public ComputeShader shader;
        public Texture matIDTex;
        public Texture[] colorTex;
        private RenderTexture colArray;
        private RenderTexture rt;
        public Material testMat;
        [EasyButtons.Button]
        void RunBilinearIDSample()
        {
            if (!colArray)
            {
                colArray = new RenderTexture(1024, 1024, 0, RenderTextureFormat.ARGB32, 0);
                colArray.dimension = TextureDimension.Tex2DArray;
                colArray.volumeDepth = 8;
                colArray.autoGenerateMips = false;
                colArray.useMipMap = false;
                colArray.Create();
                int len = min(8, colorTex.Length);
                for (int i = 0; i < len; ++i)
                {
                    Graphics.Blit(colorTex[i], colArray, 0, i);
                }
            }
            if (!rt)
            {
                rt = new RenderTexture(1024, 1024, 0, RenderTextureFormat.ARGB32, 0);
                rt.useMipMap = false;
                rt.autoGenerateMips = false;
                rt.enableRandomWrite = true;
                rt.Create();
            }
            shader.SetTexture(0, ShaderIDs._SourceTex, matIDTex);
            shader.SetTexture(0, ShaderIDs._MainTex, colArray);
            shader.SetTexture(0, ShaderIDs._DestTex, rt);
            shader.SetVector(ShaderIDs._TextureSize, float4(matIDTex.width, matIDTex.height, 1024, 1024));
            int disp = 1024 / 8;
            shader.Dispatch(0, disp, disp, 1);
            testMat.SetTexture("_EmissionMap", rt);
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

}