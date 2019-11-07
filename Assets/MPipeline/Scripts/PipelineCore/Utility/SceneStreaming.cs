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
    [Serializable]
    public unsafe sealed class SceneStreaming
    {
        public static bool loading = false;
        public string name;
        public int clusterCount;
        public enum State
        {
            Unloaded, Loaded, Loading
        }
        [NonSerialized]
        public State state;
        private NativeArray<VirtualMaterial.MaterialProperties> materialProperties;
        private NativeArray<int> materialIndexBuffer;
        private ClusterMatResources resources;
        private static Action<object> generateAsyncFunc = (obj) =>
        {
            SceneStreaming str = obj as SceneStreaming;
            str.GenerateAsync();
        };

        int propertyCount;
        private MStringBuilder sb;
        public void Init(int propertyCount, MStringBuilder msb, ClusterMatResources resources)
        {
            this.resources = resources;
            sb = msb;
            this.propertyCount = propertyCount;
            state = State.Unloaded;

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


        public void GenerateAsync(bool listCommand = true)
        {
            allStrings[0] = ClusterMatResources.infosPath;
            allStrings[1] = name;
            allStrings[2] = ".mpipe";
            sb.Combine(allStrings);
            loader.fsm = new FileStream(sb.str, FileMode.Open, FileAccess.Read);
            loader.LoadAll(clusterCount);
            materialIndexBuffer = resources.vmManager.SetMaterials(loader.allProperties.Length);
            for(int i = 0; i < loader.cluster.Length; ++i)
            {
                loader.cluster[i].index = propertyCount;
            }
            materialProperties = new NativeArray<VirtualMaterial.MaterialProperties>(loader.allProperties.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            VirtualMaterial.MaterialProperties* propertiesPtr = materialProperties.Ptr();
            //Update Material
            for (int i = 0; i < materialProperties.Length; ++i)
            {
                ref var currProp = ref propertiesPtr[i];
                currProp = loader.allProperties[i];
                if (currProp._MainTex >= 0)
                {
                    currProp._MainTex = resources.rgbaPool.GetTex(loader.albedoGUIDs[currProp._MainTex]);
                }
                if (currProp._SecondaryMainTex >= 0)
                {
                    currProp._SecondaryMainTex = resources.rgbaPool.GetTex(loader.secondAlbedoGUIDs[currProp._SecondaryMainTex]);
                }
                if (currProp._BumpMap >= 0)
                {
                    currProp._BumpMap = resources.rgbaPool.GetTex(loader.normalGUIDs[currProp._BumpMap], true);
                }
                if (currProp._SecondaryBumpMap >= 0)
                {
                    currProp._SecondaryBumpMap = resources.rgbaPool.GetTex(loader.secondNormalGUIDs[currProp._SecondaryBumpMap], true);
                }

                if (currProp._SpecularMap >= 0)
                {
                    currProp._SpecularMap = resources.rgbaPool.GetTex(loader.smoGUIDs[currProp._SpecularMap]);
                }
                if (currProp._SecondarySpecularMap >= 0)
                {
                    currProp._SecondarySpecularMap = resources.rgbaPool.GetTex(loader.secondSpecGUIDs[currProp._SecondarySpecularMap]);
                }
                if (currProp._EmissionMap >= 0)
                {
                    currProp._EmissionMap = resources.emissionPool.GetTex(loader.emissionGUIDs[currProp._EmissionMap]);
                }
                if (currProp._HeightMap >= 0)
                {
                    currProp._HeightMap = resources.heightPool.GetTex(loader.heightGUIDs[currProp._HeightMap]);
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
            }
        }

        #region MainThreadCommand
        private const int MAXIMUMINTCOUNT = 5000;
        private const int MAXIMUMVERTCOUNT = 100;

        public void DeleteRun()
        {
            PipelineResources resources = RenderPipeline.current.resources;
            PipelineBaseBuffer baseBuffer = SceneController.baseBuffer;
            int result = baseBuffer.clusterCount - clusterCount;
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
                ComputeBuffer indexBuffer = SceneController.GetTempPropertyBuffer(clusterCount, 8);
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
            this.resources.vmManager.UnloadMaterials(materialIndexBuffer);
            foreach (var i in loader.albedoGUIDs)
            {
                this.resources.rgbaPool.RemoveTex(i);
            }
            foreach (var i in loader.normalGUIDs)
            {
                this.resources.rgbaPool.RemoveTex(i);
            }
            foreach (var i in loader.emissionGUIDs)
            {
                this.resources.emissionPool.RemoveTex(i);
            }
            foreach(var i in loader.smoGUIDs)
            {
                this.resources.rgbaPool.RemoveTex(i);
            }
            foreach (var i in loader.heightGUIDs)
            {
                this.resources.heightPool.RemoveTex(i);
            }
            foreach(var i in loader.secondAlbedoGUIDs)
            {
                this.resources.rgbaPool.RemoveTex(i);
            }
            foreach (var i in loader.secondNormalGUIDs)
            {
                this.resources.rgbaPool.RemoveTex(i);
            }
            foreach (var i in loader.secondSpecGUIDs)
            {
                this.resources.rgbaPool.RemoveTex(i);
            }
            baseBuffer.clusterCount = result;
            loading = false;
            state = State.Unloaded;
            loader.Dispose();
        }
        private static void SetCustomData<T>(ComputeBuffer cb, NativeList<T> arr, int managed, int compute, int count) where T : unmanaged
        {
            cb.SetDataPtr(arr.unsafePtr + managed, compute, count);
        }
        private IEnumerator GenerateRun()
        {
            PipelineResources resources = RenderPipeline.current.resources;
            PipelineBaseBuffer baseBuffer = SceneController.baseBuffer;
            int targetCount;
            int currentCount = 0;
            while ((targetCount = currentCount + MAXIMUMVERTCOUNT) < clusterCount)
            {
                SetCustomData(baseBuffer.clusterBuffer, loader.cluster,  currentCount, currentCount + baseBuffer.clusterCount, MAXIMUMVERTCOUNT);
                SetCustomData(baseBuffer.verticesBuffer, loader.points, currentCount * PipelineBaseBuffer.CLUSTERCLIPCOUNT, (currentCount + baseBuffer.clusterCount) * PipelineBaseBuffer.CLUSTERCLIPCOUNT, MAXIMUMVERTCOUNT * PipelineBaseBuffer.CLUSTERCLIPCOUNT);
               SetCustomData(baseBuffer.triangleMaterialBuffer, loader.triangleMats, currentCount * PipelineBaseBuffer.CLUSTERTRIANGLECOUNT, (currentCount + baseBuffer.clusterCount) * PipelineBaseBuffer.CLUSTERTRIANGLECOUNT, MAXIMUMVERTCOUNT * PipelineBaseBuffer.CLUSTERTRIANGLECOUNT);
                currentCount = targetCount;
                yield return null;
            }
            //TODO
            SetCustomData(baseBuffer.clusterBuffer, loader.cluster, currentCount, currentCount + baseBuffer.clusterCount, clusterCount - currentCount);
            SetCustomData(baseBuffer.verticesBuffer, loader.points,currentCount * PipelineBaseBuffer.CLUSTERCLIPCOUNT, (currentCount + baseBuffer.clusterCount) * PipelineBaseBuffer.CLUSTERCLIPCOUNT, (clusterCount - currentCount) * PipelineBaseBuffer.CLUSTERCLIPCOUNT);
            SetCustomData(baseBuffer.triangleMaterialBuffer, loader.triangleMats, currentCount * PipelineBaseBuffer.CLUSTERTRIANGLECOUNT, (currentCount + baseBuffer.clusterCount) * PipelineBaseBuffer.CLUSTERTRIANGLECOUNT, (clusterCount - currentCount) * PipelineBaseBuffer.CLUSTERTRIANGLECOUNT);
            loading = false;
            state = State.Loaded;
            baseBuffer.clusterCount += clusterCount;
            yield return null;
            loader.cluster.Dispose();
            loader.points.Dispose();
            loader.triangleMats.Dispose();
            this.resources.vmManager.UpdateMaterialToGPU(materialProperties, materialIndexBuffer);
            materialProperties.Dispose();
            Debug.Log("Loaded");
        }
        #endregion
    }
}