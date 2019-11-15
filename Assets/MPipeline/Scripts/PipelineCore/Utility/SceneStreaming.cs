using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine.Rendering;
using System.IO;
namespace MPipeline
{
    public unsafe sealed class SceneStreaming : MonoBehaviour
    {
        public static bool loading = true;
        public string fileName;
        public enum State
        {
            Unloaded, Loaded, Loading
        }

        public State state { get; private set; }
        private NativeArray<VirtualMaterial.MaterialProperties> materialProperties;
        private NativeArray<int> materialIndexBuffer;
        private NativeList<bool> textureLoadingFlags;
        public int clusterCount => loader.clusterCount;
        private static Action<object> generateAsyncFunc = (obj) =>
        {
            SceneStreaming str = obj as SceneStreaming;
            str.GenerateAsync();
        };

        public int propertyCount { get; private set; }
        static int propertyStaticCount = int.MinValue;
        private static MStringBuilder sb;
        private float3 originPos;
        public void Awake()
        {
            if (!sb.isCreated)
            {
                sb = new MStringBuilder(100);
            }
            state = State.Unloaded;
            propertyCount = propertyStaticCount;
            propertyStaticCount++;
            originPos = transform.position;
            textureLoadingFlags = new NativeList<bool>(50, Allocator.Persistent);
        }
        private SceneStreamLoader loader;
        static string[] allStrings = new string[3];
        public static byte[] bytesArray = new byte[8192];
        public static byte[] GetByteArray(int length)
        {
            if (bytesArray == null || bytesArray.Length < length)
            {
                bytesArray = new byte[length];
            }
            return bytesArray;
        }
        /*
        private void Update()
        {
            if(state == State.Loaded)
            {
                SceneController.MoveScene(propertyCount, (float3)transform.position - originPos, loader.clusterCount);
                originPos = transform.position;
            }
        }
        */
        public void GenerateAsync(bool listCommand = true)
        {
            var resources = ClusterMatResources.current;
            allStrings[0] = ClusterMatResources.infosPath;
            allStrings[1] = fileName;
            allStrings[2] = ".mpipe";
            sb.Combine(allStrings);
            loader.fsm = new FileStream(sb.str, FileMode.Open, FileAccess.Read);
            loader.LoadAll();
            materialIndexBuffer = resources.vmManager.SetMaterials(loader.allProperties.Length);
            for (int i = 0; i < loader.cluster.Length; ++i)
            {
                loader.cluster[i].index = propertyCount;
            }
            materialProperties = new NativeArray<VirtualMaterial.MaterialProperties>(loader.allProperties.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            VirtualMaterial.MaterialProperties* propertiesPtr = materialProperties.Ptr();
            textureLoadingFlags.Clear();
            //Update Material
            for (int i = 0; i < materialProperties.Length; ++i)
            {
                ref var currProp = ref propertiesPtr[i];
                currProp = loader.allProperties[i];
                if (currProp._MainTex >= 0)
                {
                    currProp._MainTex = resources.rgbaPool.GetTex(loader.albedoGUIDs[currProp._MainTex], ref textureLoadingFlags);
                }
                if (currProp._SecondaryMainTex >= 0)
                {
                    currProp._SecondaryMainTex = resources.rgbaPool.GetTex(loader.secondAlbedoGUIDs[currProp._SecondaryMainTex], ref textureLoadingFlags);
                }
                if (currProp._BumpMap >= 0)
                {
                    currProp._BumpMap = resources.rgbaPool.GetTex(loader.normalGUIDs[currProp._BumpMap], ref textureLoadingFlags, true);
                }
                if (currProp._SecondaryBumpMap >= 0)
                {
                    currProp._SecondaryBumpMap = resources.rgbaPool.GetTex(loader.secondNormalGUIDs[currProp._SecondaryBumpMap], ref textureLoadingFlags, true);
                }

                if (currProp._SpecularMap >= 0)
                {
                    currProp._SpecularMap = resources.rgbaPool.GetTex(loader.smoGUIDs[currProp._SpecularMap], ref textureLoadingFlags);
                }
                if (currProp._SecondarySpecularMap >= 0)
                {
                    currProp._SecondarySpecularMap = resources.rgbaPool.GetTex(loader.secondSpecGUIDs[currProp._SecondarySpecularMap], ref textureLoadingFlags);
                }
                if (currProp._EmissionMap >= 0)
                {
                    currProp._EmissionMap = resources.emissionPool.GetTex(loader.emissionGUIDs[currProp._EmissionMap], ref textureLoadingFlags);
                }
                if (currProp._HeightMap >= 0)
                {
                    currProp._HeightMap = resources.heightPool.GetTex(loader.heightGUIDs[currProp._HeightMap], ref textureLoadingFlags);
                }
            }

            for (int i = 0; i < loader.triangleMats.Length; ++i)
            {
                loader.triangleMats[i] = materialIndexBuffer[loader.triangleMats[i]];
            }
            //Transform Points in runtime
            LoadingCommandQueue commandQueue = LoadingThread.commandQueue;
            if (listCommand)
            {
                lock (commandQueue)
                {
                    commandQueue.Queue(GenerateRun());
                }
            }
        }
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

        public void OnDestroy()
        {
            if (state != State.Unloaded)
            {
                Debug.LogError("Scene: \"" + fileName + "\" is still running! That will cause a leak!");
                loader.Dispose();
                if (materialProperties.IsCreated) materialProperties.Dispose();
                if (materialIndexBuffer.IsCreated) materialIndexBuffer.Dispose();
            }
            textureLoadingFlags.Dispose();
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
                DeleteRun();
                loading = false;
            }
        }

        #region MainThreadCommand
        private const int MAXIMUMINTCOUNT = 5000;
        private const int MAXIMUMVERTCOUNT = 100;

        public void DeleteRun()
        {
            var clusterResources = ClusterMatResources.current;
            PipelineResources resources = RenderPipeline.current.resources;
            PipelineBaseBuffer baseBuffer = SceneController.baseBuffer;
            int result = baseBuffer.clusterCount - loader.clusterCount;
            ComputeShader shader = resources.shaders.streamingShader;

            if (result > 0)
            {
                NativeArray<int> indirectArgs = new NativeArray<int>(5, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                indirectArgs[0] = 0;
                indirectArgs[1] = 1;
                indirectArgs[2] = 1;
                indirectArgs[3] = result;
                indirectArgs[4] = propertyCount;
                baseBuffer.moveCountBuffer.SetData(indirectArgs);
                ComputeBuffer indexBuffer = SceneController.GetTempPropertyBuffer(loader.clusterCount, 8);
                CommandBuffer buffer = RenderPipeline.BeforeFrameBuffer;
                indirectArgs.Dispose();
                buffer.SetComputeBufferParam(shader, 0, ShaderIDs.instanceCountBuffer, baseBuffer.moveCountBuffer);
                buffer.SetComputeBufferParam(shader, 0, ShaderIDs.clusterBuffer, baseBuffer.clusterBuffer);
                buffer.SetComputeBufferParam(shader, 0, ShaderIDs._IndexBuffer, indexBuffer);
                buffer.SetComputeBufferParam(shader, 1, ShaderIDs._IndexBuffer, indexBuffer);
                buffer.SetComputeBufferParam(shader, 3, ShaderIDs._IndexBuffer, indexBuffer);
                buffer.SetComputeBufferParam(shader, 1, ShaderIDs.verticesBuffer, baseBuffer.verticesBuffer);
                buffer.SetComputeBufferParam(shader, 3, ShaderIDs._TriangleMaterialBuffer, baseBuffer.triangleMaterialBuffer);
                ComputeShaderUtility.Dispatch(shader, buffer, 0, result);
                buffer.DispatchCompute(shader, 1, baseBuffer.moveCountBuffer, 0);
                buffer.DispatchCompute(shader, 3, baseBuffer.moveCountBuffer, 0);
            }
            clusterResources.vmManager.UnloadMaterials(materialIndexBuffer);
            foreach (var i in loader.albedoGUIDs)
            {
                clusterResources.rgbaPool.RemoveTex(i);
            }
            foreach (var i in loader.normalGUIDs)
            {
                clusterResources.rgbaPool.RemoveTex(i);
            }
            foreach (var i in loader.emissionGUIDs)
            {
                clusterResources.emissionPool.RemoveTex(i);
            }
            foreach (var i in loader.smoGUIDs)
            {
                clusterResources.rgbaPool.RemoveTex(i);
            }
            foreach (var i in loader.heightGUIDs)
            {
                clusterResources.heightPool.RemoveTex(i);
            }
            foreach (var i in loader.secondAlbedoGUIDs)
            {
                clusterResources.rgbaPool.RemoveTex(i);
            }
            foreach (var i in loader.secondNormalGUIDs)
            {
                clusterResources.rgbaPool.RemoveTex(i);
            }
            foreach (var i in loader.secondSpecGUIDs)
            {
                clusterResources.rgbaPool.RemoveTex(i);
            }
            baseBuffer.clusterCount = result;

            state = State.Unloaded;
            loader.Dispose();
        }
        private static void SetCustomData<T>(ComputeBuffer cb, NativeList<T> arr, int managed, int compute, int count) where T : unmanaged
        {
            cb.SetDataPtr(arr.unsafePtr + managed, compute, count);
        }
        private IEnumerator GenerateRun()
        {
            var clusterResources = ClusterMatResources.current;
            PipelineResources resources = RenderPipeline.current.resources;
            PipelineBaseBuffer baseBuffer = SceneController.baseBuffer;
            int targetCount;
            int currentCount = 0;
            while ((targetCount = currentCount + MAXIMUMVERTCOUNT) < loader.clusterCount)
            {
                SetCustomData(baseBuffer.clusterBuffer, loader.cluster, currentCount, currentCount + baseBuffer.clusterCount, MAXIMUMVERTCOUNT);
                SetCustomData(baseBuffer.verticesBuffer, loader.points, currentCount * PipelineBaseBuffer.CLUSTERCLIPCOUNT, (currentCount + baseBuffer.clusterCount) * PipelineBaseBuffer.CLUSTERCLIPCOUNT, MAXIMUMVERTCOUNT * PipelineBaseBuffer.CLUSTERCLIPCOUNT);
                SetCustomData(baseBuffer.triangleMaterialBuffer, loader.triangleMats, currentCount * PipelineBaseBuffer.CLUSTERTRIANGLECOUNT, (currentCount + baseBuffer.clusterCount) * PipelineBaseBuffer.CLUSTERTRIANGLECOUNT, MAXIMUMVERTCOUNT * PipelineBaseBuffer.CLUSTERTRIANGLECOUNT);
                currentCount = targetCount;
                yield return null;
            }
            //TODO
            SetCustomData(baseBuffer.clusterBuffer, loader.cluster, currentCount, currentCount + baseBuffer.clusterCount, loader.clusterCount - currentCount);
            SetCustomData(baseBuffer.verticesBuffer, loader.points, currentCount * PipelineBaseBuffer.CLUSTERCLIPCOUNT, (currentCount + baseBuffer.clusterCount) * PipelineBaseBuffer.CLUSTERCLIPCOUNT, (loader.clusterCount - currentCount) * PipelineBaseBuffer.CLUSTERCLIPCOUNT);
            SetCustomData(baseBuffer.triangleMaterialBuffer, loader.triangleMats, currentCount * PipelineBaseBuffer.CLUSTERTRIANGLECOUNT, (currentCount + baseBuffer.clusterCount) * PipelineBaseBuffer.CLUSTERTRIANGLECOUNT, (loader.clusterCount - currentCount) * PipelineBaseBuffer.CLUSTERTRIANGLECOUNT);
            loading = false;
            state = State.Loaded;
            baseBuffer.clusterCount += loader.clusterCount;
            yield return null;
            loader.cluster.Dispose();
            loader.points.Dispose();
            loader.triangleMats.Dispose();
            clusterResources.vmManager.UpdateMaterialToGPU(materialProperties, materialIndexBuffer);
            materialProperties.Dispose();
            for (int i = 0; i < textureLoadingFlags.Length; ++i)
            {
                while (!textureLoadingFlags[i])
                {
                    yield return null;
                }
            }
            Debug.Log("Loaded");
        }
        #endregion
    }
}