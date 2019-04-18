using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using UnityEngine.Rendering;
using static Unity.Mathematics.math;
namespace MPipeline
{

    public unsafe abstract class DecalBase : MonoBehaviour
    {
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
                    comp = MUnsafeUtility.GetManagedPtr(this)
                });
            Init();
        }

        public abstract void DrawDecal(CommandBuffer buffer);
        public abstract void OnDispose();
        public abstract void Init();
        private void OnDestroy()
        {
            DecalBase lastDec = MUnsafeUtility.GetObject<DecalBase>(decalDatas[decalDatas.Length - 1].comp);
            lastDec.index = index;
            decalDatas[index] = decalDatas[decalDatas.Length - 1];
            decalDatas.RemoveLast();
            OnDispose();
        }
    }
}
