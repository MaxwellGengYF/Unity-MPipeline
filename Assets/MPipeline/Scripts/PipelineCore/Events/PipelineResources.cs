using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using System;
using System.Reflection;
using Unity.Collections;
namespace MPipeline
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class RenderingPathAttribute : Attribute
    {
        public PipelineResources.CameraRenderingPath path { get; private set; }
        public RenderingPathAttribute(PipelineResources.CameraRenderingPath path)
        {
            this.path = path;
        }
    }
    public unsafe sealed class PipelineResources : RenderPipelineAsset
    {
        public bool useSRPBatcher = true;
        protected override UnityEngine.Rendering.RenderPipeline CreatePipeline()
        {
            return new RenderPipeline(this);
        }
        public enum CameraRenderingPath
        {
            GPUDeferred, Bake, Unlit
        }
        public PipelineEvent[] availiableEvents;
        public PipelineShaders shaders = new PipelineShaders();
        public PipelineEvent[][] allEvents { get; private set; }
        public static PipelineEvent[] GetAllEvents(Type[] types, Dictionary<Type, PipelineEvent> dict)
        {
            PipelineEvent[] events = new PipelineEvent[types.Length];
            for (int i = 0; i < events.Length; ++i)
            {
                events[i] = dict[types[i]];
            }
            return events;
        }
        private static NativeArray<UIntPtr> GetAllPath()
        {
            NativeList<UIntPtr> pool = new NativeList<UIntPtr>(10, Allocator.Temp);
            NativeList<int> typePool = new NativeList<int>(10, Allocator.Temp);
            FieldInfo[] allInfos = typeof(AllEvents).GetFields();
            foreach (var i in allInfos)
            {
                RenderingPathAttribute but = i.GetCustomAttribute(typeof(RenderingPathAttribute)) as RenderingPathAttribute;
                if (but != null && i.FieldType == typeof(Type[]))
                {
                    pool.Add(new UIntPtr(MUnsafeUtility.GetManagedPtr(i)));
                    typePool.Add((int)but.path);
                }
            }
            NativeArray<UIntPtr> final = new NativeArray<UIntPtr>(pool.Length, Allocator.Temp, NativeArrayOptions.ClearMemory);
            for (int i = 0; i < pool.Length; ++i)
            {
                final[typePool[i]] = pool[i];
            }
            return final;
        }
        public void SetRenderingPath()
        {
            NativeArray<UIntPtr> allCollection = GetAllPath();
            allEvents = new PipelineEvent[allCollection.Length][];
            Dictionary<Type, PipelineEvent> evtDict = new Dictionary<Type, PipelineEvent>(availiableEvents.Length);
            foreach(var i in availiableEvents)
            {
                evtDict.Add(i.GetType(), i);
            }
            for(int i = 0; i < allCollection.Length; ++i)
            {
                FieldInfo tp = MUnsafeUtility.GetObject<FieldInfo>(allCollection[i].ToPointer());
                Type[] tt = tp.GetValue(null) as Type[];
                allEvents[i] = GetAllEvents(tt, evtDict);
            }

        }
    }
}