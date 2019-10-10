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

    public unsafe class VirtualTextureLoader
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
        private const long CHUNK_SIZE = MTerrain.HEIGHT_RESOLUTION * MTerrain.HEIGHT_RESOLUTION * 2 + MTerrain.MASK_RESOLUTION * MTerrain.MASK_RESOLUTION;
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
            streamer = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, (int)CHUNK_SIZE);
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
}