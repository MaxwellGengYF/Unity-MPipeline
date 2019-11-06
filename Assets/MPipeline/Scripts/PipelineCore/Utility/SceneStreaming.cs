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
        public VirtualMaterial vm;
        public enum State
        {
            Unloaded, Loaded, Loading
        }
        [NonSerialized]
        public State state;
        private NativeArray<Cluster> clusterBuffer;
        private NativeArray<Point> pointsBuffer;
        private NativeArray<int> triangleMatBuffer;
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
            var allProperties = vm.allProperties;
            materialProperties = new NativeArray<VirtualMaterial.MaterialProperties>(allProperties.Count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            VirtualMaterial.MaterialProperties* propertiesPtr = materialProperties.Ptr();
            //Update Material
            for (int i = 0; i < materialProperties.Length; ++i)
            {
                ref var currProp = ref propertiesPtr[i];
                currProp = allProperties[i];
                if (currProp._MainTex >= 0)
                {
                    currProp._MainTex = resources.rgbaPool.GetTex(vm.albedoGUIDs[currProp._MainTex]);
                }
                if (currProp._SecondaryMainTex >= 0)
                {
                    currProp._SecondaryMainTex = resources.rgbaPool.GetTex(vm.secondAlbedoGUIDs[currProp._SecondaryMainTex]);
                }
                if (currProp._BumpMap >= 0)
                {
                    currProp._BumpMap = resources.rgbaPool.GetTex(vm.normalGUIDs[currProp._BumpMap], true);
                }
                if (currProp._SecondaryBumpMap >= 0)
                {
                    currProp._SecondaryBumpMap = resources.rgbaPool.GetTex(vm.secondNormalGUIDs[currProp._SecondaryBumpMap], true);
                }

                if (currProp._SpecularMap >= 0)
                {
                    currProp._SpecularMap = resources.rgbaPool.GetTex(vm.smoGUIDs[currProp._SpecularMap]);
                }
                if (currProp._SecondarySpecularMap >= 0)
                {
                    currProp._SecondarySpecularMap = resources.rgbaPool.GetTex(vm.secondSpecGUIDs[currProp._SecondarySpecularMap]);
                }
                if (currProp._EmissionMap >= 0)
                {
                    currProp._EmissionMap = resources.emissionPool.GetTex(vm.emissionGUIDs[currProp._EmissionMap]);
                }
                if (currProp._HeightMap >= 0)
                {
                    currProp._HeightMap = resources.heightPool.GetTex(vm.heightGUIDs[currProp._HeightMap]);
                }
            }
            //Update Cluster
            clusterBuffer = new NativeArray<Cluster>(clusterCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            pointsBuffer = new NativeArray<Point>(clusterCount * PipelineBaseBuffer.CLUSTERCLIPCOUNT, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            triangleMatBuffer = new NativeArray<int>(clusterCount * PipelineBaseBuffer.CLUSTERTRIANGLECOUNT, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            Cluster* clusterData = clusterBuffer.Ptr();
            Point* verticesData = pointsBuffer.Ptr();
            int* triangleMatData = triangleMatBuffer.Ptr();
            allStrings[0] = ClusterMatResources.infosPath;
            allStrings[1] = name;
            allStrings[2] = ".mpipe";
            sb.Combine(allStrings);
            using (FileStream reader = new FileStream(sb.str, FileMode.Open, FileAccess.Read))
            {
                int length = (int)reader.Length;

                byte[] bytes = GetByteArray(length);
                reader.Read(bytes, 0, length);
                fixed (byte* b = bytes)
                {
                    UnsafeUtility.MemCpy(clusterData, b, clusterBuffer.Length * sizeof(Cluster));
                    UnsafeUtility.MemCpy(verticesData, b + clusterBuffer.Length * sizeof(Cluster), pointsBuffer.Length * sizeof(Point));
                    UnsafeUtility.MemCpy(triangleMatData, b + clusterBuffer.Length * sizeof(Cluster) + pointsBuffer.Length * sizeof(Point), triangleMatBuffer.Length * sizeof(int));
                }
            }
            for (int i = 0; i < triangleMatBuffer.Length; ++i)
            {
                triangleMatData[i] = materialIndexBuffer[triangleMatData[i]];
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
                materialIndexBuffer = resources.vmManager.SetMaterials(vm.allProperties.Count);
                LoadingThread.AddCommand(generateAsyncFunc, this);
            }
        }

        public bool GenerateSync()
        {
            if (state != State.Unloaded) return false;
            if (loading) return false;
            materialIndexBuffer = resources.vmManager.SetMaterials(vm.allProperties.Count);
            GenerateAsync(false);
            GenerateRunSync();
            return true;
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
        public bool DeleteSync()
        {
            if (state == State.Unloaded) return false;
            if (loading) return false;
            DeleteRun();
            return true;
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
            foreach (var i in vm.albedoGUIDs)
            {
                this.resources.rgbaPool.RemoveTex(i);
            }
            foreach (var i in vm.normalGUIDs)
            {
                this.resources.rgbaPool.RemoveTex(i);
            }
            foreach (var i in vm.emissionGUIDs)
            {
                this.resources.emissionPool.RemoveTex(i);
            }
            foreach(var i in vm.smoGUIDs)
            {
                this.resources.rgbaPool.RemoveTex(i);
            }
            foreach (var i in vm.heightGUIDs)
            {
                this.resources.heightPool.RemoveTex(i);
            }
            foreach(var i in vm.secondAlbedoGUIDs)
            {
                this.resources.rgbaPool.RemoveTex(i);
            }
            foreach (var i in vm.secondNormalGUIDs)
            {
                this.resources.rgbaPool.RemoveTex(i);
            }
            foreach (var i in vm.secondSpecGUIDs)
            {
                this.resources.rgbaPool.RemoveTex(i);
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
                baseBuffer.triangleMaterialBuffer.SetData(triangleMatBuffer, currentCount * PipelineBaseBuffer.CLUSTERTRIANGLECOUNT, (currentCount + baseBuffer.clusterCount) * PipelineBaseBuffer.CLUSTERTRIANGLECOUNT, MAXIMUMVERTCOUNT * PipelineBaseBuffer.CLUSTERTRIANGLECOUNT);
                currentCount = targetCount;
                yield return null;
            }
            //TODO
            baseBuffer.clusterBuffer.SetData(clusterBuffer, currentCount, currentCount + baseBuffer.clusterCount, clusterBuffer.Length - currentCount);
            baseBuffer.verticesBuffer.SetData(pointsBuffer, currentCount * PipelineBaseBuffer.CLUSTERCLIPCOUNT, (currentCount + baseBuffer.clusterCount) * PipelineBaseBuffer.CLUSTERCLIPCOUNT, (clusterBuffer.Length - currentCount) * PipelineBaseBuffer.CLUSTERCLIPCOUNT);
            baseBuffer.triangleMaterialBuffer.SetData(triangleMatBuffer, currentCount * PipelineBaseBuffer.CLUSTERTRIANGLECOUNT, (currentCount + baseBuffer.clusterCount) * PipelineBaseBuffer.CLUSTERTRIANGLECOUNT, (clusterBuffer.Length - currentCount) * PipelineBaseBuffer.CLUSTERTRIANGLECOUNT);
            int clusterCount = clusterBuffer.Length;
            loading = false;
            state = State.Loaded;
            baseBuffer.clusterCount += clusterCount;
            yield return null;
            clusterBuffer.Dispose();
            pointsBuffer.Dispose();
            triangleMatBuffer.Dispose();
            this.resources.vmManager.UpdateMaterialToGPU(materialProperties, materialIndexBuffer);
            materialProperties.Dispose();
            Debug.Log("Loaded");
        }

        private void GenerateRunSync()
        {
            PipelineBaseBuffer baseBuffer = SceneController.baseBuffer;
            baseBuffer.clusterBuffer.SetData(clusterBuffer, 0, baseBuffer.clusterCount, clusterBuffer.Length);
            baseBuffer.verticesBuffer.SetData(pointsBuffer, 0, baseBuffer.clusterCount * PipelineBaseBuffer.CLUSTERCLIPCOUNT, clusterBuffer.Length * PipelineBaseBuffer.CLUSTERCLIPCOUNT);
            baseBuffer.triangleMaterialBuffer.SetData(triangleMatBuffer, 0, baseBuffer.clusterCount * PipelineBaseBuffer.CLUSTERTRIANGLECOUNT, clusterBuffer.Length * PipelineBaseBuffer.CLUSTERTRIANGLECOUNT);
            int clusterCount = clusterBuffer.Length;
            clusterBuffer.Dispose();
            pointsBuffer.Dispose();
            triangleMatBuffer.Dispose();
            this.resources.vmManager.UpdateMaterialToGPU(materialProperties, materialIndexBuffer);
            materialProperties.Dispose();
            loading = false;
            state = State.Loaded;
            baseBuffer.clusterCount += clusterCount;
            Debug.Log("Loaded");
        }
        #endregion
    }
}