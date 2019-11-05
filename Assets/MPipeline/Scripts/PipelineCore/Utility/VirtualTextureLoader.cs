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
        public struct MaskBuffer
        {
            public long offset { get; private set; }
            public byte* bytesData { get; private set; }
            public bool* isFinished { get; private set; }
            public MaskBuffer(long offset, long size)
            {
                this.offset = offset;
                bytesData = MUnsafeUtility.Malloc<byte>((long)size + 1, Allocator.Persistent);
                isFinished = (bool*)(bytesData + size);
                *isFinished = false;
            }
            public void Dispose()
            {
                if (bytesData != null)
                    UnsafeUtility.Free(bytesData, Allocator.Persistent);
            }
        }
        private FileStream maskLoader;
        private ComputeShader terrainEditShader;
        private ComputeBuffer readWriteBuffer;
        private System.Action loadingThreadExecutor;
        private long size;
        private long resolution;

        private NativeQueue<MaskBuffer> loadingCommandQueue;
        private int terrainMaskCount;
        private byte[] fileReadBuffer;
        private int readPass;
        private int writePass;
        private int bitLength;
        private MTerrainLoadingThread loadingThread;
        public long GetByteOffset(int2 chunkCoord, int terrainMaskCount)
        {
            long chunkPos = (long)(chunkCoord.y * terrainMaskCount + chunkCoord.x);
            return chunkPos * size;
        }

        public VirtualTextureLoader(string pathName, ComputeShader terrainEditShader, int terrainMaskCount, long resolution, bool is16Bit, MTerrainLoadingThread loadingThread)
        {
            this.loadingThread = loadingThread;
            if (is16Bit)
            {
                readPass = 4;
                writePass = 5;
                bitLength = 2;
            }
            else
            {
                bitLength = 1;
                readPass = 2;
                writePass = 3;
            }
            this.resolution = resolution;
            size = resolution * resolution * bitLength;
            this.terrainMaskCount = terrainMaskCount;
            fileReadBuffer = new byte[size];
            maskLoader = new FileStream(pathName, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            loadingCommandQueue = new NativeQueue<MaskBuffer>(100, Allocator.Persistent);
            this.terrainEditShader = terrainEditShader;
            readWriteBuffer = new ComputeBuffer((int)(size / sizeof(uint)), sizeof(uint));
            loadingThreadExecutor = () =>
            {
                MaskBuffer mb;
                while (loadingCommandQueue.TryDequeue(out mb))
                {
                    maskLoader.Position = mb.offset;
                    maskLoader.Read(fileReadBuffer, 0, (int)size);
                    UnsafeUtility.MemCpy(mb.bytesData, fileReadBuffer.Ptr(), size);
                    *mb.isFinished = true;
                }
            };


        }
        private static bool MaskBufferIsFinished(ref MaskBuffer mb)
        {
            return *mb.isFinished;
        }

        private void SetBufferData(ref MaskBuffer mb, int localsize, int offset)
        {
            readWriteBuffer.SetDataPtr((mb.bytesData + offset * localsize), localsize * offset, localsize);
        }

        public MaskBuffer ScheduleLoadingJob(int2 chunkCoord)
        {
            MaskBuffer mb = new MaskBuffer(GetByteOffset(chunkCoord, terrainMaskCount), size);
            loadingCommandQueue.Add(mb);
            loadingThread.AddMission(loadingThreadExecutor);
            return mb;
        }

        public IEnumerator ReadToTexture(RenderTexture rt, int texElement, MaskBuffer mb, int separateFrame)
        {

            while (!MaskBufferIsFinished(ref mb))
            {
                yield return null;
            }
            int separateSize = (int)size / separateFrame;
            for (int i = 0; i < separateFrame; ++i)
            {
                SetBufferData(ref mb, separateSize, i);
                yield return null;
            }
            mb.Dispose();

            terrainEditShader.SetInt(ShaderIDs._OffsetIndex, texElement);
            terrainEditShader.SetInt(ShaderIDs._Count, (int)resolution);
            terrainEditShader.SetTexture(readPass, ShaderIDs._DestTex, rt);
            terrainEditShader.SetBuffer(readPass, ShaderIDs._ElementBuffer, readWriteBuffer);
            int disp = (int)resolution / 16;
            terrainEditShader.Dispatch(readPass, disp, disp, 1);
        }

        public void WriteToDisk(RenderTexture rt, int texElement, int2 chunkCoord)
        {
            terrainEditShader.SetInt(ShaderIDs._OffsetIndex, texElement);
            terrainEditShader.SetInt(ShaderIDs._Count, (int)resolution);
            terrainEditShader.SetTexture(writePass, ShaderIDs._DestTex, rt);
            terrainEditShader.SetBuffer(writePass, ShaderIDs._ElementBuffer, readWriteBuffer);
            int disp = (int)(size / 256 / 4);
            terrainEditShader.Dispatch(writePass, disp, 1, 1);
            readWriteBuffer.GetData(fileReadBuffer, 0, 0, readWriteBuffer.count * 4);
            maskLoader.Position = GetByteOffset(chunkCoord, terrainMaskCount);
            maskLoader.Write(fileReadBuffer, 0, (int)size);
        }

        public void Dispose()
        {
            loadingCommandQueue.Dispose();
            readWriteBuffer.Dispose();
            if (maskLoader != null) maskLoader.Dispose();
        }
    }
}