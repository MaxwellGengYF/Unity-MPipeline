using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using MPipeline;
[ExecuteInEditMode]
[RequireComponent(typeof(Light))]
public class SunLight : MonoBehaviour
{
    public const int CASCADELEVELCOUNT = 4;
    public const int CASCADECLIPSIZE = CASCADELEVELCOUNT + 1;

    public static SunLight current = null;
    public bool enableShadow = true;
    [Range(1, 8192)]
    public int resolution = 2048;
    [Range(1, 1000)]
    public float farestZ = 500;
    public float firstLevelDistance = 10;
    public float secondLevelDistance = 25;
    public float thirdLevelDistance = 55;
    public float farestDistance = 100;
    public bool sunlightOnlyForVolumetricLight = false;
    public Vector4 bias = new Vector4(0.1f, 0.2f, 0.3f, 0.4f);
    public LayerMask shadowMask = -1;
    public Vector4 cascadeSoftValue = new Vector4(1.5f, 1.2f, 0.9f, 0.7f);
    [System.NonSerialized] public Material shadowDepthMaterial;
    [System.NonSerialized] public RenderTexture shadowmapTexture;
    [System.NonSerialized] public NativeArray<AspectInfo> shadowFrustumPlanes;
    [System.NonSerialized] public Light light;
    [System.NonSerialized] public OrthoCam shadCam;
    public static Camera shadowCam { get; private set; }
    public static NativeList_Int[] customCullResults = new NativeList_Int[CASCADELEVELCOUNT];
    private void OnEnable()
    {
        if (current)
        {
            if (current != this)
            {
                Debug.Log("Sun Light Should be Singleton!");
                Destroy(light);
                Destroy(this);
                return;
            }
            else
                OnDisable();
        }
        light = GetComponent<Light>();
        current = this;
        shadCam.forward = transform.forward;
        shadCam.up = transform.up;
        shadCam.right = transform.right;
        light.enabled = false;
        GameObject GetChild(string name)
        {
            for (int i = 0; i < transform.childCount; ++i)
            {
                if (transform.GetChild(i).name == name)
                {
                    return transform.GetChild(i).gameObject;
                }
            }
            return null;
        }
        if (!shadowCam)
        {
            GameObject shadObj = GetChild("Sun_Shadow_Cam");
            if (!shadObj)
            {
                shadObj = new GameObject("Sun_Shadow_Cam");
            }
            shadowCam = shadObj.GetComponent<Camera>();
            if (!shadowCam)
            {
                shadObj.transform.SetParent(transform);
                shadObj.transform.localRotation = Quaternion.identity;
                shadObj.transform.localPosition = Vector3.zero;
                shadObj.transform.localScale = Vector3.one;
                shadObj.hideFlags = HideFlags.HideAndDontSave;
                shadowCam = shadObj.AddComponent<Camera>();
            }
            shadowCam.enabled = false;
            shadowCam.aspect = 1;
            shadowCam.orthographic = true;
            shadowCam.worldToCameraMatrix = Matrix4x4.identity;
            shadowCam.projectionMatrix = Matrix4x4.identity;
        }
        shadowmapTexture = new RenderTexture(new RenderTextureDescriptor
        {
            width = resolution,
            height = resolution,
            depthBufferBits = 16,
            colorFormat = RenderTextureFormat.Shadowmap,
            autoGenerateMips = false,
            bindMS = false,
            dimension = UnityEngine.Rendering.TextureDimension.Tex2DArray,
            enableRandomWrite = false,
            memoryless = RenderTextureMemoryless.None,
            shadowSamplingMode = UnityEngine.Rendering.ShadowSamplingMode.RawDepth,
            msaaSamples = 1,
            sRGB = false,
            useMipMap = false,
            volumeDepth = 4,
            vrUsage = VRTextureUsage.None
        });
        shadowmapTexture.filterMode = FilterMode.Bilinear;
        shadowDepthMaterial = new Material(Shader.Find("Hidden/ShadowDepth"));
        shadowFrustumPlanes = new NativeArray<AspectInfo>(3, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
    }

    private void Update()
    {
        shadCam.forward = transform.forward;
        shadCam.up = transform.up;
        shadCam.right = transform.right;
    }

    private void OnDisable()
    {
        if (current != this) return;
        current = null;
        if (shadowmapTexture)
        {
            shadowmapTexture.Release();
            DestroyImmediate(shadowmapTexture);
        }
        if (shadowDepthMaterial)
            DestroyImmediate(shadowDepthMaterial);
        if (shadowFrustumPlanes.IsCreated)
            shadowFrustumPlanes.Dispose();
    }
}
