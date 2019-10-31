#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Unity.Mathematics;
using static Unity.Mathematics.math;
namespace MPipeline
{

    public class TerrainTools : EditorWindow
    {
        public MTerrainData terrainData;
        public Vector2Int chunkPosition = Vector2Int.zero;
        public int targetChunkCount = 1;
        public Texture heightTexture;
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
            if(!terrainEdit)
            {
                terrainEdit = Resources.Load<ComputeShader>("TerrainEdit");
            }
            terrainData = (MTerrainData)EditorGUILayout.ObjectField("Terrain Data", terrainData, typeof(MTerrainData), false);
            chunkPosition = EditorGUILayout.Vector2IntField("Chunk Position", new Vector2Int(chunkPosition.x, chunkPosition.y));
            heightTexture = EditorGUILayout.ObjectField("Height Texture", heightTexture, typeof(Texture), false) as Texture;
            targetChunkCount = EditorGUILayout.IntField("Chunk Count", targetChunkCount);
            targetChunkCount = max(0, targetChunkCount);
            if (!terrainData)
                return;
            if (GUILayout.Button("Update Height Texture"))
            {
                int chunkCount = (int)(0.1 + pow(2.0, terrainData.lodDistances.Length - terrainData.renderingLevelCount));
                Debug.Log(chunkCount);
                VirtualTextureLoader loader = new VirtualTextureLoader(
                    terrainData.heightmapPath,
                   terrainEdit,
                    chunkCount,
                    MTerrain.MASK_RESOLUTION, true);
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
                    for(int y = 0; y < targetChunkCount; ++y)
                    {
                        int2 pos = int2(x, y) + int2(chunkPosition.x, chunkPosition.y);
                        if (pos.x >= chunkCount || pos.y >= chunkCount) continue;
                        terrainEdit.SetTexture(6, ShaderIDs._SourceTex, heightTexture);
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