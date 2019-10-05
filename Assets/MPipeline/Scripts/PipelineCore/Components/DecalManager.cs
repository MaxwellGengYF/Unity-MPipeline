using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using UnityEngine.Rendering;

namespace MPipeline
{
    [ExecuteInEditMode]
    public unsafe sealed class DecalManager : MonoBehaviour
    {
        public static DecalManager current { get; private set; }
        private RenderTexture decalAlbedoAtlas;
        private RenderTexture decalNormalAtlas;
        private RenderTexture decalSpecularAtlas;

        [SerializeField] private int albedoSize = 1024;
        [SerializeField] private int normalSize = 1024;
        [SerializeField] private int specularSize = 1024;
        public List<Texture2D> allAlbedoTextures = new List<Texture2D>();
        public List<Texture2D> allNormalTextures = new List<Texture2D>();
        public List<Texture2D> allSpecularTextures = new List<Texture2D>();
        public List<DecalAtlasBase> allAlbedoComponent = new List<DecalAtlasBase>();
        public List<DecalAtlasBase> allNormalComponent = new List<DecalAtlasBase>();
        public List<DecalAtlasBase> allSpecularComponent = new List<DecalAtlasBase>();
        private Material texCopyMat;
        private void OnEnable()
        {
            if (current)
            {
                enabled = false;
                return;
            }
            current = this;
            elements = int3(allAlbedoTextures.Count, allNormalTextures.Count, allSpecularTextures.Count);
            texCopyMat = new Material(Shader.Find("Hidden/DecalCopy"));
            foreach(var i in allAlbedoComponent)
            {
                i.Init();
            }
            foreach (var i in allNormalComponent)
            {
                i.Init();
            }
            foreach (var i in allSpecularComponent)
            {
                i.Init();
            }
            int depthSlice = allAlbedoTextures.Count + allAlbedoComponent.Count;
            if (depthSlice > 0)
            {
                decalAlbedoAtlas = new RenderTexture(new RenderTextureDescriptor
                {
                    width = albedoSize,
                    height = albedoSize,
                    volumeDepth = depthSlice,
                    dimension = TextureDimension.Tex2DArray,
                    enableRandomWrite = true,
                    colorFormat = RenderTextureFormat.ARGB32,
                    msaaSamples = 1,
                });
                decalAlbedoAtlas.wrapMode = TextureWrapMode.Repeat;
                decalAlbedoAtlas.Create();
            }
            depthSlice = allNormalTextures.Count + allNormalComponent.Count;
            if (depthSlice > 0)
            {
                decalNormalAtlas = new RenderTexture(new RenderTextureDescriptor
                {
                    width = normalSize,
                    height = normalSize,
                    volumeDepth = depthSlice,
                    dimension = TextureDimension.Tex2DArray,
                    enableRandomWrite = true,
                    colorFormat = RenderTextureFormat.RGHalf,
                    msaaSamples = 1
                });
                decalNormalAtlas.wrapMode = TextureWrapMode.Repeat;
                decalNormalAtlas.Create();
            }
            depthSlice = allSpecularTextures.Count + allSpecularComponent.Count;
            if(depthSlice > 0)
            {
                decalSpecularAtlas = new RenderTexture(new RenderTextureDescriptor
                {
                    width = specularSize,
                    height = specularSize,
                    volumeDepth = depthSlice,
                    dimension = TextureDimension.Tex2DArray,
                    enableRandomWrite = true,
                    colorFormat = RenderTextureFormat.ARGB32,
                    msaaSamples = 1
                });
                decalSpecularAtlas.wrapMode = TextureWrapMode.Repeat;
                decalSpecularAtlas.Create();
            }
            if (!Application.isPlaying)
                LoadTexturesInEditor();
            else StartCoroutine(LoadTextures());
        }
        private int3 elements;
        void LoadTexturesInEditor()
        {
            int i;
            for(i = 0; i < allAlbedoTextures.Count; ++i)
            {
                Graphics.Blit(allAlbedoTextures[i], decalAlbedoAtlas, Vector2.one, Vector2.zero, 0, i);
            }
            for (i = 0; i < allNormalTextures.Count; ++i)
            {
                Graphics.SetRenderTarget(decalNormalAtlas.colorBuffer, decalNormalAtlas.depthBuffer, 0, CubemapFace.Unknown, i);
                Shader.SetGlobalTexture(ShaderIDs._MainTex, allNormalTextures[i]);
                texCopyMat.SetPass(0);
                Graphics.DrawMeshNow(GraphicsUtility.mesh, Matrix4x4.identity);
            }
            for(i = 0; i < allSpecularTextures.Count; ++i)
            {
                Graphics.Blit(allSpecularTextures[i], decalSpecularAtlas, Vector2.one, Vector2.zero, 0, i);
            }
        }

        IEnumerator LoadTextures()
        {
            yield return null;
            CommandBuffer buffer = RenderPipeline.BeforeFrameBuffer;
            for (int i = 0; i < allAlbedoTextures.Count; ++i)
            {
                buffer.Blit(allAlbedoTextures[i], decalAlbedoAtlas, Vector2.one, Vector2.zero, 0, i);
            }
            for (int i = 0; i < allNormalTextures.Count; ++i)
            {
                buffer.SetRenderTarget(decalNormalAtlas.colorBuffer, decalNormalAtlas.depthBuffer, 0, CubemapFace.Unknown, i);
                buffer.SetGlobalTexture(ShaderIDs._MainTex, allNormalTextures[i]);
                buffer.DrawMesh(GraphicsUtility.mesh, Matrix4x4.identity, texCopyMat, 0, 0);
            }
            for (int i = 0; i < allSpecularTextures.Count; ++i)
            {
                buffer.Blit(allSpecularTextures[i], decalSpecularAtlas, Vector2.one, Vector2.zero, 0, i);
            }
            yield return null;
            yield return null;
            foreach (var i in allAlbedoTextures)
                Resources.UnloadAsset(i);
            foreach (var i in allNormalTextures)
                Resources.UnloadAsset(i);
            foreach (var i in allSpecularTextures)
                Resources.UnloadAsset(i);
        }

        void Update()
        {
            CommandBuffer buffer = RenderPipeline.BeforeFrameBuffer;
            if (decalAlbedoAtlas)
                buffer.SetGlobalTexture(ShaderIDs._DecalAtlas, decalAlbedoAtlas);
            if (decalNormalAtlas)
                buffer.SetGlobalTexture(ShaderIDs._DecalNormalAtlas, decalNormalAtlas);
            if (decalSpecularAtlas)
                buffer.SetGlobalTexture(ShaderIDs._DecalSpecularAtlas, decalSpecularAtlas);
            for (int j = 0; j < allAlbedoComponent.Count; ++j)
            {
                allAlbedoComponent[j].FrameUpdate(buffer, decalAlbedoAtlas, elements.x + j);
            }
            for (int j = 0; j < allNormalComponent.Count; ++j)
            {
                allNormalComponent[j].FrameUpdate(buffer, decalNormalAtlas, elements.y + j);
            }
            for (int j = 0; j < allSpecularComponent.Count; ++j)
            {
                allSpecularComponent[j].FrameUpdate(buffer, decalSpecularAtlas, elements.z + j);
            }
        }

        private void OnDisable()
        {
            if (current != this) return;
            current = null;
            if (!Application.isPlaying)
            {
                DestroyImmediate(texCopyMat);
                if (decalAlbedoAtlas) DestroyImmediate(decalAlbedoAtlas);
                if (decalNormalAtlas) DestroyImmediate(decalNormalAtlas);
                if (decalSpecularAtlas) DestroyImmediate(decalSpecularAtlas);
            }
            else
            {
                Destroy(texCopyMat);
                if (decalAlbedoAtlas) Destroy(decalAlbedoAtlas);
                if (decalNormalAtlas) Destroy(decalNormalAtlas);
                if (decalSpecularAtlas) Destroy(decalSpecularAtlas);
            }
            foreach (var i in allAlbedoComponent)
            {
                i.Dispose();
            }
            foreach (var i in allNormalComponent)
            {
                i.Dispose();
            }
            foreach (var i in allSpecularComponent)
            {
                i.Dispose();
            }
        }
    }
}
