using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine;
namespace MPipeline
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(Renderer))]
    [RequireComponent(typeof(MeshFilter))]
    public class TextureDecalManager : MonoBehaviour
    {
        private static Material decalMat = null;
        private static MaterialPropertyBlock block = null;
        public Vector2Int textureSize = new Vector2Int(2048, 2048);
        private Renderer rend;
        private MeshFilter filt;
        private RenderTexture decalMask;
        private static int _BlendVar = Shader.PropertyToID("_BlendVar");
        private static int _MaskTex = Shader.PropertyToID("_MaskTex");
        private List<TextureDecalBase> allDecals = new List<TextureDecalBase>(10);
        private void OnEnable()
        {
            rend = GetComponent<Renderer>();
            filt = GetComponent<MeshFilter>();
            if (block == null) block = new MaterialPropertyBlock();
            decalMask = new RenderTexture(new RenderTextureDescriptor
            {
                width = textureSize.x,
                height = textureSize.y,
                volumeDepth = 1,
                colorFormat = RenderTextureFormat.RHalf,
                dimension = TextureDimension.Tex2D,
                msaaSamples = 1
            });
            InitDecal();
            UpdateDecal();
            UpdateBlock();
        }

        void InitDecal()
        {
            allDecals.Clear();
            for (int i = 0; i < transform.childCount; ++i)
            {
                var dc = transform.GetChild(i).GetComponent<TextureDecalBase>();
                if (dc && dc.gameObject.activeSelf)
                {
                    allDecals.Add(dc);
                    dc.RunInit();
                }
            }
        }

        void UpdateBlock()
        {
            rend.GetPropertyBlock(block);
            block.SetTexture(_MaskTex, decalMask);
            rend.SetPropertyBlock(block);
        }

        void UpdateDecal()
        {
            if (!decalMat) decalMat = new Material(Shader.Find("Hidden/TexDecal"));
            CommandBuffer buffer = RenderPipeline.BeforeFrameBuffer;
            buffer.SetRenderTarget(decalMask);
            buffer.ClearRenderTarget(false, true, new Color(0, 0, 0, 0));
            for (int i = 0; i < allDecals.Count; ++i)
            {
                var dc = allDecals[i];
                if (dc)
                {
                    buffer.SetGlobalFloat(_BlendVar, dc.blendWeight);
                    buffer.SetGlobalTexture(_MaskTex, dc.GetDecal(buffer));
                    //TODO
                    buffer.SetKeyword("USE_VORONOI_SAMPLE", dc.useVoronoiSample);
                    buffer.SetGlobalMatrix(ShaderIDs._WorldToLocalMatrix, Matrix4x4.TRS(dc.transform.localPosition, dc.transform.localRotation, dc.transform.localScale).inverse);
                    Mesh ms = filt.sharedMesh;
                    for (int a = 0; a < ms.subMeshCount; ++a)
                    {
                        buffer.DrawMesh(ms, Matrix4x4.identity, decalMat, a, 0);
                    }
                }
                else
                {
                    allDecals.RemoveAt(i);
                    i--;
                }
            }
        }

        [EasyButtons.Button]
        public void UpdateData()
        {
            if (!enabled) return;
            void Resize(RenderTexture rt)
            {
                if (rt.width != textureSize.x || rt.height != textureSize.y)
                {
                    rt.Release();
                    rt.width = textureSize.x;
                    rt.height = textureSize.y;
                    rt.Create();
                }
            }
            Resize(decalMask);
            InitDecal();
            UpdateDecal();
            UpdateBlock();
        }


        private void OnDisable()
        {
            rend.SetPropertyBlock(null);
            if (decalMask) DestroyImmediate(decalMask);
            foreach (var i in allDecals)
                i.RunDispose();
            allDecals.Clear();
        }
    }
}