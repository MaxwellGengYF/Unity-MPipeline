using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MPipeline
{
    [System.Serializable]
    internal struct LPProbe
    {
        [SerializeField]
        public Vector3 position;
        [SerializeField]
        public float skyVisibility;
        [SerializeField]
        public int surfelGroupPtr, surfelGroupCount;
    }

    [System.Serializable]
    internal struct LPSurfel
    {
        [SerializeField]
        public Vector3 position, normal, albedo;
    }

    [System.Serializable]
    internal struct IdWeight
    {
        [SerializeField]
        public int id;
        [SerializeField]
        public float weight;
        public IdWeight(int id, float weight)
        {
            this.id = id;
            this.weight = weight;
        }
    }

    [System.Serializable]
    internal struct LPSurfelGroup
    {
        [SerializeField]
        public int surfelPtr;
        [SerializeField]
        public int surfelCount;
    }

}