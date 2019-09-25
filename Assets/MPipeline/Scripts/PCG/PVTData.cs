using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
#if UNITY_EDITOR
using UnityEditor;
#endif
namespace MPipeline.PCG
{
    [CreateAssetMenu(menuName = "PCG/PVT Data")]
    public class PVTData : ScriptableObject
    {
        [System.Serializable]
        public struct TextureBlendSettings
        {
            public Texture2D startAlbedo;
            public Texture2D startNormal;
            public Texture2D startSMO;
            public float4 scaleOffset;
            public Texture maskTex;
            [Range(-1f, 1f)]
            public float maskScale;
            [Range(-1f, 1f)]
            public float maskOffset;
        }
        public bool realtimeUpdate = false;
        public int2 targetSize = new int2(2048, 2048);
        public List<TextureBlendSettings> allSettings = new List<TextureBlendSettings>();
        [System.NonSerialized]
        public bool shouldUpdate = false;
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(PVTData))]
    public class PVTDataEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            PVTData data = serializedObject.targetObject as PVTData;
            base.OnInspectorGUI();
            if (!data.realtimeUpdate)
            {
                if (GUILayout.Button("Update Texture"))
                    data.shouldUpdate = true;
            }
            else
                data.shouldUpdate = true;
        }
    }

#endif
}