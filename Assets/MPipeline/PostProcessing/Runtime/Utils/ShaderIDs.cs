namespace UnityEngine.Rendering.PostProcessing
{
    // Pre-hashed shader ids - naming conventions are a bit off in this file as we use the same
    // fields names as in the shaders for ease of use... Would be nice to clean this up at some
    // point.
    public static class ShaderIDs
    {
        public static readonly int MainTex                         = Shader.PropertyToID("_MainTex");

        public static readonly int Jitter                          = Shader.PropertyToID("_Jitter");
        public static readonly int Sharpness                       = Shader.PropertyToID("_Sharpness");
        public static readonly int FinalBlendParameters            = Shader.PropertyToID("_FinalBlendParameters");
        public static readonly int HistoryTex                      = Shader.PropertyToID("_HistoryTex");

        public static readonly int SMAA_Flip                       = Shader.PropertyToID("_SMAA_Flip");
        public static readonly int SMAA_Flop                       = Shader.PropertyToID("_SMAA_Flop");

        public static readonly int AOParams                        = Shader.PropertyToID("_AOParams");
        public static readonly int AOColor                         = Shader.PropertyToID("_AOColor");
        public static readonly int OcclusionTexture1               = Shader.PropertyToID("_OcclusionTexture1");
        public static readonly int OcclusionTexture2               = Shader.PropertyToID("_OcclusionTexture2");
        public static readonly int SAOcclusionTexture              = Shader.PropertyToID("_SAOcclusionTexture");
        public static readonly int MSVOcclusionTexture             = Shader.PropertyToID("_MSVOcclusionTexture");
        public static readonly int DepthCopy                       = Shader.PropertyToID("DepthCopy");
        public static readonly int LinearDepth                     = Shader.PropertyToID("LinearDepth");
        public static readonly int LowDepth1                       = Shader.PropertyToID("LowDepth1");
        public static readonly int LowDepth2                       = Shader.PropertyToID("LowDepth2");
        public static readonly int LowDepth3                       = Shader.PropertyToID("LowDepth3");
        public static readonly int LowDepth4                       = Shader.PropertyToID("LowDepth4");
        public static readonly int TiledDepth1                     = Shader.PropertyToID("TiledDepth1");
        public static readonly int TiledDepth2                     = Shader.PropertyToID("TiledDepth2");
        public static readonly int TiledDepth3                     = Shader.PropertyToID("TiledDepth3");
        public static readonly int TiledDepth4                     = Shader.PropertyToID("TiledDepth4");
        public static readonly int Occlusion1                      = Shader.PropertyToID("Occlusion1");
        public static readonly int Occlusion2                      = Shader.PropertyToID("Occlusion2");
        public static readonly int Occlusion3                      = Shader.PropertyToID("Occlusion3");
        public static readonly int Occlusion4                      = Shader.PropertyToID("Occlusion4");
        public static readonly int Combined1                       = Shader.PropertyToID("Combined1");
        public static readonly int Combined2                       = Shader.PropertyToID("Combined2");
        public static readonly int Combined3                       = Shader.PropertyToID("Combined3");

        public static readonly int SSRResolveTemp                  = Shader.PropertyToID("_SSRResolveTemp");
        public static readonly int Noise                           = Shader.PropertyToID("_Noise");
        public static readonly int Test                            = Shader.PropertyToID("_Test");
        public static readonly int Resolve                         = Shader.PropertyToID("_Resolve");
        public static readonly int History                         = Shader.PropertyToID("_History");
        public static readonly int ViewMatrix                      = Shader.PropertyToID("_ViewMatrix");
        public static readonly int InverseViewMatrix               = Shader.PropertyToID("_InverseViewMatrix");
        public static readonly int InverseProjectionMatrix         = Shader.PropertyToID("_InverseProjectionMatrix");
        public static readonly int ScreenSpaceProjectionMatrix     = Shader.PropertyToID("_ScreenSpaceProjectionMatrix");
        public static readonly int Params2                         = Shader.PropertyToID("_Params2");

        public static readonly int FogColor                        = Shader.PropertyToID("_FogColor");
        public static readonly int FogParams                       = Shader.PropertyToID("_FogParams");

        public static readonly int VelocityScale                   = Shader.PropertyToID("_VelocityScale");
        public static readonly int MaxBlurRadius                   = Shader.PropertyToID("_MaxBlurRadius");
        public static readonly int RcpMaxBlurRadius                = Shader.PropertyToID("_RcpMaxBlurRadius");
        public static readonly int VelocityTex                     = Shader.PropertyToID("_VelocityTex");
        public static readonly int Tile2RT                         = Shader.PropertyToID("_Tile2RT");
        public static readonly int Tile4RT                         = Shader.PropertyToID("_Tile4RT");
        public static readonly int Tile8RT                         = Shader.PropertyToID("_Tile8RT");
        public static readonly int TileMaxOffs                     = Shader.PropertyToID("_TileMaxOffs");
        public static readonly int TileMaxLoop                     = Shader.PropertyToID("_TileMaxLoop");
        public static readonly int TileVRT                         = Shader.PropertyToID("_TileVRT");
        public static readonly int NeighborMaxTex                  = Shader.PropertyToID("_NeighborMaxTex");
        public static readonly int LoopCount                       = Shader.PropertyToID("_LoopCount");

        public static readonly int DepthOfFieldTemp                = Shader.PropertyToID("_DepthOfFieldTemp");
        public static readonly int DepthOfFieldTex                 = Shader.PropertyToID("_DepthOfFieldTex");
        public static readonly int Distance                        = Shader.PropertyToID("_Distance");
        public static readonly int LensCoeff                       = Shader.PropertyToID("_LensCoeff");
        public static readonly int MaxCoC                          = Shader.PropertyToID("_MaxCoC");
        public static readonly int RcpMaxCoC                       = Shader.PropertyToID("_RcpMaxCoC");
        public static readonly int RcpAspect                       = Shader.PropertyToID("_RcpAspect");
        public static readonly int CoCTex                          = Shader.PropertyToID("_CoCTex");
        public static readonly int TaaParams                       = Shader.PropertyToID("_TaaParams");

        public static readonly int AutoExposureTex                 = Shader.PropertyToID("_AutoExposureTex");
        public static readonly int HistogramBuffer                 = Shader.PropertyToID("_HistogramBuffer");
        public static readonly int Params                          = Shader.PropertyToID("_Params");
        public static readonly int ScaleOffsetRes                  = Shader.PropertyToID("_ScaleOffsetRes");

        public static readonly int BloomTex                        = Shader.PropertyToID("_BloomTex");
        public static readonly int SampleScale                     = Shader.PropertyToID("_SampleScale");
        public static readonly int Threshold                       = Shader.PropertyToID("_Threshold");
        public static readonly int ColorIntensity                  = Shader.PropertyToID("_ColorIntensity");
        public static readonly int Bloom_DirtTex                   = Shader.PropertyToID("_Bloom_DirtTex");
        public static readonly int Bloom_Settings                  = Shader.PropertyToID("_Bloom_Settings");
        public static readonly int Bloom_Color                     = Shader.PropertyToID("_Bloom_Color");
        public static readonly int Bloom_DirtTileOffset            = Shader.PropertyToID("_Bloom_DirtTileOffset");

        public static readonly int ChromaticAberration_Amount      = Shader.PropertyToID("_ChromaticAberration_Amount");
        public static readonly int ChromaticAberration_SpectralLut = Shader.PropertyToID("_ChromaticAberration_SpectralLut");

        public static readonly int Distortion_CenterScale          = Shader.PropertyToID("_Distortion_CenterScale");
        public static readonly int Distortion_Amount               = Shader.PropertyToID("_Distortion_Amount");

        public static readonly int Lut2D                           = Shader.PropertyToID("_Lut2D");
        public static readonly int Lut3D                           = Shader.PropertyToID("_Lut3D");
        public static readonly int Lut3D_Params                    = Shader.PropertyToID("_Lut3D_Params");
        public static readonly int Lut2D_Params                    = Shader.PropertyToID("_Lut2D_Params");
        public static readonly int UserLut2D_Params                = Shader.PropertyToID("_UserLut2D_Params");
        public static readonly int PostExposure                    = Shader.PropertyToID("_PostExposure");
        public static readonly int ColorBalance                    = Shader.PropertyToID("_ColorBalance");
        public static readonly int ColorFilter                     = Shader.PropertyToID("_ColorFilter");
        public static readonly int HueSatCon                       = Shader.PropertyToID("_HueSatCon");
        public static readonly int Brightness                      = Shader.PropertyToID("_Brightness");
        public static readonly int ChannelMixerRed                 = Shader.PropertyToID("_ChannelMixerRed");
        public static readonly int ChannelMixerGreen               = Shader.PropertyToID("_ChannelMixerGreen");
        public static readonly int ChannelMixerBlue                = Shader.PropertyToID("_ChannelMixerBlue");
        public static readonly int Lift                            = Shader.PropertyToID("_Lift");
        public static readonly int InvGamma                        = Shader.PropertyToID("_InvGamma");
        public static readonly int Gain                            = Shader.PropertyToID("_Gain");
        public static readonly int Curves                          = Shader.PropertyToID("_Curves");
        public static readonly int CustomToneCurve                 = Shader.PropertyToID("_CustomToneCurve");
        public static readonly int ToeSegmentA                     = Shader.PropertyToID("_ToeSegmentA");
        public static readonly int ToeSegmentB                     = Shader.PropertyToID("_ToeSegmentB");
        public static readonly int MidSegmentA                     = Shader.PropertyToID("_MidSegmentA");
        public static readonly int MidSegmentB                     = Shader.PropertyToID("_MidSegmentB");
        public static readonly int ShoSegmentA                     = Shader.PropertyToID("_ShoSegmentA");
        public static readonly int ShoSegmentB                     = Shader.PropertyToID("_ShoSegmentB");

        public static readonly int Vignette_Color                  = Shader.PropertyToID("_Vignette_Color");
        public static readonly int Vignette_Center                 = Shader.PropertyToID("_Vignette_Center");
        public static readonly int Vignette_Settings               = Shader.PropertyToID("_Vignette_Settings");
        public static readonly int Vignette_Mask                   = Shader.PropertyToID("_Vignette_Mask");
        public static readonly int Vignette_Opacity                = Shader.PropertyToID("_Vignette_Opacity");
        public static readonly int Vignette_Mode                   = Shader.PropertyToID("_Vignette_Mode");

        public static readonly int Grain_Params1                   = Shader.PropertyToID("_Grain_Params1");
        public static readonly int Grain_Params2                   = Shader.PropertyToID("_Grain_Params2");
        public static readonly int GrainTex                        = Shader.PropertyToID("_GrainTex");
        public static readonly int Phase                           = Shader.PropertyToID("_Phase");

        public static readonly int LumaInAlpha                     = Shader.PropertyToID("_LumaInAlpha");

        public static readonly int DitheringTex                    = Shader.PropertyToID("_DitheringTex");
        public static readonly int Dithering_Coords                = Shader.PropertyToID("_Dithering_Coords");

        public static readonly int From                            = Shader.PropertyToID("_From");
        public static readonly int To                              = Shader.PropertyToID("_To");
        public static readonly int Interp                          = Shader.PropertyToID("_Interp");
        public static readonly int TargetColor                     = Shader.PropertyToID("_TargetColor");

        public static readonly int HalfResFinalCopy                = Shader.PropertyToID("_HalfResFinalCopy");
        public static readonly int WaveformSource                  = Shader.PropertyToID("_WaveformSource");
        public static readonly int WaveformBuffer                  = Shader.PropertyToID("_WaveformBuffer");
        public static readonly int VectorscopeBuffer               = Shader.PropertyToID("_VectorscopeBuffer");

        public static readonly int RenderViewportScaleFactor       = Shader.PropertyToID("_RenderViewportScaleFactor");

        public static readonly int UVTransform                     = Shader.PropertyToID("_UVTransform");
    }
}
