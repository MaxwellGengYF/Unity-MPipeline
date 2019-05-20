using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.Rendering;
namespace MPipeline
{
    [CreateAssetMenu(menuName = "GPURP Events/Post Processing")]
    [RequireEvent(typeof(PropertySetEvent))]
    public class FinalPostEvent : PipelineEvent
    {
        struct PostEffect
        {
            public Type type;
            public PostProcessEffectRenderer renderer;
            public bool needBlit;
        }
        public PostProcessProfile profile;
        public PostProcessResources resources;
        public bool enabledPost = true;
        public bool enableInEditor = true;
        private List<PostEffect> allPostEffects;
        private Dictionary<Type, PostProcessEffectSettings> allSettings;
        private PostProcessRenderContext postContext;
        public CyberColor cyberColor;
        public Shader debug;
        private Material debugMat;

        T AddEvents<S, T>(bool useBlit = false) where T : PostProcessEffectRenderer, new()
        {
            T renderer = new T();
            renderer.Init();
            allPostEffects.Add(new PostEffect { renderer = renderer, needBlit = useBlit, type = typeof(S) });
            return renderer;
        }
        protected override void Init(PipelineResources res)
        {
            //      debugMat = new Material(debug);
            allSettings = new Dictionary<Type, PostProcessEffectSettings>(10);
            allPostEffects = new List<PostEffect>(10);
            AddEvents<DepthOfField, DepthOfFieldRenderer>(true);
            AddEvents<MotionBlur, MotionBlurRenderer>(true);
            AddEvents<LensDistortion, LensDistortionRenderer>();
            AddEvents<ChromaticAberration, ChromaticAberrationRenderer>();
            AddEvents<Bloom, BloomRenderer>();
            AddEvents<AutoExposure, AutoExposureRenderer>();
            AddEvents<Vignette, VignetteRenderer>();
            AddEvents<ColorGrading, ColorGradingRenderer>();
            postContext = new PostProcessRenderContext();
            postContext.Reset();
            postContext.propertySheets = new PropertySheetFactory();
            postContext.resources = resources;
            postContext.logHistogram = new LogHistogram();
            postContext.uberSheet = new PropertySheet(new Material(resources.shaders.uber));
            Shader.SetGlobalFloat("_RenderViewportScaleFactor", 1);
            cyberColor.Init();
            foreach (var i in profile.settings)
            {
                allSettings.Add(i.GetType(), i);
            }
        }

        public override bool CheckProperty()
        {
            return postContext != null && postContext.uberSheet.material != null && cyberColor.Check();
        }

        protected override void Dispose()
        {
            foreach (var i in allPostEffects)
            {
                i.renderer.Release();
            }
            cyberColor.Dispose();
            postContext.uberSheet.Release();
            postContext.logHistogram.Release();
        }

        public override void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data)
        {
            if (!enabledPost)
            {
                data.buffer.Blit(cam.targets.renderTargetIdentifier, cam.cameraTarget);
                return;
            }
#if UNITY_EDITOR
            if (!enableInEditor && RenderPipeline.renderingEditor)
            {
                data.buffer.Blit(cam.targets.renderTargetIdentifier, cam.cameraTarget);
                return;
            }
#endif
            postContext.camera = cam.cam;
            postContext.command = data.buffer;
            postContext.sourceFormat = RenderTextureFormat.ARGBHalf;
            postContext.autoExposureTexture = RuntimeUtilities.whiteTexture;
            postContext.bloomBufferNameID = -1;
            RenderTargetIdentifier source, dest;
            postContext.source = cam.targets.renderTargetIdentifier;
            postContext.destination = cam.targets.backupIdentifier;
            postContext.logHistogram.Generate(postContext);
            foreach (var i in allPostEffects)
            {
                PostProcessEffectSettings setting;
                if (allSettings.TryGetValue(i.type, out setting))
                {
                    if (i.needBlit && setting.active)
                    {
                        PipelineFunctions.RunPostProcess(ref cam.targets, out source, out dest);
                        postContext.source = source;
                        postContext.destination = dest;
                    }
                    i.renderer.SetSettings(setting);
                    i.renderer.Render(postContext);
                }
            };
            cyberColor.FrameUpdate(data.buffer);
            /*HizOcclusionData hizOccData;
            hizOccData = IPerCameraData.GetProperty(cam, () => new HizOcclusionData());
            data.buffer.Blit(hizOccData.historyDepth, cam.cameraTarget);*/
            //data.buffer.Blit(ShaderIDs._CameraMotionVectorsTexture, cam.cameraTarget);
            data.buffer.BlitSRT(cam.targets.renderTargetIdentifier, cam.cameraTarget, postContext.uberSheet.material, 0, postContext.uberSheet.properties);
            if (postContext.bloomBufferNameID > -1) data.buffer.ReleaseTemporaryRT(postContext.bloomBufferNameID);
        }
    }
}