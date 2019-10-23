using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.AddressableAssets;
using System.Threading;
using System.IO;
namespace MPipeline
{
    public unsafe sealed class VirtualTextureLoader
    {
        public struct LoadingHandler
        {
            public int2 position;
            public int mipLevel;
            public byte* allBytes;
            public bool* isComplete;
            public LoadingHandler(int2 position, int mipLevel)
            {
                this.position = position;
                this.mipLevel = mipLevel;
                allBytes = MUnsafeUtility.Malloc<byte>(CHUNK_SIZE + 1, Allocator.Persistent);
                isComplete = (bool*)(allBytes + CHUNK_SIZE);
                *isComplete = false;
            }

            public void Dispose()
            {
                MUnsafeUtility.SafeFree(ref allBytes, Allocator.Persistent);
            }
        }
        public int mipLevel { get; private set; }
        private NativeQueue<LoadingHandler> handlerQueue;
        private NativeArray<long> streamPositionOffset;
        private FileStream streamer;
        private object lockerObj;
        private AutoResetEvent resetEvent;
        private Thread loadingThread;
        private bool enabled;
        private int initialMipCount;
        private byte[] bufferBytes;
        private const long CHUNK_SIZE = MTerrain.HEIGHT_RESOLUTION * MTerrain.HEIGHT_RESOLUTION * 2;
        public static NativeArray<long> GetStreamingPositionOffset(int initialMipCount, int mipLevel, Allocator alloc = Allocator.Persistent)
        {
            var streamPositionOffset = new NativeArray<long>(mipLevel, alloc, NativeArrayOptions.UninitializedMemory);
            long offset = 0;
            for (int i = 0; i < mipLevel; ++i)
            {
                streamPositionOffset[i] = offset;
                long curOffset = (long)(0.1 + pow(2, i + initialMipCount));
                offset += curOffset * curOffset;
            }
            return streamPositionOffset;
        }

        public VirtualTextureLoader(int initialMipCount, int mipLevel, string path, object lockerObj)
        {
            enabled = true;
            this.initialMipCount = initialMipCount;
            handlerQueue = new NativeQueue<LoadingHandler>(100, Allocator.Persistent);
            this.lockerObj = lockerObj;
            this.mipLevel = mipLevel;
            resetEvent = new AutoResetEvent(true);
            bufferBytes = new byte[CHUNK_SIZE];
            streamPositionOffset = GetStreamingPositionOffset(initialMipCount, mipLevel);
            streamer = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Read, FileShare.Read, (int)CHUNK_SIZE);
            loadingThread = new Thread(() =>
            {
                while (enabled)
                {
                    while (true)
                    {
                        LoadingHandler handler;
                        lock (lockerObj)
                        {
                            if (!handlerQueue.TryDequeue(out handler))
                                break;
                        }
                        try
                        {
                            streamer.Position = CHUNK_SIZE * ((long)(0.1 + pow(2.0, handler.mipLevel)) * handler.position.y + handler.position.x + streamPositionOffset[handler.mipLevel - initialMipCount]);
                            streamer.Read(bufferBytes, 0, (int)CHUNK_SIZE);
                            UnsafeUtility.MemCpy(handler.allBytes, bufferBytes.Ptr(), CHUNK_SIZE);
                        }
                        finally
                        {
                            *handler.isComplete = true;
                        }
                    }
                    resetEvent.WaitOne();
                }
            });
            loadingThread.Start();
        }

        public LoadingHandler LoadChunk(int2 position, int targetMipLevel)
        {
            LoadingHandler lh = new LoadingHandler(position, targetMipLevel);
            lock (lockerObj)
            {
                handlerQueue.Add(lh);
            }
            return lh;
        }

        public void StartLoading()
        {
            resetEvent.Set();
        }

        public void Dispose()
        {
            loadingThread = null;
            bufferBytes = null;
            streamer.Dispose();
            streamPositionOffset.Dispose();
            resetEvent.Set();
            enabled = false;
            resetEvent.Dispose();
            lockerObj = null;
            handlerQueue.Dispose();
        }
    }
    public unsafe sealed class TerrainMaskLoader
    {
        private struct MaskBuffer
        {
            public ulong offset;
            public byte* bytesData;
            public bool* isFinished;
            public MaskBuffer(ulong offset)
            {
                this.offset = offset;
                bytesData = MUnsafeUtility.Malloc<byte>((long)MASK_SIZE + 1, Allocator.Persistent);
                isFinished = (bool*)(bytesData + MASK_SIZE);
                *isFinished = false;
            }
            public void Dispose()
            {
                MUnsafeUtility.SafeFree(ref bytesData, Allocator.Persistent);
            }
        }
        private FileStream maskLoader;
        private ComputeShader terrainEditShader;
        private ComputeBuffer readWriteBuffer;
        private AutoResetEvent resetEvent;
        private Thread loadingThread;
        private bool enabled;
        private const ulong MASK_SIZE = MTerrain.MASK_RESOLUTION * MTerrain.MASK_RESOLUTION;
        private NativeQueue<MaskBuffer> loadingCommandQueue;
        private int terrainMaskCount;
        private byte[] fileReadBuffer;
        public static ulong GetByteOffset(int2 chunkCoord, int terrainMaskCount)
        {
            ulong chunkPos = (ulong)(chunkCoord.y * terrainMaskCount + chunkCoord.x);
            return chunkPos * MASK_SIZE;
        }

        public TerrainMaskLoader(string pathName, ComputeShader terrainEditShader, int terrainMaskCount)
        {
            this.terrainMaskCount = terrainMaskCount;
            fileReadBuffer = new byte[MASK_SIZE];
            maskLoader = new FileStream(pathName, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            enabled = true;
            loadingCommandQueue = new NativeQueue<MaskBuffer>(100, Allocator.Persistent);
            this.terrainEditShader = terrainEditShader;
            readWriteBuffer = new ComputeBuffer((int)(MASK_SIZE / sizeof(uint)), sizeof(uint));
            resetEvent = new AutoResetEvent(true);
            loadingThread = new Thread(() =>
            {
                while (enabled)
                {
                    resetEvent.WaitOne();
                    MaskBuffer mb;
                    while (loadingCommandQueue.TryDequeue(out mb))
                    {
                        maskLoader.Position = (long)mb.offset;
                        maskLoader.Read(fileReadBuffer, 0, (int)MASK_SIZE);
                        UnsafeUtility.MemCpy(mb.bytesData, fileReadBuffer.Ptr(), (long)MASK_SIZE);
                        *mb.isFinished = true;
                    }
                }
            });
            loadingThread.Start();
        }
        private static bool MaskBufferIsFinished(ref MaskBuffer mb)
        {
            return *mb.isFinished;
        }

        private void SetBufferData(ref MaskBuffer mb)
        {
            readWriteBuffer.SetDataPtr(mb.bytesData, (int)MASK_SIZE);
        }

        public IEnumerator ReadToTexture(RenderTexture rt, int texElement, int2 chunkCoord)
        {
            MaskBuffer mb = new MaskBuffer(GetByteOffset(chunkCoord, terrainMaskCount));
            loadingCommandQueue.Add(mb);
            resetEvent.Set();
            while (!MaskBufferIsFinished(ref mb))
            {
                yield return null;
            }
            SetBufferData(ref mb);
            mb.Dispose();
            terrainEditShader.SetInt(ShaderIDs._OffsetIndex, texElement);
            terrainEditShader.SetInt(ShaderIDs._Count, MTerrain.MASK_RESOLUTION);
            terrainEditShader.SetTexture(2, ShaderIDs._DestTex, rt);
            terrainEditShader.SetBuffer(2, ShaderIDs._ElementBuffer, readWriteBuffer);
            const int disp = MTerrain.MASK_RESOLUTION / 8;
            terrainEditShader.Dispatch(2, disp, disp, 1);
        }

        public void WriteToDisk(RenderTexture rt, int texElement, int2 chunkCoord)
        {
            terrainEditShader.SetInt(ShaderIDs._OffsetIndex, texElement);
            terrainEditShader.SetInt(ShaderIDs._Count, MTerrain.MASK_RESOLUTION);
            terrainEditShader.SetTexture(3, ShaderIDs._DestTex, rt);
            terrainEditShader.SetBuffer(3, ShaderIDs._ElementBuffer, readWriteBuffer);
            const int disp = (int)(MASK_SIZE / 64 / 4);
            terrainEditShader.Dispatch(3, disp, 1, 1);
            readWriteBuffer.GetData(fileReadBuffer);
            maskLoader.Position = (long)GetByteOffset(chunkCoord, terrainMaskCount);
            maskLoader.Write(fileReadBuffer, 0, (int)MASK_SIZE);
        }

        public void Dispose()
        {
            enabled = false;
            resetEvent.Set();
            loadingCommandQueue.Dispose();
            loadingThread = null;
            readWriteBuffer.Dispose();
            if (maskLoader != null) maskLoader.Dispose();
        }
    }
}