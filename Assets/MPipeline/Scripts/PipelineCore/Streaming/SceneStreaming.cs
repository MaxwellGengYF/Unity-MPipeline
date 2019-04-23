using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using System.IO;
namespace MPipeline
{
    public unsafe sealed class SceneStreaming
    {
        public static bool loading = false;
        public enum State
        {
            Unloaded, Loaded, Loading
        }
        public State state;
        private NativeArray<Cluster> clusterBuffer;
        private NativeArray<Point> pointsBuffer;
       
        private int resultLength;
        private static Action<object> generateAsyncFunc = (obj) =>
        {
            SceneStreaming str = obj as SceneStreaming;
            str.GenerateAsync();
        };
        private static Action<object> deleteAsyncFunc = (obj) =>
        {
            SceneStreaming str = obj as SceneStreaming;
            str.DeleteAsync();
        };
        ClusterProperty property;
        int propertyCount;
        public SceneStreaming(ClusterProperty property, int propertyCount)
        {
            this.propertyCount = propertyCount;
            state = State.Unloaded;
            this.property = property;
        }
        static string[] allStrings = new string[3];
        private static byte[] bytesArray = new byte[8192];
        private static byte[] GetByteArray(int length)
        {
            if (bytesArray == null || bytesArray.Length < length)
            {
                bytesArray = new byte[length];
            }
            return bytesArray;
        }
        public void GenerateAsync(bool listCommand = true)
        {
            clusterBuffer = new NativeArray<Cluster>(property.clusterCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            pointsBuffer = new NativeArray<Point>(property.clusterCount * PipelineBaseBuffer.CLUSTERCLIPCOUNT, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            Cluster* clusterData = clusterBuffer.Ptr();
            Point* verticesData = pointsBuffer.Ptr();
            const string infosPath = "Assets/BinaryData/MapInfos/";
            const string pointsPath = "Assets/BinaryData/MapPoints/";
            MStringBuilder sb = new MStringBuilder(pointsPath.Length + property.name.Length + ".mpipe".Length);
            allStrings[0] = infosPath;
            allStrings[1] = property.name;
            allStrings[2] = ".mpipe";
            sb.Combine(allStrings);
            // FileStream fileStream = new FileStream(sb.str, FileMode.Open, FileAccess.Read);
            using (FileStream reader = new FileStream(sb.str, FileMode.Open, FileAccess.Read))
            {
                int length = (int)reader.Length;
                byte[] bytes = GetByteArray(length);
                reader.Read(bytes, 0, length);
                fixed (byte* b = bytes)
                {
                    UnsafeUtility.MemCpy(clusterData, b, length);
                }
            }
            allStrings[0] = pointsPath;
            sb.Combine(allStrings);
            using (FileStream reader = new FileStream(sb.str, FileMode.Open, FileAccess.Read))
            {
                int length = (int)reader.Length;
                byte[] bytes = GetByteArray(length);
                reader.Read(bytes, 0, length);
                fixed (byte* b = bytes)
                {
                    UnsafeUtility.MemCpy(verticesData, b, length);
                }
            }
            LoadingCommandQueue commandQueue = LoadingThread.commandQueue;

            if (listCommand)
            {
                lock (commandQueue)
                {
                    commandQueue.Queue(GenerateRun());
                }
            }
        }
        static readonly int PROPERTYVALUESIZE = sizeof(PropertyValue);
        public IEnumerator Generate()
        {
            if (state == State.Unloaded)
            {
                state = State.Loading;
                while (loading)
                {
                    yield return null;
                }
                loading = true;
                LoadingThread.AddCommand(generateAsyncFunc, this);
            }
        }

        public IEnumerator Delete()
        {
            if (state == State.Loaded)
            {
                state = State.Loading;
                while (loading)
                {
                    yield return null;
                }
                loading = true;
                resultLength = 0;
                LoadingThread.AddCommand(deleteAsyncFunc, this);
            }
        }

        public void DeleteInEditor()
        {
            resultLength = 0;
            DeleteAsync(false);
            IEnumerator syncFunc = DeleteRun();
            while (syncFunc.MoveNext()) ;
        }

        public void GenerateInEditor()
        {
            GenerateAsync(false);
            IEnumerator syncFunc = GenerateRun();
            while (syncFunc.MoveNext()) ;
        }

        public void DeleteAsync(bool listCommand = true)
        {
            LoadingCommandQueue commandQueue = LoadingThread.commandQueue;
            if (listCommand)
            {
                lock (commandQueue)
                {
                    commandQueue.Queue(DeleteRun());
                }
            }
        }

        #region MainThreadCommand
        private const int MAXIMUMINTCOUNT = 5000;
        private const int MAXIMUMVERTCOUNT = 100;

        private IEnumerator DeleteRun()
        {
            PipelineResources resources = RenderPipeline.current.resources;
            PipelineBaseBuffer baseBuffer = SceneController.baseBuffer;
            ComputeShader shader = resources.shaders.streamingShader;
            NativeArray<int> indirectArgs = new NativeArray<int>(5, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            int result = baseBuffer.clusterCount - property.clusterCount;
            indirectArgs[0] = 0;
            indirectArgs[1] = 1;
            indirectArgs[2] = 1;
            indirectArgs[3] = result;
            indirectArgs[4] = propertyCount;
            baseBuffer.moveCountBuffer.SetData(indirectArgs);
            ComputeBuffer indexBuffer = SceneController.GetTempPropertyBuffer(property.clusterCount, 8);
            indirectArgs.Dispose();
            shader.SetBuffer(0, ShaderIDs.instanceCountBuffer, baseBuffer.moveCountBuffer);
            shader.SetBuffer(0, ShaderIDs.clusterBuffer, baseBuffer.clusterBuffer);
            shader.SetBuffer(0, ShaderIDs._IndexBuffer, indexBuffer);
            shader.SetBuffer(1, ShaderIDs._IndexBuffer, indexBuffer);
            shader.SetBuffer(1, ShaderIDs.verticesBuffer, baseBuffer.verticesBuffer);
            if (result > 0)
            {
                ComputeShaderUtility.Dispatch(shader, 0, result);
                yield return null;
                shader.DispatchIndirect(1, baseBuffer.moveCountBuffer);
            }
            baseBuffer.clusterCount = result;
            loading = false;
            state = State.Unloaded;
        }

        private IEnumerator GenerateRun()
        {
            PipelineResources resources = RenderPipeline.current.resources;
            PipelineBaseBuffer baseBuffer = SceneController.baseBuffer;
            int targetCount;
            int currentCount = 0;
            while ((targetCount = currentCount + MAXIMUMVERTCOUNT) < clusterBuffer.Length)
            {
                baseBuffer.clusterBuffer.SetData(clusterBuffer, currentCount, currentCount + baseBuffer.clusterCount, MAXIMUMVERTCOUNT);
                baseBuffer.verticesBuffer.SetData(pointsBuffer, currentCount * PipelineBaseBuffer.CLUSTERCLIPCOUNT, (currentCount + baseBuffer.clusterCount) * PipelineBaseBuffer.CLUSTERCLIPCOUNT, MAXIMUMVERTCOUNT * PipelineBaseBuffer.CLUSTERCLIPCOUNT);
                currentCount = targetCount;
                yield return null;
            }
            //TODO
            baseBuffer.clusterBuffer.SetData(clusterBuffer, currentCount, currentCount + baseBuffer.clusterCount, clusterBuffer.Length - currentCount);
            baseBuffer.verticesBuffer.SetData(pointsBuffer, currentCount * PipelineBaseBuffer.CLUSTERCLIPCOUNT, (currentCount + baseBuffer.clusterCount) * PipelineBaseBuffer.CLUSTERCLIPCOUNT, (clusterBuffer.Length - currentCount) * PipelineBaseBuffer.CLUSTERCLIPCOUNT);
            int clusterCount = clusterBuffer.Length;
            clusterBuffer.Dispose();
            pointsBuffer.Dispose();
            loading = false;
            state = State.Loaded;
            baseBuffer.clusterCount += clusterCount;
            Debug.Log("Loaded");
        }
        #endregion
    }
}