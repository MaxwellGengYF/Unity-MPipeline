using UnityEngine;
using System.Reflection;
using System.Collections.Generic;
using System;
namespace MPipeline
{
#if UNITY_EDITOR
    using UnityEditor;
    [CustomEditor(typeof(PipelineEvent), true)]
    public class EventEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            PipelineEvent evt = serializedObject.targetObject as PipelineEvent;
            evt.Enabled = EditorGUILayout.Toggle("Enabled", evt.Enabled);
            EditorUtility.SetDirty(evt);
            base.OnInspectorGUI();
        }
    }
#endif
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class RequireEventAttribute : Attribute
    {
        public Type[] events { get; private set; }
        public RequireEventAttribute(params Type[] allEvents)
        {
            events = allEvents;
        }
    }
    [System.Serializable]
    public unsafe abstract class PipelineEvent : ScriptableObject
    {
        [HideInInspector]
        [SerializeField]
        private bool enabled = false;
        private bool initialized = false;
        public bool Enabled
        {
            get
            {
                return enabled;
            }
            set
            {
                if (value == enabled) return;
                enabled = value;
                if (value) OnEnable();
                else OnDisable();
                if (initialized)
                {
                    if (value)
                    {
                        if (dependingEvents.isCreated)
                        {
                            foreach (var i in dependingEvents)
                            {
                                PipelineEvent evt = MUnsafeUtility.GetObject<PipelineEvent>(i.ToPointer());
                                if (!evt.enabled)
                                {
                                    enabled = false;
                                    return;
                                }
                            }
                        }
                    }
                    else
                    {
                        if (dependedEvents.isCreated)
                        {
                            foreach (var i in dependedEvents)
                            {
                                PipelineEvent evt = MUnsafeUtility.GetObject<PipelineEvent>(i.ToPointer());
                                evt.Enabled = false;
                            }
                        }
                    }
                }
            }
        }

        private NativeList<UIntPtr> dependedEvents;
        private NativeList<UIntPtr> dependingEvents;
        public void InitDependEventsList()
        {
            dependedEvents = new NativeList<UIntPtr>(10, Unity.Collections.Allocator.Persistent);
            dependingEvents = new NativeList<UIntPtr>(10, Unity.Collections.Allocator.Persistent);
        }
        public void DisposeDependEventsList()
        {
            dependedEvents.Dispose();
            dependingEvents.Dispose();
        }
        public void CheckInit(PipelineResources resources)
        {

            initialized = true;
            Init(resources);
        }
        public void InitEvent(PipelineResources resources)
        {
            if (initialized) return;
            initialized = true;
            Init(resources);
            
            if (enabled)
            {

                if (dependingEvents.isCreated)
                {
                    foreach (var i in dependingEvents)
                    {
                        PipelineEvent evt = MUnsafeUtility.GetObject<PipelineEvent>(i.ToPointer());
                        if (!evt.enabled)
                        {
                            Enabled = false;
                            return;
                        }
                    }
                }


            }
            else
            {
                if (dependedEvents.isCreated)
                {
                    foreach (var i in dependedEvents)
                    {
                        PipelineEvent evt = MUnsafeUtility.GetObject<PipelineEvent>(i.ToPointer());
                        evt.Enabled = false;
                    }
                }
            }
            if (Enabled) OnEnable();
            else OnDisable();
        }

        protected virtual void OnEnable()
        {

        }
        protected virtual void OnDisable()
        {

        }
        public void Prepare()
        {
            RequireEventAttribute requireEvt = GetType().GetCustomAttribute<RequireEventAttribute>(true);
            if (requireEvt != null)
            {
                foreach (var t in requireEvt.events)
                {
                    PipelineEvent targetevt = RenderPipeline.GetEvent(t);
                    if (targetevt != null)
                    {
                        targetevt.dependedEvents.Add(new UIntPtr(MUnsafeUtility.GetManagedPtr(this)));
                        dependingEvents.Add(new UIntPtr(MUnsafeUtility.GetManagedPtr(targetevt)));
                    }
                }
            }
        }
        public void DisposeEvent()
        {
            if (!initialized) return;
            initialized = false;
            OnDisable();
            Dispose();
        }
        protected abstract void Init(PipelineResources resources);
        protected abstract void Dispose();
        public abstract bool CheckProperty();
        public virtual void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data) { }
        public virtual void PreRenderFrame(PipelineCamera cam, ref PipelineCommandData data) { }
    }
}