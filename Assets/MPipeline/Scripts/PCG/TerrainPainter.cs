using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using UnityEngine.Rendering;
using Random = Unity.Mathematics.Random;
using System.IO;
namespace MPipeline.PCG
{
    public class TerrainPainter : MonoBehaviour
    {
        public static bool updateAtlas = false;
        public PVTData data;
        public Material testMat;
        private RenderTexture albedoRT;
        private RenderTexture normalRT;
        private RenderTexture smoRT;
        private Texture2D whiteTex;
        private Texture2D normalTex;
        private Material blendTexMat;
        private RenderTargetIdentifier[] colorBuffers = new RenderTargetIdentifier[3];
        private void OnEnable()
        {
            int2 targetSize = data.targetSize;
            albedoRT = new RenderTexture(targetSize.x, targetSize.y, 16, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            normalRT = new RenderTexture(targetSize.x, targetSize.y, 0, RenderTextureFormat.RGFloat, RenderTextureReadWrite.Linear);
            smoRT = new RenderTexture(albedoRT.descriptor);
            whiteTex = new Texture2D(1, 1, TextureFormat.ARGB32, false, true);
            whiteTex.SetPixel(0, 0, Color.white);
            whiteTex.Apply();
            normalTex = new Texture2D(1, 1, TextureFormat.ARGB32, false, true);
            normalTex.SetPixel(0, 0, new Color(0.5f, 0.5f, 1, 1));
            normalTex.Apply();
            albedoRT.Create();
            normalRT.Create();
            smoRT.Create();
            colorBuffers[0] = albedoRT.colorBuffer;
            colorBuffers[1] = normalRT.colorBuffer;
            colorBuffers[2] = smoRT.colorBuffer;
            blendTexMat = new Material(Shader.Find("Hidden/TerrainTexBlend"));
        }

        public void UpdateRender(CommandBuffer buffer)
        {
            buffer.SetRenderTarget(colorBuffers, albedoRT.depthBuffer);
            buffer.ClearRenderTarget(true, true, new Color(0, 0, 0, 0));
            foreach (var i in data.allSettings)
            {
                buffer.SetGlobalTexture("_BlendAlbedo", i.startAlbedo ? i.startAlbedo : whiteTex);
                buffer.SetGlobalTexture("_BlendNormal", i.startNormal ? i.startNormal : normalTex);
                buffer.SetGlobalTexture("_BlendSMO", i.startSMO ? i.startNormal : normalTex);
                buffer.SetGlobalTexture("_BlendMask", i.maskTex ? i.maskTex : whiteTex);
                buffer.SetGlobalVector("_BlendScaleOffset", i.scaleOffset);
                buffer.SetGlobalVector("_MaskScaleOffset", new Vector4(i.maskScale, i.maskOffset));
                buffer.DrawMesh(GraphicsUtility.mesh, Matrix4x4.identity, blendTexMat, 0, 0);
            }
            OrthoCam orthoCam = new OrthoCam
            {
                up = Vector3.forward,
                right = Vector3.right,
                forward = Vector3.down,
                position = transform.position,
                farClipPlane = 50,
                nearClipPlane = -50,
                size = transform.localScale.x * 0.5f
            };
            orthoCam.UpdateProjectionMatrix();
            orthoCam.UpdateTRSMatrix();
            float4x4 vp = mul(GraphicsUtility.GetGPUProjectionMatrix(orthoCam.projectionMatrix, true), orthoCam.worldToCameraMatrix);
            buffer.SetGlobalMatrix(ShaderIDs._VP, vp);
            for(int i = 0; i < transform.childCount; ++i)
            {
                VTDecal vtDecal = transform.GetChild(i).GetComponent<VTDecal>();
                if (!vtDecal) continue;
                buffer.SetGlobalVector("_DecalScaleOffset", vtDecal.scaleOffset);
                buffer.SetGlobalTexture("_DecalAlbedo", vtDecal.albedoTex ? vtDecal.albedoTex : whiteTex);
                buffer.SetGlobalTexture("_DecalNormal", vtDecal.normalTex ? vtDecal.normalTex : normalTex);
                buffer.SetGlobalTexture("_DecalSMO", vtDecal.smoTex ? vtDecal.smoTex : whiteTex);
                buffer.SetGlobalTexture("_DecalMask", vtDecal.opaqueMask ? vtDecal.opaqueMask : whiteTex);
                buffer.SetGlobalVector("_DecalSMOWeight", new Vector4(vtDecal.smoothness, vtDecal.metallic, vtDecal.occlusion, vtDecal.opaque));
                buffer.DrawMesh(vtDecal.sharedMesh, vtDecal.transform.localToWorldMatrix, blendTexMat, 0, 1);
            }
            testMat.SetTexture("_MainTex", albedoRT);
            testMat.SetTexture("_BumpMap", normalRT);
            testMat.SetTexture("_SpecularMap", smoRT);
        }
        private void Update()
        {
            if (data.shouldUpdate || updateAtlas)
            {
                CommandBuffer buffer = RenderPipeline.BeforeFrameBuffer;
                UpdateRender(buffer);
                data.shouldUpdate = false;
                updateAtlas = false;
            }
        }

        private void OnDisable()
        {
            Destroy(albedoRT);
            Destroy(normalRT);
            Destroy(smoRT);
            Destroy(whiteTex);
            Destroy(blendTexMat);
        }
    }
}