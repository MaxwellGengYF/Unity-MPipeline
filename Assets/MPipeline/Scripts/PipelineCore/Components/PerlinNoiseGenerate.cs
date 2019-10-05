using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using UnityEngine.Rendering;
namespace MPipeline
{
    public unsafe sealed class PerlinNoiseGenerate : TextureDecalBase
    {
        public enum AccumulateType
        {
            Cover = 0,
            Add = 1,
            Blend = 2,
            Max = 3,
            Min = 4,
            Multiply = 5,
            Divide = 6
        };
        [System.Serializable]
        public struct PerlinProcedural
        {
            public AccumulateType accumulateType;
            [Range(0, 10)]
            public int fractureValue;
            public float seed;
            public float multiplyValue;
            public float addedValue;
            [Range(0f, 1f)]
            public float blend;
        };
        private ComputeShader perlinShader;
        private RenderTexture testTexture;
        public Texture2D gradientMap;
        [Range(0f, 1f)]
        public float gradientSamplePos = 0.5f;
        [SerializeField] private int2 textureSize = int2(1024, 1024);
        [SerializeField] private Material testMat;
        public List<PerlinProcedural> allProcedural = new List<PerlinProcedural>(5);
        private ComputeBuffer proceduralBuffer;
        protected override void Init()
        {
            perlinShader = Resources.Load<ComputeShader>("PerlinNoiseCompute");
            testTexture = new RenderTexture(new RenderTextureDescriptor
            {
                colorFormat = RenderTextureFormat.RHalf,
                dimension = TextureDimension.Tex2D,
                enableRandomWrite = true,
                height = textureSize.y,
                width = textureSize.x,
                volumeDepth = 1,
                msaaSamples = 1
            });
            testTexture.Create();
            if (testMat)
            {
                testMat.SetTexture("_EmissionMap", testTexture);
            }
            proceduralBuffer = new ComputeBuffer(allProcedural.Capacity, sizeof(PerlinProcedural));
        }
        public override Texture GetDecal(CommandBuffer buffer)
        {
            if(proceduralBuffer.count < allProcedural.Count)
            {
                proceduralBuffer.Dispose();
                proceduralBuffer = new ComputeBuffer(allProcedural.Capacity, sizeof(PerlinProcedural));
            }
            int pass = gradientMap ? 1 : 0;
            proceduralBuffer.SetData(allProcedural);
            if (gradientMap)
            {
                buffer.SetComputeTextureParam(perlinShader, pass, ShaderIDs._GradientMap, gradientMap);
                buffer.SetComputeFloatParam(perlinShader, ShaderIDs._Offset, gradientSamplePos);
            }
            buffer.SetComputeBufferParam(perlinShader, pass, ShaderIDs._ProceduralBuffer, proceduralBuffer);
            buffer.SetComputeIntParam(perlinShader, ShaderIDs._ProceduralCount, allProcedural.Count);
            buffer.SetComputeTextureParam(perlinShader, pass, ShaderIDs._MainTex, testTexture);
            buffer.SetComputeVectorParam(perlinShader, ShaderIDs._TextureSize, float4(1f / float2(testTexture.width, testTexture.height), 0, 0));
            buffer.DispatchCompute(perlinShader, pass, testTexture.width / 8, testTexture.height / 8, 1);
            return testTexture;
        }

        protected override void Dispose()
        {
            Resources.UnloadAsset(perlinShader);
            DestroyImmediate(testTexture);
            proceduralBuffer.Dispose();
        }

        [EasyButtons.Button]
        void UpdateRun()
        {
            TextureDecalManager manager;
            if (transform.parent && (manager = transform.parent.GetComponent<TextureDecalManager>()))
            {
                manager.UpdateData();
            }
        }
    }
}