using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Unity.Collections;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Unity.Collections.LowLevel.Unsafe;
namespace MPipeline
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(Camera))]
    public unsafe sealed class PipelineCamera : MonoBehaviour
    {
        public void ResetMatrix()
        {
            Camera cam = GetComponent<Camera>();
            cam.orthographic = !cam.orthographic;
            cam.orthographic = !cam.orthographic;
            cam.ResetCullingMatrix();
            cam.ResetProjectionMatrix();
            cam.ResetStereoProjectionMatrices();
            cam.ResetStereoViewMatrices();
            cam.ResetWorldToCameraMatrix();
        }
        public struct IntEqual : IFunction<int, int, bool>
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool Run(ref int a, ref int b)
            {
                return a == b;
            }
        }
        public struct PtrEqual : IFunction<ulong, ulong, bool>
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool Run(ref ulong a, ref ulong b)
            {
                return a == b;
            }
        }
        [System.NonSerialized]
        public Camera cam;
        [System.NonSerialized]
        public RenderTargets targets;
        public UnityEngine.Rendering.PostProcessing.PostProcessProfile postProfile;
        public PipelineResources.CameraRenderingPath renderingPath = PipelineResources.CameraRenderingPath.GPUDeferred;
        public NativeDictionary<ulong, int, PtrEqual> allDatas;
        public bool inverseRender = false;
        public RenderTargetIdentifier cameraTarget = BuiltinRenderTextureType.CameraTarget;
        private static NativeDictionary<int, ulong, IntEqual> cameraSearchDict;
        public static NativeDictionary<int, ulong, IntEqual> CameraSearchDict
        {
            get { return cameraSearchDict; }
        }
        public float3 frustumMinPoint { get; private set; }
        public float3 frustumMaxPoint { get; private set; }
        [HideInInspector]
        public float[] layerCullDistance = new float[32];

        public void EnableThis(PipelineResources res)
        {
            if (!targets.initialized)
            {
                targets = RenderTargets.Init();
                ResetMatrix();
            }
        }
        public static void InitializeDict()
        {
            if (!cameraSearchDict.isCreated) cameraSearchDict = new NativeDictionary<int, ulong, IntEqual>(20, Allocator.Persistent, new IntEqual());
        }
        public void AddToDict()
        {
            InitializeDict();
            cameraSearchDict[gameObject.GetInstanceID()] = (ulong)MUnsafeUtility.GetManagedPtr(this);
            if (!frustumArray.isCreated) frustumArray = new NativeList<float4>(6, 6, Allocator.Persistent);
        }
        private void OnEnable()
        {
            if (!allDatas.isCreated) allDatas = new NativeDictionary<ulong, int, PtrEqual>(17, Allocator.Persistent, new PtrEqual());
            AddToDict();
            GetComponent<Camera>().layerCullDistances = layerCullDistance;
        }

        private void OnDisable()
        {
            if (cameraSearchDict.isCreated)
                cameraSearchDict.Remove(gameObject.GetInstanceID());
            frustumArray.Dispose();
        }
        private void OnDestroy()
        {
            if (allDatas.isCreated)
            {
                foreach (var i in allDatas)
                {
                    IPerCameraData data = ((IPerCameraData)MUnsafeUtility.GetHookedObject(i.value));
                    data.DisposeProperty();
                    MUnsafeUtility.RemoveHookedObject(i.value);
                }
                allDatas.Dispose();
            }
            cam = null;
            /* foreach (var i in commandBuffers.Values)
             {
                 i.Dispose();
             }
             commandBuffers = null;
         */
        }
        #region EVENTS
        //TODO
        //Can add events here

        private PerspCam perspCam = new PerspCam();
        public NativeList<float4> frustumArray;
        public void BeforeFrameRendering()
        {
            Transform camTrans = cam.transform;
            perspCam.forward = camTrans.forward;
            perspCam.up = camTrans.up;
            perspCam.right = camTrans.right;
            perspCam.position = camTrans.position;
            perspCam.nearClipPlane = cam.nearClipPlane;
            perspCam.farClipPlane = cam.farClipPlane;
            perspCam.aspect = cam.aspect;
            perspCam.fov = cam.fieldOfView;
            float3* corners = stackalloc float3[8];
            PipelineFunctions.GetFrustumCorner(ref perspCam, corners);
            frustumMinPoint = corners[0];
            frustumMaxPoint = corners[0];
            for (int i = 1; i < 8; ++i)
            {
                frustumMinPoint = min(frustumMinPoint, corners[i]);
                frustumMaxPoint = max(frustumMaxPoint, corners[i]);
            }
            PipelineFunctions.GetPerspFrustumPlanesWithCorner(ref perspCam, frustumArray.unsafePtr, corners + 4);
        }

        public void AfterFrameRendering()
        {

        }

        public void BeforeCameraRendering()
        {

        }

        public void AfterCameraRendering()
        {

        }
        #endregion
    }
}
