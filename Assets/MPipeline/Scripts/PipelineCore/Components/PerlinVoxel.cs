using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Unity.Mathematics.math;
using Unity.Mathematics;
using UnityEngine.Rendering;
namespace MPipeline
{
    [ExecuteInEditMode]
    public class PerlinVoxel : VoxelFogBase
    {
        public int3 size = new int3(128, 32, 128);
        private RenderTexture voxTex;
        private ComputeShader noiseShader;
        public float scale = 1;
        public float offset = 1;
        public float timeSlice = 0;
        public float3 uvScale = 1;
        [Range(1, 10)]
        public int fractureValue = 5;
        [Range(0.1f, 10)]
        public float heightPower = 1;
        public override Texture GetVoxel()
        {
            return voxTex;
        }

        protected override void OnEnableFunc()
        {
            noiseShader = Resources.Load<ComputeShader>("VoxelNoise");
            size.x = Mathf.CeilToInt(size.x / 8f) * 8;
            size.z = Mathf.CeilToInt(size.z / 8f) * 8;
            voxTex = new RenderTexture(new RenderTextureDescriptor
            {
                width = size.x,
                height = size.y,
                volumeDepth = size.z,
                colorFormat = RenderTextureFormat.ARGBHalf,
                dimension = TextureDimension.Tex3D,
                msaaSamples = 1,
                enableRandomWrite = true
            });
            voxTex.Create();
        }

        private void Update()
        {
            CommandBuffer buffer = RenderPipeline.BeforeFrameBuffer;
            float3 localscale = transform.localScale;
            localscale /= localscale.y;
            localscale *= uvScale;
            buffer.SetComputeVectorParam(noiseShader, ShaderIDs._OffsetScale, float4(localscale, 1));
            buffer.SetComputeVectorParam(noiseShader, ShaderIDs._VolumetricLightVar, new Vector4(scale, offset, timeSlice, fractureValue + 0.1f));
            buffer.SetComputeTextureParam(noiseShader, 0, ShaderIDs._AlbedoVoxel, voxTex);
            buffer.SetComputeVectorParam(noiseShader, ShaderIDs._TextureSize, float4(size.x, size.y, size.z, heightPower));
            buffer.DispatchCompute(noiseShader, 0, size.x / 8, size.y, size.z / 8);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one * 0.5f);
            UpdateData();
        }

        protected override void OnDisableFunc()
        {
            Resources.UnloadAsset(noiseShader);
            voxTex.Release();
            DestroyImmediate(voxTex);
        }
    }
}