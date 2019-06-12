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
        public Vector3 position, normal;
        [SerializeField]
        public Color albedo;
    }

    [System.Serializable]
    internal struct IdWeight
    {
        [SerializeField]
        public int id;
        [SerializeField]
        public float weight;
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