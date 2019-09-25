#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public unsafe sealed class SequenceFrameGenerator : MonoBehaviour
{
    public Camera cam;
    public Animator[] targetAnimators;
    public string stateName;
    public string dataPath = "Assets/SequenceTest.asset";
    public int targetFPS = 30;
    public float targetSecond = 5;
    public int targetSize = 256;
    public ComputeShader readPixelShader;
    private float previousTargetFPS;
    private WaitForFixedUpdate fix;
    private Color[] colorArray;
    private Texture2DArray result;
    private ComputeBuffer texDataBuffer;
    private RenderTexture tempRT;
    void InitializeSettings()
    {
        previousTargetFPS = Time.fixedDeltaTime;
        Time.fixedDeltaTime = targetFPS;
        cam.aspect = 1;
        colorArray = new Color[targetSize * targetSize];
        fix = new WaitForFixedUpdate();
        int frameCount = (int)(targetFPS * targetSecond);
        result = new Texture2DArray(targetSize, targetSize, frameCount, TextureFormat.RGBAHalf, false, true);
        texDataBuffer = new ComputeBuffer(colorArray.Length, sizeof(Color));
        tempRT = new RenderTexture(new RenderTextureDescriptor
        {
            width = targetSize,
            height = targetSize,
            volumeDepth = 1,
            colorFormat = RenderTextureFormat.ARGBHalf,
            enableRandomWrite = true,
            dimension = UnityEngine.Rendering.TextureDimension.Tex2D,
            msaaSamples = 1,
            depthBufferBits = 32
        });
        tempRT.Create();
        cam.targetTexture = tempRT;
        cam.enabled = false;
        cam.aspect = 1;
    }
    void FinishSettings()
    {
        Time.fixedDeltaTime = previousTargetFPS;
        DestroyImmediate(tempRT);
        texDataBuffer.Dispose();
    }
    [EasyButtons.Button]
    void Generate()
    {
        if(!Application.isPlaying)
        {
            Debug.LogError("Should run in playing mode!");
            return;
        }
        InitializeSettings();
        StartCoroutine(Record());
    }

    IEnumerator Record()
    {
        yield return fix;
        foreach (var i in targetAnimators)
        {
            i.Play(stateName, -1, 0);
        }
        int frameCount = (int)(targetFPS * targetSecond);
        readPixelShader.SetBuffer(0, "_TextureDatas", texDataBuffer);
        readPixelShader.SetTexture(0, "_TargetTexture", tempRT);
        readPixelShader.SetInt("_Width", targetSize);
        readPixelShader.SetInt("_Height", targetSize);
        for(int i = 0; i < frameCount; ++i)
        {
            cam.Render();
            readPixelShader.Dispatch(0, targetSize / 8, targetSize / 8, 1);
            texDataBuffer.GetData(colorArray);
            result.SetPixels(colorArray, i, 0);
            yield return fix;
        }
        result.Apply();
        UnityEditor.AssetDatabase.CreateAsset(result, dataPath);
        Debug.Log("Finish");
        FinishSettings();
    }
}
#endif