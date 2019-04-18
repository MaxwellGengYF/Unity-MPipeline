using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System;
namespace MPipeline
{
    public unsafe struct VirtualTexture
    {
        public struct TextureSparse
        {
            public int size;
            public int2 offset;
        }
        private int resolution;
        private RenderTextureFormat format;
        private ArrayOfNativeLink<TextureSparse> allSparse;
        private static bool2 IsSparseAligned(TextureSparse sparse)
        {
            return sparse.offset % (sparse.size * 2) == 0;
        }
        public VirtualTexture(int resolution, RenderTextureFormat format)
        {
            this.resolution = resolution;
            this.format = format;
            allSparse = new ArrayOfNativeLink<TextureSparse>((int)(0.01f + log2(resolution)), Allocator.Persistent, (a, b) =>
            {
                bool2 offset = a.offset == b.offset;
                bool2 size = a.size == b.size;
                return offset.x && offset.y && size.x && size.y;
            });
            NativeLinkedList<TextureSparse> originSparse = allSparse[allSparse.Length - 1];
            originSparse.AddLast(new TextureSparse
            {
                offset = 0,
                size = resolution / 2,
            });
            originSparse.AddLast(new TextureSparse
            {
                offset = int2(0, resolution / 2),
                size = resolution / 2,
            });
            originSparse.AddLast(new TextureSparse
            {
                offset = int2(resolution / 2, 0),
                size = resolution / 2,
            });
            originSparse.AddLast(new TextureSparse
            {
                offset = resolution / 2,
                size = resolution / 2,
            });
        }
        public TextureSparse GetSparse(int resolution)
        {
            return GetNewSparse((int)(0.01f + log2(resolution)));
        }
        private TextureSparse GetNewSparse(int index)
        {
            if (index >= allSparse.Length) throw new Exception("Out of Range!");
            NativeLinkedList<TextureSparse> currentSparseList = allSparse[index];
            if (currentSparseList.Length > 0)
            {
                TextureSparse sparse = *currentSparseList.GetLast();
                currentSparseList.RemoveLast();
                return sparse;
            }
            TextureSparse nextSparse = GetNewSparse(index + 1);
            int size = nextSparse.size / 2;
            currentSparseList.AddLast(new TextureSparse
            {
                offset = nextSparse.offset,
                size = size,
            });
            currentSparseList.AddLast(new TextureSparse
            {
                offset = nextSparse.offset + int2(0, size),
                size = size,
            });
            currentSparseList.AddLast(new TextureSparse
            {
                offset = nextSparse.offset + int2(size, 0),
                size = size,
            });
            return new TextureSparse
            {
                offset = nextSparse.offset + size,
                size = size,
            };
        }

        public void CombineTexture()
        {
            for (int i = 0; i < allSparse.Length - 1; ++i)
            {
                NativeLinkedList<TextureSparse> sparse = allSparse[i];
                NativeList<TextureSparse> zeroPos = new NativeList<TextureSparse>(sparse.Length, Allocator.Temp);
                foreach(var j in sparse)
                {
                    bool2 ald = IsSparseAligned(j);
                    if (ald.x && ald.y)
                        zeroPos.Add(j);
                }
                foreach(var j in zeroPos)
                {
                    TextureSparse* array = stackalloc TextureSparse[] {
                        new TextureSparse
                        {
                            offset = j.offset + int2(j.size, 0),
                            size = j.size,
                        },
                        new TextureSparse
                        {
                            offset = j.offset + int2(0, j.size),
                            size = j.size,
                        },
                        new TextureSparse
                        {
                            offset = j.offset + j.size,
                            size = j.size,
                        },
                        j
                    };
                    if(sparse.ContainsDatas(array, 3))
                    {
                        sparse.RemoveData(array, 4);
                        allSparse[i + 1].AddLast(new TextureSparse
                        {
                            offset = j.offset,
                            size = j.size * 2,
                        });
                    }
                }
            }
        }
        public void PushSparseBack(TextureSparse sparse)
        {
            int index = (int)(0.01f + log2(sparse.size));
            allSparse[index].AddLast(sparse);
        }
        public void Dispose()
        {
            allSparse.Dispose();
        }
    }

    public unsafe struct ArrayOfNativeLink<T> where T : unmanaged
    {
        private UIntPtr mainArray;
        private Allocator alloc;
        public int Length { get; private set; }
        public ArrayOfNativeLink(int arraySize, Allocator alloc, Func<T, T, bool> func)
        {
            this.alloc = alloc;
            mainArray = new UIntPtr(MUnsafeUtility.Malloc(UnsafeUtility.SizeOf<NativeLinkedList<T>>() * arraySize, alloc));
            Length = arraySize;
            for (int i = 0; i < arraySize; ++i)
            {
                SetList(i, new NativeLinkedList<T>(alloc, func));
            }
        }
        public void Dispose()
        {
            for (int i = 0; i < Length; ++i)
            {
                GetList(i).Dispose();
            }
            UnsafeUtility.Free(mainArray.ToPointer(), alloc);
        }
        public NativeLinkedList<T> this[int index]
        {
            get
            {
                NativeLinkedList<T> value = new NativeLinkedList<T>();
                UnsafeUtility.MemCpy(UnsafeUtility.AddressOf(ref value), (mainArray + index * UnsafeUtility.SizeOf<NativeLinkedList<T>>()).ToPointer(), UnsafeUtility.SizeOf<NativeLinkedList<T>>());
                return value;
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private NativeLinkedList<T> GetList(int index)
        {
            NativeLinkedList<T> value = new NativeLinkedList<T>();
            UnsafeUtility.MemCpy(UnsafeUtility.AddressOf(ref value), (mainArray + index * UnsafeUtility.SizeOf<NativeLinkedList<T>>()).ToPointer(), UnsafeUtility.SizeOf<NativeLinkedList<T>>());
            return value;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetList(int index, NativeLinkedList<T> list)
        {
            UnsafeUtility.MemCpy((mainArray + index * UnsafeUtility.SizeOf<NativeLinkedList<T>>()).ToPointer(), UnsafeUtility.AddressOf(ref list), UnsafeUtility.SizeOf<NativeLinkedList<T>>());
        }
    }

}
