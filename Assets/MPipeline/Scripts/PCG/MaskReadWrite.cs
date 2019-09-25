using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using System.IO;
namespace MPipeline
{
    public static unsafe class MaskReadWrite
    {
        private static byte[] byteArray;
        public static void ReadData(byte* targetPtr, int length, string path)
        {
            if(byteArray == null || byteArray.Length < length)
            {
                byteArray = new byte[length];
            }
            using (FileStream st = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                st.Read(byteArray, 0, length);
                UnsafeUtility.MemCpy(targetPtr, byteArray.Ptr(), length);
            }
        }

        public static void WriteTexture(string path, string saveName, Texture rt, int2 blendTarget)
        {
            string direct = path + "/" + saveName;
            if (!Directory.Exists(direct))
            {
                Directory.CreateDirectory(direct);
            }
            ComputeBuffer buffer = new ComputeBuffer(rt.width * rt.height, sizeof(float4));
            ComputeShader cs = Resources.Load<ComputeShader>("ReadRTData");
            cs.SetBuffer(0, "_TextureDatas", buffer);
            cs.SetTexture(0, "_TargetTexture", rt);
            cs.SetInt("_Width", rt.width);
            cs.SetInt("_Height", rt.height);
            cs.Dispatch(0, rt.width / 8, rt.height / 8, 1);
            

            MStringBuilder msb = new MStringBuilder(direct.Length + 25);
            msb.Add(direct);
            msb.Add('/');
            msb.Add(blendTarget.x.ToString());
            msb.Add('_');
            msb.Add(blendTarget.y.ToString());
            msb.Add(".bytes");
            Color[] cols = new Color[buffer.count];
            buffer.GetData(cols);
            byte[] bytes = new byte[cols.Length];
            for(int i = 0; i < cols.Length; ++i)
            {
                bytes[i] = (byte)(cols[i].r * 255.99999);
            }
            File.WriteAllBytes(msb.str, bytes);
        }

        public static void WriteGuide(string path, string saveName, NativeList<int2> blendTargets)
        {
            string direct = path + "/" + saveName + "/Guide.bytes";
            if (!Directory.Exists(direct))
            {
                Directory.CreateDirectory(direct);
            }
            MStringBuilder msb = new MStringBuilder(10 * blendTargets.Length);
            foreach(var i in blendTargets)
            {
                msb.Add(i.x.ToString());
                msb.Add('_');
                msb.Add(i.y.ToString());
                msb.Add("\r\n");
            }
            File.WriteAllText(direct, msb.str);
        }
    }
}