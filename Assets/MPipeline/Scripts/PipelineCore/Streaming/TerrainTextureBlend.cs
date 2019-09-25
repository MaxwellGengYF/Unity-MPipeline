using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using UnityEngine.Rendering;
using static Unity.Mathematics.math;
[System.Serializable]
struct TextureSettings
{
    public bool isOpen;
    public bool voronoiSample;
    public Texture targetTexture;
    public float2 size;
    public float2 scale;
    public float2 offset;
    public float blendAlpha;
}
public struct TerrainTextureBlend
{
    public struct TextureBlendProcedural
    {
        public bool isOpen;
        public bool voronoiSample;
        public int targetTexture;
        public float2 size;
        public float2 scale;
        public float2 offset;
        public float blendAlpha;
    }

    //private static NativeList<TextureBlendProcedural> GetblendProcedural(out List<Texture> allTexture)
}
