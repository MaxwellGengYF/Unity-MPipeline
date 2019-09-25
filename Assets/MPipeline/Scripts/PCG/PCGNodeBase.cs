#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace MPipeline.PCG
{

    public abstract class PCGNodeBase : MonoBehaviour
    {
        [SerializeField]
        private uint randomSeed;
        public uint RandomSeed
        {
            get
            {
                randomSeed = randomSeed == 0 ? 1 : randomSeed;
                return randomSeed;
            }
            set { randomSeed = value; }
        }
        public static List<PCGNodeBase> allNodeBases = new List<PCGNodeBase>(20);
        private static PCGResources m_initRes = null;
        public static PCGResources initRes
        {
            get
            {
                return m_initRes;
            }
            set
            {
                if (!m_initRes)
                {
                    foreach (var i in allNodeBases)
                        i.Init(value);
                }
                m_initRes = value;
            }
        }
        public abstract NativeList<Point> GetPointsResult(out Material[] targetMaterials);
        public abstract void Init(PCGResources resources);
        public abstract void Dispose();
        public abstract void UpdateSettings();
        public abstract Bounds GetBounds();
        public virtual void DrawDepthPrepass(CommandBuffer buffer) { }
        public virtual void DrawGBuffer(CommandBuffer buffer) { }
        private int index = -1;
        private void OnEnable()
        {
            index = allNodeBases.Count;
            allNodeBases.Add(this);
            if (m_initRes)
            {
                Init(m_initRes);
            }
        }

        private void OnDisable()
        {
            allNodeBases[index] = allNodeBases[allNodeBases.Count - 1];
            allNodeBases[index].index = index;
            index = -1;
            allNodeBases.RemoveAt(allNodeBases.Count - 1);
            Dispose();
        }
    }
}
#endif