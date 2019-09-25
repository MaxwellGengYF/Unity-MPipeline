#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MPipeline.PCG
{
    [ExecuteInEditMode]
    public class PCGMain : CustomDrawRequest
    {
        public PCGResources pcgRes;
        public static PCGMain current;
        private void Awake()
        {
            if (current && current != this)
            {
                Debug.LogError("PCG Main have to be singleton!");
                DestroyImmediate(gameObject);
            }
            current = this;
        }
        protected override void OnEnableFunc()
        {
            Awake();
            if (!pcgRes) enabled = false;
            PCGNodeBase.initRes = pcgRes;
            localToWorldMatrix = Matrix4x4.identity;
            boundingBoxExtents = new Unity.Mathematics.float3(float.MaxValue, float.MaxValue, float.MaxValue);
        }
        protected override void OnDisableFunc()
        {

            if (!pcgRes) return;
            PCGNodeBase.initRes = null;
        }

        private void OnDestroy()
        {
            if (current == this)
            {
                current = null;
            }
        }


        protected override void DrawCommand(out bool drawGBuffer, out bool drawShadow, out bool drawTransparent)
        {
            drawGBuffer = true;
            drawShadow = false;
            drawTransparent = false;
        }
        public override void DrawDepthPrepass(CommandBuffer buffer)
        {
            foreach (var i in PCGNodeBase.allNodeBases)
            {
                i.DrawDepthPrepass(buffer);
            }
        }
        public override void DrawGBuffer(CommandBuffer buffer)
        {
            foreach (var i in PCGNodeBase.allNodeBases)
            {
                i.DrawGBuffer(buffer);
            }
        }
    }
}
#endif