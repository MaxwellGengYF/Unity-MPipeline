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
        public LPProbe(Vector3 pos) { position = pos; }
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
    internal class LPSurfelGroup
    {
        [SerializeField]
        public int[] surfelId;
        [SerializeField]
        public IdWeight[] influncedProbeIdWeight;
    }

}