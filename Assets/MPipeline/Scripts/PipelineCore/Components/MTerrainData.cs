using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
namespace MPipeline
{
    [CreateAssetMenu(fileName = "TerrainData", menuName ="PCG/Terrain")]
    public class MTerrainData : ScriptableObject
    {
        public float heightOffset = 0;
        public float heightScale = 10;
        public double largestChunkSize = 1000;
        public double2 screenOffset;
        public float lodDeferredOffset = 2;
        public float[] lodDistances = new float[]
        {
            1000,
            600,
            300
        };
        [Range(1, 256)]
        public int virtualTexCapacity = 128;
        public string readWritePath = "Assets/BinaryData/Terrain.mquad";
        public Material drawTerrainMaterial;

        public MTerrain.PBRTexture[] textures;
    }
}
