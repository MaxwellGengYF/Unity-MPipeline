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
    public unsafe sealed class FinalPostEvent : PipelineEvent
    {
        struct PostEffect
        {
            public Type type;
            public PostProcessEffectRenderer renderer;
            public bool needBlit;
        }
        public PostProcessResources resources;
        public bool enabledPost = true;
        public bool enableInEditor = true;
        private List<PostEffect> allPostEffects;
        private PostProcessRenderContext postContext;
        [SerializeField]
        private CyberSet cyberGlitch;
       // public Shader debug;
      //  private Material debugMat;
        public Texture3D customLut;

        T AddEvents<S, T>(bool useBlit = false) where T : PostProcessEffectRenderer, new()
        {
            T renderer = new T();
            renderer.Init();
            allPostEffects.Add(new PostEffect { renderer = renderer, needBlit = useBlit, type = typeof(S) });
            return renderer;
        }
        protected override void Init(PipelineResources res)
        {
           // debugMat = new Material(debug);

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
            cyberGlitch.Init(res);
        }

        public override bool CheckProperty()
        {
            return postContext != null && postContext.uberSheet.material != null && cyberGlitch.Check();
        }

        protected override void Dispose()
        {
            foreach (var i in allPostEffects)
            {
                i.renderer.Release();
            }
            postContext.uberSheet.Release();
            postContext.logHistogram.Release();
            cyberGlitch.Dispose();
        }
        private struct PtrEqual : IFunction<ulong, ulong, bool>
        {
            public bool Run(ref ulong a, ref ulong b)
            {
                return a == b;
            }
        }
        public override void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data)
        {

            if (!enabledPost || !cam.postProfile)
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
            if (customLut)
            {
                data.buffer.SetGlobalTexture(ShaderIDs._CustomLut, customLut);
                data.buffer.EnableShaderKeyword("CUSTOM_LUT");
            }
            else
            {
                data.buffer.DisableShaderKeyword("CUSTOM_LUT");
            }
            NativeDictionary<ulong, ulong, PtrEqual> allSettings = new NativeDictionary<ulong, ulong, PtrEqual>(allPostEffects.Count, Unity.Collections.Allocator.Temp, new PtrEqual());
            foreach (var i in cam.postProfile.settings)
            {
                allSettings.Add((ulong)MUnsafeUtility.GetManagedPtr(i.GetType()), (ulong)MUnsafeUtility.GetManagedPtr(i));
            }
            postContext.camera = cam.cam;
            postContext.command = data.buffer;
            postContext.sourceFormat = RenderTextureFormat.ARGBHalf;
            postContext.autoExposureTexture = RuntimeUtilities.whiteTexture;
            postContext.bloomBufferNameID = -1;
            RenderTargetIdentifier source, dest;
            postContext.source = cam.targets.renderTargetIdentifier;
            postContext.destination = cam.targets.backupIdentifier;
            postContext.logHistogram.Generate(postContext);
            cyberGlitch.Render(data.buffer, ref cam.targets);
            foreach (var i in allPostEffects)
            {
                ulong settingsPtr;
                if (allSettings.Get((ulong)MUnsafeUtility.GetManagedPtr(i.type), out settingsPtr))
                {
                    PostProcessEffectSettings setting = MUnsafeUtility.GetObject<PostProcessEffectSettings>((void*)settingsPtr);
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
            allSettings.Dispose();
            //   data.buffer.Blit(ShaderIDs._CameraMotionVectorsTexture, cam.cameraTarget);
            //     data.buffer.BlitSRT(cam.cameraTarget, debugMat, 0);
            //  HizOcclusionData occD = IPerCameraData.GetProperty(cam, (c) => new HizOcclusionData(c.cam.pixelWidth));
            //   data.buffer.Blit(occD.historyDepth, cam.cameraTarget, debugMat, 0);

            data.buffer.BlitSRT(cam.targets.renderTargetIdentifier, cam.cameraTarget, postContext.uberSheet.material, 0, postContext.uberSheet.properties);
            if (postContext.bloomBufferNameID > -1) data.buffer.ReleaseTemporaryRT(postContext.bloomBufferNameID);
        }
    }
}