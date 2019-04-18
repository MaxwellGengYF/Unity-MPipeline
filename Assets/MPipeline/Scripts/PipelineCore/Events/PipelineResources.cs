using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using System;
namespace MPipeline
{

    public unsafe sealed class PipelineResources : RenderPipelineAsset
    {
        public bool useSRPBatcher = true;
        protected override UnityEngine.Rendering.RenderPipeline CreatePipeline()
        {
            return new RenderPipeline(this);
        }
        public enum CameraRenderingPath
        {
            GPUDeferred, Bake
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
        public void SetRenderingPath()
        {
            List<Pair<int, Type[]>> allCollection = AllEvents.GetAllPath();
            int maximum = -1;
            foreach(var i in allCollection)
            {
                if (i.key > maximum)
                    maximum = i.key;
            }
            allEvents = new PipelineEvent[maximum + 1][];
            Dictionary<Type, PipelineEvent> evtDict = new Dictionary<Type, PipelineEvent>(availiableEvents.Length);
            foreach(var i in availiableEvents)
            {
                evtDict.Add(i.GetType(), i);
            }
            foreach(var i in allCollection)
            {
                allEvents[i.key] = GetAllEvents(i.value, evtDict);
            }
        }
    }
}