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
        public Vector2Int chunkPosition;
        public int chunkSize = 1;
        public float tillingScale = 1;
        public Texture heightTexture;
        [MenuItem("MPipeline/Terrain/Generate Tool")]
        private static void CreateInstance()
        {
            TerrainTools mip = GetWindow(typeof(TerrainTools)) as TerrainTools;
            mip.Show();
        }
        private void OnGUI()
        {
            terrainData = (MTerrainData)EditorGUILayout.ObjectField("Terrain Data", terrainData, typeof(MTerrainData), false);
            chunkPosition = EditorGUILayout.Vector2IntField("Chunk Position", new Vector2Int(chunkPosition.x, chunkPosition.y));
            chunkSize = EditorGUILayout.IntField("Chunk Size", chunkSize);
            tillingScale = EditorGUILayout.FloatField("Tilling Scale", tillingScale);
            chunkSize = max(1, chunkSize);
            heightTexture = EditorGUILayout.ObjectField("Height Texture", heightTexture, typeof(Texture), false) as Texture;
            if (!terrainData)
                return;
            if (GUILayout.Button("Update Height Mask Texture"))
            {
              //TODO
            }
            if (GUILayout.Button("Generate Mipmap"))
            {
              //TODO
            }
        }
    }
}
#endif