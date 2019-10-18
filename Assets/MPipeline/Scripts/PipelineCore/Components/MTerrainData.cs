using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using UnityEngine.AddressableAssets;
namespace MPipeline
{
    [CreateAssetMenu(fileName = "TerrainData", menuName ="PCG/Terrain")]
    public class MTerrainData : ScriptableObject
    {
        public float heightOffset = 0;
        public float heightScale = 10;
        public float materialTillingScale = 1;
        public double largestChunkSize = 1000;
        public double2 screenOffset;
        public double lodDeferredOffset = 2;
        public AssetReference[] allMaskTextures;
        public Texture2D warpNoiseTexture;
        public float[] lodDistances = new float[]
        {
            3000,
            1000,
            600,
            300
        };
        public int renderingLevelCount = 3;
        public LayerMask[] allDecalLayers;
        [Range(1, 256)]
        public int virtualTexCapacity = 128;
        public string readWritePath = "Assets/BinaryData/Terrain.mquad";
        public Material drawTerrainMaterial;
        public MTerrain.PBRTexture[] textures;
        [EasyButtons.Button]
        void GetVirtualTextureSize()
        {
            Debug.Log((MTerrain.COLOR_RESOLUTION * MTerrain.COLOR_RESOLUTION * (4.0 + 4.0 + 2.0) + MTerrain.HEIGHT_RESOLUTION * MTerrain.HEIGHT_RESOLUTION * 2.0) * virtualTexCapacity / 1024.0 / 1024.0);
        }
    }
}
