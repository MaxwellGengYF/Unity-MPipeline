using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

[System.Serializable]
public unsafe class CyberColor
{
    public bool Enabled = false;
    #region VARIABLE_POS
    [Range(-1f, 0.5f)]
    public float _Red = 0;
    [Range(-0.5f, 0.5f)]
    public float _Orange = 0;
    [Range(-0.5f, 1f)]
    public float _Yellow = 0;
    [Range(-1f, 1f)]
    public float _Green = 0;
    [Range(-1f, 1f)]
    public float _Cyan = 0;
    [Range(-1f, 0.5f)]
    public float _Blue = 0;
    [Range(-0.5f, 0.5f)]
    public float _Purple = 0;
    [Range(-0.5f, 1f)]
    public float _Magenta = 0;
    [Range(1f, 10f)]
    public float _Pow_S = 1;
    [Range(0f, 2f)]
    public float _Value = 1;
    #endregion
    private ComputeBuffer dataBuffer;
    private static readonly int _CyberVar = Shader.PropertyToID("_CyberVar");
    public void Init()
    {
        dataBuffer = new ComputeBuffer(1, sizeof(float) * 10);
    }

    public bool Check()
    {
        return dataBuffer != null && dataBuffer.IsValid();
    }

    public void FrameUpdate(CommandBuffer buffer)
    {
        if(Enabled)
        {
            buffer.EnableShaderKeyword("ENABLE_CYBERCOLOR");
            NativeArray<float> datas = new NativeArray<float>(10, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            UnsafeUtility.MemCpy(datas.GetUnsafePtr(), UnsafeUtility.AddressOf(ref _Red), sizeof(float) * 10);
            dataBuffer.SetData(datas);
            buffer.SetGlobalBuffer(_CyberVar, dataBuffer);
        }
        else
        {
            buffer.DisableShaderKeyword("ENABLE_CYBERCOLOR");
        }
    }

    public void Dispose()
    {
        dataBuffer.Dispose();
    }
}
