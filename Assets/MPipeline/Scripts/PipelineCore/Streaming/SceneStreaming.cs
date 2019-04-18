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
        private NativeArray<int> indicesBuffer;
        private NativeArray<CullBox> clusterBuffer;
        private NativeArray<Point> pointsBuffer;
        private NativeArray<Vector2Int> results;
       
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
        public SceneStreaming(ClusterProperty property)
        {
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
            clusterBuffer = new NativeArray<CullBox>(property.clusterCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            pointsBuffer = new NativeArray<Point>(property.clusterCount * PipelineBaseBuffer.CLUSTERCLIPCOUNT, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            indicesBuffer = new NativeArray<int>(property.clusterCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            NativeList<ulong> pointerContainer = SceneController.pointerContainer;
            pointerContainer.AddCapacityTo(pointerContainer.Length + indicesBuffer.Length);
            CullBox* clusterData = clusterBuffer.Ptr();
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
            int* indicesPtr = indicesBuffer.Ptr();
            LoadingCommandQueue commandQueue = LoadingThread.commandQueue;
            for (int i = 0; i < indicesBuffer.Length; ++i)
            {
                indicesPtr[i] = pointerContainer.Length;
                pointerContainer.Add((ulong)(indicesPtr + i));
            }

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
                results = new NativeArray<Vector2Int>(indicesBuffer.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                resultLength = 0;
                LoadingThread.AddCommand(deleteAsyncFunc, this);
            }
        }

        public void DeleteInEditor()
        {
            results = new NativeArray<Vector2Int>(indicesBuffer.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
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
            ref NativeList<ulong> pointerContainer = ref SceneController.pointerContainer;
            int targetListLength = pointerContainer.Length - indicesBuffer.Length;
            int* indicesPtr = indicesBuffer.Ptr();
            int currentIndex = pointerContainer.Length - 1;
            for (int i = 0; i < indicesBuffer.Length; ++i)
            {
                if (indicesPtr[i] >= targetListLength)
                {
                    indicesPtr[i] = -1;
                    pointerContainer[indicesPtr[i]] = 0;
                }
            }

            for (int i = 0; i < indicesBuffer.Length; ++i)
            {
                int index = indicesPtr[i];
                if (index >= 0)
                {
                    while (pointerContainer[currentIndex] == 0)
                    {
                        currentIndex--;
                        if (currentIndex < 0)
                            goto FINALIZE;
                    }

                    Vector2Int value = new Vector2Int(index, currentIndex);
                    currentIndex--;
                    results[resultLength] = value;
                    pointerContainer[value.x] = pointerContainer[value.y];
                    *(int*)pointerContainer[value.x] = value.x;
                    resultLength += 1;
                }
            }
            FINALIZE:
            pointerContainer.RemoveLast(indicesBuffer.Length);
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
            ComputeBuffer indexBuffer = SceneController.GetTempPropertyBuffer(results.Length, 8);//sizeof(Vector2Int)
            int currentCount = 0;
            int targetCount;
            while ((targetCount = currentCount + MAXIMUMINTCOUNT) < resultLength)
            {
                indexBuffer.SetData(results, currentCount, currentCount, MAXIMUMINTCOUNT);
                currentCount = targetCount;
                yield return null;
            }
            if (resultLength > 0)
            {
                indexBuffer.SetData(results, currentCount, currentCount, resultLength - currentCount);
                ComputeShader shader = resources.shaders.streamingShader;
                shader.SetBuffer(0, ShaderIDs.clusterBuffer, baseBuffer.clusterBuffer);
                shader.SetBuffer(1, ShaderIDs.verticesBuffer, baseBuffer.verticesBuffer);
                shader.SetBuffer(0, ShaderIDs._IndexBuffer, indexBuffer);
                shader.SetBuffer(1, ShaderIDs._IndexBuffer, indexBuffer);
                ComputeShaderUtility.Dispatch(shader, 0, resultLength, 64);
                shader.Dispatch(1, resultLength, 1, 1);
            }
            baseBuffer.clusterCount -= indicesBuffer.Length;
            results.Dispose();
            indicesBuffer.Dispose();
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