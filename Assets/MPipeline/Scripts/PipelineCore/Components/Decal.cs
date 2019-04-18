using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
namespace MPipeline
{

    public unsafe class Decal : MonoBehaviour
    {
        public Texture2D decalTex;
        public Texture2D normalTex;
        public float normalScale = 0.1f;
        public Color albedoColor = Color.white;
        private static NativeList<DecalData> decalDatas;
        public static int allDecalCount
        {
            get
            {
                if (decalDatas.isCreated)
                    return decalDatas.Length;
                return 0;
            }
        }
        public static ref DecalData GetData(int index)
        {
            return ref decalDatas[index];
        }
        private int index;
        private void Awake()
        {
            if (!decalDatas.isCreated) decalDatas = new NativeList<DecalData>(10, Unity.Collections.Allocator.Persistent);
            decalDatas.Add(
                new DecalData
                {
                    position = transform.position,
                    rotation = transform.localToWorldMatrix,
                    normalScale = normalScale,
                    opaque = albedoColor.a,
                    color = float3(albedoColor.r, albedoColor.g, albedoColor.b),
                    comp = MUnsafeUtility.GetManagedPtr(this)
                });
        }

        private void OnDestroy()
        {
            Decal lastDec = MUnsafeUtility.GetObject<Decal>(decalDatas[decalDatas.Length - 1].comp);
            lastDec.index = index;
            decalDatas[index] = decalDatas[decalDatas.Length - 1];
            decalDatas.RemoveLast();
        }
    }
}
