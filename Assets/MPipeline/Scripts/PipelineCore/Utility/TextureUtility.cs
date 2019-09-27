using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
namespace MPipeline
{
    public unsafe static class TextureUtility
    {
        private static byte[] bytes = null;
        public static Texture2D SaveTex(RenderTexture targetRT, TextureFormat format)
        {
            Texture2D tex = new Texture2D(targetRT.width, targetRT.height, format, false, true);
            RenderTexture.active = targetRT;
            tex.ReadPixels(new Rect(0, 0, targetRT.width, targetRT.height), 0, 0);
            tex.Apply();
            return tex;
        }
        public static void SaveData<T>(T* pointer, int length, string path) where T : unmanaged
        {
            using(FileStream fs = new FileStream(path, FileMode.CreateNew))
            {
                if(bytes == null || bytes.Length < (length * sizeof(T)))
                {
                    bytes = new byte[length * sizeof(T)];
                    UnsafeUtility.MemCpy(bytes.Ptr(), pointer, length * sizeof(T));
                    fs.Write(bytes, 0, length * sizeof(T));
                }
            }
        }

        public static bool ReadData<T>(string path, Allocator allocator, out NativeArray<T> array) where T : unmanaged
        {
            if (!File.Exists(path))
            {
                array = new NativeArray<T>();
                return false;
            }
            using (FileStream fs = new FileStream(path, FileMode.Open))
            {
                int length = (int)(fs.Length / sizeof(T));
                if(bytes == null || bytes.Length < length)
                {
                    bytes = new byte[length];
                }
                fs.Read(bytes, 0, length);
                array = new NativeArray<T>(length, allocator, NativeArrayOptions.UninitializedMemory);
                UnsafeUtility.MemCpy(array.GetUnsafePtr(), bytes.Ptr(), length * sizeof(T));
            }
            return true;
        }
    }
}