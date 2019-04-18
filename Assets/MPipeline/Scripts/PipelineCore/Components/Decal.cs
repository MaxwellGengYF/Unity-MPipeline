using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MPipeline
{
    public class Decal : DecalBase
    {
        public Texture2D decalTex;
        public Texture2D normalTex;
        public float normalScale = 1f;
        public Color albedoColor = Color.white;
        private static Material regularDecalMat;
        public override void Init()
        {
            if (!regularDecalMat)
            {
                regularDecalMat = new Material(Shader.Find("Unlit/Decal"));
            }
        }
        public override void DrawDecal(CommandBuffer buffer)
        {
            buffer.SetGlobalTexture(ShaderIDs._DecalAlbedo, decalTex);
            buffer.SetGlobalTexture(ShaderIDs._DecalNormal, normalTex);
            buffer.SetGlobalVector(ShaderIDs._Color, new Vector3(albedoColor.r, albedoColor.g, albedoColor.b));
            buffer.SetGlobalVector(ShaderIDs._OpaqueScale, new Vector2(albedoColor.a, normalScale));
            buffer.DrawMesh(GraphicsUtility.cubeMesh, transform.localToWorldMatrix, regularDecalMat, 0, 0);
        }
        public override void OnDispose() { }
    }
}