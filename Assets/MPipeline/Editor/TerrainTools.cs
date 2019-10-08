#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Unity.Mathematics;
using static Unity.Mathematics.math;
namespace MPipeline
{
   
    public class GenerateMip : EditorWindow
    {
        public MTerrainData terrainData;
        public Vector2Int chunkPosition;
        public int chunkSize = 1;
        public Texture maskTexture;
        public Texture heightTexture;
        [MenuItem("MPipeline/Terrain/Generate Tool")]
        private static void CreateInstance()
        {
            GenerateMip mip = GetWindow(typeof(GenerateMip)) as GenerateMip;
            mip.Show();
        }
        private void OnGUI()
        {
            terrainData = (MTerrainData)EditorGUILayout.ObjectField("Terrain Data", terrainData, typeof(MTerrainData), false);
            chunkPosition = EditorGUILayout.Vector2IntField("Chunk Position", new Vector2Int(chunkPosition.x, chunkPosition.y));
            chunkSize = EditorGUILayout.IntField("Chunk Size", chunkSize);
            chunkSize = max(1, chunkSize);
            maskTexture = EditorGUILayout.ObjectField("Mask Texture", maskTexture, typeof(Texture), false) as Texture;
            heightTexture = EditorGUILayout.ObjectField("Height Texture", heightTexture, typeof(Texture), false) as Texture;
            if (!terrainData)
                return;
            if (GUILayout.Button("Update Height Mask Texture"))
            {
                TerrainFactory factory = new TerrainFactory(terrainData.lodDistances.Length - terrainData.renderingLevelCount, terrainData.lodDistances.Length, terrainData.readWritePath);
                try
                {
                    int resolution = (int)(0.1 + pow(2.0, terrainData.renderingLevelCount));
                    for (int x = 0; x < chunkSize; ++x)
                        for (int y = 0; y < chunkSize; ++y)
                        {
                            int2 currentPos = int2(x + chunkPosition.x, y + chunkPosition.y);
                            if (currentPos.x > resolution || currentPos.y > resolution) continue;
                            factory.BlitMask(currentPos, terrainData.lodDistances.Length - 1, maskTexture, 1f / chunkSize, float2(x, y) / chunkSize);
                            factory.BlitHeight(currentPos, terrainData.lodDistances.Length - 1, heightTexture, 1f / chunkSize, float2(x, y) / chunkSize);
                        }
                }
                finally
                {
                    factory.Dispose();
                }
            }
            if (GUILayout.Button("Generate Mipmap"))
            {
                TerrainFactory factory = new TerrainFactory(terrainData.lodDistances.Length - terrainData.renderingLevelCount, terrainData.lodDistances.Length, terrainData.readWritePath);
                try
                {
                    for (int i = terrainData.lodDistances.Length - 2; i >= terrainData.lodDistances.Length - terrainData.renderingLevelCount; --i)
                    {
                        int resolution = (int)(0.1 + pow(2.0, i));
                        for(int x = 0; x < resolution; ++x)
                            for(int y = 0; y < resolution; ++y)
                            {
                                factory.GenerateMaskMip(int2(x, y), i);
                                factory.GenerateHeightMip(int2(x, y), i);
                            }
                    }
                }
                finally
                {
                    factory.Dispose();
                }
            }
        }
    }
}
#endif