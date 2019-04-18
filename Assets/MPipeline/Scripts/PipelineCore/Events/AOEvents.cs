using UnityEngine;
using System.Collections;
using UnityEngine.Rendering;
using System.Collections.Generic;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Unity.Collections;
namespace MPipeline
{
    [CreateAssetMenu(menuName = "GPURP Events/AmbientOcclusion")]
    [RequireEvent(typeof(LightingEvent))]
    public unsafe class AOEvents : PipelineEvent
    {
        private PropertySetEvent propertySetEvent;


        //C# To Shader Property
        ///Public
        [Header("Render Property")]

        [SerializeField]
        [Range(1, 4)]
        int DirSampler = 2;


        [SerializeField]
        [Range(1, 8)]
        int SliceSampler = 2;


        [SerializeField]
        [Range(1, 5)]
        float Radius = 2.5f;


        [SerializeField]
        [Range(0, 1)]
        float Intensity = 1;


        [SerializeField]
        [Range(1, 8)]
        float Power = 2.5f;

        [Header("Filtter Property")]

        [Range(0, 1)]
        [SerializeField]
        float Sharpeness = 0.25f;

        [Range(1, 5)]
        [SerializeField]
        float TemporalScale = 1;

        [Range(0, 1)]
        [SerializeField]
        float TemporalResponse = 1;


        //BaseProperty
        private Material GTAOMaterial;
        private ComputeBuffer dataBuffer;

        //Transform property 


        // private

        private uint m_sampleStep = 0;
        private static readonly float[] m_temporalRotations = { 60, 300, 180, 240, 120, 0 };
        private static readonly float[] m_spatialOffsets = { 0, 0.5f, 0.25f, 0.75f };



        //Shader Property
        ///Public
        /*
        private static int _ProjectionMatrix_ID = Shader.PropertyToID("_ProjectionMatrix");
        private static int _LastFrameViewProjectionMatrix_ID = Shader.PropertyToID("_LastFrameViewProjectionMatrix");
        private static int _View_ProjectionMatrix_ID = Shader.PropertyToID("_View_ProjectionMatrix");
        private static int _Inverse_View_ProjectionMatrix_ID = Shader.PropertyToID("_Inverse_View_ProjectionMatrix");
        private static int _WorldToCameraMatrix_ID = Shader.PropertyToID("_WorldToCameraMatrix");
        private static int _CameraToWorldMatrix_ID = Shader.PropertyToID("_CameraToWorldMatrix");
       

        private static int _AO_DirSampler_ID = Shader.PropertyToID("_AO_DirSampler");
        private static int _AO_SliceSampler_ID = Shader.PropertyToID("_AO_SliceSampler");
        private static int _AO_Power_ID = Shader.PropertyToID("_AO_Power");
        private static int _AO_Intensity_ID = Shader.PropertyToID("_AO_Intensity");
        private static int _AO_Radius_ID = Shader.PropertyToID("_AO_Radius");
        private static int _AO_Sharpeness_ID = Shader.PropertyToID("_AO_Sharpeness");
        private static int _AO_TemporalScale_ID = Shader.PropertyToID("_AO_TemporalScale");
        private static int _AO_TemporalResponse_ID = Shader.PropertyToID("_AO_TemporalResponse");
         */

        ///Private
      /*  private static int _AO_HalfProjScale_ID = Shader.PropertyToID("_AO_HalfProjScale");
        private static int _AO_TemporalOffsets_ID = Shader.PropertyToID("_AO_TemporalOffsets");
        private static int _AO_TemporalDirections_ID = Shader.PropertyToID("_AO_TemporalDirections");
        private static int _AO_UVToView_ID = Shader.PropertyToID("_AO_UVToView");
        private static int _AO_RT_TexelSize_ID = Shader.PropertyToID("_AO_RT_TexelSize");*/

        private static int _BentNormal_Texture_ID = Shader.PropertyToID("_BentNormal_Texture");
        private static int _GTAO_Texture_ID = Shader.PropertyToID("_GTAO_Texture");
        private static int _GTAO_Spatial_Texture_ID = Shader.PropertyToID("_GTAO_Spatial_Texture");
        private static int _PrevRT_ID = Shader.PropertyToID("_PrevRT");
        private static int _CurrRT_ID = Shader.PropertyToID("_CurrRT");
        private static int _UpSampleRT = Shader.PropertyToID("_UpSampleRT");
        private static int _AOData = Shader.PropertyToID("_AOData");
        private PropertySetEvent proper;
        private RenderTargetIdentifier[] AO_BentNormal_ID = new RenderTargetIdentifier[2];
     //   public Shader debug;
     //   private Material debugMat;
        /* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* */
        protected override void Init(PipelineResources resources)
        {
        //    debugMat = new Material(debug);
            GTAOMaterial = new Material(resources.shaders.gtaoShader);
            propertySetEvent = RenderPipeline.GetEvent<PropertySetEvent>();
            dataBuffer = new ComputeBuffer(1, sizeof(AOData));
            proper = RenderPipeline.GetEvent<PropertySetEvent>();
        }

        public override bool CheckProperty()
        {
            return GTAOMaterial != null;
        }

        protected override void OnEnable()
        {
            RenderPipeline.ExecuteBufferAtFrameEnding((cb) => cb.EnableShaderKeyword("EnableGTAO"));
        }

        protected override void OnDisable()
        {
            RenderPipeline.ExecuteBufferAtFrameEnding((cb) => cb.DisableShaderKeyword("EnableGTAO"));
        }
        private struct GetDataEvent : IGetCameraData
        {
            public int2 res;
            public IPerCameraData Run()
            {
                return new AOHistoryData(res.x, res.y);
            }
        }
        public override void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data)
        {
            int2 res = int2(cam.cam.pixelWidth / 2, cam.cam.pixelHeight / 2);
            int2 originRes = int2(cam.cam.pixelWidth, cam.cam.pixelHeight);
            GetDataEvent evt = new GetDataEvent
            {
                res = res
            };
            AOHistoryData historyData = IPerCameraData.GetProperty<AOHistoryData, IGetCameraData>(cam, evt);
            UpdateVariable_SSAO(historyData, cam, ref data, res, originRes);
            RenderSSAO(historyData, cam, ref data, res, originRes);
        }

        protected override void Dispose()
        {
            if (Application.isPlaying)
            {
                Destroy(GTAOMaterial);
            }
            else
            {
                DestroyImmediate(GTAOMaterial);
            }
            dataBuffer.Dispose();
            propertySetEvent = null;
        }
        private struct AOData
        {
            public float4x4 worldToCamera;
            public float4x4 cameraToWorld;
            public float4x4 inverseVP;
            public float dirSampler;
            public float sliceSampler;
            public float intensity;
            public float radius;
            public float power;
            public float sharpness;
            public float temporalScale;
            public float temporalResponse;
            public float4 uvToView;
            public float halfProjScale;
            public float4 texelSize;
            public float temporalDirection;
            public float temporalOffset;
        }

        ////////////////////////SSAO Function////////////////////////
        private void UpdateVariable_SSAO(AOHistoryData historyData, PipelineCamera cam, ref PipelineCommandData data, int2 renderResolution, int2 originResolution)
        {
            CommandBuffer buffer = data.buffer;
            Vector4 oneOverSize_Size;
            //----------------------------------------------------------------------------------
            float fovRad = cam.cam.fieldOfView * Mathf.Deg2Rad;
            float invHalfTanFov = 1 / Mathf.Tan(fovRad * 0.5f);
            Vector2 focalLen = new Vector2(invHalfTanFov * ((float)renderResolution.y / (float)renderResolution.x), invHalfTanFov);
            Vector2 invFocalLen = new Vector2(1 / focalLen.x, 1 / focalLen.y);

            //----------------------------------------------------------------------------------
            float projScale;
            projScale = renderResolution.y / (Mathf.Tan(fovRad * 0.5f) * 2) * 0.5f;

            //----------------------------------------------------------------------------------
            oneOverSize_Size = new Vector4(1 / (float)renderResolution.x, 1 / (float)renderResolution.y, (float)renderResolution.x, (float)renderResolution.y);
            //----------------------------------------------------------------------------------
            float temporalRotation = m_temporalRotations[m_sampleStep % 6];
            float temporalOffset = m_spatialOffsets[(m_sampleStep / 6) % 4];
            m_sampleStep++;
            NativeArray<AOData> datas = new NativeArray<AOData>(1, Allocator.Temp);
            AOData* dataPtr = datas.Ptr();
            dataPtr->worldToCamera = cam.cam.worldToCameraMatrix;
            dataPtr->cameraToWorld = cam.cam.cameraToWorldMatrix;
            dataPtr->inverseVP = proper.inverseVP;
            dataPtr->dirSampler = DirSampler;
            dataPtr->sliceSampler = SliceSampler;
            dataPtr->intensity = Intensity;
            dataPtr->radius = Radius;
            dataPtr->power = Power;
            dataPtr->sharpness = Sharpeness;
            dataPtr->temporalScale = TemporalScale;
            dataPtr->temporalResponse = TemporalResponse;
            dataPtr->uvToView = new Vector4(2 * invFocalLen.x, 2 * invFocalLen.y, -1 * invFocalLen.x, -1 * invFocalLen.y);
            dataPtr->halfProjScale = projScale;
            dataPtr->texelSize = oneOverSize_Size;
            dataPtr->temporalDirection = temporalRotation / 360;
            dataPtr->temporalOffset = temporalOffset;
            dataBuffer.SetData(datas);
            datas.Dispose();
            buffer.SetGlobalBuffer(_AOData, dataBuffer);
            //----------------------------------------------------------------------------------
            //TODO
            //Resize
            historyData.UpdateSize(originResolution.x, originResolution.y);
        }

        private void RenderSSAO(AOHistoryData historyData, PipelineCamera cam, ref PipelineCommandData data, int2 renderResolution, int2 originResolution)
        {
            CommandBuffer buffer = data.buffer;
            buffer.GetTemporaryRT(_GTAO_Texture_ID, renderResolution.x, renderResolution.y, 0, FilterMode.Bilinear, RenderTextureFormat.RGHalf, RenderTextureReadWrite.Linear);
            buffer.GetTemporaryRT(_BentNormal_Texture_ID, renderResolution.x, renderResolution.y, 0, FilterMode.Bilinear, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            AO_BentNormal_ID[0] = _GTAO_Texture_ID;
            AO_BentNormal_ID[1] = _BentNormal_Texture_ID;
            //Resolve GTAO 
            buffer.BlitMRT(AO_BentNormal_ID, _GTAO_Texture_ID, GTAOMaterial, 0);

            //Spatial filter
            //------//XBlur
            buffer.GetTemporaryRT(_GTAO_Spatial_Texture_ID, renderResolution.x, renderResolution.y, 0, FilterMode.Point, RenderTextureFormat.RGHalf);
            buffer.BlitSRT(_GTAO_Spatial_Texture_ID, GTAOMaterial, 1);
            //------//YBlur
            buffer.CopyTexture(_GTAO_Spatial_Texture_ID, AO_BentNormal_ID[0]);
            buffer.BlitSRT(_GTAO_Spatial_Texture_ID, GTAOMaterial, 2);
            buffer.GetTemporaryRT(_UpSampleRT, renderResolution.x, renderResolution.y, 0, FilterMode.Point, RenderTextureFormat.RGHalf);
            buffer.BlitSRT(_UpSampleRT, GTAOMaterial, 4);
            //Temporal filter
            buffer.SetGlobalTexture(_PrevRT_ID, historyData.prev_Texture);
            buffer.GetTemporaryRT(_CurrRT_ID, originResolution.x, originResolution.y, 0, FilterMode.Point, RenderTextureFormat.RGHalf);
            buffer.BlitSRT(_CurrRT_ID, GTAOMaterial, 3);
            buffer.CopyTexture(_CurrRT_ID, historyData.prev_Texture);
            buffer.ReleaseTemporaryRT(_GTAO_Spatial_Texture_ID);
            buffer.ReleaseTemporaryRT(_CurrRT_ID);
            buffer.ReleaseTemporaryRT(_UpSampleRT);
            buffer.ReleaseTemporaryRT(_GTAO_Texture_ID);
            buffer.ReleaseTemporaryRT(_BentNormal_Texture_ID);
            buffer.SetGlobalTexture(ShaderIDs._AOROTexture, historyData.prev_Texture);
       //     buffer.Blit(historyData.prev_Texture, BuiltinRenderTextureType.CameraTarget, debugMat, 0);
        }
    }

    public class AOHistoryData : IPerCameraData
    {
        public RenderTexture prev_Texture { get; private set; }
        public AOHistoryData(int width, int height)
        {
            prev_Texture = new RenderTexture(width, height, 0, RenderTextureFormat.RGHalf, RenderTextureReadWrite.Linear);
            prev_Texture.filterMode = FilterMode.Bilinear;
            prev_Texture.Create();
        }

        public void UpdateSize(int width, int height)
        {
            if (width != prev_Texture.width || height != prev_Texture.height)
            {
                prev_Texture.Release();
                prev_Texture.width = width;
                prev_Texture.height = height;
                prev_Texture.Create();

            }
        }

        public override void DisposeProperty()
        {
            if (Application.isPlaying)
            {
                Object.Destroy(prev_Texture);
            }
            else
            {
                Object.DestroyImmediate(prev_Texture);
            }


        }
    }
}