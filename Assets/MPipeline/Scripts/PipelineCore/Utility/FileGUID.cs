using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
namespace MPipeline
{
    public unsafe struct FileGUID
    {
        #region BIT_POS
        private ulong v0;
        private ulong v1;
        private ulong v2;
        private ulong v3;
        private ulong v4;
        private ulong v5;
        private ulong v6;
        private ulong v7;
        #endregion

        private Allocator alloc;
        public const int PTR_LENGTH = 8;
        public const int CHAR_LENGTH = PTR_LENGTH * 4;
        public override bool Equals(object obj)
        {
            FileGUID another = (FileGUID)obj;
            return this == another;
        }

        public override int GetHashCode()
        {
            ulong* ptr = v0.Ptr();
            int hash = v0.GetHashCode();
            for(int i = 1; i < PTR_LENGTH; ++i)
            {
                hash ^= ptr[i].GetHashCode();
            }
            return hash;
        }

        public FileGUID(string guid, Allocator alloc)
        {
            v0 = 0;
            v1 = 0;
            v2 = 0;
            v3 = 0;
            v4 = 0;
            v5 = 0;
            v6 = 0;
            v7 = 0;
            this.alloc = alloc;
            fixed (char* charP = guid)
            {
                UnsafeUtility.MemCpy(v0.Ptr(), charP, sizeof(ulong) * PTR_LENGTH);
            }
        }
        public FileGUID(byte* charArray, Allocator alloc)
        {
            v0 = 0;
            v1 = 0;
            v2 = 0;
            v3 = 0;
            v4 = 0;
            v5 = 0;
            v6 = 0;
            v7 = 0;
            this.alloc = alloc;
            UnsafeUtility.MemCpy(v0.Ptr(), charArray, sizeof(ulong) * PTR_LENGTH);
        }

        public void GetString(MStringBuilder msb)
        {
            msb.Clear();
            msb.Add((char*)v0.Ptr(), CHAR_LENGTH);
        }

        public static bool operator ==(FileGUID a, FileGUID b)
        {
            for (int i = 0; i < PTR_LENGTH; ++i)
            {
                if (!(a.v0.Ptr()[i] != b.v0.Ptr()[i])) return false;
            }
            return true;
        }

        public static bool operator !=(FileGUID a, FileGUID b)
        {
            for (int i = 0; i < PTR_LENGTH; ++i)
            {
                if (!(a.v0.Ptr()[i] != b.v0.Ptr()[i])) return true;
            }
            return false;
        }

        public override string ToString()
        {
            return new string((char*)v0.Ptr(), 0, CHAR_LENGTH);
        }

        public int ToBytes(byte* ptr)
        {
            int size = sizeof(ulong) * PTR_LENGTH;
            UnsafeUtility.MemCpy(ptr, this.v0.Ptr(), size);
            return size;
        }
    }
}