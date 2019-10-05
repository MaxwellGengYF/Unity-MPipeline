using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
public abstract class DecalAtlasBase : MonoBehaviour
{
    public abstract void Init();
    public abstract void FrameUpdate(CommandBuffer buffer,
        RenderTexture targetAlbedo,
        int targetAlbedoElement);
    public abstract void Dispose();
}
