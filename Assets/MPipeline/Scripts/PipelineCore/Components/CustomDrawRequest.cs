using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using Unity.Mathematics;
namespace MPipeline
{
    public abstract unsafe class CustomDrawRequest : MonoBehaviour
    {
        public float4x4 localToWorldMatrix;
        public float3 boundingBoxPosition = Vector3.zero;
        public float3 boundingBoxExtents = new Vector3(0.5f, 0.5f, 0.5f);
        private int index, gbufferIndex, shadowIndex, mvIndex, transIndex;
        public struct ComponentData
        {
            public float4x4 localToWorldMatrix;
            public float3 boundingBoxPosition;
            public float3 boundingBoxExtents;
            public int index, gbufferIndex, shadowIndex, mvIndex, transIndex;
        }
        private static int AddToList(List<CustomDrawRequest> targetLst, CustomDrawRequest ths)
        {
            int index = targetLst.Count;
            targetLst.Add(ths);
            return index;
        }

        private static int AddToList(NativeList_ulong targetLst, ulong targetInd)
        {
            int index = targetLst.Length;
            targetLst.Add(targetInd);
            return index;
        }

        private static CustomDrawRequest RemoveFromList(List<CustomDrawRequest> targetLst, int targetIndex)
        {
            targetLst[targetIndex] = targetLst[targetLst.Count - 1];
            CustomDrawRequest cdr = targetLst[targetIndex];
            targetLst.RemoveAt(targetLst.Count - 1);
            return cdr;
        }

        private static ulong RemoveFromList(NativeList_ulong targetLst, int targetIndex)
        {
            targetLst[targetIndex] = targetLst[targetLst.Length - 1];
            ulong cdr = targetLst[targetIndex];
            targetLst.RemoveLast();
            return cdr;
        }
        protected virtual void OnEnableFunc() { }
        protected virtual void OnDisableFunc() { }
        public virtual void PrepareJob(PipelineResources resources) { }
        public virtual void FinishJob() { }
        protected abstract void DrawCommand(out bool drawGBuffer, out bool drawShadow, out bool drawTransparent);
        public virtual void DrawDepthPrepass(CommandBuffer buffer) { }
        public virtual void DrawGBuffer(CommandBuffer buffer) { }
        public virtual void DrawShadow(CommandBuffer buffer) { }
        public virtual void DrawTransparent(CommandBuffer buffer) { }
        public static NativeList_ulong drawGBufferList { get; private set; }
        public static NativeList_ulong drawShadowList { get; private set; }
        public static NativeList_ulong drawTransparentList { get; private set; }
        public static List<CustomDrawRequest> allEvents { get; private set; }


        private bool drawGBuffer, drawShadow, drawTransparent;
        private static bool initialized = false;
        public static void Initialize()
        {
            if (initialized) return;
            initialized = true;
            drawGBufferList = new NativeList_ulong(30, Unity.Collections.Allocator.Persistent);
            drawShadowList = new NativeList_ulong(30, Unity.Collections.Allocator.Persistent);
            drawTransparentList = new NativeList_ulong(30, Unity.Collections.Allocator.Persistent);
            allEvents = new List<CustomDrawRequest>(30);
        }
        private void OnEnable()
        {
            Initialize();
            DrawCommand(out drawGBuffer, out drawShadow, out drawTransparent);
            index = AddToList(allEvents, this);
            if (drawGBuffer) gbufferIndex = AddToList(drawGBufferList, (ulong)localToWorldMatrix.Ptr());
            if (drawShadow) shadowIndex = AddToList(drawShadowList, (ulong)localToWorldMatrix.Ptr());
            if (drawTransparent) transIndex = AddToList(drawTransparentList, (ulong)localToWorldMatrix.Ptr());
            OnEnableFunc();
        }
        public static void Dispose()
        {
            drawGBufferList.Dispose();
            drawShadowList.Dispose();
            drawTransparentList.Dispose();
            allEvents = null;
            initialized = false;
        }
        private void OnDisable()
        {
            int offset = sizeof(float4x4) + sizeof(float3) + sizeof(float3);
            if (initialized)
            {
                if (drawGBuffer)
                {
                    var a = (ComponentData*)RemoveFromList(drawGBufferList, gbufferIndex);
                    a->gbufferIndex = gbufferIndex;
                }
                if (drawShadow)
                {
                    var a = (ComponentData*)RemoveFromList(drawShadowList, shadowIndex);
                    a->shadowIndex = shadowIndex;
                }
                if (drawTransparent)
                {
                    var a = (ComponentData*)RemoveFromList(drawTransparentList, transIndex);
                    a->transIndex = transIndex;
                }
                var b = RemoveFromList(allEvents, index);
                b.index = index;
            }
        }
    }
}