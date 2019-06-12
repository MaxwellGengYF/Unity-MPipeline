using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;


#if UNITY_EDITOR



namespace MPipeline
{
    internal class BakeLightProbeRenderPass
    {
        #region _
        static CommandBuffer cb_;
        static Matrix4x4[] proj_mats_;
        static Material blitMat_;
        #endregion
        static CommandBuffer cb {
            get {
                if (cb_ == null) {
                    cb_ = new CommandBuffer();
                    cb.name = "Bake light probe";
                }
                return cb_;
            }
        }        
        static Matrix4x4[] proj_mats {
            get {
                if (proj_mats_ == null) {
                    GameObject helper_;
                    Camera helper;
                    helper_ = new GameObject("gi helper");
                    helper_.SetActive(false);
                    helper_.hideFlags = HideFlags.DontSave /*| HideFlags.HideInHierarchy*/;
                    helper = helper_.AddComponent<Camera>();
                    helper.orthographic = false;
                    helper.nearClipPlane = 0.01f;
                    helper.enabled = false;
                    helper.aspect = 1;
                    helper.fieldOfView = 90;

                    proj_mats_ = new Matrix4x4[6];
                    proj_mats_[0] = GL.GetGPUProjectionMatrix(helper.projectionMatrix, true) * Matrix4x4.Scale(new Vector3(-1, -1, -1));
                    proj_mats_[1] = Matrix4x4.Rotate(Quaternion.Euler(0, 180, 0));
                    proj_mats_[2] = Matrix4x4.Rotate(Quaternion.Euler(0, 90, 0));
                    proj_mats_[3] = Matrix4x4.Rotate(Quaternion.Euler(0, -90, 0));
                    proj_mats_[4] = Matrix4x4.Rotate(Quaternion.Euler(90, 0, 0));
                    proj_mats_[5] = Matrix4x4.Rotate(Quaternion.Euler(-90, 0, 0));

                    GameObject.DestroyImmediate(helper_);
                }
                return proj_mats_;
            }
        }
        static Material blitMat {
            get {
                if (blitMat_ == null) {
                    blitMat_ = new Material(Shader.Find("Bake/BlitFloat"));
                }
                return blitMat_;
            }
        }

        struct ShaderPropertyID
        {
            public static int bake_vp = Shader.PropertyToID("_Bake_VP");
        }



        public static bool Render(ref ScriptableRenderContext renderContext, Camera camera)
        {
            if (camera.cameraType == (CameraType)32)
            {
                cb.SetGlobalVector("_Bake_ProbePosition", camera.transform.position);

                camera.transform.position -= 30 * Vector3.forward;

                ScriptableCullingParameters cp = new ScriptableCullingParameters();
                camera.TryGetCullingParameters(out cp);
                cp.cullingOptions = CullingOptions.DisablePerObjectCulling | CullingOptions.ForceEvenIfCameraIsNotActive | CullingOptions.None;

                var result = renderContext.Cull(ref cp);

                RenderToCube(ref renderContext, cb, camera, ref result, camera.transform.position + 30 * Vector3.forward);

                renderContext.Submit();
                return true;
            }
            return false;
        }






        static void RenderToCube(ref ScriptableRenderContext context, CommandBuffer cb, Camera camera, ref CullingResults cullResults, Vector3 pos)
        {
            var filterSetting = new FilteringSettings()
            {
                layerMask = camera.cullingMask,
                renderingLayerMask = 1,
                renderQueueRange = new RenderQueueRange(2000, 2449)
            };
            //filterSetting.renderQueueRange = RenderQueueRange.opaque;

            var renderSetting = new DrawingSettings(new ShaderTagId("BAKE_LIGHT_PROBE"), new SortingSettings(camera));

            var info = camera.GetComponent<BakeLightProbeInfomation>();

            RenderTargetIdentifier[]  rts= new RenderTargetIdentifier[2];
            rts[0] = new RenderTargetIdentifier(info.rt2);
            rts[1] = new RenderTargetIdentifier(info.rt3);

            Matrix4x4 t_mat = Matrix4x4.Translate(-pos);
            {
                Matrix4x4 view_mat = t_mat;
                cb.SetRenderTarget(rts, rts[0]);
                cb.ClearRenderTarget(true, true, Color.clear);
                cb.SetGlobalMatrix(ShaderPropertyID.bake_vp, proj_mats[0] * view_mat);
                context.ExecuteCommandBuffer(cb);
                cb.Clear();
                context.DrawRenderers(cullResults, ref renderSetting, ref filterSetting);
                cb.SetRenderTarget(new RenderTargetIdentifier(info.rt0, 0, CubemapFace.PositiveZ));
                cb.Blit(info.rt2, new RenderTargetIdentifier(BuiltinRenderTextureType.CurrentActive), blitMat);
                cb.SetRenderTarget(new RenderTargetIdentifier(info.rt1, 0, CubemapFace.PositiveZ));
                cb.Blit(info.rt3, new RenderTargetIdentifier(BuiltinRenderTextureType.CurrentActive), blitMat);
                context.ExecuteCommandBuffer(cb);
                cb.Clear();
            }
            {
                Matrix4x4 view_mat = proj_mats[1] * t_mat;
                cb.SetRenderTarget(rts, rts[0]);
                cb.ClearRenderTarget(true, true, Color.clear);
                cb.SetGlobalMatrix(ShaderPropertyID.bake_vp, proj_mats[0] * view_mat);
                context.ExecuteCommandBuffer(cb);
                cb.Clear();
                context.DrawRenderers(cullResults, ref renderSetting, ref filterSetting);
                cb.SetRenderTarget(new RenderTargetIdentifier(info.rt0, 0, CubemapFace.NegativeZ));
                cb.Blit(info.rt2, new RenderTargetIdentifier(BuiltinRenderTextureType.CurrentActive), blitMat);
                cb.SetRenderTarget(new RenderTargetIdentifier(info.rt1, 0, CubemapFace.NegativeZ));
                cb.Blit(info.rt3, new RenderTargetIdentifier(BuiltinRenderTextureType.CurrentActive), blitMat);
                context.ExecuteCommandBuffer(cb);
                cb.Clear();
            }
            {
                Matrix4x4 view_mat = proj_mats[2] * t_mat;
                cb.SetRenderTarget(rts, rts[0]);
                cb.ClearRenderTarget(true, true, Color.clear);
                cb.SetGlobalMatrix(ShaderPropertyID.bake_vp, proj_mats[0] * view_mat);
                context.ExecuteCommandBuffer(cb);
                cb.Clear();
                context.DrawRenderers(cullResults, ref renderSetting, ref filterSetting);
                cb.SetRenderTarget(new RenderTargetIdentifier(info.rt0, 0, CubemapFace.PositiveX));
                cb.Blit(info.rt2, new RenderTargetIdentifier(BuiltinRenderTextureType.CurrentActive), blitMat);
                cb.SetRenderTarget(new RenderTargetIdentifier(info.rt1, 0, CubemapFace.PositiveX));
                cb.Blit(info.rt3, new RenderTargetIdentifier(BuiltinRenderTextureType.CurrentActive), blitMat);
                context.ExecuteCommandBuffer(cb);
                cb.Clear();
            }
            {
                Matrix4x4 view_mat = proj_mats[3] * t_mat;
                cb.SetRenderTarget(rts, rts[0]);
                cb.ClearRenderTarget(true, true, Color.clear);
                cb.SetGlobalMatrix(ShaderPropertyID.bake_vp, proj_mats[0] * view_mat);
                context.ExecuteCommandBuffer(cb);
                cb.Clear();
                context.DrawRenderers(cullResults, ref renderSetting, ref filterSetting);
                cb.SetRenderTarget(new RenderTargetIdentifier(info.rt0, 0, CubemapFace.NegativeX));
                cb.Blit(info.rt2, new RenderTargetIdentifier(BuiltinRenderTextureType.CurrentActive), blitMat);
                cb.SetRenderTarget(new RenderTargetIdentifier(info.rt1, 0, CubemapFace.NegativeX));
                cb.Blit(info.rt3, new RenderTargetIdentifier(BuiltinRenderTextureType.CurrentActive), blitMat);
                context.ExecuteCommandBuffer(cb);
                cb.Clear();
            }
            {
                Matrix4x4 view_mat = proj_mats[4] * t_mat;
                cb.SetRenderTarget(rts, rts[0]);
                cb.ClearRenderTarget(true, true, Color.clear);
                cb.SetGlobalMatrix(ShaderPropertyID.bake_vp, proj_mats[0] * view_mat);
                context.ExecuteCommandBuffer(cb);
                cb.Clear();
                context.DrawRenderers(cullResults, ref renderSetting, ref filterSetting);
                cb.SetRenderTarget(new RenderTargetIdentifier(info.rt0, 0, CubemapFace.PositiveY));
                cb.Blit(info.rt2, new RenderTargetIdentifier(BuiltinRenderTextureType.CurrentActive), blitMat);
                cb.SetRenderTarget(new RenderTargetIdentifier(info.rt1, 0, CubemapFace.PositiveY));
                cb.Blit(info.rt3, new RenderTargetIdentifier(BuiltinRenderTextureType.CurrentActive), blitMat);
                context.ExecuteCommandBuffer(cb);
                cb.Clear();
            }
            {
                Matrix4x4 view_mat = proj_mats[5] * t_mat;
                cb.SetRenderTarget(rts, rts[0]);
                cb.ClearRenderTarget(true, true, Color.clear);
                cb.SetGlobalMatrix(ShaderPropertyID.bake_vp, proj_mats[0] * view_mat);
                context.ExecuteCommandBuffer(cb);
                cb.Clear();
                context.DrawRenderers(cullResults, ref renderSetting, ref filterSetting);
                cb.SetRenderTarget(new RenderTargetIdentifier(info.rt0, 0, CubemapFace.NegativeY));
                cb.Blit(info.rt2, new RenderTargetIdentifier(BuiltinRenderTextureType.CurrentActive), blitMat);
                cb.SetRenderTarget(new RenderTargetIdentifier(info.rt1, 0, CubemapFace.NegativeY));
                cb.Blit(info.rt3, new RenderTargetIdentifier(BuiltinRenderTextureType.CurrentActive), blitMat);
                context.ExecuteCommandBuffer(cb);
                cb.Clear();
            }
        }
    }
}



#endif