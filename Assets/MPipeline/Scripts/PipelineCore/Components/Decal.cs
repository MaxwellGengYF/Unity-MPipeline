using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using UnityEngine.Rendering;
using static Unity.Mathematics.math;
namespace MPipeline
{
    [ExecuteInEditMode]
    public unsafe class Decal : MonoBehaviour
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
        public int texIndex;
        private void OnEnable()
        {
            if (!decalDatas.isCreated) decalDatas = new NativeList<DecalData>(10, Unity.Collections.Allocator.Persistent);
            index = decalDatas.Length;
            decalDatas.Add(
                new DecalData
                {
                    position = transform.position,
                    rotation = transform.localToWorldMatrix,
                    worldToLocal = transform.worldToLocalMatrix,
                    index = texIndex,
                    comp = MUnsafeUtility.GetManagedPtr(this)
                });
        }
        [EasyButtons.Button]
        void UpdateData()
        {
            ref DecalData da = ref decalDatas[index];
            da.position = transform.position;
            da.rotation = transform.localToWorldMatrix;
            da.worldToLocal = transform.worldToLocalMatrix;
            da.index = texIndex;
        }

        private void OnDisable()
        {
            if (!decalDatas.isCreated) return;
            Decal lastDec = MUnsafeUtility.GetObject<Decal>(decalDatas[decalDatas.Length - 1].comp);
            lastDec.index = index;
            decalDatas[index] = decalDatas[decalDatas.Length - 1];
            decalDatas.RemoveLast();
        }
    }
}
