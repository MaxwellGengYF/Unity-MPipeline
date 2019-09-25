using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using Unity.Mathematics;
using UnityEngine.Rendering;
using static Unity.Mathematics.math;
namespace MPipeline
{
    public unsafe static class HeightmapTexture
    {
        private static void PackHeightmap(Texture2D[,] allHeightmap, int textureSize, int lodLevel, string path, string terrainName)
        {
            ComputeShader shader = Resources.Load<ComputeShader>("MipmapCompute");
            ComputeBuffer readBuffer = new ComputeBuffer(textureSize * textureSize, sizeof(float));
            MStringBuilder sb = new MStringBuilder(path.Length + terrainName.Length + 15);
            sb.Add(path);
            if (path[path.Length - 1] != '/')
            {
                sb.Add("/");
            }
            sb.Add(terrainName);
            path += terrainName;
            if (Directory.Exists(sb.str))
                Directory.Delete(sb.str);
            Directory.CreateDirectory(sb.str);
            int pathLength = sb.str.Length;
            for (int i = 0; i < lodLevel; ++i)
            {
                sb.Resize(pathLength);
                sb.Add("/LOD" + i.ToString());
                Directory.CreateDirectory(sb.str);
            }
            sb.Resize(pathLength);
            sb.Add("/LOD0");
            for (int x = 0; x < allHeightmap.GetLength(0); ++x)
                for (int y = 0; y < allHeightmap.GetLength(1); ++y)
                {
                    Texture2D tex = allHeightmap[x, y];
                    if (tex.width != textureSize ||
                        tex.height != textureSize)
                    {
                        readBuffer.Dispose();
                        Resources.UnloadAsset(shader);
                        throw new System.Exception("Texture " + tex.name + " setting is not right!(Width, Height, isReadable)");
                    }
                }
            shader.SetBuffer(1, "_OutputBuffer", readBuffer);
            float[] result = new float[textureSize * textureSize];
            void SaveTexture(StreamWriter writer, Texture tex)
            {
                shader.SetTexture(1, ShaderIDs._MainTex, tex);
                int kernelSize = Mathf.CeilToInt(textureSize / 8f);
                shader.Dispatch(1, kernelSize, kernelSize, 1);
                readBuffer.GetData(result);
                char[] chrArray = new char[result.Length * sizeof(half)];
                half* arrPtr = (half*)chrArray.Ptr();
                for (int i = 0; i < result.Length; ++i)
                {
                    arrPtr[i] = (half)result[i];
                }
                writer.Write(chrArray);
            }
            using (StreamWriter writer = new StreamWriter(sb.str))
            {
                for (int x = 0; x < allHeightmap.GetLength(0); ++x)
                    for (int y = 0; y < allHeightmap.GetLength(1); ++y)
                    {
                        Texture2D tex = allHeightmap[x, y];
                        SaveTexture(writer, tex);
                    }
            }
            readBuffer.Dispose();
            Resources.UnloadAsset(shader);
        }
    }
}