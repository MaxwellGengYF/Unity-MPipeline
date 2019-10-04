using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using static Unity.Mathematics.math;
using Unity.Mathematics;
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
        public RenderTextureFormat format { get; private set; }
        public int rtPropertyID { get; private set; }
        public VirtualTextureFormat(VirtualTextureSize size, RenderTextureFormat format, string rtName)
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
            public int Get()
            {
                int t;
                do
                {
                    if (arrayPool.Length <= 0)
                    {
#if UNITY_EDITOR
                        throw new System.Exception("Virtual Texture Pool is out of range!!");
#endif
                        return -1;
                    }
                    t = arrayPool[arrayPool.Length - 1];
                    arrayPool.RemoveLast();
                } while (marks[t]);
                marks[t] = true;
                return t;
            }
        }
        private ComputeShader shader;
        public RenderTexture indexTex { get; private set; }
        private RenderTexture[] textures;
        private TexturePool pool;
        private int indexTexID;
        private NativeArray<VirtualTextureFormat> allFormats;
        private static int[] vtVariables = new int[4];
        private static int[] texSize = new int[2];
        private ComputeBuffer setIndexBuffer;
        private const int START_CHUNKSIZE = 8;
        public int2 indexSize { get; private set; }
        private struct VTChunkHandleEqual : IFunction<int2, int2, bool>
        {
            public bool Run(ref int2 a, ref int2 b)
            {
                return a.x == b.x && a.y == b.y;
            }
        }
        private NativeDictionary<int2, int2, VTChunkHandleEqual> poolDict;
        public RenderTexture GetTexture(int index)
        {
            return textures[index];
        }
        public int LeftedTextureElement
        {
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
            if(maximumSize > 2048)
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
                colorFormat = RenderTextureFormat.ARGB64,
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
                VirtualTextureFormat format = formats[i];
                textures[i] = new RenderTexture((int)format.perElementSize, (int)format.perElementSize, 0, format.format, mipCount);
                textures[i].useMipMap = mipCount > 0;
                textures[i].autoGenerateMips = false;
                textures[i].enableRandomWrite = true;
                textures[i].dimension = TextureDimension.Tex2DArray;
                textures[i].volumeDepth = maximumSize;
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

        private int GetChunk(ref int2 startIndex, int size)
        {
            startIndex %= indexSize;
            int2 result;
            if (poolDict.Get(startIndex, out result))
            {
                if (result.x != size)
                {
                    result = int2(size, result.y);
                    poolDict[startIndex] = result;
                }
                return result.y;
            }
            result = int2(size, pool.Get());
            poolDict.Add(startIndex, result);
            return result.y;
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
        public int LoadNewTexture(int2 startIndex, int size)
        {
            int sizeElement = GetChunk(ref startIndex, size);
            vtVariables[0] = startIndex.x;
            vtVariables[1] = startIndex.y;
            vtVariables[2] = size;
            vtVariables[3] = sizeElement;
            shader.SetInts(ShaderIDs._VTVariables, vtVariables);
            texSize[0] = indexSize.x;
            texSize[1] = indexSize.y;
            shader.SetInts(ShaderIDs._IndexTextureSize, texSize);
            shader.SetTexture(0, ShaderIDs._IndexTexture, indexTex);
            int dispatchCount = Mathf.CeilToInt(size / 8f);
            shader.Dispatch(0, dispatchCount, dispatchCount, 1);
            return sizeElement;
        }

        public NativeArray<int> LoadNewTextureChunks(int2 startIndex, int size, Allocator alloc, int loadLod = 0, Texture sourceTex = null, Texture[] sourceTexs = null)
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
                    ptr[x + y * size] = GetChunk(ref idx, 1);
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
            int powValue = (int)(0.1 + pow(2.0, loadLod));
            if (sourceTex)
            {

                int width = sourceTex.width / powValue;
                shader.SetVector(ShaderIDs._TextureSize, float4(width, size, (int)allFormats[0].perElementSize / powValue, 0));
                shader.SetBuffer(2, ShaderIDs._ElementBuffer, setIndexBuffer);
                shader.SetTexture(2, ShaderIDs._VirtualTexture, textures[0], loadLod);
                shader.SetTexture(2, ShaderIDs._MainTex, sourceTex);
                int disp = Mathf.CeilToInt(width / 8f);
                shader.Dispatch(2, disp, disp, 1);
            }
            if (sourceTexs != null)
            {
                int ite = min(sourceTexs.Length, allFormats.Length);
                shader.SetBuffer(2, ShaderIDs._ElementBuffer, setIndexBuffer);
                for (int i = 0; i < ite; ++i)
                {
                    int width = sourceTexs[i].width / powValue;
                    shader.SetVector(ShaderIDs._TextureSize, float4(width, size, (int)allFormats[0].perElementSize / powValue, 0));
                    shader.SetTexture(2, ShaderIDs._VirtualTexture, textures[i], loadLod);
                    shader.SetTexture(2, ShaderIDs._MainTex, sourceTexs[i]);
                    int disp = Mathf.CeilToInt(width / 8f);
                    shader.Dispatch(2, disp, disp, 1);
                }
            }
            return texs;
        }
        /// <summary>
        /// Unload space
        /// </summary>
        /// <param name="startIndex">Start Index in IndexTexture </param>
        /// <param name="size">Target Size in IndexTexture</param>
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
            int targetElement = GetChunk(ref startIndex, targetSize);
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
                    colorFormat = fmt.format,
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
                Graphics.Blit(blendRT, textures[i], 0, targetElement);
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