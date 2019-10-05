using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace MPipeline
{
    public class TextureDecal : TextureDecalBase
    {
        public Texture2D maskTex;
        protected override void Init()
        {
        }

        protected override void Dispose()
        {
        }

        public override Texture GetDecal(CommandBuffer buffer)
        {
            return maskTex;
        }
    }
}