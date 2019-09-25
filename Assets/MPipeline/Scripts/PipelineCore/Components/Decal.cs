using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using UnityEngine.Rendering;
using static Unity.Mathematics.math;
#if UNITY_EDITOR
using UnityEditor;
#endif
namespace MPipeline
{
    [ExecuteInEditMode]
    public unsafe class Decal : MonoBehaviour
    {
        public static NativeList<DecalData> decalDatas { get; private set; }
        public static int allDecalCount
        {
            get
            {
                if (decalDatas.isCreated)
                    return decalDatas.Length;
                return 0;
            }
        }
        private int index;
        public float avaliableDistance = 30;
        [Range(0f, 1f)]
        public float albedoOpacity = 1;
        [Range(0f, 1f)]
        public float normalOpacity = 1;
        [Range(0f, 1f)]
        public float specularOpacity = 1;
        public float4 albedoScaleOffset = float4(1,1,0,0);
        public float4 normalScaleOffset = float4(1, 1, 0, 0);
        public float4 specularScaleOffset = float4(1, 1, 0, 0);
        public int importance = 0;
        public int albedoIndex = -1;
        public int normalIndex = -1;
        public int specularIndex = -1;
        public float2 heightmapScaleOffset = float2(0, 1);
        [HideInInspector]
        public int layer = 0;
        private void OnEnable()
        {
            if (!decalDatas.isCreated) decalDatas = new NativeList<DecalData>(10, Unity.Collections.Allocator.Persistent);
            index = decalDatas.Length;
            float4x4 worldToLocal = transform.worldToLocalMatrix;
            float4x4 localToWorld = transform.localToWorldMatrix;
            decalDatas.Add(
                new DecalData
                {
                    localToWorld = float3x4(localToWorld.c0.xyz, localToWorld.c1.xyz, localToWorld.c2.xyz, localToWorld.c3.xyz),
                    worldToLocal = float3x4(worldToLocal.c0.xyz, worldToLocal.c1.xyz, worldToLocal.c2.xyz, worldToLocal.c3.xyz),
                    albedoScaleOffset = albedoScaleOffset,
                    normalScaleOffset = normalScaleOffset,
                    specularScaleOffset = specularScaleOffset,
                    importance = importance,
                    comp = MUnsafeUtility.GetManagedPtr(this),
                    texIndex = int3(albedoIndex, normalIndex, specularIndex),
                    avaliableDistance = avaliableDistance,
                    opacity = float3(albedoOpacity, normalOpacity, specularOpacity),
                    layer = 1<<layer ,
                    heightScaleOffset = heightmapScaleOffset
                });
        }
        public void UpdateData()
        {
            ref DecalData da = ref decalDatas[index];
            float4x4 worldToLocal = transform.worldToLocalMatrix;
            float4x4 localToWorld = transform.localToWorldMatrix;
            da.worldToLocal = float3x4(worldToLocal.c0.xyz, worldToLocal.c1.xyz, worldToLocal.c2.xyz, worldToLocal.c3.xyz);
            da.localToWorld = float3x4(localToWorld.c0.xyz, localToWorld.c1.xyz, localToWorld.c2.xyz, localToWorld.c3.xyz);
            da.albedoScaleOffset = albedoScaleOffset;
            da.normalScaleOffset = normalScaleOffset;
            da.specularScaleOffset = specularScaleOffset;
            da.importance = importance;
            da.texIndex = int3(albedoIndex, normalIndex, specularIndex);
            da.avaliableDistance = avaliableDistance;
            da.opacity = float3(albedoOpacity, normalOpacity, specularOpacity);
            da.layer = 1<<layer;
            da.heightScaleOffset = heightmapScaleOffset;
        }

        private void OnDisable()
        {
            if (!decalDatas.isCreated) return;
            Decal lastDec = MUnsafeUtility.GetObject<Decal>(decalDatas[decalDatas.Length - 1].comp);
            lastDec.index = index;
            decalDatas[index] = decalDatas[decalDatas.Length - 1];
            decalDatas.RemoveLast();
        }

        private void OnDrawGizmos()
        {
            Gizmos.DrawIcon(transform.position, "Decal");
        }

        private void OnDrawGizmosSelected()
        {
            GizmosHelper gz = new GizmosHelper();
            gz.Init();
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1, 1, 1, 0.4f);
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);

            gz.Dispose();

            gz = new GizmosHelper();
            gz.Init();
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0, 0, 1, 0.2f);
            Gizmos.DrawCube(Vector3.zero, Vector3.one);
            gz.Dispose();

            UpdateData();
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(Decal))]
    public class DecalEditor : Editor
    {
        public enum DecalLayer
        {
            Layer0 = 0,
            Layer1 = 1,
            Layer2 = 2,
            Layer3 = 3,
            Layer4 = 4,
            Layer5 = 5,
            Layer6 = 6,
            Layer7 = 7,
            Layer8 = 8,
            Layer9 = 9,
            Layer10 = 10,
            Layer11 = 11,
            Layer12 = 12,
            Layer13 = 13,
            Layer14 = 14,
            Layer15 = 15,
            Layer16 = 16,
            Layer17 = 17,
            Layer18 = 18,
            Layer19 = 19,
            Layer20 = 20,
            Layer21 = 21,
            Layer22 = 22,
        }
        public override void OnInspectorGUI()
        {
            Decal tar = serializedObject.targetObject as Decal;
            Undo.RecordObject(tar, tar.GetInstanceID().ToString());
            tar.layer = (int)(DecalLayer)EditorGUILayout.EnumPopup("Decal Layer: ", (DecalLayer)tar.layer);
            base.OnInspectorGUI();
        }
    }
#endif
}
