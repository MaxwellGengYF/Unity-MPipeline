#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Unity.Mathematics;
using static Unity.Mathematics.math;
namespace MPipeline
{

    public unsafe class TerrainTools : EditorWindow
    {
        public MTerrainData terrainData;
        public Vector2Int chunkPosition = Vector2Int.zero;
        public int targetChunkCount = 1;
        public Texture heightTexture;
        public Texture maskTexture;
        public ComputeShader terrainEdit;
        [MenuItem("MPipeline/Terrain/Generate Tool")]
        private static void CreateInstance()
        {
            TerrainTools mip = GetWindow(typeof(TerrainTools)) as TerrainTools;
            mip.terrainEdit = Resources.Load<ComputeShader>("TerrainEdit");
            mip.Show();
        }
        private void OnGUI()
        {
            if (!terrainEdit)
            {
                terrainEdit = Resources.Load<ComputeShader>("TerrainEdit");
            }
            terrainData = (MTerrainData)EditorGUILayout.ObjectField("Terrain Data", terrainData, typeof(MTerrainData), false);
            chunkPosition = EditorGUILayout.Vector2IntField("Chunk Position", new Vector2Int(chunkPosition.x, chunkPosition.y));
            heightTexture = EditorGUILayout.ObjectField("Height Texture", heightTexture, typeof(Texture), false) as Texture;
            targetChunkCount = EditorGUILayout.IntField("Chunk Count", targetChunkCount);
            targetChunkCount = max(0, targetChunkCount);
            int largestChunkCount = (int)(pow(2.0, terrainData.GetLodOffset()) + 0.1);
            if (!terrainData)
                return;
            if (GUILayout.Button("Update Height Texture"))
            {
                VirtualTextureLoader loader = new VirtualTextureLoader(
                    terrainData.heightmapPath,
                   terrainEdit,
                    largestChunkCount,
                    MTerrain.MASK_RESOLUTION, true, null);
                RenderTexture cacheRt = new RenderTexture(new RenderTextureDescriptor
                {
                    width = MTerrain.MASK_RESOLUTION,
                    height = MTerrain.MASK_RESOLUTION,
                    volumeDepth = 1,
                    dimension = UnityEngine.Rendering.TextureDimension.Tex2DArray,
                    msaaSamples = 1,
                    graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R32_SFloat,
                    enableRandomWrite = true
                });
                cacheRt.Create();
                int mipLevel = 0;
                int meshResolution = terrainData.GetMeshResolution();
                int saveMipLevel = 0;
                ComputeBuffer cb = new ComputeBuffer(meshResolution * meshResolution, sizeof(float2));

                float2[] resultArr = new float2[meshResolution * meshResolution];
                RenderTexture mipRT = new RenderTexture(MTerrain.MASK_RESOLUTION, MTerrain.MASK_RESOLUTION, 0, UnityEngine.Experimental.Rendering.GraphicsFormat.R32G32_SFloat, mipLevel);
                mipRT.enableRandomWrite = true;
                mipRT.useMipMap = true;
                mipRT.autoGenerateMips = false;
                mipRT.Create();
                System.IO.FileStream fsm = new System.IO.FileStream(terrainData.boundPath, System.IO.FileMode.OpenOrCreate, System.IO.FileAccess.ReadWrite);
                for (int i = MTerrain.MASK_RESOLUTION; i > 0; i /= 2)
                {
                    mipLevel++;
                    if (i <= meshResolution) saveMipLevel++;
                }
                MTerrainBoundingTree btree = new MTerrainBoundingTree(saveMipLevel);
                for (int x = 0; x < targetChunkCount; ++x)
                {
                    for (int y = 0; y < targetChunkCount; ++y)
                    {
                        int2 pos = int2(x, y) + int2(chunkPosition.x, chunkPosition.y);
                        if (pos.x >= largestChunkCount || pos.y >= largestChunkCount) continue;
                        terrainEdit.SetTexture(6, ShaderIDs._SourceTex, heightTexture);
                        terrainEdit.SetTexture(6, ShaderIDs._DestTex, cacheRt);
                        terrainEdit.SetInt(ShaderIDs._Count, MTerrain.MASK_RESOLUTION);
                        terrainEdit.SetInt(ShaderIDs._OffsetIndex, 0);
                        terrainEdit.SetVector("_ScaleOffset", float4(float2(1.0 / targetChunkCount), float2(x, y) / targetChunkCount));
                        const int disp = MTerrain.MASK_RESOLUTION / 8;
                        terrainEdit.Dispatch(6, disp, disp, 1);
                        loader.WriteToDisk(cacheRt, 0, pos);
                        for (int i = 0, res = MTerrain.MASK_RESOLUTION; i < mipLevel; ++i, res /= 2)
                        {
                            int pass;
                            if (i == 0)
                            {
                                pass = 7;
                                terrainEdit.SetTexture(pass, "_SourceArray", cacheRt);
                                terrainEdit.SetTexture(pass, "_Mip1", mipRT);
                            }
                            else
                            {
                                if (res <= meshResolution)
                                {
                                    pass = 9;
                                    terrainEdit.SetBuffer(pass, "_DataBuffer", cb);
                                }
                                else
                                    pass = 8;
                                terrainEdit.SetTexture(pass, "_Mip0", mipRT, i - 1);
                                terrainEdit.SetTexture(pass, "_Mip1", mipRT, i);
                            }
                            terrainEdit.SetInt(ShaderIDs._Count, res);
                            int mipdisp = Mathf.CeilToInt(res / 8f);
                            terrainEdit.Dispatch(pass, mipdisp, mipdisp, 1);
                            if (pass == 9)
                            {
                                cb.GetData(resultArr, 0, 0, res * res);
                                int targetMipLevel = mipLevel - 1 - i;
                                for (int xx = 0; xx < res * res; ++xx)
                                {
                                    btree[xx, targetMipLevel] = resultArr[xx];
                                }
                            }
                        }
                        btree.WriteToDisk(fsm, x + y * largestChunkCount);
                    }
                }

                btree.Dispose();
                cacheRt.Release();
                cb.Dispose();
                mipRT.Release();
                loader.Dispose();
                fsm.Dispose();
                Debug.Log("Finish!");
            }
            maskTexture = EditorGUILayout.ObjectField("Mask Texture", maskTexture, typeof(Texture), false) as Texture;
            if (GUILayout.Button("Update Mask Texture"))
            {
                VirtualTextureLoader loader = new VirtualTextureLoader(
                    terrainData.maskmapPath,
                   terrainEdit,
                    largestChunkCount,
                    MTerrain.MASK_RESOLUTION, false, null);
                RenderTexture cacheRt = new RenderTexture(new RenderTextureDescriptor
                {
                    width = MTerrain.MASK_RESOLUTION,
                    height = MTerrain.MASK_RESOLUTION,
                    volumeDepth = 1,
                    dimension = UnityEngine.Rendering.TextureDimension.Tex2DArray,
                    msaaSamples = 1,
                    graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R32_SFloat,
                    enableRandomWrite = true
                });
                cacheRt.Create();
                for (int x = 0; x < targetChunkCount; ++x)
                {
                    for (int y = 0; y < targetChunkCount; ++y)
                    {
                        int2 pos = int2(x, y) + int2(chunkPosition.x, chunkPosition.y);
                        if (pos.x >= largestChunkCount || pos.y >= largestChunkCount) continue;
                        terrainEdit.SetTexture(6, ShaderIDs._SourceTex, maskTexture);
                        terrainEdit.SetTexture(6, ShaderIDs._DestTex, cacheRt);
                        terrainEdit.SetInt(ShaderIDs._Count, MTerrain.MASK_RESOLUTION);
                        terrainEdit.SetInt(ShaderIDs._OffsetIndex, 0);
                        terrainEdit.SetVector("_ScaleOffset", float4(float2(1.0 / targetChunkCount), float2(x, y) / targetChunkCount));
                        const int disp = MTerrain.MASK_RESOLUTION / 8;
                        terrainEdit.Dispatch(6, disp, disp, 1);
                        loader.WriteToDisk(cacheRt, 0, pos);
                    }
                }
                cacheRt.Release();
                loader.Dispose();
            }
        }
    }
}
#endif