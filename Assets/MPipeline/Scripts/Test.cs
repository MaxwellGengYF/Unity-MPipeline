using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using MPipeline;
using Unity.Jobs;
using UnityEngine.Rendering;
using Unity.Collections;
using System.IO;
using Unity.Collections.LowLevel.Unsafe;
using Random = UnityEngine.Random;
using UnityEngine.Jobs;
using UnityEngine.AddressableAssets;
namespace MPipeline
{
    public unsafe sealed class Test : MonoBehaviour
    {
        public List<Transform> targetTrans;
        public Transform remover;
        public Camera cam;
        public Transform testTrans;
        private void Update()
        {
            if (targetTrans.Count > 0)
            {
                foreach (var i in targetTrans)
                    MoveScene.current.AddTransform(i);
                targetTrans.Clear();
            }
            if(Input.GetKeyDown(KeyCode.P) && remover)
            {
                MoveScene.current.RemoveTransform(remover);
                remover = null;
            }
            if (Input.GetKeyDown(KeyCode.Space))
            {
                MoveScene.current.Move(float3(0, 20, 0));
            }
        }
        [EasyButtons.Button]
        void RunTest()
        {
            float4x4 mat = GL.GetGPUProjectionMatrix(cam.projectionMatrix, true);
            Debug.Log((Matrix4x4)mat);
            mat.c2.z = cam.nearClipPlane / (cam.farClipPlane - cam.nearClipPlane);
            mat.c3.z = (cam.nearClipPlane * cam.farClipPlane) / (cam.farClipPlane - cam.nearClipPlane);
            Debug.Log((Matrix4x4)mat);
        }
    }
}
