using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace MPipeline
{
    [System.Serializable]
    public class RapidBlur
    {
        #region Variables
        [Range(0, 6), Tooltip("[降采样次数]向下采样的次数。此值越大,则采样间隔越大,需要处理的像素点越少,运行速度越快。")]
        public int DownSampleNum = 2;
        [Range(0.0f, 20.0f), Tooltip("[模糊扩散度]进行高斯模糊时，相邻像素点的间隔。此值越大相邻像素间隔越远，图像越模糊。但过大的值会导致失真。")]
        public float BlurSpreadSize = 3.0f;
        [Range(0, 8), Tooltip("[迭代次数]此值越大,则模糊操作的迭代次数越多，模糊效果越好，但消耗越大。")]
        public int BlurIterations = 3;

        #endregion

        #region MaterialGetAndSet
        private Material material;
        #endregion

        #region Functions

        public void Init(Shader shader)
        {
            material = new Material(shader);
        }

        public bool Check() { return material; }

        private static readonly int _DownSampleValue = Shader.PropertyToID("_DownSampleValue");
        private static readonly int _TempRT1 = Shader.PropertyToID("_TempRT1");
        private static readonly int _TempRT2 = Shader.PropertyToID("_TempRT2");
        public int Render(CommandBuffer buffer, Vector2Int camSize, RenderTargetIdentifier source)
        {
            float widthMod = 1.0f / (1.0f * (1 << DownSampleNum));
            buffer.SetGlobalFloat(_DownSampleValue, BlurSpreadSize * widthMod);
            int renderWidth = camSize.x >> DownSampleNum;
            int renderHeight = camSize.y >> DownSampleNum;
            int renderBuffer = _TempRT1;
            buffer.GetTemporaryRT(renderBuffer, renderWidth, renderHeight, 0, FilterMode.Bilinear, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            buffer.Blit(source, _TempRT1, material, 0);
            for (int i = 0; i < BlurIterations; i++)
            {
                float iterationOffs = (i * 1.0f);
                buffer.SetGlobalFloat(_DownSampleValue, BlurSpreadSize * widthMod + iterationOffs);

                int tempBuffer = _TempRT2;
                buffer.GetTemporaryRT(tempBuffer, renderWidth, renderHeight, 0, FilterMode.Bilinear, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
                buffer.Blit(renderBuffer, tempBuffer, material, 1);
                buffer.ReleaseTemporaryRT(renderBuffer);
                renderBuffer = tempBuffer;

                tempBuffer = _TempRT1;
                buffer.GetTemporaryRT(tempBuffer, renderWidth, renderHeight, 0, FilterMode.Bilinear, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
                buffer.Blit(renderBuffer, tempBuffer, material, 2);
                buffer.ReleaseTemporaryRT(renderBuffer);
                renderBuffer = tempBuffer;
            }
            return renderBuffer;
        }

        public void Dispose()
        {
            if (material)
            {
                Object.DestroyImmediate(material);
            }

        }

        #endregion

    }
}