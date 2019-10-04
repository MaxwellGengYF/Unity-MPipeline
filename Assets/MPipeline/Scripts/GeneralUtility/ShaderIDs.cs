using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static partial class ShaderIDs
{
    public static readonly int _Count = Shader.PropertyToID("_Count");
    public static readonly int planes = Shader.PropertyToID("planes");
    public static readonly int _FrustumMinPoint = Shader.PropertyToID("_FrustumMinPoint");
    public static readonly int _FrustumMaxPoint = Shader.PropertyToID("_FrustumMaxPoint");
    public static readonly int _DirShadowMap = Shader.PropertyToID("_DirShadowMap");
    public static readonly int _Color = Shader.PropertyToID("_Color");
    public static readonly int _CubeShadowMap = Shader.PropertyToID("_CubeShadowMap");
    public static readonly int _InvVP = Shader.PropertyToID("_InvVP");
    public static readonly int _InvNonJitterVP = Shader.PropertyToID("_InvNonJitterVP");
    public static readonly int _LastVp = Shader.PropertyToID("_LastVp");
    public static readonly int _InvLastVp = Shader.PropertyToID("_InvLastVp");
    public static readonly int _ShadowMapVP = Shader.PropertyToID("_ShadowMapVP");
    public static readonly int _ShadowMapVPs = Shader.PropertyToID("_ShadowMapVPs");
    public static readonly int _ShadowCamPoses = Shader.PropertyToID("_ShadowCamPoses");
    public static readonly int _ShadowDisableDistance = Shader.PropertyToID("_ShadowDisableDistance");
    public static readonly int _CascadeShadowWeight = Shader.PropertyToID("_CascadeShadowWeight");
    public static readonly int _DirLightFinalColor = Shader.PropertyToID("_DirLightFinalColor");
    public static readonly int _DirLightPos = Shader.PropertyToID("_DirLightPos");
    public static readonly int _LightPos = Shader.PropertyToID("_LightPos");
    public static readonly int _MainTex = Shader.PropertyToID("_MainTex");
    public static readonly int _BumpMap = Shader.PropertyToID("_BumpMap");
    public static readonly int _SMMap = Shader.PropertyToID("_SMMap");
    public static readonly int _SoftParam = Shader.PropertyToID("_SoftParam");
    public static readonly int _OffsetIndex = Shader.PropertyToID("_OffsetIndex");
    public static readonly int _ShadowOffset = Shader.PropertyToID("_ShadowOffset");
    public static readonly int _IndexBuffer = Shader.PropertyToID("_IndexBuffer");
    public static readonly int clusterBuffer = Shader.PropertyToID("clusterBuffer");
    public static readonly int instanceCountBuffer = Shader.PropertyToID("instanceCountBuffer");
    public static readonly int resultBuffer = Shader.PropertyToID("resultBuffer");
    public static readonly int verticesBuffer = Shader.PropertyToID("verticesBuffer");
    public static readonly int _LastVerticesBuffer = Shader.PropertyToID("_LastVerticesBuffer");
    public static readonly int dispatchBuffer = Shader.PropertyToID("dispatchBuffer");
    public static readonly int reCheckResult = Shader.PropertyToID("reCheckResult");
    public static readonly int reCheckCount = Shader.PropertyToID("reCheckCount");
    public static readonly int allPoints = Shader.PropertyToID("allPoints");
    public static readonly int _DeltaTime = Shader.PropertyToID("_DeltaTime");
    public static readonly int _LastFrameModel = Shader.PropertyToID("_LastFrameModel");
    public static readonly int _RainTexture = Shader.PropertyToID("_RainTexture");
    public static readonly int _LastFrameDepthTexture = Shader.PropertyToID("_LastFrameDepthTexture");
    public static readonly int _LastFrameMotionVectors = Shader.PropertyToID("_LastFrameMotionVectors");
    public static readonly int _CameraNormals = Shader.PropertyToID("_CameraNormals");
    public static readonly int _FrustumCorners = Shader.PropertyToID("_FrustumCorners");

    public static readonly int _Jitter = Shader.PropertyToID("_Jitter");
    public static readonly int _LastJitter = Shader.PropertyToID("_LastJitter");
    public static readonly int _Sharpness = Shader.PropertyToID("_Sharpness");
    public static readonly int _FinalBlendParameters = Shader.PropertyToID("_FinalBlendParameters");
    public static readonly int _HistoryTex = Shader.PropertyToID("_HistoryTex");
    public static readonly int _TextureSize = Shader.PropertyToID("_TextureSize");
    public static readonly int _TargetElement = Shader.PropertyToID("_TargetElement");
    public static readonly int _TextureBuffer = Shader.PropertyToID("_TextureBuffer");

    public static readonly int _ShadowMapResolution = Shader.PropertyToID("_ShadowMapResolution");
    public static readonly int _LightDir = Shader.PropertyToID("_LightDir");
    public static readonly int _WorldPoses = Shader.PropertyToID("_WorldPoses");
    public static readonly int _PreviousLevel = Shader.PropertyToID("_PreviousLevel");
    public static readonly int _HizDepthTex = Shader.PropertyToID("_HizDepthTex");
    public static readonly int _HizScreenRes = Shader.PropertyToID("_HizScreenRes");
    public static readonly int _VP = Shader.PropertyToID("_VP");
    public static readonly int _Depth = Shader.PropertyToID("_Depth");
    public static readonly int _LastDepth = Shader.PropertyToID("_LastDepth");
    public static readonly int _NonJitterVP = Shader.PropertyToID("_NonJitterVP");
    public static readonly int _NonJitterTextureVP = Shader.PropertyToID("_NonJitterTextureVP");
    public static readonly int _NonJitterTextureP = Shader.PropertyToID("_NonJitterTextureP");
    public static readonly int _Lut3D = Shader.PropertyToID("_Lut3D");
    public static readonly int _Lut3D_Params = Shader.PropertyToID("_Lut3D_Params");
    public static readonly int _PostExposure = Shader.PropertyToID("_PostExposure");
    public static readonly int _TemporalClipBounding = Shader.PropertyToID("_TemporalClipBounding");

    public static readonly int _LightIntensity = Shader.PropertyToID("_LightIntensity");
    public static readonly int _LightColor = Shader.PropertyToID("_LightColor");
    public static readonly int lightPositionBuffer = Shader.PropertyToID("lightPositionBuffer");
    public static readonly int _LightRadius = Shader.PropertyToID("_LightRadius");

    public static readonly int _TimeVar = Shader.PropertyToID("_TimeVar");
    public static readonly int _SkinVerticesBuffer = Shader.PropertyToID("_SkinVerticesBuffer");
    public static readonly int _BonesBuffer = Shader.PropertyToID("_BonesBuffer");

    public static readonly int _PropertiesBuffer = Shader.PropertyToID("_PropertiesBuffer");
    public static readonly int _TempPropBuffer = Shader.PropertyToID("_TempPropBuffer");
    public static readonly int _CameraForward = Shader.PropertyToID("_CameraForward");
    public static readonly int _CameraNearPos = Shader.PropertyToID("_CameraNearPos");
    public static readonly int _CameraFarPos = Shader.PropertyToID("_CameraFarPos");
    public static readonly int _XYPlaneTexture = Shader.PropertyToID("_XYPlaneTexture");
    public static readonly int _ZPlaneTexture = Shader.PropertyToID("_ZPlaneTexture");
    public static readonly int _PointLightTexture = Shader.PropertyToID("_PointLightTexture");
    public static readonly int _AllPointLight = Shader.PropertyToID("_AllPointLight");
    public static readonly int _AllSpotLight = Shader.PropertyToID("_AllSpotLight");
    public static readonly int _PointLightIndexBuffer = Shader.PropertyToID("_PointLightIndexBuffer");
    public static readonly int _SpotLightIndexBuffer = Shader.PropertyToID("_SpotLightIndexBuffer");
    public static readonly int _LightEnabled = Shader.PropertyToID("_LightEnabled");

    public static readonly int heightMapBuffer = Shader.PropertyToID("heightMapBuffer");
    public static readonly int triangleBuffer = Shader.PropertyToID("triangleBuffer");
    public static readonly int _MeshSize = Shader.PropertyToID("_MeshSize");
    public static readonly int _LightFlag = Shader.PropertyToID("_LightFlag");
    public static readonly int _CubeShadowMapArray = Shader.PropertyToID("_CubeShadowMapArray");
    public static readonly int _SpotMapArray = Shader.PropertyToID("_SpotMapArray");
    public static readonly int _TemporalWeight = Shader.PropertyToID("_TemporalWeight");
    public static readonly int _LinearFogDensity = Shader.PropertyToID("_LinearFogDensity");
    public static readonly int _FroxelSize = Shader.PropertyToID("_FroxelSize");
    public static readonly int _VolumeTex = Shader.PropertyToID("_VolumeTex");
    public static readonly int _RandomSeed = Shader.PropertyToID("_RandomSeed");
    public static readonly int _LastVolume = Shader.PropertyToID("_LastVolume");
    public static readonly int _VolumetricLightVar = Shader.PropertyToID("_VolumetricLightVar");
    public static readonly int _CameraClipDistance = Shader.PropertyToID("_CameraClipDistance");
    public static readonly int _Screen_TexelSize = Shader.PropertyToID("_Screen_TexelSize");
    public static readonly int _PointLightCount = Shader.PropertyToID("_PointLightCount");
    public static readonly int _SpotLightCount = Shader.PropertyToID("_SpotLightCount");
    public static readonly int _AllFogVolume = Shader.PropertyToID("_AllFogVolume");
    public static readonly int _FogVolumeCount = Shader.PropertyToID("_FogVolumeCount");
    public static readonly int _SceneOffset = Shader.PropertyToID("_SceneOffset");
    public static readonly int _BackupMap = Shader.PropertyToID("_BackupMap");
    public static readonly int _CameraMotionVectorsTexture = Shader.PropertyToID("_CameraMotionVectorsTexture");
    public static readonly int _CameraDepthTexture = Shader.PropertyToID("_CameraDepthTexture");
    public static readonly int _DepthBufferTexture = Shader.PropertyToID("_DepthBufferTexture");
    public static readonly int _LightMap = Shader.PropertyToID("_LightMap");
    public static readonly int _ReflectionIndices = Shader.PropertyToID("_ReflectionIndices");
    public static readonly int _ReflectionData = Shader.PropertyToID("_ReflectionData");
    public static readonly int _ReflectionTextures = Shader.PropertyToID("_ReflectionTextures");

    public static readonly int _AOROTexture = Shader.PropertyToID("_AOROTexture");
    public static readonly int _Coeff = Shader.PropertyToID("_Coeff");
    public static readonly int _Tex3DSize = Shader.PropertyToID("_Tex3DSize");
    public static readonly int _WorldToLocalMatrix = Shader.PropertyToID("_WorldToLocalMatrix");
    public static readonly int _DecalAtlas = Shader.PropertyToID("_DecalAtlas");
    public static readonly int _DecalNormalAtlas = Shader.PropertyToID("_DecalNormalAtlas");
    public static readonly int _DecalSpecularAtlas = Shader.PropertyToID("_DecalSpecularAtlas");

    public static readonly int _AreaLightBuffer = Shader.PropertyToID("_AreaLightBuffer");
    public static readonly int _AreaLightCount = Shader.PropertyToID("_AreaLightCount");
    public static readonly int _VolumetricNoise = Shader.PropertyToID("_VolumetricNoise");
    public static readonly int _OpaqueScale = Shader.PropertyToID("_OpaqueScale");
    public static readonly int _GrabTexture = Shader.PropertyToID("_GrabTexture");

    public static readonly int _TileSize = Shader.PropertyToID("_TileSize");
    public static readonly int _DepthBoundTexture = Shader.PropertyToID("_DepthBoundTexture");
    public static readonly int _PointLightTile = Shader.PropertyToID("_PointLightTile");
    public static readonly int _SpotLightTile = Shader.PropertyToID("_SpotLightTile");
    public static readonly int _DecalTile = Shader.PropertyToID("_DecalTile");
    public static readonly int _CameraPos = Shader.PropertyToID("_CameraPos");
    public static readonly int _TargetDepthTexture = Shader.PropertyToID("_TargetDepthTexture");
    public static readonly int _IESAtlas = Shader.PropertyToID("_IESAtlas");
    public static readonly int _AllDecals = Shader.PropertyToID("_AllDecals");
    public static readonly int _DecalCount = Shader.PropertyToID("_DecalCount");

    public static readonly int _ReflectionTex = Shader.PropertyToID("_ReflectionTex");
    public static readonly int _ReflectionIndex = Shader.PropertyToID("_ReflectionIndex");
    public static readonly int _CameraReflectionTexture = Shader.PropertyToID("_CameraReflectionTexture");
    public static readonly int _CameraGITexture = Shader.PropertyToID("_CameraGITexture");

    public static readonly int _TransformMatrices = Shader.PropertyToID("_TransformMatrices");
    public static readonly int _OriginTransformMatrices = Shader.PropertyToID("_OriginTransformMatrices");
    public static readonly int _LastTransformMatrices = Shader.PropertyToID("_LastTransformMatrices");
    public static readonly int _NoiseTexture = Shader.PropertyToID("_NoiseTexture");
    public static readonly int _NoiseTexture_Size = Shader.PropertyToID("_NoiseTexture_Size");
    public static readonly int _Offset = Shader.PropertyToID("_Offset");
    public static readonly int _OffsetDirection = Shader.PropertyToID("_OffsetDirection");

    public static readonly int _IndexTexture = Shader.PropertyToID("_IndexTexture");
    public static readonly int _IndexTextureSize = Shader.PropertyToID("_IndexTextureSize");
    public static readonly int _VirtualTexture = Shader.PropertyToID("_VirtualTexture");

    public static readonly int _VTVariables = Shader.PropertyToID("_VTVariables");
    public static readonly int _ElementBuffer = Shader.PropertyToID("_ElementBuffer");

    public static readonly int _TerrainChunks = Shader.PropertyToID("_TerrainChunks");
    public static readonly int _CullResultBuffer = Shader.PropertyToID("_CullResultBuffer");
    public static readonly int _DispatchBuffer = Shader.PropertyToID("_DispatchBuffer");

    public static readonly int _BlendTex = Shader.PropertyToID("_BlendTex");
    public static readonly int _OffsetScale = Shader.PropertyToID("_OffsetScale");
    public static readonly int _BlendTex_TexelSize = Shader.PropertyToID("_BlendTex_TexelSize");
    public static readonly int _BlendAlpha = Shader.PropertyToID("_BlendAlpha");

    public static readonly int _ProceduralBuffer = Shader.PropertyToID("_ProceduralBuffer");
    public static readonly int _ProceduralCount = Shader.PropertyToID("_ProceduralCount");
    public static readonly int _GradientMap = Shader.PropertyToID("_GradientMap");
    public static readonly int _AlbedoVoxel = Shader.PropertyToID("_AlbedoVoxel");
    public static readonly int _VoxelWorldToLocal = Shader.PropertyToID("_VoxelWorldToLocal");
    public static readonly int _CustomLut = Shader.PropertyToID("_CustomLut");
    public static readonly int _MaterialBuffer = Shader.PropertyToID("_MaterialBuffer");
    public static readonly int _TriangleMaterialBuffer = Shader.PropertyToID("_TriangleMaterialBuffer");
    public static readonly int _MaterialAddBuffer = Shader.PropertyToID("_MaterialAddBuffer");

    public static readonly int _GPURPMainTex = Shader.PropertyToID("_GPURPMainTex");
    public static readonly int _GPURPBumpMap = Shader.PropertyToID("_GPURPBumpMap");
    public static readonly int _GPURPEmissionMap = Shader.PropertyToID("_GPURPEmissionMap");
    public static readonly int _GPURPHeightMap = Shader.PropertyToID("_GPURPHeightMap");

    public static readonly int _TerrainMainTexArray = Shader.PropertyToID("_TerrainMainTexArray");
    public static readonly int _TerrainBumpMapArray = Shader.PropertyToID("_TerrainBumpMapArray");
    public static readonly int _TerrainSMTexArray = Shader.PropertyToID("_TerrainSMTexArray");
    public static readonly int _SourceTex = Shader.PropertyToID("_SourceTex");
    public static readonly int _DestTex = Shader.PropertyToID("_DestTex");
    public static readonly int _VirtualMainTex = Shader.PropertyToID("_VirtualMainTex");
    public static readonly int _VirtualBumpMap = Shader.PropertyToID("_VirtualBumpMap");
    public static readonly int _VirtualSMO = Shader.PropertyToID("_VirtualSMO");
    public static readonly int _VirtualHeightmap = Shader.PropertyToID("_VirtualHeightmap");
    public static readonly int _HeightScaleOffset = Shader.PropertyToID("_HeightScaleOffset");

}
