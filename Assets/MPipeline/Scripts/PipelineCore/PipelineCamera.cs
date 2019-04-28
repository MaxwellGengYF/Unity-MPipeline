using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Unity.Collections;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Unity.Collections.LowLevel.Unsafe;
namespace MPipeline
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(Camera))]
    public unsafe sealed class PipelineCamera : MonoBehaviour
    {
        public struct IntEqual : IFunction<int, int ,bool>
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool Run(ref int a, ref int b)
            {
                return a == b;
            }
        }
        [System.NonSerialized]
        public Camera cam;
        [System.NonSerialized]
        public RenderTargets targets;
        public PipelineResources.CameraRenderingPath renderingPath = PipelineResources.CameraRenderingPath.GPUDeferred;
        public Dictionary<Type, IPerCameraData> allDatas = new Dictionary<Type, IPerCameraData>(17);
        public bool inverseRender = false;
        public RenderTargetIdentifier cameraTarget = BuiltinRenderTextureType.CameraTarget;
        public static NativeDictionary<int, UIntPtr, IntEqual> allCamera;
        [HideInInspector]
        public float[] layerCullDistance = new float[32];

        public void EnableThis(PipelineResources res)
        {
            if (!targets.initialized)
            {
                targets = RenderTargets.Init();
            }
        }

        private void OnEnable()
        {
            if (!allCamera.isCreated)
            {
                allCamera = new NativeDictionary<int, UIntPtr, IntEqual>(17, Allocator.Persistent, new IntEqual());
            }
            allCamera.Add(gameObject.GetInstanceID(), new UIntPtr(MUnsafeUtility.GetManagedPtr(this)));
            GetComponent<Camera>().layerCullDistances = layerCullDistance;
        }

        private void OnDisable()
        {
            allCamera.Remove(gameObject.GetInstanceID());
            if (allCamera.Length <= 0)
            {
                allCamera.Dispose();
            }
        }
        private void OnDestroy()
        {
            foreach (var i in allDatas.Values)
                i.DisposeProperty();
            allDatas.Clear();
            cam = null;
        }
    }
}
