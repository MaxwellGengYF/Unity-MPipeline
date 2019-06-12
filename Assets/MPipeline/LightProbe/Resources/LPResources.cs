using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MPipeline
{
    internal class LPResources : ScriptableObject
    {
        [SerializeField]
        public ComputeShader GetSurfelIntersect;
        [SerializeField]
        public ComputeShader GetSurfelFromGBuffer;
    }
}