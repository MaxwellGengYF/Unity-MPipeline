using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using static Unity.Mathematics.math;
using Unity.Mathematics;
using UnityEngine.Experimental.Rendering;

namespace MPipeline
{
    public enum VirtualTextureSize
    {
        x16 = 16,
        x32 = 32,
        x64 = 64,
        x128 = 128,
        x256 = 256,
        x512 = 512,
        x1024 = 1024,
        x2048 = 2048,
        x4096 = 4096,
        x8192 = 8192
    };
    public struct VirtualTextureFormat
    {
        public VirtualTextureSize perElementSize { get; private set; }
        public GraphicsFormat format { get; private set; }
        public int rtPropertyID { get; private set; }
        public VirtualTextureFormat(VirtualTextureSize size, GraphicsFormat format, string rtName)
        {
            perElementSize = size;
            this.format = format;
            rtPropertyID = Shader.PropertyToID(rtName);
        }
    }
    public unsafe struct VirtualTexture
    {
        private struct TexturePool
        {
            private NativeArray<bool> marks;
            private NativeList<int> arrayPool;
            public int LeftedElement
            {
                get
                {
                    return arrayPool.Length;
                }
            }
            public TexturePool(int capacity)
            {
                marks = new NativeArray<bool>(capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                arrayPool = new NativeList<int>(capacity, Allocator.Persistent);
                for (int i = 0; i < capacity; ++i)
                {
                    arrayPool.Add(i);
                }
            }
            public void Dispose()
            {
                marks.Dispose();
                arrayPool.Dispose();
            }
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Return(int i)
            {
                if (i >= 0 && marks[i])
                {
                    marks[i] = false;
                    arrayPool.Add(i);
                }
            }
            public bool Get(out int t)
            {

                do
                {
                    if (arrayPool.Length <= 0)
                    {
                        t = -1;
                        return false;
                    }
                    t = arrayPool[arrayPool.Length - 1];
                    arrayPool.RemoveLast();
                } while (marks[t]);
                marks[t] = true;
                return true;
            }
        }
        private ComputeShader shader;
        private RenderTexture[] textures;
        private TexturePool pool;
        public int indexTexID { get; private set; }
        private NativeArray<VirtualTextureFormat> allFormats;
        private static int[] vtVariables = new int[4];
        private static int[] texSize = new int[2];
        private ComputeBuffer setIndexBuffer;
        private const int START_CHUNKSIZE = 8;
        public int2 indexSize { get; private set; }
        public RenderTexture indexTex { get; private set; }
        private struct VTChunkHandleEqual : IFunction<int2, int2, bool>
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool Run(ref int2 a, ref int2 b)
            {
                return a.x == b.x && a.y == b.y;
            }
        }
        private NativeDictionary<int2, int2, VTChunkHandleEqual> poolDict;
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public RenderTexture GetTexture(int index)
        {
            return textures[index];
        }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public VirtualTextureFormat GetTextureFormat(int index)
        {
            return allFormats[index];
        }
        public int LeftedTextureElement
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get
            {
                return pool.LeftedElement;
            }
        }

        public void Update()
        {
            CommandBuffer beforeFrameBuffer = RenderPipeline.BeforeFrameBuffer;
            beforeFrameBuffer.SetGlobalTexture(indexTexID, indexTex);
            for (int i = 0; i < allFormats.Length; ++i)
            {
                beforeFrameBuffer.SetGlobalTexture(allFormats[i].rtPropertyID, textures[i]);
            }
        }
        public void Update(Material targetMaterial)
        {
            targetMaterial.SetTexture(indexTexID, indexTex);
            for (int i = 0; i < allFormats.Length; ++i)
            {
                targetMaterial.SetTexture(allFormats[i].rtPropertyID, textures[i]);
            }
        }
        /// <summary>
        /// Init Virtual Texture
        /// </summary>
        /// <param name="perTextureSize">Virtual texture's basic size</param>
        /// <param name="maximumSize">Virtual texture's array size</param>
        /// <param name="indexSize">Index Texture's size</param>
        /// <param name="formats">Each VT's format</param>
        public VirtualTexture(int maximumSize, int2 indexSize, VirtualTextureFormat* formats, int formatLen, string indexTexName, int mipCount = 0)
        {
            if (maximumSize > 2048)
            {
                throw new System.Exception("Virtual Texture Maximum Size can not larger than 2048");
            }
            indexTexID = Shader.PropertyToID(indexTexName);
            this.indexSize = indexSize;
            setIndexBuffer = new ComputeBuffer(START_CHUNKSIZE * START_CHUNKSIZE, sizeof(uint));
            allFormats = new NativeArray<VirtualTextureFormat>(formatLen, Allocator.Persistent);
            poolDict = new NativeDictionary<int2, int2, VTChunkHandleEqual>(maximumSize, Allocator.Persistent, new VTChunkHandleEqual());
            UnsafeUtility.MemCpy(allFormats.GetUnsafePtr(), formats, sizeof(VirtualTextureFormat) * formatLen);
            shader = Resources.Load<ComputeShader>("VirtualTexture");
            pool = new TexturePool(maximumSize);
            indexTex = new RenderTexture(new RenderTextureDescriptor
            {
                graphicsFormat = GraphicsFormat.R16G16B16A16_UNorm,
                depthBufferBits = 0,
                dimension = TextureDimension.Tex2D,
                enableRandomWrite = true,
                width = indexSize.x,
                height = indexSize.y,
                volumeDepth = 1,
                msaaSamples = 1
            });
            indexTex.filterMode = FilterMode.Point;
            indexTex.Create();
            textures = new RenderTexture[formatLen];
            for (int i = 0; i < formatLen; ++i)
            {
                ref VirtualTextureFormat format = ref formats[i];
                textures[i] = new RenderTexture(new RenderTextureDescriptor
                {
                    graphicsFormat = format.format,
                    width = (int)format.perElementSize,
                    height = (int)format.perElementSize,
                    volumeDepth = maximumSize,
                    dimension = TextureDimension.Tex2DArray,
                    mipCount = mipCount,
                    autoGenerateMips = false,
                    useMipMap = mipCount > 0,
                    enableRandomWrite = true,
                    msaaSamples = 1,
                    depthBufferBits = 0,
                });
                textures[i].Create();
            }
        }
        public void Dispose()
        {
            Object.DestroyImmediate(indexTex);
            foreach (var i in textures)
            {
                Object.DestroyImmediate(i);
            }
            allFormats.Dispose();
            pool.Dispose();
            setIndexBuffer.Dispose();
            poolDict.Dispose();
        }

        public int GetChunkIndex(int2 startIndex)
        {
            int2 result;
            if (poolDict.Get(startIndex, out result))
            {
                return result.y;
            }
            return -1;
        }

        public bool ContainsChunk(int2 startIndex, int size)
        {
            startIndex %= indexSize;
            int2 result;
            if (poolDict.Get(startIndex, out result))
            {
                if (result.x == size) return true;
            }
            return false;
        }

        private bool GetChunk(ref int2 startIndex, int size, out int element)
        {
            startIndex %= indexSize;
            int2 result;
            if (poolDict.Get(startIndex, out result))
            {
                if (result.x != size)
                {
                    result.x = size;
                    poolDict[startIndex] = result;
                }
                element = result.y;
                return true;
            }
            bool res = pool.Get(out element);
            result = int2(size, element);
            poolDict.Add(startIndex, result);
            return res;
        }

        private int UnloadChunk(ref int2 startIndex)
        {
            startIndex %= indexSize;
            int2 result;
            if (poolDict.Get(startIndex, out result))
            {
                poolDict.Remove(startIndex);
                pool.Return(result.y);
                return result.x;
            }
            return 0;
        }


        /// <summary>
        /// load a new texture into virtual texture
        /// </summary>
        /// <param name="startIndex">Start Index in the index texture</param>
        /// <param name="size">Pixel count in index texture</param>
        /// <returns>The target array index in TextureArray, return -1 if the pool is full</returns>
        public bool LoadNewTexture(int2 startIndex, int size, out int element)
        {
            bool res = GetChunk(ref startIndex, size, out element);
            vtVariables[0] = startIndex.x;
            vtVariables[1] = startIndex.y;
            vtVariables[2] = size;
            vtVariables[3] = element;
            shader.SetInts(ShaderIDs._VTVariables, vtVariables);
            texSize[0] = indexSize.x;
            texSize[1] = indexSize.y;
            shader.SetInts(ShaderIDs._IndexTextureSize, texSize);
            shader.SetTexture(0, ShaderIDs._IndexTexture, indexTex);
            int dispatchCount = Mathf.CeilToInt(size / 8f);
            shader.Dispatch(0, dispatchCount, dispatchCount, 1);
            return res;
        }

        public void LoadQuadNewTextures(int2 startIndex, int size, out int4 element)
        {
            int2 leftDownIndex = startIndex;
            int2 rightDownIndex = startIndex + int2(size, 0);
            int2 leftUpIndex = startIndex + int2(0, size);
            int2 rightUpIndex = startIndex + size;
            GetChunk(ref leftDownIndex, size, out element.x);
            GetChunk(ref leftUpIndex, size, out element.y);
            GetChunk(ref rightDownIndex, size, out element.z);
            GetChunk(ref rightUpIndex, size, out element.w);
            vtVariables[0] = startIndex.x;
            vtVariables[1] = startIndex.y;
            vtVariables[2] = size;
            vtVariables[3] = size * 2;
            shader.SetInts(ShaderIDs._VTVariables, vtVariables);
            shader.SetVector(ShaderIDs._TextureSize, (float4)((element + double4(0.4)) / 2048.0));
            texSize[0] = indexSize.x;
            texSize[1] = indexSize.y;
            shader.SetInts(ShaderIDs._IndexTextureSize, texSize);
            shader.SetTexture(4, ShaderIDs._IndexTexture, indexTex);
            int dispatchCount = Mathf.CeilToInt(vtVariables[3] / 8f);
            shader.Dispatch(4, dispatchCount, dispatchCount, 1);
        }

        public NativeArray<int> LoadNewTextureChunks(int2 startIndex, int size, Allocator alloc)
        {
            if (size * size > setIndexBuffer.count)
            {
                setIndexBuffer.Dispose();
                setIndexBuffer = new ComputeBuffer(size * size, sizeof(uint));
            }
            NativeArray<int> texs = new NativeArray<int>(size * size, alloc, NativeArrayOptions.UninitializedMemory);

            int* ptr = texs.Ptr();
            for (int y = 0; y < size; ++y)
                for (int x = 0; x < size; ++x)
                {
                    int2 idx = startIndex + int2(x, y);
                    bool res = GetChunk(ref idx, 1, out ptr[x + y * size]);
                }
            startIndex %= indexSize;
            vtVariables[0] = startIndex.x;
            vtVariables[1] = startIndex.y;
            vtVariables[2] = size;
            shader.SetInts(ShaderIDs._VTVariables, vtVariables);
            texSize[0] = indexSize.x;
            texSize[1] = indexSize.y;
            shader.SetInts(ShaderIDs._IndexTextureSize, texSize);
            shader.SetTexture(1, ShaderIDs._IndexTexture, indexTex);
            shader.SetBuffer(1, ShaderIDs._ElementBuffer, setIndexBuffer);
            setIndexBuffer.SetData(texs);
            int dispatchCount = Mathf.CeilToInt(size / 8f);
            shader.Dispatch(1, dispatchCount, dispatchCount, 1);
            return texs;
        }
        /// <summary>
        /// Unload space
        /// </summary>
        /// <param name="startIndex">Start Index in IndexTexture </param>
        /// <param name="size">Target Size in IndexTexture</param>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void UnloadTexture(int2 startIndex)
        {
            UnloadChunk(ref startIndex);
        }

        public void CombineTexture(int2 startIndex, int targetSize, bool unloadCheck)
        {
            if (unloadCheck)
            {
                for (int y = 0; y < targetSize; ++y)
                {
                    for (int x = 0; x < targetSize; ++x)
                    {
                        int2 curIdx = startIndex + int2(x, y);
                        UnloadChunk(ref curIdx);
                    }
                }
            }
            int targetElement;
            if (!GetChunk(ref startIndex, targetSize, out targetElement)) return;
            int3* vtPtr = (int3*)vtVariables.Ptr();
            *vtPtr = int3(startIndex, targetSize);
            shader.SetInts(ShaderIDs._VTVariables, vtVariables);
            texSize[0] = indexSize.x;
            texSize[1] = indexSize.y;
            shader.SetInts(ShaderIDs._IndexTextureSize, texSize);
            shader.SetTexture(3, ShaderIDs._IndexTexture, indexTex);
            for (int i = 0; i < allFormats.Length; ++i)
            {
                VirtualTextureFormat fmt = allFormats[i];
                RenderTexture blendRT = RenderTexture.GetTemporary(new RenderTextureDescriptor
                {
                    width = (int)fmt.perElementSize,
                    height = (int)fmt.perElementSize,
                    volumeDepth = 1,
                    graphicsFormat = fmt.format,
                    dimension = TextureDimension.Tex2D,
                    enableRandomWrite = true,
                    msaaSamples = 1
                });
                blendRT.Create();
                shader.SetInt(ShaderIDs._Count, (int)fmt.perElementSize);
                shader.SetTexture(3, ShaderIDs._TextureBuffer, textures[i]);
                shader.SetTexture(3, ShaderIDs._BlendTex, blendRT);
                int disp = Mathf.CeilToInt((int)fmt.perElementSize / 8f);
                shader.Dispatch(3, disp, disp, 1);
                Graphics.CopyTexture(blendRT, 0, 0, textures[i], targetElement, 0);
                RenderTexture.ReleaseTemporary(blendRT);
            }
            vtVariables[0] = startIndex.x;
            vtVariables[1] = startIndex.y;
            vtVariables[2] = targetSize;
            vtVariables[3] = targetElement;
            shader.SetInts(ShaderIDs._VTVariables, vtVariables);
            shader.SetTexture(0, ShaderIDs._IndexTexture, indexTex);
            int dispatchCount = Mathf.CeilToInt(targetSize / 8f);
            shader.Dispatch(0, dispatchCount, dispatchCount, 1);
        }
    }
}