namespace UnityEngine.Rendering.PostProcessing
{
    public struct PropertySheet
    {
        public MaterialPropertyBlock properties { get; private set; }
        public Material material { get; private set; }
        public bool isCreated { get; private set; }
        public PropertySheet(Material material)
        {
            isCreated = true;
            this.material = material;
            properties = new MaterialPropertyBlock();
        }

        public void ClearKeywords()
        {
            material.shaderKeywords = null;
        }

        public void EnableKeyword(string keyword, CommandBuffer buffer)
        {
            buffer.EnableShaderKeyword(keyword);
        }

        public void DisableKeyword(string keyword, CommandBuffer buffer)
        {
            buffer.DisableShaderKeyword(keyword);
        }

        public void Release()
        {
            RuntimeUtilities.Destroy(material);
            material = null;
        }
    }
}
