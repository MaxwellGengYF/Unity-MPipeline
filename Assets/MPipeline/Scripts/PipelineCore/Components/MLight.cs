using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using UnityEngine.Rendering;
using System.Runtime.CompilerServices;
using UnityEngine.Jobs;
using MPipeline;
#if UNITY_EDITOR
using UnityEditor;
[CustomEditor(typeof(MLight))]
public class MLightEditor : Editor
{
    private MLight target;
    private void OnEnable()
    {
        target = serializedObject.targetObject as MLight;
    }
    public override void OnInspectorGUI()
    {
        if (!target.enabled || !target.gameObject.activeSelf) return;
        target.useShadow = EditorGUILayout.Toggle("Use Shadow", target.useShadow);
        target.useShadowCache = EditorGUILayout.Toggle("Use Shadow Cache", target.useShadowCache);
        target.spotNearClip = EditorGUILayout.Slider("Spot Nearclip", target.spotNearClip, 0.05f, target.light.range);
        target.smallSpotAngle = EditorGUILayout.Slider("Small Spotangle", target.smallSpotAngle, 0, target.light.spotAngle);
        target.shadowBias = EditorGUILayout.Slider("Shadow Bias", target.shadowBias, 0, 1f);
        if (target.light.type == LightType.Spot)
            target.iesIndex = EditorGUILayout.IntField("IES Atlas index", target.iesIndex);
        if (GUILayout.Button("Destroy ShadowCamera"))
            target.DestroyCamera();
        Undo.RecordObject(target, System.Guid.NewGuid().ToString());
    }
}
#endif
[DisallowMultipleComponent]
[ExecuteInEditMode]
public unsafe class MLight : MonoBehaviour
{
    public const int cubemapShadowResolution = 1024;
    public const int perspShadowResolution = 2048;
    public static List<MLight> avaliableCubemapIndices { get; private set; }
    public static List<MLight> avaliableSpotShadowIndices { get; private set; }
    private static NativeList<int> avaliableSpotPool;
    private static NativeList<int> avaliablePointPool;
    private static bool initialized = false;
    public float shadowBias = 0.1f;
    public static void Initialize()
    {
        if (initialized) return;
        initialized = true;
        avaliableCubemapIndices = new List<MLight>(CBDRSharedData.MAXIMUMPOINTLIGHTCOUNT);
        avaliableSpotShadowIndices = new List<MLight>(CBDRSharedData.MAXIMUMSPOTLIGHTCOUNT);
        avaliableSpotPool = new NativeList<int>(CBDRSharedData.MAXIMUMSPOTLIGHTCOUNT, Allocator.Persistent);
        avaliablePointPool = new NativeList<int>(CBDRSharedData.MAXIMUMPOINTLIGHTCOUNT, Allocator.Persistent);
        for (int i = 0; i < CBDRSharedData.MAXIMUMSPOTLIGHTCOUNT; ++i)
            avaliableSpotPool.Add(i);
        for (int i = 0; i < CBDRSharedData.MAXIMUMPOINTLIGHTCOUNT; ++i)
            avaliablePointPool.Add(i);
    }
    private int shadowIndex = -1;
    public int ShadowIndex
    {
        get { return shadowIndex; }
    }
    [SerializeField]
    private bool m_useShadow = false;
    public bool useShadow
    {
        get
        {
            return m_useShadow;
        }
        set
        {
            if (m_useShadow != value)
            {
                m_useShadow = value;
                updateShadowCache = true;
            }
        }
    }
    public bool updateShadowCache;
    [SerializeField]
    private bool m_useShadowCache;
    public bool useShadowCache
    {
        get
        {
            return m_useShadowCache;
        }
        set
        {
            if (m_useShadowCache != value)
            {
                m_useShadowCache = value;
                if (value)
                {
                    updateShadowCache = true;
                }
            }
        }
    }
    public float smallSpotAngle = 30;
    public float spotNearClip = 0.3f;
    public int iesIndex = -1;
    private static Dictionary<Light, MLight> lightDict = new Dictionary<Light, MLight>(47);
    public Light light { get; private set; }
    private bool useCubemap;
    public Camera shadowCam { get; private set; }
    public static void ClearLightDict()
    {
        lightDict.Clear();
    }
    public static MLight GetPointLight(Light light)
    {
        MLight mp;
        if (lightDict.TryGetValue(light, out mp)) return mp;
        mp = light.GetComponent<MLight>();
        if (mp)
        {
            lightDict[light] = mp;
            return mp;
        }
        mp = light.gameObject.AddComponent<MLight>();
        return mp;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool GetPointLight(Light light, out MLight mLight)
    {
        return lightDict.TryGetValue(light, out mLight);
    }
    public static void AddMLight(Light light)
    {
        MLight mp = light.GetComponent<MLight>();
        if (mp)
            lightDict[light] = mp;
        else
            mp = light.gameObject.AddComponent<MLight>();
    }
    public void CheckShadowCamera()
    {
        if (!shadowCam)
        {
            shadowCam = GetComponent<Camera>();
            if (!shadowCam)
            {
                shadowCam = gameObject.AddComponent<Camera>();
            }
            shadowCam.hideFlags = HideFlags.HideAndDontSave;
            shadowCam.ResetProjectionMatrix();
            shadowCam.ResetCullingMatrix();
            shadowCam.ResetWorldToCameraMatrix();
            shadowCam.enabled = false;
            shadowCam.allowMSAA = false;
            shadowCam.useOcclusionCulling = false;
            shadowCam.allowHDR = false;
            shadowCam.aspect = 1;
        }

    }
    public void CheckShadowSetting(bool isAvaliable)
    {
        if (isAvaliable && useShadow)
        {
            if (shadowIndex >= 0)
            {
                if (useCubemap != (light.type == LightType.Point))
                {
                    updateShadowCache = true;
                    useCubemap = (light.type == LightType.Point);
                    if (useCubemap)
                    {
                        avaliableSpotShadowIndices.Remove(this);
                        avaliableSpotPool.Add(shadowIndex);
                        if (avaliableCubemapIndices.Count < CBDRSharedData.MAXIMUMPOINTLIGHTCOUNT)
                        {
                            shadowIndex = avaliablePointPool[avaliablePointPool.Length - 1];
                            avaliablePointPool.RemoveLast();
                            avaliableCubemapIndices.Add(this);
                        }
                        else
                        {
                            shadowIndex = -1;
                        }
                    }
                    else
                    {
                        avaliableCubemapIndices.Remove(this);
                        avaliablePointPool.Add(shadowIndex);
                        if (avaliableSpotShadowIndices.Count < CBDRSharedData.MAXIMUMSPOTLIGHTCOUNT)
                        {
                            shadowIndex = avaliableSpotPool[avaliableSpotPool.Length - 1];
                            avaliableSpotPool.RemoveLast();
                            avaliableSpotShadowIndices.Add(this);
                        }
                        else
                        {
                            shadowIndex = -1;
                        }
                    }
                }
            }
            else
            {
                updateShadowCache = true;
                useCubemap = (light.type == LightType.Point);
                if (useCubemap)
                {
                    if (avaliableCubemapIndices.Count < CBDRSharedData.MAXIMUMPOINTLIGHTCOUNT)
                    {
                        shadowIndex = avaliablePointPool[avaliablePointPool.Length - 1];
                        avaliablePointPool.RemoveLast();
                        avaliableCubemapIndices.Add(this);
                    }
                }
                else
                {
                    if (avaliableSpotShadowIndices.Count < CBDRSharedData.MAXIMUMSPOTLIGHTCOUNT)
                    {
                        shadowIndex = avaliableSpotPool[avaliableSpotPool.Length - 1];
                        avaliableSpotPool.RemoveLast();
                        avaliableSpotShadowIndices.Add(this);
                    }
                }
            }
        }
        else
        {
            RemoveLightFromAtlas(useCubemap);
        }

    }

    public void RemoveLightFromAtlas(bool useCubemap)
    {
        if (useCubemap)
        {
            if (shadowIndex >= 0)
            {
                avaliablePointPool.Add(shadowIndex);
                avaliableCubemapIndices.Remove(this);
            }
        }
        else
        {
            if (shadowIndex >= 0)
            {
                avaliableSpotPool.Add(shadowIndex);
                avaliableSpotShadowIndices.Remove(this);
            }
        }
        shadowIndex = -1;
    }
    public void DestroyCamera()
    {
        DestroyImmediate(GetComponent<Camera>());
        shadowCam = null;
    }
    private void OnEnable()
    {
        light = GetComponent<Light>();
        Initialize();
        if (light.shadows != LightShadows.None)
        {
            useShadow = true;
            light.shadows = LightShadows.None;
        }
        lightDict[light] = this;
        updateShadowCache = true;
    }

    private void OnDisable()
    {
        if (light)
            lightDict.Remove(light);
        RemoveLightFromAtlas(useCubemap);

    }
}