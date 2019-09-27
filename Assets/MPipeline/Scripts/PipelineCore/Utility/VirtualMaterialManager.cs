using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using UnityEngine.Rendering;
using Unity.Collections.LowLevel.Unsafe;
namespace MPipeline {

    public unsafe struct VirtualMaterialManager
    {
        private int maximumMateiralCapacity;
        private int singleSceneMaterialCount;
        public ComputeBuffer materialBuffer { get; private set; }
        private ComputeBuffer indexBuffer;
        private ComputeBuffer materialAddBuffer;
        private ComputeShader moveShader;
        private NativeList<int> indexPool;

        public VirtualMaterialManager(int maxMatCapa, int singleMax, ComputeShader moveShader)
        {
            this.moveShader = moveShader;
            maximumMateiralCapacity = maxMatCapa;
            singleSceneMaterialCount = singleMax;
            materialBuffer = new ComputeBuffer(maxMatCapa, sizeof(VirtualMaterial.MaterialProperties));
            materialAddBuffer = new ComputeBuffer(singleMax, sizeof(VirtualMaterial.MaterialProperties));
            indexBuffer = new ComputeBuffer(singleMax, sizeof(int));
            indexPool = new NativeList<int>(maxMatCapa, Allocator.Persistent);
            for (int i = 0; i < maxMatCapa; ++i)
            {
                indexPool.Add(i);
            }
        }

        public NativeArray<int> SetMaterials(int count)
        {
            NativeArray<int> indexArray = new NativeArray<int>(count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            for (int i = 0; i < indexArray.Length; ++i)
            {
                indexArray[i] = indexPool[indexPool.Length - i - 1];
            }
            indexPool.RemoveLast(indexArray.Length);
            return indexArray;
        }

        public void UpdateMaterialToGPU(NativeArray<VirtualMaterial.MaterialProperties> allProperties, NativeArray<int> indexArray)
        {
            indexBuffer.SetData(indexArray);
            materialAddBuffer.SetData(allProperties);
            moveShader.SetBuffer(2, ShaderIDs._MaterialBuffer, materialBuffer);
            moveShader.SetBuffer(2, ShaderIDs._MaterialAddBuffer, materialAddBuffer);
            moveShader.SetBuffer(2, ShaderIDs._OffsetIndex, indexBuffer);
            ComputeShaderUtility.DispatchDirect(moveShader, 2, allProperties.Length);
        }

        public void UnloadMaterials(NativeArray<int> indices)
        {
            indexPool.AddRange(indices.Ptr(), indices.Length);
            indices.Dispose();
        }

        public void Dispose()
        {
            indexPool.Dispose();
            indexBuffer.Dispose();
            materialAddBuffer.Dispose();
            materialBuffer.Dispose();
        }
    }
}