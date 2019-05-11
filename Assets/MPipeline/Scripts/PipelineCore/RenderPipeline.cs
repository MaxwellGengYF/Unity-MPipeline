using UnityEngine;
using System.Collections.Generic;
using Unity.Jobs;
using System;
using UnityEngine.Rendering;
using System.Reflection;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;
namespace MPipeline
{
    public unsafe sealed class RenderPipeline : UnityEngine.Rendering.RenderPipeline
    {
        #region STATIC_AREA

        public static RenderPipeline current { get; private set; }
        public static PipelineCommandData data;
        #endregion
        private struct PtrEqual : IFunction<UIntPtr, UIntPtr, bool>
        {
            public bool Run(ref UIntPtr a, ref UIntPtr b)
            {
                return a == b;
            }
        }
        public PipelineResources resources;
        public static bool renderingEditor { get; private set; }
        public static PipelineResources.CameraRenderingPath currentPath { get; private set; }
        private static NativeDictionary<UIntPtr, int, PtrEqual> eventsGuideBook;
        private static List<Action<CommandBuffer>> bufferAfterFrame = new List<Action<CommandBuffer>>(10);
#if UNITY_EDITOR
        private struct EditorBakeCommand
        {
            public NativeList<float4x4> worldToCamera;
            public NativeList<float4x4> projection;
            public PipelineCamera pipelineCamera;
            public RenderTexture texArray;
            public CommandBuffer buffer;
        }
        private static List<EditorBakeCommand> bakeList = new List<EditorBakeCommand>();
        public static void AddRenderingMissionInEditor(
            NativeList<float4x4> worldToCameras,
            NativeList<float4x4> projections,
            PipelineCamera targetCameras,
            RenderTexture texArray,
            CommandBuffer buffer)
        {
            bakeList.Add(new EditorBakeCommand
            {
                worldToCamera = worldToCameras,
                projection = projections,
                texArray = texArray,
                pipelineCamera = targetCameras,
                buffer = buffer
            });
        }
#else
        public static void AddRenderingMissionInEditor(NativeList<float4x4> worldToCameras, NativeList<float4x4> projections, PipelineCamera targetCameras, RenderTexture texArray, RenderTexture tempTexture, CommandBuffer buffer)
        {
        //Shouldn't do anything in runtime
        }
#endif
        public static T GetEvent<T>() where T : PipelineEvent
        {
            Type type = typeof(T);
            int value;
            if (eventsGuideBook.Get(new UIntPtr(MUnsafeUtility.GetManagedPtr(type)), out value))
            {
                return current.resources.availiableEvents[value] as T;
            }
            return null;
        }

        public static PipelineEvent GetEvent(Type type)
        {
            int value;
            if (eventsGuideBook.Get(new UIntPtr(MUnsafeUtility.GetManagedPtr(type)), out value))
            {
                return current.resources.availiableEvents[value];
            }
            return null;
        }

        public static void ExecuteBufferAtFrameEnding(Action<CommandBuffer> buffer)
        {
            bufferAfterFrame.Add(buffer);
        }

        public RenderPipeline(PipelineResources resources)
        {
            eventsGuideBook = new NativeDictionary<UIntPtr, int, PtrEqual>(resources.availiableEvents.Length, Allocator.Persistent, new PtrEqual());
            resources.SetRenderingPath();
            var allEvents = resources.allEvents;
            GraphicsUtility.UpdatePlatform();
            MLight.ClearLightDict();
            this.resources = resources;
            current = this;
            data.buffer = new CommandBuffer();
            for (int i = 0; i < resources.availiableEvents.Length; ++i)
            {
                resources.availiableEvents[i].InitDependEventsList();
            }
            for (int i = 0; i < resources.availiableEvents.Length; ++i)
            {
                eventsGuideBook.Add(new UIntPtr(MUnsafeUtility.GetManagedPtr(resources.availiableEvents[i].GetType())), i);
                resources.availiableEvents[i].Prepare();
            }
            for (int i = 0; i < resources.availiableEvents.Length; ++i)
            {
                resources.availiableEvents[i].InitEvent(resources);
            }
        }

        protected override void Dispose(bool disposing)
        {
            eventsGuideBook.Dispose();
            base.Dispose(disposing);
            if (current == this)
            {
                current = null;
            }
            data.buffer.Dispose();
            var allEvents = resources.allEvents;
            for (int i = 0; i < resources.availiableEvents.Length; ++i)
            {
                resources.availiableEvents[i].DisposeEvent();
            }
            for (int i = 0; i < resources.availiableEvents.Length; ++i)
            {
                resources.availiableEvents[i].DisposeDependEventsList();
            }
            foreach (var i in PipelineCamera.allCamera)
            {
                PipelineCamera cam = MUnsafeUtility.GetObject<PipelineCamera>(i.ToPointer());
                var values = cam.allDatas.Values;
                foreach (var j in values)
                {
                    j.DisposeProperty();
                }
                cam.allDatas.Clear();
            }
        }
        protected override void Render(ScriptableRenderContext renderContext, Camera[] cameras)
        {
            bool* propertyCheckedFlags = stackalloc bool[resources.allEvents.Length];
            UnsafeUtility.MemClear(propertyCheckedFlags, resources.allEvents.Length);
            GraphicsSettings.useScriptableRenderPipelineBatching = resources.useSRPBatcher;
            SceneController.SetState();
            int tempID = Shader.PropertyToID("_TempRT");
#if UNITY_EDITOR
            foreach (var pair in bakeList)
            {
                PipelineCamera pipelineCam = pair.pipelineCamera;
                for (int i = 0; i < pair.worldToCamera.Length; ++i)
                {
                    pipelineCam.cam.worldToCameraMatrix = pair.worldToCamera[i];
                    pipelineCam.cam.projectionMatrix = pair.projection[i];
                    pipelineCam.cameraTarget = tempID;
                    data.buffer.GetTemporaryRT(tempID, pair.texArray.width, pair.texArray.height, pair.texArray.depth, FilterMode.Point, pair.texArray.format, RenderTextureReadWrite.Linear);
                    Render(pipelineCam, ref renderContext, pipelineCam.cam, propertyCheckedFlags);
                    data.buffer.CopyTexture(tempID, 0, 0, pair.texArray, i, 0);
                    data.buffer.ReleaseTemporaryRT(tempID);
                    data.ExecuteCommandBuffer();
                    renderContext.Submit();
                }
                pair.worldToCamera.Dispose();
                pair.projection.Dispose();
                renderContext.ExecuteCommandBuffer(pair.buffer);
                pair.buffer.Clear();
                renderContext.Submit();
            }
            bakeList.Clear();
#endif

            if (PipelineCamera.allCamera.isCreated)
            {

                foreach (var cam in cameras)
                {
                    PipelineCamera pipelineCam;
                    UIntPtr pipelineCamPtr;
                    if (!PipelineCamera.allCamera.Get(cam.gameObject.GetInstanceID(), out pipelineCamPtr))
                    {
#if UNITY_EDITOR
                        renderingEditor = true;
                        var pos = cam.transform.eulerAngles;
                        pos.z = 0;
                        cam.transform.eulerAngles = pos;
                        if (!PipelineCamera.allCamera.Get(Camera.main.gameObject.GetInstanceID(), out pipelineCamPtr))
                            continue;
#else
                    continue;
#endif
                    }
                    else
                    {
                        renderingEditor = false;
                    }

                    pipelineCam = MUnsafeUtility.GetObject<PipelineCamera>(pipelineCamPtr.ToPointer());
                    Render(pipelineCam, ref renderContext, cam, propertyCheckedFlags);
                    data.ExecuteCommandBuffer();
                    renderContext.Submit();
                }
                if (bufferAfterFrame.Count > 0)
                {
                    foreach (var i in bufferAfterFrame)
                    {
                        i(data.buffer);
                    }
                    data.ExecuteCommandBuffer();
                    bufferAfterFrame.Clear();
                    renderContext.Submit();
                }
            }
            else
            {
                if (bufferAfterFrame.Count > 0)
                {
                    foreach (var i in bufferAfterFrame)
                    {
                        i(data.buffer);
                    }
                    Graphics.ExecuteCommandBuffer(data.buffer);
                    bufferAfterFrame.Clear();
                }
            }
        }
        private void Render(PipelineCamera pipelineCam, ref ScriptableRenderContext context, Camera cam, bool* pipelineChecked)
        {
            PipelineResources.CameraRenderingPath path = pipelineCam.renderingPath;
            currentPath = path;
            pipelineCam.cam = cam;
            pipelineCam.EnableThis(resources);
            context.SetupCameraProperties(cam);
            //Set Global Data
            data.context = context;
            data.resources = resources;
            var allEvents = resources.allEvents;
            var collect = allEvents[(int)path];
#if UNITY_EDITOR
            //Need only check for Unity Editor's bug!
            if (!pipelineChecked[(int)path])
            {
                pipelineChecked[(int)path] = true;
                foreach (var e in collect)
                {
                    if (!e.CheckProperty())
                    {
                        e.CheckInit(resources);
                    }
                }
            }
#endif
            foreach (var e in collect)
            {
                if (e.Enabled)
                {
                    e.PreRenderFrame(pipelineCam, ref data);
                }
            }
            JobHandle.ScheduleBatchedJobs();
            foreach (var e in collect)
            {
                if (e.Enabled)
                {
                    e.FrameUpdate(pipelineCam, ref data);
                }
            }
        }
    }
}