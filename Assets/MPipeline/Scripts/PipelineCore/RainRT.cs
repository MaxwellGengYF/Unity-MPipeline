using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
namespace MPipeline
{ 
    public class RainRT : MonoBehaviour
    {
        #region VARIABLE
        public int size = 1024;
        public float timeScale = 1;
        private Material rainMat;
        private ComputeBuffer allPoints;
        private  RenderTexture rainningRT;
        public Shader rainRenderShader;
        public ComputeShader rainComputingShader;
        private System.Action<CommandBuffer> updateFunc;
        #endregion

        void Awake()
        {
            updateFunc = UpdateCB;
            allPoints = new ComputeBuffer(1024, 12);
            NativeArray<Vector3> allPointsInitValue = new NativeArray<Vector3>(1024, Allocator.Temp);
            for (int i = 0; i < 1024; ++i)
            {
                allPointsInitValue[i] = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(0f, 1f));
            }
            allPoints.SetData(allPointsInitValue);
            rainningRT = new RenderTexture(size, size, 0, RenderTextureFormat.RGHalf, RenderTextureReadWrite.Linear);
            rainningRT.Create();
            rainningRT.filterMode = FilterMode.Bilinear;
            rainningRT.wrapMode = TextureWrapMode.Mirror;
            rainMat = new Material(rainRenderShader);

        }

        private void Update()
        {
            RenderPipeline.ExecuteBufferAtFrameEnding(updateFunc);
        }

        void OnDestroy()
        {
            DestroyImmediate(rainningRT);
            rainningRT = null;
            allPoints.Dispose();
        }
        
        private void UpdateCB(CommandBuffer buffer)
        {
            if (enabled)
            {
                buffer.SetComputeVectorParam(rainComputingShader, ShaderIDs._RandomSeed, new Vector4(Random.Range(1f, 3f), Random.Range(1f, 3f), Random.Range(1f, 3f), Random.Range(1f, 3f)));
                buffer.SetComputeBufferParam(rainComputingShader, 0, ShaderIDs.allPoints, allPoints);
                buffer.SetComputeFloatParam(rainComputingShader, ShaderIDs._DeltaTime, Time.deltaTime * timeScale);
                buffer.DispatchCompute(rainComputingShader, 0, 1, 1, 1);
                buffer.SetRenderTarget(rainningRT);
                buffer.ClearRenderTarget(false, true, Color.black);
                buffer.SetGlobalBuffer(ShaderIDs.allPoints, allPoints);
                buffer.DrawProcedural(Matrix4x4.identity, rainMat, 0, MeshTopology.Quads, 4, 1024);
                buffer.SetGlobalTexture(ShaderIDs._RainTexture, rainningRT);
                buffer.EnableShaderKeyword("ENABLE_RAINNING");
            }
            else
            {
                buffer.DisableShaderKeyword("ENABLE_RAINNING");
            }
        }
    }
}